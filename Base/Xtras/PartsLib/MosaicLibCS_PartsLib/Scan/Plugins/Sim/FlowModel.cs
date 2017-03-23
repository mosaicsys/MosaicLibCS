//-------------------------------------------------------------------
/*! @file Vacuum.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2017 Mosaic Systems Inc.
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
using System.Collections.Generic;
using System.Linq;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.PartsLib.Common.Physics;
using MosaicLib.PartsLib.Common.Physics.UnitsOfMeasure;
using MosaicLib.PartsLib.Helpers;
using MosaicLib.PartsLib.Scan.Plugin.Sim.Common;
using MosaicLib.PartsLib.Scan.Plugin.Sim.FlowModel.Components;
using MosaicLib.Time;
using MosaicLib.Utils;

using Physics = MosaicLib.PartsLib.Common.Physics;

namespace MosaicLib.PartsLib.Scan.Plugin.Sim.FlowModel
{
    namespace Components
    {
        #region Pipe

        public class Pipe : FlowModel.NodePairBase
        {
            public Pipe(string name)
                : base(name, Fcns.CurrentClassLeafName)
            { }
        }

        #endregion

        #region Valve, RotaryValve, ValveConfig

        public class RotaryValve : Valve
        {
            public RotaryValve(string name, ValveConfig valveConfig)
                : base(name, valveConfig, Fcns.CurrentClassLeafName)
            { }

            public override double EffectiveAreaM2 { get { return Math.Sin(EffectivePercentOpen * 0.01 * Math.PI * 0.5) * base.EffectiveAreaM2; } }
        }

        public class Valve : FlowModel.NodePairBase
        {
            public Valve(string name, ValveConfig valveConfig)
                : this(name, valveConfig, Fcns.CurrentClassLeafName)
            { }

            public Valve(string name, ValveConfig valveConfig, string toStringComponentTypeStr)
                : base(name, toStringComponentTypeStr)
            {
                Config = new ValveConfig(valveConfig);
                RadiusInM = Config.RadiusInMM * 0.001;
                LengthInM = Config.LengthInMM * 0.001;
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;

                actuator = new Helpers.ActuatorBase(new ActuatorConfig() { Name = "{0}.a".CheckedFormat(name), InitialPos = ActuatorPosition.AtPos1, Motion1To2Time = Config.TimeToOpen, Motion2To1Time = Config.TimeToClose, Pos1Name = "Closed", Pos2Name = "Opened" });

                if (Config.InitialValveRequest != ValveRequest.None)
                    ValveRequest = Config.InitialValveRequest;

                actuatorState = actuator.State;
            }

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                base.Service(servicePhase, measuredServiceInterval, timestampNow);

                if (actuator.Motion1To2Time != Config.TimeToOpen)
                    actuator.Motion1To2Time = Config.TimeToOpen;
                if (actuator.Motion2To1Time != Config.TimeToClose)
                    actuator.Motion2To1Time = Config.TimeToClose;

                actuator.Service(measuredServiceInterval);
                actuatorState = actuator.State;
            }

            public ValveConfig Config { get; private set; }
            private ActuatorBase actuator;
            private IActuatorState actuatorState;

            public double PercentOpen { get { return actuatorState.PositionInPercent; } }
            public double PercentOpenSetpoint 
            { 
                get { return actuatorState.TargetPositionInPercent; } 
                set { SetValveRequest(positionInPercentIn: value); }
            }

            public double EffectivePercentOpen { get { return Math.Max(PercentOpen, Config.MinimumPercentOpen); } }
            public override double EffectiveAreaM2 { get { return EffectivePercentOpen * 0.01 * base.EffectiveAreaM2; } }

            public bool IsOpened { get { return actuatorState.IsAtPos2; } }
            public bool IsClosed { get { return actuatorState.IsAtPos1; } }

            public bool Open { get { return actuatorState.TargetPos.IsAtPos2(); } set { if (value) ValveRequest = ValveRequest.Open; } }
            public bool Close { get { return actuatorState.TargetPos.IsAtPos1(); } set { if (value) ValveRequest = ValveRequest.Close; } }

            public ValveRequest ValveRequest
            {
                get
                {
                    switch (actuatorState.TargetPos)
                    {
                        case ActuatorPosition.MoveToPos1: return ValveRequest.Close;
                        case ActuatorPosition.MoveToPos2: return ValveRequest.Open;
                        case ActuatorPosition.InBetween: return ValveRequest.InBetween;
                        case ActuatorPosition.None: return ValveRequest.None;
                        default: return ValveRequest.Invalid;
                    }
                }
                set { SetValveRequest(valveRequestIn: value); }
            }

            /// <summary>
            /// Supported values:
            /// <para/>ForcePositionInPercent=value
            /// <para/>None, Close, Open, InBetween
            /// </summary>
            public string FaultInjection
            {
                get { return _faultInjection; }
                set 
                {
                    string temp = value.MapNullToEmpty();
                    if (_faultInjection != temp)
                    {
                        StringScanner ss = new StringScanner(_faultInjection = temp);

                        valveRequestOverride = ValveRequest.None;

                        if (ss.ParseXmlAttribute("ForcePositionInPercent", out forcePositionInPercent))
                            valveRequestOverride = ConvertPositionToValveRequest(forcePositionInPercent);
                        else
                            valveRequestOverride = ss.ParseValue(ValveRequest.None);

                        SetValveRequest();
                    }
                }
            }
            private string _faultInjection = string.Empty;

            private ValveRequest valveRequestOverride = ValveRequest.None;
            private double forcePositionInPercent;
            private ValveRequest? lastExplicitValveRequest = null;
            private double? lastExplicitPositionRequestInPercent = null;

            public void SetValveRequest(ValveRequest? valveRequestIn = null, double? positionInPercentIn = null)
            {
                ValveRequest valveRequest = (valveRequestIn ?? lastExplicitValveRequest).GetValueOrDefault(ValveRequest.None);
                double positionInPercent = (positionInPercentIn ?? lastExplicitPositionRequestInPercent).GetValueOrDefault(0.0);

                if (valveRequestOverride != ValveRequest.None)
                {
                    valveRequest = valveRequestOverride;
                    positionInPercent = forcePositionInPercent;
                }
                else if (valveRequestIn == null)
                {
                    valveRequest = ConvertPositionToValveRequest(positionInPercent);
                }
                else if (valveRequest == ValveRequest.InBetween && positionInPercentIn == null)
                {
                    positionInPercent = Config.DefaultInBetweenPositionInPercent;
                }

                switch (valveRequest)
                {
                    case ValveRequest.Open: actuator.SetTarget(Helpers.ActuatorPosition.AtPos2); break;
                    case ValveRequest.Close: actuator.SetTarget(Helpers.ActuatorPosition.AtPos1); break;
                    case ValveRequest.InBetween: actuator.SetTarget(Helpers.ActuatorPosition.InBetween, positionInPercent); break;
                    case ValveRequest.None:
                    default: break;
                }

                if (valveRequestIn != null)
                {
                    lastExplicitValveRequest = valveRequestIn;
                    lastExplicitPositionRequestInPercent = positionInPercentIn;
                }
                else if (positionInPercentIn != null)
                {
                    lastExplicitPositionRequestInPercent = positionInPercentIn;
                    lastExplicitValveRequest = null;
                }

                actuator.Service(TimeSpan.Zero);
                actuatorState = actuator.State;
            }

            public static ValveRequest ConvertPositionToValveRequest(double positionInPercent)
            {
                if (positionInPercent >= 100.0)
                    return ValveRequest.Open;
                else if (positionInPercent <= 0.0)
                    return ValveRequest.Close;
                else
                    return ValveRequest.InBetween;
            }

            public override string ToString()
            {
                return "{0} {1}".CheckedFormat(base.ToString(), actuatorState);
            }
        }

        /// <summary>
        /// None(0), Close(1), Open(2), InBetween(3), Invalid(4)
        /// </summary>
        public enum ValveRequest : int
        {
            None = 0,
            Close = 1,
            Open = 2,
            InBetween = 3,
            Invalid = 4,
        }

        public class ValveConfig
        {
            /// <summary>
            /// Default constructor:
            /// <para/>DefaultInBetweenPositionInPercent = 50.0, RadiusInMM = 0.0, LengthInMM = 0.0, TimeToOpen = TimeSpan.Zero, TimeToClose = TimeSpan.Zero, MinimumPercentOpen = 0.0
            /// </summary>
            public ValveConfig()
            {
                DefaultInBetweenPositionInPercent = 50.0;
            }

            /// <summary>
            /// Copy constructor used to build ValveConfig values and used to copy config for use by each individual valve instance (so that they do not share ValveConfig instances and/or side effects produced when changing ValveConfig contents after construction)
            /// </summary>
            public ValveConfig(ValveConfig other)
            {
                RadiusInMM = other.RadiusInMM;
                LengthInMM = other.LengthInMM;
                InitialValveRequest = other.InitialValveRequest;
                TimeToOpen = other.TimeToOpen;
                TimeToClose = other.TimeToClose;
                DefaultInBetweenPositionInPercent = other.DefaultInBetweenPositionInPercent;
                MinimumPercentOpen = other.MinimumPercentOpen;
            }

            /// <summary>Used during construction of a valve to initialize its corresponding parameter</summary>
            public double RadiusInMM { get; set; }
            /// <summary>Used during construction of a valve to initialize its corresponding parameter</summary>
            public double LengthInMM { get; set; }

            /// <summary>Gives the initial ValueRequest value for the valve at construction time</summary>
            public ValveRequest InitialValveRequest { get; set; }

            /// <summary>Defines the time required to move from fully closed to fully open.  Partial moves require proportionally less time</summary>
            public TimeSpan TimeToOpen { get; set; }
            /// <summary>Defines the time required to move from fully open to fully closed.  Partial moves require proportionally less time</summary>
            public TimeSpan TimeToClose { get; set; }
            /// <summary>Set only.  Sets both TimeToOpen and TimeToClose to the given value</summary>
            public TimeSpan TimeToOpenAndClose { set { TimeToOpen = TimeToClose = value; } }

            /// <summary>Defines the position in percent that will be used if the valve is simply set to be InBetween</summary>
            public double DefaultInBetweenPositionInPercent { get; set; }

            /// <summary>
            /// When non-zero this value prevents the effective valve area from going below a corresponding minimum value even when the valve is fully closed.  
            /// Typically this numbrer should be 0.0 for valves that can fully close or a small number for valves that do not actually seal when closed (butterfly valves, ...)
            /// </summary>
            public double MinimumPercentOpen { get; set; }

            /// <summary>Uses constructor defaults and sets TimeToOpen to 0.3 seconds adn TimeToClose to 0.5 seconds</summary>
            public static ValveConfig Default { get { return new ValveConfig() { TimeToOpen = (0.300).FromSeconds(), TimeToClose = (0.500).FromSeconds() }; } }
            /// <summary>Uses constructor defaults and sets TimeToOpen to 0.1 seconds and TimeToClose to 0.1 seconds</summary>
            public static ValveConfig Fast { get { return new ValveConfig() { TimeToOpenAndClose = (0.1).FromSeconds() }; } }
            /// <summary>Uses constructor defaults and sets TimeToOpen to 0.0 seconds and TimeToClose to 0.0 seconds</summary>
            public static ValveConfig Instant { get { return new ValveConfig() { TimeToOpenAndClose = TimeSpan.Zero }; } }
        }

        #endregion

        #region BistableValve, BistableValveConfig

        /// <summary>
        /// A bistableValve is a type of pressure triggered two position valve.
        /// It has two states: State1 and State2 and always defaults to being in State1 
        /// </summary>
        public class BistableValve : FlowModel.NodePairBase
        {
            public BistableValve(string name, BistableValveConfig valveConfig)
                : this(name, valveConfig, Fcns.CurrentClassLeafName)
            { }

            public BistableValve(string name, BistableValveConfig valveConfig, string toStringComponentTypeStr)
                : base(name, toStringComponentTypeStr)
            {
                Config = new BistableValveConfig(valveConfig);
                RadiusInM = Config.RadiusInMM * 0.001;
                LengthInM = Config.LengthInMM * 0.001;
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;

                ValvePosition = BistableValvePossition.State1;
            }

            public BistableValveConfig Config { get; private set; }

            public BistableValvePossition ValvePosition { get; private set; }

            public double PercentOpenSetpoint { get { return (forcedValvePosition.MapDefaultTo(ValvePosition) == BistableValvePossition.State2 ? Config.State2PercentOpen : Config.State1PercentOpen); } }

            public double EffectivePercentOpen { get { return PercentOpenSetpoint; } }
            public override double EffectiveAreaM2 { get { return EffectivePercentOpen * 0.01 * base.EffectiveAreaM2; } }

            /// <summary>
            /// Supported values:
            /// <para/>ForcePositionInPercent=value
            /// <para/>None, Close, Open, InBetween
            /// </summary>
            public string FaultInjection
            {
                get { return _faultInjection; }
                set
                {
                    string temp = value.MapNullToEmpty();
                    if (_faultInjection != temp)
                    {
                        StringScanner ss = new StringScanner(_faultInjection = temp);

                        forcedValvePosition = ss.ParseValue(BistableValvePossition.None);

                        Service(ServicePhases.FaultInjection, TimeSpan.Zero, QpcTimeStamp.Now);
                    }
                }
            }
            private string _faultInjection = string.Empty;

            private BistableValvePossition forcedValvePosition = BistableValvePossition.None;

            private BistableValvePossition pendingValvePosition;
            private TimeSpan pendingDeboundTimeRemaining;       // note use of explicit elapsed time calculation so that this logic can work unit tests where time is simulated.

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                base.Service(servicePhase, measuredServiceInterval, timestampNow);

                double pressureDeltaInKPa = End1.PressureInKPa - End2.PressureInKPa;

                if (pressureDeltaInKPa < Config.state1PressureThresholdInKPa && pendingValvePosition != BistableValvePossition.State1)
                {
                    pendingValvePosition = BistableValvePossition.State1;
                    pendingDeboundTimeRemaining = Config.DebouncePeriod;
                }
                else if (pressureDeltaInKPa > Config.state2PressureThresholdInKPa && pendingValvePosition != BistableValvePossition.State2)
                {
                    pendingValvePosition = BistableValvePossition.State2;
                    pendingDeboundTimeRemaining = Config.DebouncePeriod;
                }
                else if (ValvePosition != pendingValvePosition)
                {
                    if (pendingDeboundTimeRemaining > measuredServiceInterval)
                        pendingDeboundTimeRemaining -= measuredServiceInterval;
                    else
                    {
                        pendingDeboundTimeRemaining = TimeSpan.Zero;
                        ValvePosition = pendingValvePosition;
                    }
                }
            }

            public override string ToString()
            {
                return "{0} {1} {2:f2} % Open".CheckedFormat(base.ToString(), ValvePosition, PercentOpenSetpoint);
            }
        }

        /// <summary>
        /// None(0), State1(1), State2(2)
        /// </summary>
        public enum BistableValvePossition : int
        {
            None = 0,
            State1 = 1,
            State2 = 2,
        }

        public class BistableValveConfig
        {
            /// <summary>
            /// Default constructor:
            /// <para/>PressureUnits = kPam
            /// </summary>
            public BistableValveConfig()
            {
                PressureUnits = PressureUnits.kilopascals;
            }

            /// <summary>
            /// Copy constructor used to build BistableValveConfig values and used to copy config for use by each individual valve instance (so that they do not share BistableValveConfig instances and/or side effects produced when changing BistableValveConfig contents after construction)
            /// </summary>
            public BistableValveConfig(BistableValveConfig other)
            {
                RadiusInMM = other.RadiusInMM;
                LengthInMM = other.LengthInMM;
                PressureUnits = other.PressureUnits;
                state1PressureThresholdInKPa = other.state1PressureThresholdInKPa;
                state2PressureThresholdInKPa = other.state2PressureThresholdInKPa;
                State1PercentOpen = other.State1PercentOpen;
                State2PercentOpen = other.State2PercentOpen;
                DebouncePeriod = other.DebouncePeriod;
            }

            /// <summary>Used during construction of a valve to initialize its corresponding parameter</summary>
            public double RadiusInMM { get; set; }
            /// <summary>Used during construction of a valve to initialize its corresponding parameter</summary>
            public double LengthInMM { get; set; }

            /// <summary>Defines the pressure units used for the two threshold pressure values.  Defaults to kPa</summary>
            public PressureUnits PressureUnits { get; set; }

            /// <summary>Defines the threshold pressure for (End1 - End2) below which the state transitions to State 1</summary>
            public double State1PressureThreshold { get { return PressureUnits.ConvertFromKPA(state1PressureThresholdInKPa); } set { state1PressureThresholdInKPa = PressureUnits.ConvertToKPa(value); } }

            /// <summary>Defines the threshold pressure for (End1 - End2) above which the state transitions to State 2</summary>
            public double State2PressureThreshold { get { return PressureUnits.ConvertFromKPA(state2PressureThresholdInKPa); } set { state2PressureThresholdInKPa = PressureUnits.ConvertToKPa(value); } }

            public double State1PercentOpen { get; set; }
            public double State2PercentOpen { get; set; }

            internal double state1PressureThresholdInKPa, state2PressureThresholdInKPa;

            public TimeSpan DebouncePeriod { get; set; }

            /// <summary>Atmospheric Pop Open Valve.  "Opens" if End1-End2 &gt; 10.0 torr, "Closes" if End1-End2 &lt; 1.0 torr.  PressureUnits = torr, DebouncePeriod = 0.2 seconds</summary>
            public static BistableValveConfig AtmPopOpenValve { get { return new BistableValveConfig() { PressureUnits = PressureUnits.torr, State1PressureThreshold = 1.0, State1PercentOpen = 0.0, State2PressureThreshold = 10.0, State2PercentOpen = 100.0, DebouncePeriod = (0.2).FromSeconds() }; } }
        }
        
        #endregion

        #region Gauge, GaugeConfig

        public class Gauge : FlowModel.ComponentBase
        {
            public Gauge(string name, GaugeConfig config, Chamber chamber)
                : this(name, config, chamber.EffectiveNode)
            { }

            public Gauge(string name, GaugeConfig config, FlowModel.Node observesNode)
                : base(name, Fcns.CurrentClassLeafName)
            {
                ObservesNode = observesNode;
                Config = new GaugeConfig(config);
                ServicePhases = ServicePhases.AfterRelaxation | ServicePhases.AfterSetup;

                GaugeMode = Config.InitialGaugeMode.MapDefaultTo(GaugeMode.AllwaysOn);
            }

            /// <summary>Shows the Node that this Gauge is reporting on (from which the gauge gets the flow or pressure)</summary>
            public FlowModel.Node ObservesNode { get; private set; }

            /// <summary>Set to a clone of the value given to the constructor.  Cloned instance, to which the client has access using this reference propery, Contains all of the settings that are used by this Gauge instance.</summary>
            public GaugeConfig Config { get; private set; }

            /// <summary>Gives the most recently observed pressure value in client specified PressureUnits.  Will have noise added if NoiseLevelInPercent is above zero.</summary>
            public double Value { get { return Config.ConvertValueUOM(servicedValueStdUnits, outbound: true); } }

            /// <summary>Gives the most recently observed differential pressure value (Pressure - Config.DifferentialReferencePPressure) in client specified PressureUnits.  Will have noise added if either Config.noise related parameter is above zero.</summary>
            public double DifferentialValue { get { return Config.ConvertValueUOM(serviceDifferentialValueStdUnits, outbound: true); } }

            /// <summary>Returns true if the last raw reading was outside of the stated min..max reading range.</summary>
            public bool ReadingOutOfRange { get; private set; }

            /// <summary>Returns true when the sensor has detected a fault condition (ReadingOutOfRange, FaultInjection, ...).</summary>
            public bool SensorFault { get; private set; }

            /// <summary>controls/indicates the current intended operating mode of this sensor (AlwaysOn, On, Off)</summary>
            public GaugeMode GaugeMode { get; set; }

            public bool IsOn { get { return (GaugeMode == GaugeMode.On || GaugeMode == GaugeMode.AllwaysOn); } set { if (value) SetGaugeMode(GaugeMode.On); } }
            public bool IsOff { get { return (GaugeMode == GaugeMode.Off || GaugeMode == GaugeMode.None); } set { if (value) SetGaugeMode(GaugeMode.Off); } }

            /// <summary>
            /// Knowns values: Off, SensorFault
            /// </summary>
            /// <remarks>Expect to add ForceValue=value</remarks>
            public string FaultInjection
            {
                get { return _faultInjection; }
                set
                {
                    string temp = value.MapNullToEmpty();
                    if (_faultInjection != temp)
                    {
                        StringScanner ss = new StringScanner(_faultInjection = temp);

                        forceSensorFault = false;

                        while (!ss.IsAtEnd)
                        {
                            if (ss.MatchToken("Off"))
                                GaugeMode = GaugeMode.Off;
                            else if (ss.MatchToken("SensorFault"))
                                forceSensorFault = true;
                            else
                                break;
                        }

                        Service(ServicePhases.FaultInjection, TimeSpan.Zero, QpcTimeStamp.Now);
                    }
                }
            }
            private string _faultInjection = string.Empty;

            private bool forceSensorFault = false;

            private System.Random rng = new Random();

            private double servicedValueStdUnits;
            private double serviceDifferentialValueStdUnits;
            private bool gaugeWasForcedOff = false;

            public void SetGaugeMode(GaugeMode gaugeMode)
            {
                if (GaugeMode == GaugeMode.AllwaysOn)
                    return;

                GaugeMode = gaugeMode;
                gaugeWasForcedOff = false;
            }

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                double rawValueInStdUnits = 0.0;

                switch (Config.GaugeType)
                {
                    case GaugeType.Pressure: rawValueInStdUnits = ObservesNode.PressureInKPa; break;
                    case GaugeType.VolumetricFlow: rawValueInStdUnits = ObservesNode.VolumetricFlowOutOfNodeInSCMS; break;
                    default: break;
                }

                bool gaugeIsOn = (GaugeMode == GaugeMode.AllwaysOn || GaugeMode == GaugeMode.On);

                if (GaugeMode == GaugeMode.On && (rawValueInStdUnits > Config.maximumValueInStdUnits && Config.GaugeBehavior.IsSet(GaugeBehavior.TurnGaugeOffAboveMax)))
                {
                    GaugeMode = GaugeMode.Off;
                    gaugeIsOn = false;
                    gaugeWasForcedOff = true;
                }

                if (!gaugeIsOn)
                    rawValueInStdUnits = Config.offValueInStdUnits;

                double clippedValueInStdUnits = rawValueInStdUnits;

                ReadingOutOfRange = (gaugeIsOn && !rawValueInStdUnits.IsInRange(Config.minimumValueInStdUnits, Config.maximumValueInStdUnits));
                if (ReadingOutOfRange)
                    clippedValueInStdUnits = rawValueInStdUnits.Clip(Config.minimumValueInStdUnits, Config.maximumValueInStdUnits);

                double rngValueM1P1 = ((rng.NextDouble() * 2.0) - 1.0);            // NextDouble gives back a value between 0.0 (inclusive) and 1.0 (inclusive).  This equasion converts that to -1.0 to +1.0
                double rPercent = rngValueM1P1 * Config.noiseLevelInPercentOfCurrentValue;    // a number from -noiseLevelInPercent to +noiseLevelInPercent
                double rGeometricNoiseGain = (100.0 + rPercent) * 0.01;            // a number from 1.0-rp  to 1.0+rp  rp is clipped to be no larger than 0.5
                double clippedValueWithNoiseInStdUnits = clippedValueInStdUnits * rGeometricNoiseGain + (rngValueM1P1 * Config.NoiseLevelBase); // add in multaplicative noise and base noise 

                servicedValueStdUnits = (gaugeIsOn ? clippedValueWithNoiseInStdUnits : Config.offValueInStdUnits);
                serviceDifferentialValueStdUnits = (gaugeIsOn ? (servicedValueStdUnits - Config.differentialReferenceValueInStdUnits).Clip(Config.minimumDifferentialValueInStdUnits, Config.maximumDifferentialValueInStdUnits) : Config.offDifferentialValueInStdUnits);

                SensorFault = forceSensorFault || (gaugeWasForcedOff && Config.GaugeBehavior.IsSet(GaugeBehavior.SensorFaultWhenGaugeForcedOff));
                if (ReadingOutOfRange)
                {
                    SensorFault |= ((rawValueInStdUnits > Config.maximumValueInStdUnits && Config.GaugeBehavior.IsSet(GaugeBehavior.SensorFaultAboveMax))
                                    || (rawValueInStdUnits < Config.minimumValueInStdUnits && Config.GaugeBehavior.IsSet(GaugeBehavior.SensorFaultBelowMin)));
                }
            }

            public override string ToString()
            {
                switch (Config.GaugeType)
                {
                    case GaugeType.Pressure: return "{0} '{1}' uom:{2} pressure:{3:f6} diff:{4:f6} ref:{5:f6} {6}".CheckedFormat(ToStringComponentTypeStr, Name, Config.PressureUnits, Value, DifferentialValue, Config.DifferentialReferenceValue, GaugeMode);
                    case GaugeType.VolumetricFlow: return "{0} '{1}' uom:{2} flow:{3:f6} diff:{4:f6} ref:{5:f6} {6}".CheckedFormat(ToStringComponentTypeStr, Name, Config.VolumetricFlowUnits, Value, DifferentialValue, Config.DifferentialReferenceValue, GaugeMode);
                    default: return "{0} '{1}' Invalid gauge type {2}".CheckedFormat(ToStringComponentTypeStr, Name, Config.GaugeType);
                }
            }
        }

        /// <summary>
        /// Pressure (0), VolumetricFlow (1)
        /// </summary>
        public enum GaugeType : int
        {
            Pressure = 0,
            VolumetricFlow = 1,
        }

        /// <summary>
        /// None (0 placeholder), AllwaysOn (1), Off (2), On (3)
        /// </summary>
        public enum GaugeMode : int
        {
            None = 0,
            AllwaysOn = 1,
            Off = 2,
            On = 3,
        }

        [Flags]
        public enum GaugeBehavior : int
        {
            None = 0x00,
            SensorFaultAboveMax = 0x01,
            SensorFaultBelowMin = 0x02,
            TurnGaugeOffAboveMax = 0x04,
            SensorFaultWhenGaugeForcedOff = 0x08,
        }

        public class GaugeConfig
        {
            public GaugeConfig(GaugeType gaugeType = GaugeType.Pressure)
            {
                GaugeType = gaugeType;
                GaugeBehavior = GaugeBehavior.None;
                PressureUnits = PressureUnits.kilopascals;
                VolumetricFlowUnits = VolumetricFlowUnits.sccm;

                switch (GaugeType)
                {
                    case GaugeType.Pressure:
                        minimumValueInStdUnits = 0.0;
                        maximumValueInStdUnits = double.PositiveInfinity;
                        differentialReferenceValueInStdUnits = Physics.UnitsOfMeasure.Constants.StdAtmInKPa;
                        break;

                    case GaugeType.VolumetricFlow:
                        minimumValueInStdUnits = double.NegativeInfinity;
                        maximumValueInStdUnits = double.PositiveInfinity;
                        differentialReferenceValueInStdUnits = 0.0;
                        break;

                    default:
                        throw new System.ArgumentException("gaugeType");
                }

                minimumDifferentialValueInStdUnits = double.NegativeInfinity;
                maximumDifferentialValueInStdUnits = double.PositiveInfinity;
            }

            public GaugeConfig(GaugeConfig other)
            {
                GaugeType = other.GaugeType;
                GaugeBehavior = other.GaugeBehavior;
                InitialGaugeMode = other.InitialGaugeMode;
                PressureUnits = other.PressureUnits;
                VolumetricFlowUnits = other.VolumetricFlowUnits;

                differentialReferenceValueInStdUnits = other.differentialReferenceValueInStdUnits;
                offValueInStdUnits = other.offValueInStdUnits;
                offDifferentialValueInStdUnits = other.offDifferentialValueInStdUnits;
                minimumValueInStdUnits = other.minimumValueInStdUnits;
                maximumValueInStdUnits = other.maximumValueInStdUnits;
                minimumDifferentialValueInStdUnits = other.minimumDifferentialValueInStdUnits;
                maximumDifferentialValueInStdUnits = other.maximumDifferentialValueInStdUnits;

                noiseLevelInPercentOfCurrentValue = other.noiseLevelInPercentOfCurrentValue;
                NoiseLevelBase = other.NoiseLevelBase;
            }

            /// <summary>Shows the type of gauge that has been constructued (to measure Pressure or VolumetricFlow)</summary>
            public GaugeType GaugeType { get; private set; }

            /// <summary>Defines the desired gauge behavior</summary>
            public GaugeBehavior GaugeBehavior { get; set; }

            /// <summary>Defines the initial GaugeMode</summary>
            public GaugeMode InitialGaugeMode { get; set; }

            /// <summary>Gives the PressureUnits that this gauge "reads" in (for pressure gauges)</summary>
            public PressureUnits PressureUnits { get; set; }

            /// <summary>Gives the VolumetricFlowUnits that this gauge "reads" in (for flow gauges)</summary>
            public VolumetricFlowUnits VolumetricFlowUnits { get; set; }

            /// <summary>Gives the reference value that is substracted from current value to produce the DifferentialPressure value.</summary>
            public double DifferentialReferenceValue { get { return ConvertValueUOM(differentialReferenceValueInStdUnits, outbound: true); } set { differentialReferenceValueInStdUnits = ConvertValueUOM(value, inbound: true); } }

            /// <summary>Gives the reference value that will be reported when the gauge is off.</summary>
            public double OffValue { get { return ConvertValueUOM(offValueInStdUnits, outbound: true); } set { offValueInStdUnits = ConvertValueUOM(value, inbound: true); } }

            /// <summary>Gives the reference value that will be reported when the gauge is off.</summary>
            public double OffDifferentialValue { get { return ConvertValueUOM(offDifferentialValueInStdUnits, outbound: true); } set { offDifferentialValueInStdUnits = ConvertValueUOM(value, inbound: true); } }

            /// <summary>Defines the minimum value that the gauge can read in client specified PressureUnits/VolumetricFlowUnits.</summary>
            public double MinimumValue { get { return PressureUnits.ConvertFromKPA(minimumValueInStdUnits); } set { minimumValueInStdUnits = ConvertValueUOM(value, inbound: true); } }

            /// <summary>Defines the maximum value that the gauge can read in client specified PressureUnits/VolumetricFlowUnits.</summary>
            public double MaximumValue { get { return PressureUnits.ConvertFromKPA(maximumValueInStdUnits); } set { maximumValueInStdUnits = ConvertValueUOM(value, inbound: true); } }

            /// <summary>Defines the minimum differential value that the gauge can read in client specified PressureUnits/VolumetricFlowUnits.</summary>
            public double MinimumDifferentialValue { get { return ConvertValueUOM(minimumDifferentialValueInStdUnits, outbound: true); } set { minimumDifferentialValueInStdUnits = ConvertValueUOM(value, inbound: true); } }

            /// <summary>Defines the maximum differential value that the gauge can read in client specified PressureUnits/VolumetricFlowUnits.</summary>
            public double MaximumDifferentialValue { get { return ConvertValueUOM(maximumDifferentialValueInStdUnits, outbound: true); } set { maximumDifferentialValueInStdUnits = ConvertValueUOM(value, inbound: true); } }

            /// <summary>Get/Set value gives the geometric level of noise for this reading (most useful when emulating sensors that are essentially logarithmic).  At value of 2.0 gives noise of value * fraction randomly choosen between (0.98 and 1.02)</summary>
            public double NoiseLevelInPercentOfCurrentValue
            {
                get { return noiseLevelInPercentOfCurrentValue; }
                set { noiseLevelInPercentOfCurrentValue = value.Clip(0.0, 50.0, 0.0); }
            }
            /// <summary>Get/Set value gives the base level of noise that is added to the reading in the client selected PressureUnits or VolumetricFlow units (as approrpiate)</summary>
            public double NoiseLevelBase { get; set; }

            internal double differentialReferenceValueInStdUnits;
            internal double offValueInStdUnits, offDifferentialValueInStdUnits;
            internal double noiseLevelInPercentOfCurrentValue;
            internal double minimumValueInStdUnits, maximumValueInStdUnits;
            internal double minimumDifferentialValueInStdUnits, maximumDifferentialValueInStdUnits;

            /// <summary>Used to convert the gauge reading to or from internal standard units (kPa or SCMS)</summary>
            public double ConvertValueUOM(double value, bool inbound = false, bool outbound = false)
            {
                switch (GaugeType)
                {
                    case GaugeType.Pressure: return (outbound ? PressureUnits.ConvertFromKPA(value) : inbound ? PressureUnits.ConvertToKPa(value) : 0.0);
                    case GaugeType.VolumetricFlow: return (outbound ? VolumetricFlowUnits.ConvertFromSCMS(value) : inbound ? VolumetricFlowUnits.ConvertToSCMS(value) : 0.0);
                    default: return 0.0;
                }
            }

            public static GaugeConfig IonGaugeConfig { get { return new GaugeConfig(GaugeType.Pressure) { InitialGaugeMode = GaugeMode.Off, PressureUnits = PressureUnits.torr, GaugeBehavior = GaugeBehavior.TurnGaugeOffAboveMax | GaugeBehavior.SensorFaultWhenGaugeForcedOff, MaximumValue = 1.0e-3, MinimumValue = 1.0e-10, MaximumDifferentialValue = 0.0, MinimumDifferentialValue = 0.0, DifferentialReferenceValue = 0.0 }; } }
            public static GaugeConfig ConvectronGaugeConfig { get { return new GaugeConfig(GaugeType.Pressure) { InitialGaugeMode = GaugeMode.AllwaysOn, PressureUnits = PressureUnits.torr, GaugeBehavior = GaugeBehavior.SensorFaultAboveMax, MaximumValue = 1000.0, MinimumValue = 1.0e-4, MaximumDifferentialValue = 100.0, MinimumDifferentialValue = -100.0, DifferentialReferenceValue = 760.0 }; } }
            public static GaugeConfig Baratron10TorrGaugeConfig { get { return new GaugeConfig(GaugeType.Pressure) { InitialGaugeMode = GaugeMode.AllwaysOn, PressureUnits = PressureUnits.torr, MaximumValue = 10.0, MinimumValue = 0.0, MaximumDifferentialValue = 10.0, MinimumDifferentialValue = -10.0, DifferentialReferenceValue = 0.0 }; } }
            public static GaugeConfig Baratron1TorrGaugeConfig { get { return new GaugeConfig(GaugeType.Pressure) { InitialGaugeMode = GaugeMode.AllwaysOn, PressureUnits = PressureUnits.torr, MaximumValue = 1.0, MinimumValue = 0.0, MaximumDifferentialValue = 1.0, MinimumDifferentialValue = -1.0, DifferentialReferenceValue = 0.0 }; } }
        }

        #endregion

        #region Controller, ControllerConfig

        public class Controller : FlowModel.ComponentBase
        {
            public Controller(string name, ControllerConfig controllerConfig)
                : base(name, Fcns.CurrentClassLeafName)
            {
                Config = new ControllerConfig(controllerConfig);
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;

                if (Config.InitialControlMode != ControlMode.None)
                    ControlMode = Config.InitialControlMode;
            }

            public ControllerConfig Config { get; private set; }

            public double ControlReadback { get { return Config.FeedbackGauge.Value; } }
            public double ControlReadbackInPercentOfFS { get { return ControlReadback * Config.OneOverFullScaleControlValue * 100.0; } }

            public ControlMode ControlMode { get; set; } 

            public double ControlSetpoint { get; set; }
            public double ControlSetpointInPercentOfFS { get { return ControlSetpoint * Config.OneOverFullScaleControlValue * 100.0; } set { ControlSetpoint = value * 0.01 * Config.FullScaleControlValue; } }

            //public double SlewControlSetpoint { get; private set; }
            //public double SlewControlSetpointInPercentOfFS { get { return SlewControlSetpoint * Config.OneOverFullScaleControlValue * 100.0; } }

            public double ControlOutputSetpoint { get; set; }
            public double ControlOutputSetpointInPercentOfFS { get { return ControlOutputSetpoint * Config.OneOverFullScaleControlValue * 100.0; } set { ControlOutputSetpoint = value * 0.01 * Config.FullScaleControlValue; } }

            public double ControlError { get; private set; }
            public double ControlErrorInPercentOfFS { get { return ControlError * Config.OneOverFullScaleControlValue * 100.0; } }

            public double IntegratedError { get; private set; }

            public double ControlOutput { get; private set; }

            /// <summary>
            /// Knowns values: Close, Open, Hold, ForceControlMode:value, ForceOutputSetpoint:value
            /// </summary>
            public string FaultInjection
            {
                get { return _faultInjection; }
                set
                {
                    string temp = value.MapNullToEmpty();
                    if (_faultInjection != temp)
                    {
                        StringScanner ss = new StringScanner(_faultInjection = temp);

                        forcedControlMode = ControlMode.None;

                        if (ss.ParseXmlAttribute("ForceControlSetpoint", out forcedControlSetpoint))
                            forcedControlMode = ControlMode.Normal;
                        else if (ss.ParseXmlAttribute("ForceOutputSetpoint", out forcedOutputSetpoint))
                            forcedControlMode = ControlMode.Force;
                        else
                            forcedControlMode = ss.ParseValue(ControlMode.None);

                        Service(ServicePhases.FaultInjection, TimeSpan.Zero, QpcTimeStamp.Now);
                    }
                }
            }
            private string _faultInjection = string.Empty;

            private ControlMode forcedControlMode = ControlMode.None;
            private double forcedControlSetpoint = 0.0;
            private double forcedOutputSetpoint = 0.0;

            // state information from service loop
            double effectiveControlSetpoint;
            double controlReadback;
            double useForwardOutputPortionInPercent;
            double useProportionalOutputPortionInPercent;
            double useIntegratorOutputPortionInPercent;
            bool antiWindupTriggered;
            double lastEffectiveControlSetpoint;
            double lastControlError;
            ControlMode effectiveControlMode;

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                base.Service(servicePhase, measuredServiceInterval, timestampNow);

                bool isForced = (forcedControlMode != ControlMode.None);
                effectiveControlMode = isForced ? forcedControlMode : ControlMode;
                
                effectiveControlSetpoint = isForced ? forcedControlSetpoint : ControlSetpoint;
                controlReadback = ControlReadback;

                ControlError = effectiveControlSetpoint - controlReadback;

                bool isControlModeNormal = (effectiveControlMode == ControlMode.Normal);

                useForwardOutputPortionInPercent = (effectiveControlSetpoint * Config.OneOverFullScaleControlValue * Config.ForwardGainInPercentPerFS).Clip(-Config.ForwardRangeInPercent, Config.ForwardRangeInPercent);

                useProportionalOutputPortionInPercent = (ControlError * Config.OneOverFullScaleControlValue * Config.ProportionalGainInPercentPerFSError).Clip(-Config.ProportionalRangeInPercent, Config.ProportionalRangeInPercent);

                double integrationPeriodInSeconds = measuredServiceInterval.Min(Config.IntegralMaxEffectiveDT).TotalSeconds;
                double integrationPeriodErrorIncrement = ControlError * integrationPeriodInSeconds;
                double nextIntegratedError = (IntegratedError + integrationPeriodErrorIncrement);
                double nextUnclippedIntegratorOutputPortionInPercent = (nextIntegratedError * Config.OneOverFullScaleControlValue * Config.IntegralGainInPercentPerFSErrorSec);
                useIntegratorOutputPortionInPercent = nextUnclippedIntegratorOutputPortionInPercent;

                antiWindupTriggered = Config.ControllerBehavior.IsSet(ControllerBehavior.AntiWindUp)
                                    && ((ControlOutput <= 0.0 && integrationPeriodErrorIncrement < 0.0)
                                        || (ControlOutput >= 100.0 && integrationPeriodErrorIncrement > 0.0)
                                        || (Math.Abs(Config.ControlValve.PercentOpenSetpoint - Config.ControlValve.PercentOpen) >= 5.0)
                                        || (effectiveControlSetpoint != lastEffectiveControlSetpoint)
                                        );

                if (isControlModeNormal)
                {
                    bool nextIntegratedOutputIsInRange = nextUnclippedIntegratorOutputPortionInPercent.IsInRange(-Config.IntegralRangeInPercent, Config.IntegralRangeInPercent);

                    if (nextIntegratedOutputIsInRange && !antiWindupTriggered && Config.IntegralGainInPercentPerFSErrorSec > 0.0)
                    {
                        if (IntegratedError != nextIntegratedError)
                            IntegratedError = nextIntegratedError;
                    }
                    else
                    {
                        // either the integration term has reached its limit or the anitWindupLogic has triggered.  stop changing the IntegratedError term and simply set the integratorOutputPortion to the clipped value of the nextRawIntegratedOutput
                        useIntegratorOutputPortionInPercent = nextUnclippedIntegratorOutputPortionInPercent.Clip(-Config.IntegralRangeInPercent, Config.IntegralRangeInPercent);
                    }
                }
                else
                {
                    // zero the IntegratedError term whenever the control mode is not Normal
                    IntegratedError = 0.0;
                }

                switch (effectiveControlMode)
                {
                    default: 
                    case ControlMode.None:
                    case ControlMode.Hold: break;
                    case ControlMode.Normal: ControlOutput = (useForwardOutputPortionInPercent + useProportionalOutputPortionInPercent + useIntegratorOutputPortionInPercent); break;
                    case ControlMode.Open: ControlOutput = 100.0; break;
                    case ControlMode.Close: ControlOutput = 0.0; break;
                    case ControlMode.Force: ControlOutput = isForced ? forcedOutputSetpoint : ControlOutputSetpoint; break;
                }

                Config.ControlValve.PercentOpenSetpoint = ControlOutput.Clip(0.0, 100.0, 0.0);

                lastEffectiveControlSetpoint = effectiveControlSetpoint;
                lastControlError = ControlError;
            }

            public override string ToString()
            {
                return "{0} {1} out:{2} %".CheckedFormat(base.ToString(), effectiveControlMode, ControlOutput);
            }
        }

        /// <summary>
        /// Defines the type of controller being used here
        /// <para/>Pressure (0), Flow (1)
        /// </summary>
        public enum ControllerType : int
        {
            Pressure = 0,
            Flow = 1,
        }

        /// <summary>
        /// Specifies the control mode for a Controller Component.
        /// <para/>None (0 placeholder), Hold (1), Normal (2), Open (3), Close (4), Force (5)
        /// </summary>
        public enum ControlMode : int
        {
            None = 0,
            Hold = 1,
            Normal = 2,
            Open = 3,
            Close = 4,
            Force = 5,
        }

        [Flags]
        public enum ControllerBehavior : int
        {
            None = 0x01,
            AntiWindUp = 0x01,
        }

        public class ControllerConfig
        {
            public ControllerConfig()
            {
                ControllerBehavior = ControllerBehavior.AntiWindUp;
                InitialControlMode = ControlMode.Normal;
                ForwardRangeInPercent = 100.0;
                ProportionalRangeInPercent = 100.0;
                IntegralRangeInPercent = 100.0;
                IntegralMaxEffectiveDT = (0.1).FromSeconds();
            }

            public ControllerConfig(ControllerConfig other)
            {
                ControllerType = other.ControllerType;
                FeedbackGauge = other.FeedbackGauge;
                ControlValve = other.ControlValve;
                ControllerBehavior = other.ControllerBehavior;
                InitialControlMode = other.InitialControlMode;
                FullScaleControlValue = other.FullScaleControlValue;
                OneOverFullScaleControlValue = (FullScaleControlValue != 0.0 ? 1.0 / FullScaleControlValue : 0.0);
                ForwardGainInPercentPerFS = other.ForwardGainInPercentPerFS;
                ForwardRangeInPercent = other.ForwardRangeInPercent;
                ProportionalGainInPercentPerFSError = other.ProportionalGainInPercentPerFSError;
                ProportionalRangeInPercent = other.ProportionalRangeInPercent;
                IntegralGainInPercentPerFSErrorSec = other.IntegralGainInPercentPerFSErrorSec;
                IntegralRangeInPercent = other.IntegralRangeInPercent;
                IntegralMaxEffectiveDT = other.IntegralMaxEffectiveDT;
            }

            public ControllerType ControllerType { get; set; }
            public Gauge FeedbackGauge { get; set; }
            public Valve ControlValve { get; set; }

            public ControllerBehavior ControllerBehavior { get; set; }

            public ControlMode InitialControlMode { get; set; }

            public double FullScaleControlValue { get; set; }
            public double OneOverFullScaleControlValue { get; private set; }

            public double ForwardGainInPercentPerFS { get; set; }
            public double ForwardRangeInPercent { get; set; }

            public double ProportionalGainInPercentPerFSError { get; set; }
            public double ProportionalRangeInPercent { get; set; }

            public double IntegralGainInPercentPerFSErrorSec { get; set; }
            public double IntegralRangeInPercent { get; set; }
            public TimeSpan IntegralMaxEffectiveDT { get; set; }
        }

        #endregion

        #region Pump, PumpConfig

        public class Pump : FlowModel.NodePairBase
        {
            public Pump(string name, PumpConfig config)
                : base(name, Fcns.CurrentClassLeafName)
            {
                Config = new PumpConfig(config);
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;

                RadiusInMM = Config.RadiusInMM;
                LengthInMM = Config.LengthInMM;

                ActuatorConfig speedActConfig = new ActuatorConfig("{0}.spd", "Off", "FullSpeed", Config.TimeToSpinUp, Config.TimeToSpinDown);
                speedActuator = new ActuatorBase(speedActConfig);
                speedActuatorState = speedActuator.State;

                if (Config.InitialPumpMode != PumpMode.None)
                    PumpMode = Config.InitialPumpMode;
            }

            public PumpConfig Config { get; private set; }
            private ActuatorBase speedActuator;
            private IActuatorState speedActuatorState;

            public PumpMode PumpMode 
            {
                get { return _pumpMode; }
                set
                {
                    _pumpMode = value;
                    Service(ServicePhases.Manual, TimeSpan.Zero, QpcTimeStamp.Now);
                }
            }
            private PumpMode _pumpMode = PumpMode.None;

            public double SpeedInRPM { get { return speedActuatorState.PositionInPercent * Config.FullSpeedInRPM * 0.01; } }
            public double SpeedInPercentOfFS { get { return speedActuatorState.PositionInPercent; } }

            public bool IsAtSpeed { get { return speedActuatorState.IsAtTarget; } }

            public double EffectivePumpingSpeedLperS { get; protected set; }

            /// <summary>
            /// supported values: Off, On, LowSpeed, None
            /// </summary>
            public string FaultInjection
            {
                get { return _faultInjection; }
                set
                {
                    string temp = value.MapNullToEmpty();
                    if (temp != _faultInjection)
                    {
                        StringScanner ss = new StringScanner(_faultInjection = temp);

                        forcedPumpMode = ss.ParseValue(PumpMode.None);

                        Service(ServicePhases.FaultInjection, TimeSpan.Zero, QpcTimeStamp.Now);
                    }
                }
            }
            private string _faultInjection = string.Empty;

            private PumpMode forcedPumpMode = PumpMode.None;

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                base.Service(servicePhase, measuredServiceInterval, timestampNow);

                if (speedActuator.Motion1To2Time != Config.TimeToSpinUp)
                    speedActuator.Motion1To2Time = Config.TimeToSpinUp;

                if (speedActuator.Motion2To1Time != Config.TimeToSpinDown)
                    speedActuator.Motion2To1Time = Config.TimeToSpinDown;

                PumpMode effectivePumpMode = forcedPumpMode.MapDefaultTo(PumpMode);

                switch (effectivePumpMode)
                {
                    case PumpMode.Off: if (speedActuatorState.TargetPos != ActuatorPosition.AtPos1) speedActuator.SetTarget(ActuatorPosition.MoveToPos1); break;
                    case PumpMode.On: if (speedActuatorState.TargetPos != ActuatorPosition.AtPos2) speedActuator.SetTarget(ActuatorPosition.MoveToPos2); break;
                    case PumpMode.LowSpeed: if (speedActuatorState.TargetPositionInPercent != Config.LowSpeedInPercentOfFS) speedActuator.SetTarget(ActuatorPosition.InBetween, Config.LowSpeedInPercentOfFS); break;
                    default:
                    case PumpMode.None: break;
                }

                speedActuator.Service(measuredServiceInterval);
                speedActuatorState = speedActuator.State;
            }

            bool pumpIsEffectivelyOff;
            double effectiveBestBasePressureInKPa;
            double pumpingSpeedDerating;
            double inletDeltaPressureInKPa;

            public override void UpdateFlows()
            {
                pumpIsEffectivelyOff = (speedActuatorState.PositionInPercent <= Config.EffectivelyOffThresholdInPercentOfFS);

                double volumetricFlowOutOfEnd2InSCMS = 0.0;

                bool isTurboPump = Config.PumpBehavior.IsSet(PumpBehavior.TurboPump);

                bool nextDisableBondingConnectionBetweenChambers = isTurboPump ? !pumpIsEffectivelyOff : true;      // bonding is disabled for pumps types other than turbo pumps and for turbo pumps when they are not effectively off.

                if (DisableBondingConnectionBetweenChambers != nextDisableBondingConnectionBetweenChambers)
                    DisableBondingConnectionBetweenChambers = nextDisableBondingConnectionBetweenChambers;

                if (!pumpIsEffectivelyOff)
                {
                    effectiveBestBasePressureInKPa = Config.nominalMinimumPressureInKPa;
                    pumpingSpeedDerating = 1.0;

                    if (Config.MaximumCompressionRatio > 0.0)
                    {
                        double compressionLimitedBasePressureInKPa = End2.PressureInKPa / Config.MaximumCompressionRatio;
                        effectiveBestBasePressureInKPa = Math.Max(effectiveBestBasePressureInKPa, compressionLimitedBasePressureInKPa);
                    }

                    inletDeltaPressureInKPa = End1.PressureInKPa - effectiveBestBasePressureInKPa;

                    if (inletDeltaPressureInKPa > 0.0)
                    {
                        if (Config.maximallyEfficientPressureInKPa > 0.0 && inletDeltaPressureInKPa < Config.maximallyEfficientPressureInKPa)
                            pumpingSpeedDerating = inletDeltaPressureInKPa / Config.maximallyEfficientPressureInKPa;

                        EffectivePumpingSpeedLperS = Config.PumpingSpeedInLperS * speedActuatorState.PositionInPercent * 0.01 * pumpingSpeedDerating;
                        double effectivePumpingSpeedCMS = EffectivePumpingSpeedLperS * 0.001;

                        // determine volumetricFlow through pump from effictivePumpingSpeedCMS
                        volumetricFlowOutOfEnd2InSCMS = effectivePumpingSpeedCMS * End1.PressureInKPa / Physics.Gasses.Air.StandardAtmPressureInKPa;
                    }
                }
                else
                {
                    if (Config.PumpBehavior.IsSet(PumpBehavior.TurboPump))
                    {
                        base.UpdateFlows();     // Turbo pumps behave like pipes when turned off.
                        return;
                    }

                    // else the flow is just zero. - may add leak case here later.
                }

                End1.VolumetricFlowOutOfNodeInSCMS = -volumetricFlowOutOfEnd2InSCMS;
                End2.VolumetricFlowOutOfNodeInSCMS = volumetricFlowOutOfEnd2InSCMS;
            }

            public override string ToString()
            {
                return "{0} {1}".CheckedFormat(base.ToString(), speedActuatorState);
            }
        }

        public enum PumpMode : int
        {
            None = 0,
            Off = 1,
            On = 2,
            LowSpeed = 3,
        }

        [Flags]
        public enum PumpBehavior : int
        {
            None = 0x00,
            RoughingPump = 0x01,
            TurboPump = 0x02,
        }

        public class PumpConfig
        {
            public PumpConfig()
            {
                PressureUnits = PressureUnits.kilopascals;
            }

            public PumpConfig(PumpConfig other)
            {
                RadiusInMM = other.RadiusInMM;
                LengthInMM = other.LengthInMM;
                PumpBehavior = other.PumpBehavior;
                InitialPumpMode = other.InitialPumpMode;
                FullSpeedInRPM = other.FullSpeedInRPM;
                PressureUnits = other.PressureUnits;
                PumpingSpeedInLperS = other.PumpingSpeedInLperS;
                MaximumCompressionRatio = other.MaximumCompressionRatio;
                nominalMinimumPressureInKPa = other.nominalMinimumPressureInKPa;
                maximallyEfficientPressureInKPa = other.maximallyEfficientPressureInKPa;
                TimeToSpinUp = other.TimeToSpinUp;
                TimeToSpinDown = other.TimeToSpinDown;
                LowSpeedInPercentOfFS = other.LowSpeedInPercentOfFS;
                EffectivelyOffThresholdInPercentOfFS = other.EffectivelyOffThresholdInPercentOfFS;
            }

            /// <summary>Used during construction of a pump to initialize its corresponding parameter</summary>
            public double RadiusInMM { get; set; }

            /// <summary>Used during construction of a pump to initialize its corresponding parameter</summary>
            public double LengthInMM { get; set; }

            /// <summary>Defines the desired pump behavior</summary>
            public PumpBehavior PumpBehavior { get; set; }

            /// <summary>Gives the initial PumpMode value for the pump at construction time</summary>
            public PumpMode InitialPumpMode { get; set; }

            /// <summary>Defines the full scale speed for this pump</summary>
            public double FullSpeedInRPM { get; set; }

            /// <summary>Defines the Pressure units of measure for other pressure values used in this object</summary>
            public PressureUnits PressureUnits { get; set; }

            /// <summary>Defines the nominal pumping speed measured in l/s</summary>
            public double PumpingSpeedInLperS { get; set; }

            /// <summary>Defines the maximum compression ratio that this pump produces</summary>
            public double MaximumCompressionRatio { get; set; }

            /// <summary>Defines the minimum pressure that the pump can reach</summary>
            public double NominalMinimumPressure { get { return PressureUnits.ConvertFromKPA(nominalMinimumPressureInKPa); } set { nominalMinimumPressureInKPa = PressureUnits.ConvertToKPa(value); } }

            /// <summary>Defines the pressure at which the pump reaches its maximum compression ratio</summary>
            public double MaximallyEfficientPressure { get { return PressureUnits.ConvertFromKPA(maximallyEfficientPressureInKPa); } set { maximallyEfficientPressureInKPa = PressureUnits.ConvertToKPa(value); } }

            internal double nominalMinimumPressureInKPa, maximallyEfficientPressureInKPa;

            /// <summary>Defines the time required to move from fully off to fully on.  Partial speed changes require proportionally less time</summary>
            public TimeSpan TimeToSpinUp { get; set; }
            /// <summary>Defines the time required to move from fully on to fully off.  Partial moves require proportionally less time</summary>
            public TimeSpan TimeToSpinDown { get; set; }
            /// <summary>Set only.  Sets both TimeToSpinUp and TimeToSpinDown to the given value</summary>
            public TimeSpan TimeToSpinUpAndDown { set { TimeToSpinUp = TimeToSpinDown = value; } }

            /// <summary>Defines the speed in percent that will be used when the pump is set to LowSpeed</summary>
            public double LowSpeedInPercentOfFS { get; set; }

            /// <summary>Defines the percentage of full speed that this pump must exceed before it will be treated like it is on for flow calculations.</summary>
            public double EffectivelyOffThresholdInPercentOfFS { get; set; }

            /// <summary>Uses constructor defaults and sets TimeToSpinUpAndDown to 3.0 seconds</summary>
            public static PumpConfig RoughingPump 
            { 
                get 
                { 
                    return new PumpConfig() 
                    { 
                        PumpBehavior = PumpBehavior.RoughingPump,
                        PressureUnits = PressureUnits.torr, 
                        PumpingSpeedInLperS = 3.0,
                        MaximumCompressionRatio = 1.0e6,        // 1000 / 1e-3
                        NominalMinimumPressure = 1.0e-3,
                        MaximallyEfficientPressure = 1.0e-1,
                        TimeToSpinUpAndDown = (3.0).FromSeconds(), 
                        FullSpeedInRPM = 1800.0, 
                        LowSpeedInPercentOfFS = 50.0,  
                    }; 
                } 
            }

            /// <summary>Uses constructor defaults and sets TimeToSpinUp to 5.0 seconds an TimeToSpinDown to 10.0 seconds</summary>
            public static PumpConfig LittleTurboPump 
            { 
                get 
                { 
                    return new PumpConfig() 
                    {
                        PumpBehavior = PumpBehavior.TurboPump,
                        PressureUnits = PressureUnits.torr,
                        PumpingSpeedInLperS = 2.0,
                        MaximumCompressionRatio = 1.0e9,
                        NominalMinimumPressure = 1.0e-8,
                        TimeToSpinUp = (5.0).FromSeconds(), 
                        TimeToSpinDown = (10.0).FromSeconds(), 
                        FullSpeedInRPM = 30000.0, 
                        LowSpeedInPercentOfFS = 50.0,
                        EffectivelyOffThresholdInPercentOfFS = 10.0,
                    }; 
                } 
            }

            /// <summary>Uses constructor defaults and sets TimeToSpinUp to 20.0 seconds an TimeToSpinDown to 30.0 seconds</summary>
            public static PumpConfig BigTurboPump 
            { 
                get 
                { 
                    return new PumpConfig() 
                    {
                        PumpBehavior = PumpBehavior.TurboPump,
                        PressureUnits = PressureUnits.torr,
                        PumpingSpeedInLperS = 10.0,
                        MaximumCompressionRatio = 1.0e9,
                        NominalMinimumPressure = 1.0e-10,
                        TimeToSpinUp = (20.0).FromSeconds(), 
                        TimeToSpinDown = (30.0).FromSeconds(), 
                        FullSpeedInRPM = 45000, 
                        LowSpeedInPercentOfFS = 30.0,
                        EffectivelyOffThresholdInPercentOfFS = 10.0,
                    }; 
                } 
            }
        }

        #endregion

        #region Chamber

        public class Chamber : FlowModel.ComponentBase
        {
            public Chamber(string name)
                : this(name, Fcns.CurrentClassLeafName)
            { }

            public Chamber(string name, string toStringComponentTypeStr)
                : base(name, toStringComponentTypeStr)
            {
                OriginalNode = new FlowModel.Node(name, chamber: this);
            }

            public override void UpdateFlows()
            {
                // the chamber's node volumetric flow is the sum of the flows out of all of the other nodes that the chamber attaches to with the sign changed to account for the change in direction.
                EffectiveNode.VolumetricFlowOutOfNodeInSCMS = -OriginalNode.ConnectedToNodeArray.Sum(node => node.VolumetricFlowOutOfNodeInSCMS);
            }

            /// <summary>
            /// Getter returns the spherically equivilant volume of the spherical volume of RadiusInM.
            /// Setter sets the RadiusInM to the radius of a sphere with the given volume.
            /// </summary>
            public override double VolumeInM3
            {
                get
                {
                    double radiusM = RadiusInM;
                    return (((Math.PI * 4.0) / 3.0) * radiusM * radiusM * radiusM);
                }
                set
                {
                    RadiusInM = Math.Pow((value * (3.0 / 4.0 * Math.PI)), 1.0/3.0);
                }
            }

            /// <summary>Returns true if this chamber is a pressure source chamber (RadiusMM is zero)</summary>
            public virtual bool IsSource { get { return RadiusInM == 0.0; } }

            /// <summary>Proxy get/set property for OriginalNode.PressureUnits</summary>
            public PressureUnits PressureUnits { get { return OriginalNode.PressureUnits; } set { OriginalNode.PressureUnits = value; } }

            /// <summary>Proxy get/set property for OriginalNode.Pressure</summary>
            public double Pressure { get { return OriginalNode.PressureUnits.ConvertFromKPA(EffectiveNode.PressureInKPa); } set { EffectiveNode.PressureInKPa = OriginalNode.PressureUnits.ConvertToKPa(value); } }

            /// <summary>Proxy get/set property for EffectiveNode.PressureInKPa</summary>
            public virtual double PressureInKPa { get { return EffectiveNode.PressureInKPa; } set { EffectiveNode.PressureInKPa = value; } }

            /// <summary>Proxy get only poperty gives access to OriginalNode.TemperatureUnits</summary>
            public TemperatureUnits TemperatureUnits { get { return OriginalNode.TemperatureUnits; } }

            /// <summary>Proxy get only poperty gives access to OriginalNode.Temperature</summary>
            public double Temperature { get { return OriginalNode.Temperature; } }

            /// <summary>Proxy get only poperty gives access to OriginalNode.TemperatureInDegK</summary>
            public double TemperatureInDegK { get { return OriginalNode.TemperatureInDegK; } }

            public FlowModel.Node EffectiveNode { get { return effectiveNode ?? OriginalNode; } internal set { effectiveNode = value; } }
            private FlowModel.Node effectiveNode;

            public FlowModel.Node OriginalNode { get; private set; }

            public override string ToString()
            {
                return "{0} p_kPa:{1:e3}{2} f_scms:{3:e3}".CheckedFormat(base.ToString(), EffectiveNode.PressureInKPa, IsSource ? " Src" : "", EffectiveNode.VolumetricFlowOutOfNodeInSCMS);
            }
        }

        #endregion
    }

    #region FlowModelEvaluationMode, ServicePhases, RelaxationIterationResult, related ExtensionMethods

    /// <summary>
    /// FixedInterval (0 default), AutoAdjustInterval (1)
    /// </summary>
    public enum FlowModelEvaluationMode : int
    {
        FixedInterval = 0,
        AutoAdjustInterval = 1,
    }

    [Flags]
    public enum ServicePhases : int
    {
        None = 0x00,

        BeforeRelaxation = 0x01,
        AfterRelaxation = 0x02,
        AtStartOfRelaxationInterval = 0x04,
        AtEndOfRelaxationInterval = 0x08,

        AfterSetup = 0x0100,
        Manual = 0x0200,
        FaultInjection = 0x0400,
    }

    public enum RelaxationIterationResult
    {
        None = 0,
        Monotonic = 1,
        Indeterminate = 2,
        Reversed = 3,
    }

    public static partial class ExtensionMethods
    {
        public static bool IncludeInPhase(this ServicePhases componentValue, ServicePhases includeInPhase) { return ((componentValue & includeInPhase) != ServicePhases.None); }
    }

    #endregion

    #region FlowModelConfig, FlowModel

    public class FlowModelConfig
    {
        public FlowModelConfig()
        {
            NominalMinimumRelaxationInterval = TimeSpan.FromMilliseconds(1.0);
            EvaluationMode = FlowModelEvaluationMode.FixedInterval;
            BondingConnectionThresholdInPercentPressurePerMinimumInterval = 20.0;
        }

        public FlowModelConfig(FlowModelConfig other)
        {
            NominalMinimumRelaxationInterval = other.NominalMinimumRelaxationInterval;
            EvaluationMode = other.EvaluationMode;
            BondingConnectionThresholdInPercentPressurePerMinimumInterval = other.BondingConnectionThresholdInPercentPressurePerMinimumInterval;
        }

        /// <summary>Defines the nominal minimum relaxation interval that will be used by any corresponding FlowModel instance</summary>
        public TimeSpan NominalMinimumRelaxationInterval { get; set; }

        /// <summary>Defines the evaluation mode that will be used for relaxation iterations by any corresponding FlowModel instance.</summary>
        public FlowModelEvaluationMode EvaluationMode { get; set; }

        /// <summary>Defines the threshold that is used to tell if a NodePair is considered a bonding connection between chambers (or not)</summary>
        public double BondingConnectionThresholdInPercentPressurePerMinimumInterval { get; set; }
    }

    /// <summary>
    /// This ScanEngine plugin provides a modular toolkit for modeling flow and pressure in a range of "plumbing" configurations.  
    /// Currently this model supports the basic components that can be used to build vacuum and other gas flow configurations.
    /// Supported components include chambers, pipes, valves, gauges, controllers, and pumps.  
    /// Many of these types also support fault injection related properties that can be used to selectively override the components behavior in specific manners.
    /// Current flow and pressure modeling are very simplistic and are not expected to produce physically useful predictions.  Rather this plugin is primarily
    /// intended to support a useful level of behavioral, "hand tweaked", modeling of physical devices with known properties to as to support software development 
    /// and testing when hardware is not available.  It is expected that the client/user of this plugin will be reponsible for configuraing and adjusting the characteristics
    /// and layout of the conceptual model so as to produce the behavior that the client so desires.
    /// <para/>
    /// <para/>Please note: This implementation is currently very preliminary and is expected to change in sigificant ways during future preview releases.
    /// </summary>
    public class FlowModel : ScanEngine.ScanEnginePluginBase
    {
        #region Construction

        public FlowModel(string name, FlowModelConfig config)
            : base(name)
        {
            Config = new FlowModelConfig(config);
        }

        private FlowModelConfig Config { get; set; }

        #endregion

        #region model building methods: Add, AddRange, Connect

        public FlowModel Add(ComponentBase component) 
        {
            component.FlowModelConfig = Config;

            componentList.Add(component);

            return this;
        }

        public FlowModel Add(params ComponentBase[] componentSet)
        {
            AddRange(componentSet);

            return this;
        }

        public FlowModel AddRange(IEnumerable<ComponentBase> componentSet)
        {
            foreach (var component in componentSet)
                Add(component);

            return this;
        }

        public FlowModel Add<TValueType>(DelegateItemSpec<TValueType> delegateItemSpec)
        {
            delegateValueSetAdapter.Add(delegateItemSpec);

            return this;
        }

        public FlowModel Connect(Chamber chamber, params Node[] connectToNodesSet)
        {
            return Connect(chamber.EffectiveNode, connectToNodesSet);
        }

        public FlowModel Connect(Node node1, params Node[] connectToNodesSet)
        {
            return Connect(node1, connectToNodesSet as IEnumerable<Node>);
        }

        public FlowModel Connect(Node node1, IEnumerable<Node> connectToNodesSet)
        {
            foreach (var node2 in connectToNodesSet)
                Connect(node1, node2);

            return this;
        }

        public FlowModel Connect(Node node1, Node node2) 
        {
            Node node1AsNode = node1 as Node;

            if (node1AsNode != null)
                node1AsNode.ConnectTo(node2, connectBackAsWell: true);

            return this;
        }

        #endregion

        #region IScanEnginePlugIn implementation methods: Setup, UpdateInputs, Service, UpdateOutputs

        private List<ComponentBase> componentList = new List<ComponentBase>();
        private DelegateValueSetAdapter delegateValueSetAdapter = new DelegateValueSetAdapter() { OptimizeSets = true };

        public override void Setup(string scanEnginePartName, IConfig pluginsIConfig, IValuesInterconnection pluginsIVI)
        {
            delegateValueSetAdapter.IssueEmitter = Logger.Debug;
            delegateValueSetAdapter.ValueNoteEmitter = Logger.Trace;
            delegateValueSetAdapter.EmitValueNoteNoChangeMessages = false;

            delegateValueSetAdapter.Setup(pluginsIVI, scanEnginePartName).Set().Update();

            List<ComponentBase> componentWorkingList = new List<ComponentBase>(componentList);

            allChambersArray = componentWorkingList.Select(item => item as Chamber).Where(item => item != null).ToArray();
            foreach (var item in allChambersArray)
                componentWorkingList.Remove(item);

            List<NodePairChain> nodePairChainList = new List<NodePairChain>();
            List<NodePairBase> workingNodePairList = new List<NodePairBase>(componentWorkingList.Select(item => item as NodePairBase).Where(item => item != null));

            foreach (var item in workingNodePairList)
                componentWorkingList.Remove(item);

            for (; ; )
            {
                NodePairBase[] chain = ExtractNextChain(workingNodePairList);
                if (chain.IsNullOrEmpty())
                    break;

                foreach (var item in chain)
                    workingNodePairList.Remove(item);

                NodePairChain nodePairChain = new NodePairChain(chain, Config);
                nodePairChainList.Add(nodePairChain);

                componentList.Add(nodePairChain);
            }

            componentBaseArray = componentList.ToArray();
            nodePairChainArray = nodePairChainList.ToArray();
            nodePairArray = workingNodePairList.ToArray();
            nodePairChainAndNodePairArray = nodePairChainArray.Concat(nodePairArray).ToArray();

            foreach (var comp in componentBaseArray)
                comp.Setup(scanEnginePartName, Name);

            Node[] chamberNodes = allChambersArray.Select(item => item.EffectiveNode).ToArray();
            Node[] nodePairsNodesArray = nodePairChainArray.Concat(nodePairArray).SelectMany(item => new[] { item.End1, item.End2 }).ToArray();

            List<Node> mainNodesList = new List<Node>();
            HashSet<Node> foundNodesSet = new HashSet<Node>();

            foreach (var node in chamberNodes.Concat(nodePairsNodesArray))
            {
                if (!foundNodesSet.Contains(node))
                {
                    mainNodesList.Add(node);
                    foundNodesSet.Add(node);
                    foundNodesSet.UnionWith(node.ConnectedToNodeArray);
                }
            }

            mainNodesArray = mainNodesList.Select(node => node as Node).Where(node => node != null).ToArray();
            mainNodesArrayLength = mainNodesArray.Length;

            nodeFlowsArray = new double [mainNodesArrayLength];
            lastNodeFlowsArray = new double[mainNodesArrayLength];

            // initialize newly connected node pressures
            foreach (var node in mainNodesArray)
                node.SetupInitialPressure();

            ServiceComponents(ServicePhases.AfterSetup, TimeSpan.Zero, QpcTimeStamp.Now);
        }

        NodePairBase[] ExtractNextChain(List<NodePairBase> workingNodePairList)
        {
            NodePairBase chainHead = workingNodePairList.FirstOrDefault(item => 
                ((item.End1.ConnectedToNodeArray.Length != 1 || item.End1.ConnectedToNodeArray.First().IsChamber) && item.End2.ConnectedToNodeArray.Length == 1 && item.End2.ConnectedToNodeArray.First().NodePair != null)
                );

            if (chainHead == null)
                return null;

            List<NodePairBase> chainList = new List<NodePairBase>();

            for (; ; )
            {
                bool entryChainHeadIsLastInChain = (chainHead.End2.ConnectedToNodeArray.Length != 1 || chainHead.End2.ConnectedToNodeArray.First().NodePair == null);  // this node is attached to more than one End2 node pairs or it is not attached to another NodePair

                chainList.Add(chainHead);

                chainHead = chainHead.End2.ConnectedToNodeArray.First().NodePair;

                if (chainHead == null || entryChainHeadIsLastInChain)
                    break;
            }

            foreach (var item in chainList)
                workingNodePairList.Remove(item);

            return chainList.ToArray();
        }

        public override void UpdateInputs()
        {
            if (delegateValueSetAdapter.IsUpdateNeeded)
                delegateValueSetAdapter.Update();
        }

        public override void Service(TimeSpan measuredServiceInterval, QpcTimeStamp timeStampNow)
        {
            ServiceComponents(ServicePhases.BeforeRelaxation, measuredServiceInterval, timeStampNow);

            TimeSpan remainingTime = measuredServiceInterval;

            do
            {
                ServiceComponents(ServicePhases.AtStartOfRelaxationInterval, measuredServiceInterval, timeStampNow);

                Array.Copy(nodeFlowsArray, lastNodeFlowsArray, nodeFlowsArray.Length);

                switch (Config.EvaluationMode)
                {
                    case FlowModelEvaluationMode.FixedInterval:
                    default:
                        {
                            TimeSpan iterationDt = Config.NominalMinimumRelaxationInterval;
                            bool isLastStep = (iterationDt > remainingTime);

                            RelaxationIterationResult = PerformRelaxationIteration((isLastStep ? remainingTime : iterationDt), nodeFlowsArray, lastNodeFlowsArray);

                            remainingTime = (remainingTime <= iterationDt ? TimeSpan.Zero : remainingTime - iterationDt);
                        }
                        break;
                    case FlowModelEvaluationMode.AutoAdjustInterval:
                        {
                            TimeSpan iterationDt = ((nextDT != TimeSpan.Zero) ? nextDT : (nextDT = Config.NominalMinimumRelaxationInterval));
                            bool isLastStep = (iterationDt > remainingTime);

                            RelaxationIterationResult = PerformRelaxationIteration((isLastStep ? remainingTime : iterationDt), nodeFlowsArray, lastNodeFlowsArray);

                            remainingTime = (remainingTime <= iterationDt ? TimeSpan.Zero : remainingTime - iterationDt);

                            if (RelaxationIterationResult == RelaxationIterationResult.Reversed)
                            {
                                nextDT = TimeSpan.FromTicks(Math.Max((long) (nextDT.Ticks * 0.5), Config.NominalMinimumRelaxationInterval.Ticks));
                            }
                            else if (RelaxationIterationResult == RelaxationIterationResult.Monotonic && !isLastStep)
                            {
                                if (++monotonicCount >= 4)
                                {
                                    nextDT = TimeSpan.FromTicks((long) (nextDT.Ticks * 1.2));
                                    monotonicCount = 0;
                                }
                            }
                        }
                        break;
                }

                ServiceComponents(ServicePhases.AtEndOfRelaxationInterval, measuredServiceInterval, timeStampNow);
            } while (remainingTime > TimeSpan.Zero);

            ServiceComponents(ServicePhases.AfterRelaxation, measuredServiceInterval, timeStampNow);
        }

        public override void UpdateOutputs()
        {
            delegateValueSetAdapter.Set();
        }

        private ComponentBase[] componentBaseArray;
        private Chamber[] allChambersArray;
        private Chamber[] singleChambersArray;
        private ChamberSet [] chamberSetArray;
        private Chamber[] chamberSetAndSingleChamberArray;
        private NodePairChain[] nodePairChainArray;
        private NodePairBase[] nodePairArray;
        private NodePairBase[] nodePairChainAndNodePairArray;
        private bool[] nodePairChainAndNodePairIsBondingConnectedArray;

        private static readonly ChamberSet[] emptyChamberSetArray = new ChamberSet[0];

        private Node[] mainNodesArray;
        private int mainNodesArrayLength;
        private double[] nodeFlowsArray;
        private double[] lastNodeFlowsArray;

        private Node[] serviceNodesArray;

        #endregion

        #region outer internals: ServiceComponents

        private void ServiceComponents(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timeStampNow)
        {
            foreach (var comp in componentBaseArray)
            {
                if (comp.ServicePhases.IncludeInPhase(servicePhase))
                    comp.Service(servicePhase, measuredServiceInterval, timeStampNow);
            }
        }

        #endregion

        #region inner internals: RelaxationIterationResult PerformRelaxationIteration, DetectAndApplyChamberSetChanges, CatagorizeFlowChange

        TimeSpan nextDT = TimeSpan.Zero;
        int monotonicCount = 0;

        public RelaxationIterationResult RelaxationIterationResult { get; set; }

        private RelaxationIterationResult PerformRelaxationIteration(TimeSpan dt, double[] nodeFlowsArray, double[] lastNodeFlowsArray)
        {
            RelaxationIterationResult result = RelaxationIterationResult.Monotonic;

            foreach (var npItem in nodePairChainAndNodePairArray)
                npItem.UpdateFlows();

            DetectAndApplyChamberSetChanges();

            foreach (var ch in chamberSetAndSingleChamberArray)
                ch.UpdateFlows();

            foreach (var node in serviceNodesArray)
                node.UpdatePressure(dt);

            foreach (var chSet in chamberSetArray)
                chSet.DistributePressures();

            foreach (var nodePairChain in nodePairChainArray)
                nodePairChain.DistributePressures();

            for (int nodeIdx = 0; nodeIdx < mainNodesArrayLength; nodeIdx++)
            {
                var node = mainNodesArray[nodeIdx];

                nodeFlowsArray[nodeIdx] = node.VolumetricFlowOutOfNodeInSCMS;

                result = CatagorizeFlowChange(result, node.VolumetricFlowOutOfNodeInSCMS, lastNodeFlowsArray[nodeIdx]);
            }

            return result;
        }

        /// <summary>
        /// This method only makes sense to perform after all of the node pairs have had their flows updated.
        /// </summary>
        private void DetectAndApplyChamberSetChanges()
        {
            bool areAnyGeneratedArraysNull = (singleChambersArray == null
                                              || chamberSetArray == null
                                              || chamberSetAndSingleChamberArray == null
                                              || nodePairChainAndNodePairIsBondingConnectedArray == null
                                              || serviceNodesArray == null
                                              );


            bool rebuildChamberSetsNeeded = areAnyGeneratedArraysNull;

            if (!rebuildChamberSetsNeeded)
            {
                int num = nodePairChainAndNodePairIsBondingConnectedArray.Length;
                for (int idx = 0; idx < num && !rebuildChamberSetsNeeded; idx++)
                {
                    NodePairBase item = nodePairChainAndNodePairArray[idx];
                    if (nodePairChainAndNodePairIsBondingConnectedArray[idx] != item.IsBondingConnectionBetweenChambers)
                        rebuildChamberSetsNeeded = true;
                }
            }
 
            if (!rebuildChamberSetsNeeded)
                return;

            nodePairChainAndNodePairIsBondingConnectedArray = nodePairChainAndNodePairArray.Select(item => item.IsBondingConnectionBetweenChambers).ToArray();

            // clear effects of prior chamber bonding.
            foreach (var singleCh in allChambersArray)
                singleCh.EffectiveNode = null;

            // first split the set of all chambers it the ones that are single and the ones that are bonded to some other chamber through a shared nodepair.

            singleChambersArray = allChambersArray.Where(item => item.EffectiveNode.ConnectedToNodeArray.All(node => node.NodePair == null || !node.NodePair.IsBondingConnectionBetweenChambers)).ToArray();

            if (singleChambersArray.Length != allChambersArray.Length)
            {
                List<Chamber> bondedChamberScanList = allChambersArray.Where(item => item.EffectiveNode.ConnectedToNodeArray.Any(node => node.IsPartOfBondingConnectionBetweenChambers)).ToList();

                // now divide the bondedChamberScanList into ChamberSets of two or more bonded chambers using a bredth first search technique and accumulate the resulting chamber sets.

                List<ChamberSet> chamberSetList = new List<ChamberSet>();

                for (; ; )
                {
                    Chamber nextStartingCh = bondedChamberScanList.SafeTakeFirst();

                    if (nextStartingCh == null)
                        break;

                    // build a list of this chamber and all of the chambers that are bonded to it (along with a list of the bonding connection NodePairBase objects) and then create a chamber set for it

                    List<Chamber> breadthFirstSearchList = new List<Chamber>(new[] { nextStartingCh });
                    List<Chamber> chamberSetContentBuildList = new List<Chamber>();
                    List<NodePairBase> chamberSetInternalNodePairBuildList = new List<NodePairBase>();

                    for (; ; )
                    {
                        Chamber nexSearchCh = breadthFirstSearchList.SafeTakeFirst();
                        if (nexSearchCh == null)
                            break;

                        chamberSetContentBuildList.Add(nexSearchCh);

                        foreach (var bondingNodePair in nexSearchCh.EffectiveNode.ConnectedToNodeArray.Where(node => node.IsPartOfBondingConnectionBetweenChambers).Select(node => node.NodePair))
                        {
                            Chamber otherCh = bondingNodePair.OtherConnectedChamber(nexSearchCh);
                            if (otherCh != null && !chamberSetContentBuildList.Contains(otherCh))
                            {
                                chamberSetInternalNodePairBuildList.Add(bondingNodePair);
                                breadthFirstSearchList.Add(otherCh);
                                bondedChamberScanList.Remove(otherCh);
                            }
                        }
                    }

                    ChamberSet chamberSet = new ChamberSet(chamberSetContentBuildList.ToArray(), chamberSetInternalNodePairBuildList.ToArray());
                    chamberSetList.Add(chamberSet);
                }

                chamberSetArray = chamberSetList.ToArray();
                chamberSetAndSingleChamberArray = singleChambersArray.Concat(chamberSetArray).ToArray();
            }
            else
            {
                chamberSetArray = emptyChamberSetArray;
                chamberSetAndSingleChamberArray = singleChambersArray;
            }

            // collect all of the ChamberSets effective nodes, combined with the single chamber's effective nodes, combined with all of the old main nodes that are not part of a chamber, not part of a bonding pair where are not connected to a chamber
            serviceNodesArray = chamberSetArray.Select(item => item.EffectiveNode)
                              .Concat(singleChambersArray.Select(item => item.OriginalNode))
                              .Concat(mainNodesArray.Where(node => !node.IsChamber && !node.IsConnectedToChamber && !node.IsPartOfBondingConnectionBetweenChambers))
                              .ToArray();
        }

        private static RelaxationIterationResult CatagorizeFlowChange(RelaxationIterationResult result, double nodeFlow, double lastNodeFlow)
        {
            int nodeFlowSign = Math.Sign(nodeFlow);
            int lastNodeFlowSign = Math.Sign(lastNodeFlow);

            if (nodeFlowSign == lastNodeFlowSign || nodeFlowSign == 0 || lastNodeFlowSign == 0 || result == RelaxationIterationResult.Reversed)
                return result;

            double nodeFlowAbs = Math.Abs(nodeFlow);
            double lastNodeFlowAbs = Math.Abs(lastNodeFlow);

            double oneOverNormalizeBy = 1.0 / Math.Max(nodeFlowAbs, lastNodeFlowAbs);

            nodeFlowAbs *= oneOverNormalizeBy;
            lastNodeFlowAbs *= oneOverNormalizeBy;

            if (nodeFlowAbs <= 0.01 || lastNodeFlowAbs <= 0.01)
                return result;

            if (nodeFlowAbs >= 0.25 && lastNodeFlowAbs >= 0.25)
                return RelaxationIterationResult.Reversed;

            return RelaxationIterationResult.Indeterminate;
        }

        #endregion

        #region internal usage classes: ChamberSet, NodePairChain

        public class ChamberSet : Chamber
        {
            public ChamberSet(Chamber[] chambersInSetArray, NodePairBase [] stiffConnectionsArray)
                : base("ChSet-{0}".CheckedFormat(String.Join("-", chambersInSetArray.Select(item => item.Name).ToArray())), Fcns.CurrentClassLeafName) 
            {
                ChambersInSetArray = chambersInSetArray;
                FirstSourceChamberInSet = ChambersInSetArray.FirstOrDefault(item => item.IsSource);
                BondingConnectionsArray = stiffConnectionsArray;

                List<Node> externalNodeList = new List<Node>();

                foreach (Chamber ch in ChambersInSetArray)
                {
                    foreach (Node node in ch.OriginalNode.ConnectedToNodeArray)
                    {
                        if (!node.IsPartOfBondingConnectionBetweenChambers)
                            externalNodeList.Add(node);
                    }
                }

                _volumeInM3 = ChambersInSetArray.Sum(item => item.VolumeInM3) 
                            + BondingConnectionsArray.Sum(item => item.VolumeInM3)
                            + externalNodeList.Sum(item => item.EffectiveVolumeM3)
                            ;

                double initialSetPressureInKPa = 0.0;

                if (FirstSourceChamberInSet != null)
                {
                    initialSetPressureInKPa = FirstSourceChamberInSet.PressureInKPa;
                }
                else
                {
                    double moles = ChambersInSetArray.Sum(item => Physics.Gasses.IdealGasLaw.ToMoles(item.PressureInKPa, item.VolumeInM3, item.TemperatureInDegK))
                                + BondingConnectionsArray.Sum(item => Physics.Gasses.IdealGasLaw.ToMoles(item.End1.PressureInKPa, item.VolumeInM3, item.End1.TemperatureInDegK))
                                + externalNodeList.Sum(item => Physics.Gasses.IdealGasLaw.ToMoles(item.PressureInKPa, item.EffectiveVolumeM3, item.TemperatureInDegK));

                    initialSetPressureInKPa = Physics.Gasses.IdealGasLaw.ToPressureInKPa(moles, _volumeInM3, TemperatureInDegK);
                }

                EffectiveNode.PressureInKPa = initialSetPressureInKPa;

                // need to determine the effective pressure of the newly combined nodes and 

                EffectiveNode.ConnectedToNodeArray = externalNodeList.ToArray();

                foreach (Chamber setMemberCh in ChambersInSetArray)
                    setMemberCh.EffectiveNode = EffectiveNode;

                DistributePressures();
            }

            public Chamber[] ChambersInSetArray { get; protected set; }
            public Chamber FirstSourceChamberInSet { get; protected set; }
            public NodePairBase[] BondingConnectionsArray { get; protected set; }

            public void DistributePressures()
            {
                double pressureInKPa = (FirstSourceChamberInSet != null) ? FirstSourceChamberInSet.PressureInKPa : PressureInKPa;

                foreach (var ch in ChambersInSetArray.Where(item => !item.IsSource))
                {
                    ch.OriginalNode.SetPressureAndDistributeToSubNodes(pressureInKPa);
                }

                foreach (var nodePair in BondingConnectionsArray)
                {
                    nodePair.DistributePressures();
                }
            }

            /// <summary>
            /// Getter returns the spherically equivilant volume of the spherical volume of RadiusInM.
            /// Setter sets the RadiusInM to the radius of a sphere with the given volume.
            /// </summary>
            public override double VolumeInM3 { get { return _volumeInM3;} set {_volumeInM3 = value;}}
            double _volumeInM3;

            /// <summary>Returns true if this chamber is a pressure source chamber (RadiusMM is zero)</summary>
            public override bool IsSource { get { return FirstSourceChamberInSet != null; } }

            public override string ToString()
            {
                return "{0} p_kPa:{1:e3} f_scms:{2:e3}".CheckedFormat(base.ToString(), EffectiveNode.PressureInKPa, EffectiveNode.VolumetricFlowOutOfNodeInSCMS);
            }
        }

        public class NodePairChain : NodePairBase
        {
            public NodePairChain(NodePairBase[] nodePairChainArray, FlowModelConfig flowModelConfig)
                : base("Chain-{0}".CheckedFormat(string.Join("-", nodePairChainArray.Select(item => item.Name).ToArray())), Fcns.CurrentClassLeafName)
            {
                NodePairChainArray = nodePairChainArray;
                NodePairChainArrayLength = NodePairChainArray.Length;
                nodePairResistanceArray = new double[NodePairChainArrayLength];

                FlowModelConfig = flowModelConfig;

                innerEnd1 = nodePairChainArray.First().End1;
                innerEnd2 = nodePairChainArray.Last().End2;

                Node[] entryEnd1ConnectedNodesArray = innerEnd1.ConnectedToNodeArray;
                Node[] entryEnd2ConnectedNodesArray = innerEnd2.ConnectedToNodeArray;

                innerEnd1.DisconnectFrom(innerEnd1.ConnectedToNodeArray.First());
                innerEnd2.DisconnectFrom(innerEnd2.ConnectedToNodeArray.First());

                foreach (var item in entryEnd1ConnectedNodesArray)
                {
                    item.DisconnectFrom(innerEnd1);
                    item.ConnectTo(End1);
                }

                foreach (var item in entryEnd2ConnectedNodesArray)
                {
                    item.DisconnectFrom(innerEnd2);
                    item.ConnectTo(End2);
                }

                End1.PressureInKPa = innerEnd1.PressureInKPa;
                End2.PressureInKPa = innerEnd2.PressureInKPa;

                LengthInM = NodePairChainArray.Sum(item => item.LengthInM);
                _volumeInM3 = NodePairChainArray.Sum(item => item.VolumeInM3);

                ServicePhases = ServicePhases.BeforeRelaxation;
            }

            public override double VolumeInM3 { get { return _volumeInM3; } }
            private double _volumeInM3;

            public override double ResistanceInKPaSecPerM3 { get { return totalResistanceInKPaSecPerM3; } }
            private double totalResistanceInKPaSecPerM3 = double.PositiveInfinity;

            private Node innerEnd1, innerEnd2;

            public NodePairBase[] NodePairChainArray { get; private set; }
            public int NodePairChainArrayLength { get; private set; }

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                base.Service(servicePhase, measuredServiceInterval, timestampNow);

                double totalLengthInM = 0.0;
                _volumeInM3 = 0.0;
                bool anyChange = false;

                for (int idx = 0; idx < NodePairChainArrayLength; idx++)
                {
                    var item = NodePairChainArray[idx];
                    item.Service(servicePhase, measuredServiceInterval, timestampNow);

                    totalLengthInM += item.LengthInM;
                    _volumeInM3 += item.VolumeInM3;

                    double itemResistance = item.ResistanceInKPaSecPerM3;
                    if (nodePairResistanceArray[idx] != itemResistance)
                    {
                        nodePairResistanceArray[idx] = itemResistance;
                        anyChange = true;
                    }
                }

                LengthInM = totalLengthInM;

                if (anyChange)
                {
                    totalResistanceInKPaSecPerM3 = nodePairResistanceArray.Sum();

                    if (double.IsNaN(totalResistanceInKPaSecPerM3) || totalResistanceInKPaSecPerM3 <= 0.0)
                        throw new System.InvalidOperationException("resistance");

                    double end1EffectiveVolumeM3 = _volumeInM3 * 0.5;
                    double end2EffectiveVolumeM3 = _volumeInM3 * 0.5;

                    if (double.IsInfinity(totalResistanceInKPaSecPerM3))
                    {
                        for (int idx = 0; idx < NodePairChainArrayLength; idx++)
                        {
                            var item = NodePairChainArray[idx];
                            if (!double.IsInfinity(nodePairResistanceArray[idx]))
                            {
                                end1EffectiveVolumeM3 += item.VolumeInM3;
                            }
                            else
                            {
                                end1EffectiveVolumeM3 += item.VolumeInM3 * 0.5;
                                break;
                            }
                        }

                        for (int idx = NodePairChainArrayLength - 1; idx > 0; idx--)
                        {
                            var item = NodePairChainArray[idx];
                            if (!double.IsInfinity(nodePairResistanceArray[idx]))
                            {
                                end1EffectiveVolumeM3 += item.VolumeInM3;
                            }
                            else
                            {
                                end1EffectiveVolumeM3 += item.VolumeInM3 * 0.5;
                                break;
                            }
                        }
                    }

                    End1.EffectiveVolumeM3 = end1EffectiveVolumeM3;
                    End2.EffectiveVolumeM3 = end2EffectiveVolumeM3;
                }
            }

            private double[] nodePairResistanceArray;

            public override void UpdateFlows()
            {
                base.UpdateFlows();

                double end1VolumetricFlowRateSCMS = End1.VolumetricFlowOutOfNodeInSCMS;
                double end2VolumetricFlowRateSCMS = End2.VolumetricFlowOutOfNodeInSCMS;

                foreach (var item in NodePairChainArray)
                {
                    item.End1.VolumetricFlowOutOfNodeInSCMS = end1VolumetricFlowRateSCMS;
                    item.End2.VolumetricFlowOutOfNodeInSCMS = end2VolumetricFlowRateSCMS;
                }
            }

            public override void DistributePressures()
            {
                double end1PressureInKPa = End1.PressureInKPa;
                double end2PressureInKPa = End2.PressureInKPa;

                innerEnd1.PressureInKPa = end1PressureInKPa;
                innerEnd2.PressureInKPa = end2PressureInKPa;

                if (!double.IsInfinity(totalResistanceInKPaSecPerM3))
                {
                    double pDelta = end2PressureInKPa - end1PressureInKPa;
                    double accumulatedResistance = 0.0;
                    double oneOverTotalResistance = (totalResistanceInKPaSecPerM3 > 0.0) ? (1.0 / totalResistanceInKPaSecPerM3) : 0.0;

                    foreach (var item in NodePairChainArray)
                    {
                        double itemResistance = item.ResistanceInKPaSecPerM3;
                        accumulatedResistance += itemResistance;
                        double inferredPressure = end1PressureInKPa + (accumulatedResistance * oneOverTotalResistance) * pDelta;

                        item.End2.SetPressureAndDistributeToSubNodes(inferredPressure);
                    }
                }
                else
                {
                    for (int idx = 0; idx < NodePairChainArrayLength; idx++)
                    {
                        var item = NodePairChainArray[idx];
                        if (double.IsInfinity(item.ResistanceInKPaSecPerM3))
                            break;

                        item.End2.SetPressureAndDistributeToSubNodes(end1PressureInKPa);
                    }

                    for (int idx = NodePairChainArrayLength - 1; idx > 0; idx--)
                    {
                        var item = NodePairChainArray[idx];
                        if (double.IsInfinity(item.ResistanceInKPaSecPerM3))
                            break;

                        item.End1.SetPressureAndDistributeToSubNodes(end2PressureInKPa);
                    }
                }
            }
        }

        #endregion

        #region base and helper classes for components: ComponentBase, Node, NodePair

        public abstract class ComponentBase
        {
            public ComponentBase(string name, string toStringComponentTypeStr)
            {
                Name = name;
                ToStringComponentTypeStr = toStringComponentTypeStr;
                VolumeInM3 = 0.0;
            }

            public ServicePhases ServicePhases { get; protected set; }

            internal FlowModelConfig FlowModelConfig { get; set; }

            public virtual void Setup(string scanEnginePartName, string vacuumSimPartName)
            { }

            public virtual void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            { }

            public virtual void UpdateFlows()
            { }

            public string Name { get; protected set; }

            public double RadiusInM { get; set; }
            public double RadiusInMM { get { return RadiusInM * 1000.0; } set { RadiusInM = value * 0.001; } }

            public virtual double VolumeInM3 { get; set; }
            public double VolumeInMM3 { get { return VolumeInM3 * 1.0e9; } set { VolumeInM3 = value * 1.0e-9; } }

            public string ToStringComponentTypeStr { get; private set; }

            public override string ToString()
            {
                return "{0} '{1}' r_mm:{2:f1} v_m^3:{3:e3}".CheckedFormat(ToStringComponentTypeStr, Name, RadiusInMM, VolumeInM3);
            }
        }

        public class Node
        {
            public Node(string name, Chamber chamber = null, NodePairBase nodePair = null, Node observesNode = null)
            {
                Name = name;

                if (observesNode != null)
                {
                    chamber = chamber ?? observesNode.Chamber;
                    nodePair = nodePair ?? observesNode.NodePair;
                }

                Chamber = chamber;
                NodePair = nodePair;
                ObservesNode = observesNode;

                TemperatureUnits = TemperatureUnits.DegC;
                Temperature = 20.0;
                PressureInKPa = Physics.Gasses.Air.StandardAtmPressureInKPa;
                PressureUnits = PressureUnits.kilopascals;
            }

            public string Name { get; private set; }
            public bool IsChamber { get { return (Chamber != null); } }
            public bool IsSource { get { return (IsChamber && Chamber.IsSource); } }

            /// <summary>If this node is connected to one other node and that node is a chamber then this property returns that chamber, otherwise it returns null</summary>
            public Chamber ConnectedToChamber { get { return ((ConnectedToNodeArray.SafeLength() == 1) ? ConnectedToNodeArray[0].Chamber : null) ;} }

            /// <summary>Returns true if the node is connected to a single chamber</summary>
            public bool IsConnectedToChamber { get { return (ConnectedToChamber != null); } }

            public TemperatureUnits TemperatureUnits { get; set; }
            public double Temperature { get { return TemperatureUnits.ConvertFromDegK(TemperatureInDegK); } set { TemperatureInDegK = TemperatureUnits.ConvertToDegK(value); } }
            public double TemperatureInDegK { get; set; }

            public virtual double EffectiveVolumeM3
            {
                get
                {
                    if (_effectiveVolumeM3 == 0.0)
                    {
                        if (Chamber != null)
                            _effectiveVolumeM3 = Chamber.VolumeInM3;
                        else if (NodePair != null)
                            _effectiveVolumeM3 = NodePair.VolumeInM3 * 0.5;
                    }

                    return _effectiveVolumeM3;
                }
                set { _effectiveVolumeM3 = value; }
            }
            double _effectiveVolumeM3 = 0.0;

            /// <summary>Defines the PressureUnits used with the Pressure property</summary>
            public PressureUnits PressureUnits { get; set; }

            /// <summary>gets/sets the node pressure in configured PressureUnits.</summary>
            public double Pressure { get { return PressureUnits.ConvertFromKPA(PressureInKPa); } set { PressureInKPa = PressureUnits.ConvertToKPa(value); } }

            public double PressureInKPa { get; set; }

            /// <summary>volumetric flow rate out of node in Standard cubic meters per second</summary>
            public double VolumetricFlowOutOfNodeInSCMS { get; set; }

            /// <summary>Returns true if this Node is part of a NodePair and that nodePair IsPartOfBondingConnectionBetweenChambers</summary>
            public bool IsPartOfBondingConnectionBetweenChambers { get { return (NodePair != null && NodePair.IsBondingConnectionBetweenChambers); } }

            public Node[] ConnectedToNodeArray 
            { 
                get { return connectedToNodeArray ?? (connectedToNodeArray = connectedToNodeList.ToArray()); }
                internal set { connectedToNodeList.Clear(); connectedToNodeList.AddRange(value); connectedToNodeArray = value; }
            }
            public Chamber Chamber { get; private set; }
            public NodePairBase NodePair { get; private set; }
            public Node ObservesNode { get; private set; }

            List<Node> connectedToNodeList = new List<Node>();
            Node[] connectedToNodeArray = null;

            public Node ConnectTo(Node otherNode, bool connectBackAsWell = true)
            {
                if (otherNode != null && (!IsChamber || !otherNode.IsChamber))
                {
                    connectedToNodeList.Add(otherNode);

                    connectedToNodeArray = null;

                    if (connectBackAsWell && otherNode != null)
                        otherNode.ConnectTo(this, connectBackAsWell: false);
                }
                // otherwise otherNode is null or both nodes are chambers and we cannot directly connect chambers to each other....

                return this;
            }

            public Node DisconnectFrom(Node otherNode, bool disconnectBackAsWell = true)
            {
                if (connectedToNodeList.Contains(otherNode))
                {
                    connectedToNodeList.Remove(otherNode);
                    connectedToNodeArray = null;

                    if (disconnectBackAsWell)
                        otherNode.DisconnectFrom(this, disconnectBackAsWell: false);
                }

                return this;
            }

            public override string ToString()
            {
                string connToStr = String.Join(",", ConnectedToNodeArray.Select(item => item.Name).ToArray());
                if (IsChamber)
                    return "Node {0} p_kPa:{1:e3} f_scms:{2:e3} c:{3}".CheckedFormat(Chamber, PressureInKPa, VolumetricFlowOutOfNodeInSCMS, connToStr);
                else
                    return "Node {0} p_kPa:{1:e3} f_scms:{2:e3} effV_m^3:{3:e3} c:{4}".CheckedFormat(Name, PressureInKPa, VolumetricFlowOutOfNodeInSCMS, EffectiveVolumeM3, connToStr);
            }

            public virtual void UpdatePressure(TimeSpan dt)
            {
                double updatedPressureInKPa = PressureInKPa;

                if (!IsSource)
                {
                    double summedVolumeM3 = (EffectiveVolumeM3 + ConnectedToNodeArray.Sum(item => item.EffectiveVolumeM3));
                    double summedVolumetricFlowOutOfNodeInSCMS = VolumetricFlowOutOfNodeInSCMS - (IsChamber ? 0.0 : ConnectedToNodeArray.Sum(item => item.VolumetricFlowOutOfNodeInSCMS));    // for chambers the flow has already been summed

                    if (double.IsNaN(summedVolumeM3) || double.IsInfinity(summedVolumeM3) || summedVolumeM3 <= 0.0)
                        throw new System.InvalidOperationException("summedVolumeM3");

                    if (double.IsNaN(TemperatureInDegK) || double.IsInfinity(TemperatureInDegK) || TemperatureInDegK <= 0.0)
                        throw new System.InvalidOperationException("TemperatureInDegK");

                    double currentMoles = Physics.Gasses.IdealGasLaw.ToMoles(PressureInKPa, summedVolumeM3, TemperatureInDegK);

                    double deltaVolumeOutOfNodeM3 = summedVolumetricFlowOutOfNodeInSCMS * dt.TotalSeconds;
                    double deltaMolesOutOfNode = Physics.Gasses.IdealGasLaw.ToMoles(Physics.Gasses.Air.StandardAtmPressureInKPa, deltaVolumeOutOfNodeM3, TemperatureInDegK);

                    double updatedMoles = Math.Max(0.0, currentMoles - deltaMolesOutOfNode);

                    updatedPressureInKPa = Physics.Gasses.IdealGasLaw.ToPressureInKPa(updatedMoles, summedVolumeM3, TemperatureInDegK);

                    double maxPressure = 10.0 * Physics.Gasses.Air.StandardAtmPressureInKPa;
                    if (!updatedPressureInKPa.IsInRange(0.0, maxPressure))
                        updatedPressureInKPa = updatedPressureInKPa.Clip(0.0, maxPressure, updatedPressureInKPa);

                    // note: it is expected that the pressure may actually reach zero when the above delta calculation logic overshoots 
                    if (double.IsNaN(updatedPressureInKPa) || double.IsInfinity(updatedPressureInKPa) || updatedPressureInKPa < 0.0)
                        throw new System.InvalidOperationException("updatedPressureInKPa");
                }

                SetPressureAndDistributeToSubNodes(updatedPressureInKPa);
            }

            public virtual void SetupInitialPressure()
            {
                double sharedPressureInKPa = PressureInKPa;

                if (!IsSource)
                {
                    IEnumerable<Node> allNodeSet = new Node[] { this }.Concat(ConnectedToNodeArray);
                    double summedVolumeInM3 = allNodeSet.Sum(item => item.EffectiveVolumeM3);
                    double summedNodeMoles = allNodeSet.Sum(item => Physics.Gasses.IdealGasLaw.ToMoles(item.PressureInKPa, item.EffectiveVolumeM3, item.TemperatureInDegK));

                    if (summedNodeMoles > 0.0)
                        sharedPressureInKPa = Physics.Gasses.IdealGasLaw.ToPressureInKPa(summedNodeMoles, summedVolumeInM3, TemperatureInDegK);
                    else
                        sharedPressureInKPa = PressureInKPa;
                }

                SetPressureAndDistributeToSubNodes(sharedPressureInKPa);
            }

            public virtual void SetPressureAndDistributeToSubNodes(double pressureInKPa)
            {
                PressureInKPa = pressureInKPa;

                foreach (var subNode in ConnectedToNodeArray)
                    subNode.PressureInKPa = pressureInKPa;
            }
        }

        public class NodePairBase : ComponentBase
        {
            public NodePairBase(string name, string toStringComponentTypeStr)
                : base(name, toStringComponentTypeStr)
            {
                End1 = new Node("{0}.End1".CheckedFormat(name), nodePair: this);
                End2 = new Node("{0}.End2".CheckedFormat(name), nodePair: this);
            }

            public override void UpdateFlows()
            {
                double pEnd1 = End1.PressureInKPa;
                double pEnd2 = End2.PressureInKPa;

                if (double.IsNaN(pEnd1) || double.IsInfinity(pEnd1))
                    throw new System.InvalidOperationException("pEnd1");

                if (double.IsNaN(pEnd2) || double.IsInfinity(pEnd2))
                    throw new System.InvalidOperationException("pEnd2");

                double pDiff = pEnd2 - pEnd1;       // we are calculating the flow out of End1 (and into End2) which occurs when p@End2 > p@End1
                double pAvg = (pEnd1 + pEnd2) * 0.5;

                double resistanceInKPaSecPerM3 = ResistanceInKPaSecPerM3;

                if (double.IsNaN(resistanceInKPaSecPerM3) || resistanceInKPaSecPerM3 <= 0.0)
                    throw new System.InvalidOperationException("resistance");

                double flowRateM3PerSec = (double.IsInfinity(resistanceInKPaSecPerM3) ? 0.0 : pDiff / resistanceInKPaSecPerM3);

                Chamber chEnd1 = End1.ConnectedToChamber;
                Chamber chEnd2 = End2.ConnectedToChamber;

                bool isBondingDetectionEnabled = (FlowModelConfig.BondingConnectionThresholdInPercentPressurePerMinimumInterval > 0.0);
                bool nextIsBondingConnectionBetweenChambersValue = isBondingConnectionBetweenChambers;

                if (chEnd1 != null && chEnd2 != null && isBondingDetectionEnabled)
                {
                    bool bothAreSources = chEnd1.IsSource && chEnd2.IsSource;

                    double end1ChamberVolumeInM3 = chEnd1.VolumeInM3;     // pressure reference chambers have zero volume
                    double end2ChamberVolumeInM3 = chEnd2.VolumeInM3;     // pressure reference chambers have zero volume
                    double testConnectedChamberVolumeInM3 = (end1ChamberVolumeInM3 > 0.0 && end2ChamberVolumeInM3 > 0.0 ? Math.Min(end1ChamberVolumeInM3, end2ChamberVolumeInM3) : Math.Max(end1ChamberVolumeInM3, end2ChamberVolumeInM3));

                    double absIncrementalFlowM3PerMinInterval = Math.Abs(flowRateM3PerSec * FlowModelConfig.NominalMinimumRelaxationInterval.TotalSeconds);

                    if (!bothAreSources && absIncrementalFlowM3PerMinInterval > testConnectedChamberVolumeInM3 * FlowModelConfig.BondingConnectionThresholdInPercentPressurePerMinimumInterval * 0.01)
                        nextIsBondingConnectionBetweenChambersValue = true;
                    else if (isBondingConnectionBetweenChambers && resistanceInKPaSecPerM3 > lastResistanceInKPaSecPerM3)
                        nextIsBondingConnectionBetweenChambersValue = false;        // once bonding is turned on then it will only be turned off if it is disabled or if the resistance increases.
                    // else leave it in the current state.
                }
                else
                {
                    nextIsBondingConnectionBetweenChambersValue = false;
                }

                if (isBondingConnectionBetweenChambers != nextIsBondingConnectionBetweenChambersValue)
                    isBondingConnectionBetweenChambers = nextIsBondingConnectionBetweenChambersValue;

                double volumetricFlowInSCMS = (!IsBondingConnectionBetweenChambers) ? ((pAvg / Physics.Gasses.Air.StandardAtmPressureInKPa) * flowRateM3PerSec) : 0.0;

                End1.VolumetricFlowOutOfNodeInSCMS = volumetricFlowInSCMS;        // (pDiff = pEnd2 - pEnd1) above, or zero if this IsBondingConnectionBetweenChambers
                End2.VolumetricFlowOutOfNodeInSCMS = -volumetricFlowInSCMS;

                if (double.IsNaN(volumetricFlowInSCMS) || double.IsInfinity(volumetricFlowInSCMS))
                    throw new System.InvalidOperationException("volumetricFlowInSCMS");

                lastResistanceInKPaSecPerM3 = resistanceInKPaSecPerM3;
            }

            public bool IsBondingConnectionBetweenChambers { get { return isBondingConnectionBetweenChambers && !DisableBondingConnectionBetweenChambers; } protected set { isBondingConnectionBetweenChambers = value; } }
            private bool isBondingConnectionBetweenChambers;
            private double lastResistanceInKPaSecPerM3;
            protected bool DisableBondingConnectionBetweenChambers { get; set; }

            public double LengthInM { get; set; }
            public double LengthInMM { get { return LengthInM * 1000.0; } set { LengthInM = value * 0.001; } }
            public double EffectiveAreaGain { get { return _effectiveAreaGain; } set { _effectiveAreaGain = value.Clip(0.0, 10.0, 1.0); } }
            private double _effectiveAreaGain = 1.0;

            public override double VolumeInM3 { get { return NominalAreaInM2 * LengthInM; } }

            private double NominalAreaInM2 { get { return (Math.PI * RadiusInM * RadiusInM); } }
            public virtual double EffectiveAreaM2 { get { return NominalAreaInM2 * _effectiveAreaGain; } }

            public virtual double ResistanceInKPaSecPerM3
            {
                get
                {
                    double areaM2 = EffectiveAreaM2;  // mm^2 => m^2
                    double lengthM = LengthInM; // mm => m

                    if (double.IsNaN(lengthM) || double.IsInfinity(lengthM) || lengthM <= 0.0)
                        throw new System.InvalidOperationException("lengthM");

                    if (double.IsNaN(areaM2) || double.IsInfinity(areaM2))
                        throw new System.InvalidOperationException("areaM2");

                    /* Hagen–Poiseuille flow equation:
                     * 
                     * Q = (pi * R^4 * dP)/(8 * mu * L)
                     * 
                     *  - or -
                     *  
                     * dP = (8 * mu * L * Q) / (pi * R^4)
                     * 
                     * Q: flow rate in m^3/s
                     * R: pipe radius in m
                     * L: pipe length in m
                     * mu: dynamic viscosity kPa*s
                     * dP: pressure difference in kPa
                     * 
                     * Q = (m^4 * N/m^2)/(N/m^2 * s * m) = (N * m^2)/(N * s / m) = m ^ 3/s
                     */

                    // pi was moved from denominator to numerator because of change from radius^4 to area^2 (which now has pi^2 in it)
                    double resistanceInKPaSecPerM3 = ((areaM2 > 0.0) ? ((Math.PI * 8.0 * Physics.Gasses.N2.ViscosityInKPaS * lengthM) / (areaM2 * areaM2)) : double.PositiveInfinity);

                    if (double.IsNaN(resistanceInKPaSecPerM3) || resistanceInKPaSecPerM3 <= 0.0)
                        throw new System.InvalidOperationException("resistance");

                    return resistanceInKPaSecPerM3;
                }
            }

            public virtual void DistributePressures()
            { }

            public double InitialPressureInKPa 
            { 
                set 
                {
                    End1.SetPressureAndDistributeToSubNodes(value);
                    End2.SetPressureAndDistributeToSubNodes(value);

                    DistributePressures();
                } 
            }

            public Node End1 { get; protected set; }
            public Node End2 { get; protected set; }

            public override string ToString()
            {
                return "{0} p_kPa:{1:e3},{2:e3} f_scms:{3:e3}".CheckedFormat(base.ToString(), End1.PressureInKPa, End2.PressureInKPa, End1.VolumetricFlowOutOfNodeInSCMS);
            }

            /// <summary>
            /// For node pairs that connect two chambers, this method accepts the given testCh and returns the chamber at the first end that is not same instance as the given testChamber instance.
            /// This method will return null if this node pair does not connect the given chamber to another chamber.
            /// </summary>
            public Chamber OtherConnectedChamber(Chamber testChamber)
            {
                if (Object.ReferenceEquals(End1.ConnectedToChamber, testChamber))
                    return End2.ConnectedToChamber;
                else if (Object.ReferenceEquals(End2.ConnectedToChamber, testChamber))
                    return End1.ConnectedToChamber;
                else
                    return null;
            }
        }

        #endregion
    }

    #endregion
}

//-------------------------------------------------------------------
