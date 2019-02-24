//-------------------------------------------------------------------
/*! @file LogDistribution.cs
 *  @brief This file provides the internal class definition that is used to implement the LogDistribution singleton class that handles allocation and distribution of LogMessages for the MosaicLib Logging system.
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

using MosaicLib.Modular.Config;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Utils.StringMatching;

namespace MosaicLib
{
	public static partial class Logging
	{
		#region early definitions (DistGroupID constants, LoggerNameMatchType enum)

        /// <summary>the groupID to which all logger source names belong if they are not explicitly remapped to some other group [0]</summary>
		const int DistGroupID_Default = 0;

        /// <summary>the groupID to indicate that none was provided. [-1]</summary>
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

		#region ILogMessageDistribution interface (and related sub-interfaces)

        /// <summary>
        /// This interface is an agregate version of the set of interfaces that are supported by LogMessageDistribution objects in order to be able to support loggers and distributed their emitted messages to the matching set of log message handlers.
        /// This interface is composed of the ILogMessageDistributionForLoggers, ILogMessageDistributionForQueuedLoggers, and ILogMessageDistributionManagement sub-interfaces.
        /// </summary>
        public interface ILogMessageDistribution : ILogMessageDistributionForLoggers, ILogMessageDistributionForQueuedLoggers, ILogMessageDistributionManagement
        {
            /// <summary>Returns true if this distribution instance has been shutdown</summary>
            bool HasBeenShutdown { get; }
        }

        /// <summary>This interface defines the LogMessageDistribution methods that are used by normal Logger objects to generate and emit messages for distribution.</summary>
        public interface ILogMessageDistributionForLoggers
		{
            /// <summary>used to create/lookup the LoggerSourceID for a given logger name, allowUseOfModularConfig can be given as false to suppress use of modular config keys Logging.Loggers.[loggerName].LogGate.[Increase|Reduce] as source for additional LogGate value.</summary>
            LoggerSourceInfo GetLoggerSourceInfo(string name, bool allowUseOfModularConfig = true);

            /// <summary>used by loggers to update their distribution group name</summary>
            void SetLoggerDistributionGroupName(int loggerID, string groupName);

            /// <summary>this method is fully renterant.  Once the allocated message has been given to the distribution system, the caller must make no further use of or reference to it.</summary>
            [Obsolete("The use of this method is obsolete. (2016-12-22)")]
            LogMessage GetLogMessage();

            /// <summary>used to distribute a message from the loggerID listed as the message's source.  This method consumes the given message.  The caller's handle will be nulled by this method.</summary>
            void DistributeMessage(ref LogMessage lm);

            /// <summary>used by loggers to block until its last emitted message has been fully delivered or the time limit is reached (this method is renterant and supports multiple simultanious callers)</summary>
            bool WaitForDistributionComplete(int loggerID, TimeSpan timeLimit);
        };

        /// <summary>This interface defines the LogMessageDistribution methods that are used by QueuedLogger objects to generate and emit messages for distribution.</summary>
        public interface ILogMessageDistributionForQueuedLoggers
        {
            /// <summary>request distribution to start support for queued logging</summary>
            void StartQueuedMessageDeliveryIfNeeded();

            /// <summary>used by queued loggers to request that it block until its last emitted message has been fully delivered or the time limit is reached (this method is renterant and supports multiple simultanious callers)</summary>
            bool WaitForQueuedMessageDistributionComplete(int loggerID, TimeSpan timeLimit);

            /// <summary>used by queued loggers.  This method consumes the given message.  The caller's handle will be nulled by this method.</summary>
            void EnqueueMessageForDistribution(ref LogMessage lm);
        }

        /// <summary>This interface defines the LogMessageDistribution methods that are used to manage message distribution</summary>
        public interface ILogMessageDistributionManagement
        {
            /// <summary>Initializes and starts distribution, if needed.</summary>
            void StartupIfNeeded(string callerName);

            /// <summary>Stops distribution in an orderly manner.  Attempts to distributed messages will be ignored here after.</summary>
            void Shutdown(string callerName);

            /// <summary>request distribution to stop support for queued logging</summary>
            [Obsolete("This method will be removed from this public interface.  (2016-12-22)")]
            void StopQueuedMessageDelivery();

            /// <summary>Adds the given logMessageHandler to the given groupName (or the default group if no non-empty group name is given)</summary>
            void AddLogMessageHandlerToDistributionGroup(ILogMessageHandler logMessageHandler, string groupName = null);

            /// <summary>Adds the given set of ILogMessageHandler instances to the given groupName (or the default group if no non-empty group name is given)</summary>
            void AddLogMessageHandlersToDistributionGroup(IEnumerable<ILogMessageHandler> logMessageHandlerSet, string groupName = null);

            /// <summary>Replaces the indicated group's name matching rules with the given one</summary>
            void MapLoggersToDistributionGroup(LoggerNameMatchType matchType, string matchStr, string groupName);

            /// <summary>Replaces (or appends) the indicated group's name matching rules with the given matchRuleSet.</summary>
            void MapLoggersToDistributionGroup(MatchRuleSet matchRuleSet, string groupName, bool appendRules = false);

            /// <summary>Sets the indicated group's LogGate to the given value</summary>
            void SetDistributionGroupGate(string groupName, LogGate logGate);

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
            void LinkDistributionToGroup(string fromGroupName, string linkToGroupName);

            /// <summary>
            /// This method is used to change the <paramref name="groupSettings"/> that are applied to the given <paramref name="groupName"/>
            /// <para/>Note: the use of this method is generally expected to remove the use of some of the older individual settings update methods such as SetDistributionGroupGate.
            /// </summary>
            void UpdateGroupSettings(string groupName, GroupSettings groupSettings);
        }

	    /// <summary>
	    /// This struct contains the user changable set of values that can be used to adjust the behavior of a logging group.  
        /// <para/>Note: these settings do not currently include means to change the set of LMH instances that are attached to the group or to change the set of other groups that are linked to this group.
	    /// </summary>
        public struct GroupSettings
        {
            /// <summary>
            /// Defines the LogGate to be used with this group.
            /// </summary>
            public LogGate? LogGate { get; set; }

            /// <summary>
            /// Defines the GroupLinkageBehavior that is selected for this group.
            /// </summary>
            public GroupLinkageBehavior? GroupLinkageBehavior { get; set; }

            /// <summary>Debugging and logging helper method</summary>
            public override string ToString()
            {
                return "gate:{0} linkageBehavior:{1}".CheckedFormat(LogGate, GroupLinkageBehavior);
            }
        }

        /// <summary>
        /// This flag enumeration contains a set of user available behavior options that may be selected for use with group linkage
        /// <para/>None (0x00), IncludeLinkedLMHInstancesInGroupGateEvaluation (0x01)
        /// </summary>
        [Flags]
        public enum GroupLinkageBehavior : int
        {
            /// <summary>Placeholder default.  No non-default settings are selected.  [0x00]</summary>
            None = 0x00,

            /// <summary>Selects that the link from's group effective distribution gate level will be elevated to include the active gate levels of the linked LogMessageHandler instances whenever distribution gate levels are (re)evaluated.  [0x01]</summary>
            IncludeLinkedLMHInstancesInGroupGateEvaluation = 0x01,
        }

		#endregion

		#region LogMessageDistribution class

		public class LogMessageDistribution : Utils.DisposableBase, ILogMessageDistribution
		{
            #region Singleton Instance

            private static readonly SingletonHelperBase<ILogMessageDistribution> instanceHelper = new SingletonHelperBase<ILogMessageDistribution>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new LogMessageDistribution());
            public static ILogMessageDistribution Instance { get { return instanceHelper.Instance; } set { instanceHelper.Instance = value; } }

            #endregion

			#region Construction

            /// <summary>
            /// Constructor.  Will use class name as instance name if no non-empty instanceName is explicitly given.
            /// </summary>
			public LogMessageDistribution(string instanceName = null)
			{
                instanceName = instanceName.MapNullOrEmptyTo(Fcns.CurrentClassLeafName);

                distSourceID = new LoggerSourceInfo(LoggerID_InternalLogger, instanceName);

				// define the default distribution group.  Its name is the name that the distribution uses if
				//	it is trying to emit a message.

				const int defaultDistGroupID = DistGroupID_Default;

                if (defaultDistGroupIDInfo == null)     // this is done only once.
                {
                    defaultDistGroupIDInfo = new PerDistGroupIDInfo(distGroupIDInfoList.Count, DefaultDistributionGroupName);

                    distGroupIDInfoList.Add(defaultDistGroupIDInfo);
                    distGroupIDInfoArray = distGroupIDInfoList.ToArray();

                    distGroupNameToIDMap.Add(defaultDistGroupIDInfo.DistGroupName, defaultDistGroupIDInfo.DistGroupID);
                }

                if (!Object.ReferenceEquals(distGroupIDInfoArray.SafeAccess(defaultDistGroupID), defaultDistGroupIDInfo))
                    Asserts.TakeBreakpointAfterFault("DistGroupID_Default is not valid after initializing Distribution Group table.");

				// create the loggerID used for messages that come from the distribution system
                // (block use of ModularConfig for this logger instance in order to avoid recursive use of the LogMessageDistribution singleton).

                PerLoggerIDInfo plInfo = InnerCreateLoggerInfo(instanceName, loggersLogGateReduceICKA: null, loggersLogGateIncreaseICKA: null, initialPendingLoggersReduceLogGateValue: LogGate.All.MaskBits, initialPendingLoggersIncreaseLogGateValue: LogGate.None.MaskBits);

                if (plInfo != null)
                {
                    plInfo.SpecifiedDistGroupName = DefaultDistributionGroupName;
                    distSourceID = plInfo.sourceID;
                }
                else
                {
                    // we cannot use normal logging here since it has not been setup yet, and there are no LMH objects
                    Utils.Asserts.TakeBreakpointAfterFault("A valid DistSourceID could not be created");
                }

                // setup our explicit dispose action
                AddExplicitDisposeAction(() => { Shutdown("Dispose"); });
			}

			#endregion

            #region instance variables and related types: PerLoggerIDInfo, DistHandlerInfo, PerDistGroupIDInfo

            const int MesgQueueSize = 1000;

            /// <summary>sourceID for messages that originate in this distribution system.</summary>
            LoggerSourceInfo distSourceID = null;

            /// <summary>Mutex object for access to distribution system's internals.</summary>
            private readonly object distMutex = new object();

            /// <summary>volatile to support testing outside of lock ownership.  changes are still done inside lock.</summary>
            private volatile bool shutdown = false;

            /// <summary>this is only used within the ownership of the mutex so it does not need to be interlocked</summary>
            Utils.SequenceNumberInt mesgDistributionSeqGen = new MosaicLib.Utils.SequenceNumberInt(0, true);

            Utils.WaitEventNotifier mesgDeliveryOccurredNotification = new MosaicLib.Utils.WaitEventNotifier(MosaicLib.Utils.WaitEventNotifier.Behavior.WakeAllSticky);

            class PerLoggerIDInfo
            {
                public PerLoggerIDInfo(int loggerID)
                {
                    LoggerID = loggerID;
                    seqLoggerConfigSource = new SequencedLoggerConfigSource(GenerateCurrentLoggerConfig());
                }

                public override string ToString()
                {
                    string loggerName = (sourceID != null) ? sourceID.Name : "[NullSourceID]";

                    if (SpecifiedDistGroupName == distGroupConfig.GroupName)
                        return "PerLoggerIDInfo '{0}' id:{1} grp:{2}".CheckedFormat(loggerName, LoggerID, SpecifiedDistGroupName);
                    else
                        return "PerLoggerIDInfo '{0}' id:{1} grp:{2} mappedTo:{3}".CheckedFormat(loggerName, LoggerID, SpecifiedDistGroupName, distGroupConfig.GroupName);
                }

                public int LoggerID { get; private set; }
                public int distGroupID = DistGroupID_Default;

                public IConfigKeyAccess loggersReduceLogGateICKA = null;
                public IConfigKeyAccess loggersIncreaseLogGateICKA = null;
                public int pendingLoggersReduceLogGateValueFromICKA = LogGate.All.MaskBits;        // int is used because this field is updated asynchronously.  Use of ref prevents declaring this as volatile.
                public int pendingLoggersIncreaseLogGateValueFromICKA = LogGate.None.MaskBits;     // int is used because this field is updated asynchronously.  Use of ref prevents declaring this as volatile.
                public volatile bool havePendingLogGateChanges = false;

                public LogGate loggersReduceLogGateFromModularConfig = LogGate.All;
                public LogGate loggersIncreaseLogGateFromModularConfig = LogGate.None;

                private LoggerConfig distGroupConfig = LoggerConfig.None;
                private bool disabled;

                public SequencedLoggerConfigSource seqLoggerConfigSource = null;
                public LoggerSourceInfo sourceID = null;							// contains Logger Name and ID. - only filled in after construction

                public int lastDistributedMesgSeqNum = 0;

                // non-empty if the logger specifically identified a target group.
                public string SpecifiedDistGroupName { get; set; }

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

                internal void UpdateAndPublishLoggerConfig()
                {
                    seqLoggerConfigSource.LoggerConfig = GenerateCurrentLoggerConfig();
                }

                private LoggerConfig GenerateCurrentLoggerConfig()
                {
                    LoggerConfig loggerConfig = new LoggerConfig((!disabled)
                                                                    ? (distGroupConfig & loggersReduceLogGateFromModularConfig)
                                                                    : (new LoggerConfig(distGroupConfig) { LogGate = LogGate.None })
                                                                    ) 
                                                                { 
                                                                    LogGateIncrease = loggersIncreaseLogGateFromModularConfig 
                                                                };
                    return loggerConfig;
                }
            }

            List<PerLoggerIDInfo> perLoggerIDInfoList = new List<PerLoggerIDInfo>();	// index by loggerID
            PerLoggerIDInfo[] perLoggerIDInfoArray = Utils.Collections.EmptyArrayFactory<PerLoggerIDInfo>.Instance;      // index by loggerID - always stays synchronized with perLoggerIDInfoList contents
            Dictionary<string, int> loggerNameToIDMap = new Dictionary<string, int>();

            class DistHandlerInfo
            {
                public override string ToString()
                {
                    string lmhName = (lmh != null) ? lmh.Name : "[LMHIsNull]";
                    return "DistHandlerInfo '{0}'".CheckedFormat(lmhName);
                }

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
                public override string ToString()
                {
                    string linkedGroups = string.Join(",", linkedToDistGroupList.Select(pdg => pdg.DistGroupName).ToArray());
                    string linkedLMHs = string.Join(",", distHandlerInfoList.Select(dhi => dhi.LMH.Name).ToArray());
                    return "PerDistGroupIDInfo '{0}' id:{1} linkedGroups:{2} lmhs:{3} linkageBehavior:{4}".CheckedFormat(DistGroupName, DistGroupID, linkedGroups, linkedLMHs, GroupLinkageBehavior);
                }

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

                public GroupLinkageBehavior GroupLinkageBehavior { get; set; }

                /// <summary>stores the current LoggerConfig setting for this group.</summary>
                private LoggerConfig groupLoggerConfigSetting = LoggerConfig.All;

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

                /// <summary>This is the list of DistHandlerInfo objects for the log message handlers that will recieve messages that are distributed to/through this group.</summary>
                public IListWithCachedArray<DistHandlerInfo> distHandlerInfoList = new IListWithCachedArray<DistHandlerInfo>();

                /// <summary>
                /// This is the extended list of DistHandlerInfo objects for the log message handlers that will recieve messages that are distributed to/through this group.
                /// It includes both the directly linked handlers and the indirectly linked handlers that 
                /// </summary>
                public IListWithCachedArray<DistHandlerInfo> expandedDistHandlerInfoList = new IListWithCachedArray<DistHandlerInfo>();

                /// <summary>
                /// This field is set whenever a new lmh is added to the expanded lmh list.  It is cleared when the group next re-evaluates its gate level.
                /// </summary>
                public bool expandedDistHandlerInfoListChanged = false;

                /// <summary>This is the array of info objects for the log message handlers that will recieve messages that are distributed to/through this group.</summary>
                public DistHandlerInfo[] expandedDistHandlerInfoArray = Utils.Collections.EmptyArrayFactory<DistHandlerInfo>.Instance;

                /// <summary>
                /// Used to add a new log message handler to this group's distribution list.
                /// This also adds the lmh to the expanded lmh list (if needed) for all of the groups that are linked to this group.
                /// </summary>
                public void AddDistHandlerInfo(DistHandlerInfo dhInfo) 
                {
                    distHandlerInfoList.Add(dhInfo);

                    AddExpandedDistHandlerInfo(dhInfo);

                    loggerConfigWrittenNotification.Notify();
                }

                public void AddExpandedDistHandlerInfo(DistHandlerInfo dhInfo)
                {
                    // add the given dhInfo to the local list first
                    if (!expandedDistHandlerInfoList.Contains(dhInfo))
                    {
                        expandedDistHandlerInfoList.Add(dhInfo);
                        expandedDistHandlerInfoArray = expandedDistHandlerInfoList.Array;
                        expandedDistHandlerInfoListChanged = true;
                    }

                    // then recursively add the given dhInfo to all of the expandedDistHandlerInfoLists of all of the groups that are linked to this one
                    //  recursive search ends with any such group that already has this lmh in its expanded list (thus preventing indefinite recursion.
                    foreach (var linkedFromDGInfo in linkedFromDistGroupList.Array)
                    {
                        if (!linkedFromDGInfo.expandedDistHandlerInfoList.Contains(dhInfo))
                            linkedFromDGInfo.AddExpandedDistHandlerInfo(dhInfo);
                    }
                }

                /// <summary>list of linked groups to which messages accepted by this group will also be delivered.  This group is always the first item in this list.</summary>
                public IListWithCachedArray<PerDistGroupIDInfo> linkedToDistGroupList = new IListWithCachedArray<PerDistGroupIDInfo>();

                /// <summary>list of linked groups from which messages accepted by this group will also be delivered.</summary>
                public IListWithCachedArray<PerDistGroupIDInfo> linkedFromDistGroupList = new IListWithCachedArray<PerDistGroupIDInfo>();

                /// <summary>
                /// Adds the given target group ID to this groups linked dist list if needed.  Returns true if the given ID was not already in the list and was added.  Returns false if the given ID was already in the list.
                /// This method also adds all of the extendedDistHandlerInfo items from the linkToDistGroupIDInfo's expandedDistHandlerInfoArray to this group's expandedDistHandlerInfoList if they are not already included.
                /// </summary>
                public bool AddLinkTo(PerDistGroupIDInfo linkToDistGroupIDInfo)
                {
                    linkToDistGroupIDInfo.linkedFromDistGroupList.SafeAddIfNeeded(this);

                    if (!linkedToDistGroupList.Contains(linkToDistGroupIDInfo))
                    {
                        linkedToDistGroupList.Add(linkToDistGroupIDInfo);

                        if (this != linkToDistGroupIDInfo)
                        {
                            foreach (var extendedDHInfo in linkToDistGroupIDInfo.expandedDistHandlerInfoArray)
                                AddExpandedDistHandlerInfo(extendedDHInfo);
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                /// <summary>sequence number used to record when logging config related changes have been made that might require the cached gates to be re-evaluated.</summary>
                private Utils.InterlockedSequenceNumberInt loggerConfigWrittenNotification = new MosaicLib.Utils.InterlockedSequenceNumberInt(); 	// used as INotifyable

                /// <summary>get only public property version of the loggerConfigWrittenNotification object as a simple INotifyable.</summary>
                public Utils.INotifyable LoggerConfigWrittenNotification { get { return loggerConfigWrittenNotification; } }
 
                /// <summary>SequenceNumberObserver that looks at the loggerConfigWrittenNotification.</summary>
                public Utils.SequenceNumberObserver<int> loggerConfigWrittenObserver;

                /// <summary>the logical "and" of the logGateSetting and distHandlerLogGate</summary>
                private LoggerConfig activeLoggerConfig = LoggerConfig.None;

                /// <summary>
                /// This MatchRuleSet defines the rules that are applied to the set of Lookup loggers to map them to use this group.
                /// </summary>
                public MatchRuleSet loggerNameMatchRuleSet = MatchRuleSet.None;

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

                    distHandlerLoggerConfigOr.GroupName = DistGroupName;

                    bool includeLinkedLMHInstancesInGroupGateEvaluation = (GroupLinkageBehavior & GroupLinkageBehavior.IncludeLinkedLMHInstancesInGroupGateEvaluation) != 0;

                    foreach (DistHandlerInfo dhInfo in (!includeLinkedLMHInstancesInGroupGateEvaluation ? distHandlerInfoList.Array : expandedDistHandlerInfoArray))
                        distHandlerLoggerConfigOr |= dhInfo.LoggerConfig;

                    expandedDistHandlerInfoListChanged = false;

                    LoggerConfig updatedLoggerConfig = distHandlerLoggerConfigOr & groupLoggerConfigSetting;
                    if (Disabled)
                        updatedLoggerConfig.LogGate = LogGate.None;

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

            PerDistGroupIDInfo defaultDistGroupIDInfo = null;
            List<PerDistGroupIDInfo> distGroupIDInfoList = new List<PerDistGroupIDInfo>();			// index by group id
            PerDistGroupIDInfo[] distGroupIDInfoArray = Utils.Collections.EmptyArrayFactory<PerDistGroupIDInfo>.Instance;
            Dictionary<string, int> distGroupNameToIDMap = new Dictionary<string, int>();

            // information used to implement queued logging.

            volatile MessageQueue mesgQueue = null;
            SequencedLoggerConfigSource mesgQueueLoggerGateSource = new SequencedLoggerConfigSource(LoggerConfig.All);
            System.Threading.Thread mesgQueueDistThread = null;
            Utils.WaitEventNotifier mesgQueueDistThreadWakeupNotification = new MosaicLib.Utils.WaitEventNotifier(MosaicLib.Utils.WaitEventNotifier.Behavior.WakeAllSticky);

            #endregion

            #region IConfig related parts

            /// <summary>field that gives the configInstance that is used to obtain ICKA values for reduce/increase config keys.</summary>
            private volatile IConfig configInstance = null;

            /// <summary>
            /// This is the method that is used as a delegate to field Config ChangeNotification notifications.
            /// When this callback is triggered, it simply sets the serviceICKAUpdatesHoldoffCounter to the preconfigured serviceICKAUpdatesNotifyCountValue
            /// so that the next -n DistributeMessage calls will call ServiceICKAUpdatesForPerLoggerIDInfoArray to handle possible ICKA value changes.
            /// </summary>
            void ChangeNotificationList_OnNotify()
            {
                serviceICKAUpdatesHoldoffCounter = serviceICKAUpdatesNotifyCountValue;
            }

            /// <summary>
            /// The following counter is incremented non-atomically and outside of any locks.  Since this counter is only used as a heuristic this is fine.
            /// </summary>
            private volatile int serviceICKAUpdatesHoldoffCounter = 0;
            private const int serviceICKAUpdatesRetriggerCountValue = 20;      // recheck for config updates every 100 times DistributeMessage is called
            private const int serviceICKAUpdatesNotifyCountValue = -100;        // each time ChangeNotificationList_OnNotify, the next 100 DistributeMessage calls will also call ServiceICKAUpdatesForPerLoggerIDInfoArray

            private void UpdateLogGateFromPendingConfigValues(PerLoggerIDInfo plInfo)
            {
                if (plInfo.havePendingLogGateChanges)
                {
                    plInfo.havePendingLogGateChanges = false;

                    LoggerConfig entryLoggerConfig = plInfo.seqLoggerConfigSource.LoggerConfig;
                    LogGate entryReduce = plInfo.loggersReduceLogGateFromModularConfig;
                    LogGate entryIncrease = plInfo.loggersIncreaseLogGateFromModularConfig;

                    plInfo.loggersReduceLogGateFromModularConfig = new LogGate() { MaskBits = plInfo.pendingLoggersReduceLogGateValueFromICKA };
                    plInfo.loggersIncreaseLogGateFromModularConfig = new LogGate() { MaskBits = plInfo.pendingLoggersIncreaseLogGateValueFromICKA };

                    plInfo.UpdateAndPublishLoggerConfig();

                    if (entryReduce != plInfo.loggersReduceLogGateFromModularConfig || entryIncrease != plInfo.loggersIncreaseLogGateFromModularConfig || !entryLoggerConfig.Equals(plInfo.seqLoggerConfigSource.LoggerConfig))
                        InnerLogLog(MesgType.Debug, "Logger: {0} id:{1} reduce/increase levels changed to {2}/{3} {4} [from {5}/{6} {7}]", plInfo.sourceID.Name, plInfo.sourceID.ID, plInfo.loggersReduceLogGateFromModularConfig.ToString(true), plInfo.loggersIncreaseLogGateFromModularConfig.ToString(true), plInfo.seqLoggerConfigSource.LoggerConfig, entryReduce.ToString(true), entryIncrease.ToString(true), entryLoggerConfig);
                }
            }

            public static bool UpdateICKAAndGetLogGateValueAsIntMaskBits(IConfigKeyAccess icka, ref int value, LogGate fallbackValue, bool firstUpdate = false)
            {
                LogGate logGate = new LogGate() { MaskBits = value };

                if (icka != null && (icka.IsUpdateNeeded || firstUpdate))
                    logGate = icka.UpdateValueInline().GetValue<Logging.LogGate>(defaultValue: fallbackValue);

                if (value == logGate.MaskBits)
                    return false;

                value = logGate.MaskBits;

                return true;
            }

            AtomicInt32 serviceExclusionAtomicInt = new AtomicInt32();
            volatile int serviceConfigChangeSeqNum = 0;

            private bool ServiceICKAUpdatesForPerLoggerIDInfoArray()
            {
                IConfig entryConfigInstance = configInstance;
                int entryConfigChangeSeqNum = (entryConfigInstance != null) ? entryConfigInstance.ChangeSeqNum : 0;

                if (entryConfigChangeSeqNum == serviceConfigChangeSeqNum)
                    return false;

                // now verify that only one thread actually does the sweep through the array at a time.  The other threads just skip over this.
                try
                {
                    if (serviceExclusionAtomicInt.Increment() != 1)
                        return false;

                    PerLoggerIDInfo[] capturedPerLoggerIDInfoArray = perLoggerIDInfoArray;

                    if (capturedPerLoggerIDInfoArray == null)
                        return false;

                    {
                        serviceConfigChangeSeqNum = entryConfigChangeSeqNum;

                        foreach (var plInfo in capturedPerLoggerIDInfoArray)
                        {
                            plInfo.havePendingLogGateChanges |= UpdateICKAAndGetLogGateValueAsIntMaskBits(plInfo.loggersReduceLogGateICKA, ref plInfo.pendingLoggersReduceLogGateValueFromICKA, LogGate.All);
                            plInfo.havePendingLogGateChanges |= UpdateICKAAndGetLogGateValueAsIntMaskBits(plInfo.loggersIncreaseLogGateICKA, ref plInfo.pendingLoggersIncreaseLogGateValueFromICKA, LogGate.None);
                        }

                        return true;
                    }
                }
                finally
                {
                    serviceExclusionAtomicInt.Decrement();
                }
            }

            #endregion

            #region ILogMessageDistribution methods

            /// <summary>Returns true if this distribution instance has been shutdown</summary>
            public bool HasBeenShutdown { get { return shutdown; } }
            
            #endregion

            #region ILogMessageDistributionForLoggers methods

            /// <summary>
            /// used to create/lookup the LoggerSourceInfo for a given <paramref name="loggerName"/>.
            /// <paramref name="allowUseOfModularConfig"/> can be given as false to suppress use of modular config keys Logging.Loggers.[loggerName].LogGate.[Increase|Reduce] as source for additional LogGate value.
            /// </summary>
            public LoggerSourceInfo GetLoggerSourceInfo(string loggerName, bool allowUseOfModularConfig = true)
            {
                // The first lock is used when the loggerName is already known.  This obtains and returns the LoggerSourceInfo for this logger.
                lock (distMutex)
                {
                    PerLoggerIDInfo plInfo = InnerFindLoggerInfo(loggerName);

                    if (plInfo != null)
                        return plInfo.sourceID ?? LoggerSourceInfo.Empty;

                    if (allowUseOfModularConfig)
                        configInstance = configInstance ?? Config.Instance;
                }

                // Next we will attempt to create a new logger source.  If allowUseOfModularConfig is true then we will attempt to lookup the Reduce and Increase keys for each new logger
                //   for use in dynamically adjusting the logger levels.  
                //   NOTE: obtaining these keys can trigger generation of more log messages.  As such these keys are obtained WITHOUT owning the distribution Mutex.

                IConfigKeyAccess loggersLogGateReduceICKA = null;
                IConfigKeyAccess loggersLogGateIncreaseICKA = null;
                int initialPendingLoggersReduceLogGateValue = LogGate.All.MaskBits;
                int initialPendingLoggersIncreaseLogGateValue = LogGate.None.MaskBits;

                /// If allowUseOfModularConfig is true then the method will also attempt to extract a log gate using the ConfigKey Logging.Loggers.[loggerName].LogGate.[Increase|Reduce]
                /// Passing false prevents use of modular config.

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

                    UpdateICKAAndGetLogGateValueAsIntMaskBits(loggersLogGateReduceICKA, ref initialPendingLoggersReduceLogGateValue, LogGate.All, firstUpdate: true);
                    UpdateICKAAndGetLogGateValueAsIntMaskBits(loggersLogGateIncreaseICKA, ref initialPendingLoggersIncreaseLogGateValue, LogGate.None, firstUpdate: true);
                }

                // lock and check if the logger info has been created and if not then create the per logger info for the given name and then return its LoggerSourceInfo.
                lock (distMutex)
                {
                    PerLoggerIDInfo plInfo = InnerFindLoggerInfo(loggerName) 
                        ?? InnerCreateLoggerInfo(loggerName, loggersLogGateReduceICKA, loggersLogGateIncreaseICKA, initialPendingLoggersReduceLogGateValue, initialPendingLoggersIncreaseLogGateValue);

                    if (plInfo != null)
                        return plInfo.sourceID ?? LoggerSourceInfo.Empty;

                    InnerLogLog(MesgType.Warning, "{0}: could not obtain LoggerSourceInfo for '{1}'", Fcns.CurrentMethodName, loggerName);

                    return LoggerSourceInfo.Empty;
                }
            }

            /// <summary>used to update a logger's distribution group name</summary>
            public void SetLoggerDistributionGroupName(int lid, string groupName)
			{
                if (groupName == Logging.DefaultDistributionGroupName || groupName == null)
                    groupName = string.Empty;

				lock (distMutex)
				{
                    if (shutdown)
                        return;

                    PerLoggerIDInfo plInfo = perLoggerIDInfoArray.SafeAccess(lid);

                    if (plInfo != null && plInfo.SpecifiedDistGroupName != groupName)
					{
                        plInfo.SpecifiedDistGroupName = groupName;

						// update the distGroupID from the groupName.  Use DistGroupID_Default if the groupName is null or empty
						//	lookup the groupID (and create if necessary) if the groupName is a non-empty string.

                        plInfo.distGroupID = (groupName.IsNullOrEmpty() ? DistGroupID_Default : InnerGetDistGroupID(groupName, true));

                        // record and inform the client that the DistGroup LoggerConfig has been changed

                        PerDistGroupIDInfo dgInfo = distGroupIDInfoArray.SafeAccess(plInfo.distGroupID);
                        plInfo.DistGroupConfig = (dgInfo != null) ? dgInfo.GroupLoggerConfigSetting : LoggerConfig.None;

                        // remap loggers back to target group based on name if needed.
                        InnerRemapLoggersToDistGroups();
					}
				}
			}

            /// <summary>this method is fully renterant.  Once the allocated message has been given to the distribution system, the caller must make no further use of or reference to it.</summary>
            [Obsolete("The use of this method is obsolete. (2016-12-22)")]
            public LogMessage GetLogMessage()
			{
				return InnerGetLogMessage(true);
			}

            /// <summary>used to distribute a message from the loggerID listed as the message's source.  This method consumes the given message.  The caller's handle will be nulled by this method.</summary>
            public void DistributeMessage(ref LogMessage lm)
			{
                // Implement asynchronous serviceICKAUpdatesHoldoffCounter increment and (approximate) bounds checking logic.  
                // Generally we want to call ServiceICKAUpdatesForPerLoggerIDInfoArray for the first -n calls after ChangeNotifcationList_OnNotify has been called,
                //  and approximately every n DistributeMessages calls thereafter.
                //  ServiceICKAUpdatesForPerLoggerIDInfoArray is implemented in a heuristically re-enterant manner.

                bool didUpdateICKAs = false;

                if (serviceICKAUpdatesHoldoffCounter++ <= 0)
                {
                    didUpdateICKAs = ServiceICKAUpdatesForPerLoggerIDInfoArray();
                }
                else if (serviceICKAUpdatesHoldoffCounter >= serviceICKAUpdatesRetriggerCountValue)
                {
                    serviceICKAUpdatesHoldoffCounter = 0;
                    didUpdateICKAs = ServiceICKAUpdatesForPerLoggerIDInfoArray();
                }

				lock (distMutex)
				{
					if (lm == null || shutdown)
						return;

					lm.SeqNum = mesgDistributionSeqGen.Increment();
					lm.NoteEmitted();

                    if (didUpdateICKAs && perLoggerIDInfoArray != null)
                    {
                        foreach (var plInfo in perLoggerIDInfoArray)
                            UpdateLogGateFromPendingConfigValues(plInfo);
                    }

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
                    PerLoggerIDInfo plInfo = perLoggerIDInfoArray.SafeAccess(lid);

                    if (!shutdown && plInfo != null)
					{
                        testSeqNum = plInfo.lastDistributedMesgSeqNum;
                        dgid = plInfo.distGroupID;

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

            #endregion

            #region ILogMessageDistributionForQueuedLoggers methods

            /// <summary>request distribution to start support for queued logging</summary>
            public void StartQueuedMessageDeliveryIfNeeded()
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
                    StartQueuedMessageDeliveryIfNeeded();
                    capturedMesgQueue = mesgQueue;
                }

				if (lm != null)
				{
					// enqueue the given message and then reset the pointer that was passed to us.
					//	(this method consumes the given message);

                    if (capturedMesgQueue != null)
                        capturedMesgQueue.EnqueueMesg(lm);

                    lm = null;
				}
			}

            #endregion

            #region ILogMessageDistributionManagement methods

            /// <summary>Initializes and starts distribution, if needed.</summary>
            public void StartupIfNeeded(string mesg)
            {
                lock (distMutex)
                {
                    if (!shutdown)
                        return;

                    InnerRestartAllHandlers();

                    shutdown = false;

                    // Enable all of the dist groups (internally will restore their distribution group gates to prior value)
                    foreach (var dgInfo in distGroupIDInfoArray)
                        dgInfo.Disabled = false;

                    // now Enable all of the logger's (internally will restore their distribution group gate to prior value)
                    foreach (var plInfo in perLoggerIDInfoArray)
                        plInfo.Disabled = false;

                    InnerLogLog(MesgType.Debug, mesg);
                }
            }

            /// <summary>Stops distribution in an orderly manner.  Attempts to distributed messages will be ignored here after.</summary>
            public void Shutdown(string mesg)
            {
                // mark that we are shutting down
                lock (distMutex)
                {
                    if (shutdown)
                        return;

                    // now Diable all of the logger's (internally will set their distribution group gate to None)
                    foreach (var plInfo in perLoggerIDInfoArray)
                        plInfo.Disabled = true;

                    // Disable all of the dist groups
                    foreach (var dgInfo in distGroupIDInfoArray)
                        dgInfo.Disabled = true;

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

            /// <summary>Adds the given logMessageHandler to the given groupName (or the default group if no non-empty group name is given)</summary>
            public void AddLogMessageHandlerToDistributionGroup(ILogMessageHandler logMessageHandler, string groupName = null)
            {
                AddLogMessageHandlersToDistributionGroup(new ILogMessageHandler[] { logMessageHandler }, groupName);
            }

            /// <summary>
            /// Adds the given set of ILogMessageHandler instances to the given groupName (or the default group if no non-empty group name is given)
            /// <para/>Note: this method cannot be used to add LMH objects to the LookupDistributionGroupName (which is not an actual group).
            /// </summary>
            public void AddLogMessageHandlersToDistributionGroup(IEnumerable<ILogMessageHandler> logMessageHandlerSet, string groupName = null)
            {
                groupName = groupName.MapNullOrEmptyTo(DefaultDistributionGroupName);

                if (groupName == LookupDistributionGroupName)
                {
                    InnerLogLog(MesgType.Warning, "{0}: Given GroupName '{1}' cannot be used here.", Fcns.CurrentMethodName, groupName);
                    return;
                }

                lock (distMutex)
                {
                    int dgid = InnerGetDistGroupID(groupName, createIfNeeded: true);
                    PerDistGroupIDInfo dgInfo = distGroupIDInfoArray.SafeAccess(dgid);

                    if (dgInfo != null)
                    {
                        foreach (var logMessageHandler in logMessageHandlerSet.Where(lmh => (lmh != null)))
                        {
                            int dhInfoIdx = dgInfo.distHandlerInfoList.Count;
                            dgInfo.AddDistHandlerInfo(new DistHandlerInfo(logMessageHandler));

                            logMessageHandler.NotifyOnCompletedDelivery.AddItem(mesgDeliveryOccurredNotification);

                            if (dgInfo.UpdateActiveLoggerConfig())
                                InnerUpdateLoggerDistGroupConfigForGroup(dgInfo);
                        }
                    }
                    else
                    {
                        InnerLogLog(MesgType.Warning, "{0}: Given GroupName '{1}' is not valid and could not be created.", Fcns.CurrentMethodName, groupName);
                    }
                }
            }

            /// <summary>Replaces the indicated group's name matching rules with the given one</summary>
            public void MapLoggersToDistributionGroup(LoggerNameMatchType matchType, string matchStr, string groupName)
            {
                try
                {
                    switch (matchType)
                    {
                        case LoggerNameMatchType.MatchPrefix:
                            MapLoggersToDistributionGroup(new MatchRuleSet(MatchType.Prefix, matchStr), groupName, appendRules: false);
                            break;

                        case LoggerNameMatchType.MatchSuffix:
                            MapLoggersToDistributionGroup(new MatchRuleSet(MatchType.Suffix, matchStr), groupName, appendRules: false);
                            break;

                        case LoggerNameMatchType.MatchContains:
                            MapLoggersToDistributionGroup(new MatchRuleSet(MatchType.Contains, matchStr), groupName, appendRules: false);
                            break;

                        case LoggerNameMatchType.Regex:
                            MapLoggersToDistributionGroup(new MatchRuleSet(MatchType.Regex, matchStr), groupName, appendRules: false);
                            break;

                        case LoggerNameMatchType.None:
                        default:
                            MapLoggersToDistributionGroup(Utils.StringMatching.MatchRuleSet.None, groupName, appendRules: false);
                            break;
                    }
                }
                catch (System.Exception ex)
                {
                    // cannot use normal logging here since we are in the process of updating the distribution tables when we are here.
                    Utils.Asserts.LogFaultOccurance(Utils.Fcns.CheckedFormat("{0} grp:{1}, matchType:{2}, matchStr:'{3}' failed", Fcns.CurrentMethodName, groupName, matchType, matchStr), ex);
                }
            }

            /// <summary>
            /// Replaces (or appends) the indicated group's name matching rules with the given matchRuleSet.  
            /// This method is ignored if the caller attempts to define the rule set to be used with the default distribution group.
            /// </summary>
            public void MapLoggersToDistributionGroup(Utils.StringMatching.MatchRuleSet matchRuleSet, string groupName, bool appendRules = false)
            {
                lock (distMutex)
                {
                    int dgid = InnerGetDistGroupID(groupName, createIfNeeded: true);
                    PerDistGroupIDInfo dgInfo = distGroupIDInfoArray.SafeAccess(dgid);

                    if (dgInfo != null && dgid != DistGroupID_Default)
                    {
                        if (!appendRules)
                            dgInfo.loggerNameMatchRuleSet.Clear();

                        dgInfo.loggerNameMatchRuleSet.AddRange(matchRuleSet);

                        InnerRemapLoggersToDistGroups();
                    }
                    else
                    {
                        InnerLogLog(MesgType.Warning, "{0}: Given GroupName '{1}' is not valid and could not be created.", Fcns.CurrentMethodName, groupName);
                    }
                }
            }

            /// <summary>Sets the indicated group's LogGate to the given value</summary>
            public void SetDistributionGroupGate(string groupName, LogGate logGate)
            {
                groupName = groupName.MapNullOrEmptyTo(DefaultDistributionGroupName);

                lock (distMutex)
                {
                    int dgid = InnerGetDistGroupID(groupName, createIfNeeded: true);
                    PerDistGroupIDInfo dgInfo = distGroupIDInfoArray.SafeAccess(dgid);

                    if (dgInfo != null)
                    {

                        // read the LoggerConfig (struct), update the LogGate field and then write it back to the PerDistGroupIDInfo so that it can notify observers of the change.
                        LoggerConfig lc = dgInfo.GroupLoggerConfigSetting;
                        lc.LogGate = logGate;
                        dgInfo.GroupLoggerConfigSetting = lc;

                        if (dgInfo.UpdateActiveLoggerConfig())
                            InnerUpdateLoggerDistGroupConfigForGroup(dgInfo);
                    }
                    else
                    {
                        InnerLogLog(MesgType.Warning, "{0}: Given GroupName '{1}' is not valid and could not be created.", Fcns.CurrentMethodName, groupName);
                    }
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
                    int fromDgid = InnerGetDistGroupID(fromGroupName, createIfNeeded: true);
                    int linkToDgid = InnerGetDistGroupID(linkToGroupName, createIfNeeded: true);

                    PerDistGroupIDInfo fromDgInfo = distGroupIDInfoArray.SafeAccess(fromDgid);
                    PerDistGroupIDInfo linkToDgInfo = distGroupIDInfoArray.SafeAccess(linkToDgid);

                    if (fromDgInfo != null && linkToDgInfo != null)
                    {
                        fromDgInfo.AddLinkTo(linkToDgInfo);

                        List<PerDistGroupIDInfo> updateScanList = new List<PerDistGroupIDInfo>();
                        List<PerDistGroupIDInfo> updateList = new List<PerDistGroupIDInfo>();
                        updateScanList.Add(linkToDgInfo);

                        for (int index = 0; index < updateScanList.Count; )
                        {
                            var nextToCheck = updateScanList.SafeAccess(index++);

                            if (nextToCheck == null || !nextToCheck.expandedDistHandlerInfoListChanged)
                                continue;

                            if (nextToCheck.UpdateActiveLoggerConfig())
                                updateList.SafeAddIfNeeded(nextToCheck);

                            nextToCheck.linkedToDistGroupList.Array.DoForEach(linkedToDGInfo => updateScanList.SafeAddIfNeeded(linkedToDGInfo));
                        }

                        updateList.DoForEach(dgInfo => InnerUpdateLoggerDistGroupConfigForGroup(dgInfo));
                    }
                    else
                    {
                        InnerLogLog(MesgType.Warning, "{0}: Given GroupName '{1}' or '{2}' is not valid and/or could not be created.", Fcns.CurrentMethodName, fromGroupName, linkToGroupName);
                    }
                }
            }

            /// <summary>
            /// This method is used to change the <paramref name="groupSettings"/> that are applied to the given <paramref name="groupName"/>
            /// <para/>Note: the use of this method is generally expected to remove the use of some of the older individual settings update methods such as SetDistributionGroupGate.
            /// </summary>
            public void UpdateGroupSettings(string groupName, GroupSettings groupSettings)
            {
                groupName = groupName.MapNullOrEmptyTo(DefaultDistributionGroupName);

                lock (distMutex)
                {
                    int dgid = InnerGetDistGroupID(groupName, createIfNeeded: true);
                    PerDistGroupIDInfo dgInfo = distGroupIDInfoArray.SafeAccess(dgid);

                    if (dgInfo != null)
                    {
                        if (groupSettings.LogGate != null)
                        {
                            // read the LoggerConfig (struct), update the LogGate field and then write it back to the PerDistGroupIDInfo so that it can notify observers of the change.
                            LoggerConfig lc = dgInfo.GroupLoggerConfigSetting;

                            lc.LogGate = groupSettings.LogGate ?? LogGate.All;

                            dgInfo.GroupLoggerConfigSetting = lc;
                        }

                        if (groupSettings.GroupLinkageBehavior != null)
                        {
                            dgInfo.GroupLinkageBehavior = groupSettings.GroupLinkageBehavior ?? GroupLinkageBehavior.None;
                        }

                        if (dgInfo.UpdateActiveLoggerConfig())
                            InnerUpdateLoggerDistGroupConfigForGroup(dgInfo);
                    }
                    else
                    {
                        InnerLogLog(MesgType.Warning, "{0}: Given GroupName '{1}' is not valid and could not be created.", Fcns.CurrentMethodName, groupName);
                    }
                }
            }

            #endregion

            #region inner implementation methods (these require that the mMutex already be held by the caller)

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
                        PerDistGroupIDInfo dgInfo = null;
						LogGate dgGate = LogGate.All;

                        for (int idx = 0; idx < logMesgList.Count; idx++)
                        {
                            LogMessage lm = logMesgList[idx];

                            LoggerSourceInfo lsid = (lm != null) ? lm.LoggerSourceInfo : null;
                            int lid = (lsid != null) ? lsid.ID : LoggerID_Invalid;

                            PerLoggerIDInfo loggerIDInfo = perLoggerIDInfoArray.SafeAccess(lid);
                            int mesgdgid = loggerIDInfo != null ? loggerIDInfo.distGroupID : DistGroupID_Invalid;

                            if (loggerIDInfo != null)
                            {
                                // if we have accumulated some messages in the vector and the new message's distGroupID is not the same as the
                                //	the one(s) for the messages in the vector then distribute the vector contents and empty it so that a new
                                //	vector can be started for the new int value.

                                if (dgid != mesgdgid && sameDistMesgSetList.Count != 0 && dgInfo != null)
                                {
                                    // distribute the sameDistMesgSetList to its dgid.
                                    InnerDistributeMessages(sameDistMesgSetList.ToArray(), dgInfo);
                                    sameDistMesgSetList.Clear();
                                }

                                if (dgid != mesgdgid)
                                {
                                    dgid = mesgdgid;
                                    dgInfo = distGroupIDInfoArray.SafeAccess(dgid);
                                    dgGate = dgInfo.ActiveLoggerConfig.LogGate;
                                }

                                // if the message type is enabled in the dist group then append it to the vector, 
                                //	otherwise release it now (message will not be handled by any handlers)

                                if (dgGate.IsTypeEnabled(lm.MesgType))
                                {
                                    lm.SeqNum = mesgDistributionSeqGen.Increment();

                                    sameDistMesgSetList.Add(lm);
                                }
                            }

                            logMesgList[idx] = null;
                            lm = null;
                        }

						// distribute the last set (if  any)
                        if (sameDistMesgSetList.Count != 0 && dgInfo != null)
						{
							// distribute the sameDistMesgSetList to its dgid (releases the pointers after they have been distributed).
                            InnerDistributeMessages(sameDistMesgSetList.ToArray(), dgInfo);
							sameDistMesgSetList.Clear();
						}
					}
				} while (qEnabled || (qCount != 0 && qDisabledCount < 50));

				if (qCount != 0)
				{
                    /// Cannot use InnerLogLog because we do not want deadlock risk (as distribution may be shutting down internal mesg queue thread)
                    Utils.Asserts.TakeBreakpointAfterFault("MesgQueueDistThreadFcn: Unable to distribute {0} messages during shutdown.".CheckedFormat(qCount));
				}

                if (!(qCount == 0 && !qEnabled))
                {
                    /// Cannot use InnerLogLog because we do not want deadlock risk (as distribution may be shutting down internal mesg queue thread)
                    Utils.Asserts.TakeBreakpointAfterFault("MesgQueueDistThreadFcn: Abnormal exit conditions");
                }
			}

			LogMessage InnerGetLogMessage(bool blockDuringShutdown)
			{
				if (shutdown && blockDuringShutdown)
					return null;

                return new LogMessage();
			}

            /// <summary>
            /// Code to allow the log distribution to be able to generate log messages that it distributes.
            /// </summary>
            protected void InnerLogLog(MesgType mesgType, string fmtStr, params object[] paramsArray)
            {
                InnerLogLog(mesgType, fmtStr.CheckedFormat(paramsArray));
            }

            /// <summary>
            /// Code to allow the log distribution to be able to generate log messages that it distributes.
            /// </summary>
			protected void InnerLogLog(MesgType mesgType, string mesg, bool acquireLock = true)
			{
                using (var l = new ScopedLock(distMutex, acquireLock: acquireLock))
                {
                    LogMessage lm = InnerGetLogMessage(false);

                    if (lm != null)
                    {
                        lm.Setup(distSourceID, mesgType, mesg);

                        lm.SeqNum = mesgDistributionSeqGen.Increment();
                        lm.NoteEmitted();

                        InnerTestAndDistributeMessage(ref lm);
                    }
                }
			}

            /// <summary>
            /// This method attempts to find, and return, the PerLoggerIDInfo for the given <paramref name="loggerName"/>.
            /// </summary>
            private PerLoggerIDInfo InnerFindLoggerInfo(string loggerName)
            {
                int lid = LoggerID_Invalid;

                if (loggerNameToIDMap.TryGetValue(loggerName ?? String.Empty, out lid))
                    return perLoggerIDInfoArray.SafeAccess(lid);

                return null;
            }

            /// <summary>
            /// This method creates a new PerLoggerInfo for the given <paramref name="loggerName"/>.  
            /// </summary>
            private PerLoggerIDInfo InnerCreateLoggerInfo(string loggerName, IConfigKeyAccess loggersLogGateReduceICKA, IConfigKeyAccess loggersLogGateIncreaseICKA, int initialPendingLoggersReduceLogGateValue, int initialPendingLoggersIncreaseLogGateValue)
            {
                // the name was not in the table: need to create a new one
                PerLoggerIDInfo plInfo = new PerLoggerIDInfo(perLoggerIDInfoArray.Length) 
                    {
                        DistGroupConfig = defaultDistGroupIDInfo.ActiveLoggerConfig, // by default we use the default distribution group for a newly created PerLoggerIDInfo object
                        loggersReduceLogGateICKA = loggersLogGateReduceICKA,
                        loggersIncreaseLogGateICKA = loggersLogGateIncreaseICKA,
                        pendingLoggersReduceLogGateValueFromICKA = initialPendingLoggersReduceLogGateValue,
                        pendingLoggersIncreaseLogGateValueFromICKA = initialPendingLoggersIncreaseLogGateValue,
                        havePendingLogGateChanges = true,
                    };

                plInfo.sourceID = new LoggerSourceInfo(plInfo.LoggerID, loggerName, plInfo.seqLoggerConfigSource.LoggerConfigSource);

                UpdateLogGateFromPendingConfigValues(plInfo);

                perLoggerIDInfoList.Add(plInfo);
                perLoggerIDInfoArray = perLoggerIDInfoList.ToArray();       // we expect there to be a significant amount of churn on this as logger IDs get added but that this churn will completely stop after the application reaches steady state.

                loggerNameToIDMap[loggerName.MapNullToEmpty()] = plInfo.LoggerID;

                InnerRemapLoggerToDistGroup(plInfo);

                return plInfo;
            }

            /// <summary>
            /// This method is used each time we have made a Usable IConfigKeyAccess object to read a Logger's LogGate level.
            /// The first time this method is called it will register the desired to be notified each time one or more config keys are changed.
            /// <para/>NOTE: this method is called without owning the distMutex
            /// </summary>
            private void InnerSubscribeToModularConfigIfNeeded()
            {
                if (registerWithModuleConfigAtomicCount.VolatileValue == 0 && registerWithModuleConfigAtomicCount.Increment() == 1)
                {
                    configInstance = configInstance ?? Config.Instance;
                    configInstance.ChangeNotificationList.OnNotify += ChangeNotificationList_OnNotify;
                }
            }

            AtomicInt32 registerWithModuleConfigAtomicCount = new AtomicInt32();

            /// <summary>
            /// Returns true if the given lid is a valid index into the perLoggerIDInfoArray.
            /// </summary>
            bool InnerIsLoggerIDValid(int loggerID) 
            {
                return (loggerID >= 0 && loggerID < perLoggerIDInfoArray.Length); 
            }

            /// <summary>
            /// Finds (and optionally allocates) the DistGroupID for a given groupName.
            /// <para/>If groupName is null or empty, or was not found and is not to be created, then this method returns DistGroupID_Invalid.
            /// <para/>If groupName is DefaultDistributionGroupName then this method returns DistGroupID_Default
            /// <para/>If groupName is LookupDistributionGroupName then this method returns DistGroupID_Default (loggers using lookup group are added to the default group by default)
            /// </summary>
			protected int InnerGetDistGroupID(string groupName, bool createIfNeeded = false)
			{
				int dgid = DistGroupID_Invalid;

                if ((groupName == Logging.DefaultDistributionGroupName) || (groupName == Logging.LookupDistributionGroupName))
                    return DistGroupID_Default;

                if (!groupName.IsNullOrEmpty())
				{
					if (distGroupNameToIDMap.TryGetValue(groupName ?? String.Empty, out dgid) || !createIfNeeded)
                        return (distGroupIDInfoArray.IsSafeIndex(dgid) ? dgid : DistGroupID_Invalid);

					// the name was not in the table: need to create a new one
					dgid = distGroupIDInfoList.Count;

                    distGroupIDInfoList.Add(new PerDistGroupIDInfo(dgid, groupName));
                    distGroupIDInfoArray = distGroupIDInfoList.ToArray();

                    distGroupNameToIDMap.Add(groupName, dgid);
				}

				return dgid;
			}

			protected void InnerUpdateLoggerDistGroupConfigForGroup(int dgid)
			{
                InnerUpdateLoggerDistGroupConfigForGroup(distGroupIDInfoArray.SafeAccess(dgid));
			}

            private void InnerUpdateLoggerDistGroupConfigForGroup(PerDistGroupIDInfo dgInfo)
            {
                if (dgInfo != null)
                {
                    int dgid = dgInfo.DistGroupID;

                    LoggerConfig activeLoggerConfig = dgInfo.ActiveLoggerConfig;

                    // tell all of the loggers about the new value of their distribution groups activeLogGate
                    foreach (var plInfo in perLoggerIDInfoArray)
                    {
                        if (plInfo.distGroupID == dgid)
                            plInfo.DistGroupConfig = activeLoggerConfig;		// this property handles change detection
                    }
                }
            }

            /// <summary>Loop through each logger and determin which distribution group it belongs to (default if none)</summary>
			protected void InnerRemapLoggersToDistGroups()
			{
                foreach (var plInfo in perLoggerIDInfoArray)
                    InnerRemapLoggerToDistGroup(plInfo);
			}

            /// <summary>
            /// Remap the selected loggerID to a target distribution group based on per group logger name matching.  
            /// LoggerName based mapping is supressed if the logger has been assigned to a specific group.
            /// </summary>
            protected void InnerRemapLoggerToDistGroup(int loggerID)
            {
                InnerRemapLoggerToDistGroup(perLoggerIDInfoArray.SafeAccess(loggerID));
            }

            /// <summary>
            /// Remap the selected PerLoggerIDInfo item to a target distribution group based on per group logger name matching.  
            /// LoggerName based mapping is supressed if the logger has been assigned to a specific group.
            /// </summary>
            private void InnerRemapLoggerToDistGroup(PerLoggerIDInfo plInfo)
            {
				// suppress logger name to dgid mapping for loggers that have been assigned a specific distGroupName
                if (plInfo == null || plInfo.SpecifiedDistGroupName != LookupDistributionGroupName)
					return;

				string loggerName = plInfo.sourceID.Name;

                PerDistGroupIDInfo foundDGInfo = defaultDistGroupIDInfo;

				foreach (PerDistGroupIDInfo dgInfo in distGroupIDInfoArray)
				{
                    // skip the default group
                    if (dgInfo.DistGroupID == DistGroupID_Default)
                        continue;

                    // logger uses the first group that explicitly matches its loggerName
                    if (dgInfo.loggerNameMatchRuleSet.MatchesAny(loggerName))
					{
                        foundDGInfo = dgInfo;
                        break;
					}
				}

				plInfo.distGroupID = foundDGInfo.DistGroupID;
				plInfo.DistGroupConfig = foundDGInfo.ActiveLoggerConfig;

                return;
			}

			protected void InnerTestAndDistributeMessage(ref LogMessage lm)
			{
				if (lm != null && lm.LoggerSourceInfo != null && lm.Emitted)
				{
					int lid = lm.LoggerSourceInfo.ID;
                    PerLoggerIDInfo plInfo = perLoggerIDInfoArray.SafeAccess(lid);

                    int dgid = (plInfo != null) ? plInfo.distGroupID : DistGroupID_Invalid;
					
					if (plInfo != null)
						plInfo.lastDistributedMesgSeqNum = lm.SeqNum;

                    PerDistGroupIDInfo dgInfo = distGroupIDInfoArray.SafeAccess(dgid);

                    LoggerConfig dgConfig = (dgInfo != null) ? dgInfo.ActiveLoggerConfig : LoggerConfig.None;

					bool msgEnabled = dgConfig.IsTypeEnabled(lm.MesgType);

                    if (msgEnabled)
                        InnerDistributeMessage(ref lm, dgInfo);		// this will consume the message (and null our pointer)
				}

                lm = null;
			}

            protected void InnerDistributeMessage(ref LogMessage lm, int srcDistGroupID)
            {
                // all validation and testing of passed parameters is performed by caller
                // message ptr is non-null, message type are enabled in group and distGroupID is known valid

                PerDistGroupIDInfo srcDgInfo = distGroupIDInfoArray.SafeAccess(srcDistGroupID);

                InnerDistributeMessage(ref lm, srcDgInfo);
            }

            private void InnerDistributeMessage(ref LogMessage lm, PerDistGroupIDInfo dgInfo)
			{
                // traverse groups to which this group is linked and deliver the message to each group's list of log message handlers.
                if (dgInfo != null)
                {
                    foreach (DistHandlerInfo dhInfo in dgInfo.expandedDistHandlerInfoArray)
                    {
                        if (dhInfo.Valid && dhInfo.LoggerConfig.IsTypeEnabled(lm.MesgType))
                            dhInfo.LMH.HandleLogMessage(lm);
                    }
                }

				// release the message if we still have a handle to it
                lm = null;
			}

            protected void InnerDistributeMessages(LogMessage[] lmArray, int srcDistGroupID)
            {
                // all validation and testing of passed parameters is performed by caller
                // message ptr is non-null, message type are enabled in group and distGroupID is known valid

                PerDistGroupIDInfo srcDgInfo = distGroupIDInfoArray.SafeAccess(srcDistGroupID);

                InnerDistributeMessages(lmArray, srcDgInfo);
            }

            private void InnerDistributeMessages(LogMessage[] lmArray, PerDistGroupIDInfo dgInfo)
			{
                if (dgInfo != null)
                {
                    foreach (DistHandlerInfo dhInfo in dgInfo.expandedDistHandlerInfoArray)
                    {
                        if (dhInfo.Valid)
                            dhInfo.LMH.HandleLogMessages(lmArray);
                    }
                }

				// release each of the messages in the vector if we still have a handle to any of them.

				for (int idx = 0; idx < lmArray.Length; idx++)
				{
					lmArray [idx] = null;
				}
			}

			protected bool InnerCheckIfMessageDeliveryIsStillInProgress(int dgid, int testSeqNum)
			{
                PerDistGroupIDInfo dgInfo = distGroupIDInfoArray.SafeAccess(dgid);

                if (dgInfo != null)
                {
                    foreach (DistHandlerInfo dhInfo in dgInfo.expandedDistHandlerInfoArray)
                    {
                        if (dhInfo.Valid && dhInfo.LMH.IsMessageDeliveryInProgress(testSeqNum))
                            return true;
                    }
                }

				return false;
			}

			protected void InnerFlushAllHandlers()
			{
				// first shutdown each of the logger specific handlers
				foreach (PerDistGroupIDInfo dgInfo in distGroupIDInfoArray)
				{
                    foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList.Array)
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
                    foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList.Array)
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
                    foreach (DistHandlerInfo dhInfo in dgInfo.distHandlerInfoList.Array)
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
		#region LogMessageDistributionImpl singleton public accessor methods (static methods in Logging class/namespace)

		//-------------------------------------------------------------------
		// singleton LogMessageDistribution system and method used to setup log message distribution groups

        /// <summary>Gives the caller access to the ILogMessageDistribution singleton</summary>
        [Obsolete("Please replace use of this method with direct use of LogMessageDistribution.Instance (2016-12-22)")]
		public static ILogMessageDistribution GetLogMessageDistribution()
		{
			return LogMessageDistribution.Instance; 
		}

        /// <summary>Internal method used to get access to the LMD singleton using its implementation type rather than its primary public interface.</summary>
        [Obsolete("Please replace use of this method with direct use of LogMessageDistribution.Instance (2016-12-22)")]
        private static ILogMessageDistribution GetLogMessageDistributionImpl()
		{
			return LogMessageDistribution.Instance; 
		}

        /// <summary>Method allocates a LogMessage from the distribution pool.  This method is generally used only by Loggers</summary>
        /// <returns>A log message to use, allocated from the distribution pool, or null if the distribution engine has been shutdown already.</returns>
        [Obsolete("The use of this method is obsolete. (2016-12-22)")]
        public static LogMessage AllocateLogMessageFromDistribution()		
		{
            if (!LogMessageDistribution.Instance.HasBeenShutdown)
                return new LogMessage();
            else
                return null;
		}

        /// <summary>Internal "constant" that defines the name of the Default Log Distribution Group.  "LDG.Default"</summary>
        private static readonly string defaultDistributionGroupName = "LDG.Default";

        /// <summary>Internal "constant" that defines the name of the Lookup Log Distribution Group.  Loggers placed in this group can be remapped to other groups using the MapLoggersToDistirbutionGroup method.  "LDG.Lookup"</summary>
        private static readonly string lookupDistributionGroupName = "LDG.Lookup";

        /// <summary>This is the default Logging Distribution Group name.  This group is used for all cases where a logger does not otherwise specify a different one and/or it has not been mapped to use another one.  "LDG.Default"</summary>
		public static string DefaultDistributionGroupName { get { return defaultDistributionGroupName; } }

        /// <summary>This is the custom distribution group name that is used to indicate that mapping should be applied to it.  "LDG.Lookup"</summary>
        public static string LookupDistributionGroupName { get { return lookupDistributionGroupName; } }

        /// <summary>Adds the given ILogMessageHandler(s) to the indicated group</summary>
        public static void AddLogMessageHandlerToDistributionGroup(string groupName, params ILogMessageHandler[] logMessageHandlerArray)
		{
            foreach (var lmh in logMessageHandlerArray)
                LogMessageDistribution.Instance.AddLogMessageHandlerToDistributionGroup(lmh, groupName);
		}

        /// <summary>(re)Maps the selected loggers to the given group name.  Loggers must have initially placed themselves in the LookupDistributionGroupName group in order to support being mapped.</summary>
        public static void MapLoggersToDistributionGroup(LoggerNameMatchType matchType, string matchStr, string groupName)
		{
            LogMessageDistribution.Instance.MapLoggersToDistributionGroup(matchType, matchStr, groupName);
		}

        /// <summary>Sets a gate level for messages to a given group so that it may be more restritive than that of the most permissive handler that is attached to the group.</summary>
        public static void SetDistributionGroupGate(string groupName, LogGate logGate)
		{
            LogMessageDistribution.Instance.SetDistributionGroupGate(groupName, logGate);
		}

        /// <summary>Links distribution to one group to also distribute its messages through a second group as well.</summary>
        public static void LinkDistributionToGroup(string fromGroupName, string toGroupName)
        {
            LogMessageDistribution.Instance.LinkDistributionToGroup(fromGroupName, toGroupName);
        }

        /// <summary>Links distribution from a custom group to also distribute its messages through the default group.</summary>
        public static void LinkDistributionToDefaultGroup(string fromGroupName)
        {
            LogMessageDistribution.Instance.LinkDistributionToGroup(fromGroupName, DefaultDistributionGroupName);
        }

        /// <summary>
        /// This method is used to change the <paramref name="groupSettings"/> that are applied to the given <paramref name="groupName"/>
        /// <para/>Note: the use of this method is generally expected to remove the use of some of the older individual settings update methods such as SetDistributionGroupGate.
        /// </summary>
        public static void UpdateGroupSettings(string groupName, GroupSettings groupSettings)
        {
            LogMessageDistribution.Instance.UpdateGroupSettings(groupName, groupSettings);
        }

        /// <summary>Changes the group used by specific source name to a different target group name on a source by source basis.</summary>
        public static void SetDistributionGroupName(string sourceName, string groupName)
		{
            ILogMessageDistribution dist = LogMessageDistribution.Instance;
			LoggerSourceInfo lsInfo = dist.GetLoggerSourceInfo(sourceName);
			if (lsInfo != null)
				dist.SetLoggerDistributionGroupName(lsInfo.ID, groupName);
		}

        /// <summary>Stops the logging distribution system and flushes all buffers.  Normally this should be the last intended use of the Log Distribution System.</summary>
        public static void ShutdownLogging()
		{
            LogMessageDistribution.Instance.Shutdown(Fcns.CurrentMethodName);
		}

        /// <summary>(re)Starts the logging distribution system if it has been shutdown since the last time it was initially constructed or re-started.</summary>
        public static void StartLoggingIfNeeded()
        {
            LogMessageDistribution.Instance.StartupIfNeeded(Fcns.CurrentMethodName);
        }

		//-------------------------------------------------------------------

        /// <summary>Adds the given ILogMessaeHandler(s) to the default distribution group.</summary>
        public static void AddLogMessageHandlerToDefaultDistributionGroup(params ILogMessageHandler [] logMessageHandlerArray)
		{
			AddLogMessageHandlerToDistributionGroup(DefaultDistributionGroupName, logMessageHandlerArray);
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
