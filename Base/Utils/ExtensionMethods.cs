//-------------------------------------------------------------------
/*! @file ExtensionMethods.cs
 *  @brief This file contains a number of extension methods
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2013 Mosaic Systems Inc., All rights reserved
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
using System.Diagnostics;
using System.Reflection;

namespace MosaicLib.Utils
{
    #region Extension Methods

    /// <summary>
    /// This class contains a set of extension methods.  At present this primarily adds variants of the CheckedFormat methods above to be used directly with Strings and StringBuilder
    /// </summary>
    public static partial class ExtensionMethods
    {
        #region Array and IList comparisons extension methods

        /// <summary>
        /// Extension method version of static Utils.Fcns.Equals method for two arrays
        /// Returns true if both lists have the same length and contents (using Object.Equals).  Returns false if they do not.
        /// </summary>
        public static bool IsEqualTo<ItemType>(this ItemType[] lhs, ItemType[] rhs)
        {
            return Utils.Fcns.Equals(lhs, rhs);
        }

        /// <summary>
        /// Extension method version of static Utils.Fcns.Equals method for an array and a list, both generics with the same ItemType.
        /// Returns true if both the array and the list have the same length and contents (using Object.Equals).  Returns false if they do not.
        /// </summary>
        public static bool IsEqualTo<ItemType>(this ItemType[] lhs, IList<ItemType> rhs)
        {
            return Utils.Fcns.Equals(lhs, rhs);
        }

        /// <summary>
        /// Extension method version of static Utils.Fcns.Equals method for a list and an array, both generics with the same ItemType.
        /// Returns true if both the array and the list have the same length and contents (using Object.Equals).  Returns false if they do not.
        /// </summary>
        public static bool IsEqualTo<ItemType>(this IList<ItemType> lhs, ItemType[] rhs)
        {
            return Utils.Fcns.Equals(rhs, lhs);
        }

        /// <summary>
        /// Extension method version of static Utils.Fcns.Equals method for two lists, both generics with the same ItemType.
        /// Returns true if both lists have the same length and contents (using Object.Equals).  Returns false if they do not.
        /// </summary>
        public static bool IsEqualTo<ItemType>(this IList<ItemType> lhs, IList<ItemType> rhs)
        {
            return Utils.Fcns.Equals(lhs, rhs);
        }

        #endregion

        #region Array access extension methods

        /// <summary>
        /// Extension method version of Array indexed get access that handles all out of range accesses by returning the given default value
        /// </summary>
        public static ItemType SafeAccess<ItemType>(this ItemType[] fromArray, int getFromIndex, ItemType defaultValue)
        {
            if (fromArray == null || getFromIndex < 0 || getFromIndex >= fromArray.Length)
                return defaultValue;
            else
                return fromArray[getFromIndex];
        }

        /// <summary>
        /// Extension method version of Array indexed get access that handles all out of range accesses by returning the default(<typeparam name="ItemType"/>)
        /// </summary>
        public static ItemType SafeAccess<ItemType>(this ItemType[] fromArray, int getFromIndex)
        {
            if (fromArray == null || getFromIndex < 0 || getFromIndex >= fromArray.Length)
                return default(ItemType);
            else
                return fromArray[getFromIndex];
        }

        /// <summary>
        /// Extension method version of Array indexed sub-array get access that handles all out of range accesses by returning either a partial sub-array or an empty array.
        /// </summary>
        public static ItemType[] SafeAccess<ItemType>(this ItemType[] fromArray, int getFromIndex, int getSubArrayLength)
        {
            getSubArrayLength = Math.Max(0, Math.Min(getSubArrayLength, (fromArray.Length - getFromIndex + 1)));
            ItemType[] subArray = new ItemType[getSubArrayLength];

            if (fromArray != null && getFromIndex >= 0 && getFromIndex < fromArray.Length)
                System.Array.Copy(fromArray, getFromIndex, subArray, 0, getSubArrayLength);

            return subArray;
        }


        /// <summary>
        /// Extension method version of string indexed get access that handles all out of range accesses by returning the default(char)
        /// </summary>
        public static char SafeAccess(this string fromStr, int getFromIndex)
        {
            if (fromStr == null || getFromIndex < 0 || getFromIndex > fromStr.Length)
                return default(char);
            else
                return fromStr[getFromIndex];
        }

        /// <summary>
        /// Extension method version of string indexed get access that handles all out of range accesses by returning the given default value
        /// </summary>
        public static char SafeAccess(this string fromStr, int getFromIndex, char defaultValue)
        {
            if (fromStr == null || getFromIndex < 0 || getFromIndex > fromStr.Length)
                return defaultValue;
            else
                return fromStr[getFromIndex];
        }

        /// <summary>
        /// Extension method version of Array element assignement.  Attempts to assign the given value into the given intoArray at the given putIndex location.
        /// Does nothing if the intoArray is not valid or does not have any such value.
        /// Returns the given intoArray value.
        /// </summary>
        public static ItemType[] SafePut<ItemType>(this ItemType[] intoArray, int putIndex, ItemType value)
        {
            if (intoArray != null && putIndex >= 0 && putIndex < intoArray.Length)
                intoArray[putIndex] = value;

            return intoArray;
        }

        /// <summary>
        /// Extension method version of Array sub-section assignement.  Attempts to assign the given fromArray contents into the given intoArray starting at the given putStartIndex location.
        /// Will perform a partial copy of the fromArray if only part of its fits into the intoArray. 
        /// Returns the given intoArray value.
        /// </summary>
        public static ItemType[] SafePut<ItemType>(this ItemType[] intoArray, int putStartIndex, ItemType[] fromArray)
        {
            if (intoArray != null && fromArray != null && putStartIndex >= 0 && putStartIndex < intoArray.Length)
            {
                int copyLength = Math.Min(fromArray.Length, intoArray.Length - putStartIndex + 1);

                System.Array.Copy(fromArray, 0, intoArray, putStartIndex, copyLength);
            }

            return intoArray;
        }

        #endregion

        #region Other Array and IList related extension methods

        /// <summary>
        /// Extension method returns true if the given array is null or its Length is zero.
        /// </summary>
        public static bool IsNullOrEmpty<TItemType>(this TItemType[] array)
        {
            return ((array == null) || (array.Length == 0));
        }

        /// <summary>
        /// Extension method returns true if the given list is null or its Count is zero.
        /// </summary>
        public static bool IsNullOrEmpty<TItemType>(this IList<TItemType> list)
        {
            return ((list == null) || (list.Count == 0));
        }

        /// <summary>
        /// Extension method returns true if given array is null or empty (Length == 0)
        /// </summary>
        public static bool IsEmpty<ItemType>(this ItemType[] array)
        {
            return (array == null || array.Length == 0);
        }

        /// <summary>
        /// Extension method returns true if given IList is null or empty (Count == 0)
        /// </summary>
        public static bool IsEmpty<ItemType>(this IList<ItemType> list)
        {
            return (list == null || list.Count == 0);
        }

        /// <summary>
        /// Extension method sets all of the elements of the given array to the given value.  Has no effect if the array is null or is zero length.
        /// </summary>
        public static void SetAll<ItemType>(this ItemType[] array, ItemType value)
        {
            int numItems = ((array != null) ? array.Length : 0);

            for (int idx = 0; idx < numItems; idx++)
                array[idx] = value;
        }

        /// <summary>
        /// Extension method sets all of the elements of the given array to their default value.  Has no effect if the array is null or is zero length.
        /// </summary>
        public static void Clear<ItemType>(this ItemType[] array, ItemType value)
        {
            array.SetAll(default(ItemType));
        }

        /// <summary>
        /// Extension method version of IList indexed get access that handles all out of range accesses by returning the given default value
        /// </summary>
        public static ItemType SafeAccess<ItemType>(this IList<ItemType> fromList, int getFromIndex, ItemType defaultValue)
        {
            if (fromList == null || getFromIndex < 0 || getFromIndex >= fromList.Count)
                return defaultValue;
            else
                return fromList[getFromIndex];
        }

        /// <summary>
        /// Extension method "safe" version of ToArray method.  If the given collection is non-null then this method returns the Linq ToArray method applied to the collection.
        /// If the collection is null then this method creates and returns an empty array of the given ItemType.
        /// </summary>
        public static ItemType[] SafeToArray<ItemType>(this ICollection<ItemType> collection)
        {
            return SafeToArray(collection, null);
        }

        /// <summary>
        /// Extension method "safe" version of ToArray method.  If the given collection is non-null then this method returns the Linq ToArray method applied to the collection.
        /// If the collection is null and the given fallbackArray is non-null then this method returns the fallbackArray.
        /// If the collection and the fallbackArray values are null then this creates and returns an empty array of the given ItemType.
        /// </summary>
        public static ItemType[] SafeToArray<ItemType>(this ICollection<ItemType> collection, ItemType[] fallbackArray)
        {
            if (collection != null)
                return collection.ToArray();

            if (fallbackArray != null)
                return fallbackArray;

            return new ItemType[0];
        }

        #endregion

        #region byte arrays as bit masks

        /// <summary>
        /// Extension method to support setting an indicated bitIdx to the given value using the given byteArray as a packed bit vector.
        /// If the indicated bitIndex is outside of 0 to (byteArray.Length * 8 - 1) then the method will have no effect.
        /// An empty byteArray is treated as if it has zero length.
        /// <para/>Supports call chaining
        /// </summary>
        public static byte[] SetBit(this byte[] byteArray, int bitIndex, bool value)
        {
            int byteIdx = (bitIndex >> 3);
            byte byteMask = unchecked((byte) (1 << (bitIndex & 0x007)));

            if (byteArray != null && byteIdx >= 0 && byteIdx < byteArray.Length)
            {
                if (value)
                    byteArray[byteIdx] |= byteMask;
                else
                    byteArray[byteIdx] &= unchecked((byte) ~byteMask);
            }

            return byteArray;
        }

        public static bool GetBit(this byte[] byteArray, int bitIndex)
        {
            int byteIdx = (bitIndex >> 3);
            byte byteMask = unchecked((byte)(1 << (bitIndex & 0x007)));

            if (byteArray != null && byteIdx >= 0 && byteIdx < byteArray.Length)
                return ((byteArray[byteIdx] & byteMask) != 0);

            return false;
        }

        #endregion

        #region Math and number related extension methods (Ceiling, Floor, Round, Clip, IsInRange)

        /// <summary>Returns the Math.Ceiling of the given value</summary>
        public static double Ceiling(this double value)
        {
            return Math.Ceiling(value);
        }

        /// <summary>Returns the Math.Floor of the given value</summary>
        public static double Floor(this double value)
        {
            return Math.Floor(value);
        }

        /// <summary>Returns the Math.Round of the given value</summary>
        public static double Round(this double value)
        {
            return Math.Round(value);
        }

        /// <summary>Returns the Math.Round of the given value using the given MidpointRounding mode</summary>
        public static double Round(this double value, MidpointRounding mode)
        {
            return Math.Round(value, mode);
        }

        /// <summary>Returns the Math.Round of the given value using the given number of digits and the given MidpointRounding mode</summary>
        public static double Round(this double value, int digits, MidpointRounding mode)
        {
            return Math.Round(value, digits, mode);
        }

        /// <summary>Returns the given value clipped to make certain that it is between lowLimit and highLimit.</summary>
        public static TValueType Clip<TValueType>(this TValueType value, TValueType lowLimit, TValueType highLimit) where TValueType : IComparable<TValueType>
        {
            return value.Clip(lowLimit, highLimit, value);
        }

        /// <summary>Returns the given value clipped to make certain that it is between lowLimit and highLimit.</summary>
        public static TValueType Clip<TValueType>(this TValueType value, TValueType lowLimit, TValueType highLimit, TValueType invalidCompareValue) where TValueType : IComparable<TValueType>
        {
            int lowToHighLimitCompare = lowLimit.CompareTo(highLimit);

            if (lowToHighLimitCompare > 0)
                return invalidCompareValue;

            int lowLimitCompare = lowLimit.CompareTo(value);
            int highLimitCompare = highLimit.CompareTo(value);

            if (lowLimitCompare <= 0 && highLimitCompare >= 0)
                return value;
            else if (lowLimitCompare < 0 && highLimitCompare < 0)
                return highLimit;
            else if (lowLimitCompare > 0 && highLimitCompare > 0)
                return lowLimit;
            else
                return invalidCompareValue;
        }

        /// <summary>Returns the given value clipped to make certain that it is between lowLimit and highLimit.</summary>
        public static double Clip(this double value, double lowLimit, double highLimit)
        {
            return value.Clip(lowLimit, highLimit, Double.NaN);
        }

        /// <summary>Returns the given value clipped to make certain that it is between lowLimit and highLimit.</summary>
        public static double Clip(this double value, double lowLimit, double highLimit, double invalidCompareValue)
        {
            if (double.IsNaN(value))
                return value;

            if (double.IsNaN(lowLimit) || double.IsNaN(highLimit))
                return invalidCompareValue;

            return value.Clip<double>(lowLimit, highLimit, invalidCompareValue);
        }

        /// <summary>Returns true if, and only if, the given value is no less than the given lowLimit, it is no greater than the given highLimit, and the given lowLimit is no greater than the given highLimit</summary>
        public static bool IsInRange<TValueType>(this TValueType value, TValueType lowLimit, TValueType highLimit) where TValueType : IComparable<TValueType>
        {
            int lowToHighLimitCompare = lowLimit.CompareTo(highLimit);
            int lowLimitCompare = lowLimit.CompareTo(value);
            int highLimitCompare = highLimit.CompareTo(value);

            if (lowToHighLimitCompare <= 0 && lowLimitCompare <= 0 && highLimitCompare >= 0)
                return true;
            else
                return false;
        }

        /// <summary>Returns true if, and only if, the given value is not a NaN, it is no less than the given lowLimit, it is no greater than the given highLimit, and the given lowLimit is no greater than the given highLimit</summary>
        public static bool IsInRange(this double value, double lowLimit, double highLimit)
        {
            if (double.IsNaN(value) || double.IsNaN(lowLimit) || double.IsNaN(highLimit))
                return false;

            return value.IsInRange<double>(lowLimit, highLimit);
        }

        /// <summary>Returns true if the given value is divisible by 2.</summary>
        public static bool IsEven(this int value)
        {
            return ((value & 1) == 0);
        }

        #endregion
    }

    #endregion

    #region Related Unassociated Functions

    /// <summary>
    /// Fcns class is essentially a namespace for series of static helper methods
    /// <para/>includes: array/list specific Equals methods, ...
    /// </summary>
    public static partial class Fcns
    {
        #region general static [] comparison methods

        /// <summary>Returns true if both arrays, a and b, have the same length and contents (using Object.Equals).  Returns false if they do not.</summary>
        public static bool Equals<ItemType>(ItemType[] a, ItemType[] b)
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
                if (!object.Equals(a[idx], b[idx]))
                    return false;
            }

            return true;
        }

        /// <summary>Returns true if both the array and the list have the same length and contents (using Object.Equals).  Returns false if they do not.</summary>
        public static bool Equals<ItemType>(ItemType[] a, IList<ItemType> b)
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
                if (!object.Equals(a[idx], b[idx]))
                    return false;
            }

            return true;
        }

        /// <summary>Returns true if both of the lists, a and b, have the same length and contents (using Object.Equals).  Returns false if they do not.</summary>
        public static bool Equals<ItemType>(IList<ItemType> a, IList<ItemType> b)
        {
            if (a == null && b == null)
                return true;
            if ((a == null) || (b == null))
                return false;
            if (a.Count != b.Count)
                return false;

            int n = a.Count;
            for (int idx = 0; idx < n; idx++)
            {
                if (!object.Equals(a[idx], b[idx]))
                    return false;
            }

            return true;
        }


        #endregion

        #region Migrated MapTo methods for booleans and similar cases.

        /// <summary>Maps the given boolean value to either "1" or "0"</summary>
        public static string MapToString(this bool value) { return (value ? "1" : "0"); }

        /// <summary>Maps the given boolean value to either given trueStr or given falseStr</summary>
        public static string MapToString(this bool value, string trueStr, string falseStr) { return (value ? trueStr : falseStr); }

        /// <summary>Maps the given boolean value to an integer value of 1 for true and 0 for false</summary>
        public static int MapToInt(this bool value) { return (value ? 1 : 0); }

        /// <summary>
        /// Generalized version of Map method.  If the given value does not Equals default(TValueType) then return the value, otherwise return replaceDefultWith.
        /// </summary>
        public static TValueType MapDefaultTo<TValueType>(this TValueType value, TValueType replaceDefaultWith)
        {
            if (!Object.Equals(value, default(TValueType)))
                return value;
            else
                return replaceDefaultWith;
        }

        #endregion

        #region CurrentStackFrame, CurrentMethod, CurrentMethodName helper "functions".

        /// <summary>Creates and returns the callers current StackFrame</summary>
        public static StackFrame CurrentStackFrame { get { return new System.Diagnostics.StackFrame(1); } }

        /// <summary>Creates a StackFrame for the caller and returns the stack frame's current method.</summary>
        public static MethodBase CurrentMethod { get { return new System.Diagnostics.StackFrame(1).GetMethod(); } }

        /// <summary>Creates a StackFrame for the caller and returns the Name of the stack frame's current method.</summary>
        public static string CurrentMethodName { get { return new System.Diagnostics.StackFrame(1).GetMethod().Name; } }

        #endregion
    }

    #endregion
}

//-------------------------------------------------------------------
