//-------------------------------------------------------------------
/*! @file MFC.cs
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

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.PartsLib.Scan.Plugin.Sim.Common;
using MosaicLib.PartsLib.Scan.ScanEngine;
using MosaicLib.Time;
using MosaicLib.Utils;

using Units = MosaicLib.PartsLib.Common.Physics.UnitsOfMeasure;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Values;

namespace MosaicLib.PartsLib.Scan.Plugin.Sim.MFC
{
    /// <summary>
    /// Nominal set of expected control modes that a digital MFC can be set to perform.
    /// </summary>
    public enum ControlMode
    {
        /// <summary>Requests that the MFC attempt to control to the setpoint normally.</summary>
        Normal = 0,
        /// <summary>Requests that the MFC close its valve as much as possible to produce the smallest possible flow.</summary>
        Close,
        /// <summary>Requests that the MFC open its valve as much as possible to produce the largest possible flow.</summary>
        Open,
        /// <summary>Requests that the MFC latch its valve in the current position.</summary>
        Freeze,
        /// <summary>Requests that the MFC follow the setpoint from an analog input.  The MFC simulator decides what to do.  Normally it treats this like Close</summary>
        Analog,
    }

    /// <summary>
    /// This ScanEngine Plugin provides very basic simulated fucntionality of an MFC.
    /// </summary>
    public class MFCSimScanEnginePlugin : ScanEnginePluginBase
    {
        public MFCSimScanEnginePlugin(string name) : this(name, null) { }
        public MFCSimScanEnginePlugin(string name, MFCSimPluginConfig config) 
            : base(name) 
        {
            Config = new MFCSimPluginConfig(config);

            TrackFullScaleFlowChanges();

            OutputValues = new MFCSimPluginOutputs();
        }

        public MFCSimPluginConfig Config { get; protected set; }
        public MFCSimPluginOutputs OutputValues { get; protected set; }

        private void TrackFullScaleFlowChanges()
        {
            if (fullScaleFlow != Config.FullScaleFlow)
            {
                fullScaleFlow = Config.FullScaleFlow;
                if (fullScaleFlow > 0.0)
                {
                    fullScaleFlowToPercentOfFS = (100.0 / fullScaleFlow);
                    percentOfFSToFullScaleFlow = (fullScaleFlow * 0.01);
                }
                else
                {
                    fullScaleFlowToPercentOfFS = 0.0;
                    percentOfFSToFullScaleFlow = 0.0;
                }

                Logger.Debug.Emit("Full Scale Flow changed to {0:f1}", fullScaleFlow);
            }
        }

        private double fullScaleFlow;
        private double fullScaleFlowToPercentOfFS, percentOfFSToFullScaleFlow;

        public double FlowSetpointTarget 
        {
            get { return flowSetpointTarget; }
            set
            {
                if (flowSetpointTarget != value)
                {
                    flowSetpointTarget = value;
                    flowSetpointTargetInPercentOfFS = value * fullScaleFlowToPercentOfFS;
                    Logger.Debug.Emit("Setpoint changed to {0:f3} [{1:f2} %]", flowSetpointTarget, flowSetpointTargetInPercentOfFS);
                }
            }
        }
        public double FlowSetpointTargetInPercentOfFS
        {
            get { return flowSetpointTargetInPercentOfFS; }
            set
            {
                if (flowSetpointTargetInPercentOfFS != value)
                {
                    flowSetpointTargetInPercentOfFS = value;
                    flowSetpointTarget = value * percentOfFSToFullScaleFlow;
                    Logger.Debug.Emit("Setpoint changed to {0:f2} % [{1:f2}]", flowSetpointTargetInPercentOfFS, flowSetpointTarget);
                }
            }
        }

        double flowSetpointTarget;
        double flowSetpointTargetInPercentOfFS;

        public ControlMode ControlMode { get; set; }

        public string FaultInjectionStr
        {
            get { return _faultInjectionStr; }
            set
            {
                value = value.MapNullToEmpty();
                if (_faultInjectionStr != value)
                {
                    _faultInjectionStr = value;
                    FaultInjection = Utils.Enum.TryParse(value, Common.FaultInjection.None);
                }
            }
        }
        private string _faultInjectionStr = string.Empty;

        public FaultInjection FaultInjection { get; set; }

        public override void Service(TimeSpan measuredServiceInterval, QpcTimeStamp scanStartTime)
        {
            double periodInSeconds = measuredServiceInterval.TotalSeconds;

            TrackFullScaleFlowChanges();

            double setpointTarget = flowSetpointTarget;
            double setpointTargetInPercentFS = setpointTarget * fullScaleFlowToPercentOfFS;
            double updatedTrackingSetpoint = Config.ApplySlewLimit(OutputValues.TrackingFlowSetpoint, setpointTarget, periodInSeconds);
            double trackingSetpointPercentFS = OutputValues.TrackingFlowSetpoint * fullScaleFlowToPercentOfFS;

            ControlMode controlMode = ControlMode;
            FaultInjection faultInjection = FaultInjection;

            bool isOnline = !faultInjection.IsSet(FaultInjection.Offline);
            bool forceClosed = faultInjection.IsSet(FaultInjection.ForceClose);
            bool forceOpen = faultInjection.IsSet(FaultInjection.ForceOpen);
            bool forceFreeze = faultInjection.IsSet(FaultInjection.ForceFreeze);
            bool isForced = (forceClosed || forceOpen || forceClosed);

            bool closeValve = (forceClosed || (!isForced && (controlMode == ControlMode.Close || controlMode == ControlMode.Analog)));
            bool openValve = (forceOpen || (!isForced && controlMode == ControlMode.Open));
            bool freezeValve = (forceFreeze || (!isForced && controlMode == ControlMode.Freeze));

            if (!closeValve && !openValve && !freezeValve)
            {
                if ((setpointTargetInPercentFS <= Config.SetpointThresholdForCloseInPercentOfFS) && (trackingSetpointPercentFS <= Config.SetpointThresholdForCloseInPercentOfFS))
                    closeValve = true;
                else if ((setpointTargetInPercentFS >= Config.SetpointThresholdForOpenInPercentOfFS) && (trackingSetpointPercentFS >= Config.SetpointThresholdForOpenInPercentOfFS))
                    openValve = true;
            }

            OutputValues.IsDeviceOnline = isOnline;

            if (closeValve)
            {
                OutputValues.TrackingFlowSetpointInPercentOfFS = trackingSetpointPercentFS = 0.0;
                OutputValues.TrackingFlowSetpoint = trackingSetpointPercentFS * percentOfFSToFullScaleFlow;
            }
            else if (openValve)
            {
                OutputValues.TrackingFlowSetpointInPercentOfFS = trackingSetpointPercentFS = 125.0;
                OutputValues.TrackingFlowSetpoint = trackingSetpointPercentFS * fullScaleFlowToPercentOfFS;
            }
            else if (freezeValve)
            {
                // freeze the tracking setpoint (no more updates to the tracking setpoint).
                trackingSetpointPercentFS = OutputValues.TrackingFlowSetpointInPercentOfFS;
            }
            else
            {
                // early calculations do not need to be updated
                OutputValues.TrackingFlowSetpoint = updatedTrackingSetpoint;
                OutputValues.TrackingFlowSetpointInPercentOfFS = updatedTrackingSetpoint * fullScaleFlowToPercentOfFS;
            }

            double flowNoise = (isOnline ? (Config.FlowNoise * Rng.GetNextRandomInMinus1ToPlus1Range()) : 0.0);
            double tempNoise = (isOnline ? (Config.TemperatureNoiseInDegC * Rng.GetNextRandomInMinus1ToPlus1Range()) : 0.0);

            OutputValues.MeasuredFlow = OutputValues.TrackingFlowSetpoint + flowNoise;
            OutputValues.MeasuredFlowInPercentOfFS = OutputValues.MeasuredFlow * fullScaleFlowToPercentOfFS;

            OutputValues.TotalOperatingHours += measuredServiceInterval.TotalHours;
            OutputValues.TotalFlow += OutputValues.MeasuredFlow * measuredServiceInterval.TotalMinutes;

            OutputValues.TemperatureInDegC = (Config.NominalTemperatureInDegC + tempNoise);

            if (forceClosed)
                OutputValues.ValvePositionInPercent = 0.0;
            else if (forceOpen)
                OutputValues.ValvePositionInPercent = 100.0;
            else 
                OutputValues.ValvePositionInPercent = OutputValues.MeasuredFlowInPercentOfFS * 0.75;
        }
    }

    /// <summary>
    /// Used with <see cref="MFCSimScanEnginePlugin"/> as TConfigValueSetType.  
    /// Defines the values that are used to configure how a specific MFCSimScanEnginePlugin behaves.
    /// </summary>
    public class MFCSimPluginConfig
    {
        /// <summary>
        /// Default constructor.
        /// <para/>
        /// FullScaleFlow = 1000.0, FlowNoise = 5.0, 
        /// NominalDeviceTemperatureInDegC = 27.0, DeviceTemperatureNoiseInDegC = 0.20,
        /// FullScaleResponsePeriodInSeconds = 1.25, SetpointThresholdForCloseInPercentOfFS = 5.0, SetpointThresholdForOpenInPercentOfFS = 105.0
        /// </summary>
        public MFCSimPluginConfig()
        {
            FlowUnits = Units.VolumetricFlowUnits.sccm;
            FullScaleFlow = 1000.0;
            FlowNoise = 5.0;
            NominalTemperatureInDegC = 27.0;
            TemperatureNoiseInDegC = 0.20;
            FullScaleResponsePeriod = (1.25).FromSeconds();
            SetpointThresholdForCloseInPercentOfFS = 5.0;
            SetpointThresholdForOpenInPercentOfFS = 105.0;
        }

        /// <summary>Copy constructor</summary>
        public MFCSimPluginConfig(MFCSimPluginConfig other)
        {
            FlowUnits = other.FlowUnits;
            FullScaleFlow = other.FullScaleFlow;
            FlowNoise = other.FlowNoise;
            NominalTemperatureInDegC = other.NominalTemperatureInDegC;
            FullScaleResponsePeriod = other.FullScaleResponsePeriod;
            SetpointThresholdForCloseInPercentOfFS = other.SetpointThresholdForCloseInPercentOfFS;
            SetpointThresholdForOpenInPercentOfFS = other.SetpointThresholdForOpenInPercentOfFS;
        }

        /// <summary>Gives the full scale flow for this MFC in user units.  Normally has units of sccm</summary>
        public Units.VolumetricFlowUnits FlowUnits { get; set; }

        /// <summary>Gives the full scale flow for this MFC in user units.  Normally has units of sccm</summary>
        public double FullScaleFlow { get; set; }

        /// <summary>Gives the flow noise that this MFC will exhibit in user units.  This is the half width of the +- range.  Total range of noise will be twice this value.</summary>
        public double FlowNoise { get; set; }

        /// <summary>Gives the nominal device temperature in DegC</summary>
        public double NominalTemperatureInDegC { get; set; }

        /// <summary>Gives the temperature noise in DegC</summary>
        public double TemperatureNoiseInDegC { get; set; }

        /// <summary>Gives the nominal period that this MFC takes to "ramp" its slew limited setpoint from 0% to 100% as a TimeSpan.</summary>
        public TimeSpan FullScaleResponsePeriod { get; set; }

        /// <summary>Defines threshold setpoint value, as a percent of full scale flow, at or below which the MFC will act as if it has been asked to Open (requires both target and slew limited setpoint to be at this threshold)</summary>
        public double SetpointThresholdForCloseInPercentOfFS { get; set; }

        /// <summary>Defines threshold setpoint value, as a percent of full scale flow, at or above which the MFC will act as if it has been asked to Open (requires both target and slew limited setpoint to be at this threshold)</summary>
        public double SetpointThresholdForOpenInPercentOfFS { get; set; }

        public double ApplySlewLimit(double lastSlewLimitedValue, double target, double periodInSeconds)
        {
            if (FullScaleResponsePeriod <= TimeSpan.Zero)
                return target;

            double maxSlew = periodInSeconds * FullScaleFlow / FullScaleResponsePeriod.TotalSeconds;

            if (maxSlew >= Math.Abs(target - lastSlewLimitedValue))
                return target;

            if (target > lastSlewLimitedValue)
                return lastSlewLimitedValue + maxSlew;
            else
                return lastSlewLimitedValue - maxSlew;
        }
    }

    /// <summary>Used with <see cref="MFCSimScanEnginePlugin"/> as TOutputValueSetType</summary>
    public class MFCSimPluginOutputs
    {
        public double TrackingFlowSetpoint { get; set; }

        public double TrackingFlowSetpointInPercentOfFS { get; set; }

        public double MeasuredFlowInPercentOfFS { get; set; }

        public double MeasuredFlow { get; set; }

        public double ValvePositionInPercent { get; set; }

        public double TotalOperatingHours { get; set; }

        public double TemperatureInDegC { get; set; }

        public bool IsDeviceOnline { get; set; }

        /// <summary>
        /// Has same units as MeasuredFlow * time in units of minutes
        /// </summary>
        public double TotalFlow { get; set; }
    }
}

//-------------------------------------------------------------------
