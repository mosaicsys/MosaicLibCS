//-------------------------------------------------------------------
/*! @file ExtensionMethods.cs
 *  @brief Defines a set of Extension Methods that may be used with System.Threading.Tasks Tasks.
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

using MosaicLib.Modular.Action;
using MosaicLib.Utils;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mosaic.ToolsLib.Tasks
{
    namespace ExtensionMethods
    {
        /// <summary>
        /// Task specific Extension Methods.
        /// </summary>
        public static partial class ExtensionMethods
        {
            /// <summary>Variant of <see cref="Task.Start"/> that supports call chaining.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static TTaskType StartInline<TTaskType>(this TTaskType task)
                where TTaskType : Task
            {
                task.Start();
                return task;
            }

            /// <summary>Variant of <see cref="Task.Start(TaskScheduler)"/> that supports call chaining.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static TTaskType StartInline<TTaskType>(this TTaskType task, TaskScheduler taskScheduler)
                where TTaskType : Task
            {
                task.Start(taskScheduler);
                return task;
            }

            /// <summary>
            /// Variant of <see cref="Task.Wait"/> that supports call chaining.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static TTaskType WaitInline<TTaskType>(this TTaskType task)
                where TTaskType : Task
            {
                task.Wait();
                return task;
            }

            /// <summary>
            /// Variant of <see cref="Task.Wait(CancellationToken)"/> that supports call chaining.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static TTaskType WaitInline<TTaskType>(this TTaskType task, CancellationToken cancellationToken)
                where TTaskType : Task
            {
                task.Wait(cancellationToken);
                return task;
            }

            /// <summary>
            /// Variant of <see cref="Task.Wait(TimeSpan)"/> that supports call chaining.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static TTaskType WaitInline<TTaskType>(this TTaskType task, TimeSpan timeLimit)
                where TTaskType : Task
            {
                task.Wait(timeLimit);
                return task;
            }

            /// <summary>
            /// Variant of <see cref="Task.Wait(int, CancellationToken)"/> that supports call chaining.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static TTaskType WaitInline<TTaskType>(this TTaskType task, int timeLimit, CancellationToken cancellationToken)
                where TTaskType : Task
            {
                task.Wait(timeLimit, cancellationToken);
                return task;
            }

            /// <summary>
            /// Helper EM that is used to generate a new Task that will re-route the continuation back onto the given <paramref name="taskScheduler"/>
            /// </summary>
            public static Task ContinueOn(this Task taskIn, TaskScheduler taskScheduler = null, TaskContinuationOptions taskContinuationOptions = TaskContinuationOptions.RunContinuationsAsynchronously)
            {
                var taskOut = taskIn.ContinueWith(task => { task.Wait(); }, CancellationToken.None, taskContinuationOptions, taskScheduler ?? TaskScheduler.Default);

                return taskOut;
            }

            /// <summary>
            /// Helper EM that is used to generate a new Task that will re-route the continuation back onto the given <paramref name="taskScheduler"/>
            /// </summary>
            public static Task<TTaskResultType> ContinueOn<TTaskResultType>(this Task<TTaskResultType> taskIn, TaskScheduler taskScheduler = null, TaskContinuationOptions taskContinuationOptions = TaskContinuationOptions.RunContinuationsAsynchronously)
            {
                var taskOut = taskIn.ContinueWith(task => { return task.Result; }, CancellationToken.None, taskContinuationOptions, taskScheduler ?? TaskScheduler.Default);

                return taskOut;
            }

            /// <summary>
            /// This helper EM will start the given <paramref name="icf"/> and return a <see cref="Task{TICFType}"/> that will complete
            /// after the given <paramref name="icf"/> completes.  If the action is null or it cannot be started then the resulting
            /// <see cref="Task{TICFType}"/> will fail with an exception.
            /// </summary>
            /// <exception cref="RunActionInlineAsyncStartFailedException">thrown when the <see cref="IClientFacet.Start"/> method call fails.</exception>
            /// <exception cref="RunActionInlineAsyncFailedException">thrown when the given <paramref name="icf"/> fails and the <paramref name="convertFailureToException"/> option is true (the default).</exception>
            public static Task<TICFType> RunActionInlineAsync<TICFType>(this TICFType icf, TaskContinuationOptions taskContinuationOptions = TaskContinuationOptions.RunContinuationsAsynchronously, bool convertFailureToException = true)
                where TICFType : IClientFacet
            {
                var tcs = new TaskCompletionSource<TICFType>(taskContinuationOptions);

                try
                {
                    if (icf == null)
                        throw new System.ArgumentNullException(nameof(icf));

                    icf.NotifyOnComplete.AddItem(() =>
                    {
                        bool somethingSet = false;

                        if (convertFailureToException && icf.ActionState.Failed)
                            somethingSet |= tcs.TrySetException(new RunActionInlineAsyncFailedException(icf));

                        if (!somethingSet)
                            somethingSet |= tcs.TrySetResult(icf);
                    });

                    var ias = icf.ActionState;

                    if (ias.StateCode != ActionStateCode.Ready)
                        throw new RunActionInlineAsyncStartFailedException(icf, "The given icf was not in the Ready state.  It cannot be reused here.");

                    var ec = icf.Start();

                    if (ec.IsNeitherNullNorEmpty())
                        throw new RunActionInlineAsyncStartFailedException(icf, ec);
                }
                catch (System.Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return tcs.Task;
            }
        }
    }

    /// <summary>
    /// This exception is used the <see cref="ExtensionMethods.RunActionInlineAsync{TICFType}(TICFType, TaskContinuationOptions, bool)"/> method when
    /// the action cannot be started normally and/or when the action is not in the Ready state (aka action cannot be re-used).
    /// </summary>
    public class RunActionInlineAsyncStartFailedException : System.Exception
    {
        public RunActionInlineAsyncStartFailedException(IClientFacet icf, string ec, System.Exception innerException = null)
            : base($"Attempt to start '{icf.ToString(ToStringSelect.MesgDetailAndState)}' failed: {ec}", innerException)
        {
            ErrorCode = ec;
            ICF = icf;
        }

        public string ErrorCode { get; }

        public IClientFacet ICF { get; }
    }
    /// <summary>
    /// This exception is used the <see cref="ExtensionMethods.RunActionInlineAsync{TICFType}(TICFType, TaskContinuationOptions, bool)"/> method when the 
    /// convertFailureToException optional is selected and the given <see cref="IClientFacet"/> fails.
    /// </summary>
    public class RunActionInlineAsyncFailedException : System.Exception
    {
        public RunActionInlineAsyncFailedException(IClientFacet icf, System.Exception innerException = null)
            : base(icf.ToString(ToStringSelect.MesgDetailAndState), innerException)
        {
            ICF = icf;
        }

        public IClientFacet ICF { get; }
    }
}
