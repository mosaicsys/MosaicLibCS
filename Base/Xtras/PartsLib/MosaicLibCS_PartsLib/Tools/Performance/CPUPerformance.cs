//-------------------------------------------------------------------
/*! @file CPUPerformance.cs
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

namespace MosaicLib.PartsLib.Tools.Performance
{
    public class CPUPerformancePartConfig
    {
        public CPUPerformancePartConfig(ulong sampleGroupsFileIndexUserRowFlagBits = 0, ulong aggregateGroupsFileIndexUserRowFlagBits = 0)
        {
            SampleGroupsFileIndexUserRowFlagBits = sampleGroupsFileIndexUserRowFlagBits;
            AggregateGroupsFileIndexUserRowFlagBits = aggregateGroupsFileIndexUserRowFlagBits;
            SampleInterval = (0.5).FromSeconds();
            AggregationInterval = (30.0).FromSeconds();
        }

        public CPUPerformancePartConfig(CPUPerformancePartConfig other)
        {
            SampleGroupsFileIndexUserRowFlagBits = other.SampleGroupsFileIndexUserRowFlagBits;
            AggregateGroupsFileIndexUserRowFlagBits = other.AggregateGroupsFileIndexUserRowFlagBits;
            SampleInterval = other.SampleInterval;
            AggregationInterval = other.AggregationInterval;
        }

        [ConfigItem(IsOptional = true)]
        public ulong SampleGroupsFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public ulong AggregateGroupsFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan SampleInterval { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan AggregationInterval { get; set; }

        public CPUPerformancePartConfig Setup(string prefixName = "CPUPerf.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            var adapter = new ConfigValueSetAdapter<CPUPerformancePartConfig>(config) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixName);

            return this;
        }
    }

    public class CPUPerformancePart : SimpleActivePartBase
    {
        public CPUPerformancePart(string partID, CPUPerformancePartConfig config, PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion0.Build(waitTimeLimit: (0.10).FromSeconds(), goOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.All))
        {
            Config = new CPUPerformancePartConfig(config);
            this.mdrfWriter = mdrfWriter;

            Log.SetDefaultNamedValueSetForEmitter(Logging.MesgType.All, new NamedValueSet() { { "noMDRF" } });

            sampleIntervalTimer = new QpcTimer() { TriggerInterval = config.SampleInterval, AutoReset = true }.Start();
            aggregationIntervalTimer = new QpcTimer() { TriggerInterval = config.AggregationInterval, AutoReset = true }.Start();

            h1Grp = new MDRFHistogramGroupSource("{0}.h1".CheckedFormat(partID), h1, Config.SampleGroupsFileIndexUserRowFlagBits);
            h10Grp = new MDRFHistogramGroupSource("{0}.h10".CheckedFormat(partID), h10, Config.SampleGroupsFileIndexUserRowFlagBits);
            h30Grp = new MDRFHistogramGroupSource("{0}.h30".CheckedFormat(partID), h30, Config.SampleGroupsFileIndexUserRowFlagBits);

            ah1 = new Histogram(h1);
            ah10 = new Histogram(h10);
            ah30 = new Histogram(h30);

            ah1Grp = new MDRFHistogramGroupSource("{0}.ah1".CheckedFormat(partID), ah1, Config.AggregateGroupsFileIndexUserRowFlagBits);
            ah10Grp = new MDRFHistogramGroupSource("{0}.ah10".CheckedFormat(partID), ah10, Config.AggregateGroupsFileIndexUserRowFlagBits);
            ah30Grp = new MDRFHistogramGroupSource("{0}.ah30".CheckedFormat(partID), ah30, Config.AggregateGroupsFileIndexUserRowFlagBits);

            mdrfWriter.Add(h1Grp.GroupInfo, h10Grp.GroupInfo, h30Grp.GroupInfo, ah1Grp.GroupInfo, ah10Grp.GroupInfo, ah30Grp.GroupInfo);
        }

        CPUPerformancePartConfig Config { get; set; }
        PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter;

        QpcTimer sampleIntervalTimer, aggregationIntervalTimer;
        int h1SampleSize = 100, h10SampleSize = 20, h100SampleSize = 10;     // 100 * 1 + 20 * 10 + 10 * 30 = 100 + 200 + 300 = 600
        Histogram h1 = new Histogram(new double[] { 0.0005, 0.0008, 0.0010, 0.0012, 0.0014, 0.0016, 0.0018, 0.0020, 0.0022, 0.0024, 0.0026, 0.0028, 0.0030, 0.0050, 0.0100 });
        Histogram h10 = new Histogram(new double[] { 0.001, 0.0050, 0.0060, 0.0070, 0.0080, 0.0090, 0.0100, 0.0110, 0.0120, 0.0130, 0.0140, 0.0150, 0.0200, 0.0300, 0.0500, 0.1000 });
        Histogram h30 = new Histogram(new double[] { 0.020, 0.025, 0.028, 0.029, 0.030, 0.031, 0.032, 0.033, 0.034, 0.035, 0.037, 0.040, 0.050, 0.100, 0.2000, 0.3000, 0.5000, 1.0000 });

        Histogram ah1, ah10, ah30;

        MDRFHistogramGroupSource h1Grp, h10Grp, h30Grp, ah1Grp, ah10Grp, ah30Grp;

        protected override void PerformMainLoopService()
        {
            if (sampleIntervalTimer.Started != BaseState.IsOnline)
            {
                if (BaseState.IsOnline)
                    sampleIntervalTimer.Reset(triggerImmediately: true);
                else
                    sampleIntervalTimer.Stop();
            }
            
            if (sampleIntervalTimer.IsTriggered)
            {
                h1.Clear();

                QpcTimeStamp now = QpcTimeStamp.Now;
                for (int idx = 0; idx < h1SampleSize; idx++)        // this loop is fast enough to not need to check for HasStopBeenRequested
                {
                    QpcTimeStamp start = now;
                    System.Threading.Thread.Sleep(1);
                    QpcTimeStamp end = now = QpcTimeStamp.Now;

                    h1.Add((end - start).TotalSeconds);
                }

                System.Threading.Thread.Sleep(10);

                h10.Clear();

                now = QpcTimeStamp.Now;
                for (int idx = 0; idx < h10SampleSize && !HasStopBeenRequested && actionQ.IsEmpty; idx++)
                {
                    QpcTimeStamp start = now;
                    System.Threading.Thread.Sleep(10);
                    QpcTimeStamp end = now = QpcTimeStamp.Now;

                    h10.Add((end - start).TotalSeconds);
                }

                System.Threading.Thread.Sleep(10);

                h30.Clear();

                now = QpcTimeStamp.Now;
                for (int idx = 0; idx < h100SampleSize && !HasStopBeenRequested && actionQ.IsEmpty; idx++)
                {
                    QpcTimeStamp start = now;
                    System.Threading.Thread.Sleep(30);
                    QpcTimeStamp end = now = QpcTimeStamp.Now;

                    h30.Add((end - start).TotalSeconds);
                }

                h1Grp.UpdateGroupItems();
                h10Grp.UpdateGroupItems();
                h30Grp.UpdateGroupItems();

                ah1.Add(h1);
                ah10.Add(h10);
                ah30.Add(h30);

                bool aggregationIntervalTimerTriggered = aggregationIntervalTimer.IsTriggered;
                if (aggregationIntervalTimerTriggered)
                {
                    ah1Grp.UpdateGroupItems();
                    ah10Grp.UpdateGroupItems();
                    ah30Grp.UpdateGroupItems();

                    Log.Info.Emit("AH10: {0}", ah10.ToString(Histogram.TSInclude.BaseWithMedEst | Histogram.TSInclude.BinCountArray));
                }

                mdrfWriter.RecordGroups();

                if (aggregationIntervalTimerTriggered)
                {
                    ah1.Clear();
                    ah10.Clear();
                    ah30.Clear();
                }
            }
        }
    }
}

//-------------------------------------------------------------------
