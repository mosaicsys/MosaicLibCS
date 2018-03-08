//-------------------------------------------------------------------
/*! @file MDRFReaderPart.cs
 *  @brief an active part that supports reading MDRF (Mosaic Data Recording Format) files.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2016 Mosaic Systems Inc.
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MosaicLib.File;
using MosaicLib.Modular;
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

namespace MosaicLib.PartsLib.Tools.MDRF.Reader
{
    #region ReadAndProcessContents helper definitions

    public class ReadAndProcessFilterSpec
    {
        public EventHandlerDelegate<ProcessContentEventData> EventHandlerDelegate { get; set; }
        public ProcessContentEvent PCEMask { get; set; }
        public double FirstFileDeltaTimeStamp { get; set; }
        public double LastFileDeltaTimeStamp { get; set; }
        public DateTime FirstDateTime { get; set; }
        public DateTime LastDateTime { get; set; }
        public int FirstEventBlockStartOffset { get; set; }
        public int LastEventBlockStartOffset { get; set; }
        public double NominalMinimumGroupAndTimeStampUpdateInterval { get; set; }
        /// <summary>When non-zero this mask will be used to skip reading contents from rows that do not have any of the indicated user row flag bits set</summary>
        public UInt64 FileIndexUserRowFlagBitsMask { get; set; }
        /// <summary>When true, this flag will cause the reader to rewind the starting file delta time stamp to the next oldest row that includes the start of a full group.  These will be processed but will not generate events.</summary>
        public bool AutoRewindToPriorFullGroupRow { get; set; }

        public Func<IOccurrenceInfo, bool> OccurrenceFilterDelegate { get { return occurrenceFilterDelegate; } set { occurrenceFilterDelegate = value ?? defaultOccurrenceFilter; } }
        public Func<IGroupInfo, bool> GroupFilterDelegate { get { return groupFilterDelegate; } set { groupFilterDelegate = value ?? defaultGroupFilter; } }

        private Func<IOccurrenceInfo, bool> occurrenceFilterDelegate = defaultOccurrenceFilter;
        private Func<IGroupInfo, bool> groupFilterDelegate = defaultGroupFilter;

        public ReadAndProcessFilterSpec()
        {
            SetFrom(null);
        }

        public ReadAndProcessFilterSpec(ReadAndProcessFilterSpec other)
        {
            SetFrom(other);
        }

        public ReadAndProcessFilterSpec SetFrom(ReadAndProcessFilterSpec other)
        {
            if (other == null)
            {
                EventHandlerDelegate = null;
                PCEMask = ProcessContentEvent.All;
                FirstFileDeltaTimeStamp = Double.NegativeInfinity;
                LastFileDeltaTimeStamp = Double.PositiveInfinity;
                FirstDateTime = DateTime.MinValue;
                LastDateTime = DateTime.MaxValue;
                FirstEventBlockStartOffset = 0;
                LastEventBlockStartOffset = int.MaxValue;
                NominalMinimumGroupAndTimeStampUpdateInterval = 0.0;
                FileIndexUserRowFlagBitsMask = 0;
                AutoRewindToPriorFullGroupRow = false;
                OccurrenceFilterDelegate = null;
                GroupFilterDelegate = null;
            }
            else
            {
                EventHandlerDelegate = other.EventHandlerDelegate;
                PCEMask = other.PCEMask;
                FirstFileDeltaTimeStamp = other.FirstFileDeltaTimeStamp;
                LastFileDeltaTimeStamp = other.LastFileDeltaTimeStamp;
                FirstDateTime = other.FirstDateTime;
                LastDateTime = other.LastDateTime;
                FirstEventBlockStartOffset = other.FirstEventBlockStartOffset;
                LastEventBlockStartOffset = other.LastEventBlockStartOffset;
                NominalMinimumGroupAndTimeStampUpdateInterval = other.NominalMinimumGroupAndTimeStampUpdateInterval;
                FileIndexUserRowFlagBitsMask = other.FileIndexUserRowFlagBitsMask;
                AutoRewindToPriorFullGroupRow = other.AutoRewindToPriorFullGroupRow;
                OccurrenceFilterDelegate = other.OccurrenceFilterDelegate;
                GroupFilterDelegate = other.GroupFilterDelegate;
            }

            return this;
        }

        private static readonly Func<IOccurrenceInfo, bool> defaultOccurrenceFilter = (IOccurrenceInfo oi) => true;
        private static readonly Func<IGroupInfo, bool> defaultGroupFilter = (IGroupInfo gi) => true;

        internal FileIndexRowBase[] UpdateFilterSpecAndGenerateFilteredFileIndexRows(DateTimeInfo dateTimeInfo, FileIndexInfo fileIndexInfo)
        {
            bool deltaTSAreDefaults = (FirstFileDeltaTimeStamp == Double.NegativeInfinity && LastFileDeltaTimeStamp == Double.PositiveInfinity);
            bool dateTimesAreDefaults = (FirstDateTime == DateTime.MinValue && LastDateTime == DateTime.MaxValue);
            bool eventBlockOffsetsAreDefaults = (FirstEventBlockStartOffset == 0 && LastEventBlockStartOffset == int.MaxValue);
            bool fileWasProperlyClosed = fileIndexInfo.FileWasProperlyClosed;

            // first filter out all of the null/empty rows and all of the rows that do not have the desired userRowFlagBitsSet (if desired).

            Func<FileIndexRowBase, bool> filterFunction = row => (row != null && !row.IsEmpty && ((FileIndexUserRowFlagBitsMask == 0) || (row.FileIndexUserRowFlagBits & FileIndexUserRowFlagBitsMask) != 0));

            FileIndexRowBase[] filteredFIRBArray = fileIndexInfo.FileIndexRowArray.Where(filterFunction).ToArray();

            int firstFilteredIndex = 0;
            int lastFilteredIndex = filteredFIRBArray.SafeLength() - 1;

            // find first filtered row that is at or after all constraints
            while (firstFilteredIndex < lastFilteredIndex)
            {
                FileIndexRowBase nextFilteredFIB = filteredFIRBArray.SafeAccess(firstFilteredIndex + 1);

                if (!deltaTSAreDefaults && FirstFileDeltaTimeStamp >= nextFilteredFIB.FirstBlockDeltaTimeStamp)
                    firstFilteredIndex++;
                else if (!dateTimesAreDefaults && FirstDateTime >= nextFilteredFIB.FirstBlockDateTime)
                    firstFilteredIndex++;
                else if (!eventBlockOffsetsAreDefaults && FirstEventBlockStartOffset >= nextFilteredFIB.FileOffsetToStartOfFirstBlock)
                    firstFilteredIndex++;
                else
                    break;
            }

            // if we can trust the index to contain the last valid row then rewinde the lastFilteredIndex as long as it cannot contain any data for the user defined filtered range
            if (fileWasProperlyClosed)
            {
                while (firstFilteredIndex < lastFilteredIndex)
                {
                    FileIndexRowBase lastFilteredFIB = filteredFIRBArray[lastFilteredIndex];

                    if (!deltaTSAreDefaults && LastFileDeltaTimeStamp < lastFilteredFIB.FirstBlockDeltaTimeStamp)
                        lastFilteredIndex--;
                    else if (!dateTimesAreDefaults && LastDateTime < lastFilteredFIB.FirstBlockDateTime)
                        lastFilteredIndex--;
                    else if (!eventBlockOffsetsAreDefaults && LastEventBlockStartOffset < lastFilteredFIB.FileOffsetToStartOfFirstBlock)
                        lastFilteredIndex--;
                    else
                        break;
                }
            }

            if (firstFilteredIndex > 0 && AutoRewindToPriorFullGroupRow)
            {
                // rewind to first prior row that has its ContainsStartOfFullGroup bit set, or until we get back to the front of the file)
                while (firstFilteredIndex > 0)
                {
                    FileIndexRowBase firstFilteredFIB = filteredFIRBArray[firstFilteredIndex];
                    if ((firstFilteredFIB.FileIndexRowFlagBits & FileIndexRowFlagBits.ContainsStartOfFullGroup) != 0)
                        break;

                    firstFilteredIndex--;
                }
            }

            if (dateTimesAreDefaults)
            {
                FileIndexRowBase firstFilteredFIB = filteredFIRBArray[firstFilteredIndex];
                FileIndexRowBase lastFilteredFIB = filteredFIRBArray[lastFilteredIndex];
                FileIndexRowBase lastFilteredP1FIB = filteredFIRBArray.SafeAccess(lastFilteredIndex + 1);

                FirstDateTime = (firstFilteredFIB != null) ? firstFilteredFIB.FirstBlockDateTime : DateTime.MinValue;

                if (lastFilteredFIB == null)
                    LastDateTime = DateTime.MaxValue;
                else if (lastFilteredP1FIB != null)
                    LastDateTime = lastFilteredP1FIB.FirstBlockDateTime;
                else if (fileWasProperlyClosed)
                    LastDateTime = lastFilteredFIB.FirstBlockDateTime + (fileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp - lastFilteredFIB.FirstBlockDeltaTimeStamp).FromSeconds();
                else
                    LastDateTime = DateTime.MaxValue;
            }

            return filteredFIRBArray.SafeSubArray(firstFilteredIndex, lastFilteredIndex - firstFilteredIndex + 1);
        }
    }


    /// <summary>
    /// bitfield enumeration gives each of the "events" that the file reader client can be informed of while processing an mdrf file.
    /// <para/>None (0x0000), ReadingStart (0x0001), ReadingEnd (0x0002), RowStart (0x0004), RowEnd (0x0008), NewTimeStamp (0x0010),
    /// FileLengthChanged (0x0020), Occurrence (0x0100), Message (0x0200), Error (0x0400), Group (0x1000), EmtpyGroup (0x2000),
    /// PartialGroup (0x4000), StartOfFullGroup (0x8000), GroupSetStart (0x10000), GroupSetEnd (0x20000),
    /// All (0x3ffff)
    /// </summary>
    [Flags]
    public enum ProcessContentEvent : int
    {
        /// <summary>Placeholder default value [0x0000]</summary>
        None = 0x0000,

        /// <summary>Reports that reading is starting on a file [0x0001]</summary>
        ReadingStart = 0x0001,

        /// <summary>Reports that reading has ended on a file [0x0002]</summary>
        ReadingEnd = 0x0002,

        /// <summary>Reports that the reader has started reading on a new row in the index</summary>
        RowStart = 0x0004,

        /// <summary>Reports that the reader has finished reading from the given row in the index</summary>
        RowEnd = 0x0008,

        /// <summary>Reports that the reader has just processed a NewTimeStamp block</summary>
        NewTimeStamp = 0x0010,

        /// <summary>Reports that the reader has detected that the FileLength changed since the last time that the reader read the file index</summary>
        FileLengthChanged = 0x0020,

        /// <summary>Reports that the reader has read the given Occurrence block</summary>
        Occurrence = 0x0100,

        /// <summary>Reports that the reader has read the given Message block</summary>
        Message = 0x0200,

        /// <summary>Reports that the reader has read the given Error (Message) block</summary>
        Error = 0x0400,

        /// <summary>Reports that the reader has read the given Group block</summary>
        Group = 0x1000,

        /// <summary>Reports that the reader has read the given Group block, which was empty</summary>
        EmptyGroup = 0x2000,

        /// <summary>Reports that the reader has read the given Group block, which was partial</summary>
        PartialGroup = 0x4000,

        /// <summary>Indicates that the corresponding groups data block payload indicates that this is the start of a full group update (writeall).  NOTE: this event is only included if the filterSpec includes the first defined group.</summary>
        StartOfFullGroup = 0x8000,

        /// <summary>Reports that the reader has just started reading a Group Set (a run of Group blocks with no blocks of other types between them).</summary>
        GroupSetStart = 0x10000,

        /// <summary>Reports that the reader has just finished reading a Group Set (a run of Group blocks with no blocks of other types between them).</summary>
        GroupSetEnd = 0x20000,

        /// <summary>
        /// All = (ReadingStart | ReadingEnd | RowStart | RowEnd | NewTimeStamp | FileLengthChanged | Occurrence | Message | Error | Group | EmptyGroup | PartialGroup | StartOfFullGroup | GroupSetStart | GroupSetEnd) [0x3ffff]
        /// </summary>
        All = (ReadingStart | ReadingEnd | RowStart | RowEnd | NewTimeStamp | FileLengthChanged | Occurrence | Message | Error | Group | EmptyGroup | PartialGroup | StartOfFullGroup | GroupSetStart | GroupSetEnd),
    }

    public class ProcessContentEventData
    {
        public MDRFFileReader Reader { get; internal set; }
        public ProcessContentEvent PCE { get; internal set; }
        public string FilePath { get; internal set; }
        public UInt64 SeqNum { get; internal set; }
        /// <summary>This DateTime is generated from the FileDeltaTimeStamp (which is recorded) by inference using the DateTime values recorded in the current row, and the next row (if it is present).  Due to the relative sample timing and the use of interpolation, individual DateTime values may not be strictly monotonic in relation to the underlying delta timestamp values.</summary>
        public DateTime UTCDateTime { get; internal set; }
        public double FileDeltaTimeStamp { get; internal set; }
        public FileIndexRowBase Row { get; internal set; }
        public IMessageInfo MessageInfo { get; internal set; }
        public IOccurrenceInfo OccurrenceInfo { get; internal set; }
        public IGroupInfo GroupInfo { get; internal set; }
        public ValueContainer VC { get; internal set; }
        public IGroupInfo [] GroupInfoArray { get; internal set; }
        public IGroupInfo[] FilteredGroupInfoArray { get; internal set; }

        /// <summary>Setter also asigns FileDeltaTimeStamp from blocks delta timestamp if the given value is not null</summary>
        public DataBlockBuffer DataBlockBuffer 
        {
            get { return dataBlockBuffer; }
            internal set { dataBlockBuffer = value; if (value != null) { FileDeltaTimeStamp = value.fileDeltaTimeStamp; } }
        }
        private DataBlockBuffer dataBlockBuffer;

        public ProcessContentEventData()
        { }

        public ProcessContentEventData(ProcessContentEventData other)
        {
            Reader = other.Reader;
            PCE = other.PCE;
            FilePath = other.FilePath;
            SeqNum = other.SeqNum;
            UTCDateTime = other.UTCDateTime;
            FileDeltaTimeStamp = other.FileDeltaTimeStamp;
            Row = other.Row;
            MessageInfo = other.MessageInfo;
            OccurrenceInfo = other.OccurrenceInfo;
            GroupInfo = other.GroupInfo;
            VC = other.VC;
        }

        public override string ToString()
        {
            string dbbLenPart = null;
            if (dataBlockBuffer != null)
                dbbLenPart = " dbbLen:{0}/{1}@{2}".CheckedFormat(dataBlockBuffer.headerLength, dataBlockBuffer.payloadLength, dataBlockBuffer.fileOffsetToStartOfBlock);

            string common = "pce:{0} dt:{1:yyyyMMdd_HHmmss.fff} deltaT:{2:f6} seq:{3}{4}".CheckedFormat(PCE, UTCDateTime.ToLocalTime(), FileDeltaTimeStamp, SeqNum, dbbLenPart);

            switch (PCE)
            {
                case ProcessContentEvent.RowStart:
                case ProcessContentEvent.RowEnd:
                    return "{0} row:{1} vc:{2}".CheckedFormat(common, Row, VC);
                case ProcessContentEvent.Group:
                case ProcessContentEvent.EmptyGroup:
                case ProcessContentEvent.PartialGroup:
                case (ProcessContentEvent.Group | ProcessContentEvent.EmptyGroup):
                case (ProcessContentEvent.Group | ProcessContentEvent.PartialGroup):
                    return "{0} name:{1} id:{2} fid:{3} vc:{4}".CheckedFormat(common, GroupInfo.Name, GroupInfo.GroupID, GroupInfo.FileID, VC);

                case ProcessContentEvent.Occurrence:
                    return "{0} name:{1} id:{2} fid:{3} vc:{4}".CheckedFormat(common, OccurrenceInfo.Name, OccurrenceInfo.OccurrenceID, OccurrenceInfo.FileID, VC);

                case ProcessContentEvent.Message:
                case ProcessContentEvent.Error:
                    return "{0} mesg:[{1}]".CheckedFormat(common, MessageInfo.Message);

                default:
                    return "{0} vc:{1}".CheckedFormat(common, VC);
            }
        }
    }

    public class DataBlockBuffer : IEquatable<DataBlockBuffer>
    {
        public int fileOffsetToStartOfBlock;

        public string resultCode;
        public bool IsValid { get { return resultCode.IsNullOrEmpty(); } }
        public bool HasPayloadBytes { get; set; }

        public Int32 blockTypeID32; // U8Auto coded
        public double fileDeltaTimeStamp;
        public int payloadLength;

        public int headerLength;

        public byte[] payloadDataArray = emptyByteArray;

        public FixedBlockTypeID FixedBlockTypeID { get { return unchecked((FixedBlockTypeID)blockTypeID32); } }
        public int TotalLength { get { return headerLength + payloadLength; } }

        public DataBlockBuffer()
        {
            resultCode = string.Empty;
        }

        private static readonly byte[] emptyByteArray = EmptyArrayFactory<byte>.Instance;

        public bool Equals(DataBlockBuffer other)
        {
            return (other != null
                    && (fileOffsetToStartOfBlock == other.fileOffsetToStartOfBlock)
                    && (resultCode == other.resultCode)
                    && (HasPayloadBytes == other.HasPayloadBytes)
                    && (blockTypeID32 == other.blockTypeID32)
                    && (fileDeltaTimeStamp == other.fileDeltaTimeStamp)
                    && (payloadLength == other.payloadLength)
                    && (headerLength == other.headerLength)
                    && (payloadDataArray.IsEqualTo(other.payloadDataArray))
                    );
        }

        public bool DoesFilterAcceptThis(ReadAndProcessFilterSpec filterSpec, bool checkIfComesAfterStartOfFilterPeriod = true, bool checkIfComesBeforeEndOfFilterPeriod = true)
        {
            if (checkIfComesAfterStartOfFilterPeriod && (fileOffsetToStartOfBlock < filterSpec.FirstEventBlockStartOffset || fileDeltaTimeStamp < filterSpec.FirstFileDeltaTimeStamp))
                return false;

            if (checkIfComesBeforeEndOfFilterPeriod && (fileOffsetToStartOfBlock > filterSpec.LastEventBlockStartOffset || fileDeltaTimeStamp > filterSpec.LastFileDeltaTimeStamp))
                return false;

            return true;
        }
    }

    #endregion

    /// <summary>
    /// bitfield enumeration used to define various MDRFFileReader behavior settings
    /// <para/>None (0x0000), AutoCreateIVI (0x01), EnableLiveFileHeurstics (0x02)
    /// </summary>
    [Flags]
    public enum MDRFFileReaderBehavior : int
    {
        /// <summary>None [0x00 - default placeholder value]</summary>
        None = 0x0000,

        /// <summary>
        /// Selects that MDRFFileReader should create a default IVI if none is explicitly provided in the constuctor.  
        /// If the constructor value is null and this behavior flag is not present then no IVI (nor any related IVAs) will be used by the reader.
        /// </summary>
        AutoCreateIVI = 0x01,

        /// <summary>
        /// Selects that the MDRFFileReader is likely being used with files that may be being updated while they are being read.
        /// This option selects that the file index block reader will repeatedly attempt to read the index block contents until it gets the same value
        /// twice in a row as a heuristics to indicate that any related writer is done updating the file index block.  This heuristic is only useful
        /// if any related writer is not flushing changes to the index on a very frequent basis.
        /// </summary>
        EnableLiveFileHeuristics = 0x02,
    }

    public class MDRFFileReader : DisposableBase
    {
        #region Construction (and Release)

        public MDRFFileReader(string path, IValuesInterconnection ivi = null, MDRFFileReaderBehavior mdrfFileReaderBehavior = MDRFFileReaderBehavior.AutoCreateIVI)
        {
            MDRFFileReaderBehavior = mdrfFileReaderBehavior;
            AddExplicitDisposeAction(Release);

            FilePath = path.MapNullToEmpty();
            IVI = ((ivi != null || !autoCreateIVI) ? ivi : new ValuesInterconnection("{0} {1}".CheckedFormat(Fcns.CurrentMethodName, FilePath), registerSelfInDictionary: false, makeAPIThreadSafe: true));

            LibraryInfo = new LibraryInfo();
            SetupInfo = emptySetupInfo;
            FileIndexInfo = emptyFileIndexInfo;
            DateTimeInfo = new DateTimeInfo();

            try
            {
                fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                UpdateFileLength();

                ResultCode = ReadHeaderBlocks();

                if (ResultCode.IsNullOrEmpty())
                    ResultCode = ReadMetaDataBlocks();
            }
            catch (System.Exception ex)
            {
                ResultCode = "Open '{0}' failed: {1} {2}".CheckedFormat(FilePath, ex.GetType(), ex.Message);
            }
        }

        public void Release()
        {
            Fcns.DisposeOfObject(ref fs);
        }

        public MDRFFileReaderBehavior MDRFFileReaderBehavior 
        {
            get { return _mdrfFileReaderBehavior; }
            private set
            {
                _mdrfFileReaderBehavior = value;
                autoCreateIVI = value.IsSet(MDRFFileReaderBehavior.AutoCreateIVI);
                enableLiveFileHeuristics = value.IsSet(MDRFFileReaderBehavior.EnableLiveFileHeuristics);
            }
        }
        private MDRFFileReaderBehavior _mdrfFileReaderBehavior;
        private bool autoCreateIVI, enableLiveFileHeuristics;

        #endregion

        #region public properties

        public string FilePath { get; private set; }
        public IValuesInterconnection IVI { get; private set; }

        public bool IsFileOpen { get { return (fs != null); } }
        public string ResultCode { get; private set; }

        public string HostName { get; private set; }
        public LibraryInfo LibraryInfo { get; private set; }
        public SetupInfo SetupInfo { get; private set; }
        public FileIndexInfo FileIndexInfo { get; private set; }
        public DateTimeInfo DateTimeInfo { get; private set; }

        public INamedValueSet[] MetaDataArray { get; private set; }
        public IGroupInfo[] GroupInfoArray { get; private set; }
        public IGroupPointInfo[] GroupPointInfoArray { get; private set; }
        public IOccurrenceInfo[] OccurrenceInfoArray { get; private set; }
        public IValueAccessor[] GroupPointIVAArray { get; private set; }

        public IDictionary<string, IGroupInfo> GroupInfoDictionary { get; private set; }
        public IDictionary<string, IGroupPointInfo> GroupPointInfoDictionary { get; private set; }
        public IDictionary<string, IOccurrenceInfo> OccurrenceInfoDictionary { get; private set; }
        public IDictionary<int, IMetaDataCommonInfo> FileIDToMDCommonDictinoary { get; private set; }

        public int FileLength { get; private set; }

        public void UpdateFileLength()
        {
            try
            {
                FileLength = unchecked((int)((fs != null) ? fs.Length : 0));        // This will obtain the current actual file length from win32 using the file handle
            }
            catch
            {
                FileLength = 0;
            }
        }

        #endregion

        #region Private ReadHeaderBlocks and ReadMetaDataBlocks methods, LocalGroupInfo and LocalGroupPointInfo classes

        private string ReadHeaderBlocks()
        {
            fileScanOffset = 0;

            DataBlockBuffer fileHeaderDbb = ReadDataBlockAndHandleTimeStampUpdates(ref fileScanOffset);

            if (!fileHeaderDbb.IsValid)
                return "ReadFileAndHeader failed at 1st block: {0}".CheckedFormat(fileHeaderDbb.resultCode);

            if (fileHeaderDbb.FixedBlockTypeID != FixedBlockTypeID.FileHeaderV1)
                return "ReadFileAndHeader failed: unexpected 1st block {0}".CheckedFormat(fileHeaderDbb.FixedBlockTypeID);

            fileIndexBlockStartOffset = fileScanOffset;

            DataBlockBuffer fileIndexDbb = null;
            LoadFileIndexInfo(out fileIndexDbb, attemptToDecodeFileIndex: false);

            if (!fileIndexDbb.IsValid)
                return "ReadFileAndHeader failed at 2nd block: {0}".CheckedFormat(fileIndexDbb.resultCode);

            if (fileIndexDbb.FixedBlockTypeID != FixedBlockTypeID.FileIndexV1)
                return "ReadFileAndHeader failed: unexpected 2nd block {0}".CheckedFormat(fileIndexDbb.FixedBlockTypeID);

            fileScanOffset += fileIndexDbb.TotalLength;

            DataBlockBuffer dateTimeDbb = ReadDataBlockAndHandleTimeStampUpdates(ref fileScanOffset);

            if (!dateTimeDbb.IsValid)
                return "ReadFileAndHeader failed at 3rd block: {0}".CheckedFormat(dateTimeDbb.resultCode);

            if (dateTimeDbb.FixedBlockTypeID != FixedBlockTypeID.DateTimeV1)
                return "ReadFileAndHeader failed: unexpected 3nd block {0}".CheckedFormat(dateTimeDbb.FixedBlockTypeID);

            // now we get to decode all 3 : FileHeader (LibraryInfo, SetupInfo, HostName), FileIndexInfo, (first) DateTimeInfo
            string ec = string.Empty;

            NamedValueSet setupAndLibNVS = null, dateTimeInfoNVS = null;
            // attempt to decode the fileHeader to obtain the LibraryInfo, SetupInfo and HostName
            {
                int decodeIndex = 0;
                setupAndLibNVS = new NamedValueSet().ConvertFromE005Data(fileHeaderDbb.payloadDataArray, ref decodeIndex, ref ec);

                LibraryInfo.NVS = setupAndLibNVS.ConvertToReadOnly();

                LibraryInfo.Type = setupAndLibNVS["lib.Type"].VC.GetValue<string>(false).MapNullToEmpty();
                LibraryInfo.Name = setupAndLibNVS["lib.Name"].VC.GetValue<string>(false).MapNullToEmpty();
                LibraryInfo.Version = setupAndLibNVS["lib.Version"].VC.GetValue<string>(false).MapNullToEmpty();

                SetupInfo = new Common.SetupInfo();

                SetupInfo.DirPath = setupAndLibNVS["setup.DirPath"].VC.GetValue<string>(false).MapNullToEmpty();
                SetupInfo.ClientName = setupAndLibNVS["setup.ClientName"].VC.GetValue<string>(false).MapNullToEmpty();
                SetupInfo.FileNamePrefix = setupAndLibNVS["setup.FileNamePrefix"].VC.GetValue<string>(false).MapNullToEmpty();
                SetupInfo.CreateDirectoryIfNeeded = setupAndLibNVS["setup.CreateDirectoryIfNeeded"].VC.GetValue<bool>(false);
                SetupInfo.MaxDataBlockSize = setupAndLibNVS["setup.MaxDataBlockSize"].VC.GetValue<Int32>(false);
                SetupInfo.NominalMaxFileSize = setupAndLibNVS["setup.NominalMaxFileSize"].VC.GetValue<Int32>(false);
                SetupInfo.FileIndexNumRows = setupAndLibNVS["setup.FileIndexNumRows"].VC.GetValue<Int32>(false);

                SetupInfo.MaxFileRecordingPeriod = setupAndLibNVS["setup.MaxFileRecordingPeriodInSeconds"].VC.GetValue<TimeSpan>(false);
                SetupInfo.MinInterFileCreateHoldoffPeriod = setupAndLibNVS["setup.MinInterFileCreateHoldoffPeriodInSeconds"].VC.GetValue<TimeSpan>(false);
                SetupInfo.MinNominalFileIndexWriteInterval = setupAndLibNVS["setup.MinNominalFileIndexWriteIntervalInSeconds"].VC.GetValue<TimeSpan>(false);
                SetupInfo.MinNominalWriteAllInterval = setupAndLibNVS["setup.MinNominalWriteAllIntervalInSeconds"].VC.GetValue<TimeSpan>(false);

                SetupInfo.I8Offset = setupAndLibNVS["setup.I8Offset"].VC.GetValue<Int64>(false);
                SetupInfo.I4Offset = setupAndLibNVS["setup.I4Offset"].VC.GetValue<Int32>(false);
                SetupInfo.I2Offset = setupAndLibNVS["setup.I2Offset"].VC.GetValue<Int16>(false);

                HostName = setupAndLibNVS["HostName"].VC.GetValue<string>(false).MapNullToEmpty();

                SetupInfo.ClientNVS.ConvertFromE005Data(fileHeaderDbb.payloadDataArray, ref decodeIndex, ref ec);
            }

            // attempt to decode the first DateTimeInfo
            {
                int decodeIndex = 0;
                dateTimeInfoNVS = new NamedValueSet().ConvertFromE005Data(dateTimeDbb.payloadDataArray, ref decodeIndex, ref ec);

                DateTimeInfo.NVS = dateTimeInfoNVS.ConvertToReadOnly();
                DateTimeInfo.BlockDeltaTimeStamp = dateTimeInfoNVS["BlockDeltaTimeStamp"].VC.GetValue<double>(false);
                DateTimeInfo.QPCTime = dateTimeInfoNVS["QPCTime"].VC.GetValue<double>(false);
                DateTimeInfo.UTCTimeSince1601 = dateTimeInfoNVS["UTCTimeSince1601"].VC.GetValue<double>(false);
                DateTimeInfo.TimeZoneOffset = dateTimeInfoNVS["TimeZoneOffset"].VC.GetValue<Int32>(false);
                DateTimeInfo.DSTIsActive = dateTimeInfoNVS["DSTIsActive"].VC.GetValue<bool>(false);
                DateTimeInfo.DSTBias = dateTimeInfoNVS["DSTBias"].VC.GetValue<Int32>(false);
                DateTimeInfo.TZName0 = dateTimeInfoNVS["TZName0"].VC.GetValue<string>(false).MapNullToEmpty();
                DateTimeInfo.TZName1 = dateTimeInfoNVS["TZName1"].VC.GetValue<string>(false).MapNullToEmpty();
            }

            // attempt to decode the FileIndexInfo
            AttemptToDecodeFileIndexDbbAndGenerateFileIndexInfo(fileIndexDbb, ref ec);

            return ec;
        }

        private void AttemptToDecodeFileIndexDbbAndGenerateFileIndexInfo(DataBlockBuffer fileIndexDbb, ref string ec)
        {
            int decodeIndex = 0;
            UInt32 numRows = 0, rowSizeDivisor = 0;

            if (!Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 0, out numRows)
                || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 4, out rowSizeDivisor))
            {
                ec = ec.MapNullOrEmptyTo("ReadFileAndHeader failed:  Could not extract file index from {0}".CheckedFormat(fileIndexDbb.FixedBlockTypeID));
            }

            FileIndexInfo = new Common.FileIndexInfo((Int32)numRows)
            {
                RowSizeDivisor = (Int32)rowSizeDivisor,
            };

            decodeIndex += FileIndexInfo.SerializedSize;

            UInt32 fileOffsetU4 = 0;                // 0..3
            UInt32 blockTotalLengthU4 = 0;          // 4..7
            UInt32 blockTypeID32U4 = 0;             // 8..11
            UInt64 blockDeltaTimeStampU8 = 0;       // 12..19

            if (!Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 0, out fileOffsetU4)
                || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 4, out blockTotalLengthU4)
                || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 8, out blockTypeID32U4)
                || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 12, out blockDeltaTimeStampU8)
                )
            {
                ec = ec.MapNullOrEmptyTo("ReadFileAndHeader failed:  Could not extract last file block info from {0}".CheckedFormat(fileIndexDbb.FixedBlockTypeID));
            }

            FileIndexLastBlockInfoBase lastBlockInfo = FileIndexInfo.LastBlockInfo;
            lastBlockInfo.FileOffsetToStartOfBlock = unchecked((Int32)fileOffsetU4);
            lastBlockInfo.BlockTotalLength = unchecked((Int32)blockTotalLengthU4);
            lastBlockInfo.BlockTypeID32 = unchecked((Int32)blockTypeID32U4);
            lastBlockInfo.BlockDeltaTimeStamp = blockDeltaTimeStampU8.CastToF8();

            decodeIndex += FileIndexLastBlockInfoBase.SerializedSize;

            foreach (FileIndexRowBase fileIndexRow in FileIndexInfo.FileIndexRowArray)
            {
                UInt32 fileIndexRowFlagBitsU4 = 0;          // 0..3
                UInt32 fileOffsetOfFirstBlockU4 = 0;        // 4..7
                UInt64 fileIndexUserRowFlagBitsU4 = 0;      // 8..15
                UInt64 firstBlockUtcTimeSince1601U8 = 0;    // 16..23
                UInt64 firstBlockDeltaTimeStampU8 = 0;      // 24..31
                UInt64 lastBlockDeltaTimeStampU8 = 0;       // 32..39

                if (!Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 0, out fileIndexRowFlagBitsU4)
                    || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 4, out fileOffsetOfFirstBlockU4)
                    || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 8, out fileIndexUserRowFlagBitsU4)
                    || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 16, out firstBlockUtcTimeSince1601U8)
                    || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 24, out firstBlockDeltaTimeStampU8)
                    || !Utils.Data.Pack(fileIndexDbb.payloadDataArray, decodeIndex + 32, out lastBlockDeltaTimeStampU8)
                    )
                {
                    ec = ec.MapNullOrEmptyTo("ReadFileAndHeader failed:  Could not extract file index row from {0}".CheckedFormat(fileIndexDbb.FixedBlockTypeID));
                }

                fileIndexRow.FileIndexRowFlagBitsU4 = fileIndexRowFlagBitsU4;
                fileIndexRow.FileOffsetToStartOfFirstBlock = unchecked((Int32)fileOffsetOfFirstBlockU4);
                fileIndexRow.FileIndexUserRowFlagBits = fileIndexUserRowFlagBitsU4;
                fileIndexRow.FirstBlockUtcTimeSince1601 = firstBlockUtcTimeSince1601U8.CastToF8();
                fileIndexRow.FirstBlockDeltaTimeStamp = firstBlockDeltaTimeStampU8.CastToF8();
                fileIndexRow.LastBlockDeltaTimeStamp = lastBlockDeltaTimeStampU8.CastToF8();

                decodeIndex += FileIndexRowBase.SerializedSize;
            }
        }

        private string ReadMetaDataBlocks()
        {
            List<DataBlockBuffer> mdBlockList = new List<DataBlockBuffer>();

            DataBlockBuffer dbb = new DataBlockBuffer();

            // keep reading blocks (immediately after the end of the 3 file header blocks).
            // accumulate all of the MetaData blocks until we find the first non-mdd block, or we reach the end of file.

            for (; ; )
            {
                if (fileScanOffset >= FileLength)
                    break;

                int tempOffset = fileScanOffset;

                dbb = ReadDataBlockAndHandleTimeStampUpdates(ref tempOffset, requirePayloadBytes: false);       // read and decode only the header to see if it is a MetaData block

                if (dbb.IsValid && dbb.FixedBlockTypeID == FixedBlockTypeID.MetaDataV1 && !dbb.HasPayloadBytes)
                    PopulateDataBlockBufferIfNeeded(dbb);

                if (!dbb.IsValid || dbb.FixedBlockTypeID != FixedBlockTypeID.MetaDataV1)
                    break;

                mdBlockList.Add(dbb);
                fileScanOffset = tempOffset;
            }

            // process the dbb blocks that we have.
            // each md dbb contains one or more INamedValueSet instances coded as E005Data
            // generate a list of these and also attempt to decode them into groups, groupPoints, and occurrences

            string resultCode = dbb.resultCode;
            List<INamedValueSet> mddNVSList = new List<INamedValueSet>();
            List<LocalGroupInfo> giList = new List<LocalGroupInfo>();
            List<LocalGroupPointInfo> gpiList = new List<LocalGroupPointInfo>();
            List<OccurrenceInfo> oiList = new List<OccurrenceInfo>();
            Dictionary<string, IGroupInfo> giDictionary = new Dictionary<string, IGroupInfo>();
            Dictionary<string, IGroupPointInfo> gpiDictionary = new Dictionary<string, IGroupPointInfo>();
            Dictionary<string, IOccurrenceInfo> oiDictionary = new Dictionary<string, IOccurrenceInfo>();
            Dictionary<int, IMetaDataCommonInfo> fileIDtoMDCommonDictionary = new Dictionary<int,IMetaDataCommonInfo>();

            foreach (DataBlockBuffer dbbItem in mdBlockList)
            {
                // the payload is always just a sequence of INamedValueSets

                int scanOffset = 0;
                string ec = string.Empty;

                while (scanOffset < dbbItem.payloadLength)
                {
                    NamedValueSet nvs = new NamedValueSet().ConvertFromE005Data(dbbItem.payloadDataArray, ref scanOffset, ref ec);

                    if (!ec.IsNullOrEmpty())
                        break;

                    mddNVSList.Add(nvs);

                    INamedValue itemTypeNV = nvs["ItemType"];
                    if (itemTypeNV.IsNullOrEmpty())
                        itemTypeNV = nvs["ItemTypeI4"];

                    MDItemType mdItemType = itemTypeNV.VC.GetValue<MDItemType>(false);

                    MetaDataCommonInfoBase mdci = new MetaDataCommonInfoBase()
                    {
                        ItemType = mdItemType,
                        Name = nvs["Name"].VC.GetValue<string>(false).MapNullToEmpty(),
                        Comment = nvs["Comment"].VC.GetValue<string>(false),
                        FileID = nvs["FileID"].VC.GetValue<int>(false),
                        ClientID = nvs["ClientID"].VC.GetValue<int>(false),
                        IFC = nvs["IFC"].VC.GetValue<Semi.E005.Data.ItemFormatCode>(false),
                        ClientNVS = new NamedValueSet(nvs.Where<INamedValue>(inv => inv.Name.StartsWith("c.")).Select<INamedValue, INamedValue>(inv => new NamedValue(inv.Name.Substring(2), inv.VC))).ConvertToReadOnly(),
                    };

                    switch (mdItemType)
                    {
                        case MDItemType.Source:
                            {
                                LocalGroupPointInfo gpi = new LocalGroupPointInfo(mdci)
                                {
                                    ValueCST = mdci.IFC.ConvertToContainerStorageType(),
                                    IVA = (IVI != null) ? IVI.GetValueAccessor(mdci.Name) : null,
                                };
                                gpiList.Add(gpi);
                                gpiDictionary[gpi.Name] = gpi;
                                fileIDtoMDCommonDictionary[gpi.FileID] = gpi;
                            }
                            break;
                        case MDItemType.Group:
                            {
                                LocalGroupInfo gi = new LocalGroupInfo(mdci)
                                {
                                    FileIndexUserRowFlagBits = nvs["FileIndexUserRowFlagBits"].VC.GetValue<UInt64>(false),
                                    GroupPointFileIDList = new ReadOnlyIList<int>(nvs["ItemList"].VC.GetValue<string>(false).MapNullToEmpty().Split(',').Select(s => new StringScanner(s).ParseValue<int>(0)).Where(clientID => clientID > 0)),
                                };
                                giList.Add(gi);
                                giDictionary[gi.Name] = gi;
                                fileIDtoMDCommonDictionary[gi.FileID] = gi;
                            }
                            break;
                        case MDItemType.Occurrence:
                            {
                                OccurrenceInfo oi = new OccurrenceInfo(mdci)
                                {
                                    FileIndexUserRowFlagBits = nvs["FileIndexUserRowFlagBits"].VC.GetValue<UInt64>(false),
                                };
                                oiList.Add(oi);
                                oiDictionary[oi.Name] = oi;
                                fileIDtoMDCommonDictionary[oi.FileID] = oi;
                            }
                            break;
                        default:
                            break;
                    }
                }

                resultCode = resultCode.MapNullOrEmptyTo(ec);
            }

            LocalGroupInfo[] giArray = giList.ToArray();

            foreach (LocalGroupInfo gi in giArray)
            {
                LocalGroupPointInfo [] gpiArray = gi.GroupPointFileIDList.Select(
                                linkedGroupPointFileID => 
                                {
                                    IMetaDataCommonInfo mddci = null;
                                    fileIDtoMDCommonDictionary.TryGetValue(linkedGroupPointFileID, out mddci);
                                    return mddci as LocalGroupPointInfo;
                                }).Where(gpi => gpi != null).ToArray();

                gi.GroupPointInfoList = new ReadOnlyIList<IGroupPointInfo>(gpiArray);
                gi.GroupPointInfoArray = gpiArray;
                gi.GroupPointIVAArray = gpiArray.Select(gpi => gpi.IVA).WhereIsNotDefault().ToArray();
            }

            MetaDataArray = mddNVSList.ToArray();

            GroupInfoArray = giArray;
            GroupPointInfoArray = gpiList.ToArray();
            OccurrenceInfoArray = oiList.ToArray();
            GroupPointIVAArray = gpiList.Select(gpi => gpi.IVA).WhereIsNotDefault().ToArray();

            GroupInfoDictionary = giDictionary;
            GroupPointInfoDictionary = gpiDictionary;
            OccurrenceInfoDictionary = oiDictionary;
            FileIDToMDCommonDictinoary = fileIDtoMDCommonDictionary;

            return resultCode;
        }

        private class LocalGroupPointInfo : GroupPointInfo
        {
            public IValueAccessor IVA { get; set; }

            public LocalGroupPointInfo(MetaDataCommonInfoBase other) : base(other) { }
        }

        private class LocalGroupInfo : GroupInfo
        {
            public IValueAccessor[] GroupPointIVAArray { get; set; }

            public LocalGroupInfo(MetaDataCommonInfoBase other) : base(other) { }
        }

        #endregion

        #region Content reading interface (ReadAndProcessContents, ReloadFileIndexInfo)

        public string LoadFileIndexInfo()
        {
            DataBlockBuffer fileIndexDbb;
            return LoadFileIndexInfo(out fileIndexDbb, attemptToDecodeFileIndex: true);
        }

        public string LoadFileIndexInfo(out DataBlockBuffer fileIndexDbb, bool attemptToDecodeFileIndex)
        {
            string ec = string.Empty;
            int tempFileIndexBlockStartOffsetOut = fileIndexBlockStartOffset;

            fileIndexDbb = ReadDataBlockAndHandleTimeStampUpdates(ref tempFileIndexBlockStartOffsetOut);

            if (enableLiveFileHeuristics)
            {
                for (int tryCount = 1; ; tryCount++)
                {
                    System.Threading.Thread.Sleep(10);

                    tempFileIndexBlockStartOffsetOut = fileIndexBlockStartOffset;
                    DataBlockBuffer fileIndexDbb2nd = ReadDataBlockAndHandleTimeStampUpdates(ref tempFileIndexBlockStartOffsetOut);

                    if (fileIndexDbb.Equals(fileIndexDbb2nd))
                        break;

                    fileIndexDbb = fileIndexDbb2nd;

                    if (tryCount > 10)
                        return "{0} failed: could not obtain consistant file index block contents after {1} tries".CheckedFormat(Fcns.CurrentMethodName, tryCount);
                }
            }

            if (!fileIndexDbb.IsValid)
                ec = fileIndexDbb.resultCode;

            if (fileIndexDbb.FixedBlockTypeID != FixedBlockTypeID.FileIndexV1)
                return "{0} failed: found unexpected block type {0}".CheckedFormat(Fcns.CurrentMethodName, fileIndexDbb.FixedBlockTypeID);

            if (ec.IsNullOrEmpty() && attemptToDecodeFileIndex)
                AttemptToDecodeFileIndexDbbAndGenerateFileIndexInfo(fileIndexDbb, ref ec);

            return ec;
        }

        private int lastReadAndProcessFileLength = 0;

        private IGroupInfo[] FilteredGroupInfoArray { get; set; }
        private FileIndexRowBase currentScanRow = null;
        private FileIndexRowBase nextScanRow = null;

        public void ReadAndProcessContents(ReadAndProcessFilterSpec filterSpec)
        {
            filterSpec = new ReadAndProcessFilterSpec(filterSpec);      // make a clone of the given value so that the caller can re-use, and changed, there copy while we use ours.

            FileIndexRowBase[] filteredFileIndexRowArray = filterSpec.UpdateFilterSpecAndGenerateFilteredFileIndexRows(DateTimeInfo, FileIndexInfo);
            int numFilteredFileIndexRows = filteredFileIndexRowArray.SafeLength();

            FilteredGroupInfoArray = GroupInfoArray.Where(gi => filterSpec.GroupFilterDelegate(gi)).ToArray();

            ResetIVAs();

            lastDbbTimeStamp = Double.NegativeInfinity;
            lastGroupOrTSUpdateEventTimeStamp = Double.NegativeInfinity;

            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.ReadingStart });

            UpdateFileLength();

            if (lastReadAndProcessFileLength == 0)
            {
                lastReadAndProcessFileLength = FileLength;
            }
            else if (lastReadAndProcessFileLength != FileLength)
            {
                LoadFileIndexInfo();

                lastReadAndProcessFileLength = FileLength;

                SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.FileLengthChanged });
            }

            bool isFirstBlock = true;

            int currentFilteredScanRowIndex = 0;
            currentScanRow = filteredFileIndexRowArray.SafeAccess(currentFilteredScanRowIndex);
            nextScanRow = filteredFileIndexRowArray.SafeAccess(currentFilteredScanRowIndex + 1);

            if (currentScanRow != null)
            {
                SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.RowStart, Row = currentScanRow, FileDeltaTimeStamp = currentScanRow.FirstBlockDeltaTimeStamp });

                for (; ; )
                {
                    DataBlockBuffer dbb;
                    if (isFirstBlock)
                    {
                        dbb = ReadFirstDataBlockUsingFileIndex(currentScanRow, requirePayloadBytes: false);
                        isFirstBlock = false;
                    }
                    else
                    {
                        dbb = ReadNextDataBlock(requirePayloadBytes: false);
                    }

                    if (dbb == null || !dbb.IsValid || !dbb.DoesFilterAcceptThis(filterSpec, checkIfComesAfterStartOfFilterPeriod: false, checkIfComesBeforeEndOfFilterPeriod: true))
                        break;

                    if (lastDbbTimeStamp != dbb.fileDeltaTimeStamp)
                    {
                        lastDbbTimeStamp = dbb.fileDeltaTimeStamp;

                        if ((dbb.fileDeltaTimeStamp >= filterSpec.FirstFileDeltaTimeStamp) 
                            && (dbb.fileOffsetToStartOfBlock >= filterSpec.FirstEventBlockStartOffset) 
                            && ((dbb.fileDeltaTimeStamp - lastGroupOrTSUpdateEventTimeStamp) >= filterSpec.NominalMinimumGroupAndTimeStampUpdateInterval))
                        {
                            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.NewTimeStamp, DataBlockBuffer = LastTimeStampUpdateDBB });
                            lastGroupOrTSUpdateEventTimeStamp = dbb.fileDeltaTimeStamp;
                        }
                    }

                    ProcessContents(dbb, filterSpec);

                    // "advance" to the next scan row.  This logic is only used to keep the currentScanRow current as much as possible.  
                    // Note that this logic does not skip regions of the file itself and this logic does not stop processing if the reader runs off the end of the index table while reading.  
                    // That only happens on an error (from ReadNextDataBlock/ReadFirstDataBlockUsingFileIndex) or 

                    if (nextScanRow != null && fileScanOffset >= nextScanRow.FileOffsetToStartOfFirstBlock)
                    {
                        SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.RowEnd, Row = currentScanRow, FileDeltaTimeStamp = currentScanRow.LastBlockDeltaTimeStamp });

                        bool currentAndNextRowsAreNotContiguous = (nextScanRow.RowIndex != currentScanRow.RowIndex + 1);

                        currentFilteredScanRowIndex++;
                        currentScanRow = nextScanRow;
                        nextScanRow = filteredFileIndexRowArray.SafeAccess(currentFilteredScanRowIndex + 1);

                        if (currentAndNextRowsAreNotContiguous)
                            fileScanOffset = currentScanRow.FileOffsetToStartOfFirstBlock;

                        SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.RowStart, Row = currentScanRow, FileDeltaTimeStamp = currentScanRow.LastBlockDeltaTimeStamp });
                    }

                    if (fileScanOffset >= FileLength)
                        break;
                }
            }

            // signal the RowEnd of the currentScanRow if it is not null.
            if (currentScanRow != null)
                SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.RowEnd, Row = currentScanRow, FileDeltaTimeStamp = currentScanRow.LastBlockDeltaTimeStamp });

            // signal the end of the file
            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.ReadingEnd, FileDeltaTimeStamp = lastDbbTimeStamp });
        }

        private double lastDbbTimeStamp;
        private double lastGroupOrTSUpdateEventTimeStamp;

        private void ResetIVAs()
        {
            foreach (IValueAccessor iva in GroupPointIVAArray ?? emptyIVAArray)
            {
                if (iva != null)
                    iva.Reset();
            }
        }

        private static IValueAccessor[] emptyIVAArray = EmptyArrayFactory<IValueAccessor>.Instance;

        private ProcessContentEvent lastEventPCE = ProcessContentEvent.None;
        private FixedBlockTypeID lastBlockTypeID = FixedBlockTypeID.None;

        private void SignalEventIfEnabled(ReadAndProcessFilterSpec filterSpec, ProcessContentEventData eventData)
        {
            eventData.Reader = this;
            eventData.FilePath = FilePath;

            if (eventData.Row == null)
                eventData.Row = currentScanRow;

            // generate the DateTime value
            if (currentScanRow == null || currentScanRow.IsEmpty || eventData.FileDeltaTimeStamp < currentScanRow.FirstBlockDeltaTimeStamp)
            {
                // There is no current row (or it is empty, or the current delta timestamp comes from before the current row): interpolate from the first DateTime stamp in the file
                eventData.UTCDateTime = DateTimeInfo.UTCDateTime + TimeSpan.FromSeconds(Math.Max(0.0, eventData.FileDeltaTimeStamp));
            }
            else if (nextScanRow == null || nextScanRow.IsEmpty)
            {
                // There is no next row (or it is empty): interpolate from the first DateTime stamp in the current row
                eventData.UTCDateTime = currentScanRow.FirstBlockDateTime + TimeSpan.FromSeconds(eventData.FileDeltaTimeStamp - currentScanRow.FirstBlockDeltaTimeStamp);
            }
            else if (eventData.FileDeltaTimeStamp >= nextScanRow.FirstBlockDeltaTimeStamp || currentScanRow.FirstBlockDeltaTimeStamp >= nextScanRow.FirstBlockDeltaTimeStamp)
            {
                // The delta timestamp is beyond the beginning of the next row: interpolate from the first DateTime stamp in the next row
                eventData.UTCDateTime = nextScanRow.FirstBlockDateTime + TimeSpan.FromSeconds(eventData.FileDeltaTimeStamp - nextScanRow.FirstBlockDeltaTimeStamp);
            }
            else
            {
                // the delta timestamp is between the current row and the next row.  use time interpolation between them
                double denominator = (nextScanRow.FirstBlockDeltaTimeStamp - currentScanRow.FirstBlockDeltaTimeStamp);
                double unitSpan = (denominator > 0.0 ? ((eventData.FileDeltaTimeStamp - currentScanRow.FirstBlockDeltaTimeStamp) / denominator) : 0.0);
                eventData.UTCDateTime = currentScanRow.FirstBlockDateTime + TimeSpan.FromSeconds((nextScanRow.FirstBlockDateTime - currentScanRow.FirstBlockDateTime).TotalSeconds * unitSpan);
            }

            ProcessContentEvent pce = eventData.PCE;

            // if this event is a not a RowStart or a RowEnd (which are synthetic events) then look to see if this event marks the beginning of a groupset or the end of a group set set
            if ((pce & (ProcessContentEvent.RowStart | ProcessContentEvent.RowEnd)) == 0)
            {
                // detect if we need to generate a GroupSetStart or GroupSetEnd event before emitting the current one...  RowStart and RowEnd events are ignored in relation to this work as they are not actually in the file format

                bool lastWasGroupOrGroupSetStart = ((lastEventPCE & ProcessContentEvent.Group) != 0 || lastEventPCE == ProcessContentEvent.GroupSetStart);
                bool currentIsGroupOrGroupSetStart = ((pce & ProcessContentEvent.Group) != 0 || pce == ProcessContentEvent.GroupSetStart);
                if (lastWasGroupOrGroupSetStart && !currentIsGroupOrGroupSetStart && (filterSpec.PCEMask & ProcessContentEvent.GroupSetEnd) != 0)
                {
                    filterSpec.EventHandlerDelegate(this, new ProcessContentEventData(eventData) { PCE = ProcessContentEvent.GroupSetEnd, GroupInfoArray = GroupInfoArray, FilteredGroupInfoArray = FilteredGroupInfoArray });
                }
                else if (!lastWasGroupOrGroupSetStart && currentIsGroupOrGroupSetStart && (filterSpec.PCEMask & ProcessContentEvent.GroupSetStart) != 0)
                {
                    filterSpec.EventHandlerDelegate(this, new ProcessContentEventData(eventData) { PCE = ProcessContentEvent.GroupSetStart, GroupInfoArray = GroupInfoArray, FilteredGroupInfoArray = FilteredGroupInfoArray });
                }

                lastEventPCE = pce;
            }

            if ((filterSpec.PCEMask & pce) != 0)
                filterSpec.EventHandlerDelegate(this, eventData);
        }

        private void ProcessContents(DataBlockBuffer dbb, ReadAndProcessFilterSpec filterSpec)
        {
            if (!dbb.DoesFilterAcceptThis(filterSpec, checkIfComesAfterStartOfFilterPeriod: !filterSpec.AutoRewindToPriorFullGroupRow, checkIfComesBeforeEndOfFilterPeriod: true))
                return;

            switch (dbb.FixedBlockTypeID)
            {
                case FixedBlockTypeID.ErrorV1:
                case FixedBlockTypeID.MessageV1:
                    if ((filterSpec.PCEMask & (ProcessContentEvent.Message | ProcessContentEvent.Error)) != 0)
                    {
                        PopulateDataBlockBufferIfNeeded(dbb);
                        ProcessMessageOrErrorBlock(dbb, filterSpec);
                    }
                    return;
                case FixedBlockTypeID.MetaDataV1:
                case FixedBlockTypeID.FileHeaderV1:
                case FixedBlockTypeID.FileIndexV1:
                case FixedBlockTypeID.DateTimeV1:
                case FixedBlockTypeID.FileEndV1:
                    // There is no direct PCE event for each of these.
                    return;
                case FixedBlockTypeID.TimeStampUpdateV1:
                    // NewTimeStamp events are synthesized/processed directly in ReadAndProcessContents
                    return;

                default:
                    break;
            }

            IMetaDataCommonInfo mdci;
            if (!FileIDToMDCommonDictinoary.TryGetValue(dbb.blockTypeID32, out mdci) || mdci == null)
                return;

            switch (mdci.ItemType)
            {
                case MDItemType.Occurrence:
                    {
                        IOccurrenceInfo oi = mdci as IOccurrenceInfo;

                        if (oi != null
                            && filterSpec.OccurrenceFilterDelegate(oi) 
                            && (filterSpec.PCEMask & ProcessContentEvent.Occurrence) != 0
                            )
                        {
                            PopulateDataBlockBufferIfNeeded(dbb);
                            ProcessOccurrenceBlock(oi, dbb, filterSpec);
                        }
                    }
                    break;
                case MDItemType.Group:
                    {
                        IGroupInfo gi = mdci as IGroupInfo;

                        // only filter on coming before end of filter period if 
                        if (gi != null
                            && filterSpec.GroupFilterDelegate(gi) 
                            && (filterSpec.PCEMask & (ProcessContentEvent.Group | ProcessContentEvent.EmptyGroup | ProcessContentEvent.PartialGroup | ProcessContentEvent.StartOfFullGroup | ProcessContentEvent.GroupSetStart | ProcessContentEvent.GroupSetEnd)) != 0
                            )
                        {
                            PopulateDataBlockBufferIfNeeded(dbb);
                            ProcessGroupBlock(gi, dbb, filterSpec);
                        }
                    }
                    break;
                default:
                    break;
            }

            lastBlockTypeID = dbb.FixedBlockTypeID;
        }

        private void ProcessMessageOrErrorBlock(DataBlockBuffer dbb, ReadAndProcessFilterSpec filterSpec)
        {
            int decodeIndex = 0;

            UInt64 messageRecordedUtcTimeU8 = 0;
            ValueContainer messageVC = ValueContainer.Empty;

            UInt64 seqNumU8 = dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode);

            if (!dbb.IsValid || !Utils.Data.Pack(dbb.payloadDataArray, decodeIndex + 0, out messageRecordedUtcTimeU8))
                return;

            decodeIndex += 8;

            messageVC = dbb.payloadDataArray.DecodeE005Data(ref decodeIndex, ref dbb.resultCode);
            if (!dbb.IsValid)
                return;

            IMessageInfo messageInfo = new MessageInfo()
            {
                FixedBlockTypeID = dbb.FixedBlockTypeID,
                FileDeltaTimeStamp = dbb.fileDeltaTimeStamp,
                MessageRecordedUtcTime = messageRecordedUtcTimeU8.CastToF8(),
                Message = messageVC.GetValue<string>(false),
            };

            if (dbb.FixedBlockTypeID == FixedBlockTypeID.ErrorV1)
                SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.Error, MessageInfo = messageInfo, SeqNum = seqNumU8, DataBlockBuffer = dbb });
            else
                SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.Message, MessageInfo = messageInfo, SeqNum = seqNumU8, DataBlockBuffer = dbb });
        }

        private void ProcessOccurrenceBlock(IOccurrenceInfo oi, DataBlockBuffer dbb, ReadAndProcessFilterSpec filterSpec)
        {
            int decodeIndex = 0;
            UInt64 seqNumU8 = 0;

            seqNumU8 = dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode);
            if (!dbb.IsValid)
                return;

            ValueContainer occurrenceVC = ValueContainer.Empty;

            occurrenceVC = dbb.payloadDataArray.DecodeE005Data(ref decodeIndex, ref dbb.resultCode);
            if (!dbb.IsValid)
                return;

            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.Occurrence, OccurrenceInfo = oi, SeqNum = seqNumU8, VC = occurrenceVC, DataBlockBuffer = dbb });
        }

        private void ProcessGroupBlock(IGroupInfo gi, DataBlockBuffer dbb, ReadAndProcessFilterSpec filterSpec)
        {
            gi.Touched = true;

            LocalGroupInfo lgi = gi as LocalGroupInfo;

            int decodeIndex = 0;
            UInt64 seqNumU8 = 0;

            seqNumU8 = dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode);
            if (!dbb.IsValid)
                return;

            bool startReached = (dbb.fileDeltaTimeStamp >= filterSpec.FirstFileDeltaTimeStamp);
            bool intervalEnabled = (filterSpec.NominalMinimumGroupAndTimeStampUpdateInterval != 0.0);
            bool intervalReached = (startReached && (!intervalEnabled || (dbb.fileDeltaTimeStamp - lastGroupOrTSUpdateEventTimeStamp) >= filterSpec.NominalMinimumGroupAndTimeStampUpdateInterval));
            if (intervalReached)
                lastGroupOrTSUpdateEventTimeStamp = dbb.fileDeltaTimeStamp;

            if (decodeIndex == dbb.payloadLength)
            {
                if (startReached && intervalReached)
                    SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = (ProcessContentEvent.Group | ProcessContentEvent.EmptyGroup), GroupInfo = gi, SeqNum = seqNumU8, DataBlockBuffer = dbb });

                return;
            }
            
            GroupBlockFlagBits groupBlockFlagBits = unchecked((GroupBlockFlagBits) (dbb.payloadDataArray.SafeAccess(decodeIndex++)));
            int numPoints = gi.GroupPointInfoList.Count;

            if ((groupBlockFlagBits & GroupBlockFlagBits.HasUpdateMask) == GroupBlockFlagBits.None)
            {
                // this is not a sparse group - it also might be a StartOfFullGroup group

                int ivaSetPendingCount = 0;

                for (int gpiIndex = 0; gpiIndex < numPoints; gpiIndex++)
                {
                    IGroupPointInfo gpi = gi.GroupPointInfoList[gpiIndex];
                    IValueAccessor iva = (lgi != null ? lgi.GroupPointIVAArray[gpiIndex] : null);

                    gpi.VC = DecodeGroupPointValue(gpi, dbb, ref decodeIndex);
                    if (!dbb.IsValid)
                        return;

                    if (iva != null)
                    {
                        iva.VC = gpi.VC;
                        if (iva.IsSetPending)
                            ivaSetPendingCount++;
                    }
                }

                if (ivaSetPendingCount > 0)
                    IVI.Set(GroupPointIVAArray);

                if (startReached)
                {
                    if ((groupBlockFlagBits & GroupBlockFlagBits.IsStartOfFullGroup) != 0)
                    {
                        SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = (ProcessContentEvent.Group | ProcessContentEvent.StartOfFullGroup), GroupInfo = gi, SeqNum = seqNumU8, DataBlockBuffer = dbb });
                        lastGroupOrTSUpdateEventTimeStamp = dbb.fileDeltaTimeStamp;
                    }
                    else if (intervalReached)
                    {
                        SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = (ProcessContentEvent.Group), GroupInfo = gi, SeqNum = seqNumU8, DataBlockBuffer = dbb });
                    }
                }
            }
            else
            {
                // this is an update (sparse) group

                int bitIndexOffset = (decodeIndex << 3);    // we can directly index the payloadDataArray as a bit vector if we shift the bitIndexs up to skip over the seqNum (U8Auto) and GroupBlockFlagBits (byte).

                decodeIndex += ((numPoints + 7) >> 3);

                int ivaSetPendingCount = 0;

                for (int gpiIndex = 0; gpiIndex < numPoints; gpiIndex++)
                {
                    if (dbb.payloadDataArray.GetBit(gpiIndex + bitIndexOffset))
                    {
                        IGroupPointInfo gpi = gi.GroupPointInfoList[gpiIndex];
                        IValueAccessor iva = (lgi != null ? lgi.GroupPointIVAArray[gpiIndex] : null);

                        ValueContainer pointVC = DecodeGroupPointValue(gpi, dbb, ref decodeIndex);

                        if (!dbb.IsValid)
                            return;

                        gpi.VC = pointVC;

                        if (iva != null)
                        {
                            iva.VC = pointVC;
                            if (iva.IsSetPending)
                                ivaSetPendingCount ++;
                        }
                    }
                }

                if (ivaSetPendingCount > 0)
                    IVI.Set(GroupPointIVAArray);

                if (startReached && intervalReached)
                    SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = (ProcessContentEvent.Group | ProcessContentEvent.PartialGroup), GroupInfo = gi, SeqNum = seqNumU8, DataBlockBuffer = dbb });
            }
        }

        private ValueContainer DecodeGroupPointValue(IGroupPointInfo gpi, DataBlockBuffer dbb, ref int decodeIndex)
        {
            ValueContainer vc = ValueContainer.Empty;

            if (decodeIndex > dbb.payloadLength)
                dbb.resultCode = "{0} failed at point '{1}': buffer is already empty".CheckedFormat(Fcns.CurrentMethodName, gpi.Name);

            if (dbb.IsValid)
            {
                switch (gpi.IFC)
                {
                    case ItemFormatCode.Bo: vc.SetValue(dbb.payloadDataArray.SafeAccess(decodeIndex++) != 0); break;
                    case ItemFormatCode.Bi: vc.SetValue(dbb.payloadDataArray.SafeAccess(decodeIndex++), ContainerStorageType.Binary, false); break;
                    case ItemFormatCode.I1: vc.SetValue(unchecked((sbyte) dbb.payloadDataArray.SafeAccess(decodeIndex++))); break;
                    case ItemFormatCode.I2: vc.SetValue(SetupInfo.ConvertToI2FromU2WithOffset(unchecked((UInt16) dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode)))); break;
                    case ItemFormatCode.I4: vc.SetValue(SetupInfo.ConvertToI4FromU4WithOffset(unchecked((UInt32) dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode)))); break;
                    case ItemFormatCode.I8: vc.SetValue(SetupInfo.ConvertToI8FromU8WithOffset(dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode))); break;
                    case ItemFormatCode.U1: vc.SetValue(dbb.payloadDataArray.SafeAccess(decodeIndex++)); break;
                    case ItemFormatCode.U2: vc.SetValue(unchecked((UInt16)dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode))); break;
                    case ItemFormatCode.U4: vc.SetValue(unchecked((UInt32)dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode))); break;
                    case ItemFormatCode.U8: vc.SetValue(dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode)); break;
                    case ItemFormatCode.F4: vc.SetValue(Utils.Data.Pack4(dbb.payloadDataArray, decodeIndex).CastToF4()); decodeIndex += 4; break;
                    case ItemFormatCode.F8: vc.SetValue(Utils.Data.Pack8(dbb.payloadDataArray, decodeIndex).CastToF8()); decodeIndex += 8; break;
                    default: vc = dbb.payloadDataArray.DecodeE005Data(ref decodeIndex, ref dbb.resultCode); break;
                }
            }

            if (decodeIndex > dbb.payloadLength)
                dbb.resultCode = "{0} failed at point '{1}': ran off end of buffer".CheckedFormat(Fcns.CurrentMethodName, gpi.Name);

            return vc;
        }

        #endregion

        #region public ReadNextDataBlock, ReadFirstDataBlockUsingFileIndex, PopulateDataBlockBufferIfNeeded

        public DataBlockBuffer ReadNextDataBlock(bool requirePayloadBytes = true)
        {
            return ReadDataBlockAndHandleTimeStampUpdates(ref fileScanOffset, requirePayloadBytes);
        }

        public DataBlockBuffer ReadFirstDataBlockUsingFileIndex(FileIndexRowBase row, bool requirePayloadBytes = true)
        {
            if (SetupInfo == null || FileIndexInfo == null)
                return new DataBlockBuffer() { resultCode = "{0} failed: SetupInfo and/or FileIndexInfo are null".CheckedFormat(Fcns.CurrentMethodName) };

            if (row == null || row.IsEmpty)
                return new DataBlockBuffer() { resultCode = "{0} failed: FileIndexInfo does not contain a valid and non-empty row at {0}".CheckedFormat(Fcns.CurrentMethodName, (row != null ? row.RowIndex : 0)) };

            currentBlockDeltaTimeStamp = row.FirstBlockDeltaTimeStamp;
            fileScanOffset = row.FileOffsetToStartOfFirstBlock;

            return ReadDataBlockAndHandleTimeStampUpdates(ref fileScanOffset, requirePayloadBytes);
        }

        public DataBlockBuffer ReadLastDataBlockUsingFileIndex(bool requirePayloadBytes = true)
        {
            if (SetupInfo == null || FileIndexInfo == null)
                return new DataBlockBuffer() { resultCode = "{0} failed: SetupInfo and/or FileIndexInfo are null".CheckedFormat(Fcns.CurrentMethodName) };

            currentBlockDeltaTimeStamp = FileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp;
            fileScanOffset = FileIndexInfo.LastBlockInfo.FileOffsetToStartOfBlock;

            return ReadDataBlockAndHandleTimeStampUpdates(ref fileScanOffset, requirePayloadBytes);
        }

        public bool PopulateDataBlockBufferIfNeeded(DataBlockBuffer dbb)
        {
            if (!dbb.IsValid)
                return false;

            if (dbb.HasPayloadBytes)
                return true;

            int fileOffsetToStartOfPayload = (dbb.fileOffsetToStartOfBlock + dbb.headerLength);
            int payloadStartIndex;

            dbb.resultCode = RequestThatCacheContains(fileOffsetToStartOfPayload, dbb.payloadLength, out payloadStartIndex);
            if (dbb.IsValid)
            {
                dbb.payloadDataArray = new byte[dbb.payloadLength];
                Array.Copy(cache.byteArray, payloadStartIndex, dbb.payloadDataArray, 0, dbb.payloadLength);
                dbb.HasPayloadBytes = true;
            }

            return dbb.IsValid;
        }

        /// <summary>
        /// Reads the data block at the given offset in the file.
        /// On successfull block read, if advanceOffsetOnSuccess is true, advances the given offset by the resulting data block's total length.
        /// </summary>
        private DataBlockBuffer ReadDataBlockAndHandleTimeStampUpdates(ref int atOffsetInFile, bool requirePayloadBytes = true)
        {
            DataBlockBuffer dbb = InnerReadDataBlock(ref atOffsetInFile, requirePayloadBytes: requirePayloadBytes);

            if (dbb.IsValid && dbb.FixedBlockTypeID == FixedBlockTypeID.TimeStampUpdateV1)
            {
                if (!dbb.HasPayloadBytes)
                    PopulateDataBlockBufferIfNeeded(dbb);

                if (dbb.IsValid)
                {
                    LastTimeStampUpdateDBB = dbb;

                    int decodeIndex = 0;
                    UInt64 decodedBlockDeltaTimeStampU8 = 0;
                    
                    if (!Utils.Data.Pack(dbb.payloadDataArray, decodeIndex, out decodedBlockDeltaTimeStampU8))
                        dbb.resultCode = "Decode {0} failed".CheckedFormat(dbb.FixedBlockTypeID);

                    if (dbb.IsValid)
                        currentBlockDeltaTimeStamp = decodedBlockDeltaTimeStampU8.CastToF8();
                }

                if (dbb.IsValid)
                    dbb = InnerReadDataBlock(ref atOffsetInFile, requirePayloadBytes);
            }

            return dbb;
        }

        public DataBlockBuffer LastTimeStampUpdateDBB { get; private set; }

        /// <summary>
        /// Attempts to read the block header, and optionally the block contents, at the given offset.
        /// On successfull block read, if advanceOffsetOnSuccess is true, advances the given offset by the resulting data block's total length.
        /// </summary>
        private DataBlockBuffer InnerReadDataBlock(ref int atOffsetInFile, bool requirePayloadBytes)
        {
            DataBlockBuffer dbb = new DataBlockBuffer() { fileOffsetToStartOfBlock = atOffsetInFile };

            try
            {
                int startIndex = 0;

                const int maxDataBlockHeaderLength = 27;    // 3 * 9 (max size of a U8Auto)

                dbb.resultCode = RequestThatCacheContains(atOffsetInFile, maxDataBlockHeaderLength, out startIndex);

                int decodeIndex = startIndex;

                dbb.blockTypeID32 = (Int32)cache.byteArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode);
                dbb.payloadLength = (Int32)cache.byteArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode);

                dbb.headerLength = decodeIndex - startIndex;
                dbb.fileDeltaTimeStamp = currentBlockDeltaTimeStamp;

                if (dbb.resultCode.IsNullOrEmpty())
                {
                    int payloadStartIndex = 0;
                    dbb.resultCode = RequestThatCacheContains(atOffsetInFile + dbb.headerLength, dbb.payloadLength, out payloadStartIndex);

                    if (dbb.payloadLength > 0 && (requirePayloadBytes || dbb.payloadLength <= 256))
                    {
                        dbb.payloadDataArray = new byte[dbb.payloadLength];
                        Array.Copy(cache.byteArray, payloadStartIndex, dbb.payloadDataArray, 0, dbb.payloadLength);
                        dbb.HasPayloadBytes = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                dbb.resultCode = dbb.resultCode.MapNullOrEmptyTo("ReadDataBlock from '{0}' at offset:{1} failed: {2} {3}".CheckedFormat(FilePath, atOffsetInFile, ex.GetType(), ex.Message));
            }

            if (dbb.IsValid)
                atOffsetInFile += dbb.TotalLength;

            return dbb;
        }

        #endregion

        #region FileByteArrayCache and RequestThatCacheContains

        private const int defaultCacheSize = 65534;
        private const int cacheSizeRounding = 16384;
        private const int cacheSizeRoundingMinusOne = cacheSizeRounding - 1;
        private const int minNominalCacheFileReadBoundary = 16384;
        private const int minNominalCacheFileReadBoundaryMinusOne = minNominalCacheFileReadBoundary - 1;

        /// <summary>
        /// class manages a byte array that is used to cache read conents from the file and to optimize re-use of such data
        /// </summary>
        private class FileByteArrayCache
        {
            public byte[] byteArray;
            public int bufferStartOffsetInFile;
            public int contentLen;

            public FileByteArrayCache(int byteArraySize = defaultCacheSize)
            {
                byteArraySize = (byteArraySize + cacheSizeRoundingMinusOne) & ~cacheSizeRoundingMinusOne;     // round size up the neartest multiple of cacheSizeRounding

                byteArray = new byte[byteArraySize];
                bufferStartOffsetInFile = 0;
                contentLen = 0;
            }

            /// <summary>Returns true if the given byte offset is currently present in the loaded cache contents</summary>
            public bool ContainsByte(int testFileStartOffset)
            {
                return (contentLen > 0) && testFileStartOffset.IsInRange(bufferStartOffsetInFile, bufferStartOffsetInFile + contentLen - 1);
            }

            /// <summary>Returns true if both the first byte and the last byte of the given range are present in the cache.  If testLen is zero then only the first byte needs to be present</summary>
            public bool ContainsRange(int testFileStartOffset, int testLen)
            {
                return ContainsByte(testFileStartOffset) && ((testLen <= 0) || ContainsByte(testFileStartOffset + testLen - 1));
            }
        }

        private FileByteArrayCache cache = new FileByteArrayCache();

        /// <summary>
        /// This method is responsible for attempting to enlarge the cache as needed and to pull the desired region of the file into the cache so that its contents may be directly accessed by the client.
        /// This method attempts to optimize the cached acccess in the following ways:
        /// First when the cache already containst requested data this method gives the offset to the start of the requested data in the cache and immediately returns success.
        /// Next (some data must be read from the file) the cache determine how much its prior contents can be retained (if any) and it shifts that portion of the array down to beginning of the cache as appropriate.
        /// Then it reads in the remaining required bytes, generates the offset to the start point in cache and returns.
        /// At all times, the cache attempts to keep the contents aligned on minNominalCacheFileReadBoundary boundaries.
        /// </summary>
        private string RequestThatCacheContains(int startOffsetInFile, int numBytes, out int startOffsetInCacheByteArray)
        {
            // if the cache already contains all of the requested bytes then compute the start offset in the cache buffer and return success.
            if (cache.ContainsRange(startOffsetInFile, numBytes))
            {
                startOffsetInCacheByteArray = startOffsetInFile - cache.bufferStartOffsetInFile;
                return string.Empty;
            }

            // we are going to need to read something from the file.  
            // first determine the desired cache start offset (requested start offset rounded down to nearest boundary) and the required loaded cache size (length sufficient for desired offset + desired number of bytes rounded up to nearest boundary)

            // Note: this logic only supports shifting in the normal read direction.  This logic is not optimized for frequent random (or worse reverse) access order for these files.

            int desiredCacheStartOffset = startOffsetInFile & ~minNominalCacheFileReadBoundaryMinusOne;     // round down
            int desiredCacheEndAfterOffset = ((startOffsetInFile + numBytes) + minNominalCacheFileReadBoundaryMinusOne) & ~minNominalCacheFileReadBoundaryMinusOne; // round up
            int desiredCacheCount = desiredCacheEndAfterOffset - desiredCacheStartOffset;

            // if the cache is not large enough for this requested block after boundary alignment then we reset the cache by creating another, larger one
            if (desiredCacheCount > cache.byteArray.Length)
            {
                // enlarging the cache also clears it and thus triggers a full read.
                cache = new FileByteArrayCache(desiredCacheCount);
            }
            else
            {
                // if the newly desired cache offset is beyond the current one and the cache still contains at least one byte at the the newly desire offset
                // then shift the portion that we already read down and make it the new start offset.
                if ((desiredCacheStartOffset > cache.bufferStartOffsetInFile) && cache.ContainsByte(desiredCacheStartOffset))
                {
                    int shiftDistance = (desiredCacheStartOffset - cache.bufferStartOffsetInFile);
                    int shiftByteCount = (cache.contentLen - shiftDistance);

                    if (shiftDistance > 0 && shiftByteCount > 0)
                    {
                        Array.Copy(cache.byteArray, shiftDistance, cache.byteArray, 0, shiftByteCount);
                        cache.bufferStartOffsetInFile = desiredCacheStartOffset;
                        cache.contentLen = shiftByteCount;
                    }
                }

                // if the above logic did not move the cache's start offset to match the desired one then we clear the cache.  This will trigger a full read.
                if (cache.bufferStartOffsetInFile != desiredCacheStartOffset)
                {
                    cache.bufferStartOffsetInFile = desiredCacheStartOffset;
                    cache.contentLen = 0;
                }
            }

            startOffsetInCacheByteArray = startOffsetInFile - cache.bufferStartOffsetInFile;

            int readStartOffset = cache.bufferStartOffsetInFile + cache.contentLen;
            int countToRead = Math.Min(desiredCacheCount - cache.contentLen, FileLength - readStartOffset);

            try
            {
                fs.Seek(readStartOffset, SeekOrigin.Begin);
                fs.Read(cache.byteArray, cache.contentLen, countToRead);

                cache.contentLen += countToRead;

                return string.Empty;
            }
            catch (System.Exception ex)
            {
                string ec = "ReadFile '{0}' offset:{1} count:{2} failed: {3} {4}".CheckedFormat(FilePath, readStartOffset, countToRead, ex.GetType(), ex.Message);
                return ec;
            }
        }

        #endregion

        #region Internal fields

        private FileStream fs;
        private int fileScanOffset = 0;
        private double currentBlockDeltaTimeStamp = 0;

        private int fileIndexBlockStartOffset = 0;

        private static readonly SetupInfo emptySetupInfo = new SetupInfo();
        private static readonly FileIndexInfo emptyFileIndexInfo = new FileIndexInfo();

        #endregion
    }
}

//-------------------------------------------------------------------
