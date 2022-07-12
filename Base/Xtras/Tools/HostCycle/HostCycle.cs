//-------------------------------------------------------------------
/*! @file HostCycle.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2019 Mosaic Systems Inc., All rights reserved
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.Attributes;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.Modular.Part;
using MosaicLib.Semi.E005;
using MosaicLib.Semi.E005.Data;
using MosaicLib.Semi.E005.Manager;
using MosaicLib.Semi.E005.Port;
using MosaicLib.Semi.E039;
using MosaicLib.Semi.E040;
using MosaicLib.Semi.E087;
using MosaicLib.Semi.E090;
using MosaicLib.Semi.E090.SubstrateScheduling;
using MosaicLib.Semi.E094;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace HostCycle
{
    public class HostCyclePart : SimpleActivePartBase
    {
        public HostCyclePart(string partID = "HostCycle", INamedValueSet portSettingsNVS = null, int numLPs = 1)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(addGoOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.SupportServiceActions))
        {
            ivi = Values.Instance;

            ReadConfigValues();
            BuildReportSpecs();

            manager = new ManagerBase(partID + ".Mgr");
            port = manager.CreatePort(manager.PartID + ".HSMS", PortType.E037_Active_SingleSession, makeDefault: true, portConfigNVS: portSettingsNVS);

            port.BaseStateNotifier.NotificationList.AddItem(this);

            var lpPortNumRange = Enumerable.Range(1, numLPs);

            EventReportNotificationList = new EventHandlerNotificationList<EventReport>(this);
            PerPortEventReportNotificationList = lpPortNumRange.Select(portNum => new EventHandlerNotificationList<EventReport>(this)).ToArray();
            NoPortEventReportNotificationList = new EventHandlerNotificationList<EventReport>(this);

            lpCycleSet = lpPortNumRange.Select(lpNum => new HostLPCyclePart("{0}.LP{1}".CheckedFormat(PartID, lpNum), lpNum, port, this)).ToArray();
            lpCycleICFSet = new IClientFacet[numLPs];

            NoPortEventReportNotificationList.OnNotify += AsyncHandleNoPortEventReport;

            AddMainThreadStartingAction(() =>
                {
                    manager.StartPortsIfNeeded(initializePorts: false);
                    lpCycleSet.DoForEach(lp => lp.StartPartIfNeeded());
                });

            AddMainThreadStoppingAction(() =>
                {
                    lpCycleSet.DoForEach(lp => lp.StopPart());
                    manager.StopPortsIfNeeded();
                });

            AddExplicitDisposeAction(() =>
                {
                    lpCycleSet.DoForEach(lp => lp.Dispose());
                    port = null;
                    Fcns.DisposeOfObject(ref manager);
                });

            SetupIVAs();

            CreateE039Support();
        }

        IValuesInterconnection ivi;
        IManager manager;
        IPort port;

        HostLPCyclePart[] lpCycleSet;
        IClientFacet[] lpCycleICFSet;

        private void SetupIVAs()
        {
            ivaEqpInfoNVS = ivi.GetValueAccessor($"{PartID}.EqpInfoNVS").Set(NamedValueSet.Empty);
            ivaPPIDArray = ivi.GetValueAccessor($"{PartID}.PPIDArray").Set(EmptyArrayFactory<string>.Instance);

            ivaTermMesgToHostAvailable = ivi.GetValueAccessor<bool>($"{PartID}.TermMesgToHost.Available").Set(false);
            ivaTermMesgToHostBody = ivi.GetValueAccessor($"{PartID}.TermMesgToHost.Body").Set(null);
            ivaTermMesgToEqpPending = ivi.GetValueAccessor<bool>($"{PartID}.TermMesgToEqp.Pending").Set(false);
            ivaTermMesgToEqpSendResult = ivi.GetValueAccessor<string>($"{PartID}.TermMesgToEqp.SendResult").Set("None");

            ivaSVIDsScanEnabled = ivi.GetValueAccessor<bool>($"{PartID}.SVIDs.ScanEnabled").Set(false);
            ivaSVIDsSMPLN = ivi.GetValueAccessor<long>($"{PartID}.SVIDs.SMPLN");
            ivaSVIDsSTIME = ivi.GetValueAccessor<string>($"{PartID}.SVIDs.STIME");
        }

        NamedValueSet eqpInfoNVS = new NamedValueSet();
        IValueAccessor ivaEqpInfoNVS;

        IValueAccessor ivaPPIDArray;

        IValueAccessor<bool> ivaTermMesgToHostAvailable;
        IValueAccessor ivaTermMesgToHostBody;
        IValueAccessor<bool> ivaTermMesgToEqpPending;
        IValueAccessor<string> ivaTermMesgToEqpSendResult;

        IValueAccessor<bool> ivaSVIDsScanEnabled;
        IValueAccessor<long> ivaSVIDsSMPLN;
        IValueAccessor<string> ivaSVIDsSTIME;

        bool hasInitializeBeenRequested = false;

        protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
        {
            hasInitializeBeenRequested |= andInitialize;

            if (BaseState.ExplicitFaultReason.IsNeitherNullNorEmpty())
                SetExplicitFaultReason(""); // clear any prior explicit fault reason.

            PerformMainLoopService();

            eqpInfoNVS.Clear();
            ivaEqpInfoNVS.Set(eqpInfoNVS);

            lpCycleICFSet.WhereIsNotDefault().DoForEach(icf => icf.RequestCancel());
            lpCycleICFSet.SetAll(null);

            string ec = manager.StartPortsIfNeeded(initializePorts: hasInitializeBeenRequested);

            if (ec.IsNullOrEmpty() && !hasInitializeBeenRequested && !port.BaseState.IsConnected)
            {
                SetBaseState(UseState.OnlineUninitialized, "No port connection has been attempted yet");
                return string.Empty;
            }

            if (ec.IsNullOrEmpty())
                ec = SetupConnectionAndHandlers();

            if (ec.IsNullOrEmpty())
                ec = Refresh();

            if (ec.IsNullOrEmpty())
                ec = RefreshE039Table();

            if (ec.IsNullOrEmpty())
            {
                foreach (var lp in lpCycleSet)
                {
                    ec = lp.RunGoOnlineAction(andInitialize: andInitialize);
                    if (ec.IsNeitherNullNorEmpty())
                        break;
                }
            }
            else
            {
                foreach (var lp in lpCycleSet)
                {
                    if (lp.BaseState.UseState.IsOnline(acceptOnlineFailure: false, acceptAttemptOnlineFailed: false))
                        lp.RunGoOnlineAction();
                }
            }

            return ec;
        }

        protected override string PerformGoOfflineAction(IProviderActionBase action)
        {
            PerformMainLoopService();
            lpCycleICFSet.WhereIsNotDefault().DoForEach(icf => icf.RequestCancel());
            lpCycleICFSet.SetAll(null);

            string ec = string.Empty;

            if (ec.IsNullOrEmpty())
            {
                foreach (var lp in lpCycleSet)
                {
                    ec = lp.RunGoOfflineAction();
                    if (ec.IsNeitherNullNorEmpty())
                        break;
                }
            }

            if (ec.IsNullOrEmpty())
                ec = port.RunGoOfflineAction();
            else
                port.RunGoOfflineAction();

            return ec;
        }

        protected override string PerformServiceActionEx(IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            PerformMainLoopService();

            StringScanner scanner = new StringScanner(serviceName);

            if (scanner.MatchToken("LP", skipTrailingWhiteSpace: false, requireTokenEnd: false))
            {
                int lpNum = -1;

                scanner.ParseValue(out lpNum);
                int lpIdx = lpNum - 1;
                var lpInstance = lpCycleSet.SafeAccess(lpIdx);

                if (lpInstance == null)
                    return "The given LPNum {0} is not valid in '{1}'".CheckedFormat(lpNum, serviceName);

                var prevICF = lpCycleICFSet.SafeAccess(lpIdx);
                if (prevICF != null)
                {
                    prevICF.RequestCancel();
                    lpCycleICFSet[lpIdx] = null;
                }

                if (scanner.MatchToken("Start"))
                {
                    lpCycleICFSet[lpIdx] = lpInstance.CreateServiceAction(scanner.Rest, npv).StartInline();
                    return string.Empty;
                }

                return lpInstance.RunServiceAction(scanner.Rest, npv);
            }

            string token = scanner.ExtractToken();

            switch (token)
            {
                case "Connect":
                    {
                        if (port.BaseState.IsOnlineAndConnected())
                        {
                            // future: run a quick AreYouThere (1 second) message to see if the port is really connected and then run the full pattern if this fails.
                            return "";
                        }

                        return OuterPerformGoOnlineAction(ipf, true, NamedValueSet.Empty);
                    }
                case "Disconnect": return OuterPerformGoOfflineAction((IProviderActionBase)ipf);
                case "SetHostOffline": return SetHostOffline();
                case "SetHostOnline": return SetHostOnline();
                case "GoLocal": return GoLocal();
                case "GoRemote": return GoRemote();
                case "Refresh": return Refresh().MapEmptyToNull() ?? RefreshE039Table();
                case "RefreshE039": return RefreshE039Table();

                case "TermMesgToHost.Ack": ivaTermMesgToHostAvailable.Set(false); return "";

                case "TermMesgToTool.SendOne":
                case "TermMesgToTool.SendMany":
                case "TermMesgToTool.BroadcastOne":
                case "TermMesgToTool.SendClear":
                    return HandleTermMesgToToolServiceRequest(ipf, token, npv);

                case "SVIDs": return HandleSVIDServiceRequest(ipf, serviceName, scanner.Rest, npv);
                case "ECIDs": return HandleECIDServiceRequest(ipf, serviceName, scanner.Rest, npv);

                default: return base.PerformServiceActionEx(ipf, serviceName, npv);
            }
        }

        private string SetHostOffline()
        {
            try
            {
                var s1f15Mesg = port.CreateMessage("S1/F15W");

                string ec = s1f15Mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                {
                    var replyBodyVC = s1f15Mesg.Reply.GetDecodedContents();
                    var oflack = replyBodyVC.GetValue<OFLACK>(rethrow: true);

                    if (oflack != OFLACK.Acknowledge)
                        ec = "{0} failed: OFLACK:{1} [reply:{2}]".CheckedFormat(s1f15Mesg, oflack, s1f15Mesg.Reply);
                }

                eqpInfoNVS.SetValue("ControlState", ReadControlStateSVID());
                ivaEqpInfoNVS.Set(eqpInfoNVS);

                return ec;
            }
            catch (System.Exception ex)
            {
                return "{0} failed with unexpected exception: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }
        }

        private string SetHostOnline()
        {
            try
            {
                var s1f17Mesg = port.CreateMessage("S1/F17W");

                string ec = s1f17Mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                {
                    var replyBodyVC = s1f17Mesg.Reply.GetDecodedContents();
                    var onlack = replyBodyVC.GetValue<ONLACK>(rethrow: true);

                    if (onlack != ONLACK.Accepted && onlack != ONLACK.AlreadyOnline)
                        ec = "{0} failed: ONLACK:{1} [reply:{2}]".CheckedFormat(s1f17Mesg, onlack, s1f17Mesg.Reply);
                }

                eqpInfoNVS.SetValue("ControlState", ReadControlStateSVID());
                ivaEqpInfoNVS.Set(eqpInfoNVS);

                return ec;
            }
            catch (System.Exception ex)
            {
                return "{0} failed with unexpected exception: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }
        }

        private string GoLocal()
        {
            string ec = string.Empty;

            var controlState = ReadControlStateSVID();

            if (ec.IsNullOrEmpty() && controlState != ControlState.OnlineLocal && controlState != ControlState.OnlineRemote)
                ec = SetHostOnline();

            if (ec.IsNullOrEmpty() && e030IDsConfig.GoLocalCmd.IsNeitherNullNorEmpty())
                ec = RunS2F41RemoteCmd(e030IDsConfig.GoLocalCmd);

            eqpInfoNVS.SetValue("ControlState", ReadControlStateSVID());
            ivaEqpInfoNVS.Set(eqpInfoNVS);

            return ec;
        }

        private string GoRemote()
        {
            string ec = string.Empty;

            var controlState = ReadControlStateSVID();

            if (ec.IsNullOrEmpty() && controlState != ControlState.OnlineLocal && controlState != ControlState.OnlineRemote)
                ec = SetHostOnline();

            if (ec.IsNullOrEmpty() && e030IDsConfig.GoRemoteCmd.IsNeitherNullNorEmpty())
                ec = RunS2F41RemoteCmd(e030IDsConfig.GoRemoteCmd);

            eqpInfoNVS.SetValue("ControlState", ReadControlStateSVID());
            ivaEqpInfoNVS.Set(eqpInfoNVS);

            return ec;
        }

        private string Refresh(bool readControlState = true, bool readPPIDArray = true)
        {
            string ec = string.Empty;

            if (readControlState && ec.IsNullOrEmpty())
            {
                eqpInfoNVS.SetValue("ControlState", ReadControlStateSVID());
                ivaEqpInfoNVS.Set(eqpInfoNVS);
            }

            if (readPPIDArray && ec.IsNullOrEmpty()) // read PPIDs
            {
                var s7F19Mesg = port.CreateMessage("S7/F19W");

                ec = s7F19Mesg.Send().Run();

                var ppidArray = (ec.IsNullOrEmpty() ? s7F19Mesg.Reply.GetDecodedContents().GetValue<string[]>(rethrow: true) : null).MapNullToEmpty();

                ivaPPIDArray.Set(ppidArray);
            }

            return (ec);
        }

        private string HandleTermMesgToToolServiceRequest(IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            ivaTermMesgToEqpSendResult.Set("Sending");

            try
            {
                IMessage mesg;

                switch (serviceName)
                {
                    case "TermMesgToTool.SendOne":
                        mesg = port.CreateMessage("S10F3[W]").SetContentBytes(new L(new Bi(e030IDsConfig.DefaultTermMesgToEqpTermID), new A(npv["Text"].VC.GetValue<string>(rethrow: true))));
                        ivaTermMesgToEqpPending.Set(true);
                        break;
                    case "TermMesgToTool.SendMany":
                        {
                            var linesSet = npv.Select(nv => new A(nv.VC.GetValue<string>(rethrow: true)));
                            if (e030IDsConfig.TermMesgToEqpMaxLinesToSend > 0)
                                linesSet = linesSet.Take(e030IDsConfig.TermMesgToEqpMaxLinesToSend);

                            mesg = port.CreateMessage("S10F5[W]").SetContentBytes(new L(new Bi(e030IDsConfig.DefaultTermMesgToEqpTermID), new L(linesSet)));
                        }
                        ivaTermMesgToEqpPending.Set(true);
                        break;
                    case "TermMesgToTool.BroadcastOne":
                        mesg = port.CreateMessage("S10F9").SetContentBytes(new A(npv["Text"].VC.GetValue<string>(rethrow: true)));
                        break;
                    case "TermMesgToTool.SendClear":
                        mesg = port.CreateMessage("S10F3").SetContentBytes(new L(new Bi(e030IDsConfig.DefaultTermMesgToEqpTermID), new A("")));
                        break;
                    default:
                        return "Internal - mesg to tool service name '{0}' not recognized".CheckedFormat(serviceName);
                }

                string ec = mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                {
                    if (mesg.SF.ReplyExpected)
                    {
                        var replyBodyVC = mesg.Reply.GetDecodedContents();
                        var ackc10 = replyBodyVC.GetValue<ACKC10>(rethrow: true);

                        ivaTermMesgToEqpSendResult.Set(ackc10);
                        if (ackc10 != ACKC10.AcceptedForDisplay)
                            ivaTermMesgToEqpPending.Set(false);
                    }
                    else
                    {
                        ivaTermMesgToEqpSendResult.Set("Sent");
                        ivaTermMesgToEqpPending.Set(false);
                    }
                }
                else
                {
                    ivaTermMesgToEqpSendResult.Set($"Error: {ec}");
                    ivaTermMesgToEqpPending.Set(false);
                }

                return ec;
            }
            catch (System.Exception ex)
            {
                string ec = "{0}('{1}' {2}) failed with unexpected exception: {3}".CheckedFormat(CurrentMethodName, serviceName, npv.SafeToStringSML(), ex.ToString(ExceptionFormat.TypeAndMessage));

                ivaTermMesgToEqpSendResult.Set($"Error: {ec}");
                ivaTermMesgToEqpPending.Set(false);

                return ec;
            }
        }

        private ControlState ReadControlStateSVID()
        {
            var controlStateVC = ReadSVIDValue(e030IDsConfig.ControlState_SVID);
            var controlState = controlStateVC.GetValue<ControlState>(rethrow: false);
            return controlState;
        }

        public ValueContainer ReadSVIDValue(string svidName)
        {
            return ReadSVIDValue((svidNameToInfoDictionary.SafeTryGetValue(svidName) ?? new SVIDInfo()).SVID);
        }

        public ValueContainer ReadSVIDValue(ValueContainer svid)
        {
            if (svid.IsNullOrEmpty)
                return ValueContainer.Empty;

            try
            {
                var mesg = port.CreateMessage("S1/F3W").SetContentBytes(new L(svid.MakeVCBuilder()));
                string ec = mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                    return mesg.Reply.GetDecodedContents().SafeAccess(0);
            }
            catch (System.Exception ex)
            {
                var svidInfo = svidToInfoDictionary.SafeTryGetValue(svid);
                Log.Debug.Emit("{0} {1} failed with unexpected exception: {2}", CurrentMethodName, svidInfo, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }

            return ValueContainer.Empty;
        }

        public IList<ValueContainer> ReadSVIDValues(params ValueContainer[] svidParamsArray)
        {
            if (svidParamsArray.IsNullOrEmpty())
                return EmptyArrayFactory<ValueContainer>.Instance;

            try
            {
                var mesg = port.CreateMessage("S1/F3W").SetContentBytes(new VCB() { VC = ValueContainer.Create(svidParamsArray) });
                string ec = mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                    return mesg.Reply.GetDecodedContents().GetValue<IList<ValueContainer>>(rethrow: true);
            }
            catch (System.Exception ex)
            {
                var svidsInfoStr = string.Join(",", svidParamsArray.Select(svid => svidToInfoDictionary.SafeTryGetValue(svid).SafeToString()));
                Log.Debug.Emit("{0} {1} failed with unexpected exception: {2}", CurrentMethodName, svidsInfoStr, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }

            return EmptyArrayFactory<ValueContainer>.Instance;
        }

        private string RunS2F41RemoteCmd(string cmd, INamedValueSet nvs = null)
        {
            try
            {
                var s2f41Mesg = port.CreateMessage("S2/F41W").SetContentBytes(new L(new A(cmd), nvs.MakeListBuilder()));

                string ec = s2f41Mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                {
                    var replyBodyVC = s2f41Mesg.Reply.GetDecodedContents();
                    var hcack = replyBodyVC.SafeAccess(0).GetValue<HCACK>(rethrow: true);

                    if (hcack != HCACK.Acknowledge_CommandHasBeenPerformed && hcack != HCACK.Acknowledge_CompletionWillBeSignaledLaterByEvent)
                        ec = "{0} failed: HCACK:{1} [reply:{2}]".CheckedFormat(s2f41Mesg, hcack, s2f41Mesg.Reply);
                }

                return ec;
            }
            catch (System.Exception ex)
            {
                return "{0}('{1}', {2}) failed with unexpected exception: {3}".CheckedFormat(CurrentMethodName, cmd, nvs.SafeToStringSML(), ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }
        }

        protected override void PerformMainLoopService()
        {
            base.PerformMainLoopService();

            if (lpCycleICFSet.Any(icf => icf != null && icf.ActionState.IsComplete))
            {
                lpCycleICFSet.DoForEach((icf, idx) => { if (icf.ActionState.IsComplete) lpCycleICFSet[idx] = null; });
            }

            ServicePendingEvents();
            ServiceS6F1Queue();
            Service039Support();

            {
                var portBaseState = port.BaseState;
                var portIsFaulted = portBaseState.IsFaulted();

                if (BaseState.UseState.IsOnline(acceptAttemptOnline: false, acceptUninitialized: false) && BaseState.IsFaulted() != portIsFaulted)
                {
                    if (portIsFaulted && !object.ReferenceEquals(portBaseState, lastFaultedPortBaseState))
                    {
                        var portFaultReason = $"{port.PartID} is faulted: ${portBaseState}";
                        if (BaseState.ExplicitFaultReason != portFaultReason)
                            SetExplicitFaultReason(portFaultReason);

                        lastFaultedPortBaseState = portBaseState;
                    }
                    else if (BaseState.ExplicitFaultReason.IsNeitherNullNorEmpty())
                    {
                        SetExplicitFaultReason("");
                    }
                }
            }
        }
        private IBaseState lastFaultedPortBaseState;

        private void ReadConfigValues()
        {
            ConfigValueSetAdapter<Config> configAdapter = new ConfigValueSetAdapter<Config>()
            {
                ValueSet = config,
                SetupIssueEmitter = Log.Debug,
                ValueNoteEmitter = Log.Trace,
            }.Setup("Config.HostCycle.");

            ConfigValueSetAdapter<E030IDsConfig> e030Adapter = new ConfigValueSetAdapter<E030IDsConfig>()
            {
                ValueSet = e030IDsConfig,
                SetupIssueEmitter = Log.Debug,
                ValueNoteEmitter = Log.Trace,
            }.Setup("E030Config.");

            ConfigValueSetAdapter<E087IDsConfig> e087Adapter = new ConfigValueSetAdapter<E087IDsConfig>()
            {
                ValueSet = e087IDsConfig,
                SetupIssueEmitter = Log.Debug,
                ValueNoteEmitter = Log.Trace,
            }.Setup("E087Config.");

            ConfigValueSetAdapter<E040IDsConfig> e040Adapter = new ConfigValueSetAdapter<E040IDsConfig>()
            {
                ValueSet = e040IDsConfig,
                SetupIssueEmitter = Log.Debug,
                ValueNoteEmitter = Log.Trace,
            }.Setup("E040Config.");

            ConfigValueSetAdapter<E094IDsConfig> e094Adapter = new ConfigValueSetAdapter<E094IDsConfig>()
            {
                ValueSet = e094IDsConfig,
                SetupIssueEmitter = Log.Debug,
                ValueNoteEmitter = Log.Trace,
            }.Setup("E094Config.");

            ConfigValueSetAdapter<E090IDsConfig> e090Adapter = new ConfigValueSetAdapter<E090IDsConfig>()
            {
                ValueSet = e090IDsConfig,
                SetupIssueEmitter = Log.Debug,
                ValueNoteEmitter = Log.Trace,
            }.Setup("E090Config.");
        }

        public class Config
        {
            [ConfigItem]
            public string RecipeNameExtension { get; set; } = ".txt";

            [ConfigItem]
            public bool CreateJobWithTemporaryRecipe { get; set; } = false;
        }

        public readonly Config config = new Config();

        public readonly E030IDsConfig e030IDsConfig = new E030IDsConfig();
        public readonly E087IDsConfig e087IDsConfig = new E087IDsConfig();
        public readonly E040IDsConfig e040IDsConfig = new E040IDsConfig();
        public readonly E094IDsConfig e094IDsConfig = new E094IDsConfig();
        public readonly E090IDsConfig e090IDsConfig = new E090IDsConfig();

        private string SetupConnectionAndHandlers()
        {
            try
            {
                manager.RegisterSFProcessingHandler(RootAsyncS1F13Handler, "S1/F13", "S1/F13[W]");
                manager.RegisterSFProcessingHandler(RootAsyncS6F11Handler, "S6/F11", "S6/F11[W]");
                manager.RegisterSFProcessingHandler(RootAsyncS10F1Handler, "S10/F1", "S10/F1[W]");
                manager.RegisterSFProcessingHandler(RootAsyncS16F7Handler, "S16/F7", "S16/F7[W]");
                manager.RegisterSFProcessingHandler(RootAsyncS16F9Handler, "S16/F9", "S16/F9[W]");
                manager.RegisterSFProcessingHandler(RootAsyncS6F1Handler, "S6/F1", "S6/F1[W]");
                manager.RegisterSFProcessingHandler(RootAsyncS5F1Handler, "S5/F1", "S5/F1[W]");

                manager.SetSFSetAsHighRate("S6/F1", "S6/F1[W]"); // TDS - Trace Data Send

                string ec = string.Empty;

                if (ec.IsNullOrEmpty() && !port.BaseState.IsConnected)
                    ec = "port is not connected";

                // S1F13 : Establish communications
                if (ec.IsNullOrEmpty())
                {
                    var s1f13Mesg = port.CreateMessage("S1/F13W").SetContentBytes(new L());
                    ec = s1f13Mesg.Send().Run();

                    var s1f14BodyVC = s1f13Mesg.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty());
                    var commAck = s1f14BodyVC.SafeAccess(0).GetValue<COMMACK>(rethrow: ec.IsNullOrEmpty(), defaultValue: COMMACK.Denied_Internal);
                    var valuePairList = s1f14BodyVC.SafeAccess(1);

                    if (ec.IsNullOrEmpty() && s1f13Mesg.Reply.SF.ToString() != "S1/F14" || !s1f14BodyVC.cvt.IsList() || commAck != COMMACK.Accepted)
                        ec = "Invalid reply to '{0}': '{1}' commAck:{2}".CheckedFormat(s1f13Mesg, s1f13Mesg.Reply, commAck);

                    if (ec.IsNullOrEmpty())
                    {
                        eqpInfoNVS.SetValue("MDLN", valuePairList.SafeAccess(0).GetValue<string>(rethrow: true).MapNullToEmpty());
                        eqpInfoNVS.SetValue("SOFTREV", valuePairList.SafeAccess(1).GetValue<string>(rethrow: true).MapNullToEmpty());
                        ivaEqpInfoNVS.Set(eqpInfoNVS);
                    }
                }

                // Future: move this to be run periodically in the background
                // S1F1: Are you there
                if (ec.IsNullOrEmpty())
                {
                    var s1f1Mesg = port.CreateMessage("S1/F1W");
                    ec = s1f1Mesg.Send().Run();

                    var s1f1BodyVC = s1f1Mesg.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty());

                    if (ec.IsNullOrEmpty() && s1f1Mesg.Reply.SF.ToString() != "S1/F2" || !s1f1BodyVC.cvt.IsList())
                        ec = "Invalid reply to '{0}': '{1}'".CheckedFormat(s1f1Mesg, s1f1Mesg.Reply);
                }

                // S2F43 : Disable spooling
                if (ec.IsNullOrEmpty())
                {
                    var s2F43Mesg = port.CreateMessage("S2/F43W").SetContentBytes(new L());
                    ec = s2F43Mesg.Send().Run();

                    var s2F44BodyVC = s2F43Mesg.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty());
                    var rspack = s2F44BodyVC.SafeAccess(0).GetValue<RSPACK>(rethrow: ec.IsNullOrEmpty(), defaultValue: RSPACK.Denied_Internal);

                    if (ec.IsNullOrEmpty() && rspack != RSPACK.Accepted)
                        ec = "Invalid reply to '{0}': '{1}' rspack:{2}".CheckedFormat(s2F43Mesg, s2F43Mesg.Reply, rspack);
                }

                // S6F23 : Purge spooled data
                if (ec.IsNullOrEmpty())
                {
                    var s6f23Mesg = port.CreateMessage("S6/F23W").SetContentBytes(new U1((byte)RSDC.Purge));
                    ec = s6f23Mesg.Send().Run();

                    var rsda = s6f23Mesg.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty()).GetValue<RSDA>(rethrow: ec.IsNullOrEmpty(), defaultValue: RSDA.Denied_Internal);

                    Log.Debug.Emit("Clear spool request '{0}' gave response rsda:{1} [reply:{2}]", s6f23Mesg, rsda, s6f23Mesg.Reply);
                }

                // S2F33 : Message 1 - delete and unlink all reports
                if (ec.IsNullOrEmpty())
                {
                    var dataid = port.GetNextDATAID();
                    var s2F33Mesg1 = port.CreateMessage("S2/F33W").SetContentBytes(new L(new U4(dataid), new L()));
                    ec = s2F33Mesg1.Send().Run();

                    var drack = s2F33Mesg1.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty()).GetValue<DRACK>(rethrow: ec.IsNullOrEmpty(), defaultValue: DRACK.Denied_Internal);

                    if (ec.IsNullOrEmpty() && drack != DRACK.Accepted)
                        ec = "Invalid reply to '{0}': '{1}' drack:{2}".CheckedFormat(s2F33Mesg1, s2F33Mesg1.Reply, drack);
                }

                // S2F37 : Disable all events
                if (ec.IsNullOrEmpty())
                {
                    var s2F37Mesg1 = port.CreateMessage("S2/F37W").SetContentBytes(new L(new Bo(false), new L()));
                    ec = s2F37Mesg1.Send().Run();

                    var s2F38BodyVC = s2F37Mesg1.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty());
                    var erack = s2F38BodyVC.GetValue<ERACK>(rethrow: ec.IsNullOrEmpty(), defaultValue: ERACK.Denied_Internal);

                    if (ec.IsNullOrEmpty() && erack != ERACK.Accepted)
                        ec = "Invalid reply to '{0}': '{1}' erack:{2}".CheckedFormat(s2F37Mesg1, s2F37Mesg1.Reply, erack);
                }

                // get all VID definitions (currently SVIDs and ECIDs)
                if (ec.IsNullOrEmpty())
                {
                    ec = GetAllIDDefinitions();
                }

                // disable SVID scanning - this also does an initial read of all SVIDs and populates the SVID_IVI for screen display
                if (ec.IsNullOrEmpty())
                {
                    ec = SetSVIDScanEnable(false);
                }

                // read all ECIDs
                if (ec.IsNullOrEmpty())
                {
                    ec = RefreshECIDValues();
                }

                // S2F33 : Define full set of reports
                if (ec.IsNullOrEmpty())
                {
                    var dataid = port.GetNextDATAID();

                    var reportIDSpecList = new L(reportSpecList.Where(spec => spec.IsValid).Select(spec => new L(spec.ReportID.MakeVCBuilder(), spec.VIDArray.MakeListBuilder())));

                    var s2f33Mesg2 = port.CreateMessage("S2/F33W").SetContentBytes(new L(new U4(dataid), reportIDSpecList));
                    ec = s2f33Mesg2.Send().Run();

                    var drack = s2f33Mesg2.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty()).GetValue<DRACK>(rethrow: ec.IsNullOrEmpty(), defaultValue: DRACK.Denied_Internal);

                    if (ec.IsNullOrEmpty() && drack != DRACK.Accepted)
                        ec = "Invalid reply to '{0}': '{1}' drack:{2}".CheckedFormat(s2f33Mesg2, s2f33Mesg2.Reply, drack);
                }

                // S2F35 : Link event reports
                if (ec.IsNullOrEmpty())
                {
                    var dataid = port.GetNextDATAID();

                    var eventIDLinkList = new L(eventSpecList.Where(spec => spec.IsValid).Select(eventSpec => new L(eventSpec.CEID.MakeVCBuilder(), eventSpec.ReportSpecArray.Select(reportSpec => reportSpec.ReportID).MakeListBuilder())));

                    var s2f35Mesg = port.CreateMessage("S2/F35W").SetContentBytes(new L(new U4(dataid), eventIDLinkList));
                    ec = s2f35Mesg.Send().Run();

                    var lrack = s2f35Mesg.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty()).GetValue<LRACK>(rethrow: ec.IsNullOrEmpty(), defaultValue: LRACK.Denied_Internal);

                    if (ec.IsNullOrEmpty() && lrack != LRACK.Accepted)
                        ec = "Invalid reply to '{0}': '{1}' lrack:{2}".CheckedFormat(s2f35Mesg, s2f35Mesg.Reply, lrack);
                }

                // S2F37 : Enable all events
                if (ec.IsNullOrEmpty())
                {
                    var s2F37Mesg2 = port.CreateMessage("S2/F37W").SetContentBytes(new L(new Bo(true), new L()));
                    ec = s2F37Mesg2.Send().Run();

                    var erack = s2F37Mesg2.Reply.GetDecodedContents(throwOnException: ec.IsNullOrEmpty()).GetValue<ERACK>(rethrow: ec.IsNullOrEmpty(), defaultValue: ERACK.Denied_Internal);

                    if (ec.IsNullOrEmpty() && erack != ERACK.Accepted)
                        ec = "Invalid reply to '{0}': '{1}' erack:{2}".CheckedFormat(s2F37Mesg2, s2F37Mesg2.Reply, erack);
                }

                // enable all alarms
                if (ec.IsNullOrEmpty())
                {
                    ec = EnableAllAlarms(enable: true);
                }

                return ec;
            }
            catch (System.Exception ex)
            {
                return "{0} failed with unexpected exception: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }
        }

        private string GetAllIDDefinitions()
        {
            try
            {
                string ec = string.Empty;

                // s1f11 - SVIDs
                if (ec.IsNullOrEmpty())
                {
                    var s1f11Mesg = port.CreateMessage("S1/F11W").SetContentBytes(new L());
                    ec = s1f11Mesg.Send().Run();

                    if (ec.IsNullOrEmpty())
                    {
                        var resultList = s1f11Mesg.Reply.GetDecodedContents().GetValue<IList<ValueContainer>>(rethrow: true);

                        svidInfoArray = resultList.Select(tupleListVC => new SVIDInfo(tupleListVC)).ToArray();
                        if (svidInfoArray.All(svi => svi.SVID.cvt.IsInteger()))
                            svidInfoArray = svidInfoArray.OrderBy(svi => svi.SVID.GetValue<long>(rethrow: false)).ToArray();

                        svidToInfoDictionary = new ReadOnlyIDictionary<ValueContainer, SVIDInfo>(svidInfoArray.Select(si => KVP.Create(si.SVID, si)));
                        svidNameToInfoDictionary = new ReadOnlyIDictionary<string, SVIDInfo>(svidInfoArray.Select(si => KVP.Create(si.Name, si)));
                    }
                }

                // s2f29 - ECIDs
                if (ec.IsNullOrEmpty())
                {
                    var s2f29Mesg = port.CreateMessage("S2/F29W").SetContentBytes(new L());

                    ec = s2f29Mesg.Send().Run();

                    if (ec.IsNullOrEmpty())
                    {
                        var resultList = s2f29Mesg.Reply.GetDecodedContents().GetValue<IList<ValueContainer>>(rethrow: true);

                        ecidInfoArray = resultList.Select(tupleListVC => new ECIDInfo(tupleListVC)).ToArray();
                        if (ecidInfoArray.All(eci => eci.ECID.cvt.IsInteger()))
                            ecidInfoArray = ecidInfoArray.OrderBy(eci => eci.ECID.GetValue<long>(rethrow: false)).ToArray();

                        ecidToInfoDictionary = new ReadOnlyIDictionary<ValueContainer, ECIDInfo>(ecidInfoArray.Select(ei => KVP.Create(ei.ECID, ei)));
                    }
                }

                return ec;
            }
            catch (System.Exception ex)
            {
                return "{0} failed with unexpected exception: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }
        }

        private class SVIDInfo
        {
            public SVIDInfo() { }
            public SVIDInfo(ValueContainer tupleListVC)
            {
                SVID = tupleListVC.SafeAccess(0);
                Name = tupleListVC.SafeAccess(1).GetValue<string>(rethrow: true).Sanitize();
                Units = tupleListVC.SafeAccess(2).GetValue<string>(rethrow: true).Sanitize();
            }

            public string Name;
            public ValueContainer SVID;
            public string Units;

            public string IVAName { get { return "{0,-10}{1}".CheckedFormat(SVID.ValueAsObject, Name); } }

            public override string ToString()
            {
                if (Units.IsNullOrEmpty())
                    return "SVID {0} '{1}'".CheckedFormat(SVID, Name);
                else
                    return "SVID {0} '{1}' units:'{2}'".CheckedFormat(SVID, Name, Units);
            }
        }

        private class ECIDInfo
        {
            public ECIDInfo() { }
            public ECIDInfo(ValueContainer tupleListVC)
            {
                ECID = tupleListVC.SafeAccess(0);
                Name = tupleListVC.SafeAccess(1).GetValue<string>(rethrow: true).Sanitize();
                Min = tupleListVC.SafeAccess(2);
                Max = tupleListVC.SafeAccess(3);
                Default = tupleListVC.SafeAccess(4);
                Units = tupleListVC.SafeAccess(5).GetValue<string>(rethrow: true).Sanitize();
            }

            public string Name;
            public ValueContainer ECID;
            public ValueContainer Min;
            public ValueContainer Max;
            public ValueContainer Default;
            public string Units;

            public string IVAName { get { return "{0,-10}{1}".CheckedFormat(ECID.ValueAsObject, Name); } }

            public override string ToString()
            {
                if (Units.IsNullOrEmpty())
                    return "ECID {0} '{1}' min:{2} max:{3} default:{4}".CheckedFormat(ECID, Name, Min, Max, Default);
                else
                    return "ECID {0} '{1}' min:{2} max:{3} default:{4} units:'{5}'".CheckedFormat(ECID, Name, Min, Max, Default, Units);
            }
        }

        SVIDInfo[] svidInfoArray;

        ReadOnlyIDictionary<ValueContainer, SVIDInfo> svidToInfoDictionary = ReadOnlyIDictionary<ValueContainer, SVIDInfo>.Empty;
        ReadOnlyIDictionary<string, SVIDInfo> svidNameToInfoDictionary = ReadOnlyIDictionary<string, SVIDInfo>.Empty;

        ECIDInfo[] ecidInfoArray;

        ReadOnlyIDictionary<ValueContainer, ECIDInfo> ecidToInfoDictionary = ReadOnlyIDictionary<ValueContainer, ECIDInfo>.Empty;

        #region Event reporting (BuildReportSpecs, RootAsyncS6F11Handler, AsyncHandleEventReport, ServicePendingEvents, HandleEventReport)

        private void BuildReportSpecs()
        {
            eventSpecList.Clear();

            // build up the E30 eventSpecList and reportSpecList items here
            {
                var vidCfg = e030IDsConfig;

                EventSpec equipmentOfflineEvent, controlStateLocalEvent, controlStateRemoteEvent, processingStartedEvent, processingCompleteEvent, processingStoppedEvent, processingStateChangeEvent, operatorECChangeEvent, ppChangeEvent, ppSelectedEvent, materialReceivedEvent, materialRemovedEvent, messageRecognitionEvent;
                ReportSpec equipmentOfflineReport, controlStateLocalReport, controlStateRemoteReport, processingStartedReport, processingCompleteReport, processingStoppedReport, processingStateChangeReport, operatorECChangeReport, ppChangeReport, ppSelectedReport, materialReceivedReport, materialRemovedReport, messageRecognitionReport;

                {
                    var eventID = vidCfg.EquipmentOffline_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "ControlState" },
                        VIDArray = new[] { vidCfg.ControlState_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "EquipmentOffline", CEID = eventID, ReportSpec = reportSpec };

                    equipmentOfflineEvent = eventSpec;
                    equipmentOfflineReport = reportSpec;
                }

                {
                    var eventID = vidCfg.ControlStateLocal_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "ControlState" },
                        VIDArray = new[] { vidCfg.ControlState_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "ControlStateLocal", CEID = eventID, ReportSpec = reportSpec };

                    controlStateLocalEvent = eventSpec;
                    controlStateLocalReport = reportSpec;
                }

                {
                    var eventID = vidCfg.ControlStateRemote_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "ControlState", },
                        VIDArray = new[] { vidCfg.ControlState_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "ControlStateRemote", CEID = eventID, ReportSpec = reportSpec };

                    controlStateRemoteEvent = eventSpec;
                    controlStateRemoteReport = reportSpec;
                }

                {
                    var eventID = vidCfg.ProcessingStarted_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PreviousProcessState" },
                        VIDArray = new[] { vidCfg.PreviousProcessState_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "ProcessingStarted", CEID = eventID, ReportSpec = reportSpec };

                    processingStartedEvent = eventSpec;
                    processingStartedReport = reportSpec;
                }

                {
                    var eventID = vidCfg.ProcessingCompleted_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PreviousProcessState" },
                        VIDArray = new[] { vidCfg.PreviousProcessState_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "ProcessingCompleted", CEID = eventID, ReportSpec = reportSpec };

                    processingCompleteEvent = eventSpec;
                    processingCompleteReport = reportSpec;
                }

                {
                    var eventID = vidCfg.ProcessingStopped_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PreviousProcessState" },
                        VIDArray = new[] { vidCfg.PreviousProcessState_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "ProcessingStopped", CEID = eventID, ReportSpec = reportSpec };

                    processingStoppedEvent = eventSpec;
                    processingStoppedReport = reportSpec;
                }

                {
                    var eventID = vidCfg.ProcessingStateChange_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "ProcessState", "PreviousProcessState" },
                        VIDArray = new[] { vidCfg.ProcessState_SVID, vidCfg.PreviousProcessState_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "ProcessingStateChange", CEID = eventID, ReportSpec = reportSpec };

                    processingStateChangeEvent = eventSpec;
                    processingStateChangeReport = reportSpec;
                }

                {
                    var eventID = vidCfg.OperatorEquipmentConstantChange_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "ECID" },
                        VIDArray = new[] { vidCfg.ECID_DVID },
                    };
                    var eventSpec = new EventSpec() { Name = "OperatorEquipmentConstantChange", CEID = eventID, ReportSpec = reportSpec };

                    operatorECChangeEvent = eventSpec;
                    operatorECChangeReport = reportSpec;
                }

                {
                    var eventID = vidCfg.ProcessProgramChange_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PPChangeName", "PPChangeStatus" },
                        VIDArray = new[] { vidCfg.PPChangeName_DVID, vidCfg.PPChangeStatus_DVID },
                    };
                    var eventSpec = new EventSpec() { Name = "ProcessProgramChange", CEID = eventID, ReportSpec = reportSpec };

                    ppChangeEvent = eventSpec;
                    ppChangeReport = reportSpec;
                }

                {
                    var eventID = vidCfg.ProcessProgramSelected_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PPExecName" },
                        VIDArray = new[] { vidCfg.PPExecName_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "ProcessProgramSelected", CEID = eventID, ReportSpec = reportSpec };

                    ppSelectedEvent = eventSpec;
                    ppSelectedReport = reportSpec;
                }

                {
                    var eventID = vidCfg.MaterialReceived_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "Clock" },
                        VIDArray = new[] { vidCfg.Clock_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "MaterialReceived", CEID = eventID, ReportSpec = reportSpec };

                    materialReceivedEvent = eventSpec;
                    materialReceivedReport = reportSpec;
                }

                {
                    var eventID = vidCfg.MaterialRemoved_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "Clock" },
                        VIDArray = new[] { vidCfg.Clock_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "MaterialRemoved", CEID = eventID, ReportSpec = reportSpec };

                    materialRemovedEvent = eventSpec;
                    materialRemovedReport = reportSpec;
                }

                {
                    var eventID = vidCfg.MessageRecognition_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "Clock" },
                        VIDArray = new[] { vidCfg.Clock_SVID },
                    };
                    var eventSpec = new EventSpec() { Name = "MessageRecognition", CEID = eventID, ReportSpec = reportSpec };

                    messageRecognitionEvent = eventSpec;
                    messageRecognitionReport = reportSpec;
                }

                reportSpecList.SafeAddItems(equipmentOfflineReport, controlStateLocalReport, controlStateRemoteReport, processingStartedReport, processingCompleteReport, processingStoppedReport, processingStateChangeReport, operatorECChangeReport, ppChangeReport, ppSelectedReport, materialReceivedReport, materialRemovedReport, messageRecognitionReport);
                eventSpecList.SafeAddItems(equipmentOfflineEvent, controlStateLocalEvent, controlStateRemoteEvent, processingStartedEvent, processingCompleteEvent, processingStoppedEvent, processingStateChangeEvent, operatorECChangeEvent, ppChangeEvent, ppSelectedEvent, materialReceivedEvent, materialRemovedEvent, messageRecognitionEvent);
            }

            // build up the E87 eventSpecList and reportSpecList items here
            {
                var vidCfg = e087IDsConfig;

                EventSpec[] ltsEventSpecSet, cosmEventSpecSet, amsEventSpecSet, lrsEventSpecSet, lcasEventSpecSet;
                ReportSpec ltsReportSpec, cosmReportSpec, amsReportSpec, lrsReportSpec, lcasReportSpec;

                //lts
                {
                    var eventIDList = vidCfg.LTS_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "PortID", "LTS", "CID" },
                        VIDArray = new[] { vidCfg.LTS_PortID_DVID, vidCfg.LTS_PortTransferState_DVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"LTS_Transition{index + 1}", CEID = eventID, ReportSpec = reportSpec }).ToArray();

                    ltsEventSpecSet = eventSpecSet;
                    ltsReportSpec = reportSpec;
                }

                // cosm
                {
                    var eventIDList = vidCfg.COSM_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "PortID", "CID", "LocID", "SlotMap", "Reason" },
                        VIDArray = new[] { vidCfg.COSM_PortID_DVID, vidCfg.COSM_CarrierID_DVID, vidCfg.COSM_LocationID_DVID, vidCfg.COSM_SlotMap_DVID, vidCfg.COSM_Reason_DVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"COSM_Transition{index + 1}", CEID = eventID, ReportSpec = reportSpec }).ToArray();

                    cosmEventSpecSet = eventSpecSet;
                    cosmReportSpec = reportSpec;
                }

                // ams
                {
                    var eventIDList = vidCfg.AMS_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "PortID", "AMS" },
                        VIDArray = new[] { vidCfg.AMS_PortID_DVID, vidCfg.AMS_AccessMode_DVID },
                    };
                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"AMS_Transition{index + 1}", CEID = eventID, ReportSpec = reportSpec }).ToArray();

                    amsEventSpecSet = eventSpecSet;
                    amsReportSpec = reportSpec;
                }

                // lrs
                {
                    var eventIDList = vidCfg.LRS_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "PortID", "LRS" },
                        VIDArray = new[] { vidCfg.LRS_PortID_DVID, vidCfg.LRS_LoadPortReservationState_DVID },
                    };
                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"LRS_Transition{index + 1}", CEID = eventID, ReportSpec = reportSpec }).ToArray();

                    lrsEventSpecSet = eventSpecSet;
                    lrsReportSpec = reportSpec;
                }

                // lcas
                {
                    var eventIDList = vidCfg.LCAS_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "PortID", "LCAS", "CID" },
                        VIDArray = new[] { vidCfg.LCAS_PortID_DVID, vidCfg.LCAS_PortAssociationState_DVID, vidCfg.LCAS_CarrierID_DVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"LCAS_Transition{index + 1}", CEID = eventID, ReportSpec = reportSpec }).ToArray();

                    lcasEventSpecSet = eventSpecSet;
                    lcasReportSpec = reportSpec;
                }

                reportSpecList.SafeAddItems(ltsReportSpec, cosmReportSpec, amsReportSpec, lrsReportSpec, lcasReportSpec);
                new[] { ltsEventSpecSet, cosmEventSpecSet, amsEventSpecSet, lrsEventSpecSet, lcasEventSpecSet }.DoForEach(eventSpecSet => eventSpecList.AddRange(eventSpecSet));

                // carrierEvents: approaching complete, clamped, closed, location changed, opened, unclamped, id read fail, id reader available, id reader unablailable, unknown carrier event.

                EventSpec approachingCompleteEventSpec, clampedEventSpec, unclampedEventSpec, closedEventSpec, openedEventSpec, movedEventSpec, idReadFailedEventSpec, readerAvailableEventSpec, readerUnavailableEventSpec, unknownCIDEventSpec;
                ReportSpec approachingCompleteReportSpec, clampedReportSpec, unclampedReportSpec, closedReportSpec, openedReportSpec, movedReportSpec, idReadFailedReportSpec, readerAvailableReportSpec, readerUnavailableReportSpec, unknownCIDReportSpec;

                {
                    var eventID = vidCfg.CarrierApproachingCompleteEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "CID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_CarrierID_DVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpec = new EventSpec() { Name = "CarrierApproachingComplete", CEID = eventID, ReportSpec = reportSpec };

                    approachingCompleteEventSpec = eventSpec;
                    approachingCompleteReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.CarrierClampedEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "CID", "PortID", "LocID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_CarrierID_DVID, vidCfg.CarrierEvent_PortID_DVID, vidCfg.CarrierEvent_LocationID_DVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpec = new EventSpec() { Name = "", CEID = eventID, ReportSpec = reportSpec };

                    clampedEventSpec = eventSpec;
                    clampedReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.CarrierUnclampedEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "CID", "PortID", "LocID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_CarrierID_DVID, vidCfg.CarrierEvent_PortID_DVID, vidCfg.CarrierEvent_LocationID_DVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpec = new EventSpec() { Name = "CarrierUnclamped", CEID = eventID, ReportSpec = reportSpec };

                    unclampedEventSpec = eventSpec;
                    unclampedReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.CarrierClosedEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "CID", "PortID", "LocID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_CarrierID_DVID, vidCfg.CarrierEvent_PortID_DVID, vidCfg.CarrierEvent_LocationID_DVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpec = new EventSpec() { Name = "CarrierClosed", CEID = eventID, ReportSpec = reportSpec };

                    closedEventSpec = eventSpec;
                    closedReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.CarrierOpenedEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "CID", "PortID", "LocID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_CarrierID_DVID, vidCfg.CarrierEvent_PortID_DVID, vidCfg.CarrierEvent_LocationID_DVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpec = new EventSpec() { Name = "CarrierOpened", CEID = eventID, ReportSpec = reportSpec };

                    openedEventSpec = eventSpec;
                    openedReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.CarrierLocationChangeEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "CID", "PortID", "Matrix" },
                        VIDArray = new[] { vidCfg.CarrierEvent_CarrierID_DVID, vidCfg.CarrierEvent_PortID_DVID, vidCfg.CarrierLocationMatrix_SVID },
                        E039TypesArray = new[] { e087IDsConfig.E039CarrierObjectTypeName },
                    };
                    var eventSpec = new EventSpec() { Name = "CarrierLocationChange", CEID = eventID, ReportSpec = reportSpec };

                    movedEventSpec = eventSpec;
                    movedReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.CarrierIDReadFailEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PortID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_PortID_DVID },
                    };
                    var eventSpec = new EventSpec() { Name = "CarrierIDReadFail", CEID = eventID, ReportSpec = reportSpec };

                    idReadFailedEventSpec = eventSpec;
                    idReadFailedReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.IDReaderAvailableEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PortID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_PortID_DVID },
                    };
                    var eventSpec = new EventSpec() { Name = "IDReaderAvailable", CEID = eventID, ReportSpec = reportSpec };

                    readerAvailableEventSpec = eventSpec;
                    readerAvailableReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.IDReaderUnavailableEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PortID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_PortID_DVID },
                    };
                    var eventSpec = new EventSpec() { Name = "IDReaderUnavailable", CEID = eventID, ReportSpec = reportSpec };

                    readerUnavailableEventSpec = eventSpec;
                    readerUnavailableReportSpec = reportSpec;
                }

                {
                    var eventID = vidCfg.UnknownCarrierIDEvent_CEID;
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventID,
                        TagNameArray = new[] { "PortID" },
                        VIDArray = new[] { vidCfg.CarrierEvent_PortID_DVID },
                    };
                    var eventSpec = new EventSpec() { Name = "UnknownCarrierID", CEID = eventID, ReportSpec = reportSpec };

                    unknownCIDEventSpec = eventSpec;
                    unknownCIDReportSpec = reportSpec;
                }

                reportSpecList.SafeAddItems(approachingCompleteReportSpec, clampedReportSpec, unclampedReportSpec, closedReportSpec, openedReportSpec, movedReportSpec, idReadFailedReportSpec, readerAvailableReportSpec, readerUnavailableReportSpec, unknownCIDReportSpec);
                new[] { approachingCompleteEventSpec, clampedEventSpec, unclampedEventSpec, closedEventSpec, openedEventSpec, movedEventSpec, idReadFailedEventSpec, readerAvailableEventSpec, readerUnavailableEventSpec, unknownCIDEventSpec }.DoForEach(eventSpec => eventSpecList.Add(eventSpec));
            }

            // build up the E40 eventSpecList and reportSpecList items here
            {
                var vidCfg = e040IDsConfig;

                EventSpec[] prJobTransitionEventSpecSet;
                ReportSpec prJobTransitionReportSpec;

                {
                    var eventIDList = vidCfg.PRJob_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "PRJobID", "PRJobState" },
                        VIDArray = new[] { vidCfg.PRJob_Transition_PRJobID_DVID, vidCfg.PRJob_Transition_PRJobState_DVID },
                        E039TypesArray = new[] { e040IDsConfig.E039ProcessJobObjectTypeName },
                    };
                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"PRJob_Transition{index + 1}", CEID = eventID, ReportSpec = reportSpec }).ToArray();

                    prJobTransitionEventSpecSet = eventSpecSet;
                    prJobTransitionReportSpec = reportSpec;
                }

                reportSpecList.SafeAddItems(prJobTransitionReportSpec);
                new[] { prJobTransitionEventSpecSet }.DoForEach(eventSpecSet => eventSpecList.AddRange(eventSpecSet));
            }

            // build up the E94 eventSpecList and reportSpecList items here
            {
                var vidCfg = e094IDsConfig;

                EventSpec[] ctrlJobTransitionEventSpecSet;
                ReportSpec ctrlJobTransitionReportSpec;

                {
                    var eventIDList = vidCfg.CtrlJob_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "CtrlJobID" },
                        VIDArray = new[] { vidCfg.CtrlJob_Transition_CtrlJobID_DVID },
                        E039TypesArray = new[] { e094IDsConfig.E039ControlJobObjectTypeName },
                    };
                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"CtrlJob_Transition{index + 1}", CEID = eventID, ReportSpec = reportSpec }).ToArray();

                    ctrlJobTransitionEventSpecSet = eventSpecSet;
                    ctrlJobTransitionReportSpec = reportSpec;
                }

                reportSpecList.SafeAddItems(ctrlJobTransitionReportSpec);
                new[] { ctrlJobTransitionEventSpecSet }.DoForEach(eventSpecSet => eventSpecList.AddRange(eventSpecSet));
            }

            // build up the E90 eventSpecList and reportSpecList items here
            {
                var vidCfg = e090IDsConfig;

                EventSpec[] sosmTransitionEventSpecSet, slosmTransitionEventSpecSet;
                ReportSpec sosmTransitionReportSpecA, sosmTransitionReportSpecB, slosmTransitionReportSpec;

                {
                    var eventIDList = vidCfg.SOSM_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpecA = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "SubstID", "LotID", "LocID", "STS", "SPS", "Src", "Dest", "MatStat", "Usage", "Type" },
                        VIDArray = new[] { vidCfg.SOSM_Transition_SubstID_DVID, vidCfg.SOSM_Transition_LotID_DVID, vidCfg.SOSM_Transition_SubstLocID_DVID, vidCfg.SOSM_Transition_SubstState_DVID, vidCfg.SOSM_Transition_SubstProcState_DVID, vidCfg.SOSM_Transition_SubstSource_DVID, vidCfg.SOSM_Transition_SubstDestination_DVID, vidCfg.SOSM_Transition_SubstMtrlStatus_DVID, vidCfg.SOSM_Transition_SubstUsage_DVID, vidCfg.SOSM_Transition_SubstType_DVID },
                        E039TypesArray = new[] { e090IDsConfig.E039SubstrateObjectTypeName, e090IDsConfig.E039SubstLocObjectTypeName },
                    };

                    var reportSpecB = new ReportSpec()      // used for Trasnition 5 and 12
                    {
                        ReportID = eventIDList.SafeAccess(4),       // Transition 5
                        TagNameArray = reportSpecA.TagNameArray.ConcatItems("Hist").ToArray(),
                        VIDArray = reportSpecA.VIDArray.ConcatItems(vidCfg.SOSM_Transition_SubstHistory_DVID).ToArray(),
                        E039TypesArray = new[] { e090IDsConfig.E039SubstrateObjectTypeName, e090IDsConfig.E039SubstLocObjectTypeName },
                    };

                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"SOSM_Transition{index + 1}", CEID = eventID, ReportSpec = (index != 4 && index != 11) ? reportSpecA : reportSpecB }).ToArray();

                    sosmTransitionEventSpecSet = eventSpecSet;
                    sosmTransitionReportSpecA = reportSpecA;
                    sosmTransitionReportSpecB = reportSpecB;
                }

                {
                    var eventIDList = vidCfg.SLOSM_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true);
                    var reportSpec = new ReportSpec()
                    {
                        ReportID = eventIDList.SafeAccess(0),
                        TagNameArray = new[] { "LocID", "SubstID" },
                        VIDArray = new[] { vidCfg.SLOSM_Transition_SubstLocID_DVID, vidCfg.SLOSM_Transition_SubstLocSubstID_DVID },
                        E039TypesArray = new[] { e090IDsConfig.E039SubstLocObjectTypeName, e090IDsConfig.E039SubstrateObjectTypeName },
                    };
                    var eventSpecSet = eventIDList.Select((eventID, index) => new EventSpec() { Name = $"SLOSM_Transition{index + 1}", CEID = eventID, ReportSpec = reportSpec }).ToArray();

                    slosmTransitionEventSpecSet = eventSpecSet;
                    slosmTransitionReportSpec = reportSpec;
                }

                reportSpecList.SafeAddItems(sosmTransitionReportSpecA, sosmTransitionReportSpecB, slosmTransitionReportSpec);
                new[] { sosmTransitionEventSpecSet, slosmTransitionEventSpecSet }.DoForEach(eventSpecSet => eventSpecList.AddRange(eventSpecSet));
            }

            {
                Dictionary<ValueContainer, EventSpec> eventSpecDictionaryBuilder = new Dictionary<ValueContainer, EventSpec>();

                eventSpecList.DoForEach(eventSpec => eventSpecDictionaryBuilder[eventSpec.CEID] = eventSpec);
                eventIDToEventSpecDictionary = eventSpecDictionaryBuilder.ConvertToReadOnly();
            }
        }

        List<EventSpec> eventSpecList = new List<EventSpec>();
        ReadOnlyIDictionary<ValueContainer, EventSpec> eventIDToEventSpecDictionary = new ReadOnlyIDictionary<ValueContainer, EventSpec>();
        List<ReportSpec> reportSpecList = new List<ReportSpec>();

        /// <summary>S1/F13: Establish Communications Request (CR) handler</summary>
        private void RootAsyncS1F13Handler(IMessage mesg)
        {
            var bodyVC = mesg.GetDecodedContents();

            eqpInfoNVS.SetValue("MDLN", bodyVC.SafeAccess(0).GetValue<string>(rethrow: true).MapNullToEmpty());
            eqpInfoNVS.SetValue("SOFTREV", bodyVC.SafeAccess(1).GetValue<string>(rethrow: true).MapNullToEmpty());

            ivaEqpInfoNVS.Set(eqpInfoNVS);

            if (mesg.SF.ReplyExpected)
            {
                mesg.SetReply(mesg.CreateReply().SetContentBytes(new L(new Bi((byte)MosaicLib.Semi.E005.COMMACK.Accepted), new L())));      // S1F14 Establish Communications Request Acknowledge (CRA): host sends empty list for item 2 (nomninally MDLN, SOFTREV)
            }
        }

        /// <summary>S6/F11: Event Report Send (ERS) handler</summary>
        private void RootAsyncS6F11Handler(IMessage mesg)
        {
            if (mesg.SF.ReplyExpected)
                mesg.SetReply(mesg.CreateReply());

            var bodyVC = mesg.GetDecodedContents();
            var dataID = bodyVC.SafeAccess(0).GetValue<uint>(rethrow: true);
            var ceid = bodyVC.SafeAccess(1);
            var eventSpec = eventIDToEventSpecDictionary.SafeTryGetValue(ceid);
            var listOfReportBodiesVC = bodyVC.SafeAccess(2);

            NamedValueSet nvs = new NamedValueSet();

            if (eventSpec != null)
            {
                foreach (var kvp1 in eventSpec.ReportSpecArray.Select((reportSpec, index) => KVP.Create(index, reportSpec)))
                {
                    var reportSpec = kvp1.Value;

                    var reportBodyVC = listOfReportBodiesVC.SafeAccess(kvp1.Key);
                    var reportIDVC = reportBodyVC.SafeAccess(0);
                    var vidValues = reportBodyVC.SafeAccess(1).GetValue<IList<ValueContainer>>(rethrow: true);

                    foreach (var kvp2 in reportSpec.TagNameArray.Zip(vidValues, (s, vc) => KVP.Create(s, vc)))
                    {
                        nvs.SetValue(kvp2.Key, kvp2.Value);
                    }
                }

                nvs.MakeReadOnly().BuildDictionary();
                EventReport eventReport = new EventReport() { EventSpec = eventSpec, NVS = nvs };

                Log.Debug.Emit("Received Event {0}", eventReport);

                EventReportNotificationList.Notify(eventReport);

                // detect and fire e039 things changed events.
                foreach (var reportSpec in eventSpec.ReportSpecArray)
                {
                    if (!reportSpec.E039TypesArray.IsNullOrEmpty())
                        AsyncProcessCommonE039TypeEvents(reportSpec.E039TypesArray, nvs);
                }

                int portID = nvs["PortID"].VC.GetValue<int>(rethrow: false);
                var portEventReportNotificationList = PerPortEventReportNotificationList.SafeAccess(portID - 1);
                if (portEventReportNotificationList != null)
                    portEventReportNotificationList.Notify(eventReport);
                else
                    NoPortEventReportNotificationList.Notify(eventReport);
            }
            else
            {
                Log.Debug.Emit("Received unrecognized event: CEID:{0} {1}", ceid, listOfReportBodiesVC);
            }
        }

        private void AsyncProcessCommonE039TypeEvents(string[] e039TypesArray, NamedValueSet nvs)
        {
            foreach (var e039Type in e039TypesArray)
            {
                if (e039Type == e087IDsConfig.E039CarrierObjectTypeName)
                {
                    var cid = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    AsyncNoteE039ObjectTouched(cid, e039Type);
                }
                else if (e039Type == e087IDsConfig.E039LoadPortObjectTypeName)
                {
                    int portID = nvs["PortID"].VC.GetValue<int>(rethrow: false);
                    var portName = (portID > 0) ? "LP{0}".CheckedFormat(portID) : string.Empty;
                    AsyncNoteE039ObjectTouched(portName, e039Type);
                }
                else if (e039Type == e040IDsConfig.E039ProcessJobObjectTypeName)
                {
                    var prJobID = nvs["PRJobID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    AsyncNoteE039ObjectTouched(prJobID, e039Type);
                }
                else if (e039Type == e094IDsConfig.E039ControlJobObjectTypeName)
                {
                    var ctrlJobID = nvs["CtrlJobID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    AsyncNoteE039ObjectTouched(ctrlJobID, e039Type);
                }
                else if (e039Type == e090IDsConfig.E039SubstLocObjectTypeName)
                {
                    var locID = nvs["LocID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    AsyncNoteE039ObjectTouched(locID, e039Type);
                }
                else if (e039Type == e090IDsConfig.E039SubstrateObjectTypeName)
                {
                    var substID = nvs["SubstID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    AsyncNoteE039ObjectTouched(substID, e039Type);
                }
            }
        }

        public EventHandlerNotificationList<EventReport> EventReportNotificationList;
        public EventHandlerNotificationList<EventReport>[] PerPortEventReportNotificationList;
        public EventHandlerNotificationList<EventReport> NoPortEventReportNotificationList;

        private void AsyncHandleNoPortEventReport(object source, EventReport eventReport)
        {
            lock (pendingEventReportMutex)
            {
                pendingEventReportList.Add(eventReport);
                pendingEventReportListCount = pendingEventReportList.Count;
            }

            this.Notify();
        }

        private object pendingEventReportMutex = new object();
        private List<EventReport> pendingEventReportList = new List<EventReport>();
        private volatile int pendingEventReportListCount;

        private void ServicePendingEvents()
        {
            if (pendingEventReportListCount == 0)
                return;

            var eventReportArray = EmptyArrayFactory<EventReport>.Instance;

            lock (pendingEventReportMutex)
            {
                eventReportArray = pendingEventReportList.ToArray();
                pendingEventReportList.Clear();
            }

            eventReportArray.DoForEach(eventReport => HandleEventReport(eventReport));
        }

        private void HandleEventReport(EventReport eventReport)
        {
            if (eventReport.NVS.Contains("ControlState"))
                eqpInfoNVS.SetValue("ControlState", eventReport.NVS["ControlState"].VC.GetValue<ControlState>(rethrow: true));

            if (eventReport.NVS.Contains("ProcessState"))
                eqpInfoNVS.SetValue("ProcessState", eventReport.NVS["ProcessState"].VC);
            else if (eventReport.EventSpec.CEID.Equals(e030IDsConfig.ProcessingStarted_CEID))
                eqpInfoNVS.SetValue("ProcessState", "Started");
            else if (eventReport.EventSpec.CEID.Equals(e030IDsConfig.ProcessingCompleted_CEID))
                eqpInfoNVS.SetValue("ProcessState", "Complete");
            else if (eventReport.EventSpec.CEID.Equals(e030IDsConfig.ProcessingStopped_CEID))
                eqpInfoNVS.SetValue("ProcessState", "Stopped");

            if (eventReport.NVS.Contains("PrevProcessState"))
                eqpInfoNVS.SetValue("PrevProcessState", eventReport.NVS["PrevProcessState"].VC);

            if (eventReport.EventSpec.CEID.Equals(e030IDsConfig.MaterialReceived_CEID))
                eqpInfoNVS.SetValue("Material", "Received");
            else if (eventReport.EventSpec.CEID.Equals(e030IDsConfig.MaterialRemoved_CEID))
                eqpInfoNVS.SetValue("Material", "Removed");
            else if (eventReport.EventSpec.CEID.Equals(e030IDsConfig.MessageRecognition_CEID))
                HandleTermServicesMessageRecognitionEvent(eventReport);
            else if (eventReport.EventSpec.CEID.Equals(e030IDsConfig.OperatorEquipmentConstantChange_CEID))
                HandleOperatorECChangeEvent(eventReport);

            ivaEqpInfoNVS.Set(eqpInfoNVS);
        }

        #endregion

        #region Terminal services (RootAsyncS10F1Handler, HandleTermServicesMessageRecognitionEvent)

        /// <summary>S10F1 Terminal Request (TRN) handler.</summary>
        private void RootAsyncS10F1Handler(IMessage mesg)
        {
            var bodyVC = mesg.GetDecodedContents();
            var termID = bodyVC.SafeAccess(0).GetValue<byte>(rethrow: true);
            var textVC = bodyVC.SafeAccess(1);

            var ackc10 = ACKC10.Invalid;
            if (!termID.IsInRange(e030IDsConfig.TermMesgToHostMinTermID, e030IDsConfig.TermMesgToHostMaxTermID))
            {
                Log.Error.Emit("Received Terminal Message to host with invalid Terminal ID [{0}, Min:{1}, Max:{2}]", mesg, e030IDsConfig.TermMesgToHostMinTermID, e030IDsConfig.TermMesgToHostMaxTermID);

                ackc10 = ACKC10.TerminalNotAvailable;
            }
            else if (ivaTermMesgToHostAvailable.Update().Value)
            {
                Log.Warning.Emit("Received Terminal Message to host before prior message has been acknowledged [{0}]", mesg);

                ackc10 = ACKC10.MessageWillNotBeDisplayed;
            }
            else
            {
                Log.Info.Emit("Received Terminal Message to host [{0}]", mesg);

                ivaTermMesgToHostBody.Set(textVC);
                ivaTermMesgToHostAvailable.Set(true);

                ackc10 = ACKC10.AcceptedForDisplay;
            }

            mesg.SetReply(mesg.CreateReply().SetContentBytes(ValueContainer.Create(ackc10, ContainerStorageType.Bi)));
            return;
        }

        private void HandleTermServicesMessageRecognitionEvent(EventReport eventReport)
        {
            Log.Info.Emit("Received MessageRecognition event [{0}]", eventReport);

            ivaTermMesgToEqpPending.Set(false);
        }

        #endregion

        #region SVID trace (HandleSVIDServiceRequest, SetSVIDScanEnable, RootAsyncS6F1Handler)

        private IValuesInterconnection SVID_IVI = IVIRegistration.Instance.FindIVI("SVID_IVI", addNewTableIfMissing: true);
        private HashSet<ValueContainer> SVID_ExcludeList = new HashSet<ValueContainer>();
        private VCB[] svidTraceVCBArray = EmptyArrayFactory<VCB>.Instance;
        private IValueAccessor[] svidTraceIVAArray = EmptyArrayFactory<IValueAccessor>.Instance;
        private ValueContainer svidTraceTRID = ValueContainer.Create(1234567u);
        private bool svidTraceIsOn;

        private string HandleSVIDServiceRequest(IProviderFacet ipf, string serviceName, string scannerRest, INamedValueSet npv)
        {
            switch (scannerRest)
            {
                case "Refresh": return SetSVIDScanEnable();
                case "SetScanEnable": return SetSVIDScanEnable(npv["Enable"].VC.GetValue<bool>(rethrow: true));
                default: return "Internal: SVID request not recognized in '{0}'".CheckedFormat(serviceName);
            }
        }

        private string SetSVIDScanEnable(bool? enable = null)
        {
            if (svidTraceVCBArray.IsNullOrEmpty() || svidTraceIVAArray.IsNullOrEmpty())
            {
                SVID_ExcludeList = new HashSet<ValueContainer>()
                {
                    e030IDsConfig.AlarmsEnabled_SVID,
                    e030IDsConfig.EventsEnabled_SVID,
                };

                var includeSVIArray = svidInfoArray.Where(si => !SVID_ExcludeList.Contains(si.SVID)).ToArray();
                var takeNumItems = e030IDsConfig.SVIDTraceMaxItemCount.MapDefaultTo(includeSVIArray.Length);

                svidTraceVCBArray = includeSVIArray.Take(takeNumItems).Select(svi => new VCB(svi.SVID)).ToArray();

                svidTraceIVAArray = includeSVIArray.Take(takeNumItems).Select(svi => SVID_IVI.GetValueAccessor(svi.IVAName).SetMetaData(new NamedValueSet()
                    {
                        { "SVID", svi.SVID }
                    }.ConditionalSetValue("Units", svi.Units.IsNeitherNullNorEmpty(), svi.Units)
                    )).ToArray();

                // set the initial SVID iva values.
                var svidValues = ReadSVIDValues(includeSVIArray.Select(svi => svi.SVID).ToArray());

                svidTraceIVAArray.Zip(svidValues, (iva, vc) => iva.VC = vc).DoForEach();
                SVID_IVI.Set(svidTraceIVAArray);
            }

            bool turnScanOn = (enable == true);
            bool turnScanOff = (enable == false);
            bool refresh = (enable == null);

            var traceWillBeOn = (svidTraceIsOn || turnScanOn) && !turnScanOff;
            var traceInterval = (traceWillBeOn || refresh) ? e030IDsConfig.SVIDTraceInterval : TimeSpan.Zero;

            var DSPER = new A("{0:d2}{1:d2}{2:d2}{3:d2}".CheckedFormat(traceInterval.Hours, traceInterval.Minutes, traceInterval.Seconds, (traceInterval.Milliseconds + 9) / 10));
            var TOTSMP = new I4(turnScanOff ? 0 : (traceWillBeOn ? e030IDsConfig.SVIDTraceTOTSMP : 1));
            var REPGSZ = new I4(1);

            IMessage s2F23mesg;

            if (turnScanOff)
                s2F23mesg = port.CreateMessage("S2/F23[W]").SetContentBytes(new L(new VCB(svidTraceTRID), DSPER, TOTSMP, REPGSZ, new L()));
            else
                s2F23mesg = port.CreateMessage("S2/F23[W]").SetContentBytes(new L(new VCB(svidTraceTRID), DSPER, TOTSMP, REPGSZ, new L(svidTraceVCBArray)));

            string ec = s2F23mesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                var tiaack = s2F23mesg.Reply.GetDecodedContents().GetValue<TIAACK>(rethrow: true);

                if (tiaack != TIAACK.EverythingCorrect && (!turnScanOff || tiaack < TIAACK.FirstUseSpecificCode))
                    ec = "{0} request failed: tiaack:{1} [{2}]".CheckedFormat(s2F23mesg.SF, tiaack, s2F23mesg.Reply);
            }

            if (ec.IsNullOrEmpty())
            {
                if (turnScanOff)
                    svidTraceIsOn = false;
                else if (turnScanOn)
                    svidTraceIsOn = true;

                if (ivaSVIDsScanEnabled.Update().Value != svidTraceIsOn)
                    ivaSVIDsScanEnabled.Set(svidTraceIsOn);
            }

            return ec;
        }

        /// <summary>S6/F11: Trace Data Send (TDS) handler</summary>
        private void RootAsyncS6F1Handler(IMessage mesg)
        {
            var vcBody = mesg.GetDecodedContents(throwOnException: true).GetValue<ReadOnlyIList<ValueContainer>>(rethrow: true);

            S6F1Item s6f1Item = new S6F1Item()
            {
                mesg = mesg,
                vcBody = vcBody,
                TRID = vcBody[0],
                SMPLN = vcBody[1].GetValue<long>(rethrow: false),
                STIM = vcBody[2].GetValue<string>(rethrow: false),
                roSVVCList = vcBody[3].GetValue<ReadOnlyIList<ValueContainer>>(rethrow: true),
            };

            bool valid = (s6f1Item.TRID.Equals(svidTraceTRID));

            if (mesg.SF.ReplyExpected)
                mesg.SetReply(mesg.CreateReply().SetContentBytes(new Bi((byte)ACKC6.Accepted)), isHighRateReply: true);

            if (valid)
            {
                var priorContents = System.Threading.Interlocked.Exchange(ref pendingS6F1Item, s6f1Item);

                if (priorContents != null)
                    Log.Debug.Emit("TraceData overrun: skipped prior {0}", priorContents);

                Notify();
            }
            else
            {
                Log.Debug.Emit("{0} is not valid [expected TRID {1}]", s6f1Item, svidTraceTRID);
            }
        }

        class S6F1Item
        {
            public IMessage mesg;
            public ReadOnlyIList<ValueContainer> vcBody;
            public ValueContainer TRID;
            public long SMPLN;
            public string STIM;
            public ReadOnlyIList<ValueContainer> roSVVCList;

            public override string ToString()
            {
                return "{0} TRID:{1} SMPLN:{2} STIM:{3} count:{4}".CheckedFormat(mesg.SF, TRID, SMPLN, STIM, roSVVCList.Count);
            }
        }

        volatile S6F1Item pendingS6F1Item = null;

        private void ServiceS6F1Queue()
        {
            if (pendingS6F1Item == null)
                return;

            S6F1Item item = System.Threading.Interlocked.Exchange(ref pendingS6F1Item, null);

            if (item != null)
            {
                svidTraceIVAArray.Zip(item.roSVVCList, (iva, vc) => iva.VC = vc).DoForEach();
                SVID_IVI.Set(svidTraceIVAArray);

                ivaSVIDsSMPLN.Set(item.SMPLN);
                ivaSVIDsSTIME.Set(item.STIM);
            }
        }

        #endregion

        #region ECID support - view only (HandleECIDServiceRequest, RefreshECIDValues)

        private IValuesInterconnection ECID_IVI = IVIRegistration.Instance.FindIVI("ECID_IVI", addNewTableIfMissing: true);
        private IValueAccessor[] ECID_IVAArray = EmptyArrayFactory<IValueAccessor>.Instance;

        private string HandleECIDServiceRequest(IProviderFacet ipf, string serviceName, string scannerRest, INamedValueSet npv)
        {
            switch (scannerRest)
            {
                case "Refresh": return RefreshECIDValues();
                default: return "Internal: ECID request not recognized in '{0}'".CheckedFormat(serviceName);
            }
        }

        private string RefreshECIDValues()
        {
            if (ECID_IVAArray.IsNullOrEmpty())
            {
                ECID_IVAArray = ecidInfoArray.Select(ei => ECID_IVI.GetValueAccessor(ei.IVAName).SetMetaData(new NamedValueSet()
                        {
                            { "ECID", ei.ECID },
                        }
                        .ConditionalSetValue("Min", !ei.Min.IsNullOrEmpty, ei.Min)
                        .ConditionalSetValue("Max", !ei.Max.IsNullOrEmpty, ei.Max)
                        .ConditionalSetValue("Default", !ei.Default.IsNullOrEmpty, ei.Default)
                        .ConditionalSetValue("Units", ei.Units.IsNeitherNullNorEmpty(), ei.Units)
                        )).ToArray();
            }

            var mesg = port.CreateMessage("S2/F13[W]").SetContentBytes(new L(ecidInfoArray.Select(ei => new VCB(ei.ECID))));
            string ec = mesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                var ecVCs = mesg.Reply.GetDecodedContents().GetValue<IList<ValueContainer>>(rethrow: true);

                ECID_IVAArray.Zip(ecVCs, (iva, vc) => iva.VC = vc).DoForEach();
                ECID_IVI.Set(ECID_IVAArray);
            }

            return ec;
        }

        private void HandleOperatorECChangeEvent(EventReport eventReport)
        {
            Log.Info.Emit("Received Operator EC Change event [{0}]", eventReport);

            var ecid = eventReport.NVS["ECID"].VC;
            var ecInfo = ecidToInfoDictionary.SafeTryGetValue(ecid);

            if (ecInfo != null)
            {
                var mesg = port.CreateMessage("S2/F13[W]").SetContentBytes(new L(new VCB(ecid)));
                string ec = mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                {
                    var ecVCs = mesg.Reply.GetDecodedContents().GetValue<IList<ValueContainer>>(rethrow: true);
                    var ecVC = ecVCs.SafeAccess(0);

                    var ecIVA = ECID_IVI.GetValueAccessor(ecInfo.IVAName);
                    ecIVA.Set(ecVC);
                }
            }
        }

        #endregion

        #region ALID support

        private class ALIDInfo
        {
            public ALIDInfo()
            { }

            public ALIDInfo(ValueContainer tupleListVC)
            {
                ALCD alcd = tupleListVC.SafeAccess(0).GetValue<ALCD>(rethrow: false);
                IsSet = ((alcd & ALCD.IsSet) != 0);
                ALCD = alcd & ~ALCD.IsSet;
                ALID = tupleListVC.SafeAccess(1);
                ALTX = tupleListVC.SafeAccess(2).GetValue<string>(rethrow: true).Sanitize();

                Name = ALTX.Split(new[] { ':', ' ' }, 2).SafeAccess(0).MapNullToEmpty();
            }

            public string Name;
            public bool IsSet;
            public ALCD ALCD;
            public ValueContainer ALID;
            public string ALTX;

            public string IVAName
            {
                get
                {
                    if (Name.IsNeitherNullNorEmpty())
                        return "{0,-10}{1}".CheckedFormat(ALID.ValueAsObject, Name);
                    else
                        return "{0}".CheckedFormat(ALID.ValueAsObject);
                }
            }

            public override string ToString()
            {
                return "ALID {0} {1} '{2}'".CheckedFormat(ALID, ALCD, ALTX);
            }
        }

        private IValuesInterconnection ALID_IVI = IVIRegistration.Instance.FindIVI("ALID_IVI", addNewTableIfMissing: true);
        private IDictionaryWithCachedArrays<ValueContainer, ALIDTracker> alidToTrackerDictionary = new IDictionaryWithCachedArrays<ValueContainer, ALIDTracker>();

        private class ALIDTracker
        {
            public ALIDInfo info;
            public IValueAccessor iva;
            public NamedValueSet nvs;
        }

        private string EnableAllAlarms(bool enable = true)
        {
            string ec = string.Empty;
            ValueContainer firstALID = default(ValueContainer);

            // s5f5 - ALIDs
            if (ec.IsNullOrEmpty())
            {
                var s5f5Mesg = port.CreateMessage("S5/F5W").SetContentBytes(new L());

                ec = s5f5Mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                {
                    var resultList = s5f5Mesg.Reply.GetDecodedContents().GetValue<IList<ValueContainer>>(rethrow: true);

                    var aiUpdateSet = resultList.Select(tupleListVC => new ALIDInfo(tupleListVC)).Where(ai => ai.IsSet || ai.ALTX.IsNeitherNullNorEmpty()).ToArray();
                    if (aiUpdateSet.All(ai => ai.ALID.cvt.IsInteger()))
                        aiUpdateSet = aiUpdateSet.OrderBy(ai => ai.ALID.GetValue<long>(rethrow: false)).ToArray();

                    aiUpdateSet.DoForEach(ai => AsynchHandleRxALIDInfo(ai));

                    firstALID = aiUpdateSet.FirstOrDefault()?.ALID ?? default(ValueContainer);
                }
            }

            if (ec.IsNullOrEmpty())
            {
                ValueContainer alidSetSpec;
                switch (firstALID.cvt)
                {
                    case ContainerStorageType.I4: alidSetSpec = ValueContainer.Create(new int[0]); break;
                    case ContainerStorageType.U4: alidSetSpec = ValueContainer.Create(new uint[0]); break;
                    default: alidSetSpec = ValueContainer.Create(new uint[0]); break;
                }

                var mesg = port.CreateMessage("S5F3W").SetContentBytes(new L(new Bi((byte)ALEN.Enable), new VCB(alidSetSpec)));

                ec = mesg.Send().Run();

                if (ec.IsNullOrEmpty())
                {
                    var ackc5 = mesg.Reply.GetDecodedContents().SafeAccess(0).GetValue<ACKC5>(rethrow: false);
                    if (ackc5 != ACKC5.Accepted)
                        ec = "{0} was not accepted: ackc5:{1} [{2}]".CheckedFormat(mesg, ackc5, mesg.Reply);
                }
            }

            return ec;
        }

        /// <summary>S5F1 Alarm Report Send (ARS) handler.</summary>
        private void RootAsyncS5F1Handler(IMessage mesg)
        {
            if (mesg.SF.ReplyExpected)
                mesg.SetReply(mesg.CreateReply());

            var bodyVC = mesg.GetDecodedContents();

            ALIDInfo mesgALIDInfo = new ALIDInfo(bodyVC);

            AsynchHandleRxALIDInfo(mesgALIDInfo);
        }

        private void AsynchHandleRxALIDInfo(ALIDInfo mesgALIDInfo)
        {
            lock (alidToTrackerDictionary)
            {
                var at = alidToTrackerDictionary.SafeTryGetValue(mesgALIDInfo.ALID);

                if (at == null)
                {
                    at = new ALIDTracker()
                    {
                        info = mesgALIDInfo,
                        iva = ALID_IVI.GetValueAccessor(mesgALIDInfo.IVAName),
                        nvs = new NamedValueSet()
                    };
                    alidToTrackerDictionary[at.info.ALID] = at;
                }

                at.nvs.SetValue("IsSet", mesgALIDInfo.IsSet);
                at.nvs.SetValue("ALCD", mesgALIDInfo.ALCD);
                at.nvs.SetValue("ALTX", mesgALIDInfo.ALTX);
                at.iva.Set(at.nvs);
            }
        }

        #endregion

        #region PJEvent and PJAlert handlers (RootAsyncS16F7Handler, RootAsyncS16F9Handler)

        /// <summary>S16/F7: Process Job Alert Notify (PRJA) handler</summary>
        private void RootAsyncS16F7Handler(IMessage mesg)
        {
            if (mesg.SF.ReplyExpected)
                mesg.SetReply(mesg.CreateReply());

            var bodyVC = mesg.GetDecodedContents();

            var prJobID = bodyVC.SafeAccess(1).GetValue<string>(rethrow: true);
            var prJobMilestone = bodyVC.SafeAccess(2).GetValue<PRJOBMILESTONE>(rethrow: true);
            var resultVC = bodyVC.SafeAccess(3);
            var acka = resultVC.SafeAccess(0).GetValue<ACKA>(rethrow: true);

            if (acka == ACKA.True)
                Log.Info.Emit("PRJA '{0}' {1} ACKA {2}", prJobID, prJobMilestone, resultVC.ToStringSML());
            else
                Log.Warning.Emit("PRJA '{0}' {1} reported issue ACKA {2}", prJobID, prJobMilestone, resultVC.ToStringSML());
        }

        /// <summary>S16/F9: Process Job Event Notify (PRJE) handler</summary>
        private void RootAsyncS16F9Handler(IMessage mesg)
        {
            if (mesg.SF.ReplyExpected)
                mesg.SetReply(mesg.CreateReply());

            var bodyVC = mesg.GetDecodedContents();

            var prJobEventID = bodyVC.SafeAccess(0).GetValue<PREVENTID>(rethrow: true);
            var prJobID = bodyVC.SafeAccess(2).GetValue<string>(rethrow: true);
            var vidsVC = bodyVC.SafeAccess(3);

            Log.Info.Emit("PRJE '{0}' {1} VIDs: {2}", prJobID, prJobEventID, vidsVC.ToStringSML());
        }

        #endregion

        #region E039 support

        private void CreateE039Support()
        {
            E039BasicTablePartConfig e039Config = new E039BasicTablePartConfig("E039TableMgr")
            {
                DefaultTypeSetSpec = new E039TableTypeSetPersistSpecItem()
                {
                    PersistObjFileRingConfig = null,
                    ReferenceSet = new MosaicLib.Modular.Interconnect.Sets.ReferenceSet<E039Object>(new MosaicLib.Modular.Interconnect.Sets.SetID("E039Set"), 1000),
                },
            };

            e039TableMgr = new E039BasicTablePart(e039Config).RunGoOnlineActionInline();

            AddMainThreadStoppingAction(() => e039TableMgr.StopPart());
            AddExplicitDisposeAction(() => Fcns.DisposeOfObject(ref e039TableMgr));

            ivaE039TypeNameSet = ivi.GetValueAccessor($"{PartID}.E039.TypeNameSet").Set(EmptyArrayFactory<string>.Instance);
        }

        IE039TableUpdater e039TableMgr;
        IValueAccessor ivaE039TypeNameSet;

        public void AsyncNoteE039ObjectTouched(string e039ObjInstanceName, string e039ObjTypeName)
        {
            AsyncNoteE039ObjectTouched(new E039ObjectID(e039ObjInstanceName, e039ObjTypeName, e039TableMgr));
        }

        public void AsyncNoteE039ObjectTouched(E039ObjectID objID)
        {
            lock (e039AsynchTouchedObjectIDSetMutex)
            {
                if (e039AsynchTouchedObjectIDSet == null)
                {
                    e039AsynchTouchedObjectIDSet = (e039AsyncUsedSetANext) ? e039AsyncTouchedObjectIDSetA : e039AsyncTouchedObjectIDSetB;
                    e039AsyncUsedSetANext = !e039AsyncUsedSetANext;
                }

                e039AsynchTouchedObjectIDSet.Add(objID);
            }
        }

        private object e039AsynchTouchedObjectIDSetMutex = new object();
        volatile HashSet<E039ObjectID> e039AsynchTouchedObjectIDSet;
        bool e039AsyncUsedSetANext = true;
        HashSet<E039ObjectID> e039AsyncTouchedObjectIDSetA = new HashSet<E039ObjectID>();
        HashSet<E039ObjectID> e039AsyncTouchedObjectIDSetB = new HashSet<E039ObjectID>();

        QpcTimer e039ServiceTimer = new QpcTimer() { TriggerInterval = (0.5).FromSeconds(), AutoReset = true }.Start();

        private string Service039Support(bool forceFullUpdate = false)
        {
            if (!forceFullUpdate && !e039ServiceTimer.IsTriggered || e039AsynchTouchedObjectIDSet == null)
                return string.Empty;

            ICollection<E039ObjectID> capturedTouchedObjectIDSet = null;

            lock (e039AsynchTouchedObjectIDSetMutex)
            {
                capturedTouchedObjectIDSet = e039AsynchTouchedObjectIDSet;
                e039AsynchTouchedObjectIDSet = null;
            }

            if (capturedTouchedObjectIDSet == null)
                return string.Empty;

            string ec = string.Empty;

            IEnumerator<E039ObjectID> setEnum = capturedTouchedObjectIDSet.GetEnumerator();
            for (; ; )
            {
                if (!setEnum.MoveNext())
                    break;

                var objID = setEnum.Current;

                if (objID.Type.IsNullOrEmpty())
                {
                    // read all of the types and post full updates for each one back into the AsynchSet
                    var s14f5Mesg = port.CreateMessage("S14/F5[W]").SetContentBytes(new A());       // asking for the set of supported types is not high rate

                    ec = s14f5Mesg.Send().Run();

                    if (ec.IsNullOrEmpty())
                    {
                        var replyContentVC = s14f5Mesg.Reply.GetDecodedContents();
                        var typeNameArray = replyContentVC.SafeAccess(0).GetValue<string[]>(rethrow: false).MapNullToEmpty();

                        Log.Debug.Emit("Received E039 types: {0} [{1}]", string.Join(",", typeNameArray), s14f5Mesg);

                        ivaE039TypeNameSet.Set(typeNameArray);

                        typeNameArray.DoForEach(typeName => AsyncNoteE039ObjectTouched(new E039ObjectID(string.Empty, typeName)));
                    }
                }
                else if (objID.Name.IsNullOrEmpty())
                {
                    // read all of these objects
                    var s14F1Mesg = port.CreateMessage("S14/F1[W]").SetIsHighRate().SetContentBytes(new L(new A(), new A(objID.Type), new L(), new L(), new L()));

                    ec = s14F1Mesg.Send().Run();

                    List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();
                    if (ec.IsNullOrEmpty())
                    {
                        HashSet<string> existingTableSet = new HashSet<string>(e039TableMgr.GetObjects(typeFilter: typeName => typeName == objID.Type).Select(obj => obj.ID.Name));

                        var replyContentVC = s14F1Mesg.Reply.GetDecodedContents();
                        var objListVC = replyContentVC.SafeAccess(0);
                        var objAckList = replyContentVC.SafeAccess(1);
                        var objAck = objAckList.SafeAccess(0).GetValue<OBJACK>(rethrow: true);
                        var objErrorList = objAckList.SafeAccess(1);

                        if (objAck == OBJACK.Success && objErrorList.SafeAccess(0).IsNullOrEmpty)
                        {
                            var objInfoListVCSet = objListVC.GetValue<IList<ValueContainer>>(rethrow: false).MapNullToEmpty();
                            if (objInfoListVCSet.SafeCount() > 0)
                                Log.Debug.Emit("Received E039 {0} objs: {1} [{2}]", objID.Type, String.Join(",", objInfoListVCSet.Select(infoListVC => infoListVC.SafeAccess(0).GetValue<string>(rethrow: false).MapNullToEmpty())), s14F1Mesg);
                            else
                                Log.Debug.Emit("Received no E039 {0} objs [{1}]", objID.Type, s14F1Mesg);

                            foreach (var objInfoListVC in objInfoListVCSet)
                            {
                                var readObjName = objInfoListVC.SafeAccess(0).GetValue<string>(rethrow: false).MapNullToEmpty();
                                var readObjID = new E039ObjectID(readObjName, objID.Type);
                                var nvs = objInfoListVC.SafeAccess(1).ConvertToNamedValueSet(asReadOnly: true);

                                existingTableSet.Remove(readObjName);

                                var obj = e039TableMgr.GetObject(readObjID);
                                if (obj == null)
                                    updateItemList.Add(new E039UpdateItem.AddObject(readObjID, nvs, mergeBehavior: NamedValueMergeBehavior.Replace, ifNeeded: true));
                                else if (!obj.Attributes.Equals(nvs))
                                    updateItemList.Add(new E039UpdateItem.SetAttributes(readObjID, nvs, mergeBehavior: NamedValueMergeBehavior.Replace));
                            }
                        }
                        else
                        {
                            ec = "GetTableAttr {0} failed with unexpected error: objAck:{1} {2}".CheckedFormat(s14F1Mesg, objAck, s14F1Mesg.Reply);
                        }

                        foreach (var oldObjName in existingTableSet)
                        {
                            updateItemList.Add(new E039UpdateItem.RemoveObject(new E039ObjectID(oldObjName, objID.Type)));
                        }
                    }

                    if (ec.IsNullOrEmpty() && updateItemList.Count > 0)
                        ec = e039TableMgr.Update(updateItemList.ToArray()).Run();
                }
                else
                {
                    var s14F1Mesg = port.CreateMessage("S14/F1[W]").SetIsHighRate().SetContentBytes(new L(new A(), new A(objID.Type), new L(new A(objID.Name)), new L(), new L()));

                    ec = s14F1Mesg.Send().Run();

                    if (ec.IsNullOrEmpty())
                    {
                        var replyContentVC = s14F1Mesg.Reply.GetDecodedContents();
                        var objListVC = replyContentVC.SafeAccess(0);
                        var objAckList = replyContentVC.SafeAccess(1);
                        var objAck = objAckList.SafeAccess(0).GetValue<OBJACK>(rethrow: true);
                        var objErrorList = objAckList.SafeAccess(1);

                        if (objAck == OBJACK.Success && objErrorList.SafeAccess(0).IsNullOrEmpty)
                        {
                            var objInfoListVC = objListVC.SafeAccess(0);
                            var readObjName = objInfoListVC.SafeAccess(0).GetValue<string>(rethrow: false).MapNullToEmpty();
                            var readObjID = new E039ObjectID(readObjName, objID.Type);
                            var nvs = objInfoListVC.SafeAccess(1).ConvertToNamedValueSet(asReadOnly: true);

                            Log.Debug.Emit("Received E039 {0} obj: {1} [{2}]", objID.Type, readObjName, s14F1Mesg);

                            var obj = e039TableMgr.GetObject(readObjID);
                            if (obj == null)
                                ec = e039TableMgr.Update(new E039UpdateItem.AddObject(readObjID, nvs, mergeBehavior: NamedValueMergeBehavior.Replace, ifNeeded: true)).Run();
                            else if (!obj.Attributes.Equals(nvs))
                                ec = e039TableMgr.Update(new E039UpdateItem.SetAttributes(readObjID, nvs, mergeBehavior: NamedValueMergeBehavior.Replace)).Run();
                        }
                        else
                        {
                            var errorCode1 = objAckList.SafeAccess(1).SafeAccess(0).SafeAccess(0).GetValue<ERRCODE>(rethrow: false);

                            switch (errorCode1)
                            {
                                case ERRCODE.UnknownObjectInObjectSpecifier:
                                case ERRCODE.UnknownObjectInstance:
                                    {
                                        var obj = e039TableMgr.GetObject(objID);
                                        if (obj != null)
                                            ec = e039TableMgr.Update(new E039UpdateItem.RemoveObject(objID)).Run();
                                    }
                                    break;

                                default:
                                    ec = "GetAttr {0} failed with unexpected error: objAck:{1} {2}".CheckedFormat(s14F1Mesg, objAck, s14F1Mesg.Reply);
                                    break;
                            }
                        }
                    }
                }

                if (ec.IsNeitherNullNorEmpty())
                {
                    AsyncNoteE039ObjectTouched(objID);
                    break;
                }
            }

            // if there are any failures then put all of the remaining elements of the set back into the pending list.
            for (; ; )
            {
                if (!setEnum.MoveNext())
                    break;

                AsyncNoteE039ObjectTouched(setEnum.Current);
            }

            capturedTouchedObjectIDSet.Clear();

            return ec;
        }

        private string RefreshE039Table()
        {
            AsyncNoteE039ObjectTouched(E039ObjectID.Empty);     // trigger a full update of all types and names.

            string ec = Service039Support(forceFullUpdate: true);   // once to read the typenames and put them back in the asynch update set
            if (ec.IsNullOrEmpty())
                Service039Support(forceFullUpdate: true);   // a second time to read all of the objects for each type and update the e039 table accordingly

            return ec;
        }

        #endregion
    }

    public class EventReport
    {
        public EventSpec EventSpec;
        public NamedValueSet NVS;

        public override string ToString()
        {
            return "{0} {1}".CheckedFormat(EventSpec, NVS.SafeToStringSML());
        }
    }

    public class EventSpec
    {
        public string Name;
        public ValueContainer CEID;
        public ReportSpec[] ReportSpecArray;
        public ReportSpec ReportSpec { set { ReportSpecArray = new[] { value }; } }

        public bool IsValid { get { return (!CEID.IsNullOrEmpty && (CEID.u.u64 != 0 || CEID.o != null)) && (ReportSpecArray.IsNullOrEmpty() || ReportSpecArray.All(reportSpec => reportSpec.IsValid)); } }

        public override string ToString()
        {
            if (Name.IsNeitherNullNorEmpty())
                return "CEID:{0}:{1}".CheckedFormat(Name, CEID);
            else
                return "CEID:{0}".CheckedFormat(CEID);
        }
    }

    public class ReportSpec
    {
        public ValueContainer ReportID;
        public string[] TagNameArray;
        public ValueContainer[] VIDArray;
        public string[] E039TypesArray;

        public bool IsValid { get { return (!ReportID.IsNullOrEmpty && (ReportID.u.u64 != 0 || ReportID.o != null)); } }

        public override string ToString()
        {
            return "ReportID:{0}".CheckedFormat(ReportID);
        }
    }

    public class HostLPCyclePart : SimpleActivePartBase
    {
        public HostLPCyclePart(string partID, int lpNum, IPort port, HostCyclePart parent)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(addGoOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.SupportServiceActions))
        {
            this.lpNum = lpNum;
            this.port = port;
            this.parent = parent;

            SetupEventHandling();

            parent.PerPortEventReportNotificationList.SafeAccess(lpNum - 1).OnNotify += AsyncHandleEventReport;
            parent.NoPortEventReportNotificationList.OnNotify += AsyncHandleEventReport;

            SetupIVAs();
        }

        int lpNum;
        IPort port;
        HostCyclePart parent;

        bool pauseCJs = Config.Instance.GetConfigKeyAccessOnce("HostCycle.PauseCJs").GetValue(true);

        void SetupIVAs()
        {
            state.AMS = AMS.Undefined;
            state.CAS = CAS.Undefined;
            state.CIDS = CIDS.Undefined;
            state.CSMS = CSMS.Undefined;
            state.LCAS = LCAS.Undefined;
            state.LRS = LRS.Undefined;
            state.LTS = LTS.Undefined;

            stateVSA = new ValueSetAdapter<State>() { ValueSet = state, IssueEmitter = Log.Debug, ValueNoteEmitter = Log.Trace }.Setup(PartID + ".").Set();

            ivaAutoPWCAndCancel = Values.Instance.GetValueAccessor<bool>(PartID + ".AutoPWCAndCancel").Set(false);
            ivaCycleSelected = Values.Instance.GetValueAccessor<bool>(PartID + ".CycleSelected").Set(false);
            ivaRequestPause = Values.Instance.GetValueAccessor<bool>(PartID + ".RequestPause").Set(false);
            ivaRequestStop = Values.Instance.GetValueAccessor<bool>(PartID + ".RequestStop").Set(false);
            ivaRequestAbort = Values.Instance.GetValueAccessor<bool>(PartID + ".RequestAbort").Set(false);
            ivaRunNumber = Values.Instance.GetValueAccessor<string>(PartID + ".RunNumber").Set("");
        }

        IValueAccessor<bool> ivaAutoPWCAndCancel;
        IValueAccessor<bool> ivaCycleSelected;
        IValueAccessor<bool> ivaRequestPause;
        IValueAccessor<bool> ivaRequestStop;
        IValueAccessor<bool> ivaRequestAbort;
        IValueAccessor<string> ivaRunNumber;

        public class State
        {
            [ValueSetItem]
            public LTS LTS { get; set; }
            [ValueSetItem]
            public string LTS_CarrierID { get; set; }
            [ValueSetItem]
            public AMS AMS { get; set; }
            [ValueSetItem]
            public LRS LRS { get; set; }
            [ValueSetItem]
            public LCAS LCAS { get; set; }
            [ValueSetItem]
            public string LCAS_CarrierID { get; set; }
            [ValueSetItem]
            public CIDS CIDS { get; set; }
            [ValueSetItem]
            public string CIDS_Reason { get; set; }
            [ValueSetItem]
            public string CIDS_CarrierID { get; set; }
            [ValueSetItem]
            public CSMS CSMS { get; set; }
            [ValueSetItem]
            public string CSMS_Reason { get; set; }
            [ValueSetItem]
            public string CSMS_SlotMap { get; set; }
            [ValueSetItem]
            public string CSMS_LocationID { get; set; }
            [ValueSetItem]
            public CAS CAS { get; set; }
            [ValueSetItem]
            public string CJID { get; set; }
            [ValueSetItem]
            public string CJState { get; set; }
            [ValueSetItem]
            public string PJIDs { get { return string.Join(",", PJIDList); } set { } }
            public List<string> PJIDList = new List<string>();
            [ValueSetItem]
            public string PJStates { get { return string.Join(",", PRJobStateList.Select(prJobState => prJobState.ToString())); } set { } }
            public List<PRJobState> PRJobStateList = new List<PRJobState>();
            [ValueSetItem]
            public string PPID { get; set; }
            [ValueSetItem]
            public string LastCarrierEventInfo { get; set; }
            [ValueSetItem]
            public string E090StateTally { get; set; }
        }

        State state = new State();
        ValueSetAdapter<State> stateVSA;

        protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
        {
            if (andInitialize)
            {
                ReadPortStatesFromSVIDLists();

                ivaCycleSelected.Set(false);
                ivaRequestPause.Set(false);
                ivaRequestStop.Set(false);
                ivaRequestAbort.Set(false);
                ivaRunNumber.Set("");
            }

            return string.Empty;
        }

        private void ReadPortStatesFromSVIDLists()
        {
            int portIdx = lpNum - 1;
            var e087ids = parent.e087IDsConfig;
            var vidValues = parent.ReadSVIDValues(new[]
                                    {
                                        e087ids.AccessMode_SVIDList.SafeAccess(portIdx),
                                        e087ids.LoadPortReserverationState_SVIDList.SafeAccess(portIdx),
                                        e087ids.PortTransferState_SVIDList.SafeAccess(portIdx),
                                        e087ids.PortAssociationState_SVIDList.SafeAccess(portIdx),
                                        e087ids.CarrierID_SVIDList.SafeAccess(portIdx)
                                    });

            state.AMS = vidValues.SafeAccess(0).GetValue<AMS>(rethrow: true);
            state.LRS = vidValues.SafeAccess(1).GetValue<LRS>(rethrow: true);
            state.LTS = vidValues.SafeAccess(2).GetValue<LTS>(rethrow: true);
            state.LCAS = vidValues.SafeAccess(3).GetValue<LCAS>(rethrow: true);
            state.LCAS_CarrierID = vidValues.SafeAccess(4).GetValue<string>(rethrow: true);

            stateVSA.Set();
        }

        protected override void PerformMainLoopService()
        {
            base.PerformMainLoopService();

            ServicePendingEvents();

            bool prevPauseRequest = ivaRequestPause.Value;
            bool prevStopRequest = ivaRequestStop.Value;
            bool prevAbortRequest = ivaRequestAbort.Value;

            if (ivaRequestAbort.Update().Value && !prevAbortRequest)
                RequestAbortAllJobs();
            else if (ivaRequestStop.Update().Value && !prevStopRequest)
                RequestStopAllJobs();
            else if (ivaRequestPause.Update().Value != prevPauseRequest)
            {
                if (ivaRequestPause.Value)
                    RequestPauseAllJobs();
                else
                    RequestResumeAllJobs();
            }

            var entryIVAAutoPWCAndCancelValue = ivaAutoPWCAndCancel.Value;
            var ivaAutoPWCAndCancelValue = ivaAutoPWCAndCancel.Update().Value;
            if (ivaAutoPWCAndCancelValue && !entryIVAAutoPWCAndCancelValue)
                ivaRunNumber.Set($"{runNumber = 0}");

            if (ivaAutoPWCAndCancelValue)
            {
                string activity = "";
                string ec = null;

                if (state.CIDS == CIDS.WaitingForHost && state.LTS == LTS.TransferBlocked)
                {
                    if (autoPWCAndCancelHoldoffTimer.StartIfNeeded().IsTriggered)
                    {
                        activity = "PWC1";
                        ec = RunGenericS3F17(CARRIERACTION.ProceedWithCarrier, new NamedValueSet() { { "CarrierID", state.CIDS_CarrierID } });
                        pwcIssued |= ec.IsNullOrEmpty();
                    }
                }
                else if (state.CSMS == CSMS.WaitingForHost && state.LTS == LTS.TransferBlocked)
                {
                    if (autoPWCAndCancelHoldoffTimer.StartIfNeeded().IsTriggered)
                    {
                        activity = "PWC2";
                        ec = RunGenericS3F17(CARRIERACTION.ProceedWithCarrier, new NamedValueSet() { { "CarrierID", state.CIDS_CarrierID } });
                        pwcIssued |= ec.IsNullOrEmpty();
                    }
                }
                else if ((state.CSMS == CSMS.SlotMapVerificationOk || state.CSMS == CSMS.SlotMapVerificationFailed) && state.LTS == LTS.TransferBlocked && pwcIssued)
                {
                    if (autoPWCAndCancelHoldoffTimer.StartIfNeeded().IsTriggered)
                    {
                        pwcIssued = false;
                        activity = "CancelCarrierAtPort";
                        ec = RunGenericS3F17(CARRIERACTION.CancelCarrierAtPort, NamedValueSet.Empty);
                        ivaRunNumber.Set($"{++runNumber}");
                    }
                }

                if (ec != null)
                    autoPWCAndCancelHoldoffTimer.Stop();

                if (ec.IsNeitherNullNorEmpty())
                {
                    Log.Signif.Emit($"Disabling AutoPWCAndCancel during '{activity}': {ec}");
                    ivaAutoPWCAndCancel.Set(false);
                }
            }
        }

        bool pwcIssued = false;
        QpcTimer autoPWCAndCancelHoldoffTimer = new QpcTimer() { TriggerInterval = (1.0).FromSeconds() };

        private void RequestAbortAllJobs()
        {
            foreach (var pjID in state.PJIDList.Zip(state.PRJobStateList, (id, state) => Tuple.Create(id, state)).Where(t => t.Item2 == PRJobState.SettingUp || t.Item2 == PRJobState.WaitingForStart || t.Item2 == PRJobState.Processing || t.Item2 == PRJobState.Stopping).Select(t => t.Item1))
            {
                RunS16F5PRCmd(pjID, PRCMDNAME.ABORT);
            }

            if (state.CJID.IsNeitherNullNorEmpty() && !state.CJState.StartsWith("Complete"))
                RunS16F27CtlJobCmd(CTLJOBCMD.CJAbort);
        }

        private void RequestStopAllJobs()
        {
            foreach (var pjID in state.PJIDList.Zip(state.PRJobStateList, (id, state) => Tuple.Create(id, state)).Where(t => t.Item2 == PRJobState.SettingUp || t.Item2 == PRJobState.WaitingForStart || t.Item2 == PRJobState.Processing).Select(t => t.Item1))
            {
                RunS16F5PRCmd(pjID, PRCMDNAME.STOP);
            }

            if (state.CJID.IsNeitherNullNorEmpty() && !state.CJState.StartsWith("Complete"))
                RunS16F27CtlJobCmd(CTLJOBCMD.CJStop);
        }

        private void RequestPauseAllJobs()
        {
            foreach (var pjID in state.PJIDList.Zip(state.PRJobStateList, (id, state) => Tuple.Create(id, state)).Where(t => t.Item2 == PRJobState.SettingUp || t.Item2 == PRJobState.WaitingForStart || t.Item2 == PRJobState.Processing).Select(t => t.Item1))
            {
                RunS16F5PRCmd(pjID, PRCMDNAME.PAUSE);
            }

            if (state.CJID.IsNeitherNullNorEmpty() && state.CJState.StartsWith("Executing") && pauseCJs)
                RunS16F27CtlJobCmd(CTLJOBCMD.CJPause);
        }

        private void RequestResumeAllJobs()
        {
            foreach (var pjID in state.PJIDList.Zip(state.PRJobStateList, (id, state) => Tuple.Create(id, state)).Where(t => t.Item2 == PRJobState.Pausing || t.Item2 == PRJobState.Paused).Select(t => t.Item1))
            {
                RunS16F5PRCmd(pjID, PRCMDNAME.RESUME);
            }

            if (state.CJID.IsNeitherNullNorEmpty() && state.CJState.StartsWith("Paused"))
                RunS16F27CtlJobCmd(CTLJOBCMD.CJResume);
        }

        protected override string PerformServiceActionEx(IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            switch (serviceName)
            {
                case "Bind":
                case "CancelBind":
                case "CancelCarrier":
                case "CancelCarrierAtPort":
                case "CarrierNotification":
                case "CancelCarrierNotification":
                case "CarrierReCreate":
                case "ProceedWithCarrier":
                    return RunGenericS3F17(serviceName.TryParse<CARRIERACTION>(), npv);

                case "ReserveAtPort":
                case "CancelReservationAtPort":
                    return RunGenericS3F25(serviceName, npv);

                case "Automatic":
                case "Manual":
                    return SetAMSUsingS3F27(serviceName.TryParse<AMS>());

                case "CreateJobs":
                    return CreateJobs(npv);

                case "RunCarrier":
                    return RunCarrier(ipf, npv);

                default:
                    return base.PerformServiceActionEx(ipf, serviceName, npv);
            }
        }

        private string RunGenericS3F17(CARRIERACTION carrierAction, INamedValueSet npv)
        {
            NamedValueSet nvs = ConvertStandardNPV(npv);
            string cid = nvs.RemoveAndReturnNamedValue("CarrierID").VC.GetValue<string>(rethrow: true);

            if (carrierAction == CARRIERACTION.CancelCarrier)
            {
                if (cid.IsNullOrEmpty())
                    carrierAction = CARRIERACTION.CancelCarrierAtPort;
            }

            var dataid = port.GetNextDATAID();
            var mesg = port.CreateMessage("S3/F17[W]").SetContentBytes(new L(new U4(dataid), new A(carrierAction.ToString()), new A(cid), new U1((byte)lpNum), new NamedValueSetBuilder() { NamedValueSet = nvs }));
            string ec = mesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                try
                {
                    var reply = mesg.Reply;
                    var replyVC = mesg.Reply.GetDecodedContents();
                    var caack = replyVC.SafeAccess(0).GetValue<CAACK>(rethrow: true);
                    var errorListVC = replyVC.SafeAccess(1);

                    if (caack != CAACK.AcknowledgedCommandHasBeenPerformed)
                        ec = "Request was rejected: {0} {1}".CheckedFormat(caack, replyVC.ToStringSML());
                }
                catch (System.Exception ex)
                {
                    ec = "Reply decode failed: {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }

            return ec;
        }

        private NamedValueSet ConvertStandardNPV(INamedValueSet npv)
        {
            NamedValueSet nvs = npv.ConvertToWritable().BuildDictionary();

            var capacityStr = nvs["Capacity"].VC.GetValue<string>(rethrow: false);
            if (capacityStr.IsNeitherNullNorEmpty())
                nvs.SetValue("Capacity", nvs["Capacity"].VC.GetValue<byte>(rethrow: false));
            else if (nvs.Contains("Capacity"))
                nvs.Remove("Capacity");

            var slotMapStr = nvs["SlotMap"].VC.GetValue<string>(rethrow: false);

            if (slotMapStr.IsNeitherNullNorEmpty())
                nvs.SetValue("SlotMap", new L(slotMapStr.ParseSlotStates().Select(slotState => new U1((byte) slotState))).BuildContents());
            else if (nvs.Contains("SlotMap"))
                nvs.Remove("SlotMap");

            return nvs;
        }

        private string RunGenericS3F25(string portActionRequest, INamedValueSet npv)
        {
            var mesg = port.CreateMessage("S3/F25[W]").SetContentBytes(new L(new A(portActionRequest), new U1((byte)lpNum), new NamedValueSetBuilder() { NamedValueSet = npv }));
            string ec = mesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                try
                {
                    var reply = mesg.Reply;
                    var replyVC = mesg.Reply.GetDecodedContents();
                    var caack = replyVC.SafeAccess(0).GetValue<CAACK>(rethrow: true);
                    var errorListVC = replyVC.SafeAccess(1);

                    if (caack != CAACK.AcknowledgedCommandHasBeenPerformed)
                        ec = "Request was rejected: {0} {1}".CheckedFormat(caack, replyVC.ToStringSML());
                }
                catch (System.Exception ex)
                {
                    ec = "Reply decode failed: {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }

            return ec;
        }

        private string SetAMSUsingS3F27(AMS ams)
        {
            var mesg = port.CreateMessage("S3/F27[W]").SetContentBytes(new L(new U1((byte) ams), new L(new U1((byte)lpNum))));
            string ec = mesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                try
                {
                    var reply = mesg.Reply;
                    var replyVC = mesg.Reply.GetDecodedContents();
                    var caack = replyVC.SafeAccess(0).GetValue<CAACK>(rethrow: true);
                    var errorListVC = replyVC.SafeAccess(1);

                    if (caack != CAACK.AcknowledgedCommandHasBeenPerformed)
                        ec = "Request was rejected: {0} {1}".CheckedFormat(caack, replyVC.ToStringSML());
                }
                catch (System.Exception ex)
                {
                    ec = "Reply decode failed: {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }

            return ec;
        }

        int runNumber = 1;

        private string CreateJobs(INamedValueSet npv)
        {
            string cid = npv["CarrierID"].VC.GetValue<string>(rethrow: true);
            string ppid = npv["PPID"].VC.GetValue<string>(rethrow: true);
            var slotMapArray = npv["SlotMap"].VC.GetValue<string>(rethrow: true).ParseSlotStates();
            var correctlyOccupiedSlotNumArray = slotMapArray.Select((slotState, index) => Tuple.Create(slotState, index + 1)).Where(t => t.Item1 == SlotState.CorrectlyOccupied).Select(t => t.Item2).ToArray();
            
            string cjID = "HostCJ_{0}_{1}".CheckedFormat(cid, runNumber);
            string pjID = "HostPJ_{0}_{1}".CheckedFormat(cid, runNumber);

            IMessage s16F11Mesg, s14F9Mesg;

            string usePPID = ppid;

            string ec = string.Empty;

            if (parent.config.CreateJobWithTemporaryRecipe && ec.IsNullOrEmpty())
            {
                var rcpExt = parent.config.RecipeNameExtension;
                var ppidHasExtension = ppid.EndsWith(parent.config.RecipeNameExtension);

                usePPID = ppid.RemoveSuffixIfNeeded(parent.config.RecipeNameExtension) + "_Temp";
                if (ppidHasExtension)
                    usePPID = usePPID.AddSuffixIfNeeded(parent.config.RecipeNameExtension);

                // get the requested recipe by ppid
                var pprS7F5 = port.CreateMessage("S7/F5[W]").SetContentBytes(new A(ppid));  // PPR

                ec = pprS7F5.Send().Run();

                string usePPBody = null;

                if (ec.IsNullOrEmpty())
                {
                    var replyVCL = pprS7F5.Reply.GetDecodedContents().GetValueL(rethrow: false).MapNullToEmpty();

                    if (replyVCL.Count == 2)
                        usePPBody = replyVCL.SafeAccess(1).GetValueA(rethrow: false);
                    else
                        ec = $"{pprS7F5} failed: ppid not found";
                }

                // delete the temp recipe name if needed - ignore the result code

                if (ec.IsNullOrEmpty())
                {
                    var dpsS7F17 = port.CreateMessage("S7/F17[W]").SetContentBytes(new L() { new A(usePPID) });

                    ec = dpsS7F17.Send().Run();
                }

                // download the selected recipe body to the temp name
                var ppsS7f3 = port.CreateMessage("S7F3[W]").SetContentBytes(new L() { new A(usePPID), new A(usePPBody) });

                if (ec.IsNullOrEmpty())
                {
                    ec = ppsS7f3.Send().Run();

                    var ackc7 = ppsS7f3.Reply.GetDecodedContents().GetValue<ACKC7?>(rethrow: false) ?? ACKC7.MatrixOverflow;

                    if (ec.IsNullOrEmpty() && ackc7 != ACKC7.Accepted)
                        ec = $"{ppsS7f3} failed: ACKC7: {ackc7}";
                }
            }

            var pjDataid = port.GetNextDATAID();

            s16F11Mesg = port.CreateMessage("S16/F11[W]").SetContentBytes(
                            new L()
                            {
                                new U4(pjDataid),
                                new A(pjID),
                                new Bi((byte) MF.Carrier),
                                new L(new L(new A(cid), new L(correctlyOccupiedSlotNumArray.Select(slotNum => new U1((byte) slotNum))))),
                                new L(new U1((byte)PRRECIPEMETHOD.RecipeOnly), new A(usePPID), new L()),
                                new Bo(true),   // PRProcessStart
                                new L()
                            });

            s14F9Mesg = port.CreateMessage("S14/F9[W]").SetContentBytes(
                            new L()
                            {
                                new A(),
                                new A("ControlJob"),
                                new NamedValueSetBuilder()
                                {
                                    { "ObjID", cjID },
                                    { "CarrierInputSpec", new L(new A(cid)) },
                                    { "ProcessingCtrlSpec", new L(new L(new A(pjID), new L(), new L())) },
                                    { "ProcessOrderMgmt", new U1((byte) ProcessOrderMgmt.List) },
                                    { "StartMethod", true },
                                    { "DataCollectionPlan", "" },
                                    { "MtrlOutSpec", new L() },
                                    { "MtrlOutByStatus", new L() },
                                    { "PauseEvent", new L() },
                                }
                            });

            bool pjCreated = false, cjCreated = false;

            if (ec.IsNullOrEmpty())
               ec = s16F11Mesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                var replyBodyVC = s16F11Mesg.Reply.GetDecodedContents();
                var ackaListVC = replyBodyVC.SafeAccess(1);
                var acka = ackaListVC.SafeAccess(0).GetValue<ACKA>(rethrow: true, defaultValue: ACKA.False);

                if (acka == ACKA.True)
                    pjCreated = true;
                else
                    ec = "PJCreate '{0}' failed: {1}".CheckedFormat(pjID, replyBodyVC);
            }

            // delete the temporarily created recipe if needed
            if (usePPID != ppid)
            {
                var dpsS7F17 = port.CreateMessage("S7/F17[W]").SetContentBytes(new L() { new A(usePPID) });

                if (ec.IsNullOrEmpty())
                    ec = dpsS7F17.Send().Run();
                else if (port.BaseState.IsConnected)
                    dpsS7F17.Send().Run();

                if (ec.IsNullOrEmpty())
                {
                    var ackc7 = dpsS7F17.Reply.GetDecodedContents().GetValue<ACKC7?>(rethrow: false) ?? ACKC7.MatrixOverflow;
                    if (ec.IsNullOrEmpty() && ackc7 != ACKC7.Accepted)
                        ec = $"{dpsS7F17} failed: ACKC7: {ackc7}";
                }
            }

            if (ec.IsNullOrEmpty() && pjCreated)
                ec = s14F9Mesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                var replyBodyVC = s14F9Mesg.Reply.GetDecodedContents();
                var objAckListVC = replyBodyVC.SafeAccess(2);
                var objAck = objAckListVC.SafeAccess(0).GetValue<OBJACK>(rethrow: true, defaultValue: OBJACK.Denied_Internal);

                if (objAck != OBJACK.Success)
                    ec = "CJCreate '{0}' failed: {1} {2}".CheckedFormat(cjID, objAck, objAckListVC);
                else
                    cjCreated = true;
            }

            if (ec.IsNeitherNullNorEmpty() && pjCreated && !cjCreated)
            {
                var s16F17Mesg = port.CreateMessage("S16/F17[W]").SetContentBytes(new L(new A(pjID)));
                s16F17Mesg.Send().Run();
            }

            if (ec.IsNullOrEmpty())
            {
                state.CJID = cjID;
                state.CJState = "-Created-";
                state.PJIDList.Add(pjID);
                state.PRJobStateList.Add(PRJobState.Created);
                state.PPID = ppid;
            }

            runNumber = (runNumber % 99) + 1;

            return ec;
        }

        private string RunCarrier(IProviderFacet ipf, INamedValueSet npv)
        {
            string ec = InnerRunCarrier(CurrentMethodName, ipf, npv);

            {
                var dequeuePJIDSet = state.PJIDList.Zip(state.PRJobStateList, (id, prJobState) => Tuple.Create(id, prJobState)).Where(t => t.Item2 == PRJobState.QueuedOrPooled).Select(t => t.Item1).ToArray();

                if (!dequeuePJIDSet.IsNullOrEmpty())
                {
                    Log.Warning.Emit("Attempting to dequeue stranded pjs: {0}", string.Join(",", dequeuePJIDSet));

                    var s16F17Mesg = port.CreateMessage("S16/F17[W]").SetContentBytes(new L(dequeuePJIDSet.Select(pjID => new A(pjID))));
                    string dequeueEC = s16F17Mesg.Send().Run();

                    if (dequeueEC.IsNullOrEmpty())
                    {
                        var dequeueReplyVC = s16F17Mesg.Reply.GetDecodedContents();
                        var acka = dequeueReplyVC.SafeAccess(1).SafeAccess(0).GetValue<ACKA>(rethrow: false);

                        if (acka != ACKA.True)
                            dequeueEC = "{0} reply: {1}".CheckedFormat(s16F17Mesg.SF, s16F17Mesg.Reply);
                    }

                    if (dequeueEC.IsNeitherNullNorEmpty())
                        Log.Error.Emit("Attempt to dequeue stranded pjs {0} failed: {1}", string.Join(",", dequeuePJIDSet), dequeueEC);
                }
            }

            return ec;
        }

        private string InnerRunCarrier(string description, IProviderFacet ipf, INamedValueSet npv)
        {
            string ppid = npv["PPID"].VC.GetValue<string>(rethrow: true);

            ivaRequestPause.Set(false);
            ivaRequestStop.Set(false);
            ivaRequestAbort.Set(false);

            ReadPortStatesFromSVIDLists();

            if (state.LCAS != LCAS.Associated || state.LCAS_CarrierID.IsNullOrEmpty())
                return "{0} failed: there is not valid Carrier ID that is associated with this port".CheckedFormat(description);

            var carrierID = state.LCAS_CarrierID;

            if (state.CIDS != CIDS.WaitingForHost)
                return "{0} failed: cannot start unless CIDS is WaitingForHost [{1}]".CheckedFormat(description, state.CIDS);

            if (state.LTS != LTS.TransferBlocked)
                return "{0} failed: cannot start unless LTS is TransferBlocked [{1}]".CheckedFormat(description, state.LTS);

            string ec = string.Empty;

            ec = RunGenericS3F17(CARRIERACTION.ProceedWithCarrier, new NamedValueSet() { { "CarrierID", carrierID }, { "Capacity", (byte)25 } });

            if (ec.IsNeitherNullNorEmpty())
                return "{0} failed: at attempt to issue PWC1: {1}".CheckedFormat(description, ec);

            runNumber = 1;
            ivaRunNumber.Set("{0}".CheckedFormat(runNumber));

            for (; ; )
            {
                Log.Signif.Emit("Starting Run {0}", runNumber);

                // wait until CSMS reaches waiting for host
                ec = SpinUntil(ipf, () => state.CSMS == CSMS.WaitingForHost, timeLimit: (30.0).FromSeconds());

                if (ec.IsNeitherNullNorEmpty())
                    return "{0} failed: at wait for CSMS.WaitingForHost: {1} [{2} '{3}']".CheckedFormat(description, ec, state.CSMS, state.CSMS_SlotMap);

                // generate content map using mix of LotIDs, and substrateID representations
                var slotMapStr = state.CSMS_SlotMap;
                var slotMapArray = slotMapStr.ParseSlotStates();
                var slotNumArray = Enumerable.Range(1, slotMapArray.Length).ToArray();
                var nonEmptySlotMapArray = slotMapArray.Select(slotState => slotState == SlotState.CorrectlyOccupied || slotState == SlotState.CrossSlotted || slotState == SlotState.DoubleSlotted).ToArray();

                var substIDMapArray = nonEmptySlotMapArray.Zip(slotNumArray, (present, slotNum) => (!present) ? "" : ((slotNum % 2 == 1) ? "{0}.{1:d2}" : "{0}..{1:d3}").CheckedFormat(carrierID, slotNum)).ToArray();
                var lotIDMapArray = nonEmptySlotMapArray.Zip(slotNumArray, (present, slotNum) => (!present) ? "" : ("Lot{0:d1}".CheckedFormat(((slotNum - 1) % 3) + 1))).ToArray();

                var usableSlotsMapArray = slotMapArray.Select(slotState => slotState == SlotState.CorrectlyOccupied).ToArray();

                var pwcContentMap = new L(lotIDMapArray.Zip(substIDMapArray, (lotID, substID) => new L(new A(lotID), new A(substID))));

                var jobTupleArray = usableSlotsMapArray.Zip(substIDMapArray, (use, substID) => Tuple.Create(use, substID)).ToArray();

                var occupiedSlotNumArray = nonEmptySlotMapArray.Zip(slotNumArray, (use, slotNum) => Tuple.Create(use, slotNum)).Where(t => t.Item1).Select(t => t.Item2).ToArray();
                var correctlyOccupiedSlotNumArray = usableSlotsMapArray.Zip(slotNumArray, (use, slotNum) => Tuple.Create(use, slotNum)).Where(t => t.Item1).Select(t => t.Item2).ToArray();

                var correctlyOccupiedSubstCount = correctlyOccupiedSlotNumArray.Length;
                var occupiedSubstCount = occupiedSlotNumArray.Length;

                // issue PWC2

                ec = RunGenericS3F17(CARRIERACTION.ProceedWithCarrier, new NamedValueSet() { { "CarrierID", carrierID }, { "Capacity", (byte)25 }, { "SlotMap", slotMapStr }, { "ContentMap", pwcContentMap.BuildContents() } });

                if (ec.IsNeitherNullNorEmpty())
                    return "{0} failed: at attempt to issue PWC2 during Run {1}: {2}".CheckedFormat(description, runNumber, ec);

                // wait for CSMS verified
                ec = SpinUntil(ipf, () => state.CSMS == CSMS.SlotMapVerificationOk, timeLimit: (30.0).FromSeconds());

                if (ec.IsNeitherNullNorEmpty())
                    return "{0} failed: at wait for CSMS.SlotMapVerificationOk: {1} [{2} '{3}']".CheckedFormat(description, ec, state.CSMS, state.CSMS_SlotMap);

                // wait for substrates created
                ec = SpinUntil(ipf, () => substStateTally.total == occupiedSubstCount, timeLimit: (5.0).FromSeconds());

                if (ec.IsNeitherNullNorEmpty())
                    return "{0} failed: at wait for Substrates to be created: {1} [have:{2} expected:{3}]".CheckedFormat(description, ec, substStateTally.total, occupiedSubstCount);

                if (correctlyOccupiedSubstCount > 0)
                {
                    // Create PJ and then CJ
                    string cjID = "HostRun_LP{0}_CJ{1:d3}".CheckedFormat(lpNum, runNumber);
                    string pjID = "HostRun_LP{0}_PJ{1:d3}".CheckedFormat(lpNum, runNumber);

                    // future: support selection of autoStart values

                    // Create the PJ
                    {
                        var pjDataid = port.GetNextDATAID();

                        // future: alternate between MF.Carrier and MF.Substrate formats
                        var s16F11Mesg = port.CreateMessage("S16/F11[W]").SetContentBytes(
                                            new L()
                                            {
                                                new U4(pjDataid),
                                                new A(pjID),
                                                new Bi((byte)MF.Substrate),
                                                jobTupleArray.Where(t => t.Item1 && t.Item2.IsNeitherNullNorEmpty()).Select(t => t.Item2).MakeListBuilder(),
                                                new L(new U1((byte)PRRECIPEMETHOD.RecipeOnly), new A(ppid), new L()),
                                                new Bo(true),   // PRProcessStart
                                                new L()
                                            });

                        if (ec.IsNullOrEmpty())
                            ec = s16F11Mesg.Send().Run();

                        if (ec.IsNullOrEmpty())
                        {
                            var replyBodyVC = s16F11Mesg.Reply.GetDecodedContents();
                            var ackaListVC = replyBodyVC.SafeAccess(1);
                            var acka = ackaListVC.SafeAccess(0).GetValue<ACKA>(rethrow: true, defaultValue: ACKA.False);

                            if (acka != ACKA.True)
                                ec = "{0} gave {1}".CheckedFormat(s16F11Mesg, s16F11Mesg.Reply);
                        }

                        if (ec.IsNeitherNullNorEmpty())
                            return "{0} failed: at PJ creation '{1}': {2}".CheckedFormat(description, pjID, ec);

                        state.PJIDList.Add(pjID);
                        state.PRJobStateList.Add(PRJobState.Created);
                        state.PPID = ppid;

                        stateVSA.Set();
                    }

                    // Create the CJ

                    {
                        IMessage s14F9Mesg = port.CreateMessage("S14/F9[W]").SetContentBytes(
                                            new L()
                                            {
                                                new A(),
                                                new A("ControlJob"),
                                                new NamedValueSetBuilder()
                                                {
                                                    { "ObjID", cjID },
                                                    { "CarrierInputSpec", new L(new A(carrierID)) },
                                                    { "ProcessingCtrlSpec", new L(new L(new A(pjID), new L(), new L())) },
                                                    { "ProcessOrderMgmt", new U1((byte) ProcessOrderMgmt.List) },
                                                    { "StartMethod", true },
                                                    { "DataCollectionPlan", "" },
                                                    { "MtrlOutSpec", new L() },
                                                    { "MtrlOutByStatus", new L() },
                                                    { "PauseEvent", new L() },
                                                }
                                            });

                        ec = s14F9Mesg.Send().Run();

                        if (ec.IsNullOrEmpty())
                        {
                            var replyBodyVC = s14F9Mesg.Reply.GetDecodedContents();
                            var objAckListVC = replyBodyVC.SafeAccess(2);
                            var objAck = objAckListVC.SafeAccess(0).GetValue<OBJACK>(rethrow: true, defaultValue: OBJACK.Denied_Internal);

                            if (objAck != OBJACK.Success)
                                ec = "{0} gave {1}".CheckedFormat(s14F9Mesg, s14F9Mesg.Reply);
                        }

                        if (ec.IsNeitherNullNorEmpty())
                            return "{0} failed: at CJ creation '{1}': {2}".CheckedFormat(description, cjID, ec);

                        state.CJID = cjID;
                        state.CJState = "-Created-";
                        stateVSA.Set();
                    }

                    for (; ; )
                    {
                        WaitForSomethingToDo();

                        // This method services and handles the abort, stop and pause request inputs.
                        PerformMainLoopService();

                        // monitor PJ states and issue start to any that reach WaitingForStart - then wait for its state to change before proceeding
                        if (state.PRJobStateList.Any(prJobState => prJobState == PRJobState.WaitingForStart))
                        {
                            foreach (var pjIDToStart in state.PJIDList.Zip(state.PRJobStateList, (id, prJobState) => Tuple.Create(id, prJobState)).Where(t => t.Item2 == PRJobState.WaitingForStart).Select(t => t.Item1).ToArray())
                            {
                                ec = RunS16F5PRCmd(pjIDToStart, PRCMDNAME.START);

                                if (ec.IsNeitherNullNorEmpty())
                                    return "{0} failed: at PJ START for '{1}': {2}".CheckedFormat(description, pjIDToStart, ec);
                            }
                        }

                        // monitor CJ state and issue start if it reaches WaitingForStart - then wait for its state to change before proceeding
                        if (state.CJState == "WaitingForStart")
                        {
                            var ctlJobCmd = CTLJOBCMD.CJStart;

                            ec = RunS16F27CtlJobCmd(ctlJobCmd);

                            if (ec.IsNeitherNullNorEmpty())
                                return "{0} failed: at CJStart for '{1}': {2}".CheckedFormat(description, state.CJID, ec);
                        }

                        if (state.CJState.StartsWith("Complete"))
                        {
                            int pjCount = state.PJIDList.Count;
                            int processedPJCount = state.PRJobStateList.Sum(prJobState => (prJobState == PRJobState.ProcessComplete).MapToInt());
                            int stoppedPJCount = state.PRJobStateList.Sum(prJobState => (prJobState == PRJobState.Stopped).MapToInt());
                            int abortedPJCount = state.PRJobStateList.Sum(prJobState => (prJobState == PRJobState.Aborted).MapToInt());

                            if (pjCount == processedPJCount)
                                break;

                            if (pjCount == processedPJCount + stoppedPJCount + abortedPJCount)
                            {
                                if (ivaCycleSelected.Value)
                                {
                                    Log.Warning.Emit("Cycling stopped on run {0} [PRStates: {1} sts:{2} sps:{3}]", runNumber, state.PJStates, substStateTally.STSToString(), substStateTally.SPSToString());
                                    ivaCycleSelected.Set(false);
                                }

                                break;
                            }
                        }

                        if (ivaRequestAbort.Value)
                        {
                            Log.Warning.Emit("Cycling aborted by request on run {0}: [PRStates: {1} sts:{2} sps:{3}]", runNumber, state.PJStates, substStateTally.STSToString(), substStateTally.SPSToString());
                            ivaCycleSelected.Set(false);
                            break;
                        }
                    }
                }
                else
                {
                    state.CJID = string.Empty;
                    state.CJState = "-- carrier is empty --";
                    state.PJIDList.Clear();
                    state.PRJobStateList.Clear();

                    stateVSA.Set();

                    ec = RunGenericS3F17(CARRIERACTION.CancelCarrier, new NamedValueSet() { { "CarrierID", carrierID } });

                    if (ec.IsNeitherNullNorEmpty())
                        return "{0} failed: in Run {1} at attempt to issue CancelCarrier: {1}".CheckedFormat(description, runNumber, ec);
                }

                bool aborting = ivaRequestAbort.Value;

                // wait LTS to reach ReadyToUnload (PJs and CJs have already reached a completion state)
                if (!aborting)
                {
                    ec = SpinUntil(ipf, () => state.LTS == LTS.ReadyToUnload || state.LTS == LTS.ReadyToLoad || state.CIDS == CIDS.IDVerificationFailed || state.CSMS == CSMS.SlotMapVerificationFailed || ivaRequestAbort.Value);

                    if (ec.IsNullOrEmpty() && state.LTS != LTS.ReadyToUnload)
                        ec = "Unexpected LTS:{0}, CIDs:{1}, CSMS:{2}".CheckedFormat(state.LTS, state.CIDS, state.CSMS);

                    if (ec.IsNeitherNullNorEmpty())
                        return "{0} failed: in Run {1} at wait until LTS.ReadToUnload: {1}".CheckedFormat(description, runNumber, ec);

                    Log.Signif.Emit("Completed at Run {0} [{1} {2}, {3} {4}, sts:({5}) sps:({6})]", runNumber, state.CJID, state.CJState, state.PJIDs, state.PJStates, substStateTally.STSToString(), substStateTally.SPSToString());
                }
                else
                {
                    Log.Signif.Emit("Aborted at Run {0} [{1} {2}, {3} {4}, sts:({5}) sps:({6})]", runNumber, state.CJID, state.CJState, state.PJIDs, state.PJStates, substStateTally.STSToString(), substStateTally.SPSToString());
                }

                // if Cycle is requested (and PJs and CJ have reached success state?) then issue ReCreate1 and loop back to top.
                if (!ivaCycleSelected.Update().Value)
                {
                    // otherwise we are done.
                    return string.Empty;
                }

                ec = RunGenericS3F17(CARRIERACTION.CarrierReCreate, new NamedValueSet() { { "CarrierID", carrierID }, { "Capacity", (byte)25 } });

                if (ec.IsNeitherNullNorEmpty())
                    return "{0} failed: at attempt to issue ReCreate1: {1}".CheckedFormat(description, ec);

                runNumber++;
                ivaRunNumber.Set("{0}".CheckedFormat(runNumber));
            }
        }

        private string RunS16F5PRCmd(string pjID, PRCMDNAME prCmd)
        {
            string ec;
            var dataID = port.GetNextDATAID();
            var startMesg = port.CreateMessage("S16/F5[W]").SetContentBytes(
                            new L()
                            {
                                new U4(dataID),
                                new A(pjID),
                                new A(prCmd.ToString()),
                                new L(),
                            });

            ec = startMesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                var replyVC = startMesg.Reply.GetDecodedContents();
                var acka = replyVC.SafeAccess(1).SafeAccess(0).GetValue<ACKA>(rethrow: false);

                if (acka != ACKA.True)
                    ec = "{0} gave {1}".CheckedFormat(startMesg, startMesg.Reply);
            }

            return ec;
        }

        private string RunS16F27CtlJobCmd(CTLJOBCMD ctlJobCmd)
        {
            string ec;

            bool includeAction = (ctlJobCmd == CTLJOBCMD.CJCancel || ctlJobCmd == CTLJOBCMD.CJAbort || ctlJobCmd == CTLJOBCMD.CJStop);

            var startMesg = port.CreateMessage("S16/F27[W]").SetContentBytes(
                            new L()
                            {
                                new A(state.CJID),
                                new U1((byte) ctlJobCmd),
                                includeAction ? new L(new A("Action"), new U1((byte) S16F27_Action.SaveJobs)) : new L(),
                            });

            ec = startMesg.Send().Run();

            if (ec.IsNullOrEmpty())
            {
                var replyVC = startMesg.Reply.GetDecodedContents();
                var acka = replyVC.SafeAccess(1).SafeAccess(0).GetValue<ACKA>(rethrow: false);

                if (acka != ACKA.True)
                    ec = "{0} gave {1}".CheckedFormat(startMesg, startMesg.Reply);
            }

            return ec;
        }

        string SpinUntil(IProviderFacet ipf, Func<bool> predicate, TimeSpan timeLimit = default(TimeSpan), bool checkForIPFCancelRequest = true, bool checkForPartStopRequest = true)
        {
            QpcTimer waitLimitTimer = new QpcTimer() { TriggerInterval = timeLimit, SelectedBehavior = QpcTimer.Behavior.ElapsedTimeIsZeroWhenStopped };
            if (timeLimit != TimeSpan.Zero)
                waitLimitTimer.Start();

            for (; ; )
            {
                bool waitLimitTimerIsTriggered = waitLimitTimer.IsTriggered;

                if (predicate())
                    return string.Empty;

                if (ipf.IsCancelRequestActive && checkForIPFCancelRequest)
                    return "Cancel requested";

                if (HasStopBeenRequested && checkForPartStopRequest)
                    return "Part stop has been requested";

                if (waitLimitTimerIsTriggered)
                    return "Wait time limit reached after {0:f3} seconds".CheckedFormat(waitLimitTimer.ElapsedTimeAtLastTrigger.TotalSeconds);

                WaitForSomethingToDo();
                PerformMainLoopService();
            }
        }

        #region Event handling

        private void AsyncHandleEventReport(object source, EventReport eventReport)
        {
            var eventInfo = eventIDToEventInfoDictionary.SafeTryGetValue(eventReport.EventSpec.CEID);

            if (eventInfo.EventGroup != EventGroup.None)
            {
                lock (pendingEventReportExMutex)
                {
                    pendingEventReportExList.Add(new EventReportEx() { EventReport = eventReport, EventInfo = eventInfo });
                    pendingEventReportExListCount = pendingEventReportExList.Count;
                }

                this.Notify();
            }
        }

        private object pendingEventReportExMutex = new object();
        private List<EventReportEx> pendingEventReportExList = new List<EventReportEx>();
        private volatile int pendingEventReportExListCount;

        private void ServicePendingEvents()
        {
            if (pendingEventReportExListCount == 0)
                return;

            var eventReportExArray = EmptyArrayFactory<EventReportEx>.Instance;

            lock (pendingEventReportExMutex)
            {
                eventReportExArray = pendingEventReportExList.ToArray();
                pendingEventReportExList.Clear();
            }

            foreach (var eventReportEx in eventReportExArray)
            {
                switch (eventReportEx.EventInfo.EventGroup)
                {
                    case EventGroup.LTS: HandleE87LTSEventReport(eventReportEx.EventInfo, eventReportEx.EventReport, eventReportEx.EventReport.NVS); break;
                    case EventGroup.COSM: HandleE87COSMEventReport(eventReportEx.EventInfo, eventReportEx.EventReport, eventReportEx.EventReport.NVS); break;
                    case EventGroup.AMS: HandleE87AMSEventReport(eventReportEx.EventInfo, eventReportEx.EventReport, eventReportEx.EventReport.NVS); break;
                    case EventGroup.LRS: HandleE87LRSEventReport(eventReportEx.EventInfo, eventReportEx.EventReport, eventReportEx.EventReport.NVS); break;
                    case EventGroup.LCAS: HandleE87LCASEventReport(eventReportEx.EventInfo, eventReportEx.EventReport, eventReportEx.EventReport.NVS); break;
                    case EventGroup.Carrier: HandleE87CarrierEventReport(eventReportEx.EventReport); break;
                    case EventGroup.E040: HandleE40EventReport(eventReportEx.EventInfo, eventReportEx.EventReport, eventReportEx.EventReport.NVS); break;
                    case EventGroup.E094: HandleE94EventReport(eventReportEx.EventInfo, eventReportEx.EventReport, eventReportEx.EventReport.NVS); break;
                    case EventGroup.E090_SOSM: HandleE90SLOSMEventReport(eventReportEx.EventInfo, eventReportEx.EventReport, eventReportEx.EventReport.NVS); break;
                    case EventGroup.E090_SLOSM: break;
                    default:
                        Log.Error.Emit("Internal: Unexpected event group in: {0}", eventReportEx);
                        break;
                }
            }
        }

        private void SetupEventHandling()
        {
            parent.e087IDsConfig.LTS_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.LTS, TransitionNum = index + 1 });
            parent.e087IDsConfig.COSM_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.COSM, TransitionNum = index + 1 });
            parent.e087IDsConfig.AMS_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.AMS, TransitionNum = index + 1 });
            parent.e087IDsConfig.LRS_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.LRS, TransitionNum = index + 1 });
            parent.e087IDsConfig.LCAS_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.LCAS, TransitionNum = index + 1 });

            new[]
            {
                parent.e087IDsConfig.CarrierApproachingCompleteEvent_CEID,
                parent.e087IDsConfig.CarrierClampedEvent_CEID,
                parent.e087IDsConfig.CarrierClosedEvent_CEID,
                parent.e087IDsConfig.CarrierOpenedEvent_CEID,
                parent.e087IDsConfig.CarrierUnclampedEvent_CEID,
                parent.e087IDsConfig.CarrierLocationChangeEvent_CEID,
                parent.e087IDsConfig.CarrierIDReadFailEvent_CEID,
                parent.e087IDsConfig.IDReaderAvailableEvent_CEID,
                parent.e087IDsConfig.IDReaderUnavailableEvent_CEID,
                parent.e087IDsConfig.UnknownCarrierIDEvent_CEID,
            }.DoForEach(eventID => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.Carrier });

            parent.e040IDsConfig.PRJob_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.E040, TransitionNum = index + 1 });
            parent.e094IDsConfig.CtrlJob_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.E094, TransitionNum = index + 1 });
            parent.e090IDsConfig.SOSM_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.E090_SOSM, TransitionNum = index + 1 });
            parent.e090IDsConfig.SLOSM_Transition_EventIDList.GetValue<IList<ValueContainer>>(rethrow: true).DoForEach((eventID, index) => eventIDToEventInfoDictionary[eventID] = new EventInfo() { EventGroup = EventGroup.E090_SLOSM, TransitionNum = index + 1 });
        }

        /// <summary>
        /// None (0), LTS, COSM, AMS, LRS, LCAS, Carrier, E040, E094, E090_SOSM, E090_SLOSM
        /// </summary>
        enum EventGroup : int
        {
            None = 0,
            LTS,
            COSM,
            AMS,
            LRS,
            LCAS,
            Carrier,
            E040,
            E094,
            E090_SOSM,
            E090_SLOSM,
        }

        struct EventInfo
        {
            public EventGroup EventGroup;
            public int TransitionNum;

            public override string ToString()
            {
                if (TransitionNum == 0)
                    return "EventGroup {0}".CheckedFormat(EventGroup);
                else
                    return "EventGroup {0}, Transition {1}".CheckedFormat(EventGroup, TransitionNum);
            }
        }

        struct EventReportEx
        {
            public EventInfo EventInfo;
            public EventReport EventReport;

            public override string ToString()
            {
                return "{0} {1}".CheckedFormat(EventInfo.EventGroup, EventReport);
            }
        }

        Dictionary<ValueContainer, EventInfo> eventIDToEventInfoDictionary = new Dictionary<ValueContainer, EventInfo>();

        private void HandleE87LTSEventReport(EventInfo eventInfo, EventReport eventReport, INamedValueSet nvs)
        {
            state.LTS = nvs["LTS"].VC.GetValue<LTS>(rethrow: true);
            state.LTS_CarrierID = ((state.LTS == LTS.ReadyToUnload) ? nvs["CID"].VC.GetValue<string>(rethrow: true) : string.Empty);

            stateVSA.Set();
        }

        private void HandleE87COSMEventReport(EventInfo eventInfo, EventReport eventReport, INamedValueSet nvs)
        {
            switch ((COSM_Transition) eventInfo.TransitionNum)
            {
                case COSM_Transition.Transition1: // Carrier object created
                    state.CIDS = CIDS.Undefined;
                    state.CIDS_Reason = "Transition {0} [Construction]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = string.Empty;
                    state.CSMS = CSMS.Undefined;
                    state.CSMS_Reason = "Transition {0} [Construction]".CheckedFormat(eventInfo.TransitionNum);
                    state.CSMS_SlotMap = string.Empty;
                    state.CSMS_Reason = string.Empty;
                    state.CSMS_LocationID = string.Empty;
                    state.CAS = CAS.Undefined;
                    break;
                case COSM_Transition.Transition2: // CIDS: NoState -> NotRead
                    state.CIDS = CIDS.IDNotRead;
                    state.CIDS_Reason = "Transition {0} [Bind or CarrierNotification]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition3: // CIDS: NoState -> WaitingForHost
                    state.CIDS = CIDS.WaitingForHost;
                    state.CIDS_Reason = "Transition {0} [CarrierID read]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition4: // CIDS: NoState -> VerificationOk
                    state.CIDS = CIDS.IDVerificationOk;
                    state.CIDS_Reason = "Transition {0} [ProceedWithCarrier forced ID]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition5: // CIDS: NoState -> VerificationFail
                    state.CIDS = CIDS.IDVerificationFailed;
                    state.CIDS_Reason = "Transition {0} [CancelCarrier]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition6: // CIDS: NotRead -> VerificationOk
                    state.CIDS = CIDS.IDVerificationOk;
                    state.CIDS_Reason = "Transition {0} [CarrierID read]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition7: // CIDS: NotRead -> WaitingForHost
                    state.CIDS = CIDS.WaitingForHost;
                    state.CIDS_Reason = "Transition {0} [CarrierID not read successfully]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition8: // CIDS: WaitingForHost -> VerificationOk 
                    state.CIDS = CIDS.IDVerificationOk;
                    state.CIDS_Reason = "Transition {0} [ProceedWithCarrier]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition9: // CIDS: WaitingForHost -> VerificationFail
                    state.CIDS = CIDS.IDVerificationFailed;
                    state.CIDS_Reason = "Transition {0} [CancelCarrier]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition10: // CIDS: IdNotRead -> WaitingForHost
                    state.CIDS = CIDS.WaitingForHost;
                    state.CIDS_Reason = "Transition {0} [Reader not available and not bypassed]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition11: // CIDS: IdNotRead -> VerificationOk
                    state.CIDS = CIDS.IDVerificationOk;
                    state.CIDS_Reason = "Transition {0} [Reader bypassed]".CheckedFormat(eventInfo.TransitionNum);
                    state.CIDS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition12: // SMS: NoState -> NotRead
                    state.CSMS = CSMS.SlotMapNotRead;
                    state.CSMS_Reason = "Transition {0} [Initial State]".CheckedFormat(eventInfo.TransitionNum);
                    break;
                case COSM_Transition.Transition13: // SMS: NotRead -> VerificationOk
                    state.CSMS = CSMS.SlotMapVerificationOk;
                    state.CSMS_Reason = "Transition {0} [SlotMap read and matches]".CheckedFormat(eventInfo.TransitionNum);
                    state.CSMS_SlotMap = nvs["SlotMap"].VC.ToSlotStateString(rethrow: false);
                    state.CSMS_LocationID = nvs["LocID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition14: // SMS: NotRead -> WaitingForHost
                    state.CSMS = CSMS.WaitingForHost;
                    state.CSMS_Reason = "Transition {0} [SlotMap read, '{1}']".CheckedFormat(eventInfo.TransitionNum, nvs["Reason"].VC.GetValue<string>(rethrow: false));
                    state.CSMS_SlotMap = nvs["SlotMap"].VC.ToSlotStateString(rethrow: false);
                    state.CSMS_LocationID = nvs["LocID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition15: // SMS: WaitingForHost -> VerificationOk
                    state.CSMS = CSMS.SlotMapVerificationOk;
                    state.CSMS_Reason = "Transition {0} [ProceedWithCarrier]".CheckedFormat(eventInfo.TransitionNum);
                    state.CSMS_LocationID = nvs["LocID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition16: // SMS: WaitingForHost -> VerificationFail
                    state.CSMS = CSMS.SlotMapVerificationFailed;
                    state.CSMS_Reason = "Transition {0} [CancelCarrier]".CheckedFormat(eventInfo.TransitionNum);
                    state.CSMS_LocationID = nvs["LocID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    break;
                case COSM_Transition.Transition17: // CAS: NoState -> NotAccessed
                    state.CAS = CAS.NotAccessed;
                    break;
                case COSM_Transition.Transition18: // CAS: NotAccessed -> InAccess
                    state.CAS = CAS.InAccess;
                    break;
                case COSM_Transition.Transition19: // CAS: InAccess -> CarrierComplete,
                    state.CAS = CAS.CarrierComplete;
                    break;
                case COSM_Transition.Transition20: // CAS: InAccess -> CarrierStopped,
                    state.CAS = CAS.CarrierStopped;
                    break;
                case COSM_Transition.Transition21: // Carrrier object removed
                    state.CIDS = CIDS.Undefined;
                    state.CIDS_Reason = "Transition {0} [Destruction]".CheckedFormat(eventInfo.TransitionNum);
                    state.CSMS = CSMS.Undefined;
                    state.CSMS_Reason = "Transition {0} [Destruction]".CheckedFormat(eventInfo.TransitionNum);
                    state.CAS = CAS.Undefined;

                    state.PPID = string.Empty;
                    break;
                default:
                    Log.Error.Emit("Unexpected COSM transition {0} event: {1}", eventInfo.TransitionNum, eventReport);
                    break;
            }

            stateVSA.Set();
        }

        private void HandleE87AMSEventReport(EventInfo eventInfo, EventReport eventReport, INamedValueSet nvs)
        {
            state.AMS = nvs["AMS"].VC.GetValue<AMS>(rethrow: true);

            stateVSA.Set();
        }

        private void HandleE87LRSEventReport(EventInfo eventInfo, EventReport eventReport, INamedValueSet nvs)
        {
            state.LRS = nvs["LRS"].VC.GetValue<LRS>(rethrow: true);

            stateVSA.Set();
        }

        private void HandleE87LCASEventReport(EventInfo eventInfo, EventReport eventReport, INamedValueSet nvs)
        {
            state.LCAS = nvs["LCAS"].VC.GetValue<LCAS>(rethrow: true);
            state.LCAS_CarrierID = nvs["CID"].VC.GetValue<string>(rethrow: true);

            stateVSA.Set();
        }

        private void HandleE87CarrierEventReport(EventReport eventReport)
        {
            state.LastCarrierEventInfo = "{0} {1}".CheckedFormat(eventReport.EventSpec.Name, eventReport.NVS.SafeToStringSML());

            stateVSA.Set();
        }

        private void HandleE40EventReport(EventInfo eventInfo, EventReport eventReport, INamedValueSet nvs)
        {
            var prJobID = nvs["PRJobID"].VC.GetValue<string>(rethrow: true);

            int indexOfPJID = state.PJIDList.FindIndex(item => prJobID == item);

            if (state.PJIDList.IsSafeIndex(indexOfPJID))
            {
                switch (eventInfo.TransitionNum)
                {
                    case 7: // PJ was deleted
                    case 18: // PJ was dequeued
                        state.PJIDList.RemoveAt(indexOfPJID);
                        state.PRJobStateList.RemoveAt(indexOfPJID);
                        break;
                    default:
                        var prJobState = nvs["PRJobState"].VC.GetValue<PRJobState>(rethrow: true);
                        state.PRJobStateList[indexOfPJID] = prJobState;
                        break;
                }

                stateVSA.Set();
            }
        }

        private void HandleE94EventReport(EventInfo eventInfo, EventReport eventReport, INamedValueSet nvs)
        {
            var ctrlJobID = nvs["CtrlJobID"].VC.GetValue<string>(rethrow: true);

            if (state.CJID == ctrlJobID)
            {
                switch (eventInfo.TransitionNum)
                {
                    case 1: // CJ Creation -> Queeud
                        state.CJState = "Queued";
                        break;
                    case 2: // CJ Queued -> Extinction
                        state.CJID = string.Empty;
                        state.CJState = "CJ was Dequeued";
                        break;
                    case 3: // CJ Queued -> Selected
                        state.CJState = "Selected";
                        break;
                    case 4: // CJ Selected -> Queued
                        state.CJState = "Queued (deselected)";
                        break;
                    case 5: // CJ Selected -> Executing
                        state.CJState = "Executing (auto start)";
                        break;
                    case 6: // CJ Selected -> WaitingForStart
                        state.CJState = "WaitingForStart";
                        break;
                    case 7: // CJ WaitingForStart -> Executing
                        state.CJState = "Executing (manual start)";
                        break;
                    case 8: // CJ Executing -> Paused
                        state.CJState = "Paused";
                        break;
                    case 9: // CJ Paused -> Executing
                        state.CJState = "Executing (resume)";
                        break;
                    case 10: // CJ Executing -> Completed
                        state.CJState = "Completed (normally)";
                        break;
                    case 11: // CJ Active -> Completed (by stop)
                        state.CJState = "Completed (Stopped)";
                        break;
                    case 12: // CJ Active -> Completed (by abort)
                        state.CJState = "Completed (Aborted)";
                        break;
                    case 13: // CJ Completed -> no state
                        state.CJState = "CJ was Deleted";
                        state.CJID = string.Empty;
                        break;
                    default:
                        Log.Error.Emit("Unexpected CtrlJob transition {0} event: {1}", eventInfo.TransitionNum, eventReport);
                        break;
                }

                stateVSA.Set();
            }
        }

        private void HandleE90SLOSMEventReport(EventInfo eventInfo, EventReport eventReport, INamedValueSet nvs)
        {
            var substID = nvs["SubstID"].VC.GetValue<string>(rethrow: false);
            var src = nvs["Src"].VC.GetValue<string>(rethrow: false);
            var dest = nvs["Dest"].VC.GetValue<string>(rethrow: false);

            var substIsKnownHere = substInfoDictionary.ContainsKey(substID);
            var substIsFromThisCarrier = (src.StartsWith(state.LCAS_CarrierID) || dest.StartsWith(state.LCAS_CarrierID));

            if (substIsKnownHere || substIsFromThisCarrier)
            {
                E090SubstInfo substInfo;

                if (substIsKnownHere)
                {
                    substInfo = substInfoDictionary.SafeTryGetValue(substID);
                }
                else
                {
                    var substObjID = new E039ObjectID(substID, MosaicLib.Semi.E090.Constants.SubstrateObjectType, assignUUID: false);
                    var srcLocObjID = (src.IsNeitherNullNorEmpty() ? new E039ObjectID(src, MosaicLib.Semi.E090.Constants.SubstrateLocationObjectType, assignUUID: false) : E039ObjectID.Empty);
                    var destLocObjID = (src == dest) ? srcLocObjID : ((dest.IsNeitherNullNorEmpty() ? new E039ObjectID(dest, MosaicLib.Semi.E090.Constants.SubstrateLocationObjectType, assignUUID: false) : E039ObjectID.Empty));

                    substInfo = new E090SubstInfo()
                    {
                        ObjID = substObjID,
                        LinkToSrc = new E039Link(substObjID, srcLocObjID, MosaicLib.Semi.E090.Constants.SourceLocationLinkKey),
                        LinkToDest = new E039Link(substObjID, destLocObjID, MosaicLib.Semi.E090.Constants.DestinationLocationLinkKey),
                    };
                }

                substInfo.LocID = nvs["LocID"].VC.GetValue<string>(rethrow: false);
                substInfo.LotID = nvs["LotID"].VC.GetValue<string>(rethrow: false);
                substInfo.SubstUsage = nvs["Usage"].VC.GetValue<SubstUsage>(rethrow: false);

                substInfo.STS = nvs["STS"].VC.GetValue<SubstState>(rethrow: false);
                substInfo.SPS = nvs["SPS"].VC.GetValue<SubstProcState>(rethrow: false, defaultValue: SubstProcState.Undefined);

                var sosmTransition = (SubstState_Transition)eventInfo.TransitionNum;
                bool creationEvent = (sosmTransition == SubstState_Transition.Transition1 || sosmTransition == SubstState_Transition.Transition10);
                bool extinctionEvent = (sosmTransition == SubstState_Transition.Transition7 || sosmTransition == SubstState_Transition.Transition9);

                if (extinctionEvent && substIsKnownHere)
                {
                    substInfoDictionary.Remove(substID);
                    UpdateTally();
                    stateVSA.Set();
                }
                else if (creationEvent || substIsKnownHere)
                {
                    substInfoDictionary[substID] = substInfo;
                    UpdateTally();
                    stateVSA.Set();
                }
                else if (!extinctionEvent)
                {
                    // else ignore the event
                }
            }
        }

        private void UpdateTally()
        {
            substStateTally.Clear();
            substInfoDictionary.ValueArray.DoForEach(substInfo => substStateTally.Add(substInfo, SubstrateJobState.Initial));

            state.E090StateTally = "sts:[{0}] sps:[{1}]".CheckedFormat(substStateTally.STSToString(), substStateTally.SPSToString());
        }

        IDictionaryWithCachedArrays<string, E090SubstInfo> substInfoDictionary = new IDictionaryWithCachedArrays<string, E090SubstInfo>();
        SubstrateStateTally substStateTally = new SubstrateStateTally();

        #endregion
    }

    public class E030IDsConfig
    {
        [ConfigItem(IsOptional = true)]
        public string GoRemoteCmd;

        [ConfigItem(IsOptional = true)]
        public string GoLocalCmd;

        [ConfigItem(IsOptional = true)]
        public TimeSpan SVIDTraceInterval = (0.5).FromSeconds();

        [ConfigItem(IsOptional = true)]
        public int SVIDTraceMaxItemCount = 0;

        [ConfigItem(IsOptional = true)]
        public int SVIDTraceTOTSMP = int.MaxValue;

        [ConfigItem(IsOptional = true)]
        public byte TermMesgToHostMinTermID = 0;

        [ConfigItem(IsOptional = true)]
        public byte TermMesgToHostMaxTermID = 1;

        [ConfigItem(IsOptional = true)]
        public byte DefaultTermMesgToEqpTermID = 0;

        [ConfigItem(IsOptional = true)]
        public int TermMesgToEqpMaxLinesToSend = 0;

        [ConfigItem]
        public ValueContainer AlarmID_DVID;

        [ConfigItem]
        public ValueContainer ECID_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer EventLimit_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer LimitVariable_DVID;

        [ConfigItem]
        public ValueContainer PPChangeName_DVID;

        [ConfigItem]
        public ValueContainer PPChangeStatus_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer PPError_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer RcpChangeName_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer RcpChangeStatus_DVID;

        [ConfigItem]
        public ValueContainer TransitionType_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer OperatorCommand_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer EstablishCommunicationTimeout_ECID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer MaxSpoolTransmit_ECID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer OverWriteSpool_ECID;

        [ConfigItem]
        public ValueContainer TimeFormat_ECID;

        [ConfigItem]
        public ValueContainer AlarmsEnabled_SVID;

        [ConfigItem]
        public ValueContainer AlarmsSet_SVID;

        [ConfigItem]
        public ValueContainer Clock_SVID;

        [ConfigItem]
        public ValueContainer ControlState_SVID;

        [ConfigItem]
        public ValueContainer EventsEnabled_SVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer PPError_SVID;

        [ConfigItem]
        public ValueContainer PPExecName_SVID;

        [ConfigItem]
        public ValueContainer PPFormat_SVID;

        [ConfigItem]
        public ValueContainer PreviousProcessState_SVID;

        [ConfigItem]
        public ValueContainer ProcessState_SVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer RcpExecName_SVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SpoolCountActual_SVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SpoolCountTotal_SVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SpoolFullTime_SVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SpoolStartTime_SVID;

        [ConfigItem]
        public ValueContainer EquipmentOffline_CEID;

        [ConfigItem]
        public ValueContainer ControlStateLocal_CEID;

        [ConfigItem]
        public ValueContainer ControlStateRemote_CEID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer OperatorCommandIssued_CEID;

        [ConfigItem]
        public ValueContainer ProcessingStarted_CEID;

        [ConfigItem]
        public ValueContainer ProcessingCompleted_CEID;

        [ConfigItem]
        public ValueContainer ProcessingStopped_CEID;

        [ConfigItem]
        public ValueContainer ProcessingStateChange_CEID;

        [ConfigItem]
        public ValueContainer OperatorEquipmentConstantChange_CEID;

        [ConfigItem]
        public ValueContainer ProcessProgramChange_CEID;

        [ConfigItem]
        public ValueContainer ProcessProgramSelected_CEID;

        [ConfigItem]
        public ValueContainer MaterialReceived_CEID;

        [ConfigItem]
        public ValueContainer MaterialRemoved_CEID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SpoolingActivated_CEID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SpoolingDeactivited_CEID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SpoolTransmitFailure_CEID;

        [ConfigItem]
        public ValueContainer MessageRecognition_CEID;
    }

    public class E087IDsConfig
    {
        [ConfigItem]
        public string E039CarrierObjectTypeName = "Carrier";

        [ConfigItem]
        public string E039LoadPortObjectTypeName = "LoadPort";

        [ConfigItem]
        public ValueContainer AccessMode_SVIDList;

        [ConfigItem(IsOptional =true)]
        public ValueContainer BypassReadID_SVIDList;

        [ConfigItem]
        public ValueContainer BypassReadID_SVID;

        [ConfigItem]
        public ValueContainer CarrierID_SVIDList;

        [ConfigItem]
        public ValueContainer CarrierLocationMatrix_SVID;

        [ConfigItem]
        public ValueContainer LocationID_SVIDList;

        [ConfigItem]
        public ValueContainer LoadPortReserverationState_SVIDList;

        [ConfigItem(IsOptional = true)]
        public ValueContainer LoadPortReserverationStateList_SVID;

        [ConfigItem]
        public ValueContainer PortAssociationState_SVIDList;

        [ConfigItem]
        public ValueContainer PortStateInfo_SVIDList;

        [ConfigItem(IsOptional = true)]
        public ValueContainer PortStateInfoList_SVID;

        [ConfigItem]
        public ValueContainer PortTransferState_SVIDList;

        [ConfigItem(IsOptional = true)]
        public ValueContainer PortTransferStateList_SVID;

        [ConfigItem]
        public ValueContainer LTS_Transition_EventIDList;

        [ConfigItem]
        public ValueContainer LTS_CarrierID_DVID;

        [ConfigItem]
        public ValueContainer LTS_PortID_DVID;

        [ConfigItem]
        public ValueContainer LTS_PortTransferState_DVID;

        [ConfigItem] 
        public ValueContainer COSM_Transition_EventIDList;

        [ConfigItem]
        public ValueContainer COSM_PortID_DVID;

        [ConfigItem]
        public ValueContainer COSM_CarrierIDStatus_DVID;

        [ConfigItem]
        public ValueContainer COSM_CarrierID_DVID;

        [ConfigItem]
        public ValueContainer COSM_SlotMap_DVID;

        [ConfigItem]
        public ValueContainer COSM_SlotMapStatus_DVID;

        [ConfigItem]
        public ValueContainer COSM_LocationID_DVID;

        [ConfigItem]
        public ValueContainer COSM_Reason_DVID;

        [ConfigItem]
        public ValueContainer COSM_CarrierAccessingStatus_DVID;

        [ConfigItem]
        public ValueContainer AMS_Transition_EventIDList;

        [ConfigItem]
        public ValueContainer AMS_PortID_DVID;

        [ConfigItem]
        public ValueContainer AMS_AccessMode_DVID;

        [ConfigItem]
        public ValueContainer LRS_Transition_EventIDList;

        [ConfigItem]
        public ValueContainer LRS_PortID_DVID;

        [ConfigItem]
        public ValueContainer LRS_LoadPortReservationState_DVID;

        [ConfigItem]
        public ValueContainer LCAS_Transition_EventIDList;

        [ConfigItem]
        public ValueContainer LCAS_PortID_DVID;

        [ConfigItem]
        public ValueContainer LCAS_CarrierID_DVID;

        [ConfigItem]
        public ValueContainer LCAS_PortAssociationState_DVID;

        [ConfigItem]
        public ValueContainer CarrierApproachingCompleteEvent_CEID; // CarrierEvent_CarrierID

        [ConfigItem]
        public ValueContainer CarrierClampedEvent_CEID; // CarrierEvent_CarrierID, CarrierEvent_PortID, CarrierEvent_LocationID

        [ConfigItem]
        public ValueContainer CarrierClosedEvent_CEID; // CarrierEvent_CarrierID, CarrierEvent_PortID, CarrierEvent_LocationID

        [ConfigItem]
        public ValueContainer CarrierLocationChangeEvent_CEID; // CarrierEvent_CarrierID, CarrierEvent_LocationID, CarrierLocationMatrix (svid)

        [ConfigItem]
        public ValueContainer CarrierOpenedEvent_CEID; // CarrierEvent_CarrierID, CarrierEvent_PortID, CarrierEvent_LocationID

        [ConfigItem]
        public ValueContainer CarrierUnclampedEvent_CEID; // CarrierEvent_CarrierID, CarrierEvent_PortID, CarrierEvent_LocationID

        [ConfigItem]
        public ValueContainer CarrierIDReadFailEvent_CEID; // CarrierEvent_PortID

        [ConfigItem]
        public ValueContainer IDReaderAvailableEvent_CEID; // CarrierEvent_PortID

        [ConfigItem]
        public ValueContainer IDReaderUnavailableEvent_CEID; // CarrierEvent_PortID

        [ConfigItem]
        public ValueContainer UnknownCarrierIDEvent_CEID; // CarrierEvent_PortID

        [ConfigItem]
        public ValueContainer CarrierEvent_CarrierID_DVID;

        [ConfigItem]
        public ValueContainer CarrierEvent_PortID_DVID;

        [ConfigItem]
        public ValueContainer CarrierEvent_LocationID_DVID;
    }

    public class E040IDsConfig
    {
        [ConfigItem]
        public string E039ProcessJobObjectTypeName = "PROCESSJOB";

        [ConfigItem]
        public ValueContainer PRJob_Transition_EventIDList;

        [ConfigItem]
        public ValueContainer PRJob_Transition_PRJobID_DVID;

        [ConfigItem]
        public ValueContainer PRJob_Transition_PRJobState_DVID;

        [ConfigItem]
        public ValueContainer PRJob_Transition_RecID_DVID;

        [ConfigItem]
        public ValueContainer PRJob_Transition_RecVariableList_DVID;

        [ConfigItem]
        public ValueContainer PRJob_Transition_PRMtlNameList_DVID;
    }

    public class E094IDsConfig
    {
        [ConfigItem]
        public string E039ControlJobObjectTypeName = "ControlJob";

        [ConfigItem]
        public ValueContainer CtrlJob_Transition_EventIDList;

        [ConfigItem]
        public ValueContainer CtrlJob_Transition_CtrlJobID_DVID;
    }

    public class E090IDsConfig
    {
        [ConfigItem]
        public string E039SubstrateObjectTypeName = "Substrate";

        [ConfigItem]
        public string E039SubstLocObjectTypeName = "SubstLoc";

        [ConfigItem]
        public ValueContainer SOSM_Transition_EventIDList;  // 21 transitions

        [ConfigItem(IsOptional =true)]
        public ValueContainer SOSM_Transition_AcquiredID_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_LotID_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstDestination_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstHistory_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstID_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstIDStatus_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstLocID_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SOSM_Transition_SubstMtrlStatus_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstProcState_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstSource_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstState_DVID;

        [ConfigItem(IsOptional = true)]
        public ValueContainer SOSM_Transition_SubstType_DVID;

        [ConfigItem]
        public ValueContainer SOSM_Transition_SubstUsage_DVID;

        [ConfigItem]
        public ValueContainer SLOSM_Transition_EventIDList;  // 2 transitions

        [ConfigItem]
        public ValueContainer SLOSM_Transition_SubstLocID_DVID;

        [ConfigItem]
        public ValueContainer SLOSM_Transition_SubstLocState_DVID;

        [ConfigItem]
        public ValueContainer SLOSM_Transition_SubstLocSubstID_DVID;
    }
}
