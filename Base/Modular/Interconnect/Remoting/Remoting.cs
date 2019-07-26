//-------------------------------------------------------------------
/*! @file Interconnect/Remoting/Remoting.cs
 *  @brief Common Message related definitions for Modular.Interconnect.Remoting
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
using System.Reflection;
using System.Linq;

using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.Attributes;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Parts;
using MosaicLib.Modular.Interconnect.Remoting.Buffers;
using MosaicLib.Modular.Interconnect.Remoting.MessageStreamTools;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Utils.Tools;

// Please NOTE: The code in this namespace has reached an initial level of stability and utility.  However, when using this code, please keep in mind
// that there are some non-trivial functional changes planed for the internal design and operation of the Remoting infrastructure, primarily in relation
// to extensions of the MessageStreamTool functionality and implementation.  In addition the basic IRemoting interface is expected to have occasional additions.
// As such it is not currently recommended to attempt to subclass or generate other derived or modified versions of the classes presented here at this point.

namespace MosaicLib.Modular.Interconnect.Remoting
{
    public interface IRemoting
    {
        IClientFacet Sync(SyncFlags syncFlags = default(SyncFlags));

        INotificationObject<INamedValueSet> ServerInfoNVSPublisher { get; }

        INamedValueSet ConfigNVS { get; }
    }

    [Flags]
    public enum SyncFlags : int
    {
        Default = 0x00,
    }

    public class RemotingServerConfig : ICopyable<RemotingServerConfig>
    {
        public string PartID { get; set; }
        public INamedValueSet ConfigNVS { get; set; }
        public INamedValueSet ServerInfoNVS { get; set; }
        public IConfig IConfig { get; set; }
        public IIVIRegistration IIVIRegistration { get; set; }
        public IValuesInterconnection DefaultIVI { get; set; }
        public ISetsInterconnection ISetsInterconnection { get; set; }
        public IPartsInterconnection IPartsInterconnection { get; set; }
        public IValuesInterconnection PartIVI { get; set; }

        public RemotingServerConfig MakeCopyOfThis(bool deepCopy = true) 
        {
            return new RemotingServerConfig()
            {
                PartID = PartID,
                ConfigNVS = ConfigNVS.ConvertToReadOnly(mapNullToEmpty: true),
                ServerInfoNVS = ServerInfoNVS.ConvertToReadOnly(mapNullToEmpty: true),
                IConfig = IConfig,
                IIVIRegistration = IIVIRegistration,
                DefaultIVI = DefaultIVI,
                ISetsInterconnection = ISetsInterconnection,
                IPartsInterconnection = IPartsInterconnection,
                PartIVI = PartIVI ?? Interconnect.Values.Values.Instance,
            };
        }
    }

    public class RemotingClientConfig : ICopyable<RemotingClientConfig>
    {
        public string PartID { get; set; }
        public INamedValueSet ConfigNVS { get; set; }
        public MessageStreamTools.MessageStreamToolConfigBase[] StreamToolsConfigArray { get; set; }
        public IValuesInterconnection PartIVI { get; set; }

        public RemotingClientConfig MakeCopyOfThis(bool deepCopy = true) 
        {
            return new RemotingClientConfig()
            {
                PartID = PartID,
                ConfigNVS = ConfigNVS.ConvertToReadOnly(mapNullToEmpty: true),
                StreamToolsConfigArray = StreamToolsConfigArray.SafeToArray(mapNullToEmpty: true),
                PartIVI = PartIVI ?? Interconnect.Values.Values.Instance,
            };
        }
    }

    public class RemotingServer : SimpleActivePartBase, IRemoting
    {
        public RemotingServer(RemotingServerConfig config)
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior: true, partBaseIVI: config.PartIVI))
        {
            Config = config.MakeCopyOfThis();
            traceLogger = new Logging.Logger(PartID + ".Trace", groupName: Logging.LookupDistributionGroupName, initialInstanceLogGate: Config.ConfigNVS["TraceLogger.InitialInstanceLogGate"].VC.GetValue(rethrow: false, defaultValue: Logging.LogGate.Debug));

            bufferPool = new BufferPool(PartID + ".bp", configNVS: Config.ConfigNVS, bufferStateEmitter: traceLogger.Trace);

            AddMainThreadStoppingAction(Release);

            ActionLoggingReference.Config = ActionLoggingConfig.Debug_Debug_Trace_Trace;

            _serverInfoNVSPublisher = new InterlockedNotificationRefObject<INamedValueSet>(Config.ServerInfoNVS);

            EventAndPerformanceRecording = new EventAndPerformanceRecording(PartID, Config.PartIVI);

            sessionCountIVA = Config.PartIVI.GetValueAccessor<int>("{0}.SessionCount".CheckedFormat(PartID)).Set(0);
            sessionStatesIVA = Config.PartIVI.GetValueAccessor<INamedValueSet>("{0}.SessionStates".CheckedFormat(PartID)).Set(NamedValueSet.Empty);
            ivaArray = new IValueAccessor[] { sessionCountIVA, sessionStatesIVA };
        }

        public readonly string MyUUID = Guid.NewGuid().ToString();

        public INotificationObject<INamedValueSet> ServerInfoNVSPublisher { get { return _serverInfoNVSPublisher; } }
        private InterlockedNotificationRefObject<INamedValueSet> _serverInfoNVSPublisher;

        private RemotingServerConfig Config { get; set; }
        public INamedValueSet ConfigNVS { get { return Config.ConfigNVS; } }

        private IEventAndPerformanceRecording EventAndPerformanceRecording { get; set; }

        private Logging.IBasicLogger traceLogger;

        private Transport.ITransportConnection transport;
        private Sessions.SessionManager sessionManager;
        private BufferPool bufferPool;

        private IValueAccessor<int> sessionCountIVA;
        private IValueAccessor<INamedValueSet> sessionStatesIVA;
        private bool sessionsChanged = false;
        private IValueAccessor[] ivaArray;

        private class ClientSessionTracker
        {
            /// <summary>Generally this is the same as the session.SessionName</summary>
            public string clientName;
            /// <summary>Generally this is the same as the session.ClientUUID</summary>
            public string uuid;
            /// <summary>Generally this is the same as the session.ClientInstanceNum</summary>
            public ulong instanceNum;

            public string fullName;

            public Sessions.IMessageSessionFacet session;
            public Sessions.SessionStateCode lastSessionStateCode;

            public IListWithCachedArray<MessageStreamToolTracker> messageStreamToolTrackerList = new IListWithCachedArray<MessageStreamToolTracker>();

            public MessageStreamTools.IActionRelayMessageStreamTool actionRelayStreamTool;

            public void Release()
            {
                var messageStreamToolTrackerArray = messageStreamToolTrackerList.Array.WhereIsNotDefault().ToArray();

                messageStreamToolTrackerList.Clear();

                messageStreamToolTrackerArray.DoForEach(mstt => mstt.Release());
            }

            public override string ToString()
            {
                return "CST {0} uuid:{1} instNum:".CheckedFormat(clientName, uuid, instanceNum);
            }
        }

        private IListWithCachedArray<ClientSessionTracker> clientSessionTrackerList = new IListWithCachedArray<ClientSessionTracker>();
        private Dictionary<string, ClientSessionTracker> uuidToClientSessionTrackerDictionary = new Dictionary<string, ClientSessionTracker>();
        private Dictionary<string, ClientSessionTracker> clientNameToClientSessionTrackerDictionary = new Dictionary<string, ClientSessionTracker>();

        private void AddClientSessionTracker(ClientSessionTracker cst)
        {
            cst.fullName = "{0}:{1}:{2}".CheckedFormat(cst.clientName, cst.instanceNum, cst.uuid);

            clientSessionTrackerList.Add(cst);
            uuidToClientSessionTrackerDictionary[cst.uuid] = cst;
            clientNameToClientSessionTrackerDictionary[cst.clientName] = cst;

            sessionsChanged = true;
        }

        private ClientSessionTracker[] RemoveAndReturnPerminentlyClosedSessions()
        {
            List<ClientSessionTracker> removeCSTList = new List<ClientSessionTracker>();

            for (int idx = 0; idx < clientSessionTrackerList.Count; )
            {
                var cst = clientSessionTrackerList[idx];
                if (cst.session.State.IsPerminantlyClosed)
                {
                    removeCSTList.Add(cst);
                    clientSessionTrackerList.RemoveAt(idx);
                    uuidToClientSessionTrackerDictionary.Remove(cst.uuid);
                    clientNameToClientSessionTrackerDictionary.Remove(cst.clientName);

                    sessionsChanged = true;
                }
                else
                {
                    idx++;
                }
            }

            return removeCSTList.ToArray();
        }

        private class MessageStreamToolTracker
        {
            public int stream;
            public MessageStreamTools.IMessageStreamTool messageStreamTool;
            public Messages.Message lastGeneratedMessage;

            public void Release()
            {
                Fcns.DisposeOfObject(ref messageStreamTool);
                lastGeneratedMessage = null;        // the message can only be safely "released" if it has been delivered.
            }
        }

        private void Release()
        {
            var clientSessionTrackerArray = clientSessionTrackerList.Array;

            clientSessionTrackerList.Clear();
            
            clientSessionTrackerArray.DoForEach(cst => cst.Release());

            uuidToClientSessionTrackerDictionary.Clear();
            clientNameToClientSessionTrackerDictionary.Clear();

            Fcns.DisposeOfObject(ref transport);
            Fcns.DisposeOfObject(ref sessionManager);

            sessionsChanged = true;
        }

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            string ec = string.Empty;

            try
            {
                if (andInitialize && BaseState.IsOnlineOrAttemptOnline)
                    ec = PerformGoOfflineAction();

                if (sessionManager == null || transport == null)
                {
                    sessionManager = new Sessions.SessionManager(PartID, MyUUID, Config.ConfigNVS, hostNotifier: this, handleNewSessionDelegate: HandleNewSession, bufferPool: bufferPool, stateEmitter: Log.Debug, issueEmitter: Log.Debug, traceEmitter: traceLogger.Trace)
                    {
                        EventAndPerformanceRecording = EventAndPerformanceRecording,
                    };

                    transport = Transport.TransportConnectionFactory.Instance.CreateServerConnection(Config.ConfigNVS, sessionManager);

                    sessionManager.TransportTypeFeatures = transport.TransportTypeFeatures;
                }

                return ec;
            }
            catch (System.Exception ex)
            {
                string exStr = "Generated unexpected exception: {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                Log.Debug.Emit("{0}: {1}", CurrentMethodName, exStr);

                Release();

                return ec.MapNullOrEmptyTo(exStr);
            }
        }

        protected override string PerformGoOfflineAction()
        {
            string ec = string.Empty;

            var clientSessionArray = clientSessionTrackerList.Array.Select(cst => cst.session).ToArray();

            foreach (var session in clientSessionArray)
            {
                if (session.State.IsConnected(includeConnectingStates: true, includeClosingStates: true))
                    session.SetState(QpcTimeStamp.Now, Sessions.SessionStateCode.CloseRequested, "Server {0}".CheckedFormat(CurrentActionDescription));
                else if (session.State.StateCode == Sessions.SessionStateCode.ConnectionClosed)
                    session.SetState(QpcTimeStamp.Now, Sessions.SessionStateCode.Terminated, "Server {0} [{1}]".CheckedFormat(CurrentActionDescription, session.State.StateCode));
            }

            TimeSpan maxSessionCloseWaitTime = Config.ConfigNVS["MaxSessionCloseWaitTime"].VC.GetValue(rethrow: false, defaultValue: (1.0).FromSeconds());

            QpcTimer waitTimeLimitTimer = new QpcTimer() { TriggerInterval = maxSessionCloseWaitTime }.Start();

            while (!clientSessionArray.All(session => session.State.IsPerminantlyClosed))
            {
                WaitForSomethingToDo();
                InnerServiceBackground();

                if (waitTimeLimitTimer.IsTriggered)
                {
                    ec = "Client Session close time limit reached after {0:f3} seconds [{1}]".CheckedFormat(waitTimeLimitTimer.ElapsedTimeInSeconds, String.Join(", ", clientSessionArray.Select(session => session.State)));
                    break;
                }
            }

            {
                QpcTimeStamp now = QpcTimeStamp.Now;
                var actionDescription = CurrentActionDescription;

                foreach (var cst in clientSessionTrackerList.Array)
                {
                    if (ec.IsNullOrEmpty())
                        cst.messageStreamToolTrackerList.Array.WhereIsNotDefault().DoForEach(mss => mss.messageStreamTool.ResetState(now, MessageStreamTools.ResetType.ServerSessionClosed));
                    else
                        cst.messageStreamToolTrackerList.Array.WhereIsNotDefault().DoForEach(mss => mss.messageStreamTool.ResetState(now, MessageStreamTools.ResetType.ServerSessionLost, "{0} failed: {1}".CheckedFormat(actionDescription, ec)));
                }
            }

            Release();

            if (!ec.IsNullOrEmpty())
                Log.Debug.Emit("{0} failed: {1}", CurrentActionDescription, ec);

            return ec;
        }

        protected override string PerformServiceActionEx(IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            StringScanner ss = new StringScanner(serviceName);

            if (ss.MatchToken("Remote"))
            {
                string clientIdToken = ss.ExtractToken();

                ClientSessionTracker cst = uuidToClientSessionTrackerDictionary.SafeTryGetValue(clientIdToken) ?? clientNameToClientSessionTrackerDictionary.SafeTryGetValue(clientIdToken);

                if (cst == null)
                    return "Remote client '{0}' not found".CheckedFormat(clientIdToken);
                else
                {
                    if (cst.actionRelayStreamTool == null)
                        return "Remote client '{0}' does not support remote actions".CheckedFormat(clientIdToken);

                    cst.actionRelayStreamTool.RelayServiceAction(ss.Rest, ipf);
                    return null;
                }
            }
            else if (ss.MatchToken("Sync"))
            {
                SyncFlags syncFlags = default(SyncFlags);

                if (ss.IsAtEnd || ss.ParseValue<SyncFlags>(out syncFlags))
                    return PerformSync(syncFlags);
                else
                    return "Invalid or unsupported Sync request format [{0}]".CheckedFormat(serviceName);
            }

            return base.PerformServiceActionEx(ipf, serviceName, npv);
        }

        public IClientFacet Sync(SyncFlags syncFlags = default(SyncFlags))
        {
            BasicActionImpl action = new BasicActionImpl(actionQ, PerformSync, CurrentMethodName, ActionLoggingReference);
            action.NamedParamValues = new NamedValueSet() { { "syncFlags", syncFlags } };
            return action;
        }

        private string PerformSync(IProviderFacet action)
        {
            SyncFlags syncFlags = action.NamedParamValues["syncflags"].VC.GetValue<SyncFlags>(rethrow: true);

            return PerformSync(syncFlags);
        }

        private string PerformSync(SyncFlags syncFlags)
        {
            if (!BaseState.IsOnline)
                return "Part is not online: {0}".CheckedFormat(BaseState);

            InnerServiceBackground();

            return string.Empty;
        }

        protected override void PerformMainLoopService()
        {
            InnerServiceBackground();
        }

        private void InnerServiceBackground(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

            int count = 0;

            if (transport != null)
                count += transport.Service(qpcTimeStamp);

            if (sessionManager != null)
                count += sessionManager.Service(qpcTimeStamp);

            foreach (var cst in clientSessionTrackerList.Array)
            {
                count += cst.messageStreamToolTrackerList.Array.Sum(mstt => InnerServiceStream(qpcTimeStamp, cst, mstt));
            }

            int perminantelyClosedCount = 0;

            foreach (var cst in clientSessionTrackerList.Array)
            {
                var session = cst.session;

                count += session.Service(qpcTimeStamp);

                var sessionStateCode = session.State.StateCode;

                if (cst.lastSessionStateCode != sessionStateCode)
                {
                    sessionsChanged = true;
                    cst.lastSessionStateCode = sessionStateCode;
                }

                perminantelyClosedCount += session.State.IsPerminantlyClosed.MapToInt();
            }

            if (perminantelyClosedCount > 0)
            {
                ClientSessionTracker[] removeCSTArray = RemoveAndReturnPerminentlyClosedSessions();

                foreach (var cst in removeCSTArray)
                {
                    if (cst.session.State.StateCode == Sessions.SessionStateCode.Terminated)
                        cst.messageStreamToolTrackerList.Array.WhereIsNotDefault().DoForEach(mss => mss.messageStreamTool.ResetState(qpcTimeStamp, MessageStreamTools.ResetType.ServerSessionTerminated, cst.session.State.ToString()));
                    else
                        cst.messageStreamToolTrackerList.Array.WhereIsNotDefault().DoForEach(mss => mss.messageStreamTool.ResetState(qpcTimeStamp, MessageStreamTools.ResetType.ServerSessionLost, cst.session.State.ToString()));
                }

                Log.Debug.Emit("Removed perminently closed clients: {0}", String.Join(", ", removeCSTArray.Select(cst => cst.clientName)));

                count += removeCSTArray.Length;
            }

            NoteWorkCount(count);

            EventAndPerformanceRecording.Service(qpcTimeStamp);

            if (sessionsChanged)
            {
                sessionsChanged = false;

                sessionCountIVA.Value = clientSessionTrackerList.Count;

                var nvs = new NamedValueSet();
                foreach (var cst in clientSessionTrackerList.Array)
                    nvs.SetValue(cst.fullName, cst.lastSessionStateCode);

                sessionStatesIVA.Value = nvs;

                Config.PartIVI.Set(ivaArray);
            }
        }

        private void HandleNewSession(QpcTimeStamp qpcTimeStamp, Sessions.IMessageSessionFacet session)
        {
            ClientSessionTracker cst = new ClientSessionTracker()
            {
                session = session,
                clientName = session.SessionName,
                uuid = session.ClientUUID,
                instanceNum = session.ClientInstanceNum,
            };

            // generate a delegate that binds the ClientSessionTracker so that we can correctly route inbound messages
            session.HandleInboundMessageDelegate = ((qts, stream, message) => HandleInboundMessage(qts, cst, stream, message));

            AddClientSessionTracker(cst);

            Log.Debug.Emit("New client session added [{0} {1} {2}]", session.SessionName, session.ClientUUID, session.ClientInstanceNum);
        }

        /// <summary>
        /// NOTE: this method can be called with null mstt in some cases.
        /// </summary>
        private int InnerServiceStream(QpcTimeStamp qpcTimeStamp, ClientSessionTracker cst, MessageStreamToolTracker mstt)
        {
            int count = 0;

            var session = cst.session;

            if (session != null && session.State.IsConnected() && mstt != null)
            {
                if (mstt.lastGeneratedMessage != null)
                {
                    if (mstt.lastGeneratedMessage.State == Messages.MessageState.Delivered)
                    {
                        mstt.lastGeneratedMessage.ReturnBuffersToPool(qpcTimeStamp);
                        mstt.lastGeneratedMessage = null;
                        count++;
                    }
                    else if (mstt.lastGeneratedMessage.State == Messages.MessageState.Failed)
                    {
                        string reason = mstt.lastGeneratedMessage.Reason;
                        Log.Debug.Emit("Message send for client {0} stream {1} failed [{2}, reason:{3}]", cst.clientName, mstt.stream, mstt.messageStreamTool.GetType().GetTypeDigestName(), reason);

                        // do not return message to buffer pool as its buffers are not in a known state anymore.
                        mstt.lastGeneratedMessage = null;

                        mstt.messageStreamTool.ResetState(qpcTimeStamp, MessageStreamTools.ResetType.ServerMessageDeliveryFailure, reason);

                        count++;
                    }
                }

                if (mstt.lastGeneratedMessage == null)
                {
                    var nextMesg = mstt.messageStreamTool.ServiceAndGenerateNextMessageToSend(qpcTimeStamp);
                    if (nextMesg != null)
                    {
                        session.HandleOutboundMessage(qpcTimeStamp, unchecked((ushort)mstt.stream), nextMesg);
                        mstt.lastGeneratedMessage = nextMesg;
                        count++;
                    }
                }
                else
                {
                    count += mstt.messageStreamTool.Service(qpcTimeStamp);
                }
            }

            return count;
        }

        private void HandleInboundMessage(QpcTimeStamp qpcTimeStamp, ClientSessionTracker cst, ushort stream, Messages.Message message)
        {
            MessageStreamToolTracker stt = cst.messageStreamToolTrackerList.Array.SafeAccess(stream);

            bool isConnected = cst.session.State.IsConnected();
            bool isConnecting = !isConnected && cst.session.State.IsConnecting;

            if (!isConnected)
            {
                if (isConnecting)
                {
                    Log.Warning.Emit("{0}: invalid inbound message for client '{1}' stream {2}: session is {3} [while connecting]", CurrentMethodName, cst.clientName, cst.session.State);
                    // handle protocol violation
                }
                else
                {
                    Log.Debug.Emit("{0}: ignoring inbound message for client '{1}' stream {2}: session is {3}", CurrentMethodName, cst.clientName, cst.session.State);
                }
            }
            else if (stt != null && stt.messageStreamTool != null)
            {
                stt.messageStreamTool.HandleInboundMessage(qpcTimeStamp, message);
            }
            else if (message.FirstBufferFlags.IsSet(BufferHeaderFlags.MessageContainsStreamSetup))
            {
                while (stream >= cst.messageStreamToolTrackerList.Count)
                    cst.messageStreamToolTrackerList.Add(null);

                MessageStreamToolTracker mstt = new MessageStreamToolTracker()
                {
                    stream = stream,
                };

                string failureCode = CreateMessageStreamTool(qpcTimeStamp, mstt, message);

                if (failureCode.IsNullOrEmpty())
                {
                    cst.messageStreamToolTrackerList[stream] = mstt;

                    if (mstt.messageStreamTool is MessageStreamTools.IActionRelayMessageStreamTool)
                        cst.actionRelayStreamTool = mstt.messageStreamTool as MessageStreamTools.IActionRelayMessageStreamTool;
                }
                else
                {
                    /// future: review if we should send an error message back for this case.
                    Log.Debug.Emit("{0}: failed to create stream tool for client '{1}' stream {2}: {3}", CurrentMethodName, cst.clientName, failureCode);
                }
            }

            message.ReturnBuffersToPool(qpcTimeStamp);
        }

        private string CreateMessageStreamTool(QpcTimeStamp qpcTimeStamp, MessageStreamToolTracker mstt, Messages.Message message)
        {
            INamedValueSet messageNVS = message.NVS;

            string toolTypeStr = messageNVS["ToolTypeStr"].VC.GetValue<string>(rethrow: false);
            
            MessageStreamTools.IMessageStreamTool messageStreamTool = null;

            if (toolTypeStr == MessageStreamTools.BaseMessageStreamTool.LocalConfig.toolTypeStr)
                messageStreamTool = new MessageStreamTools.BaseMessageStreamTool(PartID, mstt.stream, this, bufferPool, message, messageNVS) { ServerInfoNVS = Config.ServerInfoNVS };
            else if (toolTypeStr == MessageStreamTools.ActionRelayMessageStreamToolConfig.toolTypeStr)
                messageStreamTool = new MessageStreamTools.ActionRelayMessageStreamTool(PartID, mstt.stream, this, bufferPool, message, messageNVS, Config.IPartsInterconnection);
            else if (toolTypeStr == MessageStreamTools.SetRelayMessageStreamToolConfigBase.toolTypeStr)
                messageStreamTool = new MessageStreamTools.SetRelayMessageStreamTool(PartID, mstt.stream, this, bufferPool, message, messageNVS, Config.ISetsInterconnection);
            else if (toolTypeStr == MessageStreamTools.IVIRelayMessageStreamToolConfig.toolTypeStr)
                messageStreamTool = new MessageStreamTools.IVIRelayMessageStreamTool(PartID, mstt.stream, this, bufferPool, message, messageNVS, Config.IIVIRegistration, Config.DefaultIVI);
            //else if (toolTypeStr == MessageStreamTools.ConfigProxyProviderMessageStreamToolConfig.toolName)
            //    messageStreamTool = new MessageStreamTools.ConfigProxyProviderMessageStreamTool(PartID, mstt.stream, this, bufferPool, message, messageNVS, Config.IConfig);

            if (messageStreamTool != null)
            {
                mstt.messageStreamTool = messageStreamTool;

                return string.Empty;
            }
            else
            {
                return "Unable to generate message stream tool: ToolTypeStr '{0}' invalid or not recognized [{1}]".CheckedFormat(toolTypeStr, messageNVS.SafeToStringSML());
            }
        }

        #region NoteWorkCount, WaitForSomethingToDo - adpative sleep times.

        protected void NoteWorkCount(int count)
        {
            if (count > 0)
                quickWaitCount = 3;
        }

        int quickWaitCount = 0;
        static readonly TimeSpan quickWaitTime = (0.001).FromSeconds();

        protected override bool WaitForSomethingToDo(IWaitable waitable, TimeSpan useWaitTimeLimit)
        {
            if (quickWaitCount > 0)
            {
                quickWaitCount--;
                useWaitTimeLimit = useWaitTimeLimit.Min(quickWaitTime);
            }

            return base.WaitForSomethingToDo(waitable, useWaitTimeLimit);
        }

        #endregion
    }

    public class RemotingClient : SimpleActivePartBase, IRemoting
    {
        public RemotingClient(RemotingClientConfig config)
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior: true, partBaseIVI: config.PartIVI, addGoOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.SupportServiceActions | GoOnlineAndGoOfflineHandling.SupportMappedServiceActions))
        {
            Config = config.MakeCopyOfThis();

            traceLogger = new Logging.Logger(PartID + ".Trace", groupName: Logging.LookupDistributionGroupName, initialInstanceLogGate: Config.ConfigNVS["TraceLogger.InitialInstanceLogGate"].VC.GetValue(rethrow: false, defaultValue: Logging.LogGate.Debug));

            if (Config.StreamToolsConfigArray.IsNullOrEmpty())
                throw new System.ArgumentOutOfRangeException("Config.StreamToolsConfigArray", "Must contain at least one StreamToolConfigBase derived instance");

            bufferPool = new BufferPool(PartID + ".bp", configNVS: config.ConfigNVS, bufferStateEmitter: traceLogger.Trace);

            AddMainThreadStoppingAction(() => Release(releaseBufferPool: true, releaseMSTTs: true));

            ActionLoggingReference.Config = new ActionLoggingConfig(ActionLoggingConfig.Debug_Debug_Trace_Trace, actionLoggingStyleSelect: ActionLoggingStyleSelect.IncludeRunTimeOnCompletion);

            _serverInfoNVSPublisher = new InterlockedNotificationRefObject<INamedValueSet>();
            serverInfoNVSIVA = (config.PartIVI ?? Interconnect.Values.Values.Instance).GetValueAccessor("{0}.ServerInfoNVS".CheckedFormat(PartID));

            EventAndPerformanceRecording = new EventAndPerformanceRecording(PartID, config.PartIVI);
        }

        public readonly string MyUUID = Guid.NewGuid().ToString();

        public INotificationObject<INamedValueSet> ServerInfoNVSPublisher { get { return _serverInfoNVSPublisher; } }
        private InterlockedNotificationRefObject<INamedValueSet> _serverInfoNVSPublisher;
        private IValueAccessor serverInfoNVSIVA;

        private void InnerHandleServerInfoNVS(INamedValueSet nvs)
        {
            _serverInfoNVSPublisher.Object = nvs.ConvertToReadOnly(mapNullToEmpty: true);
            serverInfoNVSIVA.Set(nvs.ConvertToReadOnly(mapNullToEmpty: false));
        }

        private Logging.IBasicLogger traceLogger;
        private RemotingClientConfig Config { get; set; }
        public INamedValueSet ConfigNVS { get { return Config.ConfigNVS; } }

        private BufferPool bufferPool;
        private Transport.ITransportConnection transport;
        private Sessions.ConnectionSession session;
        private Sessions.SessionConfig sessionConfig;
        private IEventAndPerformanceRecording EventAndPerformanceRecording { get; set; }

        private class MessageStreamToolTracker
        {
            public int stream;
            public MessageStreamTools.MessageStreamToolConfigBase messageStreamToolConfig;
            public MessageStreamTools.IMessageStreamTool messageStreamTool;
            public Messages.Message lastGeneratedMessage;

            public void Clear(QpcTimeStamp now, bool releaseMSTT = false)
            {
                if (messageStreamTool != null)
                {
                    messageStreamTool.ResetState(now, ResetType.ClientSessionClosed, "MSTT has been cleared");
                    if (releaseMSTT)
                        Fcns.DisposeOfObject(ref messageStreamTool);
                }

                lastGeneratedMessage = null;        // the message can only be safely "released" if it has been delivered.
            }
        }

        private MessageStreamToolTracker[] messageStreamToolTrackerArray = EmptyArrayFactory<MessageStreamToolTracker>.Instance;

        private MessageStreamTools.IActionRelayMessageStreamTool actionRelayStreamTool = null;

        private void Release(bool releaseBufferPool = false, bool releaseMSTTs = false, bool setConnStateToDisconnectedIfNeeded = true)
        {
            QpcTimeStamp qpcNow = QpcTimeStamp.Now;

            Fcns.DisposeOfObject(ref transport);
            Fcns.DisposeOfObject(ref session);

            messageStreamToolTrackerArray.DoForEach(mstt => mstt.Clear(qpcNow, releaseMSTT: releaseMSTTs));

            if (releaseMSTTs)
            {
                messageStreamToolTrackerArray = EmptyArrayFactory<MessageStreamToolTracker>.Instance;
                actionRelayStreamTool = null;
            }

            if (releaseBufferPool)
                Fcns.DisposeOfObject(ref bufferPool);

            if (BaseState.ConnState.IsConnectedOrConnecting() && setConnStateToDisconnectedIfNeeded)
                SetBaseState(ConnState.Disconnected, CurrentMethodName);
        }

        protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
        {
            string description = ipf.ToString(ToStringSelect.MesgAndDetail);
            var entryBaseState = BaseState;

            if (andInitialize && !npv.IsNullOrEmpty())
            {
                Log.Debug.Emit("{0}: Processing ConfigNVS updates from NamedParamValues:{1}", ipf.ToString(ToStringSelect.MesgAndDetail), npv.ToStringSML());

                // update and save the ConfigNVS as read only.  Normally the npv null or empty.  This allows the GoOnline client to update information like the transport type and the transport configuration on each GoOnline call
                Config.ConfigNVS = Config.ConfigNVS.MergeWith(npv, mergeBehavior: NamedValueMergeBehavior.AddAndUpdate).ConvertToReadOnly();
            }

            TimeSpan maxSessionConnectWaitTime = Config.ConfigNVS["MaxSessionConnectWaitTime"].VC.GetValue(rethrow: false, defaultValue: (5.0).FromSeconds());
            TimeSpan maxSessionCloseWaitTime = Config.ConfigNVS["MaxSessionCloseWaitTime"].VC.GetValue(rethrow: false, defaultValue: (1.0).FromSeconds());

            string ec = InnerPerformGoOnlineAction(andInitialize, CurrentActionDescription, maxSessionConnectWaitTime, maxSessionCloseWaitTime, forAutoReconnect: false);

            if (!ec.IsNullOrEmpty() && session != null)
            {
                switch (session.State.TerminationReasonCode)
                {
                    case Sessions.TerminationReasonCode.BufferSizesDoNotMatch: 
                        SetBaseState(UseState.FailedToOffline, "Connection rejected [{0}, {1}]".CheckedFormat(session.State.TerminationReasonCode, session.State.Reason)); 
                        return ec;
                    default:
                        break;
                }

                Release(setConnStateToDisconnectedIfNeeded: false);
            }

            return ec;
        }

        private string InnerPerformGoOnlineAction(bool andInitialize, string currentActionDescription, TimeSpan maxSessionConnectWaitTime, TimeSpan maxSessionCloseWaitTime, bool forAutoReconnect = false)
        {
            string ec = string.Empty;

            try
            {
                if (andInitialize && BaseState.IsOnlineOrAttemptOnline && !forAutoReconnect)
                    ec = InnerPerformGoOfflineAction(currentActionDescription, maxSessionCloseWaitTime, forAutoReconnect: forAutoReconnect);        // this does a Release...
                else
                    Release(setConnStateToDisconnectedIfNeeded: false);

                {
                    Fcns.DisposeOfObject(ref session);
                    session = new Sessions.ConnectionSession(PartID, MyUUID, MyUUID, null, Config.ConfigNVS, hostNotifier: this, bufferPool: bufferPool, initialSessionStateCode: Sessions.SessionStateCode.ClientSessionInitial, issueEmitter: Log.Debug, stateEmitter: Log.Debug, traceEmitter: traceLogger.Trace, ivi: settings.simplePartBaseSettings.PartBaseIVI)
                        {
                            EventAndPerformanceRecording = EventAndPerformanceRecording,
                            HandleInboundMessageDelegate = HandleInboundMessage,
                        };

                    sessionConfig = session.Config;

                    Fcns.DisposeOfObject(ref transport);
                    transport = Transport.TransportConnectionFactory.Instance.CreateClientConnection(Config.ConfigNVS, session);

                    session.TransportTypeFeatures = transport.TransportTypeFeatures;
                }

                if (messageStreamToolTrackerArray.IsNullOrEmpty())
                {
                    actionRelayStreamTool = null;
                    var adjustedStreamToolConfigArray = new MessageStreamTools.BaseMessageStreamTool.LocalConfig().Concat<MessageStreamToolConfigBase>(Config.StreamToolsConfigArray).ToArray();
                    messageStreamToolTrackerArray = adjustedStreamToolConfigArray.Select((stc, index) => new MessageStreamToolTracker() { stream = index, messageStreamToolConfig = stc, messageStreamTool = CreateStreamTool(index, stc) }).ToArray();
                }

                session.SetState(QpcTimeStamp.Now, Sessions.SessionStateCode.RequestTransportConnect, currentActionDescription);

                QpcTimer waitTimer = new QpcTimer() { TriggerInterval = maxSessionConnectWaitTime }.Start();

                while (ec.IsNullOrEmpty())
                {
                    WaitForSomethingToDo();

                    InnerServiceBackground(serviceConnStateUpdates: !forAutoReconnect);

                    if (session.State.IsConnected())
                        break;

                    if (session.State.IsPerminantlyClosed)
                        ec = "Session connection attempt failed: [{0}]".CheckedFormat(session.State);

                    if (waitTimer.IsTriggered)
                        ec = "Session connection not complete within {0:f1} seconds [{1}]".CheckedFormat(waitTimer.ElapsedTimeInSeconds, session.State);
                }

                if (ec.IsNullOrEmpty())
                    SetBaseState(ConnState.Connected, "{0} succeeded".CheckedFormat(currentActionDescription));
                else
                    SetBaseState(ConnState.ConnectFailed, "{0} failed: {1}".CheckedFormat(currentActionDescription, ec));

                return ec;
            }
            catch (System.Exception ex)
            {
                string exStr = "Generated unexpected exception: {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                Log.Debug.Emit("{0}: {1}", CurrentMethodName, exStr);

                Release();

                return ec.MapNullOrEmptyTo(exStr);
            }
        }

        protected override string PerformGoOfflineAction()
        {
            TimeSpan maxSessionCloseWaitTime = Config.ConfigNVS["MaxSessionCloseWaitTime"].VC.GetValue(rethrow: false, defaultValue: (1.0).FromSeconds());

            string ec = InnerPerformGoOfflineAction(CurrentActionDescription, maxSessionCloseWaitTime);
            return ec;
        }

        private string InnerPerformGoOfflineAction(string actionDescription, TimeSpan maxSessionCloseWaitTime, bool forAutoReconnect = false)
        {
            string ec = string.Empty;

            if (session != null)
            {
                if (session.State.IsConnected(includeConnectingStates: true, includeClosingStates: true))
                    session.SetState(QpcTimeStamp.Now, Sessions.SessionStateCode.CloseRequested, "Client {0}".CheckedFormat(actionDescription));
                else if (session.State.StateCode == Sessions.SessionStateCode.ConnectionClosed)
                    session.SetState(QpcTimeStamp.Now, Sessions.SessionStateCode.Terminated, "Client {0} [{1}]".CheckedFormat(actionDescription, session.State.StateCode));

                session.GenerateAndAddManagementBufferToSendNowList(QpcTimeStamp.Now, ManagementType.RequestCloseSession, reason: actionDescription);

                QpcTimer waitTimeLimitTimer = new QpcTimer() { TriggerInterval = maxSessionCloseWaitTime }.Start();

                InnerServiceBackground(serviceConnStateUpdates: !forAutoReconnect);

                while (session.State.StateCode != Sessions.SessionStateCode.Terminated)
                {
                    WaitForSomethingToDo();
                    InnerServiceBackground(serviceConnStateUpdates: !forAutoReconnect);

                    if (waitTimeLimitTimer.IsTriggered)
                    {
                        ec = "Session close time limit reached after {0:f3} seconds [{1}]".CheckedFormat(waitTimeLimitTimer.ElapsedTimeInSeconds, session.State);
                        break;
                    }
                }

                {
                    QpcTimeStamp now = QpcTimeStamp.Now;

                    if (ec.IsNullOrEmpty())
                        messageStreamToolTrackerArray.DoForEach(mss => mss.messageStreamTool.ResetState(now, MessageStreamTools.ResetType.ClientSessionClosed));
                    else
                        messageStreamToolTrackerArray.DoForEach(mss => mss.messageStreamTool.ResetState(now, MessageStreamTools.ResetType.ClientSessionLost, "{0} failed: {1}".CheckedFormat(actionDescription, ec)));
                }
            }

            Release();

            if (!ec.IsNullOrEmpty())
                Log.Debug.Emit("{0} failed: {1}", actionDescription, ec);

            return ec;
        }

        protected override string PerformServiceActionEx(IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            StringScanner ss = new StringScanner(serviceName);

            if (ss.MatchToken("Remote"))
            {
                if (actionRelayStreamTool != null)
                {
                    actionRelayStreamTool.RelayServiceAction(ss.Rest, ipf);
                    InnerServiceBackground();
                    return null;
                }
                else
                {
                    return "Cannot relay this action because client has not been configured with an Action Relay Stream Tool";
                }
            }
            else if (ss.MatchToken("Sync"))
            {
                SyncFlags syncFlags = npv["syncFlags"].VC.GetValue<SyncFlags>(rethrow: false);

                if (ss.IsAtEnd || ss.ParseValue<SyncFlags>(out syncFlags))
                    return PerformSync(syncFlags);
                else
                    return "Invalid or unsupported Sync request format [{0}, npv:{1}]".CheckedFormat(serviceName, npv.SafeToStringSML());
            }
            else if (ss.MatchToken("FaultInjection"))
            {
                switch (ss.ExtractToken())
                {
                    case "GoToOnlineFailure":
                        Release();
                        SetBaseState(UseState.OnlineFailure, "By explicit request [{0}]".CheckedFormat(serviceName));
                        return "";

                    case "ManuallyTriggerAutoReconnectAttempt":
                        manuallyTriggerAutoReconnectAttempt = true;

                        if (BaseState.IsConnectedOrConnecting)
                            SetBaseState(ConnState.Disconnected, serviceName);

                        return "";

                    default:
                        return base.PerformServiceActionEx(ipf, serviceName, npv);      // we expect this to fail with the standard error message...
                }
            }
            else
            {
                return base.PerformServiceActionEx(ipf, serviceName, npv);
            }
        }

        public IClientFacet Sync(SyncFlags syncFlags = default(SyncFlags)) 
        {
            BasicActionImpl action = new BasicActionImpl(actionQ, PerformSync, CurrentMethodName, ActionLoggingReference);
            action.NamedParamValues = new NamedValueSet() { { "syncFlags", syncFlags } };
            return action;
        }

        private string PerformSync(IProviderFacet action)
        {
            SyncFlags syncFlags = action.NamedParamValues["syncFlags"].VC.GetValue<SyncFlags>(rethrow: true);

            return PerformSync(syncFlags);
        }

        private string PerformSync(SyncFlags syncFlags)
        {
            if (!BaseState.IsOnline)
                return "Part is not online: {0}".CheckedFormat(BaseState);

            InnerServiceBackground();

            return string.Empty;
        }

        private bool manuallyTriggerAutoReconnectAttempt = false;

        protected override void PerformMainLoopService()
        {
            QpcTimeStamp qpcNow = QpcTimeStamp.Now;

            InnerServiceBackground(qpcNow);

            if (BaseState.IsOnlineOrAttemptOnline && BaseState.UseState != UseState.OnlineFailure && (session == null || session.State.IsPerminantlyClosed))
            {
                Sessions.SessionState sessionState = (session != null ? session.State : default(Sessions.SessionState));

                string reason = "Session was {0}".CheckedFormat(sessionState);

                SetBaseState(useState: UseState.OnlineFailure, reason: reason);

                messageStreamToolTrackerArray.DoForEach(mstt => mstt.messageStreamTool.ResetState(qpcNow, MessageStreamTools.ResetType.ClientSessionLost, reason));
            }

            bool triggerAutoReconnectAttempt = (BaseState.UseState == UseState.OnlineFailure || BaseState.UseState == UseState.AttemptOnlineFailed)
                                             && ((sessionConfig != null && sessionConfig.AutoReconnectHoldoff != null && (qpcNow - BaseState.TimeStamp) > sessionConfig.AutoReconnectHoldoff)
                                                || manuallyTriggerAutoReconnectAttempt);
            if (triggerAutoReconnectAttempt)
            {
                TimeSpan maxSessionAutoReconnectWaitTime = Config.ConfigNVS["MaxSessionAutoReconnectWaitTime"].VC.GetValue(rethrow: false, defaultValue: (1.0).FromSeconds());
                TimeSpan maxSessionAutoReconnectCloseWaitTime = Config.ConfigNVS["MaxSessionAutoReconnectCloseWaitTime"].VC.GetValue(rethrow: false, defaultValue: (1.0).FromSeconds());

                string ec = InnerPerformGoOnlineAction(false, "Client AutoReconnect Attempt", maxSessionAutoReconnectWaitTime, maxSessionAutoReconnectCloseWaitTime, forAutoReconnect: true);

                if (ec.IsNullOrEmpty())
                {
                    SetBaseState(UseState.Online, "Client AutoReconnect complete");
                    PublishActionInfo(ActionInfo.EmptyActionInfo);
                }
                else 
                {
                    var terminationReasonCode = (session != null) ? session.State.TerminationReasonCode : default(Sessions.TerminationReasonCode);

                    switch (terminationReasonCode)
                    {
                        case Sessions.TerminationReasonCode.BufferSizesDoNotMatch: 
                            SetBaseState(UseState.FailedToOffline, "Connection rejected [{0}, {1}]".CheckedFormat(session.State.TerminationReasonCode, session.State.Reason));
                            break;
                        default:
                            if (BaseState.UseState != UseState.AttemptOnlineFailed)
                                SetBaseState(UseState.AttemptOnlineFailed, "Client AutoReconnect failed: {0}".CheckedFormat(ec));
                            break;
                    }

                    Release(setConnStateToDisconnectedIfNeeded: false);
                }

                InnerServiceBackground();
            }
        }

        private void InnerServiceBackground(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool serviceConnStateUpdates = true)
        {
            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

            int count = 0;
            if (transport != null)
                count += transport.Service(qpcTimeStamp);

            if (session != null)
                count += session.Service(qpcTimeStamp);

            count += messageStreamToolTrackerArray.Sum(mstt => InnerServiceStream(qpcTimeStamp, mstt));

            NoteWorkCount(count);

            EventAndPerformanceRecording.Service(qpcTimeStamp);

            if (serviceConnStateUpdates)
            {
                ConnState nextConnState = BaseState.ConnState;
                string nextConnStateReason = null;
                bool updateBaseStateReason = true;

                switch (BaseState.UseState)
                {
                    case UseState.Offline:
                    case UseState.FailedToOffline:
                    case UseState.Shutdown:
                    case UseState.Stopped:
                    case UseState.MainThreadFailed:
                    case UseState.Initial:
                    default:
                        nextConnState = ConnState.Disconnected;
                        nextConnStateReason = "Part UseState is {0}".CheckedFormat(BaseState.UseState);
                        updateBaseStateReason = false;
                        break;
                    case UseState.Online:
                    case UseState.OnlineBusy:
                    case UseState.OnlineUninitialized:
                    case UseState.OnlineFailure:
                    case UseState.AttemptOnlineFailed:
                        if (session != null)
                        {
                            Sessions.SessionState sessionState = session.State;
                            var sessionStateAge = sessionState.TimeStamp.Age(qpcTimeStamp);
                            var lastDeliveredKeepAliveBufferAge = qpcTimeStamp - session.lastDeliveredKeepAliveBufferTimeStamp;

                            switch (sessionState.StateCode)
                            {
                                case Sessions.SessionStateCode.ClientSessionInitial:
                                    break;

                                case Sessions.SessionStateCode.RequestTransportConnect:
                                case Sessions.SessionStateCode.RequestSessionOpen:
                                    nextConnState = ConnState.Connecting;
                                    nextConnStateReason = "Session is {0}".CheckedFormat(sessionState.StateCode);
                                    break;

                                case Sessions.SessionStateCode.Active:
                                    nextConnState = ConnState.Connected;
                                    nextConnStateReason = "Session is {0}".CheckedFormat(sessionState.StateCode);
                                    break;

                                case Sessions.SessionStateCode.Idle:
                                    if (sessionStateAge < sessionConfig.ConnectionDegradedHoldoff || sessionConfig.ConnectionDegradedHoldoff.IsZero())
                                    {
                                        nextConnState = ConnState.Connected;
                                        nextConnStateReason = "Session is {0}".CheckedFormat(sessionState.StateCode);
                                    }
                                    else if (!sessionConfig.NominalKeepAliveSendInterval.IsZero() && lastDeliveredKeepAliveBufferAge >= sessionConfig.NominalKeepAliveSendInterval && lastDeliveredKeepAliveBufferAge > sessionConfig.ConnectionDegradedHoldoff)
                                    {
                                        nextConnState = ConnState.ConnectionDegraded;
                                        nextConnStateReason = "Session is {0} (keepalive is degraded)".CheckedFormat(sessionState.StateCode);
                                    }
                                    else
                                    {
                                        nextConnState = ConnState.Connected;
                                        nextConnStateReason = "Session is {0}".CheckedFormat(sessionState.StateCode);
                                    }
                                    break;

                                case Sessions.SessionStateCode.IdleWithPendingWork:
                                    if (sessionStateAge >= sessionConfig.ConnectionDegradedHoldoff && !sessionConfig.ConnectionDegradedHoldoff.IsZero())
                                    {
                                        nextConnState = ConnState.ConnectionDegraded;
                                        nextConnStateReason = "Session is {0}".CheckedFormat(sessionState.StateCode);
                                    }
                                    break;

                                case Sessions.SessionStateCode.CloseRequested:
                                case Sessions.SessionStateCode.ConnectionClosed:
                                    nextConnState = ConnState.Disconnected;
                                    nextConnStateReason = "Session is {0}".CheckedFormat(sessionState.StateCode);
                                    break;

                                case Sessions.SessionStateCode.Terminated:
                                    nextConnState = ConnState.ConnectionFailed;
                                    nextConnStateReason = "Session is {0}".CheckedFormat(sessionState);
                                    break;

                                default:
                                    nextConnState = ConnState.ConnectionFailed;
                                    nextConnStateReason = "Session is {0}".CheckedFormat(sessionState.StateCode);
                                    break;
                            }
                        }
                        else
                        {
                            nextConnState = ConnState.ConnectionFailed;
                            nextConnStateReason = "Part UseState is {0} with no defined session".CheckedFormat(BaseState.UseState);
                            updateBaseStateReason = false;
                        }

                        break;
                }

                if ((nextConnState != BaseState.ConnState && nextConnStateReason != null) || (updateBaseStateReason && nextConnStateReason != BaseState.Reason))
                {
                    SetBaseState(nextConnState, nextConnStateReason, updateBaseStateReason: updateBaseStateReason);
                }
            }
        }

        private MessageStreamTools.IMessageStreamTool CreateStreamTool(int stream, MessageStreamTools.MessageStreamToolConfigBase messageStreamToolConfig)
        {
            MessageStreamTools.BaseMessageStreamTool.LocalConfig baseMSTConfig = messageStreamToolConfig as MessageStreamTools.BaseMessageStreamTool.LocalConfig;

            if (baseMSTConfig != null)
            {
                var tool = new MessageStreamTools.BaseMessageStreamTool(PartID, stream, this, bufferPool) { HandleNewServerInfoNVSDelegate = InnerHandleServerInfoNVS };

                return tool;
            }

            MessageStreamTools.ActionRelayMessageStreamToolConfig actionRelayConfig = messageStreamToolConfig as MessageStreamTools.ActionRelayMessageStreamToolConfig;

            if (actionRelayConfig != null)
            {
                var tool = new MessageStreamTools.ActionRelayMessageStreamTool(PartID, stream, this, bufferPool, actionRelayConfig);

                if (actionRelayStreamTool == null)
                    actionRelayStreamTool = tool;

                return tool;
            }

            MessageStreamTools.IVIRelayMessageStreamToolConfig iviRelayConfig = messageStreamToolConfig as MessageStreamTools.IVIRelayMessageStreamToolConfig;
            if (iviRelayConfig != null)
                return new MessageStreamTools.IVIRelayMessageStreamTool(PartID, stream, this, bufferPool, iviRelayConfig);

            MessageStreamTools.SetRelayMessageStreamToolConfigBase setRelayConfig = messageStreamToolConfig as MessageStreamTools.SetRelayMessageStreamToolConfigBase;
            if (setRelayConfig != null)
                return new MessageStreamTools.SetRelayMessageStreamTool(PartID, stream, this, bufferPool, setRelayConfig);

            //MessageStreamTools.ConfigProxyProviderMessageStreamToolConfig configProxyConfig = messageStreamToolConfig as MessageStreamTools.ConfigProxyProviderMessageStreamToolConfig;
            //if (configProxyConfig != null)
            //{ }

            Log.Error.Emit("Cannot create message stream tool for stream {0}: '{1}' is not not recognized or supported by this part", stream, messageStreamToolConfig);

            return null;
        }

        private int InnerServiceStream(QpcTimeStamp qpcTimeStamp, MessageStreamToolTracker stt)
        {
            int count = 0;

            if (session != null && session.State.IsConnected())
            {
                if (stt.lastGeneratedMessage != null)
                {
                    if (stt.lastGeneratedMessage.State == Messages.MessageState.Delivered)
                    {
                        stt.lastGeneratedMessage.ReturnBuffersToPool(qpcTimeStamp);
                        stt.lastGeneratedMessage = null;
                        count++;
                    }
                    else if (stt.lastGeneratedMessage.State == Messages.MessageState.Failed)
                    {
                        string reason = stt.lastGeneratedMessage.Reason;

                        Log.Debug.Emit("Message send for stream {0} failed [{1}, reason:{2}]", stt.stream, stt.messageStreamToolConfig.GetType().GetTypeDigestName(), reason);

                        // do not return message to buffer pool as its buffers are not in a known state anymore.
                        stt.lastGeneratedMessage = null;

                        stt.messageStreamTool.ResetState(qpcTimeStamp, MessageStreamTools.ResetType.ClientMessageDeliveryFailure, reason);

                        count++;
                    }
                }

                if (stt.lastGeneratedMessage == null)
                {
                    var nextMesg = stt.messageStreamTool.ServiceAndGenerateNextMessageToSend(qpcTimeStamp);
                    if (nextMesg != null)
                    {
                        session.HandleOutboundMessage(qpcTimeStamp, unchecked((ushort)stt.stream), nextMesg);
                        stt.lastGeneratedMessage = nextMesg;
                        count++;
                    }
                }
                else
                {
                    count += stt.messageStreamTool.Service(qpcTimeStamp);
                }
            }

            return count;
        }

        private void HandleInboundMessage(QpcTimeStamp qpcTimeStamp, ushort stream, Messages.Message message)
        {
            MessageStreamToolTracker stt = messageStreamToolTrackerArray.SafeAccess(stream);

            if (stt != null && stt.messageStreamTool != null)
                stt.messageStreamTool.HandleInboundMessage(qpcTimeStamp, message);

            message.ReturnBuffersToPool(qpcTimeStamp);
        }

        #region NoteWorkCount, WaitForSomethingToDo - adpative sleep times.

        protected void NoteWorkCount(int count)
        {
            if (count > 0)
                quickWaitCount = 3;
        }

        int quickWaitCount = 0;
        static readonly TimeSpan quickWaitTime = (0.001).FromSeconds();

        protected override bool WaitForSomethingToDo(IWaitable waitable, TimeSpan useWaitTimeLimit)
        {
            if (quickWaitCount > 0)
            {
                quickWaitCount--;
                useWaitTimeLimit = useWaitTimeLimit.Min(quickWaitTime);
            }
 
            return base.WaitForSomethingToDo(waitable, useWaitTimeLimit);
        }

        #endregion
    }

    public interface IEventAndPerformanceRecording : IServiceable
    {
        void RecordReceived(params Buffers.Buffer[] bufferParamsArray);
        void RecordReceived(Messages.Message message);
        void RecordReceived(ManagementType managementType);

        void RecordSent(params Buffers.Buffer [] bufferParamsArray);
        void RecordSending(ManagementType managementType);
        void RecordDelivered(Messages.Message message);
        void RecordDelivered(Buffers.Buffer buffer);

        void RecordEvent(RecordEventType eventType, int count = 1);
    }

    public enum RecordEventType : int
    {
        None = 0,
        UnexpectedNonManagementBuffersGivenToSessionManager,
        BufferReceivedOutOfOrder,
        OldBufferReceivedOutOfOrder,
        OldResentBufferRecieved,
        TransportException,
        TransportExceptionClosedSession,
    }

    public class EventAndPerformanceRecording : IEventAndPerformanceRecording
    {
        public EventAndPerformanceRecording(string hostPartID, IValuesInterconnection ivi)
        {
            IVI = ivi ?? Values.Values.Instance;

            lastSendSampleIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".Send.LastSample").Set(NamedValueSet.Empty);
            recentSendRatesIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".SendRates.Recent").Set(NamedValueSet.Empty);
            avgSendRatesIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".SendRates.Avg").Set(NamedValueSet.Empty);
            totalSentIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".Sent.Total").Set(NamedValueSet.Empty);

            lastReceiveSampleIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".Receive.LastSample").Set(NamedValueSet.Empty);
            recentReceiveRatesIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".ReceiveRates.Recent").Set(NamedValueSet.Empty);
            avgReceiveRatesIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".ReceiveRates.Avg").Set(NamedValueSet.Empty);
            totalReceivedIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".Received.Total").Set(NamedValueSet.Empty);

            counterValuesIVA = IVI.GetValueAccessor<INamedValueSet>(hostPartID + ".Counters").Set(NamedValueSet.Empty);

            rateCounterSetNVSA = new NamedValueSetAdapter<RateCounterSet>() { ItemAccess = Reflection.Attributes.ItemAccess.GetOnly }.Setup();
            counterValuesNVSA = new NamedValueSetAdapter<CounterValues>() { ItemAccess = Reflection.Attributes.ItemAccess.GetOnly }.Setup();

            ivaArray = new[] { lastSendSampleIVA, recentSendRatesIVA, avgSendRatesIVA, totalSentIVA, lastReceiveSampleIVA, recentReceiveRatesIVA, avgReceiveRatesIVA, totalReceivedIVA, counterValuesIVA };
        }

        IValuesInterconnection IVI { get; set; }

        const int shortHistoryLength = 5;  // 5 seconds
        const int fullHistoryLength = 30;   // 30 seconds
        MovingAverageTool<RateCounterSet> avgSendRateCounterSetTool = new MovingAverageTool<RateCounterSet>(nominalUpdateInterval: (1.0).FromSeconds(), maxAveragingHistoryLength: fullHistoryLength) { AutoService = false };
        MovingAverageTool<RateCounterSet> avgReceiveRateCounterSetTool = new MovingAverageTool<RateCounterSet>(nominalUpdateInterval: (1.0).FromSeconds(), maxAveragingHistoryLength: fullHistoryLength) { AutoService = false };

        IValueAccessor<INamedValueSet> lastSendSampleIVA, recentSendRatesIVA, avgSendRatesIVA, totalSentIVA;
        IValueAccessor<INamedValueSet> lastReceiveSampleIVA, recentReceiveRatesIVA, avgReceiveRatesIVA, totalReceivedIVA;
        NamedValueSetAdapter<RateCounterSet> rateCounterSetNVSA;
        
        CounterValues counterValues = new CounterValues();

        IValueAccessor<INamedValueSet> counterValuesIVA;
        NamedValueSetAdapter<CounterValues> counterValuesNVSA;

        IValueAccessor[] ivaArray;

        public class CounterValues
        {
            [NamedValueSetItem]
            public ulong ResentBuffersTx;

            [NamedValueSetItem]
            public ulong ResentBuffersRx;

            [NamedValueSetItem]
            public ulong BuffersRcvdOutOfOrder;

            [NamedValueSetItem]
            public ulong KeepAliveBuffersTx;

            [NamedValueSetItem]
            public ulong StatusBuffersTx;

            [NamedValueSetItem]
            public ulong OtherMgmtBuffersTx;

            [NamedValueSetItem]
            public ulong OtherUnexpected;

            // [NamedValueSetItem]
            public ulong TransportExceptions;

            // [NamedValueSetItem]
            public ulong TransportExceptionClosedSessions;

            // [NamedValueSetItem]
            public ulong NonMgmtBuffersToSessionMgr;
        }

        public int Service(QpcTimeStamp qpcTimeStamp)
        {
            avgSendRateCounterSetTool.Service(qpcTimeStamp);

            int count = 0;

            if (avgSendRateCounterSetTool.HasNewAvg)
            {
                count++;

                avgReceiveRateCounterSetTool.AddRecordedValuesToHistory(qpcTimeStamp);

                lastSendSampleIVA.Value = rateCounterSetNVSA.Get(avgSendRateCounterSetTool.GetAvg(1), asReadOnly: true);
                recentSendRatesIVA.Value = rateCounterSetNVSA.Get(avgSendRateCounterSetTool.GetAvg(shortHistoryLength), asReadOnly: true);
                avgSendRatesIVA.Value = rateCounterSetNVSA.Get(avgSendRateCounterSetTool.GetAvg(), asReadOnly: true);
                totalSentIVA.Value = rateCounterSetNVSA.Get(avgSendRateCounterSetTool.Totalizer.ComputeAverage(1), asReadOnly: true);

                lastReceiveSampleIVA.Value = rateCounterSetNVSA.Get(avgReceiveRateCounterSetTool.GetAvg(1), asReadOnly: true);
                recentReceiveRatesIVA.Value = rateCounterSetNVSA.Get(avgReceiveRateCounterSetTool.GetAvg(shortHistoryLength), asReadOnly: true);
                avgReceiveRatesIVA.Value = rateCounterSetNVSA.Get(avgReceiveRateCounterSetTool.GetAvg(), asReadOnly: true);
                totalReceivedIVA.Value = rateCounterSetNVSA.Get(avgReceiveRateCounterSetTool.Totalizer.ComputeAverage(1), asReadOnly: true);

                counterValuesIVA.Value = counterValuesNVSA.Get(counterValues, asReadOnly: true);

                IVI.Set(ivaArray);
            }

            return count;
        }

        public void RecordReceived(params Buffers.Buffer[] bufferParamsArray)
        {
            RateCounterSet tcs = new RateCounterSet();

            foreach (var buffer in bufferParamsArray)
            {
                if (buffer == null)
                    continue;

                tcs.Buffers++;
                tcs.Bytes += buffer.byteCount;

                if (buffer.PurposeCode == PurposeCode.Ack)
                    tcs.Acks++;

                if ((buffer.Flags & BufferHeaderFlags.BufferIsBeingResent) != 0)
                    counterValues.ResentBuffersRx++;
            }

            avgReceiveRateCounterSetTool.RecordValues(tcs);
        }

        public void RecordReceived(Messages.Message message)
        {
            avgReceiveRateCounterSetTool.RecordValues(new RateCounterSet() { Messages = 1 });
        }

        public void RecordReceived(ManagementType managementType)
        {
            //switch (managementType)
            //{
            //    case ManagementType.KeepAlive: counterValues.KeepAliveBuffersTx++; break;
            //    case ManagementType.Status: counterValues.StatusBuffersTx++; break;
            //    default: counterValues.OtherMgmtBuffersRx++; break;
            //}
        }

        public void RecordSent(params Buffers.Buffer[] bufferParamsArray)
        {
            RateCounterSet tcs = new RateCounterSet();

            foreach (var buffer in bufferParamsArray)
            {
                if (buffer == null)
                    continue;

                if (buffer.SeqNum == 0)     // only record buffers with no sequence number in the outbound byte and buffer counts.  Wait for delivery for the rest.
                {
                    tcs.Buffers++;
                    tcs.Bytes += buffer.byteCount;
                }

                if (buffer.PurposeCode == PurposeCode.Ack)
                    tcs.Acks++;

                if ((buffer.Flags & BufferHeaderFlags.BufferIsBeingResent) != 0)
                    counterValues.ResentBuffersTx++;
            }

            avgSendRateCounterSetTool.RecordValues(tcs);
        }

        public void RecordSending(ManagementType managementType)
        {
            switch (managementType)
            {
                case ManagementType.KeepAlive: counterValues.KeepAliveBuffersTx++; break;
                case ManagementType.Status: counterValues.StatusBuffersTx++; break;
                default: counterValues.OtherMgmtBuffersTx++; break;
            }
        }

        public void RecordDelivered(Messages.Message message)
        {
            avgSendRateCounterSetTool.RecordValues(new RateCounterSet() { Messages = 1, MesgDelay = message.SendPostedTimeStamp.Age.TotalSeconds });
        }

        public void RecordDelivered(Buffers.Buffer buffer)
        {
            avgSendRateCounterSetTool.RecordValues(new RateCounterSet() { Buffers = 1, Bytes = buffer.byteCount, BufDelay = buffer.SendPostedTimeStamp.Age.TotalSeconds });
        }

        public void RecordEvent(RecordEventType eventType, int count = 1)
        {
            switch (eventType)
            {
                case RecordEventType.UnexpectedNonManagementBuffersGivenToSessionManager: counterValues.OtherUnexpected++; counterValues.NonMgmtBuffersToSessionMgr++; break;
                case RecordEventType.BufferReceivedOutOfOrder: counterValues.BuffersRcvdOutOfOrder++; break;
                case RecordEventType.OldBufferReceivedOutOfOrder: counterValues.OtherUnexpected++; break;
                case RecordEventType.OldResentBufferRecieved: counterValues.ResentBuffersRx++; break;
                case RecordEventType.TransportException: counterValues.OtherUnexpected++; counterValues.TransportExceptions++; break;
                case RecordEventType.TransportExceptionClosedSession: counterValues.OtherUnexpected++; counterValues.TransportExceptionClosedSessions++; break;
                default: break;
            }
        }
    }

    public class RateCounterSet : IRequiredMovingAverageValuesOperations<RateCounterSet>
    {
        [NamedValueSetItem]
        public double Bytes;

        [NamedValueSetItem]
        public double Buffers;

        [NamedValueSetItem]
        public double Messages;

        [NamedValueSetItem]
        public double Acks;

        [NamedValueSetItem]
        public double BufDelay;

        [NamedValueSetItem]
        public double MesgDelay;

        [NamedValueSetItem]
        public int Samples;

        /// <summary>
        /// The moving average tool calls
        /// </summary>
        public RateCounterSet Add(RateCounterSet other)
        {
            Bytes += other.Bytes;
            Buffers += other.Buffers;
            Messages += other.Messages;
            Acks += other.Acks;
            BufDelay += other.BufDelay;
            MesgDelay += other.MesgDelay;
            Samples += Math.Max(1, other.Samples);

            return this;
        }

        public RateCounterSet ComputeAverage(int sampleCount)
        {
            var oneOverSampleCount = ((double)sampleCount).SafeOneOver();

            // BufDelay and MesgDelay are divided by 
            double entryBuffers = Buffers;
            double entryMessages = Messages;

            Bytes = Math.Round(Bytes * oneOverSampleCount, 0);
            Buffers = Math.Round(Buffers * oneOverSampleCount, 2);
            Messages = Math.Round(Messages * oneOverSampleCount, 2);
            Acks = Math.Round(Acks * oneOverSampleCount, 2);

            BufDelay = Math.Round(BufDelay * entryBuffers.SafeOneOver(), 3);
            MesgDelay = Math.Round(MesgDelay * entryMessages.SafeOneOver(), 3);

            return this;
        }

        public RateCounterSet MakeCopyOfThis(bool deepCopy = true)
        {
            return (RateCounterSet) this.MemberwiseClone();
        }
    }
}

//-------------------------------------------------------------------
