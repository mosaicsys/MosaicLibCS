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

using MosaicLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mosaic.ToolsLib.Tasks.ExtensionMethods
{
    /// <summary>
    /// Task specific Extension Methods.
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>Variant of <see cref="Task.Start"/> that supports call chaining.</summary>
        public static TTaskType StartInline<TTaskType>(this TTaskType task) 
            where TTaskType : Task
        {
            task.Start();
            return task;
        }

        /// <summary>Variant of <see cref="Task.Start(TaskScheduler)"/> that supports call chaining.</summary>
        public static TTaskType StartInline<TTaskType>(this TTaskType task, TaskScheduler taskScheduler)
            where TTaskType : Task
        {
            task.Start(taskScheduler);
            return task;
        }
    }
}
