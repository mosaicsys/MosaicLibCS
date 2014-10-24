//-------------------------------------------------------------------
/*! @file LPMSim.cs
 *  @brief This file contains common interfaces, class and struct defintions that are used in implementing, using, and displaying LPMSimulator Parts and their state.
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

namespace MosaicLib.PartsLib.Common.LPM.Sim
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using MosaicLib.Utils;
    using MosaicLib.Time;
    using MosaicLib.Modular;
    using MosaicLib.Modular.Part;
    using MosaicLib.PartsLib.Helpers;
    using MosaicLib.Semi.E084;

    #region ILPMSimPart

    public interface ILPMSimPart : IActivePartBase
	{
        /// <summary>Property gives client access to most recently published copy of LPMSim's state as an ILPMSimPartState object.</summary>
        State PublicState { get; }

        /// <summary>Property gives client access to the part's Guarded Notificdation Object for the part's PublicState property.</summary>
        INotificationObject<State> PublicStateNotifier { get; }

        /// <summary>Property gives client access to the part's port base state</summary>
        IBaseState PortBaseState { get; }

        /// <summary>Property gives client access to the IBaseState notifier for the part's port</summary>
        INotificationObject<IBaseState> PortBaseStateNotifier { get; }

        /// <summary>Creates a command to set the indicated simulated PIO active inputs to the given pinsState value</summary>
        IBasicAction CreateSetE084ActivePins(PIOSelect pioSelect, IActiveToPassivePinsState pinsState);

        /// <summary>Creates a command to set the indicated simulated PodPresenceSensorState to the given value</summary>
        IBasicAction CreateSetPPSensorState(PodPresenceSensorState sensorState);

        IStringParamAction CreateGUIAction(string guiRequest);
    }

    #endregion

    #region LPMSimPartConfigBase

    public class LPMSimPartConfigBase
	{
        public LPMSimPartConfigBase(string lpmName, string portSpecStr, string persistDirBasePathStr, bool fastMotion)
            : this(lpmName, portSpecStr, persistDirBasePathStr)
		{ 
            FastMotion = fastMotion;

            if (FastMotion)
			{
				ShortMotionTime = TimeSpan.FromSeconds(0.5);
				MidMotionTime = TimeSpan.FromSeconds(0.75);
				LongMotionTime = TimeSpan.FromSeconds(1.0);
			}
		}

        public LPMSimPartConfigBase(LPMSimPartConfigBase rhs)
            : this(rhs.LPMName, rhs.PortSpecStr, rhs.PersistDirPathStr)
		{
			ResetInputsOnStart = rhs.ResetInputsOnStart;
			ResetPositionOnStart = rhs.ResetPositionOnStart;
			ResetDefaultMapOnStart = rhs.ResetDefaultMapOnStart;
			ResetE84OnStart = rhs.ResetE84OnStart;
            FastMotion = rhs.FastMotion;
			ShortMotionTime = rhs.ShortMotionTime;
			MidMotionTime = rhs.MidMotionTime;
			LongMotionTime = rhs.LongMotionTime;
			DefaultMapResult = rhs.DefaultMapResult;
		}

        private LPMSimPartConfigBase(string lpmName, string portSpecStr, string persistDirBasePathStr) : this ()
        {
            this.LPMName = lpmName;
            this.PortSpecStr = portSpecStr;
            this.PersistDirPathStr = Path.Combine(persistDirBasePathStr, lpmName);
        }

        public LPMSimPartConfigBase()
        {
            ResetDefaultMapOnStart = true;
            ResetE84OnStart = true;
		    ShortMotionTime = TimeSpan.FromSeconds(0.75);       // t-bar, vacuum, clamp
		    MidMotionTime = TimeSpan.FromSeconds(1.5);         // pivot, shuttle
		    LongMotionTime = TimeSpan.FromSeconds(3.0);        // door

		    DefaultMapResult = "3333333333333333333333333";		// 25 wafers in CorrectlyOccupied state.
        }

        public string LPMName { get; set; }
        public string PortSpecStr { get; set; }
        public string PersistDirPathStr { get; set; }

        public bool ResetInputsOnStart { get; set; }
        public bool ResetPositionOnStart { get; set; }
        public bool ResetDefaultMapOnStart { get; set; }
        public bool ResetE84OnStart { get; set; }

        public bool FastMotion { get; private set; }
        public TimeSpan ShortMotionTime { get; set; }       // t-bar, vacuum, clamp
        public TimeSpan MidMotionTime { get; set; }         // pivot, shuttle
        public TimeSpan LongMotionTime { get; set; }        // door

        public string DefaultMapResult { get; set; }		// 25 wafers in CorrectlyOccupied state.
    }

    #endregion

    #region State

    [DataContract]
    public class State : Modular.Persist.IPersistSequenceable
    {
        public State()
        {
            LPMName = String.Empty;
            MapResultSetting = String.Empty;
            InputsState = new InputsState();
            OutputsState = new OutputsState();
            PositionState = new PositionState();
            DisplayState = new DisplayState();
        }

        public State(LPMSimPartConfigBase config)
            : this()
        {
            LPMName = config.LPMName;
            MapResultSetting = config.DefaultMapResult;
        }

        public State(State rhs)
        {
            PersistedVersionSequenceNumber = rhs.PersistedVersionSequenceNumber;
            LPMName = rhs.LPMName;
            Connected = rhs.Connected;
            MapResultSetting = rhs.MapResultSetting;
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

        public bool Connected { get; set; }     // always default to false - is not persisted

        [DataMember]
        public string MapResultSetting { get; set; }

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
    public class InputsState
    {
        public InputsState() { }
        public InputsState(InputsState rhs)
        {
            PodPresenceSensorState = rhs.PodPresenceSensorState;
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
