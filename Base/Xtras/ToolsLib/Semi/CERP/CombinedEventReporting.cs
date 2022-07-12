//-------------------------------------------------------------------
/*! @file CombinedEventReporting.cs
 *  @brief This file provides definitions that relate to the use of the CombinedEventReportingPart.
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
using System.Runtime.Serialization;

using MosaicLib;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;

using Mosaic.ToolsLib.Semi.IDSpec;
using Mosaic.ToolsLib.MDRF2.Common;
using MessagePack;
using System.Collections.Generic;
using System.Diagnostics;
using MosaicLib.Utils.Collections;

namespace Mosaic.ToolsLib.Semi.CERP
{
    #region ICERPEventReportBase, CERPEventReportBase

    /// <summary>
    /// This interface defines the set of base supported properties that are used with CERP Event Report record types.
    /// <para/>Please note that the final implementation objects are generally stored in pools and are re-used.
    /// </summary>
    public interface ICERPEventReport : ICopyable<ICERPEventReport>, IMDRF2RecoringKeyInfo
    {
        /// <summary>Gives the Module's originally looked up name and the found <see cref="IIDSpec"/> (if any)</summary>
        NameIIDSpecPair Module { get; }

        /// <summary>Gives an optional KVCSet which may be used to carry arbitrary additional information to event handler.</summary>
        IList<KeyValuePair<string, ValueContainer>> KVCSet { get; }

        /// <summary>
        /// When true this event report will not be passed to the delegate handler.  
        /// When false (the default) this event report will be passed to the delegate handler as normal.
        /// </summary>
        bool DisableReporting { get; }

        /// <summary>This property may be used to adjust the order that this event is reported vs being recorded.</summary>
        /// <remarks>Default (false) is to record the event record before reporting it to the delegate handler.</remarks>
        bool ReportBeforeRecording { get; }
    }

    /// <summary>
    /// This gives the partially abstract base class that is expected to be used for all derived CERP Event Report record types.
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.ToolsLibNameSpace)]
    public abstract class CERPEventReportBase : ICERPEventReport, IMDRF2MessagePackSerializable
    {
        /// <inheritdoc/>
        public NameIIDSpecPair Module { get; set; }

        [DataMember(Name = "Module", IsRequired = false, EmitDefaultValue = false)]

        private string ModuleName { get { return Module.Name; } set { Module = new NameIIDSpecPair() { Name = value }; } }

        /// <inheritdoc/>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<KeyValuePair<string, ValueContainer>> KVCSet { get; protected set; } = new List<KeyValuePair<string, ValueContainer>>();

        IList<KeyValuePair<string, ValueContainer>> ICERPEventReport.KVCSet => KVCSet;

        /// <inheritdoc/>
        public bool DisableReporting { get; set; }

        /// <inheritdoc/>
        public bool ReportBeforeRecording { get; set; }

        /// <inheritdoc/>
        public (int id, string name) RecordingKeyInfo { get; set; }

        /// <inheritdoc/>
        public abstract ICERPEventReport MakeCopyOfThis(bool deepCopy = true);

        /// <inheritdoc/>
        internal abstract void Release();

        /// <summary>
        /// This method is typically used to clear the contents of the object when it is being returned to its corresponding pool.
        /// </summary>
        public virtual void Clear()
        {
            Module = default;
            KVCSet.Clear();
            DisableReporting = default;
            ReportBeforeRecording = default;
            RecordingKeyInfo = default;
        }

        /// <inheritdoc/>
        public virtual void Serialize(ref MessagePackWriter mpWriter, MessagePackSerializerOptions mpOptions)
        {
            bool includeDisable = DisableReporting;

            mpWriter.WriteArrayHeader(2 + (includeDisable ? 1 : 0));

            mpWriter.Write(ModuleName);
            MessagePackUtils.KVCSetFormatter.Instance.Serialize(ref mpWriter, KVCSet, mpOptions);
            if (includeDisable)
                mpWriter.Write(DisableReporting);

            // note that RecordingKeyInfo is recorded by MDRF2 outside of the type name specific part of the MP record.
        }

        /// <inheritdoc/>
        public virtual void Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions)
        {
            int arraySize = mpReader.ReadArrayHeader();
            if (arraySize != 2 && arraySize != 3)
                new System.ArgumentException($"Cannot deserialize {Fcns.CurrentClassLeafName}: unexpected list size [{arraySize} != 2 or 3]").Throw();

            ModuleName = mpReader.ReadString();
            KVCSet.AddRange(MessagePackUtils.KVCSetFormatter.Instance.Deserialize(ref mpReader, mpOptions));
            if (arraySize == 3)
                DisableReporting = mpReader.ReadBoolean();

            // note that RecordingKeyInfo is populated by the MDRF2MessagePackSerializableTypeNameHandler to match the record contents.
        }
    }

    #endregion

    #region ScopedToken infrastructure (IScopedToken, ScopedTokenState, IScopedTokenEmitters)

    /// <summary>
    /// This is the base interface that all CERP ScopedToken classes support.  
    /// <para/>ScopedToken objects are generally used to specify a set of conditions (state information) and are used to deliniate the beginning and ending of application of this set of conditions/state information.
    /// This concept is used in E116 and E157 to support a simplified client way to delinate when specific context specific state information should be applied and removed,
    /// typically in the context of a CombinedEventReportingPart which is reponsible for consolidating and collating this information to drive transitions in the underlying state models.
    /// <para/>Support for the <see cref="IDisposable"/> interface, required here, allows the client to easily use the "using" pattern with these objects to automatically delinate the point
    /// in code flow where the End operation will be performed.
    /// </summary>
    public interface IScopedToken : IDisposable
    {
        /// <summary>
        /// Each scoped token may use this property to gain access to the client's <see cref="IModuleScopedToken"/> instance that defines the 
        /// module specific values that are used with its related scoped token instances.  This is also used by the related <see cref="IModuleScopedToken.CERP"/> instance to
        /// known which module is the source for any given IScopedToken.Begin or .End operation.
        /// </summary>
        IModuleScopedToken ModuleScopedToken { get; }

        /// <summary>
        /// Gives the current <see cref="ScopedTokenState"/> for this scoped token instance.
        /// Note: at present the use of this state is mostly advisory (for logging purposes).  
        /// There is only a limited amount of state transition validation logic that is used in relation to this state.
        /// </summary>
        ScopedTokenState State { get; }

        /// <summary>
        /// Gives the priority value that the client has assigned to this scoped token.  
        /// Used to allow the client to prioratize specific tokens over others when Begin order is not sufficient.
        /// </summary>
        uint Priority { get; }

        /// <summary>
        /// When non-null, this gives the set of additinal KVC items that will be included in any correspondingly generated <see cref="ICERPEventReport"/> item.
        /// </summary>
        IDictionary<string, ValueContainer> KVCDictionary { get; }

        /// <summary>
        /// Clients use this method to inform the related <see cref="IModuleScopedToken.CERP"/> that the application of the scoped conditions and state information is to begin now.
        /// <para/>This request first sets the state to <see cref="ScopedTokenState.Starting"/>.
        /// The state will remain as Starting until the related <see cref="IModuleScopedToken.CERP"/> has completed the Scoped Begin operation which will set it to <see cref="ScopedTokenState.Started"/>.
        /// </summary>
        /// <param name="waitTimeLimit">
        /// When a non-null waitTimeLimit is specified, either directly or through the specific scoped token's default value evaluation logic, it will be used to limit
        /// how long the caller is willing to wait for the completion of the underlying <see cref="ICombinedEventReportingPart.ScopedBegin(IScopedToken)"/> operation.
        /// </param>
        /// <param name="rethrow">
        /// When this property is passed as true and the operation fails, then it will both emit a corresponding issue log message and it will throw a corresponding exception to the caller.
        /// When this property is passed as false and the operation fails, then it will simply emit a corresopnding issue log message but will catch and not propagate any related thrown exceptions.
        /// </param>
        /// <remarks>
        /// Due to the risk of a scoped token being in a (temporarily) unusable state, it is not recommended to re-use scope token instances when using non-null waitTimeLimit values for this operation.
        /// Reuse of scoped token objects should only be considered by a client if the client always waits for the full completion of all related Begin and End operations so that the token is never left
        /// in the <see cref="ScopedTokenState.Starting"/> or <see cref="ScopedTokenState.Ending"/> states.
        /// </remarks>
        void Begin(TimeSpan? waitTimeLimit = null, bool rethrow = false);

        /// <summary>
        /// Clients use this method to inform the related <see cref="IModuleScopedToken.CERP"/> that the application of the scoped conditions and state information is to end now.
        /// <para/>This request first sets the state to <see cref="ScopedTokenState.Ending"/>.
        /// The state will remain as Ending until the related <see cref="IModuleScopedToken.CERP"/> has completed the Scoped End operation which will set it to <see cref="ScopedTokenState.Ended"/>.
        /// </summary>
        /// <param name="waitTimeLimit">
        /// When a non-null waitTimeLimit is specified, either directly or through the specific scoped token's default value evaluation logic, it will be used to limit
        /// how long the caller is willing to wait for the completion of the underlying <see cref="ICombinedEventReportingPart.ScopedEnd(IScopedToken)"/> operation.
        /// </param>
        /// <param name="rethrow">
        /// When this property is passed as true and the operation fails, then it will both emit a corresponding issue log message and it will throw a corresponding exception to the caller.
        /// When this property is passed as false and the operation fails, then it will simply emit a corresopnding issue log message but will catch and not propagate any related thrown exceptions.
        /// </param>
        /// <remarks>
        /// Due to the risk of a scoped token being in a (temporarily) unusable state, it is not recommended to re-use scope token instances when using non-null waitTimeLimit values for this operation.
        /// </remarks>
        void End(TimeSpan? waitTimeLimit = null, bool rethrow = false);

        /// <summary>Gives the <see cref="SyncFlags"/> value that is used with related Scoped Begin operations</summary>
        SyncFlags BeginSyncFlags { get; }

        /// <summary>Gives the <see cref="SyncFlags"/> value that is used with related Scoped End operations</summary>
        SyncFlags EndSyncFlags { get; }

        /// <summary>
        /// This property can be set by the client to true when constructing a specific <see cref="ScopedTokenBase"/> derived type.
        /// It can also be implicitly set to true if this <see cref="ScopedTokenBase"/>'s <see cref="IModuleScopedToken.DisableReporting"/> is true.
        /// When true this event report will not be passed to the delegate handler.  
        /// When false (the default) this event report will be passed to the delegate handler as normal.
        /// </summary>
        bool DisableReporting { get; }
    }

    /// <summary>
    /// This is the base implementation class that is generally used for instances that implement the <see cref="IScopedToken"/> interface.
    /// </summary>
    public class ScopedTokenBase : IScopedToken
    {
        /// <summary>
        /// Constructor.  
        /// If provided, it uses the <paramref name="other"/> <see cref="IScopedToken"/> instance to obtain the <see cref="ModuleScopedToken"/> instance to use.
        /// </summary>
        public ScopedTokenBase(IScopedToken other, string logTypeName)
            : this(logTypeName)
        {
            ModuleScopedToken = other?.ModuleScopedToken;

            if (ModuleScopedToken?.DisableReporting == true)
                DisableReporting = true;

            priority = ModuleScopedToken?.DefaultPriority ?? 0;
        }

        protected ScopedTokenBase(string logTypeName, uint defaultPriority = 0)
        {
            LogTypeName = logTypeName ?? Fcns.CurrentClassLeafName;

            priority = defaultPriority;
        }

        /// <summary>
        /// This method calls <see cref="ExtensionMethods.End{TScopedToken}(TScopedToken, TimeSpan?, bool)"/> or <see cref="ExtensionMethods.EndIfNeeded{TScopedToken}(TScopedToken, TimeSpan?, bool)"/> based on the value of the
        /// <see cref="DisposeUsesEndIfNeeded"/> static property.
        /// This method passes <see cref="EndWaitTimeLimitValueForUseWithDispose"/> and <see cref="EndRethrowValueForUseWithDispose"/> to the corresponding End/EndIfNeeded method parameters.
        /// </summary>
        public void Dispose()
        {
            if (DisposeUsesEndIfNeeded)
                ExtensionMethods.EndIfNeeded(this, waitTimeLimit: EndWaitTimeLimitValueForUseWithDispose, rethrow: EndRethrowValueForUseWithDispose);
            else
                ExtensionMethods.End(this, waitTimeLimit: EndWaitTimeLimitValueForUseWithDispose, rethrow: EndRethrowValueForUseWithDispose);
        }

        /// <summary>
        /// Defines the value that this classes Dispose method passes for the rethrow parameter when calling the End method.
        /// </summary>
        public static TimeSpan ? EndWaitTimeLimitValueForUseWithDispose { get; set; }

        /// <summary>
        /// Defines the value that this classes Dispose method passes for the rethrow parameter when calling the End method.
        /// </summary>
        public static bool EndRethrowValueForUseWithDispose { get; set; }

        /// <summary>
        /// This value defines if the <see cref="Dispose"/> method shall use <see cref="ExtensionMethods.EndIfNeeded{TScopedToken}(TScopedToken, TimeSpan?, bool)"/> or <see cref="ExtensionMethods.End{TScopedToken}(TScopedToken, TimeSpan?, bool)"/>.
        /// <para/>Defaults to true (use EndIfNeeded).
        /// </summary>
        public static bool DisposeUsesEndIfNeeded { get; set; } = true;

        /// <summary>Helper Debug and Logging method</summary>
        public override string ToString()
        {
            if (ModuleScopedToken != null)
                return $"{ModuleScopedToken.ModuleName}:{ModuleScopedToken.ModuleInstanceNum} {LogTypeName} {State}";
            else
                return $"[Empty]:0 {LogTypeName} {State}";
        }

        /// <summary>
        /// Gives the prefix name used for logging.  Inteded to carry the summary name for the type of ScopedToken that is being used here.
        /// </summary>
        protected string LogTypeName { get; set; }

        /// <inheritdoc/>
        public IModuleScopedToken ModuleScopedToken { get; protected set; }

        /// <inheritdoc/>
        public ScopedTokenState State { get; internal set; }

        /// <summary>
        /// Gives the priority value that the client has assigned to this scoped token.  
        /// Used to allow the client to prioratize specific tokens over others when Begin order is not sufficient.
        /// <para/>Note: the setter is only usable when the scoped token is in the <see cref="ScopedTokenState.Initial"/> state.
        /// Any attempt to set this property will throw an <see cref="InvalidOperationException"/> if the scoped token's <see cref="State"/> has been changed to any other value, typically through the use of the Begin or End operations.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="Priority"/> property setter can will throw this exception if the <see cref="State"/> is not <see cref="ScopedTokenState.Initial"/>.
        /// </exception>
        /// <remarks>
        /// This property is initialized from the <see cref="IModuleScopedToken.DefaultPriority"/> on construction, or to zero if there is no such <see cref="ModuleScopedToken"/>.
        /// </remarks>
        public uint Priority
        {
            get { return priority; }
            set
            {
                var capturedEntryState = State;

                if (capturedEntryState != ScopedTokenState.Initial)
                    new InvalidOperationException($"The Priority cannot be set when the State is no longer in its Initial value [{capturedEntryState}]").Throw();

                priority = value;
            }
        }
        private uint priority;

        /// <summary>
        /// When non-null, this gives the set of additinal KVC items that will be included in any correspondingly generated <see cref="ICERPEventReport"/> item.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IDictionary<string, ValueContainer> KVCDictionary { get { return _KVCDictionary ?? (_KVCDictionary = new Dictionary<string, ValueContainer>()); } set { _KVCDictionary = value; } }
        private IDictionary<string, ValueContainer> _KVCDictionary;

        /// <summary>
        /// This represents the actual list of KVC values there were found in the <see cref="_KVCDictionary"/> (if any) taken at the start
        /// of the last Begin or End operation.
        /// </summary>
        internal KeyValuePair<string, ValueContainer> [] CapturedKVCSet { get; set; }

        /// <summary>
        /// This method clears the <see cref="CapturedKVCSet"/> (if any) and then copies the contents of the <see cref="_KVCDictionary"/> into it, creating it if needed.
        /// </summary>
        protected void CaptureKVCSet()
        {
            if (!_KVCDictionary.IsNullOrEmpty())
            {
                var _KVCDictionaryCount = _KVCDictionary.Count;

                if (CapturedKVCSet == null || CapturedKVCSet.Length != _KVCDictionaryCount)
                    CapturedKVCSet = _KVCDictionary.SafeToArray();
                else
                    _KVCDictionary.CopyTo(CapturedKVCSet, 0);
            }
            else
            {
                CapturedKVCSet = null;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Note: this method is intentionally interface specific.  
        /// This is done so that derived type specific client code can use the call chain supporting ExtensionMethod variant of this method instead of this one by default.
        /// </remarks>
        void IScopedToken.Begin(TimeSpan? waitTimeLimit, bool rethrow)
        {
            try
            {
                AboutToBegin();

                State = ScopedTokenState.Starting;

                InnerRunScopedOp(ScopedOp.Begin, waitTimeLimit);
            }
            catch (System.Exception ex)
            {
                LocalEmitters.IssueEmitter.Emit("{0} Begin failed [{1}, {2}]", LogTypeName, State, ex.ToString(ExceptionFormat.TypeAndMessage));

                if (rethrow)
                    throw;
            }
        }

        /// <summary>
        /// This method may be overriden in derived classes to support customized behavior immediately before the ScopedBegin action is created and started.
        /// </summary>
        protected virtual void AboutToBegin() 
        {
            CaptureKVCSet();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Note: this method is intentionally interface specific.  
        /// This is done so that derived type specific client code can use the call chain supporting ExtensionMethod variant of this method instead of this one by default.
        /// </remarks>
        void IScopedToken.End(TimeSpan? waitTimeLimit, bool rethrow)
        {
            try
            {
                AboutToEnd();

                State = ScopedTokenState.Ending;

                InnerRunScopedOp(ScopedOp.End, waitTimeLimit);
            }
            catch (System.Exception ex)
            {
                LocalEmitters.IssueEmitter.Emit("{0} End failed [{1}, {2}]", LogTypeName, State, ex.ToString(ExceptionFormat.TypeAndMessage));

                if (rethrow)
                    throw;
            }
        }

        /// <summary>
        /// This method may be overriden in derived classes to support customized behavior immediately before the ScopedEnd action is created and started.
        /// </summary>
        protected virtual void AboutToEnd() 
        {
            CaptureKVCSet();
        }

        /// <summary>
        /// Internal method used to implement common implementation for the IScopedToken.Begin and IScopedToken.End methods.
        /// This method handles the code path variations that relate to the specification and use of the optional <paramref name="waitTimeLimit"/> parameter.
        /// </summary>
        protected void InnerRunScopedOp(ScopedOp scopedOp, TimeSpan? waitTimeLimit)
        {
            var entryState = State;

            QpcTimeStamp qpcTimeStamp = (waitTimeLimit != null) ? QpcTimeStamp.Now : QpcTimeStamp.Zero;

            if (ModuleScopedToken?.CERP == null)
            {
                LocalEmitters.StateEmitter.Emit("{0}: {1} skipped.  ModuleScopedToken or CERP was not provided.", LogTypeName, scopedOp);
                switch (scopedOp)
                {
                    case ScopedOp.Begin: State = ScopedTokenState.Started; break;
                    case ScopedOp.End: State = ScopedTokenState.Ended; break;
                    default: break;
                }
                return;
            }

            var icf = ModuleScopedToken.CERP.ScopedOp(scopedOp, this).StartInline();

            string ec = null;

            if (waitTimeLimit == null)
                ec = icf.WaitUntilComplete();
            else if (icf.WaitUntilComplete(waitTimeLimit ?? TimeSpan.Zero))
                ec = icf.ActionState.ResultCode;

            switch (ec)
            {
                case null:
                    LocalEmitters.StateEmitter.Emit("{0}: {1} wait limit reached after {2:f3} sec [{3}]", LogTypeName, scopedOp, qpcTimeStamp.Age.TotalSeconds, entryState);
                    break;
                case "":
                    LocalEmitters.StateEmitter.Emit("{0}: {1} succeeded [{2}]", LogTypeName, scopedOp, State);
                    break;
                default:
                    new ActionFailedException(ec, icf, icf.ActionState).Throw();
                    break;
            }
        }

        /// <inheritdoc/>
        public SyncFlags BeginSyncFlags { get; protected set; }

        /// <inheritdoc/>
        public SyncFlags EndSyncFlags { get; protected set; }

        /// <inheritdoc/>
        public bool DisableReporting { get; set; }

        internal IScopedTokenLogMessageEmitters LocalEmitters
        {
            get { return _LocalEmitters ?? (_LocalEmitters = ModuleScopedToken?.ScopedTokenLogMessageEmitters ?? fallbackScopedTokenLogMessageEmitter); }
            set { _LocalEmitters = value; }
        }

        private IScopedTokenLogMessageEmitters _LocalEmitters;

        protected static readonly IScopedTokenLogMessageEmitters fallbackScopedTokenLogMessageEmitter = new ScopedTokenLogMessageEmitters(null);
    }

    /// <summary>
    /// This is the <see cref="IScopedToken"/> derived interface this is supported by all module registration scoped tokens.
    /// Clients use these tokens to define the client's start and end deliniation of its use of the scoped token concept with the corresponding CERP that it is using.
    /// These scoped tokens are also used as the base for construction of all of the other scoped tokens that the client uses with this registration and these objects
    /// give these state specific scoped tokens access to a common set of information that they use in their internals.
    /// </summary>
    public interface IModuleScopedToken : IScopedToken
    {
        /// <summary>Gives the name of the module that this token relates to</summary>
        string ModuleName { get; }

        /// <summary>Gives the module instance number that the related <see cref="CERP"/> has assigned to this module, or zero if no such assignment has been performed.</summary>
        int ModuleInstanceNum { get; }

        /// <summary>Gives the <see cref="ICombinedEventReportingPart"/> part instance that is used with the related scoped tokens that a given client module generates and uses.</summary>
        ICombinedEventReportingPart CERP { get; }

        /// <summary>This gives derived scoped token internals access to a set of <see cref="Logging.IMesgEmitter"/> instnaces that it can use to record successful and/or unsuccessful operations.</summary>
        IScopedTokenLogMessageEmitters ScopedTokenLogMessageEmitters { get; }

        /// <summary>Gives the default <see cref="SyncFlags"/> value that this module's scoped tokens will (generlaly) use with ScopedBegin opereations.</summary>
        SyncFlags DefaultScopedBeginSyncFlags { get; }

        /// <summary>Gives the default <see cref="SyncFlags"/> value that this module's scoped tokens will (generlaly) use with ScopedEnd opereations.</summary>
        SyncFlags DefaultScopedEndSyncFlags { get; }

        /// <summary>
        /// When non-empty and when the <see cref="CombinedEventReportingPart"/> (CERP) has been configured with an IVI, the
        /// CERP will create and IVA for the module and every <see cref="ICERPEventReport"/> instance that is generated by state transitions on the module
        /// will also be cloned and set to the corresponding IVA.
        /// </summary>
        /// <remarks>
        /// The default <see cref="ModuleScopedTokenBase"/> constructor sets this name based on the <see cref="ModuleName"/> and the purpose string or log type name is is given.
        /// The caller can override this choice by setting the property to null/empty to disable publication for this module or to an alternate/arbitrary name.
        /// The name is captured and used when the module scoped token Begin operation is performed.
        /// </remarks>
        string IVAName { get; }

        /// <summary>This value is used as the default value for all scoped token instances that are created to reference this module scoped token.</summary>
        uint DefaultPriority { get; }
    }

    public abstract class ModuleScopedTokenBase : ScopedTokenBase, IModuleScopedToken
    {
        protected ModuleScopedTokenBase(string moduleName, ICombinedEventReportingPart cerp, string logTypeName = null, SyncFlags defaultScopedBeginSyncFlags = default, SyncFlags defaultScopedEndSyncFlags = default, string purposeStr = null, SyncFlags moduleBeginAndEndSyncFlagAdditions = SyncFlags.Events, uint defaultPriority = 0)
            : base(logTypeName: logTypeName, defaultPriority: defaultPriority)
        {
            if (moduleName.IsNullOrEmpty())
                new ArgumentException("Given module name must be non-empty (and non-null)", "moduleName").Throw();

            ModuleScopedToken = this;
            ModuleName = moduleName;
            CERP = cerp;

            ScopedTokenLogMessageEmitters = CERP?.GetScopedTokenLogMessageEmitters(ModuleName, purposeStr) ?? fallbackScopedTokenLogMessageEmitter;

            DefaultScopedBeginSyncFlags = defaultScopedBeginSyncFlags;
            DefaultScopedEndSyncFlags = defaultScopedEndSyncFlags;

            BeginSyncFlags = DefaultScopedBeginSyncFlags | moduleBeginAndEndSyncFlagAdditions;
            EndSyncFlags = DefaultScopedEndSyncFlags | moduleBeginAndEndSyncFlagAdditions;

            IVAName = $"{moduleName}.CERP.{purposeStr??LogTypeName}.State";
            // NOTE: this class cannot call Begin because it is a virtual method and will not have a defined final implementation at this point in the instance constrution process.

            DefaultPriority = defaultPriority;
        }

        /// <inheritdoc/>
        public string ModuleName { get; private set; }

        /// <inheritdoc/>
        public int ModuleInstanceNum { get; internal set; }

        /// <inheritdoc/>
        public ICombinedEventReportingPart CERP { get; private set; }

        /// <inheritdoc/>
        public IScopedTokenLogMessageEmitters ScopedTokenLogMessageEmitters { get; private set; }

        /// <inheritdoc/>
        public SyncFlags DefaultScopedBeginSyncFlags { get; set; }

        /// <inheritdoc/>
        public SyncFlags DefaultScopedEndSyncFlags { get; set; }

        /// <inheritdoc/>
        public string IVAName { get; set; }

        /// <inheritdoc/>
        public uint DefaultPriority { get; set; }
    }

    /// <summary>
    /// Gives the current state of a given scoped token instance.
    /// <para/>Initial (default: 0), Starting, Started, Ending, Ended
    /// </summary>
    [DataContract]
    public enum ScopedTokenState : int
    {
        /// <summary>This is the default initial placeholder state for newly constructed ScopedToken objects</summary>
        [EnumMember]
        Initial = 0,

        /// <summary></summary>
        [EnumMember]
        Starting,

        /// <summary></summary>
        [EnumMember]
        Started,

        /// <summary></summary>
        [EnumMember]
        Ending,

        /// <summary></summary>
        [EnumMember]
        Ended,
    }

    /// <summary>
    /// This interface defines the set of LogMessage Emitter resources that a scoped token instance can use to emit related log messages.
    /// Generally the related combined event reporting part will generate at least one such instance for each module type for use in that modules <see cref="IModuleScopedToken"/> instance
    /// and other scoped tokens instances that are dervied from it.  In some cases individual scoped token instances will obtain a use specific instance of this 
    /// interface from the <see cref="ICombinedEventReportingPart"/> that they are linked to that may be token type specific.
    /// </summary>
    public interface IScopedTokenLogMessageEmitters
    {
        /// <summary>Gives the emitter to be used to report issues with the use of a given scoped token.</summary>
        Logging.IMesgEmitter IssueEmitter { get; }

        /// <summary>Gives the emitter to be used to report state changes that are applied to a given scoped token.</summary>
        Logging.IMesgEmitter StateEmitter { get; }
    }

    /// <summary>
    /// Basic class used as default implementation for the <see cref="IScopedTokenLogMessageEmitters"/> interface.
    /// </summary>
    public class ScopedTokenLogMessageEmitters : IScopedTokenLogMessageEmitters
    {
        /// <summary>Constructor.</summary>
        public ScopedTokenLogMessageEmitters(Logging.Logger logger, Logging.MesgType issueEmitterMesgType = Logging.MesgType.Warning, Logging.MesgType stateEmitterMesgType = Logging.MesgType.Debug)
        {
            Logger = logger;
            IssueEmitter = logger?.Emitter(issueEmitterMesgType) ?? Logging.NullEmitter;
            StateEmitter = logger?.Emitter(stateEmitterMesgType) ?? Logging.NullEmitter;
        }

        public Logging.ILogger Logger { get; private set; }

        /// <inheritdoc/>
        public Logging.IMesgEmitter IssueEmitter { get; private set; }

        /// <inheritdoc/>
        public Logging.IMesgEmitter StateEmitter { get; private set; }
    }

    #endregion

    #region Extension Methods

    /// <summary>Extension Methods</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given scoped tokens (<paramref name="a"/> and <paramref name="b"/>) have the same 
        /// non-zero <see cref="IModuleScopedToken.ModuleInstanceNum"/> and if they use the same <see cref="IModuleScopedToken.CERP"/> instance.
        /// </summary>
        public static bool IsSameModuleAs(this IScopedToken a, IScopedToken b)
        {
            var mstA = a.ModuleScopedToken;
            var mstB = b.ModuleScopedToken;

            return (mstA.ModuleInstanceNum != 0 && mstA.ModuleInstanceNum == mstB.ModuleInstanceNum && Object.ReferenceEquals(mstA.CERP, mstB.CERP));
        }

        /// <summary>
        /// This ExtensionMethod provides a call chain supporting variant of the normal <see cref="IScopedToken.Begin(TimeSpan?, bool)"/> method.
        /// Clients use this method to inform the related <see cref="IModuleScopedToken.CERP"/> that the application of the scoped conditions and state information is to begin now.
        /// <para/>This request first sets the state to <see cref="ScopedTokenState.Starting"/>.
        /// The state will remain as Starting until the related <see cref="IModuleScopedToken.CERP"/> has completed the Scoped Begin operation which will set it to <see cref="ScopedTokenState.Started"/>.
        /// While the token in in the Starting state, no other Begin operation may be issued.
        /// </summary>
        /// <param name="scopedToken">Provides the instance on which to apply the Begin operation.</param>
        /// <param name="waitTimeLimit">
        /// When a non-null waitTimeLimit is specified, either directly or through the specific scoped token's default value evaluation logic, it will be used to limit
        /// how long the caller is willing to wait for the completion of the underlying <see cref="ICombinedEventReportingPart.ScopedBegin(IScopedToken)"/> operation.
        /// </param>
        /// <param name="rethrow">
        /// When this property is passed as true and the operation fails, then it will both emit a corresponding issue log message and it will throw a corresponding exception to the caller.
        /// When this property is passed as false and the operation fails, then it will simply emit a corresopnding issue log message but will catch and not propagate any related thrown exceptions.
        /// </param>
        /// <returns>the given <paramref name="scopedToken"/> instance.</returns>
        /// <remarks>
        /// Due to the risk of a scoped token being in a (temporarily) unusable state, it is not recommended to re-use scope token instances when using non-null waitTimeLimit values for this operation.
        /// Reuse of scoped token objects should only be considered by a client if the client always waits for the full completion of all related Begin and End operations so that the token is never left
        /// in the <see cref="ScopedTokenState.Starting"/> or <see cref="ScopedTokenState.Ending"/> states.
        /// </remarks>
        public static TScopedToken Begin<TScopedToken>(this TScopedToken scopedToken, TimeSpan ? waitTimeLimit = null, bool rethrow = false) where TScopedToken : IScopedToken
        {
            ((IScopedToken)scopedToken).Begin(waitTimeLimit: waitTimeLimit, rethrow: rethrow);

            return scopedToken;
        }

        /// <summary>
        /// This ExtensionMethod provides a call chain supporting variant of the normal <see cref="IScopedToken.End(TimeSpan?, bool)"/> method.
        /// Clients use this method to inform the related <see cref="IModuleScopedToken.CERP"/> that the application of the scoped conditions and state information is to end now.
        /// <para/>This request first sets the state to <see cref="ScopedTokenState.Ending"/>.
        /// The state will remain as Ending until the related <see cref="IModuleScopedToken.CERP"/> has completed the Scoped End operation which will set it to <see cref="ScopedTokenState.Ended"/>.
        /// While the token in in the Ending state, no other End or Begin operation may be issued.
        /// </summary>
        /// <param name="scopedToken">Provides the instance on which to apply the Begin operation.</param>
        /// <param name="waitTimeLimit">
        /// When a non-null waitTimeLimit is specified, either directly or through the specific scoped token's default value evaluation logic, it will be used to limit
        /// how long the caller is willing to wait for the completion of the underlying <see cref="ICombinedEventReportingPart.ScopedEnd(IScopedToken)"/> operation.
        /// </param>
        /// <param name="rethrow">
        /// When this property is passed as true and the operation fails, then it will both emit a corresponding issue log message and it will throw a corresponding exception to the caller.
        /// When this property is passed as false and the operation fails, then it will simply emit a corresopnding issue log message but will catch and not propagate any related thrown exceptions.
        /// </param>
        /// <returns>the given <paramref name="scopedToken"/> instance.</returns>
        /// <remarks>
        /// Due to the risk of a scoped token being in a (temporarily) unusable state, it is not recommended to re-use scope token instances when using non-null waitTimeLimit values for this operation.
        /// </remarks>
        public static TScopedToken End<TScopedToken>(this TScopedToken scopedToken, TimeSpan? waitTimeLimit = null, bool rethrow = false) 
            where TScopedToken : IScopedToken
        {
            ((IScopedToken)scopedToken).End(waitTimeLimit: waitTimeLimit, rethrow: rethrow);

            return scopedToken;
        }

        /// <summary>
        /// Variant of normal <see cref="End{TScopedToken}(TScopedToken, TimeSpan?, bool)"/> extension method.
        /// This method will End the <paramref name="scopedToken"/> if its' <see cref="IScopedToken.State"/> is <see cref="ScopedTokenState.Starting"/> or <see cref="ScopedTokenState.Started"/>.
        /// This method will just return the given <paramref name="scopedToken"/> it its' <see cref="IScopedToken.State"/> is either <see cref="ScopedTokenState.Initial"/> or <see cref="ScopedTokenState.Ended"/>.
        /// This method will throw and <see cref="System.InvalidOperationException"/> or use its IssueEmitter to report an issue if the <see cref="IScopedToken.State"/> has any other value.
        /// </summary>
        public static TScopedToken EndIfNeeded<TScopedToken>(this TScopedToken scopedToken, TimeSpan? waitTimeLimit = null, bool rethrow = false)
            where TScopedToken : IScopedToken
        {
            switch (scopedToken.State)
            {
                case ScopedTokenState.Initial: 
                    return scopedToken;

                case ScopedTokenState.Starting:
                case ScopedTokenState.Started: 
                    scopedToken.End(waitTimeLimit: waitTimeLimit, rethrow: rethrow); 
                    return scopedToken;

                case ScopedTokenState.Ended: 
                    return scopedToken;

                case ScopedTokenState.Ending:
                default:
                    {
                        string mesg = $"{Fcns.CurrentMethodName} failed: token state is not valid [{scopedToken}]";

                        if (rethrow)
                            new System.InvalidOperationException(mesg).Throw();

                        IssueEmitter.Emit(mesg);

                        return scopedToken;
                    }
            }
        }

        /// <summary>
        /// This emitter is used whenver any of the extension methods used here needs to report an issue.
        /// It defaults to a Debug emitter that uses source "CERP.ExtensionMethods";
        /// </summary>
        public static Logging.IMesgEmitter IssueEmitter { get; set; } = Logger.Debug;

        /// <summary>
        /// baseline logger that is used by default here.
        /// </summary>
        private static Logging.ILogger Logger 
        {
            get { return logger ?? (logger = new Logging.Logger("CERP.ExtensionMethods")); }
        }

        private static Logging.ILogger logger;
    }

    #endregion

    #region ICombinedEventReportingPart

    /// <summary>
    /// This interface defines the publically available methods (mostly action factory methods) that are supported by CombinedEventReportingPart instances.
    /// This interface supports both the methods that the related Scoped Token instances make use of and methods that are used diretly by clients in specific cases.
    /// </summary>
    public interface ICombinedEventReportingPart : IActivePartBase
    {
        /// <summary>
        /// Action Factory Method.  Requests that the <see cref="ICombinedEventReportingPart"/> run the indicated <paramref name="scopedOp"/> for the given <paramref name="scopedToken"/>Used by <see cref="IScopedToken"/> 
        /// Scoped Token instances use these actions to perform all externally visible state changes and event reporting related to use of their Begin and End method(s).
        /// </summary>
        IClientFacet ScopedOp(ScopedOp scopedOp, IScopedToken scopedToken);

        /// <summary>Action Factory Method.  May be used by clients to synchronize themselves with the internal state of a this part using the given <paramref name="syncFlags"/> value to determine which elements of th parts internal state to synchronize with.</summary>
        IClientFacet Sync(SyncFlags syncFlags = default, string moduleName = null);

        /// <summary>Method used to obtain <see cref="IScopedTokenLogMessageEmitters"/> for a given <paramref name="moduleName"/> and optional <paramref name="purpose"/>.</summary>
        IScopedTokenLogMessageEmitters GetScopedTokenLogMessageEmitters(string moduleName, string purpose = null);
    }

    public enum ScopedOp : int
    {
        None = 0,
        Begin,
        End,
    }

    /// <summary>
    /// This enum is used by a client to specify the type of synchronization steps that they would like performed when performing a related action.
    /// <para/>Default (0x00), Events (0x01)
    /// </summary>
    [Flags]
    public enum SyncFlags : int
    {
        /// <summary>By default sync operations blocks until the target part has completed the next service loop including all related state publication. [0x00]</summary>
        Default = 0x00,

        /// <summary>When selected, related sync operations block until all events that were issued by the end of the related action has been delivered by the related <see cref="ICombinedEventReportingPart"/>. [0x01]</summary>
        Events = 0x01,
    }

    #endregion
}
