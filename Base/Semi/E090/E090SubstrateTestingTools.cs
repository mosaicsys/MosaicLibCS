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
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Semi.E039;
using MosaicLib.Semi.E041;
using MosaicLib.Semi.E090;
using MosaicLib.Semi.E090.SubstrateRouting;
using MosaicLib.Semi.E090.SubstrateScheduling;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;


namespace MosaicLib.Semi.E090.SubstrateTestingTools
{
    /*
     * The classes and defintions here are generally used as part of the Scheduler related addition to the E090 namespace concepts.
     * 
     * The SubstrateTestingTools namespace contains a set of implementation objects that serve to demonstrate use of the set of concepts that are found in the sub-namespaces of
     * the Semi.E090 namespace.  These classes are also used as the basis for a number of E090 and related unit tests.
     * 
     * TestECSParts: a test harness class that is used to setup and construct a standard set of parts as used with the unit tests.
     * 
     * TestSchedulerEngine, TestSubstrateAndProcessTracker and TestSubstrateSchedulerTool: together these define a complete scheduler that is able to specify and execute a 
     *   movement and processing sequence.  The TestSubstrateSchedulerTool also makes use of two Error Annunciators that are used to direct the scheduler in how to handle
     *   routing and prepare related failures.
     *   
     * (I)SimpleExampleProcessModuleEngine: this part is used as an example simple process module with a single location that supports both the IPrepare and ITPR interfaces.
     *   This part also supports fault injection so that it can be used to test process failure scenerios.
     *   
     * (I)BeltExampleProcessModuleEngine: this part is used as an example belt and disk process module with a number of locations that are used sequentially.  
     *   It supports ITPR but not currently IPrepare.
     * 
     * Together these parts may be used to generate mockp-ups of various processing and scheduling use patterns.
     */

    #region TestECSParts

    /// <summary>
    /// This is a test version of a container/pointer/construction/destruction helper class that is used to construct and setup a set of representative parts that can be used for
    /// testing substrate routing and processing related code.  This includes 4 PMs: 2 single wafer PMs (PM1, PM2), a linear belt 4 station PM (PM3) and a circular belt 4 station PM (PM4).
    /// It also includes a corresponding ISubstrateRoutingManager (SRM) that is used to construct a set of substrate locations, a set of test substrates, and which can be used to move
    /// these substrates through the tool for processing.
    /// </summary>
    public class TestECSParts : DisposableBase
    {
        public TestECSParts(string baseName, ReferenceSet<E039Object> e090HistorySet = null, int numLPSlotsAndWafers = 5, bool disableR1ArmB = false)
            : this(baseName, new E039BasicTablePartConfig(baseName + ".E039Table") { DefaultFallbackReferenceHistorySet = e090HistorySet }, numLPSlotsAndWafers: numLPSlotsAndWafers, disableR1ArmB: disableR1ArmB)
        { }

        public TestECSParts(string baseName, E039BasicTablePartConfig e039BasicTablePartConfig, bool e039AutoGoOnline = true, bool otherPartsAutoGoOnline = true, int numLPSlotsAndWafers = 5, IConfig iConfig = null, bool disableR1ArmB = false)
        {
            iConfig = iConfig ?? Config.Instance;

            ANManagerPart = new E041.ANManagerPart(baseName + ".ANManager", ivi: e039BasicTablePartConfig.ObjectIVI, isi: e039BasicTablePartConfig.ISI, iConfig: iConfig);

            E039TableUpdater = new E039BasicTablePart(e039BasicTablePartConfig).RunGoOnlineActionInline();
            if (e039AutoGoOnline)
                E039TableUpdater.RunGoOnlineAction();

            PM1 = new SimpleExampleProcessModuleEngine(baseName + ".PM1", this, pmLocName: "PM1", enableAlmostAvailable: true);
            PM2 = new SimpleExampleProcessModuleEngine(baseName + ".PM2", this, pmLocName: "PM2", enableAlmostAvailable: false);
            PM3 = new BeltExampleProcessModuleEngine(baseName + ".PM3", E039TableUpdater, locBaseName: "PM3", engineType: BeltExampleProcessModuleEngineType.Linear, enableAlmostAvailable: true);
            PM4 = new BeltExampleProcessModuleEngine(baseName + ".PM4", E039TableUpdater, locBaseName: "PM4", engineType: BeltExampleProcessModuleEngineType.Circular, enableAlmostAvailable: true);

            PMReject = new SimpleExampleProcessModuleEngine(baseName + ".PMReject", this, pmLocName: "PMReject");
            PMAbort = new SimpleExampleProcessModuleEngine(baseName + ".PMAbort", this, pmLocName: "PMAbort");
            PMReturn = new SimpleExampleProcessModuleEngine(baseName + ".PMReturn", this, pmLocName: "PMReturn");

            SRMLocNameToITPRDictionary = new ReadOnlyIDictionary<string, ITransferPermissionRequest>(
                    new KeyValuePair<string, ITransferPermissionRequest> []
                    {
                        KVP.Create(PM1.LocID.Name, (ITransferPermissionRequest) PM1),
                        KVP.Create(PM2.LocID.Name, (ITransferPermissionRequest) PM2),
                        KVP.Create(PM3.InputLocID.Name, (ITransferPermissionRequest) PM3),
                        KVP.Create(PM3.OutputLocID.Name, (ITransferPermissionRequest) PM3),
                        KVP.Create(PM4.InputLocID.Name, (ITransferPermissionRequest) PM4),
                        KVP.Create(PMReject.LocID.Name, (ITransferPermissionRequest) PMReject),
                        KVP.Create(PMAbort.LocID.Name, (ITransferPermissionRequest) PMAbort),
                        KVP.Create(PMReturn.LocID.Name, (ITransferPermissionRequest) PMReturn),
                    });

            var srmConfig = new TestSRMConfig(baseName + ".SRM")
            {
                ECSParts = this,
                NumLPSlots = numLPSlotsAndWafers,
                NumLPWafers = numLPSlotsAndWafers,
                AutoLocNameToITPRDictionary = SRMLocNameToITPRDictionary,
                DisableR1ArmB = disableR1ArmB,
            };
            SRM = new TestSRM(srmConfig).RunGoOnlineActionInline();

            if (otherPartsAutoGoOnline)
                new IActivePartBase[] { PM1, PM2, PM3, PM4, SRM, PMReject, PMAbort, PMReturn }.DoForEach(part => part.RunGoOnlineAction());

            AddExplicitDisposeAction(() =>
                {
                    new IActivePartBase[] { SRM, PMReject, PMAbort, PMReturn, PM4, PM3, PM2, PM1, E039TableUpdater, ANManagerPart }.DoForEach(part => part.DisposeOfGivenObject());
                });

            stationNameToEnumDictionary = new ReadOnlyIDictionary<string, TestStationEnum>(
                KVP.Create(SRM.AL1LocID.Name, TestStationEnum.AL1),
                KVP.Create(PM1.LocID.Name, TestStationEnum.PM1),
                KVP.Create(PM2.LocID.Name, TestStationEnum.PM2),
                KVP.Create(PM3.InputLocID.Name, TestStationEnum.PM3Input),
                KVP.Create(PM3.OutputLocID.Name, TestStationEnum.PM3Output),
                KVP.Create(PM4.InputLocID.Name, TestStationEnum.PM4),
                KVP.Create(PMReject.LocID.Name, TestStationEnum.PMReject),
                KVP.Create(PMAbort.LocID.Name, TestStationEnum.PMAbort),
                KVP.Create(PMReturn.LocID.Name, TestStationEnum.PMReturn)
            );

            StationToITPRDictionary = new ReadOnlyIDictionary<TestStationEnum, ITransferPermissionRequest>(SRMLocNameToITPRDictionary.Select(kvpIn => KVP.Create(stationNameToEnumDictionary.SafeTryGetValue(kvpIn.Key), kvpIn.Value)).Where(kvp => kvp.Key != TestStationEnum.None));

            StationToIPreparednessStateFactoryDictionary = new ReadOnlyIDictionary<TestStationEnum, Func<IPreparednessState>>(
                    new KeyValuePair<TestStationEnum, IPrepare<IProcessSpec, IProcessStepSpec>>[]
                    {
                        KVP.Create(TestStationEnum.PM1, PM1 as IPrepare<IProcessSpec, IProcessStepSpec>),
                        KVP.Create(TestStationEnum.PM2, PM2 as IPrepare<IProcessSpec, IProcessStepSpec>),
                        KVP.Create(TestStationEnum.PM3Input, PM3 as IPrepare<IProcessSpec, IProcessStepSpec>),
                        KVP.Create(TestStationEnum.PM3Output, PM3 as IPrepare<IProcessSpec, IProcessStepSpec>),
                        KVP.Create(TestStationEnum.PM4, PM4 as IPrepare<IProcessSpec, IProcessStepSpec>),
                        KVP.Create(TestStationEnum.PMReject, PMReject as IPrepare<IProcessSpec, IProcessStepSpec>),
                        KVP.Create(TestStationEnum.PMAbort, PMAbort as IPrepare<IProcessSpec, IProcessStepSpec>),
                        KVP.Create(TestStationEnum.PMReturn, PMReturn as IPrepare<IProcessSpec, IProcessStepSpec>),
                    }
                    .Where(kvp => kvp.Value != null)
                    .Select(kvp => KVP.Create<TestStationEnum, Func<IPreparednessState>>(kvp.Key, () => kvp.Value.StatePublisher.Object))
                   );
        }

        public E041.IANManagerPart ANManagerPart { get; private set; }

        public IE039TableUpdater E039TableUpdater { get; private set; }
        public IE039TableObserver E039TableObserver { get { return E039TableUpdater; } }

        public ISimpleExampleProcessModuleEngine PM1 { get; private set; }
        public ISimpleExampleProcessModuleEngine PM2 { get; private set; }
        public IBeltExampleProcessModuleEngine PM3 { get; private set; }
        public IBeltExampleProcessModuleEngine PM4 { get; private set; }

        public ISimpleExampleProcessModuleEngine PMReject;
        public ISimpleExampleProcessModuleEngine PMAbort;
        public ISimpleExampleProcessModuleEngine PMReturn;

        public TestSRM SRM { get; private set; }
        public ITestSchedulerEngine Scheduler { get; set; }

        public ReadOnlyIDictionary<string, ITransferPermissionRequest> SRMLocNameToITPRDictionary { get; private set; }
        public ReadOnlyIDictionary<TestStationEnum, ITransferPermissionRequest> StationToITPRDictionary { get; private set; }
        public ReadOnlyIDictionary<TestStationEnum, Func<IPreparednessState>> StationToIPreparednessStateFactoryDictionary { get; private set; }

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
                case TestStationEnum.PMReject: return PMReject;
                case TestStationEnum.PMAbort: return PMAbort;
                case TestStationEnum.PMReturn: return PMReturn;
                default: return null;
            }
        }

        ReadOnlyIDictionary<string, TestStationEnum> stationNameToEnumDictionary = ReadOnlyIDictionary<string, TestStationEnum>.Empty;
    }

    /// <summary>
    /// None, AL1, PM1, PM2, PM3Input, PM3Output, PM4, PMReject, PMAbort, PMReturn
    /// </summary>
    public enum TestStationEnum
    {
        None = 0,
        AL1,
        PM1,
        PM2,
        PM3Input,
        PM3Output,
        PM4,
        /// <summary>This is a special case location - in this location the process simply, and the PendingSPS is set to Rejected.</summary>
        PMReject,
        /// <summary>This is a special case location - in this location the process fails, the SJS is set to Abort, and the PendingSPS is set to Rejected.</summary>
        PMAbort,
        /// <summary>This is a special case location - in this location the process succeeds, and the SJS gets set to Return</summary>
        PMReturn,
    }

    #endregion

    #region TestSchedulerEngine (ITestSchedulerEngine, TestSubstrateAndProcessTracker, TestSubstrateSchedulerTool)

    public interface ITestSchedulerEngine : IActivePartBase
    {
        IClientFacet SetSubstrateProcessSpecs(string jobID, ProcessSpecBase<ProcessStepSpecBase> processSpec, SubstrateJobRequestState initialSJRM, params E039ObjectID[] substIDsArray);
        IClientFacet SetSJRS(SubstrateJobRequestState sjrs, params E039ObjectID[] substIDsArray);
        IClientFacet VerifyIdle();
        IClientFacet Sync();
    }

    public class TestSchedulerEngine : SimpleActivePartBase, ITestSchedulerEngine, ISubstrateSchedulerPart
    {
        public TestSchedulerEngine(string partID = "Sched", TestECSParts ecsParts = null, TestSubstrateSchedulerTool substrateSchedulerTool = null, bool verifyIdleOnDispose = true)
            : base (partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(waitTimeLimit: (0.02).FromSeconds()))
        {
            EcsParts = ecsParts;
            TestSubstrateSchedulerTool = substrateSchedulerTool;
            TestSubstrateSchedulerTool.HostingPartNotifier = this;

            stateIVA = Values.Instance.GetValueAccessor<ISubstrateSchedulerPartState>("{0}.State".CheckedFormat(PartID));
            ServiceAndPublishStateIfNeeded(force: true);

            if (verifyIdleOnDispose)
            {
                AddExplicitDisposeAction(() =>
                    {
                        string ec = PerformVerifyIdle(forDispose: true);
                        if (ec.IsNeitherNullNorEmpty())
                        {
                            new System.InvalidOperationException("VerifyIdle failed: {0}".CheckedFormat(ec)).Throw();
                        }
                    });
            }
        }

        public TestECSParts EcsParts { get; private set;}
        TestSubstrateSchedulerTool TestSubstrateSchedulerTool { get; set; }

        public IClientFacet SetSubstrateProcessSpecs(string jobID, ProcessSpecBase<ProcessStepSpecBase> processSpec, SubstrateJobRequestState initialSJRM, params E039ObjectID[] substIdArray)
        {
            return new BasicActionImpl(actionQ, ipf => PerformSetSubstrateProcessSpecs(ipf, jobID, processSpec, initialSJRM, substIdArray), CurrentMethodName, ActionLoggingReference);
        }

        public IClientFacet SetSJRS(SubstrateJobRequestState sjrs, params E039ObjectID[] substIdArray)
        {
            return new BasicActionImpl(actionQ, ipf => PerformSetSJRS(ipf, sjrs, substIdArray), CurrentMethodName, ActionLoggingReference);
        }

        public IClientFacet VerifyIdle()
        {
            return new BasicActionImpl(ActionQueue, ipf => PerformVerifyIdle(forDispose: false), CurrentMethodName, ActionLoggingReference);
        }

        public IClientFacet Sync()
        {
            return new BasicActionImpl(ActionQueue, ipf =>
                {
                    PerformMainLoopService();
                    return "";
                }, CurrentMethodName, ActionLoggingReference);
        }

        /// <summary>
        /// Action factory method:  When the resuling action is run it will attempt to change the part's BehaviorEnableFlags to be the given value.
        /// The scheduler will generally confirm that this request is permitted by the tool before making the change.  
        /// If the <paramref name="force"/> flag is true then the part will make the change in state even if the tool indicates that it should not be permitteed.
        /// The <paramref name="force"/> flag is expected to be used in cases where the user has been asked if they really want to make this change and they
        /// explicitly confirm that they do.
        /// </summary>
        public IClientFacet SetSelectedBehavior(BehaviorEnableFlags flags, bool force = false)
        {
            return new BasicActionImpl(actionQ, ipf => PerformSetSelectedBehavior(ipf, flags, force), CurrentMethodName, ActionLoggingReference, "{0}{1}".CheckedFormat(flags, force ? ", force" : ""));
        }

        protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
        {
            ServiceAndPublishStateIfNeeded();

            var denyReasonList = TestSubstrateSchedulerTool.VerifyStateChange(state, new NamedValueSet() { andInitialize ? "SetAutomatic" : "GoOnline" } );

            if (!denyReasonList.IsNullOrEmpty())
                return "Cannot {0}: {1}".CheckedFormat(ipf.ToString(ToStringSelect.MesgAndDetail), String.Join(", ", denyReasonList));

            RequestsAndStatusOutFromTool requestAndStatusFromTool = default(RequestsAndStatusOutFromTool);

            string ec = TestSubstrateSchedulerTool.PerformGoOnlineActionEx(ipf, andInitialize, npv, () => HasStopBeenRequested, ref requestAndStatusFromTool);

            string toolFaultReason = requestAndStatusFromTool.ToolFaultReason.MapNullToEmpty();
            if (BaseState.ExplicitFaultReason != toolFaultReason)
            {
                SetExplicitFaultReason(toolFaultReason);
                ec = ec.MapNullOrEmptyTo(toolFaultReason);
            }

            if (!requestAndStatusFromTool.RequestNVSFromTool.IsNullOrEmpty())
                InnerHandleRequestFromTool(requestAndStatusFromTool.RequestNVSFromTool, publishIfNeeded: false);

            if (ec.IsNullOrEmpty() && BaseState.UseState != UseState.AttemptOnline)
                ec = BaseState.Reason;

            if (ec.IsNullOrEmpty() && npv.Contains("SetSelectedBehavior"))
            {
                var flags = npv["SetSelectedBehavior"].VC.GetValue<BehaviorEnableFlags>(rethrow: true);
                var force = npv.Contains("Force");

                ec = PerformSetSelectedBehavior(ipf, flags, force, publish: false);
            }

            ServiceAndPublishStateIfNeeded(force: true);

            return ec;
        }

        protected override string PerformGoOfflineAction(IProviderActionBase ipf)
        {
            state.BehaviorEnableFlags = BehaviorEnableFlags.None;

            ServiceAndPublishStateIfNeeded();

            var denyReasonList = TestSubstrateSchedulerTool.VerifyStateChange(state, new NamedValueSet() { "GoOffline" });

            if (!denyReasonList.IsNullOrEmpty())
                Log.Warning.Emit("Attempt to {0} gave warnings: {1}", ipf.ToString(ToStringSelect.MesgAndDetail), String.Join(", ", denyReasonList));

            string ec = TestSubstrateSchedulerTool.PerformGoOfflineAction(ipf, () => HasStopBeenRequested);

            return ec;
        }



        private string PerformSetSubstrateProcessSpecs(IProviderFacet ipf, string jobID, ProcessSpecBase<ProcessStepSpecBase> processSpec, SubstrateJobRequestState initialSJRM, E039ObjectID[] substIdArray)
        {
            switch (initialSJRM)
            {
                default:
                case SubstrateJobRequestState.None:
                    break;
                case SubstrateJobRequestState.Run:
                case SubstrateJobRequestState.Stop:
                case SubstrateJobRequestState.Pause:
                    if (state.BehaviorEnableFlags.IsClear(BehaviorEnableFlags.Automatic))
                        Log.Warning.Emit("{0}: Current behavior enable flags do not permit automatic operation now [{1}]", ipf.ToString(ToStringSelect.MesgAndDetail), state);
                    break;
                case SubstrateJobRequestState.Abort:
                case SubstrateJobRequestState.Return:
                    if (state.BehaviorEnableFlags.IsClear(BehaviorEnableFlags.Recovery))
                        Log.Warning.Emit("{0}: Current behavior enable flags do not permit automatic recovery operations now [{1}]", ipf.ToString(ToStringSelect.MesgAndDetail), state);
                    break;
            }

            foreach (var substIDiter in substIdArray)
            {
                var substID = substIDiter ?? E039ObjectID.Empty;

                TestSubstrateAndProcessTracker st = trackerDictionary.SafeTryGetValue(substID.FullName);

                if (st != null)
                    return "{0} has already been assigned a process spec".CheckedFormat(substID.FullName);

                st = new TestSubstrateAndProcessTracker();
                st.Setup(EcsParts, Log, substID, jobID, processSpec, TestSubstrateSchedulerTool.AllLocObserverDictionary);

                trackerDictionary[substID.FullName] = st;
                TestSubstrateSchedulerTool.Add(st);

                if (initialSJRM != SubstrateJobRequestState.None)
                {
                    st.SubstrateJobRequestState = initialSJRM;
                    Log.Debug.Emit("{0} Initial SJRM set to {1}", substID.FullName, initialSJRM);
                }
            }

            BusyReason = ipf.ToString(ToStringSelect.MesgAndDetail);

            return "";
        }

        private string PerformSetSJRS(IProviderFacet ipf, SubstrateJobRequestState sjrs, E039ObjectID[] substIdArray)
        {
            switch (sjrs)
            {
                default:
                case SubstrateJobRequestState.None:
                    break;
                case SubstrateJobRequestState.Run:
                case SubstrateJobRequestState.Stop:
                case SubstrateJobRequestState.Pause:
                    if (state.BehaviorEnableFlags.IsClear(BehaviorEnableFlags.Automatic))
                        Log.Warning.Emit("{0}: Current behavior enable flags do not permit automatic operation now [{1}]", ipf.ToString(ToStringSelect.MesgAndDetail), state);
                    break;
                case SubstrateJobRequestState.Abort:
                case SubstrateJobRequestState.Return:
                    if (state.BehaviorEnableFlags.IsClear(BehaviorEnableFlags.Recovery))
                        Log.Warning.Emit("{0}: Current behavior enable flags do not permit automatic recovery operations now [{1}]", ipf.ToString(ToStringSelect.MesgAndDetail), state);
                    break;
            }

            foreach (var substIDiter in substIdArray)
            {
                var substID = substIDiter ?? E039ObjectID.Empty;

                var st = trackerDictionary.SafeTryGetValue(substID.FullName);

                if (st == null)
                    return "{0} was not found".CheckedFormat(substID.FullName);

                var entrySJRS = st.SubstrateJobRequestState;
                if (entrySJRS != sjrs)
                {
                    st.SubstrateJobRequestState = sjrs;
                    Log.Debug.Emit("{0} SJRM set to {1} [from {2}]", substID.FullName, sjrs, entrySJRS);
                }
                else
                {
                    Log.Debug.Emit("{0} SJRM was already {1}", substID.FullName, sjrs);
                }
            }

            BusyReason = ipf.ToString(ToStringSelect.MesgAndDetail);

            return "";
        }

        private string PerformVerifyIdle(bool forDispose = false)
        {
            string ec = "";

            if (!forDispose && !BaseState.IsBusy)
                ec = "BaseState is not idle [{0}]".CheckedFormat(BaseState);
            else if (forDispose && BaseState.UseState != UseState.Stopped)
                ec = "BaseState is not Stopped on dispose [{0}]".CheckedFormat(BaseState);

            if (ec.IsNullOrEmpty())
                ec = TestSubstrateSchedulerTool.VerifyIdle();

            return ec;
        }

        protected string PerformSetSelectedBehavior(IProviderFacet ipf, BehaviorEnableFlags flags, bool force, bool publish = true)
        {
            var denyReasonList = TestSubstrateSchedulerTool.VerifyStateChange(state, new NamedValueSet() { { "SetSelectedBehavior", flags } });

            if (denyReasonList.IsNullOrEmpty())
            {
            }
            else if (force)
            {
                Log.Warning.Emit("{0}: operation being forced with tool reported warning(s): ", ipf.ToString(ToStringSelect.MesgAndDetail), String.Join(", ", denyReasonList));
            }
            else
            {
                return "Cannot {0}: {1}".CheckedFormat(ipf.ToString(ToStringSelect.MesgAndDetail), String.Join(", ", denyReasonList));
            }

            state.BehaviorEnableFlags = flags;

            if (publish)
                ServiceAndPublishStateIfNeeded();

            return "";
        }

        IDictionaryWithCachedArrays<string, TestSubstrateAndProcessTracker> trackerDictionary = new IDictionaryWithCachedArrays<string, TestSubstrateAndProcessTracker>();

        SubstrateSchedulerPartState state = new SubstrateSchedulerPartState()
        {
            PartStatusFlags = SubstrateSchedulerPartStatusFlags.None,
            BehaviorEnableFlags = BehaviorEnableFlags.ServiceActions | BehaviorEnableFlags.RecoveryMaterialMovement,
        };

        ISubstrateSchedulerPartState lastPublishedState = SubstrateSchedulerPartStateForPublication.Empty;

        public INotificationObject<ISubstrateSchedulerPartState> StatePublisher { get { return statePublisher; } }
        private InterlockedNotificationRefObject<ISubstrateSchedulerPartState> statePublisher = new InterlockedNotificationRefObject<ISubstrateSchedulerPartState>(SubstrateSchedulerPartStateForPublication.Empty);

        IValueAccessor<ISubstrateSchedulerPartState> stateIVA;

        private void ServiceAndPublishStateIfNeeded(bool force = false)
        {
            var capturedBaseState = BaseState;
            bool baseStateChanged = !Object.ReferenceEquals(capturedBaseState, state.PartBaseState);

            if (force || baseStateChanged || !state.Equals(lastPublishedState))
            {
                state.PartBaseState = capturedBaseState;

                Log.Debug.Emit("Publishing {0}", state);

                statePublisher.Object = lastPublishedState = state.MakeCopyOfThis();

                stateIVA.Set(new SubstrateSchedulerPartStateForPublication(lastPublishedState));
            }
        }

        protected override void PerformMainLoopService()
        {
            bool isUpdateNeeded = trackerDictionary.ValueArray.Any(tracker => tracker.IsUpdateNeeded);
            if (isUpdateNeeded)
            {
                QpcTimeStamp qpcTimeStamp = QpcTimeStamp.Now;

                trackerDictionary.ValueArray.DoForEach(tracker => tracker.UpdateIfNeeded(qpcTimeStamp: qpcTimeStamp));
            }

            substrateStateTally.Clear();
            trackerDictionary.ValueArray.DoForEach(tracker => substrateStateTally.Add(tracker));

            int serviceCount = 0;
            IBaseState baseState = BaseState;

            try
            {
                if (!Object.ReferenceEquals(BaseState, state.PartBaseState))
                    ServiceAndPublishStateIfNeeded(force: true);

                RequestsAndStatusOutFromTool requestAndStatusFromTool = default(RequestsAndStatusOutFromTool);

                serviceCount += TestSubstrateSchedulerTool.Service(state, ref requestAndStatusFromTool);

                string toolFaultReason = requestAndStatusFromTool.ToolFaultReason.MapNullToEmpty();
                if (BaseState.ExplicitFaultReason != toolFaultReason)
                    SetExplicitFaultReason(toolFaultReason);

                bool toolIsWaitingForActionToBeSelected = ((requestAndStatusFromTool.ToolStatusFlags & SubstrateSchedulerPartStatusFlags.WaitingForActionToBeSelected) != 0);
                if (toolIsWaitingForActionToBeSelected != ((state.PartStatusFlags & SubstrateSchedulerPartStatusFlags.WaitingForActionToBeSelected) != 0))
                {
                    state.PartStatusFlags.Set(SubstrateSchedulerPartStatusFlags.WaitingForActionToBeSelected, toolIsWaitingForActionToBeSelected);
                    ServiceAndPublishStateIfNeeded();
                }

                if (!requestAndStatusFromTool.RequestNVSFromTool.IsNullOrEmpty())
                {
                    InnerHandleRequestFromTool(requestAndStatusFromTool.RequestNVSFromTool);
                }
            }
            catch (System.Exception ex)
            {
                SetBaseState(UseState.FailedToOffline, "{0} caught unexpected exception: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)));

                ServiceAndPublishStateIfNeeded();
                return;
            }

            if (trackerDictionary.ValueArray.Any(tracker => tracker.IsDropRequested))
            {
                var dropTrackerSet = trackerDictionary.ValueArray.Where(tracker => tracker.IsDropRequested).ToArray();

                foreach (var dropTracker in dropTrackerSet)
                {
                    trackerDictionary.Remove(dropTracker.SubstID.FullName);
                    TestSubstrateSchedulerTool.Drop(dropTracker);
                }
            }

            if (serviceCount > 0)
                InFastSpinPeriod = true;

            string nextBusyReason = "";

            if (substrateStateTally.total > 0)
            {
                if (substrateStateTally.sjsWaitingForStart > 0)
                    nextBusyReason = "Substrates are waiting for start";
                else if (substrateStateTally.sjsRunning > 0)
                    nextBusyReason = "Substrates are in process";
                else if (substrateStateTally.sjsStopping + substrateStateTally.sjsAborting + substrateStateTally.sjsPausing + substrateStateTally.sjsReturning > 0)
                    nextBusyReason = "Substrates are stopping, aborting, pausing and/or returning";
            }

            if (nextBusyReason.IsNullOrEmpty() && InFastSpinPeriod)
                nextBusyReason = BusyReason.MapNullOrEmptyTo("Internal work triggered being busy");

            BusyReason = nextBusyReason;

            if (backgroundSpinLogTimer.IsTriggered)
            {
                var substLocMapStr = string.Join(", ", trackerDictionary.ValueArray.Select(tracker => "{0}@{1}".CheckedFormat(tracker.SubstID.Name, tracker.Info.LocID)));
                Log.Debug.Emit("Background spin log: serviceCount:{0} {1} busyReason:'{2}' {3} map:{4}", serviceCount, substrateStateTally, BusyReason, baseState, substLocMapStr);
            }
        }

        SubstrateStateTally substrateStateTally = new SubstrateStateTally();

        private void InnerHandleRequestFromTool(INamedValueSet requestNVSFromTool, bool publishIfNeeded = true)
        {
            string requestNVSFromToolSML = requestNVSFromTool.SafeToStringSML();

            var requestNV = requestNVSFromTool.FirstOrDefault().MapNullToEmpty();
            switch (requestNV.Name)
            {
                case "SetService": 
                    if (BaseState.UseState == UseState.OnlineFailure) 
                        SetBaseState(UseState.OnlineUninitialized, "By tool request: {0}".CheckedFormat(requestNVSFromToolSML)); 
                    state.BehaviorEnableFlags = BehaviorEnableFlags.Service; 
                    break;

                case "SetRecovery":
                    state.BehaviorEnableFlags &= ~BehaviorEnableFlags.Automatic; 
                    state.BehaviorEnableFlags |= BehaviorEnableFlags.Service | BehaviorEnableFlags.RecoveryMaterialMovement; 
                    break;

                case "SetAutomatic": 
                    state.BehaviorEnableFlags &= ~BehaviorEnableFlags.Service; 
                    state.BehaviorEnableFlags |= BehaviorEnableFlags.Automatic | BehaviorEnableFlags.RecoveryMaterialMovement; 
                    break;

                case "SetUseState": 
                    SetBaseState(requestNV.VC.GetValue<UseState>(rethrow: false, defaultValue: UseState.FailedToOffline), "By tool request: {0}".CheckedFormat(requestNVSFromToolSML)); 
                    break;

                default: 
                    SetBaseState(UseState.FailedToOffline, "Unrecognized tool request: {0}".CheckedFormat(requestNVSFromToolSML)); 
                    break;
            }

            if (publishIfNeeded)
                ServiceAndPublishStateIfNeeded();
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

    public class TestSubstrateAndProcessTracker : SubstrateAndProcessTrackerBase<ProcessSpecBase<ProcessStepSpecBase>, ProcessStepSpecBase>, IJobTrackerLinkage
    {
        public void Setup(TestECSParts ecsParts, Logging.IBasicLogger logger, E039ObjectID substID, string jobID, ProcessSpecBase<ProcessStepSpecBase> processSpec, IDictionary<string, E090SubstLocObserver> allLocObserverDictionary)
        {
            ECSParts = ecsParts;
            AllLocObserverDictionary = allLocObserverDictionary;

            base.Setup(ECSParts.E039TableUpdater, substID, logger, processSpec);

            srcLocNameList = new ReadOnlyIList<string>(Info.LinkToSrc.ToID.Name);
            destLocNameList = new ReadOnlyIList<string>(Info.LinkToDest.ToID.Name);
            robotArmsLocNameList = new ReadOnlyIList<string>(ECSParts.SRM.R1ArmALocID.Name, ECSParts.SRM.R1ArmBLocID.Name);

            FinalizeSPSAtEndOfLastStep = true;

            JobID = jobID;
            JobTrackerLinkage = this;
        }

        public override void Setup(IE039TableUpdater e039TableUpdater, E039ObjectID substID, Logging.IBasicLogger logger, ProcessSpecBase<ProcessStepSpecBase> processSpec)
        {
            new System.InvalidOperationException("This method can no longer be used directly - use the local variant instead").Throw();
        }

        public TestECSParts ECSParts { get; set; }
        public IDictionary<string, E090SubstLocObserver> AllLocObserverDictionary { get; set; }
        public IClientFacet CurrentStationProcessICF { get; set; }
        public bool FinalizeSPSAtEndOfLastStep { get; set; }

        private ReadOnlyIList<string> srcLocNameList, destLocNameList, robotArmsLocNameList;

        public Func<IClientFacet> GeneratePendingRunProcessActionAndCorrespondingDelegate(string nextLocName, INotifyable hostingPartNotifier, string autoReleaseKey = null, string autoAcquireKey = null)
        {
            var station = ECSParts.GetStationEnum(nextLocName);
            var part = ECSParts.GetPart(station);

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
                    pendingICF = ECSParts.PM3.RunProcess(SubstID, stepSpec, autoReleaseTransferPermissionLocNameAtStart: autoReleaseKey, autoAcquireTransferPermissionLocNameAtEnd: autoAcquireKey);  
                    break;
                case TestStationEnum.PM4:
                    pendingICF = ECSParts.PM4.RunProcess(SubstID, stepSpec, autoReleaseTransferPermissionLocNameAtStart: autoReleaseKey, autoAcquireTransferPermissionLocNameAtEnd: autoAcquireKey);
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
                    icf = ecsParts.PM1.RunProcess(SubstID, stepSpec);
                    break;
                case TestStationEnum.PM2:
                    icf = ecsParts.PM2.RunProcess(SubstID, stepSpec);
                    break;
                case TestStationEnum.PMReject:
                    icf = ecsParts.PMReject.RunProcess(SubstID, stepSpec).SetNamedParamValues(new NamedValueSet() { { "PendingSPS", SubstProcState.Rejected }, { "ResultCode", "Failed-Rejected" } });
                    break;
                case TestStationEnum.PMAbort:
                    icf = ecsParts.PMAbort.RunProcess(SubstID, stepSpec).SetNamedParamValues(new NamedValueSet() { { "SetSJRS", SubstrateJobRequestState.Abort }, { "PendingSPS", SubstProcState.Rejected }, { "ResultCode", "Failed-Aborted" } });
                    break;
                case TestStationEnum.PMReturn:
                    icf = ecsParts.PMReturn.RunProcess(SubstID, stepSpec).SetNamedParamValues(new NamedValueSet() { { "SetSJRS", SubstrateJobRequestState.Return } });
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

        public int Service(ISubstrateSchedulerPartState stateToTool
            , QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp)
            , bool forceUpdate = false
            , bool serviceSubstObserver = true
            , ServiceBasicSJSStateChangeTriggerFlags sjsStateChangeTriggerFlags = ServiceBasicSJSStateChangeTriggerFlags.All & ~ServiceBasicSJSStateChangeTriggerFlags.EnableAutoStart
            , bool serviceEvaluationUpdates = true
            , bool serviceDropReasonUpdates = true
            , bool serviceProcessCompletion = true
            , bool alreadyFoundFirstWaitingForStartSubstrate = true)
        {
            int didCount = 0;

            bool substUpdateNeeded = (IsServiceNeeded && serviceSubstObserver) || forceUpdate;

            if (substUpdateNeeded)
            {
                didCount += UpdateIfNeeded(forceUpdate: forceUpdate, qpcTimeStamp: qpcTimeStamp, noteStartingService: true).MapToInt();

                if (sjsStateChangeTriggerFlags != default(ServiceBasicSJSStateChangeTriggerFlags))
                {
                    didCount += ServiceBasicSJSStateChangeTriggers(sjsStateChangeTriggerFlags);

                    // if ServiceBasicSJSStateChangeTriggers changed anything then we may need to re-update the tracker's state.
                    if (IsUpdateNeeded)
                        didCount += UpdateIfNeeded(forceUpdate: forceUpdate, qpcTimeStamp: qpcTimeStamp, noteStartingService: false).MapToInt();
                }

                if (serviceEvaluationUpdates)
                {
                    UpdateEvalInfo(alreadyFoundFirstWaitingForStartSubstrate: alreadyFoundFirstWaitingForStartSubstrate, stateToTool: stateToTool);

                    if (Info.SJS == SubstrateJobState.Held && !evalInfo.flags.blockedByInboundHoldRequest)
                    {
                        switch (SubstrateJobRequestState)
                        {
                            case SubstrateJobRequestState.None: break;
                            case SubstrateJobRequestState.Run: SetSubstrateJobState((Info.STS == SubstState.AtSource) ? SubstrateJobState.WaitingForStart : SubstrateJobState.Running, "Inbound Hold Released"); break;
                            case SubstrateJobRequestState.Stop: SetSubstrateJobState(SubstrateJobState.Stopping, "Inbound Hold Released"); break;
                            case SubstrateJobRequestState.Pause: SetSubstrateJobState(SubstrateJobState.Pausing, "Inbound Hold Released"); break;
                            case SubstrateJobRequestState.Abort: SetSubstrateJobState(SubstrateJobState.Aborting, "Inbound Hold Released"); break;
                            case SubstrateJobRequestState.Return: break;
                            default: break;
                        }
                    }
                    else if (evalInfo.flags.blockedByInboundHoldRequest)
                    {
                        switch (SubstrateJobRequestState)
                        {
                            case SubstrateJobRequestState.None: break;
                            case SubstrateJobRequestState.Run:
                            case SubstrateJobRequestState.Pause:
                            case SubstrateJobRequestState.Stop:
                            case SubstrateJobRequestState.Abort:
                                switch (Info.SJS)
                                {
                                    case SubstrateJobState.WaitingForStart:
                                    case SubstrateJobState.Running:
                                    case SubstrateJobState.Stopping:
                                    case SubstrateJobState.Pausing:
                                    case SubstrateJobState.Aborting:
                                        SetSubstrateJobState(SubstrateJobState.Held, "Inbound Hold Requested through Prepare Interface at next process location");
                                        break;
                                    case SubstrateJobState.Returning:
                                    case SubstrateJobState.Stranded:
                                    default: break;
                                }
                                break;
                            case SubstrateJobRequestState.Return: break;
                            default: break;
                        }
                    }
                }
            }

            bool isFinalOrNull = SubstObserver.Object.IsFinalOrNull();

            if (serviceDropReasonUpdates && (substUpdateNeeded || isFinalOrNull))
                didCount += ServiceDropReasonAssertion();

            if (serviceProcessCompletion)
            {
                IClientFacet icf = CurrentStationProcessICF;
                IActionState actionState = (icf != null ? icf.ActionState : null);

                string currentLoc = Info.LocID;

                var currentStation = ECSParts.GetStationEnum(currentLoc);
                bool currentLocIsPM4 = (currentStation == TestStationEnum.PM4);

                if (icf != null && actionState.IsComplete)
                {
                    IClientFacetWithResult<IProcessStepResult> icfWithResult = icf as IClientFacetWithResult<IProcessStepResult>;
                    IProcessStepResult stepResult = (icfWithResult != null ? icfWithResult.ResultValue: null);

                    if (stepResult == null)
                        stepResult = new ProcessStepResultBase(actionState.ResultCode);
                    
                    if (stepResult != null && stepResult.SPS == SubstProcState.Processed && stepResult.ResultCode.IsNullOrEmpty() && actionState.Failed)
                        Logger.Debug.Emit("Note: {0} gave non-failure stepResult sps:{1}", icf.ToString(ToStringSelect.MesgDetailAndState), stepResult.SPS);

                    Add(new ProcessStepTrackerResultItem<ProcessStepSpecBase>() { LocName = Info.LocID, StepSpec = NextStepSpec, StepResult = stepResult }, autoLatchFinalSPS: FinalizeSPSAtEndOfLastStep && !currentLocIsPM4);

                    CurrentStationProcessICF = null;

                    didCount += 1;
                }
                else if (icf == null && FinalizeSPSAtEndOfLastStep && NextStepSpec == null && !Info.SPS.IsProcessingComplete() && !currentLocIsPM4)
                {
                    // this is typically used to record the final SPS for substrates that finished their last processing step at PM4, after they have been moved onto a robot arm.
                    var finalSPS = GetFinalSPS();
                    if (finalSPS.IsProcessingComplete() && !isFinalOrNull)
                    {
                        Logger.Debug.Emit("Setting {0} final SPS to {1} [at loc:{2}]", SubstID.FullName, finalSPS, currentLoc);
 
                        E039TableUpdater.SetSubstProcState(Info, finalSPS);

                        didCount += 1;
                    }
                }
            }

            return didCount;
        }

        private ReadOnlyIList<string> GetNextLocNameList(ISubstrateSchedulerPartState stateToTool)
        {
            if (CurrentStationProcessICF != null || Info.STS.IsAtDestination() || SubstObserver.Object.IsFinalOrNull() || IsDropRequested)
                return ReadOnlyIList<string>.Empty;

            IProcessStepSpec stepSpec = NextStepSpec;
            var inferredSPS = Info.InferredSPS;

            bool isProcessMaterialMovementEnabled = stateToTool.IsProcessMaterialMovementEnabled();
            bool isRecoveryMaterialMovementEnabled = stateToTool.IsRecoveryMaterialMovementEnabled();

            switch (Info.SJS)
            {
                case E090.SubstrateJobState.WaitingForStart:
                    if (stepSpec != null && isProcessMaterialMovementEnabled)
                    {
                        // tell the caller where the substrate needs to be next in order to be able to start processing - this allows the caller to delay the running transition until the next process location is ready/empty if desired.
                        return stepSpec.UsableLocNameList;
                    }
                    else if (stateToTool.IsRecoveryMaterialMovementEnabled())
                    {
                        return Info.SPS.IsNeedsProcessing() ? srcLocNameList : destLocNameList;
                    }
                    else
                    {
                        return ReadOnlyIList<string>.Empty;
                    }

                case E090.SubstrateJobState.Running:
                case E090.SubstrateJobState.Stopping:
                case E090.SubstrateJobState.Pausing:
                case E090.SubstrateJobState.Held:       // Held is handled like Running for purposes of determining the next loc name list contents
                    if (stepSpec != null)
                    {
                        if (isProcessMaterialMovementEnabled)
                            return stepSpec.UsableLocNameList;
                        else if (isRecoveryMaterialMovementEnabled)
                            return Info.SPS.IsNeedsProcessing() ? srcLocNameList : destLocNameList;
                        else
                            return ReadOnlyIList<string>.Empty;
                    }
                    else if (Info.STS.IsAtDestination())
                    {
                        return ReadOnlyIList<string>.Empty;
                    }
                    else if (inferredSPS.IsProcessingComplete(includeLost: false, includeSkipped: false))
                    {
                        if (isProcessMaterialMovementEnabled || isRecoveryMaterialMovementEnabled)
                            return destLocNameList;
                        else
                            return ReadOnlyIList<string>.Empty;
                    }
                    else if (Info.STS.IsAtSource() && inferredSPS.IsNeedsProcessing())
                    {
                        Logger.Debug.Emit("{0} marking '{1}' Processed [AtSource, NeedsProcessing, Recipe has no process steps]", Fcns.CurrentMethodName, Info.ObjID.Name);
                        E039TableUpdater.SetSubstProcState(SubstObserver, SubstProcState.Processed);
                        return ReadOnlyIList<string>.Empty;
                    }
                    else if (inferredSPS.IsProcessStepComplete() && isProcessMaterialMovementEnabled)
                    {
                        bool atPM4 = Info.LocID == ECSParts.PM4.OutputLocID.Name;

                        return atPM4 ? robotArmsLocNameList : destLocNameList;
                    }
                    else if (isRecoveryMaterialMovementEnabled && (Info.SJS == SubstrateJobState.Stopping || Info.SJS == SubstrateJobState.Pausing) && Info.STS.IsAtWork())
                    {
                        return Info.SPS.IsNeedsProcessing() ? srcLocNameList : destLocNameList;
                    }
                    else
                    {
                        // For cases that this code does not directly recognize.  Just leave the wafer in its current location (aka strand it)
                        return ReadOnlyIList<string>.Empty;
                    }

                case E090.SubstrateJobState.Returning:
                case E090.SubstrateJobState.Aborting:
                    if (Info.STS.IsAtWork() && isRecoveryMaterialMovementEnabled)
                        return Info.SPS.IsNeedsProcessing() ? srcLocNameList : destLocNameList;
                    else
                        return ReadOnlyIList<string>.Empty;

                default:
                    // in the future this code may need to split out and handle the Returning, Pausing, Stopping and Aborting cases.  for now all such cases strand the wafer in its current location.
                    return ReadOnlyIList<string>.Empty;
            }
        }

        public void UpdateEvalInfo(bool alreadyFoundFirstWaitingForStartSubstrate, ISubstrateSchedulerPartState stateToTool, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            var entryNextLocNameList = evalInfo.nextLocNameList.MapNullToEmpty();
            var entryNextLocNameListVCStr = evalInfo.nextLocNameListVCStr;

            if (SubstObserver.Object.IsFinalOrNull())
            {
                evalInfo.Clear(andNextLocNameListVCStr: true);
                return;
            }

            var st = this;

            evalInfo.Clear();

            var isProcessMaterialMovementEnabled = stateToTool.IsProcessMaterialMovementEnabled();
            var isRecoveryMaterialMovementEnabled = stateToTool.IsRecoveryMaterialMovementEnabled();

            bool isUsable = true;

            switch (st.Info.SJS)
            {
                case SubstrateJobState.WaitingForStart:
                    if (alreadyFoundFirstWaitingForStartSubstrate || !isProcessMaterialMovementEnabled)
                        isUsable = false;
                    break;
                case SubstrateJobState.Running:
                case SubstrateJobState.Stopping:
                case SubstrateJobState.Pausing:
                case SubstrateJobState.Held:
                    if (!isProcessMaterialMovementEnabled && !isRecoveryMaterialMovementEnabled)
                        isUsable = false;
                    break;
                case SubstrateJobState.Aborting:
                case SubstrateJobState.Returning:
                    if (!isRecoveryMaterialMovementEnabled)
                        isUsable = false;
                    break;

                default:
                    isUsable = false;
                    break;
            }

            var currentLocName = st.Info.LocID;

            if (isUsable)
            {
                var currentStation = evalInfo.currentStation = ECSParts.GetStationEnum(currentLocName);
                var currentLocObs = evalInfo.currentLocObs = AllLocObserverDictionary.SafeTryGetValue(currentLocName);
                if (currentLocObs == null || !currentLocObs.Info.NotAccessibleReason.IsNullOrEmpty())
                    isUsable = false;   // the substrate's current location is not currently accessible
            }

            if (isUsable)
            {
                var currentITPR = evalInfo.currentITPR = ECSParts.StationToITPRDictionary.SafeTryGetValue(evalInfo.currentStation);
                var currentITPS = ((currentITPR != null) ? currentITPR.TransferPermissionStatePublisher.Object : null);
                if (currentITPS != null && !currentITPS.IsAvailableOrAlmostAvailable(maxEstimatedAvailableAfterPeriodIn: null))
                    isUsable = false;   // the ITPR for the current substrate's location does not indicate that it is available.
            }

            var nextLocNameList = st.GetNextLocNameList(stateToTool);

            if (!entryNextLocNameList.Equals(nextLocNameList.MapNullToEmpty()))
            {
                evalInfo.nextLocNameList = nextLocNameList;
                evalInfo.nextLocNameListVCStr = ValueContainer.CreateFromObject(nextLocNameList).ToStringSML();

                if (evalInfo.lastNonEmptyNextLocNameListVCStr != evalInfo.nextLocNameListVCStr && !evalInfo.nextLocNameList.IsNullOrEmpty())
                {
                    evalInfo.lastNonEmptyNextLocNameListVCStr = evalInfo.nextLocNameListVCStr;
                    Logger.Debug.Emit("{0}: At loc '{1}' NextLocNamedList changed to {2}", st.SubstID.FullName, st.Info.LocID, evalInfo.nextLocNameListVCStr);
                }
            }

            if (!isUsable)
                return;

            evalInfo.nextStationEvalList.AddRange((nextLocNameList ?? ReadOnlyIList<string>.Empty).Select(locName => new StationEvaluationItem(AllLocObserverDictionary.SafeTryGetValue(locName), ECSParts)));

            EvaluationInfo.Flags flags = default(EvaluationInfo.Flags);

            flags.hasBeenStarted = (st.Info.SJS != SubstrateJobState.WaitingForStart);
            bool isSJSRunningOrStoppingOrPausing = (st.Info.SJS == SubstrateJobState.Running || st.Info.SJS == SubstrateJobState.Stopping || st.Info.SJS == SubstrateJobState.Pausing);

            flags.wantsToBeHere = (nextLocNameList ?? ReadOnlyIList<string>.Empty).Contains(st.Info.LocID);
            flags.wantsToMove = !nextLocNameList.IsNullOrEmpty() && !flags.wantsToBeHere;
            flags.isInProcessOrPendingProcess = (st.CurrentStationProcessICF != null);
            flags.canStartProcessStepHereNow = (flags.wantsToBeHere && isSJSRunningOrStoppingOrPausing && !flags.isInProcessOrPendingProcess);

            if (flags.wantsToMove && !flags.isInProcessOrPendingProcess)
            {
                evalInfo.firstCanMoveToStationEval = evalInfo.nextStationEvalList.FirstOrDefault(stationEval => stationEval.isAvailableMoveTarget);
                evalInfo.firstCanSwapAtStationEval = evalInfo.nextStationEvalList.FirstOrDefault(stationEval => stationEval.isAvailableSwapTarget);

                flags.canBeMovedNow = !flags.isInProcessOrPendingProcess && evalInfo.firstCanMoveToStationEval.isAvailableMoveTarget;
                flags.canBeSwappedNow = !flags.isInProcessOrPendingProcess && evalInfo.firstCanSwapAtStationEval.isAvailableSwapTarget;
            }

            if (flags.wantsToMove && !flags.canBeMovedNow && !flags.canBeSwappedNow && !flags.isInProcessOrPendingProcess)
            {
                evalInfo.firstPossibleAlmostAvailableForApproachToStationEval = evalInfo.nextStationEvalList.Where(stationEval => stationEval.isPossibleAlmostAvailableToApproachTarget).OrderBy(stationEval => stationEval.GetITPS().GetEstimatedTimeToAvailable(qpcTimeStamp)).FirstOrDefault();

                flags.havePossibleAlmostAvailableForApproach = evalInfo.firstPossibleAlmostAvailableForApproachToStationEval.IsValid;
            }

            flags.isOnRobotArm = (currentLocName == ECSParts.SRM.R1ArmALocID.Name || currentLocName == ECSParts.SRM.R1ArmBLocID.Name);

            flags.nextStationEvalListIncludesPipelinedPMInput = evalInfo.nextStationEvalList.Any(stationEval => stationEval.IsPipelinedPMInputLocation);
            flags.nextStationEvalListIncludesPipelinedPMOutput = evalInfo.nextStationEvalList.Any(stationEval => stationEval.IsPipelinedPMOutputLocation);

            flags.blockedByInboundHoldRequest = flags.wantsToMove && evalInfo.nextStationEvalList.All(stationEval => stationEval.locationIsRequestingHoldInboundMaterial);

            evalInfo.flags = flags;
        }

        /// <summary>This flag is true when the tool has referenced the substrate in an SRM action and that action has not completed.</summary>
        public bool IsInSRMAction { get; set; }

        public override bool IsMotionPending { get { return IsInSRMAction; } }

        public EvaluationInfo evalInfo = new EvaluationInfo();

        public class EvaluationInfo
        {
            public TestStationEnum currentStation;
            public E090SubstLocObserver currentLocObs;
            public ITransferPermissionRequest currentITPR;
            public ReadOnlyIList<string> nextLocNameList;
            public string nextLocNameListVCStr;
            public string lastNonEmptyNextLocNameListVCStr;
            public List<StationEvaluationItem> nextStationEvalList = new List<StationEvaluationItem>();

            public Flags flags;

            public struct Flags
            {
                public bool hasBeenStarted;
                public bool wantsToBeHere, wantsToMove;
                public bool isInProcessOrPendingProcess, canStartProcessStepHereNow;
                public bool canBeMovedNow, canBeSwappedNow;
                public bool havePossibleAlmostAvailableForApproach;
                public bool isOnRobotArm;
                public bool nextStationEvalListIncludesPipelinedPMInput;
                public bool nextStationEvalListIncludesPipelinedPMOutput;
                public bool blockedByInboundHoldRequest;

                public bool IsAnyFlagSet
                {
                    get { return (hasBeenStarted 
                                 || wantsToBeHere || wantsToMove 
                                 || isInProcessOrPendingProcess || canStartProcessStepHereNow 
                                 || canBeMovedNow || canBeSwappedNow 
                                 || havePossibleAlmostAvailableForApproach 
                                 || isOnRobotArm
                                 || nextStationEvalListIncludesPipelinedPMInput
                                 || nextStationEvalListIncludesPipelinedPMOutput
                                 || blockedByInboundHoldRequest
                                 ); }
                }
            }

            public StationEvaluationItem firstCanMoveToStationEval, firstCanSwapAtStationEval, firstPossibleAlmostAvailableForApproachToStationEval;

            public void Clear(bool andNextLocNameListVCStr = true)
            {
                currentStation = TestStationEnum.None;
                currentLocObs = null;
                currentITPR = null;
                nextLocNameList = ReadOnlyIList<string>.Empty;
                if (andNextLocNameListVCStr)
                    nextLocNameListVCStr = string.Empty;
                nextStationEvalList.Clear();
                flags = default(Flags);
                firstCanMoveToStationEval = firstCanSwapAtStationEval = firstPossibleAlmostAvailableForApproachToStationEval = default(StationEvaluationItem);
            }
        }

        public struct StationEvaluationItem
        {
            public StationEvaluationItem(E090SubstLocObserver locObs, TestECSParts ecsParts)
            {
                var locObsWithTracker = locObs as SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>;

                this.locObs = locObs;
                locInfo = (locObs != null ? locObs.Info : default(E090SubstLocInfo));
                station = ecsParts.GetStationEnum(locInfo.ObjID.Name);
                itpr = ecsParts.StationToITPRDictionary.SafeTryGetValue(station);
                var ipsF = ecsParts.StationToIPreparednessStateFactoryDictionary.SafeTryGetValue(station);
                ips = (ipsF != null ? ipsF() : null);
                var itps = (itpr != null ? itpr.TransferPermissionStatePublisher.Object : null);
                var locHasNoITPROrIsAvailable = (itpr == null || itps != null && itps.SummaryStateCode == TransferPermissionSummaryStateCode.Available);

                stAtLoc = (locObsWithTracker != null) ? locObsWithTracker.Tracker : null;
                stAtLocWantsToMove = (stAtLoc != null && stAtLoc.evalInfo.flags.wantsToMove);
                stAtLocIsBeingProcessed = (stAtLoc != null && stAtLoc.CurrentStationProcessICF != null);

                locationIsCurrentlyAccessible = (locObs != null) && locInfo.NotAccessibleReason.IsNullOrEmpty();
                locationIsNotCurrentlyAccessible = !locationIsCurrentlyAccessible;

                locationIsRequestingHoldInboundMaterial = ((ips != null) && ips.SummaryState.IsSet(QuerySummaryState.HoldInboundMaterial));

                isAvailableMoveTarget = locationIsCurrentlyAccessible && !locationIsRequestingHoldInboundMaterial && locInfo.IsUnoccupied && locHasNoITPROrIsAvailable;
                isAvailableSwapTarget = locationIsCurrentlyAccessible && !locationIsRequestingHoldInboundMaterial && (stAtLoc != null) && !stAtLocIsBeingProcessed && stAtLocWantsToMove && locHasNoITPROrIsAvailable;

                isPossibleAlmostAvailableToApproachTarget = locationIsCurrentlyAccessible && !locationIsRequestingHoldInboundMaterial && !isAvailableMoveTarget && !isAvailableSwapTarget && itps != null && itps.SummaryStateCode == TransferPermissionSummaryStateCode.AlmostAvailable;
            }

            public E090SubstLocObserver locObs;
            public E090SubstLocInfo locInfo;
            public TestSubstrateAndProcessTracker stAtLoc;
            public TestStationEnum station;
            public ITransferPermissionRequest itpr;
            public IPreparednessState ips;

            public bool IsValid { get { return locObs != null && locObs.Info.ObjID.Name.IsNeitherNullNorEmpty(); } }
            public bool IsPipelinedPMInputLocation { get { return station == TestStationEnum.PM4 || station == TestStationEnum.PM3Input; } }
            public bool IsPipelinedPMOutputLocation { get { return station == TestStationEnum.PM4 || station == TestStationEnum.PM3Output; } }

            public bool stAtLocIsBeingProcessed;
            public bool stAtLocWantsToMove;
            public bool locationIsRequestingHoldInboundMaterial;
            public bool locationIsCurrentlyAccessible;
            public bool locationIsNotCurrentlyAccessible;

            public bool isAvailableMoveTarget;
            public bool isAvailableSwapTarget;
            public bool isPossibleAlmostAvailableToApproachTarget;

            public ITransferPermissionState GetITPS(ITransferPermissionState fallbackValue = null)
            {
                return (itpr != null ? itpr.TransferPermissionStatePublisher.Object : fallbackValue);
            }

            public bool GetIsAlmostAvailableToApproachTarget(TimeSpan maxEstimatedAlmostAvailablePeriod, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
            {
                if (!isPossibleAlmostAvailableToApproachTarget)
                    return false;

                var itps = itpr.TransferPermissionStatePublisher.Object;
                if (itps != null && itps.SummaryStateCode == TransferPermissionSummaryStateCode.AlmostAvailable)
                    return itps.IsAvailableOrAlmostAvailable(maxEstimatedAlmostAvailablePeriod, qpcTimeStamp);

                return false;
            }
        }

        #region IJobTrackerLinkage

        private string JobID { get; set; }

        string IJobTrackerLinkage.ID { get {return JobID;}}
        bool IJobTrackerLinkage.SubstrateTrackerHasBeenUpdated { get; set; }
        bool IJobTrackerLinkage.IsDropRequested { get { return false ; } }
        string IJobTrackerLinkage.DropRequestReason { get { return string.Empty; } }

        #endregion
    }

    public class TestSubstrateSchedulerToolConfig : ICopyable<TestSubstrateSchedulerToolConfig>
    {
        public TestSubstrateSchedulerToolConfig(string name, TestECSParts ecsParts)
        {
            Name = name;
            ECSParts = ecsParts;
            MaxEstimatedAlmostAvailablePeriod = (1.0).FromSeconds();
            EnableAutoResumeFromStranded = true;
            ServiceBasicSJSStateChangeTriggerFlags = SubstrateScheduling.ServiceBasicSJSStateChangeTriggerFlags.All & ~(ServiceBasicSJSStateChangeTriggerFlags.EnableAutoStart);
        }

        public TestSubstrateSchedulerToolConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (TestSubstrateSchedulerToolConfig)this.MemberwiseClone();
        }

        public string Name { get; private set; }
        public TestECSParts ECSParts { get; private set; }

        public TimeSpan MaxEstimatedAlmostAvailablePeriod { get; set; }

        public bool EnableAutoResumeFromStranded { get; set; }

        public ServiceBasicSJSStateChangeTriggerFlags ServiceBasicSJSStateChangeTriggerFlags { get; set; }
    }

    public class TestSubstrateSchedulerTool: ISubstrateSchedulerTool<TestSubstrateAndProcessTracker, ProcessSpecBase<ProcessStepSpecBase>, ProcessStepSpecBase>
    {
        public TestSubstrateSchedulerTool(string name, TestECSParts ecsParts) 
            : this(new TestSubstrateSchedulerToolConfig(name, ecsParts)) 
        { }

        public TestSubstrateSchedulerTool(string name, TestECSParts ecsParts, TimeSpan maxEstimatedAlmostAvailablePeriod)
            : this(new TestSubstrateSchedulerToolConfig(name, ecsParts) { MaxEstimatedAlmostAvailablePeriod = maxEstimatedAlmostAvailablePeriod })
        { }

        public TestSubstrateSchedulerTool(TestSubstrateSchedulerToolConfig toolConfig)
        {
            Name = toolConfig.Name;
            ToolConfig = toolConfig.MakeCopyOfThis();

            ECSParts = ToolConfig.ECSParts;
            Logger = new Logging.Logger(Name);

            RouteSequenceFailedAnnunciator = ECSParts.ANManagerPart.RegisterANSource(Name, new E041.ANSpec() { ANName = "{0}.RouteSequenceFailed".CheckedFormat(Name), ANType = E041.ANType.Error, ALID = E041.ANAlarmID.OptLookup });
            PrepareFailedAnnunciator = ECSParts.ANManagerPart.RegisterANSource(Name, new E041.ANSpec() { ANName = "{0}.PrepareFailed".CheckedFormat(Name), ANType = E041.ANType.Error, ALID = E041.ANAlarmID.OptLookup });

            r1ArmALocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.SRM.R1ArmALocID, substTrackerDictionary);
            r1ArmBLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.SRM.R1ArmBLocID, substTrackerDictionary);
            r1LocObserverArray = new[] { r1ArmALocObserver, r1ArmBLocObserver };

            al1LocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.SRM.AL1LocID, substTrackerDictionary);
            pm1LocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.PM1.LocID, substTrackerDictionary);
            pm2LocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.PM2.LocID, substTrackerDictionary);
            pm3InputLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.PM3.InputLocID, substTrackerDictionary);
            pm3OutputLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.PM3.OutputLocID, substTrackerDictionary);
            pm4TransferLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.PM4.InputLocID, substTrackerDictionary);
            pmRejectLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.PMReject.LocID, substTrackerDictionary);
            pmAbortLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.PMAbort.LocID, substTrackerDictionary);
            pmReturnLocObserver = new SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>(ECSParts.PMReturn.LocID, substTrackerDictionary);

            foreach (var processLocObserver in al1LocObserver.ConcatItems(pm1LocObserver, pm2LocObserver, pm3InputLocObserver, pm3OutputLocObserver, pm4TransferLocObserver, pmRejectLocObserver, pmAbortLocObserver, pmReturnLocObserver))
                processLocObserverWithTrackerDictionary[processLocObserver.ID.Name] = processLocObserver;

            foreach (var locObserver in r1LocObserverArray.Concat(processLocObserverWithTrackerDictionary.ValueArray))
            {
                allLocObserverWithTrackerDictionary[locObserver.ID.Name] = locObserver;
                allLocObserverDictionary[locObserver.ID.Name] = locObserver;
            }

            foreach (var extraLocID in ECSParts.SRM.LPSlotLocIDArray)
            {
                allLocObserverDictionary[extraLocID.Name] = new E090SubstLocObserver(extraLocID.GetPublisher());
            }

            var locNameToPrepareKVPSet = new KeyValuePair<string, IPrepare<IProcessSpec, IProcessStepSpec>>[]
                {
                    KVP.Create(ECSParts.PM1.LocID.Name, ECSParts.PM1 as IPrepare<IProcessSpec, IProcessStepSpec>),
                    KVP.Create(ECSParts.PM2.LocID.Name, ECSParts.PM2 as IPrepare<IProcessSpec, IProcessStepSpec>),
                    KVP.Create(ECSParts.PM3.InputLocID.Name, ECSParts.PM3 as IPrepare<IProcessSpec, IProcessStepSpec>),
                    KVP.Create(ECSParts.PM4.InputLocID.Name, ECSParts.PM4 as IPrepare<IProcessSpec, IProcessStepSpec>),
                    KVP.Create(ECSParts.PMAbort.LocID.Name, ECSParts.PMAbort as IPrepare<IProcessSpec, IProcessStepSpec>),
                    KVP.Create(ECSParts.PMReject.LocID.Name, ECSParts.PMReject as IPrepare<IProcessSpec, IProcessStepSpec>),
                    KVP.Create(ECSParts.PMReturn.LocID.Name, ECSParts.PMReturn as IPrepare<IProcessSpec, IProcessStepSpec>),
                }
                .Where(kvp => kvp.Value != null)
                ;
            locNameToPrepareDictionary = new Dictionary<string, IPrepare<IProcessSpec, IProcessStepSpec>>().SafeAddRange(locNameToPrepareKVPSet, onlyTakeFirst: false);

            normalMoveFromLocSet = new HashSet<string>(r1LocObserverArray.Concat(processLocObserverWithTrackerDictionary.ValueArray).Select(obs => obs.ID.Name));
        }

        public string Name { get; private set; }
        TestSubstrateSchedulerToolConfig ToolConfig { get; set; }
        TestECSParts ECSParts { get; set; }
        Logging.ILogger Logger { get; set; }

        E041.IANSource RouteSequenceFailedAnnunciator { get; set; }
        E041.IANSource PrepareFailedAnnunciator { get; set; }

        /// <summary>Gives the INotifyable target that this tool is to attach to any Action it creates to notify the hosting part when the action completes</summary>
        public INotifyable HostingPartNotifier { get; set; }

        Dictionary<string, TestSubstrateAndProcessTracker> substTrackerDictionary = new Dictionary<string, TestSubstrateAndProcessTracker>();
        IListWithCachedArray<TestSubstrateAndProcessTracker> substTrackerList = new IListWithCachedArray<TestSubstrateAndProcessTracker>();
        IListWithCachedArray<TestSubstrateAndProcessTracker> filteredSubstTrackerList = new IListWithCachedArray<TestSubstrateAndProcessTracker>();

        SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker> r1ArmALocObserver, r1ArmBLocObserver;
        SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>[] r1LocObserverArray;
        IClientFacet srmAction;
        IListWithCachedArray<TestSubstrateAndProcessTracker> srmActionSubstTrackerList = new IListWithCachedArray<TestSubstrateAndProcessTracker>();

        SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker> al1LocObserver, pm1LocObserver, pm2LocObserver, pm3InputLocObserver, pm3OutputLocObserver, pm4TransferLocObserver;
        SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker> pmRejectLocObserver, pmAbortLocObserver, pmReturnLocObserver;

        IDictionaryWithCachedArrays<string, SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>> processLocObserverWithTrackerDictionary = new IDictionaryWithCachedArrays<string, SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>>();
        IDictionaryWithCachedArrays<string, SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>> allLocObserverWithTrackerDictionary = new IDictionaryWithCachedArrays<string, SubstLocObserverWithTrackerLookup<TestSubstrateAndProcessTracker>>();
        IDictionaryWithCachedArrays<string, E090SubstLocObserver> allLocObserverDictionary = new IDictionaryWithCachedArrays<string, E090SubstLocObserver>();

        public IDictionary<string, E090SubstLocObserver> AllLocObserverDictionary { get { return allLocObserverDictionary; } }

        private IDictionary<string, IPrepare<IProcessSpec, IProcessStepSpec>> locNameToPrepareDictionary;

        HashSet<string> normalMoveFromLocSet;

        IListWithCachedArray<TestSubstrateAndProcessTracker> trackersInProcessList = new IListWithCachedArray<TestSubstrateAndProcessTracker>();

        public void Add(TestSubstrateAndProcessTracker substTracker)
        {
            substTrackerDictionary.Add(substTracker.SubstID.FullName, substTracker);
            substTrackerList.Add(substTracker);
        }

        public void Drop(TestSubstrateAndProcessTracker substTracker)
        {
            substTrackerDictionary.Remove(substTracker.SubstID.FullName);
            substTrackerList.Remove(substTracker);
        }

        /// <summary>
        /// Allows the hosting part to inform the tool when a GoOnline Action is being performed.  
        /// This method is called after the hosting part has completed its own operations and they have all been completed successfully.
        /// </summary>
        public string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv, Func<bool> hasStopBeenRequestedDelegate, ref RequestsAndStatusOutFromTool requestAndStatusOutFromTool)
        {
            if (RouteSequenceFailedAnnunciator.ANState.IsSignaling)
                RouteSequenceFailedAnnunciator.Clear(ipf.ToString(ToStringSelect.MesgAndDetail));

            if (PrepareFailedAnnunciator.ANState.IsSignaling)
                PrepareFailedAnnunciator.Clear(ipf.ToString(ToStringSelect.MesgAndDetail));

            return string.Empty;
        }

        /// <summary>
        /// Allows the hosting part to inform the tool when a GoOffline Action is being performed.
        /// This method is called after the hosting part has completed its own operations and they have all been completed successfully.
        /// </summary>
        public string PerformGoOfflineAction(IProviderFacet ipf, Func<bool> hasStopBeenRequestedDelegate)
        {
            return string.Empty;
        }

        public string VerifyIdle()
        {
            if (RouteSequenceFailedAnnunciator.ANState.IsSignaling)
                return "RouteSeqeunceFailedAnnunciator is signaling";

            if (PrepareFailedAnnunciator.ANState.IsSignaling)
                return "PrepareFailedAnnunciator is signaling";

            if (substrateStateTally.stsAtWork > 0)
                return "There are still substrates at work in the tool [{0}]".CheckedFormat(substrateStateTally);

            var occupiedSLOs = r1LocObserverArray.Concat(processLocObserverWithTrackerDictionary.ValueArray).Where(st => !st.UpdateInline().IsUnoccupied).ToArray();

            if (occupiedSLOs.Length > 0)
                return "One or more locations are not unoccupied: {0}".CheckedFormat(string.Join(",", occupiedSLOs.Select(slo => slo.ID.Name)));

            if (srmActionSubstTrackerList.Count > 0)
                return "There are still substrates in the SRM Action Subst Tracker list";

            return "";
        }

        /// <summary>
        /// This method returns a list of reasons why the scheduler cannot be transitioned from the current given <paramref name="stateToTool"/> using the given <paramref name="requestNVS"/> request specification.  (See comments for Service above and here below for details on supported request patterns)
        /// This method will only be called if the request is not coming from the tool itself.  If the request is coming from the tool then the hosting part assumes that the tool has already determined that the change would be permitted.
        /// </summary>
        /// <remarks>
        /// In addition to the supported patterns from the Service method above, the following are generally expected to be supported keywords and/or named value pairs:
        ///     SetAutomatic - keyword (no value) - used to request that the hosting part can transition to a fully automatic state.  The hosting part typically indicates this to verify that it can GoOnline(true).
        ///     GoOnline - keyword (no value) - used to request that the hosting part can transition to an online state.  The hosting part typically indicates this to verify that it can GoOnline(false).
        ///     GoOffline - keyword (no value) - advisory - used to indicate that the hosting part is going to transition to the offline state by request.
        /// </remarks>
        public IList<string> VerifyStateChange(ISubstrateSchedulerPartStateToTool stateToTool, INamedValueSet requestNVS)
        {
            ServiceTrackersAndUpdateObservers(stateToTool, QpcTimeStamp.Now);

            List<string> notReadyReasonList = new List<string>();

            if (requestNVS.Contains("SetAutomatic"))
            {
                foreach (var locObs in allLocObserverWithTrackerDictionary.ValueArray.Where(locObs => locObs.IsOccupied))
                {
                    if (locObs.Tracker == null)
                    {
                        notReadyReasonList.Add("Found stranded substrate '{0}' [NoAssociatedSubstrateAndProcessTracker]".CheckedFormat(locObs.ContainsSubstInfo));
                    }
                    else if (locObs.ContainsSubstInfo.SPS.IsNeedsProcessing())
                    {
                        var srcLocInfo = new E090SubstLocInfo(locObs.ContainsSubstInfo.LinkToSrc.ToID.GetObject());

                        if (srcLocInfo.IsOccupied)
                            notReadyReasonList.Add("Found stranded substrate '{0}' [SrcLocIsOccupied]".CheckedFormat(locObs.ContainsSubstInfo));
                        else if (srcLocInfo.NotAccessibleReason.IsNeitherNullNorEmpty())
                            notReadyReasonList.Add("Found stranded substrate '{0}' [SrcLocIsNotAccessible:{1}]".CheckedFormat(locObs.ContainsSubstInfo, srcLocInfo.NotAccessibleReason));
                    }
                    else
                    {
                        var destLocInfo = new E090SubstLocInfo(locObs.ContainsSubstInfo.LinkToDest.ToID.GetObject());

                        if (destLocInfo.IsOccupied)
                            notReadyReasonList.Add("Found stranded substrate '{0}' [DestLocIsOccupied]".CheckedFormat(locObs.ContainsSubstInfo));
                        else if (destLocInfo.NotAccessibleReason.IsNeitherNullNorEmpty())
                            notReadyReasonList.Add("Found stranded substrate '{0}' [DestLocIsNotAccessible:{1}]".CheckedFormat(locObs.ContainsSubstInfo, destLocInfo.NotAccessibleReason));
                    }
                }
            }

            return notReadyReasonList.MapEmptyToNull();
        }

        QpcTimer noteStrandedSubstratesTriggerHoldoffTimer = new QpcTimer() { TriggerIntervalInSec = 10.0, AutoReset = false };

        List<SubstrateRoutingItemBase> srmActionItemBuilderList = new List<SubstrateRoutingItemBase>();
        List<TestSubstrateAndProcessTracker> srmActionItemBuilderSubstTrackerList = new List<TestSubstrateAndProcessTracker>();

        SubstrateStateTally substrateStateTally = new SubstrateStateTally();

        HashSet<string> candidateSwapAtStationLocNameSet = new HashSet<string>();

        /// <summary>
        /// Called periodically to allow the tool to perform work.  Nominally this method is where the tool is given a repeated oportunity to create and start SRM and process actions.
        /// This method returns a count of the number of change it made or observed to allow the caller to optimze its spin loop sleep behavior to increase responsiveness in relation to loosely coupled state transitions.
        /// <param name="stateToTool">Gives the tool access to the state information that the hosting part provides to the tool in order to tell the tool what behavior is permitted and behavior is not.</param>
        /// <param name="requestAndStatusOutFromTool">Allows the tool to pass a set of requests, and status information, back to the hosting part so that the hosting part can react and/or publish state accordingly.  Please see remarks below  and under VerityStateChange for details on standard request patterns.</param>
        /// </summary>
        public int Service(ISubstrateSchedulerPartStateToTool stateToTool, ref RequestsAndStatusOutFromTool requestAndStatusOutFromTool)
        {
            if (RouteSequenceFailedAnnunciator.ANState.IsSignaling)
            {
                return ServiceRouteSequenceFailedAnnunciator(stateToTool, ref requestAndStatusOutFromTool);
            }

            bool isSubstrateLaunchEnabled = stateToTool.IsSubstrateLaunchEnabled();
            bool isProcessMaterialMovementEnabled = stateToTool.IsProcessMaterialMovementEnabled();
            bool isProcessStepExecutionEnabled = stateToTool.IsProcessStepExecutionEnabled();
            bool isRecoveryMaterialMovementEnabled = stateToTool.IsRecoveryMaterialMovementEnabled();

            int deltaCount = 0;

            QpcTimeStamp qpcTimeStamp = QpcTimeStamp.Now;
            deltaCount += ServiceTrackersAndUpdateObservers(stateToTool, qpcTimeStamp);

            if (!isProcessStepExecutionEnabled)
            {
                noteStrandedSubstratesTriggerHoldoffTimer.StartIfNeeded();

                if (!isRecoveryMaterialMovementEnabled || noteStrandedSubstratesTriggerHoldoffTimer.IsTriggered)
                {
                    foreach (var st in substTrackerList.Array)
                    {
                        var sjs = st.Info.SJS;
                        if (sjs == SubstrateJobState.Running || sjs == SubstrateJobState.WaitingForStart)
                            st.SetSubstrateJobState(SubstrateJobState.Stranded, "Part is no longer permitted to run process [{0}]".CheckedFormat(stateToTool));
                    }
                }
            }
            else
            {
                noteStrandedSubstratesTriggerHoldoffTimer.StopIfNeeded();

                if (substrateStateTally.sjsStranded > 0 && ToolConfig.EnableAutoResumeFromStranded)
                {
                    foreach (var st in substTrackerList.Array.Where(st => st.Info.SJS == SubstrateJobState.Stranded))
                    {
                        if (st.Info.STS.IsAtSource())
                            st.SetSubstrateJobState(SubstrateJobState.WaitingForStart, "Auto resuming held substrate [AtSource]");
                        else if (st.Info.STS.IsAtWork())
                            st.SetSubstrateJobState(SubstrateJobState.Running, "Auto resuming held substrate [AtWork:{0}]".CheckedFormat(st.Info.LocID));
                    }
                }
            }

            foreach (var st in trackersInProcessList.Array)
            {
                if (st.CurrentStationProcessICF == null && st.Info.LocID != ECSParts.PM4.OutputLocID.Name)
                {
                    trackersInProcessList.Remove(st);       // this is safe when iterating on the IListWithCachedArray.Array.
                    deltaCount++;
                }
            }

            if (srmAction != null)
            {
                var srmActionState = srmAction.ActionState;

                if (srmActionState.IsComplete)
                {
                    srmActionSubstTrackerList.SafeTakeAll().DoForEach(st => st.IsInSRMAction = false);

                    if (srmActionState.Succeeded)
                    {
                        srmAction = null;

                        deltaCount += ServiceTrackersAndUpdateObservers(stateToTool, qpcTimeStamp);
                    }
                    else
                    {
                        string reason = "Route action '{0}' failed: {1}".CheckedFormat(srmAction.ToString(ToStringSelect.MesgAndDetail), srmActionState.ResultCode);

                        PostRouteSequenceFailedError(stateToTool, reason);

                        UpdateRequestAndStatusOutFromTool(ref requestAndStatusOutFromTool);

                        deltaCount += 1;

                        srmAction = null;

                        deltaCount += ServiceTrackersAndUpdateObservers(stateToTool, qpcTimeStamp);

                        return deltaCount;
                    }
                }
            }

            // update the filteredSubstTrackerList to contain the st instances that have at least one evaluation info flag set.  This should generaly be the set of wafers that are in the tool and one of the ones that is waiting for start.

            if (filteredSubstTrackerList.Count > 0)
                filteredSubstTrackerList.Clear();

            if (substTrackerList.Count > 0)
                filteredSubstTrackerList.AddRange(substTrackerList.Array.Where(st => st.evalInfo.flags.IsAnyFlagSet));

            // when a substrate is not in an srmAction and it has been started and it can have its next process step started here now (aka it wants to be here) then start it.
            if (isProcessStepExecutionEnabled && filteredSubstTrackerList.Count > 0)
            {
                foreach (var st in filteredSubstTrackerList.Array.Where(st => !st.IsInSRMAction && st.evalInfo.flags.hasBeenStarted && st.evalInfo.flags.canStartProcessStepHereNow))
                {
                    st.StartProcessAtCurrentLocation(ECSParts, HostingPartNotifier);

                    if (st.CurrentStationProcessICF != null)
                        trackersInProcessList.Add(st);

                    deltaCount++;
                }
            }

            if (srmAction == null && filteredSubstTrackerList.Count > 0)
            {
                srmActionItemBuilderList.Clear();
                srmActionItemBuilderSubstTrackerList.Clear();

                int numEmptyUsableArms = r1ArmALocObserver.IsUnoccupiedAndAccessible.MapToInt() + r1ArmBLocObserver.IsUnoccupiedAndAccessible.MapToInt();
                int numOccupiedUsableArms = r1ArmALocObserver.IsOccupiedAndAccessible.MapToInt() + r1ArmBLocObserver.IsOccupiedAndAccessible.MapToInt();

                bool anyCanBeMovedNow = filteredSubstTrackerList.Array.Any(st => st.evalInfo.flags.canBeMovedNow);
                bool anyCanBeSwappedNow = filteredSubstTrackerList.Array.Any(st => st.evalInfo.flags.canBeSwappedNow);

                // if there are no substrates that are ready to move now
                // and there are 2 unoccupied arms 
                // and there is at least one substrate that can be swapped now or that is almost available to be moved then 
                // move that substrate onto one of the two arms.

                if (srmActionItemBuilderList.Count == 0 && numEmptyUsableArms >= 2 && !anyCanBeMovedNow)
                {
                    var stToMoveToArm = filteredSubstTrackerList.Array.FirstOrDefault(st => st.evalInfo.flags.hasBeenStarted && (st.evalInfo.flags.canBeSwappedNow || (!st.evalInfo.flags.isOnRobotArm && st.evalInfo.flags.havePossibleAlmostAvailableForApproach && st.evalInfo.firstPossibleAlmostAvailableForApproachToStationEval.GetIsAlmostAvailableToApproachTarget(ToolConfig.MaxEstimatedAlmostAvailablePeriod, qpcTimeStamp))));
                    if (stToMoveToArm != null)
                    {
                        var useArmLocName = r1ArmALocObserver.IsUnoccupiedAndAccessible ? r1ArmALocObserver.ID.Name : r1ArmBLocObserver.ID.Name;     // future: replace this with wear leveled selection version similar to the one that the SRM uses.

                        srmActionItemBuilderList.Add(new ApproachLocationItem(useArmLocName, stToMoveToArm.Info.LocID, waitUntilDone: false) { Comment = "ApproachGetSubstApproachAlmostAvailable" });
                        srmActionItemBuilderList.Add(new MoveSubstrateItem(stToMoveToArm.SubstID, ECSParts.SRM.R1EitherArmName));

                        if (stToMoveToArm.evalInfo.flags.canBeSwappedNow)
                            srmActionItemBuilderList.Add(new ApproachLocationItem(stToMoveToArm.SubstID, stToMoveToArm.evalInfo.firstCanSwapAtStationEval.locInfo.ObjID.Name, waitUntilDone: true, mustSucceed: true));
                        else if (stToMoveToArm.evalInfo.flags.havePossibleAlmostAvailableForApproach)
                            srmActionItemBuilderList.Add(new ApproachLocationItem(stToMoveToArm.SubstID, stToMoveToArm.evalInfo.firstPossibleAlmostAvailableForApproachToStationEval.locInfo.ObjID.Name, waitUntilDone: true, mustSucceed: true));

                        srmActionItemBuilderSubstTrackerList.Add(stToMoveToArm);
                    }
                }

                // if there is a substrate on the one arm and the other arm is empty and the substrate on the arm can be swapped then swap the wafer with the one at the target location.
                if (srmActionItemBuilderList.Count == 0 && numEmptyUsableArms > 0 && numOccupiedUsableArms > 0)
                {
                    var stOnArm = filteredSubstTrackerList.Array.FirstOrDefault(st => st.evalInfo.flags.hasBeenStarted && st.evalInfo.flags.isOnRobotArm && st.evalInfo.flags.canBeSwappedNow);

                    if (stOnArm != null)
                    {
                        candidateSwapAtStationLocNameSet.Clear();

                        filteredSubstTrackerList.Array.Where(st => st.evalInfo.flags.hasBeenStarted && !st.evalInfo.flags.isOnRobotArm && st.evalInfo.flags.wantsToMove && st.evalInfo.currentLocObs != null).DoForEach(st => candidateSwapAtStationLocNameSet.SafeAddIfNeeded(st.evalInfo.currentLocObs.ID.Name));

                        var swapAtStationEvalItem = stOnArm.evalInfo.nextStationEvalList.Where(stationEval => stationEval.isAvailableSwapTarget && candidateSwapAtStationLocNameSet.Contains(stationEval.locInfo.ObjID.Name)).FirstOrDefault();

                        // change this to AttemptToSwapSubstrateToNextLocation
                        if (swapAtStationEvalItem.IsValid)
                            AttemptToMoveOrSwapSubstrateToNextLocation(isProcessStepExecutionEnabled, stOnArm, swapAtStationEvalItem, numEmptyUsableArms, comment: "SwapOutboundSubstAtLocWithInboundOneOnArm");
                    }
                }

                // if either substrate on the robot arm is available to be moved to its next location them move it (in some cases this creates and starts a pending process after the move is complete)
                if (srmActionItemBuilderList.Count == 0 && numOccupiedUsableArms >= 1 && anyCanBeMovedNow)
                {
                    foreach (var stOnArm in filteredSubstTrackerList.Array.Where(st => st.evalInfo.flags.hasBeenStarted && st.evalInfo.flags.isOnRobotArm && st.evalInfo.flags.canBeMovedNow))
                    {
                        foreach (var tryAvailableMoveTargetStationEval in stOnArm.evalInfo.nextStationEvalList.Where(stationEval => stationEval.isAvailableMoveTarget))
                        {
                            if (AttemptToMoveSubstrateToNextLocation(isProcessStepExecutionEnabled, stOnArm, tryAvailableMoveTargetStationEval, numEmptyUsableArms, comment: "PutSubstFromRobotAtNextLoc"))
                                break;
                        }

                        if (srmActionItemBuilderList.Count > 0)
                            break;
                    }
                }

                // if there is a substrate that is not on the robot arm and it can move there and none of the next location names for the substrates that are already on an arm match any of the next location names for this one then move it to the first available arm
                // Note: do not move a substrate onto the last available arm if that substrate needs to go to a location that is the output location for a pipelined PM.  This is because the pipelined PM output location can become occupied outside of the control of this logic.
                if (srmActionItemBuilderList.Count == 0 && numEmptyUsableArms > 0)
                {
                    UpdateHashSetOfLocationsRobotArmWafersCanGoToNext();

                    bool atLeastTwoArmsAreEmpty = (numEmptyUsableArms >= 2);

                    var stToGet = filteredSubstTrackerList.Array.FirstOrDefault(st => st.evalInfo.flags.hasBeenStarted && !st.evalInfo.flags.isOnRobotArm && st.evalInfo.flags.canBeMovedNow && !st.evalInfo.nextLocNameList.Any(locName => hashSetOfSubstrateTrackerOnRobotNextLocNames.Contains(locName)) && (atLeastTwoArmsAreEmpty || !st.evalInfo.flags.nextStationEvalListIncludesPipelinedPMOutput));
                    if (stToGet != null)
                    {
                        var useArmLocName = r1ArmALocObserver.IsUnoccupiedAndAccessible ? r1ArmALocObserver.ID.Name : r1ArmBLocObserver.ID.Name;     // future: replace this with wear leveled selection version similar to the one that the SRM uses.

                        srmActionItemBuilderList.Add(new ApproachLocationItem(useArmLocName, stToGet.evalInfo.currentLocObs.ID.Name, waitUntilDone: false) { Comment = "GetMovableSubstToRobot"});
                        srmActionItemBuilderList.Add(new MoveSubstrateItem(stToGet.SubstID, useArmLocName));

                        srmActionItemBuilderSubstTrackerList.Add(stToGet);
                    }
                }

                // if there is a substrate that is going to PM4 and both arms are empty then move the substrate to an arm
                if (srmActionItemBuilderList.Count == 0 && numOccupiedUsableArms == 0)
                {
                    var stToGet = filteredSubstTrackerList.Array.FirstOrDefault(st => st.evalInfo.flags.hasBeenStarted && !st.evalInfo.flags.isOnRobotArm && (st.evalInfo.flags.canBeMovedNow || st.evalInfo.flags.canBeSwappedNow || st.evalInfo.flags.havePossibleAlmostAvailableForApproach && st.evalInfo.firstPossibleAlmostAvailableForApproachToStationEval.GetIsAlmostAvailableToApproachTarget(ToolConfig.MaxEstimatedAlmostAvailablePeriod, qpcTimeStamp)) && st.evalInfo.firstCanMoveToStationEval.IsPipelinedPMOutputLocation);
                    if (stToGet != null)
                    {
                        var useArmLocName = r1ArmALocObserver.IsUnoccupiedAndAccessible ? r1ArmALocObserver.ID.Name : r1ArmBLocObserver.ID.Name;     // future: replace this with wear leveled selection version similar to the one that the SRM uses.

                        srmActionItemBuilderList.Add(new ApproachLocationItem(useArmLocName, stToGet.evalInfo.currentLocObs.ID.Name, waitUntilDone: false) { Comment = "GetNextPipelinedOutputPMLocSubstToEmptyRobot"});
                        srmActionItemBuilderList.Add(new MoveSubstrateItem(stToGet.SubstID, useArmLocName));
                        srmActionItemBuilderList.Add(new ApproachLocationItem(stToGet.SubstID, ECSParts.PM4.InputLocID.Name, waitUntilDone: true));

                        srmActionItemBuilderSubstTrackerList.Add(stToGet);
                    }
                }

                // if any logic above generated one or more srm items then start an srmAction to run them.
                if (srmActionItemBuilderList.Count > 0)
                {
                    Logger.Debug.Emit("Creating SRM action [{0}]", srmActionItemBuilderList.First().Comment);

                    srmAction = ECSParts.SRM.Sequence(srmActionItemBuilderList.ToArray()).AddNotifyOnCompleteInline(HostingPartNotifier).StartInline();

                    srmActionSubstTrackerList.AddRange(srmActionItemBuilderSubstTrackerList);
                    srmActionSubstTrackerList.DoForEach(st => st.IsInSRMAction = true);

                    deltaCount++;
                }
            }

            if (PrepareFailedAnnunciator.ANState.IsSignaling)
            {
                bool canProceed = ServicePrepareFailedAnnunciator(stateToTool, ref requestAndStatusOutFromTool, ref deltaCount, ref qpcTimeStamp);

                if (!canProceed)
                    return deltaCount;
            }

            if (pendingPrepareList.Count > 0)
            {
                var failedPrepareSet = pendingPrepareList.FilterAndRemove(icf => icf.ActionState.IsComplete).ToArray().Where(icf => icf.ActionState.Failed).ToArray();

                if (!failedPrepareSet.IsNullOrEmpty())
                {
                    PostPrepareFailedError(stateToTool, "Prepare failed for one or more modules: [{0}]".CheckedFormat(string.Join(", ", failedPrepareSet.Select(icf => icf.ToString()))));
                }
                else if (pendingPrepareList.Count  == 0 && PrepareFailedAnnunciator.ANState.ANSignalState == ANSignalState.OnAndActionActive)
                {
                    string reason = "Retry completed normally: pending prepare operations succeeded";

                    PrepareFailedAnnunciator.NoteActionCompleted(reason);
                    PrepareFailedAnnunciator.Clear(reason);
                }
            }
            
            if (pendingPrepareList.Count == 0 && PrepareFailedAnnunciator.ANState.ANSignalState == ANSignalState.OnAndActionActive)
            {
                string reason = "Retry completed unexpectedly: pending prepare list is empty";
                PrepareFailedAnnunciator.NoteActionCompleted(reason);
                PrepareFailedAnnunciator.Clear(reason);
            }

            if (srmAction == null && filteredSubstTrackerList.Count > 0 && substrateStateTally.sjsHeld == 0 && isSubstrateLaunchEnabled && pendingPrepareList.Count == 0 && !PrepareFailedAnnunciator.ANState.IsSignaling)
            {
                var stWaitingForStart = filteredSubstTrackerList.Array.FirstOrDefault(st => !st.evalInfo.flags.hasBeenStarted && (st.evalInfo.flags.canBeMovedNow || st.evalInfo.flags.canBeSwappedNow || st.evalInfo.flags.havePossibleAlmostAvailableForApproach && st.evalInfo.firstPossibleAlmostAvailableForApproachToStationEval.GetIsAlmostAvailableToApproachTarget(ToolConfig.MaxEstimatedAlmostAvailablePeriod, qpcTimeStamp)));
                var anyStartedAtSource = (stWaitingForStart != null) &&  filteredSubstTrackerList.Array.Any(st => st.evalInfo.flags.hasBeenStarted && st.Info.STS == SubstState.AtSource);

                if (stWaitingForStart != null && !anyStartedAtSource)
                {
                    qpcTimeStamp = QpcTimeStamp.Now;

                    // If any step has a part that is not ready for the corresponding process then ask it to prepare. NOTE: this does not handle cases where multiple steps re-use the same process modules more than once or with different process step specs (that are not compatible)
                    var stationStepTupleSet = GeneratePrepareRelatedStationStepTupleSet(qpcTimeStamp, stWaitingForStart);

                    var anyNotReady = stationStepTupleSet.Any(stepTuple => stepTuple.Item2.Any(locTuple => locTuple.Item4));

                    if (anyNotReady && substrateStateTally.stsAtWork == 0)
                    {
                        foreach (var stepTuple in stationStepTupleSet)
                        {
                            var stepSpec = stepTuple.Item1;
                            foreach (var locTuple in stepTuple.Item2.Where(locTuple => locTuple.Item4))
                            {
                                if (!locTuple.Item3.SummaryState.IsSet(QuerySummaryState.CanPrepare))
                                {
                                    pendingPrepareTracker = stWaitingForStart;

                                    PostPrepareFailedError(stateToTool, "For Substrate {0}: Location '{1}' cannot prepare for step: {2}".CheckedFormat(pendingPrepareTracker.SubstID.Name, locTuple.Item1, stepSpec));

                                    deltaCount++;
                                    return deltaCount;
                                }

                                pendingPrepareList.Add(locTuple.Item2.PrepareForStep(stWaitingForStart.ProcessSpec, stepSpec).AddNotifyOnCompleteInline(HostingPartNotifier).StartInline());
                            }
                        }
                    }

                    if (pendingPrepareList.Count > 0)
                    {
                        pendingPrepareTracker = stWaitingForStart;
                    }
                    else if (!anyNotReady)
                    {
                        stWaitingForStart.SetSubstrateJobState(SubstrateJobState.Running, "Starting substrate normally (next process location is available, or is almost available)");
                        deltaCount++;
                    }
                    // else keep waiting - this path is generally triggered because the required prepare for the current substrate is blocked by other substrates that are still out in the tool.
                }
            }

            UpdateRequestAndStatusOutFromTool(ref requestAndStatusOutFromTool);

            return deltaCount;
        }

        private int ServiceRouteSequenceFailedAnnunciator(ISubstrateSchedulerPartStateToTool stateToTool, ref RequestsAndStatusOutFromTool requestAndStatusOutFromTool)
        {
            var anState = RouteSequenceFailedAnnunciator.ANState;

            noteStrandedSubstratesTriggerHoldoffTimer.StopIfNeeded();

            string autoActionDisableReason = GetAutoActionDisableReason(stateToTool);

            {
                string currentContinueActionDisableReason = anState.ActionList["Continue"].GetActionDisableReason();

                if (currentContinueActionDisableReason != autoActionDisableReason && !RouteSequenceFailedAnnunciator.ANState.ANSignalState.IsActionActive())
                {
                    PostRouteSequenceFailedError(stateToTool, "Continue action disable reason changed");

                    UpdateRequestAndStatusOutFromTool(ref requestAndStatusOutFromTool);

                    return 1;
                }
            }

            string selectedActionName = anState.SelectedActionName.MapNullToEmpty();

            switch (selectedActionName)
            {
                case "":
                    requestAndStatusOutFromTool.ToolStatusFlags |= SubstrateSchedulerPartStatusFlags.WaitingForActionToBeSelected;

                    return 0;       // keep waiting.

                case "Continue":
                    {
                        if (autoActionDisableReason.IsNeitherNullNorEmpty())
                        {
                            string reason = "{0} action selected while disabled".CheckedFormat(selectedActionName);

                            Logger.Warning.Emit("{0}: {1} [{2}]", RouteSequenceFailedAnnunciator.ANSpec, reason, autoActionDisableReason);

                            RouteSequenceFailedAnnunciator.NoteActionStarted(selectedActionName, reason);
                            RouteSequenceFailedAnnunciator.NoteActionFailed(reason);
                            PostRouteSequenceFailedError(stateToTool, reason);
                        }

                        {
                            string reason = "{0} action selected".CheckedFormat(selectedActionName);
                            RouteSequenceFailedAnnunciator.NoteActionStarted(selectedActionName, reason);

                            var icf = ECSParts.SRM.RetractArmsAndReleaseAll();
                            string ec = icf.Run();

                            if (ec.IsNullOrEmpty())
                            {
                                RouteSequenceFailedAnnunciator.NoteActionCompleted(reason);
                                RouteSequenceFailedAnnunciator.Clear("{0} action selected".CheckedFormat(selectedActionName));
                            }
                            else
                            {
                                RouteSequenceFailedAnnunciator.NoteActionFailed(icf.ToString(ToStringSelect.MesgAndDetail));
                                PostRouteSequenceFailedError(stateToTool, "{0} failed: {1}".CheckedFormat(selectedActionName, icf.ToString(ToStringSelect.MesgAndDetail)));
                            }
                        }

                        break;
                    }

                case "Return":
                    {
                        string reason = "{0} action selected".CheckedFormat(selectedActionName);

                        RouteSequenceFailedAnnunciator.NoteActionStarted(selectedActionName, reason);

                        foreach (var st in substTrackerList.Array)
                        {
                            if (st.Info.STS == SubstState.AtWork)
                                st.SubstrateJobRequestState = SubstrateJobRequestState.Return;
                            else if (st.Info.STS == SubstState.AtSource)
                                st.SubstrateJobRequestState = SubstrateJobRequestState.None;
                        }

                        RouteSequenceFailedAnnunciator.NoteActionCompleted(reason);
                        RouteSequenceFailedAnnunciator.Clear(reason);

                        requestAndStatusOutFromTool.RequestNVSFromTool = new NamedValueSet() { "SetRecovery", { "Reason", reason } };

                        break;
                    }

                case "Hold":
                    {
                        string reason = "{0} action selected".CheckedFormat(selectedActionName);

                        RouteSequenceFailedAnnunciator.NoteActionStarted(selectedActionName, reason);

                        foreach (var st in substTrackerList.Array)
                        {
                            if (st.Info.STS != SubstState.AtDestination)
                                st.SubstrateJobRequestState = SubstrateJobRequestState.None;
                        }

                        RouteSequenceFailedAnnunciator.NoteActionCompleted(reason);
                        RouteSequenceFailedAnnunciator.Clear(reason);

                        requestAndStatusOutFromTool.RequestNVSFromTool = new NamedValueSet() { "SetService", { "Reason", reason } };

                        break;
                    }

                default:
                    {
                        string reason = "{0} action was not recognized".CheckedFormat(selectedActionName);

                        RouteSequenceFailedAnnunciator.NoteActionStarted(selectedActionName, reason);
                        RouteSequenceFailedAnnunciator.NoteActionFailed(reason);

                        PostRouteSequenceFailedError(stateToTool, reason);

                        break;
                    }
            }

            UpdateRequestAndStatusOutFromTool(ref requestAndStatusOutFromTool);

            return 1;
        }

        private bool ServicePrepareFailedAnnunciator(ISubstrateSchedulerPartStateToTool stateToTool, ref RequestsAndStatusOutFromTool requestAndStatusOutFromTool, ref int deltaCount, ref QpcTimeStamp qpcTimeStamp)
        {
            bool canProceed = true;

            var anState = PrepareFailedAnnunciator.ANState;

            string autoActionDisableReason = GetAutoActionDisableReason(stateToTool);

            {
                string currentRetryActionDisableReason = anState.ActionList["Retry"].GetActionDisableReason();

                if (currentRetryActionDisableReason != autoActionDisableReason && !PrepareFailedAnnunciator.ANState.ANSignalState.IsActionActive())
                {
                    PostPrepareFailedError(stateToTool, "Retry action disable reason changed");

                    deltaCount++;
                }
            }

            string selectedActionName = anState.SelectedActionName.MapNullToEmpty();

            switch (selectedActionName)
            {
                case "":
                    requestAndStatusOutFromTool.ToolStatusFlags |= SubstrateSchedulerPartStatusFlags.WaitingForActionToBeSelected;
                    break;

                case "Retry":
                    {
                        if (autoActionDisableReason.IsNeitherNullNorEmpty())
                        {
                            string reason = "{0} action selected while disabled".CheckedFormat(selectedActionName);

                            Logger.Warning.Emit("{0}: {1} [{2}]", PrepareFailedAnnunciator.ANSpec, reason, autoActionDisableReason);

                            PrepareFailedAnnunciator.NoteActionStarted(selectedActionName, reason);
                            PrepareFailedAnnunciator.NoteActionFailed(reason);
                            PostPrepareFailedError(stateToTool, reason);
                        }

                        {
                            string reason = "{0} action selected".CheckedFormat(selectedActionName);
                            PrepareFailedAnnunciator.NoteActionStarted(selectedActionName, reason);

                            if (pendingPrepareTracker != null)
                            {
                                // If any step has a part that is not ready for the corresponding process then ask it to prepare. NOTE: this does not handle cases where multiple steps re-use the same process modules more than once or with different process step specs (that are not compatible)
                                var stationStepTupleSet = GeneratePrepareRelatedStationStepTupleSet(qpcTimeStamp, pendingPrepareTracker);

                                foreach (var stepTuple in stationStepTupleSet)
                                {
                                    var stepSpec = stepTuple.Item1;
                                    foreach (var locTuple in stepTuple.Item2.Where(locTuple => locTuple.Item4))
                                    {
                                        if (!locTuple.Item3.SummaryState.IsSet(QuerySummaryState.CanPrepare))
                                        {
                                            PostPrepareFailedError(stateToTool, "For Substrate {0}: Location '{1}' cannot prepare for step: {2}".CheckedFormat(pendingPrepareTracker.SubstID.Name, locTuple.Item1, stepSpec));

                                            deltaCount++;
                                            canProceed = false;
                                            break;
                                        }

                                        pendingPrepareList.Add(locTuple.Item2.PrepareForStep(pendingPrepareTracker.ProcessSpec, stepSpec).AddNotifyOnCompleteInline(HostingPartNotifier).StartInline());
                                    }
                                }

                                if (pendingPrepareList.Count == 0)
                                {
                                    reason = "Prepare does is no longer required";
                                    PrepareFailedAnnunciator.NoteActionCompleted(reason);
                                    PrepareFailedAnnunciator.Clear(reason);
                                }
                            }
                            else
                            {
                                Logger.Warning.Emit("Unable to process {0} {1}: there is no corresponding substrate to apply this request to", PrepareFailedAnnunciator.ANSpec.ANName, selectedActionName);
                                PrepareFailedAnnunciator.NoteActionFailed("{0} [no applicable substrate found]".CheckedFormat(reason));

                                PrepareFailedAnnunciator.Clear(reason);
                            }
                        }

                        break;
                    }

                case "AbortJob":
                case "StopJob":
                case "PauseJob":
                    {
                        string reason = "{0} action selected".CheckedFormat(selectedActionName);

                        SubstrateJobRequestState targetSJRS = SubstrateJobRequestState.Abort;

                        if (selectedActionName == "StopJob")
                            targetSJRS = SubstrateJobRequestState.Stop;
                        else if (selectedActionName == "PauseJob")
                            targetSJRS = SubstrateJobRequestState.Pause;
                        else
                            targetSJRS = SubstrateJobRequestState.Abort;

                        PrepareFailedAnnunciator.NoteActionStarted(selectedActionName, reason);

                        if (pendingPrepareTracker != null)
                        {
                            ECSParts.E039TableUpdater.Update(new E039UpdateItem.SetAttributes(pendingPrepareTracker.SubstID, new NamedValueSet() { selectedActionName })).Run();

                            pendingPrepareTracker.SubstrateJobRequestState = targetSJRS;
                            string applyToSubstratesFromJobID = (pendingPrepareTracker.JobTrackerLinkage != null) ? pendingPrepareTracker.JobTrackerLinkage.ID : null;

                            if (applyToSubstratesFromJobID.IsNeitherNullNorEmpty())
                            {
                                foreach (var st in substTrackerList)
                                {
                                    if (st.Info.STS == SubstState.AtDestination)
                                        continue;

                                    if (st.Info.SPS.IsProcessingComplete())
                                        continue;

                                    if (st.SubstrateJobRequestState == SubstrateJobRequestState.Abort || st.Info.SJS == SubstrateJobState.Aborting || st.Info.SJS == SubstrateJobState.Aborted)
                                        continue;

                                    if ((targetSJRS == SubstrateJobRequestState.Stop || targetSJRS == SubstrateJobRequestState.Pause) && (st.SubstrateJobRequestState == SubstrateJobRequestState.Stop || st.Info.SJS == SubstrateJobState.Stopping || st.Info.SJS == SubstrateJobState.Stopped))
                                        continue;

                                    if (targetSJRS == SubstrateJobRequestState.Pause && (st.SubstrateJobRequestState == SubstrateJobRequestState.Pause || st.Info.SJS == SubstrateJobState.Pausing || st.Info.SJS == SubstrateJobState.Paused))
                                        continue;

                                    var stJobID = (st.JobTrackerLinkage != null) ? st.JobTrackerLinkage.ID : null;
                                    if (stJobID.IsNeitherNullNorEmpty() && stJobID == applyToSubstratesFromJobID)
                                        st.SubstrateJobRequestState = targetSJRS;
                                }
                            }

                            PrepareFailedAnnunciator.NoteActionCompleted(reason);
                        }
                        else
                        {
                            Logger.Warning.Emit("Unable to process {0} {1}: there is no corresponding substrate to apply this request to", PrepareFailedAnnunciator.ANSpec.ANName, selectedActionName);
                            PrepareFailedAnnunciator.NoteActionFailed("{0} [no applicable substrate found]".CheckedFormat(reason));
                        }

                        PrepareFailedAnnunciator.Clear(reason);

                        break;
                    }

                default:
                    {
                        string reason = "{0} action was not recognized".CheckedFormat(selectedActionName);

                        PrepareFailedAnnunciator.NoteActionStarted(selectedActionName, reason);
                        PrepareFailedAnnunciator.NoteActionFailed(reason);

                        PostPrepareFailedError(stateToTool, reason);

                        break;
                    }
            }

            return canProceed;
        }

        private Tuple<ProcessStepSpecBase, Tuple<string, IPrepare<IProcessSpec, IProcessStepSpec>, PreparednessQueryResult<IProcessSpec, IProcessStepSpec>, bool>[]>[] GeneratePrepareRelatedStationStepTupleSet(QpcTimeStamp qpcTimeStamp, TestSubstrateAndProcessTracker stWaitingForStart)
        {
            var stationStepTupleSet = stWaitingForStart.ProcessSpec.Steps.Select(stepSpec => Tuple.Create(stepSpec, stepSpec.UsableLocNameList.Select(locName => Tuple.Create(locName, locNameToPrepareDictionary.SafeTryGetValue(locName)))
                                                                                                                                              .Where(t => t.Item2 != null)
                                                                                                                                              .Select(t => Tuple.Create(t.Item1, t.Item2, t.Item2.StatePublisher.Object.Query(stWaitingForStart.ProcessSpec, stepSpec, qpcTimeStamp)))
                                                                                                                                              .Select(t => Tuple.Create(t.Item1, t.Item2, t.Item3, t.Item3.SummaryState.IsClear(QuerySummaryState.Ready)))
                                                                                                                                              .ToArray())).ToArray();
            return stationStepTupleSet;
        }

        private string GetAutoActionDisableReason(ISubstrateSchedulerPartState stateToTool)
        {
            string autoActionDisableReason = (!stateToTool.BehaviorEnableFlags.IsSet(BehaviorEnableFlags.Automatic) ? "Scheduling behavior is not currently set to Automatic [{0}]".CheckedFormat(stateToTool.BehaviorEnableFlags) : null)
                                        ?? "";
            return autoActionDisableReason;
        }

        private List<IClientFacet> pendingPrepareList = new List<IClientFacet>();
        private TestSubstrateAndProcessTracker pendingPrepareTracker;

        private HashSet<string> hashSetOfSubstrateTrackerOnRobotNextLocNames = new HashSet<string>();

        private void UpdateHashSetOfLocationsRobotArmWafersCanGoToNext()
        {
            if (hashSetOfSubstrateTrackerOnRobotNextLocNames.Count > 0)
                hashSetOfSubstrateTrackerOnRobotNextLocNames.Clear();

            if (r1LocObserverArray.Any(slo => slo.IsOccupiedAndAccessible))
                hashSetOfSubstrateTrackerOnRobotNextLocNames.SafeAddRange(r1LocObserverArray.Select(slo => slo.Tracker).WhereIsNotDefault().SelectMany(st => st.evalInfo.nextLocNameList));
        }

        private bool AttemptToMoveOrSwapSubstrateToNextLocation(bool isProcessStepExecutionEnabled, TestSubstrateAndProcessTracker stOnArm, TestSubstrateAndProcessTracker.StationEvaluationItem swapAtStationEvalItem, int numEmptyUsableArms, string comment = "")
        {
            switch (swapAtStationEvalItem.station)
            {
                case TestStationEnum.AL1:
                case TestStationEnum.PM1:
                case TestStationEnum.PM2:
                case TestStationEnum.PMAbort:
                case TestStationEnum.PMReject:
                case TestStationEnum.PMReturn:
                    {
                        srmActionItemBuilderList.Add(new ApproachLocationItem(stOnArm.SubstID, swapAtStationEvalItem.locInfo.ObjID.Name, waitUntilDone: false) { Comment = comment });
                        srmActionItemBuilderList.Add(new MoveOrSwapSubstrateItem(stOnArm.SubstID, swapAtStationEvalItem.locInfo.ObjID.Name));

                        srmActionItemBuilderSubstTrackerList.AddRange(new[] { stOnArm, swapAtStationEvalItem.stAtLoc }.WhereIsNotDefault());

                        return true;
                    }

                case TestStationEnum.PM3Input:
                case TestStationEnum.PM4:
                    // for both of these cases the other method already does a move or swap and generates and starts the corresponding process.
                    return AttemptToMoveSubstrateToNextLocation(isProcessStepExecutionEnabled, stOnArm, swapAtStationEvalItem, numEmptyUsableArms, comment);

                default:
                    {
                        srmActionItemBuilderList.Add(new ApproachLocationItem(stOnArm.SubstID, swapAtStationEvalItem.locInfo.ObjID.Name, waitUntilDone: false) { Comment = comment });
                        srmActionItemBuilderList.Add(new MoveOrSwapSubstrateItem(stOnArm.SubstID, swapAtStationEvalItem.locInfo.ObjID.Name));

                        srmActionItemBuilderSubstTrackerList.AddRange(new[] { stOnArm, swapAtStationEvalItem.stAtLoc }.WhereIsNotDefault());

                        return true;
                    }
            }
        }

        private bool AttemptToMoveSubstrateToNextLocation(bool isProcessStepExecutionEnabled, TestSubstrateAndProcessTracker stToMove, TestSubstrateAndProcessTracker.StationEvaluationItem toStationEvalItem, int numEmptyUsableArms, string comment = "")
        {
            string moveToLocName = toStationEvalItem.locInfo.ObjID.Name;

            switch (toStationEvalItem.station)
            {
                case TestStationEnum.AL1:
                case TestStationEnum.PM1:
                case TestStationEnum.PM2:
                case TestStationEnum.PMAbort:
                case TestStationEnum.PMReject:
                case TestStationEnum.PMReturn:
                    {
                        srmActionItemBuilderList.Add(new ApproachLocationItem(stToMove.SubstID, moveToLocName, waitUntilDone: false) { Comment = comment });
                        srmActionItemBuilderList.Add(new MoveSubstrateItem(stToMove.SubstID, moveToLocName));

                        srmActionItemBuilderSubstTrackerList.Add(stToMove);

                        return true;
                    }

                case TestStationEnum.PM3Input:
                    if (isProcessStepExecutionEnabled && numEmptyUsableArms > 0)
                    {
                        srmActionItemBuilderList.Add(new ApproachLocationItem(stToMove.SubstID, moveToLocName, waitUntilDone: false) { Comment = comment });
                        srmActionItemBuilderList.Add(new TransferPermissionRequestItem(TransferPermissionRequestItemSettings.AcquireAndAutoReleaseAtEndOfSequence, moveToLocName));
                        srmActionItemBuilderList.Add(new MoveOrSwapSubstrateItem(stToMove.SubstID, moveToLocName));
                        srmActionItemBuilderList.Add(new RunActionItem(icfFactoryDelegate: stToMove.GeneratePendingRunProcessActionAndCorrespondingDelegate(moveToLocName, HostingPartNotifier, autoReleaseKey: moveToLocName), runActionBehavior: RunActionBehaviorFlags.OnlyStartAction));
                        if (stToMove.CurrentStationProcessICF != null)
                            trackersInProcessList.Add(stToMove);

                        srmActionItemBuilderSubstTrackerList.Add(stToMove); // it is not easy to know which substrate might get pulled from the PM in this case however it will only end up on the arm which is not a suitable next processing location so there is no potential process start race condition created here

                        return true;
                    }
                    break;

                case TestStationEnum.PM4:
                    if (isProcessStepExecutionEnabled && numEmptyUsableArms > 0)
                    {
                        // block moving a substrate onto the arms or swapping it with the pm4 transfer location if that location already has a substrate and we have already asked that substrate to be processed there.
                        var pm4TransferLocST = pm4TransferLocObserver.Tracker;
                        bool pm4TransferLocIsInProcess = (pm4TransferLocST != null) && (pm4TransferLocST.CurrentStationProcessICF != null);

                        if (!pm4TransferLocIsInProcess)
                        {
                            srmActionItemBuilderList.Add(new ApproachLocationItem(stToMove.SubstID, moveToLocName, waitUntilDone: false) { Comment = comment });
                            srmActionItemBuilderList.Add(new TransferPermissionRequestItem(TransferPermissionRequestItemSettings.AcquireAndAutoReleaseAtEndOfSequence, moveToLocName));
                            srmActionItemBuilderList.Add(new MoveOrSwapSubstrateItem(stToMove.SubstID, moveToLocName));
                            srmActionItemBuilderList.Add(new RunActionItem(icfFactoryDelegate: stToMove.GeneratePendingRunProcessActionAndCorrespondingDelegate(moveToLocName, HostingPartNotifier, autoReleaseKey: moveToLocName), runActionBehavior: RunActionBehaviorFlags.OnlyStartAction));
                            if (stToMove.CurrentStationProcessICF != null)
                                trackersInProcessList.Add(stToMove);

                            srmActionItemBuilderSubstTrackerList.Add(stToMove); // it is not easy to know which substrate might get pulled from the PM in this case however it will only end up on the arm which is not a suitable next processing location so there is no potential process start race condition created here

                            return true;
                        }
                    }
                    break;

                default:
                    {
                        // this case covers load ports
                        srmActionItemBuilderList.Add(new ApproachLocationItem(stToMove.SubstID, moveToLocName, waitUntilDone: false) { Comment = comment });
                        srmActionItemBuilderList.Add(new MoveSubstrateItem(stToMove.SubstID, moveToLocName));

                        srmActionItemBuilderSubstTrackerList.Add(stToMove);

                        return true;
                    }
            }

            return false;
        }

        private void PostRouteSequenceFailedError(ISubstrateSchedulerPartState stateToTool, string reason)
        {
            if (RouteSequenceFailedAnnunciator.ANState.ANSignalState.IsActionActive())
                RouteSequenceFailedAnnunciator.NoteActionAborted("Aborting current action: Post called while action active [{0}]".CheckedFormat(reason));

            string autoActionDisableReason = GetAutoActionDisableReason(stateToTool);

            RouteSequenceFailedAnnunciator.Post(new NamedValueSet() { { "Continue", autoActionDisableReason }, {"Return", "" }, { "Hold", "" } }, reason);
        }

        private void PostPrepareFailedError(ISubstrateSchedulerPartState stateToTool, string reason)
        {
            if (PrepareFailedAnnunciator.ANState.ANSignalState.IsActionActive())
                PrepareFailedAnnunciator.NoteActionAborted("Aborting current action: Post called while action active [{0}]".CheckedFormat(reason));

            string autoActionDisableReason = GetAutoActionDisableReason(stateToTool);

            PrepareFailedAnnunciator.Post(new NamedValueSet() { { "Retry", autoActionDisableReason }, { "AbortJob", "" }, { "StopJob", "" }, { "PauseJob", "" } }, reason);
        }

        private void UpdateRequestAndStatusOutFromTool(ref RequestsAndStatusOutFromTool requestAndStatusOutFromTool)
        {
            if (RouteSequenceFailedAnnunciator.ANState.IsSignaling)
                requestAndStatusOutFromTool.ToolFaultReason = "RouteSequenceFailed Error is signaling";
            else if (PrepareFailedAnnunciator.ANState.IsSignaling)
                requestAndStatusOutFromTool.ToolFaultReason = "PrepareFailed Error is signaling";

            if (RouteSequenceFailedAnnunciator.ANState.ANSignalState == ANSignalState.OnAndWaiting || PrepareFailedAnnunciator.ANState.ANSignalState == ANSignalState.OnAndWaiting)
                requestAndStatusOutFromTool.ToolStatusFlags |= SubstrateSchedulerPartStatusFlags.WaitingForActionToBeSelected;
        }

        private int ServiceTrackersAndUpdateObservers(ISubstrateSchedulerPartState stateToTool, QpcTimeStamp qpcTimeStamp)
        {
            bool alreadyFoundFirstWaitingForStartSubstrate = false;
            int substDeltaCount = 0;

            substrateStateTally.Clear();

            foreach (var st in substTrackerList.Array)
            {
                substDeltaCount += st.Service(stateToTool: stateToTool, qpcTimeStamp: qpcTimeStamp, alreadyFoundFirstWaitingForStartSubstrate: alreadyFoundFirstWaitingForStartSubstrate, sjsStateChangeTriggerFlags: ToolConfig.ServiceBasicSJSStateChangeTriggerFlags);

                if (st.Info.SJS == SubstrateJobState.WaitingForStart)
                    alreadyFoundFirstWaitingForStartSubstrate = true;

                substrateStateTally.Add(st);
            }

            int locDeltaCount = allLocObserverDictionary.ValueArray.Sum(item => item.Update().MapToInt());
            
            return locDeltaCount + substDeltaCount;
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
                    E090SubstLocInfo substLocInfo = new E090SubstLocInfo(ECSParts.E039TableObserver.GetObject(new E039ObjectID(locName, E090.Constants.SubstrateLocationObjectType)));
                    if (substLocInfo.IsUnoccupied)
                        return locName;
                }
            }

            return null;
        }
    }

    #endregion

    #region TestSRM (TestSRMConfig)

    public class TestSRMConfig : SRMConfigBase, ICopyable<TestSRMConfig>
    {
        public TestSRMConfig(string partID = "SRM") : base(partID)
        {
            NumLPSlots = 5;
            NumLPWafers = 5;
        }

        public TestSRMConfig(TestSRMConfig other) : base(other)
        {
            ECSParts = other.ECSParts;
            NumLPSlots = other.NumLPSlots;
            NumLPWafers = other.NumLPWafers;
            DisableR1ArmB = other.DisableR1ArmB;
        }

        public TestECSParts ECSParts { get; set; }
        public int NumLPSlots { get; set; }
        public int NumLPWafers { get; set; }

        public bool DisableR1ArmB { get; set; }

        /// <summary>Makes and returns a clone/copy of this object</summary>
        public TestSRMConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return new TestSRMConfig(this);
        }
    }

    /// <summary>
    /// This is a test ISubstrateRoutingManager that is used for testing substrate routing and processing related code.
    /// It creates a single loadport with 5 slots (by default) (LP1), and up to 5 wafers in these slots.  It creates a single dual arm robot (R1), and an aligner station (AL1).
    /// It is aware of 4 PMs: PM1, PM2 (single location each), PM3 (seperate inbound and outbound location) and PM4 (exchange location).
    /// </summary>
    public class TestSRM : SRMBase<TestSRMConfig>
    {
        public TestSRM(TestSRMConfig config)
            : base(config, config.ECSParts.E039TableUpdater, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2)
        {
            var ecsParts = Config.ECSParts;

            LPSlotLocIDArray = Enumerable.Range(1, Config.NumLPSlots).Select(slotNum => { E039ObjectID id = null; E039TableUpdater.CreateE090SubstLoc("LP1.{0:d2}".CheckedFormat(slotNum), v => { id = v; }, instanceNum: slotNum); return id; }).ToArray();

            R1EitherArmName = "R1";
            E039TableUpdater.CreateE090SubstLoc(R1EitherArmName + ".A", v => R1ArmALocID = v, instanceNum: 1);
            E039TableUpdater.CreateE090SubstLoc(R1EitherArmName + ".B", v => R1ArmBLocID = v, instanceNum: 2);
            E039TableUpdater.CreateE090SubstLoc("AL1", v => AL1LocID = v);

            Enumerable.Range(0, Math.Min(Config.NumLPSlots, Config.NumLPWafers)).DoForEach(slotIndex => { var slotNum = slotIndex + 1; E039TableUpdater.CreateE090Subst("TestWafer_{0:d2}".CheckedFormat(slotNum), (E039ObjectID v) => { }, LPSlotLocIDArray[slotIndex]); });

            lpSlotLocObserverArray = LPSlotLocIDArray.Select(id => new E090SubstLocObserver(id)).ToArray();

            var observerSet = new[] 
            { 
                R1ArmALocID, 
                R1ArmBLocID, 
                AL1LocID, 
                ecsParts.PM1.LocID, 
                ecsParts.PM2.LocID, 
                ecsParts.PM3.InputLocID, ecsParts.PM3.OutputLocID, 
                ecsParts.PM4.InputLocID, 
                ecsParts.PMReject.LocID,
                ecsParts.PMAbort.LocID,
                ecsParts.PMReturn.LocID,
            }.Select(id => new E090SubstLocObserver(id)).ToArray();

            robotArmALocObserver = observerSet[0];
            robotArmBLocObserver = observerSet[1];
            robotArmLocObserverArray = new[] { robotArmALocObserver, robotArmBLocObserver };

            SetAllSubstLocObservers(observerSet.Concat(lpSlotLocObserverArray));

            AddMainThreadStoppingAction(() => { Log.Info.Emit("Final {0}", Counters); });

            if (config.DisableR1ArmB)
            {
                E039TableUpdater.SetSubstLocStates(robotArmBLocObserver, notAccessibleReasonParam: "This arm is disabled");
            }
        }

        public E039ObjectID[] LPSlotLocIDArray { get; private set; }
        public string R1EitherArmName { get; private set; }
        public E039ObjectID R1ArmALocID { get; private set; }
        public E039ObjectID R1ArmBLocID { get; private set; }
        public E039ObjectID AL1LocID { get; private set; }

        private E090SubstLocObserver[] lpSlotLocObserverArray;
        private E090SubstLocObserver robotArmALocObserver, robotArmBLocObserver;
        private E090SubstLocObserver [] robotArmLocObserverArray;

        public struct CounterValues
        {
            public int approachCount;
            public int moveCount;
            public int swapCount;

            public override string ToString()
            {
                return "Counter Values approach:{0} move:{1} swap:{2}".CheckedFormat(approachCount, moveCount, swapCount);
            }
        }

        private CounterValues _counters;
        public CounterValues Counters { get { return _counters; } }

        #region injectable location specific holds 

        public readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> holdAtGetFromSubstLocNameDict = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
        public readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> holdAtPutToSubstLocNameDict = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();

        public bool IsWaiting { get; private set; }

        private void WaitBeforeGetIfNeeded(string getFromSubstLocName)
        {
            WaitIfNeeded(getFromSubstLocName, holdAtGetFromSubstLocNameDict, "get from");
        }

        private void WaitBeforePutIfNeeded(string putToSubstLocName)
        {
            WaitIfNeeded(putToSubstLocName, holdAtPutToSubstLocNameDict, "put to");
        }

        private void WaitIfNeeded(string substLocName, System.Collections.Concurrent.ConcurrentDictionary<string, bool> holdSubstLocNameDict, string opDescription)
        {
            if (holdSubstLocNameDict.IsEmpty || !holdSubstLocNameDict.ContainsKey(substLocName) || !holdSubstLocNameDict[substLocName])
                return;

            IsWaiting = true;
            NotifyBaseStateNotifier();

            Log.Debug.Emit("Waiting before {0} {1} [hold set: {2}]", opDescription, substLocName, string.Join(",", holdSubstLocNameDict.ToArray().Where(kvp => kvp.Value).Select(kvp => kvp.Key)));

            while (!HasStopBeenRequested)
            {
                (0.01).FromSeconds().Sleep();

                if (!holdSubstLocNameDict[substLocName])
                    break;
            }

            Log.Debug.Emit("Done waiting before {0} {1} [hold set: {2}]", opDescription, substLocName, string.Join(",", holdSubstLocNameDict.ToArray().Where(kvp => kvp.Value).Select(kvp => kvp.Key)));

            IsWaiting = false;
            NotifyBaseStateNotifier();
        }

        #endregion

        protected override string PerformRetractArms(IProviderFacet ipf)
        {
            return "";
        }

        protected override string InnerPerformMoveSubstrate(IProviderFacet ipf, E090SubstObserver substObs, E090SubstLocObserver fromLocObs, E090SubstLocObserver toLocObs, string desc)
        {
            var toLocName = toLocObs.ID.Name;
            var fromLocName = fromLocObs.ID.Name;

            if (toLocName == R1EitherArmName)
            {
                if (robotArmALocObserver.IsUnoccupied && robotArmALocObserver.IsAccessible)
                    toLocName = robotArmALocObserver.ID.Name;
                else if (robotArmBLocObserver.IsUnoccupied && robotArmBLocObserver.IsAccessible)
                    toLocName = robotArmBLocObserver.ID.Name;
                else
                    return "{0} failed: neither robot arm is available".CheckedFormat(desc);
            }

            E090SubstLocObserver useArmObserver = null;

            if (fromLocName == R1ArmALocID.Name)
                useArmObserver = robotArmALocObserver;
            else if (fromLocName == R1ArmBLocID.Name)
                useArmObserver = robotArmBLocObserver;
            else if (robotArmALocObserver.IsUnoccupied && robotArmALocObserver.IsAccessible)
                useArmObserver = robotArmALocObserver;
            else if (robotArmBLocObserver.IsUnoccupied && robotArmBLocObserver.IsAccessible)
                useArmObserver = robotArmBLocObserver;
            else
                return "{0} failed: neither robot arm is available".CheckedFormat(desc);

            string ec = string.Empty;

            if (ec.IsNullOrEmpty())
                ec = AcquireLocationTransferPermissionForThisItemIfNeeded(ipf, fromLocObs.ID.Name, toLocObs.ID.Name);

            if (useArmObserver.ID.Name != fromLocName && ec.IsNullOrEmpty())
            {
                string failNextGetFromLocName = substObs.Object.Attributes["FailNextGetFromLocName"].VC.GetValueA(rethrow: false);

                if (ec.IsNullOrEmpty() && failNextGetFromLocName.IsNeitherNullNorEmpty() && failNextGetFromLocName == substObs.Info.LocID)
                {
                    ec = "Get {0} from {1} failed by request during move".CheckedFormat(substObs.ID.FullName, failNextGetFromLocName);
                    E039TableUpdater.Update(new E039UpdateItem.SetAttributes(substObs.ID, new NamedValueSet() { "FailNextGetFromLocName" }, NamedValueMergeBehavior.RemoveEmpty)).Run();
                    substObs.Update();
                }

                WaitBeforeGetIfNeeded(substObs.Info.LocID);

                if (ec.IsNullOrEmpty())
                    ec = E039TableUpdater.NoteSubstMoved(substObs, useArmObserver.ID);
            }

            if (ec.IsNullOrEmpty() && !useArmObserver.ID.Equals(toLocObs.ID))
            {
                string failNextPutToLocName = substObs.Object.Attributes["FailNextPutToLocName"].VC.GetValueA(rethrow: false);

                if (ec.IsNullOrEmpty() && failNextPutToLocName.IsNeitherNullNorEmpty() && failNextPutToLocName == toLocObs.ID.Name)
                {
                    ec = "Put {0} to {1} failed by request during move".CheckedFormat(substObs.ID.FullName, failNextPutToLocName);
                    E039TableUpdater.Update(new E039UpdateItem.SetAttributes(substObs.ID, new NamedValueSet() { "FailNextPutToLocName" }, NamedValueMergeBehavior.RemoveEmpty)).Run();
                    substObs.Update();
                }

                WaitBeforePutIfNeeded(toLocObs.ID.Name);

                if (ec.IsNullOrEmpty())
                    ec = E039TableUpdater.NoteSubstMoved(substObs, toLocObs.ID);
            }

            if (ec.IsNullOrEmpty())
                _counters.moveCount++;

            return ec;
        }

        protected override string InnerPerformSwapSubstrates(IProviderFacet ipf, E090SubstObserver substObs, E090SubstLocObserver fromLocObs, E090SubstObserver swapWithSubstObs, E090SubstLocObserver swapAtLocObs, string desc)
        {
            var substCurrentLocName = fromLocObs.ID.Name;
            var swapWithSubstCurrentLocName = swapAtLocObs.ID.Name;

            E090SubstLocObserver fromArmObserver = null, toArmObserver = null;

            if (!robotArmALocObserver.IsAccessible || !robotArmBLocObserver.IsAccessible)
            {
                var unusableArmListStr = string.Join(", ", robotArmLocObserverArray.Where(obs => !obs.IsAccessible).Select(obs => "{0} is not accessible: {1}".CheckedFormat(obs.ID.Name, obs.Info.NotAccessibleReason)));
                return "{0} failed: both robot arms must be accessible [{1}]".CheckedFormat(desc, unusableArmListStr);
            }
            else if (substCurrentLocName == R1ArmALocID.Name && robotArmBLocObserver.Info.IsUnoccupied)
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
                ec = AcquireLocationTransferPermissionForThisItemIfNeeded(ipf, fromLocObs.ID.Name, swapAtLocObs.ID.Name);

            if (fromArmObserver.ID.Name != substCurrentLocName && ec.IsNullOrEmpty())
            {
                string failNextGetFromLocName = substObs.Object.Attributes["FailNextGetFromLocName"].VC.GetValueA(rethrow: false);

                if (ec.IsNullOrEmpty() && failNextGetFromLocName.IsNeitherNullNorEmpty() && failNextGetFromLocName == substObs.Info.LocID)
                {
                    ec = "Get {0} from {1} failed by request during swap".CheckedFormat(substObs.ID.FullName, failNextGetFromLocName);
                    E039TableUpdater.Update(new E039UpdateItem.SetAttributes(substObs.ID, new NamedValueSet() { "FailNextGetFromLocName" }, NamedValueMergeBehavior.RemoveEmpty)).Run();
                    substObs.Update();
                }

                WaitBeforeGetIfNeeded(substObs.Info.LocID);

                if (ec.IsNullOrEmpty())
                    ec = E039TableUpdater.NoteSubstMoved(substObs, fromArmObserver.ID);
            }

            if (ec.IsNullOrEmpty())
            {
                string failNextGetFromLocName = swapWithSubstObs.Object.Attributes["FailNextGetFromLocName"].VC.GetValueA(rethrow: false);

                if (ec.IsNullOrEmpty() && failNextGetFromLocName.IsNeitherNullNorEmpty() && failNextGetFromLocName == swapWithSubstObs.Info.LocID)
                {
                    ec = "Get {0} from {1} failed by request during swap".CheckedFormat(swapWithSubstObs.ID.FullName, failNextGetFromLocName);
                    E039TableUpdater.Update(new E039UpdateItem.SetAttributes(swapWithSubstObs.ID, new NamedValueSet() { "FailNextGetFromLocName" }, NamedValueMergeBehavior.RemoveEmpty)).Run();
                    swapWithSubstObs.Update();
                }

                WaitBeforeGetIfNeeded(swapWithSubstObs.Info.LocID);

                if (ec.IsNullOrEmpty())
                    ec = E039TableUpdater.NoteSubstMoved(swapWithSubstObs, toArmObserver.ID);
            }

            if (ec.IsNullOrEmpty())
            {
                string failNextPutToLocName = substObs.Object.Attributes["FailNextPutToLocName"].VC.GetValueA(rethrow: false);

                if (ec.IsNullOrEmpty() && failNextPutToLocName.IsNeitherNullNorEmpty() && failNextPutToLocName == swapAtLocObs.ID.Name)
                {
                    ec = "Put {0} to {1} failed by request during swap".CheckedFormat(substObs.ID.FullName, failNextPutToLocName);
                    E039TableUpdater.Update(new E039UpdateItem.SetAttributes(substObs.ID, new NamedValueSet() { "FailNextPutToLocName" }, NamedValueMergeBehavior.RemoveEmpty)).Run();
                    substObs.Update();
                }

                WaitBeforePutIfNeeded(swapAtLocObs.ID.Name);

                if (ec.IsNullOrEmpty())
                    ec = E039TableUpdater.NoteSubstMoved(substObs, swapAtLocObs.ID);
            }

            if (ec.IsNullOrEmpty())
                _counters.swapCount++;

            return ec;
        }

        protected override string InnerPerformApproach(IProviderFacet ipf, ApproachLocationItem item, E090SubstLocObserver toLocObs, string desc)
        {
            _counters.approachCount++;

            return "";
        }

        protected override E090SubstLocObserver GetSubstLocObserver(string locName, SubstLocType locType = SubstLocType.Normal)
        {
            var substLocObs = base.GetSubstLocObserver(locName, locType);

            if (substLocObs == null && locType == SubstLocType.EmptyDestination && locName == R1EitherArmName)
            {
                if (robotArmALocObserver.IsUnoccupied)
                    substLocObs = robotArmALocObserver;
                else if (robotArmBLocObserver.IsUnoccupied)
                    substLocObs = robotArmBLocObserver;
            }

            return substLocObs;
        }
    }

    #endregion

    #region ISimpleExampleProcessModuleEngine (et. al.)

    /// <summary>
    /// Interface for a simple single slot example ProcessModule engine
    /// </summary>
    public interface ISimpleExampleProcessModuleEngine : IActivePartBase, ITransferPermissionRequest, IPrepare<IProcessSpec, IProcessStepSpec>
    {
        IClientFacet RunProcess(E039ObjectID substID, IProcessStepSpec stepSpec, string autoReleaseTransferPermissionLocNameAtStart = null, string autoAcquireTransferPermissionLocNameAtEnd = null);
        E039ObjectID LocID { get; }
    }

    /// <summary>
    /// Implementation class for a simple single slot example ProcessModule engine
    /// </summary>
    public class SimpleExampleProcessModuleEngine : SimpleActivePartBase, ISimpleExampleProcessModuleEngine
    {
        public SimpleExampleProcessModuleEngine(string partID, TestECSParts testECSParts, string pmLocName = null, bool enableAlmostAvailable = true)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(waitTimeLimit: (0.01).FromSeconds(), simplePartBaseSettings: SimplePartBaseSettings.DefaultVersion2.Build(addSimplePartBaseBehavior: SimplePartBaseBehavior.TreatPartAsOnlineFailureWhenOnlineOrOnlineBusyWithExplicitFailure)))
        {
            TestECSParts = testECSParts;
            E039TableUpdater = TestECSParts.E039TableUpdater;

            E039TableUpdater.CreateE090SubstLoc(pmLocName, ao => locObserver = new E090SubstLocObserver(ao.AddedObjectPublisher));
            LocID = locObserver.ID;

            ProcessAlmostCompleteLeadPeriod = (0.05).FromSeconds();
            EnableAlmostAvailable = enableAlmostAvailable;

            transferPermissionStateIVA = Values.Instance.GetValueAccessor(PartID + ".TPS").Set(lastPublishedTransferPermissionState);

            preparednessStateIVA = Values.Instance.GetValueAccessor(PartID + ".PreparednessState");
            PublishPreparednessState();
        }

        public TestECSParts TestECSParts { get; private set; }
        public IE039TableUpdater E039TableUpdater { get; private set; }

        E090SubstLocObserver locObserver;

        public E039ObjectID LocID { get; private set; }

        public TimeSpan ProcessAlmostCompleteLeadPeriod { get; private set; }
        public bool EnableAlmostAvailable { get; private set; }

        protected override string PerformServiceActionEx(IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            switch (serviceName)
            {
                case "SetExplicitFaultReason":
                    SetExplicitFaultReason(npv["Reason"].VC.GetValueA(rethrow: true));
                    PerformMainLoopService();
                    return string.Empty;

                case "ReleaseHold":
                    preparednessState.SummaryState &= ~QuerySummaryState.HoldInboundMaterial;
                    preparednessState.Reason = serviceName;
                    PublishPreparednessState();
                    return string.Empty;

                default:
                    return base.PerformServiceActionEx(ipf, serviceName, npv);
            }
        }

        #region ITransferPermissionRequest

        public IClientFacet TransferPermission(TransferPermissionRequestType requestType, string locName = "")
        {
            string desc = "{0}({1}{2})".CheckedFormat(CurrentMethodName, requestType, locName != null ? ", loc:{0}".CheckedFormat(locName) : "");
            return new BasicActionImpl(actionQ, ipf => PerformTransferPermission(ipf, requestType, locName), desc, ActionLoggingReference);
        }

        public INotificationObject<ITransferPermissionState> TransferPermissionStatePublisher { get { return transferPermissionStatePublisher; } }
        InterlockedNotificationRefObject<ITransferPermissionState> transferPermissionStatePublisher = new InterlockedNotificationRefObject<ITransferPermissionState>(TransferPermissionStateForPublication.Empty);

        ITransferPermissionState lastPublishedTransferPermissionState = TransferPermissionStateForPublication.Empty;

        TransferPermissionState transferPermissionState = new TransferPermissionState();
        bool isLocationLockedForTransfer;

        IValueAccessor transferPermissionStateIVA;

        private void PublishTransferPermissionStateIfNeeded()
        {
            if (!transferPermissionState.Equals(lastPublishedTransferPermissionState))
            {
                bool justBecameAvailable = (transferPermissionState.IsAvailable() && !lastPublishedTransferPermissionState.IsAvailable());

                transferPermissionStatePublisher.Object = (lastPublishedTransferPermissionState = new TransferPermissionStateForPublication(transferPermissionState));
                transferPermissionStateIVA.Set(lastPublishedTransferPermissionState);

                Log.Debug.Emit("Published {0}", lastPublishedTransferPermissionState);

                if (justBecameAvailable && pendingAcquireKVPList.Count > 0)
                {
                    CompletePendingAcquireRequests(publish: false);

                    transferPermissionStatePublisher.Object = (lastPublishedTransferPermissionState = new TransferPermissionStateForPublication(transferPermissionState));
                    transferPermissionStateIVA.Set(lastPublishedTransferPermissionState);

                    Log.Debug.Emit("Published {0} [after completing pending work]", lastPublishedTransferPermissionState);
                }
            }
        }

        private string PerformTransferPermission(IProviderFacet ipf, TransferPermissionRequestType requestType, string locName)
        {
            switch (requestType)
            {
                case TransferPermissionRequestType.Acquire:
                    if (transferPermissionState.SummaryStateCode == TransferPermissionSummaryStateCode.AlmostAvailable || transferPermissionState.SummaryStateCode == TransferPermissionSummaryStateCode.Busy)
                    {
                        pendingAcquireKVPList.Add(KVP.Create(locName, ipf));

                        Log.Debug.Emit("TransferPermission state is {0}: request '{1}' has been put into the pending list", transferPermissionState, ipf.ToString(ToStringSelect.MesgAndDetail));

                        return null;
                    }

                    if (!transferPermissionState.SummaryStateCode.IsAvailable())
                        return "TransferPermissionInterface is not available [{0}]".CheckedFormat(transferPermissionState);

                    transferPermissionState.GrantedTokenSet.Add(locName);
                    break;

                case TransferPermissionRequestType.Release:
                    transferPermissionState.GrantedTokenSet.Remove(locName);
                    break;

                case TransferPermissionRequestType.ReleaseAll:
                    transferPermissionState.GrantedTokenSet.Clear();
                    break;

                default:
                    return "Unsupported TransferPermissionRequest type {0}".CheckedFormat(requestType);
            }

            UpdateTransferPermission();

            return "";
        }

        List<KeyValuePair<string, IProviderFacet>> pendingAcquireKVPList = new List<KeyValuePair<string, IProviderFacet>>();

        private void UpdateTransferPermission(bool publish = true)
        {
            isLocationLockedForTransfer = transferPermissionState.IsAnyGranted(checkAvailable: false);

            if (publish)
                PublishTransferPermissionStateIfNeeded();
        }

        #endregion

        #region IPrepare

        public IClientFacet PrepareForStep(IProcessSpec processSpec, IProcessStepSpec processStepSpec)
        {
            return new BasicActionImpl(actionQ, ipf => PerformPrepareForStep(ipf, processSpec, processStepSpec), Fcns.CurrentMethodName, ActionLoggingReference, "Spec(s):{0}".CheckedFormat(processSpec.CombineRecipeNames(processStepSpec)));
        }

        public string FailNextPrepareForProcessStepSpecName { get; set; }

        private string PerformPrepareForStep(IProviderFacet ipf, IProcessSpec processSpec, IProcessStepSpec processStepSpec)
        {
            PerformMainLoopService();

            if (currentProcessTracker != null)
                return "Not permitted while process is active";

            if (locObserver.IsOccupied)
                return "Not permitted while substrate is present [{0}]".CheckedFormat(locObserver.ContainsSubstInfo);

            string ec = string.Empty;
            if (BaseState.UseState != UseState.Online)
                ec = OuterPerformGoOnlineAction(ipf, true, ipf.NamedParamValues);

            string failNextPrepareForProcessStepSpecName = FailNextPrepareForProcessStepSpecName;
            if (ec.IsNullOrEmpty() && failNextPrepareForProcessStepSpecName.IsNeitherNullNorEmpty() && processStepSpec.StepRecipeName == failNextPrepareForProcessStepSpecName)
            {
                FailNextPrepareForProcessStepSpecName = null;

                ec = "Prepare failed by request";
            }

            if (ec.IsNullOrEmpty())
            {
                preparednessState.ProcessSpec = processSpec;
                preparednessState.ProcessStepSpec = processStepSpec;
                preparednessState.SetSummaryState(QuerySummaryState.Ready | QuerySummaryState.CanPrepare, ipf.ToString(ToStringSelect.MesgAndDetail));
            }
            else
            {
                preparednessState.ProcessSpec = processSpec;
                preparednessState.ProcessStepSpec = processStepSpec;
                preparednessState.SetSummaryState(QuerySummaryState.CanPrepare, "{0} failed: {1}".CheckedFormat(ipf.ToString(ToStringSelect.MesgAndDetail), ec));
            }

            PublishPreparednessState();

            return ec;
        }

        protected PreparednessState<IProcessSpec, IProcessStepSpec> preparednessState = new PreparednessState<IProcessSpec, IProcessStepSpec>();

        protected IPreparednessState<IProcessSpec, IProcessStepSpec> lastPublishedPreparednessState;
        protected IValueAccessor preparednessStateIVA;

        public INotificationObject<IPreparednessState<IProcessSpec, IProcessStepSpec>> PreparednessStatePublisher { get { return preparednessStatePublisher; } }

        INotificationObject<IPreparednessState<IProcessSpec, IProcessStepSpec>> IPrepare<IProcessSpec, IProcessStepSpec>.StatePublisher { get { return preparednessStatePublisher; } }
        protected InterlockedNotificationRefObject<IPreparednessState<IProcessSpec, IProcessStepSpec>> preparednessStatePublisher = new InterlockedNotificationRefObject<IPreparednessState<IProcessSpec, IProcessStepSpec>>();

        protected void PublishPreparednessState()
        {
            lastPublishedPreparednessState = preparednessState.MakeCopyOfThis();

            Log.Debug.Emit("Publishing {0}", lastPublishedPreparednessState);

            preparednessStatePublisher.Object = lastPublishedPreparednessState;
            preparednessStateIVA.Set(new PreparednessStateForPublication(lastPublishedPreparednessState));
        }

        #endregion

        public IClientFacet RunProcess(E039ObjectID substID, IProcessStepSpec stepSpec, string autoReleaseTransferPermissionLocNameAtStart = null, string autoAcquireTransferPermissionLocNameAtEnd = null)
        {
            StringBuilder sb = new StringBuilder(CurrentMethodName);
            sb.CheckedAppendFormat("({0}, {1}", substID.FullName, stepSpec);

            if (autoReleaseTransferPermissionLocNameAtStart == "")
                sb.Append(", AutoRelease");
            else if (autoReleaseTransferPermissionLocNameAtStart != null)
                sb.CheckedAppendFormat(", AutoRelease:{0}", autoReleaseTransferPermissionLocNameAtStart);

            if (autoAcquireTransferPermissionLocNameAtEnd == "")
                sb.Append(", AutoAcquire");
            else if (autoAcquireTransferPermissionLocNameAtEnd != null)
                sb.CheckedAppendFormat(", AutoAcquire:{0}", autoAcquireTransferPermissionLocNameAtEnd);

            sb.Append(")");

            return new BasicActionImpl(actionQ, ipf => PerformRunProcess(ipf, substID, stepSpec, autoReleaseTransferPermissionLocNameAtStart, autoAcquireTransferPermissionLocNameAtEnd), sb.ToString(), ActionLoggingReference);
        }

        private string PerformRunProcess(IProviderFacet ipf, E039ObjectID substID, IProcessStepSpec stepSpec, string autoReleaseTransferPermissionLocNameAtStart, string autoAcquireTransferPermissionLocNameAtEnd)
        {
            if (autoReleaseTransferPermissionLocNameAtStart != null && autoReleaseTransferPermissionLocNameAtStart != LocID.Name && autoReleaseTransferPermissionLocNameAtStart != "")
                return "AutoReleaseTransferPermissionLocNameAtStart value '{0}' is not a valid process start location".CheckedFormat(autoReleaseTransferPermissionLocNameAtStart);

            if (autoAcquireTransferPermissionLocNameAtEnd != null && autoAcquireTransferPermissionLocNameAtEnd != LocID.Name && autoAcquireTransferPermissionLocNameAtEnd != "")
                return "AutoAcquireTransferPermissionLocNameAtEnd value '{0}' is not a valid process output location".CheckedFormat(autoAcquireTransferPermissionLocNameAtEnd);

            if (!BaseState.UseState.IsOnline(acceptOnlineFailure: false))
                return "Part is not online [{0}]".CheckedFormat(BaseState);

            if (!Object.ReferenceEquals(stepSpec, preparednessState.ProcessStepSpec))
                return "Part has not been prepared for this process [{0}]".CheckedFormat(preparednessState);

            if (!preparednessState.Query(stepSpec.ProcessSpec, stepSpec).SummaryState.IsSet(QuerySummaryState.Ready))
                return "Part is not ready to perform this process [{0}]".CheckedFormat(preparednessState);

            TimeSpan processTime = stepSpec.StepVariables["ProcessTime"].VC.GetValueTS(rethrow: false);

            if (autoReleaseTransferPermissionLocNameAtStart != null)
            {
                transferPermissionState.GrantedTokenSet.Remove(autoReleaseTransferPermissionLocNameAtStart);
                UpdateTransferPermission();
            }

            if (isLocationLockedForTransfer)
                return "Cannot run process: module is locked for transfer [{0}]".CheckedFormat(transferPermissionState);

            substID = substID.MapNullToEmpty();

            var substObserver = new E090SubstObserver(substID);

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
                SubstObserver = substObserver,
                ProcssStepSpec = stepSpec,
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
            public E090SubstObserver SubstObserver { get; set; }
            public IProcessStepSpec ProcssStepSpec { get; set; }
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

            if (entryEngineState == EngineState.RunningProcess && nextEngineState != EngineState.RunningProcess && currentProcessTracker != null)
            {
                var pendingSPS = currentProcessTracker.IPF.NamedParamValues["PendingSPS"].VC.GetValue(rethrow: false, defaultValue: SubstProcState.ProcessStepCompleted);
                var resultCode = currentProcessTracker.IPF.NamedParamValues["ResultCode"].VC.GetValueA(rethrow: false).MapNullToEmpty();
                var setSJRS = currentProcessTracker.IPF.NamedParamValues["SetSJRS"].VC.GetValue<SubstrateJobRequestState>(rethrow: false);

                E039TableUpdater.SetPendingSubstProcState(currentProcessTracker.SubstObserver, pendingSPS);

                if (setSJRS != SubstrateJobRequestState.None && TestECSParts.Scheduler != null)
                    TestECSParts.Scheduler.SetSJRS(setSJRS, currentProcessTracker.SubstObserver.ID).Run((5.0).FromSeconds());

                currentProcessTracker.CompleteRequest(resultCode);

                if (currentProcessTracker.AutoAcquireTransferPermissionLocNameAtEnd != null)
                {
                    transferPermissionState.GrantedTokenSet.Add(currentProcessTracker.AutoAcquireTransferPermissionLocNameAtEnd);
                    UpdateTransferPermission();
                }

                currentProcessTracker = null;

                CompletePendingAcquireRequests();
            }

            if (engineState == EngineState.RunningProcess && currentProcessTracker != null)
            {
                E039TableUpdater.SetPendingSubstProcState(currentProcessTracker.SubstObserver, SubstProcState.InProcess);

                var requestHoldForSubstrateID = currentProcessTracker.ProcssStepSpec.StepVariables["RequestHoldForSubstrateID"].VC.GetValueA(rethrow: false);

                if (requestHoldForSubstrateID.IsNeitherNullNorEmpty() && requestHoldForSubstrateID == locObserver.ContainsSubstInfo.ObjID.Name)
                {
                    Log.Debug.Emit("Requesting hold at start of process for '{0}'", requestHoldForSubstrateID);

                    preparednessState.SummaryState |= QuerySummaryState.HoldInboundMaterial;
                    preparednessState.Reason = "RequestHoldForSubstrateID triggered";

                    PublishPreparednessState();
                }
            }
        }

        private void CompletePendingAcquireRequests(bool publish = true, string resultCode = "")
        {
            if (pendingAcquireKVPList.Count > 0)
            {
                if (resultCode.IsNullOrEmpty())
                    pendingAcquireKVPList.DoForEach(kvp => transferPermissionState.GrantedTokenSet.Add(kvp.Key));

                UpdateTransferPermission(publish: publish);

                pendingAcquireKVPList.DoForEach(kvp => kvp.Value.CompleteRequest(resultCode));

                pendingAcquireKVPList.Clear();
            }
        }

        protected override void PerformMainLoopService()
        {
            locObserver.Update();

            if (currentProcessTracker != null && currentProcessTracker.IPF.ActionState.IsComplete)
                Fail("Found current ProcessTracker is complete while {0}".CheckedFormat(engineState));

            if (!BaseState.UseState.IsOnline(acceptOnlineFailure: false))
            {
                string baseStateFaultReason = BaseState.ExplicitFaultReason.MapEmptyToNull() ?? BaseState.Reason;
                if (transferPermissionState.IsAvailable() || transferPermissionState.Reason != baseStateFaultReason)
                {
                    transferPermissionState.SetState(TransferPermissionSummaryStateCode.NotAvailable, baseStateFaultReason, emitter: Log.Debug);
                    PublishTransferPermissionStateIfNeeded();
                }

                if (preparednessState.SummaryState != (QuerySummaryState.NotAvailable | QuerySummaryState.CanPrepare))
                {
                    preparednessState.SetSummaryState(QuerySummaryState.NotAvailable | QuerySummaryState.CanPrepare, "Part is not Online: {0}".CheckedFormat(BaseState));
                    PublishPreparednessState();
                }

                return;
            }
            else if (preparednessState.SummaryState.IsSet(QuerySummaryState.NotAvailable))
            {
                preparednessState.SetSummaryState(QuerySummaryState.CanPrepare, "Part is Online now: {0}".CheckedFormat(BaseState));
                PublishPreparednessState();
            }

            var engineStateAge = engineStateTS.Age;

            switch (engineState)
            {
                case EngineState.Idle:
                    if (!transferPermissionState.IsAvailable())
                    {
                        transferPermissionState.SetState(TransferPermissionSummaryStateCode.Available, engineState.ToString(), emitter: Log.Debug);
                        PublishTransferPermissionStateIfNeeded();
                    }
                    break;

                case EngineState.RunningProcess:
                    if (currentProcessTracker != null)
                    {
                        if (transferPermissionState.SummaryStateCode == TransferPermissionSummaryStateCode.Available)
                        {
                            transferPermissionState.SetState(TransferPermissionSummaryStateCode.Busy, engineState.ToString(), emitter: Log.Debug);
                            PublishTransferPermissionStateIfNeeded();
                        }

                        if ((engineStateAge >= currentProcessTracker.ProcessTime - ProcessAlmostCompleteLeadPeriod) && transferPermissionState.SummaryStateCode == TransferPermissionSummaryStateCode.Busy && EnableAlmostAvailable)
                        {
                            transferPermissionState.SetState(TransferPermissionSummaryStateCode.AlmostAvailable, engineState.ToString(), emitter: Log.Debug, estimatedAvailableAfterPeriod: ProcessAlmostCompleteLeadPeriod);
                            PublishTransferPermissionStateIfNeeded();
                        }

                        if (engineStateAge >= currentProcessTracker.ProcessTime)
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

            preparednessState.SetSummaryState(QuerySummaryState.NotAvailable | QuerySummaryState.CanPrepare, "Failed: {0}".CheckedFormat(reason));
            PublishPreparednessState();

            CompletePendingAcquireRequests(resultCode: reason);

            transferPermissionState.SetState(TransferPermissionSummaryStateCode.NotAvailable, "{0}({1})".CheckedFormat(CurrentMethodName, reason), emitter: Log.Debug);
            PublishTransferPermissionStateIfNeeded();

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

        IClientFacet RunProcess(E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS = SubstProcState.ProcessStepCompleted, string autoReleaseTransferPermissionLocNameAtStart = null, string autoAcquireTransferPermissionLocNameAtEnd = null);

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
        public BeltExampleProcessModuleEngine(string partID, IE039TableUpdater e039TableUpdater, int numStations = 4, TimeSpan? beltMoveTime = null, string locBaseName = null, BeltExampleProcessModuleEngineType engineType = BeltExampleProcessModuleEngineType.Linear, bool enableAlmostAvailable = true)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(waitTimeLimit: (0.01).FromSeconds()))
        {
            EngineType = engineType;

            int minValidStation = IsCircular ? 2 : 3;
            if (numStations < minValidStation)
                new System.ArgumentException(paramName: "numStations", message: "value must be {0} or larger for {1} engine".CheckedFormat(minValidStation, engineType)).Throw();

            E039TableUpdater = e039TableUpdater;
            NumStations = numStations;
            BeltMoveTime = beltMoveTime ?? (0.1).FromSeconds();
            BeltMoveAlmostCompleteLeadPeriod = (0.05).FromSeconds();
            NumProcessStations = IsCircular ? (NumStations - 1) : (NumStations - 2);
            EnableAlmostAvailable = enableAlmostAvailable;

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

            transferPermissionStateIVA = Values.Instance.GetValueAccessor(PartID + ".TPS").Set(lastPublishedTransferPermissionState);
        }

        public BeltExampleProcessModuleEngineType EngineType { get; private set; }
        public IE039TableUpdater E039TableUpdater { get; private set; }
        public int NumStations { get; private set; }
        public TimeSpan BeltMoveTime { get; private set; }
        public TimeSpan BeltMoveAlmostCompleteLeadPeriod { get; private set; }
        public int NumProcessStations { get; private set; }

        public bool EnableAlmostAvailable { get; private set; }

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

        public INotificationObject<ITransferPermissionState> TransferPermissionStatePublisher { get { return transferPermissionStatePublisher; } }
        private InterlockedNotificationRefObject<ITransferPermissionState> transferPermissionStatePublisher = new InterlockedNotificationRefObject<ITransferPermissionState>(TransferPermissionStateForPublication.Empty);

        ITransferPermissionState lastPublishedTransferPermissionState = TransferPermissionStateForPublication.Empty;
        TransferPermissionState transferPermissionState = new TransferPermissionState();

        IValueAccessor transferPermissionStateIVA;

        bool isInputLocationLockedForTransfer, isOutputLocationLockedForTransfer;

        private void PublishTransferPermissionStateIfNeeded()
        {
            if (!transferPermissionState.Equals(lastPublishedTransferPermissionState))
            {
                bool justBecameAvailable = (transferPermissionState.IsAvailable() && !lastPublishedTransferPermissionState.IsAvailable());

                transferPermissionStatePublisher.Object = (lastPublishedTransferPermissionState = new TransferPermissionStateForPublication(transferPermissionState));
                transferPermissionStateIVA.Set(lastPublishedTransferPermissionState);

                Log.Debug.Emit("Published {0}", lastPublishedTransferPermissionState);

                if (justBecameAvailable && pendingAcquireKVPList.Count > 0)
                {
                    CompletePendingAcquireRequests(publish: false);

                    transferPermissionStatePublisher.Object = (lastPublishedTransferPermissionState = new TransferPermissionStateForPublication(transferPermissionState));
                    transferPermissionStateIVA.Set(lastPublishedTransferPermissionState);

                    Log.Debug.Emit("Published {0} [after completing pending work]", lastPublishedTransferPermissionState);
                }
            }
        }

        public IClientFacet RunProcess(E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS = SubstProcState.ProcessStepCompleted, string autoReleaseTransferPermissionLocNameAtStart = null, string autoAcquireTransferPermissionLocNameAtEnd = null)
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
                    if (transferPermissionState.SummaryStateCode == TransferPermissionSummaryStateCode.AlmostAvailable || transferPermissionState.SummaryStateCode == TransferPermissionSummaryStateCode.Busy)
                    {
                        pendingAcquireKVPList.Add(KVP.Create(locName, ipf));

                        Log.Debug.Emit("TransferPermission state is {0}: request '{1}' has been put into the pending list", transferPermissionState, ipf.ToString(ToStringSelect.MesgAndDetail));

                        return null;
                    }

                    if (!transferPermissionState.SummaryStateCode.IsAvailable())
                    {
                        return "TransferPermissionInterface is not available [{0}]".CheckedFormat(transferPermissionState);
                    }

                    transferPermissionState.GrantedTokenSet.Add(locName);
                    break;

                case TransferPermissionRequestType.Release:
                    transferPermissionState.GrantedTokenSet.Remove(locName);
                    break;

                case TransferPermissionRequestType.ReleaseAll:
                    transferPermissionState.GrantedTokenSet.Clear();
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
            isInputLocationLockedForTransfer = transferPermissionState.GrantedTokenSet.Contains(InputLocID.Name) || (IsCircular && transferPermissionState.GrantedTokenSet.Contains(""));
            isOutputLocationLockedForTransfer = IsCircular ? isInputLocationLockedForTransfer : transferPermissionState.GrantedTokenSet.Contains(OutputLocID.Name);

            if (publish)
                PublishTransferPermissionStateIfNeeded();
        }

        private string PerformRunProcess(IProviderFacet ipf, E039ObjectID substID, IProcessStepSpec stepSpec, SubstProcState resultingSPS, string autoReleaseTransferPermissionLocNameAtStart, string autoAcquireTransferPermissionLocNameAtEnd)
        {
            if (autoReleaseTransferPermissionLocNameAtStart != null && autoReleaseTransferPermissionLocNameAtStart != InputLocID.Name && !(IsCircular && autoReleaseTransferPermissionLocNameAtStart == ""))
                return "AutoReleaseTransferPermissionLocNameAtStart value '{0}' is not a valid process start location".CheckedFormat(autoReleaseTransferPermissionLocNameAtStart);

            if (autoAcquireTransferPermissionLocNameAtEnd != null && autoAcquireTransferPermissionLocNameAtEnd != OutputLocID.Name && !(IsCircular && autoAcquireTransferPermissionLocNameAtEnd == ""))
                return "AutoAcquireTransferPermissionLocNameAtEnd value '{0}' is not a valid process output location".CheckedFormat(autoAcquireTransferPermissionLocNameAtEnd);

            TimeSpan stepInterval = stepSpec.StepVariables["StepInterval"].VC.GetValueTS(rethrow: false);

            if (!BaseState.UseState.IsOnline(acceptOnlineFailure: false))
                return "Part is not online [{0}]".CheckedFormat(BaseState);

            if (autoReleaseTransferPermissionLocNameAtStart != null)
            {
                transferPermissionState.GrantedTokenSet.Remove(autoReleaseTransferPermissionLocNameAtStart);
                UpdateTransferPermissions();
            }

            substID = substID.MapNullToEmpty();

            var substObserver = new E090SubstObserver(substID);

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
                        E039TableUpdater.SetPendingSubstProcState(pt.SubstObserver, SubstProcState.ProcessStepCompleted.MergeWith(pt.ResultingSPS));
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
                            transferPermissionState.GrantedTokenSet.Add(ptAtPickLoc.AutoAcquireTransferPermissionLocNameAtEnd);
                            UpdateTransferPermissions();
                        }
                    }

                    CompletePendingAcquireRequests();
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

        private void CompletePendingAcquireRequests(bool publish = true)
        {
            if (pendingAcquireKVPList.Count > 0)
            {
                pendingAcquireKVPList.DoForEach(kvp => transferPermissionState.GrantedTokenSet.Add(kvp.Key));

                UpdateTransferPermissions(publish: publish);

                pendingAcquireKVPList.DoForEach(kvp => kvp.Value.CompleteRequest(""));

                pendingAcquireKVPList.Clear();
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
            {
                if (transferPermissionState.IsAvailable() || transferPermissionState.Reason != BaseState.Reason)
                {
                    transferPermissionState.SetState(TransferPermissionSummaryStateCode.NotAvailable, BaseState.Reason, emitter: Log.Debug);
                    PublishTransferPermissionStateIfNeeded();
                }

                return;
            }

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

            TimeSpan engineStateAge = engineStateTS.Age;

            switch (engineState)
            {
                case EngineState.Waiting:
                    if (!transferPermissionState.IsAvailable())
                    {
                        transferPermissionState.SetState(TransferPermissionSummaryStateCode.Available, engineState.ToString());
                        PublishTransferPermissionStateIfNeeded();
                    }

                    if (isInputLocationLockedForTransfer || isOutputLocationLockedForTransfer)
                        break;     // keep waiting until the load and unload stations have been released

                    if ((stationWaferCount <= 0) || havePendingWaferAtInput || haveProcessedWaferAtOutput)
                        break;     // keep waiting until we have at least one wafer available for process or in process

                    SetEngineState(EngineState.MovingBelt, "Starting Belt move and then next process step");

                    transferPermissionState.SetState(TransferPermissionSummaryStateCode.Busy, engineState.ToString());
                    PublishTransferPermissionStateIfNeeded();

                    break;

                case EngineState.MovingBelt:
                    if (transferPermissionState.SummaryStateCode == TransferPermissionSummaryStateCode.Available)
                    {
                        transferPermissionState.SetState(TransferPermissionSummaryStateCode.Busy, engineState.ToString());
                        PublishTransferPermissionStateIfNeeded();
                    }

                    if ((engineStateAge >= BeltMoveTime - BeltMoveAlmostCompleteLeadPeriod) && (transferPermissionState.SummaryStateCode == TransferPermissionSummaryStateCode.Busy) && EnableAlmostAvailable)
                    {
                        transferPermissionState.SetState(TransferPermissionSummaryStateCode.AlmostAvailable, engineState.ToString(), estimatedAvailableAfterPeriod: BeltMoveAlmostCompleteLeadPeriod);
                        PublishTransferPermissionStateIfNeeded();
                    }

                    if (engineStateAge >= BeltMoveTime)
                    {
                        beltPosition = (beltPosition + 1) % NumStations;
                        SetEngineState(EngineState.RunningProcess, "Belt move completed");
                    }
 
                    break;

                case EngineState.RunningProcess:
                    if (!transferPermissionState.IsAvailable())
                    {
                        transferPermissionState.SetState(TransferPermissionSummaryStateCode.Available, engineState.ToString());
                        PublishTransferPermissionStateIfNeeded();
                    }

                    if (engineStateAge >= maxStepInterval)
                    {
                        SetEngineState(EngineState.Waiting, "Process complete");
                    }
                    break;
            }
        }

        private void Fail(string reason)
        {
            SetBaseState(UseState.OnlineFailure, reason);

            transferPermissionState.SetState(TransferPermissionSummaryStateCode.NotAvailable, "{0}({1})".CheckedFormat(CurrentMethodName, reason), emitter: Log.Debug);
            PublishTransferPermissionStateIfNeeded();

            ptBySubstFullNameDictionary.ValueArray.DoForEach(pt => pt.CompleteRequest(reason));
            ptBySubstFullNameDictionary.Clear();
        }
    }

    #endregion
}
