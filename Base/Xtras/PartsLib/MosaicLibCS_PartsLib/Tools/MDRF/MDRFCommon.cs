//-------------------------------------------------------------------
/*! @file MDRFCommon.cs
 *  @brief source file for common MDRF (Mosaic Data Recording Format) related definitions.
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2016 Mosaic Systems Inc.  All rights reserved
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

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular;
using MosaicLib.Modular.Part;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Semi.E005.Data;

namespace MosaicLib.PartsLib.Tools.MDRF.Common
{
    #region SetupInfo

    public class SetupInfo
    {
        public NamedValueSet ClientNVS { get; set; }

        public string DirPath { get; set; }
        public string ClientName { get; set; }
        public string FileNamePrefix { get; set; }
        public bool CreateDirectoryIfNeeded { get; set; }
        public Int32 MaxDataBlockSize { get; set; }
        public Int32 NominalMaxFileSize { get; set; }
        public Int32 FileIndexNumRows { get; set; }
        public TimeSpan MaxFileRecordingPeriod { get; set; }
        public TimeSpan MinInterFileCreateHoldoffPeriod { get; set; }
        public TimeSpan MinNominalFileIndexWriteInterval { get; set; }
        public TimeSpan MinNominalWriteAllInterval { get; set; }
        public Int64 I8Offset { get; set; }
        public Int32 I4Offset { get; set; }
        public Int16 I2Offset { get; set; }

        public SetupInfo() { SetFrom(null); }
        public SetupInfo(SetupInfo rhs) { SetFrom(rhs); }

        public SetupInfo SetFrom(SetupInfo rhs)
        {
            if (rhs == null)
            {
                ClientNVS = new NamedValueSet();

                DirPath = string.Empty;
                ClientName = string.Empty;
                FileNamePrefix = string.Empty;
                CreateDirectoryIfNeeded = true;
                MaxDataBlockSize = 262144;
                NominalMaxFileSize = 100 * 1024 * 1024;
                FileIndexNumRows = 1024;
                MaxFileRecordingPeriod = TimeSpan.FromHours(24.0);
                MinInterFileCreateHoldoffPeriod = TimeSpan.FromSeconds(15.0);
                MinNominalFileIndexWriteInterval = TimeSpan.FromSeconds(60.0);
                MinNominalWriteAllInterval = TimeSpan.FromMinutes(15.0);
                I8Offset = 120;
                I4Offset = 120;
                I2Offset = 27;
            }
            else
            {
                ClientNVS = rhs.ClientNVS.ConvertToReadOnly();

                DirPath = rhs.DirPath;
                ClientName = rhs.ClientName.MapNullOrEmptyTo("NoClientNameGiven");
                FileNamePrefix = rhs.FileNamePrefix;
                CreateDirectoryIfNeeded = rhs.CreateDirectoryIfNeeded;
                MaxDataBlockSize = rhs.MaxDataBlockSize;
                NominalMaxFileSize = rhs.NominalMaxFileSize;
                FileIndexNumRows = rhs.FileIndexNumRows;
                MaxFileRecordingPeriod = rhs.MaxFileRecordingPeriod;
                MinInterFileCreateHoldoffPeriod = rhs.MinInterFileCreateHoldoffPeriod;
                MinNominalFileIndexWriteInterval = rhs.MinNominalFileIndexWriteInterval;
                MinNominalWriteAllInterval = rhs.MinNominalWriteAllInterval;
                I8Offset = rhs.I8Offset;
                I4Offset = rhs.I4Offset;
                I2Offset = rhs.I2Offset;
            }

            return this;
        }

        public SetupInfo MapDefaultsTo(SetupInfo rhs)
        {
            if (ClientNVS.IsNullOrEmpty())
                ClientNVS = rhs.ClientNVS.ConvertToReadOnly();

            DirPath = DirPath.MapNullOrEmptyTo(rhs.DirPath);
            ClientName = ClientName.MapNullOrEmptyTo(rhs.ClientName);
            FileNamePrefix = FileNamePrefix.MapNullOrEmptyTo(rhs.FileNamePrefix);

            CreateDirectoryIfNeeded = CreateDirectoryIfNeeded.MapDefaultTo(rhs.CreateDirectoryIfNeeded);
            MaxDataBlockSize = MaxDataBlockSize.MapDefaultTo(rhs.MaxDataBlockSize);
            NominalMaxFileSize = NominalMaxFileSize.MapDefaultTo(rhs.NominalMaxFileSize);
            FileIndexNumRows = FileIndexNumRows.MapDefaultTo(rhs.FileIndexNumRows);
            MaxFileRecordingPeriod = MaxFileRecordingPeriod.MapDefaultTo(rhs.MaxFileRecordingPeriod);
            MinInterFileCreateHoldoffPeriod = MinInterFileCreateHoldoffPeriod.MapDefaultTo(rhs.MinInterFileCreateHoldoffPeriod);
            MinNominalFileIndexWriteInterval = MinNominalFileIndexWriteInterval.MapDefaultTo(rhs.MinNominalFileIndexWriteInterval);
            MinNominalWriteAllInterval = MinNominalWriteAllInterval.MapDefaultTo(rhs.MinNominalWriteAllInterval);
            I8Offset = I8Offset.MapDefaultTo(rhs.I8Offset);
            I4Offset = I4Offset.MapDefaultTo(rhs.I4Offset);
            I2Offset = I2Offset.MapDefaultTo(rhs.I2Offset);

            return this;
        }

        public const Int32 u4OneMillion = 1024 * 1024;

        public SetupInfo ClipValues()
        {
            // clip and round the maxDataBlockSize up to the next multiple of 4096
            MaxDataBlockSize = (MaxDataBlockSize.Clip(65536, u4OneMillion) + 0xfff) & 0x7ffff000;

            NominalMaxFileSize = NominalMaxFileSize.Clip(u4OneMillion, Int32.MaxValue);

            MaxFileRecordingPeriod = MaxFileRecordingPeriod.Clip(TimeSpan.FromMinutes(5.0), TimeSpan.FromDays(7.0));

            FileIndexNumRows = FileIndexNumRows.Clip(256, 65536);

            MinInterFileCreateHoldoffPeriod = MinInterFileCreateHoldoffPeriod.Clip(TimeSpan.FromSeconds(15.0), TimeSpan.FromHours(1.0));

            if (I8Offset == -1)
                I8Offset = 0;
            if (I4Offset == -1)
                I4Offset = 0;
            if (I2Offset == -1)
                I2Offset = 0;

            return this;
        }

        #region U8/I8, U4/I4 and U2/I2 conversions with offset from setup (for use with U8Auto conversion)

        public UInt64 ConvertToU8WithI8Offset(Int64 i8) { return unchecked((UInt64)(i8 + I8Offset)); }
        public UInt32 ConvertToU4WithI4Offset(Int32 i4) { return unchecked((UInt32)(i4 + I4Offset)); }
        public UInt16 ConvertToU2WithI2Offset(Int16 i2) { return unchecked((UInt16)(i2 + I2Offset)); }

        public Int64 ConvertToI8FromU8WithOffset(UInt64 u8) { return unchecked((Int64)(u8) - I8Offset); }
        public Int32 ConvertToI4FromU4WithOffset(UInt32 u4) { return unchecked((Int32)(u4) - I4Offset); }
        public Int16 ConvertToI2FromU2WithOffset(UInt16 u2) { return unchecked((Int16)(u2 - I2Offset)); }

        #endregion
    }

    #endregion

    #region LibraryInfo

    public class LibraryInfo
    {
        public INamedValueSet NVS { get; internal set; }

        public string Type { get; internal set; }
        public string Name { get; internal set; }
        public string Version { get; internal set; }

        public LibraryInfo()
        {
            NVS = NamedValueSet.Empty;
            Type = string.Empty;
            Name = string.Empty;
            Version = string.Empty;
        }
    }

    #endregion

    #region DateTimeInfo

    public class DateTimeInfo
    {
        public INamedValueSet NVS { get; internal set; }

        public double BlockDeltaTimeStamp { get; internal set; }
        public double QPCTime { get; internal set; }
        public double UTCTimeSince1601 { get; internal set; }
        public DateTime UTCDateTime { get { return UTCTimeSince1601.GetDateTimeFromUTCTimeSince1601(); } }
        public int TimeZoneOffset { get; internal set; }
        public bool DSTIsActive { get; internal set; }
        public int DSTBias { get; internal set; }
        public string TZName0 { get; internal set; }
        public string TZName1 { get; internal set; }

        public DateTimeInfo()
        {
            NVS = NamedValueSet.Empty;
            TZName0 = string.Empty;
            TZName1 = string.Empty;
        }
    }

    #endregion

    #region DateTimeStampPair

    public class DateTimeStampPair
    {
        public QpcTimeStamp qpcTimeStamp;
        public DateTime dateTime;
        public double utcTimeSince1601;

        public double fileDeltaTimeStamp;

        /// <summary>
        /// "Default" constructor.  
        /// Optionally sets both qpcTimeStamp and dateTime to thier corresponding versions of Now.
        /// Sets utcTime to the double version of DateTime as a utc Windows FTime.
        /// Does not calculate any of the fileDelta derived values.
        /// </summary>
        public DateTimeStampPair(bool setToNow = true) 
        {
            if (setToNow)
            {
                qpcTimeStamp = QpcTimeStamp.Now;
                dateTime = DateTime.Now;
                utcTimeSince1601 = dateTime.GetUTCTimeSince1601();
            }
        }

        /// <summary>
        /// Standard constructor.  Sets qpcTimeStamp and dateTime to their corresponding versions of Now.
        /// Sets utcTime to the double version of DateTime as a utc Windows FTime.
        /// Then, if the fileRefererenceDTPair and setupInfo are non-null, it sets all of the fileDelta
        /// values from the difference between the recorded time(stamp) values and their corresponding values
        /// from the given fileReferenceDTPair.  I8 versions of these are scaled using the I8 to/from seconds
        /// scaling defined in the setupInfo.
        /// </summary>
        public DateTimeStampPair(DateTimeStampPair fileReferenceDTPair, SetupInfo setupInfo)
            : this(setToNow: true)
        {
            UpdateFileDeltas(fileReferenceDTPair, setupInfo);
        }

        /// <summary>
        /// If the fileRefererenceDTPair and setupInfo are non-null, it sets all of the fileDelta
        /// values from the difference between the recorded time(stamp) values and their corresponding values
        /// from the given fileReferenceDTPair.  I8 versions of these are scaled using the I8 to/from seconds
        /// scaling defined in the setupInfo.
        /// Otherwise does nothing.
        /// </summary>
        public DateTimeStampPair UpdateFileDeltas(DateTimeStampPair fileReferenceDTPair, SetupInfo setupInfo)
        {
            if (fileReferenceDTPair != null && setupInfo != null)
                fileDeltaTimeStamp = (qpcTimeStamp.Time - fileReferenceDTPair.qpcTimeStamp.Time);

            return this;
        }

        public DateTimeStampPair ClearFileDeltas()
        {
            fileDeltaTimeStamp = 0.0;

            return this;
        }
    }

    #endregion

    #region FileIndexInfo, FileIndexRowBase, FileIndexLastBlockInfoBase

    public class FileIndexInfo : TFileIndexInfo<FileIndexRowBase, FileIndexLastBlockInfoBase>
    {
        public FileIndexInfo(SetupInfo setupInfo) : base(setupInfo) { }

        public FileIndexInfo(int numRows = 0) : base(numRows) { }
    }

    public class TFileIndexInfo<TRowType, TLastBlockInfoType> 
        where TRowType: FileIndexRowBase, new()
        where TLastBlockInfoType: FileIndexLastBlockInfoBase, new()
    {
        #region NowRow, RowSizeDivisor, and NominalMaxFileSize

        public Int32 NumRows { get; internal set; }                 // 0..3
        private Int32 rowSizeDivisor;                               // 4..7
        private Int32 nominalMaxFileSize;       // this is not serialized in the FileIndexV1 data block

        internal static int SerializedSize { get { return 8; } }

        internal Int32 NominalMaxFileSize 
        {
            get { return nominalMaxFileSize; }
            set 
            { 
                nominalMaxFileSize = value;
                rowSizeDivisor = Math.Max(1, (NumRows > 0) ? (value / NumRows) : 0); 
            } 
        }

        public Int32 RowSizeDivisor 
        {
            get { return rowSizeDivisor; }
            internal set
            {
                rowSizeDivisor = value;
                NominalMaxFileSize = NumRows * rowSizeDivisor;
            }
        }

        #endregion

        public TLastBlockInfoType LastBlockInfo { get; internal set; }

        public TRowType[] FileIndexRowArray { get; internal set; }

        #region Constructors

        public TFileIndexInfo(SetupInfo setupInfo) 
            : this(setupInfo.FileIndexNumRows) 
        {
            NominalMaxFileSize = setupInfo.NominalMaxFileSize;
        }

        public TFileIndexInfo(int numRows) 
            : this()
        {
            NumRows = Math.Max(1, numRows);
            RowSizeDivisor = 1;

            FileIndexRowArray = Enumerable.Range(0, numRows).Select(rowIdx => new TRowType() { RowIndex = rowIdx }).ToArray();
        }

        protected TFileIndexInfo()
        {
            LastBlockInfo = new TLastBlockInfoType();
            RowSizeDivisor = 1;

            FileIndexRowArray = emptyRowTypeArray;
        }

        #endregion

        #region Searching for a row that contains a given fileDeltaTimeStamp value.

        public bool FindRowForDeltaTimeStamp(double fileDeltaTimeStamp, out int rowIndex)
        {
            rowIndex = 0;

            FileIndexRowBase firstRow = FileIndexRowArray.SafeAccess(0);

            if (firstRow == null || firstRow.IsEmpty || fileDeltaTimeStamp < firstRow.FirstBlockDeltaTimeStamp)
                return false;

            int lowIndex = 0, highIndex = NumRows - 1;

            FileIndexRowBase testRow = null;

            for (; ; )
            {
                int testIndex = ((lowIndex + highIndex) >> 1);

                if ((testRow = FileIndexRowArray.SafeAccess(testIndex)) == null)
                    break;

                if (lowIndex == highIndex)
                    break;
                else if (testRow.IsEmpty || fileDeltaTimeStamp < testRow.FirstBlockDeltaTimeStamp)
                    highIndex = testIndex;
                else if (fileDeltaTimeStamp > testRow.LastBlockDeltaTimeStamp)
                    lowIndex = testIndex;
                else
                    break;
            }

            if (testRow == null)
                return false;

            rowIndex = testRow.RowIndex;

            if (!testRow.IsEmpty && fileDeltaTimeStamp >= testRow.FirstBlockDeltaTimeStamp && fileDeltaTimeStamp <= testRow.LastBlockDeltaTimeStamp)
                return true;
            else
                return false;
        }

        #endregion

        private static readonly TRowType[] emptyRowTypeArray = new TRowType[0];
    }

    public class FileIndexRowBase
    {
        public int RowIndex { get; internal set; }

        public UInt32 FileIndexRowFlagBitsU4 { get; internal set; }       // 0..3
        public Int32 FileOffsetToStartOfFirstBlock { get; internal set; } // 4..7
        public UInt64 FileIndexUserRowFlagBits { get; internal set; }     // 8..15
        public double FirstBlockUtcTimeSince1601 { get; internal set; }   // 16..23
        public double FirstBlockDeltaTimeStamp { get; internal set; }     // 24..31
        public double LastBlockDeltaTimeStamp { get; internal set; }      // 32..39

        public FileIndexRowFlagBits FileIndexRowFlagBits { get { return unchecked((FileIndexRowFlagBits)FileIndexRowFlagBitsU4); } }
        public bool ContainsStartOfFullGroup { get { return ((FileIndexRowFlagBits & FileIndexRowFlagBits.ContainsStartOfFullGroup) != Common.FileIndexRowFlagBits.None); } }

        public DateTime FirstBlockDateTime { get { return FirstBlockUtcTimeSince1601.GetDateTimeFromUTCTimeSince1601(); } }

        internal static int SerializedSize { get { return 40; } }

        internal FileIndexRowBase Clear()
        {
            FileIndexRowFlagBitsU4 = 0;
            FileOffsetToStartOfFirstBlock = 0;
            FileIndexUserRowFlagBits = 0;
            FirstBlockDeltaTimeStamp = 0;
            LastBlockDeltaTimeStamp = 0;
            FirstBlockUtcTimeSince1601 = 0.0;

            return this;
        }

        public bool IsEmpty 
        { 
            get 
            { 
                return (FileIndexRowFlagBitsU4 == 0 
                        && FileOffsetToStartOfFirstBlock == 0 
                        && FileIndexUserRowFlagBits == 0
                        && FirstBlockUtcTimeSince1601 == 0.0
                        && FirstBlockDeltaTimeStamp == 0
                        && LastBlockDeltaTimeStamp == 0
                        ); 
            } 
        }
    }

    public class FileIndexLastBlockInfoBase
    {
        public Int32 FileOffsetToStartOfBlock { get; internal set; }  // 0..3
        public Int32 BlockTotalLength { get; internal set; }          // 4..7
        public Int32 BlockTypeID32 { get; internal set; }             // 8..11
        public double BlockDeltaTimeStamp { get; internal set; }      // 12..19

        internal static int SerializedSize { get { return 20; } }

        public FixedBlockTypeID FixedBlockTypeID { get { return unchecked((FixedBlockTypeID) BlockTypeID32); } }
    }

    #endregion

    #region IGroupInfo, IGroupPointInfo, IOccurrenceInfo, IMetaDataCommonInfo, implementation classes - these definitions are primarily used with MDRF Readers.

    /// <summary>
    /// This interface defines MetaData information that is common to all IGroupInfo, IGroupPointInfo and IOccurrenceInfo objects used here.
    /// This information includes an ItemType (Group, Source, or Occurrence), Name, Comment, FileID, ClientID, IFC and ClientNVS.
    /// FileID is generally a synonym for BlockTypeID32 for Group and Occurrence MDItemTypes.  
    /// ClientID is only used to index into internal tables for objects that have already been registered.  
    /// </summary>
    public interface IMetaDataCommonInfo
    {
        MDItemType ItemType { get; }
        string Name { get; }
        string Comment { get; }
        int FileID { get; }
        int ClientID { get; }
        Semi.E005.Data.ItemFormatCode IFC { get; }

        INamedValueSet ClientNVS { get; }
    }

    public interface IGroupInfo : IMetaDataCommonInfo
    {
        UInt64 FileIndexUserRowFlagBits { get; }

        IList<int> GroupPointFileIDList { get; }
        IList<IGroupPointInfo> GroupPointInfoList { get; }
        
        int GroupID { get; }
    }

    public interface IGroupPointInfo : IMetaDataCommonInfo
    {
        ContainerStorageType ValueCST { get; }

        ValueContainer VC { get; }

        int GroupID { get; }
        int SourceID { get; }
    }

    public interface IOccurrenceInfo : IMetaDataCommonInfo
    {
        UInt64 FileIndexUserRowFlagBits { get; }
        ContainerStorageType ContentCST { get; }
        bool IsHighPriority { get; }

        int OccurrenceID { get; }
    }

    public class MetaDataCommonInfoBase : IMetaDataCommonInfo
    {
        public MDItemType ItemType { get; internal set; }
        public string Name { get; internal set; }
        public string Comment { get; internal set; }
        public int FileID { get; internal set; }
        public int ClientID { get; internal set; }
        public Semi.E005.Data.ItemFormatCode IFC { get; internal set; }
        public INamedValueSet ClientNVS { get; internal set; }

        public MetaDataCommonInfoBase() {}
        public MetaDataCommonInfoBase(MetaDataCommonInfoBase rhs)
        {
            ItemType = rhs.ItemType;
            Name = rhs.Name;
            Comment = rhs.Comment;
            FileID = rhs.FileID;
            ClientID = rhs.ClientID;
            IFC = rhs.IFC;
            ClientNVS = rhs.ClientNVS;
        }
    }

    public class GroupInfo : MetaDataCommonInfoBase, IGroupInfo
    {
        public UInt64 FileIndexUserRowFlagBits { get; internal set; }

        public IList<int> GroupPointFileIDList { get; internal set; }
        public IList<IGroupPointInfo> GroupPointInfoList { get; internal set; }

        public int GroupID { get { return ClientID; } }

        public GroupInfo() { }
        public GroupInfo(MetaDataCommonInfoBase rhs) : base(rhs) { }
    }

    public class GroupPointInfo : MetaDataCommonInfoBase, IGroupPointInfo
    {
        public ContainerStorageType ValueCST { get; internal set; }

        public ValueContainer VC { get; internal set; }

        public int GroupID { get { return ClientID; } }
        public int SourceID { get; internal set; }

        public GroupPointInfo() { }
        public GroupPointInfo(MetaDataCommonInfoBase rhs) : base(rhs) { }
    }

    public class OccurrenceInfo : MetaDataCommonInfoBase, IOccurrenceInfo
    {
        public UInt64 FileIndexUserRowFlagBits { get; internal set; }
        public ContainerStorageType ContentCST { get; internal set; }
        public bool IsHighPriority { get; internal set; }

        public int OccurrenceID { get { return ClientID; } }

        public OccurrenceInfo() { }
        public OccurrenceInfo(MetaDataCommonInfoBase rhs) : base(rhs) { }
    }

    #endregion

    #region IMessageInfo

    public interface IMessageInfo
    {
        FixedBlockTypeID FixedBlockTypeID { get; }
        double FileDeltaTimeStamp { get; }
        double MessageRecordedUtcTime { get; }
        string Message { get; }
    }

    public class MessageInfo : IMessageInfo
    {
        public FixedBlockTypeID FixedBlockTypeID { get; internal set; }
        public double FileDeltaTimeStamp { get; internal set; }
        public double MessageRecordedUtcTime { get; internal set; }
        public string Message { get; internal set; }
    }

    #endregion

    /// <summary>
    /// This enumeration is used to define the fixed (hard coded) block type ID values that are currently used with this
    /// version of the MDRF.   Some of these values are choosen to fit in a U8Auto coded value using 32 bit actual size.
    /// This leaves the entire usable range of 16383 2 byte ID values to be used by the client while still supporting 32 bit
    /// centric library versions.
    /// <para/>This library uses predefined fixed block IDs in the range of 0x00000000 through 0x0000000f, 0x3fff0000 through 0x3fffffff and 0x7fff0000 through 0x7fffffff
    /// </summary>

    public enum FixedBlockTypeID : int
    {
        /// <summary>Resevered value.  No dynamically assigned ID may use this value.</summary>
        None = 0x00000000,

        /// <summary>Very high rate block - used to record a new delta time stamp (In I8 format)</summary>
        TimeStampUpdateV1 = 0x00000001,

        /// <summary>Resevered value.  This value is the first value that is assigned during setup.  Generally groups are assigned fileIDs first so that they use low numbered (1 byte) fileIDs.</summary>
        FirstDynamicallyAssignedID = 0x000000010,

        /// <summary>FixedBlockTypeID for File Heater: 0x7fffba5e (version 1)</summary>
        FileHeaderV1 = 0x7fffba5e,        // base

        /// <summary>FixedBlockTypeID for File End: 0x7fffc105 (version 1)</summary>
        FileEndV1 = 0x7fffc105,           // clos(e)

        /// <summary>FixedBlockTypeID for File Index: 0x3fff0001 (version 1 - 32 bit centric index - max supported file size is 2 GBytes.)</summary>
        FileIndexV1 = 0x3fff0001,

        /// <summary>FixedBlockTypeID for MetaData: 0x3fff0002 (version 1)</summary>
        MetaDataV1 = 0x3fff0002,

        /// <summary>FixedBlockTypeID for Error: 0x3fff0003</summary>
        ErrorV1 = 0x3fff0003,

        /// <summary>FixedBlockTypeID for Message: 0x3fff0004</summary>
        MessageV1 = 0x3fff0004,

        /// <summary>FixedBlockTypeID for DateTime record: 0x3fff0006 (version 1)</summary>
        DateTimeV1 = 0x3fff0006,
    }

    /// <summary>
    /// This enumeration defines the supported item types for which meta data is injected into the file.
    /// <para/>None = 0, Source = 1, Group = 2, Occurrence = 3
    /// </summary>
    public enum MDItemType
    {
        /// <summary>Reserved value.  No meta-data item should be defined with this type: 0</summary>
        None = 0,
        /// <summary>This meta-data item defines a Source: 1</summary>
        Source = 1,
        /// <summary>This meta-data item defines a Group: 2</summary>
        Group = 2,
        /// <summary>This meta-data item defines an Occurrence: 3</summary>
        Occurrence = 3,
    }

    /// <summary>
    /// This enumeration defines a set of flag bit values that are used in the file index to identify what types of data blocks are present (start) in any given row of the file index.
    /// </summary>
    [Flags]
    public enum FileIndexRowFlagBits : ushort
    {
        /// <summary>No specifically identified data block types start in this row: 0x0000</summary>
        None = 0x0000,
        /// <summary>At least one file Header or MetaData block starts in this row: 0x0001</summary>
        ContainsHeaderOrMetaData = 0x0001,
        /// <summary>At least one high priority occurrence data block starts in this row: 0x0002</summary>
        ContainsHighPriorityOccurrence = 0x0002,
        /// <summary>At least one occurrence data block starts in this row: 0x0004</summary>
        ContainsOccurrence = 0x0004,
        /// <summary>At least one date time data block starts in this row: 0x0008</summary>
        ContainsDateTime = 0x0008,
        /// <summary>At least one full group (writeAll) starts in this row: 0x0080</summary>
        ContainsStartOfFullGroup = 0x0080,
    }

    /// <summary>
    /// This enumeration defines a (small) set of flag bits and values that are used to decode and interpret the contents of Group data blocks.
    /// </summary>
    [Flags]
    public enum GroupBlockFlagBits : int
    {
        /// <summary>No special information is defined.  This group data block contains values for all of the sources in the group.  0x00</summary>
        None = 0x00,

        /// <summary>This flag is set when the group has an update mask (when at least 80% of the source values need to be included in the group).  0x01</summary>
        HasUpdateMask = 0x01,

        /// <summary>This group data block is also the first group data block in a full update set (writeAll).  <seealso cref="FileIndexRowFlagBits.ContainsStartOfFullGroup"/>  0x80</summary>
        IsStartOfFullGroup = 0x80,

        /// <summary>
        /// This flag is set when the group is an empty group (no source values will be included).  
        /// This is a special case value in that when it is set, the groupBlockFlagBits will not be included in the group!  
        /// 0x100 (value outside of bits that can be represented in the payload byte reserved for this field).
        /// </summary>
        IsEmptyGroup = 0x100,
    }

    /// <summary>
    /// MDRF related Extension Methods.
    /// </summary>
    public static partial class ExtensionMethods
    {
        private static readonly byte[] emptyByteArray = new byte[0];
        private static readonly ValueContainer.Union emptyU = new ValueContainer.Union();

        #region F4 <-> U4, F8 <-> U8 conversion

        public static Single CastToF4(this UInt32 u4) { ValueContainer.Union u = emptyU; u.u32 = u4; return u.f32; }
        public static Double CastToF8(this UInt64 u8) { ValueContainer.Union u = emptyU; u.u64 = u8; return u.f64; }
        public static UInt32 CastToU4(this Single f4) { ValueContainer.Union u = emptyU; u.f32 = f4; return u.u32; }
        public static UInt64 CastToU8(this Double f8) { ValueContainer.Union u = emptyU; u.f64 = f8; return u.u64; }

        #endregion

        #region U8Auto (et. al.) related: DecodeU8Auto, AppendU8Auto, AppendU4Auto, AppendU2Auto

        public static UInt64 DecodeU8Auto(this byte[] byteArray, ref int startIndex, ref string ec)
        {
            UInt64 u64 = 0;

            byteArray = byteArray ?? emptyByteArray;

            bool success = (startIndex < byteArray.Length);
            int numBytes = 0;
            byte header = byteArray.SafeAccess(startIndex);

            if (!success)
            { }
            else if ((header & 0x80u) == 0x00)
            {
                u64 = header;
                numBytes = 1;
            }
            else if ((header & 0xc0) == 0x80)
            {
                Utils.Data.Pack(byteArray, startIndex + 1, 1, out u64);
                u64 += ((UInt64)(header & 0x3f) << 8);
                numBytes = 2;
            }
            else if ((header & 0xe0) == 0xc0)
            {
                success = Utils.Data.Pack(byteArray, startIndex + 1, 2, out u64);
                u64 += ((UInt64)(header & 0x1f) << 16);
                numBytes = 3;
            }
            else if ((header & 0xf0) == 0xe0)
            {
                success = Utils.Data.Pack(byteArray, startIndex + 1, 3, out u64);
                u64 += ((UInt64)(header & 0x0f) << 24);
                numBytes = 4;
            }
            else if ((header & 0xf8) == 0xf0)
            {
                success = Utils.Data.Pack(byteArray, startIndex + 1, 4, out u64);
                u64 += ((UInt64)(header & 0x07) << 32);
                numBytes = 5;
            }
            else if ((header & 0xfc) == 0xf8)
            {
                success = Utils.Data.Pack(byteArray, startIndex + 1, 5, out u64);
                u64 += ((UInt64)(header & 0x03) << 40);
                numBytes = 6;
            }
            else if ((header & 0xfe) == 0xfc)
            {
                success = Utils.Data.Pack(byteArray, startIndex + 1, 6, out u64);
                u64 += ((UInt64)(header & 0x01) << 48);
                numBytes = 7;
            }
            else if (header == 0xfe)
            {
                success = Utils.Data.Pack(byteArray, startIndex + 1, 7, out u64);
                numBytes = 8;
            }
            else
            {
                success = Utils.Data.Pack(byteArray, startIndex + 1, 8, out u64);
                numBytes = 9;
            }

            if (success)
                startIndex += numBytes;
            else
                ec = ec.MapNullOrEmptyTo("DecodeU8Auto failed: decode overran end of buffer");

            return u64;
        }

        public static void AppendU8Auto(this List<byte> byteArrayBuilder, UInt64 valueU8)
        {
            if ((valueU8 & ~0x0ffffu) == 0)
                byteArrayBuilder.AppendU2Auto(unchecked((UInt16)valueU8));
            else if ((valueU8 & ~0x0ffffffffu) == 0)
                byteArrayBuilder.AppendU4Auto(unchecked((UInt32)valueU8));
            else
            {
                byte b0, b1, b2, b3, b4, b5, b6, b7;

                Utils.Data.Unpack(valueU8, out b7, out b6, out b5, out b4, out b3, out b2, out b1, out b0);

                if (valueU8 <= 0x00000007ffffffff)
                {
                    byteArrayBuilder.Add(unchecked((byte)(0xf0u | b4)));
                    byteArrayBuilder.Add(b3);
                    byteArrayBuilder.Add(b2);
                    byteArrayBuilder.Add(b1);
                    byteArrayBuilder.Add(b0);
                }
                else if (valueU8 <= 0x000003ffffffffff)
                {
                    byteArrayBuilder.Add(unchecked((byte)(0xf8u | b5)));
                    byteArrayBuilder.Add(b4);
                    byteArrayBuilder.Add(b3);
                    byteArrayBuilder.Add(b2);
                    byteArrayBuilder.Add(b1);
                    byteArrayBuilder.Add(b0);
                }
                else if (valueU8 <= 0x0001ffffffffffff)
                {
                    byteArrayBuilder.Add(unchecked((byte)(0xfcu | b6)));
                    byteArrayBuilder.Add(b5);
                    byteArrayBuilder.Add(b4);
                    byteArrayBuilder.Add(b3);
                    byteArrayBuilder.Add(b2);
                    byteArrayBuilder.Add(b1);
                    byteArrayBuilder.Add(b0);
                }
                else if (valueU8 <= 0x00ffffffffffffff)
                {
                    byteArrayBuilder.Add(unchecked((byte)(0xfeu)));
                    byteArrayBuilder.Add(b6);
                    byteArrayBuilder.Add(b5);
                    byteArrayBuilder.Add(b4);
                    byteArrayBuilder.Add(b3);
                    byteArrayBuilder.Add(b2);
                    byteArrayBuilder.Add(b1);
                    byteArrayBuilder.Add(b0);
                }
                else
                {
                    byteArrayBuilder.Add(unchecked((byte)(0xffu)));
                    byteArrayBuilder.Add(b7);
                    byteArrayBuilder.Add(b6);
                    byteArrayBuilder.Add(b5);
                    byteArrayBuilder.Add(b4);
                    byteArrayBuilder.Add(b3);
                    byteArrayBuilder.Add(b2);
                    byteArrayBuilder.Add(b1);
                    byteArrayBuilder.Add(b0);
                }
            }
        }

        public static void AppendU4Auto(this List<byte> byteArrayBuilder, UInt32 valueU4)
        {
            if ((valueU4 & ~0x0ffffu) == 0)
                byteArrayBuilder.AppendU2Auto(unchecked((UInt16)valueU4));
            else
            {
                byte b0, b1, b2, b3;

                Utils.Data.Unpack(valueU4, out b3, out b2, out b1, out b0);

                if (valueU4 <= 0x001fffffu)
                {
                    byteArrayBuilder.Add(unchecked((byte)(0xc0u | b2)));
                    byteArrayBuilder.Add(b1);
                    byteArrayBuilder.Add(b0);
                }
                else if (valueU4 <= 0x0fffffffu)
                {
                    byteArrayBuilder.Add(unchecked((byte)(0xe0u | b3)));
                    byteArrayBuilder.Add(b2);
                    byteArrayBuilder.Add(b1);
                    byteArrayBuilder.Add(b0);
                }
                else
                {
                    byteArrayBuilder.Add(unchecked((byte)(0xf0u)));
                    byteArrayBuilder.Add(b3);
                    byteArrayBuilder.Add(b2);
                    byteArrayBuilder.Add(b1);
                    byteArrayBuilder.Add(b0);
                }
            }
        }

        public static void AppendU2Auto(this List<byte> byteArrayBuilder, UInt16 valueU2)
        {
            byte b0, b1;

            Utils.Data.Unpack(valueU2, out b1, out b0);

            if (valueU2 <= 0x007fu)
            {
                byteArrayBuilder.Add(b0);
            }
            else if (valueU2 <= 0x3fffu)
            {
                byteArrayBuilder.Add(unchecked((byte) (0x80u | b1)));
                byteArrayBuilder.Add(b0);
            }
            else
            {
                byteArrayBuilder.Add(unchecked((byte)(0xc0u)));
                byteArrayBuilder.Add(b1);
                byteArrayBuilder.Add(b0);
            }
        }

        #endregion
    }
}

//-------------------------------------------------------------------
