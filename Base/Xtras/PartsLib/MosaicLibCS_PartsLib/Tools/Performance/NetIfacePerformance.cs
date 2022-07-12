//-------------------------------------------------------------------
/*! @file NetIfacePerformance.cs
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
using System.Net.NetworkInformation;
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
    public class NetIfacePerformancePartConfig
    {
        public NetIfacePerformancePartConfig(ulong statisticsGroupsFileIndexUserRowFlagBits = 0, ulong changeOccurrenceFileIndexUserRowFlagBits = 0)
        {
            StatisticsGroupsFileIndexUserRowFlagBits = statisticsGroupsFileIndexUserRowFlagBits;
            ChangeOccurrenceFileIndexUserRowFlagBits = changeOccurrenceFileIndexUserRowFlagBits;
            SampleRecordingInterval = (10.0).FromSeconds();
        }

        public NetIfacePerformancePartConfig(NetIfacePerformancePartConfig other)
        {
            StatisticsGroupsFileIndexUserRowFlagBits = other.StatisticsGroupsFileIndexUserRowFlagBits;
            ChangeOccurrenceFileIndexUserRowFlagBits = other.ChangeOccurrenceFileIndexUserRowFlagBits;
            SampleRecordingInterval = other.SampleRecordingInterval;
        }

        [ConfigItem(IsOptional = true)]
        public ulong StatisticsGroupsFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public ulong ChangeOccurrenceFileIndexUserRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public TimeSpan SampleRecordingInterval { get; set; }

        public NetIfacePerformancePartConfig Setup(string prefixName = "NetIface.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            var adapter = new ConfigValueSetAdapter<NetIfacePerformancePartConfig>(config) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixName);

            return this;
        }
    }

    public class NetIfacePerformancePart : SimpleActivePartBase
    {
        public NetIfacePerformancePart(string partID, NetIfacePerformancePartConfig config, PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion0.Build(waitTimeLimit: (0.10).FromSeconds(), goOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.All))
        {
            Config = new NetIfacePerformancePartConfig(config);
            this.mdrfWriter = mdrfWriter;

            Log.SetDefaultNamedValueSetForEmitter(Logging.MesgType.All, Defaults.PerfLoggerDefaultNVS);

            sampleRecordingIntervalTimer = new QpcTimer() { TriggerInterval = config.SampleRecordingInterval, AutoReset = true }.Start();
            sampleIntervalTimer = new QpcTimer() { TriggerInterval = config.SampleRecordingInterval.Min((0.5).FromSeconds()), AutoReset = true }.Start();

            netIfaceSettingChangeOccurrence = new MDRF.Writer.OccurrenceInfo() 
            { 
                Name = "{0}.SettingChange".CheckedFormat(PartID),
                Comment = "Used to record all network adapter setting changes",
                FileIndexUserRowFlagBits = Config.ChangeOccurrenceFileIndexUserRowFlagBits 
            };

            netIfaceStatusChangeOccurrence = new MDRF.Writer.OccurrenceInfo()
            {
                Name = "{0}.StatusChange".CheckedFormat(PartID),
                Comment = "Used to record all network adapter state changes",
                FileIndexUserRowFlagBits = Config.ChangeOccurrenceFileIndexUserRowFlagBits
            };

            mdrfWriter.Add(netIfaceSettingChangeOccurrence, netIfaceStatusChangeOccurrence);

            NetworkInterface[] netInterfacesArray = NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();

            netIfaceTrackerArray = netInterfacesArray.Select((netIface, idx) => new NetIfaceTracker(PartID, netIface, idx + 1, config.StatisticsGroupsFileIndexUserRowFlagBits)).ToArray();

            netIfaceTrackerArray.DoForEach(nit => netIfaceTrackerDictionary[nit.name] = nit);
            netIfaceTrackerArray.DoForEach(nit => mdrfWriter.Add(nit.groupInfo));
        }

        NetIfacePerformancePartConfig Config { get; set; }
        PartsLib.Tools.MDRF.Writer.IMDRFWriter mdrfWriter;

        QpcTimer sampleIntervalTimer;
        QpcTimer sampleRecordingIntervalTimer;

        MDRF.Writer.OccurrenceInfo netIfaceSettingChangeOccurrence;
        MDRF.Writer.OccurrenceInfo netIfaceStatusChangeOccurrence;

        NetIfaceTracker[] netIfaceTrackerArray;
        Dictionary<string, NetIfaceTracker> netIfaceTrackerDictionary = new Dictionary<string, NetIfaceTracker>();

        [Flags]
        enum DeltaType
        {
            None = 0x00,
            Settings = 0x01,
            Status = 0x02,
        }

        class NetIfaceTracker
        {
            public NetIfaceTracker(string partID, NetworkInterface netIface, int netIfaceNum, ulong statisticsGroupsFileIndexUserRowFlagBits)
            {
                originalNetIface = netIface;
                this.netIfaceNum = netIfaceNum;

                name = netIface.Name;

                groupInfo = new MDRF.Writer.GroupInfo()
                {
                    Name = "{0}.{1:d2}.Stats".CheckedFormat(partID, netIfaceNum),
                    FileIndexUserRowFlagBits = statisticsGroupsFileIndexUserRowFlagBits,
                    GroupBehaviorOptions = MDRF.Writer.GroupBehaviorOptions.UseVCHasBeenSetForTouched | MDRF.Writer.GroupBehaviorOptions.IncrSeqNumOnTouched,
                    GroupPointInfoArray = new [] 
                    {
                        linkUpGPI,
                        opStatGPI,
                        rxBytesGPI, txBytesGPI, 
                        rxUnicastPktsGPI, txUnicastPktsGPI, 
                        rxNonUnicastPktsGPI, txNonUnicastPktsGPI, 
                        txQueueLenGPI,
                        rxPktDiscardsGPI, rxPktErrorsGPI, rxPktUnkProtosGPI,
                        txPktDiscardsGPI, txPktErrorsGPI,
                        rxByteRateGPI, txByteRateGPI, 
                        rxUnicastPktRateGPI, txUnicastPktRateGPI,
                        rxNonUnicastPktRateGPI, txNonUnicastPktRateGPI,
                    },
                };
            }

            public NetworkInterface originalNetIface;
            public int netIfaceNum;
            public string name;
            public MDRF.Writer.GroupInfo groupInfo;

            public MDRF.Writer.GroupPointInfo linkUpGPI = new MDRF.Writer.GroupPointInfo() { Name = "linkUp", Comment = "", CST = ContainerStorageType.Boolean, VC = new ValueContainer(false) };
            public MDRF.Writer.GroupPointInfo opStatGPI = new MDRF.Writer.GroupPointInfo() { Name = "opState", Comment = "", CST = ContainerStorageType.None, VC = ValueContainer.Empty };
            public MDRF.Writer.GroupPointInfo rxBytesGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxBytes", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txBytesGPI = new MDRF.Writer.GroupPointInfo() { Name = "txBytes", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxUnicastPktsGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxUnicastPkts", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txUnicastPktsGPI = new MDRF.Writer.GroupPointInfo() { Name = "txUnicastPkts", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxNonUnicastPktsGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxNonUnicastPkts", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txNonUnicastPktsGPI = new MDRF.Writer.GroupPointInfo() { Name = "txNonUnicastPkts", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txQueueLenGPI = new MDRF.Writer.GroupPointInfo() { Name = "txQueueLen", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxPktDiscardsGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxPktDiscards", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxPktErrorsGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxPktErrors", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxPktUnkProtosGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxPktUnkProtos", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txPktDiscardsGPI = new MDRF.Writer.GroupPointInfo() { Name = "txPktDiscards", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txPktErrorsGPI = new MDRF.Writer.GroupPointInfo() { Name = "txPktErrors", Comment = "", CST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };

            public MDRF.Writer.GroupPointInfo rxByteRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxByteRate", Comment = "bytes per second", CST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo txByteRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "txByteRate", Comment = "bytes per second", CST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo rxUnicastPktRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxUnicastPktRate", Comment = "packets per second", CST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo txUnicastPktRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "txUnicastPktRate", Comment = "packets per second", CST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo rxNonUnicastPktRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxNonUnicastPktRate", Comment = "packets per second", CST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo txNonUnicastPktRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "txNonUnicastPktRate", Comment = "packets per second", CST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };

            long lastRxBytes = 0, lastTxBytes = 0, lastRxUnicastPkts = 0, lastTxUnicastPkts = 0, lastRxNonUnicastPkts = 0, lastTxNonUnicastPkts = 0;
            QpcTimeStamp lastQpcTimeStamp = QpcTimeStamp.Zero;

            public DeltaType Service(NetworkInterface netIface)
            {
                DeltaType deltaType = DeltaType.None;

                var ipIfaceProps = netIface.GetIPProperties();
                string[] unicastAddrStrArray = ipIfaceProps.UnicastAddresses.Select(ipAddr => ipAddr.Address.ToString()).ToArray();
                string physAddrStr = netIface.GetPhysicalAddress().SafeToString();
                var netIfaceType = netIface.NetworkInterfaceType;
                var supportsMCast = netIface.SupportsMulticast;

                if (!lastUnicastAddrStrArray.IsEqualTo(unicastAddrStrArray) || lastPhysAddrStr != physAddrStr || lastNetIfaceType != netIfaceType || lastSupportsMCast != supportsMCast)
                {
                    deltaType |= DeltaType.Settings;

                    lastUnicastAddrStrArray = unicastAddrStrArray;
                    lastPhysAddrStr = physAddrStr;
                    lastNetIfaceType = netIfaceType;
                    lastSupportsMCast = supportsMCast;
                }

                OperationalStatus opStat = netIface.OperationalStatus;
                bool linkUp = (opStat == OperationalStatus.Up);
                long speed = netIface.Speed;

                if (lastLinkUp != linkUp || lastOpStat != opStat || lastSpeed != speed)
                {
                    deltaType |= DeltaType.Status;
                    lastLinkUp = linkUp;
                    lastOpStat = opStat;
                    lastSpeed = speed;
                }

                var ipStats = netIface.GetIPv4Statistics();

                linkUpGPI.VC = ValueContainer.Empty.SetValue(linkUp);
                opStatGPI.VC = ValueContainer.Empty.SetValue(opStat);
                rxBytesGPI.VC = ValueContainer.CreateI8(ipStats.BytesReceived);
                txBytesGPI.VC = ValueContainer.CreateI8(ipStats.BytesSent);
                rxUnicastPktsGPI.VC = ValueContainer.CreateI8(ipStats.UnicastPacketsReceived);
                txUnicastPktsGPI.VC = ValueContainer.CreateI8(ipStats.UnicastPacketsSent);
                rxNonUnicastPktsGPI.VC = ValueContainer.CreateI8(ipStats.NonUnicastPacketsReceived);
                txNonUnicastPktsGPI.VC = ValueContainer.CreateI8(ipStats.NonUnicastPacketsSent);
                txQueueLenGPI.VC = ValueContainer.CreateI8(ipStats.OutputQueueLength);
                rxPktDiscardsGPI.VC = ValueContainer.CreateI8(ipStats.IncomingPacketsDiscarded);
                rxPktErrorsGPI.VC = ValueContainer.CreateI8(ipStats.IncomingPacketsWithErrors);
                rxPktUnkProtosGPI.VC = ValueContainer.CreateI8(ipStats.IncomingUnknownProtocolPackets);
                txPktDiscardsGPI.VC = ValueContainer.CreateI8(ipStats.OutgoingPacketsDiscarded);
                txPktErrorsGPI.VC = ValueContainer.CreateI8(ipStats.OutgoingPacketsWithErrors);

                QpcTimeStamp now = QpcTimeStamp.Now;
                double elapsedTimeInSeconds = (lastQpcTimeStamp.IsZero ? 0.0 : (now - lastQpcTimeStamp).TotalSeconds);
                double oneOverElapsedTimeInSeconds = (elapsedTimeInSeconds > 0.0) ? (1.0 / elapsedTimeInSeconds) : 0.0;

                rxByteRateGPI.VC = ValueContainer.CreateF4((float)((ipStats.BytesReceived - lastRxBytes) * oneOverElapsedTimeInSeconds));
                txByteRateGPI.VC = ValueContainer.CreateF4((float)((ipStats.BytesSent - lastTxBytes) * oneOverElapsedTimeInSeconds));
                rxUnicastPktRateGPI.VC = ValueContainer.CreateF4((float)((ipStats.UnicastPacketsReceived - lastRxUnicastPkts) * oneOverElapsedTimeInSeconds));
                txUnicastPktRateGPI.VC = ValueContainer.CreateF4((float)((ipStats.UnicastPacketsSent - lastTxUnicastPkts) * oneOverElapsedTimeInSeconds));
                rxNonUnicastPktRateGPI.VC = ValueContainer.CreateF4((float)((ipStats.NonUnicastPacketsReceived - lastRxNonUnicastPkts) * oneOverElapsedTimeInSeconds));
                txNonUnicastPktRateGPI.VC = ValueContainer.CreateF4((float)((ipStats.NonUnicastPacketsSent - lastTxNonUnicastPkts) * oneOverElapsedTimeInSeconds));

                lastRxBytes = ipStats.BytesReceived;
                lastTxBytes = ipStats.BytesSent;
                lastRxUnicastPkts = ipStats.UnicastPacketsReceived;
                lastTxUnicastPkts = ipStats.UnicastPacketsSent;
                lastRxNonUnicastPkts = ipStats.NonUnicastPacketsReceived;
                lastTxNonUnicastPkts = ipStats.NonUnicastPacketsSent;

                lastQpcTimeStamp = now;

                return deltaType;
            }

            public ValueContainer SettingsOccurrenceDataVC 
            { 
                get 
                {
                    return new ValueContainer(new NamedValueSet()
                    {
                        { "IfaceName", name },
                        { "UnicastAddrs", lastUnicastAddrStrArray },
                        { "PhysAddr", lastPhysAddrStr },
                        { "NetIfaceType", lastNetIfaceType },
                        { "SupportsMCast", lastSupportsMCast },
                    });
                } 
            }
            public ValueContainer StateOccurrenceDataVC
            { 
                get 
                {
                    return new ValueContainer(new NamedValueSet()
                    {
                        { "IfaceName", name },
                        { "LinkUp", lastLinkUp },
                        { "OpStat", lastOpStat },
                        { "Speed", lastSpeed },
                    });
                } 
            }

            private string [] lastUnicastAddrStrArray;
            private string lastPhysAddrStr;
            private NetworkInterfaceType lastNetIfaceType;
            private bool lastSupportsMCast;

            private bool lastLinkUp;
            private OperationalStatus lastOpStat;
            private long lastSpeed;
        }

        protected override void PerformMainLoopService()
        {
            if (sampleIntervalTimer.Started != BaseState.IsOnline)
            {
                if (BaseState.IsOnline)
                {
                    sampleIntervalTimer.Reset(triggerImmediately: true);
                    sampleRecordingIntervalTimer.Reset(triggerImmediately: true);
                }
                else
                {
                    sampleIntervalTimer.Stop();
                    sampleRecordingIntervalTimer.Stop();
                }
            }
            
            if (sampleIntervalTimer.IsTriggered)
            {
                NetworkInterface[] netInterfacesArray = NetworkInterface.GetAllNetworkInterfaces();

                bool occurrenceRecorded = false;

                foreach (var netIface in netInterfacesArray)
                {
                    NetIfaceTracker nit = null;
                    if (netIfaceTrackerDictionary.TryGetValue(netIface.Name, out nit) && nit != null)
                    {
                        var deltaType = nit.Service(netIface);

                        if (deltaType != DeltaType.None)
                        {
                            if (deltaType.IsSet(DeltaType.Settings))
                            {
                                var dataVC = nit.SettingsOccurrenceDataVC;
                                mdrfWriter.RecordOccurrence(netIfaceSettingChangeOccurrence, dataVC: nit.SettingsOccurrenceDataVC);
                                Log.Info.Emit("Adapter Settings changed: {0}", dataVC);

                                occurrenceRecorded = true;
                            }

                            if (deltaType.IsSet(DeltaType.Status))
                            {
                                var dataVC = nit.StateOccurrenceDataVC;
                                mdrfWriter.RecordOccurrence(netIfaceStatusChangeOccurrence, dataVC: nit.StateOccurrenceDataVC);
                                Log.Info.Emit("Adapter State changed: {0}", dataVC);

                                occurrenceRecorded = true;
                            }
                        }
                    }
                }

                if (sampleRecordingIntervalTimer.IsTriggered || occurrenceRecorded)
                    mdrfWriter.RecordGroups();
            }
        }
    }
}

//-------------------------------------------------------------------
