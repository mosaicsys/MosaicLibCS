//-------------------------------------------------------------------
/*! @file E099TagRW.cs
 *  @brief Provides common definitions used to make use of drivers for generic E099 style tag read/write hardware (such as a TIRIS tag).
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2016 Mosaic Systems Inc., All rights reserved
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
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Text;
using MosaicLib.Utils;
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
        TagReaderType TagReaderType { get; }

		IReadTagAction CreateReadTagAction();
        IReadTagAction CreateReadTagAction(int startPageIdx, int numPages, TagContentDecodeMode tagContentDecodeMode);
        IReadPagesAction CreateReadPagesAction(int startPageIdx, int numPages);
        /// <summary>
        /// Action factory method.  The returned action, when run, causes each of the indicates pages to be written, one at a time,
        /// until all writes are successful or one of them fails.  If the first failure was due to there being NoTagFound then the action's ActionState.NamedValues will contain a "NoTagDetected" value that has been set to true.
        /// </summary>
        IBasicAction CreateWritePagesAction(ITagPageContents [] pages);
    }

    /// <summary>
    /// This enumeration give information about the type of RFID reader/writer that this device offers
    /// <para/>None, Reader, Writer, RFID, TIRIS,
    /// </summary>
    [Flags]
    public enum TagReaderType : int
    {
        /// <summary>This device does not support any RFID access</summary>
        None = 0x0000,

        /// <summary>This device can be used to read tags, generally by reading a selected range of pages and concatinating their contents.</summary>
        Reader = 0x0001,

        /// <summary>This device can be used to write tag contents in pages.</summary>
        Writer = 0x0002,

        /// <summary>This device makes use of RFID communications with the Tag.</summary>
        RFID = 0x0100,

        /// <summary>This device is designed to work with TIRIS tags.</summary>
        TIRIS = 0x0200,
    }


    public interface IReadTagAction : Modular.Action.IClientFacetWithResult<string> {}
    public interface IReadPagesAction : Modular.Action.IClientFacetWithResult<ITagPageContents []> {}

    public class ReadTagActionImpl : ActionImplBase<NullObj, string>, IReadTagAction
    {
        public ReadTagActionImpl(ActionQueue actionQ, ActionMethodDelegateActionArgStrResult<NullObj, string> methodDelegate, string mesg, ActionLogging actionLoggingReference)
            : base(actionQ, null, false, methodDelegate, new ActionLogging(mesg, actionLoggingReference))
        {}
    }

    public class ReadPagesActionImpl : ActionImplBase<NullObj, ITagPageContents []>, IReadPagesAction
    {
        public ReadPagesActionImpl(ActionQueue actionQ, ActionMethodDelegateActionArgStrResult<NullObj, ITagPageContents[]> methodDelegate, string mesg, ActionLogging actionLoggingReference)
            : base(actionQ, null, false, methodDelegate, new ActionLogging(mesg, actionLoggingReference))
        { }
    }

    public interface ITagPageContents
    {
        int PageIndex { get; }
        byte [] ByteArray { get; }

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
        /// <summary>Value to use when there is no Reader installed</summary>
		None = 0,

        /// <summary>Hermos ASCII protocol device.</summary>
		HermosASCII = 1,

        /// <summary>Omron V640 family (mostly)ASCII protocol device.</summary>
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
		[ConfigItem(IsOptional=true)]
		public int DefaultTagIDStartOffset { get; set; }

		///<summary>In bytes</summary>
        [ConfigItem(IsOptional=true)]
		public int DefaultTagIDSize	{ get; set; }

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

    public class TagPageContents : ITagPageContents
    {
        public int PageIndex { get; set; }
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
