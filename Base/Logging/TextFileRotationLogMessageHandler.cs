//-------------------------------------------------------------------
/*! @file TextFileRotationLogMessageHandler.cs
 *  @brief This file defines the LogMessageHandler implementation class that is responsible for implementing a form of log file ring based on creation of a ring of files in a directory.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2007 Mosaic Systems Inc.  (C++ library version)
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

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib
{
	public static partial class Logging
	{
		public static partial class Handlers
		{
            /// <summary>
            /// This class provides a Text file ring based <see cref="ILogMessageHandler"/> which makes use of a 
            /// <see cref="MosaicLib.File.DirectoryFileRotationManager"/> to manage a directory in which text log files are automatically
            /// created and removed so as to honor the size, count and age constraints from the <see cref="FileRotationLoggingConfig"/>
            /// which defines the values for the various parameters that are used to control the log file retention rules.
            /// </summary>
			public class TextFileRotationLogMessageHandler : CommonLogMessageHandlerBase
			{
				#region Public interface methods

                /// <summary>
                /// Constructor - <see cref="FileRotationLoggingConfig"/> parameter defines all initial values for the configuration and operation of this LMH.
                /// </summary>
				public TextFileRotationLogMessageHandler(FileRotationLoggingConfig frlConfig)
                    : base(frlConfig.name, frlConfig.logGate, recordSourceStackFrame: frlConfig.includeFileAndLine)
				{
					// replace the default LogMessageHandlerLogger with a normal QueuedLogger.  This is for use by all levels of this LMH type.
                    //  this allows generated messages to be inserted into and handled by the entire distribution system rather than just by this LMH instance.
					logger = new QueuedLogger(Name, LogGate);

					config = frlConfig;
					dirMgr = new File.DirectoryFileRotationManager(config.name);
                    lineFmt = new LineFormat(true, config.includeQpcTime, true, true, true, config.includeFileAndLine, "\r\n", "\t") { IncludeThreadInfo = config.includeThreadInfo, IncludeNamedValueSet = config.includeNamedValueSet };

					dirMgrConfig = new MosaicLib.File.DirectoryFileRotationManager.Config();

					dirMgrConfig.dirPath = config.dirPath;
					dirMgrConfig.fileNamePrefix = config.fileNamePrefix;
					dirMgrConfig.fileNameSuffix = config.fileNameSuffix;

					if (config.nameUsesDateAndTime)
						dirMgrConfig.fileNamePattern = File.DirectoryFileRotationManager.FileNamePattern.ByDate;
					else
                        dirMgrConfig.fileNamePattern = File.DirectoryFileRotationManager.FileNamePattern.Numeric4DecimalDigits;

					dirMgrConfig.excludeFileNamesSet = config.excludeFileNamesSet;

                    dirMgrConfig.advanceRules = config.advanceRules;
					double testPeriodInSec = config.advanceRules.testPeriodInSec;
					if (testPeriodInSec == 0.0)
						testPeriodInSec = Math.Min(10.0, config.advanceRules.fileAgeLimitInSec / 3.0);
					dirMgrConfig.advanceRules.testPeriodInSec = testPeriodInSec;

                    dirMgrConfig.purgeRules = config.purgeRules;

					dirMgrConfig.enableAutomaticCleanup = true;
					dirMgrConfig.maxAutoCleanupDeletes = 200;
					dirMgrConfig.createDirectoryIfNeeded = config.createDirectoryIfNeeded;

					SetupDirMgr();
				}

                /// <summary>LogMessage Handling method API for direct call by clients which generate and distribute one message at a time.</summary>
				public override void HandleLogMessage(LogMessage lm)
				{
					if (!IsMessageTypeEnabled(lm))
						return;

					if (!StartFileAccess() || ostream == null)
					{
						droppedMessageCount++;
						return;
					}

					if (droppedMessageCount != lastLoggedDroppedMessageCount)
						LogDroppedMessageCount();

					lineFmt.FormatLogMessageToOstream(lm, ostream);
					handledLogMessageCounter++;

					CompleteFileAccess();
				}

                /// <summary>LogMessage Handling method API for use on outputs of queued delivery engines which agregate and distribute one or more messages at a time.</summary>
                public override void HandleLogMessages(LogMessage[] lmArray)
				{
					if (!StartFileAccess() || ostream == null)
					{
						droppedMessageCount += lmArray.Length;
						return;
					}

					if (droppedMessageCount != lastLoggedDroppedMessageCount)
						LogDroppedMessageCount();

					foreach (LogMessage lm in lmArray)
					{
						if (IsMessageTypeEnabled(lm))
						{
							lineFmt.FormatLogMessageToOstream(lm, ostream);
							handledLogMessageCounter++;
						}
					}

					CompleteFileAccess();
				}

                /// <summary>Once called, this method only returns after the handler has made a reasonable effort to verify that all outsanding, pending messages, visible at the time of the call, have been full processed before the call returns.</summary>
                public override void Flush()
				{
					if (ostream != null && ostream.BaseStream.CanWrite)
						ostream.Flush();
				}

                /// <summary>
                /// Used to tell the handler that the LogMessageDistribution system is shuting down.  
                /// Handler is expected to close, release and/or Dispose of any unmanged resources before returning from this call.  
                /// Any attempt to pass messages after this point may be ignored by the handler.
                /// </summary>
                /// <remarks>This method calls <see cref="CloseFile"/></remarks>
                public override void Shutdown()
				{
					CloseFile();
				}

				#endregion

				#region private/protected methods

                /// <summary>
                /// This property returns true if the underlying <see cref="MosaicLib.File.DirectoryFileRotationManager"/> is not currently usable and requires that it be successfully
                /// setup before it can be used to manage a file ring.  If the initial setup attempt fails then this method will only allow it to be retried every 30 seconds.
                /// </summary>
				protected bool IsDirMgrSetupNeeded 
				{ 
					get
					{
						if (dirMgr.IsDirectoryUsable)
							return false;

						if (lastSetupAttemptTime.IsZero)
							return true;

						const double retrySetupHoldoffPeriod = 30.0;
						double elapsedSinceLastSetupAttempt = (Time.QpcTimeStamp.Now - lastSetupAttemptTime).TotalSeconds;
						
						return (elapsedSinceLastSetupAttempt < 0.0 || elapsedSinceLastSetupAttempt >= retrySetupHoldoffPeriod);
					}
				}

                /// <summary>
                /// This method attempts to setup the <see cref="MosaicLib.File.DirectoryFileRotationManager"/> to manage the ring of log files.  This must be comleted successfully
                /// before this LMH can be used to write to any log file.
                /// </summary>
                /// <returns>True if the setup operation was successful</returns>
				protected bool SetupDirMgr()
				{
					lastSetupAttemptTime.SetToNow();

					bool success = false;

					try {
						success = dirMgr.Setup(dirMgrConfig);

						if (!success)
						{
							logger.Error.Emit("DirMgr '{0}' setup for path '{1}' failed:'{2}'", config.name, config.dirPath, dirMgr.LastFaultCode);
							return false;
						}
					}
					catch (System.Exception ex)
					{
						logger.Error.Emit("DirMgr '{0}' setup for path '{1}' threw exception:'{2}'", config.name, config.dirPath, ex.Message);
						return false;
					}

					if (!ActivateFile())
					{
						if (string.IsNullOrEmpty(activeFilePath))
							logger.Error.Emit("DirMgr '{0}' setup for path '{1}' gave empty active file path", config.name, config.dirPath);
						else
							logger.Error.Emit("DirMgr '{0}' setup for path '{1}' failed: failed to open active file '{2}'", config.name, config.dirPath, activeFilePath);

						return false;
					}

					return true;
				}

                /// <summary>
                /// Attempts to activate the current (or next) file in the directory manager and then open the append ostream for it.
                /// </summary>
                /// <returns>true if an active file name could be determined and the ostream could be successfully opened to append to it.</returns>
				protected bool ActivateFile()
                {
                    bool isAdvanceNeeded = dirMgr.IsFileAdvanceNeeded();
                    activeFilePath = (isAdvanceNeeded ? dirMgr.AdvanceToNextActiveFile() : dirMgr.PathToActiveFile);

                    if (string.IsNullOrEmpty(activeFilePath))
                        return false;

                    bool fileExists = System.IO.File.Exists(activeFilePath);

                    if (!fileExists)
                    {
                        System.IO.File.WriteAllBytes(activeFilePath, EmptyArrayFactory<byte>.Instance); // this makes certain the file has been created

                        // this needs to be here to prevent Win32 "tunneling" from preservering the creation time from the file we just deleted
                        System.IO.File.SetCreationTime(activeFilePath, DateTime.Now);
                    }

                    // establish the open mode.  if we are supposed to advance to a new file
                    //	 then make certain that we truncate the old file if it is still there.
                    bool append = !isAdvanceNeeded;

                    ostream = new System.IO.StreamWriter(activeFilePath, append, System.Text.Encoding.ASCII);

                    handledLogMessageCounter = 0;

                    if (ostream != null && !ostream.BaseStream.CanWrite)
                        CloseFile();

                    bool addHeader = (isAdvanceNeeded || !fileExists);

                    if (ostream != null && addHeader)
                    {
                        GenerateAndProduceHeaderLines(config.fileHeaderLines,
                                                      config.fileHeaderLinesDelegate,
                                                      (lm) =>
                                                      {
                                                          lineFmt.FormatLogMessageToOstream(lm, ostream);
                                                          handledLogMessageCounter++;
                                                      }
                        );
                    }

                    dirMgr.RefreshActiveFileInfo();

                    return (ostream != null);
                }

				const int MaxMessagesToHandleBeforeRescan = 100;

                /// <summary>
                /// Performs all actions required to start access to the active file.  This includes automatic attempts to reset the directory manager and
                /// performing incremental cleanup actions when needed.
                /// </summary>
                /// <returns>the results from calling <see cref="ActivateFile"/>.  true if an active file name could be determined and the ostream could be successfully opened to append to it.</returns>
				protected bool StartFileAccess()
				{
					bool dirMgrUsable = dirMgr.IsDirectoryUsable;
					if (!dirMgrUsable)
					{
						CloseFile();

						if (IsDirMgrSetupNeeded)
							dirMgrUsable = SetupDirMgr();

						if (!dirMgrUsable)
							return false;
					}

                    string cleanupNeededReason = dirMgr.DirectoryCleanupNeededReason;
                    if (!cleanupNeededReason.IsNullOrEmpty())
                        dirMgr.PerformIncrementalCleanup(cleanupNeededReason);

					bool recheckActiveFileSize = (handledLogMessageCounter >= MaxMessagesToHandleBeforeRescan);
					if (recheckActiveFileSize)
						handledLogMessageCounter = 0;

					bool fileAdvanceNeeded = dirMgr.IsFileAdvanceNeeded(recheckActiveFileSize);

					if (ostream != null)
					{
						if (fileAdvanceNeeded)
							CloseFile();
						else
							return true;
					}

					return ActivateFile();
				}

                /// <summary>
                /// Flushes the ostream.
                /// </summary>
				protected void CompleteFileAccess()
				{
					if (ostream != null)
					{
						Int64 pos = ostream.BaseStream.Position;

						if (ostream.BaseStream.CanWrite)
							ostream.Flush();
						else
							CloseFile();
					}

					NoteMessagesHaveBeenDelivered();
				}

                /// <summary>
                /// Closes the currently open ostream (if any).
                /// </summary>
				protected void CloseFile()
				{
					if (ostream != null)
					{
						ostream.Close();
						ostream = null;
					}
				}

                /// <summary>
                /// Logs a message to indicate how many messages this LMH has dropped because the directory manager is not in a usable state.
                /// </summary>
				protected void LogDroppedMessageCount()
				{
					if (droppedMessageCount == lastLoggedDroppedMessageCount)
						return;

					logger.Error.Emit("DirMgr '{0}' dropped {1} messages since last successful write", config.name, droppedMessageCount - lastLoggedDroppedMessageCount);

					lastLoggedDroppedMessageCount = droppedMessageCount;
				}

				#endregion

				#region private instance variables

				FileRotationLoggingConfig config;

				File.DirectoryFileRotationManager dirMgr = null;
				File.DirectoryFileRotationManager.Config dirMgrConfig = null;
				Time.QpcTimeStamp lastSetupAttemptTime = Time.QpcTimeStamp.Zero;
				string activeFilePath = string.Empty;

				LineFormat lineFmt = null;
				System.IO.StreamWriter ostream;
				int handledLogMessageCounter;		// used for heuristic update of file size based on number of messages written

				int droppedMessageCount = 0;
				int lastLoggedDroppedMessageCount = 0;

				#endregion

			}
		}
	}
}

//-------------------------------------------------------------------
