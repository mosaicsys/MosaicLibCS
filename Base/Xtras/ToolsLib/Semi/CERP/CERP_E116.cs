//-------------------------------------------------------------------
/*! @file CERP_E116.cs
 *  @brief This file provides the E116 specific elements of the CombinedEventReporting pattern.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2022 Mosaic Systems Inc.
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

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

using Mosaic.ToolsLib.Semi.IDSpec;
using System.Runtime.Serialization;
using Mosaic.ToolsLib.MDRF2.Reader;
using MessagePack;
using Mosaic.ToolsLib.MDRF2.Common;

namespace Mosaic.ToolsLib.Semi.CERP.E116
{
    /// <summary>
    /// This class is used to contain information that is generated/recorded by the CERP on each emitted E116 EPT State change.
    /// Generally these objects are recycled using an object pool and as such no code should attempt to retain and/or re-use any specific instance
    /// unless it constructed that instance itself.
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.ToolsLibNameSpace)]
    public class E116EventRecord : CERPEventReportBase
    {
        /// <summary>Gives the MDRF2 type name that is generally used with this object type</summary>
        public const string MDRF2TypeName = "CERP.E116.EventRecord";

        /// <summary>Gives the <see cref="MosaicLib.Semi.E116.EPTTransition"/> that is being reported here.</summary>
        [DataMember]
        public MosaicLib.Semi.E116.EPTTransition Transition { get; set; }

        /// <summary>Gives the new <see cref="MosaicLib.Semi.E116.EPTState"/> reached after the reported transition.</summary>
        [DataMember]
        public MosaicLib.Semi.E116.EPTState State { get; set; }

        /// <summary>Gives the elapsed time that was spent in the previous <see cref="MosaicLib.Semi.E116.EPTState"/> prior to the reported transition.</summary>
        [DataMember]
        public TimeSpan StateTime { get; set; }

        /// <summary>Gives the <see cref="MosaicLib.Semi.E116.TaskType"/> that is associated with the new <see cref="MosaicLib.Semi.E116.EPTState"/>, if any</summary>
        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        public MosaicLib.Semi.E116.TaskType TaskType { get; set; }

        /// <summary>Gives the Task Name that is associated with the new <see cref="MosaicLib.Semi.E116.EPTState"/>, if any</summary>
        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        public string TaskName { get; set; }

        /// <summary>Gives the <see cref="MosaicLib.Semi.E116.BlockedReasonEx"/> that is associated with new <see cref="MosaicLib.Semi.E116.EPTState"/>, if any</summary>
        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        public MosaicLib.Semi.E116.BlockedReasonEx BlockedReason { get; set; }

        /// <summary>Gives the Blocked Reason Text that is associated with the new <see cref="MosaicLib.Semi.E116.EPTState"/>, if any</summary>
        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        public string BlockedReasonText { get; set; }

        /// <summary>Gives the previous <see cref="MosaicLib.Semi.E116.EPTState"/> from before the reported transition</summary>
        public MosaicLib.Semi.E116.EPTState PrevState { get; set; }

        /// <summary>Gives the Previous <see cref="MosaicLib.Semi.E116.TaskType"/> that is associated with the previous <see cref="MosaicLib.Semi.E116.EPTState"/></summary>
        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        public MosaicLib.Semi.E116.TaskType PrevTaskType { get; set; }

        /// <summary>Gives the Previous Task Name that is associated with the previous <see cref="MosaicLib.Semi.E116.EPTState"/></summary>
        [DataMember]
        public string PrevTaskName { get; set; }

        /// <summary>
        /// Gives the work count increment (accumulator increment) that is associated with the <see cref="MosaicLib.Semi.E116.EPTTransition"/>.  
        /// This is normally only non-empty on Busy->Idle and Busy->Busy transitions which represent successfull work completion.
        /// </summary>
        public ValueContainer WorkCountIncrement { get; set; }

        [DataMember(Name = "WorkCountIncrement", EmitDefaultValue = false, IsRequired = false)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used for serialization")]
        private ValueContainerEnvelope WorkCountIncrementVCE 
        { 
            get { return !WorkCountIncrement.IsEmpty ? new ValueContainerEnvelope() { VC = WorkCountIncrement } : null; }
            set { WorkCountIncrement = value.VC; }
        }

        /// <inheritdoc/>
        public override ICERPEventReport MakeCopyOfThis(bool deepCopy = true)
        {
            var copy = (E116EventRecord)MemberwiseClone();

            if (KVCSet != null)
                copy.KVCSet = new List<KeyValuePair<string, ValueContainer>>(KVCSet);

            return copy;
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            base.Clear();

            Transition = default;
            State = default;
            StateTime = default;
            TaskName = default;
            TaskType = default;
            BlockedReason = default;
            BlockedReasonText = default;
            PrevState = default;
            PrevTaskType = default;
            PrevTaskName = default;
            WorkCountIncrement = default;
        }

        /// <summary>Debugging and logging helper method</summary>
        public override string ToString()
        {
            switch (State)
            {
                case MosaicLib.Semi.E116.EPTState.Idle:
                    return $"E116EventRecord {Module.Name} {Transition} {State}<-{PrevState} {StateTime.TotalSeconds:f1}sec wic:{WorkCountIncrement}";
                case MosaicLib.Semi.E116.EPTState.Busy:
                    return $"E116EventRecord {Module.Name} {Transition} {State}<-{PrevState} {TaskType} '{TaskName}' prev:{PrevTaskType} '{PrevTaskName}' wic:{WorkCountIncrement}";
                case MosaicLib.Semi.E116.EPTState.Blocked:
                    return $"E116EventRecord {Module.Name} {Transition} {State}<-{PrevState} {BlockedReason} '{BlockedReasonText}' prev:{PrevTaskType} '{PrevTaskName}'";
                default:
                    return $"E116EventRecord {Module.Name} {Transition} {State}<-{PrevState}";
            }
        }

        /// <inheritdoc/>
        internal override void Release()
        {
            var mySelf = this;

            Pool.ReturnObjectToPool(ref mySelf);
        }

        /// <summary>Gives the value that will be used to define the <see cref="Pool"/>'s Capacity when the Pool is constructed on first use.</summary>
        public static int InitialPoolCapacity { get; set; } = 100;

        /// <summary>Gives the standard pool instance that is used for recycling of E116EventRecord object instances.</summary>
        internal static readonly MosaicLib.Utils.Pooling.BasicObjectPool<E116EventRecord> Pool = new MosaicLib.Utils.Pooling.BasicObjectPool<E116EventRecord>()
        {
            Capacity = InitialPoolCapacity,
            ObjectFactoryDelegate = () => new E116EventRecord(),
            ObjectClearDelegate = (item) => item.Clear(),
        };

        /// <inheritdoc/>
        public override void Serialize(ref MessagePackWriter mpWriter, MessagePackSerializerOptions mpOptions)
        {
            mpWriter.WriteArrayHeader(12);

            base.Serialize(ref mpWriter, mpOptions);
            mpWriter.Write((int)Transition);
            mpWriter.Write((int)State);
            mpWriter.Write(StateTime.TotalSeconds);
            mpWriter.Write((int)TaskType);
            mpWriter.Write(TaskName);
            mpWriter.Write((int)BlockedReason);
            mpWriter.Write(BlockedReasonText);
            mpWriter.Write((int)PrevState);
            mpWriter.Write((int)PrevTaskType);
            mpWriter.Write(PrevTaskName);
            MessagePackUtils.VCFormatter.Instance.Serialize(ref mpWriter, WorkCountIncrement, mpOptions);
        }

        /// <inheritdoc/>
        public override void Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions)
        {
            int arraySize = mpReader.ReadArrayHeader();
            if (arraySize != 12)
                new System.ArgumentException($"Cannot deserialize {Fcns.CurrentClassLeafName}: unexpected list size [{arraySize} != 12]").Throw();

            base.Deserialize(ref mpReader, mpOptions);
            Transition = (MosaicLib.Semi.E116.EPTTransition)mpReader.ReadInt32();
            State = (MosaicLib.Semi.E116.EPTState)mpReader.ReadInt32();
            StateTime = mpReader.ReadDouble().FromSeconds();
            TaskType = (MosaicLib.Semi.E116.TaskType)mpReader.ReadInt32();
            TaskName = mpReader.ReadString();
            BlockedReason = (MosaicLib.Semi.E116.BlockedReasonEx)mpReader.ReadInt32();
            BlockedReasonText = mpReader.ReadString();
            PrevState = (MosaicLib.Semi.E116.EPTState)mpReader.ReadInt32();
            PrevTaskType = (MosaicLib.Semi.E116.TaskType)mpReader.ReadInt32();
            PrevTaskName = mpReader.ReadString();
            WorkCountIncrement = MessagePackUtils.VCFormatter.Instance.Deserialize(ref mpReader, mpOptions);
        }
    }

    /// <summary>
    /// This class is used to configure a given E116 client by passing it into the constructor for an <see cref="E116ModuleScopedToken"/> instance.
    /// </summary>
    public class E116ModuleConfig : ICopyable<E116ModuleConfig>
    {
        /// <summary>Gives the name of the module to be registered</summary>
        public string ModuleName { get; set; }

        /// <summary>Gives the <see cref="ICombinedEventReportingPart"/> instance with which this module name is to be registered.</summary>
        public ICombinedEventReportingPart CERP { get; set; }

        /// <summary>When set to true the CERP will publish new event records, with the current state, to the module scoped token's StatePublisher</summary>
        public bool EnableStatePublication { get; set; }

        /// <summary>Gives the default <see cref="IScopedToken.Priority"/> value for module and other scoped tokens created from this value.</summary>
        public uint DefaultPriority { get; set; }

        /// <summary>
        /// Gives the substrate location names (IDs) for the substrate locations that are associated with this module.
        /// The CERP uses these to determine when the module has (and does not have) substrates and it automatically injects Busy states for cases
        /// where the module would normally be Idle but cannot be because it is still occupied by one or more substrates.
        /// </summary>
        public ReadOnlyIList<MosaicLib.Semi.E039.E039ObjectID> ModuleSubstLocSet { get; set; } = ReadOnlyIList<MosaicLib.Semi.E039.E039ObjectID>.Empty;

        /// <summary>
        /// When non-null this blocked info value tuple will be used to set the <see cref="E116ModuleScopedToken.InitialStateScopedToken"/> to an automatically created instance of a <see cref="E116BlockedScopedToken"/> to define the module's initial state.
        /// </summary>
        public (MosaicLib.Semi.E116.BlockedReasonEx blockedReason, string blockedReasonText)? InitialBlockedInfo { get; set; }

        /// <summary>
        /// When non-null this busy info value tuple will be used to set the <see cref="E116ModuleScopedToken.InitialStateScopedToken"/> to an automatically created instance of a <see cref="E116BusyScopedToken"/> to define the module's initial state.
        /// </summary>
        public (MosaicLib.Semi.E116.TaskType taskType, string taskName)? InitialBusyInfo { get; set; }

        /// <summary>
        /// When non-null this spccifies the <see cref="IPartBase"/> instance that is used to generate automatic E116 state transitions.
        /// When non-null this specifies the part base state source that the CERP can use to automatically generate Busy or Blocked scoped token triggered transitions from.
        /// This property is used in conjunction with the <see cref="PartBaseStateUsageBehavior"/> property here to specify how to use an provided <see cref="PartBaseStateSource"/>.
        /// </summary>
        public IPartBase SourcePart { get; set; }

        /// <summary>
        /// This value is used in combination with the <see cref="SourcePart"/>, <see cref="AutomaticBlockedTransitionDelegate"/>, and <see cref="AutomaticBusyTransitionDelegate"/> to specify how a <see cref="IPartBase"/> value may be used to automatically generate and remove E116 Busy and Blocked scoped tokens.
        /// </summary>
        public AutomaticTransitionBehavior PartBaseStateUsageBehavior { get; set; } = AutomaticTransitionBehavior.GenerateInitialScopedTokenIfNeeded | AutomaticTransitionBehavior.AutomaticWaitingTransitions;

        /// <summary>Defines the priority used for all automatically generated blocked scoped tokens</summary>
        public uint AutomaticBlockedTransitionPriorty { get; set; }

        /// <summary>Defines the priority used for all automatically generated busy scoped tokens</summary>
        public uint AutomaticBusyTransitionPriority { get; set; }

        /// <summary>
        /// Gives the predicate delegate that the CERP will use to determine when the part is faulted and what blocked reason and blocked reason text to use when it is
        /// </summary>
        public Func<E116ModuleScopedToken, IBaseState, (MosaicLib.Semi.E116.BlockedReasonEx blockedReason, string blockedReasonText)> AutomaticBlockedTransitionDelegate { get; set; } = DefaultAutomaticBlockedTransitionDelegate;

        /// <summary>
        /// Gives the predicate delegate that the CERP will use to determine when the part is busy.
        /// </summary>
        public Func<E116ModuleScopedToken, IBaseState, (MosaicLib.Semi.E116.TaskType taskType, string taskName)> AutomaticBusyTransitionDelegate { get; set; } = DefaultAutomaticBusyTransitionDelegate;

        /// <summary>Specifies the default AnnotationVC to be used with the <see cref="E116ModuleScopedToken"/></summary>
        public ValueContainer DefaultAnnotationVC { get; set; }

        /// <summary>Explicit copy/clone method</summary>
        public E116ModuleConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (E116ModuleConfig)this.MemberwiseClone();
        }

        /// <summary>
        /// Default method used to query a given parts's <paramref name="baseState"/> to produce the blocked reason, and reason text to be used if automatic blocked transitions are enabled
        /// </summary>
        public static (MosaicLib.Semi.E116.BlockedReasonEx blockedReason, string blockedReasonText) DefaultAutomaticBlockedTransitionDelegate(E116ModuleScopedToken moduleScopedToken, IBaseState baseState)
        {
            if (baseState?.IsFaulted() == true)
                return (MosaicLib.Semi.E116.BlockedReasonEx.ErrorCondition, null);
            else
                return (MosaicLib.Semi.E116.BlockedReasonEx.NotBlocked, null);
        }

        /// <summary>
        /// Default method used to query a given parts's <paramref name="baseState"/> to produce the task type, and task description text to be used if automatic busy transitions are enabled
        /// </summary>
        public static (MosaicLib.Semi.E116.TaskType taskType, string taskName) DefaultAutomaticBusyTransitionDelegate(E116ModuleScopedToken moduleScopedToken, IBaseState baseState)
        {
            if (baseState?.IsBusy == true)
                return (MosaicLib.Semi.E116.TaskType.Unspecified, null);
            else
                return (MosaicLib.Semi.E116.TaskType.NoTask, null);
        }
    }

    /// <summary>
    /// This flag enumeration defines the set of automatic Busy/Blocked transition behaviors that may be selected when a SourcePart has been specified.
    /// <para/>None (0x00), AutomaticBlockedTransitions (0x01), AutomaticBusyTransitions (0x10), AutomaticWaitingTransitions (0x20), GenerateInitialScopedTokenIfNeeded (0x100)
    /// </summary>
    [Flags]
    public enum AutomaticTransitionBehavior : int
    {
        /// <summary>Placeholder default.  No initial or automatic transitions will be produced. [0x00]</summary>
        None = 0x00,

        /// <summary>
        /// When this option is selected the CERP will automatically generate and dispose of <see cref="E116BlockedScopedToken"/> instances
        /// whenever the part changes between being faulted and not being faulted.
        /// </summary>
        AutomaticBlockedTransitions = 0x01,

        /// <summary>
        /// When this option is selected the CERP will automatically generate and dispose of <see cref="E116BusyScopedToken"/> instances 
        /// whenever the part changes between not busy and busy.
        /// </summary>
        AutomaticBusyTransitions = 0x10,

        /// <summary>
        /// When this option is selected the CERP will automatically generate and dispose of <see cref="E116BusyScopedToken"/> instances 
        /// whenever there are one or more substrates in the module and it is not otherwise busy.  
        /// The decision to indicate waiting for process or waiting for removal will be automatically determined.
        /// </summary>
        /// <remarks>
        /// NOTE: automatic waiting transitions require that E090 is making use of the SPSList and of the Moved/Relocated pseudo SPS, typically along with the ProcessStepCompleted pseudoSPS.
        /// If E090 has not been configured this way then the related logic will not necessarily produce the correct waiting reason.
        /// </remarks>
        AutomaticWaitingTransitions = 0x20,

        /// <summary>
        /// When this option is selected the CERP will review the part's initial state to determine the correct initial E116 state.  
        /// Then if the corresponding automatic behavior (<see cref="AutomaticBlockedTransitions"/> or <see cref="AutomaticBusyTransitions"/>) is not selected
        /// it will generate a corresponding <see cref="E116BlockedScopedToken"/> or <see cref="E116BusyScopedToken"/> instance to define the initial state
        /// and it will assign the generated token to the <see cref="E116ModuleScopedToken"/>'s InitialStateScopedToken property.  
        /// The client will be responsible for disposing of this token, and removing it from the <see cref="E116ModuleScopedToken"/>, 
        /// once the initial state has been resolved, or other scoped token instances have been created to determine the state.
        /// </summary>
        GenerateInitialScopedTokenIfNeeded = 0x100,
    }

    /// <summary>
    /// This Module Scoped Lock object is used to configure and anchor a client's registration and use of E116 with the configured
    /// <see cref="ICombinedEventReportingPart"/> instance.
    /// </summary>
    /// <remarks>
    /// The client registers and E116 module by:
    /// <para/>* constructing an <see cref="E116ModuleConfig"/> instance.
    /// <para/>* constructing an <see cref="E116ModuleScopedToken"/> instance to which the e116 module config is passed.
    /// <para/>* and then calling .Being on the resulting module scope token to register and configure the e116 source.
    /// <para/>
    /// When the client is done using the source they Dispose of the module scoped token to unregister it.
    /// </remarks>
    public class E116ModuleScopedToken : ModuleScopedTokenBase
    {
        /// <summary>Constructor</summary>
        /// <remarks>
        /// The current default behavior is the E116 token Begin and End operations do not wait for event delivery before completing.
        /// </remarks>
        public E116ModuleScopedToken(E116ModuleConfig moduleConfig)
            : base(moduleConfig.ModuleName, moduleConfig.CERP, "E116.Module", purposeStr: "E116", defaultScopedBeginSyncFlags: default, defaultScopedEndSyncFlags: default, defaultPriority: moduleConfig.DefaultPriority)
        {
            ModuleConfig = moduleConfig.MakeCopyOfThis();

            AnnotationVC = ModuleConfig.DefaultAnnotationVC;
        }

        /// <summary>Gives the contents of the module config object that was used (and was captured) at the construction of this scoped token</summary>
        internal E116ModuleConfig ModuleConfig { get; private set; }

        /// <summary>
        /// When the CERP processes the Scoped Begin for this Module Scoped Token it will determine the initial state to be used.  
        /// If that state is not Idle then it will generate an appropriate scoped token to capture the non-Idle initial state and will set this property to it.
        /// Once the module has completed handling of this initial state it Ends (and removes) it to release the related scoped token and 
        /// to resume normal Idle -> Busy -> Blocked scoped priority encoding and state determination.
        /// </summary>
        public IScopedToken InitialStateScopedToken { get; set; }

        /// <summary>
        /// Gives the StatePublisher for this module.
        /// Use of this publisher requires that the module corresponding <see cref="E116ModuleConfig.EnableStatePublication"/> property was explicitly set to true.
        /// The initial state will be published after the scoped token has been started.
        /// </summary>
        public ISequencedObjectSource<E116EventRecord, int> StatePublisher => _StatePublisher;

        /// <summary>
        /// Gives the internally settable StatePublisher for this module.
        /// </summary>
        internal readonly InterlockedSequencedRefObject<E116EventRecord> _StatePublisher = new InterlockedSequencedRefObject<E116EventRecord>();

        /// <summary>
        /// Dispose of initial scoped token if it is still there and it has not been ended elsewhere.
        /// </summary>
        protected override void AboutToEnd()
        {
            if (InitialStateScopedToken?.State == ScopedTokenState.Started)
            {
                InitialStateScopedToken.End();
                InitialStateScopedToken = null;
            }

            base.AboutToEnd();
        }
    }

    /// <summary>
    /// This scoped token is used to specify and report E116 Busy state conditions.
    /// </summary>
    public class E116BusyScopedToken : ScopedTokenBase
    {
        /// <summary>Constructor</summary>
        public E116BusyScopedToken(E116ModuleScopedToken moduleScopedToken)
            : base(moduleScopedToken, "E116.Busy")
        {
            base.DisableReporting = moduleScopedToken?.DisableReporting ?? true;

            BeginSyncFlags = moduleScopedToken?.DefaultScopedBeginSyncFlags ?? default;
            EndSyncFlags = moduleScopedToken?.DefaultScopedEndSyncFlags ?? default;
        }

        /// <summary>Note: This property is get only.  Its value is determined from the <see cref="ModuleScopedToken"/> at construction time.</summary>
        public new bool DisableReporting { get => base.DisableReporting; }

        /// <summary>This gives the <see cref="Semi.E116.TaskType"/> that is to be reported with any related Busy transition</summary>
        public MosaicLib.Semi.E116.TaskType TaskType { get; set; }

        /// <summary>This gives the TaskName that is to be reported with any related Busy transition</summary>
        public string TaskName { get; set; }

        /// <summary>
        /// This gives the accumulated count increment that will applied at the end of this busy scoped token whenever it will 
        /// produce a Busy to Busy or a Busy to Idle transition.
        /// <para/>As such this count increment represent a successful work increment.
        /// </summary>
        public ValueContainer EndWorkCountIncrementOnSuccess { get; set; }

        /// <summary>
        /// Internal captured version of <see cref="EndWorkCountIncrementOnSuccess"/>
        /// </summary>
        internal ValueContainer CapturedEndWorkCountIncrementOnSuccess { get; set; }

        protected override void AboutToEnd()
        {
            base.AboutToEnd();

            CapturedEndWorkCountIncrementOnSuccess = EndWorkCountIncrementOnSuccess;

            if (ClearEndWorkCountIncrementOnEnd)
                EndWorkCountIncrementOnSuccess = default;
        }

        /// <summary>
        /// When this property is true the scoped token's work count increment will be cleared on the next End/Dispose.
        /// <para/>Defaults to true.
        /// </summary>
        public bool ClearEndWorkCountIncrementOnEnd { get; set; } = true;

        /// <summary>Helper Debug and Logging method</summary>
        public override string ToString()
        {
            return "{0} {1} '{2}'".CheckedFormat(base.ToString(), TaskType, TaskName);
        }
    }

    /// <summary>
    /// This scoped token is used to specify and report E116 Blocked state conditions.
    /// </summary>
    public class E116BlockedScopedToken : ScopedTokenBase
    {
        /// <summary>Constructor</summary>
        public E116BlockedScopedToken(E116ModuleScopedToken moduleScopedToken)
            : base(moduleScopedToken, "E116.Blocked")
        {
            base.DisableReporting = moduleScopedToken?.DisableReporting ?? true;

            BeginSyncFlags = moduleScopedToken?.DefaultScopedBeginSyncFlags ?? default;
            EndSyncFlags = moduleScopedToken?.DefaultScopedEndSyncFlags ?? default;
        }

        /// <summary>Note: This property is get only.  Its value is determined from the <see cref="ModuleScopedToken"/> at construction time.</summary>
        public new bool DisableReporting { get => base.DisableReporting; }

        /// <summary>Gives the <see cref="Semi.E116.BlockedReasonEx"/> value to be used while the module is reported as Blocked.</summary>
        public MosaicLib.Semi.E116.BlockedReasonEx BlockedReason { get; set; }

        /// <summary>Gives the text description of the reason that the module is reported as Blocked.</summary>
        public string BlockedReasonText { get; set; }

        /// <summary>Helper Debug and Logging method</summary>
        public override string ToString()
        {
            return "{0} {1} '{2}'".CheckedFormat(base.ToString(), BlockedReason, BlockedReasonText);
        }
    }
}