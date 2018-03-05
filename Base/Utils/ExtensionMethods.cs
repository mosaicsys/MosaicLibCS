//-------------------------------------------------------------------
/*! @file ExtensionMethods.cs
 *  @brief This file contains a number of extension methods
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2013 Mosaic Systems Inc.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using MosaicLib.Modular.Common;
using MosaicLib.Time;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Utils
{
    #region Extension Methods

    /// <summary>
    /// standard location for ExtensionMethods added under MosaicLib.Utils.
    /// </summary>
    public static partial class ExtensionMethods
    {
        #region Array and IList comparisons extension methods

        /// <summary>
        /// Extension method version of static Utils.Fcns.Equals method for two arrays
        /// Returns true if both arrays have the same length and contents (using Object.Equals).  Returns false if they do not.
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

        /// <summary>
        /// Retruns true if the pair of given System.Array objects are the same size, the same length and who's corresponding content items are Equal
        /// </summary>
        public static bool IsEqualTo(this System.Array lhs, System.Array rhs)
        {
            return Utils.Fcns.Equals(lhs, rhs);
        }

        #endregion

        #region Array, IList, List and String access extension methods (IsSafeIndex, SafeAccess, SafeSubArray, SafePut, SafeLast, SafeCopyFrom)

        /// <summary>
        /// Extension method accepts given <paramref name="array"/> and <paramref name="testIndex"/> and returns true if the <paramref name="array"/> is non-null and the <paramref name="testIndex"/> is >= 0 and less than the <paramref name="array"/>.Length
        /// </summary>
        public static bool IsSafeIndex<ItemType>(this ItemType[] array, int testIndex, int length = 1)
        {
            return (array != null && testIndex >= 0 && length > 0 && (testIndex + length) <= array.Length);
        }

        /// <summary>
        /// Extension method accepts given <paramref name="list"/> and <paramref name="testIndex"/> and returns true if the <paramref name="list"/> is non-null and the <paramref name="testIndex"/> is >= 0 and less than the <paramref name="list"/>.Count
        /// </summary>
        public static bool IsSafeIndex<ItemType>(this IList<ItemType> list, int testIndex, int length = 1)
        {
            return (list != null && testIndex >= 0 && length > 0 && (testIndex + length) <= list.Count);
        }

        /// <summary>
        /// Extension method version of Array indexed get access that handles all out of range accesses by returning the given default value
        /// </summary>
        public static ItemType SafeAccess<ItemType>(this ItemType[] fromArray, int getFromIndex, ItemType defaultValue = default(ItemType))
        {
            if (fromArray.IsSafeIndex(getFromIndex))
                return fromArray[getFromIndex];
            else
                return defaultValue;
        }

        /// <summary>
        /// Extension method version of Array indexed sub-array get access that handles all out of range accesses by returning either a partial sub-array or an empty array.
        /// </summary>
        [Obsolete("This method has been renamed to SafeSubArray to avoid ambiguity in the applicable set of EMs with the same name.  Please convert to use the new SafeSubArray method (2017-09-20)")]
        public static ItemType[] SafeAccess<ItemType>(this ItemType[] fromArray, int getFromIndex, int getSubArrayLength)
        {
            return fromArray.SafeSubArray(getFromIndex, getSubArrayLength);
        }

        /// <summary>
        /// Extension method version of Array indexed sub-array get access that handles all out of range accesses by returning either a partial sub-array or an empty array.
        /// </summary>
        public static ItemType[] SafeSubArray<ItemType>(this ItemType[] fromArray, int getFromIndex, int getSubArrayLength = int.MaxValue)
        {
            int fromArrayLength = fromArray.SafeLength();

            getSubArrayLength = Math.Max(0, Math.Min(getSubArrayLength, (fromArrayLength - getFromIndex)));

            ItemType[] subArray = new ItemType[getSubArrayLength];

            if (fromArray != null && getFromIndex >= 0 && getFromIndex < fromArrayLength)
                System.Array.Copy(fromArray, getFromIndex, subArray, 0, getSubArrayLength);

            return subArray;
        }

        /// <summary>
        /// Extension method version of IList indexed sub-array get access that handles all out of range accesses by returning either a partial sub-array or an empty array.
        /// </summary>
        public static ItemType[] SafeSubArray<ItemType>(this IList<ItemType> fromList, int getFromIndex, int getSubArrayLength = int.MaxValue)
        {
            int fromListCount = fromList.SafeCount();

            getSubArrayLength = Math.Max(0, Math.Min(getSubArrayLength, (fromListCount - getFromIndex)));

            ItemType[] subArray = new ItemType[getSubArrayLength];

            if (fromList != null && getFromIndex >= 0 && getFromIndex < fromListCount)
            {
                for (int putToIndex = 0; putToIndex < getSubArrayLength && getFromIndex < fromListCount; putToIndex++, getFromIndex++)
                    subArray[putToIndex] = fromList[getFromIndex];
            }

            return subArray;
        }

        /// <summary>
        /// Extension method version of string indexed get access that handles all out of range accesses by returning the given default value
        /// </summary>
        public static char SafeAccess(this string fromStr, int getFromIndex, char defaultValue = default(char))
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

        /// <summary>
        /// Extension method version of Array that returns the last item of the array or the given defaultValue if the array is null or it is empty
        /// </summary>
        public static ItemType SafeLast<ItemType>(this ItemType[] fromArray, ItemType defaultValue = default(ItemType))
        {
            if (fromArray == null || fromArray.Length <= 0)
                return defaultValue;
            else
                return fromArray[fromArray.Length - 1];
        }

        /// <summary>
        /// Extension method version of Array.Copy with copy length clipped to prevent exceptions
        /// </summary>
        public static void SafeCopyFrom<ItemType>(this ItemType[] copyIntoArray, ItemType[] copyFromArray, int copyFromIndex = 0, int copyLengthLimitIn = int.MaxValue)
        {
            copyIntoArray.SafeCopyFrom(copyToIndex: 0, copyFromArray: copyFromArray, copyFromIndex: copyFromIndex, copyLengthLimitIn: copyLengthLimitIn);
        }

        /// <summary>
        /// Extension method version of Array.Copy with copy length clipped to prevent exceptions
        /// </summary>
        public static void SafeCopyFrom<ItemType>(this ItemType[] copyIntoArray, int copyToIndex, ItemType[] copyFromArray, int copyFromIndex = 0, int copyLengthLimitIn = int.MaxValue)
        {
            int fromArrayLength = copyFromArray.SafeLength();
            int intoArrayLength = copyIntoArray.SafeLength();

            int copyLength = Math.Max(0, Math.Min(copyLengthLimitIn, Math.Min(fromArrayLength - copyFromIndex, intoArrayLength - copyToIndex)));

            if (copyLength > 0)
                System.Array.Copy(copyFromArray, copyFromIndex, copyIntoArray, copyToIndex, copyLength);
        }

        #endregion

        #region Other Array, IList, List, IEnumerable related extension methods (IsNullOrEmpty, IsEmpty, MapNullToEmpty, SafeLength, SafeCount, SetAll, Clear, SafeAccess, SafeToArray, SafeTakeFirst, SafeTakeLast, SafeAddSet, SafeAddItems, ConditionalAddItems)

        /// <summary>
        /// Extension method returns true if the given array is null or its Length is zero.
        /// </summary>
        public static bool IsNullOrEmpty<TItemType>(this TItemType[] array)
        {
            return ((array == null) || (array.Length == 0));
        }

        /// <summary>
        /// Extension method returns true if given <paramref name="collection"/> is null or empty (Count == 0)
        /// </summary>
        public static bool IsNullOrEmpty(this ICollection collection)
        {
            return (collection == null || collection.Count == 0);
        }

        /// <summary>
        /// Extension method returns true if the given IEnumerable <paramref name="ien"/> is null or empty (initial MoveNext returns false).
        /// </summary>
        public static bool IsNullOrEmpty(this IEnumerable ien)
        {
            return ((ien == null) || (ien.GetEnumerator().MoveNext() == false));
        }

        /// <summary>
        /// Extension method returns true if given <paramref name="array"/> is null or empty (Length == 0)
        /// </summary>
        public static bool IsEmpty<ItemType>(this ItemType[] array)
        {
            return (array == null || array.Length == 0);
        }

        /// <summary>
        /// Extension method returns true if given <paramref name="collection"/> is null or empty (Count == 0)
        /// </summary>
        public static bool IsEmpty<TItemType>(this ICollection<TItemType> collection)
        {
            return (collection == null || collection.Count == 0);
        }

        /// <summary>
        /// Extension method returns true if the given IEnumerable <paramref name="ien"/> is null or empty (initial MoveNext returns false).
        /// </summary>
        public static bool IsEmpty(this IEnumerable ien)
        {
            return ((ien == null) || (ien.GetEnumerator().MoveNext() == false));
        }

        /// <summary>
        /// Extension method either returns the given <paramref name="arrayIn"/> (if it is not null) or returns a new empty array of <typeparamref name="TItemType"/> if the given <paramref name="arrayIn"/> is null.
        /// </summary>
        public static TItemType [] MapNullToEmpty<TItemType>(this TItemType [] arrayIn, TItemType [] fallbackArray = null)
        {
            return arrayIn ?? fallbackArray ?? EmptyArrayFactory<TItemType>.Instance;
        }

        /// <summary>
        /// Extension method returns the given <paramref name="arrayIn"/> is non-empty otherwise this method returns null.
        /// </summary>
        public static TItemType[] MapEmptyToNull<TItemType>(this TItemType[] arrayIn)
        {
            return (arrayIn.SafeLength() != 0) ? arrayIn : null;
        }

        /// <summary>
        /// Extension method either returns the given <paramref name="listIn"/> (if it is not null) or returns a new empty List{TItemType} if the given <paramref name="listIn"/> is null.
        /// </summary>
        public static List<TItemType> MapNullToEmpty<TItemType>(this List<TItemType> listIn)
        {
            return listIn ?? new List<TItemType>();
        }

        /// <summary>
        /// Extension method either returns the given <paramref name="iListIn"/> (if it is not null) or returns a new empty List{TItemType} if the given <paramref name="iListIn"/> is null.
        /// </summary>
        public static IList<TItemType> MapNullToEmpty<TItemType>(this IList<TItemType> iListIn)
        {
            return iListIn ?? new List<TItemType>();
        }

        /// <summary>
        /// Extension method returns the given <paramref name="iListIn"/> is non-empty otherwise this method returns null.
        /// </summary>
        public static IList<TItemType> MapEmptyToNull<TItemType>(this IList<TItemType> iListIn)
        {
            return (iListIn.SafeCount() != 0) ? iListIn : null;
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
        /// Extension method sets all of the elements of the given <paramref name="array"/> to the given <paramref name="value"/>.  Has no effect if the <paramref name="array"/> is null or is zero length.
        /// </summary>
        public static void SetAll<ItemType>(this ItemType[] array, ItemType value)
        {
            int numItems = ((array != null) ? array.Length : 0);

            for (int idx = 0; idx < numItems; idx++)
                array[idx] = value;
        }

        /// <summary>
        /// Extension method sets all of the elements of the given <paramref name="arraySegment"/> to the given <paramref name="value"/>.  Has no effect if the <paramref name="arraySegment"/> is empty.
        /// </summary>
        public static void SetAll<ItemType>(this ArraySegment<ItemType> arraySegment, ItemType value)
        {
            ItemType[] array = arraySegment.Array;
            int endIdx = Math.Min(arraySegment.Offset + arraySegment.Count, array.SafeLength());

            for (int idx = arraySegment.Offset; idx < endIdx; idx++)
                array[idx] = value;
        }

        /// <summary>
        /// Extension method sets all of the elements of the given array to their default value.  Has no effect if the array is null or is zero length.
        /// </summary>
        public static void Clear<ItemType>(this ItemType[] array)
        {
            array.SetAll(default(ItemType));
        }

        /// <summary>
        /// Extension method version of IList indexed get access that handles all out of range accesses by returning the given default value
        /// </summary>
        public static ItemType SafeAccess<ItemType>(this IList<ItemType> fromList, int getFromIndex, ItemType defaultValue = default(ItemType))
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
        /// Extension method "safe" version of LockedObjectListWithCachedArray.Array property.  If the given <paramref name="list"/> is non-null then this method returns the Linq ToArray method applied to the <paramref name="list"/>.
        /// If the <paramref name="list"/> is null and the given <paramref name="fallbackArray"/> is non-null then this method returns the <paramref name="fallbackArray"/>.
        /// If the <paramref name="list"/> and the <paramref name="fallbackArray"/> values are null then this creates and returns an empty array of the given ItemType (<paramref name="mapNullToEmpty"/> is true) or null (<paramref name="mapNullToEmpty"/> is false)
        /// </summary>
        public static ItemType[] SafeToArray<ItemType>(this Utils.Collections.LockedObjectListWithCachedArray<ItemType> list, ItemType[] fallbackArray = null, bool mapNullToEmpty = true)
        {
            if (list != null)
                return list.Array;

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

        /// <summary>
        /// Extension method to make a copy of the given <paramref name="array"/>.  
        /// If the caller provided <paramref name="array"/> is null then this method uses <paramref name="fallbackArray"/> and then <paramref name="mapNullToEmpty"/>
        /// to define the actual return value.
        /// </summary>
        /// <param name="array">Gives the caller provided array that this method is to make a copy of.</param>
        /// <param name="fallbackArray">If the given <paramref name="array"/> is null and this parameter is non-null then this method returns the value given to this parameter.</param>
        /// <param name="mapNullToEmpty">If both the given <paramref name="array"/> and the <paramref name="fallbackArray"/> parameters are null then if mapNullToEmpty is false, this method will return null, otherwise this method will return a new empty {ItemType} array.</param>
        public static ItemType[] MakeCopyOf<ItemType>(this ItemType[] array, ItemType[] fallbackArray = null, bool mapNullToEmpty = true)
        {
            if (array != null)
            {
                int arrayLength = array.Length;
                ItemType[] resultArray = new ItemType[arrayLength];

                System.Array.Copy(array, resultArray, arrayLength);

                return resultArray;
            }
            else
            {
                return fallbackArray ?? (mapNullToEmpty ? EmptyArrayFactory<ItemType>.Instance : null);
            }
        }

        /// <summary>
        /// Extension method to add a set of items (from an enumerable source) to the given <paramref name="list"/>.  
        /// If createListIfNeeded is true, <paramref name="list"/> is null and <paramref name="itemSet"/> is non-empty then this method will construct a new list to contain the itemSet items which will be returned.
        /// <para/>Supports call chaining.
        /// </summary>
        public static List<ItemType> SafeAddSet<ItemType>(this List<ItemType> list, IEnumerable<ItemType> itemSet, bool createListIfNeeded = true)
        {
            if (!itemSet.IsNullOrEmpty())
            {
                if (list != null)
                    list.AddRange(itemSet);
                else if (createListIfNeeded)
                    list = new List<ItemType>(itemSet);
            }

            return list;
        }

        /// <summary>
        /// Extension method to add a group of items (params parameter) to a given <paramref name="list"/>)
        /// If <paramref name="list"/> is null and <paramref name="itemsArray"/> is non-empty then this method will construct a new list to contain the itemSet items which will be returned.
        /// <para/>Supports call chaining.
        /// </summary>
        public static List<ItemType> SafeAddItems<ItemType>(this List<ItemType> list, params ItemType[] itemsArray)
        {
            if (!itemsArray.IsNullOrEmpty())
            {
                if (list != null)
                    list.AddRange(itemsArray);
                else
                    list = new List<ItemType>(itemsArray);
            }

            return list;
        }

        [Obsolete("This method is being replaced with similarly named SafeAddItems nomenclature now used with params itemsArray.  Please convert to using the new naming. (2017-11-01)")]
        public static List<ItemType> SafeAddSet<ItemType>(this List<ItemType> list, ItemType item, params ItemType[] itemSetArray)
        {
            if (list != null)
            {
                list.Add(item);
                if (!itemSetArray.IsEmpty())
                    list.AddRange(itemSetArray);
            }

            return list;
        }

        /// <summary>
        /// Extension method to conditionally add an item (or group thereof - params parameter) to a given <paramref name="list"/>)
        /// If <paramref name="list"/> is null and <paramref name="itemsArray"/> is non-empty then this method will construct a new list to contain the itemSet items which will be returned.
        /// <para/>Supports call chaining.
        /// </summary>
        public static List<ItemType> ConditionalAddItems<ItemType>(this List<ItemType> list, bool condition, params ItemType[] itemsArray)
        {
            if (condition)
                return list.SafeAddItems(itemsArray);
            else
                return list;
        }

        #endregion

        #region IList methods (ConvertToReadOnly, ConvertToWriteable)

        /// <summary>
        /// Extension method either returns the given <paramref name="iListIn"/> (if it is alread a ReadOnlyIList instance) or returns a new ReadOnlyIList{TItemType} if the given <paramref name="iListIn"/>, or null if the given <paramref name="iListIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static IList<TItemType> ConvertToReadOnly<TItemType>(this IList<TItemType> iListIn, bool mapNullToEmpty = true)
        {
            ReadOnlyIList<TItemType> roIList = iListIn as ReadOnlyIList<TItemType>;

            return roIList ?? ((iListIn != null || mapNullToEmpty) ? new ReadOnlyIList<TItemType>(iListIn) : null);
        }

        /// <summary>
        /// Extension method either returns the given <paramref name="iListIn"/> (if it is not IsReadOnly) or returns a new List{TItemType} if the given <paramref name="iListIn"/> is non-empty, or null if the given <paramref name="iListIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static IList<TItemType> ConvertToWritable<TItemType>(this IList<TItemType> iListIn, bool mapNullToEmpty = true)
        {
            if (iListIn != null)
                return !iListIn.IsReadOnly ? iListIn : new List<TItemType>(iListIn);
            else
                return mapNullToEmpty ? new List<TItemType>() : null;
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

        #region Math and number related extension methods (Ceiling, Floor, Round, Clip, IsInRange, IncrementSkipZero, SafeOneOver)

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
        public static double Round(this double value, int digits, MidpointRounding mode = MidpointRounding.ToEven)
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

        #region Enum extension methods (IsSet, IsAnySet, IsMatch, Set, Clear)

        /// <summary>
        /// Returns true if the given <paramref name="value"/> contains the given <paramref name="test"/> pattern (by bit mask test).  Eqivilant to <paramref name="value"/>.IsMatch(<paramref name="test"/>, <paramref name="test"/>)
        /// <para/>This extension method is intended for use with enumerations (especially Flag enumerations) and integer value types.  Use with unsupported types will simply return false;
        /// </summary>
        public static bool IsSet<TValueType>(this TValueType value, TValueType test) where TValueType : struct
        {
            return value.IsMatch(test, test);
        }

        /// <summary>
        /// Returns true if the given value anded with the given mask is not zero:  return ((value &amp; mask) != default(value)).
        /// <para/>This extension method is intended for use with enumerations (especially Flag enumerations) and integer value types.  Use with unsupported types will simply return false;
        /// </summary>
        public static bool IsAnySet<TValueType>(this TValueType value, TValueType mask)
        {
            return !value.IsMatch(mask, default(TValueType));
        }

        /// <summary>
        /// Returns true if the given <paramref name="value"/> does not contain any part (bit) from the given <paramref name="test"/> pattern (by bit mask test).  Eqivilant to <paramref name="value"/>.IsMatch(<paramref name="test"/>, default(<typeparamref name="TValueType"/>))
        /// <para/>This extension method is intended for use with enumerations (especially Flag enumerations) and integer value types.  Use with unsupported types will simply return false;
        /// </summary>
        public static bool IsClear<TValueType>(this TValueType value, TValueType test) where TValueType : struct
        {
            return value.IsMatch(test, default(TValueType));
        }

        /// <summary>
        /// Returns true if the given value bit anded with the given mask is equal to the given match:  return ((value &amp; mask) == match).
        /// <para/>This extension method is intended for use with enumerations (especially Flag enumerations) and integer value types.  Use with unsupported types will simply return false;
        /// </summary>
        public static bool IsMatch<TValueType>(this TValueType value, TValueType mask, TValueType match)
        {
            try
            {
                TypeCode typeCode = TypeCode.Empty;

                if (value is System.Enum)
                {
                    System.Enum startingValueAsEnum = value as System.Enum;
                    typeCode = startingValueAsEnum.GetTypeCode();
                }
                else if (value is IConvertible)
                {
                    IConvertible ic = (IConvertible) value;
                    typeCode = ic.GetTypeCode();
                }

                switch (typeCode)
                {
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                        ulong valueU8 = (ulong)System.Convert.ChangeType(value, typeof(ulong));
                        ulong maskU8 = (ulong)System.Convert.ChangeType(mask, typeof(ulong));
                        ulong matchU8 = (ulong)System.Convert.ChangeType(match, typeof(ulong));

                        return ((valueU8 & maskU8) == matchU8);

                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                        uint valueU4 = (uint)System.Convert.ChangeType(value, typeof(uint));
                        uint maskU4 = (uint)System.Convert.ChangeType(mask, typeof(uint));
                        uint matchU4 = (uint)System.Convert.ChangeType(match, typeof(uint));

                        return ((valueU4 & maskU4) == matchU4);

                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Char:
                        ushort valueU2 = (ushort)System.Convert.ChangeType(value, typeof(ushort));
                        ushort maskU2 = (ushort)System.Convert.ChangeType(mask, typeof(ushort));
                        ushort matchU2 = (ushort)System.Convert.ChangeType(match, typeof(ushort));

                        return ((valueU2 & maskU2) == matchU2);

                    case TypeCode.SByte:
                    case TypeCode.Byte:
                        byte valueU1 = (byte)System.Convert.ChangeType(value, typeof(byte));
                        byte maskU1 = (byte)System.Convert.ChangeType(mask, typeof(byte));
                        byte matchU1 = (byte)System.Convert.ChangeType(match, typeof(byte));

                        return ((valueU1 & maskU1) == matchU1);

                    default:
                    case TypeCode.Empty:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// For Enum derived <typeparamref name="TValueType"/> types, this method accepts the given startingValue and pattern and returns startingValue &amp; ~pattern.
        /// <para/>This extension method is intended for use with enumerations (especially Flag enumerations) and integer value types.  Use with unsupported types will simply return default(startingValue);
        /// </summary>
        public static TValueType Clear<TValueType>(this TValueType startingValue, TValueType pattern) where TValueType : struct
        {
            return Set(startingValue, pattern, setPattern: false);
        }

        /// <summary>
        /// For Enum derived <typeparamref name="TValueType"/> types, this method accepts the given startingValue and pattern and returns either startingValue | pattern (setPattern: true) or startingValue &amp; ~pattern (setPattern: false) depending on the given value of setPattern.
        /// <para/>This extension method is intended for use with enumerations (especially Flag enumerations) and integer value types.  Use with unsupported types will simply return default(startingValue);
        /// </summary>
        public static TValueType Set<TValueType>(this TValueType startingValue, TValueType pattern, bool setPattern = true) where TValueType : struct
        {
            try
            {
                TypeCode typeCode = TypeCode.Empty;
                bool isEnum = false;

                if (startingValue is System.Enum)
                {
                    System.Enum startingValueAsEnum = startingValue as System.Enum;
                    typeCode = startingValueAsEnum.GetTypeCode();
                    isEnum = true;
                }
                else if (startingValue is IConvertible)
                {
                    IConvertible ic = (IConvertible)startingValue;
                    typeCode = ic.GetTypeCode();
                }

                switch (typeCode)
                {
                    case TypeCode.Int64:
                        {
                            long valueI8 = (long)System.Convert.ChangeType(startingValue, typeof(long));
                            long patternI8 = (long)System.Convert.ChangeType(pattern, typeof(long));

                            if (setPattern)
                                valueI8 |= patternI8;
                            else
                                valueI8 &= ~patternI8;

                            if (isEnum)
                                return (TValueType)System.Enum.ToObject(typeof(TValueType), valueI8);
                            else
                                return (TValueType)((System.Object)valueI8);
                        }

                    case TypeCode.UInt64:
                        {
                            ulong valueU8 = (ulong)System.Convert.ChangeType(startingValue, typeof(ulong));
                            ulong patternU8 = (ulong)System.Convert.ChangeType(pattern, typeof(ulong));

                            if (setPattern)
                                valueU8 |= patternU8;
                            else
                                valueU8 &= ~patternU8;

                            if (isEnum)
                                return (TValueType)System.Enum.ToObject(typeof(TValueType), valueU8);
                            else
                                return (TValueType)((System.Object)valueU8);
                        }

                    case TypeCode.Int32:
                        {
                            int valueI4 = (int)System.Convert.ChangeType(startingValue, typeof(int));
                            int patternI4 = (int)System.Convert.ChangeType(pattern, typeof(int));

                            if (setPattern)
                                valueI4 |= patternI4;
                            else
                                valueI4 &= ~patternI4;

                            if (isEnum)
                                return (TValueType)System.Enum.ToObject(typeof(TValueType), valueI4);
                            else
                                return (TValueType)((System.Object)valueI4);
                        }

                    case TypeCode.UInt32:
                        {
                            uint valueU4 = (uint)System.Convert.ChangeType(startingValue, typeof(uint));
                            uint patternU4 = (uint)System.Convert.ChangeType(pattern, typeof(uint));

                            if (setPattern)
                                valueU4 |= patternU4;
                            else
                                valueU4 &= ~patternU4;

                            if (isEnum)
                                return (TValueType)System.Enum.ToObject(typeof(TValueType), valueU4);
                            else
                                return (TValueType)((System.Object)valueU4);
                        }

                    case TypeCode.Int16:
                        {
                            short valueI2 = (short)System.Convert.ChangeType(startingValue, typeof(short));
                            short patternI2 = (short)System.Convert.ChangeType(pattern, typeof(short));

                            if (setPattern)
                                valueI2 |= patternI2;
                            else
                                valueI2 &= (short)~patternI2;

                            if (isEnum)
                                return (TValueType)System.Enum.ToObject(typeof(TValueType), valueI2);
                            else
                                return (TValueType)((System.Object)valueI2);
                        }

                    case TypeCode.UInt16:
                    case TypeCode.Char:
                        {
                            ushort valueU2 = (ushort)System.Convert.ChangeType(startingValue, typeof(ushort));
                            ushort patternU2 = (ushort)System.Convert.ChangeType(pattern, typeof(ushort));

                            if (setPattern)
                                valueU2 |= patternU2;
                            else
                                valueU2 &= (ushort)~patternU2;

                            if (isEnum)
                                return (TValueType)System.Enum.ToObject(typeof(TValueType), valueU2);
                            else
                                return (TValueType)((System.Object)valueU2);
                        }

                    case TypeCode.SByte:
                        {
                            sbyte valueI1 = (sbyte)System.Convert.ChangeType(startingValue, typeof(sbyte));
                            sbyte patternI1 = (sbyte)System.Convert.ChangeType(pattern, typeof(sbyte));

                            if (setPattern)
                                valueI1 |= patternI1;
                            else
                                valueI1 &= (sbyte)~patternI1;

                            if (isEnum)
                                return (TValueType)System.Enum.ToObject(typeof(TValueType), valueI1);
                            else
                                return (TValueType)((System.Object)valueI1);
                        }

                    case TypeCode.Byte:
                        {
                            byte valueU1 = (byte)System.Convert.ChangeType(startingValue, typeof(byte));
                            byte patternU1 = (byte)System.Convert.ChangeType(pattern, typeof(byte));

                            if (setPattern)
                                valueU1 |= patternU1;
                            else
                                valueU1 &= (byte)~patternU1;

                            if (isEnum)
                                return (TValueType)System.Enum.ToObject(typeof(TValueType), valueU1);
                            else
                                return (TValueType)((System.Object)valueU1);
                        }

                    default:
                    case TypeCode.Empty:
                        return default(TValueType);
                }
            }
            catch
            {
                return default(TValueType);
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

        #region Linq extensions (DoForEach, Concat, FilterAndRemove)

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

            string leafRightLeafMiddlePart = String.Join(",", leafRightMiddlePartSplitList.Select(midTypeName => midTypeName.GetTypeDigestName(recursive: true)));

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

        #region FileSystemInfo, FileInfo EMs (SafeGetOldestDateTimeUtc, SafeGetCreationAge, SafeExists, SafeLength)

        /// <summary>Returns the DateTime that is the oldest from the given FileSystemInfo <paramref name="fsi"/>'s creation time, last modified time, and last accessed time, in UTC.</summary>
        public static DateTime SafeGetOldestDateTimeUtc(this FileSystemInfo fsi, DateTime fallbackValue = default(DateTime))
        {
            if (fsi.SafeExists())
                return fsi.CreationTimeUtc.Min(fsi.LastWriteTimeUtc).Min(fsi.LastAccessTimeUtc);
            else
                return fallbackValue;
        }

        /// <summary>Returns the amount of time that has elapsed from the FileSystemInfo.CreateionTime to now as a TimeSpan (returns zero if the given <paramref name="fsi"/> instance is null)</summary>
        public static TimeSpan SafeGetCreationAge(this FileSystemInfo fsi, TimeSpan fallbackValue = default(TimeSpan))
        {
            if (fsi.SafeExists())
                return fsi.CreationTimeUtc.Age();
            else
                return fallbackValue;
        }

        /// <summary>Returns true if the given <paramref name="fsi"/> is not null and it Exists</summary>
        public static bool SafeExists(this FileSystemInfo fsi)
        {
            return (fsi != null && fsi.Exists);
        }

        /// <summary>If the given <paramref name="fi"/> is non-null and it Exists then this method returns its Length, otherwise this method returns 0</summary>
        public static long SafeLength(this FileInfo fi)
        {
            return (fi.SafeExists() ? fi.Length : 0);
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
                    sb.AppendFormat(" Mesg:[{0}]", ex.Message.GenerateEscapedVersion(boxEscapeCharsList));

                if (exceptionFormat.IsSet(ExceptionFormat.IncludeData) && !ex.Data.IsNullOrEmpty())
                {
                    NamedValueSet nvs = new NamedValueSet().AddRange(ex.Data);
                    sb.AppendFormat(" Data:{0}", nvs.ToString(includeROorRW: false, treatNameWithEmptyVCAsKeyword: false));
                }

                if (exceptionFormat.IsSet(ExceptionFormat.IncludeStackTrace) && !ex.StackTrace.IsNullOrEmpty())
                    sb.AppendFormat(" Stack:[{0}]", ex.StackTrace.Trim().GenerateEscapedVersion(boxEscapeCharsList));

                return sb.ToString();
            }
            catch
            {
                return "Internal: System.Exception.ToString(fmt) threw unexpected exception";
            }
        }

        private static readonly IList<char> boxEscapeCharsList = new ReadOnlyIList<char>(new [] { '[', ']' });

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

        /// <summary>
        /// Retruns true if the pair of given System.Array objects are the same size, the same length and who's corresponding content items are Equal
        /// </summary>
        public static bool Equals(System.Array lhs, System.Array rhs)
        {
            if (System.Object.ReferenceEquals(lhs, rhs))
                return true;

            if (lhs == null || rhs == null || lhs.Length != rhs.Length)
                return false;

            return lhs.SafeToArray<object>().SequenceEqual(rhs.SafeToArray<object>());
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
