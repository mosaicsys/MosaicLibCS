//-------------------------------------------------------------------
/*! @file Queue.cs
 *  @brief This file contains the definitions and classes that are used to define the internal Action Queue for the Modular Action portions of this library.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2006 Mosaic Systems Inc.  (C++ library version)
 * All rights reserved.
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
        /// <summary>
        /// This method augments the rest of the IProviderFacet side interface and allows the Queue's Enqueue operation to verify that the 
        /// Action is in the correct state prior to accepting it into the queue.
        /// </summary>
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

		private const bool ActionQueueEnableDefault = false;
        private const int ActionQueueSizeDefault = 10;

        /// <summary>Gives the name of this ActionQueue</summary>
        public string Name { get; private set; }

        private volatile bool queueEnabled = false;

        private readonly object queueMutex = new object();

        private BasicNotificationList notifyOnEnqueueList = new BasicNotificationList();

        private AtomicInt32 queueCount = new AtomicInt32(0);
        private IProviderFacet[] queueArray = null;
        private int queueArraySize = 0;
        private int queueArrayNextPutIdx = 0;
        private int queueArrayNextGetIdx = 0;

        private AtomicInt32 cancelRequestCount = new AtomicInt32(0);
        private int lastServicedCancelRequestCount = 0;

		#endregion

        /// <summary>
        /// Constructor.  Requires a name, enabled flag and queueSize value.
        /// </summary>
        /// <param name="name">Gives the name of this queue (typically derived from the Part's Name to which the Queue belongs)</param>
        /// <param name="enabled">Used to initialize the mQueueEnabled field.  Indicates if the Queue shall be enabled immediately.</param>
        /// <param name="queueSize">Defines the maximum number of actions that can be contained at any one time.</param>
		public ActionQueue(string name, bool enabled, int queueSize) 
		{
			Name = name;
            queueArray = new IProviderFacet[queueSize];
			queueArraySize = queueArray.Length;
            queueEnabled = enabled;
		}

		/// <summary>NotificationList that will be Notified when an action is enqueued.  Typically set to signal the part's thread wakeup notifier.</summary>
		public IBasicNotificationList NotifyOnEnqueue { get { return notifyOnEnqueueList; } }

		/// <summary>Enqueue's the given action in the queue provided that it is valid and the queue is enabled.</summary>
		/// <param name="action">Gives the action to enqueue.</param>
		/// <returns>Empty string on success, error message on failure.</returns>
		/// <remarks>
		/// The given action must be non-null and in the Started state in order for this method to succeed.  
		/// In addition if the queue is not enabled or it is full at the time the enqueue is requested, the given action will be completed with a non-empty 
		/// result code and the Enqueue operation will complete successfully.
		/// </remarks>
    	public string Enqueue(IEnqueableProviderFacet action)
		{
			if (action == null)
                return "{0}.Enqueue.Failed.ActionIsNull".CheckedFormat(Name);

			if (!action.IsStarted)
                return "{0}.Enqueue.Failed.ActionHasNotBeenStarted".CheckedFormat(Name);

			lock (queueMutex)
			{
				if (!queueEnabled)
				{
                    action.CompleteRequest("{0}.Enqueue.Failed.QueueIsNotEnabled".CheckedFormat(Name));
					return "";
				}

				ServiceCancelRequests();

				if (queueCount.VolatileValue >= queueArraySize)
				{
                    action.CompleteRequest("{0}.Enqueue.Failed.QueueIsFull".CheckedFormat(Name));
					return "";
				}

				queueCount.Increment();

				queueArray[queueArrayNextPutIdx++] = action;
				if (queueArrayNextPutIdx >= queueArraySize)
					queueArrayNextPutIdx = 0;

				notifyOnEnqueueList.Notify();

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

			if (cancelRequestCount.VolatileValue == lastServicedCancelRequestCount)
				return;

            lock (queueMutex)
            {
                lastServicedCancelRequestCount = cancelRequestCount.Value;		// this is an atomic read of the volatile value

			    // Now go through the queued operations and complete any that are flaged
			    //	has CancelRequestActive.  For each of these we complete them and then
			    //	reset the correspondinging entry in the queue.  NOTE that the queue count
			    //	and getIdx are not changed as a side effect of this.  This means that the
			    //	GetNextAction method must be able to correctly skip queue entries that
			    //	have been reset since they were enqueued.

			    int idx = queueArrayNextGetIdx, numChecked = 0;

			    for (; numChecked < queueCount.VolatileValue; numChecked++)
			    {
				    IProviderFacet queueItem = queueArray[idx];

					if (queueItem != null && queueItem.IsCancelRequestActive)
				    {
                        queueItem.CompleteRequest("{0}.ServiceCancelRequests.ActionCanceledWhileEnqueued".CheckedFormat(Name));
						queueArray[idx] = null;
				    }

				    if (++idx >= queueArraySize)
					    idx = 0;
			    }
            }
        }

        /// <summary>
        /// get only:  returns the maximum number of actions that the queue can contain at any one time.  
        /// Attempts to add actions beyond this number will result in those actions being immediately completed with an error message that indicates that this queue is full.
        /// </summary>
        public int Capacity { get { return queueArraySize; } }

        /// <summary>
        /// Returns a recent copy of the count of the number of objects in the queue.  
        /// This count will include actions that have been canceled after they were started and before they were issued (and will thus never be issued).
        /// </summary>
        public Int32 VolatileCount { get { return queueCount.VolatileValue; } }

        /// <summary>
        /// Returns true if the a recent (volatile) copy of the count of the number of objects in the queue is zero.
        /// The queue will not be empty if it contains any actions that were started and then immediately canceled.  It will remain non-empty until the GetNextAction method
        /// is used which will consume previously canceled actions from the queue up to the next non-canceled action.
        /// </summary>
        public bool IsEmpty { get { return (VolatileCount == 0); } }

		/// <summary>
        /// Attempts to extract and return the next action in the queue, or returns null if the queue did not contain an action.
        /// This method also processes and discards actions that have been both started and canceled already and removes them from the queue up to the point where this method
        /// finds the first action in the queue that has not been canceled.
        /// </summary>
        public IProviderFacet GetNextAction() { return GetNextAction(false); }

        /// <summary>
        /// Attempts to (optinally) extract and return the next action in the queue, or returns null if the queue did not contain an action.
        /// This method also processes and discards actions that have been both started and canceled already and removes them from the queue up to the point where this method
        /// finds the first action in the queue that has not been canceled.
        /// <para/>If peekOnly is provided as true and the method finds an action in the queue to return then it will not remove the action from the queue before returning it.
        /// </summary>
        /// <param name="peekOnly">If this parameter is true then the returned action is not removed from the queue.</param>
        public IProviderFacet GetNextAction(bool peekOnly)
		{
			// before actually trying to obtain an object from the queue
			//	Use an asynchronous check to see if the queue is known to be empty
			//	in which case we just exit.

			int queueCount = this.queueCount.VolatileValue;

			if (queueCount == 0)
				return null;

			// next, given that the queue was not empty when we just checked it, check for new abort requests on items that might be in this queue.
			ServiceCancelRequests();

			// attempt to extract the first non-null op from the queue and return it.

			lock (queueMutex)
			{
				// loop until the queue is empty or we find a non-null item in the list
				for (; ; )
				{
					// retest that the queue is not empty 
                    queueCount = this.queueCount.VolatileValue;
					if (queueCount == 0)
						return null;

					IProviderFacet action = queueArray [queueArrayNextGetIdx];

                    if (peekOnly && action != null)
                        return action;

                    queueArray [queueArrayNextGetIdx] = null;

					if (++queueArrayNextGetIdx >= queueArraySize)
						queueArrayNextGetIdx = 0;

                    this.queueCount.Decrement();

					// else loop again until we get a non-null one or we find that the queue was actually really empty
					if (action != null)
						return action;
				}
			}
		}

		/// <summary>
		/// This property may be tested to determine if the queue is currently enabled and it may be set to enable or disable the queue.
		/// When disabling the queue, all queued actions will be completed with a non-empty result code which indicates that the action has been canceled
        /// because this queue was disabled.
		/// </summary>
		public bool QueueEnable
		{
			get { lock (queueMutex) { return queueEnabled; } }
			set 
			{
				bool entryValue = false;
				lock (queueMutex) { entryValue = queueEnabled; queueEnabled = value; }

				if (!value && entryValue)
				{
					// The Queue has just been disabled
					// Iterate using GetNextAction and complete each operation that it returns until the queue is empty

					IProviderFacet action = null;

					while ((action = GetNextAction()) != null)
						action.CompleteRequest("{0}.DisableQueue.ActionHasBeenCanceled".CheckedFormat(Name));
				}
			}
		}

		/// <summary>
		/// This asynchronous method is invoked by an action on its owning queue when the action gets canceled while in the Started state.
		/// This allows the queue to quickly determine when it needs to sweep through the queued actions looking for cancled ones (and when not).
		/// </summary>
		public void NoteCancelHasBeenRequestedOnRelatedAction()
		{
			cancelRequestCount.Increment();
		}
	}

	//-------------------------------------------------
//-------------------------------------------------
}
