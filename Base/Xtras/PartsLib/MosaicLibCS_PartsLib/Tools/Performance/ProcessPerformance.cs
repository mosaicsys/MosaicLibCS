//-------------------------------------------------------------------
/*! @file ProcessPerformance.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2017 Mosaic Systems Inc.
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
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Tools.Performance
{
    public class ProcessPerformancePartConfig
    {
        public ProcessPerformancePartConfig(ulong processDeltaOccurrenceFileIndexUserRowFlagBits = 0, ulong activeSetGroupsFileIndexUserRowFlagBits = 0, ulong activeSetMapGroupFileIndexUserRowFlagBits = 0)
        {
            ProcessDeltaOccurrenceFileIndexUserRowFlagBits = processDeltaOccurrenceFileIndexUserRowFlagBits;
            ActiveSetGroupsFileIndexUserRowFlagBits = activeSetGroupsFileIndexUserRowFlagBits;
            ActiveSetMapGroupFileIndexUserRowFlagBits = activeSetMapGroupFileIndexUserRowFlagBits;
            SampleInterval = (5.0).FromSeconds();
            ActiveSetSize = 5;     // config keys may be used to modify this default value.
            CPUUsageEstimateThresholdInPercentPerCore = 10.0;
            WorkingSetUsageThreshold = 500 * 1024 * 1024;
            ForcedElevationHoldPeriod = (60.0).FromSeconds();
        }

        public ProcessPerformancePartConfig(ProcessPerformancePartConfig other)
        {
            ProcessDeltaOccurrenceFileIndexUserRowFlagBits = other.ProcessDeltaOccurrenceFileIndexUserRowFlagBits;
            ActiveSetGroupsFileIndexUserRowFlagBits = other.ActiveSetGroupsFileIndexUserRowFlagBits;
            ActiveSetMapGroupFileIndexUserRowFlagBits = other.ActiveSetMapGroupFileIndexUserRowFlagBits;
            SampleInterval = other.SampleInterval;
            ActiveSetSize = other.ActiveSetSize;
            CPUUsageEstimateThresholdInPercentPerCore = other.CPUUsageEstimateThresholdInPercentPerCore;
            WorkingSetUsageThreshold = other.WorkingSetUsageThreshold;
            ForcedElevationHoldPeriod = other.ForcedElevationHoldPeriod;
        }

        [ConfigItem(IsOptional = true)]
        public ulong ProcessDeltaOccurrenceFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public ulong ActiveSetGroupsFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public ulong ActiveSetMapGroupFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan SampleInterval { get; set; }

        [ConfigItem(IsOptional = true)]
        public int ActiveSetSize { get; set; }

        [ConfigItem(IsOptional = true)]
        public double CPUUsageEstimateThresholdInPercentPerCore { get; set; }

        [ConfigItem(IsOptional = true)]
        public ulong WorkingSetUsageThreshold { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan ForcedElevationHoldPeriod { get; set; }

        public ProcessPerformancePartConfig Setup(string prefixName = "ProcessPerf.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            var adapter = new ConfigValueSetAdapter<ProcessPerformancePartConfig>(config) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixName);

            return this;
        }
    }

    public class ProcessPerformancePart : SimpleActivePartBase
    {
        public ProcessPerformancePart(string partID, ProcessPerformancePartConfig config, PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion0.Build(waitTimeLimit: (0.1).FromSeconds(), goOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.All))
        {
            Config = new ProcessPerformancePartConfig(config);
            this.mdrfWriter = mdrfWriter;

            sampleIntervalTimer = new QpcTimer() { TriggerInterval = config.SampleInterval, AutoReset = true }.Start();

            Log.SetDefaultNamedValueSetForEmitter(Logging.MesgType.All, Defaults.PerfLoggerDefaultNVS);

            ProcessDeltaOccurrenceInfo = new MDRF.Writer.OccurrenceInfo()
            {
                Name = "{0}.ProcessDelta".CheckedFormat(PartID),
                Comment = "Used to record all process additions, removals, and migrations to/from the active set",
                FileIndexUserRowFlagBits = config.ProcessDeltaOccurrenceFileIndexUserRowFlagBits,
            };

            activeSetItemTrackerArray = Enumerable.Range(1, Math.Max(config.ActiveSetSize, 1)).Select(groupNum => new ActiveSetItemTracker("{0}.ActSet{1:d2}".CheckedFormat(PartID, groupNum), Config.ActiveSetGroupsFileIndexUserRowFlagBits)).ToArray();
            activeSetItemTrackerArrayLength = activeSetItemTrackerArray.Length;

            activeSetMapGroup = new ActiveSetMapGroupTracker(config.ActiveSetSize, "{0}.ActSetMap".CheckedFormat(PartID), Config.ActiveSetMapGroupFileIndexUserRowFlagBits);

            GroupInfoArray = activeSetItemTrackerArray.Select(ast => ast.GroupInfo).ToArray();

            mdrfWriter.Add(ProcessDeltaOccurrenceInfo);
            mdrfWriter.Add(activeSetMapGroup.GroupInfo);
            mdrfWriter.AddRange(GroupInfoArray);
        }

        ProcessPerformancePartConfig Config { get; set; }
        PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter;

        MDRF.Writer.OccurrenceInfo ProcessDeltaOccurrenceInfo { get; set; }
        MDRF.Writer.GroupInfo[] GroupInfoArray { get; set; }

        QpcTimer sampleIntervalTimer;

        protected class ProcessTracker
        {
            public ProcessTracker(Process p, QpcTimeStamp getTimeStamp)
            {
                activePTSetIndex = -1;
                touchedTimeStamp = getTimeStamp;
                process = p;
                pid = p.Id;
                name = Utils.ExtensionMethods.TryGet(() => p.ProcessName);
                basePriority = Utils.ExtensionMethods.TryGet(() => p.BasePriority);
                mainModule = Utils.ExtensionMethods.TryGet(() => p.MainModule);
                if (mainModule != null)
                {
                    mainModuleFilePath = Utils.ExtensionMethods.TryGet(() => mainModule.FileName);
                    mainModuleFileVersion = Utils.ExtensionMethods.TryGet(() => mainModule.FileVersionInfo);
                }
                availableItemsToTrack = TrackedValues.TestAvailableItems(p);
                prevTrackedValues = default(TrackedValues);
                trackedValues = TrackedValues.TryGetFrom(p, getTimeStamp, availableItemsToTrack, prevTrackedValues);
            }

            public void UpdateFrom(Process p, QpcTimeStamp getTimeStamp)
            {
                touchedTimeStamp = getTimeStamp;
                prevTrackedValues = trackedValues;
                trackedValues = TrackedValues.TryGetFrom(p, getTimeStamp, availableItemsToTrack, prevTrackedValues);
            }

            public int activePTSetIndex;
            public bool IsInActiveSet { get { return activePTSetIndex >= 0; } }

            public string elevationRequestReason;
            public bool IsElevationRequested { get { return !elevationRequestReason.IsNullOrEmpty(); } }
            public QpcTimeStamp lastElevationRequestTriggerTimeStamp;        // zero if the process is not elevated
            public QpcTimeStamp touchedTimeStamp;
            public Process process;
            public int pid;

            public string name;
            public int basePriority;
            public ProcessModule mainModule;
            public string mainModuleFilePath;
            public FileVersionInfo mainModuleFileVersion;
            public TrackItemSelect availableItemsToTrack;
            public TrackedValues trackedValues;
            public TrackedValues prevTrackedValues;
            public TrackedValues activationBaselineTrackedValues;

            public TrackedValues TrackedValuesSinceActivation 
            { 
                get 
                {
                    TrackedValues result = trackedValues;
                    result.cpuTime -= activationBaselineTrackedValues.cpuTime;
                    result.userCpuTime -= activationBaselineTrackedValues.userCpuTime;
                    return result;
                } 
            }

            public INamedValueSet AsNVS(bool includeExtraProcessInfo = false, bool includeTrackedValues = false)
            {
                IEnumerable<INamedValue> main = new NamedValueSet() 
                {
                    { "pid", pid },
                    { "name", name },
                }.MakeReadOnly();

                NamedValueSet extra = NamedValueSet.Empty;
                if (includeExtraProcessInfo)
                {
                    extra = new NamedValueSet()
                    {
                        { "basePriority", basePriority },
                        { "mmfPath", mainModuleFilePath },
                    };

                    if (mainModuleFileVersion != null)
                    {
                        extra.SetValue("mmfVersion", mainModuleFileVersion.ProductVersion);
                        extra.SetValue("mmfCompany", mainModuleFileVersion.CompanyName);
                    }
                }
                extra.MakeReadOnly();

                IEnumerable<INamedValue> tracked = !includeTrackedValues ? NamedValueSet.Empty : trackedValues.AsNVS();

                return new NamedValueSet(main.Concat(extra).Concat(tracked)).MakeReadOnly();
            }

            [Flags]
            public enum TrackItemSelect
            {
                None = 0x0000,
                Affinity = 0x0001,
                HandleCount = 0x0002,
                VMSize = 0x0004,
                WSSize = 0x0008,
                ProcTime = 0x0010,
            }

            public struct TrackedValues
            {
                public QpcTimeStamp timeStamp;
                public UInt64 affinity;
                public int handleCount;
                public UInt64 virtualMemorySize;
                public UInt64 workingSetSize;
                public UInt64 peakVirtualMemorySize;
                public UInt64 peakWorkingSetSize;
                public float cpuPerEst;         // measured in percent of elapsed time.  My be larger than 100% on multi-core systems.
                public float userCpuPerEst;     // measured in percent of elapsed time.  My be larger than 100% on multi-core systems.
                public TimeSpan cpuTime;     // sum of both system and user cpu time used
                public TimeSpan userCpuTime;

                public IEnumerable<INamedValue> AsNVS()
                {
                    return new NamedValueSet()
                    {
                        { "affinity", affinity},
                        { "handles", handleCount },
                        { "vmSize", virtualMemorySize },
                        { "wsSize", workingSetSize },
                        { "pkVMSize", peakVirtualMemorySize },
                        { "pkWSSize", peakWorkingSetSize },
                        { "cpuPerEst", cpuPerEst },
                        { "userCpuPerEst", userCpuPerEst },
                        { "cpuTicks", cpuTime.Ticks },
                        { "userCpuTicks", userCpuTime.Ticks },
                    }.MakeReadOnly();
                }

                public static TrackItemSelect TestAvailableItems(Process p)
                {
                    TrackItemSelect availableItems = TrackItemSelect.None
                        | (Utils.ExtensionMethods.TryGet<long?>(() => p.ProcessorAffinity.ToInt64()) != null ? TrackItemSelect.Affinity : TrackItemSelect.None)
                        | (Utils.ExtensionMethods.TryGet<int?>(() => p.HandleCount) != null ? TrackItemSelect.HandleCount : TrackItemSelect.None)
                        | ((Utils.ExtensionMethods.TryGet<long?>(() => p.VirtualMemorySize64) != null) && (Utils.ExtensionMethods.TryGet<long?>(() => p.PeakVirtualMemorySize64) != null) ? TrackItemSelect.VMSize : TrackItemSelect.None)
                        | ((Utils.ExtensionMethods.TryGet<long?>(() => p.WorkingSet64) != null) && (Utils.ExtensionMethods.TryGet<long?>(() => p.PeakWorkingSet64) != null) ? TrackItemSelect.WSSize : TrackItemSelect.None)
                        | ((Utils.ExtensionMethods.TryGet<TimeSpan?>(() => p.TotalProcessorTime) != null) && (Utils.ExtensionMethods.TryGet<TimeSpan?>(() => p.UserProcessorTime) != null) ? TrackItemSelect.ProcTime : TrackItemSelect.None)
                        ;

                    if ((availableItems & TrackItemSelect.VMSize) != 0)
                    {
                        if (Utils.ExtensionMethods.TryGet(() => p.VirtualMemorySize64) == 0xffffffff && Utils.ExtensionMethods.TryGet(() => p.PeakVirtualMemorySize64) == 0xffffffff)
                            availableItems &= ~TrackItemSelect.VMSize;
                    }
                    return availableItems;
                }

                public static TrackedValues TryGetFrom(Process p, QpcTimeStamp getTimeStamp, TrackItemSelect trackItemsSelect, TrackedValues prevTrackedValues)
                {
                    TrackedValues result = default(TrackedValues);

                    result.timeStamp = getTimeStamp;
                    if ((trackItemsSelect & TrackItemSelect.Affinity) != 0)
                        result.affinity = Utils.ExtensionMethods.TryGet(() => unchecked((UInt64)p.ProcessorAffinity.ToInt64()));
                    if ((trackItemsSelect & TrackItemSelect.HandleCount) != 0)
                        result.handleCount = Utils.ExtensionMethods.TryGet(() => p.HandleCount);
                    if ((trackItemsSelect & TrackItemSelect.VMSize) != 0)
                    {
                        result.virtualMemorySize = Utils.ExtensionMethods.TryGet(() => unchecked((UInt64)p.VirtualMemorySize64));
                        result.peakVirtualMemorySize = Utils.ExtensionMethods.TryGet(() => unchecked((UInt64)p.PeakVirtualMemorySize64));
                    }
                    if ((trackItemsSelect & TrackItemSelect.WSSize) != 0)
                    {
                        result.workingSetSize = Utils.ExtensionMethods.TryGet(() => unchecked((UInt64)p.WorkingSet64));
                        result.peakWorkingSetSize = Utils.ExtensionMethods.TryGet(() => unchecked((UInt64)p.PeakWorkingSet64));
                    }

                    if ((trackItemsSelect & TrackItemSelect.ProcTime) != 0)
                    {
                        result.cpuTime = Utils.ExtensionMethods.TryGet(() => p.TotalProcessorTime);
                        result.userCpuTime = Utils.ExtensionMethods.TryGet(() => p.UserProcessorTime);

                        double totalElapsedTime = (getTimeStamp - prevTrackedValues.timeStamp).TotalSeconds;
                        double oneOverTotalElapsedTime = (totalElapsedTime > 0.0) ? (1.0 / totalElapsedTime) : 0.0;

                        double incrementalTotalCPUUsed = (result.cpuTime - prevTrackedValues.cpuTime).TotalSeconds;
                        double incrementalUserCPUUsed = (result.userCpuTime - prevTrackedValues.userCpuTime).TotalSeconds;

                        result.cpuPerEst = (float) (incrementalTotalCPUUsed * oneOverTotalElapsedTime * 100.0);
                        result.userCpuPerEst = (float) (incrementalUserCPUUsed * oneOverTotalElapsedTime * 100.0);
                    }

                    return result;
                }

                public override string ToString()
                {
                    return "af:{0:x2} h:{1} vm:{2}k ws:{3}k cpu[t/u]:{4:f1}/{5:f1}% {6:f3}/{7:f3}s".CheckedFormat(affinity, handleCount, virtualMemorySize / 1024, workingSetSize/1024, cpuPerEst, userCpuPerEst, cpuTime.TotalSeconds, userCpuTime.TotalSeconds);
                }
            }

            public override string ToString()
            {
                return "pid:{0} '{1}' prio:{2}{3}{4} {5}".CheckedFormat(pid, name, basePriority, (IsInActiveSet ? " act:{0}".CheckedFormat(activePTSetIndex) : ""), (IsElevationRequested ? " eReq:{0}".CheckedFormat(elevationRequestReason) : ""), trackedValues);
            }
        }

        protected class ActiveSetItemTracker
        {
            public ActiveSetItemTracker(string groupName, ulong activeSetGroupsFileIndexUserRowFlagBits)
            {
                GroupInfo = new MDRF.Writer.GroupInfo()
                {
                    Name = groupName,
                    GroupBehaviorOptions = MDRF.Writer.GroupBehaviorOptions.UseVCHasBeenSetForTouched | MDRF.Writer.GroupBehaviorOptions.IncrSeqNumOnTouched,
                    GroupPointInfoArray = new[] { pidGPI, cpuPerEstGPI, userCpuPerEstGPI, cpuTicksGPI, userCpuTicksGPI, vmSizeGPI, wsSizeGPI, handlesGPI },
                    FileIndexUserRowFlagBits = activeSetGroupsFileIndexUserRowFlagBits,
                };
            }

            public ProcessTracker ProcessTracker { get; set; }

            public void UpdateGroupItems()
            {
                ProcessTracker.TrackedValues trackedValues = (ProcessTracker != null) ? ProcessTracker.TrackedValuesSinceActivation : default(ProcessTracker.TrackedValues);

                pidGPI.VC = ValueContainer.CreateI4(ProcessTracker.pid);
                cpuPerEstGPI.VC = ValueContainer.CreateF4(trackedValues.cpuPerEst);
                userCpuPerEstGPI.VC = ValueContainer.CreateF4(trackedValues.userCpuPerEst);
                cpuTicksGPI.VC = ValueContainer.CreateI8(trackedValues.cpuTime.Ticks);
                userCpuTicksGPI.VC = ValueContainer.CreateI8(trackedValues.userCpuTime.Ticks);
                vmSizeGPI.VC = ValueContainer.CreateU8(trackedValues.virtualMemorySize);
                wsSizeGPI.VC = ValueContainer.CreateU8(trackedValues.workingSetSize);
                handlesGPI.VC = ValueContainer.CreateI4(trackedValues.handleCount);
            }

            public MDRF.Writer.GroupInfo GroupInfo { get; private set; }

            public MDRF.Writer.GroupPointInfo pidGPI = new MDRF.Writer.GroupPointInfo() { Name = "pid", Comment = "process id", CST = ContainerStorageType.Int32, VC = new ValueContainer(0) };
            public MDRF.Writer.GroupPointInfo cpuPerEstGPI = new MDRF.Writer.GroupPointInfo() { Name = "cpuPerEst", Comment = "cpu percent used estimate for last sample period (system + user)", CST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo userCpuPerEstGPI = new MDRF.Writer.GroupPointInfo() { Name = "userCpuPerEst", Comment = "user cpu percent used estimate for last sample period", CST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo cpuTicksGPI = new MDRF.Writer.GroupPointInfo() { Name = "cpuTicks", Comment = "cpu usage in ticks since entered active set (system + user)", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo userCpuTicksGPI = new MDRF.Writer.GroupPointInfo() { Name = "userCpuTicks", Comment = "user cpu usage in ticks since entered active set", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo vmSizeGPI = new MDRF.Writer.GroupPointInfo() { Name = "vmSize", Comment = "virtual memory size", CST = ContainerStorageType.UInt64, VC = new ValueContainer(0ul) };
            public MDRF.Writer.GroupPointInfo wsSizeGPI = new MDRF.Writer.GroupPointInfo() { Name = "wsSize", Comment = "working set size", CST = ContainerStorageType.UInt64, VC = new ValueContainer(0ul) };
            public MDRF.Writer.GroupPointInfo handlesGPI = new MDRF.Writer.GroupPointInfo() { Name = "handles", Comment = "number of handles", CST = ContainerStorageType.Int32, VC = new ValueContainer(0) };

            public override string ToString()
            {
                if (ProcessTracker != null)
                    return "{0} {1}".CheckedFormat(GroupInfo.Name, ProcessTracker);
                else
                    return "{0} Empty".CheckedFormat(GroupInfo.Name);
            }
        }

        public class ActiveSetMapGroupTracker
        {
            public ActiveSetMapGroupTracker(int configuredActiveSetSize, string groupName, ulong activeSetMapGroupFileIndexUserRowFlagBits)
            {
                gpiPairArray = Enumerable.Range(1, configuredActiveSetSize).Select(setNum => new GPIPair(setNum)).ToArray();

                GroupInfo = new MDRF.Writer.GroupInfo()
                {
                    Name = groupName,
                    GroupBehaviorOptions = MDRF.Writer.GroupBehaviorOptions.UseVCHasBeenSetForTouched | MDRF.Writer.GroupBehaviorOptions.IncrSeqNumOnTouched,
                    GroupPointInfoArray = gpiPairArray.SelectMany(pair => new [] { pair.pidGPI, pair.nameGPI }).ToArray(),
                    FileIndexUserRowFlagBits = activeSetMapGroupFileIndexUserRowFlagBits,
                };
            }

            public MDRF.Writer.GroupInfo GroupInfo { get; private set; }

            public class GPIPair
            {
                public GPIPair(int setNum)
                {
                    pidGPI = new MDRF.Writer.GroupPointInfo() { Name = "Set.{0:d2}.pid".CheckedFormat(setNum), Comment = "Gives the process ID of the process in set {0}".CheckedFormat(setNum), CST = ContainerStorageType.Int32, VC = new ValueContainer(0) };
                    nameGPI = new MDRF.Writer.GroupPointInfo() { Name = "Set.{0:d2}.name".CheckedFormat(setNum), Comment = "Gives the process name of the process in set {0}".CheckedFormat(setNum), CST = ContainerStorageType.String, VC = new ValueContainer("") };
                }

                public MDRF.Writer.GroupPointInfo pidGPI;
                public MDRF.Writer.GroupPointInfo nameGPI;
            }

            private readonly GPIPair[] gpiPairArray;

            public void Update(int setIdx, int pid, string name)
            {
                GPIPair gpiPair = gpiPairArray.SafeAccess(setIdx);
                if (gpiPair != null)
                {
                    ValueContainer pidVC = ValueContainer.CreateI4(pid);
                    ValueContainer nameVC = ValueContainer.CreateA(name);

                    if (!gpiPair.pidGPI.VC.Equals(pidVC) || !gpiPair.nameGPI.VC.Equals(nameVC))
                    {
                        gpiPair.pidGPI.VC = pidVC;
                        gpiPair.nameGPI.VC = nameVC;
                    }
                }
            }
        }

        bool firstTime = true;

        protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
        {
            InnerServiceProcessTracking();

            return string.Empty;
        }

        protected override void PerformMainLoopService()
        {
            if (sampleIntervalTimer.Started != BaseState.IsOnline)
            {
                if (BaseState.IsOnline)
                    sampleIntervalTimer.Reset(triggerImmediately: true);
                else
                    sampleIntervalTimer.Stop();
            }
            
            if (BaseState.IsOnline && (sampleIntervalTimer.IsTriggered || firstTime))
            {
                InnerServiceProcessTracking();
                firstTime = false;
            }
        }

        private void InnerServiceProcessTracking()
        {
            QpcTimeStamp getStartTime = QpcTimeStamp.Now;

            processes = Process.GetProcesses();

            getElpased = QpcTimeStamp.Now - getStartTime;

            TrackProcesss(processes, getStartTime);
        }

        System.Diagnostics.Process[] processes;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "debugging support")]
        private TimeSpan getElpased;

        Dictionary<int, ProcessTracker> trackedProcessDictionary = new Dictionary<int, ProcessTracker>();

        ProcessTracker[] ptSetArray;
        int ptSetArrayLength;
        ActiveSetItemTracker[] activeSetItemTrackerArray;
        int activeSetItemTrackerArrayLength;
        ActiveSetMapGroupTracker activeSetMapGroup;

        private void TrackProcesss(Process[] processes, QpcTimeStamp getTimeStamp)
        {
            foreach (var p in processes)
            {
                ProcessTracker pt;

                if (trackedProcessDictionary.TryGetValue(p.Id, out pt) && pt != null)
                {
                    pt.UpdateFrom(p, getTimeStamp);
                    if (pt.activePTSetIndex >= 0)
                        activeSetItemTrackerArray[pt.activePTSetIndex].UpdateGroupItems();
                }
                else
                {
                    pt = new ProcessTracker(p, getTimeStamp);
                    RecordProcessAdded(pt);
                }
            }

            UpdatePTSetArrayIfNeeded();

            int activeCount = 0;
            bool anyToElevate = false;
            ProcessTracker possibleNextToActivate = null;

            foreach (var pt in ptSetArray)
            {
                string removeReason = null;

                double lastUpdateAge = (getTimeStamp - pt.touchedTimeStamp).TotalSeconds;
                if (lastUpdateAge > 0.0)
                    removeReason = "Process drop detected after {0:f2} seconds".CheckedFormat(lastUpdateAge);

                double estCpuUsage = pt.trackedValues.cpuPerEst;
                string elevateReason = ((estCpuUsage >= Config.CPUUsageEstimateThresholdInPercentPerCore) ? "cpu [{0:f3}>={1}]".CheckedFormat(estCpuUsage, Config.CPUUsageEstimateThresholdInPercentPerCore): null)
                                        ?? ((pt.trackedValues.workingSetSize >= Config.WorkingSetUsageThreshold) ? "ws [{0}>={1}]".CheckedFormat(pt.trackedValues.workingSetSize, Config.WorkingSetUsageThreshold) : null);

                if (!removeReason.IsNullOrEmpty())
                {
                    RecordProcessRemoved(pt, removeReason);
                }
                else if (!elevateReason.IsNullOrEmpty())
                {
                    pt.elevationRequestReason = elevateReason;
                    pt.lastElevationRequestTriggerTimeStamp = getTimeStamp;

                    if (!pt.IsInActiveSet)
                        anyToElevate = true;
                }
                else if (pt.IsElevationRequested && (getTimeStamp - pt.lastElevationRequestTriggerTimeStamp) > Config.ForcedElevationHoldPeriod)
                {
                    pt.elevationRequestReason = null;
                }

                if (pt.IsInActiveSet)
                {
                    activeCount++;
                }
                else if (!pt.IsElevationRequested && (possibleNextToActivate == null || pt.trackedValues.cpuTime > possibleNextToActivate.trackedValues.cpuTime))
                {
                    possibleNextToActivate = pt;
                }
            }

            UpdatePTSetArrayIfNeeded();

            if (activeCount < ptSetArrayLength || anyToElevate || possibleNextToActivate != null)
            {
                for (; ; )
                {
                    int useActivePTSetIndex = FindFirstEmptyActivePTSetIndex();

                    // find the next inactive pt that IsElevationRequested and which has the largest totalProcessorTime
                    ProcessTracker nextElevatedPtToActivate = null;
                    ProcessTracker lowestActiveNonElevated = null, lowestActiveElevated = null;

                    for (int idx = 0; idx < ptSetArrayLength; idx++)
                    {
                        ProcessTracker pt = ptSetArray[idx];
                        bool isInActiveSet = pt.IsInActiveSet;
                        bool isElevationRequested = pt.IsElevationRequested;

                        if (isInActiveSet && !isElevationRequested && (lowestActiveElevated == null || pt.trackedValues.cpuTime < lowestActiveElevated.trackedValues.cpuTime))
                            lowestActiveElevated = pt;
                        else if (isInActiveSet && !isElevationRequested && (lowestActiveNonElevated == null || pt.trackedValues.cpuTime < lowestActiveNonElevated.trackedValues.cpuTime))
                            lowestActiveNonElevated = pt;
                        else if (!isInActiveSet && isElevationRequested && (nextElevatedPtToActivate == null || pt.trackedValues.cpuTime > nextElevatedPtToActivate.trackedValues.cpuTime))
                            nextElevatedPtToActivate = pt;
                        else if (!isInActiveSet && !isElevationRequested && (possibleNextToActivate == null || pt.trackedValues.cpuTime > possibleNextToActivate.trackedValues.cpuTime))
                            possibleNextToActivate = pt;
                    }

                    ProcessTracker ptToActivate = nextElevatedPtToActivate;

                    if (ptToActivate == null && possibleNextToActivate != null && (useActivePTSetIndex >= 0 || lowestActiveNonElevated != null && lowestActiveNonElevated.trackedValues.cpuTime < possibleNextToActivate.trackedValues.cpuTime))
                        ptToActivate = possibleNextToActivate;

                    if (ptToActivate != null && useActivePTSetIndex < 0)
                    {
                        if (lowestActiveNonElevated != null)
                            useActivePTSetIndex = lowestActiveNonElevated.activePTSetIndex;
                        else if (lowestActiveElevated != null && nextElevatedPtToActivate != null)
                            useActivePTSetIndex = lowestActiveElevated.activePTSetIndex;
                        else
                        {
                            if (!unableToAddProcessTrackerToActiveSet)
                            {
                                unableToAddProcessTrackerToActiveSet = true;

                                RecordProcessEvent("ActiveSetFull", ptToActivate, includeExtraProcessInfo: false, includeTrackedValues: true);
                            }
                            break;      // active set is full and we have nowhere to put any new items
                        }
                    }

                    if (ptToActivate == null)
                        break;

                    Activate(ptToActivate, useActivePTSetIndex);

                    unableToAddProcessTrackerToActiveSet = false;
                    possibleNextToActivate = null;
                }
            }

            mdrfWriter.RecordGroups();
        }

        bool unableToAddProcessTrackerToActiveSet = false;

        private int FindFirstEmptyActivePTSetIndex()
        {
            for (int idx = 0; idx < activeSetItemTrackerArrayLength; idx++)
            {
                ActiveSetItemTracker activeSetItemTracker = activeSetItemTrackerArray[idx];
                if (activeSetItemTracker != null && activeSetItemTracker.ProcessTracker == null)
                    return idx;
            }

            return -1;
        }

        private void UpdatePTSetArrayIfNeeded()
        {
            if (ptSetArray == null)
            {
                ptSetArray = trackedProcessDictionary.Values.ToArray();
                ptSetArrayLength = ptSetArray.Length;
            }
        }

        private void RecordProcessAdded(ProcessTracker pt)
        {
            trackedProcessDictionary[pt.pid] = pt;
            ptSetArray = null;

            RecordProcessEvent("Added", pt, includeExtraProcessInfo: true, includeTrackedValues: true);
        }

        private void RecordProcessRemoved(ProcessTracker pt, string reason)
        {
            trackedProcessDictionary.Remove(pt.pid);
            ptSetArray = null;

            if (pt.IsInActiveSet)
            {
                activeSetMapGroup.Update(pt.activePTSetIndex, 0, "");

                ActiveSetItemTracker activeSetItemTracker = activeSetItemTrackerArray[pt.activePTSetIndex];

                activeSetItemTracker.ProcessTracker = null;
                activeSetItemTracker.GroupInfo.Touched = true;
                pt.activePTSetIndex = -1;
            }

            INamedValueSet extraNVS = new NamedValueSet() { { "reason", reason } }.MakeReadOnly();

            RecordProcessEvent("Removed", pt, includeExtraProcessInfo: false, includeTrackedValues: true);
        }

        private void Activate(ProcessTracker pt, int activeSetIdx)
        {
            MDRF.Common.DateTimeStampPair dtPair = MDRF.Common.DateTimeStampPair.NowQpcOnly;

            // if needed - deactivate the previous pt in the given index
            ActiveSetItemTracker activeSetItemTracker = activeSetItemTrackerArray[activeSetIdx];

            ProcessTracker priorPT = activeSetItemTracker.ProcessTracker;

            if (priorPT != null)
            {
                // record the last set of values (and the map) from the old ProcessTracker before replacing them with the new values.
                activeSetItemTracker.GroupInfo.Touched = true;
                activeSetMapGroup.GroupInfo.Touched = true;
                mdrfWriter.RecordGroups(dtPair: dtPair);

                string deactivateReason = "pid:{0} '{1}' is being {2}".CheckedFormat(pt.pid, pt.name, pt.elevationRequestReason.IsNullOrEmpty() ? "activated" : "elevated");
                INamedValueSet extraDeactivatedNVS = new NamedValueSet() { { "deactivateReason", deactivateReason }, { "activeSetNum", activeSetIdx + 1 } }.MakeReadOnly();

                priorPT.activePTSetIndex = -1;
                RecordProcessEvent("Deactivated", priorPT, extraNVS: extraDeactivatedNVS, includeExtraProcessInfo: false, includeTrackedValues: true, dtPair: dtPair);
            }

            // now move the given pt into the active set at the selected index.

            INamedValueSet extraNVS = (pt.elevationRequestReason.IsNullOrEmpty() ? new NamedValueSet() { { "activeSetNum", activeSetIdx + 1 } } : new NamedValueSet() { { "elevateReason", pt.elevationRequestReason }, { "activeSetNum", activeSetIdx + 1 } }).MakeReadOnly();

            RecordProcessEvent("Activated", pt, extraNVS: extraNVS, includeExtraProcessInfo: false, includeTrackedValues: true, dtPair: dtPair);

            pt.activePTSetIndex = activeSetIdx;
            pt.activationBaselineTrackedValues = pt.prevTrackedValues;
            activeSetItemTracker.ProcessTracker = pt;
            activeSetItemTracker.UpdateGroupItems();
            activeSetItemTracker.GroupInfo.Touched = true;

            activeSetMapGroup.Update(activeSetIdx, pt.pid, pt.name);

            mdrfWriter.RecordGroups(dtPair: dtPair);
        }

        private void RecordProcessEvent(string eventName, ProcessTracker pt, INamedValueSet extraNVS = null, bool includeExtraProcessInfo = false, bool includeTrackedValues = false, MDRF.Common.DateTimeStampPair dtPair = null)
        {
            if (extraNVS.IsNullOrEmpty())
                Log.Info.Emit("{0} pid:{1} name:{2}", eventName, pt.pid, pt.name);
            else
                Log.Info.Emit("{0} pid:{1} name:{2} {3}", eventName, pt.pid, pt.name, extraNVS);

            INamedValueSet nvs = new NamedValueSet() { { "eventName", eventName } }
                                .MergeWith(pt.AsNVS(includeExtraProcessInfo: includeExtraProcessInfo, includeTrackedValues: includeTrackedValues))
                                .MergeWith(extraNVS, NamedValueMergeBehavior.AddNewItems)
                                .ConvertToReadOnly();

            mdrfWriter.RecordOccurrence(ProcessDeltaOccurrenceInfo, new ValueContainer(nvs), dtPair: dtPair);
        }
    }
}

//-------------------------------------------------------------------
