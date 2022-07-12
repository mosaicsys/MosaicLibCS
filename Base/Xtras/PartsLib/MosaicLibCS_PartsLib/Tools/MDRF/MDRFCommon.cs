//-------------------------------------------------------------------
/*! @file MDRFCommon.cs
 *  @brief source file for common MDRF (Mosaic Data Recording Format) related definitions.
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

using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.PartsLib.Tools.MDRF.Common
{
    #region SetupInfo

    /// <summary>
    /// Setup information that is used to configure an MDRFWriter in order to write to an MDRF file.
    /// <para/>On a reader this gives the client access to the information that the writer was originally given including the ClientNVS which carries client specific meta data that is global to the file.
    /// </summary>
    public class SetupInfo
    {
        /// <summary>Helper debug and logging method</summary>
        public override string ToString()
        {
            string nvsStr = (ClientNVS.IsNullOrEmpty() ? "" : " {0}".CheckedFormat(ClientNVS));
            return "SetupInfo Client:'{0}' fnPrefix:'{1}' nomSize:{2} numRows:{3}{4}".CheckedFormat(ClientName, FileNamePrefix, NominalMaxFileSize, FileIndexNumRows, nvsStr);
        }

        /// <summary>Gives the client provided meta data NVS that the client would like included in the file.  May be used to give file level context information for use by tools that read MDRF files.</summary>
        public INamedValueSet ClientNVS { get; set; }

        /// <summary>Gives the path to the directory that an MDRFWriter shall create its MDRF file within.</summary>
        public string DirPath { get; set; }

        /// <summary>Gives the name of the client that is writing MDRF files.  May be empty.</summary>
        public string ClientName { get; set; }

        /// <summary>This is combined with the current date to create each mdrf file name.  <para/><code>string fileName = "{0}_{1}.mdrf".CheckedFormat(setup.FileNamePrefix, dateTimePart);</code></summary>
        public string FileNamePrefix { get; set; }

        /// <summary>When true, the MDRFWriter will create the DirPath directoy if needed.  When false it will not attempt to do this.  Defaults to true</summary>
        public bool CreateDirectoryIfNeeded { get; set; }

        /// <summary>Gives the maximum MDRF data block size.  Used to size internal buffers.  Not used in MDRF2.  Defaults to 252144.</summary>
        public Int32 MaxDataBlockSize { get; set; }

        /// <summary>Gives the nominal maximum MDRF2 file block write length.  Not used in MDRF1.  Defaults to 65536.</summary>
        public Int32 NominalMaxDataBlockSize { get; set; }

        /// <summary>Only used with MDRF2 - defines the initial size of the block buffer that is used for serializing the file's meta data.</summary>
        public const Int32 InitialMetaDataBlockSize = 256 * 1024;

        /// <summary>Only used for MDRF2 - defines the extras space allocated to the initial block buffer so as to minimize the number of reallocations required to reach steady state. [10240]</summary>
        public const Int32 NominalExtraDataBlockSize = 10240;

        /// <summary>Gives the nominal maximum MDRF file size after which a new file will be created.  Defaults to 100 * 1024 * 1024 for MDRF and 50*1024*1024 for MDRF2.</summary>
        public Int32 NominalMaxFileSize { get; set; }

        /// <summary>Only available for MDRF2 files.  When > 0 the writer will generate .mdrf2.lz4 files using LZ4 compression in place of the .mdrf2 uncompressed version.  [1]</summary>
        public int CompressionLevel { get; set; }

        /// <summary>
        /// Defines the size of the file index block, in rows.
        /// The file index divides the file into nominal regions of NominalMaxFileSize/FileIndexNumRows bytes.  
        /// Each row records the file offset (and delta times) to the first block in the row's given region of the file.
        /// In addition flag bits are recorded and retained in each row as the bitwise or of the flag bits of all blocks that have been written (started) within this rows corresponding file region.
        /// This allows clients to make use of some form of random access based on the use of known user assigned and writer flag bits.
        /// <para/>Setting this value to zero effictively disables the use (and updates to) the index block as the file is written.
        /// <para/>Defaults to 2048.
        /// </summary>
        public Int32 FileIndexNumRows { get; set; }

        /// <summary>Gives the nominal maximum TimeSpan during which a given MDRF file can be recorded.  Defaults to 24 hours</summary>
        public TimeSpan MaxFileRecordingPeriod { get; set; }

        /// <summary>Gives the minimum file recording period for cases where a file is started just before a new file boundary condition is detected.  Defaults to 15.0 seconds</summary>
        public TimeSpan MinInterFileCreateHoldoffPeriod { get; set; }

        /// <summary>
        /// Gives the minimum nominal interval between updating the FileIndex.  
        /// Set this to be much smaller if you are expecting to use the index to querty the file contents while a file is being written.  
        /// <para/>For MDRF2 this value defines the nominal interval on which inline index records are generated and a content block is written to the output stream along with the rest of the records that have been accumulated since the start of the last inline index record was written.
        /// <para/>Defaults to 60.0 seconds for MDRF and 10.0 seconds for MDRF2.
        /// </summary>
        public TimeSpan MinNominalFileIndexWriteInterval { get; set; }

        /// <summary>
        /// Gives the nominal interval between time triggered group write all operations.  Defaults to 15.0 minutes.
        /// <para/>For MDRF2 if this value is set to zero it disables automatic periodic writeall.
        /// </summary>
        public TimeSpan MinNominalWriteAllInterval { get; set; }

        /// <summary>Gives the offset used with I8 values in preparation for U8Auto coding.  Not used with MDRF2.  Defaults to 120</summary>
        public Int64 I8Offset { get; set; }

        /// <summary>Gives the offset used with I4 values in preparation for U8Auto coding.  Not used with MDRF2.  Defaults to 120</summary>
        public Int32 I4Offset { get; set; }

        /// <summary>Gives the offset used with I2 values in preparation for U8Auto coding.  Not used with MDRF2.  Defaults to 27</summary>
        public Int16 I2Offset { get; set; }

        /// <summary>Default constructor.  Calls SetFrom(null).</summary>
        public SetupInfo() { SetFrom(null); }

        /// <summary>Copy constructor.  Calls SetFrom(<paramref name="other"/>)</summary>
        public SetupInfo(SetupInfo other) { SetFrom(other); }

        /// <summary>Used to initialize the contents of this object, either as a copy of the given non-null <paramref name="other"/> instance, or using the default values when <paramref name="other"/> is given as null.</summary>
        public SetupInfo SetFrom(SetupInfo other)
        {
            if (other == null)
            {
                ClientNVS = NamedValueSet.Empty;

                DirPath = string.Empty;
                ClientName = string.Empty;
                FileNamePrefix = string.Empty;
                CreateDirectoryIfNeeded = true;
                MaxDataBlockSize = 262144;
                NominalMaxDataBlockSize = 0;
                NominalMaxFileSize = 65536;
                NominalMaxFileSize = 100 * 1024 * 1024;
                FileIndexNumRows = 2048;            // 51200 bytes per row.  42 seconds per row (assuming full file consumes one day)
                MaxFileRecordingPeriod = (24.0).FromHours();
                MinInterFileCreateHoldoffPeriod = (15.0).FromSeconds();
                MinNominalFileIndexWriteInterval = (60.0).FromSeconds();
                MinNominalWriteAllInterval = (15.0).FromMinutes();
                I8Offset = 120;
                I4Offset = 120;
                I2Offset = 27;
            }
            else
            {
                ClientNVS = other.ClientNVS.ConvertToReadOnly();

                DirPath = other.DirPath;
                ClientName = other.ClientName.MapNullOrEmptyTo("NoClientNameGiven");
                FileNamePrefix = other.FileNamePrefix;
                CreateDirectoryIfNeeded = other.CreateDirectoryIfNeeded;
                MaxDataBlockSize = other.MaxDataBlockSize;
                NominalMaxDataBlockSize = other.NominalMaxDataBlockSize;
                NominalMaxFileSize = other.NominalMaxFileSize;
                FileIndexNumRows = other.FileIndexNumRows;
                MaxFileRecordingPeriod = other.MaxFileRecordingPeriod;
                MinInterFileCreateHoldoffPeriod = other.MinInterFileCreateHoldoffPeriod;
                MinNominalFileIndexWriteInterval = other.MinNominalFileIndexWriteInterval;
                MinNominalWriteAllInterval = other.MinNominalWriteAllInterval;
                I8Offset = other.I8Offset;
                I4Offset = other.I4Offset;
                I2Offset = other.I2Offset;
            }

            return this;
        }

        /// <summary>This method is used to replace default (0) values for most property/field here with the value from the given <paramref name="other"/> instance.</summary>
        public SetupInfo MapDefaultsTo(SetupInfo other)
        {
            if (ClientNVS.IsNullOrEmpty())
                ClientNVS = other.ClientNVS.ConvertToReadOnly();

            DirPath = DirPath.MapNullOrEmptyTo(other.DirPath);
            ClientName = ClientName.MapNullOrEmptyTo(other.ClientName);
            FileNamePrefix = FileNamePrefix.MapNullOrEmptyTo(other.FileNamePrefix);

            CreateDirectoryIfNeeded = CreateDirectoryIfNeeded.MapDefaultTo(other.CreateDirectoryIfNeeded);
            MaxDataBlockSize = MaxDataBlockSize.MapDefaultTo(other.MaxDataBlockSize);
            NominalMaxDataBlockSize = NominalMaxDataBlockSize.MapDefaultTo(other.NominalMaxDataBlockSize);
            NominalMaxFileSize = NominalMaxFileSize.MapDefaultTo(other.NominalMaxFileSize);
            FileIndexNumRows = FileIndexNumRows.MapDefaultTo(other.FileIndexNumRows);
            MaxFileRecordingPeriod = MaxFileRecordingPeriod.MapDefaultTo(other.MaxFileRecordingPeriod);
            MinInterFileCreateHoldoffPeriod = MinInterFileCreateHoldoffPeriod.MapDefaultTo(other.MinInterFileCreateHoldoffPeriod);
            MinNominalFileIndexWriteInterval = MinNominalFileIndexWriteInterval.MapDefaultTo(other.MinNominalFileIndexWriteInterval);
            MinNominalWriteAllInterval = MinNominalWriteAllInterval.MapDefaultTo(other.MinNominalWriteAllInterval);
            I8Offset = I8Offset.MapDefaultTo(other.I8Offset);
            I4Offset = I4Offset.MapDefaultTo(other.I4Offset);
            I2Offset = I2Offset.MapDefaultTo(other.I2Offset);

            return this;
        }

        /// <summary>1024 * 1024 = 1048576</summary>
        public const Int32 i4OneMillion = 1024 * 1024;

        /// <summary>512 * 1024 = 524288</summary>
        public const Int32 i4HalfMillion = 512 * 1024;

        /// <summary>
        /// Used to enforce specific range limits on values in this object:
        /// <para/>MaxDataBlockSize must be between 65536 and 1048576, rounded up to the next multiple of 16384,
        /// NominalMaxFileSize must be between 524288 and 2147483647 (Int32.MaxValue),
        /// MaxFileRecordingPeriod must be between 5 minutes and 7 days,
        /// FileIndexNumRows must be 0 or between 256 and 65536,
        /// MinInterFileCreateHoldoffPeriod must be between 15 seconds and 1 hour,
        /// If I8Offset/I4Offset/I2Offset is -1 it will be set to zero.
        /// </summary>
        public SetupInfo ClipValues()
        {
            // clip and round the maxDataBlockSize up to the next multiple of 4096 that is not larger than i4OneMillion
            MaxDataBlockSize = (MaxDataBlockSize.Clip(65536, i4OneMillion) + 0xfff) & 0x7ffff000;
            NominalMaxDataBlockSize = (NominalMaxDataBlockSize.Clip(1, i4OneMillion));

            NominalMaxFileSize = NominalMaxFileSize.Clip(i4HalfMillion, Int32.MaxValue);

            MaxFileRecordingPeriod = MaxFileRecordingPeriod.Clip(TimeSpan.FromMinutes(5.0), TimeSpan.FromDays(7.0));

            FileIndexNumRows = (FileIndexNumRows != -1) ? FileIndexNumRows.Clip(256, 65536) : 0;

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

        /// <summary>Applies the I8Offset to the given <paramref name="i8"/> value and returns it as a UInt64</summary>
        public UInt64 ConvertToU8WithI8Offset(Int64 i8) { return unchecked((UInt64)(i8 + I8Offset)); }
        /// <summary>Applies the I4Offset to the given <paramref name="i4"/> value and returns it as a UInt32</summary>
        public UInt32 ConvertToU4WithI4Offset(Int32 i4) { return unchecked((UInt32)(i4 + I4Offset)); }
        /// <summary>Applies the I2Offset to the given <paramref name="i2"/> value and returns it as a UInt16</summary>
        public UInt16 ConvertToU2WithI2Offset(Int16 i2) { return unchecked((UInt16)(i2 + I2Offset)); }

        /// <summary>Converts the given <paramref name="u8"/> value to an Int64, subtracts the given I8Offset and returns it.</summary>
        public Int64 ConvertToI8FromU8WithOffset(UInt64 u8) { return unchecked((Int64)(u8) - I8Offset); }
        /// <summary>Converts the given <paramref name="u4"/> value to an Int32, subtracts the given I4Offset and returns it.</summary>
        public Int32 ConvertToI4FromU4WithOffset(UInt32 u4) { return unchecked((Int32)(u4) - I4Offset); }
        /// <summary>Converts the given <paramref name="u2"/> value to an Int16, subtracts the given I2Offset and returns it.</summary>
        public Int16 ConvertToI2FromU2WithOffset(UInt16 u2) { return unchecked((Int16)(u2 - I2Offset)); }

        #endregion

        #region MDRF2DefaultValue

        public static SetupInfo DefaultForMDRF2
        {
            get
            {
                return new SetupInfo()
                {
                    NominalMaxDataBlockSize = 65536,
                    NominalMaxFileSize = 50*1024*1024,
                    MaxFileRecordingPeriod = (24.0).FromHours(),
                    MinInterFileCreateHoldoffPeriod = (15.0).FromSeconds(),
                    MinNominalFileIndexWriteInterval = (10.0).FromSeconds(),
                    MinNominalWriteAllInterval = (15.0).FromMinutes(),
                    FileIndexNumRows = -1,      // maps to 0 in ClipValues
                    I8Offset = -1,      // maps to 0 in ClipValues
                    I4Offset = -1,      // maps to 0 in ClipValues
                    I2Offset = -1,      // maps to 0 in ClipValues
                };
            }
        }

        #endregion

        public SetupInfo UpdateFrom(INamedValueSet nvs)
        {
            DirPath = nvs["setup.DirPath"].VC.GetValueA(rethrow: false);
            ClientName = nvs["setup.ClientName"].VC.GetValueA(rethrow: false);
            FileNamePrefix = nvs["setup.FileNamePrefix"].VC.GetValueA(rethrow: false);
            CreateDirectoryIfNeeded = nvs["setup.CreateDirectoryIfNeeded"].VC.GetValueBo(rethrow: false);
            MaxDataBlockSize = nvs["setup.MaxDataBlockSize"].VC.GetValueI4(rethrow: false);
            NominalMaxDataBlockSize = nvs["setup.NominalMaxDataBlockSize"].VC.GetValueI4(rethrow: false);
            NominalMaxFileSize = nvs["setup.NominalMaxFileSize"].VC.GetValueI4(rethrow: false);
            FileIndexNumRows = nvs["setup.FileIndexNumRows"].VC.GetValueI4(rethrow: false);
            MaxFileRecordingPeriod = nvs["setup.MaxFileRecordingPeriod"].VC.GetValueTS(rethrow: false);
            MinInterFileCreateHoldoffPeriod = nvs["setup.MinInterFileCreateHoldoffPeriod"].VC.GetValueTS(rethrow: false);
            MinNominalFileIndexWriteInterval = nvs["setup.MinNominalFileIndexWriteInterval"].VC.GetValueTS(rethrow: false);
            MinNominalWriteAllInterval = nvs["setup.MinNominalWriteAllInterval"].VC.GetValueTS(rethrow: false);
            I8Offset = nvs["setup.I8Offset"].VC.GetValueI8(rethrow: false);
            I4Offset = nvs["setup.I4Offset"].VC.GetValueI4(rethrow: false);
            I2Offset = nvs["setup.I2Offset"].VC.GetValueI2(rethrow: false);

            return this;
        }

        public NamedValueSet UpdateNVSFromThis(NamedValueSet nvs)
        {
            nvs.SetValue("setup.DirPath", DirPath);
            nvs.SetValue("setup.ClientName", ClientName);
            nvs.SetValue("setup.FileNamePrefix", FileNamePrefix);
            nvs.SetValue("setup.CreateDirectoryIfNeeded", CreateDirectoryIfNeeded);
            nvs.ConditionalSetValue("setup.MaxDataBlockSize", MaxDataBlockSize != 0, MaxDataBlockSize);
            nvs.ConditionalSetValue("setup.NominalMaxDataBlockSize", NominalMaxDataBlockSize != 0, NominalMaxDataBlockSize);
            nvs.ConditionalSetValue("setup.NominalMaxFileSize", NominalMaxFileSize != 0, NominalMaxFileSize);
            nvs.ConditionalSetValue("setup.FileIndexNumRows", FileIndexNumRows != 0, FileIndexNumRows);
            nvs.SetValue("setup.MaxFileRecordingPeriodInSeconds", MaxFileRecordingPeriod.TotalSeconds);
            nvs.SetValue("setup.MinInterFileCreateHoldoffPeriodInSeconds", MinInterFileCreateHoldoffPeriod.TotalSeconds);
            nvs.SetValue("setup.MinNominalFileIndexWriteIntervalInSeconds", MinNominalFileIndexWriteInterval.TotalSeconds);
            nvs.SetValue("setup.MinNominalWriteAllIntervalInSeconds", MinNominalWriteAllInterval.TotalSeconds);
            nvs.ConditionalSetValue("setup.I8Offset", I8Offset != 0, I8Offset);
            nvs.ConditionalSetValue("setup.I4Offset", I4Offset != 0, I4Offset);
            nvs.ConditionalSetValue("setup.I2Offset", I2Offset != 0, I2Offset);

            return nvs;
        }
    }

    #endregion

    #region ILibraryInfo, LibraryInfo

    /// <summary>
    /// Gives information about the assembly or software library that was used to record this mdrf file.
    /// </summary>
    public interface ILibraryInfo
    {
        /// <summary>Gives the type of file that this record has been included in.</summary>
        string Type { get; }

        /// <summary>Gives the name of the library that was used to generate this file.</summary>
        string Name { get; }

        /// <summary>Gives the version (and date) of the library that was used to generate this file.</summary>
        string Version { get; }

        /// <summary>
        /// Gives the NVS version of this LibraryInfo which is recorded in the MDRF file.  
        /// This NVS generally contains additional information as the same NVS in the MDRF file may be used to carry/record additional meta data about the file.
        /// </summary>
        INamedValueSet NVS { get; }

        /// <summary>This method is used to update the given <paramref name="nvs"/> to contain the lib.Type, lib.Name and lib.Version keywords that are used to carry the values from this library info object.</summary>
        NamedValueSet UpdateNVSFromThis(NamedValueSet nvs);
    }

    /// <summary>
    /// This is the standard implementation class for the ILibraryInfo interface.
    /// </summary>
    public class LibraryInfo : ILibraryInfo
    {
        /// <summary>Default constructor.  Sets the NVS, Type, Name and Version to be Empty.</summary>
        public LibraryInfo()
        {
            NVS = NamedValueSet.Empty;
            Type = string.Empty;
            Name = string.Empty;
            Version = string.Empty;
        }

        /// <summary>Copy constructor.  Sets the NVS to be empty and the Type, Name and Version from the given <paramref name="other"/> instance.</summary>
        public LibraryInfo(ILibraryInfo other)
        {
            NVS = NamedValueSet.Empty;
            Type = other.Type;
            Name = other.Name;
            Version = other.Version;
        }

        /// <inheritdoc/>
        public INamedValueSet NVS { get; set; }

        /// <inheritdoc/>
        public string Type { get; set; }

        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        public string Version { get; set; }

        /// <summary>Logging and Debugging helper method.</summary>
        public override string ToString()
        {
            string nvsStr = (NVS.IsNullOrEmpty() ? "" : " {0}".CheckedFormat(NVS.ToStringSML()));

            return "LibInfo Type:'{0}' Name:'{1}' Version:'{2}'{3}".CheckedFormat(Type, Name, Version, nvsStr);
        }

        /// <summary>Updates this object's Type, Name and Version, and optionally its NVS, from the contents of the given NVS using the standard lib.Type, lib.Name, lib.Version keys.</summary>
        public LibraryInfo UpdateFrom(INamedValueSet nvs, bool updateNVSProperty = true)
        {
            if (NVS == null || updateNVSProperty)
                NVS = nvs.ConvertToReadOnly();

            Type = nvs["lib.Type"].VC.GetValueA(rethrow: false).MapNullToEmpty();
            Name = nvs["lib.Name"].VC.GetValueA(rethrow: false).MapNullToEmpty();
            Version = nvs["lib.Version"].VC.GetValueA(rethrow: false).MapNullToEmpty();

            return this;
        }

        /// <inheritdoc/>
        public NamedValueSet UpdateNVSFromThis(NamedValueSet nvs)
        {
            nvs.SetValue("lib.Type", Type);
            nvs.SetValue("lib.Name", Name);
            nvs.SetValue("lib.Version", Version);

            return nvs;
        }
    }

    #endregion

    #region DateTimeInfo

    /// <summary>
    /// Gives information about the date, time, QpcTime and time zone taken when creating the headers for an mdrf file when it is being created.
    /// </summary>
    public class DateTimeInfo
    {
        /// <summary>
        /// When non-null, this gives the NVS instance that is used to record, or reconstruct, the contents of this object.
        /// </summary>
        public INamedValueSet NVS { get; set; }

        /// <summary>Gives the file delta time at the point when this object was generated.  Normally this value is expected to be zero.</summary>
        public double BlockDeltaTimeStamp { get; set; }

        /// <summary>Gives the QPCTime (F8 seconds since system power on) at the point when this object was generated.</summary>
        public double QPCTime { get; set; }

        /// <summary>Gives the current DateTime converted UTC and represented as an F8 measurement of seconds since 1601 (same offset used with Win32 FTIME)</summary>
        public double UTCTimeSince1601 { get; set; }

        /// <summary>Getter returns the UTCTimeSince1601 converted to a DateTime in UTC format.</summary>
        public DateTime UTCDateTime { get { return UTCTimeSince1601.GetDateTimeFromUTCTimeSince1601(); } }

        /// <summary>Updated by UpdateFrom which sets this to the value from TimeZoneInfo.BaseUtcOffset.TotalSeconds for the given TimeZoneInfo and DateTime which are passed to the method</summary>
        public int TimeZoneOffset { get; set; }

        /// <summary>Updated by UpdateFrom which sets this to the value from TimeZoneInfo.IsDaylightSavingTime() method for the given TimeZoneInfo and DateTime which are passed to the method</summary>
        public bool DSTIsActive { get; set; }

        /// <summary>Updated by UpdateFrom as the difference between the current GetUtcOffset and the BaseUtcOffset for the given TimeZoneInfo and DateTime which are passed to the method</summary>
        public int DSTBias { get; set; }

        /// <summary>Updated by UpdateFrom to give the name of the StandardTime for the TimeZoneInfo which was passed to the methoed.</summary>
        public string TZName0 { get; set; }

        /// <summary>Updated by UpdateFrom to give the name of the DaylightTime for the TimeZoneInfo which was passed to the methoed.</summary>
        public string TZName1 { get; set; }

        /// <summary>Default constructor</summary>
        public DateTimeInfo()
        {
            NVS = NamedValueSet.Empty;
            TZName0 = string.Empty;
            TZName1 = string.Empty;
        }

        /// <summary>
        /// Initializes/Updates the given instance using the given <paramref name="qpcTimeStamp"/>, <paramref name="fileDeltaTimeStamp"/>, <paramref name="dtNow"/> normally in UTC format, <paramref name="utcTimeSince1601"/> and  optional <paramref name="useTZI"/>.
        /// When <paramref name="useTZI"/> is given as null (the default), TimeZoneInfo.Local will be used in its place.
        /// </summary>
        public DateTimeInfo UpdateFrom(QpcTimeStamp qpcTimeStamp, double fileDeltaTimeStamp, DateTime dtNow, double utcTimeSince1601, TimeZoneInfo useTZI = null)
        {
            useTZI = useTZI ?? TimeZoneInfo.Local;
            var isDST = useTZI.IsDaylightSavingTime(dtNow);
            var utcOffset = useTZI.GetUtcOffset(dtNow);

            BlockDeltaTimeStamp = fileDeltaTimeStamp;
            QPCTime = qpcTimeStamp.Time;
            UTCTimeSince1601 = utcTimeSince1601;
            TimeZoneOffset = (int)Math.Round(useTZI.BaseUtcOffset.TotalSeconds);
            DSTIsActive = isDST;
            DSTBias = (int)Math.Round((utcOffset - useTZI.BaseUtcOffset).TotalSeconds);
            TZName0 = useTZI.StandardName;
            TZName1 = useTZI.DaylightName;

            return this;
        }

        /// <summary>Updates this object's contents, and optionally its NVS, from the contents of the given NVS using the standard set of keys. (key names are property names in this case)</summary>
        public DateTimeInfo UpdateFrom(INamedValueSet nvs, bool updateNVSProperty = true)
        {
            if (NVS == null || updateNVSProperty)
                NVS = nvs.ConvertToReadOnly();

            BlockDeltaTimeStamp = nvs["BlockDeltaTimeStamp"].VC.GetValueF8(rethrow: false);
            QPCTime = nvs["QPCTime"].VC.GetValueF8(rethrow: false);
            UTCTimeSince1601 = nvs["UTCTimeSince1601"].VC.GetValueF8(rethrow: false);
            TimeZoneOffset = nvs["TimeZoneOffset"].VC.GetValueI4(rethrow: false);
            DSTIsActive = nvs["DSTIsActive"].VC.GetValueBo(rethrow: false);
            DSTBias = nvs["DSTBias"].VC.GetValueI4(rethrow: false);
            TZName0 = nvs["TZName0"].VC.GetValueA(rethrow: false);
            TZName1 = nvs["TZName1"].VC.GetValueA(rethrow: false);

            return this;
        }

        /// <summary>Updates the given <paramref name="nvs"/> from the contents this object's properies using the standard set of keys. (key names are property names in this case)</summary>
        public NamedValueSet UpdateNVSFromThis(NamedValueSet nvs)
        {
            nvs.SetValue("BlockDeltaTimeStamp", BlockDeltaTimeStamp);
            nvs.SetValue("QPCTime", QPCTime);
            nvs.SetValue("UTCTimeSince1601", UTCTimeSince1601);      // aka FTime with time measured in seconds
            nvs.SetValue("TimeZoneOffset", TimeZoneOffset);
            nvs.SetValue("DSTIsActive", DSTIsActive);
            nvs.SetValue("DSTBias", DSTBias);
            nvs.SetValue("TZName0", TZName0);
            nvs.SetValue("TZName1", TZName1);

            return nvs;
        }
    }

    #endregion

    #region DateTimeStampPair

    /// <summary>
    /// During recording this object is used to aquire a QpcTimeStamp, and optionally a DateTime
    /// This object generally represents a pairing of a FileDeltaTime and a DateTime.  
    /// It is also used to generate FileDeltaTime values during recording
    /// </summary>
    /// <remarks>
    /// There are a number of variations on how this object is used.  
    /// Internally this object is typically generated by a top level method in one of the writer objects used here and is then used by the subordinate methods as needed.
    /// In some cases the original creator of this object may choose to only populate the qpcTimeStamp.  
    /// If the resulting dtPair actually needs a matching DateTime then one will be captured and used to update this object contents at that point.
    /// </remarks>
    public class DateTimeStampPair : ICopyable<DateTimeStampPair>
    {
        /// <summary>When non-Empty (aka non-zero) this gives the QpcTimeStamp for this pair.</summary>
        public QpcTimeStamp qpcTimeStamp;
        /// <summary>When non-default (aka non-zero) this gives the DateTime for this pair.</summary>
        public DateTime dateTime;
        /// <summary>When non-zero this gives the utc time zine 1601 for the given dateTime.</summary>
        public double utcTimeSince1601;

        /// <summary>When correctly set this gives the FileDeltaTime (aka the difference between this QpcTime and the file's initial QpcTime)</summary>
        public double fileDeltaTimeStamp;

        /// <summary>Logging and debug helper</summary>
        public override string ToString()
        {
            return "fdt:{0:f6} dt:{1:o}".CheckedFormat(fileDeltaTimeStamp, dateTime);
        }

        /// <summary>
        /// "Default" constructor.  Sets contents to default values (zero)
        /// </summary>
        public DateTimeStampPair() 
        { }

        /// <summary>
        /// Copy constructor.  If the given value other is null then this constructor sets the current value to Zero
        /// </summary>
        public DateTimeStampPair(DateTimeStampPair other)
        {
            SetFrom(other);
        }

        /// <summary>
        /// Returns a DateTimeStampPair with its contents initialized to reflect the current DateTime/QpcTimeStamp (Now)
        /// </summary>
        public static DateTimeStampPair Now
        {
            get { return new DateTimeStampPair().SetToNow(); }
        }

        /// <summary>
        /// Returns a DateTimeStampPair with its contents initialized to reflect the current QpcTimeStamp (Now) and with DateTime set to be empty.
        /// </summary>
        public static DateTimeStampPair NowQpcOnly
        {
            get { return new DateTimeStampPair().SetToNow(includeDateTime: false); }
        }

        /// <summary>
        /// Returns a DateTimeStampPair with its contents initialized to Zero
        /// </summary>
        public static DateTimeStampPair Zero
        {
            get { return new DateTimeStampPair(); }
        }

        /// <summary>
        /// Sets the current object's values from QpcTimeStamp.Now and optinally DateTime.Now.  Sets the fileDeltaTimeStamp to 0.0.
        /// <para/>Supports call chaining
        /// </summary>
        public DateTimeStampPair SetToNow(bool includeDateTime = true)
        {
            qpcTimeStamp = QpcTimeStamp.Now;
            dateTime = includeDateTime ? DateTime.Now : default(DateTime);
            utcTimeSince1601 = dateTime.GetUTCTimeSince1601();
            fileDeltaTimeStamp = 0.0;

            return this;
        }

        /// <summary>
        /// Copy constructor helper method.  Sets the contents of this object to match the given other object, or to Zero if the other object is null
        /// <para/>Supports call chaining
        /// </summary>
        public DateTimeStampPair SetFrom(DateTimeStampPair other)
        {
            if (other == null)
                other = internalZero;

            qpcTimeStamp = other.qpcTimeStamp;
            dateTime = other.dateTime;
            utcTimeSince1601 = other.utcTimeSince1601;
            fileDeltaTimeStamp = other.fileDeltaTimeStamp;

            return this;
        }

        /// <summary>
        /// Creates and returns a DateTimeStampPair from the given fileDeltaTimeStamp and utcTimeSince1601 values - typically used during deserialization.
        /// </summary>
        public static DateTimeStampPair CreateFrom(double fileDeltaTimeStamp, double utcTimeSince1601)
        {
            return new DateTimeStampPair()
            {
                fileDeltaTimeStamp = fileDeltaTimeStamp,
                utcTimeSince1601 = utcTimeSince1601,
                dateTime = utcTimeSince1601.GetDateTimeFromUTCTimeSince1601(),
            };
        }

        private static readonly DateTimeStampPair internalZero = new DateTimeStampPair();

        /// <summary>
        /// If the fileRefererenceDTPair and setupInfo are non-null, it sets all of the fileDelta
        /// values from the difference between the recorded time(stamp) values and their corresponding values
        /// from the given fileReferenceDTPair.  I8 versions of these are scaled using the I8 to/from seconds
        /// scaling defined in the setupInfo.
        /// Otherwise does nothing.
        /// </summary>
        public DateTimeStampPair UpdateFileDeltas(DateTimeStampPair fileReferenceDTPair)
        {
            if (fileReferenceDTPair != null)
                fileDeltaTimeStamp = (qpcTimeStamp.Time - fileReferenceDTPair.qpcTimeStamp.Time);

            return this;
        }

        /// <summary>
        /// If this dateTime value is zero (aka default) then this method sets it to the given <paramref name="proxyNowValue"/> if it is non-null or to DateTime.Now if it is.
        /// </summary>
        public DateTimeStampPair UpdateDateTimeToNowIfNeeded(DateTime? proxyNowValue = null)
        {
            if (dateTime.IsZero())
                dateTime = proxyNowValue ?? DateTime.Now;

            return this;
        }

        /// <summary>
        /// Sets the fileDeltaTimeStamp to 0.0
        /// </summary>
        public DateTimeStampPair ClearFileDeltas()
        {
            fileDeltaTimeStamp = 0.0;

            return this;
        }

        /// <summary>
        /// Checks if the DTPair's utcTimeSince1601 field is zero and, if so, sets it from the dateTime's field converted to time since 1601 using GetUTCTimeSince1601.
        /// </summary>
        /// <returns></returns>
        public DateTimeStampPair PopulateUTCTimeSince1601IfNeeded()
        {
            if (utcTimeSince1601 == 0.0)
                utcTimeSince1601 = dateTime.GetUTCTimeSince1601();

            return this;
        }

        /// <summary>
        /// Returns a clone/copy of this object.
        /// </summary>
        public DateTimeStampPair MakeCopyOfThis(bool deepCopy = true)
        {
            return (DateTimeStampPair)MemberwiseClone();
        }
    }

    #endregion

    #region FileIndexInfo, TFileIndexInfo<>, FileIndexRowBase, FileIndexLastBlockInfoBase

    /// <summary>
    /// Only used with MDRF1.
    /// </summary>
    public class FileIndexInfo : TFileIndexInfo<FileIndexRowBase, FileIndexLastBlockInfoBase>
    {
        public FileIndexInfo(SetupInfo setupInfo) : base(setupInfo) { }

        public FileIndexInfo(int numRows = 0) : base(numRows) { }

        /// <summary>
        /// Returns the number of bytes between the given <paramref name="row"/> and the next non-empty row, or the end of the file, whichever comes first. 
        /// </summary>
        public int GetRowLength(FileIndexRowBase row, int fileLength)
        {
            var rowIndex = row.RowIndex;

            FileIndexRowBase nextRow = FileIndexRowArray.Skip(rowIndex + 1).Where(scanRow => !scanRow.IsEmpty).FirstOrDefault();

            if (nextRow != null)
                return nextRow.FileOffsetToStartOfFirstBlock - row.FileOffsetToStartOfFirstBlock;
            else if (fileLength != 0)
                return fileLength - row.FileOffsetToStartOfFirstBlock;
            else if (LastBlockInfo != null)
                return (LastBlockInfo.FileOffsetToStartOfBlock + LastBlockInfo.BlockTotalLength) - row.FileOffsetToStartOfFirstBlock;
            else
                return 0;
        }
    }

    /// <summary>
    /// Only used with MDRF1.
    /// </summary>
    public class TFileIndexInfo<TRowType, TLastBlockInfoType> 
        where TRowType: FileIndexRowBase, new()
        where TLastBlockInfoType: FileIndexLastBlockInfoBase, new()
    {
        public override string ToString()
        {
            int numNonEmptyRows = FileIndexRowArray.Where(row => row != null && !row.IsEmpty).Count();

            var indexStateStr = !IndexInUse ? " [IndexNotInUse]" : (FileWasProperlyClosed ? "" : " [NotProperlyClosed]");

            return "FileIndex NumRows:{0} RowSizeDivisor:{1} NumNonEmptyRows:{2}{3}".CheckedFormat(NumRows, RowSizeDivisor, numNonEmptyRows, indexStateStr);
        }

        #region NumRows, RowSizeDivisor, and NominalMaxFileSize

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

        /// <summary>Returns true if the index is in use (aka if the NumRows &gt; 1)</summary>
        public bool IndexInUse { get { return (NumRows > 1); } }

        /// <summary>Returns true if the last block's block type is the expected FileEnd block type</summary>
        public bool FileWasProperlyClosed
        {
            get { return (LastBlockInfo != null && LastBlockInfo.FixedBlockTypeID == FixedBlockTypeID.FileEndV1); }
        }

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

            FileIndexRowBase testRow;

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

        private static readonly TRowType[] emptyRowTypeArray = EmptyArrayFactory<TRowType>.Instance;

        /// <summary>
        /// Helper method that is used to forward scan through the index to find and return the next non-empty row after the rowIndex passed in.
        /// Modified the passed rowIndex by reference with the rowIndex of the returned row (or the length of the row array if no non-empty row was round).
        /// Returns the found row or null if none was found.
        /// </summary>
        public TRowType FindNextNonEmptyFileIndexRow(ref int rowIndex)
        {
            TRowType row = null;
            int fileIndexRowArrayLength = FileIndexRowArray.SafeLength();

            for (; rowIndex < fileIndexRowArrayLength; rowIndex++)
            {
                row = FileIndexRowArray[rowIndex];
                if (row != null && !row.IsEmpty)
                    return (row);
            }

            return row;
        }
    }

    /// <summary>
    /// Only used with MDRF1.
    /// </summary>
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

        /// <summary>Gives the UTC DateTime for the row's indicated FirstBlockUtcTimeSince1601</summary>
        public DateTime FirstBlockDateTime { get { return FirstBlockUtcTimeSince1601.GetDateTimeFromUTCTimeSince1601(); } }

        internal static int SerializedSize { get { return 40; } }

        internal FileIndexRowBase Clear(bool clearRowIndex = false)
        {
            if (clearRowIndex)
                RowIndex = 0;
            FileIndexRowFlagBitsU4 = 0;
            FileOffsetToStartOfFirstBlock = 0;
            FileIndexUserRowFlagBits = 0;
            FirstBlockDeltaTimeStamp = 0;
            LastBlockDeltaTimeStamp = 0;
            FirstBlockUtcTimeSince1601 = 0.0;

            return this;
        }

        /// <summary>
        /// Returns true if the row's contents are empty (zero).  This test does not check the contained RowIndex for zero.
        /// </summary>
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

        public override string ToString()
        {
            if (IsEmpty)
                return "[Empty]";

            return "{0} Offset:{1} Bits:{2} URBits:${3:X8} 1stDTS:{4:f6} lastDTS:{5:f6} 1stDT:{6:yyyyMMdd_HHmmssfff}".CheckedFormat(RowIndex, FileOffsetToStartOfFirstBlock, FileIndexRowFlagBits, FileIndexUserRowFlagBits, FirstBlockDeltaTimeStamp, LastBlockDeltaTimeStamp, FirstBlockDateTime);
        }
    }

    /// <summary>
    /// Only used with MDRF1.
    /// </summary>
    public class FileIndexLastBlockInfoBase
    {
        public Int32 FileOffsetToStartOfBlock { get; internal set; }  // 0..3
        public Int32 BlockTotalLength { get; internal set; }          // 4..7
        public Int32 BlockTypeID32 { get; internal set; }             // 8..11
        public double BlockDeltaTimeStamp { get; internal set; }      // 12..19

        internal static int SerializedSize { get { return 20; } }

        public FixedBlockTypeID FixedBlockTypeID { get { return unchecked((FixedBlockTypeID) BlockTypeID32); } }

        public Int32 InferredMinimumFileLength { get { return (FileOffsetToStartOfBlock + BlockTotalLength); } }
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
        /// <summary>Gives the item type of this item (None, Source, Group, Occurrence)</summary>
        MDItemType ItemType { get; }

        /// <summary>Gives the item's Name</summary>
        string Name { get; }

        /// <summary>Gives any client provided comment for this item.</summary>
        string Comment { get; }

        /// <summary>Gives the assigned FileID for this item.</summary>
        int FileID { get; }

        /// <summary>Gives the ItemType specific ID for this item (if any)</summary>
        int ClientID { get; }

        /// <summary>Gives the ContainerStorageType to be used with this item (as appropriate)</summary>
        ContainerStorageType CST { get; }

        /// <summary>Gives the Semi.E005.Data.ItemFormatCode to be used with this item (as appropriate).  This field is only used with MDRF1 files.</summary>
        Semi.E005.Data.ItemFormatCode IFC { get; }

        /// <summary>Gives the client provided NVS containing any client metadata that is to be associated with this item.</summary>
        INamedValueSet ClientNVS { get; }
    }

    /// <summary>Gives information about an client defined MDRF Group</summary>
    public interface IGroupInfo : IMetaDataCommonInfo
    {
        /// <summary>Gives the user row flag bit value that is to be recorded (merged) into any index when this group is recorded</summary>
        UInt64 FileIndexUserRowFlagBits { get; }

        /// <summary>Gives the FileID values of the GroupPointInfo objects that are associated with this group.</summary>
        IList<int> GroupPointFileIDList { get; }

        /// <summary>Gives the array of IGroupPointInfo instances that are associated with this group.</summary>
        IGroupPointInfo[] GroupPointInfoArray { get; }

        /// <summary>Gives the GroupID for this group (this is a proxy for the ClientID)</summary>
        int GroupID { get; }

        /// <summary>This boolean propery is generlaly used to indicate that the group should be recoded by the mdrf writer when processing a RecordGroups method call (which then clears it).</summary>
        bool Touched { get; set; }
    }

    /// <summary>Gives information about an value source point which is a member of an MDRF Group</summary>
    public interface IGroupPointInfo : IMetaDataCommonInfo
    {
        /// <summary>Gives the current value of the point that will be recorded when the point's group is next recorded using the mdrf writer's RecordGroups method.</summary>
        ValueContainer VC { get; set; }

        /// <summary>Gives the SourceID for this point (this is a proxy for the ClientID)</summary>
        int SourceID { get; }

        /// <summary>Gives the IGroupInfo instance of the group that this source point is recored within.</summary>
        IGroupInfo GroupInfo { get; }

        /// <summary>Gives the full name of the point (aka GroupName.PointName)</summary>
        string FullName { get; }
    }

    /// <summary>Gives information about a client defined MDRF Occurrence</summary>
    public interface IOccurrenceInfo : IMetaDataCommonInfo
    {
        /// <summary>Gives the user row flag bit value that is to be recorded (merged) into any index when this occurrence is recorded</summary>
        UInt64 FileIndexUserRowFlagBits { get; }

        /// <summary>
        /// Set to true if this occurrence is considered to be a high priority occurrence.  
        /// When recording a group that is marked as being high priority, the FileIndexRowFlagBits.ContainsHighPriorityOccurrence will be set in the index when this occurence is recorded.
        /// </summary>
        bool IsHighPriority { get; }

        /// <summary>Gives the OccurenceID for this occurence (this is a proxy for the ClientID)</summary>
        int OccurrenceID { get; }
    }

    /// <summary>
    /// Base implementation class for IMetaDataCommonInfo objects.
    /// </summary>
    public class MetaDataCommonInfoBase : IMetaDataCommonInfo
    {
        /// <inheritdoc/>
        public MDItemType ItemType { get; set; }

        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        public string Comment { get; set; }

        /// <inheritdoc/>
        public int FileID { get; set; }

        /// <inheritdoc/>
        public int ClientID { get; set; }

        /// <inheritdoc/>
        public ContainerStorageType CST { get; set; }

        /// <inheritdoc/>
        public Semi.E005.Data.ItemFormatCode IFC { get; set; }

        /// <inheritdoc/>
        public INamedValueSet ClientNVS { get; set; }

        /// <summary>Default constructor</summary>
        public MetaDataCommonInfoBase() {}

        /// <summary>Copy constructor</summary>
        public MetaDataCommonInfoBase(MetaDataCommonInfoBase other)
        {
            ItemType = other.ItemType;
            Name = other.Name;
            Comment = other.Comment;
            FileID = other.FileID;
            ClientID = other.ClientID;
            CST = other.CST;
            IFC = other.IFC;
            ClientNVS = other.ClientNVS;
        }

        /// <summary>Logging and Debugging helper method</summary>
        public override string ToString()
        {
            string nvsStr = (ClientNVS.IsNullOrEmpty() ? "" : " {0}".CheckedFormat(ClientNVS));
            string commentStr = (Comment.IsNullOrEmpty() ? "" : " Comment:'{0}'".CheckedFormat(Comment));

            return "{0} '{1}' FileID:{2} ClientID:{3} CST:{4} IFC:{5}{6}{7}".CheckedFormat(ItemType, Name, FileID, ClientID, CST, IFC, nvsStr, commentStr);
        }
    }

    /// <summary>
    /// Contains the union of all of the IMDRFDataCommonInfo items that are used with a given file or writter along with corresponding dictionary instances that are used with these SpecItems.
    /// This class is generally only used with MDRF2 readers.
    /// </summary>
    public class SpecItemSet
    {
        /// <summary>Gives the set of all spec items that are associated with a given writter, or which have been read from a given file's header information.</summary>
        public IMetaDataCommonInfo[] SpecItems { get; set; }

        /// <summary>Gives a dictionary of all of the IOccurrenceInfo type SpecItem instances indexed by their Names.</summary>
        public IDictionaryWithCachedArrays<string, IOccurrenceInfo> OccurrenceDictionary { get; set; }

        /// <summary>Gives a dictionary of all of the IGroupInfo type SpecItem instances indexed by their Names.</summary>
        public IDictionaryWithCachedArrays<string, IGroupInfo> GroupDictionary { get; set; }

        /// <summary>Gives a dictionary of all of the IGroupPointInfo type SpecItem instances indexed by their FullNames.</summary>
        public IDictionaryWithCachedArrays<string, IGroupPointInfo> PointFullNameDictionary { get; set; }

        /// <summary>
        /// Gives a dictionary of the IGroupPointInfo type SpecItem instances indexed by their Alias(es) as appropriate.
        /// <para/>Point aliases are established at the point level using the Alias [A] or Aliases [LS] keys in the point's ClientNVS 
        /// or by using the PointAliases [NVS] key in a group's ClientNVS or in the file wide ClientNVS.  
        /// In a group the PointAliases NVS gives alias=pointName while at the global level the PointAliases NVS gives alias=pointFullName.
        /// Each alias key is required to be unique within the space of all the file's aliases.
        /// </summary>
        public IDictionaryWithCachedArrays<string, IGroupPointInfo> PointAliasDictionary { get; set; }
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// This EM takes a given <paramref name="mdci"/> instance and updates a given <paramref name="nvs"/> with its' (ItemType specific) values.
        /// The keys used here match the property names.
        /// </summary>
        public static NamedValueSet UpdateNVSFromThis(this IMetaDataCommonInfo mdci, NamedValueSet nvs)
        {
            nvs.SetValue("Name", mdci.Name);
            nvs.SetValue("ItemType", mdci.ItemType);
            nvs.ConditionalSetValue("Comment", mdci.Comment.IsNeitherNullNorEmpty(), mdci.Comment);
            nvs.SetValue("FileID", mdci.FileID);
            nvs.SetValue("ClientID", mdci.ClientID);
            nvs.ConditionalSetValue("CST", mdci.CST != ContainerStorageType.None, mdci.CST);
            nvs.ConditionalSetValue("IFC", mdci.IFC != Semi.E005.Data.ItemFormatCode.None, mdci.IFC);
            nvs.ConditionalSetValue("ClientNVS", mdci.ClientNVS.IsNeitherNullNorEmpty(), mdci.ClientNVS);

            if (mdci is IOccurrenceInfo)
            {
                var ioi = (IOccurrenceInfo)mdci;

                nvs.ConditionalSetKeyword("IsHighPriority", ioi.IsHighPriority);
                nvs.ConditionalSetValue("FileIndexUserRowFlagBits", ioi.FileIndexUserRowFlagBits != 0, ioi.FileIndexUserRowFlagBits);
            }
            else if (mdci is IGroupInfo)
            {
                var igi = (IGroupInfo)mdci;

                nvs.SetValue("ItemList", string.Join(",", igi.GroupPointFileIDList));
                nvs.ConditionalSetValue("FileIndexUserRowFlagBits", igi.FileIndexUserRowFlagBits != 0, igi.FileIndexUserRowFlagBits);
            }

            return nvs;
        }

        /// <summary>
        /// This EM takes a given <paramref name="setupInfoNVSSet"/> list of NVS instances and generates a SpecItemSet from the set of SpecItemNVS items it contains including generation of each of the related dictionaries.
        /// </summary>
        public static SpecItemSet CreateSpecItemSet(this IList<INamedValueSet> setupInfoNVSSet, INamedValueSet clientNVS = null, bool rethrow = true)
        {
            var specItems = setupInfoNVSSet.Select(nvs => nvs.CreateMDCI(rethrow: rethrow)).WhereIsNotDefault().ToArray();

            var groupPointInfoArray = specItems.Select(item => item as GroupPointInfo).WhereIsNotDefault().ToArray();
            var pointFileIDDictionary = new Dictionary<int, GroupPointInfo>().SafeAddRange(groupPointInfoArray.Select(gpi => KVP.Create(gpi.FileID, gpi)), onlyTakeFirst: false);
            var groupInfoArray = specItems.Select(item => item as GroupInfo).WhereIsNotDefault().ToArray();

            foreach (var gi in groupInfoArray)
            {
                var gpiArray = gi.GroupPointFileIDList.Select(pointFileID => pointFileIDDictionary.SafeTryGetValue(pointFileID)).WhereIsNotDefault().ToArray();

                gi.GroupPointInfoArray = gpiArray;
                gpiArray.DoForEach(gpi => gpi.GroupInfo = gi);
            }

            var groupDictionary = new IDictionaryWithCachedArrays<string, IGroupInfo>().SafeAddRange(groupInfoArray.Select(igi => KVP.Create(igi.Name, igi as IGroupInfo)), onlyTakeFirst: false);
 
            var specSet = new SpecItemSet()
            {
                SpecItems = specItems,
                OccurrenceDictionary = new IDictionaryWithCachedArrays<string, IOccurrenceInfo>().SafeAddRange(specItems.Select(item => item as IOccurrenceInfo).WhereIsNotDefault().Select(ioi => KVP.Create(ioi.Name, ioi)), onlyTakeFirst: false),
                GroupDictionary = groupDictionary,
                PointFullNameDictionary = new IDictionaryWithCachedArrays<string, IGroupPointInfo>().SafeAddRange(groupPointInfoArray.Select(gpi => KVP.Create(gpi.FullName, gpi as IGroupPointInfo)), onlyTakeFirst: false),
            };

            // extract and build dictionary(s) for point name alias handling
            var pad = new IDictionaryWithCachedArrays<string, string>();

            foreach (var gpi in groupPointInfoArray)
            {
                var alias = gpi.ClientNVS["Alias"].VC.GetValueA(rethrow: false).MapNullToEmpty();
                var aliases = gpi.ClientNVS["Aliases"].VC.GetValueLS(rethrow: false).MapNullToEmpty();

                foreach (var s in new[] { alias }.Concat(aliases).Where(s => s.IsNeitherNullNorEmpty()))
                    pad.SafeSetKeyValue(s, gpi.FullName, onlyTakeFirst: true);
            }

            foreach (var gi in groupInfoArray)
            {
                var pointAliasesNVS = gi.ClientNVS["PointAliases"].VC.GetValueNVS(rethrow: false).MapNullToEmpty();

                foreach (var nv in pointAliasesNVS)
                    pad.SafeSetKeyValue(nv.Name, "{0}.{1}".CheckedFormat(gi.Name, nv.VC.GetValueA(rethrow: rethrow)), onlyTakeFirst: true);
            }

            {
                var clientPointAliaseNVS = clientNVS.MapNullToEmpty()["PointAliases"].VC.GetValueNVS(rethrow: false).MapNullToEmpty();

                foreach (var nv in clientPointAliaseNVS)
                    pad.SafeSetKeyValue(nv.Name, nv.VC.GetValueA(rethrow: rethrow), onlyTakeFirst: true);
            }

            specSet.PointAliasDictionary = new IDictionaryWithCachedArrays<string, IGroupPointInfo>().SafeAddRange(pad.Select(kvpIn => KVP.Create(kvpIn.Key, specSet.PointFullNameDictionary.SafeTryGetValue(kvpIn.Value))).Where(kvp => kvp.Key.IsNullOrEmpty() && kvp.Value != null), onlyTakeFirst: false);

            return specSet;
        }

        /// <summary>
        /// This EM takes a given SpecItem <paramref name="nvs"/> and generates and returns a specific type of IMetaDataCommonInfo instance based on the contents of the NVS.
        /// This method currently supports Occurence, Group and Source ItemTypes.
        /// </summary>
        public static IMetaDataCommonInfo CreateMDCI(this INamedValueSet nvs, bool rethrow = true, IMetaDataCommonInfo fallbackValue = null)
        {
            var itemTypeVC = nvs["ItemType"].VC;
            var itemType = itemTypeVC.GetValue<MDItemType>(rethrow: rethrow);

            var name = nvs["Name"].VC.GetValueA(rethrow: rethrow);
            var comment = nvs["Comment"].VC.GetValueA(rethrow: false).MapNullToEmpty();
            var fileID = nvs["FileID"].VC.GetValueI4(rethrow: rethrow);
            var clientID = nvs["ClientID"].VC.GetValueI4(rethrow: rethrow);
            var cst = nvs["CST"].VC.GetValue<ContainerStorageType>(rethrow: false);
            var ifc = nvs["IFC"].VC.GetValue<Semi.E005.Data.ItemFormatCode>(rethrow: false, defaultValue: Semi.E005.Data.ItemFormatCode.None);
            var clientNVS = nvs["ClientNVS"].VC.GetValueNVS(rethrow: false).MapNullToEmpty();

            switch (itemType)
            {
                case MDItemType.Occurrence:
                    return new OccurrenceInfo()
                    {
                        ItemType = itemType,
                        Name = name,
                        Comment = comment,
                        FileID = fileID,
                        ClientID = clientID,
                        CST = cst,
                        IFC = ifc,
                        ClientNVS = clientNVS,
                        IsHighPriority = nvs.Contains("IsHighPriority"),
                        FileIndexUserRowFlagBits = nvs["FileIndexUserRowFlagBits"].VC.GetValueU8(rethrow: false),
                    };

                case MDItemType.Group:
                    return new GroupInfo()
                    {
                        ItemType = itemType,
                        Name = name,
                        Comment = comment,
                        FileID = fileID,
                        ClientID = clientID,
                        CST = cst,
                        IFC = ifc,
                        ClientNVS = clientNVS,
                        FileIndexUserRowFlagBits = nvs["FileIndexUserRowFlagBits"].VC.GetValueU8(rethrow: false),
                        GroupPointFileIDList = new ReadOnlyIList<int>(nvs["ItemList"].VC.GetValueA(rethrow: false).MapNullToEmpty().Split(',').Select(s => new StringScanner(s).ParseValue<int>(0))),
                    };

                case MDItemType.Source:
                    return new GroupPointInfo()
                    {
                        ItemType = itemType,
                        Name = name,
                        Comment = comment,
                        FileID = fileID,
                        ClientID = clientID,
                        CST = cst,
                        IFC = ifc,
                        ClientNVS = clientNVS,
                    };

                default:
                    break;
            }

            if (rethrow)
                new System.InvalidOperationException("{0} is not a valid MDItemType".CheckedFormat(itemTypeVC)).Throw();

            return fallbackValue;
        }

        /// <summary>
        /// This method is used to "merge" multiple SpecItemSets, from multiple MDRF files, into a single merged SpecItemSet that is returned.
        /// In general the resulting dictionaries will contain the names and last IMetaDataCommonInfo instance from any of the given SpecItemSet instances that were given.
        /// </summary>
        public static SpecItemSet Merge(this IEnumerable<SpecItemSet> specItemSetSet)
        {
            Dictionary<string, IMetaDataCommonInfo> specItemDict = new Dictionary<string, IMetaDataCommonInfo>();
            IDictionaryWithCachedArrays<string, IOccurrenceInfo> oDict = new IDictionaryWithCachedArrays<string, IOccurrenceInfo>();
            IDictionaryWithCachedArrays<string, IGroupInfo> gDict = new IDictionaryWithCachedArrays<string, IGroupInfo>();
            IDictionaryWithCachedArrays<string, IGroupPointInfo> pFullNameDict = new IDictionaryWithCachedArrays<string, IGroupPointInfo>();
            IDictionaryWithCachedArrays<string, IGroupPointInfo> pAliasDict = new IDictionaryWithCachedArrays<string, IGroupPointInfo>();

            foreach (var specItemSet in specItemSetSet)
            {
                // keep the last item of each name
                specItemSet.SpecItems.DoForEach(specItem => specItemDict[specItem.Name] = specItem);
                specItemSet.OccurrenceDictionary.ValueArray.DoForEach(oItem => oDict[oItem.Name] = oItem);
                specItemSet.GroupDictionary.ValueArray.DoForEach(gItem => gDict[gItem.Name] = gItem);
                specItemSet.PointFullNameDictionary.ValueArray.DoForEach(pItem => pFullNameDict[pItem.FullName] = pItem);
                specItemSet.PointAliasDictionary.KeyValuePairArray.DoForEach(kvp => pAliasDict[kvp.Key] = kvp.Value);
            }

            var result = new SpecItemSet()
            {
                SpecItems = specItemDict.Values.ToArray(),
                OccurrenceDictionary = oDict,
                GroupDictionary = gDict,
                PointFullNameDictionary = pFullNameDict,
                PointAliasDictionary = pAliasDict,
            };

            return result;
        }
    }

    /// <summary>
    /// Common implementation class for the IGroupInfo interface.
    /// </summary>
    public class GroupInfo : MetaDataCommonInfoBase, IGroupInfo
    {
        /// <inheritdoc/>
        public UInt64 FileIndexUserRowFlagBits { get; internal set; }

        /// <inheritdoc/>
        public IList<int> GroupPointFileIDList { get; internal set; }

        /// <inheritdoc/>
        public IGroupPointInfo[] GroupPointInfoArray { get; internal set; }

        /// <inheritdoc/>
        public int GroupID { get { return ClientID; } }

        /// <inheritdoc/>
        public bool Touched { get; set; }

        /// <summary>Default constructor</summary>
        public GroupInfo() { }

        /// <summary>Copy constructor</summary>
        public GroupInfo(MetaDataCommonInfoBase rhs) : base(rhs) { }
    }

    /// <summary>
    /// Common implementation class for the IGroupPointInfo interface.
    /// </summary>
    public class GroupPointInfo : MetaDataCommonInfoBase, IGroupPointInfo
    {
        /// <inheritdoc/>
        public ValueContainer VC { get; set; }

        /// <inheritdoc/>
        public int SourceID { get { return ClientID; } }

        /// <inheritdoc/>
        public IGroupInfo GroupInfo { get; set; }

        /// <inheritdoc/>
        public string FullName { get { return _FullName ?? (_FullName = "{0}.{1}".CheckedFormat(GroupInfo.Name, Name)); } }
        private string _FullName;

        /// <summary>Default constructor</summary>
        public GroupPointInfo() { }

        /// <summary>Copy constructor</summary>
        public GroupPointInfo(MetaDataCommonInfoBase rhs) : base(rhs) { }
    }

    /// <summary>
    /// Common implementation class for the IOccurenceInfo interface.
    /// </summary>
    public class OccurrenceInfo : MetaDataCommonInfoBase, IOccurrenceInfo
    {
        /// <inheritdoc/>
        public UInt64 FileIndexUserRowFlagBits { get; internal set; }

        /// <inheritdoc/>
        public bool IsHighPriority { get; internal set; }

        /// <inheritdoc/>
        public int OccurrenceID { get { return ClientID; } }

        /// <summary>Default constructor</summary>
        public OccurrenceInfo() { }

        /// <summary>Copy constructor</summary>
        public OccurrenceInfo(MetaDataCommonInfoBase rhs) : base(rhs) { }
    }

    #endregion

    #region Extension Methods

    public static partial class ExtensionMethods
    {
        /// <summary>EM converts the given <paramref name="igiArray"/> set of IGroupInfo instances to a NamedValueSet of of NVSs (one per group info)</summary>
        public static INamedValueSet ConvertToNVS(this IGroupInfo[] igiArray)
        {
            return new NamedValueSet(igiArray.Select(igi => igi.ConvertToNV())).MakeReadOnly();
        }

        /// <summary>EM converts the given <paramref name="igpiArray"/> set of IGroupPointInfo instances to a NamedValueSet of of NVSs (one per group point info)</summary>
        public static INamedValueSet ConvertToNVS(this IGroupPointInfo[] igpiArray)
        {
            return new NamedValueSet(igpiArray.Select(igpi => igpi.ConvertToNV())).MakeReadOnly();
        }

        /// <summary>EM converts the given <paramref name="ioiArray"/> set of IOccurrenceInfo instances to a NamedValueSet of of NVSs (one per group info)</summary>
        public static INamedValueSet ConvertToNVS(this IOccurrenceInfo[] ioiArray)
        {
            return new NamedValueSet(ioiArray.Select(ioi => ioi.ConvertToNV())).MakeReadOnly();
        }

        /// <summary>EM converts the given <paramref name="igi"/> IGroupInfo instance to a NamedValue containing an NVS</summary>
        public static INamedValue ConvertToNV(this IGroupInfo igi)
        {
            return new NamedValue(igi.Name, new NamedValueSet() 
                {
                    { "Comment", igi.Comment },
                    { "ClientID", igi.ClientID },
                    { "ClientNVS", igi.ClientNVS },
                    { "GroupPointInfoNVS", igi.GroupPointInfoArray.ConvertToNVS() },
                }).MakeReadOnly();
        }

        /// <summary>EM converts the given <paramref name="igpi"/> IGroupPointInfo instance to a NamedValue containing an NVS</summary>
        public static INamedValue ConvertToNV(this IGroupPointInfo igpi)
        {
            return new NamedValue(igpi.Name, new NamedValueSet() 
                {
                    { "Comment", igpi.Comment },
                    { "IFC", igpi.IFC }, 
                    { "ClientID", igpi.ClientID },
                    { "ClientNVS", igpi.ClientNVS },
                }).MakeReadOnly();
        }

        /// <summary>EM converts the given <paramref name="ioi"/> IOccurrenceInfo instance to a NamedValue containing an NVS</summary>
        public static INamedValue ConvertToNV(this IOccurrenceInfo ioi)
        {
            return new NamedValue(ioi.Name, new NamedValueSet() 
                {
                    { "Comment", ioi.Comment },
                    { "IFC", ioi.IFC }, 
                    { "ClientID", ioi.ClientID },
                    { "ClientNVS", ioi.ClientNVS },
                }).MakeReadOnly();
        }
    }

    #endregion

    #region IMessageInfo

    /// <summary>Only used with MDRF1 file reader.</summary>
    public interface IMessageInfo
    {
        /// <summary>Gives the "FixedBlockTypeID" of the records from which this message was obtained.  Typically either ErrorV1 or MessageV1</summary>
        FixedBlockTypeID FixedBlockTypeID { get; }

        /// <summary>Gives the FileDeltaTimeStamp of this record</summary>
        double FileDeltaTimeStamp { get; }

        /// <summary>Gives the UTC1602 DateTime value when this message was generated.</summary>
        double MessageRecordedUtcTime { get; }

        /// <summary>Gives the contained message (text/string)</summary>
        string Message { get; }
    }

    /// <summary>Only used with MDRF1 file reader.</summary>
    public class MessageInfo : IMessageInfo
    {
        /// <inheritdoc/>
        public FixedBlockTypeID FixedBlockTypeID { get; set; }

        /// <inheritdoc/>
        public double FileDeltaTimeStamp { get; set; }

        /// <inheritdoc/>
        public double MessageRecordedUtcTime { get; set; }

        /// <inheritdoc/>
        public string Message { get; set; }
    }

    #endregion

    /// <summary>
    /// This enumeration is used to define the fixed (hard coded) block type ID values that are currently used with this
    /// version of the MDRF.   Some of these values are choosen to fit in a U8Auto coded value using 32 bit actual size.
    /// This leaves the entire usable range of 16383 2 byte ID values to be used by the client while still supporting 32 bit
    /// centric library versions.
    /// <para/>This library uses predefined fixed block IDs in the range of 0x00000000 through 0x0000000f, 0x3fff0000 through 0x3fffffff and 0x7fff0000 through 0x7fffffff
    /// <para/>None (0x0), TiomeStampUpdateV1 (0x01), Object (0x02), FirstDynamicallyAssignedID (0x10), FileHeaderV1 (0x7fffba5e), FileEndV1 (0x7fffc105), FileIndexV1 (0x3fff0001), MetaDataV1 (0x3fff0002), ErrorV1 (0x3fff0003), MessageV1 (0x3fff0004), DateTimeV1 (0x3fff0006)
    /// </summary>
    public enum FixedBlockTypeID : int
    {
        /// <summary>Resevered value.  No dynamically assigned ID may use this value. [0x00000000]</summary>
        None = 0x00000000,

        /// <summary>Very high rate block - used to record a new delta time stamp (In F8 format)  [0x00000001]</summary>
        TimeStampUpdateV1 = 0x00000001,

        /// <summary>Fixed block type (MDRF2 FileID in record header) used when recording objects using custom serialization.  Only used in MDRF2 files.  [0x00000002]</summary>
        Object = 0x00000002,

        /// <summary>Resevered value.  This value is the first value that is assigned during setup.  Generally groups are assigned fileIDs first so that they use low numbered (1 byte) fileIDs.  [0x00000010]</summary>
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
    /// <para/>None (0x0000), ContainsHeaderOrMetaData (0x0001), ContainsHighPriorityOccurrence (0x0002), ContainsOccurrence(0x0004), ContainsDateTime (0x0008), ContainsStartOfFullGroup (0x0080), ContainsMessage (0x0100), ContainsError (0x0200), 
    /// <para/>MDRF2 only: ContainsObject (0x0010), ContainsSignificantObject (0x0020), ContainsGroup (0x0400), ContainsGroupSet (0x0800), ContainsEnd (0x8000)
    /// </summary>
    [Flags]
    public enum FileIndexRowFlagBits : ushort
    {
        /// <summary>No specifically identified data block types start in this row [0x0000]</summary>
        None = 0x0000,
        /// <summary>At least one file Header or MetaData block starts in this row [0x0001]</summary>
        ContainsHeaderOrMetaData = 0x0001,
        /// <summary>At least one high priority (signficant) occurrence data block starts in this row [0x0002]</summary>
        ContainsHighPriorityOccurrence = 0x0002,
        /// <summary>At least one occurrence data block starts in this row [0x0004]</summary>
        ContainsOccurrence = 0x0004,
        /// <summary>At least one date time data block starts in this row [0x0008]</summary>
        ContainsDateTime = 0x0008,
        /// <summary>At least one custom object block was added to this "row" [0x0010] [MDRF2 only]</summary>
        ContainsObject = 0x0010,
        /// <summary>At least one custom significant object block was added to this "row" [0x0020] [MDRF2 only]</summary>
        ContainsSignificantObject = 0x0020,
        /// <summary>At least one full group (writeAll) starts in this row [0x0080]</summary>
        ContainsStartOfFullGroup = 0x0080,
        /// <summary>At least one message record is in this "row" [0x0100]</summary>
        ContainsMessage = 0x0100,
        /// <summary>At least one error record is in this "row" [0x0200]</summary>
        ContainsError = 0x0200,
        /// <summary>At least one group is in this "row" [0x0400] [MDRF2 only]</summary>
        ContainsGroup = 0x0400,
        /// <summary>At least one group set is in this "row" [0x0800] [MDRF2 only]</summary>
        ContainsGroupSet = 0x0800,
        /// <summary>This "row"/block contains an End record [0x8000] [MDRF2 only]</summary>
        ContainsEnd = 0x8000,
    }

    /// <summary>
    /// This enumeration defines a (small) set of flag bits and values that are used to decode and interpret the contents of Group data blocks.
    /// <para/>None, HasUpdateMask (0x01), IsStartOfFullGroup (0x80), IsEmptyGroup (0x100 - not included in group blocks)
    /// <para/>NOTE: This enum is recorded in the file as a byte.  As such all enum members with values beyond the 0xff bit mask range cannot actually be recorded in the file and are thus only used as internal flags.
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
        private static readonly byte[] emptyByteArray = EmptyArrayFactory<byte>.Instance;
        private static readonly ValueContainer.Union emptyU = new ValueContainer.Union();

        #region F4 <-> U4, F8 <-> U8 conversion

        /// <summary>EM re-interprets the binary contents of the given <paramref name="u4"/> as an F4 (uses ValueContainer.Union)</summary>
        public static Single CastToF4(this UInt32 u4) { ValueContainer.Union u = emptyU; u.u32 = u4; return u.f32; }

        /// <summary>EM re-interprets the binary contents of the given <paramref name="u8"/> as an F8 (uses ValueContainer.Union)</summary>
        public static Double CastToF8(this UInt64 u8) { ValueContainer.Union u = emptyU; u.u64 = u8; return u.f64; }

        /// <summary>EM re-interprets the binary contents of the given <paramref name="f4"/> as an U4 (uses ValueContainer.Union)</summary>
        public static UInt32 CastToU4(this Single f4) { ValueContainer.Union u = emptyU; u.f32 = f4; return u.u32; }

        /// <summary>EM re-interprets the binary contents of the given <paramref name="f8"/> as an U8 (uses ValueContainer.Union)</summary>
        public static UInt64 CastToU8(this Double f8) { ValueContainer.Union u = emptyU; u.f64 = f8; return u.u64; }

        #endregion

        #region U8Auto (et. al.) related: DecodeU8Auto, AppendU8Auto, AppendU4Auto, AppendU2Auto

        /// <summary>
        /// MDRF1 only: decodes bytes from <paramref name="byteArray"/> at the given <paramref name="startIndex"/> as a variable length U8.  
        /// Sets the given <paramref name="ec"/> to be non-empty if the bytes do not specify a valid variable length encoded U8.
        /// </summary>
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

        /// <summary>
        /// This EM encodes the given <paramref name="valueU8"/> as a variable length U8 and appends the resulting bytes to the given <paramref name="byteArrayBuilder"/>
        /// </summary>
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

        /// <summary>
        /// This EM encodes the given <paramref name="valueU4"/> as a variable length U8 and appends the resulting bytes to the given <paramref name="byteArrayBuilder"/>
        /// </summary>
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

        /// <summary>
        /// This EM encodes the given <paramref name="valueU2"/> as a variable length U8 and appends the resulting bytes to the given <paramref name="byteArrayBuilder"/>
        /// </summary>
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
