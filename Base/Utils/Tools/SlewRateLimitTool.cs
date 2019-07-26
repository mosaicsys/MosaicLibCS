//-------------------------------------------------------------------
/*! @file SlewRateLimitTool.cs
 *  @brief Provides a simple model of a two position actuator
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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

using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.Utils.Tools
{
    /// <summary>
    /// This class is a helper class for implementing slew rate limited value tracking.
    /// The client generally sets up an instance of this class with: MinValue, MaxValue, MaxRatePerSec and an initial TargetValue and Value.
    /// Then the client updates TargetValue when desired and calls a variant of Service to incrementally adjust Value until it reaches TargetValue or until it reaches the value boundaries defined by MinValue and MaxValue.
    /// </summary>
    public class SlewRateLimitTool : IServiceable
    {
        /// <summary>Constructor</summary>
        public SlewRateLimitTool(double minValue = double.MinValue, double maxValue = double.MaxValue, double maxRatePerSec = double.MaxValue, double initialValue = 0.0, double ? initialTarget = null, double ? maxRisingRatePerSec = null, double ? maxFallingRatePerSec = null)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            MaxRisingRatePerSec = maxRisingRatePerSec ?? maxRatePerSec;
            MaxFallingRatePerSec = maxFallingRatePerSec ?? maxRatePerSec;

            Value = initialValue;
            TargetValue = initialTarget ?? Value;

            LastServiceQpcTimeStamp = QpcTimeStamp.Now;
        }

        /// <summary>Defines the maximum value that Value can be set to</summary>
        public double MinValue { get; set; }

        /// <summary>Defines the minimum value that Value can be set to</summary>
        public double MaxValue { get; set; }

        /// <summary>This setter may be used to set both the MaxUpwardRatePerSec and the MaxDownwardRatePerSec values.  getter returns the MaxRatePerSec if both the rising and falling rates are equal otherwise it returns double.NaN.</summary>
        public double MaxRatePerSec { get { return ((MaxRisingRatePerSec == MaxFallingRatePerSec) ? MaxRisingRatePerSec : double.NaN); } set { MaxRisingRatePerSec = MaxFallingRatePerSec = value; } }

        /// <summary>Defines the maximum rate of change of Value in units per second for rising values.  If this value is clipped to be no less than zero.</summary>
        public double MaxRisingRatePerSec { get { return _maxRisingRatePerSec; } set { _maxRisingRatePerSec = Math.Max(0.0, value); } }
        private double _maxRisingRatePerSec;

        /// <summary>Defines the maximum downward rate of change of Value in units per second for falling values.  If this value is clipped to be no less than zero.</summary>
        public double MaxFallingRatePerSec { get { return _maxFallingRatePerSec; } set { _maxFallingRatePerSec = Math.Max(0.0, value); } }
        private double _maxFallingRatePerSec;

        /// <summary>Gives the last slew limited value (from Service) or the last given value (from Reset or by direct use of the setter).  The setter clips any given value to be constrained to be between MinValue and MaxValue using the Clip Extension Method</summary>
        public double Value { get { return _value; } set { _value = value.Clip(MinValue, MaxValue); } }
        private double _value;

        /// <summary>Client sets this to the value that Value should eventually reach.</summary>
        public double TargetValue { get; set; }

        /// <summary>Gives the QpcTimeStamp last used in the QpcTimeStamp Service method variant.  This is used for interval calculation.</summary>
        public QpcTimeStamp LastServiceQpcTimeStamp { get; set; }

        /// <summary>
        /// Resets the Value to the given <paramref name="value"/> 
        /// and optionally resets the TargetValue to the given non-null <paramref name="targetValue"/> or to <paramref name="value"/> if <paramref name="replaceNullTargetWithValue"/> is true.
        /// Optionally sets LastServiceQpcTimeStamp to QpcTimeStamp.Now if <paramref name="setLastServiceQpcTimeStampToNow"/> is true.
        /// </summary>
        public SlewRateLimitTool Reset(double value, double? targetValue = null, bool replaceNullTargetWithValue = true, bool setLastServiceQpcTimeStampToNow = true)
        {
            Value = value;

            if (targetValue != null)
                TargetValue = targetValue ?? 0.0;
            else if (replaceNullTargetWithValue)
                TargetValue = Value;

            if (setLastServiceQpcTimeStampToNow)
                LastServiceQpcTimeStamp = QpcTimeStamp.Now;

            return this;
        }

        /// <summary>
        /// Basic service method.  
        /// Uses the given <paramref name="qpc"/> value (after mapping Zero to Now) and the LastServiceQpcTimeStamp to measure the elapsed time since the last Service call
        /// and then passes the resulting elapsed time value to the TimeSpan Service method variant which does the actual slew limited update.
        /// <para/>Returns 1 if Value was changed or 0 if it was not.
        /// </summary>
        public int Service(QpcTimeStamp qpc = default(QpcTimeStamp))
        {
            if (qpc.IsZero)
                qpc = QpcTimeStamp.Now;

            TimeSpan elapsed = qpc - LastServiceQpcTimeStamp;

            LastServiceQpcTimeStamp = qpc;

            return Service(elapsed);
        }

        /// <summary>
        /// Caculates the maximum incremental change to Value that can be applied based on the current MaxRisingRatePerSec or MaxFallingRatePerSec value and the given <paramref name="elapsed"/> time and then 
        /// incrementally adjusts the Value to track the current TargetValue while only changing Value be no more than the calculated maximum increment until the TargetValue is reached,
        /// or the Value Clipping rules are triggered.
        /// <para/>If the appropriate MaxYYYRatePerSec is infinity then the Service method either directly jumps Value to match TargetValue (with clipping) or does nothing if the given <paramref name="elapsed"/> time IsZero.
        /// <para/>Returns 1 if Value was changed or 0 if it was not.
        /// </summary>
        public int Service(TimeSpan elapsed)
        {
            bool elapsedIsZero = elapsed.IsZero();

            double entryValue = Value;
            double entryTargetValue = TargetValue;

            int compareTargetAndValue = entryTargetValue.CompareTo(entryValue);

            if (compareTargetAndValue > 0)
            {
                double maxValueIncrement = (!elapsedIsZero ? MaxRisingRatePerSec * elapsed.TotalSeconds : 0.0);
                Value = Math.Min(entryValue + maxValueIncrement, entryTargetValue);
            }
            else if (compareTargetAndValue < 0)
            {
                double maxValueIncrement = (!elapsedIsZero ? MaxFallingRatePerSec * elapsed.TotalSeconds : 0.0);
                Value = Math.Max(entryValue - maxValueIncrement, entryTargetValue);
            }

            return (entryValue != Value).MapToInt();
        }

		/// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            if (MaxRisingRatePerSec == MaxFallingRatePerSec)
                return "Target:{0:g3} Value:{1:g3} Rate:{2:g3}".CheckedFormat(TargetValue, Value, MaxRatePerSec);
            else
                return "Target:{0:g3} Value:{1:g3} Rate:{2:g3}/{3:g3}".CheckedFormat(TargetValue, Value, MaxRisingRatePerSec, MaxFallingRatePerSec);
        }
    }
}

//-------------------------------------------------------------------
