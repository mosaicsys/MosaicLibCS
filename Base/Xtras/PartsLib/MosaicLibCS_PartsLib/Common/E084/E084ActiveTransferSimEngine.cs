//-------------------------------------------------------------------
/*! @file E084ActiveTransferSimEngine.cs
 *  @brief an active part that support simulation of an OHT attached to a load port.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
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
//-------------------------------------------------------------------

namespace MosaicLib.PartsLib.Common.E084
{
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

    using LPMSim = MosaicLib.PartsLib.Common.LPM.Sim;

    public interface IE084ActiveTransferSimEngine : IActivePartBase
    {
        E084ActiveTransferSimEngineState State { get; }
        INotificationObject<E084ActiveTransferSimEngineState> StateNotifier { get; }

        IBasicAction CreateStartResetAction();
        IBasicAction CreateStartSingleLoadAction();
        IBasicAction CreateStartSingleUnloadAction();
        IBasicAction CreateEnableCycleAction(bool enableCycle);
    }

    public struct E084ActiveTransferSimEngineState
    {
        public string StateStr { get; set; }
        public bool IsReady { get; set; }
        public bool IsCycling { get; set; }
        public string TransferProgressStr { get; set; }
        public UInt64 TransferCount { get; set; }

        public E084ActiveTransferSimEngineState(E084ActiveTransferSimEngineState rhs) : this()
        {
            StateStr = rhs.StateStr;
            IsReady = rhs.IsReady;
            IsCycling = rhs.IsCycling;
            TransferProgressStr = rhs.TransferProgressStr;
            TransferCount = rhs.TransferCount;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is E084ActiveTransferSimEngineState))
                return false;

            E084ActiveTransferSimEngineState rhs = (E084ActiveTransferSimEngineState)obj;

            return (StateStr == rhs.StateStr
                    && IsReady == rhs.IsReady
                    && IsCycling == rhs.IsCycling
                    && TransferProgressStr == rhs.TransferProgressStr
                    && TransferCount == rhs.TransferCount);
        }
    }

    public class E084ActiveTransferSimEngine : SimpleActivePartBase, IE084ActiveTransferSimEngine
    {
        #region Construction

        public E084ActiveTransferSimEngine(LPMSim.ILPMSimPart lpmSimPart)
            : base(lpmSimPart.PartID + ".E84Sim", "E084ActiveTransferSimEngine")
        {
            ActionLoggingConfig = Modular.Action.ActionLoggingConfig.Info_Error_Trace_Trace;    // redefine the log levels for actions 

            //This part is a simulated primary part
            PrivateBaseState = new BaseState(true, true) { ConnState = ConnState.NotApplicable };

            instantActionQ = new MosaicLib.Modular.Action.ActionQueue(PartID + ".iq", true, 10);

            this.lpmSimPart = lpmSimPart;

            PublishBaseState("Constructor.Complete");
        }

        #endregion

        #region public state and interface methods

        E084ActiveTransferSimEngineState privateState;
        GuardedNotificationValueObject<E084ActiveTransferSimEngineState> publicStateNotifier = new GuardedNotificationValueObject<E084ActiveTransferSimEngineState>(new E084ActiveTransferSimEngineState());
        public INotificationObject<E084ActiveTransferSimEngineState> StateNotifier { get { return publicStateNotifier; } }
        public E084ActiveTransferSimEngineState State { get { return publicStateNotifier.Object; } }

        protected void PublishPrivateState()
        {
            publicStateNotifier.Object = privateState;
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

        public IBasicAction CreateEnableCycleAction(bool enableCycle)
        {
            return new BasicActionImpl(instantActionQ, delegate() { return PerformSetEnableCycle(enableCycle); }, Utils.Fcns.CheckedFormat("SetEnableCycle({0})", enableCycle), ActionLoggingReference) as IBasicAction;
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

        LPMSim.ILPMSimPart lpmSimPart;
        PIOSelect pioSelect = PIOSelect.OHT;

        enum ActivitySelect : byte
        {
            None = 0,
            Offline,
            Reset,
            WaitForPinsReady,
            Ready,
            PerformLoad,
            PerformUnload,
            AutoLoadAndUnload,
        }

        ActivitySelect currentActivity = ActivitySelect.Offline;
        ActivitySelect nextActivitySelect = ActivitySelect.None;
        string abortCurrentActivityReason = null;

        private bool IsActive { get { return (currentActivity != ActivitySelect.Offline && currentActivity != ActivitySelect.Ready); } }
        private bool IsReady { get { return (currentActivity == ActivitySelect.Ready); } }

        void SetCurrentActivity(ActivitySelect activity, string reason)
        {
            ActivitySelect entryActivity = currentActivity;

            Log.Debug.Emit("SetCurrentActivity changing activity to:{0} [from:{1}, reason:'{2}']", activity, currentActivity, reason);
            currentActivity = activity;

            privateState.StateStr = (Utils.Fcns.CheckedFormat("{0} [{1}]", activity, reason));
            privateState.IsCycling = (currentActivity == ActivitySelect.AutoLoadAndUnload);
            privateState.IsReady = (currentActivity == ActivitySelect.Ready);

            if (entryActivity != currentActivity)
            {
                privateState.TransferProgressStr = String.Empty;
                if (activity == ActivitySelect.AutoLoadAndUnload)
                    privateState.TransferCount = 0;
            }

            bool busy = false;
            switch (currentActivity)
            {
                case ActivitySelect.AutoLoadAndUnload:
                    busy = !String.IsNullOrEmpty(privateState.TransferProgressStr);
                    break;
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

            SetCurrentActivity(ActivitySelect.Offline, "Peforming GoOffline Action");

            nextActivitySelect = ActivitySelect.None;
            lastSetA2PPins = new ActiveToPassivePinsState();
            lastSetA2PPins.IFaceName = PartID;
            lastSetA2PPins.XferILock = true;
            lpmSimPart.CreateSetE084ActivePins(pioSelect, lastSetA2PPins).Start();

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

        private string PerformSetEnableCycle(bool enableCycle)
        {
            if (enableCycle)
            {
                if (IsReady)
                    nextActivitySelect = ActivitySelect.AutoLoadAndUnload;
                else
                    return Utils.Fcns.CheckedFormat("SetEnableCycle(true) Request not permitted, engine is not ready [{0}]", currentActivity);
            }
            else
            {
                if (currentActivity == ActivitySelect.AutoLoadAndUnload)
                    nextActivitySelect = ActivitySelect.Ready;
                else
                    return Utils.Fcns.CheckedFormat("SetEnableCycle(false) Request not permitted, engine is not cycling [{0}]", currentActivity);
            }

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
                case ActivitySelect.AutoLoadAndUnload:
                    ServicePerformAutoLoadAndUnloadActivity();
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

        void ServiceResetActivity()
        {
            using (var eeLog = new Logging.EnterExitTrace(Log))
            {
                LPMSim.State lpmState = lpmSimPart.PublicState;
                IActiveToPassivePinsState a2pPinsState = lpmState.InputsState.GetE84InputsBits(pioSelect);
                if (!a2pPinsState.IsIdle)
                {
                    lastSetA2PPins = new ActiveToPassivePinsState();
                    lastSetA2PPins.IFaceName = PartID;
                    lastSetA2PPins.XferILock = true;
                    IBasicAction clearPinsAction = lpmSimPart.CreateSetE084ActivePins(pioSelect, lastSetA2PPins);
                    clearPinsAction.Run();
                    if (clearPinsAction.ActionState.Failed)
                    {
                        Log.Error.Emit("Reset failed: unable to clear E084 pins:{0}", clearPinsAction);
                        SetCurrentActivity(ActivitySelect.Offline, "Reset failed: unable to clear E084 A2P pins");
                        return;
                    }
                }

                Spin(TimeSpan.FromSeconds(0.5));

                lpmState = lpmSimPart.PublicState;
                a2pPinsState = lpmState.InputsState.GetE84InputsBits(pioSelect);
                IPassiveToActivePinsState p2aPinsState = lpmState.OutputsState.GetE84OutputBits(pioSelect);

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
            LPMSim.State lpmState = lpmSimPart.PublicState;

            IActiveToPassivePinsState a2pPinsState = lpmState.InputsState.GetE84InputsBits(pioSelect);
            IPassiveToActivePinsState p2aPinsState = lpmState.OutputsState.GetE84OutputBits(pioSelect);

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
            LPMSim.State lpmState = lpmSimPart.PublicState;

            IActiveToPassivePinsState a2pPinsState = lpmState.InputsState.GetE84InputsBits(pioSelect);
            IPassiveToActivePinsState p2aPinsState = lpmState.OutputsState.GetE84OutputBits(pioSelect);

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
        }

        void ServicePerformAutoLoadAndUnloadActivity()
        {
            LPMSim.State lpmState = lpmSimPart.PublicState;

            IPassiveToActivePinsState p2aPinsState = lpmState.OutputsState.GetE84OutputBits(pioSelect);

            // first keep waiting until passive to active pins are seletable
            if (!p2aPinsState.IsSelectable)
            {
                if (!p2aPinsState.IsIdle)
                    SetCurrentActivity(ActivitySelect.WaitForPinsReady, Utils.Fcns.CheckedFormat("{0} failed: p2a pins are no longer idle [{1}]", currentActivity, p2aPinsState));

                return;
            }

            // passive to active pins are selectable.

            if (!lpmState.InputsState.PodPresenceSensorState.DoesPlacedEqualPresent)
            {
                SetCurrentActivity(ActivitySelect.WaitForPinsReady, Utils.Fcns.CheckedFormat("{0} failed: Unexpected Pod Presence state [{1}]", currentActivity, lpmState.InputsState.PodPresenceSensorState));
                return;
            }

            // transfer start holdoff timer

            if (lpmState.InputsState.PodPresenceSensorState.IsPlacedAndPresent)
            {
                Log.Info.Emit("{0}: P2A is selectable and FOUP is placed, starting unload", currentActivity);
                ServicePerformUnloadActivity();

                if (currentActivity == ActivitySelect.AutoLoadAndUnload)
                    Spin(TimeSpan.FromSeconds(5.0));
            }
            else
            {
                Log.Info.Emit("{0}: P2A is selectable and port is empty, starting load", currentActivity);
                ServicePeformLoadActivity();

                if (currentActivity == ActivitySelect.AutoLoadAndUnload)
                    Spin(TimeSpan.FromSeconds(5.0));
            }

            if (nextActivitySelect == ActivitySelect.Ready)
            {
                SetCurrentActivity(ActivitySelect.Ready, "Cycling stopped normally");
                nextActivitySelect = ActivitySelect.None;
            }
        }

        void ServicePeformLoadActivity()
        {
            PassiveToActivePinBits p2aPinBits = PassiveToActivePinBits.ES_pin8 | PassiveToActivePinBits.HO_AVBL_pin7;
            PassiveToActivePinBits nextP2APinBits = p2aPinBits;

            TimeSpan loweringTime = TimeSpan.FromSeconds(2.0);
            TimeSpan placementTransitionTime = TimeSpan.FromSeconds(0.5);
            TimeSpan raisingTime = TimeSpan.FromSeconds(2.0);
            TimeSpan deselectDelay = TimeSpan.FromSeconds(0.5);

            privateState.TransferCount++;
            privateState.TransferProgressStr = "Loading:Select for L_REQ";
            PublishPrivateState();
            if (!SetA2PPins(new ActiveToPassivePinsState(idleA2PPins) { VALID = true, CS_0 = true })) return;

            nextP2APinBits = p2aPinBits | PassiveToActivePinBits.L_REQ_pin1;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.L_REQ_pin1)) return; 

            privateState.TransferProgressStr = "Loading:Start T_REQ";
            PublishPrivateState();

            if (!SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { TR_REQ = true })) return;

            nextP2APinBits = p2aPinBits | PassiveToActivePinBits.READY_pin4;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.READY_pin4)) return;

            privateState.TransferProgressStr = "Loading:Go BUSY";
            PublishPrivateState();

            if (!SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { BUSY = true })) return;

            privateState.TransferProgressStr = "Loading:Sim Lowering";
            PublishPrivateState();

            if (!Spin(loweringTime)) return;

            privateState.TransferProgressStr = "Loading:Placing Foup";
            PublishPrivateState();

            if (!SetPodPresenceSensorState(LPMSim.PodPresenceSensorState.FoupPresentAndNotPlaced)) return;

            if (!Spin(placementTransitionTime)) return;

            if (!SetPodPresenceSensorState(LPMSim.PodPresenceSensorState.FoupPresentAndPlaced)) return;

            privateState.TransferProgressStr = "Loading:Wait L_REQ clear";
            PublishPrivateState();

            nextP2APinBits = p2aPinBits & ~PassiveToActivePinBits.L_REQ_pin1;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.L_REQ_pin1)) return;

            privateState.TransferProgressStr = "Loading:Sim Raising";
            PublishPrivateState();

            if (!Spin(raisingTime)) return;

            privateState.TransferProgressStr = "Loading:Go COMPT";
            PublishPrivateState();

            if (!SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { TR_REQ = false, BUSY = false, COMPT = true })) return;

            privateState.TransferProgressStr = "Loading:Wait READY clear";
            PublishPrivateState();

            nextP2APinBits = p2aPinBits & ~PassiveToActivePinBits.READY_pin4;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.READY_pin4)) return;

            privateState.TransferProgressStr = "Loading:Deselecting";
            PublishPrivateState();

            if (!Spin(deselectDelay)) return;

            if (!SetA2PPins(new ActiveToPassivePinsState(idleA2PPins))) return;

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

            TimeSpan loweringTime = TimeSpan.FromSeconds(2.0);
            TimeSpan placementTransitionTime = TimeSpan.FromSeconds(0.5);
            TimeSpan raisingTime = TimeSpan.FromSeconds(2.0);
            TimeSpan deselectDelay = TimeSpan.FromSeconds(0.5);

            privateState.TransferCount++;
            privateState.TransferProgressStr = "Unloading:Select for U_REQ";
            PublishPrivateState();
            if (!SetA2PPins(new ActiveToPassivePinsState(idleA2PPins) { VALID = true, CS_0 = true })) return;

            nextP2APinBits = p2aPinBits | PassiveToActivePinBits.U_REQ_pin2;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.U_REQ_pin2)) return;

            privateState.TransferProgressStr = "Unloading:Start T_REQ";
            PublishPrivateState();

            if (!SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { TR_REQ = true })) return;

            nextP2APinBits = p2aPinBits | PassiveToActivePinBits.READY_pin4;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.READY_pin4)) return;

            privateState.TransferProgressStr = "Unloading:Go BUSY";
            PublishPrivateState();

            if (!SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { BUSY = true })) return;

            privateState.TransferProgressStr = "Unloading:Sim Lowering";
            PublishPrivateState();

            if (!Spin(loweringTime)) return;

            privateState.TransferProgressStr = "Unloading:Taking Foup";
            PublishPrivateState();

            if (!SetPodPresenceSensorState(LPMSim.PodPresenceSensorState.FoupPresentAndNotPlaced)) return;

            if (!Spin(placementTransitionTime)) return;

            if (!SetPodPresenceSensorState(LPMSim.PodPresenceSensorState.FoupNotPresentAndNotPlaced)) return;

            privateState.TransferProgressStr = "Unloading:Wait U_REQ clear";
            PublishPrivateState();

            nextP2APinBits = p2aPinBits & ~PassiveToActivePinBits.U_REQ_pin2;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.U_REQ_pin2)) return;

            privateState.TransferProgressStr = "Unloading:Sim Raising";
            PublishPrivateState();

            if (!Spin(raisingTime)) return;

            privateState.TransferProgressStr = "Unloading:Go COMPT";
            PublishPrivateState();

            if (!SetA2PPins(new ActiveToPassivePinsState(lastSetA2PPins) { TR_REQ = false, BUSY = false, COMPT = true })) return;

            privateState.TransferProgressStr = "Unloading:Wait READY clear";
            PublishPrivateState();

            nextP2APinBits = p2aPinBits & ~PassiveToActivePinBits.READY_pin4;
            if (!WaitForP2ATransition(ref p2aPinBits, nextP2APinBits, PassiveToActivePinBits.READY_pin4)) return;

            privateState.TransferProgressStr = "Unloading:Deselecting";
            PublishPrivateState();

            if (!Spin(deselectDelay)) return;

            if (!SetA2PPins(new ActiveToPassivePinsState(idleA2PPins))) return;

            privateState.TransferProgressStr = String.Empty;
            PublishPrivateState();

            if (currentActivity == ActivitySelect.PerformUnload)
            {
                SetCurrentActivity(ActivitySelect.Ready, "Unload complete");
            }
        }

        readonly ActiveToPassivePinsState idleA2PPins = new ActiveToPassivePinsState();
        ActiveToPassivePinsState lastSetA2PPins = new ActiveToPassivePinsState();

        bool SetA2PPins(ActiveToPassivePinsState pinsState)
        {
            lastSetA2PPins = pinsState;
            lastSetA2PPins.IFaceName = PartID;
            lastSetA2PPins.XferILock = true;    // so that log messages do not complain

            IBasicAction setPinsAction = lpmSimPart.CreateSetE084ActivePins(pioSelect, pinsState);
            setPinsAction.Start();

            for (; ; )
            {
                if (setPinsAction.ActionState.Succeeded)
                    return true;

                if (setPinsAction.ActionState.Failed)
                {
                    SetCurrentActivity(ActivitySelect.Offline, Utils.Fcns.CheckedFormat("{0}[{1}] failed: unable to set E084 Active pins to '{2}'", currentActivity, privateState.TransferProgressStr, pinsState));
                    return false;
                }

                if (!Spin())
                    return false;
            }
        }

        bool SetPodPresenceSensorState(LPMSim.PodPresenceSensorState sensorState)
        {
            IBasicAction setSensorStateAction = lpmSimPart.CreateSetPPSensorState(sensorState);
            setSensorStateAction.Start();

            for (; ; )
            {
                if (setSensorStateAction.ActionState.Succeeded)
                    return true;

                if (setSensorStateAction.ActionState.Failed)
                {
                    SetCurrentActivity(ActivitySelect.Offline, Utils.Fcns.CheckedFormat("{0}[{1}] failed: unable to set Pod Sensor State Active to '{2}'", currentActivity, privateState.TransferProgressStr, sensorState));
                    return false;
                }

                if (!Spin())
                    return false;
            }
        }

        bool WaitForP2ATransition(ref PassiveToActivePinBits trackBits, PassiveToActivePinBits waitForBits, PassiveToActivePinBits deltaPinsMask)
        {
            PassiveToActivePinBits fixedPinsMask = PassiveToActivePinBits.PinsBitMask & ~deltaPinsMask;
            for (; ; )
            {
                LPMSim.State lpmState = lpmSimPart.PublicState;

                IPassiveToActivePinsState p2aPinsState = lpmState.OutputsState.GetE84OutputBits(pioSelect);

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
            WaitForSomethingToDo(TimeSpan.FromSeconds(0.1));

            ServiceInstantActionQueue();

            ActivitySelect abortToActivity = (nextActivitySelect == ActivitySelect.Reset) ? nextActivitySelect : ActivitySelect.WaitForPinsReady; 

            switch (currentActivity)
            {
                case ActivitySelect.AutoLoadAndUnload:
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
