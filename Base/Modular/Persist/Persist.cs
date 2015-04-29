//-------------------------------------------------------------------
/*! @file Persist.cs
 *  @brief Tools to assist in persisting various object types to and from files
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc.  All rights reserved. (Essential concepts and patterns adapted from C++ library version embodied in files named GSoapSerializableBase.h, GSoapPersistentObjectFileUtils.h and GSoapPersistentObjectFileUtils.cpp)
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

//<remarks>
// This file defines a set of interfaces and classes that are used to assist in saving and loading persistable objects to and from text files.
// This code is modeled after the GSoapPersistentObjectFileUtils from the prior C++ MosaicLib and generally provides a set of relatively high efficiency
//  utilities to support serialization, storage, retrieval and deserialization of various object class families.
// 
// Critical characteristics include: 
// <list>
//     <item>ability to configure storage to force full flush to disk on each write</item>
//     <item>support for storage to a small ring of automatically named files so that at least one valid file is known present on disk at any time regardless of other externally induced failures.</item>
//     <item>ability to automatically load the newest file in the ring when requested.</item>
// </list>
//</remarks>
//<remarks>
// The classes and functions presented here are intended to provide a lightweight and simple means to persist a relatively small number of relatively small objects to disk.
// This code is not intended to used in cases where more full featured business object storage and use tool kits such as the Hibernate family of tools (ORM tools).
//</remarks>

namespace MosaicLib.Modular.Persist
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;

    #region Interfaces

    /// <summary>
    /// This interface is the most common client usable type in this Modular.Perist support class set.  This interface defines the common set of methods that
    /// are provoded by persist load/store helper objects and which may be used to save and load copies of supported object types.
    /// </summary>
    /// <typeparam name="ObjType">Defines the ObjType on which the IPersistentStorage operates.  Must be a class with default constructor that implements the IPersistSequenceable interface.</typeparam>
    public interface IPersistentStorage<ObjType> where ObjType : class, IPersistSequenceable, new()
    {
        /// <summary>Gives use access to the object to which loads are completed and from which saves are performed.</summary>
        /// <remarks>
        /// The actual object returned by this property may change as a result of other actions performed on the underlying storage adapter object.
        /// </remarks>
        ObjType Object { get; set; }

        /// <summary>Attempts to Read each of the files in the ring and returns the oldest valid deserialized object as measured by the contents of the PersistedVersionSequenceNumber</summary>
        /// <returns>True if a valid file was found and loaded, throws PersistentStorageException if no valid ring file were found (Object is set as new ObjType()).</returns>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to load or parse any existing file in the ring.  If possible Object will be updated with most recent file prior to throwing the report of such a failure or Object is set as new ObjType().</exception>
        bool Load();

        /// <summary>Attempts to Read each of the files in the ring and returns the oldest valid deserialized object as measured by the contents of the PersistedVersionSequenceNumber</summary>
        /// <returns>True if a valid file was found and loaded, false if no ring files were found (Object is set as new ObjType()).</returns>
        bool LoadNoThrow();

        /// <summary>Attempts to Read each of the files in the ring and returns the oldest valid deserialized object as measured by the contents of the PersistedVersionSequenceNumber</summary>
        /// <returns>True if a valid file was found and loaded, false or throw PersistentStorageExcpetion (based on allowThrow) if no valid ring file was found (Object is set as new ObjType()).</returns>
        /// <exception cref="PersistentStorageException">if allowThrow is true then this method throws PersistentStorageException on failure to load or parse any existing file in the ring or if there are not valid files in the ring.  If possible Object will be updated with most recent file prior to throwing the report of such a failure.</exception>
        /// <param name="allowThrow">Pass true to allow called method to throw on any error or false to prevent it from doing so.</param>
        bool Load(bool allowThrow);

        /// <summary>
        /// Increments the current Object's PersistedVersionSequenceNumber (from the maximum of the object's current value and the last value written by the adapter), 
        /// and then serializes and saves the Object's contents to the next file in the ring.
        /// </summary>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        void Save();

        /// <summary>Sets the given object's PersistedVersionSequenceNumber to one more than the maximum of the last value written and the object's current value, saves the given object as the reference object, and then serializes and saves it contents to the next file in the ring.</summary>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        void Save(ObjType objToSave);

        /// <summary>Increments the current Object's PersistedVersionSequenceNumber, and then serializes and saves the Object's contents to the next file in the ring.</summary>
        /// <param name="allowThrow">Pass true to allow called method to throw on any error or false to prevent it from doing so.</param>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        /// <returns>True on success or failure (when allowThrow is false)</returns>
        bool Save(bool allowThrow);

        /// <summary>Increments the current Object's PersistedVersionSequenceNumber, and then serializes and saves the Object's contents to the next file in the ring.</summary>
        /// <returns>True on success or failure</returns>
        bool SaveNoThrow();

        /// <summary>Sets the given object's PersistedVersionSequenceNumber to one more than the maximum of the last value written and the object's current value, saves the given object as the reference object, and then serializes and saves it contents to the next file in the ring.</summary>
        /// <param name="objToSave">Pass in handle to object to Save (will have PersistedVersionSequenceNumber set to next value).  If null is passed, method will save constructor default and fail with an appropriate error.</param>
        /// <param name="allowThrow">Pass true to allow called method to throw on any error or false to prevent it from doing so.</param>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        /// <returns>True on success or failure (when allowThrow is false)</returns>
        bool Save(ObjType objToSave, bool allowThrow);

        /// <summary> Returns the path to the most recently loaded or saved file</summary>
        string LastObjectFilePath { get; }

        /// <summary> Returns the exception generated by the last Load or Save operation or null if that operation was successful.</summary>
        System.Exception LastExecption { get; }

        /// <summary>
        /// Tells engine to advance the internal ring pointer so that the next Save attempt will skip the last selected ring element.  
        /// This is primarily intended to support custom advance rules when used with AdvanceOnSaveRule.OnSuccessOnly
        /// </summary>
        void AdvanceToNextFile();

        /// <summary>
        /// Allows a client to provide an emitter to use for logging when issues are detected during unsuccessful Load and/or Save operations.  
        /// Setting this property to null us functionally identical to setting the property to MosaicLib.Logging.NullEmitter.
        /// </summary>
        Logging.IMesgEmitter IssueEmitter { set; }

        /// <summary>
        /// Allows a client to provide an emitter to use for logging successful use of Load and/or Save operations.  
        /// Setting this property to null us functionally identical to setting the property to MosaicLib.Logging.NullEmitter.
        /// </summary>
        Logging.IMesgEmitter SuccessEmitter { set; }

        /// <summary>
        /// Defines the conditions under which a Save is attempted immediately after a Load fails.
        /// </summary>
        AutoSaveConditions AutoSaveConditions { get; set; }
    }

    /// <summary>
    /// Defines the client slectable conditions under which a Save may be attempted after a Load fails.
    /// </summary>
    [Flags]
    public enum AutoSaveConditions
    {
        /// <summary>No conditions requests an automatic Save attempt after a failed Load.</summary>
        None = 0,

        /// <summary>Requests that a Save be attempted after a Load fails because there are no files.</summary>
        OnLoadFailedWithNoFilesFound = 1,

        /// <summary>Requests that a Save be attempted after an Load fails, regardless of reason.</summary>
        OnAnyFailedLoad = 2,

        /// <summary>When this flag is set, if an Auto Save is triggered and it succeeds then the Load is marked as having succeeded.</summary>
        SuccessfullSaveMakesLoadSucceed = 0x8000,
    }

    /// <summary>
    /// This interface defines the basic facilities that all Modular.Persist supported objects must implement in order to be used with this use pattern and related code.
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

    /// <summary>
    /// Exception type generated for specific Persist Storage related failures.  
    /// Also used to encapsulate other caught exceptions and to re-throw or save them in this outer exception container.
    /// </summary>
    public class PersistentStorageException : System.Exception
    {
        /// <summary>Initializes a new instance of the PersistentStorageException class and passes it the error message which describes the problem.</summary>
        public PersistentStorageException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the PeristentStorageException class and passes it both the error message describing the problem and the "innerException" that caused the error.</summary>
        public PersistentStorageException(string message, Exception innerException) : base(message, innerException) { }
    }

    #endregion

    #region Configs

    /// <summary>
    /// Preserve use of prior incorrect name for the time being.  Please use PersistentFileWriteConfig in place of this class.
    /// </summary>
    [Obsolete("This class name is not spelled correctly.  Please replace its use with the correctly named PersistentFileWriteConfig. (2013-04-02)")]
    public class PerisistentFileWriteConfig : PersistentFileWriteConfig
    {
        /// <summary>Obsolete - please use, and refer to, <see cref="PersistentFileWriteConfig"/> class instead</summary>
        public PerisistentFileWriteConfig() : base() { }
        /// <summary>Obsolete - please use, and refer to, <see cref="PersistentFileWriteConfig"/> class instead</summary>
        public PerisistentFileWriteConfig(bool useWriteThroughSemantics, bool useFlushFileBuffersAfterWrite) : base(useWriteThroughSemantics, useFlushFileBuffersAfterWrite) { }
        /// <summary>Obsolete - please use, and refer to, <see cref="PersistentFileWriteConfig"/> class instead</summary>
        public PerisistentFileWriteConfig(PersistentFileWriteConfig rhs) : base(rhs) { }
    }

    /// <summary>class contents are used to configure how conservative file write operations should be for purposes of Persistent Storage in each situation.</summary>
    /// <remarks>Shorthand static properties Fast, NoFileCaching and CommitToDisk represent the most common use cases for this object.</remarks>
    public class PersistentFileWriteConfig
	{
        /// <summary>controls use of FILE_FLAG_WRITE_THROUGH flag when opening file to write to it.</summary>
		public bool UseWriteThroughSemantics { get; set; }

        /// <summary>controls use of ::FlushFileBuffers method after writing to the file.</summary>
		public bool UseFlushFileBuffersAfterWrite { get; set; }

        /// <summary>
        /// Default constructor:  Same as PersistentFileWriteConfig.Fast
        /// </summary>
        public PersistentFileWriteConfig() : this(false, false) {}

        /// <summary>
        /// Constructor that allows caller to specify the two contained settings: useWriteThroughSemantics and useFlushFileBuffersAfterWrite
        /// </summary>
        /// <param name="useWriteThroughSemantics">controls use of FILE_FLAG_WRITE_THROUGH flag when opening file to write to it.</param>
        /// <param name="useFlushFileBuffersAfterWrite">controls use of ::FlushFileBuffers method after writing to the file.</param>
        public PersistentFileWriteConfig(bool useWriteThroughSemantics, bool useFlushFileBuffersAfterWrite)
        {
            UseWriteThroughSemantics = useWriteThroughSemantics;
            UseFlushFileBuffersAfterWrite = useFlushFileBuffersAfterWrite;
        }

        /// <summary>
        /// Copy constructor.  Sets new object to be a copy of the one given as rhs.
        /// </summary>
        public PersistentFileWriteConfig(PersistentFileWriteConfig rhs)
        {
            UseWriteThroughSemantics = rhs.UseWriteThroughSemantics;
            UseFlushFileBuffersAfterWrite = rhs.UseFlushFileBuffersAfterWrite;
        }

        private static readonly PersistentFileWriteConfig fast = new PersistentFileWriteConfig(false, false);
        private static readonly PersistentFileWriteConfig noFileCacheing = new PersistentFileWriteConfig(true, false);
        private static readonly PersistentFileWriteConfig commitToDisk = new PersistentFileWriteConfig(true, true);

        /// <summary>Version to use for performance (allows OS to cache file writes).  Files Writes complete when OS has cached the updated file contents.</summary>
        public static PersistentFileWriteConfig Fast { get { return new PersistentFileWriteConfig(fast); } }
        /// <summary>Version to use trade off between performance and consistency.  File Writes complete when OS has given the file contents to the disk drive for asynchronous/buffered writing.</summary>
        public static PersistentFileWriteConfig NoFileCaching { get { return new PersistentFileWriteConfig(noFileCacheing); } }
        /// <summary>Most conservative version for optimal reliability.  File Writes complete only after disk drive has acknowledged updated sectors have been committed to permanent storage.</summary>
        public static PersistentFileWriteConfig CommitToDisk { get { return new PersistentFileWriteConfig(commitToDisk); } }
    }

    /// <summary>Enum defines different types of conditions under which and IPersistentStorage object will advance the file path after each Save operations.</summary>
    public enum AdvanceOnSaveRule
    {
        /// <summary>Writer advances to next file for successful and unsuccessful writes.</summary>
        Allways,
        /// <summary>Writer advances to next file only after successful writes.</summary>
        OnSuccessOnly,
        /// <summary>Writer advances to next file after successful writes and after N failed writes (N is specified separately)</summary>
        OnSuccessOrNFailures,
    }

    /// <summary>
    /// Preserve use of prior incorrect name for the time being.  Please use PersistentObjectFileRingConfig in place of this class.
    /// </summary>
    [Obsolete("This class name is not spelled correctly.  Please replace its use with the correctly named PersistentObjectFileRingConfig. (2013-04-02)")]
    public class PerisistentObjectFileRingConfig : PersistentObjectFileRingConfig
    {
        /// <summary>Obsolete constructor.  Please replace with use of the corresponding <see cref="PersistentObjectFileRingConfig"/> class's constructor</summary>
        public PerisistentObjectFileRingConfig(string fileBaseNameAndPath) : base(fileBaseNameAndPath) { }
        /// <summary>Obsolete constructor.  Please replace with use of the corresponding <see cref="PersistentObjectFileRingConfig"/> class's constructor</summary>
        public PerisistentObjectFileRingConfig(string fileBaseNameAndPath, string fileRingSpecStr) : base(fileBaseNameAndPath, fileRingSpecStr) { }
        /// <summary>Obsolete constructor.  Please replace with use of the corresponding <see cref="PersistentObjectFileRingConfig"/> class's constructor</summary>
        public PerisistentObjectFileRingConfig(string fileBaseNameAndPath, string fileRingSpecStr, int expectedMaximumFileSize, PersistentFileWriteConfig fileWriteConfig, bool autoCreatePath) : base(fileBaseNameAndPath, fileRingSpecStr, expectedMaximumFileSize, fileWriteConfig, autoCreatePath) { }

        /// <summary>Obsolete constructor.  Please replace with use of the corresponding <see cref="PersistentObjectFileRingConfig"/> class's constructor</summary>
        public PerisistentObjectFileRingConfig(string fileBaseDirPath, string fileBaseName, string fileExtension, string fileRingSpecStr, int expectedMaximumFileSize, PersistentFileWriteConfig fileWriteConfig, bool autoCreatePath)
            : base(fileBaseDirPath, fileBaseName, fileExtension, fileRingSpecStr, expectedMaximumFileSize, fileWriteConfig, autoCreatePath)
        { }

        /// <summary>Obsolete copy constructor.  Please replace with use of the corresponding <see cref="PersistentObjectFileRingConfig"/> class's constructor</summary>
        public PerisistentObjectFileRingConfig(PersistentObjectFileRingConfig rhs) : base(rhs) { }
    }

    /// <summary>class contents are used to configure operation of a PersistentObjectFileRing instance.</summary>
    public class PersistentObjectFileRingConfig : PersistentFileWriteConfig
	{
        /// <summary>Defines the base path used to contain the ring of file names</summary>
        public string FileBaseDirPath { get; set; }
        /// <summary>Defines the base file name prefix used to define the ring of file names</summary>
        public string FileBaseName { get; set; }
        /// <summary>number of chars in string define ring length and name ordering.  Examples include "ABCDE" or "0123456789"</summary>
        public string FileRingSpecStr { get; set; }
        /// <summary>the file extension string which is appended after the char from the ring spec string.</summary>
        public string FileExtension { get; set; }
        /// <summary>Hint for initial buffer size to use when creating the file contents.</summary>
        public int ExpectedMaximumFileSize { get; set; }
        /// <summary>When set to true, construction of a persistent adapter will attempt to create the root path to the file directory</summary>
        public bool AutoCreatePath { get; set; }
        /// <summary>Defines the rules for when to advance on Save (especially failed Save)</summary>
        public AdvanceOnSaveRule AdvanceOnSaveRule { get; set; }
        /// <summary>When the AdvanceOnSaveRule property is set to AdvanceOnSaveRule.OnSuccessOrNFailures, this property defines the minimum number of sequential failures after which the ring will advance to the next file.  This property is ignored for other AdvanceOnSaveRule values.</summary>
        public uint AdvanceAfterNSaveFailures { get; set; }
        /// <summary>
        /// When true Load's will fail and/or throw if any errors are detected while loading from the file ring (corrupt file, ...).  
        /// When false, Load will fail and/or throw only if no valid file was found.
        /// </summary>
        public bool ThrowOnAnyLoadFailure { get; set; }

        /// <summary>
        /// Most basic constructor: caller specifies fileBaseNameAndPath.  Uses defaults of fileRingSpecStr = "AB", PersistentFileWriteConfig.Fast, and AutoCreatePath = true
        /// </summary>
        /// <exception cref="System.ArgumentException">
        /// If any of extracted FileBaseDirPath, FileBaseName, FileRingSpecStr are null or empty, or if ExpectedMaximumFileSize if is not greater than zero.
        /// </exception>
        public PersistentObjectFileRingConfig(string fileBaseNameAndPath) : this(fileBaseNameAndPath, "AB", 8192, PersistentFileWriteConfig.Fast, true) { }
        /// <summary>
        /// Basic constructor: caller specifies fileBaseNameAndPath and fileRingSpecStr.  Uses defaults of PersistentFileWriteConfig.Fast, and AutoCreatePath = true
        /// </summary>
        /// <exception cref="System.ArgumentException">
        /// If any of extracted FileBaseDirPath, FileBaseName, FileRingSpecStr are null or empty, or if ExpectedMaximumFileSize if is not greater than zero.
        /// </exception>
        public PersistentObjectFileRingConfig(string fileBaseNameAndPath, string fileRingSpecStr) : this(fileBaseNameAndPath, fileRingSpecStr, 8192, PersistentFileWriteConfig.Fast, true) { }
        /// <summary>
        /// Detailed constructor.
        /// </summary>
        /// <param name="fileBaseNameAndPath">Gives the path to the file ring directory and the base name template of the files there.  fileRingSpecStr characters are appended to the file name portion of this string to generate the ring of final file names.</param>
        /// <param name="fileRingSpecStr">Gives the set of characters which are used to define the ring.  "ABC" gives 3 files path\fileA.ext, path\fileB.ext and path\fileC.ext.</param>
        /// <param name="expectedMaximumFileSize">Gives the pre-allocated serialization buffer size for save operations.</param>
        /// <param name="fileWriteConfig">Gives the PersistentFileWriteConfig that the client would like to use for writing these files.</param>
        /// <param name="autoCreatePath">Set to true to automatically attempt to create the directories required to get to the path given by the fileBaseNameAndPath</param>
        /// <exception cref="System.ArgumentException">
        /// If any of extracted FileBaseDirPath, FileBaseName, FileRingSpecStr are null or empty, or if ExpectedMaximumFileSize if is not greater than zero.
        /// </exception>
        public PersistentObjectFileRingConfig(string fileBaseNameAndPath, string fileRingSpecStr, int expectedMaximumFileSize, PersistentFileWriteConfig fileWriteConfig, bool autoCreatePath) 
            : this(Path.GetDirectoryName(fileBaseNameAndPath), Path.GetFileNameWithoutExtension(fileBaseNameAndPath), Path.GetExtension(fileBaseNameAndPath), fileRingSpecStr, expectedMaximumFileSize, PersistentFileWriteConfig.Fast, autoCreatePath) { }

        /// <summary>
        /// Fully detailed constructors
        /// </summary>
        /// <param name="fileBaseDirPath">Gives path of the file ring directory</param>
        /// <param name="fileBaseName">Gives the base file name to which ring spec characters are appended to generate the file names for the ring.</param>
        /// <param name="fileExtension">Gives the file extension that will be appended after the ring spec character</param>
        /// <param name="fileRingSpecStr">Gives the set of characters which are used to define the ring.  "ABC" gives 3 files path\fileA.ext, path\fileB.ext and path\fileC.ext.</param>
        /// <param name="expectedMaximumFileSize">Gives the pre-allocated serialization buffer size for save operations.</param>
        /// <param name="fileWriteConfig">Gives the PersistentFileWriteConfig that the client would like to use for writing these files.</param>
        /// <param name="autoCreatePath">Set to true to automatically attempt to create the directories required to get to the path given by the fileBaseNameAndPath</param>
        /// <exception cref="System.ArgumentException">
        /// If any of FileBaseDirPath, FileBaseName, FileRingSpecStr are null or empty, or if ExpectedMaximumFileSize if is not greater than zero.
        /// </exception>
        public PersistentObjectFileRingConfig(string fileBaseDirPath, string fileBaseName, string fileExtension, string fileRingSpecStr, int expectedMaximumFileSize, PersistentFileWriteConfig fileWriteConfig, bool autoCreatePath)
            : base(fileWriteConfig)
        {
            FileBaseDirPath = fileBaseDirPath;
            FileBaseName = fileBaseName;
            FileExtension = fileExtension;
            FileRingSpecStr = fileRingSpecStr;
            ExpectedMaximumFileSize = expectedMaximumFileSize;
            AutoCreatePath = autoCreatePath;
            AdvanceOnSaveRule = AdvanceOnSaveRule.Allways;
            AdvanceAfterNSaveFailures = 0;
            ThrowOnAnyLoadFailure = true;

            TestValues();
        }

        /// <summary>
        /// Copy constructor.  Generates a new object containing a copy of the information in the given rhs.
        /// </summary>
        /// <exception cref="System.ArgumentException">
        /// If any of FileBaseDirPath, FileBaseName, FileRingSpecStr are null or empty, or if ExpectedMaximumFileSize if is not greater than zero.
        /// </exception>
        public PersistentObjectFileRingConfig(PersistentObjectFileRingConfig rhs)
            : base(rhs)
        {
            FileBaseDirPath = rhs.FileBaseDirPath;
            FileBaseName = rhs.FileBaseName;
            FileExtension = rhs.FileExtension;
            FileRingSpecStr = rhs.FileRingSpecStr;
            FileExtension = rhs.FileExtension;
            ExpectedMaximumFileSize = rhs.ExpectedMaximumFileSize;
            AutoCreatePath = rhs.AutoCreatePath;
            AdvanceOnSaveRule = rhs.AdvanceOnSaveRule;
            AdvanceAfterNSaveFailures = rhs.AdvanceAfterNSaveFailures;
            ThrowOnAnyLoadFailure = rhs.ThrowOnAnyLoadFailure;

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

        /// <summary>
        /// Returns an array containing the expanded file path names for each of the possible file paths in this ring.
        /// </summary>
        public string [] GetFileRingPathArray()
        {
            int numFilesInRing = FileRingSpecStr.Length;
            List<string> fileRingPathList = new List<string>();
            for (int idx = 0; idx < numFilesInRing; idx++)
            {
                string name = String.Concat(FileBaseName, FileRingSpecStr.Substring(idx, 1));
                string path = Path.ChangeExtension(Path.Combine(FileBaseDirPath, name), FileExtension);
                fileRingPathList.Add(path);
            }

            return fileRingPathList.ToArray();
        }

	};

    #endregion

    #region PersistentStorageAdapater(s)

    /// <summary>
    /// This class is the Adapter base class used to support common file ring based code used within specific adapter derived classes.
    /// </summary>
    /// <typeparam name="ObjType">
    /// Defines the ObjType on which the IPersistentStorage operates.  Must be a class with default constructor that implements the IPersistSequenceable interface.
    /// </typeparam>
    public abstract class PersistentObjectFileRingStorageAdapterBase<ObjType> 
        : IPersistentStorage<ObjType>
        where ObjType : class, IPersistSequenceable, new()
    {
        /// <summary>
        /// Base class constructor.  caller must provide adapterInstanceName and ringConfig parameter values which are captured by the instance.
        /// <para/>constructor also sets the Object property to a new instance of the ObjType and invokes CreateDirectoryIfNeeded.
        /// </summary>
        public PersistentObjectFileRingStorageAdapterBase(string adapterInstanceName, PersistentObjectFileRingConfig ringConfig)
        {
            this.name = adapterInstanceName;
            this.ringConfig = new PersistentObjectFileRingConfig(ringConfig);  // create a local, invariant, copy of the caller provided ringConfig
            fileRingPathArray = ringConfig.GetFileRingPathArray();

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

        /// <summary>Captured adapterInstanceName from constructor</summary>
        protected string name;

        /// <summary>Captured file ring config from constructor</summary>
        protected PersistentObjectFileRingConfig ringConfig;

        string [] fileRingPathArray;
        int fileRingNextPathIdx = 0;
        UInt64 lastSavedSeqNum = 0;     // 0 is a magic number that means nothing has been saved or loaded
        string lastUsedFilePath = string.Empty;
        uint sequentialSaveFailures = 0;

        /// <summary>Resets the index into the ring of file names so that the next save will occur to the next ring element after the one indicated with the lastLoadedIndex.</summary>
        protected void ResetRingPosition(int lastLoadedIndex)
        {
            fileRingNextPathIdx = lastLoadedIndex;

            AdvanceRingPositionAfterSave(true); // use this method to increment and wrap from the given index
        }

        /// <summary>
        /// Advances the ring position based on if the saveSucceeded and how the AdvanceOnSaveRule is configured.  
        /// Next selected file name always advances on a successful save.
        /// </summary>
        protected void AdvanceRingPositionAfterSave(bool saveSucceeded)
        {
            bool advance = false;

            // decide if we should advance...
            if (saveSucceeded)
                advance = true;
            else
            {
                if (sequentialSaveFailures != uint.MaxValue)
                    sequentialSaveFailures++;

                switch (ringConfig.AdvanceOnSaveRule)
                {
                    default:
                    case AdvanceOnSaveRule.Allways: advance = true; break;
                    case AdvanceOnSaveRule.OnSuccessOnly: advance = false; break;
                    case AdvanceOnSaveRule.OnSuccessOrNFailures: advance = (sequentialSaveFailures >= ringConfig.AdvanceAfterNSaveFailures); break;
                }
            }

            if (advance)
                AdvanceRingPosition();
            // else we are staying on the current file.
        }

        /// <summary>
        /// Advances the ring position by incrementing the fileRingNextPathIdx and wrapping it if it has moved beyond the fileRingPathArray's end.
        /// Also sets sequentialSaveFailures to zero.
        /// </summary>
        protected void AdvanceRingPosition()
        {
            fileRingNextPathIdx++;
            if (fileRingNextPathIdx >= fileRingPathArray.Length)
                fileRingNextPathIdx = 0;
            sequentialSaveFailures = 0;
        }

        /// <summary>
        /// This field is set to true if/once the target directory path is known to exist (so we do not need to actually check each time).
        /// </summary>
        bool directoryPathKnownToExist = false;

        /// <summary>
        /// Checks if the target directory path for the file ring exists and if configured with AutoCreatePath, uses System.IO.Directory.CreateDirectory to create
        /// any missing directories in the path.  Once the directory is known to exit, sets directoryPathKnownToExist to block calls to System.IO.Directory.Exists. 
        /// </summary>
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

        /// <summary>
        /// Abstract method that is invoked to clear the last Loaded object, typically be setting it back to the default value.
        /// </summary>
        protected abstract void InnerClearObject();

        /// <summary>
        /// Abstratct method that is invoked to attempt to deserialize an ObjType object from the given readStream.
        /// </summary>
        protected abstract object InnerReadObject(System.IO.Stream readStream);

        /// <summary>
        /// Abstratct method that is invoked to attempt to serialize the given ObjType obj and write it to the given writeStream.
        /// </summary>
        protected abstract void InnerWriteObject(ObjType obj, System.IO.Stream writeStream);

        #endregion

        #region IPersistentStorage<ObjType> Members (et. al.)

        /// <summary>Gives use access to the object to which loads are completed and from which saves are performed.</summary>
        /// <remarks>
        /// The actual object returned by this property may change as a result of other actions performed on the underlying storage adapter object.
        /// </remarks>
        public ObjType Object { get; set; }

        /// <summary>Attempts to Read each of the files in the ring and returns the oldest valid deserialized object as measured by the contents of the PersistedVersionSequenceNumber</summary>
        /// <returns>True if a valid file was found and loaded, throws PersistentStorageException if no valid ring file were found (Object is set as new ObjType()).</returns>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to load or parse any existing file in the ring.  If possible Object will be updated with most recent file prior to throwing the report of such a failure or Object is set as new ObjType().</exception>
        public bool Load() 
        {
            string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
            return Load(true, methodName); 
        }

        /// <summary>Attempts to Read each of the files in the ring and returns the oldest valid deserialized object as measured by the contents of the PersistedVersionSequenceNumber</summary>
        /// <returns>True if a valid file was found and loaded, false if no ring files were found (Object is set as new ObjType()).</returns>
        public bool LoadNoThrow() 
        {
            string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
            return Load(false, methodName); 
        }

        /// <summary>Attempts to Read each of the files in the ring and returns the oldest valid deserialized object as measured by the contents of the PersistedVersionSequenceNumber</summary>
        /// <returns>True if a valid file was found and loaded, false or throw PersistentStorageExcpetion (based on allowThrow) if no valid ring file was found (Object is set as new ObjType()).</returns>
        /// <exception cref="PersistentStorageException">if allowThrow is true then this method throws PersistentStorageException on failure to load or parse any existing file in the ring or if there are not valid files in the ring.  If possible Object will be updated with most recent file prior to throwing the report of such a failure.</exception>
        /// <param name="allowThrow">Pass true to allow called method to throw on any error or false to prevent it from doing so.</param>
        public bool Load(bool allowThrow)
        {
            string methodName = Utils.Fcns.CheckedFormat("{0}()", new System.Diagnostics.StackFrame().GetMethod().Name, (allowThrow ? "allowThrow" : "doNotThrow"));
            return Load(allowThrow, methodName);
        }

        /// <summary>
        /// Common private implementation method for all Load variants
        /// </summary>
        private bool Load(bool allowThrow, string methodName)
        {
            // load and parse each of the files and keep the one that has the most recent sequence number
            // log any errors loading or parsing an existing file.

            ObjType choosenObj = null;
            int choosenObjIdx = -1;
            UInt64 choosenObjSeqNum = 0;
            string choosenObjPath = string.Empty;
            string zeroSeqNumPath = null;
            string duplicateSeqNumPath = null;
            int existingFileCount = 0;
            int validFileCount = 0;
            bool reloadAtEnd = false;
           
            System.Exception firstEx = null;

            for (int idx = 0; idx < fileRingPathArray.Length; idx++)
            {
                string trialPath = fileRingPathArray[idx];

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
                        else if (currentObjSeqNum == choosenObjSeqNum)
                            duplicateSeqNumPath = trialPath;
                        else
                            validFileCount++;

                        if (currentObjSeqNum >= choosenObjSeqNum)
                        {
                            choosenObj = currentObj;
                            choosenObjSeqNum = currentObjSeqNum;
                            choosenObjIdx = idx;
                            choosenObjPath = trialPath;
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

            bool loadedValidObject = (choosenObj != null);

            if (loadedValidObject && reloadAtEnd && Object.PersistedVersionSequenceNumber != choosenObjSeqNum)
            {
                loadedValidObject = false;  // be pessimistic
                try
                {
                    using (System.IO.FileStream fs = System.IO.File.OpenRead(choosenObjPath))
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
                        firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("Final reload failed: could not read object '{0}' from path '{1}'", typeof(ObjType), choosenObjPath), e);
                }
            }

            if (!loadedValidObject)
            {
                if (firstEx == null)
                {
                    if (existingFileCount != 0)
                        firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("No found file could be loaded (starting at '{0}'), using default object contents for '{1}'", fileRingPathArray[0], typeof(ObjType)));
                    else
                        firstEx = new PersistentStorageException(Utils.Fcns.CheckedFormat("No files were found (starting at '{0}'), using default object contents for '{1}'", fileRingPathArray[0], typeof(ObjType)));
                }

                InnerClearObject();
                choosenObj = Object;
                choosenObjIdx = fileRingPathArray.Length - 1;
                choosenObjPath = fileRingPathArray[choosenObjIdx];
                choosenObjSeqNum = 0;
            }

            ResetRingPosition(choosenObjIdx);

            if (!reloadAtEnd)
                Object = choosenObj;
            lastSavedSeqNum = choosenObjSeqNum;
            lastUsedFilePath = choosenObjPath;

            LastExecption = firstEx;

            bool returnSuccess = (loadedValidObject && (firstEx == null || !ringConfig.ThrowOnAnyLoadFailure));
            System.Exception throwEx = ((firstEx != null && allowThrow) ? firstEx : null);

            if (returnSuccess)
            {
                SuccessEmitter.Emit("{0} succeeded.  seqNum:{1} filePath:'{2}'", methodName, lastSavedSeqNum, lastUsedFilePath);
                return true;
            }
            else
            {
                returnSuccess = AttemptAutoSaveAfterFailedLoad(existingFileCount);

                if (returnSuccess)
                {
                    SuccessEmitter.Emit("{0} succeeded after autoSave.  seqNum:{1} filePath:'{2}'", methodName, lastSavedSeqNum, lastUsedFilePath);
                    return true;
                }
            }

            if (throwEx != null)
            {
                IssueEmitter.Emit("{0} failed: seqNum:{1} filePath:'{2}' throwing:{3}", methodName, lastSavedSeqNum, lastUsedFilePath, firstEx);

                throw throwEx;
            }
            else
            {
                IssueEmitter.Emit("{0} failed: seqNum:{1} filePath:'{2}' error:{3}", methodName, lastSavedSeqNum, lastUsedFilePath, firstEx);

                return false;
            }
        }

        /// <summary>
        /// Internal method used to implement AutoSaveAfterFailedLoad - checks each condition that matches a selectable AutoSaveCondition and 
        /// if a matching condition is found, it calls the private variant of Save passing it the current Object, noThrow, a AutoSave specic "methodName"
        /// and indicates that the LastException should not be updated by the Save method.  This method represents the common "sub-expression" for two
        /// paths out the bottom of Load (failed with throw and failed without throw).
        /// Returns true if an AutoSave was performed and it succeeded and AutoSaveConditions includes the SuccessfullSaveMakesLoadSucceed flag.
        /// Returns false otherwise.
        /// </summary>
        private bool AttemptAutoSaveAfterFailedLoad(int existingFileCount)
        {
            bool autoSaveCompletedSuccessfully = false;

            if (existingFileCount == 0 && ((AutoSaveConditions & AutoSaveConditions.OnLoadFailedWithNoFilesFound) != AutoSaveConditions.None))
            {
                autoSaveCompletedSuccessfully = Save(Object, false, "AutoSave-OnLoadFailedWithNoFilesFound", false);
            }
            else if ((AutoSaveConditions & AutoSaveConditions.OnAnyFailedLoad) != AutoSaveConditions.None)
            {
                autoSaveCompletedSuccessfully = Save(Object, false, "AutoSave-OnAnyFailedLoad", false);
            }

            if (autoSaveCompletedSuccessfully && (AutoSaveConditions & AutoSaveConditions.SuccessfullSaveMakesLoadSucceed) != AutoSaveConditions.None)
            {
                LastExecption = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Increments the current Object's PersistedVersionSequenceNumber (from the maximum of the object's current value and the last value written by the adapter), 
        /// and then serializes and saves the Object's contents to the next file in the ring.
        /// </summary>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        public void Save() 
        {
            string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
            Save(Object, true, methodName, true); 
        }

        /// <summary>Sets the given object's PersistedVersionSequenceNumber to one more than the maximum of the last value written and the object's current value, saves the given object as the reference object, and then serializes and saves it contents to the next file in the ring.</summary>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        public void Save(ObjType value) 
        {
            string methodName = Utils.Fcns.CheckedFormat("{0}({1})", new System.Diagnostics.StackFrame().GetMethod().Name, value);
            Save(value, true, methodName, true); 
        }

        /// <summary>Increments the current Object's PersistedVersionSequenceNumber, and then serializes and saves the Object's contents to the next file in the ring.</summary>
        /// <param name="allowThrow">Pass true to allow called method to throw on any error or false to prevent it from doing so.</param>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        /// <returns>True on success or failure (when allowThrow is false)</returns>
        public bool Save(bool allowThrow) 
        {
            string methodName = Utils.Fcns.CheckedFormat("{0}({1})", new System.Diagnostics.StackFrame().GetMethod().Name, (allowThrow ? "allowThrow" : "doNotThrow"));
            return Save(Object, allowThrow, methodName, true); 
        }

        /// <summary>Increments the current Object's PersistedVersionSequenceNumber, and then serializes and saves the Object's contents to the next file in the ring.</summary>
        /// <returns>True on success or failure</returns>
        public bool SaveNoThrow() 
        { 
            string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
            return Save(Object, false, methodName, true); 
        }

        /// <summary>Sets the given object's PersistedVersionSequenceNumber to one more than the maximum of the last value written and the object's current value, saves the given object as the reference object, and then serializes and saves it contents to the next file in the ring.</summary>
        /// <param name="value">Pass in handle to object to Save (will have PersistedVersionSequenceNumber set to next value).  If null is passed, method will save constructor default and fail with an appropriate error.</param>
        /// <param name="allowThrow">Pass true to allow called method to throw on any error or false to prevent it from doing so.</param>
        /// <exception cref="PersistentStorageException">throws PersistentStorageException on failure to update, serialize, or write the Object's contents</exception>
        /// <returns>True on success or failure (when allowThrow is false)</returns>
        public bool Save(ObjType value, bool allowThrow)
        {
            string methodName = Utils.Fcns.CheckedFormat("{0}({1}, {2})", new System.Diagnostics.StackFrame().GetMethod().Name, value, (allowThrow ? "allowThrow" : "doNotThrow"));
            return Save(value, allowThrow, methodName, true);
        }

        /// <summary>
        /// Common private implementation method for all Save variants
        /// </summary>
        private bool Save(ObjType value, bool allowThrow, string methodName, bool updateLastException)
        {
            System.Exception firstEx = null;

            if (value != null)
                Object = value;
            else
            {
                if (firstEx == null)
                    firstEx = new PersistentStorageException("Attempt to save null object - saving default contents instead");

                InnerClearObject();
            }

            if (Object != null)
            {
                // set the seqNum to save as 1 plus the maximum of the last saved seq num and the value from the Object (in case it has been modified elsewhere or in case the object has been changed)
                UInt64 seqNum = 1 + Math.Max(lastSavedSeqNum, Object.PersistedVersionSequenceNumber);

                // save the new value in both the object and the lastSavedSeqNum.
                Object.PersistedVersionSequenceNumber = lastSavedSeqNum = seqNum;
            }

            string filePath = string.Empty;

            try
            {
                CreateDirectoryIfNeeded();

                filePath = fileRingPathArray[fileRingNextPathIdx];

                System.IO.FileOptions fileOptions = System.IO.FileOptions.SequentialScan
                                                  | (ringConfig.UseWriteThroughSemantics 
                                                     ? System.IO.FileOptions.WriteThrough 
                                                     : System.IO.FileOptions.None);

                using (System.IO.FileStream fs = System.IO.File.Create(filePath, ringConfig.ExpectedMaximumFileSize, fileOptions))
                {
                    InnerWriteObject(Object, fs);

                    if (ringConfig.UseFlushFileBuffersAfterWrite)
                    {
                        fs.Flush();     // flush any buffered data in the stream to the OS level before calling FlushFileBuffers

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

            if (updateLastException)
                LastExecption = firstEx;

            bool saveSucceeded = (firstEx == null);

            AdvanceRingPositionAfterSave(saveSucceeded);

            if (firstEx != null && allowThrow)
            {
                IssueEmitter.Emit("{0} failed: seqNum:{1} filePath:'{2}' throwing:{3}", methodName, lastSavedSeqNum, lastUsedFilePath, firstEx);

                throw firstEx;
            }
            else if (saveSucceeded)
            {
                SuccessEmitter.Emit("{0} succeeded.  seqNum:{1} filePath:'{2}'", methodName, lastSavedSeqNum, lastUsedFilePath);

                return true;
            }
            else
            {
                IssueEmitter.Emit("{0} failed: seqNum:{1} filePath:'{2}' error:{3}", methodName, lastSavedSeqNum, lastUsedFilePath, firstEx);

                return false;
            }
        }

        /// <summary>
        /// Tells engine to advance the internal ring pointer so that the next Save attempt will skip the last selected ring element.  
        /// This is primarily intended to support custom advance rules when used with AdvanceOnSaveRule.OnSuccessOnly
        /// </summary>
        public void AdvanceToNextFile()
        {
            AdvanceRingPosition();
        }

        /// <summary> Returns the path to the most recently loaded or saved file</summary>
        public string LastObjectFilePath { get { return lastUsedFilePath; } }

        /// <summary> Returns the exception generated by the last Load or Save operation or null if that operation was successful.</summary>
        public System.Exception LastExecption { get; private set; }

        /// <summary>
        /// Allows a client to provide an emitter to use for logging when issues are detected during unsuccessful Load and/or Save operations.  
        /// Setting this property to null us functionally identical to setting the property to MosaicLib.Logging.NullEmitter.
        /// </summary>
        public Logging.IMesgEmitter IssueEmitter { get { return IssueEmitterContainer.Emitter; } set { IssueEmitterContainer.Emitter = value; } }

        /// <summary>
        /// Allows a client to provide an emitter to use for logging successful use of Load and/or Save operations.  
        /// Setting this property to null us functionally identical to setting the property to MosaicLib.Logging.NullEmitter.
        /// </summary>
        public Logging.IMesgEmitter SuccessEmitter { get { return SuccessEmitterContainer.Emitter; } set { SuccessEmitterContainer.Emitter = value; } }

        private Logging.MesgEmitterContainer IssueEmitterContainer = new Logging.MesgEmitterContainer();
        private Logging.MesgEmitterContainer SuccessEmitterContainer = new Logging.MesgEmitterContainer();


        /// <summary>
        /// Defines the conditions under which a Save is attempted immediately after a Load fails.
        /// </summary>
        public AutoSaveConditions AutoSaveConditions { get; set; }

        #endregion
    }

    /// <summary>
    /// This class is the most commonly used type that implements the IPersistentStorage interface.
    /// This class uses a DataContractSerializer combined with XmlReader and XmlWriter objects to attempt to load objects
    /// from the configured ring of files and to write objects to the next file in the ring when requested.
    /// This class is based on the PersistentObjectFileRingStorageAdapterBase class that implements most of the
    /// ring specific logic.
    /// </summary>
    /// <typeparam name="ObjType">
    /// Defines the ObjType on which the IPersistentStorage operates.  Must be a class with default constructor that implements the IPersistSequenceable interface.
    /// </typeparam>
    public class DataContractPersistentXmlTextFileRingStorageAdapter<ObjType>
        : PersistentObjectFileRingStorageAdapterBase<ObjType>
        , IPersistentStorage<ObjType>
        where ObjType : class, IPersistSequenceable, new()
    {
        /// <summary>
        /// Required constructor.  Takes given name and ringConfig.
        /// <para/>Sets ConformanceLevel to Auto, Encoding to ASCII, and Indent to True
        /// </summary>
        public DataContractPersistentXmlTextFileRingStorageAdapter(string name, PersistentObjectFileRingConfig ringConfig)
            : base(name, ringConfig)
        {
            xws.ConformanceLevel = System.Xml.ConformanceLevel.Auto;
            xws.Encoding = System.Text.Encoding.ASCII;
            xws.Indent = true;
            xws.CloseOutput = false;     // we will explicitly close the underlying stream
        }

        System.Xml.XmlWriterSettings xws = new System.Xml.XmlWriterSettings();
        DataContractSerializer dcs = new DataContractSerializer(typeof(ObjType));

        /// <summary>
        /// Used to reset the last loaded object its default contents.
        /// <para/>Sets Object = new ObjType();
        /// </summary>
        protected override void InnerClearObject()
        {
            Object = new ObjType();
        }

        /// <summary>
        /// Implementation for required abstract method.  Asks the contained DataContractSerializer instance to read an object from the given readStream using the ReadObject method.
        /// </summary>
        protected override object InnerReadObject(System.IO.Stream readStream)
        {
            return dcs.ReadObject(readStream);
        }

        /// <summary>
        /// Implementation for required abstract method.  Asks the contained DataContractSerializer instance to write the given object to the given writeStream using the WriteObject method.
        /// </summary>
        protected override void InnerWriteObject(ObjType obj, System.IO.Stream writeStream)
        {
            using (System.Xml.XmlWriter xw = System.Xml.XmlWriter.Create(writeStream, xws))
            {
                dcs.WriteObject(xw, obj);
                xw.Flush();
            }
        }
    }

    /// <summary>
    /// This is a kernel32.dll Wind32 API access helper class that allows us to call FlushFileBuffers on a specific file handle in order to
    /// force the file system to fully commit corresponding file write data to permanent storage when configured and desired.
    /// </summary>
    public static partial class LocalWin32API
    {
        /// <summary>
        /// P-Invoke interface to Win32 FlushFileBuffers API method.  Generally causes the OS to flush both its memory cache for the given file and to block
        /// the caller until the hardware has committed all prior issued writes to persistent storage so that the side effects of any such changes will be retained, complete
        /// and visible even if the power is interrupted right after the method returns.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlushFileBuffers(System.Runtime.InteropServices.SafeHandle hFile);
    }

    #endregion
}

//-------------------------------------------------------------------
