//-------------------------------------------------------------------
/*! @file ByteArrayTranscoders.cs
 *  @brief This file contains a number of string related helper methods
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
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
using System.Text;
using System.Collections.Generic;

namespace MosaicLib.Utils
{
	#region Byte Array Transcoders

    /// <summary>
    /// This interface defines a set of methods that are used to directly transcode between byte arrays an strings.  
    /// Objects that support this interface must do so in a reentrant manner so that a single transcoder object can be concurrently used by multiple threads.
    /// </summary>
	public interface IByteArrayTranscoder
	{
		/// <summary>Encodes the bytes in the given source buffer (optinally null) and returns the resulting encoded string.</summary>
		/// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
		/// <returns>the encoded string</returns>
		string Encode(byte [] sourceBuffer);

		/// <summary>Encodes the given byte range from the given source buffer and returns the resulting encoded string</summary>
		/// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
		/// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
		/// <param name="length">specifies the number of bytes to encode from the source buffer</param>
		/// <returns>the encoded string</returns>
		string Encode(byte [] sourceBuffer, int startOffset, int length);

		/// <summary>Encodes the given byte range from the given source buffer, sets the codedStr to the resulting encoded string and returns true if the operation was successful.</summary>
		/// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
		/// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
		/// <param name="length">specifies the number of bytes to encode from the source buffer</param>
		/// <param name="codedStr">the output string parameter that will be set to the encoded string</param>
		/// <returns>true if the operation was successful, false otherwise.  The contents of the resulting encoded string are not defined if the return value is false.</returns>
		bool Encode(byte [] sourceBuffer, int startOffset, int length, out string codedStr);

		/// <summary>Decodes the given encoded string and returns the resulting decoded byte array.</summary>
		/// <param name="codedStr">the string containing the encoded characters.</param>
		/// <returns>the decoded byte array</returns>
		byte [] Decode(string codedStr);

		/// <summary>Decodes the given encoded, sets the decodedBuffer to the resulting decoded byte array and returns true if the operation was successful.</summary>
		/// <param name="codedStr">the string containing the encoded characters.</param>
		/// <param name="decodedBuffer">the output byte array variable that will be set to a new array containing the decoded bytes</param>
		/// <returns>true if the operation was successful, false otherwise.  The contents of the resulting decoded buffer are not defined if the return value is false</returns>
		bool Decode(string codedStr, out byte [] decodedBuffer);
	}

    /// <summary>This static class (sub-namespace) contains static properties to get access to commonly used transcoder singleton instances</summary>
	public static class ByteArrayTranscoders
	{
		private static IByteArrayTranscoder byteArrayStringTranscoder = new ByteArrayStringTranscoder();
		private static IByteArrayTranscoder base64UrlTranscoder = new Base64UrlTranscoder();
        private static IByteArrayTranscoder hexTranscoder = new HexByteArrayTranscoder() { UseByteSeperator = true, UseWordSeperator = true };
        private static IByteArrayTranscoder hexTranscoderNoPadding = new HexByteArrayTranscoder() { UseByteSeperator = false, UseWordSeperator = false };

        /// <summary>Returns a Transcoder that converts directly between byte arrays and strings of the identical character (bit patterns).  Encode widens each byte, Decode truncates the upper bits in each character to give the resulting byte.</summary>
		public static IByteArrayTranscoder ByteStringTranscoder { get { return byteArrayStringTranscoder; } }

        /// <summary>Returns a Transcoder that converts between binary byte arrays and Base64 coded strings</summary>
		public static IByteArrayTranscoder Base64UrlTranscoder { get { return base64UrlTranscoder; } }

        /// <summary>Returns a Transcoder that converts between binary byte arrays and hexadecimal coded strings</summary>
        public static IByteArrayTranscoder HexStringTranscoder { get { return hexTranscoder; } }

        /// <summary>Returns a Transcoder that converts between binary byte arrays and hexadecimal coded strings.  Encoded output strings have no added padding whitespace.</summary>
        public static IByteArrayTranscoder HexStringTranscoderNoPadding { get { return hexTranscoderNoPadding; } }
    }

    /// <summary>Base class for some transcoders.  Provides base implementations for most of the IByteArrayTranscoder methods</summary>
	public abstract class ByteArrayTranscoderBase : IByteArrayTranscoder
	{
        /// <summary>
        /// Protected read only empty byte array field.  Immutable.  Used for null transformation to minimize if statements in normal flow of control.
        /// </summary>
		protected readonly static byte [] emptyArray = new byte [0];

        /// <summary>Encodes the bytes in the given source buffer and returns the resulting encoded string.</summary>
        /// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
        /// <returns>the encoded string</returns>
        public string Encode(byte[] sourceBuffer) 
		{
            if (sourceBuffer == null)
                sourceBuffer = emptyArray;
            return Encode(sourceBuffer, 0, sourceBuffer.Length); 
		}

        /// <summary>Encodes the given byte range from the given source buffer and returns the resulting encoded string</summary>
        /// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
        /// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
        /// <param name="length">specifies the number of bytes to encode from the source buffer</param>
        /// <returns>the encoded string</returns>
        public string Encode(byte[] sourceBuffer, int startOffset, int length)
		{
			string base64str;
			Encode(sourceBuffer, startOffset, length, out base64str);
			return base64str;
		}

        /// <summary>
        /// Abstract method that is used to other Encode variants defined in this class.  Each specific type of transcoder directly implements this method.
        /// Encodes the given byte range from the given source buffer, sets the codedStr to the resulting encoded string and returns true if the operation was successful.
        /// </summary>
        /// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
        /// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
        /// <param name="length">specifies the number of bytes to encode from the source buffer</param>
        /// <param name="codedStr">the output string parameter that will be set to the encoded string</param>
        /// <returns>true if the operation was successful, false otherwise.  The contents of the resulting encoded string are not defined if the return value is false.</returns>
        public abstract bool Encode(byte[] sourceBuffer, int startOffset, int length, out string codedStr);

        /// <summary>Decodes the given encoded string and returns the resulting decoded byte array.</summary>
        /// <param name="codedStr">the string containing the encoded characters.</param>
        /// <returns>the decoded byte array</returns>
        public byte[] Decode(string codedStr)
		{
			byte [] outArray = null;
			Decode(codedStr, out outArray);
			return outArray;
		}

        /// <summary>
        /// Abstract method that is used to other Decode variants defined in this class.  Each specific type of transcoder directly implements this method.
        /// Decodes the given encoded, sets the decodedBuffer to the resulting decoded byte array and returns true if the operation was successful.
        /// </summary>
        /// <param name="codedStr">the string containing the encoded characters.</param>
        /// <param name="decodedBuffer">the output byte array variable that will be set to a new array containing the decoded bytes</param>
        /// <returns>true if the operation was successful,false otherwise.  The contents of the resulting decoded buffer are not defined if the return value is false</returns>
        public abstract bool Decode(string codedStr, out byte[] decodedBuffer);
	}

    /// <summary>
    /// Converts directly between byte arrays and strings of the identical characters using simple Widen and Narrow operations.  
    /// Narrow operation retains only the lower 8 bytes from each character in the transcoded string.
    /// </summary>
	public class ByteArrayStringTranscoder : ByteArrayTranscoderBase
	{
        /// <summary>
        /// Encodes the given byte range from the given source buffer, sets the codedStr to the resulting encoded string and returns true if the operation was successful.
        /// </summary>
        /// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
        /// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
        /// <param name="length">specifies the number of bytes to encode from the source buffer</param>
        /// <param name="codedStr">the output string parameter that will be set to the encoded string</param>
        /// <returns>true if the operation was successful, false otherwise.  The contents of the resulting encoded string are not defined if the return value is false.</returns>
        public override bool Encode(byte[] sourceBuffer, int startOffset, int length, out string codedStr)
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

        /// <summary>
        /// Decodes the given encoded, sets the decodedBuffer to the resulting decoded byte array and returns true if the operation was successful.
        /// </summary>
        /// <param name="codedStr">the string containing the encoded characters.</param>
        /// <param name="decodedBuffer">the output byte array variable that will be set to a new array containing the decoded bytes</param>
        /// <returns>true if the operation was successful,false otherwise.  The contents of the resulting decoded buffer are not defined if the return value is false</returns>
        public override bool Decode(string codedStr, out byte[] decodedBuffer)
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

        /// <summary>
        /// Encodes the given byte range from the given source buffer, sets the codedStr to the resulting encoded string and returns true if the operation was successful.
        /// </summary>
        /// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
        /// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
        /// <param name="length">specifies the number of bytes to encode from the source buffer</param>
        /// <param name="codedStr">the output string parameter that will be set to the encoded string</param>
        /// <returns>true if the operation was successful, false otherwise.  The contents of the resulting encoded string are not defined if the return value is false.</returns>
        public override bool Encode(byte[] sourceBuffer, int startOffset, int length, out string codedStr)
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

        /// <summary>
        /// Decodes the given encoded, sets the decodedBuffer to the resulting decoded byte array and returns true if the operation was successful.
        /// </summary>
        /// <param name="codedStr">the string containing the encoded characters.</param>
        /// <param name="decodedBuffer">the output byte array variable that will be set to a new array containing the decoded bytes</param>
        /// <returns>true if the operation was successful,false otherwise.  The contents of the resulting decoded buffer are not defined if the return value is false</returns>
        public override bool Decode(string codedStr, out byte[] decodedBuffer)
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


    /// <summary>
    /// Converts directly between byte arrays and strings containing the corresponding sequence of hexidecimal values.  
    /// </summary>
    public class HexByteArrayTranscoder : ByteArrayTranscoderBase
    {
        /// <summary>
        /// Default constructor.  Sets UseByteSeperator and UseWordSeperator to true.
        /// </summary>
        public HexByteArrayTranscoder()
            : base()
        {
            UseByteSeperator = true;
            UseWordSeperator = true;
        }

        /// <summary>Set to true to have the transcoder include spaces between the two "bytes" in each "word" of hex output.</summary>
        public bool UseByteSeperator { get; set; }

        /// <summary>Set to true to have the transcoder include spaces between "words" in the hex output (sets of 4 hex digits)</summary>
        public bool UseWordSeperator { get; set; }

        /// <summary>Encode generates upper case hex letters (A..F) when true.  Encode generates lower case hex letters (a..f) when false</summary>
        public bool UseUpperCase { get; set; }

        /// <summary>
        /// Encodes the given byte range from the given source buffer, sets the codedStr to the resulting encoded hex string and returns true if the operation was successful.
        /// </summary>
        /// <param name="sourceBuffer">specifies the source buffer from which to encode bytes</param>
        /// <param name="startOffset">specifies the index of the first byte in the source buffer</param>
        /// <param name="length">specifies the number of bytes to encode from the source buffer</param>
        /// <param name="codedStr">the output string parameter that will be set to the encoded string</param>
        /// <returns>true if the operation was successful, false otherwise.  The contents of the resulting encoded string are not defined if the return value is false.</returns>
        public override bool Encode(byte[] sourceBuffer, int startOffset, int length, out string codedStr)
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

            sb.EnsureCapacity((endOffset - startOffset) * 3);

            int lastIdx = endOffset - 1;
            for (int idx = startOffset; idx <= lastIdx; idx++)
            {
                if (UseUpperCase)
                    sb.CheckedAppendFormat("{0:X2}", unchecked((int)sourceBuffer[idx]));
                else 
                    sb.CheckedAppendFormat("{0:x2}", unchecked((int)sourceBuffer[idx]));

                bool isLastByte = (idx == lastIdx);
                bool appendSeperator = !isLastByte && (((idx % 2) == 0) ? UseWordSeperator : UseByteSeperator);

                if (appendSeperator)
                    sb.Append(" ");
            }

            codedStr = sb.ToString();
            return success;
        }

        /// <summary>
        /// Decodes the given hex codedStr, sets the decodedBuffer to the resulting decoded byte array and returns true if the operation was successful.
        /// </summary>
        /// <param name="codedStr">the string containing the encoded characters.</param>
        /// <param name="decodedBuffer">the output byte array variable that will be set to a new array containing the decoded bytes</param>
        /// <returns>true if the operation was successful,false otherwise.  The contents of the resulting decoded buffer are not defined if the return value is false</returns>
        public override bool Decode(string codedStr, out byte[] decodedBuffer)
        {
            List<byte> byteBuilder = new List<byte>();

            bool success = true;
            StringScanner ss = new StringScanner(codedStr);
            while (success && !ss.IsAtEnd)
            {
                int value;
                success = ss.ParseHexValue(out value, 2, 2, true, true, false);
                success &= (value >= 0 && value <= 255);
                if (success)
                    byteBuilder.Add(unchecked((byte)value));
            }

            decodedBuffer = byteBuilder.ToArray();

            return success;
        }
    }

	#endregion
}

//-------------------------------------------------------------------
