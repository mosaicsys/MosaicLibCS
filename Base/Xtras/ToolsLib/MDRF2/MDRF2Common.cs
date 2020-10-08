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
using System.Linq;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;
using Mosaic.ToolsLib.MessagePackUtils;
using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace Mosaic.ToolsLib.MDRF2.Common
{
    #region Constants (especially LibInfo and related version information)

    public static partial class Constants
    {
        /// <summary>
        /// <para/>Type = "Mosaic Data Recording File version 2 (MDRF2)",
        /// <para/>Name = "Mosaic Data Recording Engine version 2 (MDRE2) CS API",
        /// <para/>Version = {see remarks below},
        /// <para/>This information is included in the Session record which is found in the second block of each MDRF2 file.
        /// </summary>
        /// <remarks>
        /// Version history:
        /// 2.0.0 (2020-06-03) : First MDRF2 version.
        /// </remarks>
        public static readonly ILibraryInfo Lib2Info = new LibraryInfo()
        {
            Type = "Mosaic Data Recording File version 2 (MDRF2)",
            Name = "Mosaic Data Recording Engine version 2 (MDRE2) CS API",
            Version = "2.0.0 (2020-06-03)",
        };

        /// <summary>Keyword used in InlineMAP to indicate the first record in the file.  This InlineMAP must also contain a DateTime item.</summary>
        public const string FileStartKeyword = "MDRF2.Start";

        /// <summary>InlineMAP key name used to contain the DateTimeNVS</summary>
        public const string DateTimeNVSKeyName = "DateTime";

        /// <summary>InlineMAP key name used to contain the Instance UUID.  This is an identifier that is uniquely assigned and recorded at the start of each file.</summary>
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

        /// <summary>InlineMAP key name used to contain the SpecItems array of NVS</summary>
        public const string SpecItemsNVSListKey = "SpecItems";

        /// <summary>Keyword used in InlineMAP record to indicate the last record in the file.</summary>
        public const string FileEndKeyword = "MDRF2.End";

        /// <summary>Object short/known type for a LogMessage or ILogMessage</summary>
        public const string ObjKnownType_LogMessage = "LogMesg";

        /// <summary>Object short/known type for an E039Object or IE039Object</summary>
        public const string ObjKnownType_E039Object = "E039Obj";

        /// <summary>Object short/known type for a ValueContainer</summary>
        public const string ObjKnownType_ValueContainer = "VC";

        /// <summary>Object short/known type any other type that supports custom serialization</summary>
        public const string ObjKnownType_TypeAndValueCarrier = "tavc";

        /// <summary>InlineMAP key name used to carry Normal Mesg contents</summary>
        public const string ObjKnownType_Mesg = "Mesg";

        /// <summary>InlineMAP key name used to carry Error Mesg contents</summary>
        public const string ObjKnownType_Error = "Error";

    }

    #endregion

    #region MDRF2 specific and MP formatters (MDRF2ReaderFormatException, InlineIndexRecord, InlineIndexFormatter, ExtensionMethods)

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

        public DateTime DateTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return UTCTimeSince1601.GetDateTimeFromUTCTimeSince1601(); }
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

        double blockFDTTimeSpan, oneOverBlockFDTTimeSpan;
        double blockUDT1601TimeSpan;

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
            return (InlineIndexRecord)MemberwiseClone();
        }
    }

    /// <summary>
    /// This class is responsible for Formatting and Deformatting InlineIndexRecord instances to/from MesgPack notation.
    /// </summary>
    public class InlineIndexFormatter : IMessagePackFormatter<InlineIndexRecord>, IMessagePackFormatter
    {
        public static readonly InlineIndexFormatter Instance = new InlineIndexFormatter();

        /// <summary>
        /// inline index record [L1 [L8 lenU4 numRU4 sFDTF8 eFDTF8 sUTC1610F8 eUTC1601F8 rowFlagBitsU2 userRowFlagBitsU8]]
        /// len gives the total number of bytes in the block of MP records that follow this record.
        /// numR gives the number of records in the block of MP records that follow this record.
        /// sFDT and eFDT gives the range of file delta timestamp values for these records
        /// sUTC1601 and eUTC1601 give the corresponding range of FileUTC1601 values for these records.  These can be combined with FDT to infer the FileUTC1601 for any corresponding FDT value in this range.
        /// </summary>
        public void Serialize(ref MessagePackWriter mpWriter, InlineIndexRecord record, MessagePackSerializerOptions options)
        {
            mpWriter.WriteArrayHeader(1);
            mpWriter.WriteArrayHeader(11);
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
        }

        public InlineIndexRecord Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            var headerByteCode1 = (MessagePackUtils.MPHeaderByteCode) mpReader.NextCode;

            if (headerByteCode1 == MessagePackUtils.MPHeaderByteCode.FixArray1)
                mpReader.ReadArrayHeader();
            else
                new MDRF2ReaderException($"Encountered unexpected {headerByteCode1} record while attempting to Deserialize an InlineIndex [expected FixArray1 at:{mpReader.Position}]");

            var headerByteCode2 = (MessagePackUtils.MPHeaderByteCode)mpReader.NextCode;
            if (headerByteCode2 == MessagePackUtils.MPHeaderByteCode.FixArray11)
                mpReader.ReadArrayHeader();
            else
                new MDRF2ReaderException($"Encountered unexpected {headerByteCode2} record while attempting to Deserialize an InlineIndex [expected FixArray8 at:{mpReader.Position}]");

            InlineIndexRecord record = new InlineIndexRecord();
            record.BlockByteCount = mpReader.ReadUInt32();
            record.BlockRecordCount = mpReader.ReadUInt32();
            record.BlockFirstFileDeltaTime = mpReader.ReadDouble();
            record.BlockLastFileDeltaTime = mpReader.ReadDouble();
            record.BlockFirstUDTTimeSince1601 = mpReader.ReadDouble();
            record.BlockLastUDTTimeSince1601 = mpReader.ReadDouble();
            record.BlockFileIndexRowFlagBits = unchecked((FileIndexRowFlagBits)mpReader.ReadUInt16());
            record.BlockUserRowFlagBits = mpReader.ReadUInt64();
            record.BlockGroupSetCount = mpReader.ReadUInt32();
            record.BlockOccurrenceCount = mpReader.ReadUInt32();
            record.BlockObjectCount = mpReader.ReadUInt32();

            return record;
        }
    }

    /// <summary>
    /// This class is used as a serialization/deserialization formatter for Mesg and Error records
    /// </summary>
    public class MesgAndErrorBodyFormatter : IMessagePackFormatter<Tuple<string, MDRF2DateTimeStampPair>>, IMessagePackFormatter
    {
        public static readonly MesgAndErrorBodyFormatter Instance = new MesgAndErrorBodyFormatter();

        public void Serialize(ref MessagePackWriter mpWriter, Tuple<string, MDRF2DateTimeStampPair> mesgTuple, MessagePackSerializerOptions options)
        {
            var mesg = mesgTuple.Item1;
            var dtPair = mesgTuple.Item2;

            mpWriter.WriteArrayHeader(3);
            mpWriter.Write(dtPair.FileDeltaTime);
            mpWriter.Write(dtPair.UTCTimeSince1601);
            mpWriter.Write(mesg);
        }

        public Tuple<string, MDRF2DateTimeStampPair> Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            var arrayLen = mpReader.ReadArrayHeader();

            if (arrayLen == 3)
            {
                var emittedFileDeltaTime = mpReader.ReadDouble();
                var emittedUTCTimeSince1601 = mpReader.ReadDouble();
                var mesg = mpReader.ReadString();

                return Tuple.Create(mesg, new MDRF2DateTimeStampPair() { FileDeltaTime = emittedFileDeltaTime, UTCTimeSince1601 = emittedUTCTimeSince1601 });
            }
            else
            {
                foreach (var idx in Enumerable.Range(0, arrayLen))
                    mpReader.Skip();

                return Tuple.Create($"Invalid serialized Mesg or Error body array length [{arrayLen} != 3]", MDRF2DateTimeStampPair.NowUTCTimeSince1601Only);
            }
        }
    }

    #endregion
}
