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

using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <summary>
        /// static factory method used to create a UdpClientPort from the given portConfig and ipPortEndpointConfig
        /// </summary>
		public static IPort CreateUdpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig)
		{
			return new UdpClientPort(portConfig, ipPortEndpointConfig);
		}

        /// <summary>
        /// static factory method used to create a UdpServerPort from the given portConfig and ipPortEndpointConfig
        /// </summary>
        public static IPort CreateUdpServerPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig)
		{
			return new UdpServerPort(portConfig, ipPortEndpointConfig);
		}
	}

	#endregion

	//-----------------------------------------------------------------
	#region UdpClientPort class

    internal class UdpClientPort : UdpPortBase
    {
        /// <summary>
        /// Standard constructor.  Accepts PortConfig and IPPortEndpointConfig (parsed from PortConfig.SpecStr).
        /// </summary>
        public UdpClientPort(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig)
            : base(portConfig, ipPortEndpointConfig, "UdpClientPort")
        { }
    }

    #endregion

    //-----------------------------------------------------------------
    #region UdpClientPort class

    /// <summary>Provides an implementation of the SerialIO PortBase class for use as a UDP client.</summary>
    internal class UdpPortBase : PortBase
	{
		#region CTor, DTor

		public UdpPortBase(PortConfig portConfig, IPPortEndpointConfig ipPortEndpointConfig, string className)
			: base(portConfig, className)
		{
			targetEPConfig = ipPortEndpointConfig;

            PortBehavior = new PortBehaviorStorage() { DataDeliveryBehavior = DataDeliveryBehavior.Datagram, IsNetworkPort = true, IsClientPort = true };

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

            SelectSocketMonitor.Instance.AddSocketToList(dataSP, true, false, false, threadWakeupNotifier);
		}

		protected void DisposeDataSocket()
		{
            if (dataSP != null)
                SelectSocketMonitor.Instance.RemoveSocketFromList(dataSP);

			MosaicLib.Utils.Fcns.DisposeOfObject(ref dataSP);
		}

		#endregion

		#region private and protected fields, properties and methods

		IPPortEndpointConfig targetEPConfig = null;
		protected Socket dataSP = null;

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
			catch (System.Exception ex)
			{
				faultCode = "Exception:" + ex.Message;
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
			catch (System.Exception ex)
			{
				faultCode = "Exception:" + ex.Message;
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

                try
                {
                    return dataSP.Available;
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
				return (dataSP != null); 
			} 
		}

        protected override string InnerHandleRead(byte[] buffer, int startIdx, int maxCount, out int didCount, ref ActionResultEnum readResult)
		{
			EndPoint fromEP;
			string ec = string.Empty;

			for (;;)
			{
				fromEP = targetEPConfig.IPEndPoint;
				ec = InnerHandleRead(buffer, startIdx, maxCount, out didCount, ref readResult, ref fromEP);

				if (fromEP != targetEPConfig.IPEndPoint && didCount != 0)
					continue;

				return ec;
			}
		}

        protected override string InnerHandleWrite(byte[] buffer, int startIdx, int count, out int didCount, ref ActionResultEnum writeResult)
		{
			return InnerHandleWrite(buffer, startIdx, count, out didCount, ref writeResult, targetEPConfig.IPEndPoint);
		}

        protected string InnerHandleRead(byte[] buffer, int startIdx, int maxCount, out int didCount, ref ActionResultEnum readResult, ref EndPoint remoteEP)
		{
			didCount = 0;

			if (dataSP == null)
				return "InnerHandleRead failed: socket is null";

			try
			{
				didCount = dataSP.ReceiveFrom(buffer, startIdx, maxCount, SocketFlags.None, ref remoteEP);

				return string.Empty;
			}
			catch (System.Exception ex)
			{
				return "Exception:" + ex.Message;
			}
		}

        protected string InnerHandleWrite(byte[] buffer, int startIdx, int count, out int didCount, ref ActionResultEnum writeResult, EndPoint remoteEP)
		{
			didCount = 0;

			if (dataSP == null)
				return "InnerHandleWrite failed: socket is null";

			try
			{
				didCount = dataSP.SendTo(buffer, startIdx, count, SocketFlags.None, remoteEP);

				return string.Empty;
			}
			catch (System.Exception ex)
			{
				return "Exception:" + ex.Message;
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
	#region UdpServerPort class

	/// <summary>Provides an implementation of the SerialIO PortBase class for use as a UDP server.</summary>
	class UdpServerPort : UdpPortBase
	{
		#region CTor, DTor

		public UdpServerPort(PortConfig portConfig, IPPortEndpointConfig serverPortEndpointConfig)
			: base(portConfig, new IPPortEndpointConfig("", new IPEndPoint(IPAddress.Any, 0)), "UdpServerPort")
		{
			serverEPConfig = serverPortEndpointConfig;

            PortBehavior = new PortBehaviorStorage() { DataDeliveryBehavior = DataDeliveryBehavior.Datagram, IsNetworkPort = true, IsServerPort = true };
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

		protected override string InnerHandleRead(byte [] buffer, int startIdx, int maxCount, out int didCount, ref ActionResultEnum readResult)
		{
			EndPoint entryEP = connectedEP;
			string ec = string.Empty;

			ec = InnerHandleRead(buffer, startIdx, maxCount, out didCount, ref readResult, ref connectedEP);

			if (entryEP != connectedEP && connectedEP != epNone)
			{
				string reason = Utils.Fcns.CheckedFormat("Received data from new EndPoint:{0}, disconnected from old EndPoint:{1}", connectedEP.ToString(), entryEP.ToString());
				SetBaseState(ConnState.Connected, reason, true);
			}

			return ec;
		}

        protected override string InnerHandleWrite(byte[] buffer, int startIdx, int count, out int didCount, ref ActionResultEnum writeResult)
		{
			if (connectedEP != epNone)
				return InnerHandleWrite(buffer, startIdx, count, out didCount, ref writeResult, connectedEP);
			else
			{
				didCount = 0;
				return "InnerHandleWrite failed: there is no target endpoint to send this data to";
			}
		}
	}

	#endregion

    //-----------------------------------------------------------------
    #region SelectSocketMonitor

    /// <summary>
    /// This class implements a singleton threaded object that is used to repeatedly call select on a configured set of sockets and then inform
    /// the registered clients about desired socket activities by notifying them using an INotifyable object.
    /// (not based on SimpleActivePart)
    /// </summary>
    public class SelectSocketMonitor : DisposableBase
    {
        /// <summary>
        /// Provides public access to the singleton instance of this class.  
        /// This instance is constructed on first access to this static property.
        /// This instance's worker thread is automatically started while adding the first socket to monitor
        /// </summary>
        public static SelectSocketMonitor Instance { get { return singletonHelper.Instance; } }
        private static SingletonHelperBase<SelectSocketMonitor> singletonHelper = new SingletonHelperBase<SelectSocketMonitor>(() => new SelectSocketMonitor());

        /// <summary>
        /// Private constructor.  Instance may only be constructed by delegate used by the singletonHelper defined above.
        /// </summary>
        private SelectSocketMonitor()
        {
            string className = GetType().Name;

            Logger = new Logging.Logger(className);
            Trace = new Logging.Logger(className + ".Trace", Logging.LookupDistributionGroupName, Logging.LogGate.All);

            AddExplicitDisposeAction(() => Shutdown());
        }

        /// <summary>Defines the Logger instance that is used to log a record of basic use of this object.</summary>
        public Logging.ILogger Logger { get; private set; }
        /// <summary>Defines the Trace logger instance that is used to record trace records about this object.</summary>
        public Logging.ILogger Trace { get; private set; }

        /// <summary>Stops the background thread if it is running.</summary>
        public void Shutdown()
        {
            StopBackgroundThreadIfNeeded();
        }

        /// <summary>Starts the background thread if it is not already running.</summary>
        public void StartIfNeeded()
        {
            StartBackgroundThreadIfNeeded();
        }

        /// <summary>Adds/Replaces the indicated socket, settings and notification target to the monitor list and starts the background service thread if it is not already running.</summary>
        public void AddSocketToList(Socket s, bool read, bool write, bool error, INotifyable notifyTarget)
        {
            TableItem tableItem = new TableItem() { Socket = s, read = read, write = write, error = error, notifyTarget = notifyTarget };

            if (!tableItem.IsUsable)
            {
                string notUsableReason = tableItem.NotUsableReason;
                tableItem.LastLoggedNotUsableReason = notUsableReason;

                string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;

                Logger.Debug.Emit("{0} given unusable socket {1} : {2}", methodName, tableItem.shAsInt64, notUsableReason);
            }

            lock (userTableMutex)
            {
                // the following passes ownership of the tableItem to this object's service thread.
                userTableDictionary[s] = tableItem;
                rebuildTablesFromUserTable = true;
            }

            StartBackgroundThreadIfNeeded();

            threadWaitEventNotifier.Notify();
        }

        /// <summary>
        /// Attempts to remove the indicated socket from the set of sockets that are being monitored.  
        /// Has no effect if the socket has already been removed or has never been added.
        /// </summary>
        public void RemoveSocketFromList(Socket s)
        {
            lock (userTableMutex)
            {
                if (userTableDictionary.ContainsKey(s))
                    userTableDictionary.Remove(s);

                rebuildTablesFromUserTable = true;
            }

            threadWaitEventNotifier.Notify();
        }

        private void StartBackgroundThreadIfNeeded()
        {
            if (threadStarted)
                return;

            lock (threadStartMutex)
            {
                if (!threadStarted)
                {
                    stopThread = false;

                    serviceThread = new System.Threading.Thread(ThreadEntryPoint) { Name = Logger.Name, IsBackground = true };
                    serviceThread.Start();

                    threadStarted = true;
                }
            }
        }

        private void StopBackgroundThreadIfNeeded()
        {
            lock (threadStartMutex)
            {
                stopThread = true;
                threadWaitEventNotifier.Notify();

                if (serviceThread != null)
                {
                    serviceThread.Join();
                    serviceThread = null;

                    threadStarted = false;
                }
            }
        }

        WaitEventNotifier threadWaitEventNotifier = new WaitEventNotifier(WaitEventNotifier.Behavior.WakeOne);

        private object userTableMutex = new object();
        private Dictionary<Socket, TableItem> userTableDictionary = new Dictionary<Socket, TableItem>();
        private volatile bool rebuildTablesFromUserTable = false;

        private class TableItem
        {
            public Socket Socket 
            { 
                get { return socket; } 
                set 
                { 
                    socket = value;
                    shAsInt64 = ((socket != null && socket.Handle != null) ? socket.Handle.ToInt64() : -1);
                } 
            }
            public Socket socket { get; private set; }
            public bool read, write, error;
            public INotifyable notifyTarget;

            public Int64 shAsInt64 = -1;
            public bool IsHandleValid { get { return ((shAsInt64 != 0) && (shAsInt64 != -1)); } }

            public bool touched;

            public TableItem() { }

            /// <summary>
            /// Returns true if the socket is usable and false if it is not.  See NotUsableReason for more details.
            /// </summary>
            public bool IsUsable
            {
                get
                {
                    return NotUsableReason.IsNullOrEmpty();
                }
            }

            /// <summary>
            /// Returns empty string if the socket is usable and non-empty description of the reason if the socket is not usable
            /// </summary>
            public string NotUsableReason
            {
                get
                {
                    bool isActive = (read || write || error);
                    if (!isActive)
                        return "no monitor purpose selected (!read && !write && !error)";

                    if (socket == null)
                        return "given socket is null";

                    if (notifyTarget == null)
                        return "notifyTarget is null";

                    bool isConnectedOrBound = (socket.Connected || socket.IsBound);

                    if (!socket.Connected && !socket.IsBound)
                        return "socket is neither connected nor bound";
 
                    if (!IsHandleValid)
                        return "socket's handle '{0}' is not valid".CheckedFormat(shAsInt64);

                    bool socketTypeIsUnknown = (socket.SocketType == SocketType.Unknown);

                    if (socketTypeIsUnknown)
                        return "socket's SocketType is Unknown";

                    return string.Empty;
                }
            }

            public bool IsInActiveList { get; set; }
            public string LastLoggedNotUsableReason { get; set; }
        }

        private object threadStartMutex = new object();
        private System.Threading.Thread serviceThread = null;
        private volatile bool threadStarted = false;
        private volatile bool stopThread = false;

        private const int emptySetWaitMSec = 10;              // 10 msec
        private const int selectThrewWaitMSec = 20;           // 20 msec
        private const int selectWaitMicroSec = 20000;        // 20 msec

        private Dictionary<Socket, TableItem> referenceTableItemDictionary = new Dictionary<Socket, TableItem>();
        private TableItem[] activeTableItemArray = new TableItem[0];
        private TableItem[] inactiveTableItemArray = new TableItem[0];
        private QpcTimer rebuildTableItemsTimer = new QpcTimer() { TriggerIntervalInSec = 0.200 };

        private Socket[] activeReadSocketArray = new Socket[0];
        private Socket[] activeWriteSocketArray = new Socket[0];
        private Socket[] activeErrorSocketArray = new Socket[0];

        private List<Socket> selectReadSocketListParam = null;
        private List<Socket> selectWriteSocketListParam = null;
        private List<Socket> selectErrorSocketListParam = null;

        private void ThreadEntryPoint()
        {
            using (var eeTrace = new Logging.EnterExitTrace(Trace, "ThreadEntryPoint"))
            {
                rebuildTablesFromUserTable = true;

                for (; ; )
                {
                    if (stopThread)
                        break;

                    if (!inactiveTableItemArray.IsNullOrEmpty() && rebuildTableItemsTimer.StartIfNeeded().IsTriggered)
                        rebuildTablesFromUserTable = true;

                    if (rebuildTablesFromUserTable)
                    {
                        rebuildTablesFromUserTable = false;

                        RebuildTablesFromUserTable();
                    }

                    try
                    {
                        if (!activeReadSocketArray.IsNullOrEmpty())
                            (selectReadSocketListParam != null ? selectReadSocketListParam : (selectReadSocketListParam = new List<Socket>())).AddRange(activeReadSocketArray);

                        if (!activeWriteSocketArray.IsNullOrEmpty())
                            (selectWriteSocketListParam != null ? selectWriteSocketListParam : (selectWriteSocketListParam = new List<Socket>())).AddRange(activeWriteSocketArray);

                        if (!activeErrorSocketArray.IsNullOrEmpty())
                            (selectErrorSocketListParam != null ? selectErrorSocketListParam : (selectErrorSocketListParam = new List<Socket>())).AddRange(activeErrorSocketArray);

                        if (!selectReadSocketListParam.IsNullOrEmpty() || !selectWriteSocketListParam.IsNullOrEmpty() || !selectErrorSocketListParam.IsNullOrEmpty())
                            Socket.Select(selectReadSocketListParam, selectWriteSocketListParam, selectErrorSocketListParam, selectWaitMicroSec);
                        else
                            threadWaitEventNotifier.WaitMSec(emptySetWaitMSec);

                        SetTableItemTouchedFlags(referenceTableItemDictionary, selectReadSocketListParam);
                        SetTableItemTouchedFlags(referenceTableItemDictionary, selectWriteSocketListParam);
                        SetTableItemTouchedFlags(referenceTableItemDictionary, selectErrorSocketListParam);

                        bool anyTouched = false;
                        foreach (TableItem tableItem in activeTableItemArray)
                        {
                            if (tableItem.touched)
                            {
                                tableItem.notifyTarget.Notify();
                                tableItem.touched = false;
                                anyTouched = true;
                            }
                        }

                        if (anyTouched)
                            System.Threading.Thread.Sleep(1);      // prevent free spin of this thread when signaling events.
                    }
                    catch (System.Exception ex)
                    {
                        Trace.Debug.Emit("Select failed: {0}", ex.Message);

                        threadWaitEventNotifier.WaitMSec(selectThrewWaitMSec);
                        rebuildTablesFromUserTable = true;
                    }
                }
            }
        }

        List<TableItem> allTableItemsList = new List<TableItem>();

        private void RebuildTablesFromUserTable()
        {
            allTableItemsList.Clear();

            lock (userTableMutex)
            {
                allTableItemsList.AddRange(userTableDictionary.Values);
            }

            foreach (var tableItem in allTableItemsList)
            {
                string notUsableReason = tableItem.NotUsableReason;
                tableItem.IsInActiveList = notUsableReason.IsNullOrEmpty();

                if (tableItem.LastLoggedNotUsableReason.MapNullToEmpty() != notUsableReason)
                    Logger.Debug.Emit("Socket.Handle {0} Not Usable Reason changed to '{0}' [from '{1}']", tableItem.shAsInt64, notUsableReason, tableItem.LastLoggedNotUsableReason);

                tableItem.LastLoggedNotUsableReason = notUsableReason;
            }

            activeTableItemArray = allTableItemsList.Where(item => item.IsInActiveList).ToArray();
            inactiveTableItemArray = allTableItemsList.Where(item => !item.IsInActiveList).ToArray();

            referenceTableItemDictionary.Clear();

            foreach (TableItem tableItem in activeTableItemArray)
            {
                referenceTableItemDictionary.Add(tableItem.socket, tableItem);
            }

            activeReadSocketArray = activeTableItemArray.Where(tableItem => tableItem.read).Select(tableItem => tableItem.socket).ToArray();
            activeWriteSocketArray = activeTableItemArray.Where(tableItem => tableItem.write).Select(tableItem => tableItem.socket).ToArray();
            activeErrorSocketArray = activeTableItemArray.Where(tableItem => tableItem.error).Select(tableItem => tableItem.socket).ToArray();

            int numActiveSockets = activeTableItemArray.Length;
            int numInactiveSockets = inactiveTableItemArray.Length;

            if (lastNumActiveSockets != numActiveSockets || lastNumInactiveSockets != numInactiveSockets)
            {
                if (inactiveTableItemArray.IsNullOrEmpty())
                    Logger.Debug.Emit("Select table rebuilt.  Contains {0} active sockets", activeTableItemArray.Length);
                else
                    Logger.Debug.Emit("Select table rebuilt.  Contains {0} active sockets and {1} inactive sockets", activeTableItemArray.Length, inactiveTableItemArray.Length);

                lastNumActiveSockets = numActiveSockets;
                lastNumInactiveSockets = numInactiveSockets;
            }
        }

        private int lastNumActiveSockets = 0, lastNumInactiveSockets = 0;

        private static void SetTableItemTouchedFlags(Dictionary<Socket, TableItem> referenceTableItemDictionary, List<Socket> socketList)
        {
            if (socketList != null && socketList.Count > 0)
            {
                foreach (var socket in socketList)
                {
                    TableItem tableItem;
                    if (socket != null && referenceTableItemDictionary.TryGetValue(socket, out tableItem) && tableItem != null)
                        tableItem.touched = true;
                }

                socketList.Clear();
            }
        }
    }

    #endregion

    //-----------------------------------------------------------------
}

//-----------------------------------------------------------------
