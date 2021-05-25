//-------------------------------------------------------------------
/*! @file E090SubstrateScheduling.cs
 *  @brief 
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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
using System.Runtime.Serialization;
using System.Text;

using MosaicLib;
using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;
using MosaicLib.Semi.E039;
using MosaicLib.Semi.E090;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Semi.E090.SubstrateScheduling
{
    /*
     * The classes and defintions here are generally used as part of the Scheduler related addition to the E090 namespace concepts.
     * 
     * The SubstrateScheduling namepace contains a number of interfaces and base classes that can be used to assist in the creation of a state observing style of scheduler
     * that is envisioned here (and for which an example can be found in the MosaicLib.Semi.E090.SubstrateTestingTools namespace).
     * 
     * Generally the scheduler's contemplated here are expected to contain signficant amounts of tool specific logic, design and behavior.  The interfaces and realted concepts
     * presented here are expeced to be useful in many situations but there is no expectation that they will be useful in all substrate route and process execution scheduling cases.
     * For example the scheduler concepts presented here do not generally make use of any significant ability to look forward in time as part of any resource allocation and reservation 
     * constratined scheduler designs.
     * 
     * The scheduler use model contemplated here is based on a process that consists of a sequence of steps, each with a set of valid locations to visit and a specificiation of what processing 
     * step is to be performed at any such location.  In its basic form such a scheduler uses one or more SRM instances to move each substrate from place to place and performing the corresponoding 
     * the corresponding processing steps along the way.  This is embodied in the NextLocNameList property concept which is a property of a tracked substrate that reports out the set of locations
     * that the substrate would like to be at next based on its tracked state, its process sequence and the process steps that have already been performed on it.  As the substrate moves and is 
     * processed this property changes to indicate where the substrate needs to be next.  The majority of the work such a scheduler does is to evalute the set of subtrates
     * that would like to be moved (aka their NextLocNameList is non-empty and does not include their current location) and to select which should be moved and when to accomplish the overall
     * processing goals for each.
     * 
     * A compositional concept used here is the divsion of the scheduler into a wrapper part and a tool.  The tool contains the actual tracking and trigger logic that is used to drive the scheduler
     * behavior and the wrapper part is used to give it a thread and to perform basic housekeeping and publication related aspects.  The presance of this split allows the tool portion to be re-used
     * in a set of compatible wrapper parts so as to support use in different execution, job, and test environments.  This concept is not an essential one to the use models contempalated here and it
     * should only be employed in actual code if the ability to re-host the logic this way is desired and feasable in each specific use case.
     * 
     * In addition this overall approach is very dependant on the E039 and E090 update and publication related use model as the scheduler design requires that the updated substrate state it can see
     * will reflect the results of any SRM or processing actions in a temporally consistant maner with the completion of those related actions (AKA the published state changes that result from 
     * execution of any such action must be visible to the scheduler tracker objects before any such action completes).
     * 
     * Interface areas and concepts:  
     * 
     * ISubstrateSchedulerPart: auto/recovery/service behavior selection and state publishing.  The part is the wrapper that is generally used to host a tool instance, or directly implements the related decision tree logic.
     * ISubstrateSchedulerTool: recommended interface between wrapper (thread) and tool (decision tree logic) - when this pattern is used.
     * ISubstrateSchedulerPartState, ISubstrateSchedulerPartStateToTool (et. al.): information maintained in the wrapper, published and passed to the Tool during service and evaluation steps.
     * IPrepare, IPreparednessState, QuerySummaryState (et. al.): interface provided by process modules to allow the scheduler to determine if a processing path is currently viable and to be able to requet that one be made available when needed.
     * ISubstrateAndProcessTrackerBase (et. al.): combines the general concept of tracking the location and state of a substrate with the knowledge of its process specification and the steps that have been performed so far.
     */

    #region ISubstrateSchedulerPart (et. al.)

    /// <summary>
    /// This interface is expected to be supported by all SubstrateSchedulerPart type parts.
    /// </summary>
    /// <remarks>
    /// Generally when performing a GoOnline(true) action, a substrate scheduler part is expected to look for the presence of a SetSelectedBehavior key in its NPV.
    /// If this is found it shall get a BehaviorEnableFlags value from it and shall call PerformSetSelectedBehavior on that value.
    /// </remarks>
    public interface ISubstrateSchedulerPart : IActivePartBase
    {
        /// <summary>
        /// Action factory method:  When the resuling action is run it will attempt to change the part's BehaviorEnableFlags to be the given value.
        /// The scheduler will generally confirm that this request is permitted by the tool before making the change.  
        /// If the <paramref name="force"/> flag is true then the part will make the change in state even if the tool indicates that it should not be permitteed.
        /// The <paramref name="force"/> flag is expected to be used in cases where the user has been asked if they really want to make this change and they
        /// explicitly confirm that they do.
        /// </summary>
        IClientFacet SetSelectedBehavior(BehaviorEnableFlags flags, bool force = false);

        /// <summary>
        /// State publisher for the ISubstrateSchedulerPartState that is being passed to the tool's service method.
        /// A copy of information published here is also published to the corresponding PartID.State IVA.
        /// </summary>
        INotificationObject<ISubstrateSchedulerPartState> StatePublisher { get; }
    }

    #endregion

    #region ISubstrateSchedulerTool

    /// <summary>
    /// This is the interface that is expected to be supported by any tool class used to implement customer specific substrate scheduling rules so that these rules
    /// may be used within common Scheduler Parts and constructs in a way that allows the rules to be reused in different environements.  
    /// This is specifically intended to support seamless use of such a tool class in both Semi standard hosted enironments and in much simpler unit testing environements, amongst other cases.
    /// </summary>
    /// <remarks>
    /// This interface is only intended to be representational and/or usefull for some specific use cases and/or as an example.  
    /// It is expected that some scheduling related designs may generally want to be able to use an abstraction like this concept of a substrate scheduler tool but that the details
    /// of its implementation and use might be partially, or completely different.  As such this interface is not intended to cover all such pattern use cases.
    /// </remarks>
    public interface ISubstrateSchedulerTool<TSubstTrackerType, TProcessSpec, TProcessStepSpec> 
        where TSubstTrackerType : SubstrateAndProcessTrackerBase<TProcessSpec, TProcessStepSpec>
        where TProcessSpec : IProcessSpec<TProcessStepSpec>
        where TProcessStepSpec: IProcessStepSpec
    {
        /// <summary>Gives the INotifyable target that this tool is to attach to any Action it creates to notify the hosting part when the action completes</summary>
        INotifyable HostingPartNotifier { get; set; }

        /// <summary>Called when a new substrate tracker has been added.</summary>
        void Add(TSubstTrackerType substTracker);

        /// <summary>Called while dropping a substrate tracker</summary>
        void Drop(TSubstTrackerType substTracker);

        /// <summary>
        /// Allows the hosting part to inform the tool when a GoOnline Action is being performed.  
        /// This method is called after the hosting part has completed its own operations and they have all been completed successfully.
        /// </summary>
        string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv, Func<bool> hasStopBeenRequestedDelegate, ref RequestsAndStatusOutFromTool requestAndStatusOutFromTool);

        /// <summary>
        /// Allows the hosting part to inform the tool when a GoOffline Action is being performed.
        /// This method is called after the hosting part has completed its own operations and they have all been completed successfully.
        /// </summary>
        /// <remarks>The name of this method had been spelled PerformGoOofflineAction.  This has been corrected and will require corresponding derived classes to have the corresponding name change applied.</remarks>
        string PerformGoOfflineAction(IProviderFacet ipf, Func<bool> hasStopBeenRequestedDelegate);

        /// <summary>
        /// This method returns a list of reasons why the scheduler cannot be transitioned from the current given <paramref name="stateToTool"/> using the given <paramref name="requestNVS"/> request specification.  (See comments for Service above and here below for details on supported request patterns)
        /// This method will only be called if the request is not coming from the tool itself.  If the request is coming from the tool then the hosting part assumes that the tool has already determined that the change would be permitted.
        /// </summary>
        /// <remarks>
        /// In addition to the supported patterns from the RequestsAndStatusOutFromTool object, the following are generally expected to be supported keywords and/or named value pairs for Verify:
        ///     SetAutomatic - keyword (no value) - used to request that the hosting part can transition to a fully automatic state.  The hosting part typically indicates this to verify that it can GoOnline(true).
        ///     GoOnline - keyword (no value) - used to request that the hosting part can transition to an online state.  The hosting part typically indicates this to verify that it can GoOnline(false).
        ///     GoOffline - keyword (no value) - advisory - used to indicate that the hosting part is going to transition to the offline state by request.
        ///     SetBehavior - BehaviorEnableFlags - used when part is processing a SetSelectedBehavior request.  
        /// </remarks>
        IList<string> VerifyStateChange(ISubstrateSchedulerPartStateToTool stateToTool, INamedValueSet requestNVS);

        /// <summary>
        /// Called periodically to allow the tool to perform work.  Nominally this method is where the tool is given a repeated oportunity to create and start SRM and process actions.
        /// This method returns a count of the number of change it made or observed to allow the caller to optimze its spin loop sleep behavior to increase responsiveness in relation to loosely coupled state transitions.
        /// <param name="stateToTool">Gives the tool access to the state information that the hosting part provides to the tool in order to tell the tool what behavior is permitted and behavior is not.</param>
        /// <param name="requestAndStatusOutFromTool">Allows the tool to pass a set of requests, and status information, back to the hosting part so that the hosting part can react and/or publish state accordingly.  Please see remarks below  and under VerityStateChange for details on standard request patterns.</param>
        /// </summary>
        int Service(ISubstrateSchedulerPartStateToTool stateToTool, ref RequestsAndStatusOutFromTool requestAndStatusOutFromTool);
    }

    #endregion

    #region ISubstrateSchedulerPartState, SubstrateSchedulerPartStatusFlags, BehaviorEnableFlags, EMs, SubstrateSchedulerPartStateBase, SubstrateSchedulerPartStateForPublication

    /// <summary>
    /// This interface provides all of the standard information that a scheduler part now provides to a tool 
    /// </summary>
    public interface ISubstrateSchedulerPartState : IEquatable<ISubstrateSchedulerPartState>
    {
        /// <summary>Carries the hosting part's status flags to the tool</summary>
        SubstrateSchedulerPartStatusFlags PartStatusFlags { get; }

        /// <summary>Defines a set of tool and part behaviors that the part controls and passes to the tool so the tool can know which classes of behavior are currently permitted.</summary>
        BehaviorEnableFlags BehaviorEnableFlags { get; }

        /// <summary>Gives the tool access to the scheduler part's base state (or pseudo base state for non-part schedulers) so that the tool can derive basic information about the hosting part's UseState</summary>
        IBaseState PartBaseState { get; }

        /// <summary>Gives the tool access to an optional arbitraty set of additional key/value pairs</summary>
        INamedValueSet CustomStateNVS { get; }
    }

    /// <summary>
    /// Corresonding interface for information that is passed to the tool during Verify and Service calls.  This is a super-set of the ISubstrateSchedulerPartState.
    /// </summary>
    public interface ISubstrateSchedulerPartStateToTool : ISubstrateSchedulerPartState
    { }

    /// <summary>
    /// Status flags that a substrate scheduler maintains and makes available to the substrate scheduling tool.
    /// <para/>None (0x00), ServiceActionInProgress (0x01), RecoveryActionInProgress (0x02)
    /// </summary>
    [Flags]
    public enum SubstrateSchedulerPartStatusFlags : int
    {
        /// <summary>Placeholder default [0x00]</summary>
        None = 0x00,

        /// <summary>When this flag is set, it indicates that the scheduler part has created and started a service action on a subordinate part that has not completed yet.  The typical example of this is to indicate that a service action initiated SRM action is in progress.  [0x02]</summary>
        ServiceActionInProgress = 0x01,

        /// <summary>When this flag is set, it indicates that the sheduler part is running a recovery action pattern, such as ClearSystem.  </summary>
        RecoveryActionInProgress = 0x02,

        /// <summary>When this flag is set, one or more parts of the scheduler have an annuncaitor signaling and waiting for an action to be selected by the appropriate decision authority</summary>
        WaitingForActionToBeSelected = 0x10,
    }

    /// <summary>
    /// This enumeration defines the set of scheduler and scheduler tool flags that are used to enable various scheduling related behaviors.
    /// <para/>None (0x00), ServiceActions (0x01), RecoveryMaterialMovement (0x02), ProcessMaterialMovement (0x04), ProcessExecution (0x08), SubstrateLaunch (0x10)
    /// <para/>Combination values: Service (ServiceActions), Recovery (ServiceActions | RecoveryMaterialMovement), Automatic (RecoveryMaterialMovement | ProcessMaterialMovement | ProcessStepExecution | SubstrateLaunch)
    /// </summary>
    [Flags]
    public enum BehaviorEnableFlags : int
    {
        /// <summary>Placeholder default [0x00]</summary>
        None = 0x00,

        /// <summary>Enable handling of service actions [0x01]</summary>
        ServiceActions = 0x01,

        /// <summary>Enable handling of material movement for recovery of material [0x02]</summary>
        RecoveryMaterialMovement = 0x02,

        /// <summary>Enable handling of material movement required for normal processing [0x04]</summary>
        ProcessMaterialMovement = 0x04,

        /// <summary>Enable handling of process step exeuction required for normal processing [0x08]</summary>
        ProcessStepExecution = 0x08,

        /// <summary>Enable transition trigger logic for WaitingForStart to Runing transition [0x10]</summary>
        SubstrateLaunch = 0x10,

        /// <summary>Summary value for fully manual use: (ServiceActions) [0x01]</summary>
        Service = ServiceActions,

        /// <summary>Summary value for recovery use: (ServiceActions | RecoveryMaterialMovement) [0x03]</summary>
        Recovery = ServiceActions | RecoveryMaterialMovement,

        /// <summary>Summary value for automatic use: (RecoveryMaterialMovement | ProcessMaterialMovement | ProcessStepExecution | SubstrateLaunch) [0x1e]</summary>
        Automatic = RecoveryMaterialMovement | ProcessMaterialMovement | ProcessStepExecution | SubstrateLaunch,
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given <paramref name="stateToTool"/>'s BehaviorEnableFlags has the ServiceActions flag set 
        /// and its PartBaseState.UseState is Online, OnlineBusy, OnlineUninitialized, OnlineFailure,
        /// </summary>
        public static bool AreServiceActionsEnabled(this ISubstrateSchedulerPartState stateToTool)
        {
            return ((stateToTool.BehaviorEnableFlags & BehaviorEnableFlags.ServiceActions) != 0)
                && stateToTool.PartBaseState.UseState.IsOnline(acceptUninitialized: true, acceptOnlineFailure: true, acceptAttemptOnlineFailed: false);
        }

        /// <summary>
        /// Returns true if the given <paramref name="stateToTool"/>'s BehaviorEnableFlags has the RecoveryMaterialMovement flag set 
        /// and its PartBaseState.UseState is Online, OnlineBusy, or OnlineUninitialized
        /// </summary>
        public static bool IsRecoveryMaterialMovementEnabled(this ISubstrateSchedulerPartState stateToTool)
        {
            return ((stateToTool.BehaviorEnableFlags & BehaviorEnableFlags.RecoveryMaterialMovement) != 0)
                && stateToTool.PartBaseState.UseState.IsOnline(acceptUninitialized: true, acceptOnlineFailure: true, acceptAttemptOnlineFailed: false);
        }

        /// <summary>
        /// Returns true if the given <paramref name="stateToTool"/>'s BehaviorEnableFlags has the ProcessMaterialMovement flag set 
        /// and its PartBaseState.UseState is fully Online 
        /// </summary>
        public static bool IsProcessMaterialMovementEnabled(this ISubstrateSchedulerPartState stateToTool)
        {
            return ((stateToTool.BehaviorEnableFlags & BehaviorEnableFlags.ProcessMaterialMovement) != 0)
                && stateToTool.PartBaseState.UseState.IsOnline(acceptUninitialized: false, acceptOnlineFailure: false, acceptAttemptOnlineFailed: false);
        }

        /// <summary>
        /// Returns true if the given <paramref name="stateToTool"/>'s BehaviorEnableFlags has the ProcessStepExecution flag set 
        /// and its PartBaseState.UseState is fully Online 
        /// </summary>
        public static bool IsProcessStepExecutionEnabled(this ISubstrateSchedulerPartState stateToTool)
        {
            return ((stateToTool.BehaviorEnableFlags & BehaviorEnableFlags.ProcessStepExecution) != 0)
                && stateToTool.PartBaseState.UseState.IsOnline(acceptUninitialized: false, acceptOnlineFailure: false, acceptAttemptOnlineFailed: false);
        }

        /// <summary>
        /// Returns true if the given <paramref name="stateToTool"/>'s BehaviorEnableFlags has the SubstrateLaunch flag set 
        /// and its PartBaseState.UseState is fully Online 
        /// </summary>
        public static bool IsSubstrateLaunchEnabled(this ISubstrateSchedulerPartState stateToTool)
        {
            return ((stateToTool.BehaviorEnableFlags & BehaviorEnableFlags.SubstrateLaunch) != 0)
                && stateToTool.PartBaseState.UseState.IsOnline(acceptUninitialized: false, acceptOnlineFailure: false, acceptAttemptOnlineFailed: false);
        }
    }

    /// <summary>
    /// Default implementation object to support the ISubstrateSchedulerPartState interface
    /// </summary>
    public class SubstrateSchedulerPartState : ISubstrateSchedulerPartState, ISubstrateSchedulerPartStateToTool, ICopyable<ISubstrateSchedulerPartStateToTool>
    {
        /// <summary>Carries the hosting part's status flags to the tool</summary>
        public SubstrateSchedulerPartStatusFlags PartStatusFlags { get; set; }

        /// <summary>Gives the tool access to the scheduler part's base state (or pseudo base state for non-part schedulers) so that the tool can derive basic information about the hosting part's UseState</summary>
        public IBaseState PartBaseState { get { return _partBaseState ?? BaseState.None; } set { _partBaseState = value; } }

        private IBaseState _partBaseState;

        /// <summary>Defines a set of tool and part behaviors that the part controls and passes to the tool so the tool can know which classes of behavior are currently permitted.</summary>
        public BehaviorEnableFlags BehaviorEnableFlags { get; set; }

        INamedValueSet ISubstrateSchedulerPartState.CustomStateNVS { get { return _customStateNVS.MapNullToEmpty(); } }

        /// <summary>Gives the tool access to an optional arbitraty set of additional key/value pairs</summary>
        public NamedValueSet CustomStateNVS { get { return _customStateNVS ?? (_customStateNVS = new NamedValueSet()); } set { _customStateNVS = value; } }

        private NamedValueSet _customStateNVS;

        public override string ToString()
        {
            string nvsStr = _customStateNVS.IsNullOrEmpty() ? "" : " nvs:{0}".CheckedFormat(_customStateNVS.ToStringSML());
            return "Part:{0} Status:'{1}' EnabledBehavior:'{2}'{3}".CheckedFormat(PartBaseState, PartStatusFlags, BehaviorEnableFlags, nvsStr);
        }

        /// <summary>
        /// IEquatable implementation method.  Returns true IFF the given <paramref name="other"/>'s contents are equal to this object's contents.
        /// </summary>
        public bool Equals(ISubstrateSchedulerPartState other)
        {
            return (other != null
                    && PartStatusFlags == other.PartStatusFlags
                    && PartBaseState.Equals(other.PartBaseState)
                    && BehaviorEnableFlags == other.BehaviorEnableFlags
                    && CustomStateNVS.IsEqualTo(other.CustomStateNVS, compareReadOnly: false)
                    );
        }

        /// <summary>
        /// Allows the caller to generate a copy of this object.  This is typically used for publication through an INotificationObject such as the ISubstrateSchedulerPart.StatePublisher
        /// </summary>
        public virtual ISubstrateSchedulerPartStateToTool MakeCopyOfThis(bool deepCopy = true)
        {
            var copyOfThis = (SubstrateSchedulerPartState)this.MemberwiseClone();

            _customStateNVS = _customStateNVS.ConvertToReadOnly(mapNullToEmpty: false);

            return copyOfThis;
        }
    }

    /// <summary>
    /// Version of ISubstrateSchedulerPartState that is used for publication.  Instances of this class are immutable.
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public class SubstrateSchedulerPartStateForPublication : ISubstrateSchedulerPartState
    {
        public SubstrateSchedulerPartStateForPublication()
        {
            PartBaseState = BaseState.None;
        }

        public SubstrateSchedulerPartStateForPublication(ISubstrateSchedulerPartState other)
        {
            PartStatusFlags = other.PartStatusFlags;
            PartBaseState = other.PartBaseState.ConvertToBaseState();
            BehaviorEnableFlags = other.BehaviorEnableFlags;
            CustomStateNVS = other.CustomStateNVS.ConvertToReadOnly();
        }

        /// <summary>Returns an empty state instance.</summary>
        public static ISubstrateSchedulerPartState Empty { get { return _empty; } }
        private static readonly ISubstrateSchedulerPartState _empty = new SubstrateSchedulerPartStateForPublication();

        /// <summary>Carries the hosting part's status flags to the tool</summary>
        [DataMember(Order = 1000, EmitDefaultValue = false, IsRequired = false)]
        public SubstrateSchedulerPartStatusFlags PartStatusFlags { get; set; }

        /// <summary>Gives the tool access to the scheduler part's base state (or pseudo base state for non-part schedulers) so that the tool can derive basic information about the hosting part's UseState</summary>
        [DataMember(Order = 1100, Name = "PartBaseState", EmitDefaultValue = false, IsRequired = false)]
        public BaseState PartBaseState { get; set; }

        IBaseState ISubstrateSchedulerPartState.PartBaseState { get { return PartBaseState; } }

        /// <summary>Defines a set of tool and part behaviors that the part controls and passes to the tool so the tool can know which classes of behavior are currently permitted.</summary>
        [DataMember(Order = 1200, EmitDefaultValue = false, IsRequired = false)]
        public BehaviorEnableFlags BehaviorEnableFlags { get; set; }

        INamedValueSet ISubstrateSchedulerPartState.CustomStateNVS { get { return _customStateNVS.MapNullToEmpty(); } }

        /// <summary>Gives the tool access to an optional arbitraty set of additional key/value pairs</summary>
        public NamedValueSet CustomStateNVS { get { return _customStateNVS.MapNullToEmpty(); } set { _customStateNVS = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }

        [DataMember(Order = 1300, Name = "CustomStateNVS", EmitDefaultValue = false, IsRequired = false)]
        private NamedValueSet _customStateNVS;

        public override string ToString()
        {
            string nvsStr = _customStateNVS.IsNullOrEmpty() ? "" : " nvs:{0}".CheckedFormat(_customStateNVS.ToStringSML());
            return "Part:{0} Status:'{1}' EnabledBehavior:'{2}'{3}".CheckedFormat(PartBaseState, PartStatusFlags, BehaviorEnableFlags, nvsStr);
        }

        /// <summary>
        /// IEquatable implementation method.  Returns true IFF the given <paramref name="other"/>'s contents are equal to this object's contents.
        /// </summary>
        public bool Equals(ISubstrateSchedulerPartState other)
        {
            return (other != null
                    && PartStatusFlags == other.PartStatusFlags
                    && PartBaseState.Equals(other.PartBaseState)
                    && BehaviorEnableFlags == other.BehaviorEnableFlags
                    && CustomStateNVS.IsEqualTo(other.CustomStateNVS, compareReadOnly: false)
                    );
        }
    }

    #endregion

    #region RequestsAndStatusOutFromTool

    /// <summary>
    /// This object contains the set of status and request information that the tool provides to the scheduler so that the scheduler can react and reflect that information
    /// This is particularly centered on concepts of fault indication, and requests for changes in behavior enable flags (typically due to handling of annuniciator selected actions)
    /// </summary>
    public struct RequestsAndStatusOutFromTool
    {
        /// <summary>
        /// When this is non-empty, it indicates that the tool has at least one active fault condition (usually representing a signaling annunciator)
        /// </summary>
        public string ToolFaultReason { get; set; }

        /// <summary>
        /// This gives the set of scheduler status flags that the tool is reporting.  Generally the scheduler part will reflect this to the information that it publishes to the outside world.
        /// </summary>
        public SubstrateSchedulerPartStatusFlags ToolStatusFlags { get; set; }

        /// <summary>
        /// When this NVS is non-mepty it gives the set of keywords and/or key value pairs that can be used by the tool to ask the part to do things for it.  (see remarks below)
        /// </summary>
        /// <remarks>
        /// The following is a set of recommended keywords and named values to support:
        ///     SetService - keyword (no value) - indicates that the tool would like the part to change settings to the "Service" settings - service requests permitted, automatic motion and processing not permitted.
        ///     SetRecovery - keyword (no value) - indicates that the tool would like the part to change settings to the "Recovery" settings.
        ///     SetAutomatic - keyword (no value) - indicates that the tool would like the part to change settings to the "Automatic" settings.
        ///     Reason - string value indicates reason for this request
        ///     WaitingForActionToBeSelected - keyword (no value) - indicates that one or more tool annunciators are in an OnAndWaiting state.
        /// </remarks>
        public INamedValueSet RequestNVSFromTool { get; set; }
    }

    #endregion

    #region IPrepare, IPreparednessState, PreparednessStateForPublication, QuerySummaryState, IPreparednessState<>, PreparednessQueryResult<>, PreparednessState<>, EMs

    /// <summary>
    /// This interface may be used by a scheduler to observe and attempt to control the "preparedness" of a part/module.
    /// This interface allows the client to request the part to prepare to process a specific recipe step and to observe the last published IPreparednessState
    /// This state then supports a Query interface that can be used to (indirectly) ask the part if it is ready for a given process step spec and/or to get additional information about it
    /// that is relevant to decision tree logic for routing wafers.
    /// <para/>Individual parts are generally responsible for preforming individual process steps where the scheduler routes the wafers between locations so that it can perform each of the steps in the process sequence that has been assigned to the wafer.
    /// As such this interface is currently focused on the logic and patterns around preparing a part to be able to perform a specific processing step.
    /// </summary>
    public interface IPrepare<TProcessSpecType, TProcessStepSpecType>
        where TProcessSpecType : IProcessSpec
        where TProcessStepSpecType : IProcessStepSpec
    {
        /// <summary>
        /// Action factory method.  When the resulting action is run, it will request the part to prepare for the given <paramref name="processStepSpec"/>.
        /// This action type typically supports cancelation and it is expected that the scheduler will cancel prepare requests when it is no longer waiting for them.
        /// In additon a part may choose to abort a prepare request if it receives another that contradicts the first.
        /// </summary>
        IClientFacet PrepareForStep(TProcessSpecType processSpec, TProcessStepSpecType processStepSpec);

        /// <summary>
        /// This publisher gives the most recently generated IPreparednessState value from the part.  
        /// Typically clients observe this and re-evaluate all of their related QueryResults when a new IPreparednessState instance is published.
        /// </summary>
        INotificationObject<IPreparednessState<TProcessSpecType, TProcessStepSpecType>> StatePublisher { get; }
    }

    /// <summary>
    /// This interface is the basis for both the IPreparednessState (et. al.) and the PreparednessQueryResult.  
    /// It is also the basis for a publisher class that can be used to publish a serializable copy of either of these.
    /// </summary>
    public interface IPreparednessState : IEquatable<IPreparednessState>
    {
        /// <summary>Gives the recipe name of the corresponding process spec (if any)</summary>
        string ProcessSpecRecipeName { get; }

        /// <summary>Gives the recipe name of the corresponding process step spec (if any)</summary>
        string ProcessStepSpecRecipeName { get; }

        /// <summary>Gives the general state of preparedness as reported by the part, in relation to its currently selected, or seperately indicated ProcessSpec and ProcessStepSpec.</summary>
        QuerySummaryState SummaryState { get; }

        /// <summary>Gives the time stamp that was assigned when the SummaryState was last updated.  This is also used as the reference time stamp for use with the EstimatedTimeToReady property's value.</summary>
        QpcTimeStamp SummaryStateTimeStamp { get; }

        /// <summary>
        /// Gives the period of time, measured from the given SummaryStateTimeStamp, after which the source part believes that it will be Ready.  
        /// When the source part is not actively preparing for a given process step spec, or when it cannot predict the delay, this value will be TimeSpan.Zero.
        /// </summary>
        TimeSpan EstimatedTimeToReady { get; }

        /// <summary>Gives a text description of what triggered the last publication of this object (if any).</summary>
        string Reason { get; }

        /// <summary>Allows the object to carry additional arbitrary data.</summary>
        INamedValueSet NVS { get; }
    }

    /// <summary>
    /// This class is intended to be used to support publication of an IPreparednessState derived state via an IVAs or via other mechanisms that require serialization support.
    /// Instances of this class are immutable.
    /// </summary>
    [DataContract(Namespace=MosaicLib.Constants.E090NameSpace)]
    public class PreparednessStateForPublication : IPreparednessState
    {
        public static IPreparednessState Empty { get { return _empty; } }
        private static readonly IPreparednessState _empty = new PreparednessStateForPublication();

        public PreparednessStateForPublication()
        {
            ProcessSpecRecipeName = string.Empty;
            ProcessStepSpecRecipeName = string.Empty;
        }

        /// <summary>Makes a copy of the contents of the given <paramref name="other"/> instance.</summary>
        public PreparednessStateForPublication(IPreparednessState other)
        {
            SummaryState = other.SummaryState;
            SummaryStateTimeStamp = other.SummaryStateTimeStamp;
            EstimatedTimeToReady = other.EstimatedTimeToReady;
            Reason = other.Reason;
            NVS = other.NVS;
        }

        /// <summary>Gives the recipe name of the corresponding process spec (if any)</summary>
        [DataMember(Order = 800)]
        public string ProcessSpecRecipeName { get; private set; }

        /// <summary>Gives the recipe name of the corresponding process step spec (if any)</summary>
        [DataMember(Order = 900)]
        public string ProcessStepSpecRecipeName { get; private set; }

        /// <summary>Gives the general state of preparedness as reported by the part, in relation to its currently selected, or seperately indicated ProcessSpec and ProcessStepSpec.</summary>
        [DataMember(Order = 1000)]
        public QuerySummaryState SummaryState { get; private set; }

        /// <summary>Gives the time stamp that was assigned when the SummaryState was last updated.  This is also used as the reference time stamp for use with the EstimatedTimeToReady property's value.</summary>
        public QpcTimeStamp SummaryStateTimeStamp { get; private set; }

        [DataMember(Order = 1100)]
        private double SummaryStateTimeStampAge { get { return SummaryStateTimeStamp.Age.TotalSeconds; } set { SummaryStateTimeStamp = QpcTimeStamp.Now + value.FromSeconds(); } }

        /// <summary>
        /// Gives the period of time, measured from the given SummaryStateTimeStamp, after which the source part believes that it will be Ready.  
        /// When the source part is not actively preparing for a given process step spec, or when it cannot predict the delay, this value will be TimeSpan.Zero.
        /// </summary>
        [DataMember(Order = 1200, EmitDefaultValue=false, IsRequired=false)]
        public TimeSpan EstimatedTimeToReady { get; private set; }

        /// <summary>Gives a text description of what triggered the last publication of this object (if any).</summary>
        public string Reason { get { return _reason.MapNullToEmpty(); } private set { _reason = value.MapEmptyToNull(); } }

        [DataMember(Order = 1300, Name="Reason", EmitDefaultValue = false, IsRequired = false)]
        private string _reason;

        /// <summary>Allows the object to carry additional arbitrary data.</summary>
        public INamedValueSet NVS { get { return _nvs.MapNullToEmpty(); } private set { _nvs = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }

        [DataMember(Order = 1400, Name="NVS", EmitDefaultValue = false, IsRequired = false)]
        private NamedValueSet _nvs;

        /// <summary>Debugging and logging assistant</summary>
        public override string ToString()
        {
            return "Preparedness {0} State:{1} ETTR:{2:f3} Reason:'{3}' NVS:{4}".CheckedFormat(ProcessSpecRecipeName.CombineRecipeNames(ProcessStepSpecRecipeName), SummaryState, EstimatedTimeToReady.TotalSeconds, Reason, NVS.SafeToStringSML());
        }

        /// <summary>
        /// Returns true if the object has the same contents as the given <paramref name="other"/> one.
        /// </summary>
        public bool Equals(IPreparednessState other)
        {
            return (other != null
                    && ProcessSpecRecipeName == other.ProcessSpecRecipeName
                    && ProcessStepSpecRecipeName == other.ProcessStepSpecRecipeName
                    && SummaryState.Equals(other.SummaryState)
                    && SummaryStateTimeStamp == other.SummaryStateTimeStamp
                    && EstimatedTimeToReady == other.EstimatedTimeToReady
                    && Reason == other.Reason
                    && NVS.Equals(other.NVS)
                    );
        }
    }

    /// <summary>
    /// This flag enumeration is used to define a set of flags that are conveighed via state and query results.
    /// The interpretation, and permitted combinations, of these flags is context dependant in relation to its use in state and query results.
    /// <para/>None (0x00), NotAvailable (0x01), Ready (0x02), Preparing (0x04), CanPrepare (0x08), Compatible (0x10), ProcessLocked (0x20), HoldInboundMaterial (0x40)
    /// </summary>
    /// <remarks>
    /// This enumeration is used in both preparedness state and query results.
    /// Generally preparedness state objects will assert one of NotAvailable, Ready, or Preparing, possibly with CanPrepare
    /// Generally the Query method will assert the same values with Ready and Preparing masked off if either of the spec items given to the Query method doe not match, or are not compatible with, the corresponding ones from the sourcing state.
    /// </remarks>
    [Flags, DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum QuerySummaryState : int
    {
        /// <summary>Placeholder default value. [0x00]</summary>
        [EnumMember]
        None = 0x00,

        /// <summary>
        /// Indicates that the part is in a not availalbe, or other fault condition, where it is neither Ready nor Preparing.  
        /// If this is indicated with CanPrepare then the part expects that the use of Prepare is possibly able to succeed (such as in the OnlineFailure state). 
        /// If this is indicated without CanPrepare then the part must be successfully initialized (GoOnlineAndInitialize) first (such as in the AttemptOnlineFailed state).
        /// [0x01]
        /// </summary>
        [EnumMember]
        NotAvailable = 0x01,

        /// <summary>Indicates that the part is ready to process the corresponding ProcessStepSpec.  [0x02]</summary>
        [EnumMember]
        Ready = 0x02,

        /// <summary>Indicates that the part is preparing to be ready to process the corresponding ProcessStepSpec.  [0x04]</summary>
        [EnumMember]
        Preparing = 0x04,

        /// <summary>Indicates that the query believes that the part can prepare for the corresponding ProcessStepSpec.  [0x08]</summary>
        [EnumMember]
        CanPrepare = 0x08,

        /// <summary>
        /// Indiciates that the corresponding ProcessStepSpec is not identical to the one that the part has prepared for but that the given one is compatible with the one the part has been prepared for and thus that it can be run without re-preparing.  
        /// This flag must be combined with either Ready or Preparing.  [0x10]
        /// </summary>
        [EnumMember]
        Compatible = 0x10,

        /// <summary>
        /// This flag is used to indicate that the process the part has prepared for cannot be changed through the scheduler driven Prepare mechanism because it has been locked to only run another process.  
        /// This flag will be used alone if the step specs are not compatible or with Ready,Compatible or Preparing,Compatible.  [0x20]
        /// </summary>
        [EnumMember]
        ProcessLocked = 0x20,

        /// <summary>
        /// This flag may be used by a corresponding part to request that the scheduler block arrival of new inbound material, 
        /// typically in order to then request or automatically perform some maintenance activity that requires it be empty at the time.
        /// The use of this flag is independent of the use of all other flags.  
        /// The scheduler is expected to delay requesting the part to prepare until this flag has been cleard.
        /// <para/>Be aware that the use of this flag is asynchrnous with the scheduler and that their will be latency between when the part first requests this and when
        /// the scheduler recognizes the request and holds/stops arrival of new inbound materal.
        /// </summary>
        [EnumMember]
        HoldInboundMaterial = 0x40,
    }

    /// <summary>
    /// This interface defines the externally visible set of properties and method(s) that may be used by an external client to gain insight into the
    /// state of preparedness of a part that supports the IPrepare interface.  Each time the source part generates new information about is overall state of preparedness
    /// or has an internal state change that would modify the query results that it would produce, updates and publishes a new object that supports this interface, to reflect
    /// the internal change in state.
    /// <para/>NOTE: the contents of the object that backs any published version of this interface must be immutable.  
    /// The results of reviewing any property or calling any method with identical parameters as produced by any object that implements this interface cannot change after it has been published.
    /// <para/>As such all of the properties and method(s) supported by this interface on published objects must be thread safe and reenterant.  
    /// Normally this is a natural result of the underlying object being immutable.
    /// </summary>
    public interface IPreparednessState<TProcessSpecType, TProcessStepSpecType> : IPreparednessState, IEquatable<IPreparednessState<TProcessSpecType, TProcessStepSpecType>>
        where TProcessSpecType : IProcessSpec
        where TProcessStepSpecType : IProcessStepSpec
    {
        /// <summary>Gives the ProcessSpec for which the source object has been prepared or is preparing.  This property may be null if the part has never started a prepare.</summary>
        TProcessSpecType ProcessSpec { get; }

        /// <summary>Gives the ProcessStepSpec for which the source object has been prepared or is preparing.  This property may be null if the part has never started a prepare.</summary>
        TProcessStepSpecType ProcessStepSpec { get; }

        /// <summary>
        /// This method is passed a <paramref name="processStepSpec"/> that the caller would like to obtain preparedness information about and a time stamp.
        /// This method compares the given <paramref name="processStepSpec"/> to the one that the publisher has prepared, or is preparing, for to determine if they are the same
        /// and/or if they are compatible.  
        /// This method produces a PreparednessQueryResult that gives information about the results of the query for the given <paramref name="processStepSpec"/>
        /// which includes the specific QuerySummaryState flags for the query on this step spec and information about when the part estimates that it will be complete
        /// </summary>
        PreparednessQueryResult<TProcessSpecType, TProcessStepSpecType> Query(TProcessSpecType processSpec, TProcessStepSpecType processStepSpec, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp));
    }

    /// <summary>
    /// This struct gives the results for a Query operation on the contained ParentState when passed the contained ProcessStepSpec.  
    /// The SummaryState gives the flag values that resulted from the query and these are combined with the SummaryStateTimeStamp (which gives the time stamp at the time the Query results were evaluated)
    /// and the EstimatedTimeToReady which will be non-zero when the query results indicate that the published ParentState indicates that the part expects to be ready to process the queried process step
    /// after the indicated period estimate has elapsed.
    /// <para/>The information contained in this query result will remain valid until the source part publishes another IPreparednessState at which point the information contained here becomes stale and should be replaced with a fresh result obtained from the newly published state object.
    /// </summary>
    public struct PreparednessQueryResult<TProcessSpecType, TProcessStepSpecType> : IPreparednessState
        where TProcessSpecType : IProcessSpec
        where TProcessStepSpecType : IProcessStepSpec
    {
        /// <summary>Gives the parent IPreparednessState from which this query result was generated.</summary>
        public IPreparednessState<TProcessSpecType, TProcessStepSpecType> ParentState { get; set; }

        /// <summary>Gives the client provided process spec for which this query result was generated.</summary>
        public TProcessSpecType ProcessSpec { get; set; }

        /// <summary>Gives the client provided process step spec for which this query result was generated.</summary>
        public TProcessStepSpecType ProcessStepSpec { get; set; }

        /// <summary>Gives the recipe name of the corresponding process spec (if any)</summary>
        public string ProcessSpecRecipeName { get { return (ProcessSpec != null) ? ProcessSpec.RecipeName : null; } }

        /// <summary>Gives the recipe name of the corresponding process step spec (if any)</summary>
        public string ProcessStepSpecRecipeName { get { return (ProcessStepSpec != null) ? ProcessStepSpec.StepRecipeName : null; } }

        /// <summary>Gives the general state of preparedness as reported by the part through the published state's Query method, in relation to the indicated ProcessSpec and ProcessStepSpec which were given to the Query method when it was called.</summary>
        public QuerySummaryState SummaryState { get; set; }

        /// <summary>Gives the time stamp that was assigned when the SummaryState was last updated (by the query method).  This is also used as the reference time stamp for use with the EstimatedTimeToReady property's value.</summary>
        public QpcTimeStamp SummaryStateTimeStamp { get; set; }

        /// <summary>
        /// Gives the period of time, measured from the given SummaryStateTimeStamp, after which the source part believes that it will be Ready.  
        /// When the source part is not actively preparing for a given process step spec, or when it cannot predict the delay, this value will be TimeSpan.Zero.
        /// </summary>
        public TimeSpan EstimatedTimeToReady { get; set; }

        /// <summary>Gives a text description of what triggered the last publication of this object (if any).</summary>
        public string Reason { get; set; }

        /// <summary>Allows the object to carry additional arbitrary data.</summary>
        public INamedValueSet NVS { get; set; }

        /// <summary>Debugging and logging assistant</summary>
        public override string ToString()
        {
            return "QueryResult {0} State:{1} ETTR:{2:f3} Reason:'{3}' NVS:{4}".CheckedFormat(ProcessSpec.CombineRecipeNames(ProcessStepSpec), SummaryState, EstimatedTimeToReady.TotalSeconds, Reason, NVS.SafeToStringSML());
        }

        /// <summary>
        /// Returns true if the object has the same contents as the given <paramref name="other"/> one.
        /// </summary>
        public bool Equals(IPreparednessState other)
        {
            return (other != null
                    && ProcessSpecRecipeName == other.ProcessSpecRecipeName
                    && ProcessStepSpecRecipeName == other.ProcessStepSpecRecipeName
                    && SummaryState.Equals(other.SummaryState)
                    && SummaryStateTimeStamp == other.SummaryStateTimeStamp
                    && EstimatedTimeToReady == other.EstimatedTimeToReady
                    && Reason == other.Reason
                    && NVS.Equals(other.NVS)
                    );
        }
    }

    /// <summary>
    /// This is a basic example implementation class for the IPreparednessState
    /// </summary>
    public class PreparednessState<TProcessSpecType, TProcessStepSpecType> : IPreparednessState<TProcessSpecType, TProcessStepSpecType>, ICopyable<IPreparednessState<TProcessSpecType, TProcessStepSpecType>>
        where TProcessSpecType : IProcessSpec
        where TProcessStepSpecType : IProcessStepSpec
    {
        /// <summary>Gives the ProcessSpec for which the source object has been prepared or is preparing.  This property may be null if the part has never started a prepare.</summary>
        public TProcessSpecType ProcessSpec { get; set; }

        /// <summary>Gives the ProcessStepSpec for which the source object has been prepared or is preparing.  This property may be null if the part has never started a prepare.</summary>
        public TProcessStepSpecType ProcessStepSpec { get; set; }

        /// <summary>Gives the recipe name of the corresponding process spec (if any)</summary>
        public string ProcessSpecRecipeName { get { return (ProcessSpec != null) ? ProcessSpec.RecipeName : null; } }

        /// <summary>Gives the recipe name of the corresponding process step spec (if any)</summary>
        public string ProcessStepSpecRecipeName { get { return (ProcessStepSpec != null) ? ProcessStepSpec.StepRecipeName : null; } }
        
        /// <summary>Gives the general state of preparedness as reported by the part, in relation to its currently selected, or seperately indicated ProcessSpec and ProcessStepSpec.</summary>
        public QuerySummaryState SummaryState { get; set; }

        /// <summary>Gives the time stamp that was assigned when the SummaryState was last updated.  This is also used as the reference time stamp for use with the EstimatedTimeToReady property's value.</summary>
        public QpcTimeStamp SummaryStateTimeStamp { get; set; }

        /// <summary>
        /// Gives the period of time, measured from the given SummaryStateTimeStamp, after which the source part believes that it will be Ready.  
        /// When the source part is not actively preparing for a given process step spec, or when it cannot predict the delay, this value will be TimeSpan.Zero.
        /// </summary>
        public TimeSpan EstimatedTimeToReady { get; set; }

        /// <summary>Gives a text description of what triggered the last publication of this object (if any).</summary>
        public string Reason { get; set; }

        /// <summary>Allows the object to carry additional arbitrary data.</summary>
        public INamedValueSet NVS { get; set; }

        public virtual PreparednessQueryResult<TProcessSpecType, TProcessStepSpecType> Query(TProcessSpecType processSpec, TProcessStepSpecType processStepSpec, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

            var result = new PreparednessQueryResult<TProcessSpecType, TProcessStepSpecType>()
            {
                ParentState = this,
                ProcessSpec = processSpec,
                ProcessStepSpec = processStepSpec,
                SummaryStateTimeStamp = qpcTimeStamp,
                NVS = NVS.ConvertToReadOnly(),
            };

            if (Object.ReferenceEquals(ProcessSpec, processSpec) && Object.ReferenceEquals(ProcessStepSpec, processStepSpec))
            {
                result.SummaryState = SummaryState;

                if (!EstimatedTimeToReady.IsZero() && SummaryState.IsSet(QuerySummaryState.Preparing))
                    result.EstimatedTimeToReady = new TimeSpan(1).Min(EstimatedTimeToReady - (qpcTimeStamp - SummaryStateTimeStamp));

                Reason = "Query requeseted [{0}]".CheckedFormat(Reason);
            }
            else
            {
                result.SummaryState = SummaryState.Clear(QuerySummaryState.Ready | QuerySummaryState.Preparing);

                result.Reason = "Given process spec items do not match part's selected process spec: {0}. [{1}]".CheckedFormat(ProcessSpec.CombineRecipeNames(ProcessStepSpec), Reason);
            }

            return result;
        }

        /// <summary>
        /// Allows the caller to generate a copy of this object.  This is typically used for publication through an INotificationObject such as the IPrepare.StatePublisher
        /// </summary>
        public virtual IPreparednessState<TProcessSpecType, TProcessStepSpecType> MakeCopyOfThis(bool deepCopy = true)
        {
            var copyOfThis = (PreparednessState<TProcessSpecType, TProcessStepSpecType>) this.MemberwiseClone();
            copyOfThis.NVS = copyOfThis.NVS.ConvertToReadOnly();

            return copyOfThis;
        }

        /// <summary>Debugging and logging assistant</summary>
        public override string ToString()
        {
            return "Preparedness {0} State:{1} ETTR:{2:f3} Reason:'{3}' NVS:{4}".CheckedFormat(ProcessSpec.CombineRecipeNames(ProcessStepSpec), SummaryState, EstimatedTimeToReady.TotalSeconds, Reason, NVS.SafeToStringSML());
        }

        /// <summary>
        /// Returns true if the object has the same contents as the given <paramref name="other"/> one.
        /// </summary>
        public bool Equals(IPreparednessState other)
        {
            var otherAsDownCast = other as IPreparednessState<TProcessSpecType, TProcessStepSpecType>;
            if (otherAsDownCast != null)
                return Equals(otherAsDownCast);

            return (other != null
                    && ProcessSpecRecipeName == other.ProcessSpecRecipeName
                    && ProcessStepSpecRecipeName == other.ProcessStepSpecRecipeName
                    && SummaryState.Equals(other.SummaryState)
                    && SummaryStateTimeStamp == other.SummaryStateTimeStamp
                    && EstimatedTimeToReady == other.EstimatedTimeToReady
                    && Reason == other.Reason
                    && NVS.Equals(other.NVS)
                    );
        }

        /// <summary>
        /// Returns true if the object has the same contents as the given <paramref name="other"/> one.
        /// </summary>
        public bool Equals(IPreparednessState<TProcessSpecType, TProcessStepSpecType> other)
        {
            return (other != null
                    && object.ReferenceEquals(ProcessSpec, other.ProcessSpec)
                    && object.ReferenceEquals(ProcessStepSpec, other.ProcessStepSpec)
                    && SummaryState.Equals(other.SummaryState)
                    && SummaryStateTimeStamp == other.SummaryStateTimeStamp
                    && EstimatedTimeToReady == other.EstimatedTimeToReady
                    && Reason == other.Reason
                    && NVS.Equals(other.NVS)
                    );
        }
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Updates the SummaryState, SummaryStateTimeStamp and Reason in the given <paramref name="preparednessState"/> instance.
        /// SummaryStateTimeStamp is set to QpcTimeStamp.Now if given <paramref name="qpcTimeStamp"/> value is "zero" (aka default(QpcTimeStamp)).
        /// </summary>
        public static PreparednessState<TProcessSpecType, TProcessStepSpecType> SetSummaryState<TProcessSpecType, TProcessStepSpecType>(this PreparednessState<TProcessSpecType, TProcessStepSpecType> preparednessState, QuerySummaryState summaryState, string reason, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
            where TProcessSpecType : IProcessSpec
            where TProcessStepSpecType : IProcessStepSpec
        {
            preparednessState.SummaryState = summaryState;
            preparednessState.SummaryStateTimeStamp = qpcTimeStamp.MapDefaultToNow();
            preparednessState.Reason = reason;

            return preparednessState;
        }

        public static string CombineRecipeNames(this IProcessSpec processSpec, IProcessStepSpec processStepSpec)
        {
            string processSpecRecipeName = (processSpec != null) ? processSpec.RecipeName : null;
            string processStepSpecRecipeName = (processStepSpec != null) ? processStepSpec.StepRecipeName : null;

            return processSpecRecipeName.CombineRecipeNames(processStepSpecRecipeName);
        }

        public static string CombineRecipeNames(this string processSpecRecipeName, string processStepSpecRecipeName)
        {
            if (processSpecRecipeName.IsNeitherNullNorEmpty())
            {
                if (processStepSpecRecipeName.IsNeitherNullNorEmpty())
                    return "{0}:{1}".CheckedFormat(processSpecRecipeName, processStepSpecRecipeName);
                else
                    return processSpecRecipeName;
            }
            else if (processStepSpecRecipeName.IsNeitherNullNorEmpty())
            {
                return processStepSpecRecipeName;
            }
            else
            {
                return "[NoProcessSpecOrProcessStepSpecRecipeName]";
            }
        }
    }

    #endregion

    #region ISubstrateAndProcessTrackerBase, IProcessSpec, IProcessStepSpec, IProcessStepResult, ProcessStepTrackerResultItem

    /// <summary>
    /// This is the interface provided by the SubstrateAndProcessTrackerBase class
    /// <para/>Please note that it cannot be used to construct or initialize a SubstrateTrackerBase instance.
    /// </summary>
    public interface ISubstrateAndProcessTrackerBase<TProcessSpecType, TProcessStepSpecType> : ISubstrateTrackerBase
        where TProcessSpecType : IProcessSpec<TProcessStepSpecType>
        where TProcessStepSpecType : IProcessStepSpec
    {
        TProcessSpecType ProcessSpec { get; }

        void Add(ProcessStepTrackerResultItem<TProcessStepSpecType> trackerResultItem, bool autoAdvanceRemainingStepSpecList = true, bool autoLatchFinalSPS = true);

        IList<ProcessStepTrackerResultItem<TProcessStepSpecType>> TrackerStepResultList { get; }

        IList<TProcessStepSpecType> RemainingStepSpecList { get; }
        TProcessStepSpecType NextStepSpec { get; }
    }

    /// <summary>
    /// Basic generic interface for a top level process specification.
    /// <para/>Contains a RecipeName and RecipeVariables.
    /// </summary>
    public interface IProcessSpec
    {
        string RecipeName { get; }
        INamedValueSet RecipeVariables { get; }
    }

    /// <summary>
    /// Templated extension of the basic IProcessSpec interface that adds the <typeparamref name="TProcessStepSpecType"/> and a read only list of same called Steps.
    /// </summary>
    public interface IProcessSpec<TProcessStepSpecType> : IProcessSpec
        where TProcessStepSpecType : IProcessStepSpec
    {
        ReadOnlyIList<TProcessStepSpecType> Steps { get; }
    }

    /// <summary>
    /// Basic interface for the specification of a process step.
    /// This includes a list of usable location names and a set of step recipe variables.
    /// It is generally updated with its StepNum and ProcessSpec when it is added to the ProcessSpec's Steps using the SetProcessSpecAndStepNum method which must only be used once.
    /// </summary>
    public interface IProcessStepSpec
    {
        IProcessSpec ProcessSpec { get; }
        string StepRecipeName { get; }
        int StepNum { get; }
        ReadOnlyIList<string> UsableLocNameList { get; }
        INamedValueSet StepVariables { get; }

        void SetProcessSpecAndStepNum(IProcessSpec processSpec, int stepNum, string stepRecipeName = null);
    }

    /// <summary>
    /// Interface for results produced by running a process step spec.  Contains a ResultCode and an SPS.
    /// </summary>
    public interface IProcessStepResult
    {
        string ResultCode { get; }
        SubstProcState SPS { get; }
    }

    /// <summary>
    /// This structure is generated and recorded for each completed process step.
    /// </summary>
    public struct ProcessStepTrackerResultItem<TProcessStepSpecType> where TProcessStepSpecType : IProcessStepSpec
    {
        public string LocName { get; set; }
        public TProcessStepSpecType StepSpec { get; set; }
        public IProcessStepResult StepResult { get; set; }

        public override string ToString()
        {
            if (StepSpec != null && StepResult != null && StepResult.ResultCode.IsNullOrEmpty() && (StepResult.SPS == SubstProcState.Processed || StepResult.SPS == SubstProcState.ProcessStepCompleted))
                return "Step {0} completed successfully at loc:{1} [{2}]".CheckedFormat(StepSpec.StepNum, LocName, StepResult.SPS);
            else if (StepSpec != null && StepResult != null)
                return "Step {0} failed at loc:{1} [{2} {3}]".CheckedFormat(StepSpec.StepNum, LocName, StepResult.SPS, StepResult.ResultCode);
            else
                return "Invalid Result Item [LocName:{0} StepSpec:{1} StepResult:{2}]".CheckedFormat(LocName, StepSpec, StepResult);
        }
    }


    #endregion

    #region ISubstrateTrackerBase, ServiceBasicSJSStateChangeTriggerFlags, IJobTrackerLinkage, SubstLocObserverWithTrackerLookup

    /// <summary>
    /// This is the interface provided by the SubstrateTrackerBase class.  
    /// <para/>Please note that it cannot be used to construct or initialize a SubstrateTrackerBase instance.
    /// </summary>
    public interface ISubstrateTrackerBase
    {
        int ServiceDropReasonAssertion();
        int ServiceBasicSJSStateChangeTriggers(ServiceBasicSJSStateChangeTriggerFlags flags, bool updateIfNeeded = false, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp));
        void SetSubstrateJobState(SubstrateJobState sjs, string reason, bool ifNeeded = true);

        E039ObjectID SubstID { get; }
        IE039TableUpdater E039TableUpdater { get; }
        Logging.IBasicLogger Logger { get; }

        E090SubstObserver SubstObserver { get; }
        E090SubstInfo Info { get; }

        /// <summary>
        /// When true (and when supported) this property indicates that the tracked substrate is expected to be in motion, either now, or in the near future.
        /// <para/>this property can be used buy the ServiceBasicSJSStateChangeTriggers method to prevent requesting state changes that are only valid at the current substrate location.
        /// </summary>
        bool IsMotionPending { get; }

        QpcTimeStamp LastServiceStartTimeStamp { get; set; }
        QpcTimeStamp LastUpdateTimeStamp { get; set; }

        bool IsServiceNeeded { get; }
        bool IsUpdateNeeded { get; }

        bool UpdateIfNeeded(bool forceUpdate = false, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool noteStartingService = false);

        bool IsDropRequested { get; }
        string DropRequestReason { get; set; }

        IJobTrackerLinkage JobTrackerLinkage { get; set; }

        SubstrateJobRequestState SubstrateJobRequestState { get; set; }
    }

    /// <summary>
    /// Flags used with ISubstrateTrackerBase.ServiceBasicSJSStateChangeTriggers to control which of its available behaviors shall be enabled for a given use situation.
    /// <para/>None (0x00), EnableInfoTriggeredRules (0x01), EnableWaitingForStartRules (0x02), EnableAutoStart (0x04), EnablePausingRules (0x08),
    /// EnableStoppingRules (0x10), EnableAbortingRules (0x20), EnableRunningRules (0x40), EnableAbortedAtWork(0x80), EnableHeldRules(0x100), EnablePausedRules(0x200), All (0x3ff)
    /// </summary>
    [Flags]
    public enum ServiceBasicSJSStateChangeTriggerFlags : int
    {
        None = 0x00,
        EnableInfoTriggeredRules = 0x01,
        EnableWaitingForStartRules = 0x02,
        EnableAutoStart = 0x04,
        EnablePausingRules = 0x08,
        EnableStoppingRules = 0x10,
        EnableAbortingRules = 0x20,
        EnableRunningRules = 0x40,
        EnableAbortedAtWork = 0x80,
        EnableHeldRules = 0x100,        // now also applies to Stranded
        EnablePausedRules = 0x200,

        All = (EnableInfoTriggeredRules | EnableWaitingForStartRules | EnableAutoStart | EnablePausingRules | EnableStoppingRules | EnableAbortingRules | EnableRunningRules | EnableHeldRules | EnablePausedRules),
    }

    /// <summary>
    /// This interface may be used by a Job Tracker object that would like to be informed when a dependent substrate's observer has been updated.
    /// </summary>
    public interface IJobTrackerLinkage
    {
        /// <summary>
        /// Returns the "identity" of the job that has been linked to this substrate
        /// </summary>
        string ID { get; }

        /// <summary>
        /// Set to indicate that a dependent substrate's observer has been updated, cleared by the related Job Tracker entity once it has processed the change.
        /// </summary>
        bool SubstrateTrackerHasBeenUpdated { get; set; }

        /// <summary>
        /// Returns true if the corresponding job has had its DropRequestReason set to a non-empty value.
        /// </summary>
        bool IsDropRequested { get; }

        /// <summary>
        /// When non-empty, this string indicates the reason why this Job object is requesting that it be removed from the system.
        /// </summary>
        string DropRequestReason { get; }
    }

    /// <summary>
    /// This is a variant of an E090SubstLocObserver that accepts an IDictionary that gives the mapping of the known set of substrate trackers (of type <typeparamref name="TSubstrateAndProcessTrackerType"/>)
    /// and which looks up and obtains the tracker instance, placing it in the Tracker property, any time the underlying E090 SubstLoc's contents change, based on the FullName of the Subst object that is currently in the location.
    /// </summary>
    public class SubstLocObserverWithTrackerLookup<TSubstrateAndProcessTrackerType> 
        : E090SubstLocObserver
    {
        public SubstLocObserverWithTrackerLookup(E039ObjectID substLocID, IDictionary<string, TSubstrateAndProcessTrackerType> fullNameToSubstTrackerDictionaryIn)
            : this(substLocID.GetPublisher(), fullNameToSubstTrackerDictionaryIn)
        { }

        public SubstLocObserverWithTrackerLookup(ISequencedObjectSource<IE039Object, int> objLocPublisher, IDictionary<string, TSubstrateAndProcessTrackerType> fullNameToSubstTrackerDictionaryIn)
            : base(objLocPublisher, alsoObserveContents: true)
        {
            fullNameToSubstrTrackerDictionary = fullNameToSubstTrackerDictionaryIn;
        }

        protected override void UpdateContainsObject(IE039Object obj)
        {
            base.UpdateContainsObject(obj);

            Tracker = fullNameToSubstrTrackerDictionary.SafeTryGetValue(ContainsSubstInfo.ObjID.FullName);
        }

        public TSubstrateAndProcessTrackerType Tracker { get; private set; }

        protected IDictionary<string, TSubstrateAndProcessTrackerType> fullNameToSubstrTrackerDictionary;
    }

    #endregion

    #region SubstrateAndProcessTrackerBase, ProcessSpecBase, ProcessStepSpecBase, ProcessStepResultBase

    /// <summary>
    /// This is a useful base class that allows the user to combine Substrate Tracking and Substrate Process Tracking.
    /// </summary>
    public class SubstrateAndProcessTrackerBase<TProcessSpecType, TProcessStepSpecType> 
        : SubstrateTrackerBase, ISubstrateAndProcessTrackerBase<TProcessSpecType, TProcessStepSpecType>
        where TProcessSpecType : IProcessSpec<TProcessStepSpecType>
        where TProcessStepSpecType : IProcessStepSpec
    {
        public virtual void Setup(IE039TableUpdater e039TableUpdater, E039ObjectID substID, Logging.IBasicLogger logger, TProcessSpecType processSpec)
        {
            base.Setup(e039TableUpdater, substID, logger);
            ProcessSpec = processSpec;

            remainingStepSpecList = new List<TProcessStepSpecType>(ProcessSpec.Steps);
        }

        public TProcessSpecType ProcessSpec { get; private set; }

        public virtual void Add(ProcessStepTrackerResultItem<TProcessStepSpecType> trackerResultItem, bool autoAdvanceRemainingStepSpecList = true, bool autoLatchFinalSPS = true)
        {
            trackerStepResultList.Add(trackerResultItem);

            if (autoAdvanceRemainingStepSpecList && remainingStepSpecList.Count > 0)
                remainingStepSpecList.RemoveAt(0);

            if (!autoAdvanceRemainingStepSpecList)
            { }
            else if (NextStepSpec != null)
            {
                Logger.Debug.Emit("{0} step {1}: Next Step: [{2}]", SubstID.FullName, trackerResultItem, NextStepSpec);
            }
            else
            {
                Logger.Debug.Emit("{0} step {1}: No more steps. [InferredSPS:{2}]", SubstID.FullName, trackerResultItem, SubstObserver.Info.InferredSPS);

                var finalSPS = GetFinalSPS();

                if (autoLatchFinalSPS)
                {
                    Logger.Debug.Emit("{0}: Latching final SPS {1} at loc {2}", SubstID.FullName, finalSPS, Info.LocID);

                    E039TableUpdater.SetSubstProcState(SubstObserver, GetFinalSPS());
                }
                else
                {
                    Logger.Debug.Emit("{0}: Not latching final SPS {1} at loc {2} [yet]", SubstID.FullName, finalSPS, Info.LocID);
                }
            }
        }

        protected virtual SubstProcState GetFinalSPS()
        {
            var finalSPS = trackerStepResultList.Aggregate(SubstObserver.Info.InferredSPS, (a, b) => a.MergeWith(b.StepResult.SPS));

            return (finalSPS.IsProcessStepComplete() ? SubstProcState.Processed : finalSPS);
        }

        public virtual IList<ProcessStepTrackerResultItem<TProcessStepSpecType>> TrackerStepResultList { get { return trackerStepResultList; } }

        public virtual IList<TProcessStepSpecType> RemainingStepSpecList { get { return remainingStepSpecList; } }
        public virtual TProcessStepSpecType NextStepSpec { get { return RemainingStepSpecList.FirstOrDefault(); } }

        protected List<TProcessStepSpecType> remainingStepSpecList;
        protected List<ProcessStepTrackerResultItem<TProcessStepSpecType>> trackerStepResultList = new List<ProcessStepTrackerResultItem<TProcessStepSpecType>>();
    }

    /// <summary>
    /// Useful and/or base class to be used to contain a specified process includings one or more steps.
    /// </summary>
    public class ProcessSpecBase<TProcessStepSpecType> : IProcessSpec<TProcessStepSpecType>
        where TProcessStepSpecType : IProcessStepSpec
    {
        public ProcessSpecBase(string recipeName, INamedValueSet recipeVariables = null, IEnumerable<TProcessStepSpecType> stepSpecSet = null)
        {
            RecipeName = recipeName.MapNullToEmpty();
            RecipeVariables = recipeVariables.ConvertToReadOnly();
            Steps = new ReadOnlyIList<TProcessStepSpecType>(stepSpecSet.SafeToArray().Select((stepSpec, idx) => KVP.Create(idx + 1, stepSpec)).DoForEach(kvp => kvp.Value.SetProcessSpecAndStepNum(this, kvp.Key, "{0}.Step{1}".CheckedFormat(this.RecipeName, kvp.Key))).Select(kvp => kvp.Value));
        }

        public string RecipeName { get; private set; }
        public INamedValueSet RecipeVariables { get; private set; }
        public ReadOnlyIList<TProcessStepSpecType> Steps { get; private set; }

        public override string ToString()
        {
            return "ProcessSpec Rcp:'{0}' RcpVars:{1} Steps:{2}".CheckedFormat(RecipeName, RecipeVariables.SafeToStringSML(), Steps.SafeCount());
        }
    }

    /// <summary>
    /// Useful and/or base class to be used as a process step spec implementation class.
    /// </summary>
    public class ProcessStepSpecBase : IProcessStepSpec
    {
        public ProcessStepSpecBase(IEnumerable<string> usableLocSet, INamedValueSet stepVariables = null, string stepRecipeName = null)
            : this(null, 0, usableLocSet, stepVariables, stepRecipeName)
        { }

        public ProcessStepSpecBase(int stepNum, IEnumerable<string> usableLocSet, INamedValueSet stepVariables = null, string stepRecipeName = null)
            : this(null, stepNum, usableLocSet, stepVariables, stepRecipeName)
        { }

        public ProcessStepSpecBase(IProcessSpec processSpec, int stepNum, IEnumerable<string> usableLocSet, INamedValueSet stepVariables = null, string stepRecipeName = null)
        {
            ProcessSpec = processSpec;
            StepRecipeName = stepRecipeName.MapNullToEmpty();
            StepNum = stepNum;
            UsableLocNameList = new ReadOnlyIList<string>(usableLocSet);
            StepVariables = stepVariables.ConvertToReadOnly();
        }

        public IProcessSpec ProcessSpec { get; private set; }
        public string StepRecipeName { get; private set; }
        public int StepNum { get; private set; }
        public ReadOnlyIList<string> UsableLocNameList { get; private set; }
        public INamedValueSet StepVariables { get; private set; }

        public void SetProcessSpecAndStepNum(IProcessSpec processSpec, int stepNum, string defaultStepRecipeName = null)
        {
            if (ProcessSpec != null || StepNum != 0)
                new System.InvalidOperationException("This method is not valid once a non-null ProcessSpec, or a non-zero StepNum has been assigned to this step").Throw();

            ProcessSpec = processSpec;
            StepNum = stepNum;
            if (defaultStepRecipeName.IsNeitherNullNorEmpty() && StepRecipeName.IsNullOrEmpty())
                StepRecipeName = defaultStepRecipeName;
        }

        public override string ToString()
        {
            var processRcpNameStr = ((ProcessSpec != null) ? " ProcRcp:'{0}'".CheckedFormat(ProcessSpec.RecipeName) : " [NullProcessSpec]");
            var stepRcpNameStr = (StepRecipeName.IsNeitherNullNorEmpty() ? " StepRcp:'{0}'".CheckedFormat(StepRecipeName) : "");

            return "ProcessStepSpec{0}{1} StepNum:{2} UsableLocNameList:[{3}] StepVars:{4}".CheckedFormat(processRcpNameStr, stepRcpNameStr, StepNum, String.Join(", ", UsableLocNameList), StepVariables.SafeToStringSML());
        }
    }

    /// <summary>
    /// Useful and/or base class to be used as a process step result implementation class.
    /// </summary>
    public class ProcessStepResultBase : IProcessStepResult
    {
        public ProcessStepResultBase(string resultCode = "", SubstProcState sps = SubstProcState.Undefined, SubstProcState fallbackFailedSPS = SubstProcState.Rejected, SubstProcState defaultSucceededSPS = SubstProcState.ProcessStepCompleted)
        {
            ResultCode = resultCode.MapNullToEmpty();
            SPS = (sps != SubstProcState.Undefined) ? sps : (ResultCode.IsNullOrEmpty() ? defaultSucceededSPS : fallbackFailedSPS);
        }

        public string ResultCode { get; private set; }
        public SubstProcState SPS { get; private set; }
    }

    #endregion

    #region SubstrateTrackerBase

    /// <summary>
    /// This is the base substrate tracking class for use with E090 Substrate Scheduling.
    /// </summary>
    public class SubstrateTrackerBase : ISubstrateTrackerBase
    {
        /// <summary>
        /// Debugging and logging helper method
        /// </summary>
        public override string ToString()
        {
            string inMotionStr = (IsMotionPending ? " MotionPending" : "");
            string dropReqStr = (IsDropRequested ? " DropReq:{0}".CheckedFormat(DropRequestReason) : "");

            if (SubstObserver != null)
                return "ST: {0}{1}{2}".CheckedFormat(SubstObserver, inMotionStr, dropReqStr);
            else
                return "ST: No substrate object found for given id '{0}'".CheckedFormat(SubstID.FullName);
        }

        public virtual void Setup(IE039TableUpdater e039TableUpdater, E039ObjectID substID, Logging.IBasicLogger logger, bool setSJSToWaitingForStart = true)
        {
            SubstID = substID.MapNullToEmpty();
            E039TableUpdater = e039TableUpdater;
            Logger = logger;

            var substPublisher = E039TableUpdater.GetPublisher(SubstID);

            if (substPublisher != null)
                SubstObserver = new E090SubstObserver(substPublisher);
            else
                new SubstrateSchedulingException("No substrate object found for given id '{0}'".CheckedFormat(SubstID.FullName)).Throw();

            if (setSJSToWaitingForStart)
                SetSubstrateJobState(SubstrateJobState.WaitingForStart, Fcns.CurrentMethodName);
        }

        public virtual int ServiceDropReasonAssertion()
        {
            int didSomethingCount = 0;

            if (DropRequestReason.IsNullOrEmpty())
            {
                string nextDropReasonRequest = String.Empty;

                if (SubstObserver.Info.SPS.IsProcessingComplete() && SubstObserver.Info.STS != SubstState.AtWork)
                {
                    if (JobTrackerLinkage == null)
                        nextDropReasonRequest = "Substrate processing done and no Job was linked to it";
                    else if (JobTrackerLinkage.IsDropRequested)
                        nextDropReasonRequest = "Substrate processing done and linked Job is requesting to be dropped [{0}]".CheckedFormat(JobTrackerLinkage.DropRequestReason);
                }
                else if (SubstObserver.Info.IsFinal)              // this is the normal substrate removed case
                {
                    if (JobTrackerLinkage == null)
                        nextDropReasonRequest = "Substrate Object has been removed and no Job was linked to it";
                    else if (JobTrackerLinkage.IsDropRequested)
                        nextDropReasonRequest = "Substrate Object has been removed and linked Job is requesting to be dropped [{0}]".CheckedFormat(JobTrackerLinkage.DropRequestReason);
                }
                else if (SubstObserver.Object.IsFinalOrNull())      // this is not an expected case
                {
                    nextDropReasonRequest = "Substrate Object has been removed unexpectedly";
                }
                else if (SubstObserver.Object.IsEmpty)      // this is not an expected case
                {
                    nextDropReasonRequest = "Substrate Object has been emptied unexpectedly";
                }

                if (DropRequestReason != nextDropReasonRequest)
                {
                    DropRequestReason = nextDropReasonRequest;      // setter logs changes in reason

                    didSomethingCount++;
                }
            }

            return didSomethingCount;
        }

        public virtual int ServiceBasicSJSStateChangeTriggers(ServiceBasicSJSStateChangeTriggerFlags flags, bool updateIfNeeded = false, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            int didSomethingCount = 0;

            if (updateIfNeeded)
                didSomethingCount += UpdateIfNeeded(qpcTimeStamp: qpcTimeStamp).MapToInt();

            bool isFinalOrNull = SubstObserver.Object.IsFinalOrNull();

            bool enableInfoTriggeredRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableInfoTriggeredRules) != 0);
            bool enableWaitingForStartRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableWaitingForStartRules) != 0);
            bool enableAutoStart = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableAutoStart) != 0);
            bool enablePausingRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnablePausingRules) != 0);
            bool enableStoppingRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableStoppingRules) != 0);
            bool enableAbortingRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableAbortingRules) != 0);
            bool enableRunningRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableRunningRules) != 0);
            bool enableHeldOrStrandedRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableHeldRules) != 0);
            bool enablePausedRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnablePausedRules) != 0);

            var stInfo = Info;
            bool isUpdateNeededOrIsMotionPending = IsUpdateNeeded || IsMotionPending;
            bool stsIsAtSource = stInfo.STS.IsAtSource() && !isUpdateNeededOrIsMotionPending;
            bool stsIsAtDestination = stInfo.STS.IsAtDestination() && !isUpdateNeededOrIsMotionPending;        // once a substrate is "at destination" it is not supposed to be moved but service actions might move it
            bool stsIsAtWork = stInfo.STS.IsAtWork() && !isUpdateNeededOrIsMotionPending;
            bool isAtSrcLoc = stInfo.LocID == stInfo.LinkToSrc.ToID.Name && !isUpdateNeededOrIsMotionPending;
            bool isAtDestLoc = stInfo.LocID == stInfo.LinkToDest.ToID.Name && !isUpdateNeededOrIsMotionPending;

            SubstProcState sps = stInfo.SPS;

            SubstrateJobState nextSJS = SubstrateJobState.Initial;
            string reason = null;

            if (enableInfoTriggeredRules)
            {
                if (sps == SubstProcState.Lost)
                {
                    nextSJS = SubstrateJobState.Lost;
                    reason = "Substrate has been marked Lost";
                }
                else if (stInfo.SJRS == SubstrateJobRequestState.Return)
                {
                    if (stsIsAtSource || stsIsAtDestination || isAtSrcLoc || isAtDestLoc)
                        nextSJS = SubstrateJobState.Returned;
                    else
                        nextSJS = SubstrateJobState.Returning;
                }
                else if (stsIsAtSource)
                {
                    switch (sps)
                    {
                        case SubstProcState.Skipped: nextSJS = SubstrateJobState.Skipped; break;
                        default: break;
                    }
                }
                else if (stsIsAtDestination)
                {
                    switch (sps)
                    {
                        case SubstProcState.Processed: nextSJS = SubstrateJobState.Processed; break;
                        case SubstProcState.Rejected: nextSJS = SubstrateJobState.Rejected; break;
                        case SubstProcState.Skipped: nextSJS = SubstrateJobState.Skipped; break;
                        case SubstProcState.Stopped: nextSJS = SubstrateJobState.Stopped; break;
                        case SubstProcState.Aborted: nextSJS = SubstrateJobState.Aborted; break;
                        default: break;
                    }
                }
                else if (isFinalOrNull)
                {
                    // nextSJS = SubstrateJobState.Removed;     // we cannot set the next SJS to anything since the substrate has already been removed.
                    reason = "Substrate has been removed/deleted unexpectedly";
                }

                if (nextSJS != SubstrateJobState.Initial && reason.IsNullOrEmpty())
                {
                    reason = "Substrate reached a final state processing/transport state";
                }
            }

            if (enableAbortingRules && !isFinalOrNull)
            {
                if (nextSJS == SubstrateJobState.Initial && stsIsAtWork && sps == SubstProcState.Aborted && flags.IsSet(ServiceBasicSJSStateChangeTriggerFlags.EnableAbortedAtWork))
                {
                    nextSJS = E090.SubstrateJobState.Aborted;
                    reason = "Substrate reached Aborted state AtWork";
                }
            }

            if (nextSJS == SubstrateJobState.Initial && !isFinalOrNull)
            {
                switch (stInfo.SJS)
                {
                    case SubstrateJobState.WaitingForStart:
                        if (enableWaitingForStartRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Run && enableAutoStart)
                            {
                                nextSJS = SubstrateJobState.Running;
                                reason = "Run requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Pause)
                            {
                                nextSJS = SubstrateJobState.Pausing;
                                reason = "Pause requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Stop)
                            {
                                nextSJS = SubstrateJobState.Stopping;
                                reason = "Stop requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                        }
                        break;

                    case SubstrateJobState.Pausing:
                        if (enablePausingRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Stop)
                            {
                                nextSJS = SubstrateJobState.Stopping;
                                reason = "Stop requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                            else if (stsIsAtSource)
                            {
                                nextSJS = SubstrateJobState.Paused;
                                reason = "Paused condition reached";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Run)
                            {
                                nextSJS = (stsIsAtWork ? SubstrateJobState.Running : SubstrateJobState.WaitingForStart);
                                reason = "Resume requested";
                            }
                        }
                        break;

                    case SubstrateJobState.Paused:
                        if (enablePausedRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Stop)
                            {
                                nextSJS = SubstrateJobState.Stopping;
                                reason = "Stop requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Run)
                            {
                                nextSJS = (stsIsAtWork ? SubstrateJobState.Running : SubstrateJobState.WaitingForStart);
                                reason = "Resume requested";
                            }
                        }
                        break;

                    case SubstrateJobState.Stopping:
                        if (enableStoppingRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                            else if (stsIsAtSource)
                            {
                                nextSJS = SubstrateJobState.Skipped;
                                reason = "Stop completed (skipped)";
                            }
                        }
                        break;

                    case SubstrateJobState.Aborting:
                        if (enableAbortingRules)
                        {
                            if (stsIsAtSource)
                            {
                                nextSJS = SubstrateJobState.Skipped;
                                reason = "Abort completed (skipped)";
                            }
                        }
                        break;

                    case SubstrateJobState.Running:
                        if (enableRunningRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Pause)
                            {
                                nextSJS = SubstrateJobState.Pausing;
                                reason = "Pause requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Stop)
                            {
                                nextSJS = SubstrateJobState.Stopping;
                                reason = "Stop requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                        }
                        break;

                    case E090.SubstrateJobState.Held:
                    case SubstrateJobState.Stranded:
                        if (enableHeldOrStrandedRules)
                        {
                            // cover cases where a held NeedsProcessing wafer flagged as Stop or Abort ends up AtSource, typically through manual service actions.
                            if (stsIsAtSource && sps.IsNeedsProcessing() && (SubstrateJobRequestState == E090.SubstrateJobRequestState.Stop || SubstrateJobRequestState == E090.SubstrateJobRequestState.Abort))
                            {
                                nextSJS = E090.SubstrateJobState.Skipped;
                                reason = "{0} requested (held)".CheckedFormat(SubstrateJobRequestState);
                            }

                            // cover cases where a held InProcess wafer flagged as Abort ends up AtDestination, typically through manual service actions.
                            if (isAtDestLoc && sps.IsInProcess() && SubstrateJobRequestState == E090.SubstrateJobRequestState.Abort)
                            {
                                nextSJS = E090.SubstrateJobState.Aborted;
                                reason = "Abort requested (held AtDestLoc)";
                            }
                        }
                        break;

                    case SubstrateJobState.Processed:
                    case SubstrateJobState.Stopped:
                    case SubstrateJobState.Aborted:
                    default:
                        break;
                }
            }

            // perform common Skipped detection and handling on transition to nextSJS for Stopping and Aborting cases
            if ((nextSJS == SubstrateJobState.Stopping || nextSJS == SubstrateJobState.Aborting) && stsIsAtSource && !isFinalOrNull)
            {
                nextSJS = SubstrateJobState.Skipped;
                reason = "{0} (skip)".CheckedFormat(reason);
            }

            if (nextSJS != SubstrateJobState.Initial && stInfo.SJS != nextSJS)
            {
                SetSubstrateJobState(nextSJS, "{0} [{1} {2} {3} {4}]".CheckedFormat(reason, sps, stInfo.STS, stInfo.LocID, SubstrateJobRequestState));
                didSomethingCount++;

                if (updateIfNeeded)
                    didSomethingCount += UpdateIfNeeded(qpcTimeStamp: qpcTimeStamp).MapToInt();
            }

            return didSomethingCount;
        }

        private const E090StateUpdateBehavior finalSPSUpdateBehavior = (E090StateUpdateBehavior.StandardSPSUpdate | E090StateUpdateBehavior.BasicSPSLists);
        private const E090StateUpdateBehavior pendingSPSUpdateBehavior = (E090StateUpdateBehavior.PendingSPSUpdate | E090StateUpdateBehavior.BasicSPSLists);

        public virtual void SetSubstrateJobState(SubstrateJobState sjs, string reason, bool ifNeeded = true)
        {
            var entrySJS = Info.SJS;

            if (ifNeeded && entrySJS == sjs)
            {
                Logger.Debug.Emit("{0}({1}, sjs:{2}, reason:'{3}'): Not needed", Fcns.CurrentMethodName, SubstID.FullName, sjs, reason);
                return;
            }

            if (SubstObserver.Object.IsFinalOrNull())
            {
                Logger.Debug.Emit("{0}({1}, sjs:{2}, reason:'{3}'): cannot be performed now - substrate has already been removed", Fcns.CurrentMethodName, SubstID.FullName, sjs, reason);
                return;
            }

            Logger.Trace.Emit("{0}({1}, sjs:{2}, reason:'{3}'{4})  current sjs:{5}", Fcns.CurrentMethodName, SubstID.FullName, sjs, reason, ifNeeded ? ", IfNeeded" : "", entrySJS);

            List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();
            updateItemList.Add(new E039UpdateItem.SetAttributes(SubstID, new NamedValueSet() { { "SJS", sjs } }));

            switch (sjs)
            {
                case SubstrateJobState.Processed:
                    if (Info.SPS.IsProcessingComplete())
                    { } // nothing more to do
                    else if (Info.InferredSPS.IsProcessingComplete())
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: Info.InferredSPS, updateBehavior: finalSPSUpdateBehavior);
                    else
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Processed, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Stopping:
                    break;

                case SubstrateJobState.Stopped:
                    if (Info.SPS.IsProcessingComplete())
                    { } // nothing more to do
                    else if (Info.InferredSPS.IsProcessingComplete())
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: Info.InferredSPS, updateBehavior: finalSPSUpdateBehavior);
                    else
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Stopped, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Aborting:
                    if (Info.InferredSPS != SubstProcState.Aborted)
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Aborted, updateBehavior: pendingSPSUpdateBehavior & ~E090StateUpdateBehavior.AutoUpdateSTS);      // this transition does not change STS in order to avoid race conditions when substrate motion has already been committed do but has not actually been recorded.
                    break;

                case SubstrateJobState.Aborted:
                    if (Info.SPS.IsProcessingComplete())
                    { } // nothing more to do
                    else if (Info.InferredSPS.IsProcessingComplete())
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: Info.InferredSPS, updateBehavior: finalSPSUpdateBehavior);
                    else
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Aborted, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Skipped:
                    if (Info.SPS.IsProcessingComplete())
                    { } // nothing more to do
                    if (Info.InferredSPS.IsProcessingComplete())
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: Info.InferredSPS, updateBehavior: finalSPSUpdateBehavior);
                    else
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Skipped, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Lost:
                    updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Lost, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Returned:
                case SubstrateJobState.Returning:
                case SubstrateJobState.Held:
                case SubstrateJobState.Stranded:
                case SubstrateJobState.RoutingAlarm:
                default:
                    break;
            }

            if (updateItemList.Count > 0)
            {
                // check settings to see if we need to explicitly add an external sync item
                if (E090.Settings.GetUseExternalSync(checkNoteSubstrateMovedAdditions: false, checkSetSubstProcStateAdditions: true, checkGenerateUpdateItemAdditions: true))
                    updateItemList.Add(syncExternal);

                E039TableUpdater.Update(updateItemList.ToArray()).Run();
            }

            Logger.Debug.Emit("{0}: SJS changed to {1} [from:{2} reason:{3}]", SubstID.FullName, sjs, entrySJS, reason);
        }

        public E039ObjectID SubstID { get; private set; }
        public IE039TableUpdater E039TableUpdater { get; private set; }
        public Logging.IBasicLogger Logger { get; private set; }

        public E090SubstObserver SubstObserver { get; private set; }
        public E090SubstInfo Info { get { return SubstObserver.Info; } }

        /// <summary>
        /// When overriden in a derived class, this property can be used buy the ServiceBasicSJSStateChangeTriggers method to prevent requesting state changes that are only valid at the current substrate location.
        /// </summary>
        public virtual bool IsMotionPending { get { return false; } }

        public QpcTimeStamp LastServiceStartTimeStamp { get; set; }
        public int LastServiceStartSeqNum { get; set; }

        public QpcTimeStamp LastUpdateTimeStamp { get; set; }
        public int LastUpdatedSeqNum { get; set; }

        public virtual bool IsServiceNeeded 
        {
            get { return IsUpdateNeeded || LastServiceStartSeqNum != LastUpdatedSeqNum || LastServiceStartSeqNum != SubstObserver.VolatileSequenceNumber; }
        }

        public virtual bool IsUpdateNeeded 
        { 
            get { return SubstObserver.IsUpdateNeeded; } 
            set { SubstObserver.IsUpdateNeeded = value; } 
        }

        public virtual bool UpdateIfNeeded(bool forceUpdate = false, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool noteStartingService = false)
        {
            if (!IsUpdateNeeded && !forceUpdate && !noteStartingService)
                return false;

            var capturedSeqNum = SubstObserver.VolatileSequenceNumber;

            bool changed = SubstObserver.Update(forceUpdate: forceUpdate);

            if (changed)
            {
                if (JobTrackerLinkage != null && !JobTrackerLinkage.SubstrateTrackerHasBeenUpdated)
                    JobTrackerLinkage.SubstrateTrackerHasBeenUpdated = true;

                LastUpdateTimeStamp = (qpcTimeStamp = qpcTimeStamp.MapDefaultToNow());
                LastUpdatedSeqNum = capturedSeqNum;
            }

            if (noteStartingService)
            {
                LastServiceStartSeqNum = LastUpdatedSeqNum;
                LastServiceStartTimeStamp = (qpcTimeStamp = qpcTimeStamp.MapDefaultToNow());
            }

            return changed;
        }

        public bool IsDropRequested { get { return !DropRequestReason.IsNullOrEmpty(); } }
        public string DropRequestReason
        {
            get { return _dropReasonRequest; }
            set
            {
                var entryDropReasonRequest = _dropReasonRequest;

                _dropReasonRequest = value.MapNullToEmpty();

                IsUpdateNeeded = true;

                if (_dropReasonRequest != entryDropReasonRequest)
                {
                    if (entryDropReasonRequest.IsNullOrEmpty())
                        Logger.Trace.Emit("{0}: Drop reason requested as '{1}' [{2}]", SubstID.FullName, _dropReasonRequest, SubstObserver.Info.ToString(includeSubstID: false));
                    else
                        Logger.Trace.Emit("{0}: Drop reason request changed to '{1}' [from:'{2}', {3}]", SubstID.FullName, _dropReasonRequest, entryDropReasonRequest, SubstObserver.Info.ToString(includeSubstID: false));
                }
            }
        }
        private string _dropReasonRequest = String.Empty;

        public IJobTrackerLinkage JobTrackerLinkage
        {
            get { return _jobTrackerLinkage; }
            set
            {
                var entryLinkageID = (_jobTrackerLinkage != null) ? _jobTrackerLinkage.ID : null;

                _jobTrackerLinkage = value;
                var newLinkageID = (_jobTrackerLinkage != null) ? _jobTrackerLinkage.ID : null;

                IsUpdateNeeded = true;

                if (entryLinkageID != newLinkageID)
                {
                    if (!newLinkageID.IsNullOrEmpty() && entryLinkageID.IsNullOrEmpty())
                        Logger.Trace.Emit("{0}: Linkage added to job id:{1}", SubstID.FullName, newLinkageID);
                    else if (newLinkageID.IsNullOrEmpty() && !entryLinkageID.IsNullOrEmpty())
                        Logger.Trace.Emit("{0}: Linkage removed from job id:{1}", SubstID.FullName, entryLinkageID);
                    else
                        Logger.Trace.Emit("{0}: Linkage changed to job id:{1} [from:{2}]", SubstID.FullName, newLinkageID, entryLinkageID);
                }
            }
        }
        private IJobTrackerLinkage _jobTrackerLinkage = null;

        public SubstrateJobRequestState SubstrateJobRequestState
        {
            get { return _substrateJobRequestState; }
            set
            {
                if (_substrateJobRequestState != value)
                {
                    _substrateJobRequestState = value;

                    E039TableUpdater.Update(new E039UpdateItem.SetAttributes(SubstID, new NamedValueSet() { { "SJRS", value } })).Run();
                }
            }
        }
        private SubstrateJobRequestState _substrateJobRequestState;

        protected static readonly E039UpdateItem.SyncExternal syncExternal = new E039UpdateItem.SyncExternal();
    }

    #endregion

    #region SubstrateStateTally

    /// <summary>
    /// This object type is used to count up the states of a set of one or more SubstrateTrackerBase objects to give a quick overview of the distribution of such a set of subsrates.
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public class SubstrateStateTally : ICopyable<SubstrateStateTally>, IEquatable<SubstrateStateTally>
    {
        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int total;

        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int stsAtSource, stsAtWork, stsAtDestination, stsOther, stsLostAnywhere, stsRemovedAnywhere;

        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int spsNeedsProcessing;

        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int spsInProcess;
        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int spsProcessed, spsStopped, spsRejected, spsAborted, spsSkipped, spsLost;
        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int spsProcessStepCompleted, spsOther;

        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int sjsWaitingForStart, sjsRunning, sjsProcessed, sjsRejected, sjsSkipped, sjsPausing, sjsPaused, sjsStopping, sjsStopped, sjsAborting, sjsAborted, sjsLost, sjsReturning, sjsReturned, sjsHeld, sjsRoutingAlarm, sjsRemoved, sjsStranded, sjsOther;

        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int sjsAbortedAtDestination;

        [DataMember(IsRequired = false, EmitDefaultValue = false)] public int motionPendingCount;

        public int stsLostOrRemovedAnywhere { get { return stsLostAnywhere + stsRemovedAnywhere; } }

        public bool Equals(SubstrateStateTally other)
        {
            return (other != null
                    && total == other.total
                    && stsAtSource == other.stsAtSource
                    && stsAtWork == other.stsAtWork
                    && stsAtDestination == other.stsAtDestination
                    && stsOther == other.stsOther
                    && stsLostAnywhere == other.stsLostAnywhere
                    && stsRemovedAnywhere == other.stsRemovedAnywhere
                    && spsNeedsProcessing == other.spsNeedsProcessing
                    && spsInProcess == other.spsInProcess
                    && spsProcessed == other.spsProcessed
                    && spsStopped == other.spsStopped
                    && spsRejected == other.spsRejected
                    && spsAborted == other.spsAborted
                    && spsSkipped == other.spsSkipped
                    && spsLost == other.spsLost
                    && spsProcessStepCompleted == other.spsProcessStepCompleted
                    && spsOther == other.spsOther
                    && sjsWaitingForStart == other.sjsWaitingForStart
                    && sjsRunning == other.sjsRunning
                    && sjsProcessed == other.sjsProcessed
                    && sjsRejected == other.sjsRejected
                    && sjsSkipped == other.sjsSkipped
                    && sjsPausing == other.sjsPausing
                    && sjsPaused == other.sjsPaused
                    && sjsStopping == other.sjsStopping
                    && sjsStopped == other.sjsStopped
                    && sjsAborting == other.sjsAborting
                    && sjsAborted == other.sjsAborted
                    && sjsLost == other.sjsLost
                    && sjsReturning == other.sjsReturning
                    && sjsReturned == other.sjsReturned
                    && sjsHeld == other.sjsHeld
                    && sjsRoutingAlarm == other.sjsRoutingAlarm
                    && sjsRemoved == other.sjsRemoved
                    && sjsStranded == other.sjsStranded
                    && sjsOther == other.sjsOther
                    && sjsAbortedAtDestination == other.sjsAbortedAtDestination
                    && motionPendingCount == other.motionPendingCount
                    );
        }

        public void Clear()
        {
            total = 0;
            stsAtSource = stsAtWork = stsAtDestination = stsOther = stsLostAnywhere = stsRemovedAnywhere = 0;

            spsNeedsProcessing = 0;

            spsInProcess = 0;
            spsProcessed = spsStopped = spsRejected = spsAborted = spsSkipped = spsLost = 0;
            spsProcessStepCompleted = spsOther = 0;

            sjsWaitingForStart = sjsRunning = sjsProcessed = sjsRejected = sjsSkipped = sjsPausing = sjsPaused = sjsStopping = sjsStopped = sjsAborting = sjsAborted = sjsLost = sjsReturning = sjsReturned = sjsHeld = sjsRoutingAlarm = sjsRemoved = sjsStranded = sjsOther = 0;
            sjsAbortedAtDestination = 0;

            motionPendingCount = 0;
        }

        public void Add(SubstrateTrackerBase st)
        {
            Add(st.SubstObserver.Info);
            if (st.IsMotionPending)
                motionPendingCount++;
        }

        public void Add(E090SubstInfo info)
        {
            Add(info, info.SJS);
        }

        public void Add(E090SubstInfo info, SubstrateJobState sjs)
        {
            total++;

            var sts = info.STS;
            var inferredSPS = info.InferredSPS;
            var inferredSJS = sjs;

            if (info.SPS == SubstProcState.Lost)
            {
                stsLostAnywhere++;
                inferredSJS = SubstrateJobState.Lost;
            }
            else if (info.IsFinal)
            {
                stsRemovedAnywhere++;
                inferredSPS = SubstProcState.Lost;
                inferredSJS = SubstrateJobState.Removed;
            }
            else
            {
                switch (sts)
                {
                    case SubstState.AtSource: stsAtSource++; break;
                    case SubstState.AtWork: stsAtWork++; break;
                    case SubstState.AtDestination: stsAtDestination++; break;
                    default: stsOther++; break;
                }
            }

            switch (inferredSPS)
            {
                case SubstProcState.NeedsProcessing: spsNeedsProcessing++; break;
                case SubstProcState.InProcess: spsInProcess++; break;
                case SubstProcState.Processed: spsProcessed++; break;
                case SubstProcState.Stopped: spsStopped++; break;
                case SubstProcState.Rejected: spsRejected++; break;
                case SubstProcState.Aborted: spsAborted++; break;
                case SubstProcState.Skipped: spsSkipped++; break;
                case SubstProcState.Lost: spsLost++; break;
                case SubstProcState.ProcessStepCompleted: spsProcessStepCompleted++; break;
                default: spsOther++; break;
            }

            switch (inferredSJS)
            {
                case SubstrateJobState.WaitingForStart: sjsWaitingForStart++; break;
                case SubstrateJobState.Running: sjsRunning++; break;
                case SubstrateJobState.Processed: sjsProcessed++; break;
                case SubstrateJobState.Rejected: sjsRejected++; break;
                case SubstrateJobState.Skipped: sjsSkipped++; break;
                case SubstrateJobState.Pausing: sjsPausing++; break;
                case SubstrateJobState.Paused: sjsPaused++; break;
                case SubstrateJobState.Stopping: sjsStopping++; break;
                case SubstrateJobState.Stopped: sjsStopped++; break;
                case SubstrateJobState.Aborting: sjsAborting++; break;
                case SubstrateJobState.Aborted: sjsAborted++; if (sts == SubstState.AtDestination) sjsAbortedAtDestination++; break;
                case SubstrateJobState.Lost: sjsLost++; break;
                case SubstrateJobState.Returning: sjsReturning++; break;
                case SubstrateJobState.Returned: sjsReturned++; break;
                case SubstrateJobState.Held: sjsHeld++; break;
                case SubstrateJobState.RoutingAlarm: sjsRoutingAlarm++; break;
                case SubstrateJobState.Removed: sjsRemoved++; break;
                case SubstrateJobState.Stranded: sjsStranded++; break;
                default: sjsOther++; break;
            }
        }

        private static string CustomToString(INamedValueSet nvs, string emptyString = "[Empty]")
        {
            StringBuilder sb = new StringBuilder();

            foreach (var nv in nvs)
            {
                int count = nv.VC.GetValueI4(rethrow: false);

                if (count > 0)
                {
                    if (sb.Length != 0)
                        sb.Append(" ");

                    sb.CheckedAppendFormat("{0}:{1}", nv.Name, count);
                }
            }

            return sb.ToString().MapNullOrEmptyTo(emptyString);
        }

        private NamedValueSet GetSTSNVS()
        {
            return new NamedValueSet()
            {
                { "AtSource", stsAtSource },
                { "AtWork", stsAtWork },
                { "AtDestination", stsAtDestination },
                { "Lost", stsLostAnywhere },
                { "Removed", stsRemovedAnywhere },
                { "Other", stsOther },
            };
        }

        private NamedValueSet GetSPSNVS()
        {
            return new NamedValueSet()
            {
                { "NeedsProcessing", spsNeedsProcessing },
                { "InProcess", spsInProcess },
                { "ProcessStepCompleted", spsProcessStepCompleted },
                { "Processed", spsProcessed },
                { "Aborted", spsAborted },
                { "Stopped", spsStopped },
                { "Rejected", spsRejected },
                { "Lost", spsLost },
                { "Skipped", spsSkipped },
                { "Other", spsOther },
            };
        }

        private NamedValueSet GetSJSNVS()
        {
            return new NamedValueSet()
            {
                { "WaitingForStart", sjsWaitingForStart },
                { "Running", sjsRunning },
                { "Processed", sjsProcessed },
                { "Rejected", sjsRejected },
                { "Skipped", sjsSkipped},
                { "Pausing", sjsPausing },
                { "Paused", sjsPaused },
                { "Stopping", sjsStopping },
                { "Stopped", sjsStopped },
                { "Aborting", sjsAborting },
                { "Aborted", sjsAborted },
                { "Lost", sjsLost },
                { "Returning", sjsReturning },
                { "Returned", sjsReturned },
                { "Held", sjsHeld },
                { "RoutingAlarm", sjsRoutingAlarm },
                { "Removed", sjsRemoved },
                { "Stranded", sjsStranded },
                { "Other", sjsOther },
            };
        }

        [Obsolete("Please switch to the use of the STSToString method. (2018-12-08)")]
        public string SLSToString() { return STSToString(); }

        public string STSToString() { return CustomToString(GetSTSNVS()); }

        public string SPSToString() { return CustomToString(GetSPSNVS()); }

        public string SJSToString() { return CustomToString(GetSJSNVS()); }

        public override string ToString()
        {
            string motionPendingCountStr = (motionPendingCount > 0) ? " inMotion:{0}".CheckedFormat(motionPendingCount) : "";

            return "sts:[{0}] sps:[{1}] sjs:[{2}]{3}".CheckedFormat(CustomToString(GetSTSNVS(), emptyString: "None"), CustomToString(GetSPSNVS(), emptyString: "None"), CustomToString(GetSJSNVS(), emptyString: "None"), motionPendingCountStr);
        }

        public SubstrateStateTally MakeCopyOfThis(bool deepCopy = true)
        {
            return (SubstrateStateTally)this.MemberwiseClone();
        }
    }

    #endregion

    #region SubstrateSchedulingException

    /// <summary>
    /// Exception class that is intended for use in this substrate scheduling, and related, code.
    /// </summary>
    public class SubstrateSchedulingException : System.Exception
    {
        public SubstrateSchedulingException(string mesg, System.Exception innerException = null) 
            : base(mesg, innerException) 
        { }
    }

    #endregion
}
