//-------------------------------------------------------------------
/*! @file MessageQueue.cs
 *  @brief This file defines and implements the MessageQueue class.
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

namespace MosaicLib
{
	public static partial class Logging
	{
		//-------------------------------------------------------------------

        /// <summary>
        /// Structure used to track the range of message sequence numbers in a message queue and to allow a client to take a snapshot of the pair of sequence numbers
        /// and then ask questions about whether a given message seq number is currently residing in the queue or not.
        /// </summary>
		private struct QueuedMesgSeqNumRange
		{
            /// <summary>sequence number of the last message to have been put into the queue</summary>
			public volatile int lastSeqNumIn;
            /// <summary>sequence number of the last message to have been taken from the queue (queue is likely empty if they are both the same)</summary>
            public volatile int lastSeqNumOut;

            /// <summary>
            /// Copy constructor.  
            /// <para/>grab the out sequence number before the in so that we are most inclusive when making a copy of another QueuedMesgSeqNumRange value.
            /// order matters here since these are volatile (values may be written in the background by other threads while reading..)
            /// </summary>
			public QueuedMesgSeqNumRange(ref QueuedMesgSeqNumRange rhs)
			{
				lastSeqNumOut = rhs.lastSeqNumOut;
				lastSeqNumIn = rhs.lastSeqNumIn;
			}

            /// <summary>
            /// Returns true if the given testSeqNum is found between lastSeqNumIn and lastSeqNumOut
            /// </summary>
			public bool IsMesgSeqNumInRange(int testSeqNum)
			{
				int d1 = unchecked (lastSeqNumIn - testSeqNum);	// >= 0 if message has been put in the queue.
				int d2 = unchecked (testSeqNum - lastSeqNumOut);	// > 0 if message seq has not been pulled from queue

				bool inRange = (d1 >= 0 && d2 > 0);
				return inRange;
			}
		};

		//-------------------------------------------------------------------

		private class MessageQueue
		{
			#region Private Logger

            /// <summary>
            /// Simple LoggerBase derived class that is used by the MessageQueue to generate and insert messages into itself.
            /// </summary>
			private class MQLogger : LoggerBase
			{
				protected MessageQueue mq = null;
				public MQLogger(string name, MessageQueue mq) : base(name, string.Empty, LogGate.All, traceLoggerCtor: false, allowUseOfModularConfig: false) { this.mq = mq; }
				protected override string ClassName { get { return "MessageQueueLogger"; } }

				public override void EmitLogMessage(ref LogMessage lm)
				{
					mq.InnerEnqueueMesg(lm);
                    lm = null;
				}
			}

			#endregion

			#region Private Methods and instance variables

			MQLogger					logger = null;

			LoggerSourceInfo			effectiveSourceInfo = null;

			volatile bool				queueIsEnabled = false;		//!< Also used to tell the back end when to exit

			readonly object			    queueMutex = new object();

			Utils.INotifyable			mesgEnqueuedNotify = new Utils.NullNotifier();

			volatile bool				mesgQueueIsFull = false;		// this is only declared volatile to support testing without owning queue lock
			Time.QpcTimeStamp			mesgQueueFullStartTime = Time.QpcTimeStamp.Zero;
			uint						mesgQueueDroppedMesgCount = 0;
			uint						mesgQueueTotalDroppedMesgCount = 0;


			Queue<LogMessage>			mesgQueue = null;
			int							mesgQueueSize = 0;
			volatile int				mesgQueueCount = 0;	// this is only declared volatile to support testing for empty without owning queue lock

			bool						useLocalMesgSeqNumGenerator = true;
			Utils.SequenceNumberInt		localMesgSeqNumGenerator = new MosaicLib.Utils.SequenceNumberInt(0, true);

			QueuedMesgSeqNumRange		queuedMesgSeqNumRange = new QueuedMesgSeqNumRange();

			#endregion

			#region Ctor 

			public MessageQueue(int maxQueueCount) : this(maxQueueCount, false) { }
			public MessageQueue(int maxQueueCount, bool useLocalMesgSeqNumGenerator)
			{
				maxQueueCount = Math.Max(maxQueueCount, 2);

				this.useLocalMesgSeqNumGenerator = useLocalMesgSeqNumGenerator;
				mesgQueue = new Queue<LogMessage>(maxQueueCount);
				mesgQueueSize = maxQueueCount;
				mesgQueueCount = 0;

				logger = new MQLogger("MessageQueue", this);
			}

			public void Shutdown()
			{
				DisableQueue();
			}

			#endregion

			#region Public Methods and Properties

			public void SetEffectiveSourceInfo(LoggerSourceInfo logSource)
			{
				lock (queueMutex)
				{
					effectiveSourceInfo = logSource;
				}
			}

			public void SetNotifyOnEnqueue(Utils.INotifyable notifyOnEnqueue)
			{
				lock (queueMutex)
				{
					mesgEnqueuedNotify = notifyOnEnqueue;
				}
			}

			public void EnableQueue()
			{
				lock (queueMutex)
				{
					if (effectiveSourceInfo != null)
						queueIsEnabled = true;
					else
						Utils.Asserts.NoteFaultOccurance("EnableQueue failed: EffectiveSourceID is not valid", Utils.AssertType.ThrowException);
				}
			}
			public void DisableQueue() { queueIsEnabled = false; }
			public bool IsEnabled { get { return queueIsEnabled; }  }

            /// <summary>
            /// returns the number of messages in the queue
            /// </summary>
			public int EnqueueMesg(LogMessage lm)
			{
				if (lm == null)
					return QueueCount;

				lock (queueMutex)
				{
                    if (IsEnabled)
                    {
                        InnerEnqueueMesg(lm);

                        if (mesgEnqueuedNotify != null)
                            mesgEnqueuedNotify.Notify();
                    }

					return mesgQueueCount;
				}
			}

            /// <summary>
            /// returns the number of messages in the queue
            /// </summary>
			public int EnqueueMesgs(LogMessage [] lmArray)
			{
				lock (queueMutex)
				{
                    if (IsEnabled)
                    {
                        for (int idx = 0; idx < lmArray.Length; idx++)
                        {
                            if (IsEnabled)
                                InnerEnqueueMesg(lmArray[idx]);
                        }

                        if (mesgEnqueuedNotify != null)
                            mesgEnqueuedNotify.Notify();
                    }
                
                    return mesgQueueCount;
                }
			}

			public int DequeueMesgSet(int maxMessagesToDeque, ref System.Collections.Generic.List<LogMessage> mesgTargetList)
			{
				if (mesgTargetList.Capacity < maxMessagesToDeque)
					mesgTargetList.Capacity = maxMessagesToDeque;

				mesgTargetList.Clear();

				lock (queueMutex)
				{
					while (mesgTargetList.Count < maxMessagesToDeque && mesgQueueCount > 0)
					{
						LogMessage lm = mesgQueue.Dequeue();
						mesgQueueCount = mesgQueue.Count;

						int mesgSeqNum = lm.SeqNum;
						mesgTargetList.Add(lm);

						if (NullMessageSeqNum != mesgSeqNum)
							queuedMesgSeqNumRange.lastSeqNumOut = mesgSeqNum;
					}

					// if the message queue was full and there is at least space for 2 new messages
					//	then generate a Queue full message and enqueue it for logging in the correct
					//	order.

					if (mesgQueueIsFull && (mesgQueueSize - mesgQueueCount) >= 2)
					{
						double elapsedTime = mesgQueueFullStartTime.Age.TotalSeconds;

						logger.Error.Emit("Queue full: {0} mesgs dropped in {1} seconds.  total mesgs dropped:{2}",
												 mesgQueueDroppedMesgCount, elapsedTime.ToString("f3"), mesgQueueTotalDroppedMesgCount);

						mesgQueueDroppedMesgCount = 0;
						mesgQueueIsFull = false;			// since there is space for at least one message now...
					}
				}

				return (mesgTargetList.Count);
			}

			public int QueueCount 
            { 
                get 
                { 
                    lock (queueMutex) 
                    { 
                        return mesgQueueCount; 
                    } 
                } 
            }

			public bool IsQueueFull() { return mesgQueueIsFull; }		// access to volatile is just as accurate outside the lock as inside of it

			public uint TotalDroppedMesgCount { get { lock (queueMutex) { return mesgQueueTotalDroppedMesgCount; } } }

			public int LastEnqueuedSeqNum { get { return queuedMesgSeqNumRange.lastSeqNumIn; } }
			public int LastDequeuedSeqNum { get { return queuedMesgSeqNumRange.lastSeqNumOut; } }
			public bool IsMessageStillInQueue(int testSeqNum)
			{
				lock (queueMutex)
				{
					QueuedMesgSeqNumRange queuedMesgSeqNumRangeSnapshot = new QueuedMesgSeqNumRange(ref queuedMesgSeqNumRange);

					return queuedMesgSeqNumRangeSnapshot.IsMesgSeqNumInRange(testSeqNum);
				}
			}

			public object Mutex { get { return queueMutex; } }		// it is safe to allow our caller to own our mutex since CLR support recursive locks and we use it as a leaf lock

			#endregion

			#region Private Methods and instance variables

            /// <summary>Enqueue a new reference to the given message into the internal message queue.</summary>
			void InnerEnqueueMesg(LogMessage lm)
			{
                if (lm != null)
                {
                    if (!mesgQueueIsFull && mesgQueueCount < mesgQueueSize)
                    {
                        if (useLocalMesgSeqNumGenerator)
                            lm.SeqNum = localMesgSeqNumGenerator.Increment();

                        // enqueue the new message and update the local copy of the count.
                        mesgQueue.Enqueue(lm);
                        mesgQueueCount = mesgQueue.Count;

                        if (NullMessageSeqNum != lm.SeqNum)
                            queuedMesgSeqNumRange.lastSeqNumIn = lm.SeqNum;

                        lm = null;
                    }
                    else
                    {
                        if (!mesgQueueIsFull)
                        {
                            mesgQueueIsFull = true;
                            mesgQueueFullStartTime.SetToNow();
                            mesgQueueDroppedMesgCount = 0;
                        }

                        mesgQueueDroppedMesgCount++;
                        mesgQueueTotalDroppedMesgCount++;
                    }
                }
			}

			#endregion
		};

		//-------------------------------------------------------------------
	}
}

//-------------------------------------------------------------------
