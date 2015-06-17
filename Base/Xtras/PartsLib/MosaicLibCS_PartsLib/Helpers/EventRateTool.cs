//-------------------------------------------------------------------
/*! @file EventRateTool.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved.
 * Copyright (c) 2015 Mosaic Systems Inc.  All rights reserved.
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

using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Helpers
{
    /// <summary>
    /// This is a simple helper/utility class that can be used to calculate a recent average of the rate that some event happens.
    /// </summary>
    public class EventRateTool : INotifyable
    {
        /// <summary>Constructor</summary>
        public EventRateTool()
        {
            oneOverLength = 1.0 / rateQueue.Count;
        }

        /// <summary>Notify method calls NoteEventHappened</summary>
        void INotifyable.Notify()
        {
            NoteEventHappened();
        }

        /// <summary>Increments the current interval's eventCount and then checks if the current interval is complete.  If so it updates the average rate.</summary>
        public void NoteEventHappened()
        {
            eventCount++;
            ServiceAveraging();
        }

        /// <summary>Public property returns true if the a new AvgRate value has been generated since the last time that the AvgRate property was read.</summary>
        public bool HasNewAvgRate { get { ServiceAveraging(); return hasNewAvgRate; } }

        /// <summary>Services the averaging calculation and returns the most recently produced average rate.</summary>
        public double AvgRate { get { ServiceAveraging(); hasNewAvgRate = false; return avgRate; } }

        private bool ServiceAveraging()
        {
            if (!rateIntervalTimer.IsTriggered)
                return false;

            double elapsedTimeInSeconds = rateIntervalTimer.ElapsedTimeAtLastTrigger.TotalSeconds;
            double nextRate = ((elapsedTimeInSeconds > 0.0) ? (eventCount / elapsedTimeInSeconds) : 0.0);

            avgRate += (nextRate - rateQueue[0]) * oneOverLength;
            rateQueue.RemoveAt(0);
            rateQueue.Add(nextRate);

            eventCount = 0;

            hasNewAvgRate = true;

            return true;
        }

        private double avgRate = 0.0;
        private bool hasNewAvgRate = false;

        private int eventCount = 0;
        private QpcTimer rateIntervalTimer = new QpcTimer() { AutoReset = true, TriggerIntervalInSec = 0.5, Started = true };
        private List<double> rateQueue = new List<double>() { 0.0, 0.0, 0.0, 0.0, 0.0 };        // rate is average over 5 * 0.5 seconds
        private double oneOverLength;
    }
}