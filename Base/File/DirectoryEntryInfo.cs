//-------------------------------------------------------------------
/*! @file DirectoryEntryInfo.cs
 * @brief
 * (see descriptions below)
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
using System.IO;

using MosaicLib.Utils;

namespace MosaicLib.File
{
	//-------------------------------------------------------------------
	#region DirectoryEntryInfo

    /// <summary>
    /// This is a helper class that is used to obtain and track information about a path to a file or a directory
    /// </summary>
	public class DirectoryEntryInfo
	{
		#region public methods and properties

        /// <summary>
        /// Default constructor for an empty entry
        /// </summary>
		public DirectoryEntryInfo() { }

        /// <summary>
        /// Default constructor for entry based on a given FileSystemInfo object
        /// </summary>
		public DirectoryEntryInfo(FileSystemInfo fsi) { FileSystemInfo = fsi; }

        /// <summary>
        /// Default constructor for an entry based on a file system object at the given path.
        /// </summary>
		public DirectoryEntryInfo(string path) 
        { 
            Path = path;    // NOTE: Path setter does all of the work to setup this object's contents
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public DirectoryEntryInfo(DirectoryEntryInfo other)
        {
            SetFrom(other);
        }

        /// <summary>
        /// Refreshes the contained FileSystemInfo for this node
        /// </summary>
		public virtual void Refresh() 
		{
            if (fileSystemInfo != null && fileSystemInfo.Exists)
            {
                fileSystemInfo.Refresh();
                timeStamp.SetToNow();
            }
            else if (!path.IsNullOrEmpty())
            {
                Path = path;		// re-assign the Path to trigger a full rescan/rebuild of this node's contents
            }
		}

        /// <summary>Resets path and stored information to the empty state</summary>
		public DirectoryEntryInfo Clear()
		{
			path = string.Empty;
			name = string.Empty;
			fileSystemInfo = null;
            timeStamp = default(Time.QpcTimeStamp);

            return this;
        }

        /// <summary>
        /// Sets the contents of this directory entry info instance from the contens of the given <paramref name="other"/> instance
        /// </summary>
        public DirectoryEntryInfo SetFrom(DirectoryEntryInfo other)
        {
            path = other.path;
            name = other.name;
            fileSystemInfo = other.fileSystemInfo;
            timeStamp = other.timeStamp;

            return this;
        }

        #region private instance variables

        /// <summary>protected field gives derived types access to underlying backing store for Path property.</summary>
        protected string path = string.Empty;
        /// <summary>protected field gives derived types access to underlying backing store for Name property.</summary>
        protected string name = string.Empty;
        /// <summary>protected field gives derived types access to underlying backing store for FileSystemInfo property.</summary>
        protected FileSystemInfo fileSystemInfo = null;
        /// <summary>protected field gives derived types access to underlying backing store for QpcTimeStamp property.</summary>
        protected Time.QpcTimeStamp timeStamp = default(Time.QpcTimeStamp);

        #endregion

        /// <summary>Getter returns the stored FileSystemInfo for the entry or null if the entry IsEmpty.  Setter replaces sets this object to reference and reflect the state and path information from the given FileSystemInfo object.</summary>
        public FileSystemInfo FileSystemInfo { get { return fileSystemInfo; } set { UpdateFileSystemInfo(value, updatePath: true, updateName:true, updateTimeStamp: true); } }

        /// <summary>
        /// Refreshes (replaces) the FileSystemInfo instance contained in this entry using a newly acquired one from the current path.
        /// </summary>
        public void RefreshFileSystemInfo(bool updatePath = false, bool updateName = false, bool updateTimeStamp = true)
        {
            UpdateFileSystemInfo(path.GetFileSystemInfoForPath(), updatePath: updatePath, updateName: updateName, updateTimeStamp: updateTimeStamp);
        }

        /// <summary>
        /// Replaces the contained FileSystemInfo instance from the given <paramref name="fileSystemInfoIn"/>.  
        /// <para/>if the <paramref name="updatePath"/> parameter is true then this method sets the Path property from the FullName in the given <paramref name="fileSystemInfoIn"/>
        /// <para/>if the <paramref name="updateName"/> parameter is true then this method sets the Name property from the Name in the given <paramref name="fileSystemInfoIn"/>
        /// <para/>if the <paramref name="updateTimeStamp"/> parameter is true then this method set the TimeStamp to Now.
        /// </summary>
        public virtual void UpdateFileSystemInfo(FileSystemInfo fileSystemInfoIn, bool updatePath = true, bool updateName = true, bool updateTimeStamp = true)
        {
            if ((fileSystemInfo = fileSystemInfoIn) != null)
            {
                if (updatePath)
                    path = fileSystemInfo.FullName;

                if (updateName)
                    name = fileSystemInfo.Name;

                if (updateTimeStamp)
                    timeStamp.SetToNow();
            }
            else
            {
                Clear();
            }
        }

        /// <summary>Returns the FileSystemInfo property as a FileInfo (null if the item is not a File)</summary>
        public FileInfo FileInfo { get { return fileSystemInfo as FileInfo; } }

        /// <summary>Returns the FileSystemInfo property as a DirectoryInfo (null if the item is not a Directory)</summary>
        public DirectoryInfo DirectoryInfo { get { return fileSystemInfo as DirectoryInfo; } }
        
        /// <summary>Returns true if the path or the FileSystemInfo are null or empty</summary>
        public bool IsEmpty { get { return (Path.IsNullOrEmpty() || FileSystemInfo == null); } }
        
        /// <summary>Returns true if the FileSystemInfo is a File</summary>
        public bool IsFile { get { return (fileSystemInfo is FileInfo); } }
        
        /// <summary>Returns true if the FileSystemInfo is a Directory</summary>
        public bool IsDirectory { get { return (fileSystemInfo is DirectoryInfo); } }
        
        /// <summary>Returns true if the FileSystemInfo exists and is a file</summary>
        public bool IsExistingFile { get { return IsFile && Exists; } }
        
        /// <summary>Returns true if the FileSystemInfo esists and is a directory</summary>
        public bool IsExistingDirectory { get { return IsDirectory && Exists; } }

        /// <summary>
        /// Getter returns the full path to the current object.  
        /// <para/>Setter resets the object and updates its information to contain the FileSystemInfo for the file system object at the given path or null if there is none.
        /// Setter sets the backing storage to the FullPath from the FileSystemInfo that was obtained from the given value.
        /// </summary>
        public virtual string Path 
		{ 
			get { return path; } 
			set 
			{
                if (value != null)
                    UpdateFileSystemInfo(value.GetFileSystemInfoForPath(), updatePath: true, updateName: true, updateTimeStamp: true);
                else
                    Clear();
            } 
		}

        /// <summary>Returns the leaf name of the FileSystemInfo full path (file name or last directory name in hierarchy)</summary>
        public string Name { get { return name; } }

        /// <summary>Returns the length of the referenced file in bytes or 0 if the file does not exist or if the the given path is empty or is not a file.</summary>
        public Int64 Length { get { return FileInfo.SafeLength(); } }

        /// <summary>Returns true if the path is a file or directory that exists.</summary>
        public bool Exists { get { return FileSystemInfo.SafeExists(); } }

        /// <summary>Returns the amount of time that has elapsed from the FileSystemInfo.CreateionTime to now as a DateTime.</summary>
        public TimeSpan CreationAge { get { return FileSystemInfo.SafeGetCreationAge(); } }

        /// <summary>Returns the QpcTimeStamp of the last object creation or its last Refresh</summary>
        public Time.QpcTimeStamp QpcTimeStamp { get { return timeStamp; } }

        /// <summary>Returns the DateTime that is the oldest from the referenced file system object's creation time, last modified time, and last accessed time, in UTC.</summary>
        protected DateTime OldestFileSystemInfoDateTimeUtc { get { return FileSystemInfo.SafeGetOldestDateTimeUtc(fallbackValue: DateTime.MaxValue); } }

        /// <summary>Debugging and logging helper method</summary>
        public override string ToString()
        {
            if (path.IsNullOrEmpty())
                return "[empty]";
            else if (FileSystemInfo == null)
                return "path:'{0}' FSInfo:empty".CheckedFormat(path);
            else if (IsFile)
                return "File:'{0}'{1} FInfo:{2}".CheckedFormat(path, Exists ? "" : " -Exists", FileInfo);
            else if (IsDirectory)
                return "Directory:'{0}'{1} DInfo:{2}".CheckedFormat(path, Exists ? "" : " -Exists", DirectoryInfo);
            else
                return "Unknown:'{0}' FSInfo:{1}".CheckedFormat(path, FileSystemInfo);
        }

		#endregion
	}

	#endregion

	//-------------------------------------------------------------------
}
