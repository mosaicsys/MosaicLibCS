//-------------------------------------------------------------------
/*! @file ExtractMDRFtoDB.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2019 Mosaic Systems Inc.
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
using System.Threading.Tasks;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.PartsLib.Tools.MDRF.Reader;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Utils.StringMatching;

using System.Data.SQLite;

namespace MosaicLib.Tools.ExtractMDRFtoDB
{
    public static class ExtractMDRFtoDB
    {
        /// <summary>
        /// Defines the supported data file output formats.
        /// <para/>None (0), SQLite3, SQLite, CSV
        /// </summary>
        public enum DataFileType : int
        {
            None = 0,
            
            SQLite3,
            SQLite,

            CSV,
        }

        /// <summary>
        /// Append, Truncate
        /// </summary>
        public enum CreateMode : int
        {
            Append = 0,
            Truncate,
        }

        public class Settings
        {
            [ConfigItem]
            public string MDRFFileSpec = "";

            [ConfigItem]
            public bool AutoCDToIniFileDirectory = true;

            [ConfigItem]
            public DataFileType DataFileType = DataFileType.None;

            [ConfigItem]
            public string DataFileName = null;

            [ConfigItem]
            public CreateMode CreateMode = CreateMode.Truncate;

            [ConfigItem]
            public DateTime StartDateTime;

            [ConfigItem]
            public DateTime EndDateTime;

            [ConfigItem]
            public TimeSpan TailPeriod
            {
                get { return TimeSpan.Zero; }
                set 
                {
                    var dtNow = DateTime.Now;
                    EndDateTime = dtNow;
                    StartDateTime = dtNow - value;
                }
            }

            [ConfigItem]
            public TimeSpan NominalSampleInterval;

            public bool UsingNominalSampleInterval;

            [ConfigItem]
            public char ItemSplitChar = ',';

            [ConfigItem]
            public char ToSplitChar = ':';

            [ConfigItem]
            public string IncludeGroupSpecStr { get; set; }

            [ConfigItem]
            public string IncludeIOPointSpecStr { get; set; }

            [ConfigItem]
            public string IncludeIOPointPrefixSpecStr { get; set; }

            [ConfigItem]
            public string IncludeIOPointContainsSpecStr { get; set; }

            [ConfigItem]
            public string ExcludeIOPointContainsSpecStr { get; set; }

            [ConfigItem]
            public string IncludeOccurrenceSpecStr { get; set; }

            [ConfigItem]
            public string MapSpecStr { get; set; }

            [ConfigItem]
            public int MaxThreadsToUse { get; set; }

            [ConfigItem]
            public bool AddOccurrenceKeyColumns { get; set; }

            public Settings UpdateFromConfig(IConfig iConfig = null, bool setupForUse = true)
            {
                ConfigValueSetAdapter<Settings> adapter = new ConfigValueSetAdapter<Settings>(iConfig ?? Config.Instance) { ValueSet = this };
                adapter.Setup();

                if (setupForUse)
                    SetupForUse();

                return this;
            }

            private HashSet<string> IncludeGroupSet, IncludeIOPointSet, IncludeOccurrenceSet;
            private List<string> IncludeIOPointPrefixSet, IncludeIOPointContainsSet, ExcludeIOPointContainsSet;

            public MapNameFromToList MapNameFromToList;

            public Settings SetupForUse()
            {
                UsingNominalSampleInterval = !NominalSampleInterval.IsZero();

                if (IncludeGroupSpecStr.IsNeitherNullNorEmpty())
                    IncludeGroupSet = new HashSet<string>(IncludeGroupSpecStr.Split(ItemSplitChar).Select(item => item.Trim()));

                if (IncludeIOPointSpecStr.IsNeitherNullNorEmpty())
                    IncludeIOPointSet = new HashSet<string>(IncludeIOPointSpecStr.Split(ItemSplitChar).Select(item => item.Trim()));

                if (IncludeIOPointPrefixSpecStr.IsNeitherNullNorEmpty())
                    IncludeIOPointPrefixSet = new List<string>(IncludeIOPointPrefixSpecStr.Split(ItemSplitChar).Select(item => item.Trim()));

                if (IncludeIOPointContainsSpecStr.IsNeitherNullNorEmpty())
                    IncludeIOPointContainsSet = new List<string>(IncludeIOPointContainsSpecStr.Split(ItemSplitChar).Select(item => item.Trim()));

                if (ExcludeIOPointContainsSpecStr.IsNeitherNullNorEmpty())
                    ExcludeIOPointContainsSet = new List<string>(ExcludeIOPointContainsSpecStr.Split(ItemSplitChar).Select(item => item.Trim()));

                if (IncludeOccurrenceSpecStr.IsNeitherNullNorEmpty())
                    IncludeOccurrenceSet = new HashSet<string>(IncludeOccurrenceSpecStr.MapNullToEmpty().Split(ItemSplitChar).Select(item => item.Trim()));

                if (MapSpecStr.IsNeitherNullNorEmpty())
                {
                    var matchTokens = MapSpecStr.Split(ItemSplitChar);
                    var matchTuples = matchTokens.Select(token => { var pairArray = token.Trim().Split(new[] { ToSplitChar }, 2); return Tuple.Create(pairArray.SafeAccess(0), pairArray.SafeAccess(1)); }).ToArray();

                    MapNameFromToList = new Modular.Common.MapNameFromToList();
                    MapNameFromToList.AddRange(matchTuples.Select(t => new MapNamePrefixFromTo(t.Item1, t.Item2)));
                }

                return this;
            }

            public string GetMappedName(IGroupInfo groupInfo)
            {
                return Map(groupInfo.Name, TokenType.GroupName);
            }

            public string GetMappedName(IOccurrenceInfo occurrenceInfo)
            {
                return Map(occurrenceInfo.Name, TokenType.OccurrenceName);
            }

            public string GetMappedName(IGroupPointInfo groupPointInfo, IGroupInfo groupInfo)
            {
                string combinedName = "{0}.{1}".CheckedFormat(groupInfo.Name, groupPointInfo.Name);

                bool includeName = (IncludeIOPointSet == null && IncludeIOPointPrefixSet == null && (ExcludeIOPointContainsSet != null || IncludeIOPointContainsSet == null));

                if (IncludeIOPointSet != null)
                {
                    if (IncludeIOPointSet.Contains(groupPointInfo.Name) || IncludeIOPointSet.Contains(combinedName))
                        includeName = true;
                }

                if (ExcludeIOPointContainsSet != null)
                {
                    if (ExcludeIOPointContainsSet.Any(testContains => groupPointInfo.Name.Contains(testContains)))
                        includeName = false;
                }

                if (IncludeIOPointPrefixSet != null)
                {
                    if (IncludeIOPointPrefixSet.Any(testPrefix => groupPointInfo.Name.StartsWith(testPrefix)))
                        includeName = true;
                }

                if (IncludeIOPointContainsSet != null)
                {
                    if (IncludeIOPointContainsSet.Any(testContains => groupPointInfo.Name.Contains(testContains)))
                        includeName = true;
                }

                if (!includeName)
                    return string.Empty;

                if (MapNameFromToList != null)
                {
                    string mappedName = null;

                    if (MapNameFromToList.Map(combinedName, ref mappedName))
                        return mappedName;
                    else if (MapNameFromToList.Map(groupPointInfo.Name, ref mappedName))
                        return mappedName;
                }

                return groupPointInfo.Name;
            }

            public string Map(string givenToken, TokenType tokenType)
            {
                HashSet<string> tokenTypeIncludeSet = null;
                switch (tokenType)
                {
                    case TokenType.GroupName: tokenTypeIncludeSet = IncludeGroupSet; break;
                    case TokenType.IOPointName: tokenTypeIncludeSet = IncludeIOPointSet; break;
                    case TokenType.OccurrenceName: tokenTypeIncludeSet = IncludeOccurrenceSet; break;
                    default: break;
                }

                if (givenToken.IsNullOrEmpty() || (tokenTypeIncludeSet != null && !tokenTypeIncludeSet.Contains(givenToken)))
                    return string.Empty;

                var outputToken = givenToken;
                if (MapNameFromToList != null && MapNameFromToList.Map(givenToken, ref outputToken))
                    return outputToken;
                else
                    return givenToken;
            }
        }

        public enum TokenType
        {
            None,
            GroupName,
            IOPointName,
            OccurrenceName,
        }

        private static string appName = "AppNameNotFound";
        private static System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();

        public class ErrorMessageException : System.Exception
        {
            public ErrorMessageException(string message, System.Exception innerException = null)
                : base(message, innerException)
            { }
        }

        public class ExitException : System.Exception { }

        static void Main(string[] args)
        {
            IDataWriter dataWriter = null;
            string [] entryArgs = args;

            try
            {
                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                appName = System.IO.Path.GetFileName(currentProcess.MainModule.ModuleName);

                if (args.SafeLength() <= 0)
                {
                    WriteUsage();
                    throw new ExitException();
                }

                Config.AddStandardProviders(ref args, StandardProviderSelect.All);
                Logging.AddLogMessageHandlerToDefaultDistributionGroup(Logging.CreateConsoleLogMessageHandler(logGate: Logging.LogGate.Signif));

                List<string> argList = new List<string>().SafeAddSet(args);

                var iniFileNameArray = argList.FilterAndRemove(arg => arg.EndsWith(".ini", StringComparison.InvariantCultureIgnoreCase)).ToArray();
                if (iniFileNameArray.IsNullOrEmpty())
                {
                    Console.WriteLine("NOTE: operating in direct configruation mode.  no configuration .ini file was specified on the command line.  (See ExtractMDRFtoDB ReadMe.txt for details)");
                    Console.WriteLine();
                }
                else if (iniFileNameArray.Length > 1)
                {
                    Console.WriteLine("NOTE: more than one configuration .ini file was specified on the command line.  This program will only use the first such file.  (See ExtractMDRFtoDB ReadMe.txt for details)");
                    Console.WriteLine();
                }

                Settings settings = new Settings().UpdateFromConfig(Config.Instance, setupForUse: false);
                bool havePerformedCDalready = false;

                foreach (var iniFileName in iniFileNameArray.Take(1))
                {
                    var localIConfig = new MosaicLib.Modular.Config.ConfigBase("LocalConfig_{0}".CheckedFormat(System.IO.Path.GetFileNameWithoutExtension(iniFileName)));
                    localIConfig.AddProvider(new MosaicLib.Modular.Config.IniFileConfigKeyProvider("IniFileCKP", iniFileName));

                    settings.UpdateFromConfig(localIConfig, setupForUse: false);

                    if (settings.MDRFFileSpec.IsNeitherNullNorEmpty())
                    {
                        argList.Add(settings.MDRFFileSpec);
                        settings.MDRFFileSpec = null;
                    }

                    if (settings.AutoCDToIniFileDirectory && !havePerformedCDalready)
                    {
                        var configFileFullName = System.IO.Path.GetFullPath(iniFileName);
                        var configFileDir = System.IO.Path.GetDirectoryName(configFileFullName);

                        System.IO.Directory.SetCurrentDirectory(configFileDir);
                    }
                }

                settings.SetupForUse();

                Console.WriteLine(currentExecAssy.GetSummaryNameAndVersion());
                Console.WriteLine();

                if (settings.DataFileName.IsNullOrEmpty() && settings.DataFileType != DataFileType.None)
                {
                    WriteUsage("Given {0} DataFileName must be a non-empty string".CheckedFormat(settings.DataFileType));
                    throw new ExitException();
                }

                switch (settings.DataFileType)
                {
                    case DataFileType.SQLite3: 
                    case DataFileType.SQLite:
                        dataWriter = new SQLite3DataWriter(settings); 
                        break;

                    case DataFileType.CSV:
                        dataWriter = new CSVDataWriter(settings);
                        break;

                    default: 
                        break;
                }

                if (dataWriter == null)
                {
                    WriteUsage("Given DataFileSpec type:{0} '{1}' is not valid [type not recognized]".CheckedFormat(settings.DataFileType, settings.DataFileName));
                    throw new ExitException();
                }

                dataWriter.WriteMessage("{0} being run with arguments: {1}".CheckedFormat(currentExecAssy.GetSummaryNameAndVersion(), string.Join(" ", entryArgs)), writeToConsole: false);
                dataWriter.WriteMessage(" and current directory:{0}".CheckedFormat(System.IO.Directory.GetCurrentDirectory()), writeToConsole: false);

                var mdrfFileNames = GetFileNamesFromArgs(argList.ToArray());

                if (mdrfFileNames.IsNullOrEmpty())
                {
                    WriteUsage("No MDRF files were specified on the command line or in any configuration ini file or other configuration source.\r    At least one MDRF file name or search pattern must be specified for normal operation.");
                    throw new ExitException();
                }

                QpcTimeStamp startTime = QpcTimeStamp.Now;
                AtomicUInt64 totalBytes = new AtomicUInt64(0);

                int maxThreadsToUse = Math.Max(1, settings.MaxThreadsToUse.MapDefaultTo(System.Environment.ProcessorCount));

                Console.WriteLine("Starting processing of {0} files on <= {1} threads".CheckedFormat(mdrfFileNames.Length, maxThreadsToUse));

                DateTime dateTimeNow = DateTime.Now;

                var initialWorkItemsArray = mdrfFileNames.Select(mdrfFileName => new PendingWorkItem() { mdrfFileName = mdrfFileName }).ToArray();

                Parallel.ForEach(initialWorkItemsArray, new ParallelOptions() { MaxDegreeOfParallelism = maxThreadsToUse },
                    item =>
                    {
                        QpcTimeStamp lapTime0 = QpcTimeStamp.Now;

                        var mdrfFileName = item.mdrfFileName;
                        var mdrfReader = new MDRFFileReader(mdrfFileName, mdrfFileReaderBehavior: MDRFFileReaderBehavior.AttemptToDecodeOccurrenceBodyAsNVS);
                        item.mdrfFileReader = mdrfReader;
                        var fileKBytes = mdrfReader.FileLength * (1.0 / 1024.0);

                        QpcTimeStamp lapTime1 = QpcTimeStamp.Now;
                        double elapsed1 = (lapTime1 - lapTime0).TotalSeconds;

                        Console.WriteLine("Loaded index for {0}: Size:{1:f3}k StartTime:{2} Length:{3:f3} hours [took:{4:f3} sec]".CheckedFormat(mdrfFileName, fileKBytes, mdrfReader.DateTimeInfo.UTCDateTime.ToLocalTime(), mdrfReader.FileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp.FromSeconds().TotalHours, elapsed1));
                    });

                var sortedWorkItemsArray = initialWorkItemsArray.OrderBy(item => item.mdrfFileReader.DateTimeInfo.UTCDateTime).ToArray();

                for (; ; )
                {
                    int numWritten = sortedWorkItemsArray.Count(item => item.written);

                    if (numWritten >= sortedWorkItemsArray.Length)
                        break;

                    int numReadOrWritePending = sortedWorkItemsArray.Count(item => item.readPosted && !item.written);
                    var nextToRead = sortedWorkItemsArray.FirstOrDefault(item => !item.readPosted);

                    if (nextToRead != null && numReadOrWritePending < maxThreadsToUse)
                    {
                        nextToRead.readPosted = true;

                        System.Threading.ThreadPool.QueueUserWorkItem((ignoreMe) =>
                            {
                                var mdrfFileName = nextToRead.mdrfFileName;
                                var mdrfReader = nextToRead.mdrfFileReader;
                                var fileKBytes = mdrfReader.FileLength * (1.0 / 1024.0);

                                QpcTimeStamp lapTime1 = QpcTimeStamp.Now;

                                var contents = ReadContents(dateTimeNow, settings, mdrfReader);

                                if (contents != null)
                                {
                                    QpcTimeStamp lapTime2 = QpcTimeStamp.Now;
                                    double elapsed2 = (lapTime2 - lapTime1).TotalSeconds;

                                    Console.WriteLine("Loaded contents for {0}: Size:{1:f3}k [took:{2:f3} sec, avgRate:{3:f3} k/s]".CheckedFormat(mdrfFileName, fileKBytes, elapsed2, fileKBytes * (elapsed2.SafeOneOver())));

                                    nextToRead.mdrfContent = contents;
                                }
                                else
                                {
                                    Console.WriteLine("No contents processed for {0}".CheckedFormat(mdrfFileName));
                                    nextToRead.written = true;
                                }
                            });
                    }

                    var nextToWrite = sortedWorkItemsArray.FirstOrDefault(item => !item.written && item.mdrfContent != null);

                    if (nextToWrite != null)
                    {
                        var mdrfFileName = nextToWrite.mdrfFileName;
                        var mdrfReader = nextToWrite.mdrfFileReader;
                        var contents = nextToWrite.mdrfContent;
                        var fileKBytes = mdrfReader.FileLength * (1.0 / 1024.0);
                        QpcTimeStamp lapTime2 = QpcTimeStamp.Now;

                        dataWriter.WriteContent(contents);
                        nextToWrite.written = true;
                        nextToWrite.mdrfContent = null; // allow GC to reclaim this space now.

                        QpcTimeStamp lapTime3 = QpcTimeStamp.Now;
                        double elapsed3 = (lapTime3 - lapTime2).TotalSeconds;

                        Console.WriteLine("Saved contents for {0}: Size:{1:f3}k [took:{2:f3} sec, avgRate:{3:f3} k/s]".CheckedFormat(mdrfFileName, fileKBytes, elapsed3, fileKBytes * (elapsed3.SafeOneOver())));

                        totalBytes.Add((ulong)mdrfReader.FileLength);
                    }

                    // prevent this method from spinning overly fast.
                    (0.1).FromSeconds().Sleep();
                }

                Fcns.DisposeOfObject(ref dataWriter);

                var totalElapsedTime = startTime.Age.TotalSeconds;
                var totalKBytes = totalBytes.VolatileValue * (1.0 / 1024.0);

                Console.WriteLine("Completed processing of {0} files: {1:f3}k took {2:f3} seconds.  AvgRate:{3:f3} k/s".CheckedFormat(mdrfFileNames.Length, totalKBytes, totalElapsedTime, totalKBytes * (totalElapsedTime.SafeOneOver())));
            }
            catch (ExitException)
            { }
            catch (ErrorMessageException ex)
            {
                if (ex.InnerException == null)
                    Console.WriteLine("{0}: failed with error: {1}".CheckedFormat(appName, ex.Message));
                else
                    Console.WriteLine("{0}: failed with error: {1} [inner:{2}]".CheckedFormat(appName, ex.Message, ex.InnerException.ToString(ExceptionFormat.TypeAndMessage)));
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("{0}: failed with exception: {1}".CheckedFormat(appName, ex.ToString(ExceptionFormat.AllButStackTrace)));
            }
            finally
            {
                try
                {
                    Fcns.DisposeOfObject(ref dataWriter);
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("{0}: during final disposal of data writer: failed with exception: {1}".CheckedFormat(appName, ex.ToString(ExceptionFormat.AllButStackTrace)));
                }
            }

            if (System.Diagnostics.Debugger.IsAttached)
                (3.0).FromSeconds().Sleep();
        }

        private class PendingWorkItem
        {
            public string mdrfFileName;
            public MDRFFileReader mdrfFileReader;
            public bool readPosted;
            public volatile MDRFContent mdrfContent;
            public volatile bool written;
        }

        private static void WriteUsage(string errorMesg = null)
        {
            if (errorMesg.IsNeitherNullNorEmpty())
            {
                Console.WriteLine("Error: {0}", errorMesg);
                Console.WriteLine();
            }

            Console.WriteLine("Usage: {0} [configFileName.ini] [fileName.mdrf] ...".CheckedFormat(appName));
            Console.WriteLine("    This program generally reads the ini file contents to configure its operation and then processes all of the indicated mdrf files using that selected configuration.");
            Console.WriteLine("    See 'ExtractMDRFtoDB ReadMe.txt' for more details on the operation and use of this program.  This file is generally provided with the executable.");
            Console.WriteLine();

            Console.WriteLine("Assembly: {0}", currentExecAssy.GetSummaryNameAndVersion());
        }

        private static string [] GetFileNamesFromArgs(string [] args)
        {
            List<string> fileNameList = new List<string>();

            foreach (var argIter in args)
            {
                var arg = argIter;
                var argExt = System.IO.Path.GetExtension(arg);

                if (argExt.IsNullOrEmpty())
                    arg = arg.AddSuffixIfNeeded(".mdrf");

                if (!arg.EndsWith(".mdrf", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Do not know how to process '{0}'".CheckedFormat(arg));
                    continue;
                }

                string filePart = System.IO.Path.GetFileName(arg);
                string pathPart = System.IO.Path.GetDirectoryName(arg).MapNullOrEmptyTo(".");
                string fullPathPart = System.IO.Path.GetFullPath(pathPart);

                if (filePart.Contains("?") || filePart.Contains("*"))
                {
                    foreach (string fpath in System.IO.Directory.EnumerateFiles(pathPart, filePart, System.IO.SearchOption.TopDirectoryOnly))
                        fileNameList.Add(fpath);
                }
                else
                    fileNameList.Add(arg);
            }

            return fileNameList.ToArray();
        }

        #region ReadContents, MDRFContent, VCEHelper, GroupDataAccumulator, OccurrenceAccumulator, MessageAccumulator, VCSetDataRow, VCDataRow, ProcessContentEventHandlerDelegate (et. al.)

        private static MDRFContent ReadContents(DateTime dateTimeNow, Settings settings, MDRFFileReader mdrfFileReader)
        {
            DateTime fileStartTime = mdrfFileReader.DateTimeInfo.UTCDateTime.ToLocalTime();
            DateTime estimatedFileEndTime = fileStartTime + mdrfFileReader.FileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp.FromSeconds();

            MDRFContent contentBuilder = new MDRFContent(mdrfFileReader, settings);

            ReadAndProcessFilterSpec filterSpec = new ReadAndProcessFilterSpec()
            {
                AutoRewindToPriorFullGroupRow = false,

                PCEMask = ProcessContentEvent.NewTimeStamp | ProcessContentEvent.Occurrence | ProcessContentEvent.Message | ProcessContentEvent.Error
                        | ProcessContentEvent.StartOfFullGroup | ProcessContentEvent.Group | ProcessContentEvent.PartialGroup | ProcessContentEvent.EmptyGroup,

                FileIndexUserRowFlagBitsMask = contentBuilder.FileIndexUserRowFlagBitsMaskOfInterest,
                GroupFilterDelegate = contentBuilder.GroupFilterDelegate,
                OccurrenceFilterDelegate = contentBuilder.OccurrenceFilterDelegate,
            };

            if (!settings.StartDateTime.IsZero())
            {
                filterSpec.FirstDateTime = settings.StartDateTime;
                if (estimatedFileEndTime < filterSpec.FirstDateTime - (2.0).FromHours())
                    return null;    // this file ends at least 2 hours before the filter start time - skip this file.
            }

            if (!settings.EndDateTime.IsZero())
            {
                filterSpec.LastDateTime = settings.EndDateTime;
                if (fileStartTime > filterSpec.LastDateTime + (2.0).FromSeconds())
                    return null;   // this file starts at least 2 hours after the filter end time - skip this file. 
            }

            filterSpec.EventHandlerDelegate = (sender, pceData) => ProcessContentEventHandlerDelegate(contentBuilder, pceData, settings);

            mdrfFileReader.ReadAndProcessContents(filterSpec);

            return contentBuilder;
        }

        public class MDRFContent
        {
            public MDRFContent(MDRFFileReader mdrfFileReader, Settings settings)
            {
                MDRFFileReader = mdrfFileReader;

                LibraryInfo = mdrfFileReader.LibraryInfo;
                SetupInfo = mdrfFileReader.SetupInfo;
                FileIndexInfo = mdrfFileReader.FileIndexInfo;
                DateTimeInfo = mdrfFileReader.DateTimeInfo;

                GroupDataAccumulatorArray = mdrfFileReader.GroupInfoArray.Select(groupInfo => new GroupDataAccumulator(groupInfo, vceHelper, settings)).ToArray();
                OccurrenceAccumulatorArray = mdrfFileReader.OccurrenceInfoArray.Select(occurrenceInfo => new OccurrenceAccumulator(occurrenceInfo, vceHelper, settings)).ToArray();

                GroupDataAccumulatorByFileIDDictionary = new Dictionary<int, GroupDataAccumulator>().SafeAddRange(GroupDataAccumulatorArray.Select(gda => KVP.Create(gda.GroupInfo.FileID, gda)));
                OccurrenceAccumulatorByFileIDDictionary = new Dictionary<int, OccurrenceAccumulator>().SafeAddRange(OccurrenceAccumulatorArray.Select(oa => KVP.Create(oa.OccurrenceInfo.FileID, oa)));
                
                MessageList = new List<IMessageInfo>();
                ErrorList = new List<IMessageInfo>();

                if (GroupDataAccumulatorArray.Any(gda => !gda.RecordThisGroup))
                    groupFilterSet = new HashSet<int>().SafeAddRange(GroupDataAccumulatorArray.Where(gda => gda.RecordThisGroup).Select(gda => gda.GroupInfo.GroupID));

                if (OccurrenceAccumulatorArray.Any(oa => !oa.RecordThisOccurrence))
                    occurrenceFilterSet = new HashSet<int>().SafeAddRange(OccurrenceAccumulatorArray.Where(oa => oa.RecordThisOccurrence).Select(oa => oa.OccurrenceInfo.OccurrenceID));
            }

            private HashSet<int> groupFilterSet = null;
            private HashSet<int> occurrenceFilterSet = null;

            public Func<IGroupInfo, bool> GroupFilterDelegate
            {
                get
                {
                    if (groupFilterSet == null)
                        return null;
                    else
                        return (igi => groupFilterSet.Contains(igi.GroupID));
                }
            }

            public Func<IOccurrenceInfo, bool> OccurrenceFilterDelegate
            {
                get
                {
                    if (occurrenceFilterSet == null)
                        return null;
                    else
                        return (ioi => occurrenceFilterSet.Contains(ioi.OccurrenceID));
                }
            }

            public ulong FileIndexUserRowFlagBitsMaskOfInterest
            {
                get
                {
                    ulong userFlagBitsOfInterest = GroupDataAccumulatorArray.Where(gda => gda.RecordThisGroup).Aggregate<GroupDataAccumulator, ulong>(0, (bitsIn, gda) => (bitsIn | gda.GroupInfo.FileIndexUserRowFlagBits))
                                                 | OccurrenceAccumulatorArray.Where(oa => oa.RecordThisOccurrence).Aggregate<OccurrenceAccumulator, ulong>(0, (bitsIn, oa) => (bitsIn | oa.OccurrenceInfo.FileIndexUserRowFlagBits));

                    return userFlagBitsOfInterest;
                }
            }

            public MDRFFileReader MDRFFileReader { get; private set; }
            public LibraryInfo LibraryInfo { get; private set; }
            public SetupInfo SetupInfo { get; private set; }
            public FileIndexInfo FileIndexInfo { get; private set; }
            public DateTimeInfo DateTimeInfo { get; private set; }

            public GroupDataAccumulator[] GroupDataAccumulatorArray { get; private set; }
            public OccurrenceAccumulator[] OccurrenceAccumulatorArray { get; private set; }

            public IDictionary<int, GroupDataAccumulator> GroupDataAccumulatorByFileIDDictionary { get; private set; }
            public IDictionary<int, OccurrenceAccumulator> OccurrenceAccumulatorByFileIDDictionary { get; private set; }

            public List<IMessageInfo> MessageList { get; private set; }
            public List<IMessageInfo> ErrorList { get; private set; }

            VCEHelper vceHelper = new VCEHelper();
        }

        public class VCEHelper
        {
            public ValueContainerEnvelope vce = new ValueContainerEnvelope();
            public IDataContractAdapter<ValueContainerEnvelope> vceDCA = new DataContractJsonAdapter<ValueContainerEnvelope>();

            public ValueContainer Normalize(ValueContainer vc)
            {
                switch (vc.cvt)
                {
                    case ContainerStorageType.INamedValueSet: 
                    case ContainerStorageType.INamedValue:
                    case ContainerStorageType.IListOfString:
                    case ContainerStorageType.IListOfVC:
                    case ContainerStorageType.Object:
                    case ContainerStorageType.Custom:
                        return ValueContainer.Create(vc.ConvertToRawJSON());
                    default:
                        return vc;
                }
            }
        }

        public class GroupDataAccumulator
        {
            public GroupDataAccumulator(IGroupInfo groupInfo, VCEHelper vceHelper, Settings settings)
            {
                MappedGroupName = settings.GetMappedName(groupInfo);

                TableName = MappedGroupName.IsNeitherNullNorEmpty() ? MappedGroupName : string.Empty;
                GroupInfo = groupInfo;
                VCEHelper = vceHelper;
                MappedGroupPointInfoArray = groupInfo.GroupPointInfoArray.Select(gpi => new MappedGroupPointInfo(gpi, groupInfo, settings)).ToArray();
                NumPoints = MappedGroupPointInfoArray.Length;

                RecordThisGroup = MappedGroupName.IsNeitherNullNorEmpty() && !MappedGroupName.Contains("'") && MappedGroupPointInfoArray.Any(mgpi => mgpi.RecordThisPoint);

                if (settings.UsingNominalSampleInterval)
                {
                    UsingRowAddCadencingTimer = true;
                    rowAddCadencingTimer = new QpcTimer() { TriggerInterval = settings.NominalSampleInterval, AutoReset = true }.Reset(QpcTimeStamp.Zero, triggerImmediately: true);
                }
            }

            public string MappedGroupName { get; private set; }
            public bool RecordThisGroup { get; private set; }
            public string TableName { get; private set; }
            public IGroupInfo GroupInfo { get; private set; }
            public VCEHelper VCEHelper { get; private set; }
            public MappedGroupPointInfo[] MappedGroupPointInfoArray { get; private set; }
            public int NumPoints { get; private set; }
            public bool FindFirstNonEmptyDone { get; private set; }
            public List<VCSetDataRow> dataRowList = new List<VCSetDataRow>();
            public bool UsingRowAddCadencingTimer { get; private set; }
            public QpcTimer rowAddCadencingTimer;

            public class MappedGroupPointInfo
            {
                public MappedGroupPointInfo(IGroupPointInfo groupPointInfo, IGroupInfo groupInfo, Settings settings)
                {
                    MappedPointName = settings.GetMappedName(groupPointInfo, groupInfo);
                    RecordThisPoint = MappedPointName.IsNeitherNullNorEmpty() && !MappedPointName.Contains("'");
                    GroupPointInfo = groupPointInfo;
                }

                public string MappedPointName { get; private set; }
                public bool RecordThisPoint { get; private set; }
                public IGroupPointInfo GroupPointInfo { get; private set; }
                public ContainerStorageType ExtractedCST { get; set; }
            }

            public void Add(ProcessContentEventData pceData)
            {
                bool includeThisRow = (!UsingRowAddCadencingTimer || rowAddCadencingTimer.GetIsTriggered(new QpcTimeStamp(pceData.FileDeltaTimeStamp)));

                if (includeThisRow)
                {
                    var vcSetDataRow = new VCSetDataRow(pceData, MappedGroupPointInfoArray, VCEHelper);
                    dataRowList.Add(vcSetDataRow);

                    if (!FindFirstNonEmptyDone)
                    {
                        bool haveNone = false;

                        for (int idx = 0; idx < NumPoints; idx++)
                        {
                            var mgpi = MappedGroupPointInfoArray[idx];
                            if (mgpi.ExtractedCST == ContainerStorageType.None && mgpi.RecordThisPoint)
                            {
                                var vc = vcSetDataRow.VCArray[idx];
                                var cst = vc.cvt;

                                if (cst == ContainerStorageType.None || (cst == ContainerStorageType.Object && vc.o == null))
                                    haveNone = true;
                                else
                                    mgpi.ExtractedCST = cst;
                            }
                        }

                        FindFirstNonEmptyDone = !haveNone;
                    }
                }
            }
        }

        public class OccurrenceAccumulator
        {
            public OccurrenceAccumulator(IOccurrenceInfo occurrenceInfo, VCEHelper vceHelper, Settings settings)
            {
                MappedOccurrenceName = settings.GetMappedName(occurrenceInfo);
                RecordThisOccurrence = MappedOccurrenceName.IsNeitherNullNorEmpty() && !MappedOccurrenceName.Contains("'");
                OccurrenceInfo = occurrenceInfo;
                VCEHelper = vceHelper;
            }

            public string MappedOccurrenceName { get; private set; }
            public bool RecordThisOccurrence { get; private set; }
            public IOccurrenceInfo OccurrenceInfo { get; private set; }
            public VCEHelper VCEHelper { get; private set; }

            public List<VCDataRow> vcRowList = new List<VCDataRow>();

            public void Add(ProcessContentEventData pceData)
            {
                vcRowList.Add(new VCDataRow(pceData, VCEHelper));
            }
        }

        public class MessageAccumulator
        {
            public IMessageInfo MessageInfo { get; private set; }

            public List<VCDataRow> vcRowList = new List<VCDataRow>();
        }

        public class VCSetDataRow
        {
            public VCSetDataRow(ProcessContentEventData pceData, GroupDataAccumulator.MappedGroupPointInfo [] mappedGroupPointInfoArray, VCEHelper vceHelper)
            {
                SeqNum = pceData.SeqNum;
                UTCDateTime = pceData.UTCDateTime;
                FileDeltaTimeStamp = pceData.FileDeltaTimeStamp;
                PCE = pceData.PCE;

                VCArray = new ValueContainer[mappedGroupPointInfoArray.Length];
                VCArray.SafeCopyFrom(mappedGroupPointInfoArray.Select(mgpi => vceHelper.Normalize(mgpi.RecordThisPoint ? mgpi.GroupPointInfo.VC : ValueContainer.Empty)));
            }

            public UInt64 SeqNum { get; set; }
            /// <summary>This DateTime is generated from the FileDeltaTimeStamp (which is recorded) by inference using the DateTime values recorded in the current row, and the next row (if it is present).  Due to the relative sample timing and the use of interpolation, individual DateTime values may not be strictly monotonic in relation to the underlying delta timestamp values.</summary>
            public DateTime UTCDateTime { get; set; }
            public double FileDeltaTimeStamp { get; set; }
            public ProcessContentEvent PCE { get; set; }

            public ValueContainer[] VCArray { get; set; }
        }

        public class VCDataRow
        {
            public VCDataRow(ProcessContentEventData pceData, VCEHelper vceHelper)
            {
                SeqNum = pceData.SeqNum;
                UTCDateTime = pceData.UTCDateTime;
                FileDeltaTimeStamp = pceData.FileDeltaTimeStamp;
                PCE = pceData.PCE;

                VC = pceData.VC;
                NormalizedVC = vceHelper.Normalize(pceData.VC);
            }

            public UInt64 SeqNum { get; set; }
            /// <summary>This DateTime is generated from the FileDeltaTimeStamp (which is recorded) by inference using the DateTime values recorded in the current row, and the next row (if it is present).  Due to the relative sample timing and the use of interpolation, individual DateTime values may not be strictly monotonic in relation to the underlying delta timestamp values.</summary>
            public DateTime UTCDateTime { get; set; }
            public double FileDeltaTimeStamp { get; set; }
            public ProcessContentEvent PCE { get; set; }

            public ValueContainer VC { get; set; }
            public ValueContainer NormalizedVC { get; set; }
        }

        private static void ProcessContentEventHandlerDelegate(MDRFContent contentBuilder, ProcessContentEventData pceData, Settings settings)
        {
            switch (pceData.PCE & ~(ProcessContentEvent.EmptyGroup | ProcessContentEvent.PartialGroup | ProcessContentEvent.StartOfFullGroup))
            {
                case ProcessContentEvent.Group:
                    var groupAccumulator = contentBuilder.GroupDataAccumulatorByFileIDDictionary.SafeTryGetValue(pceData.GroupInfo.FileID);
                    if (groupAccumulator != null)
                        groupAccumulator.Add(pceData);
                    break;

                case ProcessContentEvent.Occurrence:
                    var occurrenceAccumulator = contentBuilder.OccurrenceAccumulatorByFileIDDictionary.SafeTryGetValue(pceData.OccurrenceInfo.FileID);
                    if (occurrenceAccumulator != null)
                        occurrenceAccumulator.Add(pceData);
                    break;

                case ProcessContentEvent.Message:
                    contentBuilder.MessageList.Add(pceData.MessageInfo);
                    break;

                case ProcessContentEvent.Error:
                    contentBuilder.ErrorList.Add(pceData.MessageInfo);
                    break;
            }
        }

        #endregion

        public interface IDataWriter : IDisposable
        {
            void WriteContent(MDRFContent mdrfContent);

            void WriteMessage(string mesg, bool writeToConsole = true, Logging.IMesgEmitter emitter = null);
            void WriteError(string errorMesg, bool writeToConsole = true, Logging.IMesgEmitter emitter = null);
        }

        public class SQLite3DataWriter : DisposableBase, IDataWriter
        {
            public SQLite3DataWriter(Settings settings)
            {
                Settings = settings;

                if (settings.DataFileType != DataFileType.SQLite3)
                    throw new System.ArgumentException("{0} cannot be used with DataFileType.{1}".CheckedFormat(Fcns.CurrentClassLeafName, settings.DataFileType));

                if (settings.CreateMode == CreateMode.Truncate && System.IO.File.Exists(settings.DataFileName))
                {
                    Console.WriteLine("Deleting existing file '{0}' before extraction", settings.DataFileName);

                    System.IO.File.Delete(settings.DataFileName);

                    var dbExt = System.IO.Path.GetExtension(settings.DataFileName);
                    var dbFileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(settings.DataFileName);

                    foreach (var testAdditions in new string[] { "-wal", "-journal", "-shm" })
                    {
                        var alternateFile = System.IO.Path.ChangeExtension(dbFileNameNoExt + testAdditions, dbExt);

                        if (System.IO.File.Exists(alternateFile))
                        {
                            Console.WriteLine("Deleting supporting file '{0}' before extraction", alternateFile);
                            System.IO.File.Delete(alternateFile);
                        }
                    }
                }

                var connStringBuilder = new SQLiteConnectionStringBuilder()
                {
                    DataSource = settings.DataFileName,
                    Version = 3,
                    JournalMode = SQLiteJournalModeEnum.Off,
                    SyncMode = SynchronizationModes.Off,
                    DateTimeFormat = SQLiteDateFormats.JulianDay,
                    DateTimeKind = DateTimeKind.Local,      // we may want to change this (and all related code) to UTC
                    //PageSize = 65536,     // defaults to 4096
                };

                dbConn = new SQLiteConnection(connStringBuilder.ToString());

                AddExplicitDisposeAction(() => { Fcns.DisposeOfObject(ref dbConn); });

                dbConn.Open();
                new[] 
                {
                    "CREATE TABLE IF NOT EXISTS MDRF_Files (Name TEXT, SizeBytes INT NOT NULL, StartDateTime TIMESTAMP NOT NULL, LastBlockDateTime TIMESTAMP NOT NULL, WasProperlyClosed INT NOT NULL, FileInfo TEXT, DateTimeInfo TEXT, OccurrencesInfo TEXT, GroupsInfo TEXT)",
                    "CREATE TABLE IF NOT EXISTS MDRF_Messages (DateTime TIMESTAMP NOT NULL, Text TEXT)",
                    "CREATE TABLE IF NOT EXISTS MDRF_Errors (DateTime TIMESTAMP NOT NULL, Text TEXT)",
                    "CREATE TABLE IF NOT EXISTS MDRF_Occurrences (DateTime TIMESTAMP NOT NULL, Name TEXT, Value TEXT)",
                    "CREATE INDEX IF NOT EXISTS MDRF_Message_DateTime_Index ON MDRF_Messages (DateTime)",
                    "CREATE INDEX IF NOT EXISTS MDRF_Errors_DateTime_Index ON MDRF_Errors (DateTime)",
                    "CREATE INDEX IF NOT EXISTS MDRF_Occurrences_DateTime_Index ON MDRF_Occurrences (DateTime)",
                    "CREATE INDEX IF NOT EXISTS MDRF_Occurrences_Name_Index ON MDRF_Occurrences (Name)",
                    "CREATE INDEX IF NOT EXISTS MDRF_Occurrences_DateTime__Name_Index ON MDRF_Occurrences (DateTime, Name)",
                }.DoForEach(cmd => new SQLiteCommand(dbConn) { CommandText = cmd }.ExecuteNonQuery());

                var getTablesCmd = new SQLiteCommand(dbConn) { CommandText = "SELECT * FROM sqlite_master WHERE type='table'" };
                var getTablesResultsReader = getTablesCmd.ExecuteReader();
                var schemaTable = getTablesResultsReader.GetSchemaTable();
                var getTablesColumns = getTablesResultsReader.GetFieldNames().ToArray();
                var getTablesResults = getTablesResultsReader.GenerateValuesEnumerator().ToArray();

                var tableNames = getTablesResults.Select(vcArray => vcArray.FirstOrDefault().GetValue<string>(rethrow: false)).ToArray();
            }

            Settings Settings { get; set; }

            public void WriteContent(MDRFContent mdrfContent)
            {
                DateTime fileBaseTime = mdrfContent.MDRFFileReader.DateTimeInfo.UTCDateTime.ToLocalTime();

                lock (mutex)
                {
                    // Add file to file table
                    AddMDRFFileToTable(mdrfContent, fileBaseTime);

                    // add any messages
                    if (mdrfContent.MessageList.Count > 0)
                        AddMessages(mdrfContent.MessageList, fileBaseTime);

                    // add any errors
                    if (mdrfContent.ErrorList.Count > 0)
                        AddErrors(mdrfContent.ErrorList, fileBaseTime);

                    AddAccumulatedOccurrences(mdrfContent.OccurrenceAccumulatorArray.Where(oa => oa.RecordThisOccurrence), fileBaseTime);

                    // for each accumulated group - add the data to the corresponding table
                    foreach (var groupDataAccumulator in mdrfContent.GroupDataAccumulatorArray.Where(gda => gda.RecordThisGroup))
                        AddAccumulatedGroupData(mdrfContent, groupDataAccumulator, fileBaseTime);
                }
            }

            public void WriteMessage(string mesg, bool writeToConsole = true, Logging.IMesgEmitter emitter = null)
            {
                DateTime dtNow = DateTime.Now.ToUniversalTime();

                IMessageInfo iMesgInfo = new MessageInfo()
                {
                    Message = mesg,
                    FileDeltaTimeStamp = 0.0,
                };

                AddMessages(new[] { iMesgInfo }.ToList(), dtNow);

                if (writeToConsole)
                    Console.WriteLine(mesg);

                if (emitter != null && emitter.IsEnabled)
                    emitter.Emit(mesg);
            }

            public void WriteError(string errorMesg, bool writeToConsole = true, Logging.IMesgEmitter emitter = null)
            {
                DateTime dtNow = DateTime.Now.ToUniversalTime();

                IMessageInfo iErrorMesgInfo = new MessageInfo()
                {
                    Message = errorMesg,
                    FileDeltaTimeStamp = 0.0,
                };

                AddErrors(new[] { iErrorMesgInfo }.ToList(), dtNow);

                if (writeToConsole)
                    Console.WriteLine(errorMesg);

                if (emitter != null && emitter.IsEnabled)
                    emitter.Emit(errorMesg);
            }

            public void AddMDRFFileToTable(MDRFContent mdrfContent, DateTime fileBaseTime)
            {
                lock (mutex)
                {
                    var insertFileCmd = new SQLiteCommand(dbConn) { CommandText = "INSERT INTO MDRF_Files (Name, SizeBytes, StartDateTime, LastBlockDateTime, WasProperlyClosed, FileInfo, DateTimeInfo, OccurrencesInfo, GroupsInfo) VALUES (@name, @sizeBytes, @startDateTime, @lastBlockDateTime, @wasProperlyClosed, @fileInfo, @dateTimeInfo, @occurrencesInfo, @groupsInfo)" };

                    insertFileCmd.Parameters.AddWithValue("name", mdrfContent.MDRFFileReader.FilePath);
                    insertFileCmd.Parameters.AddWithValue("sizeBytes", mdrfContent.MDRFFileReader.FileLength);
                    insertFileCmd.Parameters.AddWithValue("startDateTime", fileBaseTime);
                    insertFileCmd.Parameters.AddWithValue("lastBlockDateTime", fileBaseTime + mdrfContent.FileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp.FromSeconds());
                    insertFileCmd.Parameters.AddWithValue("wasProperlyClosed", mdrfContent.MDRFFileReader.FileIndexInfo.FileWasProperlyClosed);
                    insertFileCmd.Parameters.AddWithValue("fileInfo", ValueContainer.Create(mdrfContent.MDRFFileReader.LibraryInfo.NVS).ConvertToRawJSON());
                    insertFileCmd.Parameters.AddWithValue("dateTimeInfo", ValueContainer.Create(mdrfContent.MDRFFileReader.DateTimeInfo.NVS).ConvertToRawJSON());
                    insertFileCmd.Parameters.AddWithValue("occurrencesInfo", ValueContainer.Create(mdrfContent.MDRFFileReader.OccurrenceInfoArray.ConvertToNVS()).ConvertToRawJSON());
                    insertFileCmd.Parameters.AddWithValue("groupsInfo", ValueContainer.Create(mdrfContent.MDRFFileReader.GroupInfoArray.ConvertToNVS()).ConvertToRawJSON());

                    using (var transaction = dbConn.BeginTransaction())
                    {
                        insertFileCmd.ExecuteNonQuery();

                        transaction.Commit();
                    }
                }
            }

            public void AddMessages(List<IMessageInfo> messageList, DateTime fileBaseTime)
            {
                lock (mutex)
                {
                    var insertMesgCmd = new SQLiteCommand(dbConn) { CommandText = "INSERT INTO MDRF_Messages (DateTime, Text) VALUES (@dateTime, @text)" };
                    var dateTimeParam = new SQLiteParameter("dateTime");
                    var textParam = new SQLiteParameter("text");
                    insertMesgCmd.Parameters.AddRange(new[] { dateTimeParam, textParam });

                    using (var transaction = dbConn.BeginTransaction())
                    {
                        foreach (var mesg in messageList)
                        {
                            dateTimeParam.Value = fileBaseTime + mesg.FileDeltaTimeStamp.FromSeconds();
                            textParam.Value = mesg.Message;

                            insertMesgCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }

            public void AddErrors(List<IMessageInfo> errorList, DateTime fileBaseTime)
            {
                lock (mutex)
                {
                    var insertMesgCmd = new SQLiteCommand(dbConn) { CommandText = "INSERT INTO MDRF_Errors (DateTime, Text) VALUES (@dateTime, @text)" };
                    var dateTimeParam = new SQLiteParameter("dateTime");
                    var textParam = new SQLiteParameter("text");
                    insertMesgCmd.Parameters.AddRange(new[] { dateTimeParam, textParam });

                    using (var transaction = dbConn.BeginTransaction())
                    {
                        foreach (var mesg in errorList)
                        {
                            dateTimeParam.Value = fileBaseTime + mesg.FileDeltaTimeStamp.FromSeconds();
                            textParam.Value = mesg.Message;

                            insertMesgCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }

            private void AddAccumulatedOccurrences(IEnumerable<OccurrenceAccumulator> occurrenceAccumulatorSet, DateTime fileBaseTime)
            {
                VCEHelper vceHelper = new VCEHelper();
                var flattenedSortedArray = occurrenceAccumulatorSet.SelectMany(accum => accum.vcRowList.Select(vcRow => Tuple.Create(accum.OccurrenceInfo.Name, fileBaseTime + vcRow.FileDeltaTimeStamp.FromSeconds(), vcRow.NormalizedVC.ValueAsObject, vcRow.VC.GetValue<INamedValueSet>(rethrow: false).MapNullToEmpty().BuildDictionary()))).OrderBy(t => t.Item2).ToArray();

                Dictionary<string, string> keyColumnNameSet = new Dictionary<string, string>();

                if (Settings.AddOccurrenceKeyColumns)
                {
                    foreach (var t in flattenedSortedArray)
                        t.Item4.DoForEach(nv => keyColumnNameSet[nv.Name] = "nv.{0}".CheckedFormat(nv.Name));
                }

                lock (mutex)
                {
                    // get existing column names
                    var getTableInfoCmd = new SQLiteCommand(dbConn) { CommandText = "PRAGMA table_info(MDRF_Occurrences)" };
                    var getTableInfoReader = getTableInfoCmd.ExecuteReader();
                    var getTableInfoResults = getTableInfoReader.GenerateValuesEnumerator().ToArray();
                    var existingColumnNameSet = new HashSet<string>(getTableInfoResults.Select(vcArray => vcArray.SafeAccess(1).GetValue<string>(rethrow: false)));

                    // add missing columns as needed.
                    foreach (var columnName in keyColumnNameSet.Values)
                    {
                        if (!existingColumnNameSet.Contains(columnName))
                        {
                            try
                            {
                                var addColumnCmd = new SQLiteCommand(dbConn) { CommandText = "ALTER TABLE MDRF_Occurrences ADD COLUMN '{0}' TEXT".CheckedFormat(columnName) };

                                addColumnCmd.ExecuteNonQuery();

                                existingColumnNameSet.Add(columnName);
                            }
                            catch (SQLiteException ex)
                            {
                                // generally because we have attempted to add the same column twice.
                                WriteError("Error adding column '{0}' TEXT to table MDRF_Occurrences: {1}".CheckedFormat(columnName, ex.ToString(ExceptionFormat.TypeAndMessage)));
                            }
                        }
                    }

                    var insertCmdColumnNameArray = new string[] { "DateTime", "Name", "Value" }.Concat(keyColumnNameSet.Values).ToArray();
                    var insertCmdColumnNamesString = string.Join(", ", insertCmdColumnNameArray.Select(name => "'{0}'".CheckedFormat(name)));
                    var insertCmdQsString = string.Join(", ", insertCmdColumnNameArray.Select(columnName => "?"));
                    var insertOccurrenceCmd = new SQLiteCommand(dbConn) { CommandText = "INSERT INTO MDRF_Occurrences ({0}) VALUES ({1})".CheckedFormat(insertCmdColumnNamesString, insertCmdQsString) };

                    var dateTimeParam = new SQLiteParameter();
                    var nameParam = new SQLiteParameter();
                    var valueParam = new SQLiteParameter();
                    var keyParamDictionary = new Dictionary<string, SQLiteParameter>().SafeAddRange(keyColumnNameSet.Select(kvp => KVP.Create(kvp.Key, new SQLiteParameter())));
                    var keyParams = keyParamDictionary.Select(kvp => kvp.Value).ToArray();

                    insertOccurrenceCmd.Parameters.AddRange(new[] { dateTimeParam, nameParam, valueParam }.Concat(keyParams).ToArray());

                    var transaction = dbConn.BeginTransaction();
                    int count = 0;
                    {
                        foreach (var t in flattenedSortedArray)
                        {
                            if (++count % 2000 == 0)
                            {
                                transaction.Commit();
                                transaction = dbConn.BeginTransaction();
                            }

                            keyParams.DoForEach(param => param.Value = string.Empty);

                            nameParam.Value = t.Item1;
                            dateTimeParam.Value = t.Item2;
                            valueParam.Value = t.Item3;

                            foreach (var kvp in keyParamDictionary)
                            {
                                if (t.Item4.Contains(kvp.Key))
                                    kvp.Value.Value = vceHelper.Normalize(t.Item4[kvp.Key].VC).ValueAsObject.SafeToString();
                            }

                            insertOccurrenceCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }

            public void AddAccumulatedGroupData(MDRFContent mdrfContent, GroupDataAccumulator groupDataAccumulator, DateTime fileBaseTime)
            {
                var columnDecoderTupleArray = groupDataAccumulator.MappedGroupPointInfoArray.Select((mgpi, index) => Tuple.Create(index, mgpi.RecordThisPoint && mgpi.ExtractedCST != ContainerStorageType.None, mgpi.MappedPointName, mgpi)).ToArray();
                var filteredColumnDecoderTupleArray = columnDecoderTupleArray.Where(t => t.Item2).ToArray();

                const int includeColumnCountLimit = 995;
                if (filteredColumnDecoderTupleArray.Length > includeColumnCountLimit)
                {
                    var strippedColumnNamesArray = filteredColumnDecoderTupleArray.Skip(includeColumnCountLimit).Select(t => t.Item3).ToArray();
                    string fileName = System.IO.Path.GetFileName(mdrfContent.MDRFFileReader.FilePath);

                    WriteError("File: '{0}' Group '{1}' has more columns ({2}) than SQLite supports.  Truncating to {3}".CheckedFormat(fileName, groupDataAccumulator.TableName, filteredColumnDecoderTupleArray.Length, includeColumnCountLimit));
                    filteredColumnDecoderTupleArray = filteredColumnDecoderTupleArray.Take(includeColumnCountLimit).ToArray();

                    WriteError("The following {0} columns have been removed: {1}".CheckedFormat(strippedColumnNamesArray.Length, String.Join(",", strippedColumnNamesArray)), writeToConsole: false);
                }

                var filteredColumnNames = filteredColumnDecoderTupleArray.Select(t => t.Item3).ToArray();
                var mgpiArray = filteredColumnDecoderTupleArray.Select(t => t.Item4).ToArray();

                lock (mutex)
                {
                    // first add the table, index, and columns if needed.

                    var addGroupTableCmd = new SQLiteCommand(dbConn) { CommandText = "CREATE TABLE IF NOT EXISTS '{0}' (DateTime TIMESTAMP NOT NULL)".CheckedFormat(groupDataAccumulator.TableName) };

                    addGroupTableCmd.ExecuteNonQuery();

                    var addTableIndexCmd = new SQLiteCommand(dbConn) { CommandText = "CREATE INDEX IF NOT EXISTS '{0}_DateTime_Index' ON '{0}' (DateTime)".CheckedFormat(groupDataAccumulator.TableName) };

                    addTableIndexCmd.ExecuteNonQuery();

                    var getTableInfoCmd = new SQLiteCommand(dbConn) { CommandText = "PRAGMA table_info('{0}')".CheckedFormat(groupDataAccumulator.TableName) };
                    var getTableInfoReader = getTableInfoCmd.ExecuteReader();
                    var getTableInfoResults = getTableInfoReader.GenerateValuesEnumerator().ToArray();
                    var existingColumnNameSet = new HashSet<string>(getTableInfoResults.Select(vcArray => vcArray.SafeAccess(1).GetValue<string>(rethrow: false)));

                    foreach (var mgpi in mgpiArray)
                    {
                        var cst = mgpi.ExtractedCST;

                        if (existingColumnNameSet.Contains(mgpi.MappedPointName))
                            continue;

                        string columnType;
                        if (cst.IsInteger() || cst.IsBoolean())
                            columnType = "INT";
                        else if (cst.IsFloatingPoint())
                            columnType = "REAL";
                        else if (cst == ContainerStorageType.DateTime)
                            columnType = "TIMESTAMP";
                        else if (cst.IsString())
                            columnType = "TEXT";
                        else
                            columnType = "BLOB";

                        try
                        {
                            var addColumnCmd = new SQLiteCommand(dbConn) { CommandText = "ALTER TABLE '{0}' ADD COLUMN '{1}' {2}".CheckedFormat(groupDataAccumulator.TableName, mgpi.MappedPointName, columnType) };

                            addColumnCmd.ExecuteNonQuery();

                            existingColumnNameSet.Add(mgpi.MappedPointName);
                        }
                        catch (SQLiteException ex)
                        {
                            // generally because we have attempted to add the same column twice.
                            WriteError("Error adding column '{0}' {1} to table '{2}': {3}".CheckedFormat(mgpi.MappedPointName, columnType, groupDataAccumulator.TableName, ex.ToString(ExceptionFormat.TypeAndMessage)));
                        }
                    }

                    var insertDataRowCmd = new SQLiteCommand(dbConn) { CommandText = "INSERT INTO '{0}' (DateTime,{1}) VALUES (?,{2})".CheckedFormat(groupDataAccumulator.TableName, String.Join(",", filteredColumnNames.Select(colName => "'{0}'".CheckedFormat(colName))), String.Join(",", filteredColumnNames.Select(name => "?"))) };
                    var dateTimeParam = new SQLiteParameter("dateTime");

                    var ioPointParamsTupleArray = columnDecoderTupleArray.Where(t => t.Item2).Select(t => Tuple.Create(new SQLiteParameter(t.Item3), t.Item1)).ToArray();

                    insertDataRowCmd.Parameters.Add(dateTimeParam);
                    insertDataRowCmd.Parameters.AddRange(ioPointParamsTupleArray.Select(t => t.Item1).ToArray());

                    var transaction = dbConn.BeginTransaction();
                    int count = 0;
                    {
                        foreach (var vcSetRow in groupDataAccumulator.dataRowList)
                        {
                            if (++count % 2000 == 0)
                            {
                                transaction.Commit();
                                transaction = dbConn.BeginTransaction();
                            }

                            dateTimeParam.Value = fileBaseTime + vcSetRow.FileDeltaTimeStamp.FromSeconds();

                            foreach (var t in ioPointParamsTupleArray)
                                t.Item1.Value = vcSetRow.VCArray[t.Item2].ValueAsObject;

                            insertDataRowCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }

            object mutex = new object();

            SQLiteConnection dbConn;
        }

        public class CSVDataWriter : DisposableBase, IDataWriter
        {
            public CSVDataWriter(Settings settings)
            {
                Settings = settings;
                extensionToUse = System.IO.Path.GetExtension(Settings.DataFileName).MapNullOrEmptyTo(".csv");
                baseFileName = Settings.DataFileName.RemoveSuffixIfNeeded(extensionToUse);

                AddExplicitDisposeAction(() =>
                    {
                        Fcns.DisposeOfObject(ref occurrenceFileStream);
                        Fcns.DisposeOfObject(ref groupFileStreamList);
                    });
            }

            Settings Settings { get; set; }
            string baseFileName, extensionToUse;

            public void WriteContent(MDRFContent mdrfContent)
            {
                DateTime fileBaseTime = mdrfContent.MDRFFileReader.DateTimeInfo.UTCDateTime.ToLocalTime();

                lock (mutex)
                {
                    AddAccumulatedOccurrences(mdrfContent.OccurrenceAccumulatorArray.Where(oa => oa.RecordThisOccurrence), fileBaseTime);

                    // for each accumulated group - add the data to the corresponding table
                    foreach (var groupDataAccumulator in mdrfContent.GroupDataAccumulatorArray.Where(gda => gda.RecordThisGroup))
                        AddAccumulatedGroupData(mdrfContent, groupDataAccumulator, fileBaseTime);
                }
            }

            public void WriteMessage(string mesg, bool writeToConsole = true, Logging.IMesgEmitter emitter = null)
            {
                if (writeToConsole)
                    Console.WriteLine(mesg);

                if (emitter != null && emitter.IsEnabled)
                    emitter.Emit(mesg);
            }

            public void WriteError(string errorMesg, bool writeToConsole = true, Logging.IMesgEmitter emitter = null)
            {
                if (writeToConsole)
                    Console.WriteLine(errorMesg);

                if (emitter != null && emitter.IsEnabled)
                    emitter.Emit(errorMesg);
            }

            private void AddAccumulatedOccurrences(IEnumerable<OccurrenceAccumulator> occurrenceAccumulatorSet, DateTime fileBaseTime)
            {
                var flattenedSortedArray = occurrenceAccumulatorSet.SelectMany(accum => accum.vcRowList.Select(vcRow => Tuple.Create(accum.OccurrenceInfo.Name, fileBaseTime + vcRow.FileDeltaTimeStamp.FromSeconds(), vcRow.NormalizedVC.ValueAsObject))).OrderBy(t => t.Item2).ToArray();

                lock (mutex)
                {
                    if (occurrenceFileStream == null)
                    {
                        string filePath = string.Concat(baseFileName, "_Occurrences", extensionToUse);

                        occurrenceFileStream = System.IO.File.CreateText(filePath);

                        // write the header.
                        occurrenceFileStream.WriteLine("DateTime,Name,Value");
                    }

                    foreach (var t in flattenedSortedArray)
                    {
                        occurrenceFileStream.WriteLine("{0:MM/dd/yyyy HH:mm:ss.fff},{1},{2}".CheckedFormat(t.Item2, t.Item1, t.Item3));
                    }

                    occurrenceFileStream.Flush();
                }
            }

            private void AddAccumulatedGroupData(MDRFContent mdrfContent, GroupDataAccumulator groupDataAccumulator, DateTime fileBaseTime)
            {
                lock (mutex)
                {
                    var groupWriteHelper = groupWriteHelperDictionary.SafeTryGetValue(groupDataAccumulator.MappedGroupName);

                    if (groupWriteHelper == null)
                    {
                        string filePath = string.Concat(baseFileName, "_", groupDataAccumulator.MappedGroupName, extensionToUse);

                        groupWriteHelper = new GroupWriteHelper()
                        {
                            mappedGroupName = groupDataAccumulator.MappedGroupName,
                            fileStream = System.IO.File.CreateText(filePath),
                            columnNameArray = groupDataAccumulator.MappedGroupPointInfoArray.Select(mgpi => mgpi.MappedPointName).ToArray(),
                        };

                        groupWriteHelperDictionary[groupWriteHelper.mappedGroupName] = groupWriteHelper;

                        // write the header.
                        groupWriteHelper.fileStream.WriteLine(string.Concat("DateTime,", string.Join(",", groupWriteHelper.columnNameArray)));
                    }

                    // build mapping from this gda's MappedGroupPointInfoArray to the column indexes that the CSV uses.
                    var columnNameAndIndexKVPSet = groupDataAccumulator.MappedGroupPointInfoArray.Select((mgpi, columnIndex) => KVP.Create(mgpi.MappedPointName, columnIndex)).ToArray();
                    var mappedPointNameToIndexDictionary = new Dictionary<string, int>().SafeAddRange(columnNameAndIndexKVPSet);

                    int[] reverseMappedPointIndexByColumn = groupWriteHelper.columnNameArray.Select(columnName => mappedPointNameToIndexDictionary.SafeTryGetValue(columnName, fallbackValue: -1)).ToArray();

                    var fileStream = groupWriteHelper.fileStream;
                    foreach (var row in groupDataAccumulator.dataRowList)
                    {
                        fileStream.Write("{0:MM/dd/yyyy HH:mm:ss.fff}".CheckedFormat(fileBaseTime + row.FileDeltaTimeStamp.FromSeconds()));
                        foreach (var mappedPointIndex in reverseMappedPointIndexByColumn)
                        {
                            var vc = row.VCArray.SafeAccess(mappedPointIndex);

                            fileStream.Write(",");

                            switch (vc.cvt)
                            {
                                case ContainerStorageType.None:
                                    break;
                                    
                                case ContainerStorageType.Boolean:
                                    fileStream.Write(vc.u.b.MapToInt());
                                    break;

                                default:
                                    fileStream.Write(vc.ValueAsObject.SafeToString());
                                    break;
                            }
                        }

                        fileStream.WriteLine();
                    }
                }
            }

            object mutex = new object();

            System.IO.StreamWriter occurrenceFileStream;

            DisposableList<System.IO.StreamWriter> groupFileStreamList = new DisposableList<StreamWriter>();
            Dictionary<string, GroupWriteHelper> groupWriteHelperDictionary = new Dictionary<string, GroupWriteHelper>();

            private class GroupWriteHelper
            {
                public string mappedGroupName;
                public System.IO.StreamWriter fileStream;
                public string[] columnNameArray;
            }
        }
    }

    public static partial class ExtensionMethods
    {
        public static IEnumerable<string> GetFieldNames(this SQLiteDataReader dataReader)
        {
            int fieldCount = dataReader.FieldCount;

            return Enumerable.Range(0, fieldCount).Select(fieldIndex => dataReader.GetName(fieldIndex));
        }

        public static IEnumerable<ValueContainer[]> GenerateValuesEnumerator(this SQLiteDataReader dataReader)
        {
            int fieldCount = dataReader.FieldCount;
            object[] objArray = new object[fieldCount];
            while (dataReader.Read())
            {
                dataReader.GetValues(objArray);
                yield return objArray.Select(obj => ValueContainer.CreateFromObject(obj)).ToArray();
            }

            yield break;
        }
    }
}
