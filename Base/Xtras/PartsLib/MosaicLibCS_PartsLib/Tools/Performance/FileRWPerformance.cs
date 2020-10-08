//-------------------------------------------------------------------
/*! @file FileRWPerformance.cs
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
    public class FileRWPerformancePartConfig
    {
        public FileRWPerformancePartConfig(ulong sampleGroupsFileIndexUserRowFlagBits = 0, ulong aggregateGroupsFileIndexUserRowFlagBits = 0)
        {
            SampleGroupsFileIndexUserRowFlagBits = sampleGroupsFileIndexUserRowFlagBits;
            AggregateGroupsFileIndexUserRowFlagBits = aggregateGroupsFileIndexUserRowFlagBits;
            SampleInterval = (0.5).FromSeconds();
            AggregationInterval = (30.0).FromSeconds();
            UseFileFlagNoBuffering = true;
            DisableReadThroughUseRandomWriteIOPsThreshold = 300.0;
            EnableContinuousWriting = true;
            TestFilePath = ".\\TestFile.bin";
        }

        public FileRWPerformancePartConfig(FileRWPerformancePartConfig other)
        {
            SampleGroupsFileIndexUserRowFlagBits = other.SampleGroupsFileIndexUserRowFlagBits;
            AggregateGroupsFileIndexUserRowFlagBits = other.AggregateGroupsFileIndexUserRowFlagBits;
            SampleInterval = other.SampleInterval;
            AggregationInterval = other.AggregationInterval;
            UseFileFlagNoBuffering = other.UseFileFlagNoBuffering;
            DisableReadThroughUseRandomWriteIOPsThreshold = other.DisableReadThroughUseRandomWriteIOPsThreshold;
            EnableContinuousWriting = other.EnableContinuousWriting;
            TestFilePath = other.TestFilePath;
        }

        [ConfigItem(IsOptional = true)]
        public ulong SampleGroupsFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public ulong AggregateGroupsFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan SampleInterval { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan AggregationInterval { get; set; }

        [ConfigItem(IsOptional = true, SilenceLogging = true)]
        public bool UseFileFlagNoBuffering { get; set; }

        [ConfigItem(IsOptional = true, SilenceLogging = true)]
        public double DisableReadThroughUseRandomWriteIOPsThreshold { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool EnableContinuousWriting { get; set; }

        [ConfigItem(IsOptional = true)]
        public string TestFilePath { get; set; }

        public FileRWPerformancePartConfig Setup(string prefixName = "FileRWPerf.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            var adapter = new ConfigValueSetAdapter<FileRWPerformancePartConfig>(config) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixName);

            return this;
        }
    }
    
    public class FileRWPerformancePart : SimpleActivePartBase
    {
        public FileRWPerformancePart(string partID, FileRWPerformancePartConfig config, PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion0.Build(waitTimeLimit: (0.10).FromSeconds(), goOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.All))
        {
            Config = new FileRWPerformancePartConfig(config);
            this.mdrfWriter = mdrfWriter;

            // we trigger acquiring a new sample two times a second
            sampleIntervalTimer = new QpcTimer() { TriggerInterval = Config.SampleInterval, AutoReset = true }.Start();
            aggregationIntervalTimer = new QpcTimer() { TriggerInterval = Config.AggregationInterval, AutoReset = true }.Start();

            AddExplicitDisposeAction(() => Release());

            ahReadNormal = new Histogram(hReadNormal);
            ahReadThrough = new Histogram(hReadThrough);
            ahWriteThrough = new Histogram(hWriteThrough);

            avgRatesGroup = new MDRF.Writer.GroupInfo()
            {
                Name = "{0}.avgRates".CheckedFormat(PartID),
                GroupBehaviorOptions = MDRF.Writer.GroupBehaviorOptions.UseVCHasBeenSetForTouched | MDRF.Writer.GroupBehaviorOptions.IncrSeqNumOnTouched,
                FileIndexUserRowFlagBits = (ulong) Config.AggregateGroupsFileIndexUserRowFlagBits,
                GroupPointInfoArray = new MDRF.Writer.GroupPointInfo[]
                {
                    peakReadNormalRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "peakReadRate", CST = ContainerStorageType.Double, VC = new ValueContainer(0.0) },
                    peakReadThroughRateGPI = new MDRF.Writer.GroupPointInfo() {Name = "peakReadThroughRate", CST = ContainerStorageType.Double, VC = new ValueContainer(0.0) },
                    peakWriteThroughRateGPI = new MDRF.Writer.GroupPointInfo() {Name = "peakWriteThroughRate", CST = ContainerStorageType.Double, VC = new ValueContainer(0.0) },
                },
                Touched = true,
            };

            accumTupleArray = new Tuple<MDRFHistogramGroupSource,MDRFHistogramGroupSource> []
            {
                Tuple.Create(new MDRFHistogramGroupSource("{0}.hReadNormal".CheckedFormat(PartID), hReadNormal, Config.SampleGroupsFileIndexUserRowFlagBits), 
                            new MDRFHistogramGroupSource("{0}.ahReadNormal".CheckedFormat(PartID), ahReadNormal, Config.AggregateGroupsFileIndexUserRowFlagBits)),
                Tuple.Create(new MDRFHistogramGroupSource("{0}.hReadThrough".CheckedFormat(PartID), hReadThrough, Config.SampleGroupsFileIndexUserRowFlagBits), 
                            new MDRFHistogramGroupSource("{0}.ahReadThrough".CheckedFormat(PartID), ahReadThrough, Config.AggregateGroupsFileIndexUserRowFlagBits)),
                Tuple.Create(new MDRFHistogramGroupSource("{0}.hWriteThrough".CheckedFormat(PartID), hWriteThrough, Config.SampleGroupsFileIndexUserRowFlagBits), 
                            new MDRFHistogramGroupSource("{0}.ahWriteThrough".CheckedFormat(PartID), ahWriteThrough, Config.AggregateGroupsFileIndexUserRowFlagBits)),
            };

            IEnumerable<Tuple<MDRFHistogramGroupSource, MDRFHistogramGroupSource>> registerGroupsSet = ((Config.UseFileFlagNoBuffering) ? accumTupleArray : new [] {accumTupleArray[0], accumTupleArray[2]});

            mdrfWriter.Add(avgRatesGroup);
            mdrfWriter.AddRange(registerGroupsSet.Select(t => t.Item1.GroupInfo).Concat(registerGroupsSet.Select(t => t.Item2.GroupInfo)));

            noMDRFLogger = new Logging.Logger(PartID).SetDefaultNamedValueSetForEmitter(Logging.LogGate.All, Defaults.PerfLoggerDefaultNVS);
        }

        void Release()
        {
            Fcns.DisposeOfObject(ref fsWriteThrough);
        }

        FileRWPerformancePartConfig Config { get; set; }
        PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter;
        Logging.IBasicLogger noMDRFLogger;

        QpcTimer sampleIntervalTimer, aggregationIntervalTimer;

        private static readonly double[] binBoundariesFastArray = new double[] { 0.000001, 0.000002, 0.000003, 0.000004, 0.000006, 0.000008, 0.00001, 0.00002, 0.00003, 0.00004, 0.00006, 0.00008, 0.000100, 0.0002, 0.0003, 0.0004, 0.0006, 0.0008, 0.001, 0.002, 0.003, 0.004, 0.006, 0.008, 0.01, 0.02, 0.03, 0.04, 0.06, 0.08, 0.1};
        private static readonly double[] binBoundariesArray = new double[] { 0.00001, 0.00002, 0.00003, 0.00004, 0.00006, 0.00008, 0.0001, 0.0002, 0.0003, 0.0004, 0.0006, 0.0008, 0.001, 0.002, 0.003, 0.004, 0.006, 0.008, 0.01, 0.02, 0.03, 0.04, 0.06, 0.08, 0.1, 0.2, 0.3, 0.4, 0.6, 0.8, 1.0 };

        Histogram hCreateWrite = new Histogram(binBoundariesArray);
        TimeSpan createElapsed;

        Histogram hReadNormal = new Histogram(binBoundariesFastArray);
        Histogram hReadThrough = new Histogram(binBoundariesArray);
        Histogram hWriteThrough = new Histogram(binBoundariesArray);

        Histogram ahReadNormal;
        Histogram ahReadThrough;
        Histogram ahWriteThrough;

        PartsLib.Tools.MDRF.Writer.GroupInfo avgRatesGroup;
        PartsLib.Tools.MDRF.Writer.GroupPointInfo peakReadThroughRateGPI, peakReadNormalRateGPI, peakWriteThroughRateGPI;

        Tuple<MDRFHistogramGroupSource, MDRFHistogramGroupSource>[] accumTupleArray;

        long createBytesWritten = 0;
        long totalBytesWritten = 0;

        bool firstAHLog = true;

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
                try
                {
                    CreateFileIfNeeded();

                    foreach (var t in accumTupleArray)
                        t.Item1.Histogram.Clear();

                    ReadGroupOfBuffersFromFile(fsReadNormal, hReadNormal);

                    if (Config.UseFileFlagNoBuffering && fsReadThrough != null)
                        ReadGroupOfBuffersFromFile(fsReadThrough, hReadThrough);

                    if (Config.EnableContinuousWriting)
                        WriteGroupOfBuffersToFile(fsWriteThrough, hWriteThrough);

                    foreach (var t in accumTupleArray)
                    {
                        t.Item2.Histogram.Add(t.Item1.Histogram);
                        t.Item1.UpdateGroupItems();
                    }

                    bool aggregationIntervalTimerTriggered = aggregationIntervalTimer.IsTriggered;
                    if (aggregationIntervalTimerTriggered)
                    {
                        TimeSpan measuredAggregationInterval = aggregationIntervalTimer.ElapsedTimeAtLastTrigger;

                        foreach (var t in accumTupleArray)
                            t.Item2.UpdateGroupItems();

                        double peakNormalReadRate, peakThroughReadRate, peakThroughWriteRate;

                        peakReadNormalRateGPI.VC = new ValueContainer(peakNormalReadRate = SafeRate(bufferSize, ahReadNormal));
                        peakReadThroughRateGPI.VC = new ValueContainer(peakThroughReadRate = SafeRate(bufferSize, ahReadThrough));
                        peakWriteThroughRateGPI.VC = new ValueContainer(peakThroughWriteRate = SafeRate(bufferSize, ahWriteThrough));

                        double peakNormalReadIOPs = SafeRate(1, ahReadNormal);
                        double peakReadThroughIOPs = SafeRate(1, ahReadThrough);
                        double peakWriteThroughIOPs = SafeRate(1, ahWriteThrough);
                        double avgNormalReadIOPs = SafeRate(ahReadNormal.Count, measuredAggregationInterval);
                        double avgReadThroughIOPs = SafeRate(ahReadThrough.Count, measuredAggregationInterval);
                        double avgWriteThroughIOPs = SafeRate(ahWriteThrough.Count, measuredAggregationInterval);

                        if (firstAHLog)
                        {
                            noMDRFLogger.Info.Emit("ahWriteThroughBoundaries: {0}", ahWriteThrough.BinBoundaryArrayVC);
                            firstAHLog = false;
                        }

                        noMDRFLogger.Info.Emit("TotalBytes:{0} Mb ahWriteThrough: {1}", totalBytesWritten * (1.0 / (1024 * 1024)), ahWriteThrough.ToString(Histogram.TSInclude.BaseWithMedEst));
                        noMDRFLogger.Info.Emit("ahWriteThrough Counts: {0}", ahWriteThrough.BinCountArrayVC);
                        noMDRFLogger.Info.Emit("PeakRates (k/s): rdNormal:{0:f1} rdThrough:{1:f1} wrThrough:{2:f1}", peakNormalReadRate * (1.0 / 1024), peakThroughReadRate * (1.0 / 1024), peakThroughWriteRate * (1.0 / 1024));
                        noMDRFLogger.Info.Emit("IOPs (rn, rt, wt): peak: {0:f1} {1:f1} {2:f1}, avg: {3:f1} {4:f1} {5:f1}", peakNormalReadIOPs, peakReadThroughIOPs, peakWriteThroughIOPs, avgNormalReadIOPs, avgReadThroughIOPs, avgWriteThroughIOPs);
                    }

                    mdrfWriter.RecordGroups();

                    if (aggregationIntervalTimerTriggered)
                    {
                        foreach (var t in accumTupleArray)
                            t.Item2.Histogram.Clear();
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit("{0} generated unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }
        }

        private static double SafeRate(long count, Histogram h)
        {
            double avgTime = h.Average;

            return (avgTime > 0.0) ? (count / avgTime) : 0.0;
        }

        private static double SafeRate(long count, TimeSpan ts)
        {
            double avgTime = ts.TotalSeconds;

            return (avgTime > 0.0) ? (count / avgTime) : 0.0;
        }

        FileStream fsWriteThrough;
        FileStream fsReadThrough;
        FileStream fsReadNormal;

        const int bufferSize = 8192;

        const int numWriteBuffers = 37;
        const int numWriteBuffersPerGroup = 3;    // 3 * 8192 = approx 0.024 Mbytes per group, 2 groups per second gives 6 IOPs per second and 0.049 MBytes per second sustained write rate (or 4.2 GBytes per day or 1.6 TBytes per year)
        int nextWriteBufferSelect = 0;
        byte[][] writeBufferArray = Enumerable.Range(0, numWriteBuffers).Select(ignore => MakeWriteBuffer(bufferSize)).ToArray();

        const int numReadBuffers = 16;
        const int numReadBuffersPerGroup = 29;  // 29 * 8192 = approx 0.238 MBytes per group, 2 groups per seconds gives 0.475 MBytes per second sustained read rate
        int nextReadBufferSelect = 0;
        byte[][] readBufferArray = Enumerable.Range(0, numReadBuffers).Select(ignore => new byte[bufferSize]).ToArray();

        const int fileSize = 30 * 1024 * 1024;  // 30 mbytes
        static readonly int fileSizeInBuffers = fileSize / bufferSize;  // 3840

        const int writeStride = 887;      // at 3840 buffers in the file we get 4.32 forward stride writes per pass through the file then it wraps back...
        int nextWriteOffset = 0;

        const int readStride = 839;       // at 3840 buffers in the file we get 4.33 forward stride reads per pass through the file and then it wraps back...
        int nextReadOffset = 0;

        private void CreateFileIfNeeded()
        {
            if (fsWriteThrough != null)
                return;

            try
            {
                string testFilePath = Config.TestFilePath;
                if (System.IO.File.Exists(testFilePath))
                    System.IO.File.Delete(testFilePath);

                FileOptions optional_FILE_FLAG_NO_BUFFERING = Config.UseFileFlagNoBuffering ? unchecked((FileOptions)0x20000000) : FileOptions.None;

                FileOptions writeThroughOptions = (FileOptions.WriteThrough | optional_FILE_FLAG_NO_BUFFERING | FileOptions.RandomAccess);
                FileOptions readNoBufferingOptions = (optional_FILE_FLAG_NO_BUFFERING | FileOptions.RandomAccess);
                FileOptions readNormalOptions = (FileOptions.RandomAccess);

                fsWriteThrough = new FileStream(testFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite, bufferSize, writeThroughOptions);
                fsWriteThrough.SetLength(fileSize);

                QpcTimeStamp sweepStartTime = QpcTimeStamp.Now;

                for (int outerSweepIdx = 0; outerSweepIdx < fileSizeInBuffers; outerSweepIdx += numWriteBuffersPerGroup)
                {
                    WriteGroupOfBuffersToFile(fsWriteThrough, hCreateWrite);
                }

                createElapsed = QpcTimeStamp.Now - sweepStartTime;
                createBytesWritten = totalBytesWritten;
                totalBytesWritten = 0;

                Log.Info.Emit("File'{0}' created: bytes:{1} rate:{2:f1} k/s hCreate:{3}", testFilePath, fileSize, SafeRate(createBytesWritten, createElapsed) * (1.0 / 1024), hCreateWrite.ToString(Histogram.TSInclude.BaseWithMedEst));        // screen and MDRF
                Log.Info.Emit("hCreate Counts: {0}", hCreateWrite.BinCountArrayVC);        // screen and MDRF
                Log.Info.Emit("hCreate Boundaries: {0}", hCreateWrite.BinBoundaryArrayVC);        // screen and MDRF

                fsReadNormal = new FileStream(testFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, readNormalOptions);

                hWriteThrough.Clear();
                WriteGroupOfBuffersToFile(fsWriteThrough, hWriteThrough);
                double avgCreateWriteThroughTestIOPs = SafeRate(hCreateWrite.Count, hCreateWrite.Sum.FromSeconds());
                double avgWriteThroughTestIOPs = SafeRate(hWriteThrough.Count, hWriteThrough.Sum.FromSeconds());
                bool driveCanBeUsedToSupportReadThrough = (Config.DisableReadThroughUseRandomWriteIOPsThreshold == 0.0 || (avgCreateWriteThroughTestIOPs >= Config.DisableReadThroughUseRandomWriteIOPsThreshold && avgWriteThroughTestIOPs >= Config.DisableReadThroughUseRandomWriteIOPsThreshold));

                if (driveCanBeUsedToSupportReadThrough)
                    Log.Debug.Emit("Assuming SSD [measuredIOPs cr:{0} and wr:{1} >= threshold:{2}]", avgCreateWriteThroughTestIOPs, avgWriteThroughTestIOPs, Config.DisableReadThroughUseRandomWriteIOPsThreshold);
                else
                    Log.Debug.Emit("Assuming HDD (no readThrough testing) [measuredIOPs cr:{0} or wr:{1} < threshold:{2}]", avgCreateWriteThroughTestIOPs, avgWriteThroughTestIOPs, Config.DisableReadThroughUseRandomWriteIOPsThreshold);

                if (Config.UseFileFlagNoBuffering && driveCanBeUsedToSupportReadThrough)
                    fsReadThrough = new FileStream(testFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, readNoBufferingOptions);
            }
            catch (System.Exception ex)
            {
                Log.Debug.Emit("{0} generated unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

        private void WriteGroupOfBuffersToFile(FileStream fs, Histogram perBuffer)
        {
            try
            {
                QpcTimeStamp now = QpcTimeStamp.Now;
                QpcTimeStamp perGroupStartTime = now;

                for (int groupIdx = 0; groupIdx < numWriteBuffersPerGroup; groupIdx++)
                {
                    byte[] buffer = writeBufferArray[nextWriteBufferSelect];
                    nextWriteBufferSelect = (nextWriteBufferSelect + 1) % numWriteBuffers;

                    nextWriteOffset = (nextWriteOffset + bufferSize * writeStride) % fileSize;

                    QpcTimeStamp perBufferStartTime = now;

                    fs.Seek(nextWriteOffset, SeekOrigin.Begin);
                    fs.Write(buffer, 0, bufferSize);

                    perBuffer.Add((now.SetToNow() - perBufferStartTime).TotalSeconds);

                    totalBytesWritten += bufferSize;
                }
            }
            catch (System.Exception ex)
            {
                Log.Debug.Emit("{0} generated unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

        private void ReadGroupOfBuffersFromFile(FileStream fs, Histogram perBuffer)
        {
            try
            {
                QpcTimeStamp now = QpcTimeStamp.Now;
                QpcTimeStamp perGroupStartTime = now;

                for (int groupIdx = 0; groupIdx < numReadBuffersPerGroup; groupIdx++)
                {
                    byte[] buffer = readBufferArray[nextReadBufferSelect];
                    nextReadBufferSelect = (nextReadBufferSelect + 1) % numReadBuffers;

                    nextReadOffset = (nextReadOffset + bufferSize * readStride) % fileSize;

                    QpcTimeStamp perBufferStartTime = now;

                    fs.Seek(nextWriteOffset, SeekOrigin.Begin);
                    fs.Read(buffer, 0, bufferSize);

                    perBuffer.Add((now.SetToNow() - perBufferStartTime).TotalSeconds);
                }
            }
            catch (System.Exception ex)
            {
                Log.Debug.Emit("{0} generated unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

        static double bufferSeed = 0.0;

        private static byte[] MakeWriteBuffer(int bufferSize)
        {
            byte [] buffer = new byte [bufferSize];

            int numDoubles = (bufferSize >> 3);

            double[] dArray = Enumerable.Range(1, numDoubles).Select(n => n * Math.PI + bufferSeed).ToArray();

            System.Buffer.BlockCopy(dArray, 0, buffer, 0, bufferSize);

            bufferSeed += (Math.E * 0.5);

            return buffer;
        }
    }
}

//-------------------------------------------------------------------
