//-------------------------------------------------------------------
/*! @file SharedTimers.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2015 Mosaic Systems Inc.
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using System.Windows.Threading;

namespace MosaicLib.WPF.Timers
{
    #region ISharedDispatcherTimer and SharedDispatcherTimerFactory

    /// <summary>
    /// This is an alternate means of making use of a shared dispatcher timer.  These objects are obtained form the <seealso cref="SharedDispatcherTimerFactory"/> using its GetSharedTimer method.
    /// A client can ask the corresponding timer to start by obtaining IDisposable RunTimerToken using the GetRunTimerToken method.  Once this has been done (by the first client of the shared timer),
    /// the timer will continue to run until the last resulting token has been disposed, at which point the timer will be stopped.  This pattern may be used indefinitely as needed.
    /// </summary>
    public interface ISharedDispatcherTimer : IDisposable
    {
        /// <summary>Gives the rate value that this timer has been requested to run at</summary>
        double Rate { get; }

        /// <summary>Gives the tick interval requested for this timers Tick events.</summary>
        TimeSpan TickInterval { get; }

        /// <summary>Starts the timer (if needed) and returns an IDispoable token for this timer.  The timer will stop after all such tokens have been disposed</summary>
        IDisposable GetRunTimerToken(string clientName);

        /// <summary>Gives the IBasicNotificationList that gets Notified each time the timer "Ticks".</summary>
        IBasicNotificationList TickNotificationList { get; }
    }

    /// <summary>
    /// This is the factory class for generating ISharedDispatcherTimer objects.  Internally it contains a static dictionary or ISharedDispatcherTimer objects, one per rate value.
    /// It also contians the SharedDispatcherTimer class which is used as the implementation backing object for the ISharedDispatherTimer object.
    /// <para/>Use the GetSharedTimer and GetAndStartSharedTimer methods to 
    /// </summary>
    public static class SharedDispatcherTimerFactory
    {
        /// <summary>
        /// Obtains the ISharedDispatcherTimer instance from the dictionary for the given <paramref name="rate"/> value (creating one if needed) and returns it.
        /// <para/>Please note that the given <paramref name="rate"/> will be converted to the nearest tick interval and only one ISharedDispatcherTimer instance is created per requested tick interval.
        /// </summary>
        public static ISharedDispatcherTimer GetSharedTimer(double rate)
        {
            return GetSharedTimer(rate, rate.SafeOneOver().FromSeconds());
        }

        /// <summary>
        /// Obtains the ISharedDispatcherTimer instance from the dictionary for the given <paramref name="tickInterval"/> (creating one if needed) and returns it.
        /// </summary>
        public static ISharedDispatcherTimer GetSharedTimer(TimeSpan tickInterval)
        {
            return GetSharedTimer(tickInterval.TotalSeconds.SafeOneOver(), tickInterval);
        }

        /// <summary>
        /// Obtains the ISharedDispatcherTimer instance from the dictionary for the given <paramref name="tickInterval"/> (creating one if needed) and returns it.
        /// </summary>
        private static ISharedDispatcherTimer GetSharedTimer(double rate, TimeSpan tickInterval)
        {
            ISharedDispatcherTimer sharedDispatcherTimer = sharedTimerDictionary.SafeTryGetValue(tickInterval);

            if (sharedDispatcherTimer == null)
                sharedTimerDictionary[tickInterval] = (sharedDispatcherTimer = new SharedDispatcherTimer(rate, tickInterval));

            return sharedDispatcherTimer;
        }

        private static Dictionary<TimeSpan, ISharedDispatcherTimer> sharedTimerDictionary = new Dictionary<TimeSpan, ISharedDispatcherTimer>();

        /// <summary>
        /// This method removes all shared dispatch timers.  
        /// <para/>This method is generally used as part of unit test code.
        /// </summary>
        public static void RemoveAllSharedTimers()
        {
            var capturedSptArray = sharedTimerDictionary.Values.ToArray();

            sharedTimerDictionary.Clear();

            foreach (var spt in capturedSptArray)
            {
                spt.DisposeOfGivenObject();
            }           
        }

        /// <summary>
        /// This method causes all of the shared timers to invoke all attached tick handler methods immediately.
        /// <para/>This method is generally used as part of unit test code.
        /// </summary>
        public static void ForceServiceAllTimers()
        {
            foreach (var spt in sharedTimerDictionary.Values.ToArray())
            {
                INotifyable notifyable = spt.TickNotificationList as INotifyable;
                if (notifyable != null)
                    notifyable.Notify();
            }
        }

        /// <summary>
        /// Obtains the ISharedDispatcherTimer instance from the dictionary (creating one if needed),
        /// Adds any of the optional Tick event handler signatures (as supported by the ISharedDispatchTimer's TickNotificationList)
        /// and returns an IDisposable token that will release use of the timer and will remove any given Tick event handler signatures from the underlying TickNotificationList when the token is dipsosed.
        /// </summary>
        public static IDisposable GetAndStartSharedTimer(double rate, string clientName, BasicNotificationDelegate onTickNotifyDelegate = null, INotifyable notifyableTarget = null, System.Threading.EventWaitHandle eventWaitHandle = null)
        {
            ISharedDispatcherTimer sharedDispatcherTimer = GetSharedTimer(rate);

            if (onTickNotifyDelegate != null)
                sharedDispatcherTimer.TickNotificationList.OnNotify += onTickNotifyDelegate;

            if (notifyableTarget != null)
                sharedDispatcherTimer.TickNotificationList.AddItem(notifyableTarget);

            if (eventWaitHandle != null)
                sharedDispatcherTimer.TickNotificationList.AddItem(eventWaitHandle);

            IDisposable token = sharedDispatcherTimer.GetRunTimerToken(clientName);

            return new ExplicitDisposeActionList()
                        .AddItems(() => Fcns.DisposeOfGivenObject(token),
                                  () =>
                                  {
                                      if (onTickNotifyDelegate != null)
                                          sharedDispatcherTimer.TickNotificationList.OnNotify -= onTickNotifyDelegate;

                                      if (notifyableTarget != null)
                                          sharedDispatcherTimer.TickNotificationList.RemoveItem(notifyableTarget);

                                      if (eventWaitHandle != null)
                                          sharedDispatcherTimer.TickNotificationList.RemoveItem(eventWaitHandle);
                                  });
        }

        internal interface ISetupAndRelease
        {
            /// <summary>abstract internal Setup method implementation.  Creates the timer, attaches it to the notifiation list and starts it.</summary>
            void Setup(string firstClientName);

            /// <summary>abstract internal Rlease method implementation.  Stops the timer and disposes of it (as well as nulling the local field that holds on to it).</summary>
            void Release(string lastClientName);
        }

        public class SharedDispatcherTimer : SharedResourceSetupAndReleaseBase, ISharedDispatcherTimer, ISetupAndRelease, IDisposable
        {
            public SharedDispatcherTimer(double rate, TimeSpan tickInterval)
            {
                Rate = rate;
                TickInterval = tickInterval;
            }

            /// <summary>Gives the rate value that this timer has been requested to run at</summary>
            public double Rate { get; private set; }

            /// <summary>Gives the tick interval requested for this timers Tick events.</summary>
            public TimeSpan TickInterval { get; private set; }


            IDisposable ISharedDispatcherTimer.GetRunTimerToken(string clientName)
            {
                return base.GetSharedResourceReferenceToken(clientName);
            }

            /// <summary>Gives the IBasicNotificationList that gets Notified each time the timer "Ticks".</summary>
            public IBasicNotificationList TickNotificationList { get { return tickNotificationList; } }
            private readonly BasicNotificationList tickNotificationList = new BasicNotificationList();

            private DispatcherTimer timer;

            /// <summary>abstract internal Setup method implementation.  Creates the timer, attaches it to the notifiation list and starts it.</summary>
            protected override void Setup(string firstClientName)
            {
                if (TickInterval > TimeSpan.Zero)
                {
                    timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TickInterval };
                    timer.Tick += new EventHandler((sender, e) => { tickNotificationList.Notify(); });
                    timer.Start();
                }
            }

            /// <summary>abstract internal Rlease method implementation.  Stops the timer and disposes of it (as well as nulling the local field that holds on to it).</summary>
            protected override void Release(string lastClientName)
            {
                Dispose();
            }

            /// <summary>Implementation for <see cref="IDisposable"/> interface.  Stops the underlying DispatcherTimer (if any).</summary>
            public void Dispose()
            {
                if (timer != null)
                {
                    timer.Stop();
                    timer = null;       // the DispatcherTimer is not IDisposable.
                }
            }

            void ISetupAndRelease.Setup(string firstClientName) { this.Setup(firstClientName); }
            void ISetupAndRelease.Release(string lastClientName) { this.Release(lastClientName); }
        }
    }


    #endregion

    #region Rate specific SharedDispatchTimerResource objects (1Hz, 5Hz, 8Hz, 10Hz)

    /// <summary>Shared resource for a 1Hz DispatchTimer</summary>
    public class SharedDispatchTimer1HzResource : SharedDispatchTimerResource<Details.Timer1HzPeriod> { }

    /// <summary>Shared resource for a 5Hz DispatchTimer</summary>
    public class SharedDispatchTimer5HzResource : SharedDispatchTimerResource<Details.Timer5HzPeriod> { }

    /// <summary>Shared resource for a 8Hz DispatchTimer</summary>
    public class SharedDispatchTimer8HzResource : SharedDispatchTimerResource<Details.Timer8HzPeriod> { }
    
    /// <summary>Shared resource for a 10Hz DispatchTimer</summary>
    public class SharedDispatchTimer10HzResource : SharedDispatchTimerResource<Details.Timer10HzPeriod> { }

    #endregion

    #region SharedDispatchTimerResource class itself

    /// <summary>
    /// Shared resource framework for DispatcherTimer where the rate is determined by the template type.
    /// DispatchTimer starts and stops based on when clients attach to it and release from it.  
    /// Period is defined by the TimerPeriod property that the TTimerPeriodSpec class is constructed to return.
    /// </summary>
    /// <remarks>
    /// Please note that this 
    /// </remarks>
    public class SharedDispatchTimerResource<TTimerPeriodSpec> : SharedResourceSetupAndReleaseBase where TTimerPeriodSpec : Details.ITimerPeriodSpec, new()
    {
        /// <summary>template specified singleton instance of this SharedDispatchTimerResource class</summary>
        public static SharedDispatchTimerResource<TTimerPeriodSpec> Instance { get { return singletonHelper.Instance; } }
        private static SingletonHelperBase<SharedDispatchTimerResource<TTimerPeriodSpec>> singletonHelper = new SingletonHelperBase<SharedDispatchTimerResource<TTimerPeriodSpec>>(() => new SharedDispatchTimerResource<TTimerPeriodSpec>());

        private ISharedDispatcherTimer sharedDispatcherTimer = SharedDispatcherTimerFactory.GetSharedTimer(new TTimerPeriodSpec().TimerPeriod);

        /// <summary>Gives the IBasicNotificationList that gets Notified each time the timer "Ticks".</summary>
        public IBasicNotificationList TickNotificationList { get { return sharedDispatcherTimer.TickNotificationList; } }

        /// <summary>abstract internal Setup method implementation.  Creates the timer, attaches it to the notifiation list and starts it.</summary>
        protected override void Setup(string firstClientName)
        {
            SharedDispatcherTimerFactory.ISetupAndRelease setupAndRelease = sharedDispatcherTimer as SharedDispatcherTimerFactory.ISetupAndRelease;
            if (setupAndRelease != null)
                setupAndRelease.Setup(firstClientName);
        }

        /// <summary>abstract internal Rlease method implementation.  Stops the timer and disposes of it (as well as nulling the local field that holds on to it).</summary>
        protected override void Release(string lastClientName)
        {
            SharedDispatcherTimerFactory.ISetupAndRelease setupAndRelease = sharedDispatcherTimer as SharedDispatcherTimerFactory.ISetupAndRelease;
            if (setupAndRelease != null)
                setupAndRelease.Release(lastClientName);
        }
    }

    #endregion

    namespace Details
    {
        /// <summary>Part of the set of "trick" classes to help produce a small set of TimerResource singletons at a set of different rates</summary>
        public interface ITimerPeriodSpec
        {
            /// <summary>Value defines the period for the timer that uses an instance of this type to setup the timer</summary>
            TimeSpan TimerPeriod { get; }
        }

        /// <summary>Part of the set of "trick" classes to help produce a small set of TimerResource singletons at a set of different rates</summary>
        public class Timer1HzPeriod : ITimerPeriodSpec
        {
            /// <summary>Value defines the period for the timer that uses an instance of this type to setup the timer</summary>
            public TimeSpan TimerPeriod { get { return (1.0).FromSeconds(); } }
        }

        /// <summary>Part of the set of "trick" classes to help produce a small set of TimerResource singletons at a set of different rates</summary>
        public class Timer5HzPeriod : ITimerPeriodSpec
        {
            /// <summary>Value defines the period for the timer that uses an instance of this type to setup the timer</summary>
            public TimeSpan TimerPeriod { get { return (1.0 / 5.0).FromSeconds(); } }
        }

        /// <summary>Part of the set of "trick" classes to help produce a small set of TimerResource singletons at a set of different rates</summary>
        public class Timer8HzPeriod : ITimerPeriodSpec
        {
            /// <summary>Value defines the period for the timer that uses an instance of this type to setup the timer</summary>
            public TimeSpan TimerPeriod { get { return (1.0 / 8.0).FromSeconds(); } }
        }

        /// <summary>Part of the set of "trick" classes to help produce a small set of TimerResource singletons at a set of different rates</summary>
        public class Timer10HzPeriod : ITimerPeriodSpec
        {
            /// <summary>Value defines the period for the timer that uses an instance of this type to setup the timer</summary>
            public TimeSpan TimerPeriod { get { return (1.0 / 10.0).FromSeconds(); } }
        }
    }
}
