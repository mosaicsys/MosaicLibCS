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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

using MosaicLib.Modular.Common;
using MosaicLib.Utils.Collections;

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
        /// Extension method returns the Length from the given <paramref name="array"/> or zero if the given <paramref name="array"/> is null
        /// </summary>
        public static int SafeLength<ItemType>(this ItemType[] array)
        {
            return (array != null ? array.Length : 0);
        }

        /// <summary>
        /// Extension method returns the Count from the given <paramref name="collection"/> or zero if the given <paramref name="collection"/> is null
        /// </summary>
        public static int SafeCount<TItemType>(this ICollection<TItemType> collection)
        {
            return (collection != null ? collection.Count : 0);
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
        /// Extension method "safe" version of ToArray method.  If the given <paramref name="set"/> is non-null then this method returns the Linq ToArray method applied to the <paramref name="set"/>.
        /// If the <paramref name="set"/> is null and the given <paramref name="fallbackArray"/> is non-null then this method returns the <paramref name="fallbackArray"/>.
        /// If the <paramref name="set"/> and the <paramref name="fallbackArray"/> values are null then this creates and returns an empty array of the given ItemType (<paramref name="mapNullToEmpty"/> is true) or null (<paramref name="mapNullToEmpty"/> is false)
        /// </summary>
        public static ItemType[] SafeToArray<ItemType>(this IEnumerable<ItemType> set, ItemType[] fallbackArray = null, bool mapNullToEmpty = true)
        {
            if (set != null)
                return set.ToArray();

            return fallbackArray ?? (mapNullToEmpty ? EmptyArrayFactory<ItemType>.Instance : null);
        }

        /// <summary>
        /// Extension method "safe" version of ToArray method.  If the given <paramref name="collection"/> is non-null then this method returns the Linq ToArray method applied to the <paramref name="collection"/>.
        /// If the <paramref name="collection"/> is null and the given <paramref name="fallbackArray"/> is non-null then this method returns the <paramref name="fallbackArray"/>.
        /// If the <paramref name="collection"/> and the <paramref name="fallbackArray"/> values are null then this creates and returns an empty array of the given ItemType (<paramref name="mapNullToEmpty"/> is true) or null (<paramref name="mapNullToEmpty"/> is false)
        /// </summary>
        public static ItemType[] SafeToArray<ItemType>(this ICollection<ItemType> collection, ItemType[] fallbackArray = null, bool mapNullToEmpty = true)
        {
            if (collection != null && (collection.Count > 0 || fallbackArray == null || fallbackArray.Length != 0))
                return collection.ToArray();

            return fallbackArray ?? (mapNullToEmpty ? EmptyArrayFactory<ItemType>.Instance : null);
        }


        /// <summary>
        /// Extension method "safe" version of ToArray method.  If the given collection/set is non-null then this method returns the Linq ToArray method applied to the collection.
        /// If the collection/set is null and the given fallbackArray is non-null then this method returns the fallbackArray.
        /// If the collection/set and the fallbackArray values are null then this creates and returns an empty array of the given ItemType.
        /// </summary>
        public static ItemType[] SafeToArray<ItemType>(this IEnumerable set, ItemType[] fallbackArray = null, bool mapNullToEmpty = true)
        {
            if (set != null)
            {
                bool itemTypeIsObject = (typeof(ItemType) == typeof(object));

                List<ItemType> itemList = new List<ItemType>();
                foreach (var obj in set)
                {
                    if (obj is ItemType || itemTypeIsObject)
                        itemList.Add((ItemType) obj);
                }

                return itemList.ToArray();
            }

            return fallbackArray ?? (mapNullToEmpty ? EmptyArrayFactory<ItemType>.Instance : null);
        }

        /// <summary>
        /// Extension method to "safely" take (remove) and return the first element of the given list.  Returns defaultValue if the list is empty
        /// </summary>
        public static ItemType SafeTakeFirst<ItemType>(this IList<ItemType> itemList, ItemType defaultValue = default(ItemType))
        {
            if (itemList.Count <= 0)
                return defaultValue;

            ItemType item = itemList[0];
            itemList.RemoveAt(0);

            return item;
        }

        /// <summary>
        /// Extension method to "safely" take (remove) and return the last element of the given list.  Returns defaultValue if the list is empty
        /// </summary>
        public static ItemType SafeTakeLast<ItemType>(this IList<ItemType> itemList, ItemType defaultValue = default(ItemType))
        {
            if (itemList.Count <= 0)
                return defaultValue;

            int takeFromIdx = (itemList.Count - 1);
            ItemType item = itemList[takeFromIdx];
            itemList.RemoveAt(takeFromIdx);

            return item;
        }

        #endregion

        #region IEnumerable methods (SafeToSet variants) - for use with IEnumerable, ICollection, and IList derived objects

        /// <summary>
        /// Non-generic IEnumerable centric method to allow IEnumerable contents to be injected into other LINQ expressions.
        /// Returns an IEnumerable set of the objects found in the given <paramref name="set"/>.
        /// If the given <paramref name="set"/> is null then the <paramref name="mapNullToEmpty"/> parameter determines if this method returns null (false) or an empty set (true - default)
        /// </summary>
        public static IEnumerable<object> SafeToSet(this IEnumerable set, bool mapNullToEmpty = true)
        {
            return set.SafeToSet<object>(mapNullToEmpty: mapNullToEmpty);
        }

        /// <summary>
        /// Non-generic IEnumerable centric method to allow IEnumerable contents to be injected into other LINQ expressions.
        /// Returns an IEnumerable set of the given <typeparamref name="TItemType"/> objects found in the given <paramref name="set"/>.  Members of the given <paramref name="set"/> that are not of the given <typeparamref name="TItemType"/> will not be included in the set.
        /// To guarantee that all objects from the set are returned, the caller should pass <typeparamref name="TItemType"/> as System.Object.
        /// If the given <paramref name="set"/> is null then the <paramref name="mapNullToEmpty"/> parameter determines if this method returns null (false) or an empty set (true - default)
        /// </summary>
        public static IEnumerable<TItemType> SafeToSet<TItemType>(this IEnumerable set, bool mapNullToEmpty = true)
        {
            if (set == null)
                return (mapNullToEmpty ? Collections.EmptyArrayFactory<TItemType>.Instance : null);

            bool itemTypeIsObject = (typeof(TItemType) == typeof(object));

            return InnerSafeToSet<TItemType>(set, itemTypeIsObject);
        }

        /// <summary>
        /// Inner method used to support IEnumerable SafeToSet variants
        /// </summary>
        private static IEnumerable<TItemType> InnerSafeToSet<TItemType>(IEnumerable set, bool itemTypeIsObject)
        {
            foreach (var obj in set)
            {
                if (obj is TItemType || itemTypeIsObject)
                    yield return ((TItemType) obj);
            }

            yield break;
        }

        #endregion

        #region IDictionary related (SafeTryGetValue)

        /// <summary>
        /// This EM accepts an IDictionary <paramref name="dict"/> and attempts to obtain and return the value from it at the given <paramref name="key"/>.  
        /// If <paramref name="dict"/> is null or it if does not contain the indicated <paramref name="key"/> then this method returns the given <paramref name="fallbackValue"/>
        /// </summary>
        public static TValue SafeTryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue fallbackValue = default(TValue))
        {
            TValue value = default(TValue);

            if (dict != null && !Object.ReferenceEquals(key, null) && dict.TryGetValue(key, out value))
                return value;

            return fallbackValue;
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

        /// <summary>Returns true if the given value is not divisible by 2.</summary>
        public static bool IsOdd(this int value)
        {
            return ((value & 1) != 0);
        }

        /// <summary>
        /// returns the given <paramref name="value"/> incremented.  If the incremented <paramref name="value"/> is 0 then this method returns 1 in its place
        /// </summary>
        public static int IncrementSkipZero(this int value)
        {
            if (++value == 0)
                return 1;
            else
                return value;
        }

        /// <summary>
        /// returns the given <paramref name="value"/> incremented.  If the incremented <paramref name="value"/> is 0 then this method returns 1 in its place
        /// </summary>
        public static uint IncrementSkipZero(this uint value)
        {
            if (++value == 0)
                return 1;
            else
                return value;
        }


        /// <summary>
        /// If the given <paramref name="value"/> is not zero then this method returns (1.0 / <paramref name="value"/>).  
        /// Otherwise this method returns the given <paramref name="fallbackValue"/>.
        /// </summary>
        public static double SafeOneOver(this double value, double fallbackValue = 0.0)
        {
            return ((value != 0.0) ? (1.0 / value) : fallbackValue);
        }

        /// <summary>
        /// If the given <paramref name="value"/> is not zero then this method returns (1.0 / <paramref name="value"/>).  
        /// Otherwise this method returns the given <paramref name="fallbackValue"/>.
        /// </summary>
        public static float SafeOneOver(this float value, float fallbackValue = 0.0f)
        {
            return ((value != 0.0f) ? (1.0f / value) : fallbackValue);
        }

        #endregion

        #region Enum extension methods (IsSet, IsMatch)

        /// <summary>
        /// Returns true if the given value contains the given test pattern (by bit mask test).  Eqivilant to value.IsMatch(test, test)
        /// <para/>This extension method is intended for use with Flag enumerations.  Use with types that are not derived from System.Enum will simply return false;
        /// </summary>
        public static bool IsSet<TEnumType>(this TEnumType value, TEnumType test) where TEnumType : struct
        {
            return value.IsMatch(test, test);
        }

        /// <summary>
        /// Returns true if the given value bit anded with the given mask is equal to the given match:  return ((value &amp; mask) == match).
        /// <para/>This extension method is intended for use with Flag enumerations.  Use with types that are not derived from System.Enum will simply return false;
        /// </summary>
        public static bool IsAnySet<TEnumType>(this TEnumType value, TEnumType mask)
        {
            return !value.IsMatch(mask, default(TEnumType));
        }

        /// <summary>
        /// Returns true if the given value bit anded with the given mask is equal to the given match:  return ((value &amp; mask) == match).
        /// <para/>This extension method is intended for use with Flag enumerations.  Use with types that are not derived from System.Enum will simply return false;
        /// </summary>
        public static bool IsMatch<TEnumType>(this TEnumType value, TEnumType mask, TEnumType match)
        {
            try
            {
                if (!(value is System.Enum))
                    return false;

                int valueI = (int)System.Convert.ChangeType(value, typeof(int));
                int maskI = (int)System.Convert.ChangeType(mask, typeof(int));
                int matchI = (int)System.Convert.ChangeType(match, typeof(int));

                return ((valueI & maskI) == matchI);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region TimeSpan related (double.FromDays, double.FromHours, double.FromSeconds, double.FromMilliseconds, IsZero, Min, Max)

        /// <summary>
        /// Variant of TimeSpan.FromDays that does not round the <paramref name="timeSpanInDays"/> to the nearest msec.
        /// This extension method converts to TimeSpan using FromTicks and TimeSpan.TicksPerDay
        /// </summary>
        public static TimeSpan FromDays(this double timeSpanInDays)
        {
            return TimeSpan.FromTicks(unchecked((long)(TimeSpan.TicksPerDay * timeSpanInDays)));
        }

        /// <summary>
        /// Variant of TimeSpan.FromHours that does not round the <paramref name="timeSpanInHours"/> to the nearest msec.
        /// This extension method converts to TimeSpan using FromTicks and TimeSpan.TicksPerHour
        /// </summary>
        public static TimeSpan FromHours(this double timeSpanInHours)
        {
            return TimeSpan.FromTicks(unchecked((long)(TimeSpan.TicksPerHour * timeSpanInHours)));
        }

        /// <summary>
        /// Variant of TimeSpan.FromMinutes that does not round the <paramref name="timeSpanInMinutes"/> to the nearest msec.
        /// This extension method converts to TimeSpan using FromTicks and TimeSpan.TicksPerMinute
        /// </summary>
        public static TimeSpan FromMinutes(this double timeSpanInMinutes)
        {
            return TimeSpan.FromTicks(unchecked((long)(TimeSpan.TicksPerMinute * timeSpanInMinutes)));
        }

        /// <summary>
        /// Variant of TimeSpan.FromSeconds that does not round the <paramref name="timeSpanInSeconds"/> to the nearest msec.
        /// This extension method converts to TimeSpan using FromTicks and TimeSpan.TicksPerSecond
        /// </summary>
        public static TimeSpan FromSeconds(this double timeSpanInSeconds)
        {
            return TimeSpan.FromTicks(unchecked((long)(TimeSpan.TicksPerSecond * timeSpanInSeconds)));
        }

        /// <summary>
        /// Variant of TimeSpan.FromSeconds that does not round the <paramref name="timeSpanInMilliseconds"/> to the nearest msec.
        /// This extension method converts to TimeSpan using FromTicks and TimeSpan.TicksPerMillisecond
        /// </summary>
        public static TimeSpan FromMilliseconds(this double timeSpanInMilliseconds)
        {
            return TimeSpan.FromTicks(unchecked((long)(TimeSpan.TicksPerMillisecond * timeSpanInMilliseconds)));
        }

        /// <summary>
        /// Returns true if the given timeSpan value is equal to TimeSpan.Zero
        /// </summary>
        public static bool IsZero(this TimeSpan timeSpan)
        {
            return (timeSpan == TimeSpan.Zero);
        }

        /// <summary>Returns the Min of the given TimeSpan values</summary>
        public static TimeSpan Min(this TimeSpan a, TimeSpan b) { return (a <= b) ? a : b; }
        /// <summary>Returns the Min of the given TimeSpan values</summary>
        public static TimeSpan Min(this TimeSpan a, TimeSpan b, TimeSpan c) { return a.Min(b).Min(c); }
        /// <summary>Returns the Min of the given TimeSpan values</summary>
        public static TimeSpan Min(this TimeSpan a, params TimeSpan[] more) { return a.Concat(more).Min(); }

        /// <summary>Returns the Max of the given TimeSpan values</summary>
        public static TimeSpan Max(this TimeSpan a, TimeSpan b) { return (a >= b) ? a : b; }
        /// <summary>Returns the Max of the given TimeSpan values</summary>
        public static TimeSpan Max(this TimeSpan a, TimeSpan b, TimeSpan c) { return a.Max(b).Max(c); }
        /// <summary>Returns the Max of the given TimeSpan values</summary>
        public static TimeSpan Max(this TimeSpan a, params TimeSpan[] more) { return a.Concat(more).Max(); }

        #endregion

        #region DateTime related (Age, Min, Max)

        /// <summary>
        /// Returns the Age of the given <paramref name="dateTime"/>.  Caller can optionally provide the current time, preferrably already in the same DateTimeKind as the given value.
        /// </summary>
        public static TimeSpan Age(this DateTime dateTime, DateTime ? dateTimeNowIn = null)
        {
            if (dateTimeNowIn != null)
            {
                DateTime dateTimeNow = dateTimeNowIn.GetValueOrDefault();

                if (dateTime.Kind == dateTimeNow.Kind)
                    return (dateTimeNow - dateTime);
                else if (dateTimeNow.Kind == DateTimeKind.Utc)
                    return (dateTimeNow - dateTime.ToUniversalTime());
                else if (dateTimeNow.Kind == DateTimeKind.Local)
                    return (dateTimeNow - dateTime.ToLocalTime());
                return (dateTimeNow.ToUniversalTime() - dateTime.ToUniversalTime());
            }
            else if (dateTime.Kind == DateTimeKind.Utc)
                return (DateTime.UtcNow - dateTime);
            else if (dateTime.Kind == DateTimeKind.Local)
                return (DateTime.Now - dateTime);
            else
                return (DateTime.UtcNow - dateTime.ToUniversalTime());
        }

        /// <summary>Returns the Min of the given DateTime values</summary>
        public static DateTime Min(this DateTime a, DateTime b) { return (a <= b) ? a : b; }
        /// <summary>Returns the Min of the given DateTime values</summary>
        public static DateTime Min(this DateTime a, DateTime b, DateTime c) { return a.Min(b).Min(c); }
        /// <summary>Returns the Min of the given DateTime values</summary>
        public static DateTime Min(this DateTime a, params DateTime[] more) { return a.Concat(more).Min(); }

        /// <summary>Returns the Max of the given DateTime values</summary>
        public static DateTime Max(this DateTime a, DateTime b) { return (a >= b) ? a : b; }
        /// <summary>Returns the Max of the given DateTime values</summary>
        public static DateTime Max(this DateTime a, DateTime b, DateTime c) { return a.Max(b).Max(c); }
        /// <summary>Returns the Max of the given DateTime values</summary>
        public static DateTime Max(this DateTime a, params DateTime[] more) { return a.Concat(more).Max(); }

        #endregion

        #region Random related (GetNextRandomInMinus1ToPlus1Range)

        /// <summary>
        /// Extension method for Random.  Returns the NextDouble number produced by the given Random rng source scaled and offset to fall in the range [-1.0 .. 1.0)
        /// </summary>
        public static double GetNextRandomInMinus1ToPlus1Range(this Random rng)
        {
            return (rng.NextDouble() * 2.0 - 1.0);
        }

        #endregion

        #region TryGet extension method

        /// <summary>
        /// This extension method is invoked on a getter type of delegate (getterDelegate).  
        /// It will attempt to perform the getterDelegate and returned the obtained value.
        /// If any catchable exception is thrown while using the provided getterDelegate then this method will return the getFailedResult
        /// which default to default(TValueType)
        /// </summary>
        /// <typeparam name="TValueType">This is the generic value type that is to be returned by the provided getterDelegate</typeparam>
        public static TValueType TryGet<TValueType>(this Func<TValueType> getterDelegate, TValueType getFailedResult = default(TValueType))
        {
            try
            {
                return getterDelegate();
            }
            catch
            {
                return getFailedResult;
            }
        }

        #endregion

        #region Linq extensions (DoForEach, Concat, FilterAndRemove, WhereIsNotDefault)

        /// <summary>
        /// Simple DoForEach helper method for use with Linq.  Applies the given action to each of the {TSource} items in the given source set.
        /// <para/>supports call chaining
        /// </summary>
        public static IEnumerable<TItem> DoForEach<TItem>(this IEnumerable<TItem> source, Action<TItem> action = null)
        {
            if (source != null)
            {
                action = action ?? (ignoreItem => { });

                foreach (TItem item in source)
                    action(item);
            }

            return source;
        }

        /// <summary>
        /// Simple DoForEach helper method for use with Linq.  Applies the given action to each of the {TSource} items in the given source set.
        /// The action is also passed the index of the item in the source set in its second argument.
        /// <para/>supports call chaining
        /// </summary>
        public static IEnumerable<TItem> DoForEach<TItem>(this IEnumerable<TItem> source, Action<TItem, int> action)
        {
            if (source != null)
            {
                action = action ?? ((ignoreItem, ignoreIndex) => { });

                int idx = 0;
                foreach (TItem item in source)
                    action(item, idx++);
            }

            return source;
        }

        /// <summary>Concatinates the given <paramref name="item"/> onto the end of the given <paramref name="set"/></summary>
        public static IEnumerable<TItem> Concat<TItem>(this IEnumerable<TItem> set, TItem item)
        {
            return InnerConcat(set ?? EmptyArrayFactory<TItem>.Instance, item);
        }

        /// <summary>Concatinates (prefixes) the given <paramref name="item"/> in front of the given <paramref name="set"/></summary>
        public static IEnumerable<TItem> Concat<TItem>(this TItem item, IEnumerable<TItem> set)
        {
            return InnerConcat(item, set ?? EmptyArrayFactory<TItem>.Instance);
        }

        /// <summary>Concatinates (prefixes) the given <paramref name="item"/> in front of the given <paramref name="array"/></summary>
        public static IEnumerable<TItem> Concat<TItem>(this TItem item, TItem [] array)
        {
            return InnerConcat(item, array ?? EmptyArrayFactory<TItem>.Instance);
        }

        /// <summary>Concatinates the given items onto the end of the given <paramref name="set"/></summary>
        public static IEnumerable<TItem> Concat<TItem>(this IEnumerable<TItem> set, TItem item1, TItem item2)
        {
            return System.Linq.Enumerable.Concat(set ?? EmptyArrayFactory<TItem>.Instance, new TItem[] { item1, item2 });
        }

        /// <summary>Concatinates the given items onto the end of the given <paramref name="set"/></summary>
        public static IEnumerable<TItem> Concat<TItem>(this IEnumerable<TItem> set, TItem item1, TItem item2, TItem item3)
        {
            return System.Linq.Enumerable.Concat(set ?? EmptyArrayFactory<TItem>.Instance, new TItem[] { item1, item2, item3 });
        }

        /// <summary>Concatinates the given items onto the end of the given <paramref name="set"/></summary>
        public static IEnumerable<TItem> Concat<TItem>(this IEnumerable<TItem> set, TItem item1, TItem item2, TItem item3, params TItem[] moreParamsItemArray)
        {
            return System.Linq.Enumerable.Concat(set ?? EmptyArrayFactory<TItem>.Instance, new TItem[] { item1, item2, item3 }.Concat(moreParamsItemArray ?? EmptyArrayFactory<TItem>.Instance));
        }

        /// <summary>Concatinates (prefixes) the given <paramref name="item"/> in front of the given <paramref name="itemParamsArray"/></summary>
        public static IEnumerable<TItem> ConcatItems<TItem>(this TItem item, params TItem[] itemParamsArray)
        {
            return InnerConcat(item, itemParamsArray ?? EmptyArrayFactory<TItem>.Instance);
        }

        /// <summary>Concatinates the given <paramref name="itemParamsArray"/> params items onto the end of the given <paramref name="set"/></summary>
        public static IEnumerable<TItem> ConcatItems<TItem>(this IEnumerable<TItem> set, params TItem[] itemParamsArray)
        {
            return System.Linq.Enumerable.Concat(set ?? EmptyArrayFactory<TItem>.Instance, itemParamsArray ?? EmptyArrayFactory<TItem>.Instance);
        }

        /// <summary>
        /// If <paramref name="condition"/> is true then this method returns an enumeable with the given itesm concatinated onto the end of the given <paramref name="set"/>.
        /// Otherwise this method returns the given <paramref name="set"/> unmodified.
        /// </summary>
        public static IEnumerable<TItem> ConditionalConcatItems<TItem>(this IEnumerable<TItem> set, bool condition, params TItem [] itemsParamArray)
        {
            if (!condition || itemsParamArray.IsNullOrEmpty())
                return set;
            else
                return (set ?? EmptyArrayFactory<TItem>.Instance).Concat(itemsParamArray);
        }

        private static IEnumerable<TItem> InnerConcat<TItem>(TItem item, IEnumerable<TItem> set)
        {
            yield return item;

            foreach (var setItem in set)
                yield return setItem;

            yield break;
        }

        private static IEnumerable<TItem> InnerConcat<TItem>(IEnumerable<TItem> set, TItem item)
        {
            foreach (var setItem in set)
                yield return setItem;

            yield return item;

            yield break;
        }

        /// <summary>
        /// Linq type extension method that uses the given <paramref name="filterPredicate"/> to select which items in the given list are to be placed in the output set, removes them from the list
        /// and yields them.  If <paramref name="consecutive"/> is set to true then the filter will end as soon as the filter yields at least one element and then finds a following non-matching element.
        /// If <paramref name="fromFront"/> is set to true then the filter will end as soon as the first non-matching element is found.
        /// If <paramref name="filterPredicate"/> is null and <paramref name="mapNullFilterPredicateToAll"/> is true then this method will remove and return all of the elements in the list.
        /// </summary>
        public static IEnumerable<TItemType> FilterAndRemove<TItemType>(this List<TItemType> list, Func<TItemType, bool> filterPredicate = null, bool consecutive = false, bool fromFront = false, bool mapNullFilterPredicateToAll = true)
        {
            if (filterPredicate == null && mapNullFilterPredicateToAll)
                filterPredicate = (item => true);

            if (!list.IsNullOrEmpty() && filterPredicate != null)
            {
                bool foundAny = false;
                for (int idx = 0; idx < list.Count;)
                {
                    TItemType item = list[idx];

                    if (filterPredicate(item))
                    {
                        list.RemoveAt(idx);
                        foundAny = true;
                        yield return item;
                    }
                    else if (consecutive && foundAny || fromFront)
                    {
                        yield break;
                    }
                    else
                    {
                        idx++;
                    }
                }
            }

            yield break;
        }

        /// <summary>
        /// Linq type extension method that processes a given <paramref name="set"/> and returns a set of the given items that are not equal to the default for the given <typeparamref name="TItemType"/>.
        /// Uses the given <paramref name="eqCmp"/> equality comparer or the default one for <typeparamref name="TItemType"/> if the caller does not explicitly provide a non-default <paramref name="eqCmp"/> instance to use.
        /// </summary>
        public static IEnumerable<TItemType> WhereIsNotDefault<TItemType>(this IEnumerable<TItemType> set, IEqualityComparer<TItemType> eqCmp = null)
        {
            eqCmp = eqCmp ?? EqualityComparer<TItemType>.Default;

            var defaultForTItemType = default(TItemType);

            return (set ?? Collections.EmptyArrayFactory<TItemType>.Instance).Where(item => !eqCmp.Equals(item, defaultForTItemType));
        }

        #endregion

        #region Type related extension methods

        /// <summary>
        /// returns the Leaf Name of the given <paramref name="type"/> type (aka: The last token for the resulting sequence of dot seperated tokens)
        /// NOTE: this method has been re-implemented using the GetTypeDigestName(recursive: true) method.
        /// </summary>
        public static string GetTypeLeafName(this Type type) 
        {
            return type.GetTypeDigestName(recursive: true);
        }

        /// <summary>
        /// This method generates a digest version of the name of the given <paramref name="type"/>.  
        /// This consists of removing the namespace prefix from the types name.
        /// For types based on generics, the <paramref name="recursive"/> flag selects whether the method is applied recursively to 
        /// the sub-type.  If so then each of the sub-types will also be digested in the returned string otherwise only
        /// the main type will be digested.
        /// </summary>
        public static string GetTypeDigestName(this Type type, bool recursive = true)
        {
            return type.ToString().MapNullToEmpty().GetTypeDigestName(recursive: recursive);
        }

        /// <summary>
        /// This is a private helper extension method that is used by the GetTypeDigestName method.
        /// It is not made public so that it will not clutter the string extension namespace and because its behavior
        /// is not tested on anything other than actual type name strings.
        /// </summary>
        private static string GetTypeDigestName(this string typeNameStr, bool recursive = true)
        {
            int tickIdx = typeNameStr.IndexOf('`');

            string leftPart = ((tickIdx >= 0) ? typeNameStr.Substring(0, tickIdx) : typeNameStr);
            string rightPart = ((tickIdx >= 0) ? typeNameStr.Substring(tickIdx) : string.Empty);

            int lastDotIdx = leftPart.LastIndexOf('.');
            string leafLeftPart = (lastDotIdx >= 0) ? leftPart.Substring(lastDotIdx + 1) : leftPart;

            if (rightPart.IsNullOrEmpty())
                return leafLeftPart;

            int firstRightPartSqBIdx = rightPart.IndexOf('[');
            int lastRightPartSqBIdx = rightPart.LastIndexOf(']');

            if (firstRightPartSqBIdx < 0 || firstRightPartSqBIdx >= lastRightPartSqBIdx || !recursive)
                return String.Concat(leafLeftPart, rightPart);

            string leafRightOpenPart = rightPart.Substring(0, firstRightPartSqBIdx + 1);
            string leafRightClosePart = rightPart.Substring(lastRightPartSqBIdx);

            string leafRightMiddlePart = rightPart.Substring(firstRightPartSqBIdx + 1, Math.Max(0, lastRightPartSqBIdx - firstRightPartSqBIdx - 1));
            int leafRightMiddlePartLen = leafRightMiddlePart.Length;

            List<string> leafRightMiddlePartSplitList = new List<string>();

            int depth = 0;
            int lastCommaIndex = -1;

            int commaScanIdx = 0;
            for (; commaScanIdx < leafRightMiddlePartLen; commaScanIdx++)
            {
                char c = leafRightMiddlePart[commaScanIdx];

                switch (c)
                {
                    default: break;
                    case '[': depth++; break;
                    case ']': depth = Math.Max(0, depth-1); break;
                    case ',': 
                        if (depth == 0)
                        {
                            if (lastCommaIndex == 0)
                                leafRightMiddlePartSplitList.Add(leafRightMiddlePart.Substring(0, commaScanIdx));
                            else
                                leafRightMiddlePartSplitList.Add(leafRightMiddlePart.Substring(lastCommaIndex + 1, Math.Max(0, commaScanIdx - lastCommaIndex - 1)));

                            lastCommaIndex = commaScanIdx;
                        }
                        break;
                }
            }

            if (lastCommaIndex < 0)
                leafRightMiddlePartSplitList.Add(leafRightMiddlePart);
            else
                leafRightMiddlePartSplitList.Add(leafRightMiddlePart.Substring(lastCommaIndex + 1));

            string leafRightLeafMiddlePart = String.Join(",", leafRightMiddlePartSplitList.Select(midTypeName => midTypeName.GetTypeDigestName(recursive: true)).ToArray());

            return String.Concat(leafLeftPart, leafRightOpenPart, leafRightLeafMiddlePart, leafRightClosePart);
        }

        /// <summary>Returns true if the give type <paramref name="t"/> is Nullable</summary>
        public static bool IsNullable(this Type t)
        {
            return (t != null && t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(System.Nullable<>)));
        }

        /// <summary>For Nullable types <paramref name="t"/> this method returns the base type (from first type in GetGenericArguments()).  Otherwise this method return <paramref name="fallbackValue"/>.</summary>
        public static Type GetNullableBaseType(this Type t, Type fallbackValue = null)
        {
            if (t.IsNullable())
                return t.GetGenericArguments().SafeAccess(0, defaultValue: fallbackValue);

            return fallbackValue;
        }

        /// <summary>Returns the default value for the given type <paramref name="t"/>.  Will be null for reference types.</summary>
        public static object CreateDefaultInstance(this Type t)
        {
            return (t.IsValueType ? Activator.CreateInstance(t) : null);
        }

        #endregion

        //---------------------------
        #region Exception related methods (ToString)

        /// <summary>
        /// Exception formatting helper extension method.  Accepts a given System.Exception and a ExceptionFormat parameter
        /// and returns a string describing the exception according to the desired format.
        /// </summary>
        public static string ToString(this System.Exception ex, ExceptionFormat exceptionFormat)
        {
            try
            {
                if (ex == null)
                    return "Null Exception";

                if (exceptionFormat == ExceptionFormat.Default)
                    return string.Format("{0}", ex);

                StringBuilder sb = new StringBuilder("Exception");

                if (exceptionFormat.IsSet(ExceptionFormat.IncludeType))
                    sb.AppendFormat(" Type:{0}", ex.GetType());

                if (exceptionFormat.IsSet(ExceptionFormat.IncludeSource) && !ex.Source.IsNullOrEmpty())
                    sb.AppendFormat(" Src:{0}", ex.Source);

                if (exceptionFormat.IsSet(ExceptionFormat.IncludeMessage) && !ex.Message.IsNullOrEmpty())
                    sb.AppendFormat(" Mesg:[{0}]", ex.Message);

                //if (exceptionFormat.IsSet(ExceptionFormat.IncludeData) && !ex.Data.IsNullOrEmpty())
                //{
                //    NamedValueSet nvs = new NamedValueSet().AddRange(ex.Data);
                //    sb.AppendFormat(" Data:{0}", nvs.ToString(includeROorRW: false, treatNameWithEmptyVCAsKeyword: false));
                //}

                if (exceptionFormat.IsSet(ExceptionFormat.IncludeStackTrace) && !ex.StackTrace.IsNullOrEmpty())
                    sb.AppendFormat(" Stack:[{0}]", ex.StackTrace.Trim());

                return sb.ToString();
            }
            catch
            {
                return "Internal: System.Exception.ToString(fmt) threw unexpected exception";
            }
        }

        #endregion
    }

    /// <summary>
    /// Flag Enumeration used with System.Exception ToString extension method above.  Defines the string contents to be included from the exception.
    /// <para/>Default (0x00), TypeAndMessage (0x05), TypeAndMessageAndStackTrace (0x15), Full (0x1f), AllButStackTrace (0x0f), 
    /// <para/>IncludeType (0x01), IncludeSource (0x02), IncludeMessage (0x04), IncludeData (0x08), IncludeStackTrace (0x10)
    /// </summary>
    [Flags]
    public enum ExceptionFormat : int
    {
        /// <summary>Use basic System.Exception.ToString method [x00]</summary>
        Default = 0x00,

        /// <summary>IncludeType | IncludeMessage [0x05]</summary>
        TypeAndMessage = (IncludeType | IncludeMessage),

        /// <summary>IncludeType | IncludeMessage | IncludeStackTrace [0x15]</summary>
        TypeAndMessageAndStackTrace = (IncludeType | IncludeMessage | IncludeStackTrace),

        /// <summary>IncludeType | IncludeSource | IncludeMessage | IncludeData | IncludeStackTrace [0x1f]</summary>
        Full = (IncludeType | IncludeSource | IncludeMessage | IncludeData | IncludeStackTrace),

        /// <summary>IncludeType | IncludeSource | IncludeMessage | IncludeData [0x0f]</summary>
        AllButStackTrace = (IncludeType | IncludeSource | IncludeMessage | IncludeData),

        /// <summary>Includes Type: field [0x01]</summary>
        IncludeType = 0x01,

        /// <summary>Includes Src: field [0x02]</summary>
        IncludeSource = 0x02,

        /// <summary>Includes Mesg:[] field [0x04]</summary>
        IncludeMessage = 0x04,

        /// <summary>Includes Data:[] field [0x08]</summary>
        IncludeData = 0x08,

        /// <summary>Includes Stack:[] field [0x10]</summary>
        IncludeStackTrace = 0x10,
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

        #region CurrentStackFrame, CurrentMethod, CurrentMethodName, CurrentClassName, CurrentClassLeafName, CurrentProcessMainModuleShortName helper "functions (getters)".

        /// <summary>Creates and returns the callers current StackFrame</summary>
        public static StackFrame CurrentStackFrame 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return new System.Diagnostics.StackFrame(1); } 
        }

        /// <summary>Creates a StackFrame for the caller and returns the stack frame's current method.</summary>
        public static MethodBase CurrentMethod 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return new System.Diagnostics.StackFrame(1).GetMethod(); } 
        }

        /// <summary>Creates a StackFrame for the caller and returns the Name of the stack frame's current method.</summary>
        public static string CurrentMethodName 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return new System.Diagnostics.StackFrame(1).GetMethod().Name; } 
        }

        /// <summary>Creates a StackFrame for the caller and returns the Name of the current methods DeclaringType</summary>
        public static string CurrentClassName 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString(); } 
        }

        /// <summary>Creates a StackFrame for the caller and returns the Leaf Name of the current methods DeclaringType (The token at the end of any sequence of dot seperated tokens)</summary>
        public static string CurrentClassLeafName 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return (new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType).GetTypeLeafName(); } 
        }

        /// <summary>
        /// Attemts to obtain and return the filename (without extension) from the ModuleName of the Current Process's MainModule.
        /// </summary>
        public static string CurrentProcessMainModuleShortName 
        { 
            get 
            {
                try
                {
                    System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                    return System.IO.Path.GetFileNameWithoutExtension(currentProcess.MainModule.ModuleName);
                }
                catch
                {
                    return "[MainModuleNameCouldNotBeFound]";
                }
            }
        }

        #endregion
    }

    #endregion
}

//-------------------------------------------------------------------
