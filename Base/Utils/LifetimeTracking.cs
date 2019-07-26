//-------------------------------------------------------------------
/*! @file LifetimeTracking.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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
using System.Reflection;

using MosaicLib.Modular.Common;
using MosaicLib.Utils.Collections;
using MosaicLib.Time;

namespace MosaicLib.Utils
{
    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Helper extension method for use in creating a lifetime tracker object, obtained from the LifetimeTracking singleton.  
        /// This method takes the type of the given <paramref name="methodBase"/> and creates, and returns, an IDisposable lifetime tracker object that can be used to track the lifetime 
        /// of the given method so as to help retain a count of the total number of such method calls and returns.  This is typically done using a using statement.
        /// This method will use the given <paramref name="methodBase"/> to generate the lifetime tracker's group name by combining its Reflected type, method name, 
        /// and any given <paramref name="groupNameSuffix"/> to create the groupName for the created tracker object.  If the <paramref name="useTypeDigestName"/> parameter is true then 
        /// the ReflectedType will use its type digest name in place of its full name.
        /// The mechanisim depends on the caller to explicitly dispose of the returned tracker when the calling object is disposed or is otherwise released.
        /// If the caller fails to explicitly dispose the tracker when the parent object is disposed, then the tracker's finalizer will be used to detect that the tracker is no longer referenced anywhere.  
        /// This mechanism is used to retain a total count of the number of object/active contexts indexed by each of their type digest names (or full names if desired).
        /// </summary>
        public static IDisposable CreateLifetimeTracker(this MethodBase methodBase, string groupNameSuffix = null, bool useTypeDigestName = true)
        {
            string className = (useTypeDigestName ? methodBase.ReflectedType.GetTypeDigestName() : methodBase.ReflectedType.Name);

            string groupName = "{0}.{1}{2}".CheckedFormat(className, methodBase.Name, groupNameSuffix);

            return LifetimeTracking.Instance.CreateTracker(groupName);
        }

        /// <summary>
        /// Helper extension method for use in creating a lifetime tracker object, obtained from the LifetimeTracking singleton.  
        /// This method takes the type of the given <paramref name="target"/> object and creates, and returns, an IDisposable lifetime tracker object that can be used to track the lifetime 
        /// of the given object so as to help retain a count of the total number of such objects.  This method will use the given object to obtain either its type's full class name or type digest name, 
        /// based on the value of the given <paramref name="useTypeDigestName"/> parameter, as the lifetimeTrackingGroupName for this tracker.
        /// The mechanisim depends on the caller to explicitly dispose of the returned tracker when the calling object is disposed or is otherwise released.
        /// If the caller fails to explicitly dispose the tracker when the parent object is disposed, then the tracker's finalizer will be used to detect that the tracker is no longer referenced anywhere.  
        /// This mechanism is used to retain a total count of the number of object/active contexts indexed by each of their type digest names (or full names if desired).
        /// </summary>
        public static IDisposable CreateLifetimeTracker(this object target, bool useTypeDigestName = true)
        {
            Type targetType = (target != null ? target.GetType() : null);

            string groupName = (targetType != null) ? (useTypeDigestName ? targetType.GetTypeDigestName() : targetType.ToString()) : "[NullTargetProxy]";

            return LifetimeTracking.Instance.CreateTracker(groupName);
        }

        /// <summary>
        /// Helper extension method for use in creating a lifetime tracker object, obtained from the LifetimeTracking singleton.  
        /// This method takes a given <paramref name="groupName"/> string and creates, and returns,
        /// an IDisposable lifetime tracker object that can be used to track the lifetime of an object instance, or a code context (such as a using statement).
        /// The mechanisim depends on the caller to explicitly dispose of the returned tracker when the scope of the tracked context is recalaimed.  This is automatically done when using a using statement,
        /// or can be done explicitly by disposing of it.  If the caller fails to explicitly dispose the tracker when the parent object is disposed, then the tracker's finalizer will be used to detect that the tracker is no longer referenced anywhere.  
        /// This mechanism is used to retain a total count of the number of object/active contexts indexed by each of their type digest names (or full names if desired).
        /// </summary>
        public static IDisposable CreateLifetimeTracker(this string groupName)
        {
            return LifetimeTracking.Instance.CreateTracker(groupName);
        }
    }

    /// <summary>
    /// This class is used as a pure singleton (only constructor is private).  
    /// It supports a means for external entities to create IDisposable lifetime tracker objects, indexed by a group name, which the caller disposes of (explicitly or implicitly) to indicate
    /// the end of the lifetime.  This singleton keeps track of these lifetime objects and generates summary statistics about construction and release of each indexed by the corresponding group name.
    /// It provides a Service method that is used to support time delayted active group statistics update log messages an periodic table update messages and a DumpTableToLog method that can be used to
    /// trigger a table dump operation explicitly.
    /// </summary>
	public class LifetimeTracking
    {
        #region static singleton Instance support

        /// <summary>Singleton Instace getter property - actual ObjLifetimeTracking object will be created when the first caller calls this properties getter.</summary>
        public static LifetimeTracking Instance { get { return _singletonHelper.Instance; } }

        private static readonly SingletonHelperBase<LifetimeTracking> _singletonHelper = new SingletonHelperBase<LifetimeTracking>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new LifetimeTracking());

        #endregion

        #region Public Interface (Enable, DefaultDumpEmitter, DefaultGroupConfig, AddRange, GroupConfigItem, CreateTracker, CreateExtraWeight, PeriodicDumpTableInterval, Service, DumpTableToLog)

        /// <summary>
        /// Setting this to false will cause the CreateTracker method to return null and will prevent the Service method from doing anything
        /// </summary>
        public bool Enable { get; set; }

        /// <summary>
        /// This gives the default emitter that is used for timed and explicit table dump operations.
        /// </summary>
        public Logging.IMesgEmitter DefaultDumpEmitter { get; set; }

        /// <summary>
        /// This gives the default group config item that is used to define the extra weight and the activity emitter that is used to report activity for the group (both)
        /// </summary>
        public GroupConfigItem DefaultGroupConfig
        {
            get { return new GroupConfigItem(_defaultGroupConfig, null); }
            set { _defaultGroupConfig = new GroupConfigItem(value ?? emptyGroupConfigItem, null); }       // the copy constructor implements fallback logger mapping.
        }

        /// <summary>
        /// This method is passed a set of one or more kvp pairs of group names and GroupConfigItem instances.  
        /// It clones each of them and adds them to the internal dictionary of group name specific configuration objects.  
        /// If a given item matches a group that already has active trackers then that group's existing group config item instance will be replaced as well.
        /// </summary>
        public LifetimeTracking AddRange(IEnumerable<KeyValuePair<string, GroupConfigItem>> range)
        {
            lock (mutex)
            {
                foreach (var kvp in range)
                {
                    string key = kvp.Key.MapNullToEmpty();
                    var groupConfigItem = new GroupConfigItem(kvp.Value, DefaultGroupConfig);

                    groupConfigItemDictionary[key] = groupConfigItem;

                    var groupTrackingInfo = groupTrackingInfoDictionary.SafeTryGetValue(key);
                    if (groupTrackingInfo != null)
                        groupTrackingInfo.GroupConfigItem = groupConfigItem;
                }
            }

            return this;
        }

        /// <summary>
        /// This object is used to specify group specific configuration of the desired behvaior of this LifetimeTracking singleton.
        /// These objects are constructed and added to the singleton using the AddRange method.
        /// Each such object can be used to specify the InstanceActivityEmitter, the GroupStatisticsEmitter and the ExtraWeight to be used with this group.
        /// The copy constructor that is provided here and which is used when generating copies of caller provided instances will map caller given null values for emitters to
        /// either the given fallbackItem (internally uses the current DefaultGroupConfig) or to the singleton's default emitter choice).
        /// </summary>
        public class GroupConfigItem
        {
            public GroupConfigItem() { }
            public GroupConfigItem(GroupConfigItem other, GroupConfigItem fallbackItem)
            {
                InstanceActivityEmitter = other.InstanceActivityEmitter ?? (fallbackItem != null ? fallbackItem.InstanceActivityEmitter : fallbackLogger.Trace);
                GroupStatisticsEmitter = other.GroupStatisticsEmitter ?? (fallbackItem != null ? fallbackItem.GroupStatisticsEmitter : fallbackLogger.Debug);
                ExtraWeight = other.ExtraWeight;
            }

            /// <summary>When this property is non-null it defines the default activity emitter when reporting instance activity for this item (create/delete).  Use the NullEmitter to suppress activity logging for this group</summary>
            public Logging.IMesgEmitter InstanceActivityEmitter { get; set; }

            /// <summary>When this property is non-null it defines the default recent group statistics emitter when reporting recent activity for this group.  Use the NullEmitter to suppress activity logging for this group</summary>
            public Logging.IMesgEmitter GroupStatisticsEmitter { get; set; }

            /// <summary>When this value is non-zero, it causes the specified amount of extra data to be allocarted and attached to each created lifetime tracking object for this group</summary>
            public int ExtraWeight { get; set; }
        }

        /// <summary>
        /// This method creates and returns a tracker object for the given groupName.
        /// </summary>
        public IDisposable CreateTracker(string groupName)
        {
            if (!Enable)
                return null;

            groupName = groupName.MapNullToEmpty();

            lock (mutex)
            {
                GroupTrackingInfo trackingInfo = groupTrackingInfoDictionary.SafeTryGetValue(groupName);
                if (trackingInfo == null)
                {
                    var groupConfigItem = groupConfigItemDictionary.SafeTryGetValue(groupName);

                    trackingInfo = new GroupTrackingInfo(groupTrackingInfoList.Count, groupName, groupConfigItem);

                    groupTrackingInfoList.Add(trackingInfo);
                    groupTrackingInfoDictionary[groupName] = trackingInfo;
                }

                var tracker = new LifetimeTrackerImpl(trackingInfo.GroupIndex, CreateExtraWeight(trackingInfo.GroupConfigItem), QpcTimeStamp.Now);

                InnerRecordAddedTracker(trackingInfo);

                return tracker;
            }
        }

        private object CreateExtraWeight(GroupConfigItem groupConfigItem)
        {
            return CreateExtraWeight((groupConfigItem ?? DefaultGroupConfig).ExtraWeight);
        }

        /// <summary>
        /// Caller usable static method to create extra weight which is also used internally here.
        /// When the given <paramref name="extraWeight"/> is zero this method returns null.
        /// When the given value is no more than 4096 it returns a single byte array of the given size
        /// Otherwise it returns an array of byte[], each of about 4096 in length so as to reach the requested total size.
        /// If the given value is larger than 4096 * 4096, then this method uses 4096 * 4096 in place of the given value.
        /// </summary>
        public static object CreateExtraWeight(int extraWeight)
        {
            if (extraWeight <= 0)
                return null;

            extraWeight = Math.Min(extraWeight, 4096 * 4096);

            if (extraWeight <= 4096)
                return new byte[extraWeight];

            List<byte[]> extraWeightList = new List<byte[]>();
            while (extraWeight > 4096)
            {
                extraWeightList.Add(new byte[4096]);
                extraWeight -= 4096;
            }

            if (extraWeight > 0)
                extraWeightList.Add(new byte[extraWeight]);

            return extraWeightList.ToArray();
        }

        /// <summary>
        /// Getter returns the current interval for periodic dump table operations, or TimeSpan.Zero if this behavior has been disabled.  Defaults to 5.0 minutes.
        /// Setting this value to zero disables this periodic behavior and setting it to any other value restarts the underlying timer so that the dump table operations
        /// performed by the Service method will generally be spaced at the specified interval.
        /// </summary>
        public TimeSpan PeriodicDumpTableInterval
        {
            get 
            { 
                lock (mutex) 
                { 
                    return periodicDumpTableIntervalTimer.Started ? periodicDumpTableIntervalTimer.TriggerInterval : TimeSpan.Zero; 
                } 
            }
            set 
            {
                lock (mutex)
                {
                    if (!value.IsZero())
                        periodicDumpTableIntervalTimer.Start(value);
                    else
                        periodicDumpTableIntervalTimer.Stop();
                }
            }
        }

        QpcTimer periodicDumpTableIntervalTimer = new QpcTimer() { SelectedBehavior = QpcTimer.Behavior.AutoReset };

        /// <summary>
        /// This method is expected to be called periodically by the client code.  
        /// It supports generation of two sets of log messages.  
        /// The first set are the on-activity with brief delay statistics log messages for each group that has had recent activity.
        /// The second is the periodic dump table operation which is controlled by the PeriodicDumpTableInterval property.
        /// </summary>
        public void Service()
        {
            if (!Enable)
                return;

            if (logPendingCount > 0)
            {
                lock (mutex)
                {
                    logPendingCount = 0;

                    QpcTimeStamp qpcNow = QpcTimeStamp.Now;

                    foreach (var tracker in groupTrackingInfoDictionary.Values)
                    {
                        if (tracker.nextStatusLogTimeStamp.IsZero)
                            continue;

                        if (tracker.nextStatusLogTimeStamp <= qpcNow)
                        {
                            var emitter = (tracker.GroupConfigItem ?? DefaultGroupConfig).GroupStatisticsEmitter;

                            emitter.Emit("{0}", tracker);

                            tracker.nextStatusLogTimeStamp = QpcTimeStamp.Zero;
                        }
                        else
                        {
                            logPendingCount += 1;
                        }
                    }
                }
            }

            if (periodicDumpTableIntervalTimer.IsTriggered)
            {
                DumpTableToLog("{0}.{1}".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }
        }

        /// <summary>
        /// This method generates log messages for each of the groups in the current table and then for the [Global] group.
        /// </summary>
        public void DumpTableToLog(string reason, Logging.IMesgEmitter dumpTableEmitter = null)
        {
            dumpTableEmitter = dumpTableEmitter ?? DefaultDumpEmitter;

            lock (mutex)
            {
                dumpTableEmitter.Emit("{0}: {1} [table size: {2}]", Fcns.CurrentMethodName, reason, groupTrackingInfoDictionary.Count);

                foreach (var tracker in groupTrackingInfoDictionary.Values)
                    dumpTableEmitter.Emit("{0}", tracker);

                dumpTableEmitter.Emit("{0}", globalTrackingInfo);
            }
        }

        #endregion

        #region Constructor (private) and primary internal fields and properties

        private LifetimeTracking()
        {
            DefaultDumpEmitter = fallbackLogger.Debug;
            DefaultGroupConfig = null;      // this initializes the DefaultGroupConfig to use the fallbackLogger's Trace as the InstanceActivityEmitter for all group names.

            Enable = true;

            PeriodicDumpTableInterval = (5.0).FromMinutes();
        }

        private object mutex = new object();
        private Dictionary<string, GroupTrackingInfo> groupTrackingInfoDictionary = new Dictionary<string, GroupTrackingInfo>();
        private List<GroupTrackingInfo> groupTrackingInfoList = new List<GroupTrackingInfo>();
        private GroupTrackingInfo globalTrackingInfo = new GroupTrackingInfo(-1, "[Global]", null);
        private volatile int logPendingCount = 0;

        private static readonly Logging.Logger fallbackLogger = new Logging.Logger(Fcns.CurrentClassLeafName);

        private volatile GroupConfigItem _defaultGroupConfig;
        private static readonly GroupConfigItem emptyGroupConfigItem = new GroupConfigItem();

        private Dictionary<string, GroupConfigItem> groupConfigItemDictionary = new Dictionary<string, GroupConfigItem>();

        #endregion

        #region GroupTrackingInfo

        private class GroupTrackingInfo
        {
            public GroupTrackingInfo(int groupIndex, string groupName, GroupConfigItem groupConfigItem)
            {
                GroupIndex = groupIndex;
                GroupName = groupName;
                GroupConfigItem = groupConfigItem;
            }

            public int GroupIndex { get; private set; }
            public string GroupName { get; private set; }
            public GroupConfigItem GroupConfigItem { get; set; }

            public QpcTimeStamp nextStatusLogTimeStamp;

            public bool AddInstance(QpcTimeStamp qpcNow)
            {
                addCount += 1;
                totalCount += 1;

                if (nextStatusLogTimeStamp.IsZero && !qpcNow.IsZero)
                    nextStatusLogTimeStamp = qpcNow + (1.0).FromSeconds();

                return (!nextStatusLogTimeStamp.IsZero);
            }

            public bool RemoveInstance(QpcTimeStamp qpcNow, DisposableBase.DisposeType disploseType, double trackerAge)
            {
                trackerAgeSum += trackerAge;

                if (removeCount != 0)
                {
                    minTrackerAge = Math.Min(minTrackerAge, trackerAge);
                    maxTrackerAge = Math.Max(maxTrackerAge, trackerAge);
                }
                else
                {
                    minTrackerAge = maxTrackerAge = trackerAge;
                }

                removeCount += 1;

                switch (disploseType)
                {
                    case DisposableBase.DisposeType.CalledExplicitly: disposeCount += 1; break;
                    case DisposableBase.DisposeType.CalledByFinalizer: finalizeCount += 1; break;
                }

                totalCount -= 1;

                if (nextStatusLogTimeStamp.IsZero && !qpcNow.IsZero)
                    nextStatusLogTimeStamp = qpcNow + (1.0).FromSeconds();

                return (!nextStatusLogTimeStamp.IsZero);
            }

            public long totalCount;

            public long addCount;
            public long removeCount;
            public long disposeCount;
            public long finalizeCount;
            public double trackerAgeSum;
            public double minTrackerAge;
            public double maxTrackerAge;

            public override string ToString()
            {
                return "Group '{0}' statistics {1}".CheckedFormat(GroupName, GetCountPartStr());
            }

            public string GetCountPartStr()
            {
                double oneOverRemoveCount = (removeCount > 0) ? 1.0 / removeCount : 0.0;

                return "total:{0} [add:{1} disp:{2} finalize:{3} age avg:{4:f6} min:{5:f6} max:{6:f6}]".CheckedFormat(totalCount, addCount, disposeCount, finalizeCount, trackerAgeSum * oneOverRemoveCount, minTrackerAge, maxTrackerAge);
            }
        }

        #endregion

        #region LifetimeTrackerImpl class object

        /// <summary>
        /// This is the underlying implementation object for the lifetime trackers that are created by this class.
        /// This class implements the IDisposable interface and also provides a finalizer.  
        /// Both the Dispose method and the Finalizer method inform the LifetimeTracking singleton that an instance of this group has been reclaimed
        /// and pass in the retained groupIndex and the createTimeStamp.  These can be safely used in the Finalizer as they are both value types.
        /// In addition the Dispose method asks the GC to suppress finalizing this object later after the dispose has been completed.
        /// </summary>
        private class LifetimeTrackerImpl : IDisposable
        {
            public LifetimeTrackerImpl(int groupIndex, object extraWeightObj, QpcTimeStamp createTimeStamp)
            {
                this.groupIndex = groupIndex;
                this.extraWeightObj = extraWeightObj;
                this.createTimeStamp = createTimeStamp;
            }

            ~LifetimeTrackerImpl()
            {
                HandleDispose(DisposableBase.DisposeType.CalledByFinalizer);
            }

            public void Dispose()
            {
                HandleDispose(DisposableBase.DisposeType.CalledExplicitly);
                System.GC.SuppressFinalize(this);
            }

            private void HandleDispose(DisposableBase.DisposeType disposeType)
            {
                try
                {
                    extraWeightObj = null;

                    LifetimeTracking.Instance.RecordTrackerRemoval(groupIndex, disposeType, createTimeStamp);

                    groupIndex = -1;
                    createTimeStamp = QpcTimeStamp.Zero;
                }
                catch { }
            }

            private int groupIndex;
            private object extraWeightObj;
            private QpcTimeStamp createTimeStamp;
        }

        #endregion

        #region RecordAddedTracker and RecordTrackerDisposal methods

        private void InnerRecordAddedTracker(GroupTrackingInfo trackingInfo)
        {
            QpcTimeStamp qpcNow = QpcTimeStamp.Now;

            logPendingCount += globalTrackingInfo.AddInstance(qpcNow).MapToInt();

            logPendingCount += trackingInfo.AddInstance(qpcNow).MapToInt();

            var emitter = (trackingInfo.GroupConfigItem ?? DefaultGroupConfig).InstanceActivityEmitter;

            if (emitter.IsEnabled)
                emitter.Emit("Added '{0}': {1}", trackingInfo.GroupName, trackingInfo.GetCountPartStr());
        }

        protected void RecordTrackerRemoval(int groupIndex, DisposableBase.DisposeType disposeType, QpcTimeStamp trackerCreateTimeStamp)
        {
            lock (mutex)
            {
                QpcTimeStamp qpcNow = QpcTimeStamp.Now;

                double trackerAge = (qpcNow - trackerCreateTimeStamp).TotalSeconds;

                logPendingCount += globalTrackingInfo.RemoveInstance(qpcNow, disposeType, trackerAge).MapToInt();

                GroupTrackingInfo trackingInfo = (groupIndex >= 0 && groupIndex < groupTrackingInfoList.Count) ? groupTrackingInfoList[groupIndex] : null;

                if (trackingInfo != null)
                {
                    var emitter = (trackingInfo.GroupConfigItem ?? DefaultGroupConfig).InstanceActivityEmitter;

                    logPendingCount += trackingInfo.RemoveInstance(qpcNow, disposeType, trackerAge).MapToInt();

                    if (emitter.IsEnabled)
                        emitter.Emit("Removed '{0}' disposeType:{1}, {2}", trackingInfo.GroupName, disposeType, trackingInfo.GetCountPartStr());
                }
                else
                {
                    var emitter = DefaultGroupConfig.InstanceActivityEmitter;

                    emitter.Emit("Attempt to removed tracker at group index {0} disposeType:{1} failed: The given index is not valid.", groupIndex, disposeType);
                }
            }
        }

        #endregion
    }
}

//-----------------------------------------------------------------
