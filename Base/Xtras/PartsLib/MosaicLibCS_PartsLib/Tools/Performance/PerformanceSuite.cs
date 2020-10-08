//-------------------------------------------------------------------
/*! @file PerformanceSuite.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.Modular.Part;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.PartsLib.Tools.MDRF.Writer;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.PartsLib.Tools.Performance
{
    public class PerformanceSuitePartConfig
    {
        [Flags]
        public enum DefaultFileIndexRowFlagBits : ulong
        {
            /// <summary>0x10</summary>
            CpuSampleGroups = 0x10,
            /// <summary>0x20</summary>
            CpuAggregateGroups = 0x20,
            /// <summary>0x100</summary>
            FileRWSampleGroups = 0x100,
            /// <summary>0x200</summary>
            FileRWAggregateGroups = 0x200,
            /// <summary>0x1000</summary>
            ProcessDeltaOccurrence = 0x1000,
            /// <summary>0x2000</summary>
            ProcessActiveSetGroups = 0x2000,
            /// <summary>0x4000</summary>
            ProcessActiveSetMapGroup = 0x4000,
            /// <summary>0x10000</summary>
            PingAggregateGroups = 0x10000,
            /// <summary>0x40000</summary>
            SerialEchoAggregateGroups = 0x40000,
            /// <summary>0x100000</summary>
            PerformanceCounterGroups = 0x100000,
            /// <summary>0x1000000</summary>
            NetIfaceOccurrence = 0x1000000,
            /// <summary>0x1000000</summary>
            NetIfaceGroups = 0x2000000,
        }

        public PerformanceSuitePartConfig(string partID = "PerfSuite", string dataFilesDirPath = @".\DataFiles", WriterBehavior writerBehavior = (WriterBehavior.AdvanceOnDayBoundary | (WriterBehavior.FlushAfterAnything & ~WriterBehavior.FlushAfterEveryGroupWrite)))
        {
            PartID = partID;

            System.IO.DriveInfo[] driveInfoArray = System.IO.DriveInfo.GetDrives();

            System.Diagnostics.PerformanceCounterCategory pcc = new System.Diagnostics.PerformanceCounterCategory("PhysicalDisk");
            string [] pccInstanceNamesArray = pcc.GetInstanceNames();
            string pDiskInstanceName0 = pccInstanceNamesArray.FirstOrDefault(name => name.StartsWith("0")) ?? pccInstanceNamesArray.SafeAccess(0, string.Empty);
            string pDiskInstanceName1 = pccInstanceNamesArray.FirstOrDefault(name => name.StartsWith("1")) ?? "[WillNotFind]";

            EnableCPUPerfPart = true;
            EnableProcPerfPart = true;
            EnableFileRWPerfPart = true;
            EnablePingPerfPart = true;
            EnableSerialEchoPerfPart = true;
            EnablePerformanceCounterParts = true;
            EnableNetIfacePerfPart = true;

            CPUPerfPartConfig = new CPUPerformancePartConfig(sampleGroupsFileIndexUserRowFlagBits: (ulong) DefaultFileIndexRowFlagBits.CpuSampleGroups, aggregateGroupsFileIndexUserRowFlagBits: (ulong) DefaultFileIndexRowFlagBits.CpuAggregateGroups);
            ProcPerfPartConfig = new ProcessPerformancePartConfig(processDeltaOccurrenceFileIndexUserRowFlagBits: (ulong)DefaultFileIndexRowFlagBits.ProcessDeltaOccurrence, activeSetGroupsFileIndexUserRowFlagBits: (ulong)DefaultFileIndexRowFlagBits.ProcessActiveSetGroups, activeSetMapGroupFileIndexUserRowFlagBits: (ulong) DefaultFileIndexRowFlagBits.ProcessActiveSetMapGroup);
            FileRWPerfPartConfig = new FileRWPerformancePartConfig(sampleGroupsFileIndexUserRowFlagBits: (ulong) DefaultFileIndexRowFlagBits.FileRWSampleGroups, aggregateGroupsFileIndexUserRowFlagBits: (ulong) DefaultFileIndexRowFlagBits.FileRWAggregateGroups);
            PingPerfPartConfig = new PingPerformancePartConfig(aggregateGroupsFileIndexUserRowFlagBits: (ulong) DefaultFileIndexRowFlagBits.PingAggregateGroups);
            SerialEchoPerfPartConfig = new SerialEchoPerformancePartConfig(aggregateGroupsFileIndexUserRowFlagBits: (ulong)DefaultFileIndexRowFlagBits.SerialEchoAggregateGroups);
            PerformanceCounterPartConfigArray = new[]
                {
                    new PerformanceCountersPartConfig("{0}.PerfCtr.Fast".CheckedFormat(partID), (ulong) DefaultFileIndexRowFlagBits.PerformanceCounterGroups)
                    {
                        SampleInterval = (10.0).FromSeconds(),
                        PerformanceCounterSpecArray = new []
                        {
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "% Processor Time", InstanceName = "_Total", PointName = "CPU.UsedTimePct" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "% User Time", InstanceName = "_Total", PointName = "CPU.UserTimePct" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "Interrupts/sec", InstanceName = "_Total", PointName = "CPU.IntRate" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "% Interrupt Time", InstanceName = "_Total", PointName = "CPU.IntTimePct" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "DPCs Queued/sec", InstanceName = "_Total", PointName = "CPU.DpcQueuedRate" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "DPC Rate", InstanceName = "_Total", PointName = "CPU.DpcRate" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "% DPC Time", InstanceName = "_Total", PointName = "CPU.DpcTimePct" },

                            new PerformanceCounterSpec() { CategoryName="System", CounterName = "Processor Queue Length", InstanceName = "", PointName = "Sys.CPU.QueueLen" },
                            new PerformanceCounterSpec() { CategoryName="System", CounterName = "System Calls/sec", InstanceName = "", PointName = "Sys.SysCallRate" },
                            new PerformanceCounterSpec() { CategoryName="System", CounterName = "Context Switches/sec", InstanceName = "", PointName = "Sys.CtxSwitchRate" },

                            /// Windows XP only
                            new PerformanceCounterSpec() { CategoryName="TCP", CounterName = "Segments Received/sec", InstanceName = "", PointName = "TCP.SegRxRate" },
                            new PerformanceCounterSpec() { CategoryName="TCP", CounterName = "Segments Sent/sec", InstanceName = "", PointName = "TCP.SegTxRate" },
                            new PerformanceCounterSpec() { CategoryName="TCP", CounterName = "Segments Retransmitted/sec", InstanceName = "", PointName = "TCP.SegTxAgainRate" },

                            /// Windows XP only
                            new PerformanceCounterSpec() { CategoryName="UDP", CounterName = "Datagrams Received/sec", InstanceName = "", PointName = "UDP.RxRate" },
                            new PerformanceCounterSpec() { CategoryName="UDP", CounterName = "Datagrams Sent/sec", InstanceName = "", PointName = "UDP.TxRate" },
                            new PerformanceCounterSpec() { CategoryName="UDP", CounterName = "Datagrams No Port/sec", InstanceName = "", PointName = "UDP.NoPortRate" },
                            new PerformanceCounterSpec() { CategoryName="UDP", CounterName = "Datagrams Received Errors", InstanceName = "", PointName = "UDP.RxErrors" },

                            /// not available in Windows XP
                            new PerformanceCounterSpec() { CategoryName="TCPv4", CounterName = "Segments Received/sec", InstanceName = "", PointName = "TCPv4.SegRxRate" },
                            new PerformanceCounterSpec() { CategoryName="TCPv4", CounterName = "Segments Sent/sec", InstanceName = "", PointName = "TCPv4.SegTxRate" },
                            new PerformanceCounterSpec() { CategoryName="TCPv4", CounterName = "Segments Retransmitted/sec", InstanceName = "", PointName = "TCPv4.SegTxAgainRate" },

                            /// not available in Windows XP
                            new PerformanceCounterSpec() { CategoryName="TCPv6", CounterName = "Segments Received/sec", InstanceName = "", PointName = "TCPv6.SegRxRate" },
                            new PerformanceCounterSpec() { CategoryName="TCPv6", CounterName = "Segments Sent/sec", InstanceName = "", PointName = "TCPv6.SegTxRate" },
                            new PerformanceCounterSpec() { CategoryName="TCPv6", CounterName = "Segments Retransmitted/sec", InstanceName = "", PointName = "TCPv6.SegTxAgainRate" },

                            /// not available in Windows XP
                            new PerformanceCounterSpec() { CategoryName="UDPv4", CounterName = "Datagrams Received/sec", InstanceName = "", PointName = "UDPv4.RxRate" },
                            new PerformanceCounterSpec() { CategoryName="UDPv4", CounterName = "Datagrams Sent/sec", InstanceName = "", PointName = "UDPv4.TxRate" },
                            new PerformanceCounterSpec() { CategoryName="UDPv4", CounterName = "Datagrams No Port/sec", InstanceName = "", PointName = "UDPv4.NoPortRate" },
                            new PerformanceCounterSpec() { CategoryName="UDPv4", CounterName = "Datagrams Received Errors", InstanceName = "", PointName = "UDPv4.RxErrors" },

                            /// not available in Windows XP
                            new PerformanceCounterSpec() { CategoryName="UDPv6", CounterName = "Datagrams Received/sec", InstanceName = "", PointName = "UDPv6.RxRate" },
                            new PerformanceCounterSpec() { CategoryName="UDPv6", CounterName = "Datagrams Sent/sec", InstanceName = "", PointName = "UDPv6.TxRate" },
                            new PerformanceCounterSpec() { CategoryName="UDPv6", CounterName = "Datagrams No Port/sec", InstanceName = "", PointName = "UDPv6.NoPortRate" },
                            new PerformanceCounterSpec() { CategoryName="UDPv6", CounterName = "Datagrams Received Errors", InstanceName = "", PointName = "UDPv6.RxErrors" },
                        }.Where(pcs => pcs.IsValid()).ToArray(),
                    },
                    new PerformanceCountersPartConfig("{0}.PerfCtr.Slow".CheckedFormat(partID), (ulong) DefaultFileIndexRowFlagBits.PerformanceCounterGroups)
                    {
                        SampleInterval = (60.0).FromSeconds(),
                        PerformanceCounterSpecArray = new []
                        {
                            new PerformanceCounterSpec() { CategoryName="Memory", CounterName = "Available MBytes", InstanceName = "", PointName = "Mem.AvailMBytes" },
                            new PerformanceCounterSpec() { CategoryName="Memory", CounterName = "Cache Bytes", InstanceName = "", PointName = "Mem.CacheBytes" },
                            new PerformanceCounterSpec() { CategoryName="Memory", CounterName = "Page Faults/sec", InstanceName = "", PointName = "Mem.PageFaultRate" },
                            new PerformanceCounterSpec() { CategoryName="Memory", CounterName = "Pool Nonpaged Bytes", InstanceName = "", PointName = "Mem.NPPoolBytes" },
                            new PerformanceCounterSpec() { CategoryName="Memory", CounterName = "Pool Paged Bytes", InstanceName = "", PointName = "Mem.PPoolBytes" },
                            new PerformanceCounterSpec() { CategoryName="Memory", CounterName = "Pool Paged Resident Bytes", InstanceName = "", PointName = "Mem.PPoolResBytes" },

                            new PerformanceCounterSpec() { CategoryName="System", CounterName = "Processes", InstanceName = "", PointName = "Sys.Processes" },
                            new PerformanceCounterSpec() { CategoryName="System", CounterName = "Threads", InstanceName = "", PointName = "Sys.Threads" },
                            new PerformanceCounterSpec() { CategoryName="System", CounterName = "System Up Time", InstanceName = "", PointName = "Sys.UpTime" },

                            new PerformanceCounterSpec() { CategoryName="Process", CounterName = "Handle Count", InstanceName = "_Total", PointName = "Proc.All.Handles" },
                            new PerformanceCounterSpec() { CategoryName="Process", CounterName = "Thread Count", InstanceName = "_Total", PointName = "Proc.All.Threads" },
                            new PerformanceCounterSpec() { CategoryName="Process", CounterName = "Working Set", InstanceName = "_Total", PointName = "Proc.All.WorkingSet" },
                            new PerformanceCounterSpec() { CategoryName="Process", CounterName = "Virtual Bytes", InstanceName = "_Total", PointName = "Proc.All.VirtualBytes" },

                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "% C1 Time", InstanceName = "_Total", PointName = "CPU.C1TimePct" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "% C2 Time", InstanceName = "_Total", PointName = "CPU.C2TimePct" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "% C3 Time", InstanceName = "_Total", PointName = "CPU.C3TimePct" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "C1 Transitions/sec", InstanceName = "_Total", PointName = "CPU.C1DeltaRate" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "C2 Transitions/sec", InstanceName = "_Total", PointName = "CPU.C2DeltaRate" },
                            new PerformanceCounterSpec() { CategoryName="Processor", CounterName = "C3 Transitions/sec", InstanceName = "_Total", PointName = "CPU.C3DeltaRate" },

                            /// Windows XP only
                            new PerformanceCounterSpec() { CategoryName="TCP", CounterName = "Connections Established", InstanceName = "", PointName = "TCP.Total" },
                            new PerformanceCounterSpec() { CategoryName="TCP", CounterName = "Connections Active", InstanceName = "", PointName = "TCP.Active" },
                            new PerformanceCounterSpec() { CategoryName="TCP", CounterName = "Connections Reset", InstanceName = "", PointName = "TCP.Reset" },
                            new PerformanceCounterSpec() { CategoryName="TCP", CounterName = "Connection Failures", InstanceName = "", PointName = "TCP.ConnFails" },

                            /// not available in Windows XP
                            new PerformanceCounterSpec() { CategoryName="TCPv4", CounterName = "Connections Established", InstanceName = "", PointName = "TCPv4.Total" },
                            new PerformanceCounterSpec() { CategoryName="TCPv4", CounterName = "Connections Active", InstanceName = "", PointName = "TCPv4.Active" },
                            new PerformanceCounterSpec() { CategoryName="TCPv4", CounterName = "Connections Reset", InstanceName = "", PointName = "TCPv4.Reset" },
                            new PerformanceCounterSpec() { CategoryName="TCPv4", CounterName = "Connection Failures", InstanceName = "", PointName = "TCPv4.ConnFails" },

                            /// not available in Windows XP
                            new PerformanceCounterSpec() { CategoryName="TCPv6", CounterName = "Connections Established", InstanceName = "", PointName = "TCPv6.Total" },
                            new PerformanceCounterSpec() { CategoryName="TCPv6", CounterName = "Connections Active", InstanceName = "", PointName = "TCPv6.Active" },
                            new PerformanceCounterSpec() { CategoryName="TCPv6", CounterName = "Connections Reset", InstanceName = "", PointName = "TCPv6.Reset" },
                            new PerformanceCounterSpec() { CategoryName="TCPv6", CounterName = "Connection Failures", InstanceName = "", PointName = "TCPv6.ConnFails" },

                            new PerformanceCounterSpec() { CategoryName="LogicalDisk", CounterName = "Free Megabytes", InstanceName = "_Total", PointName = "LDisk.All.FreeMB" },
                            new PerformanceCounterSpec() { CategoryName="LogicalDisk", CounterName = "Free Megabytes", InstanceName = "C:", PointName = "LDisk.C.FreeMB" },
                            new PerformanceCounterSpec() { CategoryName="LogicalDisk", CounterName = "Free Megabytes", InstanceName = "D:", PointName = "LDisk.D.FreeMB" },
                            new PerformanceCounterSpec() { CategoryName="LogicalDisk", CounterName = "Free Megabytes", InstanceName = "E:", PointName = "LDisk.E.FreeMB" },
                            new PerformanceCounterSpec() { CategoryName="LogicalDisk", CounterName = "Free Megabytes", InstanceName = "F:", PointName = "LDisk.F.FreeMB" },

                            new PerformanceCounterSpec() { CategoryName=".NET CLR Exceptions", CounterName = "# of Exceps Thrown / sec", InstanceName = "_Global_", PointName = "CLR.All.Excep.Rate" },

                            new PerformanceCounterSpec() { CategoryName=".NET CLR Jit", CounterName = "% Time in Jit", InstanceName = "_Global_", PointName = "CLR.All.Jit.UsedTimePct" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Jit", CounterName = "# of Methods Jitted", InstanceName = "_Global_", PointName = "CLR.All.Jit.NumMethods" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Jit", CounterName = "# of IL Bytes Jitted", InstanceName = "_Global_", PointName = "CLR.All.Jit.NumILBytes" },

                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "# Bytes in all Heaps", InstanceName = "_Global_", PointName = "CLR.All.Mem.NumBytes" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "Allocated Bytes/sec", InstanceName = "_Global_", PointName = "CLR.All.Mem.AllocationRate" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "# GC Handles", InstanceName = "_Global_", PointName = "CLR.All.Mem.GCHandles" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "# of Pinned Objects", InstanceName = "_Global_", PointName = "CLR.All.Mem.NumPinnedObjects" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "Gen 0 heap size", InstanceName = "_Global_", PointName = "CLR.All.Mem.Gen0.HeapSize" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "Gen 1 heap size", InstanceName = "_Global_", PointName = "CLR.All.Mem.Gen1.HeapSize" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "Gen 2 heap size", InstanceName = "_Global_", PointName = "CLR.All.Mem.Gen2.HeapSize" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "Large Object Heap size", InstanceName = "_Global_", PointName = "CLR.All.Mem.LargeObject.HeapSize" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "# Gen 0 Collections", InstanceName = "_Global_", PointName = "CLR.All.GC.Gen0.NumCollections" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "# Gen 1 Collections", InstanceName = "_Global_", PointName = "CLR.All.GC.Gen1.NumCollections" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "# Gen 2 Collections", InstanceName = "_Global_", PointName = "CLR.All.GC.Gen2.NumCollections" },
                            new PerformanceCounterSpec() { CategoryName=".NET CLR Memory", CounterName = "% Time in GC", InstanceName = "_Global_", PointName = "CLR.All.GC.UsedTimePct" },

                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "% Idle Time", InstanceName = "_Total", PointName = "PDisk.All.IdleTimePct" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Current Disk Queue Length", InstanceName = "_Total", PointName = "PDisk.All.QueueLen" },

                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "% Idle Time", InstanceName = pDiskInstanceName0, PointName = "PDisk.0.IdleTimePct" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Current Disk Queue Length", InstanceName = pDiskInstanceName1, PointName = "PDisk.0.QueueLen" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Avg. Disk sec/Read", InstanceName = pDiskInstanceName0, PointName = "PDisk.0.AvgSecPerRead" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Avg. Disk sec/Write", InstanceName = pDiskInstanceName0, PointName = "PDisk.0.AvgSecPerWrite" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Disk Reads/sec", InstanceName = pDiskInstanceName0, PointName = "PDisk.0.ReadIOPs" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Disk Writes/sec", InstanceName = pDiskInstanceName0, PointName = "PDisk.0.WriteIOPs" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Split IO/Sec", InstanceName = pDiskInstanceName0, PointName = "PDisk.0.SplitIOPs" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Disk Read Bytes/sec", InstanceName = pDiskInstanceName0, PointName = "PDisk.0.ReadBPS" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Disk Write Bytes/sec", InstanceName = pDiskInstanceName0, PointName = "PDisk.0.WriteBPS" },

                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "% Idle Time", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.IdleTimePct" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Current Disk Queue Length", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.QueueLen" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Avg. Disk sec/Read", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.AvgSecPerRead" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Avg. Disk sec/Write", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.AvgSecPerWrite" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Disk Reads/sec", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.ReadIOPs" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Disk Writes/sec", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.WriteIOPs" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Split IO/Sec", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.SplitIOPs" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Disk Read Bytes/sec", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.ReadBPS" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Disk Write Bytes/sec", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.WriteBPS" },
                            new PerformanceCounterSpec() { CategoryName="PhysicalDisk", CounterName = "Current Disk Queue Length", InstanceName = pDiskInstanceName1, PointName = "PDisk.1.QueueLen" },
                        }.Where(pcs => pcs.IsValid()).ToArray(),
                    },
                };
            NetIfacePerfPartConfig = new NetIfacePerformancePartConfig(statisticsGroupsFileIndexUserRowFlagBits: (ulong)DefaultFileIndexRowFlagBits.NetIfaceGroups, changeOccurrenceFileIndexUserRowFlagBits: (ulong)DefaultFileIndexRowFlagBits.NetIfaceOccurrence);

            System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

            MDRFWriterSetupInfo = new SetupInfo() 
            { 
                ClientName = Fcns.CurrentProcessMainModuleShortName, 
                DirPath = dataFilesDirPath, 
                FileNamePrefix = PartID,
                CreateDirectoryIfNeeded = true,
                NominalMaxFileSize = 250 * 1024 * 1024,     // Nominal size should cover most uses for 1 day.  Current daily file sizes appear to range from 100 to 200 MBytes.  current average is about 150 MBytes
                FileIndexNumRows = 8192,
                ClientNVS = new NamedValueSet() 
                { 
                    { "WriterBehavior", writerBehavior },
                },
            };

            PruneRules = new File.DirectoryTreePruningManager.PruneRules()
            {
                FileAgeLimit = (30.0).FromDays(),
                TreeTotalSizeLimit = (5L * 1000 * 1024 * 1024),  // 5 GByte (this is equivilant to an average of 175 MBytes per day for 30 days)
            };
        }

        public PerformanceSuitePartConfig(PerformanceSuitePartConfig other)
        {
            PartID = other.PartID;
            IConfig = other.IConfig ?? Config.Instance;
            IVI = other.IVI ?? Values.Instance;
            DisableMMTimerPeriodUsage = other.DisableMMTimerPeriodUsage;
            EnableCPUPerfPart = other.EnableCPUPerfPart;
            EnableProcPerfPart = other.EnableProcPerfPart;
            EnableFileRWPerfPart = other.EnableFileRWPerfPart;
            EnablePingPerfPart = other.EnablePingPerfPart;
            EnableSerialEchoPerfPart = other.EnableSerialEchoPerfPart;
            EnablePerformanceCounterParts = other.EnablePerformanceCounterParts;
            EnableNetIfacePerfPart = other.EnableNetIfacePerfPart;
            CPUPerfPartConfig = other.CPUPerfPartConfig;
            ProcPerfPartConfig = other.ProcPerfPartConfig;
            FileRWPerfPartConfig = other.FileRWPerfPartConfig;
            PingPerfPartConfig = other.PingPerfPartConfig;
            SerialEchoPerfPartConfig = other.SerialEchoPerfPartConfig;
            _performanceCounterPartConfigArray = other._performanceCounterPartConfigArray;
            NetIfacePerfPartConfig = other.NetIfacePerfPartConfig;
            MDRFWriterSetupInfo = new SetupInfo(other.MDRFWriterSetupInfo);
            PruneRules = other.PruneRules;
        }

        public string PartID { get; private set; }

        public IConfig IConfig { get; set; }

        public IValuesInterconnection IVI { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool DisableMMTimerPeriodUsage { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool EnableCPUPerfPart { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool EnableProcPerfPart { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool EnableFileRWPerfPart { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool EnablePingPerfPart { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool EnableSerialEchoPerfPart { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool EnablePerformanceCounterParts { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool EnableNetIfacePerfPart { get; set; }

        public CPUPerformancePartConfig CPUPerfPartConfig { get; set; }
        public ProcessPerformancePartConfig ProcPerfPartConfig { get; set; }
        public FileRWPerformancePartConfig FileRWPerfPartConfig { get; set; }
        public PingPerformancePartConfig PingPerfPartConfig { get; set; }
        public SerialEchoPerformancePartConfig SerialEchoPerfPartConfig { get; set; }
        public PerformanceCountersPartConfig[] PerformanceCounterPartConfigArray { get { return _performanceCounterPartConfigArray ?? emptyPerformanceCountersPartConfigArray; } set { _performanceCounterPartConfigArray = value; } }
        private PerformanceCountersPartConfig[] _performanceCounterPartConfigArray;
        public NetIfacePerformancePartConfig NetIfacePerfPartConfig { get; set; }
        public MDRF.Common.SetupInfo MDRFWriterSetupInfo { get; set; }

        public File.DirectoryTreePruningManager.PruneRules PruneRules { get { return _pruneRules; } set { _pruneRules = value; } }
        private File.DirectoryTreePruningManager.PruneRules _pruneRules;

        // the following properties give client and config adapter direct access to nested parameters in sub-objects (MDRFWriterSetupInfo and PruneRules)

        [ConfigItem(IsOptional = true, Name="{1}MDRFFiles.{0}", NameAdjust = NameAdjust.FormatWithMemberName)]
        public string DirPath { get { return ((MDRFWriterSetupInfo != null) ? MDRFWriterSetupInfo.DirPath : ""); } set { if (MDRFWriterSetupInfo != null) MDRFWriterSetupInfo.DirPath = value; } }

        [ConfigItem(IsOptional = true, Name = "{1}MDRFFiles.{0}", NameAdjust = NameAdjust.FormatWithMemberName)]
        public string FileNamePrefix { get { return ((MDRFWriterSetupInfo != null) ? MDRFWriterSetupInfo.FileNamePrefix : ""); } set { if (MDRFWriterSetupInfo != null) MDRFWriterSetupInfo.FileNamePrefix = value; } }

        [ConfigItem(IsOptional = true, Name = "{1}MDRFFiles.{0}", NameAdjust = NameAdjust.FormatWithMemberName)]
        public string ClientName { get { return ((MDRFWriterSetupInfo != null) ? MDRFWriterSetupInfo.ClientName : ""); } set { if (MDRFWriterSetupInfo != null) MDRFWriterSetupInfo.ClientName = value; } }

        [ConfigItem(IsOptional = true, Name = "{1}MDRFFiles.{0}", NameAdjust = NameAdjust.FormatWithMemberName)]
        public bool CreateDirectoryIfNeeded { get { return ((MDRFWriterSetupInfo != null) ? MDRFWriterSetupInfo.CreateDirectoryIfNeeded : false); } set { if (MDRFWriterSetupInfo != null) MDRFWriterSetupInfo.CreateDirectoryIfNeeded = value; } }

        [ConfigItem(IsOptional = true, Name = "{1}MDRFFiles.{0}", NameAdjust = NameAdjust.FormatWithMemberName)]
        public int NominalMaxFileSize { get { return ((MDRFWriterSetupInfo != null) ? MDRFWriterSetupInfo.NominalMaxFileSize : 0); } set { if (MDRFWriterSetupInfo != null) MDRFWriterSetupInfo.NominalMaxFileSize = value; } }

        [ConfigItem(IsOptional = true, Name = "{1}MDRFFiles.{0}", NameAdjust = NameAdjust.FormatWithMemberName)]
        public TimeSpan FileAgeLimit { get { return _pruneRules.FileAgeLimit; } set { _pruneRules.FileAgeLimit = value; } }

        [ConfigItem(IsOptional = true, Name = "{1}MDRFFiles.{0}", NameAdjust = NameAdjust.FormatWithMemberName)]
        public long TreeTotalSizeLimit { get { return _pruneRules.TreeTotalSizeLimit; } set { _pruneRules.TreeTotalSizeLimit = value; } }

        public string CPUPerfPartID { get { return ((CPUPerfPartConfig != null) ? "{0}.CPU".CheckedFormat(PartID) : ""); } }
        public string ProcPerfPartID { get { return ((ProcPerfPartConfig != null) ? "{0}.Proc".CheckedFormat(PartID) : ""); } }
        public string FileRWPerfPartID { get { return ((FileRWPerfPartConfig != null) ? "{0}.FileRW".CheckedFormat(PartID) : ""); } }
        public string PingPerfPartID { get { return ((PingPerfPartConfig != null) ? "{0}.Ping".CheckedFormat(PartID) : ""); } }
        public string SerialEchoPerfPartID { get { return ((SerialEchoPerfPartConfig != null) ? "{0}.SerialEcho".CheckedFormat(PartID) : ""); } }
        public string[] PerformanceCounterPartIDArray { get { return PerformanceCounterPartConfigArray.Select(pcpc => pcpc.PartID).ToArray(); } }
        public string NetIfacePerfPartID { get { return ((NetIfacePerfPartConfig != null) ? "{0}.NetIface".CheckedFormat(PartID) : ""); } }

        public PerformanceSuitePartConfig Setup(Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            string configKeyPrefix = "{0}.".CheckedFormat(PartID);
            var adapter = new ConfigValueSetAdapter<PerformanceSuitePartConfig>(IConfig) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(configKeyPrefix);

            if (CPUPerfPartConfig != null && EnableCPUPerfPart)
                CPUPerfPartConfig.Setup("{0}.".CheckedFormat(CPUPerfPartID), config: IConfig, issueEmitter: issueEmitter, valueEmitter: valueEmitter);

            if (ProcPerfPartConfig != null && EnableProcPerfPart)
                ProcPerfPartConfig.Setup("{0}.".CheckedFormat(ProcPerfPartID), config: IConfig, issueEmitter: issueEmitter, valueEmitter: valueEmitter);

            if (FileRWPerfPartConfig != null && EnableFileRWPerfPart)
                FileRWPerfPartConfig.Setup("{0}.".CheckedFormat(FileRWPerfPartID), config: IConfig, issueEmitter: issueEmitter, valueEmitter: valueEmitter);

            if (PingPerfPartConfig != null && EnablePingPerfPart)
                PingPerfPartConfig.Setup("{0}.".CheckedFormat(PingPerfPartID), config: IConfig, issueEmitter: issueEmitter, valueEmitter: valueEmitter);

            if (SerialEchoPerfPartConfig != null && EnableSerialEchoPerfPart)
                SerialEchoPerfPartConfig.Setup("{0}.".CheckedFormat(SerialEchoPerfPartID), config: IConfig, issueEmitter: issueEmitter, valueEmitter: valueEmitter);

            if (EnablePerformanceCounterParts)
                PerformanceCounterPartConfigArray.Select(pcpc => pcpc.Setup("{0}.".CheckedFormat(pcpc.PartID), config: IConfig, issueEmitter: issueEmitter, valueEmitter: valueEmitter)).DoForEach();

            if (NetIfacePerfPartConfig != null && EnableNetIfacePerfPart)
                NetIfacePerfPartConfig.Setup("{0}.".CheckedFormat(NetIfacePerfPartID), config: IConfig, issueEmitter: issueEmitter, valueEmitter: valueEmitter);

            return this;
        }

        private static readonly PerformanceCountersPartConfig[] emptyPerformanceCountersPartConfigArray = EmptyArrayFactory<PerformanceCountersPartConfig>.Instance;
    }

    public class PerformanceSuitePart : SimpleActivePartBase
	{
        public PerformanceSuitePart(PerformanceSuitePartConfig config, IMDRFWriter mdrfWriterIn = null)
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion1)
        {
            Config = new PerformanceSuitePartConfig(config);

            if (!Config.DisableMMTimerPeriodUsage)
                mmTimerPeriod = new MMTimerPeriod();

            mdrfWriter = mdrfWriterIn ?? new MDRFWriter("{0}.mdrfwriter".CheckedFormat(PartID), Config.MDRFWriterSetupInfo, enableAPILocking: true);

            if (!Config.PruneRules.IsEmpty)
            {
                File.DirectoryTreePruningManager.Config pruningMgrConfig = new File.DirectoryTreePruningManager.Config()
                {
                    CreateDirectoryIfNeeded = true,
                    DirPath = Config.MDRFWriterSetupInfo.DirPath,
                    PruneMode = File.DirectoryTreePruningManager.PruneMode.PruneFiles,
                    PruneRules = Config.PruneRules,
                };

                pruningMgr = new File.DirectoryTreePruningManager("{0}.pruneMgr".CheckedFormat(PartID), pruningMgrConfig) 
                { 
                    EmitterDictionary = new Dictionary<string, Logging.IMesgEmitter>() { { "Issue", Log.Error }, { "Info", Log.Info }, { "Debug", Log.Debug } } 
                };
            }

            if (Config.CPUPerfPartConfig != null && Config.EnableCPUPerfPart)
                partsList.Add(cpuPerf = new CPUPerformancePart(Config.CPUPerfPartID, Config.CPUPerfPartConfig, mdrfWriter));

            if (Config.ProcPerfPartConfig != null && Config.EnableProcPerfPart)
                partsList.Add(procPerf = new ProcessPerformancePart(Config.ProcPerfPartID, Config.ProcPerfPartConfig, mdrfWriter));

            if (Config.FileRWPerfPartConfig != null && Config.EnableFileRWPerfPart)
                partsList.Add(fileRWPerf = new FileRWPerformancePart(Config.FileRWPerfPartID, Config.FileRWPerfPartConfig, mdrfWriter));

            if (Config.PingPerfPartConfig != null && Config.EnablePingPerfPart)
                partsList.Add(pingPerf = new PingPerformancePart(Config.PingPerfPartID, Config.PingPerfPartConfig, mdrfWriter));

            if (Config.SerialEchoPerfPartConfig != null && Config.EnableSerialEchoPerfPart)
                partsList.Add(serialEchoPerf = new SerialEchoPerformancePart(Config.SerialEchoPerfPartID, Config.SerialEchoPerfPartConfig, mdrfWriter));

            if (!Config.PerformanceCounterPartConfigArray.IsNullOrEmpty() && Config.EnablePerformanceCounterParts)
                partsList.AddRange(Config.PerformanceCounterPartConfigArray.Select(pcpc => new PerformanceCountersPart(pcpc, mdrfWriter)));

            if (Config.NetIfacePerfPartConfig != null && Config.EnableNetIfacePerfPart)
                partsList.Add(netIfacePerf = new NetIfacePerformancePart(Config.NetIfacePerfPartID, Config.NetIfacePerfPartConfig, mdrfWriter));

            AddExplicitDisposeAction(() => Release());
        }

        private void Release()
        {
            Fcns.DisposeOfObject(ref mmTimerPeriod);
            partsList.TakeAndDisposeOfGivenObjects();
            Fcns.DisposeOfObject(ref mdrfWriter);
            Fcns.DisposeOfObject(ref pruningMgr);
        }

        public PerformanceSuitePartConfig Config { get; private set; }

        private MMTimerPeriod mmTimerPeriod;

        private IMDRFWriter mdrfWriter;
        public IMDRFWriter MDRFWriter { get { return mdrfWriter; } }
        private File.DirectoryTreePruningManager pruningMgr;
        private List<IActivePartBase> partsList = new List<IActivePartBase>();

        private IActivePartBase cpuPerf, fileRWPerf, procPerf, pingPerf, serialEchoPerf, netIfacePerf;

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            IClientFacet[] goOnlineActionArray = partsList.Select(part => part.CreateGoOnlineAction(true).StartInline()).ToArray();
            goOnlineActionArray.DoForEach(a => a.WaitUntilComplete());

            return goOnlineActionArray.Select(a => a.ActionState.ResultCode).Where(rc => !rc.IsNullOrEmpty()).FirstOrDefault().MapNullToEmpty();
        }

        protected override string PerformGoOfflineAction()
        {
            IClientFacet[] goOfflineActionArray = partsList.Select(part => part.CreateGoOfflineAction().StartInline()).ToArray();
            goOfflineActionArray.DoForEach((a) => a.WaitUntilComplete());

            string ec = goOfflineActionArray.Select(a => a.ActionState.ResultCode).Where(rc => !rc.IsNullOrEmpty()).FirstOrDefault().MapNullToEmpty();

            if (ec.IsNullOrEmpty())
                mdrfWriter.CloseCurrentFile("By request [{0} {1}]".CheckedFormat(PartID, CurrentActionDescription));

            return ec;
        }

        protected override void PerformMainLoopService()
        {
            if (BaseState.IsOnline)
            {
                PartsLib.Tools.MDRF.Writer.FileInfo closedFileInfo = mdrfWriter.NextClosedFileInfo ?? emptyFileInfo;

                if (pruningMgr != null)
                {
                    if (!closedFileInfo.FilePath.IsNullOrEmpty())
                        pruningMgr.NotePathAdded(closedFileInfo.FilePath);

                    pruningMgr.Service();
                }
            }
            else if (mdrfWriter != null && mdrfWriter.CurrentFileInfo.IsActive)
            {
                mdrfWriter.CloseCurrentFile("By request [{0} part is {1}]".CheckedFormat(PartID, BaseState));
            }
        }

        private static readonly PartsLib.Tools.MDRF.Writer.FileInfo emptyFileInfo = new MDRF.Writer.FileInfo();
    }
}
