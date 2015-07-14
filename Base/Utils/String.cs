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

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace MosaicLib.Utils
{
    #region Unassociated Functions

    /// <summary>
    /// Fcns class is essentially a namespace for series of static helper methods
    /// <para/>inclues: DisposeOf... methods, CheckedFormat and other String related methods, array/list specific Equals methods, ...
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
		public static string MapNullToEmpty(this string s) { return ((s == null) ? string.Empty : s); }

		/// <summary>Maps the given string value to the empty string if it is null</summary>
		/// <param name="s">The string to test for null or empty and to optionally map</param>
		/// <param name="mappedS">The string value to return when the reference string s is null or empty</param>
		/// <returns>The given string s if it was not null or the empty string if it was.</returns>
		public static string MapNullOrEmptyTo(this string s, string mappedS) { return (string.IsNullOrEmpty(s) ? mappedS : s); }

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
        public static string CheckedFormat(this string fmt, object arg0, object arg1)
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
        public static string CheckedFormat(this string fmt, object arg0, object arg1, object arg2)
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
        public static string CheckedFormat(this string fmt, params object[] args)
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
        public static string CheckedFormat(this IFormatProvider provider, string fmt, params object[] args)
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

    // Library is now being built under DotNet 3.5 (or later) so we can make use of extension methods.
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

    #region String matching utilites

    namespace StringMatching
    {
        /// <summary>
        /// This is a list (set) of Rule objects that supports deep cloneing.  This set is expected to be used with the MatchesAny extension method
        /// </summary>
        public class MatchRuleSet : List<MatchRule>
        {
            /// <summary>Default constructor</summary>
            public MatchRuleSet() { }
            /// <summary>constructor starting with an externally provided set of rules</summary>
            public MatchRuleSet(IEnumerable<MatchRule> rules) : base(rules) { }
            /// <summary>copy constructor - makes a deep clone of the given rhs value.</summary>
            public MatchRuleSet(MatchRuleSet rhs) : base(rhs.Select((r) => new MatchRule(r))) { }

            /// <summary>Debugging and Logging helper method</summary>
            public override string ToString()
            {
                return String.Join(",", this.Select((r) => r.ToString()).ToArray());
            }
        }

        /// <summary>
        /// ExtensionMethods for StringMatching.
        /// </summary>
        public static partial class ExtensionMethods
        {
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
            /// If the given set is null or it is empty then the method returns false.
            /// </summary>
            public static bool MatchesAny(this MatchRuleSet set, String testString)
            {
                return set.MatchesAny(testString, false);
            }

            /// <summary>
            /// Resturns true if the given testString Matches any rule in the given set's list of MatchRule objects.
            /// If the given set is null or empty then the method returns valueToUseWhenSetIsNullOrEmpty.
            /// </summary>
            public static bool MatchesAny(this MatchRuleSet set, String testString, bool valueToUseWhenSetIsNullOrEmpty)
            {
                if (set != null && set.Count != 0)
                {
                    foreach (MatchRule rule in set)
                    {
                        if (rule.Matches(testString))
                            return true;
                    }
                }
                return valueToUseWhenSetIsNullOrEmpty;
            }
        }

        /// <summary>
        /// Simple container/implementation object for a single rule (MatchType and RuleString) used to determine if a string matches a given rule.
        /// <para/>This object is immutable in that none of its public or protected portions allow its contents to be changed.
        /// <para/>This object is not re-enterant (not thread safe) when using MatchType.Regex.
        /// </summary>
        public class MatchRule
        {
            /// <summary>
            /// Constructor.  Caller provides matchType and ruleString.  
            /// If matchType is MatchType.Regex then ruleString is used to construct a <see cref="System.Text.RegularExpressions.Regex"/> object which may throw a System.ArguementExecption
            /// if the given ruleString is not a valid Regular Expression string.
            /// </summary>
            /// <exception cref="System.ArgumentException">Thrown if MatchType is Regex and the given ruleString is not a valid regular expression.</exception>
            public MatchRule(MatchType matchType, String ruleString)
            {
                MatchType = matchType;
                RuleString = ruleString;
                if (MatchType == MatchType.Regex)
                    regex = new System.Text.RegularExpressions.Regex(RuleString);
            }

            /// <summary>
            /// Copy constructor.
            /// <exception cref="System.ArgumentException">Thrown if MatchType is Regex and the rhs's RuleString is not a valid regular expression.</exception>
            /// </summary>
            public MatchRule(MatchRule rhs)
            {
                MatchType = rhs.MatchType;
                RuleString = rhs.RuleString;
                if (MatchType == MatchType.Regex)
                    regex = new System.Text.RegularExpressions.Regex(RuleString);
            }

            /// <summary>Defines the type of match test that this rule is used to test for (Prefix, Suffix, Contains, Regex match).</summary>
            public MatchType MatchType { get; private set; }
            /// <summary>Defines the string used to test a given test string with: an expected prefix, suffix, substring, or a regular expression against which test strings are checked for a match.</summary>
            public String RuleString { get; private set; }
            /// <summary>Internal storage for the pre-computed regular expression engine if this object was constructed using StringMatchType.Regex.</summary>
            private System.Text.RegularExpressions.Regex regex = null;

            /// <summary>
            /// Resturns true if the given testString matches the rule contained in this object based on the contstructed contents of the MatchType and RuleString.
            /// Prefix checks if testString StartsWith the RuleString, Suffix tests if testString EndsWith RuleString, 
            /// Contains tests if testString Contains RuleString, and Regex tests if RuleString as regular expression IsMatch of testString.
            /// </summary>
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
                    case MatchType.Regex: return regex.IsMatch(testString);
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

        /// <summary>Enum defines the different means that a RuleString can be used to determine if a given test string is to be included in a given set, or not.</summary>
        public enum MatchType : int
        {
            /// <summary>does not match any string values without regard to the contents of the corresponding RuleString</summary>
            None = 0,
            /// <summary>matches any string value without regard to the contents of the corresponding RuleString </summary>
            Any,
            /// <summary>matches string values that start with the contents of the corresponding RuleString</summary>
            Prefix,
            /// <summary>matches string values that end with the contents of the corresponding RuleString</summary>
            Suffix,
            /// <summary>matches string value that contain a sub-string equal to the contents of the corresponding RuleString</summary>
            Contains,
            /// <summary>compiles the RuleString as a Regular Expression and the checks if the regular expression finds any match in each given test string</summary>
            Regex,
        }
    }

    #endregion
}

//-------------------------------------------------------------------
