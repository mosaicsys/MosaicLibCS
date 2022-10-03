//-------------------------------------------------------------------
/*! @file E037Port.cs
 *  @brief This file defines common types, constants, and methods that are used with semi E037 Ports.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
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

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.Attributes;
using MosaicLib.Semi.E005;
using MosaicLib.Semi.E005.Port;
using MosaicLib.Semi.E005.Manager;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Time;

namespace MosaicLib.Semi.E037
{
    //-------------------------------------------------------------------
	/// <remarks>
	/// HSMS Packet Format (14 + n bytes total)
	///		0: 4 byte unsigned packet length
	///		4: 10 byte HSMS Message Header
	///	   14: n bytes of PacketData (per length - 10)
	///	   
	/// length field defines the number of bytes that follow the length in the packet including the mandetory 10 byte HSMS Message Header and all 
	/// contents specific PacketData bytes that follow it.  Minimum length value is 10 and minimum total packet size is 14.
	/// </remarks>
	/// 

	/// <remarks>
	/// HSMS Mesg Header (10 bytes)
	///		0: 2 byte Session ID/Device ID
	///		2: 1 byte HeaderByte2
	///		3: 1 byte HeaderByte3
	///		4: 1 byte PType (presentation type)
	///		5: 1 byte SType (session type)
	///		6: 4 bytes System Bytes
	///		
	/// SessionID - UInt16 field that passes the SessionID for most traffic (0x0000 through 0x7fff are legal).  
	///			SessionID is determined by initiator of a SelectReq SType packet.
	///			0xFFFF for LinkTestReq and LinkTestRsp SType packets.  Copied from message being rejected for RejectReq SType packets.
	///			0xFFFF used for SessionID on all HSMS-SS connections.
	/// HeaderByte2 - 1 byte
	///			Carries E005 Stream and W bit for DataMessage SType packets,
	///			Carries PType or SType of rejected message for RejectReq SType packets.
	///	HeaderByte3 - 1 byte
	///			Carries E005 Function for all DataMessage SType packets,
	///			Carries Select Status in SelectRsp SType packets
	///			Carries Deselect Status in DeselectRsp SType packets
	///			Carries Reason Code in RejectReq SType packets
	/// PType - 1 byte, 0 in all cases
	/// SType - 1 byte, see enum below
	/// System Bytes - 4 byte UInt32 transaction identifier.  
	///			Must be unique in any Primary DataMessage and in all Req SType packets (except RejectReq).  
	///			Must match value from corresponding Primary DataMessage or in all Rsp SType packets.
	///			Matches message being rejected for all RejectReq SType packets.
	///			
	/// Note PacketData must be empty for all SType packets except for DataMessage SType packets.
	/// </remarks>

	public enum PType : byte
	{
		Invalid = 255,
		SECSII = 0,
	}

	public enum SType : byte
	{
		Invalid = 255,
		DataMessage = 0,
		SelectReq = 1,
		SelectRsp = 2,
		DeselectReq = 3,
		DeselectRsp = 4,
		LinktestReq = 5,
		LinktestRsp = 6,
		RejectReq = 7,          // NOTE: this SType is really a response type even though it is called Reject.Req
		// 8 is not used
		SeparateReq = 9,
		// 10 is not used
		// 11..127 reserved for subsidiary standards
		// 128..255 reserved, not used
	}

	public enum SelectStatus : byte
	{
		CommunicationEstablished = 0,
		CommunicationAlreadyActive = 1,
		ConnectionNotReady = 2,
		ConnectionExhaust = 3,
	}

	public enum DeslectStatus : byte
	{
		CommunicationEnded = 0,
		CommunicationNotEstablished = 1,
		CommunicationBusy = 2,
		NoSuchEntity = 4,			// HSMS-GS
		EntityInUse = 5,			// HSMS-GS
		EntitySelected = 6,			// HSMS-GS
	}

	public enum RejectReasonCode : byte
	{
		STypeNotSupported = 1,
		PTypeNotSupported = 2,
		TransactionNotOpen = 3,
		EntityNotSelected = 4,
	}

	//-------------------------------------------------------------------

    /// <summary>This class contains the fields and properties that are used to fully configured the operation of an E005 Port.</summary>
    public class E037PortConfig
    {
        /// <summary>Defines unsigned short E037 HSMS SessionID (0..32767) of supported sessionID for Passive Port, or of intended session for Active Port.  [Defaults to 0]</summary>
        [NamedValueSetItem]
        public UInt16 SessionID = 0;

        /// <summary>For Active ports, this gives the target HostName.  It is ignored for Passive ports or when a non-empty IPAddress is specified.</summary>
        [NamedValueSetItem]
        public string HostName;

        /// <summary>When non-empty this defines the IPAddress that is to be used as the connection target with Active ports, or as the interface address to bind to with Passive ports.</summary>
        [NamedValueSetItem]
        public string IPAddress;

        /// <summary>This gives the TCP port number to connect to or to listen on.  [Defaults to 5000]</summary>
        [NamedValueSetItem]
        public int PortNum = 5000;

        /// <summary>When non-zero this value is used to define the TCP keepalive test interval.  [Defaults to 10 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan KeepAlivePeriod = (10.0).FromSeconds();

        /// <summary>Defines the maximum amount of time that the port will use when attempting to resolve a host name to an address.  [Defaults to 5 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan HostNameLookupTimeLimit = (5.0).FromSeconds();

        /// <summary>Defines the maximum amount of time that the port will use when attempting to connect to the remote server.  [Defaults to 5 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan ConnectTimeLimit = (5.0).FromSeconds();

        /// <summary>Defines the maximum amount of time that the port will use when attempting to gracefully close a tcp connection after deselecting/seperating.  [Defaults to 1 second]</summary>
        [NamedValueSetItem]
        public TimeSpan DisconnectTimeLimit = (1.0).FromSeconds();

        /// <summary>Defines T5 timer value: Connect Seperation Timeout (aka Reconnect Holdoff).  [Defaults to 10 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan T5_ConnectSeperationTO = TimeSpan.FromSeconds(10.0);

        /// <summary>Defines T6 timer value: Control Transaction Timeout:  Specifies the maximum amount of time a requesting entity must wait for a Control Transaction response before deciding that the other side will never send it.  [Defaults to 5 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan T6_ControlTransactionTO = TimeSpan.FromSeconds(5.0);

        /// <summary>Defines T7 timer value: Not Selected Timeout: Defines the maximum amount of time that an E037 port may be in the NotSelected state before it is identified as a communication failure.  [Defaults to 10 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan T7_NotSelectedTO = TimeSpan.FromSeconds(10.0);

        /// <summary>Defines T8 timer value: Network Inter Character Timeout.  Defines the maximum amount of time that may elapse between characters in a single HSMS message before the connection is determined to have failed.  [Defaults to 5 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan T8_NetworkInterCharTO = TimeSpan.FromSeconds(5.0);

        /// <summary>Set this to non-zero period to enable use of port's link test facility (if any).  [Defaults to 10 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan IdleLineLinkTestInterval = TimeSpan.FromSeconds(10.0);

        /// <summary>Defines the maximum number of concurrent socket send requests that this port supports</summary>
        [NamedValueSetItem]
        public int MaxConcurrentPostedSends = 20;

        /// <summary>Defines the maximum amount of time that the port will wait in the Deselecting state when attempting to gracefully close the port.  [Defaults to 1.0 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan DeselectTimeLimit = TimeSpan.FromSeconds(1.0);

        [NamedValueSetItem]
        public Logging.MesgType LinkTestMesgType = Logging.MesgType.Trace;

        public E037PortConfig UpdateFromNVS(INamedValueSet nvs, string keyPrefix = "", Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueNoteEmitter = null)
        {
            NamedValueSetAdapter<E037PortConfig> adapter = new NamedValueSetAdapter<E037PortConfig>() { ValueSet = this, IssueEmitter = issueEmitter, ValueNoteEmitter = valueNoteEmitter }.Setup(keyPrefix).Set(nvs, merge: true);

            MaxConcurrentPostedSends = MaxConcurrentPostedSends.Clip(1, 100);

            return this;
        }
    }

	/// <summary>
    /// Provide and implementation of the E005.PortBase that is used to manage an E037 active or passive connection to send and receive HSMS messages.
    /// </summary>
	public class E037Port : PortBase
    {
        #region Construction and realted fields/properties

        public E037Port(string partID, int portNum, PortType portType, INamedValueSet portConfigNVS, IManagerPortFacet managerPortFacet)
            : base(partID, portNum, portType, portConfigNVS, managerPortFacet)
		{
            isPassivePort = (PortType == PortType.E037_Passive_SingleSession);
            isActivePort = (PortType == PortType.E037_Active_SingleSession);
            isSingleSession = (PortType == PortType.E037_Passive_SingleSession || PortType == PortType.E037_Active_SingleSession);
            useSessionID = (isSingleSession ? (ushort)0xffff : E037PortConfig.SessionID);
            useDeviceID = PortBaseConfig.DeviceID;

            E037PortConfig = new E037PortConfig().UpdateFromNVS(portConfigNVS, issueEmitter: Log.Debug, valueNoteEmitter: Log.Trace);

            LinkTestEmitter = TraceLogger.Emitter(E037PortConfig.LinkTestMesgType);

            AddMainThreadStoppingAction(() => { TerminateTcpClient(); TerminateTcpListener(); });

            SetupReceiever();
		}

        private E037PortConfig E037PortConfig { get; set; }

        private Logging.IMesgEmitter LinkTestEmitter { get; set; }

        #endregion

        #region connection information

        protected readonly ushort useSessionID;
        protected readonly ushort useDeviceID;
        protected readonly bool isPassivePort;
        protected readonly bool isActivePort;
        protected readonly bool isSingleSession;

        private IPAddress IPAddress { get; set; }
        private TcpListener tcpListener = null;
        private IAsyncResult pendingAcceptIAR = null;

        private TcpClient tcpClient = null;
        private readonly int maxConcurrentAccepts = 1;

        private string AttemptToSetupAndMakeConnectionIfNeeded()
        {
            if (IPAddress == null)
            {
                if (!E037PortConfig.PortNum.IsInRange(1, ushort.MaxValue))
                    return "Invalid port configuration: PortNum {0} is not valid".CheckedFormat(E037PortConfig.PortNum);

                if (E037PortConfig.IPAddress.IsNeitherNullNorEmpty())
                {
                    System.Net.IPAddress ipAddress;
                    if (System.Net.IPAddress.TryParse(E037PortConfig.IPAddress, out ipAddress))
                        IPAddress = ipAddress;
                    else
                        return "Invalid port configuration: IPAddress '{0}' is not valid".CheckedFormat(E037PortConfig.IPAddress);
                }
                else if (E037PortConfig.HostName.IsNeitherNullNorEmpty() && !isPassivePort)
                {
                    try
                    {
                        Log.Debug.Emit("Attempting to resolve host name '{0}'", E037PortConfig.HostName);

                        QpcTimeStamp startTime = QpcTimeStamp.Now;

                        var iar = System.Net.Dns.BeginGetHostAddresses(E037PortConfig.HostName, null, null);

                        if (!iar.AsyncWaitHandle.WaitOne(E037PortConfig.HostNameLookupTimeLimit))
                            return "Time limit reached while attempting to resolve host name '{0}' [after {1:f3} seconds]".CheckedFormat(E037PortConfig.HostName, startTime.Age.TotalSeconds);

                        var ipAddressSet = System.Net.Dns.EndGetHostAddresses(iar).ToArray();

                        IPAddress = ipAddressSet.Where(ipAddress => ipAddress.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();

                        if (IPAddress == null)
                            return "Invalid port configuration: no address could be resolved for host name '{0}'".CheckedFormat(E037PortConfig.HostName);
                    }
                    catch (System.Exception ex)
                    {
                        return "Invalid port configuration: attempt to resolve host name '{0}' failed with exception: {1}".CheckedFormat(E037PortConfig.HostName, ex.ToString(ExceptionFormat.TypeAndMessage));
                    }
                }
                else if (isPassivePort)
                {
                    IPAddress = System.Net.IPAddress.Any;
                }
                else
                {
                    return "Invalid port configuration: One of IPAddress or HostName must be non-empty";
                }
            }

            if (isActivePort && tcpClient == null)
            {
                try
                {
                    var ipEndpoint = new IPEndPoint(IPAddress, E037PortConfig.PortNum);

                    Log.Debug.Emit("Attempting to connect to '{0}'", ipEndpoint);

                    tcpClient = new TcpClient(IPAddress.AddressFamily);

                    QpcTimeStamp startTime = QpcTimeStamp.Now;

                    IAsyncResult iar;

                    if (IPAddress != null)
                    {
                        iar = tcpClient.BeginConnect(IPAddress, E037PortConfig.PortNum, null, null);

                        if (!iar.AsyncWaitHandle.WaitOne(E037PortConfig.ConnectTimeLimit))
                            return "Time limit reached while attempting to connect to '{0}:{1}' [after {2:f3} seconds]".CheckedFormat(IPAddress, E037PortConfig.PortNum, startTime.Age.TotalSeconds);
                    }
                    else
                    {
                        iar = tcpClient.BeginConnect(E037PortConfig.HostName, E037PortConfig.PortNum, null, null);

                        if (!iar.AsyncWaitHandle.WaitOne(E037PortConfig.ConnectTimeLimit))
                            return "Time limit reached while attempting to connect to host '{0}' port {1} [after {2:f3} seconds]".CheckedFormat(E037PortConfig.HostName, E037PortConfig.PortNum, startTime.Age.TotalSeconds);
                    }

                    tcpClient.EndConnect(iar);

                    if (!E037PortConfig.KeepAlivePeriod.IsZero())
                        tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, (int)E037PortConfig.KeepAlivePeriod.TotalSeconds);

                    tcpClient.LingerState = new LingerOption(false, 0);
                    tcpClient.NoDelay = true;

                    if (IPAddress != null)
                        Log.Debug.Emit("Connected to '{0}' using local endpoint '{1}", tcpClient.Client.RemoteEndPoint, tcpClient.Client.LocalEndPoint);
                    else
                        Log.Debug.Emit("Connected to host '{0}' endpoint '{1}' using local endpoint '{2}", E037PortConfig.HostName, tcpClient.Client.RemoteEndPoint, tcpClient.Client.LocalEndPoint);
                }
                catch (SocketException socketEx)
                {
                    switch (socketEx.SocketErrorCode)
                    {
                        case SocketError.ConnectionRefused:
                            return "Attempt to connect to host name '{0}' was rejected [{1}]".CheckedFormat(E037PortConfig.HostName, socketEx.Message);
                        default:
                            return "Attempt to connect to host name '{0}' failed [{1} {2}]".CheckedFormat(E037PortConfig.HostName, socketEx.SocketErrorCode, socketEx.Message);
                    }
                }
                catch (System.Exception ex)
                {
                    return "Attempt to connect to host name '{0}' failed with unexpected exception: {1}".CheckedFormat(E037PortConfig.HostName, ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }
            else if (isPassivePort && tcpListener == null)
            {
                try
                {
                    TerminateTcpClient();

                    tcpListener = new System.Net.Sockets.TcpListener(IPAddress, E037PortConfig.PortNum);

                    tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                    tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    tcpListener.Start(maxConcurrentAccepts);

                    Log.Debug.Emit("Listening on local endpoint '{0}'", tcpListener.LocalEndpoint);
                }
                catch (System.Exception ex)
                {
                    return "Invalid port configuration: attempt to resolve host name '{0}' failed with exception: {1}".CheckedFormat(E037PortConfig.HostName, ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }
            else
            {
                return "Invalid port configuration: Port Type {0} is not supported here".CheckedFormat(PortType);
            }

            return string.Empty;
        }

        private void TerminateTcpClient(bool closedNormally = false)
        {
            linktestSendTracker.Clear();
            slidingBuffer.ResetBuffer();

            if (tcpClient != null)
            {
                try
                {
                    if (!closedNormally)
                        Log.Debug.Emit("Releasing tcpClient");
                    else
                        Log.Debug.Emit("Releasing tcpClient after close");

                    Fcns.DisposeOfObject(ref tcpClient);
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit("{0} failed: tcpClient.Dispose threw unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                }

                tcpClient = null;
                pendingReceiveIAR = null;
                pendingSendIARList.Clear();
            }
        }

        private void TerminateTcpListener(bool closedNormally = false)
        {
            if (tcpListener != null)
            {
                try
                {
                    if (!closedNormally)
                        Log.Debug.Emit("Releasing tcpListener");
                    else
                        Log.Debug.Emit("Releasing tcpListener normally");

                    if (tcpListener.Server != null)
                        tcpListener.Server.Close();

                    Fcns.DisposeOfObject(ref tcpListener);
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit("{0} failed: tcpListener.Dispose threw unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                }

                tcpListener = null;
                pendingAcceptIAR = null;
            }
        }

        #endregion

        #region PerformGoOnlineAction, PerformGoOfflineAction

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            string ec = null;

            var entryPortConnectionState = PortConnectionState;

            if (isActivePort)
                SetConnectionState(PortConnectionState.Connecting, "Issuing Port.GoOnline", QpcTimeStamp.Now);

            if (andInitialize && tcpClient != null)
            {
                // try to quickly deselect from other side.
                AttemptToDeselectAndCloseTcpClient("Port reinitializing connection");
            }
            else
            {
                linktestSendTracker.Clear();
                slidingBuffer.ResetBuffer();
            }

            if (andInitialize || (isPassivePort && tcpListener == null))
            {
                TerminateTcpListener();
                ec = AttemptToSetupAndMakeConnectionIfNeeded();
            }
            else if ((isActivePort && tcpClient == null))
            {
                ec = AttemptToSetupAndMakeConnectionIfNeeded();
            }

            if (ec.IsNeitherNullNorEmpty())
            {
                SetConnectionState(PortConnectionState.Failed, "Port.GoOnline failed: {0}".CheckedFormat(ec), QpcTimeStamp.Now);
                return ec;
            }

            // determine if port is passive type or active type
            if (isPassivePort)
            {
                PerformMainLoopService();

                if (tcpClient != null)
                    SetConnectionState(PortConnectionState.NotSelected, "Passive port went online (already connected)", QpcTimeStamp.Now);
                else
                    SetConnectionState(PortConnectionState.NotConnected, "Passive port went online (listening)", QpcTimeStamp.Now);

                return ec;
            }
            else if (!isSingleSession || entryPortConnectionState != PortConnectionState.Selected) // we have already excluded the possiblity of !IsPassivePort && !IsActivePort
            {
                // for active connection connect to the remote port and then peform a select operation
                ec = StartPortSelect("Active Port.GoOnline succeeded.  Issuing Select to remote side", QpcTimeStamp.Now);

                Time.QpcTimer selectFailedTimer = new MosaicLib.Time.QpcTimer() { TriggerInterval = E037PortConfig.T7_NotSelectedTO, Started = true };

                while (String.IsNullOrEmpty(ec))
                {
                    WaitForSomethingToDo(TimeSpan.FromSeconds(0.1));

                    PerformMainLoopService();

                    if (PortConnectionState == PortConnectionState.Selected)
                        break;

                    if (selectFailedTimer.IsTriggered)
                        ec = "Time limit reached at {0:f3} seconds while waiting for Selected".CheckedFormat(selectFailedTimer.ElapsedTimeInSeconds);
                }

                if (ec.IsNullOrEmpty())
                    return ec;  // success

                SetConnectionState(PortConnectionState.Failed, "Port.GoOnline failed: {0}".CheckedFormat(ec), QpcTimeStamp.Now);

                return ec;
            }
            else
            {
                // single session where goonline has been called while already selected. - just do a link test in place of a select
                ec = SendLinktestHeader(QpcTimeStamp.Now);

                while (ec.IsNullOrEmpty())
                {
                    WaitForSomethingToDo(TimeSpan.FromSeconds(0.1));

                    PerformMainLoopService();

                    if (PortConnectionState != PortConnectionState.Selected)
                        ec = "Port.GoOnline failed: Lost Selected state while waiting for link test response [{0}]".CheckedFormat(PortConnectionState);
                    else if (linktestSendTracker.ResultCode == string.Empty)
                        break;
                    else if (linktestSendTracker.ResultCode != null)
                        ec = linktestSendTracker.ResultCode;
                }

                if (ec.IsNullOrEmpty())
                    return ec;  // success

                SetConnectionState(PortConnectionState.Failed, "Port.GoOnline failed: {0}".CheckedFormat(ec), QpcTimeStamp.Now);

                return ec;
            }
        }

        protected override string PerformGoOfflineAction()
        {
            // if we are selected then attempt to send a deselect with a relatively short timeout and then
            // use the base class operation to hang up on the connection

            if (tcpClient != null)
            {
                // try to quickly deselect from other side.
                AttemptToDeselectAndCloseTcpClient("Port is being set Offline");
            }

            TerminateTcpListener(closedNormally: PortConnectionState == E005.Port.PortConnectionState.NotConnected);

            SetConnectionState(PortConnectionState.OutOfService, "Port has been set Offline", QpcTimeStamp.Now);

            return string.Empty;
        }

        protected void AttemptToDeselectAndCloseTcpClient(string reason, bool setStateToDeselecting = true)
        {
            if (tcpClient != null)
            {
                QpcTimeStamp qpcTimeStamp = QpcTimeStamp.Now;

                // try to quickly deselect from other side.
                if (setStateToDeselecting)
                    SetConnectionState(PortConnectionState.Deselecting, reason, qpcTimeStamp);
                else
                    Log.Debug.Emit("Attempting to deslect and close tcpClient [{0}]", reason);

                if (!isSingleSession)
                {
                    StartPortDeselect("Port is being set Offline", qpcTimeStamp);
                    Time.QpcTimer quickDeselectTimer = new MosaicLib.Time.QpcTimer() { TriggerIntervalInSec = 1.0, Started = true };

                    for (; ; )
                    {
                        WaitForSomethingToDo(TimeSpan.FromSeconds(0.01));

                        qpcTimeStamp = QpcTimeStamp.Now;

                        ServiceReceiver(qpcTimeStamp);
                        ServiceTransmitter(qpcTimeStamp);

                        if (PortConnectionState != PortConnectionState.Deselecting)
                            break;

                        if (quickDeselectTimer.IsTriggered)
                        {
                            Log.Warning.Emit("GoOffline: timeout waiting for connection Deslect to complete after {0:f3} seconds", quickDeselectTimer.ElapsedTimeInSeconds);
                            break;
                        }
                    }
                }

                // If the deselect failed then send a seperate and Sleep briefly before completing.
                if (PortConnectionState == PortConnectionState.Deselecting)
                {
                    SendSeparateHeader();
                }

                bool disconnectedNormally = false;
                try
                {
                    var iar = tcpClient.Client.BeginDisconnect(reuseSocket: true, callback: null, state: null);

                    QpcTimeStamp startTimestamp = QpcTimeStamp.Now;

                    if (iar.AsyncWaitHandle.WaitOne(E037PortConfig.DisconnectTimeLimit))
                    {
                        tcpClient.Client.EndDisconnect(iar);
                        disconnectedNormally = true;
                    }
                    else
                    {
                        Log.Debug.Emit("Disconnect time limit reached after {0:f3} seconds", startTimestamp.Age.TotalSeconds);
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit("Disconnect generated unexpected exception: {0}", ex.ToString(ExceptionFormat.TypeAndMessage));
                }

                TerminateTcpClient(closedNormally: disconnectedNormally);
            }
        }

        #endregion

        #region PortBase overrides/abstract method implementations

        protected override void PrepareTenByteHeader(IMessage mesg)
        {
            var mesgE037TBH = mesg.TenByteHeader as E037TenByteHeader;
            if (mesgE037TBH == null)
            {
                mesgE037TBH = new E037TenByteHeader() { SF = mesg.SF, SystemBytes = mesg.SeqNum, SessionID = useDeviceID, PType = PType.SECSII, SType = SType.DataMessage };
                mesg.SetTenByteHeader(mesgE037TBH, keepMessageSF: true, keepMessageSeqNum: true);
            }
            // future: we may want to validate that the message header is a DataMessage.

            // otherwise it is a reply and we use the SystemBytes it was given when the reply was created.
        }

        protected override void ServiceConnection(QpcTimeStamp qpcTimeStamp)
        {
            // service sending new linktest requests
            if (tcpClient != null)
            {
                ServiceSentRequestEngines(qpcTimeStamp);
            }

            // service accepting new connection requests
            if (isPassivePort && tcpClient == null && tcpListener != null)
            {
                string actionStr = "Unknown";
                try
                {
                    if (pendingAcceptIAR == null)
                    {
                        actionStr = "BeingAcceptTcpClient";
                        pendingAcceptIAR = tcpListener.BeginAcceptTcpClient(iar => this.Notify(), null);
                    }
                    else if (pendingAcceptIAR.IsCompleted)
                    {
                        actionStr = "EndAcceptTcpClient";
                        tcpClient = tcpListener.EndAcceptTcpClient(pendingAcceptIAR);

                        pendingAcceptIAR = null;

                        if (!E037PortConfig.KeepAlivePeriod.IsZero())
                            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, (int)E037PortConfig.KeepAlivePeriod.TotalSeconds);

                        tcpClient.LingerState = new LingerOption(false, 0);
                        tcpClient.NoDelay = true;

                        SetConnectionState(PortConnectionState.NotSelected, "Accepted connection from {0}".CheckedFormat(tcpClient.Client.RemoteEndPoint), qpcTimeStamp);
                    }
                }
                catch (System.Exception ex)
                {
                    TerminateTcpListener();
                    TerminateTcpClient();

                    SetConnectionState(PortConnectionState.Failed, "Unexpected exception while servicing {0}: {1}".CheckedFormat(actionStr, ex.ToString(ExceptionFormat.TypeAndMessage)), qpcTimeStamp);
                }
            }
        }

        #endregion

        #region Receier (SetupReceiver, ServiceReceiver, AttemptToProcessSlidingBufferContents)

        private void SetupReceiever()
        {
            slidingBuffer = new SerialIO.SlidingBuffer(PortBaseConfig.MaximumMesgBodySize + 14 + 256) { EnableAutoAlignment = false }; 
        }

        private SerialIO.SlidingBuffer slidingBuffer;
        private IAsyncResult pendingReceiveIAR;

        protected override void ServiceReceiver(QpcTimeStamp qpcTimeStamp)
        {
            if (tcpClient == null)
                return;

            try
            {
                int addedCount = 0;

                if (pendingReceiveIAR != null && pendingReceiveIAR.IsCompleted)
                {
                    addedCount = tcpClient.Client.EndReceive(pendingReceiveIAR);
                    pendingReceiveIAR = null;

                    if (addedCount > 0)
                    {
                        slidingBuffer.AddedNChars(addedCount);

                        AddRxTracker(RxType.AddedN, count: addedCount);
                    }
                    else if (addedCount == 0)
                    {
                        HandleRemoteEndClosedCondition(qpcTimeStamp);
                    }
                }

                int nextRequirdByteCount = (4 + 10);        // default to minimum size for a single length + header block
                string ec = null;

                if (addedCount > 0 || (pendingReceiveIAR == null && slidingBuffer.BufferDataCount > 0) || slidingBuffer.BufferDataSpaceRemaining <= 0)
                {
                    for (; ; )
                    {
                        ec = AttemptToProcessSlidingBufferContents(qpcTimeStamp, ref nextRequirdByteCount, decodedReceiveMesgList);
                        if (ec != "")
                            break;
                    }

                    while (ec.IsNullOrEmpty() && decodedReceiveMesgList.Count > 0)
                    {
                        // note: The following method can call ServiceTransmitter if the received message generates and immediate response (S6F11W -> S6F12 for example)
                        ec = HandleReceivedMessage(decodedReceiveMesgList.SafeTakeFirst());     
                    }

                    if (ec.IsNeitherNullNorEmpty())
                    {
                        TerminateTcpClient();

                        SetConnectionState(PortConnectionState.NotConnected, "{0} unable to process received bytes: {1}".CheckedFormat(CurrentMethodName, ec), qpcTimeStamp, reportIssue: true);

                        return;
                    }
                }

                if (pendingReceiveIAR == null)
                {
                    int bufferDataCount = slidingBuffer.BufferDataCount;

                    int availableBytes = tcpClient.Available;
                    int requestForBytes = Math.Max(Math.Max(1, nextRequirdByteCount - bufferDataCount), availableBytes);

                    if (!slidingBuffer.IsAligned)
                    {
                        int bufferDataSpaceRemaining = slidingBuffer.BufferDataSpaceRemaining;
                        if (bufferDataCount == 0 || bufferDataSpaceRemaining < requestForBytes || lastUsedNCharsGetIndex > 1024)
                        {
                            slidingBuffer.AlignBuffer();

                            lastReceivePostedPutIndex = 0;
                            lastUsedNCharsGetIndex = 0;

                            AddRxTracker(RxType.AlignedBuffer, count: bufferDataCount);
                        }
                    }

                    byte[] buffer;
                    int nextPutIdx;
                    int spaceRemaining;
                    slidingBuffer.GetBufferPutAccessInfo(requestForBytes, out buffer, out nextPutIdx, out spaceRemaining);

                    if (spaceRemaining > 0)
                    {
                        int getCount = Math.Min(spaceRemaining, requestForBytes);

                        AddRxTracker(RxType.PutPosted, nextPutIdx, getCount);

                        pendingReceiveIAR = tcpClient.Client.BeginReceive(buffer, nextPutIdx, getCount, SocketFlags.None, iar => this.Notify(), null);

                        lastReceivePostedPutIndex = nextPutIdx;
                    }
                }
            }
            catch (System.Net.Sockets.SocketException socketEx)
            {
                HandleSocketException(CurrentMethodName, socketEx, qpcTimeStamp);
            }
            catch (System.Exception ex)
            {
                HandleNonSocketException(CurrentMethodName, ex, qpcTimeStamp);
            }
        }

        private readonly List<IMessage> decodedReceiveMesgList = new List<IMessage>();
        private int lastReceivePostedPutIndex = 0;
        private int lastUsedNCharsGetIndex = 0;

        private bool enableRxTracking = false;
        private void AddRxTracker(RxType rxType, int index = -1, int count = 0)
        {
            if (enableRxTracking)
            {
                if (rxTrackerList.Count >= 20)
                    rxTrackerList.RemoveAt(rxTrackerList.Count - 1);

                rxTrackerList.Insert(0, new RxTracker() { rxType = rxType, index = index, count = count });
            }
        }

        private readonly List<RxTracker> rxTrackerList = new List<RxTracker>();

        /// <summary>
        /// Set of Rx activities that are tracked.
        /// <para/>None (0), PutPosted, AddedN, UsedN, AlignedBuffer
        /// </summary>
        private enum RxType : int
        {
            None = 0,
            PutPosted,
            AddedN,
            UsedN,
            AlignedBuffer,
        }

        private struct RxTracker
        {
            public RxType rxType;
            public int index, count;

            public override string ToString()
            {
                if (index == -1)
                    return "{0} #{1}".CheckedFormat(rxType, count);
                else
                    return "{0} @{1} #{2}".CheckedFormat(rxType, index, count);
            }
        }

        private void HandleRemoteEndClosedCondition(QpcTimeStamp qpcTimeStamp)
        {
            bool clientIsConnected = tcpClient.Connected;
            bool socketIsConnected = tcpClient.Client.Connected;

            if (isPassivePort && PortConnectionState == E005.Port.PortConnectionState.NotSelected)
            {
                SetConnectionState(PortConnectionState.NotConnected, "Connection closed by remote end in state: {0}".CheckedFormat(PortConnectionState), qpcTimeStamp);

                TerminateTcpClient(closedNormally: true);
            }
            else
            {
                SetConnectionState(PortConnectionState.NotConnected, "Connection terminated unexpectedly in state: {0}".CheckedFormat(PortConnectionState), qpcTimeStamp, reportIssue: true);

                TerminateTcpClient();
            }
        }

        private readonly E037TenByteHeader headerDecoder = new E037TenByteHeader();

        /// <summary>
        /// Attempt to process the current contents of the sliding buffer.  
        /// Returns empty string to indicate that a packet was extracted and processed.  
        /// Returns null to indicate that there is no complete packet in the buffer right now.
        /// Returns non-empty string if a protocol violation has been detected and the connection should be closed.
        /// </summary>
		protected string AttemptToProcessSlidingBufferContents(QpcTimeStamp qpcTimeStamp, ref int nextRequiredByteCount, List<IMessage> decodedReceiveMesgList)
        {
            byte[] buffer;
            int nextGetIdx;
            int availableByteCount;
            slidingBuffer.GetBufferGetAccessInfo(out buffer, out nextGetIdx, out availableByteCount);

            nextRequiredByteCount = (4 + 10);     // 4 byte length followed by 10 byte header

            if (nextGetIdx + availableByteCount > buffer.Length)
                return "Given buffer len:{0} to small to use with idx:{1} and count:{2}".CheckedFormat(buffer.Length, nextGetIdx, availableByteCount);

            if (availableByteCount < nextRequiredByteCount)
                return null;     // not enought data to even begin to look - we are currently only looking for the length and the first header.

            uint u4;
            int e037MessageLength;
            int scanIdx = nextGetIdx;

            if (Utils.Data.Pack(buffer, scanIdx, out u4))
            {
                e037MessageLength = (int)u4;
                scanIdx += 4;
            }
            else
            {
                return "Unable to extract E037 MessageLength from given buffer at idx:{0} and len:{1}".CheckedFormat(scanIdx, buffer.Length);
            }

            UInt32 portsMaximumE037MessageLength = PortBaseConfig.MaximumMesgBodySize + 14;
            if (e037MessageLength < 10 || e037MessageLength > portsMaximumE037MessageLength)
            {
                return "Invalid E037 MessageLength:{0}, value must be between 10 and {1} for this port [from buffer at idx:{1}, len:{2}]".CheckedFormat(e037MessageLength, portsMaximumE037MessageLength, scanIdx, buffer.Length);
            }

            // only allow us to wait for more bytes once we are certain that the resulting message size will be legal.

            nextRequiredByteCount = (e037MessageLength + 4);

            if (availableByteCount < nextRequiredByteCount)
                return null;     // we wait until all of the characters in the message are available so that the rest of the packet can validated and processed one invocation.

            if (headerDecoder.Decode(buffer, scanIdx)) // we have already verified that there is enough data to extract a header from
                scanIdx += 10;
            else
                return "Unable to extract E037 TenByteHeader from given buffer at idx:{0}, len:{1}".CheckedFormat(scanIdx, buffer.Length);

            // we have successfully extracted the E037 MessageLength and TenByteHeader.  Now verify that their values are consistent

            bool messageMayContainNonEmptyDataPayload = (headerDecoder.SType == SType.DataMessage);
            int secsPayloadDataLength = e037MessageLength - 10;
            if (!messageMayContainNonEmptyDataPayload && secsPayloadDataLength != 0)
                return "Invalid E037 MessageLength:{0} for SType:{1}, value must be 10 [from buffer at idx:{1}, len:{2}]".CheckedFormat(e037MessageLength, headerDecoder.SType, scanIdx, buffer.Length);

            // tracing of the header...
            switch (headerDecoder.SType)
            {
                case SType.DataMessage: break; // none
                case SType.LinktestReq: LinkTestEmitter.Emit("Received header {0}", headerDecoder); break;
                case SType.LinktestRsp: LinkTestEmitter.Emit("Received header {0}", headerDecoder); break;
                default: TraceHeaders.Emit("Received header {0}", headerDecoder); break;
            }

            if (headerDecoder.SType != SType.DataMessage && PortRecording != null)
                PortRecording.NoteE005HeaderReceived(this, headerDecoder);

            string ec;
            switch (headerDecoder.SType)
            {
                case SType.LinktestReq: ec = HandleLinktestReq(headerDecoder, qpcTimeStamp); break;
                case SType.LinktestRsp: ec = HandleLinktestRsp(headerDecoder, qpcTimeStamp); break;
                case SType.SelectReq: ec = HandleSelectReq(headerDecoder, qpcTimeStamp); break;
                case SType.SelectRsp: ec = HandleSelectRsp(headerDecoder, qpcTimeStamp); break;
                case SType.RejectReq: ec = HandleRejectReq(headerDecoder, qpcTimeStamp); break;
                case SType.DeselectReq: ec = HandleDeselectReq(headerDecoder, qpcTimeStamp); break;
                case SType.DeselectRsp: ec = HandleDeselectRsp(headerDecoder, qpcTimeStamp); break;
                case SType.DataMessage:
                    {
                        byte[] contentBytes = null;
                        if (messageMayContainNonEmptyDataPayload && secsPayloadDataLength > 0)
                        {
                            contentBytes = new byte[secsPayloadDataLength];
                            System.Buffer.BlockCopy(buffer, scanIdx, contentBytes, 0, secsPayloadDataLength);

                            scanIdx += secsPayloadDataLength;
                        }

                        decodedReceiveMesgList.Add(new Message(headerDecoder.MakeCopyOfThis(), this, ManagerPortFacet).SetContentBytes(contentBytes, makeCopy: false));
                        ec = string.Empty;
                        break;
                    }
                case SType.SeparateReq: ec = HandleSeparateReq(headerDecoder, qpcTimeStamp); break;
                default:
                case SType.Invalid: return "Header decode failed: Invalid SType: ${0:x2}".CheckedFormat(headerDecoder.STypeByte);
            }

            int usedNBytes = (scanIdx - nextGetIdx);
            slidingBuffer.UsedNChars(usedNBytes);

            lastUsedNCharsGetIndex = nextGetIdx;

            AddRxTracker(RxType.UsedN, nextGetIdx, usedNBytes);

            nextRequiredByteCount = (4 + 10);     // the next buffer to process needs to be at least this big.... (4 byte length followed by 10 byte header)

            return ec.MapNullToEmpty();
        }

        private void ServiceSentRequestEngines(QpcTimeStamp qpcTimeStamp)
        {
            string ec = null;

            if (ec.IsNullOrEmpty())
                ec = ServiceLinkTestBackground(qpcTimeStamp);

            if (ec.IsNullOrEmpty())
                ec = ServiceSelectBackground(qpcTimeStamp);

            if (ec.IsNeitherNullNorEmpty())
                SetConnectionState(PortConnectionState.Failed, ec, qpcTimeStamp);
        }

		#endregion

        #region SendHeaderTracker

        protected struct SendHeaderTracker
        {
            public E037TenByteHeader Header { get; private set; }
            public QpcTimeStamp TimeStamp { get; private set; }
            public string ResultCode { get; private set; }

            public void SetHeader(E037TenByteHeader header, QpcTimeStamp qpcTimeStamp)
            {
                Header = header;
                TimeStamp = qpcTimeStamp.MapDefaultToNow();
                ResultCode = null;
            }

            public void Clear(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
            {
                Header = null;
                TimeStamp = qpcTimeStamp.MapDefaultToNow();
                ResultCode = null;
            }

            public void NoteCompleted(QpcTimeStamp qpcTimeStamp, string resultCode = null)
            {
                Header = null;
                TimeStamp = qpcTimeStamp.MapDefaultToNow();

                ResultCode = resultCode.MapNullToEmpty();
            }
        }

        #endregion

        #region inbound and other handling methods for Linktest

        private string HandleLinktestReq(E037TenByteHeader reqHeader, QpcTimeStamp qpcTimeStamp)
        {
            if (reqHeader.SessionID != 0xffff)
                return "Invalid Linktest.req: SessionID:${0:x4} is not legal (expected $ffff)".CheckedFormat(reqHeader.SessionID);

            E037TenByteHeader rspHeader = new E037TenByteHeader() { SType = SType.LinktestRsp, PType = PType.SECSII, SystemBytes = reqHeader.SystemBytes, SessionID = 0xffff };

            PostTransmitHeaderBuffer(ref rspHeader, qpcTimeStamp, useTraceEmitter: LinkTestEmitter);

            return String.Empty;
        }


        protected SendHeaderTracker linktestSendTracker = new SendHeaderTracker();

        protected string SendLinktestHeader(QpcTimeStamp qpcTimeStamp)
        {
            UInt32 reqHeaderSeqNum = ManagerPortFacet.GetNextMessageSequenceNum();

            E037TenByteHeader reqHeader = new E037TenByteHeader() { SType = SType.LinktestReq, PType = PType.SECSII, SystemBytes = reqHeaderSeqNum, SessionID = 0xffff };

            linktestSendTracker.SetHeader(reqHeader, qpcTimeStamp);

            PostTransmitHeaderBuffer(ref reqHeader, qpcTimeStamp, useTraceEmitter: LinkTestEmitter);

            return String.Empty;
        }

        private string HandleLinktestRsp(E037TenByteHeader rspHeader, QpcTimeStamp qpcTimeStamp)
        {
            if (linktestSendTracker.Header == null)
            {
                Log.Debug.Emit("Ignoring unexpected Linktest.rsp: there is no outstanding req to match this rsp to.");
                return String.Empty;
            }

            if (rspHeader.SystemBytes != linktestSendTracker.Header.SystemBytes)
            {
                Log.Debug.Emit("Ignoring unexpected Linktest.rsp: expecting SystemBytes:${0:x8}, found SystemBytes:${1:x8}", linktestSendTracker.Header.SystemBytes, rspHeader.SystemBytes);
                return String.Empty;
            }

            LinkTestEmitter.Emit("Linktest complete: received matching Linktest.rsp for SystemBytes:${0:x8} after {1:f6} sec", linktestSendTracker.Header.SystemBytes, (qpcTimeStamp - linktestSendTracker.TimeStamp).TotalSeconds);
            linktestSendTracker.NoteCompleted(qpcTimeStamp, string.Empty);

            return String.Empty;
        }

        private string ServiceLinkTestBackground(QpcTimeStamp qpcTimeStamp)
        {
            if (!PortConnectionState.IsSelected())
            {
                if (linktestSendTracker.Header != null)
                    linktestSendTracker.NoteCompleted(qpcTimeStamp, "Linktest.req failed: port is no longer Selected [{0}]".CheckedFormat(PortConnectionState));

                return String.Empty;
            }

            TimeSpan linktestReqAge = linktestSendTracker.TimeStamp.Age(qpcTimeStamp);

            if (linktestSendTracker.Header != null)
            {
                if (linktestReqAge > E037PortConfig.T6_ControlTransactionTO)
                {
                    string ec = "Linktest.req failed: no linktest.rsp received within T6:{0:f3} seconds of posted request".CheckedFormat(linktestReqAge.TotalSeconds);
                    linktestSendTracker.NoteCompleted(qpcTimeStamp, ec);
                    return ec;
                }
            }
            else if (E037PortConfig.IdleLineLinkTestInterval != TimeSpan.Zero && linktestReqAge > E037PortConfig.IdleLineLinkTestInterval)
            {
                return SendLinktestHeader(qpcTimeStamp);
            }

            return String.Empty;
        }

        #endregion

        #region inbound and other Select and Deselect related handling methods

        private string HandleSelectReq(E037TenByteHeader reqHeader, QpcTimeStamp qpcTimeStamp)
        {
            if (isActivePort && isSingleSession)
            {
                return "Invalid Select.req: this request is not valid for an active single session port:[{0}, req SystemBytes ${1:x8}]".CheckedFormat(PortConnectionState, reqHeader.SystemBytes);
            }

            if (PortConnectionState.IsSelected())
            {
                if (isSingleSession)
                    return "Invalid Select.req: multiple select requests are not valid for a single session port:[{0}, req SystemBytes ${1:x8}]".CheckedFormat(PortConnectionState, reqHeader.SystemBytes);

                Log.Debug.Emit("Ignoring extra Select.req: connection is already selected [SystemBytes ${0:x8}]", reqHeader.SystemBytes);

                return String.Empty;
            }

            if (!PortConnectionState.IsConnected())
            {
                return "Invalid Select.req: port is not connected:[{0}, req SystemBytes ${1:x8}]".CheckedFormat(PortConnectionState, reqHeader.SystemBytes);
            }

            // we are selecting or we have already been selected

            if (reqHeader.SessionID != useSessionID)
                return "Invalid Select.req: SessionID:${0:x4} is not valid [{1}, req SystemBytes ${2:x8}, expected SessionID:{3:x4}]".CheckedFormat(reqHeader.SessionID, PortConnectionState, reqHeader.SystemBytes, useSessionID);

            string ec = SendSelectRsp(reqHeader, qpcTimeStamp);

            if (!String.IsNullOrEmpty(ec))
                return ec;

            if (!PortConnectionState.IsSelected())
                SetConnectionState(PortConnectionState.Selected, "Accepted Select.req [SystemBytes ${0:x8}]".CheckedFormat(reqHeader.SystemBytes), qpcTimeStamp);

            return String.Empty;
        }

        private string HandleSelectRsp(E037TenByteHeader rspHeader, QpcTimeStamp qpcTimeStamp)
        {
            if (selectSendTracker.Header == null)
            {
                if (PortConnectionState.IsSelected())
                    Log.Info.Emit("Ignoring unexpected Select.rsp: there is no outstanding req to match this rsp to and port is already Selected.  [rsp.SystemBytes ${0:x8}]", rspHeader.SystemBytes);
                else
                    Log.Warning.Emit("Ignoring unexpected Select.rsp: there is no outstanding req to match this rsp to and port is not selected.  [{0}, rsp.SystemBytes ${1:x8}]", PortConnectionState, rspHeader.SystemBytes);

                return String.Empty;
            }

            if (rspHeader.SystemBytes != selectSendTracker.Header.SystemBytes)
            {
                Log.Info.Emit("Ignoring unexpected Select.rsp: expecting SystemBytes:${0:x8}, found SystemBytes:${1:x8}", selectSendTracker.Header.SystemBytes, rspHeader.SystemBytes);
                return String.Empty;
            }

            if (rspHeader.SessionID != selectSendTracker.Header.SessionID)
            {
                Log.Info.Emit("Ignoring unexpected Select.rsp: expecting SessionID:${0:x4}, found SessionID:${1:x4}", selectSendTracker.Header.SessionID, rspHeader.SessionID);
                return String.Empty;
            }

            if (PortConnectionState != PortConnectionState.Selecting)
            {
                Log.Info.Emit("Ignoring unexpected Select.rsp: while in unexpected state {0}, [SystemBytes:${0:x8}]", PortConnectionState, rspHeader.SystemBytes);
                return String.Empty;
            }

            SetConnectionState(PortConnectionState.Selected, "Select complete: received matching Select.rsp for SystemBytes:${0:x8} after {1:f6} sec".CheckedFormat(selectSendTracker.Header.SystemBytes, (qpcTimeStamp - selectSendTracker.TimeStamp).TotalSeconds), qpcTimeStamp);
            selectSendTracker.Clear(qpcTimeStamp);

            return String.Empty;
        }

        private string HandleSeparateReq(E037TenByteHeader reqHeader, QpcTimeStamp qpcTimeStamp)
        {
            if (!PortConnectionState.IsSelected())
            {
                Log.Debug.Emit("Ignoring extra Separate.req: connection is not selected [SystemBytes ${0:x8}]", reqHeader.SystemBytes);

                return String.Empty;
            }

            if (!PortConnectionState.IsConnected())
            {
                return "Invalid Separate.req: port is not connected:[{0}, req SystemBytes ${1:x8}]".CheckedFormat(PortConnectionState, reqHeader.SystemBytes);
            }

            // there is no response to a seperate request

            SetConnectionState(PortConnectionState.NotSelected, "Accepted Separate.req [SystemBytes ${0:x8}]".CheckedFormat(reqHeader.SystemBytes), qpcTimeStamp);

            return String.Empty;
        }

        private string HandleDeselectReq(E037TenByteHeader reqHeader, QpcTimeStamp qpcTimeStamp)
        {
            if (!PortConnectionState.IsConnected())
            {
                return "Invalid Deselect.req: port is not connected:[{0}, req SystemBytes ${1:x8}]".CheckedFormat(PortConnectionState, reqHeader.SystemBytes);
            }

            if (!PortConnectionState.IsSelected())
                Log.Debug.Emit("observed extra Deselect.req: connection is not selected [SystemBytes ${0:x8}]", reqHeader.SystemBytes);

            // we are selecting or we have already been selected

            string ec = SendDeselectRsp(reqHeader, qpcTimeStamp);

            if (!String.IsNullOrEmpty(ec))
                return ec;

            if (PortConnectionState.IsSelected())
                SetConnectionState(PortConnectionState.NotSelected, "Accepted Deselect.req [SystemBytes ${0:x8}]".CheckedFormat(reqHeader.SystemBytes), qpcTimeStamp);

            return String.Empty;
        }

        private string HandleDeselectRsp(E037TenByteHeader rspHeader, QpcTimeStamp qpcTimeStamp)
        {
            if (deselectSendTracker.Header == null)
            {
                if (!PortConnectionState.IsSelected())
                    Log.Info.Emit("Ignoring unexpected Deselect.rsp: there is no outstanding req to match this rsp to and port is already Deselected.  [rsp.SystemBytes ${0:x8}]", rspHeader.SystemBytes);
                else
                    Log.Warning.Emit("Ignoring unexpected Deselect.rsp: there is no outstanding req to match this rsp to and port is selected.  [{0}, rsp.SystemBytes ${1:x8}]", PortConnectionState, rspHeader.SystemBytes);

                return String.Empty;
            }

            if (rspHeader.SystemBytes != deselectSendTracker.Header.SystemBytes)
            {
                Log.Info.Emit("Ignoring unexpected Deselect.rsp: expecting SystemBytes:${0:x8}, found SystemBytes:${1:x8}", selectSendTracker.Header.SystemBytes, rspHeader.SystemBytes);
                return String.Empty;
            }

            if (rspHeader.SessionID != deselectSendTracker.Header.SessionID)
            {
                Log.Info.Emit("Ignoring unexpected Deselect.rsp: expecting SessionID:${0:x4}, found SessionID:${1:x4}", selectSendTracker.Header.SessionID, rspHeader.SessionID);
                return String.Empty;
            }

            if (PortConnectionState != PortConnectionState.Deselecting)
            {
                Log.Info.Emit("Ignoring unexpected Deselect.rsp: while in unexpected state {0}, [SystemBytes:${0:x8}]", PortConnectionState, rspHeader.SystemBytes);
                return String.Empty;
            }

            SetConnectionState(PortConnectionState.NotSelected, "Deselect complete: received matching Deselect.rsp for SystemBytes:${0:x8} after {1:f6} sec".CheckedFormat(deselectSendTracker.Header.SystemBytes, (qpcTimeStamp - deselectSendTracker.TimeStamp).TotalSeconds), qpcTimeStamp);
            deselectSendTracker.Clear(qpcTimeStamp);

            return String.Empty;
        }


        protected SendHeaderTracker selectSendTracker = new SendHeaderTracker();
        protected SendHeaderTracker deselectSendTracker = new SendHeaderTracker();

        /// <summary>
        /// Generate and send a Select message.  Changes PortConnectionState to Selecting using the given reason.
        /// </summary>
        protected string StartPortSelect(string reason, QpcTimeStamp qpcTimeStamp)
        {
            UInt32 reqHeaderSeqNum = ManagerPortFacet.GetNextMessageSequenceNum();

            E037TenByteHeader reqHeader = new E037TenByteHeader() { SType = SType.SelectReq, PType = PType.SECSII, SystemBytes = reqHeaderSeqNum, SessionID = useSessionID };

            selectSendTracker.SetHeader(reqHeader, qpcTimeStamp);

            SetConnectionState(PortConnectionState.Selecting, reason, qpcTimeStamp);

            PostTransmitHeaderBuffer(ref reqHeader, qpcTimeStamp);

            return String.Empty;
        }

        protected string StartPortDeselect(string reason, QpcTimeStamp qpcTimeStamp)
        {
            UInt32 reqHeaderSeqNum = ManagerPortFacet.GetNextMessageSequenceNum();

            E037TenByteHeader reqHeader = new E037TenByteHeader() { SType = SType.DeselectReq, PType = PType.SECSII, SystemBytes = reqHeaderSeqNum, SessionID = useSessionID };

            deselectSendTracker.SetHeader(reqHeader, qpcTimeStamp);

            SetConnectionState(PortConnectionState.Deselecting, reason, qpcTimeStamp);

            PostTransmitHeaderBuffer(ref reqHeader, qpcTimeStamp);

            return String.Empty;
        }

        private string ServiceSelectBackground(QpcTimeStamp qpcTimeStamp)
        {
            if (selectSendTracker.Header != null)
            {
                TimeSpan selectReqAge = selectSendTracker.TimeStamp.Age(qpcTimeStamp);

                if (selectReqAge > E037PortConfig.T6_ControlTransactionTO)
                    return "Select.req failed: no Select.rsp received within T6:{0:f3} seconds of posted request".CheckedFormat(selectReqAge.TotalSeconds);
            }

            if (deselectSendTracker.Header != null)
            {
                TimeSpan deselectReqAge = deselectSendTracker.TimeStamp.Age(qpcTimeStamp);

                if (deselectReqAge > E037PortConfig.T6_ControlTransactionTO)
                    return "Deselect.req failed: no Deselect.rsp received within T6:{0:f3} seconds of posted request".CheckedFormat(deselectReqAge.TotalSeconds);
            }

            return String.Empty;
        }

        private string SendSelectRsp(E037TenByteHeader reqHeader, QpcTimeStamp qpcTimeStamp)
        {
            E037TenByteHeader rspHeader = new E037TenByteHeader() { SType = SType.SelectRsp, PType = PType.SECSII, SystemBytes = reqHeader.SystemBytes, SessionID = reqHeader.SessionID };

            PostTransmitHeaderBuffer(ref rspHeader, qpcTimeStamp);

            return String.Empty;
        }

        private string SendDeselectRsp(E037TenByteHeader reqHeader, QpcTimeStamp qpcTimeStamp)
        {
            E037TenByteHeader rspHeader = new E037TenByteHeader() { SType = SType.DeselectRsp, PType = PType.SECSII, SystemBytes = reqHeader.SystemBytes, SessionID = reqHeader.SessionID };

            PostTransmitHeaderBuffer(ref rspHeader, qpcTimeStamp);

            return String.Empty;
        }

        #endregion

        #region HandleRejectReq, SendRejectReq

        private string HandleRejectReq(E037TenByteHeader rspHeader, QpcTimeStamp qpcTimeStamp)
        {
            string reason = "Received Reject Request [{0}]".CheckedFormat(rspHeader);
            TerminateTcpClient();
            SetConnectionState(PortConnectionState.Failed, reason, qpcTimeStamp);

            return reason;
        }

        // Future: review adding support for this method
        [Obsolete("Use of this method is not supported yet")]
        private string SendRejectRsp(E037TenByteHeader reqHeader, RejectReasonCode rejectReason, QpcTimeStamp qpcTimeStamp)
        {
            E037TenByteHeader rspHeader = new E037TenByteHeader() { SType = SType.RejectReq, PType = PType.SECSII, SystemBytes = reqHeader.SystemBytes, SessionID = reqHeader.SessionID };

            StreamFunction sf = rspHeader.SF;

            sf.FunctionByte = unchecked((byte) rejectReason);
            switch (rejectReason)
            {
                case RejectReasonCode.STypeNotSupported:
                    sf.StreamByte = reqHeader.STypeByte;
                    break;
                case RejectReasonCode.PTypeNotSupported:
                    sf.StreamByte = reqHeader.PTypeByte;
                    break;
                case RejectReasonCode.TransactionNotOpen:
                    break;
                case RejectReasonCode.EntityNotSelected:
                    break;
                default:
                    break;
            }

            rspHeader.SF = sf;

            PostTransmitHeaderBuffer(ref rspHeader, qpcTimeStamp);

            return String.Empty;
        }

        #endregion

        #region Transmitter (PostTransmit, ServiceTransmitter, SendSeperateHeader)

        private void PostTransmitHeaderBuffer(ref E037TenByteHeader header, QpcTimeStamp qpcTimeStamp, bool serviceTransmitter = true, Logging.IMesgEmitter useTraceEmitter = null)
        {
            (useTraceEmitter ?? TraceHeaders).Emit("Sending Header {0}", header);

            pendingTransmitHeaderQueue.Enqueue(header);
            if (serviceTransmitter)
                ServiceTransmitter(qpcTimeStamp);

            header = null;
        }

        private readonly Queue<E037TenByteHeader> pendingTransmitHeaderQueue = new Queue<E037TenByteHeader>();
        private readonly List<IAsyncResult> pendingSendIARList = new List<IAsyncResult>();

        protected override void ServiceTransmitter(QpcTimeStamp qpcTimeStamp)
        {
            InnerServiceTransmitter(qpcTimeStamp, permitPostMessageSends: true);
        }

        private void InnerServiceTransmitter(QpcTimeStamp qpcTimeStamp, bool permitPostMessageSends)
        {
            if (tcpClient == null)
                return;

            try
            {
                // first process posted sends
                if (pendingSendIARList.Count > 0)
                {
                    var completedIARSet = pendingSendIARList.FilterAndRemove(iar => iar.IsCompleted).ToArray();
                    foreach (var completedIAR in completedIARSet)
                    {
                        tcpClient.Client.EndSend(completedIAR);
                        var sendMessageOp = completedIAR.AsyncState as SendMessageOp;
                        if (sendMessageOp != null)
                            NoteMesgSent(sendMessageOp, qpcTimeStamp);
                    }
                }

                for (; ; )
                {
                    int availableSlots = E037PortConfig.MaxConcurrentPostedSends - pendingSendIARList.Count;

                    if (availableSlots <= 0)
                    {
                        break;
                    }
                    else if (pendingTransmitHeaderQueue.Count > 0)
                    {
                        var lengthByteArray = new byte[4];
                        var header = pendingTransmitHeaderQueue.Dequeue();
                        var headerByteArray = header.ByteArray;

                        Data.Unpack((UInt32)headerByteArray.Length, lengthByteArray);

                        var arraySegmentList = new List<ArraySegment<byte>>()
                            .SafeAddItems(new ArraySegment<byte>(lengthByteArray), new ArraySegment<byte>(headerByteArray));

                        if (PortRecording != null)
                            PortRecording.NoteSendingE005Header(this, header);

                        pendingSendIARList.Add(tcpClient.Client.BeginSend(arraySegmentList, SocketFlags.None, iar => this.Notify(), header));
                    }
                    else if (readyToSendOpQueue.Count > 0 && permitPostMessageSends)
                    {
                        var nextSendMessageOp = readyToSendOpQueue.Dequeue();
                        var nextSendMesg = nextSendMessageOp.Mesg;

                        GetMesgTraceEmitter(nextSendMesg).Emit("Sending Mesg {0}", nextSendMesg);

                        var lengthByteArray = new byte[4];
                        var headerByteArray = nextSendMesg.TenByteHeader.ByteArray;
                        var bodyByteArray = nextSendMesg.ContentBytes;

                        Data.Unpack((UInt32)headerByteArray.Length + (UInt32)bodyByteArray.Length, lengthByteArray);

                        var arraySegmentList = new List<ArraySegment<byte>>()
                            .SafeAddItems(new ArraySegment<byte>(lengthByteArray), new ArraySegment<byte>(headerByteArray))
                            .ConditionalAddItems(!bodyByteArray.IsNullOrEmpty(), new ArraySegment<byte>(bodyByteArray));

                        if (PortRecording != null)
                            PortRecording.NoteSendingE005Message(this, nextSendMesg);

                        pendingSendIARList.Add(tcpClient.Client.BeginSend(arraySegmentList, SocketFlags.None, iar => this.Notify(), nextSendMessageOp));
                        NoteMesgSendPosted(nextSendMessageOp, qpcTimeStamp);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (System.Net.Sockets.SocketException socketEx)
            {
                HandleSocketException(CurrentMethodName, socketEx, qpcTimeStamp);
            }
            catch (System.Exception ex)
            {
                HandleNonSocketException(CurrentMethodName, ex, qpcTimeStamp);
            }
        }

        protected string SendSeparateHeader()
        {
            var qpcTimeStamp = QpcTimeStamp.Now;
            UInt32 headerSeqNum = ManagerPortFacet.GetNextMessageSequenceNum();
            E037TenByteHeader header = new E037TenByteHeader() { SType = SType.SeparateReq, PType = PType.SECSII, SystemBytes = headerSeqNum, SessionID = useSessionID };
            var headerStr = header.ToString();

            PostTransmitHeaderBuffer(ref header, qpcTimeStamp, serviceTransmitter: false);

            QpcTimer waitLimitTimer = new QpcTimer() { TriggerInterval = (0.5).FromSeconds() }.Start();

            string ec = string.Empty;
            while (ec.IsNullOrEmpty())
            {
                qpcTimeStamp = QpcTimeStamp.Now;
                InnerServiceTransmitter(qpcTimeStamp, permitPostMessageSends: false);

                bool waitTimeLimitReached = waitLimitTimer.GetIsTriggered(qpcTimeStamp);
                WaitForSomethingToDo();

                if (pendingTransmitHeaderQueue.Count == 0 && pendingSendIARList.Count == 0)
                    break;

                if (waitTimeLimitReached)
                    ec = "{0} {1} failed: wait time limit reached after {2:f3} seconds".CheckedFormat(CurrentMethodName, headerStr, waitLimitTimer.ElapsedTimeAtLastTrigger.TotalSeconds);
            }

            if (ec.IsNullOrEmpty())
            {
                (0.25).FromSeconds().Sleep();
                TraceHeaders.Emit("Sent {0}", header);
            }
            else
            {
                TraceHeaders.Emit(ec);
            }

            return ec;
        }

        #endregion

        #region Service related exception handlers

        private void HandleSocketException(string methodName, System.Net.Sockets.SocketException socketEx, QpcTimeStamp qpcTimeStamp)
        {
            TerminateTcpClient();

            SetConnectionState(PortConnectionState.Failed, "{0} encountered connection related exception: {1}".CheckedFormat(methodName, socketEx.ToString(ExceptionFormat.TypeAndMessage)), qpcTimeStamp);
        }

        private void HandleNonSocketException(string methodName, System.Exception ex, QpcTimeStamp qpcTimeStamp)
        {
            TerminateTcpClient();

            if (isActivePort)
                SetConnectionState(PortConnectionState.OutOfService, "{0} encountered unexpected non-socket exception: {1}".CheckedFormat(methodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)), qpcTimeStamp, reportIssue: true);
            else
                SetConnectionState(PortConnectionState.Failed, "{0} encountered unexpected non-socket exception (current connection aborted): {1}".CheckedFormat(methodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)), qpcTimeStamp);
        }

        #endregion

    }

	//-------------------------------------------------------------------
	#region E037 TenByteHeader

    /// <summary>
    /// This interface is derived from ITenByteHeader and adds E037 specific properties that are used to interpret/modify the TBH contents for E037 specific purposes.
    /// </summary>
	public interface IE037TenByteHeader : ITenByteHeader
	{
        /// <summary>Gets/Sets the SessionID for this E037 TBH.  Corresponds to TBH B0B1 word.</summary>
        UInt16 SessionID { get; set; }
        /// <summary>Gets/Sets PType as a byte.  Stored in TBH Byte4 (part of TBH B4B5)</summary>
        Byte PTypeByte { get; set; }
        /// <summary>Gets/Sets SType as a byte.  Stored in TBH Byte5 (part of TBH B4B5)</summary>
        Byte STypeByte { get; set; }
        /// <summary>Gets/Sets PTypeByte casted as an E037.PType enum.</summary>
        PType PType { get; set; }
        /// <summary>Gets/Sets STypeByte casted as an E037.Stype enum.</summary>
        SType SType { get; set; }
	}

    /// <summary>
    /// This class is derived from TenByteHeaderBase and implements the IE037TenByteHeader interface.
    /// </summary>
    /// <remarks>used to generate and interpret E004/E037 Message/Block Headers</remarks>
    public class E037TenByteHeader : TenByteHeaderBase, IE037TenByteHeader, ICopyable<E037TenByteHeader>
	{
        /// <summary>Gets/Sets the SessionID for this E037 TBH.  Corresponds to TBH B0B1 word.</summary>
        public UInt16 SessionID { get { return B0B1; } set { B0B1 = value; } }

        /// <summary>Gets/Sets PType as a byte.  Stored in TBH Byte4 (part of TBH B4B5)</summary>
        public Byte PTypeByte { get { Byte b4, b5; Utils.Data.Unpack(B4B5, out b4, out b5); return b4; } set { Byte b4, b5; Utils.Data.Unpack(B4B5, out b4, out b5); B4B5 = Utils.Data.Pack(value, b5); } }

        /// <summary>Gets/Sets SType as a byte.  Stored in TBH Byte5 (part of TBH B4B5)</summary>
        public Byte STypeByte { get { Byte b4, b5; Utils.Data.Unpack(B4B5, out b4, out b5); return b5; } set { Byte b4, b5; Utils.Data.Unpack(B4B5, out b4, out b5); B4B5 = Utils.Data.Pack(b4, value); } }

        /// <summary>Gets/Sets PTypeByte casted as an E037.PType enum.</summary>
        public E037.PType PType
		{
			get { unchecked { return (E037.PType) PTypeByte; } }
			set { unchecked { PTypeByte = (Byte) value; } }
		}

        /// <summary>Gets/Sets STypeByte casted as an E037.Stype enum.</summary>
        public E037.SType SType
        {
            get { unchecked { return (E037.SType)STypeByte; } }
            set { unchecked { STypeByte = (Byte) value; } }
        }

        /// <summary>Default constructor</summary>
        public E037TenByteHeader() { }

        /// <summary>Returns string formatted version of contents for logging/debugging purposes.</summary>
        public override string ToString()
		{
            if (!SF.IsEmpty)
                return "{0} id:${1:x4},p:{2},s:{3},seq:${4:x8}".CheckedFormat(SF, SessionID, PType, SType, SystemBytes);       
            else
                return "id:${0:x4},p:{1},s:{2},seq:${3:x8}".CheckedFormat(SessionID, PType, SType, SystemBytes);
        }

        /// <summary>Returns a MemberwiseClone of this object</summary>
        public new E037TenByteHeader MakeCopyOfThis(bool deepCopy = true)
        {
            return (E037TenByteHeader)this.MemberwiseClone();
        }
    }

	#endregion

	//-------------------------------------------------------------------
}
