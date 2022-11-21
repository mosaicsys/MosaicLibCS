//-------------------------------------------------------------------
/*! @file Allocation.cs
 *  @brief A set of classes that support use of basic Identifier based allocation and release using Tasks.
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

using Mosaic.ToolsLib.Tasks.DiscreteEventTimeBase;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace Mosaic.ToolsLib.Tasks.Allocation
{
    /// <summary>
    /// This interface defines the public methods that are available with each <see cref="AllocationTool{TIdentifierType}"/>
    /// <para/>Allocate, Release, and Service
    /// </summary>
    public interface IAllocationTool<TIdentifierType>
        where TIdentifierType : IEquatable<TIdentifierType>
    {
        /// <summary>
        /// This <see cref="Task"/> factory method is used to request that the given <paramref name="id"/> be allocated for exclusive use by the caller.
        /// If the returned <see cref="Task"/> completes normally then the caller now has exclusive use of the given <paramref name="id"/> until it has been Released 
        /// or until the returned <see cref="IDAllocationToken{TIdentifierType}"/> is Disposed.  The given <paramref name="id"/> can also be internally released if a <see cref="Task"/> association 
        /// is set in the returned <see cref="IDAllocationToken{TIdentifierType}"/> and the <see cref="Task"/> is completed before the given <paramref name="id"/> has otherwise been Released.
        /// <para/>If the caller specifies a <paramref name="cancellationToken"/> or a non-zero <paramref name="timeLimit"/> then these will be monitored and if either condition is triggered
        /// before the allocation completes normally then the allocation request will be removed and the returned <see cref="Task"/> will be cancelled or aborted with a <see cref="TimeoutException"/>.
        /// </summary>
        Task<IDAllocationToken<TIdentifierType>> AllocateAsync(TIdentifierType id, CancellationToken cancellationToken = default, TimeSpan timeLimit = default);

        /// <summary>
        /// Attemps to allocate the indicated <paramref name="id"/>.  
        /// Returns an allocated <see cref="IDAllocationToken{TIdentifierType}"/> if the allocation was successful.  
        /// Returns null if the given <paramref name="id"/> is already allocated.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        IDAllocationToken<TIdentifierType> TryAllocate(TIdentifierType id);

        /// <summary>
        /// This method may be used to manually release the given <paramref name="id"/>.  
        /// This tool assumes that the caller is intentionally returning and releasing exclusive use of the <paramref name="id"/> and that it can be immediately allocated again.
        /// <para/>If the given <paramref name="service"/> is passed as true and their are other pending allocations for this <paramref name="id"/> then the first one will be completed during this call.
        /// </summary>
        void Release(TIdentifierType id, bool service = false);

        /// <summary>
        /// Gives a <see cref="IBasicNotificationList"/> that will be Notified each time an ID has been released.
        /// </summary>
        IBasicNotificationList NotifyOnRelease { get; }

        /// <summary>
        /// This method is used to scan for:
        /// <para/>* allocated IDs that have non-null <see cref="IDAllocationToken{TIdentifierType}.AutoReleaseOnTaskComplete"/> that has been completed.  These will be released.
        /// <para/>* released IDs that have one or more pending allocations, the first of which will be used to complete the next allocation of for the corresponding ID.
        /// <para/>* pending allocations where the <see cref="CancellationToken"/> is requesting cancellation or where a non-zero TimeLimit has been given and it has been reached.  These will be cancelled or aborted.
        /// </summary>
        void Service();

        /// <summary>
        /// Returns true if the given <paramref name="id"/> is currently allocated.
        /// </summary>
        bool IsAllocated(TIdentifierType id);
    }

    /// <summary>
    /// This class is produced be the <see cref="Task"/> that is returned by the <see cref="IAllocationTool{TIdentifierType}.AllocateAsync(TIdentifierType, CancellationToken, TimeSpan)"/> method on success.
    /// This class represents a successfully allocated ID, up until it is released by any of the available means, at which point the <see cref="Allocated"/> property will be false.
    /// </summary>
    /// <remarks>
    /// The allocated <see cref="ID"/> in this token can be released in three ways:
    /// <para/>* Using the <see cref="IAllocationTool{TIdentifierType}.Release(TIdentifierType, bool)"/> method directly.
    /// <para/>* Using the <see cref="Dispose"/> method on this instance.
    /// <para/>* or by using the <see cref="AttachAutoReleaseOnTaskComplete(Task)"/> method (or setting the <see cref="AutoReleaseOnTaskComplete"/> property directly) to associated 
    /// a <see cref="Task"/> with this token and then having <see cref="Task.IsCompleted"/> become true, as observed by the <see cref="IAllocationTool{TIdentifierType}.Service"/> method, 
    /// before ID has been relased through other means.
    /// </remarks>
    public class IDAllocationToken<TIdentifierType> 
        : IDisposable
        where TIdentifierType : IEquatable<TIdentifierType>
    {
        /// <summary>Gives the ID that was allocated to produce this instance.</summary>
        public TIdentifierType ID { get; set; }

        /// <summary>Initially set to true.  This will be set to false once the <see cref="ID"/> has been released.</summary>
        public bool Allocated
        {
            get { return (AllocationTool != null); }
            internal set { if (!value) AllocationTool = null; }
        }

        /// <summary>Holds the (non-null) <see cref="AllocationTool{TIdentifierType}"/> instance that generated this allocation token.  Setting the <see cref="Allocated"/> property to false also sets this property to null.</summary>
        internal AllocationTool<TIdentifierType> AllocationTool { get; set; }

        /// <summary>
        /// When this is non-null the source <see cref="IAllocationTool{TIdentifierType}"/> will, during the Service method, check if the <see cref="Task.IsCompleted"/> and if so it will automatically Release the allocated <see cref="ID"/>. 
        /// </summary>
        public Task AutoReleaseOnTaskComplete { get; set; }

        /// <summary>Releases the <see cref="ID"/> if it is currently <see cref="Allocated"/></summary>
        public void Dispose()
        {
            if (Allocated)
            {
                AllocationTool.Release(ID, service: false);
                Allocated = false;
            }
        }
    }

    /// <summary>Extension Methods</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>Sets the given <paramref name="idAllocationToken"/> AutoReleaseOnTaskComplete property to the given <paramref name="task"/>.  Supports call chaining.</summary>
        public static IDAllocationToken<TIdentifierType> AttachAutoReleaseOnTaskComplete<TIdentifierType>(this IDAllocationToken<TIdentifierType> idAllocationToken, Task task) where TIdentifierType : IEquatable<TIdentifierType>
        {
            idAllocationToken.AutoReleaseOnTaskComplete = task;
            return idAllocationToken;
        }
    }

    /// <summary>
    /// This is the implementation class for the <see cref="IAllocationTool{TIdentifierType}"/> interface.
    /// </summary>
    public class AllocationTool<TIdentifierType>
        : IAllocationTool<TIdentifierType>
        where TIdentifierType : IEquatable<TIdentifierType>
    {
        /// <summary>
        /// Constructor.  Allows the caller to specify an optional <paramref name="discreteEventTimeBase"/> to use for timeout determination when desired.
        /// </summary>
        /// <param name="discreteEventTimeBase"></param>
        public AllocationTool(IDiscreteEventTimeBase discreteEventTimeBase = null)
        {
            DiscreteEventTimeBase = discreteEventTimeBase;
        }

        private IDiscreteEventTimeBase DiscreteEventTimeBase { get; set; }

        /// <summary>
        /// Gives the <see cref="TaskCreationOptions"/> that are used with the <see cref="TaskCompletionSource{TResult}"/> instances that are created here.
        /// Defaults to <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>.
        /// </summary>
        public TaskCreationOptions TaskCreationOptions { get; set; } = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <inheritdoc/>
        public Task<IDAllocationToken<TIdentifierType>> AllocateAsync(TIdentifierType id, CancellationToken cancellationToken = default, TimeSpan timeLimit = default)
        {
            lock (apiMutex)
            {
                var ias = GetIDAllocationState(id, createIfNeeded: true);
                TaskCompletionSource<IDAllocationToken<TIdentifierType>> tcs;

                if (ias.Allocated)
                {
                    var timeLimitSpecified = !timeLimit.IsZero();

                    var pai = new PendingAllocationItem()
                    {
                        IDAllocationState = ias,
                        TCS = tcs = new TaskCompletionSource<IDAllocationToken<TIdentifierType>>(TaskCreationOptions),
                        CancellationToken = cancellationToken,
                        TimeLimit = timeLimit,
                        DESTimeLimitTimerTask = timeLimitSpecified ? DiscreteEventTimeBase?.WaitAsync(timeLimit, cancellationToken) : null,
                        QpcTimeLimitTimer = (DiscreteEventTimeBase == null && timeLimitSpecified) ? new QpcTimer() { TriggerInterval = timeLimit }.Start() : default,
                    };

                    ias.PendingAllocationItemList.Add(pai);
                    pendingAllocationItemList.Add(pai);
                }
                else
                {
                    ias.IDAllocationToken = new IDAllocationToken<TIdentifierType>() { ID = id, AllocationTool = this };

                    tcs = new TaskCompletionSource<IDAllocationToken<TIdentifierType>>(TaskCreationOptions);

                    tcs.SetResult(ias.IDAllocationToken);
                }

                return tcs.Task;
            }
        }

        /// <inheritdoc/>
        public IDAllocationToken<TIdentifierType> TryAllocate(TIdentifierType id)
        {
            lock (apiMutex)
            {
                var ias = GetIDAllocationState(id, createIfNeeded: true);

                if (!ias.Allocated)
                    return ias.IDAllocationToken = new IDAllocationToken<TIdentifierType>() { ID = id, AllocationTool = this };
                else
                    return null;
            }
        }

        /// <inheritdoc/>
        public void Release(TIdentifierType id, bool service = false)
        {
            bool idReleased = false;

            lock (apiMutex)
            {
                var ias = GetIDAllocationState(id, createIfNeeded: false);

                if (ias != null)
                {
                    if (ias.Allocated)
                    {
                        ias.IDAllocationToken.Allocated = false;
                        ias.Allocated = false;
                        idReleased = true;
                    }

                    if (service)
                        InnerService(ias);
                }
            }

            if (idReleased)
                notifyOnRelease.Notify();
        }

        /// <inheritdoc/>
        public IBasicNotificationList NotifyOnRelease => notifyOnRelease;
        private readonly BasicNotificationList notifyOnRelease = new BasicNotificationList();

        /// <inheritdoc/>
        public void Service()
        {
            lock (apiMutex)
            {
                foreach (var ias in idAllocationStateDictionary.ValueArray)
                {
                    InnerService(ias);
                }

                if (pendingAllocationItemList.Count > 0)
                {
                    QpcTimeStamp qpcTimeStamp = QpcTimeStamp.Now;

                    foreach (var pai in pendingAllocationItemList.Array)
                    {
                        var ias = pai.IDAllocationState;
                        var removePendingAllocationItem = false;

                        if (pai.CancellationToken.IsCancellationRequested)
                        {
                            pai.TCS.TrySetCanceled();
                            removePendingAllocationItem = true;
                        }
                        else if (pai.DESTimeLimitTimerTask?.IsCompleted == true)
                        {
                            pai.TCS.TrySetException(new TimeoutException($"Pending Allocation '{ias.ID}' time limit {pai.TimeLimit.TotalSeconds:f1} sec reached"));
                            removePendingAllocationItem = true;
                        }
                        else if (pai.QpcTimeLimitTimer.Started && pai.QpcTimeLimitTimer.GetIsTriggered(qpcTimeStamp))
                        {
                            pai.TCS.TrySetException(new TimeoutException($"Pending Allocation '{ias.ID}' time limit {pai.TimeLimit.TotalSeconds:f1} sec reached after {pai.QpcTimeLimitTimer.ElapsedTimeAtLastTrigger.TotalSeconds:f1} sec"));
                            removePendingAllocationItem = true;
                        }

                        if (removePendingAllocationItem)
                        {
                            ias.PendingAllocationItemList.Remove(pai);
                            pendingAllocationItemList.Remove(pai);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InnerService(IDAllocationState ias)
        {
            if (ias.Allocated && ias.IDAllocationToken.AutoReleaseOnTaskComplete?.IsCompleted == true)
            {
                ias.IDAllocationToken.Allocated = false;
                ias.Allocated = false;
            }

            if (!ias.Allocated && ias.PendingAllocationItemList.Count > 0)
            {
                var pai = ias.PendingAllocationItemList.SafeTakeFirst();
                pendingAllocationItemList.Remove(pai);

                ias.IDAllocationToken = new IDAllocationToken<TIdentifierType>() { ID = ias.ID, AllocationTool = this };

                pai.TCS.SetResult(ias.IDAllocationToken);
            }
        }

        /// <inheritdoc/>
        public bool IsAllocated(TIdentifierType id)
        {
            lock (apiMutex)
            {
                return GetIDAllocationState(id, createIfNeeded: false)?.Allocated ?? false;
            }
        }


        private readonly object apiMutex = new object();
        private readonly IDictionaryWithCachedArrays<TIdentifierType, IDAllocationState> idAllocationStateDictionary = new IDictionaryWithCachedArrays<TIdentifierType, IDAllocationState>();
        private readonly IListWithCachedArray<PendingAllocationItem> pendingAllocationItemList = new IListWithCachedArray<PendingAllocationItem>();

        private IDAllocationState GetIDAllocationState(TIdentifierType id, bool createIfNeeded)
        {
            if ((!idAllocationStateDictionary.TryGetValue(id, out IDAllocationState ias) || ias == null) && createIfNeeded)
            {
                idAllocationStateDictionary[id] = ias = new IDAllocationState()
                {
                    ID = id,
                };
            }

            return ias;
        }

        /// <summary>
        /// Represents the current allocation state and the list of pending allocations for a single ID.
        /// The tool maintains a dictionay of these indexed by the ID.
        /// </summary>
        private class IDAllocationState
        {
            /// <summary>gives the ID for this allocation state instance.</summary>
            public TIdentifierType ID { get; set; }

            /// <summary>When the allocation state is <see cref="Allocated"/> this property will refer to the <see cref="IDAllocationToken"/> that recorded the allocation.  Setting <see cref="Allocated"/> to false will also set this property to null.</summary>
            public IDAllocationToken<TIdentifierType> IDAllocationToken { get; set; }

            /// <summary>Indicates that the <see cref="ID"/> is currently allocated.  This is a synonym for having a non-null <see cref="IDAllocationToken"/>.  Setting this property to null also sets the <see cref="IDAllocationToken"/> property to null.</summary>
            public bool Allocated { get { return (IDAllocationToken != null); } set { if (!value) IDAllocationToken = null; } }

            /// <summary>Gives the list of current <see cref="PendingAllocationItem"/>s that are tracking requested allocations that are waiting for this <see cref="ID"/> to be released.</summary>
            public List<PendingAllocationItem> PendingAllocationItemList { get; private set; } = new List<PendingAllocationItem>();
        }

        /// <summary>
        /// Contains the information that is associated with each pending allocation request.
        /// </summary>
        private class PendingAllocationItem
        {
            /// <summary>Gives the <see cref="IDAllocationState"/> instance for this pending ID allocotion request</summary>
            public IDAllocationState IDAllocationState { get; set; }

            /// <summary>Gives the <see cref="TaskCompletionSource{IDAllocationToken{TIdentifierType}}"/> that will be used to signal successfull completion of the allocation, or if/when it is cancelled or aborted.</summary>
            public TaskCompletionSource<IDAllocationToken<TIdentifierType>> TCS { get; set; }

            /// <summary>Gives the <see cref="CancellationToken"/> that the caller provided with the Allocate request, if any.</summary>
            public CancellationToken CancellationToken { get; set; }

            /// <summary>Gives the <see cref="TimeSpan"/> time limit that the caller provided with the Allocate request, if any.</summary>
            public TimeSpan TimeLimit { get; set; }

            /// <summary>When <see cref="TimeLimit"/> is non-zero and the tool has been given a <see cref="IDiscreteEventTimeBase"/> instance to use, this will be set to the <see cref="Task"/> that will complete if the given <see cref="TimeLimit"/> is synthetically reached.</summary>
            public Task DESTimeLimitTimerTask { get; set; }

            /// <summary>When <see cref="TimeLimit"/> is non-zero and there is no <see cref="IDiscreteEventTimeBase"/> instance to use, this timer will trigger once the indicated time has elapsed.</summary>
            public QpcTimer QpcTimeLimitTimer { get; set; }
        }
    }
}
