//-------------------------------------------------------------------
/*! @file LogMessageHandler.cs
 * This file defines the public interface and factory methods for
 * the supported types of LogMessageHandlers.  It also contains
 * the implementation class(s) for some of these types.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version)
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
//-------------------------------------------------------------------

namespace MosaicLib
{
	using System;
	using System.Collections.Generic;

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

            /// <summary>LogMessage Handling method API for direct call by clients which generate and distribute one message at a time.</summary>
            /// <param name="lm">
            /// Gives the message to handle (save, write, relay, ...).
            /// LMH Implementation must either support Reference Counted message semenatics if method will save any reference to a this message for use beyond the scope of this call or
            /// LMH must flag that it does not support Reference counted semantics in LoggerConfig by clearing the SupportReferenceCountedRelease
            /// </param>
            void HandleLogMessage(LogMessage lm);

            /// <summary>LogMessage Handling method API for use on outputs of queued delivery engines which agregate and distribute one or more messages at a time.</summary>
            /// <param name="lmArray">
            /// Gives an array of message to handle as a set (save, write, relay, ...).  This set may be given to muliple handlers and as such may contain messages that are not relevant to this handler.
            /// As such Handler may additionally filter out or skip any messages from the given set as approprate.
            /// LMH Implementation must flag that it does not support Reference counted semantics in LoggerConfig or must support ReferenceCounting on this message if references to it are saved beyond the scope of this call.
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
		}

		#endregion

		//-------------------------------------------------------------------
		#region LogMessageHandler factory classes and methods.

		// the following methodes provide globally usable factory methods for creating different types of
		//	log message handler objects that may be used with the log distribution system.

		public static ILogMessageHandler CreateConsoleLogMessageHandler() { return CreateConsoleLogMessageHandler("LMH.Console", LogGate.All); }
		public static ILogMessageHandler CreateConsoleLogMessageHandler(LogGate logGate) { return CreateConsoleLogMessageHandler("LMH.Console", logGate); }
		public static ILogMessageHandler CreateConsoleLogMessageHandler(string name) { return CreateConsoleLogMessageHandler(name, LogGate.All); }
		public static ILogMessageHandler CreateConsoleLogMessageHandler(string name, LogGate logGate)
		{
			return new Handlers.ConsoleLogMesssageHandler(name, logGate, new Handlers.LineFormat(false, true, true, " "));
		}

		public static ILogMessageHandler CreateSimpleFileLogMessageHandler(string filePath) { return CreateSimpleFileLogMessageHandler(filePath, false, LogGate.All); }
		public static ILogMessageHandler CreateSimpleFileLogMessageHandler(string filePath, LogGate logGate) { return CreateSimpleFileLogMessageHandler(filePath, false, logGate); }
		public static ILogMessageHandler CreateSimpleFileLogMessageHandler(string filePath, bool includeFileAndLines) { return CreateSimpleFileLogMessageHandler(filePath, includeFileAndLines, LogGate.All); }
		public static ILogMessageHandler CreateSimpleFileLogMessageHandler(string filePath, bool includeFileAndLines, LogGate logGate)		//!< lmh name is "LMH." + filePath
		{
			return new Handlers.SimpleFileLogMessageHandler(filePath, includeFileAndLines, logGate);
		}

		public static ILogMessageHandler CreateWin32DebugLogMessageHandler(string appName) { return CreateWin32DebugLogMessageHandler(appName, "LMH.Debug", LogGate.All); }
		public static ILogMessageHandler CreateWin32DebugLogMessageHandler(string appName, LogGate logGate) { return CreateWin32DebugLogMessageHandler(appName, "LMH.Debug", logGate); }
		public static ILogMessageHandler CreateWin32DebugLogMessageHandler(string appName, string name) { return CreateWin32DebugLogMessageHandler(appName, name, LogGate.All); }
		public static ILogMessageHandler CreateWin32DebugLogMessageHandler(string appName, string name, LogGate logGate)	//!< appName is used to prefix all messages that are sent to the debugger
		{
			return new Handlers.Win32DebugLogMessageHandler(appName, name, logGate);
		}

		public static ILogMessageHandler CreateDiagnosticTraceLogMessageHandler() { return CreateDiagnosticTraceLogMessageHandler("LMH.DiagnosticTrace", LogGate.All); }
		public static ILogMessageHandler CreateDiagnosticTraceLogMessageHandler(LogGate logGate) { return CreateDiagnosticTraceLogMessageHandler("LMH.DiagnosticTrace", logGate); }
		public static ILogMessageHandler CreateDiagnosticTraceLogMessageHandler(string name) { return CreateDiagnosticTraceLogMessageHandler(name, LogGate.All); }
		public static ILogMessageHandler CreateDiagnosticTraceLogMessageHandler(string name, LogGate logGate)
		{
			return new Handlers.DiagnosticTraceLogMessageHandler(name, logGate, new Handlers.LineFormat(false, true, true, true, true, false, "", " "));
		}

		public static ILogMessageHandler CreateNullLogMessageHandler() { return CreateNullLogMessageHandler("LMH.Null", LogGate.None); }
		public static ILogMessageHandler CreateNullLogMessageHandler(LogGate logGate) { return CreateNullLogMessageHandler("LMH.Null", logGate); }
		public static ILogMessageHandler CreateNullLogMessageHandler(string name) { return CreateNullLogMessageHandler(name, LogGate.None); }
		public static ILogMessageHandler CreateNullLogMessageHandler(string name, LogGate logGate)
		{
			return new Handlers.NullLogMessageHandler(name, logGate, false);
		}

		//-------------------------------------------------------------------

        /// <summary>The standard default mesg queue size: 5000</summary>
		const int DefaultMesgQueueSize = 5000;

		public static ILogMessageHandler CreateQueueLogMessageHandler(ILogMessageHandler targetLMH) { return CreateQueueLogMessageHandler(targetLMH, DefaultMesgQueueSize); }
		public static ILogMessageHandler CreateQueueLogMessageHandler(ILogMessageHandler targetLMH, int maxQueueSize)
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
            /// <summary>The desired logGate.  Defaults to LogGate.None unless overriden in constructor</summary>
            public LogGate logGate;

            /// <summary>true for date_time in middle of name, false for 5 digits in middle of name.  files are named "[name][date_time].log" or "[name][nnnnn].log"</summary>
            public bool nameUsesDateAndTime;

            /// <summary>Defines the set of values that determine when the ring must advance to the next file.</summary>
            public struct AdvanceRules
			{
                /// <summary>the maximum desired size of each file (0 for no limit)</summary>
                public int fileSizeLimit;
                /// <summary>file age limit in seconds (0 for no limit)</summary>
                public double fileAgeLimitInSec;
                /// <summary>file age limit (TimeSpan.Zero for no limit)</summary>
                public TimeSpan FileAgeLimit { get { return TimeSpan.FromSeconds(fileAgeLimitInSec); } set { fileAgeLimitInSec = value.TotalSeconds; } }
                /// <summary>the period after checking the active file's size before it will be checked again.  Leave as zero for calculation based on fileAgeLimitInSec</summary>
                public double testPeriodInSec;
                /// <summary>the period after checking the active file's size before it will be checked again.  Leave as TimeSpan.Zero for calculation based on fileAgeLimitInSec</summary>
                public TimeSpan TestPeriod { get { return TimeSpan.FromSeconds(testPeriodInSec); } set { testPeriodInSec = value.TotalSeconds; } }
            }
            /// <summary>Defines the set of values that determine when the ring must advance to the next file.</summary>
            public AdvanceRules advanceRules;

            /// <summary>Defines the set of values that determine when old files in the ring must be purged/deleted.</summary>
            public struct PurgeRules
			{
                /// <summary>the user stated maximum number of files (or zero for no limit).  Must be 0 or be between 2 and 5000</summary>
                public int dirNumFilesLimit;
                /// <summary>the user stated maximum size of the set of managed files or zero for no limit</summary>
                public long dirTotalSizeLimit;
                /// <summary>the user stated maximum age in seconds of the oldest file or zero for no limit</summary>
                public double fileAgeLimitInSec;
                /// <summary>the user stated maximum age in seconds of the oldest file or zero for no limit (TimeSpan.Zero for no limit)</summary>
                public TimeSpan FileAgeLimit { get { return TimeSpan.FromSeconds(fileAgeLimitInSec); } set { fileAgeLimitInSec = value.TotalSeconds; } }
            }
            /// <summary>Defines the set of values that determine when old files in the ring must be purged/deleted.</summary>
            public PurgeRules purgeRules;

            /// <summary>only used by queued file rotation type log message handlers.  Defines the maximum number of messages that can be enqueued before being dropped or processed.</summary>
            public int mesgQueueSize;

            /// <summary>Set to true to capture and include the message source file name and line number</summary>
            public bool includeFileAndLine;
            /// <summary>Set to true to include the QpcTime in the output text.</summary>
            public bool includeQpcTime;

            /// <summary>A list of strings for file names that will not be tracked or purged by the ring.</summary>
            public List<string> excludeFileNamesSet;

            /// <summary>Set to true if the ring is expected to create the directory if needed or if it should disable logging if the directory does not exist.</summary>
            public bool createDirectoryIfNeeded;

            /// <summary>Simple constructor: applyies LogGate.All and other default settings of 2000 files in ring, 1000000 byes per file, DefaultMesgQueueSize.</summary>
            public FileRotationLoggingConfig(string name, string dirPath) : this(name, dirPath, LogGate.All) { }
            /// <summary>Simple constructor: uses default settings of 2000 files in ring, 1000000 byes per file, DefaultMesgQueueSize</summary>
            public FileRotationLoggingConfig(string name, string dirPath, LogGate logGate) : this(name, dirPath, logGate, 2000, 1000000, DefaultMesgQueueSize) { }
            /// <summary>Standard constructor</summary>
            public FileRotationLoggingConfig(string name, string dirPath, LogGate logGate, int maxFilesInRing, int advanceAfterFileSize, int mesgQueueSize) : this()
			{
				this.name = name;
				this.dirPath = dirPath;
				this.logGate = logGate;
				this.mesgQueueSize = mesgQueueSize;
				this.nameUsesDateAndTime = true;
				this.includeFileAndLine = true;
				this.includeQpcTime = true;

				this.advanceRules = new AdvanceRules();
				this.purgeRules = new PurgeRules();

				purgeRules.dirNumFilesLimit = maxFilesInRing;
				advanceRules.fileSizeLimit = advanceAfterFileSize;
				advanceRules.fileAgeLimitInSec = 24.0 * 3600.0;		// 1 day

				excludeFileNamesSet = new List<string>();

                createDirectoryIfNeeded = true;
			}
		}

		public static ILogMessageHandler CreateQueuedTextFileRotationDirectoryLogMessageHandler(FileRotationLoggingConfig config)
		{
			return new Handlers.QueueLogMessageHandler(new Handlers.TextFileRotationLogMessageHandler(config), config.mesgQueueSize);
		}

		#endregion

		//-------------------------------------------------------------------

		#endregion

		//-------------------------------------------------------------------
	}

	public static partial class Logging
	{
		public static partial class Handlers
		{
			//-------------------------------------------------------------------
			#region LogMessageHandler base classes

			//-------------------------------------------------------------------

			static readonly string defaultEolStr = "\r\n";
			static readonly string defaultTabStr = "\t";

			/// <summary>
			/// This class provides a simple type of line formatter and writer that is used with various types of
			/// LogMessageHandler.  It primarily provides means for a hanlder to define which fields of a log message
			/// are to be included in the handler's output and to perform output for that handler.  As such both
			/// format to ostream and format to string versions are provided.
			/// </summary>

			public class LineFormat
			{
				public LineFormat(bool date, bool qpc, bool source) : this(date, qpc, true, source, true, false, defaultEolStr, defaultTabStr) { }
				public LineFormat(bool date, bool qpc, bool source, string tabStr) : this(date, qpc, true, source, true, false, defaultEolStr, tabStr) { }
				public LineFormat(bool date, bool qpc, bool level, bool source, bool fandl) : this(date, qpc, level, source, true, fandl, defaultEolStr, defaultTabStr) { }
				public LineFormat(bool date, bool qpc, bool level, bool source, bool data, bool fandl, string endlStr, string tabStr)
				{
					this.date = date;
					this.qpc = qpc;
					this.level = level;
					this.source = source;
					this.data = data;
					this.fAndL = fandl;
					this.endLStr = endlStr;
					this.tabStr = tabStr;
				}

				public bool IncludeDate { get { return date; } }
				public bool IncludeQpc { get { return qpc; } }
				public bool IncludeLevel { get { return level; } }
				public bool IncludeSource { get { return source; } }
				public bool IncludeFileAndLine { get { return fAndL; } }

				public void FormatLogMessageToOstream(LogMessage lm, System.IO.StreamWriter os)
				{
					if (!os.BaseStream.CanWrite)
						return;

					bool firstItem = true;

					if (date) { TabIfNeeded(os, ref firstItem); os.Write(lm.GetFormattedDateTime()); }
					if (qpc) { TabIfNeeded(os, ref firstItem); os.Write((lm.EmittedQpcTime.Time % 1000.0).ToString("000.000000")); }
					if (level) { TabIfNeeded(os, ref firstItem); os.Write(ConvertToFixedWidthString(lm.MesgType)); }
					if (source) { TabIfNeeded(os, ref firstItem); os.Write(lm.LoggerName); }
					{ TabIfNeeded(os, ref firstItem); os.Write(lm.Mesg); }
					{ TabIfNeeded(os, ref firstItem); os.Write(lm.Keywords); }
					if (data) { TabIfNeeded(os, ref firstItem); os.Write("[{0}]", base64UrlCoder.Encode(lm.Data)); }

					if (fAndL && lm.SourceStackFrame != null)
					{
						{ os.Write(tabStr); os.Write(lm.SourceStackFrame.GetFileName()); }
						{ os.Write(tabStr); os.Write(lm.SourceStackFrame.GetFileLineNumber().ToString()); }
					}
					{ os.Write(endLStr); }
				}

				public void FormatLogMessageToStringBuilder(LogMessage lm, System.Text.StringBuilder ostr)
				{
					bool firstItem = (ostr.Length == 0);

					if (date) { TabIfNeeded(ostr, ref firstItem); ostr.Append(lm.GetFormattedDateTime()); }
					if (qpc) { TabIfNeeded(ostr, ref firstItem); ostr.Append((lm.EmittedQpcTime.Time % 1000.0).ToString("000.000000")); }
					if (level) { TabIfNeeded(ostr, ref firstItem); ostr.Append(ConvertToFixedWidthString(lm.MesgType)); }
					if (source) { TabIfNeeded(ostr, ref firstItem); ostr.Append(lm.LoggerName); }
					{ TabIfNeeded(ostr, ref firstItem); ostr.Append(lm.Mesg); }
                    { TabIfNeeded(ostr, ref firstItem); ostr.Append(lm.Keywords); }
					if (data) { TabIfNeeded(ostr, ref firstItem); ostr.Append("["); ostr.Append(base64UrlCoder.Encode(lm.Data)); ostr.Append("]"); }

					if (fAndL && lm.SourceStackFrame != null)
					{
						{ ostr.Append(tabStr); ostr.Append(lm.SourceStackFrame.GetFileName()); }
						{ ostr.Append(tabStr); ostr.Append(lm.SourceStackFrame.GetFileLineNumber().ToString()); }
					}
					{ ostr.Append(endLStr); }
				}

				protected void TabIfNeeded(System.IO.StreamWriter os, ref bool firstItem)
				{
					if (!firstItem)	os.Write(tabStr);
					else			firstItem = false;
				}

				protected void TabIfNeeded(System.Text.StringBuilder ostr, ref bool firstItem) 
				{
					if (!firstItem)	ostr.Append(tabStr);
					else			firstItem = false;
				}

				private bool date, qpc, source, level, data, fAndL;
				private string endLStr = defaultEolStr;
				private string tabStr = defaultTabStr;
				private readonly Utils.IByteArrayTranscoder base64UrlCoder = Utils.ByteArrayTranscoders.Base64UrlTranscoder;
			}

			//-------------------------------------------------------------------

			public class LogMesgHandlerLogger : LoggerBase
			{
				public LogMesgHandlerLogger(ILogMessageHandler logMesgHandler) : base(logMesgHandler.Name, string.Empty, logMesgHandler.LoggerConfig.LogGate, false)
				{
					lmh = logMesgHandler;
                    if (lmh == null)
    					Utils.Asserts.TakeBreakpointAfterFault("LogMesgHandlerLogger: LMH == null");

					sourceInfo = new LoggerSourceInfo(LoggerID_InternalLogger, Name);
				}

				public override LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame, bool getFromDistribution)
				{
					return base.GetLogMessage(mesgType, mesg, sourceStackFrame, false);
				}

				public override void EmitLogMessage(ref LogMessage lm)				//!< Emits and consumes the message (lm will be set to null)
				{
					if (lm != null && loggerHasBeenShutdown)
					{
						lm.NoteEmitted();

                        if (!lmh.LoggerConfig.SupportsReferenceCountedRelease && lm.BelongsToPool)
                            dist.ReallocateMessageForNonRefCountedHandler(ref lm);

                        lmh.HandleLogMessage(lm);
						lm.RemoveReference(ref lm);
					}
				}

				public override bool WaitForDistributionComplete(TimeSpan timeLimit) { return true; }

				protected ILogMessageHandler lmh = null;

				protected override string ClassName { get { return "LogMesgHandlerLogger"; } }
			}

			//-------------------------------------------------------------------
			/// <summary>
			/// This class implements the basic and common parts of the ILogMessageHandler interface, especially those
			/// parts that can be done easily and in a non-use specific manner.
			/// </summary>

			public abstract class CommonLogMessageHandlerBase : Utils.DisposableBase, ILogMessageHandler
			{
				public CommonLogMessageHandlerBase(string name, LogGate logGate, bool includeFileAneLine, bool supportReferenceCountedRelease) 
				{
					this.name = name;
                    loggerConfig = new LoggerConfig() { GroupName = "LMH:" + name, LogGate = logGate, RecordSourceStackFrame = includeFileAneLine, SupportsReferenceCountedRelease = supportReferenceCountedRelease };

					logger = new LogMesgHandlerLogger(this);
				}

				public string Name { get { return name; } }
				public LogGate LogGate { get { return loggerConfig.LogGate; } }
				public LoggerConfig LoggerConfig { get { return loggerConfig; } }

				public Utils.IBasicNotificationList NotifyOnCompletedDelivery { get { return notifyMessageDelevered; } }

				public abstract void HandleLogMessage(LogMessage lm);
				public abstract void HandleLogMessages(LogMessage [] lmArray);

				public abstract bool IsMessageDeliveryInProgress(int testMesgSeqNum);

				public abstract void Flush();

				public abstract void Shutdown();

				protected void NoteMessagesHaveBeenDelivered()
				{
					// tell any interested Notifyable objects that messages have been delivered

					notifyMessageDelevered.Notify();
				}

				protected bool IsMessageTypeEnabled(LogMessage lm)
				{
					return ((lm != null) ? loggerConfig.IsTypeEnabled(lm.MesgType) : false);
				}

				protected override void Dispose(MosaicLib.Utils.DisposableBase.DisposeType disposeType)
				{
					if (disposeType == DisposeType.CalledExplicitly)
						Shutdown();
				}

				// all LMH implementations need to know their name and need a pointer to the 
				//	source ID that they are to use when generating their own messages (many of them
				//	need to do this...)

				protected string name = null;
				protected LoggerConfig loggerConfig = LoggerConfig.AllNoFL;
				protected ILogger logger = null;

				Utils.BasicNotificationList notifyMessageDelevered = new MosaicLib.Utils.BasicNotificationList();
			}

			//-------------------------------------------------------------------
			/// <summary>
			/// This class implements additional common functionality that is usefull to help implement
			/// simple LogMessageHandler classes.
			/// </summary>
			/// <remarks>
			/// This class implements both the HandleLogMessage and HandleLogMessages methods by
			/// requiring the derived class to implement a common internal method called InnerHandleLogMessage.
			/// 
			/// This is a useful simplification for some types of message handlers (but typically not queued ones)
			/// 
			/// This class implements null versions of the IsMessageDeliveryInProgress and Flush methods and
			/// provides a simple implementation of the Shutdown method.  These are declared virtual so that derived
			/// classes can override the basic behavior provided here.
			/// </remarks>

			public abstract class SimpleLogMessageHandlerBase : CommonLogMessageHandlerBase
			{
                public SimpleLogMessageHandlerBase(string name, LogGate logGate, bool includeFileAndLine, bool supportReferenceCountedRelease) : base(name, logGate, includeFileAndLine, supportReferenceCountedRelease) { }

				public override void HandleLogMessage(LogMessage lm)
				{
					if (!IsMessageTypeEnabled(lm))
						return;

					InnerHandleLogMessage(lm);

					NoteMessagesHaveBeenDelivered();
				}

				public override void HandleLogMessages(LogMessage [] lmArray)
				{
					foreach (LogMessage lm in lmArray)
					{
						if (IsMessageTypeEnabled(lm))
							InnerHandleLogMessage(lm);
					}

					NoteMessagesHaveBeenDelivered();
				}

				public override bool IsMessageDeliveryInProgress(int testMesgSeqNum) { return false; }

				public override void Flush() { }

				public override void Shutdown() { Flush(); }

				protected abstract void InnerHandleLogMessage(LogMessage lm);							
			}

			//-------------------------------------------------------------------
			/// <summary>
			/// This class is a reasonable base class for all StreamWriter type LogMessageHandlers
			/// </summary>

			public class StreamWriterLogMessageHandlerBase : SimpleLogMessageHandlerBase
			{
				public StreamWriterLogMessageHandlerBase(string name, LogGate logGate, LineFormat lineFmt, System.IO.StreamWriter ostream, bool flushAfterEachWrite) 
					: base(name, logGate, lineFmt.IncludeFileAndLine, true)
				{
					this.lineFmt = lineFmt;
					this.ostream = ostream;
					this.flushAfterEachWrite = flushAfterEachWrite;

                    if (lineFmt == null)
                        Utils.Asserts.TakeBreakpointAfterFault("LineFmt is null");
                    if (ostream == null)
                        Utils.Asserts.TakeBreakpointAfterFault("ostream is null");
				}

				protected override void InnerHandleLogMessage(LogMessage lm)
				{
					lineFmt.FormatLogMessageToOstream(lm, ostream);
					if (flushAfterEachWrite)
						Flush();
				}

				public override void Flush()		// override default impl from CommonLogMessageHandlerBase
				{
					if (ostream.BaseStream.CanWrite)
						ostream.Flush();
				}

				public override void Shutdown()
				{
					base.Shutdown();

					if (ostream != null)
					{
						ostream.Close();
						ostream = null;
					}
				}

				protected LineFormat lineFmt = null;
				protected System.IO.StreamWriter ostream = null;
				protected bool flushAfterEachWrite = false;
			};

			//-------------------------------------------------------------------
			#endregion

			//-------------------------------------------------------------------

			#region Basic LogMessageHandler Implementation classes

			public class ConsoleLogMesssageHandler : StreamWriterLogMessageHandlerBase
			{
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
				public SimpleFileLogMessageHandler(string filePath, bool includeFileAndLines, LogGate logGate) 
					: base("LMH." + filePath, logGate, 
							new LineFormat(true, true, true, true, includeFileAndLines), 
							new System.IO.StreamWriter(filePath, true, System.Text.Encoding.ASCII),
							false)
				{
					if (ostream.BaseStream.CanWrite)
						ostream.Write("\r\n");

					logger.Signif.Emit("----------------------------------------");
					logger.Signif.Emit("File has been opened");
				}

				public override void Shutdown()		// override default impl from CommonLogMessageHandlerBase
				{
					logger.Signif.Emit("File is being closed");
					logger.Signif.Emit("----------------------------------------");

					ostream.Flush();
					ostream.Close();
				}
			}

			//-------------------------------------------------------------------
			/// <summary>
			/// This class provides a simple implementation of an LMH that will format the message and
			/// emit it to the current debugger using OutputDebugString.
			/// </summary>
			public class Win32DebugLogMessageHandler : SimpleLogMessageHandlerBase
			{
				public Win32DebugLogMessageHandler(string appName, string name, LogGate logGate) : base(name, logGate, false, true)
				{
					this.appName = appName;
					this.lineFmt = new LineFormat(false, true, true, true, true, false, "\n", " ");
                    if (String.IsNullOrEmpty(appName))
    					Utils.Asserts.TakeBreakpointAfterFault("AppName is empty");
				}

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
				public DiagnosticTraceLogMessageHandler(string name, LogGate logGate, LineFormat lineFmt)
					: base(name, logGate, false, true)
				{
					this.lineFmt = lineFmt;
				}

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
				public NullLogMessageHandler(string name, LogGate logGate, bool includeFileAndLine) : base(name, logGate, includeFileAndLine, true) { }

				protected override void InnerHandleLogMessage(LogMessage lm) { }
			};

			#endregion

			//-------------------------------------------------------------------
		}
	}
}
