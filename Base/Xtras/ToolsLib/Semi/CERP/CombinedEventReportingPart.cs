//-------------------------------------------------------------------
/*! @file CombinedEventReportingPart.cs
 *  @brief This file provides the CombinedEventReportingPart class.
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

using MosaicLib;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

using Mosaic.ToolsLib.Semi.IDSpec;

namespace Mosaic.ToolsLib.Semi.CERP
{
    /// <summary>
    /// This class contains all of the part configuration information that is used to determine the operational behavior of a corresponding <see cref="CombinedEventReportingPart"/> instance.
    /// </summary>
    public class CombinedEventReportingPartConfig : ICopyable<CombinedEventReportingPartConfig>
    {
        public string PartID { get; set; }

        public ActionLoggingConfig ActionLoggingConfig { get; set; } = ActionLoggingConfig.Debug_Error_Trace_Trace;

        public TimeSpan NominalPartWaitTimeLimit { get; set; } = (0.01).FromSeconds();

        public IIDSpecLookupHelper IDSpecLookupHelper { get; set; }

        public EventReportSetHandlerDelegate EventReportSetHandlerDelegate { get; set; }

        public MDRF2.Writer.IMDRF2Writer EventMDRF2Writer { get; set; }

        public string E116EventRecordingKeyNameFormatStr { get; set; } = "{0}.CERP.E116";

        public string E157EventRecordingKeyNameFormatStr { get; set; } = "{0}.CERP.E157";

        public IValuesInterconnection IVI { get; set; }

        public CombinedEventReportingPartConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (CombinedEventReportingPartConfig) MemberwiseClone();
        }
    }

    /// <summary>
    /// This delegate type defines the method signature for any (optional) caller provided method that is used to process (emit) sets of events with associated values.
    /// </summary>
    public delegate void EventReportSetHandlerDelegate(List<ICERPEventReport> eventReportSet, System.Threading.CancellationToken cancellationToken);

    /// <summary>
    /// This is the primary part that support the Combined Event Reporting concept [<see cref="ICombinedEventReportingPart"/>].
    /// This concept currently supports a Scoped Token based means to drive generation of E116 and E157 related events and state changes.
    /// </summary>
    public class CombinedEventReportingPart : SimpleActivePartBase, ICombinedEventReportingPart
    {
        #region Construction and top level logic for ICombinedEventReportingPart support

        public CombinedEventReportingPart(CombinedEventReportingPartConfig partConfig)
            : base(partConfig.PartID, SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior:true, disablePartBaseIVIUse: true, waitTimeLimit: partConfig.NominalPartWaitTimeLimit))
        {
            PartConfig = partConfig.MakeCopyOfThis();

            ActionLoggingReference.Config = PartConfig.ActionLoggingConfig;

            SetupEventDelivery();
        }

        private CombinedEventReportingPartConfig PartConfig { get; set; }

        public IClientFacet ScopedOp(ScopedOp scopedOp, IScopedToken scopedToken)
        {
            return new BasicActionImpl(ActionQueue, ipf => PerformScopedOp(ipf, scopedOp, scopedToken), scopedOp.ToString(), ActionLoggingReference, scopedToken.SafeToString());
        }

        private string PerformScopedOp(IProviderFacet ipf, ScopedOp scopedOp, IScopedToken scopedToken)
        {
            switch (scopedToken)
            {
                case E157.E157ModuleScopedToken e157ModuleToken: return PerformScopedOp(ipf, scopedOp, e157ModuleToken);
                case E157.E157GeneralExcutionScopedToken e157GeneralExecutionToken: return PerformScopedOp(ipf, scopedOp, e157GeneralExecutionToken);
                case E157.E157StepActiveScopedToken e157StepActiveToken: return PerformScopedOp(ipf, scopedOp, e157StepActiveToken);
                case E116.E116ModuleScopedToken e116moduleToken: return PerformScopedOp(ipf, scopedOp, e116moduleToken);
                case E116.E116BusyScopedToken e116BusyToken: return PerformScopedOp(ipf, scopedOp, e116BusyToken);
                case E116.E116BlockedScopedToken e116BlockedToken: return PerformScopedOp(ipf, scopedOp, e116BlockedToken);
                default: return $"'{scopedToken.SafeGetInstanceType().GetTypeDigestName()}' is not a supported scoped token type here";
            }
        }

        public IClientFacet Sync(SyncFlags syncFlags = SyncFlags.Default, string moduleName = null)
        {
            return new BasicActionImpl(ActionQueue, ipf => PerformSync(ipf, syncFlags), "Sync", ActionLoggingReference, $"[{syncFlags}]");
        }

        private string PerformSync(IProviderFacet ipf, SyncFlags syncFlags)
        {
            PerformMainLoopService();

            var syncEvents = (syncFlags & SyncFlags.Events) != 0;

            if (syncEvents)
                return QueueSyncEventAction(ipf);

            return string.Empty;
        }

        public IScopedTokenLogMessageEmitters GetScopedTokenLogMessageEmitters(string moduleName, string purpose = null)
        {
            IScopedTokenLogMessageEmitters stlme = null;
            if (purpose != null)
                stlme = roModuleAndPurposeToEmitterDictionary.SafeTryGetValue((moduleName, purpose));

            if (stlme == null)
                stlme = roModuleToEmittersDictionary.SafeTryGetValue(moduleName);

            if (stlme == null)
            {
                // should we use a different action logging refrence here?
                var icf = new BasicActionImpl(ActionQueue, ipf => PerformGetScopedTokenLogMessageEmitters(ipf, moduleName, purpose), "GetScopedTokenLogMessageEmitters", ActionLoggingReference, $"['{moduleName}' '{purpose}']").RunInline();

                stlme = icf.ActionState.NamedValues["IScopedTokenLogMessageEmitters"].VC.GetValue<IScopedTokenLogMessageEmitters>(rethrow: false);
            }

            return stlme ?? fallbackScopedTokenLogMessageEmitter;
        }

        private string PerformGetScopedTokenLogMessageEmitters(IProviderFacet ipf, string moduleName, string purpose)
        {
            IScopedTokenLogMessageEmitters stlme;

            if (purpose == null)
            {
                stlme = new ScopedTokenLogMessageEmitters(new Logging.Logger($"CERP.{moduleName}"));

                moduleToEmittersDictionary[moduleName] = stlme;
                roModuleToEmittersDictionary = moduleToEmittersDictionary.ConvertToReadOnly();
            }
            else
            {
                stlme = new ScopedTokenLogMessageEmitters(new Logging.Logger($"CERP.{purpose}.{moduleName}"));

                moduleAndPurposeToEmitterDictionary[(moduleName, purpose)] = stlme;
                roModuleAndPurposeToEmitterDictionary = moduleAndPurposeToEmitterDictionary.ConvertToReadOnly();
            }

            ipf.CompleteRequest("", new NamedValueSet() { { "IScopedTokenLogMessageEmitters", stlme } }.MakeReadOnly());

            return null;
        }

        private IScopedTokenLogMessageEmitters fallbackScopedTokenLogMessageEmitter = new ScopedTokenLogMessageEmitters(null);

        private ReadOnlyIDictionary<string, IScopedTokenLogMessageEmitters> roModuleToEmittersDictionary = ReadOnlyIDictionary<string, IScopedTokenLogMessageEmitters>.Empty;
        private ReadOnlyIDictionary<(string, string), IScopedTokenLogMessageEmitters> roModuleAndPurposeToEmitterDictionary = ReadOnlyIDictionary<(string, string), IScopedTokenLogMessageEmitters>.Empty;

        private Dictionary<string, IScopedTokenLogMessageEmitters> moduleToEmittersDictionary = new Dictionary<string, IScopedTokenLogMessageEmitters>();
        private Dictionary<(string, string), IScopedTokenLogMessageEmitters> moduleAndPurposeToEmitterDictionary = new Dictionary<(string, string), IScopedTokenLogMessageEmitters>();

        #endregion

        #region E157 PerformScopedOp variants

        private INamedValueSet e157IDSpecNVS = new NamedValueSet() { "E157" }.MakeReadOnly();

        private string PerformScopedOp(IProviderFacet ipf, ScopedOp scopedOp, E157.E157ModuleScopedToken moduleToken)
        {
            E157.E157EventRecord eventRecord = default;
            SyncFlags syncFlags;
            ScopedTokenState nextScopedOpState;
            E157ModuleTracker mt;

            switch (scopedOp)
            {
                case CERP.ScopedOp.Begin:
                    {
                        var moduleName = moduleToken.ModuleName;

                        mt = e157ModuleTrackerByNameDictionary.SafeTryGetValue(moduleName);

                        if (mt != null)
                            return $"{scopedOp} {moduleToken} failed: module already registered";

                        // create a tracker and attempt to look the module instance number for it
                        var moduleIDSpecPair = PartConfig.IDSpecLookupHelper.GetNameIIDSpecPair(moduleName, IDSpec.IDType.ModuleID, e116IDSpecNVS);
                        var moduleIDSpec = moduleIDSpecPair.IDSpec;

                        var moduleInstanceNum = moduleIDSpec?.ID ?? -1;

                        if (e157ModuleTrackerByInstanceNumDictionary.ContainsKey(moduleInstanceNum))
                            return $"{scopedOp} {moduleToken} failed: found module instance num is not unique";

                        if (moduleInstanceNum <= 0)
                            moduleInstanceNum = -1 * (e116ModuleTrackerByInstanceNumDictionary.Count + 1);

                        moduleToken.ModuleInstanceNum = moduleInstanceNum;

                        if (moduleToken.ModuleConfig.E116ModuleScopedToken != null)
                        {
                            string ec = PerformScopedOp(null, CERP.ScopedOp.Begin, moduleToken.ModuleConfig.E116ModuleScopedToken);
                            if (ec.IsNeitherNullNorEmpty())
                                return ec;
                        }

                        var recordingKeyName = PartConfig.E157EventRecordingKeyNameFormatStr.CheckedFormat(moduleName);
                        var recordingKeyID = PartConfig.EventMDRF2Writer?.RegisterAndGetKeyID(recordingKeyName) ?? default;

                        mt = new E157ModuleTracker(moduleIDSpecPair, (recordingKeyID, recordingKeyName), moduleToken, PartConfig);

                        mt.ModuleProcessState = MosaicLib.Semi.E157.ModuleProcessState.NotExecuting;
                        mt.PrevModuleProcessState = MosaicLib.Semi.E157.ModuleProcessState.NoState;

                        e157ModuleTrackerByInstanceNumDictionary[moduleInstanceNum] = mt;
                        e157ModuleTrackerByNameDictionary[moduleName] = mt;

                        eventRecord = E157.E157EventRecord.Pool.GetFreeObjectFromPool();

                        eventRecord.Transition = MosaicLib.Semi.E157.ModuleProcessStateTransition.Initial;

                        syncFlags = moduleToken.BeginSyncFlags;

                        nextScopedOpState = ScopedTokenState.Started;

                        break;
                    }

                case CERP.ScopedOp.End:
                    {
                        mt = e157ModuleTrackerByInstanceNumDictionary.SafeTryGetValue(moduleToken.ModuleInstanceNum);
                        if (mt == null)
                            return $"{scopedOp} {moduleToken} failed: module not found";

                        e157ModuleTrackerByInstanceNumDictionary.Remove(mt.ModuleToken.ModuleInstanceNum);
                        e157ModuleTrackerByNameDictionary.Remove(mt.ModuleToken.ModuleName);

                        if (moduleToken.ModuleConfig.E116ModuleScopedToken != null)
                        {
                            string ec = PerformScopedOp(null, CERP.ScopedOp.End, moduleToken.ModuleConfig.E116ModuleScopedToken);
                            if (ec.IsNeitherNullNorEmpty())
                                return ec;
                        }

                        syncFlags = moduleToken.EndSyncFlags;

                        nextScopedOpState = ScopedTokenState.Ended;

                        break;
                    }

                default: return $"Op '{scopedOp}' is not supported here";
            }

            if (eventRecord != default)
            {
                if (moduleToken.CapturedKVCSet != null)
                    eventRecord.KVCSet.AddRange(moduleToken.CapturedKVCSet);

                AddStandardE157Values(mt, eventRecord, moduleToken);

                if (mt.IVA != null)
                    mt.IVA.Set(eventRecord.MakeCopyOfThis());

                EnqueueEventItemForDelivery(eventRecord);
            }

            PerformMainLoopService();

            moduleToken.State = nextScopedOpState;

            if ((syncFlags & SyncFlags.Events) != 0 && ipf != null)
                return QueueSyncEventAction(ipf);

            return string.Empty;
        }

        private string PerformScopedOp(IProviderFacet ipf, ScopedOp scopedOp, E157.E157GeneralExcutionScopedToken generalExecutionToken)
        {
            var mt = e157ModuleTrackerByInstanceNumDictionary.SafeTryGetValue(generalExecutionToken.ModuleScopedToken.ModuleInstanceNum);
            if (mt == null)
                return $"{scopedOp} {generalExecutionToken} failed: module not found";

            E157.E157EventRecord eventRecord = default;
            SyncFlags syncFlags;
            ScopedTokenState nextScopedOpState;

            switch (scopedOp)
            {
                case CERP.ScopedOp.Begin:
                    {
                        if (mt.ActiveGeneralExcutionScopedToken != null)
                            return $"{scopedOp} {generalExecutionToken} failed: another general excution scoped token is already active [{mt.ActiveGeneralExcutionScopedToken}]";

                        if (generalExecutionToken.E116BusyScopedToken != null)
                        {
                            var ec = PerformScopedOp(null, CERP.ScopedOp.Begin, generalExecutionToken.E116BusyScopedToken);
                            if (ec.IsNeitherNullNorEmpty())
                                return ec;
                        }

                        mt.PrevModuleProcessState = mt.ModuleProcessState;
                        mt.ModuleProcessState = MosaicLib.Semi.E157.ModuleProcessState.GeneralExecution;

                        eventRecord = E157.E157EventRecord.Pool.GetFreeObjectFromPool();

                        eventRecord.Transition = MosaicLib.Semi.E157.ModuleProcessStateTransition.ExecutionStarted;

                        mt.ActiveGeneralExcutionScopedToken = generalExecutionToken;

                        syncFlags = generalExecutionToken.BeginSyncFlags;

                        nextScopedOpState = ScopedTokenState.Started;

                        break;
                    }

                case CERP.ScopedOp.End:
                    {
                        if (mt.ActiveGeneralExcutionScopedToken != generalExecutionToken)
                            return $"{scopedOp} {generalExecutionToken} failed: the given general excution scoped token is not the active one [{mt.ActiveGeneralExcutionScopedToken}]";

                        if (mt.ActiveStepActiveScopedToken != null)
                        {
                            Log.Warning.Emit($"{scopedOp} {generalExecutionToken} issue: there is an step active scoped token that is still active [implicitly ending it now]");
                            PerformScopedOp(null, CERP.ScopedOp.End, mt.ActiveStepActiveScopedToken);
                        }

                        mt.PrevModuleProcessState = mt.ModuleProcessState;
                        mt.ModuleProcessState = MosaicLib.Semi.E157.ModuleProcessState.NotExecuting;

                        eventRecord = E157.E157EventRecord.Pool.GetFreeObjectFromPool();

                        if (generalExecutionToken.GeneralExecutionSucceeded)
                            eventRecord.Transition = MosaicLib.Semi.E157.ModuleProcessStateTransition.ExecutionCompleted;
                        else
                            eventRecord.Transition = MosaicLib.Semi.E157.ModuleProcessStateTransition.ExecutionFailed;

                        mt.ActiveGeneralExcutionScopedToken = null;

                        syncFlags = generalExecutionToken.EndSyncFlags;

                        nextScopedOpState = ScopedTokenState.Ended;

                        break;
                    }

                default: return $"Op '{scopedOp}' is not supported here";
            }

            if (eventRecord != default)
            {
                if (generalExecutionToken.CapturedKVCSet != null)
                    eventRecord.KVCSet.AddRange(generalExecutionToken.CapturedKVCSet);

                AddStandardE157Values(mt, eventRecord, generalExecutionToken);

                if (mt.IVA != null)
                    mt.IVA.Set(eventRecord.MakeCopyOfThis());

                EnqueueEventItemForDelivery(eventRecord);
            }

            if (generalExecutionToken.E116BusyScopedToken != null && scopedOp == CERP.ScopedOp.End)
            {
                var ec = PerformScopedOp(null, CERP.ScopedOp.End, generalExecutionToken.E116BusyScopedToken);

                if (ec.IsNeitherNullNorEmpty())
                {
                    generalExecutionToken.State = nextScopedOpState;
                    return ec;
                }
            }

            generalExecutionToken.State = nextScopedOpState;

            if ((syncFlags & SyncFlags.Events) != 0 && ipf != null)
                return QueueSyncEventAction(ipf);

            return string.Empty;
        }

        private string PerformScopedOp(IProviderFacet ipf, ScopedOp scopedOp, E157.E157StepActiveScopedToken stepActiveToken)
        {
            var mt = e157ModuleTrackerByInstanceNumDictionary.SafeTryGetValue(stepActiveToken.ModuleScopedToken.ModuleInstanceNum);
            if (mt == null)
                return $"{scopedOp} {stepActiveToken} failed: module not found";

            E157.E157EventRecord eventRecord = default;
            SyncFlags syncFlags;
            ScopedTokenState nextScopedOpState;

            switch (scopedOp)
            {
                case CERP.ScopedOp.Begin:
                    {
                        if (mt.ActiveStepActiveScopedToken != null)
                            return $"{scopedOp} {stepActiveToken} failed: another step active scoped token is already active [{mt.ActiveStepActiveScopedToken}]";

                        if (mt.ActiveGeneralExcutionScopedToken != stepActiveToken.GeneralExcutionScopedToken)
                            return $"{scopedOp} {stepActiveToken} failed: the step active scoped token's general execution token is not already active";

                        mt.PrevModuleProcessState = mt.ModuleProcessState;
                        mt.ModuleProcessState = MosaicLib.Semi.E157.ModuleProcessState.StepActive;

                        eventRecord = E157.E157EventRecord.Pool.GetFreeObjectFromPool();

                        eventRecord.Transition = MosaicLib.Semi.E157.ModuleProcessStateTransition.StepStarted;

                        mt.ActiveStepActiveScopedToken = stepActiveToken;

                        syncFlags = stepActiveToken.BeginSyncFlags;

                        nextScopedOpState = ScopedTokenState.Started;

                        break;
                    }

                case CERP.ScopedOp.End:
                    {
                        if (mt.ActiveStepActiveScopedToken != stepActiveToken)
                            return $"{scopedOp} {stepActiveToken} failed: the given step active scoped token is not the active one [{mt.ActiveStepActiveScopedToken}]";

                        mt.PrevModuleProcessState = mt.ModuleProcessState;
                        mt.ModuleProcessState = MosaicLib.Semi.E157.ModuleProcessState.GeneralExecution;

                        eventRecord = E157.E157EventRecord.Pool.GetFreeObjectFromPool();

                        if (stepActiveToken.StepSucceeded)
                            eventRecord.Transition = MosaicLib.Semi.E157.ModuleProcessStateTransition.StepCompleted;
                        else
                            eventRecord.Transition = MosaicLib.Semi.E157.ModuleProcessStateTransition.StepFailed;

                        mt.ActiveStepActiveScopedToken = null;

                        syncFlags = stepActiveToken.EndSyncFlags;

                        nextScopedOpState = ScopedTokenState.Ended;

                        break;
                    }

                default: return $"Op '{scopedOp}' is not supported here";
            }

            if (eventRecord != default)
            {
                if (stepActiveToken.CapturedKVCSet != null)
                    eventRecord.KVCSet.AddRange(stepActiveToken.CapturedKVCSet);

                AddStandardE157Values(mt, eventRecord, stepActiveToken);

                if (mt.IVA != null)
                    mt.IVA.Set(eventRecord.MakeCopyOfThis());

                EnqueueEventItemForDelivery(eventRecord);
            }

            stepActiveToken.State = nextScopedOpState;

            if ((syncFlags & SyncFlags.Events) != 0 && ipf != null)
                return QueueSyncEventAction(ipf);

            return string.Empty;
        }

        private void AddStandardE157Values(E157ModuleTracker mt, E157.E157EventRecord eventRecord, E157.E157ModuleScopedToken moduleScopedToken)
        {
            var moduleIDSpecs = mt.ModuleIDSpecPair;
            var moduleConfig = moduleScopedToken.ModuleConfig;

            eventRecord.Module = mt.ModuleIDSpecPair;
            eventRecord.RecordingKeyInfo = mt.RecordingKeyInfo;
            eventRecord.State = mt.ModuleProcessState;
            eventRecord.PrevState = mt.PrevModuleProcessState;

            foreach (var slo in mt.substLocObsSet)
                eventRecord.E090SubstEventInfoList.Add(new MosaicLib.Semi.E090.E090SubstEventInfo(slo.UpdateInline().ContainsSubstInfo));
        }

        private void AddStandardE157Values(E157ModuleTracker mt, E157.E157EventRecord eventRecord, E157.E157GeneralExcutionScopedToken generalExcutionScopedToken)
        {
            AddStandardE157Values(mt, eventRecord, generalExcutionScopedToken.E157ModuleScopedToken);

            var firstSubstEventInfo = eventRecord.E090SubstEventInfoList.FirstOrDefault();

            eventRecord.RCID = generalExcutionScopedToken.RCID;
            eventRecord.RecID = generalExcutionScopedToken.RecID ?? firstSubstEventInfo.ProcessJobRecipeName ?? firstSubstEventInfo.PPID;
            eventRecord.ProcessJobID = generalExcutionScopedToken.ProcessJobID ?? firstSubstEventInfo.ProcessJobID;
            eventRecord.RecipeParameters = generalExcutionScopedToken.RecipeParameters.ConvertToReadOnly(mapNullToEmpty: false);
        }

        private void AddStandardE157Values(E157ModuleTracker mt, E157.E157EventRecord eventRecord, E157.E157StepActiveScopedToken stepActiveToken)
        {
            AddStandardE157Values(mt, eventRecord, stepActiveToken.GeneralExcutionScopedToken);

            eventRecord.StepID = stepActiveToken.StepID;
            eventRecord.StepCount = stepActiveToken.StepCount;
        }

        private class E157ModuleTracker
        {
            public E157ModuleTracker(NameIIDSpecPair moduleIDSpecPair, (int id, string name) recordingKeyInfo, E157.E157ModuleScopedToken moduleToken, CombinedEventReportingPartConfig partConfig)
            {
                ModuleIDSpecPair = moduleIDSpecPair;
                RecordingKeyInfo = recordingKeyInfo;

                ModuleToken = moduleToken;
                ModuleConfig = moduleToken.ModuleConfig.MakeCopyOfThis();

                if (ModuleConfig.ModuleSubstLocSet == null && ModuleConfig.E116ModuleScopedToken?.ModuleConfig.ModuleSubstLocSet != null)
                    ModuleConfig.ModuleSubstLocSet = ModuleConfig.E116ModuleScopedToken.ModuleConfig.ModuleSubstLocSet;

                substLocObsSet = (ModuleConfig.ModuleSubstLocSet?.Select(substLocID => new MosaicLib.Semi.E090.E090SubstLocObserver(substLocID))).SafeToArray();

                if (moduleToken.IVAName.IsNeitherNullNorEmpty() && partConfig.IVI != null)
                    IVA = partConfig.IVI.GetValueAccessor(moduleToken.IVAName);
            }

            public NameIIDSpecPair ModuleIDSpecPair { get; private set; }

            public (int id, string name) RecordingKeyInfo { get; private set; }

            public E157.E157ModuleScopedToken ModuleToken { get; private set; }

            public E157.E157ModuleConfig ModuleConfig { get; private set; }

            public IValueAccessor IVA { get; private set; }

            public readonly MosaicLib.Semi.E090.E090SubstLocObserver[] substLocObsSet;

            public MosaicLib.Semi.E157.ModuleProcessState ModuleProcessState { get; set; }
            public MosaicLib.Semi.E157.ModuleProcessState PrevModuleProcessState { get; set; }

            public E157.E157GeneralExcutionScopedToken ActiveGeneralExcutionScopedToken { get; set; }

            public E157.E157StepActiveScopedToken ActiveStepActiveScopedToken { get; set; }
        }

        private readonly IDictionaryWithCachedArrays<int, E157ModuleTracker> e157ModuleTrackerByInstanceNumDictionary = new IDictionaryWithCachedArrays<int, E157ModuleTracker>();
        private readonly Dictionary<string, E157ModuleTracker> e157ModuleTrackerByNameDictionary = new Dictionary<string, E157ModuleTracker>();

        #endregion

        #region E116 PerformScopedOp variants

        private INamedValueSet e116IDSpecNVS = new NamedValueSet() { "E116" }.MakeReadOnly();

        private string PerformScopedOp(IProviderFacet ipf, ScopedOp scopedOp, E116.E116ModuleScopedToken moduleToken)
        {
            SyncFlags syncFlags;
            ScopedTokenState nextScopedOpState;

            switch (scopedOp)
            {
                case CERP.ScopedOp.Begin:
                    {
                        var moduleName = moduleToken.ModuleName;

                        var mt = e116ModuleTrackerByNameDictionary.SafeTryGetValue(moduleName);

                        if (mt != null)
                            return $"{scopedOp} {moduleToken} failed: module already registered";

                        // create a tracker and attempt to look the module instance number for it
                        var moduleIDSpecPair = PartConfig.IDSpecLookupHelper.GetNameIIDSpecPair(moduleName, IDSpec.IDType.ModuleID, e116IDSpecNVS);
                        var moduleIDSpec = moduleIDSpecPair.IDSpec;

                        var moduleInstanceNum = moduleIDSpec?.ID ?? -1;

                        if (e116ModuleTrackerByInstanceNumDictionary.ContainsKey(moduleInstanceNum))
                            return $"{scopedOp} {moduleToken} failed: found module instance num is not unique";

                        if (moduleInstanceNum <= 0)
                            moduleInstanceNum = -1 * (e116ModuleTrackerByInstanceNumDictionary.Count + 1);

                        moduleToken.ModuleInstanceNum = moduleInstanceNum;

                        var recordingKeyName = PartConfig.E116EventRecordingKeyNameFormatStr.CheckedFormat(moduleName);
                        var recordingKeyID = PartConfig.EventMDRF2Writer?.RegisterAndGetKeyID(recordingKeyName) ?? default;

                        mt = new E116ModuleTracker(moduleIDSpecPair, (recordingKeyID, recordingKeyName), moduleToken, enqueueEventItemDelegate, PartConfig)
                            .Setup(QpcTimeStamp.Now);

                        if (mt.ModuleConfig.SourcePart != null)
                            mt.ModuleConfig.SourcePart.BaseStateNotifier.NotificationList.AddItem(this);

                        e116ModuleTrackerByInstanceNumDictionary[moduleInstanceNum] = mt;
                        e116ModuleTrackerByNameDictionary[moduleName] = mt;

                        syncFlags = moduleToken.BeginSyncFlags;

                        nextScopedOpState = ScopedTokenState.Started;

                        break;
                    }

                case CERP.ScopedOp.End:
                    {
                        var mt = e116ModuleTrackerByInstanceNumDictionary.SafeTryGetValue(moduleToken.ModuleInstanceNum);
                        if (mt == null)
                            return $"{scopedOp} {moduleToken} failed: module not found";

                        if (mt.ModuleConfig.SourcePart != null)
                            mt.ModuleConfig.SourcePart.BaseStateNotifier.NotificationList.RemoveItem(this);

                        e116ModuleTrackerByInstanceNumDictionary.Remove(mt.ModuleToken.ModuleInstanceNum);
                        e116ModuleTrackerByNameDictionary.Remove(mt.ModuleToken.ModuleName);

                        syncFlags = moduleToken.EndSyncFlags;

                        nextScopedOpState = ScopedTokenState.Ended;

                        break;
                    }

                default: 
                    return $"Op '{scopedOp}' is not supported here";
            }

            PerformMainLoopService();

            moduleToken.State = nextScopedOpState;

            if ((syncFlags & SyncFlags.Events) != 0 && ipf != null)
                return QueueSyncEventAction(ipf);

            return string.Empty;
        }

        private string PerformScopedOp(IProviderFacet ipf, ScopedOp scopedOp, E116.E116BusyScopedToken busyToken)
        {
            var mt = e116ModuleTrackerByInstanceNumDictionary.SafeTryGetValue(busyToken.ModuleScopedToken.ModuleInstanceNum);
            if (mt == null)
                return $"{scopedOp} {busyToken} failed: module not found";

            SyncFlags syncFlags;
            ScopedTokenState nextScopedOpState;

            switch (scopedOp)
            {
                case CERP.ScopedOp.Begin:
                    mt.Add(busyToken);
                    syncFlags = busyToken.BeginSyncFlags;

                    nextScopedOpState = ScopedTokenState.Started;

                    break;

                case CERP.ScopedOp.End:
                    if (!mt.Remove(busyToken))
                        return $"{scopedOp} {busyToken} failed: the given scoped token was not found in the active list.";

                    syncFlags = busyToken.EndSyncFlags;

                    if (!busyToken.CapturedEndWorkCountIncrementOnSuccess.IsEmpty)
                    {
                        if (mt.eptState != MosaicLib.Semi.E116.EPTState.Blocked)
                            mt.AccumulatedWorkCountIncrementOnSuccess = mt.AccumulatedWorkCountIncrementOnSuccess.Sum(busyToken.CapturedEndWorkCountIncrementOnSuccess, allowUpcastAttempts: true, rethrow: false);
                    }

                    nextScopedOpState = ScopedTokenState.Ended;

                    break;

                default: 
                    return $"Op '{scopedOp}' is not supported here";
            }

            mt.Service(QpcTimeStamp.Now);

            busyToken.State = nextScopedOpState;

            if ((syncFlags & SyncFlags.Events) != 0 && ipf != null)
                return QueueSyncEventAction(ipf);

            return string.Empty;
        }

        private string PerformScopedOp(IProviderFacet ipf, ScopedOp scopedOp, E116.E116BlockedScopedToken blockedToken)
        {
            var mt = e116ModuleTrackerByInstanceNumDictionary.SafeTryGetValue(blockedToken.ModuleScopedToken.ModuleInstanceNum);
            if (mt == null)
                return $"{scopedOp} {blockedToken} failed: module not found";

            SyncFlags syncFlags;
            ScopedTokenState nextScopedOpState;

            switch (scopedOp)
            {
                case CERP.ScopedOp.Begin:
                    mt.Add(blockedToken);
                    syncFlags = blockedToken.BeginSyncFlags;

                    nextScopedOpState = ScopedTokenState.Started;

                    break;
                case CERP.ScopedOp.End: 
                    if (!mt.Remove(blockedToken))
                        return $"{scopedOp} {blockedToken} failed: the given scoped token was not found in the active list.";

                    syncFlags = blockedToken.EndSyncFlags;

                    nextScopedOpState = ScopedTokenState.Ended;

                    break;

                default: 
                    return $"Op '{scopedOp}' is not supported here";
            }

            mt.Service(QpcTimeStamp.Now);

            blockedToken.State = nextScopedOpState;

            if ((syncFlags & SyncFlags.Events) != 0 && ipf != null)
                return QueueSyncEventAction(ipf);

            return string.Empty;
        }

        private void ServiceE116Trackers()
        {
            var qpcTimeStamp = QpcTimeStamp.Now;

            foreach (var e116ModuleTracker in e116ModuleTrackerByInstanceNumDictionary.ValueArray)
                e116ModuleTracker.Service(qpcTimeStamp);
        }

        private readonly IDictionaryWithCachedArrays<int, E116ModuleTracker> e116ModuleTrackerByInstanceNumDictionary = new IDictionaryWithCachedArrays<int, E116ModuleTracker>();
        private readonly Dictionary<string, E116ModuleTracker> e116ModuleTrackerByNameDictionary = new Dictionary<string, E116ModuleTracker>();

        #endregion

        #region PerformMainLoopService

        protected override void PerformMainLoopService()
        {
            ServiceE116Trackers();

            ServiceEventDelivery();
            ServicePendingEventDeliveryItems();

            base.PerformMainLoopService();
        }

        #endregion

        #region Event delivery

        private void SetupEventDelivery()
        {
            enqueueEventItemDelegate = EnqueueEventItemForDelivery;

            AddMainThreadStoppingAction(StopEventDelivery);
        }

        private delegate void EnqueueEventItemDelegate(ICERPEventReport eventReportItem);

        private EnqueueEventItemDelegate enqueueEventItemDelegate;

        private void EnqueueEventItemForDelivery(ICERPEventReport eventReportItem)
        {
            queuedEventItemList.Add(eventReportItem);
            queuedEventItemListSeqNum = ++eventItemSeqNumGen;
        }

        private ulong eventItemSeqNumGen = 0;

        private List<ICERPEventReport> queuedEventItemList = new List<ICERPEventReport>();

        private ulong queuedEventItemListSeqNum;

        private ulong lastDeliveredEventItemSeqNum;

        private List<(ulong seqNum, IProviderFacet ipf)> pendingEventDeliveryItemList = new List<(ulong seqNum, IProviderFacet ipf)>();

        private System.Threading.CancellationTokenSource cancellationTokenSource = new System.Threading.CancellationTokenSource();
        private System.Threading.Tasks.Task eventDeliveryTask = null;
        private List<ICERPEventReport> postedEventReportItemList = new List<ICERPEventReport>();
        private ulong postedEventReportItemListSeqNum;

        private volatile bool eventDeliveryTaskAlmostComplete = false;


        private void StopEventDelivery()
        {
            using (var timerTrace = new Logging.TimerTrace(Log.Trace, "StopEventDelivery"))
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    Log.Debug.Emit("StopEventDelivery: triggering cancellation token source");
                    cancellationTokenSource.Cancel();
                }

                try
                {
                    eventDeliveryTask?.Wait();
                    eventDeliveryTask = null;
                }
                catch (System.Exception ex)
                {
                    Log.Warning.Emit($"StopEventDelivery failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }
            }
        }

        private void ServiceEventDelivery()
        {
            if (eventDeliveryTask != null && (eventDeliveryTask.IsCompleted || eventDeliveryTaskAlmostComplete))
            {
                bool taskComplete = false;
                try
                {
                    if (eventDeliveryTaskAlmostComplete)
                    {
                        taskComplete = eventDeliveryTask.Wait((0.1).FromSeconds());
                    }
                    else
                    {
                        eventDeliveryTask.Wait();
                        taskComplete = true;
                    }

                    // only return eventReportItems to their respective pools if the task is complete and it completed the Wait call successfully -
                    // otherwise do not return the items to the pool since their final disposition cannot be known.
                    if (taskComplete)
                    {
                        // return eventReportItms to the pool.
                        foreach (var eventReportItem in postedEventReportItemList)
                        {
                            (eventReportItem as CERPEventReportBase)?.Release();
                        }

                        postedEventReportItemList.Clear();
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Warning.Emit($"EventDeliveryTask failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }

                if (taskComplete)
                {
                    eventDeliveryTask = null;
                    lastDeliveredEventItemSeqNum = postedEventReportItemListSeqNum;

                    eventDeliveryTaskAlmostComplete = false;
                }
            }

            if (eventDeliveryTask == null && queuedEventItemListSeqNum > 0 && !HasStopBeenRequested)
            {
                postedEventReportItemList.AddRange(queuedEventItemList);
                postedEventReportItemListSeqNum = queuedEventItemListSeqNum;

                queuedEventItemList.Clear();
                queuedEventItemListSeqNum = 0;

                var cancellationToken = cancellationTokenSource.Token;

                eventDeliveryTaskAlmostComplete = false;

                eventDeliveryTask = new System.Threading.Tasks.Task(() => HandlePostedEventSet(cancellationToken), cancellationToken)
                    .StartTaskInline();
            }
        }

        private void HandlePostedEventSet(System.Threading.CancellationToken cancellationToken)        
        {
            using (var timerTrace1 = new Logging.TimerTrace(Log.Trace, "HandlePostedEventSet"))
            {
                foreach (var eventReportItem in postedEventReportItemList)
                {
                    perCallList.Clear();
                    perCallList.Add(eventReportItem);

                    if (!eventReportItem.ReportBeforeRecording)
                    {
                        using (var timerTrace2 = new Logging.TimerTrace(Log.Trace, "HandlePostedEventSet.Record.A"))
                        {
                            PartConfig.EventMDRF2Writer?.RecordObject(eventReportItem);
                        }

                        using (var timerTrace3 = new Logging.TimerTrace(Log.Trace, "HandlePostedEventSet.Deliver.A"))
                        {
                            PartConfig.EventReportSetHandlerDelegate?.Invoke(perCallList, cancellationToken);
                        }
                    }
                    else
                    {
                        using (var timerTrace4 = new Logging.TimerTrace(Log.Trace, "HandlePostedEventSet.Deliver.B"))
                        {
                            PartConfig.EventReportSetHandlerDelegate?.Invoke(perCallList, cancellationToken);
                        }

                        using (var timerTrace5 = new Logging.TimerTrace(Log.Trace, "HandlePostedEventSet.Record.B"))
                        {
                            PartConfig.EventMDRF2Writer?.RecordObject(eventReportItem);
                        }
                    }

                    if (cancellationToken.IsCancellationRequested)
                        new System.Threading.Tasks.TaskCanceledException().Throw();
                }

                eventDeliveryTaskAlmostComplete = true;

                Notify();
            }
        }

        List<ICERPEventReport> perCallList = new List<ICERPEventReport>();

        private void ServicePendingEventDeliveryItems()
        {
            if (pendingEventDeliveryItemList.Count <= 0)
                return;

            for (; ; )
            {
                var firstPendingItem = pendingEventDeliveryItemList[0];

                if (lastDeliveredEventItemSeqNum < firstPendingItem.seqNum)
                    break;

                firstPendingItem.ipf.CompleteRequest(string.Empty);
                pendingEventDeliveryItemList.RemoveAt(0);

                if (pendingEventDeliveryItemList.Count <= 0)
                    break;
            }

            bool anyPendingCancelRequests = false;

            foreach (var pendingItem in pendingEventDeliveryItemList)
                anyPendingCancelRequests |= pendingItem.ipf.IsCancelRequestActive;

            if (anyPendingCancelRequests || HasStopBeenRequested)
            {
                // handle cancelation requests
                foreach (var pendingCancelItem in pendingEventDeliveryItemList.FilterAndRemove(item => item.ipf.IsCancelRequestActive).SafeToArray())
                {
                    pendingCancelItem.ipf.CompleteRequest("Canceled by request");
                }

                if (HasStopBeenRequested)
                {
                    foreach (var pendingCancelItem in pendingEventDeliveryItemList.SafeTakeAll())
                    {
                        pendingCancelItem.ipf.CompleteRequest("Canceled because part has been asked to stop");
                    }
                }
            }
        }

        private string QueueSyncEventAction(IProviderFacet ipf)
        {
            pendingEventDeliveryItemList.Add((seqNum: queuedEventItemListSeqNum, ipf: ipf));

            return null;
        }

        #endregion

        #region E116ModuleTracker

        private class E116ModuleTracker
        {
            public E116ModuleTracker(NameIIDSpecPair moduleIDSpecPair, (int id, string name) recordingKeyInfo, E116.E116ModuleScopedToken moduleToken, EnqueueEventItemDelegate enqueueEventItemDelegate, CombinedEventReportingPartConfig partConfig)
            {
                ModuleIDSpecPair = moduleIDSpecPair;
                RecordingKeyInfo = recordingKeyInfo;
                ModuleToken = moduleToken;
                ModuleConfig = moduleToken.ModuleConfig.MakeCopyOfThis();

                EnqueueEventItemDelegate = enqueueEventItemDelegate;

                enableAutomaticBlockedTransitions = (ModuleConfig.PartBaseStateUsageBehavior & E116.AutomaticTransitionBehavior.AutomaticBlockedTransitions) != 0;
                enableAutomaticBusyTransitions = (ModuleConfig.PartBaseStateUsageBehavior & E116.AutomaticTransitionBehavior.AutomaticBusyTransitions) != 0;
                enableAutomaticWaitingTransitions = (ModuleConfig.PartBaseStateUsageBehavior & E116.AutomaticTransitionBehavior.AutomaticWaitingTransitions) != 0;

                if (ModuleConfig.SourcePart != null && (enableAutomaticBlockedTransitions || enableAutomaticBusyTransitions))
                {
                    partBaseStateObserver = new SequencedRefObjectSourceObserver<IBaseState, int>(ModuleConfig.SourcePart.BaseStateNotifier)
                    {
                        IsUpdateNeeded = true,
                    };
                }

                substLocObsSet = ModuleConfig.ModuleSubstLocSet.Select(substLocID => new MosaicLib.Semi.E090.E090SubstLocObserver(substLocID)).ToArray();

                if (moduleToken.IVAName.IsNeitherNullNorEmpty() && partConfig.IVI != null)
                    IVA = partConfig.IVI.GetValueAccessor(moduleToken.IVAName);
            }

            public NameIIDSpecPair ModuleIDSpecPair { get; private set; }

            public (int id, string name) RecordingKeyInfo { get; private set; }

            public E116.E116ModuleScopedToken ModuleToken { get; private set; }

            public E116.E116ModuleConfig ModuleConfig { get; private set; }

            public EnqueueEventItemDelegate EnqueueEventItemDelegate { get; private set; }

            public IValueAccessor IVA { get; private set; }

            private readonly bool enableAutomaticBlockedTransitions, enableAutomaticBusyTransitions, enableAutomaticWaitingTransitions;

            public readonly ISequencedObjectSourceObserver<IBaseState> partBaseStateObserver;

            public readonly MosaicLib.Semi.E090.E090SubstLocObserver[] substLocObsSet;

            /// <summary>
            /// This gives the accumulated count increment that will applied at the end of this busy scoped token whenever it will 
            /// produce a Busy to Busy or a Busy to Idle transition.
            /// <para/>As such this count increment represent a successful work increment.
            /// </summary>
            public ValueContainer AccumulatedWorkCountIncrementOnSuccess { get; set; }

            public void Add(IScopedToken scopedToken)
            {
                reevaluateState = true;
                switch (scopedToken)
                {
                    case E116.E116BusyScopedToken busyScopedToken:
                        busyScopedTokenList.Add(busyScopedToken);
                        break;
                    case E116.E116BlockedScopedToken blockedScopedToken:
                        blockedScopedTokenList.Add(blockedScopedToken);
                        break;
                    default:
                        break;
                }
            }

            public bool Remove(IScopedToken scopedToken)
            {
                var removed = false;
                switch (scopedToken)
                {
                    case E116.E116BusyScopedToken busyScopedToken:
                        removed = busyScopedTokenList.Remove(busyScopedToken);
                        break;
                    case E116.E116BlockedScopedToken blockedScopedToken:
                        removed = blockedScopedTokenList.Remove(blockedScopedToken);
                        break;
                    default:
                        return false;
                }

                reevaluateState |= removed;
                return removed;
            }

            private IListWithCachedArray<E116.E116BusyScopedToken> busyScopedTokenList = new IListWithCachedArray<E116.E116BusyScopedToken>();
            private IListWithCachedArray<E116.E116BlockedScopedToken> blockedScopedTokenList = new IListWithCachedArray<E116.E116BlockedScopedToken>();

            private bool reevaluateState;

            private E116.E116BusyScopedToken autoBusyScopedToken;
            private E116.E116BlockedScopedToken autoBlockedScopedToken;
            private E116.E116BusyScopedToken autoWaitingScopedToken;

            private IScopedToken activeScopedToken;

            public E116ModuleTracker Setup(QpcTimeStamp qpcTimeStamp)
            {
                IBaseState initialBaseState = ModuleConfig.SourcePart?.BaseState;

                if (ModuleConfig.InitialBlockedInfo != null)
                {
                    ModuleToken.InitialStateScopedToken = new E116.E116BlockedScopedToken(ModuleToken)
                    {
                        Priority = ModuleConfig.AutomaticBlockedTransitionPriorty,
                        BlockedReason = ModuleConfig.InitialBlockedInfo.Value.blockedReason,
                        BlockedReasonText = ModuleConfig.InitialBlockedInfo.Value.blockedReasonText,
                        State = ScopedTokenState.Started,
                    };
                }
                else if (ModuleConfig.InitialBusyInfo != null)
                {
                    ModuleToken.InitialStateScopedToken = new E116.E116BusyScopedToken(ModuleToken)
                    {
                        Priority = ModuleConfig.AutomaticBusyTransitionPriority,
                        TaskType = ModuleConfig.InitialBusyInfo.Value.taskType,
                        TaskName = ModuleConfig.InitialBusyInfo.Value.taskName,
                        State = ScopedTokenState.Started,
                    };
                }
                else if (initialBaseState != null && (ModuleConfig.PartBaseStateUsageBehavior & E116.AutomaticTransitionBehavior.GenerateInitialScopedTokenIfNeeded) != 0)
                {
                    var initialBlockedInfo = ModuleConfig.AutomaticBlockedTransitionDelegate?.Invoke(ModuleToken, initialBaseState) ?? default;
                    var initialBusyInfo = ModuleConfig.AutomaticBusyTransitionDelegate?.Invoke(ModuleToken, initialBaseState) ?? default;

                    if (initialBlockedInfo.blockedReason != MosaicLib.Semi.E116.BlockedReasonEx.NotBlocked && !enableAutomaticBlockedTransitions)
                    {
                        ModuleToken.InitialStateScopedToken = new E116.E116BlockedScopedToken(ModuleToken)
                        {
                            Priority = ModuleConfig.AutomaticBlockedTransitionPriorty,
                            BlockedReason = initialBlockedInfo.blockedReason,
                            BlockedReasonText = initialBlockedInfo.blockedReasonText ?? $"Part's initial state is blocked [{initialBaseState}]",
                            State = ScopedTokenState.Started,
                        };
                    }
                    else if (initialBusyInfo.taskType != MosaicLib.Semi.E116.TaskType.NoTask && !enableAutomaticBusyTransitions)
                    {
                        ModuleToken.InitialStateScopedToken = new E116.E116BusyScopedToken(ModuleToken)
                        {
                            Priority = ModuleConfig.AutomaticBusyTransitionPriority,
                            TaskType = initialBusyInfo.taskType,
                            TaskName = initialBusyInfo.taskName ?? $"Part's initial state is busy [{initialBaseState}]",
                            State = ScopedTokenState.Started,
                        };
                    }
                }

                if (ModuleToken.InitialStateScopedToken != null)
                    Add(ModuleToken.InitialStateScopedToken);

                reevaluateState = true;

                Service(qpcTimeStamp);

                return this;
            }

            public void Service(QpcTimeStamp qpcTimeStamp)
            {
                if (partBaseStateObserver != null && partBaseStateObserver.IsUpdateNeeded)
                {
                    var baseState = partBaseStateObserver.UpdateInline()?.Object;

                    if (enableAutomaticBlockedTransitions)
                    {
                        var blockedInfo = ModuleConfig.AutomaticBlockedTransitionDelegate?.Invoke(ModuleToken, baseState) ?? default;
                        var isBlocked = blockedInfo.blockedReason != MosaicLib.Semi.E116.BlockedReasonEx.NotBlocked;

                        if (isBlocked)
                        {
                            if (autoBlockedScopedToken != null && (autoBlockedScopedToken.BlockedReason != blockedInfo.blockedReason || autoBlockedScopedToken.BlockedReasonText != blockedInfo.blockedReasonText))
                            {
                                Remove(autoBlockedScopedToken);
                                autoBlockedScopedToken = null;
                            }

                            if (autoBlockedScopedToken == null)
                            {
                                Add(autoBlockedScopedToken = new E116.E116BlockedScopedToken(ModuleToken)
                                {
                                    Priority = ModuleConfig.AutomaticBlockedTransitionPriorty,
                                    BlockedReason = blockedInfo.blockedReason,
                                    BlockedReasonText = blockedInfo.blockedReasonText ?? $"Part is faulted [{baseState}]",
                                    State = ScopedTokenState.Started,
                                });
                            }
                        }
                        else if (autoBlockedScopedToken != null)
                        {
                            Remove(autoBlockedScopedToken);
                            autoBlockedScopedToken = null;
                        }
                    }

                    if (enableAutomaticBusyTransitions)
                    {
                        var busyInfo = ModuleConfig.AutomaticBusyTransitionDelegate?.Invoke(ModuleToken, baseState) ?? default;
                        var isBusy = busyInfo.taskType != MosaicLib.Semi.E116.TaskType.NoTask;

                        if (isBusy)
                        {
                            if (autoBusyScopedToken != null && (autoBusyScopedToken.TaskType != busyInfo.taskType || autoBusyScopedToken.TaskName != busyInfo.taskName))
                            {
                                Remove(autoBusyScopedToken);
                                autoBusyScopedToken = null;
                            }

                            if (autoBusyScopedToken == null)
                            {
                                Add(autoBusyScopedToken = new E116.E116BusyScopedToken(ModuleToken)
                                {
                                    Priority = ModuleConfig.AutomaticBusyTransitionPriority,
                                    TaskType = busyInfo.taskType,
                                    TaskName = busyInfo.taskName ?? $"Part is busy [{baseState}]",
                                    State = ScopedTokenState.Started,
                                });
                            }
                        }
                        else if (autoBusyScopedToken != null)
                        {
                            Remove(autoBusyScopedToken);
                            autoBusyScopedToken = null;
                        }
                    }
                }

                if (enableAutomaticWaitingTransitions)
                {
                    bool locUpdated = false;
                    foreach (var slo in substLocObsSet)
                        locUpdated |= slo.IsUpdateNeeded;

                    if (autoWaitingScopedToken == null
                        && (locUpdated || reevaluateState)
                        && blockedScopedTokenList.Count == 0
                        && busyScopedTokenList.Count == 0)
                    {
                        int anyWaitingToStartProcess = 0, anyWaitingToRemove = 0;

                        foreach (var slo in substLocObsSet)
                        {
                            if (slo.UpdateInline().IsOccupied)
                            {
                                var substInfo = slo.ContainsSubstInfo;
                                var lastPseudoSPS = substInfo.SPSList.LastOrDefault();

                                switch (lastPseudoSPS)
                                {
                                    case MosaicLib.Semi.E090.SubstProcState.InProcess:
                                    case MosaicLib.Semi.E090.SubstProcState.Processed:
                                    case MosaicLib.Semi.E090.SubstProcState.Rejected:
                                    case MosaicLib.Semi.E090.SubstProcState.Stopped:
                                    case MosaicLib.Semi.E090.SubstProcState.Aborted:
                                    case MosaicLib.Semi.E090.SubstProcState.ProcessStepCompleted:
                                    default:
                                        anyWaitingToRemove++;
                                        break;
                                    case MosaicLib.Semi.E090.SubstProcState.NeedsProcessing:
                                    case MosaicLib.Semi.E090.SubstProcState.Moved:
                                    case MosaicLib.Semi.E090.SubstProcState.Relocated:
                                    case MosaicLib.Semi.E090.SubstProcState.Created:
                                        anyWaitingToStartProcess++;
                                        break;
                                }
                            }
                        }

                        if (anyWaitingToRemove > 0)
                        {
                            Add(autoWaitingScopedToken = new E116.E116BusyScopedToken(ModuleToken)
                            {
                                Priority = ModuleConfig.AutomaticBusyTransitionPriority,
                                TaskType = MosaicLib.Semi.E116.TaskType.Waiting,
                                TaskName = $"Substrate is waiting to be removed",
                                State = ScopedTokenState.Started,
                            });
                        }
                        else if (anyWaitingToStartProcess > 0)
                        {
                            Add(autoWaitingScopedToken = new E116.E116BusyScopedToken(ModuleToken)
                            {
                                Priority = ModuleConfig.AutomaticBusyTransitionPriority,
                                TaskType = MosaicLib.Semi.E116.TaskType.Waiting,
                                TaskName = $"Substrate is waiting to start process",
                                State = ScopedTokenState.Started,
                            });
                        }
                    }
                    else if (autoWaitingScopedToken != null && (locUpdated || reevaluateState))
                    {
                        var anyOccupied = false;
                        foreach (var slo in substLocObsSet)
                        {
                            anyOccupied |= slo.UpdateInline().IsOccupied;
                        }

                        if (!anyOccupied || busyScopedTokenList.Count > 0)
                        {
                            Remove(autoWaitingScopedToken);
                            autoWaitingScopedToken = null;
                        }
                    }
                }

                if (reevaluateState)
                {
                    reevaluateState = false;

                    IScopedToken nextActiveScopedToken = null;

                    if (nextActiveScopedToken == null && blockedScopedTokenList.Count > 0)
                    {
                        foreach (var scanScopedToken in blockedScopedTokenList.Array)
                        {
                            if (nextActiveScopedToken == null || (nextActiveScopedToken.Priority < scanScopedToken.Priority))
                                nextActiveScopedToken = scanScopedToken;
                        }
                    }

                    if (nextActiveScopedToken == null && busyScopedTokenList.Count > 0)
                    {
                        foreach (var scanScopedToken in busyScopedTokenList.Array)
                        {
                            if (nextActiveScopedToken == null || (nextActiveScopedToken.Priority < scanScopedToken.Priority))
                                nextActiveScopedToken = scanScopedToken;
                        }
                    }

                    var lastActiveScopedToken = activeScopedToken;
                    if (!Object.ReferenceEquals(activeScopedToken, nextActiveScopedToken) || (eptState == MosaicLib.Semi.E116.EPTState.NoState && prevEPTState == MosaicLib.Semi.E116.EPTState.NoState))
                        IssueTransitionEvent(activeScopedToken = nextActiveScopedToken, lastActiveScopedToken, qpcTimeStamp);
                }
            }

            public MosaicLib.Semi.E116.EPTState eptState = MosaicLib.Semi.E116.EPTState.NoState;

            QpcTimeStamp prevQpcTimeStamp;
            MosaicLib.Semi.E116.EPTState prevEPTState = MosaicLib.Semi.E116.EPTState.NoState;
            MosaicLib.Semi.E116.TaskType prevTaskType = MosaicLib.Semi.E116.TaskType.NoTask;
            string prevTaskName;

            public void IssueTransitionEvent(IScopedToken scopedToken, IScopedToken lastScopedToken, QpcTimeStamp qpcTimeStamp)
            {
                MosaicLib.Semi.E116.EPTTransition transition;
                MosaicLib.Semi.E116.TaskType taskType = default;
                string taskName = null;
                MosaicLib.Semi.E116.BlockedReasonEx blockedReason = default;
                string blockedReasonStr = null;

                switch (scopedToken)
                {
                    default: // idle
                        {
                            eptState = MosaicLib.Semi.E116.EPTState.Idle;

                            switch (prevEPTState)
                            {
                                case MosaicLib.Semi.E116.EPTState.NoState: transition = MosaicLib.Semi.E116.EPTTransition.Transition1; break;
                                case MosaicLib.Semi.E116.EPTState.Idle: transition = default; break;
                                case MosaicLib.Semi.E116.EPTState.Busy: transition = MosaicLib.Semi.E116.EPTTransition.Transition3; break;
                                case MosaicLib.Semi.E116.EPTState.Blocked: transition = MosaicLib.Semi.E116.EPTTransition.Transition7; break;
                                default: transition = 0; break;
                            }

                            taskName = "No Task";
                        }
                        break;
                    case E116.E116BusyScopedToken busyScopedToken:
                        {
                            eptState = MosaicLib.Semi.E116.EPTState.Busy;

                            switch (prevEPTState)
                            {
                                case MosaicLib.Semi.E116.EPTState.NoState: transition = MosaicLib.Semi.E116.EPTTransition.Transition1; break;
                                case MosaicLib.Semi.E116.EPTState.Idle: transition = MosaicLib.Semi.E116.EPTTransition.Transition2; break;
                                case MosaicLib.Semi.E116.EPTState.Busy: transition = MosaicLib.Semi.E116.EPTTransition.Transition4; break;
                                case MosaicLib.Semi.E116.EPTState.Blocked: transition = MosaicLib.Semi.E116.EPTTransition.Transition6; break;
                                default: transition = 0; break;
                            }

                            taskType = busyScopedToken.TaskType;
                            taskName = busyScopedToken.TaskName;
                        }
                        break;
                    case E116.E116BlockedScopedToken blockedScopedToken:
                        {
                            eptState = MosaicLib.Semi.E116.EPTState.Blocked;

                            switch (prevEPTState)
                            {
                                case MosaicLib.Semi.E116.EPTState.NoState: transition = MosaicLib.Semi.E116.EPTTransition.Transition1; break;
                                case MosaicLib.Semi.E116.EPTState.Idle: transition = MosaicLib.Semi.E116.EPTTransition.Transition8; break;
                                case MosaicLib.Semi.E116.EPTState.Busy: transition = MosaicLib.Semi.E116.EPTTransition.Transition5; break;
                                case MosaicLib.Semi.E116.EPTState.Blocked: transition = MosaicLib.Semi.E116.EPTTransition.Transition9; break;
                                default: transition = 0; break;
                            }

                            blockedReason = blockedScopedToken.BlockedReason;
                            blockedReasonStr = blockedScopedToken.BlockedReasonText;
                        }
                        break;
                }

                if (transition == default)
                    return;

                var eventRecord = E116.E116EventRecord.Pool.GetFreeObjectFromPool();

                eventRecord.Module = ModuleIDSpecPair;
                eventRecord.RecordingKeyInfo = RecordingKeyInfo;
                eventRecord.Transition = transition;
                eventRecord.State = eptState;
                eventRecord.PrevState = prevEPTState;

                var getKVCSetFromToken = (scopedToken ?? lastScopedToken);
                if (getKVCSetFromToken is ScopedTokenBase scb && scb.CapturedKVCSet != null)
                    eventRecord.KVCSet.AddRange(scb.CapturedKVCSet);

                var isInitialTransition = (transition == MosaicLib.Semi.E116.EPTTransition.Transition1);

                eventRecord.StateTime = (!isInitialTransition) ? qpcTimeStamp - prevQpcTimeStamp : TimeSpan.Zero;

                if (isInitialTransition)
                {
                    taskName = taskName ?? "No Task";
                    prevTaskName = prevTaskName ?? "No Task";
                }

                if (eptState == MosaicLib.Semi.E116.EPTState.Idle)
                {
                    if (prevEPTState == MosaicLib.Semi.E116.EPTState.Busy)
                    {
                        taskName = "No Task";
                    }
                    else if (prevEPTState == MosaicLib.Semi.E116.EPTState.Blocked)
                    {
                        blockedReasonStr = "Not Blocked";
                        taskName = "No Task";
                    }
                }

                if (taskName != null)
                {
                    eventRecord.TaskType = taskType;
                    eventRecord.TaskName = taskName;
                }

                if (blockedReason != default || blockedReasonStr != null)
                {
                    eventRecord.BlockedReason = blockedReason;
                    eventRecord.BlockedReasonText = blockedReasonStr;
                }
                else if (prevEPTState == MosaicLib.Semi.E116.EPTState.Blocked && eptState != MosaicLib.Semi.E116.EPTState.Blocked)
                {
                    eventRecord.BlockedReason = MosaicLib.Semi.E116.BlockedReasonEx.NotBlocked;
                    eventRecord.BlockedReasonText = "Not Blocked";
                }

                if (isInitialTransition || prevEPTState == MosaicLib.Semi.E116.EPTState.Busy)
                {
                    if (prevTaskType != default || prevTaskName.IsNeitherNullNorEmpty())
                    {
                        eventRecord.PrevTaskType = prevTaskType;
                        eventRecord.PrevTaskName = prevTaskName ?? "No Task";

                        prevTaskType = default;
                        prevTaskName = null;
                    }
                }
                else
                {
                    prevTaskType = default;
                    prevTaskName = null;
                }

                if (eptState == MosaicLib.Semi.E116.EPTState.Busy)
                {
                    prevTaskType = taskType;
                    prevTaskName = taskName;
                }

                if (!AccumulatedWorkCountIncrementOnSuccess.IsEmpty)
                {
                    if (eptState != MosaicLib.Semi.E116.EPTState.Blocked)
                        eventRecord.WorkCountIncrement = AccumulatedWorkCountIncrementOnSuccess;
                    else
                        ModuleToken.LocalEmitters.IssueEmitter.Emit($"Non-empty accumulated EndWorkCountIncrementOnSuccess '{AccumulatedWorkCountIncrementOnSuccess}' has been discarded due to {eptState} transition.");

                    AccumulatedWorkCountIncrementOnSuccess = default;
                }

                prevQpcTimeStamp = qpcTimeStamp;
                prevEPTState = eptState;

                if (IVA != null)
                    IVA.Set(eventRecord?.MakeCopyOfThis());

                EnqueueEventItemDelegate(eventRecord);
            }
        }

        #endregion
    }
}