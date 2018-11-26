//-------------------------------------------------------------------
/*! @file Notification.cs
 *  @brief This file contains a series of utility classes that are used to handle notification, especially in multi-threaded apps.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2002 Mosaic Systems Inc.  (C++ library version)
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MosaicLib.Time;

namespace MosaicLib.Utils
{
    #region IServiceable and related Service() EM

    /// <summary>
    /// This interface may be used by various objects to support a generic means of servicing the object using an external entity.
    /// </summary>
    public interface IServiceable
    {
        /// <summary>
        /// Caller passes temporary control to the target object's IServiceable.Service method.
        /// This method may be passed a <paramref name="qpcTimeStamp"/> as provided by the caller or QpcTimeStamp.Zero if the caller does not normally provide a non-default time stamp value.
        /// This method generally is expected to return a possitive value if the method made or observed any relevant changes, otherwise the method is generally expected to return zero.
        /// </summary>
        int Service(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp));
    }

    /// <summary>
    /// Partial ExtensionMethods wrapper class
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Allows the caller to Service the given IServiceable <paramref name="obj"/>
        /// If the given <paramref name="obj"/> is null then this method has no effect.
        /// If this Service method throws an exeception then it will be recorded using Asserts.NoteFaultOccurance.
        /// </summary>
        public static int SafeService(this IServiceable obj, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            if (obj != null)
            {
                try
                {
                    return obj.Service(qpcTimeStamp);
                }
                catch (System.Exception ex)
                {
                    string faultStr = "IServiceable object '{0}' Service method threw exception: {1}".CheckedFormat(obj, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                    Asserts.NoteFaultOccurance(faultStr, AssertType.Log);
                }
            }

            return 0;
        }

        /// <summary>
        /// If the given IServiceable <paramref name="obj"/> is non-null, this method uses ThreadPool.QueueUserWorkItem to queue a call to run the SafeService method on it.
        /// If <paramref name="passQpcAtTimeOfCall"/> is true then the SafeService method will be passed QpcTimeStamp.Now, otherwise it will be passed QpcTimeStamp.Zero.
        /// </summary>
        public static void QueueServiceWorkItem(this IServiceable obj, bool passQpcAtTimeOfCall = true)
        {
            if (obj != null)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(o => obj.SafeService(qpcTimeStamp: (passQpcAtTimeOfCall ? QpcTimeStamp.Now : QpcTimeStamp.Zero)));
            }
        }
    }

    #endregion

    //-------------------------------------------------
    #region Notification targets: delegates (BasicNotificationDelegate)

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

        #region internal properties

        internal object CreatedInPool { get; set; }

        /// <summary>Provides a message emitter that may be used to record a Trace of the Notification and Wait usage of this object</summary>
        public Logging.IMesgEmitter TraceEmitter { get { return traceEmitter ?? Logging.NullEmitter; } set { traceEmitter = value; } }
        private Logging.IMesgEmitter traceEmitter = null;

        /// <summary>
        /// Returns true if the TraceEmitter IsEnabled.
        /// </summary>
        protected bool IsTraceEmitterEnabled { get { return (traceEmitter != null && traceEmitter.IsEnabled); } }

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
            /// <summary>Notify wakes one thread if any are waiting (AutoReset) or none if no threads are currently waiting (uses AutoResetEvent internally)</summary>
            WakeOne,
            /// <summary>Notify wakes all threads that are currently waiting (Pulse with ManualReset), or none if no threads are currently waiting (uses ManualResetEvent internally)</summary>
            WakeAll,
            /// <summary>Notify wakes all threads if any are waiting.  If no threads are waiting then it wakes the next thread or threads that attempt to invoke Wait on the previously signaled object.  Last caller to return from Wait in this manner will reset the object to its non-signaling state.  (uses ManualResetEvent internally)</summary>
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
                    eventH = EventWaitHandleHelper.CreateEvent(false, false);
					break;
				case Behavior.WakeAll:
				case Behavior.WakeAllSticky:
                    eventH = EventWaitHandleHelper.CreateEvent(true, false);
                    break;
				default:
					Asserts.NoteFaultOccurance("Unexpected WaitEventNotifier.Behavior value", AssertType.ThrowException);
					break;
			}

            useEnterAndLeaveWait = (behavior == Behavior.WakeAllSticky);

            AddExplicitDisposeAction(() => Release());
		}

        private bool useEnterAndLeaveWait = false;

		#endregion

        #region EventNotifierBase.DisposableBase Members

        /// <summary>
        /// Releases the underlying notification object by closing the SafeWaitHandle maintained here.  
        /// This object is no longer usable for notification purposes after calling this method.
        /// Normally this is used during an explicit dispose pattern.
        /// </summary>
        public void Release()
        {
            EventWaitHandleHelper.Close(eventH);
        }

        #endregion

        #region EventNotifierBase Members

        /// <summary>Caller invokes this to Notify a target object that something notable has happened.</summary>
        public override void Notify()
		{
            using (var eeTrace = (IsTraceEmitterEnabled) ? new Logging.EnterExitTrace(TraceEmitter) : null)
            {
                if (behavior == Behavior.WakeAll)
                    EventWaitHandleHelper.PulseEvent(eventH);
                else
                    EventWaitHandleHelper.SetEvent(eventH);
            }
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
            using (var eeTrace = (IsTraceEmitterEnabled) ? new Logging.EnterExitTrace(TraceEmitter) : null)
            {
                EventWaitHandleHelper.ResetEvent(eventH);
            }
		}

        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        public override bool Wait()
		{
            using (var eeTrace = (IsTraceEmitterEnabled) ? new Logging.EnterExitTrace(TraceEmitter) : null)
            {
                bool signaled = false;
                try
                {
                    if (useEnterAndLeaveWait)
                        EnterWait();

                    signaled = EventWaitHandleHelper.WaitOne(eventH, true);
                }
                catch (System.Exception ex)
                {
                    EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.WaitOne()");
                }
                finally
                {
                    if (useEnterAndLeaveWait)
                        LeaveWait(signaled);
                }

                if (signaled && eeTrace != null)
                    eeTrace.ExtraMessage = "signaled";

                return signaled;
            }
		}

        /// <summary>returns true if object was signaling at end of wait, false otherwise</summary>
        public override bool WaitMSec(int timeLimitInMSec)
		{
            string methodName = (IsTraceEmitterEnabled) ? Fcns.CheckedFormat("{0}({1})", Fcns.CurrentMethodName, timeLimitInMSec) : string.Empty;

            using (var eeTrace = (IsTraceEmitterEnabled) ? new Logging.EnterExitTrace(TraceEmitter, methodName) : null)
            {
                bool signaled = false;
                try
                {
                    if (useEnterAndLeaveWait)
                        EnterWait();

                    // only attempt to actually wait if the timeLimitInMSec is positive or if it is -1 (infinite - wait)
                    if ((timeLimitInMSec >= 0) || (timeLimitInMSec == -1))
                        signaled = EventWaitHandleHelper.WaitOne(eventH, unchecked((uint)timeLimitInMSec), true);
                }
                catch (System.Exception ex)
                {
                    EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.WaitOne(msec)");
                }
                finally
                {
                    if (useEnterAndLeaveWait)
                        LeaveWait(signaled);
                }

                if (signaled && eeTrace != null)
                    eeTrace.ExtraMessage = "signaled";

                return signaled;
            }
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

        private SafeWaitHandle eventH;
		internal Behavior behavior;
		private AtomicInt32 numWaiters = new AtomicInt32(0);

		#endregion
	}

	/// <summary>
	/// This class is an instance of an EventNotifierBase that does nothing when signaled and which sleeps when called to Wait.
	/// </summary>
	public class NullNotifier : EventNotifierBase
	{
        /// <summary>Support a singleton Instance for clients to use.</summary>
        public static NullNotifier Instance { get { return instance; } }
        private static NullNotifier instance = new NullNotifier();

        /// <summary>
        /// Default constructor for a NullNotifier.
        /// </summary>
		public NullNotifier() { }

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
        #region Methods that act directly on SafeWaitHandle values


        /// <summary>
        /// SafeWaitHandle extension method.  Returns true if the given safeWaitHandle is not null, is not Closed and is not Invalid.
        /// </summary>
        public static bool IsValid(this SafeWaitHandle safeWaitHandle)
        {
            return (safeWaitHandle != null && !safeWaitHandle.IsClosed && !safeWaitHandle.IsInvalid);
        }

        /// <summary>
        /// Creates and returns a SafeWaitHandle for a created Win32 event using the Kernel32::CreateEvent API via pInvoke.  Security Event Attributes is passed as null so default security will be used.
        /// </summary>
        public static SafeWaitHandle CreateEvent(bool manualReset, bool initialState)
        {
            return Win32.CreateEvent((IntPtr)null, manualReset, initialState, null);
        }

        /// <summary>
        /// Creates and returns a SafeWaitHandle for a created Win32 event using the Kernel32::CreateEvent API via pInvoke.  Security Event Attributes is passed as null so default security will be used.
        /// </summary>
        public static SafeWaitHandle CreateEvent(bool manualReset, bool initialState, string publicName)
        {
            return Win32.CreateEvent((IntPtr)null, manualReset, initialState, publicName);
        }

        /// <summary>
        /// Attempts to close an open safeWaitHandle.  Does nothing if the safeWaitHandle is Invalid or has already been closed
        /// </summary>
        public static void Close(SafeWaitHandle safeWaitHandle)
        {
            try
            {
                if (safeWaitHandle.IsValid())
                    safeWaitHandle.Close();
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.Close");
            }
        }

        /// <summary>
        /// Tests if the given EventWaitHandle instance is signaling by obtaining a SafeHandle from it and then invoking WaitForSingleObjectEx with a zero timeout and looking at the return code
        /// to see if it is WAIT_OBJECT_0 or some other code.  This method has the side effect of clearing the signaling state for any underlying event object that is configure to AutoReset.
        /// </summary>
        public static bool IsEventSet(SafeWaitHandle safeWaitHandle)
        {
            try
            {
                if (safeWaitHandle.IsValid())
                    return (Win32.WaitForSingleObjectEx(safeWaitHandle, 0, true) == Win32.WAIT_OBJECT_0);
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
        public static bool WaitOne(SafeWaitHandle safeWaitHandle, bool allowAlertToExitWait)
        {
            try
            {
                if (safeWaitHandle.IsValid())
                    return (Win32.WaitForSingleObjectEx(safeWaitHandle, Win32.INFINITE, allowAlertToExitWait) == Win32.WAIT_OBJECT_0);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.WaitOne (Inf)");
            }

            return false;
        }

        /// <summary>
        /// Waits for the given EventWaitHandle instance to become signaling by obtaining a SafeHandle from it and then invoking WaitForSingleObjectEx with the given milliseconds time limit.
        /// Returns true if the wait handle became signaling and returned WAIT_OBJECT_0, or false otherwise.  
        /// This method has the side effect of clearing the signaling state for any underlying event object that is configure to AutoReset.
        /// </summary>
        public static bool WaitOne(SafeWaitHandle safeWaitHandle, uint milliseconds, bool allowAlertToExitWait)
        {
            try
            {
                if (safeWaitHandle.IsValid())
                    return (Win32.WaitForSingleObjectEx(safeWaitHandle, milliseconds, allowAlertToExitWait) == Win32.WAIT_OBJECT_0);
            }
            catch (System.Exception ex)
            {
                HandleEventException(ex, "EventWaitHandleHelper.WaitOne (TimeLimit)");
            }

            return false;
        }

        /// <summary>
        /// Calls kernel.dll::SetEvent on the SafeWaitHandle contained in the given EventWaitHandle object.
        /// </summary>
        public static void SetEvent(SafeWaitHandle safeWaitHandle)
        {
            try
            {
                if (safeWaitHandle.IsValid())
                    Win32.SetEvent(safeWaitHandle);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.SetEvent");
            }
        }

        /// <summary>
        /// Calls kernel.dll::ResetEvent on the SafeWaitHandle contained in the given EventWaitHandle object.
        /// </summary>
        public static void ResetEvent(SafeWaitHandle safeWaitHandle)
        {
            try
            {
                if (safeWaitHandle.IsValid())
                    Win32.ResetEvent(safeWaitHandle);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.ResetEvent");
            }
        }

        /// <summary>
        /// Calls kernel.dll::PulseEvent on the SafeWaitHandle contained in the given EventWaitHandle object.
        /// </summary>
        public static void PulseEvent(SafeWaitHandle safeWaitHandle)
        {
            try
            {
                if (safeWaitHandle.IsValid())
                    Win32.PulseEvent(safeWaitHandle);
            }
            catch (System.Exception ex)
            {
                EventWaitHandleHelper.HandleEventException(ex, "EventWaitHandleHelper.PulseEvent");
            }
        }

        #endregion

        #region Exception handling

        /// <summary>
        /// Directly handles ThreadAbortExceptions by logging them and using a short sleep to prevent uncontrolled spin in a incorrectly protected client.
        /// Attempts to take a breakpoint (if a debugger is attached) and log a suitable message for all other exception types.
        /// </summary>
        public static void HandleEventException(Exception ex, string locationStr)
        {
            string faultStr = Fcns.CheckedFormat("{0} failed: {1}", locationStr, ex);

            if (ex is System.Threading.ThreadAbortException)
            {
                Asserts.NoteConditionCheckFailed(faultStr, AssertType.Log);
                System.Threading.Thread.Sleep(10);     // prevent uncontrolled spinning
            }
            else
            {
                Asserts.TakeBreakpointAfterFault(faultStr);
            }
        }

        #endregion

        #region System.Threading.EventWaitHandle versions of these methods

        /// <summary>
        /// Tests if the given EventWaitHandle instance is signaling by obtaining a SafeHandle from it and then invoking WaitForSingleObjectEx with a zero timeout and looking at the return code
        /// to see if it is WAIT_OBJECT_0 or some other code.  This method has the side effect of clearing the signaling state for any underlying event object that is configure to AutoReset.
        /// </summary>
        public static bool IsEventSet(System.Threading.EventWaitHandle eventWaitHandle)
        {
            try
            {
                SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    return IsEventSet(safeWaitHandle);
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
                SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    return WaitOne(safeWaitHandle, Win32.INFINITE, allowAlertToExitWait);
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
                SafeWaitHandle safeWaitHandle;
                if (GetValidSafeWaitHandle(eventWaitHandle, out safeWaitHandle))
                    return WaitOne(safeWaitHandle, milliseconds, allowAlertToExitWait);
            }
            catch (System.Exception ex)
            {
                HandleEventException(ex, "EventWaitHandleHelper.WaitOne (b)");
            }

            return false;
        }

        /// <summary>
        /// Calls kernel.dll::SetEvent on the SafeWaitHandle contained in the given EventWaitHandle object.
        /// </summary>
        public static void SetEvent(System.Threading.EventWaitHandle eventWaitHandle)
        {
            try
            {
                SafeWaitHandle safeWaitHandle;
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
                SafeWaitHandle safeWaitHandle;
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
                SafeWaitHandle safeWaitHandle;
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
        private static bool GetValidSafeWaitHandle(System.Threading.EventWaitHandle eventWaitHandle, out SafeWaitHandle safeWaitHandle)
        {
            // capture a copy of the SafeWaitHandle from it
            safeWaitHandle = (eventWaitHandle != null ? eventWaitHandle.SafeWaitHandle : null);

            // if the handle is not closed and it is not invalid then attempt to call Win32.SetEvent on it
            return safeWaitHandle.IsValid();
        }

        #endregion
    }

    /// <summary>
    /// Internal static class used to get access to Win32 system calls that are useful in this library.
    /// </summary>
    internal static class Win32
    {
        #region SetEvent Win32 system calls and related definitions

        public const UInt32 INFINITE = 0xFFFFFFFF;
        public const UInt32 WAIT_ABANDONED = 0x00000080;
        public const UInt32 WAIT_OBJECT_0 = 0x00000000;
        public const UInt32 WAIT_TIMEOUT = 0x00000102;
        public const UInt32 WAIT_IO_COMPLETION = 0x000000C0;
        public const UInt32 WAIT_FAILED = 0xFFFFFFFF;

        [DllImport("kernel32.dll", EntryPoint = "CreateEvent", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        public static extern SafeWaitHandle CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", EntryPoint = "WaitForSingleObjectEx", SetLastError = true)]
        public static extern uint WaitForSingleObjectEx(SafeWaitHandle safeWaitHandle, uint dwMilliseconds, bool bAlertable);

        [DllImport("kernel32.dll", EntryPoint = "SetEvent", SetLastError = true)]
        public static extern bool SetEvent(SafeWaitHandle safeWaitHandle);

        [DllImport("kernel32.dll", EntryPoint = "ResetEvent", SetLastError = true)]
        public static extern bool ResetEvent(SafeWaitHandle safeWaitHandle);

        [DllImport("kernel32.dll", EntryPoint = "PulseEvent", SetLastError = true)]
        public static extern bool PulseEvent(SafeWaitHandle safeWaitHandle);

        [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId", BestFitMapping = false)]
        public static extern int GetCurrentThreadId();

        [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcessId", BestFitMapping = false)]
        public static extern int GetCurrentProcessId();

        #endregion
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

        /// <summary>Removes the first instance of the given <see cref="INotifyable"/> target object from the list.  Has no effect if no such instance is found in the list.</summary>
        void RemoveItem(INotifyable notifyableTarget);

        /// <summary>Adds the given <see cref="System.Threading.EventWaitHandle"/> object to the list</summary>
        void AddItem(System.Threading.EventWaitHandle eventWaitHandle);

        /// <summary>Removes the first instance of the given <see cref="System.Threading.EventWaitHandle"/> object from the list.  Has no effect if no such instance is found in the list.</summary>
        void RemoveItem(System.Threading.EventWaitHandle eventWaitHandle);

        /// <summary>Adds the given <paramref name="action"/> to the list</summary>
        void AddItem(Action action);

        /// <summary>Remvoes the first instance of the given <paramref name="action"/> from the list.  Has no effect if no such instance is found in the list.</summary>
        void RemoveItem(Action action);
	}

    /// <summary>
    /// Combines the capabilities of a IBasicNotificationList interface (ability to add and remove target objects) and the ability for any party to
    /// notify the list.
    /// </summary>
    public interface INotifyableBasicNotificationList : IBasicNotificationList, INotifyable
    {
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
            add { AddItem(ref basicNotificationDelegateList, value); }
            remove { RemoveItem(ref basicNotificationDelegateList, value); }
		}

        /// <summary>Adds the given <see cref="INotifyable"/> target object to the list</summary>
        public void AddItem(INotifyable notifyableTarget)
		{
            AddItem(ref notifyableList, notifyableTarget);
		}

        /// <summary>Removes the first instance of the given <see cref="INotifyable"/> target object from the list</summary>
        public void RemoveItem(INotifyable notifyableTarget)
		{
            RemoveItem(ref notifyableList, notifyableTarget);
		}

        /// <summary>Adds the given <see cref="System.Threading.EventWaitHandle"/> object to the list</summary>
        public void AddItem(System.Threading.EventWaitHandle eventWaitHandle)
        {
            AddItem(ref eventWaitHandleList, eventWaitHandle);
        }

        /// <summary>Removes the first instance of the given <see cref="System.Threading.EventWaitHandle"/> object from the list</summary>
        public void RemoveItem(System.Threading.EventWaitHandle eventWaitHandle)
        {
            RemoveItem(ref eventWaitHandleList, eventWaitHandle);
        }

        /// <summary>Adds the given <paramref name="action"/> to the list</summary>
        public void AddItem(Action action)
        {
            AddItem(ref basicNotificationActionList, action);
        }

        /// <summary>Remvoes the first instance of the given <paramref name="action"/> from the list.  Has no effect if no such instance is found in the list.</summary>
        public void RemoveItem(Action action)
        {
            RemoveItem(ref basicNotificationActionList, action);
        }

		#endregion

		#region INotifyable Members

        /// <summary>Caller invokes this to Notify a target object that something notable has happened.</summary>
        public virtual void Notify() 
		{
			int exceptionCount = 0;
            System.Exception firstEx = null;

            Action[] actionArray = GetCombinedActionArrayAndRebuildIfNeeded();

            foreach (Action a in actionArray)
            {
                try
                {
                    a();
                }
                catch (System.Exception ex)
                {
                    firstEx = firstEx ?? ex;
                    exceptionCount++;
                }
            }

            if (firstEx != null)
            {
                Asserts.TakeBreakpointAfterFault(Utils.Fcns.CheckedFormat("Notify triggered {0} exception(s) [first: {1}]", exceptionCount, firstEx.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)));
            }
		}

		#endregion

        #region Internals

        /// <summary>
        /// Protected mutex object used during object creation as required within the CreateEmptyObjectIfNeeded static method.
        /// </summary>
        protected static readonly object mutex = new object();

        private List<Action> basicNotificationActionList = null;
        private List<BasicNotificationDelegate> basicNotificationDelegateList = null;
        private List<INotifyable> notifyableList = null;
        private List<System.Threading.EventWaitHandle> eventWaitHandleList = null;

        /// <summary>Internal helper method used to add items to the lists used here</summary>
        protected void AddItem<TItemType>(ref List<TItemType> listRef, TItemType item) where TItemType: class
        {
            lock (mutex)
            {
                if (listRef == null)
                    listRef = new List<TItemType>();

                if (item != null)
                    listRef.Add(item);

                InnerNoteListContentsChanged();
            }
        }

        /// <summary>Internal helper method used to add items to the lists used here</summary>
        protected void RemoveItem<TItemType>(ref List<TItemType> listRef, TItemType item) where TItemType : class
        {
            lock (mutex)
            {
                if (listRef == null)
                    listRef = new List<TItemType>();

                if (item != null)
                    listRef.Remove(item);

                InnerNoteListContentsChanged();
            }
        }

        /// <summary>
        /// Called while owning the mutex to indicate that one or more list contents have been changed.
        /// </summary>
        protected virtual void InnerNoteListContentsChanged()
        {
            savedCombinedActionArray = null;
        }

        private volatile Action[] savedCombinedActionArray = emptyActionArray;

        private Action[] GetCombinedActionArrayAndRebuildIfNeeded()
        {
            Action[] actionArray = savedCombinedActionArray;

            if (actionArray != null)
                return actionArray;

            lock (mutex)
            {
                savedCombinedActionArray = actionArray = emptyActionArray
                                    .Concat(basicNotificationActionList as IList<Action> ?? Collections.ReadOnlyIList<Action>.Empty)
                                    .Concat((basicNotificationDelegateList as IList<BasicNotificationDelegate> ?? Collections.ReadOnlyIList<BasicNotificationDelegate>.Empty).Select(bnd => (Action)(() => bnd())))
                                    .Concat((notifyableList as IList<INotifyable> ?? Collections.ReadOnlyIList<INotifyable>.Empty).Select(notifyable => (Action)(() => notifyable.Notify())))
                                    .Concat((eventWaitHandleList as IList<System.Threading.EventWaitHandle> ?? Collections.ReadOnlyIList<System.Threading.EventWaitHandle>.Empty).Select(ewl => (Action)(() => ewl.Set())))
                                    .ToArray();

                return actionArray;
            }
        }

        private static readonly Action[] emptyActionArray = Utils.Collections.EmptyArrayFactory<Action>.Instance;

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
    public class EventHandlerNotificationList<EventArgsType> : BasicNotificationList, IEventHandlerNotificationList<EventArgsType>, INotifyable<EventArgsType>
	{
		#region Construction

		/// <summary>Constructor defaults to using EventHandlerNotificationList instance itself as source of events </summary>
		public EventHandlerNotificationList() { Source = this; }

        /// <summary>Constructor allows caller to explicitly specify the source object that will be passed to the delegates on Notify </summary>
		public EventHandlerNotificationList(object eventSourceObject) { Source = eventSourceObject; }

		#endregion

		#region IEventHandlerNotificationList<EventArgsType> Members

        /// <summary>
        /// <see cref="EventHandlerDelegate{EventArgsType}"/> event interface.  Allows such delegates to be added/removed from the list.
        /// </summary>
        public new event EventHandlerDelegate<EventArgsType> OnNotify
		{
            add { AddItem(ref eventHandlerList, value); }
            remove { RemoveItem(ref eventHandlerList, value); }
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
		public virtual void Notify(EventArgsType eventArgs) 
		{
            object source = Source;

            int exceptionCount = 0;
            System.Exception firstEx = null;

            EventHandlerDelegate<EventArgsType>[] eventHandlerArray = GetEventHandlerArrayAndRebuildIfNeeded();

            foreach (EventHandlerDelegate<EventArgsType> eventHandler in eventHandlerArray)
            {
                try 
                { 
                    eventHandler(source, eventArgs); 
                }
                catch (System.Exception ex)
                {
                    firstEx = firstEx ?? ex;
                    exceptionCount++; 
                }
            }

            if (firstEx != null)
            {
                Asserts.TakeBreakpointAfterFault(Utils.Fcns.CheckedFormat("Notify(EventArgs) triggered {0} exception(s) [first: {1}]", exceptionCount, firstEx.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)));
            }

            base.Notify();
        }

		#endregion

        #region Internals

        private List<EventHandlerDelegate<EventArgsType>> eventHandlerList = null;

        private volatile EventHandlerDelegate<EventArgsType>[] savedEventHandlerArray = emptyEventHandlerArray;

        protected override void InnerNoteListContentsChanged()
        {
            base.InnerNoteListContentsChanged();
            savedEventHandlerArray = null;
        }

        private EventHandlerDelegate<EventArgsType>[] GetEventHandlerArrayAndRebuildIfNeeded()
        {
            EventHandlerDelegate<EventArgsType>[] eventHandlerArray = savedEventHandlerArray;

            if (eventHandlerArray != null)
                return eventHandlerArray;

            lock (mutex)
            {
                savedEventHandlerArray = eventHandlerArray = eventHandlerList.SafeToArray();

                return eventHandlerArray;
            }
        }

        private static readonly EventHandlerDelegate<EventArgsType>[] emptyEventHandlerArray = Collections.EmptyArrayFactory<EventHandlerDelegate<EventArgsType>>.Instance;

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
	public class InterlockedNotificationRefObject<RefObjectType> : InterlockedSequencedRefObject<RefObjectType>, INotifyable, INotificationObject<RefObjectType> where RefObjectType : class
	{
        /// <summary>Default Constructor.  By default the contained Object will be null and sequence number will be in its initial, unset, state.</summary>
        public InterlockedNotificationRefObject() { }

        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.  Sequence number will be incremented from initial state.</summary>
        public InterlockedNotificationRefObject(RefObjectType initialValue) 
            : base(initialValue) 
        { }

        /// <summary>Sets the contained object and notifies all parties in the contained notificationList.  Use of lock free, volatile update and notify pattern requires that property setter cannot be used reentrantly.</summary>
		public override RefObjectType Object { set { base.Object = value; notificationList.Notify(); } }

        /// <summary>Property gives the caller access to the IBasicNotificationList of INotifyable object that will be signaled when the contained object is replaced</summary>
        public IBasicNotificationList NotificationList { get { return notificationList; } }

        /// <summary>Caller invokes this to Notify all items in the NotificatinList that something notable has happened.  This is done automatically when publishing a new object but may be done explicitly using this method without publishing a new object.</summary>
        public void Notify() { notificationList.Notify(); }

		private BasicNotificationList notificationList = new BasicNotificationList();

    }

	/// <summary>
    /// Implementation of <see cref="INotificationObject{ValueObjectType}"/> for use with value objects (structs).  
    /// Uses mutex to control and synchronize access to guarded value.
    /// </summary>
    public class GuardedNotificationValueObject<ValueObjectType> : GuardedSequencedValueObject<ValueObjectType>, INotifyable, INotificationObject<ValueObjectType> where ValueObjectType : struct
	{
        /// <summary>Default Constructor.  By default the contained Object will be null and sequence number will be in its initial, unset, state.</summary>
        public GuardedNotificationValueObject() { }

        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.  Sequence number will be incremented from initial state.</summary>
        public GuardedNotificationValueObject(ValueObjectType initialValue) 
            : base(initialValue) 
        { }

        /// <summary>
        /// Accessor inherited from GuardedValueObject which uses mutex to control access to stored value object.  Accessor is thread safe and reentrant.
        /// Mutator/Setter sets the contained object and notifies all parties in the contained notificationList.  Mutator/Setter method cannot be used reentrantly since no lock is used during notify portion of update pattern.
        /// </summary>
        public override ValueObjectType Object { set { base.Object = value; notificationList.Notify(); } }

        /// <summary>Property gives the caller access to the IBasicNotificationList of INotifyable object that will be signaled when the contained object is replaced</summary>
        public IBasicNotificationList NotificationList { get { return notificationList; } }

        /// <summary>Caller invokes this to Notify all items in the NotificatinList that something notable has happened.  This is done automatically when publishing a new object but may be done explicitly using this method without publishing a new object.</summary>
        public void Notify() { notificationList.Notify(); }

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
    
    [Obsolete("Use of SharedWaitEventNotifiderSet is obsolete.  Deficiencies in implementation of PulseEvent Win32 API call prevents correct use of the WakeAll and WakeAllSticky type WaitEventNotifiers [2014-10-21]")]
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
                newArray[idx] = new WaitEventNotifier(notifierBehavior) { CreatedInPool = this };

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
    #region EventNotifierPool

    /// <summary>This class is used to provide a pool of reusable <see cref="IEventNotifier"/> objects.</summary>
    public class EventNotifierPool : DisposableBase
    {
        /// <summary>
        /// Defines the default initial pool size for a WaitEventNotifiderPool when using the default constructor.
        /// </summary>
        public const int defaultInitialPoolSize = 16;

        /// <summary>
        /// Defines the default maximum pool size for a WaitEventNotifiderPool when using the default constructor.
        /// </summary>
        public const int defaultMaximumPoolSize = 256;

        /// <summary>
        /// Constructs a set of defaultInitialPoolSize (16) WaitEventNotifier.Behavior.WakeOne WaitEventNotifiers and sets the maxumum pool size to defaultMaximumPoolSize (256).
        /// </summary>
        public EventNotifierPool() 
            : this(defaultInitialPoolSize, defaultMaximumPoolSize, WaitEventNotifier.Behavior.WakeOne) 
        { 
        }

        /// <summary>
        /// Constructs pool with the given initial, and maximum pool size and defines the behavior of the pooled objects using the given behavior
        /// </summary>
        public EventNotifierPool(int initialPoolSize, int maximumPoolSize, WaitEventNotifier.Behavior behavior) 
        {
            Behavior = behavior;

            eventNotifierPool = new MosaicLib.Utils.Pooling.BasicObjectPool<WaitEventNotifier>()
            {
                Capacity = maximumPoolSize,
                ObjectFactoryDelegate = () => new WaitEventNotifier(behavior) { CreatedInPool = this }
            };

            foreach (var wen in Enumerable.Range(0, initialPoolSize).Select((idx) => eventNotifierPool.GetFreeObjectFromPool()))
            {
                var wenRef = wen;
                eventNotifierPool.ReturnObjectToPool(ref wenRef);
            }

            AddExplicitDisposeAction(() => eventNotifierPool.Shutdown());
        }

        /// <summary>
        /// Allows the caller to determine the WaitEventNotifier.Behavior that is used to construct new WaitEventNotifiers in this pool.
        /// </summary>
        public WaitEventNotifier.Behavior Behavior { get; private set; }
        private Pooling.BasicObjectPool<WaitEventNotifier> eventNotifierPool;

        /// <summary>
        /// Returns the a WaitEventNotifier object from the pool casted as an IEventNotifier.  
        /// </summary>
        public IEventNotifier GetInstanceFromPool()
        {
            return eventNotifierPool.GetFreeObjectFromPool();
        }

        /// <summary>
        /// Returns the given object to the pool.  given Item must be non-null and must have been returned by a call to GetInstanceFromPool above.
        /// </summary>
        public void ReturnInstanceToPool(ref IEventNotifier item)
        {
            WaitEventNotifier wenRef = item as WaitEventNotifier;

            if (item == null)
                throw new System.NullReferenceException("Given item is null");
            else if (wenRef == null || wenRef.behavior != Behavior || !System.Object.ReferenceEquals((object) this, wenRef.CreatedInPool))
                throw new System.NotSupportedException("Given item was not obtained from this pool.");

            item = null;
            eventNotifierPool.ReturnObjectToPool(ref wenRef);
        }
    }


    #endregion

    //-------------------------------------------------
}

//-------------------------------------------------
