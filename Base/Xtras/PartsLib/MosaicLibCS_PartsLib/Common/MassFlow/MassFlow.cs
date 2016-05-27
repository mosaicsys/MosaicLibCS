//-------------------------------------------------------------------
/*! @file MassFlow.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
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

namespace MosaicLib.PartsLib.Common.MassFlow
{
    public enum MassFlowUnits
    {
        Undefined = 0,
        None,
        percent,
        sccm,
        slm,
    }

    public enum PressureUnits
    {
        Undefined = 0,
        None,
        PSI,
        Bar,
        MilliBar,
        Torr,
        MilliTorr,
        Pascals,
        Kilopascals,
    }

    public enum TemperatureUnits
    {
        Undefined = 0,
        None,
        DegC,
        DegK,
        DegF,
    }

    [Obsolete("This interface has not been implemented and may eventually be removed (2015-05-08)")]
    public interface IMassFlowValue
    {
        double Value { get; }
        MassFlowUnits Units { get; }
    }

    [Obsolete("This interface has not been implemented and may eventually be removed (2015-05-08)")]
    public interface IPressureValue
    {
        double Value { get; }
        PressureUnits Units { get; }
    }

    [Obsolete("This interface has not been implemented and may eventually be removed (2015-05-08)")]
    public interface ITemperatureValue
    {
        double Value { get; }
        TemperatureUnits Units { get; }
    }

    /// <summary>
    /// A list of possible control modes for Mass Flow type devices.  Not all devices support all modes.
    /// </summary>
    public enum MassFlowControlMode
    {
        None = 0,
        Normal = 1,
        Closed = 2,
        Open = 3,

        ControlFlow = Normal,            // control regulator is set to control flow
        NoFlow = Closed,             // control regulator is set fully closed
        FullFlow = Open,               // control regulator is set fully open - often used for purge flow
        ControlPressure,        // control regulator is set to control pressure
        ControlValve,           // 
        Lock,                 // control regulator stops accepting new setpoints locks at current setpoint mode.
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given value is Undefined or None
        /// </summary>
        public static bool IsUndefinedOrNone(this MassFlowUnits value)
        {
            return (value == MassFlowUnits.Undefined || value == MassFlowUnits.None);
        }

        /// <summary>
        /// Returns true if the given value is Undefined or None
        /// </summary>
        public static bool IsUndefinedOrNone(this PressureUnits value)
        {
            return (value == PressureUnits.Undefined || value == PressureUnits.None);
        }

        /// <summary>
        /// Returns true if the given value is Undefined or None
        /// </summary>
        public static bool IsUndefinedOrNone(this TemperatureUnits value)
        {
            return (value == TemperatureUnits.Undefined || value == TemperatureUnits.None);
        }
    }
}

//-------------------------------------------------------------------
