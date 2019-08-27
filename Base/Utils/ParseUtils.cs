//-------------------------------------------------------------------
/*! @file ParseUtils.cs
 *  @brief Provides definitions and implementations of a series of Parsing related utility functions.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2002 Mosaic Systems Inc.  (C++ library version)
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace MosaicLib.Utils
{
	//-------------------------------------

    /// <summary>
    /// Simple enumeration of the types of tokens that are supported in this set of parsing utilities.
    /// <para/>ToNextWhiteSpace, ToNextTokenSeparator, AlphaNumeric, SimpleFileName, SimpleFilePath, HexDigits, NumericsDigits, NumericFloatDigits
    /// </summary>
    public enum TokenType
    {
        /// <summary>span anything except whitespace</summary>
        ToNextWhiteSpace,
        /// <summary>span anything except whitespace, ',', and '|'</summary>
        ToNextTokenSeparator,
        /// <summary>span seq of letters and numbers</summary>
        AlphaNumeric,
        /// <summary>span alphanumeric, '-', '_' and '.'</summary>
        SimpleFileName,
        /// <summary>span alphanumeric, '-', '_', '.', '\\', '/', ':'</summary>
        SimpleFilePath,
        /// <summary>span numeric and letters 'a'-'f' and 'A'-'F'</summary>
        HexDigits,
        /// <summary>span sequence of numeric digits</summary>
        NumericDigits,
        /// <summary>span a sequence of numeric digits, '.', '-', '+', 'e' and 'E'</summary>
        NumericFloatDigits,
    }

    /// <summary>
    /// This is a helper object that represents an string with an index location into the string.  
    /// This object provides a number of helper methods, some static, that may be used with the object to perform common string parsing functions.
    /// These helper methods are generally implemented to leave the object unmodified if the requested operation fails and to update the position when it succeeds.
    /// This allows the helper methods to be used in chains and/or trees in order to perform more complex parsing functions.
    /// </summary>
    /// <remarks>
    /// Please note that this object is implemented as a struct so as to minimize generation of garbage for local methods that create and use
    /// one of these objects.  To pass this object into method tree's, please use the ref notation so that the lower levels of the tree will
    /// use the original object rather than a copy of it.
    /// </remarks>
	public struct StringScanner : IEquatable<StringScanner>
    {
        #region private fields

        /// <summary>Stores a reference to the original given string (or null if none - this is also the default value)</summary>
        private string str;

        /// <summary>Stores the current scan index.</summary>
        private int idx;

        #endregion

        #region Constructors

        /// <summary>Standard Constructor.  Gives a new scanner at a specified position (defaults to 0 - the first character) in the given string (null is mapped to empty automatically).</summary>
        public StringScanner(string str, int idx = 0) 
            : this() 
        {
            Str = str; 
            Idx = idx;
        }

        /// <summary>Copy constructor.  Gives a scanner with the same string and position as the given <paramref name="other"/> scanner.</summary>
        public StringScanner(StringScanner other) 
            : this() 
        {
            str = other.str;
            idx = other.idx;
        }

        #endregion

        #region public state accessor properties, some are settable, Rewind, Equals

        /// <summary>
        /// Gives get/set access to the string that is currently referenced by this object.  
        /// Setter accepts the given string and resets the current position to the start of the string (Idx = 0).  
        /// Getter returns empty string if there is no contained string.
        /// </summary>
        public string Str 
        { 
            get { return str.MapNullToEmpty(); } 
            set { str = value; idx = 0; } 
        }

        /// <summary>Get/Set: gives the current index location in the contained string.</summary>
        public int Idx { get { return idx; } set { idx = value; } }

        /// <summary>Returns true if there are no more remaining characters in the string at the current position or if the current position is otherwise invalid.</summary>
        public bool IsAtEnd { get { return (!IsIdxValid); } }

        /// <summary>Returns true if the current index position referes to a legal character in the contained string, and the string is not null</summary>
        public bool IsIdxValid { get { return (str != null && (idx >= 0 && idx < str.Length)); } }

        /// <summary>Returns the number of characters that remain in the contained string to its end or 0 if the current index position is not, or is no longer, valid</summary>
        public int NumChars { get { return (!IsAtEnd ? (str.Length - Idx) : 0); } }

        /// <summary>Returns the character at the currently indexed scan position or the null character if the current index is not, or is no longer, valid.</summary>
        public char Char { get { return (IsIdxValid ? str[Idx] : (char)0); } }

        /// <summary>Returns a new string containing the remaining characters in the contained string from the current position to its end (or the empty string if there are none)</summary>
        public string Rest { get { return (!IsIdxValid ? String.Empty : (Idx == 0 ? Str : Str.Substring(Idx))); } }

        /// <summary>static Operator that may be used to increment the current position.  Does nothing if the current position is at (or past) the last position in the current string.</summary>
        public static StringScanner operator ++(StringScanner ss) 
        { 
            if (!ss.IsAtEnd) 
                ss.Idx++; 

            return ss; 
        }

        /// <summary>Sets the current position back to the start of the string.</summary>
        public void Rewind() 
        { 
            Idx = 0;
        }

        /// <summary>
        /// Returns true if this StringScanner has identical contents to the <paramref name="other"/> StringScanner
        /// </summary>
        public bool Equals(StringScanner other)
        {
            return (str == other.str
                    && idx == other.idx
                    );
        }

        #endregion

        #region Whitespace traversal functions

        /// <summary>
        /// Advances the Idx until the current Char is no longer a WhiteSpace char or the end or string is reached.
        /// <para/>Returns "this" to allow caller to use call chaining, such as in the constructor.
        /// </summary>
        public StringScanner SkipOverWhiteSpace()
        {
            while (IsIdxValid && Char.IsWhiteSpace(str[Idx]))
                Idx++;

            return this;
        }

        /// <summary>
        /// Advances the Idx until the current Char is a WhiteSpace char or the end of string is reached.
        /// <para/>Returns "this" to allow caller to use call chaining, such as in the constructor.
        /// </summary>
        public StringScanner SkipToNextWhiteSpace()
        {
            while (IsIdxValid && !Char.IsWhiteSpace(str[Idx]))
                Idx++;

            return this;
        }

        #endregion

        #region Token match and extract functions IsValidTokenChar, IsValidTokenEndChar, MatchToken, ExtractToken

        /// <summary>
        /// Returns true if the given char is a valid char in the set of chars defined by the tokenType.
        /// TokenType.ToNextWhiteSpace accepts all characters except whitespace characters.
        /// TokenType.AlphaNumeric accepts all letters and digits.
        /// TokenType.SimpleFileName accepts all AlphaNumeric and adds '-', '_' and '.'
        /// TokenType.SimpleFilePath accepts all SimpleFileName and adds '\\', '/' and ':'
        /// </summary>
        public static bool IsValidTokenChar(Char c, TokenType tokenType)
        {
            return c.IsValidTokenChar(tokenType);
        }

        /// <summary>
        /// Returns true if the given char is a valid end of token char. 
        /// For all TokenType's this reduces to Char.IsWhiteSpace(c)
        /// </summary>
        public static bool IsValidTokenEndChar(Char c, TokenType tokenType)
        {
            return c.IsValidTokenEndChar(tokenType);
        }

        /// <summary>Determines if the current position in the Str matches the given token and returns true if so.  When returning false, the scanner Idx is not moved.  SkipsLeadingWhiteSpace, TokenType.SimpleFileName</summary>
        /// <param name="token">Gives the string that is being tested to determine if the current Idx position in Str matches, or not.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after a match is found.  Set to false to prevent this.</param>
        /// <param name="requireTokenEnd">Set to true to require that the character after the match must be a TokenEnd character according to the TokenType.SimpleFileName rules.</param>
        /// <remarks>NOTE: this signature cannot be removed as its parameters are not in the same order as the fully specified version is that supports use of optional parameters.</remarks>
        public bool MatchToken(string token, bool skipTrailingWhiteSpace, bool requireTokenEnd)
        {
            return MatchToken(token, skipLeadingWhiteSpace: true, skipTrailingWhiteSpace: skipTrailingWhiteSpace, requireTokenEnd: requireTokenEnd, tokenEndType: TokenType.SimpleFileName);
        }

        /// <summary>Determines if the current position in the Str matches the given token and returns true if so.  When returning false, the scanner Idx is not moved.</summary>
        /// <param name="token">Gives the string that is being tested to determine if the current Idx position in Str matches, or not.</param>
        /// <param name="skipLeadingWhiteSpace">Set to true to allow the scanner to advance over leading whitespace before checking if a match is found.  Set to false to prevent this.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after a match is found.  Set to false to prevent this.</param>
        /// <param name="requireTokenEnd">Set to true to require that the character after the match must be a TokenEnd character according to the selected tokenEndType rules.</param>
        /// <param name="tokenEndType">Defines the TokenType that is used for token end checking when the requireTokenEnd parameter is true.</param>
        /// <remarks>NOTE: the order of these parameters cannot be changed as this method is used with both explicitly and implicitly defined parameter passing order.</remarks>
        public bool MatchToken(string token, bool skipLeadingWhiteSpace = true, bool skipTrailingWhiteSpace = true, bool requireTokenEnd = true, TokenType tokenEndType = TokenType.SimpleFileName)
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

            if (requireTokenEnd && !localScan.IsAtEnd && !IsValidTokenEndChar(localScan.Char, tokenEndType))
                return false;

            Idx = localScan.Idx;

            if (skipTrailingWhiteSpace)
                SkipOverWhiteSpace();

            return true;
        }

        /// <summary>
        /// Attempts to extract a non-empty sequence of characters at the current position using the given TokenType to determine when the sequence of characters must first end.  
        /// <para/>returns the extracted token or String.Empty if no token could be successfully extracted.
        /// <para/>SkipLeadingWhiteSpace, SkipTrailingWhiteSpace, RequireTokenEnd
        /// </summary>
        /// <remarks>NOTE: this signature cannot be removed as its parameters are not in the same order as the fully specified version is that supports use of optional parameters.</remarks>
        public string ExtractToken(TokenType tokenType, bool requireTokenEnd)
        {
            string token;
            ExtractToken(out token, tokenType: tokenType, skipLeadingWhiteSpace: true, skipTrailingWhiteSpace: true, requireTokenEnd: requireTokenEnd);
            return token;
        }

        /// <summary>
        /// Attempts to extract a non-empty sequence of characters at the current position using the default TokenType to determine when the sequence of characters must first end.  
        /// <para/>returns the extracted token or String.Empty if no token could be successfully extracted.
        /// <para/>TokenType.ToNextWhiteSpace, SkipLeadingWhiteSpace, SkipTrailingWhiteSpace, RequireTokenEnd
        /// </summary>
        /// <remarks>NOTE: the order of these parameters cannot be changed as this method is used with both explicitly and implicitly defined parameter passing order.</remarks>
        public string ExtractToken(TokenType tokenType = TokenType.ToNextWhiteSpace, bool skipLeadingWhiteSpace = true, bool skipTrailingWhiteSpace = true, bool requireTokenEnd = true)
        {
            string token;
            ExtractToken(out token, tokenType: tokenType, skipLeadingWhiteSpace: skipLeadingWhiteSpace, skipTrailingWhiteSpace: skipTrailingWhiteSpace, requireTokenEnd: requireTokenEnd);
            return token;
        }

        /// <summary>
        /// Attempts to extract a non-empty sequence of characters at the current position using the given tokenType to determine when the sequence of characters must first end.  
        /// </summary>
        /// <param name="tokenStr">Out parameter the receives the contents of the token or String.Empty if no valid token was extracted.</param>
        /// <param name="tokenType">Defines the TokenType that is used to determine which characters can be collected into a contiguous character sequence as the extracted token.</param>
        /// <param name="skipLeadingWhiteSpace">If true the token extraction will only begin after skipping leading white space.</param>
        /// <param name="skipTrailingWhiteSpace">If true, trailing whitespace will also be skipped if a token was successfully extracted.</param>
        /// <param name="requireTokenEnd">If true then token extraction can only succeed if the next character after the end of the token is a valid TokenEnd character for the specified tokenType or if the end of the Str was reached.</param>
        /// <returns>true if the token was extracted, false otherwise.  The Idx is not moved if this method returns false.</returns>
        /// <remarks>NOTE: the order of these parameters cannot be changed as this method is used with both explicitly and implicitly defined parameter passing order.</remarks>
        public bool ExtractToken(out string tokenStr, TokenType tokenType = TokenType.ToNextWhiteSpace, bool skipLeadingWhiteSpace = true, bool skipTrailingWhiteSpace = true, bool requireTokenEnd = true)
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

            // calculate the number of characters we have traversed
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

        /// <summary>
        /// Extract sequential characters as a token until any of the chars in the given toDelimiterSet are found and return resulting token.
        /// If any of the indicated delimiter characters are found then the resulting token is extracted from the scan source string, and the method succeeeds and updates the current scan position.
        /// Otherwise the method fails and does not update the current scan position.
        /// when true keepDelimter causes the method to include the delimiter in the resulting token while when false keepDelimieter cause it to skip over and discard delimiter.
        /// When true, skipLeadingWhiteSpace and skipTrailingWhiteSpace causes the method to skip leading and/or traiing white space provided that the method was successful.
        /// </summary>
        public bool ExtractToken(out string tokenStr, ICollection<char> toDelimiterSet, bool keepDelimiter, bool skipLeadingWhiteSpace = true, bool skipTrailingWhiteSpace = true)
        {
            StringScanner localScan = this;

            if (!localScan.IsIdxValid || toDelimiterSet == null || toDelimiterSet.Count <= 0)
            {
                tokenStr = string.Empty;
                return false;
            }

            if (skipLeadingWhiteSpace)
                localScan.SkipOverWhiteSpace();

            StringScanner tokenStart = localScan;

            // advance to next delimiter, or end of string

            bool foundDelimter = false;
            for (; !localScan.IsAtEnd && !(foundDelimter = toDelimiterSet.Contains(localScan.Char)); localScan++)
            { }

            // calculate the number of characters we have traversed
            int tokenLen = (localScan.Idx - tokenStart.Idx);

            // determine if we found the end
            bool tokenIsValid = foundDelimter;
            if (!foundDelimter)
            {
                tokenStr = string.Empty;
                return false;
            }

            // save the token (or the fraction thereof)

            tokenStr = Str.Substring(tokenStart.Idx, tokenLen + keepDelimiter.MapToInt());

            if (foundDelimter)
                localScan++;

            Idx = localScan.Idx;

            if (skipTrailingWhiteSpace)
                SkipOverWhiteSpace();

            return true;
        }

        #endregion

        #region debugging - support hover over quick view

        /// <summary>Debugging assistant, returns the current Idx and the Rest of the string.</summary>
        public override string ToString()
        {
            return Utils.Fcns.CheckedFormat("Scanner:@{0},[{1}]", Idx, Rest);
        }

        #endregion

        //-----------------------------------------------------------------
        #region Map use functions

        /// <summary>Extracts an AlphaNumeric token from the scanner and uses the given Dictionary to find the ItemValue for the corresponding token.  SkipsLeadingWhiteSpace, RequiresTokenEnd, TokenType.AlphaNumeric</summary>
        /// <typeparam name="ItemType">Defines the Dictionary's TValue type used in the given map</typeparam>
        /// <param name="map">Gives the Dictionary of token/value pairs that will be used after extracting the next AlphaNumeric token.</param>
        /// <param name="value">This out parameter receives the mapped value or the default value if the no token or match was found.</param>
        /// <param name="skipTrailingWhiteSpace">If true, trailing whitespace will also be skipped if a token was successfully extracted.</param>
        /// <returns>true if the token was extracted and a matching Dictionary entry was found, false otherwise.  The Idx is not moved if this method returns false.</returns>
        public bool ParseTokenAndMapValueByName<ItemType>(Dictionary<string, ItemType> map, out ItemType value, bool skipTrailingWhiteSpace = true)
        {
            StringScanner localScanner = this;
            string tokenStr = string.Empty;
            bool success = localScanner.ExtractToken(out tokenStr, TokenType.AlphaNumeric, true, skipTrailingWhiteSpace);
            success = FindTokenValueByName(tokenStr, map, out value) && success;

            if (success)
                Idx = localScanner.Idx;

            return success;
        }

        /// <summary>
        /// Uses a reverse iterative search in the Dictionary to find the string key value for the corresponding given ItemType valueToFind.
        /// </summary>
        /// <typeparam name="ItemType">Defines the Dictionary's TValue type used in the given map</typeparam>
        /// <param name="valueToFind">Gives the ItemType value that the map will be searched to find the first entry that returns true from Object.Equals</param>
        /// <param name="map">Defines the Dictionary instance to use for this search</param>
        /// <param name="tokenStr">Is assigned the string value from the first found map entry for valueToFind on success.  Assigned to notFoundStr if valueToFind was not found.</param>
        /// <param name="notFoundStr">Defines the contents of the output string placed in tokenStr if the ItemType valueToFind was not found in the given map.</param>
        /// <returns>true if the ItemType valueToFind was found in the given map and false otherwise.</returns>
        public static bool FindTokenNameByValue<ItemType>(ItemType valueToFind, Dictionary<string, ItemType> map, out string tokenStr, string notFoundStr = "")
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

        /// <summary>
        /// Searches the given Dictionary object to find the given tokenStr key value.
        /// </summary>
        /// <typeparam name="ItemType">Defines the Dictionary's TValue type used in the given map</typeparam>
        /// <param name="tokenStr">Defines the token string to search for.</param>
        /// <param name="map">Defines the Dictionary instance to use for this search</param>
        /// <param name="value">Assigned to the ItemType value for the matching entry in the given map when tokenStr is found.  Otherwise is assigned to default(ItemType).</param>
        /// <returns>true if the given tokenStr was found in the given map or false otherwise.</returns>
        public static bool FindTokenValueByName<ItemType>(string tokenStr, Dictionary<string, ItemType> map, out ItemType value)
        {
            return map.TryGetValue(tokenStr ?? String.Empty, out value);
        }

        /// <summary>
        /// Uses a reverse iterative search in the Dictionary to find the string key value for the corresponding given ItemType valueToFind and returns it.
        /// </summary>
        /// <typeparam name="ItemType">Defines the Dictionary's TValue type used in the given map</typeparam>
        /// <param name="valueToFind">Gives the ItemType value that the map will be searched to find the first entry that returns true from Object.Equals</param>
        /// <param name="map">Defines the Dictionary instance to use for this search</param>
        /// <param name="notFoundStr">Defines the contents of the output string placed in tokenStr if the ItemType valueToFind was not found in the given map.</param>
        /// <returns>the string value from the first found map entry for valueToFind on success or notFoundStr if valueToFind was not found in map.</returns>
        public static string FindTokenNameByValue<ItemType>(ItemType valueToFind, Dictionary<string, ItemType> map, string notFoundStr = "")
        {
            string token;
            FindTokenNameByValue(valueToFind, map, out token, notFoundStr);
            return token;
        }

        #endregion

        //-------------------------------------
        #region Value Parsers

        /// <summary>
        /// Extracts a TokenType.ToNextWhiteSpace token and assigns it to the given output value parameter.  
        /// SkipLeadingWhiteSpace, RequireTokenEnd, SkipTrailingWhiteSpace
        /// </summary>
        /// <param name="value">Receives the Extracted token value or String.Empty if no valid token was extracted.</param>
        /// <returns>true if the ExtractToken method succeeds or false otherwise.</returns>
        public bool ParseValue(out string value)
        {
            return ParseValue(out value, skipTrailingWhiteSpace: true);
        }

        /// <summary>
        /// Extracts a TokenType.ToNextWhiteSpace token and assigns it to the given output value parameter.  
        /// SkipLeadingWhiteSpace, RequireTokenEnd
        /// </summary>
        /// <param name="value">Receives the Extracted token value or String.Empty if no valid token was extracted.</param>
        /// <param name="skipTrailingWhiteSpace">If true, trailing whitespace will also be skipped if a token was successfully extracted.</param>
        /// <returns>true if the ExtractToken method succeeds or false otherwise.</returns>
        public bool ParseValue(out string value, bool skipTrailingWhiteSpace)
        {
            return ParseValue(out value, parseFailedResult: string.Empty, skipTrailingWhiteSpace: skipTrailingWhiteSpace, tokenType: TokenType.ToNextWhiteSpace, requireTokenEnd: true);
        }

        /// <summary>
        /// Extracts a TokenType.SimpleFileName token and parses it using Int32.TryParse as a Decimal number or as a HexNumber if the token is prefixed with "0x", "0X", or "$".
        /// RequireTokenEnd, SkipTrailingWhiteSpace
        /// </summary>
        /// <param name="value">assigned to the parsed value or to zero if the extraction or parse were not successful.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue(out Int32 value)
        {
            return ParseValue(out value, skipTrailingWhiteSpace: true);
        }

        /// <summary>
        /// Extracts a TokenType.SimpleFileName token and parses it using Int32.TryParse as a Decimal number or as a HexNumber if the token is prefixed with "0x", "0X", or "$".
        /// </summary>
        public bool ParseValue(out Int32 value, bool skipTrailingWhiteSpace, bool requireTokenEnd = false)
        {
            return ParseValue(out value, parseFailedResult: default(Int32), skipTrailingWhiteSpace: skipTrailingWhiteSpace, tokenType: TokenType.ToNextTokenSeparator, requireTokenEnd: requireTokenEnd);
        }

        /// <summary>
        /// Extracts a TokenType.SimpleFileName token and parses it using Uint32.TryParse as a Decimal number or as a HexNumber if the token is prefixed with "0x", "0X", or "$".
        /// SkipTrailingWhiteSpace
        /// </summary>
        /// <param name="value">assigned to the parsed value or to zero if the extraction or parse were not successful.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue(out UInt32 value)
        {
            return ParseValue(out value, skipTrailingWhiteSpace: true);
        }

        /// <summary>
        /// Extracts a TokenType.SimpleFileName token and parses it using Uint32.TryParse as a Decimal number or as a HexNumber if the token is prefixed with "0x", "0X", or "$".
        /// </summary>
        public bool ParseValue(out UInt32 value, bool skipTrailingWhiteSpace, bool requireTokenEnd = false)
        {
            return ParseValue(out value, parseFailedResult: default(UInt32), skipTrailingWhiteSpace: skipTrailingWhiteSpace, tokenType: TokenType.ToNextTokenSeparator, requireTokenEnd: requireTokenEnd);
        }

        /// <summary>
        /// Extracts a TokenType.AlphaNumeric token and parses it using the static bool ParseValue method which attempts to covert the token to a boolean value using the
        /// <see cref="BooleanXmlAttributeTokenValueList"/> dictionary.
        /// SkipLeadingWhiteSpace, RequireTokenEnd, SkipTrailingWhiteSpace
        /// </summary>
        /// <param name="value">assigned to the parsed value or to false if the extraction or parse were not successful.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue(out bool value)
        {
            return ParseValue(out value, skipTrailingWhiteSpace: true);
        }

        /// <summary>
        /// Extracts a TokenType.AlphaNumeric token and parses it using the static bool ParseValue method which attempts to covert the token to a boolean value using the
        /// <see cref="BooleanXmlAttributeTokenValueList"/> dictionary.
        /// SkipLeadingWhiteSpace, RequireTokenEnd
        /// </summary>
        /// <param name="value">assigned to the parsed value or to false if the extraction or parse were not successful.</param>
        /// <param name="skipTrailingWhiteSpace">If true, trailing whitespace will also be skipped if a token was successfully extracted.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue(out bool value, bool skipTrailingWhiteSpace)
        {
            return ParseValue(out value, parseFailedResult: default(bool), skipTrailingWhiteSpace: skipTrailingWhiteSpace, tokenType: TokenType.AlphaNumeric);
        }

        /// <summary>
        /// Extracts a TokenType.ToNextWhiteSpace or TokenType.NumericFloatDigits (based on skipTrailingWhiteSpace value) token and parses it using the static Double ParseValue method which generally attempts to covert the token to a Double value using the
        /// Double.TryParse method.
        /// SkipLeadingWhiteSpace, RequireTokenEnd, SkipTrailingWhiteSpace
        /// </summary>
        /// <param name="value">assigned to the parsed value or to false if the extraction or parse were not successful.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue(out Double value)
        {
            return ParseValue(out value, skipTrailingWhiteSpace: true);
        }

        /// <summary>
        /// Extracts a TokenType.ToNextWhiteSpace or TokenType.NumericFloatDigits (based on skipTrailingWhiteSpace value) token and parses it using the static Double ParseValue method which generally attempts to covert the token to a Double value using the
        /// Double.TryParse method.
        /// SkipLeadingWhiteSpace, RequireTokenEnd
        /// </summary>
        /// <param name="value">assigned to the parsed value or to false if the extraction or parse were not successful.</param>
        /// <param name="skipTrailingWhiteSpace">If true, trailing whitespace will also be skipped if a token was successfully extracted.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue(out Double value, bool skipTrailingWhiteSpace)
        {
            return ParseValue(out value, parseFailedResult: default(Double), skipTrailingWhiteSpace: skipTrailingWhiteSpace, tokenType: (skipTrailingWhiteSpace ? TokenType.ToNextWhiteSpace : TokenType.NumericFloatDigits));
        }

        /// <summary>
        /// Extracts a TokenType.AlphaNumeric token and parses it using the static bool ParseValue method which attempts to covert the token to an ItemType value using the
        /// given map dictionary.
        /// SkipLeadingWhiteSpace, RequireTokenEnd, SkipTrailingWhiteSpace
        /// </summary>
        /// <param name="map">Defines the dictionary in which the token is to be found.</param>
        /// <param name="value">assigned to the parsed value or to default(ItemType) if the extraction or token lookup were not successful.</param>
        /// <param name="skipTrailingWhiteSpace">If true, trailing whitespace will also be skipped if a token was successfully extracted.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue<ItemType>(Dictionary<string, ItemType> map, out ItemType value, bool skipTrailingWhiteSpace = true)
        {
            return ParseTokenAndMapValueByName<ItemType>(map, out value, skipTrailingWhiteSpace);
        }

        /// <summary>
        /// Extracts a TokenType.ToNextWhiteSpace token and parses it as one of the supported value types:
        ///  string, bool, float, double, sbyte, short, int, long, byte, ushort, uint, ulong, or an Enumeration Type using the MosaicLib.Utils.Enum.TryParse{EnumT} method, 
        /// <para/>returns the successfully parsed value or the given default parseFailedResults if the parse was not successful.
        /// <para/>TokenType.ToNextWhiteSpace is used so that the comma character can be used within the enum string token value so as to support flag enums
        /// <para/>Integer values can be represented as decimal values or as hex values when preceeded with "0x", "0X", or "$".
        /// <para/>SkipLeadingWhiteSpace, !RequireTokenEnd
        /// </summary>
        /// <typeparam name="ValueType">Gives the ValueType to parse.  Must be of type string, bool, float, double, sbyte, short, int, long, byte, ushort, uint, ulong, or System.Enum.</typeparam>
        /// <param name="parseFailedResult">Defines the value that will be assigned to the result if any part of the parse fails.</param>
        /// <param name="ignoreCase">If true, ignore case; otherwise, regard case.  Only relevant for System.Enum types.</param>
        /// <param name="skipTrailingWhiteSpace">If true, trailing whitespace will also be skipped if a token was successfully extracted.</param>
        public ValueType ParseValue<ValueType>(ValueType parseFailedResult, bool ignoreCase = false, bool skipTrailingWhiteSpace = true)
        {
            ValueType value;
            ParseValue(out value, parseFailedResult, ignoreCase, skipTrailingWhiteSpace);
            return value;
        }

        /// <summary>
        /// Extracts a TokenType.ToNextWhiteSpace token and parses it as one of the supported value types:
        ///  string, bool, float, double, sbyte, short, int, long, byte, ushort, uint, ulong, or an Enumeration Type using the MosaicLib.Utils.Enum.TryParse{EnumT} method, 
        /// <para/>TokenType.ToNextWhiteSpace is used so that the comma character can be used within the enum string token value so as to support flag enums
        /// <para/>Integer values can be represented as decimal values or as hex values when preceeded with "0x", "0X", or "$".
        /// <para/>SkipLeadingWhiteSpace, !RequireTokenEnd
        /// </summary>
        /// <typeparam name="ValueType">Gives the ValueType to parse.  Must be of type string, bool, float, double, sbyte, short, int, long, byte, ushort, uint, ulong, or System.Enum.</typeparam>
        /// <param name="value">assigned to the parsed value or to parseFailedResult if the token extraction or type specific parse was not successful.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue<ValueType>(out ValueType value)
        {
            return ParseValue(out value, parseFailedResult: default(ValueType));
        }

        /// <summary>
        /// Extracts a TokenType.ToNextWhiteSpace token and parses it as one of the supported value types:
        ///  string, bool, float, double, sbyte, short, int, long, byte, ushort, uint, ulong, or an Enumeration Type using the MosaicLib.Utils.Enum.TryParse{EnumT} method, 
        /// <para/>TokenType.ToNextWhiteSpace is used so that the comma character can be used within the enum string token value so as to support flag enums
        /// <para/>Integer values can be represented as decimal values or as hex values when preceeded with "0x", "0X", or "$".
        /// <para/>SkipLeadingWhiteSpace, !RequireTokenEnd
        /// </summary>
        /// <typeparam name="ValueType">Gives the ValueType to parse.  Must be of type string, bool, float, double, sbyte, short, int, long, byte, ushort, uint, ulong, or System.Enum.</typeparam>
        /// <param name="value">assigned to the parsed value or to parseFailedResult if the token extraction or type specific parse was not successful.</param>
        /// <param name="parseFailedResult">Defines the value that will be assigned to the result if any part of the parse fails.</param>
        /// <param name="ignoreCase">If true, ignore case; otherwise, regard case.  Only relevant for System.Enum types.</param>
        /// <param name="skipTrailingWhiteSpace">If true, trailing whitespace will also be skipped if a token was successfully extracted.</param>
        /// <param name="skipLeadingWhiteSpace">If true the token extraction will only begin after skipping leading white space.</param>
        /// <param name="tokenType">Defines the token type that will be used to slice off the next text portion to attempt to parse.</param>
        /// <param name="requireTokenEnd">If true then token extraction can only succeed if the next character after the end of the token is a valid TokenEnd character for the specified tokenType or if the end of the Str was reached.</param>
        /// <returns>true on success or false otherwise.</returns>
        public bool ParseValue<ValueType>(out ValueType value, ValueType parseFailedResult, bool ignoreCase = false, bool skipTrailingWhiteSpace = true, bool skipLeadingWhiteSpace = true, TokenType tokenType = TokenType.ToNextWhiteSpace, bool requireTokenEnd = false)
        {
            value = parseFailedResult;

            Type valueTypeType = typeof(ValueType);

            StringScanner localScanner = this;
            string token;
            bool success = localScanner.ExtractToken(out token, tokenType, skipLeadingWhiteSpace, skipTrailingWhiteSpace, requireTokenEnd);

            if (success)
            {
                if (valueTypeType.IsEnum)
                    success = MosaicLib.Utils.Enum.TryParse<ValueType>(token, out value, parseFailedResult, ignoreCase);
                else
                {
                    object valueObject = null;

                    if (valueTypeType == typeof(string))
                    {
                        success = true;
                        valueObject = token;
                    }
                    else if (valueTypeType == typeof(bool))
                    {
                        bool typedValue;
                        success = ParseValue(token, out typedValue);
                        valueObject = typedValue;
                    }
                    else if (valueTypeType == typeof(float))
                    {
                        float typedValue;
                        success = ParseValue(token, out typedValue);
                        valueObject = typedValue;
                    }
                    else if (valueTypeType == typeof(double))
                    {
                        double typedValue;
                        success = ParseValue(token, out typedValue);
                        valueObject = typedValue;
                    }
                    else if (valueTypeType == typeof(TimeSpan))
                    {
                        TimeSpan typedValue;
                        success = ParseValue(token, out typedValue);
                        valueObject = typedValue;
                    }
                    else if (valueTypeType == typeof(DateTime))
                    {
                        DateTime typedValue;
                        success = ParseValue(token, out typedValue);
                        valueObject = typedValue;
                    }
                    else
                    {
                        System.Globalization.NumberStyles numberStyle = System.Globalization.NumberStyles.Integer;
                        string tryParseValueFromStr = token;
                        if (token.StartsWith("0x") || token.StartsWith("0X"))
                        {
                            numberStyle = System.Globalization.NumberStyles.HexNumber;
                            tryParseValueFromStr = token.Substring(2);
                        }
                        else if (token.StartsWith("$"))
                        {
                            numberStyle = System.Globalization.NumberStyles.HexNumber;
                            tryParseValueFromStr = token.Substring(1);
                        }

                        if (valueTypeType == typeof(byte))
                        {
                            byte typedValue;
                            success = byte.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                        else if (valueTypeType == typeof(sbyte))
                        {
                            sbyte typedValue;
                            success = sbyte.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                        else if (valueTypeType == typeof(short))
                        {
                            short typedValue;
                            success = short.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                        else if (valueTypeType == typeof(int))
                        {
                            int typedValue;
                            success = int.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                        else if (valueTypeType == typeof(long))
                        {
                            long typedValue;
                            success = long.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                        else if (valueTypeType == typeof(long))
                        {
                            long typedValue;
                            success = long.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                        else if (valueTypeType == typeof(ushort))
                        {
                            ushort typedValue;
                            success = ushort.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                        else if (valueTypeType == typeof(uint))
                        {
                            uint typedValue;
                            success = uint.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                        else if (valueTypeType == typeof(ulong))
                        {
                            ulong typedValue;
                            success = ulong.TryParse(tryParseValueFromStr, numberStyle, invariantCultureFormatProvider, out typedValue);
                            valueObject = typedValue;
                        }
                    }

                    try
                    {
                        value = (ValueType)valueObject;
                    }
                    catch
                    {
                        success = false;
                    }
                }
            }

            if (success)
                Idx = localScanner.Idx;
            else
                value = parseFailedResult;

            return success;
        }


        #region Related public static String Value Parsers

        private static readonly NumberStyles defaultFloatingPointTryParseNumberStyles = (NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowExponent);
        private static readonly IFormatProvider invariantCultureFormatProvider = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>
        /// This method is a proxy for calling Int32.TryParse:
        ///     Converts the string representation of a number to its 32-bit signed integer equivalent.
        ///     A return value indicates whether the conversion succeeded or failed.
        ///     This method first attempts to parse the token as a simple decimal Integer.   If this fails then the method will determine
        ///     if the token starts with "0x", "0X", or "$" and if so it will attempt to parse the remainder of the token as a hexadecimal integer.
        /// </summary>
        /// <param name="token">A string containing a number to convert.</param>
        /// <param name="value">
        ///     When this method returns, contains the 32-bit signed integer value equivalent
        ///     to the number contained in token, if the conversion succeeded, or zero if the
        ///     conversion failed. The conversion fails if the s parameter is null, is not
        ///     in a format compliant with style, or represents a number less than System.Int32.MinValue
        ///     or greater than System.Int32.MaxValue. This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if token was converted successfully, or false otherwise.</returns>
        public static bool ParseValue(string token, out Int32 value)
        {
            token = token ?? string.Empty;

            unchecked
            {
                if (Int32.TryParse(token, System.Globalization.NumberStyles.Integer, invariantCultureFormatProvider, out value))
                    return true;

                if ((token.StartsWith("0x") || token.StartsWith("0X"))
                    && Int32.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, invariantCultureFormatProvider, out value)
                    )
                {
                    return true;
                }

                if (token.StartsWith("$")
                    && Int32.TryParse(token.Substring(1), System.Globalization.NumberStyles.HexNumber, invariantCultureFormatProvider, out value)
                    )
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// This method is a proxy for calling UInt32.TryParse:
        ///     Converts the string representation of a number to its 32-bit unsigned integer equivalent.
        ///     A return value indicates whether the conversion succeeded or failed.
        ///     This method first attempts to parse the token as a simple Integer if this fails then the method will determine
        ///     if the token starts with "0x", "0X", or "$" and if so it will attempt to parse the remainder of the token as a hexadecimal integer.
        /// </summary>
        /// <param name="token">A string containing a number to convert.</param>
        /// <param name="value">
        ///     When this method returns, contains the 32-bit unsigned integer value equivalent
        ///     to the number contained in token, if the conversion succeeded, or zero if the
        ///     conversion failed. The conversion fails if the s parameter is null, is not
        ///     in a format compliant with style, or represents a number less than System.UInt32.MinValue
        ///     or greater than System.UInt32.MaxValue. This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if token was converted successfully, or false otherwise.</returns>
        public static bool ParseValue(string token, out UInt32 value)
        {
            token = token ?? string.Empty;

            unchecked
            {
                if (UInt32.TryParse(token, System.Globalization.NumberStyles.Integer, invariantCultureFormatProvider, out value))
                    return true;

                if ((token.StartsWith("0x") || token.StartsWith("0X"))
                    && UInt32.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, invariantCultureFormatProvider, out value)
                    )
                {
                    return true;
                }

                if (token.StartsWith("$")
                    && UInt32.TryParse(token.Substring(1), System.Globalization.NumberStyles.HexNumber, invariantCultureFormatProvider, out value)
                    )
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Accepts the given token and attempts to find a matching entry in the <see cref="BooleanXmlAttributeTokenValueList"/> dictionary.  
        /// If a matching string is found in this dictionary then the value parameter is set to the corresponding boolean value and the method returns true.
        /// </summary>
        /// <param name="token">contains the string that shall be found in the <see cref="BooleanXmlAttributeTokenValueList"/> dictionary.</param>
        /// <param name="value">assigned to the value entry value from the dictionary when a match is found or false when no match is found.</param>
        /// <returns>True if a matching entry is found in the <see cref="BooleanXmlAttributeTokenValueList"/> dictionary, or false otherwise.</returns>
        public static bool ParseValue(string token, out bool value)
        {
            bool success = FindTokenValueByName<bool>(token, BooleanXmlAttributeTokenValueList, out value);

            return success;
        }

        /// <summary>
        /// This method is a proxy for calling Single.TryParse:
        ///     Converts the string representation of a number to its single-precision floating-point
        ///     number equivalent. A return value indicates whether the conversion succeeded
        ///     or failed.  This method uses a invariant culture FormatProvider.
        /// </summary>
        /// <param name="token">A string containing a number to convert.</param>
        /// <param name="value">
        ///     When this method returns, contains the single-precision floating-point number
        ///     equivalent to the token parameter, if the conversion succeeded, or zero if the
        ///     conversion failed.  The conversion fails if the token parameter is null, is not
        ///     a number in a valid format, or represents a number less than System.Single.MinValue
        ///     or greater than System.Single.MaxValue. (Inf, and NaN flavors are also accepted)
        /// </param>
        /// <returns>true if token was converted successfully, or false otherwise.</returns>
        public static bool ParseValue(string token, out Single value)
        {
            token = token ?? string.Empty;

            bool success = Single.TryParse(token, defaultFloatingPointTryParseNumberStyles, invariantCultureFormatProvider, out value);

            return success;
        }

        /// <summary>
        /// This method is a proxy for calling Double.TryParse:
        ///     Converts the string representation of a number to its double-precision floating-point
        ///     number equivalent. A return value indicates whether the conversion succeeded
        ///     or failed.  This method uses a invariant culture FormatProvider.
        /// </summary>
        /// <param name="token">A string containing a number to convert.</param>
        /// <param name="value">
        ///     When this method returns, contains the double-precision floating-point number
        ///     equivalent to the token parameter, if the conversion succeeded, or zero if the
        ///     conversion failed.  The conversion fails if the token parameter is null, is not
        ///     a number in a valid format, or represents a number less than System.Double.MinValue
        ///     or greater than System.Double.MaxValue. (Inf, and NaN flavors are also accepted)
        /// </param>
        /// <returns>true if token was converted successfully, or false otherwise.</returns>
        public static bool ParseValue(string token, out Double value)
        {
            token = token ?? string.Empty;

            bool success = Double.TryParse(token, defaultFloatingPointTryParseNumberStyles, invariantCultureFormatProvider, out value);

            return success;
        }

        /// <summary>
        /// This method attempts to parse the given token as a TimeSpan, first using Double.TryParse and then using TimeSpan.TryParse.
        /// Returns true on success or false otherwise.  
        /// If <paramref name="formatProvider"/> is null (the default) then this method will use the invariant culture format provider.
        /// </summary>
        public static bool ParseValue(string token, out TimeSpan value, IFormatProvider formatProvider = null)
        {
            double dValue;
            bool dValueParseSuccess = ParseValue(token, out dValue);

            if (dValueParseSuccess)
                value = TimeSpan.FromSeconds(dValue);
            else
                dValueParseSuccess = TimeSpan.TryParse(token, formatProvider ?? invariantCultureFormatProvider, out value);

            return dValueParseSuccess;
        }

        /// <summary>
        /// This method attempts to parse the given token as a DateTime using DateTime.TryParse.
        /// Returns true on success or false otherwise
        /// If <paramref name="formatProvider"/> is null (the default) then this method will use the invariant culture format provider.
        /// </summary>
        public static bool ParseValue(string token, out DateTime value, IFormatProvider formatProvider = null, DateTimeStyles dateTimeStyles = DateTimeStyles.None)
        {
            bool success = DateTime.TryParse(token, formatProvider ?? invariantCultureFormatProvider, dateTimeStyles, out value);

            return success;
        }

        #endregion

        #region Boolean token value maps

        /// <summary>
        /// This dictionary gives the list of accepted token strings for boolean values along with with actual boolean value that each one represents.
        /// </summary>
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

        #region ParseHexValue

        /// <summary>
        /// Attempts to parse a hexadecimal number at the current scanner position.  
        /// MinDigits=1, MaxDigits=8, SkipLeadingWhiteSpace, SkipTrailingWhiteSpace, RequireTokenEnd
        /// </summary>
        /// <param name="value">receives the parsed value or zero if the parse was not successful.</param>
        /// <returns>true if a valid Int32 hexadecimal number could be parsed, or false if not.  Does not move the scan position if the method returns false.</returns>
        public bool ParseHexValue(out Int32 value)
        {
            return ParseHexValue(out value, 1, 8, true, true, true);
        }

        /// <summary>Attempts to parse a hexadecimal number at the current scanner position.</summary>
        /// <param name="value">receives the parsed value or zero if the parse was not successful.</param>
        /// <param name="minDigits">Defines the minimum number of hex digits that the hex number can include</param>
        /// <param name="maxDigits">Defines the maximum number of hex digits that the hex number can include</param>
        /// <param name="skipLeadingWhiteSpace">Set to true to allow the scanner to advance over leading whitespace before attempting to parse.  Set to false to prevent this.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <param name="requireTokenEnd">Set to true to require that the character after the parse must be a valid TokenEnd for TokenType.HexDigits.  Set to false to accept any character after the last parsed digit.</param>
        /// <returns>true if a valid Int32 hexadecimal number could be parsed, or false if not.  Does not move the scan position if the method returns false.</returns>
        public bool ParseHexValue(out Int32 value, int minDigits, int maxDigits, bool skipLeadingWhiteSpace, bool skipTrailingWhiteSpace, bool requireTokenEnd)
        {
            StringScanner localScanner = this;

            if (skipLeadingWhiteSpace)
                localScanner.SkipOverWhiteSpace();

            bool success = true;
            value = 0;
            int numDigits = 0;

            while (success)
            {
                if (!localScanner.IsIdxValid)
                    break;

                char c = localScanner.Char;

                if (Char.IsDigit(c))
                    value = unchecked((value << 4) + ((int)c - '0'));
                else if (c >= 'a' && c <= 'f')
                    value = unchecked((value << 4) + (10 + ((int)c - 'a')));
                else if (c >= 'A' && c <= 'F')
                    value = unchecked((value << 4) + (10 + ((int)c - 'A')));
                else
                    break;

                numDigits++;
                localScanner++;

                if (numDigits >= maxDigits)
                    break;
            }

            if (numDigits < minDigits)
                success = false;
            else if (requireTokenEnd && !localScanner.IsAtEnd && !IsValidTokenEndChar(localScanner.Char, TokenType.HexDigits))
                success = false;

            if (skipTrailingWhiteSpace)
                localScanner.SkipOverWhiteSpace();

            if (success)
                Idx = localScanner.Idx;
            else
                value = 0;

            return success;
        }

        #endregion

        #endregion

        //-------------------------------------
        #region simple Xml (style) attribute Parsers

        /// <summary>
        /// Attempts to parse an Xml style Attribute from the current position where the xml generally reads as
        /// <code>attribName='value'</code>, 
        /// <code>attribName="value"</code>, or
        /// <code>attribName=value</code>
        /// <para/>Please note that the current implementation does not support automatic decode of any escaped characters in the value string.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the string contents of the value portion of the matching attribute or String.Empty if the parse fails.</param>
        /// <returns>true if the attributeName matched and a properly delimited string could be found, or false otherwise.</returns>
        /// <remarks>NOTE: this signature is retained to prevent collision with Enum centric variant</remarks>
        public bool ParseXmlAttribute(string attribName, out string value)
        {
            return ParseXmlAttribute(attribName, out value, true);
        }

        /// <summary>
        /// Attempts to parse an Xml style Attribute from the current position where the xml generally reads as
        /// <code>attribName='value'</code>, or 
        /// <code>attribName="value"</code>, or
        /// <code>attribName=value</code>, or
        /// <code>attribName:value</code>
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the string contents of the value portion of the matching attribute or String.Empty if the parse fails.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited string could be found, or false otherwise.</returns>
        /// <remarks>Please note that the current implementation does not support automatic decode of any escaped characters in the value string.</remarks>
        public bool ParseXmlAttribute(string attribName, out string value, bool skipTrailingWhiteSpace)
        {
            bool success = true;
            StringScanner localScan = this;
            char termChar = (char)0;

            // the following defines the tokenEnd character(s) that are used when the parser does not recognize the attribute as either type of quote delimited attribute.
            const TokenType equalsUndelimitedValueTokenType = TokenType.ToNextWhiteSpace;

            success = localScan.MatchToken(attribName, false, false);

            if (localScan.MatchToken("=\"", false, false))
                termChar = '"';
            else if (localScan.MatchToken("=\'", false, false))
                termChar = '\'';
            else if (localScan.MatchToken("=", false, false))
            { }
            else if (localScan.MatchToken(":", false, false))
            { }
            else
                success = false;

            StringScanner valueStart = localScan;

            if (termChar != (char)0)
            {
                while (localScan.IsIdxValid && localScan.Char != termChar)
                    localScan.Idx++;
            }
            else
            {
                while (localScan.IsIdxValid && IsValidTokenChar(localScan.Char, equalsUndelimitedValueTokenType))
                    localScan.Idx++;
            }

            StringScanner valueEnd = localScan;

            if (termChar != (char)0)
            {
                string termStr = new string(termChar, 1);
                success = localScan.MatchToken(termStr, skipTrailingWhiteSpace, false) && success;
            }
            else
            {
                success = success && (localScan.IsAtEnd || IsValidTokenEndChar(localScan.Char, equalsUndelimitedValueTokenType));
                if (success)
                    localScan.SkipOverWhiteSpace();
            }

            int valueLen = valueEnd.Idx - valueStart.Idx;
            if (success)
            {
                Idx = localScan.Idx;
                value = valueStart.Str.Substring(valueStart.Idx, valueLen);
            }
            else
            {
                value = String.Empty;
            }

            return success;
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an Int32 value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed Int32 contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        /// <remarks>NOTE: this signature is retained to prevent collision with Enum centric variant</remarks>
        public bool ParseXmlAttribute(string attribName, out Int32 value)
        {
            return ParseXmlAttribute(attribName, out value, true);
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an Int32 value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed Int32 contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        public bool ParseXmlAttribute(string attribName, out Int32 value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScan = this;
            string attribValue;
            bool success = localScan.ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an UInt32 value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed UInt32 contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        /// <remarks>NOTE: this signature is retained to prevent collision with Enum centric variant</remarks>
        public bool ParseXmlAttribute(string attribName, out UInt32 value)
        {
            return ParseXmlAttribute(attribName, out value, true);
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an UInt32 value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed UInt32 contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        public bool ParseXmlAttribute(string attribName, out UInt32 value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScan = this;
            string attribValue;
            bool success = localScan.ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an bool value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed bool value of the requested attribute value or false if any portion of the operation failed.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        /// <remarks>NOTE: this signature is retained to prevent collision with Enum centric variant</remarks>
        public bool ParseXmlAttribute(string attribName, out bool value)
        {
            return ParseXmlAttribute(attribName, out value, true);
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an bool value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed bool value of the requested attribute value or false if any portion of the operation failed.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        public bool ParseXmlAttribute(string attribName, out bool value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScan = this;
            string attribValue;
            bool success = localScan.ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an Double value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed Double contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        /// <remarks>NOTE: this signature is retained to prevent collision with Enum centric variant</remarks>
        public bool ParseXmlAttribute(string attribName, out Double value)
        {
            return ParseXmlAttribute(attribName, out value, true);
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an Double value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed Double contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        public bool ParseXmlAttribute(string attribName, out Double value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScan = this;
            string attribValue;
            bool success = localScan.ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an table mapped value.
        /// </summary>
        /// <typeparam name="ItemType">Defines the Dictionary's TValue type used in the given map</typeparam>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="map">Defines the dictionary in which the token is to be found.</param>
        /// <param name="value">receives the found entry value contents of the requested attribute value or default(ItemType) if any portion of the operation failed.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and was found in the given map and assigned to the value output parameter.</returns>
        public bool ParseXmlAttribute<ItemType>(string attribName, Dictionary<string, ItemType> map, out ItemType value, bool skipTrailingWhiteSpace = true)
        {
            StringScanner localScan = this;
            string attribValue;
            bool success = localScan.ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = FindTokenValueByName<ItemType>(attribValue, map, out value) && success;

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for the given EnumT enum type.
        /// </summary>
        /// <typeparam name="EnumT">Gives the Enum Type for the enumeration that is to be parsed into.  Must be of type System.Enum.</typeparam>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed EnumT contents of the requested attribute value or parseFailedValue if any portion of the operation failed.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        /// <remarks>NOTE: this signature is retained to prevent collision with non-Enum centric variant</remarks>
        public bool ParseXmlAttribute<EnumT>(string attribName, out EnumT value) 
            where EnumT : struct
        {
            return ParseXmlAttribute(attribName, out value, default(EnumT), false, true);
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for the given EnumT enum type.
        /// </summary>
        /// <typeparam name="EnumT">Gives the Enum Type for the enumeration that is to be parsed into.  Must be of type System.Enum.</typeparam>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed EnumT contents of the requested attribute value or parseFailedValue if any portion of the operation failed.</param>
        /// <param name="parseFailedValue">Defines the EnumT value that will be assigned to the result if any part of the parse fails.</param>
        /// <param name="ignoreCase">If true, ignore case; otherwise, regard case.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        public bool ParseXmlAttribute<EnumT>(string attribName, out EnumT value, EnumT parseFailedValue, bool ignoreCase, bool skipTrailingWhiteSpace) 
            where EnumT : struct
        {
            StringScanner localScan = this;
            string attribValue;
            bool success = localScan.ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = Utils.Enum.TryParse<EnumT>(attribValue, out value, parseFailedValue, ignoreCase) && success;

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an TimeSpan value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed TimeSpan contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        /// <remarks>NOTE: this signature is retained to prevent collision with Enum centric variant</remarks>
        public bool ParseXmlAttribute(string attribName, out TimeSpan value)
        {
            return ParseXmlAttribute(attribName, out value, true);
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an TimeSpan value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed TimeSpan contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        public bool ParseXmlAttribute(string attribName, out TimeSpan value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScan = this;
            string attribValue;
            bool success = localScan.ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an DateTime value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed DateTime contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        /// <remarks>NOTE: this signature is retained to prevent collision with Enum centric variant</remarks>
        public bool ParseXmlAttribute(string attribName, out DateTime value)
        {
            return ParseXmlAttribute(attribName, out value, true);
        }

        /// <summary>
        /// Combines the ParseXmlAttribute and ParseValue methods for an DateTime value.
        /// </summary>
        /// <param name="attribName">gives the attribute name of the attribute that is to be parsed</param>
        /// <param name="value">receives the parsed DateTime contents of the requested attribute value or zero if any portion of the operation failed.</param>
        /// <param name="skipTrailingWhiteSpace">Set to true to allow the scanner to advance over trailing whitespace after successful parsing.  Set to false to prevent this.</param>
        /// <returns>true if the attributeName matched and a properly delimited value string was found and could be parsed successfully and assigned to the value output parameter.</returns>
        public bool ParseXmlAttribute(string attribName, out DateTime value, bool skipTrailingWhiteSpace)
        {
            StringScanner localScan = this;
            string attribValue;
            bool success = localScan.ParseXmlAttribute(attribName, out attribValue, skipTrailingWhiteSpace);

            success = ParseValue(attribValue, out value) && success;

            if (success)
                Idx = localScan.Idx;

            return success;
        }

        #endregion

        //-------------------------------------
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given char is a valid char in the set of chars defined by the tokenType.
        /// TokenType.ToNextWhiteSpace accepts all characters except whitespace characters.
        /// TokenType.AlphaNumeric accepts all letters and digits.
        /// TokenType.SimpleFileName accepts all AlphaNumeric and adds '-', '_' and '.'
        /// TokenType.SimpleFilePath accepts all SimpleFileName and adds '\\', '/' and ':'
        /// </summary>
        public static bool IsValidTokenChar(this Char c, TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.ToNextWhiteSpace: return !Char.IsWhiteSpace(c);
                case TokenType.ToNextTokenSeparator: return !c.IsTokenSeperatorChar();
                case TokenType.AlphaNumeric: return (Char.IsLetterOrDigit(c));
                case TokenType.SimpleFileName: return (Char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
                case TokenType.SimpleFilePath: return (Char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' || c == '\\' || c == '/' || c == ':');
                case TokenType.HexDigits: return (Char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
                case TokenType.NumericDigits: return (Char.IsDigit(c));
                case TokenType.NumericFloatDigits: return (Char.IsDigit(c) || c == '-' || c == '.' || c == '+' || c == 'e' || c == 'E');
                default: return false;
            }
        }

        /// <summary>
        /// Returns true if the given char is a valid end of token char. 
        /// For all TokenType's this reduces to Char.IsWhiteSpace(c)
        /// </summary>
        public static bool IsValidTokenEndChar(this Char c, TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.ToNextWhiteSpace: return Char.IsWhiteSpace(c);
                case TokenType.ToNextTokenSeparator: return c.IsTokenSeperatorChar();
                case TokenType.AlphaNumeric: return Char.IsWhiteSpace(c);
                case TokenType.SimpleFileName: return Char.IsWhiteSpace(c);
                case TokenType.SimpleFilePath: return Char.IsWhiteSpace(c);
                case TokenType.HexDigits: return Char.IsWhiteSpace(c);
                case TokenType.NumericDigits: return Char.IsWhiteSpace(c);
                case TokenType.NumericFloatDigits: return Char.IsWhiteSpace(c);
                default: return false;
            }
        }

        /// <summary>
        /// Returns true if the given character is consider a token seperator: whitespace ',', '|'
        /// </summary>
        /// <param name="ch"></param>
        /// <returns></returns>
        public static bool IsTokenSeperatorChar(this char ch)
        {
            return (Char.IsWhiteSpace(ch) || ch == ',' || ch == '|');
        }
    }
}
//-----------------------------------------------------------------
//-----------------------------------------------------------------
