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
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Helpers
{
    /// <summary>
    /// This is a simple helper/utility class that can be used to calculate a recent average of the rate that some event happens.
    /// <para/>This class has been refactored to be completely implemented using the class source that has been moved to MosaicLib.Utils.Tools
    /// </summary>
    public class EventRateTool : MosaicLib.Utils.Tools.EventRateTool
    { }
}