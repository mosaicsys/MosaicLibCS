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
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Parts;
using MosaicLib.Modular.Interconnect.Remoting.Buffers;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Modular.Interconnect.Remoting.MessageStreamTools;

//!!!!! Please NOTE: There are significant functional changes planed for the internal design and operation of the Remoting infrastructure.
// As such it is not currently recommended to make use of any capabilities beyond the top level publically available ones and these public interfaces
// are currently more likely than not to change as part of this planed capability extension for future versions of the this library.

// Please NOTE: All of the code portions in the following namespace (and the namespaces under it) are currently very early in their development cycle.  
// They are passing basic unit tests but they have not been fully tested at this point.
// In addition each related usage interface (API) is in a early stage and may be modified somewhat in subsequent preview releases.

namespace MosaicLib.Modular.Interconnect.Remoting
{
    public interface IRemoting
    {
        IClientFacet Sync(SyncFlags syncFlags = default(SyncFlags));

        INotificationObject<INamedValueSet> ServerInfoNVSPublisher { get; }
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
                PartIVI = PartIVI,
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
                PartIVI = PartIVI,
            };
        }
    }

    public class RemotingServer : SimpleActivePartBase, IRemoting
    {
        public RemotingServer(RemotingServerConfig config)
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion1.Build(automaticallyIncAndDecBusyCountAroundActionInvoke: false, partBaseIVI: config.PartIVI))
        {
            Config = config.MakeCopyOfThis();
            traceLogger = new Logging.Logger(PartID + ".Trace", groupName: Logging.LookupDistributionGroupName, initialInstanceLogGate: Config.ConfigNVS["TraceLogger.InitialInstanceLogGate"].VC.GetValue(rethrow: false, defaultValue: Logging.LogGate.Debug));

            bufferPool = new BufferPool(configNVS: Config.ConfigNVS, bufferStateEmitter: traceLogger.Trace);

            AddMainThreadStoppingAction(Release);

            ActionLoggingReference.Config = ActionLoggingConfig.Debug_Debug_Trace_Trace;

            _serverInfoNVSPublisher = new InterlockedNotificationRefObject<INamedValueSet>(Config.ServerInfoNVS);
        }

        public INotificationObject<INamedValueSet> ServerInfoNVSPublisher { get { return _serverInfoNVSPublisher; } }
        private InterlockedNotificationRefObject<INamedValueSet> _serverInfoNVSPublisher;

        private RemotingServerConfig Config { get; set; }

        private Logging.IBasicLogger traceLogger;

        private Transport.ITransportConnection transport;
        private Sessions.SessionManager sessionManager;
        private BufferPool bufferPool;

        private class ClientSessionTracker
        {
            /// <summary>Generally this is the same as the session.SessionName</summary>
            public string clientName;
            /// <summary>Generally this is the same as the session.SessionUUID</summary>
            public string uuid;
            public Sessions.IMessageSessionFacet session;

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
                return "CST {0} uuid:{1}".CheckedFormat(clientName, uuid);
            }
        }

        private IListWithCachedArray<ClientSessionTracker> clientSessionTrackerList = new IListWithCachedArray<ClientSessionTracker>();
        private Dictionary<string, ClientSessionTracker> uuidToClientSessionTrackerDictionary = new Dictionary<string, ClientSessionTracker>();
        private Dictionary<string, ClientSessionTracker> clientNameToClientSessionTrackerDictionary = new Dictionary<string, ClientSessionTracker>();

        private void AddClientSessionTracker(ClientSessionTracker cst)
        {
            clientSessionTrackerList.Add(cst);
            uuidToClientSessionTrackerDictionary[cst.uuid] = cst;
            clientNameToClientSessionTrackerDictionary[cst.clientName] = cst;
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
                    sessionManager = new Sessions.SessionManager(PartID, Config.ConfigNVS, hostNotifier: this, handleNewSessionDelegate: HandleNewSession, bufferPool: bufferPool, stateEmitter: Log.Debug, issueEmitter: Log.Debug, traceEmitter: traceLogger.Trace);

                    transport = Transport.TransportConnectionFactory.Instance.CreateServerConnection(Config.ConfigNVS, sessionManager);
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
                InnerServiceBackground(QpcTimeStamp.Now);

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

        protected override string PerformServiceAction(Action.IProviderActionBase<string, NullObj> action)
        {
            StringScanner ss = new StringScanner(action.ParamValue);

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

                    cst.actionRelayStreamTool.RelayServiceAction(ss.Rest, action);
                    return null;
                }
            }
            else if (ss.MatchToken("Sync"))
            {
                SyncFlags syncFlags = default(SyncFlags);

                if (ss.IsAtEnd || ss.ParseValue<SyncFlags>(out syncFlags))
                    return PerformSync(syncFlags);
                else
                    return "Invalid or unsupported Sync request format [{0}]".CheckedFormat(action.ParamValue);
            }

            return base.PerformServiceAction(action);
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

            InnerServiceBackground(QpcTimeStamp.Now);

            return string.Empty;
        }

        protected override void PerformMainLoopService()
        {
            InnerServiceBackground(QpcTimeStamp.Now);
        }

        private void InnerServiceBackground(QpcTimeStamp qpcTimeStamp)
        {
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
        }

        private void HandleNewSession(QpcTimeStamp qpcTimeStamp, Sessions.IMessageSessionFacet session)
        {
            ClientSessionTracker cst = new ClientSessionTracker()
            {
                session = session,
                clientName = session.SessionName,
                uuid = session.SessionUUID,
            };

            // generate a delegate that binds the ClientSessionTracker so that we can correctly route inbound messages
            session.HandleInboundMessageDelegate = ((qts, stream, message) => HandleInboundMessage(qts, cst, stream, message));

            AddClientSessionTracker(cst);

            Log.Debug.Emit("New client session added [{0} {1}]", session.SessionName, session.SessionUUID);
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
                    /// Todo: review if we should send an error message back for this cast.
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

            if (toolTypeStr == MessageStreamTools.BaseMessageStreamTool.Config.toolTypeStr)
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
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion1.Build(automaticallyIncAndDecBusyCountAroundActionInvoke: false, partBaseIVI: config.PartIVI))
        {
            Config = config.MakeCopyOfThis();
            traceLogger = new Logging.Logger(PartID + ".Trace", groupName: Logging.LookupDistributionGroupName, initialInstanceLogGate: Config.ConfigNVS["TraceLogger.InitialInstanceLogGate"].VC.GetValue(rethrow: false, defaultValue: Logging.LogGate.Debug));

            if (Config.StreamToolsConfigArray.IsNullOrEmpty())
                throw new System.ArgumentOutOfRangeException("Config.StreamToolsConfigArray", "Must contain at least one StreamToolConfigBase derived instance");

            bufferPool = new BufferPool(configNVS: config.ConfigNVS, bufferStateEmitter: traceLogger.Trace);

            AddMainThreadStoppingAction(() => Release(releaseBufferPool: true, releaseMSTTs: true));

            ActionLoggingReference.Config = ActionLoggingConfig.Debug_Debug_Trace_Trace;

            _serverInfoNVSPublisher = new InterlockedNotificationRefObject<INamedValueSet>();
            serverInfoNVSIVA = (config.PartIVI ?? Interconnect.Values.Values.Instance).GetValueAccessor("{0}.ServerInfoNVS".CheckedFormat(PartID));
        }

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

        private BufferPool bufferPool;
        private Transport.ITransportConnection transport;
        private Sessions.ConnectionSession session;
        private Sessions.SessionConfig sessionConfig;

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

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            TimeSpan maxSessionConnectWaitTime = Config.ConfigNVS["MaxSessionConnectWaitTime"].VC.GetValue(rethrow: false, defaultValue: (5.0).FromSeconds());
            TimeSpan maxSessionCloseWaitTime = Config.ConfigNVS["MaxSessionCloseWaitTime"].VC.GetValue(rethrow: false, defaultValue: (1.0).FromSeconds());

            string ec = InnerPerformGoOnlineAction(andInitialize, CurrentActionDescription, maxSessionConnectWaitTime, maxSessionCloseWaitTime);

            return ec;
        }

        private string InnerPerformGoOnlineAction(bool andInitialize, string currentActionDescription, TimeSpan maxSessionConnectWaitTime, TimeSpan maxSessionCloseWaitTime)
        {
            string ec = string.Empty;

            try
            {
                if (andInitialize && BaseState.IsOnlineOrAttemptOnline)
                    ec = InnerPerformGoOfflineAction(currentActionDescription, maxSessionCloseWaitTime);        // this does a Release...

                if (session == null || transport == null || session.State.IsClosing || session.State.IsPerminantlyClosed)
                {
                    Fcns.DisposeOfObject(ref session);
                    session = new Sessions.ConnectionSession(PartID, Config.ConfigNVS, hostNotifier: this, bufferPool: bufferPool, initialSessionStateCode: Sessions.SessionStateCode.ClientSessionInitial, issueEmitter: Log.Debug, stateEmitter: Log.Debug, traceEmitter: traceLogger.Trace);
                    session.HandleInboundMessageDelegate = HandleInboundMessage;

                    sessionConfig = session.Config;

                    Fcns.DisposeOfObject(ref transport);
                    transport = Transport.TransportConnectionFactory.Instance.CreateClientConnection(Config.ConfigNVS, session);
                }

                // only create message stream tools once (especially important with set relay tool
                if (session != null && transport != null && messageStreamToolTrackerArray.IsNullOrEmpty())
                {
                    actionRelayStreamTool = null;
                    var adjustedStreamToolConfigArray = new MessageStreamTools.BaseMessageStreamTool.Config().Concat<MessageStreamToolConfigBase>(Config.StreamToolsConfigArray).ToArray();
                    messageStreamToolTrackerArray = adjustedStreamToolConfigArray.Select((stc, index) => new MessageStreamToolTracker() { stream = index, messageStreamToolConfig = stc, messageStreamTool = CreateStreamTool(index, stc) }).ToArray();
                }

                if (!session.State.IsConnected(includeClosingStates: true))
                    session.SetState(QpcTimeStamp.Now, Sessions.SessionStateCode.RequestTransportConnect, currentActionDescription);

                QpcTimer waitTimer = new QpcTimer() { TriggerInterval = maxSessionConnectWaitTime }.Start();

                while (ec.IsNullOrEmpty())
                {
                    WaitForSomethingToDo();

                    InnerServiceBackground(QpcTimeStamp.Now);

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

        private string InnerPerformGoOfflineAction(string actionDescription, TimeSpan maxSessionCloseWaitTime)
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

                InnerServiceBackground(QpcTimeStamp.Now);

                while (session.State.StateCode != Sessions.SessionStateCode.Terminated)
                {
                    WaitForSomethingToDo();
                    InnerServiceBackground(QpcTimeStamp.Now);

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

        protected override string PerformServiceAction(Action.IProviderActionBase<string, NullObj> action)
        {
            StringScanner ss = new StringScanner(action.ParamValue);

            if (ss.MatchToken("Remote"))
            {
                if (actionRelayStreamTool != null)
                {
                    actionRelayStreamTool.RelayServiceAction(ss.Rest, action);
                    InnerServiceBackground(QpcTimeStamp.Now);
                    return null;
                }
                else
                {
                    return "Cannot relay this action because client has not been configured with an Action Relay Stream Tool";
                }
            }
            else if (ss.MatchToken("Sync"))
            {
                SyncFlags syncFlags = default(SyncFlags);

                if (ss.IsAtEnd || ss.ParseValue<SyncFlags>(out syncFlags))
                    return PerformSync(syncFlags);
                else
                    return "Invalid or unsupported Sync request format [{0}]".CheckedFormat(action.ParamValue);
            }
            else if (ss.MatchToken("FaultInjection"))
            {
                switch (ss.ExtractToken())
                {
                    case "GoToOnlineFailure":
                        Release();
                        SetBaseState(UseState.OnlineFailure, "By explicit request [{0}]".CheckedFormat(action.ParamValue)); 
                        return "";

                    case "ManuallyTriggerAutoReconnectAttempt": 
                        manuallyTriggerAutoReconnectAttempt = true;  
                        return "";

                    default: 
                        return base.PerformServiceAction(action);      // we expect this to fail with the standard error message...
                }
            }

            return base.PerformServiceAction(action);
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

            InnerServiceBackground(QpcTimeStamp.Now);

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

                SetBaseState(UseState.AttemptOnline, "Starting Client AutoReconnect Attempt");

                string ec = InnerPerformGoOnlineAction(false, "Client AutoReconnect Attempt", maxSessionAutoReconnectWaitTime, maxSessionAutoReconnectCloseWaitTime);

                if (ec.IsNullOrEmpty())
                    SetBaseState(UseState.Online, "Client AutoReconnect complete");
                else
                    SetBaseState(UseState.AttemptOnlineFailed, "Client AutoReconnect failed: {0}".CheckedFormat(ec));
            }
        }

        private void InnerServiceBackground(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;
            if (transport != null)
                count += transport.Service(qpcTimeStamp);

            if (session != null)
                count += session.Service(qpcTimeStamp);

            count += messageStreamToolTrackerArray.Sum(mstt => InnerServiceStream(qpcTimeStamp, mstt));

            NoteWorkCount(count);
        }

        private MessageStreamTools.IMessageStreamTool CreateStreamTool(int stream, MessageStreamTools.MessageStreamToolConfigBase messageStreamToolConfig)
        {
            MessageStreamTools.BaseMessageStreamTool.Config baseMSTConfig = messageStreamToolConfig as MessageStreamTools.BaseMessageStreamTool.Config;

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
}

//-------------------------------------------------------------------
