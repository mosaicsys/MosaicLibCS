//-------------------------------------------------------------------
/*! @file LogMessageHandler.cs
 * This file defines the public interface and factory methods for
 * the supported types of LogMessageHandlers.  It also contains
 * the implementation class(s) for some of these types.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2006 Mosaic Systems Inc.  (C++ library version)
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
using System.Linq;

using MosaicLib.Utils;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Time;
using System.Runtime.Serialization;
using MosaicLib.Modular.Common;

namespace MosaicLib
{
    public static partial class Logging
    {
        //-------------------------------------------------------------------
        #region ILogMessageHandler

        /// <summary>
        /// This interface defines the methods that must be provied by any object that is able to accept
        /// and/or record messages from the MosaicLib Logging LogDistribution system.
        /// </summary>
        /// <remarks>
        /// See MosaicLib.Logging.Handlers pseuod namespace for base class helpers and implementation objects
        /// </remarks>
        public interface ILogMessageHandler
        {
            /// <summary>Returns the name of this LogMessageHandler</summary>
            string Name { get; }

            /// <summary>Returns the source LoggerConfig for this LogMessageHandler - LoggerConfig.GroupName is derived from handler name.</summary>
            LoggerConfig LoggerConfig { get; }

            /// <summary>Notification List that is signaled on any completed message delivery.</summary>
            Utils.IBasicNotificationList NotifyOnCompletedDelivery { get; }

            /// <summary>
            /// LogMessage Handling method API for direct call by clients which generate and distribute one message at a time.
            /// Handler is expected to re-test that the message is enabled before processing it (LogDistribution will already have done this for single messages)
            /// </summary>
            /// <param name="lm">
            /// Gives the message to handle (save, write, relay, ...).
            /// </param>
            void HandleLogMessage(LogMessage lm);

            /// <summary>
            /// LogMessage Handling method API for use on outputs of queued delivery engines which agregate and distribute one or more messages at a time.
            /// Handler is expected to test if each message is enabled before processing it.
            /// </summary>
            /// <param name="lmArray">
            /// Gives an array of message to handle as a set (save, write, relay, ...).  This set may be given to muliple handlers and as such may contain messages that are not relevant to this handler.
            /// As such Handler may additionally filter out or skip any messages from the given set as approprate.
            /// </param>
            void HandleLogMessages(LogMessage[] lmArray);

            /// <summary>Query method that may be used to tell if message delivery for a given message is still in progress on this handler.</summary>
            bool IsMessageDeliveryInProgress(int testMesgSeqNum);

            /// <summary>Once called, this method only returns after the handler has made a reasonable effort to verify that all outsanding, pending messages, visible at the time of the call, have been full processed before the call returns.</summary>
            void Flush();

            /// <summary>
            /// Used to tell the handler that the LogMessageDistribution system is shuting down.  
            /// Handler is expected to close, release and/or Dispose of any unmanged resources before returning from this call.  
            /// Any attempt to pass messages after this point may be ignored by the handler.
            /// </summary>
            void Shutdown();

            /// <summary>
            /// Used to tell the handler to (re)Start processing distributed messages, as if the handler has just been constructed.
            /// </summary>
            void StartIfNeeded();
        }

        #endregion

        //-------------------------------------------------------------------
        #region LogMessageHandler factory classes and methods.

        // the following methodes provide globally usable factory methods for creating different types of
        //	log message handler objects that may be used with the log distribution system.

        /// <summary>Creates a ConsoleLogMessageHandler given name (null maps to "LMH.Console") and logGate (null maps to LogGate.All).</summary>
        public static ILogMessageHandler CreateConsoleLogMessageHandler(string name = null, LogGate? logGate = null, bool data = true, bool nvs = true)
        {
            return new Handlers.ConsoleLogMesssageHandler(name ?? "LMH.Console", logGate ?? LogGate.All, new Handlers.LineFormat(date: false, qpc: true, source: true, tabStr: " ", data: data, nvs: nvs));
        }

        /// <summary>Creates a SimpleFileLogMessageHandler to write to the given filePath, and given LogGate value (null is mapped to LogGate.All).  Optionally includes files and line numbers.  lmh name is "LMH." + filePath</summary>
        public static ILogMessageHandler CreateSimpleFileLogMessageHandler(string filePath, bool includeFileAndLines = false, LogGate? logGate = null)
        {
            return new Handlers.SimpleFileLogMessageHandler(filePath, includeFileAndLines, logGate ?? LogGate.All);
        }

        /// <summary>Creates a Win32DebugLogMessageHandler given name (null maps to "LMH.Debug") and logGate (null maps to LogGate.All)</summary>
        /// <remarks>appName is used to prefix all messages that are sent to the debugger</remarks>
        public static ILogMessageHandler CreateWin32DebugLogMessageHandler(string appName, string name = null, LogGate? logGate = null)
        {
            return new Handlers.Win32DebugLogMessageHandler(appName, name ?? "LMH.Debug", logGate ?? LogGate.All);
        }

        /// <summary>Creates a DiagnosticTraceLogMessageHandler given name (null maps to "LMH.DiagnosticTrace") and logGate (null maps to LogGate.All)</summary>
        public static ILogMessageHandler CreateDiagnosticTraceLogMessageHandler(string name = null, LogGate ? logGate = null)
        {
            return new Handlers.DiagnosticTraceLogMessageHandler(name ?? "LMH.DiagnosticTrace", logGate ?? LogGate.All, new Handlers.LineFormat(date: false, qpc: true, level: true, source: true, data: false, fandl: false, endlStr: "", tabStr: " "));
        }

        /// <summary>Creates a NullLogMessageHandler given name (null maps to "LMH.Null") and logGate (null maps to LogGate.None)</summary>
        public static ILogMessageHandler CreateNullLogMessageHandler(string name = null, LogGate ? logGate = null)
        {
            return new Handlers.NullLogMessageHandler(name ?? "LMH.Null", logGate ?? LogGate.None, false);
        }

        //-------------------------------------------------------------------

        /// <summary>The standard default mesg queue size: 5000</summary>
        const int DefaultMesgQueueSize = 5000;

        /// <summary>Creates a wrapper LMH that serves to Queue delivery of messages to the given targetLMH.  Uses given maxQueueSize</summary>
        public static ILogMessageHandler CreateQueueLogMessageHandler(ILogMessageHandler targetLMH, int maxQueueSize = DefaultMesgQueueSize)
        {
            return new Handlers.QueueLogMessageHandler(targetLMH, maxQueueSize);
        }

        //-------------------------------------------------------------------

        #region FileRotationLogging

        /// <summary>
        /// Contains all configuration information that is used to setup a FileRotation type log message handler (queued or not).
        /// </summary>
        public struct FileRotationLoggingConfig
        {
            /// <summary>sink name - may be used as prefix to created files in directory</summary>
            public string name;

            /// <summary>full path to directory in which to place files.  This directory is only intended to store files that relate to this rotation set</summary>
            public string dirPath;

            /// <summary>the file name prefix to use for files generated using this configuration.</summary>
            public string fileNamePrefix;

            /// <summary>the file name suffix (usually something like '.log') to use for files generated using this configuration.</summary>
            public string fileNameSuffix;

            /// <summary>The desired logGate.  Defaults to LogGate.None unless overriden in constructor</summary>
            public LogGate logGate;

            /// <summary>true for date_time in middle of name, false for 5 digits in middle of name.  files are named "[name][date_time].log" or "[name][nnnnn].log"</summary>
            public bool nameUsesDateAndTime;

            /// <summary>Sub-structure used to defines the set of values that determine when the ring must advance to the next file.</summary>
            public struct AdvanceRules
            {
                /// <summary>the maximum desired size of each file (0 for no limit)</summary>
                public int fileSizeLimit;
                /// <summary>file age limit in seconds (0 for no limit)</summary>
                public double fileAgeLimitInSec;
                /// <summary>file age limit (TimeSpan.Zero for no limit)</summary>
                public TimeSpan FileAgeLimit { get { return TimeSpan.FromSeconds(fileAgeLimitInSec); } set { fileAgeLimitInSec = value.TotalSeconds; } }
                /// <summary>selected advance on boundary value</summary>
                public AdvanceOnTimeBoundary advanceOnTimeBoundary;
                /// <summary>the period after checking the active file's size before it will be checked again.  Leave as zero for calculation based on fileAgeLimitInSec</summary>
                public double testPeriodInSec;
                /// <summary>the period after checking the active file's size before it will be checked again.  Leave as TimeSpan.Zero for calculation based on fileAgeLimitInSec</summary>
                public TimeSpan TestPeriod { get { return TimeSpan.FromSeconds(testPeriodInSec); } set { testPeriodInSec = value.TotalSeconds; } }
            }
            /// <summary>Defines the set of values that determine when the ring must advance to the next file.</summary>
            public AdvanceRules advanceRules;

            /// <summary>Sub-structure used to define the set of values that determine when old files in the ring must be purged/deleted.</summary>
            public struct PurgeRules
            {
                /// <summary>the user stated maximum number of files (or zero for no limit).  Must be 0 or be between 2 and 5000</summary>
                public int dirNumFilesLimit;
                /// <summary>the user stated maximum size of the set of managed files or zero for no limit</summary>
                public long dirTotalSizeLimit;
                /// <summary>the user stated maximum age in seconds of the oldest file or zero for no limit</summary>
                public double fileAgeLimitInSec;

                /// <summary>the user stated maximum age of the oldest file or TimeSpan.Zero for no limit</summary>
                public TimeSpan FileAgeLimit { get { return TimeSpan.FromSeconds(fileAgeLimitInSec); } set { fileAgeLimitInSec = value.TotalSeconds; } }

                /// <summary>
                /// Specific copy constructor.  
                /// Sets fileAgeLimit, dirNumFilesLimit and dirTotalSizeLimit from similar fields in given pruneRules.
                /// </summary>
                public PurgeRules(File.DirectoryTreePruningManager.PruneRules pruneRules)
                    : this()
                {
                    FileAgeLimit = pruneRules.FileAgeLimit;
                    dirNumFilesLimit = pruneRules.TreeNumFilesLimit;
                    dirTotalSizeLimit = pruneRules.TreeTotalSizeLimit;
                }
            }
            /// <summary>Defines the set of values that determine when old files in the ring must be purged/deleted.</summary>
            public PurgeRules purgeRules;

            /// <summary>only used by queued file rotation type log message handlers.  Defines the maximum number of messages that can be enqueued before being dropped or processed.</summary>
            public int mesgQueueSize;

            /// <summary>Set to true to capture and include the message source file name and line number</summary>
            public bool includeFileAndLine;
            /// <summary>Set to true to include the QpcTime in the output text.</summary>
            public bool includeQpcTime;
            /// <summary>Set to true to include the ThreadInfo in the output text.</summary>
            public bool includeThreadInfo;
            /// <summary>Set to true to include the NamedValueSet contents in the output text.</summary>
            public bool includeNamedValueSet;

            /// <summary>A list of strings for file names that will not be tracked or purged by the ring.</summary>
            public List<string> excludeFileNamesSet;

            /// <summary>Set to true if the ring is expected to create the directory if needed or if it should disable logging if the directory does not exist.</summary>
            public bool createDirectoryIfNeeded;

            /// <summary>
            /// Defines an, optionally null, array of lines of text that will be added at the top of each file that is created when using this configuration
            /// </summary>
            public string[] fileHeaderLines;

            /// <summary>
            /// When this is non-null, Defines an delegate that is invoked to return an array of lines of text that will be added at the top of each file that is created when using this configuration.
            /// The lines produced by this delegate will be added to the file after the fileHeaderLines have been added.
            /// </summary>
            public Func<string[]> fileHeaderLinesDelegate;

            /// <summary>Simple constructor - intended for use with property initializers and/or load from config: gives empty dirPath, LogGate.None, zeros for maxFilesInRing, advanceAfterFileSize and mesgQueueSize.  uses defaults for all other values.</summary>
            public FileRotationLoggingConfig(string name)
                : this(name, String.Empty, LogGate.None, 0, 0, 0)
            { }

            /// <summary>Simple constructor: applyies LogGate.All and other default settings of 2000 files in ring, 1000000 byes per file, DefaultMesgQueueSize.</summary>
            public FileRotationLoggingConfig(string name, string dirPath)
                : this(name, dirPath, LogGate.All)
            { }

            /// <summary>Simple constructor: uses default settings of 2000 files in ring, 1000000 byes per file, DefaultMesgQueueSize</summary>
            public FileRotationLoggingConfig(string name, string dirPath, LogGate logGate)
                : this(name, dirPath, logGate, 2000, 1000000, DefaultMesgQueueSize)
            { }

            /// <summary>
            /// Standard constructor.
            /// Sets fileNamePrefix to the given name and fileNameSuffix to '.log'
            /// </summary>
            public FileRotationLoggingConfig(string name, string dirPath, LogGate logGate, int maxFilesInRing, int advanceAfterFileSize, int mesgQueueSize)
                : this()
            {
                this.name = name;
                this.dirPath = dirPath;
                this.fileNamePrefix = name;
                this.fileNameSuffix = ".log";
                this.logGate = logGate;
                this.mesgQueueSize = mesgQueueSize;
                this.nameUsesDateAndTime = true;
                this.includeFileAndLine = true;
                this.includeQpcTime = true;
                this.includeNamedValueSet = true;

                this.advanceRules = new AdvanceRules();
                this.purgeRules = new PurgeRules();

                purgeRules.dirNumFilesLimit = maxFilesInRing;
                advanceRules.fileSizeLimit = advanceAfterFileSize;
                advanceRules.FileAgeLimit = TimeSpan.FromDays(1.0);

                excludeFileNamesSet = new List<string>();

                createDirectoryIfNeeded = true;
            }

            /// <summary>
            /// Use this method to read the stock set of ModularConfig points using the given configKeyPrefixStr and to update this ring configuration using the valid, non-zero values read from these keys.
            /// <para/>Keys are LogGate, DirectoryPath, MaxFilesToKeep, MaxFileAgeToKeepInDays, MaxTotalSizeToKeep, AdvanceAfterFileReachesSize, AdvanceAfterFileReachesAge, 
            /// IncludeQPCTime, IncludeThreadInfo, IncludeFileAndLine
            /// </summary>
            public FileRotationLoggingConfig UpdateFromModularConfig(string configKeyPrefixStr, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
            {
                ConfigValueSetAdapter<ConfigKeyValuesHelper> adapter = new ConfigValueSetAdapter<ConfigKeyValuesHelper>() { ValueSet = new ConfigKeyValuesHelper(), SetupIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(configKeyPrefixStr);

                ConfigKeyValuesHelper configValues = adapter.ValueSet;

                logGate |= configValues.LogGate;

                if (!configValues.DirectoryPath.IsNullOrEmpty())
                    dirPath = configValues.DirectoryPath;

                if (!configValues.FileNamePrefix.IsNullOrEmpty())
                    fileNamePrefix = configValues.FileNamePrefix;

                if (!configValues.FileNameSuffix.IsNullOrEmpty())
                    fileNameSuffix = configValues.FileNameSuffix;

                if (configValues.MaxFilesToKeep != 0)
                    purgeRules.dirNumFilesLimit = configValues.MaxFilesToKeep;

                if (configValues.MaxFileAgeToKeep != TimeSpan.Zero)
                    purgeRules.FileAgeLimit = configValues.MaxFileAgeToKeep;

                if (configValues.MaxTotalSizeToKeep != 0)
                    purgeRules.dirTotalSizeLimit = configValues.MaxTotalSizeToKeep;

                if (configValues.AdvanceAfterFileReachesSize != 0)
                    advanceRules.fileSizeLimit = configValues.AdvanceAfterFileReachesSize;

                if (configValues.AdvanceAfterFileReachesAge != TimeSpan.Zero)
                    advanceRules.FileAgeLimit = configValues.AdvanceAfterFileReachesAge;

                if (!configValues.AdvanceOnTimeBoundary.IsNullOrEmpty)
                    advanceRules.advanceOnTimeBoundary = configValues.AdvanceOnTimeBoundary.GetValue<AdvanceOnTimeBoundary>(false);

                if (configValues.IncludeQPCTime.HasValue)
                    includeQpcTime = configValues.IncludeQPCTime.GetValueOrDefault();

                if (configValues.IncludeThreadInfo.HasValue)
                    includeThreadInfo = configValues.IncludeThreadInfo.GetValueOrDefault();

                if (configValues.IncludeFileAndLine.HasValue)
                    includeFileAndLine = configValues.IncludeFileAndLine.GetValueOrDefault();

                if (configValues.IncludeNamedValueSet.HasValue)
                    includeNamedValueSet = configValues.IncludeNamedValueSet.GetValueOrDefault();

                return this;
            }

            /// <summary>Glue class - used to define config points that can be used to setup a FileRotationLoggingConfig object.  This class is required because the ConfigValueSetAdapter does not support use with structs (which FileRotationLoggingConfig is)</summary>
            public class ConfigKeyValuesHelper
            {
                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public Logging.LogGate LogGate { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public string DirectoryPath { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public string FileNamePrefix { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public string FileNameSuffix { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public int MaxFilesToKeep { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public double MaxFileAgeToKeepInDays { get; set; }
                /// <summary>TimeSpan version of corresponding InDays property</summary>
                public TimeSpan MaxFileAgeToKeep { get { return TimeSpan.FromDays(MaxFileAgeToKeepInDays); } }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public long MaxTotalSizeToKeep { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public int AdvanceAfterFileReachesSize { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public double AdvanceAfterFileReachesAgeInDays { get; set; }

                /// <summary>TimeSpan version of corresponding InDays property</summary>
                public TimeSpan AdvanceAfterFileReachesAge { get { return TimeSpan.FromDays(AdvanceAfterFileReachesAgeInDays); } }

                /// <summary>Target property for key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public ValueContainer AdvanceOnTimeBoundary { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public bool? IncludeQPCTime { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public bool? IncludeThreadInfo { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public bool? IncludeFileAndLine { get; set; }

                /// <summary>Target property for a key of the same name</summary>
                [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
                public bool? IncludeNamedValueSet { get; set; }
            }
        }

        /// <summary>
        /// This enumeration is used to select one of a fixed number of choices for log file advance on Time boundaries.
        /// <para/>None (Default: 0), Hourly, Daily, Weekly, Monthly
        /// </summary>
        [DataContract(Namespace = Constants.LoggingNameSpace)]
        public enum AdvanceOnTimeBoundary : int
        {
            /// <summary>Used to select that log files should not advance on a simply (repeated) time boundary.</summary>
            [EnumMember]
            None = 0,

            /// <summary>Used to select that a new log file should be used at the start of each hour.</summary>
            [EnumMember]
            Hourly,

            /// <summary>Used to select that a new log file should be used at the start of each day.</summary>
            [EnumMember]
            Daily,

            /// <summary>Used to select that a new log file should be used at the start of each Monday.</summary>
            [EnumMember]
            EachMonday,

            /// <summary>Used to select that a new log file should be used at the start of each Sunday.</summary>
            [EnumMember]
            EachSunday,

            /// <summary>Used to select that a new log file should be used at the start of each month.</summary>
            [EnumMember]
            Monthly,
        }

        /// <summary>Creates a QueueLogMessageHandler wrapped around a TextFileRotationLogMessageHandler configured using the given config value.</summary>
        public static ILogMessageHandler CreateQueuedTextFileRotationDirectoryLogMessageHandler(FileRotationLoggingConfig config)
        {
            return new Handlers.QueueLogMessageHandler(new Handlers.TextFileRotationLogMessageHandler(config), config.mesgQueueSize);
        }

        /// <summary>Creates a QueueLogMessageHandler wrapped around a TextFileDateTreeLogMessageHandler configured using the given config value.</summary>
        public static ILogMessageHandler CreateQueuedTextFileDateTreeLogMessageHandler(Handlers.TextFileDateTreeLogMessageHandler.Config config)
        {
            return new Handlers.QueueLogMessageHandler(new Handlers.TextFileDateTreeLogMessageHandler(config), config.MesgQueueSize);
        }

        #endregion

        //-------------------------------------------------------------------

        #endregion

        //-------------------------------------------------------------------
        #region additional data types used by LMH objects but which may be directly used by clients

        /// <summary>
        /// Enumeration used to define the supported data inclusion formats for log output.
        /// <para/>None (0: default), Base64, EscapedAscii, Hex
        /// </summary>
        public enum DataEncodingFormat : int
        {
            /// <summary>Data field will not be included in generated log message output, even when non-empty</summary>
            None = 0,
            /// <summary>Data field will be included in generated log message output using Base64 encoding</summary>
            Base64,
            /// <summary>Data field will be included in generated log message output using Escaped Ascii encoding</summary>
            EscapedAscii,
            /// <summary>Data field will be included in generated log message output using Hex encoding with no seperators</summary>
            Hex,
        }

        #endregion
    }

    public static partial class Logging
    {
        /// <summary>
        /// This partial static class acts as a namespace in which all Handler related classes and definitions are found.
        /// </summary>
        public static partial class Handlers
        {
            //-------------------------------------------------------------------
            #region LogMessageHandler base and support classes (LineFormat, LogMesgHandlerLogger, CommonLogMessageHandlerBase, SimpleLogMessageHandlerBase, StreamWriterLogMessageHandlerBase)

            //-------------------------------------------------------------------

            const string defaultEolStr = "\r\n";
            const string defaultTabStr = "\t";

            /// <summary>
            /// This class provides a simple type of line formatter and writer that is used with various types of
            /// LogMessageHandler.  It primarily provides means for a hanlder to define which fields of a log message
            /// are to be included in the handler's output and to perform output for that handler.  As such both
            /// format to ostream and format to string versions are provided.
            /// </summary>
            public class LineFormat
            {
                /// <summary>Constructor variant.  includes level, data and uses default EOL and Tab strings.</summary>
                public LineFormat(bool date, bool qpc, bool source) 
                    : this(date, qpc, true, source, true, false, defaultEolStr, defaultTabStr) 
                { }

                /// <summary>Constructor variant.  includes level, data and uses default EOL string.</summary>
                public LineFormat(bool date, bool qpc, bool source, string tabStr) 
                    : this(date, qpc, true, source, true, false, defaultEolStr, tabStr) 
                { }

                /// <summary>Constructor variant.  includes data and uses default EOL and Tab strings.</summary>
                public LineFormat(bool date, bool qpc, bool level, bool source, bool fandl) 
                    : this(date, qpc, level, source, true, fandl, defaultEolStr, defaultTabStr) 
                { }

                /// <summary>Full constructor.</summary>
                /// <param name="date">Set to true to include the Date portion in formatted output lines</param>
                /// <param name="qpc">Set to true to include the QPC timestamp portion in formatted output lines</param>
                /// <param name="level">Set to true to include the MesgType/LogLevel portion in formatted output lines</param>
                /// <param name="source">Set to true to include the Logger/Source Name portion in formatted output lines</param>
                /// <param name="data">Set to true to include the base64 coded optional Data in formatted output lines</param>
                /// <param name="fandl">Set to true to include the file name and line number in formatted output lines</param>
                /// <param name="endlStr">Defines the end-of-line string that is used in formatted output lines</param>
                /// <param name="tabStr">Defines the tab string that is used as a column seperator in output lines</param>
                /// <param name="threadInfo">Set to true to include the thread information in the output lines</param>
                /// <param name="nvs">Set to true to include message NamedValueSet information in the output lines</param>
                public LineFormat(bool date = true, bool qpc = true, bool level = true, bool source = true, bool data = true, bool fandl = false, string endlStr = defaultEolStr, string tabStr = defaultTabStr, bool threadInfo = false, bool nvs = true)
                {
                    this.date = date;
                    this.qpc = qpc;
                    this.level = level;
                    this.source = source;
                    this.IncludeData = data;
                    this.fAndL = fandl;
                    this.endLStr = endlStr;
                    this.tabStr = tabStr;
                    IncludeThreadInfo = threadInfo;
                    IncludeNamedValueSet = nvs;
                }

                /// <summary>Copy constructor</summary>
                public LineFormat(LineFormat rhs)
                {
                    SetFrom(rhs);
                }

                /// <summary>Copy constructor helper method</summary>
                public void SetFrom(LineFormat rhs)
                {
                    date = rhs.date;
                    qpc = rhs.qpc;
                    level = rhs.level;
                    source = rhs.source;
                    fAndL = rhs.fAndL;
                    endLStr = rhs.endLStr;
                    tabStr = rhs.tabStr;
                    IncludeNamedValueSet = rhs.IncludeNamedValueSet;
                    IncludeThreadInfo = rhs.IncludeThreadInfo;
                    DataEncodingFormat = rhs.DataEncodingFormat;
                }

                /// <summary>True if the Date is included in formatted output lines.</summary>
                public bool IncludeDate { get { return date; } set { date = value; } }
                /// <summary>True if the QPC timestamp is included in formatted output lines.</summary>
                public bool IncludeQpc { get { return qpc; } set { qpc = value; } }
                /// <summary>True if the MesgType/LogLevel is included in formatted output lines.</summary>
                public bool IncludeLevel { get { return level; } set { level = value; } }
                /// <summary>True if the Logger/Source Name is included in formatted output lines.</summary>
                public bool IncludeSource { get { return source; } set { source = value; } }
                /// <summary>get/set property:  True if NamedValueSet content information will be included in formatted output lines.</summary>
                public bool IncludeNamedValueSet { get; set; }
                /// <summary>True if (optioal) message data converted to base64 and is included in the formatted output lines.</summary>
                public bool IncludeData { get { return (DataEncodingFormat != DataEncodingFormat.None); } set { DataEncodingFormat = (value ? DefaultDataEncodingFormat : DataEncodingFormat.None); } }
                /// <summary>Defines the encoding format used for the optinal message data block, when it is present</summary>
                public DataEncodingFormat DataEncodingFormat { get; set; }
                /// <summary>True if the ThreadName and/or ThreadID is to be included in the formatted output lines.</summary>
                public bool IncludeThreadInfo { get; set; }
                /// <summary>True if the source File and Line information is included in formatted output lines.</summary>
                public bool IncludeFileAndLine { get { return fAndL; } set { fAndL = value; } }

                /// <summary>
                /// This static property may be used to define the data encoding format that is used by default when a LineFormat object is told to IncludeData but is not explicitly given the DataEncodingFormat to use.
                /// <para/>Historically this properties initial value was Base64 but it has been changed to Hex
                /// </summary>
                public static DataEncodingFormat DefaultDataEncodingFormat { get { return defaultDataEncodingFormat; } set { defaultDataEncodingFormat = value; } }
                private static DataEncodingFormat defaultDataEncodingFormat = DataEncodingFormat.Hex;

                /// <summary>Converts the thread info in the given message into a string consiting of the managed TID, the Win32 TID (in hex) and the thread name if it is not null or empty.</summary>
                private string FormatThreadInfo(LogMessage lm)
                {
                    if (String.IsNullOrEmpty(lm.ThreadName))
                        return Utils.Fcns.CheckedFormat("tid:{0:d4}/${1:x4}", lm.ThreadID, lm.Win32ThreadID);
                    else
                        return Utils.Fcns.CheckedFormat("tid:{0:d4}/${1:x4}/{2}", lm.ThreadID, lm.Win32ThreadID, lm.ThreadName);
                }

                /// <summary>
                /// Formats the given message as configured and incrementally Writes it to the given StreamWriter
                /// </summary>
                /// <param name="lm">Gives the LogMessage instance to format and Write</param>
                /// <param name="os">Gives the StreamWriter instance to Write the formatted message to.</param>
                public void FormatLogMessageToOstream(LogMessage lm, System.IO.StreamWriter os)
                {
                    if (!os.BaseStream.CanWrite)
                        return;

                    bool firstItem = true;

                    if (date) { TabIfNeeded(os, ref firstItem); os.Write(lm.GetFormattedDateTime()); }
                    if (qpc) { TabIfNeeded(os, ref firstItem); os.Write((lm.EmittedQpcTime.Time % 1000.0).ToString("000.000000")); }
                    if (level) { TabIfNeeded(os, ref firstItem); os.Write(ConvertToFixedWidthString(lm.MesgType)); }
                    if (source) { TabIfNeeded(os, ref firstItem); os.Write(lm.LoggerName); }
                    { TabIfNeeded(os, ref firstItem); os.Write(lm.MesgEscaped); }
                    if (IncludeNamedValueSet) { os.Write(tabStr); os.Write(lm.NamedValueSet.ToString(false, true)); }
                    switch (DataEncodingFormat)
                    {
                        case DataEncodingFormat.Base64: { os.Write(tabStr); if (lm.Data.IsNullOrEmpty()) os.Write("[]"); else  os.CheckedWrite("[b64 {0}]", base64UrlCoder.Encode(lm.Data)); } break;
                        case DataEncodingFormat.EscapedAscii: { os.Write(tabStr); if (lm.Data.IsNullOrEmpty()) os.Write("[]"); else os.CheckedWrite("[A {0}]", byteStringCoder.Encode(lm.Data).GenerateLoggingVersion()); } break;
                        case DataEncodingFormat.Hex: { os.Write(tabStr); if (lm.Data.IsNullOrEmpty()) os.Write("[]"); else os.CheckedWrite("[hex {0}]", hexCoder.Encode(lm.Data)); } break;
                        case DataEncodingFormat.None:
                        default: break;
                    }
                    if (IncludeThreadInfo) { os.Write(tabStr); os.Write(FormatThreadInfo(lm)); }
                    if (fAndL && lm.SourceStackFrame != null)
                    {
                        { os.Write(tabStr); os.Write(lm.SourceStackFrame.GetFileName()); }
                        { os.Write(tabStr); os.Write(lm.SourceStackFrame.GetFileLineNumber().ToString()); }
                    }
                    { os.Write(endLStr); }
                }

                /// <summary>
                /// Formats the given message as configured and Appends it to the given StringBuilder
                /// </summary>
                /// <param name="lm">Gives the LogMessage instance to format and Append</param>
                /// <param name="ostr">Gives the StringBuilder instance to Append the formatted message into.</param>
                public void FormatLogMessageToStringBuilder(LogMessage lm, System.Text.StringBuilder ostr)
                {
                    bool firstItem = (ostr.Length == 0);

                    if (date) { TabIfNeeded(ostr, ref firstItem); ostr.Append(lm.GetFormattedDateTime()); }
                    if (qpc) { TabIfNeeded(ostr, ref firstItem); ostr.Append((lm.EmittedQpcTime.Time % 1000.0).ToString("000.000000")); }
                    if (level) { TabIfNeeded(ostr, ref firstItem); ostr.Append(ConvertToFixedWidthString(lm.MesgType)); }
                    if (source) { TabIfNeeded(ostr, ref firstItem); ostr.Append(lm.LoggerName); }
                    { TabIfNeeded(ostr, ref firstItem); ostr.Append(lm.MesgEscaped); }
                    if (IncludeNamedValueSet) { ostr.Append(tabStr); ostr.Append(lm.NamedValueSet.ToString(false, true)); }
                    switch (DataEncodingFormat)
                    {
                        case DataEncodingFormat.Base64: { ostr.Append(tabStr); if (lm.Data.IsNullOrEmpty()) ostr.Append("[]"); else  ostr.CheckedAppendFormat("[b64 {0}]", base64UrlCoder.Encode(lm.Data)); } break;
                        case DataEncodingFormat.EscapedAscii: { ostr.Append(tabStr); if (lm.Data.IsNullOrEmpty()) ostr.Append("[]"); else ostr.CheckedAppendFormat("[A {0}]", byteStringCoder.Encode(lm.Data).GenerateLoggingVersion()); } break;
                        case DataEncodingFormat.Hex: { ostr.Append(tabStr); if (lm.Data.IsNullOrEmpty()) ostr.Append("[]"); else ostr.CheckedAppendFormat("[hex {0}]", hexCoder.Encode(lm.Data)); } break;
                        case DataEncodingFormat.None:
                        default: break;
                    }
                    if (IncludeThreadInfo) { ostr.Append(tabStr); ostr.Append(FormatThreadInfo(lm)); }
                    if (fAndL && lm.SourceStackFrame != null)
                    {
                        { ostr.Append(tabStr); ostr.Append(lm.SourceStackFrame.GetFileName()); }
                        { ostr.Append(tabStr); ostr.Append(lm.SourceStackFrame.GetFileLineNumber().ToString()); }
                    }
                    { ostr.Append(endLStr); }
                }

                /// <summary>Method is used to append a TabStr to the given StreamWriter if this is not the first Item in the line.</summary>
                /// <param name="os">Gives the StreamWriter instance to Write the formatted message to.</param>
                /// <param name="firstItem">ref boolean that is externally set to true and which is cleared by this method on the first call.</param>
                protected void TabIfNeeded(System.IO.StreamWriter os, ref bool firstItem)
                {
                    if (!firstItem) os.Write(tabStr);
                    else firstItem = false;
                }

                /// <summary>Method is used to append a TabStr to the given StringBuilder if this is not the first Item in the line.</summary>
                /// <param name="ostr">Gives the StringBuilder instance to Append the formatted message into.</param>
                /// <param name="firstItem">ref boolean that is externally set to true and which is cleared by this method on the first call.</param>
                protected void TabIfNeeded(System.Text.StringBuilder ostr, ref bool firstItem)
                {
                    if (!firstItem) ostr.Append(tabStr);
                    else firstItem = false;
                }

                private bool date, qpc, source, level, fAndL;
                private string endLStr = defaultEolStr;
                private string tabStr = defaultTabStr;
                private readonly Utils.IByteArrayTranscoder base64UrlCoder = Utils.ByteArrayTranscoders.Base64UrlTranscoder;
                private readonly Utils.IByteArrayTranscoder hexCoder = Utils.ByteArrayTranscoders.HexStringTranscoderNoPadding;
                private readonly Utils.IByteArrayTranscoder byteStringCoder = Utils.ByteArrayTranscoders.ByteStringTranscoder;
            }

            //-------------------------------------------------------------------

            /// <summary>
            /// This class is used in LogMessageHandlers to log messages and avoids the recursion issue by emitting these messages directly into the lmh's own HandleLogMessage thus bypassing the distribution system.
            /// </summary>
            public class LogMesgHandlerLogger : LoggerBase
            {
                /// <summary>Constructor</summary>
                /// <param name="logMesgHandler">Gives the LMH to which emitted messages will be passed.</param>
                public LogMesgHandlerLogger(ILogMessageHandler logMesgHandler)
                    : base(logMesgHandler.Name, string.Empty, logMesgHandler.LoggerConfig.LogGate, traceLoggerCtor: false)
                {
                    lmh = logMesgHandler;
                    if (lmh == null)
                        Utils.Asserts.TakeBreakpointAfterFault("LogMesgHandlerLogger: LMH == null");

                    sourceInfo = new LoggerSourceInfo(LoggerID_InternalLogger, Name);
                }

                /// <summary>Handles emitting and consuming the given LogMessage.  ref parameter lm will be set to null</summary>
                public override void EmitLogMessage(ref LogMessage lm)
                {
                    if (lm != null)
                    {
                        lm.NoteEmitted();

                        if (!loggerHasBeenShutdown)
                        {
                            lmh.HandleLogMessage(lm);
                        }

                        lm = null;
                    }
                }

                /// <summary>This mechanism is not supported here.  This method returns true immediately.</summary>
                public override bool WaitForDistributionComplete(TimeSpan timeLimit) { return true; }

                private ILogMessageHandler lmh = null;

                /// <summary>Defines the ClassName value that will be used by the LoggerBase when generating trace messages (if enabled).</summary>
                protected override string ClassName { get { return "LogMesgHandlerLogger"; } }
            }

            //-------------------------------------------------------------------
            /// <summary>
            /// This class implements the basic and common parts of the ILogMessageHandler interface, especially those
            /// parts that can be done easily and in a non-use specific manner.
            /// </summary>
            /// <remarks>
            /// This class now provides default implementations for the IsMessageDeliveryInProgress, Flush, and Shutdown methods, where
            /// IsMessageDeliveryInProgress returns false, Flush is a noop, and Shutdown calls Flush.
            /// </remarks>
            public abstract class CommonLogMessageHandlerBase : Utils.DisposableBase, ILogMessageHandler
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="name">Defines the LMH name.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                /// <param name="recordSourceStackFrame">Indicates if this handler should record and save source file and line numbers.</param>
                public CommonLogMessageHandlerBase(string name, LogGate logGate, bool recordSourceStackFrame = true)
                {
                    this.name = name;
                    loggerConfig = new LoggerConfig() { GroupName = "LMH:" + name, LogGate = logGate, RecordSourceStackFrame = recordSourceStackFrame };

                    logger = new LogMesgHandlerLogger(this);
                }

                /// <summary>Gives the LMH's Name</summary>
                public string Name { get { return name; } }

                /// <summary>Gives the LogGate being used by this LMH</summary>
                public LogGate LogGate { get { return loggerConfig.LogGate; } }

                /// <summary>Gives the LoggerConfig that this LMH is using.</summary>
                public LoggerConfig LoggerConfig { get { return loggerConfig; } }

                /// <summary>Gives the notification list that must be notified on each delivery of a set of 1 or more messages.</summary>
                public Utils.IBasicNotificationList NotifyOnCompletedDelivery { get { return notifyMessageDelivered; } }

                /// <summary>Defines the abstract method that must be implemented and which is used to handle a single LogMessage</summary>
                public abstract void HandleLogMessage(LogMessage lm);

                /// <summary>Defines the abstract method that must be implemented and which is used to handle arrays of LogMessages</summary>
                public abstract void HandleLogMessages(LogMessage[] lmArray);

                /// <summary>Query method that may be used to tell if message delivery for a given message is still in progress on this handler.</summary>
                /// <remarks>This default implementation returns false.</remarks>
                public virtual bool IsMessageDeliveryInProgress(int testMesgSeqNum) { return false; }

                /// <summary>Once called, this method only returns after the handler has made a reasonable effort to verify that all outsanding, pending messages, visible at the time of the call, have been full processed before the call returns.</summary>
                /// <remarks>This default implementation does nothing.  This method should be overriden/replaced in derived versions when appropriate.</remarks>
                public virtual void Flush() 
                { }

                /// <summary>
                /// Used to tell the handler that the LogMessageDistribution system is shuting down.  
                /// Handler is expected to close, release and/or Dispose of any unmanged resources before returning from this call.  
                /// Any attempt to pass messages after this point may be ignored by the handler.
                /// </summary>
                /// <remarks>This default implementation calls <see cref="Flush"/></remarks>
                public virtual void Shutdown() 
                { 
                    Flush(); 
                }

                /// <summary>This virtual method will be called when the distribution system is being restarted.  It can be overriden to allows a drived handler class to handler to (re)Start processing distributed messages, as if the handler has just been constructed.</summary>
                public virtual void StartIfNeeded()
                { }

                /// <summary>tell any interested Notifyable objects (as currently in the notifyMessageDelivered list) that messages have been delivered</summary>
                protected void NoteMessagesHaveBeenDelivered()
                {
                    notifyMessageDelivered.Notify();
                }

                /// <summary>Returns true if the MesgType from the given LogMessage is currently enabled in this handler's LoggerConfig object.</summary>
                protected bool IsMessageTypeEnabled(LogMessage lm)
                {
                    return ((lm != null) ? loggerConfig.IsTypeEnabled(lm.MesgType) : false);
                }

                /// <summary>Implements the required Dispose method from the Utils.DisposableBase class.  Calls Shutdown when called explicitly.</summary>
                protected override void Dispose(MosaicLib.Utils.DisposableBase.DisposeType disposeType)
                {
                    if (disposeType == DisposeType.CalledExplicitly)
                        Shutdown();
                }

                // all LMH implementations need to know their name and need a pointer to the 
                //	source ID that they are to use when generating their own messages (many of them
                //	need to do this...)

                /// <summary>Protected storage for this object's name</summary>
                protected string name = null;

                /// <summary>Protected storage for this object's current loggerConfig</summary>
                protected LoggerConfig loggerConfig = LoggerConfig.AllNoFL;

                /// <summary>Protected storage for this object's internal ILogger object.</summary>
                protected ILogger logger = null;

                Utils.BasicNotificationList notifyMessageDelivered = new MosaicLib.Utils.BasicNotificationList();

                #region Header line helper methods (used by thoese LMH types that support header lines)

                /// <summary>
                /// Helper method used to manage generation of the full set of header strings as the concatination of the given list and the delegate generated list,
                ///  wrapping these messages in a corresponding set of LogMessages from the "header logger" and then consuming (logging) these messages using a caller provided
                ///  message consuming delegate.
                /// <para/>If baseHeaderLines contains a null and there is also a non-null delegateHeaderLines delegate then the results of evaluating the delegate will be
                /// inserted in place of the null.  Otherwise the results of the delegate will be appended to the baseHeaderLines.
                /// </summary>
                protected void GenerateAndProduceHeaderLines(string[] baseHeaderLines, Func<string[]> headerLineDelegate, Action<Logging.LogMessage> headerLogMessageConsumer)
                {
                    List<string> headerLines = new List<string>(baseHeaderLines ?? emptyStringArray);

                    if (headerLineDelegate != null)
                    {
                        string[] delegateHeaderLines = null;

                        try
                        {
                            delegateHeaderLines = headerLineDelegate();
                        }
                        catch (System.Exception ex)
                        {
                            delegateHeaderLines = new[] { "HeaderLinesDelegate generated unexpected {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessage)) };
                        }

                        int firstNullIdx = -1;
                        if (headerLines.Contains(null))
                            firstNullIdx = headerLines.FindIndex(s => (s == null));

                        if (firstNullIdx >= 0)
                        {
                            headerLines.RemoveAt(firstNullIdx);
                            headerLines.InsertRange(firstNullIdx, delegateHeaderLines);
                        }
                        else
                        {
                            headerLines.AddRange(delegateHeaderLines);
                        }
                    }

                    if (headerLoggerStub == null)
                        headerLoggerStub = new Logger("Header");

                    foreach (Logging.LogMessage lm in headerLines.Where(mesg => mesg != null).Select(mesg => headerLoggerStub.GetLogMessage(MesgType.Info, mesg).NoteEmitted()).ToArray())
                    {
                        headerLogMessageConsumer(lm);
                    }
                }

                /// <summary>This logger instance is used to obtain the log messages that will be formatted into the newly opened file to implement the header.</summary>
                private Logging.Logger headerLoggerStub = null;
                private static readonly string[] emptyStringArray = new string[0];

                #endregion
            }

            //-------------------------------------------------------------------
            /// <summary>
            /// This class implements common base class functionality for simple LogMessageHandler classes.
            /// <para/>This base class should only be used in cases where the cost of handling an array of messages is the same as the total cost of handling them one at a time.
            /// </summary>
            /// <remarks>
            /// This class implements both the HandleLogMessage and HandleLogMessages methods by
            /// requiring the derived class to implement a common internal method called InnerHandleLogMessage.
            /// This abstract InnerHandleLogMessage method is used to handle log messages one at a time when given to either the HandleLogMessage or HandleLogMessages method.
            /// 
            /// This is a useful simplification for some types of message handlers (but typically not queued ones)
            /// </remarks>
            public abstract class SimpleLogMessageHandlerBase : CommonLogMessageHandlerBase
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="name">Defines the LMH name.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                /// <param name="recordSourceStackFrame">Indicates if this handler should record and save source file and line numbers.</param>
                public SimpleLogMessageHandlerBase(string name, LogGate logGate, bool recordSourceStackFrame = true)
                    : base(name, logGate, recordSourceStackFrame)
                { }

                /// <summary>LogMessage Handling method API for direct call by clients which generate and distribute one message at a time.</summary>
                public override void HandleLogMessage(LogMessage lm)
                {
                    if (!IsMessageTypeEnabled(lm))
                        return;

                    InnerHandleLogMessage(lm);

                    NoteMessagesHaveBeenDelivered();
                }

                /// <summary>LogMessage Handling method API for use on outputs of queued delivery engines which agregate and distribute one or more messages at a time.</summary>
                public override void HandleLogMessages(LogMessage[] lmArray)
                {
                    foreach (LogMessage lm in lmArray)
                    {
                        if (IsMessageTypeEnabled(lm))
                            InnerHandleLogMessage(lm);
                    }

                    NoteMessagesHaveBeenDelivered();
                }

                /// <summary>
                /// This is the common abstract method that is used for handleing each log message.  This method must be implemented in a derived type
                /// and must provide the type specific behavior used to process and record each message when using this base class.
                /// The HandleLogMessage and HandleLogMessages methods above make use of this method to process each log message given to the LMH in order. 
                /// </summary>
                /// <param name="lm">Gives the LogMessage instance that is to be handled.</param>
                protected abstract void InnerHandleLogMessage(LogMessage lm);
            }

            //-------------------------------------------------------------------
            /// <summary>
            /// This class is a reasonable base class for all StreamWriter type LogMessageHandlers
            /// </summary>
            public class StreamWriterLogMessageHandlerBase : SimpleLogMessageHandlerBase
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="name">Defines the LMH name.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                /// <param name="lineFmt">Gives the line formatting rules that will be used for this LMH</param>
                /// <param name="ostream">Gives the StreamWriter instance to which the output text will be written.</param>
                /// <param name="flushAfterEachWrite">Set to true to flush the ostream after each write, or false to Flush only when explicitly told to by the LogDistribution system.</param>
                public StreamWriterLogMessageHandlerBase(string name, LogGate logGate, LineFormat lineFmt, System.IO.StreamWriter ostream, bool flushAfterEachWrite)
                    : base(name, logGate, recordSourceStackFrame: lineFmt.IncludeFileAndLine)
                {
                    this.lineFmt = lineFmt;
                    this.ostream = ostream;
                    this.flushAfterEachWrite = flushAfterEachWrite;

                    if (lineFmt == null)
                        Utils.Asserts.TakeBreakpointAfterFault("LineFmt is null");
                    if (ostream == null)
                        Utils.Asserts.TakeBreakpointAfterFault("ostream is null");

                    // add an action to close the ostream when this object is disposed.
                    AddExplicitDisposeAction(
                        () =>
                        {
                            if (this.ostream != null)
                            {
                                this.ostream.Close();
                                this.ostream = null;
                            }
                        });
                }

                /// <summary>
                /// Takes the given LogMessage, formats it and writes it to the ostream.  Calls Flush if the object was constructed with FlushAfterEachWrite set to true.
                /// </summary>
                protected override void InnerHandleLogMessage(LogMessage lm)
                {
                    lineFmt.FormatLogMessageToOstream(lm, ostream);
                    if (flushAfterEachWrite)
                        Flush();
                }

                /// <summary>
                /// If the ostream is writable then this method will call Flush on it.
                /// </summary>
                public override void Flush()
                {
                    if (ostream.BaseStream.CanWrite)
                        ostream.Flush();
                }

                /// <summary>
                /// Calls base.Shutdown and then closes the ostream.  This method does not directly dispose of the ostream itself.
                /// </summary>
                public override void Shutdown()
                {
                    base.Shutdown();

                    // shutdown no longer closes the ostream
                    ostream.Flush();
                }

                /// <summary>Gives access to the LineFormat instance that was given to this object at construction time.</summary>
                protected LineFormat lineFmt = null;

                /// <summary>Gives access to the ostream instance that was given to this object at construction time.</summary>
                protected System.IO.StreamWriter ostream = null;

                /// <summary>Gives access to the flushAfterEachWrite flag value that was original assigned at construction time.</summary>
                protected bool flushAfterEachWrite = false;
            };

            //-------------------------------------------------------------------
            #endregion

            //-------------------------------------------------------------------

            #region Basic LogMessageHandler Implementation classes (ConsoleLogMesssageHandler, SimpleFileLogMessageHandler, Win32DebugLogMessageHandler, DiagnosticTraceLogMessageHandler, NullLogMessageHandler)

            /// <summary>
            /// This class provides the standard implementation for a Console Output LMH.
            /// It is based on StreamWriterLogMessageHandlerBase used with the Console.OpenStandardOutput() method.
            /// This version always calls Flush after each write.
            /// </summary>
            public class ConsoleLogMesssageHandler : StreamWriterLogMessageHandlerBase
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="name">Defines the LMH name.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                /// <param name="lineFmt">Gives the line formatting rules that will be used for this LMH</param>
                public ConsoleLogMesssageHandler(string name, LogGate logGate, LineFormat lineFmt)
                    : base(name, logGate, lineFmt, new System.IO.StreamWriter(Console.OpenStandardOutput()), true)
                { }
            }

            //-------------------------------------------------------------------
            /// <summary>
            /// This class provides the standard implementation for a simple text file output LMH.
            /// It is based on StreamWriterLogMessageHandlerBase used with a locally opened/created StreamWriter
            /// </summary>
            public class SimpleFileLogMessageHandler : StreamWriterLogMessageHandlerBase
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="filePath">Gives the path to the file name to which log messages will be appended.  Also used to name the LMH instance as "LMH." + filePath</param>
                /// <param name="includeFileAndLines">Indicates if this handler should record and include source file and line numbers.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                /// <param name="flushAfterEachWrite">Set to true to flush the ostream after each write, or false to Flush only when explicitly told to by the LogDistribution system.</param>
                public SimpleFileLogMessageHandler(string filePath, bool includeFileAndLines, LogGate logGate, bool flushAfterEachWrite = false)
                    : base("LMH." + filePath, logGate,
                            new LineFormat(true, true, true, true, includeFileAndLines),
                            new System.IO.StreamWriter(filePath, true, System.Text.Encoding.ASCII),
                            flushAfterEachWrite: flushAfterEachWrite)
                {
                    if (ostream.BaseStream.CanWrite)
                        ostream.Write("\r\n");

                    logger.Signif.Emit("----------------------------------------");
                    logger.Signif.Emit("LogMessageHandler starting: File has been opened");
                }

                /// <summary>
                /// Emits a set of messages to indicate that the file has been closed and then flushes and closes the file ostream
                /// </summary>
                public override void Shutdown()		// override default impl from CommonLogMessageHandlerBase
                {
                    logger.Signif.Emit("LogMessageHandler Shutdown");
                    logger.Signif.Emit("----------------------------------------");

                    base.Shutdown();
                }

                /// <summary>
                /// Emits a message to indicate that the handler has been re-started
                /// </summary>
                public override void StartIfNeeded()
                {
                    base.StartIfNeeded();

                    logger.Signif.Emit("----------------------------------------");
                    logger.Signif.Emit("LogMessageHandler has been re-started");
                }
            }

            //-------------------------------------------------------------------
            /// <summary>
            /// This class provides a simple implementation of an LMH that will format the message and
            /// emit it to the current debugger using OutputDebugString.
            /// </summary>
            public class Win32DebugLogMessageHandler : SimpleLogMessageHandlerBase
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="appName">Gives the name of the appication that will be used as a prefix for the debug log messages that are generated</param>
                /// <param name="name">Defines the LMH name.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                public Win32DebugLogMessageHandler(string appName, string name, LogGate logGate)
                    : base(name, logGate, recordSourceStackFrame: false)
                {
                    this.appName = appName;
                    this.lineFmt = new LineFormat(false, true, true, true, true, false, "\n", " ");
                    if (String.IsNullOrEmpty(appName))
                        Utils.Asserts.TakeBreakpointAfterFault("AppName is empty");
                }

                /// <summary>
                /// Takes the given LogMessage, formats it and writes it to the Win32 debug output using the originally given AppName as a prefix.
                /// </summary>
                protected override void InnerHandleLogMessage(LogMessage lm)
                {
                    System.Text.StringBuilder lineBuilder = new System.Text.StringBuilder();
                    lineBuilder.Append(appName);
                    lineBuilder.Append(" ");

                    lineFmt.FormatLogMessageToStringBuilder(lm, lineBuilder);

                    BasicFallbackLogging.OutputDebugString(lineBuilder.ToString());
                }

                string appName = null;
                LineFormat lineFmt = null;
            }

            //-------------------------------------------------------------------
            /// <summary>
            /// This class provides a simple implementation of an LMH that will format the message and
            /// emit it to the current diagnostic trace sink
            /// </summary>
            public class DiagnosticTraceLogMessageHandler : SimpleLogMessageHandlerBase
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="name">Defines the LMH name.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                /// <param name="lineFmt">Gives the line formatting rules that will be used for this LMH</param>
                public DiagnosticTraceLogMessageHandler(string name, LogGate logGate, LineFormat lineFmt)
                    : base(name, logGate, recordSourceStackFrame: false)
                {
                    this.lineFmt = lineFmt;
                }

                /// <summary>
                /// Takes the given LogMessage, formats it and writes it to the System.Diagnostics.Trace output as a TraceError, TraceWarning or TraceInformation message.
                /// </summary>
                protected override void InnerHandleLogMessage(LogMessage lm)
                {
                    lineBuilder.Length = 0;		// clear line builder

                    lineFmt.FormatLogMessageToStringBuilder(lm, lineBuilder);
                    string mesg = lineBuilder.ToString();

                    switch (lm.MesgType)
                    {
                        case MesgType.Fatal:
                        case MesgType.Error:
                            System.Diagnostics.Trace.TraceError(mesg);
                            break;
                        case MesgType.Warning:
                            System.Diagnostics.Trace.TraceWarning(mesg);
                            break;
                        case MesgType.Signif:
                        case MesgType.Info:
                        case MesgType.Debug:
                        case MesgType.Trace:
                        default:
                            System.Diagnostics.Trace.TraceInformation(mesg);
                            break;
                    }
                }

                LineFormat lineFmt = null;
                System.Text.StringBuilder lineBuilder = new System.Text.StringBuilder();
            }

            //-------------------------------------------------------------------
            /// <summary>
            /// This class implements a null LMH (ie it ignores all messages that are given to it
            /// </summary>
            public class NullLogMessageHandler : SimpleLogMessageHandlerBase
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="name">Defines the LMH name.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                /// <param name="includeFileAndLines">Indicates if this handler should record and include source file and line numbers.</param>
                public NullLogMessageHandler(string name, LogGate logGate, bool includeFileAndLines)
                    : base(name, logGate, recordSourceStackFrame: includeFileAndLines)
                { }

                /// <summary>
                /// Takes the given LogMessage and ignores it.
                /// </summary>
                protected override void InnerHandleLogMessage(LogMessage lm)
                { }
            };

            /// <summary>
            /// This class provides an ILogMessageHandler that can be used to accumulate log messages emitted through distribution.
            /// Its primary purpose is to be used in unit test cases.
            /// </summary>
            public class AccumulatingLogMessageHandler : CommonLogMessageHandlerBase
            {
                /// <summary>Basic constructor.</summary>
                /// <param name="name">Defines the LMH name.</param>
                /// <param name="logGate">Defines the LogGate value for messages handled here.  Messages with MesgType that is not included in this gate will be ignored.</param>
                /// <param name="recordSourceStackFrame">Indicates if this handler should record source stack frames.</param>
                public AccumulatingLogMessageHandler(string name, LogGate logGate, bool recordSourceStackFrame = true)
                    : base(name, logGate, recordSourceStackFrame: recordSourceStackFrame)
                { }

                /// <summary>
                /// Returns the current number of messages that have been accumulated
                /// </summary>
                public int Count { get { return lmListVolatileCount; } }

                /// <summary>
                /// Returns an array containing the LogMessages that have been accumulated at this point.
                /// If the take parameter is true then these messages will be removed from internal list otherwise these messages will remain in the internal list.
                /// </summary>
                /// <param name="take">Determines if this method will clear the list after generating the array of messages that are currently in the list.</param>
                /// <returns></returns>
                public LogMessage [] GetAccumulatedLogMessages(bool take = true)
                {
                    lock (lmListMutex)
                    {
                        LogMessage[] lmArray = lmList.ToArray();
                        if (take)
                            lmList.Clear();

                        lmListVolatileCount = lmList.Count;

                        return lmArray;
                    }
                }

                private readonly object lmListMutex = new object();
                private List<LogMessage> lmList = new List<LogMessage>();
                private volatile int lmListVolatileCount = 0;

                /// <summary>LogMessage Handling method API for direct call by clients which generate and distribute one message at a time.</summary>
                /// <param name="lm">
                /// Gives the message to handle (save, write, relay, ...).
                /// LMH Implementation must either support Reference Counted message semenatics if method will save any reference to a this message for use beyond the scope of this call or
                /// LMH must flag that it does not support Reference counted semantics in LoggerConfig by clearing the SupportReferenceCountedRelease
                /// </param>
                public override void HandleLogMessage(LogMessage lm)
                {
                    if (!IsMessageTypeEnabled(lm))
                        return;

                    lock (lmListMutex)
                    {
                        lmList.Add(lm);
                        lmListVolatileCount = lmList.Count;
                    }

                    NoteMessagesHaveBeenDelivered();
                }

                /// <summary>LogMessage Handling method API for use on outputs of queued delivery engines which agregate and distribute one or more messages at a time.</summary>
                /// <param name="lmArray">
                /// Gives an array of message to handle as a set (save, write, relay, ...).  This set may be given to muliple handlers and as such may contain messages that are not relevant to this handler.
                /// As such Handler may additionally filter out or skip any messages from the given set as approprate.
                /// LMH Implementation must flag that it does not support Reference counted semantics in LoggerConfig or must support ReferenceCounting on this message if references to it are saved beyond the scope of this call.
                /// </param>
                public override void HandleLogMessages(LogMessage[] lmArray)
                {
                    int lmArrayLength = lmArray.Length;
                    for (int idx = 0; idx < lmArrayLength; idx++)
                    {
                        if (!IsMessageTypeEnabled(lmArray[idx]))
                            lmArray[idx] = null;
                    }

                    lock (lmListMutex)
                    {
                        lmList.AddRange(lmArray.Where(lm => lm != null));
                        lmListVolatileCount = lmList.Count;
                    }

                    NoteMessagesHaveBeenDelivered();
                }
            }

            #endregion

            //-------------------------------------------------------------------
        }

        #region helper method to generate default file header line arrays

        /// <summary>
        /// This method may be used to generate a basic set of fixed header lines for use with configurable log message handlers that support them.
        /// The resulting array consisists of a pattern that looks like:
        /// <para/>================================================================================================================================
        /// <para/>Log file for 'logBaseName'
        /// <para/>Hosting Assembly: 'HostingAssemblyName (callerProvidedAssemblyName)'
        /// <para/>Main Assembly: 'MainAssemblyName'
        /// <para/>Process name:'GetCurrentPerocess.ProcessName' id:GetCurrentProcess.Id 32bit
        /// <para/>Machine:'machineName' os:'os name and version' 64bit Cores:'nunCores' PageSize:pageSize
        /// <para/>User:'userName' {interactive|service}
        /// <para/>[optional null]
        /// <para/>================================================================================================================================
        /// </summary>

        public static string[] GenerateDefaultHeaderLines(string logBaseName = null, bool includeNullForDynamicLines = false, System.Reflection.Assembly hostingAssembly = null)
        {
            appUptimeBaseTimeStamp = QpcTimeStamp.Now;

            System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            System.Reflection.Assembly mainAssembly = System.Reflection.Assembly.GetEntryAssembly();

            string[] deliniatorLineArray = new string[] { "================================================================================================================================" };

            string[] defaultFileHeaderLines1 = new string[]
            {
                ((logBaseName != null) ? "Log file for '{0}'".CheckedFormat(logBaseName) : null),
                ((hostingAssembly != null) ? "Hosting Assembly: '{0}'".CheckedFormat(hostingAssembly) : null),
                ((mainAssembly != null && mainAssembly != hostingAssembly) ? "Main Assembly: '{0}'".CheckedFormat(mainAssembly) : null),
                "Process name:'{0}' id:{1} {2}".CheckedFormat(currentProcess.ProcessName, currentProcess.Id, Environment.Is64BitProcess ? "64-bit" : "32-bit"),
                "Machine:'{0}' os:'{1}'{2} Cores:{3} PageSize:{4}".CheckedFormat(Environment.MachineName, Environment.OSVersion, Environment.Is64BitOperatingSystem ? " 64-bit" : "", Environment.ProcessorCount, Environment.SystemPageSize),
                "User:'{0}' {1}".CheckedFormat(Environment.UserName, Environment.UserInteractive ? "interactive" : "service"),
            }.Where(s => !s.IsNullOrEmpty()).ToArray();

            if (!includeNullForDynamicLines)
                return deliniatorLineArray.Concat(defaultFileHeaderLines1).Concat(deliniatorLineArray).ToArray();
            else
                return deliniatorLineArray.Concat(defaultFileHeaderLines1).Concat(new string[] { null }).Concat(deliniatorLineArray).ToArray();
        }

        static QpcTimeStamp appUptimeBaseTimeStamp = QpcTimeStamp.Now;

        /// <summary>
        /// This method may be used to generate the dynamic header lines, typically combined with the use of GenerateDefaultHeaderLines.
        /// The resulting array looks like:
        /// <para/>Uptime: 1.001 hours [only included if time >= 0.001 hours]
        /// </summary>
        public static string[] GenerateDynamicHeaderLines()
        {
            double appUptimeHours = appUptimeBaseTimeStamp.Age.TotalHours;
            double roundedAppUptimeHours = appUptimeHours.Round(4, MidpointRounding.ToEven);

            string [] dynamicHeaderLinesArray = new string[]
            {
                ((appUptimeHours > 0.0005) ? "Uptime: {0:f3} hours".CheckedFormat(appUptimeHours) : null),
            }.Where(s => s != null).ToArray();

            return dynamicHeaderLinesArray;
        }

        #endregion
    }
}
