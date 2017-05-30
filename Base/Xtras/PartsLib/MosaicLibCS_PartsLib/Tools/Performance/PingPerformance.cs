//-------------------------------------------------------------------
/*! @file PingPerformance.cs
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
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Tools.Performance
{
    public class PingPerformancePartConfig
    {
        public PingPerformancePartConfig(ulong aggregateGroupsFileIndexUserRowFlagBits = 0)
        {
            AggregateGroupsFileIndexUserRowFlagBits = aggregateGroupsFileIndexUserRowFlagBits;
            SampleInterval = (0.333).FromSeconds();
            AggregationInterval = (30.0).FromSeconds();
            ResponseTimeLimit = (1.0).FromSeconds();
            ExtraLength = 0;
            PingTargetArray = emptyStringArray;
        }

        public PingPerformancePartConfig(PingPerformancePartConfig other)
        {
            AggregateGroupsFileIndexUserRowFlagBits = other.AggregateGroupsFileIndexUserRowFlagBits;
            SampleInterval = other.SampleInterval;
            AggregationInterval = other.AggregationInterval;
            ResponseTimeLimit = other.ResponseTimeLimit;
            ExtraLength = other.ExtraLength;
            PingTargetArray = other.PingTargetArray ?? emptyStringArray;
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
        public int ExtraLength { get; set; }

        public string[] PingTargetArray { get; set; }

        [ConfigItem(IsOptional = true, Name="PingTargetArray")]
        public ValueContainer PingTargetArrayVC 
        {
            get { return (PingTargetArray.SafeLength() <= 1 ? new ValueContainer(PingTargetArray.SafeAccess(0, string.Empty)) : new ValueContainer(PingTargetArray)); }
            set 
            {
                string[] strArray = value.GetValue<string[]>(false);
                string str = value.GetValue<string>(false);
                if (strArray == null && !str.IsNullOrEmpty())
                    strArray = str.Split(new[] { ' ', ',', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                PingTargetArray = strArray ?? emptyStringArray;
            }
        }

        public PingPerformancePartConfig Setup(string prefixName = "PingPerf.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            var adapter = new ConfigValueSetAdapter<PingPerformancePartConfig>(config) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixName);

            return this;
        }

        private static readonly string[] emptyStringArray = new string[0];
    }
    
    public class PingPerformancePart : SimpleActivePartBase
    {
        public PingPerformancePart(string partID, PingPerformancePartConfig config, PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion0.Build(waitTimeLimit: (0.10).FromSeconds(), goOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.All))
        {
            Config = new PingPerformancePartConfig(config);
            this.mdrfWriter = mdrfWriter;

            sampleIntervalTimer = new QpcTimer() { TriggerInterval = Config.SampleInterval, AutoReset = true }.Start();
            aggregationIntervalTimer = new QpcTimer() { TriggerInterval = Config.AggregationInterval, AutoReset = true }.Start();

            AddExplicitDisposeAction(() => Release());

            pingTrackerArray = Config.PingTargetArray.Select(hostNameOrAddress => new PingTracker(hostNameOrAddress, binBoundariesArray, Config, this)).ToArray();

            pingTrackerArray.DoForEach(pt => mdrfWriter.Add(pt.hGrp.GroupInfo));

            noMDRFLogger = new Logging.Logger(PartID).SetDefaultNamedValueSetForEmitter(Logging.LogGate.All, new NamedValueSet() { { "noMDRF" } });
        }

        void Release()
        {
            if (!pingTrackerArray.IsNullOrEmpty())
                pingTrackerArray.DoForEach(pt => pt.Release());
        }

        PingPerformancePartConfig Config { get; set; }
        PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter;
        Logging.IBasicLogger noMDRFLogger;

        QpcTimer sampleIntervalTimer, aggregationIntervalTimer;

        private static readonly double[] binBoundariesArray = new double[] { 0.0005, 0.0008, 0.001, 0.002, 0.003, 0.004, 0.006, 0.008, 0.01, 0.02, 0.03, 0.04, 0.06, 0.08, 0.1, 0.2, 0.3, 0.4, 0.6, 0.8, 1.0 };

        private PingTracker [] pingTrackerArray;

        private class PingTracker
        {
            public PingTracker(string hostNameOrAddress, double[] binBoundariesArray, PingPerformancePartConfig config, INotifyable notifyOnDone)
            {
                HostNameOrAddress = hostNameOrAddress;
                NotifyOnDone = notifyOnDone;

                // calling GetHostAddresses can throw for many reasons (invalid app.config for example...)
                Func<System.Net.IPAddress []> getHostAddressesDelegate = () => System.Net.Dns.GetHostAddresses(hostNameOrAddress);
                IPAddressArray = getHostAddressesDelegate.TryGet();

                IPAddress = IPAddressArray.SafeAccess(0, System.Net.IPAddress.None);
                h = new Histogram(binBoundariesArray);

                timeoutCountGPI = new MDRF.Writer.GroupPointInfo() { Name = "timeoutCount", ValueCST = ContainerStorageType.UInt64, VC = new ValueContainer(0L) };
                failureCountGPI = new MDRF.Writer.GroupPointInfo() { Name = "failureCount", ValueCST = ContainerStorageType.UInt64, VC = new ValueContainer(0L) };

                hGrp = new MDRFHistogramGroupSource("hPing_{0}".CheckedFormat(HostNameOrAddress),
                                                    h,
                                                    (ulong)config.AggregateGroupsFileIndexUserRowFlagBits,
                                                    extraClientNVS: new NamedValueSet() { { "Ping" },  { "Host", HostNameOrAddress }, { "IPAddress", IPAddress.ToString() } },
                                                    extraGPISet: new[] { timeoutCountGPI, failureCountGPI });

                extraData = new byte[config.ExtraLength];

                responseTimeLimitInMSec = unchecked((int) config.ResponseTimeLimit.TotalMilliseconds);
                responseWaitTimeLimit = (config.ResponseTimeLimit.TotalSeconds + 0.25).FromSeconds();
            }

            public void Release()
            {
                Fcns.DisposeOfObject(ref ping);
            }

            public string HostNameOrAddress { get; private set; }
            public INotifyable NotifyOnDone { get; private set; }

            public System.Net.IPAddress [] IPAddressArray { get; private set; }
            public System.Net.IPAddress IPAddress { get; private set; }

            public byte [] extraData;
            public int responseTimeLimitInMSec;
            public TimeSpan responseWaitTimeLimit;

            public Histogram h;
            public ulong timeoutCount = 0;
            public ulong failureCount = 0;
            public System.Net.NetworkInformation.IPStatus pingIPStatus = default(System.Net.NetworkInformation.IPStatus);
            public System.Net.NetworkInformation.IPStatus lastPingIPStatus = default(System.Net.NetworkInformation.IPStatus);

            public MDRFHistogramGroupSource hGrp;
            public PartsLib.Tools.MDRF.Writer.GroupPointInfo timeoutCountGPI, failureCountGPI;

            System.Net.NetworkInformation.Ping ping = null;

            readonly object mutex = new object();

            Queue<PingUserToken> tokenQueue = new Queue<PingUserToken>();

            PingUserToken Token 
            {
                get { lock (mutex) { return (tokenQueue.Count <= 0) ? new PingUserToken() : tokenQueue.Dequeue(); } }
                set { if (value != null) { lock (mutex) { tokenQueue.Enqueue(value); } } }
            }

            public class PingUserToken
            {
                public QpcTimeStamp sendTimeStamp;
                public PingUserToken SetToNow() { sendTimeStamp.SetToNow(); return this; }
            }

            public void Service(bool sampleTimerTriggered, Logging.IBasicLogger logger)
            {
                if (currentUserToken != null)
                {
                    TimeSpan currentElapsedTime = currentUserToken.sendTimeStamp.Age;

                    if (pingCompleteEventArgs != null)
                    {
                        // process ping response
                        if (!pingCompleteEventArgs.Cancelled && pingCompleteEventArgs.Error == null && pingCompleteEventArgs.Reply != null)
                        {
                            switch (pingIPStatus = pingCompleteEventArgs.Reply.Status)
                            {
                                case System.Net.NetworkInformation.IPStatus.Success: 
                                    h.Add(pingToReplyElapsedTime.TotalSeconds); 
                                    break;
                                case System.Net.NetworkInformation.IPStatus.TimedOut: 
                                    timeoutCount++; 
                                    timeoutCountGPI.VC = new ValueContainer(timeoutCount);
                                    break;
                                default: 
                                    failureCount++; 
                                    failureCountGPI.VC = new ValueContainer(failureCount);
                                    break;
                            }
                        }
                        else
                        {
                            pingIPStatus = System.Net.NetworkInformation.IPStatus.Unknown;
                            failureCount++;
                            failureCountGPI.VC = new ValueContainer(failureCount);
                        }

                        ReleaseToken();
                    }
                    else if (currentElapsedTime >= responseWaitTimeLimit)
                    {
                        pingIPStatus = System.Net.NetworkInformation.IPStatus.Unknown;
                        failureCount++;
                        failureCountGPI.VC = new ValueContainer(failureCount);

                        AbortCurrentPingRequest();
                    }

                    if (lastPingIPStatus != pingIPStatus)
                    {
                        if (pingIPStatus != System.Net.NetworkInformation.IPStatus.Success)
                            logger.Info.Emit("Ping to '{0}' [{1}] failed: {2} after {3:f6} sec", HostNameOrAddress, IPAddress, pingIPStatus, currentElapsedTime.TotalSeconds);

                        lastPingIPStatus = pingIPStatus;
                    }
                }

                if (sampleTimerTriggered && (currentUserToken == null))
                    SendNextPingRequest(logger);
            }

            private void ReleaseToken()
            {
                lock (mutex)
                {
                    Token = currentUserToken;
                    currentUserToken = null;
                    pingCompleteEventArgs = null;
                }
            }

            volatile PingUserToken currentUserToken = null;
            volatile System.Net.NetworkInformation.PingCompletedEventArgs pingCompleteEventArgs = null;
            TimeSpan pingToReplyElapsedTime;

            public void SendNextPingRequest(Logging.IBasicLogger logger)
            {
                if (ping == null)
                {
                    ping = new System.Net.NetworkInformation.Ping();
                    ping.PingCompleted += ping_PingCompleted;
                }

                lock (mutex)
                {
                    pingCompleteEventArgs = null;
                    pingToReplyElapsedTime = TimeSpan.Zero;

                    currentUserToken = Token.SetToNow();

                    try
                    {
                        ping.SendAsync(IPAddress, responseTimeLimitInMSec, extraData, currentUserToken);
                    }
                    catch (System.Exception ex)
                    {
                        logger.Trace.Emit("{0} (SendAsync) generated unexpected exception: {1}", CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage));
                    }
                }
            }

            void AbortCurrentPingRequest()
            {
                lock (mutex)
                {
                    currentUserToken = null;
                }

                try
                {
                    if (ping != null)
                        ping.SendAsyncCancel();
                }
                catch
                {}

                Fcns.DisposeOfObject(ref ping);
            }

            void ping_PingCompleted(object sender, System.Net.NetworkInformation.PingCompletedEventArgs e)
            {
                if (e != null)
                {
                    lock (mutex)
                    {
                        if (currentUserToken != null && Object.ReferenceEquals(e.UserState, currentUserToken as object))
                        {
                            pingCompleteEventArgs = e;
                            pingToReplyElapsedTime = currentUserToken.sendTimeStamp.Age;
                        }
                        else
                        {
                            unexpectedPingResponseCount++;
                        }
                    }
                }
            }

            volatile uint unexpectedPingResponseCount = 0;
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
                pingTrackerArray.DoForEach(pt => pt.Service(sampleIntervalTimerTriggered, Log));

                if (sampleIntervalTimerTriggered)
                {
                    bool aggregationIntervalTimerTriggered = aggregationIntervalTimer.IsTriggered;
                    if (aggregationIntervalTimerTriggered)
                    {
                        TimeSpan measuredAggregationInterval = aggregationIntervalTimer.ElapsedTimeAtLastTrigger;

                        pingTrackerArray.DoForEach(pt => pt.hGrp.UpdateGroupItems());

                        mdrfWriter.RecordGroups();

                        pingTrackerArray.DoForEach(pt => noMDRFLogger.Info.Emit("Ping '{0}' [{1}] results: {2} to:{3} fails:{4}", pt.HostNameOrAddress, pt.IPAddress, pt.h.ToString(Histogram.TSInclude.BaseWithMedEst), pt.timeoutCount, pt.failureCount));
                        pingTrackerArray.DoForEach(pt => pt.h.Clear());
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
