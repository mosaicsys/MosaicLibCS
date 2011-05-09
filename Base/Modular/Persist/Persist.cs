//-------------------------------------------------------------------
/*! @file Persist.cs
 *  @brief Tools to assist in persisting various object types to and from files
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (Essential concepts and patterns adapted from C++ library version embodied in files named GSoapSerializableBase.h, GSoapPersistentObjectFileUtils.h and GSoapPersistentObjectFileUtils.cpp)
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

///<summary>
/// This file defines a set of interfaces and classes that are used to assist in saving and loading persistable objects to and from text files.
/// This code is modeled after the GSoapPersistentObjectFileUtils from the prior C++ MosaicLib and generally provides a set of relatively high efficiency
///  utilities to support serialization, storage, retreival and deserialization of various object class families.
/// 
/// Critical characteristics include: 
/// <list>
///     <item>ability to configure storage to force full flush to disk on each write</item>
///     <item>support for storage to a small ring of automatically named files so that at least one valid file is known present on disk at any time regardless of other externally induced failures.</item>
///     <item>ability to automatically load the newest file in the ring when requested.</item>
/// </list>
///</summary>
///<remarks>
/// The classes and functions presented here are intended to provide a lightweight and simple means to persist a relatively small number of relatively small objects to disk.
/// This code is not intended to used in cases where more full featured bussiness object storage and use tool kits such as the Hibernate family of tools (ORM tools).
///</remarks>

namespace MosaicLib.Modular.Persist
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;

    #region Interfaces

    /// <summary>
    /// This interface is the most common client usable type in this Modular.Perist support class set.  This inteface defines the common set of methods that
    /// are provoded by persist load/store helper objects and which may be used to save and load copies of supported object types.
    /// </summary>
    /// <typeparam name="ObjType">Defines the ObjType on which the IPersistentStorage operates.  Must be a class with default constructor that implements the IPersistSequenceable interface.</typeparam>
    public interface IPersistentStorage<ObjType> where ObjType : class, IPersistSequenceable, new()
    {
        /// <summary>Gives use access to the object to which loads are completed and from which saves are performed.</summary>
        /// <remarks>The actual object returned by this property may change as a result of other actions peformed on the underlying storage adapter object</remarks>
        ObjType Object { get; }

        /// <summary>Reads each of the files in the ring and returns the oldest valid dersialized object as measured by the contents of the PersistedVersionSequenceNumber</summary>
        /// <returns>True if a valid file was found and loaded, false if no ring files were found (Object is set to defaults).</returns>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to load or parse any existing file in the ring.  If possible Object will be updated with most recent file prior to throwing the report of such a failure.</exception>
        bool Load();

        /// <summary>Increments the current Object's PersistedVersionSequenceNumber, and then serializes and saves the Object's contents to the next file in the ring.</summary>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        void Save();

        /// <summary> Returns the path to the most recently loaded or saved file</summary>
        string LastObjectFilePath { get; }
    }

    /// <summary>
    /// This inteface defines the basic facilites that all Modular.Persist supported objects must implement in order to be used with this use pattern and related code.
    /// </summary>

    public interface IPersistSequenceable
    {
        /// <summary>
        /// This settable property provides the Modular.Persist support classes with access to a field in the target object type that can be used to store 
        /// the SaveObject sequence number that is used to determine which of a set of sources represents the most recent valid copy of the persisted object.
        /// This property MUST be included in the set of fields that are serialized on storage and deserialized on load in order for the most recent copy to be determined.
        /// </summary>
        System.UInt64 PersistedVersionSequenceNumber { get; set; }
    }

    #endregion

    #region Execptions

    public class PersistentStorageException : System.Exception
    {
        /// <summary>Initializes a new instance of the PersistentStorageException class and passes it the error message which describes the problem.</summary>
        public PersistentStorageException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the PeristentStorageException class and passes it both the error message describing the problem and the "innerException" that caused the error.</summary>
        public PersistentStorageException(string message, Exception innerException) : base(message, innerException) { }
    }

    #endregion

    #region Configs

    /// <summary>class contents are used to configure how conservative file write operations should be for purposes of Persistent Storage in each situation.</summary>
    /// <remarks>Shorthand static properties Fast, NoFileCaching and CommitToDisk represent the most common use cases for this object.</remarks>
    public class PerisistentFileWriteConfig
	{
        /// <summary>controls use of FILE_FLAG_WRITE_THROUGH flag when opening file to write to it</summary>
		public bool UseWriteThroughSemantics { get; protected set; }

        /// <summary>controls use of ::FlushFileBuffers method after writing to the file.</summary>
		public bool UseFlushFileBuffersAfterWrite { get; protected set; }

        public PerisistentFileWriteConfig() : this(false, false) {}
        public PerisistentFileWriteConfig(bool useWriteThroughSemantics, bool useFlushFileBuffersAfterWrite)
        {
            UseWriteThroughSemantics = useWriteThroughSemantics;
            UseFlushFileBuffersAfterWrite = useFlushFileBuffersAfterWrite;
        }
        public PerisistentFileWriteConfig(PerisistentFileWriteConfig rhs)
        {
            UseWriteThroughSemantics = rhs.UseWriteThroughSemantics;
            UseFlushFileBuffersAfterWrite = rhs.UseFlushFileBuffersAfterWrite;
        }

        private static readonly PerisistentFileWriteConfig fast = new PerisistentFileWriteConfig(false, false);
        private static readonly PerisistentFileWriteConfig noFileCacheing = new PerisistentFileWriteConfig(true, false);
        private static readonly PerisistentFileWriteConfig commitToDisk = new PerisistentFileWriteConfig(true, true);

        /// <summary>Version to use for performance (allows OS to cache file writes).  Files Writes complete when OS has cached the updated file contents.</summary>
        public static PerisistentFileWriteConfig Fast { get { return fast; } }
        /// <summary>Version to use tradeoff between performance and consisancy.  File Writes complete when OS has given the file contents to the disk drive for asynchronous/buffered writing.</summary>
        public static PerisistentFileWriteConfig NoFileCaching { get { return noFileCacheing; } }
        /// <summary>Most conservative version for optimal reliability.  File Writes complete only after disk drive has acknowledged updated sectors have been committed to perminent storage.</summary>
        public static PerisistentFileWriteConfig CommitToDisk { get { return commitToDisk; } }
    }

    /// <summary>class contents are used to configure operation of a PersistentObjectFileRing instance.</summary>
    public class PerisistentObjectFileRingConfig : PerisistentFileWriteConfig
	{
        /// <summary>Defines the base path used to contian the ring of file names</summary>
        public string FileBaseDirPath { get; protected set; }
        /// <summary>Defines the base file name prefix used to define the ring of file names</summary>
        public string FileBaseName { get; protected set; }
        /// <summary>number of chars in string define ring length and name ordering.  Examples include "ABCDE" or "0123456789"</summary>
        public string FileRingSpecStr { get; protected set; }
        /// <summary>the file extension string which is appended after the char from the ring spec string.</summary>
        public string FileExtension { get; protected set; }
        /// <summary>Hint for initial buffer size to use when creating the file contents.</summary>
        public int ExpectedMaximumFileSize { get; protected set; }
        /// <summary>When set to true, constrution of a persistent adpater will attempt to create the root path to the file directory</summary>
        public bool AutoCreatePath { get; protected set; }

        public PerisistentObjectFileRingConfig(string fileBaseNameAndPath) : this(fileBaseNameAndPath, "AB", 8192, PerisistentFileWriteConfig.Fast, true) { }
        public PerisistentObjectFileRingConfig(string fileBaseNameAndPath, string fileRingSpecStr) : this(fileBaseNameAndPath, fileRingSpecStr, 8192, PerisistentFileWriteConfig.Fast, true) { }
        public PerisistentObjectFileRingConfig(string fileBaseNameAndPath, string fileRingSpecStr, int expectedMaximumFileSize, PerisistentFileWriteConfig fileWriteConfig, bool autoCreatePath) 
            : this(Path.GetDirectoryName(fileBaseNameAndPath), Path.GetFileNameWithoutExtension(fileBaseNameAndPath), Path.GetExtension(fileBaseNameAndPath), fileRingSpecStr, expectedMaximumFileSize, PerisistentFileWriteConfig.Fast, autoCreatePath) { }

        public PerisistentObjectFileRingConfig(string fileBaseDirPath, string fileBaseName, string fileExtension, string fileRingSpecStr, int expectedMaximumFileSize, PerisistentFileWriteConfig fileWriteConfig, bool autoCreatePath)
            : base(fileWriteConfig)
        {
            FileBaseDirPath = fileBaseDirPath;
            FileBaseName = fileBaseName;
            FileExtension = fileExtension;
            FileRingSpecStr = fileRingSpecStr;
            ExpectedMaximumFileSize = expectedMaximumFileSize;
            AutoCreatePath = autoCreatePath;

            TestValues();
        }

        public PerisistentObjectFileRingConfig(PerisistentObjectFileRingConfig rhs) : base(rhs)
        {
            FileBaseDirPath = rhs.FileBaseDirPath;
            FileBaseName = rhs.FileBaseName;
            FileExtension = rhs.FileExtension;
            FileRingSpecStr = rhs.FileRingSpecStr;
            FileExtension = rhs.FileExtension;
            ExpectedMaximumFileSize = rhs.ExpectedMaximumFileSize;

            TestValues();
        }

        private void TestValues()
        {
            if (string.IsNullOrEmpty(FileBaseDirPath))
                throw new System.ArgumentException("FileBaseDirPath property must not be empty or null");
            if (string.IsNullOrEmpty(FileBaseName))
                throw new System.ArgumentException("FileBaseName property must not be empty or null");
            if (string.IsNullOrEmpty(FileRingSpecStr))
                throw new System.ArgumentException("FileRingSpecStr parameter must not be empty or null");
            if (ExpectedMaximumFileSize <= 0)
                throw new System.ArgumentException("ExpectedMaximumFileSize must be greater than zero");
        }
	};

    #endregion

    #region PersistentStorageAdapater(s)

    /// <summary>Adapter base class used to support common file ring based code used within specific adapter type specific derived classes.</summary>

    public abstract class PersistentObjectFileRingStorageAdapterBase<ObjType> 
        : IPersistentStorage<ObjType>
        where ObjType : class, IPersistSequenceable, new()
    {
        public PersistentObjectFileRingStorageAdapterBase(string adapterInstanceName, PerisistentObjectFileRingConfig ringConfig)
        {
            this.name = adapterInstanceName;
            this.ringConfig = ringConfig;
            fileRingPathList = GetFileRingPathList(ringConfig);

            Object = new ObjType();

            try
            {
                CreateDirectoryIfNeeded();
            }
            catch
            {
            }
        }

        #region protected methods, properties and fields

        protected string name;
        protected PerisistentObjectFileRingConfig ringConfig;

        List<string> fileRingPathList;
        int fileRingNextPathIdx = 0;
        UInt64 lastSavedSeqNum = 0;     // 0 is a magic number that means nothing has been saved or loaded
        string lastUsedFilePath = string.Empty;

        protected static List<string> GetFileRingPathList(PerisistentObjectFileRingConfig ringConfig)
        {
            int numFilesInRing = ringConfig.FileRingSpecStr.Length;
            List<string> fileRingPathList = new List<string>();
            for (int idx = 0; idx < numFilesInRing; idx++)
            {
                string name = String.Concat(ringConfig.FileBaseName, ringConfig.FileRingSpecStr.Substring(idx, 1));
                string path = Path.ChangeExtension(Path.Combine(ringConfig.FileBaseDirPath, name), ringConfig.FileExtension);
                fileRingPathList.Add(path);
            }

            return fileRingPathList;
        }

        bool directoryPathKnownToExist = false;

        protected void CreateDirectoryIfNeeded()
        {
            if (ringConfig.AutoCreatePath && !directoryPathKnownToExist)
            {
                if (System.IO.Directory.Exists(ringConfig.FileBaseDirPath))
                {
                    directoryPathKnownToExist = true;
                    return;
                }

                if (System.IO.File.Exists(ringConfig.FileBaseDirPath))
                {
                    // nothing good to do here - directory path refers to a file...
                    throw new PersistentStorageException(Utils.Fcns.CheckedFormat("BaseDirectoryPath:'{0}' is not a directory", ringConfig.FileBaseDirPath));
                }

                try
                {
                    System.IO.Directory.CreateDirectory(ringConfig.FileBaseDirPath);
                }
                catch (System.Exception e)
                {
                    throw new PersistentStorageException(Utils.Fcns.CheckedFormat("CreateDirectory:'{0}' failed: [{1}]", ringConfig.FileBaseDirPath, e.ToString()));
                }

                directoryPathKnownToExist = true;
            }
        }

        #endregion

        #region abstract methods to be implemented in derived class

        protected abstract void InnerClearObject();
        protected abstract object InnerReadObject(System.IO.Stream readStream);
        protected abstract void InnerWriteObject(ObjType obj, System.IO.Stream writeStream);

        #endregion

        #region IPersistentStorage<ObjType> Members (et. al.)

        public ObjType Object { get; protected set; }

        public bool Load() { return Load(true); }
        public bool LoadNoThrow() { return Load(false); }
        public bool Load(bool allowThrow)
        {
            // load and parse each of the files and keep the one that has the most recent sequence number
            // log any errors loading or parsing an existing file.

            ObjType oldestObj = null;
            int oldestObjIdx = -1;
            UInt64 oldestObjSeqNum = 0;
            string oldestObjPath = string.Empty;
            string zeroSeqNumPath = null;
            string duplicateSeqNumPath = null;
            int existingFileCount = 0;
            int validFileCount = 0;
            bool reloadAtEnd = false;
           
            System.Exception firstEx = null;

            for (int idx = 0; idx < fileRingPathList.Count; idx++)
            {
                string trialPath = fileRingPathList[idx];

                try
                {
                    CreateDirectoryIfNeeded();

                    if (!System.IO.File.Exists(trialPath))
                        continue;

                    existingFileCount++;

                    using (System.IO.FileStream fs = System.IO.File.OpenRead(trialPath))
                    {
                        object loadedObj = InnerReadObject(fs);
                        fs.Close();

                        ObjType currentObj = loadedObj as ObjType;

                        if (loadedObj != null && currentObj == null)
                            throw new PersistentStorageException(Utils.Fcns.CheckedFormat("Reader produced object:'{0}' cannot be casted to target type", loadedObj.GetType()));

                        if (loadedObj == (object)Object)        // if the derived object does not create new objects on InnerReadObject then we may need to reload the last oldest object at the end
                            reloadAtEnd = true;

                        UInt64 currentObjSeqNum = currentObj.PersistedVersionSequenceNumber;

                        if (currentObjSeqNum == 0)
                            zeroSeqNumPath = trialPath;
                        else if (currentObjSeqNum == oldestObjSeqNum)
                            duplicateSeqNumPath = trialPath;
                        else
                            validFileCount++;

                        if (currentObjSeqNum >= oldestObjSeqNum)
                        {
                            oldestObj = currentObj;
                            oldestObjSeqNum = currentObjSeqNum;
                            oldestObjIdx = idx;
                            oldestObjPath = trialPath;
                        }
                    }

                }
                catch (PersistentStorageException e)
                {
                    if (firstEx == null)
                        firstEx = e;
                }
                catch (System.Exception e)
                {
                    if (firstEx == null)
                        firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("Attempt to load '{0}' object from path '{1}' failed", typeof(ObjType), trialPath), e);
                }
            }

            if (firstEx == null && zeroSeqNumPath != null)
                firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("Loaded object '{0}' from path '{1}' contained zero sequence number", typeof(ObjType), zeroSeqNumPath));
            
            if (firstEx == null && duplicateSeqNumPath != null)
                firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("Loaded object '{0}' from path '{1}' contained non-unique sequence number", typeof(ObjType), duplicateSeqNumPath));

            bool loadedValidObject = (oldestObj != null);

            if (loadedValidObject && reloadAtEnd && Object.PersistedVersionSequenceNumber != oldestObjSeqNum)
            {
                loadedValidObject = false;  // be pesimistic
                try
                {
                    using (System.IO.FileStream fs = System.IO.File.OpenRead(oldestObjPath))
                    {
                        object loadedObj = InnerReadObject(fs);
                        fs.Close();
                        ObjType currentObj = loadedObj as ObjType;

                        if (loadedObj != null && currentObj == null)
                            throw new PersistentStorageException(Utils.Fcns.CheckedFormat("Final Load failed: Reader produced object:'{0}' cannot be casted to target type", loadedObj.GetType()));

                        loadedValidObject = true;
                    }
                }
                catch (PersistentStorageException e)
                {
                    if (firstEx == null)
                        firstEx = e;
                }
                catch (System.Exception e)
                {
                    if (firstEx == null)
                        firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("Final reload failed: could not read object '{0}' from path '{1}'", typeof(ObjType), oldestObjPath), e);
                }
            }

            if (!loadedValidObject)
            {
                if (firstEx == null && existingFileCount != 0)
                {
                    firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("No found file could be loaded (starting at '{0}'), using default object contents for '{1}'", fileRingPathList[0], typeof(ObjType)));
                }

                InnerClearObject();
                oldestObj = Object;
                oldestObjIdx = fileRingPathList.Count - 1;
                oldestObjPath = fileRingPathList[oldestObjIdx];
                oldestObjSeqNum = 0;
            }

            fileRingNextPathIdx = oldestObjIdx + 1;
            if (fileRingNextPathIdx >= fileRingPathList.Count)
                fileRingNextPathIdx = 0;

            if (!reloadAtEnd)
                Object = oldestObj;
            lastSavedSeqNum = oldestObjSeqNum;
            lastUsedFilePath = oldestObjPath;

            if (firstEx != null && allowThrow)
                throw firstEx;

            return (loadedValidObject && firstEx == null);
        }

        public void Save() { Save(true); }
        public bool SaveNoThrow() { return Save(false); }
        public bool Save(bool allowThrow)
        {
            System.Exception firstEx = null;

            if (Object == null)
            {
                if (firstEx == null)
                    firstEx = new PersistentStorageException("Attempt to save null object - saving default contents");

                InnerClearObject();
            }

            if (Object != null)
                Object.PersistedVersionSequenceNumber = ++lastSavedSeqNum;

            string filePath = string.Empty;

            try
            {
                CreateDirectoryIfNeeded();

                filePath = fileRingPathList[fileRingNextPathIdx++];

                if (fileRingNextPathIdx >= fileRingPathList.Count)
                    fileRingNextPathIdx = 0;

                System.IO.FileOptions fileOptions = System.IO.FileOptions.SequentialScan
                                                  | (ringConfig.UseWriteThroughSemantics 
                                                     ? System.IO.FileOptions.WriteThrough 
                                                     : System.IO.FileOptions.None);

                using (System.IO.FileStream fs = System.IO.File.Create(filePath, ringConfig.ExpectedMaximumFileSize, fileOptions))
                {
                    InnerWriteObject(Object, fs);

                    if (ringConfig.UseFlushFileBuffersAfterWrite)
                    {
                        using (SafeHandle sh = fs.SafeFileHandle)
                        {
                            LocalWin32API.FlushFileBuffers(sh);
                        }
                    }

                    fs.Close();

                    // lastSavedSeqNum was incremented above
                    lastUsedFilePath = filePath;
                }
            }
            catch (System.Exception e)
            {
                if (firstEx == null)
                    firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("Save '{0}' to path:'{1}' failed", typeof(ObjType), filePath), e);
            }

            if (firstEx != null && allowThrow)
                throw firstEx;

            return (firstEx == null);
        }

        public string LastObjectFilePath { get { return lastUsedFilePath; } }

        #endregion

    }

    public class DataContractPersistentXmlTextFileRingStorageAdapter<ObjType>
        : PersistentObjectFileRingStorageAdapterBase<ObjType>
        , IPersistentStorage<ObjType>
        where ObjType : class, IPersistSequenceable, new()
    {
        public DataContractPersistentXmlTextFileRingStorageAdapter(string name, PerisistentObjectFileRingConfig ringConfig)
            : base(name, ringConfig)
        {
            xws.ConformanceLevel = System.Xml.ConformanceLevel.Auto;
            xws.Encoding = System.Text.Encoding.ASCII;
            xws.Indent = true;
            xws.CloseOutput = false;     // we will explicitly close the underlying stream
        }

        System.Xml.XmlWriterSettings xws = new System.Xml.XmlWriterSettings();
        DataContractSerializer dcs = new DataContractSerializer(typeof(ObjType));

        protected override void  InnerClearObject()
        {
            Object = new ObjType();
        }

        protected override object InnerReadObject(System.IO.Stream readStream)
        {
            return dcs.ReadObject(readStream);
        }

        protected override void InnerWriteObject(ObjType obj, System.IO.Stream writeStream)
        {
            using (System.Xml.XmlWriter xw = System.Xml.XmlWriter.Create(writeStream, xws))
            {
                dcs.WriteObject(xw, obj);
                xw.Flush();
            }
        }
    }

    public static partial class LocalWin32API
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlushFileBuffers(System.Runtime.InteropServices.SafeHandle hFile);
    }

    #endregion
}

//-------------------------------------------------------------------
