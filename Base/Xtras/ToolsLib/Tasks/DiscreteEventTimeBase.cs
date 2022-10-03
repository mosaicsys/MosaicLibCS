//-------------------------------------------------------------------
/*! @file DiscreteEventTimeBase.cs
 *  @brief Defines a set of types that are used to support creation of a discrete event time base, such as one that is often used for descrete event simulation.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2022 Mosaic Systems Inc.
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace Mosaic.ToolsLib.Tasks.DiscreteEventTimeBase
{
    /// <summary>
    /// This interface defines the public API for the <see cref="DiscreteEventTimeBase"/> class (and others like it).
    /// This API is intended to support a set of capabilites that are often used to create discrete event simulation systems.
    /// This interface is thread-safe and supports use in multi-threaded systems.
    /// </summary>
    public interface IDiscreteEventTimeBase
    {
        /// <summary>
        /// This value defines the minimum amount that the <see cref="CurrentSyntheticElapsedTime"/> must advance per call to the <see cref="Service(bool)"/> method when there are no waiting tasks.
        /// </summary>
        TimeSpan MinimumIdleSyntheticTimeIncrement { get; }

        /// <summary>
        /// When this value is non-zero it determines the maximum amount that the <see cref="CurrentSyntheticElapsedTime"/> can advance per call to the <see cref="Service(bool)"/> method when there are waiting tasks.
        /// </summary>
        TimeSpan MaximumNonIdleSyntheticTimeIncrement { get; }

        /// <summary>
        /// If <paramref name="advanceTime"/> is passed as true (the default) then this method will advance the <see cref="CurrentSyntheticElapsedTime"/> to the next value, 
        /// either based on the <see cref="MaximumNonIdleSyntheticTimeIncrement"/> or on the next Task completion time stamp.
        /// In either case this method then services completion of Tasks generated from this interface and for which the <see cref="CurrentSyntheticElapsedTime"/> is reached or passed their implicit completion time.
        /// In addition this method checks to see if any pending Task Wait item was given a <see cref="CancellationToken"/> that now indicates that it needs to be cancelled, in which case the wait is completed immediately with a cancelled exception.
        /// </summary>
        /// <remarks>
        /// Note: In most usage patterns for a discrete time synthesizer, this method is only called from a single thread.
        /// </remarks>
        void Service(bool advanceTime = true);

        /// <summary>
        /// This property returns the tuple pair of the UTC DateTime and the QpcTimeStamp that were given and/or captured when the instance was constructed.
        /// </summary>
        (DateTime utcDT, QpcTimeStamp qpcTS) BaseTime { get; }

        /// <summary>
        /// This gives the total amount of accumulated synthetically "elapsed" time.
        /// This property is updated by the <see cref="Service(bool)"/> method each time it is told to advance the current time.
        /// </summary>
        TimeSpan CurrentSyntheticElapsedTime { get; }

        /// <summary>This returns <see cref="BaseTime"/> + <see cref="CurrentSyntheticElapsedTime"/>.</summary>
        (DateTime utcDT, QpcTimeStamp qpcTS) CurrentSyntheticTime { get; }

        /// <summary>Returns the UTC DateTime from <see cref="CurrentSyntheticTime"/>.</summary>
        DateTime CurrentSyntheticUtcDateTime { get; }

        /// <summary>Returns the QpcTimeStamp from <see cref="CurrentSyntheticTime"/>.</summary>
        QpcTimeStamp CurrentSyntheticQpcTimeStamp { get; }

        /// <summary>
        /// This method returns a <see cref="Task"/> that will complete after the given <paramref name="incrementalWaitTimeSpan"/> has elapsed from <see cref="CurrentSyntheticElapsedTime"/> or the optional <paramref name="cancellationToken"/> indicates it should be cancelled.
        /// The resulting <see cref="Task"/>'s value carries the value of <see cref="CurrentSyntheticElapsedTime"/> at the point it was completed.
        /// </summary>
        Task<TimeSpan> WaitAsync(TimeSpan incrementalWaitTimeSpan, CancellationToken cancellationToken = default);

        /// <summary>
        /// This method returns a <see cref="Task"/> that will complete after the <see cref="CurrentSyntheticUtcDateTime"/> passes the given <paramref name="waitUntilAfterDateTime"/> or the optional <paramref name="cancellationToken"/> indicates it should be cancelled.
        /// The resulting <see cref="Task"/>'s value carries the value of <see cref="CurrentSyntheticUtcDateTime"/> at the point it was completed.
        /// </summary>
        Task<DateTime> WaitUntilAsync(DateTime waitUntilAfterDateTime, CancellationToken cancellationToken = default);

        /// <summary>
        /// This method returns a <see cref="Task"/> that will complete after the <see cref="CurrentSyntheticQpcTimeStamp"/> passes the given <paramref name="waitUntilAfterQpcTimestamp"/> or the optional <paramref name="cancellationToken"/> indicates it should be cancelled.
        /// The resulting <see cref="Task"/>'s value carries the value of <see cref="CurrentSyntheticQpcTimeStamp"/> at the point it was completed.
        /// </summary>
        Task<QpcTimeStamp> WaitUntilAsync(QpcTimeStamp waitUntilAfterQpcTimestamp, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// This is the primary implementation class for the <see cref="IDiscreteEventTimeBase"/> interface.
    /// This class (and the interface it is used with) support the concept of generating a discrete, event centric, time base
    /// where the time intervals in the generated time base are adaptively determined to generally optimize the individual time step size
    /// to minimize the number of Service calls (and thus individual time adavance steps) that are required to allow a specific set of requested future delays to be triggered.
    /// In this class the methods that request events to be triggered in the future are implemented in the form of a Task factory where the resulting <see cref="Task"/> will be marked as completed
    /// at the indicated time, or delay from now.  This architectureal choice is intended to support the use of the "await" on these resulting tasks and/or the use of other <see cref="Task"/> specific
    /// constructs such as continuations, etc.
    /// </summary>
    /// <remarks>
    /// Please note that this class is intended for use in both general <see cref="Task"/> cases and in cases where the <see cref="Tasks"/> belong to a <see cref="Tasks.ManualTaskScheduler"/> which may, or may not,
    /// make use of the same Service thread as is used to service a corresonding <see cref="DiscreteEventTimeBase"/> instance.
    /// </remarks>
    public class DiscreteEventTimeBase : IDiscreteEventTimeBase
    {
        public DiscreteEventTimeBase(TimeSpan? maximumNonIdleSyntheticTimeIncrement = default, TimeSpan? minimumIdleSyntheticTimeIncrement = default, DateTime? initialDateTime = default, QpcTimeStamp? initialQpcTimeStamp = default)
        {
            MinimumIdleSyntheticTimeIncrement = (minimumIdleSyntheticTimeIncrement ?? default).Max(MinimumMinimumSyntheticIdleTimeIncrement);
            MaximumNonIdleSyntheticTimeIncrement = maximumNonIdleSyntheticTimeIncrement ?? default;

            startedUtcDateTime = (initialDateTime?.ToUniversalTime() ?? DateTime.UtcNow);
            startedQpcTimeStamp = (initialQpcTimeStamp ?? QpcTimeStamp.Now);

            currentSyntheticElapsedTime = TimeSpan.Zero;
            currentSyntheticUtcDateTime = startedUtcDateTime;
            currentSyntheticQpcTimeStamp = startedQpcTimeStamp;
        }

        /// <inheritdoc/>
        public TimeSpan MinimumIdleSyntheticTimeIncrement { get; private set; }

        /// <inheritdoc/>
        public TimeSpan MaximumNonIdleSyntheticTimeIncrement { get; private set; }

        private static readonly TimeSpan MinimumMinimumSyntheticIdleTimeIncrement = (0.000001).FromSeconds();

        /// <inheritdoc/>
        public (DateTime utcDT, QpcTimeStamp qpcTS) BaseTime => (startedUtcDateTime, startedQpcTimeStamp);

        private readonly DateTime startedUtcDateTime;
        private readonly QpcTimeStamp startedQpcTimeStamp;

        /// <inheritdoc/>
        public TimeSpan CurrentSyntheticElapsedTime => currentSyntheticElapsedTime;
        private TimeSpan currentSyntheticElapsedTime;

        /// <inheritdoc/>
        public (DateTime utcDT, QpcTimeStamp qpcTS) CurrentSyntheticTime => (currentSyntheticUtcDateTime, currentSyntheticQpcTimeStamp);
        private DateTime currentSyntheticUtcDateTime;
        private QpcTimeStamp currentSyntheticQpcTimeStamp;

        /// <inheritdoc/>
        public DateTime CurrentSyntheticUtcDateTime => currentSyntheticUtcDateTime;

        /// <inheritdoc/>
        public QpcTimeStamp CurrentSyntheticQpcTimeStamp => currentSyntheticQpcTimeStamp;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Service(bool advanceTime = true)
        {
            if (advanceTime)
            {
                var firstWaitTaskItem = waitItemSortedList.FirstOrDefault().Key;

                TimeSpan timeDelta;

                if (firstWaitTaskItem != null)
                {
                    timeDelta = (firstWaitTaskItem.WaitUntilAfterSyntheticElapsedTime - CurrentSyntheticElapsedTime).Max(TimeSpan.Zero);

                    if (!MaximumNonIdleSyntheticTimeIncrement.IsZero() && timeDelta > MaximumNonIdleSyntheticTimeIncrement)
                    {
                        timeDelta = MaximumNonIdleSyntheticTimeIncrement;
                    }
                }
                else
                {
                    timeDelta = MinimumIdleSyntheticTimeIncrement;
                }

                currentSyntheticElapsedTime += timeDelta;
                currentSyntheticQpcTimeStamp = startedQpcTimeStamp + currentSyntheticElapsedTime;
                currentSyntheticUtcDateTime = startedUtcDateTime + currentSyntheticElapsedTime;
            }

            for (; ; )
            {
                var waitTaskItem = waitItemSortedList.Values.FirstOrDefault();

                if (waitTaskItem == null || waitTaskItem.WaitUntilAfterSyntheticElapsedTime > currentSyntheticElapsedTime)
                    break;

                waitItemSortedList.RemoveAt(0);

                if (waitTaskItem.CancellationToken.CanBeCanceled)
                    cancelationCheckWaitItemListWithCachedArray.Remove(waitTaskItem);

                if (waitTaskItem.TCS_TimeSpan != null)
                    waitTaskItem.TCS_TimeSpan.SetResult(currentSyntheticElapsedTime);
                if (waitTaskItem.TCS_UDTDateTime != null)
                    waitTaskItem.TCS_UDTDateTime.SetResult(currentSyntheticUtcDateTime);
                if (waitTaskItem.TCS_QpcTimeStamp != null)
                    waitTaskItem.TCS_QpcTimeStamp.SetResult(currentSyntheticQpcTimeStamp);

                waitItemTrackerFreeList.Release(ref waitTaskItem);
            }

            var cancelationCheckArray = cancelationCheckWaitItemListWithCachedArray.Array;
            var cancelationCheckArrayLength = cancelationCheckArray.Length;

            for (int index = 0; index < cancelationCheckArrayLength; index++)
            {
                var waitTaskItem = cancelationCheckArray[index];
                if (waitTaskItem.CancellationToken.IsCancellationRequested)
                {
                    waitItemSortedList.Remove(waitTaskItem);
                    cancelationCheckWaitItemListWithCachedArray.RemoveAt(index);

                    if (waitTaskItem.TCS_TimeSpan != null)
                        waitTaskItem.TCS_TimeSpan.TrySetCanceled(waitTaskItem.CancellationToken);
                    if (waitTaskItem.TCS_UDTDateTime != null)
                        waitTaskItem.TCS_UDTDateTime.TrySetCanceled(waitTaskItem.CancellationToken);
                    if (waitTaskItem.TCS_QpcTimeStamp != null)
                        waitTaskItem.TCS_QpcTimeStamp.TrySetCanceled(waitTaskItem.CancellationToken);

                    waitItemTrackerFreeList.Release(ref waitTaskItem);
                }
            }
        }

        /// <inheritdoc/>
        public Task<TimeSpan> WaitAsync(TimeSpan incrementalWaitTimeSpan, CancellationToken cancellationToken = default)
        {
            lock (mutex)
            {
                var waitItemTracker = waitItemTrackerFreeList.Get();

                waitItemTracker.WaitUntilAfterSyntheticElapsedTime = CurrentSyntheticElapsedTime + incrementalWaitTimeSpan;
                waitItemTracker.WaitSeqNum = ++waitItemSeqNumGen;
                waitItemTracker.CancellationToken = cancellationToken;
                waitItemTracker.TCS_TimeSpan = new TaskCompletionSource<TimeSpan>();

                waitItemSortedList.Add(waitItemTracker, waitItemTracker);
                if (cancellationToken.CanBeCanceled)
                    cancelationCheckWaitItemListWithCachedArray.Add(waitItemTracker);

                return waitItemTracker.TCS_TimeSpan.Task;
            }
        }

        /// <inheritdoc/>
        public Task<DateTime> WaitUntilAsync(DateTime waitUntilAfterDateTime, CancellationToken cancellationToken = default)
        {
            lock (mutex)
            {
                var waitItemTracker = waitItemTrackerFreeList.Get();

                waitItemTracker.WaitUntilAfterSyntheticElapsedTime = waitUntilAfterDateTime.ToUniversalTime() - startedUtcDateTime;
                waitItemTracker.WaitSeqNum = ++waitItemSeqNumGen;
                waitItemTracker.CancellationToken = cancellationToken;
                waitItemTracker.TCS_UDTDateTime = new TaskCompletionSource<DateTime>();

                waitItemSortedList.Add(waitItemTracker, waitItemTracker);
                if (cancellationToken.CanBeCanceled)
                    cancelationCheckWaitItemListWithCachedArray.Add(waitItemTracker);

                return waitItemTracker.TCS_UDTDateTime.Task;
            }
        }

        /// <inheritdoc/>
        public Task<QpcTimeStamp> WaitUntilAsync(QpcTimeStamp waitUntilAfterQpcTimestamp, CancellationToken cancellationToken = default)
        {
            lock (mutex)
            {
                var waitItemTracker = waitItemTrackerFreeList.Get();

                waitItemTracker.WaitUntilAfterSyntheticElapsedTime = waitUntilAfterQpcTimestamp - startedQpcTimeStamp;
                waitItemTracker.WaitSeqNum = ++waitItemSeqNumGen;
                waitItemTracker.CancellationToken = cancellationToken;
                waitItemTracker.TCS_QpcTimeStamp = new TaskCompletionSource<QpcTimeStamp>();

                waitItemSortedList.Add(waitItemTracker, waitItemTracker);
                if (cancellationToken.CanBeCanceled)
                    cancelationCheckWaitItemListWithCachedArray.Add(waitItemTracker);

                return waitItemTracker.TCS_QpcTimeStamp.Task;
            }
        }

        private readonly object mutex = new object();

        private long waitItemSeqNumGen = 0;

        /// <summary>
        /// This class is used to track each of the wait items that have been added to the synthesizer's wait list(s)
        /// </summary>
        private class WaitItemTracker
        {
            /// <summary>Gives the <see cref="QpcTimeStamp"/> (synthetic or not) at which the correspondingly returned task has been requested to complete.</summary>
            public TimeSpan WaitUntilAfterSyntheticElapsedTime { get; set; }

            /// <summary>Gives the wait sequence number generated when the item was added.  Used to resolve cases where multiple items are added with the same wait until time so as to preserve the order that they were added in.</summary>
            public long WaitSeqNum { get; set; }

            /// <summary>Provides the <see cref="CancellationToken"/> that was provided when the item was added or default when none was provided.</summary>
            public CancellationToken CancellationToken { get; set; }

            /// <summary>This TaskCompletionSource is used for tasks that produce a <see cref="TimeSpan"/> value.</summary>
            public TaskCompletionSource<TimeSpan> TCS_TimeSpan { get; set; }

            /// <summary>This TaskCompletionSource is used for tasks that produce a UTC <see cref="DateTime"/> value.</summary>
            public TaskCompletionSource<DateTime> TCS_UDTDateTime { get; set; }

            /// <summary>This TaskCompletionSource is used for tasks that produce a <see cref="QpcTimeStamp"/> value.</summary>
            public TaskCompletionSource<QpcTimeStamp> TCS_QpcTimeStamp { get; set; }

            /// <summary>Used when returning a wait item to the free list.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear()
            {
                WaitUntilAfterSyntheticElapsedTime = TimeSpan.Zero;
                WaitSeqNum = 0;
                CancellationToken = CancellationToken.None;
                TCS_TimeSpan = null;
                TCS_UDTDateTime = null;
                TCS_QpcTimeStamp = null;
            }
        }

        /// <summary>
        /// This class provides the custom <see cref="Compare(WaitItemTracker, WaitItemTracker)"/> method that is used with the <see cref="waitItemSortedList"/>.
        /// </summary>
        private class WaitItemTrackerComparer : IComparer<WaitItemTracker>
        {
            /// <inheritdoc/>
            public int Compare(WaitItemTracker x, WaitItemTracker y)
            {
                var result = x.WaitUntilAfterSyntheticElapsedTime.CompareTo(y.WaitUntilAfterSyntheticElapsedTime);

                if (result == 0)
                    result = x.WaitSeqNum.CompareTo(y.WaitSeqNum);

                return result;
            }
        }

        private readonly MosaicLib.Utils.Pooling.BasicFreeList<WaitItemTracker> waitItemTrackerFreeList = new MosaicLib.Utils.Pooling.BasicFreeList<WaitItemTracker>() { MaxItemsToKeep = 1000, FactoryDelegate = () => new WaitItemTracker(), ClearDelegate = item => item.Clear() };

        private readonly SortedList<WaitItemTracker, WaitItemTracker> waitItemSortedList = new SortedList<WaitItemTracker, WaitItemTracker>(comparer: new WaitItemTrackerComparer());
        private readonly IListWithCachedArray<WaitItemTracker> cancelationCheckWaitItemListWithCachedArray = new IListWithCachedArray<WaitItemTracker>();
    }
}
