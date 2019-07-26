//-------------------------------------------------------------------
/*! @file Interconnect/Remoting/Sessions.cs
 *  @brief Common Session related definitions for Modular.Interconnect.Remoting.Sessions
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
using System.Net;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.Attributes;
using MosaicLib.Modular.Interconnect.Remoting.Buffers;
using MosaicLib.Modular.Interconnect.Remoting.Messages;
using MosaicLib.Semi.E005.Data;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Modular.Interconnect.Values;

// Please note: see comments in for MosaicLib.Modular.Interconnect.Remoting in Remoting.cs

namespace MosaicLib.Modular.Interconnect.Remoting.Sessions
{
    /// <summary>
    /// This enum gives the current summary state for a given client session.
    /// <para/>None (0), ClientSessionInitial, ServerSessionInitial, Active, Idle, IdleWithPendingWork, RequestTransportConnect, RequestTransportReconnect, RequestSessionOpen, RequestSessionResume, CloseRequested, ConnectionClosed, Terminated
    /// </summary>
    public enum SessionStateCode : int
    {
        /// <summary>Default placeholder value [0]</summary>
        None = 0,

        /// <summary>Initial session state for a session created by a client.  This is not a connected state.  Client normally transitions to RequestTransportOpen when it is ready to make the connection</summary>
        ClientSessionInitial,

        /// <summary>Intial session state for a session that has been created by a server.  This is a partially connected state.  Session will ignore inbound buffers until it encounters a SessionOpenRequest</summary>
        ServerSessionInitial,

        /// <summary>Session is connected and is active (valid traffic sent or received within stated period)</summary>
        Active,

        /// <summary>Session is connected but no recent (valid) traffic has been sent, or received</summary>
        Idle,

        /// <summary>Session is connected but no recent (valid) traffic has been sent, or received, and the outbound queue is non-empty.  This is typically only expected when the session is about to fail.</summary>
        IdleWithPendingWork,

        /// <summary>Requests that the transport layer open the session's connection.  Transport responds with NoteTransportConnected once the connection is complete.</summary>
        RequestTransportConnect,

        /// <summary>State that covers Client Open Requests.  Session will then periodically post Session Open requests.</summary>
        RequestSessionOpen,

        /// <summary>The Session client has requested that the session be closed (perminantly).  CloseRequestReason will be non-empty.</summary>
        CloseRequested,

        /// <summary>The transport layer has indicated that the session has been closed, either by request, or due to an error.  Auto-reconnect attempts may occur from this state.</summary>
        ConnectionClosed,

        /// <summary>The transport layer has indicated that the session has been closed/terminated due to an error.  This state will not attempt any auto-reconnect.</summary>
        Terminated,
    }

    /// <summary>
    /// This enumeration is used to provide programatically usable meaning in termination conditions.  This allows the remoting client to vary its reconnect timing logic accordingly.
    /// </summary>
    public enum TerminationReasonCode : int
    {
        /// <summary>Default placeholder - no reason code was provided</summary>
        None = 0,

        /// <summary>The Session was terminated because it was closed by request</summary>
        ClosedByRequest,

        /// <summary>Server's SessionManager's BufferSize does not match value included with client's RequestOpenSession managment request.</summary>
        BufferSizesDoNotMatch,

        /// <summary>SessionManager or ConnectedSession detected a protocol violation.</summary>
        ProtocolViolation,

        /// <summary>Session time limit reached while waiting for keepalive probe to be delivered.</summary>
        SessionKeepAliveTimeLimitReached,

        /// <summary>Session time limit reached while waiting for pending work (messages) to be delivered.</summary>
        SessionPendingWorkTimeLimitReached,
        ConnectWaitTimeLimitReached,
        CloseRequestWaitTimeLimitReached,
    }

    public static partial class ExtensionMethods
    {
        public static bool IsConnected(this SessionStateCode stateCode, bool includeConnectingStates = false, bool includeClosingStates = false)
        {
            switch (stateCode)
            {
                case SessionStateCode.Active:
                case SessionStateCode.Idle:
                case SessionStateCode.IdleWithPendingWork:
                    return true;

                case SessionStateCode.CloseRequested:
                    return includeClosingStates;

                case SessionStateCode.ServerSessionInitial:
                case SessionStateCode.RequestTransportConnect:
                case SessionStateCode.RequestSessionOpen:
                    return includeConnectingStates;

                case SessionStateCode.ConnectionClosed:
                case SessionStateCode.Terminated:
                default: 
                    return false;
            }
        }

        public static bool IsClosing(this SessionStateCode stateCode)
        {
            switch (stateCode)
            {
                case SessionStateCode.CloseRequested:
                    return true;

                default:
                    return false;
            }

        }

        public static bool IsConnecting(this SessionStateCode stateCode)
        {
            switch (stateCode)
            {
                case SessionStateCode.ServerSessionInitial:
                case SessionStateCode.RequestTransportConnect:
                case SessionStateCode.RequestSessionOpen:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true if the given <paramref name="stateCode"/> is Terminated (or any other perminantly closed state - tbd)
        /// </summary>
        public static bool IsPerminantlyClosed(this SessionStateCode stateCode)
        {
            return (stateCode == SessionStateCode.Terminated);
        }

        public static bool CanAcceptOutboundMessages(this SessionStateCode stateCode)
        {
            switch (stateCode)
            {
                case SessionStateCode.Active:
                case SessionStateCode.Idle:
                case SessionStateCode.IdleWithPendingWork:
                    return true;
                default:
                    return false;
            }
        }
    }

    public delegate void HandleNewSessionDelegate(QpcTimeStamp qpcTimeStamp, IMessageSessionFacet clientSession);

    public delegate void HandleMessageDelegate(QpcTimeStamp qpcTimeStamp, ushort stream, Messages.Message message);

    public interface ISessionState : IEquatable<ISessionState>
    {
        SessionStateCode StateCode { get; }
        QpcTimeStamp TimeStamp { get; }
        TerminationReasonCode TerminationReasonCode { get; }
        string Reason { get; }
    }

    public struct SessionState : ISessionState, IEquatable<ISessionState>
    {
        public SessionStateCode StateCode { get; set; }
        public QpcTimeStamp TimeStamp { get; set; }
        public TerminationReasonCode TerminationReasonCode { get; set; }
        public string Reason { get; set; }

        public override string ToString()
        {
            if (StateCode != SessionStateCode.Terminated || TerminationReasonCode == Sessions.TerminationReasonCode.None)
                return "{0} [{1}]".CheckedFormat(StateCode, Reason.MapNullOrEmptyTo("NoReasonGiven"));
            else
                return "{0} [{1} {2}]".CheckedFormat(StateCode, TerminationReasonCode, Reason.MapNullOrEmptyTo("NoReasonGiven"));
        }

        public bool Equals(ISessionState other)
        {
            return (other != null 
                    && StateCode == other.StateCode
                    && TimeStamp == other.TimeStamp
                    && TerminationReasonCode == other.TerminationReasonCode
                    && Reason == other.Reason
                    );
        }

        public bool IsConnecting { get { return StateCode.IsConnecting(); } }
        public bool IsClosing { get { return StateCode.IsClosing(); } }
        public bool IsConnected(bool includeConnectingStates = false, bool includeClosingStates = false) { return StateCode.IsConnected(includeConnectingStates: includeConnectingStates, includeClosingStates: includeClosingStates); }

        /// <summary>Returns true if the contained StateCode is Terminated (or any other perminantly closed state - tbd)</summary>
        public bool IsPerminantlyClosed { get { return StateCode.IsPerminantlyClosed(); } }
        public bool CanAcceptOutboundMessages { get { return StateCode.CanAcceptOutboundMessages(); } }
    }

    public interface IMessageSessionFacet : IServiceable
    {
        string SessionName { get; }
        string ClientUUID { get; }
        ulong ClientInstanceNum { get; }

        SessionState State { get; }
        ISequencedObjectSource<SessionState, int> StatePublisher { get; }

        void SetState(QpcTimeStamp qpcTimeStamp, SessionStateCode stateCode, string reason, TerminationReasonCode terminationReasonCode = TerminationReasonCode.None);

        void HandleOutboundMessage(QpcTimeStamp qpcTimeStamp, ushort stream, Messages.Message message);
        HandleMessageDelegate HandleInboundMessageDelegate { get; set; }
    }

    public class SessionConfig
    {
        public SessionConfig()
        {
            ConnectWaitTimeLimit = (5.0).FromSeconds();
            CloseRequestWaitTimeLimit = (1.25).FromSeconds();
            SessionExpirationPeriod = (5.0).FromMinutes();
            ActiveToIdleHoldoff = (5.0).FromSeconds();
            NominalKeepAliveSendInterval = (10.0).FromSeconds();
            MaxBufferWriteAheadCount = 30;      // this is a reasonable default for 1k buffers since the socket buffer size should be significantly larger than 32k
            MaxOutOfOrderBufferHoldPeriod = (10.0).FromSeconds();
            MaxOutOfOrderBufferHoldCount = 100;
            ShortRetransmitHoldoffPeriod = (0.200).FromSeconds();
            NormalRetransmitHoldoffPeriod = (0.400).FromSeconds();
            MaxHeldBufferSeqNumsToIncludeInStatusUpdate = 20;
            ExplicitAckHoldoffPeriod = (0.020).FromSeconds();
            ConnectionDegradedHoldoff = (5.0).FromSeconds();
        }

        [NamedValueSetItem]
        public TimeSpan ConnectWaitTimeLimit { get; set; }

        [NamedValueSetItem]
        public TimeSpan CloseRequestWaitTimeLimit { get; set; }

        [NamedValueSetItem]
        public TimeSpan SessionExpirationPeriod { get; set; }

        [NamedValueSetItem]
        public TimeSpan ActiveToIdleHoldoff { get; set; }

        [NamedValueSetItem]
        public TimeSpan NominalKeepAliveSendInterval { get; set; }

        [NamedValueSetItem]
        public int MaxBufferWriteAheadCount { get; set; }

        [NamedValueSetItem]
        public TimeSpan? AutoReconnectHoldoff { get; set; }

        [NamedValueSetItem]
        public TimeSpan MaxOutOfOrderBufferHoldPeriod { get; set; }

        [NamedValueSetItem]
        public int MaxOutOfOrderBufferHoldCount { get; set; }

        [NamedValueSetItem]
        public TimeSpan ShortRetransmitHoldoffPeriod { get; set; }

        [NamedValueSetItem]
        public TimeSpan NormalRetransmitHoldoffPeriod { get; set; }

        [NamedValueSetItem]
        public int MaxHeldBufferSeqNumsToIncludeInStatusUpdate { get; set; }

        [NamedValueSetItem]
        public TimeSpan ExplicitAckHoldoffPeriod { get; set; }

        [NamedValueSetItem]
        public TimeSpan ConnectionDegradedHoldoff { get; set; }
    }

    #region Transport specific interfaces: HandleBuffersDelegate, ISessionTransportFacet, ISessionTransportFactoryFacet

    /// <summary>
    /// This is the delegate that is used to deliver buffers both from the transport into the session and to deliver buffers from the session into the transport.
    /// When the caller is a session, it shall provide the transportEndpoint back to the transport that the transport provided to the session when the ression was first created or was last resumed.
    /// </summary>
    public delegate void HandleBuffersDelegate(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray);

    public interface ITransportSessionFacetBase
    {
        string SessionName { get; }
        SessionConfig Config { get; }

        INotifyable HostNotifier { get; }
        Buffers.BufferPool BufferPool { get; }

        INamedValueSet TransportParamsNVS { get; set; }

        HandleBuffersDelegate HandleOutboundBuffersDelegate { get; set; }
        void HandleInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray);

        void HandleTransportException(QpcTimeStamp qpcTimeStamp, object transportEndpoint, System.Exception ex, bool endpointClosed = false);
    }

    public interface ITransportConnectionSessionFacet : ITransportSessionFacetBase
    {
        string ClientUUID { get; }
        ulong ClientInstanceNum { get; }
        INamedValueSet HostParamsNVS { get; }

        SessionState State { get; }

        void NoteTransportIsConnected(QpcTimeStamp qpcTimeStamp, object transportEndpoint = null);
        void NoteTransportIsClosed(QpcTimeStamp qpcTimeStamp, object transportEndpoint = null, string failureCode = "");

        string CloseRequestReason { get; }
    }

    public interface ITransportServerSessionManagerFacet : ITransportSessionFacetBase
    {
        ITransportConnectionSessionFacet ProcessSessionLevelInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, HandleBuffersDelegate newConnectionHandleOutboundBuffersDelegate, params Buffers.Buffer[] bufferParamsArray);
    }


    /// <summary>
    /// Exception type typically thrown when a session encounters an unacceptable buffer that it cannot ignore.
    /// </summary>
    public class SessionException : System.Exception
    {
        public SessionException(string message, System.Exception innerException = null, SessionExceptionType sessionExceptionType = default(SessionExceptionType))
            : base(message, innerException)
        {
            SessionExceptionType = sessionExceptionType;
        }

        public SessionExceptionType SessionExceptionType { get; private set; }
    }

    public enum SessionExceptionType : int
    {
        Default = 0,
        TrafficRejectedByRemoteEnd,
    }

    #endregion

    #region SessionManager

    public class SessionManager : ITransportServerSessionManagerFacet
    {
        public static AtomicUInt64 instanceNumGen = new AtomicUInt64();
        public readonly ulong instanceNum = instanceNumGen.Increment();

        public SessionManager(string hostName, string hostUUID, INamedValueSet hostParamsNVS, INotifyable hostNotifier, Logging.IMesgEmitter stateEmitter, HandleNewSessionDelegate handleNewSessionDelegate, Buffers.BufferPool bufferPool = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter traceEmitter = null)
        {
            HostName = hostName.MapNullToEmpty();
            HostParamsNVS = hostParamsNVS.ConvertToReadOnly();
            HostNotifier = hostNotifier ?? NullNotifier.Instance;

            SessionName = "{0}.sm_{1:x4}".CheckedFormat(HostName, instanceNum & 0xffff);

            TraceEmitter = traceEmitter ?? Logging.NullMesgEmitter.Instance;
            StateEmitter = stateEmitter ?? TraceEmitter;
            IssueEmitter = issueEmitter ?? StateEmitter;

            HandleNewSessionDelegate = handleNewSessionDelegate;
            BufferPool = bufferPool ?? new Buffers.BufferPool(SessionName + ".dbp", bufferStateEmitter: traceEmitter, configNVS: hostParamsNVS, configNVSKeyPrefix: "Server.BufferPool.");

            new NamedValueSetAdapter<SessionConfig>() { IssueEmitter = IssueEmitter, ValueNoteEmitter = TraceEmitter, ValueSet = Config = new SessionConfig() }.Setup().Set(HostParamsNVS, merge: true);
        }

        public string HostName { get; private set; }
        public string HostUUID { get; private set; }
        public INamedValueSet HostParamsNVS { get; private set; }
        public INotifyable HostNotifier { get; private set; }
        public IEventAndPerformanceRecording EventAndPerformanceRecording { get; set; }

        public string SessionName { get; private set; }

        public Logging.IMesgEmitter IssueEmitter { get; private set; }
        public Logging.IMesgEmitter StateEmitter { get; private set; }
        public Logging.IMesgEmitter TraceEmitter { get; private set; }

        public Buffers.BufferPool BufferPool { get; private set; }
        public HandleNewSessionDelegate HandleNewSessionDelegate { get; private set; }

        public INamedValueSet TransportParamsNVS { get; set; }

        public Transport.TransportTypeFeatures TransportTypeFeatures { get; set; }

        public HandleBuffersDelegate HandleOutboundBuffersDelegate { get; set; }
        public HandleBuffersDelegate HandleInboundBuffersDelegate { get { return HandleInboundBuffers; } }

        public SessionConfig Config { get; private set; }

        public void HandleInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray)
        {
            ITransportConnectionSessionFacet session = transportEPToClientSessionDictionary.SafeTryGetValue(transportEndpoint);

            if (session != null)
                session.HandleInboundBuffers(qpcTimeStamp, transportEndpoint, bufferParamsArray);
            else
                session = ProcessSessionLevelInboundBuffers(qpcTimeStamp, transportEndpoint, null, bufferParamsArray);        // passing null triggers this method to use the SessionManager's HandleOutboundBuffersDelegate
        }

        public void HandleTransportException(QpcTimeStamp qpcTimeStamp, object transportEndpoint, System.Exception ex, bool endpointClosed = false)
        {
            if (TraceEmitter.IsEnabled)
                TraceEmitter.Emit("{0}: ep:{1}{2} ex: {3}", Fcns.CurrentMethodName, transportEndpoint, endpointClosed ? " Closed" : "", ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

            ConnectionSession session = transportEPToClientSessionDictionary.SafeTryGetValue(transportEndpoint);

            if (session != null)
                session.HandleTransportException(qpcTimeStamp, transportEndpoint, ex, endpointClosed);
        }

        public ITransportConnectionSessionFacet ProcessSessionLevelInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, HandleBuffersDelegate newConnectionHandleOutboundBuffersDelegate, params Buffers.Buffer[] bufferParamsArray)
        {
            Buffers.Buffer buffer = bufferParamsArray.SafeAccess(0);

            while (buffer != null && buffer.PurposeCode == PurposeCode.Ack)
            {
                bufferParamsArray = bufferParamsArray.Skip(1).ToArray();
                buffer = bufferParamsArray.SafeAccess(0);
            }

            if (buffer == null)
                return null;

            ITransportConnectionSessionFacet session = ProcessSessionLevelInboundBuffer(qpcTimeStamp, transportEndpoint, newConnectionHandleOutboundBuffersDelegate, buffer);

            bufferParamsArray = bufferParamsArray.Skip(1).ToArray();

            if (bufferParamsArray.IsNullOrEmpty())
                return session;

            if (session != null)
                session.HandleInboundBuffers(qpcTimeStamp, transportEndpoint, bufferParamsArray);
            else
                IssueEmitter.Emit("{0}: Ignoring additional buffers after no session found for first non-Ack buffer [{1}, {2}]", Fcns.CurrentMethodName, buffer, string.Join(", ", bufferParamsArray.Select(b => b.ToString())));

            return session;
        }

        private ITransportConnectionSessionFacet ProcessSessionLevelInboundBuffer(QpcTimeStamp qpcTimeStamp, object transportEndpoint, HandleBuffersDelegate newConnectionHandleOutboundBuffersDelegate, Buffers.Buffer buffer)
        {
            EventAndPerformanceRecording.RecordReceived(buffer);

            switch (buffer.PurposeCode)
            {
                case PurposeCode.Management:
                    {
                        Buffers.BufferHeaderV1 bufferHeader = (buffer != null) ? buffer.header : default(Buffers.BufferHeaderV1);
                        INamedValueSet bufferNVS = (buffer != null) ? buffer.GetPayloadAsE005NVS(NamedValueSet.Empty) : NamedValueSet.Empty;
                        ManagementType managementType = bufferNVS["Type"].VC.GetValue<ManagementType>(rethrow: false);

                        EventAndPerformanceRecording.RecordReceived(managementType);

                        if (managementType == ManagementType.RequestOpenSession && bufferHeader.SeqNum == 0)
                        {
                            int startAtIndex = bufferHeader.Length;
                            string clientUUID = bufferNVS["ClientUUID"].VC.GetValue<string>(rethrow: false);
                            ulong clientInstanceNum = bufferNVS["ClientInstanceNum"].VC.GetValue<ulong>(rethrow: false);
                            string name = bufferNVS["Name"].VC.GetValue<string>(rethrow: false);

                            if (!clientUUID.IsNullOrEmpty() && clientInstanceNum != 0)
                            {
                                ConnectionSession newClientSession = new ConnectionSession(HostName, HostUUID, clientUUID, clientInstanceNum, HostParamsNVS, HostNotifier, bufferPool: BufferPool, sessionConfig: Config, issueEmitter: IssueEmitter, stateEmitter: StateEmitter, traceEmitter: TraceEmitter)
                                {
                                    TransportParamsNVS = TransportParamsNVS,
                                    TransportEndpoint = transportEndpoint,
                                    TransportRole = Transport.TransportRole.Server,
                                    TransportTypeFeatures = TransportTypeFeatures,
                                    HandleOutboundBuffersDelegate = newConnectionHandleOutboundBuffersDelegate ?? HandleOutboundBuffersDelegate,
                                    SessionName = name,
                                    EventAndPerformanceRecording = EventAndPerformanceRecording,
                                };

                                ConnectionSession[] strandedSessionsArray = clientUUIDToClientSessionDictionary.SafeTryGetValue(clientUUID).ConcatItems(transportEPToClientSessionDictionary.SafeTryGetValue(transportEndpoint)).Where(item => item != null).ToArray();
                                if (!strandedSessionsArray.IsNullOrEmpty())
                                {
                                    TraceEmitter.Emit("{0} {1} stranded {2} sessions [{3}]", Fcns.CurrentMethodName, managementType, strandedSessionsArray.Length, string.Join(",", strandedSessionsArray.Select(item => item.SessionName)));

                                    pendingDeadSessionList.SafeAddSet(strandedSessionsArray);
                                }

                                clientUUIDToClientSessionDictionary[clientUUID] = newClientSession;       // will need to filter and remove prior connected sessions for the same ClientUUID
                                transportEPToClientSessionDictionary[transportEndpoint] = newClientSession;

                                IPEndPoint ipEndPoint = (transportEndpoint as IPEndPoint);
                                if (ipEndPoint != null)
                                {
                                    IPAddress ipAddress = ipEndPoint.Address;

                                    List<ConnectionSession> clientSessionList = ipAddressToClientSessionListDictionary.SafeTryGetValue(ipAddress);
                                    if (clientSessionList == null)
                                        ipAddressToClientSessionListDictionary[ipEndPoint.Address] = (clientSessionList = new List<ConnectionSession>());

                                    if (!clientSessionList.Contains(newClientSession))
                                        clientSessionList.Add(newClientSession);
                                }

                                clientSessionArray = null;

                                newClientSession.SetState(qpcTimeStamp, SessionStateCode.ServerSessionInitial, Fcns.CurrentMethodName);

                                HandleNewSessionDelegate handleNewSessionDelegate = HandleNewSessionDelegate;
                                if (handleNewSessionDelegate != null)
                                    handleNewSessionDelegate(qpcTimeStamp, newClientSession);

                                newClientSession.HandleInboundBuffers(qpcTimeStamp, transportEndpoint, buffer);

                                return newClientSession;
                            }
                            else
                            {
                                IssueEmitter.Emit("{0}: received invalid buffer {1} with ManagementType:{2} from ep:'{3}': no valid SessionUUID found", Fcns.CurrentMethodName, buffer, managementType, transportEndpoint);
                            }
                        }
                        else
                        {
                            IssueEmitter.Emit("{0}: received buffer {1} with unexpected ManagementType:{2} from ep:'{3}'", Fcns.CurrentMethodName, buffer, managementType, transportEndpoint);
                        }
                    }
                    return null;

                case PurposeCode.Ack:
                    EventAndPerformanceRecording.RecordEvent(RecordEventType.UnexpectedNonManagementBuffersGivenToSessionManager);

                    TraceEmitter.Emit("{0}: received unexpected Ack only buffer {1} from ep:'{2}' [ignored]", Fcns.CurrentMethodName, buffer, transportEndpoint);
                    return null;

                default:
                    EventAndPerformanceRecording.RecordEvent(RecordEventType.UnexpectedNonManagementBuffersGivenToSessionManager);

                    IssueEmitter.Emit("{0}: received unexpected non-managment buffer {1} from ep:'{2}' [ignored]", Fcns.CurrentMethodName, buffer, transportEndpoint);
                    return null;
            }
        }

        public int Service(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            count += RebuildClientSessionArrayIfNeeded.Sum(session => session.Service(qpcTimeStamp));
            count += RemoveTerminatedSessions(qpcTimeStamp);

            return count;
        }

        private Dictionary<string, ConnectionSession> clientUUIDToClientSessionDictionary = new Dictionary<string, ConnectionSession>();
        private Dictionary<object, ConnectionSession> transportEPToClientSessionDictionary = new Dictionary<object, ConnectionSession>();
        private Dictionary<IPAddress, List<ConnectionSession>> ipAddressToClientSessionListDictionary = new Dictionary<IPAddress, List<ConnectionSession>>();
        private List<ConnectionSession> pendingDeadSessionList = new List<ConnectionSession>();

        private ConnectionSession[] clientSessionArray = null;

        private ConnectionSession[] RebuildClientSessionArrayIfNeeded { get { return (clientSessionArray ?? (clientSessionArray = transportEPToClientSessionDictionary.Values.ToArray())); } }

        private int RemoveTerminatedSessions(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            pendingDeadSessionList.AddRange(RebuildClientSessionArrayIfNeeded.Where(session => session.State.IsPerminantlyClosed));

            if (!pendingDeadSessionList.IsNullOrEmpty())
            {
                ConnectionSession[] terminatedSessionArray = pendingDeadSessionList.ToArray();
                pendingDeadSessionList.Clear();

                foreach (var session in terminatedSessionArray)
                {
                    if (!session.State.IsPerminantlyClosed)
                        session.SetState(qpcTimeStamp, SessionStateCode.Terminated, "Session unexpectedly moved to pendingDeadSessionList");

                    if (!session.ClientUUID.IsNullOrEmpty() && Object.ReferenceEquals(session, clientUUIDToClientSessionDictionary.SafeTryGetValue(session.ClientUUID)))
                        clientUUIDToClientSessionDictionary.Remove(session.ClientUUID);

                    if (session.TransportEndpoint != null && Object.ReferenceEquals(session, transportEPToClientSessionDictionary.SafeTryGetValue(session.TransportEndpoint)))
                        transportEPToClientSessionDictionary.Remove(session.TransportEndpoint);

                    IPAddress ipAddress = session.IPAddress;
                    if (ipAddress != null)
                    {
                        List<ConnectionSession> sessionList = ipAddressToClientSessionListDictionary.SafeTryGetValue(ipAddress);

                        if (!sessionList.IsNullOrEmpty())
                            sessionList.Remove(session);

                        if (sessionList.IsNullOrEmpty())
                            transportEPToClientSessionDictionary.Remove(ipAddress);
                    }
                }

                if (clientSessionArray != null)
                    clientSessionArray = null;

                count += terminatedSessionArray.Length;
            }

            return count;
        }
    }

    #endregion

    #region ConnectionSession

    public class ConnectionSession : IServiceable, IMessageSessionFacet, ITransportConnectionSessionFacet
    {
        private static ulong localInstanceNumGen = 0;

        public ConnectionSession(string hostName, string hostUUID, string clientUUID, ulong ? clientInstanceNum, INamedValueSet hostParamsNVS, INotifyable hostNotifier, Buffers.BufferPool bufferPool, SessionConfig sessionConfig = null, SessionStateCode initialSessionStateCode = SessionStateCode.None, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter stateEmitter = null, Logging.IMesgEmitter traceEmitter = null, IValuesInterconnection ivi = null)
        {
            IsClientSession = (initialSessionStateCode == SessionStateCode.ClientSessionInitial);

            HostName = hostName.MapNullToEmpty();
            HostUUID = hostUUID;
            ClientUUID = clientUUID;
            ClientInstanceNum = clientInstanceNum ?? ++localInstanceNumGen;
            HostParamsNVS = hostParamsNVS.ConvertToReadOnly();
            HostNotifier = hostNotifier ?? NullNotifier.Instance;

            TraceEmitter = traceEmitter ?? Logging.NullMesgEmitter.Instance;
            StateEmitter = stateEmitter ?? TraceEmitter;
            IssueEmitter = issueEmitter ?? StateEmitter;

            SessionName = "{0}.cs_{1:x4}".CheckedFormat(HostName, ClientInstanceNum & 0xffff);

            BufferPool = bufferPool ?? new Buffers.BufferPool(SessionName + ".dbp",  bufferStateEmitter: stateEmitter);

            if ((Config = sessionConfig) == null)
                new NamedValueSetAdapter<SessionConfig>() { IssueEmitter = IssueEmitter, ValueNoteEmitter = TraceEmitter, ValueSet = Config = new SessionConfig() }.Setup().Set(HostParamsNVS, merge: true);

            State = new SessionState() { StateCode = initialSessionStateCode, TimeStamp = QpcTimeStamp.Now, Reason = "Construction" };
            lastRecvActivityTimeStamp = lastSendActivityTimeStamp = State.TimeStamp;

            if (ivi != null && IsClientSession)
                SessionStateIVA = ivi.GetValueAccessor<ISessionState>("{0}.SessionState".CheckedFormat(HostName)).Set((ISessionState) State);
        }

        public bool IsClientSession { get; private set; }
        public string HostName { get; private set; }
        public string HostUUID { get; private set; }
        public string ClientUUID { get; private set; }
        public ulong ClientInstanceNum { get; private set; }
        public INamedValueSet HostParamsNVS { get; private set; }
        public INotifyable HostNotifier { get; set; }
        public IEventAndPerformanceRecording EventAndPerformanceRecording { get; set; }

        public IValueAccessor<ISessionState> SessionStateIVA { get; private set; }

        public Logging.IMesgEmitter IssueEmitter { get; private set; }
        public Logging.IMesgEmitter StateEmitter { get; private set; }
        public Logging.IMesgEmitter TraceEmitter { get; private set; }
        public Buffers.BufferPool BufferPool { get; set; }

        public SessionConfig Config { get; internal set; }

        public string SessionName { get; set; }

        public INamedValueSet TransportParamsNVS { get; set; }
        public object TransportEndpoint { get; set; }
        public Transport.TransportRole TransportRole { get; set; }
        public IPAddress IPAddress { get { IPEndPoint ipEP = TransportEndpoint as IPEndPoint; return (ipEP != null ? ipEP.Address : null); } }

        public Transport.TransportTypeFeatures TransportTypeFeatures { get { return _transportTypeFeatures; } set { _transportTypeFeatures = value; transportIsReliable = value.IsSet(Transport.TransportTypeFeatures.Reliable); } }
        private Transport.TransportTypeFeatures _transportTypeFeatures;
        private bool transportIsReliable;

        public string RemoteName { get { return _remoteName ?? "[RemoteNameIsNull]"; } private set { _remoteName = value; } }
        private string _remoteName = null;

        /// <summary>
        /// Delegate that is used to pass fully received stream messages to the next level up: typically to the stream handler
        /// </summary>
        public HandleMessageDelegate HandleInboundMessageDelegate { get; set; }

        /// <summary>
        /// Delegate that is used to pass outbound buffers to the transport layer to be queued for delivery.
        /// </summary>
        public HandleBuffersDelegate HandleOutboundBuffersDelegate { get; set; }

        public void HandleTransportException(QpcTimeStamp qpcTimeStamp, object transportEndpoint, System.Exception ex, bool endpointClosed = false)
        {
            SessionException sessionException = ex as SessionException;

            if (State.IsConnected(includeConnectingStates: true, includeClosingStates: true))
            {
                if (!State.IsConnecting || sessionException == null || sessionException.SessionExceptionType != SessionExceptionType.TrafficRejectedByRemoteEnd)
                {
                    IssueEmitter.Emit("{0} State:{1} note {2} for endPoint:{3}{4} ex:{5}", SessionName, StateCode, Fcns.CurrentMethodName, transportEndpoint, endpointClosed ? " Closed" : "", ex.ToString(ExceptionFormat.TypeAndMessage));

                    SetState(qpcTimeStamp, SessionStateCode.ConnectionClosed, "Encountered transport exception for {0}: {1}".CheckedFormat(transportEndpoint, ex.ToString(ExceptionFormat.TypeAndMessage)));

                    EventAndPerformanceRecording.RecordEvent(RecordEventType.TransportExceptionClosedSession);
                }
                else
                {
                    TraceEmitter.Emit("{0} State:{1} note {2} for endPoint:{3}{4} ex:{5} [Ignored]", SessionName, StateCode, Fcns.CurrentMethodName, transportEndpoint, endpointClosed ? " Closed" : "", ex.ToString(ExceptionFormat.TypeAndMessage));
                    EventAndPerformanceRecording.RecordEvent(RecordEventType.TransportException);
                }
            }
            else
            {
                TraceEmitter.Emit("{0} State:{1} note ignoring {2} for endPoint:{3}{4} ex:{5}", SessionName, StateCode, Fcns.CurrentMethodName, transportEndpoint, endpointClosed ? " Closed" : "", ex.ToString(ExceptionFormat.TypeAndMessage));
                EventAndPerformanceRecording.RecordEvent(RecordEventType.TransportException);
            }
        }

        public SessionState State { get { return _state; } private set { _statePublisher.Object = (_state = value); } }
        private SessionState _state = default(SessionState);
        public ISequencedObjectSource<SessionState, int> StatePublisher { get { return _statePublisher; } }
        private GuardedSequencedValueObject<SessionState> _statePublisher = new GuardedSequencedValueObject<SessionState>(default(SessionState));

        public SessionStateCode StateCode { get { return State.StateCode; } }

        public string CloseRequestReason { get; protected set; }

        public void SetState(QpcTimeStamp qpcTimeStamp, SessionStateCode stateCode, string reason, TerminationReasonCode terminationReasonCode = TerminationReasonCode.None)
        {
            SessionState entryState = State;
            Logging.IMesgEmitter useEmitter = StateEmitter;

            TimeSpan entryStateAge = (entryState.TimeStamp != QpcTimeStamp.Zero) ? (qpcTimeStamp - entryState.TimeStamp) : TimeSpan.Zero;

            State = new SessionState() { StateCode = stateCode, TimeStamp = qpcTimeStamp, Reason = reason ?? "SetState was not given a reason", TerminationReasonCode = terminationReasonCode };

            string closeRequestReason = string.Empty;

            switch (stateCode)
            {
                case SessionStateCode.Idle:
                case SessionStateCode.Active:
                    useEmitter = TraceEmitter;
                    break;

                case SessionStateCode.RequestTransportConnect:
                    if (entryState.StateCode == SessionStateCode.ClientSessionInitial)       // decrease number of normal state transition log messages for this case
                        useEmitter = TraceEmitter;
                    break;

                case SessionStateCode.RequestSessionOpen:
                    if (entryState.StateCode == SessionStateCode.RequestTransportConnect)       // decrease number of normal state transition log messages for this case
                        useEmitter = TraceEmitter;
                    break;

                case SessionStateCode.CloseRequested:
                    closeRequestReason = reason;            // update CloseRequestReason - used by transport to determine when to close a connection
                    break;

                case SessionStateCode.ConnectionClosed:
                    closeRequestReason = "Session is {0}: {1}".CheckedFormat(stateCode, reason);            // update CloseRequestReason - used by transport to determine when to close a connection
                    break;
                case SessionStateCode.Terminated:
                    closeRequestReason = "Session is {0}: {1}, {2}".CheckedFormat(stateCode, terminationReasonCode, reason);            // update CloseRequestReason - used by transport to determine when to close a connection
                    break;

                default:
                    break;
            }

            CloseRequestReason = closeRequestReason;

            if (SessionStateIVA != null)
                SessionStateIVA.Set((ISessionState) State);

            if (terminationReasonCode == TerminationReasonCode.None)
                useEmitter.Emit("{0} State changed to {1} [from: {2}, reason: {3}]", SessionName, stateCode, entryState, reason ?? "NoReasonGiven");
            else
                useEmitter.Emit("{0} State changed to {1}, {2} [from: {3}, reason: {4}]", SessionName, stateCode, terminationReasonCode, entryState, reason ?? "NoReasonGiven");
        }

        public void TouchStateTime(QpcTimeStamp qpcTimeStamp, string reason)
        {
            SessionState entryState = State;
            State = new SessionState() { StateCode = entryState.StateCode, TimeStamp = qpcTimeStamp, Reason = entryState.Reason };

            TraceEmitter.Emit("{0} State Timestamp touched [state:{0}, reason:{1}]", StateCode, reason ?? "NoReasonGiven");
        }

        public int Service(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            TimeSpan stateAge = (qpcTimeStamp - State.TimeStamp);

            switch (StateCode)
            {
                case SessionStateCode.ClientSessionInitial:
                case SessionStateCode.ServerSessionInitial:
                    break;

                case SessionStateCode.Idle:
                case SessionStateCode.IdleWithPendingWork:
                case SessionStateCode.Active:
                    var omCount = ServiceOutgoingMessageList(qpcTimeStamp);
                    var txCount = ServiceTransmitter(qpcTimeStamp);
                    var hmCount = ServiceHeldOutOfOrderBufferList(qpcTimeStamp, checkForStaleBuffers: true);     // used to get rid of stale held out of order buffers.
                    var kaCount = ServiceSendingKeepAliveBuffers(qpcTimeStamp);

                    count += (omCount + txCount + hmCount + kaCount);

                    TimeSpan lastValidAckTimeStampAge = qpcTimeStamp - lastRecvdValidAckBufferSeqNumTimeStamp;

                    if (StateCode == SessionStateCode.Active && count == 0)
                    {
                        int numStreamPendingMessages = outboundPerMessageStreamHandlerArray.Sum(handler => handler.outboundMessageList.Count);

                        // by default the elapsed is the min of the time in the current state (Active) and the time since we received the last valid ack.  
                        // On the client side this typically works with keepalive to make sure that the age of the last valid received ack only grows if the server is not responding.

                        TimeSpan elapsed = stateAge.Min(lastValidAckTimeStampAge);

                        // on the server also compute the time since the last send and receive activity and consider the connection active if there is either recent send or receive activity.
                        if (!IsClientSession)
                            elapsed = elapsed.Min(qpcTimeStamp - lastRecvActivityTimeStamp, qpcTimeStamp - lastSendActivityTimeStamp);

                        if (elapsed > Config.ActiveToIdleHoldoff)
                        {
                            SetState(qpcTimeStamp, (numStreamPendingMessages > 0) ? SessionStateCode.IdleWithPendingWork : SessionStateCode.Idle, "No recent session activity detected");
                            count++;
                        }
                    }
                    else if (StateCode != SessionStateCode.Active && omCount > 0)
                    {
                        SetState(qpcTimeStamp, SessionStateCode.Active, "Session became active (buffer transmitter)");
                    }
                    else if (StateCode != SessionStateCode.Active && (qpcTimeStamp - lastDeliveredMessageTimeStamp) <= (5.0).FromSeconds())
                    {
                        SetState(qpcTimeStamp, SessionStateCode.Active, "Session became active (message delivered here)");
                    }
                    else
                    {
                        int numStreamPendingMessages = outboundPerMessageStreamHandlerArray.Sum(handler => handler.outboundMessageList.Count);

                        if (StateCode == SessionStateCode.IdleWithPendingWork && numStreamPendingMessages == 0)
                            SetState(qpcTimeStamp, SessionStateCode.Idle, "There are no more pending messages");

                        TimeSpan lastDeliveredKeepAliveBufferAge = lastDeliveredKeepAliveBufferTimeStamp.Age(qpcTimeStamp);

                        string terminateSessionReason = string.Empty;
                        TerminationReasonCode terminateSessionReasonCode = TerminationReasonCode.None;

                        if (lastDeliveredKeepAliveBufferAge > Config.SessionExpirationPeriod && !Config.NominalKeepAliveSendInterval.IsZero() && IsClientSession)
                        {
                            terminateSessionReason = "Session timeout: KeepAlive buffer delivery failed after {0:f3} seconds".CheckedFormat(lastDeliveredKeepAliveBufferAge.TotalSeconds);
                            terminateSessionReasonCode = TerminationReasonCode.SessionKeepAliveTimeLimitReached;
                        }
                        else if (StateCode == SessionStateCode.IdleWithPendingWork && stateAge > Config.SessionExpirationPeriod)
                        {
                            terminateSessionReason = "Session timeout: State has been {0} for {1:f3} seconds".CheckedFormat(StateCode, stateAge.TotalSeconds);
                            terminateSessionReasonCode = TerminationReasonCode.SessionPendingWorkTimeLimitReached;
                        }

                        if (terminateSessionReason.IsNeitherNullNorEmpty() || terminateSessionReasonCode != TerminationReasonCode.None)
                        {
                            StateEmitter.Emit("{0} {1} [sending termination message]",terminateSessionReasonCode, terminateSessionReason);

                            GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.NoteSessionTerminated, reason: terminateSessionReason, terminationReasonCode: terminateSessionReasonCode);

                            SetState(qpcTimeStamp, SessionStateCode.Terminated, terminateSessionReason);
                        }
                    }

                    break;

                case SessionStateCode.RequestTransportConnect:
                    break;

                case SessionStateCode.RequestSessionOpen:
                    count += ServiceTransmitter(qpcTimeStamp);

                    if (stateAge > Config.NormalRetransmitHoldoffPeriod)
                    {
                        ManagementType managementType = ManagementType.RequestOpenSession;

                        TouchStateTime(qpcTimeStamp, "Resending {0} after {1:f3} seconds".CheckedFormat(managementType, stateAge.TotalSeconds));

                        GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, managementType, serviceTransmitter: true);

                        count++;
                    }
                    else if (stateAge > Config.ConnectWaitTimeLimit)
                    {
                        SetState(qpcTimeStamp, SessionStateCode.Terminated, "Connection not accepted within {0:f3} seconds [{1}]".CheckedFormat(stateAge.TotalSeconds, StateCode), TerminationReasonCode.ConnectWaitTimeLimitReached);
                        count++;
                    }
                    break;

                case SessionStateCode.CloseRequested:
                    count += ServiceTransmitter(qpcTimeStamp, enableRetransmission: true, enableMessageSending: false);

                    if (count == 0)
                    {
                        TimeSpan txAge = (qpcTimeStamp - lastSendActivityTimeStamp);

                        if (txAge >= Config.ShortRetransmitHoldoffPeriod && stateAge >= Config.ShortRetransmitHoldoffPeriod)
                            GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.RequestCloseSession, reason: "Close request retransmit: {0}".CheckedFormat(State.Reason));

                        if (stateAge > Config.CloseRequestWaitTimeLimit)
                            SetState(qpcTimeStamp, SessionStateCode.Terminated, "Close request not accepted witin {0:f3} seconds [{1}]".CheckedFormat(stateAge.TotalSeconds, StateCode), TerminationReasonCode.CloseRequestWaitTimeLimitReached);
                    }
                    break;

                case SessionStateCode.ConnectionClosed:
                    {
                        SetState(qpcTimeStamp, SessionStateCode.Terminated, "Connection has been closed", TerminationReasonCode.ClosedByRequest);
                        count++;
                    }
                    break;

                case SessionStateCode.None:
                case SessionStateCode.Terminated:
                default:
                    break;
            }

            return count;
        }

        public void NoteTransportIsConnected(QpcTimeStamp qpcTimeStamp, object transportEndpoint)
        {
            transportEndpoint = transportEndpoint ?? TransportEndpoint;

            switch (StateCode)
            {
                case SessionStateCode.RequestTransportConnect:
                    SetState(qpcTimeStamp, SessionStateCode.RequestSessionOpen, "{0}[{1}]".CheckedFormat(Fcns.CurrentMethodName, transportEndpoint));
                    GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.RequestOpenSession, serviceTransmitter: true);
                    break;
                default:
                    TraceEmitter.Emit("{0}[{1}]: ignored in session state {2}", Fcns.CurrentMethodName, transportEndpoint, StateCode);
                    break;
            }
        }

        public void NoteTransportIsClosed(QpcTimeStamp qpcTimeStamp, object transportEndpoint, string failureCode = "")
        {
            SetState(qpcTimeStamp, SessionStateCode.ConnectionClosed, "{0}: {1}".CheckedFormat(Fcns.CurrentMethodName, failureCode));
        }

        private int HandleSessionProtocolViolation(QpcTimeStamp qpcTimeStamp, string failureCode, bool serviceTransmitter = true, TerminationReasonCode terminationReasonCode = TerminationReasonCode.ProtocolViolation)
        {
            IssueEmitter.Emit("{0} State:{1} note {2} failureCode:{3}", SessionName, StateCode, Fcns.CurrentMethodName, failureCode);

            string faultReason = "Session protocol violation: {0}".CheckedFormat(failureCode);

            SetState(qpcTimeStamp, SessionStateCode.Terminated, faultReason, terminationReasonCode);

            // make one attempt to send this message (this likely only works if the serviceTransmitter is passed as true)
            GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.NoteSessionTerminated, reason: faultReason, serviceTransmitter: serviceTransmitter, terminationReasonCode: terminationReasonCode);

            return 1;
        }

        #region Outbound messages - distribution to streams and early buffer preperation

        private class OutboundPerMessageStreamHandler
        {
            public ushort stream;
            public List<Messages.Message> outboundMessageList = new List<Messages.Message>();
            public List<Buffers.Buffer> unpostedBufferList = new List<Buffers.Buffer>();

            public int notifySeqNum = 0;
        }

        private List<OutboundPerMessageStreamHandler> outboundPerMessageStreamHandlerList = new List<OutboundPerMessageStreamHandler>();
        private OutboundPerMessageStreamHandler[] outboundPerMessageStreamHandlerArray = EmptyArrayFactory<OutboundPerMessageStreamHandler>.Instance;

        public void HandleOutboundMessage(QpcTimeStamp qpcTimeStamp, ushort stream, Messages.Message message)
        {
            if (message == null || message.bufferList.IsNullOrEmpty())
                return;     // there is nothing to do.

            if (!State.CanAcceptOutboundMessages)
            {
                message.SetState(qpcTimeStamp, Messages.MessageState.Failed, "{0} {1}: Message not accepted, session state {2} does not support message transfer".CheckedFormat(SessionName, Fcns.CurrentMethodName, State));
                return;
            }

            switch (message.State)
            {
                case Messages.MessageState.Initial:
                case Messages.MessageState.Data:
                    break;
                default:
                    message.SetState(qpcTimeStamp, Messages.MessageState.Failed, "{0} {1}: Message not accepted, message state must be Initial or Data".CheckedFormat(SessionName, Fcns.CurrentMethodName));
                    return;
            }

            Buffers.Buffer firstBadBuffer = message.bufferList.FirstOrDefault(buffer => (!buffer.State.IsReadyToPost() || buffer.header.PurposeCode != PurposeCode.None));
            if (firstBadBuffer != null)
            {
                message.SetState(qpcTimeStamp, Messages.MessageState.Failed, "{0} {1}: Message not accepted, buffer has incorrect state: {2}".CheckedFormat(SessionName, Fcns.CurrentMethodName, firstBadBuffer));
                return;
            }

            string reason = Fcns.CurrentMethodName;

            message.SetState(qpcTimeStamp, Messages.MessageState.SendPosted, reason);

            Buffers.Buffer firstBuffer = message.bufferList[0];
            Buffers.Buffer lastBuffer = message.bufferList[message.bufferList.Count - 1];

            if (firstBuffer == lastBuffer)
            {
                firstBuffer.header.PurposeCode = PurposeCode.Message;
            }
            else
            {
                firstBuffer.header.PurposeCode = PurposeCode.MessageStart;
                lastBuffer.header.PurposeCode = PurposeCode.MessageEnd;
            }

            foreach (var buffer in message.bufferList)
            {
                buffer.header.MessageStream = stream;
                buffer.Message = message;
                buffer.SetState(qpcTimeStamp, BufferState.ReadyToSend, reason);

                if (buffer.header.PurposeCode == PurposeCode.None)
                    buffer.header.PurposeCode = PurposeCode.MessageMiddle;
            }

            OutboundPerMessageStreamHandler streamHandler = outboundPerMessageStreamHandlerArray.SafeAccess(stream);
            if (streamHandler == null)
            {
                int currentMaxStream = outboundPerMessageStreamHandlerArray.Length;

                outboundPerMessageStreamHandlerList.AddRange(Enumerable.Range(currentMaxStream, stream + 1 - currentMaxStream).Select(addingStream => new OutboundPerMessageStreamHandler() { stream = unchecked((ushort)addingStream) }));
                outboundPerMessageStreamHandlerArray = outboundPerMessageStreamHandlerList.ToArray();

                streamHandler = outboundPerMessageStreamHandlerArray[stream];
            }

            streamHandler.outboundMessageList.Add(message);
            streamHandler.unpostedBufferList.AddRange(message.bufferList);

            ServiceTransmitter(qpcTimeStamp);
        }

        private int ServiceOutgoingMessageList(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;
            int deliveredOrFailedCount = 0;

            foreach (var streamHandler in outboundPerMessageStreamHandlerArray)
            {
                List<Messages.Message> outboundMessageList = streamHandler.outboundMessageList;

                foreach (var message in outboundMessageList)
                {
                    if (message.bufferList.IsNullOrEmpty() && message.State != Messages.MessageState.Delivered)
                    {
                        message.SetState(qpcTimeStamp, Messages.MessageState.Failed, "{0}: message no longer has any attached buffers: {1}".CheckedFormat(Fcns.CurrentMethodName, message));
                        deliveredOrFailedCount++;
                    }

                    bool messageHasBeenAssignedLastBufferSeqNum = (message.LastBufferSeqNum != 0);
                    bool checkForBufferInBadState = false;

                    switch (message.State)
                    {
                        case Messages.MessageState.SendPosted:
                            if (!messageHasBeenAssignedLastBufferSeqNum)
                            {
                                checkForBufferInBadState = true;
                            }
                            else if (message.LastBufferSeqNum <= maxDeliveredBufferSeqNum)
                            {
                                message.SetState(qpcTimeStamp, Messages.MessageState.Delivered, "All buffers have been Delivered");
                                EventAndPerformanceRecording.RecordDelivered(message);
                                deliveredOrFailedCount++;
                            }
                            else if (message.LastBufferSeqNum <= maxSentBufferSeqNum)
                            {
                                message.SetState(qpcTimeStamp, Messages.MessageState.Sent, "All buffers are either Sent or Delivered");
                                count++;
                            }
                            else
                            {
                                checkForBufferInBadState = true;
                            }
                            break;

                        case Messages.MessageState.Sent:
                            if (!messageHasBeenAssignedLastBufferSeqNum)
                            {
                                checkForBufferInBadState = true;
                            }
                            else if (message.LastBufferSeqNum <= maxDeliveredBufferSeqNum)
                            {
                                message.SetState(qpcTimeStamp, Messages.MessageState.Delivered, "All buffers have been Delivered");
                                EventAndPerformanceRecording.RecordDelivered(message);
                                deliveredOrFailedCount++;
                            }
                            else
                            {
                                checkForBufferInBadState = true;
                            }
                            break;

                        case Messages.MessageState.Delivered:
                            deliveredOrFailedCount++;
                            break;

                        case Messages.MessageState.Failed:
                            deliveredOrFailedCount++;
                            break;

                        default:
                            message.SetState(qpcTimeStamp, Messages.MessageState.Failed, "{0}: message is not in expected/supported state".CheckedFormat(Fcns.CurrentMethodName));
                            deliveredOrFailedCount++;
                            break;
                    }

                    if (checkForBufferInBadState)
                    {
                        Buffers.Buffer firstBadBuffer = message.bufferList.FirstOrDefault(buffer => ((buffer.State != BufferState.ReadyToSend) && (buffer.State != BufferState.SendPosted) && (buffer.State != BufferState.Sent) && (buffer.State != BufferState.Delivered) && (buffer.State != BufferState.ReadyToResend)));
                        if (firstBadBuffer != null)
                        {
                            message.SetState(qpcTimeStamp, Messages.MessageState.Failed, "{0}: encountered invalid buffer state for message {1}: {2}".CheckedFormat(Fcns.CurrentMethodName, message, firstBadBuffer));
                            deliveredOrFailedCount++;
                        }
                    }
                }

                if (deliveredOrFailedCount > 0)
                {
                    outboundMessageList.RemoveAll(message => (message.State == Messages.MessageState.Delivered || message.State == Messages.MessageState.Failed));

                    count += deliveredOrFailedCount;
                }
            }

            return count;
        }

        Buffers.Buffer pendingKeepAliveBuffer;
        QpcTimeStamp pendingKeepAliveBufferSendTimeStamp = QpcTimeStamp.Now;
        public QpcTimeStamp lastDeliveredKeepAliveBufferTimeStamp = QpcTimeStamp.Now;

        private int ServiceSendingKeepAliveBuffers(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            if (IsClientSession && State.IsConnected())
            {
                if (pendingKeepAliveBuffer != null && !pendingKeepAliveBuffer.State.IsDeliveryPending())
                {
                    var elapsedDeliveryTime = pendingKeepAliveBufferSendTimeStamp.Age(qpcTimeStamp).TotalSeconds;

                    if (pendingKeepAliveBuffer.State == BufferState.Delivered)
                    {
                        lastDeliveredKeepAliveBufferTimeStamp = qpcTimeStamp;
                        TraceEmitter.Emit("KeepAlive buffer delivered [{0}, after:{1:f3} sec]", pendingKeepAliveBuffer, elapsedDeliveryTime);
                        pendingKeepAliveBuffer.ReturnToPool(qpcTimeStamp, "keep alive delivery complete");
                    }
                    else
                    {
                        IssueEmitter.Emit("KeepAlive buffer was not delivered normally [{0}, after:{1:f3} sec]", pendingKeepAliveBuffer, elapsedDeliveryTime);
                        pendingKeepAliveBuffer.ReturnToPool(qpcTimeStamp, "keep alive delivery failed");
                    }

                    pendingKeepAliveBuffer = null;

                    count++;
                }

                if (pendingKeepAliveBuffer == null && (pendingKeepAliveBufferSendTimeStamp.Age(qpcTimeStamp) >= Config.NominalKeepAliveSendInterval) && !Config.NominalKeepAliveSendInterval.IsZero())
                {
                    TraceEmitter.Emit("Sending KeepAlive buffer");

                    pendingKeepAliveBuffer = GenerateAndAddManagementBufferToReadyToSendList(qpcTimeStamp, ManagementType.KeepAlive, reason: "nominal keep alive send interval reached", assignSeqNum: true);

                    pendingKeepAliveBufferSendTimeStamp = qpcTimeStamp;

                    count++;
                }
            }

            return count;
        }

        #endregion

        #region Inbound stream based Message accumulation and delivery

        private class InboundPerMessageStreamHandler
        {
            public ushort stream;
            public List<Buffers.Buffer> bufferAccumulationList = new List<Buffers.Buffer>();
            public bool waitingForMessageBoundary = false;
            public int lastBufferAccumulationListCount = 0;
            public QpcTimeStamp lastActivityMessageTimeStamp = QpcTimeStamp.Zero;
        }

        private List<InboundPerMessageStreamHandler> inboundPerMessageStreamHandlerList = new List<InboundPerMessageStreamHandler>();
        private InboundPerMessageStreamHandler [] inboundPerMessageStreamHandlerArray = EmptyArrayFactory<InboundPerMessageStreamHandler>.Instance;
        private QpcTimeStamp lastDeliveredMessageTimeStamp = QpcTimeStamp.Now;

        private void AddBufferToMessageAccumulation(QpcTimeStamp qpcTimeStamp, Buffers.Buffer buffer)
        {
            var stream = buffer.header.MessageStream;

            InboundPerMessageStreamHandler streamHandler = inboundPerMessageStreamHandlerArray.SafeAccess(stream);

            if (streamHandler == null)
            {
                int currentMaxStream = inboundPerMessageStreamHandlerArray.Length;
                inboundPerMessageStreamHandlerList.AddRange(Enumerable.Range(currentMaxStream, stream + 1 - currentMaxStream).Select(addingStream => new InboundPerMessageStreamHandler() { stream = unchecked((ushort)addingStream) }));
                inboundPerMessageStreamHandlerArray = inboundPerMessageStreamHandlerList.ToArray();

                streamHandler = inboundPerMessageStreamHandlerArray[stream];
            }

            streamHandler.bufferAccumulationList.Add(buffer);
            streamHandler.lastActivityMessageTimeStamp = qpcTimeStamp;

            if (streamHandler.waitingForMessageBoundary && (buffer.PurposeCode != PurposeCode.MessageMiddle))
                streamHandler.waitingForMessageBoundary = false;
        }

        private int ServiceMessageAccumulationAndDelivery(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            foreach (var streamHandler in inboundPerMessageStreamHandlerArray)
            {
                var stream = streamHandler.stream;
                List<Buffers.Buffer> inboundBufferAccumulationList = streamHandler.bufferAccumulationList;
                int listCount = inboundBufferAccumulationList.Count;

                // this method only runs if the length of the inboundBufferAccumulationList changes length since the last time we evaluated its contents.
                if (streamHandler.lastBufferAccumulationListCount == listCount)
                    continue;

                while (listCount > 0 && !streamHandler.waitingForMessageBoundary)
                {
                    Buffers.Buffer firstBuffer = inboundBufferAccumulationList[0];
                    BufferHeaderV1 firstBufferHeader = firstBuffer.header;
                    PurposeCode firstBufferPurpose = firstBuffer.header.PurposeCode;

                    int runLength = 0;

                    if (firstBufferPurpose == PurposeCode.Message)
                    {
                        runLength = 1;
                    }
                    else if (firstBufferPurpose == PurposeCode.MessageStart)
                    {
                        for (int trialRunLength = 2; trialRunLength <= listCount; )
                        {
                            Buffers.Buffer nextBuffer = inboundBufferAccumulationList[trialRunLength - 1];
                            if (nextBuffer.PurposeCode == PurposeCode.MessageEnd)
                            {
                                runLength = trialRunLength;
                                break;
                            }
                            else if (nextBuffer.PurposeCode == PurposeCode.MessageMiddle)
                            {
                                trialRunLength++;
                            }
                            else
                            {
                                count += HandleSessionProtocolViolation(qpcTimeStamp, "{0}: found non-middle of message buffer in stream {1} accumulation list: {2}".CheckedFormat(Fcns.CurrentMethodName, stream, nextBuffer));
                                break;
                            }
                        }

                        if (runLength == 0)
                            streamHandler.waitingForMessageBoundary = true;
                    }
                    else
                    {
                        count += HandleSessionProtocolViolation(qpcTimeStamp, "{0}: found non-start of message buffer at head of stream {1} accumulation list: {2}".CheckedFormat(Fcns.CurrentMethodName, stream, firstBuffer));
                        count++;
                    }

                    if (runLength > 0)
                    {
                        Messages.Message receivedMessage = new Messages.Message(bufferSourcePool: BufferPool, stateEmitter: TraceEmitter, issueEmitter: IssueEmitter).SetState(qpcTimeStamp, Messages.MessageState.Received, Fcns.CurrentMethodName);

                        EventAndPerformanceRecording.RecordReceived(receivedMessage);

                        receivedMessage.bufferList.AddRange(inboundBufferAccumulationList.Take(runLength));
                        inboundBufferAccumulationList.RemoveRange(0, runLength);

                        if (streamHandler.waitingForMessageBoundary)
                            streamHandler.waitingForMessageBoundary = false;

                        HandleMessageDelegate handleInboundMessageDelegate = HandleInboundMessageDelegate;
                        if (handleInboundMessageDelegate != null)
                        {
                            handleInboundMessageDelegate(qpcTimeStamp, stream, receivedMessage);
                            streamHandler.lastActivityMessageTimeStamp = qpcTimeStamp;
                            lastDeliveredMessageTimeStamp = qpcTimeStamp;

                            count++;
                        }
                        else
                        {
                            count += HandleSessionProtocolViolation(qpcTimeStamp, "{0}: could not deliver message on stream {1}, HandleInboundMessageDelegate is null: {2}".CheckedFormat(Fcns.CurrentMethodName, stream, receivedMessage));
                            receivedMessage.ReturnBuffersToPool(qpcTimeStamp);
                        }

                        listCount = inboundBufferAccumulationList.Count;
                    }
                    else
                    {
                        // no buffers were removed on this iteration - so do not keep working on this stream right now.
                        break;
                    }
                } 

                streamHandler.lastBufferAccumulationListCount = listCount;

                if (listCount > 0)
                {
                    TimeSpan lastActivityAge = (qpcTimeStamp - streamHandler.lastActivityMessageTimeStamp);
                    if (lastActivityAge > Config.SessionExpirationPeriod)
                        count += HandleSessionProtocolViolation(qpcTimeStamp, "{0}: stream {1} message accumulation appears to be stuck [buffer count:{2}, age:{3:f3}]".CheckedFormat(Fcns.CurrentMethodName, stream, listCount, lastActivityAge.TotalSeconds));
                }
            }

            return count;
        }

        #endregion

        #region Transmitter

        QpcTimeStamp lastSendActivityTimeStamp;
        ulong maxSendPostedBufferSeqNum = 0;
        ulong maxSentBufferSeqNum = 0;
        ulong maxDeliveredBufferSeqNum = 0;

        QpcTimeStamp sendBufferAckSeqNumAfterTimeStamp;
        ulong bufferAckSeqNumToSend = 0;
        ulong maxSentBufferAckSeqNum = 0;

        private ulong bufferSeqNumGen = 0;

        int nextSourceSession = 0;
        List<Buffers.Buffer> readyToSendList = new List<Buffers.Buffer>();
        List<Buffers.Buffer> deliveryPendingList = new List<Buffers.Buffer>();
        List<Buffers.Buffer> sendNowList = new List<Buffers.Buffer>();

        private class NotifyOnBufferSetState : INotifyable
        {
            public bool HasBeenNotified { get; private set; }

            void INotifyable.Notify() 
            {
                if (!HasBeenNotified)
                    HasBeenNotified = true; 
            }

            public void Clear() { HasBeenNotified = false; }
        }

        private NotifyOnBufferSetState notifyOnBufferSetState = new NotifyOnBufferSetState();

        private int ServiceTransmitter(QpcTimeStamp qpcTimeStamp, bool enableRetransmission = true, bool enableMessageSending = true, bool requestSendAckNow = false)
        {
            int count = 0;

            if (bufferAckSeqNumToSend != lastRecvdValidBufferSeqNum)
                bufferAckSeqNumToSend = lastRecvdValidBufferSeqNum;

            if (notifyOnBufferSetState.HasBeenNotified)
            {
                notifyOnBufferSetState.Clear();
                
                int numDelivered = 0;

                /// Consider adding global buffer state change counts so that 
                foreach (var buffer in deliveryPendingList)
                {
                    bool bufferHasBeenDelivered = (buffer.State == BufferState.Delivered);
                    if (maxSentBufferSeqNum < buffer.SeqNum && (bufferHasBeenDelivered || buffer.State == BufferState.Sent))    // look at both cases as TCP stream moves some buffers directly to delivered (skipping sent state)
                    {
                        maxSentBufferSeqNum = buffer.SeqNum;
                        maxSentBufferAckSeqNum = Math.Max(maxSentBufferAckSeqNum, buffer.header.AckSeqNum);
                        count++;
                    }

                    if (bufferHasBeenDelivered)
                        numDelivered++;
                }

                if (numDelivered > 0)
                {
                    count += numDelivered;

                    deliveryPendingList.FilterAndRemove(buffer => (buffer.State == BufferState.Delivered)).DoForEach(buffer => EventAndPerformanceRecording.RecordDelivered(buffer));
                }
            }

            if (enableRetransmission && !transportIsReliable)
            {
                /// check for need to send a status update message about out of order buffers.
                if (!firstOutOfOrderBufferReceivedTimeStamp.IsZero && ((qpcTimeStamp - firstOutOfOrderBufferReceivedTimeStamp) >= Config.ShortRetransmitHoldoffPeriod))
                {
                    firstOutOfOrderBufferReceivedTimeStamp = qpcTimeStamp + Config.ShortRetransmitHoldoffPeriod;        // holdoff the next one by 2 times the short retransmit holdoff unless we consume some or add more to the list

                    NamedValueSet nvs = new NamedValueSet() 
                    { 
                        { Constants.HeldBufferSeqNumsAttributeName, heldOutOfOrderBuffersList.Values.Take(Config.MaxHeldBufferSeqNumsToIncludeInStatusUpdate).Select(buffer => buffer.SeqNum).ToArray() } 
                    };

                    GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.Status, nvs, serviceTransmitter: false);
                }

                /// implement timer based retransmission. (move selected buffers from Sent to ReadyToResend)

                QpcTimeStamp shortResendThreshold = qpcTimeStamp - Config.ShortRetransmitHoldoffPeriod;
                QpcTimeStamp normalResendThreshold = qpcTimeStamp - Config.NormalRetransmitHoldoffPeriod;

                if (outOfOrderPossibleMissingBufferArray != null)
                {
                    if (outOfOrderPossibleMissingBufferArray.Any(buffer => buffer.State == BufferState.Sent))
                    {
                        Buffers.Buffer[] addToSendNowSetArray = outOfOrderPossibleMissingBufferArray.Where(buffer => (buffer.SeqNum > lastRecvdValidAckBufferSeqNum) && (buffer.State == BufferState.Sent) && (buffer.TimeStamp < shortResendThreshold)).ToArray();

                        TraceEmitter.Emit("Triggering quick resend (ooo + time) for: {0} [of {1}]", String.Join(",", addToSendNowSetArray.Select(buffer => buffer.BufferName)), outOfOrderPossibleMissingBufferArray.Length);

                        foreach (var buffer in addToSendNowSetArray)
                            sendNowList.Add(buffer.SetState(qpcTimeStamp, BufferState.ReadyToResend, "Quick Resend: not in recent HeldBufferSeqNums status").Update(ackSeqNum: bufferAckSeqNumToSend, orInFlags: BufferHeaderFlags.BufferIsBeingResent));

                        outOfOrderPossibleMissingBufferArray = null;
                    }
                }

                Buffers.Buffer firstCandidateResendBuffer = deliveryPendingList.FirstOrDefault(buffer => buffer.SeqNum > lastRecvdValidAckBufferSeqNum);        // this usually gets the first buffer in the list

                // we do not do any resending if there is no first candidate buffer or it has not been Sent yet.
                if ((firstCandidateResendBuffer != null) && (firstCandidateResendBuffer.State == BufferState.Sent))
                {
                    if ((firstCandidateResendBuffer.SeqNum == (lastRecvdValidAckBufferSeqNum + 1)) && (firstCandidateResendBuffer.TimeStamp < shortResendThreshold))
                    {
                        TraceEmitter.Emit("Triggering quick resend (next seq num + time) for: {0}", firstCandidateResendBuffer);

                        sendNowList.Add(firstCandidateResendBuffer.SetState(qpcTimeStamp, BufferState.ReadyToResend, "Quick Resend: not in recent HeldBufferSeqNums status").Update(ackSeqNum: bufferAckSeqNumToSend, orInFlags: BufferHeaderFlags.BufferIsBeingResent));
                    }
                    else if (firstCandidateResendBuffer.TimeStamp < normalResendThreshold)
                    {
                        // collect and trigger resend on all of the related buffers in the outgoingBuffersList
                        Buffers.Buffer[] addToSendNowSetArray = deliveryPendingList.Where(buffer => (buffer.SeqNum > lastRecvdValidAckBufferSeqNum) && (buffer.State == BufferState.Sent) && (buffer.TimeStamp < normalResendThreshold)).ToArray();

                        TraceEmitter.Emit("Triggering normal resend (time) for: {0} [of {1}]", String.Join(",", addToSendNowSetArray.Select(buffer => buffer.BufferName)), outOfOrderPossibleMissingBufferArray.SafeLength());

                        foreach (var resendBuffer in addToSendNowSetArray)
                            sendNowList.Add(resendBuffer.SetState(qpcTimeStamp, BufferState.ReadyToResend, "Resend: after normal time delay").Update(ackSeqNum: bufferAckSeqNumToSend, orInFlags: BufferHeaderFlags.BufferIsBeingResent));
                    }
                }
            }

            // implement burst oriented, round robin stream prioritization scheme

            if (State.CanAcceptOutboundMessages && enableMessageSending)
            {
                int maxReadyToSendListCount = Math.Max(0, Config.MaxBufferWriteAheadCount - deliveryPendingList.Count);

                while (readyToSendList.Count < maxReadyToSendListCount)
                {
                    // move buffers from the outbound message stream handlers into the readyToSendList
                    var streamHandler = outboundPerMessageStreamHandlerArray.SafeAccess(nextSourceSession++);
                    if (streamHandler == null)
                        break;

                    int bufferCountToPostFromThisStream = Math.Min(maxReadyToSendListCount - readyToSendList.Count, streamHandler.unpostedBufferList.Count);

                    if (bufferCountToPostFromThisStream > 0)
                    {
                        Buffers.Buffer[] moveFromStreamToReadyToSendBuffersArray = streamHandler.unpostedBufferList.Take(bufferCountToPostFromThisStream).Select(buffer => buffer.Update(seqNum: ++bufferSeqNumGen)).ToArray();

                        readyToSendList.AddRange(moveFromStreamToReadyToSendBuffersArray);

                        streamHandler.unpostedBufferList.RemoveRange(0, bufferCountToPostFromThisStream);
                    }
                }

                if (nextSourceSession >= outboundPerMessageStreamHandlerArray.Length)
                    nextSourceSession = 0;
            }

            if (enableMessageSending && readyToSendList.Count > 0)
            {
                // implement basic forward "window" based initial transmission of buffers that are in a ReadyToSend state.
                int readyToSendTakeCount = Math.Min(readyToSendList.Count, Math.Max(0, Config.MaxBufferWriteAheadCount - (sendNowList.Count + deliveryPendingList.Count)));

                if (readyToSendTakeCount > 0)
                {
                    Buffers.Buffer[] addToSendNowSetArray = readyToSendList.Take(readyToSendTakeCount).ToArray();

                    readyToSendList.RemoveRange(0, readyToSendTakeCount);

                    foreach (var buffer in addToSendNowSetArray)
                        buffer.Update(notifyOnSetState: notifyOnBufferSetState);

                    sendNowList.AddRange(addToSendNowSetArray);
                    deliveryPendingList.AddRange(addToSendNowSetArray);
                }
            }

            // if there is nothing else to send and there is a new buffer seq num to acknowledge and enough time has elpased then send a simple ack buffer.
            if (sendNowList.Count == 0 && (bufferAckSeqNumToSend != maxSentBufferAckSeqNum) && (requestSendAckNow || (!sendBufferAckSeqNumAfterTimeStamp.IsZero && qpcTimeStamp >= sendBufferAckSeqNumAfterTimeStamp)))
            {
                Buffers.Buffer explicitAckBuffer = BufferPool.Acquire(qpcTimeStamp).Update(purposeCode: PurposeCode.Ack).SetState(qpcTimeStamp, BufferState.ReadyToSend, "Sending explicit ack");
                sendNowList.Add(explicitAckBuffer);

                TraceEmitter.Emit("Sending explicit ack: {0}", explicitAckBuffer);
            }

            // if there is anything to send now then send it.
            if (sendNowList.Count > 0)
            {
                Buffers.Buffer[] sendNowArray = sendNowList.ToArray();

                foreach (var buffer in sendNowArray)
                {
                    buffer.Update(ackSeqNum: bufferAckSeqNumToSend);

                    // move ReadyToSend buffers to SendPosted early so that notify will not be performed on this state change.  transport will not make state change again since bufferes will already be send posted.
                    if (buffer.State == BufferState.ReadyToSend)
                    {
                        buffer.SetState(qpcTimeStamp, BufferState.SendPosted, "Session is posting send");
                        buffer.NotifyOnSetState = notifyOnBufferSetState;

                        if (maxSendPostedBufferSeqNum <= buffer.SeqNum)
                            maxSendPostedBufferSeqNum = buffer.SeqNum;
                    }
                }

                HandleBuffersDelegate handleOutboundBuffersDelegate = HandleOutboundBuffersDelegate;

                if (handleOutboundBuffersDelegate != null)
                    handleOutboundBuffersDelegate(qpcTimeStamp, TransportEndpoint, sendNowArray);
                else
                    SetState(qpcTimeStamp, SessionStateCode.Terminated, "Attempt to send before HandleOutboundBuffersDelegate has been provided");

                EventAndPerformanceRecording.RecordSent(sendNowArray);

                sendNowList.Clear();

                if (!sendBufferAckSeqNumAfterTimeStamp.IsZero)
                    sendBufferAckSeqNumAfterTimeStamp = QpcTimeStamp.Zero;

                count++;
            }

            if (count > 0)
                lastSendActivityTimeStamp = qpcTimeStamp;

            return count;
        }

        Buffers.Buffer [] outOfOrderPossibleMissingBufferArray = null;

        private int ProcessTransmitterAspectsOfStatusUpdate(QpcTimeStamp qpcTimeStamp, INamedValueSet statusNVS)
        {
            int count = 0;

            // note: the following will produce a null array if the corresponding attribute is not pressent.
            ulong[] heldBuffersSeqNumsArray = statusNVS[Constants.HeldBufferSeqNumsAttributeName].VC.GetValue<ulong[]>(rethrow: false);

            if (!heldBuffersSeqNumsArray.IsNullOrEmpty())
            {
                ulong lastHeldSeqNum = heldBuffersSeqNumsArray.Last();
                outOfOrderPossibleMissingBufferArray = deliveryPendingList.Where(buffer => buffer.SeqNum < lastHeldSeqNum && !heldBuffersSeqNumsArray.Contains(buffer.SeqNum)).ToArray().MapEmptyToNull();

                if (outOfOrderPossibleMissingBufferArray != null)
                    count++;
            }

            return count;
        }

        public static Buffers.Buffer GenerateManagementBuffer(QpcTimeStamp qpcTimeStamp, ManagementType managementType, string hostName, string clientUUID, ulong clientInstanceNum, NamedValueSet nvs, string reason, Transport.TransportRole transportRole, BufferPool bufferPool, bool setState, TerminationReasonCode terminationReasonCode = TerminationReasonCode.None)
        {
            nvs = nvs.ConvertToWritable();

            nvs.SetValue("Type", managementType);
            nvs.ConditionalSetValue("Reason", reason != null, reason);

            if (managementType != ManagementType.Status)
            {
                if (!nvs.Contains("Name"))
                    nvs.SetValue("Name", hostName);

                if (!nvs.Contains("ClientUUID"))
                    nvs.SetValue("ClientUUID", clientUUID);

                if (!nvs.Contains("ClientInstanceNum"))
                    nvs.SetValue("ClientInstanceNum", clientInstanceNum);

                if (!nvs.Contains("BufferSize"))
                    nvs.SetValue("BufferSize", bufferPool.BufferSize);
            }

            if (terminationReasonCode != TerminationReasonCode.None)
                nvs.SetValue("TerminationReason", terminationReasonCode);

            Buffers.Buffer managementBuffer = bufferPool.Acquire(qpcTimeStamp).Update(purposeCode: PurposeCode.Management, buildPayloadDataFromE005NVS: nvs);

            if (setState)
               managementBuffer.SetState(qpcTimeStamp, BufferState.ReadyToSend, "generated management buffer {0}".CheckedFormat(nvs.SafeToStringSML()));

            return managementBuffer;
        }

        public Buffers.Buffer GenerateAndAddManagementBufferToSendNowList(QpcTimeStamp qpcTimeStamp, ManagementType managementType, NamedValueSet nvs = null, bool serviceTransmitter = true, string reason = null, TerminationReasonCode terminationReasonCode = TerminationReasonCode.None)
        {
            Buffers.Buffer managementBuffer = GenerateManagementBuffer(qpcTimeStamp, managementType, HostName, ClientUUID, ClientInstanceNum, nvs, reason, TransportRole, BufferPool, setState: false, terminationReasonCode: terminationReasonCode);

            managementBuffer.SetState(qpcTimeStamp, BufferState.ReadyToSend, "Adding management buffer to send now list {0}".CheckedFormat(nvs.SafeToStringSML()));

            sendNowList.Add(managementBuffer);

            EventAndPerformanceRecording.RecordSending(managementType);

            if (serviceTransmitter)
                ServiceTransmitter(qpcTimeStamp, enableRetransmission: false, enableMessageSending: false);

            return managementBuffer;
        }

        public Buffers.Buffer GenerateAndAddManagementBufferToReadyToSendList(QpcTimeStamp qpcTimeStamp, ManagementType managementType, NamedValueSet nvs = null, bool serviceTransmitter = true, string reason = null, bool assignSeqNum = true, TerminationReasonCode terminationReasonCode = TerminationReasonCode.None)
        {
            Buffers.Buffer managementBuffer = GenerateManagementBuffer(qpcTimeStamp, managementType, HostName, ClientUUID, ClientInstanceNum, nvs, reason, TransportRole, BufferPool, setState: false, terminationReasonCode: terminationReasonCode);

            managementBuffer.SetState(qpcTimeStamp, BufferState.ReadyToSend, "Adding management buffer to send now list {0}".CheckedFormat(nvs.SafeToStringSML()));

            if (assignSeqNum)
                managementBuffer.Update(seqNum: ++bufferSeqNumGen);

            readyToSendList.Add(managementBuffer);

            EventAndPerformanceRecording.RecordSending(managementType);

            if (serviceTransmitter)
                ServiceTransmitter(qpcTimeStamp, enableRetransmission: false, enableMessageSending: false);

            return managementBuffer;
        }

        #endregion

        #region Receiver

        QpcTimeStamp lastRecvActivityTimeStamp;

        public void HandleInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray)
        {
            int count = 0;

            lastRecvActivityTimeStamp = qpcTimeStamp;

            EventAndPerformanceRecording.RecordReceived(bufferParamsArray);

            bool messageEndReceived = false;

            foreach (var buffer in bufferParamsArray)
            {
                count += ProcessReceivedBufferAck(qpcTimeStamp, buffer);
                count += ProcessReceivedBuffer(qpcTimeStamp, buffer);

                messageEndReceived |= (buffer.PurposeCode == PurposeCode.Message || buffer.PurposeCode == PurposeCode.MessageEnd);
            }

            count += ServiceMessageAccumulationAndDelivery(qpcTimeStamp);

            if (messageEndReceived)
                count += ServiceTransmitter(qpcTimeStamp, enableRetransmission: false, enableMessageSending: false, requestSendAckNow: true);
        }

        /// <summary>
        /// This defines the range of valid reception ack sequence numbers around the most recently received valid one.
        /// Any non-resent buffer who's ack seq num is outside of this range is a session protocol violation.
        /// (10000)
        /// </summary>
        public const ulong MaxAcceptableAckWindowWidth = 10000;

        ulong lastRecvdValidAckBufferSeqNum = 0;
        QpcTimeStamp lastRecvdValidAckBufferSeqNumTimeStamp = QpcTimeStamp.Now;

        private int ProcessReceivedBufferAck(QpcTimeStamp qpcTimeStamp, Buffers.Buffer rxBuffer)
        {
            int count = 0;

            ulong ackBufferSeqNum = rxBuffer.header.AckSeqNum;

            // buffers with a zero for the ack seq num or which came before the buffer that gave us the lastRecvdValidAckBufferSeqNum will be ignored (even if they have not been resent - aka they arrived out of order)
            if (ackBufferSeqNum == 0 || (ackBufferSeqNum <= lastRecvdValidAckBufferSeqNum))
                return count;

            if (ackBufferSeqNum - lastRecvdValidAckBufferSeqNum > MaxAcceptableAckWindowWidth)
            {
                count += HandleSessionProtocolViolation(qpcTimeStamp, "{0}: Buffer's ack seq num {1} is out of valid range {2}..{3}: {4}".CheckedFormat(Fcns.CurrentMethodName, ackBufferSeqNum, lastRecvdValidAckBufferSeqNum, lastRecvdValidAckBufferSeqNum + MaxAcceptableAckWindowWidth, rxBuffer));
                return count;
            }

            ulong entryLastRecvdValidAckBufferSeqNum = lastRecvdValidAckBufferSeqNum;
            lastRecvdValidAckBufferSeqNum = ackBufferSeqNum;
            lastRecvdValidAckBufferSeqNumTimeStamp = qpcTimeStamp;

            string reason = Fcns.CurrentMethodName;

            foreach (var buffer in deliveryPendingList.FilterAndRemove(txBuffer => txBuffer.SeqNum <= lastRecvdValidAckBufferSeqNum))
            {
                buffer.SetState(qpcTimeStamp, BufferState.Delivered, reason);

                EventAndPerformanceRecording.RecordDelivered(buffer);

                maxSentBufferSeqNum = Math.Max(buffer.SeqNum, maxSentBufferSeqNum);
                maxSentBufferAckSeqNum = Math.Max(maxSentBufferAckSeqNum, buffer.header.AckSeqNum);

                count++;
            }

            if (count > 0 && maxDeliveredBufferSeqNum < lastRecvdValidAckBufferSeqNum)
            {
                maxDeliveredBufferSeqNum = lastRecvdValidAckBufferSeqNum;
                if (maxSentBufferSeqNum < maxDeliveredBufferSeqNum)
                    maxSentBufferSeqNum = maxDeliveredBufferSeqNum;
            }

            TraceEmitter.Emit("Ack advance {0} -> {1} marked {2} buffers as delivered", entryLastRecvdValidAckBufferSeqNum, ackBufferSeqNum, count);

            return count;
        }

        ulong lastRecvdValidBufferSeqNum = 0;

        private int ProcessReceivedBuffer(QpcTimeStamp qpcTimeStamp, Buffers.Buffer buffer)
        {
            ulong bufferSeqNum = buffer.SeqNum;

            // decide what kind of buffer this is
            SelectedBufferHandling bufferHandling;

            if (buffer.PurposeCode == PurposeCode.Ack) // these buffers are used for expicit acks
                bufferHandling = SelectedBufferHandling.BufferIsAckOnly;
            else if (State.IsPerminantlyClosed)
                bufferHandling = SelectedBufferHandling.SessionTerminated;
            else if (bufferSeqNum == 0)
                bufferHandling = SelectedBufferHandling.BufferIsSeqNumZero;
            else if (!State.IsConnected(includeConnectingStates: false, includeClosingStates: true))
                bufferHandling = SelectedBufferHandling.NotConnectedYet;
            else if (bufferSeqNum == (lastRecvdValidBufferSeqNum + 1) || (lastRecvdValidBufferSeqNum == 0))
                bufferHandling = SelectedBufferHandling.BufferIsInNormalOrder;
            else if (bufferSeqNum > lastRecvdValidBufferSeqNum)
                bufferHandling = SelectedBufferHandling.BufferIsOutOfOrderInTheFuture;
            else
                bufferHandling = SelectedBufferHandling.BufferIsOutOfOrderInThePast;

            int count = 0;

            switch (bufferHandling)
            {
                case SelectedBufferHandling.BufferIsAckOnly:
                    TraceEmitter.Emit("Received explicit ack: {0}", buffer);
                    break;

                case SelectedBufferHandling.BufferIsSeqNumZero:

                    if (buffer.PurposeCode == PurposeCode.Management)
                        count += ProcessReceivedManagementBuffer(qpcTimeStamp, buffer);
                    else
                        count += HandleSessionProtocolViolation(qpcTimeStamp, "Recieved seqNumZero buffer and unsupported purpose. [state:{0} buffer:{1}]".CheckedFormat(State, buffer));

                    break;

                case SelectedBufferHandling.NotConnectedYet:
                    if (buffer.PurposeCode == PurposeCode.Management)
                        count += ProcessReceivedManagementBuffer(qpcTimeStamp, buffer);
                    else
                        count += HandleSessionProtocolViolation(qpcTimeStamp, "Recieved buffer and unsupported purpose while not connected. [state:{0} buffer:{1}]".CheckedFormat(State, buffer));

                    break;

                case SelectedBufferHandling.BufferIsInNormalOrder:
                    lastRecvdValidBufferSeqNum = bufferSeqNum;

                    if (sendBufferAckSeqNumAfterTimeStamp.IsZero)
                        sendBufferAckSeqNumAfterTimeStamp = qpcTimeStamp + Config.ExplicitAckHoldoffPeriod;

                    count += ProcessInOrderOrNoSeqNumBufferReceived(qpcTimeStamp, buffer);

                    if (heldOutOfOrderBuffersList.Count > 0)
                        count += ServiceHeldOutOfOrderBufferList(qpcTimeStamp, checkForStaleBuffers: false);

                    break;

                case SelectedBufferHandling.BufferIsOutOfOrderInTheFuture:
                    if (firstOutOfOrderBufferReceivedTimeStamp.IsZero || firstOutOfOrderBufferReceivedTimeStamp > qpcTimeStamp)
                        firstOutOfOrderBufferReceivedTimeStamp = qpcTimeStamp;

                    if (heldOutOfOrderBuffersList.Count < Config.MaxOutOfOrderBufferHoldCount)
                    {
                        TraceEmitter.Emit("Received out of order buffer {0}", buffer);

                        // out of order buffers that have sequence numbers will be saved for later processing or discard based on later reception of in order buffers.  This logic now includes processing of management buffers with sequence numbrers.
                        heldOutOfOrderBuffersList[bufferSeqNum] = buffer;

                        EventAndPerformanceRecording.RecordEvent(RecordEventType.BufferReceivedOutOfOrder);
                    }
                    else
                    {
                        TraceEmitter.Emit("Could not retain received out of order buffer {0}: held buffer list has reached maximum capacity [{1} >= {2}]", buffer, heldOutOfOrderBuffersList.Count, Config.MaxOutOfOrderBufferHoldCount);
                    }                    
                    count++;

                    break;

                case SelectedBufferHandling.BufferIsOutOfOrderInThePast:
                    if ((buffer.Flags & BufferHeaderFlags.BufferIsBeingResent) != 0)
                    {
                        TraceEmitter.Emit("Resent buffer had already been received [{0}]", buffer);
                        EventAndPerformanceRecording.RecordEvent(RecordEventType.OldResentBufferRecieved);
                    }
                    else
                    {
                        IssueEmitter.Emit("Non-Resent buffer appears to have already been received [{0}]", buffer);
                        EventAndPerformanceRecording.RecordEvent(RecordEventType.OldBufferReceivedOutOfOrder);
                    }
                    count++;

                    break;

                case SelectedBufferHandling.SessionTerminated:
                    TraceEmitter.Emit("received message buffer while session is terminated [state:{0} buffer:{1}]", State, buffer);
                    count++;
                    break;

                default:
                    break;
            }

            return count;
        }

        private enum SelectedBufferHandling : int
        {
            /// <summary>Buffer received Ack only buffer.  Normally these also have a zero seq num.</summary>
            BufferIsAckOnly,
            /// <summary>Buffer received with a non</summary>
            BufferIsSeqNumZero,
            /// <summary>Buffer received with a non-zero seq num before we have been connected.  This can only be a management buffer or an Ack.</summary>
            NotConnectedYet,
            /// <summary>Buffer has been received with a seqNum that is exactly one beyond the last valid received seq num</summary>
            BufferIsInNormalOrder,
            /// <summary>Buffer recieved with a seqNum that is more than one beyond the last valid received seq num</summary>
            BufferIsOutOfOrderInTheFuture,
            /// <summary>Buffer recieved with a seqNum that has already been received validly (in order) - this is usually due to unnecessary retransmit</summary>
            BufferIsOutOfOrderInThePast,
            /// <summary>Buffer received when session has already been terminated (aka Perminantly closed)</summary>
            SessionTerminated,
        }

        private int ProcessInOrderOrNoSeqNumBufferReceived(QpcTimeStamp qpcTimeStamp, Buffers.Buffer buffer)
        {
            int count = 0;

            PurposeCode bufferPurposeCode = buffer.PurposeCode;

            switch (bufferPurposeCode)
            {
                case PurposeCode.Ack:      // these are used for expicit acks
                    TraceEmitter.Emit("Received explicit ack: {0}", buffer);
                    break;

                case PurposeCode.Management:
                    count += ProcessReceivedManagementBuffer(qpcTimeStamp, buffer);
                    break;

                case PurposeCode.Message:
                case PurposeCode.MessageStart:
                case PurposeCode.MessageMiddle:
                case PurposeCode.MessageEnd:
                    AddBufferToMessageAccumulation(qpcTimeStamp, buffer);
                    count++;
                    break;

                default:
                    count += HandleSessionProtocolViolation(qpcTimeStamp, "Recieved buffer with unsupported purpose: {1}".CheckedFormat(State, buffer));
                    break;
            }

            return count;
        }

        private int ProcessReceivedManagementBuffer(QpcTimeStamp qpcTimeStamp, Buffers.Buffer buffer)
        {
            int count = 0;

            TraceEmitter.Emit("Received management buffer: {0}", buffer);

            INamedValueSet bufferNVS = buffer.GetPayloadAsE005NVS(NamedValueSet.Empty);
            ManagementType managementType = bufferNVS["Type"].VC.GetValue<ManagementType>(rethrow: false);
            string remoteName = bufferNVS["Name"].VC.GetValue<string>(rethrow: false);
            string clientUUID = bufferNVS["ClientUUID"].VC.GetValue<string>(rethrow: false);
            ulong clientInstanceNum = bufferNVS["ClientInstanceNum"].VC.GetValue<ulong>(rethrow: false);

            EventAndPerformanceRecording.RecordReceived(managementType);

            switch (managementType)
            {
                case ManagementType.RequestOpenSession:
                    switch (State.StateCode)
                    {
                        case SessionStateCode.ServerSessionInitial:
                            count += ProcessServerSessionInitialOpen(qpcTimeStamp, remoteName, clientUUID, clientInstanceNum, bufferNVS);
                            break;
                        default:
                            if ((ClientUUID != clientUUID || ClientInstanceNum != clientInstanceNum) && !State.IsConnected())
                                count += HandleSessionProtocolViolation(qpcTimeStamp, "Session State {0} does not accept buffer: {1}".CheckedFormat(State, buffer));
                            break;
                    }
                    break;

                case ManagementType.SessionRequestAcceptedResponse:
                    switch (State.StateCode)
                    {
                        case SessionStateCode.RequestSessionOpen:
                            count += ProcessClientSessionOpenAcceptance(qpcTimeStamp, bufferNVS);
                            break;
                        default:
                            if ((ClientUUID != clientUUID || ClientInstanceNum != clientInstanceNum) && !State.IsConnected())
                                count += HandleSessionProtocolViolation(qpcTimeStamp, "Session State {0} does not accept buffer: {1}".CheckedFormat(State, buffer));
                            break;
                    }
                    break;

                case ManagementType.RequestCloseSession:
                    {
                        ValueContainer reasonVC = bufferNVS["Reason"].VC;

                        string reason = "Received request to close session {0}".CheckedFormat(reasonVC.ToStringSML());

                        SetState(qpcTimeStamp, SessionStateCode.CloseRequested, reason);

                        GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.NoteSessionTerminated, reason: reason, terminationReasonCode: TerminationReasonCode.ClosedByRequest);

                        SetState(qpcTimeStamp, SessionStateCode.Terminated, reason);

                        count++;
                    }
                    break;

                case ManagementType.NoteSessionTerminated:
                    {
                        if (TraceEmitter.IsEnabled)
                            TraceEmitter.Emit("{0} Received buffer {1}, nvs:{2} in State {3}", SessionName, buffer, bufferNVS.ToStringSML(), State);

                        ValueContainer reasonVC = bufferNVS["Reason"].VC;
                        var terminationReasonCode = bufferNVS["TerminationReason"].VC.GetValue<TerminationReasonCode>(rethrow: false);

                        SetState(qpcTimeStamp, SessionStateCode.Terminated, "Received remote termination reason: {0}".CheckedFormat(reasonVC.ToStringSML()), terminationReasonCode);
                    }
                    break;

                case ManagementType.Status:
                    if (TraceEmitter.IsEnabled)
                        TraceEmitter.Emit("Received status update: {0}", bufferNVS.ToStringSML());

                    count += ProcessTransmitterAspectsOfStatusUpdate(qpcTimeStamp, bufferNVS);
                    break;

                case ManagementType.KeepAlive:
                    count += ServiceTransmitter(qpcTimeStamp, enableRetransmission: true, enableMessageSending: true, requestSendAckNow: true);
                    break;

                default:
                    count += HandleSessionProtocolViolation(qpcTimeStamp, "Received management buffer with unsupported contents: {0} {1}".CheckedFormat(buffer, bufferNVS.ToStringSML()));
                    break;
            }

            return count;
        }

        private int ProcessServerSessionInitialOpen(QpcTimeStamp qpcTimeStamp, string remoteName, string clientUUID, ulong clientInstanceNum, INamedValueSet bufferNVS)
        {
            int bufferSize = bufferNVS["BufferSize"].VC.GetValue<int>(rethrow: false);

            if (!remoteName.IsNullOrEmpty() && !ClientUUID.IsNullOrEmpty() && ClientInstanceNum != 0 && BufferPool.BufferSize == bufferSize)
            {
                RemoteName = remoteName;
                ClientUUID = clientUUID;
                ClientInstanceNum = clientInstanceNum;

                GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.SessionRequestAcceptedResponse);

                SetState(qpcTimeStamp, SessionStateCode.Active, Fcns.CurrentMethodName);

                return 1;
            }
            else
            {
                return HandleSessionProtocolViolation(qpcTimeStamp, "RequestOpenSession failed: Name, ClientUUID, ClientInstanceNum, and/or BufferSize are missing, invalid, or incorrecct in: {0} [expected BufferSize:{1}]".CheckedFormat(bufferNVS.ToStringSML(), BufferPool.BufferSize), terminationReasonCode: TerminationReasonCode.BufferSizesDoNotMatch);
            }
        }

        private int ProcessClientSessionOpenAcceptance(QpcTimeStamp qpcTimeStamp, INamedValueSet bufferNVS)
        {
            var bufferSizeVC = bufferNVS["BufferSize"].VC;
            int bufferSize = bufferSizeVC.GetValue<int>(rethrow: false);

            if (bufferSize != BufferPool.BufferSize)
            {
                return HandleSessionProtocolViolation(qpcTimeStamp, "SessionRequestAcceptedResponse is not valid: given BufferSize {0} does not match current value {1}".CheckedFormat(bufferSizeVC.ToStringSML(), BufferPool.BufferSize), terminationReasonCode: TerminationReasonCode.BufferSizesDoNotMatch);
            }

            SetState(qpcTimeStamp, SessionStateCode.Active, "Session open request has been accepted");

            return 1;
        }

        SortedList<ulong, Buffers.Buffer> heldOutOfOrderBuffersList = new SortedList<ulong, Buffers.Buffer>();
        QpcTimeStamp firstOutOfOrderBufferReceivedTimeStamp = QpcTimeStamp.Zero;

        private int ServiceHeldOutOfOrderBufferList(QpcTimeStamp qpcTimeStamp, bool checkForStaleBuffers = true)
        {
            int count = 0;

            for (;;)
            {
                Buffers.Buffer firstHeldBuffer = heldOutOfOrderBuffersList.FirstOrDefault().Value;

                if (firstHeldBuffer == null)
                    break;

                ulong firstHeldBufferSeqNum = (firstHeldBuffer != null) ? firstHeldBuffer.SeqNum : 0;

                if (firstHeldBufferSeqNum != (lastRecvdValidBufferSeqNum + 1))
                    break;

                lastRecvdValidBufferSeqNum = firstHeldBufferSeqNum;

                count += ProcessInOrderOrNoSeqNumBufferReceived(qpcTimeStamp, firstHeldBuffer);

                heldOutOfOrderBuffersList.RemoveAt(0);

                TraceEmitter.Emit("Accepted and handled next held (out of order) buffer {0}", firstHeldBuffer);

                count++;
            }

            // if we consume some or all of the held out of order buffers then clear the update the firstOutOfOrderBufferReceivedTimeStamp (to zero or to current time stamp)
            if (count > 0)
            {
                firstOutOfOrderBufferReceivedTimeStamp = (heldOutOfOrderBuffersList.Count == 0) ? QpcTimeStamp.Zero : qpcTimeStamp;
            }

            // if we are allowed to check for stale buffers and we did not pull any from the out of order list then do check.
            if (checkForStaleBuffers && count == 0)
            {
                for (; ; )
                {
                    Buffers.Buffer firstHeldBuffer = heldOutOfOrderBuffersList.FirstOrDefault().Value;

                    if (firstHeldBuffer == null)
                        break;

                    TimeSpan bufferAge = (qpcTimeStamp - firstHeldBuffer.TimeStamp);

                    if (firstHeldBuffer.SeqNum <= lastRecvdValidBufferSeqNum)
                    {
                        heldOutOfOrderBuffersList.RemoveAt(0);
                        count++;

                        TraceEmitter.Emit("Discarded held buffer because seq num has been passed: {0} [age:{1:f3}]", firstHeldBuffer, bufferAge.TotalSeconds);
                    }
                    else if (bufferAge > Config.MaxOutOfOrderBufferHoldPeriod)
                    {
                        heldOutOfOrderBuffersList.RemoveAt(0);
                        count++;

                        TraceEmitter.Emit("Discarded held buffer due to age: {0} [age:{1:f3} > {2:f3}]", firstHeldBuffer, bufferAge.TotalSeconds, Config.MaxOutOfOrderBufferHoldPeriod.TotalSeconds);
                    }
                    else
                    {
                        // once we reach a held buffer that we are going to continue holding, we also implicitly keep all of the rest of them (as they are in sorted order).
                        break;
                    }
                }
            }

            return count;
        }

        #endregion
    }

    #endregion

    #region Constants

    public static partial class Constants
    {
        /// <summary>
        /// Attribute name for HeldBufferSeqNums ("HeldBufferSeqNums")
        /// </summary>
        public const string HeldBufferSeqNumsAttributeName = "HeldBufferSeqNums";
    }

    #endregion
}

//-------------------------------------------------------------------
