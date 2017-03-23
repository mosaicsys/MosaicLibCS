//-------------------------------------------------------------------
/*! @file QpcTime.cs
 *  @brief This file defines interfaces and classes that help give the client access to and use of the windows platform performance counter related timer functions.
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
using System.Runtime.InteropServices;

using MosaicLib;
using MosaicLib.Utils;

namespace MosaicLib.Time
{
    #region Qpc static wrapper class

    /// <summary>This class acts as a form of namespace in which to place various QueryPerformanceCounter (QPC) related items</summary>
	public static class Qpc
	{
		#region extern kernal functions

		/// <summary>
		/// Access method to invoke Kernel32.dll's QueryPerformanceCounter method
		/// </summary>
		/// <param name="lpPerformanceCount">output variable that is to receive the 64 bit performance counter value.</param>
		/// <returns>true on success, false if the performance counter is not available on this platform.</returns>
		[DllImport("Kernel32.dll")]
		private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

		/// <summary>
		/// Access method to invoke Kernel32.dll's QueryPerformanceFrequency method
		/// </summary>
		/// <param name="lpFrequency">output variable that is to receive the 64 bit performance counter frequency (in counts per sec) value.</param>
		/// <returns>true on success, false if the performance counter is not available on this platform.</returns>
		[DllImport("Kernel32.dll")]
		private static extern bool QueryPerformanceFrequency(out long lpFrequency);

		#endregion

		#region member variables

		/// <summary>static saved value from first successfull call to QueryPerformanceFrequency</summary>
		private static long qpcRateInHZ = 0;

		/// <summary>static saved reciprocal from first successfull call to QueryPerformanceFrequency</summary>
		private static double qpcPeriodInSec = 0.0;

		#endregion

		#region constructor

		/// <summary>This is the static constructor for the static Qpc class.  It attempts to fill in the qpcRateInHZ and the qpcPeriodInSec fields.</summary>
		static Qpc()
		{
			if (QueryPerformanceFrequency(out qpcRateInHZ) == false)
			{
				// high-performance counter not supported

				throw new System.ComponentModel.Win32Exception();
			}

			if (qpcRateInHZ > 0)
			{
				qpcPeriodInSec = 1.0 / (double) qpcRateInHZ;
			}
		}

		#endregion

		#region properties

		/// <summary>invokes QueryPerformanceCounter and returns the resulting 64 bit value.</summary>
		public static long CountNow { get { long qpcCount = 0; QueryPerformanceCounter(out qpcCount); return qpcCount; } }

		/// <summary>samples and returns the QPC counter value converted to units of elapsed time (since counter start) in seconds.</summary>
		public static double TimeNow { get { return CountNow * qpcPeriodInSec; } }

		/// <summary>property that provides the QPC Rate in HZ as a 64 bit integer.</summary>
		public static long RateI64 { get { return qpcRateInHZ; } }

		/// <summary>property that provides the QPC Rate in Hz as a double.</summary>
		public static double RateF8 { get { return (double) RateI64; } }

		#endregion
	}

	#endregion

	#region QpcTimeStamp

	/// <summary>This struct provides properties and methods to adapts the QPC counter for use as a standard TimeStamp type object.</summary>
	/// <remarks>
	/// This struct stores a single QPC counter value as a double and allows it to be used as a TimeStamp type of object by supporting all of the
	/// relevant interfaces and related properties and methods.
	/// </remarks>

	public struct QpcTimeStamp : IComparable, IComparable<QpcTimeStamp>, IEquatable<QpcTimeStamp>
	{
		/// <summary>Storage for the actual time stamp value.</summary>
		private double qpcTime;						// value defaults to zero

		/// <summary>Constructor for use with an explicitly provied double qpc time stamp value.</summary>
		/// <param name="time">the double QPC time value to retain as the time stamp</param>
		public QpcTimeStamp(double time) { qpcTime = time; }

		/// <summary>Copy Constructors</summary>
		/// <param name="rhs">The right hand side (rhs) to copy the timestamp from.</param>
		public QpcTimeStamp(QpcTimeStamp rhs) { qpcTime = rhs.qpcTime; }

		/// <summary>Static method to return the constant Zero timestamp value.</summary>
		public static QpcTimeStamp Zero { get { return default(QpcTimeStamp); } }

		/// <summary>Static method to return the current QPC counter value in a new QPCTimeStamp</summary>
		public static QpcTimeStamp Now { get { return new QpcTimeStamp(Qpc.TimeNow); } }

		/// <summary>Returns the stored double qpc timestamp value.</summary>
		public double Time { get { return qpcTime; } set { qpcTime = value; } }

		/// <summary>Returns the TimeSpan between Now and the stored qpc timestamp.</summary>
		public TimeSpan Age { get { return (Now - this); } }

		/// <summary>Sets the stored qpc timestamp to Now.</summary>
		public QpcTimeStamp SetToNow() { Time = Qpc.TimeNow; return this; }

		/// <summary>True if the stored qpc timestamp is zero.</summary>
		public bool IsZero { get { return Time == 0.0; } }

		/// <summary>Adds the given TimeSpan to the stored timestamp.</summary>
		public QpcTimeStamp Add(TimeSpan rhs) { Time += rhs.TotalSeconds; return this; }

		/// <summary>Adds the given double time period in seconds to the stored timestamp.</summary>
		public QpcTimeStamp Add(double rhs) { Time += rhs; return this; }

		/// <summary>Subtracts the given TimeSpan from the stored timestamp.</summary>
		public QpcTimeStamp Substract(TimeSpan rhs) { Time -= rhs.TotalSeconds; return this; }

		/// <summary>Subtracts the given double time period in seconds from the stored timestamp.</summary>
		public QpcTimeStamp Substract(double rhs) { Time -= rhs; return this; }

		/// <summary>returns the TimeSpan for the difference between the lhs timestamp and the rhs timestamp (lhs - rhs)</summary>
		public static TimeSpan operator -(QpcTimeStamp lhs, QpcTimeStamp rhs) { return (lhs.Time - rhs.Time).FromSeconds(); }

		/// <summary>returns a new QpcTimeStamp value with the timestamp set to the lhs timestamp value decreased by the given rhs TimeSpan</summary>
		public static QpcTimeStamp operator -(QpcTimeStamp lhs, double rhs) { return new QpcTimeStamp(lhs.Time - rhs); }

		/// <summary>returns a new QpcTimeStamp value with the timestamp set to the lhs timestamp value increased  by the given rhs TimeSpan</summary>
		public static QpcTimeStamp operator +(QpcTimeStamp lhs, double rhs) { return new QpcTimeStamp(lhs.Time + rhs); }

		/// <summary>returns a new QpcTimeStamp value with the timestamp set to the lhs timestamp value decreased by the given rhs TimeSpan</summary>
		public static QpcTimeStamp operator -(QpcTimeStamp lhs, TimeSpan rhs) { return new QpcTimeStamp(lhs.Time - rhs.TotalSeconds); }

		/// <summary>returns a new QpcTimeStamp value with the timestamp set to the lhs timestamp value increased  by the given rhs TimeSpan</summary>
		public static QpcTimeStamp operator +(QpcTimeStamp lhs, TimeSpan rhs) { return new QpcTimeStamp(lhs.Time + rhs.TotalSeconds); }

		/// <summary>returns true if the lhs timestamp is not equal to the rhs timestamp</summary>
		public static bool operator !=(QpcTimeStamp lhs, QpcTimeStamp rhs) { return lhs.Time != rhs.Time; }

		/// <summary>returns true if the lhs timestamp comes before the rhs timestamp</summary>
		public static bool operator <(QpcTimeStamp lhs, QpcTimeStamp rhs) { return lhs.Time < rhs.Time; }

		/// <summary>returns true if the lhs timestamp comes before, or equals, the rhs timestamp</summary>
		public static bool operator <=(QpcTimeStamp lhs, QpcTimeStamp rhs) { return lhs.Time <= rhs.Time; }

		/// <summary>returns true if the lhs timestamp equals the rhs timestamp</summary>
		public static bool operator ==(QpcTimeStamp lhs, QpcTimeStamp rhs) { return lhs.Time == rhs.Time; }

		/// <summary>returns true if the lhs timestamp comes after the rhs timestamp</summary>
		public static bool operator >(QpcTimeStamp lhs, QpcTimeStamp rhs) { return lhs.Time > rhs.Time; }

		/// <summary>returns true if the lhs timestamp comes after, or equals, the rhs timestamp</summary>
		public static bool operator >=(QpcTimeStamp lhs, QpcTimeStamp rhs) { return lhs.Time >= rhs.Time; }

		/// <summary>returns the result of lhs.CompareTo(rhs)</summary>
		public static int Compare(QpcTimeStamp lhs, QpcTimeStamp rhs) { return lhs.Time.CompareTo(rhs.Time); }

		/// <summary>Invokes CompareTo on the underlying double values and returns its result.</summary>
		public int CompareTo(QpcTimeStamp rhs) { return Time.CompareTo(rhs.Time); }

		/// <summary>Verifies that the rhs object is a QpcTimeStamp and, if so, Invokes CompareTo on the underlying double values and returns its result.</summary>
		/// <remarks>Throws an System.ArgumentException if the rhsObj is not a QpcTimeStamp.</remarks>
		public int CompareTo(object rhsObj)
		{
			if (rhsObj != null && rhsObj is QpcTimeStamp)
				return CompareTo((QpcTimeStamp) rhsObj);
			else
				throw new System.ArgumentException("Invalid use of QpcTimeStamp.Compare(object rhs)");
		}

		/// <summary>Returns true if the rhs timestamp value is equal to this object's timestamp value.</summary>
		public bool Equals(QpcTimeStamp rhs) { return Time.Equals(rhs.Time); }

		/// <summary>Returns true if the rhsObject is a QpcTimeStamp and it Equals this object's value.</summary>
		public override bool Equals(object rhsObj) 
		{
			if (rhsObj != null && rhsObj is QpcTimeStamp)
				return Equals((QpcTimeStamp) rhsObj);
			return false;
		}

		/// <summary>static verison of Equals.  Identical to lhs.Equals(rhs);</summary>
		public static bool Equals(QpcTimeStamp lhs, QpcTimeStamp rhs) { return lhs.Equals(rhs); }

		/// <summary>returns the GetHashCode value for the stored timestamp.</summary>
		public override int GetHashCode() { return Time.GetHashCode(); }

		/// <summary>returns the stored timestamp formatted as a string.</summary>
		public override string ToString() { return Time.ToString(); }
	}

	#endregion

	#region QpcTimer

    /// <summary>
    /// The struct provides a form of restable Timer that is implemented using QpcTimeStamps.  
    /// This Timer provides the ability for a client to mark the start of an interval and to query the timer one some recuring basis to find out if the interval has elapsed since, or not.
    /// In addition the Timer allows the interval to automatically be restated when its completion is signaled.
    /// </summary>
	public struct QpcTimer
    {
        #region Construction 

        /// <summary>Defines timer based on given interval and autoReset flag</summary>
        /// <param name="triggerInterval">TimeSpan value that defines the interval length</param>
        /// <param name="autoReset">boolean value that determines if the timer resets itself on reported experation of the timer or if it requires the client to explicitly restart it using the Reset method.</param>
        public QpcTimer(TimeSpan triggerInterval, bool autoReset = false)
            : this(triggerInterval.TotalSeconds, autoReset: autoReset) 
        { }

        /// <summary>Defines timer based on given interval and autoReset flag</summary>
        /// <param name="triggerIntervalInSec">double value that defines the timer interval in units of seconds.</param>
        /// <param name="autoReset">boolean value that determines if the timer resets itself on reported experation of the timer or if it requires the client to explicitly restart it using the Reset method.</param>
        public QpcTimer(double triggerIntervalInSec, bool autoReset = false)
            : this()
		{
            lastTriggerTimeStamp = QpcTimeStamp.Zero;           // essentially calls Stop();
			TriggerIntervalInSec = triggerIntervalInSec;        // advances next
			AutoReset = autoReset;
        }

        // default constructor always leaves all fields in default, zero state

        #endregion

        /// <summary>
        /// Property can be used to get or set the TriggerInterval as a double value in units of seconds.  
        /// Setting this interval implicitly resets the timer to expire after the interval has elapsed since the last occurance (if it has already been started).
        /// </summary>
        public double TriggerIntervalInSec { get { return triggerIntervalInSec; } set { triggerIntervalInSec = value; nextTriggerTimeStamp = lastTriggerTimeStamp + value; } }
 
        /// <summary>
        /// Property can be used to get or set the TriggerInterval as a TimeSpan value.  
        /// Setting this interval implicitly resets the timer to expire after the interval has elpased since the since the last occurance (if it has already been started).
        /// </summary>
        public TimeSpan TriggerInterval { get { return TriggerIntervalInSec.FromSeconds(); } set { TriggerIntervalInSec = value.TotalSeconds; } }

        /// <summary>
        /// Indicates that the timer has been set to use autoReset behavior.  Setter allows autoReset behavior to be enabled or disabled, typically as part of the construction time property initializer list.
        /// </summary>
        public bool AutoReset
        {
            get { return (SelectedBehavior & Behavior.AutoReset) != Behavior.None; }
            set
            {
                if (value)
                    SelectedBehavior = SelectedBehavior | Behavior.AutoReset;
                else
                    SelectedBehavior = SelectedBehavior & ~Behavior.AutoReset;
            }
        }

        /// <summary>
        /// This enum defines the various behaviors that a QpcTimer can be configured to use.
        /// <para/>None (0x00), AutoReset (0x01), ElapsedTimeIsZeroWhenStopped (0x02), ZeroTriggerIntervalRunsTimer (0x04), IntervalMeasurementTimer (0x08)
        /// <para/>NewDefault = (Behavior.ElapsedTimeIsZeroWhenStopped | Behavior.ZeroTriggerIntervalRunsTimer)
        /// <para/>NewAutoReset = (Behavior.AutoReset | Behavior.ElapsedTimeIsZeroWhenStopped | Behavior.ZeroTriggerIntervalRunsTimer)
        /// </summary>
        [Flags]
        public enum Behavior : int
        {
            /// <summary>Struct default constructor default value - all zeros - no listed behaviors are selected. [0x00]</summary>
            None = 0x00,
         
            /// <summary>Selects that the AutoReset behavior shall be used [0x01]</summary>
            AutoReset = 0x01,

            /// <summary>Selects that the ElapsedTime properties will report zero when the timer is stopped.  Otherwise they report large elapsed times (now - zero) [0x02]</summary>
            ElapsedTimeIsZeroWhenStopped = 0x02,

            /// <summary>Selects that the IsTriggered and GetIsTriggered functions/properties will allow the timer to run if when the TriggerInterval is zero.  default is that they will not. [0x04]</summary>
            ZeroTriggerIntervalRunsTimer = 0x04,

            /// <summary>When this behavior is selected the timer will never trigger and each time the ElapsedTimer is obtained the timer will reset itself. [0x08]</summary>
            IntervalMeasurementTimer = 0x08,

            /// <summary>Selects that both ElapsedTimeIsZeroWhenStopped and ZeroTriggerIntervaleRunsTimer will be enabled.<para/>This default value must be selected explicitly when desired [0x06]</summary>
            NewDefault = (Behavior.ElapsedTimeIsZeroWhenStopped | Behavior.ZeroTriggerIntervalRunsTimer),

            /// <summary>Selects that AutoReset, ElapsedTimeIsZeroWhenStopped, and ZeroTriggerIntervaleRunsTimer will be enabled. [0x07]</summary>
            NewAutoReset = (Behavior.AutoReset | Behavior.ElapsedTimeIsZeroWhenStopped | Behavior.ZeroTriggerIntervalRunsTimer),
        }

        /// <summary>
        /// This property defines the timer behavior options and allows the caller to change them using the setter.
        /// </summary>
        public Behavior SelectedBehavior { get; set; }

        /// <summary>indicates that ElapsedTime will be forced to zero when timer is stopped</summary>
        public bool ElapsedTimeIsZeroWhenStopped { get { return ((SelectedBehavior & Behavior.ElapsedTimeIsZeroWhenStopped) != Behavior.None); } }

        /// <summary>Indicates that Timer may trigger even when TriggerInterval is zero</summary>
        public bool ZeroTriggerIntervalRunsTimer { get { return ((SelectedBehavior & Behavior.ZeroTriggerIntervalRunsTimer) != Behavior.None); } }

        /// <summary>Returns true if the SelectedBehavior has the IntervalMeasurementTimer option selected</summary>
        public bool IsIntervalMeasurementTimer { get { return (SelectedBehavior & Behavior.IntervalMeasurementTimer) != Behavior.None; } }


        /// <summary>
        /// Method is used to reset the timer to occur after TriggerInterval has elpased from now.  This is a synonym for Start().
        /// </summary>
        public QpcTimer Reset() 
        {
            return Start();
        }

        /// <summary>
        /// Method is used to reset the timer to occur after TriggerInterval has elpased from stated time called now.
        /// This is a synonym for Start(now).
        /// </summary>
        /// <param name="now">Caller provies the time stamp from which the interval is measured.</param>
        public QpcTimer Reset(QpcTimeStamp now)
		{
            return Start(now);
		}

        /// <summary>
        /// Getter returns true if the timer has been started (i.e. !lastTriggerTimeStamp.IsZero).  
        /// Setter starts the timer if assigned to true or stops the timer if assigned to false.
        /// NOTE: use of the setter for this property puts the QpcTimer in the new behavior where 
        /// ElapsedTime is forced to zero when the timer is stopped and where the TriggerInterval of zero will not block the timer from triggering (negative values still do)
        /// </summary>
        public bool Started
        {
            get { return !lastTriggerTimeStamp.IsZero; }

            set 
            {
                if (value) 
                    Start(); 
                else 
                    Stop(); 
            }
		}

        /// <summary>
        /// Method is used to set the TriggerInterval to the given value and then Reset the timer so that it will expire after the given interval.
        /// </summary>
        /// <param name="newTriggerInterval">Caller provided TimeSpan after which the timer should expire</param>
        public QpcTimer Start(TimeSpan newTriggerInterval)
        {
            TriggerInterval = newTriggerInterval;
            return Start();
        }

        /// <summary>
        /// Starts the timer to run from now, using the current TriggerInterval 
        /// </summary>
        public QpcTimer Start()
        {
            return Start(QpcTimeStamp.Now);
        }

        /// <summary>
        /// If the timer is not already Started, this method Starts the timer to run from now, using the current TriggerInterval.
        /// If the timer is already Started, this method has no effect.
        /// </summary>
        public QpcTimer StartIfNeeded()
        {
            if (!Started)
                Start();

            return this;
        }

        /// <summary>
        /// If the timer is not already Started or if the given newTriggerInterval does not match the current TriggerInterval value, this method Starts the timer to run from now, using the current newTriggerInterval.
        /// If the timer is already Started, this method has no effect.
        /// </summary>
        public QpcTimer StartIfNeeded(TimeSpan newTriggerInterval)
        {
            if (!Started || TriggerInterval != newTriggerInterval)
                Start(newTriggerInterval);

            return this;
        }

        /// <summary>
        /// If the timer is not already Stopped, this method Stops the timer.
        /// If the timer is already Stopped, this method has no effect.
        /// </summary>
        public QpcTimer StopIfNeeded()
        {
            if (Started)
                Stop();

            return this;
        }

        /// <summary>
        /// Internal method: starts the timer to trigger after the TriggerInterval has elpased from the given starting timestamp.
        /// </summary>
        private QpcTimer Start(QpcTimeStamp now)
        {
            ElapsedTimeAtLastTrigger = TimeSpan.Zero;

            lastTriggerTimeStamp = now;
            nextTriggerTimeStamp = now + triggerIntervalInSec;

            return this;
        }

        /// <summary>
        /// Stops the timer so that it will not trigger until it has been started again.
        /// </summary>
        public QpcTimer Stop()
        {
            ElapsedTimeAtLastTrigger = ElapsedTime;
            lastTriggerTimeStamp = QpcTimeStamp.Zero;
            return this;
        }

        /// <summary>
        /// Get property returns true if the timer's TriggerInterval has elapsed since the last occurance.  
        /// If timer is configured to AutoReset, the timer will automatically reset to expire after 
        /// the TriggerInterval has elapsed from the most recent occurance.
        /// <para/>This property returns false if the timer is not running (either by being explicitly Started or by having the TriggerInterval be zero with the non-default ZeroTriggerIntervalRunsTimer behavior selected).
        /// non-AutoReset timers will continue to return true once they have triggered until some other action is taken to change, reset or start the timer.
        /// <para/>Internally this is the same as calling GetIsTriggered(QpcTimeStamp.Now);
        /// </summary>
        public bool IsTriggered { get { return GetIsTriggered(QpcTimeStamp.Now); } }

        /// <summary>
        /// Method is used to determin is the timer's TriggerInterval has elapsed based on the given time Now and the recorded time of the last occurance.
        /// If the timer has elapsed and the timer is configured to AutoReset, method will add the TriggerInterval to the last occurance to the timestamp 
        /// of the current occurance (even if now is after that occurance).  Then if this newly advanced timer has already expired then the method Reset's 
        /// the timer to occur at TimerInterval from the given now value.
        /// <para/>This method returns false if the timer is not running (either by being explicitly Started or by having the TriggerInterval be zero with the non-default ZeroTriggerIntervalRunsTimer behavior selected).
        /// non-AutoReset timers will continue to return true once they have triggered until some other action is taken to change, reset or start the timer.
        /// </summary>
        /// <param name="now">Caller provided QpcTimeStamp value that defines when the timer is evaluated against and provides the base time for a Reset style advance when appropriate.</param>
		public bool GetIsTriggered(QpcTimeStamp now)
		{
            bool isTimerRunning, isTimerTriggered;

            InnerGetIsTriggered(now, out isTimerRunning, out isTimerTriggered);

            if (!isTimerRunning)
                return false;

            if (isTimerTriggered && !lastTriggered)
            {
                ElapsedTimeAtLastTrigger = GetElapsedTime(now);
            }

            lastTriggered = isTimerTriggered;

			if (isTimerTriggered && AutoReset)
			{
                // update the lastTriggerTimestamp to match the incoming nextTriggerTimestamp
				lastTriggerTimeStamp = nextTriggerTimeStamp;

                // add the interval to the nextTriggerTimestamp
				nextTriggerTimeStamp.Add(triggerIntervalInSec);

                // if the new nextTriggerTimestamp is already past then reset/restart the timer to expire after the full triggerInterval has elapsed from now.
				if (nextTriggerTimeStamp < now)
					Start(now);
			}

			return isTimerTriggered;
		}

        private void InnerGetIsTriggered(QpcTimeStamp now, out bool isTimerRunning, out bool isTimerTriggered)
        {
            isTimerRunning = (Started && (triggerIntervalInSec > 0.0) || (triggerIntervalInSec == 0.0 && ZeroTriggerIntervalRunsTimer));
            isTimerTriggered = isTimerRunning && (now > nextTriggerTimeStamp) && !IsIntervalMeasurementTimer;
        }

        /// <summary>
        /// Gives  the ElapsedTimeInSeconds since the timer was last started or expired.
        /// <para/>If the IntervalMeasurmentTimer behavior is selected and the timer is running then the use of this property getter will also Reset the timer
        /// </summary>
        public double ElapsedTimeInSeconds { get { return GetElapsedTime(QpcTimeStamp.Now).TotalSeconds; } }

        /// <summary>
        /// Gives the ElapsedTime since the timer was last started or expired as a TimeSpan.
        /// <para/>If the IntervalMeasurmentTimer behavior is selected and the timer is running then the use of this property getter will also Reset the timer
        /// </summary>
        public TimeSpan ElapsedTime { get { return GetElapsedTime(QpcTimeStamp.Now); } }

        /// <summary>
        /// Gives the value of the ElapsedTime property at the time that the timer last transitioned to triggered or was stopped.  This value is set to TimeSpan.Zero when the timer is started.
        /// <para/>If the IntervalMeasurmentTimer behavior is selected and the timer is running then the use of this property getter will also Reset the timer
        /// </summary>
        public TimeSpan ElapsedTimeAtLastTrigger { get; private set; }

        /// <summary>
        /// Gives the Elapsed Time between the given now value and the time that the timer last expired as a TimeSpan value.
        /// <para/>If the IntervalMeasurmentTimer behavior is selected and the timer is running then the use of this method will also Reset the timer
        /// </summary>
        public TimeSpan GetElapsedTime(QpcTimeStamp now)
        {
            bool started = Started;
            TimeSpan result = (started || !ElapsedTimeIsZeroWhenStopped) ? (now - lastTriggerTimeStamp) : TimeSpan.Zero;

            if (IsIntervalMeasurementTimer && started)
                Reset();

            return result;
        }

        /// <summary>
        /// If the timer is running and it is not currently triggered then this method returns the expected remaining time before the timer will next trigger.
        /// Otherwise this method returns TimeSpan.Zero.
        /// </summary>
        public TimeSpan RemainingTime
        {
            get
            {
                QpcTimeStamp now = QpcTimeStamp.Now;
                bool isTimerRunning, isTimerTriggered;
                InnerGetIsTriggered(now, out isTimerRunning, out isTimerTriggered);

                if (isTimerRunning && !isTimerTriggered && now < nextTriggerTimeStamp)
                    return nextTriggerTimeStamp - now;
                else
                    return TimeSpan.Zero;
            }
        }

        #region private fields

        /// <summary>QpcTimeStamp of the last time the timer expired, or zero if the timer has not been started.</summary>
        private QpcTimeStamp lastTriggerTimeStamp;

        /// <summary>QpcTimeStamp of the next time after which the timer will have expired</summary>
        private QpcTimeStamp nextTriggerTimeStamp;

        /// <summary>Boolean used to do delta detect on the triggered value to know when to update the ElapsedTimeAtLastTrigger property.</summary>
        private bool lastTriggered;

        /// <summary>Configured expieration interval in seconds</summary>
        private double triggerIntervalInSec;

        #endregion

        /// <summary>
        /// Debugging and logging helper
        /// </summary>
        public override string ToString()
        {
            bool isTimerRunning, isTimerTriggered;

            InnerGetIsTriggered(QpcTimeStamp.Now, out isTimerRunning, out isTimerTriggered);

            return "QPCTimer {0} {1} {2} Interval:{3:f3} sec, Elapsed:{4:f3} ElapsedAtLastTrigger:{5:f3} {6}".CheckedFormat(
                        Started ? "Started" : "-Started", isTimerRunning ? "Running" : "-Running", isTimerTriggered ? "Triggered" : "-Triggered",
                        TriggerIntervalInSec, ElapsedTimeInSeconds, ElapsedTimeAtLastTrigger.TotalSeconds,
                        SelectedBehavior
                        );
        }
    }

	#endregion

    #region Extension Methods

    public static partial class ExtensionMethods
    {
        public static TimeSpan Min(this TimeSpan a, TimeSpan b)
        {
            return ((a <= b) ? a : b);
        }

        public static TimeSpan Max(this TimeSpan a, TimeSpan b)
        {
            return ((a >= b) ? a : b);
        }

        public static QpcTimeStamp Min(this QpcTimeStamp a, QpcTimeStamp b)
        {
            return ((a <= b) ? a : b);
        }

        public static QpcTimeStamp Max(this QpcTimeStamp a, QpcTimeStamp b)
        {
            return ((a >= b) ? a : b);
        }
    }

    #endregion
}

//-------------------------------------------------------------------
