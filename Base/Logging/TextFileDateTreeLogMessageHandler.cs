//-------------------------------------------------------------------
/*! @file TextFileDateTreeLogMessagehandler.cs
 *  @brief This file defines the LogMessageHandler implementation class that is responsible for implementing a form of log file ring 
 *        based on creation of a date tree of text files with integrated pruning.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2016 Mosaic Systems Inc.
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
using System.IO;

using MosaicLib.File;
using MosaicLib.Modular.Config;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib
{
	public static partial class Logging
	{
		public static partial class Handlers
		{
            /// <summary>
            /// This class provides a Text file date tree based <see cref="ILogMessageHandler"/> which makes use of a 
            /// <see cref="MosaicLib.File.DirectoryTreePruningManager"/> to manage a directory tree in which text log files are automatically
            /// created and removed so as to honor the size, count and age constraints from the <see cref="MosaicLib.File.DirectoryTreePruningManager.Config"/>
            /// which defines the values for the various parameters that are used to control the log file retention rules.
            /// </summary>
			public class TextFileDateTreeLogMessageHandler : CommonLogMessageHandlerBase
            {
                #region Config

                /// <summary>
                /// Configuration object for TextFileDateTreeLogMessageHandler (and for typeical QueueLogMessageHandler that it is used with).
                /// This object support manual and optional modular config based overrides on the settings for nearly every operational detail on the use of this LogMessageHandler class.
                /// </summary>
                public class Config : File.DirectoryTreePruningManager.Config
                {
                    public string Name { get; set; }
                    public string DateTreeFormat { get; set; }
                    public string FileNamePrefix { get; set; }
                    public string FileNameExtension { get; set; }
                    public Logging.LogGate LogGate { get; set; }
                    public bool IncludeDate { get { return LineFormat.IncludeDate; } set { LineFormat.IncludeDate = value; } }
                    public bool IncludeQpc { get { return LineFormat.IncludeQpc; } set { LineFormat.IncludeQpc = value; } }
                    public bool IncludeSource { get { return LineFormat.IncludeSource; } set { LineFormat.IncludeSource = value; } }
                    [Obsolete("Support for recording file and line information has been removed from logging.  This property is no longer supported (2017-07-21)")]
                    public bool IncludeFileAndLine { get { return false; } set { } }
                    public bool IncludeData { get { return LineFormat.IncludeData; } set { LineFormat.IncludeData = value; } }
                    public bool IncludeThreadInfo { get { return LineFormat.IncludeThreadInfo; } set { LineFormat.IncludeThreadInfo = value; } }
                    public bool IncludeNamedValueSet { get { return LineFormat.IncludeNamedValueSet; } set { LineFormat.IncludeNamedValueSet = value; } }

                    /// <summary>
                    /// Defines an, optionally null, array of lines of text that will be added at the top of each file that is created when using this configuration
                    /// </summary>
                    public string[] FileHeaderLines { get; set; }

                    /// <summary>
                    /// When this is non-null, Defines an delegate that is invoked to return an array of lines of text that will be added at the top of each file that is created when using this configuration.
                    /// The lines produced by this delegate will be added to the file after the FileHeaderLines have been added.
                    /// </summary>
                    public Func<string[]> FileHeaderLinesDelegate { get; set; }

                    public LineFormat LineFormat { get; set; }

                    public TimeSpan PruneInterval { get; set; }

                    public FileRotationLoggingConfig.AdvanceRules AdvanceRules { get; set; }

                    public int MesgQueueSize { get; set; }

                    public Config()
                        : this("DirTreeLMH", @"./Logs")
                    {}

                    /// <summary>
                    /// Normal constructor.  Caller directly provides name of any correspondingly created LMH object and the directory path that will be the root for the resulting dirctory tree of files that this tool manages.
                    /// <para/>Sets the following default values: 
                    /// DateTreeFormat="yyyy/MM", FileNamePrefix="Log_", FileNameExtension=".txt", 
                    /// LogGate=Debug, 
                    /// IncludeDate=true, IncludeQpc=true, IncludeSource=true, IncludeData=true, 
                    /// IncludeThreadInfo=false, IncludeNamedValueSet=true, IncludeFileAndLine=false, 
                    /// PruneInterval=10.0 seconds, AdvanceRules={1 year, 10000000 bytes, test period=10 seconds}, MesgQueueSize=5000, 
                    /// CreateDirectoryIfNeeded = true, MaxInitialAutoCleanupIterations=0, MaxEntriesToDeletePerIteration=50,
                    /// FileHeaderLines = null, FileHeaderLinesDelegate = null,
                    /// </summary>
                    public Config(string name, string dirPath)
                    {
                        Name = name;
                        base.DirPath = dirPath;

                        LogGate = Logging.LogGate.Debug;

                        DateTreeFormat = @"yyyy/MM";       // use of / as path delimiter is intentional.  It is cross platform and does not trigger escape char handling in DateTime.ToString method.
                        FileNamePrefix = "Log_";
                        FileNameExtension = ".txt";

                        LineFormat = new LineFormat(true, true, true, true, true, false, "\r\n", "\t") { IncludeThreadInfo = false, IncludeNamedValueSet = false };

                        IncludeQpc = true;
                        IncludeData = true;
                        IncludeThreadInfo = true;

                        PruneInterval = TimeSpan.FromSeconds(10.0);
                        AdvanceRules = new FileRotationLoggingConfig.AdvanceRules() { FileAgeLimit = TimeSpan.FromDays(365.25), fileSizeLimit = 10000000, TestPeriod = TimeSpan.FromSeconds(10.0) };

                        MesgQueueSize = 5000;

                        base.CreateDirectoryIfNeeded = true;
                        base.MaxInitialAutoCleanupIterations = 0;
                        base.MaxEntriesToDeletePerIteration = 50;
                    }

                    /// <summary>
                    /// Copy constructor
                    /// </summary>
                    public Config(Config rhs)
                    {
                        SetFrom(rhs);
                    }

                    /// <summary>
                    /// Copy constructor helper method.
                    /// </summary>
                    public void SetFrom(Config rhs)
                    {
                        Name = rhs.Name;
                        DateTreeFormat = rhs.DateTreeFormat;
                        FileNamePrefix  =  rhs.FileNamePrefix;
                        FileNameExtension = rhs.FileNameExtension;
                        LogGate = rhs.LogGate;

                        LineFormat = new LineFormat(rhs.LineFormat);

                        PruneInterval = rhs.PruneInterval;
                        AdvanceRules = rhs.AdvanceRules;

                        MesgQueueSize = rhs.MesgQueueSize;

                        FileHeaderLines = ((rhs.FileHeaderLines != null) ? new List<string>(rhs.FileHeaderLines).ToArray() : null);
                        FileHeaderLinesDelegate = rhs.FileHeaderLinesDelegate;

                        base.SetFrom(rhs);
                    }

                    /// <summary>
                    /// Modular.Config helper setup method.  Constructs a FileRotationLoggingConfig from the matching contents in this object and then
                    /// calls its UpdateFromModularConfig method to fill in its values from modular config.  Finally this method updates the localy stored
                    /// values from the updated FileRotationLoggingConfig contents and reconstructs the LineFormat
                    /// </summary>
                    public Config UpdateFromModularConfig(string configKeyPrefixStr, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null, IConfig configInstance = null)
                    {
                        FileRotationLoggingConfig frlConfig = new FileRotationLoggingConfig(Name, DirPath)
                        {
                            dirPath = DirPath,
                            fileNamePrefix = FileNamePrefix,
                            fileNameSuffix = FileNameExtension,
                            logGate = LogGate,
                            includeQpcTime = IncludeQpc,
                            includeNamedValueSet = IncludeNamedValueSet,
                            includeThreadInfo = IncludeThreadInfo,
                            advanceRules = AdvanceRules,
                            purgeRules = new FileRotationLoggingConfig.PurgeRules(PruneRules),
                            mesgQueueSize = MesgQueueSize,
                        };
                        frlConfig.UpdateFromModularConfig(configKeyPrefixStr, issueEmitter: issueEmitter, valueEmitter: valueEmitter, configInstance: configInstance);

                        DirPath = frlConfig.dirPath;
                        FileNamePrefix = frlConfig.fileNamePrefix;
                        FileNameExtension = frlConfig.fileNameSuffix;
                        LogGate = frlConfig.logGate;

                        IncludeQpc = frlConfig.includeQpcTime;
                        IncludeNamedValueSet = frlConfig.includeNamedValueSet;
                        IncludeThreadInfo = frlConfig.includeThreadInfo;

                        AdvanceRules = frlConfig.advanceRules;

                        PruneMode = File.DirectoryTreePruningManager.PruneMode.PruneDirectories;
                        PruneRules = new File.DirectoryTreePruningManager.PruneRules(frlConfig.purgeRules);

                        MesgQueueSize = frlConfig.mesgQueueSize;

                        return this;
                    }
                }

                #endregion

                #region Constructor (and related fields)

                /// <summary>
                /// Constructor - <see cref="FileRotationLoggingConfig"/> parameter defines all initial values for the configuration and operation of this LMH.
                /// </summary>
                public TextFileDateTreeLogMessageHandler(Config configIn) 
					: base(configIn.Name, configIn.LogGate)
				{
                    config = new Config(configIn);
                    
                    // replace the default LogMessageHandlerLogger with a normal QueuedLogger.  This is for use by all levels of this LMH type.
                    //  this allows generated messages to be inserted into and handled by the entire distribution system rather than just by this LMH instance.
					logger = new QueuedLogger(Name, LogGate);

                    string pruneMgrName = "{0}.PruneMgr".CheckedFormat(config.Name);
                    Logging.ILogger pruneLogger = new QueuedLogger(pruneMgrName, LogGate);

                    Dictionary<string, Logging.IMesgEmitter> pruneMgrEmitterDictionary = new Dictionary<string, IMesgEmitter>()
                    {
                        { "Issue", pruneLogger.Info },
                        { "Info", pruneLogger.Debug },
                        { "Debug", pruneLogger.Trace },
                    };

                    dirPruneMgr = new File.DirectoryTreePruningManager(pruneMgrName, config, pruneMgrEmitterDictionary);

                    lineFmt = config.LineFormat;

                    Setup();
				}

                Config config;

                #endregion

                #region Public interface methods

                /// <summary>LogMessage Handling method API for direct call by clients which generate and distribute one message at a time.</summary>
				public override void HandleLogMessage(LogMessage lm)
				{
					if (!IsMessageTypeEnabled(lm))
						return;

					if (!StartFileAccess() || ostream == null)
					{
                        NoteMessagesHaveBeenDropped(1);
						return;
					}

                    EmitDroppedMesgMesgIfNeeded();

					lineFmt.FormatLogMessageToOstream(lm, ostream);
					handledLogMessageCounter++;

					CompleteFileAccess();
				}

                /// <summary>LogMessage Handling method API for use on outputs of queued delivery engines which agregate and distribute one or more messages at a time.</summary>
                public override void HandleLogMessages(LogMessage[] lmArray)
				{
                    if (!StartFileAccess() || ostream == null)
					{
                        NoteMessagesHaveBeenDropped(lmArray.Length);
                        return;
					}

                    EmitDroppedMesgMesgIfNeeded();

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

                #region Active File management

                bool setupPerformed = false;
                string setupFaultCode = string.Empty;

                LineFormat lineFmt = null;

                /// <summary>
                /// Property gives access to the current DirectoryEntryInfo for the current active file (name).
                /// </summary>
                DirectoryEntryInfo ActiveFileDirEntryInfo
                {
                    get { return activeFileDirEntryInfoBackingField; }
                    set 
                    {
                        DirectoryEntryInfo priorActiveFileDirEntryInfo = activeFileDirEntryInfoBackingField;

                        activeFileDirEntryInfoBackingField = value;

                        if (dirPruneMgr != null && priorActiveFileDirEntryInfo != null)
                        {
                            if (!priorActiveFileDirEntryInfo.Path.IsNullOrEmpty() && priorActiveFileDirEntryInfo.Path != PathToActiveFile)
                                dirPruneMgr.NotePathAdded(priorActiveFileDirEntryInfo.Path);
                        }
                    }
                }
                DirectoryEntryInfo activeFileDirEntryInfoBackingField = null;

                /// <summary>returns the path to the active file or the empty path if there is no active file (or if the directory is not usable)</summary>
                public string PathToActiveFile
                {
                    get
                    {
                        if (ActiveFileDirEntryInfo != null && ActiveFileDirEntryInfo.IsFile)
                            return ActiveFileDirEntryInfo.FileSystemInfo.FullName;

                        return string.Empty;
                    }
                }


                System.IO.StreamWriter ostream;

                int handledLogMessageCounter = 0;		// used for heuristic update of file size based on number of messages written
                QpcTimer updateIntervalTimer = new QpcTimer() { TriggerInterval = MaxRefreshActiveFileSizeInfoInterval, AutoReset = true, Started = true };

                const int MaxMessagesToHandleBeforeRescan = 100;
                static readonly TimeSpan MaxRefreshActiveFileSizeInfoInterval = TimeSpan.FromSeconds(10.0);


                /// <summary>
                /// This method attempts to setup the <see cref="MosaicLib.File.DirectoryFileRotationManager"/> to manage the ring of log files.  This must be comleted successfully
                /// before this LMH can be used to write to any log file.
                /// </summary>
                /// <returns>True if the setup operation was successfull</returns>
                protected bool Setup()
                {
                    lastSetupAttemptTime.SetToNow();

                    bool success = false;

                    try
                    {
                        success = dirPruneMgr.Setup(config);
                        dirPruneIntervalTimer = new QpcTimer() { TriggerInterval = config.PruneInterval, AutoReset = true, Started = true };

                        if (!success)
                        {
                            setupFaultCode = "{0} setup for path '{1}' failed:'{2}'".CheckedFormat(dirPruneMgr.ObjID, config.DirPath, dirPruneMgr.SetupFaultCode);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        setupFaultCode = "{0} setup for path '{1}' threw exception:'{2}'".CheckedFormat(dirPruneMgr.ObjID, config.DirPath, ex.Message);
                    }

                    if (!ActivateFile(true) && setupFaultCode.IsNullOrEmpty())
                    {
                        if (!IsActiveFileDefined)
                            setupFaultCode = "path '{0}' gave empty active file path".CheckedFormat(config.DirPath);
                        else
                            setupFaultCode = "path '{0}' failed: failed to open active file '{1}'".CheckedFormat(config.DirPath, PathToActiveFile);
                    }

                    setupPerformed = true;

                    if (setupFaultCode.IsNullOrEmpty())
                    {
                        return true;
                    }
                    else
                    {
                        logger.Error.Emit("{0} failed: {1}", Fcns.CurrentMethodName, setupFaultCode);
                        return false;
                    }
                }

                protected bool IsSetupNeeded
                {
                    get
                    {
                        if (IsDirectoryUsable)
                            return false;

                        if (lastSetupAttemptTime.IsZero)
                            return true;

                        const double retrySetupHoldoffPeriod = 30.0;
                        double elapsedSinceLastSetupAttempt = (Time.QpcTimeStamp.Now - lastSetupAttemptTime).TotalSeconds;

                        return (elapsedSinceLastSetupAttempt < 0.0 || elapsedSinceLastSetupAttempt >= retrySetupHoldoffPeriod);
                    }
                }

                /// <summary>Returns true if configuration is valid and directory was found and scanned successfully</summary>
                public bool IsDirectoryUsable { get { return (setupPerformed && setupFaultCode.IsNullOrEmpty() && (dirPruneMgr != null && dirPruneMgr.IsDirectoryUsable)); } }

                /// <summary>Returns the fault code, if any, produced during the last Setup call.</summary>
                public string LastFaultCode { get { return setupFaultCode; } }


                /// <summary>
                /// Attempts to activate the current (or next) file and then open the append ostream for it.
                /// </summary>
                /// <returns>true if an active file name could be determined and the ostream could be successfully opened to append to it.</returns>
				protected bool ActivateFile(bool forceFileAdvance)
				{
                    bool isAdvanceNeeded = forceFileAdvance || IsFileAdvanceNeeded(false);
					string activeFilePath = (isAdvanceNeeded ? AdvanceToNextActiveFile() : PathToActiveFile);

					if (activeFilePath.IsNullOrEmpty())
						return false;

                    // create the directory if needed
                    string activeFileDirPath = System.IO.Path.GetDirectoryName(activeFilePath);
                    if (!System.IO.Directory.Exists(activeFileDirPath))
                    {
                        System.IO.Directory.CreateDirectory(activeFileDirPath);
                    }

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

                    bool addHeader = (ActiveFileDirEntryInfo == null || !ActiveFileDirEntryInfo.IsExistingFile);

                    ostream = new System.IO.StreamWriter(activeFilePath, append, System.Text.Encoding.ASCII);

					handledLogMessageCounter = 0;

					if (ostream != null && !ostream.BaseStream.CanWrite)
						CloseFile();

                    if (ostream != null && addHeader)
                    {
                        GenerateAndProduceHeaderLines(config.FileHeaderLines, 
                                                      config.FileHeaderLinesDelegate, 
                                                      (lm) =>
                                                        {
                                                            lineFmt.FormatLogMessageToOstream(lm, ostream);
                                                            handledLogMessageCounter++;
                                                        }
                        );
                    }

					return (ostream != null);
				}

                /// <summary>Returns true if the client should stop using the current file and should start using a new file in the directory</summary>
                public bool IsFileAdvanceNeeded(bool recheckActiveFileSizeNow)
                {
                    if (!IsDirectoryUsable)
                        return false;			// if the directory is not usable then we do not need to advance (since we cannot ever advance)

                    DirectoryEntryInfo activeFileEntry = ActiveFileDirEntryInfo;
                    FileSystemInfo activeFSI = activeFileEntry.FileSystemInfo;
                    FileInfo activeFileInfo = activeFSI as FileInfo;

                    RefreshActiveFileDirIfNeeded(recheckActiveFileSizeNow);

                    if (activeFSI == null || activeFileEntry.IsDirectory)
                        return true;		// advance needed if entry's file system info is null or if it is a directory.

                    if (!activeFSI.Exists)
                        return false;		// advance is not needed if the entry represents a file that does not exist (yet).

                    return config.AdvanceRules.IsFileAdvanceNeeded(activeFileInfo);
                }

                /// <summary>
                /// Updates and returns the path to the next active file which the client should create and then use.
                /// If a prior file with the same name already exists it will have been deleted.  
                /// Returns empty path if the advance attempt failed (old file could not be deleted)
                /// </summary>
                public string AdvanceToNextActiveFile()
                {
                    RefreshActiveFileDirIfNeeded(true);

                    if (ActiveFileDirEntryInfo != null)
                    {
                        double fileAgeInHours = ActiveFileDirEntryInfo.CreationAge.TotalHours;

                        logger.Debug.Emit("{0}: prior file:'{1}' size:{2} age:{3} hours", Fcns.CurrentMethodName, ActiveFileDirEntryInfo.Name, ActiveFileDirEntryInfo.Length, fileAgeInHours.ToString("f3"));
                    }

                    GenerateNextActiveFile();

                    return PathToActiveFile;
                }

                /// <summary>Returns true if the PathToActiveFile is neither null nor empty</summary>
                public bool IsActiveFileDefined { get { return !PathToActiveFile.IsNullOrEmpty(); } }

                /// <summary>
                /// Internal method that is used to generate the next Active file name.  May delete the oldest file in order to reuse the name when appropriate.
                /// </summary>
                protected void GenerateNextActiveFile()
                {
                    string formatStr = string.Empty;
                    string dateTreeStr = string.Empty;
                    string middleStr = string.Empty;

                    DateTime dtNow = DateTime.Now;

                    try
                    {
                        dateTreeStr = dtNow.ToString(config.DateTreeFormat).AddSuffixIfNeeded(@"\");
                    }
                    catch
                    {
                        dateTreeStr = @"BadDateTreeFmt\";
                    }

                    middleStr = Utils.Dates.CvtToString(ref dtNow, Utils.Dates.DateTimeFormat.ShortWithMSec);

                    string name = "{0}{1}{2}".CheckedFormat(config.FileNamePrefix, middleStr, config.FileNameExtension.AddPrefixIfNeeded("."));

                    // generate the file's full path
                    string fullDirPath = System.IO.Path.Combine(config.DirPath, dateTreeStr);
                    string fullFilePath = System.IO.Path.Combine(fullDirPath, name);

                    // check if the file is already known and if so attempt to delete it

                    DirectoryEntryInfo fileEntryInfo = new DirectoryEntryInfo(fullFilePath);
                    FileSystemInfo fileEntryFSI = fileEntryInfo.FileSystemInfo;

                    if (fileEntryInfo.Exists)
                    {
                        // delete the file (failure means we cannot use this name...)

                        double fileAgeInHours = fileEntryInfo.CreationAge.TotalHours;
                        Int64 fileSize = fileEntryInfo.Length;

                        logger.Trace.Emit("{0}: Attempting to delete prior file:'{1}' size:{2} age:{3} hours", Fcns.CurrentMethodName, fileEntryInfo.Name, fileSize, fileAgeInHours.ToString("f3"));

                        try
                        {
                            fileEntryFSI.Delete();
                            logger.Debug.Emit("{0}: Deleted prior file:'{1}' size:{2} age:{3} hours", Fcns.CurrentMethodName, fileEntryInfo.Name, fileSize, fileAgeInHours.ToString("f3"));
                        }
                        catch (System.Exception ex)
                        {
                            logger.Error.Emit("{0}: failed to delete prior file:'{1}', error:'{2}'", Fcns.CurrentMethodName, fileEntryInfo.Name, ex.Message);
                            return;
                        }
                    }

                    // the name has been generated and if it existed, it has been deleted
                    //	create the entry, add it to the maps and set the active entry to refer to the new entry

                    ActiveFileDirEntryInfo = fileEntryInfo;

                    logger.Debug.Emit("GenerateNextActiveFile active file is now:'{0}'", ActiveFileDirEntryInfo.Name);
                }

                /// <summary>
                /// Internal method used to track changes in the state of the active file.
                /// The forceUpdate parameter may be used to cause the information to be immediately refreshed rather than waiting for the config.advanceRules.TestPeriodInSec to elapse between refreshes (default behavior).
                /// </summary>
                protected void RefreshActiveFileDirIfNeeded(bool forceUpdate)
                {
                    if (ActiveFileDirEntryInfo == null)
                        return;

                    DirectoryEntryInfo activeFileInfo = ActiveFileDirEntryInfo;

                    bool wasExistingFile = activeFileInfo.IsExistingFile;
                    Int64 oldSize = wasExistingFile ? activeFileInfo.Length : 0;

                    double stateAge = activeFileInfo.QpcTimeStamp.Age.TotalSeconds;

                    if (forceUpdate || !wasExistingFile || stateAge < 0.0 || stateAge >= config.AdvanceRules.testPeriodInSec)
                        activeFileInfo.Refresh();
                }

                /// <summary>
                /// Performs all actions required to start access to the active file.  This includes automatic attempts to reset the directory manager and
                /// performing incremental cleanup actions when needed.
                /// </summary>
                /// <returns>the results from calling <see cref="ActivateFile"/>.  true if an active file name could be determined and the ostream could be successfully opened to append to it.</returns>
				protected bool StartFileAccess()
				{
					bool dirUsable = IsDirectoryUsable;
                    if (!dirUsable)
					{
						CloseFile();

                        if (IsSetupNeeded)
                            Setup();

                        if (!IsDirectoryUsable)
							return false;
					}

                    ServicePruneMgr();

                    bool recheckActiveFileSize = (handledLogMessageCounter >= MaxMessagesToHandleBeforeRescan || updateIntervalTimer.IsTriggered);
					if (recheckActiveFileSize)
						handledLogMessageCounter = 0;

					bool fileAdvanceNeeded = IsFileAdvanceNeeded(recheckActiveFileSize);

					if (ostream != null)
					{
						if (!fileAdvanceNeeded)
							return true;
                        else
							CloseFile();
					}

					return ActivateFile(false);
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

                /// <summary>Internal method used to set the Setup Fault Code to a given value and to log it.</summary>
                protected void SetSetupFaultCode(string faultCode)
                {
                    if (string.IsNullOrEmpty(setupFaultCode))
                    {
                        setupFaultCode = faultCode;
                        logger.Error.Emit("SetupFaultCode set to '{0}'", faultCode);
                    }
                    else
                    {
                        logger.Trace.Emit("Ignoring additional setup fault code '{0}'", faultCode);
                    }
                }

                #endregion

                #region Droppped message tracking

                int droppedMessageCount = 0;
                int lastLoggedDroppedMessageCount = 0;

                private void NoteMessagesHaveBeenDropped(int count)
                {
                    droppedMessageCount += count;
                }

                /// <summary>
                /// checks to see if we need to (try) to emit a dropped message count message, and if so, it attempts to do so.
                /// </summary>
                private void EmitDroppedMesgMesgIfNeeded()
                {
                    if (droppedMessageCount != lastLoggedDroppedMessageCount)
                    {
                        logger.Error.Emit("DirMgr '{0}' dropped {1} messages since last successfull write", config.Name, droppedMessageCount - lastLoggedDroppedMessageCount);

                        lastLoggedDroppedMessageCount = droppedMessageCount;
                    }
                }

				#endregion

                #region DirPruneMgr

                private void ServicePruneMgr()
                {
                    if (dirPruneIntervalTimer.IsTriggered && dirPruneMgr != null)
                    {
                        dirPruneMgr.Service();
                    }
                }

                File.DirectoryTreePruningManager dirPruneMgr = null;
                QpcTimer dirPruneIntervalTimer;

                Time.QpcTimeStamp lastSetupAttemptTime = Time.QpcTimeStamp.Zero;

                #endregion
            }
		}
	}
}

//-------------------------------------------------------------------
