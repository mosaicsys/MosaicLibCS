//-------------------------------------------------------------------
/*! @file E084PassiveTransferState.cs
 * @brief This file defines the E084 passive transfer state machine (et. al.).
 *
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2016 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version(s) E084PassiveTransferState.cpp and E084PassiveTransferState.h)
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

using MosaicLib;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.Modular.Part;
using MosaicLib.Semi.E041;
using MosaicLib.Semi.E087;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.PartsLib.Common.LPM;

namespace MosaicLib.PartsLib.Common.E084
{
    #region StateCode enumeration and related extension methods

    /*! Enum Code
        The following enum defines the set of discrete and distinct "states" which a 
        E084 passive state can reasonably be consolidated into.  The design of this consolidation
        is based on the following rules:

        A) Implement a distinct state transition for each normal change in passive to active pin states
        B) Implement a distinct state transition for each normal (and complete) change in active to passive pin states.
        C) Implement additional states as needed to inform the Active side about handoff failures
        D) Implement additional states as needed to correctly handle and recover from aberrant cases.

        As you can see most of the states are used to define the normal, happy path, handoff between.
        For unloading this patterns looks like:
            NotAvail->Available
                ->Selected->SelectedAndUnloadable->RequestStartUnload->ReadyToStartUnload
                ->Unloading->UnloadingAndPodRemoved->UnloadTransferComplete->StateCode.UnloadCompleted
                ->[Available or AvailableContinuous]
        For loading this patterns looks like:
            [Available or AvailableContinuous]->Selected->SelectedAndLoadable->RequestStartLoad->ReadyToStartLoad
                ->Loading->LoadingAndPodPresent->LoadTransferComplete->StateCode.LoadCompleted
                ->NotAvail

        The above patterns show the full range of normal handoff patterns including the use of
        the CONT flag to indicate that the AMHS expects to perform a load immediately after
        performing the unload.

        More detailed description of each state:

        NotAvail_PortNotInit:		ES,-HO [-L_REQ,-U_REQ,-READY]
        NotAvail_PortIsInitializing: ES, -HO [-L_REQ,-U_REQ,-READY]
        NotAvail_HardwareNotInstalled: -ES,-HO [-L_REQ,-U_REQ,-READY]
        NotAvail_PortNotInService:	ES,-HO [-L_REQ,-U_REQ,-READY]
        NotAvail_PortNotInAutoMode:	ES,-HO [-L_REQ,-U_REQ,-READY]
        NotAvail_TransferBlocked:	ES,-HO [-L_REQ,-U_REQ,-READY]
        NotAvail_PodPlacementFault:	ES,-HO [-L_REQ,-U_REQ,-READY]
        NotAvail_LightCurtainIlockTripped: ES (configurable), -HO [-L_REQ,-U_REQ,-READY]

        Fault_IONotInstalled:		-ES, -HO, ...: error interacting with non-existent E84 interface (ex: Auto mode but E84 not installed)
        Fault_IOFailure:			    -ES, -HO, ...: error interacting with E84 interface (state error or output readback timeout)
        Fault_InvalidSelect:		    ES,-HO (invalid select while not in fault state: VALID, CS_0 or CS_1 when -HO, or VALID and CS_1 when HO)
        Fault_InvalidActivePins:	    ES,-HO (unexpected active pin transition or not idle in fault state)
        Fault_TransferAborted:		ES,-HO (external signal triggered loss of transfer - auto->manual during transfer, port out of service during transfer)
        Fault_TransferAbortedByInterlock:	-ES, -HO, or ES, -HO (depends on configuration) - transfer was aborted because (light curtain) interlock was tripped during an active transfer.
        Fault_TP1_Timeout:			ES,-HO
        Fault_TP2_Timeout:			ES,-HO
        Fault_TP3_Timeout:			ES,-HO
        Fault_TP4_Timeout:			ES,-HO
        Fault_TP5_Timeout:			ES,-HO
        Fault_TP6_Timeout:			ES,-HO
        Fault_PodTransitionTimeout: ES,-HO
        Fault_UnexpectedPodPlacement: ES,-HO

        Available:					ES,HO:  PTS is R2L or R2U, PAM is auto.  Waiting for CS_0 and VALID
        AvailableContinuous:		    ES,HO,CONT: wait for CS_0 and VALID then transition to Selected.
        Selected:					ES,HO,CS_0,VALID: Assert L_REQ or U_REQ based on PTS and transition to
                                    SelectedAnd{Loadable|Unloadable}.
        SelectedAndLoadable:		    ES,HO,L_REQ,CS_0,VALID: Waiting for TR_REQ
        SelectedAndUnloadable:		ES,HO,U_REQ,CS_0,VALID: Waiting for TR_REQ

        RequestStartLoad:			ES,HO,L_REQ,CS_0,VALID,TR_REQ: signal load transfer start and wait for ack, then assert READY
        ReadyToStartLoad:			ES,HO,L_REQ,READY,CS_0,VALID,TR_REQ: wait for BUSY
        Loading:					    ES,HO,L_REQ,READY,CS_0,VALID,TR_REQ,BUSY: wait for podPresence==ConfirmedPlaced then clear L_REQ
        LoadingAndPodPresent:		ES,HO,-L_REQ,READY,CS_0,VALID,TR_REQ,BUSY: wait for -TR_REQ,-BUSY and COMPT
        LoadTransferDone:		    ES,HO,READY,CS_0,VALID,-TR_REQ,-BUSY,COMPT: clear READY and transition to LoadTransferDoneWaitReleased
        LoadTransferDoneWaitReleased:       ES,HO,-READY: wait for -CS_0,-VALID and -COMPT, transition to LoadCompleted
        - to be removed LoadCompleteSignaled:		ES,HO,-READY: signal transfer complete, wait for ack, transition to LoadCompleted
        LoadCompleted:				ES,-HO,-READY: wait for -E84LoadInProgress: change to Idle or NotAvailable based on PTS.

        RequestStartUnload:		    ES,HO,U_REQ,CS_0,VALID,TR_REQ: signal unload transfer start (undocks and unclamps as needed) and wait for ack and then assert READY
        ReadyToStartUnload:			ES,HO,U_REQ,READY,CS_0,VALID,TR_REQ: wait for BUSY
        Unloading:					ES,HO,U_REQ,READY,CS_0,VALID,TR_REQ,BUSY: wait for podPresence==ConfirmedRemoved then clear U_REQ
        UnloadingAndPodRemoved:		ES,HO,-U_REQ,READY,CS_0,VALID,TR_REQ,BUSY: wait for -TR_REQ,-BUSY and COMPT
        UnloadTransferDone:		    ES,HO,READY,CS_0,VALID,-TR_REQ,-BUSY,COMPT: clear READY and transition to UnloadTransferDoneWaitReleased
        UnloadTransferDoneWaitReleased:     ES,HO,-READY: wait for -CS_0,-VALID and -COMPT: change to UnloadCompleted
        - to be removed UnloadCompleteSignaled:		ES,HO,-READY: signal transfer complete, wait for ack, transition to StateCode.UnloadCompleted
        UnloadCompleted:			    ES,-HO,-READY: wait for -E84UnloadInProgress: change to Idle, IdleContinuous or NotAvailable based CONT and on PTS.

        Note: Fault_ states all share a common behavior as follows:
            a) PIO_Failed alarm will be signaled.
            b) PIO_Failed alarm will accept the acknowledge recovery if all fault conditions have been resolved
            c) Transition following accepted acknowledge recovery is to default non-active state.
            d) If triggering fault condition for current state is resolved and another fault condition is active
                then state will transition to appropriate fault condition state.
            e) Transition to Fault_IOFailure is done immediately even if in another fault state.
            f) InvalidSelect and UnexpectedPodPlacement fault states can only normally be detected while in a non-fault state.
     */

    ///<summary>
    ///	The following enum defines the set of discrete and distinct "states" which a 
    /// E084 passive state can reasonably be consolidated into.  See notes above for more details.
    ///</summary>
    public enum StateCode : int
    {
        Initial = 0,

        NotAvail_PortNotInit, NotAvail_PortIsInitializing,
        NotAvail_HardwareNotInstalled,
        NotAvail_PortNotInService, NotAvail_PortNotInAutoMode,
        NotAvail_TransferBlocked, NotAvail_PodPlacementFault,
        NotAvail_LightCurtainIlockTripped,

        Fault_IONotInstalled, Fault_IOFailure, Fault_InvalidSelect, Fault_InvalidActivePins,
        Fault_TransferAborted, Fault_TransferAbortedByInterlock,
        Fault_TP1_Timeout, Fault_TP2_Timeout, Fault_TP3_Timeout, Fault_TP4_Timeout, Fault_TP5_Timeout, Fault_TP6_Timeout,
        Fault_PodTransitionTimeout,		// Fault when a pod placement or removal does not occur within the configured maximum time.
        Fault_UnexpectedPodPlacement,	// Fault when a pod placement occurs at an unexpected time.

        Available, AvailableContinuous, Selected, SelectedAndLoadable, SelectedAndUnloadable,

        RequestStartLoad, ReadyToStartLoad,
        Loading, LoadingAndPodPresent, LoadTransferDone, LoadTransferDoneWaitReleased, 
        LoadCompleted,

        RequestStartUnload, ReadyToStartUnload,
        Unloading, UnloadingAndPodRemoved, UnloadTransferDone, UnloadTransferDoneWaitReleased,
        UnloadCompleted,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given stateCode is in any error state</summary>
        public static bool IsFaulted(this StateCode stateCode)
        {
            switch (stateCode)
            {
                // list all of the non-fault states first:
                case StateCode.Initial:
                case StateCode.NotAvail_PortNotInit:
                case StateCode.NotAvail_PortIsInitializing:
                case StateCode.NotAvail_HardwareNotInstalled:
                case StateCode.NotAvail_PortNotInService:
                case StateCode.NotAvail_PortNotInAutoMode:
                case StateCode.NotAvail_TransferBlocked:
                case StateCode.NotAvail_PodPlacementFault:
                case StateCode.NotAvail_LightCurtainIlockTripped:
                case StateCode.Available:
                case StateCode.AvailableContinuous:
                case StateCode.Selected:
                case StateCode.SelectedAndLoadable:
                case StateCode.SelectedAndUnloadable:
                case StateCode.RequestStartLoad:
                case StateCode.ReadyToStartLoad:
                case StateCode.Loading:
                case StateCode.LoadingAndPodPresent:
                case StateCode.LoadTransferDone:
                case StateCode.LoadTransferDoneWaitReleased:
                case StateCode.LoadCompleted:
                case StateCode.RequestStartUnload:
                case StateCode.ReadyToStartUnload:
                case StateCode.Unloading:
                case StateCode.UnloadingAndPodRemoved:
                case StateCode.UnloadTransferDone:
                case StateCode.UnloadTransferDoneWaitReleased:
                case StateCode.UnloadCompleted:
                    return false;

                default:
                    // all of the unlisted states are fault states.
                    return true;
            }
        }

        /// <summary>Returns true if the given stateCode is in any NotAvail state</summary>
        public static bool IsNotAvailable(this StateCode stateCode)
        {
            switch (stateCode)
            {
                case StateCode.NotAvail_PortNotInit:
                case StateCode.NotAvail_PortIsInitializing:
                case StateCode.NotAvail_HardwareNotInstalled:
                case StateCode.NotAvail_PortNotInService:
                case StateCode.NotAvail_PortNotInAutoMode:
                case StateCode.NotAvail_TransferBlocked:
                case StateCode.NotAvail_PodPlacementFault:
                case StateCode.NotAvail_LightCurtainIlockTripped:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Returns true if the given stateCode is in any available/idle state.  Excludes Continuous states by default.  See includesContinuous property</summary>
        public static bool IsAvailable(this StateCode stateCode, bool includeContinuous = false)
        {
            if (stateCode == StateCode.Available)
                return true;

            if (stateCode == StateCode.AvailableContinuous && includeContinuous)
                return true;

            return false;
        }

        /// <summary>Returns true if the given stateCode is in a Selected state.</summary>
        public static bool IsSelected(this StateCode stateCode)
        {
            switch (stateCode)
            {
                case StateCode.Selected:
                case StateCode.SelectedAndLoadable:
                case StateCode.SelectedAndUnloadable:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Returns true if the given stateCode is in a Loading state.</summary>
        public static bool IsLoading(this StateCode stateCode)
        {
            switch (stateCode)
            {
                case StateCode.RequestStartLoad:
                case StateCode.ReadyToStartLoad:
                case StateCode.Loading:
                case StateCode.LoadingAndPodPresent:
                case StateCode.LoadTransferDone:
                case StateCode.LoadTransferDoneWaitReleased:
                case StateCode.LoadCompleted:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Returns true if the given stateCode is in an Unloading state.</summary>
        public static bool IsUnloading(this StateCode stateCode)
        {
            switch (stateCode)
            {
                case StateCode.RequestStartUnload:
                case StateCode.ReadyToStartUnload:
                case StateCode.Unloading:
                case StateCode.UnloadingAndPodRemoved:
                case StateCode.UnloadTransferDone:
                case StateCode.UnloadTransferDoneWaitReleased:
                case StateCode.UnloadCompleted:
                    return true;
                default:
                    return false;
            }
        }
    }

    #endregion

    public enum PIOActiveSelect : int
    {
        /// <summary>Neither PIO1 (OHT) nor PIO2 (AGV) are active/selected</summary>
        Neither,

        /// <summary>PIO1 (usually OHT) is active/selected</summary>
        PIO1,

        /// <summary>PIO2 (usually AGV) is active/selected</summary>
        PIO2,
    };

    public class PassiveTransferStateMachine : SimplePartBase
    {
        #region Version history and string

        // E84 state machine version string and brief revision history
        //	V1.1.0 2011-04-25: First separately identified version.
        //	V1.1.1 2011-04-27: Added LCIlockClearsHO, LCIlockClearsES config point to allow passing of GC ES test using light curtain or other HO interlock signal
        //  V1.1.2 2011-05-04: Added disableInputDebounce option.  Client sets this to decrease debounce induced state change/reaction delays
        //  V1.1.3 2011-05-04: Modified Fault_IOFailure trigger logic to include timer from last match time in addition to time in state and time since last target change.
        //  V1.1.4 2011-05-11: Modified Initialize to clear held outputs during a fault state,  Modified Post initialize to explicitly attempt to return to inactive state and to clear PIOFailure (no ack needed) if state could be moved back to an inactive state.
        //						Test case is to Initialize PDO with fault output latching turned on, and auto recovery off, on E84 inputs idle case this should both clear PIO and return to a normal inactive state.
        //  V1.1.5 2011-05-25: Removed ResetAlarms method.  Tracking initialize is now used for this purpose.
        //  V1.1.6 2011-06-21: Added ServiceState call to AcknowledgeTransferComplete method so that transition to next state is atomic with caller's indication.
        //  V1.1.7 2011-06-22: Extended READY through end of LoadCompleteSignaled and UnloadCompleteSignaled to resolve race condition.  Active side signals are not expected to change in either of these states but early drop in READY allowed active side to do so legally.
        //						Added defensive coding changes in AcknowledgeTransferComplete to better identify and record the failure case.
        //  V1.1.8 2015-10-20: Added support for new Fault_UnexpectedPodPlacement state which allows PIO to fault for a specific set of pod placement transitions during states for which no such transition is expected.
        //  V1.1.9 2015-10-27: Added support for generic UpdateConfig(TransferStateMachineConfig) method that applies all possible changes from the given TSMConfig to the saved values and logs the changes.
        //  V1.1.10 2015-10-29: Added support for new permitAutoRecoveryInTransferAbortedByInterlockState config point (defaults to true).
        //  V1.1.11 2015-12-04: Added support for new permitAutoRecoveryIndexedByTPNum config points (6 elements default to true, 0:unused, 1:TP1, 2:TP2, 3:TP3, 4:TP4, 5:TP5)
        //  V2.0.0 2016-08-08: Created CS version from C++ version.
        //  V2.0.1 2016-08-16: Version passing all basic tests.

        public const string E084PassiveTransferStateMachineVersionStr = "V2.0.1 2016-08-16";

        #endregion

        #region Contruction and Release

        public PassiveTransferStateMachine(string name, Config initConfig, INotifyable notifyOnConfigChange = null, IANManagerPart anManager = null)
            : base(name)
        {
            SetupConfig(initConfig, notifyOnConfigChange);
            this.anManager = anManager ?? Semi.E041.ANManagerPart.Instance;

            if (anManager != null && !mConfig.PIOFailureAlarmName.IsNullOrEmpty())
            {
                pioFailureANSource = anManager.RegisterANSource(PartID, new ANSpec() { ANName = mConfig.PIOFailureAlarmName, ANType = ANType.Error, ALID = ANAlarmID.OptLookup, Comment = "PIO failure alarm" });
            }

            SetState(StateCode.Initial, "Constructor");

            Log.Debug.Emit("Version: {0}", E084PassiveTransferStateMachineVersionStr);

            AddExplicitDisposeAction(Release);
        }

        public void Release()
        {
            ReleaseConfig();
        }

        #endregion

        #region Configuration, SetupConfig, ReleaseConfig, ServiceConfig

        #region ConfigTimes, Config classes

        /// Todo: fix case of properties in items below (capital camel case)

        /// <summary>
        /// This class extends the E084 PassiveTimers
        /// </summary>
        public class ConfigTimes : Semi.E084.PassiveTimers
        {
            /// <summary>When non-zero this defines the maximum period where the readback of the E084 outputs does not match the last set values before the state machine will transition to a fault/alarm state.  Set to zero to disable readback mismatch time limit checking</summary>
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public double OutputReadbackMatchTimeout { get; set; }

            /// <summary>
            /// Defines the minimum time that the state machine will remain in the NotAvail_PodPlacementFault state after the being requested to continue from the alarm state.  
            /// This is typically used when auto recovery is enabled.
            /// </summary>
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public double PostPPFaultHoldoff { get; set; }

            /// <summary>When non-zero, this defines the maximum period, during a load or unload handoff, where Present (or Placed) has been triggered but the other has not before the state machine will transition to a fault/alarm state.  Set to zero to disable pod placement transition timee limit checking</summary>
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public double PodPlacementTransitionTimeout { get; set; }

            /// <summary>Debugging and Logging helper method</summary>
            public override string ToString()
            {
                return "{0} ORMT:{1:f1} PPPHO:{2:f2} PPTTO:{3:f1}".CheckedFormat(base.ToString(), OutputReadbackMatchTimeout, PostPPFaultHoldoff, PodPlacementTransitionTimeout);
            }

            /// <summary>
            /// Default constructor:
            /// OutputReadbackMatchTimeout = 5.0, PostPPFaultHoldoff = 1.0, PodPlacementTransitionTimeout = 5.0
            /// TP1 = 2.0, TP2 = 2.0, TP3 = 60.0, TP4 = 60.0, TP5 = 2.0, TP6 = 2.0
            /// </summary>
            public ConfigTimes()
            {
                SetFrom(null);
            }

            /// <summary>Copy constructor</summary>
            public ConfigTimes(ConfigTimes rhs)
            {
                SetFrom(rhs);
            }

            /// <summary>Constructor and Copy constructor helper function</summary>
            public ConfigTimes SetFrom(ConfigTimes rhs)
            {
                base.SetFrom(rhs);

                if (rhs != null)
                {
                    OutputReadbackMatchTimeout = rhs.OutputReadbackMatchTimeout;
                    PostPPFaultHoldoff = rhs.PostPPFaultHoldoff;
                    PodPlacementTransitionTimeout = rhs.PodPlacementTransitionTimeout;
                }
                else
                {
                    OutputReadbackMatchTimeout = 5.0;
                    PostPPFaultHoldoff = 1.0;
                    PodPlacementTransitionTimeout = 5.0;
                }

                return this;
            }

            /// <summary>
            /// Update this object's ConfigItem marked public properties from corresponingly named config keys (using the namePrefix)
            /// </summary>
            public new ConfigTimes Setup(string namePrefix, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
            {
                base.Setup(namePrefix, issueEmitter, valueEmitter);

                ConfigValueSetAdapter<ConfigTimes> adapter = new ConfigValueSetAdapter<ConfigTimes>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(namePrefix);

                return this;
            }

            /// <summary>
            /// Compares this object against the rhs to determine if they are both of the same type and, if so, if they have the same contents.
            /// </summary>
            /// <param name="rhsAsObject">defines the object against which this one will be compared.</param>
            /// <returns>true if both objects contain the same values, false if rhsAsObject is null, is not of this type or has different contents</returns>
            public override bool Equals(object rhsAsObject)
            {
                ConfigTimes rhs = rhsAsObject as ConfigTimes;
                return (rhs != null
                        && base.Equals(rhs)
                        && rhs.OutputReadbackMatchTimeout == OutputReadbackMatchTimeout
                        && rhs.PostPPFaultHoldoff == PostPPFaultHoldoff
                        && rhs.PodPlacementTransitionTimeout == PodPlacementTransitionTimeout
                        );
            }

            /// <summary>
            /// Override for GetHashCode so as to prevent warning due to overriden Equal method.
            /// </summary>
            /// <returns>base.GetHashCode();</returns>
            public override int GetHashCode() { return base.GetHashCode(); }
        }

        ///Todo: rename all following properties to raise case of first letter.
        /// <summary>
        /// This class defines all of the parameters that are used to statically and dynamically configure this E084 state machine.
        /// </summary>
        public class Config
        {
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool HwInstalled { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool LightCurtainInterlockInstalled { get; set; }

            /// <summary>from fault state to idle state on correction</summary>
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool EnableAutoRecovery { get; set; }

            /// <summary>works with enableAutoRecovery to determine when ES is re-asserted.  enabled by default</summary>
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool ClearESInTransferAbortedByInterlockState { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool HoldPassiveOutputsDuringFault { get; set; }

            /// <summary>alternate pio interface (AGV)</summary>
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool PIO2InterfacePresent { get; set; }

            public ConfigTimes Times { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public string PIOFailureAlarmName { get; set; }

            /// <summary>use of these flag is not equivalent to a hardware interlock and is not suitable for S2 compliance</summary>
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool LCIlockClearsHO { get; set; }

            /// <summary>use of these flag is not equivalent to a hardware interlock and is not suitable for S2 compliance</summary>
            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool LCIlockClearsES { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool EnableUnexpectedPodPlacementFault { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool PermitAutoRecoveryInTransferAbortedByInterlockState { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool PermitAutoRecoveryInTP1 { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool PermitAutoRecoveryInTP2 { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool PermitAutoRecoveryInTP3 { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool PermitAutoRecoveryInTP4 { get; set; }

            [ConfigItem(IsOptional = true)]
            [ValueSetItem]
            public bool PermitAutoRecoveryInTP5 { get; set; }

            /// <summary>
            /// Default constructor:
            /// </summary>
            public Config()
            {
                SetFrom(null);
            }

            /// <summary>Copy constructor</summary>
            public Config(Config rhs)
            {
                SetFrom(rhs);
            }

            /// <summary>Constructor and Copy constructor helper function</summary>
            public Config SetFrom(Config rhs)
            {
                if (rhs != null)
                {
                    HwInstalled = rhs.HwInstalled;
                    LightCurtainInterlockInstalled = rhs.LightCurtainInterlockInstalled;
                    EnableAutoRecovery = rhs.EnableAutoRecovery;
                    ClearESInTransferAbortedByInterlockState = rhs.ClearESInTransferAbortedByInterlockState;
                    HoldPassiveOutputsDuringFault = rhs.HoldPassiveOutputsDuringFault;
                    PIO2InterfacePresent = rhs.PIO2InterfacePresent;
                    Times = new ConfigTimes(rhs.Times);
                    PIOFailureAlarmName = rhs.PIOFailureAlarmName;
                    LCIlockClearsHO = rhs.LCIlockClearsHO;
                    LCIlockClearsES = rhs.LCIlockClearsES;
                    EnableUnexpectedPodPlacementFault = rhs.EnableUnexpectedPodPlacementFault;
                    PermitAutoRecoveryInTransferAbortedByInterlockState = rhs.PermitAutoRecoveryInTransferAbortedByInterlockState;
                    PermitAutoRecoveryInTP1 = rhs.PermitAutoRecoveryInTP1;
                    PermitAutoRecoveryInTP2 = rhs.PermitAutoRecoveryInTP2;
                    PermitAutoRecoveryInTP3 = rhs.PermitAutoRecoveryInTP3;
                    PermitAutoRecoveryInTP4 = rhs.PermitAutoRecoveryInTP4;
                    PermitAutoRecoveryInTP5 = rhs.PermitAutoRecoveryInTP5;
                }
                else
                {
                    HwInstalled = true;
                    LightCurtainInterlockInstalled = true;
                    EnableAutoRecovery = false;
                    ClearESInTransferAbortedByInterlockState = true;
                    HoldPassiveOutputsDuringFault = false;
                    PIO2InterfacePresent = false;
                    Times = new ConfigTimes();
                    PIOFailureAlarmName = string.Empty;
                    LCIlockClearsHO = false;
                    LCIlockClearsES = false;
                    EnableUnexpectedPodPlacementFault = false;
                    PermitAutoRecoveryInTransferAbortedByInterlockState = true;
                    PermitAutoRecoveryInTP1 = true;
                    PermitAutoRecoveryInTP2 = true;
                    PermitAutoRecoveryInTP3 = true;
                    PermitAutoRecoveryInTP4 = true;
                    PermitAutoRecoveryInTP5 = true;
                }

                return this;
            }

            /// <summary>
            /// Update this object's ConfigItem marked public properties from corresponingly named config keys (using the namePrefix)
            /// </summary>
            public Config Setup(string namePrefix, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
            {
                Times.Setup("{0}Times.".CheckedFormat(namePrefix), issueEmitter, valueEmitter);

                ConfigValueSetAdapter<Config> adapter = new ConfigValueSetAdapter<Config>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(namePrefix);

                return this;
            }

            /// <summary>
            /// Compares this object against the rhs to determine if they are both of the same type and, if so, if they have the same contents.
            /// </summary>
            /// <param name="rhsAsObject">defines the object against which this one will be compared.</param>
            /// <returns>true if both objects contain the same values, false if rhsAsObject is null, is not of this type or has different contents</returns>
            public override bool Equals(object rhsAsObject)
            {
                Config rhs = rhsAsObject as Config;
                return (rhs != null
                        && HwInstalled == rhs.HwInstalled
                        && LightCurtainInterlockInstalled == rhs.LightCurtainInterlockInstalled
                        && EnableAutoRecovery == rhs.EnableAutoRecovery
                        && ClearESInTransferAbortedByInterlockState == rhs.ClearESInTransferAbortedByInterlockState
                        && HoldPassiveOutputsDuringFault == rhs.HoldPassiveOutputsDuringFault
                        && PIO2InterfacePresent == rhs.PIO2InterfacePresent
                        && Times.Equals(rhs.Times)
                        && PIOFailureAlarmName == rhs.PIOFailureAlarmName
                        && LCIlockClearsHO == rhs.LCIlockClearsHO
                        && LCIlockClearsES == rhs.LCIlockClearsES
                        && EnableUnexpectedPodPlacementFault == rhs.EnableUnexpectedPodPlacementFault
                        && PermitAutoRecoveryInTransferAbortedByInterlockState == rhs.PermitAutoRecoveryInTransferAbortedByInterlockState
                        && PermitAutoRecoveryInTP1 == rhs.PermitAutoRecoveryInTP1
                        && PermitAutoRecoveryInTP2 == rhs.PermitAutoRecoveryInTP2
                        && PermitAutoRecoveryInTP3 == rhs.PermitAutoRecoveryInTP3
                        && PermitAutoRecoveryInTP4 == rhs.PermitAutoRecoveryInTP4
                        && PermitAutoRecoveryInTP5 == rhs.PermitAutoRecoveryInTP5
                        );
            }

            /// <summary>
            /// Override for GetHashCode so as to prevent warning due to overriden Equal method.
            /// </summary>
            /// <returns>base.GetHashCode();</returns>
            public override int GetHashCode() { return base.GetHashCode(); }
        }

        #endregion

        ///Todo:  remove the leading m
        private Config mConfig;
        private ConfigValueSetAdapter<Config> configAdapter;
        private ConfigValueSetAdapter<ConfigTimes> configTimesAdapter;
        private INotifyable notifyOnConfigChange = null;

        private void SetupConfig(Config initialConfig, INotifyable notifyOnConfigChange)
        {
            mConfig = new Config(initialConfig);

            configAdapter = new ConfigValueSetAdapter<Config>() { ValueSet = mConfig, SetupIssueEmitter = Log.Debug, UpdateIssueEmitter = Log.Debug, ValueNoteEmitter = Log.Trace }.Setup("{0}.".CheckedFormat(PartID));
            configTimesAdapter = new ConfigValueSetAdapter<ConfigTimes>() { ValueSet = mConfig.Times, SetupIssueEmitter = Log.Debug, UpdateIssueEmitter = Log.Debug, ValueNoteEmitter = Log.Trace }.Setup("{0}.".CheckedFormat(PartID));

            if (notifyOnConfigChange != null)
            {
                configAdapter.UpdateNotificationList.AddItem(notifyOnConfigChange);
                configTimesAdapter.UpdateNotificationList.AddItem(notifyOnConfigChange);

                this.notifyOnConfigChange = notifyOnConfigChange;
            }
        }

        private void ReleaseConfig()
        {
            if (notifyOnConfigChange != null)
            {
                notifyOnConfigChange = null;

                configAdapter.UpdateNotificationList.RemoveItem(notifyOnConfigChange);
                configTimesAdapter.UpdateNotificationList.RemoveItem(notifyOnConfigChange);
            }
        }

        private bool ServiceConfig()
        {
            if (configAdapter.IsUpdateNeeded || configTimesAdapter.IsUpdateNeeded)
            {
                configAdapter.Update();
                configTimesAdapter.Update();
                return true;
            }

            return false;
        }

        #endregion

        #region Alarm(s)

        private IANManagerPart anManager;

        private IANSource pioFailureANSource;       // This is a hybrid alarm - It only supports an Ack action, however this action is interlocked with the stateCode and the 

        #endregion

        #region Service methods

        [Flags]
        private enum EventsToProcess
        {
            None = 0x0000,
            InitializeStarted = 0x0001,
            InitializeSucceeded = 0x0002,
            InitializeFailed = 0x0004,
        }

        /// <summary>
        /// Updates the StateCode, StateCodeReason and OutputSetpoint for the given E84State object
        /// </summary>
        public void UpdateE84State(LPMState lpmState, bool updateOutputSetpoints = true)
        {
            lpmState.E84State.StateCode = mPriv.stateCode;
            lpmState.E84State.StateCodeReason = mPriv.stateCodeReason;
            if (updateOutputSetpoints)
                lpmState.E84State.OutputSetpoint = new Semi.E084.PassiveToActivePinsState() { IFaceName = "E84sm", PackedWord = mPriv.pio1OutputPins };
        }

        /// <summary>
        /// Service state and update outputs
        /// </summary>
        public void Service(LPMState lpmState, bool updateOutputSetpoints = true)
        {
            ServiceConfig();

            EventsToProcess eventsToProcess;

            ServiceLPMState(lpmState, out eventsToProcess);

            if (eventsToProcess != EventsToProcess.None)
                HandleInternalEvents(eventsToProcess);

            ServiceStateAndUpdateOutputs();

            UpdateE84State(lpmState, updateOutputSetpoints);
        }

        private void ServiceStateAndUpdateOutputs()
        {
            StateCode preServiceState = mPriv.stateCode;

            /// Todo: confirm if this is a valid comment.
            ServiceState();		// ??? valid comment? cause state to transition to NotAvail_PortNotInService

            if (preServiceState == mPriv.stateCode)
                UpdateOutputs(mPriv.stateCode);
        }

        private void ServiceState()
        {
            QpcTimeStamp tsNow = QpcTimeStamp.Now;

            mPriv.timeInState = (tsNow - mPriv.stateCodeTime).TotalSeconds;
            mPriv.activePIOOutputPinsElapsedTime = (tsNow - mPriv.activePIOOutputPinsTimeStamp).TotalSeconds;

            if (mPriv.podSensorValues.PresentPlaced.DoesPlacedEqualPresent())		// keep advancing the last confirmed time for pod transition timeout detection during transfers
                mPriv.lastConfirmedPPStateTime = tsNow;

            // update outputs any time the lcInterlockTripped state changes
            bool lcInterlockTripped = IsLCInterlockTripped();

            if (lcInterlockTripped != mPriv.lastLCInterlockTripped)
            {
                mPriv.lastLCInterlockTripped = lcInterlockTripped;
                UpdateOutputs(mPriv.stateCode);
            }

            if (HandleFaultStatesAndConditions(tsNow))
                return;

            if (HandleFaultStatesAlarmRecovery(tsNow))
                return;

            if (ServiceInactiveStateChanges())
                return;

            if (ServiceSelectedAndTransferStateChanges())
                return;

            if (HandlePassiveTimeoutDetection())
                return;
        }

        /// <summary>
        /// Service new LPMState values by doing delta detect, accumulating the detected events that need to be processed
        /// </summary>
        private void ServiceLPMState(ILPMState lpmState, out EventsToProcess eventsToProcess)
        {
            eventsToProcess = EventsToProcess.None;

            bool amsChanged = (mPriv.portUsageContextInfo.AMS != lpmState.PortUsageContextInfo.AMS);
            bool ltsChanged = (mPriv.portUsageContextInfo.LTS != lpmState.PortUsageContextInfo.LTS);
            bool portContextInfoChanged = !mPriv.portUsageContextInfo.IsEqualTo(lpmState.PortUsageContextInfo);
            bool podLocChanged = (mPriv.lpmPositionState.IsUnclamped != lpmState.PositionState.IsUnclamped
                                    || mPriv.lpmPositionState.IsUndocked != lpmState.PositionState.IsUndocked);

            if (portContextInfoChanged)
            {
                // detect portContextInfo triggered events to process
                if (!mPriv.portUsageContextInfo.Initializing && lpmState.PortUsageContextInfo.Initializing)
                    eventsToProcess |= EventsToProcess.InitializeStarted;
                else if (mPriv.portUsageContextInfo.Initializing && !lpmState.PortUsageContextInfo.Initializing)
                    eventsToProcess |= (lpmState.PortUsageContextInfo.Error ? EventsToProcess.InitializeFailed : EventsToProcess.InitializeSucceeded);

                /// Todo: detect other trigger cases here:  E84LoadStart, E84Unload start, ...

                Log.Debug.Emit("Port Context Info changed to '{0}' [from:{1}]", lpmState.PortUsageContextInfo, mPriv.portUsageContextInfo);
                mPriv.portUsageContextInfo.SetFrom(lpmState.PortUsageContextInfo);
            }

            if (mPriv.portUsageContextInfo.APresentOrPlacementAlarmIsActive || mPriv.lastPresentOrPlacementAlarmIsActiveTime.IsZero)
                mPriv.lastPresentOrPlacementAlarmIsActiveTime.SetToNow();

            if (!mPriv.podSensorValues.IsEqualTo(lpmState.PodSensorValues))
            {
                Log.Debug.Emit("Pod Sensor Values changed to '{0}' [from:{1}]", lpmState.PodSensorValues, mPriv.podSensorValues);
                mPriv.podSensorValues.SetFrom(lpmState.PodSensorValues);
            }

            if (!mPriv.lpmPositionState.IsEqualTo(lpmState.PositionState))
            {
                Log.Trace.Emit("LPM Position State changed to '{0}' [from:{1}]", lpmState.PositionState, mPriv.lpmPositionState);
                mPriv.lpmPositionState.SetFrom(lpmState.PositionState);
            }

            if (ltsChanged)
            {
                Log.Info.Emit("PortTransferState is now '{0}'", mPriv.portUsageContextInfo.LTS);

                if (!mPriv.portUsageContextInfo.LTS.IsInService())
                {
                    if (mPriv.stateCode.IsSelected())
                        SetState(StateCode.Fault_TransferAborted, "Port was taken out of service while PIO Select is active.");
                    else if (mPriv.stateCode.IsLoading())
                        SetState(StateCode.Fault_TransferAborted, "Port was taken out of service during active Load transfer.");
                    else if (mPriv.stateCode.IsUnloading())
                        SetState(StateCode.Fault_TransferAborted, "Port was taken out of service during active Unload transfer.");
                    else
                        ServiceStateAndUpdateOutputs();// cause state to transition to NotAvail_PortNotInService
                }
            }

            if (amsChanged)
            {
                Log.Info.Emit("PortAccessMode is now '{0}'", mPriv.portUsageContextInfo.AMS);

                if (mPriv.portUsageContextInfo.AMS != Semi.E087.AMS.Automatic)
                {
                    if (mPriv.stateCode.IsLoading())
                        SetState(StateCode.Fault_TransferAborted, "Port set to manual mode during active Load transfer.");
                    else if (mPriv.stateCode.IsUnloading())
                        SetState(StateCode.Fault_TransferAborted, "Port set to manual mode during active Unload transfer.");
                    else
                        ServiceStateAndUpdateOutputs();		// this may cause the state to be changed to NotAvail_PortNotInAutoMode
                }
            }

            if (podLocChanged)
            {
                Log.Info.Emit("LPM pod location undocked:{0} unclamped:{1}", mPriv.lpmPositionState.IsUndocked.MapToInt(), mPriv.lpmPositionState.IsUnclamped.MapToInt());
            }

            {
                Semi.E084.IOSupport.PassiveIOState pio1State = new Semi.E084.IOSupport.PassiveIOState()
                {
                    outputs = new Semi.E084.PassiveToActivePinsState(lpmState.E84State.OutputSetpoint),
                    outputsReadback = new Semi.E084.PassiveToActivePinsState(lpmState.E84State.OutputReadback),
                    OutputIsPending = (!lpmState.E84State.OutputSetpointPinsMatchesReadback),
                    inputs = new Semi.E084.ActiveToPassivePinsState(lpmState.E84State.Inputs),
                    InputsAreValid = (lpmState.E84State.Inputs != null),
                };

                bool pio1StateChanged = !mPriv.lastPIO1State.IsEqualTo(pio1State);

                if (pio1StateChanged)
                {
                    mPriv.pio1InputState = pio1State.inputs;

                    if (!pio1State.OutputIsPending)
                    {
                        Log.Info.Emit("PIO1 PIO State is now inputs:'{0}' outputs:'{1}' [readbacks match]",
                                        pio1State.inputs,
                                        pio1State.outputs
                                        );
                    }
                    else
                    {
                        Log.Info.Emit("PIO1 PIO State is now inputs:'{0}' outputs:'{1}' [!= readbacks:'{2}']",
                                        pio1State.inputs,
                                        pio1State.outputs,
                                        pio1State.outputsReadback
                                        );

                    }
                }

                bool pio1InputPinsAreIdle = mPriv.pio1InputState.IsIdle;
                if (!pio1InputPinsAreIdle)
                    mPriv.pio1ActivePinsAreIdle = false;
                else if (mPriv.activePIO == PIOActiveSelect.PIO1 || DoesCurrentStatePermitPinsIdleReset())
                    mPriv.pio1ActivePinsAreIdle = true;

                mPriv.lastPIO1State = pio1State;
            }

            // PIO2 would use the same pattern as PIO1 if ILPMState supported 2 PIO interfaces - preserving form of C++ logic that supported dual PIO interfaces even though that code cannot currently be fully tested.
            {
                mPriv.pio2ActivePinsAreIdle = true;
            }
        }

        private void HandleInternalEvents(EventsToProcess eventsToProcess)
        {
            using (var eeTrace = new Logging.EnterExitTrace(Log, "{0}({1})".CheckedFormat(CurrentMethodName, eventsToProcess)))
            {
                if ((eventsToProcess & EventsToProcess.InitializeStarted) != EventsToProcess.None)
                {
                    Log.Info.Emit("Port Initialize Started");

                    // initialize is starting - update things
                    if (mPriv.stateCode.IsSelected())
                        SetState(StateCode.Fault_TransferAborted, "Port was taken out of service (init) while PIO Select is active.");
                    else if (mPriv.stateCode.IsLoading())
                        SetState(StateCode.Fault_TransferAborted, "Port was taken out of service (init) during active Load transfer.");
                    else if (mPriv.stateCode.IsUnloading())
                        SetState(StateCode.Fault_TransferAborted, "Port was taken out of service (init) during active Unload transfer.");
                    else
                        ServiceStateAndUpdateOutputs();// cause state to transition to NotAvail_PortIsInitializing
                }
                else if ((eventsToProcess & EventsToProcess.InitializeSucceeded) != EventsToProcess.None)
                {
                    Log.Info.Emit("Port Initialize Complete");

                    mPriv.portHasBeenInitialized = true;
                    mPriv.lastPortInitializeSucceeded = true;

                    // attempt to return to the correct inactive state., clear pioFailure alarm is we are no longer in a faulted state
                    string reason = "PDO Initialize is complete";

                    StateCode recoveredState = GetCurrentInactiveState(ref reason);

                    SetState(recoveredState, reason);		// this will clear the alarm

                    if (pioFailureANSource != null)
                    {
                        IANState pioFailureANState = pioFailureANSource.ANState;
                        if (!mPriv.stateCode.IsFaulted() && pioFailureANState.IsSignaling)
                        {
                            string mesg = "PDO Initialize complete";
                            Log.Info.Emit("PIOFailure alarm cleared after {0}, E84 state:{0}", mesg, mPriv.stateCode);
                            pioFailureANSource.Clear(mesg);
                        }
                    }
                }
                else if ((eventsToProcess & EventsToProcess.InitializeFailed) != EventsToProcess.None)
                {
                    Log.Info.Emit("Port Initialize Failed");

                    mPriv.lastPortInitializeSucceeded = false;

                    string reason = string.Empty;

                    StateCode recoveredState = GetCurrentInactiveState(ref reason);

                    SetState(recoveredState, reason);		// this will clear the alarm
                }
            }
        }

        #endregion

        #region private state

        struct Priv
        {
            public PortUsageContextInfo portUsageContextInfo;
            public PodSensorValues podSensorValues;
            public PositionState lpmPositionState;
            public bool portHasBeenInitialized;		// at least once 
            public bool lastPortInitializeSucceeded;	
            public PIOActiveSelect activePIO;
            public StateCode stateCode, pio1StateCode, pio2StateCode;
            public QpcTimeStamp stateCodeTime;
            public double timeInState;
            public string stateCodeReason;
            public Semi.E084.ActiveToPassivePinBits stateCodeContinuousBit;

            public QpcTimeStamp lastConfirmedPPStateTime;     // used for AMHS handoff transition time limit checking. (Removed to Placed, Placed to Removed)...
            public QpcTimeStamp lastPresentOrPlacementAlarmIsActiveTime;

            public bool lastLCInterlockTripped;

            public bool pio1ActivePinsAreIdle;	// and online, flag can only be cleared for non-selected interface except in Fault state
            public bool pio2ActivePinsAreIdle;	// and online, flag can only be cleared for non-selected interface except in Fault state

            public Semi.E084.ActiveToPassivePinsState pio1InputState;
            public Semi.E084.ActiveToPassivePinsState pio2InputState;
            ///Todo: rename to match case for properties (or remove)
            public Semi.E084.ActiveToPassivePinBits pio1InputPinBits { get { return pio1InputState.PackedWord; } }
            public Semi.E084.ActiveToPassivePinBits pio2InputPinBits { get { return pio2InputState.PackedWord; } }

            public Semi.E084.IOSupport.PassiveIOState lastPIO1State;
            public Semi.E084.IOSupport.PassiveIOState lastPIO2State;

            public QpcTimeStamp activePIOOutputPinsTimeStamp, lastOutputMatchTime;
            public double activePIOOutputPinsElapsedTime;
            public Semi.E084.PassiveToActivePinBits activePIOOutputPins;
            public Semi.E084.PassiveToActivePinBits pio1OutputPins;
            public Semi.E084.PassiveToActivePinBits pio2OutputPins;

            public bool pioFailureAcknowledgeAvailable;

            ///Todo: remove the following two items (once the code has been refactored)
            //public bool transferStartAck;
            //public bool transferCompleteAck;
        }

        ///Todo:  remove the leading m (et. al.)
        private Priv mPriv = new Priv()
        {
            portUsageContextInfo = new PortUsageContextInfo(),
            podSensorValues = new PodSensorValues(),
            lpmPositionState = new PositionState(),
            lastPIO1State = new Semi.E084.IOSupport.PassiveIOState(),
            lastPIO2State = new Semi.E084.IOSupport.PassiveIOState(),
        };

        #endregion

        #region Service helper methods: HandleFaultStatesAndConditions, HandleFaultStatesAlarmRecovery, ServiceInactiveStateChanges, ServiceSelectedAndTransferStateChanges, HandlePassiveTimeoutDetection

        //-------------------------------------------------------------------

        // each of the following service methods are intended to be used in turn
        //	until one of them returns true to indicate that it is handling the state
        //	or that it performed a state change.

        bool HandleFaultStatesAndConditions(QpcTimeStamp tsNow)
        {
            //--------------------------------------

            string reason = string.Empty;

            //--------------------------------------
            // we do not do any fault checking until the port has been initialized the first time.

            if (!mPriv.portHasBeenInitialized)
                return false;

            //--------------------------------------
            // determine assertIoNotInstalled and act on it immediately as appropriate:
            //	the conditions that cause this transition always take priority

            bool assertIoNotInstalled = (!mConfig.HwInstalled && mPriv.portUsageContextInfo.AMS == AMS.Automatic);

            if (assertIoNotInstalled)
            {
                SetState(StateCode.Fault_IONotInstalled, "Automatic mode not supported: E84 Hardware is not installed");

                return true;
            }

            //--------------------------------------
            // determine assertIoFailure and act on it immediately as appropriate:
            //	the conditions that cause transition to the Fault_IOFailure state always take priority

            double ioTimeoutPeriod = mConfig.Times.OutputReadbackMatchTimeout;
            bool ioTimeoutEnabled = (ioTimeoutPeriod != 0.0);

            bool pio1OutputMismatch = ioTimeoutEnabled
                                    && (mPriv.lastPIO1State.outputs.PackedWord != mPriv.lastPIO1State.outputsReadback.PackedWord);
            bool pio2OutputMismatch = ioTimeoutEnabled
                                    && (mPriv.lastPIO2State.outputs.PackedWord != mPriv.lastPIO2State.outputsReadback.PackedWord)
                                    && mConfig.PIO2InterfacePresent;

            // reset the last output match timestamp every time both outputs match or throughout PDO initialize periods
            if (!pio1OutputMismatch && !pio2OutputMismatch || mPriv.portUsageContextInfo.Initializing)
                mPriv.lastOutputMatchTime = tsNow;

            double outputMismatchElapsedTime = (tsNow - mPriv.lastOutputMatchTime).TotalSeconds;

            bool mismatchExceedsTimeLimit = (pio1OutputMismatch || pio2OutputMismatch)
                                            && mPriv.timeInState >= ioTimeoutPeriod
                                            && mPriv.activePIOOutputPinsElapsedTime >= ioTimeoutPeriod
                                            && outputMismatchElapsedTime >= ioTimeoutPeriod
                                            ;

            bool assertIoFailure = (pio1OutputMismatch || pio2OutputMismatch)
                                    && (mismatchExceedsTimeLimit || mPriv.stateCode == StateCode.Fault_IOFailure)		// once in the Fault_IOFailure state, ignore the timers until the root cause is resolved.
                                    && mPriv.portUsageContextInfo.LTS.IsInService()
                                    && !mPriv.portUsageContextInfo.Initializing							// output mismatch is ignored during initialize periods.
                                    ;

            if (assertIoFailure)
            {
                // only produce a reason if we are not already in the required state
                if (mPriv.stateCode != StateCode.Fault_IOFailure || mPriv.pioFailureAcknowledgeAvailable)
                {
                    if (pio1OutputMismatch)
                        reason = "PIO1 Output Failure (sp:{0} != rb:{1})".CheckedFormat(mPriv.lastPIO1State.outputs, mPriv.lastPIO1State.outputsReadback);
                    else if (pio2OutputMismatch)
                        reason = "PIO2 Output Failure (sp:{0} != rb:{1})".CheckedFormat(mPriv.lastPIO2State.outputs, mPriv.lastPIO2State.outputsReadback);
                    else
                        reason = "Unknown IOFailure condition detected";

                    SetState(StateCode.Fault_IOFailure, reason);
                }

                return true;
            }

            //--------------------------------------
            // check for and handle light curtain interlock conditions while either interface is Selected
            bool pio1LightCurtainInterlockTripped = (mConfig.LightCurtainInterlockInstalled && ((mPriv.pio1InputPinBits & LC_ILOCK) == 0));
            bool pio2LightCurtainInterlockTripped = (mConfig.LightCurtainInterlockInstalled && mConfig.PIO2InterfacePresent && ((mPriv.pio2InputPinBits & LC_ILOCK) == 0));

            if (mPriv.stateCode.IsSelected() || mPriv.stateCode.IsLoading() || mPriv.stateCode.IsUnloading())
            {
                if (pio1LightCurtainInterlockTripped)
                {
                    SetState(StateCode.Fault_TransferAbortedByInterlock, "PIO1 Light Curtain Interlock Tripped while transfer selected or in progress");
                    return true;
                }
                else if (pio2LightCurtainInterlockTripped)
                {
                    SetState(StateCode.Fault_TransferAbortedByInterlock, "PIO2 Light Curtain Interlock Tripped while transfer selected or in progress");
                    return true;
                }
            }

            //--------------------------------------
            // determin maintainInvalidSelect

            bool pio1ValidActive = ((mPriv.pio1InputPinBits & VALID) != 0);
            bool pio1cs0Active = ((mPriv.pio1InputPinBits & CS_0) != 0);
            bool pio1cs1Active = ((mPriv.pio1InputPinBits & CS_1) != 0);

            bool pio2ValidActive = mConfig.PIO2InterfacePresent && ((mPriv.pio2InputPinBits & VALID) != 0);
            bool pio2cs0Active = mConfig.PIO2InterfacePresent && ((mPriv.pio2InputPinBits & CS_0) != 0);
            bool pio2cs1Active = mConfig.PIO2InterfacePresent && ((mPriv.pio2InputPinBits & CS_1) != 0);

            bool maintainInvalidSelect = (pio1ValidActive || pio1cs0Active || pio1cs1Active)		// only relevant when we are already in the corresponding fault state
                                        || (pio2ValidActive || pio2cs0Active || pio2cs1Active);

            //--------------------------------------
            // determin maintainInvalidActivePins

            bool maintainInvalidActivePins = (!mPriv.pio1ActivePinsAreIdle || !mPriv.pio2ActivePinsAreIdle);

            //--------------------------------------
            // determin maintainTransferAborted

            bool maintainTransferAborted = (!mPriv.pio1ActivePinsAreIdle 
                                            || !mPriv.pio2ActivePinsAreIdle 
                                            || mPriv.portUsageContextInfo.E84LoadInProgress
                                            || mPriv.portUsageContextInfo.E84UnloadInProgress
                                            || mPriv.portUsageContextInfo.Error
                                            );

            //--------------------------------------
            // determin maintainTimeoutState

            bool maintainTimeoutState = (!mPriv.pio1ActivePinsAreIdle 
                                            || !mPriv.pio2ActivePinsAreIdle
                                            );

            //--------------------------------------
            // use simple dispatch logic to maintain current fault state as long as the relevant
            //	maintenance conditions are still true.

            bool maintainCurrentState = false;

            switch (mPriv.stateCode)
            {
                case StateCode.Fault_InvalidSelect: maintainCurrentState = maintainInvalidSelect; break;
                case StateCode.Fault_InvalidActivePins: maintainCurrentState = maintainInvalidActivePins; break;
                case StateCode.Fault_TransferAborted: maintainCurrentState = maintainTransferAborted; break;
                case StateCode.Fault_TransferAbortedByInterlock: maintainCurrentState = maintainTransferAborted; break;
                case StateCode.Fault_TP1_Timeout:
                case StateCode.Fault_TP2_Timeout:
                case StateCode.Fault_TP3_Timeout:
                case StateCode.Fault_TP4_Timeout:
                case StateCode.Fault_TP5_Timeout:
                case StateCode.Fault_TP6_Timeout: maintainCurrentState = maintainTimeoutState; break;
                case StateCode.Fault_PodTransitionTimeout: maintainCurrentState = (maintainTimeoutState || !mPriv.podSensorValues.PresentPlaced.DoesPlacedEqualPresent()); break;
                case StateCode.Fault_UnexpectedPodPlacement: maintainCurrentState = (maintainTimeoutState || !mPriv.podSensorValues.PresentPlaced.DoesPlacedEqualPresent()); break;
                default: break;
            }

            // NOTE: we do not maintain the current state if the fault condition that caused it
            //	was ever lost.  In that case we depend on the assert rules to reassert a fault
            //	condition if needed.

            if (maintainCurrentState && !mPriv.pioFailureAcknowledgeAvailable)
            {
                return true;
            }

            //--------------------------------------
            // implement normal assert fault handlers.
            //	Note we do not check if the state is already in target state since we want to
            //	have additional SetState calls to note changes in pioFailureAcknowledgeAvailable.

            //--------------------------------------
            // determin and implement assertInvalidSelect

            bool pio1Selected = (pio1ValidActive && (pio1cs0Active || pio1cs1Active));
            bool pio2Selected = (pio2ValidActive && (pio2cs0Active || pio2cs1Active));

            bool pio1cs1Selected = (pio1Selected && pio1cs1Active);
            bool pio2cs1Selected = (pio2Selected && pio2cs1Active);

            bool pio1SelectedWhilePortNotAvailable = false;
            bool pio2SelectedWhilePortNotAvailable = false;
            bool pio1SelectedWhenPIO2WasAlreadyActive = false;
            bool pio2SelectedWhenPIO1WasAlreadyActive = false;

            if (mPriv.stateCode.IsNotAvailable())
            {
                pio1SelectedWhilePortNotAvailable = pio1Selected;
                pio2SelectedWhilePortNotAvailable = pio2Selected;
            }

            if (mPriv.activePIO == PIOActiveSelect.PIO1)
            {
                pio2SelectedWhenPIO1WasAlreadyActive = pio2Selected;
            }
            else if (mPriv.activePIO == PIOActiveSelect.PIO2)
            {
                pio1SelectedWhenPIO2WasAlreadyActive = pio1Selected;
            }

            bool assertInvalidSelect = maintainInvalidSelect
                                        && (pio1cs1Selected || pio1SelectedWhilePortNotAvailable || pio1SelectedWhenPIO2WasAlreadyActive
                                            || pio2cs1Selected || pio2SelectedWhilePortNotAvailable || pio2SelectedWhenPIO1WasAlreadyActive);

            if (assertInvalidSelect)
            {
                if (pio1cs1Selected)
                    reason = "PIO1 CS_1 cannot be selected";
                else if (pio1SelectedWhilePortNotAvailable)
                    reason = "PIO1 selected while port is not available";
                else if (pio1SelectedWhenPIO2WasAlreadyActive)
                    reason = "PIO1 selected when PIO2 was already active";
                else if (pio2cs1Selected)
                    reason = "PIO2 CS_1 cannot be selected";
                else if (pio2SelectedWhilePortNotAvailable)
                    reason = "PIO2 selected while port is not available";
                else if (pio2SelectedWhenPIO1WasAlreadyActive)
                    reason = "PIO2 selected when PIO1 was already active";
                else
                    reason = "Unknown InvalidSelect condition detected";

                SetState(StateCode.Fault_InvalidSelect, reason);

                return true;
            }

            //--------------------------------------
            // determin and implement invalid pod placement transition timeout

            double podPlacementTimeSinceLastConfirmed = (tsNow - mPriv.lastConfirmedPPStateTime).TotalSeconds;

            if (!mPriv.podSensorValues.PresentPlaced.DoesPlacedEqualPresent()
                && (mPriv.stateCode.IsLoading() || mPriv.stateCode.IsUnloading())
                && mConfig.Times.PodPlacementTransitionTimeout > 0
                && podPlacementTimeSinceLastConfirmed > mConfig.Times.PodPlacementTransitionTimeout
                && podPlacementTimeSinceLastConfirmed > 1.0
                )
            {
                // pod placement state is in transition (it is no longer confirmed)
                // and we are loading or unloading
                // and podPlacementTransitionTimeout is non-zero
                // and time since last podPlacementState being confirmed is larger than configured transition timeout

                reason = "PodPlacementTransitionTimeout: placement transition has used {0:f2} sec, limit:{1:f2} sec, state:'{2}'".CheckedFormat(
                            podPlacementTimeSinceLastConfirmed,
                            mConfig.Times.PodPlacementTransitionTimeout,
                            mPriv.podSensorValues.PresentPlaced);

                SetState(StateCode.Fault_PodTransitionTimeout, reason);

                return true;
            }

            //--------------------------------------
            // detect conditions for Fault_UnexpectedPodPlacement and transition there when appropriate

            if (mConfig.EnableUnexpectedPodPlacementFault)
            {
                // the following switch is an opt in for states that can detect and react to unexpected pod placement conditions
                switch (mPriv.stateCode)
                {
                    case StateCode.Selected:
                    case StateCode.SelectedAndLoadable:
                    case StateCode.RequestStartLoad:
                    case StateCode.ReadyToStartLoad:
                        if (!mPriv.podSensorValues.PresentPlaced.IsNeitherPresentNorPlaced() || mPriv.portUsageContextInfo.APresentOrPlacementAlarmIsActive)
                        {
                            reason = "Unexpected PodPlacement state '{0}'{1} detected".CheckedFormat(mPriv.podSensorValues.PresentPlaced, (mPriv.portUsageContextInfo.APresentOrPlacementAlarmIsActive ? " PPAlarm" : ""));
                            SetState(StateCode.Fault_UnexpectedPodPlacement, reason);

                            return true;
                        }
                        break;
                    case StateCode.SelectedAndUnloadable:
                    case StateCode.RequestStartUnload:
                    case StateCode.ReadyToStartUnload:
                        if (!mPriv.podSensorValues.PresentPlaced.IsProperlyPlaced() || mPriv.portUsageContextInfo.APresentOrPlacementAlarmIsActive)
                        {
                            reason = "Unexpected PodPlacement state '{0}'{1} detected".CheckedFormat(mPriv.podSensorValues.PresentPlaced, (mPriv.portUsageContextInfo.APresentOrPlacementAlarmIsActive ? " PPAlarm" : ""));
                            SetState(StateCode.Fault_UnexpectedPodPlacement, reason);

                            return true;
                        }
                        break;
                    default:
                        break;
                }
            }

            //--------------------------------------
            // determin and implement assertInvalidActivePins

            bool assertInvalidActivePins = false;

            if (mPriv.stateCode.IsFaulted())
            {
                // the only pin transitions that are tracked in fault states is to require that
                //	all active pins are idle.

                assertInvalidActivePins = maintainInvalidActivePins;

                if (assertInvalidActivePins)
                {
                    if (!mPriv.pio1ActivePinsAreIdle)
                        reason = "PIO1 Active pins are not Idle";
                    else if (!mPriv.pio2ActivePinsAreIdle)
                        reason = "PIO2 Active pins are not Idle";
                    else
                        reason = "Unknown InvalidActivePin condition detected";
                }
            }
            else
            {
                // For all non fault states we test and enforce that all active side pin state changes are considered valid
                //	for the current state.  This includes testing both PIO1 and PIO2 based on which is selected at any given time.

                Semi.E084.ActiveToPassivePinBits ignorePins = GetE084IgnoreActivePinsByState(mPriv.stateCode);
                Semi.E084.ActiveToPassivePinBits deltaPins = GetE084ActiveChangablePinsByState(mPriv.stateCode);
                Semi.E084.ActiveToPassivePinBits expectPins = GetE084ActiveExpectedPinsByState(mPriv.stateCode);
                Semi.E084.ActiveToPassivePinBits checkPins = ~(deltaPins | ignorePins);
                Semi.E084.ActiveToPassivePinBits matchValue = checkPins & expectPins;
                Semi.E084.ActiveToPassivePinBits checkPins2 = (CS_0 | CS_1);
                Semi.E084.ActiveToPassivePinBits matchValue2 = Semi.E084.ActiveToPassivePinBits.None;

                switch (mPriv.activePIO)
                {
                    case PIOActiveSelect.Neither:
                        if ((mPriv.pio1InputPinBits & checkPins) != matchValue)
                        {
                            assertInvalidActivePins = true;
                            reason = "UnexpectedPIO1ActivePinStateChange_NoSel";
                        }
                        else if (mConfig.PIO2InterfacePresent && (mPriv.pio2InputPinBits & checkPins) != matchValue)
                        {
                            assertInvalidActivePins = true;
                            reason = "UnexpectedPIO2ActivePinStateChange_NoSel";
                        }
                        break;
                    case PIOActiveSelect.PIO1:
                        if ((mPriv.pio1InputPinBits & checkPins) != matchValue)
                        {
                            assertInvalidActivePins = true;
                            reason = "UnexpectedPIO1ActivePinStateChange";
                        }
                        else if (mConfig.PIO2InterfacePresent && (mPriv.pio2InputPinBits & checkPins2) != matchValue2)
                        {
                            assertInvalidActivePins = true;
                            reason = "UnexpectedPIO2ActivePinStateChangeWhilePIO1InUse";
                        }
                        break;
                    case PIOActiveSelect.PIO2:
                        if ((mPriv.pio2InputPinBits & checkPins) != matchValue)
                        {
                            assertInvalidActivePins = true;
                            reason = "UnexpectedPIO2ActivePinStateChange";
                        }
                        else if ((mPriv.pio1InputPinBits & checkPins2) != matchValue2)
                        {
                            assertInvalidActivePins = true;
                            reason = "UnexpectedPIO1ActivePinStateChangeWhilePIO2InUse";
                        }
                        break;
                }
            }

            if (assertInvalidActivePins)
            {
                SetState(StateCode.Fault_InvalidActivePins, reason);

                return true;
            }

            //--------------------------------------
            // determine if transfer is, or should be, aborted because of mismatch between stateCode and portContextInfo E84LoadInProgress/E84UnloadInProgress

            bool mustBeLoading = false, mustBeUnloading = false, mayBeLoading = false, mayBeUnloading = false;

            switch (mPriv.stateCode)
            {
                case StateCode.RequestStartLoad: mayBeLoading = true; break;
                case StateCode.ReadyToStartLoad: mustBeLoading = true; break;
                case StateCode.Loading: mustBeLoading = true; break;
                case StateCode.LoadingAndPodPresent: mustBeLoading = true; break;
                case StateCode.LoadTransferDone: mustBeLoading = true; break;
                case StateCode.LoadTransferDoneWaitReleased: mustBeLoading = true; break;
                case StateCode.LoadCompleted: mayBeLoading = true; break;

                case StateCode.RequestStartUnload: mayBeUnloading = true; break;
                case StateCode.ReadyToStartUnload: mustBeUnloading = true; break;
                case StateCode.Unloading: mustBeUnloading = true; break;
                case StateCode.UnloadingAndPodRemoved: mustBeUnloading = true; break;
                case StateCode.UnloadTransferDone: mustBeUnloading = true; break;
                case StateCode.UnloadTransferDoneWaitReleased: mustBeUnloading = true; break;
                case StateCode.UnloadCompleted: mayBeUnloading = true; break;

                default: break;
            }

            if (mPriv.stateCode.IsFaulted() || mPriv.stateCode.IsNotAvailable())
            {
                // nothing to do here
            }
            else if (mPriv.portUsageContextInfo.E84LoadInProgress && !(mayBeLoading || mustBeLoading))
                SetState(StateCode.Fault_TransferAborted, "External E84LoadInProgress asserted unexpectedly");
            else if (!mPriv.portUsageContextInfo.E84LoadInProgress && mustBeLoading)
                SetState(StateCode.Fault_TransferAborted, "External E84LoadInProgress cleared unexpectedly");
            else if (mPriv.portUsageContextInfo.E84UnloadInProgress && !(mayBeUnloading || mustBeUnloading))
                SetState(StateCode.Fault_TransferAborted, "External E84UnloadInProgress asserted unexpectedly");
            else if (!mPriv.portUsageContextInfo.E84UnloadInProgress && mustBeUnloading)
                SetState(StateCode.Fault_TransferAborted, "External E84UnloadInProgress cleared unexpectedly");

            //--------------------------------------

            return false;
        }

        bool HandleFaultStatesAlarmRecovery(QpcTimeStamp tsNow)
        {
            //--------------------------------------

            string reason = string.Empty;

            //--------------------------------------
            // we do not do any fault checking until the port has been initialized the first time.

            if (!mPriv.portHasBeenInitialized)
                return false;

            //--------------------------------------
            // implement fault state alarm recovery logic
            //	NOTE: at this point there are no faults that need to be maintained or asserted

            if (mPriv.stateCode.IsFaulted())
            {
                bool blockAutoRecoveryInThisState = false;

                switch (mPriv.stateCode)
                {
                    case StateCode.Fault_TransferAbortedByInterlock: blockAutoRecoveryInThisState = !mConfig.PermitAutoRecoveryInTransferAbortedByInterlockState; break;
                    case StateCode.Fault_TP1_Timeout: blockAutoRecoveryInThisState = !mConfig.PermitAutoRecoveryInTP1; break;
                    case StateCode.Fault_TP2_Timeout: blockAutoRecoveryInThisState = !mConfig.PermitAutoRecoveryInTP2; break;
                    case StateCode.Fault_TP3_Timeout: blockAutoRecoveryInThisState = !mConfig.PermitAutoRecoveryInTP3; break;
                    case StateCode.Fault_TP4_Timeout: blockAutoRecoveryInThisState = !mConfig.PermitAutoRecoveryInTP4; break;
                    case StateCode.Fault_TP5_Timeout: blockAutoRecoveryInThisState = !mConfig.PermitAutoRecoveryInTP5; break;
                    default: break;
                }

                if (mConfig.EnableAutoRecovery && !blockAutoRecoveryInThisState)
                {
                    reason = "AutoRecovery Activated: Fault condition(s) have been resolved";

                    StateCode recoveredState = GetCurrentInactiveState(ref reason);

                    SetState(recoveredState, reason);
                }
                else if (!mPriv.pioFailureAcknowledgeAvailable)
                {
                    mPriv.pioFailureAcknowledgeAvailable = true;

                    SetState(mPriv.stateCode, "Recovery Available: Fault condition(s) have been resolved", false);
                }

                if (pioFailureANSource != null)
                {
                    IANState pioFailureANState = pioFailureANSource.ANState;
                    string selectedAction = ((pioFailureANState.ANSignalState == ANSignalState.OnAndWaiting) ? pioFailureANState.SelectedActionName : null) ?? string.Empty;

                    if (selectedAction == "Ack")
                    {
                        reason = "PIOFailureAlarm has been Acknowledged";

                        pioFailureANSource.NoteActionCompleted(reason);
                        ///Todo: confirm that SetState will clear the alarm (if needed)

                        StateCode recoveredState = GetCurrentInactiveState(ref reason);

                        SetState(recoveredState, reason);		// this will clear the alarm
                    }
                }

                if (mPriv.portUsageContextInfo.Initializing)
                {
                    reason = "PDO is initializing";

                    StateCode recoveredState = GetCurrentInactiveState(ref reason);

                    SetState(recoveredState, reason);		// this will clear the alarm for auto clear cases.

                    if (!mPriv.stateCode.IsFaulted() && pioFailureANSource != null && pioFailureANSource.ANState.IsSignaling)
                    {
                        Log.Info.Emit("PIOFailure alarm cleared during PDO Initialize complete, from E84 state:{0}", mPriv.stateCode);
                        pioFailureANSource.Clear(reason);
                    }
                }

                return true;
            }

            return false;
        }

        bool ServiceInactiveStateChanges()
        {
            // if state is not IsAvailable (Available or AvailableContinuous) 
            //	and state is not IsNotAvailable (NotAvail_...)
            //	and state is not Initial then leave the method without making any additional changes.

            if (!mPriv.stateCode.IsAvailable()
                && !mPriv.stateCode.IsNotAvailable()
                && mPriv.stateCode != StateCode.Initial)
            {
                return false;
            }

            // update the inactive state as needed to track external changes
            // and then detect and handle valid select operations from either PIO

            string reason = String.Empty;       // just take the reason from the current inactive state's reason.
            StateCode newInactiveState = GetCurrentInactiveState(ref reason);

            if (mPriv.stateCode != newInactiveState)
            {
                SetState(newInactiveState, reason);
                return true;
            }

            // detect and attempt to trigger a valid transition to the Selected state.
            //	NOTE that the SetState method further validates this transition while it is
            //	updating the activePIO state.

            Semi.E084.ActiveToPassivePinBits ignorePins = GetE084IgnoreActivePinsByState(StateCode.Selected);
            Semi.E084.ActiveToPassivePinBits nextStateActivePinBits = GetE084ActiveExpectedPinsByState(StateCode.Selected) & ~ignorePins;

            if ((mPriv.pio1InputPinBits & ~ignorePins) == nextStateActivePinBits)
            {
                SetState(StateCode.Selected, "PIO1StartingSelectHandshake");
                return false;	// allow caller service loop to advance to next item in service loop (allow dispatch to Selected sub state in one pass rather than 2
            }
            else if (mConfig.PIO2InterfacePresent && (mPriv.pio2InputPinBits & ~ignorePins) == (nextStateActivePinBits & ~ignorePins))
            {
                SetState(StateCode.Selected, "PIO2StartingSelectHandshake");
                return false;	// allow caller service loop to advance to next item in service loop (allow dispatch to Selected sub state in one pass rather than 2
            }

            // we are still in either the IsAvailable or IsNotAvailable (or Initial) state.  Since this method handles all of these states, indicate to caller than service loop is done.
            return true;
        }

        bool ServiceSelectedAndTransferStateChanges()
        {
            StateCode stateCode = StateCode.Initial;
            string reason = string.Empty;

            StateCode nextState1 = StateCode.Initial;			// setting this will cause an immediate state change
            StateCode nextExpectedState1 = StateCode.Initial;	// setting this will wait until the active pins are in the correct expected configuration and then will change.
            string transitionReason1 = string.Empty;

            StateCode nextExpectedState2 = StateCode.Initial;	// set this to allow for an alternate next expected state.
            string transitionReason2 = string.Empty;

            Semi.E084.ActiveToPassivePinBits activePinBitsNow = Semi.E084.ActiveToPassivePinBits.None;
            bool activePinBitsAreIdle = false;
            bool passiveOutputPinsMatchReabacks = false;

            switch (mPriv.activePIO)
            {
                default:
                case PIOActiveSelect.Neither:
                    break;
                case PIOActiveSelect.PIO1:
                    activePinBitsNow = mPriv.pio1InputPinBits;
                    activePinBitsAreIdle = mPriv.pio1ActivePinsAreIdle;
                    passiveOutputPinsMatchReabacks = (mPriv.pio1OutputPins == mPriv.lastPIO1State.outputsReadback.PackedWord);
                    break;
                case PIOActiveSelect.PIO2:
                    activePinBitsNow = mPriv.pio2InputPinBits;
                    activePinBitsAreIdle = mPriv.pio2ActivePinsAreIdle;
                    passiveOutputPinsMatchReabacks = (mPriv.pio2OutputPins == mPriv.lastPIO2State.outputsReadback.PackedWord);
                    break;
            }

            switch (mPriv.stateCode)
            {
                case StateCode.Selected:
                    stateCode = GetCurrentInactiveState(ref reason);
                    if (stateCode != StateCode.Available && stateCode != StateCode.AvailableContinuous)
                    {
                        nextState1 = StateCode.Fault_TransferAborted;
                        transitionReason1 = "SelectAborted:" + reason;
                    }
                    else if (mPriv.portUsageContextInfo.LTS == LTS.ReadyToLoad)
                    {
                        nextState1 = StateCode.SelectedAndLoadable;
                        transitionReason1 = "SelectedAndReadyToLoad";
                    }
                    else if (mPriv.portUsageContextInfo.LTS == LTS.ReadyToUnload)
                    {
                        nextState1 = StateCode.SelectedAndUnloadable;
                        transitionReason1 = "SelectedAndReadyToUnload";
                    }
                    else
                    {
                        SetState(StateCode.Fault_InvalidSelect, "InternalError:LTSnotRecognized");
                        return true;
                    }
                    break;
                case StateCode.SelectedAndLoadable:
                    nextExpectedState1 = StateCode.RequestStartLoad;
                    transitionReason1 = "L_REQ,TR_REQ";
                    nextExpectedState2 = StateCode.Available;					// will be replaced with CurrentInactiveState
                    transitionReason2 = "SelectedLoadAborted:-VALID,-CS_0";
                    break;
                case StateCode.SelectedAndUnloadable:
                    nextExpectedState1 = StateCode.RequestStartUnload;
                    transitionReason1 = "U_REQ,TR_REQ";
                    nextExpectedState2 = StateCode.Available;					// will be replaced with CurrentInactiveState
                    transitionReason2 = "SelectedUnloadAborted:-VALID,-CS_0";
                    break;

                case StateCode.RequestStartLoad:        // TR_REQ has been asserted with L_REQ on
                    if (mPriv.portUsageContextInfo.E84LoadInProgress)
                    {
                        // confirm that the load port is undocked and unclamped - it should not have been R2L without already being in this position.
                        if (mPriv.lpmPositionState.IsUndocked && mPriv.lpmPositionState.IsUnclamped)
                        {
                            nextState1 = StateCode.ReadyToStartLoad;
                            transitionReason1 = "TR_REQ,ReadyToLoad";
                        }
                        else
                        {
                            nextState1 = StateCode.Fault_TransferAborted;
                            transitionReason1 = "StartLoadAborted:LoadPortIsNotUndockedAndUnclamped";
                        }
                    }
                    break;
                case StateCode.ReadyToStartLoad:
                    nextExpectedState1 = StateCode.Loading;
                    transitionReason1 = "BUSY";
                    nextExpectedState2 = StateCode.Available;					// will be replaced with CurrentInactiveState
                    transitionReason2 = "LoadStartAborted:-VALID,-CS_0,-TR_REQ";
                    break;
                case StateCode.Loading:
                    if (mPriv.podSensorValues.PresentPlaced.IsProperlyPlaced())
                    {
                        nextState1 = StateCode.LoadingAndPodPresent;
                        transitionReason1 = "PodIsProperlyPlaced";
                    }
                    break;
                case StateCode.LoadingAndPodPresent:
                    nextExpectedState1 = StateCode.LoadTransferDone;
                    transitionReason1 = "COMPT,-TR_REQ,-BUSY";
                    break;
                case StateCode.LoadTransferDone:
                    nextState1 = StateCode.LoadTransferDoneWaitReleased;
                    transitionReason1 = "AcknowledgeCOMPT:-READY";
                    break;
                case StateCode.LoadTransferDoneWaitReleased:
                    nextExpectedState1 = StateCode.LoadCompleted;
                    transitionReason1 = "-COMPT,-VALID,-CS_0";
                    break;
                case StateCode.LoadCompleted:
                    if (activePinBitsAreIdle && !mPriv.portUsageContextInfo.E84LoadInProgress)
                    {
                        nextState1 = StateCode.Available;					// will be replaced with CurrentInactiveState
                        transitionReason1 = "LoadHandshakeCompletedAndAcknowledgedByPort";
                    }
                    break;

                case StateCode.RequestStartUnload:        // TR_REQ has been asserted with U_REQ on.  Wait for E84UnloadInProgress and undocked and unclamped
                    if (mPriv.portUsageContextInfo.E84UnloadInProgress && mPriv.lpmPositionState.IsUndocked && mPriv.lpmPositionState.IsUnclamped)
                    {
                        nextState1 = StateCode.ReadyToStartUnload;
                        transitionReason1 = "TR_REQ,ReadyToUnload";
                    }
                    break;
                case StateCode.ReadyToStartUnload:
                    nextExpectedState1 = StateCode.Unloading;
                    transitionReason1 = "BUSY";
                    nextExpectedState2 = StateCode.Available;					// will be replaced with CurrentInactiveState
                    transitionReason2 = "UnloadStartAborted:-VALID,-CS_0,-TR_REQ";
                    break;
                case StateCode.Unloading:
                    if (mPriv.podSensorValues.PresentPlaced.IsNeitherPresentNorPlaced())
                    {
                        nextState1 = StateCode.UnloadingAndPodRemoved;
                        transitionReason1 = "PodIsNeitherPresentNorPlaced";
                    }
                    break;
                case StateCode.UnloadingAndPodRemoved:
                    nextExpectedState1 = StateCode.UnloadTransferDone;
                    transitionReason1 = "COMPT,-TR_REQ,-BUSY";
                    break;
                case StateCode.UnloadTransferDone:
                    nextState1 = StateCode.UnloadTransferDoneWaitReleased;
                    transitionReason1 = "AcknowledgeCOMPT:-READY";
                    break;
                case StateCode.UnloadTransferDoneWaitReleased:
                    nextExpectedState1 = StateCode.UnloadCompleted;
                    transitionReason1 = "-COMPT,-VALID,-CS_0";
                    break;
                case StateCode.UnloadCompleted:
                    if (activePinBitsAreIdle && !mPriv.portUsageContextInfo.E84UnloadInProgress)
                    {
                        nextState1 = StateCode.Available;					// will be replaced with CurrentInactiveState
                        transitionReason1 = "UnloadHandshakeCompletedAndAcknowledgedByPort";
                    }
                    break;

                // this logic does not handle any other state transitions.
                default:
                    break;
            }

            if (nextState1 == StateCode.Available)
                nextState1 = GetCurrentInactiveState(ref transitionReason1);

            if (nextState1 != StateCode.Initial)
            {
                if (transitionReason1.IsNullOrEmpty())
                    transitionReason1 = "UnknownReason";
                SetState(nextState1, transitionReason1);
                return true;
            }

            // now check for and implement the common logic used to implement
            //	the pin dependant transitions to one of 2 next possible expected states.
            //	the the pins transition from the current states valid pins states to the
            //	correct ones for one of the nextExpectedStates and no other unexpected pins
            //	changed states then take the transition.

            if (mPriv.activePIO == PIOActiveSelect.Neither)
                return false;

            if (nextExpectedState1 == StateCode.Available)
                nextExpectedState1 = GetCurrentInactiveState(ref transitionReason1);

            Semi.E084.ActiveToPassivePinBits changableActivePinBits = GetE084ActiveChangablePinsByState(mPriv.stateCode);
            bool isCONTchangeable = ((changableActivePinBits & CONT) != 0);

            if (nextExpectedState1 != StateCode.Initial)
            {
                Semi.E084.ActiveToPassivePinBits nextStateActivePinBits = GetE084ActiveExpectedPinsByState(nextExpectedState1);
                Semi.E084.ActiveToPassivePinBits ignorePins = GetE084IgnoreActivePinsByState(nextExpectedState1);
                Semi.E084.ActiveToPassivePinBits testMask = ~((isCONTchangeable ? CONT : 0x00) | ignorePins);

                if ((activePinBitsNow & testMask) == (nextStateActivePinBits & testMask))
                {
                    if (transitionReason1.IsNullOrEmpty())
                        transitionReason1 = "StandardActiveStateTransition";
                    SetState(nextExpectedState1, transitionReason1);
                    return true;
                }
            }

            if (nextExpectedState2 == StateCode.Available)
                nextExpectedState2 = GetCurrentInactiveState(ref transitionReason2);

            if (nextExpectedState2 != StateCode.Initial)
            {
                Semi.E084.ActiveToPassivePinBits nextStateActivePinBits = GetE084ActiveExpectedPinsByState(nextExpectedState2);
                Semi.E084.ActiveToPassivePinBits ignorePins = GetE084IgnoreActivePinsByState(nextExpectedState2);
                Semi.E084.ActiveToPassivePinBits testMask = ~((isCONTchangeable ? CONT : 0x00) | ignorePins);

                if ((activePinBitsNow & testMask) == (nextStateActivePinBits & testMask))
                {
                    if (transitionReason2.IsNullOrEmpty())
                        transitionReason2 = "AlternateActiveStateTransition";
                    SetState(nextExpectedState2, transitionReason2);
                    return true;
                }
            }

            return false;
        }

        bool HandlePassiveTimeoutDetection()
        {
            string timeoutName = "[INVALID_TP]";
            double timeLimit = 0.0;
            StateCode timeoutTargetState = StateCode.Initial;

            switch (mPriv.stateCode)
            {
                // TP1 timeout states
                case StateCode.SelectedAndLoadable:
                case StateCode.SelectedAndUnloadable:
                    timeoutName = "TP1";
                    timeoutTargetState = StateCode.Fault_TP1_Timeout;
                    timeLimit = mConfig.Times.TP1;
                    break;

                //  T2 timeout states
                case StateCode.ReadyToStartLoad:
                case StateCode.ReadyToStartUnload:
                    timeoutName = "TP2";
                    timeoutTargetState = StateCode.Fault_TP2_Timeout;
                    timeLimit = mConfig.Times.TP2;
                    break;

                // TP3 timeout states
                case StateCode.Loading:
                case StateCode.Unloading:
                    timeoutName = "TP3";
                    timeoutTargetState = StateCode.Fault_TP3_Timeout;
                    timeLimit = mConfig.Times.TP3;
                    break;

                // TP4 timeout states
                case StateCode.LoadingAndPodPresent:
                case StateCode.UnloadingAndPodRemoved:
                    timeoutName = "TP4";
                    timeoutTargetState = StateCode.Fault_TP4_Timeout;
                    timeLimit = mConfig.Times.TP4;
                    break;

                // TP5 timeout states
                case StateCode.LoadTransferDoneWaitReleased:
                case StateCode.UnloadTransferDoneWaitReleased:
                    timeoutName = "TP5";
                    timeoutTargetState = StateCode.Fault_TP5_Timeout;
                    timeLimit = mConfig.Times.TP5;
                    break;

                // TP6 timeout states
                case StateCode.AvailableContinuous:
                    timeoutName = "TP6";
                    timeoutTargetState = StateCode.Fault_TP6_Timeout;
                    timeLimit = mConfig.Times.TP6;
                    break;

                default:
                    return false;
            }

            if (mPriv.timeInState > timeLimit)
            {
                SetState(timeoutTargetState, "{0} Timeout detected (timer={1:f2})".CheckedFormat(timeoutName, timeLimit));
                return true;
            }

            return false;
        }

        #endregion

        #region SetState (variants), UpdateOutputs

        void SetState(StateCode newState, string reason)
        {
            SetState(newState, reason, true);
        }

        void SetState(StateCode newStateCode, string reason, bool autoClearPIOFailureRecovery)
        {
            // detect and handle certain states as special cases.  These cases are the
            //	states that cause changes in the activePIO selection indication.

            if (newStateCode.IsNotAvailable())
                mPriv.activePIO = PIOActiveSelect.Neither;
            else if (newStateCode.IsAvailable(false))
                mPriv.activePIO = PIOActiveSelect.Neither;
            else if (newStateCode == StateCode.Selected)
            {
                if (mPriv.pio2ActivePinsAreIdle && !mPriv.pio1ActivePinsAreIdle)
                    mPriv.activePIO = PIOActiveSelect.PIO1;
                else if (mConfig.PIO2InterfacePresent && !mPriv.pio2ActivePinsAreIdle && mPriv.pio1ActivePinsAreIdle)
                    mPriv.activePIO = PIOActiveSelect.PIO2;
                else
                {
                    mPriv.activePIO = PIOActiveSelect.Neither;
                    newStateCode = StateCode.Fault_InvalidSelect;
                    reason = "{0}:Select pio determination failed".CheckedFormat(reason);
                }
            }

            UpdateOutputs(newStateCode);

            if (mPriv.stateCode != newStateCode || (!reason.IsNullOrEmpty() && mPriv.stateCodeReason != reason))
            {
                StateCode prevState = mPriv.stateCode;

                mPriv.stateCode = newStateCode;
                mPriv.stateCodeTime = QpcTimeStamp.Now;
                mPriv.stateCodeReason = reason;
                mPriv.stateCodeContinuousBit = GetContinuousBit();

                Logging.IMesgEmitter emitter = newStateCode.IsFaulted() ? Log.Warning : Log.Info;

                emitter.Emit("State changed to '{0}' reason:'{1}' [from:{2}]", newStateCode, reason, prevState);
            }

            // check if the alarm needs to be posted or cleared (based on the target StateCode)
            bool pioIsFaulted = mPriv.stateCode.IsFaulted();

            if (autoClearPIOFailureRecovery)
                mPriv.pioFailureAcknowledgeAvailable = false;		// this may only be set while remaining in a single state.

            if (pioFailureANSource != null)
            {
                if (pioIsFaulted)
                    pioFailureANSource.Post(new NamedValueSet() { { "Ack", mPriv.pioFailureAcknowledgeAvailable } }, reason);
                else
                    pioFailureANSource.Clear(reason);
            }
        }

        void UpdateOutputs(StateCode state)
        {
            // now determine what the state specific output bits should be for the 
            //	active PIO and then apply them to the two PIO outputs appropriately.

            Semi.E084.PassiveToActivePinBits statePinBits = GetE084PassiveOutputPackedWordByState(state);

            if (mPriv.activePIOOutputPins != statePinBits)
            {
                mPriv.activePIOOutputPins = statePinBits;
                mPriv.activePIOOutputPinsTimeStamp = QpcTimeStamp.Now;
                mPriv.activePIOOutputPinsElapsedTime = 0.0;
            }

            switch (mPriv.activePIO)
            {
                case PIOActiveSelect.Neither:
                    mPriv.pio2OutputPins = mPriv.activePIOOutputPins;
                    mPriv.pio1OutputPins = mPriv.activePIOOutputPins;
                    mPriv.pio2StateCode = state;
                    mPriv.pio1StateCode = state;
                    break;

                case PIOActiveSelect.PIO1:
                    mPriv.pio1OutputPins = mPriv.activePIOOutputPins;
                    mPriv.pio1StateCode = state;

                    mPriv.pio2StateCode = (mPriv.pio2ActivePinsAreIdle ? StateCode.NotAvail_TransferBlocked : StateCode.Fault_InvalidActivePins);
                    mPriv.pio2OutputPins = GetE084PassiveOutputPackedWordByState(mPriv.pio2StateCode);
                    break;

                case PIOActiveSelect.PIO2:
                    mPriv.pio2OutputPins = mPriv.activePIOOutputPins;
                    mPriv.pio2StateCode = state;

                    mPriv.pio1StateCode = (mPriv.pio1ActivePinsAreIdle ? StateCode.NotAvail_TransferBlocked : StateCode.Fault_InvalidActivePins);
                    mPriv.pio1OutputPins = GetE084PassiveOutputPackedWordByState(mPriv.pio1StateCode);
                    break;
            }
        }

        #endregion

        #region GetContinuousBit, DoesCurrentStatePermitPinsIdleReset, GetCurrentInactiveState, IsLCInterlockTripped

        Semi.E084.ActiveToPassivePinBits GetContinuousBit()
        {
            Semi.E084.ActiveToPassivePinBits continuousBit = Semi.E084.ActiveToPassivePinBits.None;

            switch (mPriv.activePIO)
            {
                case PIOActiveSelect.Neither:
                    break;

                case PIOActiveSelect.PIO1:
                    continuousBit = (mPriv.pio1InputPinBits & CONT);
                    break;

                case PIOActiveSelect.PIO2:
                    continuousBit = (mPriv.pio2InputPinBits & CONT);
                    break;
            }

            return continuousBit;
        }

        bool DoesCurrentStatePermitPinsIdleReset()
        {
            return mPriv.stateCode.IsFaulted();
        }

        StateCode GetCurrentInactiveState(ref string reason)
        {
            StateCode idleStateCode = ((mPriv.stateCodeContinuousBit != 0) ? StateCode.AvailableContinuous : StateCode.Available);

            StateCode inactiveState = StateCode.Initial;
            string stateReason = string.Empty;

            if (mPriv.portUsageContextInfo.Initializing)
            {
                inactiveState = StateCode.NotAvail_PortIsInitializing;
                stateReason = "Port is being initialized";
            }
            else if (!mPriv.portHasBeenInitialized)
            {
                inactiveState = StateCode.NotAvail_PortNotInit;
                stateReason = "Port has not been initialized";
            }
            else if (!mPriv.lastPortInitializeSucceeded)
            {
                inactiveState = StateCode.NotAvail_PortNotInit;
                stateReason = "Last port initialize failed";
            }
            else if (mPriv.portUsageContextInfo.Error)
            {
                inactiveState = StateCode.NotAvail_PortNotInit;
                stateReason = "Port must be initialized (Context.Error)";
            }
            else if (mPriv.portUsageContextInfo.Alarm)
            {
                inactiveState = StateCode.NotAvail_PortNotInit;
                stateReason = "Port must be initialized (Context.Alarm)";
            }
            else if (!mPriv.lpmPositionState.IsReferenced)
            {
                inactiveState = StateCode.NotAvail_PortNotInit;
                stateReason = "Port must be initialized (NotReferenced)";
            }
            else if (!mPriv.lpmPositionState.IsServoOn)
            {
                inactiveState = StateCode.NotAvail_PortNotInit;
                stateReason = "Port must be initialized (ServoIsNotOn)";
            }
            else if (!mConfig.HwInstalled)
            {
                inactiveState = StateCode.NotAvail_HardwareNotInstalled;
                stateReason = "E84 hardware is not installed";
            }
            else if (!mPriv.portUsageContextInfo.LTS.IsInService())
            {
                inactiveState = StateCode.NotAvail_PortNotInService;
                stateReason = "Port is not In Service";
            }
            else if (mPriv.portUsageContextInfo.AMS != AMS.Automatic)
            {
                inactiveState = StateCode.NotAvail_PortNotInAutoMode;
                stateReason = "Port mode is not Automatic";
            }
            else if (IsLCInterlockTripped())
            {
                bool pio1LightCurtainInterlockTripped = ((mPriv.pio1InputPinBits & LC_ILOCK) == 0);
                bool pio2LightCurtainInterlockTripped = mConfig.PIO2InterfacePresent && ((mPriv.pio2InputPinBits & LC_ILOCK) == 0);

                inactiveState = StateCode.NotAvail_LightCurtainIlockTripped;
                stateReason = "Light Curtain Interlock is tripped";
                if (pio1LightCurtainInterlockTripped && !pio2LightCurtainInterlockTripped)
                    stateReason += " (PIO1)";
                else if (!pio1LightCurtainInterlockTripped && pio2LightCurtainInterlockTripped)
                    stateReason = " (PIO2)";
                else if (pio1LightCurtainInterlockTripped && pio2LightCurtainInterlockTripped)
                    stateReason = " (PIO1,2)";
            }
            else if (mPriv.portUsageContextInfo.LTS == LTS.TransferBlocked)
            {
                inactiveState = StateCode.NotAvail_TransferBlocked;
                stateReason = "Port transfer state is Blocked";
            }
            else if (mPriv.portUsageContextInfo.APresentOrPlacementAlarmIsActive)
            {
                inactiveState = StateCode.NotAvail_PodPlacementFault;
                stateReason = "Pod Placement Handler Alarm is active";
            }
            else if (mPriv.stateCode == StateCode.NotAvail_PodPlacementFault)
            {
                double timeInPPState = (QpcTimeStamp.Now - mPriv.lastPresentOrPlacementAlarmIsActiveTime).TotalSeconds;

                if (mPriv.podSensorValues.PresentPlaced.DoesPlacedEqualPresent() && timeInPPState < mConfig.Times.PostPPFaultHoldoff)
                {
                    inactiveState = StateCode.NotAvail_PodPlacementFault;
                    stateReason = "In Post Pod Placement Alarm holdoff period";
                }
            }

            if (inactiveState == StateCode.Initial)
            {
                inactiveState = idleStateCode;

                if (mPriv.stateCode != inactiveState)
                {
                    if (mPriv.stateCode.IsAvailable())
                        stateReason = "CONT signal changed";
                    else
                        stateReason = "Port is available to be selected";
                }
            }

            if (!stateReason.IsNullOrEmpty())
            {
                if (reason.IsNullOrEmpty())
                    reason = stateReason;
                else
                    reason = reason + " : " + stateReason;
            }

            return inactiveState;
        }

        bool IsLCInterlockTripped()
        {
            bool pio1LightCurtainInterlockTripped = ((mPriv.pio1InputPinBits & LC_ILOCK) == 0);
            bool pio2LightCurtainInterlockTripped = mConfig.PIO2InterfacePresent && ((mPriv.pio2InputPinBits & LC_ILOCK) == 0);

            bool LCInterlockTripped = (mConfig.LightCurtainInterlockInstalled && (pio1LightCurtainInterlockTripped || pio2LightCurtainInterlockTripped));

            return LCInterlockTripped;
        }

        #endregion

        #region pin Constants for local use: ES, HO_AVBL, L_REQ, U_REQ, READY, VALID, CS_0, CS_1, TR_REQ, CONT, BUSY, COMPT, LC_ILOCK

        const Semi.E084.PassiveToActivePinBits L_REQ = Semi.E084.PassiveToActivePinBits.L_REQ_pin1;
        const Semi.E084.PassiveToActivePinBits U_REQ = Semi.E084.PassiveToActivePinBits.U_REQ_pin2;
        const Semi.E084.PassiveToActivePinBits READY = Semi.E084.PassiveToActivePinBits.READY_pin4;
        const Semi.E084.PassiveToActivePinBits HO_AVBL = Semi.E084.PassiveToActivePinBits.HO_AVBL_pin7;
        const Semi.E084.PassiveToActivePinBits ES = Semi.E084.PassiveToActivePinBits.ES_pin8;

        const Semi.E084.ActiveToPassivePinBits VALID = Semi.E084.ActiveToPassivePinBits.VALID_pin14;
        const Semi.E084.ActiveToPassivePinBits CS_0 = Semi.E084.ActiveToPassivePinBits.CS_0_pin15;
        const Semi.E084.ActiveToPassivePinBits CS_1 = Semi.E084.ActiveToPassivePinBits.CS_1_pin16;
        const Semi.E084.ActiveToPassivePinBits TR_REQ = Semi.E084.ActiveToPassivePinBits.TR_REQ_pin18;
        const Semi.E084.ActiveToPassivePinBits BUSY = Semi.E084.ActiveToPassivePinBits.BUSY_pin19;
        const Semi.E084.ActiveToPassivePinBits COMPT = Semi.E084.ActiveToPassivePinBits.COMPT_pin20;
        const Semi.E084.ActiveToPassivePinBits CONT = Semi.E084.ActiveToPassivePinBits.CONT_pin21;
        const Semi.E084.ActiveToPassivePinBits LC_ILOCK = Semi.E084.ActiveToPassivePinBits.XferILock_sig;       // this is a pseuod pin...

        #endregion

        #region GetE084PassiveOutputPackedWordByState, GetE084ActiveChangablePinsByState, GetE084ActiveExpectedPinsByState, GetE084IgnoreActivePinsByState

        Semi.E084.PassiveToActivePinBits GetE084PassiveOutputPackedWordByState(StateCode stateCode)
        {
            const Semi.E084.PassiveToActivePinBits noPins = Semi.E084.PassiveToActivePinBits.None;
            Semi.E084.PassiveToActivePinBits pinBits = Semi.E084.PassiveToActivePinBits.None;

            Semi.E084.PassiveToActivePinBits esByHwInstalled = (mConfig.HwInstalled ? ES : noPins);
            Semi.E084.PassiveToActivePinBits faultStatePinsBaseBits = (mConfig.HoldPassiveOutputsDuringFault ? mPriv.activePIOOutputPins : noPins)
                                                                        & ~HO_AVBL
                                                                        & ~ES;

            // if port is initializing then we do not hold the outputs
            if (mPriv.portUsageContextInfo.Initializing)
                faultStatePinsBaseBits = noPins;

            // determine if there are any bits that we must clear due to light curtain interlock
            Semi.E084.PassiveToActivePinBits clearPinBits = noPins;

            if (IsLCInterlockTripped())
            {
                if (mConfig.LCIlockClearsHO)
                    clearPinBits = clearPinBits | HO_AVBL;
                if (mConfig.LCIlockClearsES)
                    clearPinBits = clearPinBits | ES;
            }

            // true for clearESInTransferAbortedByInterlockState selects noPins being active.  false for clearESInTransferAbortedByInterlockState selects ES being active.
            Semi.E084.PassiveToActivePinBits xferAbortedByILockES = (mConfig.ClearESInTransferAbortedByInterlockState ? noPins : ES);

            // convert any given StateCode value into the corresponding output pattern that
            //	should be output on the selected PIO pins while in that state.
            //	NOTE: because half of the states are used to record output transitions and half to wait for input transitions
            //	you may observe that many of the states duplicate the output pin state value for an adjacent state.

            switch (stateCode)
            {
                case StateCode.Initial: pinBits = noPins; break;

                case StateCode.NotAvail_PortNotInit: pinBits = esByHwInstalled; break;
                case StateCode.NotAvail_PortIsInitializing: pinBits = esByHwInstalled; break;
                case StateCode.NotAvail_HardwareNotInstalled: pinBits = esByHwInstalled; break;
                case StateCode.NotAvail_PortNotInService: pinBits = esByHwInstalled; break;
                case StateCode.NotAvail_PortNotInAutoMode: pinBits = esByHwInstalled; break;
                case StateCode.NotAvail_TransferBlocked: pinBits = esByHwInstalled; break;

                case StateCode.NotAvail_PodPlacementFault: pinBits = esByHwInstalled | faultStatePinsBaseBits; break;
                case StateCode.NotAvail_LightCurtainIlockTripped: pinBits = esByHwInstalled | faultStatePinsBaseBits; break;

                case StateCode.Fault_IONotInstalled: pinBits = noPins; break;								// do not assert ES when there is no E84 hardware installed.
                case StateCode.Fault_IOFailure: pinBits = noPins | faultStatePinsBaseBits; break;		// drop ES on IO Failure.
                case StateCode.Fault_InvalidSelect: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_InvalidActivePins: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_TransferAborted: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_TransferAbortedByInterlock: pinBits = xferAbortedByILockES | faultStatePinsBaseBits; break;		// state of ES depends on configuration.
                case StateCode.Fault_TP1_Timeout: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_TP2_Timeout: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_TP3_Timeout: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_TP4_Timeout: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_TP5_Timeout: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_TP6_Timeout: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_PodTransitionTimeout: pinBits = ES | faultStatePinsBaseBits; break;
                case StateCode.Fault_UnexpectedPodPlacement: pinBits = ES | faultStatePinsBaseBits; break;

                case StateCode.Available: pinBits = ES | HO_AVBL; break;
                case StateCode.AvailableContinuous: pinBits = ES | HO_AVBL; break;
                case StateCode.Selected: pinBits = ES | HO_AVBL; break;

                case StateCode.SelectedAndLoadable: pinBits = ES | HO_AVBL | L_REQ; break;
                case StateCode.RequestStartLoad: pinBits = ES | HO_AVBL | L_REQ; break;
                case StateCode.ReadyToStartLoad: pinBits = ES | HO_AVBL | L_REQ | READY; break;
                case StateCode.Loading: pinBits = ES | HO_AVBL | L_REQ | READY; break;
                case StateCode.LoadingAndPodPresent: pinBits = ES | HO_AVBL | READY; break;
                case StateCode.LoadTransferDone: pinBits = ES | HO_AVBL | READY; break;
                case StateCode.LoadTransferDoneWaitReleased: pinBits = ES | HO_AVBL; break;
                case StateCode.LoadCompleted: pinBits = ES; break;     // drop HO_AVBL immediately after interface has been released.  It will be re-asserted again if appropriate on transition back to idle

                case StateCode.SelectedAndUnloadable: pinBits = ES | HO_AVBL | U_REQ; break;
                case StateCode.RequestStartUnload: pinBits = ES | HO_AVBL | U_REQ; break;
                case StateCode.ReadyToStartUnload: pinBits = ES | HO_AVBL | U_REQ | READY; break;
                case StateCode.Unloading: pinBits = ES | HO_AVBL | U_REQ | READY; break;
                case StateCode.UnloadingAndPodRemoved: pinBits = ES | HO_AVBL | READY; break;
                case StateCode.UnloadTransferDone: pinBits = ES | HO_AVBL | READY; break;
                case StateCode.UnloadTransferDoneWaitReleased: pinBits = ES | HO_AVBL; break;
                case StateCode.UnloadCompleted: pinBits = ES | HO_AVBL; break;     // drop HO_AVBL immediately after interface has been released.  It will be re-asserted again if appropriate on transition back to idle

                default: pinBits = noPins; break;
            }

            if (clearPinBits != 0)
            {
                pinBits = pinBits & ~clearPinBits;
            }

            return pinBits;
        }

        Semi.E084.ActiveToPassivePinBits GetE084ActiveChangablePinsByState(StateCode stateCode)
        {
            Semi.E084.ActiveToPassivePinBits deltaPins = Semi.E084.ActiveToPassivePinBits.None;
            const Semi.E084.ActiveToPassivePinBits allPins = (Semi.E084.ActiveToPassivePinBits)0xffff;
            const Semi.E084.ActiveToPassivePinBits noPins = Semi.E084.ActiveToPassivePinBits.None;

            const bool permitDeselectAfterSelect = false;		// set this true if handoff may be silently cleared after TR_REQ asserted but before BUSY asserted, false if assertion of TR_REQ should lock state of VALID, CS_0 and CS_1
            const Semi.E084.ActiveToPassivePinBits permitDeselectAfterSelectPins = (permitDeselectAfterSelect ? (VALID | CS_0 | TR_REQ | CONT) : noPins);

            switch (stateCode)
            {
                case StateCode.Initial: deltaPins = allPins; break;

                case StateCode.NotAvail_PortNotInit: deltaPins = noPins; break;
                case StateCode.NotAvail_PortIsInitializing: deltaPins = noPins; break;
                case StateCode.NotAvail_HardwareNotInstalled: deltaPins = noPins; break;
                case StateCode.NotAvail_PortNotInService: deltaPins = noPins; break;
                case StateCode.NotAvail_PortNotInAutoMode: deltaPins = noPins; break;
                case StateCode.NotAvail_TransferBlocked: deltaPins = noPins; break;
                case StateCode.NotAvail_PodPlacementFault: deltaPins = noPins; break;
                case StateCode.NotAvail_LightCurtainIlockTripped: deltaPins = noPins; break;

                case StateCode.Fault_IONotInstalled: deltaPins = allPins; break;
                case StateCode.Fault_IOFailure: deltaPins = allPins; break;
                case StateCode.Fault_InvalidSelect: deltaPins = VALID | CS_0 | CS_1 | CONT; break;
                case StateCode.Fault_InvalidActivePins: deltaPins = allPins; break;
                case StateCode.Fault_TransferAborted: deltaPins = noPins; break;
                case StateCode.Fault_TransferAbortedByInterlock: deltaPins = noPins; break;
                case StateCode.Fault_TP1_Timeout: deltaPins = noPins; break;
                case StateCode.Fault_TP2_Timeout: deltaPins = noPins; break;
                case StateCode.Fault_TP3_Timeout: deltaPins = noPins; break;
                case StateCode.Fault_TP4_Timeout: deltaPins = noPins; break;
                case StateCode.Fault_TP5_Timeout: deltaPins = noPins; break;
                case StateCode.Fault_TP6_Timeout: deltaPins = noPins; break;
                case StateCode.Fault_PodTransitionTimeout: deltaPins = noPins; break;
                case StateCode.Fault_UnexpectedPodPlacement: deltaPins = noPins; break;

                case StateCode.Available: deltaPins = VALID | CS_0 | CS_1 | LC_ILOCK; break;
                case StateCode.AvailableContinuous: deltaPins = VALID | CS_0 | CS_1 | LC_ILOCK | CONT; break;	// NOTE: CONT may be silently cleared in AvailableContinuous
                case StateCode.Selected: deltaPins = VALID | CS_0; break;				// permit Active side to deselect in this state (will take effect after change to appropriate available state).

                case StateCode.SelectedAndLoadable: deltaPins = permitDeselectAfterSelectPins | TR_REQ; break;
                case StateCode.RequestStartLoad: deltaPins = permitDeselectAfterSelectPins; break;
                case StateCode.ReadyToStartLoad: deltaPins = permitDeselectAfterSelectPins | BUSY; break;
                case StateCode.Loading: deltaPins = noPins; break;
                case StateCode.LoadingAndPodPresent: deltaPins = TR_REQ | BUSY | COMPT; break;
                case StateCode.LoadTransferDone: deltaPins = noPins; break;
                case StateCode.LoadTransferDoneWaitReleased: deltaPins = VALID | CS_0 | COMPT; break;
                case StateCode.LoadCompleted: deltaPins = noPins; break;

                case StateCode.SelectedAndUnloadable: deltaPins = permitDeselectAfterSelectPins | TR_REQ; break;
                case StateCode.RequestStartUnload: deltaPins = permitDeselectAfterSelectPins; break;
                case StateCode.ReadyToStartUnload: deltaPins = permitDeselectAfterSelectPins | BUSY; break;
                case StateCode.Unloading: deltaPins = noPins; break;
                case StateCode.UnloadingAndPodRemoved: deltaPins = TR_REQ | BUSY | COMPT; break;
                case StateCode.UnloadTransferDone: deltaPins = noPins; break;
                case StateCode.UnloadTransferDoneWaitReleased: deltaPins = VALID | CS_0 | COMPT; break;
                case StateCode.UnloadCompleted: deltaPins = noPins; break;

                default: deltaPins = allPins; break;
            }

            return deltaPins;
        }

        Semi.E084.ActiveToPassivePinBits GetE084ActiveExpectedPinsByState(StateCode stateCode)
        {
            const Semi.E084.ActiveToPassivePinBits noPins = Semi.E084.ActiveToPassivePinBits.None;
            Semi.E084.ActiveToPassivePinBits latchedCONT = mPriv.stateCodeContinuousBit;

            Semi.E084.ActiveToPassivePinBits pinBits = noPins;

            switch (stateCode)
            {
                case StateCode.Initial: pinBits = noPins; break;

                case StateCode.NotAvail_PortNotInit: pinBits = noPins; break;
                case StateCode.NotAvail_PortIsInitializing: pinBits = noPins; break;
                case StateCode.NotAvail_HardwareNotInstalled: pinBits = noPins; break;
                case StateCode.NotAvail_PortNotInService: pinBits = noPins; break;
                case StateCode.NotAvail_PortNotInAutoMode: pinBits = noPins; break;
                case StateCode.NotAvail_TransferBlocked: pinBits = noPins; break;
                case StateCode.NotAvail_PodPlacementFault: pinBits = noPins; break;
                case StateCode.NotAvail_LightCurtainIlockTripped: pinBits = noPins; break;

                case StateCode.Fault_IONotInstalled: pinBits = noPins; break;
                case StateCode.Fault_IOFailure: pinBits = noPins; break;
                case StateCode.Fault_InvalidSelect: pinBits = noPins; break;
                case StateCode.Fault_InvalidActivePins: pinBits = noPins; break;
                case StateCode.Fault_TransferAborted: pinBits = noPins; break;
                case StateCode.Fault_TransferAbortedByInterlock: pinBits = noPins; break;
                case StateCode.Fault_TP1_Timeout: pinBits = noPins; break;
                case StateCode.Fault_TP2_Timeout: pinBits = noPins; break;
                case StateCode.Fault_TP3_Timeout: pinBits = noPins; break;
                case StateCode.Fault_TP4_Timeout: pinBits = noPins; break;
                case StateCode.Fault_TP5_Timeout: pinBits = noPins; break;
                case StateCode.Fault_TP6_Timeout: pinBits = noPins; break;
                case StateCode.Fault_PodTransitionTimeout: pinBits = noPins; break;
                case StateCode.Fault_UnexpectedPodPlacement: pinBits = noPins; break;

                case StateCode.Available: pinBits = noPins; break;
                case StateCode.AvailableContinuous: pinBits = latchedCONT; break;
                case StateCode.Selected: pinBits = VALID | CS_0 | latchedCONT; break;

                case StateCode.SelectedAndLoadable: pinBits = VALID | CS_0 | latchedCONT; break;
                case StateCode.RequestStartLoad: pinBits = VALID | CS_0 | TR_REQ | latchedCONT; break;
                case StateCode.ReadyToStartLoad: pinBits = VALID | CS_0 | TR_REQ | latchedCONT; break;
                case StateCode.Loading: pinBits = VALID | CS_0 | TR_REQ | BUSY | latchedCONT; break;
                case StateCode.LoadingAndPodPresent: pinBits = VALID | CS_0 | TR_REQ | BUSY | latchedCONT; break;
                case StateCode.LoadTransferDone: pinBits = VALID | CS_0 | COMPT | latchedCONT; break;
                case StateCode.LoadTransferDoneWaitReleased: pinBits = latchedCONT; break;
                case StateCode.LoadCompleted: pinBits = latchedCONT; break;

                case StateCode.SelectedAndUnloadable: pinBits = VALID | CS_0 | latchedCONT; break;
                case StateCode.RequestStartUnload: pinBits = VALID | CS_0 | TR_REQ | latchedCONT; break;
                case StateCode.ReadyToStartUnload: pinBits = VALID | CS_0 | TR_REQ | latchedCONT; break;
                case StateCode.Unloading: pinBits = VALID | CS_0 | TR_REQ | BUSY | latchedCONT; break;
                case StateCode.UnloadingAndPodRemoved: pinBits = VALID | CS_0 | TR_REQ | BUSY | latchedCONT; break;
                case StateCode.UnloadTransferDone: pinBits = VALID | CS_0 | COMPT | latchedCONT; break;
                case StateCode.UnloadTransferDoneWaitReleased: pinBits = latchedCONT; break;
                case StateCode.UnloadCompleted: pinBits = latchedCONT; break;

                default: pinBits = noPins; break;
            }

            if (mConfig.LightCurtainInterlockInstalled)
                pinBits |= LC_ILOCK;		// we expect LC_ILOCK to remain set in this case

            return pinBits;
        }

        Semi.E084.ActiveToPassivePinBits GetE084IgnoreActivePinsByState(StateCode stateCode)
        {
            Semi.E084.ActiveToPassivePinBits pinBits = Semi.E084.ActiveToPassivePinBits.None;
            const Semi.E084.ActiveToPassivePinBits noPins = Semi.E084.ActiveToPassivePinBits.None;
            Semi.E084.ActiveToPassivePinBits inactiveIgnorePins = LC_ILOCK;
            Semi.E084.ActiveToPassivePinBits activeIgnorePins = (mConfig.LightCurtainInterlockInstalled ? noPins : LC_ILOCK);

            switch (stateCode)
            {
                case StateCode.Initial: pinBits = inactiveIgnorePins; break;

                case StateCode.NotAvail_PortNotInit: pinBits = inactiveIgnorePins; break;
                case StateCode.NotAvail_PortIsInitializing: pinBits = inactiveIgnorePins; break;
                case StateCode.NotAvail_HardwareNotInstalled: pinBits = inactiveIgnorePins; break;
                case StateCode.NotAvail_PortNotInService: pinBits = inactiveIgnorePins; break;
                case StateCode.NotAvail_PortNotInAutoMode: pinBits = inactiveIgnorePins; break;
                case StateCode.NotAvail_TransferBlocked: pinBits = inactiveIgnorePins; break;
                case StateCode.NotAvail_PodPlacementFault: pinBits = inactiveIgnorePins; break;
                case StateCode.NotAvail_LightCurtainIlockTripped: pinBits = inactiveIgnorePins; break;

                case StateCode.Fault_IONotInstalled: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_IOFailure: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_InvalidSelect: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_InvalidActivePins: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_TransferAborted: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_TransferAbortedByInterlock: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_TP1_Timeout: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_TP2_Timeout: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_TP3_Timeout: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_TP4_Timeout: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_TP5_Timeout: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_TP6_Timeout: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_PodTransitionTimeout: pinBits = inactiveIgnorePins; break;
                case StateCode.Fault_UnexpectedPodPlacement: pinBits = inactiveIgnorePins; break;

                case StateCode.Available: pinBits = inactiveIgnorePins; break;
                case StateCode.AvailableContinuous: pinBits = inactiveIgnorePins; break;
                case StateCode.Selected: pinBits = activeIgnorePins; break;

                case StateCode.SelectedAndLoadable: pinBits = activeIgnorePins; break;
                case StateCode.RequestStartLoad: pinBits = activeIgnorePins; break;
                case StateCode.ReadyToStartLoad: pinBits = activeIgnorePins; break;
                case StateCode.Loading: pinBits = activeIgnorePins; break;
                case StateCode.LoadingAndPodPresent: pinBits = activeIgnorePins; break;
                case StateCode.LoadTransferDone: pinBits = activeIgnorePins; break;
                case StateCode.LoadTransferDoneWaitReleased: pinBits = activeIgnorePins; break;
                case StateCode.LoadCompleted: pinBits = activeIgnorePins; break;

                case StateCode.SelectedAndUnloadable: pinBits = activeIgnorePins; break;
                case StateCode.RequestStartUnload: pinBits = activeIgnorePins; break;
                case StateCode.ReadyToStartUnload: pinBits = activeIgnorePins; break;
                case StateCode.Unloading: pinBits = activeIgnorePins; break;
                case StateCode.UnloadingAndPodRemoved: pinBits = activeIgnorePins; break;
                case StateCode.UnloadTransferDone: pinBits = activeIgnorePins; break;
                case StateCode.UnloadTransferDoneWaitReleased: pinBits = activeIgnorePins; break;
                case StateCode.UnloadCompleted: pinBits = activeIgnorePins; break;

                default: pinBits = noPins; break;
            }

            return pinBits;
        }

        #endregion

        //-------------------------------------------------------------------

#if (false)    // old TransferStateMachine.Requests class definition
			struct Requests		//!< Requests from state machine to the outside world
			{
				bool signalLoadStart;				//!< request client code to process load transfer start (PTS to blocked).
				bool signalUnloadStart;				//!< request client code to process unload transfer start (PTS to blocked).
				bool signalTransferComplete;		//!< request client code to process completed transfer (PTS to ready to load or remain blocked).  This request must be serviced promptly (<= 1.0 second) to avoid race conditions with use of AtoP pins

				Requests() 
					: signalLoadStart(false)
					, signalUnloadStart(false)
					, signalTransferComplete(false)
					, pioFailure(false)
					, pioFailureAcknowledgeAvailable(false)
				{}
			};
#endif
    }

    #region No longer needed

#if (false)
			TransferStateMachine(const string &myID, const TransferStateMachineConfig &config = TransferStateMachineConfig())
				, mConfig(config)
				, mAlarms(myID, config)
				, mLogger(myID)
				{
					SetState(Initial, "Constructor"); 
				}

			virtual ~TransferStateMachine() {};

			static const char * GetVersionStrP() { return E084PassiveTransferStateMachineVersionStr; };

			StateCode GetStateCode() { return mPriv.stateCode; }
			StateCode GetPIO1StateCode() { return mPriv.pio1StateCode; }
			StateCode GetPIO2StateCode() { return mPriv.pio2StateCode; }

			struct PortStateInfo
			{
				bool initializationInProgress;
				E087::LTS lts;				// port transfer state
				E087::AMS ams;				// access mode state
				bool podIsUndocked, podIsUnclamped;

				PortStateInfo() 
					: initializationInProgress(true)
					, lts(LTS.Undefined), ams(E087::AMS_Undefined)
					, podIsUndocked(false), podIsUnclamped(false)
				{}

				bool IsValid() { return (lts != LTS.Undefined && ams != E087::AMS_Undefined); }
			};

			PortStateInfo GetPortStateInfo() { return mPriv.portStateInfo; }
			void ProcessPortStateInfo(const PortStateInfo &portStateInfo);

			void ProcessPodPlacement(const E087::PodPlacement::SequencedState &podPlacementState);

			void ProcessPIO1State(const E084::IE084PassiveIO::SequencedState &passivePIO1IOState);
			void ProcessPIO2State(const E084::IE084PassiveIO::SequencedState &passivePIO2IOState);

				void ProcessPodPlacement(const E087::PodPlacement::SequencedState &podPlacementState)
				{
					if (mPriv.podSensorValues.PresentPlaced.GetSequenceNumber() == podPlacementState.GetSequenceNumber()
						|| mPriv.podSensorValues.PresentPlaced.GetObjectCRef() == podPlacementState.GetObjectCRef())
					{
						return;
					}

					mPriv.podSensorValues.PresentPlaced = podPlacementState;
					const E087::PodPlacement::State &state = mPriv.podSensorValues.PresentPlaced;

					Log.Info.Emit(chk_format("PodPlacementState is now '%s'") % state.ToString());

					ServiceStateAndUpdateOutputs();		// update state on all changes in pod placement
				}
				//-------------------------------------------------------------------
				//-------------------------------------------------------------------
				void ProcessPortStateInfo(const PortStateInfo &portStateInfo)
				{
					bool initChanged = (mPriv.portContextInfo.Initializing != mPriv.portContextInfo.Initializing);
					bool ltsChanged = (mPriv.portContextInfo.LTS != portStateInfo.lts);
					bool amsChanged = (mPriv.portContextInfo.AMS != portStateInfo.ams);
					bool podLocChanged = (mPriv.lpmPositionState.IsUnclamped != portStateInfo.podIsUnclamped
										  || mPriv.lpmPositionState.IsUndocked != portStateInfo.podIsUndocked);

					mPriv.portStateInfo = portStateInfo;

					if (initChanged)
					{
						if (mPriv.portContextInfo.Initializing)
							Log.Info.Emit("Port Initialize Started");
						else
						{
							Log.Info.Emit("Port Initialize Complete");
							mPriv.portHasBeenInitialized = true;
						}

						StateCodeSummaryInfo stateCodeSummaryInfo(mPriv.stateCode);

						if (mPriv.portContextInfo.Initializing)
						{
							// initialize is starting - update things
							if (stateCodeSummaryInfo.IsSelected())
								SetState(StateCode.Fault_TransferAborted, "Port was taken out of service (init) while PIO Select is active.");
							else if (stateCodeSummaryInfo.IsLoading())
								SetState(StateCode.Fault_TransferAborted, "Port was taken out of service (init) during active Load transfer.");
							else if (stateCodeSummaryInfo.IsUnloading())
								SetState(StateCode.Fault_TransferAborted, "Port was taken out of service (init) during active Unload transfer.");
							else
								ServiceStateAndUpdateOutputs();// cause state to transition to NotAvail_PortIsInitializing
						}
						else
						{
							// attempt to return to the correct inactive state., clear pioFailure alarm is we are no longer in a faulted state
							string reason = "PDO Initialize is complete";

							StateCode recoveredState = GetCurrentInactiveState(reason);

							SetState(recoveredState, reason);		// this will clear the alarm

							StateCodeSummaryInfo recoveredSummaryInfo(mPriv.stateCode);

							E041::ANState pioFailureANState = mAlarms.pioFailure.GetANState();
							if (!recoveredSummaryInfo.IsFaulted() && (pioFailureANState.signaling || pioFailureANState.visible))
							{
								Log.Info.Emit(chk_format("PIOFailure alarm cleared after PDO Initialize complete, E84 state:%s") % ConvertToString(mPriv.stateCode));
								mAlarms.pioFailure.Clear();
							}
						}
					}

					if (ltsChanged)
					{
						string stateStr = E087::ConvertToString(portStateInfo.lts);

						Log.Info.Emit(chk_format("PortTransferState is now '%s'") % stateStr);

						StateCodeSummaryInfo stateCodeSummaryInfo(mPriv.stateCode);

						if (!E087::IsLTSInService(mPriv.portContextInfo.LTS))
						{
							if (stateCodeSummaryInfo.IsSelected())
								SetState(StateCode.Fault_TransferAborted, "Port was taken out of service while PIO Select is active.");
							else if (stateCodeSummaryInfo.IsLoading())
								SetState(StateCode.Fault_TransferAborted, "Port was taken out of service during active Load transfer.");
							else if (stateCodeSummaryInfo.IsUnloading())
								SetState(StateCode.Fault_TransferAborted, "Port was taken out of service during active Unload transfer.");
							else
								ServiceStateAndUpdateOutputs();// cause state to transition to NotAvail_PortNotInService
						}
					}

					if (amsChanged)
					{
						Log.Info.Emit(chk_format("PortAccessMode is now '%s'") % E087::ConvertToString(portStateInfo.ams));

						if (mPriv.portContextInfo.AMS != AMS.Automatic)
						{
							StateCodeSummaryInfo stateCodeSummaryInfo(mPriv.stateCode);

							if (stateCodeSummaryInfo.IsLoading())
								SetState(StateCode.Fault_TransferAborted, "Port set to manual mode during active Load transfer.");
							else if (stateCodeSummaryInfo.IsUnloading())
								SetState(StateCode.Fault_TransferAborted, "Port set to manual mode during active Unload transfer.");
							else
								ServiceStateAndUpdateOutputs();		// this may cause the state to be changed to NotAvail_PortNotInAutoMode
						}
					}

					if (podLocChanged)
					{
						Log.Info.Emit(chk_format("LPM pod location undocked:%d unclamped:%d") 
															% int(mPriv.lpmPositionState.IsUndocked) 
															% int(mPriv.lpmPositionState.IsUnclamped));
					}
				}
    				void ProcessPIO1State(const E084::IE084PassiveIO::SequencedState &passivePIO1IOState)
				{
					if (mPriv.lastPIO1State.GetSequenceNumber() != passivePIO1IOState.GetSequenceNumber())
					{
						const E084::IE084PassiveIO::State &state = passivePIO1IOState;

						if (mPriv.lastPIO1State.inputs == state.inputs || mConfig.disableInputDebounce)
						{
							mPriv.pio1InputPinBits = state.inputs.GetPackedWord();
						}

						if (mPriv.lastPIO1State.inputs != state.inputs 
							|| mPriv.lastPIO1State.outputs != state.outputs
							|| mPriv.lastPIO1State.outputsReadback != state.outputsReadback)
						{
							Log.Info.Emit(chk_format("PIO1 PIO State is now inputs:'%s' outputs:'%s' %s readbacks:'%s'")
													% state.inputs.ToString()
													% state.outputs.ToString()
													% (state.outputs == state.outputsReadback ? "==" : "!=")
													% state.outputsReadback.ToString());
						}
					}

					bool inputPinsAreIdle = E084::ActiveToPassivePins::IsIdle(mPriv.pio1InputPinBits);					
					if (!inputPinsAreIdle)
						mPriv.pio1ActivePinsAreIdle = false;
					else if (mPriv.activePIO == PIOActiveSelect.PIO1 || DoesCurrentStatePermitPinsIdleReset())
						mPriv.pio1ActivePinsAreIdle = true;

					mPriv.lastPIO1State = passivePIO1IOState;
				}

				void ProcessPIO2State(const E084::IE084PassiveIO::SequencedState &passivePIO2IOState)
				{
					if (!mConfig.pio2InterfacePresent)
						return;

					if (mPriv.lastPIO2State.GetSequenceNumber() != passivePIO2IOState.GetSequenceNumber())
					{
						const E084::IE084PassiveIO::State &state = passivePIO2IOState;

						if (mPriv.lastPIO2State.inputs == state.inputs.GetPackedWord())
						{
							mPriv.pio2InputPinBits = state.inputs.GetPackedWord();
						}

						if (mPriv.lastPIO2State.inputs != state.inputs 
							|| mPriv.lastPIO2State.outputs != state.outputs
							|| mPriv.lastPIO2State.outputsReadback != state.outputsReadback)
						{
							Log.Info.Emit(chk_format("PIO2 PIO State is now inputs:'%s' outputs:'%s' %s readbacks:'%s'")
													% state.inputs.ToString()
													% state.outputs.ToString()
													% (state.outputs == state.outputsReadback ? "==" : "!=")
													% state.outputsReadback.ToString());
						}
					}

					bool inputPinsAreIdle = E084::ActiveToPassivePins::IsIdle(mPriv.pio2InputPinBits);					
					if (!inputPinsAreIdle)
						mPriv.pio2ActivePinsAreIdle = false;
					else if (mPriv.activePIO == PIOActiveSelect.PIO2 || DoesCurrentStatePermitPinsIdleReset())
						mPriv.pio2ActivePinsAreIdle = true;

					mPriv.lastPIO2State = passivePIO2IOState;
				}




				void UpdateConfig(const TransferStateMachineConfig &config)
				{
					if (mConfig.pio2InterfacePresent != config.pio2InterfacePresent
						|| mConfig.LCIlockClearsES != config.LCIlockClearsES
						|| mConfig.LCIlockClearsHO != config.LCIlockClearsHO
						|| mConfig.disableInputDebounce != config.disableInputDebounce
						|| mConfig.enableUnexpectedPodPlacementFault != config.enableUnexpectedPodPlacementFault
						|| mConfig.permitAutoRecoveryInTransferAbortedByInterlockState != config.permitAutoRecoveryInTransferAbortedByInterlockState
						|| (0 != memcmp(mConfig.permitAutoRecoveryIndexedByTPNum, config.permitAutoRecoveryIndexedByTPNum, sizeof(mConfig.permitAutoRecoveryIndexedByTPNum)))
						)
					{
						mConfig.pio2InterfacePresent = config.pio2InterfacePresent;
						mConfig.LCIlockClearsES = config.LCIlockClearsES;
						mConfig.LCIlockClearsHO = config.LCIlockClearsHO;
						mConfig.disableInputDebounce = config.disableInputDebounce;
						mConfig.enableUnexpectedPodPlacementFault = config.enableUnexpectedPodPlacementFault;
						mConfig.permitAutoRecoveryInTransferAbortedByInterlockState = config.permitAutoRecoveryInTransferAbortedByInterlockState;
						memcpy(mConfig.permitAutoRecoveryIndexedByTPNum, config.permitAutoRecoveryIndexedByTPNum, sizeof(mConfig.permitAutoRecoveryIndexedByTPNum));

						Log.Info.Emit(chk_format("HWConfig changed: pio2:'%s' LCIlockClearsES:'%s' LCIlockClearsHO:'%s' disableInputDebounce:'%s' enableUnexpectedPodPlacementFault:'%s' permitARinILockAbortState:'%s' permitARByTPNum:'%d,%d,%d,%d,%d'") 
										% (mConfig.pio2InterfacePresent ? "Yes" : "No")
										% (mConfig.LCIlockClearsES ? "Yes" : "No")
										% (mConfig.LCIlockClearsHO ? "Yes" : "No")
										% (mConfig.disableInputDebounce ? "Yes" : "No")
										% (mConfig.enableUnexpectedPodPlacementFault ? "Yes" : "No")
										% (mConfig.permitAutoRecoveryInTransferAbortedByInterlockState ? "Yes" : "No")
										% (mConfig.permitAutoRecoveryIndexedByTPNum[1] ? 1 : 0)
										% (mConfig.permitAutoRecoveryIndexedByTPNum[2] ? 1 : 0)
										% (mConfig.permitAutoRecoveryIndexedByTPNum[3] ? 1 : 0)
										% (mConfig.permitAutoRecoveryIndexedByTPNum[4] ? 1 : 0)
										% (mConfig.permitAutoRecoveryIndexedByTPNum[5] ? 1 : 0)
										);

						ServiceStateAndUpdateOutputs();
					}

					UpdateConfigs(config.hwInstalled, config.lightCurtainInterlockInstalled, config.enableAutoRecovery, config.holdPassiveOutputsDuringFault, config.clearESInTransferAbortedByInterlockState);
					UpdateConfigTimeValues(config.times);
				}

				void UpdateConfigs(bool hwInstalled, bool lightCurtainInterlockInstalled, bool enableAutoRecovery, bool holdPassiveOutputsDuringFault, bool clearESInTransferAbortedByInterlockState)
				{
					if (mConfig.hwInstalled != hwInstalled 
						|| mConfig.lightCurtainInterlockInstalled != lightCurtainInterlockInstalled
						|| mConfig.enableAutoRecovery != enableAutoRecovery
						|| mConfig.holdPassiveOutputsDuringFault != holdPassiveOutputsDuringFault
						|| mConfig.clearESInTransferAbortedByInterlockState != clearESInTransferAbortedByInterlockState
						)
					{
						mConfig.hwInstalled = hwInstalled;
						mConfig.lightCurtainInterlockInstalled = lightCurtainInterlockInstalled;
						mConfig.enableAutoRecovery = enableAutoRecovery;
						mConfig.holdPassiveOutputsDuringFault = holdPassiveOutputsDuringFault;
						mConfig.clearESInTransferAbortedByInterlockState = clearESInTransferAbortedByInterlockState;

						Log.Info.Emit(chk_format("HWConfig changed: E84_HwInstalled:'%s' lightCurtainInterlockInstalled:'%s' autoRecovery:'%s' holdPassiveOutputsDuringFault:'%s' clearESInTransferAbortedByInterlockState:'%s'") 
										% (hwInstalled ? "Yes" : "No")
										% (lightCurtainInterlockInstalled ? "Yes" : "No")
										% (enableAutoRecovery ? "Yes" : "No")
										% (holdPassiveOutputsDuringFault ? "Yes" : "No")
										% (clearESInTransferAbortedByInterlockState ? "Yes" : "No")
										);

						StateCodeSummaryInfo stateSummary(mPriv.stateCode);

						if (stateSummary.IsNotAvailable())
							SetState(mPriv.stateCode, "Hardware Installed flag has changed (drive delta ES pin)");
						else
							ServiceStateAndUpdateOutputs();
					}
				}

				void UpdatePassiveTimerValues(const E084::PassiveTimers &passiveTimerValues)
				{
					if (mConfig.times.passiveTimerValues != passiveTimerValues)
					{
						mConfig.times.passiveTimerValues = passiveTimerValues;

						Log.Info.Emit(chk_format("Passive Timer values changed to TP1:%.2f TP2:%.2f TP3:%.2f TP4:%.2f TP5:%.2f TP6:%.2f")
												% passiveTimerValues.TP1 % passiveTimerValues.TP2 % passiveTimerValues.TP3 
												% passiveTimerValues.TP4 % passiveTimerValues.TP5 % passiveTimerValues.TP6);
					}
				}

				void UpdateConfigTimeValues(const TransferStateMachineConfig::Times &configTimes)
				{
					if (mConfig.times != configTimes)
					{
						mConfig.times = configTimes;

						Log.Info.Emit(chk_format("Config Times values changed to TP1:%.2f TP2:%.2f TP3:%.2f TP4:%.2f TP5:%.2f TP6:%.2f OutRdMatchTO:%.2f PostPPFHO:%.2f PPTransTO:%.2f")
													% configTimes.passiveTimerValues.TP1 % configTimes.passiveTimerValues.TP2 % configTimes.passiveTimerValues.TP3 
													% configTimes.passiveTimerValues.TP4 % configTimes.passiveTimerValues.TP5 % configTimes.passiveTimerValues.TP6
													% configTimes.outputReadbackMatchTimeout
													% configTimes.postPPFaultHoldoff
													% configTimes.podPlacementTransitionTimeout);
					}
				}

				string AcknowledgePIOFailureAlarm()
				{
					string result;

					StateCodeSummaryInfo stateCodeSummaryInfo(mPriv.stateCode);

					if (!mAlarms.anManagerP)
						result = string("Unable to access annunciator manager.");
					else
					{
						E041::IAnnunciatorManager &mgr = *mAlarms.anManagerP;

						result = mgr.SelectAction(mAlarms.pioFailureANID, "Acknowledge");
					}

					if (!result.IsNullOrEmpty())
						Log.Info.Emit("TSM::PIOFailure Alarm Acknowledge request failed:" + result);

					return result;
				}
#endif

    #endregion
}
