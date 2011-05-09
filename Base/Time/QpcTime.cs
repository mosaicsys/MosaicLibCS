//-------------------------------------------------------------------
/*! @file QpcTime.cs
 *  @brief This file defines interfaces and classes that help give the client access to and use of the windows platform performance counter related timer functions.
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

namespace MosaicLib.Time
{
	using System;
	using System.Runtime.InteropServices;

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

		private static QpcTimeStamp zeroTime = new QpcTimeStamp(0.0);		// default constructor sets its qpcTime to zero.

		/// <summary>Constructor for use with an explicitly provied double qpc time stamp value.</summary>
		/// <param name="time">the double QPC time value to retain as the time stamp</param>
		public QpcTimeStamp(double time) { qpcTime = time; }

		/// <summary>Copy Constructors</summary>
		/// <param name="rhs">The right hand side (rhs) to copy the timestamp from.</param>
		public QpcTimeStamp(QpcTimeStamp rhs) { qpcTime = rhs.qpcTime; }

		/// <summary>Static method to return the constant Zero timestamp value.</summary>
		public static QpcTimeStamp Zero { get { return zeroTime; } }

		/// <summary>Static method to return the current QPC counter value in a new QPCTimeStamp</summary>
		public static QpcTimeStamp Now { get { return new QpcTimeStamp(Qpc.TimeNow); } }

		/// <summary>Returns the stored double qpc timestamp value.</summary>
		public double Time { get { return qpcTime; } set { qpcTime = value; } }

		/// <summary>Returns the TimeSpan between Now and the stored qpc timestamp.</summary>
		public TimeSpan Age { get { return (Now - this); } }

		/// <summary>Sets the stored qpc timestamp to Now.</summary>
		public void SetToNow() { Time = Qpc.TimeNow; }

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
		public static TimeSpan operator -(QpcTimeStamp lhs, QpcTimeStamp rhs) { return TimeSpan.FromSeconds(lhs.Time - rhs.Time); }

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

		/// <summary>returns the </summary>
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
        public QpcTimer(TimeSpan triggerInterval, bool autoReset) 
            : this(triggerInterval.TotalSeconds, autoReset) 
        { }

        /// <summary>Defines timer based on given interval and autoReset flag</summary>
        /// <param name="triggerInervalInSec">double value that defines the timer interval in units of seconds.</param>
        /// <param name="autoReset">boolean value that determines if the timer resets itself on reported experation of the timer or if it requires the client to explicitly restart it using the Reset method.</param>
        public QpcTimer(double triggerIntervalInSec, bool autoReset)
            : this()
		{
			lastTriggerTimestamp = QpcTimeStamp.Zero;
			TriggerIntervalInSec = triggerIntervalInSec;
			this.autoReset = autoReset;
        }

        #endregion

        /// <summary>
        /// Property can be used to get or set the TriggerInterval as a double value in units of seconds.  Setting this interval implicitly resets the timer to expire after the interval has elapsed since the last occurance.
        /// </summary>
        public double TriggerIntervalInSec { get { return triggerIntervalInSec; } set { triggerIntervalInSec = value; nextTriggerTimestamp = lastTriggerTimestamp + value; } }
        /// <summary>
        /// Property can be used to get or set the TriggerInterval as a TimeSpan value.  Setting this interval implicitly resets the timer to expire after the interval has elpased since the since the last occurance. 
        /// </summary>
        public TimeSpan TriggerInterval { get { return TimeSpan.FromSeconds(triggerIntervalInSec); } set { triggerIntervalInSec = value.TotalSeconds; } }

        /// <summary>
        /// Method is used to reset the timer to occur after TriggerInterval has elpased from now.
        /// </summary>
		public void Reset() { Reset(QpcTimeStamp.Now); }

        /// <summary>
        /// Method is used to reset the timer to occur after TriggerInterval has elpased from state time called now.
        /// </summary>
        /// <param name="now">Caller provies the time stamp from which the interval is measured.</param>
		public void Reset(QpcTimeStamp now)
		{
			nextTriggerTimestamp = lastTriggerTimestamp = now;
			nextTriggerTimestamp.Add(triggerIntervalInSec);
		}

        /// <summary>
        /// Get property returns true if the timer's TriggerInterval has elapsed since the last occurance.  Uses GetIsTriggered method internally.  If timer is configured to AutoReset, the timer will automatically reset to expire after after the TriggerInterval has elapsed from the most recent occurance.
        /// </summary>
        public bool IsTriggered { get { return GetIsTriggered(QpcTimeStamp.Now); } }

        /// <summary>
        /// Method is used to determin is the timer's TriggerInterval has elapsed based on the given time Now and the recorded time of the last occurance.
        /// If the timer has elapsed and the timer is configured to AutoReset, method will last occurance to the timestamp of the current occurance (even if now is after that occurance).  Then if this newly advanced timer has already expired then the method Reset's the timer to occur at TimerInterval from the given now value.
        /// </summary>
        /// <param name="now">Caller provided QpcTimeStamp value that defines when the timer is evaluated against and provides the base time for a Reset style advance when appropriate.</param>
        /// <remarks>
        /// Method always returns false if the timer has not configured either during construction or by setting the TriggerInterval.
        /// </remarks>
		public bool GetIsTriggered(QpcTimeStamp now)
		{
			if (lastTriggerTimestamp.IsZero || triggerIntervalInSec <= 0.0)
				return false;

			bool triggered = (now > nextTriggerTimestamp);

			if (triggered && autoReset)
			{
				lastTriggerTimestamp = nextTriggerTimestamp;
				nextTriggerTimestamp.Add(triggerIntervalInSec);

				if (nextTriggerTimestamp < now)
					Reset(now);
			}

			return triggered;
		}

        /// <summary>
        /// Gives  the ElapsedTimeInSeconds since the timer last expired.
        /// </summary>
        public double ElapsedTimeInSeconds { get { return GetElapsedTime(QpcTimeStamp.Now).TotalSeconds; } }

        /// <summary>
        /// Gives the ElapsedTime since the timer last expired as a TimeSpan.
        /// </summary>
        public TimeSpan ElapsedTime { get { return GetElapsedTime(QpcTimeStamp.Now); } }

        /// <summary>
        /// Gives the Elapsed Time between the given now value and the time that the timer last expired as a TimeSpan value.
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        public TimeSpan GetElapsedTime(QpcTimeStamp now)
        {
            return (now - lastTriggerTimestamp);
        }

        #region private fields

        /// <summary>QpcTimeStamp of the last time the timer expired</summary>
        private QpcTimeStamp lastTriggerTimestamp;

        /// <summary>QpcTimeStamp of the next time after which the timer will have expired</summary>
        private QpcTimeStamp nextTriggerTimestamp;

        /// <summary>Configured expieration interval in seconds</summary>
        private double triggerIntervalInSec;

        /// <summary></summary>
        private bool autoReset;

        #endregion

    }

	#endregion
}

//-------------------------------------------------------------------
