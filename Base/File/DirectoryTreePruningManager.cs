//-------------------------------------------------------------------
/*! @file DirectoryTreePruningManager.cs
 *  @brief This file defines the DirectoryTreePruningManager class.
 *  (see descriptions below)
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2011 Mosaic Systems Inc. 
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MosaicLib.Modular.Part;
using MosaicLib.Utils;

namespace MosaicLib.File
{
    #region DirectoryTreePruningManager

    /// <summary>
    /// This class is used to implement a general set of methods for managing the contents of a multi-level directory tree,
    /// especially as it relates to the creation and use of trees of files for archival and retention purposes such as for
    /// process result files.
    /// 
    /// This class provides the ability to specify a directory to manage and information that is used to determine the means:
    ///   1) by which the client keeps the manager synchronized with directory and/or file additions to the tree
    ///   2) by which the client can request the manager to perform incremental directory contents cleanup/pruning
    ///   
    /// When a manager is constructed it is given configuration and a name.
    /// The configuration gives the manager all of the operational details that it needs including the directory, and the tree pruning rules.
    /// </summary>
    /// <remarks>
    /// Principle capabilities that are provided by this class:
    ///
    ///		A) iterate through the directory on construction and retain list of all files in directory tree
    ///			including information on their size and age.
    ///		B) provide means to determine if old files/directories must be purned from the tree
    ///			so as to prevent the total number of files from exceeding the configured maximum.
    ///		C) on construction provide means to automatically clean (partially clean) the directory 
    ///			if its contents exceed the configured maximums.
    ///		D) provides means to allow the client to efficiently determine if cleanup is
    ///			needed and to perform the cleanup in an incremental fashion be deleting the oldest
    ///			files/directories when needed.
    ///		E) provide means to monitor the total number of managed files in the directory and
    ///			their total size.
    ///
    /// </remarks>

    public class DirectoryTreePruningManager : Utils.ObjIDBase
    {
        #region Constants

        /// <summary>This defines the minimum number of files that a manager can be configured to keep in a directory tree.</summary>
        public const int ConfigPruneNumFilesMinValue = 2;
        /// <summary>This defines the maximum number of files that a manager can be configured to keep in a directory tree.</summary>
        /// <remarks>Value is choosen to aim for something on the order of 300 MBytes of memory to retain the tree.</remarks>
        public const int ConfigPruneNumFilesMaxValue = 1000000;

        #endregion

        #region Configuration related definitions

        /// <summary>Enum define the intended pruning behavior</summary>
        public enum PruneMode
        {
            /// <summary>pruning is generally done file by file with directories that get emptied of files also getting removed when the last file or directory is removed from them.</summary>
            PruneFiles = 0,
            /// <summary>purning is generally done by deleting all of the files in the directory that has the oldest file as a set (and the directory along with them).  If the directory contains more than maxEntriesToDeletePerIteration files then this mode behaves as a hybrid between directory and file mode.</summary>
            PruneDirectories,
        };

        /// <summary>structure used to configure a DirectoryTreePruningManager</summary>
        public class Config
        {
            /// <summary>information about the base directory whose contents will be managed</summary>
            public String DirPath { get; set; }

            /// <summary>Property that contains the PruneRules struct</summary>
            public PruneRules PruneRules { get; set; }

            /// <summary>If true then the manager will create the path to the configured root directory on startup (if possible).  Defaults to true.</summary>
            public bool CreateDirectoryIfNeeded { get; set; }
            /// <summary>Defines the PrunMode that will be used by this manager.  Defaults to PruneMode.PruneFiles</summary>
            public PruneMode PruneMode { get; set; }
            /// <summary>Defines the maximum number of iterations of auto cleanup that will be used during setup.  Set to zero to disable inital cleanup.  Defaults to 100.</summary>
            public int MaxInitialAutoCleanupIterations { get; set; }
            /// <summary>Defines the maximum number of file entries that the manager will attempt to delete per iteration (PerformIncrementalPrune).  Defaults to 100.  This number may be exceeded by no more than the tree depth to a directory that gets emptied by a single incremental prune iteration.</summary>
            public int MaxEntriesToDeletePerIteration { get; set; }

            /// <summary>Constructor - sets default values of some properties</summary>
            public Config()
            {
                SetFrom(null);
            }

            /// <summary>Copy constructor</summary>
            public Config(Config rhs)
            {
                SetFrom(rhs);
            }

            /// <summary>
            /// Copy constructor helper method.  Sets this instace from the given other instance, or sets this instance to default values if the other instance is given as null
            /// </summary>
            public void SetFrom(Config other)
            {
                if (other == null)
                {
                    DirPath = String.Empty;
                    PruneRules = new PruneRules() { TreeNumFilesLimit = ConfigPruneNumFilesMaxValue, TreeNumItemsLimit = 0, TreeTotalSizeLimit = 0, FileAgeLimit = TimeSpan.Zero };
                    CreateDirectoryIfNeeded = true;
                    PruneMode = PruneMode.PruneFiles;
                    MaxInitialAutoCleanupIterations = 100;
                    MaxEntriesToDeletePerIteration = 100;
                }
                else
                {
                    DirPath = other.DirPath;
                    PruneRules = new PruneRules(other.PruneRules);
                    CreateDirectoryIfNeeded = other.CreateDirectoryIfNeeded;
                    PruneMode = other.PruneMode;
                    MaxInitialAutoCleanupIterations = other.MaxInitialAutoCleanupIterations;
                    MaxEntriesToDeletePerIteration = other.MaxEntriesToDeletePerIteration;
                }
            }
        };

        /// <summary>
        /// information about the rules for purging old files from the directory
        /// These rules are used to check if the oldest file in the directory needs to be
        /// deleted.  (note that rules are only applied when client explicitly checks and invokes the
        /// cleanup method).
        /// </summary>
        public struct PruneRules : IEquatable<PruneRules>
        {
            /// <summary> the user stated maximum number of files or directories (or zero for no limit).  Must be 0 or be between 2 and 1000000</summary>
            public int TreeNumItemsLimit { get; set; }
            /// <summary>the user stated maximum number of files (or zero for no limit).  Must be 0 or be between 2 and 1000000</summary>
            public int TreeNumFilesLimit { get; set; }
            /// <summary>the user stated maximum size of the set of managed files or zero for no limit</summary>
            public Int64 TreeTotalSizeLimit { get; set; }
            /// <summary>the user stated maximum age in seconds of the oldest file or zero for no limit</summary>
            public TimeSpan FileAgeLimit { get; set; }

            /// <summary>Copy constructor</summary>
            public PruneRules(PruneRules rhs)
                : this()
            {
                TreeNumItemsLimit = rhs.TreeNumItemsLimit;
                TreeNumFilesLimit = rhs.TreeNumFilesLimit;
                TreeTotalSizeLimit = rhs.TreeTotalSizeLimit;
                FileAgeLimit = rhs.FileAgeLimit;
            }

            /// <summary>
            /// Specific copy constructor.  
            /// Sets FileAgeLimit, TreeNumFilesLimit and TreeTotalSizeLimit from similar fields in given purgeRules.
            /// Sets TreeNumItemsLimit to zero.
            /// </summary>
            public PruneRules(Logging.FileRotationLoggingConfig.PurgeRules purgeRules)
                : this()
            {
                FileAgeLimit = purgeRules.FileAgeLimit;
                TreeNumFilesLimit = purgeRules.dirNumFilesLimit;
                TreeTotalSizeLimit = purgeRules.dirTotalSizeLimit;
                TreeNumItemsLimit = 0;
            }

            /// <summary>
            /// Indicates whether the current PruneRules is equal to another object given PruneRules.
            /// </summary>
            public bool Equals(PruneRules other)
            {
                return (TreeNumItemsLimit == other.TreeNumItemsLimit
                        && TreeNumFilesLimit == other.TreeNumFilesLimit
                        && TreeTotalSizeLimit == other.TreeTotalSizeLimit
                        && FileAgeLimit == other.FileAgeLimit
                        );
            }

            /// <summary>
            /// Returns true if this PruneRules is in its default (all zeros) state.  Generally this is used to indicate that the client does not wish to actually perform any pruning (since TotalNumFilesLimit cannot usefully be zero!)
            /// </summary>
            public bool IsEmpty { get { return this.Equals(default(PruneRules)); } }
        }

        #endregion

        #region Constructors

        /// <summary>Basic constructor.  objIDStr is used to initialize the ObjID base class.  emitterDictionary defines source for emitters or null to use standard Logger.</summary>
        public DirectoryTreePruningManager(string objIDStr, IDictionary<string, Logging.IMesgEmitter> emitterDictionary = null)
            : base(0, objIDStr)
        {
            EmitterDictionary = emitterDictionary;

            Clear();
        }

        /// <summary>Full constructor.  objIDStr is used to initialize the ObjID base class, config is used to trigger a Setup operation.  emitterDictionary defines source for emitters or null to use standard Logger.</summary>
        public DirectoryTreePruningManager(string objIDStr, Config config, IDictionary<string, Logging.IMesgEmitter> emitterDictionary = null)
            : this(objIDStr, emitterDictionary)
        {
            EmitterDictionary = emitterDictionary;

            Setup(config);
        }

        #endregion

        #region Emitters

        /// <summary>This emitter container contains the emitter that is used for all reported Issues (Errors)</summary>
        [Logging.MesgEmitterProperty]
        public Logging.MesgEmitterContainer Issue { get; set; }
        /// <summary>This emitter container contains the emitter that is used for all information messages such as items being deleted</summary>
        [Logging.MesgEmitterProperty]
        public Logging.MesgEmitterContainer Info { get; set; }
        /// <summary>This emitter container contains the emitter that is used for all debug messages</summary>
        [Logging.MesgEmitterProperty]
        public Logging.MesgEmitterContainer Debug { get; set; }

        /// <summary>
        /// This set only property may be used to set the object's emitter containers to contain the desired emitters.  There are three named emitters: Issue, Info, and Debug
        /// </summary>
        public IDictionary<string, Logging.IMesgEmitter> EmitterDictionary
        {
            set
            {
                if (value != null)
                {
                    Logging.SetAnnotatedInstanceEmitters<DirectoryTreePruningManager>(this, value);
                }
                else
                {
                    if (Logger == null)
                        Logger = new Logging.Logger(ObjID);
                    Issue = new Logging.MesgEmitterContainer(Logger.Info);
                    Info = new Logging.MesgEmitterContainer(Logger.Info);
                    Debug = new Logging.MesgEmitterContainer(Logger.Debug);
                }
            }
        }

        #endregion

        #region private fields

        private Config config = null;
        private Logging.Logger Logger { get; set; }
        private bool firstSetup = true;

        private bool setupPerformed = false;
        private string setupFaultCode = string.Empty;

        private DirectoryTreeEntryNode treeRootEntry = null;

        private Time.QpcTimer blockPruningUntilAfterTimer = new MosaicLib.Time.QpcTimer() { SelectedBehavior = MosaicLib.Time.QpcTimer.Behavior.NewDefault };

        /// <summary>Returns true if pruning is currently blocked for a period of time</summary>
        protected bool IsPruningBlocked { get { return (blockPruningUntilAfterTimer.Started && !blockPruningUntilAfterTimer.IsTriggered); } }

        /// <summary>Sets the timer that is used to block pruning for the given blockPeriod TimeSpan</summary>
        protected void BlockPruningForPeriod(TimeSpan blockPeriod)
        {
            if (blockPeriod > TimeSpan.Zero)
                blockPruningUntilAfterTimer.Start(blockPeriod);
            else
                blockPruningUntilAfterTimer.Stop();
        }

        #endregion

        #region public methods

        /// <summary>Get only property returns a clone of the last Config settings for which the object has been Setup, or null if the object has not been Setup yet.</summary>
        public Config SetupConfig
        {
            get { return (config != null ? new Config(config) : null); }
        }

        /// <summary>Setup the manager to use the given configuration.</summary>
        /// <returns>true if directory contents are usable after setup is complete</returns>
        public bool Setup(Config config)
        {
            Clear();

            InnerSetup(config);

            if (IsDirectoryUsable && firstSetup)
            {
                firstSetup = false;
            }

            return IsDirectoryUsable;
        }

        /// <summary>returns true if configuration is valid and directory was found and scanned successfully</summary>
        public bool IsDirectoryUsable { get { return (setupPerformed && string.IsNullOrEmpty(setupFaultCode)); } }

        /// <summary>Returns the string of the last fault code encountered by the manager.</summary>
        public string SetupFaultCode { get { return setupFaultCode ?? String.Empty; } }

        /// <summary>Returns true if the SetupFaultCode is not empty</summary>
        public bool DidSetupFail { get { return (SetupFaultCode.Length != 0); } }

        /// <summary>Service the manager - performs one iteration of cleanup if any cleanup is needed.</summary>
        public bool Service() { return Service(true); }

        /// <summary>Service the manager.  Pass true if service operation is permitted to delete files.</summary>
        public bool Service(bool cleanupIfNeeded)
        {
            bool didAnything = false;

            treeRootEntry.UpdateTree(Info.Emitter);

            if (cleanupIfNeeded && IsTreePruningNeeded)
                didAnything = didAnything || PerformIncrementalPrune();

            return didAnything;
        }

        /// <summary>Used to inform manager about the addition of another file or diretory which should be monitored by the manager (and which should eventually be pruned).</summary>
        public void NotePathAdded(string pathToAdd)
        {
            NotePathAdded(pathToAdd, Info.Emitter);
        }

        /// <summary>
        /// Used to inform manager about the addition of another file or diretory which should be monitored by the manager (and which should eventually be pruned).
        /// This method may also be used to inform the manager that the file contents might have changed.
        /// </summary>
        public void NotePathAdded(string pathToAdd, Logging.IMesgEmitter issueEmitter)
        {
            try
            {
                string fullPathToAdd = System.IO.Path.GetFullPath(pathToAdd);
                string workingRootPath = treeRootEntry.Path;

                string relativePathPart = String.Empty;
                string[] relativePathSegments = null;

                if (fullPathToAdd.StartsWith(workingRootPath))
                {
                    // convert the full path into a relative path
                    relativePathPart = @".{0}".CheckedFormat(fullPathToAdd.Substring(workingRootPath.Length));

                    relativePathSegments = relativePathPart.Split(new char[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }).Skip(1).ToArray();
                }

                string regeneratedFullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(workingRootPath, relativePathPart));

                if (!String.IsNullOrEmpty(relativePathPart) && (regeneratedFullPath == fullPathToAdd))
                    treeRootEntry.AddRelativePath(relativePathSegments, issueEmitter);
                else
                    issueEmitter.Emit("NotePathAdded '{0}' failed: given path is a proper subpath under under the monitored path '{1}'", pathToAdd, workingRootPath);

                treeRootEntry.UpdateTree(issueEmitter);
            }
            catch (System.Exception ex)
            {
                issueEmitter.Emit("NotePathAdded '{0}' failed: error:'{1}'", pathToAdd, ex);
            }
        }

        /// <summary>Gives the total size of this tree and all of the sub-nodes under this node</summary>
        public Int64 TreeContentsSize { get { Service(false); return treeRootEntry.TreeContentsSize; } }
        /// <summary>Gives the oldest file creation time or directory modification time for this tree or any of its sub-nodes</summary>
        public DateTime OldestUtcTime { get { Service(false); return treeRootEntry.OldestUtcTime; } }
        /// <summary>Gives the total number of items under and including this tree root node.</summary>
        public int TreeItemCount { get { Service(false); return treeRootEntry.TreeItemCount; } }
        /// <summary>Gives the total number of file items under and including this tree.</summary>
        public int TreeFileCount { get { Service(false); return treeRootEntry.TreeFileCount; } }

        /// <summary>returns true if there are one or more items in the tree that need to be deleted</summary>
        public bool IsTreePruningNeeded
        {
            get
            {
                if (IsPruningBlocked)
                    return false;

                if (treeRootEntry.DirContentsNodeList.Count == 0)
                    return false;			// you cannot prune an empty tree!

                if (config.PruneRules.TreeNumItemsLimit != 0)
                {
                    if (treeRootEntry.TreeItemCount > config.PruneRules.TreeNumItemsLimit)
                        return true;
                }

                if (config.PruneRules.TreeNumFilesLimit != 0)
                {
                    if (treeRootEntry.TreeFileCount > config.PruneRules.TreeNumFilesLimit)
                        return true;
                }

                if (config.PruneRules.TreeTotalSizeLimit != 0)
                {
                    if (treeRootEntry.TreeContentsSize > config.PruneRules.TreeTotalSizeLimit)
                        return true;
                }

                if (config.PruneRules.FileAgeLimit > TimeSpan.Zero)
                {
                    TimeSpan treeAge = treeRootEntry.TreeAge;
                    if (treeAge > config.PruneRules.FileAgeLimit)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Obtains the oldest DirectoryEntryItem, or Items from the tree that are to be pruned next.  
        /// Actual list returned depends on the selected PruneMode in that it will attempt to remove the oldest file when pruning Files 
        /// or it will attempt to delete all of the files in the directory with the oldest file, up to the given deletion limit count.
        /// In either case it will also include any directories above the first chosen item that become empty after all prior selected items
        /// have been deleted.
        /// </summary>
        /// <param name="pruneItemList">List to which new that have been removed from the tree are added - these items must be deleted from the file system separately</param>
        /// <param name="issueEmitter">Gives the IMesgEmitter to which error messages and other Tree traversal issues are given during this process.</param>
        public void ExtractNextListOfIncrementalItemsToPrune(List<DirectoryEntryInfo> pruneItemList, Logging.IMesgEmitter issueEmitter)
        {
            if (config.PruneMode == PruneMode.PruneDirectories)
                treeRootEntry.AppendAndRemoveOldestTreeDirectory(pruneItemList, config.MaxEntriesToDeletePerIteration, issueEmitter);
            else
                treeRootEntry.AppendAndRemoveOldestTreeItem(pruneItemList, issueEmitter);
        }

        /// <summary>
        /// Attempts to delete files/directories given in the list of DirectoryEntryInfo items to prune.
        /// <para/>returns the number of items that were successfully deleted.
        /// </summary>
        /// <param name="pruneItemList">Gives the list of DirectoryEntryInfo items that are to be removed from the file system.</param>
        /// <param name="deleteEmitter">Gives the IMesgEmitter that will recieve messages about the successfull deletions</param>
        /// <param name="issueEmitter">Gives the IMesgEmitter that will receive any messages about failures while attempting to delete each item.</param>
        /// <returns>The number of items that were successfully deleted.</returns>
        public int DeletePrunedItems(List<DirectoryEntryInfo> pruneItemList, Logging.IMesgEmitter deleteEmitter, Logging.IMesgEmitter issueEmitter)
        {
            int deletedItemCount = 0;		// actually the count of the number of items that we have attempted to delete

            for (int idx = 0; idx < pruneItemList.Count; idx++)
            {
                DirectoryEntryInfo entryToDelete = pruneItemList[idx];

                double ageInDays = entryToDelete.CreationAge.TotalDays;

                if (entryToDelete.IsFile)
                {
                    try
                    {
                        System.IO.File.Delete(entryToDelete.Path);
                        deletedItemCount++;
                        deleteEmitter.Emit("Pruned file:'{0}', size:{1}, age:{2:f3} days", entryToDelete.Path, entryToDelete.Length, ageInDays);
                    }
                    catch (System.Exception ex)
                    {
                        issueEmitter.Emit("Prune failed to delete file:'{0}', error:'{1}'", entryToDelete.Path, ex.Message);
                    }
                }
                else if (entryToDelete.IsDirectory)
                {
                    try
                    {
                        System.IO.Directory.Delete(entryToDelete.Path);
                        deletedItemCount++;
                        deleteEmitter.Emit("Pruned directory:'{0}', size:{1}, age:{2:f3} days", entryToDelete.Path, entryToDelete.Length, ageInDays);
                    }
                    catch (System.Exception ex)
                    {
                        issueEmitter.Emit("Prune failed to delete directory:'{0}', error:'{1}'", entryToDelete.Path, ex.Message);
                    }
                }
                else
                {
                    issueEmitter.Emit("Prune cannot delete unknown tree node at path:'{0}'", entryToDelete.Path);
                }
            }

            return deletedItemCount;
        }

        /// <summary>
        /// Peform an incremental prune iteration.
        /// <para/>returns true if any tree items where deleted, false otherwise.
        /// </summary>
        /// <returns>true if any tree items where deleted, false otherwise.</returns>
        /// <remarks>Number of deletions is limited by config.maxEntriesToDeletePerIteration</remarks>
        public bool PerformIncrementalPrune()
        {
            int deletedItemCount = 0;		// actually the count of the number of items that we have attempted to delete
            bool incrementalPruneFailed = false;

            List<DirectoryEntryInfo> pruneItemList = new List<DirectoryEntryInfo>();

            while (deletedItemCount < config.MaxEntriesToDeletePerIteration && IsTreePruningNeeded && !incrementalPruneFailed)
            {
                pruneItemList.Clear();

                ExtractNextListOfIncrementalItemsToPrune(pruneItemList, Info.Emitter);

                int iterationItemsToDelete = pruneItemList.Count;
                int iterationDeletedItemsCount = DeletePrunedItems(pruneItemList, Info.Emitter, Issue.Emitter);
                deletedItemCount += iterationDeletedItemsCount;

                if (iterationItemsToDelete == 0)
                {
                    Debug.Emitter.Emit("Prune did not find any removable items under:'{0}'", treeRootEntry.Path);
                    incrementalPruneFailed = true;
                }
                else if (iterationDeletedItemsCount != iterationItemsToDelete)
                {
                    Debug.Emitter.Emit("DeleteItems was not able to delete all of the items during this iteration: items:{0}, deleted items:{1}", iterationItemsToDelete, iterationDeletedItemsCount);
                    incrementalPruneFailed = true;
                }
            }

            if (incrementalPruneFailed)
            {
                TimeSpan blockPeriod = TimeSpan.FromSeconds(10.0);
                Issue.Emitter.Emit("Incremental Prune failure blocking automatic pruning for {0:f1} seconds", blockPeriod.TotalSeconds);

                BlockPruningForPeriod(blockPeriod);
            }

            if (deletedItemCount > 0)
            {
                Info.Emitter.Emit("Incremental Prune deleted {0} items.  Directory state: path:'{1}' total files:{2}, items:{3}, size:{4:f3} Mb, Age:{5:f3} days"
                                            , deletedItemCount
                                            , treeRootEntry.Path
                                            , treeRootEntry.TreeFileCount
                                            , treeRootEntry.TreeItemCount
                                            , (treeRootEntry.TreeContentsSize * (1.0 / (1024.0 * 1024.0)))
                                            , treeRootEntry.TreeAge.TotalDays
                                            );
            }

            return (deletedItemCount != 0);
        }

        #endregion

        #region private methods

        /// <summary>Resets the internal state of the manager so that it can be Setup again.</summary>
        private void Clear()
        {
            setupPerformed = false;
            setupFaultCode = "";

            if (treeRootEntry == null)
                treeRootEntry = new DirectoryTreeEntryNode();
            treeRootEntry.Clear();

            BlockPruningForPeriod(TimeSpan.Zero);
        }

        /// <summary>
        /// Internal method used to perform setup.  
        /// Verifies that directory exists, or can be created.  
        /// Builds the tree from the root path.  
        /// Verifies that configuration is valid.  
        /// Performs initial pruning if desired.
        /// Logs setup success or failure
        /// </summary>
        private void InnerSetup(Config configIn)
        {
            // if needed, clear the prior state.
            if (setupPerformed)
                Clear();

            try
            {
                // record the given configuration
                config = configIn;
                config.DirPath = System.IO.Path.GetFullPath(config.DirPath);

                bool dirExists = System.IO.Directory.Exists(config.DirPath);
                bool fileExists = !dirExists && System.IO.File.Exists(config.DirPath);

                if (fileExists)
                    SetFaultCode(Fcns.CheckedFormat("Setup Failure: target path '{0}' exists and does not specify a directory", config.DirPath));
                else if (!dirExists)
                {
                    if (config.CreateDirectoryIfNeeded)
                        System.IO.Directory.CreateDirectory(config.DirPath);
                    else
                        SetFaultCode(Fcns.CheckedFormat("Setup Failure: target directory '{0}' does not exist and could not be automatically created", config.DirPath));
                }

                if (!DidSetupFail)
                {
                    // directory exists or has been created - now scan it and record each of the entries that are found therein
                    treeRootEntry.SetPathAndGetInfo(config.DirPath);

                    treeRootEntry.BuildTree(Issue.Emitter);
                    treeRootEntry.UpdateTree(Issue.Emitter);
                }
            }
            catch (System.Exception ex)
            {
                SetFaultCode(Fcns.CheckedFormat("Setup Failure: {0}", ex));
            }

            if (!DidSetupFail)
            {
                if (config.PruneRules.TreeNumFilesLimit != 0 && config.PruneRules.TreeNumFilesLimit < ConfigPruneNumFilesMinValue)
                    SetFaultCode("Setup Failure: Config: PruneRules.TreeNumFilesLimit is too small");
                else if (config.PruneRules.TreeNumFilesLimit != 0 && config.PruneRules.TreeNumFilesLimit > ConfigPruneNumFilesMaxValue)
                    SetFaultCode("Setup Failure: Config: PruneRules.TreeNumFilesLimit is too large");
                else if (config.PruneRules.TreeTotalSizeLimit < 0)
                    SetFaultCode("Setup Failure: Config: PruneRules.TreeTotalSizeLimit is negative");
                else if (config.PruneRules.FileAgeLimit.TotalDays < 0.0)
                    SetFaultCode("Setup Failure: Config: pruneRules.FileAgeLimitInDays is negative");
            }

            if (!DidSetupFail && config.MaxInitialAutoCleanupIterations > 0)
            {
                for (int iteration = 0; IsTreePruningNeeded && (iteration < config.MaxInitialAutoCleanupIterations); iteration++)
                {
                    PerformIncrementalPrune();
                }
            }

            if (!DidSetupFail)
            {
                Info.Emitter.Emit("Directory is usable: path:'{0}' total files:{1}, items:{2}, size:{3:f3} Mb, Age:{4:f3} days",
                                config.DirPath,
                                treeRootEntry.TreeFileCount,
                                treeRootEntry.TreeItemCount,
                                (treeRootEntry.TreeContentsSize * (1.0 / (1024.0 * 1024.0))),
                                treeRootEntry.TreeAge.TotalDays
                                );
            }
            else
            {
                Issue.Emitter.Emit("Dirctory is not usable: path:'{0}' fault:'{1}'", config.DirPath, SetupFaultCode);
            }

            setupPerformed = true;
        }

        /// <summary>If empty, this method Sets the fault code to the given value and logs</summary>
        private void SetFaultCode(string faultCode)
        {
            if (string.IsNullOrEmpty(setupFaultCode))
            {
                setupFaultCode = faultCode;
                Issue.Emitter.Emit("FaultCode set to {0}", faultCode);
            }
        }

        #endregion
    }

    #endregion

} // namespace MosaicLib.File
