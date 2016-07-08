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
using MosaicLib.Modular.Common;

namespace MosaicLib.PartsLib.Common.LPM
{
    #region ILPMPart

    /// <summary>
    /// This interface defines the set of action factory methods and state publication that is required of parts that would like to support generic use
    /// as a PDO device based on the requirements that are provided here.
    /// </summary>
    /// <remarks>
    /// In addition to the formal requirments defined here, such PDO driver parts are also expected to support the following IVA names
    /// <para/>"LPMState": contains the last published ILPMState object for gui data binding use (et. al.)
    /// </remarks>
    public interface ILPMPart : IActivePartBase
	{
        /// <summary>
        /// Action factory method.  When run, this action will cause this part to initialize the load port.  
        /// This is noinally the same as creating and running a GoOnline(true) action.
        /// </summary>
        IBasicAction Initialize();

        /// <summary>
        /// This property can be used to attempt to obtain interface access to the TagRW capabilities of this part, if it supports them.  If the part does not support such capabilites right now then it will return true.
        /// Typically this property getter is only used after the part has successfully been placed OnLine and has been Initialized.  For some devide types, the part cannot know if the physical hardware actually supports the corresponding functionality until the connection has been fully established.
        /// In these cases the part will return false on for this property until the part has confirmed that the target device actually supports the required functions.
        /// </summary>
        E099.ITagRWPart TagRW { get; }

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to clamp the carrier that has been placed.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// </summary>
        IBasicAction Clamp();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to unclamp the carrier that has been placed.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// </summary>
        IBasicAction Unclamp();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to dock the carrier that has been placed.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// </summary>
        IBasicAction Dock();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to undock, but not unclmap, the carrier that has been placed.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// </summary>
        IBasicAction Undock();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to open the carrier that has been placed.
        /// if requestMap is true and the device supports mapping for the current carrier type, then the carrier will be mapped as the door is being opened.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// </summary>
        IBasicAction Open(bool requestMap);

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to close the carrier that has been placed.
        /// if requestMap is true and the device supports mapping for the current carrier type, then the carrier will be mapped as the door is being closed.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// </summary>
        IBasicAction Close(bool requestMap);

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to map the carrier that has been placed.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// </summary>
        IBasicAction Map();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to clear any previously produced map result.
        /// </summary>
        IBasicAction ClearMap();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to explicitly run all normally used status commands, and then update and publish all related state information (if needed).
        /// </summary>
        IBasicAction Sync();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to update the E84OutputSetpoint from the given value, run the commands needed to update the device's copy of this and to verify that it was successfully applied to the E84 outputs (subject to masking contraints on HO and ES per any light curtain interlock information)
        /// </summary>
        IBasicAction SetE84OutputSetpoint(IPassiveToActivePinsState setpoint);

        /// <summary>
        /// Action factory method.  When run, if the given decodedPodInfo is non-null, this action will cause this part replace its internally determined decodedPodInfo with the copy of the given value, publish the change, and apply the given value to configure how future motion actions are performed.
        /// If the given decodedPodInfo is null then this action will cause this part to return to using its normal internal logic for decoding, publishing and using the decoded pod into.
        /// </summary>
        IBasicAction SetDecodedPodInfoOverride(IDecodedPodInfo decodedPodInfo, bool andInitialize);

        /// <summary>
        /// Action factory method.  When run, this action will replace any previously provided IPortDisplayContextInfo publisher with the given value.
        /// </summary>
        IBasicAction SetPortDisplayContextInfoPublisher(INotificationObject<IPortDisplayContextInfo> publisher);

        /// <summary>ILPMState state publisher.  This is also a notification object</summary>
        INotificationObject<ILPMState> StatePublisher { get; }
    }

    #endregion

    #region LPMState

    public interface ILPMState
    {
        INamedValueSet NVS { get; }

        IDeviceCapabilities DeviceCapabilities { get; }
        IPodSensorValues PodSensorValues { get; }
        IDecodedPodInfo DecodedPodInfo { get; }
        IPositionState PositionState { get; }

        IDisplayState DisplayStateSetpoint { get; }
        IDisplayState DisplayState { get; }
        IButtonSet ButtonSet { get; }

        IPassiveToActivePinsState E84OutputSetpoint { get; }
        IPassiveToActivePinsState E84OutputReadback { get; }
        IActiveToPassivePinsState E84Inputs { get; }

        IMapResults MapResults { get; }

        IPortDisplayContextInfo PortDisplayContextInfo { get; }

        bool IsEqualTo(ILPMState rhs);
    }

    public class LPMState : ILPMState
    {
        public LPMState()
            : this(true)
        { }

        private LPMState(bool initialize)
        {
            if (initialize)
            {
                DeviceCapabilities = new DeviceCapabilities();
                PodSensorValues = new PodSensorValues();
                DecodedPodInfo = new DecodedPodInfo();
                PositionState = new PositionState();

                DisplayStateSetpoint = new DisplayState();
                DisplayState = new DisplayState();
                ButtonSet = new ButtonSet();

                E84OutputSetpoint = new PassiveToActivePinsState();
                E84OutputReadback = new PassiveToActivePinsState();
                E84Inputs = new ActiveToPassivePinsState();
                MapResults = new MapResults();
            }
        }

        public LPMState(ILPMState rhs)
        {
            SetFrom(rhs);
        }

        public LPMState SetFrom(ILPMState rhs)
        {
            NVS = rhs.NVS.IsNullOrEmpty() ? null : new NamedValueSet(rhs.NVS);

            DeviceCapabilities = new DeviceCapabilities(rhs.DeviceCapabilities);
            PodSensorValues = new PodSensorValues(rhs.PodSensorValues);
            DecodedPodInfo = new DecodedPodInfo(rhs.DecodedPodInfo);
            PositionState = new PositionState(rhs.PositionState);

            DisplayStateSetpoint = new DisplayState(rhs.DisplayStateSetpoint);
            DisplayState = new DisplayState(rhs.DisplayState);
            ButtonSet = new ButtonSet(rhs.ButtonSet);

            E84OutputSetpoint = new PassiveToActivePinsState(rhs.E84OutputSetpoint);
            E84OutputReadback = new PassiveToActivePinsState(rhs.E84OutputReadback);
            E84Inputs = new ActiveToPassivePinsState(rhs.E84Inputs);
            MapResults = new MapResults(rhs.MapResults);

            PortDisplayContextInfo = rhs.PortDisplayContextInfo;
            
            return this;
        }

        INamedValueSet ILPMState.NVS { get { return nvs ?? NamedValueSet.Empty; } }

        IDeviceCapabilities ILPMState.DeviceCapabilities { get { return DeviceCapabilities; } }
        IPodSensorValues ILPMState.PodSensorValues { get { return this.PodSensorValues; } }
        IDecodedPodInfo ILPMState.DecodedPodInfo { get { return this.DecodedPodInfo; } }
        IPositionState ILPMState.PositionState { get { return this.PositionState; } }
        IDisplayState ILPMState.DisplayStateSetpoint { get { return this.DisplayStateSetpoint; } }
        IDisplayState ILPMState.DisplayState { get { return this.DisplayState; } }
        IButtonSet ILPMState.ButtonSet { get { return this.ButtonSet; } }
        IPassiveToActivePinsState ILPMState.E84OutputSetpoint { get { return this.E84OutputSetpoint; } }
        IPassiveToActivePinsState ILPMState.E84OutputReadback { get { return this.E84OutputReadback; } }
        IActiveToPassivePinsState ILPMState.E84Inputs { get { return this.E84Inputs; } }
        IMapResults ILPMState.MapResults { get { return this.MapResults; } }
        IPortDisplayContextInfo ILPMState.PortDisplayContextInfo { get { return this.PortDisplayContextInfo; } }

        public NamedValueSet NVS { get { return (nvs ?? (nvs = new NamedValueSet())); } set { nvs = value; } }
        private NamedValueSet nvs = null;

        public DeviceCapabilities DeviceCapabilities { get; set; }
        public PodSensorValues PodSensorValues { get; set; }
        public DecodedPodInfo DecodedPodInfo { get; set; }
        public PositionState PositionState { get; set; }

        public DisplayState DisplayStateSetpoint { get; set; }
        public DisplayState DisplayState { get; set; }
        public ButtonSet ButtonSet { get; set; }

        public PassiveToActivePinsState E84OutputSetpoint { get; set; }
        public PassiveToActivePinsState E84OutputReadback { get; set; }
        public ActiveToPassivePinsState E84Inputs { get; set; }
        public MapResults MapResults { get; set; }

        public IPortDisplayContextInfo PortDisplayContextInfo { get; set; }

        public bool IsEqualTo(ILPMState rhs)
        {
            return (rhs != null
                    && NVS.IsEqualTo(rhs.NVS)
                    && DeviceCapabilities.IsEqualTo(rhs.DeviceCapabilities)
                    && PodSensorValues.IsEqualTo(rhs.PodSensorValues)
                    && DecodedPodInfo.IsEqualTo(rhs.DecodedPodInfo)
                    && PositionState.IsEqualTo(rhs.PositionState)
                    && DisplayStateSetpoint.IsEqualTo(rhs.DisplayStateSetpoint)
                    && DisplayState.IsEqualTo(rhs.DisplayState)
                    && ButtonSet.IsEqualTo(rhs.ButtonSet)
                    && E84OutputSetpoint.IsEqualTo(rhs.E84OutputSetpoint)
                    && E84OutputReadback.IsEqualTo(rhs.E84OutputReadback)
                    && E84Inputs.IsEqualTo(rhs.E84Inputs)
                    && MapResults.IsEqualTo(rhs.MapResults)
                    && ((PortDisplayContextInfo == null && rhs.PortDisplayContextInfo == null) || (PortDisplayContextInfo != null && PortDisplayContextInfo.IsEqualTo(rhs.PortDisplayContextInfo)))
                    );
        }
    }

    #endregion

    #region IPortDisplayContextInfo

    /// <summary>
    /// This interface defines the set of property values that may generally be used to drive a load port's lights from, depending on configuration.
    /// </summary>
    public interface IPortDisplayContextInfo
    {
        bool Manual { get; }
        bool Auto { get; }
        bool Error { get; }
        bool Alarm { get; }
        bool Busy { get; }
        bool Loading { get; }
        bool Unloading { get; }
        bool LTSIsReadyToLoad { get; }
        bool LTSIsReadyToUnload { get; }
        DisplayItemState.OnOffFlashState Button1State { get; }      // usually only button or load button
        DisplayItemState.OnOffFlashState Button2State { get; }      // usually unload button if they are separate

        bool IsEqualTo(IPortDisplayContextInfo rhs);
    }

    public class PortDisplayContextInfo : IPortDisplayContextInfo
    {
        public PortDisplayContextInfo()
        { }

        public PortDisplayContextInfo(IPortDisplayContextInfo rhs)
        {
            SetFrom(rhs);
        }

        public PortDisplayContextInfo SetFrom(IPortDisplayContextInfo rhs)
        {
            Manual = rhs.Manual;
            Auto = rhs.Auto;
            Error = rhs.Error;
            Alarm = rhs.Alarm;
            Busy = rhs.Busy;
            Loading = rhs.Loading;
            Unloading = rhs.Unloading;
            LTSIsReadyToLoad = rhs.LTSIsReadyToLoad;
            LTSIsReadyToUnload = rhs.LTSIsReadyToUnload;
            Button1State = rhs.Button1State;
            Button2State = rhs.Button2State;

            return this;
        }

        public bool Manual { get; set; }
        public bool Auto { get; set; }
        public bool Error { get; set; }
        public bool Alarm { get; set; }
        public bool Busy { get; set; }
        public bool Loading { get; set; }
        public bool Unloading { get; set; }
        public bool LTSIsReadyToLoad { get; set; }
        public bool LTSIsReadyToUnload { get; set; }
        public DisplayItemState.OnOffFlashState Button1State { get; set; }
        public DisplayItemState.OnOffFlashState Button2State { get; set; }

        public bool IsEqualTo(IPortDisplayContextInfo rhs)
        {
            return (rhs != null
                    && Manual == rhs.Manual
                    && Auto == rhs.Auto
                    && Error == rhs.Error
                    && Alarm == rhs.Alarm
                    && Busy == rhs.Busy
                    && Loading == rhs.Loading
                    && Unloading == rhs.Unloading
                    && LTSIsReadyToLoad == rhs.LTSIsReadyToLoad
                    && LTSIsReadyToUnload == rhs.LTSIsReadyToUnload
                    && Button1State == rhs.Button1State
                    && Button2State == rhs.Button2State
                    );
        }
    }

    #endregion

    #region IDeviceCapabilites

    /// <summary>
    /// This interface provides the client with dynamically updated information about the capabilites of the device that the driver is currently connected to (or was last connected to).
    /// <para/>Please note that additional capabilites may be indicated by using the INamedValueSet related capabiliites at the ILPMState level.
    /// </summary>
    public interface IDeviceCapabilities
    {
        /// <summary>This value indicates what form of CarrierID/Tag Reader capabilities this device supports, if any.</summary>
        E099.TagReaderType TagReaderType { get; }

        /// <summary>This value indicates whether the device has any E84 hardware available</summary>
        bool HasE84 { get; }

        /// <summary>This value indicates what mapping capabilities this device offers.</summary>
        MapperCapabilities MapperCapabilities { get; }

        /// <summary>Returns true if this object has the same contents as the given rhs one.</summary>
        bool IsEqualTo(IDeviceCapabilities rhs);
    }

    /// <summary>
    /// This enumeration give information about the type of mapper support this device offers
    /// <para/>None, HasMapper, CanMapOnClose
    /// </summary>
    [Flags]
    public enum MapperCapabilities : int
    {
        /// <summary>This device does not support any type of mapping.</summary>
        None = 0,

        /// <summary>This device has a mapper that, at minimum, can be used to map while opening the Carrier, or after opening the Carrier.</summary>
        HasMapper = 1,

        /// <summary>This device is able to map while closing the Carrier (generally by reversing the motions used to map while opening the Carrier).</summary>
        CanMapOnClose = 2, 
    }

    public class DeviceCapabilities : IDeviceCapabilities
    {
        public DeviceCapabilities()
        { }

        public DeviceCapabilities(IDeviceCapabilities rhs)
        {
            SetFrom(rhs);
        }

        public DeviceCapabilities SetFrom(IDeviceCapabilities rhs)
        {
            TagReaderType = rhs.TagReaderType;
            HasE84 = rhs.HasE84;
            MapperCapabilities = rhs.MapperCapabilities;

            return this;
        }

        /// <summary>This value indicates what form of CarrierID/Tag Reader capabilities this device supports, if any.</summary>
        public E099.TagReaderType TagReaderType { get; set; }

        /// <summary>This value indicates whether the device has any E84 hardware available</summary>
        public bool HasE84 { get; set; }

        /// <summary>This value indicates what mapping capabilities this device offers.</summary>
        public MapperCapabilities MapperCapabilities { get; set; }

        /// <summary>Returns true if this object has the same contents as the given rhs one.</summary>
        public bool IsEqualTo(IDeviceCapabilities rhs)
        {
            return (rhs != null
                    && TagReaderType == rhs.TagReaderType
                    && HasE84 == rhs.HasE84
                    && MapperCapabilities == rhs.MapperCapabilities
                    );
        }
    }

    #endregion

    #region IMapResults and MapResults

    /// <summary>
    /// This interface defines the publisheable results from 
    /// </summary>
    public interface IMapResults
    {
        /// <summary>This is true whenever ResultCode has been set to null.  This is the state produced when a prior map result has been cleared by the start of some corresonding action</summary>
        bool IsEmpty { get; }
        /// <summary>This is true whenever the ResultCode is both non-null and non-empty</summary>
        bool Failed { get; }
        /// <summary>This is true when the ResultCode is the empty string.  This is the same as what is normally called Succeeded, except that it does not tell you anything about the contents of the SlotMap itself.</summary>
        bool IsValid { get; }

        SlotState[] SlotMap { get; }
        string ResultCode { get; }

        bool IsEqualTo(IMapResults rhs);
    }

    public class MapResults : IMapResults
    {
        public MapResults() { }

        public MapResults(IMapResults rhs)
        {
            SetFrom(rhs.ResultCode, rhs.SlotMap);
        }

        public MapResults SetFrom(IMapResults rhs)
        {
            return SetFrom(rhs.ResultCode, rhs.SlotMap);
        }

        public MapResults SetFrom(string resultCode, SlotState[] slotStateArray = null)
        {
            ResultCode = resultCode;
            SlotMap = (slotStateArray.IsNullOrEmpty() ? emptySlotMap : new List<SlotState>(slotStateArray).ToArray());

            return this;
        }

        public MapResults Clear()
        {
            return SetFrom(emptyMapResults);
        }

        private readonly static IMapResults emptyMapResults = new MapResults();

        /// <summary>This is true whenever ResultCode has been set to null.  This is the state produced when a prior map result has been cleared by the start of some corresonding action</summary>
        public bool IsEmpty { get { return ResultCode == null; } }
        /// <summary>This is true whenever the ResultCode is both non-null and non-empty</summary>
        public bool Failed { get { return !ResultCode.IsNullOrEmpty(); } }
        /// <summary>This is true when the ResultCode is the empty string.  This is the same as what is normally called Succeeded, except that it does not tell you anything about the contents of the SlotMap itself.</summary>
        public bool IsValid { get { return ResultCode == string.Empty; } }

        public SlotState[] SlotMap { get { return slotMap; } set { slotMap = value ?? emptySlotMap; } }
        private SlotState[] slotMap = emptySlotMap;
        private static readonly SlotState[] emptySlotMap = new SlotState[0];

        public string ResultCode { get; set; }

        public bool IsEqualTo(IMapResults rhs)
        {
            return (rhs != null
                    && SlotMap.IsEqualTo(rhs.SlotMap)
                    && ResultCode == rhs.ResultCode
                    );
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "Empty";
            else if (Failed)
                return "Failed: {0}".CheckedFormat(ResultCode);
            else
                return "'{0}'".CheckedFormat(SlotMap.ToString(SlotStateStringFormat.Graphics));
        }
    }

    #endregion

    #region IPodSensorValues, PodSensorValues, related enums

    public interface IPodSensorValues
    {
        PresentPlaced PresentPlaced { get; }
        bool PresenceSensor { get; }
        bool PlacementSensor { get; }
        bool IsProperlyPlaced { get; }
        bool IsPartiallyPlaced { get; }
        bool IsNeitherPresentNorPlaced { get; }
        bool DoesPlacedEqualPresent { get; }
        bool IsPlacedOrPresent { get; }

        InfoPads InfoPads { get; }
        bool InfoPad_A { get; }
        bool InfoPad_B { get; }
        bool InfoPad_C { get; }
        bool InfoPad_D { get; }

        bool IsEqualTo(IPodSensorValues rhs);
    }

    public class PodSensorValues : IPodSensorValues
    {
        public PodSensorValues() 
        { }

        public PodSensorValues(IPodSensorValues rhs)
        {
            SetFrom(rhs);
        }

        public PodSensorValues SetFrom(IPodSensorValues rhs)
        {
            PresentPlaced = rhs.PresentPlaced;
            InfoPads = rhs.InfoPads;
            return this;
        }

        public PresentPlaced PresentPlaced { get; set; }
        public bool PresenceSensor { get { return PresentPlaced.IsSet(PresentPlaced.Present); } }
        public bool PlacementSensor { get { return PresentPlaced.IsSet(PresentPlaced.Placed); } }
        public bool IsPartiallyPlaced { get { return PresentPlaced.IsSet(PresentPlaced.PartiallyPlaced); } }
        public bool IsProperlyPlaced { get { return PresentPlaced.IsProperlyPlaced(); } }
        public bool IsNeitherPresentNorPlaced { get { return PresentPlaced.IsNeitherPresentNorPlaced();} }
        public bool DoesPlacedEqualPresent { get { return PresentPlaced.DoesPlacedEqualPresent(); } }
        public bool IsPlacedOrPresent { get { return PresentPlaced.IsPlacedOrPresent(); } }

        public InfoPads InfoPads { get; set; }

        public bool InfoPad_A { get { return InfoPads.IsSet(InfoPads.A); } }
        public bool InfoPad_B { get { return InfoPads.IsSet(InfoPads.B); } }
        public bool InfoPad_C { get { return InfoPads.IsSet(InfoPads.C); } }
        public bool InfoPad_D { get { return InfoPads.IsSet(InfoPads.D); } }

        public bool IsEqualTo(IPodSensorValues rhs)
        {
            return (rhs != null
                    && PresentPlaced == rhs.PresentPlaced
                    && InfoPads == rhs.InfoPads
                    );
        }    
    }

    /// <summary>Present/Placed related flag enum</summary>
    [DataContract]
    [Flags]
    public enum PresentPlaced
    {
        /// <summary>Neither Presence nor Placement sensors are active</summary>
        [EnumMember]
        None = 0x00,
        /// <summary>Presence sensor is active</summary>
        [EnumMember]
        Present = 0x01,
        /// <summary>Placement sensor is active</summary>
        [EnumMember]
        Placed = 0x02,
        /// <summary>Placement sensor is partially active</summary>
        [EnumMember]
        PartiallyPlaced = 0x04,
    }

    /// <summary>InfoPad flag enum</summary>
    [DataContract]
    [Flags]
    public enum InfoPads
    {
        /// <summary>No InfoPad is currently active.  0x00</summary>
        [EnumMember]
        None = 0x00,
        /// <summary>InfoPad A is active.  0x01</summary>
        [EnumMember]
        A = 0x01,
        /// <summary>InfoPad B is active.  0x02</summary>
        [EnumMember]
        B = 0x02,
        /// <summary>InfoPad C is active.  0x04</summary>
        [EnumMember]
        C = 0x04,
        /// <summary>InfoPad D is active.  0x08</summary>
        [EnumMember]
        D = 0x08,
    }

    #endregion

    #region IDecodedPodInfo, DecodedPodInfo, related enums

    public interface IDecodedPodInfo
    {
        CarrierType CarrierType { get; }

        OCA OCA { get; }

        bool IsEqualTo(IDecodedPodInfo rhs);
    }

    public class DecodedPodInfo : IDecodedPodInfo
    {
        public DecodedPodInfo() 
        { }

        public DecodedPodInfo(IDecodedPodInfo rhs)
        {
            SetFrom(rhs);
        }

        public DecodedPodInfo SetFrom(IDecodedPodInfo rhs)
        {
            CarrierType = rhs.CarrierType;
            OCA = rhs.OCA;

            return this;
        }

        public CarrierType CarrierType { get; set; }

        public OCA OCA { get; set; }

        public bool IsEqualTo(IDecodedPodInfo rhs)
        {
            return (rhs != null
                    && CarrierType == rhs.CarrierType
                    && OCA == rhs.OCA
                    );
        }    
    }

    /// <summary>Flag enumeration that gives information about a Carrier including type (FOUP, FOSB, Cassette), Slots (25, 13, 26) and WaferSize (100, 125, 150, 200, 300, 450 mm)</summary>
    [DataContract]
    [Flags]
    public enum CarrierType
    {
        /// <summary>There is currently no Carrier detected of any type.  0x00</summary>
        [EnumMember]
        None = 0x00,
        /// <summary>A FOUP has been detected.  0x01</summary>
        [EnumMember]
        FOUP = 0x01,
        /// <summary>A FOSB has been detected.  0x02</summary>
        [EnumMember]
        FOSB = 0x02,
        /// <summary>A FOSB has been detected which has its door removed prior to placement.  0x04</summary>
        [EnumMember]
        FOSB_NoDoor = 0x04,
        /// <summary>A Cassette has been detected (typically placed in an OCA).  0x08</summary>
        [EnumMember]
        Cassette = 0x08,
        /// <summary>A generic Carrier has been detected (details must be determined through other means.  0x80</summary>
        [EnumMember]
        Other = 0x80,

        /// <summary>The Carrier has 25 slots.  0x100</summary>
        [EnumMember]
        Slots_25 = 0x100,
        /// <summary>The Carrier has 13 slots.  0x200</summary>
        [EnumMember]
        Slots_13 = 0x200,
        /// <summary>The Carrier has 26 slots.  0x400</summary>
        [EnumMember]
        Slots_26 = 0x400,

        /// <summary>The Carrier contains 100mm round substrates.  0x010000</summary>
        [EnumMember]
        Size_100mm = 0x010000,
        /// <summary>The Carrier contains 125mm round substrates.  0x020000</summary>
        [EnumMember]
        Size_125mm = 0x020000,
        /// <summary>The Carrier contains 150mm round substrates.  0x040000</summary>
        [EnumMember]
        Size_150mm = 0x040000,
        /// <summary>The Carrier contains 200mm round substrates.  0x080000</summary>
        [EnumMember]
        Size_200mm = 0x080000,
        /// <summary>The Carrier contains 300mm round substrates.  0x100000</summary>
        [EnumMember]
        Size_300mm = 0x100000,
        /// <summary>The Carrier contains 450mm round substrates.  0x200000</summary>
        [EnumMember]
        Size_400mm = 0x200000,
    }

    /// <summary>Flag enumeration that gives information about OCA (if any)</summary>
    [DataContract]
    [Flags]
    public enum OCA
    {
        /// <summary>No OCA is currently installed.  0x00</summary>
        [EnumMember]
        None = 0x00,

        /// <summary>An OCA is installed.  0x01</summary>
        [EnumMember]
        Installed = 0x01,

        /// <summary>The OCA is Open (for devices that can recognize this state).  0x02</summary>
        [EnumMember]
        Open = 0x02,

        /// <summary>The OCA is Locked (for devices that can recognize this state).  0x04</summary>
        [EnumMember]
        Locked = 0x04,
    }

    #endregion

    #region IPositionState, PositionState, IActuatorStates, ActuatorStates

    public interface IPositionState
    {
        /// <summary>Clamped == AtPos2, Unclamped == AtPos1</summary>
        ActuatorPosition ClampState { get; }
        /// <summary>Docked == AtPos2, Undocked == AtPos1</summary>
        ActuatorPosition DockState { get; }
        /// <summary>Vacuum turned on == AtPos2, Vacuum turned off == AtPos1</summary>
        ActuatorPosition VacState { get; }
        /// <summary>Door Keys Horizontal (door unlatched from carrier) == AtPos2, Door Keys Vertical (door latched in carrier) == AtPos1</summary>
        ActuatorPosition DoorKeysState { get; }
        /// <summary>Door Open (away from carrier) == AtPos2, Door Closed (pressed into carrier) == AtPos1</summary>
        ActuatorPosition DoorOpenState { get; }
        /// <summary>Door Down == AtPos2, Door Up == AtPos1</summary>
        ActuatorPosition DoorDownState { get; }

        /// <summary>This property returns true if the device has been initialized/homed/referenced since we last started communicating with it</summary>
        bool IsReferenced { get; }
        /// <summary>This property returns true if the driver believes that the device is activly controlling its position.</summary>
        bool IsServoOn { get; }

        bool DockMotionILockTripped { get; }
        bool ProtrusionSensorIsTripped { get; }

        string InMotionReason { get; }

        bool IsInMotion { get; }

        bool IsValid { get; }
        bool IsClamped { get; }
        bool IsUnclamped { get; }
        bool IsDocked { get; }
        bool IsUndocked { get; }
        /// <summary>True if vacuum is enabled to the suction cups</summary>
        bool IsVacEnabled { get; }
        /// <summary>True if vacuum is disabled from the suction cups</summary>
        bool IsVacDisabled { get; }
        bool IsVacSensed { get; }
        bool AreDoorKeysHorizontal { get; }
        bool AreDoorKeysVertical { get; }
        bool IsDoorOpen { get; }
        bool IsDoorClosed { get; }
        bool IsDoorDown { get; }
        bool IsDoorUp { get; }

        bool IsCarrierOpen { get; }
        bool IsCarrierClosed { get; }

        bool IsEqualTo(IPositionState rhs);
    }

    public class PositionState : IPositionState
    {
        public PositionState()
        { }

        public PositionState(IPositionState rhs)
        {
            SetFrom(rhs);
        }

        public PositionState SetFrom(IPositionState rhs)
        {
            ClampState = rhs.ClampState;
            DockState = rhs.DockState;
            VacState = rhs.VacState;
            DoorKeysState = rhs.DoorKeysState;
            DoorOpenState = rhs.DoorOpenState;
            DoorDownState = rhs.DoorDownState;
            IsReferenced = rhs.IsReferenced;
            IsServoOn = rhs.IsServoOn;
            DockMotionILockTripped = rhs.DockMotionILockTripped;
            ProtrusionSensorIsTripped = rhs.ProtrusionSensorIsTripped;
            InMotionReason = rhs.InMotionReason;
            IsVacSensed = rhs.IsVacSensed;
            
            return this;
        }

        public PositionState SetFrom(IActuatorStates actuatorStates)
        {
            ClampState = actuatorStates.ClampState.PosState;
            DockState = actuatorStates.DockState.PosState;
            VacState = actuatorStates.VacState.PosState;
            DoorKeysState = actuatorStates.DoorKeysState.PosState;
            DoorOpenState = actuatorStates.DoorOpenState.PosState;
            DoorDownState = actuatorStates.DoorDownState.PosState;

            bool statesValid = (actuatorStates.ClampState.IsValid && actuatorStates.DockState.IsValid && actuatorStates.VacState.IsValid && actuatorStates.DoorKeysState.IsValid && actuatorStates.DoorOpenState.IsValid && actuatorStates.DoorDownState.IsValid);

            IsReferenced = statesValid;
            IsServoOn |= statesValid;

            return this;
        }

        /// <summary>Clamped == AtPos2, Unclamped/Released == AtPos1</summary>
        public ActuatorPosition ClampState { get; set; }
        /// <summary>Docked == AtPos2, Undocked == AtPos1</summary>
        public ActuatorPosition DockState { get; set; }
        /// <summary>Vacuum turned on == AtPos2, Vacuum turned off == AtPos1</summary>
        public ActuatorPosition VacState { get; set; }
        /// <summary>Door Keys Horizontal (door unlatched from carrier) == AtPos2, Door Keys Vertical (door latched in carrier) == AtPos1</summary>
        public ActuatorPosition DoorKeysState { get; set; }
        /// <summary>Door Open (away from carrier) == AtPos2, Door Closed (pressed into carrier) == AtPos1</summary>
        public ActuatorPosition DoorOpenState { get; set; }
        /// <summary>Door Down == AtPos2, Door Up == AtPos1</summary>
        public ActuatorPosition DoorDownState { get; set; }

        /// <summary>This property returns true if the device has been initialized/homed/referenced since we last started communicating with it</summary>
        public bool IsReferenced { get; set;  }
        /// <summary>This property returns true if the driver believes that the device is activly controlling its position.</summary>
        public bool IsServoOn { get; set; }

        public bool DockMotionILockTripped { get; set; }
        public bool ProtrusionSensorIsTripped { get; set; }

        public string InMotionReason { get; set; }

        public bool IsVacSensed { get; set; }

        public bool IsEqualTo(IPositionState rhs)
        {
            return (rhs != null
                    && ClampState == rhs.ClampState
                    && DockState == rhs.DockState
                    && VacState == rhs.VacState
                    && DoorKeysState == rhs.DoorKeysState
                    && DoorOpenState == rhs.DoorOpenState
                    && DoorDownState == rhs.DoorDownState
                    && IsReferenced == rhs.IsReferenced
                    && IsServoOn == rhs.IsServoOn
                    && DockMotionILockTripped == rhs.DockMotionILockTripped
                    && ProtrusionSensorIsTripped == rhs.ProtrusionSensorIsTripped
                    && InMotionReason == rhs.InMotionReason
                    && IsVacSensed == rhs.IsVacSensed
                    );
        }

        public virtual bool IsInMotion
        {
            get
            {
                return (ClampState.IsInMotion() || DockState.IsInMotion() || VacState.IsInMotion()
                        || DoorKeysState.IsInMotion() || DoorOpenState.IsInMotion() || DoorDownState.IsInMotion()
                        || !InMotionReason.IsNullOrEmpty());
            }
        }

        public bool IsValid
        {
            get
            {
                return (ClampState.IsValid() && DockState.IsValid() && VacState.IsValid()
                        && DoorKeysState.IsValid() && DoorOpenState.IsValid() && DoorDownState.IsValid()
                        && IsReferenced && IsServoOn);
            }
        }

        public bool IsClamped { get { return ClampState.IsAtPos2(); } }
        public bool IsUnclamped { get { return ClampState.IsAtPos1(); } }
        public bool IsDocked { get { return DockState.IsAtPos2(); } }
        public bool IsUndocked { get { return DockState.IsAtPos1(); } }
        /// <summary>True if vacuum is enabled to the suction cups</summary>
        public bool IsVacEnabled { get { return VacState.IsAtPos2(); } }
        /// <summary>True if vacuum is disabled from the suction cups</summary>
        public bool IsVacDisabled { get { return VacState.IsAtPos1(); } }
        public bool AreDoorKeysHorizontal { get { return DoorKeysState.IsAtPos2(); } }
        public bool AreDoorKeysVertical { get { return DoorKeysState.IsAtPos1(); } }
        public bool IsDoorOpen { get { return DoorOpenState.IsAtPos2(); } }
        public bool IsDoorClosed { get { return DoorOpenState.IsAtPos1(); } }
        public bool IsDoorDown { get { return DoorDownState.IsAtPos2(); } }
        public bool IsDoorUp { get { return DoorDownState.IsAtPos1(); } }

        public bool IsCarrierOpen { get { return IsValid && IsDoorOpen && IsDoorDown; } }
        public bool IsCarrierClosed { get { return IsValid && IsDoorUp && IsDoorClosed && AreDoorKeysVertical && IsVacDisabled; } }
    }

    public interface IActuatorStates
    {
        IActuatorState ClampState { get; }
        IActuatorState DockState { get; }
        IActuatorState VacState { get; }
        IActuatorState DoorKeysState { get; }
        IActuatorState DoorOpenState { get; }
        IActuatorState DoorDownState { get; }

        bool IsEqualTo(IActuatorStates rhs);
    }

    public class ActuatorStates : IActuatorStates
    {
        public ActuatorStates()
        {
            ClampState = new ActuatorState();
            DockState = new ActuatorState();
            VacState = new ActuatorState();
            DoorKeysState = new ActuatorState();
            DoorOpenState = new ActuatorState();
            DoorDownState = new ActuatorState();
        }

        public ActuatorStates(IActuatorStates rhs)
        {
            SetFrom(rhs);
        }

        public ActuatorStates SetFrom(IActuatorStates rhs)
        {
            ClampState = new ActuatorState(rhs.ClampState);
            DockState = new ActuatorState(rhs.DockState);
            VacState = new ActuatorState(rhs.VacState);
            DoorKeysState = new ActuatorState(rhs.DoorKeysState);
            DoorOpenState = new ActuatorState(rhs.DoorOpenState);
            DoorDownState = new ActuatorState(rhs.DoorDownState);

            return this;
        }

        public ActuatorState ClampState { get; set; }
        public ActuatorState DockState { get; set; }
        public ActuatorState VacState { get; set; }
        public ActuatorState DoorKeysState { get; set; }
        public ActuatorState DoorOpenState { get; set; }
        public ActuatorState DoorDownState { get; set; }

        IActuatorState IActuatorStates.ClampState { get { return this.ClampState; } }
        IActuatorState IActuatorStates.DockState { get { return this.DockState; } }
        IActuatorState IActuatorStates.VacState { get { return this.VacState; } }
        IActuatorState IActuatorStates.DoorKeysState { get { return this.DoorKeysState; } }
        IActuatorState IActuatorStates.DoorOpenState { get { return this.DoorOpenState; } }
        IActuatorState IActuatorStates.DoorDownState { get { return this.DoorDownState; } }

        public bool IsEqualTo(IActuatorStates rhs)
        {
            return (rhs != null
                    && ClampState.IsEqualTo(rhs.ClampState)
                    && DockState.IsEqualTo(rhs.DockState)
                    && VacState.IsEqualTo(rhs.VacState)
                    && DoorKeysState.IsEqualTo(rhs.DoorKeysState)
                    && DoorOpenState.IsEqualTo(rhs.DoorOpenState)
                    && DoorDownState.IsEqualTo(rhs.DoorDownState)
                    );
        }
    }

    #endregion

    #region PositionsSummary

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

    #endregion

    #region DisplayState

    public interface IDisplayState
    {
        IDisplayItemState[] ButtonItemArray { get; }
        IDisplayItemState[] PanelItemArray { get; }
        IDisplayItemState[] AllItemArray { get; }
    }

    public class DisplayState : IDisplayState
    {
        public const int DefaultNumPanelItems = 8;
        public const int DefaultNumButtonItems = 2;
        public const int ButtonLampNumOffset = 8;

        public DisplayState()
            : this(DefaultNumPanelItems, DefaultNumButtonItems)
        { }

        public DisplayState(int numPanelItems, int numButtonItems)
        {
            PanelItemArray = Enumerable.Range(0, numPanelItems).Select(ledIdx => new DisplayItemState("LED{0}".CheckedFormat(ledIdx + 1)) { ItemIdx = ledIdx, IsButton = false }).ToArray();
            ButtonItemArray = Enumerable.Range(0, numButtonItems).Select(buttonIdx => new DisplayItemState("Button{0}".CheckedFormat(buttonIdx + 1), "LightBlue") { ItemIdx = buttonIdx, IsButton = true }).ToArray();
            AllItemArray = PanelItemArray.Concat(ButtonItemArray).ToArray();
        }

        public DisplayState(IDisplayState rhs)
        {
            SetFrom(rhs);
        }

        public DisplayState SetFrom(IDisplayState rhs)
        {
            PanelItemArray = rhs.PanelItemArray.Select(item => new DisplayItemState(item)).ToArray();
            ButtonItemArray = rhs.ButtonItemArray.Select(item => new DisplayItemState(item)).ToArray();
            AllItemArray = PanelItemArray.Concat(ButtonItemArray).ToArray();
            return this;
        }

        public DisplayItemState[] ButtonItemArray { get; private set; }
        public DisplayItemState[] PanelItemArray { get; private set; }
        public DisplayItemState[] AllItemArray { get; private set; }

        IDisplayItemState[] IDisplayState.ButtonItemArray { get { return this.ButtonItemArray; } }
        IDisplayItemState[] IDisplayState.PanelItemArray { get { return this.PanelItemArray; } }
        IDisplayItemState[] IDisplayState.AllItemArray { get { return this.AllItemArray; } }

        public bool IsEqualTo(IDisplayState rhs)
        {
            return (rhs != null
                    && AllItemArray.IsEqualTo(rhs.AllItemArray)
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

    public interface IButtonSet
    {
        bool Button1 { get; }
        bool Button2 { get; }

        int Button1ChangeCount { get; }
        int Button2ChangeCount { get; }

        bool IsEqualTo(IButtonSet rhs);
    }

    public class ButtonSet : IButtonSet
    {
        public ButtonSet() { }

        public ButtonSet(IButtonSet rhs)
        {
            SetFrom(rhs);
        }

        public ButtonSet SetFrom(IButtonSet rhs)
        {
            Button1 = rhs.Button1;
            Button2 = rhs.Button2;

            Button1ChangeCount = rhs.Button1ChangeCount;
            Button2ChangeCount = rhs.Button2ChangeCount;

            return this;
        }

        public bool Button1 { get; set; }
        public bool Button2 { get; set; }

        public int Button1ChangeCount { get; set; }
        public int Button2ChangeCount { get; set; }

        public void ButtonChanged(int buttonNum, bool value)
        {
            switch (buttonNum)
            {
                case 1: Button1ChangeCount++; Button1 = value; break;
                case 2: Button2ChangeCount++; Button2 = value; break;
                default: break;
            }
        }

        public bool IsEqualTo(IButtonSet rhs)
        {
            return (rhs != null 
                    && Button1 == rhs.Button1 && Button2 == rhs.Button2
                    && Button1ChangeCount == rhs.Button1ChangeCount && Button2ChangeCount == rhs.Button2ChangeCount);
        }

        public override string ToString()
        {
            string[] buttonsDown = { (Button1 ? "1" : string.Empty), (Button2 ? "2" : string.Empty) };

            return "{0} {1} {2}".CheckedFormat(string.Join(" ", buttonsDown.Where(s => !s.IsNullOrEmpty()).ToArray()).MapNullOrEmptyTo("None"), Button1ChangeCount, Button2ChangeCount);
        }
    }

    public interface IDisplayItemState
    {
        bool IsButton { get; }
        int ItemIdx { get; }
        int LampNum { get; }
        string Text { get; }
        string BorderColor { get; }
        string OffBackgroundColor { get; }
        string OnBackgroundColor { get; }
        DisplayItemState.OnOffFlashState State { get; }
        bool IsInternal { get; }
        DisplayItemState.OnOffFlashState LastLampCmdState { get; }
        bool FlashStateIsOn { get; }
        string CurrentBackgroundColor { get; }
    }

    public class DisplayItemState : IDisplayItemState
    {
        /// <summary>Represents the different states that an individual LPM physical annunciator (LED) can be set to</summary>
        public enum OnOffFlashState
        {
            /// <summary>LED is in inactive state.  0</summary>
            Off = 0,
            /// <summary>LED is in active state.  1</summary>
            On = 1,
            /// <summary>LED is in cycling between and passive and active state.  2</summary>
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

        public DisplayItemState(IDisplayItemState rhs)
        {
            SetFrom(rhs);
        }

        public DisplayItemState SetFrom(IDisplayItemState rhs)
        {
            IsButton = rhs.IsButton;
            ItemIdx = rhs.ItemIdx;
            Text = rhs.Text;
            BorderColor = rhs.BorderColor;
            OffBackgroundColor = rhs.OffBackgroundColor;
            OnBackgroundColor = rhs.OnBackgroundColor;
            State = rhs.State;
            LastLampCmdState = rhs.LastLampCmdState;
            IsInternal = rhs.IsInternal;
            FlashStateIsOn = rhs.FlashStateIsOn;

            return this;
        }

        public bool IsButton { get; set; }
        public int ItemIdx { get; set; }
        public int LampNum { get { return (ItemIdx + 1 + (IsButton ? DisplayState.ButtonLampNumOffset : 0)); } }

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
            return IsEqualTo(obj as IDisplayItemState);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return "{0} {1} {2}".CheckedFormat(Text, State, CurrentBackgroundColor);
        }

        public bool IsEqualTo(IDisplayItemState rhs)
        {
            return (rhs != null
                    && IsButton == rhs.IsButton
                    && ItemIdx == rhs.ItemIdx
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

    #region Common extension methods

    public static partial class ExtensionMethods
    {
        public static bool IsNeitherPresentNorPlaced(this PresentPlaced value) { return (value == PresentPlaced.None); }
        public static bool IsPresent(this PresentPlaced value) { return value.IsSet(PresentPlaced.Present); }
        public static bool IsPlaced(this PresentPlaced value) { return value.IsSet(PresentPlaced.Placed); }
        public static bool IsProperlyPlaced(this PresentPlaced value) { return value.Matches(PresentPlaced.Present | PresentPlaced.Placed, PresentPlaced.Present | PresentPlaced.Placed); }
        public static bool DoesPlacedEqualPresent(this PresentPlaced value) { return value.IsSet(PresentPlaced.Present) == value.IsSet(PresentPlaced.Placed); }
        public static bool IsPlacedOrPresent(this PresentPlaced value) { return value.IsSet(PresentPlaced.Present) || value.IsSet(PresentPlaced.Placed); }

        public static bool IsSet(this PresentPlaced value, PresentPlaced test) { return value.Matches(test, test); }
        public static bool IsSet(this InfoPads value, InfoPads test) { return value.Matches(test, test); }
        public static bool IsSet(this CarrierType value, CarrierType test) { return value.Matches(test, test); }
        public static bool IsSet(this OCA value, OCA test) { return value.Matches(test, test); }

        public static bool Matches(this PresentPlaced testValue, PresentPlaced mask, PresentPlaced expectedValue) { return ((testValue & mask) == expectedValue); }
        public static bool Matches(this InfoPads testValue, InfoPads mask, InfoPads expectedValue) { return ((testValue & mask) == expectedValue); }
        public static bool Matches(this CarrierType testValue, CarrierType mask, CarrierType expectedValue) { return ((testValue & mask) == expectedValue); }
        public static bool Matches(this OCA testValue, OCA mask, OCA expectedValue) { return ((testValue & mask) == expectedValue); }
    }

    #endregion
}
