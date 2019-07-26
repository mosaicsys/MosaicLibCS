//-------------------------------------------------------------------
/*! @file EventRateTool.cs
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
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.Utils.Tools
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

        /// <summary>Increments the current interval's eventCount using the given <paramref name="countIncrement"/> and then checks if the current interval is complete.  If so it updates the average rate.</summary>
        public void NoteEventHappened(double countIncrement = 1.0)
        {
            eventCount += countIncrement;
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

            eventCount = 0.0;

            hasNewAvgRate = true;

            return true;
        }

        private double avgRate = 0.0;
        private bool hasNewAvgRate = false;

        private double eventCount = 0;
        private QpcTimer rateIntervalTimer = new QpcTimer() { AutoReset = true, TriggerIntervalInSec = 0.5, Started = true };
        private List<double> rateQueue = new List<double>() { 0.0, 0.0, 0.0, 0.0, 0.0 };        // rate is average over 5 * 0.5 seconds
        private double oneOverLength;
    }

    /// <summary>
    /// This is an extended version of the EventRateTool found above which is used to accumlate values and to generate a moving average of the accumulated values
    /// sampled at a given update interval and using a given history length for averaging.  The client is expected to call RecordValues at some rate (may be variable) to record (assumulate/add) given values
    /// at whatever rate and event structure the client wants.  The tool uses a cadencing interval timer to determine when to "sample" these accumulated values.  These sampled accumulated values
    /// are then moved into the list of historical values and an average is calculated from this list of historical values.  
    /// Each time this process takes place, the HasNewAvg property becomes true and remains true until the client calls the Avg getter which returns the last calculated average and clears the HasNewAvg property.
    /// The HasNewAvg getter indirectly services the averaging tool so the client can simply poll the HasNewAvg property and then obtain the new Avg values when it returns true to drive the cadenced part of the
    /// moving average calculation.  Clearly if the client never passes non-zero values into the RecordValues method, the resulting Avg values will end up being zero (assuming that the client provided Add and Multiple methods have the expected behavior).
    /// <para/>The given <typeparamref name="TCustomValuesType"/> type must implement the IRequiredValuesOperations (Add, Multiply, Copy) interface and must support a default constructor.  Both class and struct based types are supported here.
    /// </summary>
    public class MovingAverageTool<TCustomValuesType>
        where TCustomValuesType : IRequiredMovingAverageValuesOperations<TCustomValuesType>, new()
    {
        /// <summary>Constructor</summary>
        public MovingAverageTool(TimeSpan? nominalUpdateInterval = null, int maxAveragingHistoryLength = 5, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            rateIntervalTimer = new QpcTimer()
            {
                TriggerInterval = nominalUpdateInterval ?? (0.5).FromSeconds(),
                SelectedBehavior = QpcTimer.Behavior.NewAutoReset,
            };

            this.maxAveragingHistoryLength = maxAveragingHistoryLength.Clip(1, 1000);
            historyList = new List<HistoryItem>(maxAveragingHistoryLength);

            Reset(qpcTimeStamp, clearTotalizer: true);

            AutoService = true;
        }

        /// <summary>
        /// When this is true (the default) use of the HasNewAvg property getter and the RecordValues method will implicitly Service the tool.  
        /// Set this property to false to disable this default behavior, in which case the client is responsible for calling Service explicitly in order for the tool to operate normally.
        /// </summary>
        public bool AutoService { get; set; }

        /// <summary>
        /// This method is used to record (accumulate) a new set of values.  Use of this method also services the moving average calculation.
        /// The 
        /// </summary>
        public MovingAverageTool<TCustomValuesType> RecordValues(TCustomValuesType values, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            accumulator = accumulator.Add(values);
            accumulatedValueCount++;

            totalAccumulator = totalAccumulator.Add(values);
            totalAccumulatorCount++;
            
            return AutoService ? Service(qpcTimeStamp) : this;
        }

        public MovingAverageTool<TCustomValuesType> Reset(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool clearTotalizer = true)
        {
            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

            accumulator = new TCustomValuesType();
            accumulatedValueCount = 0;
            accumulatorStartTime = qpcTimeStamp;

            if (clearTotalizer)
            {
                totalAccumulator = new TCustomValuesType();
                totalAccumulatorCount = 0;
            }

            historyList.Clear();
            rateIntervalTimer.Start(qpcTimeStamp);
            hasNewSample = false;

            return this;
        }

        public int HistorySampleCount { get { return historyList.Count; } }

        public int SampleSeqNum { get { return _sampleSeqNum; } set { _sampleSeqNum = value; } }
        private int _sampleSeqNum;

        public TCustomValuesType Totalizer { get { return totalAccumulator.MakeCopyOfThis(); } set { totalAccumulator = value.MakeCopyOfThis(); totalAccumulatorCount = 0; } }

        public bool HasNewAvg { get { if (AutoService) Service(QpcTimeStamp.Now); return hasNewSample; } set { hasNewSample = value; } }

        public TCustomValuesType Avg { get { return GetAvg(); } }

        public TCustomValuesType GetAvg(TimeSpan nominalAcceptanceAge, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            return GetAvg(qpcTimeStamp.MapDefaultToNow() - nominalAcceptanceAge);
        }

        public TCustomValuesType GetAvg(QpcTimeStamp nominalAcceptanceTimeStamp)
        {
            hasNewSample = false;

            int gotCount = 0;

            var avgValues = new TCustomValuesType();

            foreach (var item in historyList.Where(item => item.sampleEndTimeStamp >= nominalAcceptanceTimeStamp))
            {
                avgValues = avgValues.Add(item.accumulatedValues);
                gotCount++;
            }

            avgValues = avgValues.ComputeAverage(gotCount);

            return avgValues;   
        }

        public TCustomValuesType GetAvg(int numHistorySamplesToAverage = 0)
        {
            hasNewSample = false; 

            numHistorySamplesToAverage = Math.Min(numHistorySamplesToAverage.MapDefaultTo(maxAveragingHistoryLength), historyList.Count);

            var avgValues = new TCustomValuesType();

            foreach (var item in historyList.Take(numHistorySamplesToAverage))
                avgValues = avgValues.Add(item.accumulatedValues);

            avgValues = avgValues.ComputeAverage(numHistorySamplesToAverage);

            return avgValues;
        }

        public MovingAverageTool<TCustomValuesType> Service(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

            if (!rateIntervalTimer.GetIsTriggered(qpcTimeStamp))
                return this;

            return AddRecordedValuesToHistory(qpcTimeStamp);
        }

        public MovingAverageTool<TCustomValuesType> AddRecordedValuesToHistory(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

            HistoryItem queueItem = new HistoryItem()
            {
                sampleStartTimeStamp = accumulatorStartTime,
                sampleEndTimeStamp = qpcTimeStamp,
                accumulatedValues = accumulator,
            };

            int historyListCount = historyList.Count;
            if (historyListCount >= maxAveragingHistoryLength && historyListCount > 0)
            {
                historyList.RemoveAt(historyListCount - 1);
                historyList.Insert(0, queueItem);
            }
            else
            {
                historyList.Insert(0, queueItem);
            }

            accumulator = new TCustomValuesType();
            accumulatedValueCount = 0;
            accumulatorStartTime = qpcTimeStamp;

            hasNewSample = true;

            _sampleSeqNum = _sampleSeqNum.IncrementSkipZero();

            return this;
        }

        private QpcTimer rateIntervalTimer;
        private int maxAveragingHistoryLength;
        private List<HistoryItem> historyList;

        struct HistoryItem
        {
            public QpcTimeStamp sampleStartTimeStamp, sampleEndTimeStamp;
            public TCustomValuesType accumulatedValues;
        }

        private bool hasNewSample;

        private TCustomValuesType accumulator;
        private int accumulatedValueCount;
        private QpcTimeStamp accumulatorStartTime;

        private TCustomValuesType totalAccumulator;
        private int totalAccumulatorCount;
    }

    public interface IRequiredMovingAverageValuesOperations<TCustomValuesType> : ICopyable<TCustomValuesType>
    {
        /// <summary>
        /// The moving average tool calls this method for two purposes:  
        /// First it calls it for adding client provided values into the sample period accumulator.  
        /// Second it calls this method to add one or more historical sample period accumulated values together prior to computing the average value.
        /// </summary>
        TCustomValuesType Add(TCustomValuesType other);

        /// <summary>
        /// The moving average tool calls this method after adding the sample period accumlated values for the desired portion of the recent saved history of such values.
        /// The passed <paramref name="sampleCount"/> gives the count of the number of sample period accumulated values that were added together and on which this call is being made to convert the result into an average.
        /// </summary>
        TCustomValuesType ComputeAverage(int sampleCount);
    }
}