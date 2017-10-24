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
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
using MosaicLib.Utils.StringMatching;


namespace MosaicLib.PartsLib.Tools.MDRF.Writer
{
    #region Constants (especially LibInfo and related version information)

    public static partial class Constants
    {
        /// <remarks>
        /// Version history:
        /// 1.0.0 (2016-09-01) : First CR version.
        /// 1.0.1 (2016-09-24) : API extensions to improve usability, modified RecordGroups to trigger write of file index any time RecordGroups does a writeall.
        /// 1.0.2 (2016-10-02) : Changed default Flush flags to All (from File)
        /// 1.0.3 (2017-04-01) : Added AdvanceOnDayBoundary option with 10 minute holdoff.  Changed default FileIndexNumRows to 2048 (from 1024).  Added optional enableAPILocking flat for MDRFWriter constructor to support MT use of public MDRFWriter api.  Added writeAll option to RecordOccurrence variants
        /// 1.0.4 (2017-04-04) : IMDRFWriter API Usability improvements.  Support for post construction addition of groups and occurrences to MDRFWriter objects.  GroupInfo refactored to make use of GroupBehaviorOptions to specify desired options/behavior.  Replaced WriteFileAdvanceBehavior with WriteBehavior which covers a larger set of options.  Added MDRFLogMessageHandlerAdapter to support LogDistribution output to an mdrf file.  Corrected issue with serialized format of QPCTime parameter in generated DateTimeBlock.
        /// 1.0.5 (2017-04-26) : MDRFWriter refactoring to move IVI/IVA logic so that each group defines the IVI used to create source IVAs (when enabled).  Refactored GroupBehaviorOptions members and added UseVCHasBeenSetForTouched flag to better support use in non-IVI cases.
        /// </remarks>
        public static readonly LibraryInfo LibInfo = new LibraryInfo()
        {
            Type = "Mosaic Data Recording File (MDRF)",
            Name = "Mosaic Data Recording Engine (MDRE) CS API",
            Version = "1.0.5 (2017-04-26)",
        };
    }

    #endregion

    #region IMDRFWriter and related definitions (GroupInfo, GroupPointInfo, OccurrenceInfo)

    public interface IMDRFWriter : IPartBase
    {
        IMDRFWriter Add(params GroupInfo[] groupInfoParamsArray);
        IMDRFWriter AddRange(IEnumerable<GroupInfo> groupInfoSet);
        IMDRFWriter Add(params OccurrenceInfo[] occurrenceInfoParamsArray);
        IMDRFWriter AddRange(IEnumerable<OccurrenceInfo> occurrenceInfoSet);

        string RecordGroups(bool writeAll = false, DateTimeStampPair dtPair = null);

        string RecordOccurrence(IOccurrenceInfo occurrenceInfo, object dataValue, DateTimeStampPair dtPair = null, bool writeAll = false, bool forceFlush = false);
        string RecordOccurrence(IOccurrenceInfo occurrenceInfo, ValueContainer dataVC = default(ValueContainer), DateTimeStampPair dtPair = null, bool writeAll = false, bool forceFlush = false);

        string Flush(FlushFlags flushFlags = FlushFlags.All, DateTimeStampPair dtPair = null);

	    int GetCurrentFileSize();

        bool IsFileOpen { get; }
        FileInfo CurrentFileInfo { get; }
        FileInfo LastFileInfo { get; }
        FileInfo? NextClosedFileInfo { get; }

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

    /// <summary>
    /// Available options for specifying/configuring the behavior for each group.
    /// <para/>None (0x00), UseSourceIVAsForTouched (0x01), UseVCHasBeenSetForTouched (0x02), IncrSeqNumOnTouched (0x04)
    /// </summary>
    [Flags]
    public enum GroupBehaviorOptions
    {
        /// <summary>Placeholder default value [0x00]</summary>
        None = 0x00,
        /// <summary>When requested, the group will be considered to have been touched if any of the IVAs from the associated sources indicate IsUpdateNeeded [0x01]</summary>
        UseSourceIVAsForTouched = 0x01,
        /// <summary>When requested, the group and sources will be considered to have been touched if (any of) the associated GroupPointInfo's VCHasBeenSet is true.  [0x02]</summary>
        UseVCHasBeenSetForTouched = 0x02,
        /// <summary>When requested, the group sequence number will only be incremented when the group has been marked as touched.  If this flag is not selected then the group sequence numbrer will be incremented any time the group is written.</summary>
        IncrSeqNumOnTouched = 0x04,
    }

    public class GroupInfo : MetaDataCommonInfo
    {
        public UInt64 FileIndexUserRowFlagBits { get; set; }
        public GroupPointInfo[] GroupPointInfoArray { get; set; }
        public GroupBehaviorOptions GroupBehaviorOptions 
        { 
            get { return _groupBehaviorOptions; }
            set
            {
                _groupBehaviorOptions = value;
                UseSourceIVAsForTouched = value.IsSet(GroupBehaviorOptions.UseSourceIVAsForTouched);
                UseVCHasBeenSetForTouched = value.IsSet(GroupBehaviorOptions.UseVCHasBeenSetForTouched);
                IncrSeqNumOnTouched = value.IsSet(GroupBehaviorOptions.IncrSeqNumOnTouched);
            }
        }
        public GroupBehaviorOptions _groupBehaviorOptions;

        public bool UseSourceIVAsForTouched { get; private set; }
        public bool UseVCHasBeenSetForTouched { get; private set; }
        public bool IncrSeqNumOnTouched { get; private set; }

        public int GroupID { get { return ClientID; } set { ClientID = value; } }
        public bool Touched { get; set; }

        public IValuesInterconnection IVI { get; set; }

        public GroupInfo() { ItemType = MDItemType.Group; }
    }

    /// <summary>
    /// A GroupPointInfo defines the source of values for the items in a group.  This also called a Source in the MDRF nomenclature
    /// </summary>
    public class GroupPointInfo : MetaDataCommonInfo
    {
        public ContainerStorageType ValueCST { get; set; }
        public IValueAccessor ValueSourceIVA { get; set; }

        public ValueContainer VC { get { return _vc; } set { _vc = value; VCHasBeenSet = true; } }
        private ValueContainer _vc;
        public bool VCHasBeenSet { get; set; }
        public bool VCIsUsable { get { return (ValueCST == ContainerStorageType.None || ValueCST == VC.cvt); } }

        public int GroupID { get; set; }
        public int SourceID { get { return ClientID; } set { ClientID = value; } }

        public GroupPointInfo() { ItemType = MDItemType.Source; }
    }

    public class OccurrenceInfo : MetaDataCommonInfo, IOccurrenceInfo
    {
        public UInt64 FileIndexUserRowFlagBits { get; set; }
        public ContainerStorageType ContentCST { get; set; }
        public bool IsHighPriority { get; set; }

        public int OccurrenceID { get { return ClientID; } set { ClientID = value; } }

        public OccurrenceInfo() { ItemType = MDItemType.Occurrence; }
    }

    public class MetaDataCommonInfo : IMetaDataCommonInfo
    {
        public MDItemType ItemType { get; protected set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public NamedValueSet ClientNVS { get; set; }
        INamedValueSet IMetaDataCommonInfo.ClientNVS { get { return ClientNVS; } }
        public Semi.E005.Data.ItemFormatCode IFC { get; protected set; }

        public int ClientID { get; set; }
        public int FileID { get; set; }

        public override string ToString()
        {
            string nvsStr = (ClientNVS.IsNullOrEmpty() ? "" : " {0}".CheckedFormat(ClientNVS));
            string commentStr = (Comment.IsNullOrEmpty() ? "" : " Comment:'{0}'".CheckedFormat(Comment));

            return "{0} name:'{1}' clientID:{2} fileID:{3} ifc:{4}{5}{6}".CheckedFormat(ItemType, Name, ClientID, FileID, IFC, nvsStr, commentStr);
        }
    }

    /// <summary>
    /// Additional bitfield enum that is used with SetupInfo.ClientNVS ("WriterBehavior") to specify additional options on when to write file should advance
    /// <para/>None (0x00), AdvanceOnDayBoundary (0x01), WriteAllBeforeEveryOccurrence (0x02), FlushAfterEveryOccurrence (0x04), FlushAfterEveryGroupWrite (0x08), FlushAfterEveryMessage (0x10), FlushAfterAnything (0x1c)
    /// </summary>
    [Flags]
    public enum WriterBehavior : int
    {
        /// <summary>
        /// Placeholder default value [0x00]
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Requests advance to the next file when the day transitions to a new value (midnight).  The effects of this flag will be delayed until the file is at least 10 minutes old. [0x01]
        /// </summary>
        AdvanceOnDayBoundary = 0x01,

        /// <summary>
        /// When this behavior option is selected, the RecordOccurrence method will always issue RecordGroups with the writeAll flag set to true. [0x02]
        /// </summary>
        WriteAllBeforeEveryOccurrence = 0x02,

        /// <summary>
        /// When this behavior option is selected, the RecordOccurrence method will always issue a Flush call before returning. [0x04]
        /// </summary>
        FlushAfterEveryOccurrence = 0x04,

        /// <summary>
        /// When this behavior option is selected, the RecordGroups method will always issue a Flush call before returning. [0x08]
        /// </summary>
        FlushAfterEveryGroupWrite = 0x08,

        /// <summary>
        /// When this behavior option is selected, the RecordError and RecordMessage methods will always issue a Flush call before returning. [0x10]
        /// </summary>
        FlushAfterEveryMessage = 0x10,

        /// <summary>
        /// FlushAfterAnything = (FlushAfterEveryOccurrence | FlushAfterEveryGroupWrite | FlushAfterEveryMessage) [0x1c]
        /// </summary>
        FlushAfterAnything = (FlushAfterEveryOccurrence | FlushAfterEveryGroupWrite | FlushAfterEveryMessage),
    }

    #endregion

    #region MDRFWriter implementation

    public class MDRFWriter : SimplePartBase, IMDRFWriter
    {
        #region Construction, Release, call-chainable Add variants, and supporting fields

        public MDRFWriter(string partID, SetupInfo setupInfo, GroupInfo [] groupInfoArray = null, OccurrenceInfo [] occurrenceInfoArray = null, bool enableAPILocking = false)
            : base(partID)
        {
            mutex = enableAPILocking ? new object() : null;

            AddExplicitDisposeAction(Release);

            // validate selected fields
            SetupResultCode = string.Empty;

            if (setupInfo == null)
                SetupResultCode = SetupResultCode.MapNullOrEmptyTo("{0} failed: given setupInfo cannot be null".CheckedFormat(CurrentMethodName));

            setupOrig = setupInfo ?? defaultSetupInfo;
            setup = new SetupInfo(setupOrig).MapDefaultsTo(defaultSetupInfo).ClipValues();

            writerBehavior = setup.ClientNVS.MapNullToEmpty()["WriterBehavior"].VC.GetValue<WriterBehavior>(false);

            dataBlockBuffer = new DataBlockBuffer(setup);
            fileIndexData = new FileIndexData(setup);

            if (!groupInfoArray.IsNullOrEmpty())
            {
                groupInfoList.AddRange(groupInfoArray);
                groupOrOccurrenceInfoListModified = true;
            }

            if (!occurrenceInfoArray.IsNullOrEmpty())
            {
                occurrenceInfoList.AddRange(occurrenceInfoArray);
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

        public void Release()
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                CloseCurrentFile("On Release");
            }
        }

        public MDRFWriter Add(GroupInfo groupInfo)
        {
            return Add(new [] { groupInfo });
        }

        public MDRFWriter Add(params GroupInfo[] groupInfoParamsArray)
        {
            return AddRange(groupInfoParamsArray ?? emptyGroupInfoArray);
        }

        public MDRFWriter AddRange(IEnumerable<GroupInfo> groupInfoSet)
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                groupInfoList.AddRange(groupInfoSet.Where(gi => gi != null));
                groupOrOccurrenceInfoListModified = true;
            }

            return this;
        }

        public MDRFWriter Add(OccurrenceInfo occurrenceInfo)
        {
            return Add(new [] { occurrenceInfo });
        }

        public MDRFWriter Add(params OccurrenceInfo[] occurrenceInfoParamsArray)
        {
            return AddRange(occurrenceInfoParamsArray ?? emptyOccurrenceInfoArray);
        }

        public MDRFWriter AddRange(IEnumerable<OccurrenceInfo> occurrenceInfoSet)
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                occurrenceInfoList.AddRange(occurrenceInfoSet.Where(oi => oi != null));
                groupOrOccurrenceInfoListModified = true;
            }

            return this;
        }

        /// <summary>
        /// This string returns the success/failure code for the set of setup steps that are performed during object construction.
        /// </summary>
        public string SetupResultCode { get; set; }

        List<GroupInfo> groupInfoList = new List<GroupInfo>();
        List<OccurrenceInfo> occurrenceInfoList = new List<OccurrenceInfo>();
        bool groupOrOccurrenceInfoListModified = false;

        static readonly SetupInfo defaultSetupInfo = new SetupInfo();
        static readonly GroupInfo[] emptyGroupInfoArray = new GroupInfo[0];
        static readonly GroupPointInfo[] emptyPointGroupInfoArray = new GroupPointInfo[0];
        static readonly OccurrenceInfo[] emptyOccurrenceInfoArray = new OccurrenceInfo[0];

        #endregion

        #region IMDRFWriter implementation methods (both interface specific methods and general public methods that support the interface)

        IMDRFWriter IMDRFWriter.Add(params GroupInfo[] groupInfoParamsArray) { return AddRange(groupInfoParamsArray ?? emptyGroupInfoArray); }
        IMDRFWriter IMDRFWriter.AddRange(IEnumerable<GroupInfo> groupInfoSet) { return AddRange(groupInfoSet); }
        IMDRFWriter IMDRFWriter.Add(params OccurrenceInfo[] occurrenceInfoParamsArray) { return AddRange(occurrenceInfoParamsArray ?? emptyOccurrenceInfoArray); }
        IMDRFWriter IMDRFWriter.AddRange(IEnumerable<OccurrenceInfo> occurrenceInfoSet) { return AddRange(occurrenceInfoSet); }

        public string RecordGroups(bool writeAll = false, DateTimeStampPair dtPair = null)
        {
            return RecordGroups(writeAll, dtPair, writerBehavior);
        }

        private string RecordGroups(bool writeAll, DateTimeStampPair dtPair, WriterBehavior writerBehaviorParam)
        {
            if (IsDisposed)
                return "{0} has already been disposed".CheckedFormat(CurrentClassLeafName);

            using (var scopedLock = new ScopedLock(mutex))
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
                    groupTracker.numTouchedSources = 0; // clear numTouchedSources count at start of each service loop
                    groupTracker.Service(writeAll);
                }

                // generate and write the group data blocks themselves
                if (ec.IsNullOrEmpty())
                {
                    FileIndexRowFlagBits firstBlockFlagBits = (writeAll ? FileIndexRowFlagBits.ContainsStartOfFullGroup : FileIndexRowFlagBits.None);

                    foreach (GroupTracker groupTracker in groupTrackerArray)
                    {
                        GroupInfo groupInfo = groupTracker.groupInfo;
                        bool groupInfoWasTouched = (groupInfo != null && groupInfo.Touched);
                        bool incrementGroupSeqNum = (groupInfoWasTouched || !groupInfo.IncrSeqNumOnTouched);

                        if (groupTracker.touched)
                        {
                            if (groupTracker.WriteAsEmptyGroup)
                            {
                                StartGroupDataBlock(groupTracker, dtPair, firstBlockFlagBits, GroupBlockFlagBits.IsEmptyGroup, preIncrementSeqNum: incrementGroupSeqNum);
                            }
                            else if (groupTracker.WriteAsSparseGroup)
                            {
                                // write group with updateMask (sparse group)
                                StartGroupDataBlock(groupTracker, dtPair, firstBlockFlagBits, GroupBlockFlagBits.HasUpdateMask, preIncrementSeqNum: incrementGroupSeqNum);

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
                                StartGroupDataBlock(groupTracker, dtPair, firstBlockFlagBits, GroupBlockFlagBits.None, preIncrementSeqNum: incrementGroupSeqNum);

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
                    ec = ServicePendingWrites(dtPair);

                if (ec.IsNullOrEmpty() && writerBehaviorParam.IsSet(WriterBehavior.FlushAfterEveryGroupWrite))
                    ec = InnerFlush(FlushFlags.All, dtPair, false);

                if (!ec.IsNullOrEmpty())
                {
                    droppedCounts.IncrementDataGroup();
                    RecordError(ec, dtPair);
                    CloseFile(dtPair);
                }

                return ec;
            }
        }

        public string RecordOccurrence(IOccurrenceInfo occurrenceInfo, object dataValue, DateTimeStampPair dtPair = null, bool writeAll = false, bool forceFlush = false)
        {
            return RecordOccurrence(occurrenceInfo, dataVC: new ValueContainer(dataValue), dtPair: dtPair, writeAll: writeAll, forceFlush: forceFlush);
        }

        public string RecordOccurrence(IOccurrenceInfo occurrenceInfo, ValueContainer dataVC = default(ValueContainer), DateTimeStampPair dtPair = null, bool writeAll = false, bool forceFlush = false)
        {
            if (occurrenceInfo == null)
                return "{0} failed:  occurrenceInfo parameter cannot be null".CheckedFormat(CurrentMethodName);

            if (IsDisposed)
                return "{0} has already been disposed".CheckedFormat(CurrentClassLeafName);

            using (var scopedLock = new ScopedLock(mutex))
            {
                ReassignIDsAndBuildNewTrackersIfNeeded(dtPair);

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

                writeAll |= writerBehavior.IsSet(WriterBehavior.WriteAllBeforeEveryOccurrence);

                string ec = ActiviateFile(dtPair);

                if (writeAll)
                    RecordGroups(writeAll, dtPair, WriterBehavior.None);        // no "special" writer behaviors are selected when recursively using RecordGroups

                if (ec.IsNullOrEmpty())
                {
                    StartOccurrenceDataBlock(occurrenceTracker, dtPair);
                    dataBlockBuffer.payloadDataList.AppendWithIH(dataVC);
                    ec = WriteDataBlock(dtPair);
                }

                if (ec.IsNullOrEmpty() && (forceFlush || writerBehavior.IsSet(WriterBehavior.FlushAfterEveryOccurrence)))
                    ec = InnerFlush(FlushFlags.All, dtPair, false);

                if (!ec.IsNullOrEmpty())
                    RecordError(ec, dtPair);

                return ec;
            }
        }

        public string Flush(FlushFlags flushFlags = FlushFlags.All, DateTimeStampPair dtPair = null)
        {
            if (IsDisposed)
                return "{0} has already been disposed".CheckedFormat(CurrentClassLeafName);

            using (var scopedLock = new ScopedLock(mutex))
            {
                string ec = string.Empty;

                if (InnerIsFileOpen)
                    dtPair = (dtPair ?? DateTimeStampPair.Now).UpdateFileDeltas(fileReferenceDTPair);
                else
                    ec = "{0} failed: File is not open".CheckedFormat(CurrentMethodName);

                try
                {
                    if (ec.IsNullOrEmpty())
                        ec = ServicePendingWrites(dtPair);

                    if (ec.IsNullOrEmpty())
                        ec = InnerFlush(flushFlags, dtPair, true);
                }
                catch (System.Exception ex)
                {
                    if (InnerIsFileOpen)
                    {
                        ec = RecordError("{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage)), dtPair);

                        CloseFile(dtPair);
                    }
                }

                return ec;
            }
        }

        private string InnerFlush(FlushFlags flushFlags, DateTimeStampPair dtPair, bool rethrow)
        {
            string ec = string.Empty;

            try
            {
                if (ec.IsNullOrEmpty() && fileIndexData.fileIndexChanged && flushFlags.IsSet(FlushFlags.Index))
                    ec = UpdateAndWriteFileIndex(doFlush: false, dtPair: dtPair);

                if (ec.IsNullOrEmpty() && flushFlags.IsSet(FlushFlags.File))
                    fs.Flush();
            }
            catch (System.Exception ex)
            {
                if (rethrow)
                    throw;

                ec = RecordError("{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage)), dtPair);

                CloseFile(dtPair);
            }

            return ec;
        }

        public int GetCurrentFileSize()
        {
            if (IsDisposed)
                return 0;

            using (var scopedLock = new ScopedLock(mutex))
            {
                return (InnerIsFileOpen ? CurrentFileInfo : LastFileInfo).FileSize;
            }
        }

        public bool IsFileOpen
        {
            get
            {
                if (IsDisposed)
                    return false;

                using (var scopedLock = new ScopedLock(mutex))
                {
                    return currentFileInfo.IsActive && InnerIsFileOpen;
                }
            }
        }

        public FileInfo CurrentFileInfo
        {
            get
            {
                if (IsDisposed)
                    return default(FileInfo);

                using (var scopedLock = new ScopedLock(mutex)) 
                { 
                    return currentFileInfo; 
                }
            }
        }

        public FileInfo LastFileInfo
        {
            get
            {
                if (IsDisposed)
                    return default(FileInfo);

                using (var scopedLock = new ScopedLock(mutex)) 
                { 
                    return lastFileInfo; 
                }
            }
        }

        const int maxClosedFileListLength = 10;
        volatile int volatileClosedFileListCount = 0;
        List<FileInfo> closedFileList = new List<FileInfo>();

        public FileInfo? NextClosedFileInfo
        {
            get
            {
                if (volatileClosedFileListCount <= 0)
                    return null;

                using (var scopedLock = new ScopedLock(mutex))
                {
                    if (closedFileList.Count <= 0)
                        return null;

                    FileInfo fileInfo = closedFileList[0];
                    closedFileList.RemoveAt(0);
                    volatileClosedFileListCount = closedFileList.Count;

                    return fileInfo;
                }
            }
        }

        public int CloseCurrentFile(string reason, DateTimeStampPair dtPair = null)
        {
            if (IsDisposed)
                return 0;

            using (var scopedLock = new ScopedLock(mutex))
            {
                dtPair = (dtPair ?? DateTimeStampPair.Now).UpdateFileDeltas(fileReferenceDTPair);

                if (InnerIsFileOpen)
                    RecordMessage("{0} reason:{1}".CheckedFormat(CurrentMethodName, reason.MapNullOrEmptyTo("[NoReasonGiven]")), dtPair);

                ServicePendingWrites(dtPair);

                if (InnerIsFileOpen)
                    WriteFileEndBlockAndCloseFile(dtPair);

                allowImmediateReopen = true;

                while (closedFileList.Count >= maxClosedFileListLength)
                {
                    FileInfo droppingFileInfo = closedFileList[0];

                    RecordMessage("Dropped old closedFileList item ['{0}' {1} bytes]".CheckedFormat(System.IO.Path.GetFileName(droppingFileInfo.FilePath), droppingFileInfo.FileSize), dtPair);
                    closedFileList.RemoveAt(0);
                }

                closedFileList.Add(LastFileInfo);

                volatileClosedFileListCount = closedFileList.Count;

                return LastFileInfo.FileSize;
            }
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

        void ReassignIDsAndBuildNewTrackersIfNeeded(DateTimeStampPair dtPair = null, bool forceRebuild = false)
        {
            if (groupOrOccurrenceInfoListModified || forceRebuild)
            {
                if (InnerIsFileOpen)
                    CloseCurrentFile("GroupOrOccurrenceInfoListModified: Reassigning IDs and building new trackers", dtPair);

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
                    int fileID = unchecked((int)FixedBlockTypeID.FirstDynamicallyAssignedID);
                    foreach (TrackerCommon tc in allItemCommonArray)
                        tc.FileID = fileID++;

                    // then Setup all of the items so that they can generate their NVS contents
                    foreach (TrackerCommon tc in allItemCommonArray)
                        tc.Setup();

                    allMetaDataItemsArray = (sourceTrackerArray as TrackerCommon[]).Concat(groupTrackerArray as TrackerCommon[]).Concat(occurrenceTrackerArray as TrackerCommon[]).ToArray();
                }

                groupOrOccurrenceInfoListModified = false;
            }
        }

        QpcTimer dstRecheckTimer = new QpcTimer() { TriggerIntervalInSec = 1.0, AutoReset = true }.StartIfNeeded();

        string ActiviateFile(DateTimeStampPair dtPair)
        {
            ReassignIDsAndBuildNewTrackersIfNeeded(dtPair);

            TimeSpan fileDeltaTimeSpan = dtPair.fileDeltaTimeStamp.FromSeconds();

            if (InnerIsFileOpen)
            {
                QpcTimeStamp tsNow = dtPair.qpcTimeStamp;

                int dayOfYear = dtPair.dateTime.ToLocalTime().DayOfYear;
                bool dayOfYearChanged = (dayOfYear != fileReferenceDayOfYear);

                if (dayOfYearChanged && writerBehavior.IsSet(WriterBehavior.AdvanceOnDayBoundary) && fileDeltaTimeSpan.TotalMinutes > 10.0)
                {
                    CloseCurrentFile("AdvanceOnDayBoundary triggered: '{0}' is {1} bytes and {2:f3} hours)".CheckedFormat(currentFileInfo.FilePath, currentFileInfo.FileSize, fileDeltaTimeSpan.TotalHours));
                }
                else if (currentFileInfo.FileSize >= setup.NominalMaxFileSize)
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

            if (InnerIsFileOpen)
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
                fileReferenceDayOfYear = dtPair.dateTime.ToLocalTime().DayOfYear;
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
                string mesg = "{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage));

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

                System.Diagnostics.Process currentProcess = Utils.ExtensionMethods.TryGet<System.Diagnostics.Process>(() => System.Diagnostics.Process.GetCurrentProcess());

                nvSet.SetValue("CurrentProcess.ProcessName", Utils.ExtensionMethods.TryGet<string>(() => currentProcess.ProcessName));
                nvSet.SetValue("CurrentProcess.Id",  Utils.ExtensionMethods.TryGet<int>(() => currentProcess.Id));
                nvSet.SetValue("CurrentProcess.Affinity", Utils.ExtensionMethods.TryGet<long>(() => currentProcess.ProcessorAffinity.ToInt64()));
                nvSet.SetValue("Environment.Is64BitProcess", Environment.Is64BitProcess);
                nvSet.SetValue("Environment.MachineName", Environment.MachineName);
                nvSet.SetValue("Environment.OSVersion", Environment.OSVersion.ToString());
                nvSet.SetValue("Environment.Is64BitOperatingSystem", Environment.Is64BitOperatingSystem);
                nvSet.SetValue("Environment.ProcessorCount", Environment.ProcessorCount);
                nvSet.SetValue("Environment.SystemPageSize", Environment.SystemPageSize);
                nvSet.SetValue("Environment.UserName", Environment.UserName);
                nvSet.SetValue("Environment.UserInteractive", Environment.UserInteractive);

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
                ec = InnerFlush(FlushFlags.File, dtPair, false);

            if (ec.IsNullOrEmpty())
                currentFileInfo.IsActive = true;

            if (!ec.IsNullOrEmpty())
                return RecordError(ec, dtPair);

            // clear the dropped counters after all messages have been emitted successfully
            droppedCounts = new DroppedCounts();

            return "";
        }

        /// <summary>
        /// Returns true if we have a non-null file stream (fs)
        /// </summary>
        bool InnerIsFileOpen 
        { 
            get 
            { 
                return fs != null; 
            } 
        }

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
                    Log.Debug.Emit(RecordError("{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage)), dtPair));
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
            dateTimeBlockNVSet.SetValue("QPCTime", dtPair.qpcTimeStamp.Time);
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

                if (!InnerIsFileOpen)
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

                        if (ec.IsNullOrEmpty() && writerBehavior.IsSet(WriterBehavior.FlushAfterEveryMessage))
                            ec = InnerFlush(FlushFlags.All, dtPair, false);
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

            if (writeFileIndexNow && InnerIsFileOpen)
            {
                ec = UpdateAndWriteFileIndex(doFlush: true, dtPair: dtPair);
            }

            return ec;
        }

        string UpdateAndWriteFileIndex(bool doFlush, DateTimeStampPair dtPair)
        {
            if (!InnerIsFileOpen)
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

                return RecordError("{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage)), dtPair);
            }

            lastFileIndexWriteTimeStamp = QpcTimeStamp.Now;
            fileIndexData.fileIndexChanged = false;
            writeFileIndexNow = false;

            return string.Empty;
        }

        string WriteDataBlock(DateTimeStampPair dtPair)
        {
            if (!InnerIsFileOpen)
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

                return RecordError("{0} failed: {1} {2}".CheckedFormat(CurrentMethodName, currentFileInfo, ex.ToString(ExceptionFormat.TypeAndMessage)), dtPair);
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

        #region Internals: MessageItem, FileIndexData, FileIndexRow, FileIndexLastBlockInfo, DataBlockBuffer, TrackerCommon, SourceTracker, GroupTracker, OccurrenceTracker, DroppedCounts

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
            public override string ToString()
            {
                return "Tracker {0} seqNum:{1}".CheckedFormat(mdci, seqNum);
            }

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
            public GroupTracker groupTracker;
            public SetupInfo setupInfo;
            public GroupInfo groupInfo;
            public GroupPointInfo groupPointInfo;

            public bool touched;
            public ValueContainer lastServicedValue;
            public uint lastServicedIVASeqNum;

            public SourceTracker(GroupPointInfo gpi, GroupTracker gt) 
                : base(MDItemType.Source, gpi)
            {
                groupTracker = gt;
                setupInfo = gt.setupInfo;
                groupInfo = gt.groupInfo;
                groupPointInfo = gpi;
                ifc = gpi.ValueCST.ConvertToItemFormatCode();

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
                    bool different = groupPointInfo.VCIsUsable && (groupTracker.writeAll || !lastServicedValue.Equals(groupPointInfo.VC));

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

            public void AppendValue(List<byte> byteArrayBuilder)
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

            /// <summary>True if writeAll or if numTouchedSources >= 80% of total number</summary>
            public bool WriteAsFullGroup { get { return writeAll || (numTouchedSources * oneOverSourceTrackerArrayLength >= 0.80); } }
            /// <summary>True if !writeAll and numTouchedSources is zero</summary>
            public bool WriteAsEmptyGroup { get { return !writeAll && (numTouchedSources == 0); } }
            /// <summary>True if numTouchedSources is not zero and !writeAll and !WriteAsFullGroup</summary>
            public bool WriteAsSparseGroup { get { return !(numTouchedSources == 0) && !WriteAsFullGroup; } }

            public byte [] updateMaskByteArray;
            public int updateMaskByteArraySize;

            public IValuesInterconnection ivi;
            public IValueAccessor[] ivaArray;

            public GroupTracker(GroupInfo gi, SetupInfo si)
                : base(MDItemType.Group, gi)
            {
                groupInfo = gi;
                setupInfo = si; 
                AddClientNVSItems();

                ivi = gi.IVI;
                if (gi.UseSourceIVAsForTouched && ivi == null)
                    ivi = Values.Instance;

                sourceTrackerArray = (groupInfo.GroupPointInfoArray ?? emptyPointGroupInfoArray).Select(pointInfo => new SourceTracker(pointInfo, this)).ToArray();
                sourceTrackerArrayLength = sourceTrackerArray.Length;
                oneOverSourceTrackerArrayLength = (sourceTrackerArrayLength > 0 ? (1.0 / sourceTrackerArrayLength) : 0);

                updateMaskByteArraySize = ((sourceTrackerArrayLength + 7) >> 3);
                updateMaskByteArray = new byte [updateMaskByteArraySize];

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

                nvSet.SetValue("ItemList", string.Join(",", sourceTrackerArray.Select(st => st.FileID.ToString()).ToArray()));
                nvSet.SetValue("FileIndexUserRowFlagBits", groupInfo.FileIndexUserRowFlagBits);
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

        #region Internals:  fields

        /// <remarks>
        /// Annotate this as readonly since the value is only assigned once in the constructor (to either new object() or null).
        /// </remarks>
        readonly object mutex;

        SetupInfo setupOrig;
        SetupInfo setup;

        WriterBehavior writerBehavior;

        AtomicInt32 seqNumGen = new AtomicInt32();

        System.IO.FileStream fs;
        DateTimeStampPair fileReferenceDTPair = null;
        int fileReferenceDayOfYear = 0;
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

    #region MDRFRecordingEngine, MDRFRecordingEngineConfig, IMDRFRecordingEngine

    public interface IMDRFRecordingEngine : IActivePartBase
    {
        /// <summary>
        /// StringParamAction action factory method.
        /// The resulting action, when run, will record an occurrance containing the eventName and the given eventDataNVS.
        /// The OccurrenceInfo used for this event is determined from the given eventName.  
        /// If the eventName matches a name registered with a specific occurrance then that occurrence will be used, otherwise the DefaultRecordEventOccurrance will be used.
        /// </summary>
        IStringParamAction RecordEvent(string eventName = null, INamedValueSet eventDataNVS = null, bool writeAll = false);
    }

    /// <summary>
    /// Defines the client usable behavior of an MDRFRecordingEngine instance.  
    /// This defines the PartID to use and a number of other characteristics including:
    /// the NominalScanPeriod to use, 
    /// the IVI from which to get values,
    /// an optional GroupDefinitionSet that may be used to define what IVI items shall be included and what group each one shall be placed in,
    /// the SetupInfo to be used with the internal IMDRFWriter instance, 
    /// and the PruningConfig that may be used to define how the pruning engine is used to automatically remove old files as new ones are generated.
    /// </summary>
    public class MDRFRecordingEngineConfig
    {
        public MDRFRecordingEngineConfig(string partID)
        {
            PartID = partID;
            NominalScanPeriod = (0.1).FromSeconds();
            NominalPruningInterval = (10.0).FromSeconds();
        }

        public MDRFRecordingEngineConfig(MDRFRecordingEngineConfig other)
        {
            PartID = other.PartID;

            SetupInfo = new SetupInfo(other.SetupInfo);     // will be mapped to default values if the other is null)

            NominalScanPeriod = other.NominalScanPeriod;

            ScanPeriodScheduleArray = new List<TimeSpan>(other.ScanPeriodScheduleArray ?? emptyTimeSpanArray).ToArray();

            SourceIVI = other.SourceIVI ?? Values.Instance;

            AllocateUserRowFlagBitsForGroups = other.AllocateUserRowFlagBitsForGroups;

            if (other.GroupDefinitionSet != null)
                GroupDefinitionSet = other.GroupDefinitionSet.Select(otherTuple => Tuple.Create<string, MatchRuleSet, INamedValueSet>(otherTuple.Item1, new Utils.StringMatching.MatchRuleSet(otherTuple.Item2), otherTuple.Item3.ConvertToReadOnly())).ToArray();

            if (other.EventDefinitionSet != null)
                EventDefinitionSet = other.EventDefinitionSet.Select(otherTuple => Tuple.Create<string, IEnumerable<string>, INamedValueSet>(otherTuple.Item1, otherTuple.Item2.SafeToArray(), otherTuple.Item3.ConvertToReadOnly())).ToArray();

            if (other.PruningConfig != null)
            {
                PruningConfig = new DirectoryTreePruningManager.Config(other.PruningConfig)
                {
                    DirPath = SetupInfo.DirPath,
                };
            }

            NominalPruningInterval = other.NominalPruningInterval;
        }

        public string PartID { get; private set; }

        public SetupInfo SetupInfo { get; set; }

        public TimeSpan NominalScanPeriod { get; set; }

        public TimeSpan [] ScanPeriodScheduleArray { get; set; }

        public IValuesInterconnection SourceIVI { get; set; }

        public bool AllocateUserRowFlagBitsForGroups { get; set; }

        public IEnumerable<Tuple<string, Utils.StringMatching.MatchRuleSet, INamedValueSet>> GroupDefinitionSet { get; set; }

        public IEnumerable<Tuple<string, IEnumerable<string>, INamedValueSet>> EventDefinitionSet { get; set; }

        public DirectoryTreePruningManager.Config PruningConfig { get; set; }

        public TimeSpan NominalPruningInterval { get; set; }

        private static readonly TimeSpan[] emptyTimeSpanArray = new TimeSpan[0];
    }

    /// <summary>
    /// This class defines a part that can be used to implement standard MDRF recording scenarios.
    /// </summary>
    public class MDRFRecordingEngine : SimpleActivePartBase, IMDRFRecordingEngine
    {
        #region Construction (et. al.)

        public MDRFRecordingEngine(MDRFRecordingEngineConfig config)
            : base(config.PartID, SimpleActivePartBaseSettings.DefaultVersion1.Build(waitTimeLimit: (0.01).FromSeconds()))
        {
            Config = new MDRFRecordingEngineConfig(config);

            AddExplicitDisposeAction(() => Fcns.DisposeOfObject(ref mdrfWriter));
        }

        public MDRFRecordingEngineConfig Config { get; private set; }

        #endregion

        #region private fields (et. al.)

        IMDRFWriter mdrfWriter = null;
        QpcTimer mdrfWriterServiceTimer;

        DirectoryTreePruningManager pruningManager = null;
        QpcTimer pruningServiceTimer;

        #endregion

        #region local actions and custom properties

        /// <summary>
        /// StringParamAction action factory method.
        /// The resulting action, when run, will record an occurrance containing the eventName and the given eventDataNVS.
        /// The OccurrenceInfo used for this event is determined from the given eventName.  
        /// If the eventName matches a name registered with a specific occurrance then that occurrence will be used, otherwise the DefaultRecordEventOccurrance will be used.
        /// </summary>
        public IStringParamAction RecordEvent(string eventName = null, INamedValueSet eventDataNVS = null, bool writeAll = false)
        {
            ActionMethodDelegateActionArgStrResult<string, NullObj> performRecordEventDelegate = (actionProviderFacet) => PerformRecordEvent(actionProviderFacet, writeAll);
            IStringParamAction action = new StringActionImpl(actionQ, eventName, performRecordEventDelegate, "{0}({1}{2})".CheckedFormat(CurrentMethodName, eventName, writeAll ? ",writeAll" : string.Empty), ActionLoggingReference);

            if (eventDataNVS != null)   
                action.NamedParamValues = eventDataNVS;

            return action;
        }

        private string PerformRecordEvent(IProviderActionBase<string, NullObj> action, bool writeAll)
        {
            string eventName = action.ParamValue;
            INamedValueSet eventDataNVS = action.NamedParamValues;

            if (!BaseState.IsOnline || mdrfWriter == null)
                return "Part is not Online or there is no valid mdrfWriter";
 
            NamedValueSet occurrenceDataNVS = new NamedValueSet() { { "eventName", eventName } }.MergeWith(eventDataNVS, NamedValueMergeBehavior.AddNewItems);

            OccurrenceInfo occurrenceInfo = null;

            if (!eventNameToOccurrenceInfoDictionary.TryGetValue(eventName, out occurrenceInfo) || occurrenceInfo == null)
                occurrenceInfo = defaultRecordEventOccurranceInfo;

            return mdrfWriter.RecordOccurrence(occurrenceInfo, occurrenceDataNVS, writeAll: writeAll);
        }

        OccurrenceInfo defaultRecordEventOccurranceInfo;
        private Dictionary<string, OccurrenceInfo> eventNameToOccurrenceInfoDictionary = new Dictionary<string,OccurrenceInfo>();

        /// <summary>
        /// This method may be used by a client to select a new scheduled scan period using the given scanPeriodScheduleSelection value (converted to an Int32)
        /// </summary>
        public void SelectScanPeriodSchedule<TScheduleIndex>(TScheduleIndex scanPeriodScheduleSelection)
            where TScheduleIndex : struct
        {
            try
            {
                volatileScanPeriodScheduleIndex = (int) System.Convert.ChangeType(scanPeriodScheduleSelection, typeof(int));
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

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            if (Config.PruningConfig != null && pruningManager == null)
            {
                pruningManager = new DirectoryTreePruningManager("{0}.pm".CheckedFormat(PartID), Config.PruningConfig);
                pruningServiceTimer = new QpcTimer() { TriggerInterval = Config.NominalPruningInterval, AutoReset = true, Started = true };
            }

            if (mdrfWriter == null)
            {
                List<UInt64> availableUserFlagRowBitsList = new List<UInt64>(Enumerable.Range(8, 63).Select(bitIdx => (1UL << bitIdx)));

                List<string> ivaNamesList = new List<string>(Config.SourceIVI.ValueNamesArray);

                List<GroupInfo> groupInfoList = new List<GroupInfo>();

                if (Config.GroupDefinitionSet.IsNullOrEmpty())
                {
                    IValueAccessor[] ivaArray = ivaNamesList.Select(name => Config.SourceIVI.GetValueAccessor(name)).ToArray();
                    groupInfoList.Add(new GroupInfo() 
                                        { 
                                            Name = "Default", 
                                            GroupBehaviorOptions = GroupBehaviorOptions.UseSourceIVAsForTouched, 
                                            FileIndexUserRowFlagBits = Config.AllocateUserRowFlagBitsForGroups ? availableUserFlagRowBitsList.SafeTakeFirst() : 0,
                                            IVI = Config.SourceIVI,
                                            GroupPointInfoArray = ivaArray.Select(iva => new GroupPointInfo() { Name = iva.Name, ValueSourceIVA = iva }).ToArray(),
                                        });
                }
                else
                {
                    foreach (var tuple in Config.GroupDefinitionSet)
                    {
                        string[] matchedIVANamesArray = ivaNamesList.Where(ivaName => tuple.Item2.MatchesAny(ivaName)).ToArray();

                        foreach (string matchedIVAName in matchedIVANamesArray)
                            ivaNamesList.Remove(matchedIVAName);

                        IValueAccessor[] ivaArray = matchedIVANamesArray.Select(name => Config.SourceIVI.GetValueAccessor(name)).ToArray();

                        groupInfoList.Add(new GroupInfo() 
                                        { 
                                            Name = tuple.Item1,
                                            GroupBehaviorOptions = GroupBehaviorOptions.UseSourceIVAsForTouched,
                                            FileIndexUserRowFlagBits = Config.AllocateUserRowFlagBitsForGroups ? availableUserFlagRowBitsList.SafeTakeFirst() : 0,
                                            IVI = Config.SourceIVI,
                                            GroupPointInfoArray = ivaArray.Select(iva => new GroupPointInfo() { Name = iva.Name, ValueSourceIVA = iva }).ToArray(),
                                            ClientNVS = tuple.Item3.ConvertToReadOnly(),
                                        });
                    }
                }

                defaultRecordEventOccurranceInfo = new OccurrenceInfo()
                {
                     Name = "DefaultRecordEventOccurrance",
                     ContentCST = ContainerStorageType.Object,
                };

                List<OccurrenceInfo> occuranceInfoList = new List<OccurrenceInfo>();
                occuranceInfoList.Add(defaultRecordEventOccurranceInfo);

                if (Config.EventDefinitionSet != null)
                {
                    foreach (var tuple in Config.EventDefinitionSet)
                    {
                        string [] eventNamesArray = tuple.Item2.SafeToArray();

                        NamedValueSet occurrenceInfoNVS = new NamedValueSet() { { "eventNames", eventNamesArray } }.MergeWith(tuple.Item3, NamedValueMergeBehavior.AddNewItems);

                        OccurrenceInfo occurrenceInfo = new OccurrenceInfo()
                        {
                            Name = tuple.Item1,
                            FileIndexUserRowFlagBits = availableUserFlagRowBitsList.SafeTakeFirst(),
                            ClientNVS = occurrenceInfoNVS.ConvertToReadOnly(),
                        };

                        occuranceInfoList.Add(occurrenceInfo);

                        foreach (var eventName in eventNamesArray)
                        {
                            eventNameToOccurrenceInfoDictionary[eventName.Sanitize()] = occurrenceInfo;
                        }
                    }
                }

                mdrfWriter = new MDRFWriter("{0}.mw".CheckedFormat(PartID), Config.SetupInfo, groupInfoList.ToArray(), occuranceInfoList.ToArray());
                mdrfWriterServiceTimer = new QpcTimer() { TriggerInterval = Config.NominalScanPeriod, AutoReset = true, Started = true };
            }

            return string.Empty;
        }

        protected override string PerformGoOfflineAction()
        {
            PerformMainLoopService(forceServiceWriter: true, forceServicePruning: true);

            Fcns.DisposeOfObject(ref mdrfWriter);

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

                mdrfWriterServiceTimer.Start(scanPeriod);
                
                PerformMainLoopService(forceServiceWriter: true);

                Log.Debug.Emit("Scan Period changed to index:{0} period:{1:f3} sec", currentScanPeriodScheduleIndex, scanPeriod.TotalSeconds);
            }
            else
            {
                PerformMainLoopService(forceServiceWriter: false, forceServicePruning: false);
            }
        }

        private void PerformMainLoopService(bool forceServiceWriter = false, bool forceServicePruning = false)
        {
            if (mdrfWriter != null && (mdrfWriterServiceTimer.IsTriggered || forceServiceWriter))
            {
                mdrfWriter.RecordGroups(writeAll: forceServiceWriter);
            }

            if (pruningManager != null && (pruningServiceTimer.IsTriggered || forceServicePruning))
            {
                if (mdrfWriter != null)
                {
                    FileInfo? closedFileInfo = mdrfWriter.NextClosedFileInfo;
                    if (closedFileInfo != null)
                        pruningManager.NotePathAdded(closedFileInfo.GetValueOrDefault().FilePath);
                }

                pruningManager.Service();
            }
        }

        #endregion
    }

    #endregion

    #region MDRFLogMessageHandlerAdapter, MesgTypeToIOccurrenceInfoMap

    public class MDRFLogMessageHandlerAdapterConfig
    {
        public MDRFLogMessageHandlerAdapterConfig()
        {
            NormalMesgOccurrenceName = "Mesg";
            NormalMesgUserFileIndexRowFlagBits = 0;
            NormalMesgFlushesWriter = false;
            SignificantMesgOccurrenceName = "SigMesg";
            SignificantMesgTypeLogGate = Logging.LogGate.Signif;
            SignificantMesgUserFileIndexRowFlagBits = 0;
            SignificantMesgIsHighPriority = true;
            SignificantMesgFlushesWriter = true;
            FlushFlagstoUseAfterEachMessageBlock = FlushFlags.File;  
        }

        public MDRFLogMessageHandlerAdapterConfig(MDRFLogMessageHandlerAdapterConfig other)
        {
            NormalMesgOccurrenceName = other.NormalMesgOccurrenceName;
            NormalMesgUserFileIndexRowFlagBits = other.NormalMesgUserFileIndexRowFlagBits;
            NormalMesgFlushesWriter = other.NormalMesgFlushesWriter;
            SignificantMesgOccurrenceName = other.SignificantMesgOccurrenceName;
            SignificantMesgTypeLogGate = other.SignificantMesgTypeLogGate;
            SignificantMesgUserFileIndexRowFlagBits = other.SignificantMesgUserFileIndexRowFlagBits;
            SignificantMesgIsHighPriority = other.SignificantMesgIsHighPriority;
            SignificantMesgFlushesWriter = other.SignificantMesgFlushesWriter;
            FlushFlagstoUseAfterEachMessageBlock = other.FlushFlagstoUseAfterEachMessageBlock;
            OnlyRecordMessagesIfFileIsAlreadyActive = other.OnlyRecordMessagesIfFileIsAlreadyActive;
        }

        [ConfigItem(IsOptional = true)]
        public string NormalMesgOccurrenceName { get; set; }

        [ConfigItem(IsOptional = true)]
        public ulong NormalMesgUserFileIndexRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool NormalMesgFlushesWriter { get; set; }

        [ConfigItem(IsOptional = true)]
        public string SignificantMesgOccurrenceName { get; set; }

        [ConfigItem(IsOptional = true)]
        public Logging.LogGate SignificantMesgTypeLogGate { get; set; }

        [ConfigItem(IsOptional = true)]
        public ulong SignificantMesgUserFileIndexRowFlagBits { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool SignificantMesgIsHighPriority { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool SignificantMesgFlushesWriter { get; set; }

        [ConfigItem(IsOptional = true)]
        public FlushFlags FlushFlagstoUseAfterEachMessageBlock { get; set; }

        [ConfigItem(IsOptional = true)]
        public bool OnlyRecordMessagesIfFileIsAlreadyActive { get; set; }

        /// <summary>
        /// Update this object's ConfigItem marked public properties from corresponingly named config keys (using the namePrefix)
        /// </summary>
        public MDRFLogMessageHandlerAdapterConfig Setup(string prefixName = "Logging.LMH.MDRFLogMessageHandler.", IConfig config = null, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueEmitter = null)
        {
            ConfigValueSetAdapter<MDRFLogMessageHandlerAdapterConfig> adapter = new ConfigValueSetAdapter<MDRFLogMessageHandlerAdapterConfig>(config) 
            { 
                ValueSet = this, 
                SetupIssueEmitter = issueEmitter, 
                UpdateIssueEmitter = issueEmitter, 
                ValueNoteEmitter = valueEmitter 
            }.Setup(prefixName);

            return this;
        }
    }

    public class MDRFLogMessageHandlerAdapter : MosaicLib.Logging.Handlers.SimpleLogMessageHandlerBase
    {
        public MDRFLogMessageHandlerAdapter(string name, Logging.LogGate logGate, IMDRFWriter mdrfWriter, MDRFLogMessageHandlerAdapterConfig config)
            : base(name, logGate, recordSourceStackFrame: false)
        {
            this.mdrfWriter = mdrfWriter;
            Config = new MDRFLogMessageHandlerAdapterConfig(config);

            normalMesgOccurrenceInfo = new OccurrenceInfo() 
            { 
                Name = Config.NormalMesgOccurrenceName, 
                Comment = "occurrence used for lmh messages that are not configured to be treated as 'significant'",
                FileIndexUserRowFlagBits = Config.NormalMesgUserFileIndexRowFlagBits,
            };
            
            significantOccurrenceInfo = new OccurrenceInfo() 
            { 
                Name = Config.SignificantMesgOccurrenceName,
                Comment = "occurrence used for lmh messages that are configured to be treated as 'significant'",
                FileIndexUserRowFlagBits = Config.SignificantMesgUserFileIndexRowFlagBits, 
                IsHighPriority = Config.SignificantMesgIsHighPriority,
            };

            mdrfWriter.Add(normalMesgOccurrenceInfo, significantOccurrenceInfo);

            // do not attempt to dispose of the mdrfWriter when this class stops using it.
            AddExplicitDisposeAction(() => {mdrfWriter = null;});
        }

        IMDRFWriter mdrfWriter;
        MDRFLogMessageHandlerAdapterConfig Config { get; set; }

        OccurrenceInfo normalMesgOccurrenceInfo;
        OccurrenceInfo significantOccurrenceInfo;

        public override void HandleLogMessages(Logging.LogMessage[] lmArray)
        {
            int lmArrayLen = lmArray.SafeLength();

            if (lmArrayLen == 1)
                InnerInnerHandleLogMessage(lmArray[0]);
            else
            {
                foreach (Logging.LogMessage lm in lmArray)
                {
                    if (IsMessageTypeEnabled(lm))
                        InnerInnerHandleLogMessage(lm, blockFlush: true);
                }

                if (Config.FlushFlagstoUseAfterEachMessageBlock != FlushFlags.None && mdrfWriter != null && mdrfWriter.IsFileOpen)
                    mdrfWriter.Flush(Config.FlushFlagstoUseAfterEachMessageBlock);
            }


            NoteMessagesHaveBeenDelivered();
        }

        protected override void InnerHandleLogMessage(Logging.LogMessage lm)
        {
            if (lm != null && IsMessageTypeEnabled(lm))
                InnerInnerHandleLogMessage(lm);
        }

        private void InnerInnerHandleLogMessage(Logging.LogMessage lm, bool blockFlush = false, bool forceFlush = false)
        {
            if (lm == null || mdrfWriter == null || (Config.OnlyRecordMessagesIfFileIsAlreadyActive && !mdrfWriter.IsFileOpen))
                return;

            if (lm.NamedValueSet.Contains("noMDRF"))
                return;

            Logging.MesgType mesgType = lm.MesgType;
            bool mesgTypeIsSignificant = Config.SignificantMesgTypeLogGate.IsTypeEnabled(mesgType);

            IOccurrenceInfo occurrenceInfo = (mesgTypeIsSignificant ? significantOccurrenceInfo : normalMesgOccurrenceInfo);
            forceFlush |= !blockFlush && (mesgTypeIsSignificant ? Config.SignificantMesgFlushesWriter : Config.NormalMesgFlushesWriter);

            if (mdrfWriter != null && occurrenceInfo != null)
            {
                NamedValueSet mesgNVS = new NamedValueSet() { { "type", mesgType }, { "src", lm.LoggerName }, { "msg", lm.Mesg }};

                if (!lm.Data.IsNullOrEmpty())
                    mesgNVS.SetValue("data", lm.Data);

                if (!lm.NamedValueSet.IsNullOrEmpty())
                    mesgNVS.MergeWith(lm.NamedValueSet, NamedValueMergeBehavior.AddNewItems);

                mdrfWriter.RecordOccurrence(occurrenceInfo, new ValueContainer(mesgNVS), forceFlush: forceFlush);
            }
        }
    }

    #endregion
}

//-------------------------------------------------------------------
