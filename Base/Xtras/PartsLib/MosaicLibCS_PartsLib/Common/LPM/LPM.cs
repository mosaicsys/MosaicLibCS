//-------------------------------------------------------------------
/*! @file LPM.cs
 *  @brief This file contains common interfaces, class and struct definitions that are used in implementing, using, and displaying LPM Parts and their state.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2016 Mosaic Systems Inc., All rights reserved
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
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Values.Attributes;

namespace MosaicLib.PartsLib.Common.LPM
{
    #region ILPMPart

    public interface ILPMPart : IActivePartBase
	{
    }

    #endregion

    #region LPMartConfigBase

    public class LPMPartConfigBase
	{
        public LPMPartConfigBase(LPMPartConfigBase rhs)
		{
            LPMName = rhs.LPMName;
            Type = rhs.Type;
            PortSpecStr = rhs.PortSpecStr;

            Mapper_Enable = rhs.Mapper_Enable;
            E84_1_Enable = rhs.E84_1_Enable;
            E84_2_Enable = rhs.E84_2_Enable;
            IntegratedRFID_Enable = rhs.IntegratedRFID_Enable;
            SeperateRFID_Type = rhs.SeperateRFID_Type;

            CarrierTypeSpec = new LPMConfigSetAndArrayItems(rhs.CarrierTypeSpec);
		}

        public LPMPartConfigBase(string lpmName, string persistDirBasePathStr) 
            : this ()
        {
            LPMName = lpmName;
        }

        public LPMPartConfigBase()
        {
        }

        public string LPMName { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true)]        // IsOptional so that search path with LPAll does not log errors when it is not found.
        public LPType Type { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true)]        // IsOptional so that search path with LPAll does not log errors when it is not found.
        public string PortSpecStr { get; set; }

        [ConfigItem(Name = "Mapper.Enable", IsOptional = true, ReadOnlyOnce = true)]
        public bool Mapper_Enable { get; set; }

        [ConfigItem(Name = "E84.1.Enable", IsOptional = true, ReadOnlyOnce = true)]
        public bool E84_1_Enable { get; set; }

        [ConfigItem(Name = "E84.2.Enable", IsOptional = true, ReadOnlyOnce = true)]
        public bool E84_2_Enable { get; set; }

        [ConfigItem(Name = "IntegratedRFID.Enable", IsOptional = true, ReadOnlyOnce = true)]
        public LPTagRWType IntegratedRFID_Enable { get; set; }

        [ConfigItem(Name = "SeperateRFID.Type", IsOptional = true, ReadOnlyOnce = true)]
        public LPTagRWType SeperateRFID_Type { get; set; }

        public LPMConfigSetAndArrayItems CarrierTypeSpec = new LPMConfigSetAndArrayItems() { Item1 = "FOUP InfoPads='None'" };

        public LPMPartConfigBase Setup(Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<LPMPartConfigBase> adapter;

            LPMName = LPMName ?? String.Empty;

            // update values from any LPAll derived kesy.  We never log issues when trying to read from LPAll prefixed keys.
            adapter = new ConfigValueSetAdapter<LPMPartConfigBase>() { ValueSet = this, SetupIssueEmitter = Logging.NullEmitter, UpdateIssueEmitter = Logging.NullEmitter, ValueNoteEmitter = valueEmitter }.Setup("LPAll.");
            CarrierTypeSpec.UpdateFromModularConfig("LPAll.CarrierTypeSpec.", Logging.NullEmitter, valueEmitter);

            // update values from any lpmInstanceName derived keys.
            adapter = new ConfigValueSetAdapter<LPMPartConfigBase>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(LPMName + ".");
            CarrierTypeSpec.UpdateFromModularConfig(LPMName + ".CarrierTypeSpec.", issueEmitter, valueEmitter);

            return this;
        }

        public bool IsEqualTo(LPMPartConfigBase rhs)
        {
            return (LPMName == rhs.LPMName
                    && Type == rhs.Type
                    && PortSpecStr == rhs.PortSpecStr
                    && Mapper_Enable == rhs.Mapper_Enable
                    && E84_1_Enable == rhs.E84_1_Enable
                    && E84_2_Enable == rhs.E84_2_Enable
                    && IntegratedRFID_Enable == rhs.IntegratedRFID_Enable
                    && SeperateRFID_Type == rhs.SeperateRFID_Type
                    && CarrierTypeSpec.IsEqualTo(rhs.CarrierTypeSpec)
                    );
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

    public class LPMConfigSetAndArrayItems
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

        public LPMConfigSetAndArrayItems()
        { }

        public LPMConfigSetAndArrayItems(LPMConfigSetAndArrayItems rhs)
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

        public LPMConfigSetAndArrayItems UpdateFromModularConfig(string prefixStr, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<LPMConfigSetAndArrayItems> adapter = new ConfigValueSetAdapter<LPMConfigSetAndArrayItems>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixStr);

            return this;
        }

        internal bool IsEqualTo(LPMConfigSetAndArrayItems rhs)
        {
            return ItemsAsArray.IsEqualTo(rhs.ItemsAsArray);
        }
    }

    #endregion

    #region State related definitions

    [DataContract]
    [Flags]
    public enum PodSensorValues : int
    {
        [EnumMember]
        None = 0x0000,

        [EnumMember]
        PresenceSensor = 0x01,
        [EnumMember]
        PlacementSensor = 0x02,

        [EnumMember]
        ProperyPlaced = PresenceSensor | PlacementSensor,

        /// <summary>InfoPad A</summary>
        [EnumMember]
        A = 0x10,
        /// <summary>InfoPad B</summary>
        [EnumMember]
        B = 0x20,
        /// <summary>InfoPad C</summary>
        [EnumMember]
        C = 0x40,
        /// <summary>InfoPad D</summary>
        [EnumMember]
        D = 0x80,

        /// <summary>InfoPad A,B,C,D</summary>
        [EnumMember]
        InfoPad_All = (A | B | C | D),

        [EnumMember]
        OCA_Present = 0x8000,
        [EnumMember]
        OCA_Cassette_100mm_Present = 0x100,
        [EnumMember]
        OCA_Cassette_150mm_Present = 0x200,
        [EnumMember]
        OCA_Cassette_200mm_Present = 0x400,
    }

    public static partial class ExtensionMethods
    {
        public static bool IsPresent(this PodSensorValues podSensorValues) { return ((podSensorValues & PodSensorValues.PresenceSensor) != PodSensorValues.None); }
        public static bool IsPlaced(this PodSensorValues podSensorValues) { return ((podSensorValues & PodSensorValues.PlacementSensor) != PodSensorValues.None); }
        public static bool IsProperlyPlaced(this PodSensorValues podSensorValues) { return (podSensorValues == PodSensorValues.ProperyPlaced); }

        public static bool DoesPlacedEqualPresent(this PodSensorValues podSensorValues) { return (podSensorValues.IsPlaced() == podSensorValues.IsPlaced()); }
        public static bool IsPlacedOrPresent(this PodSensorValues podSensorValues) { return (podSensorValues.IsPlaced() || podSensorValues.IsPlaced()); }

        public static bool InfoPad_A(this PodSensorValues infoPads) { return ((infoPads & PodSensorValues.A) != PodSensorValues.None); }
        public static bool InfoPad_B(this PodSensorValues infoPads) { return ((infoPads & PodSensorValues.B) != PodSensorValues.None); }
        public static bool InfoPad_C(this PodSensorValues infoPads) { return ((infoPads & PodSensorValues.C) != PodSensorValues.None); }
        public static bool InfoPad_D(this PodSensorValues infoPads) { return ((infoPads & PodSensorValues.D) != PodSensorValues.None); }
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
            CycleCount = 0;
        }

        public PositionState(PositionState rhs)
        {
            ClampState = new ActuatorState(rhs.ClampState);
            DockState = new ActuatorState(rhs.DockState);
            VacState = new ActuatorState(rhs.VacState);
            DoorKeysState = new ActuatorState(rhs.DoorKeysState);
            DoorOpenState = new ActuatorState(rhs.DoorOpenState);
            DoorDownState = new ActuatorState(rhs.DoorDownState);
            CycleCount = rhs.CycleCount;
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

        [DataMember]
        public int CycleCount { get; set; }

        public bool IsEqualTo(PositionState rhs)
        {
            return (rhs != null
                    && ClampState.IsEqualTo(rhs.ClampState)
                    && DockState.IsEqualTo(rhs.DockState)
                    && VacState.IsEqualTo(rhs.VacState)
                    && DoorKeysState.IsEqualTo(rhs.DoorKeysState)
                    && DoorOpenState.IsEqualTo(rhs.DoorOpenState)
                    && DoorDownState.IsEqualTo(rhs.DoorDownState)
                    && CycleCount == rhs.CycleCount
                    );
        }

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

    public class DisplayState
    {
        public const int DefaultNumPanelItems = 8;
        public const int DefaultNumButtonItems = 2;

        public DisplayState()
            : this(DefaultNumPanelItems, DefaultNumButtonItems)
        { }

        public DisplayState(int numPanelItems, int numButtonItems)
        {
            PanelItemArray = Enumerable.Range(1, numPanelItems).Select(ledNum => new DisplayItemState("LED{0}".CheckedFormat(ledNum)) { ItemNum = ledNum }).ToArray();
            ButtonItemArray = Enumerable.Range(1, numButtonItems).Select(buttonNum => new DisplayItemState("Button{0}".CheckedFormat(buttonNum), "White") { ItemNum = buttonNum }).ToArray();
        }

        public DisplayState(DisplayState rhs)
        {
            PanelItemArray = rhs.PanelItemArray.Select(item => new DisplayItemState(item)).ToArray();
            ButtonItemArray = rhs.ButtonItemArray.Select(item => new DisplayItemState(item)).ToArray();
        }

        public DisplayItemState[] ButtonItemArray { get; private set; }
        public DisplayItemState[] PanelItemArray { get; private set; }

        public bool IsEqualTo(DisplayState rhs)
        {
            return (rhs != null
                    && PanelItemArray.IsEqualTo(rhs.PanelItemArray)
                    && ButtonItemArray.IsEqualTo(rhs.ButtonItemArray)
                    );
        }

        public void ServiceFlashing(bool flashState)
        {
            foreach (DisplayItemState item in PanelItemArray)
                item.FlashStateIsOn = flashState;
            foreach (DisplayItemState item in ButtonItemArray)
                item.FlashStateIsOn = flashState;
        }
    }

    public struct ButtonSet
    {
        public bool Button1 { get; set; }
        public bool Button2 { get; set; }
        public bool Button3 { get; set; }
        public bool Button4 { get; set; }

        public bool IsEqual(ButtonSet rhs)
        {
            return (Button1 == rhs.Button1 && Button2 == rhs.Button2 && Button3 == rhs.Button3 && Button4 == rhs.Button4);
        }

        public override string ToString()
        {
            string[] buttonsDown = { (Button1 ? "1" : string.Empty), (Button2 ? "2" : string.Empty), (Button3 ? "3" : string.Empty), (Button4 ? "4" : string.Empty) };

            return string.Join(" ", buttonsDown.Where(s => !s.IsNullOrEmpty()).ToArray()).MapNullOrEmptyTo("None");
        }
    }

    public class DisplayItemState
    {
        public enum OnOffFlashState
        {
            Off = 0,
            On = 1,
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
            ItemNum = rhs.ItemNum;
            Text = rhs.Text;
            BorderColor = rhs.BorderColor;
            OffBackgroundColor = rhs.OffBackgroundColor;
            OnBackgroundColor = rhs.OnBackgroundColor;
            State = rhs.State;
            LastLampCmdState = rhs.LastLampCmdState;
            IsInternal = rhs.IsInternal;
            FlashStateIsOn = rhs.FlashStateIsOn;
        }

        public int ItemNum { get; set; }

        public string Text { get; set; }
        public string BorderColor { get; set; }
        public string OffBackgroundColor { get; set; }
        public string OnBackgroundColor { get; set; }
        public OnOffFlashState State { get; set; }
        public bool IsInternal { get; set; }

        public OnOffFlashState LastLampCmdState { get; set; }

        public bool FlashStateIsOn { get; set; }

        public string CurrentBackgroundColor
        {
            get
            {
                switch (State)
                {
                    case OnOffFlashState.On:
                        return OnBackgroundColor;
                    case OnOffFlashState.Flash:
                        return FlashStateIsOn ? OnBackgroundColor : OffBackgroundColor;
                    default:
                    case OnOffFlashState.Off:
                        return OffBackgroundColor;
                }
            }
        }

        public override bool Equals(object obj)
        {
            return IsEqualTo(obj as DisplayItemState);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return "{0} {1} {2}".CheckedFormat(Text, State, CurrentBackgroundColor);
        }

        public bool IsEqualTo(DisplayItemState rhs)
        {
            return (rhs != null
                    && Text == rhs.Text
                    && BorderColor == rhs.BorderColor
                    && OffBackgroundColor == rhs.OffBackgroundColor
                    && OnBackgroundColor == rhs.OnBackgroundColor
                    && State == rhs.State
                    && IsInternal == rhs.IsInternal
                    && LastLampCmdState == rhs.LastLampCmdState
                    && FlashStateIsOn == rhs.FlashStateIsOn
                    );
        }
    }

    #endregion
}
