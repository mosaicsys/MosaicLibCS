//-------------------------------------------------------------------
/*! @file Interconnect/Remoting/Transport.cs
 *  @brief Common Transport related definitions for Modular.Interconnect.Remoting.Transport
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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Remoting.Sessions;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;

// Please note: see comments in for MosaicLib.Modular.Interconnect.Remoting in Remoting.cs

namespace MosaicLib.Modular.Interconnect.Remoting.Transport
{
    #region ITransportConnection, ITransportConnectionFactory and TransportConnectionFactory itself

    public interface ITransportConnection : IServiceable, IDisposable
    {
        TransportTypeFeatures TransportTypeFeatures { get; }
    }

    [Flags]
    public enum TransportTypeFeatures : int
    {
        Default = 0x00,
        Stream = 0x01,
        Message = 0x02,
        Reliable = 0x04,
        Server = 0x08,
        Client = 0x10,
    }

    public interface ITransportConnectionFactory
    {
        /// <summary>
        /// Extracts the "ConnectionType" string from the provided <paramref name="connParamsNVS"/> and attemps to find and create a server connection of that type using the given params nvs to configure the resulting server connection's behavior.
        /// If the resulting ConnectionType is not recognized this method will return null.
        /// <para/>Supported connection types are UDP
        /// </summary>
        /// <exception cref="TransportConnectionFactoryException">This exception is trown for any case where the connection factory object (or one it delegates the work to) encounters an error that cannot be resolved locally</exception>
        ITransportConnection CreateServerConnection(INamedValueSet connParamsNVS, ITransportServerSessionManagerFacet sessionManager);

        /// <summary>
        /// Extracts the "ConnectionType" string frm the provided <paramref name="connParamsNVS"/> and attempts to find and create a server connection of that type using the given params nvs to configure the resulting client connection's behavior.
        /// If the resulting ConnectionType is not recognized this method will return null.
        /// <para/>Supported connection types are UDP
        /// </summary>
        /// <exception cref="TransportConnectionFactoryException">This exception is trown for any case where the connection factory object (or one it delegates the work to) encounters an error that cannot be resolved locally</exception>
        ITransportConnection CreateClientConnection(INamedValueSet connParamsNVS, ITransportConnectionSessionFacet session);
    }

    /// <summary>
    /// Exception type typically thrown when a transport connection factory encounters a problem.
    /// </summary>
    public class TransportConnectionFactoryException : System.Exception
    {
        public TransportConnectionFactoryException(string message, System.Exception innerException = null) 
            : base(message, innerException) 
        { }
    }

    /// <summary>
    /// This class plays two roles.  
    /// First it is the default singleton Instance manager for obtaining an ITransportConnectionFactory.
    /// Second it provides the basic ConnectionType based dictionary of factory objects for the supported connection types and provides the means to add new supported types so that they can be used by clients of this class.
    /// <para/>Initially known transport (ConnectionType) types: UDP, UDPv4, UDPv6, TCP, TCPv4, TCPv6, DefaultPatchPanel
    /// </summary>
    public class TransportConnectionFactory : ITransportConnectionFactory
    {
        public static ITransportConnectionFactory Instance { get { return singletonInstanceHelper.Instance; } }
        private static SingletonHelperBase<ITransportConnectionFactory> singletonInstanceHelper = new SingletonHelperBase<ITransportConnectionFactory>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new TransportConnectionFactory());

        internal TransportConnectionFactory() 
        {
            ResetContents();
        }

        public ITransportConnectionFactory this[string connectionType]
        {
            get
            {
                lock (mutex)
                {
                    return typeDictionary[connectionType.Sanitize()];
                }
            }
            set 
            {
                lock (mutex)
                {
                    typeDictionary[connectionType.Sanitize()] = value;
                }
            }
        }

        public TransportConnectionFactory Add(string connectionType, ITransportConnectionFactory connectionFactoryForType) 
        {
            lock (mutex)
            {
                typeDictionary.Add(connectionType.Sanitize(), connectionFactoryForType);
            }

            return this;
        }

        ITransportConnection ITransportConnectionFactory.CreateServerConnection(INamedValueSet connParamsNVS, ITransportServerSessionManagerFacet sessionManager)
        {
            connParamsNVS = connParamsNVS.MapNullToEmpty();

            lock (mutex)
            {
                string connectionType = connParamsNVS["ConnectionType"].VC.GetValue<string>(rethrow: false);

                ITransportConnectionFactory connectionFactoryForType = typeDictionary.SafeTryGetValue(connectionType);

                if (connectionFactoryForType == null)
                    throw new TransportConnectionFactoryException("{0} failed: ConnectionType '{2}' is not recognized in {2}".CheckedFormat(Fcns.CurrentMethodName, connectionType, connParamsNVS.ToStringSML()));

                try 
                {
                    return connectionFactoryForType.CreateServerConnection(connParamsNVS, sessionManager);
                }
                catch (TransportConnectionFactoryException)
                {
                    throw;
                }
                catch (System.Exception ex)
                {
                    throw new TransportConnectionFactoryException("Encountered unexpected exception", ex);
                }
            }
        }

        ITransportConnection ITransportConnectionFactory.CreateClientConnection(INamedValueSet connParamsNVS, ITransportConnectionSessionFacet session)
        {
            connParamsNVS = connParamsNVS.MapNullToEmpty();

            lock (mutex)
            {
                string connectionType = connParamsNVS["ConnectionType"].VC.GetValue<string>(rethrow: false);

                ITransportConnectionFactory connectionFactoryForType = typeDictionary.SafeTryGetValue(connectionType);

                if (connectionFactoryForType == null)
                    throw new TransportConnectionFactoryException("{0} failed: ConnectionType '{2}' is not recognized in {2}".CheckedFormat(Fcns.CurrentMethodName, connectionType, connParamsNVS.ToStringSML()));

                try 
                {
                    return connectionFactoryForType.CreateClientConnection(connParamsNVS, session);
                }
                catch (TransportConnectionFactoryException)
                {
                    throw;
                }
                catch (System.Exception ex)
                {
                    throw new TransportConnectionFactoryException("Encountered unexpected exception", ex);
                }
            }
        }

        private object mutex = new object();
        private Dictionary<string, ITransportConnectionFactory> typeDictionary;

        /// <summary>
        /// Resets the internal dictionary's contents to construction default.
        /// <para/>Supports call chaining.
        /// </summary>
        public TransportConnectionFactory ResetContents()
        {
            lock (mutex)
            {
                typeDictionary = new Dictionary<string, ITransportConnectionFactory>()
                {
                    { "UDP", new Details.UDPTransportConnectionFactory() },
                    { "UDPv4", new Details.UDPTransportConnectionFactory() },
                    { "UDPv6", new Details.UDPTransportConnectionFactory() },
                    { "TCP", new Details.TCPTransportConnectionFactory() },
                    { "TCPv4", new Details.TCPTransportConnectionFactory() },
                    { "TCPv6", new Details.TCPTransportConnectionFactory() },
                    { "DefaultPatchPanel", new Details.PatchPanelTransportConnectionFactory() },
                };
            }

            return this;
        }
    }

    /// <summary>
    /// Gives the role that has been assigned to a transport instance.
    /// <para/>Client (0), Server
    /// </summary>
    public enum TransportRole : int
    {
        Client = 0,
        Server,
    }

    #endregion

    namespace Details
    {
        public static partial class Constants
        {
            public const int DefaultUDPPort = 22971;
            public const int DefaultTCPPort = 22971;
        }

        #region UDPTransportConnectionFactory, TCPTransportConnectionFactory, PatchPanelTransportConnectionFactory

        public class UDPTransportConnectionFactory : ITransportConnectionFactory
        {
            ITransportConnection ITransportConnectionFactory.CreateServerConnection(INamedValueSet connParamsNVS, ITransportServerSessionManagerFacet sessionManager)
            {
                return new UDPTransport(connParamsNVS, sessionManager);
            }

            ITransportConnection ITransportConnectionFactory.CreateClientConnection(INamedValueSet connParamsNVS, ITransportConnectionSessionFacet session)
            {
                return new UDPTransport(connParamsNVS, session);
            }
        }

        public class TCPTransportConnectionFactory : ITransportConnectionFactory
        {
            ITransportConnection ITransportConnectionFactory.CreateServerConnection(INamedValueSet connParamsNVS, ITransportServerSessionManagerFacet sessionManager)
            {
                return new TCPTransport(connParamsNVS, sessionManager);
            }

            ITransportConnection ITransportConnectionFactory.CreateClientConnection(INamedValueSet connParamsNVS, ITransportConnectionSessionFacet session)
            {
                return new TCPTransport(connParamsNVS, session);
            }
        }

        public class PatchPanelTransportConnectionFactory : ITransportConnectionFactory
        {
            public PatchPanelTransportConnectionFactory(string serverName = "Server", PatchPanel patchPanelInstance = null)
            {
                ServerName = serverName;
                PatchPanelInstance = patchPanelInstance ?? new PatchPanel();
            }

            public string ServerName { get; set; }
            public PatchPanel PatchPanelInstance { get; set; }

            ITransportConnection ITransportConnectionFactory.CreateServerConnection(INamedValueSet connParamsNVS, ITransportServerSessionManagerFacet sessionManager)
            {
                return new PatchPanelTransport(connParamsNVS, sessionManager, PatchPanelInstance, ServerName);
            }

            ITransportConnection ITransportConnectionFactory.CreateClientConnection(INamedValueSet connParamsNVS, ITransportConnectionSessionFacet session)
            {
                return new PatchPanelTransport(connParamsNVS, session, PatchPanelInstance, ServerName);
            }
        }

        #endregion

        #region UDPTransport

        public class UDPTransport : DisposableBase, ITransportConnection
        {
            private static AtomicUInt32 instanceNumGen = new AtomicUInt32();

            public const int DefaultServerConcurrentReads = 8;
            public const int DefaultClientConcurrentReads = 4;

            public UDPTransport(INamedValueSet connParamsNVS, ITransportServerSessionManagerFacet sessionManager)
                : this(TransportRole.Server, connParamsNVS, sessionManager)
            {
                ServerSession = sessionManager;

                TransportTypeFeatures = TransportTypeFeatures.Message | TransportTypeFeatures.Server;

                Service(QpcTimeStamp.Now);
            }

            public UDPTransport(INamedValueSet connParamsNVS, ITransportConnectionSessionFacet session)
                : this(TransportRole.Client, connParamsNVS, session)
            {
                ConnectionSession = session;

                TransportTypeFeatures = TransportTypeFeatures.Message | TransportTypeFeatures.Client;

                Service(QpcTimeStamp.Now);
            }

            public TransportTypeFeatures TransportTypeFeatures { get; private set; } 

            public UDPTransport(TransportRole role, INamedValueSet connParamsNVS, ITransportSessionFacetBase sessionBase)
            {
                AddExplicitDisposeAction(Release);

                InstanceNum = instanceNumGen.Increment();
                Role = role;
                ConnParamsNVS = (connParamsNVS = connParamsNVS.ConvertToReadOnly(mapNullToEmpty: true));

                SessionBase = sessionBase;
                BufferPool = sessionBase.BufferPool;
                HostNotifier = sessionBase.HostNotifier;

                sessionBase.TransportParamsNVS = ConnParamsNVS;
                sessionBase.HandleOutboundBuffersDelegate = HandleOutboundBuffers;

                try
                {
                    Port = ConnParamsNVS["Port"].VC.GetValue<int>(rethrow: false, defaultValue: Constants.DefaultUDPPort);
                    ConnectionType = ConnParamsNVS["ConnectionType"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    NumConcurrentReads = ConnParamsNVS["NumConcurrentReads"].VC.GetValue<int>(rethrow: false, defaultValue: IsServer ? DefaultServerConcurrentReads : DefaultClientConcurrentReads);
                    int requestReceiveBufferSize = ConnParamsNVS["ReceiveBufferSize"].VC.GetValue<int>(rethrow: false);

                    IPV6 = ConnectionType.EndsWith("v6");

                    switch (Role)
                    {
                        case TransportRole.Client:
                            if (connParamsNVS.Contains("IPAddress"))
                            {
                                IPAddress ipAddress;
                                if (IPAddress.TryParse(connParamsNVS["IPAddress"].VC.GetValue<string>(rethrow: false).MapNullToEmpty(), out ipAddress))
                                    IPAddress = ipAddress;
                                else
                                    IPAddress = IPAddress.None;
                            }
                            else
                            {
                                IPAddress = IPV6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
                            }

                            ClientTargetIPEndPoint = new IPEndPoint(IPAddress, Port);

                            udpPort = new UdpClient(new IPEndPoint(IPV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
                            // we do not connect our port to the other end.  We always use "sendto", even on the client side.

                            break;

                        case TransportRole.Server:
                            IPAddress = IPV6 ? IPAddress.IPv6Any : IPAddress.Any;
                            ServerIPEndPoint = new IPEndPoint(IPAddress, Port);

                            udpPort = new UdpClient(ServerIPEndPoint);

                            break;

                        default:
                            throw new TransportConnectionFactoryException("Unable to construct {0}: Role {1} is not valid.  {2}".CheckedFormat(Fcns.CurrentClassLeafName, Role, connParamsNVS.ToStringSML()));
                    }

                    if (udpPort != null && udpPort.Client != null)
                    {
                        LocalEndPoint = udpPort.Client.LocalEndPoint;
                    }

                    udpSocket = ((udpPort != null) ? udpPort.Client : null);

                    if (udpSocket == null)
                        throw new TransportConnectionFactoryException("Unable to construct {0} {1}: settings {2} are not valid".CheckedFormat(Role, Fcns.CurrentClassLeafName, connParamsNVS.ToStringSML()));

                    logger = new Logging.Logger("{0}_{1}{2:d2}_Port{3:d5}".CheckedFormat(ConnectionType, Role, InstanceNum, Port));

                    traceLogger = new Logging.Logger("{0}.Trace".CheckedFormat(logger.Name), groupName: Logging.LookupDistributionGroupName, initialInstanceLogGate: connParamsNVS["Transport.TraceLogger.InitialInstanceLogGate"].VC.GetValue(rethrow: false, defaultValue: Logging.LogGate.Debug));
                    trace = traceLogger.Trace;

                    logger.Trace.Emit("LocalEndPoint: {0}", LocalEndPoint);

                    if (IsClient)
                        logger.Trace.Emit("ConnectsToEndPoint: {0}", ClientTargetIPEndPoint);

                    {
                        int entryReceiveBufferSize = udpSocket.ReceiveBufferSize;

                        if (requestReceiveBufferSize > entryReceiveBufferSize)
                        {
                            udpSocket.ReceiveBufferSize = requestReceiveBufferSize;

                            int adjustedReceiveBufferSize = udpSocket.ReceiveBufferSize;

                            if (adjustedReceiveBufferSize == requestReceiveBufferSize)
                                logger.Trace.Emit("ReceiveBufferSize changed to: {0} [from:{1}]", adjustedReceiveBufferSize, entryReceiveBufferSize);
                            else if (adjustedReceiveBufferSize == entryReceiveBufferSize)
                                logger.Trace.Emit("ReceiveBufferSize could not be changed to {0} [from:{0}]", requestReceiveBufferSize, entryReceiveBufferSize);
                            else
                                logger.Trace.Emit("ReceiveBufferSize could not be changed to {0} [became:{1}, from:{2}]", requestReceiveBufferSize, adjustedReceiveBufferSize, entryReceiveBufferSize);
                        }
                        else
                        {
                            logger.Trace.Emit("Default ReceiveBufferSize: {0}", entryReceiveBufferSize);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    throw new TransportConnectionFactoryException("Construction of {0} {1} with settings {2} generated unexpected exception {3}".CheckedFormat(Role, Fcns.CurrentClassLeafName, connParamsNVS.ToStringSML(), ex.ToString(ExceptionFormat.TypeAndMessage)), ex);
                }
            }

            void Release()
            {
                inRelease = true;

                Fcns.DisposeOfObject(ref udpPort);

                postedRecvList.Clear();
                postedSendList.Clear();

                inRelease = false;
            }

            bool inRelease = false;


            public uint InstanceNum { get; private set; }
            public TransportRole Role { get; private set; }
            public bool IsServer { get { return Role == TransportRole.Server; } }
            public bool IsClient { get { return Role == TransportRole.Client; } }
            public INamedValueSet ConnParamsNVS { get; private set; }

            public ITransportSessionFacetBase SessionBase { get; private set; }
            public ITransportConnectionSessionFacet ConnectionSession { get; private set; }
            public ITransportServerSessionManagerFacet ServerSession { get; private set; }

            public Buffers.BufferPool BufferPool { get; private set; }
            public INotifyable HostNotifier { get; private set; }

            public string ConnectionType { get; private set; }
            public bool IPV6 { get; private set; }
            public int Port { get; private set; }
            public IPAddress IPAddress { get; private set; }
            public IPEndPoint ClientTargetIPEndPoint { get; private set; }
            public IPEndPoint ServerIPEndPoint { get; private set; }
            public EndPoint LocalEndPoint { get; private set; }
            public int NumConcurrentReads { get; private set; }

            Logging.Logger logger;
            Logging.Logger traceLogger;
            Logging.IMesgEmitter trace;
            UdpClient udpPort;
            Socket udpSocket;

            #region ToString

            /// <summary>Debugging helper method</summary>
            public override string ToString()
            {
                if (IsClient && ConnectionSession != null)
                    return "{0} ConnectionSessionState:{1}".CheckedFormat(logger.Name, ConnectionSession.State);
                else
                    return "{0}".CheckedFormat(logger.Name);
            }

            #endregion

            public int Service(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                count += ServicePostedSends(qpcTimeStamp);
                count += ServicePostedRecv(qpcTimeStamp);
                count += ServiceSessionState(qpcTimeStamp);

                return count;
            }

            #region Session

            private int ServiceSessionState(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                if (ConnectionSession != null)
                {
                    switch (ConnectionSession.State.StateCode)
                    {
                        case SessionStateCode.RequestTransportConnect:
                            ConnectionSession.NoteTransportIsConnected(qpcTimeStamp);
                            count++;
                            break;
                        default:
                            break;
                    }
                }

                return count;
            }

            #endregion

            #region Sending

            List<SendTracker> postedSendList = new List<SendTracker>();

            private class SendTracker
            {
                private static int seqNumSource = 0;
                public int seqNum = seqNumSource++;
                public Buffers.Buffer buffer;
                public IPEndPoint ipEndPoint;
                public Socket udpSocket;
                public IAsyncResult iar;
                public System.Exception ex;
                public int sentCount;
                public volatile bool done;
            }

            private int ServicePostedSends(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                postedSendList.FilterAndRemove(st => (st == null || st.done)).Where(st => st != null).DoForEach(st => { HandleCompletedSend(qpcTimeStamp, st); count++; });

                return count;
            }

            private void HandleCompletedSend(QpcTimeStamp qpcTimeStamp, SendTracker st)
            {
                if (st.buffer.State == Buffers.BufferState.SendPosted)
                    st.buffer.SetState(qpcTimeStamp, Buffers.BufferState.Sent, Fcns.CurrentMethodName);

                trace.Emit("st:{0:x4} sent [{1} to {2}]", st.seqNum & 0x0ffff, st.buffer, st.ipEndPoint);
            }

            private void HandleOutboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndPoint, params Buffers.Buffer[] bufferParamsArray)
            {
                IPEndPoint ipEndPoint = (transportEndPoint as IPEndPoint) ?? (IsClient ? ClientTargetIPEndPoint : null);

                try
                {
                    if (udpPort != null && !bufferParamsArray.IsNullOrEmpty())
                    {
                        foreach (var buffer in bufferParamsArray)
                        {
                            if (buffer != null && buffer.byteCount > 0 && buffer.byteArray != null && ipEndPoint != null)
                            {
                                if (buffer.State != Buffers.BufferState.SendPosted)
                                    buffer.SetState(qpcTimeStamp, Buffers.BufferState.SendPosted, "transport is posting send");

                                SendTracker st = new SendTracker() { buffer = buffer, udpSocket = udpSocket, ipEndPoint = ipEndPoint };

                                trace.Emit("st:{0:x4} BeginSendTo called [{1} to {2}]", st.seqNum & 0x0ffff, st.buffer, st.ipEndPoint);

                                postedSendList.Add(st);

                                st.iar = udpSocket.BeginSendTo(buffer.byteArray, 0, buffer.byteCount, SocketFlags.None, ipEndPoint, HandleBeginSendAsyncRequestCallback, st);
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    logger.Debug.Emit("{0} caught unexpected exception: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));

                    SessionBase.HandleTransportException(qpcTimeStamp, ipEndPoint, ex);
                }
            }

            private void HandleBeginSendAsyncRequestCallback(IAsyncResult iar)
            {
                SendTracker st = (iar != null) ? (iar.AsyncState as SendTracker) : null;

                if (st != null)
                {
                    try
                    {
                        st.sentCount = st.udpSocket.EndSendTo(iar);

                        trace.Emit("st:{0:x4} EndSendTo succeeded [{1} to:{2}]", st.seqNum & 0x0ffff, st.buffer, st.ipEndPoint);
                    }
                    catch (System.Exception ex)
                    {
                        st.ex = ConvertExceptionIfNeededAndLog(ex, Fcns.CurrentMethodName);
                    }

                    st.done = true;
                }

                HostNotifier.Notify();
            }

            #endregion

            #region Receiving

            List<RecvTracker> postedRecvList = new List<RecvTracker>();

            private class RecvTracker
            {
                private static int seqNumSource = 0;
                public int seqNum = seqNumSource++;
                public Buffers.Buffer buffer;
                public EndPoint fromEndPoint;
                public Socket udpSocket;
                public IAsyncResult iar;
                public System.Exception ex;
                public volatile bool done;
            }

            private int ServicePostedRecv(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;
                {
                    List<RecvTracker> doneRTWithBufferList = postedRecvList.FilterAndRemove(rt => (rt == null || rt.done)).Where(rt => rt != null).ToList();

                    if (doneRTWithBufferList.Count > 0)
                    {
                        doneRTWithBufferList.Where(rt => rt.buffer != null).DoForEach(rt => rt.buffer.UpdateHeaderFromByteArray());

                        HandleCompletedRecv(qpcTimeStamp, doneRTWithBufferList);
                        count++;
                    }
                }

                try
                {
                    while (postedRecvList.Count < NumConcurrentReads && udpPort != null)
                    {
                        Buffers.Buffer buffer = BufferPool.Acquire(qpcTimeStamp);

                        RecvTracker rt = new RecvTracker() { buffer = buffer, udpSocket = udpSocket, fromEndPoint = IPV6 ? AnyV6 : AnyV4 };

                        trace.Emit("rt:{0:x4} BeingReceiveFrom calling", rt.seqNum & 0x0ffff);

                        buffer.SetState(qpcTimeStamp, Buffers.BufferState.ReceivePosted, Fcns.CurrentMethodName);

                        postedRecvList.Add(rt);

                        rt.iar = udpSocket.BeginReceiveFrom(buffer.byteArray, 0, buffer.byteArraySize, SocketFlags.None, ref rt.fromEndPoint, HandleBeginRecvAsyncRequestCallback, rt);

                        count++;
                    }
                }
                catch (System.Exception ex)
                {
                    if (ex != null)
                    {
                        SessionBase.HandleTransportException(qpcTimeStamp, IsClient ? ClientTargetIPEndPoint : null, ex);

                        logger.Debug.Emit("{0} caught unexpected exception: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                        count++;
                    }
                }

                return count;
            }

            private static readonly IPEndPoint AnyV4 = new IPEndPoint(IPAddress.Any, 0);
            private static readonly IPEndPoint AnyV6 = new IPEndPoint(IPAddress.IPv6Any, 0);

            private void HandleCompletedRecv(QpcTimeStamp qpcTimeStamp, List<RecvTracker> doneRTWithBufferList)
            {
                if (SessionBase != null)
                {
                    while (doneRTWithBufferList.SafeCount() > 0)
                    {
                        RecvTracker rtPeek = doneRTWithBufferList[0];
                        EndPoint fromEndPoint = (rtPeek != null) ?  rtPeek.fromEndPoint : null;

                        if (rtPeek == null)
                        {
                            doneRTWithBufferList.RemoveAt(0);
                        }
                        if (rtPeek.ex != null)
                        {
                            doneRTWithBufferList.RemoveAt(0);

                            SessionBase.HandleTransportException(qpcTimeStamp, fromEndPoint, rtPeek.ex);
                        }
                        else if (doneRTWithBufferList.Count == 1 && rtPeek.buffer != null)
                        {
                            doneRTWithBufferList.RemoveAt(0);

                            SessionBase.HandleInboundBuffers(qpcTimeStamp, fromEndPoint, rtPeek.buffer);
                        }
                        else
                        {
                            RecvTracker[] rtSetArray = doneRTWithBufferList.FilterAndRemove(rt => Object.Equals(rt.fromEndPoint, fromEndPoint) && rt.buffer != null && rt.ex == null).ToArray();
                            Buffers.Buffer[] bufferArray = rtSetArray.Select(rt => rt.buffer).ToArray();

                            trace.Emit("Deliverying {0} from {1}", rtSetArray.Length, fromEndPoint);
                            bufferArray.DoForEach(buffer => { trace.Emit("{0}", buffer); });

                            string fromIPEndPointStr = fromEndPoint.SafeToString();
                            bool anyMismatch = rtSetArray.Any(rt => rt.fromEndPoint.SafeToString() != fromIPEndPointStr);
                            if (anyMismatch)
                            {
                                logger.Error.Emit("{0}: mismatch fromIPEndPoint found", Fcns.CurrentMethodName);
                            }

                            SessionBase.HandleInboundBuffers(qpcTimeStamp, fromEndPoint, bufferArray);
                        }
                    }
                }
            }

            private void HandleBeginRecvAsyncRequestCallback(IAsyncResult iar)
            {
                RecvTracker rt = (iar != null) ? (iar.AsyncState as RecvTracker) : null;

                if (rt != null)
                {
                    try
                    {
                        rt.buffer.byteCount = rt.udpSocket.EndReceiveFrom(iar, ref rt.fromEndPoint);

                        rt.buffer.SetState(QpcTimeStamp.Now, Buffers.BufferState.Received, "EndReceiveFrom completed");

                        trace.Emit("rt:{0:x4} EndReceive succeeded [{1} from {2}]", rt.seqNum & 0x0ffff, rt.buffer, rt.fromEndPoint);
                    }
                    catch (System.Exception ex)
                    {
                        rt.ex = ConvertExceptionIfNeededAndLog(ex, Fcns.CurrentMethodName);
                    }

                    rt.done = true;
                }

                HostNotifier.Notify();
            }

            #endregion

            #region exception mapping 

            public System.Exception ConvertExceptionIfNeededAndLog(System.Exception ex, string callerMethodName)
            {
                ex = ex ?? new System.Exception("Given null exception");

                System.Net.Sockets.SocketException se = ex as System.Net.Sockets.SocketException;
                System.Net.Sockets.SocketError sError = unchecked((System.Net.Sockets.SocketError)(se != null ? se.ErrorCode : 0));

                if (sError == SocketError.ConnectionReset)
                {
                    if (!IsDisposingOrDisposed && !inRelease)
                        logger.Trace.Emit("{0} caught unexpected exception: {1} [mapping to SessionExceptionType.TrafficRejectedByRemoteEnd]", callerMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));

                    ex = new SessionException(ex.Message, ex, SessionExceptionType.TrafficRejectedByRemoteEnd);
                }
                else
                {
                    if (!IsDisposingOrDisposed && !inRelease)
                        logger.Debug.Emit("{0} caught unexpected exception: {1}", callerMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                }

                return ex;
            }

            #endregion
        }

        #endregion

        #region TCPTransport

        /// <summary>
        /// This enum is used to control which TCPTransport opimizations the client would like to use.
        /// <para/>None (0x00), EarlyMessageDelivery (0x01), EarlyBufferDelivery (0x02), OptimizedDefault (0x01), All (0xffffffff)
        /// </summary>
        [Flags]
        public enum TCPOptimizations : uint
        {
            /// <summary>Select no optimizations (0x00)</summary>
            None = 0x00,

            /// <summary>Set message start and message middle buffers as delivered on send complete by skipping the sent state [0x01]</summary>
            EarlyMessageDelivery = 0x01,

            /// <summary>(EarlyMessageDelivery)</summary>
            OptimizedDefault = (EarlyMessageDelivery),

            /// <summary>All bits set</summary>
            All = 0xffffffff,
        }

        public class TCPTransport : DisposableBase, ITransportConnection
        {
            private static AtomicUInt32 instanceNumGen = new AtomicUInt32();

            public const int DefaultNumConcurrentAccepts = 2;

            public TCPTransport(INamedValueSet connParamsNVS, ITransportServerSessionManagerFacet sessionManager)
                : this(TransportRole.Server, connParamsNVS, sessionManager)
            {
                ServerSession = sessionManager;

                TransportTypeFeatures = TransportTypeFeatures.Stream | TransportTypeFeatures.Reliable | TransportTypeFeatures.Server;

                Service(QpcTimeStamp.Now);
            }

            public TCPTransport(INamedValueSet connParamsNVS, ITransportConnectionSessionFacet session)
                : this(TransportRole.Client, connParamsNVS, session)
            {
                ClientSession = session;

                TransportTypeFeatures = TransportTypeFeatures.Stream | TransportTypeFeatures.Reliable | TransportTypeFeatures.Client;

                Service(QpcTimeStamp.Now);
            }

            public TransportTypeFeatures TransportTypeFeatures { get; private set; } 

            public TCPTransport(TransportRole role, INamedValueSet connParamsNVS, ITransportSessionFacetBase sessionBase)
            {
                AddExplicitDisposeAction(Release);

                InstanceNum = instanceNumGen.Increment();
                Role = role;
                ConnParamsNVS = (connParamsNVS = connParamsNVS.ConvertToReadOnly(mapNullToEmpty: true));

                SessionBase = sessionBase;
                BufferPool = sessionBase.BufferPool;
                HostNotifier = sessionBase.HostNotifier;

                sessionBase.TransportParamsNVS = ConnParamsNVS;

                minReceiveBufferSize = BufferPool.BufferSize * TcpBufferRunHeaderV1.maxBuffersInRun * TcpBufferRunHeaderV1.size;
                minSendBufferSize = Math.Max(minReceiveBufferSize, sessionBase.Config.MaxBufferWriteAheadCount * BufferPool.BufferSize + TcpBufferRunHeaderV1.size);

                try
                {
                    Port = ConnParamsNVS["Port"].VC.GetValue<int>(rethrow: false, defaultValue: Constants.DefaultTCPPort);
                    KeepAlivePeriod = ConnParamsNVS["KeepAlivePeriod"].VC.GetValue<TimeSpan>(rethrow: false, defaultValue: (10.0).FromSeconds());
                    ConnectionType = ConnParamsNVS["ConnectionType"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
                    TCPOptimizations = ConnParamsNVS["TCPOptimizations"].VC.GetValue<TCPOptimizations>(rethrow: false, defaultValue: TCPOptimizations.OptimizedDefault);

                    IPV6 = ConnectionType.EndsWith("v6");

                    logger = new Logging.Logger("{0}_{1}{2:d2}_Port{3:d5}".CheckedFormat(ConnectionType, Role, InstanceNum, Port));

                    traceLogger = new Logging.Logger("{0}.Trace".CheckedFormat(logger.Name), groupName: Logging.LookupDistributionGroupName, initialInstanceLogGate: connParamsNVS["Transport.TraceLogger.InitialInstanceLogGate"].VC.GetValue(rethrow: false, defaultValue: Logging.LogGate.Debug));
                    trace = traceLogger.Trace;

                    switch (Role)
                    {
                        case TransportRole.Client:
                            if (connParamsNVS.Contains("HostName"))
                            {
                                HostName = connParamsNVS["HostName"].VC.GetValue<string>(rethrow: false);
                            }
                            else if (connParamsNVS.Contains("IPAddress"))
                            {
                                IPAddress ipAddress;
                                if (IPAddress.TryParse(connParamsNVS["IPAddress"].VC.GetValue<string>(rethrow: false).MapNullToEmpty(), out ipAddress))
                                    IPAddress = ipAddress;
                                else
                                    IPAddress = IPAddress.None;
                            }
                            else
                            {
                                IPAddress = IPV6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
                            }

                            ConnectionTracker ct = new ConnectionTracker()
                            {
                                bufferPool = BufferPool,
                                hostNotifier = HostNotifier,
                                tcpClient = null,
                                tcpSocket = null,
                                remoteHostName = HostName,
                                remotePort = Port,
                                remoteIPAddress = IPAddress,
                                connectionSession = null,                   // these will be filled in just prior to calling BeginConnect
                                emitter = logger.Debug,
                                traceEmitter = trace,
                            }.SetState(ConnectionTrackerState.WaitingForConnectRequest, "Initializing connection to {0}".CheckedFormat((!HostName.IsNullOrEmpty() ? "host:{0}".CheckedFormat(HostName) : "address:{0}".CheckedFormat(IPAddress))));

                            connectionTrackerList.Add(ct);

                            break;

                        case TransportRole.Server:
                            NumConcurrentAccepts = connParamsNVS["NumConcurrentAccepts"].VC.GetValue<int>(rethrow: false, defaultValue: DefaultNumConcurrentAccepts);

                            IPAddress = IPV6 ? IPAddress.IPv6Any : IPAddress.Any;
                            ServerIPEndPoint = new IPEndPoint(IPAddress, Port);

                            tcpListener = new TcpListener(ServerIPEndPoint);

                            tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                            tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                            tcpListener.Start(NumConcurrentAccepts);

                            LocalEndPoint = tcpListener.LocalEndpoint;

                            logger.Debug.Emit("LocalEndPoint: {0}", LocalEndPoint);

                            break;
                        default:
                            throw new TransportConnectionFactoryException("Unable to construct {0}: Role {1} is not valid.  {2}".CheckedFormat(Fcns.CurrentClassLeafName, Role, connParamsNVS.ToStringSML()));
                    }

                    if (IsClient)
                    {
                        if (IPAddress != null)
                            logger.Trace.Emit("Connects to address:'{0}' port:{1}", IPAddress, Port);
                        else
                            logger.Trace.Emit("Connects to host:'{0}' port:{1}", HostName, Port);
                    }
                }
                catch (System.Exception ex)
                {
                    throw new TransportConnectionFactoryException("Construction of {0} {1} with settings {2} generated unexpected exception {3}".CheckedFormat(Role, Fcns.CurrentClassLeafName, connParamsNVS.ToStringSML(), ex.ToString(ExceptionFormat.TypeAndMessage)), ex);
                }
            }

            void Release()
            {
                inRelease = true;

                if (tcpListener != null)
                    tcpListener.Stop();

                Service(QpcTimeStamp.Now);

                Fcns.DisposeOfObject(ref tcpListener);

                ReleaseConnectionTrackers();

                inRelease = false;
            }

            private bool inRelease = false;

            public uint InstanceNum { get; private set; }
            public TransportRole Role { get; private set; }
            public bool IsServer { get { return Role == TransportRole.Server; } }
            public bool IsClient { get { return Role == TransportRole.Client; } }
            public INamedValueSet ConnParamsNVS { get; private set; }

            public ITransportSessionFacetBase SessionBase { get; private set; }
            public ITransportConnectionSessionFacet ClientSession { get; private set; }
            public ITransportServerSessionManagerFacet ServerSession { get; private set; }

            public Buffers.BufferPool BufferPool { get; private set; }
            public INotifyable HostNotifier { get; private set; }

            public string ConnectionType { get; private set; }
            public bool IPV6 { get; private set; }
            public AddressFamily SelectedAddressFamily { get { return (IPV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork); } }
            public int Port { get; private set; }
            public TimeSpan KeepAlivePeriod { get; private set; }
            public string HostName { get; private set; }
            public IPAddress IPAddress { get; private set; }
            public IPEndPoint ServerIPEndPoint { get; private set; }
            public EndPoint LocalEndPoint { get; private set; }

            public int NumConcurrentAccepts { get; private set; }
            public TCPOptimizations TCPOptimizations 
            { 
                get { return _tcpOptimizations; } 
                private set 
                { 
                    _tcpOptimizations = value;
                    EnableEarlyMessageDelivery = value.IsSet(TCPOptimizations.EarlyMessageDelivery);
                } 
            }
            public TCPOptimizations _tcpOptimizations;
            public bool EnableEarlyMessageDelivery { get; private set; }

            public int minReceiveBufferSize;
            public int minSendBufferSize;

            Logging.Logger logger;
            Logging.Logger traceLogger;
            Logging.IMesgEmitter trace;
            TcpListener tcpListener;

            public int Service(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                if (IsServer)
                    count += ServicePostedAccepts(qpcTimeStamp);

                count += ServiceConnectionTrackers(qpcTimeStamp);

                return count;
            }

            #region ToString

            /// <summary>Debugging helper method</summary>
            public override string ToString()
            {
                if (IsClient && ClientSession != null)
                    return "{0} ConnectionSessionState:{1} ConnectionTrackerState:{2}".CheckedFormat(logger.Name, ClientSession.State, String.Join(",", connectionTrackerList.Select(ct => ct.State)));
                else
                    return "{0} ConnectionTrackerStates:{1}".CheckedFormat(logger.Name, String.Join(",", connectionTrackerList.Select(ct => ct.State)));
            }

            #endregion

            #region tcpListener Accept Tracking and handling

            List<AcceptTracker> postedAcceptList = new List<AcceptTracker>();

            private class AcceptTracker
            {
                private static AtomicUInt32 instanceNumGen = new AtomicUInt32();
                public uint instanceNum = instanceNumGen.Increment();
                public TcpListener tcpListener;
                public IAsyncResult iar;
                public System.Exception ex;
                public TcpClient acceptedTcpClient;
                public volatile bool done;
            }

            private int ServicePostedAccepts(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                try
                {
                    if (postedAcceptList.Count > 0 && postedAcceptList.Any(at => at.done))
                    {
                        AcceptTracker[] succeededAcceptTrackerArray = postedAcceptList.FilterAndRemove(at => at.done && at.acceptedTcpClient != null && at.acceptedTcpClient.Client != null && at.ex == null).ToArray();
                        AcceptTracker[] failedAcceptTrackerArray = postedAcceptList.FilterAndRemove(at => at.done && (at.ex != null || at.acceptedTcpClient == null || at.acceptedTcpClient.Client == null)).ToArray();

                        if (succeededAcceptTrackerArray.Length > 0)
                            succeededAcceptTrackerArray.DoForEach(at => { HandleCompletedAccept(at); count++; });

                        if (failedAcceptTrackerArray.Length > 0)
                        {
                            logger.Debug.Emit("{0}: {1} accept trackers failed", failedAcceptTrackerArray.Length);
                        }
                    }

                    while (postedAcceptList.Count < NumConcurrentAccepts && !IsDisposingOrDisposed && !inRelease)
                    {
                        AcceptTracker at = new AcceptTracker() { tcpListener = tcpListener };

                        logger.Trace.Emit("at:{0:x4} calling BeginAcceptTcpClient [{1}]", at.instanceNum & 0x0ffff, LocalEndPoint);

                        postedAcceptList.Add(at);

                        at.iar = tcpListener.BeginAcceptTcpClient(HandleBeginAcceptAsyncRequestCallback, at);

                        count++;
                    }
                }
                catch (System.Exception ex)
                {
                    if (!IsDisposingOrDisposed && !inRelease)
                    {
                        logger.Debug.Emit("{0} caught unexpected exception: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));

                        SessionBase.HandleTransportException(qpcTimeStamp, LocalEndPoint, ex);
                    }
                    else
                    {
                        logger.Trace.Emit("{0} caught exception during release: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                    }

                    count++;
                }

                return count;
            }

            private void HandleCompletedAccept(AcceptTracker at)
            {
                EndPoint localEndPoint = at.acceptedTcpClient.Client.LocalEndPoint;
                EndPoint remoteEndPoint = at.acceptedTcpClient.Client.RemoteEndPoint;

                trace.Emit("at:{0:x4} accepted [from {1}]", at.instanceNum & 0x0ffff, remoteEndPoint);

                at.acceptedTcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, (int)KeepAlivePeriod.TotalSeconds);
                at.acceptedTcpClient.LingerState = new LingerOption(false, 0);

                if (at.acceptedTcpClient.ReceiveBufferSize < minReceiveBufferSize)
                    at.acceptedTcpClient.ReceiveBufferSize = minReceiveBufferSize;
                if (at.acceptedTcpClient.SendBufferSize < minSendBufferSize)
                    at.acceptedTcpClient.SendBufferSize = minSendBufferSize;

                ConnectionTracker ct = new ConnectionTracker()
                {
                    bufferPool = BufferPool,
                    hostNotifier = HostNotifier,
                    tcpClient = at.acceptedTcpClient,
                    tcpSocket = at.acceptedTcpClient.Client,
                    localEndPoint = localEndPoint as IPEndPoint,
                    remoteEndPoint = remoteEndPoint as IPEndPoint,
                    connectionSession = null,    // session has not been opened yet
                    emitter = logger.Debug,
                    traceEmitter = trace,
                }.SetState(ConnectionTrackerState.Ready, "Accepted connection from {0}".CheckedFormat(remoteEndPoint));

                connectionTrackerList.Add(ct);
            }

            private void HandleBeginAcceptAsyncRequestCallback(IAsyncResult iar)
            {
                AcceptTracker at = (iar != null) ? (iar.AsyncState as AcceptTracker) : null;

                if (IsDisposingOrDisposed || inRelease)
                { }
                else if (at != null)
                {
                    try
                    {
                        at.acceptedTcpClient = at.tcpListener.EndAcceptTcpClient(iar);

                        trace.Emit("at:{0:x4} EndAcceptTcpClient succeeded [from: {1}]", at.instanceNum & 0x0ffff, at.acceptedTcpClient.Client.RemoteEndPoint);
                    }
                    catch (System.Exception ex)
                    {
                        at.ex = ex ?? new System.Exception("Caught null exception");

                        if (!IsDisposingOrDisposed && !inRelease)
                            logger.Debug.Emit("{0} caught unexpected exception: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                        else
                            logger.Trace.Emit("{0} caught exception during release: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                    }

                    at.done = true;
                }
                else
                {
                    logger.Debug.Emit("{0} received with invalid IAR: AsyncState was not an AcceptTracker", Fcns.CurrentMethodName);
                }

                HostNotifier.Notify();
            }

            #endregion

            #region ConnectionTracker, ConnectionState and reception tracking.

            private class ConnectionTracker
            {
                private static int seqNumSource = 0;
                public int seqNum = seqNumSource++;

                public Buffers.BufferPool bufferPool;
                public INotifyable hostNotifier;

                public TcpClient tcpClient;
                public Socket tcpSocket;
                public IPEndPoint localEndPoint;
                public string remoteHostName;
                public int remotePort;
                public volatile IPAddress remoteIPAddress;
                public IPEndPoint remoteEndPoint;
                public ITransportConnectionSessionFacet connectionSession;

                public Logging.IMesgEmitter emitter;
                public Logging.IMesgEmitter traceEmitter;

                public ConnectionTrackerState State { get; private set; }

                /// <summary>
                /// Returns true if tcpClient != null in IssueConnectRequest and WaitingForConnected states, returns false if state is Closed or Unusable, returns true if tcpClient, tcpSocket, and remoteEndpoint are all non-null in all other states.
                /// </summary>
                public bool IsUsable 
                { 
                    get 
                    {
                        switch (State)
                        {
                            case ConnectionTrackerState.WaitingForConnectRequest:
                                return !remoteHostName.IsNullOrEmpty() || remoteIPAddress != null;
                            case ConnectionTrackerState.WaitingForHostIPAddress:
                                return !remoteHostName.IsNullOrEmpty();
                            case ConnectionTrackerState.WaitingForConnected:
                                return tcpClient != null;

                            case ConnectionTrackerState.Closed:
                            case ConnectionTrackerState.Unusable:
                                return false;

                            default:
                                return tcpClient != null && tcpSocket != null && remoteEndPoint != null;
                        }
                    } 
                }

                public byte[] incommingBufferRunHeaderByteArray = new byte[TcpBufferRunHeaderV1.size];
                public int incommingBufferRunHeaderByteCount;
                public TcpBufferRunHeaderV1 incommingBufferRunHeader;
                public int[] bufferRunBufferLenArray;

                public List<SendTracker> postedSendList = new List<SendTracker>();
                public RecvTracker pendingRT;

                public volatile System.Exception ex;

                public ConnectionTracker SetState(ConnectionTrackerState state, string reason, System.Exception ex = null)
                {
                    ConnectionTrackerState entryState = State;

                    Logging.IMesgEmitter stateEmitter = emitter;

                    switch (state)
                    {
                        case ConnectionTrackerState.Ready:
                        case ConnectionTrackerState.WaitingForBufferRunHeader:
                        case ConnectionTrackerState.ReadyToIssueBufferReceives:
                        case ConnectionTrackerState.ReissuePartiallyCompleteBufferRunHeader:
                        case ConnectionTrackerState.WaitingForPendingBufferReceives:
                        case ConnectionTrackerState.ReissuePartiallyCompleteReceive:
                        case ConnectionTrackerState.ProcessReceivedBuffers:
                            stateEmitter = traceEmitter;
                            break;
                        case ConnectionTrackerState.InformSessionOfException:
                            ex = ex ?? new System.Exception("Given exception was null");
                            break;
                    }

                    this.ex = ex;
                    State = state;

                    if (ex == null)
                        (stateEmitter ?? Logging.NullEmitter).Emit("ct:{0:x4} State changed to {1} [from: {2}, reason: '{3}']", seqNum & 0x0ffff, state, entryState, reason ?? "NoReasonGiven");
                    else
                        (emitter ?? Logging.NullEmitter).Emit("ct:{0:x4} State Changed to {1} [from: {2}, reason: '{3}', ex: {4}]", seqNum & 0xffff, state, entryState, reason ?? "NoReasonGiven", ex.ToString(ExceptionFormat.TypeAndMessage));

                    return this;
                }

                public void Release(string reason)
                {
                    SetState(TCPTransport.ConnectionTrackerState.Closed, reason ?? Fcns.CurrentMethodName);

                    Fcns.DisposeOfObject(ref tcpClient);
                    tcpSocket = null;

                    postedSendList.Clear();

                    if (connectionSession != null)
                        connectionSession.NoteTransportIsClosed(QpcTimeStamp.Now, remoteEndPoint, "Connection is being released: {0}".CheckedFormat(reason));
                }

                public void HandleAsynchException(string caller, System.Exception ex)
                {
                    if (State != ConnectionTrackerState.Closed)
                        SetState(ConnectionTrackerState.InformSessionOfException, "Received unexpected exception", ex);
                }
  
                public override string ToString()
                {
                    return "ct:{0:x4} fromEP:{1} state:{2}".CheckedFormat(seqNum & 0xffff, remoteEndPoint, State);
                }
            }

            /// <summary>
            /// ConnectionTracker State values.  NOTE: some states are only used for client roles.
            /// <para/>None (0), WaitingForConnectRequest, WaitingForConnected, TellSessionTransportIsConnected, Ready, WaitingForBufferRunHeader, ReadyToIssueBufferReceives, WaitingForPendingBufferReceives, ProcessReceivedBuffers, InformSessionOfException, Unusable, Closed
            /// </summary>
            enum ConnectionTrackerState : int
            {
                /// <summary>Placeholder default value (0)</summary>
                None = 0,

                /// <summary>Client Role only:  Wait until the session indicates that would like to connect/reconnect to the target.</summary>
                WaitingForConnectRequest,

                /// <summary>Client Role only:  BeginGetHostIPAddress has been issued, waiting for IAsyncResult callback to be performed which will change state to WaitingforConnected.</summary>
                WaitingForHostIPAddress,

                /// <summary>Client Role only:  BeginConnect has been issued, waiting for IAsyncResult callback to be performed which will change state to TellSessionTransportIsConnected.</summary>
                WaitingForConnected,

                /// <summary>Used to inform the client session that the transport has been connected - either as an initiator, or as a target/servant.</summary>
                TellSessionTransportIsConnected,

                /// <summary>This state indicates that the connetion is ready to attempt to start receiving the next buffer run header.  Normally this state issues a BeginReceive for the buffer run header.</summary>
                Ready,

                /// <summary>Process partially complete reception of buffer run header and re-issue BeginRecieve for remaining buffers.</summary>
                ReissuePartiallyCompleteBufferRunHeader,

                /// <summary>Waiting for the next buffer run header (which will be signaled in relation to the IAsynchResult from the BRH BeginReceive).</summary>
                WaitingForBufferRunHeader,

                /// <summary>Process reception of buffer run header and issue BeginReceive for the individual buffers.</summary>
                ReadyToIssueBufferReceives,

                /// <summary>Process partially complete reception of expected buffers (from prior buffer run header) and re-issue BeginRecieve for remaining buffers.</summary>
                ReissuePartiallyCompleteReceive,

                /// <summary>Waiting for the BeginReceive for individual buffers to complete.</summary>
                WaitingForPendingBufferReceives,

                /// <summary>After receipt of one or more buffers this state is used to pass the buffers to the connection session.</summary>
                ProcessReceivedBuffers,

                /// <summary>This state is used to inform the connection session of the captured occurance of a connection related exception</summary>
                InformSessionOfException,

                /// <summary>Indicates that the connection has been closed.</summary>
                Closed,

                /// <summary>Indicates that an internal error, or low level protocol violation has occurred such that this connection object can no longer be used.</summary>
                Unusable,
            }

            private class RecvTracker
            {
                private static int seqNumSource = 0;
                public int seqNum = seqNumSource++;
                public ConnectionTracker ct;
                public Buffers.Buffer [] bufferArray;
                public List<ArraySegment<byte>> bufferArraySegmentList;
                public IAsyncResult iar;
                public int expectedReadCount;
                public int receivedByteCount;
            }

            int ServiceConnectionTrackers(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;
                if (connectionTrackerList.Any(ct => ct == null || !ct.IsUsable))
                {
                    ConnectionTracker[] deadCTArray = connectionTrackerList.FilterAndRemove(ct => ct == null || !ct.IsUsable).ToArray();
                    foreach (var ct in deadCTArray.Where(item => item != null))
                    {
                        logger.Debug.Emit("Releasing Connection '{0}'", ct);
                        ct.Release("Non-usable connection found in service loop");
                        count++;
                    }
                }

                foreach (var ct in connectionTrackerList)
                {
                    try
                    {
                        ITransportConnectionSessionFacet connectionSession = ct.connectionSession;
                        if (connectionSession != null)
                        {
                            string closeRequestReason = connectionSession.CloseRequestReason;

                            if (!closeRequestReason.IsNullOrEmpty())
                            {
                                if (ct.State != ConnectionTrackerState.Closed && ct.State != ConnectionTrackerState.Unusable)
                                {
                                    string reason = "by client request: {0}".CheckedFormat(closeRequestReason);

                                    ct.SetState(ConnectionTrackerState.Closed, reason);
                                    ct.connectionSession.NoteTransportIsClosed(qpcTimeStamp, ct.remoteEndPoint, reason);

                                    Fcns.DisposeOfObject(ref ct.tcpClient);
                                    ct.tcpSocket = null;

                                    continue;
                                }
                            }
                        }

                        switch (ct.State)
                        {
                            case ConnectionTrackerState.WaitingForConnectRequest:
                                if (ct.connectionSession == null && ClientSession != null)
                                {
                                    ct.connectionSession = ClientSession;
                                    ct.connectionSession.HandleOutboundBuffersDelegate = (ts, ep, bufArray) => HandleConnectionOutboundBuffersDelegate(ts, ct, ep, bufArray);
                                }

                                if (ct.connectionSession != null)
                                {
                                    switch (ct.connectionSession.State.StateCode)
                                    {
                                        case SessionStateCode.RequestTransportConnect:
                                            if (!ct.remoteHostName.IsNullOrEmpty() && ct.remoteIPAddress == null)
                                            {
                                                ct.SetState(ConnectionTrackerState.WaitingForHostIPAddress, "issuing BeginGetHostAddresses({0}) ...".CheckedFormat(ct.remoteHostName));

                                                System.Net.Dns.BeginGetHostAddresses(ct.remoteHostName, HandleBeginGetHostAddressAsyncRequestCallback, ct);
                                            }
                                            else
                                            {
                                                IssueBeginConnect(ct);
                                            }

                                            count++;
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                break;

                            case ConnectionTrackerState.WaitingForHostIPAddress:
                                if (ct.remoteIPAddress != null)
                                {
                                    logger.Debug.Emit("Host name '{0}' has been resolved to address '{1}'", ct.remoteHostName, ct.remoteIPAddress);
                                    IssueBeginConnect(ct);
                                }

                                break;

                            case ConnectionTrackerState.WaitingForConnected:
                                // note: the CloseRequestReason logic above abort the connection attempt if the session stops waiting for it to finish opening.
                                // otherwise there is nothing to do here but wait
                                break;

                            case ConnectionTrackerState.TellSessionTransportIsConnected:
                                if (LocalEndPoint == null && ct.localEndPoint != null)
                                {
                                    LocalEndPoint = ct.localEndPoint;

                                    logger.Debug.Emit("LocalEndPoint: {0}", LocalEndPoint);
                                }

                                if (ct.connectionSession != null)
                                {
                                    ct.connectionSession.NoteTransportIsConnected(qpcTimeStamp, ct.remoteEndPoint);

                                    ct.SetState(ConnectionTrackerState.Ready, "Session has been told about transport connection");
                                    count++;
                                }

                                break;

                            case ConnectionTrackerState.Ready:
                                {
                                    ct.incommingBufferRunHeaderByteArray.Clear();
                                    ct.incommingBufferRunHeaderByteCount = 0;
                                    ct.incommingBufferRunHeader = TcpBufferRunHeaderV1.Empty;

                                    ct.SetState(ConnectionTrackerState.WaitingForBufferRunHeader, "issuing BeginReceive(BufferRunHeader)");

                                    ct.tcpSocket.BeginReceive(ct.incommingBufferRunHeaderByteArray, 0, TcpBufferRunHeaderV1.size, SocketFlags.None, HandleBufferRunHeaderBeginReceiveAsyncRequestCallback, ct);

                                    count++;
                                }
                                break;

                            case ConnectionTrackerState.ReissuePartiallyCompleteBufferRunHeader:
                                {
                                    ct.SetState(ConnectionTrackerState.WaitingForBufferRunHeader, "issuing BeginReceive(BufferRunHeader - remaining bytes)");

                                    ct.tcpSocket.BeginReceive(ct.incommingBufferRunHeaderByteArray, ct.incommingBufferRunHeaderByteCount, TcpBufferRunHeaderV1.size - ct.incommingBufferRunHeaderByteCount, SocketFlags.None, HandleBufferRunHeaderBeginReceiveAsyncRequestCallback, ct);

                                    count++;
                                }
                                break;

                            case ConnectionTrackerState.WaitingForBufferRunHeader:
                                break;   // nothing to do here but wait.

                            case ConnectionTrackerState.ReadyToIssueBufferReceives:
                                {
                                    Buffers.Buffer[] bufferArray = ct.bufferRunBufferLenArray.Select(len => ct.bufferPool.Acquire(qpcTimeStamp, ct.State.ToString()).Update(byteCount: len)).ToArray();
                                    List<ArraySegment<byte>> bufferArraySegmentList = ct.bufferRunBufferLenArray.Zip(bufferArray, (len, buffer) => new ArraySegment<byte>(buffer.byteArray, 0, len)).ToList();

                                    RecvTracker rt = new RecvTracker() { ct = ct, bufferArray = bufferArray, bufferArraySegmentList = bufferArraySegmentList, expectedReadCount = ct.bufferRunBufferLenArray.Sum() };

                                    rt.bufferArray.DoForEach(buffer => buffer.SetState(qpcTimeStamp, Buffers.BufferState.ReceivePosted, ct.State.ToString()));

                                    ct.pendingRT = rt;

                                    ct.SetState(ConnectionTrackerState.WaitingForPendingBufferReceives, "issuing BeginReceive(buffers)");

                                    rt.iar = ct.tcpSocket.BeginReceive(bufferArraySegmentList, SocketFlags.None, HandleBufferSegmentListBeginReceiveAsyncRequestCallback, rt);

                                    count++;
                                }
                                break;

                            case ConnectionTrackerState.ReissuePartiallyCompleteReceive:
                                {
                                    var rt = ct.pendingRT;

                                    List<ArraySegment<byte>> remainingBufferArraySegmentList = new List<ArraySegment<byte>>();

                                    int skipCount = rt.receivedByteCount;

                                    foreach (var t in ct.bufferRunBufferLenArray.Zip(rt.bufferArray, (len, buffer) => Tuple.Create(len, buffer)))
                                    {
                                        var bufferLen = t.Item1;
                                        var buffer = t.Item2;

                                        if (skipCount > bufferLen)
                                            skipCount -= bufferLen;
                                        else if (skipCount <= 0)
                                            remainingBufferArraySegmentList.Add(new ArraySegment<byte>(buffer.byteArray, 0, bufferLen));
                                        else
                                        {
                                            remainingBufferArraySegmentList.Add(new ArraySegment<byte>(buffer.byteArray, skipCount, bufferLen - skipCount));
                                            skipCount = 0;
                                        }
                                    }

                                    ct.SetState(ConnectionTrackerState.WaitingForPendingBufferReceives, "issuing BeingReceive(buffers - remaining bytes)");

                                    rt.bufferArraySegmentList = remainingBufferArraySegmentList;

                                    rt.iar = ct.tcpSocket.BeginReceive(rt.bufferArraySegmentList, SocketFlags.None, HandleBufferSegmentListBeginReceiveAsyncRequestCallback, rt);
                                }
                                break;

                            case ConnectionTrackerState.WaitingForPendingBufferReceives:
                                break;  // nothing to do here but wait.

                            case ConnectionTrackerState.ProcessReceivedBuffers:
                                {
                                    RecvTracker rt = ct.pendingRT;
                                    ct.pendingRT = null;

                                    if (ct.connectionSession != null && rt != null)
                                    {
                                        ct.connectionSession.HandleInboundBuffers(qpcTimeStamp, ct.remoteEndPoint, rt.bufferArray);

                                        ct.SetState(ConnectionTrackerState.Ready, "Received Buffers have been forwarded");
                                        count++;
                                    }
                                    else if (ct.connectionSession == null)
                                    {
                                        // ask the session manager to process the first buffer run (which generally contains a session request buffer).
                                        HandleBuffersDelegate newConnectionHandleOutboundBuffersDelegate = (ts, ep, bufArray) => HandleConnectionOutboundBuffersDelegate(ts, ct, ep, bufArray);

                                        ct.connectionSession = ServerSession.ProcessSessionLevelInboundBuffers(qpcTimeStamp, ct.remoteEndPoint, newConnectionHandleOutboundBuffersDelegate, rt.bufferArray);

                                        if (ct.connectionSession != null)
                                            ct.SetState(ConnectionTrackerState.Ready, "Received Buffers have been processed at session level");
                                        else 
                                            ct.SetState(ConnectionTrackerState.Ready, "Received Buffers have been ignored at session level");

                                        count++;
                                    }
                                    else
                                    {
                                        ct.SetState(ConnectionTrackerState.Unusable, "Connection state has no place to deliver the received buffers");
                                        count++;
                                    }
                                }
                                break;

                            case ConnectionTrackerState.InformSessionOfException:
                                if (ct.connectionSession != null)
                                {
                                    ct.connectionSession.HandleTransportException(qpcTimeStamp, ct.remoteEndPoint, ct.ex, endpointClosed: true);
                                    ct.SetState(ConnectionTrackerState.Unusable, "Session has been informed of transport exception");
                                }
                                else
                                {
                                    ct.SetState(ConnectionTrackerState.Unusable, "Session could not be informed of transport exception (no client session found)");
                                }
                                count++;
                                break;

                            default:
                                throw new System.InvalidOperationException("ConnectionState {0} is not valid or supported here".CheckedFormat(ct.State));
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ct.HandleAsynchException(Fcns.CurrentMethodName, ex);
                        count++;
                    }

                    count += ServiceConnectionSends(qpcTimeStamp, ct);
                }

                return count;
            }

            private void ReleaseConnectionTrackers()
            {
                connectionTrackerList.FilterAndRemove().DoForEach(ct => ct.Release(Fcns.CurrentMethodName));
            }

            List<ConnectionTracker> connectionTrackerList = new List<ConnectionTracker>();

            private void HandleBeginGetHostAddressAsyncRequestCallback(IAsyncResult iar)
            {
                ConnectionTracker ct = (iar != null) ? (iar.AsyncState as ConnectionTracker) : null;

                if (ct != null && ct.State != ConnectionTrackerState.Closed)
                {
                    try
                    {
                        var hostAddressArray = System.Net.Dns.EndGetHostAddresses(iar);

                        var firstFilteredAddress = hostAddressArray.Where(addr => (addr.AddressFamily == (IPV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork))).FirstOrDefault();

                        var firstAddress = firstFilteredAddress ?? hostAddressArray.FirstOrDefault();

                        if (firstAddress != null)
                            ct.remoteIPAddress = firstAddress;
                        else
                            throw new HostNotFoundException("GetHostByAddress '{0}' gave no valid addresses".CheckedFormat(ct.remoteHostName));
                    }
                    catch (System.Exception ex)
                    {
                        ct.HandleAsynchException(Fcns.CurrentMethodName, ex);
                    }

                    ct.hostNotifier.Notify();
                }
            }

            private void IssueBeginConnect(ConnectionTracker ct)
            {
                ct.remoteEndPoint = new IPEndPoint(ct.remoteIPAddress, ct.remotePort);

                ct.tcpClient = new TcpClient(ct.remoteIPAddress.AddressFamily);

                ct.SetState(ConnectionTrackerState.WaitingForConnected, "issuing BeginConnect({0}, ...)".CheckedFormat(ct.remoteEndPoint));

                ct.tcpClient.BeginConnect(ct.remoteEndPoint.Address, ct.remoteEndPoint.Port, HandleBeginConnectAsyncRequestCallback, ct);
            }

            private void HandleBeginConnectAsyncRequestCallback(IAsyncResult iar)
            {
                ConnectionTracker ct = (iar != null) ? (iar.AsyncState as ConnectionTracker) : null;

                if (ct != null && ct.tcpClient != null)
                {
                    try
                    {
                        ct.tcpClient.EndConnect(iar);

                        ct.tcpSocket = ct.tcpClient.Client;
                        ct.tcpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, (int) KeepAlivePeriod.TotalSeconds);

                        if (ct.tcpSocket.ReceiveBufferSize < minReceiveBufferSize)
                            ct.tcpSocket.ReceiveBufferSize = minReceiveBufferSize;
                        if (ct.tcpSocket.SendBufferSize < minSendBufferSize)
                            ct.tcpSocket.SendBufferSize = minSendBufferSize;
                        
                        ct.tcpClient.LingerState = new LingerOption(false, 0);

                        ct.localEndPoint = ct.tcpSocket.LocalEndPoint as IPEndPoint;

                        ct.SetState(ConnectionTrackerState.TellSessionTransportIsConnected, "EndConnect succeeded");
                    }
                    catch (System.Exception ex)
                    {
                        ct.HandleAsynchException(Fcns.CurrentMethodName, ex);
                    }

                    ct.hostNotifier.Notify();
                }
            }

            private void HandleBufferRunHeaderBeginReceiveAsyncRequestCallback(IAsyncResult iar)
            {
                ConnectionTracker ct = (iar != null) ? (iar.AsyncState as ConnectionTracker) : null;

                if (ct != null && ct.tcpSocket != null && ct.State != ConnectionTrackerState.Closed)
                {
                    try
                    {
                        int readCount = ct.tcpSocket.EndReceive(iar);

                        ct.incommingBufferRunHeaderByteCount += readCount;

                        if (ct.incommingBufferRunHeaderByteCount == 0)
                        {
                            ct.SetState(ConnectionTrackerState.Ready, "BRH EndReceive returned 0");
                        }
                        else if (ct.incommingBufferRunHeaderByteCount < TcpBufferRunHeaderV1.size)
                        {
                            ct.SetState(ConnectionTrackerState.ReissuePartiallyCompleteBufferRunHeader, "EndReceive gave {0} bytes of partial BRH [now have {1} of {2}]".CheckedFormat(readCount, ct.incommingBufferRunHeaderByteCount, TcpBufferRunHeaderV1.size));
                        }
                        else if (readCount != TcpBufferRunHeaderV1.size)
                        {
                            throw new TCPTransportProtocolViolation("Invalid Buffer run header: receive produced unexpected total byte count: {0}, expected total:{1}, this readCount:{2}".CheckedFormat(ct.incommingBufferRunHeaderByteCount, TcpBufferRunHeaderV1.size, readCount));
                        }
                        else
                        {
                            ct.incommingBufferRunHeader = ct.incommingBufferRunHeaderByteArray.MarshalStructFromByteArray<TcpBufferRunHeaderV1>(rethrow: true);

                            if (!ct.incommingBufferRunHeader.IsMagicValid)
                                throw new TCPTransportProtocolViolation("Received invalid buffer run header: Magic {0:x8} != expected {1:x8}".CheckedFormat(ct.incommingBufferRunHeader.Magic, TcpBufferRunHeaderV1.MagicValueV1));

                            ct.bufferRunBufferLenArray = ct.incommingBufferRunHeader.BufferLenArray.Where(len => len != 0).ToArray();
                            int firstBadBufferLength = ct.bufferRunBufferLenArray.Where(len => len > ct.bufferPool.BufferSize).FirstOrDefault();
                            if (firstBadBufferLength != 0)
                                throw new TCPTransportProtocolViolation("Received invalid buffer run header {0}: Requested Len {1} > MaxBufferLen {2}".CheckedFormat(ct, firstBadBufferLength, ct.bufferPool.BufferSize));

                            ct.SetState(ConnectionTrackerState.ReadyToIssueBufferReceives, "valid buffer run header received [{0}]".CheckedFormat(ct.incommingBufferRunHeader));
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ct.HandleAsynchException(Fcns.CurrentMethodName, ex);
                    }

                    ct.hostNotifier.Notify();
                }
            }

            private void HandleBufferSegmentListBeginReceiveAsyncRequestCallback(IAsyncResult iar)
            {
                RecvTracker rt = (iar != null) ? (iar.AsyncState as RecvTracker) : null;
                ConnectionTracker ct = (rt != null) ? rt.ct : null;

                if (ct != null && ct.tcpSocket != null && ct.State != ConnectionTrackerState.Closed)
                {
                    try
                    {
                        int readCount = ct.tcpSocket.EndReceive(iar);

                        rt.receivedByteCount += readCount;

                        if (rt.receivedByteCount < rt.expectedReadCount)
                            ct.SetState(ConnectionTrackerState.ReissuePartiallyCompleteReceive, "EndReceive gave {0} bytes of buffer data [now have {1} of {2}]".CheckedFormat(readCount, rt.receivedByteCount, rt.expectedReadCount));
                        else if (rt.receivedByteCount != rt.expectedReadCount)
                            throw new TCPTransportProtocolViolation("Invalid Buffer delivery: EndReceive produced unexpected byte count: {0}, expecting: {1} [from brh:{2}]".CheckedFormat(readCount, rt.expectedReadCount, ct.incommingBufferRunHeader));
                        else
                        {
                            QpcTimeStamp tsNow = QpcTimeStamp.Now;

                            foreach (var buffer in rt.bufferArray)
                            {
                                buffer.SetState(tsNow, Buffers.BufferState.Received, "EndReceive completed");

                                if (!buffer.header.IsPurposeCodeValid)
                                    throw new TCPTransportProtocolViolation("Received invalid Buffer: PurposeCode {0} is not valid".CheckedFormat(buffer.header.PurposeCode));
                            }

                            ct.SetState(ConnectionTrackerState.ProcessReceivedBuffers, "a run of buffers have been received [total bytes:{0} buffers:{1}]".CheckedFormat(readCount, rt.bufferArray.Length));
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ct.HandleAsynchException(Fcns.CurrentMethodName, ex);
                    }

                    ct.hostNotifier.Notify();
                }
            }

            public class TCPTransportProtocolViolation : System.Exception
            {
                public TCPTransportProtocolViolation(string message, System.Exception innerException = null) : base(message, innerException) 
                { }
            }

            #endregion

            #region Connection Send Tracking

            private class SendTracker
            {
                private static int seqNumSource = 0;
                public int seqNum = seqNumSource++;

                public ConnectionTracker ct;
                public TcpBufferRunHeaderV1 brHeader;
                public Buffers.Buffer [] bufferArray;
                public int totalByteCount;

                public IAsyncResult iar;
                public volatile bool done;
            }

            void HandleConnectionOutboundBuffersDelegate(QpcTimeStamp qpcTimeStamp, ConnectionTracker ct, object transportEndpoint, Buffers.Buffer[] bufferArray)
            {
                if (ct != null && !bufferArray.IsNullOrEmpty())
                {
                    try
                    {
                        foreach (var buffer in bufferArray)
                        {
                            if (buffer.State != Buffers.BufferState.SendPosted)
                                buffer.SetState(qpcTimeStamp, Buffers.BufferState.SendPosted, "transport is posting send");
                        }

                        int numBuffers = bufferArray.Length;
                        for (int offset = 0; offset < numBuffers; )
                        {
                            int passCount = Math.Min(numBuffers - offset, TcpBufferRunHeaderV1.maxBuffersInRun);

                            InnerPostSendOneBufferRun(ct, bufferArray, offset, passCount);

                            offset += passCount;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ct.HandleAsynchException(Fcns.CurrentMethodName, ex);
                    }
                }
            }

            void InnerPostSendOneBufferRun(ConnectionTracker ct, Buffers.Buffer [] bufferArray, int offset, int count)
            {
                if (ct != null && ct.tcpSocket != null && !IsDisposingOrDisposed && !inRelease)
                {
                    Buffers.Buffer[] bufferSubArray = bufferArray.SafeSubArray(offset, count);

                    SendTracker st = new SendTracker() { ct = ct, bufferArray = bufferSubArray };

                    st.brHeader = TcpBufferRunHeaderV1.EmptyWithMagic;

                    int[] bufferLenArray = (st.brHeader.BufferLenArray = bufferSubArray.Select(buffer => buffer.byteCount).ToArray());

                    ArraySegment<byte> brHeaderSegment = new ArraySegment<byte>(st.brHeader.MarshalStructToByteArray());

                    List<ArraySegment<byte>> bufferSegementList = brHeaderSegment.Concat(bufferSubArray.Select(buffer => new ArraySegment<byte>(buffer.byteArray, 0, buffer.byteCount))).ToList();

                    st.totalByteCount = TcpBufferRunHeaderV1.size + bufferLenArray.Sum();

                    ct.postedSendList.Add(st);

                    if (traceLogger.Trace.IsEnabled)
                        traceLogger.Trace.Emit("{0}: issuing BeginSend(Run {1})", Fcns.CurrentMethodName, st.brHeader);

                    st.iar = ct.tcpSocket.BeginSend(bufferSegementList, SocketFlags.None, HandleConnectionBeginSendAsynchRequestCallback, st);
                }
            }

            private int ServiceConnectionSends(QpcTimeStamp qpcTimeStamp, ConnectionTracker ct)
            {
                int count = 0;

                ct.postedSendList.FilterAndRemove(st => (st == null || st.done)).Where(st => st != null).DoForEach(st => { HandleCompletedSend(qpcTimeStamp, st); count++; });

                return count;
            }

            private void HandleCompletedSend(QpcTimeStamp qpcTimeStamp, SendTracker st)
            {
                string reason = Fcns.CurrentMethodName;

                foreach (var buffer in st.bufferArray)
                {
                    if (buffer.State == Buffers.BufferState.SendPosted)
                    {
                        bool markBufferDeliveredEarly = EnableEarlyMessageDelivery && (buffer.PurposeCode == Buffers.PurposeCode.MessageMiddle || buffer.PurposeCode == Buffers.PurposeCode.MessageStart);

                        // immediately mark message start or message middle buffers as having been delivered.  Only mark message or message end buffers as Sent (aka waiting for ack)
                        buffer.SetState(qpcTimeStamp, markBufferDeliveredEarly ? Buffers.BufferState.Delivered : Buffers.BufferState.Sent, reason);
                    }
                }
            }

            private void HandleConnectionBeginSendAsynchRequestCallback(IAsyncResult iar)
            {
                SendTracker st = (iar != null) ? (iar.AsyncState as SendTracker) : null;
                ConnectionTracker ct = (st != null) ? st.ct : null;

                if (ct != null && ct.tcpSocket != null && ct.State != ConnectionTrackerState.Closed)
                {
                    try
                    {
                        int sendCount = ct.tcpSocket.EndSend(iar);

                        if (sendCount != st.totalByteCount)
                            throw new TCPTransportProtocolViolation("Invalid Buffer delivery: EndSend produced unexpected byte count: {0}, expecting: {1} [from brh:{2}]".CheckedFormat(sendCount, st.totalByteCount, st.brHeader));

                        st.done = true;
                    }
                    catch (System.Exception ex)
                    {
                        ct.HandleAsynchException(Fcns.CurrentMethodName, ex);
                    }

                    ct.hostNotifier.Notify();
                }
            }

            #endregion

            #region TCPBufferRunHeaderV1

            /// <summary>
            /// Version 1 of the TCP Buffer Run Header.  [Size = 4 + 16 * 4 = 68]
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            public struct TcpBufferRunHeaderV1
            {
                /// <summary>Returns the serialized size of this header using GetMarshaledByteArraySize</summary>
                public static readonly int size = typeof(TcpBufferRunHeaderV1).GetMarshaledByteArraySize(rethrow: false);

                /// <summary>Defines the maximum number of buffers in a single run.  Currently 16</summary>
                public const int maxBuffersInRun = 16;

                /// <summary>Constant magic value used as the first 4 bytes in a V1 header (0x6a231f00 + maxBuffersInRun)</summary>
                public const uint MagicValueV1 = 0x6a231f00 + maxBuffersInRun;

                /// <summary>First 4 bytes of any given buffer header.  Used as basic heuristic to confirm protocol version and delivery alignment.</summary>
                public uint Magic;

                /// <summary>Returns true if the current header's Magic matches the expected value.</summary>
                public bool IsMagicValid { get { return Magic == MagicValueV1; } }

                [MarshalAs(UnmanagedType.ByValArray, SizeConst=maxBuffersInRun)]
                private int[] bufferLenArray;

                public int[] BufferLenArray
                {
                    get { return bufferLenArray ?? (bufferLenArray = new int [maxBuffersInRun]); }
                    set 
                    {
                        if (bufferLenArray != null)
                            bufferLenArray.Clear();
                        else
                            bufferLenArray = new int[maxBuffersInRun];

                        bufferLenArray.SafeCopyFrom(value);

                        if (value.SafeLength() > maxBuffersInRun)
                            throw new System.ArgumentOutOfRangeException("BufferLenArray", "Array length must be between 0 and {0}".CheckedFormat(maxBuffersInRun));
                    }
                }

                /// <summary>IEquatable{BufferHeaderV1} implementation method.  Returns true if both headers have the same contents.</summary>
                public bool Equals(TcpBufferRunHeaderV1 other)
                {
                    return (Magic == other.Magic
                            && BufferLenArray.IsEqualTo(other.BufferLenArray)
                            );
                }

                public override string ToString()
                {
                    return "{0} {1}".CheckedFormat(((Magic == MagicValueV1) ? "Magic" : "!Magic"), String.Join(",", BufferLenArray.Select(len => len.ToString())));
                }

                public bool IsEmpty { get { return (this.Equals(EmptyWithMagic) || this.Equals(Empty)); } }

                public static readonly TcpBufferRunHeaderV1 Empty = new TcpBufferRunHeaderV1();
                public static readonly TcpBufferRunHeaderV1 EmptyWithMagic = new TcpBufferRunHeaderV1() { Magic = TcpBufferRunHeaderV1.MagicValueV1 };
            }

            #endregion
        }

        /// <summary>
        /// Exception thrown if an attempt to find a host fails to return any IPAddresses.
        /// </summary>
        public class HostNotFoundException : System.Exception
        {
            /// <summary>Default constructor</summary>
            public HostNotFoundException(string mesg, System.Exception innerException = null)
                : base(mesg, innerException)
            { }
        }

        #endregion

        #region PatchPanelTransport, PatchPanel

        public class PatchPanelTransport : DisposableBase, ITransportConnection
        {
            private static AtomicUInt32 instanceNumGen = new AtomicUInt32();

            #region Construction, Release, related fields

            public PatchPanelTransport(INamedValueSet connParamsNVS, ITransportServerSessionManagerFacet sessionManager, PatchPanel patchPanel, string serverPortName)
                : this(TransportRole.Server, connParamsNVS, sessionManager, patchPanel, serverPortName)
            {
                ServerSession = sessionManager;

                TransportTypeFeatures = TransportTypeFeatures.Message | TransportTypeFeatures.Server;

                Service(QpcTimeStamp.Now);
            }

            public PatchPanelTransport(INamedValueSet connParamsNVS, ITransportConnectionSessionFacet session, PatchPanel patchPanel, string serverPortName)
                : this(TransportRole.Client, connParamsNVS, session, patchPanel, serverPortName)
            {
                ConnectionSession = session;

                TransportTypeFeatures = TransportTypeFeatures.Message | TransportTypeFeatures.Client;

                Service(QpcTimeStamp.Now);
            }

            public TransportTypeFeatures TransportTypeFeatures { get; private set; } 

            public PatchPanelTransport(TransportRole role, INamedValueSet connParamsNVS, ITransportSessionFacetBase sessionBase, PatchPanel patchPanel, string serverPortName)
            {
                AddExplicitDisposeAction(Release);

                InstanceNum = instanceNumGen.Increment();
                Role = role;
                InstanceName = "{0}{1}_{2:d2}".CheckedFormat(Fcns.CurrentClassLeafName, Role, InstanceNum);

                logger = new Logging.Logger(InstanceName);
                traceLogger = new Logging.Logger("{0}.Trace".CheckedFormat(logger.Name), groupName: Logging.LookupDistributionGroupName, initialInstanceLogGate: connParamsNVS["Transport.TraceLogger.InitialInstanceLogGate"].VC.GetValue(rethrow: false, defaultValue: Logging.LogGate.Debug));
                traceEmitter = traceLogger.Trace;

                ConnParamsNVS = (connParamsNVS = connParamsNVS.ConvertToReadOnly(mapNullToEmpty: true));

                TransmitDelayCount = ConnParamsNVS["TransmitDelayCount"].VC.GetValue<int>(rethrow: false);
                TransmitReorderInterval = ConnParamsNVS["TransmitReorderInterval"].VC.GetValue<int>(rethrow: false);
                TransmitReorderAdditionalDelay = ConnParamsNVS["TransmitReorderAdditionalDelay"].VC.GetValue<int>(rethrow: false, defaultValue: 1);

                SessionBase = sessionBase;
                BufferPool = sessionBase.BufferPool;
                HostNotifier = sessionBase.HostNotifier;

                PatchPanel = patchPanel;
                ServerPortName = serverPortName;

                if (IsServer)
                {
                    LocalPort = PatchPanel.FindPort(serverPortName, createIfNeeded: true);
                }
                else if (IsClient)
                {
                    LocalPort = PatchPanel.FindPort(InstanceName, createIfNeeded: true);
                    ConnectedToPort = PatchPanel.FindPort(serverPortName);
                }

                lock (LocalPort.Mutex)
                {
                    LocalPort.NotifyOnRx = HostNotifier;
                    LocalPort.InboundBufferStateEmitter = BufferPool.BufferStateEmitter;
                    LocalPort.IsOpen = true;
                }

                sessionBase.TransportParamsNVS = ConnParamsNVS;
                sessionBase.HandleOutboundBuffersDelegate = HandleOutboundBuffers;

                logger.Debug.Emit("LocalPort: {0}", LocalPort);

                if (IsClient)
                    logger.Debug.Emit("ConnectsToServer: {0}", ServerPortName);
            }

            void Release()
            {
                lock (LocalPort.Mutex)
                {
                    LocalPort.IsOpen = false;
                }
            }

            public uint InstanceNum { get; private set; }
            public TransportRole Role { get; private set; }
            public string InstanceName { get; private set; }
            public bool IsServer { get { return Role == TransportRole.Server; } }
            public bool IsClient { get { return Role == TransportRole.Client; } }
            public INamedValueSet ConnParamsNVS { get; private set; }

            public int TransmitDelayCount { get; private set; }
            public int TransmitReorderInterval { get; private set; }
            public int TransmitReorderAdditionalDelay { get; private set; }

            public ITransportSessionFacetBase SessionBase { get; private set; }
            public ITransportConnectionSessionFacet ConnectionSession { get; private set; }
            public ITransportServerSessionManagerFacet ServerSession { get; private set; }

            public PatchPanel PatchPanel { get; private set; }
            public string ServerPortName { get; private set; }
            public PatchPanel.Port LocalPort { get; private set; }
            public PatchPanel.Port ConnectedToPort { get; private set; }
            public List<PatchPanel.Port> RecentPortsByPortNumList = new List<PatchPanel.Port>();

            public Buffers.BufferPool BufferPool { get; private set; }
            public INotifyable HostNotifier { get; private set; }

            Logging.Logger logger;
            Logging.Logger traceLogger;
            Logging.IMesgEmitter traceEmitter;

            #endregion

            #region ToString

            /// <summary>Debugging helper method</summary>
            public override string ToString()
            {
                if (IsClient && ConnectionSession != null)
                    return "{0} ConnectionSessionState:{1} LocalPort:[{2}] ConnectedToPort:[{3}]".CheckedFormat(InstanceName, ConnectionSession.State, LocalPort, ConnectedToPort);
                else
                    return "{0} LocalPort:[{1}]".CheckedFormat(InstanceName, LocalPort);
            }

            #endregion

            #region Service, ServiceSessionState

            public int Service(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                count += ServicePostedSends(qpcTimeStamp);
                count += ServicePostedRecv(qpcTimeStamp);
                count += ServiceSessionState(qpcTimeStamp);

                return count;
            }

            private int ServiceSessionState(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                if (ConnectionSession != null)
                {
                    switch (ConnectionSession.State.StateCode)
                    {
                        case SessionStateCode.RequestTransportConnect:
                            if (ConnectedToPort == null)
                                ConnectedToPort = PatchPanel.FindPort(ServerPortName);

                            if (ConnectedToPort != null)
                            {
                                ConnectionSession.NoteTransportIsConnected(qpcTimeStamp);
                                count++;
                            }

                            break;
                        default:
                            break;
                    }
                }

                return count;
            }

            #endregion

            #region Sending

            List<PatchPanel.BufferDeliveryItem> txBDIList = new List<PatchPanel.BufferDeliveryItem>();
            List<PatchPanel.BufferDeliveryItem> pendingBDIList = new List<PatchPanel.BufferDeliveryItem>();

            private int ServicePostedSends(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                lock (LocalPort.Mutex)
                {
                    if (LocalPort.outboundBufferList.Count > 0)
                    {
                        LocalPort.outboundBufferList.DoForEach(bdi => { if (bdi.forwardingDelayCount > 0) bdi.forwardingDelayCount--; });

                        txBDIList.AddRange(LocalPort.outboundBufferList.FilterAndRemove(bdi => (bdi.forwardingDelayCount == 0)));
                    }
                }

                while (txBDIList.Count > 0)
                {
                    PatchPanel.BufferDeliveryItem firstBDIItem = txBDIList[0];
                    int toPortNum = firstBDIItem.toPortNum;

                    PatchPanel.BufferDeliveryItem [] consecutiveBdisToPortNumArray = txBDIList.FilterAndRemove(bdi => bdi.toPortNum == toPortNum, consecutive: true, fromFront: true).ToArray();

                    PatchPanel.Port toPort = ConnectedToPort;

                    if (toPort == null || toPort.PortNum != toPortNum)
                        toPort = RecentPortsByPortNumList.SafeAccess(toPortNum);

                    if (toPort == null)
                    {
                        toPort = PatchPanel.GetPort(toPortNum);

                        if (toPort != null)
                        {
                            while (RecentPortsByPortNumList.Count <= toPortNum)
                                RecentPortsByPortNumList.Add(null);

                            RecentPortsByPortNumList[toPortNum] = toPort;
                        }
                    }

                    if (toPort != null)
                    {
                        PatchPanel.BufferDeliveryItem[] bdisCopyArray = consecutiveBdisToPortNumArray.Select(bdi => MakeCloneOf(bdi, inboundBufferStateEmitter: toPort.InboundBufferStateEmitter)).ToArray();

                        INotifyable notifyOnRx = null;

                        lock (toPort.Mutex)
                        {
                            toPort.inboundBuffersList.AddRange(bdisCopyArray);
                            notifyOnRx = toPort.NotifyOnRx;
                        }

                        consecutiveBdisToPortNumArray.DoForEach(bdi => { bdi.delivered = true; });

                        pendingBDIList.AddRange(consecutiveBdisToPortNumArray);

                        traceEmitter.Emit("Delivered {0} buffers to port {1}", bdisCopyArray.Length, toPort);

                        if (notifyOnRx != null)
                            notifyOnRx.Notify();
                    }
                    else
                    {
                        logger.Debug.Emit("{0}: Cannot find toPortNum:{1} [Dropped {2} buffers]", Fcns.CurrentMethodName, toPortNum, consecutiveBdisToPortNumArray.Length);
                    }

                    count++;
                }

                if (pendingBDIList.Count > 0)
                {
                    PatchPanel.BufferDeliveryItem[] deliveredBDIArray = pendingBDIList.FilterAndRemove(bdi => bdi.delivered).ToArray();

                    foreach (var bdi in deliveredBDIArray)
                    {
                        if (bdi.buffer.State == Buffers.BufferState.SendPosted)
                            bdi.buffer.SetState(qpcTimeStamp, Buffers.BufferState.Sent, Fcns.CurrentMethodName);
                    }
                }

                return count;
            }

            private static PatchPanel.BufferDeliveryItem MakeCloneOf(PatchPanel.BufferDeliveryItem fromBDI, int forwardingDelayCount = 0, Logging.IMesgEmitter inboundBufferStateEmitter = null)
            {
                PatchPanel.BufferDeliveryItem toBDI = new PatchPanel.BufferDeliveryItem()
                {
                    toPortNum = fromBDI.toPortNum,
                    fromPortNum = fromBDI.fromPortNum,
                    forwardingDelayCount = forwardingDelayCount,
                };

                Buffers.Buffer copyFromBuffer = fromBDI.buffer;
                Buffers.Buffer copyToBuffer = new Buffers.Buffer(fromBDI.buffer.byteArraySize, stateEmitter: inboundBufferStateEmitter);

                copyToBuffer.byteArray.SafeCopyFrom(copyFromBuffer.byteArray, 0, copyFromBuffer.byteCount);
                copyToBuffer.byteCount = Math.Min(copyFromBuffer.byteCount, copyToBuffer.byteArraySize);
                copyToBuffer.UpdateHeaderFromByteArray();

                toBDI.buffer = copyToBuffer;

                return toBDI;
            }

            int transmitBufferCount = 0;

            private void HandleOutboundBuffers(QpcTimeStamp qpcTimeStamp, object transportEndPoint, params Buffers.Buffer[] bufferParamsArray)
            {
                bufferParamsArray.DoForEach(buffer => buffer.CopyHeaderToByteArray());

                int toPortNum = (transportEndPoint is int) ? ((int) transportEndPoint) : 0;
                if (toPortNum == 0 && ConnectedToPort != null)
                    toPortNum = ConnectedToPort.PortNum;

                PatchPanel.BufferDeliveryItem [] sendBDIArray = bufferParamsArray.Select(buffer => new PatchPanel.BufferDeliveryItem() { fromPortNum = LocalPort.PortNum, toPortNum = toPortNum, forwardingDelayCount = TransmitDelayCount, buffer = buffer }).ToArray();
                int sendCount = sendBDIArray.Length;

                if (TransmitReorderInterval > 0)
                {
                    for (int idx = 0; idx < sendCount; idx++)
                    {
                        if (++transmitBufferCount > TransmitReorderInterval)
                        {
                            transmitBufferCount = 0;
                            sendBDIArray[idx].forwardingDelayCount += TransmitReorderAdditionalDelay;
                        }
                    }
                }

                lock (LocalPort.Mutex)
                {
                    LocalPort.outboundBufferList.AddRange(sendBDIArray);
                }

                ServicePostedSends(qpcTimeStamp);

                traceEmitter.Emit("Enqueued {0} buffers for later delivery in port {1}", sendBDIArray.Length, LocalPort);
            }

            #endregion

            #region Receiving

            List<PatchPanel.BufferDeliveryItem> rxBDIList = new List<PatchPanel.BufferDeliveryItem>();

            private int ServicePostedRecv(QpcTimeStamp qpcTimeStamp)
            {
                int count = 0;

                lock (LocalPort.Mutex)
                {
                    if (LocalPort.inboundBuffersList.Count > 0)
                    {
                        LocalPort.inboundBuffersList.DoForEach(bdi => { if (bdi.forwardingDelayCount > 0) bdi.forwardingDelayCount--; });

                        rxBDIList.AddRange(LocalPort.inboundBuffersList.FilterAndRemove(bdi => (bdi.forwardingDelayCount == 0)));
                    }
                }

                while (rxBDIList.Count > 0)
                {
                    PatchPanel.BufferDeliveryItem firstBDIItem = rxBDIList[0];
                    int fromPortNum = firstBDIItem.fromPortNum;

                    PatchPanel.BufferDeliveryItem[] consecutiveBdisFromPortNumArray = rxBDIList.FilterAndRemove(bdi => bdi.fromPortNum == fromPortNum, consecutive: true, fromFront: true).ToArray();

                    SessionBase.HandleInboundBuffers(qpcTimeStamp, fromPortNum, consecutiveBdisFromPortNumArray.Select(bdi => bdi.buffer).ToArray());

                    consecutiveBdisFromPortNumArray.DoForEach(bdi => { bdi.delivered = true; });

                    traceEmitter.Emit("Delivered {0} buffers from Port {1} to Session {2}", consecutiveBdisFromPortNumArray.Length, LocalPort, SessionBase.SessionName);

                    count += consecutiveBdisFromPortNumArray.Length;
                }

                HostNotifier.Notify();

                return count;
            }

            #endregion
        }

        public class PatchPanel
        {
            public Port FindPort(string portName, bool createIfNeeded = false)
            {
                lock (mutex)
                {
                    Port port = portNameToPortDictionary.SafeTryGetValue(portName);

                    if (port == null && createIfNeeded)
                    {
                        port = new Port()
                        {
                            PortNum = portList.Count,
                            PortName = portName,
                            Mutex = mutex,
                        };

                        portList.Add(port);
                        portNameToPortDictionary[portName] = port;
                    }

                    return port;
                }
            }

            public Port GetPort(int portNum)
            {
                lock (mutex)
                {
                    return portList.SafeAccess(portNum);
                }
            }

            public object mutex = new object();

            public class BufferDeliveryItem
            {
                public int toPortNum;
                public int fromPortNum;
                public int forwardingDelayCount;

                public Buffers.Buffer buffer;
                public volatile bool delivered;

                public override string ToString()
                {
                    return "To:{0} From:{1} Delay:{2} Buffer:{3}{4}".CheckedFormat(toPortNum, fromPortNum, forwardingDelayCount, buffer, (delivered ? " Delivered" : ""));
                }
            
                public void SwapFromAndToPortNums()
                {
                    int tempPortNum = toPortNum;
                    toPortNum = fromPortNum;
                    toPortNum = tempPortNum;
                }
            }

            public class Port
            {
                public int PortNum { get; set; }
                public string PortName { get; set; }
                public object Mutex { get; set; }
                public bool IsOpen { get; set; }
                public INotifyable NotifyOnRx { get; set; }
                public Logging.IMesgEmitter InboundBufferStateEmitter { get; set; }

                public List<BufferDeliveryItem> outboundBufferList = new List<BufferDeliveryItem>();
                public List<BufferDeliveryItem> inboundBuffersList = new List<BufferDeliveryItem>();

                public override string ToString()
                {
                    return "Port:{0} '{1}' {2} out#:{3} in#:{4}".CheckedFormat(PortNum, PortName, (IsOpen ? "Open" : "Closed"), outboundBufferList.Count, inboundBuffersList.Count);
                }
            }

            public Dictionary<string, Port> portNameToPortDictionary = new Dictionary<string, Port>();
            public List<Port> portList = new List<Port>() { null };
        }

        #endregion
    }
}

//-------------------------------------------------------------------
