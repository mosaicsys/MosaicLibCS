//-------------------------------------------------------------------
/*! @file DirectoryFileRotationManager.cs
 * @brief This file defines the DirectoryFileRotationManager class.
 * (see descriptions below)
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

namespace MosaicLib.File
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;

	using MosaicLib.Modular.Part;

	//-------------------------------------------------------------------
	#region DirectoryEntryInfo

	public class DirectoryEntryInfo
	{
		#region private instance variables

		protected string path = string.Empty;
		protected string name = string.Empty;
		protected FileSystemInfo fsiItem = null;
        protected Time.QpcTimeStamp timeStamp = Time.QpcTimeStamp.Zero;

        #endregion

		#region public methods and properties

		public DirectoryEntryInfo() { }
		public DirectoryEntryInfo(FileSystemInfo fsiItem) { this.FileSystemInfo = fsiItem; }
		public DirectoryEntryInfo(string path) { Path = path; }

        /// <summary>
        /// Refreshes contained FileSystemInfo from stored path
        /// </summary>
		public void Refresh() 
		{
            if (Exists && (IsFile || IsDirectory))
            {
                FileSystemInfo.Refresh();
                timeStamp.SetToNow();
            }
            else if (!string.IsNullOrEmpty(path))
                Path = path;		// trigger a full rescan
		}

        /// <summary>Resets path and stored information to the empty state</summary>
		public void Clear()
		{
			path = string.Empty;
			name = string.Empty;
			fsiItem = null;
		}

        /// <summary>Getter returns the stored FileSystemInfo for the entry or null if the entry IsEmpty.  Setter replaces sets this object to reference and reflect the state and path information from the given FileSystemInfo object.</summary>
        public FileSystemInfo FileSystemInfo 
		{ 
			get { return fsiItem; }
			set
			{
				if (value == null)
					Clear();
				else
				{
					fsiItem = value;
					path = fsiItem.FullName;
					name = fsiItem.Name;
					timeStamp.SetToNow();
				}
			}
		}

        /// <summary>Returns the FileSystemInfo property as a FileInfo (null if the item is not a File)</summary>
        public FileInfo FileInfo { get { return fsiItem as FileInfo; } }
        /// <summary>Returns the FileSystemInfo property as a DirectoryInfo (nul if the item is not a Directory)</summary>
        public DirectoryInfo DirectoryInfo { get { return fsiItem as DirectoryInfo; } }
        /// <summary>Returns true if the path or the FileSystemInfo are null or empty</summary>
        public bool IsEmpty { get { return (string.IsNullOrEmpty(Path) || FileSystemInfo == null); } }
        /// <summary>Returns true if the FileSystemInfo is a File</summary>
        public bool IsFile { get { return (fsiItem is FileInfo); } }
        /// <summary>Returns true if the FileSystemInfo is a Directory</summary>
        public bool IsDirectory { get { return (fsiItem is DirectoryInfo); } }
        /// <summary>Returns ture if the FileSystemInfo exists and is a file</summary>
        public bool IsExistingFile { get { return IsFile && Exists; } }
        /// <summary>Returns true if the FileSystemInfo esists and is a directory</summary>
        public bool IsExistingDirectory { get { return IsDirectory && Exists; } }

        /// <summary>Getter returns the full path to the current object.  Setter resets the object and updates its informtion to contain the FileSystemInfo for the file system object at the given path or null if there is none.</summary>
        public string Path 
		{ 
			get { return path; } 
			set 
			{
				Clear();
				if (value != null)
				{
					if (System.IO.File.Exists(value))
						fsiItem = new FileInfo(value);
					else if (System.IO.Directory.Exists(value))
						fsiItem = new DirectoryInfo(value);
					else
						fsiItem = new FileInfo(value);

					path = fsiItem.FullName;
					name = fsiItem.Name;
					timeStamp.SetToNow();
				}
			} 
		}

        /// <summary>Returns the leaf name of the FileSystemInfo full path (file name or last directory name in hierarchy)</summary>
        public string Name { get { return name; } }

        /// <summary>Returns the length of the referenced file in bytes or 0 if the file does not exist or if the the given path is empty or is not a file.</summary>
        public Int64 Length
		{
			get
			{
				FileInfo fi = FileSystemInfo as FileInfo;
				return ((fi != null && fi.Exists) ? fi.Length : 0);
			}
		}

        /// <summary>Returns true if the path is a file or directory that exists.</summary>
        public bool Exists
		{
			get
			{
				if (IsFile)
					return FileInfo.Exists;
				else if (IsDirectory)
					return DirectoryInfo.Exists;
				else
					return false;
			}
		}

        /// <summary>Returns the amount of time that has elapsed from the FileSystemInfo.CreateionTime to now as a DateTime.</summary>
        public TimeSpan CreationAge 
		{
			get
			{
				if (!IsEmpty)
					return (DateTime.Now - FileSystemInfo.CreationTime);
				else
					return TimeSpan.Zero;
			}
		}

        /// <summary>Returns the QpcTimeStamp of the last object creation or its last Refresh</summary>
        public Time.QpcTimeStamp QpcTimeStamp { get { return timeStamp; } }

        /// <summary>Returns the DateTime that is the oldest from the referenced file system object's creation time, last modified time, and last accessed time, in UTC.</summary>
        protected DateTime OldestFileSystemInfoDateTimeUtc
        {
            get
            {
                if (!IsEmpty)
                {
                    DateTime oldestTime = FileSystemInfo.CreationTimeUtc;

                    if (oldestTime > FileSystemInfo.LastWriteTimeUtc)
                        oldestTime = FileSystemInfo.LastWriteTimeUtc;
                    if (oldestTime > FileSystemInfo.LastAccessTimeUtc)
                        oldestTime = FileSystemInfo.LastAccessTimeUtc;

                    return oldestTime;
                }
                else
                {
                    return DateTime.Now;
                }
            }
        }

		#endregion
	}

	#endregion

	//-------------------------------------------------------------------
	#region DirectoryFileRotationManager

	/// <summary>
	/// This class is used to implement a general set of methods for managing the contents of a directory,
	/// especially as it relates to the creation and use of sets of files for archival or retention purposes
	/// such as for logging.
	/// </summary>
	/// <remarks>
	///	This class provides the ability to specify a directory and to generate information from it 
	///	that is used to do determine the means by which:
	///		1) the client determines the name of the current file in use
	///		2) the client determines if they should start using a new file
	///		3) the client obtains the name of the next file to use
	///		4) the client keeps the manager synchronized with changes to the active file
	///		5) the client can inform the manager when to perform directory contents cleanup.
	///
	///	When a manager is constructed it is given configuration and a name.  
	///	The configuration gives the manager all of the operational details that it needs
	///	including the target directory, the file naming rules, and the file deletion rules.
	///
	///	Principle capabilities that are provided by this class:
	///
	///		A) iterate through the directory on construction and retain list of all files in directory
	///			including information on their size and age.
	///		B) provide means to determine if old files must be deleted from the directory
	///			so as to prevent the total number of files from exceeding the configured maximum.
	///			(at present this cleanup does not support deletion by size of one or all files)
	///		C) on construction provide means to automatically clean (partially clean) the directory 
	///			if its contents exceed the configured maximums.
	///		D) provides means to allow the client to efficiently determine if cleanup is
	///			needed and to perform the cleanup in an incremental fashion be deleting the oldest
	///			file when needed.
	///		E) to provide means to generate a sequence of file names to use.  Supported patterns
	///			include use of a numerical incrementing field in the file name or use of the current
	///			date and time.
	///		F) to provide means to determine which filename in the directory represents the current
	///			active file (or none if there are no valid files in the directory)
	///		G) provide means for client to determine if the current active file needs to be advanced
	///			(either due to lack of current active file or becuase current active file characteristics
	///			have triggered need to advance)
	///		H) provide means for client to cause the manager to generate the next file name to use
	///			as the current active file (to delete an old file with the same name if it exists and if desired)
	///			and to return the full path to use for creating/opening the new file.
	///		I) provide means to monitor the total number of managed files in the directory and
	///			their total size.
	///		J) The means used to track the state (size) of the active file will cache the last observed size
	///			of the active file and will only update the actual value infrequently (every few seconds).
	///			This cache updates will be immediately effect both the decision of if the active file needs
	///			to be advanced and effect the reported total size of the files in the diretory.
	/// </remarks>

	public class DirectoryFileRotationManager : SimplePartBase
	{
		#region SimplePartBase methods

		protected override void Dispose(MosaicLib.Utils.DisposableBase.DisposeType disposeType) {}

		#endregion

		#region Public interface

		public DirectoryFileRotationManager(string name) : base(name, typeof(DirectoryFileRotationManager).FullName)
		{
			logger = new Logging.QueuedLogger(name);
			Clear();
		}

		public bool Setup(Config config)		//!< @retval true if directory contents are usable after setup is complete
		{
			Clear();

			InnerSetup(config);

			if (IsDirectoryUsable && firstSetup)
			{
				firstSetup = false;
				AdvanceToNextActiveFile();
			}

			return IsDirectoryUsable;
		}

		public bool IsDirectoryUsable { get { return (setupPerformed && string.IsNullOrEmpty(setupFaultCode)); } }			//!< @retval true if configuration is valid and directory was found and scanned successfully
		public string LastFaultCode { get { return setupFaultCode; } }

		public bool IsDirectoryCleanupNeeded		//!< @retval true if there are files in the directory that need to be deleted
		{
			get 
			{
				if (config.purgeRules.dirNumFilesLimit != 0 && (totalNumberOfManagedFiles > config.purgeRules.dirNumFilesLimit))
					return true;

				if (config.purgeRules.dirTotalSizeLimit != 0 && (totalSizeOfManagedFiles > config.purgeRules.dirTotalSizeLimit))
					return true;

				if (config.purgeRules.fileAgeLimitInSec > 0.0)
				{
					if (IsDirEntryIDValid(oldestFileDirEntryID))
					{
						DirectoryEntryInfo oldestEntry = dirEntryList[oldestFileDirEntryID];

						if (oldestEntry.CreationAge.TotalSeconds >= config.purgeRules.fileAgeLimitInSec)
							return true;
					}
					else
					{
						oldestFileDirEntryID = FindOldestDirEntryID();
					}
				}

				return false;
			}
		}
		public void PerformIncrementalCleanup()		//!< deletes the oldest file in the directory as needed to cleanup the directory (max of one deletion per call)
		{
			if (!IsDirectoryCleanupNeeded)
				return;

			int entryID = FindOldestDirEntryID();

			if (!IsDirEntryIDValid(entryID))
			{
				Utils.Asserts.TakeBreakpointAfterFault("PerformIncrementalCleanup:: oldest entry is not valid?");

				return;
			}
			else if (entryID == activeFileEntryID)
			{
				Utils.Asserts.TakeBreakpointIfConditionIsNotTrue((totalNumberOfManagedFiles > 1), "Cleanup cannot be done: active file (newest) is only");

				// cannot delete the active file
				return;
			}

			DirectoryEntryInfo entryToDelete = dirEntryList[entryID];
			FileInfo entryFileInfo = entryToDelete.FileSystemInfo as FileInfo;
			if (entryFileInfo == null)
			{
				Utils.Asserts.TakeBreakpointAfterFault("PerformIncrementalCleanup:: oldest entry is not a file");

				return;
			}

			string nameToDelete = entryToDelete.Name;

			double fileAgeInHours = entryToDelete.CreationAge.TotalHours;

			logger.Trace.Emit("PerformIncrementalCleanup::Attempting to delete file:'{0}' id:{1} size:{2} age:{3} hours", nameToDelete, entryID, entryFileInfo.Length, fileAgeInHours.ToString("f3"));

			try {
				entryFileInfo.Delete();
				logger.Info.Emit("Cleanup deleted file:'{0}', size:{1} age:{2} hours", nameToDelete, entryFileInfo.Length, fileAgeInHours.ToString("f3"));
			}
			catch (SystemException e)
			{
				logger.Error.Emit("Cleanup failed to delete file:'{0}', code:{1}", nameToDelete, e.Message);
			}


			// wether or not the deletion succeeded - remove the entry from the two maps
			RemoveDirEntry(entryID);
		}


		public string PathToActiveFile	//!< gives path to active file or the empty path if there is no active file (or if the directory is not usable)
		{
			get
			{
				DirectoryEntryInfo activeFileEntry = null;

				if (IsDirEntryIDValid(activeFileEntryID))
					activeFileEntry = dirEntryList[activeFileEntryID];

				if (activeFileEntry != null && activeFileEntry.IsFile)
					return activeFileEntry.FileSystemInfo.FullName;

				return string.Empty;
			}
		}

		public bool IsFileAdvanceNeeded() {return IsFileAdvanceNeeded(true); }				//!< @retval true if the client should stop using the current file and should start using a new file in the directory
		public bool IsFileAdvanceNeeded(bool recheckActiveFileSizeNow)				//!< @retval true if the client should stop using the current file and should start using a new file in the directory
		{
			if (!IsDirectoryUsable)
				return false;			// if the directory is not usable then we do not need to advance (since we cannot ever advance)

			if (!IsDirEntryIDValid(activeFileEntryID))
				return true;

			DirectoryEntryInfo activeFileEntry = dirEntryList[activeFileEntryID];
			FileSystemInfo activeFSI = activeFileEntry.FileSystemInfo;
			FileInfo activeFileInfo = activeFSI as FileInfo;

			MaintainActiveFileInfo(recheckActiveFileSizeNow);	

			if (activeFSI == null || activeFileEntry.IsDirectory)
				return true;		// advance needed if entry's file system info is null or if it is a directory.

			if (!activeFSI.Exists)
				return false;		// advance is not needed if the entry represents a file that does not exist (yet).

			if (activeFileInfo == null)	// if we could not cast the entry's FSI to a FileInfo entry then advance is needed
				return true;

			// check for size limit reached
			bool sizeLimitReached = (activeFileInfo.Length > config.advanceRules.fileSizeLimit 
									 && config.advanceRules.fileSizeLimit > 0);

			if (sizeLimitReached)
				return true;

			// then check for age limit reached
			bool ageLimitReached = false;

			if (config.advanceRules.fileAgeLimitInSec > 0.0)
			{
				double fileAgeInSec = activeFileEntry.CreationAge.TotalSeconds;

				ageLimitReached = (fileAgeInSec > config.advanceRules.fileAgeLimitInSec);
			}

			if (ageLimitReached)
				return true;

			// neither limit reached
			return false;
		}

		public string AdvanceToNextActiveFile()	//!< @retval the path to the active file which the client should create.  If a prior file with the same name already exists it will have been deleted.  Returns empty path if the advance attempt failed (old file could not be deleted)
		{
			MaintainActiveFileInfo(true);

			if (IsDirEntryIDValid(activeFileEntryID))
			{
				DirectoryEntryInfo activeFileEntry = dirEntryList[activeFileEntryID];

				double fileAgeInHours = activeFileEntry.CreationAge.TotalHours;

				logger.Debug.Emit("AdvanceToNextActiveFile::prior file:'{0}' id:{1} size:{2} age:{3} hours", activeFileEntry.Name, activeFileEntryID, activeFileEntry.Length, fileAgeInHours.ToString("f3"));
			}

			GenerateNextActiveFile();

			return PathToActiveFile;
		}

		#endregion

		#region Config and related definitions

		private const int DirEntryID_Invalid = -1;

		private const int ConfigPurgeNumFilesMinValue = 2;
		private const int ConfigPurgeNumFilesMaxValue = 5000;

		public enum FileNamePattern
		{
			ByDate,						//!< <prefix>YYYYMMDD_hhmmssnn<suffix>
			Numeric2DecimalDigits,		//!< <prefix>nn<suffix>
			Numeric3DecimalDigits,		//!< <prefix>nnn<suffix>
			Numeric4DecimalDigits,		//!< <prefix>nnnn<suffix>
		}

		public class Config							//!< structure used to configure a DirectoryFileRotationManager
		{
			// information about the base directory whose contents will be managed
			public string					dirPath = string.Empty;

			// information used to define file names that can be generated in the set
			public string					fileNamePrefix = string.Empty;		//!< the common prefix shared by all managed files
			public string					fileNameSuffix = string.Empty;		//!< the common suffix shared by all managed files
			public FileNamePattern			fileNamePattern = FileNamePattern.ByDate;	//!< selects the pattern used to build actual file names

			// information about files that may be in the directory and which should not be created or managed
			public List<string>			excludeFileNamesSet = new List<string>(); //!< a set of the names of files to exclude from the set (ie they cannot be created or deleted by the manager

			// information on rules for advancing from one active file to the next one

			public class AdvanceRules
			{
				public Int64				fileSizeLimit = 0;		//!< the maximum desired size of each file (0 for no limit)
				public double				fileAgeLimitInSec = 0.0;	//!< file age limit in seconds (0 for no limit)
				public double				testPeriodInSec = 10.0;	//!< the period after checking the active file's size before it will be checked again
			}
			public AdvanceRules advanceRules = new AdvanceRules();

			// information about the rules for purging old files from the directory
			//	These rules are used to check if the oldest file in the directory needs to be
			//	deleted.  (note that rules are only applied when client explicitly checks and invokes the
			//	cleanup method).

			public class PurgeRules
			{
				public int					dirNumFilesLimit = 0;	//!< the user stated maximum number of files (or zero for no limit).  Must be 0 or be between 2 and 5000
				public Int64				dirTotalSizeLimit = 0;	//!< the user stated maximum size of the set of managed files or zero for no limit
				public double				fileAgeLimitInSec = 0.0;	//!< the user stated maximum age in seconds of the oldest file or zero for no limit
			}
			public PurgeRules purgeRules = new PurgeRules();

			// additional flags
			public bool					enableAutomaticCleanup = true;
			public int					maxAutoCleanupDeletes = 1000;
			public bool					createDirectoryIfNeeded = false;
		}

		#endregion

		#region private methods

		protected void Clear()
		{
			setupPerformed = false;
			setupFaultCode = "";

			numFileNumberDigits = 0;
			maxFileNumber = 0;

			activeFileEntryID = DirEntryID_Invalid;
			activeFileNumber = 0;

			dirEntryList.Clear();
			dirEntryVectFreeIDStack.Clear();

			dirEntryIDMapByName.Clear();
			dirEntryIDMapByCreatedFTimeUtc.Clear();
			oldestFileDirEntryID = DirEntryID_Invalid;

			numSubDirEntries = 0;
			numBadDirEntries = 0;

			totalNumberOfManagedFiles = 0;
			totalSizeOfManagedFiles = 0;
		}

		protected class SetupFailureException : System.Exception
		{
			public SetupFailureException(string mesg) : base(mesg) {}
		}

		protected bool SetupFailed { get { return (!string.IsNullOrEmpty(setupFaultCode)); } }

		protected void InnerSetup(Config config)
		{
			// if needed, clear the prior state.
			if (setupPerformed)
				Clear();

			// record the given configuration
			this.config = config;
			excludedFileSet = new System.Collections.Specialized.StringCollection();
			excludedFileSet.AddRange(config.excludeFileNamesSet.ToArray());

			string dirPath = config.dirPath;

			// try to add a DirectoryEntryInfo record for each of the files that are in the directory 
			try 
			{
				DirectoryEntryInfo basePathInfo = new DirectoryEntryInfo(dirPath);

				if (basePathInfo.Exists)
				{
					if (basePathInfo.IsFile)
						throw new SetupFailureException(Utils.Fcns.CheckedFormat("target path '{0}' does not specify a directory.", dirPath));
				}
				else
				{
					if (config.createDirectoryIfNeeded)
						System.IO.Directory.CreateDirectory(dirPath);
					else
						throw new SetupFailureException(Utils.Fcns.CheckedFormat("target path '{0}' does not exist.", dirPath));
				}

				// directory exists or has been created - now scan it and record each of the entries that are found therein
				DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
				FileSystemInfo [] directoryFSIArray = dirInfo.GetFileSystemInfos();

				foreach (FileSystemInfo fsi in directoryFSIArray)
				{
					string path = fsi.FullName;
					string name = fsi.Name;

					if (!excludedFileSet.Contains(name) && !excludedFileSet.Contains(path))
						AddDirEntry(path, true);
				}

				if (numBadDirEntries != 0)
					logger.Error.Emit("Setup Failure: There are bad directory entries in dir '{0}'", dirPath);
			}
			catch (SetupFailureException sfe)
			{
				SetSetupFaultCode(sfe.Message);
			}
			catch (System.Exception e)
			{
				SetSetupFaultCode(Utils.Fcns.CheckedFormat("Setup Failure: encountered unexpected exception '{0}' while processing dir '{1}'", e.Message, dirPath));
			}

			if (!SetupFailed)
			{
				// perform an additional set of tests
				if (string.IsNullOrEmpty(config.fileNamePrefix) || string.IsNullOrEmpty(config.fileNameSuffix))
					SetSetupFaultCode("Setup Failure: Invalid file name fields in configuration");
				else if (config.advanceRules.fileAgeLimitInSec < 0.0)
					SetSetupFaultCode("Setup Failure: Config: advanceRules.fileAgeLimitInSec is negative");
				else if (config.purgeRules.dirNumFilesLimit > 0 && config.purgeRules.dirNumFilesLimit < ConfigPurgeNumFilesMinValue)
					SetSetupFaultCode("Setup Failure: Config: purgeRules.dirNumFilesLimit is too small");
				else if (config.purgeRules.dirNumFilesLimit > 0 && config.purgeRules.dirNumFilesLimit > ConfigPurgeNumFilesMaxValue)
					SetSetupFaultCode("Setup Failure: Config: purgeRules.dirNumFilesLimit is too large");
				else if (config.purgeRules.dirTotalSizeLimit < 0)
					SetSetupFaultCode("Setup Failure: Config: purgeRules.dirTotalSizeLimit is negative");
				else if (config.purgeRules.fileAgeLimitInSec < 0.0)
					SetSetupFaultCode("Setup Failure: Config: purgeRules.maxFileAgeLimitInSec is negative");
			}

			DirectoryEntryInfo activeFileInfo = new DirectoryEntryInfo();

			if (!SetupFailed)
			{
				switch (config.fileNamePattern)
				{
				case FileNamePattern.ByDate:				numFileNumberDigits = 0; break;
				case FileNamePattern.Numeric2DecimalDigits:	numFileNumberDigits = 2; maxFileNumber = 100; break;
				case FileNamePattern.Numeric3DecimalDigits:	numFileNumberDigits = 3; maxFileNumber = 1000; break;
				case FileNamePattern.Numeric4DecimalDigits:	numFileNumberDigits = 4; maxFileNumber = 10000; break;
				default: SetSetupFaultCode("Setup Failure: Invalid file name pattern in configuration"); break;
				}

				// go through the directory file info entries (acquired above) from newest to oldest
				//	and retain the newest valid file that matches the name pattern for this content 
				//	manager.  This file will become the initial active file.

				activeFileEntryID = DirEntryID_Invalid;
				activeFileNumber = 0;
				bool matchFound = false;

				IList<Int64> itemKeys = dirEntryIDMapByCreatedFTimeUtc.Keys;
				IList<int> itemValues = dirEntryIDMapByCreatedFTimeUtc.Values;

				for (int idx = dirEntryIDMapByCreatedFTimeUtc.Count - 1; !matchFound && idx >= 0; idx--)
				{
					Int64 itemFTime = itemKeys[idx];
					int itemEntryID = itemValues[idx];

					activeFileEntryID = itemEntryID;

					if (IsDirEntryIDValid(activeFileEntryID))
						activeFileInfo = dirEntryList[activeFileEntryID];
					else
					{
						activeFileInfo.Clear();
						Utils.Asserts.TakeBreakpointAfterFault("Setup: entry ID in MapByCreated is not valid");
						continue;
					}

					// verify that the entry is a file
					if (!activeFileInfo.IsFile)
					{
						Utils.Asserts.TakeBreakpointAfterFault("Setup: entry ID in MapByCreated is not a file");
						continue;
					}

					// divide the name into prefix, middle and suffix fields

					string fileName = activeFileInfo.Name;
					bool fileNameIsValidMatch = true;

					int fileNamePrefixLen = config.fileNamePrefix.Length;
					int fileNameSuffixLen = config.fileNameSuffix.Length;

                    int split1Idx = Math.Min(fileNamePrefixLen, fileName.Length);   // prevent attempting to call Substring with a second arg that is beyond the end of the string.
                    string prefix = fileName.Substring(0, split1Idx);
                    string rest = fileName.Substring(split1Idx);
					int restLen = rest.Length;

					string middle = string.Empty, suffix = string.Empty;

					if (restLen >= fileNameSuffixLen)
					{
						int splitPoint = restLen - fileNameSuffixLen;

						middle = rest.Substring(0, splitPoint);
						suffix = rest.Substring(splitPoint);
					}
					else
					{
						// this file name does not match requirements - exclude from search for current active file
						fileNameIsValidMatch = false;
					}

					// test if the prefix and suffix's match
					if (prefix != config.fileNamePrefix || suffix != config.fileNameSuffix)
						fileNameIsValidMatch = false;

					// test if the middle is valid
					if (numFileNumberDigits > 0)
					{
						int testFileNumber = -1;
						bool match = int.TryParse(middle, out testFileNumber);
						
						if (testFileNumber >= 0 && middle.Length == numFileNumberDigits && match)
							activeFileNumber = testFileNumber;
						else
							fileNameIsValidMatch = false;
					}
					else
					{
						// for FileNamePattern.ByDate files, we assume that the middle is valid if it is not empty
						if (middle.Length == 0)
							fileNameIsValidMatch = false;
					}

					matchFound = fileNameIsValidMatch;
				}

				if (!matchFound && dirEntryIDMapByCreatedFTimeUtc.Count != 0)
					logger.Warning.Emit("Setup Warning: no valid active file found in non-empty directory '{0}'", dirPath);
			}

			if (!SetupFailed && config.enableAutomaticCleanup)
			{
				for (int limit = 0; IsDirectoryCleanupNeeded && (limit < config.maxAutoCleanupDeletes); limit++)
					PerformIncrementalCleanup();
			}

			if (SetupFailed)
			{
				logger.Error.Emit("Directory is not usable: path:'{0}' fault:'{1}'", dirPath, setupFaultCode);
			}
			else
			{
				logger.Debug.Emit("Directory is usable: path:'{0}' number of fileS:{1} active file:'{2}'", 
											dirPath, dirEntryIDMapByCreatedFTimeUtc.Count, activeFileInfo.Name);
			}

			setupPerformed = true;
		}

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

		protected void MaintainActiveFileInfo() { MaintainActiveFileInfo(false); }
		protected void MaintainActiveFileInfo(bool forceUpdate)
		{
			if (!IsDirEntryIDValid(activeFileEntryID))
				return;

			DirectoryEntryInfo activeFileInfo = dirEntryList[activeFileEntryID];

			bool wasExistingFile = activeFileInfo.IsExistingFile;
			Int64 oldSize = wasExistingFile ? activeFileInfo.Length : 0;

			double stateAge = activeFileInfo.QpcTimeStamp.Age.TotalSeconds;

			if (forceUpdate || !wasExistingFile || stateAge < 0.0 || stateAge >= config.advanceRules.testPeriodInSec)
				activeFileInfo.Refresh();

			Int64 newSize = activeFileInfo.Exists ? activeFileInfo.Length : 0;

			// if the file just got created then add its entry to the age map
			if (!wasExistingFile && activeFileInfo.IsExistingFile)
			{
				// need to add the file to the dirEntryIDMapByCreatedFTimeUtc

				dirEntryIDMapByCreatedFTimeUtc.Add(activeFileInfo.FileSystemInfo.CreationTime.ToFileTimeUtc(), activeFileEntryID);
				oldestFileDirEntryID = DirEntryID_Invalid;	// trigger rescan to determin the oldest file
			}

			if (newSize != oldSize)
				totalSizeOfManagedFiles += (newSize - oldSize);
		}

		protected void GenerateNextActiveFile()
		{
			// any current active entry is no longer active
			activeFileEntryID = DirEntryID_Invalid;

			// increment the file number (for numeric files)
			activeFileNumber += 1;
			if (activeFileNumber >= maxFileNumber)
				activeFileNumber = 0;

			string formatStr = string.Empty;
			string middleStr = string.Empty;

			switch (config.fileNamePattern)
			{
			default:
			case FileNamePattern.ByDate:
				{
					DateTime dtNow = DateTime.Now;

					middleStr = Utils.Dates.CvtToString(ref dtNow, Utils.Dates.DateTimeFormat.ShortWithMSec);
				}
				break;

			case FileNamePattern.Numeric2DecimalDigits:
			case FileNamePattern.Numeric3DecimalDigits:
			case FileNamePattern.Numeric4DecimalDigits:
				{
					formatStr = Utils.Fcns.CheckedFormat("D{0}", numFileNumberDigits);
					middleStr = activeFileNumber.ToString(formatStr);
				}
				break;
			}

			string name = config.fileNamePrefix + middleStr + config.fileNameSuffix;

			// we have a full path now.
			int entryID = FindDirEntryByFileName(name);

			bool nameWasInMap = (IsDirEntryIDValid(entryID) ? RemoveDirEntry(entryID) : false);

			// if the file is already known
            string filePath = System.IO.Path.Combine(config.dirPath, name);
			DirectoryEntryInfo entryInfo = new DirectoryEntryInfo(filePath);
			FileSystemInfo entryFSI = entryInfo.FileSystemInfo;

			Utils.Asserts.LogIfConditionIsNotTrue((nameWasInMap == entryInfo.Exists), "GenerateNextActiveFile: name already exists only if it was removed from map");

			if (entryInfo.Exists)
			{
				// delete the file (failure means we cannot use this name...)

				double fileAgeInHours = entryInfo.CreationAge.TotalHours;
                Int64 fileSize = entryInfo.Length;

				logger.Trace.Emit("GenerateNextActiveFile: Attempting to delete prior file:'{0}' size:{1} age:{2} hours", entryInfo.Name, fileSize, fileAgeInHours.ToString("f3"));

				try {
					entryFSI.Delete();
					logger.Debug.Emit("GenerateNextActiveFile: Deleted prior file:'{0}' size:{1} age:{2} hours", entryInfo.Name, fileSize, fileAgeInHours.ToString("f3"));
				}
				catch (System.Exception e)
				{
					logger.Error.Emit("GenerateNextActiveFile: failed to delete prior file:'{1}', error:'{1}'", entryInfo.Name, e.Message);
					return;
				}
			}

			// the name has been generated and if it existed, it has been deleted
			//	create the entry, add it to the maps and set the active entry to refer to the new entry

            activeFileEntryID = AddDirEntry(entryInfo.Path, false);

			DirectoryEntryInfo activeFileEntry = dirEntryList[activeFileEntryID];

            logger.Debug.Emit("GenerateNextActiveFile active file is now:'{0}' id:{1}", activeFileEntry.Name, activeFileEntryID);
		}

		protected int FindOldestDirEntryID()
		{
			int entryID = DirEntryID_Invalid;

			int oldestIdx = 0;
            if (dirEntryIDMapByCreatedFTimeUtc.Values.Count >= 1)
				entryID = dirEntryIDMapByCreatedFTimeUtc.Values[oldestIdx];

			return entryID;
		}

		protected int FindDirEntryByFileName(string fileName)
		{
			int entryID = DirEntryID_Invalid;

			if (!dirEntryIDMapByName.TryGetValue(fileName, out entryID))
				return DirEntryID_Invalid;

			return entryID;
		}

		protected int AddDirEntry(string filePathToAdd, bool duringInitialScan)
		{
			int entryID = DirEntryID_Invalid;
			bool entryIDValid = false;

			// attempt to get a valid entryID from the mDirEntryVectFreeID
			while (!entryIDValid && dirEntryVectFreeIDStack.Count > 0)
			{
				entryID = dirEntryVectFreeIDStack.Pop();
				entryIDValid = IsDirEntryIDValid(entryID);
			}

			if (!entryIDValid)
			{
				entryID = dirEntryList.Count;
				dirEntryList.Add(new DirectoryEntryInfo());
				entryIDValid = IsDirEntryIDValid(entryID);
			}

			if (!entryIDValid)
			{
				logger.Error.Emit("AddDirEntry: unable to reuse an existing entry or allocate a new entry");
				return DirEntryID_Invalid;
			}

			DirectoryEntryInfo entry = dirEntryList[entryID];

			Utils.Asserts.TakeBreakpointIfConditionIsNotTrue(entry.IsEmpty, "AddDirEntry::selected entry is empty");

			entry.Path = filePathToAdd;		// this triggers the entry to update its contents to reflect the information about the given file path

			// check if we can add this entry to either of the maps

			if (entry.IsExistingFile)
			{
				// if the file already exists then add the entryID to the MapByCreated time.
				dirEntryIDMapByCreatedFTimeUtc.Add(entry.FileSystemInfo.CreationTime.ToFileTimeUtc(), entryID);
				oldestFileDirEntryID = DirEntryID_Invalid;

				totalSizeOfManagedFiles += entry.Length;
			}
			else if (entry.IsDirectory)
			{
				if (duringInitialScan)
					numSubDirEntries++;
				else
					logger.Error.Emit("AddDirEntry: invalid add attemp: '{0}' is a directory", entry.Name);

				return DirEntryID_Invalid;
			}
			else if (!entry.IsFile)
			{
				if (duringInitialScan)
					numBadDirEntries++;
				else
					logger.Error.Emit("AddDirEntry: invalid add attemp: '{0}' is not a file", entry.Name);

				return DirEntryID_Invalid;
			}
			else
			{
				if (duringInitialScan)
				{
					logger.Error.Emit("AddDirEntry: invalid add attemp: '{0}' was not found (does not exist during initial scan)", entry.Name);

					return DirEntryID_Invalid;
				}

				// else the file does not already exist (it will be created externally)
				//	as such we do not add it to the dirEntryIDMapByCreatedFTimeUtc map.  
				//	MaintainActiveFileInfo will take that action after the file has been created.
			}

			// we only get here if the entry is either a file or was not found and this is not
			//	an initial scan.  Add the entry to the map by name.  If the entry was for
			//	a file then it will have been added to the map by create data and added
			//	to the total managed size above (in the IsFile block).

			dirEntryIDMapByName.Add(entry.Name, entryID);

			totalNumberOfManagedFiles = dirEntryIDMapByName.Count;

			return entryID;
		}


		protected bool RemoveDirEntry(int entryID)
		{
			bool found = IsDirEntryIDValid(entryID);

			string name = string.Empty;
			Int64 createUtcFTime = 0;

			if (found)
			{
				DirectoryEntryInfo entry = dirEntryList[entryID];

				// save the name and create time for later
				name = entry.Name;
				if (entry.Exists)
					createUtcFTime = entry.FileSystemInfo.CreationTime.ToFileTimeUtc();

				// if this entry is an existing file then decrease the totalSizeOfManagedFiles by the size of this entry
				if (entry.IsExistingFile)
				{
					totalSizeOfManagedFiles -= entry.Length;
				}

				// we will add this entry to the free list if it is not empty
				bool putBackInFreeList = !entry.IsEmpty;

				// clear the entry
				entry.Clear();

				if (putBackInFreeList)
					dirEntryVectFreeIDStack.Push(entryID);
			}

			// the entry has been cleared.  Now remove it from the two maps

			// if the name is not empty then find it in the map by name and erase it
			if (!string.IsNullOrEmpty(name))
			{
				bool foundIDInNameMap = dirEntryIDMapByName.Remove(name);

				if (!foundIDInNameMap)
					found = false;
			}

			// if the createTime is not zero then search through all the items with the matching create time
			//	and delete the item(s) that refer to the given enterID

			IList<Int64> keys = dirEntryIDMapByCreatedFTimeUtc.Keys;
			IList<int> values = dirEntryIDMapByCreatedFTimeUtc.Values;

			if (createUtcFTime != 0)
			{
				int entryIdx = dirEntryIDMapByCreatedFTimeUtc.IndexOfKey(createUtcFTime);

				// find the first occurance of this create time in the sorted list
				while (entryIdx > 0)
				{
					int prevEntryIdx = entryIdx - 1;
					if (keys[prevEntryIdx] != createUtcFTime)
						break;
					entryIdx = prevEntryIdx;
				}

				// search forward until we either find the entry for this entryID
				bool foundIDInTimeMap = false;

				// find and remove any entries that match the creation time and entryID
				while (entryIdx < dirEntryIDMapByCreatedFTimeUtc.Count)
				{
					if (keys[entryIdx] != createUtcFTime)
						break;

					if (values[entryIdx] != entryID)
					{
						entryIdx++;
						continue;
					}

					// remove this entry and update the keys and values arrays to reflected the removal

					dirEntryIDMapByCreatedFTimeUtc.RemoveAt(entryIdx);
					keys = dirEntryIDMapByCreatedFTimeUtc.Keys;
					values = dirEntryIDMapByCreatedFTimeUtc.Values;
					foundIDInTimeMap = true;

					// loop to reprocess the same entryIdx
				}

				if (!foundIDInTimeMap)
					found = false;
			}

			if (!found)
				logger.Error.Emit("RemoveDirEntry: attempt to remove '{0}' from entry:{1} failed: entry not found in all expected locations", name, entryID);

			totalNumberOfManagedFiles = dirEntryIDMapByName.Count;

			return found;
		}

		protected bool IsDirEntryIDValid(int idIdx) { return (idIdx >= 0 && idIdx < dirEntryList.Count); }

		#endregion

		#region private variables

		Logging.QueuedLogger logger = null;
		bool firstSetup = true;

		Config config = null;
		System.Collections.Specialized.StringCollection excludedFileSet = null;

		bool setupPerformed = false;
		string setupFaultCode = null;

		int	numFileNumberDigits = 0;
		int	maxFileNumber = 0;

		int	activeFileEntryID = 0;			//!< points to the active file entry in the directory maps
		int	activeFileNumber = 0;			//!< the value of the numeric field of the active file (as appropriate)

		List<DirectoryEntryInfo>	dirEntryList = new List<DirectoryEntryInfo>();				//!< vector of all the actual directory entries, in no particular order
		Stack<int>					dirEntryVectFreeIDStack = new Stack<int>();		//!< list of vector indecies for empty directory entries.

		SortedList<string, int>		dirEntryIDMapByName = new SortedList<string,int>();
		SortedList<Int64, int>		dirEntryIDMapByCreatedFTimeUtc = new SortedList<long,int>();

		int							oldestFileDirEntryID = -1;		//!< the directory entry id of the oldest file.  cleared any time the dirEntryIDMapByCreatedFTimeUtc is modified, filled in as needed by IsDirectoryCleanupNeeded

		int numSubDirEntries = 0;
		int numBadDirEntries = 0;

		int totalNumberOfManagedFiles = 0;
		Int64 totalSizeOfManagedFiles = 0;

		#endregion
	}

	#endregion

	//-------------------------------------------------------------------
}
