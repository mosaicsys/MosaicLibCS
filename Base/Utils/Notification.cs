//-------------------------------------------------------------------
/*! @file Notification.cs
 *  @brief This file contains a series of utility classes that are used to handle notification, especially in multi-threaded apps.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2002 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

namespace MosaicLib.Utils
{
	using System;
	using System.Collections.Generic;

	//-------------------------------------------------
	#region Notification targets: delegates

	/// <summary> Define the signature of the delegate that is used with BasicNotification </summary>

	public delegate void BasicNotificationDelegate();

	#endregion

	//-------------------------------------------------
	#region Notification targets: notification and wait objects

	/// <summary> This interface defines the Notify method which a client may invoke on a INotifyable object to Notify it that something has happened. </summary>

	public interface INotifyable
	{
		void Notify();
	}

	/// <summary> Provides a version of the INotifyable interface in which the Notify call is passed an eventArgs parameter. </summary>
	/// <typeparam name="EventArgsType">The type of the eventArgs parameter that will be passed to the Notify call.</typeparam>
	public interface INotifyable<EventArgsType>
	{
		void Notify(EventArgsType eventArgs);
	}

	/// <summary> This interface defines a simple interface that is supported by objects which can be used to cause a thread to wait until the object is signalted (Notified). </summary>
	public interface IWaitable
	{
        /// <summary>returns true if the underlying object is currently set (so that first wait call is expected to return immiedately)</summary>
		bool IsSet { get; }
        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        bool Wait();
        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        bool WaitMSec(int timeLimitInMSec);
        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        bool WaitSec(double timeLimitInSeconds);
        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        bool Wait(TimeSpan timeLimit);
	}

	/// <summary>Objects that implement this interface provide the combined functinality of an INotifyable object and an IWaitable object</summary>
	public interface IEventNotifier : INotifyable, IWaitable { }

	/// <summary>
	/// This is the base class for all EventNotifier type objects.  
	/// It defines some abstract methods that must be implemented in a derived class and provides common implementations for some of the IEventNotifier methods.
	/// </summary>

	public abstract class EventNotifierBase : DisposableBase, IEventNotifier
	{
		#region INotifyable Members

		public abstract void Notify();

		#endregion

		#region IWaitable Members

		public abstract bool IsSet { get; }	// returns true if the underlying object is currently set (so that first wait call is expected to return immiedately)
		public abstract void Reset();

		public abstract bool Wait();

		public abstract bool WaitMSec(int timeLimitInMSec);

		public virtual bool WaitSec(double timeLimitInSeconds)
		{
			double dblTimeLimitInMSec = timeLimitInSeconds * 1000.0;
			int timeLimitInMSec = (int) Math.Max(Math.Min(dblTimeLimitInMSec, (double) 0x7fffffff), 0.0);
			return WaitMSec(timeLimitInMSec);
		}

		public virtual bool Wait(TimeSpan timeLimit)
		{
			return WaitSec(timeLimit.TotalSeconds);
		}

		#endregion
	}

	/// <summary> 
	/// This class is both INotifyable and IWaitable.  It provides an implementation around an EventWaitHandle where the constructor can detemine the
	/// signaling behavior of the object to be WakeOne, WakeAll or WakeAllSticky.  See description of Behavior enum items for more details  
	/// </summary>

	public class WaitEventNotifier : EventNotifierBase
	{
		#region CTor and related definitions

		/// <summary> Enum defines the types of behavior that this object may have </summary>
		public enum Behavior
		{
			Invalid = 0,		// Initial value placeholder.  Illegal in normal use.  Will throw exception if attempt is made to construct a WaitEventNotifier using this value.
			WakeOne,			// Notify wakes one thread if any are waiting (AutoReset) or none if no threads are currently waiting
			WakeAll,			// Notify wakes all threads that are currently waiting (Pulse with ManualReset), or none if no threads are currently waiting
			WakeAllSticky,		// Notify wakes all threads if any are waiting.  If no threads are waiting then it wakes the next thread or threads that attempt to invoke Wait on the previsouly signaled object.  Last caller to return from Wait in this manner will reset the object to its non-signaling state.  (based on ManualReset)
		}

		public WaitEventNotifier(Behavior behavior) 
		{
			this.behavior = behavior;

			switch (behavior)
			{
				case Behavior.WakeOne:
					eventH = new System.Threading.AutoResetEvent(false);
					break;
				case Behavior.WakeAll:
				case Behavior.WakeAllSticky:
					eventH = new System.Threading.ManualResetEvent(false);
					break;
				default:
					Utils.Assert.Fault("Unexpected WaitEventNotifier.Behavior value", AssertType.ThrowException);
					break;
			}
		}

		#endregion

		#region EventNotifierBase Members

		public override void Notify()
		{
			if (behavior == Behavior.WakeAll)
				PulseEvent();
			else
				SetEvent();
		}

		public override bool IsSet
		{
			get
			{
				bool signaled = false;
				System.Threading.EventWaitHandle ewh = eventH;

				if (behavior == Behavior.WakeAllSticky && ewh != null)
					signaled = ewh.WaitOne(0, false);

				return signaled;
			}
		}

		public override void Reset()
		{
			ResetEvent();
		}

		public override bool Wait()
		{
			System.Threading.EventWaitHandle ewh = eventH;

			if (ewh == null)
				return false;

			EnterWait();

			bool signaled = false;

			try
			{
				signaled = ewh.WaitOne();
			}
			catch (Exception e)
			{
				Assert.BreakpointFault("eventH.WaitOne failed", e);
			}
			finally
			{
				LeaveWait(true);
			}

			return signaled;
		}

		public override bool WaitMSec(int timeLimitInMSec)
		{
			if (eventH == null)
				return false;

			EnterWait();

			bool signaled = false;

			try
			{
				signaled = eventH.WaitOne(timeLimitInMSec, false);
			}
			catch (Exception e)
			{
				Assert.BreakpointFault("eventH.WaitOne(msec) failed", e);
			}
			finally
			{
				LeaveWait(signaled);
			}

			return signaled;
		}

		#endregion

		#region EventNotifierBase.DisposableBase Members

		protected override void Dispose(DisposableBase.DisposeType disposeType)
		{
			System.Threading.EventWaitHandle ewh = eventH;

			if (ewh != null)
			{
				eventH = null;
				ewh.Close();
			}
		}

		#endregion

		#region Private methods and instance variables

		private void SetEvent()
		{
			System.Threading.EventWaitHandle ewh = eventH;

			try
			{
				if (ewh != null)
					ewh.Set();
			}
			catch (Exception e)
			{
				Assert.BreakpointFault("eventH.Set failed", e);
			}
		}

		private void ResetEvent()
		{
			System.Threading.EventWaitHandle ewh = eventH;

			try
			{
				if (ewh != null)
					ewh.Reset();
			}
			catch (Exception e)
			{
				Assert.BreakpointFault("eventH.WaitOne failed", e);
			}
		}

		private void PulseEvent()
		{
			SetEvent();
			ResetEvent();
		}

		private void EnterWait()
		{
			numWaiters.Increment();
		}

		private void LeaveWait(bool wasSignaled)
		{
			int finalWaiters = numWaiters.Decrement();

			if (finalWaiters == 0 && wasSignaled && eventH != null)
			{
				if (behavior == Behavior.WakeAllSticky)
					ResetEvent();
			}
		}

		private System.Threading.EventWaitHandle eventH = null;
		private Behavior behavior = Behavior.Invalid;
		private AtomicInt32 numWaiters = new AtomicInt32(0);

		#endregion
	}

	/// <summary>
	/// This class is an instance of an EventNotifierBase that does nothing when signaled and which sleeps when called to Wait.
	/// </summary>
	public class NullNotifier : EventNotifierBase
	{
		public NullNotifier() { }

		#region EventNotifierBase Members

		public override void Notify() {}

		public override bool IsSet { get { return false; } }
		public override void Reset() { }

		public override bool Wait() 		// Sleep for fixed period and then return false - this method is not intended to be used...
		{
			System.Threading.Thread.Sleep(100);
			return false; 
		}

		public override bool WaitMSec(int timeLimitInMSec)
		{
			System.Threading.Thread.Sleep(timeLimitInMSec);
			return false;
		}

		#endregion

		#region EventNotifierBase.DisposableBase Members

		protected override void Dispose(DisposableBase.DisposeType disposeType) { }	// empty method - nothing to dispose

		#endregion
	}

	#endregion

	//-------------------------------------------------
	#region Notification: list interfaces and implementation classes

	/// <summary> Define the interface that is provided to clients to allow them to add and remove their BasicNotificationDelegates </summary>
	public interface IBasicNotificationList
	{
		event BasicNotificationDelegate OnNotify;

		void AddItem(INotifyable notifyableTarget);
		void RemoveItem(INotifyable notifyableTarget);
	}

	/// <summary>Define the delegate type that is used with our generic IEventHandlerNotificationList and derived types</summary>
	public delegate void EventHandlerDelegate<EventArgsType>(object source, EventArgsType eventArgs);

	/// <summary> Define the interface that is provided to clients to allow them to add and remove their typed EventHandler delegates </summary>
	public interface IEventHandlerNotificationList<EventArgsType>
	{
		event EventHandlerDelegate<EventArgsType> OnNotify;
	}

	/// <summary>Provides a thread safe container for storing a set of delegates that can be invoked without locking.</summary>  
	/// <remarks>
	/// Based on the use of a locked list of the delegates and a volatile handle to an array of delegates that is (re)objtained from the
	/// list when needed
	/// </remarks>

	public class LockedDelegateListBase<DelegateType>
	{
		protected void Add(DelegateType d)
		{
			lock (mutex)
			{
				delegateList.Add(d);
				rebuildVolatileDelegateArray = true;
			}
		}
		protected void Remove(DelegateType d)
		{
			lock (mutex)
			{
				delegateList.Remove(d);
				rebuildVolatileDelegateArray = true;
			}
		}

		public bool IsEmpty { get { return (Array.Length != 0); } }

		protected DelegateType [] Array 
		{
			get
			{
				DelegateType [] array = volatileDelegateArray;

				if (rebuildVolatileDelegateArray)
				{
					lock (mutex)
					{
						rebuildVolatileDelegateArray = false;

						array = delegateList.ToArray();
						if (array == null)
							array = emptyDelegateArray;

						volatileDelegateArray = array;
					}
				}

				return array;
			}
		}

		private object mutex = new object();
		private List<DelegateType> delegateList = new List<DelegateType>();
		private static DelegateType [] emptyDelegateArray = new DelegateType [0];
		private volatile DelegateType [] volatileDelegateArray = emptyDelegateArray;
		private volatile bool rebuildVolatileDelegateArray = true;
	}

	/// <summary> This class implements an MT safe event list for BasicNotificationDelegates. </summary>
	/// <remarks> 
	/// Add/Remove methods for IBasicNotificationList interface event are fully thread-safe and renterant.  
	/// Notify method makes asynchronous atomic copy of the current list of delegates and then iteratively (and synchronously) invokes them within try/catch.  
	/// As such Notify method may be safely used while invoking Add/Remove.  However please note that because invocation of the registered delegates occurs 
	/// without owning the mutex, a delegate may be invoked after it has been removed.
	/// Notify method is not intended to be renterantly invoked but will not generate any exceptions if this occurs.
	/// </remarks>
	public class BasicNotificationList : LockedDelegateListBase<BasicNotificationDelegate>, IBasicNotificationList, INotifyable
	{
		#region IBasicNotificationList Members

		public event BasicNotificationDelegate OnNotify
		{
			add		{ Add(value); }
			remove	{ Remove(value); }
		}

		public void AddItem(INotifyable notifyableTarget)
		{
			OnNotify += notifyableTarget.Notify;
		}

		public void RemoveItem(INotifyable notifyableTarget)
		{
			OnNotify -= notifyableTarget.Notify;
		}

		#endregion

		#region INotifyable Members

		public virtual void Notify() 
		{
			// take atomic snapshot of the array of delegates to invoke
			BasicNotificationDelegate [] array = Array;

			int exceptions = 0;

			if (array != null && array.Length != 0)
			{
				foreach (BasicNotificationDelegate bnd in array)
				{
					try
					{
						if (bnd != null)
							bnd();
					}
					catch
					{
						exceptions++;
					}
				}
			}

			if (exceptions != 0)
				Utils.Assert.BreakpointFault(Utils.Fcns.CheckedFormat("{0} exceptions triggered while invoking registered event delegate(s)", exceptions));
		}

		#endregion
	}

	/// <summary> This generic class implements a MT safe event list for EventHandler delegates </summary>
	/// <remarks> 
	/// Add/Remove methods for IEventHandlerNotificationList interface event are fully thread-safe and renterant.  
	/// Notify method makes asynchronous atomic copy of the current list of delegates and then iteratively (and synchronously) invokes them within try/catch.  
	/// As such Notify method may be safely used while invoking Add/Remove.  However please note that because invocation of the registered delegates occurs 
	/// without owning the mutex, a delegate may be invoked after it has been removed.
	/// Notify method is not intended to be renterantly invoked but will not generate any exceptions if this occurs.
	/// </remarks>
	/// 

	public class EventHandlerNotificationList<EventArgsType> : LockedDelegateListBase<EventHandlerDelegate<EventArgsType>>, IEventHandlerNotificationList<EventArgsType>, INotifyable<EventArgsType>, IBasicNotificationList
	{
		#region Ctor

		/// <summary> Ctor defaults to using EventHandlerNotificationList instance itself as source of events </summary>
		public EventHandlerNotificationList() { source = this; }

		/// <summary> Ctor allows caller to explicitly specify the source object that will be passed to the delegates on Notify </summary>
		public EventHandlerNotificationList(object eventSourceObject) { source = eventSourceObject; }

		#endregion

		#region IEventHandlerNotificationList<EventArgsType> Members

		public event EventHandlerDelegate<EventArgsType> OnNotify
		{
			add { Add(value); }
			remove { Remove(value); }
		}

		#endregion

		#region IBasicNotificationList Members

		event BasicNotificationDelegate IBasicNotificationList.OnNotify
		{
			add { Add(delegate(object source, EventArgsType eventArgs) { value(); }); }
			remove { Remove(delegate(object source, EventArgsType eventArgs) { value(); }); }		// I am not certain that this works as expected...
		}

		public void AddItem(INotifyable notifyableTarget)
		{
			((IBasicNotificationList) this).OnNotify += notifyableTarget.Notify;
		}

		public void RemoveItem(INotifyable notifyableTarget)
		{
			((IBasicNotificationList) this).OnNotify -= notifyableTarget.Notify;
		}

		#endregion

		#region INotifyable<EventArgsType> Members

		public virtual void Notify(EventArgsType eventArgs) 
		{
			// take atomic snapshot of the array of delegates to invoke
			EventHandlerDelegate<EventArgsType> [] array = Array;

			int exceptions = 0;

			if (array != null && array.Length != 0)
			{
				foreach (EventHandlerDelegate<EventArgsType> ehd in array)
				{
					try
					{
						if (ehd != null)
							ehd(source, eventArgs);
					}
					catch
					{
						exceptions++;
					}
				}
			}

			if (exceptions != 0)
				Utils.Assert.BreakpointFault(Utils.Fcns.CheckedFormat("{0} exceptions triggered while invoking registered event delegate(s)", exceptions));
		}

		#endregion

		private readonly object source;
	}

	#endregion

	//-------------------------------------------------
	#region Guarded Notification object(s)

	/// <summary> 
	/// Defines the interface that is provided by NotificationObjects.  
	/// Supports access to (possibly guarded) Sequenced Object and registration of BasicNotificationDelegates with underlying NotificationList 
	/// </summary>
	public interface INotificationObject<ObjectType> : ISequencedObjectSource<ObjectType, int>
	{
		IBasicNotificationList NotificationList { get; }
	}

	/// <summary> Implementation of INotificationObject for use with reference objects (classes).  Uses volatile handle to provide safe/synchronized access to object. </summary>
	public class InterlockedNotificationRefObject<RefObjectType> : InterlockedSequencedRefObject<RefObjectType>, INotificationObject<RefObjectType> where RefObjectType : class
	{
		public InterlockedNotificationRefObject() {}
		public InterlockedNotificationRefObject(RefObjectType initialValue) : base(initialValue) { }

		public override RefObjectType Object { set { base.Object = value; notificationList.Notify(); } }

		public IBasicNotificationList NotificationList { get { return notificationList; } }

		private BasicNotificationList notificationList = new BasicNotificationList();
	}

	/// <summary> Implementation of IGuardedNotificationObject for use with value objects (structs).  Uses mutex to control and synchronize access to guarded value. </summary>
	public class GuardedNotificationValueObject<ValueObjectType> : GuardedSequencedValueObject<ValueObjectType>, INotificationObject<ValueObjectType> where ValueObjectType : struct
	{
		public GuardedNotificationValueObject() {}
		public GuardedNotificationValueObject(ValueObjectType initialValue) : base(initialValue) { }

		public override ValueObjectType Object { set { base.Object = value; notificationList.Notify(); } }

		public IBasicNotificationList NotificationList { get { return notificationList; } }

		private BasicNotificationList notificationList = new BasicNotificationList();
	}

	#endregion

	//-------------------------------------------------
	#region SharedWaitEventNotifierSet

	/// <summary>This class is used to provide a pool of reusable (concurrently if needed) IEventNotifier objects.</summary>
	/// <remarks>
	/// Such a pool is used when dynamically constructed
	/// objects need to make brief use of a IEventNotifier object so as to accomplish efficient sleeping in the context of a given call.  In general the
	/// WakeAllSticky behavior should be used and the clients should be aware that the underlying IEventNotifier object may be in use by more than one client
	/// at a time or for more than one purpose at a time.  However the ability to use such an object on an occasional basis is still a significant improvement
	/// on providing wait responsiveness (no use of short fixed sleeps) without adding allocation and handle churn related to dynamic construction of 
	/// IEventNotifier objects.
    /// 
    /// Generally this object is used as a type specific singleton.
	/// </remarks>

	public class SharedWaitEventNotifierSet : DisposableBase
	{
		public SharedWaitEventNotifierSet() : this(37) { }
		public SharedWaitEventNotifierSet(int setSize) : this(setSize, WaitEventNotifier.Behavior.WakeAllSticky) { } 

		public SharedWaitEventNotifierSet(int setSize, WaitEventNotifier.Behavior notifierBehavior) 
		{
			while (eventSetList.Count < setSize)
			{
				eventSetList.Add(new WaitEventNotifier(notifierBehavior));
			}
		}

		public IEventNotifier GetNextEventNotifier()
		{
			int seqNum = seqNumGen.Increment();
			int idx = seqNum % eventSetList.Count;
			IEventNotifier ien = eventSetList [idx];

			return ien;
		}

		Utils.AtomicInt32 seqNumGen = new AtomicInt32();

		List<IEventNotifier> eventSetList = new List<IEventNotifier>();

        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (disposeType == DisposeType.CalledExplicitly)
            {
                for (int idx = 0; idx < eventSetList.Count; idx++)
                {
                    IEventNotifier ien = eventSetList[idx];
                    eventSetList[idx] = null;
                    Utils.Fcns.DisposeOfObject(ref ien);
                }
            }
        }
    }

	#endregion

	//-------------------------------------------------
}

//-------------------------------------------------
