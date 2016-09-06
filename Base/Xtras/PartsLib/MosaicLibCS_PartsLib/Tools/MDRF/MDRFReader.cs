//-------------------------------------------------------------------
/*! @file MDRFReaderPart.cs
 *  @brief an active part that supports reading MDRF (Mosaic Data Recording Format) files.
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

using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.Semi.E005.Data;

namespace MosaicLib.PartsLib.Tools.MDRF.Reader
{
    #region ReadAndProcessContents helper definitions

    public class ReadAndProcessFilterSpec
    {
        public EventHandlerDelegate<ProcessContentEventData> EventHandlerDelegate { get; set; }
        public ProcessContentEvent PCEMask { get; set; }
        public double FirstFileDeltaTimeStamp { get; set; }
        public double LastFileDeltaTimeStamp { get; set; }
        public double NominalMinimumGroupAndTimeStampUpdateInterval { get; set; }

        public ReadAndProcessFilterSpec()
        {
            PCEMask = ProcessContentEvent.All;
            FirstFileDeltaTimeStamp = Double.NegativeInfinity;
            LastFileDeltaTimeStamp = Double.PositiveInfinity;
            NominalMinimumGroupAndTimeStampUpdateInterval = 0.0;
        }
    }

    [Flags]
    public enum ProcessContentEvent : int
    {
        None = 0x0000,
        ReadingStart = 0x0001,
        ReadingEnd = 0x0002,
        RowStart = 0x0004,
        RowEnd = 0x0008,
        NewTimeStamp = 0x0010,
        Occurrence = 0x0100,
        Message = 0x0200,
        Error = 0x0400,
        Group = 0x1000,
        EmptyGroup = 0x2000,
        PartialGroup = 0x4000,
        StartOfFullGroup = 0x8000,
        All = (ReadingStart | ReadingEnd | RowStart | RowEnd | NewTimeStamp | Occurrence | Message | Error | Group | EmptyGroup | PartialGroup | StartOfFullGroup),
    }

    public class ProcessContentEventData
    {
        public ProcessContentEvent PCE { get; internal set; }
        public UInt64 SeqNum { get; internal set; }
        public double FileDeltaTimeStamp { get; internal set; }
        public FileIndexRowBase Row { get; internal set; }
        public IMessageInfo MessageInfo { get; internal set; }
        public IOccurrenceInfo OccurrentInfo { get; internal set; }
        public IGroupInfo GroupInfo { get; internal set; }
        public ValueContainer VC { get; internal set; }

        public DataBlockBuffer DataBlockBuffer 
        {
            get { return dataBlockBuffer; }
            internal set { dataBlockBuffer = value; if (value != null) { FileDeltaTimeStamp = value.fileDeltaTimeStamp; } }
        }
        private DataBlockBuffer dataBlockBuffer;
    }

    public class DataBlockBuffer
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

        private static readonly byte[] emptyByteArray = new byte[0];
    }

    #endregion

    public class MDRFFileReader : DisposableBase
    {
        #region Construction (and Release)

        public MDRFFileReader(string path)
            : this(path, null)
        { }

        public MDRFFileReader(string path, IValuesInterconnection ivi)
        {
            AddExplicitDisposeAction(Release);

            FilePath = path.MapNullToEmpty();
            IVI = ((ivi != null) ? ivi : new ValuesInterconnection("{0} {1}".CheckedFormat(Fcns.CurrentMethodName, FilePath), false));

            LibraryInfo = new LibraryInfo();
            SetupInfo = emptySetupInfo;
            FileIndexInfo = emptyFileIndexInfo;
            DateTimeInfo = new DateTimeInfo();

            try
            {
                fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                FileLength = (Int32)fs.Length;

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

        #endregion

        #region public properties

        public string FilePath { get; private set; }
        public IValuesInterconnection IVI { get; private set; }

        public bool IsFileOpen { get { return (fs != null); } }
        public int FileLength { get; private set; }
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

            DataBlockBuffer fileIndexDbb = ReadDataBlockAndHandleTimeStampUpdates(ref fileScanOffset);

            if (!fileIndexDbb.IsValid)
                return "ReadFileAndHeader failed at 2nd block: {0}".CheckedFormat(fileIndexDbb.resultCode);

            if (fileIndexDbb.FixedBlockTypeID != FixedBlockTypeID.FileIndexV1)
                return "ReadFileAndHeader failed: unexpected 2nd block {0}".CheckedFormat(fileIndexDbb.FixedBlockTypeID);

            DataBlockBuffer dateTimeDbb = ReadDataBlockAndHandleTimeStampUpdates(ref fileScanOffset);

            if (!dateTimeDbb.IsValid)
                return "ReadFileAndHeader failed at 3rd block: {0}".CheckedFormat(dateTimeDbb.resultCode);

            if (dateTimeDbb.FixedBlockTypeID != FixedBlockTypeID.DateTimeV1)
                return "ReadFileAndHeader failed: unexpected 2nd block {0}".CheckedFormat(dateTimeDbb.FixedBlockTypeID);

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

            return ec;
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
                                    IVA = IVI.GetValueAccessor(mdci.Name),
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
                                    GroupPointFileIDList = new List<int>(nvs["ItemList"].VC.GetValue<string>(false).MapNullToEmpty().Split(',').Select(s => new StringScanner(s).ParseValue<int>(0))).AsReadOnly(),
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
                                }).ToArray();

                gi.GroupPointInfoList = new List<IGroupPointInfo>(gpiArray).AsReadOnly();
                gi.GroupPointIVAArray = gpiArray.Select(gpi => gpi.IVA).ToArray();
            }

            MetaDataArray = mddNVSList.ToArray();

            GroupInfoArray = giArray;
            GroupPointInfoArray = gpiList.ToArray();
            OccurrenceInfoArray = oiList.ToArray();
            GroupPointIVAArray = gpiList.Select(gpi => gpi.IVA).ToArray();

            GroupInfoDictionary = giDictionary;
            GroupPointInfoDictionary = gpiDictionary;
            OccurrenceInfoDictionary = oiDictionary;
            FileIDToMDCommonDictinoary = fileIDtoMDCommonDictionary;

            return resultCode;
        }

        private class LocalGroupPointInfo : GroupPointInfo
        {
            public IValueAccessor IVA { get; set; }

            public LocalGroupPointInfo(MetaDataCommonInfoBase rhs) : base(rhs) { }
        }

        private class LocalGroupInfo : GroupInfo
        {
            public IValueAccessor[] GroupPointIVAArray { get; set; }

            public LocalGroupInfo(MetaDataCommonInfoBase rhs) : base(rhs) { }
        }

        #endregion

        #region Content reading interface

        public void ReadAndProcessContents(ReadAndProcessFilterSpec filterSpec)
        {
            ResetIVAs();

            lastDbbTimeStamp = filterSpec.FirstFileDeltaTimeStamp;
            lastGroupOrTSUpdateEventTimeStamp = Double.NegativeInfinity;

            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.ReadingStart, FileDeltaTimeStamp = lastDbbTimeStamp });

            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.NewTimeStamp, FileDeltaTimeStamp = lastDbbTimeStamp });

            int firstRowIndex = 0;
            if (FileIndexInfo.FindRowForDeltaTimeStamp(filterSpec.FirstFileDeltaTimeStamp, out firstRowIndex))
            {
                for (; firstRowIndex > 0; firstRowIndex -= 1)
                {
                    FileIndexRowBase row = FileIndexInfo.FileIndexRowArray.SafeAccess(firstRowIndex);
                    if (row.ContainsStartOfFullGroup)
                        break;
                }
            }

            int rowIndex = firstRowIndex;
            FileIndexRowBase scanRow = FileIndexInfo.FileIndexRowArray.SafeAccess(rowIndex);
            FileIndexRowBase nextScanRow = FileIndexInfo.FileIndexRowArray.SafeAccess(rowIndex + 1);

            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.RowStart, Row = scanRow, FileDeltaTimeStamp = scanRow.FirstBlockDeltaTimeStamp });

            bool isFirstBlock = true;
            for (; ; )
            {
                DataBlockBuffer dbb;
                if (isFirstBlock)
                {
                    dbb = ReadFirstDataBlockUsingFileIndex(rowIndex);
                    isFirstBlock = false;
                }
                else
                {
                    dbb = ReadNextDataBlock();
                }

                if (dbb == null || !dbb.IsValid || dbb.fileDeltaTimeStamp > filterSpec.LastFileDeltaTimeStamp)
                    break;

                if (lastDbbTimeStamp != dbb.fileDeltaTimeStamp)
                {
                    lastDbbTimeStamp = dbb.fileDeltaTimeStamp;

                    if ((dbb.fileDeltaTimeStamp >= filterSpec.FirstFileDeltaTimeStamp) && ((dbb.fileDeltaTimeStamp - lastGroupOrTSUpdateEventTimeStamp) >= filterSpec.NominalMinimumGroupAndTimeStampUpdateInterval))
                    {
                        SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.NewTimeStamp, DataBlockBuffer = LastTimeStampUpdateDBB });
                        lastGroupOrTSUpdateEventTimeStamp = dbb.fileDeltaTimeStamp;
                    }
                }

                ProcessContents(dbb, filterSpec);

                if (fileScanOffset >= FileLength)
                    break;

                if (nextScanRow != null && !nextScanRow.IsEmpty && fileScanOffset >= nextScanRow.FileOffsetToStartOfFirstBlock)
                {
                    SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.RowEnd, Row = scanRow, FileDeltaTimeStamp = scanRow.LastBlockDeltaTimeStamp });
                    scanRow = nextScanRow;
                    SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.RowStart, Row = scanRow, FileDeltaTimeStamp = scanRow.FirstBlockDeltaTimeStamp });

                    nextScanRow = FileIndexInfo.FileIndexRowArray.SafeAccess(++rowIndex);
                }
            }

            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.RowEnd, Row = scanRow, FileDeltaTimeStamp = scanRow.LastBlockDeltaTimeStamp });

            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.ReadingEnd, FileDeltaTimeStamp = lastDbbTimeStamp });
        }

        private double lastDbbTimeStamp;
        private double lastGroupOrTSUpdateEventTimeStamp;

        private void ResetIVAs()
        {
            foreach (IValueAccessor iva in GroupPointIVAArray ?? emptyIVAArray)
                iva.Reset();
        }

        private static IValueAccessor[] emptyIVAArray = new IValueAccessor[0];

        private void SignalEventIfEnabled(ReadAndProcessFilterSpec filterSpec, ProcessContentEventData eventData)
        {
            if ((eventData.PCE & filterSpec.PCEMask) != ProcessContentEvent.None && filterSpec.EventHandlerDelegate != null)
                filterSpec.EventHandlerDelegate(this, eventData);
        }

        private void ProcessContents(DataBlockBuffer dbb, ReadAndProcessFilterSpec filterSpec)
        {
            switch (dbb.FixedBlockTypeID)
            {
                case FixedBlockTypeID.ErrorV1:
                case FixedBlockTypeID.MessageV1:
                    if (dbb.fileDeltaTimeStamp >= filterSpec.FirstFileDeltaTimeStamp)
                        ProcessMessageOrErrorBlock(dbb, filterSpec);
                    return;
                case FixedBlockTypeID.MetaDataV1:
                case FixedBlockTypeID.FileHeaderV1:
                case FixedBlockTypeID.FileIndexV1:
                case FixedBlockTypeID.DateTimeV1:
                case FixedBlockTypeID.FileEndV1:
                case FixedBlockTypeID.TimeStampUpdateV1:
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

                        if (oi != null && dbb.fileDeltaTimeStamp >= filterSpec.FirstFileDeltaTimeStamp)
                            ProcessOccurrenceBlock(oi, dbb, filterSpec);
                    }
                    return;
                case MDItemType.Group:
                    {
                        IGroupInfo gi = mdci as IGroupInfo;

                        ProcessGroupBlock(gi, dbb, filterSpec);
                    }
                    return;
                default:
                    return;
            }
        }

        private void ProcessMessageOrErrorBlock(DataBlockBuffer dbb, ReadAndProcessFilterSpec filterSpec)
        {
            int decodeIndex = 0;

            UInt64 messageRecordedUtcTimeU8 = 0;
            ValueContainer messageVC = ValueContainer.Empty;

            UInt64 seqNum = dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode);

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
                SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.Error, MessageInfo = messageInfo, DataBlockBuffer = dbb });
            else
                SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.Message, MessageInfo = messageInfo, DataBlockBuffer = dbb });
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

            SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = ProcessContentEvent.Occurrence, OccurrentInfo = oi, SeqNum = seqNumU8, VC = occurrenceVC, DataBlockBuffer = dbb });
        }

        private void ProcessGroupBlock(IGroupInfo gi, DataBlockBuffer dbb, ReadAndProcessFilterSpec filterSpec)
        {
            LocalGroupInfo lgi = gi as LocalGroupInfo;
            if (lgi == null)
                return;

            int decodeIndex = 0;
            UInt64 seqNumU8 = 0;

            seqNumU8 = dbb.payloadDataArray.DecodeU8Auto(ref decodeIndex, ref dbb.resultCode);
            if (!dbb.IsValid)
                return;

            bool startReached = (dbb.fileDeltaTimeStamp >= filterSpec.FirstFileDeltaTimeStamp);
            bool intervalReached = (startReached && (dbb.fileDeltaTimeStamp - lastGroupOrTSUpdateEventTimeStamp) >= filterSpec.NominalMinimumGroupAndTimeStampUpdateInterval);
            if (intervalReached)
                lastGroupOrTSUpdateEventTimeStamp = dbb.fileDeltaTimeStamp;

            if (decodeIndex == dbb.payloadLength)
            {
                if (startReached && intervalReached)
                    SignalEventIfEnabled(filterSpec, new ProcessContentEventData() { PCE = (ProcessContentEvent.Group | ProcessContentEvent.EmptyGroup), GroupInfo = gi, SeqNum = seqNumU8, DataBlockBuffer = dbb });

                return;
            }
            
            GroupBlockFlagBits groupBlockFlagBits = unchecked((GroupBlockFlagBits) (dbb.payloadDataArray.SafeAccess(decodeIndex++)));
            int numPoints = lgi.GroupPointInfoList.Count;

            if ((groupBlockFlagBits & GroupBlockFlagBits.HasUpdateMask) == GroupBlockFlagBits.None)
            {
                // this is not a sparse group - it also might be a StartOfFullGroup group

                for (int gpiIndex = 0; gpiIndex < numPoints; gpiIndex++)
                {
                    IGroupPointInfo gpi = gi.GroupPointInfoList[gpiIndex];
                    IValueAccessor iva = lgi.GroupPointIVAArray[gpiIndex];

                    ValueContainer pointVC = DecodeGroupPointValue(gpi, dbb, ref decodeIndex);
                    if (!dbb.IsValid || iva == null)
                        return;

                    iva.Set(pointVC);
                }

                if (startReached)
                {
                    if ((groupBlockFlagBits & GroupBlockFlagBits.IsStartOfFullGroup) != GroupBlockFlagBits.None)
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

                for (int gpiIndex = 0; gpiIndex < numPoints; gpiIndex++)
                {
                    if (dbb.payloadDataArray.GetBit(gpiIndex + bitIndexOffset))
                    {
                        IGroupPointInfo gpi = gi.GroupPointInfoList[gpiIndex];
                        IValueAccessor iva = lgi.GroupPointIVAArray[gpiIndex];

                        ValueContainer pointVC = DecodeGroupPointValue(gpi, dbb, ref decodeIndex);

                        if (!dbb.IsValid || iva == null)
                            return;

                        iva.Set(pointVC);
                    }
                }

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

        public DataBlockBuffer ReadFirstDataBlockUsingFileIndex(int rowIndex, bool requirePayloadBytes = true)
        {
            if (SetupInfo == null || FileIndexInfo == null)
                return new DataBlockBuffer() { resultCode = "{0} failed: SetupInfo and/or FileIndexInfo are null".CheckedFormat(Fcns.CurrentMethodName) };

            FileIndexRowBase row = FileIndexInfo.FileIndexRowArray.SafeAccess(rowIndex);

            if (row == null || row.IsEmpty)
                return new DataBlockBuffer() { resultCode = "{0} failed: FileIndexInfo does not contain a valid and non-empty row at {0}".CheckedFormat(Fcns.CurrentMethodName, rowIndex) };

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
        /// </summary>
        private DataBlockBuffer ReadDataBlockAndHandleTimeStampUpdates(ref int atOffsetInFile, bool requirePayloadBytes = true)
        {
            DataBlockBuffer dbb = InnerReadDataBlock(ref atOffsetInFile, requirePayloadBytes);

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

        private class FileByteArrayCache
        {
            public byte[] byteArray = new byte[65536];
            public int fileStartOffset;
            public int contentLen;

            public FileByteArrayCache(int byteArraySize = 65536)
            {
                byteArraySize = (byteArraySize + 16383) & ~16383;     // round size up the neartest multiple of 16k

                byteArray = new byte[byteArraySize];
            }

            public bool ContainsByte(int testFileStartOffset)
            {
                return testFileStartOffset.IsInRange(fileStartOffset, fileStartOffset + contentLen - 1);
            }

            public bool ContainsRange(int testFileStartOffset, int testLen)
            {
                return ContainsByte(testFileStartOffset) && ((testLen <= 0) || ContainsByte(testFileStartOffset + testLen - 1));
            }
        }

        private FileByteArrayCache cache = new FileByteArrayCache();
        private const int minNominalFileReadBoundary = 16384;
        private const int minNominalFileReadBoundaryMinusOne = minNominalFileReadBoundary - 1;

        private string RequestThatCacheContains(int startOffsetInFile, int numBytes, out int startOffsetInCacheByteArray)
        {
            // if the cache is not large enough for this requested block then we reset the cache by creating another, larger one
            if (numBytes > cache.byteArray.Length)
                cache = new FileByteArrayCache(numBytes);

            // if the cache already contains all of the requested bytes then compute the start offset in the cache buffer and return success.
            if (cache.ContainsRange(startOffsetInFile, numBytes))
            {
                startOffsetInCacheByteArray = startOffsetInFile - cache.fileStartOffset;
                return string.Empty;
            }

            // we are going to need to read something from the file.  If there are blocks of data in the cache that do not need to be read again then shift them over.
            // Note: this logic only supports shifting in the normal read direction.  This logic is not optimized for frequent random (or worse reverse) access order for these files.

            int desiredCacheStartOffset = startOffsetInFile & ~minNominalFileReadBoundaryMinusOne;
            int desiredCacheEndOffset = (startOffsetInFile + numBytes - 1 + minNominalFileReadBoundaryMinusOne) & ~minNominalFileReadBoundaryMinusOne;
            int desiredCacheCount = desiredCacheEndOffset - desiredCacheStartOffset + 1;

            // if the newly desired cache offset is beyond the current one and the cache still contains at least one byte at the the newly desire offset
            // then shift the portion that we already read down and make it the new start offset.
            if ((desiredCacheStartOffset > cache.fileStartOffset) && cache.ContainsByte(desiredCacheStartOffset))
            {
                int shiftDistance = (desiredCacheStartOffset - cache.fileStartOffset);
                int shiftByteCount = (cache.contentLen - shiftDistance);

                if (shiftDistance > 0 && shiftByteCount > 0)
                {
                    Array.Copy(cache.byteArray, shiftDistance, cache.byteArray, 0, shiftByteCount);
                    cache.fileStartOffset = desiredCacheStartOffset;
                    cache.contentLen = shiftByteCount;
                }
            }

            // if the above logic did not move the cache's start offset to match the desired one then we clear the cache.  This will trigger a full read.
            if (cache.fileStartOffset != desiredCacheStartOffset)
            {
                cache.fileStartOffset = desiredCacheStartOffset;
                cache.contentLen = 0;
            }
            startOffsetInCacheByteArray = startOffsetInFile - cache.fileStartOffset;

            int readStartOffset = cache.fileStartOffset + cache.contentLen;
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

        private static readonly SetupInfo emptySetupInfo = new SetupInfo();
        private static readonly FileIndexInfo emptyFileIndexInfo = new FileIndexInfo();

        #endregion
    }
}

//-------------------------------------------------------------------
