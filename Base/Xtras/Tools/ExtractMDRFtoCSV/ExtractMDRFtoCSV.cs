//-------------------------------------------------------------------
/*! @file ExtractMDRFtoCSV.cs
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
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.PartsLib.Tools.MDRF.Reader;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.Tools.ExtractMDRFtoCSV
{
    public static class ExtractMDRFtoCSV
    {
        private static string appName = "AppNameNotFound";
        private static System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();
        private static string[] currentExecAssyFullNameSplit = currentExecAssy.FullName.Split(' ').Select(item => item.Trim(',')).ToArray();

        static void Main(string[] args)
        {
            try
            {
                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                appName = System.IO.Path.GetFileName(currentProcess.MainModule.ModuleName);

                bool stopProcessingArgs = false;

                if (args.SafeLength() <= 0)
                {
                    WriteUsage();
                    stopProcessingArgs = true;
                }

                foreach (var arg in args)
                {
                    if (stopProcessingArgs)
                        break;

                    if (arg.StartsWith("-"))
                    {
                        StringScanner ss = new StringScanner(arg);
                        ss.Idx++;
                        Select argSelect = Select.None;

                        if (ss.MatchToken("group:", requireTokenEnd: false) || ss.MatchToken("g:", requireTokenEnd: false))
                            selectedGroupList.Add(ss.Rest);
                        else if (ss.MatchToken("tag:", requireTokenEnd: false) || ss.MatchToken("t:", requireTokenEnd: false))
                            fileNameTag = ss.Rest;
                        else if ((ss.MatchToken("start:", requireTokenEnd: false) || ss.MatchToken("s:", requireTokenEnd: false)) && ss.ParseValue(out startDeltaTime))
                        { }
                        else if ((ss.MatchToken("end:", requireTokenEnd: false) || ss.MatchToken("e:", requireTokenEnd: false)) && ss.ParseValue(out endDeltaTime))
                        { }
                        else if (ss.MatchToken("tail:", requireTokenEnd: false) && ss.ParseValue(out tailTime))
                        { }
                        else if ((ss.MatchToken("interval:", requireTokenEnd: false) || ss.MatchToken("i:", requireTokenEnd: false)) && ss.ParseValue(out nominalUpdateInterval))
                        { }
                        else if (ss.ParseValue(out argSelect) && ss.IsAtEnd)
                        {
                            select |= argSelect;
                        }
                        else switch (arg)
                            {
                                case "-?":
                                    WriteUsage();
                                    stopProcessingArgs = true;
                                    break;

                                case "-r0": nominalUpdateInterval = 0.0; break;
                                case "-r1": nominalUpdateInterval = 1.0; break;
                                case "-r2": nominalUpdateInterval = 0.5; break;
                                case "-r5": nominalUpdateInterval = 0.2; break;
                                case "-r10": nominalUpdateInterval = 0.1; break;

                                default:
                                    Console.WriteLine("{0}: option '{1}' is not recognized.".CheckedFormat(appName, arg));
                                    WriteUsage();
                                    stopProcessingArgs = true;
                                    break;
                            }
                    }
                    else
                    {
                        ProcessFileNameArg(arg);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("{0}: failed with exception: {1}".CheckedFormat(appName, ex.ToString(ExceptionFormat.AllButStackTrace)));
            }
        }

        /// <summary>
        /// Bitmask used by command line parser to help record what what the client wants the tool to do.
        /// <para/>None, IncludeOccurrences, IncludeExtra, Sparse, List, ListIndex, ListGroups, ListOccurrences
        /// </summary>
        [Flags]
        public enum Select
        {
            None = 0x0000,
            IncludeOccurrences = 0x0001,
            IncludeExtras = 0x0002,
            Sparse = 0x0004,
            NoData = 0x0008,
            HeaderAndDataOnly = 0x0010,
            ListInfo = 0x0100,
            ListIndex = 0x0200,
            ListGroupInfo = 0x0400,
            ListOccurrenceInfo = 0x0800,
            ListOccurrences = 0x1000,
            ListMessages = 0x2000,
            Debug = 0x10000,
            MapBool = 0x20000,

            io = IncludeOccurrences,
            ie = IncludeExtras,
            ix = IncludeExtras,
            s = Sparse,
            nd = NoData,
            hado = HeaderAndDataOnly,
            l = ListInfo,
            List = ListInfo,
            li = ListIndex,
            lgi = ListGroupInfo,
            loi = ListOccurrenceInfo,
            lo = ListOccurrences,
            lm = ListMessages,
            d = Debug,
            mb = MapBool,
        }

        private static Select select = Select.None;
        private static double startDeltaTime = 0.0;
        private static double endDeltaTime = double.PositiveInfinity;
        private static double tailTime = 0.0;
        private static string fileNameTag = null;
        private static List<string> selectedGroupList = new List<string>();
        private static double nominalUpdateInterval = 0.0;

        private static void WriteUsage()
        {
            Console.WriteLine("Usage: {0} [-IncludeOccurrences | -IncludeExtras".CheckedFormat(appName));
            Console.WriteLine("       | -Sparse | -NoData | -HeaderAndDataOnly | -MapBool | -group:name | -tag:tag");
            Console.WriteLine("       | -interval:interval | -start:deltaTime | -end:deltaTime | -tail:period] [fileName.mdrf] ...");
            Console.WriteLine("   or: {0} [-List | -ListIndex | -ListGroupInfo | -ListOccurrenceInfo] [fileName.mdrf] ...".CheckedFormat(appName));
            Console.WriteLine("   or: {0} [-ListOccurrences | -ListMessages] [fileName.mdrf] ...".CheckedFormat(appName));
            Console.WriteLine(" Also accepts: -io, -ie, -s, -nd, -hado, -mb, -g:name, -t:tag, -s:dt, -e:dt, -i:interval");
            Console.WriteLine("               -l, -li, -lgi, -loi");
            Console.WriteLine("               -lo, -lm");
            Console.WriteLine();

            Console.WriteLine("Assembly: {0} [{1}]", currentExecAssyFullNameSplit.SafeAccess(0), currentExecAssyFullNameSplit.SafeAccess(1));
        }

        private static void ProcessFileNameArg(string arg)
        {
            if (!System.IO.Path.HasExtension(arg))
                arg = arg + ".mdrf";

            string filePart = System.IO.Path.GetFileName(arg);
            string pathPart = System.IO.Path.GetDirectoryName(arg).MapNullOrEmptyTo(".");
            string fullPathPart = System.IO.Path.GetFullPath(pathPart);

            if (filePart.Contains("?") || filePart.Contains("*"))
            {
                foreach (string fpath in System.IO.Directory.EnumerateFiles(pathPart, filePart, System.IO.SearchOption.TopDirectoryOnly))
                {
                    ProcessMDRFFile(fpath);
                }
            }
            else
            {
                ProcessMDRFFile(arg);
            }
        }

        private static void ProcessMDRFFile(string mdrfFilePath)
        {
            using (currentReader = new MDRFFileReader(mdrfFilePath, mdrfFileReaderBehavior: MDRFFileReaderBehavior.EnableLiveFileHeuristics))
            {
                if (!currentReader.ResultCode.IsNullOrEmpty())
                {
                    Console.WriteLine("Failed to open '{0}': {1}".CheckedFormat(System.IO.Path.GetFileName(mdrfFilePath), currentReader.ResultCode));
                    return;
                }

                if (select.IsAnySet(Select.ListInfo | Select.ListIndex | Select.ListGroupInfo | Select.ListOccurrenceInfo))
                {
                    ListSelectedMDRFParts(currentReader);
                    return;
                }

                if (select.IsAnySet(Select.ListOccurrences | Select.ListMessages))
                {
                    ListOccurrencesAndMessages(currentReader);
                    return;
                }

                string noExtensionPath = mdrfFilePath.RemoveSuffixIfNeeded(".mdrf");
                if (!fileNameTag.IsNullOrEmpty())
                    noExtensionPath = "{0}_{1}".CheckedFormat(noExtensionPath, fileNameTag);

                string csvPath = noExtensionPath.AddSuffixIfNeeded(".csv");

                Console.WriteLine("Processing '{0}' {1} bytes => '{2}'".CheckedFormat(System.IO.Path.GetFileName(mdrfFilePath), currentReader.FileLength, System.IO.Path.GetFileName(csvPath)));

                if (System.IO.File.Exists(csvPath))
                    System.IO.File.Delete(csvPath);

                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(csvPath))
                {
                    bool headerAndDataOnly = select.IsSet(Select.HeaderAndDataOnly);

                    Func<IGroupInfo, bool> groupFilterDelegate = (selectedGroupList.IsNullOrEmpty() ? (Func<IGroupInfo, bool>)((IGroupInfo gi) => true) : ((IGroupInfo gi) => selectedGroupList.Any(grpName => gi.Name.Contains(grpName))));

                    IGroupInfo[] FilteredGroupInfoArray = currentReader.GroupInfoArray.Where(gi => groupFilterDelegate(gi)).ToArray();

                    if (!headerAndDataOnly)
                    {
                        sw.CheckedWriteLine("$File.Path,{0}{1}", System.IO.Path.GetFullPath(mdrfFilePath), currentReader.FileIndexInfo.FileWasProperlyClosed ? "" : ",NotProperlyClosed");

                        sw.CheckedWriteLine("$File.Size,{0}", currentReader.FileLength);
                        sw.CheckedWriteLine("$File.Date.First,{0:o}", currentReader.DateTimeInfo.UTCDateTime.ToLocalTime());
                        sw.CheckedWriteLine("$File.Date.Last,{0:o}", currentReader.FileIndexInfo.FileIndexRowArray.Select(row => row.FirstBlockDateTime + (row.LastBlockDeltaTimeStamp - row.FirstBlockDeltaTimeStamp).FromSeconds()).Max().ToLocalTime());
                        sw.CheckedWriteLine("$File.Elapsed.Hours,{0:f6}", currentReader.FileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp / 3600.0);

                        foreach (var key in new string[] { "HostName", "CurrentProcess.ProcessName", "Environment.MachineName", "Environment.OSVersion", "Environment.Is64BitOperatingSystem", "Environment.ProcessorCount" })
                        {
                            sw.WriteLineKeyIfPresent(currentReader.LibraryInfo.NVS, key);
                        }

                        if (FilteredGroupInfoArray.SafeLength() <= 1)
                            sw.CheckedWriteLine("$Group.Name,{0}", FilteredGroupInfoArray.Select(gi => gi.Name).ToArray().SafeAccess(0, "[NoGroupSelected]"));
                        else
                            sw.CheckedWriteLine("$Group.Names,{0}", String.Join(",", FilteredGroupInfoArray.Select((gi, idx) => "{0}:{1}".CheckedFormat(idx + 1, gi.Name)).ToArray()));

                        if (tailTime != 0.0)
                        {
                            sw.CheckedWriteLine("$Filter.TailTime,{0:f6}", tailTime);
                        }
                        else if (startDeltaTime != 0.0 || endDeltaTime != double.PositiveInfinity)
                        {
                            sw.CheckedWriteLine("$Filter.DeltaTime,{0:f6},{1:f6}", startDeltaTime, endDeltaTime);
                        }

                        sw.CheckedWriteLine("");
                    }

                    string[] columnNames = new[] { "DateTime", "DeltaTime" };
                    if (FilteredGroupInfoArray.SafeLength() <= 1)
                        columnNames = columnNames.Concat(FilteredGroupInfoArray.SelectMany(gi => gi.GroupPointInfoArray.Select(gpi => gpi.Name))).ToArray();
                    else
                        columnNames = columnNames.Concat(FilteredGroupInfoArray.SelectMany((gi, idx) => gi.GroupPointInfoArray.Select(gpi => "{0}:{1}".CheckedFormat(idx+1, gpi.Name)))).ToArray();

                    sw.CheckedWriteLine(String.Join(",", columnNames));

                    if (tailTime != 0.0)
                    {
                        endDeltaTime = currentReader.FileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp;
                        startDeltaTime = Math.Max(0.0, endDeltaTime - tailTime);
                    }

                    ReadAndProcessFilterSpec filterSpec = new ReadAndProcessFilterSpec()
                    {
                        FirstFileDeltaTimeStamp = startDeltaTime,
                        LastFileDeltaTimeStamp = endDeltaTime,
                        EventHandlerDelegate = (sender, pceData) => ProcessContentEventHandlerDelegate(sw, sender, pceData),
                        PCEMask = ProcessContentEvent.All,
                        FileIndexUserRowFlagBitsMask = 0,
                        NominalMinimumGroupAndTimeStampUpdateInterval = nominalUpdateInterval,
                        GroupFilterDelegate = groupFilterDelegate,
                    };

                    currentReader.ReadAndProcessContents(filterSpec);
                }
            }
        }

        private static MDRFFileReader currentReader = null;

        private static void ProcessContentEventHandlerDelegate(StreamWriter sw, object sender, ProcessContentEventData pceData)
        {
            bool debugSelected = select.IsSet(Select.Debug);
            bool includeExtrasSelected = select.IsSet(Select.IncludeExtras);

            switch (pceData.PCE)
            {
                case ProcessContentEvent.ReadingStart:
                case ProcessContentEvent.ReadingEnd:
                    if (debugSelected)
                        sw.CheckedWriteLine("$Debug {0}", pceData);
                    else if (includeExtrasSelected)
                        sw.CheckedWriteLine("${0} ts:{1:f6} {2:o}", pceData.PCE, pceData.FileDeltaTimeStamp, pceData.UTCDateTime.ToLocalTime());
                    return;
                case ProcessContentEvent.RowStart:
                case ProcessContentEvent.RowEnd:
                    if (debugSelected)
                        sw.CheckedWriteLine("$Debug {0}", pceData);
                    else if (includeExtrasSelected)
                        sw.CheckedWriteLine("${0} rowIdx:{1} firstTS:{2:f6} {3:o} userFlags:0x{4:x16}", pceData.PCE, pceData.Row.RowIndex, pceData.FileDeltaTimeStamp, pceData.UTCDateTime.ToLocalTime(), pceData.Row.FileIndexUserRowFlagBits);
                    return;

                case ProcessContentEvent.StartOfFullGroup:
                    if (debugSelected)
                        sw.CheckedWriteLine("$Debug {0}", pceData);
                    else if (includeExtrasSelected)
                        sw.CheckedWriteLine("${0}", pceData.PCE);
                    return;

                case ProcessContentEvent.GroupSetEnd:
                    if (debugSelected)
                        sw.CheckedWriteLine("$Debug {0}", pceData);

                    if (select.IsSet(Select.NoData))
                    { }
                    else if (pceData.FilteredGroupInfoArray.Any(gi => gi.Touched))
                    {
                        sw.CheckedWrite("{0:MM/dd/yyyy HH:mm:ss.fff},{1:f6}", pceData.UTCDateTime.ToLocalTime(), pceData.FileDeltaTimeStamp);

                        foreach (IGroupInfo gi in pceData.FilteredGroupInfoArray)
                        {
                            if (gi.Touched || !select.IsSet(Select.Sparse))
                            {
                                foreach (var gpiVC in gi.GroupPointInfoArray.Select(gpi => gpi.VC))
                                {
                                    string gpiVCStr;
                                    if (gpiVC.cvt == ContainerStorageType.Boolean && select.IsSet(Select.MapBool))
                                        gpiVCStr = gpiVC.GetValue<int>(rethrow: false).ToString();
                                    else if (gpiVC.cvt.IsString() || gpiVC.IsObject || gpiVC.IsNullOrEmpty)
                                        gpiVCStr = gpiVC.ToString();
                                    else
                                        gpiVCStr = gpiVC.ValueAsObject.SafeToString();

                                    sw.CheckedWrite(",{0}", gpiVCStr.Replace(',', '|'));
                                }
                            }
                            else
                            {
                                sw.CheckedWrite(new string(',', gi.GroupPointInfoArray.SafeLength()));
                            }

                            gi.Touched = false;
                        }

                        sw.CheckedWriteLine("");
                    }
                    return;

                case ProcessContentEvent.Group:
                case ProcessContentEvent.PartialGroup:
                case ProcessContentEvent.EmptyGroup:
                case (ProcessContentEvent.Group | ProcessContentEvent.PartialGroup):
                case (ProcessContentEvent.Group | ProcessContentEvent.EmptyGroup):
                    if (debugSelected)
                        sw.CheckedWriteLine("$Debug {0}", pceData);
                    return;

                case ProcessContentEvent.Occurrence:
                    if (debugSelected)
                        sw.CheckedWriteLine("$Debug {0}", pceData);
                    else if (select.IsSet(Select.IncludeOccurrences))
                        sw.CheckedWriteLine("${0} ts:{1:f6} {2:o} {3} {4}", pceData.PCE, pceData.FileDeltaTimeStamp, pceData.UTCDateTime.ToLocalTime(), pceData.OccurrenceInfo.Name, pceData.VC);
                    return;

                case ProcessContentEvent.Error:
                case ProcessContentEvent.Message:
                    if (debugSelected)
                        sw.CheckedWriteLine("$Debug {0}", pceData);
                    else if (includeExtrasSelected || pceData.PCE == ProcessContentEvent.Error)
                        sw.CheckedWriteLine("${0} ts:{1:f6} {2:o} {3}", pceData.PCE, pceData.FileDeltaTimeStamp, pceData.UTCDateTime.ToLocalTime(), pceData.MessageInfo.Message);
                    return;

                default:
                    return;
            }
        }

        public static void ListSelectedMDRFParts(PartsLib.Tools.MDRF.Reader.MDRFFileReader reader)
        {
            Console.WriteLine("File: '{0}' size: {1} kB{2}", Path.GetFileName(reader.FilePath), reader.FileLength * (1.0 / 1024), reader.FileIndexInfo.FileWasProperlyClosed ? "" : " NotProperlyClosed");

            if (select.IsSet(Select.ListInfo))
            {
                Console.WriteLine(" Date First: {0:o}", reader.DateTimeInfo.UTCDateTime.ToLocalTime());
                Console.WriteLine(" Date  Last: {0:o}", reader.FileIndexInfo.FileIndexRowArray.Select(row => row.FirstBlockDateTime + (row.LastBlockDeltaTimeStamp - row.FirstBlockDeltaTimeStamp).FromSeconds()).Max().ToLocalTime());
                Console.WriteLine(" Elapsed Hours: {0:f6}", reader.FileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp / 3600.0);

                foreach (var key in new string[] { "Environment.MachineName", "Environment.OSVersion" })
                {
                    currentReader.LibraryInfo.NVS.ConsoleWriteLineKeyIfPresent(key);
                }

                if (!select.IsSet(Select.ListGroupInfo))
                    Console.WriteLine(" Groups: {0}", String.Join(",", reader.GroupInfoArray.Select(gi => gi.Name).ToArray()));
                if (!select.IsSet(Select.ListOccurrenceInfo))
                    Console.WriteLine(" Occurrences: {0}", String.Join(",", reader.OccurrenceInfoArray.Select(oi => oi.Name).ToArray()));
            }

            if (select.IsSet(Select.ListIndex))
            {
                Console.WriteLine("Index NumRows: {0} RowSizeDivisor: {1}", reader.FileIndexInfo.NumRows, reader.FileIndexInfo.RowSizeDivisor);
                List<int> emptyRowIndexList = new List<int>();

                for (int rowIdx = 0; rowIdx < reader.FileIndexInfo.NumRows; rowIdx++)
                {
                    FileIndexRowBase row = reader.FileIndexInfo.FileIndexRowArray.SafeAccess(rowIdx);
                    if (row == null || row.IsEmpty)
                        emptyRowIndexList.Add(rowIdx);
                    else
                    {
                        WriteEmptyRows(" EmptyRow(s):", emptyRowIndexList);

                        Console.WriteLine(" RowIdx:{0:d4} Offset:{1} RFBits:${2:X4} URFBits:${3:X8} 1stDTS:{4:f3} lastDTS:{5:f3} 1stDT:{6:yyyyMMdd_HHmmssfff}", rowIdx, row.FileOffsetToStartOfFirstBlock, (ulong)row.FileIndexRowFlagBits, row.FileIndexUserRowFlagBits, row.LastBlockDeltaTimeStamp, row.FirstBlockDeltaTimeStamp, row.FirstBlockDateTime);
                        if (row.FileIndexRowFlagBits != FileIndexRowFlagBits.None)
                            Console.WriteLine("              RFBits:{0}", row.FileIndexRowFlagBits);
                    }
                }

                WriteEmptyRows(" EmptyRow(s):", emptyRowIndexList);

                Console.WriteLine(" LastBlock: type: {0} size: {1}", reader.FileIndexInfo.LastBlockInfo.FixedBlockTypeID, reader.FileIndexInfo.LastBlockInfo.BlockTotalLength);
            }

            if (select.IsSet(Select.ListGroupInfo))
            {
                if (reader.GroupInfoArray.SafeCount() > 0)
                {
                    foreach (var gi in reader.GroupInfoArray)
                    {
                        Console.WriteLine("GroupInfo '{0}' id:{1} fileID:{2} URFBits:${3:x4} points:{4}", gi.Name, gi.GroupID, gi.FileID, gi.FileIndexUserRowFlagBits, GetClippedGPIListString(gi, clipAfterLength: 55));
                    }
                }
                else
                {
                    Console.WriteLine("No Groups are defined");
                }
            }

            if (select.IsSet(Select.ListOccurrenceInfo))
            {
                if (reader.OccurrenceInfoArray.SafeCount() > 0)
                {
                    foreach (var oi in reader.OccurrenceInfoArray)
                    {
                        Console.WriteLine("OccurrenceInfo '{0}' id:{1} fileID:{2} URFBits:${3:x4} ", oi.Name, oi.OccurrenceID, oi.FileID, oi.FileIndexUserRowFlagBits);
                    }
                }
                else
                {
                    Console.WriteLine("No Occurrences are defined");
                }
            }

            Console.WriteLine();
        }

        public static void ListOccurrencesAndMessages(PartsLib.Tools.MDRF.Reader.MDRFFileReader reader)
        {
            Console.WriteLine("File: '{0}' size: {1} kB{2}", Path.GetFileName(reader.FilePath), reader.FileLength * (1.0 / 1024), reader.FileIndexInfo.FileWasProperlyClosed ? "" : " NotProperlyClosed");

            int lineCount = 0;
            bool listMessages = select.IsSet(Select.ListMessages);
            bool listOccurrences = select.IsSet(Select.ListOccurrences);

            ReadAndProcessFilterSpec filterSpec = new ReadAndProcessFilterSpec()
            {
                EventHandlerDelegate = (sender, pceData) => ProcessContentEventHandlerDelegate2(sender, pceData, ref lineCount),
                PCEMask = ProcessContentEvent.None.Set(ProcessContentEvent.Occurrence, listOccurrences).Set(ProcessContentEvent.Message | ProcessContentEvent.Error, listMessages),
            };

            currentReader.ReadAndProcessContents(filterSpec);

            Console.WriteLine();
        }

        private static void ProcessContentEventHandlerDelegate2(object sender, ProcessContentEventData pceData, ref int lineCount)
        {
            switch (pceData.PCE)
            {
                case ProcessContentEvent.Occurrence:
                    Console.WriteLine("{0:d5} ts:{1:f3} {2:o} {3} {4} {5}".CheckedFormat(++lineCount, pceData.FileDeltaTimeStamp, pceData.UTCDateTime.ToLocalTime(), pceData.PCE, pceData.OccurrenceInfo.Name, pceData.VC));
                    return;

                case ProcessContentEvent.Error:
                case ProcessContentEvent.Message:
                    Console.WriteLine("{0:d5} ts:{1:f3} {2:o} {3} {4}".CheckedFormat(++lineCount, pceData.FileDeltaTimeStamp, pceData.UTCDateTime.ToLocalTime(), pceData.PCE, pceData.MessageInfo.Message));
                    return;

                default:
                    return;
            }
        }

        private static string GetClippedGPIListString(IGroupInfo gi, int clipAfterLength = 60)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var gpi in gi.GroupPointInfoArray)
            {
                string gpiName = gpi.Name.MapNullToEmpty();

                if (sb.Length > 0)
                    sb.Append(",");

                if (sb.Length + gpiName.Length <= clipAfterLength)
                    sb.Append(gpiName);
                else
                    return "{0}...".CheckedFormat(sb);
            }

            return sb.ToString();
        }

        private static void WriteEmptyRows(string prefix, List<int> emptyRowIndexList)
        {
            if (!emptyRowIndexList.IsNullOrEmpty())
            {
                StringBuilder sb = new StringBuilder();

                while (emptyRowIndexList.Count > 0)
                {
                    int rangeStart = emptyRowIndexList.SafeTakeFirst(defaultValue: -1);
                    int rangeEnd = rangeStart;

                    while (emptyRowIndexList.Count > 0 && emptyRowIndexList[0] == rangeEnd + 1)
                    {
                        rangeEnd += 1;
                        emptyRowIndexList.RemoveAt(0);
                    }

                    if (sb.Length > 0)
                        sb.Append(",");

                    if (rangeStart == rangeEnd)
                        sb.CheckedAppendFormat("{0}", rangeStart);
                    else if (rangeStart+1 == rangeEnd)
                        sb.CheckedAppendFormat("{0},{1}", rangeStart, rangeEnd);
                    else
                        sb.CheckedAppendFormat("{0}..{1}", rangeStart, rangeEnd);
                }

                Console.WriteLine("{0}{1}", prefix, sb.ToString());
            }
        }
    }

    public static partial class ExtensionMethods
    {
        public static void WriteLineKeyIfPresent(this StreamWriter sw, INamedValueSet nvs, string key)
        {
            if (nvs.Contains(key))
                sw.CheckedWriteLine("${0},{1}", key, nvs[key].VC);
        }

        public static void ConsoleWriteLineKeyIfPresent(this INamedValueSet nvs, string key)
        {
            if (nvs.Contains(key))
                Console.WriteLine(" {0}".CheckedFormat(nvs[key]));
        }
    }
}
