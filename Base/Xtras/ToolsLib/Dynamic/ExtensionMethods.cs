//-------------------------------------------------------------------
/*! @file ExtensionMethods.cs
 *  @brief Defines a set of Extension Methods that may be used with Dynamic object types.
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

using MosaicLib.Modular.Common;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mosaic.ToolsLib.Dynamic
{
    namespace ExtensionMethods
    {
        /// <summary>
        /// DynamicObject specific Extension Methods.
        /// </summary>
        public static partial class ExtensionMethods
        {
            private static DynamicKVCFactory LocalDefaultDynamicKVCFactory { get; } = new DynamicKVCFactory();

            /// <summary>This EM creates, populates, and returns a new <see cref="DynamicKVC"/> from the given <paramref name="kvcSet"/></summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DynamicKVC ToDynamicKVC(this IEnumerable<KeyValuePair<string, ValueContainer>> kvcSet)
            {
                return LocalDefaultDynamicKVCFactory.Create(kvcSet);
            }

            /// <summary>This EM creates, populates, and returns a new <see cref="DynamicKVC"/> from the given <paramref name="kvpSet"/></summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DynamicKVC ToDynamicKVC(this IEnumerable<KeyValuePair<string, object>> kvpSet, bool ? enablePropertyGetterVCSuffixHandling = null)
            {
                if (enablePropertyGetterVCSuffixHandling == null)
                    return LocalDefaultDynamicKVCFactory.Create(kvpSet);
                else
                    return LocalDefaultDynamicKVCFactory.Create().SetEnableVCSuffixHandlingInline(enablePropertyGetterVCSuffixHandling ?? default).UpdateFrom(kvpSet);
            }

            /// <summary>This EM creates, populates, and returns a new <see cref="DynamicKVC"/> from the given <paramref name="nvSet"/></summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DynamicKVC ToDynamicKVC(this INamedValueSet nvSet)
            {
                return LocalDefaultDynamicKVCFactory.Create(nvSet);
            }

            /// <summary>This EM creates, populates, and returns a new <see cref="DynamicKVC"/> from the given <paramref name="jObject"/></summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DynamicKVC ToDynamicKVC(this JObject jObject)
            {
                return LocalDefaultDynamicKVCFactory.Create(jObject);
            }

            /// <summary>This EM sets the <see cref="DynamicKVC.EnablePropertyGetterVCSuffixHandling"/> property on the given <paramref name="dynamicKVC"/> to <paramref name="enablePropertyGetterVCSuffixHandling"/>.  Supports call chaining.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DynamicKVC SetEnableVCSuffixHandlingInline(this DynamicKVC dynamicKVC, bool enablePropertyGetterVCSuffixHandling)
            {
                dynamicKVC.EnablePropertyGetterVCSuffixHandling = enablePropertyGetterVCSuffixHandling;
                return dynamicKVC;
            }
        }
    }
}
