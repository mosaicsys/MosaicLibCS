//-------------------------------------------------------------------
/*! @file LPMSim.cs
 *  @brief This file contains common interfaces, class and struct definitions that are used in implementing, using, and displaying LPMSimulator Parts and their state.
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

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Helpers;
using MosaicLib.Semi.E084;
using MosaicLib.Semi.E087;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;

namespace MosaicLib.PartsLib.Common.LPM.Sim
{
    #region ILPMSimPart

    public interface ILPMSimPart : IActivePartBase
	{
        /// <summary>Property gives client access to most recently published copy of LPMSim's state as an ILPMSimPartState object.</summary>
        State PublicState { get; }

        /// <summary>Property gives client access to the part's Guarded Notification Object for the part's PublicState property.</summary>
        INotificationObject<State> PublicStateNotifier { get; }

        /// <summary>Property gives client access to the part's port base state</summary>
        IBaseState PortBaseState { get; }

        /// <summary>Property gives client access to the IBaseState notifier for the part's port</summary>
        INotificationObject<IBaseState> PortBaseStateNotifier { get; }

        /// <summary>Creates a command to set the indicated simulated PIO active inputs to the given pinsState value</summary>
        IBasicAction CreateSetE084ActivePins(PIOSelect pioSelect, IActiveToPassivePinsState pinsState);

        /// <summary>Creates a command to set the indicated simulated PodPresenceSensorState to the given value</summary>
        IBasicAction CreateSetPPSensorState(PodPresenceSensorState sensorState);

        /// <summary>
        /// Creates a GUI Action that is used to handle screen button presses (et. al.) that are directly related to the LPM.
        /// The list of commands includes:
        /// <para/>
        /// "Reset_Port.Clicked", "Button[1..4].{Pressed|Released}", "WaferSlideOut.{On|Off}", "LightCurtainBeamBreak.{On|Off}",
        /// "PinchSensor.{On|Off}", "CDAHasFailed.{On|Off}", "VacHasFailed.{On|Off}"
        /// "SetMapResultPatternIndex='n'", "SetCarrierTypeSpecIndex='n'"
        /// </summary>
        IStringParamAction CreateGUIAction(string guiRequest);

        /// <summary>
        /// Allows an E099TagRWSimEngine instance to inform this LPM that it can be used to simulate tag read/write operations for TagRW instance 1
        /// </summary>
        E099.Sim.ITagRWSimEngine TagRWSimEngine { get; set; }
    }

    #endregion

    #region LPMSimPartConfigBase

    public class LPMSimPartConfigBase
	{
        public LPMSimPartConfigBase(LPMSimPartConfigBase rhs)
		{
            LPMName = rhs.LPMName;
            Type = rhs.Type;
            PortSpecStr = rhs.PortSpecStr;
            PersistDirPathStr = rhs.PersistDirPathStr;

            Mapper_Installed = rhs.Mapper_Installed;
            E84_1_Installed = rhs.E84_1_Installed;
            E84_2_Installed = rhs.E84_2_Installed;
            IntegratedRFID_Type = rhs.IntegratedRFID_Type;
            SeperateRFID_Type = rhs.SeperateRFID_Type;

			ResetInputsOnStart = rhs.ResetInputsOnStart;
			ResetPositionOnStart = rhs.ResetPositionOnStart;
			ResetE84OnStart = rhs.ResetE84OnStart;

            useFastMotion = rhs.useFastMotion;
            VacuumOffMotionTime = rhs.VacuumOffMotionTime;
            VacuumOnMotionTime = rhs.VacuumOnMotionTime;
            ClampMotionTime = rhs.ClampMotionTime;
            TBarMotionTime = rhs.TBarMotionTime;
			MidMotionTime = rhs.MidMotionTime;
			LongMotionTime = rhs.LongMotionTime;

            MapResultPattern = new LPMSimConfigSetAndArrayItems(rhs.MapResultPattern);
            CarrierTypeSpec = new LPMSimConfigSetAndArrayItems(rhs.CarrierTypeSpec);
		}

        public LPMSimPartConfigBase(string lpmName, string persistDirBasePathStr) 
            : this ()
        {
            LPMName = lpmName;
            PersistDirPathStr = Path.Combine(persistDirBasePathStr, lpmName);
        }

        public LPMSimPartConfigBase()
        {
            ResetE84OnStart = true;

            SetMotionTimes(false);
        }

        public string LPMName { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true)]        // IsOptional so that search path with LPAll does not log errors when it is not found.
        public LPType Type { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true)]        // IsOptional so that search path with LPAll does not log errors when it is not found.
        public string PortSpecStr { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true, SilenceIssues=true)]        // this point is almost never used
        public string PersistDirPathStr { get; set; }

        [ConfigItem(Name = "Mapper.Installed", IsOptional = true, ReadOnlyOnce = true)]
        public bool Mapper_Installed { get; set; }

        [ConfigItem(Name = "E84.1.Installed", IsOptional = true, ReadOnlyOnce = true)]
        public bool E84_1_Installed { get; set; }

        [ConfigItem(Name = "E84.2.Installed", IsOptional = true, ReadOnlyOnce = true)]
        public bool E84_2_Installed { get; set; }

        [ConfigItem(Name = "IntegratedRFID.Type", IsOptional = true, ReadOnlyOnce = true)]
        public LPTagRWType IntegratedRFID_Type { get; set; }

        [ConfigItem(Name = "SeperateRFID.Type", IsOptional = true, ReadOnlyOnce = true)]
        public LPTagRWType SeperateRFID_Type { get; set; }

        public bool ResetInputsOnStart { get; set; }
        public bool ResetPositionOnStart { get; set; }
        public bool ResetE84OnStart { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true, SilenceIssues=true)]        // this can be found in either LPAll or LP<n> sections - silence for simplicity.
        public bool UseFastMotion { get { return useFastMotion; } set { SetMotionTimes(value); } }

        private bool useFastMotion = false;

        public TimeSpan VacuumOnMotionTime { get; set; }
        public TimeSpan VacuumOffMotionTime { get; set; }
        public TimeSpan ClampMotionTime { get; set; }
        public TimeSpan TBarMotionTime { get; set; }
        public TimeSpan MidMotionTime { get; set; }         // pivot, shuttle
        public TimeSpan LongMotionTime { get; set; }        // door

        public LPMSimPartConfigBase SetMotionTimes(bool fastMotion)
        {
            this.useFastMotion = fastMotion;

            VacuumOffMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.1 : 0.5);
            VacuumOnMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.1 : 0.25);
            ClampMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.1 : 0.5);
            TBarMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.25 : 0.75);
            MidMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.75 : 1.5);      // pivot, shuttle
            LongMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 1.0 : 3.0);      // door

            return this;
        }

        public LPMSimConfigSetAndArrayItems MapResultPattern = new LPMSimConfigSetAndArrayItems() { Item1 = "ooooooooooooooooooooooooo" };    // 25 correctly occupied slots
        public LPMSimConfigSetAndArrayItems CarrierTypeSpec = new LPMSimConfigSetAndArrayItems() { Item1 = "FOUP InfoPads='None'" };

        public LPMSimPartConfigBase Setup(Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<LPMSimPartConfigBase> adapter;

            LPMName = LPMName ?? String.Empty;

            // update values from any LPAll derived kesy.  We never log issues when trying to read from LPAll prefixed keys.
            adapter = new ConfigValueSetAdapter<LPMSimPartConfigBase>() { ValueSet = this, SetupIssueEmitter = Logging.NullEmitter, UpdateIssueEmitter = Logging.NullEmitter, ValueNoteEmitter = valueEmitter }.Setup("LPAll.");
            MapResultPattern.UpdateFromModularConfig("LPAll.MapResultPattern.", Logging.NullEmitter, valueEmitter);
            CarrierTypeSpec.UpdateFromModularConfig("LPAll.CarrierTypeSpec.", Logging.NullEmitter, valueEmitter);

            // update values from any lpmInstanceName derived keys.
            adapter = new ConfigValueSetAdapter<LPMSimPartConfigBase>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(LPMName + ".");
            MapResultPattern.UpdateFromModularConfig(LPMName + ".MapResultPattern.", issueEmitter, valueEmitter);
            CarrierTypeSpec.UpdateFromModularConfig(LPMName + ".CarrierTypeSpec.", issueEmitter, valueEmitter);

            return this;
        }
    }

    public enum LPType : int
    {
        None = 0,
        Fixload6,
        VisionLPM,
    }

    public enum LPTagRWType : int
    {
        None = 0,
        Hermos,
        BrooksRFID,
    }

    public class LPMSimConfigSetAndArrayItems
    {
        [ConfigItem(Name = "1", IsOptional=true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item1 { get; set; }
        [ConfigItem(Name = "2", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item2 { get; set; }
        [ConfigItem(Name = "3", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item3 { get; set; }
        [ConfigItem(Name = "4", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item4 { get; set; }
        [ConfigItem(Name = "5", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item5 { get; set; }
        [ConfigItem(Name = "6", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item6 { get; set; }
        [ConfigItem(Name = "7", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item7 { get; set; }
        [ConfigItem(Name = "8", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item8 { get; set; }
        [ConfigItem(Name = "9", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item9 { get; set; }
        [ConfigItem(Name = "10", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item10 { get; set; }

        public LPMSimConfigSetAndArrayItems()
        { }

        public LPMSimConfigSetAndArrayItems(LPMSimConfigSetAndArrayItems rhs)
        {
            Item1 = rhs.Item1;
            Item2 = rhs.Item2;
            Item3 = rhs.Item3;
            Item4 = rhs.Item4;
            Item5 = rhs.Item5;
            Item6 = rhs.Item6;
            Item7 = rhs.Item7;
            Item8 = rhs.Item8;
            Item9 = rhs.Item9;
            Item10 = rhs.Item10;
        }

        public string[] ItemsAsArray
        {
            get
            {
                String[] filteredArray = new[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9, Item10 }.TakeWhile((s) => !String.IsNullOrEmpty(s)).ToArray();
                return filteredArray;
            }
        }

        public LPMSimConfigSetAndArrayItems UpdateFromModularConfig(string prefixStr, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<LPMSimConfigSetAndArrayItems> adapter = new ConfigValueSetAdapter<LPMSimConfigSetAndArrayItems>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixStr);

            return this;
        }
    }

    #endregion

    #region State

    [DataContract]
    public class State : Modular.Persist.IPersistSequenceable
    {
        public State()
        {
            LPMName = String.Empty;
            PDODeviceTypeStr = String.Empty;
            SelectedSettings = new SelectedSettings();
            InputsState = new InputsState();
            OutputsState = new OutputsState();
            PositionState = new PositionState();
            DisplayState = new DisplayState();
        }

        public State(LPMSimPartConfigBase config)
            : this()
        {
            LPMName = config.LPMName;
            Config = config;
        }

        public State(State rhs)
        {
            PersistedVersionSequenceNumber = rhs.PersistedVersionSequenceNumber;
            LPMName = rhs.LPMName;
            Config = rhs.Config;
            PDODeviceTypeStr = rhs.PDODeviceTypeStr;
            Connected = rhs.Connected;
            SelectedSettings = new SelectedSettings(rhs.SelectedSettings);
            InputsState = new InputsState(rhs.InputsState);
            OutputsState = new OutputsState(rhs.OutputsState);
            PositionState = new PositionState(rhs.PositionState);
            DisplayState = new DisplayState(rhs.DisplayState);
            CycleCount = rhs.CycleCount;
            CmdRateHz = rhs.CmdRateHz;
        }

        [DataMember]
        public UInt64 PersistedVersionSequenceNumber { get; set; }

        [DataMember]
        public string LPMName { get; set; }

        public LPMSimPartConfigBase Config { get; set; }

        /// <summary>Gives a summary description of the LPM device type</summary>
        [DataMember(IsRequired = false)]
        public String PDODeviceTypeStr { get; set; }

        public bool Connected { get; set; }     // always default to false - is not persisted

        public State UpdateSelectedSettings()
        {
            SelectedSettings.UpdateFrom(Config);

            return this;
        }

        [DataMember]
        public SelectedSettings SelectedSettings { get; set; }

        [DataMember]
        public InputsState InputsState { get; set; }

        [DataMember]
        public OutputsState OutputsState { get; set; }

        [DataMember]
        public PositionState PositionState { get; set; }

        [DataMember]
        public DisplayState DisplayState { get; set; }

        [DataMember]
        public Int32 CycleCount { get; set; }

        public Double CmdRateHz { get; set; }
    }

    [DataContract]
    public class SelectedSettings
    {
        public SelectedSettings() 
        {
        }

        public SelectedSettings(SelectedSettings rhs)
        {
            SelectedMapResultPatternIndex = rhs.SelectedMapResultPatternIndex;
            SelectedCarrierTypeSpecIndex = rhs.SelectedCarrierTypeSpecIndex;

            SelectedMapResultPattern = rhs.SelectedMapResultPattern;
            SelectedMapResultSlotStateList = new List<SlotState>(rhs.SelectedMapResultSlotStateList);

            SelectedCarrierTypeSpec = rhs.SelectedCarrierTypeSpec;
            SelectedCarrierType = rhs.SelectedCarrierType;
            SelectedInfoPads = rhs.SelectedInfoPads;
        }

        public void UpdateFrom(LPMSimPartConfigBase config)
        {
            SelectedMapResultPattern = config.MapResultPattern.ItemsAsArray.SafeAccess(SelectedMapResultPatternIndex, String.Empty);
            SelectedMapResultSlotStateList = SelectedMapResultSlotStateList.Parse(SelectedMapResultPattern);        // fixes null caused by DataContract deserializer.
            
            SelectedCarrierTypeSpec = config.CarrierTypeSpec.ItemsAsArray.SafeAccess(SelectedCarrierTypeSpecIndex, String.Empty);

            StringScanner ss = new StringScanner(SelectedCarrierTypeSpec);
            SelectedCarrierType = ss.ExtractToken();

            InfoPads infoPads = InfoPads.None;
            if (ss.ParseXmlAttribute("InfoPads", out infoPads))
                SelectedInfoPads = infoPads;
        }

        [DataMember(IsRequired = false)]
        public int SelectedMapResultPatternIndex { get; set; }

        [DataMember(IsRequired = false)]
        public int SelectedCarrierTypeSpecIndex { get; set; }

        public string SelectedMapResultPattern { get; private set; }
        public List<SlotState> SelectedMapResultSlotStateList { get; private set; }

        public string SelectedCarrierTypeSpec { get; private set; }
        public string SelectedCarrierType { get; private set; }

        public InfoPads SelectedInfoPads { get; private set; }

        public bool IsEqualTo(SelectedSettings rhs)
        {
            return (SelectedMapResultPatternIndex == rhs.SelectedMapResultPatternIndex
                    && SelectedCarrierTypeSpecIndex == rhs.SelectedCarrierTypeSpecIndex
                    && SelectedMapResultPattern == rhs.SelectedMapResultPattern
                    && SelectedMapResultSlotStateList.IsEqualTo(rhs.SelectedMapResultSlotStateList)
                    && SelectedCarrierTypeSpec == rhs.SelectedCarrierTypeSpec
                    && SelectedCarrierType == rhs.SelectedCarrierType
                    && SelectedInfoPads == rhs.SelectedInfoPads
                    );
        }
    }

    [DataContract]
    [Flags]
    public enum InfoPads : int
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        A = 1,
        [EnumMember]
        B = 2,
        [EnumMember]
        C = 4,
        [EnumMember]
        D = 8,
    }

    [DataContract]
    public class InputsState
    {
        public InputsState() { }
        public InputsState(InputsState rhs)
        {
            PodPresenceSensorState = rhs.PodPresenceSensorState;
            InfoPads = rhs.InfoPads;
            Button1Input = rhs.Button1Input;
            Button2Input = rhs.Button2Input;
            Button3Input = rhs.Button3Input;
            Button4Input = rhs.Button4Input;
            WaferProtrusionSensed = rhs.WaferProtrusionSensed;
            LightCurtainBeamBroken = rhs.LightCurtainBeamBroken;
            PinchSensorTripped = rhs.PinchSensorTripped;
            CDAHasFailed = rhs.CDAHasFailed;
            VacHasFailed = rhs.VacHasFailed;
            E84_OHT_InputBits = rhs.E84_OHT_InputBits;
            E84_AGV_InputBits = rhs.E84_AGV_InputBits;
        }

        [DataMember]
        public PodPresenceSensorState PodPresenceSensorState { get; set; }
        [DataMember]
        public InfoPads InfoPads { get; set; }
        public bool InfoPad_A { get { return ((InfoPads & InfoPads.A) != InfoPads.None); } }
        public bool InfoPad_B { get { return ((InfoPads & InfoPads.B) != InfoPads.None); } }
        public bool InfoPad_C { get { return ((InfoPads & InfoPads.C) != InfoPads.None); } }
        public bool InfoPad_D { get { return ((InfoPads & InfoPads.D) != InfoPads.None); } }

        [DataMember]
        public bool Button1Input { get; set; }
        [DataMember]
        public bool Button2Input { get; set; }
        [DataMember]
        public bool Button3Input { get; set; }
        [DataMember]
        public bool Button4Input { get; set; }
        [DataMember]
        public bool WaferProtrusionSensed { get; set; }
        [DataMember]
        public bool LightCurtainBeamBroken { get { return lightCurtainBeamBroken; } set { lightCurtainBeamBroken = value; e84_oht_InputBits.XferILock = value; } }
        [DataMember]
        public bool PinchSensorTripped { get; set; }
        [DataMember]
        public bool CDAHasFailed { get; set; }
        [DataMember]
        public bool VacHasFailed { get; set; }
        [DataMember]
        public ActiveToPassivePinsState E84_OHT_InputBits { get { return e84_oht_InputBits; } set { e84_oht_InputBits = value; e84_oht_InputBits.XferILock = lightCurtainBeamBroken; } }
        [DataMember]
        public ActiveToPassivePinsState E84_AGV_InputBits { get { return e84_agv_InputBits; } set { e84_agv_InputBits = value; } }

        public ActiveToPassivePinsState GetE84InputsBits(PIOSelect pioSelect) { return ((pioSelect == PIOSelect.OHT) ? E84_OHT_InputBits : E84_AGV_InputBits); }

        private bool lightCurtainBeamBroken = false;
        private ActiveToPassivePinsState e84_oht_InputBits, e84_agv_InputBits;
    }

    [DataContract]
    public struct PodPresenceSensorState
    {
        [DataMember]
        public bool PresentSensorInput { get; set; }
        [DataMember]
        public bool PlacedSensorInput { get; set; }

        public static PodPresenceSensorState FoupNotPresentAndNotPlaced { get { return new PodPresenceSensorState(); } }
        public static PodPresenceSensorState FoupPresentAndNotPlaced { get { return new PodPresenceSensorState() { PresentSensorInput = true }; } }
        public static PodPresenceSensorState FoupPresentAndPlaced { get { return new PodPresenceSensorState() { PresentSensorInput = true, PlacedSensorInput = true }; } }
        public static PodPresenceSensorState FoupInvalid_NotPresentAndPlaced { get { return new PodPresenceSensorState() { PlacedSensorInput = true }; } }

        public bool IsPlacedAndPresent { get { return (PlacedSensorInput && PresentSensorInput); } }
        public bool IsPlacedOrPresent { get { return (PlacedSensorInput || PresentSensorInput); } }
        public bool DoesPlacedEqualPresent { get { return (PlacedSensorInput == PresentSensorInput); } }
        public bool IsNeitherPlacedNorPresent { get { return (!PlacedSensorInput && !PresentSensorInput); } }

        public override string ToString()
        {
            return Utils.Fcns.CheckedFormat("{0}Present,{1}Placed", (PresentSensorInput ? "" : "!"), (PlacedSensorInput ? "" : "!"));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object rhsAsObj)
        {
            if (rhsAsObj == null || !(rhsAsObj is PodPresenceSensorState))
                return false;
            PodPresenceSensorState rhs = (PodPresenceSensorState) rhsAsObj;

            return IsEqualTo(rhs);
        }

        public bool IsEqualTo(PodPresenceSensorState rhs)
        {
            bool isEqual = (PresentSensorInput == rhs.PresentSensorInput
                            && PlacedSensorInput == rhs.PlacedSensorInput);

            return isEqual;
        }
    }

    [DataContract]
    public class OutputsState
    {
        public OutputsState() { }
        public OutputsState(OutputsState rhs)
        {
            E84_OHT_OutputBits = rhs.E84_OHT_OutputBits;
            E84_AGV_OutputBits = rhs.E84_AGV_OutputBits;
        }

        [DataMember]
        public PassiveToActivePinsState E84_OHT_OutputBits { get; set; }
        [DataMember]
        public PassiveToActivePinsState E84_AGV_OutputBits { get; set; }

        public IPassiveToActivePinsState GetE84OutputBits(PIOSelect pioSelect) { return ((pioSelect == PIOSelect.OHT) ? E84_OHT_OutputBits : E84_AGV_OutputBits); }
    }

    [DataContract]
    public class PositionState
    {
        public PositionState()
        {
            ClampState = new ActuatorState();
            DockState = new ActuatorState();
            VacState = new ActuatorState();
            DoorKeysState = new ActuatorState();
            DoorOpenState = new ActuatorState();
            DoorDownState = new ActuatorState();
        }

        public PositionState(PositionState rhs)
        {
            ClampState = new ActuatorState(rhs.ClampState);
            DockState = new ActuatorState(rhs.DockState);
            VacState = new ActuatorState(rhs.VacState);
            DoorKeysState = new ActuatorState(rhs.DoorKeysState);
            DoorOpenState = new ActuatorState(rhs.DoorOpenState);
            DoorDownState = new ActuatorState(rhs.DoorDownState);
        }

        [DataMember]
        public ActuatorState ClampState { get; set; }
        [DataMember]
        public ActuatorState DockState { get; set; }
        [DataMember]
        public ActuatorState VacState { get; set; }
        [DataMember]
        public ActuatorState DoorKeysState { get; set; }
        [DataMember]
        public ActuatorState DoorOpenState { get; set; }
        [DataMember]
        public ActuatorState DoorDownState { get; set; }

        public bool IsInMotion
        {
            get
            {
                return (ClampState.IsInMotion || DockState.IsInMotion || VacState.IsInMotion
                        || DoorKeysState.IsInMotion || DoorOpenState.IsInMotion || DoorDownState.IsInMotion);
            }
        }

        public bool IsValid
        {
            get
            {
                return (ClampState.IsValid && DockState.IsValid && VacState.IsValid
                        && DoorKeysState.IsValid && DoorOpenState.IsValid && DoorDownState.IsValid);
            }
        }

        public bool IsClamped { get { return ClampState.IsAtPos2; } }
        public bool IsUnclamped { get { return ClampState.IsAtPos1; } }
        public bool IsDocked { get { return DockState.IsAtPos2; } }
        public bool IsUndocked { get { return DockState.IsAtPos1; } }
        /// <summary>True if vacuum is enabled to the suction cups</summary>
        public bool IsVacEnabled { get { return VacState.IsAtPos2; } }
        /// <summary>True if vacuum is disabled from the suction cups</summary>
        public bool IsVacDisabled { get { return VacState.IsAtPos1; } }
        public bool AreDoorKeysHorizontal { get { return DoorKeysState.IsAtPos2; } }
        public bool AreDoorKeysVertical { get { return DoorKeysState.IsAtPos1; } }
        public bool IsDoorOpen { get { return DoorOpenState.IsAtPos2; } }
        public bool IsDoorClosed { get { return DoorOpenState.IsAtPos1; } }
        public bool IsDoorDown { get { return DoorDownState.IsAtPos2; } }
        public bool IsDoorUp { get { return DoorDownState.IsAtPos1; } }

        public bool IsCarrierOpen { get { return IsDoorOpen && IsDoorDown; } }
        public bool IsCarrierClosed { get { return IsDoorUp && IsDoorClosed && AreDoorKeysVertical && IsVacDisabled; } }
    }

    public enum PositionSummary
    {
        Unknown,
        UndockedEmpty,
        UndockedPresent,
        UndockedPlaced,
        UndockedClamped,
        DockedDoorLocked,
        DockedDoorUnlocked,
        DockedDoorOpen,
        DockedDoorDown,
    }

    [DataContract]
    public class DisplayState
    {
        public const int NumPanelItems = 8;
        public const int NumButtonItems = 2;

        public DisplayState()
        {
            int idx;
            PanelItemArray = new DisplayItemState[NumPanelItems];
            for (idx = 0; idx < NumPanelItems; idx++)
                PanelItemArray[idx] = new DisplayItemState(Utils.Fcns.CheckedFormat("LED{0}", idx + 1));

            ButtonItemArray = new DisplayItemState[NumButtonItems];
            for (idx = 0; idx < NumButtonItems; idx++)
                ButtonItemArray[idx] = new DisplayItemState(Utils.Fcns.CheckedFormat("Button{0}", idx + 1), "White");
        }

        public DisplayState(DisplayState rhs)
        {
            int idx;
            PanelItemArray = new DisplayItemState[rhs.PanelItemArray.Length];
            for (idx = 0; idx < rhs.PanelItemArray.Length; idx++)
                PanelItemArray[idx] = new DisplayItemState(rhs.PanelItemArray[idx]);

            ButtonItemArray = new DisplayItemState[rhs.ButtonItemArray.Length];
            for (idx = 0; idx < rhs.ButtonItemArray.Length; idx++)
                ButtonItemArray[idx] = new DisplayItemState(rhs.ButtonItemArray[idx]);
        }

        [DataMember]
        public DisplayItemState[] ButtonItemArray { get; set; }
        [DataMember]
        public DisplayItemState[] PanelItemArray { get; set; }

        public override int GetHashCode() { return base.GetHashCode(); }

        public override bool Equals(object rhsAsObj)
        {
            DisplayState rhs = rhsAsObj as DisplayState;
            if (rhs == null)
                return false;

            if (PanelItemArray.Length != rhs.PanelItemArray.Length || ButtonItemArray.Length != rhs.ButtonItemArray.Length)
                return false;

            int idx;
            for (idx = 0; idx < PanelItemArray.Length; idx++)
            {
                if (!PanelItemArray[idx].Equals(rhs.PanelItemArray[idx]))
                    return false;
            }

            for (idx = 0; idx < ButtonItemArray.Length; idx++)
            {
                if (!ButtonItemArray[idx].Equals(rhs.ButtonItemArray[idx]))
                    return false;
            }

            return true;
        }
    }

    [DataContract]
    public class DisplayItemState
    {
        [DataContract]
        public enum OnOffFlashState
        {
            [EnumMember]
            Off = 0,
            [EnumMember]
            On = 1,
            [EnumMember]
            Flash = 2,
        };

        public DisplayItemState() : this("------", "Black", false) { }
        public DisplayItemState(string text) : this(text, "Black", false) { }
        public DisplayItemState(string text, string colorFamily) : this(text, colorFamily, false) { }
        public DisplayItemState(string text, string colorFamily, bool isInternal)
        {
            Text = text;
            State = OnOffFlashState.Off;
            IsInternal = isInternal;

            switch (colorFamily)
            {
                default:
                case "": BorderColor = "DarkGray"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "LightGray"; break;
                case "Black": BorderColor = "Black"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "LightGray"; break;
                case "Red": BorderColor = "DarkRed"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "Red"; break;
                case "Orange": BorderColor = "DarkOrange"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "Orange"; break;
                case "Green": BorderColor = "DarkGreen"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "Green"; break;
                case "Yellow": BorderColor = "DarkGoldenrod"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "Yellow"; break;
                case "Blue": BorderColor = "DarkBlue"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "Blue"; break;
                case "Magenta": BorderColor = "DarkMagenta"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "Magenta"; break;
                case "Cyan": BorderColor = "DarkCyan"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "Cyan"; break;
                case "White": BorderColor = "Gray"; OffBackgroundColor = "DarkGray"; OnBackgroundColor = "White"; break;
            }
        }

        public DisplayItemState(DisplayItemState rhs)
        {
            Text = rhs.Text;
            BorderColor = rhs.BorderColor;
            OffBackgroundColor = rhs.OffBackgroundColor;
            OnBackgroundColor = rhs.OnBackgroundColor;
            State = rhs.State;
            LastLampCmdState = rhs.LastLampCmdState;
            IsInternal = rhs.IsInternal;
        }

        [DataMember]
        public String Text { get; set; }
        [DataMember]
        public String BorderColor { get; set; }
        [DataMember]
        public String OffBackgroundColor { get; set; }
        [DataMember]
        public String OnBackgroundColor { get; set; }
        [DataMember]
        public OnOffFlashState State { get; set; }
        [DataMember]
        public bool IsInternal { get; set; }

        public OnOffFlashState LastLampCmdState { get; set; }

        public override int GetHashCode() { return base.GetHashCode(); }

        public override bool Equals(object rhsAsObj)
        {
            DisplayItemState rhs = rhsAsObj as DisplayItemState;
            return (rhs != null
                    && Text == rhs.Text
                    && BorderColor == rhs.BorderColor
                    && OffBackgroundColor == rhs.OffBackgroundColor
                    && OnBackgroundColor == rhs.OnBackgroundColor
                    && State == rhs.State
                    && LastLampCmdState == rhs.LastLampCmdState
                    && IsInternal == rhs.IsInternal);
        }
    }

    #endregion
}
