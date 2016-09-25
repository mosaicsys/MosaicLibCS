//-------------------------------------------------------------------
/*! @file DirectoryEntryInfo.cs
 * @brief
 * (see descriptions below)
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2007 Mosaic Systems Inc.  All rights reserved. (C++ library version)
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

	//-------------------------------------------------------------------
	#region DirectoryEntryInfo

    /// <summary>
    /// This object is a helper object used to obtain and track information about a file system directory entry for a file or a directory
    /// </summary>
	public class DirectoryEntryInfo
	{
		#region private instance variables

        /// <summary>protected field gives derived types access to underlying backing store for Path property.</summary>
		protected string path = string.Empty;
        /// <summary>protected field gives derived types access to underlying backing store for Name property.</summary>
        protected string name = string.Empty;
        /// <summary>protected field gives derived types access to underlying backing store for FileSystemInfo property.</summary>
        protected FileSystemInfo fsiItem = null;
        /// <summary>protected field gives derived types access to underlying backing store for QpcTimeStamp property.</summary>
        protected Time.QpcTimeStamp timeStamp = Time.QpcTimeStamp.Zero;

        #endregion

		#region public methods and properties

        /// <summary>
        /// Default constructor for an empty entry
        /// </summary>
		public DirectoryEntryInfo() { }

        /// <summary>
        /// Default constructor for entry based on a given FileSystemInfo object
        /// </summary>
		public DirectoryEntryInfo(FileSystemInfo fsiItem) { this.FileSystemInfo = fsiItem; }

        /// <summary>
        /// Default constructor for an entry based on a file system object at the given path.
        /// </summary>
		public DirectoryEntryInfo(string path) 
        { 
            Path = path;    // NOTE: Path setter does all of the work to setup this object's contents
        }

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
        /// <summary>Returns the FileSystemInfo property as a DirectoryInfo (null if the item is not a Directory)</summary>
        public DirectoryInfo DirectoryInfo { get { return fsiItem as DirectoryInfo; } }
        /// <summary>Returns true if the path or the FileSystemInfo are null or empty</summary>
        public bool IsEmpty { get { return (string.IsNullOrEmpty(Path) || FileSystemInfo == null); } }
        /// <summary>Returns true if the FileSystemInfo is a File</summary>
        public bool IsFile { get { return (fsiItem is FileInfo); } }
        /// <summary>Returns true if the FileSystemInfo is a Directory</summary>
        public bool IsDirectory { get { return (fsiItem is DirectoryInfo); } }
        /// <summary>Returns true if the FileSystemInfo exists and is a file</summary>
        public bool IsExistingFile { get { return IsFile && Exists; } }
        /// <summary>Returns true if the FileSystemInfo esists and is a directory</summary>
        public bool IsExistingDirectory { get { return IsDirectory && Exists; } }

        /// <summary>Getter returns the full path to the current object.  Setter resets the object and updates its information to contain the FileSystemInfo for the file system object at the given path or null if there is none.</summary>
        public virtual string Path 
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
}
