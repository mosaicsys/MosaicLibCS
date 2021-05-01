//-------------------------------------------------------------------
/*! @file MDRF2Writer.cs
 *  @brief classes that are used to support writing MDRF2 (Mosaic Data Recording Format 2) files.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2020 Mosaic Systems Inc.
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
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using MosaicLib;
using MosaicLib.File;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.Semi.E005.Data;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Utils.StringMatching;

using Mosaic.ToolsLib.BufferWriter;
using Mosaic.ToolsLib.MessagePackUtils;

using MDRF = MosaicLib.PartsLib.Tools.MDRF;
using Utils = MosaicLib.Utils;
using MosaicLib.PartsLib.Tools.MDRF.Writer;
using Mosaic.ToolsLib.MDRF2.Common;
using Mosaic.ToolsLib.Compression;

namespace Mosaic.ToolsLib.MDRF2.Writer
{
    #region MDRF2Writer implementation

    /// <summary>
    /// This interface gives the methods and properties that the IMDRF2Writer supports on top of the normal set that the IMDRFWriter supports (on which this interface is based)
    /// </summary>
    public interface IMDRF2Writer : MDRF.Writer.IMDRFWriter
    {
        /// <summary>Method used to record an object into the current MDRF2 file</summary>
        string RecordObject(object obj, DateTimeStampPair dtPairIn = null, bool writeAll = false, bool forceFlush = false, bool isSignificant = false, ulong userRowFlagBits = 0);

        /// <summary>Gives the total number of client operations that this writer has been asked to perform.  This allows a client to tell if other clients have used the writer recently.</summary>
        ulong ClientOpRequestCount { get; }
    }

    /// <summary>
    /// configuration used to define the behavior of an MDRF2Writer
    /// </summary>
    public class MDRF2WriterConfig : ICopyable<MDRF2WriterConfig>
    {
        /// <summary>Specifies the partID of the writer</summary>
        public string PartID { get; set; }

        /// <summary>Specifies the SetupInfo to be included in the MDRF2 file(s).  Defaults to SetupInfo.DefaultforMDRF2.</summary>
        public SetupInfo SetupInfo { get; set; } = SetupInfo.DefaultForMDRF2;

        /// <summary>Gives the set of group definitions to use.  Set to null to create the default group.  The empty array (this default) selects that no groups shall be created or used.</summary>
        public MDRF.Writer.GroupInfo[] GroupInfoArray { get; set; } = EmptyArrayFactory<MDRF.Writer.GroupInfo>.Instance;

        /// <summary>Gives the set of occurrence definitions to use.  Set to null to create the default occurrences.  The empty array (this default) selects that no occurrences are created.</summary>
        public MDRF.Writer.OccurrenceInfo[] OccurrenceInfoArray { get; set; } = EmptyArrayFactory<MDRF.Writer.OccurrenceInfo>.Instance;

        /// <summary>Selects the writer behavior.  Defaults to MDRF2WriterConfigBehavior.EnableAPILocking.  Set to None to disable API locking (so as to increase performance at the expense of built in thread safety).</summary>
        public MDRF2WriterConfigBehavior WriterBehavior { get; set; } = MDRF2WriterConfigBehavior.EnableAPILocking;

        /// <summary>Gives </summary>
        public CompressorSelect CompressorSelect { get; set; } = CompressorSelect.LZ4;

        /// <summary>When the CompressorSelect is not set to None, this gives the desired compression level for the selected Compressor type.  Defaults to 2.</summary>
        /// <remarks>
        /// Values generally range from 0 to 15 depending on compressor engine type.  This value is clipped to the supported range for the selected compressor type.
        /// 0 means least (or no) compression.  For GZip this disables compression.
        /// 1 is the fastest selectable compression which produces the least compression cost.
        /// 2 appears to be the sweet spot for both LZ4 and 2 or 3 for GZip compression - good tradoff of compression rate/cost and decompression speed.
        /// </remarks>
        public int CompressionLevel { get; set; } = 2;

        /// <summary>Gives the MesgType that is used when the writer is recording a message.  Defaults to MesgType.Debug</summary>
        public Logging.MesgType RecordMessageMesgType { get; set; } = Logging.MesgType.Debug;

        /// <summary>Gives the MesgType that is used when the writer is recording an error message.  Defaults to MesgType.Debug</summary>
        public Logging.MesgType RecordErrorMesgType { get; set; } = Logging.MesgType.Debug;

        /// <summary>Implementation for ICopyable interface.</summary>
        public MDRF2WriterConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (MDRF2WriterConfig)this.MemberwiseClone();
        }
    }

    /// <summary>
    /// Used to select the behaviors that are to be implemented by the MDRF2Writer
    /// <para/>None (0x00), EnableAPILocking (0x01)
    /// </summary>
    [Flags]
    public enum MDRF2WriterConfigBehavior : int
    {
        /// <summary>Placeholder default value.  [0x00]</summary>
        None = 0x00,

        /// <summary>When this flag is selected it requests the writer to use a mutex to control access to the public API methods so that the instance may be safely used by more than one thread. [0x01]</summary>
        EnableAPILocking = 0x01,
    }

    /// <summary>
    /// This class is used to create and write a sequence of MDRF2 file (using optional compression).  
    /// These files are based on the use of the MessagePack format.  See Remarks section below for more details.
    /// </summary>
    /// <remarks>
    /// The MDRF2 file is an, optionally lz4 compressed, sequence of one or more MessagePack records.  
    /// Nominally these records are grouped, and optionally compressed, and written together to the output file as a block
    /// Each block starts with what is called an inline index header record which gives some summary information about the records that are in the block
    /// and includes the length of the block so that the reader can skip fully decoding the block if it is easily known to contain no information of note.
    /// Within each block there will be one or more records.  The first block in the file is always used to contain the file meta data.
    /// All blocks thereafter are used to store timestamp, group, occurrance, messages, and records with other recorded data formats.
    /// This writer attempts to accumulate group and occurrence related records until either the block reaches a desired minimum nominal size, 
    /// or the age of the first record in the block is overly long.  At this point the block being accumulated is written out and the next block is started.
    /// </remarks>
    public class MDRF2Writer : SimplePartBase, IMDRF2Writer
    {
        #region Construction, Release, call-chainable Add variants, and supporting fields

        /// <summary>
        /// Constructor.
        /// </summary>
        public MDRF2Writer(MDRF2WriterConfig writerConfig, Stream externallyProvidedFileStream = null)
            : base(writerConfig.PartID)
        {
            WriterConfig = writerConfig.MakeCopyOfThis();
            ExternallyProvidedFileStream = externallyProvidedFileStream;

            compressorSelect = WriterConfig.CompressorSelect;

            mutex = ((WriterConfig.WriterBehavior & MDRF2WriterConfigBehavior.EnableAPILocking) != 0) ? new object() : null;

            AddExplicitDisposeAction(Release);

            // validate selected fields
            SetupResultCode = string.Empty;

            var setupInfo = WriterConfig.SetupInfo;
            if (setupInfo == null)
                SetupResultCode = SetupResultCode.MapNullOrEmptyTo("{0} failed: given setupInfo cannot be null".CheckedFormat(CurrentMethodName));

            setupOrig = setupInfo ?? defaultSetupInfo;
            setup = new SetupInfo(setupOrig).MapDefaultsTo(defaultSetupInfo).ClipValues();

            writerBehavior = setup.ClientNVS.MapNullToEmpty()["WriterBehavior"].VC.GetValue<MDRF.Writer.WriterBehavior>(false);

            if (!WriterConfig.GroupInfoArray.IsNullOrEmpty())
            {
                groupInfoList.AddRange(WriterConfig.GroupInfoArray.WhereIsNotDefault());
                groupOrOccurrenceInfoListModified = true;
            }

            if (!WriterConfig.OccurrenceInfoArray.IsNullOrEmpty())
            {
                occurrenceInfoList.AddRange(WriterConfig.OccurrenceInfoArray.WhereIsNotDefault());
                groupOrOccurrenceInfoListModified = true;
            }

            ReassignIDsAndBuildNewTrackersIfNeeded(forceRebuild: true);

            // create the directory if needed.

            if (setup.DirPath.IsNullOrEmpty())
                SetupResultCode = SetupResultCode.MapNullOrEmptyTo("{0} failed: given DirPath cannot be null or empty".CheckedFormat(CurrentMethodName));
            else
                setup.DirPath = System.IO.Path.GetFullPath(setup.DirPath);

            if (!System.IO.Directory.Exists(setup.DirPath ?? string.Empty))
            {
                if (!setup.CreateDirectoryIfNeeded)
                    SetupResultCode = SetupResultCode.MapNullOrEmptyTo("{0} failed: given DirPath '{1}' was not found or is not a directory".CheckedFormat(CurrentMethodName, setupOrig.DirPath));
                else
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(setup.DirPath);
                    }
                    catch (System.Exception ex)
                    {
                        SetupResultCode = SetupResultCode.MapNullOrEmptyTo("{0} failed: CreateDirectory '{1}': {2}".CheckedFormat(CurrentMethodName, setup.DirPath, ex.ToString(ExceptionFormat.TypeAndMessage)));
                    }
                }
            }
        }

        private MDRF2WriterConfig WriterConfig { get; set; }
        private Stream ExternallyProvidedFileStream { get; set; }

        private CompressorSelect compressorSelect;
        private static readonly MessagePack.MessagePackSerializerOptions mpOptions = Instances.VCDefaultMPOptions;

        public void Release()
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                try
                {
                    InnerNoteClientOpRequested();

                    var dtPair = MDRF2DateTimeStampPair.Now;

                    InnerFinishAndCloseCurrentFile("On Release or Dispose", ref dtPair);

                    Fcns.DisposeOfObject(ref metaDataBlockBufferWriter);
                    Fcns.DisposeOfObject(ref inlineIndexBlockBufferWriter);
                    Fcns.DisposeOfObject(ref blockBufferWriter);
                }
                catch (System.Exception ex)
                {
                    RecordError("{0} failed: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)));
                }
                finally
                {
                    fileStream = null;
                    outputStream = null;
                    compressorStream = null;

                    metaDataBlockBufferWriter = null;
                    inlineIndexBlockBufferWriter = null;
                    blockBufferWriter = null;
                }
            }
        }

        /// <inheritdoc/>
        public MDRF2Writer Add(MDRF.Writer.GroupInfo groupInfo)
        {
            return Add(new[] { groupInfo });
        }

        /// <inheritdoc/>
        public MDRF2Writer Add(params MDRF.Writer.GroupInfo[] groupInfoParamsArray)
        {
            return AddRange(groupInfoParamsArray ?? EmptyArrayFactory<MDRF.Writer.GroupInfo>.Instance);
        }

        /// <inheritdoc/>
        public MDRF2Writer AddRange(IEnumerable<MDRF.Writer.GroupInfo> groupInfoSet)
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                InnerNoteClientOpRequested();

                groupInfoList.AddRange(groupInfoSet.Where(gi => gi != null));
                groupOrOccurrenceInfoListModified = true;
            }

            return this;
        }

        /// <inheritdoc/>
        public MDRF2Writer Add(MDRF.Writer.OccurrenceInfo occurrenceInfo)
        {
            return Add(new[] { occurrenceInfo });
        }

        /// <inheritdoc/>
        public MDRF2Writer Add(params MDRF.Writer.OccurrenceInfo[] occurrenceInfoParamsArray)
        {
            return AddRange(occurrenceInfoParamsArray ?? EmptyArrayFactory<MDRF.Writer.OccurrenceInfo>.Instance);
        }

        /// <inheritdoc/>
        public MDRF2Writer AddRange(IEnumerable<MDRF.Writer.OccurrenceInfo> occurrenceInfoSet)
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                InnerNoteClientOpRequested();

                occurrenceInfoList.AddRange(occurrenceInfoSet.Where(oi => oi != null));
                groupOrOccurrenceInfoListModified = true;
            }

            return this;
        }

        /// <summary>
        /// This string returns the success/failure code for the set of setup steps that are performed during object construction.
        /// </summary>
        public string SetupResultCode { get; set; }

        List<MDRF.Writer.GroupInfo> groupInfoList = new List<MDRF.Writer.GroupInfo>();
        List<MDRF.Writer.OccurrenceInfo> occurrenceInfoList = new List<MDRF.Writer.OccurrenceInfo>();
        bool groupOrOccurrenceInfoListModified = false;

        static readonly SetupInfo defaultSetupInfo = SetupInfo.DefaultForMDRF2;

        #endregion

        #region IMDRF2Writer implementation methods (both interface specific methods and general public methods that support the interface)

        MDRF.Writer.IMDRFWriter MDRF.Writer.IMDRFWriter.Add(params MDRF.Writer.GroupInfo[] groupInfoParamsArray) { return AddRange(groupInfoParamsArray ?? EmptyArrayFactory<MDRF.Writer.GroupInfo>.Instance); }
        MDRF.Writer.IMDRFWriter MDRF.Writer.IMDRFWriter.AddRange(IEnumerable<MDRF.Writer.GroupInfo> groupInfoSet) { return AddRange(groupInfoSet); }
        MDRF.Writer.IMDRFWriter MDRF.Writer.IMDRFWriter.Add(params MDRF.Writer.OccurrenceInfo[] occurrenceInfoParamsArray) { return AddRange(occurrenceInfoParamsArray ?? EmptyArrayFactory<MDRF.Writer.OccurrenceInfo>.Instance); }
        MDRF.Writer.IMDRFWriter MDRF.Writer.IMDRFWriter.AddRange(IEnumerable<MDRF.Writer.OccurrenceInfo> occurrenceInfoSet) { return AddRange(occurrenceInfoSet); }

        /// <inheritdoc/>
        public string RecordGroups(bool writeAll = false, DateTimeStampPair dtPairIn = null)
        {
            if (IsDisposed)
                return "{0} has already been disposed".CheckedFormat(CurrentClassLeafName);

            using (var scopedLock = new ScopedLock(mutex))
            {
                InnerNoteClientOpRequested();

                MDRF2DateTimeStampPair dtPair = (dtPairIn != null) ? new MDRF2DateTimeStampPair(dtPairIn, setUTCTimeSince1601IfNeeded: false) : MDRF2DateTimeStampPair.NowQPCOnly;

                return InnerRecordGroups(writeAll, ref dtPair, writerBehavior);
            }
        }

        private string InnerRecordGroups(bool writeAll, ref MDRF2DateTimeStampPair dtPair, MDRF.Writer.WriterBehavior writerBehaviorParam)
        {
            {
                string ec = ActivateFile(ref dtPair);

                if (!ec.IsNullOrEmpty())
                {
                    droppedCounts.IncrementDataGroup();
                    return ec;
                }

                QpcTimeStamp tsNow = dtPair.QpcTimeStamp;

                // check if we need to do a writeAll
                if (!writeAll)
                {
                    // if this is the first call or the MinNominalWriteAllInterval has elapsed then make this a writeall call
                    if (lastWriteAllTimeStamp.IsZero || ((tsNow - lastWriteAllTimeStamp) >= setup.MinNominalWriteAllInterval))
                        writeAll = true;
                }

                int touchedGTCount = 0;
                foreach (GroupTracker groupTracker in groupTrackerArray)
                {
                    groupTracker.numTouchedSources = 0; // clear numTouchedSources count at start of each service loop
                    groupTracker.Service(writeAll);
                    touchedGTCount += groupTracker.touched ? 1 : 0;
                }

                // generate and write the group data blocks themselves
                if (ec.IsNullOrEmpty() && (writeAll || (touchedGTCount > 0)))
                {
                    try
                    {
                        StartNewBlockIfNeeded(blockBufferWriter, ref dtPair);
                        var mpWriter = new MessagePack.MessagePackWriter(blockBufferWriter);

                        UpdateFileDeltaTimeStampIfNeeded(ref mpWriter, ref dtPair);

                        FileIndexRowFlagBits blockFlagBits = FileIndexRowFlagBits.ContainsGroup | (writeAll ? FileIndexRowFlagBits.ContainsStartOfFullGroup : FileIndexRowFlagBits.None);

                        foreach (GroupTracker groupTracker in groupTrackerArray)
                        {
                            MDRF.Writer.GroupInfo groupInfo = groupTracker?.groupInfo;

                            if (groupTracker != null && groupTracker.touched)
                            {
                                // [L1 +GroupID [Ln pointValues]]
                                mpWriter.WriteArrayHeader(2);
                                mpWriter.Write(groupTracker.ClientID);    // these will be positive values

                                var numPoints = groupTracker.sourceTrackerArrayLength;
                                mpWriter.WriteArrayHeader(numPoints);

                                for (int pointIdx = 0; pointIdx < numPoints; pointIdx++)
                                    mpWriter.SerializeVC(groupTracker.sourceTrackerArray[pointIdx].lastServicedValue, mpOptions);

                                NoteRecordAddedToBuffer(blockFlagBits, groupInfo.FileIndexUserRowFlagBits);

                                groupTracker.NoteWritten();
                            }
                            else
                            {
                                // otherwise we do not write the group at all (see Service for details)
                            }
                        }

                        mpWriter.Flush();

                        inlineIndexRecord.BlockGroupSetCount += 1;
                    }
                    catch (System.Exception ex)
                    {
                        ec = "{0} failed: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                    }
                }

                if (ec.IsNullOrEmpty())
                    ec = ServicePendingWrites(ref dtPair);

                if (ec.IsNullOrEmpty())
                    ec = FinishAndWriteCurrentBlockIfNeeded(blockBufferWriter, ref dtPair);

                if (ec.IsNullOrEmpty() && ((writerBehaviorParam & MDRF.Writer.WriterBehavior.FlushAfterEveryGroupWrite) != 0))
                    ec = InnerFlush(MDRF.Writer.FlushFlags.All, ref dtPair, false);

                if (ec.IsNullOrEmpty() && writeAll)
                    lastWriteAllTimeStamp = tsNow;

                if (!ec.IsNullOrEmpty())
                {
                    droppedCounts.IncrementDataGroup();
                    RecordError(ec);
                    InnerCloseFile(ref dtPair);
                }

                return ec;
            }
        }

        /// <inheritdoc/>
        public string RecordOccurrence(IOccurrenceInfo occurrenceInfo, object dataValue, DateTimeStampPair dtPairIn = null, bool writeAll = false, bool forceFlush = false)
        {
            return RecordOccurrence(occurrenceInfo, ValueContainer.CreateFromObject(dataValue), dtPairIn: dtPairIn, writeAll: writeAll, forceFlush: forceFlush);
        }

        /// <inheritdoc/>
        public string RecordOccurrence(IOccurrenceInfo occurrenceInfo, ValueContainer dataVC = default(ValueContainer), DateTimeStampPair dtPairIn = null, bool writeAll = false, bool forceFlush = false)
        {
            if (occurrenceInfo == null)
                return "{0} failed:  occurrenceInfo parameter cannot be null".CheckedFormat(CurrentMethodName);

            if (IsDisposed)
                return "{0} has already been disposed".CheckedFormat(CurrentClassLeafName);

            using (var scopedLock = new ScopedLock(mutex))
            {
                InnerNoteClientOpRequested();

                MDRF2DateTimeStampPair dtPair = (dtPairIn != null) ? new MDRF2DateTimeStampPair(dtPairIn, setUTCTimeSince1601IfNeeded: false) : MDRF2DateTimeStampPair.NowQPCOnly;

                ReassignIDsAndBuildNewTrackersIfNeeded();

                OccurrenceTracker occurrenceTracker = occurrenceTrackerArray.SafeAccess(occurrenceInfo.OccurrenceID - 1);

                if (occurrenceTracker == null)
                {
                    if (occurrenceInfo.OccurrenceID == 0)
                        return RecordError("{0} failed: no tracker found for occurrence {1} (probably was not registered)".CheckedFormat(CurrentMethodName, occurrenceInfo));
                    else
                        return RecordError("{0} failed: no tracker found for occurrence {1}".CheckedFormat(CurrentMethodName, occurrenceInfo));
                }

                if (occurrenceTracker.mdci.CST != ContainerStorageType.None && occurrenceTracker.mdci.CST != dataVC.cvt)
                    return RecordError($"{CurrentMethodName} failed on {occurrenceInfo}: data {dataVC} is not compatible with required cst:{occurrenceTracker.mdci.CST}");

                writeAll |= ((writerBehavior & MDRF.Writer.WriterBehavior.WriteAllBeforeEveryOccurrence) != 0);

                string ec = ActivateFile(ref dtPair);

                if (writeAll || ((writerBehavior & MDRF.Writer.WriterBehavior.WriteGroupsBeforeEveryOccurrence) != 0))
                    InnerRecordGroups(writeAll, ref dtPair, MDRF.Writer.WriterBehavior.None);        // no "special" writer behaviors are selected when recursively using RecordGroups

                if (ec.IsNullOrEmpty())
                {
                    try
                    {
                        StartNewBlockIfNeeded(blockBufferWriter, ref dtPair);

                        var mpWriter = new MessagePack.MessagePackWriter(blockBufferWriter);

                        UpdateFileDeltaTimeStampIfNeeded(ref mpWriter, ref dtPair);

                        // [L2 -OccurrenceID dataVC]
                        mpWriter.WriteArrayHeader(2);
                        mpWriter.Write(-occurrenceInfo.ClientID);   // ClientID is positive so this will always be negative.
                        mpWriter.SerializeVC(dataVC);

                        mpWriter.Flush();

                        var rowFlagBits = (occurrenceInfo.IsHighPriority ? FileIndexRowFlagBits.ContainsHighPriorityOccurrence : FileIndexRowFlagBits.ContainsOccurrence);

                        ec = NoteRecordAddedToBufferAndWriteIfNeeded(blockBufferWriter, ref dtPair, rowFlagBits, occurrenceInfo.FileIndexUserRowFlagBits);
                    }
                    catch (System.Exception ex)
                    {
                        ec = "{0} failed: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                    }
                }

                if (ec.IsNullOrEmpty() && (forceFlush || ((writerBehavior & MDRF.Writer.WriterBehavior.FlushAfterEveryOccurrence) != 0)))
                    ec = InnerFlush(MDRF.Writer.FlushFlags.All, ref dtPair, false);

                if (!ec.IsNullOrEmpty())
                    RecordError(ec);

                return ec;
            }
        }

        /// <inheritdoc/>
        public string RecordObject(object obj, DateTimeStampPair dtPairIn = null, bool writeAll = false, bool forceFlush = false, bool isSignificant = false, ulong userRowFlagBits = 0)
        {
            var oType = obj?.GetType();

            if (IsDisposed)
                return "{0} has already been disposed".CheckedFormat(CurrentClassLeafName);

            using (var scopedLock = new ScopedLock(mutex))
            {
                InnerNoteClientOpRequested();

                MDRF2DateTimeStampPair dtPair = (dtPairIn != null) ? new MDRF2DateTimeStampPair(dtPairIn, setUTCTimeSince1601IfNeeded: false) : MDRF2DateTimeStampPair.NowQPCOnly;

                writeAll |= ((writerBehavior & MDRF.Writer.WriterBehavior.WriteAllBeforeEveryObject) != 0);

                string ec = ActivateFile(ref dtPair);

                if (writeAll || ((writerBehavior & MDRF.Writer.WriterBehavior.WriteGroupsBeforeEveryObject) != 0))
                    InnerRecordGroups(writeAll, ref dtPair, MDRF.Writer.WriterBehavior.None);        // no "special" writer behaviors are selected when recursively using RecordGroups

                if (ec.IsNullOrEmpty())
                {
                    try
                    {
                        StartNewBlockIfNeeded(blockBufferWriter, ref dtPair);

                        var mpWriter = new MessagePack.MessagePackWriter(blockBufferWriter);

                        UpdateFileDeltaTimeStampIfNeeded(ref mpWriter, ref dtPair);

                        // [L3 userRowFlagBits objectKnownType serializedObject]
                        // older version was [L3 objectKnownType serializedObject nil]
                        mpWriter.WriteArrayHeader(3);

                        if (obj == null)
                        {
                            mpWriter.Write(userRowFlagBits);
                            mpWriter.WriteNil();
                            mpWriter.WriteNil();
                        }
                        else if (oType == typeof(MosaicLib.Logging.LogMessage))
                        {
                            mpWriter.Write(userRowFlagBits);
                            mpWriter.Write(Common.Constants.ObjKnownType_LogMessage);
                            LogMessageFormatter.Instance.Serialize(ref mpWriter, obj as Logging.ILogMessage, mpOptions);
                        }
                        else if (typeof(MosaicLib.Semi.E039.IE039Object).IsAssignableFrom(oType))
                        {
                            mpWriter.Write(userRowFlagBits);
                            mpWriter.Write(Common.Constants.ObjKnownType_E039Object);
                            E039ObjectFormatter.Instance.Serialize(ref mpWriter, obj as MosaicLib.Semi.E039.IE039Object, mpOptions);
                        }
                        else if (oType == typeof(MosaicLib.Modular.Common.ValueContainer))
                        {
                            mpWriter.Write(userRowFlagBits);
                            mpWriter.Write(Common.Constants.ObjKnownType_ValueContainer);
                            VCFormatter.Instance.Serialize(ref mpWriter, (ValueContainer)obj, mpOptions);
                        }
                        else
                        {
                            var customSerializer = MosaicLib.Modular.Common.CustomSerialization.CustomSerialization.Instance.GetCustomTypeSerializerItemFor(oType);
                            var tavc = customSerializer.Serialize(obj);

                            mpWriter.Write(userRowFlagBits);
                            mpWriter.Write(Common.Constants.ObjKnownType_TypeAndValueCarrier);
                            TypeAndValueCarrierFormatter.Instance.Serialize(ref mpWriter, tavc, mpOptions);
                        }

                        mpWriter.Flush();

                        ec = NoteRecordAddedToBufferAndWriteIfNeeded(blockBufferWriter, ref dtPair, isSignificant ? FileIndexRowFlagBits.ContainsSignificantObject : FileIndexRowFlagBits.ContainsObject, userRowFlagBits);
                    }
                    catch (System.Exception ex)
                    {
                        ec = "{0} failed: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                    }
                }

                if (ec.IsNullOrEmpty() && (forceFlush || ((writerBehavior & MDRF.Writer.WriterBehavior.FlushAfterEveryObject) != 0)))
                    ec = InnerFlush(MDRF.Writer.FlushFlags.All, ref dtPair, false);

                if (!ec.IsNullOrEmpty())
                    RecordError(ec);

                return ec;
            }
        }

        /// <inheritdoc/>
        public string Flush(MDRF.Writer.FlushFlags flushFlags = MDRF.Writer.FlushFlags.All, DateTimeStampPair dtPairIn = null)
        {
            if (IsDisposed)
                return "{0} has already been disposed".CheckedFormat(CurrentClassLeafName);

            using (var scopedLock = new ScopedLock(mutex))
            {
                InnerNoteClientOpRequested();

                MDRF2DateTimeStampPair dtPair = (dtPairIn != null) ? new MDRF2DateTimeStampPair(dtPairIn, setUTCTimeSince1601IfNeeded: false) : MDRF2DateTimeStampPair.NowQPCOnly;

                string ec = string.Empty;

                if (!InnerIsStreamWritable)
                    ec = "{0} failed: File is not open".CheckedFormat(CurrentMethodName);

                try
                {
                    if (ec.IsNullOrEmpty())
                        ec = ServicePendingWrites(ref dtPair);

                    if (ec.IsNullOrEmpty())
                        ec = InnerFlush(flushFlags, ref dtPair, true);
                }
                catch (System.Exception ex)
                {
                    if (InnerIsStreamWritable)
                    {
                        ec = RecordError("{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage)));

                        InnerCloseFile(ref dtPair);
                    }
                }

                return ec;
            }
        }

        private string InnerFlush(MDRF.Writer.FlushFlags flushFlags, ref MDRF2DateTimeStampPair dtPair, bool rethrow = false)
        {
            string ec = string.Empty;

            try
            {
                ec = FinishAndWriteCurrentBlock(blockBufferWriter, ref dtPair, ifNeeded: true, flush: (flushFlags & MDRF.Writer.FlushFlags.File) != 0);
            }
            catch (System.Exception ex)
            {
                ec = RecordError("{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage)));

                if (rethrow)
                    ex.Throw();

                InnerCloseFile(ref dtPair);
            }

            return ec;
        }

        /// <inheritdoc/>
        public int GetCurrentFileSize()
        {
            if (IsDisposed)
                return 0;

            using (var scopedLock = new ScopedLock(mutex))
            {
                return (InnerIsStreamWritable ? CurrentFileInfo : LastFileInfo).FileSize;
            }
        }

        /// <inheritdoc/>
        public bool IsFileOpen
        {
            get
            {
                if (IsDisposed)
                    return false;

                using (var scopedLock = new ScopedLock(mutex))
                {
                    return currentFileInfo.IsActive && InnerIsStreamWritable;
                }
            }
        }

        /// <inheritdoc/>
        public MDRF.Writer.FileInfo CurrentFileInfo
        {
            get
            {
                if (IsDisposed)
                    return default(MDRF.Writer.FileInfo);

                using (var scopedLock = new ScopedLock(mutex))
                {
                    return currentFileInfo;
                }
            }
        }

        /// <inheritdoc/>
        public MDRF.Writer.FileInfo LastFileInfo
        {
            get
            {
                if (IsDisposed)
                    return default(MDRF.Writer.FileInfo);

                using (var scopedLock = new ScopedLock(mutex))
                {
                    return lastFileInfo;
                }
            }
        }

        const int maxClosedFileListLength = 10;
        volatile int volatileClosedFileListCount = 0;
        List<MDRF.Writer.FileInfo> closedFileList = new List<MDRF.Writer.FileInfo>();

        /// <inheritdoc/>
        public MDRF.Writer.FileInfo? NextClosedFileInfo
        {
            get
            {
                if (volatileClosedFileListCount <= 0)
                    return null;

                using (var scopedLock = new ScopedLock(mutex))
                {
                    if (closedFileList.Count <= 0)
                        return null;

                    var fileInfo = closedFileList[0];
                    closedFileList.RemoveAt(0);
                    volatileClosedFileListCount = closedFileList.Count;

                    return fileInfo;
                }
            }
        }

        /// <inheritdoc/>
        public int CloseCurrentFile(string reason, DateTimeStampPair dtPairIn = null)
        {
            if (IsDisposed)
                return 0;

            using (var scopedLock = new ScopedLock(mutex))
            {
                InnerNoteClientOpRequested();

                MDRF2DateTimeStampPair dtPair = (dtPairIn != null) ? new MDRF2DateTimeStampPair(dtPairIn, setUTCTimeSince1601IfNeeded: false) : MDRF2DateTimeStampPair.NowQPCOnly;

                return InnerFinishAndCloseCurrentFile(reason, ref dtPair);
            }
        }

        private int InnerFinishAndCloseCurrentFile(string reason, ref MDRF2DateTimeStampPair dtPair)
        {
            if (InnerIsStreamWritable)
                RecordMessage("{0} reason:{1}".CheckedFormat(CurrentMethodName, reason.MapNullOrEmptyTo("[NoReasonGiven]")), ref dtPair);

            ServicePendingWrites(ref dtPair);

            if (InnerIsStreamWritable)
                WriteFileEndBlockAndCloseFile(ref dtPair);

            allowImmediateReopen = true;

            while (closedFileList.Count >= maxClosedFileListLength)
            {
                var droppingFileInfo = closedFileList[0];

                RecordMessage("Dropped old closedFileList item ['{0}' {1} bytes]".CheckedFormat(System.IO.Path.GetFileName(droppingFileInfo.FilePath), droppingFileInfo.FileSize), ref dtPair);
                closedFileList.RemoveAt(0);
            }

            closedFileList.Add(LastFileInfo);

            volatileClosedFileListCount = closedFileList.Count;

            return LastFileInfo.FileSize;
        }

        /// <inheritdoc/>
        public ulong ClientOpRequestCount { get; private set; }

        /// <summary>
        /// Note: use of this method requires that the mutex (if any) has already been acquired.
        /// </summary>
        private void InnerNoteClientOpRequested() { ClientOpRequestCount += 1; }

        #endregion

        #region RecordError, RecordMessage

        string RecordError(string errorMesg)
        {
            if (!errorMesg.IsNullOrEmpty())
            {
                Log.Emitter(WriterConfig.RecordErrorMesgType).Emit("{0}: {1}", CurrentMethodName, errorMesg);

                MessageItem mesgItem = new MessageItem() { mesg = errorMesg, blockTypeID = FixedBlockTypeID.ErrorV1, dtPair = MDRF2DateTimeStampPair.Now };

                if (errorQueueCount < MessageItem.maxQueueSize)
                {
                    errorQueue.Enqueue(mesgItem);
                    errorQueueCount++;
                }
                else
                {
                    errorQueue.Dequeue();
                    droppedCounts.IncrementError();
                    errorQueue.Enqueue(mesgItem);
                }
            }

            return errorMesg;
        }

        string RecordMessage(string mesg, ref MDRF2DateTimeStampPair dtPair)
        {
            if (!mesg.IsNullOrEmpty())
            {
                Log.Emitter(WriterConfig.RecordMessageMesgType).Emit("{0}: {1}", CurrentMethodName, mesg);

                UpdateUTCTimeSince1601IfNeeded(ref dtPair);

                MessageItem mesgItem = new MessageItem() { mesg = mesg, blockTypeID = FixedBlockTypeID.MessageV1, dtPair = dtPair };

                if (mesgQueueCount < MessageItem.maxQueueSize)
                {
                    mesgQueue.Enqueue(mesgItem);
                    mesgQueueCount++;
                }
                else
                {
                    mesgQueue.Dequeue();
                    droppedCounts.IncrementMessage();
                    mesgQueue.Enqueue(mesgItem);
                }
            }

            return mesg;
        }

        #endregion

        #region More private implementation methods

        void ReassignIDsAndBuildNewTrackersIfNeeded(bool forceRebuild = false)
        {
            if (groupOrOccurrenceInfoListModified || forceRebuild)
            {
                if (InnerIsStreamWritable)
                {
                    CloseCurrentFile("GroupOrOccurrenceInfoListModified: Reassigning IDs and building new trackers");
                }

                {
                    // create outermost occurance, group tracker, and source tracker arrays
                    occurrenceTrackerArray = (occurrenceInfoList).Select(occurrenceInfo => new OccurrenceTracker(occurrenceInfo)).ToArray();
                    groupTrackerArray = (groupInfoList).Select(groupInfo => new GroupTracker(groupInfo, setup)).ToArray();
                    sourceTrackerArray = groupTrackerArray.SelectMany(groupTracker => groupTracker.sourceTrackerArray).ToArray();

                    // assign ids... fileIDs are unique accross the entire file.  itemIDs start at 1 for each item type.  Groups refer to sources by their itemIDs
                    int itemID = 1;
                    foreach (GroupTracker gt in groupTrackerArray)
                    {
                        gt.ClientID = itemID++;
                        gt.groupInfo.GroupID = gt.ClientID;
                    }

                    itemID = 1;
                    foreach (OccurrenceTracker ot in occurrenceTrackerArray)
                    {
                        ot.ClientID = itemID++;
                        ot.occurrenceInfo.OccurrenceID = ot.ClientID;
                    }

                    itemID = 1;
                    foreach (SourceTracker st in sourceTrackerArray)
                    {
                        st.ClientID = itemID++;
                        st.groupPointInfo.SourceID = st.ClientID;
                        st.groupPointInfo.GroupID = st.groupTracker.groupInfo.GroupID;
                    }

                    // make a list of all of the items in the order we want to assign their FileIDs: groups, occurrences, sources - ideally all of the group and occurrence FileIDs will be <= 127
                    // the source FileIDs are used in the Group's ItemList named value
                    TrackerCommon[] allItemCommonArray = (groupTrackerArray as TrackerCommon[]).Concat(occurrenceTrackerArray as TrackerCommon[]).Concat(sourceTrackerArray as TrackerCommon[]).ToArray();

                    // assign all of the FileIDs
                    uint fileID = unchecked((uint)FixedBlockTypeID.FirstDynamicallyAssignedID);
                    foreach (TrackerCommon tc in allItemCommonArray)
                        tc.FileID = fileID++;

                    // then Setup all of the items so that they can generate their NVS contents
                    // NOTE: this updates the ItemList attribute in each group to be the comma seperated set of point FileID values for all of the points that are in each group.
                    foreach (TrackerCommon tc in allItemCommonArray)
                        tc.Setup();

                    allMetaDataItemsArray = (sourceTrackerArray as TrackerCommon[]).Concat(groupTrackerArray as TrackerCommon[]).Concat(occurrenceTrackerArray as TrackerCommon[]).ToArray();
                }

                groupOrOccurrenceInfoListModified = false;
            }
        }

        QpcTimer tenSecondTimer = new QpcTimer() { TriggerIntervalInSec = 10.0, AutoReset = true }.StartIfNeeded();

        string ActivateFile(ref MDRF2DateTimeStampPair dtPair)
        {
            TimeSpan fileDeltaTimeSpan = dtPair.ConvertQPCToFDT(ref fileReferenceQPCDTPair).FromSeconds();

            if (InnerIsStreamWritable && ExternallyProvidedFileStream == null)
            {
                if (groupOrOccurrenceInfoListModified)
                {
                    InnerFinishAndCloseCurrentFile("GroupOccurrence specifications changed: '{0}' is {1} bytes and {2:f3} hours)".CheckedFormat(currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours), ref dtPair);
                }
                else if (currentFileInfo.FileSize >= setup.NominalMaxFileSize && setup.NominalMaxFileSize != 0)
                {
                    InnerFinishAndCloseCurrentFile("NominalMaxFileSize limit of {0} bytes reached: '{1}' is {2} bytes and {3:f3} hours)".CheckedFormat(setup.NominalMaxFileSize, currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours), ref dtPair);
                }
                else if (fileDeltaTimeSpan > setup.MaxFileRecordingPeriod && !setup.MaxFileRecordingPeriod.IsZero())
                {
                    InnerFinishAndCloseCurrentFile("MaxFileRecordingPeriod limit of {0:f3} hours reached: '{1}' is {2} bytes and {3:f3} hours".CheckedFormat(setup.MaxFileRecordingPeriod.TotalHours, currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours), ref dtPair);
                }
                else if (tenSecondTimer.GetIsTriggered(dtPair.QpcTimeStamp))
                {
                    UpdateUTCTimeSince1601IfNeeded(ref dtPair);

                    var localDateTime = dtPair.DateTime.ToLocalTime();
                    int dayOfYear = localDateTime.DayOfYear;
                    bool dayOfYearChanged = (dayOfYear != fileReferenceDayOfYear);

                    if (dayOfYearChanged && ((writerBehavior & MDRF.Writer.WriterBehavior.AdvanceOnDayBoundary) != 0) && fileDeltaTimeSpan.TotalMinutes > 10.0)
                    {
                        InnerFinishAndCloseCurrentFile("AdvanceOnDayBoundary triggered: '{0}' is {1} bytes and {2:f3} hours)".CheckedFormat(currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours), ref dtPair);
                    }
                    else if (fileReferenceIsDST != TimeZoneInfo.Local.IsDaylightSavingTime(localDateTime) || fileReferenceUTCOffset != TimeZoneInfo.Local.GetUtcOffset(localDateTime))
                    {
                        InnerFinishAndCloseCurrentFile("DST and/or timezone have changed since file was opened: '{0}' is {1} bytes and {2:f3} hours".CheckedFormat(currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours), ref dtPair);
                    }
                }
            }

            if (InnerIsStreamWritable)
                return "";

            ReassignIDsAndBuildNewTrackersIfNeeded();

            // capture the timestamp and the corresponding dateTime and related information
            {
                UpdateUTCTimeSince1601IfNeeded(ref dtPair);

                DateTime utcDateTime = dtPair.DateTime;
                DateTime localDateTime = utcDateTime.ToLocalTime();

                TimeZoneInfo localTZ = TimeZoneInfo.Local;

                bool isDST = localTZ.IsDaylightSavingTime(localDateTime);
                TimeSpan utcOffset = localTZ.GetUtcOffset(localDateTime);

                // verify that we are allowed to attempt to activate a new file (time since last open sufficient long or allowImmediateReopen is true)            

                if (allowImmediateReopen)
                {
                    allowImmediateReopen = false;
                }
                else if (fileReferenceDateTime.IsZero())
                {
                    // this is the first ActivateFile call
                }
                else if (fileDeltaTimeSpan < setup.MinInterFileCreateHoldoffPeriod)
                {
                    return "ActivateFile failed: minimum inter-create file holdoff period has not been met.";
                }

                // we are now going to use this dtPair value as the fileReferenceDTPair

                fileReferenceDateTime = utcDateTime;
                fileReferenceQPCDTPair = new MDRF2DateTimeStampPair() { FileDeltaTime = dtPair.FileDeltaTime, UTCTimeSince1601 = fileReferenceDateTime.GetUTCTimeSince1601() };
                fileReferenceDayOfYear = fileReferenceDateTime.ToLocalTime().DayOfYear;
                fileReferenceIsDST = isDST;
                fileReferenceUTCOffset = utcOffset;
            }

            // zero out other file access information prior to creating new file.
            currentFileDeltaTimeStamp = 0.0;

            currentFileInfo.IsActive = false;
            currentFileInfo.FileSize = 0;

            lastWriteAllTimeStamp = QpcTimeStamp.Zero;

            // generate the file name
            string dateTimePart = Dates.CvtToString(fileReferenceDateTime.ToLocalTime(), Dates.DateTimeFormat.ShortWithMSec);

            if (ExternallyProvidedFileStream == null)
            {
                string mdrf2FileExtension = MDRF2.Common.Constants.mdrf2LZ4FileExtension + compressorSelect.GetFileExtension();

                string fileName = $"{0}_{1}{2}".CheckedFormat(setup.FileNamePrefix, dateTimePart, mdrf2FileExtension);

                currentFileInfo.FilePath = System.IO.Path.Combine(setup.DirPath, fileName);
            }
            else
            {
                currentFileInfo.FilePath = "";
                currentFileInfo.StreamProvidedExternally = true;
            }

            // attempt to create/open the file
            string ec = string.Empty;

            try
            {
                if (ExternallyProvidedFileStream == null)
                {
                    int useStreamBufferSize = setup.NominalMaxDataBlockSize.Clip(1024, 65536);

                    fileStream = new FileStream(currentFileInfo.FilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, useStreamBufferSize, FileOptions.None);

                    if (compressorSelect != CompressorSelect.None)
                        compressorStream = fileStream.CreateCompressor(compressorSelect, WriterConfig.CompressionLevel, leaveStreamOpen: true);

                    outputStream = compressorStream ?? fileStream;
                }
                else
                {
                    outputStream = ExternallyProvidedFileStream;

                    if (!outputStream.CanWrite)
                        ec = $"{CurrentMethodName} failed: externally provided file stream does not support writing";
                }
            }
            catch (System.Exception ex)
            {
                string mesg = "{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage));

                ec = mesg;
            }

            if (ec.IsNullOrEmpty())
            {
                try
                {
                    AllocatedBuffersIfNeeded();

                    var mpWriter = new MessagePack.MessagePackWriter(metaDataBlockBufferWriter);

                    // generate and write the first file header block with single InlineMAP
                    if (ec.IsNullOrEmpty())
                    {
                        StartNewBlockIfNeeded(metaDataBlockBufferWriter, ref dtPair);

                        AddInlineMap1_StartAndDateTime(ref mpWriter, ref dtPair);

                        mpWriter.Flush();

                        // finish and write block
                        ec = FinishAndWriteCurrentBlock(metaDataBlockBufferWriter, ref dtPair);
                    }

                    // generate and write the first file header block with single InlineMAP
                    if (ec.IsNullOrEmpty())
                    {
                        StartNewBlockIfNeeded(metaDataBlockBufferWriter, ref dtPair);

                        AddInlineMap2_LibSessionClientAndSpecItems(ref mpWriter);

                        mpWriter.Flush();

                        // finish and write block
                        ec = FinishAndWriteCurrentBlock(metaDataBlockBufferWriter, ref dtPair);
                    }

                    // service pending writes to record any messages or errors into the file (if it is still open)
                    if (ec.IsNullOrEmpty())
                        ec = ServicePendingWrites(ref dtPair);

                    // finally flush the file
                    if (ec.IsNullOrEmpty())
                        ec = InnerFlush(MDRF.Writer.FlushFlags.File, ref dtPair, false);

                    if (ec.IsNullOrEmpty())
                        currentFileInfo.IsActive = true;
                }
                catch (System.Exception ex)
                {
                    ec = "{0} failed: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                }
            }

            if (ec.IsNeitherNullNorEmpty())
                return RecordError(ec);

            // clear the dropped counters after all messages have been emitted successfully
            droppedCounts = new DroppedCounts();

            return ec;
        }

        // [MAP "MDRF2_Start":nil "DateTime":DateTimeNVS]
        private void AddInlineMap1_StartAndDateTime(ref MessagePack.MessagePackWriter mpWriter, ref MDRF2DateTimeStampPair dtPair)
        {
            mpWriter.WriteMapHeader(2);

            // "MDRF2.Start":nil
            {
                mpWriter.Write(Common.Constants.FileStartKeyword);
                mpWriter.WriteNil();
            }

            // "DateTime":dateTimeNVS
            {
                mpWriter.Write(Common.Constants.DateTimeNVSKeyName);

                UpdateUTCTimeSince1601IfNeeded(ref dtPair);

                var fileDeltaTime = dtPair.ConvertQPCToFDT(ref fileReferenceQPCDTPair);

                dtInfo.UpdateFrom(dtPair.QpcTimeStamp, fileDeltaTime, dtPair.DateTime, dtPair.UTCTimeSince1601);

                mpWriter.SerializeAsVC(dtInfo.UpdateNVSFromThis(dateTimeBlockNVSet));
            }

            NoteRecordAddedToBuffer(FileIndexRowFlagBits.ContainsHeaderOrMetaData, 0);
        }


        DateTimeInfo dtInfo = new DateTimeInfo();
        NamedValueSet dateTimeBlockNVSet = new NamedValueSet();
        NamedValueSet currentProcessNVS = new NamedValueSet();

        INamedValueSet libNVS, setupNVS, environmentNVS;

        // [MAP "UUID":uuid "Lib":libNVS "Setup":setupNVS "Client":clientNVS "HostName":hostName "CurrentProcess":currentProcessNVS "Environment":environmentNVS "SpecItems":specItemsLOfNVS]
        void AddInlineMap2_LibSessionClientAndSpecItems(ref MessagePack.MessagePackWriter mpWriter)
        {
            mpWriter.WriteMapHeader(8);

            // UUID
            {
                mpWriter.Write(Common.Constants.FileInstanceUUIDKeyName);
                mpWriter.Write(Guid.NewGuid().ToString("D"));
            }

            // Lib
            {
                mpWriter.Write(Common.Constants.LibNVSKeyName);

                if (libNVS == null)
                    libNVS = Common.Constants.Lib2Info.UpdateNVSFromThis(new NamedValueSet());

                mpWriter.SerializeAsVC(libNVS);
            }

            // Setup
            {
                mpWriter.Write(Common.Constants.SetupNVSKeyName);

                if (setupNVS == null)
                {
                    setupNVS = setup.UpdateNVSFromThis(new NamedValueSet())
                        .MakeReadOnly();
                }

                mpWriter.SerializeAsVC(setupNVS);
            }

            // Client
            {
                mpWriter.Write(Common.Constants.ClientNVSKeyName);

                mpWriter.SerializeAsVC(setup.ClientNVS.MapNullToEmpty());
            }

            // HostName
            {
                mpWriter.Write(Common.Constants.HostNameKeyName);

                string hostName;

                try
                {
                    hostName = System.Net.Dns.GetHostName();
                }
                catch (System.Exception ex)
                {
                    hostName = "GetHostName failed: {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessage));
                }

                mpWriter.Write(hostName);
            }

            // CurrentProcess
            {
                mpWriter.Write(Common.Constants.CurrentProcessKeyName);

                var nvSet = currentProcessNVS;

                System.Diagnostics.Process currentProcess = Utils.ExtensionMethods.TryGet<System.Diagnostics.Process>(() => System.Diagnostics.Process.GetCurrentProcess());

                nvSet.SetValue("ProcessName", Utils.ExtensionMethods.TryGet<string>(() => currentProcess.ProcessName));
                nvSet.SetValue("Id", Utils.ExtensionMethods.TryGet<int>(() => currentProcess.Id));
                nvSet.SetValue("Affinity", Utils.ExtensionMethods.TryGet<long>(() => currentProcess.ProcessorAffinity.ToInt64()));

                mpWriter.SerializeAsVC(nvSet);
            }

            // Environment
            {
                mpWriter.Write(Common.Constants.EnvironmentKeyName);

                if (environmentNVS == null)
                {
                    environmentNVS = new NamedValueSet()
                    {
                        { "Is64BitProcess", Environment.Is64BitProcess },
                        { "MachineName", Environment.MachineName },
                        { "OSVersion", Environment.OSVersion.ToString() },
                        { "Is64BitOperatingSystem", Environment.Is64BitOperatingSystem },
                        { "ProcessorCount", Environment.ProcessorCount },
                        { "SystemPageSize", Environment.SystemPageSize },
                        { "UserName", Environment.UserName },
                        { "UserInteractive", Environment.UserInteractive },
                    }.MakeReadOnly();
                }

                mpWriter.SerializeAsVC(environmentNVS);
            }

            // SpecItems
            {
                mpWriter.Write(Common.Constants.SpecItemsNVSListKey);

                INamedValueSet[] specNVSArray = allMetaDataItemsArray.Select(tracker => tracker.nvSet).ToArray();

                mpWriter.WriteArrayHeader(specNVSArray.Length);
                foreach (var nvs in specNVSArray)
                    mpWriter.SerializeAsVC(nvs);
            }

            NoteRecordAddedToBuffer(FileIndexRowFlagBits.ContainsHeaderOrMetaData, 0);
        }

        // [MAP "MDRF2.End":nil]
        private void AddInlineMapLast_FileEnd(ref MessagePack.MessagePackWriter mpWriter)
        {
            mpWriter.WriteMapHeader(1);

            // "MDRF2.End":nil
            {
                mpWriter.Write(Common.Constants.FileEndKeyword);
                mpWriter.WriteNil();
            }

            NoteRecordAddedToBuffer(FileIndexRowFlagBits.ContainsHeaderOrMetaData | FileIndexRowFlagBits.ContainsEnd, 0);
        }

        /// <summary>Returns true if outputStream?.CanWrite is true</summary>
        bool InnerIsStreamWritable { get { return (outputStream?.CanWrite ?? false); } }

        void InnerCloseFile(ref MDRF2DateTimeStampPair dtPair)
        {
            if (outputStream != null)
            {
                try
                {
                    if (blockBufferWriter != null && HasBlockBeenStarted)
                    {
                        FinishAndWriteCurrentBlock(blockBufferWriter, ref dtPair);
                    }

                    currentFileInfo.IsActive = false;
                    lastFileInfo = currentFileInfo;

                    outputStream.Flush();
                    outputStream = null;

                    Fcns.DisposeOfObject(ref compressorStream);

                    if (fileStream != null)
                    {
                        fileStream.Flush();
                        fileStream.Close();
                        Fcns.DisposeOfObject(ref fileStream);
                    }
                }
                catch (System.Exception ex)
                {
                    RecordError("{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage)));

                    fileStream = null;
                    compressorStream = null;
                    outputStream = null;
                }
            }
        }

        string WriteFileEndBlockAndCloseFile(ref MDRF2DateTimeStampPair dtPair)
        {
            string ec = ServicePendingWrites(ref dtPair);

            if (ec.IsNullOrEmpty())
                ec = FinishAndWriteCurrentBlock(blockBufferWriter, ref dtPair, ifNeeded: true);

            if (ec.IsNullOrEmpty())
            {
                try
                {
                    StartNewBlockIfNeeded(blockBufferWriter, ref dtPair);

                    MessagePack.MessagePackWriter mpWriter = new MessagePack.MessagePackWriter(blockBufferWriter);

                    AddInlineMapLast_FileEnd(ref mpWriter);

                    mpWriter.Flush();

                    ec = FinishAndWriteCurrentBlock(blockBufferWriter, ref dtPair);
                }
                catch (System.Exception ex)
                {
                    ec = "{0} failed: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                }
            }

            InnerCloseFile(ref dtPair);

            if (ec.IsNeitherNullNorEmpty())
                RecordError(ec);

            return ec;
        }

        string ServicePendingWrites(ref MDRF2DateTimeStampPair dtPair)
        {
            string ec = string.Empty;

            while (ec.IsNullOrEmpty())
            {
                if (errorQueueCount <= MessageItem.maxQueueSize && droppedCounts.total > 0)
                {
                    MessageItem mesgItem = new MessageItem()
                    {
                        blockTypeID = FixedBlockTypeID.ErrorV1,
                        dtPair = MDRF2DateTimeStampPair.Now,
                        mesg = "Note dropped items: {0}".CheckedFormat(droppedCounts.ToString()),
                    };

                    errorQueue.Enqueue(mesgItem);
                    errorQueueCount++;

                    Log.Debug.Emit(mesgItem.mesg);

                    droppedCounts = new DroppedCounts();
                }

                if (!InnerIsStreamWritable)
                    return "{0} failed: file is not open".CheckedFormat(CurrentMethodName);

                {
                    bool haveErrors = (errorQueueCount > 0);
                    bool haveMesgs = (mesgQueueCount > 0);

                    if (!haveErrors && !haveMesgs)
                        break;

                    bool takeFromErrorQueue = haveErrors;

                    if (haveErrors && haveMesgs)
                    {
                        MessageItem errorItem = errorQueue.Peek();
                        MessageItem mesgItem = mesgQueue.Peek();

                        takeFromErrorQueue = (errorItem.dtPair.QpcTimeStamp <= mesgItem.dtPair.QpcTimeStamp);
                    }

                    MessageItem writeItem = null;

                    if (haveErrors && takeFromErrorQueue)
                    {
                        writeItem = errorQueue.Dequeue();
                        errorQueueCount = errorQueue.Count;
                    }
                    else if (haveMesgs)
                    {
                        writeItem = mesgQueue.Dequeue();
                        mesgQueueCount = mesgQueue.Count;
                    }

                    if (writeItem != null)
                    {
                        try
                        {
                            StartNewBlockIfNeeded(blockBufferWriter, ref dtPair);

                            MessagePack.MessagePackWriter mpWriter = new MessagePack.MessagePackWriter(blockBufferWriter);

                            UpdateFileDeltaTimeStampIfNeeded(ref mpWriter, ref dtPair);

                            // use object representation: [L3 "Mesg"|"Error" [L3 issueFDT issueUTC1601 mesg] nil]
                            bool isError = (writeItem.blockTypeID == FixedBlockTypeID.ErrorV1);

                            mpWriter.WriteArrayHeader(3);   // fixed list length used to represent objects
                            mpWriter.Write(isError ? MDRF2.Common.Constants.ObjKnownType_Error : MDRF2.Common.Constants.ObjKnownType_Mesg);
                            MesgAndErrorBodyFormatter.Instance.Serialize(ref mpWriter, Tuple.Create(writeItem.mesg, writeItem.dtPair), mpOptions);
                            mpWriter.WriteNil();

                            mpWriter.Flush();

                            ec = NoteRecordAddedToBufferAndWriteIfNeeded(blockBufferWriter, ref dtPair, isError ? FileIndexRowFlagBits.ContainsError : FileIndexRowFlagBits.ContainsMessage, 0);

                            if (ec.IsNullOrEmpty() && (isError || writerBehavior.IsSet(MDRF.Writer.WriterBehavior.FlushAfterEveryMessage)))
                                ec = InnerFlush(MDRF.Writer.FlushFlags.All, ref dtPair, false);
                        }
                        catch (System.Exception ex)
                        {
                            ec = "{0} failed: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                        }
                    }
                }
            }

            return ec;
        }

        #endregion

        #region Internals: MessageItem, TrackerCommon, SourceTracker, GroupTracker, OccurrenceTracker, DroppedCounts

        class MessageItem
        {
            public FixedBlockTypeID blockTypeID;
            public MDRF2DateTimeStampPair dtPair; // when the message was enqueued.
            public string mesg;

            public const int maxQueueSize = 100;
        }

        class TrackerCommon
        {
            public override string ToString()
            {
                return $"Tracker {mdci}";
            }

            public string Name { get { return mdci.Name; } }
            public string Comment { get { return mdci.Comment; } }
            public MDRF.Writer.MetaDataCommonInfo mdci;
            public MDRF.Common.MDItemType mdItemType;
            public uint FileID { get { return (uint) mdci.FileID; } set { mdci.FileID = (int) value; } }
            public int ClientID { get { return mdci.ClientID; } set { mdci.ClientID = value; } }
            public NamedValueSet nvSet = new NamedValueSet();

            public TrackerCommon(MDItemType mdItemType, MDRF.Writer.MetaDataCommonInfo mdci)
            {
                this.mdci = mdci;
                this.mdItemType = mdItemType;
            }

            public void AddClientNVSItems()
            {
                if (!mdci.ClientNVS.IsNullOrEmpty())
                    nvSet.AddRange(mdci.ClientNVS.Select<INamedValue, INamedValue>(inv => new NamedValue("c.{0}".CheckedFormat(inv.Name), inv.VC)));
            }

            public virtual void Setup()
            {
                nvSet = mdci.UpdateNVSFromThis(new NamedValueSet()).MakeReadOnly();
            }
        }

        class SourceTracker : TrackerCommon
        {
            public GroupTracker groupTracker;
            public SetupInfo setupInfo;
            public MDRF.Writer.GroupInfo groupInfo;
            public MDRF.Writer.GroupPointInfo groupPointInfo;

            public bool touched;
            public ValueContainer lastServicedValue;
            public uint lastServicedIVASeqNum;

            public SourceTracker(MDRF.Writer.GroupPointInfo gpi, GroupTracker gt)
                : base(MDRF.Common.MDItemType.Source, gpi)
            {
                groupTracker = gt;
                setupInfo = gt.setupInfo;
                groupInfo = gt.groupInfo;
                groupPointInfo = gpi;
                mdci.CST = gpi.CST;

                AddClientNVSItems();

                if (gpi.ValueSourceIVA == null && gt.ivi != null)
                {
                    gpi.ValueSourceIVA = gt.ivi.GetValueAccessor(gpi.Name);
                    if (gpi.ValueSourceIVA.HasValueBeenSet)
                        gpi.VC = gpi.ValueSourceIVA.VC;
                    lastServicedIVASeqNum = gpi.ValueSourceIVA.ValueSeqNum;
                }

                lastServicedValue = gpi.VC;
                touched = (gpi.VCHasBeenSet || !lastServicedValue.IsEmpty);
            }

            public void Service()
            {
                // if we do not have an associated group tracker or the group is neither touched, to be written, nor flagged for write all) then there is no need to service this source further.
                if (groupTracker == null || groupPointInfo == null)
                    return;

                IValueAccessor iva = groupPointInfo.ValueSourceIVA;

                // if we are using iva based value sources and the groupTracker has not already been marked as touched or writeAll then we are done - no need to attempt to update the local value
                if (iva != null && !(groupTracker.touched || groupTracker.writeAll))
                    return;

                if (iva != null)
                {
                    if (lastServicedIVASeqNum != iva.ValueSeqNum)
                    {
                        groupPointInfo.VC = iva.VC;
                        lastServicedValue = groupPointInfo.VC;
                        if (!touched)
                            touched = true;
                    }
                }
                else if (groupInfo.UseSourceIVAsForTouched)
                {
                    if (groupPointInfo.VCHasBeenSet)
                    {
                        lastServicedValue = groupPointInfo.VC;
                        touched = true;
                    }
                }
                else
                {
                    bool different = groupPointInfo.VCIsUsable && (touched || groupTracker.writeAll || !lastServicedValue.Equals(groupPointInfo.VC));

                    if (different)
                        lastServicedValue = groupPointInfo.VC;

                    if (different && !touched)
                        touched = true;
                }

                if (touched)
                {
                    groupTracker.numTouchedSources++;
                    if (!groupTracker.touched)
                        groupTracker.touched = true;
                }
            }

            public void NoteWritten()
            {
                touched = false;
                groupPointInfo.VCHasBeenSet = false;
            }
        }

        class GroupTracker : TrackerCommon
        {
            public MDRF.Writer.GroupInfo groupInfo;
            public SetupInfo setupInfo;
            public SourceTracker[] sourceTrackerArray;
            public int sourceTrackerArrayLength;

            public bool writeAll;
            public bool touched;
            public int numTouchedSources;

            public IValuesInterconnection ivi;
            public IValueAccessor[] ivaArray;

            public GroupTracker(MDRF.Writer.GroupInfo gi, SetupInfo si)
                : base(MDItemType.Group, gi)
            {
                groupInfo = gi;
                setupInfo = si;
                AddClientNVSItems();

                ivi = gi.IVI;
                if (gi.UseSourceIVAsForTouched && ivi == null)
                    ivi = Values.Instance;

                sourceTrackerArray = (groupInfo.GroupPointInfoArray ?? EmptyArrayFactory<MDRF.Writer.GroupPointInfo>.Instance).Select(pointInfo => new SourceTracker(pointInfo, this)).ToArray();
                sourceTrackerArrayLength = sourceTrackerArray.Length;

                if (gi.UseSourceIVAsForTouched)
                {
                    ivaArray = gi.GroupPointInfoArray.Select(ptInfo => ptInfo.ValueSourceIVA).Where(iva => iva != null).ToArray();
                }
            }

            public void Service(bool writeAll)
            {
                this.writeAll |= writeAll;

                if (!touched)
                {
                    if (writeAll)
                        touched = true;
                    else if (groupInfo.Touched)
                        touched = true;
                    else if (groupInfo.UseSourceIVAsForTouched && ivaArray.IsUpdateNeeded())
                    {
                        touched = true;
                        ivi.Update(ivaArray);
                    }
                }

                foreach (var st in sourceTrackerArray)
                    st.Service();
            }

            public override void Setup()
            {
                base.Setup();

                nvSet = nvSet.ConvertToWritable()
                    .SetValue("ItemList", string.Join(",", sourceTrackerArray.Select(st => st.FileID)))   // preserve prior format of this field.
                    .SetValue("FileIndexUserRowFlagBits", groupInfo.FileIndexUserRowFlagBits)
                    .ConvertToReadOnly();
            }

            /// <summary>
            /// Clears touched flag, writeAll flag, groupInfo.Touched flag, and calls each underlying SourceTracker's NoteWritten method.
            /// </summary>
            public void NoteWritten()
            {
                touched = false;
                writeAll = false;

                if (groupInfo != null)
                    groupInfo.Touched = false;

                foreach (var st in this.sourceTrackerArray)
                    st.NoteWritten();

                numTouchedSources = 0;
            }
        }

        class OccurrenceTracker : TrackerCommon
        {
            public MDRF.Writer.OccurrenceInfo occurrenceInfo;
            public MDRF.Common.FileIndexRowFlagBits fileIndexRowFlagBits;

            public OccurrenceTracker(MDRF.Writer.OccurrenceInfo oi)
                : base(MDItemType.Occurrence, oi)
            {
                occurrenceInfo = oi;
                AddClientNVSItems();

                fileIndexRowFlagBits = FileIndexRowFlagBits.ContainsOccurrence | (oi.IsHighPriority ? FileIndexRowFlagBits.ContainsHighPriorityOccurrence : FileIndexRowFlagBits.None);
            }

            public override void Setup()
            {
                base.Setup();

                nvSet = nvSet.ConvertToWritable()
                    .SetValue("HighPriority", occurrenceInfo.IsHighPriority)
                    .SetValue("FileIndexUserRowFlagBits", occurrenceInfo.FileIndexUserRowFlagBits)
                    .ConvertToReadOnly();
            }
        }

        struct DroppedCounts
        {
            public int error, message, occurrence, dataGroup, total;

            public void IncrementError() { error++; total++; }
            public void IncrementMessage() { message++; total++; }
            public void IncrementOccurrence() { occurrence++; total++; }
            public void IncrementDataGroup() { dataGroup++; total++; }

            public override string ToString()
            {
                return "errors:{0}, mesg:{1} occur:{2} data:{3} total:{4}".CheckedFormat(error, message, occurrence, dataGroup, total);
            }
        }

        #endregion

        #region Block builder

        private string FinishAndWriteCurrentBlock(ByteArrayBufferWriter bufferWriter, ref MDRF2DateTimeStampPair dtPair, bool ifNeeded = false, bool flush = false)
        {
            if (!HasBlockBeenStarted)
            {
                if (ifNeeded)
                    return string.Empty;
                else
                    return "There is no current block to write";
            }

            string ec = string.Empty;

            try
            {
                if (outputStream != null)
                {
                    var currentRecordByteCount = (uint)bufferWriter.CurrentCount;

                    {
                        UpdateUTCTimeSince1601IfNeeded(ref dtPair);

                        inlineIndexBlockBufferWriter.ResetCount();

                        var headerMPWriter = new MessagePack.MessagePackWriter(inlineIndexBlockBufferWriter);

                        inlineIndexRecord.BlockByteCount = currentRecordByteCount;

                        inlineIndexRecord.BlockFirstFileDeltaTime = blockBufferFirstDTPair.ConvertQPCToFDT(ref fileReferenceQPCDTPair);
                        inlineIndexRecord.BlockFirstUDTTimeSince1601 = blockBufferFirstDTPair.UTCTimeSince1601;  // this one must have already captured its UTCTimeSince1601

                        var blockBufferLastDTPair = dtPair;
                        inlineIndexRecord.BlockLastFileDeltaTime = blockBufferLastDTPair.ConvertQPCToFDT(ref fileReferenceQPCDTPair);
                        inlineIndexRecord.BlockLastUDTTimeSince1601 = blockBufferLastDTPair.UTCTimeSince1601;

                        InlineIndexFormatter.Instance.Serialize(ref headerMPWriter, inlineIndexRecord, mpOptions);

                        headerMPWriter.Flush();
                    }

                    inlineIndexBlockBufferWriter.WriteTo(outputStream);
                    bufferWriter.WriteTo(outputStream);

                    if (flush)
                        outputStream.Flush();
                }
                else
                {
                    ec = "Internal: there is no file open to write this block to";
                }

                inlineIndexBlockBufferWriter.ResetCount();
                bufferWriter.ResetCount();

                inlineIndexRecord.Clear();
                blockBufferFirstDTPair = default;
                HasBlockBeenStarted = false;
            }
            catch (System.Exception ex)
            {
                ec = "{0} failed: {1}".CheckedFormat(CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }

            if (ec.IsNeitherNullNorEmpty())
                return RecordError(ec);

            numBlocksWritten += 1;

            return ec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string NoteRecordAddedToBufferAndWriteIfNeeded(ByteArrayBufferWriter bufferWriter, ref MDRF2DateTimeStampPair dtPair, FileIndexRowFlagBits rowFlagBits, UInt64 userRowFlagBits)
        {
            if (!HasBlockBeenStarted)
                return "Cannot add a record to a block that has not been started";

            NoteRecordAddedToBuffer(rowFlagBits, userRowFlagBits);

            return FinishAndWriteCurrentBlockIfNeeded(bufferWriter, ref dtPair);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string FinishAndWriteCurrentBlockIfNeeded(ByteArrayBufferWriter bufferWriter, ref MDRF2DateTimeStampPair dtPair)
        {
            if (!HasBlockBeenStarted)
                return "";

            var dtPairAgeSinceStartOfBlock = blockBufferFirstDTPair.QpcTimeStamp.Age(dtPair.QpcTimeStamp);

            if ((bufferWriter.CurrentCount >= setup.NominalMaxDataBlockSize) || (dtPairAgeSinceStartOfBlock >= setup.MinNominalFileIndexWriteInterval))
                return FinishAndWriteCurrentBlock(bufferWriter, ref dtPair);
            else
                return string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NoteRecordAddedToBuffer(FileIndexRowFlagBits rowFlagBits, UInt64 userRowFlagBits)
        {
            inlineIndexRecord.BlockRecordCount += 1;

            if ((rowFlagBits & (FileIndexRowFlagBits.ContainsOccurrence | FileIndexRowFlagBits.ContainsHighPriorityOccurrence)) != 0)
                inlineIndexRecord.BlockOccurrenceCount += 1;

            if ((rowFlagBits & (FileIndexRowFlagBits.ContainsObject | FileIndexRowFlagBits.ContainsSignificantObject)) != 0)
                inlineIndexRecord.BlockObjectCount += 1;

            inlineIndexRecord.BlockFileIndexRowFlagBits |= rowFlagBits;
            inlineIndexRecord.BlockUserRowFlagBits |= userRowFlagBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AllocatedBuffersIfNeeded()
        {
            if (blockBufferWriter == null)
            {
                if (inlineIndexBlockBufferWriter == null)
                    inlineIndexBlockBufferWriter = new ByteArrayBufferWriter(128) { MinGrowthFactor = 2.0, MinExtraSize = 64 };

                if (metaDataBlockBufferWriter == null)
                    metaDataBlockBufferWriter = new ByteArrayBufferWriter(SetupInfo.InitialMetaDataBlockSize) { MinGrowthFactor = 2.0 };

                blockBufferWriter = new ByteArrayBufferWriter(setup.NominalMaxDataBlockSize + SetupInfo.NominalExtraDataBlockSize);
            }
        }

        /// <summary>
        /// If the given <paramref name="dtPair"/> contains a QPC value (aka its FileDeltaTime is negative) then convert it to an FDT using the fileReferenceQPCDTPair value.
        /// If it's UTCTimeSince1601 is zero then set it to DateTime.Now.GetUTCTimeSinge1601().
        /// Generally this is only done immediately before writing the result out, or recording it for later use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateUTCTimeSince1601IfNeeded(ref MDRF2DateTimeStampPair dtPair)
        {
            if (dtPair.UTCTimeSince1601 == 0.0)
                dtPair.UTCTimeSince1601 = DateTime.Now.GetUTCTimeSince1601();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StartNewBlockIfNeeded(ByteArrayBufferWriter bufferWriter, ref MDRF2DateTimeStampPair dtPair)
        {
            if (HasBlockBeenStarted)
                return;

            UpdateUTCTimeSince1601IfNeeded(ref dtPair);

            blockBufferFirstDTPair = dtPair;

            inlineIndexRecord.Clear();
            // the index itself is not counted in the number of records or in the total size.

            // block creating another FDT update record whenever starting a new block as the starting FDT is carried in the inline index record body that will be prefixed to the other records in this block.
            currentFileDeltaTimeStamp = blockBufferFirstDTPair.ConvertQPCToFDT(ref fileReferenceQPCDTPair);

            HasBlockBeenStarted = true;
        }

        /// <summary>Returns true if the inline index buffer has had its template record generated (aka it is non-empty).</summary>
        private bool HasBlockBeenStarted { get; set; }

        private ByteArrayBufferWriter inlineIndexBlockBufferWriter;
        private ByteArrayBufferWriter metaDataBlockBufferWriter;
        private ByteArrayBufferWriter blockBufferWriter;

        private InlineIndexRecord inlineIndexRecord = new InlineIndexRecord();
        private MDRF2DateTimeStampPair blockBufferFirstDTPair = default;
        private int numBlocksWritten = 0;

        #endregion

        #region Emitting FileDeltaTimeStamp update records

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFileDeltaTimeStampIfNeeded(ref MessagePack.MessagePackWriter mpWriter, ref MDRF2DateTimeStampPair dtPair)
        {
            var fileDeltaTime = dtPair.ConvertQPCToFDT(ref fileReferenceQPCDTPair);

            // a FDT update record cannot be the first record in the current block
            if (!HasBlockBeenStarted || currentFileDeltaTimeStamp == fileDeltaTime)
                return;

            mpWriter.Write(currentFileDeltaTimeStamp = fileDeltaTime);

            inlineIndexRecord.BlockRecordCount++;
        }

        double currentFileDeltaTimeStamp;

        #endregion

        #region Internals: fields

        /// <remarks>
        /// Annotate this as readonly since the value is only assigned once in the constructor (to either new object() or null).
        /// </remarks>
        readonly object mutex;

        SetupInfo setupOrig;
        SetupInfo setup;

        MDRF.Writer.WriterBehavior writerBehavior;

        /// <summary>
        /// This is the underlying stream for the file we are writing to
        /// </summary>
        System.IO.FileStream fileStream;

        /// <summary>
        /// If we are using compression then this is the stream that is used to compress into the file stream
        /// </summary>
        System.IO.Stream compressorStream;

        /// <summary>
        /// This is a shorthand for either the compressor stream or the file stream depeding on wether compression is being used or not.
        /// </summary>
        System.IO.Stream outputStream;

        DateTime fileReferenceDateTime = default;
        MDRF2DateTimeStampPair fileReferenceQPCDTPair = default;
        int fileReferenceDayOfYear = 0;
        bool fileReferenceIsDST;
        TimeSpan fileReferenceUTCOffset;
        bool allowImmediateReopen;

        MDRF.Writer.FileInfo currentFileInfo = new MDRF.Writer.FileInfo();
        MDRF.Writer.FileInfo lastFileInfo = new MDRF.Writer.FileInfo();

        QpcTimeStamp lastWriteAllTimeStamp;

        SourceTracker[] sourceTrackerArray;
        GroupTracker[] groupTrackerArray;
        OccurrenceTracker[] occurrenceTrackerArray;
        TrackerCommon[] allMetaDataItemsArray;     // in order of source, group, occurrence

        Queue<MessageItem> errorQueue = new Queue<MessageItem>(MessageItem.maxQueueSize);
        int errorQueueCount;
        Queue<MessageItem> mesgQueue = new Queue<MessageItem>(MessageItem.maxQueueSize);
        int mesgQueueCount;

        DroppedCounts droppedCounts = new DroppedCounts();

        #endregion
    }

    #endregion

    #region IMDRF2RecordingEngine, MDRF2RecordingEngine, MDRF2RecordingEngineConfig

    /// <summary>
    /// Gives the interface that is implemented by the MDRFRecordingEngine part.
    /// </summary>
    public interface IMDRF2RecordingEngine : MDRF.Writer.IMDRFRecordingEngine
    {
        /// <summary>
        /// Gives the client direct access to the MDRF1Writer that this object creates and uses.
        /// <para/>Warning: this property may be null if there is no currently active mdrfWriter.
        /// </summary>
        new IMDRF2Writer MDRFWriter { get; }

        /// <summary>
        /// Action factory method.  When run the resulting action will record the given object <paramref name="obj"/> using MDRF2's object recording mechanism.
        /// If <paramref name="writeAll"/> is true then this action will record all groups before writing this one.
        /// If <paramref name="isHighRate"/> is true then this action will use the configured high rate action logging.
        /// If <paramref name="isSignificant"/> is true then this action will record the object using being significant.
        /// If <paramref name="userRowFlagBits"/> is non-zero then its value will be added (logically ored) into the inline index user row flag bit values and will be attached to the object record (for newly recorded MDRF2 objects).
        /// </summary>
        IClientFacet RecordObject(object obj, bool writeAll = false, bool isHighRate = true, bool isSignificant = false, ulong userRowFlagBits = 0);

        /// <summary>
        /// Action factory method.  When run the resulting action will record the given set of objects from <paramref name="objArray"/> using MDRF2's object recording mechanism.
        /// If <paramref name="writeAll"/> is true then this action will record all groups before writing this one.
        /// If <paramref name="isHighRate"/> is true then this action will use the configured high rate action logging.
        /// If <paramref name="isSignificant"/> is true then this action will record the objects using being significant.
        /// If <paramref name="userRowFlagBits"/> is non-zero then its value will be added (logically ored) into the inline index user row flag bit values and will be attached to the object records (for newly recorded MDRF2 objects).
        /// </summary>
        IClientFacet RecordObjects(object [] objArray, bool writeAll = false, bool isHighRate = true, bool isSignificant = false, ulong userRowFlagBits = 0);

        /// <summary>
        /// Action factory method.  When run the resulting action will request the underlying MDRFWriter instance to perform a Flush operation with the given <paramref name="flushFlags"/>.
        /// </summary>
        IClientFacet Flush(FlushFlags flushFlags = FlushFlags.All, bool isHighRate = true);
    }

    /// <summary>
    /// Defines the client usable behavior of an MDRF2RecordingEngine instance.  
    /// This defines the PartID to use and a number of other characteristics including:
    /// the NominalScanPeriod to use, 
    /// the IVI from which to get values,
    /// an optional GroupDefinitionSet that may be used to define what IVI items shall be included and what group each one shall be placed in,
    /// the SetupInfo to be used with the internal IMDRFWriter instance, 
    /// and the PruningConfig that may be used to define how the pruning engine is used to automatically remove old files as new ones are generated.
    /// </summary>
    public class MDRF2RecordingEngineConfig
    {
        /// <summary>
        /// Basic constructor.
        /// <para/>defaults: NominalScanPeriod = 0.1 seconds, NominalPruningInterval = 10.0 seconds.
        /// </summary>
        public MDRF2RecordingEngineConfig(string partID)
        {
            PartID = partID;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public MDRF2RecordingEngineConfig(MDRF2RecordingEngineConfig other)
        {
            PartID = other.PartID;

            SetupInfo = new SetupInfo(other.SetupInfo);     // will be mapped to default values if the other is null)
            CompressorSelect = other.CompressorSelect;
            CompressionLevel = other.CompressionLevel;

            NominalScanPeriod = other.NominalScanPeriod;

            ScanPeriodScheduleArray = new List<TimeSpan>(other.ScanPeriodScheduleArray ?? emptyTimeSpanArray).ToArray();

            SourceIVI = other.SourceIVI ?? Values.Instance;

            AllocateUserRowFlagBitsForGroups = other.AllocateUserRowFlagBitsForGroups;

            if (other.GroupDefinitionItemArray != null)
                GroupDefinitionItemArray = other.GroupDefinitionItemArray.SafeToArray();

            if (other.OccurrenceDefinitionItemArray != null)
                OccurrenceDefinitionItemArray = other.OccurrenceDefinitionItemArray.Select(item => new MDRF2RecordingEngineItems.OccurrenceDefinitionItem() { OccurrenceName = item.OccurrenceName, EventNames = item.EventNames.SafeToArray(mapNullToEmpty: false), OccurrenceMetaData = item.OccurrenceMetaData }).ToArray();

            if (other.PruningConfig != null)
            {
                PruningConfig = new DirectoryTreePruningManager.Config(other.PruningConfig)
                {
                    DirPath = SetupInfo.DirPath,
                };
            }

            NominalPruningInterval = other.NominalPruningInterval;
            ActionLoggingConfig = new ActionLoggingConfig(other.ActionLoggingConfig);
            HighRateActionLoggingConfig = new ActionLoggingConfig(other.HighRateActionLoggingConfig);
        }

        /// <summary>Defines the PartID that will be used by the MDRFRecordingEngine that is constructed from this configuration object</summary>
        public string PartID { get; private set; }

        /// <summary>Gives the MDRF SetupInfo that is used to configure the underlying MDRFWriter.</summary>
        public SetupInfo SetupInfo { get; set; } = SetupInfo.DefaultForMDRF2;

        public CompressorSelect CompressorSelect { get; set; } = CompressorSelect.LZ4;

        public int CompressionLevel { get; set; } = 2;

        /// <summary>Gives the nominal scan priod for recording groups.  If this value is zero it disables peridic recording of groups</summary>
        public TimeSpan NominalScanPeriod { get; set; } = (0.1).FromSeconds();

        /// <summary>
        /// Gives an array of selectable nominal scan periods.  
        /// These are used with the SelectScanPeriodSchedule method if the client would like to be able to vary the nominal scan period based on external information.
        /// If a value is selected that is outside of the usable indecies for this array then the NominalScanPeriod will be used instead.
        /// </summary>
        public TimeSpan[] ScanPeriodScheduleArray { get; set; }

        /// <summary>Gives the IVI instance that is to be used for defining group and reading values.  The Values.Instance singleton will be used if this property is null.</summary>
        public IValuesInterconnection SourceIVI { get; set; }

        /// <summary>Set to true to allocate user row flag bit values for each group that is defined here.</summary>
        public bool AllocateUserRowFlagBitsForGroups { get; set; }

        /// <summary>Gives the array of GroupDefinitionItems that are used to define the groups that will be recorded by the engine</summary>
        public MDRF2RecordingEngineItems.GroupDefinitionItem[] GroupDefinitionItemArray { get; set; }

        /// <summary>Gives the array of OccurrenceDefinitionItems that are used to define the occurrence types that may be directly recorded by this engine.  The client may also directly register OccurrenceInfo objects with the underlying MDRFWriter.</summary>
        public MDRF2RecordingEngineItems.OccurrenceDefinitionItem[] OccurrenceDefinitionItemArray { get; set; }

        /// <summary>Gives the pruning configuration for the DirectoryTreePruningManager that can be used by the engine to prevent the engine from filling the local storage.</summary>
        public DirectoryTreePruningManager.Config PruningConfig { get; set; }

        /// <summary>Defines the nominal interval that the DirectoryTreePruningManager will be serviced.</summary>
        public TimeSpan NominalPruningInterval { get; set; } = (10.0).FromSeconds();

        private static readonly TimeSpan[] emptyTimeSpanArray = EmptyArrayFactory<TimeSpan>.Instance;

        /// <summary>Defines ActionLoggingConfig used for most action factory methods in resulting part.</summary>
        public ActionLoggingConfig ActionLoggingConfig { get; set; } = ActionLoggingConfig.Debug_Error_Trace_Trace;

        /// <summary>Defines ActionLoggingConfig used for actions that are indicated by the client as being "high rate".</summary>
        public ActionLoggingConfig HighRateActionLoggingConfig { get; set; } = ActionLoggingConfig.Trace_Trace_Trace_Trace;
    }

    namespace MDRF2RecordingEngineItems
    {
        /// <summary>
        /// Defines the characteristics of a Group that will be recorded using an MDRF2RecordingEngine
        /// </summary>
        public struct GroupDefinitionItem
        {
            /// <summary>Gives the name of this group</summary>
            public string GroupName { get; set; }

            /// <summary>Gives the MatchRuleSet for the set of IVA names that are to be included in this group.  When null, all names may be included in this group.</summary>
            public MatchRuleSet MatchRuleSet { get; set; }

            /// <summary>Gives an optional TableItemFilter that (when non-null) will be used to filter the IVA entires before applying the MatchRuleSet to define which IVA items are in each group.  Each IVA will only show up in the first group that accepts its name and metadata information.</summary>
            public Func<string, INamedValueSet, bool> TableItemFilter { get; set; }

            /// <summary>Gives the meta data that is to be recorded with the group definition in each resulting MDRF file that is created by the engine</summary>
            public INamedValueSet GroupMetaData { get { return _groupMetaData; } set { _groupMetaData = value.ConvertToReadOnly(mapNullToEmpty: false); } }

            private INamedValueSet _groupMetaData;
        }

        /// <summary>
        /// Defines the characteristics of an Occurrence that can be recorded using an MDRF2RecordingEngine
        /// </summary>
        public struct OccurrenceDefinitionItem
        {
            /// <summary>Gives the desired Occurrence name</summary>
            public string OccurrenceName { get; set; }

            /// <summary>Gives the supported set of Event names that will use this occurrence</summary>
            public string[] EventNames { get; set; }

            /// <summary>Gives the meta data that is to be recorded with the occurrence definition in each resulting MDRF file that is created by the engine</summary>
            public INamedValueSet OccurrenceMetaData { get { return _occurrenceMetaData; } set { _occurrenceMetaData = value.ConvertToReadOnly(mapNullToEmpty: false); } }

            private INamedValueSet _occurrenceMetaData;
        }
    }

    #endregion

    /// <summary>
    /// This class defines a part that can be used to implement standard MDRF recording scenarios.
    /// This part filters the given SourceIVI and defines groups to be recorded on the first use of a GoOnline action.
    /// This part only records MDRF files while it is in an online state.
    /// </summary>
    public class MDRFRecordingEngine : SimpleActivePartBase, IMDRF2RecordingEngine
    {
        #region Construction (et. al.)

        public MDRFRecordingEngine(MDRF2RecordingEngineConfig config)
            : base(config.PartID, SimpleActivePartBaseSettings.DefaultVersion2.Build(waitTimeLimit: (0.01).FromSeconds(), disableBusyBehavior: true))
        {
            Config = new MDRF2RecordingEngineConfig(config);

            ActionLoggingReference.Config = Config.ActionLoggingConfig;
            highRateActionLoggingReference = new ActionLogging(Log, Config.HighRateActionLoggingConfig);

            AddExplicitDisposeAction(() => Fcns.DisposeOfObject(ref mdrfWriter));
        }

        public MDRF2RecordingEngineConfig Config { get; private set; }

        private readonly ActionLogging highRateActionLoggingReference;

        #endregion

        #region private fields (et. al.)

        IMDRF2Writer mdrfWriter;
        QpcTimer mdrfWriterServiceTimer;

        DirectoryTreePruningManager pruningManager;
        QpcTimer pruningServiceTimer;

        #endregion

        #region IMDRFRecordingEngine implementation

        /// <summary>
        /// Gives the client direct access to the MDRFWriter that this object creates and uses.
        /// <para/>Warning: this property will be null until the part has been set online
        /// </summary>
        public IMDRF2Writer MDRFWriter { get { return mdrfWriter; } }

        IMDRFWriter IMDRFRecordingEngine.MDRFWriter { get { return mdrfWriter; } }

        /// <summary>
        /// StringParamAction action factory method.
        /// The resulting action, when run, will record an occurrance containing the eventName and the given eventDataNVS.
        /// The OccurrenceInfo used for this event is determined from the given eventName.  
        /// If the eventName matches a name registered with a specific occurrance then that occurrence will be used, otherwise the DefaultRecordEventOccurrance will be used.
        /// </summary>
        public IStringParamAction RecordEvent(string eventName = null, INamedValueSet eventDataNVS = null, bool writeAll = false)
        {
            ActionMethodDelegateActionArgStrResult<string, NullObj> performRecordEventDelegate = (actionProviderFacet) => PerformRecordEvent(actionProviderFacet, writeAll);
            IStringParamAction action = new StringActionImpl(actionQ, eventName, performRecordEventDelegate, writeAll ? "RecordEvent+writeAll" : "RecordEvent", ActionLoggingReference);

            if (!eventDataNVS.IsNullOrEmpty())
                action.NamedParamValues = eventDataNVS;

            return action;
        }

        private string PerformRecordEvent(IProviderActionBase<string, NullObj> action, bool writeAll)
        {
            string eventName = action.ParamValue;
            INamedValueSet eventDataNVS = action.NamedParamValues;

            if (!BaseState.IsOnline)
                return "Part is not Online";

            NamedValueSet occurrenceDataNVS = new NamedValueSet() { { "eventName", eventName } }.MergeWith(eventDataNVS, NamedValueMergeBehavior.AddNewItems);

            MDRF.Writer.OccurrenceInfo occurrenceInfo = null;

            if (!eventNameToOccurrenceInfoDictionary.TryGetValue(eventName, out occurrenceInfo) || occurrenceInfo == null)
                occurrenceInfo = defaultRecordEventOccurranceInfo;

            lastWriterClientOpRequestCount++;

            return mdrfWriter.RecordOccurrence(occurrenceInfo, occurrenceDataNVS, writeAll: writeAll);
        }

        /// <summary>
        /// Action factory method.
        /// The resulting action, when run, will record the requested occurrence with the given nvs attached.
        /// The OccurrenceDefinitionInfo used for this event is determined from the given occurrenceName.  
        /// </summary>
        public IClientFacet RecordOccurrence(string occurrenceName = null, INamedValueSet occurrenceDataNVS = null, bool writeAll = false)
        {
            var action = new BasicActionImpl(actionQ, (a) => PerformRecordEvent(occurrenceName, a.NamedParamValues, writeAll), "RecordOccurrence", ActionLoggingReference, "{0}{1}".CheckedFormat(occurrenceName, writeAll ? ",writeAll" : ""));

            if (!occurrenceDataNVS.IsNullOrEmpty())
                action.NamedParamValues = occurrenceDataNVS;

            return action;
        }

        private string PerformRecordEvent(string occurrenceName, INamedValueSet occurrenceDataNVS, bool writeAll)
        {
            if (!BaseState.IsOnline)
                return "Part is not Online";

            MDRF.Writer.OccurrenceInfo occurrenceInfo = null;

            if (!occurrenceInfoDictionary.TryGetValue(occurrenceName.Sanitize(), out occurrenceInfo) || occurrenceInfo == null)
                return "'{0}' not a known occurrence name".CheckedFormat(occurrenceName);

            lastWriterClientOpRequestCount++;

            return mdrfWriter.RecordOccurrence(occurrenceInfo, occurrenceDataNVS, writeAll: writeAll);
        }

        /// <summary>
        /// Action factory method.
        /// The resulting action, when run, will record the requested occurrence with the given nvs attached.
        /// </summary>
        public IClientFacet RecordOccurrence(IOccurrenceInfo occurrenceInfo = null, INamedValueSet occurrenceDataNVS = null, bool writeAll = false)
        {
            var action = new BasicActionImpl(actionQ, (a) => PerformRecordOccurrence(occurrenceInfo, a.NamedParamValues, writeAll), "RecordOccurrence", ActionLoggingReference, "{0}{1}".CheckedFormat(occurrenceInfo.Name, writeAll ? ",writeAll" : ""));

            if (!occurrenceDataNVS.IsNullOrEmpty())
                action.NamedParamValues = occurrenceDataNVS;

            return action;
        }

        private string PerformRecordOccurrence(IOccurrenceInfo occurrenceInfo, INamedValueSet occurrenceDataNVS, bool writeAll)
        {
            if (!BaseState.IsOnline)
                return "Part is not Online";

            lastWriterClientOpRequestCount++;

            return mdrfWriter.RecordOccurrence(occurrenceInfo, occurrenceDataNVS, writeAll: writeAll);
        }

        MDRF.Writer.OccurrenceInfo defaultRecordEventOccurranceInfo;
        private Dictionary<string, MDRF.Writer.OccurrenceInfo> eventNameToOccurrenceInfoDictionary = new Dictionary<string, MDRF.Writer.OccurrenceInfo>();
        private Dictionary<string, MDRF.Writer.OccurrenceInfo> occurrenceInfoDictionary = new Dictionary<string, MDRF.Writer.OccurrenceInfo>();

        ///<inheritdoc/>
        public IClientFacet RecordObject(object obj, bool writeAll = false, bool isHighRate = true, bool isSignificant = false, ulong userRowFlagBits = 0)
        {
            return new BasicActionImpl(ActionQueue, () => PerformRecordObjects(obj, null, writeAll: writeAll, isSignificant, userRowFlagBits), "RecordObject", isHighRate ? highRateActionLoggingReference : ActionLoggingReference, obj.SafeToString());
        }

        ///<inheritdoc/>
        public IClientFacet RecordObjects(object[] objArray, bool writeAll = false, bool isHighRate = true, bool isSignificant = false, ulong userRowFlagBits = 0)
        {
            return new BasicActionImpl(ActionQueue, () => PerformRecordObjects(null, objArray, writeAll: writeAll, isSignificant, userRowFlagBits), "RecordObject", isHighRate ? highRateActionLoggingReference : ActionLoggingReference, String.Join(",", objArray.Select(obj => obj.SafeToString())));
        }

        private string PerformRecordObjects(object obj1, object[] objArray, bool writeAll, bool isSignificant, ulong userRowFlagBits)
        {
            if (!BaseState.IsOnline)
                return "Part is not Online";

            string ec = string.Empty;
            if (obj1 != null)
            {
                ec = mdrfWriter.RecordObject(obj1, writeAll: writeAll, isSignificant: isSignificant, userRowFlagBits: userRowFlagBits);

                lastWriterClientOpRequestCount++;
            }

            if (!objArray.IsNullOrEmpty() && ec.IsNullOrEmpty())
            {
                var arrayLen = objArray.Length;
                for (int idx = 0; idx < arrayLen; idx++)
                {
                    var obj = objArray[idx];

                    ec = mdrfWriter.RecordObject(obj, writeAll: writeAll, isSignificant: isSignificant, userRowFlagBits: userRowFlagBits);

                    lastWriterClientOpRequestCount++;

                    if (ec.IsNeitherNullNorEmpty())
                        break;
                }
            }

            return ec;
        }

        ///<inheritdoc/>
        public IClientFacet Flush(FlushFlags flushFlags = FlushFlags.All, bool isHighRate = true)
        {
            return new BasicActionImpl(ActionQueue, () => PerformFlush(flushFlags), "Flush", isHighRate ? highRateActionLoggingReference : ActionLoggingReference, $"{flushFlags}");
        }

        private string PerformFlush(FlushFlags flushFlags)
        {
            lastWriterClientOpRequestCount++;

            return mdrfWriter.Flush(flushFlags);
        }

        /// <summary>
        /// This method may be used by a client to select a new scheduled scan period using the given scanPeriodScheduleSelection value (converted to an Int32)
        /// </summary>
        public void SelectScanPeriodSchedule<TScheduleIndex>(TScheduleIndex scanPeriodScheduleSelection)
            where TScheduleIndex : struct
        {
            try
            {
                volatileScanPeriodScheduleIndex = (int)System.Convert.ChangeType(scanPeriodScheduleSelection, typeof(int));
            }
            catch
            {
                volatileScanPeriodScheduleIndex = 0;
            }

            this.Notify();
        }

        volatile int volatileScanPeriodScheduleIndex = 0;
        int currentScanPeriodScheduleIndex = 0;

        #endregion

        #region SimpleActivePartBase overrides (PerformGoOnlineAction, PerformGoOfflineAction, MainThreadFcn, PerformMainLoopService)

        bool once = true;

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            if (Config.PruningConfig != null && pruningManager == null)
            {
                pruningManager = new DirectoryTreePruningManager("{0}.pm".CheckedFormat(PartID), Config.PruningConfig);
                pruningServiceTimer = new QpcTimer() { TriggerInterval = Config.NominalPruningInterval, AutoReset = true, Started = true };
            }

            if (once)
            {
                once = false;

                List<UInt64> availableUserFlagRowBitsList = new List<UInt64>(Enumerable.Range(0, 63).Select(bitIdx => (1UL << bitIdx)));

                List<string> ivaNamesList = new List<string>(Config.SourceIVI.ValueNamesArray);

                List<MDRF.Writer.GroupInfo> groupInfoList = new List<MDRF.Writer.GroupInfo>();

                if (Config.GroupDefinitionItemArray == null)
                {
                    IValueAccessor[] ivaArray = ivaNamesList.Select(name => Config.SourceIVI.GetValueAccessor(name)).ToArray();
                    groupInfoList.Add(new MDRF.Writer.GroupInfo()
                    {
                        Name = "Default",
                        GroupBehaviorOptions = GroupBehaviorOptions.UseSourceIVAsForTouched,
                        FileIndexUserRowFlagBits = Config.AllocateUserRowFlagBitsForGroups ? availableUserFlagRowBitsList.SafeTakeFirst() : 0,
                        IVI = Config.SourceIVI,
                        GroupPointInfoArray = ivaArray.Select(iva => new MDRF.Writer.GroupPointInfo() { Name = iva.Name, ValueSourceIVA = iva, ClientNVS = iva.MetaData }).ToArray(),
                    });
                }
                else
                {
                    foreach (var item in Config.GroupDefinitionItemArray)
                    {
                        MatchRuleSet matchRuleSet = item.MatchRuleSet.MapDefaultTo(MatchRuleSet.Any);
                        string[] matchedIVANamesArray = ((item.TableItemFilter != null) ? Config.SourceIVI.GetFilteredNames(item.TableItemFilter) : ivaNamesList.ToArray())
                                                        .Where(ivaName => matchRuleSet.MatchesAny(ivaName))
                                                        .ToArray();

                        foreach (string matchedIVAName in matchedIVANamesArray)
                            ivaNamesList.Remove(matchedIVAName);

                        IValueAccessor[] ivaArray = matchedIVANamesArray.Select(name => Config.SourceIVI.GetValueAccessor(name)).ToArray();

                        groupInfoList.Add(new MDRF.Writer.GroupInfo()
                        {
                            Name = item.GroupName,
                            GroupBehaviorOptions = GroupBehaviorOptions.UseSourceIVAsForTouched,
                            FileIndexUserRowFlagBits = Config.AllocateUserRowFlagBitsForGroups ? availableUserFlagRowBitsList.SafeTakeFirst() : 0,
                            IVI = Config.SourceIVI,
                            GroupPointInfoArray = ivaArray.Select(iva => new MDRF.Writer.GroupPointInfo() { Name = iva.Name, ValueSourceIVA = iva, ClientNVS = iva.MetaData }).ToArray(),
                            ClientNVS = item.GroupMetaData,
                        });
                    }
                }

                defaultRecordEventOccurranceInfo = new MDRF.Writer.OccurrenceInfo()
                {
                    Name = "DefaultRecordEventOccurrance",
                    CST = ContainerStorageType.Object,
                };

                List<MDRF.Writer.OccurrenceInfo> occuranceInfoList = new List<MDRF.Writer.OccurrenceInfo>();
                occuranceInfoList.Add(defaultRecordEventOccurranceInfo);

                if (!Config.OccurrenceDefinitionItemArray.IsNullOrEmpty())
                {
                    foreach (var item in Config.OccurrenceDefinitionItemArray)
                    {
                        INamedValueSet occurrenceInfoNVS = (item.EventNames.IsNullOrEmpty()
                                                            ? item.OccurrenceMetaData
                                                            : new NamedValueSet() { { "eventNames", item.EventNames } }.MergeWith(item.OccurrenceMetaData, NamedValueMergeBehavior.AddNewItems));

                        MDRF.Writer.OccurrenceInfo occurrenceInfo = new MDRF.Writer.OccurrenceInfo()
                        {
                            Name = item.OccurrenceName,
                            FileIndexUserRowFlagBits = availableUserFlagRowBitsList.SafeTakeFirst(),
                            ClientNVS = occurrenceInfoNVS.ConvertToReadOnly(),
                        };

                        occuranceInfoList.Add(occurrenceInfo);

                        item.EventNames.DoForEach(eventName => { eventNameToOccurrenceInfoDictionary[eventName.Sanitize()] = occurrenceInfo; });

                        occurrenceInfoDictionary[occurrenceInfo.Name.Sanitize()] = occurrenceInfo;
                    }
                }

                var writerConfig = new MDRF2WriterConfig()
                {
                    PartID = $"{PartID}.mw",
                    SetupInfo = Config.SetupInfo,
                    WriterBehavior = MDRF2WriterConfigBehavior.EnableAPILocking,
                    CompressorSelect = Config.CompressorSelect,
                    CompressionLevel = Config.CompressionLevel,
                };

                mdrfWriter = new MDRF2Writer(writerConfig);
                mdrfWriter.AddRange(groupInfoList);
                mdrfWriter.AddRange(occuranceInfoList);

                lastWriterClientOpRequestCount = default;
                lastWriterClientOpRequestTimeStamp = default;

                mdrfWriterServiceTimer = new QpcTimer() { TriggerInterval = Config.NominalScanPeriod, AutoReset = true, Started = !Config.NominalScanPeriod.IsZero() };
            }

            return string.Empty;
        }

        protected override string PerformGoOfflineAction()
        {
            PerformMainLoopService(forceServiceWriter: true, forceServicePruning: true);

            lastWriterClientOpRequestCount++;

            mdrfWriter.CloseCurrentFile("Closed by request: {0}".CheckedFormat(CurrentActionDescription));

            return string.Empty;
        }

        protected override void MainThreadFcn()
        {
            base.MainThreadFcn();

            PerformGoOfflineAction();
        }

        protected override void PerformMainLoopService()
        {
            int capturedScanPeriodScheduleIndex = volatileScanPeriodScheduleIndex;
            bool scanPeriodScheduleChange = (capturedScanPeriodScheduleIndex != currentScanPeriodScheduleIndex);

            if (scanPeriodScheduleChange)
            {
                currentScanPeriodScheduleIndex = capturedScanPeriodScheduleIndex;
                TimeSpan scanPeriod = Config.ScanPeriodScheduleArray.SafeAccess(currentScanPeriodScheduleIndex, defaultValue: Config.NominalScanPeriod);

                if (!scanPeriod.IsZero())
                {
                    mdrfWriterServiceTimer.Start(scanPeriod);

                    Log.Debug.Emit("Scan period set to index:{0} period:{1:f3} sec", currentScanPeriodScheduleIndex, scanPeriod.TotalSeconds);
                }
                else
                {
                    mdrfWriterServiceTimer.Stop();

                    Log.Debug.Emit("Scan stopped [index:{0} period:{1:f3} sec]", currentScanPeriodScheduleIndex, scanPeriod.TotalSeconds);
                }

                PerformMainLoopService(forceServiceWriter: true);
            }
            else
            {
                PerformMainLoopService(forceServiceWriter: false, forceServicePruning: false);
            }
        }

        private void PerformMainLoopService(bool forceServiceWriter = false, bool forceServicePruning = false)
        {
            if (BaseState.IsOnline)
            {
                if (mdrfWriterServiceTimer.IsTriggered || forceServiceWriter)
                {
                    mdrfWriter.RecordGroups(writeAll: forceServiceWriter);

                    lastWriterClientOpRequestCount++;
                }

                InnerServiceTimerTriggeredFlush();

                if (pruningManager != null && (pruningServiceTimer.IsTriggered || forceServicePruning))
                {
                    MDRF.Writer.FileInfo? closedFileInfo = mdrfWriter.NextClosedFileInfo;
                    if (closedFileInfo != null)
                        pruningManager.NotePathAdded(closedFileInfo.GetValueOrDefault().FilePath);

                    pruningManager.Service();
                }
            }
        }

        private ulong lastWriterClientOpRequestCount = 0;
        QpcTimeStamp lastWriterClientOpRequestTimeStamp;

        private void InnerServiceTimerTriggeredFlush()
        {
            var capturedWriterClientOpRequestCount = mdrfWriter.ClientOpRequestCount;

            var lastWriterClientOpRequestTimeStampIsZero = lastWriterClientOpRequestTimeStamp.IsZero;

            if (lastWriterClientOpRequestCount != capturedWriterClientOpRequestCount && lastWriterClientOpRequestTimeStampIsZero)
            {
                lastWriterClientOpRequestCount = capturedWriterClientOpRequestCount;
                lastWriterClientOpRequestTimeStamp = QpcTimeStamp.Now;

                Log.Trace.Emit("External writer client op detected");
            }
            else if (!lastWriterClientOpRequestTimeStampIsZero && lastWriterClientOpRequestTimeStamp.Age > Config.SetupInfo.MinNominalFileIndexWriteInterval && mdrfWriter.IsFileOpen)
            {
                lastWriterClientOpRequestCount = capturedWriterClientOpRequestCount + 1;
                lastWriterClientOpRequestTimeStamp = default;

                mdrfWriter.Flush();

                Log.Trace.Emit("Writer flushed after external writer client op detected.");
            }
        }

        #endregion
    }

    #region MDRF2LogMessageHandlerAdapter, MDRF2LogMessageHandlerAdapterConfig

    public class MDRF2LogMessageHandlerAdapterConfig : ICopyable<MDRF2LogMessageHandlerAdapterConfig>
    {
        [ConfigItem(IsOptional = true)]
        public ulong NormalMesgUserFileIndexRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool NormalMesgFlushesWriter { get; set; }

        [ConfigItem(IsOptional = true)]
        public Logging.LogGate SignificantMesgTypeLogGate { get; set; } = Logging.LogGate.Signif;

        [ConfigItem(IsOptional = true)]
        public ulong SignificantMesgUserFileIndexRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool SignificantMesgIsHighPriority { get; set; } = true;

        [ConfigItem(IsOptional = true)]
        public bool SignificantMesgFlushesWriter { get; set; }

        [ConfigItem(IsOptional = true)]
        public MDRF.Writer.FlushFlags FlushFlagsToUseAfterEachMessageBlock { get; set; } = MDRF.Writer.FlushFlags.None;

        [ConfigItem(IsOptional = true)]
        public bool OnlyRecordMessagesIfFileIsAlreadyActive { get; set; }

        public MDRF2LogMessageHandlerAdapterConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (MDRF2LogMessageHandlerAdapterConfig)MemberwiseClone();
        }

        /// <summary>
        /// Update this object's ConfigItem marked public properties from corresponingly named config keys (using the namePrefix)
        /// </summary>
        public MDRF2LogMessageHandlerAdapterConfig Setup(string prefixName = "Logging.LMH.MDRF2LogMessageHandler.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            ConfigValueSetAdapter<MDRF2LogMessageHandlerAdapterConfig> adapter = new ConfigValueSetAdapter<MDRF2LogMessageHandlerAdapterConfig>(config) 
            { 
                ValueSet = this, 
                SetupIssueEmitter = issueEmitter, 
                UpdateIssueEmitter = issueEmitter, 
                ValueNoteEmitter = valueEmitter 
            }.Setup(prefixName);

            return this;
        }
    }

    public class MDRF2LogMessageHandlerAdapter : MosaicLib.Logging.Handlers.SimpleLogMessageHandlerBase
    {
        public MDRF2LogMessageHandlerAdapter(string name, Logging.LogGate logGate, IMDRF2Writer mdrfWriter, MDRF2LogMessageHandlerAdapterConfig config)
            : base(name, logGate, recordSourceStackFrame: false)
        {
            this.mdrfWriter = mdrfWriter;
            Config = config.MakeCopyOfThis();

            // do not attempt to dispose of the mdrfWriter when this class stops using it.
            AddExplicitDisposeAction(() => { this.mdrfWriter = null; });
        }

        IMDRF2Writer mdrfWriter;
        MDRF2LogMessageHandlerAdapterConfig Config { get; set; }

        public override void HandleLogMessages(Logging.LogMessage[] lmArray)
        {
            int lmArrayLen = lmArray.SafeLength();

            if (lmArrayLen == 1)
                InnerInnerHandleLogMessage(lmArray[0]);
            else
            {
                foreach (Logging.LogMessage lm in lmArray)
                {
                    InnerInnerHandleLogMessage(lm, blockFlush: true);
                }

                if (Config.FlushFlagsToUseAfterEachMessageBlock != MDRF.Writer.FlushFlags.None && mdrfWriter?.IsFileOpen == true)
                    mdrfWriter?.Flush(Config.FlushFlagsToUseAfterEachMessageBlock);
            }


            NoteMessagesHaveBeenDelivered();
        }

        protected override void InnerHandleLogMessage(Logging.LogMessage lm)
        {
            InnerInnerHandleLogMessage(lm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InnerInnerHandleLogMessage(Logging.LogMessage lm, bool blockFlush = false, bool forceFlush = false)
        {
            if (!IsMessageTypeEnabled(lm) || lm.NamedValueSet.Contains("noMDRF") || (Config.OnlyRecordMessagesIfFileIsAlreadyActive && mdrfWriter?.IsFileOpen != true))
                return;

            Logging.MesgType mesgType = lm.MesgType;
            bool mesgTypeIsSignificant = Config.SignificantMesgTypeLogGate.IsTypeEnabled(mesgType);

            forceFlush |= !blockFlush && (mesgTypeIsSignificant ? Config.SignificantMesgFlushesWriter : Config.NormalMesgFlushesWriter);
            var userRowFlagBits = mesgTypeIsSignificant ? Config.SignificantMesgUserFileIndexRowFlagBits : Config.NormalMesgUserFileIndexRowFlagBits;

            mdrfWriter?.RecordObject(lm, forceFlush: forceFlush, isSignificant: mesgTypeIsSignificant, userRowFlagBits: userRowFlagBits);
        }
    }

    #endregion
}
