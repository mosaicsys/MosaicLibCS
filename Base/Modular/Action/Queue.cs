//-------------------------------------------------------------------
/*! @file Queue.cs
 * @brief This file contains the definitions and classes that are used to define the internal Action Queue for the Modular Action portions of this library.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
//-------------------------------------------------------------------

namespace MosaicLib.Modular.Action
{
	//-------------------------------------------------
	using System;
	using System.Collections.Generic;
	using MosaicLib.Utils;
	using MosaicLib.Time;

	//-------------------------------------------------
	/// <summary>
	/// This interface augments the IProviderFacet side interface with an IsStarted method that is used by the
	/// Queue's Enqueue operation to verify that the Action is in the correct state prior to accepting it into the queue.
	/// </summary>

	public interface IEnqueableProviderFacet : IProviderFacet
	{
		bool IsStarted { get; }
	}

	//-------------------------------------------------

	/// <summary>This class is used by Active Parts to enqueue the started Actions so they can be pulled by the part's service thread and get performed by it.</summary>
	/// <remarks>
	/// Note that the Enqueue method accepts an object of the IEnqueableProviderFacet type (used to verify that enqueued actions are in the started state)
	/// while the queue itself only retains the action as an IProviderFacet object which can be extracted from the queue using the GetNextAction method.
	/// In addition the QueueEnable property is frequently used by an active part to signal when the part should stop its internal thread.
	/// </remarks>

	public class ActionQueue
	{
		#region private class and instance fields

		const bool ActionQueueEnableDefault = false;
		const int ActionQueueSizeDefault = 10;

		string					mQueueName = null;
		volatile bool			mQueueEnabled = false;

		object					mQueueMutex = new object();
		
		BasicNotificationList	mNotifyOnEnqueueList = new BasicNotificationList();

		AtomicInt32				mPtrQueueCount = new AtomicInt32(0);
		IProviderFacet []		mPtrQueueArray = null;
		int						mPtrQueueArraySize = 0;
		int                     mPtrQueueArrayNextPutIdx = 0;
        int                     mPtrQueueArrayNextGetIdx = 0;

		AtomicInt32				mCancelRequestCount = new AtomicInt32(0);
		int						mLastServicedCancelRequestCount = 0;

		#endregion

		public ActionQueue(string name, bool enabled, int queueSize) 
		{
			mQueueName = name;
            mPtrQueueArray = new IProviderFacet[queueSize];
			mPtrQueueArraySize = mPtrQueueArray.Length;
            mQueueEnabled = enabled;
		}

		/// <summary>NotificationList that will be Notified when an action is enqueued.  Typically set to signal the part's thread wakeup notifier.</summary>
		public IBasicNotificationList NotifyOnEnqueue { get { return mNotifyOnEnqueueList; } }

		/// <summary>Enqueue's the given action in the queue provided that it is valid and the queue is enabled.</summary>
		/// <param name="action">Gives the action to enqueue.</param>
		/// <returns>Empty string on success, error message on failure.</returns>
		/// <remarks>
		/// The given action must be non-null and in the Started state inorder for this method to succeed.  
		/// In addition if the queue is not enabled or it full at the time the enqueue is requested, the given action will be completed with a non-empty 
		/// result code and the Enqueue operation will complete successfully.
		/// </remarks>

    	public string Enqueue(IEnqueableProviderFacet action)	//!< Note this method attempts to enqueue the given operation.  This method only returns an error string if the given operation is invalid or is not in a valid state to be enqueued.
		{
			if (action == null)
				return mQueueName + ".Enqueue.Failed.ActionIsNull";

			if (!action.IsStarted)
				return mQueueName + ".Enqueue.Failed.ActionHasNotBeenStarted";

			lock (mQueueMutex)
			{
				if (!mQueueEnabled)
				{
					action.CompleteRequest(mQueueName + ".Enqueue.Failed.QueueIsNotEnabled");
					return "";
				}

				ServiceCancelRequests();

				if (mPtrQueueCount.VolatileValue >= mPtrQueueArraySize)
				{
					action.CompleteRequest(mQueueName + ".Enqueue.Failed.QueueIsFull");
					return "";
				}

				mPtrQueueCount.Increment();

				mPtrQueueArray[mPtrQueueArrayNextPutIdx++] = action;
				if (mPtrQueueArrayNextPutIdx >= mPtrQueueArraySize)
					mPtrQueueArrayNextPutIdx = 0;

				mNotifyOnEnqueueList.Notify();

				return "";
			}
		}

		/// <summary>
		/// Quickly tests if there have been any cancel requests since the last time this method was invoked and if so, scans through the queue and
		/// completes and removes any actions that have their cancel request set.
		/// </summary>

		public void ServiceCancelRequests()
		{
			// NOTE we are only looking to detect change here.  
			//  Failure to detect a cancel request on any given pass is simply a form of acceptable race condition between the request and the 
			//  service pass that might react to it.  This optimization allows this method to be very cheap to use when no recent operation 
			//	abort requests have been issued.

			if (mCancelRequestCount.VolatileValue == mLastServicedCancelRequestCount)
				return;

            lock (mQueueMutex)
            {
                mLastServicedCancelRequestCount = mCancelRequestCount.Value;		// this is an atomic read of the volatile value

			    // Now go through the queued operations and complete any that are flaged
			    //	has CancelRequestActive.  For each of these we complete them and then
			    //	reset the correspondinging entry in the queue.  NOTE that the queue count
			    //	and getIdx are not changed as a side effect of this.  This means that the
			    //	GetNextAction method must be able to correctly skip queue entries that
			    //	have been reset since they were enqueued.

			    int idx = mPtrQueueArrayNextGetIdx, numChecked = 0;

			    for (; numChecked < mPtrQueueCount.VolatileValue; numChecked++)
			    {
				    IProviderFacet queueItem = mPtrQueueArray[idx];

					if (queueItem != null && queueItem.IsCancelRequestActive)
				    {
						queueItem.CompleteRequest(mQueueName + ":ServiceCancelRequests:ActionCanceledWhileEnqueued");
						mPtrQueueArray[idx] = null;
				    }

				    if (++idx >= mPtrQueueArraySize)
					    idx = 0;
			    }
            }
        }

        /// <summary>Returns a recent copy of the count of the number of objects in the queue.</summary>
        public Int32 VolatileCount { get { return mPtrQueueCount.VolatileValue; } }

        /// <summary>Returns true if the a recent (volatile) copy of the count of the number of objects in the queue is zero.</summary>
        public bool IsEmpty { get { return (VolatileCount == 0); } }

		/// <summary>Attempts to extract and return the next action in the queue.</summary>
		/// <returns>The next extracted action or null if the queue is empty.</returns>
        public IProviderFacet GetNextAction() { return GetNextAction(false); }

        /// <summary>Attempts to extract and return the next action in the queue.</summary>
        /// <returns>The next extracted action or null if the queue is empty.</returns>
        /// <param name="peekOnly">If this parameter is true then the returned action is not removed from the queue.</param>
        public IProviderFacet GetNextAction(bool peekOnly)
		{
			// before actually trying to obtain an object from the queue
			//	Use an asynchronous check to see if the queue is known to be empty
			//	in which case we just exit.

			int queueCount = mPtrQueueCount.VolatileValue;

			if (queueCount == 0)
				return null;

			// next, given that the queue was not empty when we just checked it, check for new abort requests on items that might be in this queue.
			ServiceCancelRequests();

			// attempt to extract the first non-null op from the queue and return it.
			//

			lock (mQueueMutex)
			{
				// loop until the queue is empty or we find a non-null item in the list
				for (; ; )
				{
					// retest that the queue is not empty 
					queueCount = mPtrQueueCount.VolatileValue;
					if (queueCount == 0)
						return null;

					IProviderFacet action = mPtrQueueArray [mPtrQueueArrayNextGetIdx];

                    if (peekOnly)
                        return action;

                    mPtrQueueArray [mPtrQueueArrayNextGetIdx] = null;

					if (++mPtrQueueArrayNextGetIdx >= mPtrQueueArraySize)
						mPtrQueueArrayNextGetIdx = 0;

					mPtrQueueCount.Decrement();

					// else loop again until we get a non-null one or we find that the queue was actually really empty
					if (action != null)
						return action;
				}
			}
		}

		/// <summary>
		/// This property may be tested to determine if the queue is currently enabled and it may be set to enable or disable the queue.
		/// When disabling the queue, all queued actions will be completed with a non-empty result code.
		/// </summary>
		public bool QueueEnable
		{
			get { lock (mQueueMutex) { return mQueueEnabled; } }
			set 
			{
				bool entryValue = false;
				lock (mQueueMutex) { entryValue = mQueueEnabled; mQueueEnabled = value; }

				if (!value && entryValue)
				{
					// The Queue has just been disabled
					// Iterate using GetNextAction and complete each operation that it returns until the queue is empty

					IProviderFacet action = null;

					while ((action = GetNextAction()) != null)
						action.CompleteRequest(mQueueName + ":DisableQueue:ActionHasBeenCanceled");
				}
			}
		}

		/// <summary>
		/// This asynchronous method is invoked by an action on its owning queue when the action gets canceled while in the Started state.
		/// This allows the queue to quickly determine when it needs to sweep through the queued actions looking for cancled ones (and when not).
		/// </summary>
		public void NoteCancelHasBeenRequestedOnRelatedAction()
		{
			mCancelRequestCount.Increment();
		}
	}

	//-------------------------------------------------
//-------------------------------------------------
}
