//-------------------------------------------------------------------
/*! @file TextFileRotationLogMessageHandler.cs
 * @brief This file defines the LogMessageHandler implementation class that is responsible for implementing a form of log file ring based on creation of a ring of files in a directory.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2007 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

	public static partial class Logging
	{
		public static partial class Handlers
		{
			public class TextFileRotationLogMessageHandler : CommonLogMessageHandlerBase
			{
				#region Public interface methods

				public TextFileRotationLogMessageHandler(FileRotationLoggingConfig frlConfig) 
					: base(frlConfig.name, frlConfig.logGate, frlConfig.includeFileAndLine, true)
				{
					// replace the default LogMessageHandlerLogger with a normal QueuedLogger.  This is for use by all levels of this LMH type.
					logger = new QueuedLogger(Name, LogGate, frlConfig.includeFileAndLine);

					config = frlConfig;
					dirMgr = new File.DirectoryFileRotationManager(config.name);
					lineFmt = new LineFormat(true, config.includeQpcTime, true, true, true, config.includeFileAndLine, "\r\n", "\t");

					dirMgrConfig = new MosaicLib.File.DirectoryFileRotationManager.Config();

					dirMgrConfig.dirPath = config.dirPath;
					dirMgrConfig.fileNamePrefix = config.name;
					dirMgrConfig.fileNameSuffix = ".log";

					if (config.nameUsesDateAndTime)
						dirMgrConfig.fileNamePattern = File.DirectoryFileRotationManager.FileNamePattern.ByDate;
					else
						dirMgrConfig.fileNamePattern = File.DirectoryFileRotationManager.FileNamePattern.Numeric4DecimalDigits;

					dirMgrConfig.excludeFileNamesSet = config.excludeFileNamesSet;

					dirMgrConfig.advanceRules.fileSizeLimit = config.advanceRules.fileSizeLimit;
					dirMgrConfig.advanceRules.fileAgeLimitInSec = config.advanceRules.fileAgeLimitInSec;
					double testPeriodInSec = config.advanceRules.testPeriodInSec;
					if (testPeriodInSec == 0.0)
						testPeriodInSec = Math.Min(10.0, config.advanceRules.fileAgeLimitInSec / 3.0);
					dirMgrConfig.advanceRules.testPeriodInSec = testPeriodInSec;

					dirMgrConfig.purgeRules.dirNumFilesLimit = config.purgeRules.dirNumFilesLimit;
					dirMgrConfig.purgeRules.dirTotalSizeLimit = config.purgeRules.dirTotalSizeLimit;
					dirMgrConfig.purgeRules.fileAgeLimitInSec = config.purgeRules.fileAgeLimitInSec;

					dirMgrConfig.enableAutomaticCleanup = true;
					dirMgrConfig.maxAutoCleanupDeletes = 200;
					dirMgrConfig.createDirectoryIfNeeded = config.createDirectoryIfNeeded;

					SetupDirMgr();
				}

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

				public override void  HandleLogMessages(LogMessage[] lmArray)
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

				public override bool IsMessageDeliveryInProgress(int testMesgSeqNum) { return false; }

				public override void Flush()
				{
					if (ostream != null && ostream.BaseStream.CanWrite)
						ostream.Flush();
				}

				public override void Shutdown()
				{
					CloseFile();
				}

				#endregion

				#region private methods

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
					catch (System.Exception e)
					{
						logger.Error.Emit("DirMgr '{0}' setup for path '{1}' threw exception:'{2}'", config.name, config.dirPath, e.Message);
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

				protected bool ActivateFile()
				{
					bool isAdvanceNeeded = dirMgr.IsFileAdvanceNeeded();
					activeFilePath = (isAdvanceNeeded ? dirMgr.AdvanceToNextActiveFile() : dirMgr.PathToActiveFile);

					if (string.IsNullOrEmpty(activeFilePath))
						return false;

					// establish the open mode.  if we are supposed to advance to a new file
					//	 then make certain that we truncate the old file if it is still there.
					bool append = !isAdvanceNeeded;
					
					ostream = new System.IO.StreamWriter(activeFilePath, append, System.Text.Encoding.ASCII);

					handledLogMessageCounter = 0;

					if (ostream != null && !ostream.BaseStream.CanWrite)
						CloseFile();

					return (ostream != null);
				}

				const int MaxMessagesToHandleBeforeRescan = 100;

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

					if (dirMgr.IsDirectoryCleanupNeeded)
						dirMgr.PerformIncrementalCleanup();

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

				protected void CloseFile()
				{
					if (ostream != null)
					{
						ostream.Close();
						ostream = null;
					}
				}

				protected void LogDroppedMessageCount()
				{
					if (droppedMessageCount == lastLoggedDroppedMessageCount)
						return;

					logger.Error.Emit("DirMgr '{0}' dropped {1} messages since last successfull write", config.name, droppedMessageCount - lastLoggedDroppedMessageCount);

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
