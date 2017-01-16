//-------------------------------------------------------------------
/*! @file MDRFWriterPart.cs
 *  @brief a passive part that supports writing MDRF (Mosaic Data Recording Format) files.
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
using System.Runtime.InteropServices;
using System.Text;

using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Utils;
using MosaicLib.Time;

using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.Semi.E005.Data;
using System.Collections;

namespace MosaicLib.PartsLib.Tools.MDRF.Writer
{
    #region Constants

    public static partial class Constants
    {
        /// <remarks>
        /// Version history:
        /// 1.0.0 (2016-09-01) : First CR version.
        /// 1.0.1 (2016-09-24) : API extensions to improve usability, modified RecordGroups to trigger write of file index any time RecordGroups does a writeall.
        /// 1.0.2 (2016-10-02) : Changed default Flush flags to All (from File)
        /// </remarks>
        public static readonly LibraryInfo LibInfo = new LibraryInfo()
        {
            Type = "Mosaic Data Recording File (MDRF)",
            Name = "Mosaic Data Recording Engine (MDRE) CS API",
            Version = "1.0.2 (2016-10-02)",
        };
    }

    #endregion

    #region IMDRFWriter and related definitions (GroupInfo, GroupPointInfo, OccurrenceInfo)

    public interface IMDRFWriter : IPartBase
    {
        string RecordGroups(bool writeAll = false, DateTimeStampPair dtPair = null);

        string RecordOccurrence(OccurrenceInfo occurrenceInfo, object dataValue, DateTimeStampPair dtPair = null);
        string RecordOccurrence(OccurrenceInfo occurrenceInfo, ValueContainer dataVC, DateTimeStampPair dtPair = null);

        string Flush(FlushFlags flushFlags = FlushFlags.All, DateTimeStampPair dtPair = null);

	    int GetCurrentFileSize();

        FileInfo CurrentFileInfo { get; }
        FileInfo LastFileInfo { get; }

        int CloseCurrentFile(string reason, DateTimeStampPair dtPair = null);
    }

    /// <summary>
    /// Flags that are used with the IMDRFWriter Flush method to configure/select what specific actions it should perform.
    /// <para/>None (0x00), File (0x01), Index (0x02), All (File | Index)
    /// </summary>
    [Flags]
    	public enum FlushFlags
	{
        /// <summary>Placeholder value, not intended for external use (0x00)</summary>
        None = 0x00,
        /// <summary>Requests that the flush operation flush file buffers that are used by the writer (0x01)</summary>
        File = 0x01,
        /// <summary>Requests that the flush operation update/write the file index (0x02)</summary>
        Index = 0x02,
        /// <summary>Selects that all flush actions be performed (File | Index)</summary>
        All = (FlushFlags.File | FlushFlags.Index),
	}

    /// <summary>
    /// Defines the set of public information that may be obtained about a recording file including the full file path, 
    /// the file's seqNum (current count of files created by the tool) the file size and an indicator of whether it is currently active (open).
    /// </summary>
    public struct FileInfo : IEquatable<FileInfo>
    {
        /// <summary>Gives the full file path for the this file</summary>
        public string FilePath { get; internal set; }
        /// <summary>Gives the count of the number of files that the source writter has created</summary>
        public int SeqNum { get; internal set; }
        /// <summary>Gives the total size of the current file</summary>
        public int FileSize { get; internal set; }
        /// <summary>Is true if the file was active (open) when this information was obtained</summary>
        public bool IsActive { get; internal set; }

        /// <summary>Returns true if this FileInfo has the same contents as the given other FileInfo</summary>
        public bool Equals(FileInfo other)
        {
            return (FilePath == other.FilePath
                    && SeqNum == other.SeqNum
                    && FileSize == other.FileSize
                    && IsActive == other.IsActive
                    );
        }

        /// <summary>Debugging and logging helper method</summary>
        public override string ToString()
        {
            return "'{0}' seqNum:{1} size:{2}{3}".CheckedFormat(FilePath, SeqNum, FileSize, IsActive ? " [active]" : string.Empty);
        }
    }

    public class GroupInfo : MetaDataCommonInfo
    {
        public UInt64 FileIndexUserRowFlagBits { get; set; }
        public GroupPointInfo[] GroupPointInfoArray { get; set; }
        public bool UsePointIVAsForTouched { get; set; }

        public int GroupID { get { return ClientID; } set { ClientID = value; } }
        public bool Touched { get; set; }
    }

    public class GroupPointInfo : MetaDataCommonInfo
    {
        public ContainerStorageType ValueCST { get; set; }
        public IValuesInterconnection ValueSourceIVAFactoryIVI { get; set; }
        public IValueAccessor ValueSourceIVA { get; set; }

        public ValueContainer VC { get; set; }
        public bool VCIsUsable { get { return (ValueCST == ContainerStorageType.None || ValueCST == VC.cvt); } }

        public int GroupID { get; set; }
        public int SourceID { get { return ClientID; } set { ClientID = value; } }
    }

    public class OccurrenceInfo : MetaDataCommonInfo
    {
        public UInt64 FileIndexUserRowFlagBits { get; set; }
        public ContainerStorageType ContentCST { get; set; }
        public bool IsHighPriority { get; set; }

        public int OccurrenceID { get { return ClientID; } set { ClientID = value; } }
    }

    public class MetaDataCommonInfo
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public NamedValueSet ClientNVS { get; set; }

        public int ClientID { get; set; }
        public int FileID { get; set; }

        public override string ToString()
        {
            return "name:'{0}' clientID:{1} fileID:{2}".CheckedFormat(Name, ClientID, FileID);
        }
    }

    #endregion

    #region MDRFWriter implementation

    public class MDRFWriter : SimplePartBase, IMDRFWriter
    {
        #region Construction and Release

        public MDRFWriter(string partID, SetupInfo setupInfo, GroupInfo [] groupInfoArray, OccurrenceInfo [] occurrenceInfoArray)
            : base(partID)
        {
            AddExplicitDisposeAction(Release);

            if (setupInfo == null)
                LastOpResultCode = LastOpResultCode.MapNullOrEmptyTo("{0} failed: given setupInfo cannot be null".CheckedFormat(CurrentMethodName));

            setupOrig = setupInfo ?? defaultSetupInfo;
            setup = new SetupInfo(setupOrig).MapDefaultsTo(defaultSetupInfo).ClipValues();

            dataBlockBuffer = new DataBlockBuffer(setup);
            fileIndexData = new FileIndexData(setup);

            // create outermost occurance, group tracker, and source tracker arrays
            occurrenceTrackerArray = (occurrenceInfoArray ?? emptyOccurrenceInfoArray).Select(occurrenceInfo => new OccurrenceTracker(occurrenceInfo)).ToArray();
            groupTrackerArray = (groupInfoArray ?? emptyGroupInfoArray).Select(groupInfo => new GroupTracker(groupInfo, setup)).ToArray();
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
            int fileID = unchecked((int) FixedBlockTypeID.FirstDynamicallyAssignedID);
            foreach (TrackerCommon tc in allItemCommonArray)
                tc.FileID = fileID++;

            // then Setup all of the items so that they can generate their NVS contents
            foreach (TrackerCommon tc in allItemCommonArray)
                tc.Setup();

            allMetaDataItemsArray = (sourceTrackerArray as TrackerCommon[]).Concat(groupTrackerArray as TrackerCommon[]).Concat(occurrenceTrackerArray as TrackerCommon[]).ToArray();

            // validate selected fields
            LastOpResultCode = string.Empty;

            // create the directory if needed.

            if (setup.DirPath.IsNullOrEmpty())
                LastOpResultCode = LastOpResultCode.MapNullOrEmptyTo("{0} failed: given DirPath cannot be null or empty".CheckedFormat(CurrentMethodName));
            else
                setup.DirPath = System.IO.Path.GetFullPath(setup.DirPath);

            if (!System.IO.Directory.Exists(setup.DirPath ?? string.Empty))
            {
                if (!setup.CreateDirectoryIfNeeded)
                    LastOpResultCode = LastOpResultCode.MapNullOrEmptyTo("{0} failed: given DirPath '{1}' was not found or is not a directory".CheckedFormat(CurrentMethodName, setupOrig.DirPath));
                else
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(setup.DirPath);
                    }
                    catch (System.Exception ex)
                    {
                        LastOpResultCode = LastOpResultCode.MapNullOrEmptyTo("{0} failed: CreateDirectory '{1}': {2} {3}".CheckedFormat(CurrentMethodName, setup.DirPath, ex.GetType(), ex.Message));
                    }
                }
            }
        }

        public void Release()
        {
            CloseCurrentFile("On Release");
        }

        public string LastOpResultCode { get; set; }

        private static readonly SetupInfo defaultSetupInfo = new SetupInfo();
        private static readonly GroupInfo[] emptyGroupInfoArray = new GroupInfo[0];
        private static readonly GroupPointInfo[] emptyPointGroupInfoArray = new GroupPointInfo[0];
        private static readonly OccurrenceInfo[] emptyOccurrenceInfoArray = new OccurrenceInfo[0];

        #endregion

        #region IMDRFWriter implementation methods

        public string RecordGroups(bool writeAll = false, DateTimeStampPair dtPair = null)
        {
            dtPair = (dtPair ?? DateTimeStampPair.Now).UpdateFileDeltas(fileReferenceDTPair);

            string ec = ActiviateFile(dtPair);

            if (!ec.IsNullOrEmpty())
            {
                droppedCounts.IncrementDataGroup();
                return ec;
            }

            QpcTimeStamp tsNow = dtPair.qpcTimeStamp;

            // check if we need to do a writeAll
            if (!writeAll)
            {
                // if this is the first call or the MinNominalWriteAllInterval has elapsed then make this a writeall call
                if (lastWriteAllTimeStamp.IsZero || ((tsNow - lastWriteAllTimeStamp) >= setup.MinNominalWriteAllInterval))
                    writeAll = true;
            }

            if (writeAll)
            {
                lastWriteAllTimeStamp = tsNow;
                writeFileIndexNow = true;
            }

            foreach (GroupTracker groupTracker in groupTrackerArray)
            {
                groupTracker.Service(writeAll);
                groupTracker.numTouchedSources = 0; // clear numTouchedSources count at start of each service loop
            }

            foreach (SourceTracker sourceTracker in sourceTrackerArray)
                sourceTracker.Service();

            // generate and write the group data blocks themselves
            if (ec.IsNullOrEmpty())
            {
                FileIndexRowFlagBits firstBlockFlagBits = (writeAll ? FileIndexRowFlagBits.ContainsStartOfFullGroup : FileIndexRowFlagBits.None);

                foreach (GroupTracker groupTracker in groupTrackerArray)
                {
                    if (groupTracker.touched)
                    {
                        if (groupTracker.WriteAsEmptyGroup)
                        {
                            StartGroupDataBlock(groupTracker, dtPair, firstBlockFlagBits, GroupBlockFlagBits.IsEmptyGroup, preIncrementSeqNum: true);
                        }
                        else if (groupTracker.WriteAsSparseGroup)
                        {
                            // write group with updateMask (sparse group)
                            StartGroupDataBlock(groupTracker, dtPair, firstBlockFlagBits, GroupBlockFlagBits.HasUpdateMask, preIncrementSeqNum: true);

                            for (int sourceIdx = 0; sourceIdx < groupTracker.sourceTrackerArrayLength; sourceIdx++)
                                groupTracker.updateMaskByteArray.SetBit(sourceIdx, groupTracker.sourceTrackerArray[sourceIdx].touched);

                            dataBlockBuffer.payloadDataList.AddRange(groupTracker.updateMaskByteArray);

                            foreach (SourceTracker sourceTracker in groupTracker.sourceTrackerArray)
                            {
                                if (sourceTracker.touched)
                                    sourceTracker.AppendValue(dataBlockBuffer.payloadDataList);
                            }
                        }
                        else
                        {
                            // write as full group
                            StartGroupDataBlock(groupTracker, dtPair, firstBlockFlagBits, GroupBlockFlagBits.None, preIncrementSeqNum: true);

                            foreach (SourceTracker sourceTracker in groupTracker.sourceTrackerArray)
                                sourceTracker.AppendValue(dataBlockBuffer.payloadDataList);
                        }

                        if (ec.IsNullOrEmpty())
                            ec = WriteDataBlock(dtPair);

                        if (ec.IsNullOrEmpty())
                            groupTracker.NoteWritten();

                        firstBlockFlagBits = FileIndexRowFlagBits.None;
                    }
                    else 
                    {
                        // otherwise we do not write the group at all (see Service for details)
                    }
                }
            }

            if (ec.IsNullOrEmpty())
                ServicePendingWrites(dtPair);

            if (!ec.IsNullOrEmpty())
            {
                droppedCounts.IncrementDataGroup();
                RecordError(ec, dtPair);
                CloseFile(dtPair);
            }

            return ec;
        }

        public string RecordOccurrence(OccurrenceInfo occurrenceInfo, object dataValue, DateTimeStampPair dtPair = null)
        {
            return RecordOccurrence(occurrenceInfo, new ValueContainer(dataValue), dtPair);
        }

        public string RecordOccurrence(OccurrenceInfo occurrenceInfo, ValueContainer dataVC, DateTimeStampPair dtPair = null)
        {
            if (occurrenceInfo == null)
                return "{0} failed:  occurrenceInfo parameter cannot be null".CheckedFormat(CurrentMethodName);

            OccurrenceTracker occurrenceTracker = occurrenceTrackerArray.SafeAccess(occurrenceInfo.OccurrenceID - 1);

            if (occurrenceTracker == null)
            {
                if (occurrenceInfo.OccurrenceID == 0)
                    return RecordError("{0} failed: no tracker found for occurrence {1} (probably was not registered)".CheckedFormat(CurrentMethodName, occurrenceInfo), dtPair);
                else
                    return RecordError("{0} failed: no tracker found for occurrence {1}".CheckedFormat(CurrentMethodName, occurrenceInfo), dtPair);
            }

            if (occurrenceTracker.ifc != ItemFormatCode.None && occurrenceTracker.inferredCST != dataVC.cvt)
                return RecordError("{0} failed on {1}: data {2} is not compatible with required ifc:{3}".CheckedFormat(CurrentMethodName, occurrenceInfo, dataVC, occurrenceTracker.ifc), dtPair);

            dtPair = (dtPair ?? DateTimeStampPair.Now).UpdateFileDeltas(fileReferenceDTPair);

            string ec = ActiviateFile(dtPair);

            if (ec.IsNullOrEmpty())
            {
                StartOccurrenceDataBlock(occurrenceTracker, dtPair);
                dataBlockBuffer.payloadDataList.AppendWithIH(dataVC);
                ec = WriteDataBlock(dtPair);
            }

            if (!ec.IsNullOrEmpty())
                RecordError(ec, dtPair);

            return ec;
        }

        public string Flush(FlushFlags flushFlags = FlushFlags.All, DateTimeStampPair dtPair = null)
        {
            string ec = string.Empty;

            if (!IsFileOpen)
                ec = "{0} failed: File is not open".CheckedFormat(CurrentMethodName);

            dtPair = (dtPair ?? DateTimeStampPair.Now).UpdateFileDeltas(fileReferenceDTPair);

            try
            {
                if (ec.IsNullOrEmpty() && flushFlags.IsSet(FlushFlags.Index))
                    ec = UpdateAndWriteFileIndex(doFlush: false, dtPair: dtPair);

                if (ec.IsNullOrEmpty() && flushFlags.IsSet(FlushFlags.File))
                    fs.Flush();
            }
            catch (System.Exception ex)
            {
                ec = RecordError("{0} failed: {1} {2} {3}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.GetType(), ex.Message), dtPair);

                CloseFile(dtPair);
            }

            return ec;
        }

        public int GetCurrentFileSize()
        {
            return (IsFileOpen ? CurrentFileInfo : LastFileInfo).FileSize;
        }

        public FileInfo CurrentFileInfo { get { return currentFileInfo; } }
        public FileInfo LastFileInfo { get { return lastFileInfo; } }

        public int CloseCurrentFile(string reason, DateTimeStampPair dtPair = null)
        {
            dtPair = (dtPair ?? DateTimeStampPair.Now).UpdateFileDeltas(fileReferenceDTPair);

            if (IsFileOpen)
                RecordMessage("{0} reason:{1}".CheckedFormat(CurrentMethodName, reason.MapNullOrEmptyTo("[NoReasonGiven]")), dtPair);

            ServicePendingWrites(dtPair);

            if (IsFileOpen)
                WriteFileEndBlockAndCloseFile(dtPair);

            allowImmediateReopen = true;

            return LastFileInfo.FileSize;
        }

        #endregion

        #region private implementation methods

        #region RecordError, RecordMessage

        string RecordError(string errorMesg, DateTimeStampPair dtPair)
        {
            if (!errorMesg.IsNullOrEmpty())
            {
                Log.Debug.Emit("{0}: {1}", CurrentMethodName, errorMesg);

                MessageItem mesgItem = new MessageItem() { mesg = errorMesg, blockTypeID = FixedBlockTypeID.ErrorV1, dtPair = dtPair ?? DateTimeStampPair.Now };

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

        string RecordMessage(string mesg, DateTimeStampPair dtPair)
        {
            if (!mesg.IsNullOrEmpty())
            {
                Log.Debug.Emit("{0}: {1}", CurrentMethodName, mesg);

                MessageItem mesgItem = new MessageItem() { mesg = mesg, blockTypeID = FixedBlockTypeID.MessageV1, dtPair = dtPair ?? DateTimeStampPair.Now };

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

        QpcTimer dstRecheckTimer = new QpcTimer() { TriggerIntervalInSec = 1.0, AutoReset = true }.StartIfNeeded();

        string ActiviateFile(DateTimeStampPair dtPair)
        {
            TimeSpan fileDeltaTimeSpan = TimeSpan.FromSeconds(dtPair.fileDeltaTimeStamp);

            if (IsFileOpen)
            {
                QpcTimeStamp tsNow = dtPair.qpcTimeStamp;

                if (currentFileInfo.FileSize >= setup.NominalMaxFileSize)
                {
                    CloseCurrentFile("size limit {0} bytes reached: '{1}' is {2} bytes and {3:f3} hours)".CheckedFormat(setup.NominalMaxFileSize, currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours));
                }
                else if (fileDeltaTimeSpan > setup.MaxFileRecordingPeriod)
                {
                    CloseCurrentFile("age limit {0:f3} hours reached: '{1}' is {2} bytes and {3:f3} hours (>= {3:f3})".CheckedFormat(setup.MaxFileRecordingPeriod.TotalHours, currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours));
                }
                else if (dstRecheckTimer.GetIsTriggered(tsNow))
                {
                    DateTime dtNow = dtPair.dateTime;

                    if (fileReferenceIsDST != TimeZoneInfo.Local.IsDaylightSavingTime(dtNow)
                        || fileReferenceUTCOffset != TimeZoneInfo.Local.GetUtcOffset(dtNow))
                    {
                        CloseCurrentFile("DST and/or timezone have changed since file was opened: '{0}' is {1} bytes and {2:f3} hours".CheckedFormat(currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours));
                    }
                }
            }

            if (IsFileOpen)
                return "";

            // capture the timestamp and the corresponding dateTime and related information
            {
                TimeZoneInfo localTZ = TimeZoneInfo.Local;

                QpcTimeStamp tsNow = dtPair.qpcTimeStamp;
                DateTime dtNow = dtPair.dateTime;
                bool isDST = localTZ.IsDaylightSavingTime(dtNow);
                TimeSpan utcOffset = localTZ.GetUtcOffset(dtNow);

                // verify that we are allowed to attempt to activate a new file (time since last open sufficient long or allowImmediateReopen is true)            

                if (allowImmediateReopen)
                {
                    allowImmediateReopen = false;
                }
                else if (fileReferenceDTPair == null)
                {
                    // this is the first ActivateFile call
                }
                else if (fileDeltaTimeSpan < setup.MinInterFileCreateHoldoffPeriod)
                {
                    return "ActivateFile failed: minimum inter-create file holdoff period has not been met.";
                }

                // we are now going to use this dtPair value as the fileReferenceDTPair
                dtPair.ClearFileDeltas();

                fileReferenceDTPair = dtPair;
                fileReferenceIsDST = isDST;
                fileReferenceUTCOffset = utcOffset;
            }

            // zero out other file access information prior to creating new file.
            currentFileDeltaTimeStamp = 0.0;

            currentFileInfo.IsActive = false;
            currentFileInfo.FileSize = 0;
            currentFileInfo.SeqNum = seqNumGen.IncrementSkipZero();

            offsetToStartOfFileIndexPayload = 0;
            lastWriteAllTimeStamp = QpcTimeStamp.Zero;
            
            // generate the file name
            string dateTimePart = Dates.CvtToString(fileReferenceDTPair.dateTime, Dates.DateTimeFormat.ShortWithMSec);
            string fileName = "{0}_{1}.mdrf".CheckedFormat(setup.FileNamePrefix, dateTimePart);
            currentFileInfo.FilePath = System.IO.Path.Combine(setup.DirPath, fileName);
            
            // attempt to create/open the file
            string ec = string.Empty;

            try
            {
                int useBufferSize = setup.MaxDataBlockSize.Clip(1024, 65536);

                fs = new FileStream(currentFileInfo.FilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, useBufferSize, FileOptions.None);
            }
            catch (System.Exception ex)
            {
                string mesg = "{0} failed: {1} {2} {3}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.GetType(), ex.Message);

                ec = mesg;
            }

            // generate and write the file header block
            if (ec.IsNullOrEmpty())
            {
                NamedValueSet nvSet = fileHeaderNVS;

		        nvSet.SetValue("lib.Type", Constants.LibInfo.Type);
                nvSet.SetValue("lib.Name", Constants.LibInfo.Name);
                nvSet.SetValue("lib.Version", Constants.LibInfo.Version);

		        nvSet.SetValue("setup.DirPath", setup.DirPath);
		        nvSet.SetValue("setup.ClientName", setup.ClientName);
		        nvSet.SetValue("setup.FileNamePrefix", setupOrig.FileNamePrefix);
		        nvSet.SetValue("setup.CreateDirectoryIfNeeded", setup.CreateDirectoryIfNeeded);
		        nvSet.SetValue("setup.MaxDataBlockSize", setup.MaxDataBlockSize);
		        nvSet.SetValue("setup.NominalMaxFileSize", setup.NominalMaxFileSize);
		        nvSet.SetValue("setup.FileIndexNumRows", setup.FileIndexNumRows);
		        nvSet.SetValue("setup.MaxFileRecordingPeriodInSeconds", setup.MaxFileRecordingPeriod.TotalSeconds);
		        nvSet.SetValue("setup.MinInterFileCreateHoldoffPeriodInSeconds", setup.MinInterFileCreateHoldoffPeriod.TotalSeconds);
		        nvSet.SetValue("setup.MinNominalFileIndexWriteIntervalInSeconds", setup.MinNominalFileIndexWriteInterval.TotalSeconds);
		        nvSet.SetValue("setup.MinNominalWriteAllIntervalInSeconds", setup.MinNominalWriteAllInterval.TotalSeconds);
		        nvSet.SetValue("setup.I8Offset", setup.I8Offset);
		        nvSet.SetValue("setup.I4Offset", setup.I4Offset);
		        nvSet.SetValue("setup.I2Offset", setup.I2Offset);

                try
                {
                    string hostName = System.Net.Dns.GetHostName();
                    nvSet.SetValue("HostName", hostName);
                }
                catch
                { }

		        // generate and write file header - note: the blockDeltaTimeStamp is zero on the file header data block by definitions
		        StartDataBlock(FixedBlockTypeID.FileHeaderV1, dtPair, FileIndexRowFlagBits.ContainsHeaderOrMetaData);

		        // append above specified NVItemSet as NVS1
                dataBlockBuffer.payloadDataList.AppendWithIH(nvSet);

                // append the client provided NVItemSet
                dataBlockBuffer.payloadDataList.AppendWithIH(setup.ClientNVS.MapNullToEmpty());

                ec = WriteDataBlock(dtPair);
            }

            	// write file index - because this is the first write, it also defines where the file index block is in the file.
            if (ec.IsNullOrEmpty())
                ec = UpdateAndWriteFileIndex(doFlush: false, dtPair: dtPair);

         	// generate DateTime block immediately after index
            if (ec.IsNullOrEmpty())
                ec = WriteDateTimeBlock(dtPair);

            // generate and write metadata blocks
            if (ec.IsNullOrEmpty())
            {
                bool startNewBlock = true;
                int triggerNewBlockAfterSize = (setup.MaxDataBlockSize >> 1);

                foreach (TrackerCommon trackerItem in allMetaDataItemsArray)
                {
                    if (startNewBlock)
                    {
                        StartDataBlock(FixedBlockTypeID.MetaDataV1, dtPair, FileIndexRowFlagBits.ContainsHeaderOrMetaData);
                        startNewBlock = false;
                    }

                    dataBlockBuffer.payloadDataList.AppendWithIH(trackerItem.nvSet);

                    if (dataBlockBuffer.payloadDataList.Count >= triggerNewBlockAfterSize)
                    {
                        if (!(ec = WriteDataBlock(dtPair)).IsNullOrEmpty())
                            break;
                        startNewBlock = true;
                    }
                }

                if (ec.IsNullOrEmpty() && dataBlockBuffer.payloadDataList.Count > 0)
                    ec = WriteDataBlock(dtPair);
            }

            // service pending writes to record any messages or errors into the file (if it is still open)
            if (ec.IsNullOrEmpty())
                ec = ServicePendingWrites(dtPair);

            // finaly flush the file
            if (ec.IsNullOrEmpty())
                ec = Flush(FlushFlags.File);

            if (ec.IsNullOrEmpty())
                currentFileInfo.IsActive = true;

            if (!ec.IsNullOrEmpty())
                return RecordError(ec, dtPair);

            // clear the dropped counters after all messages have been emitted successfully
            droppedCounts = new DroppedCounts();

            return "";
        }

        bool IsFileOpen { get { return fs != null; } }

        void CloseFile(DateTimeStampPair dtPair)
        {
            if (fs != null)
            {
                currentFileInfo.IsActive = false;
                lastFileInfo = currentFileInfo;

                try
                {
                    fs.Close();
                    Fcns.DisposeOfObject(ref fs);
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit(RecordError("{0} failed: {1} {2} {3}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.GetType(), ex.Message), dtPair));
                    fs = null;
                }
            }
        }

        string WriteDateTimeBlock(DateTimeStampPair dtPair)
        {
            QpcTimeStamp qpcTimeStamp = dtPair.qpcTimeStamp;
            bool isDST = fileReferenceIsDST;
            TimeSpan utcOffset = fileReferenceUTCOffset;
            TimeZoneInfo localTZ = TimeZoneInfo.Local;

            StartDataBlock(FixedBlockTypeID.DateTimeV1, dtPair, FileIndexRowFlagBits.ContainsDateTime);

            dateTimeBlockNVSet.SetValue("BlockDeltaTimeStamp", dtPair.fileDeltaTimeStamp);
            dateTimeBlockNVSet.SetValue("QPCTime", dtPair.qpcTimeStamp);
            dateTimeBlockNVSet.SetValue("UTCTimeSince1601", dtPair.utcTimeSince1601);      // aka FTime with time measured in seconds
            dateTimeBlockNVSet.SetValue("TimeZoneOffset", localTZ.BaseUtcOffset.TotalSeconds);
            dateTimeBlockNVSet.SetValue("DSTIsActive", isDST);
            dateTimeBlockNVSet.SetValue("DSTBias", (utcOffset - localTZ.BaseUtcOffset).TotalSeconds);
            dateTimeBlockNVSet.SetValue("TZName0", localTZ.StandardName);
            dateTimeBlockNVSet.SetValue("TZName1", localTZ.DaylightName);

            dataBlockBuffer.payloadDataList.AppendWithIH(dateTimeBlockNVSet);

            string ec = WriteDataBlock(dtPair);

            return ec;
        }

        string WriteFileEndBlockAndCloseFile(DateTimeStampPair dtPair)
        {
            StartDataBlock(FixedBlockTypeID.FileEndV1, dtPair, FileIndexRowFlagBits.None);

            string ec = WriteDataBlock(dtPair);

            if (ec.IsNullOrEmpty())
                ec = UpdateAndWriteFileIndex(doFlush: false, dtPair: dtPair);
            else
                UpdateAndWriteFileIndex(doFlush: false, dtPair: dtPair);

            CloseFile(dtPair);

            return ec;
        }

        void StartOccurrenceDataBlock(OccurrenceTracker occurrenceTracker, DateTimeStampPair dtPair, bool preIncrementSeqNum = true)
        {
            StartDataBlock(occurrenceTracker.FileID, dtPair, occurrenceTracker.fileIndexRowFlagBits);
            dataBlockBuffer.fileIndexUserRowFlagBits = occurrenceTracker.occurrenceInfo.FileIndexUserRowFlagBits;

            dataBlockBuffer.payloadDataList.AppendU8Auto(preIncrementSeqNum ? ++(occurrenceTracker.seqNum) : occurrenceTracker.seqNum);
        }

        void StartGroupDataBlock(GroupTracker groupTracker, DateTimeStampPair dtPair, FileIndexRowFlagBits fileIndexRowFlagBits, GroupBlockFlagBits groupBlockFlagBits, bool preIncrementSeqNum = false)
        {
            StartDataBlock(groupTracker.FileID, dtPair, fileIndexRowFlagBits);
            dataBlockBuffer.fileIndexUserRowFlagBits = groupTracker.groupInfo.FileIndexUserRowFlagBits;

            dataBlockBuffer.payloadDataList.AppendU8Auto(preIncrementSeqNum ? ++(groupTracker.seqNum) : groupTracker.seqNum);

            if ((fileIndexRowFlagBits & FileIndexRowFlagBits.ContainsStartOfFullGroup) != FileIndexRowFlagBits.None)
                groupBlockFlagBits |= GroupBlockFlagBits.IsStartOfFullGroup;

            // if this is an empty group then we do not include the groupBlockFlagBits byte.
            if ((groupBlockFlagBits & GroupBlockFlagBits.IsEmptyGroup) == GroupBlockFlagBits.None)
                dataBlockBuffer.payloadDataList.Add(unchecked((byte) groupBlockFlagBits));
        }

        string ServicePendingWrites(DateTimeStampPair dtPair)
        {
            string ec = string.Empty;

            while (ec.IsNullOrEmpty())
            {
                if (errorQueueCount <= MessageItem.maxQueueSize && droppedCounts.total > 0)
                {
                    MessageItem mesgItem = new MessageItem()
                    {
                        blockTypeID = FixedBlockTypeID.ErrorV1,
                        dtPair = new DateTimeStampPair(),
                        mesg = "Note dropped items: {0}".CheckedFormat(droppedCounts.ToString()),
                    };

                    errorQueue.Enqueue(mesgItem);
                    errorQueueCount++;

                    Log.Debug.Emit(mesgItem.mesg);

                    droppedCounts = new DroppedCounts();
                }

                if (!IsFileOpen)
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

                        takeFromErrorQueue = (errorItem.dtPair.qpcTimeStamp <= mesgItem.dtPair.qpcTimeStamp);
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
                        OccurrenceTracker oTracker = (writeItem.blockTypeID == FixedBlockTypeID.ErrorV1) ? errorOccurrenceTracker : mesgOccurrenceTracker;

                        writeItem.dtPair.UpdateFileDeltas(fileReferenceDTPair);

                        StartOccurrenceDataBlock(oTracker, writeItem.dtPair);

                        // append the utcTime at which this message was recorded (it may be from before the block is being written to the file...) followed by the message string itself
                        dataBlockBuffer.payloadDataList.AppendRaw(writeItem.dtPair.utcTimeSince1601.CastToU8());
                        dataBlockBuffer.payloadDataList.AppendWithIH(new ValueContainer(writeItem.mesg));

                        ec = WriteDataBlock(dtPair);
                    }
                }
            }

            // check if it is time to write the file index out.
            if (fileIndexData.fileIndexChanged && !writeFileIndexNow)
            {
                QpcTimeStamp tsNow = QpcTimeStamp.Now;

                if (lastFileIndexWriteTimeStamp.IsZero || (tsNow - lastFileIndexWriteTimeStamp) >= setup.MinNominalFileIndexWriteInterval)
                    writeFileIndexNow = true;
            }

            if (writeFileIndexNow && IsFileOpen)
            {
                ec = UpdateAndWriteFileIndex(doFlush: true, dtPair: dtPair);
            }

            return ec;
        }

        string UpdateAndWriteFileIndex(bool doFlush, DateTimeStampPair dtPair)
        {
            if (!IsFileOpen)
                return RecordError("Cannot {0}: no file is open".CheckedFormat(CurrentMethodName), dtPair);

            if (currentFileInfo.FileSize == 0)
                return RecordError("Cannot {0}: file is empty (file start block has not been written yet)".CheckedFormat(CurrentMethodName), dtPair);

            bool thisIsFirstWrite = (offsetToStartOfFileIndexPayload == 0);

            if (!thisIsFirstWrite)
                fileIndexData.UpdateDataBuffer();
            else
            {
                // generate a data block header for the file index and 
                fileIndexData.SetupAndClear();

                StartDataBlock(FixedBlockTypeID.FileIndexV1, fileReferenceDTPair, FileIndexRowFlagBits.None);

                dataBlockBuffer.GenerateHeader(overridePayloadCount: fileIndexData.fileIndexDataBuffer.Length);        // override the payload count with the length we will write

                offsetToStartOfFileIndexPayload = (currentFileInfo.FileSize + dataBlockBuffer.generatedHeaderList.Count);
            }

            try
            {
                if (!thisIsFirstWrite)
                    fs.Seek(offsetToStartOfFileIndexPayload, SeekOrigin.Begin);
                else
                    fs.Write(dataBlockBuffer.generatedHeaderList.ToArray(), 0, dataBlockBuffer.generatedHeaderList.Count);

                fs.Write(fileIndexData.fileIndexDataBuffer, 0, fileIndexData.fileIndexDataBuffer.Length);

                if (!thisIsFirstWrite)
                    fs.Seek(currentFileInfo.FileSize, SeekOrigin.Begin);
                else
                    currentFileInfo.FileSize = offsetToStartOfFileIndexPayload + fileIndexData.fileIndexDataBuffer.Length;

                if (doFlush)
                    fs.Flush();
            }
            catch (System.Exception ex)
            {
                CloseFile(dtPair);

                return RecordError("{0} failed: {1} {2} {3}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.GetType(), ex.Message), dtPair);
            }

            lastFileIndexWriteTimeStamp = QpcTimeStamp.Now;
            fileIndexData.fileIndexChanged = false;
            writeFileIndexNow = false;

            return string.Empty;
        }

        string WriteDataBlock(DateTimeStampPair dtPair)
        {
            if (!IsFileOpen)
                return RecordError("Cannot {0}: no file is open".CheckedFormat(CurrentMethodName), dtPair);

            try
            {
                // check if the dataBlockBuffer's timestamp is different than the last written one.  If so then generate and write a TimeStampUpdate block
                if (currentFileDeltaTimeStamp != dataBlockBuffer.dtPair.fileDeltaTimeStamp)
                {
                    timeStampUpdateDBB.payloadDataList.Clear();
                    timeStampUpdateDBB.payloadDataList.AppendRaw(dataBlockBuffer.dtPair.fileDeltaTimeStamp.CastToU8());

                    timeStampUpdateDBB.GenerateHeader();

                    int tsuHeaderCount = timeStampUpdateDBB.generatedHeaderList.Count;
                    int tsuPayloadCount = timeStampUpdateDBB.payloadDataList.Count;

                    fs.Write(timeStampUpdateDBB.generatedHeaderList.ToArray(), 0, tsuHeaderCount);
                    fs.Write(timeStampUpdateDBB.payloadDataList.ToArray(), 0, tsuPayloadCount);

                    currentFileInfo.FileSize += tsuHeaderCount + tsuPayloadCount;

                    // only update the currentFileDeltaTimeStampI8 after the corresponding block has been written.
                    currentFileDeltaTimeStamp = dataBlockBuffer.dtPair.fileDeltaTimeStamp;
                }

                // generate the dataBlockBuffer's header.  This is required so that we know its total length (used in the fileIndexData.LastBlockInfo)
                dataBlockBuffer.GenerateHeader();

                // update the file index with the current record and mark that it eventually needs to be written
                int fileOffsetToStartOfThisBlock = currentFileInfo.FileSize;

                fileIndexData.RecordWriteDataBlock(dataBlockBuffer, setup, fileOffsetToStartOfThisBlock);

                // write the header and the payload blocks
                {
                    int dbbHheaderCount = dataBlockBuffer.generatedHeaderList.Count;
                    int dbbPayloadCount = dataBlockBuffer.payloadDataList.Count;

                    fs.Write(dataBlockBuffer.generatedHeaderList.ToArray(), 0, dbbHheaderCount);

                    if (dbbPayloadCount > 0)
                        fs.Write(dataBlockBuffer.payloadDataList.ToArray(), 0, dbbPayloadCount);

                    currentFileInfo.FileSize += dbbHheaderCount + dbbPayloadCount;
                }

                dataBlockBuffer.Clear();
            }
            catch (System.Exception ex)
            {
                CloseFile(dtPair);

                return RecordError("{0} failed: {1} {2} {3}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.GetType(), ex.Message), dtPair);
            }

            return string.Empty;
        }

        void StartDataBlock(FixedBlockTypeID blockTypeID, DateTimeStampPair dtPair, FileIndexRowFlagBits fileIndexRowFlagBits)
        {
            StartDataBlock(unchecked((Int32)blockTypeID), dtPair, fileIndexRowFlagBits);
        }

        void StartDataBlock(Int32 blockTypeID32, DateTimeStampPair dtPair, FileIndexRowFlagBits fileIndexRowFlagBits)
        {
            dataBlockBuffer.Clear();

            dataBlockBuffer.fileIndexRowFlagBits = fileIndexRowFlagBits;
            dataBlockBuffer.blockTypeID32 = blockTypeID32;
            dataBlockBuffer.dtPair = dtPair;

            FixedBlockTypeID fixedBlockTypeID = unchecked((FixedBlockTypeID) blockTypeID32);

            switch (fixedBlockTypeID)
            {
                case FixedBlockTypeID.FileHeaderV1:
                case FixedBlockTypeID.FileIndexV1:
                case FixedBlockTypeID.MetaDataV1:
                    dataBlockBuffer.fileIndexRowFlagBits |= FileIndexRowFlagBits.ContainsHeaderOrMetaData;
                    break;
                case FixedBlockTypeID.ErrorV1:
                    dataBlockBuffer.fileIndexRowFlagBits |= (FileIndexRowFlagBits.ContainsHighPriorityOccurrence | FileIndexRowFlagBits.ContainsOccurrence);
                    break;
                case FixedBlockTypeID.MessageV1:
                    dataBlockBuffer.fileIndexRowFlagBits |= FileIndexRowFlagBits.ContainsOccurrence;
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Internals: classes, structs

        class MessageItem
        {
            public FixedBlockTypeID blockTypeID;
            public DateTimeStampPair dtPair;            // when the message was enqueued.
            public string mesg;

            public const int maxQueueSize = 100;
        }

        class FileIndexData : TFileIndexInfo<FileIndexRow, FileIndexLastBlockInfo>
        {
            public byte[] fileIndexDataBuffer;

            public bool fileIndexChanged = false;

            public const int maxFileIndexRows = 65536;

            public FileIndexData(SetupInfo setupInfo) 
                : base(setupInfo) 
            {}

            public void SetupAndClear()
            {
                int fileIndexStartOffset = 0;
                int lastBlockInfoStartOffset = LastBlockInfo.offsetToStartInIndexPayloadBuffer = fileIndexStartOffset + 4 + 4;

                int lastBlockInfoSerializedSize = FileIndexLastBlockInfo.SerializedSize;
                int fileIndexRowSerializedSize = FileIndexRow.SerializedSize;
                int firstRowStartOffset = (lastBlockInfoStartOffset + lastBlockInfoSerializedSize);

                for (int rowIdx = 0; rowIdx < NumRows; rowIdx++)
                {
                    FileIndexRow fileIndexRow = FileIndexRowArray[rowIdx].Clear();
                    fileIndexRow.offsetToStartOfRow = (firstRowStartOffset + rowIdx * fileIndexRowSerializedSize);
                    fileIndexRow.rowContentsChanged = true;
                }

                fileIndexDataBuffer = new byte[firstRowStartOffset + NumRows * fileIndexRowSerializedSize];

                Utils.Data.Unpack((UInt32)NumRows, fileIndexDataBuffer, fileIndexStartOffset + 0);
                Utils.Data.Unpack((UInt32)RowSizeDivisor, fileIndexDataBuffer, fileIndexStartOffset + 4);

                UpdateDataBuffer();     // use UpdateDataBuffer to fill in the rest of the data.

                fileIndexChanged = true;
            }

            public void UpdateDataBuffer()
            {
                LastBlockInfo.UpdateDataBuffer(fileIndexDataBuffer);

                foreach (FileIndexRow fileIndexRow in FileIndexRowArray)
                {
                    if (fileIndexRow.rowContentsChanged)
                    {
                        fileIndexRow.UpdateDataBuffer(fileIndexDataBuffer);
                        fileIndexRow.rowContentsChanged = false;
                    }
                }
            }

            public void RecordWriteDataBlock(DataBlockBuffer dataBlockBuffer, SetupInfo setupInfo, int fileOffsetToStartOfThisBlock)
            {
                int fileIndexRowIdx = ((RowSizeDivisor > 0) ? (fileOffsetToStartOfThisBlock / RowSizeDivisor) : 0).Clip(0, NumRows - 1);

                FileIndexRow fileIndexRow = FileIndexRowArray.SafeAccess(fileIndexRowIdx);

                if (fileIndexRow != null)
                    fileIndexRow.RecordWriteDataBlock(dataBlockBuffer, fileOffsetToStartOfThisBlock);

                LastBlockInfo.FileOffsetToStartOfBlock = fileOffsetToStartOfThisBlock;
                LastBlockInfo.BlockTotalLength = dataBlockBuffer.generatedHeaderList.Count + dataBlockBuffer.payloadDataList.Count;
                LastBlockInfo.BlockTypeID32 = dataBlockBuffer.blockTypeID32;
                LastBlockInfo.BlockDeltaTimeStamp = dataBlockBuffer.dtPair.fileDeltaTimeStamp;

                if (!fileIndexChanged)
                    fileIndexChanged = true;
            }
        }

        class FileIndexRow : FileIndexRowBase
        {
            public new FileIndexRow Clear()
            {
                base.Clear();
                rowContentsChanged = false;
                offsetToStartOfRow = 0;

                return this;
            }

            public bool rowContentsChanged;     // this is not serialized
            public int offsetToStartOfRow;

            internal void UpdateDataBuffer(byte[] fileIndexDataBuffer)
            {
                Utils.Data.Unpack(FileIndexRowFlagBitsU4, fileIndexDataBuffer, offsetToStartOfRow + 0);
                Utils.Data.Unpack(unchecked((UInt32)FileOffsetToStartOfFirstBlock), fileIndexDataBuffer, offsetToStartOfRow + 4);
                Utils.Data.Unpack(FileIndexUserRowFlagBits, fileIndexDataBuffer, offsetToStartOfRow + 8);
                Utils.Data.Unpack(FirstBlockUtcTimeSince1601.CastToU8(), fileIndexDataBuffer, offsetToStartOfRow + 16);
                Utils.Data.Unpack(FirstBlockDeltaTimeStamp.CastToU8(), fileIndexDataBuffer, offsetToStartOfRow + 24);
                Utils.Data.Unpack(LastBlockDeltaTimeStamp.CastToU8(), fileIndexDataBuffer, offsetToStartOfRow + 32);
            }

            internal void RecordWriteDataBlock(DataBlockBuffer dataBlockBuffer, int blockStartOffset)
            {
                bool thisIsFirstBlockInRow = IsEmpty;

                FileIndexRowFlagBitsU4 |= unchecked((UInt16)dataBlockBuffer.fileIndexRowFlagBits);
                FileIndexUserRowFlagBits |= dataBlockBuffer.fileIndexUserRowFlagBits;

                double fileDeltaTimeStamp = dataBlockBuffer.dtPair.fileDeltaTimeStamp;

                if (thisIsFirstBlockInRow)
                {
                    FileOffsetToStartOfFirstBlock = blockStartOffset;
                    FirstBlockUtcTimeSince1601 = dataBlockBuffer.dtPair.utcTimeSince1601;
                    FirstBlockDeltaTimeStamp = fileDeltaTimeStamp;
                }

                LastBlockDeltaTimeStamp = fileDeltaTimeStamp;

                if (!rowContentsChanged)
                    rowContentsChanged = true;
            }
        }

        class FileIndexLastBlockInfo : FileIndexLastBlockInfoBase
        {
            public int offsetToStartInIndexPayloadBuffer;

            public void UpdateDataBuffer(byte[] fileIndexDataBuffer)
            {
                Utils.Data.Unpack(unchecked((UInt32)FileOffsetToStartOfBlock), fileIndexDataBuffer, offsetToStartInIndexPayloadBuffer + 0);
                Utils.Data.Unpack(unchecked((UInt32)BlockTotalLength), fileIndexDataBuffer, offsetToStartInIndexPayloadBuffer + 4);
                Utils.Data.Unpack(unchecked((UInt32)BlockTypeID32), fileIndexDataBuffer, offsetToStartInIndexPayloadBuffer + 8);
                Utils.Data.Unpack(BlockDeltaTimeStamp.CastToU8(), fileIndexDataBuffer, offsetToStartInIndexPayloadBuffer + 12);
            }
        }

        class DataBlockBuffer
        {
            public FileIndexRowFlagBits fileIndexRowFlagBits;
            public UInt64 fileIndexUserRowFlagBits;
            public DateTimeStampPair dtPair;

            public Int32 blockTypeID32; // U8Auto coded

            public FixedBlockTypeID FixedBlockTypeID { get { return unchecked((FixedBlockTypeID)blockTypeID32); } set { blockTypeID32 = unchecked((int)value); } }

            public List<byte> generatedHeaderList = new List<byte>();

            public List<byte> payloadDataList = new List<byte>();

            public DataBlockBuffer()
            { }

            public DataBlockBuffer(SetupInfo setupInfo)
                : this(setupInfo.MaxDataBlockSize)
            { }

            public DataBlockBuffer(int initialPayloadDataCapacity)
            {
                if (payloadDataList.Capacity < initialPayloadDataCapacity)
                    payloadDataList.Capacity = initialPayloadDataCapacity;
            }

            /// <summary>
            /// Clears the generatedHeader and then sets it to contain the blockTypeID and payloadDataList.Count (or overridePayloadCount if non-zero) both using U4Auto coding
            /// </summary>
            public void GenerateHeader(int overridePayloadCount = 0)
            {
                if (generatedHeaderList.Count != 0)
                    generatedHeaderList.Clear();

                generatedHeaderList.AppendU4Auto(unchecked((UInt32)blockTypeID32));
                generatedHeaderList.AppendU4Auto(unchecked((UInt32)(overridePayloadCount.MapDefaultTo(payloadDataList.Count))));
            }

            /// <summary>
            /// Clear data block buffer contents (including information, header buffer, and payload buffer).
            /// </summary>
            public void Clear()
            {
                fileIndexRowFlagBits = FileIndexRowFlagBits.None;
                fileIndexUserRowFlagBits = 0;
                blockTypeID32 = 0;
                dtPair = null;

                if (generatedHeaderList.Count != 0)
                    generatedHeaderList.Clear();

                if (payloadDataList.Count != 0)
                    payloadDataList.Clear();
            }
        }

        class TrackerCommon
        {
            public string Name { get { return mdci.Name; } }
            public string Comment { get { return mdci.Comment; } }
            public MetaDataCommonInfo mdci;
            public MDItemType mdItemType;
            public int FileID { get { return mdci.FileID; } set { mdci.FileID = value; } }
            public int ClientID { get { return mdci.ClientID; } set { mdci.ClientID = value; } }
            public NamedValueSet nvSet = new NamedValueSet();
            public UInt64 seqNum;
            public ItemFormatCode ifc = ItemFormatCode.None;
            public ContainerStorageType inferredCST = ContainerStorageType.None;

            public TrackerCommon(MDItemType mdItemType, MetaDataCommonInfo mdci)
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
		        switch (mdItemType)
		        {
		        case MDItemType.Source:
                case MDItemType.Group:
                case MDItemType.Occurrence:
                    nvSet.SetValue("ItemType", mdItemType.ToString()); 
                    break;
		        default:
                    nvSet.SetValue("ItemTypeI4", unchecked((int)mdItemType));
                    break;
		        }

                nvSet.SetValue("Name", Name);
                if (Comment != null)
                    nvSet.SetValue("Comment", Comment);
                nvSet.SetValue("FileID", FileID);
                nvSet.SetValue("ClientID", ClientID);
                nvSet.SetValue("IFC", ifc);
                if (ifc != ItemFormatCode.None)
                    inferredCST = ifc.ConvertToContainerStorageType();
            }
        }

        class SourceTracker : TrackerCommon
        {
            public GroupPointInfo groupPointInfo;
            public GroupTracker groupTracker;
            public SetupInfo setupInfo;

            public bool touched;
            public ValueContainer lastServicedValue;

            public SourceTracker(GroupPointInfo gpi, GroupTracker gt) 
                : base(MDItemType.Source, gpi)
            {
                groupPointInfo = gpi;
                groupTracker = gt;
                setupInfo = gt.setupInfo;
                ifc = gpi.ValueCST.ConvertToItemFormatCode();
                AddClientNVSItems();

                if (gpi.ValueSourceIVA == null && gpi.ValueSourceIVAFactoryIVI != null)
                {
                    gpi.ValueSourceIVA = gpi.ValueSourceIVAFactoryIVI.GetValueAccessor(gpi.Name);
                    gpi.VC = gpi.ValueSourceIVA.ValueContainer;
                }
            }

            public bool Service()
            {
                // if we do not have an associated group tracker or the group is neither touched, to be written, nor flagged for write all) then there is no need to service this source further.
                if (groupTracker == null || groupPointInfo == null || !(groupTracker.touched || groupTracker.writeAll))
                    return false;

                IValueAccessor iva = groupPointInfo.ValueSourceIVA;
                if (iva != null)
                {
                    if (iva.IsUpdateNeeded)
                    {
                        groupPointInfo.VC = iva.Update().ValueContainer;
                        lastServicedValue = groupPointInfo.VC;
                        if (!touched)
                            touched = true;
                    }
                }
                else
                {
                    bool different = groupPointInfo.VCIsUsable && (groupTracker.writeAll || !lastServicedValue.IsEqualTo(groupPointInfo.VC));

                    if (different)
                        lastServicedValue.CopyFrom(groupPointInfo.VC);

                    if (different && !touched)
                        touched = true;
                }

                if (touched)
                    groupTracker.numTouchedSources++;

                return touched;
            }

            public void AppendValue(List<byte> byteArrayBuilder, bool clearTouched = true)
            {
                switch (ifc)
                {
                    case ItemFormatCode.Bo: 
                        byteArrayBuilder.AppendRaw(unchecked((byte) (lastServicedValue.u.b ? 1 : 0))); 
                        break;
                    case ItemFormatCode.Bi: 
                        byteArrayBuilder.AppendRaw(lastServicedValue.u.bi); 
                        break;
                    case ItemFormatCode.I1: 
                        byteArrayBuilder.AppendRaw(lastServicedValue.u.i8); 
                        break;
                    case ItemFormatCode.I2:
                        byteArrayBuilder.AppendU2Auto(setupInfo.ConvertToU2WithI2Offset(lastServicedValue.u.i16)); 
                        break;
                    case ItemFormatCode.I4: 
                        byteArrayBuilder.AppendU4Auto(setupInfo.ConvertToU4WithI4Offset(lastServicedValue.u.i32)); 
                        break;
                    case ItemFormatCode.I8: 
                        byteArrayBuilder.AppendU8Auto(setupInfo.ConvertToU8WithI8Offset(lastServicedValue.u.i64)); 
                        break;
                    case ItemFormatCode.U1: 
                        byteArrayBuilder.AppendRaw(lastServicedValue.u.u8); 
                        break;
                    case ItemFormatCode.U2: 
                        byteArrayBuilder.AppendU2Auto(lastServicedValue.u.u16); 
                        break;
                    case ItemFormatCode.U4: 
                        byteArrayBuilder.AppendU4Auto(lastServicedValue.u.u32); 
                        break;
                    case ItemFormatCode.U8: 
                        byteArrayBuilder.AppendU8Auto(lastServicedValue.u.u64); 
                        break;
                    case ItemFormatCode.F4: 
                        byteArrayBuilder.AppendRaw(lastServicedValue.u.f32); 
                        break;
                    case ItemFormatCode.F8: 
                        byteArrayBuilder.AppendRaw(lastServicedValue.u.f64); 
                        break;
                    case ItemFormatCode.A: 
                        byteArrayBuilder.AppendWithIH(lastServicedValue.o as string); 
                        break;
                    default:
                        byteArrayBuilder.AppendWithIH(lastServicedValue);
                        break;
                }

                if (touched && clearTouched)
                    touched = false;
            }
        }

        class GroupTracker : TrackerCommon
        {
            public GroupInfo groupInfo;
            public SetupInfo setupInfo;
            public SourceTracker[] sourceTrackerArray;
            public int sourceTrackerArrayLength;
            public double oneOverSourceTrackerArrayLength;

            public bool writeAll;
            public bool touched;
            public int numTouchedSources;

            public bool WriteAsFullGroup { get { return writeAll || (numTouchedSources * oneOverSourceTrackerArrayLength >= 0.80); } }
            public bool WriteAsEmptyGroup { get { return !writeAll && (numTouchedSources == 0); } }
            public bool WriteAsSparseGroup { get { return !(numTouchedSources == 0) && !WriteAsFullGroup; } }

            public byte [] updateMaskByteArray;
            public int updateMaskByteArraySize;

            public IValueAccessor[] ivaArray;

            public GroupTracker(GroupInfo gi, SetupInfo si)
                : base(MDItemType.Group, gi)
            {
                groupInfo = gi;
                setupInfo = si; 
                AddClientNVSItems();

                sourceTrackerArray = (groupInfo.GroupPointInfoArray ?? emptyPointGroupInfoArray).Select(pointInfo => new SourceTracker(pointInfo, this)).ToArray();
                sourceTrackerArrayLength = sourceTrackerArray.Length;
                oneOverSourceTrackerArrayLength = (sourceTrackerArrayLength > 0 ? (1.0 / sourceTrackerArrayLength) : 0);

                updateMaskByteArraySize = ((sourceTrackerArrayLength + 7) >> 3);
                updateMaskByteArray = new byte [updateMaskByteArraySize];

                if (gi.UsePointIVAsForTouched)
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
                    else if (groupInfo.UsePointIVAsForTouched && ivaArray.IsUpdateNeeded())
                        touched = true;
                }
            }

            public override void Setup()
            {
                base.Setup();

                nvSet.SetValue("ItemList", string.Join(",", sourceTrackerArray.Select(st => st.FileID.ToString()).ToArray()));
                nvSet.SetValue("FileIndexUserRowFlagBits", groupInfo.FileIndexUserRowFlagBits);
            }

            /// <summary>
            /// Clears touched, writeAll flags, and numTouchedSources counter
            /// </summary>
            public void NoteWritten()
            {
                touched = false;
                writeAll = false;
            }
        }

        class OccurrenceTracker : TrackerCommon
        {
            public OccurrenceInfo occurrenceInfo;
            public FileIndexRowFlagBits fileIndexRowFlagBits;

            public OccurrenceTracker(OccurrenceInfo oi)
                : base(MDItemType.Occurrence, oi)
            {
                occurrenceInfo = oi;
                AddClientNVSItems();

                ifc = occurrenceInfo.ContentCST.ConvertToItemFormatCode();

                fileIndexRowFlagBits = FileIndexRowFlagBits.ContainsOccurrence | (oi.IsHighPriority ? FileIndexRowFlagBits.ContainsHighPriorityOccurrence : FileIndexRowFlagBits.None);
            }

            public override void Setup()
            {
                base.Setup();

                nvSet.SetValue("HighPriority", occurrenceInfo.IsHighPriority);
                nvSet.SetValue("FileIndexUserRowFlagBits", occurrenceInfo.FileIndexUserRowFlagBits);
            }
        }

        private struct DroppedCounts
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

        #region Internals:  fields

        SetupInfo setupOrig;
        SetupInfo setup;

        AtomicInt32 seqNumGen = new AtomicInt32();

        System.IO.FileStream fs;
        DateTimeStampPair fileReferenceDTPair = null;
        bool fileReferenceIsDST;
        TimeSpan fileReferenceUTCOffset;
        bool allowImmediateReopen;

        double currentFileDeltaTimeStamp;

        FileInfo currentFileInfo = new FileInfo();
        FileInfo lastFileInfo = new FileInfo();

        FileIndexData fileIndexData;
        int offsetToStartOfFileIndexPayload;
        QpcTimeStamp lastFileIndexWriteTimeStamp;
        bool writeFileIndexNow;

        QpcTimeStamp lastWriteAllTimeStamp;

        NamedValueSet dateTimeBlockNVSet = new NamedValueSet();

        DataBlockBuffer dataBlockBuffer;
        DataBlockBuffer timeStampUpdateDBB = new DataBlockBuffer(9) { FixedBlockTypeID = FixedBlockTypeID.TimeStampUpdateV1 };

        NamedValueSet fileHeaderNVS = new NamedValueSet();

        SourceTracker [] sourceTrackerArray;
        GroupTracker [] groupTrackerArray;
        OccurrenceTracker[] occurrenceTrackerArray;
        TrackerCommon[] allMetaDataItemsArray;     // in order of source, group, occurrence

        OccurrenceTracker errorOccurrenceTracker = new OccurrenceTracker(new OccurrenceInfo() { Name = "Error", ContentCST = ContainerStorageType.String, IsHighPriority = true }) { FileID = unchecked((int)FixedBlockTypeID.ErrorV1), mdItemType = MDItemType.Occurrence, fileIndexRowFlagBits = FileIndexRowFlagBits.ContainsOccurrence | FileIndexRowFlagBits.ContainsHighPriorityOccurrence };
        OccurrenceTracker mesgOccurrenceTracker = new OccurrenceTracker(new OccurrenceInfo() { Name = "Message", ContentCST = ContainerStorageType.String, IsHighPriority = false }) { FileID = unchecked((int)FixedBlockTypeID.MessageV1), mdItemType = MDItemType.Occurrence, fileIndexRowFlagBits = FileIndexRowFlagBits.ContainsOccurrence };

        Queue<MessageItem> errorQueue = new Queue<MessageItem>(MessageItem.maxQueueSize);
        int errorQueueCount;
        Queue<MessageItem> mesgQueue = new Queue<MessageItem>(MessageItem.maxQueueSize);
        int mesgQueueCount;

        DroppedCounts droppedCounts = new DroppedCounts();

        #endregion
    }

    #endregion
}

//-------------------------------------------------------------------
