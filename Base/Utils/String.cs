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
    /// <para/>inclues: DisposeOf... methods, CheckedFormat and other String related methods, array/list specific Equals methods, ...
    /// </summary>
    public static partial class Fcns
	{
		#region static String value mapping functions

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

        /// <summary>Maps the given boolean value to an integer value of 1 for true and 0 for false</summary>
        public static int MapToInt(bool value) { return (value ? 1 : 0); }

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
        public static string CheckedFormat(string fmt, params object[] args)
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

        #region static String [] methods

        /// <summary>Returns true if both lists have the same contents.  Returns false if they do not.</summary>
        public static bool Equals(String[] a, String[] b)
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
        public static bool Equals(String[] a, IList<String> b)
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

    // Library is now being built under DotNet 3.5 (or later)
    #region Extension Functions

    /// <summary>
    /// This class contains a set of extension methods.  At present this primarily adds variants of the CheckedFormat methods above to be used directly with Strings and StringBuilder
    /// </summary>
    public static partial class ExtensionMethods
    {
        #region static System.Text.StringBuilder extension methods

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
}

//-------------------------------------------------------------------
