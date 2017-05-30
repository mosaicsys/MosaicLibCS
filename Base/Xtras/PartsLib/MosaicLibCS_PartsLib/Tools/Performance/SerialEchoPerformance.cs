//-------------------------------------------------------------------
/*! @file SerialEchoPerformance.cs
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

using MosaicLib;
using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.SerialIO;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Tools.Performance
{
    public class SerialEchoPerformancePartConfig
    {
        public SerialEchoPerformancePartConfig(ulong aggregateGroupsFileIndexUserRowFlagBits = 0)
        {
            AggregateGroupsFileIndexUserRowFlagBits = aggregateGroupsFileIndexUserRowFlagBits;
            SampleInterval = (0.333).FromSeconds();
            AggregationInterval = (30.0).FromSeconds();
            ResponseTimeLimit = (1.0).FromSeconds();
            TestDataLength = 100;
            TestDataPatternCount = 8;
            PortTargetSpecArray = emptyStringArray;
        }

        public SerialEchoPerformancePartConfig(SerialEchoPerformancePartConfig other)
        {
            AggregateGroupsFileIndexUserRowFlagBits = other.AggregateGroupsFileIndexUserRowFlagBits;
            SampleInterval = other.SampleInterval;
            AggregationInterval = other.AggregationInterval;
            ResponseTimeLimit = other.ResponseTimeLimit;
            TestDataLength = Math.Max(16, other.TestDataLength);
            TestDataPatternCount = Math.Max(1, other.TestDataPatternCount);
            PortTargetSpecArray = other.PortTargetSpecArray ?? emptyStringArray;
        }

        [ConfigItem(IsOptional = true)]
        public ulong AggregateGroupsFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan SampleInterval { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan AggregationInterval { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan ResponseTimeLimit { get; set; }

        [ConfigItem(IsOptional = true)]
        public int TestDataLength { get; set; }

        [ConfigItem(IsOptional = true)]
        public int TestDataPatternCount { get; set; }

        public string[] PortTargetSpecArray { get; set; }

        [ConfigItem(IsOptional = true, Name="PortTargetSpecArray")]
        public ValueContainer PortTargetSpecArrayVC 
        {
            get { return (PortTargetSpecArray.SafeLength() <= 1 ? new ValueContainer(PortTargetSpecArray.SafeAccess(0, string.Empty)) : new ValueContainer(PortTargetSpecArray)); }
            set 
            {
                string[] strArray = value.GetValue<string[]>(false);
                string str = value.GetValue<string>(false);
                if (strArray == null && !str.IsNullOrEmpty())
                    strArray = str.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                PortTargetSpecArray = strArray ?? emptyStringArray;
            }
        }

        public SerialEchoPerformancePartConfig Setup(string prefixName = "SIOEchoPerf.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            var adapter = new ConfigValueSetAdapter<SerialEchoPerformancePartConfig>(config) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixName);

            return this;
        }

        private static readonly string[] emptyStringArray = new string[0];
    }
    
    public class SerialEchoPerformancePart : SimpleActivePartBase
    {
        public SerialEchoPerformancePart(string partID, SerialEchoPerformancePartConfig config, PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion0.Build(waitTimeLimit: (0.10).FromSeconds(), goOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.All))
        {
            Config = new SerialEchoPerformancePartConfig(config);
            this.mdrfWriter = mdrfWriter;

            sampleIntervalTimer = new QpcTimer() { TriggerInterval = Config.SampleInterval, AutoReset = true }.Start();
            aggregationIntervalTimer = new QpcTimer() { TriggerInterval = Config.AggregationInterval, AutoReset = true }.Start();

            AddExplicitDisposeAction(() => Release());

            int targetCount = 1;
            serialEchoTrackerArray = Config.PortTargetSpecArray.Select(portTargetSpec => new SerialEchoTracker("SerialEchoPort_{0:d2}".CheckedFormat(targetCount++), portTargetSpec, binBoundariesArray, Config, this, Log)).ToArray();

            serialEchoTrackerArray.DoForEach(pt => mdrfWriter.Add(pt.hGrp.GroupInfo));

            noMDRFLogger = new Logging.Logger(PartID).SetDefaultNamedValueSetForEmitter(Logging.LogGate.All, new NamedValueSet() { { "noMDRF" } });
        }

        void Release()
        {
            if (!serialEchoTrackerArray.IsNullOrEmpty())
                serialEchoTrackerArray.DoForEach(pt => pt.Release());
        }

        SerialEchoPerformancePartConfig Config { get; set; }
        PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter;
        Logging.IBasicLogger noMDRFLogger;

        QpcTimer sampleIntervalTimer, aggregationIntervalTimer;

        private static readonly double[] binBoundariesArray = new double[] { 0.0001, 0.0002, 0.0003, 0.0004, 0.0006, 0.0008, 0.001, 0.002, 0.003, 0.004, 0.006, 0.008, 0.01, 0.02, 0.03, 0.04, 0.06, 0.08, 0.1, 0.2, 0.3, 0.4, 0.6, 0.8, 1.0 };

        private SerialEchoTracker [] serialEchoTrackerArray;

        private class SerialEchoTracker
        {
            public SerialEchoTracker(string portPartID, string portTargetSpec, double[] binBoundariesArray, SerialEchoPerformancePartConfig config, INotifyable notifyOnDone, Logging.IBasicLogger logger)
            {
                PortTargetSpec = portTargetSpec;
                Config = config;
                NotifyOnDone = notifyOnDone;
                Logger = logger;

                h = new Histogram(binBoundariesArray);

                timeoutCountGPI = new MDRF.Writer.GroupPointInfo() { Name = "timeoutCount", ValueCST = ContainerStorageType.UInt64, VC = new ValueContainer(0L) };
                failureCountGPI = new MDRF.Writer.GroupPointInfo() { Name = "failureCount", ValueCST = ContainerStorageType.UInt64, VC = new ValueContainer(0L) };

                hGrp = new MDRFHistogramGroupSource("{0}".CheckedFormat(portPartID),
                                                    h,
                                                    (ulong)config.AggregateGroupsFileIndexUserRowFlagBits,
                                                    extraClientNVS: new NamedValueSet() { { "SerialEcho" },  { "PortTargetSpec", PortTargetSpec } },
                                                    extraGPISet: new[] { timeoutCountGPI, failureCountGPI });

                try 
                {
                    portConfig = new PortConfig(portPartID, portTargetSpec)
                    {
                        TxLineTerm = LineTerm.None,
                        RxLineTerm = LineTerm.CR,
                        ConnectTimeout = (2.0).FromSeconds(),
                        WriteTimeout = (1.0).FromSeconds(),
                        ReadTimeout = (1.0).FromSeconds(),
                        IdleTime = (1.0).FromSeconds(),
                        EnableAutoReconnect = true,
                    };

                    port = MosaicLib.SerialIO.Factory.CreatePort(portConfig);

                    portGetNextPacketAction = port.CreateGetNextPacketAction();
                    portFlushAction = port.CreateFlushAction();
                    portWriteAction = port.CreateWriteAction(portWriteActionParam = new WriteActionParam());
                }
                catch (System.Exception ex)
                {
                    Logger.Error.Emit("Port setup for '{0}' failed: {1}", portTargetSpec, ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }

            public void Release()
            {
                Fcns.DisposeOfObject(ref port);
            }

            public string PortTargetSpec { get; private set; }
            public SerialEchoPerformancePartConfig Config { get; private set; } 
            public INotifyable NotifyOnDone { get; private set; }
            public Logging.IBasicLogger Logger { get; private set; }

            public PortConfig portConfig;
            public IPort port;
            public IGetNextPacketAction portGetNextPacketAction;
            public IFlushAction portFlushAction;
            public IWriteAction portWriteAction;
            public WriteActionParam portWriteActionParam;

            public string currentTestPattern = null;

            public State state;
            public QpcTimeStamp stateTimeStamp;

            public TimeSpan lastEchoTestPeriod;
            public bool lastEchoTestSucceeded;

            public enum State : int
            {
                RequestFlush = 0,
                WaitUntilForFlushComplete,
                WriteTestPattern,
                WaitForEcho,
                EchoTestSucceeded,
                EchoTestFailed,
            }

            public void SetState(State nextState, string reason)
            {
                State entryState = state;

                state = nextState;
                stateTimeStamp.SetToNow();

                if (state != entryState)
                    Logger.Trace.Emit("{0}: State is now {1} [from {2}]", portConfig.Name, state, entryState);
            }

            public Queue<string> testDataPatternQueue = new Queue<string>();
            public string GetNextTestDataPattern()
            {
                string result = null;
                if (testDataPatternQueue.Count >= Config.TestDataPatternCount)
                    result = testDataPatternQueue.Dequeue();
    
                if (result == null)
                {
                    Random rng = new Random();
                    int patternBuilderStrLen = patternBuilderStr.Length;
                    result = new String(Enumerable.Range(1, Config.TestDataLength - 1).Select(ignore => patternBuilderStr[rng.Next(0, patternBuilderStrLen)]).Concat(new [] { '\r' }).ToArray());
                }

                testDataPatternQueue.Enqueue(result);

                return result;
            }
            public static readonly string patternBuilderStr = @"0123456789-=~!@#$%^&*()_+[]\{}|;':,./<>?abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            
            public Histogram h;
            public ulong timeoutCount = 0;
            public ulong failureCount = 0;

            public MDRFHistogramGroupSource hGrp;
            public PartsLib.Tools.MDRF.Writer.GroupPointInfo timeoutCountGPI, failureCountGPI;

            public void Service(bool sampleTimerTriggered)
            {
                TimeSpan stateAge = stateTimeStamp.Age;
                string ec = string.Empty;

                for (;;)
                {
                    switch (state)
                    {
                        case State.RequestFlush:
                            if (!port.BaseState.IsConnected)
                                ec = port.CreateGoOnlineAction(andInitialize: true).Run();

                            if (ec.IsNullOrEmpty())
                                ec = portFlushAction.Start();

                                if (ec.IsNullOrEmpty())
                                SetState(State.WaitUntilForFlushComplete, "Flush requested");
                            else
                                SetState(State.EchoTestFailed, "Flush.Start failed: {0}".CheckedFormat(ec));

                            return;

                        case State.WaitUntilForFlushComplete:
                            if (portFlushAction.ActionState.IsComplete)
                                SetState(State.WriteTestPattern, "Flush {0}".CheckedFormat(portFlushAction.ActionState.ToString()));

                            return;

                        case State.WriteTestPattern:
                            portWriteActionParam.BufferAsStr = currentTestPattern = GetNextTestDataPattern();

                            ec = portWriteAction.Start();

                            if (ec.IsNullOrEmpty())
                                SetState(State.WaitForEcho, "Write requested");
                            else
                                SetState(State.EchoTestFailed, "Write.Start failed: {0}".CheckedFormat(ec));

                            return;

                        case State.WaitForEcho:
                            lastEchoTestPeriod = stateAge;

                            if (port.HasPacket)
                            {
                                ec = portGetNextPacketAction.Run();
                                Packet p = portGetNextPacketAction.ResultValue;

                                if (ec.IsNullOrEmpty() && p == null)
                                    ec = "successful {0} action gave null packet".CheckedFormat(portGetNextPacketAction.ToString(ToStringSelect.JustMesg));

                                if (ec.IsNullOrEmpty() && !p.IsData)
                                    ec = "successful {0} action gave unexpected packet '{1}'".CheckedFormat(portGetNextPacketAction.ToString(ToStringSelect.JustMesg), p);

                                if (ec.IsNullOrEmpty() && p.DataStr != currentTestPattern)
                                    ec = "Received unexpected echo [rx:'{0}' != expected:'{1}']".CheckedFormat(p.DataStr, currentTestPattern);

                                if (ec.IsNullOrEmpty())
                                {
                                    h.Add(lastEchoTestPeriod.TotalSeconds);
                                    SetState(State.EchoTestSucceeded, "Echo test succeeded after {0:f6} sec".CheckedFormat(stateAge.TotalSeconds));
                                    return;
                                }
                                else
                                {
                                    failureCount++;
                                    SetState(State.EchoTestFailed, "Echo response failed: {0} [after {0:f6} sec]".CheckedFormat(stateAge.TotalSeconds));
                                    return;
                                }
                            }
                            else if (stateAge > Config.ResponseTimeLimit)
                            {
                                timeoutCount++;
                                SetState(State.EchoTestFailed, "Echo response time limit reached after {0:f6} sec".CheckedFormat(stateAge.TotalSeconds));
                                return;
                            }

                            break;

                        case State.EchoTestSucceeded:
                            if (!lastEchoTestSucceeded)
                            {
                                Logger.Debug.Emit("{0}: Echo test on '{1}' succeeded after {2:f6} seconds [Prior test failed]", portConfig.Name, PortTargetSpec, lastEchoTestPeriod.TotalSeconds);

                                lastEchoTestSucceeded = true;
                            }

                            if (!sampleTimerTriggered)
                                return;

                            SetState(State.WriteTestPattern, "Starting next sample");

                            break;

                        case State.EchoTestFailed:
                            if (lastEchoTestSucceeded)
                            {
                                Logger.Debug.Emit("{0}: Echo test on '{1}' failed after {2:f6} seconds [Prior test succeeded]", portConfig.Name, PortTargetSpec, lastEchoTestPeriod.TotalSeconds);

                                lastEchoTestSucceeded = true;
                            }

                            if (!sampleTimerTriggered)
                                return;

                            SetState(State.RequestFlush, "Starting next sample (with Flush first)");

                            break;

                        default:
                            Logger.Error.Emit("{0}: Reached unexpected state {1}", portConfig.Name, state);

                            SetState(State.RequestFlush, "Recovery from unknown state");

                            break;
                    }
                }
            }
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

            try
            {
                serialEchoTrackerArray.DoForEach(pt => pt.Service(sampleIntervalTimerTriggered));

                if (sampleIntervalTimerTriggered)
                {
                    bool aggregationIntervalTimerTriggered = aggregationIntervalTimer.IsTriggered;
                    if (aggregationIntervalTimerTriggered)
                    {
                        TimeSpan measuredAggregationInterval = aggregationIntervalTimer.ElapsedTimeAtLastTrigger;

                        serialEchoTrackerArray.DoForEach(pt => pt.hGrp.UpdateGroupItems());

                        mdrfWriter.RecordGroups();

                        serialEchoTrackerArray.DoForEach(pt => noMDRFLogger.Info.Emit("Echo '{0}' results: {1} to:{2} fails:{3}", pt.PortTargetSpec, pt.h.ToString(Histogram.TSInclude.BaseWithMedEst), pt.timeoutCount, pt.failureCount));
                        serialEchoTrackerArray.DoForEach(pt => pt.h.Clear());
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Debug.Emit("{0} generated unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }
    }
}

//-------------------------------------------------------------------
