//-------------------------------------------------------------------
/*! @file SharedTimers.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved.
 * Copyright (c) 2015 Mosaic Systems Inc., All rights reserved.
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
    /// <summary>Shared resource for a 1Hz DispatchTimer</summary>
    public class SharedDispatchTimer1HzResource : SharedDispatchTimerResource<Details.Timer1HzPeriod> { }
    /// <summary>Shared resource for a 5Hz DispatchTimer</summary>
    public class SharedDispatchTimer5HzResource : SharedDispatchTimerResource<Details.Timer5HzPeriod> { }
    /// <summary>Shared resource for a 8Hz DispatchTimer</summary>
    public class SharedDispatchTimer8HzResource : SharedDispatchTimerResource<Details.Timer8HzPeriod> { }
    /// <summary>Shared resource for a 10Hz DispatchTimer</summary>
    public class SharedDispatchTimer10HzResource : SharedDispatchTimerResource<Details.Timer10HzPeriod> { }

    /// <summary>
    /// Shared resource framework for DispatchTimer where the rate is determined by the template type.
    /// DispatchTimer starts and stops based on when clients attach to it and release from it.  
    /// Period is defined by the TimerPeriod property that the TTimerPeriodSpec class is constructed to return.
    /// </summary>
    public class SharedDispatchTimerResource<TTimerPeriodSpec> : SharedResourceSetupAndReleaseBase where TTimerPeriodSpec : Details.ITimerPeriodSpec, new()
    {
        /// <summary>template specified singleton instance of this SharedDispatchTimerResource class</summary>
        public static SharedDispatchTimerResource<TTimerPeriodSpec> Instance { get { return singletonHelper.Instance; } }
        private static SingletonHelperBase<SharedDispatchTimerResource<TTimerPeriodSpec>> singletonHelper = new SingletonHelperBase<SharedDispatchTimerResource<TTimerPeriodSpec>>(() => new SharedDispatchTimerResource<TTimerPeriodSpec>());

        private DispatcherTimer timer;

        /// <summary>IBasicNotificationList that gets Notified each time the timer "Ticks".</summary>
        public IBasicNotificationList TickNotificationList { get { return tickNotificationList; } }
        private BasicNotificationList tickNotificationList = new BasicNotificationList();

        /// <summary>abstract internal Setup method implementation.  Creates the timer, attaches it to the notifiation list and starts it.</summary>
        protected override void Setup(string firstClientName)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = new TTimerPeriodSpec().TimerPeriod };
            timer.Tick += new EventHandler((sender, e) => { tickNotificationList.Notify(); });
            timer.Start();
        }

        /// <summary>abstract internal Rlease method implementation.  Stops the timer and disposes of it (as well as nulling the local field that holds on to it).</summary>
        protected override void Release(string lastClientName)
        {
            timer.Stop();
            Fcns.DisposeOfObject(ref timer);
        }
    }

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
            public TimeSpan TimerPeriod { get { return TimeSpan.FromSeconds(1.0); } }
        }

        /// <summary>Part of the set of "trick" classes to help produce a small set of TimerResource singletons at a set of different rates</summary>
        public class Timer5HzPeriod : ITimerPeriodSpec
        {
            /// <summary>Value defines the period for the timer that uses an instance of this type to setup the timer</summary>
            public TimeSpan TimerPeriod { get { return TimeSpan.FromSeconds(1.0 / 5.0); } }
        }

        /// <summary>Part of the set of "trick" classes to help produce a small set of TimerResource singletons at a set of different rates</summary>
        public class Timer8HzPeriod : ITimerPeriodSpec
        {
            /// <summary>Value defines the period for the timer that uses an instance of this type to setup the timer</summary>
            public TimeSpan TimerPeriod { get { return TimeSpan.FromSeconds(1.0 / 8.0); } }
        }

        /// <summary>Part of the set of "trick" classes to help produce a small set of TimerResource singletons at a set of different rates</summary>
        public class Timer10HzPeriod : ITimerPeriodSpec
        {
            /// <summary>Value defines the period for the timer that uses an instance of this type to setup the timer</summary>
            public TimeSpan TimerPeriod { get { return TimeSpan.FromSeconds(1.0 / 10.0); } }
        }
    }
}
