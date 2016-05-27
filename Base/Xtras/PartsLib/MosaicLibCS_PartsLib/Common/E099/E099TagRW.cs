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
		IReadTagAction CreateReadTagAction();
        IReadTagAction CreateReadTagAction(int startPageIdx, int numPages, TagContentDecodeMode tagContentDecodeMode);
        IReadPagesAction CreateReadPagesAction(int startPageIdx, int numPages);
        IBasicAction CreateWritePageAction(ITagPageContents [] pages);
    }

    public interface IReadTagAction : Modular.Action.IClientFacetWithResult<string> {}
    public interface IReadPagesAction : Modular.Action.IClientFacetWithResult<ITagPageContents []> {}

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
	}

    /// <summary>
    /// Defines the TagContent decode mode to use.
    /// <para/>AutoTrimAscii (default), Ascii, Binary.
    /// </summary>
    public enum TagContentDecodeMode : int
    {
        AutoTrimAscii = 0,
        Ascii,
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
}

//-------------------------------------------------------------------
