//-------------------------------------------------------------------
/*! @file UdpSerialIO.cs
 * @brief This file defines the SerialIO related classes that are used for Udp based ports (UdpClientPort and UdpServerPort)
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
		public static IPort CreateUdpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig)
		{
			return new UdpClientPort(portConfig, ipPortEndpointConfig);
		}

		public static IPort CreateUdpServerPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig)
		{
			return new UdpServerPort(portConfig, ipPortEndpointConfig);
		}
	}

	#endregion

	//-----------------------------------------------------------------
	#region UdpClientPort class

	/// <summary></summary>
	/// <remarks>
	/// </remarks>

	class UdpClientPort : PortBase
	{
		#region CTor, DTor

		public UdpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig) : this(portConfig, ipPortEndpointConfig, "UdpClientPort") {}

		public UdpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig, string className)
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

		protected virtual void CreateDataSocket()
		{
			Socket s = new Socket(targetEPConfig.IPEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

			UseDataSocket(s);

			dataSP.Bind(new IPEndPoint(IPAddress.Any, 0));
		}

		protected void UseDataSocket(Socket s)
		{
			dataSP = s;

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

			}
			catch (System.Exception e)
			{
				faultCode = "Exception:" + e.Message;
			}

			if (string.IsNullOrEmpty(faultCode))
			{
				if (targetEPConfig.Address != IPAddress.None || targetEPConfig.Port != 0)
					SetBaseState(ConnState.Connected, actionName + ".Inner.Done", true);
				else
					SetBaseState(ConnState.WaitingForConnect, actionName + ".Inner.Done no target", true);

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
			catch (System.Exception e)
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
				return (dataSP != null); 
			} 
		}

		protected override string InnerHandleRead(byte [] buffer, int startIdx, int maxCount, out int didCount)
		{
			EndPoint fromEP;
			string ec = string.Empty;

			for (;;)
			{
				fromEP = targetEPConfig.IPEndPoint;
				ec = InnerHandleRead(buffer, startIdx, maxCount, out didCount, ref fromEP);

				if (fromEP != targetEPConfig.IPEndPoint && didCount != 0)
					continue;

				return ec;
			}
		}

		protected override string InnerHandleWrite(byte [] buffer, int startIdx, int count, out int didCount)
		{
			return InnerHandleWrite(buffer, startIdx, count, out didCount, targetEPConfig.IPEndPoint);
		}

		protected string InnerHandleRead(byte [] buffer, int startIdx, int maxCount, out int didCount, ref EndPoint remoteEP)
		{
			didCount = 0;

			if (dataSP == null)
				return "InnerHandleRead failed: socket is null";

			try
			{
				didCount = dataSP.ReceiveFrom(buffer, startIdx, maxCount, SocketFlags.None, ref remoteEP);

				return string.Empty;
			}
			catch (System.Exception e)
			{
				return "Exception:" + e.Message;
			}
		}

		protected string InnerHandleWrite(byte [] buffer, int startIdx, int count, out int didCount, EndPoint remoteEP)
		{
			didCount = 0;

			if (dataSP == null)
				return "InnerHandleWrite failed: socket is null";

			try
			{
				didCount = dataSP.SendTo(buffer, startIdx, count, SocketFlags.None, remoteEP);

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
				bool isConnected = (dataSP != null);
				return isConnected;
			}
		}

		protected virtual void ServicePortConnState() { }

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
	#region UdpServerPort class

	/// <summary>Provides an implementation of the SerialIO PortBase class for use as a TCP client/connection initiator.</summary>
	/// <remarks>
	/// </remarks>

	class UdpServerPort : UdpClientPort
	{
		#region CTor, DTor

		public UdpServerPort(PortConfig portConfig, IPPortEndpointConfig serverPortEndpointConfig)
			: base(portConfig, new IPPortEndpointConfig("", new IPEndPoint(IPAddress.Any, 0)), "UdpServerPort")
		{
			serverEPConfig = serverPortEndpointConfig;
		}

		protected override void DisposeCalledPassdown(DisposeType disposeType)		// this is called after StopPart has completed during dispose
		{
			base.DisposeCalledPassdown(disposeType);
		}

		protected override void CreateDataSocket()
		{
			Socket s = new Socket(serverEPConfig.IPEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

			UseDataSocket(s);

			dataSP.Bind(serverEPConfig.IPEndPoint);
		}


		#endregion

		#region private and protected fields, properties and methods

		IPPortEndpointConfig serverEPConfig = null;

		static EndPoint epNone = new IPEndPoint(IPAddress.None, 0);
		EndPoint connectedEP = epNone;

		#endregion

		protected override string InnerHandleRead(byte [] buffer, int startIdx, int maxCount, out int didCount)
		{
			EndPoint entryEP = connectedEP;
			string ec = string.Empty;

			ec = InnerHandleRead(buffer, startIdx, maxCount, out didCount, ref connectedEP);

			if (entryEP != connectedEP && connectedEP != epNone)
			{
				string reason = Utils.Fcns.CheckedFormat("Received data from new EndPoint:{0}, disconnected from old EndPoint:{1}", connectedEP.ToString(), entryEP.ToString());
				SetBaseState(ConnState.Connected, reason, true);
			}

			return ec;
		}

		protected override string InnerHandleWrite(byte [] buffer, int startIdx, int count, out int didCount)
		{
			if (connectedEP != epNone)
				return InnerHandleWrite(buffer, startIdx, count, out didCount, connectedEP);
			else
			{
				didCount = 0;
				return "InnerHandleWrite failed: there is no target endpoint to send this data to";
			}
		}
	}

	#endregion

	//-----------------------------------------------------------------
}

//-----------------------------------------------------------------
