//-------------------------------------------------------------------
/*! @file E090SubstrateTestingTools.cs
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
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Part;
using MosaicLib.Semi.E039;
using MosaicLib.Semi.E090;
using MosaicLib.Semi.E090.SubstrateRouting;
using MosaicLib.Semi.E090.SubstrateScheduling;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Semi.E090.SubstrateTestingTools
{
    #region TestECSParts

    /// <summary>
    /// This is a test version of a container/pointer/construction/destruction helper class that is used to construct and setup a set of representative parts that can be used for
    /// testing substrate routing and processing related code.  This includes 4 PMs: 2 single wafer PMs (PM1, PM2), a linear belt 4 station PM (PM3) and a circular belt 4 station PM (PM4).
    /// It also includes a corresponding ISubstrateRoutingManager (SRM) that is used to construct a set of substrate locations, a set of test substrates, and which can be used to move
    /// these substrates through the tool for processing.
    /// </summary>
    public class TestECSParts : DisposableBase
    {
        public TestECSParts(string baseName, ReferenceSet<E039Object> e090HistorySet = null, int numLPSlotsAndWafers = 5)
            : this(baseName, new E039BasicTablePartConfig(baseName + ".E039Table") { DefaultFallbackReferenceHistorySet = e090HistorySet }, numLPSlotsAndWafers: numLPSlotsAndWafers)
        { }

        public TestECSParts(string baseName, E039BasicTablePartConfig e039BasicTablePartConfig, bool e039AutoGoOnline = true, bool otherPartsAutoGoOnline = true, int numLPSlotsAndWafers = 5)
        {
            E039TableUpdater = new E039BasicTablePart(e039BasicTablePartConfig).RunGoOnlineActionInline();
            if (e039AutoGoOnline)
                E039TableUpdater.RunGoOnlineAction();

            PM1 = new SimpleExampleProcessModuleEngine(baseName + ".PM1", E039TableUpdater, pmLocName: "PM1");
            PM2 = new SimpleExampleProcessModuleEngine(baseName + ".PM2", E039TableUpdater, pmLocName: "PM2");
            PM3 = new BeltExampleProcessModuleEngine(baseName + ".PM3", E039TableUpdater, locBaseName: "PM3", engineType: BeltExampleProcessModuleEngineType.Linear);
            PM4 = new BeltExampleProcessModuleEngine(baseName + ".PM4", E039TableUpdater, locBaseName: "PM4", engineType: BeltExampleProcessModuleEngineType.Circular);

            var srmConfig = new TestSRMConfig(baseName + ".SRM")
            {
                ECSParts = this,
                NumLPSlots = numLPSlotsAndWafers,
                NumLPWafers = numLPSlotsAndWafers,
                ManualLocNameToITPRDictionary = new ReadOnlyIDictionary<string, ITransferPermissionRequest>(
                    new KeyValuePair<string, ITransferPermissionRequest> []
                    {
                        KVP.Create(PM1.LocID.Name, (ITransferPermissionRequest) PM1),
                        KVP.Create(PM2.LocID.Name, (ITransferPermissionRequest) PM2),
                        KVP.Create(PM3.InputLocID.Name, (ITransferPermissionRequest) PM3),
                        KVP.Create(PM3.OutputLocID.Name, (ITransferPermissionRequest) PM3),
                        KVP.Create(PM4.InputLocID.Name, (ITransferPermissionRequest) PM4),
                    }),
            };
            SRM = new TestSRM(srmConfig).RunGoOnlineActionInline();

            if (otherPartsAutoGoOnline)
                new IActivePartBase[] { PM1, PM2, PM3, PM4, SRM }.DoForEach(part => part.RunGoOnlineAction());

            AddExplicitDisposeAction(() => Fcns.DisposeOfGivenObject(SRM));
            AddExplicitDisposeAction(() => Fcns.DisposeOfGivenObject(PM4));
            AddExplicitDisposeAction(() => Fcns.DisposeOfGivenObject(PM3));
            AddExplicitDisposeAction(() => Fcns.DisposeOfGivenObject(PM2));
            AddExplicitDisposeAction(() => Fcns.DisposeOfGivenObject(PM1));
            AddExplicitDisposeAction(() => Fcns.DisposeOfGivenObject(E039TableUpdater));

            stationNameToEnumDictionary = new ReadOnlyIDictionary<string, TestStationEnum>(
                KVP.Create(SRM.AL1LocID.Name, TestStationEnum.AL1),
                KVP.Create(PM1.LocID.Name, TestStationEnum.PM1),
                KVP.Create(PM2.LocID.Name, TestStationEnum.PM2),
                KVP.Create(PM3.InputLocID.Name, TestStationEnum.PM3Input),
                KVP.Create(PM3.OutputLocID.Name, TestStationEnum.PM3Output),
                KVP.Create(PM4.InputLocID.Name, TestStationEnum.PM4)
            );
        }

        public IE039TableUpdater E039TableUpdater { get; private set; }
        public IE039TableObserver E039TableObserver { get { return E039TableUpdater; } }

        public ISimpleExampleProcessModuleEngine PM1 { get; private set; }
        public ISimpleExampleProcessModuleEngine PM2 { get; private set; }
        public IBeltExampleProcessModuleEngine PM3 { get; private set; }
        public IBeltExampleProcessModuleEngine PM4 { get; private set; }
        public TestSRM SRM { get; private set; }

        public TestStationEnum GetStationEnum(string forLocName)
        {
            return stationNameToEnumDictionary.SafeTryGetValue(forLocName);
        }

        public IActivePartBase GetPart(TestStationEnum station)
        {
            switch (station)
            {
                case TestStationEnum.PM1: return PM1;
                case TestStationEnum.PM2: return PM2;
                case TestStationEnum.PM3Input: return PM3;
                case TestStationEnum.PM3Output: return PM3;
                case TestStationEnum.PM4: return PM4;
                default: return null;
            }
        }

        ReadOnlyIDictionary<string, TestStationEnum> stationNameToEnumDictionary = ReadOnlyIDictionary<string, TestStationEnum>.Empty;
    }

    public enum TestStationEnum
    {
        None = 0,
        AL1,
        PM1,
        PM2,
        PM3Input,
        PM3Output,
        PM4,
    }

    #endregion

    #region TestSchedulerEngine (ITestSchedulerEngine, TestSubstrateAndProcessTracker, TestSubstrateSchedulerTool)

    public interface ITestSchedulerEngine : IActivePartBase
    {
        IClientFacet SetSubstrateProcessSpecs(ProcessSpecBase<ProcessStepSpecBase> processSpec, SubstrateJobRequestState initialSJRM, params E039ObjectID[] substIDsArray);
        IClientFacet SetSJRM(SubstrateJobRequestState sjrm, params E039ObjectID[] substIDsArray);
    }

    public class TestSchedulerEngine : SimpleActivePartBase, ITestSchedulerEngine
    {
        public TestSchedulerEngine(string partID = "Sched", TestECSParts ecsParts = null, ISubstrateSchedulerTool<TestSubstrateAndProcessTracker, ProcessSpecBase<ProcessStepSpecBase>, ProcessStepSpecBase> substrateSchedulerTool = null)
            : base (partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion1)
        {
            EcsParts = ecsParts;
            SubstrateSchedulerTool = substrateSchedulerTool;
            SubstrateSchedulerTool.HostingPartNotifier = this;
        }

        public TestECSParts EcsParts { get; private set;}
        ISubstrateSchedulerTool<TestSubstrateAndProcessTracker, ProcessSpecBase<ProcessStepSpecBase>, ProcessStepSpecBase> SubstrateSchedulerTool { get; set; }

        public IClientFacet SetSubstrateProcessSpecs(ProcessSpecBase<ProcessStepSpecBase> processSpec, SubstrateJobRequestState initialSJRM, params E039ObjectID[] substIdArray)
        {
            return new BasicActionImpl(actionQ, ipf => PerformSetSubstrateProcessSpecs(ipf, processSpec, initialSJRM, substIdArray), CurrentMethodName, ActionLoggingReference);
        }

        public IClientFacet SetSJRM(SubstrateJobRequestState sjrm, params E039ObjectID[] substIdArray)
        {
            return new BasicActionImpl(actionQ, ipf => PerformSetSJRM(ipf, sjrm, substIdArray), CurrentMethodName, ActionLoggingReference);
        }

        protected override string PerformGoOnlineAction(IProviderActionBase<bool, NullObj> ipf)
        {
            bool andInitialize = ipf.ParamValue;

            UseState requestUseState = andInitialize ? UseState.Online : UseState.OnlineUninitialized;
            var denyReasonList = SubstrateSchedulerTool.VerifyUseStateChange(BaseState, requestUseState, andInitialize: andInitialize);

            if (!denyReasonList.IsNullOrEmpty())
                return "Cannot {0}: {1}".CheckedFormat(ipf.ToString(ToStringSelect.MesgAndDetail), String.Join(", ", denyReasonList));

            return "";
        }

        protected override string PerformGoOfflineAction(IProviderActionBase ipf)
        {
            var denyReasonList = SubstrateSchedulerTool.VerifyUseStateChange(BaseState, UseState.Offline);

            if (!denyReasonList.IsNullOrEmpty())
                Log.Warning.Emit("Attempt to {0} gave warnings: {1}", ipf.ToString(ToStringSelect.MesgAndDetail), String.Join(", ", denyReasonList));

            return "";
        }

        private string PerformSetSubstrateProcessSpecs(IProviderFacet ipf, ProcessSpecBase<ProcessStepSpecBase> processSpec, SubstrateJobRequestState initialSJRM, E039ObjectID[] substIdArray)
        {
            foreach (var substIDiter in substIdArray)
            {
                var substID = substIDiter ?? E039ObjectID.Empty;

                TestSubstrateAndProcessTracker st = trackerDictionary.SafeTryGetValue(substID.FullName);

                if (st != null)
                    return "{0} has already been assigned a process spec".CheckedFormat(substID.FullName);

                st = new TestSubstrateAndProcessTracker();
                st.Setup(EcsParts.E039TableUpdater, substID, Log, processSpec);

                trackerDictionary[substID.FullName] = st;
                SubstrateSchedulerTool.Add(st);

                if (initialSJRM != SubstrateJobRequestState.None)
                {
                    st.SubstrateJobRequestState = initialSJRM;
                    Log.Debug.Emit("{0} Initial SJRM set to {1}", substID.FullName, initialSJRM);
                }
            }

            BusyReason = ipf.ToString(ToStringSelect.MesgAndDetail);

            return "";
        }

        private string PerformSetSJRM(IProviderFacet ipf, SubstrateJobRequestState sjrm, E039ObjectID[] substIdArray)
        {
            foreach (var substIDiter in substIdArray)
            {
                var substID = substIDiter ?? E039ObjectID.Empty;

                var st = trackerDictionary.SafeTryGetValue(substID.FullName);

                if (st == null)
                    return "{0} was not found".CheckedFormat(substID.FullName);

                if (st.SubstrateJobRequestState != sjrm)
                {
                    st.SubstrateJobRequestState = sjrm;
                    Log.Debug.Emit("{0} SJRM set to {1}", substID.FullName, sjrm);
                }
                else
                {
                    Log.Debug.Emit("{0} SJRM was already {1}", substID.FullName, sjrm);
                }
            }

            BusyReason = ipf.ToString(ToStringSelect.MesgAndDetail);

            return "";
        }

        IDictionaryWithCachedArrays<string, TestSubstrateAndProcessTracker> trackerDictionary = new IDictionaryWithCachedArrays<string, TestSubstrateAndProcessTracker>();

        protected override void PerformMainLoopService()
        {
            bool isUpdateNeeded = trackerDictionary.ValueArray.Any(tracker => tracker.IsUpdateNeeded);
            if (isUpdateNeeded)
                trackerDictionary.ValueArray.DoForEach(tracker => tracker.UpdateIfNeeded());

            SubstrateStateTally substrateStateTally = new SubstrateStateTally();
            trackerDictionary.ValueArray.DoForEach(tracker => substrateStateTally.Add(tracker));

            int serviceCount = 0;
            IBaseState baseState = BaseState;
            try
            {
                serviceCount += SubstrateSchedulerTool.Service(isUpdateNeeded || InFastSpinPeriod, ref substrateStateTally, baseState);
            }
            catch (System.Exception ex)
            {
                SetBaseState(UseState.OnlineFailure, "{0} caught unexpected exception: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)));
                return;
            }

            if (trackerDictionary.ValueArray.Any(tracker => !tracker.DropRequestReason.IsNullOrEmpty()))
            {
                var dropTrackerSet = trackerDictionary.ValueArray.Where(tracker => !tracker.DropRequestReason.IsNullOrEmpty()).ToArray();

                foreach (var dropTracker in dropTrackerSet)
                {
                    trackerDictionary.Remove(dropTracker.SubstID.FullName);
                    SubstrateSchedulerTool.Drop(dropTracker);
                }
            }

            if (serviceCount > 0)
                InFastSpinPeriod = true;

            string nextBusyReason = "";

            if (substrateStateTally.total > 0)
            {
                if (substrateStateTally.sjsRunning > 0)
                    nextBusyReason = "Substrates at in process";
                else if (substrateStateTally.stsAtWork > substrateStateTally.sjsPaused)
                    nextBusyReason = "Unpaused substrates are at work";
            }

            if (nextBusyReason.IsNullOrEmpty() && InFastSpinPeriod)
                nextBusyReason = BusyReason.MapNullOrEmptyTo("Internal work triggered being busy");

            BusyReason = nextBusyReason;

            if (backgroundSpinLogTimer.IsTriggered)
                Log.Debug.Emit("Background spin log: serviceCount:{0} {1} busyReason:'{2}' {3}", serviceCount, substrateStateTally, BusyReason, baseState);
        }

        QpcTimer backgroundSpinLogTimer = new QpcTimer() { TriggerIntervalInSec = 2.0, AutoReset = true }.Start();

        IDisposable busyToken;
        string BusyReason 
        {
            get { return _busyReason; }
            set 
            {
                if (_busyReason != value)
                {
                    Log.Debug.Emit("Busy Reason changed to '{0}' [from '{1}']", value, _busyReason);

                    _busyReason = value;

                    var entryBusyToken = busyToken;

                    if (!_busyReason.IsNullOrEmpty())
                        busyToken = CreateInternalBusyFlagHolderObject(flagName: value);

                    entryBusyToken.DisposeOfGivenObject();
                }
            }
        }
        string _busyReason = "";

        bool InFastSpinPeriod { get { return fastSpinCount > 0; } set { fastSpinCount = (value) ? 3 : 0; } }
        int fastSpinCount = 0;
        static readonly TimeSpan fastSpinWaitPeriod = (1.0).FromMilliseconds();

        protected override bool WaitForSomethingToDo(IWaitable waitable, TimeSpan useWaitTimeLimit)
        {
            if (InFastSpinPeriod)
            {
                fastSpinCount--;
                fastSpinWaitPeriod.Sleep();
                return true;
            }
            else
            {
                return base.WaitForSomethingToDo(waitable, useWaitTimeLimit);
            }
        }
    }

    public class TestSubstrateAndProcessTracker : SubstrateAndProcessTrackerBase<ProcessSpecBase<ProcessStepSpecBase>, ProcessStepSpecBase>
    {
        public override void Setup(IE039TableUpdater e039TableUpdater, E039ObjectID substID, Logging.IBasicLogger logger, ProcessSpecBase<ProcessStepSpecBase> processSpec, bool useExternalSync = false)
        {
            base.Setup(e039TableUpdater, substID, logger, processSpec, useExternalSync);

            srcLocNameList = new ReadOnlyIList<string>(Info.LinkToSrc.ToID.Name);
            destLocNameList = new ReadOnlyIList<string>(Info.LinkToDest.ToID.Name);
        }

        public IClientFacet CurrentStationProcessICF { get; set; }
        public bool FinalizeSPSAtEndOfLastStep { get; set; }

        private ReadOnlyIList<string> srcLocNameList, destLocNameList;

        public Func<IClientFacet> GeneratePendingRunProcessActionAndCorrespondingDelegate(string nextLocName, TestECSParts ecsParts, INotifyable hostingPartNotifier, string autoReleaseKey = null, string autoAcquireKey = null)
        {
            var station = ecsParts.GetStationEnum(nextLocName);
            var part = ecsParts.GetPart(station);

            if (part != null)
            {
                var partBaseState = part.BaseState;

                if (!partBaseState.UseState.IsOnline(acceptUninitialized: false, acceptOnlineFailure: false))
                {
                    SetSubstrateJobState(SubstrateJobState.Aborting, "Part {0} is not ready for use [{1}, {2}]".CheckedFormat(part.PartID, partBaseState, Fcns.CurrentMethodName));
                    return null;
                }
            }

            var stepSpec = NextStepSpec;

            if (stepSpec == null)
            {
                SetSubstrateJobState(SubstrateJobState.Aborting, "Pending process start requested at process location '{0}' after substrate processing has been completed".CheckedFormat(nextLocName));
                return null;
            }
            else if (!stepSpec.UsableLocNameList.MapNullToEmpty().Contains(nextLocName))
            {
                SetSubstrateJobState(SubstrateJobState.Aborting, "Pending process start requested at unexpected process location '{0}'.  Next process step: {1}, supported locations [{2}]".CheckedFormat(nextLocName, stepSpec.StepNum, String.Join(",", stepSpec.UsableLocNameList)));
                return null;
            }

            IClientFacet pendingICF = null;

            switch (station)
            {
                case TestStationEnum.PM3Input:
                    pendingICF = ecsParts.PM3.RunProcess(SubstID, stepSpec, autoReleaseTransferPermissionLocNameAtStart: autoReleaseKey, autoAcquireTransferPermissionLocNameAtEnd: autoAcquireKey);  
                    break;
                case TestStationEnum.PM4:
                    pendingICF = ecsParts.PM4.RunProcess(SubstID, stepSpec, autoReleaseTransferPermissionLocNameAtStart: autoReleaseKey, autoAcquireTransferPermissionLocNameAtEnd: autoAcquireKey);
                    break;
                default:
                    SetSubstrateJobState(SubstrateJobState.Aborting, "'{0}' is not a valid process location [{1}]".CheckedFormat(nextLocName, Fcns.CurrentMethodName));
                    return null;
            }

            if (hostingPartNotifier != null)
                pendingICF.NotifyOnComplete.AddItem(hostingPartNotifier);

            CurrentStationProcessICF = pendingICF;
            currentStationProcessICFIsPending = true;

            return () =>
            {
                currentStationProcessICFIsPending = false;
                return CurrentStationProcessICF;
            };
        }

        public volatile bool currentStationProcessICFIsPending = false;

        public void StartProcessAtCurrentLocation(TestECSParts ecsParts, INotifyable hostingPartNotifier)
        {
            currentStationProcessICFIsPending = false;

            string currentLoc = Info.LocID;

            var station = ecsParts.GetStationEnum(currentLoc);
            var part = ecsParts.GetPart(station);

            if (part != null)
            {
                var partBaseState = part.BaseState;

                if (!partBaseState.UseState.IsOnline(acceptUninitialized: false, acceptOnlineFailure: false))
                {
                    SetSubstrateJobState(SubstrateJobState.Aborting, "Part {0} is not ready for use [{1}, {2}]".CheckedFormat(part.PartID, partBaseState, Fcns.CurrentMethodName));
                    return;
                }
            }

            var stepSpec = NextStepSpec;

            if (stepSpec == null)
                SetSubstrateJobState(SubstrateJobState.Aborting, "Process start requested at process location '{0}' after substrate processing has been completed".CheckedFormat(currentLoc));
            else if (!stepSpec.UsableLocNameList.MapNullToEmpty().Contains(currentLoc))
                SetSubstrateJobState(SubstrateJobState.Aborting, "Process start requested at unexpected process location '{0}'.  Next process step: {1}, supported locations [{2}]".CheckedFormat(currentLoc, stepSpec.StepNum, String.Join(",", stepSpec.UsableLocNameList)));

            IClientFacet icf = null;

            switch (station)
            {
                case TestStationEnum.AL1:
                    Add(new ProcessStepTrackerResultItem<ProcessStepSpecBase>() { LocName = currentLoc, StepSpec = stepSpec, StepResult = new ProcessStepResultBase() });
                    break;
                case TestStationEnum.PM1:
                    icf = ecsParts.PM1.RunProcess(SubstID, stepSpec).StartInline();
                    break;
                case TestStationEnum.PM2:
                    icf = ecsParts.PM2.RunProcess(SubstID, stepSpec).StartInline();
                    break;
                case TestStationEnum.PM3Input:
                case TestStationEnum.PM4:
                    SetSubstrateJobState(SubstrateJobState.Aborting, "StartProcessAtCurrentLocation at process location '{0}' is no longer supported".CheckedFormat(currentLoc));
                    break;
                default:
                    SetSubstrateJobState(SubstrateJobState.Aborting, "Process start requested at unrecognized process location '{0}'".CheckedFormat(currentLoc));
                    break;
            }

            if (icf != null)
            {
                if (hostingPartNotifier != null)
                    icf.NotifyOnComplete.AddItem(hostingPartNotifier);

                icf.StartInline();
            }

            CurrentStationProcessICF = icf;
        }

        public int Service()
        {
            IClientFacet icf = CurrentStationProcessICF;
            IActionState actionState = icf.ActionState;

            if (icf != null && actionState.IsComplete)
            {
                IClientFacetWithResult<IProcessStepResult> icfWithResult = icf as IClientFacetWithResult<IProcessStepResult>;
                IProcessStepResult stepResult = (icfWithResult != null ? icfWithResult.ResultValue: null);

                if (stepResult == null)
                    stepResult = new ProcessStepResultBase(actionState.ResultCode);
                    
                if (stepResult != null && stepResult.SPS == SubstProcState.Processed && stepResult.ResultCode.IsNullOrEmpty() && actionState.Failed)
                    Logger.Debug.Emit("Note: {0} gave non-failure stepResult sps:{1}", icf.ToString(ToStringSelect.MesgDetailAndState), stepResult.SPS);

                Add(new ProcessStepTrackerResultItem<ProcessStepSpecBase>() { LocName = Info.LocID, StepSpec = NextStepSpec, StepResult = stepResult }, autoLatchFinalSPS: false);

                if (NextStepSpec == null && FinalizeSPSAtEndOfLastStep && (Info.SPS != Info.InferredSPS))
                    E039TableUpdater.SetSubstProcState(Info, Info.InferredSPS, addSyncExternalItem: UseExternalSync);

                CurrentStationProcessICF = null;

                return 1;
            }

            return 0;
        }

        public ReadOnlyIList<string> NextLocNameList
        {
            get
            {
                if (CurrentStationProcessICF != null || Info.STS.IsAtDestination())
                    return ReadOnlyIList<string>.Empty;

                IProcessStepSpec stepSpec = NextStepSpec;
                var inferredSPS = Info.InferredSPS;

                switch (Info.SJS)
                {
                    case SubstrateJobState.Initial:
                    case SubstrateJobState.Held:
                        return ReadOnlyIList<string>.Empty;

                    case SubstrateJobState.WaitingForStart:
                    case SubstrateJobState.Running:
                        if (stepSpec == null)
                        {
                            if (Info.STS.IsAtSource() && inferredSPS.IsNeedsProcessing())
                            {
                                E039TableUpdater.SetSubstProcState(SubstObserver.Info, SubstProcState.Processed, addSyncExternalItem: UseExternalSync);
                                return destLocNameList;
                            }
                            else if (Info.STS.IsAtDestination())
                            {
                                return ReadOnlyIList<string>.Empty;
                            }
                            else if (inferredSPS.IsProcessingComplete(includeLost: false, includeSkipped: false))
                            {
                                return destLocNameList;
                            }
                        }
                        
                        if (stepSpec != null)
                        {
                            return stepSpec.UsableLocNameList;
                        }

                        break;

                    default:
                        {
                            if (inferredSPS.IsNeedsProcessing())
                                return srcLocNameList;

                            if (inferredSPS.IsProcessingComplete(includeLost: false, includeSkipped: false))
                                return destLocNameList;
                        }
                        break;
                }

                return null;
            }
        }
    }

    public class TestSubstrateSchedulerTool: ISubstrateSchedulerTool<TestSubstrateAndProcessTracker, ProcessSpecBase<ProcessStepSpecBase>, ProcessStepSpecBase>
    {
        public TestSubstrateSchedulerTool(TestECSParts ecsParts)
        {
            ECSPart = ecsParts;
            Logger = new Logging.Logger(Fcns.CurrentClassLeafName);

            r1ArmALocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSPart.SRM.R1ArmALocID.GetPublisher(), substTrackerDictionary);
            r1ArmBLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSPart.SRM.R1ArmBLocID.GetPublisher(), substTrackerDictionary);

            al1LocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSPart.SRM.AL1LocID.GetPublisher(), substTrackerDictionary);
            pm1LocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSPart.PM1.LocID.GetPublisher(), substTrackerDictionary);
            pm2LocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSPart.PM2.LocID.GetPublisher(), substTrackerDictionary);
            pm3InputLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSPart.PM3.InputLocID.GetPublisher(), substTrackerDictionary);
            pm3OutputLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSPart.PM3.OutputLocID.GetPublisher(), substTrackerDictionary);
            pm4TransferLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSPart.PM4.InputLocID.GetPublisher(), substTrackerDictionary);

            foreach (var processLocObserver in al1LocObserver.ConcatItems(pm1LocObserver, pm2LocObserver, pm3InputLocObserver, pm3OutputLocObserver, pm4TransferLocObserver))
                processLocObserverDictionary[processLocObserver.ID.Name] = processLocObserver;

            foreach (var locObserver in r1ArmALocObserver.ConcatItems(r1ArmBLocObserver).Concat(processLocObserverDictionary.ValueArray))
                allLocObserverDictionary[locObserver.ID.Name] = locObserver;
        }

        TestECSParts ECSPart { get; set; }
        Logging.ILogger Logger { get; set; }

        /// <summary>Gives the INotifyable target that this tool is to attach to any Action it creates to notify the hosting part when the action completes</summary>
        public INotifyable HostingPartNotifier { get; set; }

        public void Add(TestSubstrateAndProcessTracker substTracker)
        {
            substTrackerDictionary.Add(substTracker.SubstID.FullName, substTracker);
        }

        public void Drop(TestSubstrateAndProcessTracker substTracker)
        {
            substTrackerDictionary.Remove(substTracker.SubstID.FullName);
        }

        public IList<string> VerifyUseStateChange(IBaseState partBaseState, UseState requestedUseState, bool andInitialize = false)
        {
            return null;
        }

        TestSubstrateAndProcessTracker lastSTAtSource = null;
        QpcTimer lastWaitForStartMessageTimer = new QpcTimer() { TriggerIntervalInSec = 1.0, AutoReset = true }.Start();

        public int Service(bool recentTrackerChangeMayHaveOccurred, ref SubstrateStateTally substrateStateTally, IBaseState partBaseState)
        {
            int deltaCount = UpdateObservers() * (!recentTrackerChangeMayHaveOccurred).MapToInt();

            bool isFullyOnline = partBaseState.UseState.IsOnline(acceptAttemptOnline: false, acceptUninitialized: false, acceptOnlineFailure: false);
            bool isPartiallyOnline = partBaseState.UseState.IsOnline(acceptAttemptOnline: true, acceptUninitialized: false, acceptOnlineFailure: false);

            if (!isFullyOnline)
            {
                foreach (var st in substTrackerDictionary.ValueArray)
                {
                    var sjs = st.SubstrateJobState;
                    if (sjs == SubstrateJobState.Running || sjs == SubstrateJobState.WaitingForStart)
                        st.SetSubstrateJobState(SubstrateJobState.Pausing, "Part is no longer online [{0}]".CheckedFormat(partBaseState));
                }
            }

            var enabledBasicSJSStateChangeTriggers = (ServiceBasicSJSStateChangeTriggerFlags.All & ~ServiceBasicSJSStateChangeTriggerFlags.EnableAutoStart);
            deltaCount += substTrackerDictionary.ValueArray.Sum(tracker => tracker.ServiceBasicSJSStateChangeTriggers(enabledBasicSJSStateChangeTriggers));
            deltaCount += substTrackerDictionary.ValueArray.Sum(tracker => tracker.ServiceDropReasonAssertion());

            // make autostart more smart - look at first process requested location and wait until it is empty
            bool enableAutoStart = isFullyOnline && (substrateStateTally.stsAtSource > 0 && substrateStateTally.sjsWaitingForStart > 0);

            if (enableAutoStart && deltaCount == 0 && substTrackerDictionary.ValueArray.Length > 0)
            {
                var nextSTAtSource = substTrackerDictionary.ValueArray.FirstOrDefault(st => st.SubstObserver.Info.STS.IsAtSource());

                if (nextSTAtSource != lastSTAtSource)
                    lastWaitForStartMessageTimer.Reset();

                if (nextSTAtSource != null)
                {
                    var st = nextSTAtSource;
                    string blockedReason = null;

                    if (st.SubstrateJobState != SubstrateJobState.WaitingForStart || st.SubstrateJobRequestState != SubstrateJobRequestState.Run)
                    {
                        // This is likely a substrate that we just started but which has not left the FOUP yet - stop looking until it leaves.
                        blockedReason = "Cannot issue AtSource {0} [{1} {2}]".CheckedFormat(st.SubstID.FullName, st.SubstrateJobState, st.SubstrateJobRequestState);
                    }

                    // next check if the next location for this wafer is empty
                    var nextLocList = st.NextLocNameList;

                    if (blockedReason.IsNullOrEmpty() && nextLocList.IsNullOrEmpty())
                    {
                        // if we encounter an AtSource, WaitingForStart, Run substrate that that does not have any locations in its NextLocNameList then we stop issuing
                        blockedReason = "Cannot issue AtSource {0} [NextLocNameList is empty]".CheckedFormat(st.SubstID.FullName);
                    }

                    var nextMoveToLocName = FindFirstUnoccupiedLoc(nextLocList);

                    if (blockedReason.IsNullOrEmpty())
                    {
                        if (!nextMoveToLocName.IsNullOrEmpty())
                        {
                            st.SetSubstrateJobState(SubstrateJobState.Running, "Starting substrate normally (next process location is available)");
                            deltaCount++;
                        }
                        else
                        {
                            blockedReason = "Cannot issue AtSource {0} [No empty PMs: {1}]".CheckedFormat(st.SubstID.FullName, string.Join(", ", nextLocList));
                        }
                    }

                    if (!blockedReason.IsNullOrEmpty() && lastWaitForStartMessageTimer.IsTriggered)
                    {
                        Logger.Debug.Emit(blockedReason);
                    }
                }
            }

            foreach (var st in trackersInProcessList.Array)
            {
                deltaCount += st.Service();

                if (st.CurrentStationProcessICF == null)
                {
                    trackersInProcessList.Remove(st);       // this is safe when iterating on the List.Array.
                    deltaCount++;

                    Logger.Debug.Emit("{0} NextLocList is now [{1}]", st.SubstID.FullName, string.Join(", ", st.NextLocNameList));
                }
            }

            if (srmAction != null)
            {
                var srmActionState = srmAction.ActionState;

                if (srmActionState.IsComplete)
                {
                    if (srmActionState.Failed)
                        throw new SubstrateSchedulingException("{0} failed: {1}".CheckedFormat(srmAction.ToString(ToStringSelect.MesgAndDetail), srmActionState));

                    srmAction = null;

                    deltaCount += UpdateObservers();
                }
            }

            if (srmAction == null && isFullyOnline)
            {
                foreach (var pt in processLocObserverDictionary.ValueArray)
                {
                    var st = pt.Tracker;
                    var substLocInfo = pt.Info;
                    var containsSubstInfo = pt.ContainsSubstInfo;

                    if (st != null)
                    {
                        if (st.CurrentStationProcessICF == null && st.NextLocNameList.Contains(pt.ID.Name))
                        {
                            st.StartProcessAtCurrentLocation(ECSPart, HostingPartNotifier);
                            deltaCount++;

                            if (st.CurrentStationProcessICF != null)
                                trackersInProcessList.Add(st);
                        }
                    }
                    else if (substLocInfo.IsOccupied)
                    {
                        if (containsSubstInfo.InferredSPS != SubstProcState.NeedsProcessing)
                            ECSPart.E039TableUpdater.SetSubstProcState(containsSubstInfo, SubstProcState.Aborted);

                        if (!containsSubstInfo.SJS.IsFinal())
                            ECSPart.E039TableUpdater.SetSubstrateJobStates(containsSubstInfo, sjs: SubstrateJobState.Aborting);
                    }
                }
            }

            if (srmAction == null && isPartiallyOnline)
            {
                UpdateObservers();

                int numEmptyArms = r1ArmALocObserver.IsUnoccupied.MapToInt() + r1ArmBLocObserver.IsUnoccupied.MapToInt();

                foreach (var pt in r1ArmALocObserver.ConcatItems(r1ArmBLocObserver).Concat(processLocObserverDictionary.ValueArray).Where(plot => plot.IsOccupied))
                {
                    var st = pt.Tracker;

                    var currentLocName = st.Info.LocID;
                    var stIsAlreadyOnArm = (currentLocName != ECSPart.SRM.R1ArmALocID.Name || currentLocName != ECSPart.SRM.R1ArmALocID.Name);

                    var nextLocList = (st != null) ? st.NextLocNameList : new ReadOnlyIList<string>(pt.ContainsSubstInfo.LinkToSrc.ToID.Name);

                    // skip over locations where there no next location or the next location is the location we are already in.  This also skips over locations where a process step has been requested for that location.
                    if (nextLocList.IsNullOrEmpty() || nextLocList.Contains(pt.ID.Name))
                        continue;

                    List<SubstrateRoutingItemBase> srmItemList = new List<SubstrateRoutingItemBase>();

                    var nextMoveToLocName = FindFirstUnoccupiedLoc(nextLocList);
                    var nextMoveToStation = ECSPart.GetStationEnum(nextMoveToLocName);

                    var nextStationArray = nextLocList.Select(locName => ECSPart.GetStationEnum(locName)).ToArray();

                    if ((nextMoveToStation == TestStationEnum.None) && nextStationArray.Contains(TestStationEnum.PM4))
                    {
                        nextMoveToLocName = ECSPart.PM4.InputLocID.Name;
                        nextMoveToStation = TestStationEnum.PM4;
                    }

                    switch (nextMoveToStation)
                    {
                        case TestStationEnum.AL1:
                            if (numEmptyArms > 0)
                                srmItemList.Add(new MoveSubstrateItem(st.SubstID, nextMoveToLocName));
                            break;

                        case TestStationEnum.PM1:
                        case TestStationEnum.PM2:
                            if (numEmptyArms > 0 && isFullyOnline)
                                srmItemList.Add(new MoveSubstrateItem(st.SubstID, nextMoveToLocName));
                            break;

                        case TestStationEnum.PM3Input:
                            if (numEmptyArms > 0 && isFullyOnline)
                            {
                                srmItemList.Add(new TransferPermissionRequestItem(TransferPermissionRequestItemSettings.Acquire, nextMoveToLocName));
                                srmItemList.Add(new MoveSubstrateItem(st.SubstID, nextMoveToLocName));
                                srmItemList.Add(new RunActionItem(icfFactoryDelegate: st.GeneratePendingRunProcessActionAndCorrespondingDelegate(nextMoveToLocName, ECSPart, HostingPartNotifier, autoReleaseKey: nextMoveToLocName), runActionBehavior: RunActionBehaviorFlags.OnlyStartAction));
                                if (st.CurrentStationProcessICF != null)
                                    trackersInProcessList.Add(st);
                            }
                            break;

                        case TestStationEnum.PM4:
                            if (numEmptyArms > 0 && isFullyOnline)
                            {
                                // block moving a substrate onto the arms or swapping it with the pm4 transfer location if that location already has a substrate and we have already asked that substrate to be processed there.
                                var pm4TransferLocST = pm4TransferLocObserver.Tracker;
                                bool pm4TransferLocIsInProcess = (pm4TransferLocST != null) && (pm4TransferLocST.CurrentStationProcessICF != null);

                                if (!stIsAlreadyOnArm && numEmptyArms >= 2 && !pm4TransferLocIsInProcess)
                                {
                                    // move the substrate to either arm ASAP
                                    srmItemList.Add(new MoveSubstrateItem(st.SubstID, ECSPart.SRM.R1EitherArmName));
                                }
                                else if (numEmptyArms >= 2 && !pm4TransferLocIsInProcess)
                                {
                                    srmItemList.Add(new TransferPermissionRequestItem(TransferPermissionRequestItemSettings.Acquire, nextMoveToLocName));
                                    srmItemList.Add(new MoveOrSwapSubstrateItem(st.SubstID, nextMoveToLocName));
                                    srmItemList.Add(new RunActionItem(icfFactoryDelegate: st.GeneratePendingRunProcessActionAndCorrespondingDelegate(nextMoveToLocName, ECSPart, HostingPartNotifier, autoReleaseKey: nextMoveToLocName), runActionBehavior: RunActionBehaviorFlags.OnlyStartAction));
                                    if (st.CurrentStationProcessICF != null)
                                        trackersInProcessList.Add(st);
                                }
                            }
                            break;

                        default:
                            if (numEmptyArms > 0 && !nextMoveToLocName.IsNullOrEmpty())
                                srmItemList.Add(new MoveSubstrateItem(st.SubstID, nextMoveToLocName));
                            break;
                    }

                    if (srmItemList.Count > 0)
                    {
                        srmAction = ECSPart.SRM.Sequence(srmItemList.ToArray()).StartInline();

                        deltaCount++;

                        break;
                    }
                }

                if (srmAction == null)
                {
                    var nextToLaunch = substTrackerDictionary.ValueArray.FirstOrDefault(st => st.Info.STS.IsAtSource() && st.Info.SJS == SubstrateJobState.Running);

                    if (nextToLaunch != null)
                    {
                        var firstUnoccupiedLocNameInNextLocList = FindFirstUnoccupiedLoc(nextToLaunch.NextLocNameList);

                        if (!firstUnoccupiedLocNameInNextLocList.IsNullOrEmpty())
                        {
                            srmAction = ECSPart.SRM.Sequence(new MoveSubstrateItem(nextToLaunch.SubstID, firstUnoccupiedLocNameInNextLocList)).StartInline();
                            deltaCount++;
                        }
                    }
                }
            }

            return deltaCount;
        }

        private int UpdateObservers()
        {
            int substDeltaCount = substTrackerDictionary.ValueArray.Sum(item => item.UpdateIfNeeded().MapToInt());
            int locDeltaCount = allLocObserverDictionary.ValueArray.Sum(item => item.Update().MapToInt());
            
            return locDeltaCount;
        }

        string FindFirstUnoccupiedLoc(ReadOnlyIList<string> locList)
        {
            locList = locList ?? ReadOnlyIList<string>.Empty;
            foreach (var locName in locList)
            {
                var lot = allLocObserverDictionary.SafeTryGetValue(locName);

                if (lot != null)
                {
                    if (lot.Info.IsUnoccupied)
                        return locName;
                } 
                else
                {
                    E090SubstLocInfo substLocInfo = new E090SubstLocInfo(ECSPart.E039TableObserver.GetObject(new E039ObjectID(locName, E090.Constants.SubstrateLocationObjectType)));
                    if (substLocInfo.IsUnoccupied)
                        return locName;
                }
            }

            return null;
        }

        IDictionaryWithCachedArrays<string, TestSubstrateAndProcessTracker> substTrackerDictionary = new IDictionaryWithCachedArrays<string, TestSubstrateAndProcessTracker>();

        SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker> r1ArmALocObserver, r1ArmBLocObserver;
        IClientFacet srmAction;

        SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker> al1LocObserver, pm1LocObserver, pm2LocObserver, pm3InputLocObserver, pm3OutputLocObserver, pm4TransferLocObserver;
        IDictionaryWithCachedArrays<string, SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>> processLocObserverDictionary = new IDictionaryWithCachedArrays<string, SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>>();
        IDictionaryWithCachedArrays<string, SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>> allLocObserverDictionary = new IDictionaryWithCachedArrays<string, SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>>();

        IListWithCachedArray<TestSubstrateAndProcessTracker> trackersInProcessList = new IListWithCachedArray<TestSubstrateAndProcessTracker>();
    }

    #endregion

    #region TestSRM (TestSRMConfig)

    public class TestSRMConfig
    {
        public TestSRMConfig(string partID = "SRM")
        {
            PartID = partID;
            NumLPSlots = 5;
            NumLPWafers = 5;
        }

        public TestSRMConfig(TestSRMConfig other)
        {
            PartID = other.PartID;
            ECSParts = other.ECSParts;
            NumLPSlots = other.NumLPSlots;
            NumLPWafers = other.NumLPWafers;
            AutoLocNameToITPRDictionary = other.AutoLocNameToITPRDictionary.MapNullToEmpty();
            ManualLocNameToITPRDictionary = other.ManualLocNameToITPRDictionary.MapNullToEmpty();
        }

        public string PartID { get; private set; }
        public TestECSParts ECSParts { get; set; }
        public int NumLPSlots { get; set; }
        public int NumLPWafers { get; set; }

        /// <summary>This dictionary gives the set of locations (and ITRP instances) for which material movement operations will automatically perform acquire and release TPR operations.  Items in the manual set but not in this set will not have automatic acquire performed on them during such movement.</summary>
        public ReadOnlyIDictionary<string, ITransferPermissionRequest> AutoLocNameToITPRDictionary { get; set; }

        /// <summary>This dictionary gives the set of locations (and ITRP instances) with which TransferPermissionRequestItem can be explicitly used in this TestSRM.  Internally the TestSRM will merge this set with the contents of the AutoLocNameToITPRDictionary so that all automatic locations can also be manually acquired and released.</summary>
        public ReadOnlyIDictionary<string, ITransferPermissionRequest> ManualLocNameToITPRDictionary { get; set; }
    }

    /// <summary>
    /// This is a test ISubstrateRoutingManager that is used for testing substrate routing and processing related code.
    /// It creates a single loadport with 5 slots (by default) (LP1), and up to 5 wafers in these slots.  It creates a single dual arm robot (R1), and an aligner station (AL1).
    /// It is aware of 4 PMs: PM1, PM2 (single location each), PM3 (seperate inbound and outbound location) and PM4 (exchange location).
    /// </summary>
    public class TestSRM : SimpleActivePartBase, ISubstrateRoutingManager
    {
        public TestSRM(TestSRMConfig config)
            : base(config.PartID)
        {
            Config = new TestSRMConfig(config);
            var ecsParts = Config.ECSParts;

            E039TableUpdater = ecsParts.E039TableUpdater;

            AutoLocNameToITPRDictionary = Config.AutoLocNameToITPRDictionary;
            ManualLocNameToITPRDictionary = Config.ManualLocNameToITPRDictionary;

            bool autoITPRDictionaryIsEmpty = Config.AutoLocNameToITPRDictionary.IsNullOrEmpty();
            bool manualITPRDictionaryIsEmpty = Config.ManualLocNameToITPRDictionary.IsNullOrEmpty();

            if (!autoITPRDictionaryIsEmpty && manualITPRDictionaryIsEmpty)
            {
                // auto is non-empty and manual is empty - replace manual with auto so that we can do manual requests for all of the auto items.
                ManualLocNameToITPRDictionary = AutoLocNameToITPRDictionary;
            }
            else if (!autoITPRDictionaryIsEmpty && !manualITPRDictionaryIsEmpty)
            {
                // both auto and manual are provided as non-empty - internally use the union of the auto with the manual set.  the manual set has priority (same name items replaces any one corresponding from the auto set)
                ManualLocNameToITPRDictionary = new ReadOnlyIDictionary<string, ITransferPermissionRequest>(AutoLocNameToITPRDictionary.ConvertToWritable().SafeAddRange(ManualLocNameToITPRDictionary));
            }

            var pm1 = ecsParts.PM1;
            var pm2 = ecsParts.PM2;
            var pm3 = ecsParts.PM3;
            var pm4 = ecsParts.PM4;

            LPSlotLocIDArray = Enumerable.Range(1, Config.NumLPSlots).Select(slotNum => { E039ObjectID id = null; E039TableUpdater.CreateE090SubstLoc("LP1.{0:d2}".CheckedFormat(slotNum), v => { id = v; }, instanceNum: slotNum); return id; }).ToArray();

            R1EitherArmName = "R1";
            E039TableUpdater.CreateE090SubstLoc(R1EitherArmName + ".A", v => R1ArmALocID = v, instanceNum: 1);
            E039TableUpdater.CreateE090SubstLoc(R1EitherArmName + ".B", v => R1ArmBLocID = v, instanceNum: 2);
            E039TableUpdater.CreateE090SubstLoc("AL1", v => AL1LocID = v);

            Enumerable.Range(0, Math.Min(Config.NumLPSlots, Config.NumLPWafers)).DoForEach(slotIndex => { var slotNum = slotIndex + 1; E039TableUpdater.CreateE090Subst("TestWafer_{0:d2}".CheckedFormat(slotNum), (E039ObjectID v) => { }, LPSlotLocIDArray[slotIndex]); });

            lpSlotLocObserverArray = LPSlotLocIDArray.Select(id => new E090SubstLocObserver(id.GetPublisher())).ToArray();

            var observerSet = new[] { R1ArmALocID, R1ArmBLocID, AL1LocID, pm1.LocID, pm2.LocID, pm3.InputLocID, pm3.OutputLocID, pm4.InputLocID }.Select(id => new E090SubstLocObserver(id.GetPublisher())).ToArray();
            robotArmALocObserver = observerSet[0];
            robotArmBLocObserver = observerSet[1];

            allSubstLocObserverArray = lpSlotLocObserverArray.Concat(observerSet).ToArray();

            allSubstLocObserverArray.DoForEach(obs => { allSubstLocObserverByLocNameDicationary[obs.ID.Name] = obs; });
        }

        TestSRMConfig Config { get; set; }

        public E039ObjectID[] LPSlotLocIDArray { get; private set; }
        public string R1EitherArmName { get; private set; }
        public E039ObjectID R1ArmALocID { get; private set; }
        public E039ObjectID R1ArmBLocID { get; private set; }
        public E039ObjectID AL1LocID { get; private set; }

        public IE039TableUpdater E039TableUpdater { get; private set; }
        public ReadOnlyIDictionary<string, ITransferPermissionRequest> AutoLocNameToITPRDictionary { get; private set; }
        public ReadOnlyIDictionary<string, ITransferPermissionRequest> ManualLocNameToITPRDictionary { get; private set; }

        public INotificationObject<INamedValueSet> DetailsNVSPublisher { get { return detailsNVSPublisher; } }
        private InterlockedNotificationRefObject<INamedValueSet> detailsNVSPublisher = new InterlockedNotificationRefObject<INamedValueSet>(NamedValueSet.Empty);

        public IClientFacet Sequence(params SubstrateRoutingItemBase[] itemParamsArray)
        {
            string desc = "{0}({1})".CheckedFormat(CurrentMethodName, string.Join(", ", itemParamsArray.Select(item => item.GetType().GetTypeDigestName())));
            return new BasicActionImpl(actionQ, ipf => PerformSequence(ipf, false, itemParamsArray), desc, ActionLoggingReference);
        }

        private string PerformSequence(IProviderFacet ipf, bool preposition, SubstrateRoutingItemBase[] itemArray)
        {
            if (preposition)
                return "";

            string ec = string.Empty;

            foreach (var item in itemArray)
            {
                if (item is MoveSubstrateItem)
                    ec = PerformItem(ipf, (MoveSubstrateItem)item);
                else if (item is SwapSubstratesItem)
                    ec = PerformItem(ipf, (SwapSubstratesItem)item);
                else if (item is MoveOrSwapSubstrateItem)
                    ec = PerformItem(ipf, (MoveOrSwapSubstrateItem)item);
                else if (item is ApproachLocationItem)
                    ec = PerformItem(ipf, (ApproachLocationItem)item);
                else if (item is RunActionItem)
                    ec = PerformItem(ipf, (RunActionItem)item);
                else if (item is TransferPermissionRequestItem)
                    ec = PerformItem(ipf, (TransferPermissionRequestItem)item);
                else
                    ec = "Item type '{0}' is not supported".CheckedFormat(item.GetType());

                if (!ec.IsNullOrEmpty())
                    break;
            }

            if (ec.IsNullOrEmpty())
                ec = ReleaseAcquiredEndOfSequenceTransferPermissionsIfNeeded(ipf);

            if (!ec.IsNullOrEmpty())
                NoteSequenceFailed(ipf);

            return ec;
        }

        private string PerformItem(IProviderFacet ipf, MoveSubstrateItem item)
        {
            UpdateObservers();

            var substID = item.SubstID.MapNullToEmpty();
            var toLocName = item.ToSubstLocName.MapNullToEmpty();

            var desc = "{0}[{1} -> {2}]".CheckedFormat(item.GetType().GetTypeLeafName(), substID.FullName, toLocName);

            return PerformMoveSubstrate(ipf, substID, toLocName, desc);
        }

        private string PerformMoveSubstrate(IProviderFacet ipf, E039ObjectID substID, string toLocName, string desc)
        {
            var substObserver = new E090SubstObserver(substID.GetPublisher());

            if (!substObserver.Info.IsValid)
                return "{0} failed: given substrate ID is not valid".CheckedFormat(desc);

            var substCurrentLocName = substObserver.Info.LocID;
            var fromLocObs = allSubstLocObserverByLocNameDicationary.SafeTryGetValue(substCurrentLocName);
            if (fromLocObs == null)
                return "{0} failed: given substrate's current location is not supported here [{1}]".CheckedFormat(desc, substCurrentLocName);

            if (toLocName == R1EitherArmName)
            {
                if (robotArmALocObserver.Info.IsUnoccupied)
                    toLocName = robotArmALocObserver.ID.Name;
                else if (robotArmBLocObserver.Info.IsUnoccupied)
                    toLocName = robotArmBLocObserver.ID.Name;
                else
                    return "{0} failed: neither robot arm is abavilable".CheckedFormat(desc);
            }

            var toLocObs = allSubstLocObserverByLocNameDicationary.SafeTryGetValue(toLocName);
            if (toLocObs == null)
                return "{0} failed: given to location name is not supported here".CheckedFormat(desc);

            if (!toLocObs.Info.IsUnoccupied)
                return "{0} failed: given to location is not empty [{1}]".CheckedFormat(desc, toLocObs.ContainsSubstInfo.ObjID.FullName);

            E090SubstLocObserver useArmObserver = null;

            if (substCurrentLocName == R1ArmALocID.Name)
                useArmObserver = robotArmALocObserver;
            else if (substCurrentLocName == R1ArmBLocID.Name)
                useArmObserver = robotArmBLocObserver;
            else if (robotArmALocObserver.Info.IsUnoccupied)
                useArmObserver = robotArmALocObserver;
            else if (robotArmBLocObserver.Info.IsUnoccupied)
                useArmObserver = robotArmBLocObserver;
            else
                return "{0} failed: neither robot arm is avilable".CheckedFormat(desc);

            string ec = string.Empty;

            if (ec.IsNullOrEmpty())
                ec = AcquireLocationTransferPermissionForThisItemIfNeeded(ipf, fromLocObs.ID.Name, toLocObs.ID.Name);

            if (useArmObserver.ID.Name != substCurrentLocName && ec.IsNullOrEmpty())
                ec = E039TableUpdater.NoteSubstMoved(substObserver, useArmObserver.ID);

            if (ec.IsNullOrEmpty() && !useArmObserver.ID.Equals(toLocObs.ID))
                ec = E039TableUpdater.NoteSubstMoved(substObserver, toLocObs.ID);

            if (ec.IsNullOrEmpty())
                ec = ReleaseAcquiredEndOfItemTransferPermissionsIfNeeded(ipf);

            return ec;
        }

        private string PerformItem(IProviderFacet ipf, SwapSubstratesItem item)
        {
            UpdateObservers();

            var substID = item.SubstID.MapNullToEmpty();
            var swapWithSubstID = item.SwapWithSubstID.MapNullToEmpty();

            var desc = "{0}[{1} with {2}]".CheckedFormat(item.GetType().GetTypeLeafName(), substID.FullName, swapWithSubstID.FullName);

            return PerformSwapSubstrates(ipf, substID, swapWithSubstID, desc);
        }

        private string PerformSwapSubstrates(IProviderFacet ipf, E039ObjectID substID, E039ObjectID swapWithSubstID, string desc)
        {
            var substObserver = new E090SubstObserver(substID.GetPublisher());
            var swapWithSubstObserver = new E090SubstObserver(swapWithSubstID.GetPublisher());

            if (!substObserver.Info.IsValid || !swapWithSubstObserver.Info.IsValid)
                return "{0} failed: one or both substrate IDs are not valid".CheckedFormat(desc);

            var substCurrentLocName = substObserver.Info.LocID;
            var swapWithSubstCurrentLocName = swapWithSubstObserver.Info.LocID;

            var fromLocObs = allSubstLocObserverByLocNameDicationary.SafeTryGetValue(substCurrentLocName);
            var toLocObs = allSubstLocObserverByLocNameDicationary.SafeTryGetValue(swapWithSubstCurrentLocName);

            if (fromLocObs == null)
                return "{0} failed: given substrate's current location is not supported here [{1}]".CheckedFormat(desc, substCurrentLocName);

            if (toLocObs == null)
                return "{0} failed: given substrate's current location is not supported here [{1}]".CheckedFormat(desc, swapWithSubstCurrentLocName);

            E090SubstLocObserver fromArmObserver = null, toArmObserver = null;

            if (substCurrentLocName == R1ArmALocID.Name && robotArmBLocObserver.Info.IsUnoccupied)
            {
                fromArmObserver = robotArmALocObserver;
                toArmObserver = robotArmBLocObserver;
            }
            else if (substCurrentLocName == R1ArmBLocID.Name && robotArmALocObserver.Info.IsUnoccupied)
            {
                fromArmObserver = robotArmBLocObserver;
                toArmObserver = robotArmALocObserver;
            }
            else if (robotArmALocObserver.Info.IsUnoccupied && robotArmBLocObserver.Info.IsUnoccupied)
            {
                fromArmObserver = robotArmALocObserver;
                toArmObserver = robotArmBLocObserver;
            }
            else
            {
                return "{0} failed: both robot arms must be availble unless the oringial substrate is already on one of them".CheckedFormat(desc);
            }

            string ec = string.Empty;

            if (ec.IsNullOrEmpty())
                ec = AcquireLocationTransferPermissionForThisItemIfNeeded(ipf, fromLocObs.ID.Name, toLocObs.ID.Name);

            if (fromArmObserver.ID.Name != substCurrentLocName && ec.IsNullOrEmpty())
                ec = E039TableUpdater.NoteSubstMoved(substObserver, fromArmObserver.ID);

            if (ec.IsNullOrEmpty())
                ec = E039TableUpdater.NoteSubstMoved(swapWithSubstObserver, toArmObserver.ID);

            if (ec.IsNullOrEmpty())
                ec = E039TableUpdater.NoteSubstMoved(substObserver, toLocObs.ID);

            if (ec.IsNullOrEmpty())
                ec = ReleaseAcquiredEndOfItemTransferPermissionsIfNeeded(ipf);

            return ec;
        }

        private string PerformItem(IProviderFacet ipf, MoveOrSwapSubstrateItem item)
        {
            UpdateObservers();

            var substID = item.SubstID.MapNullToEmpty();
            var toLocName = item.ToSubstLocName.MapNullToEmpty();

            var toLocObs = allSubstLocObserverByLocNameDicationary.SafeTryGetValue(toLocName);

            if (toLocObs == null || toLocObs.IsUnoccupied)
            {
                var desc = "{0}[{1} -> {2}]".CheckedFormat(item.GetType().GetTypeLeafName(), substID.FullName, toLocName);

                return PerformMoveSubstrate(ipf, substID, toLocName, desc);
            }
            else
            {
                var swapWithSubstID = toLocObs.ContainsSubstInfo.ObjID;

                var desc = "{0}[{1} with {2}]".CheckedFormat(item.GetType().GetTypeLeafName(), substID.FullName, swapWithSubstID.FullName);

                return PerformSwapSubstrates(ipf, substID, swapWithSubstID, desc);
            }
        }

        private string PerformItem(IProviderFacet ipf, ApproachLocationItem item)
        {
            return "";
        }

        private string PerformItem(IProviderFacet ipf, RunActionItem item)
        {
            bool onlyStartAction = item.Behavior.IsSet(RunActionBehaviorFlags.OnlyStartAction);
            bool ignoreFailures = item.Behavior.IsSet(RunActionBehaviorFlags.IgnoreFailures);

            IClientFacet icf = item.CreateICFIfNeeded();
            string ec = string.Empty;

            if (icf != null)
                ec = icf.Start();
            else
                ec = "No IClientFacet was provided";

            if (ec.IsNullOrEmpty() && icf != null && !onlyStartAction)
                ec = WaitForCompletion(ipf, icf);

            if (!ec.IsNullOrEmpty() && ignoreFailures)
            {
                if (icf != null)
                    Log.Debug.Emit("Ignoring failure [icf:{0} ec:{1}]", icf, ec);
                else
                    Log.Debug.Emit("Ignoring failure [ec:{0}]", ec);

                ec = string.Empty;
            }

            return ec;
        }

        private string PerformItem(IProviderFacet ipf, TransferPermissionRequestItem item)
        {
            var locNameList = (item.LocNameList ?? ReadOnlyIList<string>.Empty); 
            var locNameArray = locNameList.ToArray();
            var kvpArray = locNameList.Select(locName => KVP.Create(locName, ManualLocNameToITPRDictionary.SafeTryGetValue(locName))).ToArray();

            if (kvpArray.Any(kvp => kvp.Value == null))
                return "TransferPermissionRequestItem '{0}' is not supported for given location(s) [{1}]".CheckedFormat(item.Settings, String.Join(", ", kvpArray.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key)));

            string ec = WaitForPostedItemsComplete(ipf, locNameArray);

            var isRecursiveAcquire = item.Settings.IsSet(TransferPermissionRequestItemSettings.RecursiveAcquire);
            var isAcquireIfNeeded = !isRecursiveAcquire && item.Settings.IsSet(TransferPermissionRequestItemSettings.Acquire);
            var autoReleaseAtEndOfSequence = (isAcquireIfNeeded | isRecursiveAcquire) && item.Settings.IsSet(TransferPermissionRequestItemSettings.AutoReleaseAtEndOfSequence);
            var isReleaseIfNeeded = !isAcquireIfNeeded && !isRecursiveAcquire && item.Settings.IsSet(TransferPermissionRequestItemSettings.Release);
            var onlyStartRequest = item.Settings.IsSet(TransferPermissionRequestItemSettings.OnlyStartRequest);

            TransferPermissionRequestType requestType = TransferPermissionRequestType.None;
            if (ec.IsNullOrEmpty())
            {
                if (isAcquireIfNeeded)
                {
                    requestType = TransferPermissionRequestType.Acquire;
                    kvpArray = kvpArray.Where(kvp => !kvp.Value.TransferPermissionStatePublisher.Object.GetIsTransferPermitted(kvp.Key)).ToArray();
                }
                else if (isRecursiveAcquire)
                {
                    requestType = TransferPermissionRequestType.Acquire;
                }
                else if (isReleaseIfNeeded)
                {
                    requestType = TransferPermissionRequestType.Release;
                    kvpArray = kvpArray.Where(kvp => kvp.Value.TransferPermissionStatePublisher.Object.GetIsTransferPermitted(kvp.Key)).ToArray();
                }
                else
                {
                    ec = "Internal: TransferPermissionRequestItem Settings '{0}' unrecognized or unsupported".CheckedFormat(item.Settings);
                }
            }

            if (ec.IsNullOrEmpty())
            {
                if (onlyStartRequest)
                {
                    postedItemList.AddRange(kvpArray.Select(kvp => new PostedItem() { KVP = kvp, ICF = kvp.Value.TransferPermission(requestType, kvp.Key).StartInline() }));
                }
                else
                {
                    var icfArray = kvpArray.Select(kvp => kvp.Value.TransferPermission(requestType, kvp.Key).StartInline()).ToArray();
                    ec = WaitForCompletion(ipf, icfArray);
                }
            }

            // on release remove each item from the currentSequenceAutoReleaseKVPList whose name matches on of the locNames in the 
            if (ec.IsNullOrEmpty() && isReleaseIfNeeded)
            {
                var releasedLocNameArray = kvpArray.Select(kvp => kvp.Key).ToArray();
                currentSequenceAutoReleaseKVPList.FilterAndRemove(kvp => releasedLocNameArray.Contains(kvp.Key));
            }

            if (ec.IsNullOrEmpty() && autoReleaseAtEndOfSequence)
            {
                currentSequenceAutoReleaseKVPList.AddRange(kvpArray);
            }

            return ec;
        }

        private string WaitForPostedItemsComplete(IProviderFacet ipf, string[] locNameArray = null)
        {
            string ec = string.Empty;

            if (postedItemList.Count > 0)
            {
                PostedItem [] filteredPostedItemArray = null;

                if (locNameArray != null)
                    filteredPostedItemArray = postedItemList.FilterAndRemove(item => locNameArray.Contains(item.KVP.Key)).ToArray();
                else
                {
                    filteredPostedItemArray = postedItemList.ToArray();
                    postedItemList.Clear();
                }

                ec = WaitForCompletion(ipf, filteredPostedItemArray.Select(item => item.ICF).ToArray());

                if (ec.IsNullOrEmpty())
                    currentSequenceAutoReleaseKVPList.AddRange(filteredPostedItemArray.Select(item => item.KVP));
            }

            return ec;
        }

        private string AcquireLocationTransferPermissionForThisItemIfNeeded(IProviderFacet ipf, params string[] locNameParamsArray)
        {
            string ec = WaitForPostedItemsComplete(ipf, locNameParamsArray);

            if (ec.IsNullOrEmpty())
            {
                var neededKVPSet = locNameParamsArray.Select(locName => KVP.Create(locName, AutoLocNameToITPRDictionary.SafeTryGetValue(locName))).Where(kvp => kvp.Value != null && !kvp.Value.TransferPermissionStatePublisher.Object.GetIsTransferPermitted(kvp.Key)).ToArray();

                currentItemAutoReleaseKVPList.AddRange(neededKVPSet);      // request each one to get released even if the acquire fails

                var acquireICFArray = neededKVPSet.Select(kvp => kvp.Value.TransferPermission(TransferPermissionRequestType.Acquire, kvp.Key).StartInline()).ToArray();

                ec = WaitForCompletion(ipf, acquireICFArray);
            }

            return ec;
        }

        private string ReleaseAcquiredEndOfItemTransferPermissionsIfNeeded(IProviderFacet ipf)
        {
            string ec = string.Empty;

            if (currentItemAutoReleaseKVPList.Count > 0)
            {
                var releaseICFs = currentItemAutoReleaseKVPList.Select(kvp => kvp.Value.TransferPermission(TransferPermissionRequestType.Release, kvp.Key).StartInline()).ToArray();
                currentItemAutoReleaseKVPList.Clear();

                ec = WaitForCompletion(ipf, releaseICFs);
            }

            return ec;
        }

        private string ReleaseAcquiredEndOfSequenceTransferPermissionsIfNeeded(IProviderFacet ipf)
        {
            string ec = WaitForPostedItemsComplete(ipf);

            if (currentItemAutoReleaseKVPList.Count > 0)
            {
                currentSequenceAutoReleaseKVPList.AddRange(currentItemAutoReleaseKVPList);
                currentItemAutoReleaseKVPList.Clear();
            }

            if (currentSequenceAutoReleaseKVPList.Count > 0)
            {
                var releaseICFs = currentSequenceAutoReleaseKVPList.Select(kvp => kvp.Value.TransferPermission(TransferPermissionRequestType.Release, kvp.Key).StartInline()).ToArray();
                currentSequenceAutoReleaseKVPList.Clear();

                ec = WaitForCompletion(ipf, releaseICFs);
            }

            return ec;
        }

        private void NoteSequenceFailed(IProviderFacet ipf)
        {
            postedItemList.Clear();

            currentItemAutoReleaseKVPList.Clear();
            currentSequenceAutoReleaseKVPList.Clear();
        }

        public struct PostedItem
        {
            public KeyValuePair<string, ITransferPermissionRequest> KVP { get; set; }
            public IClientFacet ICF { get; set; }
        }

        private List<PostedItem> postedItemList = new List<PostedItem>(); 
        private List<KeyValuePair<string, ITransferPermissionRequest>> currentItemAutoReleaseKVPList = new List<KeyValuePair<string, ITransferPermissionRequest>>();
        private List<KeyValuePair<string, ITransferPermissionRequest>> currentSequenceAutoReleaseKVPList = new List<KeyValuePair<string, ITransferPermissionRequest>>();

        private E090SubstLocObserver[] lpSlotLocObserverArray;
        private E090SubstLocObserver robotArmALocObserver, robotArmBLocObserver;
        private E090SubstLocObserver[] allSubstLocObserverArray;

        private IDictionaryWithCachedArrays<string, E090SubstLocObserver> allSubstLocObserverByLocNameDicationary = new IDictionaryWithCachedArrays<string, E090SubstLocObserver>();

        private void UpdateObservers()
        {
            allSubstLocObserverArray.DoForEach(obs => obs.Update());
        }

        private string WaitForCompletion(IProviderFacet ipf, params IClientFacet[] icfParamsArray)
        {
            if (icfParamsArray.IsNullOrEmpty())
                return string.Empty;

            icfParamsArray.DoForEach(icf => icf.NotifyOnComplete.AddItem(this));

            string ec = string.Empty;

            for (; ;)
            {
                WaitForSomethingToDo();

                if (icfParamsArray.All(icf => icf.ActionState.IsComplete))
                    break;

                if (HasStopBeenRequested)
                {
                    ec = "Part has been asked to stop";
                    icfParamsArray.DoForEach(icf => icf.RequestCancel());
                    break;
                }

                bool cancelRequest = (ipf != null && ipf.IsCancelRequestActive) || (CurrentAction != null && CurrentAction.IsCancelRequestActive);
                if (cancelRequest && !icfParamsArray.All(icf => icf.IsCancelRequestActive))
                    icfParamsArray.DoForEach(icf => icf.RequestCancel());
            }

            icfParamsArray.DoForEach(icf => icf.NotifyOnComplete.RemoveItem(this));

            if (ec.IsNullOrEmpty())
            {
                var firstFailedICF = icfParamsArray.FirstOrDefault(icf => icf.ActionState.Failed);
                ec = (firstFailedICF != null) ? firstFailedICF.ActionState.ResultCode : string.Empty;
            }

            return ec;
        }
    }

    #endregion

    #region ISimpleExampleProcessModuleEngine (et. al.)

    /// <summary>
    /// Interface for a simple single slot example ProcessModule engine
    /// </summary>
    public interface ISimpleExampleProcessModuleEngine : IActivePartBase, ITransferPermissionRequest
    {
        IClientFacet RunProcess(E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS = SubstProcState.Processed, string autoReleaseTransferPermissionLocNameAtStart = null, string autoAcquireTransferPermissionLocNameAtEnd = null);
        E039ObjectID LocID { get; }
    }

    /// <summary>
    /// Implementation class for a simple single slot example ProcessModule engine
    /// </summary>
    public class SimpleExampleProcessModuleEngine : SimpleActivePartBase, ISimpleExampleProcessModuleEngine
    {
        public SimpleExampleProcessModuleEngine(string partID, IE039TableUpdater e039TableUpdater, string pmLocName = null)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion1.Build(waitTimeLimit: (0.0333).FromSeconds()))
        {
            E039TableUpdater = e039TableUpdater;

            e039TableUpdater.CreateE090SubstLoc(pmLocName, ao => locObserver = new E090SubstLocObserver(ao.AddedObjectPublisher));
            LocID = locObserver.ID;
        }

        public IE039TableUpdater E039TableUpdater { get; private set; }

        E090SubstLocObserver locObserver;

        public E039ObjectID LocID { get; private set; }

        public IClientFacet TransferPermission(TransferPermissionRequestType requestType, string locName = "")
        {
            string desc = "{0}({1}{2})".CheckedFormat(CurrentMethodName, requestType, locName != null ? ", loc:{0}".CheckedFormat(locName) : "");
            return new BasicActionImpl(actionQ, ipf => PerformTransferPermission(ipf, requestType, locName), desc, ActionLoggingReference);
        }

        public INotificationObject<ITokenSet<string>> TransferPermissionStatePublisher { get { return transferPermissionStatePublisher; } }
        InterlockedNotificationRefObject<ITokenSet<string>> transferPermissionStatePublisher = new InterlockedNotificationRefObject<ITokenSet<string>>(TokenSet<string>.Empty);

        ITokenSet<string> lastPublishedTransferPermissionState = TokenSet<string>.Empty;

        TokenSet<string> transferPermisionState = new TokenSet<string>();

        private void PublishStateIfNeeded()
        {
            if (!transferPermisionState.Equals(lastPublishedTransferPermissionState, compareReadOnly: false))
                transferPermissionStatePublisher.Object = (lastPublishedTransferPermissionState = transferPermisionState.ConvertToReadOnly());
        }

        bool isLocationLockedForTransfer = false;

        private string PerformTransferPermission(IProviderFacet ipf, TransferPermissionRequestType requestType, string locName)
        {
            switch (requestType)
            {
                case TransferPermissionRequestType.Acquire:
                    if (engineState != EngineState.Idle)
                        return "Not valid in engine state {0}".CheckedFormat(engineState);

                    transferPermisionState.Add(locName);
                    break;

                case TransferPermissionRequestType.Release:
                    transferPermisionState.Remove(locName);
                    break;

                case TransferPermissionRequestType.ReleaseAll:
                    transferPermisionState.Clear();
                    break;

                default:
                    return "Unsupported TransferPermissionRequest type {0}".CheckedFormat(requestType);
            }

            UpdateTransferPermission();

            return "";
        }

        private void UpdateTransferPermission(bool publish = true)
        {
            isLocationLockedForTransfer = transferPermisionState.Count > 0;

            if (publish)
                PublishStateIfNeeded();
        }

        public IClientFacet RunProcess(E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS = SubstProcState.Processed, string autoReleaseTransferPermissionLocNameAtStart = null, string autoAcquireTransferPermissionLocNameAtEnd = null)
        {
            StringBuilder sb = new StringBuilder(CurrentMethodName);
            sb.CheckedAppendFormat("({0}, {1}, sps:{2}", substID.FullName, stepSpec, resultingSPS);

            if (autoReleaseTransferPermissionLocNameAtStart == "")
                sb.Append(", AutoRelease");
            else if (autoReleaseTransferPermissionLocNameAtStart != null)
                sb.CheckedAppendFormat(", AutoRelease:{0}", autoReleaseTransferPermissionLocNameAtStart);

            if (autoAcquireTransferPermissionLocNameAtEnd == "")
                sb.Append(", AutoAcquire");
            else if (autoAcquireTransferPermissionLocNameAtEnd != null)
                sb.CheckedAppendFormat(", AutoAcquire:{0}", autoAcquireTransferPermissionLocNameAtEnd);

            sb.Append(")");

            return new BasicActionImpl(actionQ, ipf => PerformRunProcess(ipf, substID, stepSpec, resultingSPS, autoReleaseTransferPermissionLocNameAtStart, autoAcquireTransferPermissionLocNameAtEnd), sb.ToString(), ActionLoggingReference);
        }

        private string PerformRunProcess(IProviderFacet ipf, E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS, string autoReleaseTransferPermissionLocNameAtStart, string autoAcquireTransferPermissionLocNameAtEnd)
        {
            if (autoReleaseTransferPermissionLocNameAtStart != null && autoReleaseTransferPermissionLocNameAtStart != LocID.Name && autoReleaseTransferPermissionLocNameAtStart != "")
                return "AutoReleaseTransferPermissionLocNameAtStart value '{0}' is not a valid process start location".CheckedFormat(autoReleaseTransferPermissionLocNameAtStart);

            if (autoAcquireTransferPermissionLocNameAtEnd != null && autoAcquireTransferPermissionLocNameAtEnd != LocID.Name && autoAcquireTransferPermissionLocNameAtEnd != "")
                return "AutoAcquireTransferPermissionLocNameAtEnd value '{0}' is not a valid process output location".CheckedFormat(autoAcquireTransferPermissionLocNameAtEnd);

            TimeSpan processTime = stepSpec.StepVariables["ProcessTime"].VC.GetValue<TimeSpan>(rethrow: false);

            if (!BaseState.UseState.IsOnline(acceptOnlineFailure: false))
                return "Part is not online [{0}]".CheckedFormat(BaseState);

            if (autoReleaseTransferPermissionLocNameAtStart != null)
            {
                transferPermisionState.Remove(autoReleaseTransferPermissionLocNameAtStart);
                UpdateTransferPermission();
            }

            if (isLocationLockedForTransfer)
                return "Cannot run process: module is locked for transfer [{0}]".CheckedFormat(transferPermisionState);

            substID = substID.MapNullToEmpty();

            var substObserver = new E090SubstObserver(substID.GetPublisher());

            if (!substObserver.Info.IsValid)
                return "The given {0} is not a valid substrate".CheckedFormat(substID.FullName);

            PerformMainLoopService();

            if (!locObserver.ContainsSubstInfo.ObjID.Equals(substID))
                return "Cannot process {0}: it is not actual in this module [{1}]".CheckedFormat(substID.FullName, locObserver.ID.Name);

            if (engineState != EngineState.Idle)
                return "Not valid in engine state {0}".CheckedFormat(engineState);

            if (currentProcessTracker != null)
                return "Only one RunProcess request is valid at a time [currently processing {0}]".CheckedFormat(currentProcessTracker.SubstObserver.ID.FullName);

            ProcessTracker pt = new ProcessTracker()
            {
                IPF = ipf,
                ResultingSPS = resultingSPS,
                SubstObserver = substObserver,
                ProcessTime = processTime,
                AutoAcquireTransferPermissionLocNameAtEnd = autoAcquireTransferPermissionLocNameAtEnd,
                BusyToken = CreateInternalBusyFlagHolderObject(flagName: "Running Process for {0}".CheckedFormat(substID.FullName)),
            };

            currentProcessTracker = pt;

            SetEngineState(EngineState.RunningProcess, "Starting process for {0}".CheckedFormat(substID.FullName));

            return null;
        }

        private class ProcessTracker
        {
            public IProviderFacet IPF { get; set; }
            public SubstProcState ResultingSPS { get; set; }
            public E090SubstObserver SubstObserver { get; set; }
            public TimeSpan ProcessTime { get; set; }
            public string AutoAcquireTransferPermissionLocNameAtEnd { get; set; }
            public IDisposable BusyToken { get; set; }

            public void CompleteRequest(string ec)
            {
                if (!IPF.ActionState.IsComplete)
                    IPF.CompleteRequest(ec);

                BusyToken.DisposeOfGivenObject();
                BusyToken = null;
            }
        }

        private ProcessTracker currentProcessTracker = null;

        enum EngineState
        {
            Idle,
            RunningProcess,
        }

        EngineState engineState = EngineState.Idle;
        QpcTimeStamp engineStateTS = QpcTimeStamp.Now;

        private void SetEngineState(EngineState nextEngineState, string reason)
        {
            EngineState entryEngineState = engineState;

            engineState = nextEngineState;
            engineStateTS = QpcTimeStamp.Now;

            Log.Debug.Emit("EngineState change to {0} [from:{1}, reason:{2}]", engineState, entryEngineState, reason);

            if (entryEngineState == EngineState.RunningProcess && currentProcessTracker != null)
            {
                E039TableUpdater.SetPendingSubstProcState(currentProcessTracker.SubstObserver, SubstProcState.Processed.MergeWith(currentProcessTracker.ResultingSPS));
                currentProcessTracker.CompleteRequest("");

                if (currentProcessTracker.AutoAcquireTransferPermissionLocNameAtEnd != null)
                {
                    transferPermisionState.Add(currentProcessTracker.AutoAcquireTransferPermissionLocNameAtEnd);
                    UpdateTransferPermission();
                }

                currentProcessTracker = null;
            }

            if (engineState == EngineState.RunningProcess && currentProcessTracker != null)
                E039TableUpdater.SetPendingSubstProcState(currentProcessTracker.SubstObserver, SubstProcState.InProcess);
        }

        protected override void PerformMainLoopService()
        {
            locObserver.Update();

            if (currentProcessTracker != null && currentProcessTracker.IPF.ActionState.IsComplete)
                Fail("Found current ProcessTracker is complete while {0}".CheckedFormat(engineState));

            if (!BaseState.UseState.IsOnline(acceptOnlineFailure: false))
                return;

            switch (engineState)
            {
                case EngineState.Idle:
                    break;

                case EngineState.RunningProcess:
                    if (currentProcessTracker != null)
                    {
                        if (engineStateTS.Age >= currentProcessTracker.ProcessTime)
                            SetEngineState(EngineState.Idle, "Process complete");
                    }
                    else
                    {
                        Fail("Found current ProcessTracker is null while {0}".CheckedFormat(engineState));
                    }
                    break;
            }
        }

        private void Fail(string reason)
        {
            SetBaseState(UseState.OnlineFailure, reason);

            if (currentProcessTracker != null)
            {
                currentProcessTracker.CompleteRequest(reason);
                currentProcessTracker = null;
            }
        }
    }

    #endregion

    #region IBeltExampleProcessModuleEngine (et. al.)

    /// <summary>
    /// Interface for a belt type mutli-station example ProcessModule engine (linear or circular)
    /// <para/>Please note that on a circular version, the OutputLocID is the same as the InputLocID and both Request/Release actions set/clear both IsReadyForLoad and IsReadyForUnload
    /// </summary>
    public interface IBeltExampleProcessModuleEngine : IActivePartBase, ITransferPermissionRequest
    {
        BeltExampleProcessModuleEngineType EngineType { get; }
        int NumStations { get; }
        TimeSpan BeltMoveTime { get; }

        IClientFacet RunProcess(E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS = SubstProcState.Processed, string autoReleaseTransferPermissionLocNameAtStart = null, string autoAcquireTransferPermissionLocNameAtEnd = null);

        E039ObjectID[] StationLocIDArray { get; }
        E039ObjectID[] BeltLocIDArray { get; }
        E039ObjectID InputLocID { get; }
        E039ObjectID OutputLocID { get; }
    }

    /// <summary>
    /// Enumeration of the supported behaviors for the BeltExampleProcessModuleEngine.
    /// <para/>Linear, Circular
    /// </summary>
    public enum BeltExampleProcessModuleEngineType
    {
        Linear,
        Circular,
    }

    public class BeltExampleProcessModuleEngine : SimpleActivePartBase, IBeltExampleProcessModuleEngine
    {
        public BeltExampleProcessModuleEngine(string partID, IE039TableUpdater e039TableUpdater, int numStations = 4, TimeSpan? beltMoveTime = null, string locBaseName = null, BeltExampleProcessModuleEngineType engineType = BeltExampleProcessModuleEngineType.Linear)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion1.Build(waitTimeLimit: (0.0333).FromSeconds()))
        {
            EngineType = engineType;

            int minValidStation = IsCircular ? 2 : 3;
            if (numStations < minValidStation)
                throw new System.ArgumentException(paramName: "numStations", message: "value must be {0} or larger for {1} engine".CheckedFormat(minValidStation, engineType));

            E039TableUpdater = e039TableUpdater;
            NumStations = numStations;
            BeltMoveTime = beltMoveTime ?? (0.1).FromSeconds();
            NumProcessStations = IsCircular ? (NumStations - 1) : (NumStations - 2);

            stationLocObserverArray = Enumerable.Range(1, NumStations).Select(slotNum => { E090SubstLocObserver obs = null; e039TableUpdater.CreateE090SubstLoc("{0}.Station{1}".CheckedFormat(locBaseName, slotNum), ao => { obs = new E090SubstLocObserver(ao.AddedObjectPublisher); }, instanceNum: slotNum); return obs; }).ToArray();
            processStationsLocObserverArray = stationLocObserverArray.Skip(1).Take(NumProcessStations).ToArray();
            beltLocObserverArray = Enumerable.Range(0, NumStations).Select(slotIndex => { E090SubstLocObserver obs = null; e039TableUpdater.CreateE090SubstLoc("{0}.Belt.{1}".CheckedFormat(locBaseName, (char)(slotIndex + 'A')), ao => { obs = new E090SubstLocObserver(ao.AddedObjectPublisher); }, instanceNum: slotIndex + 1); return obs; }).ToArray();

            StationLocIDArray = stationLocObserverArray.Select(obs => obs.ID).ToArray();
            BeltLocIDArray = beltLocObserverArray.Select(obs => obs.ID).ToArray();
            InputLocID = StationLocIDArray.First();
            OutputLocID = (engineType == BeltExampleProcessModuleEngineType.Circular) ? InputLocID : StationLocIDArray.Last();

            allLocObserverArray = stationLocObserverArray.Concat(beltLocObserverArray).ToArray();
            inputLocObserver = stationLocObserverArray.First();

            if (IsCircular)
                outputLocObserver = inputLocObserver;
            else
                outputLocObserver = stationLocObserverArray.Last();
        }

        public BeltExampleProcessModuleEngineType EngineType { get; private set; }
        public IE039TableUpdater E039TableUpdater { get; private set; }
        public int NumStations { get; private set; }
        public TimeSpan BeltMoveTime { get; private set; }
        public int NumProcessStations { get; private set; }

        bool IsCircular { get { return (EngineType == BeltExampleProcessModuleEngineType.Circular); } }

        E090SubstLocObserver[] stationLocObserverArray;
        E090SubstLocObserver[] processStationsLocObserverArray;
        E090SubstLocObserver[] beltLocObserverArray;

        E090SubstLocObserver[] allLocObserverArray;
        E090SubstLocObserver inputLocObserver, outputLocObserver;

        public E039ObjectID[] StationLocIDArray { get; private set; }
        public E039ObjectID[] BeltLocIDArray { get; private set; }
        public E039ObjectID InputLocID { get; private set; }
        public E039ObjectID OutputLocID { get; private set; }

        public IClientFacet TransferPermission(TransferPermissionRequestType requestType, string locName)
        {
            string desc = "{0}({1}{2})".CheckedFormat(CurrentMethodName, requestType, locName != null ? ", loc:{0}".CheckedFormat(locName) : "");
            return new BasicActionImpl(actionQ, ipf => PerformTransferPermission(ipf, requestType, locName), desc, ActionLoggingReference);
        }

        public INotificationObject<ITokenSet<string>> TransferPermissionStatePublisher { get { return transferPermissionStatePublisher; } }
        private InterlockedNotificationRefObject<ITokenSet<string>> transferPermissionStatePublisher = new InterlockedNotificationRefObject<ITokenSet<string>>(TokenSet<string>.Empty);

        ITokenSet<string> lastPublishedTransferPermissionState = TokenSet<string>.Empty;
        TokenSet<string> transferPermissionState = new TokenSet<string>();

        bool isInputLocationLockedForTransfer, isOutputLocationLockedForTransfer;

        private void PublishStateIfNeeded()
        {
            if (!transferPermissionState.Equals(lastPublishedTransferPermissionState, compareReadOnly: false))
                transferPermissionStatePublisher.Object = lastPublishedTransferPermissionState = transferPermissionState.ConvertToReadOnly();
        }

        public IClientFacet RunProcess(E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS = SubstProcState.Processed, string autoReleaseTransferPermissionLocNameAtStart = null, string autoAcquireTransferPermissionLocNameAtEnd = null)
        {
            StringBuilder sb = new StringBuilder(CurrentMethodName);
            sb.CheckedAppendFormat("({0}, {1}, sps:{2}", substID.FullName, stepSpec, resultingSPS);
            
            if (autoReleaseTransferPermissionLocNameAtStart == "")
                sb.Append(", AutoRelease");
            else if (autoReleaseTransferPermissionLocNameAtStart != null)
                sb.CheckedAppendFormat(", AutoRelease:{0}", autoReleaseTransferPermissionLocNameAtStart);

            if (autoAcquireTransferPermissionLocNameAtEnd == "")
                sb.Append(", AutoAcquire");
            else if (autoAcquireTransferPermissionLocNameAtEnd != null)
                sb.CheckedAppendFormat(", AutoAcquire:{0}", autoAcquireTransferPermissionLocNameAtEnd);

            sb.Append(")");

            return new BasicActionImpl(actionQ, ipf => PerformRunProcess(ipf, substID, stepSpec, resultingSPS, autoReleaseTransferPermissionLocNameAtStart, autoAcquireTransferPermissionLocNameAtEnd), sb.ToString(), ActionLoggingReference);
        }

        private string PerformTransferPermission(IProviderFacet ipf, TransferPermissionRequestType requestType, string locName)
        {
            if (locName != InputLocID.Name && locName != OutputLocID.Name && !(IsCircular && locName == ""))
                return "Given location name '{0}' is not valid for this part".CheckedFormat(locName);

            switch (requestType)
            {
                case TransferPermissionRequestType.Acquire:
                    if (engineState == EngineState.MovingBelt)
                    {
                        pendingAcquireKVPList.Add(KVP.Create(locName, ipf));

                        Log.Debug.Emit("Belt is moving: put '{0}' in pending list", ipf.ToString(ToStringSelect.MesgAndDetail));

                        return null;
                    }

                    transferPermissionState.Add(locName);
                    break;

                case TransferPermissionRequestType.Release:
                    transferPermissionState.Remove(locName);
                    break;

                case TransferPermissionRequestType.ReleaseAll:
                    while (transferPermissionState.Contains(locName))
                        transferPermissionState.Remove(locName);
                    break;

                default:
                    return "TransferPermissionRequestType {0} is not valid".CheckedFormat(requestType);
            }

            UpdateTransferPermissions();

            return "";
        }

        List<KeyValuePair<string, IProviderFacet>> pendingAcquireKVPList = new List<KeyValuePair<string, IProviderFacet>>();

        private void UpdateTransferPermissions(bool publish = true)
        {
            isInputLocationLockedForTransfer = transferPermissionState.Contains(InputLocID.Name) || (IsCircular && transferPermissionState.Contains(""));
            isOutputLocationLockedForTransfer = IsCircular ? isInputLocationLockedForTransfer : transferPermissionState.Contains(OutputLocID.Name);

            if (publish)
                PublishStateIfNeeded();
        }

        private string PerformRunProcess(IProviderFacet ipf, E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS, string autoReleaseTransferPermissionLocNameAtStart, string autoAcquireTransferPermissionLocNameAtEnd)
        {
            if (autoReleaseTransferPermissionLocNameAtStart != null && autoReleaseTransferPermissionLocNameAtStart != InputLocID.Name && !(IsCircular && autoReleaseTransferPermissionLocNameAtStart == ""))
                return "AutoReleaseTransferPermissionLocNameAtStart value '{0}' is not a valid process start location".CheckedFormat(autoReleaseTransferPermissionLocNameAtStart);

            if (autoAcquireTransferPermissionLocNameAtEnd != null && autoAcquireTransferPermissionLocNameAtEnd != OutputLocID.Name && !(IsCircular && autoAcquireTransferPermissionLocNameAtEnd == ""))
                return "AutoAcquireTransferPermissionLocNameAtEnd value '{0}' is not a valid process output location".CheckedFormat(autoAcquireTransferPermissionLocNameAtEnd);

            TimeSpan stepInterval = stepSpec.StepVariables["StepInterval"].VC.GetValue<TimeSpan>(rethrow: false);

            if (!BaseState.UseState.IsOnline(acceptOnlineFailure: false))
                return "Part is not online [{0}]".CheckedFormat(BaseState);

            if (autoReleaseTransferPermissionLocNameAtStart != null)
            {
                transferPermissionState.Remove(autoReleaseTransferPermissionLocNameAtStart);
                UpdateTransferPermissions();
            }

            substID = substID.MapNullToEmpty();

            var substObserver = new E090SubstObserver(substID.GetPublisher());

            if (!substObserver.Info.IsValid)
                return "The given {0} is not a valid substrate".CheckedFormat(substID.FullName);

            PerformMainLoopService();

            if (!inputLocObserver.ContainsSubstInfo.ObjID.Equals(substID))
                return "Cannot process {0}: it is not at the place location [{1}]".CheckedFormat(substID.FullName, inputLocObserver.ID.Name);

            ProcessTracker pt = new ProcessTracker()
            {
                IPF = ipf,
                ResultingSPS = resultingSPS,
                SubstObserver = substObserver,
                StepInterval = stepInterval,
                AutoAcquireTransferPermissionLocNameAtEnd = autoAcquireTransferPermissionLocNameAtEnd,
                BusyToken = CreateInternalBusyFlagHolderObject(flagName: "Running Process for {0}".CheckedFormat(substID.FullName)),
            };

            ptBySubstFullNameDictionary[substObserver.ID.FullName] = pt;

            return null;
        }

        private class ProcessTracker
        {
            public IProviderFacet IPF { get; set; }
            public SubstProcState ResultingSPS { get; set; }
            public E090SubstObserver SubstObserver { get; set; }
            public TimeSpan StepInterval { get; set; }
            public string AutoAcquireTransferPermissionLocNameAtEnd { get; set; }
            public IDisposable BusyToken { get; set; }
            public bool HasBeenStarted { get; set; }

            public void CompleteRequest(string ec, bool andSetPendingSPS = true)
            {
                if (!IPF.ActionState.IsComplete)
                    IPF.CompleteRequest(ec);

                BusyToken.DisposeOfGivenObject();
                BusyToken = null;
            }
        }

        private IDictionaryWithCachedArrays<string, ProcessTracker> ptBySubstFullNameDictionary = new IDictionaryWithCachedArrays<string, ProcessTracker>();

        enum EngineState
        {
            Waiting,
            MovingBelt,
            RunningProcess,
        }

        int beltPosition = 0;
        int[] beltStationIndexByPositionArray;
        EngineState engineState = EngineState.Waiting;
        QpcTimeStamp engineStateTS = QpcTimeStamp.Now;
        TimeSpan maxStepInterval = TimeSpan.Zero;

        E039ObjectID processedSubstIDAtOutputLoc = E039ObjectID.Empty;

        private void SetEngineState(EngineState nextEngineState, string reason)
        {
            EngineState entryEngineState = engineState;

            engineState = nextEngineState;
            engineStateTS = QpcTimeStamp.Now;

            Log.Debug.Emit("EngineState change to {0} [from:{1}, reason:{2}]", engineState, entryEngineState, reason);

            if (entryEngineState == EngineState.RunningProcess)
            {
                foreach (var processStationLocObserver in processStationsLocObserverArray)
                {
                    var pt = ptBySubstFullNameDictionary.SafeTryGetValue(processStationLocObserver.ContainsSubstInfo.ObjID.FullName);

                    if (pt != null)
                        E039TableUpdater.SetPendingSubstProcState(pt.SubstObserver, SubstProcState.Processed.MergeWith(pt.ResultingSPS));
                }
            }

            {
                beltStationIndexByPositionArray = Enumerable.Range(0, NumStations).Select(posIndex => (beltPosition - posIndex + NumStations) % NumStations).ToArray();

                var stationLocObserversByBeltPositionArray = beltStationIndexByPositionArray.Select(stationIndex => stationLocObserverArray[stationIndex]).ToArray();

                if (engineState == EngineState.MovingBelt)
                {
                    // move wafers from stations to belt
                    foreach (var beltPositionIndex in Enumerable.Range(0, NumStations))
                    {
                        var beltLocObserver = beltLocObserverArray[beltPositionIndex];
                        var stationLocObserver = stationLocObserversByBeltPositionArray[beltPositionIndex];

                        if (stationLocObserver.Info.IsOccupied)
                            E039TableUpdater.NoteSubstMoved(stationLocObserver.ContainsSubstInfo, beltLocObserver.ID);
                    }

                    foreach (var pt in ptBySubstFullNameDictionary.ValueArray)
                    {
                        if (!pt.HasBeenStarted)
                            pt.HasBeenStarted = true;
                    }
                }
                else if (entryEngineState == EngineState.MovingBelt)
                {
                    // move wafers from belt back to stations
                    foreach (var beltPositionIndex in Enumerable.Range(0, NumStations))
                    {
                        var beltLocObserver = beltLocObserverArray[beltPositionIndex];
                        var stationLocObserver = stationLocObserversByBeltPositionArray[beltPositionIndex];

                        if (beltLocObserver.Info.IsOccupied)
                            E039TableUpdater.NoteSubstMoved(beltLocObserver.ContainsSubstInfo, stationLocObserver.ID);
                    }

                    stationLocObserverArray.DoForEach(obs => obs.Update());

                    // on transition from MovingBelt to either RunningProcess or Waiting, take the substrate that just arrived at the output location and finish its process.
                    var ptAtPickLoc = ptBySubstFullNameDictionary.SafeTryGetValue(outputLocObserver.ContainsSubstInfo.ObjID.FullName);
                    if (ptAtPickLoc != null)
                    {
                        processedSubstIDAtOutputLoc = ptAtPickLoc.SubstObserver.ID;

                        ptBySubstFullNameDictionary.Remove(ptAtPickLoc.SubstObserver.ID.FullName);
                        ptAtPickLoc.CompleteRequest("");
                        if (ptAtPickLoc.AutoAcquireTransferPermissionLocNameAtEnd != null)
                        {
                            transferPermissionState.Add(ptAtPickLoc.AutoAcquireTransferPermissionLocNameAtEnd);
                            UpdateTransferPermissions();
                        }
                    }

                    if (pendingAcquireKVPList.Count > 0)
                    {
                        pendingAcquireKVPList.DoForEach(kvp => transferPermissionState.Add(kvp.Key));

                        UpdateTransferPermissions();

                        pendingAcquireKVPList.DoForEach(kvp => kvp.Value.CompleteRequest(""));

                        pendingAcquireKVPList.Clear();
                    }
                }
            }

            if (engineState == EngineState.RunningProcess)
            {
                var activePTsArray = ptBySubstFullNameDictionary.ValueArray.Where(pt => !pt.IPF.ActionState.IsComplete).ToArray();
                maxStepInterval = activePTsArray.IsNullOrEmpty() ? TimeSpan.Zero : activePTsArray.Max(pt => pt.StepInterval);

                stationLocObserverArray.DoForEach(obs => obs.Update());

                foreach (var processStationLocObserver in processStationsLocObserverArray)
                {
                    var pt = ptBySubstFullNameDictionary.SafeTryGetValue(processStationLocObserver.ContainsSubstInfo.ObjID.FullName);

                    if (pt != null)
                        E039TableUpdater.SetPendingSubstProcState(pt.SubstObserver, SubstProcState.InProcess);
                }
            }
        }

        protected override void PerformMainLoopService()
        {
            if (allLocObserverArray.Any(obs => obs.IsUpdateNeeded))
                allLocObserverArray.DoForEach(obs => obs.Update());

            foreach (var pt in ptBySubstFullNameDictionary.ValueArray)
            {
                if (pt.IPF.ActionState.IsComplete)
                {
                    Fail("Found active ProcessTracker is complete while {0}".CheckedFormat(engineState));
                    break;
                }
            }

            if (!BaseState.UseState.IsOnline(acceptOnlineFailure: false))
                return;

            int beltWaferCount = beltLocObserverArray.Count(obs => !obs.Info.IsUnoccupied);
            int stationWaferCount = stationLocObserverArray.Count(obs => !obs.Info.IsUnoccupied);

            if (beltWaferCount > 0 && engineState != EngineState.MovingBelt)
            {
                Fail("Internal: found {0} wafers on belt in state {1}".CheckedFormat(beltWaferCount, engineState));
                return;
            }

            bool havePendingWaferAtInput = false;
            bool haveProcessedWaferAtOutput = false;

            if (inputLocObserver.IsOccupied)
            {
                var pt = ptBySubstFullNameDictionary.SafeTryGetValue(inputLocObserver.ContainsSubstInfo.ObjID.FullName);
                havePendingWaferAtInput = (pt == null);
            }

            if (outputLocObserver.IsOccupied)
                haveProcessedWaferAtOutput = outputLocObserver.ContainsSubstInfo.ObjID.Equals(processedSubstIDAtOutputLoc);
            else
                processedSubstIDAtOutputLoc = E039ObjectID.Empty;

            switch (engineState)
            {
                case EngineState.Waiting:
                    if (isInputLocationLockedForTransfer || isOutputLocationLockedForTransfer)
                        break;     // keep waiting until the load and unload stations have been released

                    if ((stationWaferCount <= 0) || havePendingWaferAtInput || haveProcessedWaferAtOutput)
                        break;     // keep waiting until we have at least one wafer available for process or in process

                    SetEngineState(EngineState.MovingBelt, "Starting Belt move and then next process step");

                    break;

                case EngineState.MovingBelt:
                    if (engineStateTS.Age >= BeltMoveTime)
                    {
                        beltPosition = (beltPosition + 1) % NumStations;
                        SetEngineState(EngineState.RunningProcess, "Belt move completed");
                    }
                    break;

                case EngineState.RunningProcess:
                    if (engineStateTS.Age >= maxStepInterval)
                    {
                        SetEngineState(EngineState.Waiting, "Process complete");
                    }
                    break;
            }
        }

        private void Fail(string reason)
        {
            SetBaseState(UseState.OnlineFailure, reason);

            ptBySubstFullNameDictionary.ValueArray.DoForEach(pt => pt.CompleteRequest(reason));
            ptBySubstFullNameDictionary.Clear();
        }
    }

    #endregion
}