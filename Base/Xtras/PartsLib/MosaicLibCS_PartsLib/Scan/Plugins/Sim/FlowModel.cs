//-------------------------------------------------------------------
/*! @file FlowModel.cs
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
using MosaicLib.Utils.Collections;

using Physics = MosaicLib.PartsLib.Common.Physics;

namespace MosaicLib.PartsLib.Scan.Plugin.Sim.FlowModel
{
    namespace Components
    {
        #region Serviceable

        /// <summary>
        /// This object allows a client to invoke delegates during setup and service phases of the use of the flow model.
        /// This is intended to allow an external entity to "hang" custom logic off of the internals of a flow model, such as to propagate a value from one component to another.
        /// <para/>defaults: ServicePhases = BeforeRelaxation
        /// </summary>
        public class Serviceable : FlowModel.IServiceable
        {
            Serviceable()
            {
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;
            }

            public ServicePhases ServicePhases { get; set; }

            public Action<string, string> SetupProxyDelegate { get; set; }

            public Action<ServicePhases, TimeSpan, QpcTimeStamp> ServiceProxyDelegate { get; set; }

            void FlowModel.IServiceable.Setup(string scanEnginePartName, string vacuumSimPartName)
            {
                Action<string, string> setupProxyDelegate = SetupProxyDelegate;
                if (setupProxyDelegate != null)
                    setupProxyDelegate(scanEnginePartName, vacuumSimPartName);
            }

            void FlowModel.IServiceable.Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                Action<ServicePhases, TimeSpan, QpcTimeStamp> serviceProxyDelegate = ServiceProxyDelegate;
                if (serviceProxyDelegate != null)
                    serviceProxyDelegate(servicePhase, measuredServiceInterval, timestampNow);
            }
        }

        #endregion

        #region Pipe

        public class Pipe : FlowModel.NodePairBase<PipeConfig>
        {
            public Pipe(string name, PipeConfig config)
                : base(name, config, Fcns.CurrentClassLeafName)
            { }
        }

        public class PipeConfig : FlowModel.ComponentConfig, ICopyable<PipeConfig>
        {
            public PipeConfig() 
            { }

            public PipeConfig(PipeConfig other) 
                : base(other) 
            { }

            PipeConfig ICopyable<PipeConfig>.MakeCopyOfThis(bool deepCopy) { return new PipeConfig(this); }
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

        public class Valve : FlowModel.NodePairBase<ValveConfig>
        {
            public Valve(string name, ValveConfig valveConfig)
                : this(name, valveConfig, Fcns.CurrentClassLeafName)
            { }

            public Valve(string name, ValveConfig valveConfig, string toStringComponentTypeStr)
                : base(name, valveConfig, toStringComponentTypeStr)
            {
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;

                actuator = new Helpers.ActuatorBase(Name = "{0}.a".CheckedFormat(name), new ActuatorConfig() { InitialPos = ActuatorPosition.AtPos1, Motion1To2Time = Config.TimeToOpen, Motion2To1Time = Config.TimeToClose, Pos1Name = "Closed", Pos2Name = "Opened" });

                switch (Config.InitialValveRequest)
                {
                    case ValveRequest.Open:
                    case ValveRequest.Close:
                        SetValveRequest(Config.InitialValveRequest);
                        break;
                    case ValveRequest.InBetween:
                        SetValveRequest(Config.InitialValveRequest, Config.InitialPercentOpenSetpoint);
                        break;
                    default:
                    case ValveRequest.None:
                        if (Config.InitialPercentOpenSetpoint != 0.0)
                            PercentOpenSetpoint = Config.InitialPercentOpenSetpoint;
                        break;
                }

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

            private ActuatorBase actuator;
            private IActuatorState actuatorState;

            public IActuatorState ActuatorState { get { return actuatorState; } }

            public double PercentOpen { get { return actuatorState.PositionInPercent; } }
            public double PercentOpenSetpoint 
            { 
                get { return actuatorState.TargetPositionInPercent; } 
                set { SetValveRequest(positionInPercentIn: value); }
            }

            /// <summary>
            /// This property is a variant of the PercentOpenSetpoint which as been modified to linearize the conductance rather than the open area (sqrt/square applied as appropriate)
            /// Please note that the conductance varies with the square of the effective area.
            /// </summary>
            public double ConductanceLinearizedPercentOpenSetpoint
            {
                get { return Math.Pow(PercentOpenSetpoint * 0.01, 2.0) * 100.0; }
                set { PercentOpenSetpoint = Math.Pow(value * 0.01, 0.5) * 100.0; }
            }

            public double EffectivePercentOpen { get { return Math.Max(PercentOpen, Config.MinimumPercentOpen); } }
            public override double EffectiveAreaM2 { get { return EffectivePercentOpen * 0.01 * base.EffectiveAreaM2; } }

            public bool IsOpened { get { return actuatorState.IsAtPos2; } }
            public bool IsClosed { get { return actuatorState.IsAtPos1; } }

            public bool IsOpening { get { return (actuatorState.PosState == ActuatorPosition.MovingToPos2); } }
            public bool IsClosing { get { return (actuatorState.PosState == ActuatorPosition.MovingToPos1); } }

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

        public class ValveConfig : FlowModel.ComponentConfig, ICopyable<ValveConfig>
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
                : base(other)
            {
                InitialValveRequest = other.InitialValveRequest;
                InitialPercentOpenSetpoint = other.InitialPercentOpenSetpoint;
                TimeToOpen = other.TimeToOpen;
                TimeToClose = other.TimeToClose;
                DefaultInBetweenPositionInPercent = other.DefaultInBetweenPositionInPercent;
                MinimumPercentOpen = other.MinimumPercentOpen;
            }

            ValveConfig ICopyable<ValveConfig>.MakeCopyOfThis(bool deepCopy) { return new ValveConfig(this); }

            /// <summary>Gives the initial ValueRequest value for the valve at construction time</summary>
            public ValveRequest InitialValveRequest { get; set; }

            /// <summary>Gives the initial InitialPercentOpenSetpoint value for the valve at construction time.  This will be considered if InitialValveRequest is None or is InBetween</summary>
            public double InitialPercentOpenSetpoint { get; set; }

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
        public class BistableValve : FlowModel.NodePairBase<BistableValveConfig>
        {
            public BistableValve(string name, BistableValveConfig valveConfig)
                : this(name, valveConfig, Fcns.CurrentClassLeafName)
            { }

            public BistableValve(string name, BistableValveConfig valveConfig, string toStringComponentTypeStr)
                : base(name, valveConfig, toStringComponentTypeStr)
            {
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;

                ValvePosition = BistableValvePossition.State1;

                DisableBondingConnectionBetweenChambers = true;
            }

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
                string posnStr = null;

                if (PercentOpenSetpoint == 100.0)
                    posnStr = "[Open]";
                else if (PercentOpenSetpoint == 0.0)
                    posnStr = "[Closed]";

                return "{0} {1} {2:f2} % {3}".CheckedFormat(base.ToString(), ValvePosition, PercentOpenSetpoint, posnStr ?? ValvePosition.ToString());
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

        public class BistableValveConfig : FlowModel.ComponentConfig, ICopyable<BistableValveConfig>
        {
            public BistableValveConfig()
            { }

            /// <summary>
            /// Copy constructor used to build BistableValveConfig values and used to copy config for use by each individual valve instance (so that they do not share BistableValveConfig instances and/or side effects produced when changing BistableValveConfig contents after construction)
            /// </summary>
            public BistableValveConfig(BistableValveConfig other)
                : base(other)
            {
                state1PressureThresholdInKPa = other.state1PressureThresholdInKPa;
                state2PressureThresholdInKPa = other.state2PressureThresholdInKPa;
                State1PercentOpen = other.State1PercentOpen;
                State2PercentOpen = other.State2PercentOpen;
                DebouncePeriod = other.DebouncePeriod;
            }

            BistableValveConfig ICopyable<BistableValveConfig>.MakeCopyOfThis(bool deepCopy) { return new BistableValveConfig(this); }

            /// <summary>Defines the threshold pressure for (End1 - End2) below which the state transitions to State 1</summary>
            public double State1PressureThreshold { get { return PressureUnits.ConvertFromKPA(state1PressureThresholdInKPa); } set { state1PressureThresholdInKPa = PressureUnits.ConvertToKPa(value); } }

            /// <summary>Defines the threshold pressure for (End1 - End2) above which the state transitions to State 2</summary>
            public double State2PressureThreshold { get { return PressureUnits.ConvertFromKPA(state2PressureThresholdInKPa); } set { state2PressureThresholdInKPa = PressureUnits.ConvertToKPa(value); } }

            public double State1PercentOpen { get; set; }
            public double State2PercentOpen { get; set; }

            internal double state1PressureThresholdInKPa, state2PressureThresholdInKPa;

            public TimeSpan DebouncePeriod { get; set; }

            /// <summary>Atmospheric Pop Open Valve.  "Opens" if End1-End2 &gt; 30.0 torr, "Closes" if End1-End2 &lt; 2.0 torr.  PressureUnits = torr, DebouncePeriod = 0.2 seconds</summary>
            public static BistableValveConfig AtmPopOpenValve { get { return new BistableValveConfig() { PressureUnits = PressureUnits.torr, State1PressureThreshold = 2.0, State1PercentOpen = 0.0, State2PressureThreshold = 30.0, State2PercentOpen = 100.0, DebouncePeriod = (0.2).FromSeconds() }; } }
        }
        
        #endregion

        #region Gauge, GaugeConfig

        public class Gauge : FlowModel.ComponentBase<GaugeConfig>
        {
            public Gauge(string name, GaugeConfig config, Chamber chamber)
                : this(name, config, chamber.EffectiveNode)
            { }

            public Gauge(string name, GaugeConfig config, FlowModel.Node observesNode)
                : this(name, config, GetValueSourceDelegate(observesNode, config))
            {
                ObservesNode = observesNode;
            }

            public Gauge(string name, GaugeConfig config, Func<double> rawValueSourceDelegate)
                : base(name, config, Fcns.CurrentClassLeafName)
            {
                RawValueSourceDelegate = rawValueSourceDelegate;
                ServicePhases = ServicePhases.AfterRelaxation | ServicePhases.AfterSetup;

                GaugeMode = Config.InitialGaugeMode.MapDefaultTo(GaugeMode.AllwaysOn);
            }

            /// <summary>Shows the Node that this Gauge is reporting on (from which the gauge gets the flow or pressure).  Note that this property will be null for gauge instances that are looking at a directly specified RawValueSourceDelegate</summary>
            public FlowModel.Node ObservesNode { get; private set; }

            /// <summary>Allows the client to "attach" a gauge to any delgate that can produce a floating point value.</summary>
            public Func<double> RawValueSourceDelegate { get; private set; }

            /// <summary>Gives the most recently observed pressure value in client specified PressureUnits.  Will have noise added if NoiseLevelInPercent is above zero.</summary>
            public double Value { get { return Config.ConvertValueUOM(servicedScaledValueStdUnits, outbound: true); } }

            /// <summary>Gives the most recently observed differential pressure value (Pressure - Config.DifferentialReferencePPressure) in client specified PressureUnits.  Will have noise added if either Config.noise related parameter is above zero.</summary>
            public double DifferentialValue { get { return Config.ConvertValueUOM(servicedScaledDifferentialValueStdUnits, outbound: true); } }

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
            private double servicedDifferentialValueStdUnits;
            private double servicedScaledValueStdUnits;
            private double servicedScaledDifferentialValueStdUnits;
            private bool gaugeWasForcedOff = false;

            public void SetGaugeMode(GaugeMode gaugeMode)
            {
                if (GaugeMode == GaugeMode.AllwaysOn)
                    return;

                GaugeMode = gaugeMode;
                gaugeWasForcedOff = false;
            }

            private static Func<double> GetValueSourceDelegate(FlowModel.Node sourceNode, GaugeConfig config)
            {
                switch (config.GaugeType)
                {
                    case GaugeType.Pressure: return (() => sourceNode.PressureInKPa);
                    case GaugeType.VolumetricFlow: return (() => sourceNode.VolumetricFlowOutOfNodeInSCMS);
                    default: return (() => 0.0);
                }
            }

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                double rawValueInStdUnits = RawValueSourceDelegate();

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

                double rngValueM1P1 = rng.GetNextRandomInMinus1ToPlus1Range();
                double rPercent = rngValueM1P1 * Config.NoiseLevelInPercentOfCurrentValue;    // a number from -noiseLevelInPercent to +noiseLevelInPercent
                double rGeometricNoiseGain = (100.0 + rPercent) * 0.01;            // a number from 1.0-rp  to 1.0+rp  rp is clipped to be no larger than 0.5
                double clippedValueWithNoiseInStdUnits = clippedValueInStdUnits * rGeometricNoiseGain + (rngValueM1P1 * Config.NoiseLevelBase); // add in multaplicative noise and base noise 

                servicedValueStdUnits = (gaugeIsOn ? clippedValueWithNoiseInStdUnits : Config.offValueInStdUnits);
                servicedDifferentialValueStdUnits = (gaugeIsOn ? (servicedScaledValueStdUnits - Config.differentialReferenceValueInStdUnits).Clip(Config.minimumDifferentialValueInStdUnits, Config.maximumDifferentialValueInStdUnits) : Config.offDifferentialValueInStdUnits);
                servicedScaledValueStdUnits = servicedValueStdUnits * Config.ReadingGain;
                servicedScaledDifferentialValueStdUnits = servicedDifferentialValueStdUnits * Config.ReadingGain;

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

        public class GaugeConfig : FlowModel.ComponentConfig, ICopyable<GaugeConfig>
        {
            public GaugeConfig(GaugeType gaugeType = GaugeType.Pressure)
            {
                GaugeType = gaugeType;
                GaugeBehavior = GaugeBehavior.None;

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

                ReadingGain = 1.0;
            }

            public GaugeConfig(GaugeConfig other)
                : base(other)
            {
                GaugeType = other.GaugeType;
                GaugeBehavior = other.GaugeBehavior;
                InitialGaugeMode = other.InitialGaugeMode;

                differentialReferenceValueInStdUnits = other.differentialReferenceValueInStdUnits;
                offValueInStdUnits = other.offValueInStdUnits;
                offDifferentialValueInStdUnits = other.offDifferentialValueInStdUnits;
                minimumValueInStdUnits = other.minimumValueInStdUnits;
                maximumValueInStdUnits = other.maximumValueInStdUnits;
                minimumDifferentialValueInStdUnits = other.minimumDifferentialValueInStdUnits;
                maximumDifferentialValueInStdUnits = other.maximumDifferentialValueInStdUnits;

                NoiseLevelInPercentOfCurrentValue = other.NoiseLevelInPercentOfCurrentValue;
                NoiseLevelBase = other.NoiseLevelBase;
                ReadingGain = other.ReadingGain;
            }

            GaugeConfig ICopyable<GaugeConfig>.MakeCopyOfThis(bool deepCopy) { return new GaugeConfig(this); }

            /// <summary>Shows the type of gauge that has been constructued (to measure Pressure or VolumetricFlow)</summary>
            public GaugeType GaugeType { get; private set; }

            /// <summary>Defines the desired gauge behavior</summary>
            public GaugeBehavior GaugeBehavior { get; set; }

            /// <summary>Defines the initial GaugeMode</summary>
            public GaugeMode InitialGaugeMode { get; set; }

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
            public double NoiseLevelInPercentOfCurrentValue { get; set; }

            /// <summary>Get/Set value gives the base level of noise that is added to the reading in the client selected PressureUnits or VolumetricFlow units (as approrpiate)</summary>
            public double NoiseLevelBase { get; set; }

            /// <summary>Gives a gain factor on the reading that is produced in relation to the actual value source that it is observing.</summary>
            public double ReadingGain { get; set; }

            internal double differentialReferenceValueInStdUnits;
            internal double offValueInStdUnits, offDifferentialValueInStdUnits;
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

        public class Controller : FlowModel.ComponentBase<ControllerConfig>
        {
            public Controller(string name, ControllerConfig config)
                : base(name, config, Fcns.CurrentClassLeafName)
            {
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;

                if (Config.InitialControlMode != ControlMode.None)
                    ControlMode = Config.InitialControlMode;

                setpointActuator = new ActuatorBase("{0}.spa".CheckedFormat(Name), new ActuatorConfig("Off", "Full", Config.FullScaleSetpointRampTime));
                setpointActuatorState = setpointActuator.State;
            }

            private ActuatorBase setpointActuator;
            private IActuatorState setpointActuatorState;

            public double ControlReadback { get { return Config.FeedbackGauge.Value; } }
            public double ControlReadbackInPercentOfFS { get { return ControlReadback * Config.OneOverFullScaleControlValue * 100.0; } }

            public ControlMode ControlMode { get; set; } 

            public double ControlSetpoint { get; set; }
            public double ControlSetpointInPercentOfFS { get { return ControlSetpoint * Config.OneOverFullScaleControlValue * 100.0; } set { ControlSetpoint = value * 0.01 * Config.FullScaleControlValue; } }

            public double SlewLimitedEffectiveControlSetpoint { get { return SlewLimitedEffectiveControlSetpointInPercentOfFS * Config.FullScaleControlValue * 0.01; } }
            public double SlewLimitedEffectiveControlSetpointInPercentOfFS { get { return setpointActuatorState.PositionInPercent; } }

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
            double slewLimitedEffectiveControlSetpoint;
            double controlReadback;
            double useForwardOutputPortionInPercent;
            double useProportionalOutputPortionInPercent;
            double useIntegratorOutputPortionInPercent;
            bool antiWindupTriggered;
            double lastSlewLimitedEffectiveControlSetpoint;
            double lastControlError;
            ControlMode effectiveControlMode;

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                base.Service(servicePhase, measuredServiceInterval, timestampNow);

                if (setpointActuator.Motion1To2Time != Config.FullScaleSetpointRampTime)
                    setpointActuator.Motion1To2Time = setpointActuator.Motion2To1Time = Config.FullScaleSetpointRampTime;

                bool isForced = (forcedControlMode != ControlMode.None);
                effectiveControlMode = isForced ? forcedControlMode : ControlMode;
                
                effectiveControlSetpoint = isForced ? forcedControlSetpoint : ControlSetpoint;

                if (setpointActuatorState.TargetPositionInPercent != effectiveControlSetpoint)
                    setpointActuator.SetTarget(effectiveControlSetpoint * Config.OneOverFullScaleControlValue * 100.0);

                setpointActuator.Service(measuredServiceInterval);
                setpointActuatorState = setpointActuator.State;

                slewLimitedEffectiveControlSetpoint = SlewLimitedEffectiveControlSetpoint;

                controlReadback = ControlReadback;

                ControlError = slewLimitedEffectiveControlSetpoint - controlReadback;

                bool isControlModeNormal = (effectiveControlMode == ControlMode.Normal);

                useForwardOutputPortionInPercent = (slewLimitedEffectiveControlSetpoint * Config.OneOverFullScaleControlValue * Config.ForwardGainInPercentPerFS).Clip(-Config.ForwardRangeInPercent, Config.ForwardRangeInPercent);

                useProportionalOutputPortionInPercent = (ControlError * Config.OneOverFullScaleControlValue * Config.ProportionalGainInPercentPerFSError).Clip(-Config.ProportionalRangeInPercent, Config.ProportionalRangeInPercent);

                double integrationPeriodInSeconds = measuredServiceInterval.Min(Config.IntegralMaxEffectiveDT).TotalSeconds;
                double integrationPeriodErrorIncrement = ControlError * integrationPeriodInSeconds;
                double nextIntegratedError = (IntegratedError + integrationPeriodErrorIncrement);
                double nextUnclippedIntegratorOutputPortionInPercent = (nextIntegratedError * Config.OneOverFullScaleControlValue * Config.IntegralGainInPercentPerFSErrorSec);
                useIntegratorOutputPortionInPercent = nextUnclippedIntegratorOutputPortionInPercent;

                bool antiWindUpSelected = Config.ControllerBehavior.IsSet(ControllerBehavior.AntiWindUp);
                bool permitIntegrationWhileSlewing = Config.ControllerBehavior.IsSet(ControllerBehavior.PermitIntegrationWhileSlewing);

                antiWindupTriggered = (antiWindUpSelected
                                        && ((ControlOutput <= 0.0 && integrationPeriodErrorIncrement < 0.0)     // integrator would be attempting to further saturate the output below 0 %
                                            || (ControlOutput >= 100.0 && integrationPeriodErrorIncrement > 0.0)    // integrator would be attempting to further saturate the output above 100 %
                                            || (Math.Abs(Config.ControlValve.PercentOpenSetpoint - Config.ControlValve.PercentOpen) >= 10.0)  // the control valve is limiting the output setpoint effective rate of change
                                      ))
                                    || ((antiWindUpSelected && !permitIntegrationWhileSlewing) && (slewLimitedEffectiveControlSetpoint != lastSlewLimitedEffectiveControlSetpoint))
                                    ;

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

                double controlOutput = ControlOutput;

                switch (effectiveControlMode)
                {
                    default: 
                    case ControlMode.None:
                    case ControlMode.Hold: break;
                    case ControlMode.Normal: controlOutput = (useForwardOutputPortionInPercent + useProportionalOutputPortionInPercent + useIntegratorOutputPortionInPercent); break;
                    case ControlMode.Open: controlOutput = 100.0; break;
                    case ControlMode.Close: controlOutput = 0.0; break;
                    case ControlMode.Force: controlOutput = isForced ? forcedOutputSetpoint : ControlOutputSetpoint; break;
                }

                if (!Config.ControllerBehavior.IsSet(ControllerBehavior.UseConductanceLinearizedPercentOpenSetpoint))
                    Config.ControlValve.PercentOpenSetpoint = ControlOutput = controlOutput.Clip(Config.MinimumControlOutputInPercent, Config.MaximumControlOutputInPercent, 0.0);
                else
                    Config.ControlValve.ConductanceLinearizedPercentOpenSetpoint = ControlOutput = controlOutput.Clip(Config.MinimumControlOutputInPercent, Config.MaximumControlOutputInPercent, 0.0);

                lastSlewLimitedEffectiveControlSetpoint = slewLimitedEffectiveControlSetpoint;
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
            PermitIntegrationWhileSlewing = 0x02,
            UseConductanceLinearizedPercentOpenSetpoint = 0x04,
        }

        public class ControllerConfig : FlowModel.ComponentConfig, ICopyable<ControllerConfig>
        {
            public ControllerConfig()
            {
                ControllerBehavior = ControllerBehavior.AntiWindUp | ControllerBehavior.PermitIntegrationWhileSlewing;
                InitialControlMode = ControlMode.Normal;
                MinimumControlOutputInPercent = 0.0;
                MaximumControlOutputInPercent = 100.0;
                ForwardRangeInPercent = 100.0;
                ProportionalRangeInPercent = 100.0;
                IntegralRangeInPercent = 100.0;
                IntegralMaxEffectiveDT = (0.1).FromSeconds();
            }

            public ControllerConfig(ControllerConfig other)
                : base(other)
            {
                ControllerType = other.ControllerType;
                FeedbackGauge = other.FeedbackGauge;
                ControlValve = other.ControlValve;
                ControllerBehavior = other.ControllerBehavior;
                InitialControlMode = other.InitialControlMode;
                fullScaleControlValue = other.fullScaleControlValue;
                OneOverFullScaleControlValue = (FullScaleControlValue != 0.0 ? 1.0 / FullScaleControlValue : 0.0);
                FullScaleSetpointRampTime = other.FullScaleSetpointRampTime;
                MinimumControlOutputInPercent = other.MinimumControlOutputInPercent;
                MaximumControlOutputInPercent = other.MaximumControlOutputInPercent;
                ForwardGainInPercentPerFS = other.ForwardGainInPercentPerFS;
                ForwardRangeInPercent = other.ForwardRangeInPercent;
                ProportionalGainInPercentPerFSError = other.ProportionalGainInPercentPerFSError;
                ProportionalRangeInPercent = other.ProportionalRangeInPercent;
                IntegralGainInPercentPerFSErrorSec = other.IntegralGainInPercentPerFSErrorSec;
                IntegralRangeInPercent = other.IntegralRangeInPercent;
                IntegralMaxEffectiveDT = other.IntegralMaxEffectiveDT;
            }

            ControllerConfig ICopyable<ControllerConfig>.MakeCopyOfThis(bool deepCopy) { return new ControllerConfig(this); }

            public ControllerType ControllerType { get; set; }
            public Gauge FeedbackGauge { get; set; }
            public Valve ControlValve { get; set; }

            public ControllerBehavior ControllerBehavior { get; set; }

            public ControlMode InitialControlMode { get; set; }

            /// <summary>
            /// If non-zero value is specified then the specified value is used for the controller, otherwise the given FeedbackGauge's configured MaximumValue is used.
            /// </summary>
            public double FullScaleControlValue { get { return fullScaleControlValue.MapDefaultTo(FeedbackGauge.Config.MaximumValue); } set { fullScaleControlValue = value; } }
            private double fullScaleControlValue;

            public double OneOverFullScaleControlValue { get; private set; }
            public TimeSpan FullScaleSetpointRampTime { get; set; }

            public double MinimumControlOutputInPercent { get; set; }
            public double MaximumControlOutputInPercent { get; set; }

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

        public class Pump : FlowModel.NodePairBase<PumpConfig>
        {
            public Pump(string name, PumpConfig config)
                : this(name, config, Fcns.CurrentClassLeafName)
            { }

            public Pump(string name, PumpConfig config, string toStringComponentTypeStr)
                : base(name, config, toStringComponentTypeStr)
            {
                ServicePhases = ServicePhases.BeforeRelaxation | ServicePhases.AfterSetup;

                ActuatorConfig speedActConfig = new ActuatorConfig("Off", "FullSpeed", Config.TimeToSpinUp, Config.TimeToSpinDown);
                speedActuator = new ActuatorBase("{0}.spd".CheckedFormat(Name), speedActConfig);
                speedActuatorState = speedActuator.State;

                if (Config.InitialPumpMode != PumpMode.None)
                    PumpMode = Config.InitialPumpMode;
            }

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

            public double PowerInWatts { get; private set; }

            public bool IsOff { get { return speedActuatorState.IsAtPos1; } }
            public bool IsAtFullSpeed { get { return speedActuatorState.IsAtPos2; } }
            public bool IsAtLowSpeed { get { return (speedActuatorState.PositionInPercent == Config.LowSpeedInPercentOfFS); } }

            public bool IsSpeedingUp { get { return (speedActuatorState.PosState == ActuatorPosition.MovingToPos2); } }
            public bool IsSlowingDown { get { return (speedActuatorState.PosState == ActuatorPosition.MovingToPos1); } }

            [Obsolete("Please change to using the IsOff, IsAtFullSpeed, IsAtLowSpeed and IsSpeedingUp and IsSlowingDown properties instead (2017-03-23)")]
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
            bool pumpIsEffectivelyOff = false;

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

                if (effectivePumpMode == Components.PumpMode.Off || pumpIsEffectivelyOff)
                    PowerInWatts = 0.0;
                else
                    PowerInWatts = Config.PowerInWattsPerPercentOfFSSpeed * SpeedInPercentOfFS
                                 + Config.powerInWassPerPressureDeltaInKPa * Math.Max(0.0, (End2.PressureInKPa - End1.PressureInKPa))
                                 + Config.powerInWattsPerVolumetricFlowInSCMS * Math.Max(0.0, (End2.VolumetricFlowOutOfNodeInSCMS))
                                 ;
            }

            double effectiveBestBasePressureInKPa;
            double pumpingSpeedDerating;
            double inletDeltaPressureInKPa;

            public override void UpdateFlows()
            {
                pumpIsEffectivelyOff = (speedActuatorState.PositionInPercent <= Config.EffectivelyOffThresholdInPercentOfFS);

                double volumetricFlowOutOfEnd2InSCMS = 0.0;

                double basePipeVolumetricFlowOutOfEnd1InSCMS, basePipeResistanceInKPaSecPerM3;

                GetDefaultPipeFlowRateAndResistance(out basePipeVolumetricFlowOutOfEnd1InSCMS, out basePipeResistanceInKPaSecPerM3);

                double basePipeVolumetricFlowOutOfEnd2InSCMS = -basePipeVolumetricFlowOutOfEnd1InSCMS;

                bool turboAsPipeIsContributing = Config.IsTurboPump && (End1.PressureInKPa > End2.PressureInKPa) && (basePipeVolumetricFlowOutOfEnd2InSCMS > 0.0 || IsBondingConnectionBetweenChambers);

                bool nextDisableBondingConnectionBetweenChambers = Config.IsTurboPump ? !pumpIsEffectivelyOff && !turboAsPipeIsContributing : true;      // bonding is disabled for pumps types other than turbo pumps and for turbo pumps when they are not effectively off.

                if (DisableBondingConnectionBetweenChambers != nextDisableBondingConnectionBetweenChambers)
                    DisableBondingConnectionBetweenChambers = nextDisableBondingConnectionBetweenChambers;

                if (!pumpIsEffectivelyOff && !IsBondingConnectionBetweenChambers)
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

                    if (turboAsPipeIsContributing && basePipeVolumetricFlowOutOfEnd2InSCMS > volumetricFlowOutOfEnd2InSCMS)
                        volumetricFlowOutOfEnd2InSCMS = basePipeVolumetricFlowOutOfEnd1InSCMS;       // at minimum turbo pumps behave like pipes (even when on) if the flow is from End1 to End2.  As such they should have high pumping capacity until the load End1 gets below the backing pressure End2.
                }
                else
                {
                    if (Config.IsTurboPump)
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

        /// <summary>
        /// Defines the type of pump behavior supported by this device.
        /// <para/>None (0x00), RoughingPump (0x01), TurboPump (0x02)
        /// </summary>
        [Flags]
        public enum PumpBehavior : int
        {
            None = 0x00,
            RoughingPump = 0x01,
            TurboPump = 0x02,
        }

        public class PumpConfig : FlowModel.ComponentConfig, ICopyable<PumpConfig>
        {
            public PumpConfig()
            { }

            public PumpConfig(PumpConfig other)
                : base(other)
            {
                PumpBehavior = other.PumpBehavior;
                InitialPumpMode = other.InitialPumpMode;
                FullSpeedInRPM = other.FullSpeedInRPM;
                PumpingSpeedInLperS = other.PumpingSpeedInLperS;
                MaximumCompressionRatio = other.MaximumCompressionRatio;
                nominalMinimumPressureInKPa = other.nominalMinimumPressureInKPa;
                maximallyEfficientPressureInKPa = other.maximallyEfficientPressureInKPa;
                TimeToSpinUp = other.TimeToSpinUp;
                TimeToSpinDown = other.TimeToSpinDown;
                LowSpeedInPercentOfFS = other.LowSpeedInPercentOfFS;
                EffectivelyOffThresholdInPercentOfFS = other.EffectivelyOffThresholdInPercentOfFS;
                PowerInWattsPerPercentOfFSSpeed = other.PowerInWattsPerPercentOfFSSpeed;
                powerInWassPerPressureDeltaInKPa = other.powerInWassPerPressureDeltaInKPa;
                powerInWattsPerVolumetricFlowInSCMS = other.powerInWattsPerVolumetricFlowInSCMS;
            }

            PumpConfig ICopyable<PumpConfig>.MakeCopyOfThis(bool deepCopy) { return new PumpConfig(this); }

            /// <summary>Defines the desired pump behavior</summary>
            public PumpBehavior PumpBehavior { get { return _pumpBehavior; } set { _pumpBehavior = value; IsTurboPump = value.IsSet(PumpBehavior.TurboPump); } }
            private PumpBehavior _pumpBehavior;

            /// <summary>Returns true if the PumpBehavior has PumpBehavior.TurboPump set.</summary>
            public bool IsTurboPump { get; private set; }

            /// <summary>Gives the initial PumpMode value for the pump at construction time</summary>
            public PumpMode InitialPumpMode { get; set; }

            /// <summary>Defines the full scale speed for this pump</summary>
            public double FullSpeedInRPM { get; set; }

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

            /// <summary>Gives the power consumed by this pump in Watts per percent of full spin speed</summary>
            public double PowerInWattsPerPercentOfFSSpeed { get; set; }

            public double PowerInWattsPerPressureDelta { get { return PressureUnits.ConvertFromKPA(powerInWassPerPressureDeltaInKPa); } set { powerInWassPerPressureDeltaInKPa = PressureUnits.ConvertToKPa(value); } }

            internal double powerInWassPerPressureDeltaInKPa;

            public double PowerInWattsPerVolumetricFlow { get { return VolumetricFlowUnits.ConvertFromSCMS(powerInWattsPerVolumetricFlowInSCMS); } set { powerInWattsPerVolumetricFlowInSCMS = VolumetricFlowUnits.ConvertToSCMS(value); } }

            internal double powerInWattsPerVolumetricFlowInSCMS;

            /// <summary>Uses constructor defaults and sets TimeToSpinUpAndDown to 3.0 seconds</summary>
            public static PumpConfig RoughingPump 
            { 
                get 
                { 
                    return new PumpConfig() 
                    { 
                        RadiusInMM = 20.0,
                        LengthInMM = 500.0,
                        PumpBehavior = PumpBehavior.RoughingPump,
                        PressureUnits = PressureUnits.torr, 
                        PumpingSpeedInLperS = 3.0,
                        MaximumCompressionRatio = 1.0e6,        // 1000 / 1e-3
                        NominalMinimumPressure = 1.0e-3,
                        MaximallyEfficientPressure = 1.0e-1,
                        TimeToSpinUpAndDown = (3.0).FromSeconds(), 
                        FullSpeedInRPM = 1800.0, 
                        LowSpeedInPercentOfFS = 50.0,
                        PowerInWattsPerPercentOfFSSpeed = 5.0,
                        PowerInWattsPerPressureDelta = (500.0 / 760.0),
                        VolumetricFlowUnits = VolumetricFlowUnits.slm,
                        PowerInWattsPerVolumetricFlow = 100.0,
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
                        RadiusInMM = 30.0,
                        LengthInMM = 200.0,
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
                        PowerInWattsPerPercentOfFSSpeed = 2.0,
                        VolumetricFlowUnits = VolumetricFlowUnits.slm,
                        PowerInWattsPerVolumetricFlow = 100.0,
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
                        RadiusInMM = 50.0,
                        LengthInMM = 300.0,
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
                        PowerInWattsPerPercentOfFSSpeed = 5.0,
                        VolumetricFlowUnits = VolumetricFlowUnits.slm,
                        PowerInWattsPerVolumetricFlow = 100.0,
                    }; 
                } 
            }
        }

        #endregion

        #region Chamber

        public class Chamber : FlowModel.ComponentBase<ChamberConfig>
        {
            public Chamber(string name, ChamberConfig config)
                : this(name, config, Fcns.CurrentClassLeafName)
            { }

            public Chamber(string name, ChamberConfig config, string toStringComponentTypeStr)
                : base(name, config, toStringComponentTypeStr)
            {
                OriginalNode = new FlowModel.Node(name, chamber: this);
                PressureInKPa = Config.InitialPressureInKPa;
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
                    double radiusM = Config.RadiusInM;
                    return (((Math.PI * 4.0) / 3.0) * radiusM * radiusM * radiusM);
                }
                internal set
                {
                    Config.RadiusInM = Math.Pow((value * (3.0 / 4.0 * Math.PI)), 1.0 / 3.0);
                }
            }

            /// <summary>Returns true if this chamber is a pressure source chamber (RadiusMM is zero)</summary>
            public virtual bool IsSource { get { return Config.IsSource; } }

            /// <summary>Proxy get/set property for OriginalNode.Pressure</summary>
            public double Pressure { get { return OriginalNode.PressureUnits.ConvertFromKPA(EffectiveNode.PressureInKPa); } set { EffectiveNode.PressureInKPa = OriginalNode.PressureUnits.ConvertToKPa(value); } }

            /// <summary>Proxy get/set property for EffectiveNode.PressureInKPa</summary>
            public virtual double PressureInKPa { get { return EffectiveNode.PressureInKPa; } set { EffectiveNode.PressureInKPa = value; } }

            public FlowModel.Node EffectiveNode { get { return effectiveNode ?? OriginalNode; } internal set { effectiveNode = value; } }
            private FlowModel.Node effectiveNode;

            public FlowModel.Node OriginalNode { get; private set; }

            public override string ToString()
            {
                return "{0} p_kPa:{1:e3}{2} f_scms:{3:e3}".CheckedFormat(base.ToString(), EffectiveNode.PressureInKPa, IsSource ? " Src" : "", EffectiveNode.VolumetricFlowOutOfNodeInSCMS);
            }
        }

        public class ChamberConfig : FlowModel.ComponentConfig, ICopyable<ChamberConfig>
        {
            public ChamberConfig() { }
            public ChamberConfig(ChamberConfig other) : base(other) { }

            ChamberConfig ICopyable<ChamberConfig>.MakeCopyOfThis(bool deepCopy) { return new ChamberConfig(this); }

            /// <summary>Returns true if this chamber is a pressure source chamber (Radius is zero)</summary>
            public bool IsSource { get { return (RadiusInM == 0.0); } }

            /// <summary>Sets the chamber to be a fixed pressure source chamber (IsSource == true, RadiusInM == 0.0) and sets its InitialPressure to the given value</summary>
            public double FixedSourcePressure { get { return PressureUnits.ConvertFromKPA(FixedSourcePressureInKPa); } set { FixedSourcePressureInKPa = PressureUnits.ConvertToKPa(value); } }

            /// <summary>Sets the chamber to be a fixed pressure source chamber (IsSource == true, RadiusInM == 0.0) and sets its InitialPressureInKPa to the given value</summary>
            public double FixedSourcePressureInKPa { get { return IsSource ? InitialPressureInKPa : 0.0; } set { InitialPressureInKPa = value; RadiusInM = 0.0; } }
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

    /// <summary>
    /// Flags enumreatinon to define which parts of the FlowModel service loop a given entity it interested in receiving service calls during.
    /// <para/>None (0x00), BeforeRelaxation (0x01), AfterRelaxation (0x02), AtStartOfRelaxationInterval (0x04), AtEndOfRelaxationInterval (0x08), AllRelaxationPhases (0x0f), AfterSetup (0x0100), Manual (0x0200), FaultInjection (0x0400)
    /// </summary>
    [Flags]
    public enum ServicePhases : int
    {
        None = 0x00,

        BeforeRelaxation = 0x01,
        AfterRelaxation = 0x02,
        AtStartOfRelaxationInterval = 0x04,
        AtEndOfRelaxationInterval = 0x08,

        AllRelaxationPhases = (BeforeRelaxation | AfterRelaxation | AtStartOfRelaxationInterval | AtEndOfRelaxationInterval),

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

        public FlowModel Add(IComponent component) 
        {
            IComponentForFlowModel icffm = component as IComponentForFlowModel;
            if (icffm != null)
                icffm.FlowModelConfig = Config;

            componentList.Add(component);

            return this;
        }

        public FlowModel Add(params IComponent[] componentSet)
        {
            AddRange(componentSet);

            return this;
        }

        public FlowModel AddRange(IEnumerable<IComponent> componentSet)
        {
            foreach (var component in componentSet)
                Add(component);

            return this;
        }

        public FlowModel AddItems(params IServiceable[] serviceableSetParamsArray)
        {
            serviceableList.AddRange(serviceableSetParamsArray);

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

        private List<IServiceable> serviceableList = new List<IServiceable>();
        private List<IComponent> componentList = new List<IComponent>();
        private DelegateValueSetAdapter delegateValueSetAdapter = new DelegateValueSetAdapter() { OptimizeSets = true };

        public override void Setup(string scanEnginePartName, IConfig pluginsIConfig, IValuesInterconnection pluginsIVI)
        {
            delegateValueSetAdapter.IssueEmitter = Logger.Debug;
            delegateValueSetAdapter.ValueNoteEmitter = Logger.Trace;
            delegateValueSetAdapter.EmitValueNoteNoChangeMessages = false;

            delegateValueSetAdapter.Setup(pluginsIVI, scanEnginePartName).Set().Update();

            List<IComponent> componentWorkingList = new List<IComponent>(componentList);

            allChambersArray = componentWorkingList.Select(item => item as Chamber).Where(item => item != null).ToArray();
            foreach (var item in allChambersArray)
                componentWorkingList.Remove(item);

            List<NodePairChain> nodePairChainList = new List<NodePairChain>();
            List<INodePair> workingNodePairList = new List<INodePair>(componentWorkingList.Select(item => item as INodePair).Where(item => item != null));

            foreach (var item in workingNodePairList)
                componentWorkingList.Remove(item);

            for (; ; )
            {
                INodePair[] chain = ExtractNextChain(workingNodePairList);
                if (chain.IsNullOrEmpty())
                    break;

                foreach (var item in chain)
                    workingNodePairList.Remove(item);

                NodePairChain nodePairChain = new NodePairChain(chain, Config);
                nodePairChainList.Add(nodePairChain);

                componentList.Add(nodePairChain);
            }

            serviceableBaseArray = serviceableList.Concat(componentList).ToArray();
            componentBaseArray = componentList.ToArray();
            nodePairChainArray = nodePairChainList.ToArray();
            nodePairArray = workingNodePairList.ToArray();
            nodePairChainAndNodePairArray = nodePairChainArray.Concat(nodePairArray).ToArray();

            foreach (var item in serviceableBaseArray)
                item.Setup(scanEnginePartName, Name);

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

            ServiceServiceableItemsAndComponents(ServicePhases.AfterSetup, TimeSpan.Zero, QpcTimeStamp.Now);
        }

        INodePair[] ExtractNextChain(List<INodePair> workingNodePairList)
        {
            INodePair chainHead = workingNodePairList.FirstOrDefault(item => 
                ((item.End1.ConnectedToNodeArray.Length != 1 || item.End1.ConnectedToNodeArray.First().IsChamber) && item.End2.ConnectedToNodeArray.Length == 1 && item.End2.ConnectedToNodeArray.First().NodePair != null)
                );

            if (chainHead == null)
                return null;

            List<INodePair> chainList = new List<INodePair>();

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
            ServiceServiceableItemsAndComponents(ServicePhases.BeforeRelaxation, measuredServiceInterval, timeStampNow);

            TimeSpan remainingTime = measuredServiceInterval;

            do
            {
                ServiceServiceableItemsAndComponents(ServicePhases.AtStartOfRelaxationInterval, measuredServiceInterval, timeStampNow);

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
                                nextDT = TimeSpan.FromTicks(Math.Max((long)(nextDT.Ticks * 0.5), Config.NominalMinimumRelaxationInterval.Ticks));
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

                ServiceServiceableItemsAndComponents(ServicePhases.AtEndOfRelaxationInterval, measuredServiceInterval, timeStampNow);
            } while (remainingTime > TimeSpan.Zero);

            ServiceServiceableItemsAndComponents(ServicePhases.AfterRelaxation, measuredServiceInterval, timeStampNow);
        }

        public override void UpdateOutputs()
        {
            delegateValueSetAdapter.Set();
        }

        private IServiceable[] serviceableBaseArray;
        private IComponent[] componentBaseArray;
        private Chamber[] allChambersArray;
        private Chamber[] singleChambersArray;
        private ChamberSet [] chamberSetArray;
        private Chamber[] chamberSetAndSingleChamberArray;
        private NodePairChain[] nodePairChainArray;
        private INodePair[] nodePairArray;
        private INodePair[] nodePairChainAndNodePairArray;
        private bool[] nodePairChainAndNodePairIsBondingConnectedArray;

        private static readonly ChamberSet[] emptyChamberSetArray = EmptyArrayFactory<ChamberSet>.Instance;

        private Node[] mainNodesArray;
        private int mainNodesArrayLength;
        private double[] nodeFlowsArray;
        private double[] lastNodeFlowsArray;

        private Node[] serviceNodesArray;

        #endregion

        #region outer internals: ServiceComponents

        private void ServiceServiceableItemsAndComponents(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timeStampNow)
        {
            foreach (var item in serviceableBaseArray)
            {
                if (item.ServicePhases.IncludeInPhase(servicePhase))
                    item.Service(servicePhase, measuredServiceInterval, timeStampNow);
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
                    INodePair item = nodePairChainAndNodePairArray[idx];
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
                    List<INodePair> chamberSetInternalNodePairBuildList = new List<INodePair>();

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
            public ChamberSet(Chamber[] chambersInSetArray, INodePair[] stiffConnectionsArray)
                : base("ChSet-{0}".CheckedFormat(String.Join("-", chambersInSetArray.Select(item => item.Name).ToArray())), new ChamberConfig(chambersInSetArray[0].Config), Fcns.CurrentClassLeafName) 
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
                                + BondingConnectionsArray.Sum(item => Physics.Gasses.IdealGasLaw.ToMoles(item.End1.PressureInKPa, item.VolumeInM3, item.TemperatureInDegK))
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
            public INodePair[] BondingConnectionsArray { get; protected set; }

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
            public override double VolumeInM3 { get { return _volumeInM3;} internal set {_volumeInM3 = value;}}
            double _volumeInM3;

            /// <summary>Returns true if this chamber is a pressure source chamber (RadiusMM is zero)</summary>
            public override bool IsSource { get { return FirstSourceChamberInSet != null; } }

            public override string ToString()
            {
                return "{0} p_kPa:{1:e3} f_scms:{2:e3}".CheckedFormat(base.ToString(), EffectiveNode.PressureInKPa, EffectiveNode.VolumetricFlowOutOfNodeInSCMS);
            }
        }

        public class NodePairChain : NodePairBase<ComponentConfig>
        {
            public NodePairChain(INodePair[] nodePairChainArray, FlowModelConfig flowModelConfig)
                : base("Chain-{0}".CheckedFormat(string.Join("-", nodePairChainArray.Select(item => item.Name).ToArray())), new ComponentConfig(nodePairChainArray[0].Config), Fcns.CurrentClassLeafName)
            {
                NodePairChainArray = nodePairChainArray;
                NodePairChainArrayLength = NodePairChainArray.Length;
                nodePairResistanceArray = new double[NodePairChainArrayLength];

                (this as IComponentForFlowModel).FlowModelConfig = flowModelConfig;

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

                Config.LengthInM = totalLengthInM = NodePairChainArray.Sum(item => item.Config.LengthInM);
                _volumeInM3 = NodePairChainArray.Sum(item => item.VolumeInM3);

                ServicePhases = ServicePhases.BeforeRelaxation;
            }

            double totalLengthInM = 0.0;

            public override double VolumeInM3 { get { return _volumeInM3; } }
            private double _volumeInM3;

            public override double ResistanceInKPaSecPerM3 { get { return totalResistanceInKPaSecPerM3; } }
            private double totalResistanceInKPaSecPerM3 = double.PositiveInfinity;

            private Node innerEnd1, innerEnd2;

            public INodePair[] NodePairChainArray { get; private set; }
            public int NodePairChainArrayLength { get; private set; }

            public override void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            {
                base.Service(servicePhase, measuredServiceInterval, timestampNow);

                _volumeInM3 = 0.0;
                bool anyChange = false;

                for (int idx = 0; idx < NodePairChainArrayLength; idx++)
                {
                    var item = NodePairChainArray[idx];
                    item.Service(servicePhase, measuredServiceInterval, timestampNow);

                    _volumeInM3 += item.VolumeInM3;

                    double itemResistance = item.ResistanceInKPaSecPerM3;
                    if (nodePairResistanceArray[idx] != itemResistance)
                    {
                        nodePairResistanceArray[idx] = itemResistance;
                        anyChange = true;
                    }
                }

                if (anyChange)
                {
                    totalResistanceInKPaSecPerM3 = nodePairResistanceArray.Sum();

                    //if (double.IsNaN(totalResistanceInKPaSecPerM3) || totalResistanceInKPaSecPerM3 <= 0.0)
                    //    throw new System.InvalidOperationException("resistance");

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

        #region base and helper classes for components: ComponentBase, Node, NodePair, Serviceable

        public interface IServiceable
        {
            ServicePhases ServicePhases { get; }

            void Setup(string scanEnginePartName, string vacuumSimPartName);
            void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow);
        }

        public interface IComponent : IServiceable
        {
            string Name { get; }
            ComponentConfig Config { get; }

            double VolumeInM3 { get; }
            double VolumeInMM3 { get; }

            double Temperature { get; }
            double TemperatureInDegK { get; }

            void UpdateFlows();
        }

        public interface IComponentForFlowModel
        {
            FlowModelConfig FlowModelConfig { set; }
        }

        public class ComponentBase<TConfigType> : IComponent, IComponentForFlowModel
            where TConfigType : ComponentConfig, ICopyable<TConfigType>
        {
            public ComponentBase(string name, TConfigType config, string toStringComponentTypeStr)
            {
                Name = name;
                Config = config.MakeCopyOfThis();
                ToStringComponentTypeStr = toStringComponentTypeStr;
                TemperatureInDegK = Physics.UnitsOfMeasure.Constants.StandardTemperatureInDegK;
            }

            public string Name { get; protected set; }

            public TConfigType Config { get; private set; }
            ComponentConfig IComponent.Config { get { return Config; } }

            public string ToStringComponentTypeStr { get; private set; }

            public virtual double VolumeInM3 { get; internal set; }
            public double VolumeInMM3 { get { return VolumeInM3 * 1.0e9; } set { VolumeInM3 = value * 1.0e-9; } }

            public double Temperature { get { return Config.TemperatureUnits.ConvertFromDegK(TemperatureInDegK); } set { TemperatureInDegK = Config.TemperatureUnits.ConvertToDegK(value); } }
            public double TemperatureInDegK { get; set; }

            public ServicePhases ServicePhases { get; protected set; }

            public FlowModelConfig FlowModelConfig { get; private set; }
            FlowModelConfig IComponentForFlowModel.FlowModelConfig { set { FlowModelConfig = value; } }

            public virtual void Setup(string scanEnginePartName, string vacuumSimPartName)
            { }

            public virtual void Service(ServicePhases servicePhase, TimeSpan measuredServiceInterval, QpcTimeStamp timestampNow)
            { }

            public virtual void UpdateFlows()
            { }

            public override string ToString()
            {
                if (VolumeInM3 > 0.0)
                    return "{0} '{1}' r_mm:{2:f1} v_m^3:{3:e3}".CheckedFormat(ToStringComponentTypeStr, Name, Config.RadiusInMM, VolumeInM3);
                else if (Config.RadiusInMM > 0.0)
                    return "{0} '{1}' r_mm:{2:f1}".CheckedFormat(ToStringComponentTypeStr, Name, Config.RadiusInMM);
                else
                    return "{0} '{1}'".CheckedFormat(ToStringComponentTypeStr, Name);
            }
        }

        public class ComponentConfig : ICopyable<ComponentConfig>
        {
            public ComponentConfig()
            {
                PressureUnits = PressureUnits.kilopascals;
                VolumetricFlowUnits = VolumetricFlowUnits.sccm;
                TemperatureUnits = TemperatureUnits.DegC;
                EffectiveResistanceGainAdjustment = 1.0;
            }

            public ComponentConfig(ComponentConfig other)
            {
                PressureUnits = other.PressureUnits;
                VolumetricFlowUnits = other.VolumetricFlowUnits;
                TemperatureUnits = other.TemperatureUnits;
                RadiusInM = other.RadiusInM;
                LengthInM = other.LengthInM;
                InitialPressureInKPa = other.InitialPressureInKPa;
                EffectiveResistanceGainAdjustment = other.EffectiveResistanceGainAdjustment;
            }

            ComponentConfig ICopyable<ComponentConfig>.MakeCopyOfThis(bool deepCopy) { return new ComponentConfig(this); }

            /// <summary>Defines the pressure units used for pressure values.  Defaults to PressureUnits.kilopascals</summary>
            public PressureUnits PressureUnits { get; set; }

            /// <summary>Defines the volumetric flow units used for flow values.  Defaults to VolumetricFlowUnits.sccm</summary>
            public VolumetricFlowUnits VolumetricFlowUnits { get; set; }

            /// <summary>Defines the temperature units used for temperature values.  Defaults to TemperatureUnits.DegC</summary>
            public TemperatureUnits TemperatureUnits { get; set; }

            public virtual double RadiusInM { get; set; }
            public double RadiusInMM { get { return RadiusInM * 1000.0; } set { RadiusInM = value * 0.001; } }

            public virtual double LengthInM { get; set; }
            public double LengthInMM { get { return LengthInM * 1000.0; } set { LengthInM = value * 0.001; } }

            public double NominalAreaInM2 { get { return (Math.PI * RadiusInM * RadiusInM); } }
            public double NominalVolumeInM3 { get { return NominalAreaInM2 * LengthInM; } }

            public double InitialPressure { get { return PressureUnits.ConvertFromKPA(InitialPressureInKPa); } set { InitialPressureInKPa = PressureUnits.ConvertToKPa(value); } }
            public double InitialPressureInKPa { get; set; }

            /// <summary>
            /// Allows the modeled component resistance to be adjusted by this gain factor from its natively calculated value.
            /// </summary>
            public double EffectiveResistanceGainAdjustment { get; set; }
        }

        public class Node
        {
            public Node(string name, Chamber chamber = null, INodePair nodePair = null, Node observesNode = null)
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

                Component = (Chamber as IComponent) ?? NodePair ?? observesNode.Component;

                Config = (Component != null) ? Component.Config : new ComponentConfig();

                PressureInKPa = Physics.Gasses.Air.StandardAtmPressureInKPa;
            }

            public string Name { get; private set; }

            public IComponent Component { get; set; }
            public ComponentConfig Config { get; set; }

            public bool IsChamber { get { return (Chamber != null); } }
            public bool IsSource { get { return (IsChamber && Chamber.IsSource); } }

            /// <summary>If this node is connected to one other node and that node is a chamber then this property returns that chamber, otherwise it returns null</summary>
            public Chamber ConnectedToChamber { get { return ((ConnectedToNodeArray.SafeLength() == 1) ? ConnectedToNodeArray[0].Chamber : null) ;} }

            /// <summary>Returns true if the node is connected to a single chamber</summary>
            public bool IsConnectedToChamber { get { return (ConnectedToChamber != null); } }

            public double TemperatureInDegK { get { return (Component != null ? Component.TemperatureInDegK : Physics.UnitsOfMeasure.Constants.StandardTemperatureInDegK); } }

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
            public INodePair NodePair { get; private set; }
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

                    //if (double.IsNaN(summedVolumeM3) || double.IsInfinity(summedVolumeM3) || summedVolumeM3 <= 0.0)
                    //    throw new System.InvalidOperationException("summedVolumeM3");

                    //if (double.IsNaN(TemperatureInDegK) || double.IsInfinity(TemperatureInDegK) || TemperatureInDegK <= 0.0)
                    //    throw new System.InvalidOperationException("TemperatureInDegK");

                    double currentMoles = Physics.Gasses.IdealGasLaw.ToMoles(PressureInKPa, summedVolumeM3, TemperatureInDegK);

                    double deltaVolumeOutOfNodeM3 = summedVolumetricFlowOutOfNodeInSCMS * dt.TotalSeconds;
                    double deltaMolesOutOfNode = Physics.Gasses.IdealGasLaw.ToMoles(Physics.Gasses.Air.StandardAtmPressureInKPa, deltaVolumeOutOfNodeM3, TemperatureInDegK);

                    double updatedMoles = Math.Max(0.0, currentMoles - deltaMolesOutOfNode);

                    updatedPressureInKPa = Physics.Gasses.IdealGasLaw.ToPressureInKPa(updatedMoles, summedVolumeM3, TemperatureInDegK);

                    double maxPressure = 10.0 * Physics.Gasses.Air.StandardAtmPressureInKPa;
                    if (!updatedPressureInKPa.IsInRange(0.0, maxPressure))
                        updatedPressureInKPa = updatedPressureInKPa.Clip(0.0, maxPressure, updatedPressureInKPa);

                    // note: it is expected that the pressure may actually reach zero when the above delta calculation logic overshoots 
                    //if (double.IsNaN(updatedPressureInKPa) || double.IsInfinity(updatedPressureInKPa) || updatedPressureInKPa < 0.0)
                    //    throw new System.InvalidOperationException("updatedPressureInKPa");
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

                if (IsChamber)
                    SetPressureAndDistributeToSubNodes(sharedPressureInKPa);
            }

            public virtual void SetPressureAndDistributeToSubNodes(double pressureInKPa)
            {
                if (!IsChamber && ConnectedToNodeArray.Any(node => node.IsChamber))
                    return; // do not attempt to distribute the pressure from a non-chamber into any connected chamber.

                Node firstSourceChamberNode = ConnectedToNodeArray.FirstOrDefault(node => node.IsSource);

                PressureInKPa = (firstSourceChamberNode != null) ? firstSourceChamberNode.PressureInKPa : pressureInKPa;

                foreach (var subNode in ConnectedToNodeArray)
                {
                    if (!subNode.IsSource)
                        subNode.PressureInKPa = pressureInKPa;
                }
            }
        }

        public interface INodePair : IComponent
        {
            new ComponentConfig Config { get; }

            Node End1 { get; }
            Node End2 { get; }

            bool IsBondingConnectionBetweenChambers { get; }

            double ResistanceInKPaSecPerM3 { get; }

            void DistributePressures();

            /// <summary>
            /// For node pairs that connect two chambers, this method accepts the given testCh and returns the chamber at the first end that is not same instance as the given testChamber instance.
            /// This method will return null if this node pair does not connect the given chamber to another chamber.
            /// </summary>
            Chamber OtherConnectedChamber(Chamber nexSearchCh);
        }

        public class NodePairBase<TConfigType> : ComponentBase<TConfigType>, INodePair
            where TConfigType : ComponentConfig, ICopyable<TConfigType>
        {
            public NodePairBase(string name, TConfigType config, string toStringComponentTypeStr)
                : base(name, config, toStringComponentTypeStr)
            {
                End1 = new Node("{0}.End1".CheckedFormat(name), nodePair: this) { PressureInKPa = config.InitialPressureInKPa };
                End2 = new Node("{0}.End2".CheckedFormat(name), nodePair: this) { PressureInKPa = config.InitialPressureInKPa };
            }

            ComponentConfig INodePair.Config { get { return Config; } }

            public Node End1 { get; protected set; }
            public Node End2 { get; protected set; }

            public override void Setup(string scanEnginePartName, string vacuumSimPartName)
            {
                base.Setup(scanEnginePartName, vacuumSimPartName);

                End1.SetPressureAndDistributeToSubNodes(Config.InitialPressureInKPa);
                End2.SetPressureAndDistributeToSubNodes(Config.InitialPressureInKPa);

                DistributePressures();
            }

            public void GetDefaultPipeFlowRateAndResistance(out double volumetricFlowOutOfEnd1InSCMS, out double resistanceInKPaSecPerM3)
            {
                double pEnd1 = End1.PressureInKPa;
                double pEnd2 = End2.PressureInKPa;

                //if (double.IsNaN(pEnd1) || double.IsInfinity(pEnd1))
                //    throw new System.InvalidOperationException("pEnd1");

                //if (double.IsNaN(pEnd2) || double.IsInfinity(pEnd2))
                //    throw new System.InvalidOperationException("pEnd2");

                double pDiff = pEnd2 - pEnd1;       // we are calculating the flow out of End1 (and into End2) which occurs when p@End2 > p@End1
                double pAvg = (pEnd1 + pEnd2) * 0.5;

                resistanceInKPaSecPerM3 = ResistanceInKPaSecPerM3;

                //if (double.IsNaN(resistanceInKPaSecPerM3) || resistanceInKPaSecPerM3 <= 0.0)
                //    throw new System.InvalidOperationException("resistance");

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

                volumetricFlowOutOfEnd1InSCMS = (!IsBondingConnectionBetweenChambers) ? ((pAvg / Physics.Gasses.Air.StandardAtmPressureInKPa) * flowRateM3PerSec) : 0.0;

                //if (double.IsNaN(volumetricFlowInSCMS) || double.IsInfinity(volumetricFlowInSCMS))
                //    throw new System.InvalidOperationException("volumetricFlowInSCMS");
            }

            public override void UpdateFlows()
            {
                double volumetricFlowOutOfEnd1InSCMS;
                double resistanceInKPaSecPerM3;

                GetDefaultPipeFlowRateAndResistance(out volumetricFlowOutOfEnd1InSCMS, out resistanceInKPaSecPerM3);

                End1.VolumetricFlowOutOfNodeInSCMS = volumetricFlowOutOfEnd1InSCMS;        // (pDiff = pEnd2 - pEnd1) above, or zero if this IsBondingConnectionBetweenChambers
                End2.VolumetricFlowOutOfNodeInSCMS = -volumetricFlowOutOfEnd1InSCMS;

                lastResistanceInKPaSecPerM3 = resistanceInKPaSecPerM3;
            }

            public bool IsBondingConnectionBetweenChambers { get { return isBondingConnectionBetweenChambers && !DisableBondingConnectionBetweenChambers; } protected set { isBondingConnectionBetweenChambers = value; } }
            private bool isBondingConnectionBetweenChambers;
            private double lastResistanceInKPaSecPerM3;
            protected bool DisableBondingConnectionBetweenChambers { get; set; }

            public double EffectiveAreaGain { get { return _effectiveAreaGain; } set { _effectiveAreaGain = value.Clip(0.0, 10.0, 1.0); } }
            private double _effectiveAreaGain = 1.0;

            public override double VolumeInM3 { get { return Config.NominalVolumeInM3; } }

            public virtual double EffectiveAreaM2 { get { return Config.NominalAreaInM2 * _effectiveAreaGain; } }

            public virtual double ResistanceInKPaSecPerM3
            {
                get
                {
                    double areaM2 = EffectiveAreaM2;  // mm^2 => m^2
                    double lengthM = Config.LengthInM; // mm => m

                    //if (double.IsNaN(lengthM) || double.IsInfinity(lengthM) || lengthM <= 0.0)
                    //    throw new System.InvalidOperationException("lengthM");

                    //if (double.IsNaN(areaM2) || double.IsInfinity(areaM2))
                    //    throw new System.InvalidOperationException("areaM2");

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

                    //if (double.IsNaN(resistanceInKPaSecPerM3) || resistanceInKPaSecPerM3 <= 0.0)
                    //    throw new System.InvalidOperationException("resistance");

                    return resistanceInKPaSecPerM3 * Config.EffectiveResistanceGainAdjustment;
                }
            }

            public virtual void DistributePressures()
            {
            }

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
