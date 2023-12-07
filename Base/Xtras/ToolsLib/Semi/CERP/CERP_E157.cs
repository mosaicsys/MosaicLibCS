//-------------------------------------------------------------------
/*! @file CERP_E157.cs
 *  @brief This file provides the E157 specific elements of the CombinedEventReporting pattern.
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
using System.Runtime.Serialization;

using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

using MessagePack;

namespace Mosaic.ToolsLib.Semi.CERP.E157
{
    /// <summary>
    /// This class is used to contain information that is generated/recorded by the CERP on each emitted E157 Module Process State change.
    /// Generally these objects are recycled using an object pool and as such no code should attempt to retain and/or re-use any specific instance
    /// unless it constructed that instance itself.
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.ToolsLibNameSpace)]
    public class E157EventRecord : CERPEventReportBase
    {
        /// <summary>Gives the MDRF2 type name that is generally used with this object type</summary>
        public const string MDRF2TypeName = "CERP.E157.EventRecord";

        /// <summary>Gives the <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransition"/> that is being reported here.</summary>
        [DataMember]
        public MosaicLib.Semi.E157.ModuleProcessStateTransition Transition { get; set; }

        /// <summary>Gives the new <see cref="MosaicLib.Semi.E157.ModuleProcessState"/> reached after the reported transition.</summary>
        [DataMember]
        public MosaicLib.Semi.E157.ModuleProcessState State { get; set; }

        /// <summary>Gives the previous <see cref="MosaicLib.Semi.E157.ModuleProcessState"/> from before the reported transition.</summary>
        [DataMember]
        public MosaicLib.Semi.E157.ModuleProcessState PrevState { get; set; }

        /// <summary>
        /// Gives the identifier of the Recipe that the module has been told to process.  
        /// This is typically the Recipe ID of the process recipe selected by the corresponding process step specification in the
        /// sequence recipe.
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string RCID { get; set; }

        /// <summary>
        /// This specifies the identifier of the "Master Recipe".  This is typically a seqeuence recipe.
        /// When used with E040 this must be set to the corresonding Process Job's RecID.
        /// When used with standard E030 remote commands this must be set to the corresponding PPID.
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string RecID { get; set; }

        /// <summary>
        /// This specifies the ProcessJobID that is associated with the general execution (if any).
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string ProcessJobID { get; set; }

        /// <summary>
        /// This gives the Recipe Parameters that were specifid in the corresponding Process Job, if any.
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public NamedValueSet RecipeParameters { get; set; }

        /// <summary>
        /// Gives the string identifier that is used to identify the step being executed.
        /// These names may be hard coded based on the recipe execution format, 
        /// they may be automatically generated as the recipe is executed,
        /// they may be part of the recipe that the end customer defines for their on specific purposes,
        /// or they may be derived using other separately documented purposes.
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string StepID { get; set; }

        /// <summary>
        /// Gives the StepCount value to be incremented and reported with each Step active transition.  
        /// <para/>If this Scoped Token was constructed with the enableAutomaticStepCountGeneration parameter as true (the default)
        /// then this property will be set/incremented automatically using the step counter in the parent <see cref="GeneralExcutionScopedToken"/>.
        /// Otherwise the client is reponsible for assigning the value is E157 compliant maner.
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public uint StepCount { get; set; }

        /// <summary>
        /// Gives the <see cref="SubstInfoHandlingSelect"/> flag values that have been configured and selected for this event record's transition.
        /// </summary>
        public E157SubstInfoHandlingSelect SubstInfoHandlingSelect { get; set; }

        /// <summary>
        /// This gives the list of <see cref="MosaicLib.Semi.E039.IE039Object"/> instances for the substrate(s) that were in the Module at the time that this event record was populated.
        /// </summary>
        /// <remarks>
        /// Note: This property is intentionally not serialized at a <see cref="DataMember"/>.  
        /// Recording of this property is only supported using the <see cref="IMDRF2MessagePackSerializable"/> interface support provided elsewhere in this class.
        /// </remarks>
        public List<MosaicLib.Semi.E039.IE039Object> E090SubstObjList 
        { 
            get { return _E090SubstObjList ?? (_E090SubstObjList = new List<MosaicLib.Semi.E039.IE039Object>()); }
            set { _E090SubstObjList = value; }
        }
        private List<MosaicLib.Semi.E039.IE039Object> _E090SubstObjList;

        /// <summary>
        /// This gives the array of <see cref="MosaicLib.Semi.E090.E090SubstEventInfo"/> for the substrate(s) that were in the Module at th time that this event record was populated.
        /// </summary>
        public MosaicLib.Semi.E090.E090SubstEventInfo [] E090SubstEventInfoSet => GetSubstEventInfoSetFrom(_E090SubstObjList);

        /// <summary>
        /// Populates the given <paramref name="e090SubstEventInfoList"/> from the set of objects that are contained in this event record.
        /// If <paramref name="clearListFirst"/> is true then the given <paramref name="e090SubstEventInfoList"/> will be Cleared before any new <see cref="MosaicLib.Semi.E090.E090SubstEventInfo"/> instances are added to it.
        /// </summary>
        public void Populate(List<MosaicLib.Semi.E090.E090SubstEventInfo> e090SubstEventInfoList, bool clearListFirst = false)
        {
            if (clearListFirst)
                e090SubstEventInfoList.Clear();

            foreach (var e090SubstObj in _E090SubstObjList.MapNullToEmpty())
                e090SubstEventInfoList.Add(new MosaicLib.Semi.E090.E090SubstEventInfo(e090SubstObj));
        }

        /// <summary>
        /// Gives a query side usable proprety that will contain the set of substrate IDs for the substrates in the Module at the time the event record was populated.
        /// </summary>
        [DataMember(Name = "SubstIDSet", IsRequired = false, EmitDefaultValue = false)]
        public string[] SubstIDSet
        {
            get { return _SubstIDSet ?? (_SubstIDSet = GetSubstIDSetFrom(_E090SubstObjList)); }
            set { _SubstIDSet = value; }
        }
        private string[] _SubstIDSet;

        /// <summary>
        /// Takes a, possibly null, <paramref name="e090SubstObjList"/> and returns an array of the substrate IDs from that list.
        /// </summary>
        public static string[] GetSubstIDSetFrom(List<MosaicLib.Semi.E039.IE039Object> e090SubstObjList)
        {
            return (e090SubstObjList?.Select(obj => obj?.ID.Name)).SafeToArray(mapNullToEmpty: true);
        }

        /// <summary>
        /// Takes a, possibly null, <paramref name="e090SubstObjList"/> and returns an array of <see cref="MosaicLib.Semi.E090.E090SubstEventInfo"/> insatnces from that list.
        /// </summary>
        public static MosaicLib.Semi.E090.E090SubstEventInfo[] GetSubstEventInfoSetFrom(List<MosaicLib.Semi.E039.IE039Object> e090SubstObjList)
        {
            return (e090SubstObjList?.Select(obj => new MosaicLib.Semi.E090.E090SubstEventInfo(obj))).SafeToArray(mapNullToEmpty: true);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// When <paramref name="deepCopy"/> is passed as false, this method will only attempt to populate the <see cref="_SubstIDSet"/> but not the <see cref="_E090SubstObjList"/> in the copy.
        /// It will obtain the copies <see cref="_SubstIDSet"/> from either this instances <see cref="_SubstIDSet"/> or using <see cref="GetSubstIDSetFrom(List{MosaicLib.Semi.E039.IE039Object})"/> on this instances <see cref="_E090SubstObjList"/> if it is non-null.
        /// </remarks>
        public override ICERPEventReport MakeCopyOfThis(bool deepCopy = true)
        {
            var copy = (E157EventRecord)MemberwiseClone();

            if (deepCopy && _E090SubstObjList != null)
                copy._E090SubstObjList = _E090SubstObjList.SafeToList();

            if (_SubstIDSet != null)
                copy._SubstIDSet = _SubstIDSet.SafeToArray();
            else if (!deepCopy && _E090SubstObjList != null)
                copy._SubstIDSet = GetSubstIDSetFrom(_E090SubstObjList);

            copy._KVCSet = _KVCSet.SafeToList(mapNullToEmpty: false);

            return copy;
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            base.Clear();

            Transition = default;
            State = default;
            PrevState = default;
            RCID = default;
            RecID = default;
            ProcessJobID = default;
            RecipeParameters = default;
            StepID = default;
            StepCount = default;
            SubstInfoHandlingSelect = default;
            _E090SubstObjList?.Clear();
            _SubstIDSet = null;
        }

        /// <summary>Debugging and logging helper method</summary>
        public override string ToString()
        {
            return ToString(default);
        }

        /// <summary>
        /// Defines non-default ToString options that may be selected.
        /// </summary>
        [Flags]
        public enum ToStringSelect : int
        {
            Default = 0x00,
            IncludeSubstEventInfo = 0x01,
            IncludeKVCSet = 0x02,
            IncludeAll = (IncludeSubstEventInfo | IncludeKVCSet),
        }

        /// <summary>Debugging and logging helper method</summary>
        public string ToString(ToStringSelect toStringSelect)
        {
            bool includeSubstEventInfo = (toStringSelect & ToStringSelect.IncludeSubstEventInfo) != 0;
            bool includeKVCSet = (toStringSelect & ToStringSelect.IncludeKVCSet) != 0 && !_KVCSet.IsNullOrEmpty();

            string annotationVCStr = (AnnotationVC.IsEmpty ? string.Empty : $" annotation:{AnnotationVC}");
            string substInfoStr;
            string kvcInfoStr = "";

            if (_SubstIDSet.IsNullOrEmpty() && _E090SubstObjList.IsNullOrEmpty())
                substInfoStr = "";
            else if (includeSubstEventInfo && _E090SubstObjList != null)
                substInfoStr = string.Concat(" subst:[", string.Join(",", E090SubstEventInfoSet.Select(item => $"[{item}]")), "]");
            else
                substInfoStr = $" subst:{string.Join(",", SubstIDSet.MapNullToEmpty())}";

            if (includeKVCSet)
                kvcInfoStr = $" kvcSet:{KVCSet.ConvertToNamedValueSet().ToStringSML()}";

            return $"E157EventRecord {Module.Name} {Transition} {State} step:{StepCount}:'{StepID}' RCID:'{RCID}' RecID:'{RecID}' pjID:'{ProcessJobID}' prev:{PrevState}{annotationVCStr}{substInfoStr}{kvcInfoStr}";
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
        internal static readonly MosaicLib.Utils.Pooling.BasicObjectPool<E157EventRecord> Pool = new MosaicLib.Utils.Pooling.BasicObjectPool<E157EventRecord>()
        {
            Capacity = InitialPoolCapacity,
            ObjectFactoryDelegate = () => new E157EventRecord(),
            ObjectClearDelegate = (item) => item.Clear(),
        };

        /// <inheritdoc/>
        public override void Serialize(ref MessagePackWriter mpWriter, MessagePackSerializerOptions mpOptions)
        {
            bool recordE090SubstEventInfoList = (_E090SubstObjList != null) && ((SubstInfoHandlingSelect & E157SubstInfoHandlingSelect.RecordE090SubstObjList) != 0);
            bool recordSubstIDSet = !recordE090SubstEventInfoList && (_SubstIDSet != null || _E090SubstObjList != null) &&  ((SubstInfoHandlingSelect & (E157SubstInfoHandlingSelect.RecordE090SubstObjList | E157SubstInfoHandlingSelect.RecordSubstIDSet)) != 0);

            mpWriter.WriteArrayHeader(!recordE090SubstEventInfoList ? 11 : 12);

            base.Serialize(ref mpWriter, mpOptions);

            mpWriter.Write((int)Transition);
            mpWriter.Write((int)State);
            mpWriter.Write((int)PrevState);
            mpWriter.Write(RCID);
            mpWriter.Write(RecID);
            mpWriter.Write(ProcessJobID);
            MessagePackUtils.NVSFormatter.Instance.Serialize(ref mpWriter, RecipeParameters, mpOptions);
            mpWriter.Write(StepID);
            mpWriter.Write(StepCount);

            if (!recordSubstIDSet && !recordE090SubstEventInfoList)
            {
                mpWriter.WriteNil();        // placeholder for missing SubstIDSet
            }
            else if (recordE090SubstEventInfoList)
            {
                mpWriter.WriteNil();        // placeholder for older/skipped SubstIDSet

                // write out the objects from which the event info items were extracted.
                int numItems = _E090SubstObjList.Count;

                mpWriter.WriteArrayHeader(numItems);
                for (int index = 0; index < numItems; index++)
                    MessagePackUtils.E039ObjectFormatter.Instance.Serialize(ref mpWriter, _E090SubstObjList[index], mpOptions);
            }
            else // recordSubstIDSet
            {
                stringArrayFormatter.Serialize(ref mpWriter, SubstIDSet, mpOptions);
            }
        }

        private static readonly MessagePack.Formatters.ArrayFormatter<string> stringArrayFormatter = new MessagePack.Formatters.ArrayFormatter<string>();

        /// <inheritdoc/>
        public override void Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions)
        {
            int arraySize = mpReader.ReadArrayHeader();
            var haveE090SubstEventInfo = (arraySize == 12);

            if (arraySize != 11 && !haveE090SubstEventInfo)
                new System.ArgumentException($"Cannot deserialize {Fcns.CurrentClassLeafName}: unexpected list size [{arraySize} != 11]").Throw();

            base.Deserialize(ref mpReader, mpOptions);

            Transition = (MosaicLib.Semi.E157.ModuleProcessStateTransition)mpReader.ReadInt32();
            State = (MosaicLib.Semi.E157.ModuleProcessState)mpReader.ReadInt32();
            PrevState = (MosaicLib.Semi.E157.ModuleProcessState)mpReader.ReadInt32();
            RCID = mpReader.ReadString();
            RecID = mpReader.ReadString();
            ProcessJobID = mpReader.ReadString();
            RecipeParameters = MessagePackUtils.NVSFormatter.Instance.Deserialize(ref mpReader, mpOptions);
            StepID = mpReader.ReadString();
            StepCount = mpReader.ReadUInt32();

            if (haveE090SubstEventInfo)
            {
                mpReader.ReadNil();     // skip over placeholder null SubstIDSet

                int numItems = mpReader.ReadArrayHeader();

                var e090SubstObjListBuilder = new List<MosaicLib.Semi.E039.IE039Object>(numItems);

                for (int index = 0; index < numItems; index++)
                    e090SubstObjListBuilder.Add(MessagePackUtils.E039ObjectFormatter.Instance.Deserialize(ref mpReader, mpOptions));

                _E090SubstObjList = e090SubstObjListBuilder;
            }
            else
            {
                _SubstIDSet = stringArrayFormatter.Deserialize(ref mpReader, mpOptions);
            }
        }
    }

    /// <summary>
    /// This flag enumeration is used in <see cref="E157EventRecord"/> processing to determine which "flavor" of substrate information handling is to be used.
    /// <para/>None (0x00), CaptureE090SubstEventInfoList (0x01), CaptureSubstIDSet (0x02), RecordE090SubstEventInfoList (0x10), RerordSubstIDSet (0x20)
    /// </summary>
    /// <remarks>
    /// In general a selected value must be captured in order for it to be reported during event delivery and in order for it to be recorded.
    /// If the user has selected capture of only the <see cref="E157EventRecord.SubstIDSet"/> and recording of the <see cref="E157EventRecord.E090SubstObjList"/> then the recording may record the <see cref="E157EventRecord.SubstIDSet"/> instead.
    /// </remarks>
    [Flags]
    public enum E157SubstInfoHandlingSelect : int
    {
        /// <summary>Neither the <see cref="E157EventRecord.E090SubstObjList"/> nor the <see cref="E157EventRecord.SubstIDSet"/> will be captured or recorded.</summary>
        None = 0x00,

        /// <summary></summary>
        CaptureE090SubstObjList = 0x01,

        /// <summary></summary>
        CaptureSubstIDSet = 0x02,

        /// <summary></summary>
        RecordE090SubstObjList = 0x10,

        /// <summary></summary>
        RecordSubstIDSet = 0x20,
    }

    /// <summary>
    /// This class is used to configure a given E157 client by passing it into the constructor for an <see cref="E157ModuleScopedToken"/> instance.
    /// </summary>
    public class E157ModuleConfig : ICopyable<E157ModuleConfig>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public E157ModuleConfig() 
        { }

        /// <summary>
        /// Constructor to use when creating an E157 module instance that is linked to the given <paramref name="e116ModuleScopedToken"/>.
        /// </summary>
        public E157ModuleConfig(CERP.E116.E116ModuleScopedToken e116ModuleScopedToken)
            : this()
        {
            var e116ModuleConfig = e116ModuleScopedToken.ModuleConfig;

            ModuleName = e116ModuleConfig.ModuleName;
            CERP = e116ModuleConfig.CERP;
            ModuleSubstLocSet = e116ModuleConfig.ModuleSubstLocSet;

            E116ModuleScopedToken = e116ModuleScopedToken;
        }

        /// <summary>Gives the name of the module to be registered</summary>
        public string ModuleName { get; set; }

        /// <summary>Gives the <see cref="ICombinedEventReportingPart"/> instance with which this module name is to be registered.  Defaults to <see cref="DefaultCERP"/></summary>
        public ICombinedEventReportingPart CERP { get; set; } = DefaultCERP;

        /// <summary>When set to true the CERP will publish new event records, with the current state, to the module scoped token's StatePublisher.  Defaults to <see cref="DefaultEnableStatePublication"/>.</summary>
        public bool EnableStatePublication { get; set; } = DefaultEnableStatePublication;

        /// <summary>Gives the default <see cref="IScopedToken.Priority"/> value for module and other scoped tokens created from this value.  Defaults to <see cref="DefaultDefaultPriority"/>.</summary>
        public uint DefaultPriority { get; set; } = DefaultDefaultPriority;

        /// <summary>Gives the substrate location names (IDs) for the substrate locations that are associated with this module.</summary>
        public ReadOnlyIList<MosaicLib.Semi.E039.E039ObjectID> ModuleSubstLocSet { get; set; }

        /// <summary>Specifies the <see cref="CERP.E116.E116ModuleScopedToken"/> instance that this E157 instance is to be automatically linked to, if any.</summary>
        public CERP.E116.E116ModuleScopedToken E116ModuleScopedToken { get; set; }

        /// <summary>This value defines the default value that will be used to initialize the <see cref="E157GeneralExcutionScopedToken.GeneralExecutionSucceeded"/> property.  Defaults to <see cref="DefaultDefaultGeneralExecutionScopedTokenSuccess"/>.</summary>
        public bool ? DefaultGeneralExecutionScopedTokenSuccess { get; set; } = DefaultDefaultGeneralExecutionScopedTokenSuccess;

        /// <summary>This value defines the default value that will be used to initialize the <see cref="E157StepActiveScopedToken.StepSucceeded"/> property.  Defaults to <see cref="DefaultDefaultStepActiveScopedTokenSuccess"/>.</summary>
        public bool ? DefaultStepActiveScopedTokenSuccess { get; set; } = DefaultDefaultStepActiveScopedTokenSuccess;

        /// <summary>When this is true, GeneralExecution to NotActive transitions will carry both the StepCount and the StepID from the last completed step.  Defaults to <see cref="DefaultIncludeStepIDOnExecutingToNotActiveTransition"/>.</summary>
        public bool IncludeStepIDOnExecutingToNotActiveTransition { get; set; } = DefaultIncludeStepIDOnExecutingToNotActiveTransition;

        /// <summary>When this is true, the GeneralExecution scoped token will automatically set its own EndWorkCountIncrementOnSuccess property to the number of substrates during begin.  Defaults to <see cref="DefaultInitializeEndWorkCountIncrementOnSuccessOnGeneralExecutionBegin"/>.</summary>
        public bool InitializeEndWorkCountIncrementOnSuccessOnGeneralExecutionBegin { get; set; } = DefaultInitializeEndWorkCountIncrementOnSuccessOnGeneralExecutionBegin;

        /// <summary>This value defines the default value that will be used to initialize the <see cref="E157BlockRecordScopedToken.BlockRecordSucceeded"/> property.  Defaults to <see cref="DefaultDefaultBlockRecordScopedTokenSuccess"/>.</summary>
        public bool ? DefaultBlockRecordScopedTokenSuccess { get; set; } = DefaultDefaultBlockRecordScopedTokenSuccess;

        /// <summary>This value defines the default value that will be used to initialize the <see cref="E157BlockRecordScopedToken.RecordBeginTransition"/> property.  Defaults to <see cref="DefaultDefaultBlockRecordScopedTokenRecordsBeginTransition"/>.</summary>
        public bool ? DefaultBlockRecordScopedTokenRecordsBeginTransition { get; set; } = DefaultDefaultBlockRecordScopedTokenRecordsBeginTransition;

        /// <summary>This value defines the default value that will be used to initialize the <see cref="E157BlockRecordScopedToken.RecordEndTransition"/> property.  Defaults to <see cref="DefaultDefaultBlockRecordScopedTokenRecordsEndTransition"/>.</summary>
        public bool ? DefaultBlockRecordScopedTokenRecordsEndTransition { get; set; } = DefaultDefaultBlockRecordScopedTokenRecordsEndTransition;

        /// <summary>Specifies the default AnnotationVC to be used with the <see cref="E157ModuleScopedToken"/>.  Defaults to <see cref="ValueContainer.Empty"/>.</summary>
        public ValueContainer DefaultAnnotationVC { get; set; }

        /// <summary>This property specifies which <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransition"/>s will cause capturing of <see cref="E157EventRecord.E090SubstObjList"/>.  These will be avilable for reporting and/or recording.  If these are not capture for a given transition then they will not be available for reporting or recording, even if requested.  Defaults to <see cref="DefaultCaptureE090SubstObjListTransitionMask"/></summary>
        public MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask CaptureE090SubstObjListTransitionMask { get; set; } = DefaultCaptureE090SubstObjListTransitionMask;

        /// <summary>This property specifies which <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransition"/>s will cause capturing of the <see cref="E157EventRecord.SubstIDSet"/>.  These will be avilable for reporting and/or recording.  If these are not capture for a given transition then they will not be available for reporting or recording, even if requested.  Defaults to <see cref="DefaultCaptureSubstIDSetTransitionMask"/></summary>
        public MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask CaptureSubstIDSetTransitionMask { get; set; } = DefaultCaptureSubstIDSetTransitionMask;

        /// <summary>This property specifies which <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransition"/>s will cause recording of <see cref="E157EventRecord.E090SubstObjList"/>.  Defaults to <see cref="DefaultRecordE090SubstObjListTransitionMask"/></summary>
        public MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask RecordE090SubstObjListTransitionMask { get; set; } = DefaultRecordE090SubstObjListTransitionMask;

        /// <summary>This property specifies which <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransition"/>s will cause recording of the <see cref="E157EventRecord.SubstIDSet"/>.  Defaults to <see cref="DefaultRecordSubstIDSetTransitionMask"/></summary>
        public MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask RecordSubstIDSetTransitionMask { get; set; } = DefaultRecordSubstIDSetTransitionMask;

        /// <summary>Explicit copy/clone method</summary>
        public E157ModuleConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (E157ModuleConfig)this.MemberwiseClone();
        }

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.CERP"/> property values.</summary>
        public static ICombinedEventReportingPart DefaultCERP { get; set; }

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.EnableStatePublication"/> property values.  Defaults to <see langword="false"/></summary>
        public static bool DefaultEnableStatePublication { get; set; } = false;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.DefaultPriority"/> property values.  Defaults to <see langword="0"/></summary>
        public static uint DefaultDefaultPriority { get; set; } = 0;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.DefaultGeneralExecutionScopedTokenSuccess"/> property values.  Defaults to <see langword="false"/></summary>
        public static bool? DefaultDefaultGeneralExecutionScopedTokenSuccess { get; set; } = false;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.DefaultStepActiveScopedTokenSuccess"/> property values.  Defaults to <see langword="false"/></summary>
        public static bool? DefaultDefaultStepActiveScopedTokenSuccess { get; set; } = false;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.IncludeStepIDOnExecutingToNotActiveTransition"/> property values.  Defaults to <see langword="false"/></summary>
        public static bool DefaultIncludeStepIDOnExecutingToNotActiveTransition { get; set; } = false;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.InitializeEndWorkCountIncrementOnSuccessOnGeneralExecutionBegin"/> property values.  Defaults to <see langword="false"/></summary>
        public static bool DefaultInitializeEndWorkCountIncrementOnSuccessOnGeneralExecutionBegin { get; set; } = false;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.DefaultBlockRecordScopedTokenSuccess"/> property values.  Defaults to <see langword="false"/></summary>
        public static bool? DefaultDefaultBlockRecordScopedTokenSuccess { get; set; } = false;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.DefaultBlockRecordScopedTokenRecordsBeginTransition"/> property values.  Defaults to <see langword="false"/></summary>
        public static bool? DefaultDefaultBlockRecordScopedTokenRecordsBeginTransition { get; set; } = false;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.DefaultBlockRecordScopedTokenRecordsEndTransition"/> property values.  Defaults to <see langword="true"/></summary>
        public static bool? DefaultDefaultBlockRecordScopedTokenRecordsEndTransition { get; set; } = true;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.CaptureE090SubstObjListTransitionMask"/> property values.  Defaults to <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask.None"/></summary>
        public static MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask DefaultCaptureE090SubstObjListTransitionMask { get; set; } = MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask.None;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.CaptureSubstIDSetTransitionMask"/> property values.  Defaults to <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask.Standard"/></summary>
        public static MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask DefaultCaptureSubstIDSetTransitionMask { get; set; } = MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask.Standard;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.RecordE090SubstObjListTransitionMask"/> property values.  Defaults to <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask.None"/></summary>
        public static MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask DefaultRecordE090SubstObjListTransitionMask { get; set; } = MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask.None;

        /// <summary>This static property specifies the default value for newly constructed <see cref="E157ModuleConfig.RecordSubstIDSetTransitionMask"/> property values.  Defaults to <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask.Standard"/></summary>
        public static MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask DefaultRecordSubstIDSetTransitionMask { get; set; } = MosaicLib.Semi.E157.ModuleProcessStateTransitionBitMask.Standard;
    }

    /// <summary>
    /// Basic interface that is supported by all E157 Scoped Token types.  This is used as the type of scoped token with the <see cref="E157BlockRecordScopedToken"/> constructor.
    /// </summary>
    public interface IE157ScopedToken : IScopedToken
    {
        /// <summary>
        /// Gives client access to the <see cref="E157ModuleScopedToken"/> instance that this token is attached to.
        /// </summary>
        E157ModuleScopedToken E157ModuleScopedToken { get; }
    }

    /// <summary>
    /// This Module Scoped Lock object is used to configure and anchor a client's registration and use of E157 with the configured
    /// <see cref="ICombinedEventReportingPart"/> instance, along with optinally linking E157 to E116 by configuring this <see cref="E157ModuleScopedToken"/>
    /// to be able to use a corresponding <see cref="CERP.E116.E116ModuleScopedToken"/> to obtain <see cref="CERP.E116.E116BusyScopedToken"/> instances
    /// when reporting that process is being executed.
    /// </summary>
    /// <remarks>
    /// The client registers and E157 module by:
    /// <para/>* constructing an <see cref="E157ModuleConfig"/> instance.
    /// <para/>* constructing an <see cref="E157ModuleScopedToken"/> instance to which the e157 module config is passed.
    /// <para/>* and then calling .Being on the resulting module scope token to register and configure the e157 source.
    /// <para/>
    /// When the client is done using the source they Dispose of the module scoped token to unregister it.
    /// </remarks>
    public class E157ModuleScopedToken : ModuleScopedTokenBase, IE157ScopedToken
    {
        /// <summary>Constructor</summary>
        /// <remarks>
        /// The current default behavior is the E157 token Begin and End operations generally default to waiting on event delivery before completing.
        /// </remarks>
        public E157ModuleScopedToken(E157ModuleConfig moduleConfig)
            : base(moduleConfig.ModuleName, moduleConfig.CERP, "E157.Module", purposeStr: "E157", defaultScopedBeginSyncFlags: SyncFlags.Events, defaultScopedEndSyncFlags: SyncFlags.Events, defaultPriority: moduleConfig.DefaultPriority)
        {
            ModuleConfig = moduleConfig.MakeCopyOfThis();

            AnnotationVC = ModuleConfig.DefaultAnnotationVC;
        }

        /// <summary>Gives the contents of the module config object that was used (and was captured) at the construction of this scoped token</summary>
        internal E157ModuleConfig ModuleConfig { get; private set; }

        E157ModuleScopedToken IE157ScopedToken.E157ModuleScopedToken { get => this; }

        /// <summary>
        /// Gives the StatePublisher for this module.
        /// Use of this publisher requires that the module corresponding <see cref="E157ModuleConfig.EnableStatePublication"/> property was explicitly set to true.
        /// The initial state will be published after the scoped token has been started.
        /// </summary>
        public ISequencedObjectSource<E157EventRecord, int> StatePublisher => _StatePublisher;

        /// <summary>
        /// Gives the internally settable StatePublisher for this module.
        /// </summary>
        internal readonly InterlockedSequencedRefObject<E157EventRecord> _StatePublisher = new InterlockedSequencedRefObject<E157EventRecord>();
    }

    /// <summary>
    /// This scoped token is used to specify and report E157 General Execution state conditions.
    /// </summary>
    public class E157GeneralExcutionScopedToken : ScopedTokenBase, IE157ScopedToken
    {
        /// <summary>
        /// Constructor.
        /// <para/>When this E157 instance is linked to E116 then the given <paramref name="e116TaskName"/> and <paramref name="e116TaskType"/>
        /// Will be used to configure the <see cref="E116.E116BusyScopedToken"/> instance that this scoped token will create and which will be
        /// used to indicate that the module is Busy while it is in the GeneralExecution state.
        /// </summary>
        public E157GeneralExcutionScopedToken(E157ModuleScopedToken moduleScopedToken, string e116TaskName = null, MosaicLib.Semi.E116.TaskType ? e116TaskType = null)
            : base(moduleScopedToken, "E157.GeneralExecution")
        {
            E157ModuleScopedToken = moduleScopedToken;

            var e157ModuleConfig = moduleScopedToken?.ModuleConfig;
            var e116ModuleScopedToken = e157ModuleConfig?.E116ModuleScopedToken;

            if (e116ModuleScopedToken != null)
            {
                E116BusyScopedToken = new E116.E116BusyScopedToken(e116ModuleScopedToken)
                {
                    TaskName = e116TaskName ?? LogTypeName,
                    TaskType = e116TaskType ?? MosaicLib.Semi.E116.TaskType.Process,
                };
            }

            BeginSyncFlags = moduleScopedToken?.DefaultScopedBeginSyncFlags ?? default;
            EndSyncFlags = moduleScopedToken?.DefaultScopedEndSyncFlags ?? default;

            GeneralExecutionSucceeded = moduleScopedToken?.ModuleConfig?.DefaultGeneralExecutionScopedTokenSuccess ?? true;
        }

        /// <inheritdoc/>
        public E157ModuleScopedToken E157ModuleScopedToken { get; private set; }

        /// <summary>
        /// Gives the identifier of the Recipe that the module has been told to process.  
        /// This is typically the Recipe ID of the process recipe selected by the corresponding process step specification in the
        /// sequence recipe.
        /// </summary>
        public string RCID { get; set; }

        /// <summary>
        /// This specifies the identifier of the "Master Recipe".  This is typically a seqeuence recipe.
        /// When used with E040 this must be set to the corresonding Process Job's RecID, or the E030 PPID.
        /// Note: if unspecified, the CERP will attempt to replace this with the sequence recipe name from the first substrate that is in this module.
        /// </summary>
        public string RecID { get; set; }

        /// <summary>
        /// This specifies the ProcessJobID that is associated with the general execution (if any).
        /// Note: if unspecified, the CERP will attempt to replace this with the process job ID from the first substrate that is in this module.
        /// </summary>
        public string ProcessJobID { get; set; }

        /// <summary>
        /// Gives the set of Recipe Parameters (if any) that have been specified to adjust the process execution, 
        /// for APC and other purposes.
        /// </summary>
        public INamedValueSet RecipeParameters { get; set; }

        /// <summary>
        /// The client is expected to update this property prior to ending the step in order that it can generate the correspondingly correct event.
        /// </summary>
        public bool GeneralExecutionSucceeded { get; set; }

        /// <summary>
        /// When this value is non-empty, and this module is operating in E116 linked mode, then it will be used to set the 
        /// related <see cref="E116.E116BusyScopedToken.EndWorkCountIncrementOnSuccess"/> based on the final success of the excution
        /// when the End is processed.
        /// </summary>
        public ValueContainer EndWorkCountIncrementOnSuccess { get; set; }

        /// <summary>
        /// Gives the, optional, <see cref="CERP.E116.E116BusyScopedToken"/> that is used to perform the linked E116 Busy state request
        /// that is active for the duration of this scoped token's active period.
        /// </summary>
        public CERP.E116.E116BusyScopedToken E116BusyScopedToken { get; private set; }

        /// <summary>Helper Debug and Logging method</summary>
        public override string ToString()
        {
            return "{0} RCID:'{1}' RecID:'{2}'".CheckedFormat(base.ToString(), RCID, RecID);
        }

        /// <summary>
        /// This step counter is incremented and used when derived <see cref="E157StepActiveScopedToken"/> instances are about to begin
        /// if they have automatic step counting enabled.
        /// </summary>
        internal uint StepCountGenerator { get; set; }

        /// <summary>
        /// Each time General Execution Scoped Token is about to begin the <see cref="StepCountGenerator"/> is set to 1.
        /// </summary>
        protected override void AboutToBegin()
        {
            base.AboutToBegin();

            StepCountGenerator = 1;
        }
    }

    /// <summary>
    /// This scoped token is used to specify and report E157 Step Active state conditions.
    /// </summary>
    public class E157StepActiveScopedToken : ScopedTokenBase, IE157ScopedToken
    {
        /// <summary>Constructor</summary>
        public E157StepActiveScopedToken(E157GeneralExcutionScopedToken generalExcutionScopedToken, bool enableAutomaticStepCountGeneration = true)
            : base(generalExcutionScopedToken, "E157.StepActive")
        {
            GeneralExcutionScopedToken = generalExcutionScopedToken;

            base.DisableReporting = GeneralExcutionScopedToken?.DisableReporting ?? true;

            EnableAutomaticStepCountGeneration = enableAutomaticStepCountGeneration;

            E157ModuleScopedToken = generalExcutionScopedToken?.E157ModuleScopedToken;

            BeginSyncFlags = E157ModuleScopedToken?.DefaultScopedBeginSyncFlags ?? default;
            EndSyncFlags = E157ModuleScopedToken?.DefaultScopedEndSyncFlags ?? default;

            StepSucceeded = E157ModuleScopedToken?.ModuleConfig?.DefaultStepActiveScopedTokenSuccess ?? true;
        }

        /// <summary>Note: This property is get only.  Its value is determined from the <see cref="GeneralExcutionScopedToken"/> at construction time.</summary>
        public new bool DisableReporting { get => base.DisableReporting; }

        /// <inheritdoc/>
        public E157ModuleScopedToken E157ModuleScopedToken { get; private set; }

        private bool EnableAutomaticStepCountGeneration { get; set; }

        /// <summary>
        /// Each time the Step Active Scoped Token is about to Being and <see cref="EnableAutomaticStepCountGeneration"/> is true
        /// then the <see cref="StepCount"/> will be set to the current value of the corresponding <see cref="E157GeneralExcutionScopedToken.StepCountGenerator"/>
        /// and then the Step Count Generator will be incremented.  The resulting sequence shall be 1, 2, ... n.
        /// </summary>
        protected override void AboutToBegin()
        {
            base.AboutToBegin();

            if (EnableAutomaticStepCountGeneration && GeneralExcutionScopedToken != null)
            {
                StepCount = GeneralExcutionScopedToken.StepCountGenerator++;
            }
        }

        /// <summary>
        /// Gives the string identifier that is used to identify the step being executed.
        /// These names may be hard coded based on the recipe execution format, 
        /// they may be automatically generated as the recipe is executed,
        /// they may be part of the recipe that the end customer defines for their on specific purposes,
        /// or they may be derived using other separately documented purposes.
        /// </summary>
        public string StepID { get; set; }

        /// <summary>
        /// Gives the StepCount value to be incremented and reported with each Step active transition.  
        /// <para/>If this Scoped Token was constructed with the enableAutomaticStepCountGeneration parameter as true (the default)
        /// then this property will be set/incremented automatically using the step counter in the parent <see cref="GeneralExcutionScopedToken"/>.
        /// Otherwise the client is reponsible for assigning the value is E157 compliant maner.
        /// </summary>
        public uint StepCount { get; set; }

        /// <summary>
        /// Gives client access to the <see cref="E157GeneralExcutionScopedToken"/> instance that this token is attached to, or null if there is no such token.
        /// </summary>
        public E157GeneralExcutionScopedToken GeneralExcutionScopedToken { get; private set; }

        /// <summary>
        /// The client is expected to update this property prior to ending the step in order that it can generate the correspondingly correct event.
        /// </summary>
        public bool StepSucceeded { get; set; }

        /// <summary>Helper Debug and Logging method</summary>
        public override string ToString()
        {
            return "{0} StepID:'{1}' StepCount:{2}".CheckedFormat(base.ToString(), StepID, StepCount);
        }
    }

    /// <summary>
    /// This scoped token is used to specify and optionally record (but not report) Block Record transitions.
    /// </summary>
    public class E157BlockRecordScopedToken : ScopedTokenBase, IE157ScopedToken
    {
        /// <summary>Constructor</summary>
        public E157BlockRecordScopedToken(IE157ScopedToken parentScopedToken)
            : base(parentScopedToken, "E157.BlockRecord")
        {
            ParentScopedToken = parentScopedToken;

            base.DisableReporting = true;

            E157ModuleScopedToken = parentScopedToken?.E157ModuleScopedToken;

            BeginSyncFlags = E157ModuleScopedToken?.DefaultScopedBeginSyncFlags ?? default;
            EndSyncFlags = E157ModuleScopedToken?.DefaultScopedEndSyncFlags ?? default;

            BlockRecordSucceeded = E157ModuleScopedToken?.ModuleConfig?.DefaultBlockRecordScopedTokenSuccess ?? true;
            RecordBeginTransition = E157ModuleScopedToken?.ModuleConfig?.DefaultBlockRecordScopedTokenRecordsBeginTransition ?? false;
            RecordEndTransition = E157ModuleScopedToken?.ModuleConfig?.DefaultBlockRecordScopedTokenRecordsEndTransition ?? true;
        }

        /// <summary>Note: this property is not usable.  It throws a <see cref="System.NotImplementedException"/>.</summary>
        public new bool DisableReporting { get => throw new System.NotImplementedException(); }

        /// <inheritdoc/>
        public E157ModuleScopedToken E157ModuleScopedToken { get; private set; }

        /// <summary>
        /// This gives the <see cref="IE157ScopedToken"/> instance within which this block record token is used.
        /// </summary>
        public IE157ScopedToken ParentScopedToken { get; private set; }

        /// <summary>
        /// The client is expected to update this property prior to ending the step in order that it can generate the correspondingly correct event.
        /// </summary>
        public bool BlockRecordSucceeded { get; set; }

        /// <summary>When this is true the <see cref="IScopedToken.Begin(TimeSpan?, bool)"/> operation will record a <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransition.BlockRecordStarted"/> transition event.</summary>
        public bool RecordBeginTransition { get; set; }

        /// <summary>When this is true the <see cref="IScopedToken.End(TimeSpan?, bool)"/> operation will record a <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransition.BlockRecordCompleted"/> or <see cref="MosaicLib.Semi.E157.ModuleProcessStateTransition.BlockRecordFailed"/> transition event.</summary>
        public bool RecordEndTransition { get; set; }
    }
}