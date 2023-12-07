//-------------------------------------------------------------------
/*! @file PerformanceCounters.cs
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

using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.PartsLib.Tools.Performance
{
    public class PerformanceCountersPartConfig
    {
        public PerformanceCountersPartConfig(string partID, ulong performanceCounterGroupsFileIndexUserRowFlagBits = 0)
        {
            PartID = partID;
            PerformanceCounterGroupsFileIndexUserRowFlagBits = performanceCounterGroupsFileIndexUserRowFlagBits;
            SampleInterval = (5.0).FromSeconds();
        }

        public PerformanceCountersPartConfig(PerformanceCountersPartConfig other, string newPartID = null)
        {
            PartID = newPartID.MapNullOrEmptyTo(other.PartID);
            PerformanceCounterGroupsFileIndexUserRowFlagBits = other.PerformanceCounterGroupsFileIndexUserRowFlagBits;
            SampleInterval = other.SampleInterval;
            PerformanceCounterSpecArray = (other.PerformanceCounterSpecArray ?? emptyPCSpecArray).Select(pcs => new PerformanceCounterSpec(pcs)).ToArray();
        }

        public string PartID { get; private set; }

        [ConfigItem(IsOptional = true)]
        public ulong PerformanceCounterGroupsFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan SampleInterval { get; set; }

        public PerformanceCounterSpec[] PerformanceCounterSpecArray { get; set; }

        public PerformanceCountersPartConfig Setup(string prefixName = "PerfCtr.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            var adapter = new ConfigValueSetAdapter<PerformanceCountersPartConfig>(config) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixName);

            return this;
        }

        private static readonly PerformanceCounterSpec[] emptyPCSpecArray = EmptyArrayFactory<PerformanceCounterSpec>.Instance;
    }

    public class PerformanceCounterSpec
    {
        public PerformanceCounterSpec()
        {
            MachineName = ".";
        }

        public PerformanceCounterSpec(PerformanceCounterSpec other)
        {
            MachineName = other.MachineName;
            CategoryName = other.CategoryName;
            CounterName = other.CounterName;
            InstanceName = other.InstanceName;
            UseRawValue = other.UseRawValue;
            _pointName = other._pointName;
        }

        public string MachineName { get; set; }
        public string CategoryName { get; set; }
        public string CounterName { get; set; }
        public string InstanceName { get; set; }
        public bool UseRawValue { get; set; }

        public string PointName { get { return _pointName.MapNullOrEmptyTo(DefaultPointName); } set { _pointName = value; } }
        private string _pointName;

        public override string ToString()
        {
            return PointName;
        }

        private string DefaultPointName
        {
            get 
            {
                string baseStr = String.Join(".", new[] { CategoryName, CounterName, InstanceName }.Where(s => !s.IsNullOrEmpty()).ToArray());

                if (MachineName.IsNullOrEmpty() || MachineName == ".")
                    return baseStr;
                else
                    return @"\\{0}\{1}".CheckedFormat(MachineName, baseStr);
            }
        }
    }

    internal class PerformanceCounterCategoryHelper
    {
        public PerformanceCounterCategory GetPerformanceCounterCategory(string categoryName)
        {
            categoryName = categoryName.Sanitize();

            lock (mutex)
            {
                InitializeIfNeeded();

                PerformanceCounterCategory pcc = null;

                pccDictionary.TryGetValue(categoryName, out pcc);

                return pcc;
            }
        }

        /// <summary>
        /// This method returns true if the given PerformanceCounterSpec <paramref name="pcs"/> refers to a counter instance that exists in the indicated category
        /// </summary>
        public bool IsValid(PerformanceCounterSpec pcs)
        {
            if (pcs == null || pcs.CategoryName.IsNullOrEmpty() || pcs.CounterName.IsNullOrEmpty())
                return false;

            lock (mutex)
            {
                InitializeIfNeeded();

                try
                {
                    PerformanceCounterCategory pcc = null;

                    if (!pccDictionary.TryGetValue(pcs.CategoryName, out pcc) || pcc == null)
                        return false;

                    PerformanceCounter [] perfCtrArray = null;

                    if (pcs.InstanceName.IsNullOrEmpty())
                    {
                        perfCtrArray = pcc.GetCounters();
                    }
                    else
                    {
                        if (!pcc.InstanceExists(pcs.InstanceName))
                            return false;

                        perfCtrArray = pcc.GetCounters(pcs.InstanceName);
                    }

                    if (perfCtrArray.Any(pc => pc.CounterName == pcs.CounterName))
                        return true;
                    else
                        return false;
                }
                catch (System.Exception ex)
                {
                    Logger.Debug.Emit("IsValid on point '{0}' failed: {1}", pcs.PointName, ex.ToString(ExceptionFormat.TypeAndMessage));

                    return false;
                }
            }        
        }

        private void InitializeIfNeeded()
        {
            if (pccArray == null)
            {
                pccArray = PerformanceCounterCategory.GetCategories();
                pccArray.DoForEach(pcc => pccDictionary[pcc.CategoryName] = pcc);
            }
        }

        private object mutex = new object();
        private PerformanceCounterCategory[] pccArray = null;
        private Dictionary<string, PerformanceCounterCategory> pccDictionary = new Dictionary<string, PerformanceCounterCategory>();
        private Logging.ILogger pcchLogger;
        private Logging.ILogger Logger { get { return pcchLogger ?? (pcchLogger = new Logging.Logger("PerformanceCounterCategoryHelper")); } }
    }

    public static partial class ExtensionMethods
    {
        internal static PerformanceCounterCategoryHelper PerformanceCounterCategoryHelper = new PerformanceCounterCategoryHelper();

        /// <summary>
        /// This extension method returns true if the given PerformanceCounterSpec <paramref name="pcs"/> refers to a counter instance that exists in the indicated category
        /// </summary>
        public static bool IsValid(this PerformanceCounterSpec pcs)
        {
            return PerformanceCounterCategoryHelper.IsValid(pcs);
        }

        /// <summary>
        /// This extension method attempts to obtain and return the PerformanceCounterCategory object for a given PerformanceCounterSpec <paramref name="pcs"/>.  Returns null if the given CategoryName could not be found.
        /// </summary>
        public static PerformanceCounterCategory GetPerformanceCounterCategory(this PerformanceCounterSpec pcs)
        {
            return PerformanceCounterCategoryHelper.GetPerformanceCounterCategory(pcs.CategoryName);
        }
    }

    public class PerformanceCountersPart : SimpleActivePartBase
    {
        public PerformanceCountersPart(PerformanceCountersPartConfig config, PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter)
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion0.Build(waitTimeLimit: (0.10).FromSeconds(), goOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.All))
        {
            Config = new PerformanceCountersPartConfig(config);
            this.mdrfWriter = mdrfWriter;

            // we trigger acquiring a new sample two times a second
            sampleIntervalTimer = new QpcTimer() { TriggerInterval = Config.SampleInterval, AutoReset = true }.Start();

            performanceCounterTrackerArray = Config.PerformanceCounterSpecArray.Select(pcs => new PerformanceCounterTracker(Log, pcs)).ToArray();

            pccTupleArray = performanceCounterTrackerArray.GroupBy(pct => pct.pcc, (pcc, pctSet) => Tuple.Create(pcc, pctSet.ToArray())).ToArray();

            groupInfo = new MDRF.Writer.GroupInfo()
            {
                Name = "{0}.Grp".CheckedFormat(PartID),
                GroupBehaviorOptions = MDRF.Writer.GroupBehaviorOptions.UseVCHasBeenSetForTouched | MDRF.Writer.GroupBehaviorOptions.IncrSeqNumOnTouched,
                FileIndexUserRowFlagBits = (ulong)Config.PerformanceCounterGroupsFileIndexUserRowFlagBits,
                GroupPointInfoArray = performanceCounterTrackerArray.Select(pct => pct.gpInfo).ToArray(),
            };

            mdrfWriter.Add(groupInfo);

            noMDRFLogger = new Logging.Logger(PartID).SetDefaultNamedValueSetForEmitter(Logging.LogGate.All, Defaults.PerfLoggerDefaultNVS);
        }

        PerformanceCountersPartConfig Config { get; set; }
        MDRF.Writer.IMDRFWriter mdrfWriter;
        Logging.IBasicLogger noMDRFLogger;

        QpcTimer sampleIntervalTimer;

        MDRF.Writer.GroupInfo groupInfo;

        PerformanceCounterTracker[] performanceCounterTrackerArray;
        Tuple<PerformanceCounterCategory, PerformanceCounterTracker[]>[] pccTupleArray;

        private class PerformanceCounterTracker
        {
            public PerformanceCounterTracker(Logging.IBasicLogger logger, PerformanceCounterSpec pcs)
            {
                this.pcs = pcs;
                pcc = pcs.GetPerformanceCounterCategory();

                useRawPerfCtrValue = pcs.UseRawValue;

                gpInfo = new MDRF.Writer.GroupPointInfo()
                {
                    Name = pcs.PointName,
                    CST = ContainerStorageType.None,
                };

                try
                {
                    var categorySample = pcc.ReadCategory();
                    Service(logger, categorySample, true);
                }
                catch (System.Exception ex)
                {
                    Logging.IMesgEmitter emitter = (logger == null ? Logging.NullEmitter : logger.Info);

                    emitter.Emit("{0} for '{1}' generated unexpected exception: {2}", CurrentMethodName, gpInfo.Name, ex.ToString(ExceptionFormat.TypeAndMessage));

                    isUsable = false;
                }
            }

            public override string ToString()
            {
                return "Tracker {0}".CheckedFormat(gpInfo);
            }

            private CounterSample lastCounterSample = default(CounterSample);

            public void Service(Logging.IBasicLogger logger, InstanceDataCollectionCollection categorySample, bool rethrow = false)
            {
                if (!isUsable)
                    return;

                try
                {
                    InstanceDataCollection instanceDataCollection = categorySample[pcs.CounterName];
                    InstanceData instanceData = (instanceDataCollection != null) ? instanceDataCollection[pcs.InstanceName.MapNullToEmpty()] : null;      // use empty string for counters that only have one value

                    if (instanceData != null)
                    {
                        CounterSample counterSample = instanceData.Sample;

                        if (!useRawPerfCtrValue)
                            gpInfo.VC = CounterSample.Calculate(lastCounterSample, counterSample).CreateVC();
                        else
                            gpInfo.VC = counterSample.RawValue.CreateVC();

                        lastCounterSample = counterSample;

                        logExceptionElevationHoldoffTimer.StopIfNeeded();
                    }
                    else
                    {
                        gpInfo.VC = ValueContainer.Empty;
                        lastCounterSample = CounterSample.Empty;
                    }
                }
                catch (System.Exception ex)
                {
                    bool useHighLevelMesg = (logExceptionElevationHoldoffTimer.IsTriggered || !logExceptionElevationHoldoffTimer.Started);
                    logExceptionElevationHoldoffTimer.StartIfNeeded();

                    Logging.IMesgEmitter emitter = (logger == null ? Logging.NullEmitter : (useHighLevelMesg ? logger.Info : logger.Trace));

                    emitter.Emit("{0} for '{1}' generated exception: {2}", CurrentMethodName, gpInfo.Name, ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }

            public PerformanceCounterSpec pcs;
            public PerformanceCounterCategory pcc;
            public bool useRawPerfCtrValue;
            public MDRF.Writer.GroupPointInfo gpInfo;
            public bool isUsable = true;
            public QpcTimer logExceptionElevationHoldoffTimer = new QpcTimer() { TriggerIntervalInSec = 120.0, AutoReset = true };
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

            bool sampleIntervalTimerTriggered = sampleIntervalTimer.IsTriggered;

            if (sampleIntervalTimerTriggered)
            {
                try
                {
                    foreach (var tuple in pccTupleArray)
                    {
                        InstanceDataCollectionCollection categorySample = tuple.Item1.ReadCategory();
                        tuple.Item2.DoForEach(pct => pct.Service(Log, categorySample));
                    }

                    mdrfWriter.RecordGroups();
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit("{0} generated unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }
        }
    }
}

//-------------------------------------------------------------------
