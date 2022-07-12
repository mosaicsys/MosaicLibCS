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

using Mosaic.ToolsLib.MDRF2.Reader;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MosaicLib.Tools.ExtractMDRFtoCSV
{
    public static class ExtractMDRFtoCSV
    {
        private static string appName = "AppNameNotFound";
        private static Logging.ILogger appLogger;
        private static System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();

        static void Main(string[] args)
        {
            try
            {
                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                appName = System.IO.Path.GetFileName(currentProcess.MainModule.ModuleName);
                appLogger = new Logging.Logger("Main");

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

        //MDRF2DirectoryTracker 

        /// <summary>
        /// Bitmask used by command line parser to help record what what the client wants the tool to do.
        /// <para/>None, IncludeOccurrences, IncludeObjects, IncludeExtra, List, ListIndex, ListGroups, ListOccurrences, ListMessages, ListObjects, Debug, MapBool
        /// </summary>
        [Flags]
        public enum Select
        {
            None = 0x0000,
            IncludeOccurrences = 0x0001,
            IncludeExtras = 0x0002,
            //Sparse = 0x0004,
            NoData = 0x0008,
            HeaderAndDataOnly = 0x0010,
            IncludeObjects = 0x0020,
            ListInfo = 0x0100,
            ListIndex = 0x0200,
            ListGroupInfo = 0x0400,
            ListOccurrenceInfo = 0x0800,
            ListOccurrences = 0x1000,
            ListMessages = 0x2000,
            ListObjects = 0x4000,
            Debug = 0x10000,
            MapBool = 0x20000,

            io = IncludeOccurrences,
            ie = IncludeExtras,
            ix = IncludeExtras,
            //s = Sparse,
            nd = NoData,
            hado = HeaderAndDataOnly,
            iobj = IncludeObjects,
            l = ListInfo,
            List = ListInfo,
            li = ListIndex,
            lgi = ListGroupInfo,
            loi = ListOccurrenceInfo,
            lo = ListOccurrences,
            lm = ListMessages,
            lobj = ListObjects,
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
            Console.WriteLine("Usage: {0} [-IncludeOccurrences | -IncludeExtras | -IncludeObjects".CheckedFormat(appName));
            Console.WriteLine("       | -NoData | -HeaderAndDataOnly | -MapBool | -group:name | -tag:tag");
            Console.WriteLine("       | -interval:interval | -start:deltaTime | -end:deltaTime | -tail:period] [fileName.mdrf|.mdrf2|.mdrf2.lz4|.mdrf2.gz] ...");
            Console.WriteLine("   or: {0} [-List | -ListIndex | -ListGroupInfo | -ListOccurrenceInfo | -ListObjects] [fileName.mdrf] ...".CheckedFormat(appName));
            Console.WriteLine("   or: {0} [-ListOccurrences | -ListMessages] [fileName.mdrf] ...".CheckedFormat(appName));
            Console.WriteLine(" Also accepts: -io, -ie, -iobj, -nd, -hado, -mb, -g:name, -t:tag, -s:dt, -e:dt, -i:interval");
            Console.WriteLine("               -l, -li, -lgi, -loi");
            Console.WriteLine("               -lo, -lm, -lobj");
            Console.WriteLine();

            Console.WriteLine("Assembly: {0}", currentExecAssy.GetSummaryNameAndVersion());
        }

        private static void ProcessFileNameArg(string arg)
        {
            if (!System.IO.Path.HasExtension(arg))
                arg += ".mdrf2.lz4";

            string filePart = System.IO.Path.GetFileName(arg);
            string pathPart = System.IO.Path.GetDirectoryName(arg).MapNullOrEmptyTo(".");

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

        static IMDRF2QueryFactory mdrf2QueryFactory;

        private static void ProcessMDRFFile(string mdrfFilePath)
        {
            var mdrFileName = Path.GetFileName(mdrfFilePath);
            mdrf2QueryFactory = mdrf2QueryFactory ?? new MDRF2QueryFactory(appLogger);

            var fileInfo = new MDRF2FileInfo(mdrfFilePath).PopulateIfNeeded();

            QpcTimeStamp startTime = QpcTimeStamp.Now;

            var digestInfo = mdrf2QueryFactory.GetFileDigestInfo(fileInfo);

            var runTime = startTime.Age;

            var fileSummary = digestInfo.Item2;
            var specItemSet = fileInfo.SpecItemSet;

            {
                if (!fileInfo.IsUsable)
                {
                    Console.WriteLine($"'{System.IO.Path.GetFileName(mdrfFilePath)}': Is not usable: {fileInfo.FaultCode}");
                    return;
                }

                if (select.IsAnySet(Select.ListInfo | Select.ListIndex | Select.ListGroupInfo | Select.ListOccurrenceInfo))
                {
                    Console.WriteLine("File: '{0}' size: {1:f3} kB{2}", Path.GetFileName(fileInfo.FileNameAndRelativePath), fileInfo.FileLength * (1.0 / 1024), fileSummary.EndRecordFound ? "" : ",NotProperlyClosed");

                    ListSelectedMDRFParts(fileInfo, digestInfo);

                    return;
                }

                if (select.IsAnySet(Select.ListOccurrences | Select.ListMessages))
                {
                    Console.WriteLine("File: '{0}' size: {1:f3} kB{2}", Path.GetFileName(fileInfo.FileNameAndRelativePath), fileInfo.FileLength * (1.0 / 1024), fileSummary.EndRecordFound ? "" : ",NotProperlyClosed");

                    ListOccurrencesAndObjectsAndMessages(fileInfo);

                    return;
                }

                string noExtensionPath = mdrfFilePath.RemoveSuffixIfNeeded(".mdrf").RemoveSuffixIfNeeded(".mdrf2").RemoveSuffixIfNeeded(".mdrf2.lz4");
                if (!fileNameTag.IsNullOrEmpty())
                    noExtensionPath = "{0}_{1}".CheckedFormat(noExtensionPath, fileNameTag);

                string csvPath = noExtensionPath.AddSuffixIfNeeded(".csv");
                string csvName = Path.GetFileName(csvPath);

                Console.WriteLine($"Processing '{mdrFileName}' => '{csvName}'");

                if (System.IO.File.Exists(csvPath))
                    System.IO.File.Delete(csvPath);

                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(csvPath))
                {
                    bool headerAndDataOnly = select.IsSet(Select.HeaderAndDataOnly);

                    Func<IGroupInfo, bool> groupFilterDelegate = (selectedGroupList.IsNullOrEmpty() ? (Func<IGroupInfo, bool>)((IGroupInfo gi) => true) : ((IGroupInfo gi) => selectedGroupList.Any(grpName => gi.Name.Contains(grpName))));

                    IGroupInfo[] FilteredGroupInfoArray = specItemSet.GroupDictionary.ValueArray.Where(gi => groupFilterDelegate(gi)).ToArray();

                    if (!headerAndDataOnly)
                    {
                        var indexStateStr = fileSummary.EndRecordFound ? "" : ",NotProperlyClosed";
                        sw.CheckedWriteLine("$File.Path,{0}{1}", System.IO.Path.GetFullPath(mdrfFilePath).GenerateRFC4180EscapedVersion(), indexStateStr);

                        sw.CheckedWriteLine("$File.Size,{0}", fileInfo.FileLength);
                        sw.CheckedWriteLine("$File.Date.First,{0:o}", fileInfo.DateTimeInfo.UTCDateTime.ToLocalTime());
                        sw.CheckedWriteLine("$File.Date.Last,{0:o}", fileSummary.LastFileDTPair.DateTime.ToLocalTime());
                        sw.CheckedWriteLine("$File.Elapsed.Hours,{0:f6}", fileSummary.LastFileDTPair.FileDeltaTime.FromSeconds().TotalHours);

                        foreach (var key in new string[]
                            {
                                "HostName",
                                "Environment.MachineName", "MachineName",
                                "Environment.OSVersion", "OSVersion",
                                "Environment.Is64BitOperatingSystem", "Is64BitOperatingSystem",
                                "Environment.ProcessorCount", "ProcessorCount",
                                "CurrentProcess.ProcessName", "ProcessName",
                            })
                        {
                            foreach (var nvs in fileInfo.NonSpecMetaNVS)
                                sw.WriteLineKeyIfPresent(nvs.VC.GetValueNVS(rethrow: false).MapNullToEmpty(), key);
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
                        columnNames = columnNames.Concat(FilteredGroupInfoArray.SelectMany((gi, idx) => gi.GroupPointInfoArray.Select(gpi => "{0}:{1}".CheckedFormat(idx + 1, gpi.Name)))).ToArray();

                    sw.CheckedWriteLine(String.Join(",", columnNames.Select(name => name.GenerateRFC4180EscapedVersion())));

                    var fullPointNamesArray = FilteredGroupInfoArray.SelectMany(gi => gi.GroupPointInfoArray.Select(gpi => gpi.FullName)).ToArray();
                    MDRF2QuerySpec querySpec = new MDRF2QuerySpec()
                    {
                        AllowRecordReuse = true,
                        PointSetSpec = Tuple.Create(fullPointNamesArray, MDRF2PointSetArrayItemType.VC | MDRF2PointSetArrayItemType.ReuseArrays, nominalUpdateInterval.FromSeconds()),
                        ItemTypeSelect = MDRF2QueryItemTypeSelect.All & ~(MDRF2QueryItemTypeSelect.FileDeltaTimeUpdate),
                        OccurrenceNameArray = null, // all occurrences
                        ObjectTypeNameSet = null, // all object types
                    };

                    if (tailTime == 0.0)
                    {
                        querySpec.StartUTCTimeSince1601 = fileInfo.DateTimeInfo.UTCTimeSince1601 + startDeltaTime;
                        querySpec.EndUTCTimeSince1601 = fileInfo.DateTimeInfo.UTCTimeSince1601 + endDeltaTime;
                    }
                    else
                    {
                        querySpec.StartUTCTimeSince1601 = fileSummary.LastFileDTPair.UTCTimeSince1601 - tailTime;
                        querySpec.EndUTCTimeSince1601 = fileSummary.LastFileDTPair.UTCTimeSince1601;
                    }

                    bool debugSelected = select.IsSet(Select.Debug);
                    bool includeExtrasSelected = select.IsSet(Select.IncludeExtras) && !debugSelected;
                    bool includeOccurrences = select.IsSet(Select.IncludeOccurrences) && !debugSelected;
                    bool includePointSetData = !select.IsSet(Select.NoData);
                    bool mapBool = select.IsSet(Select.MapBool);
                    bool mapNone = true;

                    int inlineIndexCount = 0;

                    foreach (var record in mdrf2QueryFactory.CreateQuery(querySpec, fileInfo))
                    {
                        var dtPair = record.DTPair;

                        if (debugSelected)
                            sw.CheckedWriteLine("$Debug {0}", record);

                        switch (record.ItemType)
                        {
                            case MDRF2QueryItemTypeSelect.FileStart:
                            case MDRF2QueryItemTypeSelect.FileEnd:
                                if (includeExtrasSelected)
                                    sw.CheckedWriteLine("${0} ts:{1:f6} {2:o}", record.ItemType, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime());
                                break;
                            case MDRF2QueryItemTypeSelect.InlineIndexRecord:
                            case MDRF2QueryItemTypeSelect.InlineIndexRecord | MDRF2QueryItemTypeSelect.WillSkip:
                                inlineIndexCount++;
                                if (includeExtrasSelected)
                                    sw.CheckedWriteLine("${0} rowIdx:{1} firstTS:{2:f6} {3:o} userFlags:0x{4:x16}", record.ItemType, inlineIndexCount, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), (record.DataAsObject as Mosaic.ToolsLib.MDRF2.Common.InlineIndexRecord)?.BlockUserRowFlagBits);
                                break;
                            case MDRF2QueryItemTypeSelect.BlockEnd:
                                if (includeExtrasSelected)
                                    sw.CheckedWriteLine("${0} rowIdx:{1} firstTS:{2:f6} {3:o} userFlags:0x{4:x16}", record.ItemType, inlineIndexCount, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), (record.DataAsObject as Mosaic.ToolsLib.MDRF2.Common.InlineIndexRecord)?.BlockUserRowFlagBits);
                                break;
                            case MDRF2QueryItemTypeSelect.PointSet:
                                if (includePointSetData)
                                {
                                    sw.CheckedWrite("{0:MM/dd/yyyy HH:mm:ss.fff},{1:f6}", dtPair.DateTime.ToLocalTime(), dtPair.FileDeltaTime);

                                    foreach (var vc in (record.DataAsObject as ValueContainer[]).MapNullToEmpty())
                                    {
                                        string vcStr;
                                        if (vc.cvt == ContainerStorageType.Boolean && mapBool)
                                            vcStr = vc.GetValueI4(rethrow: false).ToString();
                                        else if (vc.cvt.IsString() || vc.IsObject || vc.IsNull)
                                            vcStr = vc.ToString();
                                        else if (vc.IsEmpty)
                                            vcStr = (mapNone) ? string.Empty : vc.ToString();
                                        else
                                            vcStr = vc.ValueAsObject.SafeToString();

                                        sw.CheckedWrite(",{0}", vcStr.GenerateRFC4180EscapedVersion());
                                    }

                                    sw.CheckedWriteLine("");
                                }
                                break;
                            case MDRF2QueryItemTypeSelect.Occurrence:
                                if (includeOccurrences)
                                {
                                    var oqrd = (MDRF2OccurrenceQueryRecordData)record.DataAsObject;
                                    sw.CheckedWriteLine("${0} ts:{1:f6} {2:o} {3} {4}", record.ItemType, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), oqrd.OccurrenceInfo.Name, oqrd.VC);
                                }
                                break;
                            case MDRF2QueryItemTypeSelect.Object:
                                if (includeOccurrences)
                                    sw.CheckedWriteLine("${0} ts:{1:f6} {2:o} {3}", record.ItemType, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), ValueContainer.Create(record.DataAsObject));
                                break;
                            case MDRF2QueryItemTypeSelect.Mesg:
                            case MDRF2QueryItemTypeSelect.Error:
                                if (includeExtrasSelected)
                                    sw.CheckedWriteLine("${0} ts:{1:f6} {2:o} {3}", record.ItemType, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), ValueContainer.Create(record.DataAsObject));
                                break;
                            case MDRF2QueryItemTypeSelect.DecodingIssue:
                                {
                                    if (!(record.DataAsObject is System.Exception ex))
                                        sw.CheckedWriteLine("${0} ts:{1:f6} {2:o} {3}", record.ItemType, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), ValueContainer.Create(record.DataAsObject));
                                    else
                                        sw.CheckedWriteLine("${0} ts:{1:f6} {2:o} {3}", record.ItemType, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), ex.ToString(ExceptionFormat.TypeAndMessage));
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        public static void ListSelectedMDRFParts(IMDRF2FileInfo fileInfo, Tuple<IMDRF2FileInfo, MDRF2QueryFileSummary, Mosaic.ToolsLib.MDRF2.Common.InlineIndexRecord[]> digestInfo)
        {
            var fileSummary = digestInfo.Item2;
            var specItemSet = fileInfo.SpecItemSet;
            var inlineIndexArray = digestInfo.Item3;

            if (select.IsSet(Select.ListInfo))
            {
                Console.WriteLine(" Date First: {0:o}", fileInfo.DateTimeInfo.UTCDateTime.ToLocalTime());
                Console.WriteLine(" Date  Last: {0:o}", fileSummary.LastFileDTPair.DateTime.ToLocalTime());
                Console.WriteLine(" Elapsed Hours: {0:f6}", fileSummary.LastFileDTPair.FileDeltaTime.FromSeconds().TotalHours);

                foreach (var key in new string[] { "Environment.MachineName", "Environment.OSVersion", "MachineName", "OSVersion" })
                {
                    foreach (var nvs in fileInfo.NonSpecMetaNVS)
                        nvs.VC.GetValueNVS(rethrow: false).MapNullToEmpty().ConsoleWriteLineKeyIfPresent(key);
                }

                if (!select.IsSet(Select.ListGroupInfo))
                    Console.WriteLine(" Groups: {0}", String.Join(",", specItemSet.GroupDictionary.ValueArray.Select(gi => gi.Name).ToArray()));
                if (!select.IsSet(Select.ListOccurrenceInfo))
                    Console.WriteLine(" Occurrences: {0}", String.Join(",", specItemSet.OccurrenceDictionary.ValueArray.Select(oi => oi.Name).ToArray()));
            }

            if (select.IsSet(Select.ListIndex))
            {
                Console.WriteLine("Index NumRows: {0}", inlineIndexArray.Length);
                List<int> emptyRowIndexList = new List<int>();

                FileIndexRowFlagBits mask = ~FileIndexRowFlagBits.ContainsGroup;
                for (int rowIdx = 0; rowIdx < inlineIndexArray.Length; rowIdx++)
                {
                    var row = inlineIndexArray[rowIdx];
                    {
                        Console.WriteLine(" RowIdx:{0:d4} Size:{1} RFBits:${2:X4} URFBits:${3:X8} 1stDTS:{4:f3} lastDTS:{5:f3} 1stDT:{6:yyyyMMdd_HHmmssfff}", rowIdx, row.BlockByteCount, (ulong)row.BlockFileIndexRowFlagBits, row.BlockUserRowFlagBits, row.BlockLastFileDeltaTime, row.BlockFirstFileDeltaTime, row.FirstDTPair.DateTime.ToLocalTime());
                        if ((row.BlockFileIndexRowFlagBits & mask) != FileIndexRowFlagBits.None)
                            Console.WriteLine("              RFBits:{0}", row.BlockFileIndexRowFlagBits & mask);
                    }
                }
            }

            if (select.IsSet(Select.ListGroupInfo))
            {
                var groupInfoArray = specItemSet.GroupDictionary.ValueArray;
                if (groupInfoArray.SafeCount() > 0)
                {
                    foreach (var gi in groupInfoArray)
                    {
                        Console.WriteLine("GroupInfo '{0}' id:{1} fileID:{2} URFBits:${3:x4} points:{4}", gi.Name, gi.GroupID, gi.FileID, gi.FileIndexUserRowFlagBits, GetClippedGPIListString(gi, clipAfterLength: 100));
                    }
                }
                else
                {
                    Console.WriteLine("No Groups are defined");
                }
            }

            if (select.IsSet(Select.ListOccurrenceInfo))
            {
                var occurrenceInfoArray = specItemSet.OccurrenceDictionary.ValueArray;
                if (occurrenceInfoArray.SafeCount() > 0)
                {
                    foreach (var oi in occurrenceInfoArray)
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

        public static void ListOccurrencesAndObjectsAndMessages(IMDRF2FileInfo fileInfo)
        {
            bool listMessages = select.IsSet(Select.ListMessages);
            bool listOccurrences = select.IsSet(Select.ListOccurrences);
            bool listObjects = select.IsSet(Select.ListObjects);

            MDRF2QuerySpec querySpec = new MDRF2QuerySpec()
            {
                ItemTypeSelect = ((listMessages) ? (MDRF2QueryItemTypeSelect.Mesg | MDRF2QueryItemTypeSelect.Error | MDRF2QueryItemTypeSelect.DecodingIssue) : default)
                                | ((listOccurrences) ? (MDRF2QueryItemTypeSelect.Occurrence) : default)
                                | ((listObjects) ? (MDRF2QueryItemTypeSelect.Object) : default),
                OccurrenceNameArray = null,
                PointSetSpec = Tuple.Create<string[], MDRF2PointSetArrayItemType, TimeSpan>(EmptyArrayFactory<string>.Instance, MDRF2PointSetArrayItemType.None, TimeSpan.Zero),
                ObjectTypeNameSet = null,
            };

            int lineCount = 0;

            foreach (var record in mdrf2QueryFactory.CreateQuery(querySpec, fileInfo))
            {
                var dtPair = record.DTPair;
                switch (record.ItemType)
                {
                    case MDRF2QueryItemTypeSelect.Occurrence:
                        {
                            var queryData = (MDRF2OccurrenceQueryRecordData)record.DataAsObject;
                            Console.WriteLine("{0:d5} ts:{1:f3} {2:o} {3} {4} {5}".CheckedFormat(++lineCount, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), record.ItemType, queryData.OccurrenceInfo.Name, queryData.VC));
                        }
                        break;
                    case MDRF2QueryItemTypeSelect.Object:
                        {
                            Console.WriteLine("{0:d5} ts:{1:f3} {2:o} {3} {4}".CheckedFormat(++lineCount, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), record.ItemType, ValueContainer.Create(record.DataAsObject)));
                        }
                        break;
                    case MDRF2QueryItemTypeSelect.Mesg:
                    case MDRF2QueryItemTypeSelect.Error:
                        {
                            Console.WriteLine("{0:d5} ts:{1:f3} {2:o} {3} {4}".CheckedFormat(++lineCount, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), record.ItemType, ValueContainer.Create(record.DataAsObject)));
                        }
                        break;
                    case MDRF2QueryItemTypeSelect.DecodingIssue:
                        {
                            if (!(record.DataAsObject is System.Exception ex))
                                Console.WriteLine("{0:d5} ts:{1:f3} {2:o} {3} {4}".CheckedFormat(++lineCount, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), record.ItemType, ValueContainer.Create(record.DataAsObject)));
                            else
                                Console.WriteLine("{0:d5} ts:{1:f3} {2:o} {3} {4}".CheckedFormat(++lineCount, dtPair.FileDeltaTime, dtPair.DateTime.ToLocalTime(), record.ItemType, ex.ToString(ExceptionFormat.TypeAndMessage)));
                        }
                        break;
                    default:
                        break;
                }
            }

            Console.WriteLine();
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
                    sb.Append(gpiName.GenerateRFC4180EscapedVersion());
                else
                    return "{0}...".CheckedFormat(sb);
            }

            return sb.ToString();
        }
    }

    public static partial class ExtensionMethods
    {
        public static void WriteLineKeyIfPresent(this StreamWriter sw, INamedValueSet nvs, string key)
        {
            if (nvs.Contains(key))
                sw.CheckedWriteLine("${0},{1}", key.GenerateRFC4180EscapedVersion(), nvs[key].VC.ToString().GenerateRFC4180EscapedVersion());
        }

        public static void ConsoleWriteLineKeyIfPresent(this INamedValueSet nvs, string key)
        {
            if (nvs.Contains(key))
                Console.WriteLine(" {0}".CheckedFormat(nvs[key].ToString().GenerateRFC4180EscapedVersion()));
        }
    }
}
