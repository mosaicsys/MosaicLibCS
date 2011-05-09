//-------------------------------------------------------------------
/*! @file QueueLogMessageHandler.cs
 * @brief This file provides an implementation of the QueueLogMessageHandler LMH requeuing class.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2007 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

namespace MosaicLib
{
	using System;
	using System.Collections.Generic;

	public static partial class Logging
	{
		public static partial class Handlers
		{
			public class QueueLogMessageHandler : CommonLogMessageHandlerBase
			{
				public QueueLogMessageHandler(ILogMessageHandler targetLMH, int maxQueueSize) 
					: base(targetLMH.Name, targetLMH.LoggerConfig.LogGate, targetLMH.LoggerConfig.RecordSourceStackFrame, targetLMH.LoggerConfig.SupportsReferenceCountedRelease)
				{
                    this.targetLMH = targetLMH;
                    dist = Logging.GetLogMessageDistribution();

                    mesgDeliveryList = new List<LogMessage>(10);		// define its initial capacity

					mesgQueue = new MessageQueue(maxQueueSize);
					mesgQueue.SetEffectiveSourceInfo(logger.LoggerSourceInfo);
					mesgQueue.EnableQueue();
					mesgQueueMutex = mesgQueue.Mutex;

					// create and start the thread
					mainThread = new System.Threading.Thread(MainThreadFcn);
					mainThread.Start();
				}
			
				const int minQueueSizeToWakeupDeliveryThread = 100;

				// NOTE: neither the HandleLogMessage nor HandleLogMessages attempts to apply
				//	the logGate to determine which messages should be passed.  If they make it
				//	here, then they will be passed to the encapsulated lmh which will perform
				//	its own message type gating.

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

				public override void  HandleLogMessages(LogMessage[] lmArray)
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

				public override bool IsMessageDeliveryInProgress(int testMesgSeqNum)
				{
					QueuedMesgSeqNumRange mesgQueueSeqNumRangeSnapshot = mesgQueueSeqNumRange;

					return mesgQueueSeqNumRangeSnapshot.IsMesgSeqNumInRange(testMesgSeqNum);
				}

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

				protected override void Dispose(MosaicLib.Utils.DisposableBase.DisposeType disposeType)
				{
					base.Dispose(disposeType);

					targetLMH = null;
				}

				const int maxMessagesToDequePerIteration = 500;
				const double minDeliveryThreadSpinWaitPeriod = 0.050;		// 20 Hz

				protected void MainThreadFcn()
				{
					if (targetLMH == null || !mesgQueue.IsEnabled || threadWakeupEvent == null)
					{
						mesgQueue.DisableQueue();

                        if (targetLMH != null)
                            targetLMH.Shutdown();

						Utils.Assert.BreakpointFault("QLMH thread Startup test failed");
						return;
					}

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
							targetLMH.Flush();
						}

						if (!didAnything)
							threadWakeupEvent.WaitSec(minDeliveryThreadSpinWaitPeriod);
					}

					// service the queue until it is empty
					while (mesgQueue.QueueCount != 0 || mesgQueue.IsQueueFull())
						ServiceQueueDelivery(maxMessagesToDequePerIteration);

					uint totalDroppedMesgs = mesgQueue.TotalDroppedMesgCount;
					if (totalDroppedMesgs != 0)
						LogShutdownMesg(totalDroppedMesgs);

					targetLMH.Shutdown();
				}

				protected void ServiceQueueDelivery(int maxMessagesToDeque)
				{
					// get the next block of messages from the queue
					mesgQueue.DequeueMesgSet(maxMessagesToDeque, ref mesgDeliveryList);

					// return if we did not get any messages
					if (mesgDeliveryList.Count == 0)
						return;

					// delivere the messages
                    if (!targetLMH.LoggerConfig.SupportsReferenceCountedRelease)
                        dist.ReallocateMessagesForNonRefCountedHandler(mesgDeliveryList);
                        
					targetLMH.HandleLogMessages(mesgDeliveryList.ToArray());

					int lastDeliveredSeqNum = mesgQueueSeqNumRange.lastSeqNumOut;

					for (int idx = 0; idx < mesgDeliveryList.Count; idx++)
					{
						LogMessage lm = mesgDeliveryList[idx];
						mesgDeliveryList [idx] = null;

						if (lm == null)
							continue;

						lastDeliveredSeqNum = lm.SeqNum;
						lm.RemoveReference(ref lm);
					}

					mesgDeliveryList.Clear();

					mesgQueueSeqNumRange.lastSeqNumOut = lastDeliveredSeqNum;

					// tell any interested Notifyable objects that the queue has advanced
					NoteMessagesHaveBeenDelivered();
				}

				protected void LogShutdownMesg(uint totalLostMesgCount)
				{
					logger.Error.Emit("Total mesgs dropped:{0}", totalLostMesgCount);

					ServiceQueueDelivery(10);		// attempt to allow some more mesgs to be logged (so the above message makes it out as well
				}

				#region private variables

				ILogMessageHandler			targetLMH = null;
                ILogMessageDistribution     dist = null;
                System.Threading.Thread     mainThread = null;

				Utils.WaitEventNotifier		threadWakeupEvent = new MosaicLib.Utils.WaitEventNotifier(MosaicLib.Utils.WaitEventNotifier.Behavior.WakeAllSticky);

				int							flushAfterSeqNum = 0;
				volatile bool				flushRequested = false;

				MessageQueue				mesgQueue = null;
				object						mesgQueueMutex = null;

				QueuedMesgSeqNumRange		mesgQueueSeqNumRange = new QueuedMesgSeqNumRange();

				List<LogMessage>			mesgDeliveryList = null;		// temporary place to put messages that are taken from the queue but that have not been delivered yet

				#endregion
			}
		}
	}
}

//-------------------------------------------------------------------
