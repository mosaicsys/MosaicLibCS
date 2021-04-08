//-------------------------------------------------------------------
/*! @file E084ActiveTransferSimEngine.cs
 *  @brief an active part that support simulation of an OHT attached to a load port.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2011 Mosaic Systems Inc.
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
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Helpers;
using MosaicLib.Semi.E084;

using MosaicLib.PartsLib.Common.LPM;
using LPM = MosaicLib.PartsLib.Common.LPM;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Common;

namespace MosaicLib.PartsLib.Common.E084
{
    public interface IE084ActiveTransferSimEngine : IActivePartBase
    {
        E084ActiveTransferSimEngineState State { get; }
        INotificationObject<E084ActiveTransferSimEngineState> StateNotifier { get; }

        IBasicAction CreateStartResetAction();
        IBasicAction CreateStartSingleLoadAction();
        IBasicAction CreateStartSingleUnloadAction();
    }

    public struct E084ActiveTransferSimEngineState
    {
        public string StateStr { get; set; }
        public string NotReadyReason { get; set; }
        public string NotReadyToLoadReason { get; set; }
        public string NotReadyToUnloadReason { get; set; }
        public bool IsReady { get { return NotReadyReason.IsNullOrEmpty(); } }
        public bool IsReadyToLoad { get { return NotReadyToLoadReason.IsNullOrEmpty(); } }
        public bool IsReadyToUnload { get { return NotReadyToUnloadReason.IsNullOrEmpty(); } }
        public bool IsCycling { get; set; }
        public string TransferProgressStr { get; set; }
        public UInt64 TransferCount { get; set; }

        [Obsolete("This struct copy constructor is vestigial and will be removed (2018-10-15)")]
        public E084ActiveTransferSimEngineState(E084ActiveTransferSimEngineState other) : this()
        {
            StateStr = other.StateStr;
            NotReadyReason = other.NotReadyReason;
            NotReadyToLoadReason = other.NotReadyToLoadReason;
            NotReadyToUnloadReason = other.NotReadyToUnloadReason;
            IsCycling = other.IsCycling;
            TransferProgressStr = other.TransferProgressStr;
            TransferCount = other.TransferCount;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object otherObj)
        {
            if (otherObj == null || !(otherObj is E084ActiveTransferSimEngineState))
                return false;

            E084ActiveTransferSimEngineState other = (E084ActiveTransferSimEngineState)otherObj;

            return (StateStr == other.StateStr
                    && NotReadyReason == other.NotReadyReason
                    && NotReadyToLoadReason == other.NotReadyToLoadReason
                    && NotReadyToUnloadReason == other.NotReadyToUnloadReason
                    && IsCycling == other.IsCycling
                    && TransferProgressStr == other.TransferProgressStr
                    && TransferCount == other.TransferCount);
        }
    }

    public class E084ActiveTransferSimEngine : SimpleActivePartBase, IE084ActiveTransferSimEngine
    {
        #region Construction

        public E084ActiveTransferSimEngine(LPM.Sim.ILPMSimPart lpmSimPart)
            : base(lpmSimPart.PartID + ".E84Sim", "E084ActiveTransferSimEngine", initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior: true))
        {
            ActionLoggingConfig = Modular.Action.ActionLoggingConfig.Info_Error_Trace_Trace;    // redefine the log levels for actions 

            //This part is a simulated primary part
            PrivateBaseState = new BaseState(true, true) { PartID = PartID, ConnState = ConnState.NotApplicable };

            this.lpmSimPart = lpmSimPart;

            IVI = Modular.Interconnect.Values.Values.Instance;

            statePublisherIVA = IVI.GetValueAccessor("{0}.State".CheckedFormat(PartID));

            instantActionQ = new MosaicLib.Modular.Action.ActionQueue(PartID + ".iq", true, 10);

            enableAutoLoadIVA = IVI.GetValueAccessor("{0}.EnableAutoLoad".CheckedFormat(PartID));
            enableAutoUnloadIVA = IVI.GetValueAccessor("{0}.EnableAutoUnload".CheckedFormat(PartID));

            string lpmPartBaseName = lpmSimPart.PartID;
            lpmPresentPlacedInputIVA = IVI.GetValueAccessor("{0}.PresentPlacedInput".CheckedFormat(lpmPartBaseName));
            ohtPassiveToActivePinsStateIVA = IVI.GetValueAccessor("{0}.E84.OHT.PassiveToActivePinsState".CheckedFormat(lpmPartBaseName));
            agvPassiveToActivePinsStateIVA = IVI.GetValueAccessor("{0}.E84.AGV.PassiveToActivePinsState".CheckedFormat(lpmPartBaseName));
            ohtActiveToPassivePinsStateIVA = IVI.GetValueAccessor("{0}.E84.OHT.ActiveToPassivePinsState".CheckedFormat(lpmPartBaseName));
            agvActiveToPassivePinsStateIVA = IVI.GetValueAccessor("{0}.E84.AGV.ActiveToPassivePinsState".CheckedFormat(lpmPartBaseName));

            SetupConfig();

            PublishBaseState("Constructor.Complete");
        }

        #endregion

        #region config

        public class ConfigValues
        {
            [ConfigItem]
            public TimeSpan LoadUnloadStartHoldoff = TimeSpan.FromSeconds(2.0);

            [ConfigItem]
            public TimeSpan LoweringTime = TimeSpan.FromSeconds(2.0);

            [ConfigItem]
            public TimeSpan LoadPlacementTransitionTime = TimeSpan.FromSeconds(0.5);

            [ConfigItem]
            public TimeSpan UnloadPlacementTransitionTime = TimeSpan.FromSeconds(0.5);

            [ConfigItem]
            public TimeSpan RaisingTime = TimeSpan.FromSeconds(2.0);

            [ConfigItem]
            public TimeSpan LoadDeselectDelay = TimeSpan.FromSeconds(1.25);     // long enough to help detect issues with concurrent clamping, short enough to prevent tripping TP5 when set to 2 seconds.

            [ConfigItem]
            public TimeSpan UnloadDeselectDelay = TimeSpan.FromSeconds(0.1);
        }

        ConfigValues configValues = new ConfigValues();
        ConfigValueSetAdapter<ConfigValues> configAdapter;

        void SetupConfig()
        {
            configAdapter = new ConfigValueSetAdapter<ConfigValues>() { ValueSet = configValues, SetupIssueEmitter = Log.Debug, UpdateIssueEmitter = Log.Debug, ValueNoteEmitter = Log.Debug }.Setup("{0}.".CheckedFormat(PartID));
            configAdapter.UpdateNotificationList.AddItem(this);
            AddExplicitDisposeAction(() => configAdapter.UpdateNotificationList.RemoveItem(this));
        }

        void ServiceConfig()
        {
            if (configAdapter.IsUpdateNeeded)
                configAdapter.Update();
        }

        #endregion

        #region local fields and IVA support

        private LPM.Sim.ILPMSimPart lpmSimPart;
        private PIOSelect pioSelect = PIOSelect.OHT;
        private IValuesInterconnection IVI { get; set; }

        private IValueAccessor enableAutoLoadIVA;
        private IValueAccessor enableAutoUnloadIVA;

        private IValueAccessor ohtPassiveToActivePinsStateIVA;
        private IValueAccessor agvPassiveToActivePinsStateIVA;
        private IValueAccessor ohtActiveToPassivePinsStateIVA;
        private IValueAccessor agvActiveToPassivePinsStateIVA;

        private IValueAccessor SelectedPassiveToActivePinsStateIVA { get { return (pioSelect == PIOSelect.OHT ? ohtPassiveToActivePinsStateIVA : agvPassiveToActivePinsStateIVA); } }
        private IValueAccessor SelectedActiveToPassivePinsStateIVA { get { return (pioSelect == PIOSelect.OHT ? ohtActiveToPassivePinsStateIVA : agvActiveToPassivePinsStateIVA); } }

        private IValueAccessor lpmPresentPlacedInputIVA;

        private PresentPlaced LPMPresentPlacedInput
        {
            get { return lpmPresentPlacedInputIVA.Update().VC.GetValue<PresentPlaced>(false); }
            set { lpmPresentPlacedInputIVA.Set(new ValueContainer(value)); }
        }

        #endregion

        #region public state and interface methods

        E084ActiveTransferSimEngineState privateState;
        GuardedNotificationValueObject<E084ActiveTransferSimEngineState> publicStateNotifier = new GuardedNotificationValueObject<E084ActiveTransferSimEngineState>(new E084ActiveTransferSimEngineState());
        public INotificationObject<E084ActiveTransferSimEngineState> StateNotifier { get { return publicStateNotifier; } }
        public E084ActiveTransferSimEngineState State { get { return publicStateNotifier.Object; } }

        private IValueAccessor statePublisherIVA;

        protected void PublishPrivateState()
        {
            publicStateNotifier.Object = privateState;
            statePublisherIVA.Set(privateState);
        }

        public IBasicAction CreateStartResetAction()
        {
            return new BasicActionImpl(instantActionQ, PerformStartReset, "Reset", ActionLoggingReference) as IBasicAction;
        }

        public IBasicAction CreateStartSingleLoadAction()
        {
            return new BasicActionImpl(instantActionQ, PerformSingleLoad, "Load", ActionLoggingReference) as IBasicAction;
        }

        public IBasicAction CreateStartSingleUnloadAction()
        {
            return new BasicActionImpl(instantActionQ, PerformSingleUnload, "Unload", ActionLoggingReference) as IBasicAction;
        }

        #endregion

        #region Instant Action Q and related methods

        protected Modular.Action.ActionQueue instantActionQ;

        protected bool ServiceInstantActionQueue()
        {
            int actionCount = 0;
            for (; actionCount < 10 && !instantActionQ.IsEmpty && instantActionQ.QueueEnable; actionCount++)
            {
                Modular.Action.IProviderFacet action = instantActionQ.GetNextAction();
                if (action != null)
                    PerformAction(action);
            }

            return (actionCount > 0);
        }

        #endregion

        #region Internal implementation

        enum ActivitySelect : byte
        {
            None = 0,
            Offline,
            Reset,
            WaitForPinsReady,
            Ready,
            PerformLoad,
            PerformUnload,
        }

        ActivitySelect currentActivity = ActivitySelect.Offline;
        ActivitySelect nextActivitySelect = ActivitySelect.None;
        string abortCurrentActivityReason = null;

        private bool IsActive { get { return (currentActivity != ActivitySelect.Offline && currentActivity != ActivitySelect.Ready); } }
        private bool IsReady { get { return (currentActivity == ActivitySelect.Ready); } }

        void UpdateReadyToLoadAndUnload(bool forceUpdate, bool publishIfNeeded)
        {
            PresentPlaced presentPlacedInput = lpmPresentPlacedInputIVA.Update().VC.GetValue<PresentPlaced>(false);

            if (lastPresentPlacedInput != presentPlacedInput || forceUpdate)
            {
                var isReady = privateState.NotReadyReason.IsNullOrEmpty();
                var isReadyToLoad = isReady && presentPlacedInput.IsNeitherPresentNorPlaced();
                var isReadyToUnload = isReady && presentPlacedInput.IsProperlyPlaced();
                privateState.NotReadyToLoadReason = isReadyToLoad ? "" : (isReady ? "Must be Empty: {0}".CheckedFormat(presentPlacedInput) : privateState.NotReadyReason);
                privateState.NotReadyToUnloadReason = isReadyToUnload ? "" : (isReady ? "Must be properly placed: {0}".CheckedFormat(presentPlacedInput) : privateState.NotReadyReason);

                lastPresentPlacedInput = presentPlacedInput;

                if (publishIfNeeded)
                    PublishPrivateState();
            }
        }

        PresentPlaced lastPresentPlacedInput = PresentPlaced.None;

        void SetCurrentActivity(ActivitySelect activity, string reason)
        {
            ActivitySelect entryActivity = currentActivity;

            Log.Debug.Emit("SetCurrentActivity changing activity to:{0} [from:{1}, reason:'{2}']", activity, currentActivity, reason);
            currentActivity = activity;

            privateState.StateStr = (Utils.Fcns.CheckedFormat("{0} [{1}]", activity, reason));
            privateState.IsCycling = (enableAutoLoadIVA.Update().VC.GetValue<bool>(false) && enableAutoUnloadIVA.Update().VC.GetValue<bool>(false));

            privateState.NotReadyReason = ((currentActivity == ActivitySelect.Ready) ? "" : currentActivity.ToString());
            UpdateReadyToLoadAndUnload(true, false);

            if (entryActivity != currentActivity)
            {
                privateState.TransferProgressStr = String.Empty;
                if (!privateState.IsCycling)
                    privateState.TransferCount = 0;
            }

            bool busy = false;
            switch (currentActivity)
            {
                case ActivitySelect.PerformLoad:
                case ActivitySelect.PerformUnload:
                case ActivitySelect.Reset:
                    busy = true;
                    break;
                default:
                    break;
            }

            // update PrivateBaseState now.
            if (currentActivity == ActivitySelect.Offline)
            {
                if (!PrivateBaseState.IsOnline)
                {
                    if (entryActivity == ActivitySelect.Ready || entryActivity == ActivitySelect.None)
                        SetBaseState(UseState.Offline, reason, true);
                    else
                        SetBaseState(UseState.FailedToOffline, reason, true);
                }
            }
            else if (!PrivateBaseState.IsOnline)
                SetBaseState(UseState.OnlineBusy, reason, true);
            else if (!PrivateBaseState.IsBusy && busy)
                SetBaseState(UseState.OnlineBusy, reason, true);
            else if (PrivateBaseState.IsBusy && !busy)
                SetBaseState(UseState.Online, reason, true);

            PublishPrivateState();
        }

        protected override string PerformGoOfflineAction()
        {
            SetBaseState(UseState.Offline, "GoOffline Action has been performed", true);

            SetCurrentActivity(ActivitySelect.Offline, "Performing GoOffline Action");

            nextActivitySelect = ActivitySelect.None;
            lastSetA2PPins = new ActiveToPassivePinsState();
            lastSetA2PPins.IFaceName = PartID;
            lastSetA2PPins.XferILock = true;
            ohtActiveToPassivePinsStateIVA.Set(lastSetA2PPins as IActiveToPassivePinsState);
            agvActiveToPassivePinsStateIVA.Set(lastSetA2PPins as IActiveToPassivePinsState);

            privateState = new E084ActiveTransferSimEngineState() { StateStr = "Offline" };

            PublishPrivateState();

            return String.Empty;
        }

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            SetBaseState(UseState.Online, "GoOnline Action has been performed", true);

            if (currentActivity == ActivitySelect.Offline)
                SetCurrentActivity(ActivitySelect.None, "Performing GoOnline Action");

            if (andInitialize)
                nextActivitySelect = ActivitySelect.Reset;

            return String.Empty;
        }

        protected override void PreStartPart()
        {
            if (instantActionQ != null)
            {
                instantActionQ.NotifyOnEnqueue.OnNotify += this.Notify;
                instantActionQ.QueueEnable = true;
            }

            base.PreStartPart();
        }

        protected override void PreStopPart()
        {
            if (instantActionQ != null)
            {
                instantActionQ.QueueEnable = false;
                instantActionQ.NotifyOnEnqueue.OnNotify -= this.Notify;
            }

            base.PreStopPart();
        }

        private string PerformStartReset()
        {
            nextActivitySelect = ActivitySelect.Reset;
            if (IsActive)
                abortCurrentActivityReason = "Reset has been requested";

            return String.Empty;
        }

        private string PerformSingleLoad()
        {
            if (currentActivity != ActivitySelect.Ready)
                return Utils.Fcns.CheckedFormat("Load Request not permitted, engine is not ready [{0}]", currentActivity);

            nextActivitySelect = ActivitySelect.PerformLoad;
            return String.Empty;
        }

        private string PerformSingleUnload()
        {
            if (currentActivity != ActivitySelect.Ready)
                return Utils.Fcns.CheckedFormat("Unload Request not permitted, engine is not ready [{0}]", currentActivity);

            nextActivitySelect = ActivitySelect.PerformUnload;
            return String.Empty;
        }

        protected override void PerformMainLoopService()
        {
            Spin();

            // service current activity
            switch (currentActivity)
            {
                default:
                case ActivitySelect.Offline:
                    break;  // nothing to do
                case ActivitySelect.None:
                    ServiceDispatchNextActivity();
                    break;
                case ActivitySelect.Reset:
                    ServiceResetActivity();
                    break;
                case ActivitySelect.WaitForPinsReady:
                    ServiceWaitForPinsReadyActivity();
                    break;
                case ActivitySelect.Ready:
                    ServiceReadyActivity();
                    if (currentActivity == ActivitySelect.Ready)
                        ServiceDispatchNextActivity();
                    break;
                case ActivitySelect.PerformLoad:
                    ServicePeformLoadActivity();
                    break;
                case ActivitySelect.PerformUnload:
                    ServicePerformUnloadActivity();
                    break;
            }
        }

        void ServiceDispatchNextActivity()
        {
            if (nextActivitySelect == ActivitySelect.None)
                return;

            Log.Debug.Emit("Processing nextActivity:{0}", nextActivitySelect);

            SetCurrentActivity(nextActivitySelect, "Starting new activity");
            nextActivitySelect = ActivitySelect.None;
        }

        private IConfig config = Config.Instance;

        void ServiceResetActivity()
        {
            using (var eeLog = new Logging.EnterExitTrace(Log))
            {
                // clear the enable auto load and enable auto unload values

                if (enableAutoLoadIVA.Update().VC.GetValue<bool>(false))
                    enableAutoLoadIVA.Set(false);

                if (enableAutoUnloadIVA.Update().VC.GetValue<bool>(false))
                    enableAutoUnloadIVA.Set(false);

                IValueAccessor a2pPinsStateIVA = SelectedActiveToPassivePinsStateIVA;
                IValueAccessor p2aPinsStateIVA = SelectedPassiveToActivePinsStateIVA;

                IActiveToPassivePinsState a2pPinsState = new ActiveToPassivePinsState(a2pPinsStateIVA.Update().VC);
                IPassiveToActivePinsState p2aPinsState = new PassiveToActivePinsState(p2aPinsStateIVA.Update().VC);

                if (!a2pPinsState.IsIdle)
                {
                    lastSetA2PPins = new ActiveToPassivePinsState();
                    lastSetA2PPins.IFaceName = PartID;
                    lastSetA2PPins.XferILock = true;
                    a2pPinsStateIVA.Set(lastSetA2PPins as IActiveToPassivePinsState);
                }

                if (config.GetConfigKeyAccessOnce("E84Sim.ResetForcesESandHO").GetValue<bool>(false) && !p2aPinsState.IsSelectable)
                {
                    p2aPinsStateIVA.Set(new PassiveToActivePinsState(p2aPinsState) { ES = true, HO_AVBL = true } as IPassiveToActivePinsState);
                }

                Spin(TimeSpan.FromSeconds(0.5));

                a2pPinsState = new ActiveToPassivePinsState(a2pPinsStateIVA.Update().VC);
                p2aPinsState = new PassiveToActivePinsState(p2aPinsStateIVA.Update().VC);

                if (!p2aPinsState.IsSelectable)
                    SetCurrentActivity(ActivitySelect.WaitForPinsReady, Utils.Fcns.CheckedFormat("Reset complete with E84 P->A pins not selectable [{0}]", p2aPinsState));
                else if (!a2pPinsState.IsIdle)
                    SetCurrentActivity(ActivitySelect.WaitForPinsReady, Utils.Fcns.CheckedFormat("Reset complete with E84 A->P pins not idle [{0}]", a2pPinsState));
                else
                    SetCurrentActivity(ActivitySelect.Ready, "Reset complete and Ready for select");
            }
        }

        void ServiceWaitForPinsReadyActivity()
        {
            IValueAccessor a2pPinsStateIVA = SelectedActiveToPassivePinsStateIVA;
            IValueAccessor p2aPinsStateIVA = SelectedPassiveToActivePinsStateIVA;

            IActiveToPassivePinsState a2pPinsState = new ActiveToPassivePinsState(a2pPinsStateIVA.Update().VC);
            IPassiveToActivePinsState p2aPinsState = new PassiveToActivePinsState(p2aPinsStateIVA.Update().VC);

            if (!a2pPinsState.IsIdle)
            {
                privateState.TransferProgressStr = Utils.Fcns.CheckedFormat("Wait A2P Idle: {0}", a2pPinsState);
            }
            else if (!p2aPinsState.IsSelectable)
            {
                privateState.TransferProgressStr = Utils.Fcns.CheckedFormat("Wait P2A Selectable: {0}", p2aPinsState);
            }
            else
            {
                SetCurrentActivity(ActivitySelect.Ready, "A2P pins are selectable now");
            }
        }

        void ServiceReadyActivity()
        {
            IValueAccessor a2pPinsStateIVA = SelectedActiveToPassivePinsStateIVA;
            IValueAccessor p2aPinsStateIVA = SelectedPassiveToActivePinsStateIVA;

            IActiveToPassivePinsState a2pPinsState = new ActiveToPassivePinsState(a2pPinsStateIVA.Update().VC);
            IPassiveToActivePinsState p2aPinsState = new PassiveToActivePinsState(p2aPinsStateIVA.Update().VC);

            if (!a2pPinsState.IsIdle)
            {
                SetCurrentActivity(ActivitySelect.WaitForPinsReady, Utils.Fcns.CheckedFormat("A2P pins are no longer idle [{0}]", a2pPinsState));
                return;
            }

            if (!p2aPinsState.IsSelectable)
            {
                SetCurrentActivity(ActivitySelect.WaitForPinsReady, Utils.Fcns.CheckedFormat("P2A pins are no longer selectable [{0}]", p2aPinsState));
                return;
            }

            // interface is idle and is selectable.  detect auto trigger conditions and automatically request next transition accordingly.

            UpdateReadyToLoadAndUnload(false, true);

            if (nextActivitySelect == ActivitySelect.None)
            {
                PresentPlaced presentPlacedInput = lpmPresentPlacedInputIVA.Update().VC.GetValue<PresentPlaced>(false);

                if (presentPlacedInput.IsNeitherPresentNorPlaced() && enableAutoLoadIVA.Update().VC.GetValue<bool>(false))
                {
                    if (loadUnloadStartHoldoffTimer.StartIfNeeded(configValues.LoadUnloadStartHoldoff).IsTriggered)
                    {
                        nextActivitySelect = ActivitySelect.PerformLoad;
                        loadUnloadStartHoldoffTimer.Stop();
                    }
                }
                else if (presentPlacedInput.IsProperlyPlaced() && enableAutoUnloadIVA.Update().VC.GetValue<bool>(false))
                {
                    if (loadUnloadStartHoldoffTimer.StartIfNeeded(configValues.LoadUnloadStartHoldoff).IsTriggered)
                    {
                        nextActivitySelect = ActivitySelect.PerformUnload;
                        loadUnloadStartHoldoffTimer.Stop();
                    }
                }
                else
                {
                    loadUnloadStartHoldoffTimer.StopIfNeeded();
                }
            }
            else
            {
                loadUnloadStartHoldoffTimer.StopIfNeeded();
            }
        }

        QpcTimer loadUnloadStartHoldoffTimer = new QpcTimer() { AutoReset = false };

        void ServicePeformLoadActivity()
        {
            PassiveToActivePinBits p2aPinBits = PassiveToActivePinBits.ES_pin8 | PassiveToActivePinBits.HO_AVBL_pin7;
            PassiveToActivePinBits nextP2APinBits = p2aPinBits;

            privateState.TransferCount++;
            privateState.TransferProgressStr = "Loading:Select for L_REQ";
            PublishPrivateState();
            SetA2PPins(new ActiveToPassivePinsState(idleA2PPins) { VALID = true, CS_0 = true });

            nextP2APinBits = p2aPinBits | PassiveToActivePinBits.L_REQ_pin1;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.L_REQ_pin1)) return; 

            privateState.TransferProgressStr = "Loading:Start T_REQ";
            PublishPrivateState();

            SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { TR_REQ = true });

            nextP2APinBits = p2aPinBits | PassiveToActivePinBits.READY_pin4;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.READY_pin4)) return;

            privateState.TransferProgressStr = "Loading:Go BUSY";
            PublishPrivateState();

            SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { BUSY = true });

            privateState.TransferProgressStr = "Loading:Sim Lowering";
            PublishPrivateState();

            if (!Spin(configValues.LoweringTime)) return;

            privateState.TransferProgressStr = "Loading:Placing Foup";
            PublishPrivateState();

            LPMPresentPlacedInput = PresentPlaced.Present;

            if (!Spin(configValues.LoadPlacementTransitionTime)) return;

            LPMPresentPlacedInput = PresentPlaced.Present | PresentPlaced.Placed;

            privateState.TransferProgressStr = "Loading:Wait L_REQ clear";
            PublishPrivateState();

            nextP2APinBits = p2aPinBits & ~PassiveToActivePinBits.L_REQ_pin1;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.L_REQ_pin1)) return;

            privateState.TransferProgressStr = "Loading:Sim Raising";
            PublishPrivateState();

            if (!Spin(configValues.RaisingTime)) return;

            privateState.TransferProgressStr = "Loading:Go COMPT";
            PublishPrivateState();

            SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { TR_REQ = false, BUSY = false, COMPT = true });

            privateState.TransferProgressStr = "Loading:Wait READY clear";
            PublishPrivateState();

            nextP2APinBits = p2aPinBits & ~PassiveToActivePinBits.READY_pin4;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.READY_pin4)) return;

            privateState.TransferProgressStr = "Loading:Deselecting";
            PublishPrivateState();

            if (!Spin(configValues.LoadDeselectDelay)) return;

            SetA2PPins(new ActiveToPassivePinsState(idleA2PPins));

            privateState.TransferProgressStr = String.Empty;
            PublishPrivateState();

            if (currentActivity == ActivitySelect.PerformLoad)
            {
                SetCurrentActivity(ActivitySelect.Ready, "Load complete");
            }
        }

        void ServicePerformUnloadActivity()
        {
            PassiveToActivePinBits p2aPinBits = PassiveToActivePinBits.ES_pin8 | PassiveToActivePinBits.HO_AVBL_pin7;
            PassiveToActivePinBits nextP2APinBits = p2aPinBits;

            privateState.TransferCount++;
            privateState.TransferProgressStr = "Unloading:Select for U_REQ";
            PublishPrivateState();
            SetA2PPins(new ActiveToPassivePinsState(idleA2PPins) { VALID = true, CS_0 = true });

            nextP2APinBits = p2aPinBits | PassiveToActivePinBits.U_REQ_pin2;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.U_REQ_pin2)) return;

            privateState.TransferProgressStr = "Unloading:Start T_REQ";
            PublishPrivateState();

            SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { TR_REQ = true });

            nextP2APinBits = p2aPinBits | PassiveToActivePinBits.READY_pin4;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.READY_pin4)) return;

            privateState.TransferProgressStr = "Unloading:Go BUSY";
            PublishPrivateState();

            SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { BUSY = true });

            privateState.TransferProgressStr = "Unloading:Sim Lowering";
            PublishPrivateState();

            if (!Spin(configValues.LoweringTime)) return;

            privateState.TransferProgressStr = "Unloading:Removing Foup";
            PublishPrivateState();

            LPMPresentPlacedInput = PresentPlaced.Present;

            if (!Spin(configValues.UnloadPlacementTransitionTime)) return;

            LPMPresentPlacedInput = PresentPlaced.None;

            privateState.TransferProgressStr = "Unloading:Wait U_REQ clear";
            PublishPrivateState();

            nextP2APinBits = p2aPinBits & ~PassiveToActivePinBits.U_REQ_pin2;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.U_REQ_pin2)) return;

            privateState.TransferProgressStr = "Unloading:Sim Raising";
            PublishPrivateState();

            if (!Spin(configValues.RaisingTime)) return;

            privateState.TransferProgressStr = "Unloading:Go COMPT";
            PublishPrivateState();

            SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { TR_REQ = false, BUSY = false, COMPT = true });

            privateState.TransferProgressStr = "Unloading:Wait READY clear";
            PublishPrivateState();

            nextP2APinBits = p2aPinBits & ~PassiveToActivePinBits.READY_pin4;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.READY_pin4)) return;

            privateState.TransferProgressStr = "Unloading:Deselecting";
            PublishPrivateState();

            if (!Spin(configValues.UnloadDeselectDelay)) return;

            SetA2PPins(new ActiveToPassivePinsState(idleA2PPins));

            privateState.TransferProgressStr = String.Empty;
            PublishPrivateState();

            if (currentActivity == ActivitySelect.PerformUnload)
            {
                SetCurrentActivity(ActivitySelect.Ready, "Unload complete");
            }
        }

        readonly ActiveToPassivePinsState idleA2PPins = new ActiveToPassivePinsState();
        ActiveToPassivePinsState lastSetA2PPins = new ActiveToPassivePinsState();

        void SetA2PPins(ActiveToPassivePinsState pinsState)
        {
            lastSetA2PPins = pinsState;
            lastSetA2PPins.IFaceName = PartID;
            lastSetA2PPins.XferILock = true;    // so that log messages do not complain

            SelectedActiveToPassivePinsStateIVA.Set(lastSetA2PPins as IActiveToPassivePinsState);
        }

        bool WaitForP2ATransition(ref PassiveToActivePinBits trackBits, PassiveToActivePinBits waitForBits, PassiveToActivePinBits deltaPinsMask)
        {
            IValueAccessor p2aPinsStateIVA = SelectedPassiveToActivePinsStateIVA;

            PassiveToActivePinBits fixedPinsMask = PassiveToActivePinBits.PinsBitMask & ~deltaPinsMask;
            for (; ; )
            {
                IPassiveToActivePinsState p2aPinsState = new PassiveToActivePinsState(p2aPinsStateIVA.Update().VC);

                PassiveToActivePinBits packedWord = p2aPinsState.PackedWord;

                if (packedWord == waitForBits)
                {
                    trackBits = packedWord;
                    return true;
                }

                if ((packedWord & fixedPinsMask) != (trackBits & fixedPinsMask))
                {
                    SetCurrentActivity(ActivitySelect.WaitForPinsReady, Utils.Fcns.CheckedFormat("{0}[{1}] failed: unexpected P2A pins transition to {2}", currentActivity, privateState.TransferProgressStr, p2aPinsState));
                    return false;
                }

                if (!Spin())
                    return false;
            }
        }

        bool Spin(TimeSpan spinTime)
        {
            Time.QpcTimer timer = new QpcTimer();

            timer.Start(spinTime);

            for (; ; )
            {
                if (!Spin())
                    return false;

                if (timer.IsTriggered)
                    return true;
            }
        }

        bool Spin()
        {
            WaitForSomethingToDo(TimeSpan.FromSeconds(0.02));

            ServiceInstantActionQueue();
            ServiceConfig();

            ActivitySelect abortToActivity = (nextActivitySelect == ActivitySelect.Reset) ? nextActivitySelect : ActivitySelect.WaitForPinsReady; 

            switch (currentActivity)
            {
                case ActivitySelect.PerformLoad:
                case ActivitySelect.PerformUnload:
                    if (abortCurrentActivityReason != null)
                    {
                        SetCurrentActivity(abortToActivity, abortCurrentActivityReason);
                        abortCurrentActivityReason = null;
                        return false;
                    }
                    break;
                default:
                    if (abortCurrentActivityReason != null)
                    {
                        if (nextActivitySelect == ActivitySelect.Reset)
                        {
                            if (currentActivity == ActivitySelect.Reset)
                                Log.Debug.Emit("Ignoring extra Reset request - Reset as already in progress");
                            else if (currentActivity == ActivitySelect.Offline)
                                Log.Warning.Emit("Unable to abort activity:{0} with reason:{1}", currentActivity, abortCurrentActivityReason);
                            else
                                SetCurrentActivity(abortToActivity, abortCurrentActivityReason);
                        }
                        else
                            Log.Warning.Emit("Unable to abort activity:{0} with reason:{1}", currentActivity, abortCurrentActivityReason);

                        abortCurrentActivityReason = null;
                    }
                    break;
            }

            return true;
        }

        #endregion
    }
}

//-------------------------------------------------------------------
