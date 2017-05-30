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

            Log.SetDefaultNamedValueSetForEmitter(Logging.MesgType.All, new NamedValueSet() { { "noMDRF" } });

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

            public MDRF.Writer.GroupPointInfo opStatGPI = new MDRF.Writer.GroupPointInfo() { Name = "opState", Comment = "", ValueCST = ContainerStorageType.None, VC = ValueContainer.Empty };
            public MDRF.Writer.GroupPointInfo rxBytesGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxBytes", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txBytesGPI = new MDRF.Writer.GroupPointInfo() { Name = "txBytes", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxUnicastPktsGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxUnicastPkts", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txUnicastPktsGPI = new MDRF.Writer.GroupPointInfo() { Name = "txUnicastPkts", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxNonUnicastPktsGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxNonUnicastPkts", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txNonUnicastPktsGPI = new MDRF.Writer.GroupPointInfo() { Name = "txNonUnicastPkts", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txQueueLenGPI = new MDRF.Writer.GroupPointInfo() { Name = "txQueueLen", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxPktDiscardsGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxPktDiscards", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxPktErrorsGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxPktErrors", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo rxPktUnkProtosGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxPktUnkProtos", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txPktDiscardsGPI = new MDRF.Writer.GroupPointInfo() { Name = "txPktDiscards", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };
            public MDRF.Writer.GroupPointInfo txPktErrorsGPI = new MDRF.Writer.GroupPointInfo() { Name = "txPktErrors", Comment = "", ValueCST = ContainerStorageType.Int64, VC = new ValueContainer(0L) };

            public MDRF.Writer.GroupPointInfo rxByteRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxByteRate", Comment = "bytes per second", ValueCST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo txByteRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "txByteRate", Comment = "bytes per second", ValueCST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo rxUnicastPktRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxUnicastPktRate", Comment = "packets per second", ValueCST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo txUnicastPktRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "txUnicastPktRate", Comment = "packets per second", ValueCST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo rxNonUnicastPktRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "rxNonUnicastPktRate", Comment = "packets per second", ValueCST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };
            public MDRF.Writer.GroupPointInfo txNonUnicastPktRateGPI = new MDRF.Writer.GroupPointInfo() { Name = "txNonUnicastPktRate", Comment = "packets per second", ValueCST = ContainerStorageType.Single, VC = new ValueContainer(0.0f) };

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
                long speed = netIface.Speed;

                if (lastOpStat != opStat || lastSpeed != speed)
                {
                    deltaType |= DeltaType.Status;
                    lastOpStat = opStat;
                    lastSpeed = speed;
                }

                var ipStats = netIface.GetIPv4Statistics();

                opStatGPI.VC = new ValueContainer(opStat);
                rxBytesGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.BytesReceived, ContainerStorageType.Int64, false);
                txBytesGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.BytesSent, ContainerStorageType.Int64, false);
                rxUnicastPktsGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.UnicastPacketsReceived, ContainerStorageType.Int64, false);
                txUnicastPktsGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.UnicastPacketsSent, ContainerStorageType.Int64, false);
                rxNonUnicastPktsGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.NonUnicastPacketsReceived, ContainerStorageType.Int64, false);
                txNonUnicastPktsGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.NonUnicastPacketsSent, ContainerStorageType.Int64, false);
                txQueueLenGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.OutputQueueLength, ContainerStorageType.Int64, false);
                rxPktDiscardsGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.IncomingPacketsDiscarded, ContainerStorageType.Int64, false);
                rxPktErrorsGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.IncomingPacketsWithErrors, ContainerStorageType.Int64, false);
                rxPktUnkProtosGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.IncomingUnknownProtocolPackets, ContainerStorageType.Int64, false);
                txPktDiscardsGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.OutgoingPacketsDiscarded, ContainerStorageType.Int64, false);
                txPktErrorsGPI.VC = ValueContainer.Empty.SetValue<long>(ipStats.OutgoingPacketsWithErrors, ContainerStorageType.Int64, false);

                QpcTimeStamp now = QpcTimeStamp.Now;
                double elapsedTimeInSeconds = (lastQpcTimeStamp.IsZero ? 0.0 : (now - lastQpcTimeStamp).TotalSeconds);
                double oneOverElapsedTimeInSeconds = (elapsedTimeInSeconds > 0.0) ? (1.0 / elapsedTimeInSeconds) : 0.0;

                rxByteRateGPI.VC = ValueContainer.Empty.SetValue<float>((float)((ipStats.BytesReceived - lastRxBytes) * oneOverElapsedTimeInSeconds), ContainerStorageType.Single, false);
                txByteRateGPI.VC = ValueContainer.Empty.SetValue<float>((float)((ipStats.BytesSent - lastTxBytes) * oneOverElapsedTimeInSeconds), ContainerStorageType.Single, false);
                rxUnicastPktRateGPI.VC = ValueContainer.Empty.SetValue<float>((float)((ipStats.UnicastPacketsReceived - lastRxUnicastPkts) * oneOverElapsedTimeInSeconds), ContainerStorageType.Single, false);
                txUnicastPktRateGPI.VC = ValueContainer.Empty.SetValue<float>((float)((ipStats.UnicastPacketsSent - lastTxUnicastPkts) * oneOverElapsedTimeInSeconds), ContainerStorageType.Single, false);
                rxNonUnicastPktRateGPI.VC = ValueContainer.Empty.SetValue<float>((float)((ipStats.NonUnicastPacketsReceived - lastRxNonUnicastPkts) * oneOverElapsedTimeInSeconds), ContainerStorageType.Single, false);
                txNonUnicastPktRateGPI.VC = ValueContainer.Empty.SetValue<float>((float)((ipStats.NonUnicastPacketsSent - lastTxNonUnicastPkts) * oneOverElapsedTimeInSeconds), ContainerStorageType.Single, false);

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
                        { "OpStat", lastOpStat },
                        { "Speed", lastSpeed },
                    });
                } 
            }

            private string [] lastUnicastAddrStrArray;
            private string lastPhysAddrStr;
            private NetworkInterfaceType lastNetIfaceType;
            private bool lastSupportsMCast;

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
