//-------------------------------------------------------------------
/*! @file String.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

using MosaicLib.Modular.Common;

namespace MosaicLib.Utils
{
    #region Unassociated Functions

    /// <summary>
    /// Fcns class is essentially a namespace for series of static helper methods
    /// <para/>includes: DisposeOf... methods, CheckedFormat and other String related methods, array/list specific Equals methods, ...
    /// </summary>
    /// <remarks>These methods are now also Extension Methods</remarks>
    public static partial class Fcns
    {
        #region static IsNullOrEmpty method

        /// <summary>
        /// Extension method version of String.IsNullOrEmpty(s).  Returns true if the given string is null or is String.Empty.  Returns false otherwise.
        /// </summary>
        public static bool IsNullOrEmpty(this string s)
        {
            return string.IsNullOrEmpty(s);
        }

        #endregion

        #region static String value mapping functions

        /// <summary>Maps the given string value to the empty string if it is null</summary>
		/// <param name="s">The string to test for null and optionally map</param>
		/// <returns>The given string s if it was not null or the empty string if it was.</returns>
		public static string MapNullToEmpty(this string s) 
        { 
            return ((s == null) ? string.Empty : s); 
        }

		/// <summary>Maps the given string s value to the given mappedS value if the given s is null or empty</summary>
		/// <param name="s">The string to test for null or empty and to optionally map</param>
		/// <param name="mappedS">The string value to return when the reference string s is null or empty</param>
		/// <returns>The given string s if it was not null and not empty or the given string mappedS.</returns>
		public static string MapNullOrEmptyTo(this string s, string mappedS) 
        { 
            return (string.IsNullOrEmpty(s) ? mappedS : s); 
        }

        /// <summary>
        /// When the given string <paramref name="s"/> is the empty string then this method returns the given <paramref name="mapEmptyTo"/> value,
        /// otherwise this method returns the given <paramref name="s"/> value without change.
        /// </summary>
        public static string MapEmptyTo(this string s, string mapEmptyTo = null)
        {
            return ((s == string.Empty) ? mapEmptyTo : s);
        }

		#endregion

        #region static String Ascii predicate method(s)

        /// <summary>
        /// Returns true if all of the characters in this string have char values from 32 to 127, or the string is empty.  valueForNull is returned if the given string is null.
        /// </summary>
        public static bool IsBasicAscii(this string s, bool valueForNull = true)
        {
            return s.IsBasicAscii(null, valueForNull);
        }

        /// <summary>
        /// Returns true if the given character value is between 32 and 127
        /// </summary>
        public static bool IsBasicAscii(this char c)
        {
            return IsBasicAscii(c, null);
        }

        /// <summary>
        /// Returns true if all of the characters in this string have char values from 32 to 127, or the string is empty.
        /// If otherExcludeCharsList is not null then the method returns false if any character in s is also included in the given otherExcludeCharsList.
        /// valueForNull is returned if the given string is null.
        /// </summary>
        public static bool IsBasicAscii(this string s, IList<char> otherExcludeCharsList, bool valueForNull = true)
        {
            if (s == null)
                return valueForNull;

            int sLength = s.Length;
            for (int idx = 0; idx < sLength; idx++)
            {
                char c = s[idx];
                if (!c.IsBasicAscii(otherExcludeCharsList))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the given char c's value is between 32 and 127.  If otherExcludeCharsList is not null then the method returns false if the given char c is included in the given otherExcludeCharsList.
        /// </summary>
        public static bool IsBasicAscii(this char c, IList<char> otherExcludeCharsList)
        {
            if (c < 32 || c > 127)
                return false;

            if (otherExcludeCharsList != null && otherExcludeCharsList.Contains(c))
                return false;

            return true;
        }

        #endregion

        #region IsByteSerializable

        /// <summary>
        /// Returns true if all of the characters in the given string s are byte serializable (integer values between 0 and 255).
        /// Also returns true if the given string s is null.
        /// </summary>
        public static bool IsByteSerializable(this string s)
        {
            return IsByteSerializable(s, true);
        }

        /// <summary>
        /// Returns true if all of the characters in the given string s are byte serializable (integer values between 0 and 255).
        /// If the given string s is null then this method returns the valueForNull parameter instead.
        /// </summary>
        public static bool IsByteSerializable(this string s, bool valueForNull)
        {
            if (s == null)
                return valueForNull;

            int sLength = s.Length;
            for (int idx = 0; idx < sLength; idx++)
            {
                char c = s[idx];
                if (!c.IsByteSerializable())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the integer value of the given char c is safely convertable to an unsigned byte (0 .. 255)
        /// </summary>
        public static bool IsByteSerializable(this char c)
        {
            return (c >= 0 && c <= 255);
        }

        #endregion

        #region static String Ascii escape methods

        /// <summary>
        /// Generate and return a "JSON escaped" version of given string s.
        /// By default this escapes all non-printable characters and also escapes the escape charater and the double-quote character
        /// </summary>
        public static string GenerateJSONVersion(this string s)
        {
            if (s.IsBasicAscii(jsonForceEscapeCharList, true))
                return s ?? string.Empty;

            return s.GenerateEscapedVersion(jsonForceEscapeCharList);
        }

        private static readonly List<char> jsonForceEscapeCharList = new List<char>() { '\"', '\\' };

        /// <summary>
        /// Generate and return a escaped version of given string s that is suitable for logging.
        /// By default this escapes all non-printable characters and also escapes the escape charater and the double-quote character
        /// </summary>
        public static string GenerateLoggingVersion(this string s)
        {
            if (s.IsBasicAscii(loggingForceEscapeCharList, true))
                return s ?? string.Empty;

            return s.GenerateEscapedVersion(loggingForceEscapeCharList);
        }

        private static readonly IList<char> loggingForceEscapeCharList = new List<char>() { '\\' }.AsReadOnly();

        /// <summary>
        /// Generate and return a escaped version of given string s that is suitable for inserting in-between single or double quotes.
        /// By default this escapes all non-printable characters and also escapes the escape charater and the single and double-quote characters
        /// </summary>
        public static string GenerateQuotableVersion(this string s)
        {
            if (s.IsBasicAscii(quotesForceEscapeCharList, true))
                return s ?? string.Empty;

            return s.GenerateEscapedVersion(quotesForceEscapeCharList);
        }

        private static readonly IList<char> quotesForceEscapeCharList = new List<char>() { '\'', '\"', '\\' }.AsReadOnly();

        /// <summary>
        /// Generate and return a Square Bracket escaped version of given string s.
        /// By default this escapes all non-printable characters and also escapes the escape charater and the open and close square bracket characters ('[' and ']')
        /// </summary>
        public static string GenerateSquareBracketEscapedVersion(this string s)
        {
            if (s.IsBasicAscii(squareBracketForceEscapeCharList, true))
                return s ?? string.Empty;

            return s.GenerateEscapedVersion(squareBracketForceEscapeCharList);
        }

        private static readonly List<char> squareBracketForceEscapeCharList = new List<char>() { '[', ']' };


        /// <summary>
        /// Generate and return "escaped" version of given string s.  Supports use for general JSON style escapeing.
        /// Also generates escaped version of any other characters that are explicitly includes in the given extraEscapeCharList
        /// (typically '\"' or '\'')
        /// </summary>
        public static string GenerateEscapedVersion(this string s, IList<char> extraEscapeCharList)
        {
            s = s.MapNullToEmpty();

            StringBuilder sb = new StringBuilder();

            foreach (char c in s)
            {
                if (c.IsBasicAscii(extraEscapeCharList) && c != '\\')
                {
                    sb.Append(c);
                }
                else
                {
                    // this character is 
                    switch (c)
                    {
                        case '\"': sb.Append("\\\""); break;        // double quote char - we may not get here depending on the contents of the extraEscapeCharList
                        case '\'': sb.Append(@"\'"); break;         // single quote char - we may not get here depending on the contents of the extraEscapeCharList
                        case '\\': sb.Append(@"\\"); break;         // escape char      // JSON calls this a reverse solidus
                        case '\r': sb.Append(@"\r"); break;         // carrage return
                        case '\n': sb.Append(@"\n"); break;         // line feed
                        case '\t': sb.Append(@"\t"); break;         // (horizontal) tab
                        case '\b': sb.Append(@"\b"); break;         // backspace
                        case '\f': sb.Append(@"\f"); break;         // form feed
                        case '\v': sb.Append(@"\v"); break;         // vertical tab
                        default:
                            if (c >= 0x00 && c <= 0xff)
                                sb.CheckedAppendFormat("\\x{0:x2}", unchecked((Byte)c));
                            else
                                sb.CheckedAppendFormat("\\u{0:x4}", unchecked((UInt16)c));

                            break;
                    }
                }
            }

            return sb.ToString();
        }

        #endregion

        #region static CheckedFormat methods

        /// <summary>
        /// Invokes System.String.Format with the given args within a try/catch pattern.
        /// System.String.Format replaces the format item in a specified System.String with the text equivalent of the value of a specified System.Object instance.
        /// </summary>
        /// <param name="fmt">A composite format string.</param>
        /// <param name="arg0">An System.Object to format.</param>
        /// <returns>
        ///     A copy of fmt in which the first format item has been replaced by the
        ///     System.String equivalent of arg0.
        /// </returns>
        public static string CheckedFormat(this string fmt, object arg0)
		{
            try
            {
                return System.String.Format(fmt, arg0);
            }
            catch (System.Exception ex)
            {
                return System.String.Format("Format1('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
		}

		/// <summary>
        /// Invokes System.String.Format with the given args within a try/catch pattern.
        /// System.String.Format replaces the format item in a specified System.String with the text equivalent
        ///     of the value of two specified System.Object instances.
        /// </summary>
        /// <param name="fmt">A composite format string.</param>
        /// <param name="arg0">The first System.Object to format.</param>
        /// <param name="arg1">The second System.Object to format.</param>
        /// <returns>
        ///     A copy of format in which the first and second format items have
        ///     been replaced by the System.String equivalents of arg0 and arg1.
        /// </returns>
        public static string CheckedFormat(this string fmt, object arg0, object arg1)
		{
			try
			{
				return System.String.Format(fmt, arg0, arg1);
			}
            catch (System.Exception ex)
            {
                return System.String.Format("Format2('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

		/// <summary>
        /// Invokes System.String.Format with the given args within a try/catch pattern.
        /// System.String.Format replaces the format item in a specified System.String with the text equivalent
        ///     of the value of three specified System.Object instances.
        /// </summary>
        /// <param name="fmt">A composite format string.</param>
        /// <param name="arg0">The first System.Object to format.</param>
        /// <param name="arg1">The second System.Object to format.</param>
        /// <param name="arg2">The third System.Object to format.</param>
        /// <returns>
        ///     A copy of format in which the first, second, and third format items have
        ///     been replaced by the System.String equivalents of arg0, arg1, and arg2.
        /// </returns>
        public static string CheckedFormat(this string fmt, object arg0, object arg1, object arg2)
		{
			try
			{
				return System.String.Format(fmt, arg0, arg1, arg2);
			}
            catch (System.Exception ex)
            {
                return System.String.Format("Format3('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

		/// <summary>
        /// Invokes System.String.Format with the given args within a try/catch pattern.
        /// System.String.Format replaces the format item in a specified System.String with the text equivalent
        ///     of the value of a corresponding System.Object instance in a specified array.
        /// </summary>
        /// <param name="fmt">A composite format string.</param>
        /// <param name="args">An System.Object array containing zero or more objects to format.</param>
        /// <returns>
        ///     A copy of fmt in which the format items have been replaced by the System.String
        ///     equivalent of the corresponding instances of System.Object in args.
        /// </returns>
        public static string CheckedFormat(this string fmt, params object[] args)
		{
			try
			{
				return System.String.Format(fmt, args);
			}
            catch (System.Exception ex)
            {
                return System.String.Format("FormatN('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }


        /// <summary>
        /// Invokes System.String.Format with the given args within a try/catch pattern.
        /// System.String.Format replaces the format item in a specified System.String with the text equivalent
        ///     of the value of a corresponding System.Object instance in a specified array.
        ///     A specified parameter supplies culture-specific formatting information.
        /// </summary>
        /// <param name="provider">An System.IFormatProvider that supplies culture-specific formatting information.</param>
        /// <param name="fmt">A composite format string.</param>
        /// <param name="args">An System.Object array containing zero or more objects to format.</param>
        /// <returns>
        ///     A copy of fmt in which the format items have been replaced by the System.String
        ///     equivalent of the corresponding instances of System.Object in args.
        /// </returns>
        public static string CheckedFormat(this IFormatProvider provider, string fmt, params object[] args)
        {
            try
            {
                return System.String.Format(provider, fmt, args);
            }
            catch (System.Exception ex)
            {
                return System.String.Format(provider, "FormatPN('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

        #endregion

        #region static String [] methods

        /// <summary>Returns true if both lists have the same contents.  Returns false if they do not.</summary>
        public static bool Equals(this String[] a, String[] b)
        {
            if (a == null && b == null)
                return true;
            if ((a == null) || (b == null))
                return false;
            if (a.Length != b.Length)
                return false;

            int n = a.Length;
            for (int idx = 0; idx < n; idx++)
            {
                if (a[idx] != b[idx])
                    return false;
            }

            return true;
        }

        /// <summary>Returns true if both the array and the list have the same contents.  Returns false if they do not.</summary>
        public static bool Equals(this String[] a, IList<String> b)
        {
            if (a == null && b == null)
                return true;
            if ((a == null) || (b == null))
                return false;
            if (a.Length != b.Count)
                return false;

            int n = a.Length;
            for (int idx = 0; idx < n; idx++)
            {
                if (a[idx] != b[idx])
                    return false;
            }

            return true;
        }

        #endregion
    }

	#endregion

    #region Extension Methods

    /// <summary>
    /// This class contains a set of extension methods.  At present this primarily adds variants of the CheckedFormat methods above to be used directly with Strings and StringBuilder
    /// </summary>
    public static partial class ExtensionMethods
    {
        #region static System.Text.StringBuilder extension methods (Reset)

        /// <summary>Extension method that allows a StringBuilder contents to be cleared.</summary>
        public static StringBuilder Reset(this StringBuilder sb)
        {
            sb.Remove(0, sb.Length);
            return sb;
        }
        
        #endregion

        #region static System.Text.StringBuilder.CheckedAppendFormat extension methods

        /// <summary>Invokes System.Text.StringBuilder.AppendFormat with the given args within a try/catch pattern.</summary>
        public static StringBuilder CheckedAppendFormat(this StringBuilder sb, string fmt, object arg0)
        {
            try
            {
                sb.AppendFormat(fmt, arg0);
            }
            catch (System.Exception ex)
            {
                sb.AppendFormat("Format1('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
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
            catch (System.Exception ex)
            {
                sb.AppendFormat("Format2('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
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
            catch (System.Exception ex)
            {
                sb.AppendFormat("Format3('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
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
            catch (System.Exception ex)
            {
                sb.AppendFormat("FormatN('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
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
            catch (System.Exception ex)
            {
                sb.AppendFormat("FormatPN('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sb;
        }

        #endregion

        #region static System.IO.StreamWriter CheckedWrite and CheckedWriteLine extension methods

        /// <summary>Invokes System.IO.StreamWriter.Write with the given args within a try/catch pattern.</summary>
        public static StreamWriter CheckedWrite(this StreamWriter sw, string fmt, object arg0)
        {
            try
            {
                sw.Write(fmt, arg0);
            }
            catch (System.Exception ex)
            {
                sw.Write("Format1('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sw;
        }

        /// <summary>Invokes System.IO.StreamWriter.Write with the given args within a try/catch pattern.</summary>
        public static StreamWriter CheckedWrite(this StreamWriter sw, string fmt, object arg0, object arg1)
        {
            try
            {
                sw.Write(fmt, arg0, arg1);
            }
            catch (System.Exception ex)
            {
                sw.Write("Format2('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sw;
        }

        /// <summary>Invokes System.IO.StreamWriter.Write with the given args within a try/catch pattern.</summary>
        public static StreamWriter CheckedWrite(this StreamWriter sw, string fmt, object arg0, object arg1, object arg2)
        {
            try
            {
                sw.Write(fmt, arg0, arg1, arg2);
            }
            catch (System.Exception ex)
            {
                sw.Write("Format3('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sw;
        }

        /// <summary>Invokes System.IO.StreamWriter.Write with the given args within a try/catch pattern.</summary>
        public static StreamWriter CheckedWrite(this StreamWriter sw, string fmt, params object[] args)
        {
            try
            {
                sw.Write(fmt, args);
            }
            catch (System.Exception ex)
            {
                sw.Write("FormatN('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sw;
        }

        /// <summary>Invokes System.IO.StreamWriteLiner.WriteLine with the given args within a try/catch pattern.</summary>
        public static StreamWriter CheckedWriteLine(this StreamWriter sw, string fmt, object arg0)
        {
            try
            {
                sw.WriteLine(fmt, arg0);
            }
            catch (System.Exception ex)
            {
                sw.WriteLine("Format1('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sw;
        }

        /// <summary>Invokes System.IO.StreamWriter.WriteLine with the given args within a try/catch pattern.</summary>
        public static StreamWriter CheckedWriteLine(this StreamWriter sw, string fmt, object arg0, object arg1)
        {
            try
            {
                sw.WriteLine(fmt, arg0, arg1);
            }
            catch (System.Exception ex)
            {
                sw.WriteLine("Format2('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sw;
        }

        /// <summary>Invokes System.IO.StreamWriter.WriteLine with the given args within a try/catch pattern.</summary>
        public static StreamWriter CheckedWriteLine(this StreamWriter sw, string fmt, object arg0, object arg1, object arg2)
        {
            try
            {
                sw.WriteLine(fmt, arg0, arg1, arg2);
            }
            catch (System.Exception ex)
            {
                sw.WriteLine("Format3('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sw;
        }

        /// <summary>Invokes System.IO.StreamWriter.WriteLine with the given args within a try/catch pattern.</summary>
        public static StreamWriter CheckedWriteLine(this StreamWriter sw, string fmt, params object[] args)
        {
            try
            {
                sw.WriteLine(fmt, args);
            }
            catch (System.Exception ex)
            {
                sw.WriteLine("FormatN('{0}') threw {1}", fmt, ex.ToString(ExceptionFormat.TypeAndMessage));
            }

            return sw;
        }

        #endregion

        #region string and string array size estimate methods

        /// <summary>Returns the estimated size of the contents of the given string in bytes, assuming that each character in the string will consume 2 bytes</summary>
        public static int EstimatedContentSizeInBytes(this String s)
        {
            return (s.MapNullToEmpty().Length * sizeof(char));
        }

        /// <summary>Returns the estimated size of the contents of the given string array in bytes, assuming that each character in each string in the array will consume 2 bytes</summary>
        public static int EstimatedContentSizeInBytes(this String[] sArray)
        {
            return (sArray.Sum((s) => s.MapNullToEmpty().Length) * sizeof(char));
        }

        /// <summary>Returns the estimated size of the contents of the given list of strings in bytes, assuming that each character in each string in the list will consume 2 bytes</summary>
        public static int EstimatedContentSizeInBytes(this IList<String> sList)
        {
            return (sList.Sum((s) => s.MapNullToEmpty().Length) * sizeof(char));
        }

        /// <summary>Returns the sum of the estimated size of the contents of the given list of ValueContainers in bytes</summary>
        public static int EstimatedContentSizeInBytes(this IList<ValueContainer> vcList)
        {
            return (vcList.Sum((vc) => vc.EstimatedContentSizeInBytes));
        }

        /// <summary>Returns the sum of the estimated size of the contents of the given array of ValueContainers in bytes</summary>
        public static int EstimatedContentSizeInBytes(this ValueContainer [] vcArray)
        {
            return (vcArray.Sum((vc) => vc.EstimatedContentSizeInBytes));
        }

        #endregion

        #region string prefix add/remote tools

        /// <summary>
        /// This takes the given from string value and adds the given prefix string value if the from string does not already start with the prefix string.
        /// If from is null or empty then this method simply returns prefix.  
        /// If prefix is null or empty or from already starts with prefix then this method returns from.
        /// Otherwise this method returns prefix + from.
        /// </summary>
        public static string AddPrefixIfNeeded(this string from, string prefix)
        {
            if (from.IsNullOrEmpty())
                return prefix;

            if (prefix.IsNullOrEmpty() || from.StartsWith(prefix))
                return from;

            return prefix + from;
        }

        /// <summary>
        /// This takes the given from string value and removes the given prefix string value if the from string starts with the prefix string.
        /// If from is null or empty or prefix is null or empty then this method simply returns from. 
        /// If from does not start with prefix then this method returns from.
        /// Otherwise this method returns from.SubString(prefix.Length).
        /// </summary>
        public static string RemovePrefixIfNeeded(this string from, string prefix)
        {
            if (from.IsNullOrEmpty() || prefix.IsNullOrEmpty())
                return from;

            if (!from.StartsWith(prefix))
                return from;

            return from.Substring(prefix.Length);
        }

        /// <summary>
        /// This takes the given from string value and adds the given suffix string value if the from string does not already end with the suffix string.
        /// If from is null or empty then this method simply returns suffix.  
        /// If suffix is null or empty or from already ends with suffix then this method returns from.
        /// Otherwise this method returns from + suffix.
        /// </summary>
        public static string AddSuffixIfNeeded(this string from, string suffix)
        {
            if (from.IsNullOrEmpty())
                return suffix;

            if (suffix.IsNullOrEmpty() || from.EndsWith(suffix))
                return from;

            return from + suffix;
        }

        /// <summary>
        /// This takes the given from string value and removes the given suffix string value if the from string starts with the suffix string.
        /// If from is null or empty or suffix is null or empty then this method simply returns from. 
        /// If from does not start with suffix then this method returns from.
        /// Otherwise this method returns from.Substring(0, from.Length - suffix.Length).
        /// </summary>
        public static string RemoveSuffixIfNeeded(this string from, string suffix)
        {
            if (from.IsNullOrEmpty() || suffix.IsNullOrEmpty())
                return from;

            if (!from.EndsWith(suffix))
                return from;

            return from.Substring(0, from.Length - suffix.Length);
        }

        #endregion

        #region IsHex char/string related extension methods

        /// <summary>
        /// Returns true if the given string s is non-empty and each of its characters are valid hex digits
        /// </summary>
        public static bool IsHexNumber(this string s)
        {
            return s.IsHexNumber(true, true);
        }

        /// <summary>
        /// Returns true if the given string s is non-empty and each of its characters are valid hex digits
        /// </summary>
        public static bool IsHexNumber(this string s, bool allowLowerCase, bool allowUpperCase)
        {
            if (s.IsNullOrEmpty())
                return false;

            foreach (char c in s)
            {
                if (!c.IsHexDigit(allowLowerCase, allowUpperCase))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the indexed char in the given string s is a valid digit ('0'-'9') or is an upper or lower case leter from 'A' to 'F'
        /// </summary>
        public static bool IsHexDigit(this string s, int index)
        {
            if (s != null && index >= 0 && index < s.Length)
                return s[index].IsHexDigit(true, true);

            return false;
        }

        /// <summary>
        /// Returns true if the indexed char in the given string s is a valid digit ('0'-'9') and allowLowerCase or allowUpperCase is true, 
        /// or it is a lower case letter from 'a' to 'f' and allowLowerCase is true,
        /// or it is an upper case letter from 'A' to 'F' and allowUpperCase is true.
        /// </summary>
        public static bool IsHexDigit(this string s, int index, bool allowLowerCase, bool allowUpperCase)
        {
            if (s != null && index >= 0 && index < s.Length)
                return s[index].IsHexDigit(allowLowerCase, allowUpperCase);

            return false;
        }

        /// <summary>
        /// Returns true if the given char c is a digit ('0'-'9') or is an upper or lower case leter from 'A' to 'F'
        /// </summary>
        public static bool IsHexDigit(this char c)
        {
            return c.IsHexDigit(true, true);
        }

        /// <summary>
        /// Returns true if the given char c is a digit ('0'-'9') and allowLowerCase or allowUpperCase is true, 
        /// or it is a lower case letter from 'a' to 'f' and allowLowerCase is true,
        /// or it is an upper case letter from 'A' to 'F' and allowUpperCase is true.
        /// </summary>
        public static bool IsHexDigit(this char c, bool allowLowerCase, bool allowUpperCase)
        {
            switch (c)
            {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return (allowLowerCase || allowUpperCase);
                case 'A':
                case 'B':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                    return allowUpperCase;
                case 'a':
                case 'b':
                case 'c':
                case 'd':
                case 'e':
                case 'f':
                    return allowLowerCase;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Accepts an upper or lower case hex digit char c, and converts it to the corresponding digit value, which is then returned.
        /// If the given char c is not a hex digit then the method returns -1.
        /// </summary>
        public static int HexDigitValue(this char c)
        {
            if (Char.IsDigit(c))
                return c - '0';
            else if (c >= 'A' && c <= 'F')
                return 10 + c - 'A';
            else if (c >= 'a' && c <= 'f')
                return 10 + c - 'a';
            else
                return -1;
        }

        #endregion

        #region SafeToString

        /// <summary>Extension method that returns ToString applied to the given object, or mapNullTo (defaults to "") if the given object is null.</summary>
        public static string SafeToString(this object o, string mapNullTo = "")
        {
            return (o != null ? o.ToString() : mapNullTo);
        }

        #endregion
    }

    #endregion

    #region String matching utilites

    namespace StringMatching
    {
        /// <summary>
        /// This is a list (set) of Rule objects that supports deep cloneing.  
        /// <para/>This set is expected to be used with the MatchesAny extension method which supports unified use with normal, empty, or null sets
        /// </summary>
        [CollectionDataContract(Namespace = Constants.UtilsNameSpace)]
        public class MatchRuleSet : List<MatchRule>
        {
            /// <summary>Default constructor</summary>
            public MatchRuleSet() { }

            /// <summary>constructor starting with an externally provided set of rules</summary>
            public MatchRuleSet(IEnumerable<MatchRule> rules) : base(rules ?? emptyMatchRuleArray) { }

            /// <summary>
            /// copy constructor - makes a deep clone of the given rhs value.
            /// if the rhs value is null then this converts the null either to None, or Any based on the value of the <paramref name="convertNullToAny"/> parameter (true -> any, false -> None)
            /// <param name="other">Gives the MatchRuleSet of which a copy is to be made.  If null is provided then this method uses the <paramref name="convertNullToAny"/> parameter to determine the constructor's behavior</param>
            /// <param name="convertNullToAny">When <paramref name="other"/> is given as null, this parameter determines if the copy constructor maps the null to Any (true) or None (false).  Default value is false (None)</param>
            /// </summary>
            public MatchRuleSet(MatchRuleSet other, bool convertNullToAny = false)
                : base((other ?? (convertNullToAny ? Any : None)).Select((r) => new MatchRule(r)))
            { }

            private static readonly MatchRule[] emptyMatchRuleArray = new MatchRule[0];

            /// <summary>
            /// Shorthand constructor, constructs this MatchRuleSet to contain a single MatchRule of the indicated matchType
            /// </summary>
            public MatchRuleSet(MatchType matchType, string ruleString = null)
                : base(1)
            {
                if (matchType == MatchType.Any && ruleString == null)
                    Add(MatchRule.Any);
                else
                    Add(new MatchRule(matchType, ruleString));
            }

            /// <summary>Debugging and Logging helper method</summary>
            public override string ToString()
            {
                return String.Join(",", this.Select((r) => r.ToString()).ToArray());
            }

            /// <summary>
            /// Getter constructs and returns a MatchRuleSet that contains a single MatchType.Any MatchRule.
            /// </summary>
            public static MatchRuleSet Any
            {
                get { return new MatchRuleSet() { MatchRule.Any }; }
            }

            /// <summary>
            /// Getter constructs and returns a MatchRuleSet that contains a single MatchType.None MatchRule.
            /// </summary>
            public static MatchRuleSet None
            {
                get { return new MatchRuleSet() { MatchRule.None }; }
            }

            /// <summary>
            /// Returns true if this MatchRuleSet contains an Any match rule (and will thus match anything), or this MatchRuleSet IsEmpty
            /// </summary>
            public bool IsAny 
            { 
                get { return this.Any(rule => rule.IsAny) || this.IsEmpty(); } 
            }

            /// <summary>
            /// Returns true if this MatchRuleSet is composed of one or more None match rules.
            /// </summary>
            public bool IsNone
            {
                get { return ((this.Count >= 1) && this.All(rule => rule.IsNone)); }
            }

            /// <summary>
            /// This method allows this class to be used with a dictionary style content initializer
            /// </summary>
            public MatchRuleSet Add(MatchType matchType, String ruleString)
            {
                Add(new MatchRule(matchType, ruleString));
                return this;
            }
        }

        /// <summary>
        /// ExtensionMethods for StringMatching.
        /// </summary>
        public static partial class ExtensionMethods
        {
            /// <summary>
            /// Returns true if the given set is null or it IsAny (it contains at least one MatchRule.IsAny element (MatchType == MatchType.Any))
            /// </summary>
            public static bool IsNullOrAny(this MatchRuleSet set)
            {
                return (set == null || set.IsAny);
            }

            /// <summary>
            /// Creates and returns a clone (deep copy) of the given set if the set is non-null or returns null if the given set is null.
            /// </summary>
            public static MatchRuleSet Clone(this MatchRuleSet set)
            {
                if (set != null)
                    return new MatchRuleSet(set);
                else
                    return null;
            }

            /// <summary>
            /// Resturns true if the given testString Matches any rule in the given set's list of MatchRule objects.
            /// If the given set is null or empty then the method returns valueToUseWhenSetIsNullOrEmpty (which defaults to true).
            /// </summary>
            public static bool MatchesAny(this MatchRuleSet set, String testString, bool valueToUseWhenSetIsNullOrEmpty = true)
            {
                if (set.IsNullOrEmpty())
                    return valueToUseWhenSetIsNullOrEmpty;

                foreach (MatchRule rule in set)
                {
                    if (rule.Matches(testString))
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Simple container/implementation object for a single rule (MatchType and RuleString) used to determine if a string matches a given rule.
        /// <para/>This object is immutable in that none of its public or protected portions allow its contents to be changed.
        /// </summary>
        [DataContract(Namespace = Constants.UtilsNameSpace)]
        public class MatchRule
        {
            /// <summary>
            /// Getter constructs and returns a MatchRule with its MatchType set to MatchType.Any.
            /// </summary>
            public static MatchRule Any { get { return matchRuleAny; } }

            /// <summary>
            /// Getter constructs and returns a MatchRule with its MatchType set to MatchType.Any.
            /// </summary>
            public static MatchRule None { get { return matchRuleNone; } }

            /// <summary>Static factory method to create MatchType.Prefix match rule instances</summary>
            public static MatchRule Prefix(string prefix) { return new MatchRule(MatchType.Prefix, prefix); }

            /// <summary>Static factory method to create MatchType.Suffix match rule instances</summary>
            public static MatchRule Suffix(string suffix) { return new MatchRule(MatchType.Suffix, suffix); }

            /// <summary>Static factory method to create MatchType.Contains match rule instances</summary>
            public static MatchRule Contains(string contains) { return new MatchRule(MatchType.Contains, contains); }

            /// <summary>Static factory method to create MatchType.Regex match rule instances</summary>
            public static MatchRule Regex(string regex) { return new MatchRule(MatchType.Regex, regex); }

            private static readonly MatchRule matchRuleAny = new MatchRule(MatchType.Any);
            private static readonly MatchRule matchRuleNone = new MatchRule(MatchType.None);

            /// <summary>
            /// Returns true if this MatchRule's MatchType is Any (and will thus match anything).
            /// </summary>
            public bool IsAny { get { return (MatchType == MatchType.Any); } }

            /// <summary>
            /// Returns true if this MatchRule's MatchType is None (and will thus match nothing).
            /// </summary>
            public bool IsNone { get { return (MatchType == MatchType.None); } }

            /// <summary>
            /// Constructor.  Caller provides matchType and ruleString.  
            /// If matchType is MatchType.Regex then ruleString is used to construct a <see cref="System.Text.RegularExpressions.Regex"/> object which may throw a System.ArguementExecption
            /// if the given ruleString is not a valid Regular Expression string.
            /// </summary>
            /// <exception cref="System.ArgumentException">Thrown if MatchType is Regex and the given ruleString is not a valid regular expression.</exception>
            public MatchRule(MatchType matchType = MatchType.None, String ruleString = null)
            {
                MatchType = matchType;
                RuleString = ruleString ?? String.Empty;
                BuildRegexIfNeeded();
            }

            /// <summary>
            /// Copy constructor.
            /// <exception cref="System.ArgumentException">Thrown if MatchType is Regex and the rhs's RuleString is not a valid regular expression.</exception>
            /// </summary>
            public MatchRule(MatchRule rhs)
            {
                MatchType = rhs.MatchType;
                RuleString = rhs.RuleString;
                BuildRegexIfNeeded();
            }

            /// <summary>Defines the type of match test that this rule is used to test for (Prefix, Suffix, Contains, Regex match).</summary>
            [DataMember(Order = 1)]
            public MatchType MatchType { get; private set; }

            /// <summary>Defines the string used to test a given test string with: an expected prefix, suffix, substring, or a regular expression against which test strings are checked for a match.</summary>
            /// <remarks>Note that the constructor sanitizes this string at construction time so that this property will never return null.</remarks>
            [DataMember(Order = 2)]
            public String RuleString { get; private set; }

            /// <summary>Internal storage for the pre-computed regular expression engine if this object was constructed using StringMatchType.Regex.</summary>
            internal System.Text.RegularExpressions.Regex regex = null;

            /// <summary>This method will build the regex automatically if it has not already been built</summary>
            internal System.Text.RegularExpressions.Regex BuildRegexIfNeeded()
            {
                if (regex == null && MatchType == StringMatching.MatchType.Regex)
                    regex = new System.Text.RegularExpressions.Regex(RuleString);

                return regex;
            }

            /// <summary>
            /// Resturns true if the given testString matches the rule contained in this object based on the contstructed contents of the MatchType and RuleString.
            /// Prefix checks if testString StartsWith the RuleString, Suffix tests if testString EndsWith RuleString, 
            /// Contains tests if testString Contains RuleString, and Regex tests if RuleString as regular expression IsMatch of testString.
            /// </summary>
            /// <exception cref="System.ArgumentException">May be thrown if MatchType is Regex and the rhs's RuleString is not a valid regular expression.</exception>
            public bool Matches(String testString)
            {
                testString = testString ?? String.Empty;

                switch (MatchType)
                {
                    case MatchType.None: return false;
                    case MatchType.Any: return true;
                    case MatchType.Prefix: return testString.StartsWith(RuleString);
                    case MatchType.Suffix: return testString.EndsWith(RuleString);
                    case MatchType.Contains: return testString.Contains(RuleString);
                    case MatchType.Regex: return BuildRegexIfNeeded().IsMatch(testString);
                    case MatchType.Exact: return (testString == RuleString);
                    default: return false;
                }
            }

            /// <summary>
            /// Debugging and logging helper method
            /// </summary>
            public override string ToString()
            {
                return "MatchType.{0} '{1}'".CheckedFormat(MatchType, RuleString);
            }
        }

        /// <summary>
        /// Enum defines the different means that a RuleString can be used to determine if a given test string is to be included in a given set, or not.
        /// <para/>None (0), Any, Prefix, Suffix, Contains, Regex, Exact
        /// </summary>
        [DataContract(Namespace = Constants.UtilsNameSpace)]
        public enum MatchType : int
        {
            /// <summary>does not match any string values without regard to the contents of the corresponding RuleString</summary>
            [EnumMember]
            None = 0,
            /// <summary>matches any string value without regard to the contents of the corresponding RuleString </summary>
            [EnumMember]
            Any,
            /// <summary>matches string values that start with the contents of the corresponding RuleString</summary>
            [EnumMember]
            Prefix,
            /// <summary>matches string values that end with the contents of the corresponding RuleString</summary>
            [EnumMember]
            Suffix,
            /// <summary>matches string value that contain a sub-string equal to the contents of the corresponding RuleString</summary>
            [EnumMember]
            Contains,
            /// <summary>compiles the RuleString as a Regular Expression and the checks if the regular expression finds any match in each given test string</summary>
            [EnumMember]
            Regex,
            /// <summary>matches if the string value is exactly the same as the RuleString</summary>
            [EnumMember]
            Exact,
        }
    }

    #endregion
}

//-------------------------------------------------------------------
