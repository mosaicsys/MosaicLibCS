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

namespace MosaicLib.PartsLib.Common.MassFlow
{
    public enum MassFlowUnits
    {
        Undefined = 0,
        percent,
        sccm,
        slm,
    }

    public enum PressureUnits
    {
        Undefined = 0,
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
        DegC,
        DegK,
        DegF,
    }

    public interface IMassFlowValue
    {
        double Value { get; }
        MassFlowUnits Units { get; }
    }

    public interface IPressureValue
    {
        double Value { get; }
        PressureUnits Units { get; }
    }

    public interface ITemperatureValue
    {
        double Value { get; }
        TemperatureUnits Units { get; }
    }

    public enum MassFlowControlMode
    {
        NoFlow = 0,             // control regulator is set fully closed
        FullFlow,               // control regulator is set fully open - often used for purge flow
        ControlFlow,            // control regulator is set to control flow
        ControlPressure,        // control regulator is set to control pressure
    }
}

//-------------------------------------------------------------------
