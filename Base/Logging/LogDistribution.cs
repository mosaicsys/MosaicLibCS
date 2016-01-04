//-------------------------------------------------------------------
/*! @file LogDistribution.cs
 * @brief This file provides the internal class definition that is used to implement the LogDistribution singleton class that handles allocation and distribution of LogMessages for the MosaicLib Logging system.
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
using System;
using System.Collections.Generic;
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular.Config;

namespace MosaicLib
{
	public static partial class Logging
	{
		//-------------------------------------------------------------------
		#region early definitions

        /// <summary>the groupID to which all logger source names belong if they are not explicitly remapped to some other group</summary>
		const int DistGroupID_Default = 0;
        /// <summary>the groupID to indicate that none was provided.</summary>
        const int DistGroupID_Invalid = -1;

        /// <summary>Enum defines the different means that a string can be used to determine if a Logger Name is to be included in a specific matching group.</summary>
        public enum LoggerNameMatchType
		{
            /// <summary>string does not match any logger names (default for all groups except default group)</summary>
            None = 0,
            /// <summary>string must equal the leading characters of each selected logger name</summary>
            MatchPrefix,
            /// <summary>string must equal the trailing characters of each selected logger name</summary>
            MatchSuffix,
            /// <summary>string must be present in each selected logger name</summary>
            MatchContains,
            /// <summary>string is a regular expression that matches each of the names in the desired set of logger names</summary>
            Regex,
		}

		#endregion

		//-------------------------------------------------------------------
		#region ILogMessageDistribution interface

		// definitions for and about interactions with the log distribution system

		/// <summary> This class defines the public interface to the LogMessageDistributionImpl system. </summary>
		/// <remarks>
		/// This interface includes registration of log sources (via GetLoggerSourceID), 
		/// methods used to change the distrubution rules and/or gate level for a given loggerID
		/// and methods that are used to allocate new messages and emit them into the distribution system.
		/// </remarks>

		public interface ILogMessageDistribution
		{
            /// <summary>used to create/lookup the LoggerSourceIDPtr for a given logger name</summary>
			LoggerSourceInfo GetLoggerSourceInfo(string name);

            /// <summary>used to create/lookup the LoggerSourceIDPtr for a given logger name, allowUseOfModularConfig can be given as false to suppress use of modular config key Logging.Loggers.[name].LogGate as source for additional LogGate value.</summary>
            LoggerSourceInfo GetLoggerSourceInfo(string name, bool allowUseOfModularConfig);

            /// <summary>used to update a logger's distribution group name</summary>
            void SetLoggerDistributionGroupName(int loggerID, string groupName);

            /// <summary>this method is fully renterant.  Once the allocated message has been given to the distribution system, the caller must make no further use of or reference to it.</summary>
            LogMessage GetLogMessage();

            /// <summary>used to distribute a message from the loggerID listed as the message's source.  This method consumes the given message.  The caller's handle will be nulled by this method.</summary>
            void DistributeMessage(ref LogMessage lm);

            /// <summary>used to allow a logger to request that it block until its last emitted message has been fully delivered or the time limit is reached (renterant method on multiple callers)</summary>
            bool WaitForDistributionComplete(int loggerID, TimeSpan timeLimit);

            /// <summary>request distribution to start support for queued logging</summary>
            void StartQueuedMessageDelivery();

            /// <summary>request distribution to stop support for queued logging</summary>
            void StopQueuedMessageDelivery();

            /// <summary>used to allow a queued logger to request that it block until its last emitted message has been fully delivered or the time limit is reached (renterant method on multiple callers)</summary>
            bool WaitForQueuedMessageDistributionComplete(int loggerID, TimeSpan timeLimit);

            /// <summary>used to support QueuedLogger loggers.  This method consumes the given message.  The caller's handle will be nulled by this method.</summary>
            void EnqueueMessageForDistribution(ref LogMessage lm);

            /// <summary>
            /// Allows caller (typically distribution itself, queued handlers or custom message loggers) to scan and replace pooled messages with non-pooled ones for hanlders that do not support Reference Counted Semantics
            /// </summary>
            void ReallocateMessageForNonRefCountedHandler(ref LogMessage lm);

            /// <summary>
            /// Allows caller (typically distribution itself, queued handlers or custom message loggers) to scan and replace pooled messages with non-pooled ones for hanlders that do not support Reference Counted Semantics
            /// </summary>
            void ReallocateMessagesForNonRefCountedHandler(LogMessage[] lmArray);

            /// <summary>
            /// Allows caller (typically distribution itself, queued handlers or custom message loggers) to scan and replace pooled messages with non-pooled ones for hanlders that do not support Reference Counted Semantics
            /// </summary>
            void ReallocateMessagesForNonRefCountedHandler(List<LogMessage> lmList);
        };

	
		#endregion

		//-------------------------------------------------------------------
		#region LogMessageDistributionImpl class

		private class LogMessageDistributionImpl : Utils.DisposableBase, ILogMessageDistribution
		{
            //-------------------------------------------------------------------
            #region instance variables and related types: PerLoggerIDInfo, DistHandlerInfo, PerDistGroupIDInfo

            const int PoolCapacity = 1000;				// the maximum number of log messages to keep on the stack at any given time
			const int PreallocatedPoolItems = 100;		// the default pool size to start with
			const int MesgQueueSize = 1000;				// big enough for serious use.

			LoggerSourceInfo distSourceID = null;				//!< sourceID for messages that originate in this distribution system.

            /// <summary>Mutex object for access to distribution system's internals.</summary>
			Object distMutex = new Object();
			volatile bool shutdown = false;					//!< volatile to support testing outside of lock ownership.  changes are still done inside lock

			Utils.SequenceNumberInt mesgDistributionSeqGen = new MosaicLib.Utils.SequenceNumberInt(0, true);		// this is only used within the ownership of the mutex so it does not need to be interlocked

			Utils.WaitEventNotifier	mesgDeliveryOccurredNotification = new MosaicLib.Utils.WaitEventNotifier(MosaicLib.Utils.WaitEventNotifier.Behavior.WakeAllSticky);

            class PerLoggerIDInfo
            {
                private int lid = 0;
                public int distGroupID = DistGroupID_Default;
                public IConfigKeyAccess loggersReduceLogGateICKA = null;
                public IConfigKeyAccess loggersIncreaseLogGateICKA = null;

                public LogGate loggersReduceLogGateFromModularConfig = LogGate.All;
                public LogGate loggersIncreaseLogGateFromModularConfig = LogGate.None;

                private LoggerConfig distGroupConfig = LoggerConfig.None;
                private bool disabled;

                public SequencedLoggerConfigSource seqLoggerConfigSource = null;
                public LoggerSourceInfo sourceID = null;							// contains Logger Name and ID. - only filled in after construction

                public int lastDistributedMesgSeqNum = 0;

                public int LoggerID { get { return lid; } }

                public string SelectedDistGroupName		// non-empty if the logger specifically identified a target group.
                {
                    get { return distGroupConfig.GroupName; }
                }

                public PerLoggerIDInfo UpdateLogGateFromModularConfig()
                {
                    bool updateLoggerConfigNeeded = false;

                    if (loggersReduceLogGateICKA != null && (loggersReduceLogGateICKA.UpdateValue() || loggersReduceLogGateFromModularConfig == LogGate.All))
                    {
                        LogGate configGate = LogGate.All;
                        configGate.TryParse(loggersReduceLogGateICKA.GetValue<string>(String.Empty));

                        if (loggersReduceLogGateFromModularConfig != configGate)
                        {
                            loggersReduceLogGateFromModularConfig = configGate;
                            updateLoggerConfigNeeded = true;
                        }
                    }

                    if (loggersIncreaseLogGateICKA != null && (loggersIncreaseLogGateICKA.UpdateValue() || loggersIncreaseLogGateFromModularConfig == LogGate.None))
                    {
                        LogGate configGate = LogGate.None;
                        configGate.TryParse(loggersIncreaseLogGateICKA.GetValue<string>(String.Empty));

                        if (loggersIncreaseLogGateFromModularConfig != configGate)
                        {
                            loggersIncreaseLogGateFromModularConfig = configGate;
                            updateLoggerConfigNeeded = true;
                        }
                    }

                    if (updateLoggerConfigNeeded)
                        UpdateAndPublishLoggerConfig();

                    return this;
                }

                public LoggerConfig DistGroupConfig
                {
                    get { return distGroupConfig; }
                    set
                    {
                        if (!distGroupConfig.Equals(value))
                        {
                            distGroupConfig = value;
                            UpdateAndPublishLoggerConfig();
                        }
                    }
                }

                public bool Disabled
                {
                    get { return disabled; }
                    set
                    {
                        if (disabled != value)
                        {
                            disabled = value;
                            UpdateAndPublishLoggerConfig();
                        }
                    }
                }

                private void UpdateAndPublishLoggerConfig()
                {
                    seqLoggerConfigSource.LoggerConfig = GenerateCurrentLoggerConfig();
                }

                public PerLoggerIDInfo(int loggerID)
                {
                    lid = loggerID;
                    seqLoggerConfigSource = new SequencedLoggerConfigSource(GenerateCurrentLoggerConfig());
                }
                private LoggerConfig GenerateCurrentLoggerConfig()
                {
                    LoggerConfig loggerConfig = new LoggerConfig((!disabled)
                                                                    ? (distGroupConfig & loggersReduceLogGateFromModularConfig)
                                                                    : (new LoggerConfig(distGroupConfig) { LogGate = LogGate.None })
                                                                    ) { LogGateIncrease = loggersIncreaseLogGateFromModularConfig };
                    return loggerConfig;
                }
            }

			List<PerLoggerIDInfo> perLoggerIDInfoList = new List<PerLoggerIDInfo>();	// index by loggerID
            PerLoggerIDInfo[] perLoggerIDInfoArray = new PerLoggerIDInfo[0];      // index by loggerID - always stays synchronized with perLoggerIDInfoList contents
            Dictionary<string, int> loggerNameToIDMap = new Dictionary<string, int>();

			class DistHandlerInfo
			{
				protected ILogMessageHandler lmh = null;
				protected LoggerConfig lmhLoggerConfig = LoggerConfig.None;

				public bool Valid { get { return (LMH != null); } }
				public ILogMessageHandler LMH { get { return lmh; } set { lmh = value; if (lmh != null) lmhLoggerConfig = lmh.LoggerConfig; } }
				public LoggerConfig LoggerConfig { get { return lmhLoggerConfig; } }

				public DistHandlerInfo() { }
				public DistHandlerInfo(ILogMessageHandler lmh) { LMH = lmh; }
			}

			class PerDistGroupIDInfo
			{
                /// <summary>contains the name of the group.</summary>
				private string groupName = string.Empty;

                /// <summary>contains the group's ID (also used as an index into the distGroupIDInfoList)</summary>
                private int id = DistGroupID_Invalid;

                public string DistGroupName { get { return groupName; } }
                /// <summary>readonly property gives caller access to the stored Distribution Group ID for this group.</summary>
                public int DistGroupID { get { return id; } }

                private bool disabled = false;
                public bool Disabled
                {
                    get { return disabled; }
                    set
                    {
                        if (disabled != value)
                        {
                            disabled = value;
                            loggerConfigWrittenNotification.Notify();
                        }
                    }
                }

                /// <summary>stores the current LoggerConfig setting for this group.</summary>
                private LoggerConfig groupLoggerConfigSetting = LoggerConfig.AllNoFL;
                /// <summary>public property access to stored LoggerConfig.  Signals that a logger config update is needed whenever the property is set.</summary>
                public LoggerConfig GroupLoggerConfigSetting 
                { 
                    get { return groupLoggerConfigSetting; } 
                    set 
                    {
                        groupLoggerConfigSetting = new LoggerConfig(value) { GroupName = DistGroupName }; 
                        loggerConfigWrittenNotification.Notify(); 
                    } 
                }

                /// <summary>This is the list of info objects for the log message handlers that will recieve messages that are distributed to/through this group.</summary>
                public List<DistHandlerInfo> distHandlerInfoList = new List<DistHandlerInfo>();
                /// <summary>Used to add a new log message handler to this group's distribution list.</summary>
                public void Add(DistHandlerInfo dhInfo) { distHandlerInfoList.Add(dhInfo); loggerConfigWrittenNotification.Notify(); }

                /// <summary>internal list of linked groups to which messages accepted by this group will also be delivered.  This group is always the first item in this list.</summary>
                private List<PerDistGroupIDInfo> linkedDistGroupList = new List<PerDistGroupIDInfo>();
                /// <summary>cached ToArray version of linkedDistGroupList for improved foreach behavior.</summary>
                private PerDistGroupIDInfo[] linkedDistGroupArray = new PerDistGroupIDInfo[0];
                /// <summary>public get-only propery version of linkedDistGroupArray cached list value.</summary>
                public PerDistGroupIDInfo[] LinkedDistGroupArray { get { return linkedDistGroupArray; } }

                /// <summary>Adds the given target group ID to this groups linked dist list if needed.  Returns true if the given ID was not already in the list and was added.  Returns false if the given ID was already in the list.</summary>
                public bool AddLinkTo(PerDistGroupIDInfo linkToDistGroupIDInfo)
                {
                    if (!linkedDistGroupList.Contains(linkToDistGroupIDInfo))
                    {
                        linkedDistGroupList.Add(linkToDistGroupIDInfo);
                        linkedDistGroupArray = linkedDistGroupList.ToArray();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                /// <summary>sequence number used to record when logging config related changes have been made that might require the cached gates to be re-evaluated.</summary>
                private Utils.InterlockedSequenceNumberInt loggerConfigWrittenNotification = new MosaicLib.Utils.InterlockedSequenceNumberInt();	// used as INotifyable
                /// <summary>get only public property version of the loggerConfigWrittenNotification object as a simple INotifyable.</summary>
                public Utils.INotifyable LoggerConfigWrittenNotification { get { return loggerConfigWrittenNotification; } }
                /// <summary>SequenceNumberObserver that looks at the loggerConfigWrittenNotification.</summary>
                public Utils.SequenceNumberObserver<int> loggerConfigWrittenObserver;

                /// <summary>the logical "and" of the logGateSetting and distHandlerLogGate</summary>
				private LoggerConfig activeLoggerConfig = LoggerConfig.None;

                /// <summary>MatchType for determining which loggers should belong to this group by default.</summary>
                public LoggerNameMatchType loggerNameMatchType = LoggerNameMatchType.None;
                /// <summary>String used according to loggerNameMatchType for determining which loggers belong to this group</summary>
                public string loggerNameMatchStr = string.Empty;
                /// <summary>Parsed RegularExpression for loggerNameMatchStr when loggerNameMatchType selects use of a regex matching.</summary>
                public System.Text.RegularExpressions.Regex loggerNameMatchRegex = null;

                /// <summary>Constructor - caller provides group ID and name</summary>
                public PerDistGroupIDInfo(int dgid, string groupName) 
				{ 
					id = dgid;
                    this.groupName = groupName;
                    AddLinkTo(this);        // so the list always starts with us

                    activeLoggerConfig.GroupName = groupName;
                    groupLoggerConfigSetting.GroupName = groupName;
                    loggerConfigWrittenObserver = new MosaicLib.Utils.SequenceNumberObserver<int>(loggerConfigWrittenNotification);
				}

                /// <summary>Returns true if any log message handler (logger) has had its configuration changed since we last scanned them.  Use UpdateActiveLoggerConfig to incorporate these changes.</summary>
                public bool IsActiveLoggerConfigUpdateNeeded { get { return loggerConfigWrittenObserver.IsUpdateNeeded; } }
                /// <summary>Combines the configs for the handlers in the distribution set with the group's configuration to generate the activeLoggerConfig, the cached summary of the config that is used to gate allocation and delivery of messages.</summary>
                public bool UpdateActiveLoggerConfig()
				{
					LoggerConfig distHandlerLoggerConfigOr = LoggerConfig.None;			//!< the logical "or" of the gates for our distribution handlers at the time they were added.
                    LoggerConfig distHandlerLoggerConfigAnd = LoggerConfig.None;	    //!< the logical "and" of the gates for our distribution handlers at the time they were added.

                    distHandlerLoggerConfigOr.GroupName = DistGroupName;

                    foreach (DistHandlerInfo dhInfo in distHandlerInfoList)
                    {
                        distHandlerLoggerConfigOr |= dhInfo.LoggerConfig;
                        distHandlerLoggerConfigAnd &= dhInfo.LoggerConfig;
                    }

					LoggerConfig updatedLoggerConfig = distHandlerLoggerConfigOr & groupLoggerConfigSetting;
                    if (Disabled)
                        updatedLoggerConfig.LogGate = LogGate.None;

                    if (!distHandlerLoggerConfigAnd.SupportsReferenceCountedRelease)
                        updatedLoggerConfig.SupportsReferenceCountedRelease = false;

					if (activeLoggerConfig.Equals(updatedLoggerConfig))
						return false;

					activeLoggerConfig = updatedLoggerConfig;
					return true;
				}

                /// <summary>Returns a LoggerConfig that is the logical "and" of the logGateSetting and distHandlerLogGate.  Automatically triggers updates when needed.</summary>
                public LoggerConfig ActiveLoggerConfig
				{
					get
					{
						if (IsActiveLoggerConfigUpdateNeeded)
							UpdateActiveLoggerConfig();

						return activeLoggerConfig;
					}
				}
			}

			List<PerDistGroupIDInfo> distGroupIDInfoList = new List<PerDistGroupIDInfo>();			// index by group id
            PerDistGroupIDInfo[] distGroupIDInfoArray = new PerDistGroupIDInfo[0];
            Dictionary<string, int> distGroupNameToIDMap = new Dictionary<string, int>();

			// information used to implement queued logging.

			volatile MessageQueue mesgQueue = null;
			SequencedLoggerConfigSource mesgQueueLoggerGateSource = new SequencedLoggerConfigSource(LoggerConfig.AllWithFL);
			System.Threading.Thread mesgQueueDistThread = null;
			Utils.WaitEventNotifier	mesgQueueDistThreadWakeupNotification = new MosaicLib.Utils.WaitEventNotifier(MosaicLib.Utils.WaitEventNotifier.Behavior.WakeAllSticky);

            /// <summary>
            /// A mesgPool is created by the distribution engine during initial construction and remains available.  It cannot be set to null.
            /// </summary>
			readonly Utils.Pooling.ObjectPool<LogMessage> mesgPool = new MosaicLib.Utils.Pooling.ObjectPool<LogMessage>(PreallocatedPoolItems, PoolCapacity);

			#endregion

			//-------------------------------------------------------------------
			#region ctor and dispose

            /// <summary>
            /// Constructor
            /// </summary>
			public LogMessageDistributionImpl()
			{
				distSourceID = new LoggerSourceInfo(LoggerID_InternalLogger, "LogMessageDistributionImpl");

				// define the default distribution group.  Its name is the name that the distribution uses if
				//	it is trying to emit a message.

				const int defaultDistGroupID = DistGroupID_Default;
                PerDistGroupIDInfo dgInfo = null;

                if (distGroupIDInfoList.Count == 0)     // this is only done once.
                {
                    dgInfo = new PerDistGroupIDInfo(distGroupIDInfoList.Count, DefaultDistributionGroupName);

                    distGroupIDInfoList.Add(dgInfo);
                    distGroupIDInfoArray = distGroupIDInfoList.ToArray();

                    distGroupNameToIDMap.Add(dgInfo.DistGroupName, dgInfo.DistGroupID);
                }

                if (!InnerIsDistGroupIDValid(defaultDistGroupID))
                    Asserts.TakeBreakpointAfterFault("DistGroupID_Default is not valid after initializing Distribution Group table.");

				// create the loggerID used for messages that come from the distribution system

				int lmdLoggerID = InnerGetLoggerID("LogMessageDistributionImpl", false);    // block use of ModularConfig in order to avoid recursive use of the LogMessageDistribution singleton.
                if (InnerIsLoggerIDValid(lmdLoggerID))
                {
                    perLoggerIDInfoArray = perLoggerIDInfoList.ToArray();
                    distSourceID = perLoggerIDInfoArray[lmdLoggerID].sourceID;
                }
                else
                    Utils.Asserts.TakeBreakpointAfterFault("A valid DistSourceID could not be created");
			}

			protected override void Dispose(DisposeType disposeType)
			{
				if (disposeType == DisposeType.CalledExplicitly)
				{
					Shutdown();
				}
				else
				{
					// else this is being invoked by the finalizer.
					// This only occurs once there are no more references to this object on any heap in this application space.
					//	as such we can safely assume that any thread that was created within this object, to run a delegated method
					//	in the object, must have completed or been otherwise reclaimed otherwise it would still own a reference to the
					//	object that is being finalized... (which better not be the case...)
				}
			}

			#endregion

			//-------------------------------------------------------------------
			#region Singleton - uses simple static instance to allow singleton to function during static class/member intialization

            static readonly SingletonHelper<LogMessageDistributionImpl> instanceHelper = new SingletonHelper<LogMessageDistributionImpl>();
            public static LogMessageDistributionImpl Instance { get { return instanceHelper.Instance; } }

			#endregion

			//-------------------------------------------------------------------
			#region ILogMessageDistribution methods

			// methods used by ILoggers

            /// <summary>used to create/lookup the LoggerSourceIDPtr for a given logger name</summary>
            public LoggerSourceInfo GetLoggerSourceInfo(string name)
            {
                return GetLoggerSourceInfo(name, true);
            }

            /// <summary>used to create/lookup the LoggerSourceIDPtr for a given logger name, allowUseOfModularConfig can be given as false to suppress use of modular config key Logging.Loggers.[name].LogGate as source for additional LogGate value.</summary>
            public LoggerSourceInfo GetLoggerSourceInfo(string name, bool allowUseOfModularConfig)
			{
				int lid = LoggerID_Invalid;
				LoggerSourceInfo lsid = null;

				lock (distMutex)
				{
					lid = InnerGetLoggerID(name, allowUseOfModularConfig);

					if (InnerIsLoggerIDValid(lid))
						lsid = perLoggerIDInfoArray[lid].sourceID;

                    if (lsid == null)
    					Utils.Asserts.TakeBreakpointAfterFault("GetLoggerSourceInfo result null");
				}

				return lsid;
			}

            /// <summary>used to update a logger's distribution group name</summary>
            public void SetLoggerDistributionGroupName(int id, string groupName)
			{
				lock (distMutex)
				{
					if (shutdown || !InnerIsLoggerIDValid(id))
						return;

					PerLoggerIDInfo pli = perLoggerIDInfoArray[id];

					if (pli.SelectedDistGroupName != groupName)
					{
						// update the distGroupID from the groupName.  Use DistGroupID_Default if the groupName is null or emtpy
						//	lookup the groupID (and create if necessary) if the groupName is a non-empty string.

						pli.distGroupID = (System.String.IsNullOrEmpty(groupName) ? DistGroupID_Default : InnerGetDistGroupID(groupName, true));

                        PerDistGroupIDInfo distGroupInfo = distGroupIDInfoArray[pli.distGroupID];

                        // record and inform the client that the DistGroup LoggerConfig has been changed
                        pli.DistGroupConfig = distGroupInfo.GroupLoggerConfigSetting;

                        // remap loggers back to target group based on name if needed.
						InnerRemapLoggersToDistGroups();
					}
				}
			}

            /// <summary>this method is fully renterant.  Once the allocated message has been given to the distribution system, the caller must make no further use of or reference to it.</summary>
            public LogMessage GetLogMessage()
			{
				// this code can safely use the InnerGetLogMessage method to access the pool
				//	due to its internal implementation.  Please see the comments in the Inner
				//	method for more details.  The true arguement prevents the method from
				//	allocating a message object from the pool if the distribution is being,
				//	or has been, shut down.

				return InnerGetLogMessage(true);
			}

            /// <summary>used to distribute a message from the loggerID listed as the message's source.  This method consumes the given message.  The caller's handle will be nulled by this method.</summary>
            public void DistributeMessage(ref LogMessage lm)
			{
				lock (distMutex)
				{
					if (lm == null || shutdown)
						return;

					lm.SeqNum = mesgDistributionSeqGen.Increment();
					lm.NoteEmitted();

					InnerTestAndDistributeMessage(ref lm);
				}
			}

            /// <summary>used to allow a logger to request that it block until its last emitted message has been fully delivered or the time limit is reached (renterant method on multiple callers)</summary>
            public bool WaitForDistributionComplete(int lid, TimeSpan timeLimit)
			{
				// first take a snapshot of the last mesg seq num that has been distributed from this source

				int testSeqNum = NullMessageSeqNum;
				int dgid = DistGroupID_Invalid;
				bool isMessageDeliveryInProgress = false;

				lock (distMutex)
				{
					if (!shutdown && InnerIsLoggerIDValid(lid))
					{
                        PerLoggerIDInfo plIDInfo = perLoggerIDInfoArray[lid];

                        testSeqNum = plIDInfo.lastDistributedMesgSeqNum;
                        dgid = plIDInfo.distGroupID;

                        isMessageDeliveryInProgress = InnerCheckIfMessageDeliveryIsStillInProgress(dgid, testSeqNum);
					}
				}

				if (testSeqNum == NullMessageSeqNum || !isMessageDeliveryInProgress)
					return !isMessageDeliveryInProgress;

				bool waitForever = (timeLimit == TimeSpan.Zero);

				QpcTimeStamp timeNow = QpcTimeStamp.Now;
				QpcTimeStamp waitStartTime = timeNow;
				QpcTimeStamp waitEndTime = waitStartTime + timeLimit;

				do
				{
					timeNow.SetToNow();

					mesgDeliveryOccurredNotification.WaitSec(0.02);				// 50 Hz poll rate

					lock (distMutex)
					{
						isMessageDeliveryInProgress = InnerCheckIfMessageDeliveryIsStillInProgress(dgid, testSeqNum);
					}
				} while (isMessageDeliveryInProgress && (waitForever || (timeNow <= waitEndTime)));

				return !isMessageDeliveryInProgress;
			}

            /// <summary>request distribution to start support for queued logging</summary>
            public void StartQueuedMessageDelivery()
			{
				// use preliminary asynchronous MT safe test to determine if the thread is already started (do not need to lock in that case)
				if (mesgQueueDistThread != null && mesgQueue != null && mesgQueue.IsEnabled)
					return;

				lock (distMutex)
				{
					// retest after owning lock to determine if the thread is already started
					if (mesgQueueDistThread != null && mesgQueue != null && mesgQueue.IsEnabled)
						return;

					// enable the thread and start the queue service thread
					if (mesgQueue == null)
						mesgQueue = new MessageQueue(MesgQueueSize);

					LoggerSourceInfo lsInfo = new LoggerSourceInfo(LoggerID_InternalLogger, "LogDist.InternalMesgQueue", mesgQueueLoggerGateSource.LoggerConfigSource);
					mesgQueue.SetEffectiveSourceInfo(lsInfo);
					mesgQueue.SetNotifyOnEnqueue(mesgQueueDistThreadWakeupNotification);
					mesgQueue.EnableQueue();

					if (mesgQueue != null && mesgQueue.IsEnabled)
					{
                        mesgQueueDistThread = new System.Threading.Thread(MesgQueueDistThreadFcn) 
                        { 
                            IsBackground = true, 
                            Name = "LogDist.QueuedLoggerRelay",
                        };

                        mesgQueueDistThread.Start();
					}

					if (mesgQueue == null || !mesgQueue.IsEnabled || mesgQueueDistThread == null)
						Utils.Asserts.TakeBreakpointAfterFault("LMD::StartQueuedMessageDelivery MesgQueue did not enable or thread not created.");
				}
			}

            /// <summary>request distribution to stop support for queued logging</summary>
            public void StopQueuedMessageDelivery()
            {
                if (mesgQueueDistThread == null && (mesgQueue == null || mesgQueue.IsEnabled == false))
                    return;

                System.Threading.Thread threadToJoin = null;

                lock (distMutex)
                {
                    if (mesgQueueDistThread == null && (mesgQueue == null || mesgQueue.IsEnabled == false))
                        return;

                    if (mesgQueue != null)
                        mesgQueue.DisableQueue();

                    threadToJoin = mesgQueueDistThread;
                    mesgQueueDistThread = null;

                    mesgQueue = null;
                }

                if (threadToJoin != null)
                {
                    threadToJoin.Join();
                    Utils.Fcns.DisposeOfObject(ref threadToJoin);
                }
            }

            /// <summary>used to allow a queued logger to request that it block until its last emitted message has been fully delivered or the time limit is reached (renterant method on multiple callers)</summary>
            public bool WaitForQueuedMessageDistributionComplete(int id, TimeSpan timeLimit)
			{
				// first take a snapshot of the last mesg seq num that has been enqueued to the message queue

				int testSeqNum = NullMessageSeqNum;
				bool isMessageInQueue = false;

				lock (distMutex)
				{
					if (!shutdown && InnerIsLoggerIDValid(id))
					{
						testSeqNum = mesgQueue.LastEnqueuedSeqNum;
						isMessageInQueue = mesgQueue.IsMessageStillInQueue(testSeqNum);		// the queue might be empty...
					}
				}

				if (testSeqNum == NullMessageSeqNum || !isMessageInQueue)
					return !isMessageInQueue;

				bool waitForever = (timeLimit == TimeSpan.Zero);

				QpcTimeStamp timeNow = QpcTimeStamp.Now;
				QpcTimeStamp waitStartTime = timeNow;
				QpcTimeStamp waitEndTime = waitStartTime + timeLimit;

				do
				{
					timeNow.SetToNow();

					mesgDeliveryOccurredNotification.WaitSec(0.02);		// 50 Hz poll rate

					isMessageInQueue = mesgQueue.IsMessageStillInQueue(testSeqNum);		// the queue might be empty...
				} while (isMessageInQueue && (waitForever || (timeNow <= waitEndTime)));

				if (isMessageInQueue)		// we exausted the wait period and the message is still in the queue
					return false;

				TimeSpan timeRemaining = timeLimit - (timeNow - waitStartTime);	// usable time less (used time)
				if (waitForever)
					timeRemaining = TimeSpan.Zero;
				else if (timeRemaining <= TimeSpan.Zero)
					timeRemaining = TimeSpan.FromTicks(1);

				return WaitForDistributionComplete(id, timeRemaining);
			}

            /// <summary>used to support QueuedLogger loggers.  This method consumes the given message.  The caller's handle will be nulled by this method.</summary>
            public void EnqueueMessageForDistribution(ref LogMessage lm)
			{
                MessageQueue capturedMesgQueue = mesgQueue;

                if (capturedMesgQueue == null)
                {
                    StartQueuedMessageDelivery();
                    capturedMesgQueue = mesgQueue;
                }

				if (lm != null)
				{
					// enqueue the given message and then reset the pointer that was passed to us.
					//	(this method consumes the given message);

                    if (capturedMesgQueue != null)
                        capturedMesgQueue.EnqueueMesg(lm);

					lm.RemoveReference(ref lm);		// mesgQueue always adds a reference internally - as such we must explicitly remove the caller's reference before returing
				}
			}

            /// <summary>
            /// Allows caller (typically distribution itself, queued handlers or custom message loggers) to scan and replace pooled messages with non-pooled ones for hanlders that do not support Reference Counted Semantics
            /// </summary>
            public void ReallocateMessageForNonRefCountedHandler(ref LogMessage lm)
            {
                if (lm != null && lm.BelongsToPool)
                {
                    LogMessage lmTemp = lm;
                    lm = new LogMessage(lmTemp);
                    lmTemp.RemoveReference(ref lmTemp);
                }
            }

            /// <summary>
            /// Allows caller (typically distribution itself, queued handlers or custom message loggers) to scan and replace pooled messages with non-pooled ones for hanlders that do not support Reference Counted Semantics
            /// </summary>
            public void ReallocateMessagesForNonRefCountedHandler(LogMessage[] lmArray)
            {
                for (int idx = 0; idx < lmArray.Length; idx++)
                {
                    ReallocateMessageForNonRefCountedHandler(ref lmArray[idx]);
                }
            }

            /// <summary>
            /// Allows caller (typically distribution itself, queued handlers or custom message loggers) to scan and replace pooled messages with non-pooled ones for hanlders that do not support Reference Counted Semantics
            /// </summary>
            public void ReallocateMessagesForNonRefCountedHandler(List<LogMessage> lmList)
            {
                for (int idx = 0; idx < lmList.Count; idx++)
                {
                    LogMessage lm = lmList[idx];
                    ReallocateMessageForNonRefCountedHandler(ref lm);
                    lmList[idx] = lm;
                }
            }

            #endregion

            //-------------------------------------------------------------------
            #region Other LogMessageDistImpl methods (used by public static helpers)

            // methods used by global methods

            public void AddLogMessageHandlerToDistributionGroup(ILogMessageHandler logMessageHandler, string groupName)
			{
				lock (distMutex)
				{
					int dgid = InnerGetDistGroupID(groupName, true);

					if (!InnerIsDistGroupIDValid(dgid))
					{
						Utils.Asserts.TakeBreakpointAfterFault("AddLogMessageHandler...::Given GroupName could not be created.");
						return;
					}

					PerDistGroupIDInfo dgInfo = distGroupIDInfoArray[dgid];

					if (logMessageHandler != null)
					{
						int dhInfoIdx = dgInfo.distHandlerInfoList.Count;
						dgInfo.Add(new DistHandlerInfo(logMessageHandler));
						DistHandlerInfo distHandlerInfo = dgInfo.distHandlerInfoList [dhInfoIdx];

						logMessageHandler.NotifyOnCompletedDelivery.AddItem(mesgDeliveryOccurredNotification);

						if (dgInfo.UpdateActiveLoggerConfig())
							InnerUpdateLoggerDistGroupConfigForGroup(dgid);
					}
				}
			}

			public void MapLoggersToDistributionGroup(LoggerNameMatchType matchType, string matchStr, string groupName)
			{
				lock (distMutex)
				{
					int dgid = InnerGetDistGroupID(groupName, true);

					if (!InnerIsDistGroupIDValid(dgid))
					{
						Utils.Asserts.TakeBreakpointAfterFault("MapLoggersToDistributionGroup::Given GroupName could not be created.");
						return;
					}

					PerDistGroupIDInfo dgInfo = distGroupIDInfoArray[dgid];

					dgInfo.loggerNameMatchType = matchType;
					dgInfo.loggerNameMatchStr = matchStr;
					if (matchType == LoggerNameMatchType.Regex)
					{
						try
						{
							dgInfo.loggerNameMatchRegex = new System.Text.RegularExpressions.Regex(matchStr);
						}
						catch (System.Exception ex)
						{
							Utils.Asserts.LogFaultOccurance(Utils.Fcns.CheckedFormat("MapLoggersToDistributionGroup grp:{0}, regex:{1} failed", groupName, matchStr), ex);
						}
					}

					InnerRemapLoggersToDistGroups();
				}
			}

			public void SetDistributionGroupGate(string groupName, LogGate logGate)
			{
				lock (distMutex)
				{
					int dgid = InnerGetDistGroupID(groupName, true);

					if (!InnerIsDistGroupIDValid(dgid))
					{
						Utils.Asserts.TakeBreakpointAfterFault("SetDistributionGroupGate::Given GroupName could not be created.");
						return;
					}

					PerDistGroupIDInfo dgInfo = distGroupIDInfoArray[dgid];

                    // read the LoggerConfig (struct), update the LogGate field and then write it back to the PerDistGroupIDInfo so that it can notify observers of the change.
					LoggerConfig lc = dgInfo.GroupLoggerConfigSetting;
					lc.LogGate = logGate;
					dgInfo.GroupLoggerConfigSetting = lc;

					if (dgInfo.UpdateActiveLoggerConfig())
						InnerUpdateLoggerDistGroupConfigForGroup(dgid);
				}
			}

            /// <summary>
            /// Setup an internal link from the fromGroupName to the group (and groups) represented by linkToGroupName so that messages that are 
            /// accepted and emitted into the first group will also be passed to the linked group and to any other group that it is linked to at time
            /// this link is established.  
            /// </summary>
            /// <remarks>
            /// Currently links are recursively evaluated at the time the link is made but they are not re-evaluated when other groups are linked to each other.
            /// Rersive evaluation is designed to handle looped links so that in the worst case adding a link will cause messages to be delivered to all of the groups
            /// in the loop.  Order of delivery is dependant on the order that groups are linked/added so the delivery order for two groups that are in a loop will not
            /// generally be the same as each other.  
            /// Evaluation of the handlers in each group is dynamic so that addition of a handler to a group will change the set of handlers to which messages are delivered
            /// for both this group and for any group that is linked, directly or indirectly, to this group.
            /// </remarks>
            public void LinkDistributionToGroup(string fromGroupName, string linkToGroupName)
            {
                lock (distMutex)
                {
                    int fromDgid = InnerGetDistGroupID(fromGroupName, true);
                    int linkToDgid = InnerGetDistGroupID(linkToGroupName, true);

                    if (!InnerIsDistGroupIDValid(fromDgid) || !InnerIsDistGroupIDValid(linkToDgid))
                    {
                        Utils.Asserts.TakeBreakpointAfterFault("LinkDistributionGroups::One or both given group names could not be created.");
                        return;
                    }

                    PerDistGroupIDInfo fromDgInfo = distGroupIDInfoArray[fromDgid];
                    PerDistGroupIDInfo linkToDgInfo = distGroupIDInfoArray[linkToDgid];

                    Queue<PerDistGroupIDInfo> linkBuildQueue = new Queue<PerDistGroupIDInfo>();
                    linkBuildQueue.Enqueue(linkToDgInfo);

                    while (linkBuildQueue.Count != 0)
                    {
                        PerDistGroupIDInfo linkTestDgInfo = linkBuildQueue.Dequeue();

                        if (!fromDgInfo.AddLinkTo(linkTestDgInfo))
                            continue;       // given ID is already in the list - do not add it or its subs

                        foreach (PerDistGroupIDInfo dgInfo in linkTestDgInfo.LinkedDistGroupArray)
                            linkBuildQueue.Enqueue(dgInfo);
                    }
                }
            }

            public void StartupIfNeeded()
            {
                StartupIfNeeded(!shutdown ? "Logging started" : "Logging restarted");
            }

            public void StartupIfNeeded(string mesg)
            {
                lock (distMutex)
                {
                    if (!shutdown)
                        return;

                    mesgPool.StartIfNeeded();

                    InnerRestartAllHandlers();

                    shutdown = false;

                    // Enable all of the dist groups (internally will restore their distribution group gates to prior value)
                    for (int dgid = 0; dgid < distGroupIDInfoArray.Length; dgid++)
                    {
                        PerDistGroupIDInfo dgInfo = distGroupIDInfoArray[dgid];
                        dgInfo.Disabled = false;
                    }


                    // now Enable all of the logger's (internally will restore their distribution group gate to prior value)
                    for (int lid = 0; lid < perLoggerIDInfoArray.Length; lid++)
                    {
                        PerLoggerIDInfo lInfo = perLoggerIDInfoArray[lid];
                        lInfo.Disabled = false;
                    }

                    InnerLogLog(MesgType.Debug, mesg);
                }
            }

			public void Shutdown() 
            { 
                Shutdown("Distribution has been stopped."); 
            }

			public void Shutdown(string mesg)
			{
				// mark that we are shutting down
				lock (distMutex)
				{
					if (shutdown)
						return;

                    // now Diable all of the logger's (internally will set their distribution group gate to None)
                    for (int lid = 0; lid < perLoggerIDInfoArray.Length; lid++)
                    {
                        PerLoggerIDInfo lInfo = perLoggerIDInfoArray[lid];
                        lInfo.Disabled = true;
                    }

                    // Disable all of the dist groups
					for (int dgid = 0; dgid < distGroupIDInfoArray.Length; dgid++)
					{
						PerDistGroupIDInfo dgInfo = distGroupIDInfoArray[dgid];
                        dgInfo.Disabled = true;
					}

					shutdown = true;

					InnerLogLog(MesgType.Debug, mesg);				// log message (from inner source) that distribution has stopped
				}

				// leave the lock and run the pattern required to shutdown the inner message queue and service thread
				//	that are used by QueuedLoggers.

				{
					// disable the queue (tells mesgQueueDistThread to stop)
					if (mesgQueue != null && mesgQueue.IsEnabled)
						mesgQueue.DisableQueue();

					// signal the thread to wakeup and service the disabled queue
					if (mesgQueueDistThreadWakeupNotification != null)
						mesgQueueDistThreadWakeupNotification.Notify();

					// join the thread (if it has been started)
					if (mesgQueueDistThread != null)
					{
						mesgQueueDistThread.Join();
						mesgQueueDistThread = null;
					}

					// dist thread will have drained the queue by this point (if it could do so)
				}

				// relock the distribution and complete the shutdown of the handlers and the distribution.
				lock (distMutex)
				{
					InnerShutdownAllHandlers();						// stop all handlers

					mesgPool.Shutdown();						// deallocate and disable its vector of saved objects.
				}
			}

			#endregion

			//-------------------------------------------------------------------
			#region inner implementation methods
			// inner methods - these require that the mMutex already be held by the caller

			protected void ForceFullFlush()
			{
				lock (distMutex)
				{
					if (shutdown)
						return;

					InnerFlushAllHandlers();
				}
			}

			protected void MesgQueueDistThreadFcn()
			{
                MessageQueue myMesgQueue = mesgQueue;

                if (!(myMesgQueue.IsEnabled && mesgQueueDistThreadWakeupNotification != null))
    				Utils.Asserts.TakeBreakpointAfterFault("MesgQueueDistThreadFcn is not ready to start");

				const int maxMesgDequeueCount = 100;
				List<LogMessage> logMesgList = new List<LogMessage>(maxMesgDequeueCount);
				List<LogMessage> sameDistMesgSetList = new List<LogMessage>(maxMesgDequeueCount);

				bool qEnabled = false;
				int qCount = 0, qDisabledCount = 0;

				do
				{
                    qEnabled = myMesgQueue.IsEnabled;
					if (!qEnabled)
						qDisabledCount++;

					qCount = myMesgQueue.QueueCount;

					if (qCount == 0)
					{
						mesgQueueDistThreadWakeupNotification.WaitSec(0.10);
						continue;
					}

					lock (distMutex)
					{
						// acquire access to internals of log distribution object so that
						//	we can process the block of messages in an atomic manner relative to
						//	pulling them from the queue.

						if (myMesgQueue.DequeueMesgSet(maxMesgDequeueCount, ref logMesgList) == 0)
							continue;

						// divide up the block of messages into sub-sections each of which
						//	share the same distribution id and distribute the messages
						//	in the sub-section to that distribution group.

						sameDistMesgSetList.Clear();
						int dgid = DistGroupID_Invalid;
						LogGate dgGate = LogGate.All;

						for (int idx = 0; idx < logMesgList.Count; idx++)
						{
							LogMessage lm = logMesgList [idx];
							LoggerSourceInfo lsid = null;
							int lid = LoggerID_Invalid;
							PerLoggerIDInfo loggerIDInfo = null;
							int mesgdgid = DistGroupID_Invalid;

							if (lm != null)
								lsid = lm.LoggerSourceInfo;

							if (lsid != null)
								lid = lsid.ID;

							if (InnerIsLoggerIDValid(lid))
								loggerIDInfo = perLoggerIDInfoArray[lid];

							if (loggerIDInfo != null)
								mesgdgid = loggerIDInfo.distGroupID;

							if (lm != null && !InnerIsDistGroupIDValid(mesgdgid))
							{
								lm.RemoveReference(ref lm);
								logMesgList [idx] = null;
								continue;
							}

							// if we have accumulated some messages in the vector and the new message's distGroupID is not the same as the
							//	the one(s) for the messages in the vector then distribute the vector contents and empty it so that a new
							//	vector can be started for the new int value.

							if (dgid != mesgdgid && sameDistMesgSetList.Count != 0 && InnerIsDistGroupIDValid(dgid))
							{
								// distribute the sameDistMesgSetList to its dgid (releases the pointers after they have been distributed).
								InnerDistributeMessages(sameDistMesgSetList.ToArray(), dgid);
								sameDistMesgSetList.Clear();
							}

							if (dgid != mesgdgid)
							{
								dgid = mesgdgid;
								dgGate = distGroupIDInfoArray[dgid].ActiveLoggerConfig.LogGate;
							}

							// if the message type is enabled in the dist group then append it to the vector, 
							//	otherwise release it now (message will not be handled by any handlers)

							if (dgGate.IsTypeEnabled(lm.MesgType))
							{
								lm.SeqNum = mesgDistributionSeqGen.Increment();

								sameDistMesgSetList.Add(lm);
							}
							else
								lm.RemoveReference(ref lm);

							logMesgList [idx] = null;
							lm = null;
						}

						// distribute the last set (if  any)
						if (sameDistMesgSetList.Count != 0 && InnerIsDistGroupIDValid(dgid))
						{
							// distribute the sameDistMesgSetList to its dgid (releases the pointers after they have been distributed).
							InnerDistributeMessages(sameDistMesgSetList.ToArray(), dgid);
							sameDistMesgSetList.Clear();
						}
					}
				} while (qEnabled || (qCount != 0 && qDisabledCount < 50));

				if (qCount != 0)
				{
					InnerLogLog(MesgType.Error, Utils.Fcns.CheckedFormat("MesgQueueDistThreadFcn: Unable to distribute {0} messages during shutdown.", qCount));
				}

                if (!(qCount == 0 && !qEnabled))
    				Utils.Asserts.TakeBreakpointAfterFault("MesgQueueDistThreadFcn: Abnormal exit conditions");
			}

			LogMessage InnerGetLogMessage(bool blockDuringShutdown)
			{
				if (shutdown && blockDuringShutdown)
					return null;

				return mesgPool.GetFreeObjectFromPool();
			}

			protected void InnerLogLog(MesgType mesgType, string mesg)
			{
				LogMessage lm = InnerGetLogMessage(false);

				if (lm != null)
				{
					lm.Setup(distSourceID, mesgType, mesg, new System.Diagnostics.StackFrame(1, true));

					lm.SeqNum = mesgDistributionSeqGen.Increment();
					lm.NoteEmitted();

					InnerTestAndDistributeMessage(ref lm);
				}
			}

            /// <summary>
            /// This method gets, or creates, the loggerID for the given LoggerName.  If allowUseOfModularConfig is true then
            /// the method will also attempt to extract a log gate using the ConfigKey Logging.Loggers.[loggerName].LogGate.
            /// Passing false prevents use of modular config.
            /// </summary>
			protected int InnerGetLoggerID(string loggerName, bool allowUseOfModularConfig)
            {
                int lid = LoggerID_Invalid;

                {
                    if (loggerNameToIDMap.TryGetValue(loggerName ?? String.Empty, out lid))
                        return (InnerIsLoggerIDValid(lid) ? lid : LoggerID_Invalid);

                    IConfigKeyAccess loggersLogGateReduceICKA = null;
                    IConfigKeyAccess loggersLogGateIncreaseICKA = null;

                    if (allowUseOfModularConfig)
                    {
                        string gateReduceKeyName = "Logging.Loggers.{0}.LogGate.Reduce".CheckedFormat(loggerName);
                        string gateIncreaseKeyName = "Logging.Loggers.{0}.LogGate.Increase".CheckedFormat(loggerName);

                        ConfigKeyAccessSpec reducedCKAS = new ConfigKeyAccessSpec(gateReduceKeyName, new ConfigKeyAccessFlags() { IsOptional = true, MayBeChanged = true, SilenceIssues = true });
                        ConfigKeyAccessSpec increaseCKAS = new ConfigKeyAccessSpec(gateIncreaseKeyName, new ConfigKeyAccessFlags() { IsOptional = true, MayBeChanged = true, SilenceIssues = true });

                        loggersLogGateReduceICKA = Config.Instance.GetConfigKeyAccess(reducedCKAS);
                        loggersLogGateIncreaseICKA = Config.Instance.GetConfigKeyAccess(increaseCKAS);

                        // if the ICKA that we found has a value then subscribe to later updates that might be made to this value.
                        if (loggersLogGateReduceICKA.IsUsable || loggersLogGateIncreaseICKA.IsUsable)
                            InnerSubscribeToModularConfigIfNeeded();

                        // map unusuable log gate ICKA items to null to prevent trying to use them when they were not defined.
                        loggersLogGateReduceICKA = (loggersLogGateReduceICKA.IsUsable) ? loggersLogGateReduceICKA : null;
                        loggersLogGateIncreaseICKA = (loggersLogGateIncreaseICKA.IsUsable) ? loggersLogGateIncreaseICKA : null;
                    }

                    // the name was not in the table: need to create a new one
                    lid = perLoggerIDInfoArray.Length;

                    loggerNameToIDMap.Add(loggerName, lid);

                    PerLoggerIDInfo plIDInfo = new PerLoggerIDInfo(lid) 
                        { 
                            loggersReduceLogGateICKA = loggersLogGateReduceICKA,
                            loggersIncreaseLogGateICKA = loggersLogGateIncreaseICKA,
                        }.UpdateLogGateFromModularConfig();

                    perLoggerIDInfoList.Add(plIDInfo);
                    perLoggerIDInfoArray = perLoggerIDInfoList.ToArray();       // we expect there to be a significant amount of churn on this as logger IDs get added but that this churn will completely stop after the application reaches steady state.

                    LoggerSourceInfo sourceID = new LoggerSourceInfo(lid, loggerName, plIDInfo.seqLoggerConfigSource.LoggerConfigSource);

                    plIDInfo.sourceID = sourceID;
                    plIDInfo.DistGroupConfig = distGroupIDInfoArray[DistGroupID_Default].ActiveLoggerConfig;

                    InnerRemapLoggerToDistGroup(lid);
                }

                return lid;
            }

            private bool haveRegisteredWithModularConfig = false;

            /// <summary>
            /// This method is used each time we have made a Usable IConfigKeyAccess object to read a Logger's LogGate level.
            /// The first time this method is called it will register the desired to be notified each time one or more config keys are changed.
            /// </summary>
            private void InnerSubscribeToModularConfigIfNeeded()
            {
                if (!haveRegisteredWithModularConfig)
                {
                    Config.Instance.ChangeNotificationList.OnNotify += new BasicNotificationDelegate(ChangeNotificationList_OnNotify);

                    haveRegisteredWithModularConfig = true;
                }
            }

            /// <summary>
            /// This is the method that is used as a delegate to field Config ChangeNotification notifications.
            /// To prevent this method from blocking the thread that called into the Config.Instance, we re-thread the call onto a ThreadPool thread.
            /// As such changes in LogGate levels are applied somewhat after the provider(s) have been given new config key values and the config system has
            /// informed its ChangeNotificationList that such a change has happened.
            /// </summary>
            void ChangeNotificationList_OnNotify()
            {
                System.Threading.ThreadPool.QueueUserWorkItem(HandleModularConfigNotification);
            }

            /// <summary>This method fields and implements the actual rescan of the PerLoggerIDInfo's LogGate ICKA objects.  This is done while owning the distMutex but is otherwise non-blocking.</summary>
            void HandleModularConfigNotification(object state)
            {
                lock (distMutex)
                {
                    foreach (PerLoggerIDInfo plid in perLoggerIDInfoArray)
                    {
                        plid.UpdateLogGateFromModularConfig();
                    }
                }
            }

            /// <summary>
            /// Returns true if the given lid is a valid index into the perLoggerIDInfoArray.
            /// </summary>
			bool InnerIsLoggerIDValid(int lid) 
            { 
                return (lid >= 0 && lid < perLoggerIDInfoArray.Length); 
            }

			int InnerGetDistGroupID(string name) { return InnerGetDistGroupID(name, false); }
			protected int InnerGetDistGroupID(string name, bool createIfNeeded)
			{
				int dgid = DistGroupID_Invalid;

				if (!string.IsNullOrEmpty(name))
				{
					if (distGroupNameToIDMap.TryGetValue(name ?? String.Empty, out dgid) || !createIfNeeded)
						return (InnerIsDistGroupIDValid(dgid) ? dgid : DistGroupID_Invalid);

					// the name was not in the table: need to create a new one
					dgid = distGroupIDInfoList.Count;

                    distGroupIDInfoList.Add(new PerDistGroupIDInfo(dgid, name));
                    distGroupIDInfoArray = distGroupIDInfoList.ToArray();

                    distGroupNameToIDMap.Add(name, dgid);
				}

				return dgid;
			}

            /// <summary>
            /// Returns true if the given gid is a valid index into the distGroupIDInfoArray.
            /// </summary>
			bool InnerIsDistGroupIDValid(int gid) 
            { 
                return (gid >= 0 && gid < distGroupIDInfoArray.Length); 
            }

			protected void InnerUpdateLoggerDistGroupConfigForGroup(int dgid)
			{
				if (!InnerIsDistGroupIDValid(dgid))
				{
					Utils.Asserts.TakeBreakpointAfterFault("dgid valid in InnerUpdateLoggerDistGroupConfigForGroup");
					return;
				}

				PerDistGroupIDInfo dgInfo = distGroupIDInfoArray[dgid];
				LoggerConfig activeLoggerConfig = dgInfo.ActiveLoggerConfig;

				// tell all of the loggers about the new value of their distribution groups activeLogGate
				int lid = 0, lastLID = perLoggerIDInfoArray.Length - 1;

				for (; lid <= lastLID; lid++)
				{
					PerLoggerIDInfo lInfo = perLoggerIDInfoArray[lid];

					if (lInfo.distGroupID == dgid)
						lInfo.DistGroupConfig = activeLoggerConfig;		// this property handles change detection
				}
			}


            /// <summary>Loop through each logger and determin which distribution group it belongs to (default if none)</summary>
			protected void InnerRemapLoggersToDistGroups()
			{
				for (int lid = 0; lid < perLoggerIDInfoArray.Length; lid++)
					InnerRemapLoggerToDistGroup(lid);
			}

            /// <summary>
            /// Remap the selected loggerID to a target distribution group based on per group logger name matching.  
            /// LoggerName based mapping is supressed if the logger has been assigned to a specific group.
            /// </summary>
			protected void InnerRemapLoggerToDistGroup(int lid)
			{
				if (!InnerIsLoggerIDValid(lid))
					return;

				PerLoggerIDInfo plInfo = perLoggerIDInfoArray[lid];

				// surpress logger name to dgid mapping for loggers that have been assigned a specific distGroupName
                if (plInfo.SelectedDistGroupName != LookupDistributionGroupName)
					return;

				// march through the distribution groups
				//	set the mappedDistGroupID for this logger to the first group who's match rules match our logger name

				string loggerName = plInfo.sourceID.Name;

				// setup common vector iterator items

				foreach (PerDistGroupIDInfo dgInfo in distGroupIDInfoArray)
				{
					// skip groups that have no match rules defined
					if (dgInfo.loggerNameMatchType == LoggerNameMatchType.None || String.IsNullOrEmpty(dgInfo.loggerNameMatchStr))
						continue;

					bool matches = false;

					switch (dgInfo.loggerNameMatchType)
					{
						case LoggerNameMatchType.MatchPrefix:
							matches = loggerName.StartsWith(dgInfo.loggerNameMatchStr);
							break;
						case LoggerNameMatchType.MatchSuffix:
							matches = loggerName.EndsWith(dgInfo.loggerNameMatchStr);
							break;
						case LoggerNameMatchType.MatchContains:
							matches = loggerName.Contains(dgInfo.loggerNameMatchStr);
							break;
						case LoggerNameMatchType.Regex:
							try
							{
								if (dgInfo.loggerNameMatchRegex != null)
									matches = dgInfo.loggerNameMatchRegex.IsMatch(loggerName);
							}
							catch { }
							break;

						default:
							break;
					}

					if (matches)
					{
						plInfo.distGroupID = dgInfo.DistGroupID;
						plInfo.DistGroupConfig = dgInfo.ActiveLoggerConfig;
						return;
					}
				}
			}

			protected void InnerTestAndDistributeMessage(ref LogMessage lm)
			{
				if (lm != null && lm.LoggerSourceInfo != null && lm.Emitted)
				{
					int lid = lm.LoggerSourceInfo.ID;
					int dgid = DistGroupID_Invalid;
					LoggerConfig dgConfig = LoggerConfig.None;

					if (InnerIsLoggerIDValid(lid))
					{
						PerLoggerIDInfo lInfo = perLoggerIDInfoArray[lid];
						dgid = lInfo.distGroupID;
						lInfo.lastDistributedMesgSeqNum = lm.SeqNum;
					}

					bool dgidValid = InnerIsDistGroupIDValid(dgid);

					if (dgidValid)
						dgConfig = distGroupIDInfoArray[dgid].ActiveLoggerConfig;

					bool msgEnabled = dgConfig.IsTypeEnabled(lm.MesgType);

					if (msgEnabled && dgidValid)
						InnerDistributeMessage(ref lm, dgid);		// this may consume the message (and null our pointer)
				}

				if (lm != null)
					lm.RemoveReference(ref lm);
			}

			protected void InnerDistributeMessage(ref LogMessage lm, int srcDistGroupID)
			{
				// all validation and testing of passed parameters is performed by caller
				// message ptr is non-null, message type are enabled in group and distGroupID is known valid

				PerDistGroupIDInfo srcDgInfo = distGroupIDInfoArray[srcDistGroupID];
                bool messageHasBeenReallocated = false;

                // traverse groups to which this group is linked and deliver the message to each group's list of log message handlers.
                foreach (PerDistGroupIDInfo dgInfo in srcDgInfo.LinkedDistGroupArray)
                {
                    if (!srcDgInfo.ActiveLoggerConfig.SupportsReferenceCountedRelease && !messageHasBeenReallocated)
                    {
                        ReallocateMessageForNonRefCountedHandler(ref lm);
                        messageHasBeenReallocated = true;
                    }

                    // tell each of the log message handlers in this group to process this message

                    foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList)
                    {
                        if (dhInfo.Valid && dhInfo.LoggerConfig.IsTypeEnabled(lm.MesgType))
                            dhInfo.LMH.HandleLogMessage(lm);
                    }
                }

				// release the message if we still have a handle to it

				if (lm != null)
					lm.RemoveReference(ref lm);
			}

			protected void InnerDistributeMessages(LogMessage [] lmArray, int srcDistGroupID)
			{
				// all validation and testing of passed parameters is performed by caller
				// vector of messages is assumed non-empty, non-null, all types are enabled in group and distGroupID is known valid

				PerDistGroupIDInfo srcDgInfo = distGroupIDInfoArray[srcDistGroupID];
                bool messagesHaveBeenReallocated = false;

                // traverse groups to which this group is linked and deliver the messages to each group's list of log message handlers.
                foreach (PerDistGroupIDInfo dgInfo in srcDgInfo.LinkedDistGroupArray)
                {
                    if (!dgInfo.ActiveLoggerConfig.SupportsReferenceCountedRelease && !messagesHaveBeenReallocated)
                    {
                        ReallocateMessagesForNonRefCountedHandler(lmArray);
                        messagesHaveBeenReallocated = true;
                    }

                    // tell each of the log message handlers to process this vector of messages

                    foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList)
                    {
                        if (dhInfo.Valid)
                            dhInfo.LMH.HandleLogMessages(lmArray);
                    }
                }

				// release each of the messages in the vector if we still have a handle to any of them.

				for (int idx = 0; idx < lmArray.Length; idx++)
				{
					LogMessage lm = lmArray [idx];
					lmArray [idx] = null;
					lm.RemoveReference(ref lm);
				}
			}

			protected bool InnerCheckIfMessageDeliveryIsStillInProgress(int dgid, int testSeqNum)
			{
				if (!InnerIsDistGroupIDValid(dgid))
					return false;

				PerDistGroupIDInfo dgInfo = distGroupIDInfoArray[dgid];

				foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList)
				{
					if (dhInfo.Valid && dhInfo.LMH.IsMessageDeliveryInProgress(testSeqNum))
						return true;
				}

				return false;
			}

			protected void InnerFlushAllHandlers()
			{
				// first shutdown each of the logger specific handlers
				foreach (PerDistGroupIDInfo dgInfo in distGroupIDInfoArray)
				{
					foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList)
					{
						if (dhInfo.Valid)
							dhInfo.LMH.Flush();
					}
				}
			}

			protected void InnerShutdownAllHandlers()
			{
                foreach (PerDistGroupIDInfo dgInfo in distGroupIDInfoArray)
				{
					foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList)
					{
						if (dhInfo.Valid)
							dhInfo.LMH.Shutdown();
					}
				}
			}

            protected void InnerRestartAllHandlers()
            {
                foreach (PerDistGroupIDInfo dgInfo in distGroupIDInfoArray)
                {
                    foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList)
                    {
                        if (dhInfo.Valid)
                            dhInfo.LMH.StartIfNeeded();
                    }
                }
            }

			#endregion
        }

		#endregion

		//-------------------------------------------------------------------
		#region LogMessageDistributionImpl singleton public accessor methods

		//-------------------------------------------------------------------
		// singleton LogMessageDistributionImpl system and method used to setup log message distribution groups

        /// <summary>Gives the caller access to the ILogMessageDistribution singleton</summary>
		public static ILogMessageDistribution GetLogMessageDistribution()
		{
			return LogMessageDistributionImpl.Instance; 
		}

        /// <summary>Internal method used to get access to the LMD singleton using its implementation type rather than its primary public interface.</summary>
        private static LogMessageDistributionImpl GetLogMessageDistributionImpl()
		{
			return LogMessageDistributionImpl.Instance; 
		}

        /// <summary>Method allocates a LogMessage from the distribution pool.  This method is generally used only by Loggers</summary>
        /// <returns>A log message to use, allocated from the distribution pool, or null if the distribution engine has been shutdown already.</returns>
        public static LogMessage AllocateLogMessageFromDistribution()		
		{
			return GetLogMessageDistribution().GetLogMessage();
		}

        /// <summary>Internal "constant" that defines the name of the Default Log Distribution Group.  "LDG.Default"</summary>
        private static readonly string defaultDistributionGroupName = "LDG.Default";

        /// <summary>Internal "constant" that defines the name of the Lookup Log Distribution Group.  Loggers placed in this group can be remapped to other groups using the MapLoggersToDistirbutionGroup method.  "LDG.Lookup"</summary>
        private static readonly string lookupDistributionGroupName = "LDG.Lookup";

        /// <summary>This is the default Logging Distribution Group name.  This group is used for all cases where a logger does not otherwise specify a different one and/or it has not been mapped to use another one.  "LDG.Default"</summary>
		public static string DefaultDistributionGroupName { get { return defaultDistributionGroupName; } }

        /// <summary>This is the custom distribution group name that is used to indicate that mapping should be applied to it.  "LDG.Lookup"</summary>
        public static string LookupDistributionGroupName { get { return lookupDistributionGroupName; } }

        /// <summary>Adds the given ILogMessageHandler to the indicated group</summary>
        public static void AddLogMessageHandlerToDistributionGroup(string groupName, ILogMessageHandler logMessageHandler)
		{
			GetLogMessageDistributionImpl().AddLogMessageHandlerToDistributionGroup(logMessageHandler, groupName);
		}

        /// <summary>(re)Maps the selected loggers to the given group name.  Loggers must have initially placed themselves in the LookupDistributionGroupName group in order to support being mapped.</summary>
        public static void MapLoggersToDistributionGroup(LoggerNameMatchType matchType, string matchStr, string groupName)
		{
			GetLogMessageDistributionImpl().MapLoggersToDistributionGroup(matchType, matchStr, groupName);
		}

        /// <summary>Sets a gate level for messages to a given group so that it may be more restritive than that of the most permissive handler that is attached to the group.</summary>
        public static void SetDistributionGroupGate(string groupName, LogGate logGate)
		{
			GetLogMessageDistributionImpl().SetDistributionGroupGate(groupName, logGate);
		}

        /// <summary>Links distribution to one group to also distribute its messages through a second group as well.</summary>
        public static void LinkDistributionToGroup(string fromGroupName, string toGroupName)
        {
            GetLogMessageDistributionImpl().LinkDistributionToGroup(fromGroupName, toGroupName);
        }

        /// <summary>Links distribution from a custom group to also distribute its messages through the default group.</summary>
        public static void LinkDistributionToDefaultGroup(string fromGroupName)
        {
            GetLogMessageDistributionImpl().LinkDistributionToGroup(fromGroupName, DefaultDistributionGroupName);
        }


        /// <summary>Changes the group used by specific source name to a different target group name on a source by source basis.</summary>
        public static void SetDistributionGroupName(string sourceName, string groupName)
		{
			ILogMessageDistribution dist = GetLogMessageDistribution();
			LoggerSourceInfo lsInfo = dist.GetLoggerSourceInfo(sourceName);
			if (lsInfo != null)
				dist.SetLoggerDistributionGroupName(lsInfo.ID, groupName);
		}

        /// <summary>Stops the logging distribution system and flushes all buffers.  Normally this should be the last intended use of the Log Distribution System.</summary>
        public static void ShutdownLogging()
		{
			GetLogMessageDistributionImpl().Shutdown();
		}

        /// <summary>(re)Starts the logging distribution system if it has been shutdown since the last time it was initially constructed or re-started.</summary>
        public static void StartLoggingIfNeeded()
        {
            GetLogMessageDistributionImpl().StartupIfNeeded();
        }

		//-------------------------------------------------------------------

        /// <summary>Adds the given ILogMessaeHandler to the default distribution group.</summary>
        public static void AddLogMessageHandlerToDefaultDistributionGroup(ILogMessageHandler logMessageHandler)
		{
			AddLogMessageHandlerToDistributionGroup(DefaultDistributionGroupName, logMessageHandler);
		}

        /// <summary>Sets the group gate level for the default distribution group.</summary>
        public static void SetDefaultDistributionGroupGate(LogGate logGate)
		{
			SetDistributionGroupGate(DefaultDistributionGroupName, logGate);
		}

		#endregion

		//-------------------------------------------------------------------
	}
}

//-------------------------------------------------------------------
