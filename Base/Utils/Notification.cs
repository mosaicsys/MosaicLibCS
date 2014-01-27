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
	#region Notification targets: notification and wait object interfaces

	/// <summary> This interface defines the Notify method which a client may invoke on a INotifyable object to Notify it that something has happened. </summary>
	public interface INotifyable
	{
        /// <summary>Caller invokes this to Notify a target object that something notable has happened.</summary>
        void Notify();
	}

	/// <summary> Provides a version of the INotifyable interface in which the Notify call is passed an eventArgs parameter. </summary>
	/// <typeparam name="EventArgsType">The type of the eventArgs parameter that will be passed to the Notify call.</typeparam>
	public interface INotifyable<EventArgsType>
	{
        /// <summary>
        /// Caller invokes this to Notify a target object that something notable has happened and passes the requested EventArgType argument to it.
        /// </summary>
        void Notify(EventArgsType eventArgs);
	}

	/// <summary> This interface defines a simple interface that is supported by objects which can be used to cause a thread to wait until the object is signaled (Notified). </summary>
	public interface IWaitable
	{
        /// <summary>returns true if the underlying object is currently set (so that first wait call is expected to return immediately)</summary>
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

	/// <summary>
    /// Objects that implement this interface provide the combined functionality of an <see cref="INotifyable"/> object and an <see cref="IWaitable"/> object
    /// </summary>
	public interface IEventNotifier : INotifyable, IWaitable { }

    #endregion 	
    
    #region Notification targets: notification and wait implementation objects

    /// <summary>
	/// This is the base class for all EventNotifier type objects.  
	/// It defines some abstract methods that must be implemented in a derived class and provides common implementations for some of the IEventNotifier methods.
	/// </summary>
    /// <remarks>
    /// This base class provides a small number of helper/mapping functions for different variants on the WaitOne pattern.
    /// </remarks>
	public abstract class EventNotifierBase : DisposableBase, IEventNotifier
	{
		#region INotifyable Members

        /// <summary>Caller invokes this to Notify a target object that something notable has happened.</summary>
        public abstract void Notify();

		#endregion

		#region IWaitable Members

        /// <summary>returns true if the underlying object is currently set (so that first wait call is expected to return immediately)</summary>
        public abstract bool IsSet { get; }

        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        public abstract bool Wait();

        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        public abstract bool WaitMSec(int timeLimitInMSec);

        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        public virtual bool WaitSec(double timeLimitInSeconds)
		{
			double dblTimeLimitInMSec = timeLimitInSeconds * 1000.0;
			int timeLimitInMSec = (int) Math.Max(Math.Min(dblTimeLimitInMSec, (double) 0x7fffffff), 0.0);
			return WaitMSec(timeLimitInMSec);
		}

        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        public virtual bool Wait(TimeSpan timeLimit)
		{
			return WaitSec(timeLimit.TotalSeconds);
		}

		#endregion

        #region Additional required methods

        /// <summary>Clears the signaling state of the target object.  Implemented as appropriate based on the actual implementation object type.</summary>
        public abstract void Reset();

        #endregion
    }

	/// <summary> 
	/// This class is both INotifyable and IWaitable.  It provides an implementation around an EventWaitHandle where the constructor can determine the
	/// signaling behavior of the object to be WakeOne, WakeAll or WakeAllSticky.  See description of Behavior enum items for more details  
	/// </summary>
	public class WaitEventNotifier : EventNotifierBase
	{
		#region CTor and related definitions

		/// <summary> Enum defines the types of behavior that this object may have </summary>
		public enum Behavior
		{
            /// <summary>Notify wakes one thread if any are waiting (AutoReset) or none if no threads are currently waiting</summary>
            WakeOne,
            /// <summary>Notify wakes all threads that are currently waiting (Pulse with ManualReset), or none if no threads are currently waiting</summary>
            WakeAll,
            /// <summary>Notify wakes all threads if any are waiting.  If no threads are waiting then it wakes the next thread or threads that attempt to invoke Wait on the previously signaled object.  Last caller to return from Wait in this manner will reset the object to its non-signaling state.  (based on ManualReset)</summary>
            WakeAllSticky,
		}

        /// <summary>
        /// Constructor: caller specifies the required Behavior as one of Behavior.WakeOne, Behavior.WakeAll, or Behavior.WakeAllSticky.
        /// </summary>
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
					Asserts.NoteFaultOccurance("Unexpected WaitEventNotifier.Behavior value", AssertType.ThrowException);
					break;
			}
		}

		#endregion

        #region EventNotifierBase.DisposableBase Members

        /// <summary>
        /// Internal implementation method for DisposeableBase.Dispose(DisposeType) abstract method.
        /// Releases any held unmanaged/disposable objects when called explicitly.
        /// </summary>
        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (disposeType == DisposeType.CalledExplicitly)
                Utils.Fcns.DisposeOfObject(ref eventH);
        }

        #endregion

        #region EventNotifierBase Members

        /// <summary>Caller invokes this to Notify a target object that something notable has happened.</summary>
        public override void Notify()
		{
			if (behavior == Behavior.WakeAll)
                EventWaitHandleHelper.PulseEvent(eventH);
			else
                EventWaitHandleHelper.SetEvent(eventH);
		}

        /// <summary>
        /// When the configured behavior is WakeAll or WakeAllSticky then this method returns true if the underlying EventWaitHandle is in a signaling state.
        /// In all other cases this returns false.
        /// </summary>
        /// <remarks>Each use of this property invokes a kernel call to check the state of the underlying event object.</remarks>
        public override bool IsSet
		{
			get
			{
                if (behavior == Behavior.WakeAllSticky || behavior == Behavior.WakeAll)
				    return EventWaitHandleHelper.IsEventSet(eventH);

                return false;
			}
		}

        /// <summary>Resets the underlying event.</summary>
        public override void Reset()
		{
            EventWaitHandleHelper.ResetEvent(eventH);
		}

        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        public override bool Wait()
		{
            bool signaled = false;
            try
            {
                EnterWait();

                signaled = EventWaitHandleHelper.WaitOne(eventH, false);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.WaitOne()");
            }
            finally
            {
                LeaveWait(signaled);
            }

			return signaled;
		}

        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        public override bool WaitMSec(int timeLimitInMSec)
		{
            bool signaled = false;
            try
            {
                EnterWait();

                // only attempt to actually wait if the timeLimitInMSec is positive or if it is -1 (infinite - wait)
                if ((timeLimitInMSec >= 0) || (timeLimitInMSec == -1))
                    signaled = EventWaitHandleHelper.WaitOne(eventH, unchecked((uint)timeLimitInMSec), false);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.WaitOne(msec)");
            }
            finally
            {
                LeaveWait(signaled);
            }

            return signaled;
		}

		#endregion

		#region Private methods and instance variables

		private void EnterWait()
		{
			numWaiters.Increment();
		}

		private void LeaveWait(bool wasSignaled)
		{
			int finalWaiters = numWaiters.Decrement();

			if (finalWaiters == 0 && wasSignaled && behavior == Behavior.WakeAllSticky)
			{
                EventWaitHandleHelper.ResetEvent(eventH);
			}
		}

		private System.Threading.EventWaitHandle eventH;
		private Behavior behavior;
		private AtomicInt32 numWaiters = new AtomicInt32(0);

		#endregion
	}

	/// <summary>
	/// This class is an instance of an EventNotifierBase that does nothing when signaled and which sleeps when called to Wait.
	/// </summary>
	public class NullNotifier : EventNotifierBase
	{
        /// <summary>
        /// Default constructor for a NullNotifier.
        /// </summary>
		public NullNotifier() { }

        #region EventNotifierBase.DisposableBase Members

        /// <summary>
        /// Internal implementation method for DisposeableBase.Dispose(DisposeType) abstract method.
        /// Empty method - there is nothing to dispose in this sub-type of IEventNotifier
        /// </summary>
        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            // empty method - nothing to dispose
        }

        #endregion
        
        #region EventNotifierBase Members

        /// <summary>
        /// Caller invokes this to Notify a target object that something notable has happened.
        /// Under this class the Notify method does nothing.
        /// </summary>
        public override void Notify() { }

        /// <summary>
        /// Under this class the IsSet property always returns false.
        /// </summary>
		public override bool IsSet { get { return false; } }

        /// <summary>
        /// Under this class the Reset method does nothing.
        /// </summary>
		public override void Reset() { }

        /// <summary>
        /// Under this class the Wait method always blocks the caller for a fixed period of time (100 mSec) and then returns false.
        /// Normally no caller should be calling the NullNotifier's Wait method.
        /// </summary>
		public override bool Wait() 		// Sleep for fixed period and then return false - this method is not intended to be used...
		{
			System.Threading.Thread.Sleep(100);
			return false; 
		}

        /// <summary>
        /// Under this class the WaitMSec method always blocks the caller for the requested timeLimitInMSec period of time and then returns false.
        /// Normally no caller should be calling the NullNotifier's Wait method.
        /// </summary>
        public override bool WaitMSec(int timeLimitInMSec)
		{
			System.Threading.Thread.Sleep(timeLimitInMSec);
			return false;
		}

		#endregion
	}

    /// <summary>
    /// INotifyable target which contains a counter that does IncrementSkipZero each time the Notify method is invoked.
    /// </summary>
    public class AtomicInt32CounterNotifier : INotifyable
    {
        /// <summary>Default constructor.</summary>
        public AtomicInt32CounterNotifier() {}

        /// <summary><see cref="INotifyable"/> Notify method.  Invokes counter.IncrementSkipZero.</summary>
        public void Notify() { counter.IncrementSkipZero(); }

        /// <summary>Get/Set property gives acceess to countained counter's Value property</summary>
        public int Value { get { return counter.Value; } set { counter.Value = value; } }

        /// <summary>Get/Set property gives acceess to countained counter's VolatileValue property</summary>
        public int VolatileValue { get { return counter.VolatileValue; } set { counter.VolatileValue = value; } }

        AtomicInt32 counter = new AtomicInt32();
    }

    /// <summary>
    /// static class containing a number of <see cref="System.Threading.EventWaitHandle"/> related static methods that may be used to safely manipulate an EventWaitHandle object.
    /// </summary>
    internal static class EventWaitHandleHelper
    {
        /// <summary>
        /// Tests if the given EventWaitHandle instance is signaling by obtaining a SafeHandle from it and then invoking WaitForSingleObjectEx with a zero timeout and looking at the return code
        /// to see if it is WAIT_OBJECT_0 or some other code.  This method has the side effect of clearing the signaling state for any underlying event object that is configure to AutoReset.
        /// </summary>
        public static bool IsEventSet(System.Threading.EventWaitHandle eventWaitHandle)
        {
            try
            {
                Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    return (WaitForSingleObjectEx(safeWaitHandle, 0, false) == WAIT_OBJECT_0);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.IsEventSet");
            }

            return false;
        }

        /// <summary>
        /// Waits for the given EventWaitHandle instance to become signaling by obtaining a SafeHandle from it and then invoking WaitForSingleObjectEx with an INFINITE timeout.
        /// Returns true if the wait handle became signaling and returned WAIT_OBJECT_0, or false otherwise.  
        /// This method has the side effect of clearing the signaling state for any underlying event object that is configure to AutoReset.
        /// </summary>
        public static bool WaitOne(System.Threading.EventWaitHandle eventWaitHandle, bool allowAlertToExitWait)
        {
            try
            {
                Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    return (WaitForSingleObjectEx(safeWaitHandle, INFINITE, allowAlertToExitWait) == WAIT_OBJECT_0);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.WaitOne (a)");
            }

            return false;
        }

        /// <summary>
        /// Waits for the given EventWaitHandle instance to become signaling by obtaining a SafeHandle from it and then invoking WaitForSingleObjectEx with the given milliseconds time limit.
        /// Returns true if the wait handle became signaling and returned WAIT_OBJECT_0, or false otherwise.  
        /// This method has the side effect of clearing the signaling state for any underlying event object that is configure to AutoReset.
        /// </summary>
        public static bool WaitOne(System.Threading.EventWaitHandle eventWaitHandle, uint milliseconds, bool allowAlertToExitWait)
        {
            try
            {
                Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    return (WaitForSingleObjectEx(safeWaitHandle, milliseconds, allowAlertToExitWait) == WAIT_OBJECT_0);
            }
            catch (System.Exception ex)
            {
                HandleEventException(ex, "EventWaitHandleHelper.WaitOne (b)");
            }

            return false;
        }

        public static void HandleEventException(Exception ex, string locationStr)
        {
            string faultStr = Fcns.CheckedFormat("{0} failed: {1}", locationStr, ex);

            if (ex is System.Threading.ThreadAbortException)
            {
                Asserts.LogFaultOccurance(faultStr);
                System.Threading.Thread.Sleep(10);     // prevent uncontrolled spinning
            }
            else
            {
                Asserts.TakeBreakpointAfterFault(faultStr);
            }
        }

        /// <summary>
        /// Calls kernel.dll::SetEvent on the SafeWaitHandle contained in the given EventWaitHandle object.
        /// </summary>
        public static void SetEvent(System.Threading.EventWaitHandle eventWaitHandle)
        {
            try
            {
                Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    SetEvent(safeWaitHandle);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.SetEvent");
            }
        }

        /// <summary>
        /// Calls kernel.dll::ResetEvent on the SafeWaitHandle contained in the given EventWaitHandle object.
        /// </summary>
        public static void ResetEvent(System.Threading.EventWaitHandle eventWaitHandle)
        {
            try
            {
                Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    ResetEvent(safeWaitHandle);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.ResetEvent");
            }
        }

        /// <summary>
        /// Calls kernel.dll::PulseEvent on the SafeWaitHandle contained in the given EventWaitHandle object.
        /// </summary>
        public static void PulseEvent(System.Threading.EventWaitHandle eventWaitHandle)
        {
            try
            {
                Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    PulseEvent(safeWaitHandle);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.PulseEvent");
            }
        }

        /// <summary>
        /// Attempts to obtain a valid safeWaitHandle from the given eventWaitHandle and return true if the produced safeWaitHandle value is non-null, is not Closed and it is not Invalid.
        /// </summary>
        /// <returns>true if the safeWaitHandle value appears to be usable (it is not null, not Closed and not IsInvalid), or false otherwise.</returns>
        private static bool GetValidSafeWaitHandle(System.Threading.EventWaitHandle eventWaitHandle, out Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle)
        {
            // capture a copy of the SafeWaitHandle from it
            safeWaitHandle = (eventWaitHandle != null ? eventWaitHandle.SafeWaitHandle : null);

            // if the handle is not closed and it is not invalid then attempt to call Win32.SetEvent on it
            return (safeWaitHandle != null && !safeWaitHandle.IsClosed && !safeWaitHandle.IsInvalid);
        }

        #region SetEvent Win32 system calls and related definitions

        private const UInt32 INFINITE = 0xFFFFFFFF;
        private const UInt32 WAIT_ABANDONED = 0x00000080;
        private const UInt32 WAIT_OBJECT_0 = 0x00000000;
        private const UInt32 WAIT_TIMEOUT = 0x00000102;
        private const UInt32 WAIT_IO_COMPLETION = 0x000000C0;
        private const UInt32 WAIT_FAILED = 0xFFFFFFFF;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObjectEx(Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle, uint dwMilliseconds, bool bAlertable);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetEvent(Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ResetEvent(Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PulseEvent(Microsoft.Win32.SafeHandles.SafeWaitHandle safeWaitHandle);

        #endregion
    }

	#endregion

    //-------------------------------------------------
    #region Notification related collections and Collections namespace

    namespace Collections
    {
        /// <summary>
        /// Provides a thread safe container for storing a set of objects with a backing cached array for thread safe efficient atomic snapshot of the set contents.
        /// This object is intended to be used in the following cases:
        /// <list type="number">
        /// <item>Where logic frequently iterates through the items in a list but where the contents are rarely changed.  Use of conversion of list contents to a cached Array decreases access/iteration cost and minimizes garbage generation.</item>
        /// <item>Where list content changes may be made on multiple threads with list iteration performed on a single thread.  Use of conversion of list contents to an array allows iterating thread to take a snapshot of the list contents before each iteration method and then iterate without needing to lock or otherwise concern itself with changeable list contents during a single iteration phase.</item>
        /// </list>
        /// Examples of these cases include delegate and event lists as well as any generic list of items that are iterated through much more frequently than the set of such items changes.
        /// </summary>  
        /// <typeparam name="ObjectType">ObjectType may be any reference or value object.  Use is expected to be based on reference object types but does not require it.</typeparam>
        /// <remarks>
        /// Based on the use of a locked list of the objects and a volatile handle to an array of objects that is (re)obtained from the list when needed
        /// </remarks>

        public class LockedObjectListWithCachedArray<ObjectType>
        {
            /// <summary>Default contstructor</summary>
            public LockedObjectListWithCachedArray() { }

            /// <summary>Collection based constructor.  Sets up the list to contain the given collection of objects.</summary>
            public LockedObjectListWithCachedArray(IEnumerable<ObjectType> collection)
            {
                AddRange(collection);
            }

            #region Public methods and properties

            /// <summary>
            /// Adds the given object instance to the list and triggers the Array be rebuilt on next use.  
            /// Re-enterant and thread safe using leaf lock on list contents.
            /// </summary>
            /// <returns>this object for call chaining</returns>
            public LockedObjectListWithCachedArray<ObjectType> Add(ObjectType d)
            {
                lock (listMutex)
                {
                    rebuildVolatileObjectArray = true;
                    objectList.Add(d);
                }

                return this;
            }
            /// <summary>
            /// Removes the given object instance from the list and triggers the Array be rebuilt on next use.  
            /// Re-enterant and thread safe using leaf lock on list contents.
            /// </summary>
            /// <returns>this object for call chaining</returns>
            public LockedObjectListWithCachedArray<ObjectType> Remove(ObjectType d)
            {
                lock (listMutex)
                {
                    rebuildVolatileObjectArray = true;
                    objectList.Remove(d);
                }

                return this;
            }

            /// <summary>
            /// Adds the given collection of objects to the end of the list and triggers the Array to be rebuilt on its next use.
            /// Re-enterant and thread safe using leaf lock on list contents.
            /// </summary>
            /// <param name="collection">Gives the IEnumerable collection of items to append to the end of this list.</param>
            /// <returns>this object for call chaining</returns>
            public LockedObjectListWithCachedArray<ObjectType> AddRange(IEnumerable<ObjectType> collection)
            {
                lock (listMutex)
                {
                    rebuildVolatileObjectArray = true;
                    objectList.AddRange(collection);
                }

                return this;
            }

            /// <summary>
            /// Gets or sets the element at the specified index. 
            /// </summary>
            /// <param name="index">The zero-based index of the element to get or set.</param>
            /// <returns>The element at the specified index.</returns>
            /// <exception cref="System.ArgumentOutOfRangeException">index is less than 0.  -or- index is equal to or greater than Count.</exception>
            public ObjectType this[int index]
            {
                get
                {
                    // the following logic is designed to decrease the risk that the Array will be regenerated many times if a client is getting and setting array elements frequently.
                    if (!rebuildVolatileObjectArray)
                        return Array[index];

                    lock (listMutex)
                    {
                        return objectList[index];
                    }
                }
                set
                {
                    lock (listMutex)
                    {
                        objectList[index] = value;
                        rebuildVolatileObjectArray = true;
                    }
                }
            }

            /// <summary>
            ///  Gets the number of elements actually contained in this list using the Length of the Array property
            /// </summary>
            public int Count { get { return Array.Length; } }

            /// <summary>Returns true if the Array property is currently empty (returns a zero length array).</summary>
            public bool IsEmpty { get { return (Count == 0); } }

            /// <summary>
            /// Returns the most recently generated copy of the Array version of the underlying list of objects.  Will return a fixed empty array when the list is empty.
            /// Implementation guarantees that returned value will include effects of any change made to the list by the thread that is requesting this array.
            /// Changes made by other threads produce race conditions where the side effects of the change on another thread may, or may not, be visible in the array contents
            /// until the thread reading this property invokes it entirely after another thread in question's Add or Remove method has returned from that method invocation.
            /// This method does not attempt to lock or update the underlying Array value unless it knows that at least one change has been completed to the list contents.
            /// </summary>
            /// <remarks>
            /// If any change to the list has been recorded via the rebuild flag then this property will lock access to the list, 
            /// generate the array version of it and then retain the Array version for later requests until the list contents have been changed again.
            /// Use of locked access to list during rebuild prevents the risk that the list may change contents while the rebuild is taking place.
            /// </remarks>
            public ObjectType[] Array
            {
                get
                {
                    ObjectType[] array = volatileObjectArray;

                    if (rebuildVolatileObjectArray)
                    {
                        lock (listMutex)
                        {
                            rebuildVolatileObjectArray = false;

                            array = objectList.ToArray();
                            if (array == null)
                                array = emptyObjectArray;

                            volatileObjectArray = array;
                        }
                    }

                    return array;
                }
            }

            #endregion

            #region Private fields

            /// <summary>mutex used to guard/sequence access to the underlying list so that both changes and access to the list are performed atomically.</summary>
            private object listMutex = new object();
            /// <summary>underlying reference list of delegates, access to this list must only be made while owning the corresponding mutex.</summary>
            private List<ObjectType> objectList = new List<ObjectType>();
            /// <summary>Single common empty array that is used as the array when the list is empty.</summary>
            private static ObjectType[] emptyObjectArray = new ObjectType[0];
            /// <summary>volatile handle to the array of delegates produced during the last rebuild operation.</summary>
            private volatile ObjectType[] volatileObjectArray = emptyObjectArray;
            /// <summary>volatile boolean used to flag that a rebuild is required during the next access to the Array property.</summary>
            private volatile bool rebuildVolatileObjectArray = true;

            #endregion
        }
    }

    /// <summary>
    /// Please replace current use with the equivalent, relocated, MosaicLib.Utils.Collections.LockedObjectListWithCachedArray class
    /// </summary>
    [Obsolete("Please replace current use with the equivalent, relocated, MosaicLib.Utils.Collections.LockedObjectListWithCachedArray class.  (2013-04-02)")]
    public class LockedObjectListWithCachedArray<ObjectType> : Collections.LockedObjectListWithCachedArray<ObjectType>
    {
        /// <summary>
        /// Default Constructor: Please replace current use with the equivalent, relocated, MosaicLib.Utils.Collections.LockedObjectListWithCachedArray class
        /// </summary>
        public LockedObjectListWithCachedArray() { }
    }

    /// <summary>
    /// Provides a thread safe container for storing a set of delegates that can be invoked without locking.
    /// This class is a synonym for the LockedObjectListWithCachedArray templatized class.
    /// </summary>  
    /// <remarks>
    /// Based on the use of a locked list of the delegates and a volatile handle to an array of delegates that is (re)obtained from the
    /// list when needed
    /// </remarks>
    [Obsolete("Please replace current use with the new MosaicLib.Utils.Collections.LockedObjectListWithCachedArray type.  (2013-04-02)")]
    public class LockedDelegateListBase<DelegateType> : Collections.LockedObjectListWithCachedArray<DelegateType>
    {
        /// <summary>
        /// Adds the given delegate instance to the list and triggers the Array copy to be rebuilt.  
        /// Re-enterant and thread safe using leaf lock on list contents.
        /// </summary>
        protected new void Add(DelegateType d)
        {
            base.Add(d);
        }

        /// <summary>
        /// Removes the given delegate instance from the list and triggers the Array copy to be rebuilt.  
        /// Re-enterant and thread safe using leaf lock on list contents.
        /// </summary>
        protected new void Remove(DelegateType d)
        {
            base.Remove(d);
        }

        /// <summary>
        /// Returns the most recently generated copy of the Array version of the underlying list of delegates.  Will return a fixed empty array when the list is empty.
        /// Implementation guarantees that returned value will include effects of any change made to the list by the thread that is requesting this array.
        /// Changes made by other threads produce a race condition where the side effects of the change on another thread will not be visible in the array contents
        /// until the thread reading this property invokes it entirely after another thread in question's Add or Remove method has returned from that method invocation.
        /// This method does not attempt to lock or update the underlying Array value unless it knows that at least one change has been completed to the list contents.
        /// </summary>
        /// <remarks>
        /// If any change to the list has been recorded via the rebuild flag then this property will lock access to the list, 
        /// generate the array version of it and then retain the Array version for later requests until the list contents have been changed again.
        /// Use of locked access to list during rebuild prevents the risk that the list may change contents while the rebuild is taking place.
        /// </remarks>
        protected new DelegateType[] Array
        {
            get { return base.Array; }
        }
    }

    #endregion

    //-------------------------------------------------
	#region Notification: list interfaces and implementation classes

	/// <summary> 
    /// Define the interface that is provided to clients to allow them to add and remove their 
    /// <see cref="BasicNotificationDelegate"/> and <see cref="System.Threading.EventWaitHandle"/> object to a list that can be notified.
    /// </summary>
	public interface IBasicNotificationList
	{
        /// <summary>
        /// <see cref="BasicNotificationDelegate"/> event interface.  Allows such delegates to be added/removed from the list.
        /// </summary>
		event BasicNotificationDelegate OnNotify;

        /// <summary>Adds the given <see cref="INotifyable"/> target object to the list</summary>
		void AddItem(INotifyable notifyableTarget);
        /// <summary>Removes the first instance of the given <see cref="INotifyable"/> target object from the list</summary>
        void RemoveItem(INotifyable notifyableTarget);

        /// <summary>Adds the given <see cref="System.Threading.EventWaitHandle"/> object to the list</summary>
        void AddItem(System.Threading.EventWaitHandle eventWaitHandle);
        /// <summary>Removes the first instance of the given <see cref="System.Threading.EventWaitHandle"/> object from the list</summary>
        void RemoveItem(System.Threading.EventWaitHandle eventWaitHandle);
	}

	/// <summary>Define the delegate type that is used with our generic IEventHandlerNotificationList and derived types</summary>
	public delegate void EventHandlerDelegate<EventArgsType>(object source, EventArgsType eventArgs);

	/// <summary> Define the interface that is provided to clients to allow them to add and remove their typed EventHandler delegates </summary>
	public interface IEventHandlerNotificationList<EventArgsType>
	{
        /// <summary>
        /// <see cref="EventHandlerDelegate{EventArgsType}"/> event interface.  Allows such delegates to be added/removed from the list.
        /// </summary>
		event EventHandlerDelegate<EventArgsType> OnNotify;

        /// <summary>
        /// Defines the source object that is used for all notify calls that originate from this list.
        /// </summary>
        object Source { get; }
	}

	/// <summary> This class implements an MT safe event list for BasicNotificationDelegates, INotifyable objects and EventWaitHandle objects. </summary>
	/// <remarks> 
	/// Add/Remove methods for supported notification targets are fully thread-safe and reentrant.  
	/// Notify method makes asynchronous atomic copy of the current list of delegates and then iteratively (and synchronously) invokes them within try/catch.  
	/// As such Notify method may be safely used while invoking Add/Remove.  However please note that because invocation of the registered delegates occurs 
	/// without owning the mutex, a delegate may be invoked after it has been removed.
	/// Notify method is not intended to be reentrantly invoked but will not generate any exceptions if this occurs.
	/// </remarks>
	public class BasicNotificationList : IBasicNotificationList, INotifyable
	{
		#region IBasicNotificationList Members

        /// <summary>
        /// <see cref="BasicNotificationDelegate"/> event interface.  Allows such delegates to be added/removed from the list.
        /// </summary>
        public event BasicNotificationDelegate OnNotify
		{
            add { CreateEmptyObjectIfNeeded(ref basicNotificationDelegateList).Add(value); }
            remove { CreateEmptyObjectIfNeeded(ref basicNotificationDelegateList).Remove(value); }
		}

        /// <summary>Adds the given <see cref="INotifyable"/> target object to the list</summary>
        public void AddItem(INotifyable notifyableTarget)
		{
            CreateEmptyObjectIfNeeded(ref notifyableList).Add(notifyableTarget);
		}

        /// <summary>Removes the first instance of the given <see cref="INotifyable"/> target object from the list</summary>
        public void RemoveItem(INotifyable notifyableTarget)
		{
            CreateEmptyObjectIfNeeded(ref notifyableList).Remove(notifyableTarget);
		}

        /// <summary>Adds the given <see cref="System.Threading.EventWaitHandle"/> object to the list</summary>
        public void AddItem(System.Threading.EventWaitHandle eventWaitHandle)
        {
            CreateEmptyObjectIfNeeded(ref eventWaitHandleList).Add(eventWaitHandle);
        }

        /// <summary>Removes the first instance of the given <see cref="System.Threading.EventWaitHandle"/> object from the list</summary>
        public void RemoveItem(System.Threading.EventWaitHandle eventWaitHandle)
        {
            CreateEmptyObjectIfNeeded(ref eventWaitHandleList).Remove(eventWaitHandle);
        }

		#endregion

		#region INotifyable Members

        /// <summary>Caller invokes this to Notify a target object that something notable has happened.</summary>
        public virtual void Notify() 
		{
			int delegateExceptions = 0, notifyExceptions = 0, eventWaitHandleExeceptions = 0;

            if (basicNotificationDelegateList != null)
            {
                foreach (BasicNotificationDelegate bnd in basicNotificationDelegateList.Array)
                {
                    try { (bnd ?? nullBasicNotificationDelegate)(); }
                    catch { delegateExceptions++; }
                }
            }

            if (notifyableList != null)
            {
                foreach (INotifyable notificationItem in notifyableList.Array)
                {
                    try { (notificationItem ?? nullNotifier).Notify(); }
                    catch { notifyExceptions++; }
                }
            }

            if (eventWaitHandleList != null)
            {
                foreach (System.Threading.EventWaitHandle eventWaitHandle in eventWaitHandleList.Array)
                {
                    try { EventWaitHandleHelper.SetEvent(eventWaitHandle); }
                    catch { eventWaitHandleExeceptions++; }
                }
            }

            if (delegateExceptions != 0 || notifyExceptions != 0 || eventWaitHandleExeceptions != 0)
                Asserts.TakeBreakpointAfterFault(Utils.Fcns.CheckedFormat("Notify triggered exceptions: delegates:{0} inotifyable:{1} eventWaitHandle:{2}", delegateExceptions, notifyExceptions, eventWaitHandleExeceptions));
		}

		#endregion

        #region Private fields and methods

        private Collections.LockedObjectListWithCachedArray<BasicNotificationDelegate> basicNotificationDelegateList = null;
        private Collections.LockedObjectListWithCachedArray<INotifyable> notifyableList = null;
        private Collections.LockedObjectListWithCachedArray<System.Threading.EventWaitHandle> eventWaitHandleList = null;

        private static BasicNotificationDelegate nullBasicNotificationDelegate = (delegate() { });
        private static NullNotifier nullNotifier = new NullNotifier();

        /// <summary>
        /// Protected mutex object used during object creation as required within the CreateEmptyObjectIfNeeded static method.
        /// </summary>
        protected static object objectCreationMutex = new object();

        /// <summary>
        /// Static helper method used to combine handle query, object creation and handle update into a single pass through method.  
        /// This method supports a form of on-first-use singleton construction for notification target object lists.
        /// This allows this class to have 0, 1, 2, or 3 lists of notification target objects but only to create each list if it will actually be used.
        /// This method locks the single objectCreationMutex to make certain that only once thread will create any given referenced object at a time.
        /// </summary>
        protected static TObjectType CreateEmptyObjectIfNeeded<TObjectType>(ref TObjectType objectHandleRef) where TObjectType : class, new()
        {
            if (objectHandleRef != null)
                return objectHandleRef;

            lock (objectCreationMutex) 
            { 
                return (objectHandleRef = objectHandleRef ?? new TObjectType()); 
            }
        }

        #endregion
    }

	/// <summary> This generic class implements a MT safe event list for EventHandler delegates </summary>
	/// <remarks> 
	/// Add/Remove methods for IEventHandlerNotificationList interface event are fully thread-safe and reentrant.  
	/// Notify method makes asynchronous atomic copy of the current list of delegates and then iteratively (and synchronously) invokes them within try/catch.  
	/// As such Notify method may be safely used while invoking Add/Remove.  However please note that because invocation of the registered delegates occurs 
	/// without owning the mutex, a delegate may be invoked after it has been removed.
	/// Notify method is not intended to be reentrantly invoked but will not generate any exceptions if this occurs.
	/// </remarks>
	/// 

    public class EventHandlerNotificationList<EventArgsType> : BasicNotificationList, IEventHandlerNotificationList<EventArgsType>, INotifyable<EventArgsType>
	{
		#region Ctor

		/// <summary> Ctor defaults to using EventHandlerNotificationList instance itself as source of events </summary>
		public EventHandlerNotificationList() { Source = this; }

		/// <summary> Ctor allows caller to explicitly specify the source object that will be passed to the delegates on Notify </summary>
		public EventHandlerNotificationList(object eventSourceObject) { Source = eventSourceObject; }

		#endregion

		#region IEventHandlerNotificationList<EventArgsType> Members

        /// <summary>
        /// <see cref="EventHandlerDelegate{EventArgsType}"/> event interface.  Allows such delegates to be added/removed from the list.
        /// </summary>
        public new event EventHandlerDelegate<EventArgsType> OnNotify
		{
            add { CreateEmptyObjectIfNeeded(ref eventNotificationDelegateList).Add(value); }
            remove { CreateEmptyObjectIfNeeded(ref eventNotificationDelegateList).Remove(value); }
		}

        /// <summary>
        /// get/set property used as the Source object for the EventHandlerDelegate calls.
        /// Default constructor sets initial value of this Source property to this object.  Alternate constructor allows caller to explicitly provide the initial value.
        /// </summary>
        public object Source { get; set; }

		#endregion

		#region INotifyable<EventArgsType> Members

        /// <summary>
        /// Implementation method for INotifyable{EventArgsType}.Nofity method.  
        /// Invokes each EventHandlerDelegate from a cached copy of the OnNotify event list, passing it a captured copy of the Source property and the given eventArgs property.
        /// Then invokes the base.Notify method so that this method can also be used to signal any of the supported BasicNotificationList targets.
        /// </summary>
        /// <param name="eventArgs"></param>
		public virtual void Notify(EventArgsType eventArgs) 
		{
            int delegateExceptions = 0;
            object source = Source;

            if (eventNotificationDelegateList != null)
            {
                foreach (EventHandlerDelegate<EventArgsType> eventDelegate in eventNotificationDelegateList.Array)
                {
                    try { (eventDelegate ?? nullEventNotificationDelegate)(source, eventArgs); }
                    catch { delegateExceptions++; }
                }
            }

            if (delegateExceptions != 0)
                Asserts.TakeBreakpointAfterFault(Utils.Fcns.CheckedFormat("Notify(EventArgs) triggered exceptions: delegates:{0}", delegateExceptions));

            base.Notify();
        }

		#endregion

        #region private fields

        private Collections.LockedObjectListWithCachedArray<EventHandlerDelegate<EventArgsType>> eventNotificationDelegateList = null;

        private static EventHandlerDelegate<EventArgsType> nullEventNotificationDelegate = (delegate(object source, EventArgsType eventArgs) { });

        #endregion
	}

	#endregion

	//-------------------------------------------------
	#region Guarded Notification object(s)

	/// <summary> 
	/// Defines the interface that is provided by Notification Object type objects.  
	/// These objects support access to a (possibly guarded) Sequenced Object and also support registration of BasicNotificationDelegates with an underlying NotificationList.
    /// Current implementation classes include: <see cref="InterlockedNotificationRefObject{RefObjectType}"/> and <see cref="GuardedNotificationValueObject{ValueObjectType}"/>.
    /// May be used as a <see cref="IBasicNotificationList"/> and may be observed using the 
    /// <see cref="SequencedRefObjectSourceObserver{RefObjectType, SeqNumberType}"/> 
    /// or <see cref="SequencedValueObjectSourceObserver{ValueObjectType, SeqNumberType}"/> 
    /// as appropriate
	/// </summary>
	public interface INotificationObject<ObjectType> : ISequencedObjectSource<ObjectType, int>
	{
        /// <summary>Property gives the caller access to the IBasicNotificationList of INotifyable object that will be signaled when the contained object is replaced</summary>
		IBasicNotificationList NotificationList { get; }
	}

	/// <summary> 
    /// Implementation of <see cref="INotificationObject{RefObjectType}"/> for use with reference objects (classes).  
    /// Uses volatile handle to provide safe/synchronized access to object. 
    /// </summary>
	public class InterlockedNotificationRefObject<RefObjectType> : InterlockedSequencedRefObject<RefObjectType>, INotificationObject<RefObjectType> where RefObjectType : class
	{
        /// <summary>Default Constructor.  By default the contained Object will be null and sequence number will be in its initial, unset, state.</summary>
        public InterlockedNotificationRefObject() { }
        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.  Sequence number will be incremented from initial state.</summary>
        public InterlockedNotificationRefObject(RefObjectType initialValue) : base(initialValue) { }

        /// <summary>Sets the contained object and notifies all parties in the contained notificationList.  Use of lock free, volatile update and notify pattern requires that property setter cannot be used reentrantly.</summary>
		public override RefObjectType Object { set { base.Object = value; notificationList.Notify(); } }

        /// <summary>Property gives the caller access to the IBasicNotificationList of INotifyable object that will be signaled when the contained object is replaced</summary>
        public IBasicNotificationList NotificationList { get { return notificationList; } }

		private BasicNotificationList notificationList = new BasicNotificationList();
	}

	/// <summary>
    /// Implementation of <see cref="INotificationObject{ValueObjectType}"/> for use with value objects (structs).  
    /// Uses mutex to control and synchronize access to guarded value.
    /// </summary>
	public class GuardedNotificationValueObject<ValueObjectType> : GuardedSequencedValueObject<ValueObjectType>, INotificationObject<ValueObjectType> where ValueObjectType : struct
	{
        /// <summary>Default Constructor.  By default the contained Object will be null and sequence number will be in its initial, unset, state.</summary>
        public GuardedNotificationValueObject() { }
        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.  Sequence number will be incremented from initial state.</summary>
        public GuardedNotificationValueObject(ValueObjectType initialValue) : base(initialValue) { }

        /// <summary>
        /// Accessor inherited from GuardedValueObject which uses mutex to control access to stored value object.  Accessor is thread safe and reentrant.
        /// Mutator/Setter sets the contained object and notifies all parties in the contained notificationList.  Mutator/Setter method cannot be used reentrantly since no lock is used during notify portion of update pattern.
        /// </summary>
        public override ValueObjectType Object { set { base.Object = value; notificationList.Notify(); } }

        /// <summary>Property gives the caller access to the IBasicNotificationList of INotifyable object that will be signaled when the contained object is replaced</summary>
        public IBasicNotificationList NotificationList { get { return notificationList; } }

		private BasicNotificationList notificationList = new BasicNotificationList();
	}

	#endregion

	//-------------------------------------------------
	#region SharedWaitEventNotifierSet

    /// <summary>This class is used to provide a pool of reusable (concurrently if needed) <see cref="IEventNotifier"/> objects.</summary>
	/// <remarks>
	/// Such a pool is used when dynamically constructed objects need to make brief use of a IEventNotifier object so as to accomplish efficient 
    /// sleeping in the context of a given call.  In general the WakeAllSticky behavior should be used and the clients should be aware that the 
    /// underlying IEventNotifier object may be in use by more than one client at a time or for more than one purpose at a time.  
    /// However the ability to use such an object on an occasional basis is still a significant improvement on providing wait responsiveness 
    /// (no use of short fixed sleeps) without adding allocation and handle churn related to dynamic construction of <see cref="IEventNotifier"/> objects.
    /// 
    /// Generally this object is used as a type specific singleton.
	/// </remarks>
	public class SharedWaitEventNotifierSet : DisposableBase
	{
        /// <summary>
        /// Defines the default size for a SharedWaitEventNotifiderSet when using the default constructor.
        /// </summary>
        public const int defaultSetSize = 131;

        /// <summary>
        /// Constructs a set of size defaultSetSize (131 at present) using WaitEventNotifier.Behavior.WakeAllSticky behavior.
        /// </summary>
        public SharedWaitEventNotifierSet() : this(defaultSetSize) { }

        /// <summary>
        /// Constructs a set of the given size using WaitEventNotifier.Behavior.WakeAllSticky behavior.
        /// </summary>
        public SharedWaitEventNotifierSet(int setSize) : this(setSize, WaitEventNotifier.Behavior.WakeAllSticky) { }

        /// <summary>
        /// Constructs a set of the given size using the given WaitEventNotifier.Behavior.
        /// </summary>
        public SharedWaitEventNotifierSet(int setSize, WaitEventNotifier.Behavior notifierBehavior) 
		{
            IEventNotifier[] newArray = new IEventNotifier[setSize];

            for (int idx = 0; idx < newArray.Length; idx++)
                newArray[idx] = new WaitEventNotifier(notifierBehavior);

            eventSetArray = newArray;
		}

        /// <summary>
        /// Returns the next shared IEventNotifier object from the shared set.  
        /// Will return null if this object is being or has been disposed.
        /// </summary>
		public IEventNotifier GetNextEventNotifier()
		{
            // use AtomicInt32 to generate next index
			int seqNum = seqNumGen.Increment();

            // capture the most recent eventSetArray
            IEventNotifier [] capturedEventSetArray = eventSetArray;

            if (capturedEventSetArray == null || capturedEventSetArray.Length == 0)
                return null;

            int idx = seqNum % capturedEventSetArray.Length;

            IEventNotifier ien = capturedEventSetArray[idx];

			return ien;
		}

        /// <summary>
        /// sequence number generator.  Use to distribute use of the events in the set by cycling through them in round robin order.
        /// </summary>
		private Utils.AtomicInt32 seqNumGen = new AtomicInt32();

        /// <summary>
        /// The array that contains the actual set.  A fixed sized array will be allocated and populated at object construction time. 
        /// </summary>
        private volatile IEventNotifier[] eventSetArray = null;

        /// <summary>
        /// Provides explicit implementation of the DisposeableBase's corresponding abstract method.
        /// When called explicitly, this method captures the contents of the set, clears it and then 
        /// calls MosaicLib.Utils.Fcns.DisposeOfGivenObject on each object in the captured set.
        /// </summary>
        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (disposeType == DisposeType.CalledExplicitly)
            {
                IEventNotifier[] capturedEventSetArray = eventSetArray;
                eventSetArray = null;

                if (capturedEventSetArray != null)
                {
                    foreach (IEventNotifier ien in capturedEventSetArray)
                        Utils.Fcns.DisposeOfGivenObject(ien);
                }
            }
        }
    }

	#endregion

    //-------------------------------------------------
}

//-------------------------------------------------
