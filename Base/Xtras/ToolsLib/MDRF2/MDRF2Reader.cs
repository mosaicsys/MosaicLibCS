//-------------------------------------------------------------------
/*! @file MDRF2REader.cs
 *  @brief classes that are used to support reading MDRF2 (Mosaic Data Recording Format 2) files.
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
using System.Threading;

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
using System.Collections;
using MessagePack;
using System.Collections.Concurrent;
using Mosaic.ToolsLib.Compression;

namespace Mosaic.ToolsLib.MDRF2.Reader
{
    #region IMDRF2DirectoryTracker, SyncFlags

    /// <summary>
    /// This is the interface that is supported by the MDRF2DirectoryTracker hybrid part.  This part has two basic behaviors:
    /// A) it supports part based directory content tracking and pre-reading of MDRF2FileInfo for each mdrf file in the directory, and
    /// B) it supports generation of query execution engines that allow clients to read and process the contents of files that are generally tracked by the part.
    /// These query execution engine's are executed on the caller's thread using a LINQ compatible IEnumerable{IMDRF2QueryRecord} based pattern.
    /// </summary>
    public interface IMDRF2DirectoryTracker : IMDRF2QueryFactory, IActivePartBase
    {
        /// <summary>
        /// Action factory method.  When the resulting action is run it will perform the synchronization operation specified by the caller provided <paramref name="syncFlags"/>.
        /// </summary>
        IClientFacet Sync(SyncFlags syncFlags = default);

        /// <summary>
        /// This publisher publishes the IMDRF2FileInfo for the most recently observed set of such files in the tracked directory.  
        /// Generally these files are sorted by the starting DateTime value that is contained in their corresponding DateTimeInfo item.
        /// </summary>
        INotificationObject<ReadOnlyIList<IMDRF2FileInfo>> MDRF2FileInfoSetPublisher { get; }

        /// <summary>
        /// Filters the current set of known files and returns the IMDRF2FileInfo for the known files that meet the query specification criteria (start and end dates mainly).
        /// </summary>
        IMDRF2FileInfo[] FilterFiles(MDRF2QuerySpec querySpec, bool autoSync = true);

        /// <summary>
        /// This publisher publishes the, optionally generated and updated, MDRF2FileDigestResult for the each of the most recently observed set of such files in the tracked directory.
        /// </summary>
        INotificationObject<ReadOnlyIList<MDRF2FileDigestResult>> MDRF2FileDigestResultSetPublisher { get; }
    }

    /// <summary>
    /// This enumeration defines the different synchronization behaviors that a client may request of the Directory Tracker instance.
    /// <para/>Normal (0x00), FullDirectoryScan (0x01)
    /// </summary>
    [Flags]
    public enum SyncFlags
    {
        /// <summary>Normal: just confirm that the inner service loop has been performed once to process any events that have already been reported by the file system watcher before proceeding. [0x00]</summary>
        Normal = 0x00,

        /// <summary>Full Dirctory Rescan: reset the file system watcher and re-enumerate the entire directory tree before proceeding. [0x01]</summary>
        FullDirctoryRescan = 0x01,

        /// <summary>When selected, this requests that the sync operation update (as needed) and publish MDRF2FileDigestResult for each tracked file.</summary>
        UpdateMDRF2FileDigestResults = 0x02,
    }

    #endregion

    #region Query factory interfaces and supporting classes (IMDRF2QueryFactory, IMDRF2QueryRecord++, IMDRF2FileInfo++, MDRF2QuerySpec, DynamicQueryAdjustItems++, MDRF2QueryFileSummary)

    /// <summary>
    /// This interface defines the query factory method.
    /// It supports generation of query execution engines that allow clients to read and process the contents of files.
    /// These query execution engine's are executed on the caller's thread using a LINQ compatible IEnumerable{IMDRF2QueryRecord} based pattern.
    /// </summary>
    public interface IMDRF2QueryFactory
    {
        /// <summary>
        /// Creates and returns a IEnumerable{IMDRF2QueryRecord} query execution object that can be used (once) to extract MDRF2 record contents from the given set of MDRF files (<paramref name="fileInfoParamsArray"/>) using the given <paramref name="querySpec"/>.
        /// <para/>Note: The returned query execution object can only be enumerated once.  Attempts to use it more than once will throw an exception.
        /// </summary>
        IEnumerable<IMDRF2QueryRecord> CreateQuery(MDRF2QuerySpec querySpec, params IMDRF2FileInfo [] fileInfoParamsArray);

        /// <summary>
        /// Creates and returns an IEnumerable{IMDRF2QueryRecord} query execution object that can be used (once) to extract MDRF2 record contents from the given set of MDRF files (<paramref name="fileInfoArray"/>) using the given <paramref name="querySpec"/>.
        /// The <paramref name="applyDynamicQueryAdjustItemsDelegate"/> passes the caller a delegate that can be used while enumerating the resulting query to change dynamically adjustable query parameters such as the PointSetSampleInterval.
        /// The <paramref name="enableAutoSync"/> property is used by a directory tracking query factory when the given <paramref name="fileInfoArray"/> is given as null, which causes the tracker to use its FilterFiles method to obtain the set of files to use.
        /// </summary>
        IEnumerable<IMDRF2QueryRecord> CreateQuery(MDRF2QuerySpec querySpec, IMDRF2FileInfo[] fileInfoArray, out ApplyDynamicQueryAdjustItemsDelegate applyDynamicQueryAdjustItemsDelegate, bool enableAutoSync = true);
    }

    /// <summary>Delegate form for caller (the enumeration execution context) to call back into the query execution engine to dynamically adjust supported settings.  These features are only functional when processing MDRF2 files.</summary>
    public delegate void ApplyDynamicQueryAdjustItemsDelegate(ref DynamicQueryAdjustItems dynamicQueryAdjustItems);

    /// <summary>Gives the set of optional values that the caller (the enumeration execution context) can pass back into the query spec to adjust the spec as it executes.  These features are only functional when processing MDRF2 files.</summary>
    public struct DynamicQueryAdjustItems
    {
        /// <summary>When non-null this requests the query engine to change the PointSet sample interval that it is using to use the given value.</summary>
        public TimeSpan? PointSetSampleInterval { get; set; }
    }

    /// <summary>
    /// Defines the base contents of a query record instance.
    /// </summary>
    public interface IMDRF2QueryRecord
    {
        /// <summary>Gives the IMDRF2FileInfo for the file from which this record was generated, or null if there is no such file for this record.</summary>
        IMDRF2FileInfo FileInfo { get; }

        /// <summary>Gives the MDRF2DateTimeStampPair for the record, or default if there is no such time stamp for this record.</summary>
        Common.MDRF2DateTimeStampPair DTPair { get; }

        /// <summary>Gives the MDRF2QueryItemTypeSelect flag for this record's type</summary>
        MDRF2QueryItemTypeSelect ItemType { get; }

        /// <summary>Gives the relevant UserRowFlagBits value for this record, as appropriate</summary>
        ulong UserRowFlagBits { get; }
 
        /// <summary>Gives the query record data as an object.  Generally the Data is accessed using the TItemType specific version of this interface which carries the typed Data property.</summary>
        object DataAsObject { get; }
    }

    /// <summary>
    /// Defines the base contents of a type specific version of a query record.
    /// </summary>
    public interface IMDRF2QueryRecord<TItemType> : IMDRF2QueryRecord
    {
        /// <summary>Gives the {TItemType} specific Data contents for this record.</summary>
        TItemType Data { get; }
    }

    /// <summary>
    /// Default reference type implementation class for all query records.
    /// </summary>
    public class MDRF2QueryRecord<TItemType> : IMDRF2QueryRecord<TItemType>
    {
        public IMDRF2FileInfo FileInfo { get; set; }
        public MDRF2QueryItemTypeSelect ItemType { get; set; }
        public ulong UserRowFlagBits { get; set; }
        public Common.MDRF2DateTimeStampPair DTPair { get; set; }
        public TItemType Data { get; set; }
        object IMDRF2QueryRecord.DataAsObject { get { return Data; } }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MDRF2QueryRecord<TItemType> Update(in Common.MDRF2DateTimeStampPair dtPair, TItemType data = default, ulong ? userRowFlagBits = null)
        {
            DTPair = dtPair;
            Data = data;
            UserRowFlagBits = userRowFlagBits ?? UserRowFlagBits;

            return this;
        }

        public override string ToString()
        {
            string dataStr = Data.SafeToString(mapNullTo: "[Null]");

            if (UserRowFlagBits != 0)
                return $"MDRF2QueryRecord<{typeof(TItemType).GetTypeLeafName()}> {ItemType} urfb:{UserRowFlagBits:x4} {DTPair} data:{dataStr}";
            else
                return $"MDRF2QueryRecord<{typeof(TItemType).GetTypeLeafName()}> {ItemType} {DTPair} data:{dataStr}";
        }
    }

    /// <summary>
    /// Default value type implementation of a basic query record.
    /// </summary>
    public struct MDRF2QueryRecordStruct : IMDRF2QueryRecord
    {
        public MDRF2QueryRecordStruct(IMDRF2QueryRecord other)
        {
            FileInfo = other.FileInfo;
            DTPair = other.DTPair;
            ItemType = other.ItemType;
            UserRowFlagBits = other.UserRowFlagBits;
            DataAsObject = other.DataAsObject;
        }

        public IMDRF2FileInfo FileInfo { get; private set; }
        public Common.MDRF2DateTimeStampPair DTPair { get; private set; }
        public MDRF2QueryItemTypeSelect ItemType { get; private set; }
        public ulong UserRowFlagBits { get; set; }
        public object DataAsObject { get; private set; }

        public override string ToString()
        {
            string dataStr = DataAsObject.SafeToString(mapNullTo: "[Null]");
            if (UserRowFlagBits != 0)
                return $"MDRF2QueryRecordStruct {ItemType} urfb:{UserRowFlagBits:x4} {DTPair} data:{dataStr}";
            else
                return $"MDRF2QueryRecordStruct {ItemType} {DTPair} data:{dataStr}";
        }
    }

    /// <summary>
    /// Value type used to contain extracted occurence information for occurrence specific query records.
    /// Contains the IOccurrenceInfo reference information for this occurrence and the ValueContainer value that was recorded.
    /// </summary>
    public struct MDRF2OccurrenceQueryRecordData
    {
        public MDRF2OccurrenceQueryRecordData(IOccurrenceInfo occurrenceInfo, ValueContainer vc)
        {
            OccurrenceInfo = occurrenceInfo;
            VC = vc;
        }

        public IOccurrenceInfo OccurrenceInfo { get; set; }
        public ValueContainer VC { get; set; }

        public override string ToString()
        {
            return $"{OccurrenceInfo} {VC}";
        }
    }

    /// <summary>
    /// Interface for MDRF2 summary file information
    /// </summary>
    public interface IMDRF2FileInfo : ICopyable<MDRF2FileInfo>
    {
        string FileNameAndRelativePath { get; }
        string FileNameFromConfiguredPath { get; }
        string FullPath { get; }

        ulong FileLength { get; }
        QpcTimeStamp ScanTimeStamp { get; }

        DateTimeInfo DateTimeInfo { get; }
        LibraryInfo LibraryInfo { get; }
        SetupInfo SetupInfo { get; }

        INamedValueSet SetupInfoNVS { get; }
        INamedValueSet ClientInfoNVS { get; }

        INamedValueSet NonSpecMetaNVS { get; }

        IList<INamedValueSet> SpecItemNVSSet { get; set; }
        SpecItemSet SpecItemSet { get; }

        string FaultCode { get; }
        bool IsUsable { get; }

        /// <summary>
        /// Attempts to populate this FileInfo object from the FullPath indicated file, if needed.  
        /// When non-null the <paramref name="optionalSettings"/> is used to specify handling of standard exception types.
        /// </summary>
        IMDRF2FileInfo PopulateIfNeeded(MDRF2QuerySpec optionalSettings = null);
    }

    /// <summary>
    /// Standard implementation type for IMDRF2FileInfo
    /// </summary>
    public class MDRF2FileInfo : IMDRF2FileInfo
    {
        public MDRF2FileInfo() { }

        public MDRF2FileInfo(string fileNameAndRelativePath, string fileNameFromConfiguredPath = null, string fullPath = null)
        {
            FileNameAndRelativePath = fileNameAndRelativePath;
            FileNameFromConfiguredPath = fileNameFromConfiguredPath ?? fileNameAndRelativePath;
            FullPath = fullPath ?? System.IO.Path.GetFullPath(fileNameAndRelativePath);
        }

        public string FileNameAndRelativePath { get; set; }
        public string FileNameFromConfiguredPath { get; set; }
        public string FullPath { get; set; }

        public ulong FileLength { get; set; }
        public QpcTimeStamp ScanTimeStamp { get; set; }

        public DateTimeInfo DateTimeInfo { get; set; }
        public LibraryInfo LibraryInfo { get; set; }
        public SetupInfo SetupInfo { get; set; }

        public INamedValueSet SetupInfoNVS { get; set; }
        public INamedValueSet ClientInfoNVS { get; set; }

        public INamedValueSet NonSpecMetaNVS { get; set; }

        public IList<INamedValueSet> SpecItemNVSSet { get; set; }
        public SpecItemSet SpecItemSet { get; set; }

        public string FaultCode { get; set; }
        public bool IsUsable { get { return (FaultCode.IsNullOrEmpty() && DateTimeInfo != null && LibraryInfo != null && SetupInfo != null && SpecItemSet != null); } }

        public MDRF2FileInfo MakeCopyOfThis(bool deepCopy = true)
        {
            return (MDRF2FileInfo)MemberwiseClone();
        }

        public override string ToString()
        {
            if (IsUsable)
                return $"'{FileNameAndRelativePath}' len:{FileLength} client:'{SetupInfo.ClientName}'";
            else if (FaultCode.IsNeitherNullNorEmpty())
                return $"'{FileNameAndRelativePath}' len:{FileLength} ec:'{FaultCode}'";
            else
                return $"'{FileNameAndRelativePath}' len:{FileLength} [FileInfoNotFullyLoadedYet]";
        }

        private static readonly Logging.IBasicLogger populateIfNeededLogger = new Logging.Logger($"{Fcns.CurrentClassLeafName}.PopulateIfNeeded");

        /// <inheritdoc>
        public IMDRF2FileInfo PopulateIfNeeded(MDRF2QuerySpec settings = null)
        {
            try
            {
                if (IsUsable)
                    return this;

                MDRF.Reader.MDRFFileReaderBehavior mdrf1FileReaderBehavior = settings?.MDRF1FileReaderBehavior ?? MDRF.Reader.MDRFFileReaderBehavior.AttemptToDecodeOccurrenceBodyAsNVS;
                MDRF2FileReaderBehavior mdrf2FileReaderBehavior = settings?.MDRF2FileReaderBehavior ?? MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile;

                using (var reader = new MDRF2FileReadingHelper(FullPath, populateIfNeededLogger, mdrf1FileReaderBehavior: mdrf1FileReaderBehavior, mdrf2FileReaderBehavior: mdrf2FileReaderBehavior))
                {
                    // attempt to read the header.  This may fail or throw if the file does not have a complete set of header records written to it yet.
                    reader.ReadHeadersIfNeeded();

                    return reader.FileInfo;
                }
            }
            catch (System.Exception ex)
            {
                var fileInfo = MakeCopyOfThis();

                fileInfo.FaultCode = $"{Fcns.CurrentMethodName} for '{FileNameAndRelativePath}' failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}";

                return fileInfo;
            }
        }
    }

    /// <summary>
    /// Query specification used with MDRF2 query factories.
    /// </summary>
    public class MDRF2QuerySpec
    {
        /// <summary>Default constructor</summary>
        public MDRF2QuerySpec() { }

        /// <summary>Copy constructor</summary>
        public MDRF2QuerySpec(MDRF2QuerySpec other)
        {
            Name = other.Name;
            StartUTCTimeSince1601 = other.StartUTCTimeSince1601;
            EndUTCTimeSince1601 = other.EndUTCTimeSince1601;
            ItemTypeSelect = other.ItemTypeSelect;
            PointSetSpec = other.PointSetSpec;
            OccurrenceNameArray = other.OccurrenceNameArray;
            ObjectTypeNameSet = other.ObjectTypeNameSet;
            ObjectSpecificUserRowFlagBits = other.ObjectSpecificUserRowFlagBits;
            AllowRecordReuse = other.AllowRecordReuse;
            MDRF2FileReaderBehavior = other.MDRF2FileReaderBehavior;
            MDRF1FileReaderBehavior = other.MDRF1FileReaderBehavior;
        }

        /// <summary>Client usable name for this query (included any related query execution log messages).  Defaults to "MDRF2QuerySpec"</summary>
        public string Name { get; set; } = "MDRF2QuerySpec";

        /// <summary>Conversion getter/setter for use of StartUTCTimeSince1601 as a DateTime</summary>
        public DateTime StartDateTime { get { return StartUTCTimeSince1601.GetDateTimeFromUTCTimeSince1601(); } set { StartUTCTimeSince1601 = value.GetUTCTimeSince1601(); } }

        /// <summary>Conversion getter/setter for use of EndUTCTimeSince1601 as a DateTime</summary>
        public DateTime EndDateTime { get { return EndUTCTimeSince1601.GetDateTimeFromUTCTimeSince1601(); } set { EndUTCTimeSince1601 = value.GetUTCTimeSince1601(); } }

        /// <summary>Gives the first UTCTimeSince1601 for which query records are desired.  The query engine may return records recorded before this time in the regions where it filters which files and file contents to process but will not generally process objects, occurrences or group data that was recorded before this time.  Defaults to NegativeInfinity.</summary>
        public double StartUTCTimeSince1601 { get; set; } = double.NegativeInfinity;

        /// <summary>Gives the last UTCTimeSince1601 for which query records are desired.  The query engine may return records recorded after this time in the regions where it filters which files and file contents to process but will not generally process objects, occurrences or group data that was recorded after this time.  Defaults to NegativeInfinity.</summary>
        public double EndUTCTimeSince1601 { get; set; } = double.PositiveInfinity;

        /// <summary>Gives the client provided mask for the set of Query item types that the client would like to be given.  Some record types are enabled explicitly in other areas of this query spec (points, occurrences, objects, ...)</summary>
        public MDRF2QueryItemTypeSelect ItemTypeSelect { get; set; } = MDRF2QueryItemTypeSelect.None;

        /// <summary>
        /// Defines the set of point names (subject to lookup and name mapping rules) that the client would like to be given data contents for along with the type of data that shall be returned (VC, F8 or F4) and the nominal sample interval that the client would like to use.  
        /// When this is null, or its string array is null and its MDRF2PointSetArrayItemType is None, it indicates that all points are to be included in the selected notation.  To select no point data set the point array type selection to None.
        /// When the client includes the MDRF2PointSetArrayItemType.ResuseArrays flag in the contained point set item type selection field, it will cause the resulting point set query records that are generated to all refer to the same array instance which is directly updated as the group data is processed.
        /// When this flag is not included, each resulting emitted record contents will be given a copy of the array that the data is extracted into so that the client may directly retain these arrays as the representation of the extracted pont time series.
        /// This flag is often used in conjuction with the setting of the AllowRecordReuse query spec property, although these two values may be used seperately from one another.
        /// </summary>
        public Tuple<string[], MDRF2PointSetArrayItemType, TimeSpan> PointSetSpec { get; set; } = Tuple.Create<string[], MDRF2PointSetArrayItemType, TimeSpan>(null, MDRF2PointSetArrayItemType.None, TimeSpan.Zero);

        /// <summary>Gives the set of occurrence names that the client would like records generated for.  If this is null then all occurrences will be included.  Set this to the empty array to disable occurrence reporting.</summary>
        public string[] OccurrenceNameArray { get; set; } = EmptyArrayFactory<string>.Instance;

        /// <summary>
        /// Gives the set of object type names that the client would like records generated for.  If this is null then all records will (attempt to) be generated for all object types.  Set this to the empty set to disable reporting of objects.
        /// Note: the MDRF2QueryItemTypeSelect.Object flag must be included in the ItemTypeSelect in order for the query engine to yeild object records.
        /// </summary>
        public ReadOnlyHashSet<string> ObjectTypeNameSet { get; set; } = ReadOnlyHashSet<string>.Empty;

        /// <summary>
        /// When this is non-zero it is used to indicate the set of user row flag bit values that are associated with the object types to be decoded and will prevent decoding blocks (and individual records in some cases) when they do not contain any such object instances.
        /// <para/>Note: use of this option may block decoding of object types that were not recorded with any matching flag bit value, or which were recorded with zero for the flag bit value.
        /// </summary>
        public ulong ObjectSpecificUserRowFlagBits { get; set; }

        /// <summary>
        /// When this value is false (the default) the query execution engine will generate new record instances for all records that are yeilded by the enumeration.  
        /// This allows the client to directly accumulate and retain the individual record instances without additional work.  
        /// When this value is passed as true it allows the query execution engine to re-use record instances in the enumeration output.  
        /// This is only intended to support clients that immediately haravest the desired information from emitted records (or explicitly clone them) and which thus do not retain any of the enumeration's directly record instances
        /// The use of this option is intended to decrease the load on the GC engine for clients that do not directly retain the emitted record instances.
        /// Please note that for some low frequency record types, new record instances are always generated without regard to the setting of this flag. 
        /// This flag is often used in conjunction with the MDRF2PointSetArrayItemType.ResuseArrays flag.
        /// [defaults to false]
        /// </summary>
        public bool AllowRecordReuse { get; set; }

        [Obsolete("Replace use of this property with use of the corresponding behavior in the MDRF2FileReaderBehavior given below (2021-04-06)")]
        public bool EndOfStreamExceptionIsEndOfFile { get { return MDRF2FileReaderBehavior.IsSet(MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile); } set { MDRF2FileReaderBehavior = MDRF2FileReaderBehavior.Set(MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile, value); } }

        [Obsolete("Replace use of this property with use of the corresponding behavior in the MDRF2FileReaderBehavior given below (2021-04-06)")]
        public bool DecompressionEngineExceptionIsEndOfFile { get { return MDRF2FileReaderBehavior.IsSet(MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile); } set { MDRF2FileReaderBehavior = MDRF2FileReaderBehavior.Set(MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile, value); } }

        /// <summary>Gives the MDRF2FileReaderBehavior to use when reading MDRF2 files as part of this query.</summary>
        public MDRF2FileReaderBehavior MDRF2FileReaderBehavior { get; set; } = MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile | MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile;

        /// <summary>Gives the MDRFFileReaderBehavior to use when reading MDRF1 files as part of this query.</summary>
        public MDRF.Reader.MDRFFileReaderBehavior MDRF1FileReaderBehavior { get; set; } = MDRF.Reader.MDRFFileReaderBehavior.AttemptToDecodeOccurrenceBodyAsNVS;
    }

    /// <summary>
    /// This enumeration is used as part of a PointSetSpec tuple to define the format of the point data that the client is requesting.
    /// The client can ask for point set record data to be an array ov ValueContainers (VC), an array of doubles (F8) or an array of floats (F4).
    /// By default each resulting report is a new object instance and it will contain a new array instance so that the query client may accumulate the resulting records.
    /// If the query client is going to transform the resulting values into some other form on the fly then the query client may additionally requests the ReuseArrayAndRecord option that causes
    /// the query execution logic to reuse the same array and record instance for each resulting point set record.
    /// <para/>None (0x00), VC (0x01), F8 (0x02), F4 (0x04), TypeMask (0x07), ReuseArrays (0x08)
    /// </summary>
    [Flags]
    public enum MDRF2PointSetArrayItemType : int
    {
        /// <summary>Placeholder default [0x00]</summary>
        None = 0x00,
        /// <summary>Array of ValueContainer format requested [0x01]</summary>
        VC = 0x01,
        /// <summary>Array of Double format requested [0x02]</summary>
        F8 = 0x02,
        /// <summary>Array of Single format requested [0x04]</summary>
        F4 = 0x04,
        /// <summary>Mask for the supported types (VC | F8 | F4) [0x07]</summary>
        TypeMask = (VC | F8 | F4),
        /// <summary>Flag bit used to request array object instance reuse.  Without this flag bit selected new array instances will be generated for each PointSet record.  With this flag bit selected the same array instance will be updated and used in all such PointSet records.  Only use this flag bit in query clients that plan to copy the results elsewhere as the query results are generated. [0x08]</summary>
        ReuseArrays = 0x08,
    }

    /// <summary>
    /// Defines a bit mask of query record types the client would like to see in the query record output stream.  
    /// This is also used used in IMDRF2QueryRecord object to indicate the record type.
    /// Specific bits are ignored when used as a mask since the generation of the corresponding record type is controlled by other elements in the filter specification.
    /// <para/>None (0x0000), FileStart (0x0001), FileEnd (0x0002), InlineIndexRecord (0x0004), FileDeltaTimeUpdate (0x0008),
    /// Occurrence (0x0010), Mesg (0x0020), Error (0x0040), Object (0x0100), PointSet (0x1000), BlockEnd (0x10000),
    /// ExtractionStart (0x100000), ExtractionComplete (0x200000), DecodingIssue (0x1000000), ExtractionStopped (0x2000000), ExtractionCancelled (0x4000000)
    /// WillSkip(0x80000000)
    /// </summary>
    [Flags]
    public enum MDRF2QueryItemTypeSelect : uint
    {
        /// <summary>Placeholder default [0x00]</summary>
        None = 0x0000,
        /// <summary>produces MDRF2QueryFileSummary - signalled on start of extraction from a new file - may come after one or two InlineIndexRecord. - FileInfo includes all header and item specification information loaded from this file. [0x01]</summary>
        FileStart = 0x0001,
        /// <summary>produces MDRF2QueryFileSummary [0x02]</summary>
        FileEnd = 0x0002,
        /// <summary>produces InlineIndexRecord - indicates start of a block - these may be emitted without regard to start and end time filters [0x04]</summary>
        InlineIndexRecord = 0x0004,
        /// <summary>produces Common.MDRF2DateTimeStampPair [0x08]</summary>
        FileDeltaTimeUpdate = 0x0008,
        /// <summary>produces MDRF2OccurrenceQueryRecordData [0x10]</summary>
        Occurrence = 0x0010,
        /// <summary>produces string [0x20]</summary>
        Mesg = 0x0020,
        /// <summary>produces string [0x40]</summary>
        Error = 0x0040,
        /// <summary>produces object [0x100]</summary>
        Object = 0x0100,
        /// <summary>produces array of VC, F8 or F4 - array is in order of requested points by name. [0x1000]</summary>
        PointSet = 0x1000,
        /// <summary>produces InlineIndexRecord [0x10000]</summary>
        BlockEnd = 0x10000,
        /// <summary>produces object (null) signaled once at initial start (FileInfo will be null) [0x200000]</summary>
        ExtractionStart = 0x100000,
        /// <summary>produces object (null) [0x200000]</summary>
        ExtractionComplete = 0x200000,
        /// <summary>produces string or exception [0x1000000]</summary>
        DecodingIssue = 0x1000000,
        /// <summary>produces exception [0x2000000]</summary>
        ExtractionStopped = 0x2000000,
        /// <summary>produces exception [0x4000000]</summary>
        ExtractionCancelled = 0x4000000, 

        /// <summary>used with InlineIndexRecord to indicate that the block's contents are being skipped [0x80000000]</summary>
        WillSkip = 0x80000000,

        /// <summary>Flag used internally with synthetically created items from an MDRF1 file source, especially InlineIndexRecord (RowStart) and BlockEnd (RowEnd)</summary>
        MDRF1 = 0x40000000,

        /// <summary>(FileStart | FileEnd | InlineIndexRecord | FileDeltaTimeUpdate | Occurrence | Mesg | Error | Object | PointSet | BlockEnd | ExtractionStart | ExtractionComplete | DecodingIssue | ExtractionStopped | ExtractionCancelled | WillSkip)</summary>
        All = (FileStart | FileEnd | InlineIndexRecord | FileDeltaTimeUpdate | Occurrence | Mesg | Error | Object | PointSet | BlockEnd | ExtractionStart | ExtractionComplete | DecodingIssue | ExtractionStopped | ExtractionCancelled | WillSkip),
    }

    /// <summary>
    /// This class contains summary information about an MDRF or MDRF2 file that is accumulated while processing a query for the corresponding IMDRF2FileInfo file.
    /// </summary>
    public class MDRF2QueryFileSummary : ICopyable<MDRF2QueryFileSummary>
    {
        public IMDRF2FileInfo FileInfo { get; set; }
        public Common.MDRF2DateTimeStampPair FirstFileDTPair { get; set; }
        public Common.MDRF2DateTimeStampPair LastFileDTPair { get; set; }
        public TimeSpan ElapsedTime { get { return (LastFileDTPair.UTCTimeSince1601 - FirstFileDTPair.UTCTimeSince1601).FromSeconds(); } }
        public string FaultCode { get; set; }
        public ulong FileLength { get; set; }
        public ulong BytesProcessed { get; set; }
        public ulong RecordCount { get; set; }
        public ulong InlineIndexCount { get; set; }
        public ulong SkippedBlockCount { get; set; }
        public ulong SkippedRecordCount { get; set; }
        public ulong SkippedByteCount { get; set; }
        public FileIndexRowFlagBits SkippedFileIndexRowFlagBits { get; set; }
        public ulong SkippedUserRowFlagBits { get; set; }
        public ulong FDTUpdateCount { get; set; }
        public ulong ProcessedGroupCount { get; set; }
        public ulong ProcessedOccurrenceCount { get; set; }
        public ulong ProcessedObjectCount { get; set; }
        public ulong TotalBlockGroupSetCount { get; set; }
        public ulong TotalBlockOccurrenceCount { get; set; }
        public ulong TotalBlockObjectCount { get; set; }
        public ulong[] EncounteredCountByGroupIDArray { get; set; }
        public ulong[] EncounteredCountByOccurrenceIDArray { get; set; }
        public int EndRecordFoundCount { get; set; }
        public bool EndRecordFound { get { return EndRecordFoundCount != 0; } set { EndRecordFoundCount = value.MapToInt(); } }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NoteProcessingBlockFor(Common.InlineIndexRecord inlineIndexRecord)
        {
            TotalBlockGroupSetCount += inlineIndexRecord.BlockGroupSetCount;
            TotalBlockOccurrenceCount += inlineIndexRecord.BlockOccurrenceCount;
            TotalBlockObjectCount += inlineIndexRecord.BlockObjectCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NoteSkippingBlockFor(Common.InlineIndexRecord inlineIndexRecord)
        {
            SkippedBlockCount += 1;
            SkippedRecordCount += inlineIndexRecord.BlockRecordCount;
            SkippedByteCount += inlineIndexRecord.BlockByteCount;
            SkippedFileIndexRowFlagBits |= inlineIndexRecord.BlockFileIndexRowFlagBits;
            SkippedUserRowFlagBits |= inlineIndexRecord.BlockUserRowFlagBits;

            TotalBlockGroupSetCount += inlineIndexRecord.BlockGroupSetCount;
            TotalBlockOccurrenceCount += inlineIndexRecord.BlockOccurrenceCount;
            TotalBlockObjectCount += inlineIndexRecord.BlockObjectCount;
        }

        public MDRF2QueryFileSummary MakeCopyOfThis(bool deepCopy = true)
        {
            return (MDRF2QueryFileSummary)MemberwiseClone();
        }

        public MDRF2QueryFileSummary Sum(MDRF2QueryFileSummary other)
        {
            FileInfo = FileInfo ?? other.FileInfo;
            if (FirstFileDTPair.IsEmpty)
                FirstFileDTPair = other.FirstFileDTPair;
            if (LastFileDTPair.IsEmpty)
                LastFileDTPair = other.LastFileDTPair;
            FaultCode = FaultCode.MapNullOrEmptyTo(other.FaultCode);
            FileLength += other.FileLength;
            BytesProcessed += other.BytesProcessed;
            RecordCount += other.RecordCount;
            InlineIndexCount += other.InlineIndexCount;
            SkippedBlockCount += other.SkippedBlockCount;
            SkippedRecordCount += other.SkippedRecordCount;
            SkippedByteCount += other.SkippedByteCount;
            SkippedFileIndexRowFlagBits |= other.SkippedFileIndexRowFlagBits;
            SkippedUserRowFlagBits |= other.SkippedUserRowFlagBits;
            FDTUpdateCount = other.FDTUpdateCount;
            ProcessedOccurrenceCount += other.ProcessedOccurrenceCount;
            ProcessedGroupCount += other.ProcessedGroupCount;
            ProcessedObjectCount += other.ProcessedObjectCount;
            TotalBlockGroupSetCount += other.TotalBlockGroupSetCount;
            TotalBlockOccurrenceCount += other.TotalBlockOccurrenceCount;
            TotalBlockObjectCount += other.TotalBlockObjectCount;
            EndRecordFoundCount += other.EndRecordFoundCount;

            EncounteredCountByGroupIDArray = EncounteredCountByGroupIDArray.Add(other.EncounteredCountByGroupIDArray);
            EncounteredCountByOccurrenceIDArray = EncounteredCountByOccurrenceIDArray.Add(other.EncounteredCountByOccurrenceIDArray);

            return this;
        }
    }

    #endregion

    #region Extension Methods

    public static partial class ExtensionMethods
    {
        /// <summary>Method used when counting records by group and occurrence IDs</summary>
        public static ulong [] Add(this ulong[] array, ulong [] otherArray)
        {
            var arrayLen = array.SafeLength();
            var otherArrayLen = otherArray.SafeLength();
            if (arrayLen < otherArrayLen)
                System.Array.Resize(ref array, otherArrayLen);

            for (int idx = 0; idx < otherArrayLen; idx++)
                array[idx] += otherArray[idx];

            return array;
        }

        /// <summary>Helper method used to determine which portion of the given <paramref name="set"/> of MDRF2FileInfo objects will be included in the time range specified by the given <paramref name="querySpec"/></summary>
        public static bool RangeSearch(this IList<IMDRF2FileInfo> set, MDRF2QuerySpec querySpec, out int startIndex, out int count)
        {
            startIndex = 0;
            if (set.IsNullOrEmpty())
            {
                count = 0;
                return (set != null);
            }

            var setFirst = set.First();
            var setLast = set.Last();
            var setCount = set.Count;
            var startUTCTimeSince1601 = querySpec.StartUTCTimeSince1601;
            var endUTCTimeSince1601 = querySpec.EndUTCTimeSince1601;

            if (startUTCTimeSince1601 <= setFirst.DateTimeInfo.UTCTimeSince1601 && endUTCTimeSince1601 >= setLast.DateTimeInfo.UTCTimeSince1601)
            {
                count = setCount;
                return true;
            }

            if (endUTCTimeSince1601 < setFirst.DateTimeInfo.UTCTimeSince1601)
            {
                startIndex = setCount - 1;
                count = 0;
                return false;
            }

            if (startUTCTimeSince1601 > setFirst.DateTimeInfo.UTCTimeSince1601)
            {
                int pastStartSearchEndIndex = startIndex + setCount;

                while (startIndex < pastStartSearchEndIndex)
                {
                    var testIndex = (startIndex + pastStartSearchEndIndex) >> 1;

                    var testItem = set.SafeAccess(testIndex);
                    var testItemNext = set.SafeAccess(testIndex + 1);
                    if (testItem == null || testItemNext == null)
                        break;

                    var testValue = testItem.DateTimeInfo.UTCTimeSince1601;
                    var testValueNext = testItemNext.DateTimeInfo.UTCTimeSince1601;

                    if (startUTCTimeSince1601 < testValue)
                        pastStartSearchEndIndex = testIndex;
                    else if (startUTCTimeSince1601 == testValue || startUTCTimeSince1601 < testValueNext)
                        break;
                    else
                        startIndex = testIndex + 1;
                }
            }

            if (endUTCTimeSince1601 >= setLast.DateTimeInfo.UTCTimeSince1601)
            {
                count = setCount - startIndex;
                return true;
            }
            else
            {
                int lastItemIndex = setCount - 1;
                int startSearchFence = startIndex;

                while (startSearchFence < (lastItemIndex - 1))
                {
                    var testIndex = (startSearchFence + lastItemIndex) >> 1;
                    var testValue = set[testIndex].DateTimeInfo.UTCTimeSince1601;

                    if (endUTCTimeSince1601 < testValue)
                        lastItemIndex = testIndex;
                    else if (endUTCTimeSince1601 > testValue)
                        startSearchFence = testIndex;
                    else // they must be equal
                        break;
                }

                count = Math.Min(lastItemIndex + 1 - startIndex, setCount);
                return true;
            }
        }


        /// <summary>Takes the given <paramref name="queryFactory"/> and <paramref name="fileInfo"/> and produces a tuple with the updated MDRF2FileInfo (if needed), the MDRF2QueryFileSummary and the array of InlineIndexRecords for the given <paramref name="fileInfo"/>.</summary>
        public static Tuple<IMDRF2FileInfo, MDRF2QueryFileSummary, Common.InlineIndexRecord[]> GetFileDigestInfo(this IMDRF2QueryFactory queryFactory, IMDRF2FileInfo fileInfo, MDRF2QuerySpec optionalSettings = null)
        {
            var result = queryFactory.GetMDRF2FileDigest(fileInfo, optionalSettings);
            return Tuple.Create(result.FileInfo, result.FileSummary, result.InlineIndexRecordArray);
        }

        /// <summary>Takes the given <paramref name="queryFactory"/> and <paramref name="fileInfo"/> and produces the MDRF2FileDigestResult for it.</summary>
        public static MDRF2FileDigestResult GetMDRF2FileDigest(this IMDRF2QueryFactory queryFactory, IMDRF2FileInfo fileInfo, MDRF2QuerySpec optionalSettings = null)
        {
            MDRF2QuerySpec querySpec = new MDRF2QuerySpec(optionalSettings ?? getDigistInfoFallbackQuerySpec)
            {
                ItemTypeSelect = MDRF2QueryItemTypeSelect.InlineIndexRecord | MDRF2QueryItemTypeSelect.FileEnd,
                PointSetSpec = Tuple.Create<string[], MDRF2PointSetArrayItemType, TimeSpan>(null, MDRF2PointSetArrayItemType.None, TimeSpan.Zero),
                OccurrenceNameArray = EmptyArrayFactory<string>.Instance,
                ObjectTypeNameSet = ReadOnlyHashSet<string>.Empty,
            };

            MDRF2QueryFileSummary fileSummary = null;
            List<Common.InlineIndexRecord> inlineIndexRecordList = new List<Common.InlineIndexRecord>();

            foreach (var record in queryFactory.CreateQuery(querySpec, fileInfo))
            {
                var inlineIndexRecord = record.DataAsObject as Common.InlineIndexRecord;

                if (inlineIndexRecord != null)
                {
                    inlineIndexRecordList.Add(inlineIndexRecord);
                    continue;
                }

                var fileSummaryRecord = record.DataAsObject as MDRF2QueryFileSummary;

                if (fileSummaryRecord != null)
                {
                    fileSummary = fileSummaryRecord;
                    break;
                }
            }

            return new MDRF2FileDigestResult() { FileInfo = fileInfo, FileSummary = fileSummary, InlineIndexRecordArray = inlineIndexRecordList.ToArray() };
        }

        private static readonly MDRF2QuerySpec getDigistInfoFallbackQuerySpec = new MDRF2QuerySpec();
    }

    /// <summary>
    /// Used to contain the results of a GetFileDigest request.
    /// </summary>
    public struct MDRF2FileDigestResult
    {
        public IMDRF2FileInfo FileInfo { get; set; }
        public MDRF2QueryFileSummary FileSummary { get; set; }
        public Common.InlineIndexRecord[] InlineIndexRecordArray { get; set; } 

        /// <summary>Returns true if the FileInfo IsUsable and the FileSummary and InlineIndexRecordArray are both non-null</summary>
        public bool IsUsable { get => (FileInfo?.IsUsable == true && FileSummary != null && InlineIndexRecordArray != null); }
    }

    #endregion

    #region MDRF2DiretoryTrackerConfig, MDRF2DirectoryTracker, MDRF2QueryFactory

    public class MDRF2DirectoryTrackerConfig : ICopyable<MDRF2DirectoryTrackerConfig>
    {
        public MDRF2DirectoryTrackerConfig(string partID)
        {
            PartID = partID;
        }

        public string PartID { get; private set; }
        public string RootDirPath { get; set; } = ".";
        public TimeSpan ScanForChangesInterval { get; set; } = (30.0).FromSeconds();
        public int FSWInternalBufferSize { get; set; } = 65536;
        public ActionLoggingConfig ActionLoggingConfig { get; set; } = ActionLoggingConfig.Debug_Debug_Trace_Trace.Update(actionLoggingStyleSelect: ActionLoggingStyleSelect.IncludeRunTimeOnCompletion);
        public bool UpdateMDRF2FileDigestResultsAutomatically { get; set; } = false;

        [Obsolete("Replace use of this property with use of the corresponding behavior in the MDRF2FileReaderBehavior given below (2021-04-06)")]
        public bool EndOfStreamExceptionIsEndOfFile { get { return MDRF2FileReaderBehavior.IsSet(MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile); } set { MDRF2FileReaderBehavior = MDRF2FileReaderBehavior.Set(MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile, value); } }

        [Obsolete("Replace use of this property with use of the corresponding behavior in the MDRF2FileReaderBehavior given below (2021-04-06)")]
        public bool DecompressionEngineExceptionIsEndOfFile { get { return MDRF2FileReaderBehavior.IsSet(MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile); } set { MDRF2FileReaderBehavior = MDRF2FileReaderBehavior.Set(MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile, value); } }

        /// <summary>Gives the MDRF2FileReaderBehavior to use when reading MDRF2 files as part of this query.</summary>
        public MDRF2FileReaderBehavior MDRF2FileReaderBehavior { get; set; } = MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile | MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile;

        /// <summary>Gives the MDRFFileReaderBehavior to use when reading MDRF1 files as part of this query.</summary>
        public MDRF.Reader.MDRFFileReaderBehavior MDRF1FileReaderBehavior { get; set; } = MDRF.Reader.MDRFFileReaderBehavior.AttemptToDecodeOccurrenceBodyAsNVS;

        public MDRF2DirectoryTrackerConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (MDRF2DirectoryTrackerConfig) MemberwiseClone();
        }
    }

    public class MDRF2DirectoryTracker : SimpleActivePartBase, IMDRF2DirectoryTracker
    {
        public MDRF2DirectoryTracker(MDRF2DirectoryTrackerConfig config) 
            : base(config.PartID, SimpleActivePartBaseSettings.DefaultVersion2)
        {
            Config = config.MakeCopyOfThis();
            RootDirFullPath = System.IO.Path.GetFullPath(Config.RootDirPath);

            ActionLoggingReference.Config = Config.ActionLoggingConfig;

            if (!System.IO.Directory.Exists(RootDirFullPath))
                System.IO.Directory.CreateDirectory(RootDirFullPath);

            QueryLogger = new Logging.Logger($"{PartID}.query");

            AddMainThreadStoppingAction(() => Release());
        }

        public void Release()
        {
            ResetFSWAndMarkAllTrackersTouched();
            Fcns.DisposeOfObject(ref fsw);
        }

        private MDRF2DirectoryTrackerConfig Config { get; set; }
        private readonly string RootDirFullPath;
        public Logging.ILogger QueryLogger { get; private set; }

        protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
        {
            if (andInitialize)
                Release();

            try
            {
                if (fsw == null)
                {
                    fsw = new FileSystemWatcher()
                    {
                        Path = RootDirFullPath,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                        Filter = "*.*",
                        IncludeSubdirectories = false,
                        EnableRaisingEvents = false,
                        InternalBufferSize = Config.FSWInternalBufferSize, // defaults to 8192
                    };

                    fsw.Changed += (o, e) => { asyncTouchedFileNamesSet.Add(e.Name); Notify(); };
                    fsw.Created += (o, e) => { asyncTouchedFileNamesSet.Add(e.Name); Notify(); };
                    fsw.Deleted += (o, e) => { asyncTouchedFileNamesSet.Add(e.Name); Notify(); };
                    fsw.Error += (o, e) => { asyncErrorCount++; Notify(); Log.Debug.Emit("Received unexpected FileSystemWatcher Error Event: {0}", e); };
                    fsw.Renamed += (o, e) => { asyncTouchedFileNamesSet.Add(e.Name); asyncTouchedFileNamesSet.Add(e.OldName); Notify(); };
                }
            }
            catch (System.Exception ex)
            {
                Fcns.DisposeOfObject(ref fsw);
                Log.Debug.Emit($"Unable to construct a usable FileSystemWatcher for '{Config.RootDirPath}': {ex.ToString(ExceptionFormat.TypeAndMessage)}");
            }

            rescanIntervalTimer = new QpcTimer() { TriggerInterval = Config.ScanForChangesInterval, SelectedBehavior = QpcTimer.Behavior.NewAutoReset }.Start();

            Service(forceFullUpdate: true);

            return string.Empty;
        }

        protected override string PerformGoOfflineAction(IProviderActionBase action)
        {
            Release();

            return base.PerformGoOfflineAction(action);
        }

        protected override void PerformMainLoopService()
        {
            if (BaseState.IsOnlineOrAttemptOnline)
                Service();

            base.PerformMainLoopService();
        }

        public IClientFacet Sync(SyncFlags syncFlags = default)
        {
            return new BasicActionImpl(ActionQueue, (ipf) => PerformSync(ipf, syncFlags), "Sync", ActionLoggingReference, $"{syncFlags}");
        }

        private string PerformSync(IProviderFacet ipf, SyncFlags syncFlags)
        {
            bool fullDirectoryRescan = (syncFlags & SyncFlags.FullDirctoryRescan) != 0;
            bool updateFileDigestResults = (syncFlags & SyncFlags.UpdateMDRF2FileDigestResults) != 0;

            Service(forceFullUpdate: fullDirectoryRescan, updateMDRF2FileDigestResults: updateFileDigestResults);

            return string.Empty;
        }

        /// <inheritdoc/>
        public INotificationObject<ReadOnlyIList<IMDRF2FileInfo>> MDRF2FileInfoSetPublisher { get => _MDRF2FileInfoSetPublisher; }
        private InterlockedNotificationRefObject<ReadOnlyIList<IMDRF2FileInfo>> _MDRF2FileInfoSetPublisher = new InterlockedNotificationRefObject<ReadOnlyIList<IMDRF2FileInfo>>() { Object = ReadOnlyIList<IMDRF2FileInfo>.Empty };

        /// <inheritdoc/>
        public INotificationObject<ReadOnlyIList<MDRF2FileDigestResult>> MDRF2FileDigestResultSetPublisher { get => _MDRF2FileDigestResultSetPublisher; }
        private InterlockedNotificationRefObject<ReadOnlyIList<MDRF2FileDigestResult>> _MDRF2FileDigestResultSetPublisher = new InterlockedNotificationRefObject<ReadOnlyIList<MDRF2FileDigestResult>>() { Object = ReadOnlyIList<MDRF2FileDigestResult>.Empty };

        private System.IO.FileSystemWatcher fsw;
        private class AsyncTouchedFileNamesSet
        {
            public object mutex = new object();
            public HashSet<string> set = new HashSet<string>();
            public volatile int setCount = 0;

            public void Add(string name) { lock (mutex) { set.Add(name); setCount = set.Count; } }
            public void Clear() { lock (mutex) { set.Clear(); setCount = set.Count; } }
            public void TransferContentsTo(HashSet<string> other)
            {
                lock (mutex)
                {
                    other.UnionWith(set);
                    set.Clear();
                    setCount = set.Count;
                }
            }
        }
        AsyncTouchedFileNamesSet asyncTouchedFileNamesSet = new AsyncTouchedFileNamesSet();
        private volatile int asyncErrorCount;

        private HashSet<string> pendingFileNameSet = new HashSet<string>();

        private IDictionaryWithCachedArrays<string, FileTracker> fileTrackerDictionary = new IDictionaryWithCachedArrays<string, FileTracker>();

        private class FileTracker
        {
            public string fileNameAndRelativePath;
            public string fileNameFromConfiguredPath;
            public string fullPath;
            public ulong fileLength;
            public QpcTimeStamp lastScanTime;
            public MDRF2FileInfo mdrf2FileInfo;
            public MDRF2FileDigestResult mdrf2FileDigestResult;
            public string faultCode;
            public bool touched, found;
        }

        private void ResetFSWAndMarkAllTrackersTouched()
        {
            if (fsw != null)
                fsw.EnableRaisingEvents = false;

            asyncTouchedFileNamesSet.Clear();
            asyncErrorCount = 0;

            fileTrackerDictionary.ValueArray.DoForEach(ft => ft.touched = true );

            ignoreFileNameAndRelativePathSet.Clear();
        }

        private HashSet<string> newFileNameAndRelativePathSet = new HashSet<string>();
        private HashSet<string> ignoreFileNameAndRelativePathSet = new HashSet<string>();

        private bool fullUpdateNeeded, rescanDirectoryNeeded;
        QpcTimer rescanIntervalTimer;

        private int Service(bool forceFullUpdate = false, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool updateMDRF2FileDigestResults = false)
        {
            int didCount = 0;

            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

            fullUpdateNeeded |= forceFullUpdate || (asyncErrorCount > 0);
            rescanDirectoryNeeded |= fullUpdateNeeded | rescanIntervalTimer.GetIsTriggered(qpcTimeStamp);

            if (fullUpdateNeeded)
            {
                ResetFSWAndMarkAllTrackersTouched();
                fullUpdateNeeded = false;
            }
            else if (asyncTouchedFileNamesSet.setCount > 0)
            {
                asyncTouchedFileNamesSet.TransferContentsTo(pendingFileNameSet);
            }

            if (pendingFileNameSet.Count > 0)
            {
                var pendingFileNameSetArray = pendingFileNameSet.ToArray();

                pendingFileNameSet.Clear();

                foreach (var fileNameAndRelativePath in pendingFileNameSetArray)
                {
                    bool isDirectory = System.IO.Directory.Exists(System.IO.Path.Combine(RootDirFullPath, fileNameAndRelativePath));
                    var isSupportedFileExt = MDRF2FileReadingHelper.IsSupportedFileExtension(fileNameAndRelativePath);

                    if (ignoreFileNameAndRelativePathSet.Contains(fileNameAndRelativePath))
                    { }
                    else if (isDirectory)
                    {
                        Log.Debug.Emit($"Ignoring directory '{fileNameAndRelativePath}'");
                        ignoreFileNameAndRelativePathSet.Add(fileNameAndRelativePath);
                    }
                    else if (fileTrackerDictionary.TryGetValue(fileNameAndRelativePath, out FileTracker ft) && ft != null)
                    {
                        ft.touched = true;
                    }
                    else if (isSupportedFileExt)
                    {
                        newFileNameAndRelativePathSet.Add(fileNameAndRelativePath);
                    }
                    else
                    {
                        Log.Debug.Emit($"Ignoring file '{fileNameAndRelativePath}': file extension is not supported here");
                        ignoreFileNameAndRelativePathSet.Add(fileNameAndRelativePath);
                    }
                }
            }

            if (rescanDirectoryNeeded)
            {
                fileTrackerDictionary.ValueArray.DoForEach(ft => ft.found = false);

                if (fsw != null && !fsw.EnableRaisingEvents)
                    fsw.EnableRaisingEvents = true;

                rescanDirectoryNeeded = false;

                foreach (var scanFileNameAndFullPath in System.IO.Directory.EnumerateFiles(RootDirFullPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var scanFileNameAndRelativePath = scanFileNameAndFullPath.MakeRelativePath(RootDirFullPath);

                    var isSupportedFileExt = MDRF2FileReadingHelper.IsSupportedFileExtension(scanFileNameAndRelativePath);
                    bool isDirectory = System.IO.Directory.Exists(scanFileNameAndFullPath);

                    if (ignoreFileNameAndRelativePathSet.Contains(scanFileNameAndRelativePath))
                    {
                        // note: we may have files in this list that also end with a supported extension so this test needs to come first.
                    }
                    else if (isDirectory)
                    {
                        Log.Debug.Emit($"Ignoring directory '{scanFileNameAndRelativePath}'");
                        ignoreFileNameAndRelativePathSet.Add(scanFileNameAndRelativePath);
                    }
                    else if (isSupportedFileExt)
                    {
                        if (fileTrackerDictionary.TryGetValue(scanFileNameAndRelativePath, out FileTracker ft) && ft != null)
                        {
                            ft.touched = true;
                            ft.found = true;
                        }
                        else
                        {
                            newFileNameAndRelativePathSet.Add(scanFileNameAndRelativePath);
                        }
                    }
                    else
                    {
                        Log.Debug.Emit($"Ignoring file '{scanFileNameAndRelativePath}': file extension is not supported here");
                        ignoreFileNameAndRelativePathSet.Add(scanFileNameAndRelativePath);
                    }
                }

                // mark all of the not found files as touched
                fileTrackerDictionary.ValueArray.Where(ft => !ft.found).DoForEach(ft => ft.touched = true);
            }

            if (newFileNameAndRelativePathSet.Count > 0)
            {
                foreach (var newFileNameAndRelativePath in newFileNameAndRelativePathSet)
                {
                    try
                    {
                        var fileNameFromConfiguredPath = System.IO.Path.Combine(Config.RootDirPath, newFileNameAndRelativePath);
                        var fullPath = System.IO.Path.Combine(RootDirFullPath, newFileNameAndRelativePath);

                        if (!System.IO.Directory.Exists(fullPath))
                        {
                            fileTrackerDictionary[newFileNameAndRelativePath] = new FileTracker()
                            {
                                fileNameAndRelativePath = newFileNameAndRelativePath,
                                fileNameFromConfiguredPath = fileNameFromConfiguredPath,
                                fullPath = fullPath,
                                touched = true,
                            };
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ignoreFileNameAndRelativePathSet.Add(newFileNameAndRelativePath);
                        Log.Debug.Emit($"Unexpected issue generating FileTracker for '{newFileNameAndRelativePath}': {ex.ToString(ExceptionFormat.TypeAndMessage)} [added to ignore list]");
                    }
                }

                newFileNameAndRelativePathSet.Clear();
            }

            if (fileTrackerDictionary.ValueArray.Any(ft => ft.touched))
            {
                foreach (var ft in fileTrackerDictionary.ValueArray.Where(ft => ft.touched))
                {
                    try
                    {
                        ft.touched = false;

                        var sysFileInfo = new FileInfo(ft.fullPath);

                        if (sysFileInfo.Exists)     // it might have been deleted/pruned since it was added here.
                        {
                            if (ft.fileLength != (ulong) sysFileInfo.Length)
                            {
                                bool wasUsable = ft.mdrf2FileInfo?.IsUsable ?? false;

                                if (!wasUsable)
                                {
                                    using (var fileReaderHelper = new MDRF2FileReadingHelper(new MDRF2FileInfo(ft.fileNameAndRelativePath, ft.fileNameFromConfiguredPath, ft.fullPath), Log, Config.MDRF1FileReaderBehavior, Config.MDRF2FileReaderBehavior))
                                    {
                                        // attempt to read the header.  This may fail or throw if the file does not have a complete set of header records written to it yet.
                                        fileReaderHelper.ReadHeadersIfNeeded();

                                        ft.fileLength = fileReaderHelper.FileInfo.FileLength;
                                        ft.lastScanTime = qpcTimeStamp;

                                        if (fileReaderHelper.HeadersRead)
                                        {
                                            ft.mdrf2FileInfo = fileReaderHelper.FileInfo.MakeCopyOfThis();

                                            ft.mdrf2FileDigestResult = new MDRF2FileDigestResult() { FileInfo = ft.mdrf2FileInfo };

                                            ft.faultCode = ft.mdrf2FileInfo.IsUsable ? "" : $"This is not a valid MDRF2 or MDRF file: {ft.mdrf2FileInfo.FaultCode}";
                                        }
                                        else
                                        {
                                            Log.Trace.Emit($"File '{ft.fileNameAndRelativePath}':{ft.fileLength} is not usable yet [{fileReaderHelper.HeaderReadFailedReason}]");
                                        }
                                    }
                                }
                                else
                                {
                                    ft.fileLength = ft.mdrf2FileInfo.FileLength = (ulong) sysFileInfo.Length;
                                    ft.lastScanTime = ft.mdrf2FileInfo.ScanTimeStamp = qpcTimeStamp;
                                }

                                didCount++;
                            }

                            if (updateMDRF2FileDigestResults || Config.UpdateMDRF2FileDigestResultsAutomatically)
                            {
                                ft.mdrf2FileDigestResult = queryFactory.GetMDRF2FileDigest(ft.mdrf2FileInfo);
                                ft.lastScanTime = qpcTimeStamp;

                                didCount++;
                            }
                        }
                        else
                        {
                            Log.Debug.Emit($"File '{ft.fileNameAndRelativePath}' has been removed");
                            ft.faultCode = null;

                            fileTrackerDictionary.Remove(ft.fileNameAndRelativePath);
                            didCount++;
                        }

                        if (ft.faultCode.IsNeitherNullNorEmpty())
                        {
                            Log.Debug.Emit($"Previous issue with file '{ft.fileNameAndRelativePath}' has been resolved [{ft.faultCode}]");
                            ft.faultCode = string.Empty;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ft.faultCode = $"Rescan failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}";
                        Log.Debug.Emit($"Issue with file '{ft.fileNameAndRelativePath}': {ft.faultCode}");
                    }
                }
            }

            if (updateMDRF2FileDigestResults && fileTrackerDictionary.ValueArray.Any(ft => ft.mdrf2FileDigestResult.FileSummary == null))
            {
                foreach (var ft in fileTrackerDictionary.ValueArray.Where(ft => ft.mdrf2FileDigestResult.FileSummary == null))
                {
                    ft.mdrf2FileDigestResult = queryFactory.GetMDRF2FileDigest(ft.mdrf2FileInfo);
                    ft.lastScanTime = qpcTimeStamp;
                    didCount++;
                }
            }


            if (didCount > 0)
            {
                var orderedTrackers = fileTrackerDictionary.ValueArray
                                .Where(ft => ft.faultCode.IsNullOrEmpty() && ft.mdrf2FileInfo != null && ft.mdrf2FileInfo.IsUsable)
                                .OrderBy(ft => ft.mdrf2FileInfo.DateTimeInfo.UTCDateTime);

                _MDRF2FileInfoSetPublisher.Object = new ReadOnlyIList<IMDRF2FileInfo>(orderedTrackers.Select(ft => ft.mdrf2FileInfo));

                if (updateMDRF2FileDigestResults || Config.UpdateMDRF2FileDigestResultsAutomatically)
                    _MDRF2FileDigestResultSetPublisher.Object = new ReadOnlyIList<MDRF2FileDigestResult>(orderedTrackers.Select(ft => ft.mdrf2FileDigestResult));
            }

            return didCount;
        }

        public IMDRF2FileInfo [] FilterFiles(MDRF2QuerySpec querySpec, bool autoSync = true)
        {
            if (autoSync)
                Sync().Run();

            var roFileInfoSet = MDRF2FileInfoSetPublisher.Object;

            roFileInfoSet.RangeSearch(querySpec, out int startIndex, out int count);

            var fileInfoArray = roFileInfoSet.SafeSubArray(startIndex, count);

            return fileInfoArray;
        }

        public virtual IEnumerable<IMDRF2QueryRecord> CreateQuery(MDRF2QuerySpec querySpec, params IMDRF2FileInfo[] fileInfoParamsArray)
        {
            return CreateQuery(querySpec, fileInfoParamsArray, out ApplyDynamicQueryAdjustItemsDelegate applyDynamicQueryAdjustItemsDelegate);
        }

        public virtual IEnumerable<IMDRF2QueryRecord> CreateQuery(MDRF2QuerySpec querySpec, IMDRF2FileInfo[] fileInfoArray, out ApplyDynamicQueryAdjustItemsDelegate applyDynamicQueryAdjustItemsDelegate, bool enableAutoSync = true)
        {
            // populate fileSetArray as needed - replace a null array with results of calling FilterFiles.
            if (fileInfoArray == null)
                fileInfoArray = FilterFiles(querySpec, autoSync: enableAutoSync);

            if (queryFactory == null)
                queryFactory = new MDRF2QueryFactory(QueryLogger);

            return queryFactory.CreateQuery(querySpec, fileInfoArray, out applyDynamicQueryAdjustItemsDelegate);
        }

        private MDRF2QueryFactory queryFactory = null;
    }

    public class MDRF2QueryFactory : IMDRF2QueryFactory
    {
        public MDRF2QueryFactory(Logging.IBasicLogger queryLogger)
        {
            QueryLogger = queryLogger;
        }

        public Logging.IBasicLogger QueryLogger { get; private set; }

        public virtual IEnumerable<IMDRF2QueryRecord> CreateQuery(MDRF2QuerySpec querySpec, params IMDRF2FileInfo[] fileInfoParamsArray)
        {
            return CreateQuery(querySpec, fileInfoParamsArray, out ApplyDynamicQueryAdjustItemsDelegate applyDynamicQueryAdjustItemsDelegate);
        }

        public virtual IEnumerable<IMDRF2QueryRecord> CreateQuery(MDRF2QuerySpec querySpec, IMDRF2FileInfo[] fileInfoArray, out ApplyDynamicQueryAdjustItemsDelegate applyDynamicQueryAdjustItemsDelegate, bool enableAutoSync = true)
        {
            // Create initial TaskSpec

            var queryTaskSpec = new QueryTaskSpec()
            {
                FileSetArray = null,        // todo: replace this with query 
                ClientQuerySpec = querySpec,
                Logger = QueryLogger,
                PointNamesArray = (querySpec.PointSetSpec?.Item1).MapNullToEmpty(),
                PointSetArrayType = querySpec.PointSetSpec?.Item2 ?? MDRF2PointSetArrayItemType.F8,
                PointSetIntervalInSec = (querySpec.PointSetSpec?.Item3 ?? TimeSpan.Zero).TotalSeconds,
            };

            queryTaskSpec.FileSetArray = fileInfoArray.MapNullToEmpty();

            if (queryTaskSpec.FileSetArray.Any(fileInfo => !fileInfo.IsUsable))
            {
                foreach (var index in Enumerable.Range(0, queryTaskSpec.FileSetArray.Length))
                {
                    queryTaskSpec.FileSetArray[index] = queryTaskSpec.FileSetArray[index].PopulateIfNeeded(querySpec);
                }
            }

            // Determine the expanded PointNamesArray value if needed.

            var readAllPoints = (querySpec.PointSetSpec == null || (querySpec.PointSetSpec.Item1 == null && querySpec.PointSetSpec.Item2 != MDRF2PointSetArrayItemType.None));
            if (readAllPoints)
            {
                var pointNamesHashSetSet = queryTaskSpec.FileSetArray.Select(fileInfo => new HashSet<string>((fileInfo?.SpecItemSet?.PointFullNameDictionary?.Keys).MapNullToEmpty()));

                // start with the largest set
                var setBuilder = pointNamesHashSetSet.OrderByDescending(item => item.Count).FirstOrDefault() ?? new HashSet<string>();

                foreach (var pointNamesSet in pointNamesHashSetSet)
                    setBuilder.UnionWith(pointNamesSet);

                queryTaskSpec.PointNamesArray = setBuilder.ToArray();
            }
            // else leave the QueryTaskSpec PointNamesArray set to the empty array.

            // Create set of TaskItemSpec instances

            queryTaskSpec.QueryTaskStepSpecArray = queryTaskSpec.FileSetArray.Select(fileInfo => new QueryTaskStepSpec(queryTaskSpec, fileInfo).Setup()).ToArray();

            // setup taskSpec (aka allocate desired point set array type)
            queryTaskSpec.Setup();

            // give the caller the delegate that can be used to make dynamic adjustments to the query.
            applyDynamicQueryAdjustItemsDelegate = queryTaskSpec.ApplyDynamicQueryAdjustItemsDelegate;

            // Call the task spec's GenerateRecords method that actually generates the enumerable query execution object.
            return queryTaskSpec.GenerateRecords();
        }

        private class QueryTaskSpec : IDisposable
        {
            public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
            public IMDRF2FileInfo[] FileSetArray { get; set; }
            public MDRF2QuerySpec ClientQuerySpec { get; set; }
            public Logging.IBasicLogger Logger { get; set; }

            public QueryTaskStepSpec[] QueryTaskStepSpecArray { get; set; }

            public string[] PointNamesArray { get; set; }
            public MDRF2PointSetArrayItemType PointSetArrayType { get; set; }
            public double PointSetIntervalInSec { get; set; }

            public ValueContainer[] pointVCArray;
            public double[] pointF8Array;
            public float[] pointF4Array;
            public Common.MDRF2DateTimeStampPair pointSetFirstQRDTPair;

            /// <summary>
            /// Setup this task spec instance.  Currently this allocates the internal pointYYArray for the selected point set array item type
            /// </summary>
            public QueryTaskSpec Setup()
            {
                switch (PointSetArrayType & MDRF2PointSetArrayItemType.TypeMask)
                {
                    case MDRF2PointSetArrayItemType.VC: pointVCArray = new ValueContainer[PointNamesArray.Length]; break;
                    case MDRF2PointSetArrayItemType.F8: pointF8Array = new double[PointNamesArray.Length]; break;
                    case MDRF2PointSetArrayItemType.F4: pointF4Array = new float[PointNamesArray.Length]; break;
                }

                return this;
            }

            public void ApplyDynamicQueryAdjustItemsDelegate(ref DynamicQueryAdjustItems dynamicQueryAdjustItemsToApply)
            {
                if (dynamicQueryAdjustItemsToApply.PointSetSampleInterval != null)
                    PointSetIntervalInSec = (dynamicQueryAdjustItemsToApply.PointSetSampleInterval ?? TimeSpan.Zero).TotalSeconds;
            }

            public void Dispose()
            {
                IsDisposed = true;
                QueryTaskStepSpecArray.DoForEach(item => item.DisposeOfGivenObject());
                QueryTaskStepSpecArray = null;
            }

            bool IsDisposed { get; set; }

            internal IEnumerable<IMDRF2QueryRecord> GenerateRecords()
            {
                try
                {
                    if (IsDisposed)
                        new System.ObjectDisposedException($"TaskSpec for '{ClientQuerySpec?.Name}' has been disposed.  Each IEnumerable from StartQuery can only be used once.").Throw();

                    {
                        if ((ClientQuerySpec.ItemTypeSelect & MDRF2QueryItemTypeSelect.ExtractionStart) != 0)
                            yield return new MDRF2QueryRecord<object>() { ItemType = MDRF2QueryItemTypeSelect.ExtractionStart };
                    }

                    IMDRF2FileInfo lastFileInfo = null;
                    IMDRF2QueryRecord lastEmittedRecord = null;

                    foreach (var queryTaskStepSpec in QueryTaskStepSpecArray)
                    {
                        lastFileInfo = queryTaskStepSpec.FileInfo;

                        foreach (var record in queryTaskStepSpec.GenerateFilteredRecords())
                        {
                            lastEmittedRecord = record;
                            yield return record;
                        }
                    }

                    {
                        if ((ClientQuerySpec.ItemTypeSelect & MDRF2QueryItemTypeSelect.ExtractionComplete) != 0)
                            yield return new MDRF2QueryRecord<object>() { ItemType = MDRF2QueryItemTypeSelect.ExtractionComplete, FileInfo = lastFileInfo, DTPair = lastEmittedRecord?.DTPair ?? default };
                    }
                }
                finally
                {
                    Dispose();
                }
            }
        }

        private class QueryTaskStepSpec : IDisposable
        {
            public QueryTaskSpec QueryTaskSpec { get; set; }
            public IMDRF2FileInfo FileInfo { get; set; }

            public MDRF2FileReadingHelper.Filter rhFilter;
            public MDRF2FileReadingHelper readerHelper;

            public QueryTaskStepSpec(QueryTaskSpec queryTaskSpec, IMDRF2FileInfo fileInfo)
            {
                QueryTaskSpec = queryTaskSpec;
                FileInfo = fileInfo;
            }

            public QueryTaskStepSpec Setup()
            {
                var clientQuerySpec = QueryTaskSpec.ClientQuerySpec;

                readerHelper = new MDRF2FileReadingHelper(FileInfo, QueryTaskSpec.Logger, clientQuerySpec.MDRF1FileReaderBehavior, clientQuerySpec.MDRF2FileReaderBehavior);

                readerHelper.ReadHeadersIfNeeded();

                var specItemSet = FileInfo.SpecItemSet ?? readerHelper.FileInfo.SpecItemSet;

                rhFilter = new MDRF2FileReadingHelper.Filter()
                {
                    StartUTCTimeSince1601 = clientQuerySpec.StartUTCTimeSince1601,
                    EndUTCTimeSince1601 = clientQuerySpec.EndUTCTimeSince1601,
                    ItemTypeSelect = clientQuerySpec.ItemTypeSelect,
                    OccurrenceDictionary = null,
                    GroupIDHashSet = null,
                    GroupDataRecordingDelegate = GroupDataRecordingDelegate,
                    NoteOtherRecordDelegate = NoteOtherRecordDelegate,
                    ObjectTypeNameSet = null,
                    CurrentSelectedPointSetIntervalInSecDelegate = ((QueryTaskSpec != null) ? (() => QueryTaskSpec.PointSetIntervalInSec) : ((Func<double>)null)),
                };

                rhFilter.OccurrenceDictionary = new Dictionary<int, IOccurrenceInfo>();

                if (clientQuerySpec.OccurrenceNameArray == null)
                    rhFilter.OccurrenceDictionary.SafeAddRange(specItemSet.OccurrenceDictionary.ValueArray.Select(ioi => KVP.Create(ioi.OccurrenceID, ioi)));
                else
                    rhFilter.OccurrenceDictionary.SafeAddRange(clientQuerySpec.OccurrenceNameArray.Select(name => specItemSet.OccurrenceDictionary.SafeTryGetValue(name)).WhereIsNotDefault().Select(ioi => KVP.Create(ioi.OccurrenceID, ioi)));

                rhFilter.ObjectTypeNameSet = (clientQuerySpec.ObjectTypeNameSet != null) ? new HashSet<string>(clientQuerySpec.ObjectTypeNameSet) : null;

                pointInfoArray = QueryTaskSpec.PointNamesArray.Select(pointName => specItemSet.PointFullNameDictionary.SafeTryGetValue(pointName) ?? specItemSet.PointAliasDictionary.SafeTryGetValue(pointName)).ToArray();
                var pointInfoToPositionDictionary = new Dictionary<IGroupPointInfo, int>().SafeAddRange(pointInfoArray.Select((igpi, index) => KVP.Create(igpi, index)).Where(kvp => kvp.Key != null));

                var groupInfoHashSet = new HashSet<IGroupInfo>(pointInfoArray.WhereIsNotDefault().Select(gpi => gpi?.GroupInfo));

                rhFilter.GroupIDHashSet = new HashSet<int>(groupInfoHashSet.Select(gi => gi?.GroupID ?? -1));

                pointDecoderArrayByGroupDictionary = new IDictionaryWithCachedArrays<int, GroupPointStateAndDecoder>();

                var mapBuilderList = new List<int>();
                foreach (var gi in groupInfoHashSet.Where(gi => gi != null))
                {
                    mapBuilderList.Clear();
                    mapBuilderList.AddRange(gi.GroupPointInfoArray.Select(gpi => pointInfoToPositionDictionary.SafeTryGetValue(gpi, fallbackValue: -1)));
                    while (mapBuilderList.LastOrDefault() == -1)
                        mapBuilderList.RemoveAt(mapBuilderList.Count - 1);

                    pointDecoderArrayByGroupDictionary[gi.GroupID] = new GroupPointStateAndDecoder() { GroupToPointIndexMap = mapBuilderList.ToArray() };
                }

                bool anyInlineIndexRowFlagBitMaskMissing = false;
                FileIndexRowFlagBits inlineIndexRowFlagBitMask = default;
                bool anyInlineIndexUserRowFlagBitMaskMissing = false;
                UInt64 inlineIndexUserRowFlagBitMask = 0;

                foreach (var oi in rhFilter.OccurrenceDictionary.Values)
                {
                    inlineIndexRowFlagBitMask |= oi.IsHighPriority ? (FileIndexRowFlagBits.ContainsHighPriorityOccurrence) : (FileIndexRowFlagBits.ContainsHighPriorityOccurrence | FileIndexRowFlagBits.ContainsOccurrence);
                    if (oi.FileIndexUserRowFlagBits != 0)
                        inlineIndexUserRowFlagBitMask |= oi.FileIndexUserRowFlagBits;
                    else
                        anyInlineIndexUserRowFlagBitMaskMissing = true;
                }

                foreach (var gi in groupInfoHashSet)
                {
                    anyInlineIndexRowFlagBitMaskMissing = true;
                    if (gi.FileIndexUserRowFlagBits != 0)
                        inlineIndexUserRowFlagBitMask |= gi.FileIndexUserRowFlagBits;
                    else
                        anyInlineIndexUserRowFlagBitMaskMissing = true;
                }

                if (rhFilter.ObjectTypeNameSet == null || rhFilter.ObjectTypeNameSet.Count > 0)
                    inlineIndexRowFlagBitMask |= FileIndexRowFlagBits.ContainsObject | FileIndexRowFlagBits.ContainsSignificantObject;

                rhFilter.ObjectSpecificUserRowFlagBitMask = QueryTaskSpec.ClientQuerySpec.ObjectSpecificUserRowFlagBits;

                if ((rhFilter.ItemTypeSelect & MDRF2QueryItemTypeSelect.Mesg) != 0)
                    inlineIndexRowFlagBitMask |= FileIndexRowFlagBits.ContainsMessage;

                if ((rhFilter.ItemTypeSelect & MDRF2QueryItemTypeSelect.Error) != 0)
                    inlineIndexRowFlagBitMask |= FileIndexRowFlagBits.ContainsError;

                rhFilter.InlineIndexRowFlagBitMask = anyInlineIndexRowFlagBitMaskMissing ? FileIndexRowFlagBits.None : inlineIndexRowFlagBitMask;
                rhFilter.InlineIndexUserRowFlagBitMask = anyInlineIndexUserRowFlagBitMaskMissing ? 0 : (inlineIndexUserRowFlagBitMask | rhFilter.ObjectSpecificUserRowFlagBitMask);

                rhFilter.AllowRecordReuse = QueryTaskSpec.ClientQuerySpec.AllowRecordReuse;

                return this;
            }

            /// <summary>
            /// Note: this array will have null values for point names that were requested but for which there is no matching point in this file.
            /// </summary>
            public IGroupPointInfo[] pointInfoArray;

            public bool HaveNewData { get; set; }
            public IDictionaryWithCachedArrays<int, GroupPointStateAndDecoder> pointDecoderArrayByGroupDictionary;

            public class GroupPointStateAndDecoder
            {
                public bool HasNewData { get; set; }
                public int[] GroupToPointIndexMap { get; set; }
            }

            /// <summary>
            /// Read the given group (or at least part of it) and accumulate the values to the mapped locations in the task spec's configured point value array.
            /// </summary>
            public IMDRF2QueryRecord GroupDataRecordingDelegate(MDRF2FileReadingHelper.Filter filter, int groupID, in Common.MDRF2DateTimeStampPair dtPair, ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IGroupInfo groupInfoFromMDRF1)
            {
                if (!HaveNewData)
                    QueryTaskSpec.pointSetFirstQRDTPair = dtPair;

                // return immediately if there are no mapped points for this group or we already have data for this group in this time interval.
                if (!pointDecoderArrayByGroupDictionary.TryGetValue(groupID, out GroupPointStateAndDecoder gpsd) || gpsd?.HasNewData != false)
                    return null;             // there are no mapped points for this group

                gpsd.HasNewData = true;
                HaveNewData = true;

                var map = gpsd.GroupToPointIndexMap;
                var mapLength = map.Length;

                if (groupInfoFromMDRF1 == null)
                {
                    // data comming from MessagePack array that contains the values of the Group's points.
                    var numValues = mpReader.ReadArrayHeader();

                    switch (QueryTaskSpec.PointSetArrayType & MDRF2PointSetArrayItemType.TypeMask)
                    {
                        case MDRF2PointSetArrayItemType.VC:
                            for (int valueIndex = 0; valueIndex < numValues && valueIndex < mapLength; valueIndex++)
                            {
                                var mappedIndex = map[valueIndex];
                                if (mappedIndex >= 0)
                                    QueryTaskSpec.pointVCArray[mappedIndex] = VCFormatter.Instance.Deserialize(ref mpReader, mpOptions);
                                else
                                    mpReader.Skip();
                            }
                            break;

                        case MDRF2PointSetArrayItemType.F8:
                            for (int valueIndex = 0; valueIndex < numValues && valueIndex < mapLength; valueIndex++)
                            {
                                var mappedIndex = map[valueIndex];
                                if (mappedIndex >= 0)
                                {
                                    var mpCode = (MPHeaderByteCode)mpReader.NextCode;

                                    switch (mpCode)
                                    {
                                        case MPHeaderByteCode.Float32: QueryTaskSpec.pointF8Array[mappedIndex] = mpReader.ReadSingle(); break;
                                        case MPHeaderByteCode.Float64: QueryTaskSpec.pointF8Array[mappedIndex] = mpReader.ReadDouble(); break;
                                        case MPHeaderByteCode.False: QueryTaskSpec.pointF8Array[mappedIndex] = 0.0; mpReader.Skip(); break;
                                        case MPHeaderByteCode.True: QueryTaskSpec.pointF8Array[mappedIndex] = 1.0; mpReader.Skip(); break;
                                        default:
                                            {
                                                var mpItemType = mpReader.NextMessagePackType;
                                                switch (mpItemType)
                                                {
                                                    case MessagePackType.Integer: QueryTaskSpec.pointF8Array[mappedIndex] = mpReader.ReadDouble(); break;
                                                    default: QueryTaskSpec.pointF8Array[mappedIndex] = double.NaN; mpReader.Skip(); break;
                                                }
                                            }
                                            break;
                                    }
                                }
                                else
                                {
                                    mpReader.Skip();
                                }
                            }
                            break;

                        case MDRF2PointSetArrayItemType.F4:
                            for (int valueIndex = 0; valueIndex < numValues && valueIndex < mapLength; valueIndex++)
                            {
                                var mappedIndex = map[valueIndex];
                                if (mappedIndex >= 0)
                                {
                                    var mpCode = (MPHeaderByteCode)mpReader.NextCode;

                                    switch (mpCode)
                                    {
                                        case MPHeaderByteCode.Float32: QueryTaskSpec.pointF4Array[mappedIndex] = mpReader.ReadSingle(); break;
                                        case MPHeaderByteCode.Float64: QueryTaskSpec.pointF4Array[mappedIndex] = (float)mpReader.ReadDouble(); break;
                                        case MPHeaderByteCode.False: QueryTaskSpec.pointF4Array[mappedIndex] = 0.0f; mpReader.Skip(); break;
                                        case MPHeaderByteCode.True: QueryTaskSpec.pointF4Array[mappedIndex] = 1.0f; mpReader.Skip(); break;
                                        default:
                                            {
                                                var mpItemType = mpReader.NextMessagePackType;
                                                switch (mpItemType)
                                                {
                                                    case MessagePackType.Integer: QueryTaskSpec.pointF4Array[mappedIndex] = mpReader.ReadSingle(); break;
                                                    default: QueryTaskSpec.pointF4Array[mappedIndex] = float.NaN; mpReader.Skip(); break;
                                                }
                                            }
                                            break;
                                    }
                                }
                                else
                                {
                                    mpReader.Skip();
                                }
                            }
                            break;

                        default:
                            break;
                    }
                }
                else
                {
                    // data coming from mdrf1
                    var gpiArray = groupInfoFromMDRF1.GroupPointInfoArray;
                    var numValues = gpiArray.Length;

                    switch (QueryTaskSpec.PointSetArrayType & MDRF2PointSetArrayItemType.TypeMask)
                    {
                        case MDRF2PointSetArrayItemType.VC:
                            for (int valueIndex = 0; valueIndex < numValues && valueIndex < mapLength; valueIndex++)
                            {
                                var mappedIndex = map[valueIndex];
                                if (mappedIndex >= 0)
                                    QueryTaskSpec.pointVCArray[mappedIndex] = gpiArray[valueIndex].VC;
                            }
                            break;

                        case MDRF2PointSetArrayItemType.F8:
                            for (int valueIndex = 0; valueIndex < numValues && valueIndex < mapLength; valueIndex++)
                            {
                                var mappedIndex = map[valueIndex];
                                if (mappedIndex >= 0)
                                {
                                    var vc = gpiArray[valueIndex].VC;
                                    var cst = vc.cvt;
                                    if (cst.IsFloatingPoint() || cst.IsInteger() || cst.IsBoolean())
                                        QueryTaskSpec.pointF8Array[mappedIndex] = vc.GetValueF8(rethrow: false);
                                }
                            }
                            break;

                        case MDRF2PointSetArrayItemType.F4:
                            for (int valueIndex = 0; valueIndex < numValues && valueIndex < mapLength; valueIndex++)
                            {
                                var mappedIndex = map[valueIndex];
                                if (mappedIndex >= 0)
                                {
                                    var vc = gpiArray[valueIndex].VC;
                                    var cst = vc.cvt;
                                    if (cst.IsFloatingPoint() || cst.IsInteger() || cst.IsBoolean())
                                        QueryTaskSpec.pointF4Array[mappedIndex] = vc.GetValueF4(rethrow: false);
                                }
                            }
                            break;

                        default:
                            break;
                    }
                }

                return null;
            }

            MDRF2QueryRecord<ValueContainer[]> vcArrayRecord;
            MDRF2QueryRecord<double[]> f8ArrayRecord;
            MDRF2QueryRecord<float[]> f4ArrayRecord;

            public IMDRF2QueryRecord NoteOtherRecordDelegate(MDRF2FileReadingHelper.Filter filter, MDRF2QueryItemTypeSelect recordType, in Common.MDRF2DateTimeStampPair dtPair, object data)
            {
                const MDRF2QueryItemTypeSelect canEmitPointSetRecordOnItemTypes = (MDRF2QueryItemTypeSelect.InlineIndexRecord | MDRF2QueryItemTypeSelect.BlockEnd | MDRF2QueryItemTypeSelect.FileDeltaTimeUpdate | MDRF2QueryItemTypeSelect.FileEnd | MDRF2QueryItemTypeSelect.DecodingIssue | MDRF2QueryItemTypeSelect.ExtractionStopped | MDRF2QueryItemTypeSelect.ExtractionCancelled);
                IMDRF2QueryRecord result = null;

                if (recordType == MDRF2QueryItemTypeSelect.FileStart)
                {
                    switch (QueryTaskSpec.PointSetArrayType & MDRF2PointSetArrayItemType.TypeMask)
                    {
                        case MDRF2PointSetArrayItemType.VC: QueryTaskSpec.pointVCArray.SetAll(ValueContainer.Empty); break;
                        case MDRF2PointSetArrayItemType.F4: QueryTaskSpec.pointF4Array.SetAll(float.NaN); break;
                        case MDRF2PointSetArrayItemType.F8: QueryTaskSpec.pointF8Array.SetAll(double.NaN); break;
                        default: break;
                    }
                }

                // Note: we ignore synthetic InlineIndexRecord and BlockEnd records from MDRF1 (RowStart, RowEnd) since they do not occur on group set boundaries.
                if (HaveNewData && ((recordType & canEmitPointSetRecordOnItemTypes) != 0) && ((recordType & MDRF2QueryItemTypeSelect.MDRF1) == 0))
                {
                    // generate a point set record
                    var reuseArrays = (QueryTaskSpec.PointSetArrayType & MDRF2PointSetArrayItemType.ReuseArrays) != 0;
                    var allowRecordReuse = filter.AllowRecordReuse;

                    switch (QueryTaskSpec.PointSetArrayType & MDRF2PointSetArrayItemType.TypeMask)
                    {
                        case MDRF2PointSetArrayItemType.VC:
                            if (!allowRecordReuse || vcArrayRecord == null)
                                vcArrayRecord = new MDRF2QueryRecord<ValueContainer[]>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.PointSet };
                            result = vcArrayRecord.Update(QueryTaskSpec.pointSetFirstQRDTPair, reuseArrays ? QueryTaskSpec.pointVCArray : QueryTaskSpec.pointVCArray.SafeToArray());
                            break;
                        case MDRF2PointSetArrayItemType.F8:
                            if (!allowRecordReuse || f8ArrayRecord == null)
                                f8ArrayRecord = new MDRF2QueryRecord<double[]>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.PointSet };
                            result = f8ArrayRecord.Update(QueryTaskSpec.pointSetFirstQRDTPair, reuseArrays ? QueryTaskSpec.pointF8Array : QueryTaskSpec.pointF8Array.SafeToArray());
                            break;
                        case MDRF2PointSetArrayItemType.F4:
                            if (!allowRecordReuse || f4ArrayRecord == null)
                                f4ArrayRecord = new MDRF2QueryRecord<float[]>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.PointSet };
                            result = f4ArrayRecord.Update(QueryTaskSpec.pointSetFirstQRDTPair, reuseArrays ? QueryTaskSpec.pointF4Array : QueryTaskSpec.pointF4Array.SafeToArray());
                            break;
                        default:
                            break;
                    }

                    pointDecoderArrayByGroupDictionary.ValueArray.DoForEach(gpsd => gpsd.HasNewData = false);

                    HaveNewData = false;
                    QueryTaskSpec.pointSetFirstQRDTPair = default;

                    // advance the SkipGroupsUntilAfterFDT based on the configured point set interval
                    var pointSetIntervalInSec = QueryTaskSpec.PointSetIntervalInSec;
                    if (pointSetIntervalInSec != 0.0)
                    {
                        var nextSkipGroupsUntilAfterFDT = filter.SkipGroupsUntilAfterFDT + pointSetIntervalInSec;

                        if (nextSkipGroupsUntilAfterFDT + pointSetIntervalInSec < dtPair.FileDeltaTime)
                            nextSkipGroupsUntilAfterFDT = dtPair.FileDeltaTime - pointSetIntervalInSec;

                        filter.SkipGroupsUntilAfterFDT = nextSkipGroupsUntilAfterFDT;
                    }
                }

                if (keepRecentEmittedPointSetRecordCount > 0 && result != null)
                {
                    if (recentEmittedPointSetRecordList == null)
                        recentEmittedPointSetRecordList = new List<IMDRF2QueryRecord>();
                    else if (recentEmittedPointSetRecordList.Count >= keepRecentEmittedPointSetRecordCount)
                        recentEmittedPointSetRecordList.RemoveAt(0);

                    recentEmittedPointSetRecordList.Add(result);

                    lastEmittedPointSetRecord = result;
                }

                return result;
            }

            private const int keepRecentEmittedPointSetRecordCount = 0;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "This variable is only used to support internal debugging only when keepRecentEmittedPointSetRecordCount is non-zero")]
            private List<IMDRF2QueryRecord> recentEmittedPointSetRecordList;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "This variable is only used to support internal debugging only when keepRecentEmittedPointSetRecordCount is non-zero")]
            private IMDRF2QueryRecord lastEmittedPointSetRecord;

            public void Dispose()
            {
                Fcns.DisposeOfObject(ref readerHelper);
            }

            internal IEnumerable<IMDRF2QueryRecord> GenerateFilteredRecords()
            {
                return readerHelper.GenerateFilteredRecords(rhFilter, QueryTaskSpec.CancellationToken);
            }
        }
    }

    #endregion

    #region MDRF2FileReadingHelper

    /// <summary>
    /// bitfield enumeration used to define various MDRF2FileReadingHelper behavior settings
    /// <para/>None (0x00),  TreatEndOfStreamExceptionAsEndOfFile (0x01),  DecompressionEngineExceptionIsEndOfFile (0x02)
    /// </summary>
    [Flags]
    public enum MDRF2FileReaderBehavior : int
    {
        /// <summary>Placeholder default value [0x00]</summary>
        None = 0x00,

        /// <summary>
        /// When selected, end of stream exceptions are handled as if the normal end of file record was processed and the underlying stream exception is not passed to the query client during enumeration.  
        /// This allows files to be processed without error while they are being recorded.  
        /// [0x01]
        /// </summary>
        TreatEndOfStreamExceptionAsEndOfFile = 0x01,

        /// <summary>
        /// When selected, decompression engine related exceptions are handled as if the normal end of file record was processed and the underlying exception is not passed to the query client during enumeration.  
        /// This allows files to be processed without error while they are being recorded.  
        /// [0x02]
        /// </summary>
        TreatDecompressionEngineExceptionAsEndOfFile = 0x02,
    }


    public class MDRF2FileReadingHelper : IDisposable
    {
        /// <summary>Returns true if the given fileName has a supported mdrf1 or mdrf2 file extension (.mdrf, .mdrf1, .mdrf2, .mdrf2.lz4, .mdrf2.gz)</summary>
        public static bool IsSupportedFileExtension(string fileName)
        {
            return IsSupportedMDRF2FileExtension(fileName) || IsSupportedMDRF1FileExtension(fileName);
        }

        /// <summary>Returns true if the given fileName has a supported mdrf2 file extension (.mdrf2, .mdrf2.lz4, .mdrf2.gz)</summary>
        public static bool IsSupportedMDRF2FileExtension(string fileName)
        {
            return fileName.EndsWith(Common.Constants.mdrf2LZ4FileExtension, StringComparison.InvariantCultureIgnoreCase)
                    || fileName.EndsWith(Common.Constants.mdrf2GZipFileExtension, StringComparison.InvariantCultureIgnoreCase)
                    || fileName.EndsWith(Common.Constants.mdrf2BaseFileExtension, StringComparison.InvariantCultureIgnoreCase)
                    ;
        }

        /// <summary>
        /// Returns true if the given fileName has a supported mdrf1 file extension (.mdrf, .mdrf1)
        /// If <paramref name="includeCompressedVersions"/> is true then this also includes .gz and .lz4 versions of these names
        /// </summary>
        public static bool IsSupportedMDRF1FileExtension(string fileName, bool includeCompressedVersions = true)
        {
            return fileName.EndsWith(Common.Constants.mdrf1FileExtension, StringComparison.InvariantCultureIgnoreCase)
                    || fileName.EndsWith(Common.Constants.mdrf1AltFileExtension, StringComparison.InvariantCultureIgnoreCase)
                    || (includeCompressedVersions &&
                        (fileName.EndsWith(Common.Constants.mdrf1LZ4FileExtension, StringComparison.InvariantCultureIgnoreCase)
                        || fileName.EndsWith(Common.Constants.mdrf1GZipFileExtension, StringComparison.InvariantCultureIgnoreCase)
                        || fileName.EndsWith(Common.Constants.mdrf1AltLZ4FileExtension, StringComparison.InvariantCultureIgnoreCase)
                        || fileName.EndsWith(Common.Constants.mdrf1AltGZipFileExtension, StringComparison.InvariantCultureIgnoreCase)
                        )
                       )
                    ;
        }

        public class Filter
        {
            /// <summary>Conversion getter/setter for use of StartUTCTimeSince1601 as a DateTime</summary>
            public DateTime StartDateTime { get { return StartUTCTimeSince1601.GetDateTimeFromUTCTimeSince1601(); } set { StartUTCTimeSince1601 = value.GetUTCTimeSince1601(); } }

            /// <summary>Conversion getter/setter for use of EndUTCTimeSince1601 as a DateTime</summary>
            public DateTime EndDateTime { get { return EndUTCTimeSince1601.GetDateTimeFromUTCTimeSince1601(); } set { EndUTCTimeSince1601 = value.GetUTCTimeSince1601(); } }

            /// <summary>Gives the first UTCTimeSince1601 for which query records are desired.  The query engine may return records recorded before this time in the regions where it filters which files and file contents to process but will not generally process objects, occurrences or group data that was recorded before this time.  Defaults to NegativeInfinity.</summary>
            public double StartUTCTimeSince1601 { get; set; } = double.NegativeInfinity;

            /// <summary>Gives the last UTCTimeSince1601 for which query records are desired.  The query engine may return records recorded after this time in the regions where it filters which files and file contents to process but will not generally process objects, occurrences or group data that was recorded after this time.  Defaults to NegativeInfinity.</summary>
            public double EndUTCTimeSince1601 { get; set; } = double.PositiveInfinity;

            /// <summary>This value may be used to assist in implementing sub-sampling of group data.  When this value is non-zero it prevents processing of records that contain group data for FileDeltaTime values that come before this this value.  It is expected that the Group data handling delegate methods referenced here will dynamically change this value.</summary>
            public double SkipGroupsUntilAfterFDT { get; set; }

            /// <summary>Used by the client to (nominally) specify the record types that the client is interested in.</summary>
            public MDRF2QueryItemTypeSelect ItemTypeSelect { get; set; } = MDRF2QueryItemTypeSelect.None;

            /// <summary>Used by the client to specify which file index row flag bit values they would like processed.  InlineIndex record blocks that do not contain indicated record types will be skipped.  When this is set to None (0) it selects that no filtering will be performed using this value.</summary>
            public FileIndexRowFlagBits InlineIndexRowFlagBitMask { get; set; } = FileIndexRowFlagBits.None;

            /// <summary>Used by the client to specify which user row flag bit values they would like processed.  InlineIndex record blocks that do not contain records associated with the given mask will be skipped.  When this is set to 0 it selects that no related filtering will be performed using this value.</summary>
            public UInt64 InlineIndexUserRowFlagBitMask { get; set; } = 0;

            /// <summary>Defines the set of object types that should be extracted and for which query records should be yielded.  When null is used it indicates that all objects shall be extracted and yielded.</summary>
            public HashSet<string> ObjectTypeNameSet { get; set; }

            /// <summary>When non-zero, this property defines the set of object specific user row flag bits that this filter should perform callbacks for.  If this is non-zero then objects records that were not recorded with a non-zero value will not be decoded or reported.</summary>
            public ulong ObjectSpecificUserRowFlagBitMask { get; set; }

            /// <summary>
            /// Defines the mapping of Occurrence ID values to their corresponding IOccurrenceInfo objects.  All occurence types that the client would like records generated for and yielded must be included in this dictionary and must include a non-null IOccurrenceInfo instance.
            /// If this property is null or empty then no Occurrence records will be generated or yielded.
            /// </summary>
            public Dictionary<int, IOccurrenceInfo> OccurrenceDictionary { get; set; }

            /// <summary>
            /// When non-null this set defines the set of GroupIDs that the client would like to have processed.  When this record is null then normal group record processing will be performed for all groups.
            /// </summary>
            public HashSet<int> GroupIDHashSet { get; set; }

            /// <summary>When non-null, this property gives the delegate that will be called to process group record contents for MDRF2 and MDRF1 group records as they are encountered while extracting the current file, subject to the other filter criteria encoded here.</summary>
            public GroupDataHandlerDelegate GroupDataRecordingDelegate { get; set; }

            /// <summary>When non-null, this property gives the delegate that will be called to be made aware of during MDRF2 record processing as needed to be able to generate other query record types to be yielded, typically based on the end of a record block or the set of records that were generated during a single FDT period.</summary>
            public NoteOtherRecordDelegate NoteOtherRecordDelegate { get; set; }

            /// <summary>This method returns true if the filter's GroupIDHashSet is null or if it contains the given <paramref name="groupID"/></summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ProcessGroup(int groupID)
            {
                return (GroupIDHashSet == null || GroupIDHashSet.Contains(groupID));
            }

            /// <summary>
            /// When this value is false (the default) the query execution engine will generate new record instances for all records that are yeilded by the enumeration.  
            /// This allows the client to directly accumulate and retain the individual record instances without additional work.  
            /// When this value is passed as true it allows the query execution engine to re-use record instances in the enumeration output.  
            /// This is only intended to support clients that immediately haravest the desired information from emitted records (or explicitly clone them) and which thus do not retain any of the enumeration's directly record instances
            /// The use of this option is intended to decrease the load on the GC engine for clients that do not directly retain the emitted record instances.
            /// Please note that for some low frequency record types, new record instances are always generated without regard to the setting of this flag. 
            /// This flag is often used in conjunction with the MDRF2PointSetArrayItemType.ResuseArrays flag.
            /// [defaults to false]
            /// </summary>
            public bool AllowRecordReuse { get; set; }

            /// <summary>
            /// When non-null, this delegate is used to obtain the current PointSetIntervalInSec from the QueryTaskSpec (or any other source) that will be used when starting to filter a new MDRF1 file or in any other context where no other means are available for this purpose.
            /// </summary>
            public Func<double> CurrentSelectedPointSetIntervalInSecDelegate { get; set; }

            /// <summary>
            /// When true, end of stream exceptions are handled as if the normal end of file record was processed and the underlying stream exception is not passed to the query client during enumeration.  
            /// This allows files to be processed without error while they are being recorded.  
            /// [Defaults to true]
            /// </summary>
            [Obsolete("This property is no longer in use (2021-04-06)")]
            public bool EndOfStreamExceptionIsEndOfFile { get; set; } = true;

            /// <summary>
            /// When true, decompression engine related exceptions are handled as if the normal end of file record was processed and the underlying exception is not passed to the query client during enumeration.  
            /// This allows files to be processed without error while they are being recorded.  
            /// [Defaults to true]
            /// </summary>
            [Obsolete("This property is no longer in use (2021-04-06)")]
            public bool DecompressionEngineExceptionIsEndOfFile { get; set; } = true;

            /// <summary>
            /// Returns true if the given <paramref name="dtPair"/> falls within the time region specified here.  The optional <paramref name="checkStartTime"/> and <paramref name="checkEndTime"/> parameters are used to control checking against either end, or both ends, of the contained time range.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsInTimeRange(in Common.MDRF2DateTimeStampPair dtPair, bool checkStartTime = true, bool checkEndTime = true)
            {
                var utcTimeSince1601 = dtPair.UTCTimeSince1601;

                if (checkStartTime && utcTimeSince1601 < StartUTCTimeSince1601)
                    return false;
                if (checkEndTime && utcTimeSince1601 > EndUTCTimeSince1601)
                    return false;

                return true;
            }
        }

        /// <summary>
        /// This delegate is called each time a filtered group ID is found.  The mpReader will be pointing at the start of the MP array that contains the record set the group's point's values.
        /// </summary>
        public delegate IMDRF2QueryRecord GroupDataHandlerDelegate(Filter filter, int groupID, in Common.MDRF2DateTimeStampPair dtPair, ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions, IGroupInfo groupInfoFromMDRF1);
        public delegate IMDRF2QueryRecord NoteOtherRecordDelegate(Filter filter, MDRF2QueryItemTypeSelect recordType, in Common.MDRF2DateTimeStampPair dtPair, object data = null);

        public MDRF2FileReadingHelper(string fileNameAndPath, Logging.IBasicLogger logger, MDRF.Reader.MDRFFileReaderBehavior mdrf1FileReaderBehavior = MDRF.Reader.MDRFFileReaderBehavior.AttemptToDecodeOccurrenceBodyAsNVS, MDRF2FileReaderBehavior mdrf2FileReaderBehavior = MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile | MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile)
               : this(new MDRF2FileInfo(fileNameAndPath), logger, mdrf1FileReaderBehavior, mdrf2FileReaderBehavior)
        { }

        public MDRF2FileReadingHelper(IMDRF2FileInfo fileInfoIn, Logging.IBasicLogger logger, MDRF.Reader.MDRFFileReaderBehavior mdrf1FileReaderBehavior = MDRF.Reader.MDRFFileReaderBehavior.AttemptToDecodeOccurrenceBodyAsNVS, MDRF2FileReaderBehavior mdrf2FileReaderBehavior = MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile | MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile)
        {
            Logger = logger;
            fileInfo = fileInfoIn.MakeCopyOfThis();

            this.mdrf1FileReaderBehavior = mdrf1FileReaderBehavior;
            this.mdrf2FileReaderBehavior = mdrf2FileReaderBehavior;

            var fileName = this.fileInfo.FileNameAndRelativePath;
            var relativePath = this.fileInfo.FileNameFromConfiguredPath;
            var fullPath = this.fileInfo.FullPath;

            bool isSupportedMDRF2Extension = IsSupportedMDRF2FileExtension(fileName);
            bool isSupportedMDRF1Extension = IsSupportedMDRF1FileExtension(fileName);

            mpFileRecordReaderSettings = new MessagePackFileRecordReaderSettings()
            {
                TreatEndOfStreamExceptionAsEndOfFile = (mdrf2FileReaderBehavior & MDRF2FileReaderBehavior.TreatEndOfStreamExceptionAsEndOfFile) != 0,
                TreatExpectedDecompressionErrorsAsEndOfFile = (mdrf2FileReaderBehavior & MDRF2FileReaderBehavior.TreatDecompressionEngineExceptionAsEndOfFile) != 0,
            };

            if (isSupportedMDRF1Extension)
            {
                if (IsSupportedMDRF1FileExtension(fileName, false))
                    mdrf1Reader = new MDRF.Reader.MDRFFileReader(fullPath, mdrfFileReaderBehavior: mdrf1FileReaderBehavior);
                else
                    mdrf1Reader = new MDRF.Reader.MDRFFileReader("", mdrfFileReaderBehavior: mdrf1FileReaderBehavior, externallyProvidedFileStream: fullPath.CreateDecompressor());

                fileInfo.FileLength = (ulong)mdrf1Reader.FileLength;

                fileInfo.LibraryInfo = mdrf1Reader.LibraryInfo;

                fileInfo.DateTimeInfo = mdrf1Reader.DateTimeInfo;
                fileInfo.SetupInfo = mdrf1Reader.SetupInfo;
                fileInfo.SetupInfoNVS = mdrf1Reader.LibraryInfo.NVS;
                fileInfo.ClientInfoNVS = mdrf1Reader.SetupInfo.ClientNVS;
                fileInfo.NonSpecMetaNVS = new NamedValueSet()
                {
                    "MDRF1",
                    { "MainNVS", mdrf1Reader.LibraryInfo.NVS },
                    { "ClientNVS", mdrf1Reader.SetupInfo.ClientNVS },
                    { "DateTimeNVS", mdrf1Reader.DateTimeInfo.NVS },
                }.MakeReadOnly();

                fileInfo.SpecItemNVSSet = new ReadOnlyIList<INamedValueSet>(mdrf1Reader.MetaDataArray);
                fileInfo.SpecItemSet = fileInfo.SpecItemNVSSet.CreateSpecItemSet(fileInfo.ClientInfoNVS, rethrow: true);

                fileInfo.FaultCode = mdrf1Reader.ResultCode;
                fileInfo.ScanTimeStamp = QpcTimeStamp.Now;

                HeadersRead = true;
            }
            else if (isSupportedMDRF2Extension)
            {
                mpFileRecordReader = new MessagePackFileRecordReader().Open(fullPath, mpFileRecordReaderSettings);
                var fileLength = (ulong)mpFileRecordReader.Counters.FileLength;

                if (fileInfo.FileLength != fileLength)
                    fileInfo.FileLength = fileLength;
            }
            else
            {
                new System.NotSupportedException($"File extension for '{relativePath}' is not recognized and/or supported").Throw();
            }

            FileInfo = fileInfo.MakeCopyOfThis();
        }

        Logging.IBasicLogger Logger { get; set; }

        private MDRF2FileInfo fileInfo { get; set; }
        public IMDRF2FileInfo FileInfo { get; set; }
        private NamedValueSet InlineMapUnionNVS = new NamedValueSet();
        private NamedValueSet NonSpecMetaNVS = new NamedValueSet();

        private static readonly MessagePackSerializerOptions mpOptions = Instances.VCDefaultMPOptions;

        public void Dispose()
        {
            try
            {
                Fcns.DisposeOfObject(ref mpFileRecordReader);
                Fcns.DisposeOfObject(ref mdrf1Reader);
            }
            catch (System.Exception ex)
            {
                Logger.Error.Emit("Dispose '{0}' triggered exception: {1}", fileInfo.FileNameFromConfiguredPath, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }
            finally
            {
                mpFileRecordReader = null;
                mdrf1Reader = null;
            }
        }

        MDRF2FileReaderBehavior mdrf2FileReaderBehavior;
        MessagePackFileRecordReaderSettings mpFileRecordReaderSettings;
        MessagePackFileRecordReader mpFileRecordReader;

        MDRF.Reader.MDRFFileReaderBehavior mdrf1FileReaderBehavior;
        MDRF.Reader.MDRFFileReader mdrf1Reader;

        public bool HeadersRead { get; set; }
        public string FaultCode { get { return _FaultCode.MapNullToEmpty(); } private set { _FaultCode = value; } }
        private string _FaultCode = null;

        /// <summary>
        /// Whenever the header read fails to successfully read the headers but no FaultCode is latched/noted, this property will describe the reason.
        /// </summary>
        public string HeaderReadFailedReason { get; set; }

        public void ReadHeadersIfNeeded(Filter filter = null)
        {
            try
            {
                if (!HeadersRead)
                {
                    if (mdrf1Reader == null)
                    {
                        MessagePackReader mpReader = default;

                        bool firstBlockNeeded = fileInfo.DateTimeInfo == null;
                        bool secondBlockNeeded = firstBlockNeeded || (fileInfo.LibraryInfo == null || fileInfo.SetupInfo == null || fileInfo.SpecItemSet == null);

                        mpFileRecordReader.SetupMPReaderForNextRecord(ref mpReader);
                        ReadNextInlineIndexRecord(ref mpReader, mpOptions);

                        if (firstBlockNeeded)
                        {
                            // advance past inline index
                            mpFileRecordReader.AdvancePastCurrentRecord();

                            // read and process first inline map - contains DateTimeInfoNVS
                            var setupSuccess = mpFileRecordReader.SetupMPReaderForNextRecord(ref mpReader);

                            ReadAndProcessInlineMap(ref mpReader, mpOptions);

                            fileInfo.ScanTimeStamp = QpcTimeStamp.Now;

                            mpFileRecordReader.AdvancePastCurrentRecord();
                        }
                        else
                        {
                            mpFileRecordReader.Advance(mpFileRecordReader.NextRecordLength + CurrentInlineIndexRecord.BlockByteCount);
                        }

                        if (secondBlockNeeded && filter != null && CurrentInlineIndexRecord.FirstDTPair.UTCTimeSince1601 > filter.EndUTCTimeSince1601)
                            secondBlockNeeded = false;

                        mpFileRecordReader.SetupMPReaderForNextRecord(ref mpReader);
                        ReadNextInlineIndexRecord(ref mpReader, mpOptions);

                        if (secondBlockNeeded)
                        {
                            // advance past inline index
                            mpFileRecordReader.AdvancePastCurrentRecord();

                            // read and process second inline map - contains the rest of the expected file meta data (LibraryInfo, SetupInfo, ClientNVS, SpecItemSet)
                            var setupSuccess = mpFileRecordReader.SetupMPReaderForNextRecord(ref mpReader);

                            ReadAndProcessInlineMap(ref mpReader, mpOptions);

                            fileInfo.ScanTimeStamp = QpcTimeStamp.Now;

                            mpFileRecordReader.AdvancePastCurrentRecord();
                        }
                        else
                        {
                            mpFileRecordReader.Advance(mpFileRecordReader.NextRecordLength + CurrentInlineIndexRecord.BlockByteCount);
                        }
                    }

                    FileInfo = fileInfo.MakeCopyOfThis();

                    HeadersRead = true;
                    HeaderReadFailedReason = string.Empty;
                }
            }
            catch (System.Exception ex)
            {
                // NOTE: the mpReader can throw this exception if it runs off the end of the currently buffered data.
                if (ex is System.IO.EndOfStreamException && mpFileRecordReaderSettings.TreatEndOfStreamExceptionAsEndOfFile)
                {
                    FaultCode = "";
                    HeaderReadFailedReason = $"Headers are not complete [{ex.ToString(ExceptionFormat.TypeAndMessage)}]";
                }
                else
                {
                    FaultCode = $"{Fcns.CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}";
                }

                fileInfo.FaultCode = fileInfo.FaultCode.MapNullOrEmptyTo(FaultCode);

                FileInfo = fileInfo.MakeCopyOfThis();
            }
        }

        public IEnumerable<IMDRF2QueryRecord> GenerateFilteredRecords(Filter filter, CancellationToken cancellationToken)
        {
            if (mdrf1Reader == null)
                return InnerGenerateFilteredRecords_MDRF2(filter, cancellationToken);
            else
            {
                bool processIndexOnly = ((filter.ItemTypeSelect & ~(MDRF2QueryItemTypeSelect.InlineIndexRecord | MDRF2QueryItemTypeSelect.FileEnd | MDRF2QueryItemTypeSelect.FileStart)) == 0)
                                      && (filter.InlineIndexUserRowFlagBitMask == 0)
                                      && (filter.InlineIndexRowFlagBitMask == 0)
                                      && (filter.GroupIDHashSet != null && filter.GroupIDHashSet.IsEmpty())
                                      && (filter.ObjectTypeNameSet != null && filter.ObjectTypeNameSet.IsEmpty())
                                      && (filter.OccurrenceDictionary != null && filter.OccurrenceDictionary.IsEmpty())
                                      ;

                if (processIndexOnly)
                    return InnerGenerateFilteredIndexRecords_MDRF1(filter, cancellationToken);
                else
                    return InnerGenerateFilteredRecords_MDRF1(filter, cancellationToken);
            }
        }

        #region MDRF2: InnerGenerateFilteredRecords_MDRF2, InnerGenerateFilteredRecords_Increment_MDRF2, ReadNextInlineIndexRecord, ReadAndProcessInlineMap

        struct InterationState
        {
            public bool EndRecordFound { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
            public bool InBlock { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
            public int Count { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
            public IMDRF2QueryRecord Record1 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
            public IMDRF2QueryRecord Record2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
            public IMDRF2QueryRecord Record3 { get; set; }
            public IMDRF2QueryRecord Record4 { get; set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ClearRecords()
            {
                Count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddRecordIfNotNull(IMDRF2QueryRecord record)
            {
                if (record != null)
                    AddRecord(record);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddRecord(IMDRF2QueryRecord record)
            {
                if (Count == 0)
                {
                    Record1 = record;
                    Count++;
                }
                else if (Count == 1)
                {
                    Record2 = record;
                    Count++;
                }
                else if (Count == 2)
                {
                    Record3 = record;
                    Count++;
                }
                else if (Count == 3)
                {
                    Record4 = record;
                    Count++;
                }
            }
        }

        private IEnumerable<IMDRF2QueryRecord> InnerGenerateFilteredRecords_MDRF2(Filter filter, CancellationToken cancellationToken)
        {
            MDRF2QueryFileSummary fileSummary = new MDRF2QueryFileSummary()
            {
                FileInfo = FileInfo,
                FaultCode = FaultCode,
                FileLength = FileInfo.FileLength,
                FirstFileDTPair = CurrentInlineIndexRecord?.FirstDTPair ?? default,
                EncounteredCountByGroupIDArray = new ulong[FileInfo.SpecItemSet?.GroupDictionary?.ValueArray?.Select(gi => gi.ClientID).ConcatItems(0).Max() ?? 0],
                EncounteredCountByOccurrenceIDArray = new ulong[FileInfo.SpecItemSet?.OccurrenceDictionary?.ValueArray?.Select(oi => oi.ClientID).ConcatItems(0).Max() ?? 0],
            };

            if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.FileStart) != 0)
                yield return new MDRF2QueryRecord<MDRF2QueryFileSummary>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.FileStart, DTPair = CurrentQRDTPair, Data = fileSummary.MakeCopyOfThis() };

            {
                var record = filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.FileStart, CurrentQRDTPair, fileSummary);
                if (record != null)
                    yield return record;
            }

            InterationState state = default;

            while (!state.EndRecordFound)
            {
                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();

                InnerGenerateFilteredRecords_Increment_MDRF2(filter, ref state, fileSummary);

                switch (state.Count)
                {
                    case 0:
                        break;
                    case 1:
                        yield return state.Record1;
                        break;
                    case 2:
                        yield return state.Record1;
                        yield return state.Record2;
                        break;
                    case 3:
                        yield return state.Record1;
                        yield return state.Record2;
                        yield return state.Record3;
                        break;
                    case 4:
                        yield return state.Record1;
                        yield return state.Record2;
                        yield return state.Record3;
                        yield return state.Record4;
                        break;
                }
            }

            {
                var counters = mpFileRecordReader.Counters;
                fileSummary.FaultCode = FaultCode;
                fileSummary.BytesProcessed = counters.TotalBytesProcessed;
                fileSummary.LastFileDTPair = CurrentQRDTPair;
                fileSummary.EndRecordFound = state.EndRecordFound;

                {
                    var record = filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.FileEnd, CurrentQRDTPair, fileSummary);
                    if (record != null)
                        yield return record;
                }

                if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.FileEnd) != 0)
                    yield return new MDRF2QueryRecord<MDRF2QueryFileSummary>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.FileEnd, DTPair = CurrentQRDTPair, Data = fileSummary };
            }

            if (_FaultCode == null)
                FaultCode = state.EndRecordFound ? string.Empty : "End record was not found";
        }

        private void InnerGenerateFilteredRecords_Increment_MDRF2(Filter filter, ref InterationState state, MDRF2QueryFileSummary fileSummary)
        {
            if (state.Count > 0)
                state.ClearRecords();

            try
            {
                MessagePackReader mpReader = default;

                if (!mpFileRecordReader.SetupMPReaderForNextRecord(ref mpReader))
                    new System.IO.EndOfStreamException(FaultCode = "Unabled to load next MessagePack record").Throw();

                fileSummary.RecordCount += 1;

                var mpHeaderByteCode = mpFileRecordReader.NextRecordHeaderByteCode;

                switch (mpHeaderByteCode)
                {
                    case MPHeaderByteCode.FixArray1:    // L1 is always used for inline index records: [L1 [L8 ...]]
                        {
                            ReadNextInlineIndexRecord(ref mpReader, mpOptions);
                            fileSummary.InlineIndexCount += 1;
                            if (fileSummary.FirstFileDTPair.IsEmpty)
                                fileSummary.FirstFileDTPair = CurrentInlineIndexRecord.FirstDTPair;

                            state.AddRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.InlineIndexRecord, in _CurrentQRDTPair, CurrentInlineIndexRecord));

                            bool outOfTimeRange = (CurrentInlineIndexRecord.LastDTPair.UTCTimeSince1601 < filter.StartUTCTimeSince1601) || (CurrentInlineIndexRecord.FirstDTPair.UTCTimeSince1601 > filter.EndUTCTimeSince1601);
                            bool inTimeRange = !outOfTimeRange;

                            var blockFileIndexRowFlagsBits = CurrentInlineIndexRecord.BlockFileIndexRowFlagBits;
                            var filteredBlockFileIndexRowFlagBits = blockFileIndexRowFlagsBits & filter.InlineIndexRowFlagBitMask;

                            bool fileIndexBitsMatch = ((filteredBlockFileIndexRowFlagBits != 0) || (filter.InlineIndexRowFlagBitMask == 0));
                            bool userRowFlagBitsMatch = (((CurrentInlineIndexRecord.BlockUserRowFlagBits & filter.InlineIndexUserRowFlagBitMask) != 0) || (filter.InlineIndexUserRowFlagBitMask == 0));

                            bool blockMayHaveGroupsToProcess = (CurrentInlineIndexRecord.LastDTPair.FileDeltaTime >= filter.SkipGroupsUntilAfterFDT) && (filter.GroupIDHashSet == null || filter.GroupIDHashSet.Count > 0);
                            bool blockMayHaveOccurrencesToProcess = ((blockFileIndexRowFlagsBits & (FileIndexRowFlagBits.ContainsOccurrence | FileIndexRowFlagBits.ContainsHighPriorityOccurrence)) != 0) && (filter.OccurrenceDictionary == null || filter.OccurrenceDictionary.Count > 0);
                            bool blockMayHaveObjectsToProcess = ((blockFileIndexRowFlagsBits & (FileIndexRowFlagBits.ContainsObject | FileIndexRowFlagBits.ContainsSignificantObject)) != 0) && (filter.ObjectTypeNameSet == null || filter.ObjectTypeNameSet.Count > 0);
                            bool blockMayHaveMesgsOrErrorsToProcess = ((filteredBlockFileIndexRowFlagBits & (FileIndexRowFlagBits.ContainsMessage | FileIndexRowFlagBits.ContainsError)) != 0);

                            bool blockHasHeaderOrMetaDataOrEnd = ((blockFileIndexRowFlagsBits & (FileIndexRowFlagBits.ContainsHeaderOrMetaData | FileIndexRowFlagBits.ContainsEnd)) != 0);

                            bool processBlock = (inTimeRange && (fileIndexBitsMatch | userRowFlagBitsMatch) && (blockMayHaveGroupsToProcess || blockMayHaveOccurrencesToProcess || blockMayHaveObjectsToProcess || blockMayHaveMesgsOrErrorsToProcess))
                                                || blockHasHeaderOrMetaDataOrEnd
                                                ;

                            if (processBlock)
                            {
                                fileSummary.NoteProcessingBlockFor(CurrentInlineIndexRecord);

                                mpFileRecordReader.AdvancePastCurrentRecord();
                                mpFileRecordReader.AttemptToReadMoreBytes(CurrentInlineIndexRecord.BlockByteCount); // read in the rest of the block's bytes all at once.

                                state.InBlock = (RemainingRecordsInThisBlock > 0);

                                if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.InlineIndexRecord) != 0)
                                {
                                    if (!filter.AllowRecordReuse || inlineIndexQueryRecord == null)
                                        inlineIndexQueryRecord = new MDRF2QueryRecord<Common.InlineIndexRecord>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.InlineIndexRecord };

                                    state.AddRecord(inlineIndexQueryRecord.Update(in _CurrentQRDTPair, CurrentInlineIndexRecord.MakeCopyOfThis()));
                                }
                            }
                            else
                            {
                                fileSummary.NoteSkippingBlockFor(CurrentInlineIndexRecord);

                                mpFileRecordReader.Advance(mpFileRecordReader.NextRecordLength + CurrentInlineIndexRecord.BlockByteCount);
                                state.InBlock = false;

                                if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.InlineIndexRecord) != 0)
                                {
                                    if (!filter.AllowRecordReuse || inlineIndexWillSkipQueryRecord == null)
                                        inlineIndexWillSkipQueryRecord = new MDRF2QueryRecord<Common.InlineIndexRecord>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.InlineIndexRecord | MDRF2QueryItemTypeSelect.WillSkip };

                                    state.AddRecord(inlineIndexWillSkipQueryRecord.Update(in _CurrentQRDTPair, CurrentInlineIndexRecord.MakeCopyOfThis()));
                                }
                            }
                        }
                        return;

                    case MPHeaderByteCode.FixMap1:      // ["MDRF2.End":nil]
                        {
                            mpReader.ReadMapHeader();   // this will return 1
                            var keyword = mpReader.ReadString();
                            mpReader.ReadNil();

                            mpFileRecordReader.AdvancePastCurrentRecord();

                            if (keyword == Common.Constants.FileEndKeyword)
                            {
                                if (!mpFileRecordReader.EndReached)
                                    mpFileRecordReader.AttemptToPopulateNextRecord();

                                state.EndRecordFound = mpFileRecordReader.EndReached;

                                fileInfo.NonSpecMetaNVS = NonSpecMetaNVS.SetKeyword(keyword).ConvertToReadOnly();
                                FileInfo = fileInfo.MakeCopyOfThis();
                            }

                            if (state.EndRecordFound)
                            {
                                if (state.InBlock)
                                    state.AddRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.BlockEnd, in _CurrentQRDTPair, CurrentInlineIndexRecord));

                                return;
                            }

                            throw new Common.MDRF2ReaderException(FaultCode = $"Unexpected inline {mpHeaderByteCode} content in block [{CurrentInlineIndexRecord}]");
                        }

                    case MPHeaderByteCode.Float64:      // FileDeltaTime update
                        {
                            fileSummary.FDTUpdateCount += 1;

                            CurrentInlineIndexRecord.UpdateDTPair(ref _CurrentQRDTPair, mpReader.ReadDouble());

                            mpFileRecordReader.AdvancePastCurrentRecord();

                            RemainingRecordsInThisBlock -= 1;

                            state.AddRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.FileDeltaTimeUpdate, in _CurrentQRDTPair));

                            if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.FileDeltaTimeUpdate) != 0 && filter.IsInTimeRange(CurrentQRDTPair))
                            {
                                if (!filter.AllowRecordReuse || fileDeltaTimeUpdateQueryRecord == null)
                                    fileDeltaTimeUpdateQueryRecord = new MDRF2QueryRecord<Common.MDRF2DateTimeStampPair>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.FileDeltaTimeUpdate };

                                state.AddRecord(fileDeltaTimeUpdateQueryRecord.Update(CurrentQRDTPair, CurrentQRDTPair));
                            }
                        }
                        break;

                    case MPHeaderByteCode.FixArray2:    // Group or Occurrence: [L2 GroupID|OccurrenceID {data}] where {data} is [Ln pointValues ...] or occurrenceVC
                        {
                            if (filter.IsInTimeRange(CurrentQRDTPair) && (CurrentQRDTPair.FileDeltaTime > filter.SkipGroupsUntilAfterFDT || filter.SkipGroupsUntilAfterFDT == 0.0))
                            {
                                mpReader.ReadArrayHeader();

                                int id = mpReader.ReadInt32();

                                if (id > 0)
                                {
                                    int idIdx = id - 1;
                                    if (fileSummary.EncounteredCountByGroupIDArray.IsSafeIndex(idIdx))
                                        fileSummary.EncounteredCountByGroupIDArray[idIdx] += 1;

                                    if (filter.ProcessGroup(id))
                                    {
                                        fileSummary.ProcessedGroupCount += 1;

                                        state.AddRecordIfNotNull(filter.GroupDataRecordingDelegate?.Invoke(filter, id, in _CurrentQRDTPair, ref mpReader, mpOptions, null));  // emission of record on a per group basis depends on the form of the callback handler.  The normal MDRF2DirectoryTracker query execution logic does not emit point set records here.
                                    }
                                }
                                else if (id < 0)
                                {
                                    id = -id;
                                    int idIdx = id - 1;
                                    if (fileSummary.EncounteredCountByOccurrenceIDArray.IsSafeIndex(idIdx))
                                        fileSummary.EncounteredCountByOccurrenceIDArray[idIdx] += 1;

                                    if (filter.OccurrenceDictionary.TryGetValue(id, out IOccurrenceInfo occurrenceInfo) && occurrenceInfo != null)
                                    {
                                        fileSummary.ProcessedOccurrenceCount += 1;

                                        if (!filter.AllowRecordReuse || occurrenceQueryRecord == null)
                                            occurrenceQueryRecord = new MDRF2QueryRecord<MDRF2OccurrenceQueryRecordData>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.Occurrence };

                                        state.AddRecord(occurrenceQueryRecord.Update(CurrentQRDTPair, new MDRF2OccurrenceQueryRecordData(occurrenceInfo, mpReader.DeserializeVC(mpOptions))));
                                    }
                                }
                            }

                            mpFileRecordReader.AdvancePastCurrentRecord();
                            RemainingRecordsInThisBlock -= 1;
                        }
                        break;

                    case MPHeaderByteCode.FixArray3:    // Object or Mesg or Error : new [L3 recordUserRowFlagBits objKnownTypeName objKnownTypeData], old [L3 objKnownTypeName objKnownTypeData nil]
                        {
                            if (filter.IsInTimeRange(CurrentQRDTPair))
                            {
                                mpReader.ReadArrayHeader();

                                ulong recordUserRowFlagBits = 0;

                                if (mpReader.NextMessagePackType == MessagePackType.Integer)
                                    recordUserRowFlagBits = mpReader.ReadUInt64();

                                bool enableEmitObject = ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.Object) != 0);

                                bool enableEmitThisObject = enableEmitObject && (((recordUserRowFlagBits & filter.ObjectSpecificUserRowFlagBitMask) != 0) || (filter.ObjectSpecificUserRowFlagBitMask == 0));

                                var objTypeHashSet = filter.ObjectTypeNameSet;
                                var acceptAllObjectTypes = (objTypeHashSet == null);

                                string objKnownTypeName = mpReader.ReadString();

                                switch (objKnownTypeName)
                                {
                                    case Common.Constants.ObjKnownType_Mesg:    // this is a pseudo object type - it has its own ItemTypeSelect value but is serialized as an object
                                        if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.Mesg) != 0)
                                        {
                                            fileSummary.ProcessedObjectCount += 1;

                                            var mesgT = Common.MesgAndErrorBodyFormatter.Instance.Deserialize(ref mpReader, mpOptions);
                                            state.AddRecord(new MDRF2QueryRecord<string>() { FileInfo = FileInfo, DTPair = mesgT.Item2, ItemType = MDRF2QueryItemTypeSelect.Mesg, Data = mesgT.Item1, UserRowFlagBits = recordUserRowFlagBits });
                                        }
                                        else
                                        {
                                            fileSummary.SkippedRecordCount += 1;
                                        }
                                        break;

                                    case Common.Constants.ObjKnownType_Error:    // this is a pseudo object type - it has its own ItemTypeSelect value but is serialized as an object
                                        if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.Error) != 0)
                                        {
                                            fileSummary.ProcessedObjectCount += 1;

                                            var mesgT = Common.MesgAndErrorBodyFormatter.Instance.Deserialize(ref mpReader, mpOptions);
                                            state.AddRecord(new MDRF2QueryRecord<string>() { FileInfo = FileInfo, DTPair = mesgT.Item2, ItemType = MDRF2QueryItemTypeSelect.Error, Data = mesgT.Item1, UserRowFlagBits = recordUserRowFlagBits });
                                        }
                                        else
                                        {
                                            fileSummary.SkippedRecordCount += 1;
                                        }
                                        break;

                                    case Common.Constants.ObjKnownType_LogMessage:
                                        if (enableEmitThisObject && (acceptAllObjectTypes || objTypeHashSet.Contains(objKnownTypeName) || objTypeHashSet.Contains(typeString_ILogMessage) || objTypeHashSet.Contains(typeString_LogMessage)))
                                        {
                                            fileSummary.ProcessedObjectCount += 1;

                                            if (!filter.AllowRecordReuse || logMessageObjectQueryRecord == null)
                                                logMessageObjectQueryRecord = new MDRF2QueryRecord<Logging.ILogMessage>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.Object };

                                            state.AddRecord(logMessageObjectQueryRecord.Update(CurrentQRDTPair, LogMessageFormatter.Instance.Deserialize(ref mpReader, mpOptions), recordUserRowFlagBits));
                                        }
                                        else
                                        {
                                            fileSummary.SkippedRecordCount += 1;
                                            fileSummary.SkippedUserRowFlagBits |= recordUserRowFlagBits;
                                        }

                                        break;

                                    case Common.Constants.ObjKnownType_E039Object:
                                        if (enableEmitThisObject && (acceptAllObjectTypes || objTypeHashSet.Contains(objKnownTypeName) || objTypeHashSet.Contains(typeString_IE039Object) || objTypeHashSet.Contains(typeString_E039Object)))
                                        {
                                            fileSummary.ProcessedObjectCount += 1;

                                            if (!filter.AllowRecordReuse || e039ObjectQueryRecord == null)
                                                e039ObjectQueryRecord = new MDRF2QueryRecord<MosaicLib.Semi.E039.IE039Object>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.Object };

                                            state.AddRecord(e039ObjectQueryRecord.Update(CurrentQRDTPair, E039ObjectFormatter.Instance.Deserialize(ref mpReader, mpOptions), recordUserRowFlagBits));
                                        }
                                        else
                                        {
                                            fileSummary.SkippedRecordCount += 1;
                                            fileSummary.SkippedUserRowFlagBits |= recordUserRowFlagBits;
                                        }

                                        break;

                                    case Common.Constants.ObjKnownType_ValueContainer:
                                        if (enableEmitThisObject && (acceptAllObjectTypes || objTypeHashSet.Contains(objKnownTypeName) || objTypeHashSet.Contains("ValueContainer") || objTypeHashSet.Contains(typeString_ValueContainer)))
                                        {
                                            fileSummary.ProcessedObjectCount += 1;

                                            if (!filter.AllowRecordReuse || vcObjectQueryRecord == null)
                                                vcObjectQueryRecord = new MDRF2QueryRecord<ValueContainer>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.Object };

                                            state.AddRecord(vcObjectQueryRecord.Update(CurrentQRDTPair, VCFormatter.Instance.Deserialize(ref mpReader, mpOptions), recordUserRowFlagBits));
                                        }
                                        else
                                        {
                                            fileSummary.SkippedRecordCount += 1;
                                            fileSummary.SkippedUserRowFlagBits |= recordUserRowFlagBits;
                                        }

                                        break;

                                    case Common.Constants.ObjKnownType_TypeAndValueCarrier:
                                        if (enableEmitThisObject)
                                        {
                                            fileSummary.ProcessedObjectCount += 1;

                                            var tavc = TypeAndValueCarrierFormatter.Instance.Deserialize(ref mpReader, mpOptions);
                                            if (tavc != null && (acceptAllObjectTypes || objTypeHashSet.Contains(objKnownTypeName) || objTypeHashSet.Contains(tavc.TypeStr)))
                                            {
                                                try
                                                {
                                                    var typeSerializer = MosaicLib.Modular.Common.CustomSerialization.CustomSerialization.Instance.GetCustomTypeSerializerItemFor(tavc);
                                                    var obj = typeSerializer.Deserialize(tavc);

                                                    if (!filter.AllowRecordReuse || objectQueryRecord == null)
                                                        objectQueryRecord = new MDRF2QueryRecord<object>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.Object };

                                                    state.AddRecord(objectQueryRecord.Update(CurrentQRDTPair, obj, recordUserRowFlagBits));
                                                }
                                                catch (System.Exception ex)
                                                {
                                                    GenerateAndLogDecodingIssue(filter, $"Unable to deserialize object [{tavc}]: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            fileSummary.SkippedRecordCount += 1;
                                            fileSummary.SkippedUserRowFlagBits |= recordUserRowFlagBits;
                                        }

                                        break;

                                    default:
                                        // unrecognized object type
                                        state.AddRecord(GenerateAndLogDecodingIssue(filter, $"Encountered unexpected object known type name '{objKnownTypeName}' in block [{CurrentInlineIndexRecord}]"));

                                        break;
                                }
                            }

                            mpFileRecordReader.AdvancePastCurrentRecord();
                            RemainingRecordsInThisBlock -= 1;
                        }
                        break;

                    default:
                        state.AddRecord(GenerateAndLogDecodingIssue(filter, $"Encountered unexpected top level MessagePack record type '{mpHeaderByteCode}' ({mpReader.NextMessagePackType}) in block [{CurrentInlineIndexRecord}]"));
                        break;
                }

                if (RemainingRecordsInThisBlock == 0 && state.InBlock)
                {
                    state.AddRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.BlockEnd, in _CurrentQRDTPair, CurrentInlineIndexRecord));

                    if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.BlockEnd) != 0)
                        state.AddRecord(new MDRF2QueryRecord<Common.InlineIndexRecord>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.BlockEnd, Data = CurrentInlineIndexRecord });
                }
            }
            catch (System.Exception ex)
            {
                // NOTE: the mpReader can throw this exception if it runs off the end of the currently buffered data.
                if (ex is System.IO.EndOfStreamException && mpFileRecordReaderSettings.TreatEndOfStreamExceptionAsEndOfFile)
                {
                    Logger.Debug.Emit($"Handling EOSE as normal end of file [{ex.ToString(ExceptionFormat.TypeAndMessage)}]");
                    state.EndRecordFound = true;
                }
                else
                {
                    throw;
                }
            }
        }

        private MDRF2QueryRecord<Common.InlineIndexRecord> inlineIndexQueryRecord, inlineIndexWillSkipQueryRecord;
        private MDRF2QueryRecord<Common.MDRF2DateTimeStampPair> fileDeltaTimeUpdateQueryRecord;
        private MDRF2QueryRecord<MDRF2OccurrenceQueryRecordData> occurrenceQueryRecord;
        private MDRF2QueryRecord<Logging.ILogMessage> logMessageObjectQueryRecord;
        private MDRF2QueryRecord<MosaicLib.Semi.E039.IE039Object> e039ObjectQueryRecord;
        private MDRF2QueryRecord<ValueContainer> vcObjectQueryRecord;
        private MDRF2QueryRecord<object> objectQueryRecord;

        private static readonly string typeString_LogMessage = typeof(MosaicLib.Logging.LogMessage).Name;
        private static readonly string typeString_ILogMessage = typeof(MosaicLib.Logging.ILogMessage).Name;
        private static readonly string typeString_E039Object = typeof(MosaicLib.Semi.E039.E039Object).Name;
        private static readonly string typeString_IE039Object = typeof(MosaicLib.Semi.E039.IE039Object).Name;
        private static readonly string typeString_ValueContainer = typeof(MosaicLib.Modular.Common.ValueContainer).Name;

        public Common.InlineIndexRecord CurrentInlineIndexRecord { get; set; }
        public uint RemainingRecordsInThisBlock { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Common.InlineIndexRecord ReadNextInlineIndexRecord(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            CurrentInlineIndexRecord = Common.InlineIndexFormatter.Instance.Deserialize(ref mpReader, options).SetupForReaderUse();
            CurrentInlineIndexRecord.UpdateDTPair(ref _CurrentQRDTPair, CurrentInlineIndexRecord.BlockFirstFileDeltaTime);

            RemainingRecordsInThisBlock = CurrentInlineIndexRecord.BlockRecordCount;

            return CurrentInlineIndexRecord;
        }

        private void ReadAndProcessInlineMap(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            RemainingRecordsInThisBlock -= 1;

            int numKeys = mpReader.ReadMapHeader();

            for (int keyIndex = 0; keyIndex < numKeys; keyIndex++)
            {
                var keyName = mpReader.ReadString();
                var keyVC = VCFormatter.Instance.Deserialize(ref mpReader, options);

                InlineMapUnionNVS.SetValue(keyName, keyVC);

                bool isSpecItemsNVS = false;

                switch (keyName)
                {
                    case Common.Constants.FileStartKeyword:
                        break;

                    case Common.Constants.FileEndKeyword:
                        throw new Common.MDRF2ReaderException($"Found invalid InlineMap keyword '{keyName}':{keyVC}");

                    case Common.Constants.DateTimeNVSKeyName:
                        fileInfo.DateTimeInfo = new DateTimeInfo().UpdateFrom(keyVC.GetValueNVS(rethrow: true).BuildDictionary());
                        break;

                    case Common.Constants.LibNVSKeyName:
                        fileInfo.LibraryInfo = new LibraryInfo().UpdateFrom(keyVC.GetValueNVS(rethrow: true).BuildDictionary());
                        break;

                    case Common.Constants.SetupNVSKeyName:
                        fileInfo.SetupInfoNVS = keyVC.GetValueNVS(rethrow: true).BuildDictionary();
                        fileInfo.SetupInfo = new SetupInfo().UpdateFrom(fileInfo.SetupInfoNVS);
                        break;

                    case Common.Constants.ClientNVSKeyName:
                        fileInfo.ClientInfoNVS = keyVC.GetValueNVS(rethrow: true).BuildDictionary();
                        fileInfo.SetupInfo.ClientNVS = fileInfo.ClientInfoNVS;
                        break;

                    case Common.Constants.SpecItemsNVSListKey:
                        fileInfo.SpecItemNVSSet = keyVC.GetValueL(rethrow: true).Select(vc => vc.GetValueNVS(rethrow: true)).ConvertToReadOnly();
                        fileInfo.SpecItemSet = fileInfo.SpecItemNVSSet.CreateSpecItemSet(fileInfo.ClientInfoNVS, rethrow: true);
                        isSpecItemsNVS = true;
                        break;

                    case Common.Constants.FileInstanceUUIDKeyName:
                    case Common.Constants.HostNameKeyName:
                    case Common.Constants.CurrentProcessKeyName:
                    case Common.Constants.EnvironmentKeyName:
                        break;

                    default:
                        Logger.Debug.Emit($"Found unexpected InlineMap keyword '{keyName}':{keyVC}");
                        break;
                }

                if (!isSpecItemsNVS)
                {
                    if (NonSpecMetaNVS.IsEmpty())
                        NonSpecMetaNVS.SetKeyword("MDRF2");

                    NonSpecMetaNVS.SetValue(keyName, keyVC);
                }
            }

            {
                fileInfo.NonSpecMetaNVS = NonSpecMetaNVS.ConvertToReadOnly();

                fileInfo.ScanTimeStamp = QpcTimeStamp.Now;
                FileInfo = fileInfo.MakeCopyOfThis();
            }
        }

        #endregion

        #region MDRF1: InnerGenerateFilteredRecords_MDRF1, MDRF1_ReadAndProcessTask

        private IEnumerable<IMDRF2QueryRecord> InnerGenerateFilteredRecords_MDRF1(Filter filter, CancellationToken cancellationToken)
        {
            mdrf1_filter = filter;
            mdrf1_cancellationToken = cancellationToken;

            mdrf1_RecordBuffer = new BlockingCollection<IMDRF2QueryRecord>(1000);

            var taskFactory = new System.Threading.Tasks.TaskFactory();
            var task = taskFactory.StartNew(() => MDRF1_ReadAndProcessTask());

            try
            {
                foreach (var record in mdrf1_RecordBuffer.GetConsumingEnumerable())
                    yield return record;

                yield break;
            }
            finally
            {
                task.Wait();

                Fcns.DisposeOfObject(ref task);
                Fcns.DisposeOfObject(ref mdrf1_RecordBuffer);
            }
        }

        private IEnumerable<IMDRF2QueryRecord> InnerGenerateFilteredIndexRecords_MDRF1(Filter filter, CancellationToken cancellationToken)
        {
            mdrf1_filter = filter;
            mdrf1_cancellationToken = cancellationToken;

            bool includeInlineIndexRecords = ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.InlineIndexRecord) != 0);

            var fileSummary = mdrf1_fileSummary = new MDRF2QueryFileSummary()
            {
                FileInfo = FileInfo,
                FaultCode = FaultCode,
                FileLength = FileInfo.FileLength,
                FirstFileDTPair = new Common.MDRF2DateTimeStampPair() { FileDeltaTime = 0.0, UTCTimeSince1601 = mdrf1Reader.DateTimeInfo.UTCTimeSince1601 },
                EncounteredCountByGroupIDArray = EmptyArrayFactory<ulong>.Instance,
                EncounteredCountByOccurrenceIDArray = EmptyArrayFactory<ulong>.Instance,
            };

            if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.FileStart) != 0)
                yield return (new MDRF2QueryRecord<MDRF2QueryFileSummary>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.FileStart, DTPair = CurrentQRDTPair, Data = mdrf1_fileSummary.MakeCopyOfThis() });

            var fileIndexInfo = mdrf1Reader.FileIndexInfo;

            foreach (var row in fileIndexInfo.FileIndexRowArray.Where(row => !row.IsEmpty))
            {
                fileSummary.RecordCount += 1;
                fileSummary.InlineIndexCount += 1;

                // emit an inline index for the this
                var inlineIndexRecord = mdrf1_currentInlineIndexRecord = new Common.InlineIndexRecord(row);
                inlineIndexRecord.BlockByteCount = (uint)mdrf1Reader.FileIndexInfo.GetRowLength(row, mdrf1Reader.FileLength);

                if (CurrentQRDTPair.FileDeltaTime != row.FirstBlockDeltaTimeStamp || CurrentQRDTPair.UTCTimeSince1601 != row.FirstBlockUtcTimeSince1601)
                {
                    _CurrentQRDTPair.FileDeltaTime = row.FirstBlockDeltaTimeStamp;
                    _CurrentQRDTPair.UTCTimeSince1601 = row.FirstBlockUtcTimeSince1601;

                    fileSummary.LastFileDTPair = _CurrentQRDTPair;
                }

                bool currentTimeIsInRange = CurrentQRDTPair.UTCTimeSince1601.IsInRange(filter.StartUTCTimeSince1601, filter.EndUTCTimeSince1601);

                if (includeInlineIndexRecords)
                    yield return (new MDRF2QueryRecord<Common.InlineIndexRecord>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.InlineIndexRecord, Data = inlineIndexRecord });

                fileSummary.BytesProcessed += inlineIndexRecord.BlockByteCount;
                mdrf1_LastFileIndexRow = row;
            }

            fileSummary.EndRecordFound = fileIndexInfo.FileWasProperlyClosed;

            if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.FileEnd) != 0)
                yield return (new MDRF2QueryRecord<MDRF2QueryFileSummary>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.FileEnd, Data = mdrf1_fileSummary });
        }

        private Filter mdrf1_filter;
        private CancellationToken mdrf1_cancellationToken;
        private System.Collections.Concurrent.BlockingCollection<IMDRF2QueryRecord> mdrf1_RecordBuffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void MDRF1_PostRecord(IMDRF2QueryRecord record)
        {
            mdrf1_RecordBuffer.Add(mdrf1_LastRecordPosted = record);
        }

        IMDRF2QueryRecord mdrf1_LastRecordPosted;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void MDRF1_PostRecordIfNotNull(IMDRF2QueryRecord record)
        {
            if (record != null)
                MDRF1_PostRecord(record);
        }

        private void MDRF1_ReadAndProcessTask()
        {
            Filter filter = mdrf1_filter;
            CancellationToken cancellationToken = mdrf1_cancellationToken;

            // convert this to use a task to query the records and put them in a buffer block and then a yield return loop to pull them from the block and return them to the client.
            try
            {
                var pceMask = (MDRF.Reader.ProcessContentEvent.None
                                | MDRF.Reader.ProcessContentEvent.ReadingStart // - the corresponding record item types are generated explicitly.
                                | MDRF.Reader.ProcessContentEvent.ReadingEnd
                                | MDRF.Reader.ProcessContentEvent.RowStart
                                | MDRF.Reader.ProcessContentEvent.RowEnd
                                | MDRF.Reader.ProcessContentEvent.Message
                                | MDRF.Reader.ProcessContentEvent.Error
                                | MDRF.Reader.ProcessContentEvent.NewTimeStamp
                                | MDRF.Reader.ProcessContentEvent.Occurrence
                                | MDRF.Reader.ProcessContentEvent.Group
                                | MDRF.Reader.ProcessContentEvent.EmptyGroup
                                | MDRF.Reader.ProcessContentEvent.PartialGroup
                                //| MDRF.Reader.ProcessContentEvent.StartOfFullGroup
                                //| MDRF.Reader.ProcessContentEvent.GroupSetStart
                                //| MDRF.Reader.ProcessContentEvent.GroupSetEnd
                    );

                // map lastDateTime to be explicitly just before MaxValue to prevent ReadAndProcessContents from replacing it with inferred last row TS from index (which might not round correctly)
                var endIsInfinity = filter.EndUTCTimeSince1601 == double.PositiveInfinity;
                var lastDateTime = (endIsInfinity ? DateTime.MaxValue - (1.0).FromSeconds() : filter.EndUTCTimeSince1601.GetDateTimeFromUTCTimeSince1601());

                var rapFilterSpec = new MDRF.Reader.ReadAndProcessFilterSpec()
                {
                    //FirstDateTime = filter.UTCStartDateTime,      // always start at the beginning of the file so that we will have valid data on the first actual callback
                    LastDateTime = lastDateTime,
                    PCEMask = pceMask,
                    OccurrenceFilterDelegate = ioi => filter.OccurrenceDictionary.ContainsKey(ioi.OccurrenceID),
                    GroupFilterDelegate = igi => filter.GroupIDHashSet.Contains(igi.GroupID),
                    EventHandlerDelegate = (sender, pcedata) => InnerHandlePCDData_MDRF1(pcedata),
                    NominalMinimumGroupAndTimeStampUpdateInterval = filter.CurrentSelectedPointSetIntervalInSecDelegate?.Invoke() ?? 0.0,
                };

                mdrf1_fileSummary = new MDRF2QueryFileSummary()
                {
                    FileInfo = FileInfo,
                    FaultCode = FaultCode,
                    FileLength = FileInfo.FileLength,
                    FirstFileDTPair = new Common.MDRF2DateTimeStampPair() { FileDeltaTime = 0.0, UTCTimeSince1601 = mdrf1Reader.DateTimeInfo.UTCTimeSince1601 },
                    EncounteredCountByGroupIDArray = new ulong[FileInfo.SpecItemSet.GroupDictionary.ValueArray.Select(gi => gi.ClientID).ConcatItems(0).Max()],
                    EncounteredCountByOccurrenceIDArray = new ulong[FileInfo.SpecItemSet.OccurrenceDictionary.ValueArray.Select(oi => oi.ClientID).ConcatItems(0).Max()],
                };

                MDRF1_PostRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.FileStart, in _CurrentQRDTPair, mdrf1_fileSummary));

                if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.FileStart) != 0)
                    MDRF1_PostRecord(new MDRF2QueryRecord<MDRF2QueryFileSummary>() { FileInfo = FileInfo, ItemType = MDRF2QueryItemTypeSelect.FileStart, DTPair = CurrentQRDTPair, Data = mdrf1_fileSummary.MakeCopyOfThis() });

                mdrf1Reader.ReadAndProcessContents(rapFilterSpec);

                MDRF1_PostRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.FileEnd, in _CurrentQRDTPair, mdrf1_fileSummary));

                if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.FileEnd) != 0)
                    MDRF1_PostRecord(new MDRF2QueryRecord<MDRF2QueryFileSummary>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.FileEnd, Data = mdrf1_fileSummary });
            }
            finally
            {
                mdrf1_RecordBuffer.CompleteAdding();
            }
        }

        private MDRF2QueryFileSummary mdrf1_fileSummary;
        private Common.InlineIndexRecord mdrf1_currentInlineIndexRecord = new Common.InlineIndexRecord();
        private FileIndexRowBase mdrf1_LastFileIndexRow = null;

        const int retainRecentPCEDataItemCount = 0;
        List<MDRF.Reader.ProcessContentEventData> recentPCEDataList = new List<MDRF.Reader.ProcessContentEventData>();

        private void InnerHandlePCDData_MDRF1(MDRF.Reader.ProcessContentEventData pceData)
        {
#pragma warning disable CS0162
            if (retainRecentPCEDataItemCount > 0)
            {
                if (recentPCEDataList.Count >= retainRecentPCEDataItemCount)
                    recentPCEDataList.RemoveAt(0);

                recentPCEDataList.Add(pceData);
            }
#pragma warning restore CS0162

            Filter filter = mdrf1_filter;
            MDRF2QueryFileSummary fileSummary = mdrf1_fileSummary;

            if (mdrf1_cancellationToken.IsCancellationRequested)
                mdrf1_cancellationToken.ThrowIfCancellationRequested();

            fileSummary.BytesProcessed += (ulong)(pceData.DataBlockBuffer?.TotalLength ?? default);
            fileSummary.RecordCount += 1;

            var pceDataUTCTimeSince1601 = pceData.UTCDateTime.GetUTCTimeSince1601();
            if (CurrentQRDTPair.FileDeltaTime != pceData.FileDeltaTimeStamp || CurrentQRDTPair.UTCTimeSince1601 != pceDataUTCTimeSince1601)
            {
                _CurrentQRDTPair.FileDeltaTime = pceData.FileDeltaTimeStamp;
                _CurrentQRDTPair.UTCTimeSince1601 = pceDataUTCTimeSince1601;

                fileSummary.LastFileDTPair = _CurrentQRDTPair;
            }

            bool currentTimeIsInRange = CurrentQRDTPair.UTCTimeSince1601.IsInRange(filter.StartUTCTimeSince1601, filter.EndUTCTimeSince1601);
            bool recordProcessed = false;

            switch (pceData.PCE)
            {
                case MDRF.Reader.ProcessContentEvent.RowStart:
                    {
                        var inlineIndexRecord = new Common.InlineIndexRecord(pceData.Row);
                        inlineIndexRecord.BlockByteCount = (uint) mdrf1Reader.FileIndexInfo.GetRowLength(pceData.Row, mdrf1Reader.FileLength);

                        mdrf1_currentInlineIndexRecord = inlineIndexRecord;
                        mdrf1_LastFileIndexRow = pceData.Row;

                        MDRF1_PostRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.InlineIndexRecord | MDRF2QueryItemTypeSelect.MDRF1, in _CurrentQRDTPair, inlineIndexRecord));

                        if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.InlineIndexRecord) != 0)
                            MDRF1_PostRecord(new MDRF2QueryRecord<Common.InlineIndexRecord>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.InlineIndexRecord, Data = inlineIndexRecord });

                        fileSummary.InlineIndexCount += 1;

                        recordProcessed = true;
                    }
                    break;

                case MDRF.Reader.ProcessContentEvent.RowEnd:
                    {
                        MDRF1_PostRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.BlockEnd | MDRF2QueryItemTypeSelect.MDRF1, in _CurrentQRDTPair, mdrf1_currentInlineIndexRecord));

                        if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.BlockEnd) != 0)
                            MDRF1_PostRecord(new MDRF2QueryRecord<Common.InlineIndexRecord>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.BlockEnd, Data = mdrf1_currentInlineIndexRecord });

                        recordProcessed = true;
                    }
                    break;

                case MDRF.Reader.ProcessContentEvent.ReadingEnd:
                    mdrf1_fileSummary.EndRecordFound = pceData.VC.GetValueBo(rethrow: false);

                    recordProcessed = true;
                    break;

                default:
                    break;
            }

            if (currentTimeIsInRange && !recordProcessed)
            {
                switch (pceData.PCE)
                {
                    case MDRF.Reader.ProcessContentEvent.Message:
                        fileSummary.ProcessedObjectCount += 1;     // Messages are stored as objects in MDRF2
                        if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.Mesg) != 0)
                        {
                            var mi = pceData.MessageInfo;
                            MDRF1_PostRecord(new MDRF2QueryRecord<string>()
                            {
                                FileInfo = FileInfo,
                                DTPair = new Common.MDRF2DateTimeStampPair() { FileDeltaTime = mi.FileDeltaTimeStamp, UTCTimeSince1601 = mi.MessageRecordedUtcTime },
                                ItemType = MDRF2QueryItemTypeSelect.Mesg,
                                Data = mi.Message,
                            });
                        }
                        recordProcessed = true;
                        break;

                    case MDRF.Reader.ProcessContentEvent.Error:
                        fileSummary.ProcessedObjectCount += 1;     // Messages are stored as objects in MDRF2
                        if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.Error) != 0)
                        {
                            var mi = pceData.MessageInfo;
                            MDRF1_PostRecord(new MDRF2QueryRecord<string>()
                            {
                                FileInfo = FileInfo,
                                DTPair = new Common.MDRF2DateTimeStampPair() { FileDeltaTime = mi.FileDeltaTimeStamp, UTCTimeSince1601 = mi.MessageRecordedUtcTime },
                                ItemType = MDRF2QueryItemTypeSelect.Error,
                                Data = mi.Message,
                            });
                        }
                        recordProcessed = true;
                        break;

                    case MDRF.Reader.ProcessContentEvent.Occurrence:
                        {
                            var oi = pceData.OccurrenceInfo;
                            int occurrenceID = oi.OccurrenceID;
                            int idIdx = occurrenceID - 1;
                            if (fileSummary.EncounteredCountByOccurrenceIDArray.IsSafeIndex(idIdx))
                                fileSummary.EncounteredCountByOccurrenceIDArray[idIdx] += 1;

                            if (filter.OccurrenceDictionary.ContainsKey(occurrenceID))
                            {
                                fileSummary.ProcessedOccurrenceCount += 1;

                                MDRF1_PostRecord(new MDRF2QueryRecord<MDRF2OccurrenceQueryRecordData>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.Occurrence, Data = new MDRF2OccurrenceQueryRecordData(oi, pceData.VC) });
                            }
                        }
                        recordProcessed = true;
                        break;

                    case MDRF.Reader.ProcessContentEvent.NewTimeStamp:
                        {
                            MDRF1_PostRecordIfNotNull(filter.NoteOtherRecordDelegate?.Invoke(filter, MDRF2QueryItemTypeSelect.FileDeltaTimeUpdate, in _CurrentQRDTPair));

                            if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.FileDeltaTimeUpdate) != 0)
                                MDRF1_PostRecord(new MDRF2QueryRecord<Common.MDRF2DateTimeStampPair>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.FileDeltaTimeUpdate, Data = CurrentQRDTPair });
                        }
                        recordProcessed = true;
                        break;

                    default:
                        break;
                }

                var groupInfo = pceData.GroupInfo;
                if (groupInfo != null)
                {
                    int groupID = groupInfo.GroupID;
                    int idIdx = groupID - 1;
                    if (fileSummary.EncounteredCountByGroupIDArray.IsSafeIndex(idIdx))
                        fileSummary.EncounteredCountByGroupIDArray[idIdx] += 1;

                    if (filter.ProcessGroup(groupID))
                    {
                        var emptyMPReader = new MessagePackReader();

                        MDRF1_PostRecordIfNotNull(filter.GroupDataRecordingDelegate?.Invoke(filter, groupID, in _CurrentQRDTPair, ref emptyMPReader, mpOptions, groupInfo));

                        fileSummary.ProcessedGroupCount += 1;
                    }

                    recordProcessed = true;
                }
            }

            if (!recordProcessed)
            {
                fileSummary.SkippedRecordCount += 1;
                fileSummary.SkippedByteCount += (ulong) (pceData.DataBlockBuffer?.TotalLength ?? 0);
            }
        }

        #endregion

        #region Supporting logic

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IMDRF2QueryRecord GenerateAndLogDecodingIssue(Filter filter, string mesg)
        {
            Logger.Debug.Emit("Encountered file decoding issue: {0}", mesg);

            if ((filter.ItemTypeSelect & MDRF2QueryItemTypeSelect.DecodingIssue) != 0)
            {
                return new MDRF2QueryRecord<string>() { FileInfo = FileInfo, DTPair = CurrentQRDTPair, ItemType = MDRF2QueryItemTypeSelect.DecodingIssue, Data = mesg };
            }

            return null;
        }

        public Common.MDRF2DateTimeStampPair CurrentQRDTPair { get { return _CurrentQRDTPair; } private set { _CurrentQRDTPair = value; } }
        private Common.MDRF2DateTimeStampPair _CurrentQRDTPair;

        #endregion
    }

    #endregion

    #region StopExtractionException

    public class StopExtractionException : System.Exception
    {
        public StopExtractionException(string mesg = null, System.Exception innerException = null) : base(mesg, innerException) { }
    }

    #endregion
}

//-------------------------------------------------------------------
