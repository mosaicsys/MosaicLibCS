//-------------------------------------------------------------------
/*! @file E099TagRW.cs
 *  @brief Provides common definitions used to make use of drivers for generic E099 style tag read/write hardware (such as a TIRIS tag).
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
using System.Runtime.Serialization;
using System.Linq;
using System.Text;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Time;
using MosaicLib.Modular;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Helpers;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Action;

namespace MosaicLib.PartsLib.Common.E099
{
    public interface ITagRWPart : IActivePartBase
    {
        /// <summary>Returns the TagReaderType for this tag reader (tag r/w) part</summary>
        TagReaderType TagReaderType { get; }

        /// <summary>
        /// Action factory method.  When run, this action cases the part to read the default set of TagID pages and will validate and combine their
        /// contents into a single TagID value which will be put into IReadTagAction's ResultValue property.  The resulting TagID and the corresponding
        /// ITagPageContents array will also be saved in the corresponding fields of the ITagRWState that is published by this part.
        /// </summary>
        IReadTagAction CreateReadTagAction();

        /// <summary>
        /// Action factory method.  When run, this action cases the part to read the indicated range set of TagID pages and will validate and combine their
        /// contents into a single TagID value, using the given tagContentDecodeMode.  The resulting TagID which will be put into IReadTagAction's ResultValue property.  
        /// The resulting TagID and the corresponding ITagPageContents array will also be saved in the corresponding fields of the ITagRWState that is published by this part.
        /// </summary>
        IReadTagAction CreateReadTagAction(int startPageIdx, int numPages, TagContentDecodeMode tagContentDecodeMode);

        /// <summary>
        /// Action factory method.  When run, this action cases the part to read the given number of pages starting at the given startPageIdx (0 based index).
        /// The resulting ITagPageContents array is both saved in this action's ResultValue property and is published in the ITagRWState for this part.
        /// </summary>
        IReadPagesAction CreateReadPagesAction(int startPageIdx, int numPages);

        /// <summary>
        /// Action factory method.  The returned action, when run, causes each of the indicates pages to be written, one at a time,
        /// until all writes are successful or one of them fails.  If the first failure was due to there being NoTagFound then the action's ActionState.NamedValues will contain a "NoTagDetected" value that has been set to true.
        /// </summary>
        IBasicAction CreateWritePagesAction(ITagPageContents[] pages);

        /// <summary>
        /// Action factory method.  The returned action, when run, causes each of the indicated results to be cleared.  
        /// This is typically used after a Carrier has departed in order to clear the ReadTag results.
        /// </summary>
        IBasicAction CreateClearResultsAction(ClearResultsSelect clearResultsSelect);

        /// <summary>ITagRWState state publisher.  This is also a notification object</summary>
        INotificationObject<ITagRWState> StatePublisher { get; }
    }

    [Flags]
    public enum ClearResultsSelect : int
    {
        None = 0x00,
        ReadTag = 0x01,
        ReadPages = 0x02,
        WritePages = 0x04,
        All = (ReadTag | ReadPages | WritePages),
    }

    public interface ITagRWState : IEquatable<ITagRWState>
    {
        INamedValueSet NVS { get; }

        /// <summary>Gives the result information from the current/last ReadTagID action</summary>
        ITagActionInfo ReadTagIDActionInfo { get; }

        /// <summary>Gives the result information from the current/last ReadPages action</summary>
        ITagActionInfo ReadPagesActionInfo { get; }

        /// <summary>Gives the result information from the current/last WritePages action</summary>
        ITagActionInfo WritePagesActionInfo { get; }

        /// <summary>Gives the result information from the current/last action (ReadTag, ReadPages, WritePages)</summary>
        ITagActionInfo CombinedActionInfo { get; }

        IBaseState PartBaseState { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(ITagRWState rhs);
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
    public class TagRWState : ITagRWState
    {
        public TagRWState()
        {
            PartBaseState = new Modular.Part.BaseState();

            ReadTagIDActionInfo = new TagActionInfo();
            ReadPagesActionInfo = new TagActionInfo();
            WritePagesActionInfo = new TagActionInfo();
            CombinedActionInfo = new TagActionInfo();
        }

        public TagRWState(ITagRWState rhs)
        {
            SetFrom(rhs);
        }

        public TagRWState SetFrom(ITagRWState other, bool asReadOnly = false)
        {
            NVS = (asReadOnly) ? other.NVS.ConvertToReadOnly(mapNullToEmpty: false) : (other.NVS.IsNullOrEmpty() ? null : new NamedValueSet(other.NVS));

            ReadTagIDActionInfo = new TagActionInfo(other.ReadTagIDActionInfo);
            ReadPagesActionInfo = new TagActionInfo(other.ReadPagesActionInfo);
            WritePagesActionInfo = new TagActionInfo(other.WritePagesActionInfo);
            CombinedActionInfo = new TagActionInfo(other.CombinedActionInfo);

            PartBaseState = new BaseState(other.PartBaseState);

            return this;
        }

        INamedValueSet ITagRWState.NVS { get { return nvs ?? NamedValueSet.Empty; } }

        ITagActionInfo ITagRWState.ReadTagIDActionInfo { get { return ReadTagIDActionInfo; } }
        ITagActionInfo ITagRWState.ReadPagesActionInfo { get { return ReadPagesActionInfo; } }
        ITagActionInfo ITagRWState.WritePagesActionInfo { get { return WritePagesActionInfo; } }
        ITagActionInfo ITagRWState.CombinedActionInfo { get { return CombinedActionInfo; } }

        IBaseState ITagRWState.PartBaseState { get { return PartBaseState; } }

        public NamedValueSet NVS { get { return (nvs ?? (nvs = new NamedValueSet())); } set { nvs = value.MapEmptyToNull(); } }

        [DataMember(Order = 100, Name = "NVS", IsRequired = false, EmitDefaultValue = false)]
        private NamedValueSet nvs = null;

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public TagActionInfo ReadTagIDActionInfo { get; set; }

        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public TagActionInfo ReadPagesActionInfo { get; set; }

        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
        public TagActionInfo WritePagesActionInfo { get; set; }

        [DataMember(Order = 500, IsRequired = false, EmitDefaultValue = false)]
        public TagActionInfo CombinedActionInfo { get; set; }

        [DataMember(Order = 600, IsRequired = false, EmitDefaultValue = false)]
        public BaseState PartBaseState { get; set; }

        public bool Equals(ITagRWState other)
        {
            return (other != null
                    && ((NVS.IsNullOrEmpty() && other.NVS.IsNullOrEmpty()) || NVS.IsEqualTo(other.NVS, compareReadOnly: false))
                    && ReadTagIDActionInfo.Equals(other.ReadTagIDActionInfo)
                    && ReadPagesActionInfo.Equals(other.ReadPagesActionInfo)
                    && WritePagesActionInfo.Equals(other.WritePagesActionInfo)
                    && CombinedActionInfo.Equals(other.CombinedActionInfo)
                    && PartBaseState.Equals(other.PartBaseState, compareTimestamps: false)
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(ITagRWState other)
        {
            return Equals(other);
        }

        /// <summary>Supports debugging and logging.</summary>
        public override string ToString()
        {
            if (!CombinedActionInfo.IsEmpty)
                return "{0}".CheckedFormat(CombinedActionInfo);
            else
                return "{0}".CheckedFormat(PartBaseState);
        }
    }

    public interface ITagActionInfo : IEquatable<ITagActionInfo>
    {
        IActionInfo ActionInfo { get; }
        string TagID { get; }
        ITagPageContents[] PageContentsArray { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(ITagActionInfo rhs);

        bool IsEmpty { get; }

        bool NoTagDetected { get; }

        /// <summary>If the info contains a non-empty TagID then it is returned, otherwise this property returns contents based on the ActionInfo</summary>
        string DisplayTextForTagID { get; }
    }

    [DataContract(Namespace=MosaicLib.Constants.PartsLibNameSpace)]
    [KnownType(typeof(ActionInfo))]
    [KnownType(typeof(TagPageContents))]
    public class TagActionInfo : ITagActionInfo
    {
        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public IActionInfo ActionInfo { get; set; }

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public string TagID { get; set; }

        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public ITagPageContents[] PageContentsArray { get; set; }

        public TagActionInfo()
        {
            ActionInfo = Modular.Part.ActionInfo.EmptyActionInfo;
            TagID = string.Empty;
            PageContentsArray = emptyPageContentsArray;
        }

        public TagActionInfo(ITagActionInfo rhs)
        {
            SetFrom(rhs.ActionInfo, rhs.TagID, rhs.PageContentsArray);
        }

        public TagActionInfo SetFrom(IActionInfo actionInfo, string tagID, ITagPageContents[] pageArray)
        {
            ActionInfo = actionInfo ?? Modular.Part.ActionInfo.EmptyActionInfo;
            TagID = tagID ?? string.Empty;
            PageContentsArray = pageArray ?? emptyPageContentsArray;

            return this;
        }

        public bool Equals(ITagActionInfo other)
        {
            return (ActionInfo.Equals(other.ActionInfo, compareTimestamps: false)
                    && TagID == other.TagID
                    && PageContentsArray.IsEqualTo(other.PageContentsArray)
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(ITagActionInfo other)
        {
            return Equals(other);
        }

        public bool IsEmpty { get { return ActionInfo.IsEmpty && TagID.IsNullOrEmpty() && PageContentsArray.IsNullOrEmpty(); } }

        public bool NoTagDetected { get { return ActionInfo.ActionState.NamedValues["NoTagDetected"].VC.GetValue<bool>(false); } }

        /// <summary>If the info contains a non-empty TagID then it is returned, otherwise this property returns contents based on the ActionInfo</summary>
        public string DisplayTextForTagID
        {
            get
            {
                if (!TagID.IsNullOrEmpty())
                    return TagID;
                else if (IsEmpty)
                    return "[Empty]";
                else if (NoTagDetected)
                    return "[No Tag Detected]";
                else if (PageContentsArray.IsNullOrEmpty())
                    return "[{0}]".CheckedFormat(ActionInfo);
                else
                    return "[{0} {1}]".CheckedFormat(ActionInfo, String.Join(" ", PageContentsArray.Select(page => page.ToString()).ToArray()));
            }
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "[Empty]";
            else
                return "{0} Tag:{1} Pages:{2}".CheckedFormat(ActionInfo, TagID, String.Join(" ", PageContentsArray.Select(page => page.ToString()).ToArray()));
        }

        private static readonly ITagPageContents[] emptyPageContentsArray = EmptyArrayFactory<ITagPageContents>.Instance;
        private static readonly IActionState emptyActionState = new ActionStateCopy();
    }

    /// <summary>
    /// This enumeration give information about the type of RFID reader/writer that this device offers
    /// <para/>None (0x00), Reader (0x01), Writer (0x02), RFID (0x100), TIRIS (0x200),
    /// </summary>
    [Flags]
    public enum TagReaderType : int
    {
        /// <summary>This device does not support any RFID access [0x00]</summary>
        None = 0x00,

        /// <summary>This device can be used to read tags, generally by reading a selected range of pages and concatinating their contents. [0x01]</summary>
        Reader = 0x01,

        /// <summary>This device can be used to write tag contents in pages. [0x02]</summary>
        Writer = 0x02,

        /// <summary>This device makes use of RFID communications with the Tag. [0x100]</summary>
        RFID = 0x100,

        /// <summary>This device is designed to work with TIRIS tags. [0x200]</summary>
        TIRIS = 0x200,
    }


    public interface IReadTagAction : Modular.Action.IClientFacetWithResult<string> { }
    public interface IReadPagesAction : Modular.Action.IClientFacetWithResult<ITagPageContents[]> { }

    public class ReadTagActionImpl : ActionImplBase<NullObj, string>, IReadTagAction
    {
        public ReadTagActionImpl(ActionQueue actionQ, ActionMethodDelegateActionArgStrResult<NullObj, string> methodDelegate, string mesg, ActionLogging actionLoggingReference)
            : base(actionQ, null, false, methodDelegate, new ActionLogging(mesg, actionLoggingReference))
        { }
    }

    public class ReadPagesActionImpl : ActionImplBase<NullObj, ITagPageContents[]>, IReadPagesAction
    {
        public ReadPagesActionImpl(ActionQueue actionQ, ActionMethodDelegateActionArgStrResult<NullObj, ITagPageContents[]> methodDelegate, string mesg, ActionLogging actionLoggingReference)
            : base(actionQ, null, false, methodDelegate, new ActionLogging(mesg, actionLoggingReference))
        { }
    }

    public interface ITagPageContents
    {
        int PageIndex { get; }
        byte[] ByteArray { get; }

        bool Equals(object rhsAsObject);

        string ToString(TagRWPageContentsStringFormat fmtToUse);
    }

    public enum TagRWPageContentsStringFormat
    {
        HexAndAscii = 0,
        DecimalPageSpaceBytesInHex,
        DecimalPageSpaceQuotedBytesInHex,
        Default = HexAndAscii,
    }

    /// <summary>
    /// Defines the technology that the tag read/write engine is using.  The technology normally determines a number of details about how the tags operate.
    /// <para/>None=0, TIRIS
    /// </summary>
    public enum ReaderType : int
    {
        /// <summary>Value returned when there is a reader installed but its type is not otherwise recognized.</summary>
        Unknown = -1,
        /// <summary>Value to use when there is no Reader installed</summary>
        None = 0,
        /// <summary>Devices that follow the TI HDX transponder (used to be called TIRIS) with related signaling, protocol, and operation.</summary>
        TIRIS = 1,
    }

    /// <summary>
    /// Defines the type of driver to use for this tag reader
    /// </summary>
    public enum DriverType : int
    {
        /// <summary>Value to use when there is no Reader installed (0)</summary>
        None = 0,

        /// <summary>Hermos ASCII protocol device. (1, same as BrooksASCII)</summary>
        HermosASCII = 1,

        /// <summary>Brooks ASCII protocol device. (1, same as HermosASCII)</summary>
        BrooksASCII = 1,

        /// <summary>Omron V640 family (mostly)ASCII protocol device. (2)</summary>
        OmronV640 = 2,
    }

    /// <summary>
    /// Defines the TagContent decode mode to use.
    /// <para/>AutoTrimAscii (default), Ascii, Binary.
    /// </summary>
    public enum TagContentDecodeMode : int
    {
        /// <summary>
        /// Configured tag pages are required to contain Ascii characters and may include leading or trailing space or null characters.
        /// Leading and trailing spaces and/or nulls will be removed before reporting the obtained TagID.
        /// If the tag pages contains non-Ascii contents then the TagID read fails.
        /// </summary>
        AutoTrimAscii = 0,

        /// <summary>
        /// Configured tag pages are required to contain Ascii characters and may include embedded whitespace.
        /// Leading and Trailing spaces will be retained.  Trailing null characters will be removed.
        /// If the tag pages contains non-Ascii contents then the TagID read fails.
        /// </summary>
        Ascii,

        /// <summary>
        /// Configured tag pages will be obtained and returned without additional confirmation or trimming.
        /// </summary>
        Binary,
    }

    public class TagRWPartConfig
    {
        public string PartID { get; set; }

        public DriverType DriverType
        {
            get { return driverType; }
            set
            {
                driverType = value;
                switch (ReaderType)
                {
                    case ReaderType.TIRIS:
                        PageDataSize = 8;
                        NumPages = 17;
                        break;
                    default:
                        break;
                }
            }
        }
        private DriverType driverType = DriverType.None;

        public ReaderType ReaderType
        {
            get
            {
                switch (DriverType)
                {
                    case DriverType.None: return ReaderType.None;
                    case DriverType.HermosASCII: return ReaderType.TIRIS;
                    default: return ReaderType.Unknown;
                }
            }
        }

        [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
        public int NumPages { get; set; }

        [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
        public int PageDataSize { get; set; }

        ///<summary>In bytes</summary>
        [ConfigItem(IsOptional = true)]
        public int DefaultTagIDStartOffset { get; set; }

        ///<summary>In bytes</summary>
        [ConfigItem(IsOptional = true)]
        public int DefaultTagIDSize { get; set; }

        [ConfigItem(IsOptional = true)]
        public TagContentDecodeMode DefaultTagDecodeMode { get; set; }

        public TagRWPartConfig()
        {
            DefaultTagIDSize = 2;
        }

        public TagRWPartConfig(TagRWPartConfig rhs)
        {
            PartID = rhs.PartID;
            DriverType = rhs.DriverType;
            NumPages = rhs.NumPages;
            PageDataSize = rhs.PageDataSize;
            DefaultTagIDStartOffset = rhs.DefaultTagIDStartOffset;
            DefaultTagIDSize = rhs.DefaultTagIDSize;
            DefaultTagDecodeMode = rhs.DefaultTagDecodeMode;
        }
    }

    [DataContract(Namespace = MosaicLib.Constants.PartsLibNameSpace)]
    public class TagPageContents : ITagPageContents
    {
        [DataMember(Order = 100)]
        public int PageIndex { get; set; }

        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public byte[] ByteArray { get; set; }

        public override bool Equals(object rhsAsObject)
        {
            ITagPageContents rhs = rhsAsObject as ITagPageContents;
            if (rhs == null)
                return false;

            return (PageIndex == rhs.PageIndex && ByteArray.IsEqualTo(rhs.ByteArray));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return ToString(TagRWPageContentsStringFormat.Default);
        }

        public string ToString(TagRWPageContentsStringFormat fmtToUse)
        {
            switch (fmtToUse)
            {
                case TagRWPageContentsStringFormat.DecimalPageSpaceBytesInHex:
                    // version used by some drivers
                    return Fcns.CheckedFormat("{0:d2} {1}", (PageIndex + 1), ByteArrayTranscoders.HexStringTranscoderNoPadding.Encode(ByteArray));

                case TagRWPageContentsStringFormat.DecimalPageSpaceQuotedBytesInHex:
                    // version used by some drivers
                    return Fcns.CheckedFormat("{0:d2} '{1}'", (PageIndex + 1), ByteArrayTranscoders.HexStringTranscoderNoPadding.Encode(ByteArray));

                case TagRWPageContentsStringFormat.HexAndAscii:
                default:
                    // version used for screen display and logging.
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.CheckedAppendFormat("{0:d2}: {1} [", (PageIndex + 1), ByteArrayTranscoders.HexStringTranscoder.Encode(ByteArray));

                        foreach (byte b in ByteArray)
                        {
                            char c = unchecked((char)b);

                            if (Char.IsLetterOrDigit(c) || Char.IsPunctuation(c) || Char.IsSymbol(c))
                                sb.Append(c);
                            else
                                sb.Append('.');
                        }

                        sb.Append(']');

                        return sb.ToString();
                    }
            }
        }
    }

    public static partial class ExtensionMethods
    {
        public static bool IsSet(this TagReaderType value, TagReaderType test) { return value.Matches(test, test); }
        public static bool Matches(this TagReaderType testValue, TagReaderType mask, TagReaderType expectedValue) { return ((testValue & mask) == expectedValue); }

        public static string DecodePageDataAndVerifyTagID(this byte[] rawPageData, TagContentDecodeMode tagContentDecodeMode, out string tagIDOut)
        {
            tagIDOut = ByteArrayTranscoders.ByteStringTranscoder.Encode(rawPageData);

            switch (tagContentDecodeMode)
            {
                case TagContentDecodeMode.AutoTrimAscii:
                    tagIDOut = tagIDOut.Trim(' ', '\0');
                    if (!tagIDOut.IsBasicAscii())
                        return "read TagID contains non-Ascii contents";
                    break;

                case TagContentDecodeMode.Ascii:
                    tagIDOut = tagIDOut.TrimEnd('\0');
                    if (!tagIDOut.IsBasicAscii())
                        return "read TagID contains non-Ascii contents";
                    break;

                case TagContentDecodeMode.Binary:
                    break;

                default:
                    return "Unsupported TagContentDecodeMode:{0}".CheckedFormat(tagContentDecodeMode);
            }

            return string.Empty;
        }
    }
}

//-------------------------------------------------------------------
