//-------------------------------------------------------------------
/*! @file ManualTaskScheduler.cs
 *  @brief Defines a task scheduler with local custom passthrough synchronization context to support use of a single threaded Concurrent Task and async/await usage pattern.
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

using MosaicLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mosaic.ToolsLib.Tasks
{
    /// <summary>
    /// This class provides a <see cref="TaskScheduler"/> that is used to manually queue and (incrementally) execute the related tasks when the <see cref="Service"/> method is called.
    /// NOTE: when this class is constructed with the captureAndWrapSychronizationContext set to true (the default), the constructor will wrap and replace the <see cref="Thread"/>'s current <see cref="SynchronizationContext"/>
    /// with one that forwards all Post calls to be serviced by that <see cref="ManualTaskScheduler"/> instance.  
    /// This allows async/await methods to route their task execution back to same thread that is being used to service that <see cref="ManualTaskScheduler"/> instance.
    /// </summary>
    public class ManualTaskScheduler : TaskScheduler
    {
        /// <summary>
        /// Constructor.
        /// <para/>When the <paramref name="setServiceThreadToCurrentThread"/> is set to true (the default) then it calls the <see cref="SetSelectedServiceThreadToCurrentThread"/> method
        /// to capture the current the current <see cref="Thread"/> and its current <see cref="SynchronizationContext"/> for internal use.
        /// If the <paramref name="captureAndWrapSychronizationContext"/> is set to true (the default) then this method will addtionally replace the current <see cref="Thread"/>'s SynchronizationContext
        /// with the newly generated one so that await generated task execution will be routed back to the <see cref="SelectedServiceThread"/>.
        /// </summary>
        public ManualTaskScheduler(bool setServiceThreadToCurrentThread = true, bool captureAndWrapSychronizationContext = true)
        {
            localReroutePostSyncronizationContext = new ReroutePostSynchronizationContext() 
            { 
                PostToManualTaskScheduler = this, 
            };

            if (setServiceThreadToCurrentThread)
                SetSelectedServiceThreadToCurrentThread(captureAndWrapSychronizationContext);
        }

        /// <summary>
        /// When true, this property allows the <see cref="TryExecuteTaskInline(Task, bool)"/> method to execute inline tasks and inline task continuations when they are already using the current task scheduler and <see cref="SelectedServiceThread"/>.
        /// When false, all such calls will return false to indicate that the task must be (re)queued.
        /// </summary>
        public bool EnableInlineTaskExecution { get; set; }

        /// <summary>
        /// When true the first call to <see cref="Service"/> may be used to automatically set the <see cref="SelectedServiceThread"/>.  
        /// When false the <see cref="SelectedServiceThread"/> must be determined before the <see cref="Service"/> method can be used, either in the constructor in using the <see cref="SetSelectedServiceThreadToCurrentThread"/> method.
        /// </summary>
        public bool EnableAutomaticThreadSelectionOnFirstService { get; set; }

        /// <summary>
        /// Captures the current <see cref="Thread"/> and its current <see cref="SynchronizationContext"/>.
        /// This method may only be called once, and only if the class constructor was explicitly told to not set the <see cref="SelectedServiceThread"/>, which it does by default.
        /// If the <paramref name="captureAndWrapSychronizationContext"/> is set to true (the default) then this method will addtionally replace the current <see cref="Thread"/>'s SynchronizationContext
        /// with the newly generated one so that await generated task execution will be routed back to the <see cref="SelectedServiceThread"/>.
        /// </summary>
        public ManualTaskScheduler SetSelectedServiceThreadToCurrentThread(bool captureAndWrapSychronizationContext = true)
        {
            if (SelectedServiceThread != null)
                throw new System.InvalidOperationException($"{Fcns.CurrentMethodName} failed: This method cannot be used once the a service thread has been selected.");

            SelectedServiceThread = System.Threading.Thread.CurrentThread;
            localReroutePostSyncronizationContext.PassthroughSynchronizationContext = SynchronizationContext.Current;

            if (captureAndWrapSychronizationContext)
                SynchronizationContext.SetSynchronizationContext(localReroutePostSyncronizationContext);

            return this;
        }

        private readonly ReroutePostSynchronizationContext localReroutePostSyncronizationContext;

        /// <summary>
        /// This custom <see cref="SynchronizationContext"/> derived class is used to reroute all <see cref="Post(SendOrPostCallback, object)"/> calls to be run on the 
        /// </summary>
        private class ReroutePostSynchronizationContext : SynchronizationContext
        {
            /// <summary>Gives the <see cref="ManualTaskScheduler"/> instance to which the <see cref="Post(SendOrPostCallback, object)"/> calls will be routed</summary>
            public ManualTaskScheduler PostToManualTaskScheduler { get; set; }

            /// <summary>Gives the <see cref="SynchronizationContext"/> instance to which the <see cref="Send(SendOrPostCallback, object)"/>, and <see cref="Wait(IntPtr[], bool, int)"/> calls are normally routed.</summary>
            public SynchronizationContext PassthroughSynchronizationContext { get; set; }

            /// <inheritdoc/>
            public override SynchronizationContext CreateCopy()
            {
                return this;
            }

            /// <inheritdoc/>
            public override void Send(SendOrPostCallback d, object state)
            {
                if (PassthroughSynchronizationContext != null)
                    PassthroughSynchronizationContext.Send(d, state);
                else
                    base.Send(d, state);
            }

            /// <inheritdoc/>
            public override void Post(SendOrPostCallback d, object state)
            {
                PostToManualTaskScheduler.Post(d, state);
            }

            /// <inheritdoc/>
            public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
            {
                if (PassthroughSynchronizationContext != null)
                    return PassthroughSynchronizationContext.Wait(waitHandles, waitAll, millisecondsTimeout);
                else
                    return base.Wait(waitHandles, waitAll, millisecondsTimeout);
            }

            /// <inheritdoc/>
            public override void OperationCompleted()
            {
                if (PassthroughSynchronizationContext != null)
                    PassthroughSynchronizationContext.OperationCompleted();
                else
                    base.OperationCompleted();
            }

            /// <inheritdoc/>
            public override void OperationStarted()
            {
                if (PassthroughSynchronizationContext != null)
                    PassthroughSynchronizationContext.OperationStarted();
                else
                    base.OperationStarted();
            }
        }

        private struct TaskOrPostItem
        {
            public Task task;
            public SendOrPostCallback sendOrPostCallback;
            public object sendOrPostCallbackState;
        }

        private readonly object itemQueueMutex = new object();
        private readonly Queue<TaskOrPostItem> itemQueue = new Queue<TaskOrPostItem>();
        private volatile int volatileItemQueueCount = 0;

        /// <summary>
        /// Returns the number of task items that are currently queued in this scheduler instance.
        /// This includes both tasks and related post callback items.
        /// </summary>
        public int QueuedItemCount => volatileItemQueueCount;

        /// <summary>Gives the <see cref="Thread"/> that has been selected for use by this task scheduler, or null if no such <see cref="Thread"/> as been selected yet.</summary>
        public System.Threading.Thread SelectedServiceThread { get; private set; }

        private readonly List<TaskOrPostItem> serviceTaskOrPostItemList = new List<TaskOrPostItem>();

        /// <summary>This <see cref="IBasicNotificationList"/> will be Notified whenever a new task has been queued.</summary>
        public IBasicNotificationList NotifyOnTaskQueued => notifyOnTaskQueued;
        private readonly BasicNotificationList notifyOnTaskQueued = new BasicNotificationList();

        /// <summary>
        /// This method first checks if the current thread is the same as the firstServiceThread.  If not it will throw a <see cref="InvalidOperationException"/>.
        /// Next method first checks if there may be any Tasks in the queue already.  If not then the method exists.
        /// Next it captures the current contents of the task queue and then iteratively (and incrementally) executes these tasks.
        /// Note that this method supports queueing additional tasks while the method is executing.  It "locks down" the set of tasks it will execute during this call when it extracts the current contents of the queue
        /// and then any additional tasks that are queued from that point on will only be processed during the next call.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Service()
        {
            if (SelectedServiceThread != Thread.CurrentThread)
                HandleNonMatchingServiceThreadCase(nameof(Service));

            if (volatileItemQueueCount == 0)
                return;

            lock (itemQueueMutex)
            {
                serviceTaskOrPostItemList.AddRange(itemQueue);

                itemQueue.Clear();
                volatileItemQueueCount = 0;
            }

            int listCount = serviceTaskOrPostItemList.Count;

            if (listCount > 0)
            {
                var entrySynchronizationContext = SynchronizationContext.Current;
                var pushSynchContextNeeded = (entrySynchronizationContext != localReroutePostSyncronizationContext);

                if (pushSynchContextNeeded)
                    SynchronizationContext.SetSynchronizationContext(localReroutePostSyncronizationContext);

                try
                {
                    for (int index = 0; index < listCount; index++)
                    {
                        var item = serviceTaskOrPostItemList[index];

                        if (item.task != null)
                            base.TryExecuteTask(item.task);
                        else 
                            item.sendOrPostCallback?.Invoke(item.sendOrPostCallbackState);
                    }
                }
                finally
                {
                    if (pushSynchContextNeeded)
                        SynchronizationContext.SetSynchronizationContext(entrySynchronizationContext);
                }
            }

            serviceTaskOrPostItemList.Clear();
        }

        private void HandleNonMatchingServiceThreadCase(string methodName)
        {
            if (SelectedServiceThread != null)
                throw new InvalidOperationException($"Invalid thread used to call {methodName}: this method may only be called using the previously selected service thread [current:'{Thread.CurrentThread}' != selected:'{SelectedServiceThread}']");
            else if (!EnableAutomaticThreadSelectionOnFirstService)
                throw new InvalidOperationException($"Invalid call to {methodName}: SetServiceThreadToCurrentThread, or the constructor, must be used to set the selected service thread before calling this method.");
            else
                SetSelectedServiceThreadToCurrentThread();
        }

        /// <inheritdoc/>
        public override int MaximumConcurrencyLevel => 1;

        /// <summary>
        /// This internal method is used by the Post reroute synchronization context to move its routed calls to be handled by this task scheduler as if the given calls were actually tasks
        /// </summary>
        protected void Post(SendOrPostCallback sendOrPostCallback, object sendOrPostCallbackState)
        {
            lock (itemQueueMutex)
            {
                itemQueue.Enqueue(new TaskOrPostItem() { sendOrPostCallback = sendOrPostCallback, sendOrPostCallbackState = sendOrPostCallbackState });
                volatileItemQueueCount = itemQueue.Count;
            }

            notifyOnTaskQueued.Notify();
        }

        /// <inheritdoc/>
        protected override void QueueTask(Task task)
        {
            lock (itemQueueMutex)
            {
                itemQueue.Enqueue(new TaskOrPostItem() { task = task });
                volatileItemQueueCount = itemQueue.Count;
            }

            notifyOnTaskQueued.Notify();
        }

        /// <inheritdoc/>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (EnableInlineTaskExecution && TaskScheduler.Current == this && Thread.CurrentThread == SelectedServiceThread)
                return TryExecuteTask(task);

            return false;
        }

        /// <inheritdoc/>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            Task[] taskArray;

            lock (itemQueueMutex)
            {
                taskArray = itemQueue.Select(item => item.task).WhereIsNotDefault().ToArray();
            }

            return taskArray;
        }
    }
}
