//-------------------------------------------------------------------
/*! @file TcpSerialIO.cs
 * @brief This file defines the SerialIO related classes that are used for Tcp based ports (TcpClientPort and TcpServerPort)
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2002 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

namespace MosaicLib.SerialIO
{
	//-----------------------------------------------------------------

	using System;
	using System.Collections.Generic;

	using System.Net;
	using System.Net.Sockets;

	using MosaicLib.Utils;
	using MosaicLib.Time;
	using MosaicLib.Modular.Action;
	using MosaicLib.Modular.Part;

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
                || !specScan.ExtractToken(out elementName))
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
            else if (!specScan.MatchToken("/>"))
            {
                ec = Utils.Fcns.CheckedFormat("Did not find expected element end in SpecStr:'{0}'", specScan.Str);
            }
            else if (!CannedIPAddresses.TryGetValue(addrAttribValue, out ipAddr)
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
	/// <remarks>
	/// </remarks>

	class TcpClientPort : PortBase
	{
		#region CTor, DTor

		public TcpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig) : this(portConfig, ipPortEndpointConfig, "TcpClientPort") {}

		public TcpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig, string className)
			: base(portConfig, className)
		{
			targetEPConfig = ipPortEndpointConfig;

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

			UpdateList();
		}

		protected void DisposeDataSocket()
		{
			MosaicLib.Utils.Fcns.DisposeOfObject(ref dataSP);
			UpdateList();
		}

		#endregion

		#region private and protected fields, properties and methods

		IPPortEndpointConfig targetEPConfig = null;
		protected Socket dataSP = null;
		protected List<Socket> spList = new List<Socket>();

		protected virtual void UpdateList()
		{
			if (dataSP != null)
				spList = new List<Socket> { dataSP };
			else
				spList = new List<Socket>();
		}

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
			catch (SystemException e)
			{
				faultCode = "Exception:" + e.Message;
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
			catch (SystemException e)
			{
				faultCode = "Exception:" + e.Message;
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

		protected override int InnerReadBytesAvailable
		{
			get
			{
				if (dataSP == null)
					return 0;

				return dataSP.Available;
			}
		}

		protected override bool InnerIsAnyWriteSpaceAvailable 
		{
			get 
			{ 
				return (dataSP != null && dataSP.Connected); 
			} 
		}

		protected override string InnerHandleRead(byte [] buffer, int startIdx, int maxCount, out int didCount)
		{
			didCount = 0;

			if (dataSP == null)
				return "InnerHandleRead failed: socket is null";

			try
			{
				SocketError sockError;

				didCount = dataSP.Receive(buffer, startIdx, maxCount, SocketFlags.None, out sockError);

				if (sockError != SocketError.Success && sockError != SocketError.WouldBlock)
				{
					string faultCode = "Socket.Receive failed with error:" + sockError.ToString();
					if (!dataSP.Connected)
						SetBaseState(ConnState.ConnectionFailed, faultCode, true);

					return faultCode;
				}
				
				return string.Empty;
			}
			catch (System.Exception e)
			{
				return "Exception:" + e.Message;
			}
		}

		protected override string InnerHandleWrite(byte [] buffer, int startIdx, int count, out int didCount)
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
						SetBaseState(ConnState.ConnectionFailed, faultCode, true);

					return faultCode;
			}
				
				return string.Empty;
			}
			catch (System.Exception e)
			{
				return "Exception:" + e.Message;
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
			if (BaseState.IsConnected && (dataSP == null || !dataSP.Connected))
				SetBaseState(ConnState.ConnectionFailed, "Socket is no longer connected", true);
		}

		protected override bool WaitForSomethingToDo(Utils.IWaitable waitable, TimeSpan waitTimeLimit)
		{
			ServicePortConnState();

			bool isWaiting = (BaseState.ConnState == ConnState.WaitingForConnect);

			if (dataSP == null && !isWaiting)
				return base.WaitForSomethingToDo(waitable, waitTimeLimit);

			int usec = (int) (waitTimeLimit.TotalSeconds * 1000000.0);

			if (spList.Count > 0)
			{
				if (usec > 0)
					Socket.Select(spList, null, null, usec);
				else if (pendingReadActionsQueue.Count > 0)
					System.Threading.Thread.Sleep(0);
				else
					return base.WaitForSomethingToDo(waitable, waitTimeLimit);
			}
			else
				return base.WaitForSomethingToDo(waitable, waitTimeLimit);

			return (dataSP != null && dataSP.Available != 0);
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
		}

		protected override void DisposeCalledPassdown(DisposeType disposeType)		// this is called after StopPart has completed during dispose
		{
			base.DisposeCalledPassdown(disposeType);

			if (disposeType == DisposeType.CalledExplicitly)
				DisposeListenSocket();
		}

		void CreateListenSocket()
		{
			listenSP = new Socket(serverEPConfig.IPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			listenSP.LingerState = new LingerOption(false, 0);
			listenSP.NoDelay = true;
			listenSP.Blocking = false;

			listenSP.Bind(serverEPConfig.IPEndPoint);
		}

		private void DisposeListenSocket()
		{
			MosaicLib.Utils.Fcns.DisposeOfObject(ref listenSP);
		}

		#endregion

		#region private and protected fields, properties and methods

		IPPortEndpointConfig serverEPConfig = null;
		Socket listenSP = null;

		protected override void UpdateList()
		{
			base.UpdateList();

			if (listenSP != null)
				spList.Add(listenSP);
		}

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
					CreateListenSocket();

				if (listenSP == null)
				{
					faultCode = "Could not create Socket";
					SetBaseState(ConnState.ConnectFailed, actionName + ".Inner: Failed:" + faultCode, true);
					return faultCode;
				}

				listenSP.Listen(1);
			}
			catch (SystemException e)
			{
				faultCode = "Exception:" + e.Message;
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
			catch (SystemException e)
			{
				if (string.IsNullOrEmpty(faultCode))
					faultCode = "Exception:" + e.Message;
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
			base.ServicePortConnState();

			bool connPending = (listenSP != null && listenSP.Poll(0, SelectMode.SelectRead));

			if (connPending)
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
			}
			else if (listenSP != null && !BaseState.IsConnected && BaseState.ConnState != ConnState.WaitingForConnect)
			{
				if (BaseState.TimeStamp.Age.TotalSeconds > 1.0)
					SetBaseState(ConnState.WaitingForConnect, "Reverting state after connection closed or lost", true);
			}
		}
	}

	#endregion

	//-----------------------------------------------------------------
}

//-----------------------------------------------------------------
