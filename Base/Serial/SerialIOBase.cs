//-------------------------------------------------------------------
/*! @file SerialIOBase.cs
 * @brief This file contains the definitions of the base class(s) that are used by use case specific implementation objects in the SerialIO parts of this library.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2002 Mosaic Systems Inc., All rights reserved. (C++ library version: SerialPort.h, SerialPort.cpp)
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
	using System.Collections;
	using System.Collections.Generic;
	using MosaicLib.Utils;
	using MosaicLib.Time;
    using MosaicLib.Modular.Common;
    using MosaicLib.Modular.Action;
	using MosaicLib.Modular.Part;

	//-----------------------------------------------------------------
	#region PortBase

    /// <summary>
    /// This class is the base clase for most IPort type objects.  It implements the common logic and execution logic that all (or most) port types share.
    /// </summary>
	public abstract class PortBase : SimpleActivePartBase, IPort
	{
		//-----------------------------------------------------------------
		#region Ctor, DTor

		protected PortBase(PortConfig config, string partType) 
            : base(config.Name, partType, config.SpinWaitTimeLimit) 
		{ 
			portConfig = config;

            // change the default so that SerialPort objects do not automatically flip between Busy and Idle while running commands
            AutomaticallyIncAndDecBusyCountAroundActionInvoke = false;

			Error = Log.Emitter(config.ErrorMesgType);
			Info = Log.Emitter(config.InfoMesgType);
			Debug = Log.Emitter(config.DebugMesgType);
            Trace = Log.Emitter(config.TraceMesgType);
            Logging.Logger logger = Log as Logging.Logger;
            if (logger != null)
               logger.GroupName = config.LoggerGroupID;

			traceDataLogger = new Logging.Logger(config.Name + ".Data", config.TraceDataLoggerGroupID);
			TraceData = traceDataLogger.Emitter(config.TraceDataMesgType);

			BaseStateChangeEmitter = Debug;

			// make all Port actions use the given Trace, Debug and Info level for their State, Done and Error messages.
			ActionLoggingReference.State = Trace;
			ActionLoggingReference.Done = Debug;
			ActionLoggingReference.Error = Info;

            // most serial action progress messages are logged to the trace logger along with the data trace messages
            ActionLoggingConfig traceActionLoggingConfig = new ActionLoggingConfig(config.DebugMesgType, config.InfoMesgType, config.TraceDataMesgType, config.TraceDataMesgType);
            ActionLoggingTraceReference = new ActionLogging(traceDataLogger, traceActionLoggingConfig);

			BaseStatePublishedNotificationList.OnNotify += BaseStateChangedEventHandler;

			if (config.RxPacketEndStrArray.Length > 0 || config.RxPacketEndScannerDelegate != null)
			{
                slidingPacketBuffer = new SlidingPacketBuffer(config.RxBufferSize, config.RxPacketEndStrArray, config.IdleTime);
                if (config.RxPacketEndScannerDelegate != null)
                    slidingPacketBuffer.PacketEndScannerDelegate = config.RxPacketEndScannerDelegate;
			}
		}

		#endregion

		//-----------------------------------------------------------------
		#region IPort Members

		private readonly PortConfig portConfig;
		public string Name { get { return portConfig.Name; } }
		public PortConfig PortConfig { get { return portConfig; } }

        IPortBehavior IPort.PortBehavior { get { return PortBehavior; } }
        protected PortBehaviorStorage PortBehavior { get; set; }

        /// <summary>
        /// Impelmentation and storage class for IPortBehavior interface.
        /// </summary>
        public struct PortBehaviorStorage : IPortBehavior
        {
            /// <summary>Gives the DataDeliveryBehavior enum value that this port believes it implements</summary>
            public DataDeliveryBehavior DataDeliveryBehavior { get; set; }

            /// <summary>Returns true if this port's underlying engine is network based (EtherNet or WIFI, ...)</summary>
            /// <remarks>false for COM ports, true for TCP and UDP ports.</remarks>
            public bool IsNetworkPort { get; set; }

            /// <summary>Returns true if this port's DataDeliveryBehavior is ByteStream</summary>
            public bool IsByteStreamPort { get { return DataDeliveryBehavior == DataDeliveryBehavior.ByteStream; } }
            /// <summary>Returns true if this port's DataDeliveryBehavior is Datagram</summary>
            public bool IsDatagramPort { get { return DataDeliveryBehavior == DataDeliveryBehavior.Datagram; } }
            /// <summary>Returns true if this port generally initiates the connection to the transport medium</summary>
            public bool IsClientPort { get; set; }
            /// <summary>Returns true if this port generally accepts connections from the transport medium</summary>
            public bool IsServerPort { get; set; }
        }

		protected class ReadAction : ActionImplBase<ReadActionParam, NullObj>, IReadAction
		{
			public ReadAction(ActionQueue actionQ, ReadActionParam param, FullActionMethodDelegate<ReadActionParam, NullObj> method, ActionLogging loggingReference)
                : base(actionQ, param, false, method, loggingReference) 
			{ }
		}

		public IReadAction CreateReadAction(ReadActionParam param)
		{
            return new ReadAction(actionQ, param, PerformReadAction, new ActionLogging("Read", ActionLoggingTraceReference));
		}

		protected class WriteAction : ActionImplBase<WriteActionParam, NullObj>, IWriteAction
		{
			public WriteAction(ActionQueue actionQ, WriteActionParam param, FullActionMethodDelegate<WriteActionParam, NullObj> method, ActionLogging loggingReference)
                : base(actionQ, param, false, method, loggingReference) 
			{ }
		}

		public IWriteAction CreateWriteAction(WriteActionParam param)
		{
            return new WriteAction(actionQ, param, PerformWriteAction, new ActionLogging("Write", ActionLoggingTraceReference));
		}

        protected class FlushAction : ActionImplBase<TimeSpan, NullObj>, IFlushAction
		{
			public FlushAction(ActionQueue actionQ, TimeSpan param, FullActionMethodDelegate<TimeSpan, NullObj> method, ActionLogging loggingReference)
                : base(actionQ, param, false, method, loggingReference) 
			{ }
		}

        public IFlushAction CreateFlushAction(TimeSpan flushWaitLimit)
		{
            return new FlushAction(actionQ, flushWaitLimit, PerformFlushAction, new ActionLogging("Flush", ActionLoggingTraceReference));
		}

        public bool HasPacket { get { return (NumPacketsReady > 0); } }
        public int NumPacketsReady { get { return volatileNumberOfPacketsAvailable; } }

        protected class GetNextPacketAction : ActionImplBase<NullObj, Packet>, IGetNextPacketAction
        {
            public GetNextPacketAction(ActionQueue actionQ, FullActionMethodDelegate<NullObj, Packet> method, ActionLogging loggingReference)
                : base(actionQ, null, true, method, loggingReference)
            { }
        }

        public IGetNextPacketAction CreateGetNextPacketAction()
        {
            return new GetNextPacketAction(actionQ, PerformGetNextPacket, new ActionLogging("GetNextPacket", ActionLoggingTraceReference));
        }

		#endregion

		//-----------------------------------------------------------------
		#region IPort action and abstract implementation methods (or abstract overrides to force passdown of abstract to derived class

		protected void PerformReadAction(IProviderActionBase<ReadActionParam, NullObj> action, out string resultCode)
		{
			action.ParamValue.Reset();

			pendingReadActionsQueue.Enqueue(action);

			ServicePendingActions();

			resultCode = null;		// no further action state changes are desired at this point
		}

		protected void PerformWriteAction(IProviderActionBase<WriteActionParam, NullObj> action, out string resultCode)
		{
			action.ParamValue.Reset();

			pendingWriteActionsQueue.Enqueue(action);

			ServicePendingActions();

			resultCode = null;		// no further action state changes are desired at this point
		}

		protected void PerformFlushAction(IProviderActionBase<TimeSpan, NullObj> action, out string resultCode)
		{
			resultCode = HandleFlush("Flush", action.ParamValue);
		}

        protected void PerformGetNextPacket(IProviderActionBase<NullObj, Packet> action, out string resultCode)
        {
            if (HasSlidingBuffer)
            {
                Packet p = slidingPacketBuffer.GetNextPacket();
                if (p != null)
                    GenerateDataTrace("GetNextPacket." + p.Type.ToString(), p.ErrorCode, p.Data, 0, p.Data.Length);
                else
                    GenerateDataTrace("GetNextPacket.<null>", "no packet available", null, 0, 0);

                action.ResultValue = p;
                resultCode = string.Empty;
            }
            else
            {
                action.ResultValue = null;
                resultCode = "Invalid: Port configuration does not support auto rx packetization";
            }
        }

		protected override string PerformGoOnlineAction(bool andInitialize)
		{
			string actionName = (andInitialize ? "GoOnlineAndInitialize" : "GoOnline");

			SetBaseState(UseState.AttemptOnline, actionName + ".Start", true);

			string rc = InnerPerformGoOnlineAction(actionName, andInitialize);

			if (string.IsNullOrEmpty(rc) && InnerReadBytesAvailable != 0)
				HandleFlush(actionName, TimeSpan.FromSeconds(0.100));

			bool success = string.IsNullOrEmpty(rc);

			if (success && !InnerIsConnected && !BaseState.IsConnecting)
			{
				rc = "Internal:PortNotConnectedAfterInnerGoOnlineSucceeded";
				success = false;
			}

			if (success)
				SetBaseState(UseState.Online, actionName + ".Done", true);
			else
				SetBaseState(UseState.AttemptOnlineFailed, actionName + ".Failed", true);

			return (success ? string.Empty : rc);
		}

		protected override string PerformGoOfflineAction()
		{
			string actionName = "GoOffline";

			string rc = InnerPerformGoOfflineAction(actionName);
			bool success = string.IsNullOrEmpty(rc);

			if (success && InnerIsConnected)
			{
				rc = "Internal:PortStillConnectedAfterInnerGoOfflineSucceeded";
				success = false;
			}

			if (success)
				SetBaseState(UseState.Offline, actionName + ".Inner.Done", true);
			else
				SetBaseState(UseState.Offline, actionName + ".Inner.Failed", true);

			return (success ? string.Empty : rc);
		}

        protected override void MainThreadFcn()
        {
            base.MainThreadFcn();

            if (PrivateBaseState.IsOnline)
                PerformGoOfflineAction();
        }

		protected override void PerformMainLoopService()
		{
			ServicePendingActions();

			ServicePortState();
		}

		#endregion

		//-----------------------------------------------------------------
		#region Other abstract utility methods to be implemented by a sub-class

		protected abstract string InnerPerformGoOnlineAction(string actionName, bool andInitialize);
		protected abstract string InnerPerformGoOfflineAction(string actionName);

		protected abstract int InnerReadBytesAvailable { get; }
		protected virtual int InnerWriteSpaceUsed { get { return 0; } }
		protected virtual int InnerWriteSpaceAvailable { get { return 0; } }
		protected virtual bool InnerIsAnyWriteSpaceAvailable { get { return (InnerWriteSpaceUsed < InnerWriteSpaceAvailable); } }

		protected abstract string InnerHandleRead(byte [] buffer, int startIdx, int maxCount, out int didCount, ref ActionResultEnum readResult);
        protected abstract string InnerHandleWrite(byte[] buffer, int startIdx, int count, out int didCount, ref ActionResultEnum readResult);
		protected abstract bool InnerIsConnected { get; }

		#endregion

		//-----------------------------------------------------------------
		#region private and protected fields and related properties

        protected ActionLogging ActionLoggingTraceReference { get; private set; }

		protected readonly Logging.ILogger traceDataLogger = null;

		protected readonly Logging.IMesgEmitter Error;
        protected readonly Logging.IMesgEmitter Info;
        protected readonly Logging.IMesgEmitter Debug;
        protected readonly Logging.IMesgEmitter Trace;
        protected readonly Logging.IMesgEmitter TraceData;

		protected Queue<IProviderActionBase<ReadActionParam, NullObj>> pendingReadActionsQueue = new Queue<IProviderActionBase<ReadActionParam, NullObj>>();
		protected Queue<IProviderActionBase<WriteActionParam, NullObj>> pendingWriteActionsQueue = new Queue<IProviderActionBase<WriteActionParam, NullObj>>();

		protected bool AreAnyActionsPending { get { return (pendingReadActionsQueue.Count != 0 || pendingWriteActionsQueue.Count != 0); } }

		private byte [] flushBuf = new byte [512];

        private volatile int volatileNumberOfPacketsAvailable = 0;
		private SlidingPacketBuffer slidingPacketBuffer = null;
		private bool HasSlidingBuffer { get { return (slidingPacketBuffer != null); } }

		#endregion

		//-----------------------------------------------------------------
		#region Common utility methods

		protected void ServicePendingActions()
        {
            if (AreAnyActionsPending)
            {
                if (!BaseState.IsOnline)
                {
                    CancelPendingActions("Port is not online");
                    return;
                }

                if (!BaseState.IsConnected)
                {
                    CancelPendingActions("Connection is not ready");
                    return;
                }

                if (!InnerIsConnected)
                {
                    CancelPendingActions("Connection is not ready (internal)");
                    return;
                }
            }

            if (AreAnyActionsPending || HasSlidingBuffer)
            {
                QpcTimeStamp now = QpcTimeStamp.Now;

                ServicePendingWriteActions(now);
                ServicePendingReadActions(now);
            }
        }

		protected void ServicePendingReadActions(QpcTimeStamp now)
		{
            ActionResultEnum readResult = ActionResultEnum.None;

            if (HasSlidingBuffer)
			{
				// read characters into the sliding buffer

				int rxBytesAvail = InnerReadBytesAvailable;

				if (rxBytesAvail > 0)
				{
					byte [] buffer = null;
					int nextPutIdx = 0, spaceRemaining = 0;

					slidingPacketBuffer.GetBufferPutAccessInfo(128, out buffer, out nextPutIdx, out spaceRemaining);

					int didCount = 0;
                    string ec = HandleRead("BufRead", buffer, nextPutIdx, spaceRemaining, out didCount, ref readResult);

					if (didCount > 0)
						slidingPacketBuffer.AddedNChars(didCount);

                    if (!string.IsNullOrEmpty(ec) && BaseState.ConnState != ConnState.ConnectionFailed)
                    {
                        // only mark that the connection has failed if a lower level has not already done this (preserve its reason string).
                        // this is only done for connections with a SlidingBuffer since other connectsion can decide when to go offline or 
                        // reset the connection during their normal handling of completed actions.

                        this.SetBaseState(ConnState.ConnectionFailed, ec, true);
                    }
				}

                slidingPacketBuffer.Service();

                volatileNumberOfPacketsAvailable = slidingPacketBuffer.NumPacketsReady;
			}

			while (pendingReadActionsQueue.Count > 0)
			{
				IProviderActionBase<ReadActionParam, NullObj> rdAction = pendingReadActionsQueue.Peek();
				ReadActionParam rdActionParam = rdAction.ParamValue;

				if (!rdActionParam.HasBeenStarted)
					rdActionParam.Start();

				if (rdAction.IsCancelRequestActive)
				{
					rdActionParam.ActionResultEnum = ActionResultEnum.ReadCanceled;
					rdAction.CompleteRequest(rdActionParam.ResultCode = "Action Canceled by request");
					pendingReadActionsQueue.Dequeue();
					continue;
				}

				int gotCount = 0;
				string ec = null;

				if (HasSlidingBuffer)
				{
                    int numPacketsReady = slidingPacketBuffer.NumPacketsReady;
                    if (numPacketsReady > 0)
                    {
                        volatileNumberOfPacketsAvailable = numPacketsReady - 1;

                        Packet p = slidingPacketBuffer.GetNextPacket();

                        if (p == null)
                            ec = "Internal: SB.HasPacket and packet was null";
                        else if (!String.IsNullOrEmpty(p.ErrorCode))
                            ec = p.ErrorCode;

                        int copyCount = (p.Data != null ? p.Data.Length : 0);
                        if (copyCount > rdActionParam.BytesToRead)
                        {
                            if (ec == null)
                                ec = Utils.Fcns.CheckedFormat("Read error: read in packet mode where target buffer size:{0} less than packet data size:{1}", rdActionParam.BytesToRead, copyCount);
                            copyCount = rdActionParam.BytesToRead;
                        }

                        if (copyCount > 0)
                            System.Buffer.BlockCopy(p.Data, 0, rdActionParam.Buffer, 0, copyCount);
                        gotCount = copyCount;
                    }
                    else
                    {
                        volatileNumberOfPacketsAvailable = 0;
                    }
				}
				else
				{
					int rxBytesAvail = InnerReadBytesAvailable;

					if (rxBytesAvail > 0)
					{
						int rdBytes = Math.Min(rxBytesAvail, (rdActionParam.BytesToRead - rdActionParam.BytesRead));

                        ec = HandleRead("Read", rdActionParam.Buffer, rdActionParam.BytesRead, rdBytes, out gotCount, ref readResult);
					}
				}

				if (gotCount > 0)
					rdActionParam.BytesRead += gotCount;

                bool readComplete = (rdActionParam.WaitForAllBytes ? (rdActionParam.BytesRead >= rdActionParam.BytesToRead) : (gotCount > 0));
                if (ec == null && (readResult != ActionResultEnum.None && readResult != ActionResultEnum.ReadDone && readResult != ActionResultEnum.ReadRemoteEndHasBeenClosed))
                    ec = Fcns.CheckedFormat("HandleRead gave: {0}", readResult);
				if (ec == null && readComplete)
					ec = string.Empty;

				TimeSpan elapsed = now - rdActionParam.StartTime;
				bool readTimeout = elapsed > PortConfig.ReadTimeout;

				if (ec == null && readTimeout)
					ec = Utils.Fcns.CheckedFormat("Read failed: timeout after {0} sec, got {1} of {2} bytes", elapsed.TotalSeconds, rdActionParam.BytesRead, rdActionParam.BytesToRead);

				if (ec != null)
				{
					bool readSuccess = (ec == string.Empty);

                    if (readResult == ActionResultEnum.None)
                        rdActionParam.ActionResultEnum = (readSuccess ? ActionResultEnum.ReadDone : (readTimeout ? ActionResultEnum.ReadTimeout : ActionResultEnum.ReadFailed));
                    else
                        rdActionParam.ActionResultEnum = readResult;
					rdAction.CompleteRequest(rdActionParam.ResultCode = ec);
					pendingReadActionsQueue.Dequeue();
					continue;
				}

				break;
			}
		}

		protected void ServicePendingWriteActions(QpcTimeStamp now)
		{
			while (pendingWriteActionsQueue.Count > 0)
			{
				IProviderActionBase<WriteActionParam, NullObj> wrAction = pendingWriteActionsQueue.Peek();
				WriteActionParam wrActionParam = wrAction.ParamValue;

				if (!wrActionParam.HasBeenStarted)
					wrActionParam.Start();

				if (wrAction.IsCancelRequestActive)
				{
					wrActionParam.ActionResultEnum = ActionResultEnum.WriteCanceled;
					wrAction.CompleteRequest(wrActionParam.ResultCode = "Action Canceled by request");
					pendingWriteActionsQueue.Dequeue();
					continue;
				}

				// write more bytes if possible
				string ec = null;
				bool isAnyTxSpaceAvailable = false;
                ActionResultEnum writeResult = ActionResultEnum.None;

				if (isAnyTxSpaceAvailable = InnerIsAnyWriteSpaceAvailable)
				{
					int bytesToTx = wrActionParam.BytesToWrite - wrActionParam.BytesWritten;
					int didCount = 0;

					if (bytesToTx > 0)
					{
						ec = HandleWrite("Write", wrActionParam.Buffer, wrActionParam.BytesWritten, bytesToTx, out didCount, ref writeResult);

						if (didCount > 0)
							wrActionParam.BytesWritten += didCount;
					}

					bool bufferWriteComplete = (wrActionParam.BytesWritten >= wrActionParam.BytesToWrite);

					byte [] txTermBytes = portConfig.TxLineTermBytes;
					int didTermCount = wrActionParam.BytesWritten - wrActionParam.BytesToWrite;
					int termBytesToGo = txTermBytes.Length - didTermCount;

					bool writeComplete = (wrActionParam.BytesWritten >= (wrActionParam.BytesToWrite + txTermBytes.Length));

					bytesToTx = termBytesToGo;

					isAnyTxSpaceAvailable = InnerIsAnyWriteSpaceAvailable;

					if (string.IsNullOrEmpty(ec) && bufferWriteComplete && !writeComplete && isAnyTxSpaceAvailable)
					{
						// write the line termination. - Keep trying until all of it has been written
						ec = HandleWrite("Write", txTermBytes, didTermCount, bytesToTx, out didCount, ref writeResult);
						if (didCount > 0)
							wrActionParam.BytesWritten += didCount;

						writeComplete = (wrActionParam.BytesWritten >= (wrActionParam.BytesToWrite + txTermBytes.Length));
					}

					bool writeFailed = !string.IsNullOrEmpty(ec);
                    if (writeComplete || writeFailed || writeResult != ActionResultEnum.None)
					{
                        bool actionStillActive = wrAction.ActionState.IsPendingCompletion;      // it may alredy have been canceled in some cases
                        if (actionStillActive)
                        {
                            if (writeResult == ActionResultEnum.None)
                                wrActionParam.ActionResultEnum = writeFailed ? ActionResultEnum.WriteFailed : ActionResultEnum.WriteDone;
                            else
                                wrActionParam.ActionResultEnum = writeResult;

                            wrAction.CompleteRequest(wrActionParam.ResultCode = Utils.Fcns.MapNullToEmpty(ec));
                        }

                        // attempt to dequeue the write action that we Peeked above.  If the queue is empty at this point, do not complain as logic in HandleWrite may have already cleared the queue.
                        if (pendingWriteActionsQueue.Count > 0)
                            pendingWriteActionsQueue.Dequeue();

                        continue;
					}
				}

				// check for timeout
				TimeSpan elapsed = now - wrActionParam.StartTime;

				if (wrActionParam.IsNonBlocking)
					ec = Utils.Fcns.CheckedFormat("Write failed: would block len:{0} space:{1}", wrActionParam.BytesToWrite, isAnyTxSpaceAvailable);
				else if (elapsed > portConfig.WriteTimeout)
					ec = Utils.Fcns.CheckedFormat("Write failed: timeout after {0} sec.  did {1} of {2} bytes", elapsed.TotalSeconds.ToString("f6"), wrActionParam.BytesWritten, wrActionParam.BytesToWrite);

				if (!string.IsNullOrEmpty(ec))
				{
					wrActionParam.ActionResultEnum = ActionResultEnum.WriteFailed;
					wrAction.CompleteRequest(wrActionParam.ResultCode = ec);
					pendingWriteActionsQueue.Dequeue();
					continue;
				}

				break;
			}
		}

		protected virtual void ServicePortState()
		{
			IBaseState baseState = PrivateBaseState;

			if (baseState.IsConnected && !InnerIsConnected)
			{
				SetBaseState(ConnState.ConnectionFailed, "Connection Lost", true);
				return;
			}

			if (!baseState.IsOnline || !PortConfig.EnableAutoReconnect)
				return;	
		
			bool stateSupportsAutoReconnect = ((baseState.ConnState == ConnState.ConnectFailed) || (baseState.ConnState == ConnState.ConnectionFailed));
			TimeSpan timeInState = QpcTimeStamp.Now - baseState.TimeStamp;

			if (stateSupportsAutoReconnect && timeInState > PortConfig.ReconnectHoldoff)
			{
				InnerPerformGoOnlineAction("AutoReconnect", true);
			}
		}

		protected string HandleFlush(string actionName, TimeSpan timeLimit)
		{
			if (AreAnyActionsPending)
				CancelPendingActions(actionName + " Action invoked");

            // flush the sliding buffer first.
            if (HasSlidingBuffer && (slidingPacketBuffer.BufferDataCount > 0))
            {
                for (; ; )
                {
                    slidingPacketBuffer.Service();
                    Packet p = slidingPacketBuffer.GetNextPacket();
                    if (p != null)
                        GenerateDataTrace(actionName + " " + p.Type.ToString(), String.Empty, p.Data, 0, p.Data.Length);
                    else
                        break;
                }

                if (slidingPacketBuffer.BufferDataCount > 0)
                {
                    byte[] buffer;
                    int getIdx, getCount;
                    slidingPacketBuffer.GetBufferGetAccessInfo(out buffer, out getIdx, out getCount);
                    GenerateDataTrace(actionName + " Sliding Buffer", String.Empty, buffer, getIdx, getCount);
                    slidingPacketBuffer.FlushBuffer();
                }
            }

			// need to flush line buffer if this port is using one

			QpcTimeStamp actionStartTime = QpcTimeStamp.Now;
			QpcTimeStamp idleStartTime = actionStartTime;
			bool portIsIdle = false;
			bool portWasIdle = false;
			bool idleTimeReached = false;
			int didCount = 0;
            string rc = string.Empty;

			for (; ; )
			{
				QpcTimeStamp now = QpcTimeStamp.Now;
                ActionResultEnum readResult = ActionResultEnum.None;
				rc = HandleRead(actionName, flushBuf, 0, flushBuf.Length, out didCount, ref readResult);
				bool success = string.IsNullOrEmpty(rc);
                bool forceFlushDone = (readResult != ActionResultEnum.None && readResult != ActionResultEnum.ReadDone);

				portIsIdle = (didCount == 0);

				if (!portWasIdle && portIsIdle)
					idleStartTime = QpcTimeStamp.Now;
				else
					idleTimeReached = (now - idleStartTime) > PortConfig.IdleTime;

                if (!success || idleTimeReached || forceFlushDone)
                    break;

				if ((now - actionStartTime) > timeLimit)
				{
					if (!portIsIdle)
						rc = actionName + " time limit reached before port fully idle";

					break;
				}

				WaitForSomethingToDo(TimeSpan.FromMilliseconds(10));		// check at 100 Hz nominal rate.
			}

            return rc;
		}

        protected string HandleRead(string actionName, byte[] buffer, int startIdx, int maxCount, out int didCount, ref ActionResultEnum readResult)
		{
			string rc = InnerHandleRead(buffer, startIdx, maxCount, out didCount, ref readResult);

			GenerateDataTrace(actionName, rc, buffer, startIdx, didCount);

			return (string.IsNullOrEmpty(rc) ? string.Empty : rc);
		}

		protected string HandleWrite(string actionName, byte [] buffer, int startIdx, int count, out int didCount, ref ActionResultEnum writeResult)
		{
            string rc = InnerHandleWrite(buffer, startIdx, count, out didCount, ref writeResult);

			GenerateDataTrace(actionName, rc, buffer, startIdx, didCount);

			return (string.IsNullOrEmpty(rc) ? string.Empty : rc);
		}

		protected void BaseStateChangedEventHandler(object source, IBaseState state)	// synchronously invoked during PublishBaseState, used to cancel pending actions on connection failure.
		{
			bool isConnectedOrConnecting = state.IsConnected || state.IsConnecting;

			if (!isConnectedOrConnecting && AreAnyActionsPending)
				CancelPendingActions(Utils.Fcns.CheckedFormat("Aborted because ConnState is {0}", state.ConnState));
		}

		protected void CancelPendingActions(string reason)
		{
			reason = Utils.Fcns.MapNullOrEmptyTo(reason, "[CancelReasonWasNotSpecified]");

			foreach (IProviderActionBase<ReadActionParam, NullObj> action in pendingReadActionsQueue)
			{
				action.ParamValue.ActionResultEnum = ActionResultEnum.ReadFailed;
				action.ParamValue.ResultCode = reason;
				action.CompleteRequest(reason);
			}

			pendingReadActionsQueue.Clear();

			foreach (IProviderActionBase<WriteActionParam, NullObj> action in pendingWriteActionsQueue)
			{
				action.ParamValue.ActionResultEnum = ActionResultEnum.WriteFailed;
				action.ParamValue.ResultCode = reason;
				action.CompleteRequest(reason);
			}

			pendingWriteActionsQueue.Clear();
		}

		private System.Text.StringBuilder traceSB = new System.Text.StringBuilder();

		void GenerateDataTrace(string actionName, string resultCode, byte [] buffer, int startIdx, int count)
		{
			if (!TraceData.IsEnabled)
				return;

			string mesg = null;

			try
			{
				traceSB.Length = 0;	// clear the string

				traceSB.AppendFormat("<DataTrace action=\"{0}\" count=\"{1}\" rc=\"{2}\">", 
										actionName, count,
										(string.IsNullOrEmpty(resultCode) ? "" : resultCode));

				int idx = 0, endIdx = startIdx + count;

				while (startIdx < endIdx)
				{
                    const int maxSectionSize = 32;
					int sectionCount = System.Math.Min(maxSectionSize, endIdx - startIdx);
					int sectionEndIdx = startIdx + sectionCount;

                    traceSB.AppendFormat("<ascii n=\"{0}\">", sectionCount);

                    for (idx = startIdx; idx < sectionEndIdx; idx++)
                    {
                        Char c = (Char)buffer[idx];
                        bool dotChar = !(Char.IsLetterOrDigit(c) || Char.IsPunctuation(c));

                        traceSB.Append(dotChar ? '.' : c);
                    }

                    traceSB.Append("</ascii>");
                    traceSB.AppendFormat("<hex n=\"{0}\">", sectionCount);

					for (idx = startIdx; idx < sectionEndIdx; idx++)
					{
						byte b = buffer[idx];
						traceSB.Append(b.ToString("x2"));
					}

					traceSB.AppendFormat("</hex>");

					startIdx += sectionCount;
				}

				traceSB.Append("</DataTrace>");

				mesg = traceSB.ToString();
			}
			catch (System.Exception ex)
			{
				mesg = "GenerateDataTrace failed:" + ex.Message;
			}

			TraceData.Emit(mesg);
		}

		#endregion

		//-----------------------------------------------------------------
	}

	#endregion

	//-----------------------------------------------------------------
	#region NullPort

	public class NullPort : PortBase
	{
		public NullPort(PortConfig portConfig) 
            : base(portConfig, "NullPort")
        {
            PortBehavior = new PortBehaviorStorage() { DataDeliveryBehavior = DataDeliveryBehavior.None };
        }

		protected override string InnerPerformGoOnlineAction(string actionName, bool andInitialize)
		{
			SetBaseState(UseState.Online, ConnState.Connected, actionName + ":Done", true);
			return string.Empty;
		}

		protected override string InnerPerformGoOfflineAction(string actionName)
		{
			SetBaseState(UseState.Offline, ConnState.Disconnected, actionName + ":Done", true);
			return string.Empty;
		}

		protected override int InnerReadBytesAvailable
		{
			get { return 0; }
		}

		protected override string InnerHandleRead(byte [] buffer, int startIdx, int maxCount, out int didCount, ref ActionResultEnum readResult)
		{
			didCount = 0;
			return string.Empty;
		}

        protected override string InnerHandleWrite(byte[] buffer, int startIdx, int count, out int didCount, ref ActionResultEnum writeResult)
		{
			didCount = count;
			return string.Empty;
		}

		protected override bool InnerIsConnected
		{
			get { return BaseState.IsConnected; }
		}
	}
	#endregion

	//-----------------------------------------------------------------
}
