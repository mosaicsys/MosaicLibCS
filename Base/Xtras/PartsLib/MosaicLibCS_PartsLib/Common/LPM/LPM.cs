//-------------------------------------------------------------------
/*! @file LPM.cs
 *  @brief This file contains common interfaces, class and struct definitions that are used in implementing, using, and displaying LPM Parts and their state.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2016 Mosaic Systems Inc.
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
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
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
        /// if requestMapIfSupported is true and the device supports mapping for the current carrier type, then the carrier will be mapped as the door is being opened.
        /// if requestMapIfSupported is true and the device does not support mapping for the current carrier type, then the carrier will be opened and the map results will be set to empty with a non-empty map result code.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// <para/>if requestMapIfSupported is true then this action will set the MapResults NamedValue in ActionState.NamedValues to last published map result produced by this action.
        /// </summary>
        IBasicAction Open(bool requestMapIfSupported);

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to close the carrier that has been placed.
        /// if requestMapIfSupported is true and the device supports mapping for the current carrier type, then the carrier will be mapped as the door is being closed.
        /// if requestMapIfSupported is true and the device does not support mapping for the current carrier type, then the carrier will be closed and the map results will be set to empty with a non-empty map result code.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// <para/>if requestMapIfSupported is true then this action will set the MapResults NamedValue in ActionState.NamedValues to last published map result produced by this action.
        /// </summary>
        IBasicAction Close(bool requestMapIfSupported);

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to attempt to map the carrier that has been placed.
        /// If the device does not support mapping then the action will fail.
        /// If the device does not support mapping with the current carrier type then the action will succeeded and the map results will be set to empty with a non=empty map result code.
        /// <para/>Supported NamedParam values: Retry=true: indicates that the action is being used as part of an recovery action and should take additional steps to attempt to get the motion to succeed
        /// <para/>This action will set the MapResults NamedValue in ActionState.NamedValues to last published map result produced by this action.
        /// </summary>
        IBasicAction Map();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to clear any previously produced map result.
        /// </summary>
        IBasicAction ClearMap();

        /// <summary>
        /// Action factory method.  When run, this action will cause this part to explicitly run all normally used status commands, and then update and publish all related state information (if needed).
        /// </summary>
        IBasicAction Sync(SyncFlags syncFlags = SyncFlags.Inputs | SyncFlags.Outputs | SyncFlags.E84);

        /// <summary>
        /// Action factory method.  When run, if the given decodedPodInfo is non-null, this action will cause this part replace its internally determined decodedPodInfo with the copy of the given value, publish the change, and apply the given value to configure how future motion actions are performed.
        /// If the given decodedPodInfo is null then this action will cause this part to return to using its normal internal logic for decoding, publishing and using the decoded pod into.
        /// </summary>
        IBasicAction SetDecodedPodInfoOverride(IDecodedPodInfo decodedPodInfo, bool andInitialize);

        /// <summary>
        /// Action factory method.  When run, this action will replace any previously provided IPortDisplayContextInfo publisher with the given value.
        /// </summary>
        IBasicAction SetPortUseContextInfoPublisher(INotificationObject<IPortUsageContextInfo> publisher);

        /// <summary>ILPMState state publisher.  This is also a notification object</summary>
        INotificationObject<ILPMState> StatePublisher { get; }
    }

    /// <summary>
    /// SyncFlags are used to inform the Sync action of any specific features that the client would like synced.  These may be combined bitwise
    /// <para/>None(0x00), Quick (0x01), Inputs (0x02), Outputs (0x04), E84 (0x08)
    /// </summary>
    [Flags]
    public enum SyncFlags : int
    {
        /// <summary>Placeholder [0x00].</summary>
        None = 0x00,
        /// <summary>Normal Sync simply verifies that the driver has been able to publish whatever the last state it may have observed was.  This type of sync does not necessarily run any commands to the target device. [0x01]</summary>
        Quick = 0x01,
        /// <summary>Input Sync makes certain to refresh and publish all scanned inputs before continuing [0x02]</summary>
        Inputs = 0x02,
        /// <summary>Output Sync updates all outputs (e84, lamp and/or buttons as needed). [0x04]</summary>
        Outputs = 0x04,
        /// <summary>E84 Sync re-reads the E84 inputs, iterates on the E84 state machine once and updates the E84 outputs (if needed). [0x08]</summary>
        E84 = 0x08,
    }

    #endregion

    #region LPMState

    public interface ILPMState : IEquatable<ILPMState>
    {
        INamedValueSet NVS { get; }

        IDeviceCapabilities DeviceCapabilities { get; }
        IPodSensorValues PodSensorValues { get; }
        IDecodedPodInfo DecodedPodInfo { get; }
        IPositionState PositionState { get; }

        IDisplayState DisplayStateSetpoint { get; }
        IDisplayState DisplayState { get; }
        IButtonSet ButtonSet { get; }

        IE84State E84State { get; }

        IMapResults MapResults { get; }

        IPortUsageContextInfo PortUsageContextInfo { get; }

        IBaseState PartBaseState { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(ILPMState rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
    public class LPMState : ILPMState
    {
        /// <summary>
        /// Returns an empty LPMState instance.
        /// </summary>
        public static ILPMState Empty { get { return _empty; } }
        private static readonly ILPMState _empty = new LPMState();

        public LPMState()
        {
            DeviceCapabilities = new DeviceCapabilities();
            PodSensorValues = new PodSensorValues();
            DecodedPodInfo = new DecodedPodInfo();
            PositionState = new PositionState();

            DisplayStateSetpoint = new DisplayState();
            DisplayState = new DisplayState();
            ButtonSet = new ButtonSet();

            E84State = new E84State();

            MapResults = new MapResults();

            PartBaseState = new BaseState();

            PortUsageContextInfo = new PortUsageContextInfo();
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

            E84State = new E84State(rhs.E84State);

            MapResults = new MapResults(rhs.MapResults);

            PartBaseState = new BaseState(rhs.PartBaseState);

            PortUsageContextInfo = new PortUsageContextInfo(rhs.PortUsageContextInfo);
            
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
        IE84State ILPMState.E84State { get { return this.E84State; } }
        IMapResults ILPMState.MapResults { get { return this.MapResults; } }
        IPortUsageContextInfo ILPMState.PortUsageContextInfo { get { return this.PortUsageContextInfo; } }
        IBaseState ILPMState.PartBaseState { get { return this.PartBaseState; } }

        public NamedValueSet NVS { get { return (nvs ?? (nvs = new NamedValueSet())); } set { nvs = value.MapEmptyToNull(); } }

        [DataMember(Order = 100, Name = "NVS", IsRequired = false, EmitDefaultValue = false)]
        private NamedValueSet nvs = null;

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public DeviceCapabilities DeviceCapabilities { get; set; }

        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public PodSensorValues PodSensorValues { get; set; }

        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
        public DecodedPodInfo DecodedPodInfo { get; set; }

        [DataMember(Order = 500, IsRequired = false, EmitDefaultValue = false)]
        public PositionState PositionState { get; set; }

        [DataMember(Order = 600, IsRequired = false, EmitDefaultValue = false)]
        public DisplayState DisplayStateSetpoint { get; set; }

        [DataMember(Order = 700, IsRequired = false, EmitDefaultValue = false)]
        public DisplayState DisplayState { get; set; }

        [DataMember(Order = 800, IsRequired = false, EmitDefaultValue = false)]
        public ButtonSet ButtonSet { get; set; }

        [DataMember(Order = 900, IsRequired = false, EmitDefaultValue = false)]
        public E84State E84State { get; set; }

        [DataMember(Order = 1000, IsRequired = false, EmitDefaultValue = false)]
        public MapResults MapResults { get; set; }

        [DataMember(Order = 1100, IsRequired = false, EmitDefaultValue = false)]
        public PortUsageContextInfo PortUsageContextInfo { get; set; }

        [DataMember(Order = 1200, IsRequired = false, EmitDefaultValue = false)]
        public BaseState PartBaseState { get; set; }

        public bool Equals(ILPMState other)
        {
            return (other != null
                && ((NVS.IsNullOrEmpty() && other.NVS.IsNullOrEmpty()) || NVS.IsEqualTo(other.NVS, compareReadOnly: false))
                    && DeviceCapabilities.Equals(other.DeviceCapabilities)
                    && PodSensorValues.Equals(other.PodSensorValues)
                    && DecodedPodInfo.Equals(other.DecodedPodInfo)
                    && PositionState.Equals(other.PositionState)
                    && DisplayStateSetpoint.Equals(other.DisplayStateSetpoint)
                    && DisplayState.Equals(other.DisplayState)
                    && ButtonSet.Equals(other.ButtonSet)
                    && E84State.Equals(other.E84State)
                    && MapResults.Equals(other.MapResults)
                    && ((PortUsageContextInfo == null && other.PortUsageContextInfo == null) || (PortUsageContextInfo != null && PortUsageContextInfo.Equals(other.PortUsageContextInfo)))
                    && PartBaseState.Equals(other.PartBaseState, compareTimestamps: false)
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(ILPMState rhs)
        {
            return Equals(rhs);
        }

        /// <summary>Supports debugging and logging.</summary>
        public override string ToString()
        {
            return "Partial State: pod:{0} pos:{1} e84:{2} map:{3}".CheckedFormat(PodSensorValues, PositionState, E84State, MapResults);
        }
    }

    #endregion

    #region MotionAction enum

    /// <summary>
    /// This enumeration gives a quick summary list of the individual actions that a LPM can be asked to perform.
    /// It is being defined as a flag enum so that it can also be used as an interlock action enable mask.
    /// </summary>
    [Flags]
    public enum MotionAction : int
    {
        /// <summary>default/placeholder value: 0x0000</summary>
        None = 0x0000,

        /// <summary>Initialize: 0x0001</summary>
        Initialize = 0x0001,

        /// <summary>Clamp: 0x0002</summary>
        Clamp = 0x0002,

        /// <summary>Unclamp: 0x0004</summary>
        Unclamp = 0x0004,

        /// <summary>Dock: 0x0008</summary>
        Dock = 0x0008,

        /// <summary>Undock: 0x0010</summary>
        Undock = 0x0010,

        /// <summary>Open: 0x0020</summary>
        Open = 0x0020,

        /// <summary>Close: 0x0040</summary>
        Close = 0x0040,

        /// <summary>Map: 0x0080</summary>
        Map = 0x0080,
    }

    #endregion

    #region IPortDisplayContextInfo

    /// <summary>
    /// This interface defines the set of property values that may generally be used to drive a load port's lights from, depending on configuration.
    /// </summary>
    public interface IPortUsageContextInfo : IEquatable<IPortUsageContextInfo>
    {
        Semi.E087.AMS AMS { get; }
        Semi.E087.LTS LTS { get; }
        Semi.E087.LRS LRS { get; }

        bool Initializing { get; }
        bool Error { get; }
        bool Alarm { get; }
        bool Busy { get; }
        bool Loading { get; }
        bool Unloading { get; }
        bool E84LoadInProgress { get; }
        bool E84UnloadInProgress { get; }

        bool APresentOrPlacementAlarmIsActive { get; }

        DisplayItemState.OnOffFlashState Button1State { get; }      // usually only button or load button
        DisplayItemState.OnOffFlashState Button2State { get; }      // usually unload button if they are separate

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IPortUsageContextInfo rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
    public class PortUsageContextInfo : IPortUsageContextInfo
    {
        public PortUsageContextInfo()
        { }

        public PortUsageContextInfo(IPortUsageContextInfo rhs)
        {
            SetFrom(rhs);
        }

        public PortUsageContextInfo SetFrom(IPortUsageContextInfo rhs)
        {
            AMS = rhs.AMS;
            LTS = rhs.LTS;
            LRS = rhs.LRS;

            Initializing = rhs.Initializing;
            Error = rhs.Error;
            Alarm = rhs.Alarm;
            Busy = rhs.Busy;
            Loading = rhs.Loading;
            Unloading = rhs.Unloading;
            E84LoadInProgress = rhs.E84LoadInProgress;
            E84UnloadInProgress = rhs.E84UnloadInProgress;

            APresentOrPlacementAlarmIsActive = rhs.APresentOrPlacementAlarmIsActive;

            Button1State = rhs.Button1State;
            Button2State = rhs.Button2State;

            return this;
        }

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public Semi.E087.AMS AMS { get; set; }
 
        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public Semi.E087.LTS LTS { get; set; }

        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public Semi.E087.LRS LRS { get; set; }

        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
        public bool Initializing { get; set; }

        [DataMember(Order = 500, IsRequired = false, EmitDefaultValue = false)]
        public bool Error { get; set; }

        [DataMember(Order = 600, IsRequired = false, EmitDefaultValue = false)]
        public bool Alarm { get; set; }
        
        [DataMember(Order = 700, IsRequired = false, EmitDefaultValue = false)]
        public bool Busy { get; set; }
        
        [DataMember(Order = 800, IsRequired = false, EmitDefaultValue = false)]
        public bool Loading { get; set; }
        
        [DataMember(Order = 900, IsRequired = false, EmitDefaultValue = false)]
        public bool Unloading { get; set; }
        
        [DataMember(Order = 1000, IsRequired = false, EmitDefaultValue = false)]
        public bool E84LoadInProgress { get; set; }
        
        [DataMember(Order = 1100, IsRequired = false, EmitDefaultValue = false)]
        public bool E84UnloadInProgress { get; set; }

        [DataMember(Order = 1200, IsRequired = false, EmitDefaultValue = false)]
        public bool APresentOrPlacementAlarmIsActive { get; set; }

        [DataMember(Order = 1300, IsRequired = false, EmitDefaultValue = false)]
        public DisplayItemState.OnOffFlashState Button1State { get; set; }

        [DataMember(Order = 1400, IsRequired = false, EmitDefaultValue = false)]
        public DisplayItemState.OnOffFlashState Button2State { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.CheckedAppendFormat("AMS:{0} LTS:{1} LRS:{2}", AMS, LTS, LRS);

            if (Initializing)
                sb.Append(" Initing");
            if (Error)
                sb.Append(" Err");
            if (Alarm)
                sb.Append(" Alarm");
            if (Busy)
                sb.Append(" Busy");
            if (Loading)
                sb.Append(" Loading");
            if (Unloading)
                sb.Append(" Unloading");
            if (E84LoadInProgress)
                sb.Append(" E84-Loading");
            if (E84UnloadInProgress)
                sb.Append(" E84-Unloading");

            if (APresentOrPlacementAlarmIsActive)
                sb.Append(" PPAlarm");

            if (Button1State != DisplayItemState.OnOffFlashState.Off)
                sb.CheckedAppendFormat(" Btn1:{0}", Button1State);

            if (Button2State != DisplayItemState.OnOffFlashState.Off)
                sb.CheckedAppendFormat(" Btn2:{0}", Button2State);

            return sb.ToString();
        }

        public bool Equals(IPortUsageContextInfo other)
        {
            return (other != null
                    && AMS == other.AMS
                    && LTS == other.LTS
                    && LRS == other.LRS
                    && Initializing == other.Initializing
                    && Error == other.Error
                    && Alarm == other.Alarm
                    && Busy == other.Busy
                    && Loading == other.Loading
                    && Unloading == other.Unloading
                    && E84LoadInProgress == other.E84LoadInProgress
                    && E84UnloadInProgress == other.E84UnloadInProgress
                    && APresentOrPlacementAlarmIsActive == other.APresentOrPlacementAlarmIsActive
                    && Button1State == other.Button1State
                    && Button2State == other.Button2State
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IPortUsageContextInfo rhs)
        {
            return Equals(rhs);
        }
    }

    #endregion

    #region IDeviceCapabilites

    /// <summary>
    /// This interface provides the client with dynamically updated information about the capabilites of the device that the driver is currently connected to (or was last connected to).
    /// <para/>Please note that additional capabilites may be indicated by using the INamedValueSet related capabiliites at the ILPMState level.
    /// </summary>
    public interface IDeviceCapabilities : IEquatable<IDeviceCapabilities>
    {
        /// <summary>This value indicates what form of CarrierID/Tag Reader capabilities this device supports, if any.</summary>
        E099.TagReaderType TagReaderType { get; }

        /// <summary>This value indicates whether the device has any E84 hardware available</summary>
        bool HasE84 { get; }

        /// <summary>This value indicates what mapping capabilities this device offers.</summary>
        MapperCapabilities MapperCapabilities { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IDeviceCapabilities rhs);
    }

    /// <summary>
    /// This enumeration give information about the type of mapper support this device offers
    /// <para/>None (0x00), CanMapOnOpen (0x01), CanMapAfterOpen (0x02), CanMapOnClose (0x04)
    /// </summary>
    [Flags]
    public enum MapperCapabilities : int
    {
        /// <summary>This device does not support any type of mapping. [0x00]</summary>
        None = 0x00,

        /// <summary>This device has a mapper that can be used to map while opening the Carrier. [0x01]</summary>
        CanMapOnOpen = 0x01,

        /// <summary>This device has a mapper that can be used to map after opening the Carrier (generally by raising the door and mapping while lowering the door). [0x02]</summary>
        CanMapAfterOpen = 0x02,

        /// <summary>This device has a mapper that can be used to map while closing the Carrier (generally by reversing the motions used to map while opening the Carrier). [0x04]</summary>
        CanMapOnClose = 0x04,
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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
        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public E099.TagReaderType TagReaderType { get; set; }

        /// <summary>This value indicates whether the device has any E84 hardware available</summary>
        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public bool HasE84 { get; set; }

        /// <summary>This value indicates what mapping capabilities this device offers.</summary>
        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public MapperCapabilities MapperCapabilities { get; set; }

        /// <summary>Returns true if this object has the same contents as the given rhs one.</summary>
        public bool Equals(IDeviceCapabilities other)
        {
            return (other != null
                    && TagReaderType == other.TagReaderType
                    && HasE84 == other.HasE84
                    && MapperCapabilities == other.MapperCapabilities
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IDeviceCapabilities rhs)
        {
            return Equals(rhs);
        }

        /// <summary>Supports debugging and logging.</summary>
        public override string ToString()
        {
            return "DeviceCapabilites IntegratedTagRdr:{0} HasE84:{1} Mapping:{2}".CheckedFormat(TagReaderType, (HasE84 ? "Yes": "No"),  MapperCapabilities);
        }
    }

    #endregion

    #region IMapResults and MapResults

    /// <summary>
    /// This interface defines the publisheable results from 
    /// </summary>
    public interface IMapResults : IEquatable<IMapResults>
    {
        /// <summary>This is true whenever ResultCode has been set to null.  This is the state produced when a prior map result has been cleared by the start of some corresonding action</summary>
        bool IsEmpty { get; }
        /// <summary>This is true whenever the ResultCode is both non-null and non-empty</summary>
        bool Failed { get; }
        /// <summary>This is true when the ResultCode is the empty string.  This is the same as what is normally called Succeeded, except that it does not tell you anything about the contents of the SlotMap itself.</summary>
        bool IsValid { get; }

        SlotState[] SlotMap { get; }
        string ResultCode { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IMapResults rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public SlotState[] SlotMap { get { return slotMap; } set { slotMap = value ?? emptySlotMap; } }
        private SlotState[] slotMap = emptySlotMap;
        private static readonly SlotState[] emptySlotMap = EmptyArrayFactory<SlotState>.Instance;

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public string ResultCode { get; set; }

        public bool Equals(IMapResults other)
        {
            return (other != null
                    && SlotMap.IsEqualTo(other.SlotMap)
                    && ResultCode == other.ResultCode
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IMapResults rhs)
        {
            return Equals(rhs);
        }

        /// <summary>Supports debugging and logging.</summary>
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

    public interface IPodSensorValues : IEquatable<IPodSensorValues>
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

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IPodSensorValues rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public PresentPlaced PresentPlaced { get; set; }

        public bool PresenceSensor { get { return PresentPlaced.IsSet(PresentPlaced.Present); } }
        public bool PlacementSensor { get { return PresentPlaced.IsSet(PresentPlaced.Placed); } }
        public bool IsPartiallyPlaced { get { return PresentPlaced.IsSet(PresentPlaced.PartiallyPlaced); } }
        public bool IsProperlyPlaced { get { return PresentPlaced.IsProperlyPlaced(); } }
        public bool IsNeitherPresentNorPlaced { get { return PresentPlaced.IsNeitherPresentNorPlaced();} }
        public bool DoesPlacedEqualPresent { get { return PresentPlaced.DoesPlacedEqualPresent(); } }
        public bool IsPlacedOrPresent { get { return PresentPlaced.IsPlacedOrPresent(); } }

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public InfoPads InfoPads { get; set; }

        public bool InfoPad_A { get { return InfoPads.IsSet(InfoPads.A); } }
        public bool InfoPad_B { get { return InfoPads.IsSet(InfoPads.B); } }
        public bool InfoPad_C { get { return InfoPads.IsSet(InfoPads.C); } }
        public bool InfoPad_D { get { return InfoPads.IsSet(InfoPads.D); } }

        public bool Equals(IPodSensorValues other)
        {
            return (other != null
                    && PresentPlaced == other.PresentPlaced
                    && InfoPads == other.InfoPads
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IPodSensorValues rhs)
        {
            return Equals(rhs);
        }

        /// <summary>Supports debugging and logging.</summary>
        public override string ToString()
        {
            return "Sensors:{0} InfoPads:{1}".CheckedFormat(PresentPlaced, InfoPads);
        }
    }

    /// <summary>
    /// Present/Placed related flag enum
    /// <para/>None (0x00), Present (0x01), Placed (0x02), PartiallyPlaced (0x04)
    /// </summary>
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

    /// <summary>
    /// InfoPad flag enum
    /// <para/>None (0x00), A (0x01), B (0x02), C (0x04), D (0x08)
    /// </summary>
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

    public interface IDecodedPodInfo : IEquatable<IDecodedPodInfo>
    {
        CarrierType CarrierType { get; }

        OCA OCA { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IDecodedPodInfo rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public CarrierType CarrierType { get; set; }

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public OCA OCA { get; set; }

        public bool Equals(IDecodedPodInfo other)
        {
            return (other != null
                    && CarrierType == other.CarrierType
                    && OCA == other.OCA
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IDecodedPodInfo rhs)
        {
            return Equals(rhs);
        }

        /// <summary>Supports debugging and logging.</summary>
        public override string ToString()
        {
            return "CarrierType:{0} OCA:{1}".CheckedFormat(CarrierType, OCA);
        }
    }

    /// <summary>
    /// Flag enumeration that gives information about a Carrier including type (FOUP, FOSB, Cassette), Slots (25, 13, 26) and WaferSize (100, 125, 150, 200, 300, 450 mm)
    /// <para/>None (0x00), FOUP (0x01), FOSB (0x02), FOSB_NoDoor (0x04), Cassette (0x08), Other (0x80), Slot_25 (0x100), Slot_13 (0x200), Slot_26 (0x400),
    /// Size_100mm (0x10000), Size_125mm (0x20000), Size_150mm (0x40000), Size_200mm (0x80000), Size_300mm (0x100000), Size_450mm (0x200000)
    /// </summary>
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

    /// <summary>
    /// Flag enumeration that gives information about OCA (if any)
    /// <para/>None (0x00), Installed (0x01), Open (0s02), Locked (0x04)
    /// </summary>
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

    public interface IPositionState : IEquatable<IPositionState>
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

        /// <summary>True if any aspect of the device's motion is blocked by a interlock input or internal interlock sensor, such as a anti-pinch sensor</summary>
        bool MotionILockSensorIsTripped { get; }
        /// <summary>True if the device offers a wafer protrusion sensor and it is currently tripped (beam broken).  This sensor may be activiated at any time, such as when a robot arm gets or puts material to a carrier slot.</summary>
        bool ProtrusionSensorIsTripped { get; }

        /// <summary>Carrier Door is detected (either by vacuum grip or by other means)</summary>
        bool IsCarrierDoorDetected { get; }

        /// <summary>
        /// Carrier is Open (detailed meaning is device specific).  
        /// Generally this means that the carrier is present, the position is valid, docked, and the door is open and down.
        /// </summary>
        bool IsCarrierOpen { get; }
        /// <summary>
        /// Carrier is Closed (detailed meaning is device specific).  
        /// Generally this means that the carrier is present, the position is valid, the door is up, closed, latched and vacuum is released/disabled.
        /// </summary>
        bool IsCarrierClosed { get; }

        /// <summary>
        /// Indicates that this object's contents are valid  (detailed meaning is device specific).
        /// Generally this means that the ActuatorPosition values are all known, that the device indicates that the position is referenced and that the servo(s) are on.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// LPM position is safe to access (detailed meaning is device specific).  
        /// Generally this means that the carrier is present, the position is valid, docked, and the door is open and down.
        /// </summary>
        bool IsSafeToAccess { get; }

        /// <summary>
        /// LPM position is safe for the Carrier to be manually placed or removed.  
        /// Generally this means that the position is valid, the door is up and closed, and the PDO is undocked and unclamped.
        /// </summary>
        bool IsSafeForManualCarrierHandoff { get; }

        /// <summary>Indicates the reason that the IsInMotion is true</summary>
        string InMotionReason { get; }

        /// <summary>Part of internal logic for reporting when LPM position is in motion (or is about to or just was) because of some higher level ongoing action</summary>
        string ExplicitInMotionReason { get; }

        /// <summary>Returns true when the driver (state publisher) believes that the device is, or may be, in motion</summary>
        bool IsInMotion { get; }

        /// <summary>The clamp actuator is in the clamped position (AtPos2)</summary>
        bool IsClamped { get; }
        /// <summary>The clamp actuator is in the unclamped position (AtPos1)</summary>
        bool IsUnclamped { get; }
        /// <summary>The dock actuator is in the docked position (AtPos2)</summary>
        bool IsDocked { get; }
        /// <summary>The dock actuator is in the undocked position (AtPos1)</summary>
        bool IsUndocked { get; }
        /// <summary>True if vacuum is enabled to the suction cups (AtPos2)</summary>
        bool IsVacEnabled { get; }
        /// <summary>True if vacuum is disabled from the suction cups (AtPos1)</summary>
        bool IsVacDisabled { get; }
        /// <summary>True if the door keys are in the horizontal position.  Any attached door has been unlatched from its carrier.  (AtPos2)</summary>
        bool AreDoorKeysHorizontal { get; }
        /// <summary>True if the door keys are in the vertical position.  Any attached door has been latched to its carrier.  (AtPos1)</summary>
        bool AreDoorKeysVertical { get; }
        /// <summary>True of the door open actuator is in the open position.  (AtPos2)</summary>
        bool IsDoorOpen { get; }
        /// <summary>True of the door open actuator is in the closed position.  (AtPos1)</summary>
        bool IsDoorClosed { get; }
        /// <summary>True of the door up/down actuator is in the down position.  (AtPos2)</summary>
        bool IsDoorDown { get; }
        /// <summary>True of the door up/down actuator is in the up position.  (AtPos1)</summary>
        bool IsDoorUp { get; }

        /// <summary>Generates and returns a PositionSummary value for the current position.</summary>
        PositionSummary PositionSummary { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IPositionState rhs);
    }

    /// <summary>
    /// Represents the standard set of positions that a LPM part can physically be in.
    /// </summary>
    public enum PositionSummary
    {
        UndockedUnclamped,
        Undocked,
        UndockedClamped,        
        DockedDoorLatched,      // or door not present
        DockedDoorUnlatched,
        DockedDoorOpen,
        DockedCarrierOpen,
        InMotion,
        ServoOff,
        Other,
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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
            MotionILockSensorIsTripped = rhs.MotionILockSensorIsTripped;
            ProtrusionSensorIsTripped = rhs.ProtrusionSensorIsTripped;
            IsCarrierDoorDetected = rhs.IsCarrierDoorDetected;
            IsCarrierOpen = rhs.IsCarrierOpen;
            IsCarrierClosed = rhs.IsCarrierClosed;
            IsValid = rhs.IsValid;
            IsSafeToAccess = rhs.IsSafeToAccess;
            IsSafeForManualCarrierHandoff = rhs.IsSafeForManualCarrierHandoff;
            ExplicitInMotionReason = rhs.ExplicitInMotionReason;
            
            return this;
        }

        public PositionState SetFrom(IActuatorStates actuatorStates, bool isValid = true)
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

            IsValid = isValid && statesValid && IsReferenced && IsServoOn;

            return this;
        }

        /// <summary>Clamped == AtPos2, Unclamped/Released == AtPos1</summary>
        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public ActuatorPosition ClampState { get; set; }

        /// <summary>Docked == AtPos2, Undocked == AtPos1</summary>
        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public ActuatorPosition DockState { get; set; }

        /// <summary>Vacuum turned on == AtPos2, Vacuum turned off == AtPos1</summary>
        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public ActuatorPosition VacState { get; set; }

        /// <summary>Door Keys Horizontal (door unlatched from carrier) == AtPos2, Door Keys Vertical (door latched in carrier) == AtPos1</summary>
        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
        public ActuatorPosition DoorKeysState { get; set; }

        /// <summary>Door Open (away from carrier) == AtPos2, Door Closed (pressed into carrier) == AtPos1</summary>
        [DataMember(Order = 500, IsRequired = false, EmitDefaultValue = false)]
        public ActuatorPosition DoorOpenState { get; set; }
       
        /// <summary>Door Down == AtPos2, Door Up == AtPos1</summary>
        [DataMember(Order = 600, IsRequired = false, EmitDefaultValue = false)]
        public ActuatorPosition DoorDownState { get; set; }

        public bool AreAllActuatorPositionsValid { get { return (ClampState.IsValid() && DockState.IsValid() && VacState.IsValid() && DoorKeysState.IsValid() && DoorOpenState.IsValid() && DoorDownState.IsValid()); } }

        /// <summary>This property returns true if the device has been initialized/homed/referenced since we last started communicating with it</summary>
        [DataMember(Order = 700, IsRequired = false, EmitDefaultValue = false)]
        public bool IsReferenced { get; set; }

        /// <summary>This property returns true if the driver believes that the device is activly controlling its position.</summary>
        [DataMember(Order = 800, IsRequired = false, EmitDefaultValue = false)]
        public bool IsServoOn { get; set; }

        [DataMember(Order = 900, IsRequired = false, EmitDefaultValue = false)]
        public bool MotionILockSensorIsTripped { get; set; }

        [DataMember(Order = 1000, IsRequired = false, EmitDefaultValue = false)]
        public bool ProtrusionSensorIsTripped { get; set; }

        [DataMember(Order = 1100, IsRequired = false, EmitDefaultValue = false)]
        public bool IsCarrierDoorDetected { get; set; }

        [DataMember(Order = 1200, IsRequired = false, EmitDefaultValue = false)]
        public bool IsCarrierOpen { get; set; }

        [DataMember(Order = 1300, IsRequired = false, EmitDefaultValue = false)]
        public bool IsCarrierClosed { get; set; }

        [DataMember(Order = 1400, IsRequired = false, EmitDefaultValue = false)]
        public bool IsValid { get; set; }

        [DataMember(Order = 1500, IsRequired = false, EmitDefaultValue = false)]
        public bool IsSafeToAccess { get; set; }

        /// <summary>
        /// LPM position is safe for the Carrier to be manually placed or removed.  
        /// Generally this means that the position is valid, the door is up and closed, and the PDO is undocked and unclamped.
        /// </summary>
        [DataMember(Order = 1600, IsRequired = false, EmitDefaultValue = false)]
        public bool IsSafeForManualCarrierHandoff { get; set; }

        public string InMotionReason 
        {
            get
            {
                string localInMotionReason = string.Empty;

                if (ClampState == ActuatorPosition.MovingToPos2)
                    localInMotionReason = "Clamping";
                else if (ClampState == ActuatorPosition.MovingToPos1)
                    localInMotionReason = "Unclamping";
                else if (DockState == ActuatorPosition.MovingToPos2)
                    localInMotionReason = "Docking";
                else if (DockState == ActuatorPosition.MovingToPos1)
                    localInMotionReason = "Undocking";
                else if (VacState == ActuatorPosition.MovingToPos2)
                    localInMotionReason = "Enabling Carrier Door Vacuum";
                else if (VacState == ActuatorPosition.MovingToPos1)
                    localInMotionReason = "Releasing Carrier Door Vacuum";
                else if (DoorKeysState == ActuatorPosition.MovingToPos2)
                    localInMotionReason = "Unlatching Carrier Door";
                else if (DoorKeysState == ActuatorPosition.MovingToPos1)
                    localInMotionReason = "Latching Carrier Door";
                else if (DoorOpenState == ActuatorPosition.MovingToPos2)
                    localInMotionReason = "Opening Carrier Door";
                else if (DoorOpenState == ActuatorPosition.MovingToPos1)
                    localInMotionReason = "Closing Carrier Door";
                else if (DoorDownState == ActuatorPosition.MovingToPos2)
                    localInMotionReason = "Moving Door Down";
                else if (DoorDownState == ActuatorPosition.MovingToPos1)
                    localInMotionReason = "Moving Door Up";
                else
                    return ExplicitInMotionReason;

                if (ExplicitInMotionReason.IsNullOrEmpty())
                    return localInMotionReason;

                return "{0} [{1}]".CheckedFormat(ExplicitInMotionReason, localInMotionReason);
            }
        }

        /// <summary>
        /// Setter also clears IsSafeToAccess if given value is neither null nor empty
        /// </summary>
        public string ExplicitInMotionReason
        {
            get { return explicitInMotionReason ?? string.Empty; }
            set
            {
                explicitInMotionReason = value.MapEmptyToNull();

                if (!explicitInMotionReason.IsNullOrEmpty())
                {
                    IsSafeToAccess = false;
                    IsSafeForManualCarrierHandoff = false;
                }
            }
        }

        [DataMember(Order = 1600, Name = "ExplicitInMotionReason", IsRequired = false, EmitDefaultValue = false)]
        private string explicitInMotionReason;

        public bool Equals(IPositionState other)
        {
            return (other != null
                    && ClampState == other.ClampState
                    && DockState == other.DockState
                    && VacState == other.VacState
                    && DoorKeysState == other.DoorKeysState
                    && DoorOpenState == other.DoorOpenState
                    && DoorDownState == other.DoorDownState
                    && IsReferenced == other.IsReferenced
                    && IsServoOn == other.IsServoOn
                    && MotionILockSensorIsTripped == other.MotionILockSensorIsTripped
                    && ProtrusionSensorIsTripped == other.ProtrusionSensorIsTripped
                    && ExplicitInMotionReason == other.ExplicitInMotionReason
                    && IsCarrierDoorDetected == other.IsCarrierDoorDetected
                    && IsCarrierOpen == other.IsCarrierOpen
                    && IsCarrierClosed == other.IsCarrierClosed
                    && IsValid == other.IsValid
                    && IsSafeToAccess == other.IsSafeToAccess
                    && IsSafeForManualCarrierHandoff == other.IsSafeForManualCarrierHandoff
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IPositionState rhs)
        {
            return Equals(rhs);
        }

        public bool IsInMotion { get { return !InMotionReason.IsNullOrEmpty(); } }
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

        public PositionSummary PositionSummary
        {
            get 
            {
                if (IsInMotion)
                    return PositionSummary.InMotion;
                else if (IsUnclamped && IsUndocked && AreDoorKeysVertical && IsDoorClosed && IsDoorUp)
                    return PositionSummary.UndockedUnclamped;
                else if (IsClamped && IsUndocked && AreDoorKeysVertical && IsDoorClosed && IsDoorUp)
                    return PositionSummary.UndockedClamped;
                else if (IsClamped && IsDocked && AreDoorKeysVertical && IsDoorClosed && IsDoorUp)
                    return PositionSummary.DockedDoorLatched;
                else if (IsClamped && IsDocked && AreDoorKeysHorizontal && IsDoorClosed && IsDoorUp)
                    return PositionSummary.DockedDoorUnlatched;
                else if (IsClamped && IsDocked && IsDoorOpen && IsDoorUp)
                    return PositionSummary.DockedDoorOpen;          // latch key position and use of vacuum depends on door actually being present - remove from rest of tests.
                else if (IsClamped && IsDocked && IsDoorOpen && IsDoorDown)
                    return PositionSummary.DockedCarrierOpen;
                else if (!IsServoOn)
                    return PositionSummary.ServoOff;
                else
                    return PositionSummary.Other;
            }
        }
            
        /// <summary>Supports debugging and logging.</summary>
        public override string ToString()
        {
            PositionSummary posSummary = PositionSummary;

            string motionILockStr = (MotionILockSensorIsTripped ? " MotILock" : "");
            string wsoStr = (ProtrusionSensorIsTripped ? " WSO" : "");
            string vacYN = ((IsVacEnabled && IsCarrierDoorDetected) ? "Yes" : "No");

            switch (posSummary)
            {
                default: return "{0} vac:{1}{2}{3}".CheckedFormat(posSummary, vacYN, motionILockStr, wsoStr);
                case PositionSummary.InMotion: return "InMotion:{0} vac:{1}{2}{3}".CheckedFormat(InMotionReason, vacYN, motionILockStr, wsoStr);
                case PositionSummary.ServoOff:
                case PositionSummary.Other: return "{0} vac:{1}{2}{3}".CheckedFormat(posSummary, vacYN, motionILockStr, wsoStr);
            }
        }
    }

    public interface IActuatorStates : IEquatable<IActuatorStates>
    {
        IActuatorState ClampState { get; }
        IActuatorState DockState { get; }
        IActuatorState VacState { get; }
        IActuatorState DoorKeysState { get; }
        IActuatorState DoorOpenState { get; }
        IActuatorState DoorDownState { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IActuatorStates rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public IActuatorState ClampState { get; set; }

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public IActuatorState DockState { get; set; }

        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public IActuatorState VacState { get; set; }

        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
        public IActuatorState DoorKeysState { get; set; }

        [DataMember(Order = 500, IsRequired = false, EmitDefaultValue = false)]
        public IActuatorState DoorOpenState { get; set; }

        [DataMember(Order = 600, IsRequired = false, EmitDefaultValue = false)]
        public IActuatorState DoorDownState { get; set; }

        public bool Equals(IActuatorStates other)
        {
            return (other != null
                    && ClampState.Equals(other.ClampState)
                    && DockState.Equals(other.DockState)
                    && VacState.Equals(other.VacState)
                    && DoorKeysState.Equals(other.DoorKeysState)
                    && DoorOpenState.Equals(other.DoorOpenState)
                    && DoorDownState.Equals(other.DoorDownState)
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IActuatorStates rhs)
        {
            return Equals(rhs);
        }
    }

    #endregion

    #region DisplayState, ButtonSet

    public interface IDisplayState : IEquatable<IDisplayState>
    {
        IDisplayItemState[] ButtonItemArray { get; }
        IDisplayItemState[] PanelItemArray { get; }
        IDisplayItemState[] AllItemArray { get; }
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public DisplayItemState[] ButtonItemArray { get; private set; }
        
        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public DisplayItemState[] PanelItemArray { get; private set; }

        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public DisplayItemState[] AllItemArray { get; private set; }

        IDisplayItemState[] IDisplayState.ButtonItemArray { get { return this.ButtonItemArray; } }
        IDisplayItemState[] IDisplayState.PanelItemArray { get { return this.PanelItemArray; } }
        IDisplayItemState[] IDisplayState.AllItemArray { get { return this.AllItemArray; } }

        public bool Equals(IDisplayState other)
        {
            return (other != null
                    && AllItemArray.IsEqualTo(other.AllItemArray)
                    );
        }


        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IDisplayState rhs)
        {
            return Equals(rhs);
        }

        public void ServiceFlashing(bool flashState)
        {
            foreach (DisplayItemState item in PanelItemArray)
                item.FlashStateIsOn = flashState;
            foreach (DisplayItemState item in ButtonItemArray)
                item.FlashStateIsOn = flashState;
        }
    }

    public interface IButtonSet : IEquatable<IButtonSet>
    {
        bool Button1 { get; }
        bool Button2 { get; }

        int Button1ChangeCount { get; }
        int Button2ChangeCount { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IButtonSet rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public bool Button1 { get; set; }

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public bool Button2 { get; set; }

        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public int Button1ChangeCount { get; set; }

        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
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

        public bool Equals(IButtonSet other)
        {
            return (other != null 
                    && Button1 == other.Button1 
                    && Button2 == other.Button2
                    && Button1ChangeCount == other.Button1ChangeCount 
                    && Button2ChangeCount == other.Button2ChangeCount
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IButtonSet rhs)
        {
            return Equals(rhs);
        }

        public override string ToString()
        {
            string[] buttonsDown = { (Button1 ? "B1" : string.Empty), (Button2 ? "B2" : string.Empty) };

            return "{0} chgCounts: {1} {2}".CheckedFormat(string.Join(" ", buttonsDown.Where(s => !s.IsNullOrEmpty()).ToArray()).MapNullOrEmptyTo("None"), Button1ChangeCount, Button2ChangeCount);
        }
    }

    public interface IDisplayItemState : IEquatable<IDisplayItemState>
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
        bool IsInstalled { get; }
        DisplayItemState.OnOffFlashState LastLampCmdState { get; }
        bool FlashStateIsOn { get; }
        string CurrentBackgroundColor { get; }
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
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

        public DisplayItemState() : this("------", "Black", false, false) { }
        public DisplayItemState(string text) : this(text, "Black", false, !text.IsNullOrEmpty()) { }
        public DisplayItemState(string text, string colorFamily) : this(text, colorFamily, false) { }
        public DisplayItemState(string text, string colorFamily, bool isInternal) : this(text, colorFamily, isInternal, !text.IsNullOrEmpty() || !colorFamily.IsNullOrEmpty()) { }
        public DisplayItemState(string text, string colorFamily, bool isInternal, bool isInstalled)
        {
            Text = text;
            State = OnOffFlashState.Off;
            IsInternal = isInternal;
            IsInstalled = isInstalled;

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

        public DisplayItemState SetFrom(IDisplayItemState rhs, bool includeItemIdx = true, bool includeState = true)
        {
            IsButton = rhs.IsButton;
            if (includeItemIdx)
                ItemIdx = rhs.ItemIdx;
            Text = rhs.Text;
            BorderColor = rhs.BorderColor;
            OffBackgroundColor = rhs.OffBackgroundColor;
            OnBackgroundColor = rhs.OnBackgroundColor;
            if (includeState)
                State = rhs.State;
            LastLampCmdState = rhs.LastLampCmdState;
            IsInternal = rhs.IsInternal;
            IsInstalled = rhs.IsInstalled;
            FlashStateIsOn = rhs.FlashStateIsOn;

            return this;
        }

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public bool IsButton { get; set; }

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public int ItemIdx { get; set; }

        public int LampNum { get { return (ItemIdx + 1 + (IsButton ? DisplayState.ButtonLampNumOffset : 0)); } }

        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public string Text { get; set; }

        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
        public string BorderColor { get; set; }

        [DataMember(Order = 500, IsRequired = false, EmitDefaultValue = false)]
        public string OffBackgroundColor { get; set; }

        [DataMember(Order = 600, IsRequired = false, EmitDefaultValue = false)]
        public string OnBackgroundColor { get; set; }

        [DataMember(Order = 700, IsRequired = false, EmitDefaultValue = false)]
        public OnOffFlashState State { get; set; }

        [DataMember(Order = 800, IsRequired = false, EmitDefaultValue = false)]
        public bool IsInternal { get; set; }

        [DataMember(Order = 900, IsRequired = false, EmitDefaultValue = false)]
        public bool IsInstalled { get; set; }

        [DataMember(Order = 1000, IsRequired = false, EmitDefaultValue = false)]
        public OnOffFlashState LastLampCmdState { get; set; }

        [DataMember(Order = 1100, IsRequired = false, EmitDefaultValue = false)]
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
            return Equals(obj as IDisplayItemState);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return "{0} {1} {2}".CheckedFormat(Text, State, CurrentBackgroundColor);
        }

        public bool Equals(IDisplayItemState other)
        {
            return (other != null
                    && IsButton == other.IsButton
                    && ItemIdx == other.ItemIdx
                    && Text == other.Text
                    && BorderColor == other.BorderColor
                    && OffBackgroundColor == other.OffBackgroundColor
                    && OnBackgroundColor == other.OnBackgroundColor
                    && State == other.State
                    && IsInternal == other.IsInternal
                    && LastLampCmdState == other.LastLampCmdState
                    && FlashStateIsOn == other.FlashStateIsOn
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IDisplayItemState rhs)
        {
            return Equals(rhs);
        }
    }

    #endregion

    #region E84State

    public interface IE84State : IEquatable<IE84State>
    {
        PartsLib.Common.E084.StateCode StateCode { get; }
        string StateCodeReason { get; }

        IPassiveToActivePinsState OutputSetpoint { get; }
        IPassiveToActivePinsState OutputReadback { get; }
        bool OutputSetpointPinsMatchesReadback { get; }
        IActiveToPassivePinsState Inputs { get; }

        bool IsEmpty { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IE84State rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
    public class E84State : IE84State
    {        
        public E84State() 
        {
            SetFrom(null);
        }

        public E84State(IE84State rhs)
        {
            SetFrom(rhs);
        }

        public E84State SetFrom(IE84State rhs)
        {
            if (rhs != null)
            {
                StateCode = rhs.StateCode;
                StateCodeReason = rhs.StateCodeReason;
                OutputSetpoint = new PassiveToActivePinsState(rhs.OutputSetpoint);
                OutputReadback = new PassiveToActivePinsState(rhs.OutputReadback);
                Inputs = new ActiveToPassivePinsState(rhs.Inputs);
            }
            else
            {
                StateCode = default(PartsLib.Common.E084.StateCode);
                StateCodeReason = string.Empty;
                OutputSetpoint = default(PassiveToActivePinsState);
                OutputReadback = default(PassiveToActivePinsState);
                Inputs = default(ActiveToPassivePinsState);
            }

            return this;
        }

        public E84State Clear()
        {
            return SetFrom(null);
        }

        public bool IsEmpty { get { return Equals(emptyE84State); } }

        private readonly static IE84State emptyE84State = new E84State();

        IPassiveToActivePinsState IE84State.OutputSetpoint { get { return this.OutputSetpoint; } }
        IPassiveToActivePinsState IE84State.OutputReadback { get { return this.OutputReadback; } }
        IActiveToPassivePinsState IE84State.Inputs { get { return this.Inputs; } }

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public PartsLib.Common.E084.StateCode StateCode { get; set; }

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public string StateCodeReason { get; set; }
        
        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public PassiveToActivePinsState OutputSetpoint { get; set; }

        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
        public PassiveToActivePinsState OutputReadback { get; set; }
    
        [DataMember(Order = 500, IsRequired = false, EmitDefaultValue = false)]
        public ActiveToPassivePinsState Inputs { get; set; }

        public bool OutputSetpointPinsMatchesReadback { get { return (OutputSetpoint.PackedWord == OutputReadback.PackedWord); } }

        public bool Equals(IE84State other)
        {
            return (other != null
                    && StateCode == other.StateCode
                    && StateCodeReason == other.StateCodeReason
                    && OutputSetpoint.Equals(other.OutputSetpoint)
                    && OutputReadback.Equals(other.OutputReadback)
                    && Inputs.Equals(other.Inputs)
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IE84State rhs)
        {
            return Equals(rhs);
        }

        /// <summary>Supports debugging and logging.</summary>
        public override string ToString()
        {
            if (IsEmpty)
                return "Empty";
            else if (OutputSetpointPinsMatchesReadback)
                return "state:{0} reason:'{1}' in:{2} out:{3} (match)".CheckedFormat(StateCode, StateCodeReason, Inputs, OutputSetpoint);
            else
                return "state:{0} reason:'{1}' in:{2} out:{3} (!= {4})".CheckedFormat(StateCode, StateCodeReason, Inputs, OutputSetpoint, OutputReadback);
        }
    }

    #endregion

    #region Common extension methods

    public static partial class ExtensionMethods
    {
        public static bool IsClamp(this MotionAction value) { return value == MotionAction.Clamp; }
        public static bool IsUnclamp(this MotionAction value) { return value == MotionAction.Unclamp; }
        public static bool IsDock(this MotionAction value) { return value == MotionAction.Dock; }
        public static bool IsUndock(this MotionAction value) { return value == MotionAction.Undock; }
        public static bool IsOpen(this MotionAction value) { return value == MotionAction.Open; }
        public static bool IsClose(this MotionAction value) { return value == MotionAction.Close; }
        public static bool IsMap(this MotionAction value) { return value == MotionAction.Map; }
        public static bool IsInitialize(this MotionAction value) { return value == MotionAction.Initialize; }

        public static bool IsNeitherPresentNorPlaced(this PresentPlaced value) { return (value == PresentPlaced.None); }
        public static bool IsPresent(this PresentPlaced value) { return value.IsSet(PresentPlaced.Present); }
        public static bool IsPlaced(this PresentPlaced value) { return value.IsSet(PresentPlaced.Placed); }
        public static bool IsProperlyPlaced(this PresentPlaced value) { return value.Matches(PresentPlaced.Present | PresentPlaced.Placed, PresentPlaced.Present | PresentPlaced.Placed); }
        public static bool DoesPlacedEqualPresent(this PresentPlaced value) { return value.IsSet(PresentPlaced.Present) == value.IsSet(PresentPlaced.Placed); }
        public static bool IsPlacedOrPresent(this PresentPlaced value) { return value.IsSet(PresentPlaced.Present) || value.IsSet(PresentPlaced.Placed); }

        public static bool IsSet(this MotionAction value, MotionAction test) { return value.Matches(test, test); }
        public static bool IsSet(this PresentPlaced value, PresentPlaced test) { return value.Matches(test, test); }
        public static bool IsSet(this InfoPads value, InfoPads test) { return value.Matches(test, test); }
        public static bool IsSet(this CarrierType value, CarrierType test) { return value.Matches(test, test); }
        public static bool IsSet(this OCA value, OCA test) { return value.Matches(test, test); }

        public static bool Matches(this MotionAction testValue, MotionAction mask, MotionAction expectedValue) { return ((testValue & mask) == expectedValue); }
        public static bool Matches(this PresentPlaced testValue, PresentPlaced mask, PresentPlaced expectedValue) { return ((testValue & mask) == expectedValue); }
        public static bool Matches(this InfoPads testValue, InfoPads mask, InfoPads expectedValue) { return ((testValue & mask) == expectedValue); }
        public static bool Matches(this CarrierType testValue, CarrierType mask, CarrierType expectedValue) { return ((testValue & mask) == expectedValue); }
        public static bool Matches(this OCA testValue, OCA mask, OCA expectedValue) { return ((testValue & mask) == expectedValue); }
    }

    #endregion
}
