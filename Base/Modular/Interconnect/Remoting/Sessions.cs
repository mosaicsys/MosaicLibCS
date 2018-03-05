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
using MosaicLib.Semi.E005.Data;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

// Please note: see comments in for MosaicLib.Modular.Interconnect.Remoting in Remoting.cs

namespace MosaicLib.Modular.Interconnect.Remoting.Sessions
{
    /// <summary>
    /// This enum gives the current summary state for a given client session.
    /// <para/>None (0), Opening, Reopening, Active, Idle, IdleWithPendingWork, CloseRequested, Closed, ConnectionLost, Terminated
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

        /// <summary>Session is connected but no recent (valid) traffic has been sent, or recieved</summary>
        Idle,

        /// <summary>Session is connected but no recent (valid) traffic has been sent, or recieved, and the outbound queue is non-empty.  This is typically only expected when the session is about to fail.</summary>
        IdleWithPendingWork,

        /// <summary>Requests that the transport layer open the session's connection.  Transport responds with NoteTransportConnected once the connection is complete.</summary>
        RequestTransportConnect,

        /// <summary>Requests that the transport layer open the session's connection.  Transport responds with NoteTransportConnected once the connection is complete.</summary>
        RequestTransportReconnect,

        /// <summary>State that covers Client Open Requests.  Session will then periodically post Session Open requests.</summary>
        RequestSessionOpen,

        /// <summary>State that covers Client Resume Requests.  Session will then periodically post Session Reopen requests.</summary>
        RequestSessionResume,

        /// <summary>The Session client has requested that the session be closed.  CloseRequestReason will be non-empty.</summary>
        CloseRequested,

        /// <summary>The Session client has requested that the session be closed (perminantly).  CloseRequestReason will be non-empty.</summary>
        FinalCloseRequested,

        /// <summary>The transport layer has indicated that the session has been closed, either by request, or due to an error.  Auto-reconnect attempts may occur from this state.</summary>
        ConnectionClosed,

        /// <summary>The transport layer has indicated that the session has been closed/terminated due to an error.  This state will not attempt any auto-reconnect.</summary>
        Terminated,
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
                case SessionStateCode.FinalCloseRequested:
                    return includeClosingStates;

                case SessionStateCode.ServerSessionInitial:
                case SessionStateCode.RequestTransportConnect:
                case SessionStateCode.RequestTransportReconnect:
                case SessionStateCode.RequestSessionOpen:
                case SessionStateCode.RequestSessionResume:
                    return includeConnectingStates;

                case SessionStateCode.ConnectionClosed:
                case SessionStateCode.Terminated:
                default: 
                    return false;
            }
        }

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

    public struct SessionState : IEquatable<SessionState>
    {
        public SessionStateCode StateCode { get; set; }
        public QpcTimeStamp TimeStamp { get; set; }

        public override string ToString()
        {
            return "{0}".CheckedFormat(StateCode);
        }

        public bool Equals(SessionState other)
        {
            return (StateCode == other.StateCode
                    && TimeStamp == other.TimeStamp);
        }

        public bool IsConnected(bool includeConnectingStates = false, bool includeClosingStates = false) { return StateCode.IsConnected(includeConnectingStates: includeConnectingStates, includeClosingStates: includeClosingStates); }
        public bool IsPerminantlyClosed { get { return StateCode.IsPerminantlyClosed(); } }
        public bool CanAcceptOutboundMessages { get { return StateCode.CanAcceptOutboundMessages(); } }
    }

    /// <summary>
    /// This interface is used by all of the servicable layers under remoting.  
    /// This allows type agnostic runtime hierarchy while supporting part based threading of the object type tree that is constructed at runtime to support each remoting interface.
    /// </summary>
    public interface IServiceable
    {
        /// <summary>
        /// This method services the underlying object and returns the count of the number of things that it did.  The result must only be zero if the Service method did not find any work to do.
        /// The expectation is that hierarchical layers of service methods can safely report to the top level if any work was done without needing to use any intermediate if statements provided that the layers in the middle return the sum of the results from that level and below it.
        /// </summary>
        int Service(QpcTimeStamp qpcTimeStamp);
    }

    public interface IMessageSessionFacet : IServiceable
    {
        string SessionName { get; }
        string SessionUUID { get; }

        SessionState State { get; }
        ISequencedObjectSource<SessionState, int> StatePublisher { get; }

        void SetState(QpcTimeStamp qpcTimeStamp, SessionStateCode stateCode, string reason);

        void HandleOutboundMessage(QpcTimeStamp qpcTimeStamp, ushort stream, Messages.Message message);
        HandleMessageDelegate HandleInboundMessageDelegate { get; set; }
    }

    public class SessionConfig
    {
        public SessionConfig()
        {
            SessionExpirationPeriod = (5.0).FromMinutes();
            MaxBufferWriteAheadCount = 4;
            ActiveToIdleHoldoff = (5.0).FromSeconds();
            MaxOutOfOrderBufferHoldPeriod = (10.0).FromSeconds();
            MaxOutOfOrderBufferHoldCount = 100;
            ShortRetransmitHoldoffPeriod = (0.100).FromSeconds();
            NormalRetransmitHoldoffPeriod = (0.333).FromSeconds();
            MaxHeldBufferSeqNumsToIncludeInStatusUpdate = 20;
            ExplicitAckHoldoffPeriod = (0.020).FromSeconds();
        }

        [NamedValueSetItem]
        public TimeSpan SessionExpirationPeriod { get; set; }

        [NamedValueSetItem]
        public int MaxBufferWriteAheadCount { get; set; }

        [NamedValueSetItem]
        public TimeSpan ActiveToIdleHoldoff { get; set; }

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

        INotifyable HostNotifier { get; }
        Buffers.BufferPool BufferPool { get; }

        INamedValueSet TransportParamsNVS { get; set; }

        HandleBuffersDelegate HandleOutboundBuffersDelegate { get; set; }
        void HandleInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray);

        void HandleTransportException(QpcTimeStamp qpcTimeStamp, object transportEndpoint, System.Exception ex, bool endpointClosed = false);
    }

    public interface ITransportConnectionSessionFacet : ITransportSessionFacetBase
    {
        string SessionUUID { get; set; }
        INamedValueSet HostParamsNVS { get; }

        SessionState State { get; }

        void NoteTransportIsConnected(QpcTimeStamp qpcTimeStamp, object transportEndpoint = null);
        void NoteTransportIsClosed(QpcTimeStamp qpcTimeStamp, object transportEndpoint = null, string failureCode = "");

        string CloseRequestReason { get; }
    }

    public interface ITransportServerSessionManagerFacet : ITransportSessionFacetBase
    {
        ITransportConnectionSessionFacet ProcessSessionLevelInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray);
    }


    /// <summary>
    /// Exception type typically thrown when a session encounters an unacceptable buffer that it cannot ignore.
    /// </summary>
    public class SessionException : System.Exception
    {
        public SessionException(string message, System.Exception innerException = null)
            : base(message, innerException)
        { }
    }

    #endregion

    #region SessionManager

    public class SessionManager : ITransportServerSessionManagerFacet
    {
        public static AtomicUInt64 instanceNumGen = new AtomicUInt64();
        public readonly ulong instanceNum = instanceNumGen.Increment();

        public SessionManager(string hostName, INamedValueSet hostParamsNVS, INotifyable hostNotifier, Logging.IMesgEmitter stateEmitter, HandleNewSessionDelegate handleNewSessionDelegate, Buffers.BufferPool bufferPool = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter traceEmitter = null)
        {
            HostName = hostName.MapNullToEmpty();
            HostParamsNVS = hostParamsNVS.ConvertToReadOnly();
            HostNotifier = hostNotifier ?? NullNotifier.Instance;

            SessionName = "{0}.sm_{0:x4}".CheckedFormat(HostName, instanceNum & 0xffff);

            TraceEmitter = traceEmitter ?? Logging.NullMesgEmitter.Instance;
            StateEmitter = stateEmitter ?? TraceEmitter;
            IssueEmitter = issueEmitter ?? StateEmitter;

            HandleNewSessionDelegate = handleNewSessionDelegate;
            BufferPool = bufferPool ?? new Buffers.BufferPool(bufferStateEmitter: traceEmitter, configNVS: hostParamsNVS, configNVSKeyPrefix: "Server.BufferPool.");

            new NamedValueSetAdapter<SessionConfig>() { IssueEmitter = IssueEmitter, ValueNoteEmitter = TraceEmitter, ValueSet = Config = new SessionConfig() }.Setup().Set(HostParamsNVS, merge: true);
        }

        public string HostName { get; private set; }
        public INamedValueSet HostParamsNVS { get; private set; }
        public INotifyable HostNotifier { get; private set; }

        public string SessionName { get; private set; }

        public Logging.IMesgEmitter IssueEmitter { get; private set; }
        public Logging.IMesgEmitter StateEmitter { get; private set; }
        public Logging.IMesgEmitter TraceEmitter { get; private set; }

        public Buffers.BufferPool BufferPool { get; private set; }
        public HandleNewSessionDelegate HandleNewSessionDelegate { get; private set; }

        public INamedValueSet TransportParamsNVS { get; set; }

        public HandleBuffersDelegate HandleOutboundBuffersDelegate { get; set; }
        public HandleBuffersDelegate HandleInboundBuffersDelegate { get { return HandleInboundBuffers; } }

        public SessionConfig Config { get; private set; }

        public void HandleInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray)
        {
            ConnectionSession session = transportEPToClientSessionDictionary.SafeTryGetValue(transportEndpoint);

            if (session != null)
                session.HandleInboundBuffers(qpcTimeStamp, transportEndpoint, bufferParamsArray);
            else
                ProcessSessionLevelInboundBuffers(qpcTimeStamp, transportEndpoint, bufferParamsArray);
        }

        public void HandleTransportException(QpcTimeStamp qpcTimeStamp, object transportEndpoint, System.Exception ex, bool endpointClosed = false)
        {
            if (TraceEmitter.IsEnabled)
                TraceEmitter.Emit("{0}: ep:{1}{2} ex: {3}", Fcns.CurrentMethodName, transportEndpoint, endpointClosed ? " Closed" : "", ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

            ConnectionSession session = transportEPToClientSessionDictionary.SafeTryGetValue(transportEndpoint);

            if (session != null)
                session.HandleTransportException(qpcTimeStamp, transportEndpoint, ex, endpointClosed);
        }

        public ITransportConnectionSessionFacet ProcessSessionLevelInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray)
        {
            var firstBuffer = bufferParamsArray.SafeAccess(0);
            Buffers.BufferHeaderV1 firstHeader = (firstBuffer != null) ? firstBuffer.header : default(Buffers.BufferHeaderV1);
            INamedValueSet firstBufferNVS = (firstBuffer != null) ? firstBuffer.GetPayloadAsE005NVS(NamedValueSet.Empty) : NamedValueSet.Empty;
            ManagementType managementType = firstBufferNVS["Type"].VC.GetValue<ManagementType>(rethrow: false);

            if (firstBuffer == null)
            {}
            else if (firstHeader.PurposeCode != PurposeCode.Management)
            {
                IssueEmitter.Emit("{0}: received unexpected non-managment buffer {1} from ep:'{2}' [ignored]", Fcns.CurrentMethodName, firstBuffer, transportEndpoint);
            }
            else if (managementType == ManagementType.RequestOpenSession || managementType == ManagementType.RequestResumeSession)
            {
                int startAtIndex = firstHeader.Length;
                string sessionUUID = firstBufferNVS["SessionUUID"].VC.GetValue<string>(rethrow: false);
                string name = firstBufferNVS["Name"].VC.GetValue<string>(rethrow: false);

                if (firstHeader.SeqNum == 0 && managementType == ManagementType.RequestOpenSession && !sessionUUID.IsNullOrEmpty())
                {
                    ConnectionSession newClientSession = new ConnectionSession(HostName, HostParamsNVS, HostNotifier, bufferPool: BufferPool, sessionConfig: Config, issueEmitter: IssueEmitter, stateEmitter: StateEmitter, traceEmitter: TraceEmitter)
                    {
                        TransportParamsNVS = TransportParamsNVS,
                        TransportEndpoint = transportEndpoint,
                        TransportRole = Transport.TransportRole.Server,
                        HandleOutboundBuffersDelegate = HandleOutboundBuffersDelegate,
                        SessionName = name,
                        SessionUUID = sessionUUID,
                    };

                    ConnectionSession[] strandedSessionsArray = sessionUUIDToClientSessionDictionary.SafeTryGetValue(sessionUUID).ConcatItems(transportEPToClientSessionDictionary.SafeTryGetValue(transportEndpoint)).Where(item => item != null).ToArray();
                    if (!strandedSessionsArray.IsNullOrEmpty())
                    {
                        TraceEmitter.Emit("{0} {1} stranded {2} sessions [{3}]", Fcns.CurrentMethodName, managementType, strandedSessionsArray.Length, string.Join(",", strandedSessionsArray.Select(item => item.SessionName)));

                        pendingDeadSessionList.SafeAddSet(strandedSessionsArray);
                    }

                    sessionUUIDToClientSessionDictionary[sessionUUID] = newClientSession;
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

                    newClientSession.HandleInboundBuffers(qpcTimeStamp, transportEndpoint, bufferParamsArray);

                    return newClientSession;
                }
                else if (managementType == ManagementType.RequestResumeSession)
                {
                    ConnectionSession session = sessionUUIDToClientSessionDictionary.SafeTryGetValue(sessionUUID) ?? transportEPToClientSessionDictionary.SafeTryGetValue(transportEndpoint);

                    if (session != null && sessionUUID == session.SessionUUID)
                    {
                        session.HandleInboundBuffers(qpcTimeStamp, transportEndpoint, bufferParamsArray);

                        return session;
                    }

                    IssueEmitter.Emit("{0}: Could not find matching session for uuid:'{2}', ep:'{1}'", Fcns.CurrentMethodName, sessionUUID, transportEndpoint);
                }
            }
            else
            {
                IssueEmitter.Emit("{0}: received buffer {1} with unexpected ManagementType:{2} from ep:'{3}'", Fcns.CurrentMethodName, firstBuffer, managementType, transportEndpoint);
            }

            return null;
        }

        public int Service(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            count += RebuildClientSessionArrayIfNeeded.Sum(session => session.Service(qpcTimeStamp));
            count += RemoveTerminatedSessions(qpcTimeStamp);

            return count;
        }

        private Dictionary<string, ConnectionSession> sessionUUIDToClientSessionDictionary = new Dictionary<string, ConnectionSession>();
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

                    if (!session.SessionUUID.IsNullOrEmpty() && Object.ReferenceEquals(session, sessionUUIDToClientSessionDictionary.SafeTryGetValue(session.SessionUUID)))
                        sessionUUIDToClientSessionDictionary.Remove(session.SessionUUID);

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
        public static AtomicUInt64 instanceNumGen = new AtomicUInt64();
        public readonly ulong instanceNum = instanceNumGen.Increment();

        public ConnectionSession(string hostName, INamedValueSet hostParamsNVS, INotifyable hostNotifier, Buffers.BufferPool bufferPool, bool assignUUID = true, SessionConfig sessionConfig = null, SessionStateCode initialSessionStateCode = SessionStateCode.None, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter stateEmitter = null, Logging.IMesgEmitter traceEmitter = null)
        {
            HostName = hostName.MapNullToEmpty();
            HostParamsNVS = hostParamsNVS.ConvertToReadOnly();
            HostNotifier = hostNotifier ?? NullNotifier.Instance;

            TraceEmitter = traceEmitter ?? Logging.NullMesgEmitter.Instance;
            StateEmitter = stateEmitter ?? TraceEmitter;
            IssueEmitter = issueEmitter ?? StateEmitter;

            BufferPool = bufferPool ?? new Buffers.BufferPool(bufferStateEmitter: stateEmitter);
            SessionName = "{0}.cs_{0:x4}".CheckedFormat(HostName, instanceNum & 0xffff);
            SessionUUID = assignUUID ? Guid.NewGuid().ToString() : string.Empty;

            if ((Config = sessionConfig) == null)
                new NamedValueSetAdapter<SessionConfig>() { IssueEmitter = IssueEmitter, ValueNoteEmitter = TraceEmitter, ValueSet = Config = new SessionConfig() }.Setup().Set(HostParamsNVS, merge: true);

            State = new SessionState() { StateCode = initialSessionStateCode, TimeStamp = QpcTimeStamp.Now };
            lastRecvActivityTimeStamp = lastSendActivityTimeStamp = State.TimeStamp;
        }

        public string HostName { get; private set; }
        public INamedValueSet HostParamsNVS { get; private set; }
        public INotifyable HostNotifier { get; set; }

        public Logging.IMesgEmitter IssueEmitter { get; private set; }
        public Logging.IMesgEmitter StateEmitter { get; private set; }
        public Logging.IMesgEmitter TraceEmitter { get; private set; }
        public Buffers.BufferPool BufferPool { get; set; }

        public SessionConfig Config { get; internal set; }

        public string SessionName { get; set; }
        public string SessionUUID { get; set; }

        public INamedValueSet TransportParamsNVS { get; set; }
        public object TransportEndpoint { get; set; }
        public Transport.TransportRole TransportRole { get; set; }
        public IPAddress IPAddress { get { IPEndPoint ipEP = TransportEndpoint as IPEndPoint; return (ipEP != null ? ipEP.Address : null); } }

        public string RemoteName { get { return _remoteName ?? "[RemoteNameIsNull]"; } private set { _remoteName = value; } }
        private string _remoteName = null;

        public HandleMessageDelegate HandleInboundMessageDelegate { get; set; }     // messages that are delivered to the next level up in the protocol stack

        public HandleBuffersDelegate HandleOutboundBuffersDelegate { get; set; }    // buffers that are to be delivired to the transport layer.

        public void HandleTransportException(QpcTimeStamp qpcTimeStamp, object transportEndpoint, System.Exception ex, bool endpointClosed = false)
        {
            if (State.IsConnected(includeConnectingStates: true, includeClosingStates: true))
            {
                IssueEmitter.Emit("{0} State:{1} note {2} for endPoint:{3}{4} ex:{5}", SessionName, StateCode, Fcns.CurrentMethodName, transportEndpoint, endpointClosed ? " Closed" : "", ex.ToString(ExceptionFormat.TypeAndMessage));

                SetState(qpcTimeStamp, SessionStateCode.ConnectionClosed, "Encountered transport exception for {0}: {1}".CheckedFormat(transportEndpoint, ex.ToString(ExceptionFormat.TypeAndMessage)));
            }
            else
            {
                TraceEmitter.Emit("{0} State:{1} note ignoring {2} for endPoint:{3}{4} ex:{5}", SessionName, StateCode, Fcns.CurrentMethodName, transportEndpoint, endpointClosed ? " Closed" : "", ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

        public SessionState State { get { return _state; } private set { _statePublisher.Object = (_state = value); } }
        private SessionState _state = default(SessionState);
        public ISequencedObjectSource<SessionState, int> StatePublisher { get { return _statePublisher; } }
        private GuardedSequencedValueObject<SessionState> _statePublisher = new GuardedSequencedValueObject<SessionState>(default(SessionState));

        public SessionStateCode StateCode { get { return State.StateCode; } }

        public string CloseRequestReason { get; protected set; }

        public void SetState(QpcTimeStamp qpcTimeStamp, SessionStateCode stateCode, string reason)
        {
            SessionState entryState = State;
            Logging.IMesgEmitter useEmitter = StateEmitter;

            TimeSpan entryStateAge = (entryState.TimeStamp != QpcTimeStamp.Zero) ? (qpcTimeStamp - entryState.TimeStamp) : TimeSpan.Zero;

            State = new Sessions.SessionState() { StateCode = stateCode, TimeStamp = qpcTimeStamp };

            string closeRequestReason = string.Empty;

            switch (stateCode)
            {
                case SessionStateCode.Idle:
                case SessionStateCode.Active:
                    useEmitter = TraceEmitter;
                    break;

                case SessionStateCode.CloseRequested:
                case SessionStateCode.FinalCloseRequested:
                    closeRequestReason = reason;            // update CloseRequestReason - used by transport to determine when to close a connection
                    break;

                case SessionStateCode.ConnectionClosed:
                case SessionStateCode.Terminated:
                    closeRequestReason = "Session is {0}: {1}".CheckedFormat(stateCode, reason);            // update CloseRequestReason - used by transport to determine when to close a connection
                    break;

                default:
                    break;
            }

            CloseRequestReason = closeRequestReason;

            useEmitter.Emit("{0} State changed to {1} [from: {2}, reason: {3}]", SessionName, stateCode, entryState, reason ?? "NoReasonGiven");
        }

        public int Service(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            switch (StateCode)
            {
                case SessionStateCode.ClientSessionInitial:
                case SessionStateCode.ServerSessionInitial:
                    break;

                case SessionStateCode.Idle:
                case SessionStateCode.IdleWithPendingWork:
                    count += ServiceOutgoingMessageList(qpcTimeStamp);
                    count += ServiceTransmitter(qpcTimeStamp);
                    count += ServiceHeldOutOfOrderBufferList(qpcTimeStamp, checkForStaleBuffers: true);     // used to get rid of stale held out of order buffers.

                    if (count > 0)
                    {
                        SetState(qpcTimeStamp, SessionStateCode.Active, "Session became active");
                    }
                    else 
                    {
                        TimeSpan stateAge = (qpcTimeStamp - State.TimeStamp);

                        if (stateAge > Config.SessionExpirationPeriod)
                            SetState(qpcTimeStamp, SessionStateCode.CloseRequested, "Session has expired after {0:f3} seconds".CheckedFormat(stateAge.TotalSeconds));
                    }

                    break;

                case SessionStateCode.Active:
                    count += ServiceOutgoingMessageList(qpcTimeStamp);
                    count += ServiceTransmitter(qpcTimeStamp);

                    if (count == 0)
                    {
                        int numPendingMessages = outboundPerMessageStreamHandlerArray.Sum(handler => handler.outboundMessageList.Count);

                        TimeSpan elapsed = (qpcTimeStamp - State.TimeStamp).Min(qpcTimeStamp - lastRecvActivityTimeStamp, qpcTimeStamp - lastSendActivityTimeStamp);
                        if (elapsed > Config.ActiveToIdleHoldoff)
                        {
                            SetState(qpcTimeStamp, (numPendingMessages > 0) ? SessionStateCode.IdleWithPendingWork : SessionStateCode.Idle, "No recent session activity detected");
                            count++;
                        }
                    }
                    break;

                case SessionStateCode.RequestTransportConnect:
                case SessionStateCode.RequestTransportReconnect:
                    break;

                case SessionStateCode.RequestSessionOpen:
                case SessionStateCode.RequestSessionResume:
                    count += ServiceTransmitter(qpcTimeStamp);

                    if (count == 0)
                    {
                        TimeSpan stateAge = (qpcTimeStamp - State.TimeStamp);

                        if (stateAge > Config.NormalRetransmitHoldoffPeriod)
                        {
                            ManagementType managementType = ((StateCode == SessionStateCode.RequestSessionOpen) ? ManagementType.RequestOpenSession : ManagementType.RequestResumeSession);

                            SetState(qpcTimeStamp, StateCode, "Resending {0} after {1:f3} seconds".CheckedFormat(managementType, stateAge.TotalSeconds));

                            GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, managementType, serviceTransmitter: true);
                        }
                    }
                    break;

                case SessionStateCode.CloseRequested:
                    count += ServiceTransmitter(qpcTimeStamp, enableRetransmission: true, enableMessageSending: false);

                    if (count == 0)
                    {
                        TimeSpan stateAge = (qpcTimeStamp - State.TimeStamp);

                        if (TransportRole == Transport.TransportRole.Client && stateAge > Config.NormalRetransmitHoldoffPeriod)
                            SetState(qpcTimeStamp, SessionStateCode.ConnectionClosed, "Client wait while {0} expired after {1:f3} seconds".CheckedFormat(StateCode, stateAge.TotalSeconds));

                        // Server session can stay in this state until they are removed due to inactivity
                    }
                    break;

                case SessionStateCode.FinalCloseRequested:
                    count += ServiceTransmitter(qpcTimeStamp, enableRetransmission: true, enableMessageSending: false);

                    if (count == 0)
                    {
                        TimeSpan stateAge = (qpcTimeStamp - State.TimeStamp);

                        if (stateAge > Config.NormalRetransmitHoldoffPeriod)
                            SetState(qpcTimeStamp, SessionStateCode.Terminated, "Client wait while {0} expired after {1:f3} seconds".CheckedFormat(StateCode, stateAge.TotalSeconds));
                    }
                    break;

                case SessionStateCode.ConnectionClosed:
                    {
                        bool canAttemptAutoReconnect = (Config.AutoReconnectHoldoff != null);
                        TimeSpan stateAge = (qpcTimeStamp - State.TimeStamp);

                        if (!canAttemptAutoReconnect)
                        {
                            SetState(qpcTimeStamp, SessionStateCode.Terminated, "Connection has been closed and auto reconnect is not enabled");
                            count++;
                        }
                        else if (stateAge >= (Config.AutoReconnectHoldoff ?? TimeSpan.Zero))
                        {
                            SetState(qpcTimeStamp, SessionStateCode.RequestTransportReconnect, "Attempting auto reconnect after {0:f3} seconds".CheckedFormat(stateAge.TotalSeconds));
                            count++;
                        }
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
                case SessionStateCode.RequestTransportReconnect:
                    SetState(qpcTimeStamp, SessionStateCode.RequestSessionResume, "{0}[{1}]".CheckedFormat(Fcns.CurrentMethodName, transportEndpoint));
                    GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.RequestResumeSession, serviceTransmitter: true);
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

        private void HandleSessionProtocolViolation(QpcTimeStamp qpcTimeStamp, string failureCode, bool serviceTransmitter = true)
        {
            IssueEmitter.Emit("{0} State:{1} note {2} failureCode:{3}", SessionName, StateCode, Fcns.CurrentMethodName, failureCode);

            string faultReason = "Session protocol violation: {0}".CheckedFormat(failureCode);

            SetState(qpcTimeStamp, SessionStateCode.CloseRequested, faultReason);

            GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.NoteSessionTerminated, new NamedValueSet() { { "Reason", faultReason } }, serviceTransmitter: serviceTransmitter);
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
                                deliveredOrFailedCount++;
                            }
                            else
                            {
                                checkForBufferInBadState = true;
                            }
                            break;

                        case Messages.MessageState.Delivered:
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

        private void AddBufferToMessageAccumulation(QpcTimeStamp qpcTimeStamp, Buffers.Buffer buffer)
        {
            ushort stream = buffer.header.MessageStream;

            InboundPerMessageStreamHandler streamHandler = (stream < inboundPerMessageStreamHandlerArray.Length) ? inboundPerMessageStreamHandlerArray[stream] : null;

            if (streamHandler == null)
            {
                int currentMaxStream = inboundPerMessageStreamHandlerArray.Length;
                inboundPerMessageStreamHandlerList.AddRange(Enumerable.Range(currentMaxStream, stream + 1 - currentMaxStream).Select(addingStream => new InboundPerMessageStreamHandler() { stream = unchecked((ushort) addingStream) }));
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
                ushort stream = streamHandler.stream;
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
                                HandleSessionProtocolViolation(qpcTimeStamp, "{0}: found non-middle of message buffer in stream {1} accumulation list: {2}".CheckedFormat(Fcns.CurrentMethodName, stream, nextBuffer));
                                count++;
                                break;
                            }
                        }

                        if (runLength == 0)
                            streamHandler.waitingForMessageBoundary = true;
                    }
                    else
                    {
                        HandleSessionProtocolViolation(qpcTimeStamp, "{0}: found non-start of message buffer at head of stream {1} accumulation list: {2}".CheckedFormat(Fcns.CurrentMethodName, stream, firstBuffer));
                        count++;
                    }

                    if (runLength > 0)
                    {
                        Messages.Message receivedMessage = new Messages.Message(bufferSourcePool: BufferPool, stateEmitter: TraceEmitter, issueEmitter: IssueEmitter).SetState(qpcTimeStamp, Messages.MessageState.Received, Fcns.CurrentMethodName);

                        receivedMessage.bufferList.AddRange(inboundBufferAccumulationList.Take(runLength));
                        inboundBufferAccumulationList.RemoveRange(0, runLength);

                        if (streamHandler.waitingForMessageBoundary)
                            streamHandler.waitingForMessageBoundary = false;

                        HandleMessageDelegate handleInboundMessageDelegate = HandleInboundMessageDelegate;
                        if (handleInboundMessageDelegate != null)
                        {
                            handleInboundMessageDelegate(qpcTimeStamp, stream, receivedMessage);
                            streamHandler.lastActivityMessageTimeStamp = qpcTimeStamp;
                        }
                        else
                        {
                            HandleSessionProtocolViolation(qpcTimeStamp, "{0}: could not deliver message on stream {1}, HandleInboundMessageDelegate is null: {2}".CheckedFormat(Fcns.CurrentMethodName, stream, receivedMessage));
                            receivedMessage.ReturnBuffersToPool(qpcTimeStamp);
                        }

                        count++;

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
                        HandleSessionProtocolViolation(qpcTimeStamp, "{0}: stream {1} message accumulation appears to be stuck [buffer count:{2}, age:{3:f3}]".CheckedFormat(Fcns.CurrentMethodName, stream, listCount, lastActivityAge.TotalSeconds));
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

        private int ServiceTransmitter(QpcTimeStamp qpcTimeStamp, bool enableRetransmission = true, bool enableMessageSending = true)
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
                    if (maxSentBufferSeqNum < buffer.SeqNum && buffer.State == BufferState.Sent)
                    {
                        maxSentBufferSeqNum = buffer.SeqNum;
                        maxSentBufferAckSeqNum = Math.Max(maxSentBufferAckSeqNum, buffer.header.AckSeqNum);
                        count++;
                    }
                    
                    if (buffer.State == BufferState.Delivered)
                        numDelivered++;
                }

                if (numDelivered > 0)
                {
                    count += numDelivered;
                    deliveryPendingList.RemoveAll(buffer => (buffer.State == BufferState.Delivered));
                }
            }

            if (enableRetransmission)
            {
                /// check for need to send a status update message about out of order buffers.
                if (!firstOutOfOrderBufferReceivedTimeStamp.IsZero && ((qpcTimeStamp - firstOutOfOrderBufferReceivedTimeStamp) >= Config.ShortRetransmitHoldoffPeriod))
                {
                    firstOutOfOrderBufferReceivedTimeStamp = qpcTimeStamp + Config.ShortRetransmitHoldoffPeriod;        // holdoff the next one by 2 times the short retransmit holdoff unless we consume some or add more to the list

                    NamedValueSet nvs = new NamedValueSet() 
                    { 
                        { "HeldBufferSeqNums", heldOutOfOrderBuffersList.Values.Take(Config.MaxHeldBufferSeqNumsToIncludeInStatusUpdate).Select(buffer => buffer.SeqNum).ToArray() } 
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

                        TraceEmitter.Emit("Triggering normal resend (time) for: {0} [of {1}]", String.Join(",", addToSendNowSetArray.Select(buffer => buffer.BufferName)), outOfOrderPossibleMissingBufferArray.Length);

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
            if (sendNowList.Count == 0 && (bufferAckSeqNumToSend != maxSentBufferAckSeqNum) && !sendBufferAckSeqNumAfterTimeStamp.IsZero && qpcTimeStamp >= sendBufferAckSeqNumAfterTimeStamp)
            {
                Buffers.Buffer explicitAckBuffer = BufferPool.Acquire(qpcTimeStamp).Update(purposeCode: PurposeCode.None).SetState(qpcTimeStamp, BufferState.ReadyToSend, "Sending explicit ack");
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

            ulong [] heldBuffersSeqNumsArray = statusNVS["HeldBufferSeqNums"].VC.GetValue<ulong []>(rethrow: false);

            if (!heldBuffersSeqNumsArray.IsNullOrEmpty())
            {
                ulong lastHeldSeqNum = heldBuffersSeqNumsArray.Last();
                outOfOrderPossibleMissingBufferArray = deliveryPendingList.Where(buffer => buffer.SeqNum < lastHeldSeqNum && !heldBuffersSeqNumsArray.Contains(buffer.SeqNum)).ToArray().MapEmptyToNull();

                if (outOfOrderPossibleMissingBufferArray != null)
                    count++;
            }

            return count;
        }

        private void GenerateAndAddManagementBufferToSendNowList(QpcTimeStamp qpcTimeStamp, ManagementType managementType, NamedValueSet nvs = null, bool serviceTransmitter = true)
        {
            nvs = nvs.ConvertToWriteable();

            nvs.SetValue("Type", managementType);

            if (managementType != ManagementType.Status)
            {
                if (!nvs.Contains("Name"))
                    nvs.SetValue("Name", HostName);

                if (!nvs.Contains("SessionUUID"))
                    nvs.SetValue("SessionUUID", SessionUUID);

                if ((managementType == ManagementType.RequestResumeSession || TransportRole == Transport.TransportRole.Server) && !nvs.Contains("BufferSize"))
                    nvs.SetValue("BufferSize", BufferPool.BufferSize);
            }

            Buffers.Buffer managementBuffer = BufferPool.Acquire(qpcTimeStamp).Update(purposeCode: PurposeCode.Management, buildPayloadDataFromE005NVS: nvs);

            managementBuffer.SetState(qpcTimeStamp, BufferState.ReadyToSend, "Adding management buffer to send now list {0}".CheckedFormat(nvs.SafeToStringSML()));

            sendNowList.Add(managementBuffer);

            if (serviceTransmitter)
                ServiceTransmitter(qpcTimeStamp, enableRetransmission: false, enableMessageSending: false);
        }

        #endregion

        #region Receiver

        QpcTimeStamp lastRecvActivityTimeStamp;

        public void HandleInboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndpoint, params Buffers.Buffer[] bufferParamsArray)
        {
            int count = 0;

            lastRecvActivityTimeStamp = qpcTimeStamp;

            foreach (var buffer in bufferParamsArray)
            {
                count += ProcessReceivedBufferAck(qpcTimeStamp, buffer);
                count += ProcessReceivedBuffer(qpcTimeStamp, buffer);
            }

            count += ServiceMessageAccumulationAndDelivery(qpcTimeStamp);
        }

        /// <summary>
        /// This defines the range of valid reception ack sequence numbers around the most recently received valid one.
        /// Any non-resent buffer who's ack seq num is outside of this range is a session protocol violation.
        /// (10000)
        /// </summary>
        public const ulong MaxAcceptableAckWindowWidth = 10000;

        ulong lastRecvdValidAckBufferSeqNum = 0;

        private int ProcessReceivedBufferAck(QpcTimeStamp qpcTimeStamp, Buffers.Buffer rxBuffer)
        {
            int count = 0;

            ulong ackBufferSeqNum = rxBuffer.header.AckSeqNum;

            // buffers with a zero for the ack seq num or which came before the buffer that gave us the lastRecvdValidAckBufferSeqNum will be ignored (even if they have not been resent - aka they arrived out of order)
            if (ackBufferSeqNum == 0 || (ackBufferSeqNum <= lastRecvdValidAckBufferSeqNum))
                return count;

            if (ackBufferSeqNum - lastRecvdValidAckBufferSeqNum > MaxAcceptableAckWindowWidth)
            {
                HandleSessionProtocolViolation(qpcTimeStamp, "{0}: Buffer's ack seq num {1} is out of valid range {2}..{3}: {4}".CheckedFormat(Fcns.CurrentMethodName, ackBufferSeqNum, lastRecvdValidAckBufferSeqNum, lastRecvdValidAckBufferSeqNum + MaxAcceptableAckWindowWidth, rxBuffer));
                return count;
            }

            ulong entryLastRecvdValidAckBufferSeqNum = lastRecvdValidAckBufferSeqNum;
            lastRecvdValidAckBufferSeqNum = ackBufferSeqNum;

            string reason = Fcns.CurrentMethodName;

            foreach (var buffer in deliveryPendingList.FilterAndRemove(txBuffer => txBuffer.SeqNum <= lastRecvdValidAckBufferSeqNum))
            {
                buffer.SetState(qpcTimeStamp, BufferState.Delivered, reason);

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
            int count = 0;

            ulong bufferSeqNum = buffer.SeqNum;
            PurposeCode bufferPurposeCode = buffer.PurposeCode;
            BufferHeaderFlags bufferHeaderFlags = buffer.Flags;
            bool isResent = ((bufferHeaderFlags & BufferHeaderFlags.BufferIsBeingResent) != 0);

            switch (bufferPurposeCode)
            {
                case PurposeCode.None:      // these are used for expicit acks
                    TraceEmitter.Emit("Received explicit ack: {0}", buffer);
                    break;

                case PurposeCode.Message:
                case PurposeCode.MessageStart:
                case PurposeCode.MessageMiddle:
                case PurposeCode.MessageEnd:
                    if (State.IsConnected(includeConnectingStates: false))
                    {
                        if (bufferSeqNum != 0)
                        {
                            if (bufferSeqNum == (lastRecvdValidBufferSeqNum + 1) || (lastRecvdValidBufferSeqNum == 0))
                            {
                                lastRecvdValidBufferSeqNum = bufferSeqNum;

                                if (sendBufferAckSeqNumAfterTimeStamp.IsZero)
                                    sendBufferAckSeqNumAfterTimeStamp = qpcTimeStamp + Config.ExplicitAckHoldoffPeriod;

                                AddBufferToMessageAccumulation(qpcTimeStamp, buffer);
                                count++;

                                if (heldOutOfOrderBuffersList.Count > 0)
                                    count += ServiceHeldOutOfOrderBufferList(qpcTimeStamp, checkForStaleBuffers: false);
                            }
                            else if (bufferSeqNum > lastRecvdValidBufferSeqNum)
                            {
                                if (firstOutOfOrderBufferReceivedTimeStamp.IsZero || firstOutOfOrderBufferReceivedTimeStamp > qpcTimeStamp)
                                    firstOutOfOrderBufferReceivedTimeStamp = qpcTimeStamp;

                                if (heldOutOfOrderBuffersList.Count < Config.MaxOutOfOrderBufferHoldCount)
                                {
                                    TraceEmitter.Emit("Received out of order buffer {0}", buffer);

                                    heldOutOfOrderBuffersList.Add(bufferSeqNum, buffer);
                                }
                                else
                                {
                                    TraceEmitter.Emit("Could not retain received out of order buffer {0}: held buffer list has reached maximum capacity [{1} >= {2}]", buffer, heldOutOfOrderBuffersList.Count, Config.MaxOutOfOrderBufferHoldCount);
                                }
                            }
                        }
                        else
                        {
                            HandleSessionProtocolViolation(qpcTimeStamp, "recieved message buffer with zero SeqNum: {10} [{1}]".CheckedFormat(buffer, State));
                        }
                    }
                    else
                    {
                        HandleSessionProtocolViolation(qpcTimeStamp, "recieved message buffer while session is not connected: {1}".CheckedFormat(State));
                    }
                    break;

                case PurposeCode.Management:
                    {
                        TraceEmitter.Emit("Received management buffer: {0}", buffer);

                        INamedValueSet bufferNVS = buffer.GetPayloadAsE005NVS(NamedValueSet.Empty);
                        ManagementType managementType = bufferNVS["Type"].VC.GetValue<ManagementType>(rethrow: false);
                        string remoteName = bufferNVS["Name"].VC.GetValue<string>(rethrow: false);
                        string sessionUUID = bufferNVS["SessionUUID"].VC.GetValue<string>(rethrow: false);

                        switch (managementType)
                        {
                            case ManagementType.RequestOpenSession:
                                switch (State.StateCode)
                                {
                                    case SessionStateCode.ServerSessionInitial:
                                        count += ProcessServerSessionInitialOpen(qpcTimeStamp, remoteName, sessionUUID, bufferNVS);
                                        break;
                                    default:
                                        if (SessionUUID != sessionUUID && !State.IsConnected())
                                            HandleSessionProtocolViolation(qpcTimeStamp, "Session State {0} does not accept buffer: {1}".CheckedFormat(State, buffer));

                                        count++;
                                        break;
                                }
                                break;

                            case ManagementType.RequestResumeSession:
                                switch (State.StateCode)
                                {
                                    case SessionStateCode.ConnectionClosed:
                                        count += ProcessServerSessionResume(qpcTimeStamp, remoteName, sessionUUID, bufferNVS);
                                        break;
                                    default:
                                        HandleSessionProtocolViolation(qpcTimeStamp, "Session State {0} does not accept buffer: {1}".CheckedFormat(State, buffer));
                                        count++;
                                        break;
                                }
                                break;

                            case ManagementType.SessionRequestAcceptedResponse:
                                switch (State.StateCode)
                                {
                                    case SessionStateCode.RequestSessionOpen:
                                        count += ProcessClientSessionOpenAcceptance(qpcTimeStamp, bufferNVS);
                                        break;
                                    case SessionStateCode.RequestSessionResume:
                                        SetState(qpcTimeStamp, SessionStateCode.Active, "Session resume request has been accepted");
                                        count++;
                                        break;
                                    default:
                                        if (SessionUUID != sessionUUID && !State.IsConnected())
                                            HandleSessionProtocolViolation(qpcTimeStamp, "Session State {0} does not accept buffer: {1}".CheckedFormat(State, buffer));

                                            count++;
                                        break;
                                }
                                break;

                            case ManagementType.NoteSessionTerminated:
                                {
                                    if (TraceEmitter.IsEnabled)
                                        TraceEmitter.Emit("{0} Received buffer {1}, nvs:{2} in State {3}", SessionName, buffer, bufferNVS.ToStringSML(), State);

                                    ValueContainer reasonVC = bufferNVS["Reason"].VC;

                                    SetState(qpcTimeStamp, SessionStateCode.Terminated, "Received remote termination reason: {0}".CheckedFormat(reasonVC.ToStringSML()));
                                }
                                break;

                            case ManagementType.Status:
                                if (TraceEmitter.IsEnabled)
                                    TraceEmitter.Emit("Received status update: {0}", bufferNVS.ToStringSML());

                                count += ProcessTransmitterAspectsOfStatusUpdate(qpcTimeStamp, bufferNVS);
                                break;

                            default:
                                HandleSessionProtocolViolation(qpcTimeStamp, "Received management buffer with unsupported contents: {0} {1}".CheckedFormat(buffer, bufferNVS.ToStringSML()));
                                count++;
                                break;
                        }
                    }
                    break;

                default:
                    HandleSessionProtocolViolation(qpcTimeStamp, "Recieved buffer with unsupported purpose: {1}".CheckedFormat(State, buffer));
                    count++;
                    break;
            }

            return count;
        }

        private int ProcessServerSessionInitialOpen(QpcTimeStamp qpcTimeStamp, string remoteName, string sessionUUID, INamedValueSet bufferNVS)
        {
            if (!remoteName.IsNullOrEmpty() && !sessionUUID.IsNullOrEmpty())
            {
                RemoteName = remoteName;
                SessionUUID = sessionUUID;

                GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.SessionRequestAcceptedResponse);

                SetState(qpcTimeStamp, SessionStateCode.Active, Fcns.CurrentMethodName);
            }
            else
            {
                HandleSessionProtocolViolation(qpcTimeStamp, "RequestOpenSession failed: Name and/or SessionUUID are missing or invalid in: {0}".CheckedFormat(bufferNVS.ToStringSML()));
            }

            return 1;
        }

        private int ProcessServerSessionResume(QpcTimeStamp qpcTimeStamp, string remoteName, string sessionUUID, INamedValueSet bufferNVS)
        {
            int bufferSize = bufferNVS["BufferSize"].VC.GetValue<int>(rethrow: false);

            if (RemoteName == remoteName && SessionUUID == sessionUUID && BufferPool.BufferSize == bufferSize)
            {
                GenerateAndAddManagementBufferToSendNowList(qpcTimeStamp, ManagementType.SessionRequestAcceptedResponse);
                SetState(qpcTimeStamp, SessionStateCode.Active, Fcns.CurrentMethodName);
            }
            else
            {
                HandleSessionProtocolViolation(qpcTimeStamp, "RequestResumeSession failed: Name, SessionUUID, and/or BufferSize are missing, invalid, or incorrect in: {0}".CheckedFormat(bufferNVS.ToStringSML()));
            }

            return 1;
        }

        private int ProcessClientSessionOpenAcceptance(QpcTimeStamp qpcTimeStamp, INamedValueSet bufferNVS)
        {
            int bufferSize = bufferNVS["BufferSize"].VC.GetValue<int>(rethrow: false);

            if (bufferSize != 0)
            {
                int entryBufferSize = BufferPool.BufferSize;
                if (BufferPool.BufferSize != bufferSize)
                {
                    BufferPool.BufferSize = bufferSize;
                    StateEmitter.Emit("Changed BufferPool BufferSize to {0} to match server's value [from:{1}]", bufferSize, entryBufferSize);
                }

                SetState(qpcTimeStamp, SessionStateCode.Active, "Session open request has been accepted");
            }
            else
            {
                HandleSessionProtocolViolation(qpcTimeStamp, "SessionRequestAcceptedResponse is not valid: BufferSize is missing or invalid in: {0}".CheckedFormat(bufferNVS.ToStringSML()));
            }

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

                AddBufferToMessageAccumulation(qpcTimeStamp, firstHeldBuffer);
                heldOutOfOrderBuffersList.RemoveAt(0);

                TraceEmitter.Emit("Accepted held (out of order) buffer {0}", firstHeldBuffer);

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
}

//-------------------------------------------------------------------
