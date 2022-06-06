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

using System.Collections.Generic;
using System.Linq;

using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

using Mosaic.ToolsLib.Semi.IDSpec;
using System.Runtime.Serialization;
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
        /// <summary>Gives the type name that is used with this object type.</summary>
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
        /// This gives the list of <see cref="MosaicLib.Semi.E090.E090SubstEventInfo"/> for the substrate(s) that were in the Module at th time that this event record was populated.
        /// </summary>
        public List<MosaicLib.Semi.E090.E090SubstEventInfo> E090SubstEventInfoList { get; set; } = new List<MosaicLib.Semi.E090.E090SubstEventInfo>();

        /// <summary>
        /// Gives a query side usable proprety that will contain the set of substrate IDs for the substrates in the Module at the time the event record was populated.
        /// </summary>
        [DataMember(Name = "SubstIDSet", IsRequired = false, EmitDefaultValue = false)]
        public string[] SubstIDSet
        {
            get { return _SubstIDSet ?? E090SubstEventInfoList.Select(eventInfo => eventInfo.E090SubstInfo.ObjID.Name).ToArray(); }
            set { _SubstIDSet = value; }
        }
        private string[] _SubstIDSet;

        /// <inheritdoc/>
        public override ICERPEventReport MakeCopyOfThis(bool deepCopy = true)
        {
            var copy = (E157EventRecord)MemberwiseClone();

            if (KVCSet != null)
                copy.KVCSet = new List<KeyValuePair<string, ValueContainer>>(KVCSet);

            if (E090SubstEventInfoList.Count > 0)
                copy.SubstIDSet = SubstIDSet;

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
            E090SubstEventInfoList.Clear();
            _SubstIDSet = null;
        }

        /// <summary>Debugging and logging helper method</summary>
        public override string ToString()
        {
            switch (State)
            {
                case MosaicLib.Semi.E157.ModuleProcessState.NotExecuting:
                case MosaicLib.Semi.E157.ModuleProcessState.GeneralExecution:
                default:
                    return $"E157EventRecord {Module.Name} {Transition} {State} RCID:'{RCID}' RecID:'{RecID}' pjID:'{ProcessJobID}' prev:{PrevState}";
                case MosaicLib.Semi.E157.ModuleProcessState.StepActive:
                    return $"E157EventRecord {Module.Name} {Transition} {State} step:{StepCount}:'{StepID}' rcp:'{RCID}'/'{RecID}' pjID:'{ProcessJobID}' prev:{PrevState}";
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
        internal static readonly MosaicLib.Utils.Pooling.BasicObjectPool<E157EventRecord> Pool = new MosaicLib.Utils.Pooling.BasicObjectPool<E157EventRecord>()
        {
            Capacity = InitialPoolCapacity,
            ObjectFactoryDelegate = () => new E157EventRecord(),
            ObjectClearDelegate = (item) => item.Clear(),
        };

        /// <inheritdoc/>
        public override void Serialize(ref MessagePackWriter mpWriter, MessagePackSerializerOptions mpOptions)
        {
            mpWriter.WriteArrayHeader(11);

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
            stringArrayFormatter.Serialize(ref mpWriter, SubstIDSet, mpOptions);
        }

        private static readonly MessagePack.Formatters.ArrayFormatter<string> stringArrayFormatter = new MessagePack.Formatters.ArrayFormatter<string>();

        /// <inheritdoc/>
        public override void Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions)
        {
            int arraySize = mpReader.ReadArrayHeader();
            if (arraySize != 11)
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
            SubstIDSet = stringArrayFormatter.Deserialize(ref mpReader, mpOptions);
        }
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

        /// <summary>Gives the <see cref="ICombinedEventReportingPart"/> instance with which this module name is to be registered.</summary>
        public ICombinedEventReportingPart CERP { get; set; }

        /// <summary>Gives the default <see cref="IScopedToken.Priority"/> value for module and other scoped tokens created from this value.</summary>
        public uint DefaultPriority { get; set; }

        /// <summary>Gives the substrate location names (IDs) for the substrate locations that are associated with this module.</summary>
        public ReadOnlyIList<MosaicLib.Semi.E039.E039ObjectID> ModuleSubstLocSet { get; set; }

        /// <summary>Specifies the <see cref="CERP.E116.E116ModuleScopedToken"/> instance that this E157 instance is to be automatically linked to, if any.</summary>
        public CERP.E116.E116ModuleScopedToken E116ModuleScopedToken { get; set; }

        /// <summary>When non-null this string specifies the substrate attribute name that is used to carry the associated RecipeID (aka the identifier of the master recipe).</summary>
        public string RecipeIDAttributeName { get; set; }

        /// <summary>When non-null this string specifies the substrate attribute name that is used to carry the associated Process Job ID.</summary>
        public string ProcessJobIDAttributeName { get; set; }

        /// <summary>Explicit copy/clone method</summary>
        public E157ModuleConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (E157ModuleConfig)this.MemberwiseClone();
        }
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
    public class E157ModuleScopedToken : ModuleScopedTokenBase
    {
        /// <summary>Constructor</summary>
        /// <remarks>
        /// The current default behavior is the E157 token Begin and End operations generally default to waiting on event delivery before completing.
        /// </remarks>
        public E157ModuleScopedToken(E157ModuleConfig moduleConfig)
            : base(moduleConfig.ModuleName, moduleConfig.CERP, "E157.Module", purposeStr: "E157", defaultScopedBeginSyncFlags: SyncFlags.Events, defaultScopedEndSyncFlags: SyncFlags.Events, defaultPriority: moduleConfig.DefaultPriority)
        {
            ModuleConfig = moduleConfig.MakeCopyOfThis();
        }

        /// <summary>Gives the contents of the module config object that was used (and was captured) at the construction of this scoped token</summary>
        internal E157ModuleConfig ModuleConfig { get; private set; }
    }

    /// <summary>
    /// This scoped token is used to specify and report E157 General Execution state conditions.
    /// </summary>
    public class E157GeneralExcutionScopedToken : ScopedTokenBase
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

            var e116ModuleScopedToken = moduleScopedToken.ModuleConfig.E116ModuleScopedToken;

            if (e116ModuleScopedToken != null)
            {
                E116BusyScopedToken = new E116.E116BusyScopedToken(e116ModuleScopedToken)
                {
                    TaskName = e116TaskName ?? LogTypeName,
                    TaskType = e116TaskType ?? MosaicLib.Semi.E116.TaskType.Process,
                };
            }

            BeginSyncFlags = moduleScopedToken.DefaultScopedBeginSyncFlags;
            EndSyncFlags = moduleScopedToken.DefaultScopedEndSyncFlags;
        }

        /// <summary>
        /// Gives client access to the <see cref="E157.E157ModuleScopedToken"/> instance that this token is attached to.
        /// </summary>
        public E157.E157ModuleScopedToken E157ModuleScopedToken { get; private set; }

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
        public bool GeneralExecutionSucceeded { get; set; } = true;

        /// <summary>
        /// Gives the, optional, <see cref="CERP.E116.E116BusyScopedToken"/> that is used to perform the linked E116 Busy state request
        /// that is active for the duration of this scoped tokens active period.
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
    public class E157StepActiveScopedToken : ScopedTokenBase
    {
        /// <summary>Constructor</summary>
        public E157StepActiveScopedToken(E157GeneralExcutionScopedToken generalExcutionScopedToken, bool enableAutomaticStepCountGeneration = true)
            : base(generalExcutionScopedToken, "E157.StepActive")
        {
            GeneralExcutionScopedToken = generalExcutionScopedToken;
            EnableAutomaticStepCountGeneration = enableAutomaticStepCountGeneration;

            var moduleScopedToken = generalExcutionScopedToken.E157ModuleScopedToken;

            BeginSyncFlags = moduleScopedToken.DefaultScopedBeginSyncFlags;
            EndSyncFlags = moduleScopedToken.DefaultScopedEndSyncFlags;
        }

        private bool EnableAutomaticStepCountGeneration { get; set; }

        /// <summary>
        /// Each time the Step Active Scoped Token is about to Being and <see cref="EnableAutomaticStepCountGeneration"/> is true
        /// then the <see cref="StepCount"/> will be set to the current value of the corresponding <see cref="E157GeneralExcutionScopedToken.StepCountGenerator"/>
        /// and then the Step Count Generator will be incremented.  The resulting sequence shall be 1, 2, ... n.
        /// </summary>
        protected override void AboutToBegin()
        {
            base.AboutToBegin();

            if (EnableAutomaticStepCountGeneration)
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
        /// This gives the <see cref="E157GeneralExcutionScopedToken"/> instance within which this step active token is used.
        /// </summary>
        public E157GeneralExcutionScopedToken GeneralExcutionScopedToken { get; private set; }

        /// <summary>
        /// The client is expected to update this property prior to ending the step in order that it can generate the correspondingly correct event.
        /// </summary>
        public bool StepSucceeded { get; set; } = true;

        /// <summary>Helper Debug and Logging method</summary>
        public override string ToString()
        {
            return "{0} StepID:'{1}' StepCount:{2}".CheckedFormat(base.ToString(), StepID, StepCount);
        }
    }
}