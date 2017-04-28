//-------------------------------------------------------------------
/*! @file QueueLogMessageHandler.cs
 *  @brief This file provides an implementation of the QueueLogMessageHandler LMH requeuing class.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2007 Mosaic Systems Inc.  (C++ library version)
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

namespace MosaicLib
{
	public static partial class Logging
	{
		public static partial class Handlers
		{
            /// <summary>
            /// This class implements a special type of LogMessageHandler.  It is given another LMH instance and acts as a passthrough where messages that are given to this
            /// LMH are queued and then an internal thread periodically passes larger groups of the queue messages into the given target LMH thus loosely decoupling the main message
            /// distribution system from the per message performance of the target LMH.  This also improves the performance with some types of target LMH where there is a relatively
            /// large cost to start writing a message but a relatively small cost two write two in a row after setting up to the write the first (file types for example).
            /// </summary>
			public class QueueLogMessageHandler : CommonLogMessageHandlerBase
            {
                /// <summary>
                /// Single targetLMH constructor.  Derives name and LoggingConfig values from the given targetLMH.
                /// </summary>
                /// <param name="targetLMH">Gives the target LMH instance to which the queued messages will be delivered.</param>
                /// <param name="maxQueueSize">Defines te maximum number of messages that can be held internally before messages are lost.</param>
                public QueueLogMessageHandler(ILogMessageHandler targetLMH, int maxQueueSize = DefaultMesgQueueSize)
                    : this(targetLMH.Name + ".q", new[] { targetLMH }, maxQueueSize)
                { }

                /// <summary>
                /// Multiple targetLMH constructor.  Derives LoggingConfig values from the given targetLMH
                /// </summary>
                /// <param name="name">Gives the name of this LMH - different than the names of the target LMH instances</param>
                /// <param name="targetLMHArray">Gives the set of LMH instance that are to be given the dequeued LogMessages.</param>
                /// <param name="maxQueueSize">Defines te maximum number of messages that can be held internally before messages are lost.</param>
                /// <param name="allowRecordSourceStackFrame">When this parameter is true then this LMH will record source stack frames if any of the given targetLMH items do.  Otherwise it will not record source stack frames.</param>
                public QueueLogMessageHandler(string name, ILogMessageHandler[] targetLMHArray, int maxQueueSize = DefaultMesgQueueSize, bool allowRecordSourceStackFrame = true)
                    : base(name, LogGate.None, recordSourceStackFrame: false)
                {
                    targetLMHArray = targetLMHArray ?? emptyLMHArray;

                    LogGate logGate = LogGate.None;
                    bool recordSourceStackFrame = false;

                    foreach (ILogMessageHandler targetLMH in targetLMHArray)
                    {
                        logGate.MesgTypeMask |= targetLMH.LoggerConfig.LogGate.MesgTypeMask;
                        recordSourceStackFrame |= targetLMH.LoggerConfig.RecordSourceStackFrame;
                    }

                    loggerConfig.LogGate = logGate;
                    loggerConfig.RecordSourceStackFrame = recordSourceStackFrame && allowRecordSourceStackFrame;

                    this.targetLMHArray = targetLMHArray;

                    mesgDeliveryList = new List<LogMessage>(10);		// define its initial capacity

                    mesgQueue = new MessageQueue(maxQueueSize);
                    mesgQueue.SetEffectiveSourceInfo(logger.LoggerSourceInfo);
                    mesgQueue.EnableQueue();
                    mesgQueueMutex = mesgQueue.Mutex;

                    // create and start the thread
                    StartIfNeeded();
                }

                const int minQueueSizeToWakeupDeliveryThread = 100;

                // NOTE: neither the HandleLogMessage nor HandleLogMessages attempts to apply
                //	the logGate to determine which messages should be passed.  If they make it
                //	here, then they will be passed to the encapsulated lmh(s) which will perform
                //	its own message type gating.

                /// <summary>LogMessage Handling method API for direct call by clients which generate and distribute one message at a time.</summary>
                /// <param name="lm">
                /// Gives the message to handle (save, write, relay, ...).
                /// LMH Implementation must either support Reference Counted message semenatics if method will save any reference to a this message for use beyond the scope of this call or
                /// LMH must flag that it does not support Reference counted semantics in LoggerConfig by clearing the SupportReferenceCountedRelease
                /// </param>
                public override void HandleLogMessage(LogMessage lm)
                {
                    bool notifyThread = false;
                    lock (mesgQueueMutex)
                    {
                        if (mesgQueue.EnqueueMesg(lm) >= minQueueSizeToWakeupDeliveryThread)
                            notifyThread = true;

                        mesgQueueSeqNumRange.lastSeqNumIn = mesgQueue.LastEnqueuedSeqNum;
                    }

                    if (notifyThread)
                        threadWakeupEvent.Notify();
                }

                /// <summary>LogMessage Handling method API for use on outputs of queued delivery engines which agregate and distribute one or more messages at a time.</summary>
                /// <param name="lmArray">
                /// Gives an array of message to handle as a set (save, write, relay, ...).  This set may be given to muliple handlers and as such may contain messages that are not relevant to this handler.
                /// As such Handler may additionally filter out or skip any messages from the given set as approprate.
                /// LMH Implementation must flag that it does not support Reference counted semantics in LoggerConfig or must support ReferenceCounting on this message if references to it are saved beyond the scope of this call.
                /// </param>
                public override void HandleLogMessages(LogMessage[] lmArray)
                {
                    bool notifyThread = false;

                    lock (mesgQueueMutex)
                    {
                        if (mesgQueue.EnqueueMesgs(lmArray) >= minQueueSizeToWakeupDeliveryThread)
                            notifyThread = true;

                        mesgQueueSeqNumRange.lastSeqNumIn = mesgQueue.LastEnqueuedSeqNum;
                    }

                    if (notifyThread)
                        threadWakeupEvent.Notify();
                }

                /// <summary>Query method that may be used to tell if message delivery for a given message is still in progress on this handler.</summary>
                public override bool IsMessageDeliveryInProgress(int testMesgSeqNum)
                {
                    QueuedMesgSeqNumRange mesgQueueSeqNumRangeSnapshot = mesgQueueSeqNumRange;

                    return mesgQueueSeqNumRangeSnapshot.IsMesgSeqNumInRange(testMesgSeqNum);
                }

                /// <summary>Once called, this method only returns after the handler has made a reasonable effort to verify that all outsanding, pending messages, visible at the time of the call, have been full processed before the call returns.</summary>
                public override void Flush()
                {
                    int lastPutSeqNumSnapshot = mesgQueueSeqNumRange.lastSeqNumIn;

                    if (NullMessageSeqNum != lastPutSeqNumSnapshot)
                        System.Threading.Interlocked.CompareExchange(ref flushAfterSeqNum, lastPutSeqNumSnapshot, 0);
                    else
                        flushRequested = true;

                    threadWakeupEvent.Notify();

                    // need to wait for queue to drain
                    if (lastPutSeqNumSnapshot == NullMessageSeqNum)
                        return;

                    while (IsMessageDeliveryInProgress(lastPutSeqNumSnapshot))
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }

                /// <summary>
                /// Used to tell the handler that the LogMessageDistribution system is shuting down.  
                /// Handler is expected to close, release and/or Dispose of any unmanged resources before returning from this call.  
                /// Any attempt to pass messages after this point may be ignored by the handler.
                /// </summary>
                public override void Shutdown()
                {
                    // stop new messages from being inserted into the queue and ask 
                    //	background thread to drain queue and stop

                    mesgQueue.DisableQueue();

                    // wait for the thread to exit

                    if (mainThread != null)
                    {
                        mainThread.Join();
                        mainThread = null;
                    }

                    // NOTE: targetLMH is shutdown by the mainThread prior to its completion.
                }

                /// <summary>
                /// Re-enabled the queue and restart the mesg queue thread.
                /// </summary>
                public override void StartIfNeeded()
                {
                    mesgQueue.EnableQueue();

                    if (mainThread == null)
                    {
                        mainThread = new System.Threading.Thread(MainThreadFcn)
                        {
                            Name = this.Name,
                            IsBackground = true,        // allow the application to shutdown even if the client code forgets to 
                        };
                        mainThread.Start();
                    }
                }

                /// <summary>
                /// Implementes locally adjusted version of the base classes DisposableBase.Dispose(disposeType) method.
                /// Calls base.Dispose(disposeType).  If disposeType is CalledExplictly then it disposes of each of the contained targetLMH instances.
                /// </summary>
                /// <param name="disposeType">Indicates if this dispose call is made explicitly or by the finalizer.</param>
                protected override void Dispose(MosaicLib.Utils.DisposableBase.DisposeType disposeType)
                {
                    base.Dispose(disposeType);

                    if (disposeType == DisposeType.CalledExplicitly)
                    {
                        for (int idx = 0; idx < targetLMHArray.Length; idx++)
                            Utils.Fcns.DisposeOfObject(ref targetLMHArray[idx]);
                    }

                    targetLMHArray = null;
                }

                const int maxMessagesToDequePerIteration = 500;
                const double minDeliveryThreadSpinWaitPeriod = 0.050;		// 20 Hz

                /// <summary>
                /// This gives the method that is called by the internal service thread to pull messages from the back of the message queue and deliver them to the
                /// target LMH instances.
                /// </summary>
                protected void MainThreadFcn()
                {
                    if (targetLMHArray == null || !mesgQueue.IsEnabled || threadWakeupEvent == null)
                    {
                        mesgQueue.DisableQueue();

                        foreach (var lmh in targetLMHArray ?? emptyLMHArray)
                            lmh.Shutdown();

                        Utils.Asserts.TakeBreakpointAfterFault("QLMH thread Startup test failed");
                        return;
                    }

                    foreach (var lmh in targetLMHArray)
                        lmh.StartIfNeeded();

                    while (mesgQueue.IsEnabled)
                    {
                        threadWakeupEvent.Reset();

                        bool didAnything = false;

                        // process messages from a non-empty queue (or full)
                        if (mesgQueue.QueueCount != 0 || mesgQueue.IsQueueFull())
                        {
                            didAnything = true;
                            ServiceQueueDelivery(maxMessagesToDequePerIteration);
                        }

                        // detect seqNum based flush request and ask underlying lmh to flush after
                        // given seqNum is no longer in the mesgQueue
                        int flushReqSeqNumSnapshot = flushAfterSeqNum;
                        QueuedMesgSeqNumRange mesgQueueSeqNumRangeSnapshot = mesgQueueSeqNumRange;

                        if (NullMessageSeqNum != flushReqSeqNumSnapshot
                            && !mesgQueueSeqNumRangeSnapshot.IsMesgSeqNumInRange(flushReqSeqNumSnapshot))
                        {
                            flushRequested = true;
                            flushAfterSeqNum = NullMessageSeqNum;
                        }

                        if (flushRequested)	// when queue is empty then process flush request
                        {
                            didAnything = true;
                            flushRequested = false;

                            foreach (var lmh in targetLMHArray)
                                lmh.Flush();
                        }

                        if (!didAnything)
                            threadWakeupEvent.WaitSec(minDeliveryThreadSpinWaitPeriod);
                    }

                    // service the queue until it is empty
                    while (mesgQueue.QueueCount != 0 || mesgQueue.IsQueueFull())
                        ServiceQueueDelivery(maxMessagesToDequePerIteration);

                    uint totalDroppedMesgs = mesgQueue.TotalDroppedMesgCount;
                    if (totalDroppedMesgs != 0)
                        LogShutdownDroppedMessagesMesg(totalDroppedMesgs);

                    foreach (var lmh in targetLMHArray)
                        lmh.Shutdown();
                }

                /// <summary>
                /// Intennal method that pulls messages from the queue and delivers them to the set of target handlers.
                /// </summary>
                /// <param name="maxMessagesToDeque">Gives the maxmum number of messages to pull from the queue at a time.</param>
                protected void ServiceQueueDelivery(int maxMessagesToDeque)
                {
                    // get the next block of messages from the queue
                    mesgQueue.DequeueMesgSet(maxMessagesToDeque, ref mesgDeliveryList);

                    // return if we did not get any messages
                    if (mesgDeliveryList.Count == 0)
                        return;

                    // delivere the messages
                    LogMessage[] lmArray = mesgDeliveryList.ToArray();
                    foreach (var lmh in targetLMHArray)
                        lmh.HandleLogMessages(lmArray);

                    int lastDeliveredSeqNum = mesgQueueSeqNumRange.lastSeqNumOut;

                    for (int idx = 0; idx < mesgDeliveryList.Count; idx++)
                    {
                        LogMessage lm = mesgDeliveryList[idx];
                        mesgDeliveryList[idx] = null;

                        if (lm == null)
                            continue;

                        lastDeliveredSeqNum = lm.SeqNum;
                        lm = null;
                    }

                    mesgDeliveryList.Clear();

                    mesgQueueSeqNumRange.lastSeqNumOut = lastDeliveredSeqNum;

                    // tell any interested Notifyable objects that the queue has advanced
                    NoteMessagesHaveBeenDelivered();
                }

                /// <summary>
                /// Makes a final effort to record the number of dropped messages.
                /// </summary>
                /// <param name="totalLostMesgCount">Gives the number of messages that the queue dropped.</param>
                protected void LogShutdownDroppedMessagesMesg(uint totalLostMesgCount)
                {
                    logger.Error.Emit("Total mesgs dropped:{0}", totalLostMesgCount);

                    ServiceQueueDelivery(10);		// attempt to allow some more mesgs to be logged (so the above message makes it out as well)
                }

                #region private variables

                static readonly ILogMessageHandler[] emptyLMHArray = new ILogMessageHandler[0];

                ILogMessageHandler[] targetLMHArray = null;
                System.Threading.Thread mainThread = null;

                Utils.WaitEventNotifier threadWakeupEvent = new MosaicLib.Utils.WaitEventNotifier(MosaicLib.Utils.WaitEventNotifier.Behavior.WakeAllSticky);

                int flushAfterSeqNum = 0;
                volatile bool flushRequested = false;

                MessageQueue mesgQueue = null;
                object mesgQueueMutex = null;

                QueuedMesgSeqNumRange mesgQueueSeqNumRange = new QueuedMesgSeqNumRange();

                List<LogMessage> mesgDeliveryList = null;		// temporary place to put messages that are taken from the queue but that have not been delivered yet

                #endregion
            }
		}
	}
}

//-------------------------------------------------------------------
