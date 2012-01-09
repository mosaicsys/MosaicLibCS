//-------------------------------------------------------------------
/*! @file String.cs
 *  @brief This file contains a number of string related helper methods
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
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

namespace MosaicLib.Utils
{
	using System;
    using System.Text;
	using System.Collections.Generic;

	#region Unassociated Functions

    /// <summary>
    /// Fcns class is essentially a namespace for series of static helper methods
    /// </summary>
	public static partial class Fcns
	{
		#region String value mapping functions

		/// <summary>Maps the given string value to the empty string if it is null</summary>
		/// <param name="s">The string to test for null and optionally map</param>
		/// <returns>The given string s if it was not null or the empty string if it was.</returns>
		public static string MapNullToEmpty(string s) { return ((s == null) ? string.Empty : s); }

		/// <summary>Maps the given string value to the empty string if it is null</summary>
		/// <param name="s">The string to test for null or empty and to optionally map</param>
		/// <param name="mappedS">The string value to return when the reference string s is null or empty</param>
		/// <returns>The given string s if it was not null or the empty string if it was.</returns>
		public static string MapNullOrEmptyTo(string s, string mappedS) { return (string.IsNullOrEmpty(s) ? mappedS : s); }

        /// <summary>Maps the given boolean value to either "1" or "0"</summary>
        public static string MapToString(bool value) { return (value ? "1" : "0"); }

        /// <summary>Maps the given boolean value to either given trueStr or given falseStr</summary>
        public static string MapToString(bool value, string trueStr, string falseStr) { return (value ? trueStr : falseStr); }

        /// <summary>Maps the given boolean value to either given trueStr or given falseStr</summary>
        public static int MapToInt(bool value) { return (value ? 1 : 0); }

		#endregion

		#region static CheckedFormat methods

		/// <summary>Invokes System.String.Format with the given args within a try/catch pattern.</summary>
		public static string CheckedFormat(string fmt, object arg0)
		{
            try
            {
                return System.String.Format(fmt, arg0);
            }
            catch (System.FormatException ex)
            {
                return System.String.Format("Format1('{0}') threw FormatException '{1}'", fmt, ex.Message);
            }
            catch (System.ArgumentNullException ex)
            {
                return System.String.Format("Format1('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
            }
            catch (System.Exception ex)
            {
                return System.String.Format("Format1('{0}') threw Exception '{1}'", fmt, ex.Message);
            }
		}

		/// <summary>Invokes System.String.Format with the given args within a try/catch pattern.</summary>
		public static string CheckedFormat(string fmt, object arg0, object arg1)
		{
			try
			{
				return System.String.Format(fmt, arg0, arg1);
			}
			catch (System.FormatException ex)
			{
				return System.String.Format("Format2('{0}') threw FormatException '{1}'", fmt, ex.Message);
			}
			catch (System.ArgumentNullException ex)
			{
				return System.String.Format("Format2('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
			}
            catch (System.Exception ex)
            {
                return System.String.Format("Format2('{0}') threw Exception '{1}'", fmt, ex.Message);
            }
        }

		/// <summary>Invokes System.String.Format with the given args within a try/catch pattern.</summary>
		public static string CheckedFormat(string fmt, object arg0, object arg1, object arg2)
		{
			try
			{
				return System.String.Format(fmt, arg0, arg1, arg2);
			}
			catch (System.FormatException ex)
			{
				return System.String.Format("Format3('{0}') threw FormatException '{1}'", fmt, ex.Message);
			}
			catch (System.ArgumentNullException ex)
			{
				return System.String.Format("Format3('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
			}
            catch (System.Exception ex)
            {
                return System.String.Format("Format3('{0}') threw Exception '{1}'", fmt, ex.Message);
            }
        }

		/// <summary>Invokes System.String.Format with the given args within a try/catch pattern.</summary>
		public static string CheckedFormat(string fmt, params object [] args)
		{
			try
			{
				return System.String.Format(fmt, args);
			}
			catch (System.FormatException ex)
			{
				return System.String.Format("FormatN('{0}') threw FormatException '{1}'", fmt, ex.Message);
			}
			catch (System.ArgumentNullException ex)
			{
				return System.String.Format("FormatN('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
			}
            catch (System.Exception ex)
            {
                return System.String.Format("FormatN('{0}') threw Exception '{1}'", fmt, ex.Message);
            }
        }


        /// <summary>Invokes System.String.Format with the given args within a try/catch pattern.</summary>
        public static string CheckedFormat(IFormatProvider provider, string fmt, params object[] args)
        {
            try
            {
                return System.String.Format(provider, fmt, args);
            }
            catch (System.FormatException ex)
            {
                return System.String.Format(provider, "FormatPN('{0}') threw FormatException '{1}'", fmt, ex.Message);
            }
            catch (System.ArgumentNullException ex)
            {
                return System.String.Format(provider, "FormatPN('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
            }
            catch (System.Exception ex)
            {
                return System.String.Format(provider, "FormatPN('{0}') threw Exception '{1}'", fmt, ex.Message);
            }
        }

        #endregion
    }

	#endregion

#if (false) // disable until we switch to 3.5
    #region Extension Functions

    /// <summary>
    /// This class contains a set of extension methods.  At present this primarily adds variants of the CheckedFormat methods above to be used directly with Strings and StringBuilder
    /// </summary>
    public static partial class ExtensionMethods
    {
        #region static System.Text.StringBuilder.CheckedAppendFormat extension methods

        /// <summary>Invokes System.Text.StringBuilder.AppendFormat with the given args within a try/catch pattern.</summary>
        public static StringBuilder CheckedAppendFormat(this StringBuilder sb, string fmt, object arg0)
        {
            try
            {
                sb.AppendFormat(fmt, arg0);
            }
            catch (System.FormatException ex)
            {
                sb.AppendFormat("Format1('{0}') threw FormatException '{1}'", fmt, ex.Message);
            }
            catch (System.ArgumentNullException ex)
            {
                sb.AppendFormat("Format1('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
            }
            catch (System.Exception ex)
            {
                sb.AppendFormat("Format1('{0}') threw Exception '{1}'", fmt, ex.Message);
            }

            return sb;
        }

        /// <summary>Invokes System.Text.StringBuilder.AppendFormat with the given args within a try/catch pattern.</summary>
        public static StringBuilder CheckedAppendFormat(this StringBuilder sb, string fmt, object arg0, object arg1)
        {
            try
            {
                sb.AppendFormat(fmt, arg0, arg1);
            }
            catch (System.FormatException ex)
            {
                sb.AppendFormat("Format2('{0}') threw FormatException '{1}'", fmt, ex.Message);
            }
            catch (System.ArgumentNullException ex)
            {
                sb.AppendFormat("Format2('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
            }
            catch (System.Exception ex)
            {
                sb.AppendFormat("Format2('{0}') threw Exception '{1}'", fmt, ex.Message);
            }

            return sb;
        }

        /// <summary>Invokes System.Text.StringBuilder.AppendFormat with the given args within a try/catch pattern.</summary>
        public static StringBuilder CheckedAppendFormat(this StringBuilder sb, string fmt, object arg0, object arg1, object arg2)
        {
            try
            {
                sb.AppendFormat(fmt, arg0, arg1, arg2);
            }
            catch (System.FormatException ex)
            {
                sb.AppendFormat("Format3('{0}') threw FormatException '{1}'", fmt, ex.Message);
            }
            catch (System.ArgumentNullException ex)
            {
                sb.AppendFormat("Format3('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
            }
            catch (System.Exception ex)
            {
                sb.AppendFormat("Format3('{0}') threw Exception '{1}'", fmt, ex.Message);
            }

            return sb;
        }

        /// <summary>Invokes System.Text.StringBuilder.AppendFormat with the given args within a try/catch pattern.</summary>
        public static StringBuilder CheckedAppendFormat(this StringBuilder sb, string fmt, params object[] args)
        {
            try
            {
                sb.AppendFormat(fmt, args);
            }
            catch (System.FormatException ex)
            {
                sb.AppendFormat("FormatN('{0}') threw FormatException '{1}'", fmt, ex.Message);
            }
            catch (System.ArgumentNullException ex)
            {
                sb.AppendFormat("FormatN('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
            }
            catch (System.Exception ex)
            {
                sb.AppendFormat("FormatN('{0}') threw Exception '{1}'", fmt, ex.Message);
            }

            return sb;
        }

        /// <summary>Invokes System.Text.StringBuilder.AppendFormat with the given args within a try/catch pattern.</summary>
        public static StringBuilder CheckedAppendFormat(this StringBuilder sb, IFormatProvider provider, string fmt, params object[] args)
        {
            try
            {
                sb.AppendFormat(provider, fmt, args);
            }
            catch (System.FormatException ex)
            {
                sb.AppendFormat("FormatPN('{0}') threw FormatException '{1}'", fmt, ex.Message);
            }
            catch (System.ArgumentNullException ex)
            {
                sb.AppendFormat("FormatPN('{0}') threw ArgumentNullException '{1}'", fmt, ex.Message);
            }
            catch (System.Exception ex)
            {
                sb.AppendFormat("FormatPN('{0}') threw Exception '{1}'", fmt, ex.Message);
            }

            return sb;
        }

        #endregion
    }

    #endregion

#endif

    #region Enum

    /// <summary>
    /// Enum class is essentially a namespace for series of static Enum related helper methods
    /// </summary>
    public static partial class Enum
	{
		#region TryParse

		/// <summary>Helper function to parse a string s as an enum of type EnumT</summary>
		/// <typeparam name="EnumT">The specific Enum type to parse.  This must be an System.Enum</typeparam>
		/// <param name="s">The string to parse</param>
		/// <returns>parsed EnumT value on success, or default(EnumT) on failure</returns>
		/// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
		public static EnumT TryParse<EnumT>(string s) where EnumT : struct
		{
			return TryParse<EnumT>(s, default(EnumT));
		}

		/// <summary>Helper function to parse a string s as an enum of type EnumT</summary>
		/// <typeparam name="EnumT">The specific Enum type to parse.  This must be an System.Enum</typeparam>
		/// <param name="s">The string to parse</param>
		/// <param name="parseFailedResult">Defines the EnumT value that will be returned if the parse fails</param>
		/// <returns>parsed EnumT value on success, or parseFailedResult on failure</returns>
		/// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
		public static EnumT TryParse<EnumT>(string s, EnumT parseFailedResult) where EnumT : struct
		{
			EnumT result;

			TryParse<EnumT>(s, out result, parseFailedResult);

			return result;
		}

		/// <summary>Helper function to parse a string s as an enum of type EnumT</summary>
		/// <typeparam name="EnumT">The specific Enum type to parse.  This must be an System.Enum</typeparam>
		/// <param name="s">The string to parse</param>
		/// <param name="result">Assigned to parsed EnumT value on success, or to default(EnumT) on failure.</param>
		/// <returns>True if the Parse was successful, false otherwise</returns>
		/// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
		public static bool TryParse<EnumT>(string s, out EnumT result) where EnumT : struct
		{
			return TryParse<EnumT>(s, out result, default(EnumT));
		}

		/// <summary>Helper function to parse a string s as an enum of type EnumT</summary>
		/// <typeparam name="EnumT">The specific Enum type to parse.  This must be an System.Enum</typeparam>
		/// <param name="s">The string to parse</param>
		/// <param name="result">Assigned to the Parsed value on success or to parseFailedResult on failure</param>
		/// <param name="parseFailedResult">Defines the EnumT value that will be assigned to the result if the parse fails</param>
		/// <returns>True if the Parse was successful, false otherwise</returns>
		/// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
		public static bool TryParse<EnumT>(string s, out EnumT result, EnumT parseFailedResult)
		{
			Type enumT = typeof(EnumT);

			if (!enumT.IsEnum)
				throw new System.InvalidOperationException(Fcns.CheckedFormat("Type:'{0}' is not usable with Utils.Enum.TryParse.  It must be a System.Enum", typeof(EnumT).ToString()));

			if (string.IsNullOrEmpty(s))
			{
				result = parseFailedResult;
				return false;
			}

			try 
			{
				result = (EnumT) System.Enum.Parse(typeof(EnumT), s);
				return true;
			}
			catch (InvalidCastException)
			{
				result = parseFailedResult;
				return false;
			}
		}

		#endregion
	}

	#endregion

	#region Dates

    /// <summary>
    /// Dates class is essentially a namespace for series of static Date related helper methods
    /// </summary>
    public static class Dates
	{
		// methods used to provide timestamps for LogMessages

		/// <summary>Enum to define the supported formats for converting DataTime values to a string.</summary>
		public enum DateTimeFormat
		{
			/// <summary>Enum value when format should look like 1970-01-01 00:00:00.000</summary>
			LogDefault = 0,

			/// <summary>Enum value when format should look like 19700101_000000.000</summary>
			ShortWithMSec,
		}

		/// <summary>Converts the given DateTime value to a string using the given summary desired format</summary>
		/// <param name="dt">Specifies the DateTime value to convert</param>
		/// <param name="dtFormat">Specifies the desired format from the set of supported enum values.</param>
		/// <returns>The DateTime converted to a string based on the desired format.</returns>

		public static string CvtToString(ref DateTime dt, DateTimeFormat dtFormat)
		{
			string result = string.Empty;

			switch (dtFormat)
			{
				default:
				case DateTimeFormat.LogDefault:
					result = Fcns.CheckedFormat("{0}-{1}-{2} {3}:{4}:{5}.{6}", 
													dt.Year.ToString("D4"), dt.Month.ToString("D2"), dt.Day.ToString("D2"),
													dt.Hour.ToString("D2"), dt.Minute.ToString("D2"), dt.Second.ToString("D2"), 
													dt.Millisecond.ToString("D3")); 
					break;
				case DateTimeFormat.ShortWithMSec:
					result = Fcns.CheckedFormat("{0}{1}{2}_{3}{4}{5}.{6}", 
													dt.Year.ToString("D4"), dt.Month.ToString("D2"), dt.Day.ToString("D2"),
													dt.Hour.ToString("D2"), dt.Minute.ToString("D2"), dt.Second.ToString("D2"), 
													dt.Millisecond.ToString("D3"));
					break;
			}

			return result;
		}
	}

	#endregion

	#region Byte Array Transcoders

    /// <summary>
    /// This interface defines a set of methods that are used to directly transcode between byte arrayes an strings.  
    /// Objects that support this interface must do so in a reenterant manner so that a single transcoder object can be concurrently used by multiple threads.
    /// </summary>
	public interface IByteArrayTranscoder
	{
		/// <summary>Encodes the bytes in the given source buffer and returns the resulting encoded string.</summary>
		/// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
		/// <returns>the encoded string</returns>
		string Encode(byte [] sourceBuffer);

		/// <summary>Encodes the given byte range from the given source buffer and returns the resulting encoded string</summary>
		/// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
		/// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
		/// <param name="length">specifies the number of bytes to encode from the source buffer</param>
		/// <returns>the encoded string</returns>
		string Encode(byte [] sourceBuffer, int startOffset, int length);

		/// <summary>Encodes the given byte range from the given source buffer, sets the codedStr to the resulting encoded string and returns true if the operation was successfull.</summary>
		/// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
		/// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
		/// <param name="length">specifies the number of bytes to encode from the source buffer</param>
		/// <param name="codedStr">the output string parameter that will be set to the encoded string</param>
		/// <returns>true if the operation was successfull, false otherwise.  The contents of the resulting encoded string are not defined if the return value is false.</returns>
		bool Encode(byte [] sourceBuffer, int startOffset, int length, out string codedStr);

		/// <summary>Decodes the given encoded string and returns the resulting decoded byte array.</summary>
		/// <param name="codedStr">the string containing the encoded characters.</param>
		/// <returns>the decoded byte array</returns>
		byte [] Decode(string codedStr);

		/// <summary>Decodes the given encoded, sets the decodedBuffer to the resulting decoded byte array and returns true if the operation was successfull.</summary>
		/// <param name="codedStr">the string containing the encoded characters.</param>
		/// <param name="decodedBuffer">the output byte array variable that will be set to a new array containing the decoded bytes</param>
		/// <returns>true if the operation was successfull,false otherwise.  The contents of the resulting decoded buffer are not defined if the return value is false</returns>
		bool Decode(string codedStr, out byte [] decodedBuffer);
	}

    /// <summary>This static class (sub-namespace) contains static properties to get access to commonly used transcoder singleton instances</summary>
	public static class ByteArrayTranscoders
	{
		private static IByteArrayTranscoder byteArrayStringTranscoder = new ByteArrayStringTranscoder();
		private static IByteArrayTranscoder base64UrlTranscoder = new Base64UrlTranscoder();

        /// <summary>Returns a Transcoder that converts directly between byte arrays and strings of the identical character (bit patterns).  Encode widens each byte, Decode truncates the upper bits in each character to give the resulting byte.</summary>
		public static IByteArrayTranscoder ByteStringTranscoder { get { return byteArrayStringTranscoder; } }

        /// <summary>Returns a Transcoder that converst between binary byte arrays and Base64 coded strings</summary>
		public static IByteArrayTranscoder Base64UrlTranscoder { get { return base64UrlTranscoder; } }
	}

    /// <summary>Base class for some transcoders.  Provides base implementations for most of the IByteArrayTranscoder methods</summary>
	public abstract class ByteArrayTranscoderBase : IByteArrayTranscoder
	{
		protected readonly static byte [] emptyArray = new byte [0];

		public string Encode(byte [] buffer) 
		{ 
			if (buffer == null) 
				buffer = emptyArray; 
			return Encode(buffer, 0, buffer.Length); 
		}

		public string Encode(byte [] buffer, int startOffset, int length)
		{
			string base64str;
			Encode(buffer, startOffset, length, out base64str);
			return base64str;
		}

		public abstract bool Encode(byte [] sourceBuffer, int startOffset, int length, out string codedStr);

		public byte [] Decode(string codedStr)
		{
			byte [] outArray = null;
			Decode(codedStr, out outArray);
			return outArray;
		}

		public abstract bool Decode(string codedStr, out byte [] decodedBuffer);
	}

	/// <summary>Converts directly between byte arrays and strings of the identical characters.</summary>
	public class ByteArrayStringTranscoder : ByteArrayTranscoderBase
	{
		public override bool Encode(byte [] sourceBuffer, int startOffset, int length, out string codedStr)
		{
			codedStr = string.Empty;

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			if (sourceBuffer == null)
				sourceBuffer = emptyArray;

			int sbLength = sourceBuffer.Length;
			if (startOffset >= sbLength)
				return false;

			int endOffset = startOffset + length;
			bool success = true;
			if (endOffset > sbLength)
			{
				endOffset = sbLength;
				success = false;
			}

			sb.EnsureCapacity(endOffset - startOffset);

			for (int idx = startOffset; idx < endOffset; idx++)
				sb.Append((char) sourceBuffer [idx]);

			codedStr = sb.ToString();
			return success;
		}

		public override bool Decode(string codedStr, out byte [] decodedBuffer)
		{
			int len = (codedStr != null ? codedStr.Length : 0);
			decodedBuffer = new byte [len];

			bool success = true;
			for (int idx = 0; idx < len; idx++)
			{
				char c = codedStr [idx];
				if (c < 0 || c > 255)
					success = false;
				unchecked { decodedBuffer [idx] = (byte) c; }
			}

			return success;
		}
	}

	/// <summary>This static class provides means to encode and decode between byte arrays and base64 strings.</summary>
	/// <remarks>
	/// See http://tools.ietf.org/pdf/rfc4648 for details on encoding format.
	/// </remarks>
	public class Base64UrlTranscoder : ByteArrayTranscoderBase
	{
		private static readonly byte [] mapByteToChar = (new ByteArrayStringTranscoder()).Decode("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");
		private char EncodeByte(int bits) { return ((char) mapByteToChar [bits & 0x3f]); }
		private bool DecodeChar(char c, out int bits)
		{
			if (c >= 'A' && c <= 'Z')
				bits = c - 'A' + 0;
			else if (c >= 'a' && c <= 'z')
				bits = c - 'a' + 26;
			else if (c >= '0' && c <= '9')
				bits = c - '0' + 52;
			else if (c == '-')
				bits = 62;
			else if (c == '_')
				bits = 63;
			else
			{
				bits = 0;
				return false;
			}

			return true;
		}

		public override bool Encode(byte [] sourceBuffer, int startOffset, int length, out string codedStr)
		{
			int endIdx = startOffset + length;
			if (sourceBuffer == null || endIdx > sourceBuffer.Length)
			{
				codedStr = string.Empty;
				return false;
			}

			int estimatedEncodedLength = (((length + 2) * 4) / 3) + 1;
			System.Text.StringBuilder sb = new System.Text.StringBuilder(estimatedEncodedLength);

			int inBytes = 0, blockSize = 0;
			char c1, c2, c3, c4;

			for (int blockIdx = startOffset; blockIdx < endIdx; )
			{
				int byteIdx = blockIdx;
				inBytes = (((int) sourceBuffer [byteIdx++] << 16)
							| (((byteIdx < endIdx) ? (int) sourceBuffer [byteIdx++] : 0) << 8)
							| (((byteIdx < endIdx) ? (int) sourceBuffer [byteIdx++] : 0) << 0)
							);

				c1 = EncodeByte(inBytes >> 18);
				c2 = EncodeByte(inBytes >> 12);
				c3 = EncodeByte(inBytes >> 6);
				c4 = EncodeByte(inBytes);

				blockSize = (byteIdx - blockIdx);
				switch (blockSize)
				{
					case 3: { sb.Append(c1); sb.Append(c2); sb.Append(c3); sb.Append(c4); } break;
					case 2: { sb.Append(c1); sb.Append(c2); sb.Append(c3); sb.Append('='); } break;
					case 1: { sb.Append(c1); sb.Append(c2); sb.Append('='); sb.Append('='); } break;
					default: break;
				}

				blockIdx = byteIdx;
			}

			codedStr = sb.ToString();
			return true;
		}

		public override bool Decode(string codedStr, out byte [] decodedBuffer)
		{
			decodedBuffer = emptyArray;
			if (String.IsNullOrEmpty(codedStr))
				return false;

			int estimatedDecodeLength = ((codedStr.Length * 3) / 4);
			List<byte> arrayBuilder = new List<byte>(estimatedDecodeLength);

			StringScanner strScan = new StringScanner(codedStr);

			bool success = true;
			bool done = false;

			while (success && !done)
			{
				int numChars = strScan.NumChars;
				if (numChars <= 0)
					break;

				if (numChars < 4)
					success = false;

                char c1 = strScan++.Char, c2 = strScan++.Char, c3 = strScan++.Char, c4 = strScan++.Char;

				int bits1, bits2, bits3, bits4;
				byte byte1, byte2, byte3;

				// decode the first two chars to generate either 8 or 12 bits in the bitsAll pattern, extract and add 1 output byte
				success &= DecodeChar(c1, out bits1);
				success &= DecodeChar(c2, out bits2);
				int bitsAll = (bits1 << 18 | bits2 << 12);
				unchecked { byte1 = (byte) ((bitsAll >> 16) & 0x0ff); }
				arrayBuilder.Add(byte1);

				// determine if there is 2, 1 or 0 more bytes
				if (c4 != '=')
				{
					// 2 more bytes: decode 2 more chars, or them into the all bits, extract and add 2 more output bytes
					success &= DecodeChar(c3, out bits3);
					success &= DecodeChar(c4, out bits4);
					bitsAll |= (bits3 << 6 | bits4);
					unchecked { byte2 = (byte) ((bitsAll >> 8) & 0x0ff); byte3 = (byte) (bitsAll & 0x0ff); }
					arrayBuilder.Add(byte2);
					arrayBuilder.Add(byte3);
				}
				else if (c3 != '=')
				{
					// 1 more bytes: decode 1 more char, or it into the all bits, extract and add 1 more output byte, verify unused bits in allBits are zero
					success &= (DecodeChar(c3, out bits3));
					bitsAll |= (bits3 << 6);
					unchecked { byte2 = (byte) ((bitsAll >> 8) & 0x0ff); }
					arrayBuilder.Add(byte2);
					success &= ((bitsAll & 0x0ff) == 0);
					done = true;
				}
				else
				{
					// 0 more bytes: verify unused bits in allBits are zero
					success &= ((bitsAll & 0x0ffff) == 0);
					done = true;
				}
			}

			decodedBuffer = arrayBuilder.ToArray();
			return success;
		}
	}

	#endregion
}

//-------------------------------------------------------------------
