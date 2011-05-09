//-------------------------------------------------------------------
/*! @file ParseUtils.cs
 *  @breif Provides definitions and implementations of a series of Parseing related utility functions.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2002 Mosaic Systems Inc., All rights reserved. (C++ library version)
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
	//-------------------------------------

	using System;
	using System.Collections;
	using System.Collections.Generic;

	//-------------------------------------

    /// <summary>Simple enumeration of the types of tokens that are supported in this set of parsing utilities.</summary>
    public enum TokenType
    {
        ToNextWhiteSpace,		// span anything except whitespace
        AlphaNumeric,			// span seq of letters and numbers
        SimpleFileName,			// span alphanumeric, '-', '_' and '.'
    }

    /// <summary>
    /// This is a helper object that represents an string with an index location into the string.  
    /// This object provides a number of helper methods, some static, that may be used with the object to perform common string parsing functions.
    /// These helper methods are generally implemented to leave the object unmofied if the requested operation fails and to update the position when it succeeds.
    /// This allows the helper methods to be used in chains and/or trees in order to perform more complex parsing functions.
    /// </summary>
    /// <remarks>
    /// Please note that this object is implemented as a struct so as to minimize generation of garbage for local methods that create and use
    /// one of these objects.  To pass this object into method tree's, please use the ref notation so that the lower levels of the tree will
    /// use the original object rather than a copy of it.
    /// </remarks>
	public struct StringScanner
    {
        //-----------------------------------------------------------------
        #region private fields

        /// <summary>Stores a reference to the original given string (or null if none)</summary>
        private string str;

        /// <summary>Stores the index into the str string.</summary>
		private int idx;

        #endregion

        //-----------------------------------------------------------------
        #region Constructors

        /// <summary>Standard constructor.  Gives a scanner positioned at the first character in the given string.</summary>
        public StringScanner(string s) : this() { Str = s; }

        /// <summary>Constructor.  Gives a new scanner at a specified position in the given string.</summary>
        public StringScanner(string s, int i) : this() { Str = s; Idx = i; }

        /// <summary>Copy constructor.  Gives a scanner with the same string and position as the given scanner.</summary>
        public StringScanner(StringScanner rhs) : this(rhs.str, rhs.idx) {}

        #endregion

        //-----------------------------------------------------------------
        #region public state accessor properties, some are settable

        /// <summary>Gives access to the string that is currently referenced by this object.  May be set.  Setter also resets the current position.  Getter returns empty string if there is no contained string.</summary>
        public string Str { get { return (str != null ? str : string.Empty); } set { str = (value != null ? value : string.Empty); idx = 0; } }

        /// <summary>Gives acces to the current index location in the contained string.  May be set</summary>
        public int Idx { get { return idx; } set { idx = value; } }

        /// <summary>Returns true if there are no more remaining characters in the string at the current position or if the current position is otherwise invalid.</summary>
        public bool IsAtEnd { get { return (str == null || idx >= str.Length || idx < 0); } }

        /// <summary>Returns true if the current index position referes to a legal character in the contained string</summary>
        public bool IsIdxValid { get { return (!IsAtEnd); } }

        /// <summary>Returns the character at the currently indexed scan position or the null character if the current index is not, or is no longer, valid.</summary>
        public char Char { get { return (!IsAtEnd ? str[idx] : (char)0); } }

        /// <summary>Returns the number of characters that remain in the contained string to its end or 0 if the current index position is not, or is no longer, valid</summary>
        public int NumChars { get { return (!IsAtEnd ? (str.Length - idx) : 0); } }

        /// <summary>Returns a new string containing the remaining characters in the contained string from the current position to its end (or the empty string if there are none)</summary>
        public string Rest { get { return (IsAtEnd ? String.Empty : (idx == 0 ? Str : Str.Substring(idx))); } }

        /// <summary>static Operator that may be used to increment the current position.  Does nothing if the current position is at (or past) the last position in the current string.</summary>
        public static StringScanner operator ++(StringScanner ss) { if (!ss.IsAtEnd) ss.Idx++; return ss; }

        #endregion

        #region Related methods

        /// <summary>Sets the current position back to the start of the string.</summary>
        public void Rewind() { Idx = 0; }

        #endregion

        //-------------------------------------
        #region Whitespace traversal functions

        public void SkipOverWhiteSpace()
        {
            while (IsIdxValid && Char.IsWhiteSpace(str[idx]))
                idx++;
        }

        public void SkipToNextWhiteSpace()
        {
            while (IsIdxValid && !Char.IsWhiteSpace(str[idx]))
                Idx++;
        }

        #endregion

        //-------------------------------------
        #region Token match and extract functions

        public static bool IsValidTokenChar(Char c, TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.ToNextWhiteSpace: return !Char.IsWhiteSpace(c);
                case TokenType.AlphaNumeric: return (Char.IsLetterOrDigit(c));
                case TokenType.SimpleFileName: return (Char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
                default: return false;
            }
        }

        public static bool IsValidTokenEndChar(Char c, TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.ToNextWhiteSpace: return Char.IsWhiteSpace(c);
                case TokenType.AlphaNumeric:
                case TokenType.SimpleFileName: return !IsValidTokenChar(c, TokenType.SimpleFileName);
                default: return false;
            }
        }


        public bool MatchToken(string token) { return MatchToken(token, true, true, true); }
        public bool MatchToken(string token, bool skipTrailingWhiteSpace, bool requireTokenEnd) { return MatchToken(token, true, skipTrailingWhiteSpace, requireTokenEnd); }
        public bool MatchToken(string token, bool skipLeadingWhiteSpace, bool skipTrailingWhiteSpace, bool requireTokenEnd)
        {
            StringScanner localScan = this;
            StringScanner tokenScan = new StringScanner(token);

            if (skipLeadingWhiteSpace)
                localScan.SkipOverWhiteSpace();

            // advance over both token and scan string until a mismatch is found
            //	or we reach the end of the token

            while (localScan.IsIdxValid && tokenScan.IsIdxValid && localScan.Char == tokenScan.Char)
            {
                localScan.Idx++;
                tokenScan.Idx++;
            }

            // match failed if we did not reach end of specified token
            //	or if the first charater after the token in the scan string
            //	could be interpreted as part of an normal identifier

            if (!tokenScan.IsAtEnd)
                return false;

            if (requireTokenEnd && !localScan.IsAtEnd && !IsValidTokenEndChar(localScan.Char, TokenType.SimpleFileName))
                return false;

            Idx = localScan.Idx;

            if (skipTrailingWhiteSpace)
                SkipOverWhiteSpace();

            return true;
        }

        public bool ExtractToken(out string tokenStr) { return ExtractToken(out tokenStr, TokenType.ToNextWhiteSpace, true, true, true); }
        public bool ExtractToken(out string tokenStr, TokenType tokenType) { return ExtractToken(out tokenStr, tokenType, true, true, true); }
        public bool ExtractToken(out string tokenStr, TokenType tokenType, bool skipLeadingWhiteSpace, bool skipTrailingWhiteSpace) { return ExtractToken(out tokenStr, tokenType, skipLeadingWhiteSpace, skipTrailingWhiteSpace, true); }
        public bool ExtractToken(out string tokenStr, TokenType tokenType, bool skipLeadingWhiteSpace, bool skipTrailingWhiteSpace, bool requireTokenEnd)
        {
            StringScanner localScan = this;

            if (!localScan.IsIdxValid)
            {
                tokenStr = string.Empty;
                return false;
            }

            if (skipLeadingWhiteSpace)
                localScan.SkipOverWhiteSpace();

            StringScanner tokenStart = localScan;

            // advance to end of token (or end of string) based on token type

            for (; !localScan.IsAtEnd && IsValidTokenChar(localScan.Char, tokenType); localScan++) { }

            // calculate the number of charactes we have traversed
            int tokenLen = (localScan.Idx - tokenStart.Idx);

            // determine if we found the end
            bool haveValidTokenEnd = (localScan.IsAtEnd || IsValidTokenEndChar(localScan.Char, tokenType));
            bool tokenIsValid = ((tokenLen > 0) && (!requireTokenEnd || haveValidTokenEnd));
            if (!tokenIsValid)
            {
                tokenStr = string.Empty;
                return false;
            }

            // save the token (or the fraction thereof)

            tokenStr = Str.Substring(tokenStart.Idx, tokenLen);

            Idx = localScan.Idx;

            if (skipTrailingWhiteSpace)
                SkipOverWhiteSpace();

            return true;
        }

        #endregion

        //-------------------------------------

        #region debugging - support hover over quick view

        public override string ToString()
        {
            return Utils.Fcns.CheckedFormat("Scanner:@{0},[{1}]", Idx, Rest);
        }

        #endregion

        //-----------------------------------------------------------------
        #region Map use functions

        public bool ParseTokenAndMapValueByName<ItemType>(Dictionary<string, ItemType> map, out ItemType value) { return ParseTokenAndMapValueByName<ItemType>(map, out value, true); }
        public bool ParseTokenAndMapValueByName<ItemType>(Dictionary<string, ItemType> map, out ItemType value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScanner = this;
            string tokenStr = string.Empty;
            bool success = localScanner.ExtractToken(out tokenStr, TokenType.ToNextWhiteSpace, true, skipTrailingWhiteSpace);
            success = FindTokenValueByName(tokenStr, map, out value) && success;

            if (success)
                Idx = localScanner.Idx;

            return success;
        }

        public static bool FindTokenNameByValue<ItemType>(ItemType valueToFind, Dictionary<string, ItemType> map, out string tokenStr) { return FindTokenNameByValue<ItemType>(valueToFind, map, out tokenStr, string.Empty); }
        public static bool FindTokenNameByValue<ItemType>(ItemType valueToFind, Dictionary<string, ItemType> map, out string tokenStr, string notFoundStr)
        {
            if (!map.ContainsValue(valueToFind))
            {
                tokenStr = notFoundStr;
                return false;
            }

            foreach (KeyValuePair<string, ItemType> pair in map)
            {
                if (pair.Value.Equals(valueToFind))
                {
                    tokenStr = pair.Key;
                    return true;
                }
            }

            tokenStr = notFoundStr;
            return false;
        }

        public static bool FindTokenValueByName<ItemType>(string tokenStr, Dictionary<string, ItemType> map, out ItemType value)
        {
            return map.TryGetValue(tokenStr, out value);
        }

        public static string FindTokenNameByValue<ItemType>(ItemType valueToFind, Dictionary<string, ItemType> map) { return FindTokenNameByValue(valueToFind, map, string.Empty); }
        public static string FindTokenNameByValue<ItemType>(ItemType valueToFind, Dictionary<string, ItemType> map, string notFoundStr)
        {
            string token;
            FindTokenNameByValue(valueToFind, map, out token, notFoundStr);
            return token;
        }

        #endregion

        //-------------------------------------
        #region Value Parsers

        public bool ParseValue(out string value) { return ParseValue(out value, true); }
        public bool ParseValue(out string value, bool skipTrailingWhiteSpace)
        {
            return ExtractToken(out value, TokenType.ToNextWhiteSpace, true, skipTrailingWhiteSpace);
        }

        public bool ParseValue(out Int32 value) { return ParseValue(out value, true); }
        public bool ParseValue(out Int32 value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScanner = this;
            string token;
            bool success = localScanner.ParseValue(out token, skipTrailingWhiteSpace);
            success = ParseValue(token, out value) && success;

            if (success)
                Idx = localScanner.Idx;

            return success;
        }
        public bool ParseValue(out UInt32 value) { return ParseValue(out value, true); }
        public bool ParseValue(out UInt32 value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScanner = this;
            string token;
            bool success = localScanner.ParseValue(out token, skipTrailingWhiteSpace);
            success = ParseValue(token, out value) && success;

            if (success)
                Idx = localScanner.Idx;

            return success;
        }

        public bool ParseValue(out bool value) { return ParseValue(out value, true); }
        public bool ParseValue(out bool value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScanner = this;
            string token;
            bool success = localScanner.ParseValue(out token, skipTrailingWhiteSpace);
            success = ParseValue(token, out value) && success;

            if (success)
                Idx = localScanner.Idx;

            return success;
        }

        public bool ParseValue(out Double value) { return ParseValue(out value, true); }
        public bool ParseValue(out Double value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScanner = this;
            string token;
            bool success = localScanner.ParseValue(out token, skipTrailingWhiteSpace);
            success = ParseValue(token, out value) && success;

            if (success)
                Idx = localScanner.Idx;

            return success;
        }

        public bool ParseValue<ItemType>(Dictionary<string, ItemType> map, out ItemType value) { return ParseValue(map, out value, true); }
        public bool ParseValue<ItemType>(Dictionary<string, ItemType> map, out ItemType value, bool skipTrailingWhiteSpace)
        {
            return ParseTokenAndMapValueByName<ItemType>(map, out value, skipTrailingWhiteSpace);
        }

        #region Related public static String Value Parsers

        public static bool ParseValue(string token, out Int32 value)
        {
            unchecked
            {
                if (Int32.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
                    return true;

                if ((token.StartsWith("0x") || token.StartsWith("0X"))
                    && Int32.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value)
                    )
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        public static bool ParseValue(string token, out UInt32 value)
        {
            unchecked
            {
                if (UInt32.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
                    return true;

                if ((token.StartsWith("0x") || token.StartsWith("0X"))
                    && UInt32.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value)
                    )
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        public static bool ParseValue(string token, out bool value)
        {
            bool success = FindTokenValueByName<bool>(token, BooleanXmlAttributeTokenValueList, out value);

            return success;
        }

        public static bool ParseValue(string token, out Double value)
        {
            bool success = Double.TryParse(token, out value);

            return success;
        }

        #endregion

        #region Boolean token value maps

        public static readonly Dictionary<string, bool> BooleanXmlAttributeTokenValueList = new Dictionary<string, bool>()
		{
			{"1", true}, 
			{"0", false},
			{"t", true}, 
			{"T", true}, 
			{"true", true}, 
			{"True", true}, 
			{"TRUE", true}, 
			{"f", false}, 
			{"F", false}, 
			{"false", false}, 
			{"False", false}, 
			{"FALSE", false}
		};

        #endregion

        #endregion

        //-------------------------------------
        #region simple Xml attribute Parsers

        public bool ParseXmlAttribute(string attribName, out string value) { return ParseXmlAttribute(attribName, out value, true); }
        public bool ParseXmlAttribute(string attribName, out string value, bool skipTrailingWhiteSpace)
        {
            bool success = true;
            StringScanner localScan = this;
            char termChar = (char)0;

            success = localScan.MatchToken(attribName);
            if (localScan.MatchToken("=\"", true, false))
                termChar = '"';
            else if (localScan.MatchToken("=\'", true, false))
                termChar = '\'';
            else
                success = false;

            StringScanner valueStart = localScan;

            while (localScan.IsIdxValid && localScan.Char != termChar)
                localScan.Idx++;

            StringScanner valueEnd = localScan;

            string termStr = new string(termChar, 1);
            success = localScan.MatchToken(termStr, skipTrailingWhiteSpace, true) && success;

            int valueLen = valueEnd.Idx - valueStart.Idx;
            value = valueStart.Str.Substring(valueStart.Idx, valueLen);

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        public bool ParseXmlAttribute(string attribName, out Int32 value) { return ParseXmlAttribute(attribName, out value, true); }
        public bool ParseXmlAttribute(string attribName, out Int32 value, bool skipTrailingWhiteSpace)
        {
            string attribValue;
            bool success = ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            return success;
        }

        public bool ParseXmlAttribute(string attribName, out UInt32 value) { return ParseXmlAttribute(attribName, out value, true); }
        public bool ParseXmlAttribute(string attribName, out UInt32 value, bool skipTrailingWhiteSpace)
        {
            string attribValue;
            bool success = ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            return success;
        }

        public bool ParseXmlAttribute(string attribName, out bool value) { return ParseXmlAttribute(attribName, out value, true); }
        public bool ParseXmlAttribute(string attribName, out bool value, bool skipTrailingWhiteSpace)
        {
            string attribValue;
            bool success = ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            return success;
        }

        public bool ParseXmlAttribute(string attribName, out Double value) { return ParseXmlAttribute(attribName, out value, true); }
        public bool ParseXmlAttribute(string attribName, out Double value, bool skipTrailingWhiteSpace)
        {
            string attribValue;
            bool success = ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            return success;
        }

        public bool ParseXmlAttribute<ItemType>(string attribName, Dictionary<string, ItemType> map, out ItemType value) { return ParseXmlAttribute(attribName, map, out value, true); }
        public bool ParseXmlAttribute<ItemType>(string attribName, Dictionary<string, ItemType> map, out ItemType value, bool skipTrailingWhiteSpace)
        {
            string attribValue;
            bool success = ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            StringScanner localScan = new StringScanner(attribValue);
            success = FindTokenValueByName<ItemType>(attribValue, map, out value) && success;

            return success;
        }

        #endregion

        //-------------------------------------
    }
}
//-----------------------------------------------------------------
//-----------------------------------------------------------------
