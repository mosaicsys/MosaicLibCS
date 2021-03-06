//-------------------------------------------------------------------
/*! @file TcpSerialIO.cs
 *  @brief This file defines the SerialIO related classes that are used for Tcp based ports (TcpClientPort and TcpServerPort)
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2002 Mosaic Systems Inc.  (C++ library version)
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

using System.Net;
using System.Net.Sockets;

using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Part;

namespace MosaicLib.SerialIO
{
	//-----------------------------------------------------------------
	#region ComPort Factory method

	public static partial class Factory
	{
		public static IPort CreateTcpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig)
		{
			return new TcpClientPort(portConfig, ipPortEndpointConfig);
		}

		public static IPort CreateTcpServerPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig)
		{
			return new TcpServerPort(portConfig, ipPortEndpointConfig);
		}
	}

	#endregion

	//-----------------------------------------------------------------
	#region IPPortEndpointConfig

	///		<TcpClient addr="127.0.0.1" port="5002"/>
	///		<TcpServer addr="127.0.0.1" port="8001"/>		addr is optional - will use any if no address is provided
	///		<UdpClient addr="127.0.0.1" port="5005"/>
	///		<UdpServer addr="127.0.0.1" port="5006"/>		addr is optional - will use any if no address is provided

	public class IPPortEndpointConfig
	{
		string errorCode = string.Empty;
		string elementName = string.Empty;
		IPEndPoint ipEndpoint = null;
		string toString = string.Empty;

		public IPPortEndpointConfig(string elementName, IPEndPoint ipEndpoint) : this(elementName, ipEndpoint, string.Empty) { }
		public IPPortEndpointConfig(string elementName, IPEndPoint ipEndpoint, string ec)
		{
			this.errorCode = ec;
			this.elementName = elementName;
			this.ipEndpoint = ipEndpoint;

			if (IsValid)
				toString = Utils.Fcns.CheckedFormat("<{0} addr=\"{1}\" port=\"{2}\"/>", elementName, Address, Port);
			else
				toString = Utils.Fcns.CheckedFormat("<{0} addr=\"{1}\" port=\"{2}\" fault=\"{3}\"/>", elementName, Address, Port, errorCode);
		}

		public bool IsValid { get { return string.IsNullOrEmpty(errorCode); } }
		public string ErrorCode { get { return (IsValid ? string.Empty : errorCode); } }
		public string ElementName { get { return elementName; } }
		public IPEndPoint IPEndPoint { get { return ipEndpoint; } }
		public IPAddress Address { get { return ipEndpoint.Address; } }
		public int Port { get { return ipEndpoint.Port; } }
		public override string ToString() { return toString; }

		public static bool TryParse(ref Utils.StringScanner specScanRef, out IPPortEndpointConfig ipPortEndpointConfig)
		{
			Utils.StringScanner specScan = specScanRef;
			string ec = null;
			string elementName = null;
			string addrAttribValue = string.Empty;
			int portAttribValue = 0;
			IPAddress ipAddr = null;

            if (!specScan.MatchToken("<", true, false)
                || !specScan.ExtractToken(out elementName, TokenType.AlphaNumeric, false, true, false))
			{
				ec = Utils.Fcns.CheckedFormat("Could not find element name in SpecStr:'{0}'", specScan.Str);
			}
			else if (specScan.Rest.StartsWith("addr=") && !specScan.ParseXmlAttribute("addr", out addrAttribValue))
			{
				ec = Utils.Fcns.CheckedFormat("Could not extract addr attribute from SpecStr:'{0}'", specScan.Str);
			}
            else if (!specScan.ParseXmlAttribute("port", out portAttribValue))
			{
				ec = Utils.Fcns.CheckedFormat("Could not extract port attribute from SpecStr:'{0}'", specScan.Str);
			}
            else if (!specScan.MatchToken("/>", true, false))
            {
                ec = Utils.Fcns.CheckedFormat("Did not find expected element end in SpecStr:'{0}'", specScan.Str);
            }
            else if (!CannedIPAddresses.TryGetValue(addrAttribValue ?? String.Empty, out ipAddr)
						&& !IPAddress.TryParse(addrAttribValue, out ipAddr))
			{
				ec = Utils.Fcns.CheckedFormat("valid IPAddress could not be extracted from addr attribute:'{0}' in SpecStr:'{1}'", addrAttribValue, specScan.Str);
			}

			if (ec == null)
			{
				// successfull parse
				ec = string.Empty;
				specScanRef = specScan;
			}

			if (ipAddr == null)
				ipAddr = IPAddress.None;

			ipPortEndpointConfig = new IPPortEndpointConfig(elementName, new IPEndPoint(ipAddr, portAttribValue), ec);

			return ipPortEndpointConfig.IsValid;
		}

		public static readonly Dictionary<string, IPAddress> CannedIPAddresses = new Dictionary<string, IPAddress>()
		{
			{"", IPAddress.Any},
			{"Any", IPAddress.Any},
			{"Broadcast", IPAddress.Broadcast}, 
			{"LoopBack", IPAddress.Loopback}, 
			{"localhost", IPAddress.Loopback}, 
			{"None", IPAddress.None}, 
			{"IPv6Any", IPAddress.IPv6Any},
			{"IPv6Loopback", IPAddress.IPv6Loopback},
			{"IPv6None", IPAddress.IPv6None},
		};
	}

	#endregion

	//-----------------------------------------------------------------
	#region TcpClientPort class

	/// <summary>Provides an implementation of the SerialIO PortBase class for use as a TCP client/connection initiator.</summary>
    internal class TcpClientPort : PortBase
	{
		#region CTor, DTor

		public TcpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig) : this(portConfig, ipPortEndpointConfig, "TcpClientPort") {}

		public TcpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig, string className)
			: base(portConfig, className)
		{
			targetEPConfig = ipPortEndpointConfig;

            PortBehavior = new PortBehaviorStorage() { DataDeliveryBehavior = DataDeliveryBehavior.ByteStream, IsNetworkPort = true, IsClientPort = true };

			PrivateBaseState = new BaseState(false, true);
			PublishBaseState("object constructed");
		}

		protected override void DisposeCalledPassdown(DisposeType disposeType)		// this is called after StopPart has completed during dispose
		{
			base.DisposeCalledPassdown(disposeType);

			if (disposeType == DisposeType.CalledExplicitly)
				DisposeDataSocket();
		}

		void CreateDataSocket()
		{
			Socket s = new Socket(targetEPConfig.IPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			UseDataSocket(s);

			dataSP.Bind(new IPEndPoint(IPAddress.Any, 0));
        }

		protected void UseDataSocket(Socket s)
		{
			dataSP = s;

			dataSP.LingerState = new LingerOption(false, 0);
			dataSP.NoDelay = true;
			dataSP.Blocking = false;

			if (PortConfig.RxBufferSize != 0)
				dataSP.ReceiveBufferSize = (int) PortConfig.RxBufferSize;

			if (PortConfig.TxBufferSize != 0)
				dataSP.SendBufferSize = (int) PortConfig.TxBufferSize;

			dataSP.ReceiveTimeout = 0;		// read operations are non-blocking at this level.
			dataSP.SendTimeout = 0;			// write operations are non-blocking at this level.

            lastReadResult = ActionResultEnum.None;


            SelectSocketMonitor.Instance.AddSocketToList(dataSP, true, false, true, threadWakeupNotifier);
        }

        protected void DisposeDataSocket() 
        {
            DisposeDataSocket(true); 
        }

		protected void DisposeDataSocket(bool clearLastReadResult)
		{
            if (dataSP != null)
                SelectSocketMonitor.Instance.RemoveSocketFromList(dataSP);

			MosaicLib.Utils.Fcns.DisposeOfObject(ref dataSP);
            if (clearLastReadResult)
                lastReadResult = ActionResultEnum.None;
		}

		#endregion

		#region private and protected fields, properties and methods

		IPPortEndpointConfig targetEPConfig = null;
		protected Socket dataSP = null;
        protected ActionResultEnum lastReadResult = ActionResultEnum.None;

		#endregion

		protected override string InnerPerformGoOnlineAction(string actionName, bool andInitialize)
		{
			string faultCode = null;

			try
			{
				if (dataSP != null && andInitialize)
				{
					DisposeDataSocket();
					SetBaseState(ConnState.Disconnected, actionName + ".Inner: active connection closed by initialize", true);
				}

				if (dataSP == null)
					CreateDataSocket();

				if (dataSP == null)
				{
					faultCode = "Could not create Socket";
					SetBaseState(ConnState.ConnectFailed, actionName + ".Inner: Failed:" + faultCode, true);
					return faultCode;
				}

				if (dataSP.Connected)
					return string.Empty;

				SetBaseState(ConnState.Connecting, actionName + ".Inner: Attempting to go online", true);

				bool completed = false;
				using (SocketAsyncEventArgs saea = new SocketAsyncEventArgs())
				{
					QpcTimeStamp connTOAfter = QpcTimeStamp.Now + PortConfig.ConnectTimeout;

					saea.RemoteEndPoint = targetEPConfig.IPEndPoint;

					saea.Completed += delegate(object source, SocketAsyncEventArgs eventArgs) 
					{
						if (eventArgs.SocketError == SocketError.Success)
							completed = true;
						else
							faultCode = "Connect attempt failed with error:" + eventArgs.SocketError.ToString();

						threadWakeupNotifier.Notify(); 
					};

					dataSP.ConnectAsync(saea);

					while (!completed && QpcTimeStamp.Now <= connTOAfter && faultCode == null)
					{
						WaitForSomethingToDo();
					}
				}

				if (completed && !dataSP.Connected)
				{
					faultCode = "Internal: ConnectAsync completed in non-connected state";
					completed = false;
				}

				if (!completed && string.IsNullOrEmpty(faultCode))
					faultCode = Utils.Fcns.CheckedFormat("Connect failed: socket not connected to {0} after {1} seconds", targetEPConfig.IPEndPoint.ToString(), PortConfig.ConnectTimeout.TotalSeconds.ToString("f3"));

				if (!completed)
				{
					dataSP.Close();
					DisposeDataSocket();

					SetBaseState(ConnState.ConnectionFailed, actionName + ".Inner: Failed:" + faultCode, true);
					return faultCode;
				}
			}
			catch (System.Exception ex)
			{
				faultCode = ex.ToString(ExceptionFormat.TypeAndMessage);
			}

			if (string.IsNullOrEmpty(faultCode))
			{
				SetBaseState(ConnState.Connected, actionName + ".Inner.Done", true);
				return string.Empty;
			}
			else
			{
				SetBaseState(ConnState.ConnectFailed, actionName + ".Inner.Failed", true);
				return faultCode;
			}
		}

		protected override string InnerPerformGoOfflineAction(string actionName)
		{
			string faultCode = null;

			try
			{
				if (dataSP != null)
					DisposeDataSocket();
			}
			catch (System.Exception ex)
			{
				faultCode = ex.ToString(ExceptionFormat.TypeAndMessage);
			}

			if (string.IsNullOrEmpty(faultCode))
			{
				SetBaseState(ConnState.Disconnected, actionName + ".Inner: Done", true);
				return string.Empty;
			}
			else
			{
				SetBaseState(ConnState.ConnectionFailed, actionName + ".Inner: Failed:" + faultCode, true);
				return faultCode;
			}
		}

        private QpcTimer readBytesAvailableSelectHoldoffTimer = new QpcTimer() { TriggerIntervalInSec = 0.1, AutoReset = true, Started = true };

		protected override int InnerReadBytesAvailable
		{
			get
			{
 				if (dataSP == null)
					return 0;

                try
                {
                    int availableBytes = dataSP.Available;

                    if (readBytesAvailableSelectHoldoffTimer.IsTriggered && (availableBytes == 0) && dataSP.Poll(0, SelectMode.SelectRead))
                        availableBytes = Math.Max(dataSP.Available, 1);       // force an indication of at least 1 byte if the socket indicates there are byte available

                    return availableBytes;
                }
                catch
                {
                    return 1;       // cause caller to attempt to read this byte and thus have the read fail.
                }
            }
		}

		protected override bool InnerIsAnyWriteSpaceAvailable 
		{
			get 
			{ 
				return (dataSP != null && dataSP.Connected); 
			} 
		}

        /// <summary>
        /// Method returns true for errors that indicate that an connected TCP socket is no longer usable even if it is not automatically closed by the OS.
        /// </summary>
        private static bool IsPerminantSocketFailure(SocketError sockErr)
        {
            switch (sockErr)
            {
                case SocketError.AccessDenied:
                case SocketError.Fault:
                case SocketError.NetworkDown:
                case SocketError.NetworkUnreachable:
                case SocketError.ConnectionAborted:
                case SocketError.ConnectionReset:
                case SocketError.ConnectionRefused:
                case SocketError.Shutdown:
                case SocketError.TimedOut:
                case SocketError.NotConnected:
                case SocketError.HostDown:
                case SocketError.HostUnreachable:
                case SocketError.Disconnecting:
                    return true;
                default: 
                    return false;
            }
        }

        protected override string InnerHandleRead(byte[] buffer, int startIdx, int maxCount, out int didCount, ref ActionResultEnum readResult)
		{
			didCount = 0;

			if (dataSP == null)
				return "InnerHandleRead failed: socket is null";

			try
			{
				SocketError sockError;

                bool pollRead = dataSP.Poll(0, SelectMode.SelectRead);
				didCount = dataSP.Receive(buffer, startIdx, maxCount, SocketFlags.None, out sockError);
                if (didCount == 0 && maxCount > 0 && sockError == SocketError.Success && pollRead)
                {
                    // the remote socket port has been shutdown - report this to the caller and flag it on the port
                    readResult = ActionResultEnum.ReadRemoteEndHasBeenClosed;
                }

                if (lastReadResult != readResult)
                    lastReadResult = readResult;

				if (sockError != SocketError.Success && sockError != SocketError.WouldBlock)
				{
					string faultCode = "Socket.Receive failed with error:" + sockError.ToString();

                    if (!dataSP.Connected)
                    {
                        DisposeDataSocket(false);
                        SetBaseState(ConnState.ConnectionFailed, faultCode + " [Error closed socket]", true);
                    }
                    else if (IsPerminantSocketFailure(sockError))
                    {
                        DisposeDataSocket(false);
                        SetBaseState(ConnState.ConnectionFailed, faultCode + " [Perminant Error occured on open socket]", true);
                    }
                    // else we assume that the connection might still work.

					return faultCode;
				}
				
				return string.Empty;
			}
			catch (System.Exception ex)
			{
				return ex.ToString(ExceptionFormat.TypeAndMessage);
			}
		}

        protected override string InnerHandleWrite(byte[] buffer, int startIdx, int count, out int didCount, ref ActionResultEnum writeResult)
		{
			didCount = 0;

			if (dataSP == null)
				return "InnerHandleWrite failed: socket is null";

			try
			{
				SocketError sockError;

				didCount = dataSP.Send(buffer, startIdx, count, SocketFlags.None, out sockError);

				if (sockError != SocketError.Success && sockError != SocketError.WouldBlock)
				{
					string faultCode = "Socket.Send failed with error:" + sockError.ToString();

                    if (!dataSP.Connected)
                    {
                        DisposeDataSocket(false);
                        SetBaseState(ConnState.ConnectionFailed, faultCode + " [Error closed socket]", true);
                    }
                    else if (IsPerminantSocketFailure(sockError))
                    {
                        DisposeDataSocket(false);
                        SetBaseState(ConnState.ConnectionFailed, faultCode + " [Perminant Error occured on open socket]", true);
                    }
                    // else we assume that the connection might still work.

					return faultCode;
    			}
				
				return string.Empty;
			}
			catch (System.Exception ex)
			{
				return ex.ToString(ExceptionFormat.TypeAndMessage);
			}
		}

		protected override bool InnerIsConnected
		{
			get
			{
				bool isConnected = (dataSP != null && dataSP.Connected);
				return isConnected;
			}
		}

        protected virtual void ServicePortConnState()
        {
            ServicePortConnState(ConnState.DisconnectedByOtherEnd);
        }

		protected virtual void ServicePortConnState(ConnState remoteEndHasBeenClosedState)
		{
            if (BaseState.IsConnected && (dataSP == null || !dataSP.Connected))
            {
                DisposeDataSocket(false);
                SetBaseState(ConnState.ConnectionFailed, "Socket is no longer connected", true);
            }

            if (BaseState.IsConnected && (lastReadResult == ActionResultEnum.ReadRemoteEndHasBeenClosed))
            {
                try
                {
                    if (dataSP != null)
                        DisposeDataSocket();

                    SetBaseState(remoteEndHasBeenClosedState, "Socket remote end has been closed", true);
                }
                catch (System.Exception ex)
                {
                    SetBaseState(ConnState.ConnectionFailed, Fcns.CheckedFormat("Unable to close data socket while handling remote end closed: {0}", ex), true);                
                }
            }
		}

		protected override bool WaitForSomethingToDo(Utils.IWaitable waitable, TimeSpan waitTimeLimit)
		{
			ServicePortConnState();

			bool isWaiting = (BaseState.ConnState == ConnState.WaitingForConnect);

			if (dataSP == null && !isWaiting)
				return base.WaitForSomethingToDo(waitable, waitTimeLimit);

			int usec = (int) (waitTimeLimit.TotalSeconds * 1000000.0);
            usec = 1;

            if (dataSP != null)
            {
                bool isReadyReady = dataSP.Poll(usec, SelectMode.SelectRead);
                if (isReadyReady || dataSP.Available != 0)
                    return true;

                if (usec == 0 && pendingReadActionsQueue.Count > 0)
                {
                    System.Threading.Thread.Sleep(0);
                    return false;
                }
            }

            return base.WaitForSomethingToDo(waitable, waitTimeLimit);
        }
	}

	#endregion

	//-----------------------------------------------------------------
	#region TcpServerPort class

	/// <summary>Provides an implementation of the SerialIO PortBase class for use as a TCP client/connection initiator.</summary>
	/// <remarks>
	/// </remarks>

	class TcpServerPort : TcpClientPort
	{
		#region CTor, DTor

		public TcpServerPort(PortConfig portConfig, IPPortEndpointConfig serverPortEndpointConfig)
			: base(portConfig, new IPPortEndpointConfig("", new IPEndPoint(IPAddress.None, 0)), "TcpServerPort")
		{
			serverEPConfig = serverPortEndpointConfig;

            PortBehavior = new PortBehaviorStorage() { DataDeliveryBehavior = DataDeliveryBehavior.ByteStream, IsNetworkPort = true, IsServerPort = true };
		}

		protected override void DisposeCalledPassdown(DisposeType disposeType)		// this is called after StopPart has completed during dispose
		{
			base.DisposeCalledPassdown(disposeType);

			if (disposeType == DisposeType.CalledExplicitly)
				DisposeListenSocket();
		}

		void CreateListenSocketAndStartListening(int allowedListenBacklog)
		{
			listenSP = new Socket(serverEPConfig.IPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			listenSP.LingerState = new LingerOption(false, 0);
			listenSP.NoDelay = true;
			listenSP.Blocking = false;

			listenSP.Bind(serverEPConfig.IPEndPoint);

            listenSP.Listen(allowedListenBacklog);

            SelectSocketMonitor.Instance.AddSocketToList(listenSP, true, false, true, threadWakeupNotifier);
		}

		private void DisposeListenSocket()
		{
            if (listenSP != null)
                SelectSocketMonitor.Instance.RemoveSocketFromList(listenSP);

			MosaicLib.Utils.Fcns.DisposeOfObject(ref listenSP);
		}

		#endregion

		#region private and protected fields, properties and methods

		IPPortEndpointConfig serverEPConfig = null;
		Socket listenSP = null;

		#endregion

		protected override string InnerPerformGoOnlineAction(string actionName, bool andInitialize)
		{
			string faultCode = null;

			try
			{
				if (listenSP != null && andInitialize)
					DisposeListenSocket();

				if (dataSP != null && andInitialize)
				{
					DisposeDataSocket();
					SetBaseState(ConnState.Disconnected, actionName + ".Inner: active connection closed by initialize", true);
				}

                if (listenSP == null)
                {
                    CreateListenSocketAndStartListening(1);
                }

				if (listenSP == null)
				{
					faultCode = "Could not create Socket";
					SetBaseState(ConnState.ConnectFailed, actionName + ".Inner: Failed:" + faultCode, true);
					return faultCode;
				}
			}
			catch (System.Exception ex)
			{
				faultCode = ex.ToString(ExceptionFormat.TypeAndMessage);
			}

			if (string.IsNullOrEmpty(faultCode))
			{
				SetBaseState(base.InnerIsConnected ? ConnState.Connected : ConnState.WaitingForConnect, actionName + ".Inner.Done", true);
				return string.Empty;
			}
			else
			{
				SetBaseState(ConnState.ConnectFailed, actionName + ".Inner.Failed", true);
				return faultCode;
			}
		}

		protected override string InnerPerformGoOfflineAction(string actionName)
		{
			string faultCode = base.InnerPerformGoOfflineAction(actionName);

			try
			{
				if (listenSP != null)
					DisposeListenSocket();
			}
			catch (System.Exception ex)
			{
				if (string.IsNullOrEmpty(faultCode))
					faultCode = ex.ToString(ExceptionFormat.TypeAndMessage);
			}

			if (string.IsNullOrEmpty(faultCode))
			{
				SetBaseState(ConnState.Disconnected, actionName + ".Inner: Done", true);
				return string.Empty;
			}
			else
			{
				SetBaseState(ConnState.ConnectionFailed, actionName + ".Inner: Failed:" + faultCode, true);
				return faultCode;
			}
		}

		protected override void ServicePortConnState()
		{
			base.ServicePortConnState(ConnState.WaitingForConnect);

			bool connPending = (listenSP != null && listenSP.Poll(0, SelectMode.SelectRead));
            bool madeNewConnection = false;

			if (connPending)
			{
                try
                {
                    Socket newSocket = listenSP.Accept();

                    if (dataSP != null)
                    {
                        DisposeDataSocket();
                        SetBaseState(ConnState.Disconnected, "Aborting connection to accept new incomming connection", true);
                    }

                    UseDataSocket(newSocket);

                    string reason = Utils.Fcns.CheckedFormat("Accepted new connection from {0}", newSocket.RemoteEndPoint.ToString());
                    SetBaseState(ConnState.Connected, reason, true);
                    madeNewConnection = true;
                }
                catch (System.Exception ex)
                {
                    Log.Info.Emit("Accept failed for {0}: {1}, ignored", listenSP.LocalEndPoint, ex.ToString(ExceptionFormat.TypeAndMessage));
                }
			}
			
            if (!madeNewConnection && listenSP != null && !BaseState.IsConnected && BaseState.ConnState != ConnState.WaitingForConnect)
			{
				if (BaseState.TimeStamp.Age.TotalSeconds > 0.20)
					SetBaseState(ConnState.WaitingForConnect, "Reverting state after connection closed or lost", true);
			}
		}
	}

	#endregion

	//-----------------------------------------------------------------
}

//-----------------------------------------------------------------
