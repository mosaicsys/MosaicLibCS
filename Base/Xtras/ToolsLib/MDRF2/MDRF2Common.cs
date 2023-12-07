//-------------------------------------------------------------------
/*! @file MDRF2Common.cs
 *  @brief definitions that are common to MDRF2 (Mosaic Data Recording Format 2) readers and writers.
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using MessagePack;
using MessagePack.Formatters;

using Mosaic.ToolsLib.MDRF2.Reader;
using Mosaic.ToolsLib.MessagePackUtils;
using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.Attributes;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace Mosaic.ToolsLib.MDRF2.Common
{
    #region Constants (version information)

    public static partial class Constants
    {
        /// <summary>
        /// <para/>Type = "Mosaic Data Recording File version 2.1 (MDRF2)",
        /// <para/>Name = "Mosaic Data Recording Engine version 2.1 (MDRE2) CS API",
        /// <para/>Version = {see remarks below},
        /// <para/>This information is included in the "Lib" record which is found in the second block of each MDRF2 file.
        /// </summary>
        /// <remarks>
        /// Version history:
        /// 2.0.0 (2020-06-03) : First MDRF2 version.
        /// 2.1.0 (2022-05-26) : Added KeyID/KeyName concept for use with RecordObject.  Writen files are format compatible with prior version(s) when KeyID/KeyName concept is not actively being used.
        ///                      Added TypeID/TypeKeyName concept for use with RecordObject in order to support client provided custom object serializers (formatters) and matching deserializers for use during query operations.  
        ///                      Added support for new write behavior option to write TypeIDs in place of TypeKeyName.
        /// 2.1.1 (2022-07-28) : Addition of SessionInfo in InlineMap in second header.
        /// 2.1.2 (2022-10-01) : Addition of optional MDRF2WriterConfigBehavior.SortPointsByDataType concept.
        ///                      Added IMDRF2Writer.RecordObjects method.
        /// </remarks>
        public static readonly ILibraryInfo Lib2Info = new LibraryInfo()
        {
            Type = "Mosaic Data Recording File version 2.1 (MDRF2)",
            Name = "Mosaic Data Recording Engine version 2.1 (MDRE2) CS API",
            Version = "2.1.2 (2022-10-01)",
        };
    }

    #endregion

    #region Constants (keywords and file extension related constant strings)

    public static partial class Constants
    {
        /// <summary>Keyword used in InlineMAP to indicate the first record in the file.  This InlineMAP must also contain a DateTime item.</summary>
        public const string FileStartKeyword = "MDRF2.Start";

        /// <summary>InlineMAP key name used to contain the DateTimeNVS</summary>
        public const string DateTimeNVSKeyName = "DateTime";

        /// <summary>
        /// InlineMAP key name used to contain the Instance UUID.  
        /// This is an identifier that is uniquely assigned and recorded at the start of each file.
        /// </summary>
        public const string FileInstanceUUIDKeyName = "Instance.UUID";

        /// <summary>InlineMAP key name used to contain the LibNVS</summary>
        public const string LibNVSKeyName = "Lib";

        /// <summary>InlineMAP key name used to contain the ClientNVS (from the SetupInfo).</summary>
        public const string SetupNVSKeyName = "Setup";

        /// <summary>InlineMAP key name used to contain the ClientNVS (from the SetupInfo).</summary>
        public const string ClientNVSKeyName = "Client";

        /// <summary>InlineMAP key name used to contain the ClientNVS (from the SetupInfo).</summary>
        public const string HostNameKeyName = "HostName";

        /// <summary>InlineMAP key name used to contain the ClientNVS (from the SetupInfo).</summary>
        public const string CurrentProcessKeyName = "CurrentProcess";

        /// <summary>InlineMAP key name used to contain the ClientNVS (from the SetupInfo).</summary>
        public const string EnvironmentKeyName = "Environment";

        /// <summary>InlineMAP key name used to contain the Session specific information.</summary>
        public const string SessionInfoKeyName = "SessionInfo";

        /// <summary>InlineMAP key name used to contain the SpecItems array of NVS</summary>
        public const string SpecItemsNVSListKey = "SpecItems";

        /// <summary>InlineMAP key name used to contain a full (or incremental) KeyID mapping NVS (set of names and ID numbers)</summary>
        public const string KeyIDsInlineMapKey = "KeyIDs";

        /// <summary>InlineMAP key name used to contain a full (or incremental) TypeID mapping NVS (set of names and ID numbers)</summary>
        public const string TypeIDsInlineMapKey = "TypeIDs";

        /// <summary>Keyword used in InlineMAP record to indicate the last record in the file.</summary>
        public const string FileEndKeyword = "MDRF2.End";

        /// <summary>Object short/known type for a LogMessage or ILogMessage</summary>
        public const string ObjKnownType_LogMessage = "LogMesg";

        /// <summary>Object short/known type for an E039Object or IE039Object</summary>
        public const string ObjKnownType_E039Object = "E039Obj";

        /// <summary>Object short/known type for a ValueContainer</summary>
        public const string ObjKnownType_ValueContainer = "VC";

        /// <summary>Object short/known type for a NamedValueSet and/or KVCSet</summary>
        public const string ObjKnownType_NamedValueSet = "NVS";

        /// <summary>Object short/known type any other type that supports custom serialization</summary>
        public const string ObjKnownType_TypeAndValueCarrier = "tavc";

        /// <summary>InlineMAP key name used to carry Normal Mesg contents</summary>
        public const string ObjKnownType_Mesg = "Mesg";

        /// <summary>InlineMAP key name used to carry Error Mesg contents</summary>
        public const string ObjKnownType_Error = "Error";

        /// <summary>file extension used for mdrf1 files [.mdrf]</summary>
        public const string mdrf1FileExtension = ".mdrf";

        /// <summary>file extension used for mdrf1 files [.mdrf1]</summary>
        public const string mdrf1AltFileExtension = ".mdrf1";

        /// <summary>base file extension used for mdrf2 files [.mdrf2]</summary>
        public const string mdrf2BaseFileExtension = ".mdrf2";

        /// <summary>full file extension for lz4 compressed mdrf2 files [.mdrf2.lz4]</summary>
        public const string mdrf2LZ4FileExtension = mdrf2BaseFileExtension + Compression.Constants.LZ4FileExtension;

        /// <summary>full file extension for gzip compressed mdrf2 files [.mdrf2.gz]</summary>
        public const string mdrf2GZipFileExtension = mdrf2BaseFileExtension + Compression.Constants.GZipFileExtension;

        /// <summary>full file extension for lz4 compressed mdrf1 files [.mdrf.lz4]</summary>
        public const string mdrf1LZ4FileExtension = mdrf1FileExtension + Compression.Constants.LZ4FileExtension;

        /// <summary>full file extension for gzip compressed mdrf1 files [.mdrf.gz]</summary>
        public const string mdrf1GZipFileExtension = mdrf1FileExtension + Compression.Constants.GZipFileExtension;

        /// <summary>full file extension for lz4 compressed mdrf1 files [.mdrf1.lz4]</summary>
        public const string mdrf1AltLZ4FileExtension = mdrf1AltFileExtension + Compression.Constants.LZ4FileExtension;

        /// <summary>full file extension for gzip compressed mdrf1 files [.mdrf1.gz]</summary>
        public const string mdrf1AltGZipFileExtension = mdrf1AltFileExtension + Compression.Constants.GZipFileExtension;
    }

    #endregion

    #region MDRF2 ISessionInfo, SessionInfo

    /// <summary>
    /// Gives information about the recording session
    /// </summary>
    public interface ISessionInfo : IEquatable<ISessionInfo>
    {
        /// <summary>
        /// Gives the ID for this session which is either manually assigned or which uses a single D format UUID generated for this session.
        /// Each process that starts recording MDRF2 files will generate a single UUID on first use for this session and all recorded files
        /// for the duration of this process will use this value.
        /// <para/>The recorded value can be overridden by manually asigning the <see cref="SessionInfo.SharedSessionID"/> proprty prior to its first use.
        /// </summary>
        string SessionID { get; }

        /// <summary>
        /// Gives the ID associated with this writer instance.  
        /// Normally this is generated at writer construction time to contain a UUID in D format.
        /// </summary>
        string WriterInstanceID { get; }

        /// <summary>
        /// Carries the UTC <see cref="DateTime"/> taken when the the <see cref="SessionID"/> was generated.
        /// </summary>
        DateTime UtcDateTime { get; }

        /// <summary>
        /// Updates the contents of the given <paramref name="nvs"/> using the keys and values based on the names and contents of this object's properties.
        /// </summary>
        NamedValueSet UpdateNVSFromThis(NamedValueSet nvs);
    }

    /// <summary>
    /// This is the standard implementation object for the <see cref="ISessionInfo"/> interface
    /// </summary>
    public class SessionInfo : ISessionInfo
    {
        /// <summary>
        /// Gives the single UUID value that is used for shared session IDs when the client code does not explicitly provided a custom non-empty one.
        /// </summary>
        public static readonly string SharedSessionUUID = Guid.NewGuid().ToString("D");

        /// <summary>
        /// Gives the single UTC <see cref="DateTime"/> that is used for shared session info when client code does not explicitly provide a custom non-empty one.
        /// </summary>
        public static readonly DateTime SharedSessionUtcDateTime = DateTime.UtcNow;

        /// <summary>
        /// Gives the a <see cref="ISessionInfo"/> instance that contains the <see cref="SharedSessionUUID"/> and <see cref="SharedSessionUtcDateTime"/>.
        /// </summary>
        public static ISessionInfo SharedSessionInfo => new SessionInfo() { SessionID = SharedSessionUUID, UtcDateTime = SharedSessionUtcDateTime };

        /// <summary>Default constructor.  Sets the NVS, Type, Name and Version to be Empty.</summary>
        public SessionInfo()
        {
            SessionID = string.Empty;
            WriterInstanceID = string.Empty;
        }

        /// <summary>Copy constructor.  Sets the NVS to be empty and the Type, Name and Version from the given <paramref name="other"/> instance.</summary>
        public SessionInfo(ISessionInfo other)
        {
            SessionID = other.SessionID;
            WriterInstanceID = other.WriterInstanceID;
            UtcDateTime = other.UtcDateTime;
        }

        /// <summary>
        /// Initializes the <see cref="WriterInstanceID"/> to contain a UUID.
        /// If <paramref name="ifNeeded"/> is true and the current <see cref="WriterInstanceID"/> is non-empty then it will not be changed.
        /// </summary>
        public SessionInfo SetWriterInstanceUUID(bool ifNeeded = true)
        {
            if (!ifNeeded || WriterInstanceID.IsNullOrEmpty())
                WriterInstanceID = Guid.NewGuid().ToString("D");
            return this;
        }

        /// <inheritdoc/>
        public string SessionID { get; set; }

        /// <inheritdoc/>
        public string WriterInstanceID { get; set; }

        /// <inheritdoc/>
        public DateTime UtcDateTime { get; set; }

        /// <summary>Logging and Debugging helper method.</summary>
        public override string ToString()
        {
            return $"SessionInfo SessionID:'{SessionID}' WriterInstanceID:'{WriterInstanceID}' {UtcDateTime.ToLocalTime().CvtToString(Dates.DateTimeFormat.LogDefault)}";
        }

        /// <summary>Updates this object's contents from the contents of the given NVS using the related keys.</summary>
        public SessionInfo UpdateFrom(INamedValueSet nvs)
        {
            SessionID = nvs["SessionID"].VC.GetValueA(rethrow: false).MapNullToEmpty();
            WriterInstanceID = nvs["WriterInstanceID"].VC.GetValueA(rethrow: false).MapNullToEmpty();
            UtcDateTime = nvs["UtcDateTime"].VC.GetValueDT(rethrow: false);

            return this;
        }

        /// <inheritdoc/>
        public NamedValueSet UpdateNVSFromThis(NamedValueSet nvs)
        {
            nvs.SetValue("SessionID", SessionID.CreateVC());
            nvs.SetValue("WriterInstanceID", WriterInstanceID.CreateVC());
            nvs.SetValue("UtcDateTime", UtcDateTime.CreateVC());

            return nvs;
        }

        /// <inheritdoc/>
        public bool Equals(ISessionInfo other)
        {
            return (other != null
                && SessionID == other.SessionID
                && WriterInstanceID == other.WriterInstanceID
                && UtcDateTime == other.UtcDateTime
                );
        }
    }

    #endregion

    #region MDRF2 specific types (MDRF2ReaderFormatException, MDRF2DatTimeStampPair, InlineIndexRecord, KeyInfo)

    /// <summary>
    /// Exception type thrown when the reader encounters an unexpected or unsupported condition, such as when the next MP record is not in its expected format
    /// </summary>
    public class MDRF2ReaderException : System.Exception
    {
        public MDRF2ReaderException(string mesg, System.Exception innerException = null)
            : base(mesg, innerException)
        { }
    }

    /// <summary>
    /// This object is used as the pairing of a FileDeltaTime and a UTCTimeSince1601 for use with MDRF2 recording and query related logic.
    /// Depending on context the FileDeltaTime may contain a qpc time stamp or a file delta time stamp.
    /// </summary>
    public struct MDRF2DateTimeStampPair
    {
        /// <summary>When this value is positive it is a file delta time.  When it is negative it is a QpcTimeStamp.Time value</summary>
        public double FileDeltaTime { get; set; }

        /// <summary>When non-zero this is a UTC DateTime as seconds since 00:00:00.000 Jan 1 1601 (aka the FTime base offset)</summary>
        public double UTCTimeSince1601 { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MDRF2DateTimeStampPair(DateTimeStampPair dtPair, bool setQpcTimeStampIfNeeded = true, bool setUTCTimeSince1601IfNeeded = true)
        {
            if (!dtPair.qpcTimeStamp.IsZero)
                FileDeltaTime = -dtPair.qpcTimeStamp.Time;
            else if (setQpcTimeStampIfNeeded)
                FileDeltaTime = -QpcTimeStamp.Now.Time;
            else
                FileDeltaTime = 0.0;

            if (dtPair.utcTimeSince1601 == 0.0 && !dtPair.dateTime.IsZero())
                UTCTimeSince1601 = dtPair.dateTime.GetUTCTimeSince1601();
            else if (dtPair.utcTimeSince1601 > 0.0)
                UTCTimeSince1601 = dtPair.utcTimeSince1601;
            else if (setUTCTimeSince1601IfNeeded)
                UTCTimeSince1601 = DateTime.Now.GetUTCTimeSince1601();
            else
                UTCTimeSince1601 = 0.0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MDRF2DateTimeStampPair(DateTimeStampPair dtPair, ref MDRF2DateTimeStampPair fileReferenceQPCDTPair)
        {
            if (!dtPair.qpcTimeStamp.IsZero)
                FileDeltaTime = dtPair.qpcTimeStamp.Time + fileReferenceQPCDTPair.FileDeltaTime;
            else
                FileDeltaTime = dtPair.fileDeltaTimeStamp;

            if (dtPair.utcTimeSince1601 == 0.0 && !dtPair.dateTime.IsZero())
                UTCTimeSince1601 = dtPair.dateTime.GetUTCTimeSince1601();
            else
                UTCTimeSince1601 = dtPair.utcTimeSince1601;
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "[Empty]";
            else if (FileDeltaTime < 0.0)
                return $"qpc:{-FileDeltaTime:f6} utcTimeSince1601:{UTCTimeSince1601:f6} [{UTCTimeSince1601.GetDateTimeFromUTCTimeSince1601().ToLocalTime():o}]";
            else
                return $"fdt:{FileDeltaTime:f6} utcTimeSince1601:{UTCTimeSince1601:f6} [{UTCTimeSince1601.GetDateTimeFromUTCTimeSince1601().ToLocalTime():o}]";
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (UTCTimeSince1601 == 0.0 && FileDeltaTime == 0.0); }
        }

        /// <summary>
        /// This property is the old name for the new <see cref="DateTimeUTC"/> property, who's value it now returns
        /// </summary>
        [Obsolete("This property is obsolete.  Please use the DateTimeUTC or DateTimeLocal properties in its place (2023-09-26)")]
        public DateTime DateTime => DateTimeUTC;

        /// <summary>
        /// Returns the contains <see cref="UTCTimeSince1601"/> converted to a UTC <see cref="System.DateTime"/> using the double.GetDateTimeFromUTCTimeSince1601() extension method
        /// </summary>
        public DateTime DateTimeUTC
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UTCTimeSince1601.GetDateTimeFromUTCTimeSince1601();
        }

        /// <summary>
        /// Returns the contains <see cref="UTCTimeSince1601"/> converted to a Local <see cref="System.DateTime"/> using the <see cref="System.DateTime.ToLocalTime()"/> method on the <see cref="DateTimeUTC"/> property value.
        /// </summary>
        public DateTime DateTimeLocal
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => DateTimeUTC.ToLocalTime();
        }

        public QpcTimeStamp QpcTimeStamp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (ContainsQPC ? new QpcTimeStamp(-FileDeltaTime) : QpcTimeStamp.Zero); }
        }

        public bool ContainsQPC
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (FileDeltaTime < 0.0); }
        }

        public static MDRF2DateTimeStampPair NowUTCTimeSince1601Only
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new MDRF2DateTimeStampPair() { FileDeltaTime = 0.0, UTCTimeSince1601 = DateTime.Now.GetUTCTimeSince1601() }; }
        }

        public static MDRF2DateTimeStampPair NowQPCOnly
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new MDRF2DateTimeStampPair() { FileDeltaTime = -Qpc.TimeNow, UTCTimeSince1601 = 0.0 }; }
        }

        public static MDRF2DateTimeStampPair Now
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new MDRF2DateTimeStampPair() { FileDeltaTime = -Qpc.TimeNow, UTCTimeSince1601 = DateTime.Now.GetUTCTimeSince1601() }; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ConvertQPCToFDT(ref MDRF2DateTimeStampPair fileReferenceQPCDTPair)
        {
            if (ContainsQPC && fileReferenceQPCDTPair.ContainsQPC)
                return fileReferenceQPCDTPair.FileDeltaTime - FileDeltaTime;  // WARNING: this logic is intentionally odd as it is only used if both the curernt value and the reference one contain QPC values and not file delta values

            return FileDeltaTime;
        }
    }

    /// <summary>
    /// This class contains the set of information that is recorded in each MDRF2 file block header's first message pack record.
    /// This record contains information about the rest of the records that are in the block including the total size of the block,
    /// its time range, and flag bits and hashset that summarizes the types of records that have been known to have been recorded.
    /// When reading an MDRF2 file this record is used to determine if the entire contents of the block can be skipped (or not) so as to
    /// help increase the performance of query code when it can concisely determine that a given block does not contain any interenal records
    /// of interest.
    /// </summary>
    public class InlineIndexRecord : ICopyable<InlineIndexRecord>
    {
        public InlineIndexRecord()
        { }

        public InlineIndexRecord(FileIndexRowBase row)
            : this()
        {
            BlockFirstFileDeltaTime = row.FirstBlockDeltaTimeStamp;
            BlockLastFileDeltaTime = row.LastBlockDeltaTimeStamp;
            BlockFirstUDTTimeSince1601 = row.FirstBlockUtcTimeSince1601;
            BlockLastUDTTimeSince1601 = row.FirstBlockUtcTimeSince1601 + (row.LastBlockDeltaTimeStamp - row.FirstBlockDeltaTimeStamp);
            BlockFileIndexRowFlagBits = row.FileIndexRowFlagBits;
            BlockUserRowFlagBits = row.FileIndexUserRowFlagBits;
        }

        public uint BlockByteCount { get; set; }
        public uint BlockRecordCount { get; set; }
        public double BlockFirstFileDeltaTime { get; set; }
        public double BlockLastFileDeltaTime { get; set; }
        public double BlockFirstUDTTimeSince1601 { get; set; }
        public double BlockLastUDTTimeSince1601 { get; set; }
        public FileIndexRowFlagBits BlockFileIndexRowFlagBits { get; set; }
        public ulong BlockUserRowFlagBits { get; set; }
        public uint BlockGroupSetCount { get; set; }
        public uint BlockOccurrenceCount { get; set; }
        public uint BlockObjectCount { get; set; }

        public System.Collections.Generic.HashSet<int> BlockKeyIDHashSet { get; private set; } = new System.Collections.Generic.HashSet<int>();

        public void NoteKeyRecorded(KeyInfo keyInfo)
        {
            BlockKeyIDHashSet.Add(keyInfo.KeyID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            BlockByteCount = 0;
            BlockRecordCount = 0;
            BlockFirstFileDeltaTime = 0.0;
            BlockLastFileDeltaTime = 0.0;
            BlockFirstUDTTimeSince1601 = 0.0;
            BlockLastUDTTimeSince1601 = 0.0;
            BlockFileIndexRowFlagBits = default;
            BlockUserRowFlagBits = 0;
            BlockGroupSetCount = 0;
            BlockOccurrenceCount = 0;
            BlockObjectCount = 0;
            BlockKeyIDHashSet.Clear();
        }

        public MDRF2DateTimeStampPair FirstDTPair
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new MDRF2DateTimeStampPair() { FileDeltaTime = BlockFirstFileDeltaTime, UTCTimeSince1601 = BlockFirstUDTTimeSince1601 }; }
        }

        public MDRF2DateTimeStampPair LastDTPair
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new MDRF2DateTimeStampPair() { FileDeltaTime = BlockLastFileDeltaTime, UTCTimeSince1601 = BlockLastUDTTimeSince1601 }; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InlineIndexRecord SetupForReaderUse()
        {
            blockFDTTimeSpan = (BlockLastFileDeltaTime - BlockFirstFileDeltaTime);
            blockUDT1601TimeSpan = (BlockLastUDTTimeSince1601 - BlockFirstUDTTimeSince1601);
            oneOverBlockFDTTimeSpan = blockUDT1601TimeSpan.SafeOneOver();

            return this;
        }

        private double blockFDTTimeSpan;
        private double oneOverBlockFDTTimeSpan;
        private double blockUDT1601TimeSpan;

        /// <summary>
        /// Updates the given <paramref name="dtPair"/> to contain the given <paramref name="fileDeltaTime"/> and to contain an UTCTimeSince1601 that is calculated
        /// based on the corresponding time range covered by this inline index record.  When <paramref name="useInterpolation"/> is selected,
        /// the calculated UTCTimeSince1601 will be interpolated between the starting and ending 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateDTPair(ref MDRF2DateTimeStampPair dtPair, double fileDeltaTime, bool useInterpolation = true)
        {
            dtPair.FileDeltaTime = fileDeltaTime;

            var fdtOffsetFromStartOfBlock = fileDeltaTime - BlockFirstFileDeltaTime;
            if (fdtOffsetFromStartOfBlock <= 0.0)
            {
                // we do not interpolate backward for fdt values that come before the first UDT1601 value in this block.
                dtPair.UTCTimeSince1601 = BlockFirstUDTTimeSince1601;
            }
            else if (!useInterpolation)
            {
                dtPair.UTCTimeSince1601 = BlockFirstUDTTimeSince1601 + fdtOffsetFromStartOfBlock;
            }
            else if (fileDeltaTime < BlockLastFileDeltaTime)
            {
                var unitSpan = fdtOffsetFromStartOfBlock * oneOverBlockFDTTimeSpan; // value goes from 0 to 1 for range of fdt and udt times in this block.
                dtPair.UTCTimeSince1601 = BlockFirstUDTTimeSince1601 + unitSpan * blockUDT1601TimeSpan;
            }
            else
            {
                dtPair.UTCTimeSince1601 = BlockLastUDTTimeSince1601 + (fileDeltaTime - BlockLastFileDeltaTime);
            }
        }

        public override string ToString()
        {
            return $"bytes:{BlockByteCount} records:{BlockRecordCount} flags:{BlockFileIndexRowFlagBits},${BlockUserRowFlagBits:x8} times:[{BlockFirstFileDeltaTime:f6}/{BlockFirstUDTTimeSince1601:f6} delta:{BlockLastFileDeltaTime - BlockFirstFileDeltaTime:f6}/{BlockLastUDTTimeSince1601 - BlockFirstUDTTimeSince1601:f6}]";
        }

        public InlineIndexRecord MakeCopyOfThis(bool deepCopy = true)
        {
            var copy = (InlineIndexRecord)MemberwiseClone();

            copy.BlockKeyIDHashSet = new System.Collections.Generic.HashSet<int>(BlockKeyIDHashSet);

            return copy;
        }
    }

    /// <summary>
    /// This class is used internally to contain the information that is known about each key that has been registered and/or used.
    /// </summary>
    public class KeyInfo
    {
        public string KeyName { get; internal set; }

        public int KeyHashCode { get; internal set; }
        public int KeyID { get; internal set; }
        public ulong KeyUserRowFlagBits { get; internal set; }
    };

    #endregion

    #region MDRF2 specific MP formatters (InlineIndexFormatter, MesgAndErrorBodyFormatter)

    /// <summary>
    /// This class is responsible for Formatting and Deformatting InlineIndexRecord instances to/from MesgPack notation.
    /// </summary>
    public class InlineIndexFormatter : IMessagePackFormatter<InlineIndexRecord>, IMessagePackFormatter
    {
        public static readonly InlineIndexFormatter Instance = new InlineIndexFormatter();

        /// <summary>
        /// inline index record [L1 [L11 lenU4 numRU4 sFDTF8 eFDTF8 sUTC1610F8 eUTC1601F8 rowFlagBitsU2 userRowFlagBitsU8 groupSetCountU4 occurrenceCountU4 objectCountU4]]
        /// len gives the total number of bytes in the block of MP records that follow this record.
        /// numR gives the number of records in the block of MP records that follow this record.
        /// sFDT and eFDT gives the range of file delta timestamp values for these records
        /// sUTC1601 and eUTC1601 give the corresponding range of FileUTC1601 values for these records.  These can be combined with FDT to infer the FileUTC1601 for any corresponding FDT value in this range.
        /// </summary>
        public void Serialize(ref MessagePackWriter mpWriter, InlineIndexRecord record, MessagePackSerializerOptions options)
        {
            bool haveKeyIDSet = record.BlockKeyIDHashSet.Count > 0;

            mpWriter.WriteArrayHeader(1);
            mpWriter.WriteArrayHeader(11 + haveKeyIDSet.MapToInt());

            mpWriter.Write(record.BlockByteCount);
            mpWriter.Write(record.BlockRecordCount);
            mpWriter.Write(record.BlockFirstFileDeltaTime);
            mpWriter.Write(record.BlockLastFileDeltaTime);
            mpWriter.Write(record.BlockFirstUDTTimeSince1601);
            mpWriter.Write(record.BlockLastUDTTimeSince1601);
            mpWriter.Write((ushort)record.BlockFileIndexRowFlagBits);
            mpWriter.Write(record.BlockUserRowFlagBits);
            mpWriter.Write(record.BlockGroupSetCount);
            mpWriter.Write(record.BlockOccurrenceCount);
            mpWriter.Write(record.BlockObjectCount);

            if (haveKeyIDSet)
            {
                mpWriter.WriteArrayHeader(record.BlockKeyIDHashSet.Count);
                foreach (var keyID in record.BlockKeyIDHashSet)
                    mpWriter.Write(keyID);
            }
        }

        public InlineIndexRecord Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            var headerByteCode1 = (MessagePackUtils.MPHeaderByteCode) mpReader.NextCode;

            if (headerByteCode1 == MessagePackUtils.MPHeaderByteCode.FixArray1)
                mpReader.ReadArrayHeader();
            else
                new MDRF2ReaderException($"Encountered unexpected {headerByteCode1} record while attempting to Deserialize an InlineIndex [expected FixArray1 at:{mpReader.Position}]");

            var headerByteCode2 = (MessagePackUtils.MPHeaderByteCode)mpReader.NextCode;
            int arrayLength;
            switch (headerByteCode2)
            {
                case MPHeaderByteCode.FixArray11:
                case MPHeaderByteCode.FixArray12:
                    arrayLength = mpReader.ReadArrayHeader();
                    break;
                default:
                    arrayLength = 0;
                    new MDRF2ReaderException($"Encountered unexpected {headerByteCode2} record while attempting to Deserialize an InlineIndex [expected FixArray11 at:{mpReader.Position}]");
                    break;
            }

            InlineIndexRecord record = new InlineIndexRecord
            {
                BlockByteCount = mpReader.ReadUInt32(),
                BlockRecordCount = mpReader.ReadUInt32(),
                BlockFirstFileDeltaTime = mpReader.ReadDouble(),
                BlockLastFileDeltaTime = mpReader.ReadDouble(),
                BlockFirstUDTTimeSince1601 = mpReader.ReadDouble(),
                BlockLastUDTTimeSince1601 = mpReader.ReadDouble(),
                BlockFileIndexRowFlagBits = unchecked((FileIndexRowFlagBits)mpReader.ReadUInt16()),
                BlockUserRowFlagBits = mpReader.ReadUInt64(),
                BlockGroupSetCount = mpReader.ReadUInt32(),
                BlockOccurrenceCount = mpReader.ReadUInt32(),
                BlockObjectCount = mpReader.ReadUInt32()
            };

            if (arrayLength >= 12)
            {
                var keyIDArrayLength = mpReader.ReadArrayHeader();

                var hashSet = record.BlockKeyIDHashSet;

                for (int index = 0; index < keyIDArrayLength; index++)
                    hashSet.Add(mpReader.ReadInt32());
            }

            return record;
        }
    }

    /// <summary>
    /// This class is used as a serialization/deserialization formatter for Mesg and Error records
    /// </summary>
    internal class MesgAndErrorBodyFormatter : IMessagePackFormatter<(string mesg, MDRF2DateTimeStampPair dtPair)>, IMessagePackFormatter
    {
        public static readonly MesgAndErrorBodyFormatter Instance = new MesgAndErrorBodyFormatter();

        public void Serialize(ref MessagePackWriter mpWriter, (string mesg, MDRF2DateTimeStampPair dtPair) mesgTuple, MessagePackSerializerOptions options)
        {
            var mesg = mesgTuple.mesg;
            var dtPair = mesgTuple.dtPair;

            mpWriter.WriteArrayHeader(3);

            mpWriter.Write(dtPair.FileDeltaTime);       // NOTE: When this value is negative it givs the QPC timestamp at the point the message was queued.
            mpWriter.Write(dtPair.UTCTimeSince1601);
            mpWriter.Write(mesg);
        }

        public (string mesg, MDRF2DateTimeStampPair dtPair) Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            var arrayLen = mpReader.ReadArrayHeader();

            if (arrayLen == 3)
            {
                var emittedFileDeltaTime = mpReader.ReadDouble();
                var emittedUTCTimeSince1601 = mpReader.ReadDouble();
                var mesg = mpReader.ReadString();

                return (mesg, new MDRF2DateTimeStampPair() { FileDeltaTime = emittedFileDeltaTime, UTCTimeSince1601 = emittedUTCTimeSince1601 });
            }
            else
            {
                foreach (var _ in Enumerable.Range(0, arrayLen))
                    mpReader.Skip();

                return ($"Invalid serialized Mesg or Error body array length [{arrayLen} != 3]", MDRF2DateTimeStampPair.NowUTCTimeSince1601Only);
            }
        }
    }

    #endregion

    #region MDRF2 type name handler related classes

    /// <summary>
    /// This interface defines the API that is required to be supported by objects that support being used to deserialize specific object types
    /// and to generate corresponding <see cref="IMDRF2QueryRecord"/> instances to be reported.  
    /// </summary>
    /// <remarks>
    /// The use of an interface in place of the delegate that is used for similar purposes in the <see cref="Writer.IMDRF2Writer"/> is driven
    /// by the need to support optional retention and re-use of previously emitted type specfic record instances which cannot easily be done 
    /// using a static method delegate.
    /// </remarks>
    public interface IMDRF2TypeNameHandler
    {
        /// <summary>
        /// This method is used to record the given <paramref name="value"/> object to the given <paramref name="mpWriter"/> using the given <paramref name="mpOptions"/>.
        /// </summary>
        void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions);

        /// <summary>
        /// When called this method generates a new <see cref="MDRF2QueryRecord{TItemType}"/> of the desired type, updates most of its contents 
        /// from the <paramref name="refQueryRecord"/>, deserializes the known object from the object content specific portion of the object MP record
        /// using the given <paramref name="mpReader"/>,  assigns the type specific record to contain the deserialzed object and returns it.  
        /// If the <paramref name="allowRecordReuse"/> parameter is passed as true and this instance supports it then the method may choose to
        /// retain, update and return the same type specific record instance over and over.  This pattern is only used when the query client has
        /// specifically requested this and it will copy out the relevant information from the record before returning to its query enumerator to 
        /// obtain the next record.
        /// <para/>Returning null indicates that the method does not want to yield a query record for the corresponding object record and indicates that
        /// type handler has decided to skip this object record and that it should not be counted as processed.
        /// </summary>
        /// <remarks>
        /// Once the <see cref="MDRF2FileReadingHelper"/> has determined that a given object record in the file contains a serialized object that
        /// will be emitted, it updates the contents of type generic <paramref name="refQueryRecord"/> instance, and then calls this method to deserialize
        /// the object type specific part of the MP record and then it yields the record from there.  
        /// </remarks>
        IMDRF2QueryRecord DeserializeAndGenerateTypeSpecificRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IMDRF2QueryRecord refQueryRecord, bool allowRecordReuse);
    }

    /// <summary>
    /// This interface is supported by types that know how to serialize/deserialize themselves using message pack.
    /// </summary>
    public interface IMDRF2MessagePackSerializable
    {
        /// <summary>
        /// This method is used to record this object to the given <paramref name="mpWriter"/> using the given <paramref name="mpOptions"/>.
        /// </summary>
        void Serialize(ref MessagePackWriter mpWriter, MessagePackSerializerOptions mpOptions);

        /// <summary>
        /// This method is used to deserialize and populate this object's contents from the given <paramref name="mpReader"/> using the given <paramref name="mpOptions"/>
        /// </summary>
        void Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions);
    }

    /// <summary>
    /// This interface is optionally supported and used to cross connect an object with the keyID and keyNames that it is recorded with.
    /// </summary>
    public interface IMDRF2RecoringKeyInfo
    {
        /// <summary>
        /// Gives the recording key ID and name to use when recording this report.
        /// When used with specific type name handler types 
        /// (<see cref="TypeNameHandlers.MDRF2MessagePackSerializableTypeNameHandler{TItemType}"/> for example)
        /// The setter will be populated with the keyID and keyName from the reference record during deserialization and record generation.
        /// </summary>
        (int id, string name) RecordingKeyInfo { get; set; }
    }

    /// <summary>
    /// This static "namespace" class contains the definitions for all of the internally provided and used type name handlers that support
    /// well known object type names.  The use of externally visible classes is intended to facilitate improved ease of custom query client 
    /// configuration and use of the related logic and beaviours.
    /// </summary>
    public static class TypeNameHandlers
    {
        /// <summary>Type name handler for <see cref="Logging.ILogMessage"/> instances</summary>
        public class ILogMessageTypeNameHandler : TypeNameHandlerBase<Logging.ILogMessage>
        {
            public ILogMessageTypeNameHandler() : base(new LogMessageFormatter()) { }
        }

        /// <summary>Type name handler for <see cref="MosaicLib.Semi.E039.IE039Object"/> instances</summary>
        public class IE039ObjectTypeNameHandler : TypeNameHandlerBase<MosaicLib.Semi.E039.IE039Object>
        {
            /// <summary>
            /// This gives the default hash set of type names that is used to determine which type names we are enabling serialization of links from other objects for.
            /// When null it selects that all types shall enable this feature by default.  When explicitly empty it selects that no types shall enable this feature by default.
            /// <para/>This proprty is only used when using the <see cref="IE039ObjectTypeNameHandler.IE039ObjectTypeNameHandler"/> default constructor.
            /// </summary>
            public static ReadOnlyHashSet<string> DefaultEnableSerializationOfLinksFromOtherObjectsTypeNameHashSet { get; set; } = ReadOnlyHashSet<string>.Empty;

            /// <summary>
            /// Resets the <see cref="DefaultEnableSerializationOfLinksFromOtherObjectsTypeNameHashSet"/> back to its default value.
            /// <para/>primarily used in unit test code.
            /// </summary>
            public static void ResetDefaultEnableSerializationOfLinksFromOtherObjectsTypeNameHashSet()
            {
                DefaultEnableSerializationOfLinksFromOtherObjectsTypeNameHashSet = ReadOnlyHashSet<string>.Empty;
            }

            /// <summary>
            /// Default constructor.  
            /// Creates (and uses) a new <see cref="E039ObjectFormatter"/> using the current <see cref="DefaultEnableSerializationOfLinksFromOtherObjectsTypeNameHashSet"/>
            /// for its <see cref="E039ObjectFormatter.EnableSerializationOfLinksFromOtherObjectsTypeNameHashSet"/>.
            /// </summary>
            public IE039ObjectTypeNameHandler()
                : base(new E039ObjectFormatter() { EnableSerializationOfLinksFromOtherObjectsTypeNameHashSet = DefaultEnableSerializationOfLinksFromOtherObjectsTypeNameHashSet.ConvertToHashSet(mapNullToEmpty: false)})
            { }

            /// <summary>
            /// Explicit set constructor.
            /// Creates (and uses) a new <see cref="E039ObjectFormatter"/> using the given <paramref name="enableSerializationOfLinksFromOtherObjectsTypeNameSet"/> to define the contents, and nulledness, of the set 
            /// for its <see cref="E039ObjectFormatter.EnableSerializationOfLinksFromOtherObjectsTypeNameHashSet"/>.
            /// </summary>
            public IE039ObjectTypeNameHandler(IEnumerable<string> enableSerializationOfLinksFromOtherObjectsTypeNameSet) 
                : base(new E039ObjectFormatter() { EnableSerializationOfLinksFromOtherObjectsTypeNameHashSet = enableSerializationOfLinksFromOtherObjectsTypeNameSet.ConvertToHashSet(mapNullToEmpty: false) }) 
            { }
        }

        /// <summary>Type name handler for <see cref="ValueContainer"/> instances</summary>
        public class ValueContainerTypeNameHandler : TypeNameHandlerBase<ValueContainer>
        {
            public ValueContainerTypeNameHandler() : base(new VCFormatter()) { }
        }

        /// <summary>Type name handler for <see cref="INamedValueSet"/> instances</summary>
        public class INamedValueSetTypeNameHandler : TypeNameHandlerBase<INamedValueSet>
        {
            public INamedValueSetTypeNameHandler() : base(new NVSFormatter()) { }
        }

        /// <summary>Type name handler for KVCSet (aka <see cref="ICollection{KeyValuePair{string,ValueContainer}}"/>) instances</summary>
        public class KVCSetTypeNameHandler : TypeNameHandlerBase<ICollection<KeyValuePair<string, ValueContainer>>>
        {
            public KVCSetTypeNameHandler() : base(new KVCSetFormatter()) { }
        }

        /// <summary>
        /// Type name handler used with <see cref="MosaicLib.Modular.Part.IBaseState"/> objects.
        /// </summary>
        public class IBaseStateTypeNameHandler : TypeNameHandlerQueryRecordReuseHelperBase<MosaicLib.Modular.Part.IBaseState>, IMDRF2TypeNameHandler
        {
            /// <summary>Gives the MDRF2 type name that is generally used with this object type</summary>
            public const string MDRF2TypeName = "Part.BaseState";

            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions)
            {
                var baseState = (MosaicLib.Modular.Part.IBaseState)value;

                if (baseState == null)
                {
                    mpWriter.WriteNil();
                    return;
                }

                mpWriter.WriteArrayHeader(8);
                mpWriter.Write(baseState.PartID);
                mpWriter.Write(baseState.IsSimulated);
                mpWriter.Write(baseState.IsPrimaryPart);
                mpWriter.Write((int)baseState.UseState);
                mpWriter.Write((int)baseState.ConnState);
                mpWriter.Write(baseState.ActionName);
                mpWriter.Write(baseState.Reason);
                mpWriter.Write(baseState.ExplicitFaultReason);
            }

            /// <inheritdoc/>
            public IMDRF2QueryRecord DeserializeAndGenerateTypeSpecificRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IMDRF2QueryRecord refQueryRecord, bool allowRecordReuse)
            {
                var record = GetOrCreateAndUpdateQueryRecord(refQueryRecord, allowRecordReuse);

                if (mpReader.TryReadNil())
                {
                    record.Data = null;
                    return record;
                }

                var arrayLen = mpReader.ReadArrayHeader();

                if (arrayLen == 8)
                {
                    var partID = mpReader.ReadString();
                    var isSimulated = mpReader.ReadBoolean();
                    var isPrimaryPart = mpReader.ReadBoolean();

                    var baseState = new MosaicLib.Modular.Part.BaseState(isSimulated: isSimulated, isPrimaryPart: isPrimaryPart)
                    {
                        PartID = partID,
                        UseState = unchecked((MosaicLib.Modular.Part.UseState) mpReader.ReadInt32()),
                        ConnState = unchecked((MosaicLib.Modular.Part.ConnState)mpReader.ReadInt32()),
                        ActionName = mpReader.ReadString(),
                        Reason = mpReader.ReadString(),
                        ExplicitFaultReason = mpReader.ReadString(),
                    };

                    record.Data = baseState;
                }
                else
                {
                    new System.ArgumentOutOfRangeException($"{Fcns.CurrentClassLeafName} Deserialize failed: contents are not valid [arrayLen was not 8, was {arrayLen}]").Throw();
                }

                return record;
            }
        }

        /// <summary>
        /// Type name handler used with <see cref="MosaicLib.Semi.E005.IMessage"/> objects.
        /// </summary>
        public class E005MessageTypeNameHandler : TypeNameHandlerQueryRecordReuseHelperBase<MosaicLib.Semi.E005.IMessage>, IMDRF2TypeNameHandler
        {
            /// <summary>Gives the MDRF2 type name that is generally used with this object type</summary>
            public const string MDRF2TypeName = "E005.Message";

            private readonly E005TenByteHeaderTypeNameHandler tbhSerializer = new E005TenByteHeaderTypeNameHandler();

            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions)
            {
                var mesg = (MosaicLib.Semi.E005.IMessage)value;

                if (mesg == null)
                {
                    mpWriter.WriteNil();
                    return;
                }

                mpWriter.WriteArrayHeader(2);

                if (mesg.TenByteHeader != null)
                    tbhSerializer.Serialize(ref mpWriter, mesg.TenByteHeader, mpOptions);
                else
                    mpWriter.Write(mesg.SF.B2B3);

                mpWriter.Write(mesg.ContentBytes);
            }

            /// <inheritdoc/>
            public IMDRF2QueryRecord DeserializeAndGenerateTypeSpecificRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IMDRF2QueryRecord refQueryRecord, bool allowRecordReuse)
            {
                var record = GetOrCreateAndUpdateQueryRecord(refQueryRecord, allowRecordReuse);

                if (mpReader.TryReadNil())
                {
                    record.Data = null;
                    return record;
                }

                var arrayLen = mpReader.ReadArrayHeader();

                if (arrayLen == 2)
                {
                    MosaicLib.Semi.E005.StreamFunction sf = default;
                    MosaicLib.Semi.E005.ITenByteHeader tbh = default;

                    if (mpReader.NextMessagePackType == MessagePackType.Integer)
                        sf.B2B3 = mpReader.ReadUInt16();
                    else                        
                    {
                        tbh = tbhSerializer.Deserialize(ref mpReader, mpOptions);
                        sf = tbh.SF;
                    }

                    var mesg = new MosaicLib.Semi.E005.Message(sf: sf);

                    if (tbh != null)
                        mesg.SetTenByteHeader(tbh, keepMessageSF: false, keepMessageSeqNum: false);

                    mesg.SetContentBytes(mpReader.ReadBytes()?.First.ToArray());

                    record.Data = mesg;
                }
                else
                {
                    new System.ArgumentOutOfRangeException($"{Fcns.CurrentClassLeafName} Deserialize failed: contents are not valid [arrayLen was not 2, was {arrayLen}]").Throw();
                }

                return record;
            }
        }

        /// <summary>
        /// Type name handler used with <see cref="MosaicLib.Semi.E005.ITenByteHeader"/> objects.
        /// </summary>
        public class E005TenByteHeaderTypeNameHandler  : TypeNameHandlerQueryRecordReuseHelperBase<MosaicLib.Semi.E005.ITenByteHeader>, IMDRF2TypeNameHandler
        {
            /// <summary>Gives the MDRF2 type name that is generally used with this object type</summary>
            public const string MDRF2TypeName = "E005.TenByteHeader";

            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions)
            {
                if (value == null)
                {
                    mpWriter.WriteNil();
                }
                if (value is MosaicLib.Semi.E037.IE037TenByteHeader e037tbh)
                {
                    mpWriter.Write(e037tbh.ByteArray);
                }
                else
                {
                    var tbh = (MosaicLib.Semi.E005.ITenByteHeader)value;

                    mpWriter.WriteArrayHeader(1);

                    mpWriter.Write(tbh.ByteArray);
                }
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Placeholder for consistent calling pattern")]
            public MosaicLib.Semi.E005.ITenByteHeader Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions)
            {
                if (mpReader.TryReadNil())
                    return null;

                if (mpReader.NextMessagePackType == MessagePackType.Binary)
                {
                    return new MosaicLib.Semi.E037.E037TenByteHeader() { ByteArray = mpReader.ReadBytes()?.First.ToArray() };
                }
                else
                {
                    var arrayLen = mpReader.ReadArrayHeader();

                    if (arrayLen == 1)
                    {
                        return new MosaicLib.Semi.E005.TenByteHeaderBase() { ByteArray = mpReader.ReadBytes()?.First.ToArray() };
                    }
                    else
                    {
                        throw new System.ArgumentOutOfRangeException($"{Fcns.CurrentClassLeafName} Deserialize failed: contents are not valid [arrayLen was not 1, was {arrayLen}]");
                    }
                }
            }

            /// <inheritdoc/>
            public IMDRF2QueryRecord DeserializeAndGenerateTypeSpecificRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IMDRF2QueryRecord refQueryRecord, bool allowRecordReuse)
            {
                var record = GetOrCreateAndUpdateQueryRecord(refQueryRecord, allowRecordReuse);

                record.Data = Deserialize(ref mpReader, mpOptions);

                return record;
            }
        }

        /// <summary>Type name handler for <see cref="System.Dynamic.DynamicObject"/> instances</summary>
        /// <remarks>
        /// On serialization this type name handler can directly serialize any <see cref="System.Dynamic.ExpandoObject"/> instance by casting it to an <see cref="IDictionary{string, value}"/> and iterating on that as a set of key value pairs.
        /// For all other (dyanmic) value types this handler uses reflection to obtain the set of, and values of, each property from which it builds the set of key value pairs to be serialized.
        /// Deserialization always generates records that contain <see cref="System.Dynamic.ExpandoObject"/> instances.
        /// </remarks>
        public class DynamicObjectTypeNameHandler : TypeNameHandlerQueryRecordReuseHelperBase<dynamic>, IMDRF2TypeNameHandler
        {
            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions)
            {
                KeyValuePair<string, ValueContainer> [] kvcSet;

                switch (value)
                {
                    case null:
                        kvcSet = null;
                        break;
                    case System.Dynamic.ExpandoObject expandoObject:
                        kvcSet = expandoObject
                                    .Select(kvp => KVP.Create(kvp.Key, ValueContainer.CreateFromObject(kvp.Value)))
                                    .ToArray();
                        break;
                    default:
                        kvcSet = value.GetType()
                                    .GetProperties()
                                    .Select(prop => KVP.Create(prop.Name, ValueContainer.CreateFromObject(prop.GetValue(value))))
                                    .ToArray();
                        break;
                }

                KVCSetFormatter.Instance.Serialize(ref mpWriter, kvcSet, mpOptions);
            }

            /// <inheritdoc/>
            public IMDRF2QueryRecord DeserializeAndGenerateTypeSpecificRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IMDRF2QueryRecord refQueryRecord, bool allowRecordReuse)
            {
                var record = GetOrCreateAndUpdateQueryRecord(refQueryRecord, allowRecordReuse);

                var kvcSet = KVCSetFormatter.Instance.Deserialize(ref mpReader, mpOptions);

                if (kvcSet != null)
                {
                    var eo = new System.Dynamic.ExpandoObject();
                    var eoDictionary = (IDictionary<string, object>)eo;

                    foreach (var kvc in kvcSet)
                        eoDictionary[kvc.Key] = kvc.Value.ValueAsObject;

                    record.Data = eo;
                }
                else
                {
                    record.Data = null;
                }

                return record;
            }
        }

        /// <summary>
        /// This type name handler deserializes NVS records from an mpReader and generates ValueContainer records from them.
        /// </summary>
        public class INamedValueSetAsValueContainerTypeNameHandler : TypeNameHandlerQueryRecordReuseHelperBase<ValueContainer>, IMDRF2TypeNameHandler
        {
            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions)
            {
                var nvs = ((ValueContainer) value).GetValueNVS(rethrow: true);

                NVSFormatter.Instance.Serialize(ref mpWriter, nvs, mpOptions);
            }

            /// <inheritdoc/>
            public IMDRF2QueryRecord DeserializeAndGenerateTypeSpecificRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IMDRF2QueryRecord refQueryRecord, bool allowRecordReuse)
            {
                var record = GetOrCreateAndUpdateQueryRecord(refQueryRecord, allowRecordReuse);

                record.Data = ValueContainer.CreateNVS(NVSFormatter.Instance.Deserialize(ref mpReader, mpOptions));

                return record;
            }
        }

        /// <summary>
        /// This type name handler supports use of the new <see cref="DataContractJsonSerializationFormatter{TItemType}"/> formatter
        /// for serialization and deserialization with record generetion for all <typeparamref name="TItemType"/> types that support
        /// data contract serialization.  The resulting message pack content is more efficient than when using tavc based serialization
        /// as all of the type name related aspects of tavc serialization can be represented as a single TypeID value in the as written format.
        /// </summary>
        public class DataContractJsonSerializationTypeNameHandler<TItemType> : TypeNameHandlerBase<TItemType>, IMDRF2TypeNameHandler
        {
            public DataContractJsonSerializationTypeNameHandler() : base(new DataContractJsonSerializationFormatter<TItemType>()) { }
        }

        /// <summary>
        /// This type name handler supports serialization and deserialization with record generation for all types that support a default constructor and support the <see cref="IMDRF2MessagePackSerializable"/> interface.
        /// </summary>
        public class MDRF2MessagePackSerializableTypeNameHandler<TItemType> 
            : TypeNameHandlerQueryRecordReuseHelperBase<TItemType>, IMDRF2TypeNameHandler
            where TItemType : IMDRF2MessagePackSerializable, new()
        {
            /// <inheritdoc/>
            public virtual void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions)
            {
                var item = (IMDRF2MessagePackSerializable)value;

                item.Serialize(ref mpWriter, mpOptions);
            }

            /// <inheritdoc/>
            public virtual IMDRF2QueryRecord DeserializeAndGenerateTypeSpecificRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IMDRF2QueryRecord refQueryRecord, bool allowRecordReuse)
            {
                var item = FactoryMethod();
                item.Deserialize(ref mpReader, mpOptions);

                var record = GetOrCreateAndUpdateQueryRecord(refQueryRecord, allowRecordReuse);
                record.Data = item;

                if (item is IMDRF2RecoringKeyInfo irki)
                    irki.RecordingKeyInfo = (record.KeyID, record.KeyName);

                return record;
            }

            protected Func<TItemType> FactoryMethod { get; set; } = () => new TItemType();
        }

        /// <summary>
        /// Generic base class for commonly used {TItemType} specific cases where a corresponding <see cref="MessagePack.Formatters.IMessagePackFormatter{TItemType}"/> instace is already available.
        /// </summary>

        public class TypeNameHandlerBase<TItemType> : TypeNameHandlerQueryRecordReuseHelperBase<TItemType>, IMDRF2TypeNameHandler
        {
            public TypeNameHandlerBase(MessagePack.Formatters.IMessagePackFormatter<TItemType> mpFormater)
            {
                MPFormatter = mpFormater;
            }

            public MessagePack.Formatters.IMessagePackFormatter<TItemType> MPFormatter { get; protected set; }

            /// <inheritdoc/>
            public virtual void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions)
            {
                var item = (TItemType)value;

                MPFormatter.Serialize(ref mpWriter, item, mpOptions);
            }

            /// <inheritdoc/>
            public virtual IMDRF2QueryRecord DeserializeAndGenerateTypeSpecificRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IMDRF2QueryRecord refRecord, bool allowRecordReuse)
            {
                var record = GetOrCreateAndUpdateQueryRecord(refRecord, allowRecordReuse);

                record.Data = MPFormatter.Deserialize(ref mpReader, mpOptions);

                return record;
            }
        }

        /// <summary>
        /// This base class encapsulates most of the logic that is used to support the allow query record reuse concept for 
        /// TypeName handlers.  It combines the logic that is used to retain and update a single query record instance with logic used to 
        /// construct (and update) new query record instances.
        /// </summary>
        public class TypeNameHandlerQueryRecordReuseHelperBase<TRecordDataType>
        {
            private MDRF2QueryRecord<TRecordDataType> queeryRecordForReuse;

            protected MDRF2QueryRecord<TRecordDataType> GetOrCreateAndUpdateQueryRecord(IMDRF2QueryRecord refQueryRecord, bool allowRecordReuse)
            {
                var record = ((allowRecordReuse ? queeryRecordForReuse : null) ?? new MDRF2QueryRecord<TRecordDataType>())
                    .UpdateFrom(refQueryRecord);

                if (allowRecordReuse && queeryRecordForReuse == null)
                    queeryRecordForReuse = record;

                return record;
            }
        }
    }

    #endregion
}
