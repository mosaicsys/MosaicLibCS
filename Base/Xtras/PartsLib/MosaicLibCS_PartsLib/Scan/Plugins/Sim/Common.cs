//-------------------------------------------------------------------
/*! @file Common.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved.
 * Copyright (c) 2016 Mosaic Systems Inc.  All rights reserved.
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
using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.PartsLib.Common.MassFlow;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.Time;
using MosaicLib.Modular.Interconnect.Values;

namespace MosaicLib.PartsLib.Scan.Plugin.Sim.Common
{
    /// <summary>
    /// Common Enumeration to be used with simple FaultInjection related controls (typically combo boxes) and their related IVAs
    /// <para/>This is a Flags enumeration so that a single value can support each fault type being independently selected.  Actual prioritization and implementation of requests
    /// Fault values is always device specific.  Not all context menues will support all fault times either.
    /// <para/>Values: None = Normal = 0, Offline = 0x0001, SensorFault = SensorOpen = 0x0010, SensorShort = 0x0020, ForceFreeze = 0x0100, ForceClose = 0x0200, ForceOpen = 0x0400,
    /// </summary>
	[Flags]
	public enum FaultInjection : int
	{
        /// <summary>Fallback placeholder value that indictes that no individual Faults have been requested. 0x0000 (Same as Normal)</summary>
		None = 0x0000,
        /// <summary>Fallback placeholder value that indictes that the target device should be have normally.  No individual Faults have been requested. 0x0000 (Same as None)</summary>
        Normal = 0x0000,
        /// <summary>Request that the target device behave like it has lost power and can no longer respond.  0x0001</summary>
        Offline = 0x0001,
        /// <summary>Request that the target device/sensor behave like the sensor measurement logic has failed.  0x0010 (Same as SensorOpen)</summary>
        SensorFault = 0x0010,
        /// <summary>Request that the target device/sensor behave like the sensor measurement logic has failed in the open circuit state.  0x0010 (Same as SensorFault)</summary>
        SensorOpen = 0x0010,
        /// <summary>Request that the target device/sensor behave like the sensor measurement logic has failed in the short circuit state.  0x0020</summary>
        SensorShort = 0x0020,
        /// <summary>Request that the target device behave like any actuator that it controls has become stuck (froozen) in its last controlled state.  0x0100</summary>
        ForceFreeze = 0x0100,
        /// <summary>Request that the target device behave like any actuator that it controls has become stuck in the fully closed state.  0x0200</summary>
        ForceClose = 0x0200,
        /// <summary>Request that the target device behave like any actuator that it controls has become stuck in the fully opened state.  0x0400</summary>
        ForceOpen = 0x0400,
    }

    public static partial class ExtensionMethods
    {
        public static bool IsSet(this FaultInjection value, FaultInjection testMask)
        {
            return value.IsMatch(testMask, testMask);
        }

        public static bool IsMatch(this FaultInjection value, FaultInjection mask, FaultInjection expectedValue)
        {
            return ((value & mask) == expectedValue);
        }
    }

}

//-------------------------------------------------------------------
