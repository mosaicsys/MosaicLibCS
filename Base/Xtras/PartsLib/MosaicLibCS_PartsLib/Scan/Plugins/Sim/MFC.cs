//-------------------------------------------------------------------
/*! @file MassFlow.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved.
 * Copyright (c) 2015 Mosaic Systems Inc.  All rights reserved.
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

namespace MosaicLib.PartsLib.Scan.Plugin.Sim.MFC
{
    /// <summary>
    /// This ScanEngine Plugin provides very basic simulated fucntionality of an MFC.
    /// <para/>Supports following channels:
    /// <para/>Inputs:  FlowSetpointTarget, FlowSetpointTargetInPercentOfFS (last writer wins), ControlModeSetpoint (Close, Analog, Open, Freeze, Normal, Offline), IsOnline
    /// <para/>Outputs: TrackingFlowSetpoint, TrackingFlowSetpointInPercientOfFS, MeasuredFlowInPercentOfFS, MeasuredFlow, ValvePositionInPercent
    /// <para/>Supports the following config keys
    /// <para/>FullScaleFlow (1000.0), FlowNoise (5.0), FullScaleResponsePeriodInSeconds (1.25)
    /// </summary>
    public class MFCSimScanEnginePlugin : ScanEngine.ScanEnginePluginBase<MFCSimPluginConfig, MFCSimPluginInputs, MFCSimPluginOutputs>
    {
        public MFCSimScanEnginePlugin(string name) : this(name, null) { }
        public MFCSimScanEnginePlugin(string name, MFCSimPluginConfig config) 
            : base(name, config) 
        {
            if (ConfigValues.FullScaleFlow <= 0.0)
                ConfigValues.FullScaleFlow = 1.0;
        }

        public override void Setup(string scanEnginePartName)
        {
            base.Setup(scanEnginePartName);

            flowSetpointTargetInputIVA = Values.Instance.GetValueAccessor<double>("{0}.FlowSetpointTarget".CheckedFormat(Name)).Set(0.0);
            flowSetpointTargetInputInPercentOfFSIVA = Values.Instance.GetValueAccessor<double>("{0}.FlowSetpointTargetInPercentOfFS".CheckedFormat(Name)).Set(0.0);
        }

        protected IValueAccessor<double> flowSetpointTargetInputIVA;
        protected IValueAccessor<double> flowSetpointTargetInputInPercentOfFSIVA;
        protected Random rng = new Random();

        public override void UpdateInputs()
        {
            base.UpdateInputs();

            if (flowSetpointTargetInputIVA.IsUpdateNeeded)
            {
                OutputValues.LastWrittenFlowSetpointTarget = flowSetpointTargetInputIVA.Update().Value;
                flowSetpointTargetInputInPercentOfFSIVA.Set(ConfigValues.ConvertToPercentOfFS(OutputValues.LastWrittenFlowSetpointTarget));
                Logger.Debug.Emit("Setpoint changed to {0:f3} [{1:f2} %]", OutputValues.LastWrittenFlowSetpointTarget, flowSetpointTargetInputInPercentOfFSIVA.Value);
            }
            else if (flowSetpointTargetInputInPercentOfFSIVA.IsUpdateNeeded)
            {
                OutputValues.LastWrittenFlowSetpointTarget = ConfigValues.ConvertFromPercentToFlow(flowSetpointTargetInputInPercentOfFSIVA.Update().Value);
                flowSetpointTargetInputIVA.Set(OutputValues.LastWrittenFlowSetpointTarget);
                Logger.Debug.Emit("Setpoint changed to {0:f2} % [{1:f2}]", flowSetpointTargetInputInPercentOfFSIVA.Value, OutputValues.LastWrittenFlowSetpointTarget);
            }
        }

        public override void Service(TimeSpan measuredServiceInterval, QpcTimeStamp scanStartTime)
        {
            double periodInSeconds = measuredServiceInterval.TotalSeconds;

            double setpointTarget = OutputValues.LastWrittenFlowSetpointTarget;
            double setpointTargetInPercentFS = ConfigValues.ConvertToPercentOfFS(setpointTarget);
            double updatedTrackingSetpoint = ConfigValues.ApplySlewLimit(OutputValues.TrackingFlowSetpoint, setpointTarget, periodInSeconds);
            double trackingSetpointPercentFS = ConfigValues.ConvertToPercentOfFS(OutputValues.TrackingFlowSetpoint);

            bool isOnline = ((InputValues.IsOnline == null || InputValues.IsOnline == true) && InputValues.ControlModeSetpoint != "Offline");
            bool forceClosed = !isOnline || (InputValues.ControlModeSetpoint == "Close") || (InputValues.ControlModeSetpoint == "Analog");
            bool forceOpen = isOnline && (InputValues.ControlModeSetpoint == "Open");

            if (!forceClosed && !forceOpen)
            {
                if ((setpointTargetInPercentFS <= ConfigValues.SetpointThresholdForCloseInPercentOfFS) && (trackingSetpointPercentFS <= ConfigValues.SetpointThresholdForCloseInPercentOfFS))
                    forceClosed = true;
                else if ((setpointTargetInPercentFS >= ConfigValues.SetpointThresholdForOpenInPercentOfFS) && (trackingSetpointPercentFS >= ConfigValues.SetpointThresholdForOpenInPercentOfFS))
                    forceOpen = true;
            }

            bool normal = (!forceClosed && !forceOpen && (InputValues.ControlModeSetpoint != "Freeze"));

            if (forceClosed)
            {
                OutputValues.TrackingFlowSetpointInPercentOfFS = trackingSetpointPercentFS = 0.0;
                OutputValues.TrackingFlowSetpoint = ConfigValues.ConvertFromPercentToFlow(trackingSetpointPercentFS);
            }
            else if (forceOpen)
            {
                OutputValues.TrackingFlowSetpointInPercentOfFS = trackingSetpointPercentFS = 125.0;
                OutputValues.TrackingFlowSetpoint = ConfigValues.ConvertFromPercentToFlow(trackingSetpointPercentFS);
            }
            else if (normal)
            {
                // early calculations do not need to be updated
                OutputValues.TrackingFlowSetpoint = updatedTrackingSetpoint;
                OutputValues.TrackingFlowSetpointInPercentOfFS = ConfigValues.ConvertToPercentOfFS(updatedTrackingSetpoint);
            }
            else
            {
                // freeze the tracking setpoint (no more updates to the tracking setpoint).
                trackingSetpointPercentFS = OutputValues.TrackingFlowSetpointInPercentOfFS;
            }

            double flowNoise = (isOnline ? (ConfigValues.FlowNoise * (rng.NextDouble() * 2.0 - 1.0)) : 0.0);

            OutputValues.MeasuredFlow = OutputValues.TrackingFlowSetpoint + flowNoise;
            OutputValues.MeasuredFlowInPercentOfFS = ConfigValues.ConvertToPercentOfFS(OutputValues.MeasuredFlow);

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
    public class MFCSimPluginConfig : ICloneable
    {
        /// <summary>
        /// Default constructor.
        /// <para/>FullScaleFlow = 1000.0, FlowNoise = 5.0, FullScaleResponsePeriodInSeconds = 1.25, SetpointThresholdForCloseInPercentOfFS = 5.0, SetpointThresholdForOpenInPercentOfFS = 105.0
        /// </summary>
        public MFCSimPluginConfig()
        {
            FullScaleFlow = 1000.0;
            FlowNoise = 5.0;
            FullScaleResponsePeriodInSeconds = 1.25;
            SetpointThresholdForCloseInPercentOfFS = 5.0;
            SetpointThresholdForOpenInPercentOfFS = 105.0;
        }

        /// <summary>Gives the full scale flow for this MFC in user units.  Normally has units of sccm</summary>
        [ConfigItem]
        public double FullScaleFlow { get; set; }

        /// <summary>Gives the flow noise that this MFC will exhibit in user units.  This is the half width of the +- range.  Total range of noise will be twice this value.</summary>
        [ConfigItem]
        public double FlowNoise { get; set; }

        /// <summary>Gives the nominal period that this MFC takes to "ramp" its slew limited setpoint from 0% to 100% of full scale in units of seconds.</summary>
        [ConfigItem]
        public double FullScaleResponsePeriodInSeconds { get { return FullScaleResponsePeriod.TotalSeconds; } set { FullScaleResponsePeriod = TimeSpan.FromSeconds(value); } }

        /// <summary>Gives the nominal period that this MFC takes to "ramp" its slew limited setpoint from 0% to 100% as a TimeSpan.</summary>
        public TimeSpan FullScaleResponsePeriod { get; set; }

        /// <summary>Defines threshold setpoint value, as a percent of full scale flow, at or below which the MFC will act as if it has been asked to Open (requires both target and slew limited setpoint to be at this threshold)</summary>
        [ConfigItem]
        public double SetpointThresholdForCloseInPercentOfFS { get; set; }

        /// <summary>Defines threshold setpoint value, as a percent of full scale flow, at or above which the MFC will act as if it has been asked to Open (requires both target and slew limited setpoint to be at this threshold)</summary>
        [ConfigItem]
        public double SetpointThresholdForOpenInPercentOfFS { get; set; }

        public double ConvertFromPercentToFlow(double valueInPercent)
        {
            return (valueInPercent * 0.01) * FullScaleFlow;
        }

        public double ConvertToPercentOfFS(double value)
        {
            return (value / FullScaleFlow) * 100.0;
        }

        public double ApplySlewLimit(double lastSlewLimitedValue, double target, double periodInSeconds)
        {
            if (FullScaleResponsePeriodInSeconds <= 0.0)
                return target;

            double maxSlew = periodInSeconds * FullScaleFlow / FullScaleResponsePeriodInSeconds;

            if (maxSlew >= Math.Abs(target - lastSlewLimitedValue))
                return target;

            if (target > lastSlewLimitedValue)
                return lastSlewLimitedValue + maxSlew;
            else
                return lastSlewLimitedValue - maxSlew;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    /// <summary>Used with <see cref="MFCSimScanEnginePlugin"/> as TInputValueSetType</summary>
    public class MFCSimPluginInputs
    {
        [ValueSetItem]
        public string ControlModeSetpoint { get; set; }

        [ValueSetItem]
        public bool? IsOnline { get; set; }

        public MFCSimPluginInputs()
        {
            IsOnline = true;
        }
    }

    /// <summary>Used with <see cref="MFCSimScanEnginePlugin"/> as TOutputValueSetType</summary>
    public class MFCSimPluginOutputs
    {
        public double LastWrittenFlowSetpointTarget { get; set; }

        [ValueSetItem]
        public double TrackingFlowSetpoint { get; set; }

        [ValueSetItem]
        public double TrackingFlowSetpointInPercentOfFS { get; set; }

        [ValueSetItem]
        public double MeasuredFlowInPercentOfFS { get; set; }

        [ValueSetItem]
        public double MeasuredFlow { get; set; }

        [ValueSetItem]
        public double ValvePositionInPercent { get; set; }
    }
}

//-------------------------------------------------------------------
