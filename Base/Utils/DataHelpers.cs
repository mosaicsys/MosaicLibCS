//-------------------------------------------------------------------
/*! @file DataHelpers.cs
 * This file contains a series of utility classes that are used to encapuslate
 * track, record and/or manage data objects.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2006 Mosaic Systems Inc.  (C++ library version)
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
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

using MosaicLib.Utils.Collections;

namespace MosaicLib.Utils
{
    #region utility interfaces: ICopyable<TObjectType>

    /// <summary>
    /// This interface is used with generics so that they can make use of some type of copy construction in a generic manner through the use of the MakeCopyOfThis method defined by this interface.
    /// </summary>
    public interface ICopyable<TObjectType>
    {
        /// <summary>
        /// Requests the current object to make a copy of itself of the given type.  
        /// If deepCopy is true then the copy shall be a deep copy otherwise it can be any type of copy the object defines.
        /// In many cases this method will only produce deep copies.
        /// </summary>
        TObjectType MakeCopyOfThis(bool deepCopy = true);
    }

    #endregion

    //-------------------------------------------------
	#region Data Packing, Unpacking, Byte Order manipulations

	/// <summary>
	/// This static partial class provides static helper methods that can be used to pack and unpack values.
	/// Byte array versions always use network order (msb first).
	/// </summary>
	public static partial class Data
    {
        #region Packing

        /// <summary>Packs the given pair of bytes as a UInt16 and returns it</summary>
        public static UInt16 Pack(Byte msb, Byte lsb) { unchecked { return (UInt16) ((((UInt32) msb) << 8) | ((UInt32) lsb)); } }
        /// <summary>Packs the given set of 4 bytes as a UInt32 and returns it</summary>
        public static UInt32 Pack(Byte umsb, Byte ulsb, Byte lmsb, Byte llsb) { unchecked { return ((((UInt32)umsb) << 24) | (((UInt32)ulsb) << 16) | (((UInt32)lmsb) << 8) | ((UInt32)llsb)); } }
        /// <summary>Packs the given set of 4 bytes as a UInt32 and returns it</summary>
        public static UInt64 Pack(Byte msb8, Byte msb7, Byte msb6, Byte msb5, Byte msb4, Byte msb3, Byte msb2, Byte lsb) { unchecked { return ((((UInt64)msb8) << 56) | (((UInt64)msb7) << 48) | (((UInt64)msb6) << 40) | ((UInt64)msb5) << 32) | ((((UInt64)msb4) << 24) | (((UInt64)msb3) << 16) | (((UInt64)msb2) << 8) | ((UInt64)lsb)); } }
        /// <summary>Packs the given set of 3 bytes as the lower 24 bits of a UInt32 and returns it</summary>
        public static UInt32 Pack(Byte ulsb, Byte lmsb, Byte llsb) { unchecked { return ((((UInt32)ulsb) << 16) | (((UInt32)lmsb) << 8) | ((UInt32)llsb)); } }
        /// <summary>Packs the given pair of UInt16 values as a UInt32 and returns it</summary>
        public static UInt32 Pack(UInt16 msw, UInt16 lsw) { unchecked { return ((((UInt32)msw) << 16) | ((UInt32)lsw)); } }

        /// <summary>Packs a pair of bytes, in big endian order, from the indicated location in the given byteArray, and places them in the UInt16 value output.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to obtain the required number of bytes</returns>
        public static bool Pack(Byte[] byteArray, int baseIdx, out UInt16 value) 
		{ 
			value = 0;

            if (!byteArray.IsSafeIndex(baseIdx, length: 2))
                return false;

            value = Pack(byteArray [baseIdx], byteArray [baseIdx + 1]);
			return true;
		}

        /// <summary>Packs a set of 4 bytes, in big endian order, from the indicated location in the given byteArray, and places them in the UInt32 value output.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to obtain the required number of bytes</returns>
        public static bool Pack(Byte[] byteArray, int baseIdx, out UInt32 value) 
		{ 
			value = 0;

            if (!byteArray.IsSafeIndex(baseIdx, length: 4))
                return false;

            value = Pack(byteArray [baseIdx], byteArray [baseIdx + 1], byteArray [baseIdx + 2], byteArray [baseIdx + 3]);
			return true;
        }

        /// <summary>Packs a set of 8 bytes, in big endian order, from the indicated location in the given byteArray, and places them in the UInt64 value output.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to obtain the required number of bytes</returns>
        public static bool Pack(Byte[] byteArray, int baseIdx, out UInt64 value)
        {
            value = 0;

            if (!byteArray.IsSafeIndex(baseIdx, length: 8))
                return false;

            value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2], byteArray[baseIdx + 3], byteArray[baseIdx + 4], byteArray[baseIdx + 5], byteArray[baseIdx + 6], byteArray[baseIdx + 7]);
            return true;
        }

        /// <summary>Packs a set of the given number of bytes (1, 2, 3, or 4, in big endian order) from the indicated location in the given byteArray, and places them in the UInt32 value output.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to obtain the required number of bytes, or if numBytes is not a supported value.</returns>
        public static bool Pack(Byte[] byteArray, int baseIdx, int numBytes, out UInt32 value)
        {
            value = 0;

            if (!byteArray.IsSafeIndex(baseIdx, length: numBytes))
                return false;

            switch (numBytes)
            {
                case 1: value = byteArray[baseIdx]; return true;
                case 2: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1]); return true;
                case 3: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2]); return true;
                case 4: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2], byteArray[baseIdx + 3]); return true;
                default: return false;
            }
        }

        /// <summary>Packs a set of the given number of bytes (2, 3, or 4, in big endian order) from the indicated location in the given byteArray, and places them in the UInt32 value output.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to obtain the required number of bytes, or if numBytes is not a supported value.</returns>
        public static bool Pack(Byte[] byteArray, int baseIdx, int numBytes, out UInt64 value)
        {
            value = 0;

            if (!byteArray.IsSafeIndex(baseIdx, length: numBytes))
                return false;

            switch (numBytes)
            {
                case 1: value = byteArray[baseIdx]; return true;
                case 2: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1]); return true;
                case 3: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2]); return true;
                case 4: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2], byteArray[baseIdx + 3]); return true;
                case 5: value = Pack(0, 0, 0, byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2], byteArray[baseIdx + 3], byteArray[baseIdx + 4]); return true;
                case 6: value = Pack(0, 0, byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2], byteArray[baseIdx + 3], byteArray[baseIdx + 4], byteArray[baseIdx + 5]); return true;
                case 7: value = Pack(0, byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2], byteArray[baseIdx + 3], byteArray[baseIdx + 4], byteArray[baseIdx + 5], byteArray[baseIdx + 6]); return true;
                case 8: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2], byteArray[baseIdx + 3], byteArray[baseIdx + 4], byteArray[baseIdx + 5], byteArray[baseIdx + 6], byteArray[baseIdx + 7]); return true;
                default: return false;
            }
        }

        /// <summary>Packs and returns 2 bytes from the indicated location in the given byteArray in BigEndian Order.  Returns 0 if any of the indicates bytes are not accessible.</summary>
        public static UInt16 Pack2(Byte[] byteArray, int baseIdx = 0)
        {
            UInt16 value;
            Pack(byteArray, baseIdx, out value);
            return value;
        }

        /// <summary>Packs and returns 3 bytes from the indicated location in the given byteArray in BigEndian Order.  Returns 0 if any of the indicates bytes are not accessible.</summary>
        public static UInt32 Pack3(Byte[] byteArray, int baseIdx = 0)
        {
            UInt32 value;
            Pack(byteArray, baseIdx, 3, out value);
            return value;
        }

        /// <summary>Packs and returns 4 bytes from the indicated location in the given byteArray in BigEndian Order.  Returns 0 if any of the indicates bytes are not accessible.</summary>
        public static UInt32 Pack4(Byte[] byteArray, int baseIdx = 0)
        {
            UInt32 value;
            Pack(byteArray, baseIdx, out value);
            return value;
        }

        /// <summary>Packs and returns 8 bytes from the indicated location in the given byteArray in BigEndian Order.  Returns 0 if any of the indicates bytes are not accessible.</summary>
        public static UInt64 Pack8(Byte[] byteArray, int baseIdx = 0)
        {
            UInt64 value;

            Pack(byteArray, baseIdx, out value);
            
            return value;
        }

        #endregion

        #region Unpacking

        /// <summary>Unpacks 2 bytes from the given UInt16 value and saves them in the corresponding output variables</summary>
        public static void Unpack(UInt16 w, out Byte msb, out Byte lsb)
		{
			unchecked
			{
				msb = (Byte) (w >> 8);
				lsb = (Byte) (w >> 0);
			}
		}

        /// <summary>Unpacks 2 words from the given UInt32 value and saves them in the corresponding output variables</summary>
        public static void Unpack(UInt32 l, out UInt16 msw, out UInt16 lsw)
		{
			unchecked
			{
				msw = (UInt16) (l >> 16);
				lsw = (UInt16) (l >> 0);
			}
		}

        /// <summary>Unpacks 3 bytes from the given UInt32 value and saves them in the corresponding output variables</summary>
        public static void Unpack(UInt32 l, out Byte ulsb, out Byte lmsb, out Byte llsb)
        {
            unchecked
            {
                l = (l & 0xffffff);
                ulsb = (Byte)(l >> 16);
                lmsb = (Byte)(l >> 8);
                llsb = (Byte)(l >> 0);
            }
        }

        /// <summary>Unpacks 4 bytes from the given UInt32 value and saves them in the corresponding output variables</summary>
        public static void Unpack(UInt32 l, out Byte umsb, out Byte ulsb, out Byte lmsb, out Byte llsb)
		{
			unchecked
			{
				umsb = (Byte) (l >> 24);
				ulsb = (Byte) (l >> 16);
				lmsb = (Byte) (l >> 8);
				llsb = (Byte) (l >> 0);
			}
		}

        /// <summary>Unpacks 8 bytes from the given UInt64 value and saves them in the corresponding output variables</summary>
        public static void Unpack(UInt64 l, out Byte uumsb, out Byte uulsb, out Byte ulmsb, out Byte ullsb, out Byte lumsb, out Byte lulsb, out Byte llmsb, out Byte lllsb)
        {
            unchecked
            {
                uumsb = (Byte)(l >> 56);
                uulsb = (Byte)(l >> 48);
                ulmsb = (Byte)(l >> 40);
                ullsb = (Byte)(l >> 32);
                lumsb = (Byte)(l >> 24);
                lulsb = (Byte)(l >> 16);
                llmsb = (Byte)(l >> 8);
                lllsb = (Byte)(l >> 0);
            }
        }

        /// <summary>Unpacks 4 words from the given UInt64 value and saves them in the corresponding output variables</summary>
        public static void Unpack(UInt64 l, out UInt16 umsw, out UInt16 ulsw, out UInt16 lmsw, out UInt16 llsw)
        {
            unchecked
            {
                umsw = (UInt16)(l >> 48);
                ulsw = (UInt16)(l >> 32);
                lmsw = (UInt16)(l >> 16);
                llsw = (UInt16)(l >> 0);
            }
        }


        /// <summary>Unpacks 2 bytes from the given UInt16 value and saves them in the give byteArray at the given baseIdx offset.  Uses Big Endian byte ordering.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to save the required number of bytes.</returns>
        public static bool Unpack(UInt16 w, byte[] byteArray, int baseIdx = 0)
		{
			if (byteArray == null || baseIdx < 0 || ((baseIdx + 2) > byteArray.Length))
				return false;
			Unpack(w, out byteArray [baseIdx], out byteArray [baseIdx + 1]);
			return true;
		}

        /// <summary>Unpacks 4 bytes from the given UInt32 value and saves them in the give byteArray at the given baseIdx offset.  Uses Big Endian byte ordering.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to save the required number of bytes.</returns>
        public static bool Unpack(UInt32 l, byte[] byteArray, int baseIdx = 0)
		{
			if (byteArray == null || baseIdx < 0 || ((baseIdx + 4) > byteArray.Length))
				return false;
			Unpack(l, out byteArray [baseIdx], out byteArray [baseIdx + 1], out byteArray [baseIdx + 2], out byteArray [baseIdx + 3]);
			return true;
        }

        /// <summary>Unpacks 8 bytes from the given UInt64 value and saves them in the give byteArray at the given baseIdx offset.  Uses Big Endian byte ordering.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to save the required number of bytes.</returns>
        public static bool Unpack(UInt64 l, byte[] byteArray, int baseIdx = 0)
        {
            if (byteArray == null || baseIdx < 0 || ((baseIdx + 8) > byteArray.Length))
                return false;
            Unpack(l, out byteArray[baseIdx], out byteArray[baseIdx + 1], out byteArray[baseIdx + 2], out byteArray[baseIdx + 3], out byteArray[baseIdx + 4], out byteArray[baseIdx + 5], out byteArray[baseIdx + 6], out byteArray[baseIdx + 7]);
            return true;
        }

        /// <summary>Unpacks the given number of bytes (1, 2, 3 or 4) from the given UInt32 value and saves them in the give byteArray at the given baseIdx offset.  Uses Big Endian byte ordering.</summary>
        /// <returns>True on success, false if the byteArray or baseIdx could not be used to save the required number of bytes, or if numBytes is not a supported value.</returns>
        public static bool Unpack(UInt32 l, byte[] byteArray, int baseIdx, int numBytes)
        {
            if (byteArray == null || baseIdx < 0 || ((baseIdx + numBytes) > byteArray.Length))
                return false;

            switch (numBytes)
            {
                case 1: byteArray[baseIdx] = (byte) (l & 0x00ff); return true;
                case 2: Unpack((UInt16) (l & 0xffff), out byteArray[baseIdx], out byteArray[baseIdx + 1]); return true;
                case 3: Unpack(l, out byteArray[baseIdx], out byteArray[baseIdx + 1], out byteArray[baseIdx + 2]); return true;
                case 4: Unpack(l, out byteArray[baseIdx], out byteArray[baseIdx + 1], out byteArray[baseIdx + 2], out byteArray[baseIdx + 3]); return true;
                default: return false;
            }
        }

        #endregion

        #region Byte order manipulation

        /// <summary>Enum defines the two known types of byte ordering</summary>
        public enum ByteOrder
        {
            /// <summary>Little Endian Byte Ordering - words are stored in memory with the least significant byte first.</summary>
            LittleEndian, 
            /// <summary>
            /// Big Endian Byte Ordering - words are stored in memory with the most significant byte first.  
            /// Allows values to be read from a byte hex dump.  Often used as, and referred to as, Network byte order.  (see htons, ntohs, htonl, ntohl)</summary>
            BigEndian,
        }

        /// <summary>Returns the ByteOrder of the current execution environment.</summary>
        public static ByteOrder MachineOrder { get { return ((BitConverter.IsLittleEndian) ? ByteOrder.LittleEndian : ByteOrder.BigEndian); } }
        /// <summary>Returns true if the MachineOrder is ByteOrder.LittleEndian</summary>
        public static bool IsMachineLittleEndian { get { return (MachineOrder == ByteOrder.LittleEndian); } }
        /// <summary>Returns true if the MachineOrder is ByteOrder.BigEndian</summary>
        public static bool IsMachineBigEndian { get { return (MachineOrder == ByteOrder.BigEndian); } }

        /// <summary>Attempts to change the byte order for itemSize number of bytes in the given array from the given byte order to the current Machine byte order.</summary>
        /// <param name="byteArray">Gives the byteArray that contains the bytes to be re-ordered.</param>
        /// <param name="baseIdx">Gives the index of the first byte for the item in the byte array to be re-ordered</param>
        /// <param name="itemSize">Gives the item size of the item in the byte array to be re-ordered (1, 2, 3, 4 or 8)</param>
        /// <param name="fromByteOrder">Gives the byte order that the item is already known to be in.</param>
        /// <returns>
        /// True if the conversion was successful, 
        /// false if the item in the byteArray could not be successfully converted either because the baseIndex was out of bounds or because the itemSize is not a supported value.
        /// </returns>
        public static bool ChangeByteOrder(byte[] byteArray, int baseIdx, int itemSize, ByteOrder fromByteOrder) 
        { 
            return ChangeByteOrder(byteArray, baseIdx, itemSize, fromByteOrder, MachineOrder); 
        }

        /// <summary>Attempts to change the byte order for itemSize number of bytes in the given array from the given byte order to the given byte order.</summary>
        /// <param name="byteArray">Gives the byteArray that contains the bytes to be re-ordered.</param>
        /// <param name="baseIdx">Gives the index of the first byte for the item in the byte array to be re-ordered</param>
        /// <param name="itemSize">Gives the item size of the item in the byte array to be re-ordered (1, 2, 3, 4 or 8)</param>
        /// <param name="fromByteOrder">Gives the byte order that the item is already known to be in.</param>
        /// <param name="toByteOrder">Gives the byte order that the item is be converted to.</param>
        /// <returns>
        /// True if the conversion was successful, 
        /// false if the item in the byteArray could not be successfully converted either because the baseIndex was out of bounds or because the itemSize is not a supported value.
        /// </returns>
        public static bool ChangeByteOrder(byte[] byteArray, int baseIdx, int itemSize, ByteOrder fromByteOrder, ByteOrder toByteOrder)
        {
            if (byteArray == null || baseIdx < 0 || byteArray.Length < baseIdx + itemSize)
                return false;

            switch (itemSize)
            {
                case 1:
                    return true;
                case 2:
                    if (fromByteOrder == toByteOrder)
                        return true;

                    Swap<byte>(ref byteArray[baseIdx + 0], ref byteArray[baseIdx + 1]);
                    return true;
                case 3:
                    if (fromByteOrder == toByteOrder)
                        return true;

                    Swap<byte>(ref byteArray[baseIdx + 0], ref byteArray[baseIdx + 2]);
                    return true;
                case 4:
                    if (fromByteOrder == toByteOrder)
                        return true;

                    Swap<byte>(ref byteArray[baseIdx + 0], ref byteArray[baseIdx + 3]);
                    Swap<byte>(ref byteArray[baseIdx + 1], ref byteArray[baseIdx + 2]);
                    return true;
                case 8:
                    if (fromByteOrder == toByteOrder)
                        return true;

                    Swap<byte>(ref byteArray[baseIdx + 0], ref byteArray[baseIdx + 7]);
                    Swap<byte>(ref byteArray[baseIdx + 1], ref byteArray[baseIdx + 6]);
                    Swap<byte>(ref byteArray[baseIdx + 2], ref byteArray[baseIdx + 5]);
                    Swap<byte>(ref byteArray[baseIdx + 3], ref byteArray[baseIdx + 4]);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Attempts to change the byte order for numItems * itemSize number of bytes in the given array from the given byte order to the current Machine byte order.</summary>
        /// <param name="byteArray">Gives the byteArray that contains the bytes of the items to be re-ordered.</param>
        /// <param name="baseIdx">Gives the index of the first byte for the items in the byte array that are to be re-ordered</param>
        /// <param name="numItems">Gives the number of itmes who's bytes are to be re-ordered</param>
        /// <param name="itemSize">Gives the item size of each item in the byte array to be re-ordered (1, 2, 3, 4 or 8)</param>
        /// <param name="fromByteOrder">Gives the byte order that the item is already known to be in</param>
        /// <returns>
        /// True if the conversion was successful, 
        /// false if the item in the byteArray could not be successfully converted either because the baseIndex was out of bounds or because the itemSize is not a supported value.
        /// </returns>
        public static bool ChangeByteOrder(byte[] byteArray, int baseIdx, int numItems, int itemSize, ByteOrder fromByteOrder) 
        { 
            return ChangeByteOrder(byteArray, baseIdx, itemSize, numItems, fromByteOrder, MachineOrder); 
        }

        /// <summary>Attempts to change the byte order for numItems * itemSize number of bytes in the given array from the given byte order to the given byte order.</summary>
        /// <param name="byteArray">Gives the byteArray that contains the bytes of the items to be re-ordered.</param>
        /// <param name="baseIdx">Gives the index of the first byte for the items in the byte array that are to be re-ordered</param>
        /// <param name="numItems">Gives the number of itmes who's bytes are to be re-ordered</param>
        /// <param name="itemSize">Gives the item size of each item in the byte array to be re-ordered(1, 2, 3, 4 or 8)</param>
        /// <param name="fromByteOrder">Gives the byte order that the item is already known to be in</param>
        /// <param name="toByteOrder">Gives the byte order that the item is be converted to.</param>
        /// <returns>
        /// True if the conversion was successful, 
        /// false if the item in the byteArray could not be successfully converted either because the baseIndex was out of bounds or because the itemSize is not a supported value.
        /// </returns>
        public static bool ChangeByteOrder(byte[] byteArray, int baseIdx, int numItems, int itemSize, ByteOrder fromByteOrder, ByteOrder toByteOrder)
        {
            if (byteArray == null || baseIdx < 0 || byteArray.Length < baseIdx + (itemSize * numItems) || numItems < 0)
                return false;

            switch (itemSize)
            {
                case 1:
                    return true;
                case 2:
                    if (fromByteOrder == toByteOrder || numItems == 0)
                        return true;

                    for (; numItems > 0; --numItems)
                    {
                        Swap<byte>(ref byteArray[baseIdx + 0], ref byteArray[baseIdx + 1]);
                        baseIdx += 2;
                    }
                    return true;
                case 3:
                    if (fromByteOrder == toByteOrder || numItems == 0)
                        return true;

                    for (; numItems > 0; --numItems)
                    {
                        Swap<byte>(ref byteArray[baseIdx + 0], ref byteArray[baseIdx + 2]);
                        baseIdx += 3;
                    }
                    return true;
                case 4:
                    if (fromByteOrder == toByteOrder || numItems == 0)
                        return true;

                    for (; numItems > 0; --numItems)
                    {
                        Swap<byte>(ref byteArray[baseIdx + 0], ref byteArray[baseIdx + 3]);
                        Swap<byte>(ref byteArray[baseIdx + 1], ref byteArray[baseIdx + 2]);
                        baseIdx += 4;
                    }
                    return true;
                case 8:
                    if (fromByteOrder == toByteOrder || numItems == 0)
                        return true;

                    for (; numItems > 0; --numItems)
                    {
                        Swap<byte>(ref byteArray[baseIdx + 0], ref byteArray[baseIdx + 7]);
                        Swap<byte>(ref byteArray[baseIdx + 1], ref byteArray[baseIdx + 6]);
                        Swap<byte>(ref byteArray[baseIdx + 2], ref byteArray[baseIdx + 5]);
                        Swap<byte>(ref byteArray[baseIdx + 3], ref byteArray[baseIdx + 4]);
                        baseIdx += 8;
                    }
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Templatized method that may be used to swap a pair of referenced values or object handles.</summary>
        /// <typeparam name="TypeT">Gives the type of the reference variables who's contents are to be swapped.</typeparam>
        public static void Swap<TypeT>(ref TypeT left, ref TypeT right)
        {
            TypeT temp = left;
            left = right;
            right = temp;
        }

        #endregion
    }

	#endregion

    #region Extension methods for marshaling between byte arrays and other objects

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// This method obtains the byte array length that will be required in order to marshal the given <paramref name="type"/> of object to a byte array.
        /// </summary>
        public static int GetMarshaledByteArraySize(this System.Type type, int fallbackValue = 0, bool rethrow = true)
        {
            try
            {
                int length = Marshal.SizeOf(type);

                return length;
            }
            catch (System.Exception ex)
            {
                if (rethrow && ex != null)
                    throw;

                return fallbackValue;
            }
        }

        /// <summary>
        /// This method obtains the byte array length that will be required in order to marshal the given <paramref name="value"/> object to a byte array.
        /// </summary>
        public static int GetMarshaledByteArraySize<TObjType>(this TObjType value, int fallbackValue = 0, bool rethrow = true)
        {
            try
            {
                int length = Marshal.SizeOf(value);

                return length;
            }
            catch (System.Exception ex)
            {
                if (rethrow && ex != null)
                    throw;

                return fallbackValue;
            }
        }

        /// <summary>
        /// This method allocates a byte array of the length given by Marshal.Sizeof({TObjType}), 
        /// pins it and marshals the given <paramref name="value"/>'s contents into the allocated byte array 
        /// which is unpinned and returned.  
        /// If the method encounteres any error (exceptions) this method will either <paramref name="rethrow"/> (when requested)
        /// or will return the given <paramref name="fallbackValue"/>.
        /// </summary>
        public static byte[] MarshalStructToByteArray<TObjType>(this TObjType value, byte[] fallbackValue = null, bool rethrow = true)
        {
            GCHandle gch = default(GCHandle);
            IntPtr gchP = default(IntPtr);

            try
            {
                int length = Marshal.SizeOf(typeof(TObjType));
                byte [] data = new byte [length];

                gch = GCHandle.Alloc(data, GCHandleType.Pinned);
                gchP = gch.AddrOfPinnedObject();

                Marshal.StructureToPtr(value, gchP, false);

                return data;
            }
            catch (System.Exception ex)
            {
                if (rethrow && ex != null)
                    throw;

                return fallbackValue;
            }
            finally
            {
                gchP = default(IntPtr);

                if (gch.IsAllocated)
                    gch.Free();
            }
        }

        /// <summary>
        /// This method pins the given <paramref name="byteArray"/>, verifies that it has sufficient size to receive the <paramref name="value"/>'s contents,
        /// and marshals those contents into the byte array starting at the given <paramref name="startIdx"/>. 
        /// Method returns the number of bytes produced after unpining the byte array.
        /// If the method encounteres any error (exceptions) this method will either <paramref name="rethrow"/> (when requested)
        /// or will return 0.
        /// </summary>
        public static int MarshalStructIntoToByteArray<TObjType>(this TObjType value, byte[] byteArray, int startIdx = 0, bool rethrow = true)
        {
            GCHandle gch = default(GCHandle);
            IntPtr gchP = default(IntPtr);

            try
            {
                int length = Marshal.SizeOf(typeof(TObjType));

                if (byteArray != null)
                    gch = GCHandle.Alloc(byteArray, GCHandleType.Pinned);

                if (gch.IsAllocated && byteArray.IsSafeIndex(startIdx, length))
                {
                    gchP = gch.AddrOfPinnedObject();
                    if (startIdx != 0)
                        gchP = IntPtr.Add(gchP, startIdx);
                }
                else
                {
                    throw new System.IndexOutOfRangeException("invalid combination of size, startIdx, and length [{0}, {1}, {2}]".CheckedFormat(byteArray.SafeCount(), startIdx, length));
                }

                Marshal.StructureToPtr(value, gchP, false);

                return length;
            }
            catch (System.Exception ex)
            {
                if (rethrow && ex != null)
                    throw;

                return 0;
            }
            finally
            {
                gchP = default(IntPtr);

                if (gch.IsAllocated)
                    gch.Free();
            }
        }

        /// <summary>
        /// This method pins the given byteArray, computes a relative offset into the byte array, 
        /// and marshals the indicated bytes into a new instance of the given <typeparamref name="TObjType"/> which is returned after unpinning the given byteArray.
        /// If the method encounteres any error (exceptions) this method will either <paramref name="rethrow"/> (when requested)
        /// or will return the given <paramref name="fallbackValue"/>.
        /// </summary>
        public static TObjType MarshalStructFromByteArray<TObjType>(this byte[] byteArray, int startIdx = 0, TObjType fallbackValue = default(TObjType), bool rethrow = true, int? byteCountIn = null)
        {
            TObjType value;

            byteArray.MarshalStructFromByteArray(out value, startIdx: startIdx, fallbackValue: fallbackValue, rethrow: rethrow, availableByteCountIn: byteCountIn);

            return value;
        }

        /// <summary>
        /// This method pins the given <paramref name="byteArray"/>, computes a relative offset into it, verifies that the byte array is sufficiently large, 
        /// and marshals the indicated bytes into a new instance of the given <typeparamref name="TObjType"/> which is assigned to the given <paramref name="valueOut"/> out parameter.  The number of bytes produced is returned after unpinning the given byteArray.
        /// If the method encounteres any error (exceptions) this method will either <paramref name="rethrow"/> (when requested)
        /// or will assign the given <paramref name="fallbackValue"/> and return zero.
        /// </summary>
        public static int MarshalStructFromByteArray<TObjType>(this byte[] byteArray, out TObjType valueOut, int startIdx = 0, TObjType fallbackValue = default(TObjType), bool strictLength = true, bool rethrow = true, int? availableByteCountIn = null)
        {
            GCHandle gch = default(GCHandle);
            IntPtr gchP = default(IntPtr);

            try
            {
                int length = Marshal.SizeOf(typeof(TObjType));

                if (byteArray != null)
                {
                    gch = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
                }

                int testLength = (strictLength ? length : 1);

                bool lengthLessThanGivenAvailableByteCount = ((availableByteCountIn == null) || (testLength <= availableByteCountIn));

                if (byteArray.IsSafeIndex(startIdx, length: testLength) && gch.IsAllocated && lengthLessThanGivenAvailableByteCount)
                {
                    gchP = gch.AddrOfPinnedObject();
                    if (startIdx != 0)
                        gchP = IntPtr.Add(gchP, startIdx);
                }
                else
                {
                    throw new System.IndexOutOfRangeException("invalid combination of size, and startIdx [{0}, {1}]".CheckedFormat(byteArray.SafeCount(), startIdx));
                }

                valueOut = (TObjType)Marshal.PtrToStructure(gchP, typeof(TObjType));

                return Math.Min(length, byteArray.SafeCount() - startIdx);
            }
            catch (System.Exception ex)
            {
                valueOut = fallbackValue;

                if (rethrow && ex != null)
                    throw;

                return 0;
            }
            finally
            {
                gchP = default(IntPtr);

                if (gch.IsAllocated)
                    gch.Free();
            }
        }
        private static readonly byte[] emptyByteArray = EmptyArrayFactory<byte>.Instance;
    }

    #endregion

    //-------------------------------------------------
	#region Atomic Value types

    /// <summary>Defines an interface to the Atomic Value storage and manipulation types used here.</summary>
    /// <typeparam name="ValueType">Gives the underlying storage type on which the interface is defined.</typeparam>
    public interface IAtomicValue<ValueType> where ValueType : struct
    {
		/// <summary>Provide accessor to underlying value as a volatile value (without locking)</summary>
        ValueType VolatileValue { get; set; }

        /// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
        ValueType Value { get; set; }

        /// <summary>Performs Interlocked.Increment on the contained value.</summary>
        ValueType Increment();
        /// <summary>Performs Interlocked.Increment on the contained value, once or twice, so as to produce a non-zero value</summary>
        ValueType IncrementSkipZero();
        /// <summary>Performs Interlocked.Decrement on the contained value.</summary>
        ValueType Decrement();
        /// <summary>Performs Interlocked.Add on the contained value.</summary>
        ValueType Add(ValueType value);
        /// <summary>Performs Interlocked.Exchange on the contained value.</summary>
        ValueType Exchange(ValueType value);
        /// <summary>Performs Interlocked.CompareExchange on the contained value.</summary>
        ValueType CompareExchange(ValueType value, ValueType comparand);
    }

    // suppress "warning CS0420: 'xxxx': a reference to a volatile field will not be treated as volatile"
    //	The following structs are designed to support use of atomic, interlocked operations on volatile values
    #pragma warning disable 0420

	/// <summary>
	/// This struct provides the standard System.Threading.Interlocked operations wrapped around a volatile System.Int32 value.  This is done to allow us to suppress the warnings that are generated when passing a volatile by reference
	/// </summary>
    public struct AtomicInt32 : IAtomicValue<System.Int32>
	{
        /// <summary> Constructor.  Requires explicit definition of initial value. </summary>
        /// <param name="initialValue">Gives the initial value contained in the created AtomicValue object</param>
        public AtomicInt32(System.Int32 initialValue) { value = initialValue; }

		private volatile System.Int32 value;

		/// <summary>Provide accessor to underlying value as a volatile value</summary>
		public System.Int32 VolatileValue { get { return this.value; } set { this.value = value; }}

		/// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
		public System.Int32 Value
		{
			get { return CompareExchange(0, 0); }
			set { Exchange(value); }
		}

        /// <summary>Performs Interlocked.Increment on the contained value.</summary>
        public System.Int32 Increment() { return System.Threading.Interlocked.Increment(ref this.value); }
        /// <summary>Performs Interlocked.Increment on the contained value, once or twice, so as to produce a non-zero value</summary>
        public System.Int32 IncrementSkipZero() { Int32 value = Increment(); while (value == 0) value = Increment(); return value; }
        /// <summary>Performs Interlocked.Decrement on the contained value.</summary>
        public System.Int32 Decrement() { return System.Threading.Interlocked.Decrement(ref this.value); }
        /// <summary>Performs Interlocked.Add on the contained value.</summary>
        public System.Int32 Add(System.Int32 value) { return System.Threading.Interlocked.Add(ref this.value, value); }
        /// <summary>Performs Interlocked.Exchange on the contained value.</summary>
        public System.Int32 Exchange(System.Int32 value) { return System.Threading.Interlocked.Exchange(ref this.value, value); }
        /// <summary>Performs Interlocked.CompareExchange on the contained value.</summary>
        public System.Int32 CompareExchange(System.Int32 value, System.Int32 comparand) { return System.Threading.Interlocked.CompareExchange(ref this.value, value, comparand); }

        /// <summary>Debugging helper</summary>
        public override string ToString()
        {
            return "Value:{0}".CheckedFormat(VolatileValue);
        }
    }

	/// <summary>
	/// This struct provides the same functionality as AtomicInt32 but casted to act as an UInt32
	/// </summary>
    public struct AtomicUInt32 : IAtomicValue<System.UInt32>
	{
        /// <summary> Constructor.  Requires explicit definition of initial value. </summary>
        /// <param name="initialValue">Gives the initial value contained in the created AtomicValue object</param>
        public AtomicUInt32(System.UInt32 initialValue) { ai32 = new AtomicInt32(unchecked((Int32)initialValue)); }

		private AtomicInt32 ai32;

		/// <summary>Provide accessor to underlying value as a volatile value</summary>
		public System.UInt32 VolatileValue { get { return unchecked((UInt32) ai32.VolatileValue); } set { ai32.VolatileValue = unchecked((Int32) value); } }

		/// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
		public System.UInt32 Value { get { return unchecked((UInt32) ai32.Value); } set { ai32.Value = unchecked((Int32) value); } }

        /// <summary>Performs Interlocked.Increment on the contained value.</summary>
        public System.UInt32 Increment() { return unchecked((UInt32)ai32.Increment()); }
        /// <summary>Performs Interlocked.Increment on the contained value, once or twice, so as to produce a non-zero value</summary>
        public System.UInt32 IncrementSkipZero() { return unchecked((UInt32)ai32.IncrementSkipZero()); }
        /// <summary>Performs Interlocked.Decrement on the contained value.</summary>
        public System.UInt32 Decrement() { return unchecked((UInt32)ai32.Decrement()); }
        /// <summary>Performs Interlocked.Add on the contained value.</summary>
        public System.UInt32 Add(System.UInt32 value) { return unchecked((UInt32)ai32.Add(unchecked((Int32)value))); }
        /// <summary>Performs Interlocked.Exchange on the contained value.</summary>
        public System.UInt32 Exchange(System.UInt32 value) { return unchecked((UInt32)ai32.Exchange(unchecked((Int32)value))); }
        /// <summary>Performs Interlocked.CompareExchange on the contained value.</summary>
        public System.UInt32 CompareExchange(System.UInt32 value, System.UInt32 comparand) { return unchecked((UInt32)ai32.CompareExchange(unchecked((Int32)value), unchecked((Int32)comparand))); }

        /// <summary>Debugging helper</summary>
        public override string ToString()
        {
            return "Value:{0}".CheckedFormat(VolatileValue);
        }
    }

    /// <summary>
    /// This struct provides the standard System.Threading.Interlocked operations wrapped around a volatile System.Int64 value.  This is done to allow us to suppress the warnings that are generated when passing a volatile by reference
    /// </summary>
    public struct AtomicInt64 : IAtomicValue<System.Int64>
    {
        /// <summary> Constructor.  Requires explicit definition of initial value. </summary>
        /// <param name="initialValue">Gives the initial value contained in the created AtomicValue object</param>
        public AtomicInt64(System.Int64 initialValue) { value = initialValue; }

        private System.Int64 value;     // cannot be volatile since bus access to this object is not allways atomic

        /// <summary>Provide accessor to underlying value as a volatile value</summary>
        public System.Int64 VolatileValue { get { return this.value; } set { this.value = value; } }

        /// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
        public System.Int64 Value
        {
            get { return CompareExchange(0, 0); }
            set { Exchange(value); }
        }

        /// <summary>Performs Interlocked.Increment on the contained value.</summary>
        public System.Int64 Increment() { return System.Threading.Interlocked.Increment(ref this.value); }
        /// <summary>Performs Interlocked.Increment on the contained value, once or twice, so as to produce a non-zero value</summary>
        public System.Int64 IncrementSkipZero() { Int64 value = Increment(); while (value == 0) value = Increment(); return value; }
        /// <summary>Performs Interlocked.Decrement on the contained value.</summary>
        public System.Int64 Decrement() { return System.Threading.Interlocked.Decrement(ref this.value); }
        /// <summary>Performs Interlocked.Add on the contained value.</summary>
        public System.Int64 Add(System.Int64 value) { return System.Threading.Interlocked.Add(ref this.value, value); }
        /// <summary>Performs Interlocked.Exchange on the contained value.</summary>
        public System.Int64 Exchange(System.Int64 value) { return System.Threading.Interlocked.Exchange(ref this.value, value); }
        /// <summary>Performs Interlocked.CompareExchange on the contained value.</summary>
        public System.Int64 CompareExchange(System.Int64 value, System.Int64 comparand) { return System.Threading.Interlocked.CompareExchange(ref this.value, value, comparand); }

        /// <summary>Debugging helper</summary>
        public override string ToString()
        {
            return "Value:{0}".CheckedFormat(VolatileValue);
        }
    }

    /// <summary>
    /// This struct provides the same functionality as AtomicInt64 but casted to act as an UInt64
    /// </summary>
    public struct AtomicUInt64 : IAtomicValue<System.UInt64>
    {
        /// <summary> Constructor.  Requires explicit definition of initial value. </summary>
        /// <param name="initialValue">Gives the initial value contained in the created AtomicValue object</param>
        public AtomicUInt64(System.UInt64 initialValue) { ai64 = new AtomicInt64(unchecked((Int64)initialValue)); }

        private AtomicInt64 ai64;

        /// <summary>Provide accessor to underlying value as a volatile value</summary>
        public System.UInt64 VolatileValue { get { return unchecked((UInt64)ai64.VolatileValue); } set { ai64.VolatileValue = unchecked((Int64)value); } }

        /// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
        public System.UInt64 Value { get { return unchecked((UInt64)ai64.Value); } set { ai64.Value = unchecked((Int64)value); } }

        /// <summary>Performs Interlocked.Increment on the contained value.</summary>
        public System.UInt64 Increment() { return unchecked((UInt64)ai64.Increment()); }
        /// <summary>Performs Interlocked.Increment on the contained value, once or twice, so as to produce a non-zero value</summary>
        public System.UInt64 IncrementSkipZero() { return unchecked((UInt64)ai64.IncrementSkipZero()); }
        /// <summary>Performs Interlocked.Decrement on the contained value.</summary>
        public System.UInt64 Decrement() { return unchecked((UInt64)ai64.Decrement()); }
        /// <summary>Performs Interlocked.Add on the contained value.</summary>
        public System.UInt64 Add(System.UInt64 value) { return unchecked((UInt64)ai64.Add(unchecked((Int64)value))); }
        /// <summary>Performs Interlocked.Exchange on the contained value.</summary>
        public System.UInt64 Exchange(System.UInt64 value) { return unchecked((UInt64)ai64.Exchange(unchecked((Int64)value))); }
        /// <summary>Performs Interlocked.CompareExchange on the contained value.</summary>
        public System.UInt64 CompareExchange(System.UInt64 value, System.UInt64 comparand) { return unchecked((UInt64)ai64.CompareExchange(unchecked((Int64)value), unchecked((Int64)comparand))); }

        /// <summary>Debugging helper</summary>
        public override string ToString()
        {
            return "Value:{0}".CheckedFormat(VolatileValue);
        }
    }

    // restore prior "warning CS0420: 'xxxx': a reference to a volatile field will not be treated as volatile" warning behavior
    #pragma warning restore 0420

	#endregion

    //-------------------------------------------------
	// <remarks>
	// The following GuardedObject and SequenceNumber related definitions form an essential component that is required to support the efficient 
	// poll version of event reaction and handling.  
	//
	// Sequence numbers by themselves are provided in simple form (with and without time stamps on last increment) and in an interlocked form.
	// The simple form is not thread reentrant and any user of the simple form must implement appropriate thread locking to prevent concurrent use
	// of the interface methods on two or more threads.  The interlocked form supports non-blocking and non-locking use in such multi-threaded
	// environments.
	// 
	// A Sequence number observer pattern is provided that is constructed from a ISequenceNumberValue provider and allows the caller to quickly
	// test if the provider has been Incremented recently and to Update the local copy when desired so as to reset such a change indicator.
	// 
	// The basic Sequence number classes are also aggregated into Sequenced Objects.  There are a number of these implementation classes that 
	// all focus on implementing on of the ISequencedRefObject or ISequencedValueObject interfaces.  These implementations allow the owner to
	// provide a generic interface from which clients can determine if a published value has been recently written and to obtain such a value 
	// in a thread safe manner.  The pattern of implementation objects include a version that uses Interlocked operations to update the value and
	// to increment the sequence number along with a larger set of implementations that are lock based.  
	// </remarks>

	//-------------------------------------------------
	#region IObjectSource, IVolatileObjectSource interfaces

	#region Object source interface

	/// <summary>
	/// Interface that provides a templatized Object property for obtaining an Object (or copy thereof) from some object container or source.  
	/// This interface is most frequently provided by an implementation object that provides thread safe access control (guarded), sequencing 
	/// and/or automatic notification.
	/// </summary>
	/// <typeparam name="ObjectType">The type of the Object property.</typeparam>
	public interface IObjectSource<ObjectType>
	{
        /// <summary>Property caller access to contained object.  Some implementation types may use locking so as to keep sequence number synchronized with actual object instance.</summary>
		ObjectType Object { get; }
	}

	/// <summary>
	/// Defines the provided by objects that give read access to some internal volatile ref object of the specified type.
	/// </summary>
	/// <typeparam name="ObjectType">The type of the VolatileObject property.</typeparam>
	public interface IVolatileObjectSource<ObjectType> where ObjectType : class
	{
        /// <summary>Provides caller with volatile access to the contained object.  Will allow caller to snapshot a specific object with minimal locking required to produce a valid result.</summary>
		ObjectType VolatileObject { get; }
	}

	#endregion

	#region SequenceNumber interfaces

	/// <summary>This generic interface defines the additional type specific property to access the sequence number value.</summary>
	/// <typeparam name="SeqNumberType">Defines the numeric type used by this sequence number's counter.</typeparam>
	public interface ISequenceNumberValue<SeqNumberType>
	{
        /// <summary>Returns true if the number has been incremented or has been explicitly constructed with a non-zero value</summary>
		bool HasBeenSet { get; }

        /// <summary>Returns the current sequence number.  May return zero if sequence number is set to skip zero and Increment is in progress on another thread.</summary>
        SeqNumberType SequenceNumber { get; }

        /// <summary>Returns the current sequence number read as a volatile (no locking) - May return zero if sequence number is set to skip zero and Increment is in progress on another thread</summary>
        SeqNumberType VolatileSequenceNumber { get; }
	}

	/// <summary> This interface defines the methods that are available for all sequence number objects and is used to generate the next value in the sequence </summary>
	/// <typeparam name="SeqNumberType">Defines the numeric type used by this sequence number's counter.</typeparam>
	public interface ISequenceNumberGenerator<SeqNumberType>
	{
        /// <summary>Advances the sequence number to the next value and returns it</summary>
        /// <remarks>SkipZero type SequenceNumberGenerators implement this by performing the increment and then doing it again if the result of the first was zero.</remarks>
		SeqNumberType Increment();
	}

	#endregion

	#region Sequenced object interfaces

    /// <summary>
    /// Combines the <see cref="MosaicLib.Utils.IObjectSource{ObjectType}"/> and <see cref="MosaicLib.Utils.ISequenceNumberValue{SeqNumberType}"/> 
    /// interfaces to represent a source of sequenced objects.
    /// </summary>
	/// <typeparam name="ObjectType">Defines the type of the sequenced object.</typeparam>
	/// <typeparam name="SeqNumberType">Defines the numeric type used by this sequence number's counter.</typeparam>
	public interface ISequencedObjectSource<ObjectType, SeqNumberType> : IObjectSource<ObjectType>, ISequenceNumberValue<SeqNumberType> { }

	#endregion

	#region SequencedObjectObserver and SequenceNumberObserver related interfaces

	/// <summary>
	/// This interface is the basic interface that is implemented by all of the sequenced source observer classes.  It provides the generic
	/// means for a client to know if the observer is up to date and to trigger the observer to update its copy of the source's value.
	/// </summary>
	public interface ISequencedSourceObserver
	{
        /// <summary>returns true when source's seq number does not match seq number during last update.  May be set to true to indicate that an update is needed.</summary>
		bool IsUpdateNeeded { get; set; }
        /// <summary>updates the local copy of the source's value(s), returns true if the update was needed.</summary>
		bool Update();

        /// <summary>Variant of ISequenceSourceObserver.Update suitable for use with call chaining.  Updates local copy of the source's value.</summary>
        ISequencedSourceObserver UpdateInline();
	}

	/// <summary>This interface gives the client access to the Object from the source that was obtained during the last Update call.</summary>
	public interface ISequencedObjectSourceObserver<ObjectType> 
        : IObjectSource<ObjectType>, ISequencedSourceObserver 
    {
        /// <summary>Variant of ISequenceSourceObserver.Update suitable for use with call chaining.  Updates local copy of the source's value.</summary>
        new ISequencedObjectSourceObserver<ObjectType> UpdateInline();
    }

	/// <summary>This interface combines the functionality of a ISequencedSourceObserver and an ISequenceNumberValue</summary>
	/// <typeparam name="SeqNumberType">Defines the type of the sequence number</typeparam>
	public interface ISequenceNumberObserver<SeqNumberType> 
        : ISequenceNumberValue<SeqNumberType>, ISequencedSourceObserver 
    { }

	/// <summary>This interface combines an ISequencedObjectSourceObserver and a ISequenceNumberValue</summary>
	/// <typeparam name="ObjectType">Defines the type of the observed object</typeparam>
	/// <typeparam name="SeqNumberType">Defines the type of the sequence number</typeparam>
	public interface ISequencedObjectSourceObserver<ObjectType, SeqNumberType> 
        : ISequencedObjectSourceObserver<ObjectType>, ISequenceNumberValue<SeqNumberType> 
    {
        /// <summary>Variant of ISequenceSourceObserver.Update suitable for use with call chaining.  Updates local copy of the source's value.</summary>
        new ISequencedObjectSourceObserver<ObjectType, SeqNumberType> UpdateInline();
    }

	#endregion

	#region Sequenced Ref and Value object observer interfaces

	/// <summary>Interface defines a ISequencedObjectSourceObserver for use with ref type objects</summary>
	/// <typeparam name="RefObjectType">Defines the type of the observed object.  Must be a ref type.</typeparam>
	/// <typeparam name="SeqNumberType">Defines the type of the sequence number.</typeparam>
	public interface ISequencedRefObjectSourceObserver<RefObjectType, SeqNumberType> : ISequencedObjectSourceObserver<RefObjectType, SeqNumberType>
		where RefObjectType : class
	{
        /// <summary>Variant of ISequenceSourceObserver.Update suitable for use with call chaining.  Updates local copy of the source's value.</summary>
        new ISequencedRefObjectSourceObserver<RefObjectType, SeqNumberType> UpdateInline();
    }

	/// <summary>Interface defines a ISequencedObjectSourceObserver for use with value type objects</summary>
	/// <typeparam name="ValueObjectType">Defines the type of the observed object.  Must be a value type.</typeparam>
	/// <typeparam name="SeqNumberType">Defines the type of the sequence number.</typeparam>
    public interface ISequencedValueObjectSourceObserver<ValueObjectType, SeqNumberType> : ISequencedObjectSourceObserver<ValueObjectType, SeqNumberType>
        where ValueObjectType : struct
	{
        /// <summary>Variant of ISequenceSourceObserver.Update suitable for use with call chaining.  Updates local copy of the source's value.</summary>
        new ISequencedValueObjectSourceObserver<ValueObjectType, SeqNumberType> UpdateInline();
    }

	#endregion

	#endregion

	//-------------------------------------------------
	#region Guarded object implementation

	/// <summary>Implements the IGuardedObjectSource.  Uses a volatile handle to implement object access safety/synchronization.</summary>
	/// <typeparam name="RefObjectType">The type of the guarded object.  Must be a ref type.</typeparam>
	public class VolatileRefObject<RefObjectType> : IObjectSource<RefObjectType>, IVolatileObjectSource<RefObjectType> where RefObjectType : class
	{
        /// <summary>Default Constructor.  By default the contained Object will be null.</summary>
		public VolatileRefObject() { }
        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.</summary>
		public VolatileRefObject(RefObjectType initialValue) { Object = initialValue; }

        /// <summary>Property caller access to contained object.  synonym for VolatileObject.</summary>
        public virtual RefObjectType Object { get { return volatileObjHandle; } set { volatileObjHandle = value; } }
        /// <summary>Provides caller with volatile access to the contained object.  Will allow caller to snapshot a specific object with minimal locking required to produce a valid object.</summary>
        public virtual RefObjectType VolatileObject { get { return volatileObjHandle; } }

        /// <summary>Protected native volatile handle that contains the object reference.</summary>
        /// <remarks>reference objects may be flagged as being volatile and as such they may be atomically updated/replaced without additional concern.  The CLR makes certain that this operation is safe and meaningful.</remarks>
        protected volatile RefObjectType volatileObjHandle = null;

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return "VolatileRefObject {0}".CheckedFormat(Object);
        }
	}

	/// <summary>Implements the IGuardedObjectSource.  Uses mutex to implement object access synchronization.</summary>
	/// <typeparam name="RefObjectType">The type of the guarded object.  Must be a ref type.</typeparam>
	public class GuardedRefObject<RefObjectType> : IObjectSource<RefObjectType>, IVolatileObjectSource<RefObjectType> where RefObjectType : class
	{
        /// <summary>Default Constructor.  By default the contained Object will be null.</summary>
        public GuardedRefObject() { }
        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.</summary>
        public GuardedRefObject(RefObjectType initialValue) { Object = initialValue; }

        /// <summary>Property caller access to contained object.  Uses locking to control access to internal object.</summary>
        public virtual RefObjectType Object { get { lock (mutex) { return volatileObjHandle; } } set { lock (mutex) { volatileObjHandle = value; } } }
        /// <summary>Provides caller with volatile access to the contained object.  Will allow caller to snapshot a specific object with minimal locking required to produce a valid object.</summary>
        public virtual RefObjectType VolatileObject { get { return volatileObjHandle; } }

        /// <summary>Protected mutex object which is locked to control set access to the contained volatileObjHandle.  For this object the mutex is not used for get access to the volatileObjHandle.</summary>
		protected readonly object mutex = new object();
        /// <summary>Protected native volatile handle that contains the object reference.</summary>
        /// <remarks>reference objects may be flagged as being volatile and as such they may be atomically updated/replaced without additional concern.  The CLR makes certain that this operation is safe and meaningful.</remarks>
		protected volatile RefObjectType volatileObjHandle = null;

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return "GuardedRefObject {0}".CheckedFormat(Object);
        }
    }

	/// <summary>Implements the IGuardedObjectSource.  Uses mutex to implement object access synchronization.</summary>
	/// <typeparam name="ValueObjectType">The type of the guarded object.  Must be a value type.</typeparam>
	public class GuardedValueObject<ValueObjectType> : IObjectSource<ValueObjectType> where ValueObjectType : struct
	{
        /// <summary>Explicit Constructor.  contained Object will be initialized to default(ObjectType).</summary>
		public GuardedValueObject() { }
        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.</summary>
        public GuardedValueObject(ValueObjectType initialValue) { Object = initialValue; }

        /// <summary>Property caller access to contained object.  Uses locking to control access to internal (value) object.</summary>
        public virtual ValueObjectType Object { get { lock (mutex) { return valueObjStorage; } } set { lock (mutex) { valueObjStorage = value; } } }

        /// <summary>Protected mutex object which is locked to control get and set access to the contained valueObjStorage.</summary>
        protected readonly object mutex = new object();
        /// <summary>Protected native storage for the value object type against which this class has been defined.</summary>
        /// <remarks>value objects are stored in this object and require use of the mutex for both access and modification.</remarks>
        protected ValueObjectType valueObjStorage = default(ValueObjectType);

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return "GuardedValueObject {0}".CheckedFormat(Object);
        }
    }

	#endregion

	//-------------------------------------------------
	#region SequenceNumber implementations

    /// <summary>
    /// Provides a common base class for all ISequenceNumberValue implementation types.  Based on one of the 4 IAtomicValue implementation types.
    /// </summary>
    /// <typeparam name="ValueType">Must be one of System.Int32, System.UInt32, System.Int64, System.UInt64</typeparam>
    /// <remarks>
    /// This provides atomic increment for all of the supported data types provided that the SkipZero flag is false.  
    /// If this flag is true then an observer might observer the zero value since it is accomplished by incrementing the variable twice
    /// </remarks>
    public class SequenceNumberBase<ValueType> 
        : ISequenceNumberValue<ValueType>, ISequenceNumberGenerator<ValueType>
        where ValueType : struct
    {
		/// <summary>Constructor: initialValue as given, has been set if initial value is non-zero, skips zero on increment only if skipZero is true </summary>
        public SequenceNumberBase(ValueType initialValue, bool skipZero, bool haveInitialValue) 
		{
            if (typeof(ValueType) == typeof(Int32))
                sequenceNumberGen = new AtomicInt32() as IAtomicValue<ValueType>;
            else if (typeof(ValueType) == typeof(UInt32))
                sequenceNumberGen = new AtomicUInt32() as IAtomicValue<ValueType>;
            else if (typeof(ValueType) == typeof(Int64))
                sequenceNumberGen = new AtomicInt64() as IAtomicValue<ValueType>;
            else if (typeof(ValueType) == typeof(UInt64))
                sequenceNumberGen = new AtomicUInt64() as IAtomicValue<ValueType>;

            if (sequenceNumberGen == null)
                Asserts.ThrowAfterFault(Utils.Fcns.CheckedFormat("SequenceNumberBase ValueType:{0} must be System.Int32, System.Int64, System.UInt32 or System.UInt64", typeof(ValueType)));

            SkipZero = skipZero;
            sequenceNumberGen.VolatileValue = initialValue;

            if (haveInitialValue)
                InnerSequenceNumberHasBeenSet();
		}

        /// <summary>
        /// get/set property which indicates if this sequence number generator will skip zero when incremented or if zero is a permitted value.  
        /// This property is only used when performing Increment.  It is not used when the sequence number is explicitly set by the caller.
        /// </summary>
        /// <remarks>
        /// This property is not generally intended to be changed while incrementing the sequence number and the implementation assumes that this property is not used concurrently with the Increment
        /// method.  This object will continue to function acceptably in this case but the actually sequence of sequence numbers would be indeterminate if this constraint is violated.
        /// </remarks>
		public virtual bool SkipZero { get { return skipZero; } set { skipZero = value; } }

        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public virtual bool HasBeenSet { get { return hasSequenceNumberBeenSet; } }
        /// <summary>get/set property that gives the caller interlocked access to the current value of the contained sequence number value.  Setter also flags that the sequence number has been set.</summary>
        public virtual ValueType SequenceNumber { get { return sequenceNumberGen.Value; } set { sequenceNumberGen.Value = value; InnerSequenceNumberHasBeenSet(); } }
        /// <summary>
        /// Gives the caller direct access to the sequence number storage without the use of interlocked instructions. 
        /// </summary>
        /// <remarks>
        /// When using 64 bit types on 32 bit processor configurations this value may not change in strictly atomic transitions.  
        /// Caller should be aware that a 64 bit volatile value in these cases may, temporarily, show only half of a changed value with the next half showing up in a later access to this property.
        /// This property is still useful in cases where 2 observed transitions are acceptable for a single interlocked change (such as an increment) and as such it is being retained despite this
        /// caveat.
        /// </remarks>
        public virtual ValueType VolatileSequenceNumber { get { return sequenceNumberGen.VolatileValue; } }
        /// <summary>Allows the caller to advance the contained sequence number to the next value.</summary>
        /// <returns>the value of the contained sequence number after being incremented.</returns>
        public virtual ValueType Increment() { return InnerIncrementNumber(); }

        /// <summary>Innermost method used to increment a sequence number.  Implements skip zero behavior.</summary>
        protected virtual ValueType InnerIncrementNumber()
		{
            ValueType temp;

            if (!skipZero)
                temp = sequenceNumberGen.Increment();
            else
                temp = sequenceNumberGen.IncrementSkipZero();

            InnerSequenceNumberHasBeenSet();

            return temp;
		}

        /// <summary>True if the contained value of the sequence number has been explicitly set.  False otherwise.</summary>
		protected virtual void InnerSequenceNumberHasBeenSet() { hasSequenceNumberBeenSet = true;  }

        /// <summary>Container for the chosen IAtomicValue type that is used here to contain and generate sequence number values.</summary>
		protected IAtomicValue<ValueType> sequenceNumberGen = null;
        /// <summary>boolean flag used to determine if the sequence number generator contains the constructor default value or if its value has been explicitly defined.</summary>
		private volatile bool hasSequenceNumberBeenSet = false;
        /// <summary>Internal storage field for the SkipZero property.</summary>
		private bool skipZero = false;
    }

    #region Int32 SequenceNumber implementations

    /// <summary> 
	/// This class provides an implementation of the ISequenceNumber for the System.Int32 data type.  
	/// By default this version skips the zero value.
    /// This version only support reentrant Increment if SkipZero is explicitly constructed or set to be false.
	/// </summary>
    public class SequenceNumberInt : SequenceNumberBase<System.Int32>
	{
		/// <summary>Constructor: initial value is zero, skips zero on increment, initial value has not been set</summary>
		public SequenceNumberInt() : base(0, true, false) {}
        /// <summary>Constructor: initialValue as given, skips zero on increment, initial value has been set</summary>
        public SequenceNumberInt(System.Int32 initialValue) : base(initialValue, true, true) { }
        /// <summary>Constructor: initialValue as given, skips zero on increment only if skipZero is true, initial value has been set</summary>
        public SequenceNumberInt(System.Int32 initialValue, bool skipZero) : base(initialValue, skipZero, true) { }
	}

	/// <summary> 
	/// This class provides an implementation of the ISequenceNumber for the System.Int32 data type and uses Interlocked.Increment.  
	/// This version supports reentrant use of Increment.  It does not support use of SkipZero.  
	/// This class can also be used as a INotifyable target.
	/// </summary>
    public class InterlockedSequenceNumberInt : SequenceNumberBase<System.Int32>, INotifyable
	{
        /// <summary>Constructor: initial value is zero, does not skip zero on increment, initial value has not been set</summary>
        public InterlockedSequenceNumberInt() : base(0, false, false) { }
        /// <summary>Constructor: initial value as given, does not skip zero on increment, initial value has been set</summary>
        public InterlockedSequenceNumberInt(int initialValue) : base(initialValue, false, true) { }

        /// <summary>replaces SequenceNumberBase{System.Int32}.SkipZero property implementation.  Any attempt to set this to true will throw an assert exception</summary>
        /// <exception cref="MosaicLib.Utils.AssertException">Thrown if property is set to true.</exception>
		private new bool SkipZero { get { return base.SkipZero; } set { Asserts.ThrowIfConditionIsNotTrue(value == false, "InterlockedSequenceNumberInt.SkipZero must be false"); base.SkipZero = false; } }

        /// <summary>Implemenation for INotifyable.Notify method.  Increments the contained sequence number value.</summary>
		public virtual void Notify() { InnerIncrementNumber(); }
    }

    #endregion

    #region Uint64 SequenceNumber implementation

    /// <summary> 
    /// This class provides an implementation of the ISequenceNumber for the System.UInt64 data type.  
    /// By default skipZero is false because 64 bit sequence numbers are assumed to be non-wrapping due to their size.
    /// This version only support reentrant Increment if SkipZero is explicitly constructed or set to be false.
    /// </summary>
    public class SequenceNumberUInt64 : SequenceNumberBase<System.UInt64>
    {
        /// <summary>Constructor: initial value is zero, does not skip zero on increment, initial value has not been set</summary>
        /// <remarks>By default this constructor does not set SkipZero flag because 64 bit sequence numbers are assumed to be non-wrapping due to their size.</remarks>
        public SequenceNumberUInt64() : base(0, false, false) { }
        /// <summary>Constructor: initialValue as given, does not skip zero on increment, initial value has been set</summary>
        /// <remarks>By default this constructor does not set SkipZero flag because 64 bit sequence numbers are assumed to be non-wrapping due to their size.</remarks>
        public SequenceNumberUInt64(System.UInt64 initialValue) : base(initialValue, false, true) { }
        /// <summary>Constructor: initialValue as given, skips zero on increment as requested, initial value has been set</summary>
        public SequenceNumberUInt64(System.UInt64 initialValue, bool skipZero) : base(initialValue, skipZero, true) { }
    }

	/// <summary> 
	/// This class provides an implementation of the ISequenceNumber for the System.UInt32 data type and uses Interlocked.Increment.  
	/// This version supports reentrant use of Increment.  It does not support use of SkipZero.  
	/// This class can also be used as a INotifyable target.
	/// </summary>
    public class InterlockedSequenceNumberUInt64 : SequenceNumberBase<System.UInt64>, INotifyable
    {
        /// <summary>Constructor: initial value is zero, does not skip zero on increment, initial value has not been set</summary>
	/// <summary> 
	/// This class provides an implementation of the ISequenceNumber for the System.UInt64 data type and uses Interlocked.Increment.  
	/// This version supports reentrant use of Increment.  It does not support use of SkipZero.  
	/// This class can also be used as a INotifyable target.
	/// </summary>
        public InterlockedSequenceNumberUInt64() : base(0, false, false) { }
        /// <summary>Constructor: initial value as given, does not skip zero on increment, initial value has been set</summary>
        public InterlockedSequenceNumberUInt64(System.UInt64 initialValue) : base(initialValue, false, true) { }

        /// <summary>replaces SequenceNumberBase{System.Int32}.SkipZero property implementation.  Any attempt to set this to true will throw an assert exception</summary>
        /// <exception cref="MosaicLib.Utils.AssertException">Thrown if property is set to true.</exception>
        private new bool SkipZero { get { return base.SkipZero; } set { Asserts.ThrowIfConditionIsNotTrue(value == false, "InterlockedSequenceNumberUInt64.SkipZero must be false"); base.SkipZero = false; } }

        /// <summary>Implemenation for INotifyable.Notify method.  Increments the contained sequence number value.</summary>
        public virtual void Notify() { InnerIncrementNumber(); }
    }

    #endregion

    #endregion

    //-------------------------------------------------
	#region SequenceNumber Observer implementation

	/// <summary>This is an implementation class for the ISequenceNumberObserver interface.</summary>
	/// <typeparam name="SeqNumberType">Defines the type of the observed sequence number.</typeparam>
	public struct SequenceNumberObserver<SeqNumberType> : ISequenceNumberObserver<SeqNumberType> where SeqNumberType : new()
	{
        /// <summary>
        /// Constructs an observer for the given <see cref="MosaicLib.Utils.ISequenceNumberValue{SeqNumberType}"/> source.
        /// </summary>
        /// <param name="sequenceNumberSource">Defines the <see cref="MosaicLib.Utils.ISequenceNumberValue{SeqNumberType}"/> object that this object will observe.</param>
		public SequenceNumberObserver(ISequenceNumberValue<SeqNumberType> sequenceNumberSource) 
		{ 
			this.sequenceNumberSource = sequenceNumberSource;
			copyOfLastValue = new SeqNumberType();
			hasBeenUpdated = false;
			Update(); 
		}

        /// <summary>Copy constructor that may be used to create a clonse of an existing SequenceNumberObserver.</summary>
        /// <param name="rhs">Defines the <see cref="MosaicLib.Utils.SequenceNumberObserver{SeqNumberType}"/> from which the copy is made.</param>
        public SequenceNumberObserver(SequenceNumberObserver<SeqNumberType> rhs)
        {
            sequenceNumberSource = rhs.sequenceNumberSource;
            copyOfLastValue = rhs.copyOfLastValue;
            hasBeenUpdated = rhs.hasBeenUpdated;
            Update();
        }

		private ISequenceNumberValue<SeqNumberType> sequenceNumberSource;
		private SeqNumberType copyOfLastValue;
		private bool hasBeenUpdated;

		#region ISequenceNumberObserver<SeqNumberType> Members

        /// <summary>returns true when source's seq number does not match seq number during last update.  May be set to true to indicate that an update is needed.</summary>
        public bool IsUpdateNeeded
		{
			get
			{
				if (!sequenceNumberSource.HasBeenSet)
					return false;

				if (!hasBeenUpdated)
					return true;

				// compare against the volatile value for testing if the update is needed (we might miss it and need to check later but this is much faster)
				if (copyOfLastValue.Equals(sequenceNumberSource.VolatileSequenceNumber))
					return false;

				return true;
			}
			set { if (value == true) hasBeenUpdated = false; }
		}

        /// <summary>updates the local copy of the source's value(s), returns true if the update was needed.</summary>
        public bool Update()
		{
			bool doUpdate = IsUpdateNeeded;
			if (doUpdate)
			{
				copyOfLastValue = sequenceNumberSource.SequenceNumber;		// make a copy from the synchronized version
				if (sequenceNumberSource.HasBeenSet)
					hasBeenUpdated = true;
			}

			return doUpdate;
		}

        /// <summary>Variant of ISequenceSourceObserver.Update suitable for use with call chaining.  Updates local copy of the source's value.</summary>
        public ISequenceNumberObserver<SeqNumberType> UpdateInline() { Update(); return this; }

        ISequencedSourceObserver ISequencedSourceObserver.UpdateInline() { return UpdateInline(); }

		#endregion

		#region ISequenceNumberValue<SeqNumberType> Members

        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public bool HasBeenSet { get { return hasBeenUpdated; } }
        /// <summary>Returns the current sequence number.  May return zero if sequence number is set to skip zero and Increment is in progress on another thread.</summary>
        public SeqNumberType SequenceNumber { get { return copyOfLastValue; } }
        /// <summary>Returns the current sequence number read as a volatile (no locking) - May return zero if sequence number is set to skip zero and Increment is in progress on another thread</summary>
        public SeqNumberType VolatileSequenceNumber { get { return copyOfLastValue; } }

		#endregion
	}

	#endregion

	//-------------------------------------------------
	#region Sequenced Ref and Value object implementations

    /// <summary>
    /// A variation of a <see cref="MosaicLib.Utils.GuardedRefObject{RefObjectType}"/> that can be used as an <see cref="MosaicLib.Utils.ISequencedObjectSource{RefObjectType, SeqNumberType}"/>
    /// </summary>
	/// <typeparam name="RefObjectType">Gives the type of the guarded object.  Must be a ref type.</typeparam>
    public class GuardedSequencedRefObject<RefObjectType> : GuardedRefObject<RefObjectType>, ISequencedObjectSource<RefObjectType, int> where RefObjectType : class
	{
        /// <summary>Default Constructor.  By default the contained Object will be null and sequence number will be in its initial, unset, state.</summary>
        public GuardedSequencedRefObject() { }
        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.  Sequence number will be incremented from initial state.</summary>
        public GuardedSequencedRefObject(RefObjectType initialValue) { Object = initialValue; }

        /// <summary>Overrides set method for Object property: uses locking to atomically update the stored object handle and to increment the contained sequence number.</summary>
        public override RefObjectType Object { set { lock (mutex) { volatileObjHandle = value; seqNum.Increment(); } } }

        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public virtual bool HasBeenSet { get { return seqNum.HasBeenSet; } }		// lock not needed for access to set once volatile boolean
        /// <summary>Gives the caller get access to the contained sequence number</summary>
		public virtual int SequenceNumber { get { lock (mutex) { return seqNum.VolatileSequenceNumber; } } }
        /// <summary>Gives the caller get access to the contained sequence number</summary>
        public virtual int VolatileSequenceNumber { get { return seqNum.VolatileSequenceNumber; } }
        /// <summary>Allows the caller to increment the contained sequence number</summary>
        /// <returns>the incremented value of the sequence number</returns>
        public virtual int Increment() { lock (mutex) { return seqNum.Increment(); } }

        /// <summary>Protected access to underlying sequence number generator used by this object</summary>
		protected SequenceNumberInt seqNum = new SequenceNumberInt();

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return "GuardedSequencedRefObject seqNum:{0} obj:{1}".CheckedFormat(VolatileSequenceNumber, Object);
        }
    }

    /// <summary>
    /// A variation of a <see cref="MosaicLib.Utils.VolatileRefObject{ValueObjectType}"/> that can be used as an <see cref="MosaicLib.Utils.ISequencedObjectSource{RefObjectType, SeqNumberType}"/>
    /// </summary>
    /// <typeparam name="RefObjectType">Gives the type of the access controlled object.  Must be a ref type.</typeparam>
    public class InterlockedSequencedRefObject<RefObjectType> : VolatileRefObject<RefObjectType>, ISequencedObjectSource<RefObjectType, int> where RefObjectType : class
	{
        /// <summary>Default Constructor.  By default the contained Object will be null and sequence number will be in its initial, unset, state.</summary>
        public InterlockedSequencedRefObject() { }
        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.  Sequence number will be incremented from initial state.</summary>
        public InterlockedSequencedRefObject(RefObjectType initialValue) { Object = initialValue; }

        /// <summary>Overrides set method for Object property: does not use locking.  Update the volatile object handle and then increments the contained sequence number.  This property setter may only be used by one thread at a time.  Unlike the base class getter this setter is not re-enterant.</summary>
        public override RefObjectType Object { set { volatileObjHandle = value; seqNum.Increment(); } }

        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public virtual bool HasBeenSet { get { return seqNum.HasBeenSet; } }		// lock not needed for access to set once volatile boolean
        /// <summary>Gives the caller get access to the contained sequence number using interlocked atomic access semantics</summary>
        public virtual int SequenceNumber { get { return seqNum.SequenceNumber; } }
        /// <summary>Gives the caller get access to the contained sequence number using volatile access semantics.</summary>
        public virtual int VolatileSequenceNumber { get { return seqNum.VolatileSequenceNumber; } }
        /// <summary>Allows the caller to increment the contained sequence number</summary>
        /// <returns>the incremented value of the sequence number</returns>
        public virtual int Increment() { return seqNum.Increment(); }

		private InterlockedSequenceNumberInt seqNum  = new InterlockedSequenceNumberInt();
    
        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return "InterlockedSequencedRefObject seqNum:{0} obj:{1}".CheckedFormat(VolatileSequenceNumber, Object);
        }
    }

	/// <summary>A variation of a GuardedValueObject that can be used as an ISequencedValueObjectSource</summary>
	/// <typeparam name="ValueObjectType">Gives the type of the guarded object.  Must be a value type.</typeparam>
	public class GuardedSequencedValueObject<ValueObjectType> : GuardedValueObject<ValueObjectType>, ISequencedObjectSource<ValueObjectType, int> where ValueObjectType : struct
	{
        /// <summary>Default Constructor.  By default the contained Object will be default(ObjectType) and sequence number will be in its initial, unset, state.</summary>
        public GuardedSequencedValueObject() { }
        /// <summary>Explicit Constructor.  Caller provides default initialValue for Object.  Sequence number will be incremented from initial state.</summary>
		public GuardedSequencedValueObject(ValueObjectType initialValue) { Object = initialValue; }

        /// <summary>Overrides set method for Object property: uses locking to atomically update the stored object value and to increment the contained sequence number.</summary>
        public override ValueObjectType Object { set { lock (mutex) { valueObjStorage = value; seqNum.Increment(); } } }

        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public virtual bool HasBeenSet { get { return seqNum.HasBeenSet; } }		// lock not needed for access to set once volatile boolean
        /// <summary>Gives the caller get access to the contained sequence number using interlocked atomic access semantics</summary>
        public virtual int SequenceNumber { get { lock (mutex) { return seqNum.VolatileSequenceNumber; } } }
        /// <summary>Gives the caller get access to the contained sequence number.</summary>
        public virtual int VolatileSequenceNumber { get { return seqNum.VolatileSequenceNumber; } }
        /// <summary>Allows the caller to increment the contained sequence number</summary>
        /// <returns>the incremented value of the sequence number</returns>
        public virtual int Increment() { lock (mutex) { return seqNum.Increment(); } }

        /// <summary>Protected access to underlying sequence number generator used by this object</summary>
        protected SequenceNumberInt seqNum = new SequenceNumberInt();

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return "GuardedSequencedValueObject seqNum:{0} obj:{1}".CheckedFormat(VolatileSequenceNumber, Object);
        }
    }

	#endregion

	//-------------------------------------------------
	#region Sequenced Ref and Value object Observer implementation classes (so that they can be used as a base class)

	/// <summary>Provides an implementation of the ISequencedRefObjectSourceObserver</summary>
	/// <typeparam name="RefObjectType">Gives the type of the observed object.  Must be a ref type.</typeparam>
	/// <typeparam name="SeqNumberType">Gives the type of the sequence number.</typeparam>
	public class SequencedRefObjectSourceObserver<RefObjectType, SeqNumberType> : ISequencedRefObjectSourceObserver<RefObjectType, SeqNumberType>
		where RefObjectType : class
		where SeqNumberType : new()
	{
		private ISequencedObjectSource<RefObjectType, SeqNumberType> objSource;
		private SequenceNumberObserver<SeqNumberType> seqNumObserver;
		private RefObjectType localObjCopy = null;

        /// <summary>Constructs a new instance to track the given object Source and then Updates the local copy from the source.</summary>
        /// <param name="objSource">Gives the <see cref="ISequencedObjectSource{RefObjectType, SeqNumberType}"/> instance which will be observed.</param>
		public SequencedRefObjectSourceObserver(ISequencedObjectSource<RefObjectType, SeqNumberType> objSource)
		{
			this.objSource = objSource;
			seqNumObserver = new SequenceNumberObserver<SeqNumberType>(objSource);
			IsUpdateNeeded = true;
			Update();
		}

        /// <summary>Constructs a clone of another instance of this type of Source Observer.</summary>
        /// <param name="rhs">Gives the <see cref="SequencedRefObjectSourceObserver{RefObjectType, SeqNumberType}"/> instance which will be cloned.</param>
        public SequencedRefObjectSourceObserver(SequencedRefObjectSourceObserver<RefObjectType, SeqNumberType> rhs)
        {
            objSource = rhs.objSource;
            seqNumObserver = new SequenceNumberObserver<SeqNumberType>(rhs.seqNumObserver);
            localObjCopy = rhs.localObjCopy;
        }

		#region ISequencedRefObjectSourceObserver<ObjectType, SeqNumberType> Members

        /// <summary>Getter property to return the locally cached Object handle value.</summary>
		public RefObjectType Object { get { return localObjCopy; } }

        /// <summary>returns true when source's seq number does not match seq number during last update.  May be set to true to indicate that an update is needed.</summary>
        public bool IsUpdateNeeded { get { return seqNumObserver.IsUpdateNeeded; } set { seqNumObserver.IsUpdateNeeded = value; } }
        /// <summary>updates the local copy of the source's value(s), returns true if the update was needed.</summary>
        public bool Update() { if (seqNumObserver.Update()) { localObjCopy = objSource.Object; return true; } else return false; }
        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public bool HasBeenSet { get { return seqNumObserver.HasBeenSet; } }
        /// <summary>Returns the current sequence number.  May return zero if sequence number is set to skip zero and Increment is in progress on another thread.</summary>
        public SeqNumberType SequenceNumber { get { return seqNumObserver.SequenceNumber; } }
        /// <summary>Returns the current sequence number read as a volatile (no locking) - May return zero if sequence number is set to skip zero and Increment is in progress on another thread</summary>
        public SeqNumberType VolatileSequenceNumber { get { return seqNumObserver.VolatileSequenceNumber; } }

        /// <summary>Variant of ISequenceSourceObserver.Update suitable for use with call chaining.  Updates local copy of the source's value.</summary>
        public ISequencedRefObjectSourceObserver<RefObjectType, SeqNumberType> UpdateInline() { Update(); return this; }

        ISequencedObjectSourceObserver<RefObjectType, SeqNumberType> ISequencedObjectSourceObserver<RefObjectType, SeqNumberType>.UpdateInline() { return UpdateInline(); }
        ISequencedObjectSourceObserver<RefObjectType> ISequencedObjectSourceObserver<RefObjectType>.UpdateInline() { return UpdateInline(); }
        ISequencedSourceObserver ISequencedSourceObserver.UpdateInline() { return UpdateInline(); }

		#endregion
    
        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("SequencedRefObjectSourceObserver seqNum:{0}".CheckedFormat(VolatileSequenceNumber));

            if (IsUpdateNeeded)
                sb.Append(" UpdateNeeded");

            if (HasBeenSet)
                sb.CheckedAppendFormat(" obj:{0}", Object);
            else
                sb.Append(" HasNotBeenSet");

            return sb.ToString();
        }
    }

	/// <summary>Provides an implementation of the ISequencedValueObjectSourceObserver</summary>
	/// <typeparam name="ValueObjectType">Gives the type of the observed object.  Must be a value type.</typeparam>
	/// <typeparam name="SeqNumberType">Gives the type of the sequence number.</typeparam>
	public class SequencedValueObjectSourceObserver<ValueObjectType, SeqNumberType> : ISequencedValueObjectSourceObserver<ValueObjectType, SeqNumberType>
		where ValueObjectType : struct
		where SeqNumberType : new()
	{
		private ISequencedObjectSource<ValueObjectType, SeqNumberType> objSource;
		private SequenceNumberObserver<SeqNumberType> seqNumObserver;
		private ValueObjectType localObjCopy;

        /// <summary>Constructs a new instance to track the given object Source and then Updates the local copy from the source.</summary>
        /// <param name="objSource">Gives the <see cref="ISequencedObjectSource{ValueObjectType, SeqNumberType}"/> instance which will be observed.</param>
        public SequencedValueObjectSourceObserver(ISequencedObjectSource<ValueObjectType, SeqNumberType> objSource)
		{
			this.objSource = objSource;
			seqNumObserver = new SequenceNumberObserver<SeqNumberType>(objSource);
			IsUpdateNeeded = true;
			Update();
		}

        /// <summary>Constructs a clone of another instance of this type of Source Observer.</summary>
        /// <param name="rhs">Gives the <see cref="SequencedValueObjectSourceObserver{RefObjectType, SeqNumberType}"/> instance which will be cloned.</param>
        public SequencedValueObjectSourceObserver(SequencedValueObjectSourceObserver<ValueObjectType, SeqNumberType> rhs)
        {
            objSource = rhs.objSource;
            seqNumObserver = new SequenceNumberObserver<SeqNumberType>(rhs.seqNumObserver);
            localObjCopy = rhs.localObjCopy;
        }

		#region ISequencedValueObjectSourceObserver<ObjectType, SeqNumberType> Members

        /// <summary>Getter property to return the locally cached Object value.</summary>
        public ValueObjectType Object { get { return localObjCopy; } }

        /// <summary>returns true when source's seq number does not match seq number during last update.  May be set to true to indicate that an update is needed.</summary>
        public bool IsUpdateNeeded { get { return seqNumObserver.IsUpdateNeeded; } set { seqNumObserver.IsUpdateNeeded = value; } }
        /// <summary>updates the local copy of the source's value(s), returns true if the update was needed.</summary>
        public bool Update() { if (seqNumObserver.Update()) { localObjCopy = objSource.Object; return true; } else return false; }
        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public bool HasBeenSet { get { return seqNumObserver.HasBeenSet; } }
        /// <summary>Returns the current sequence number.  May return zero if sequence number is set to skip zero and Increment is in progress on another thread.</summary>
        public SeqNumberType SequenceNumber { get { return seqNumObserver.SequenceNumber; } }
        /// <summary>Returns the current sequence number read as a volatile (no locking) - May return zero if sequence number is set to skip zero and Increment is in progress on another thread</summary>
        public SeqNumberType VolatileSequenceNumber { get { return seqNumObserver.VolatileSequenceNumber; } }

        /// <summary>Variant of ISequenceSourceObserver.Update suitable for use with call chaining.  Updates local copy of the source's value.</summary>
        public ISequencedValueObjectSourceObserver<ValueObjectType, SeqNumberType> UpdateInline() { Update(); return this; }

        ISequencedObjectSourceObserver<ValueObjectType, SeqNumberType> ISequencedObjectSourceObserver<ValueObjectType, SeqNumberType>.UpdateInline() { return UpdateInline(); }
        ISequencedObjectSourceObserver<ValueObjectType> ISequencedObjectSourceObserver<ValueObjectType>.UpdateInline() { return UpdateInline(); }
        ISequencedSourceObserver ISequencedSourceObserver.UpdateInline() { return UpdateInline(); }

		#endregion

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("SequencedValueObjectSourceObserver seqNum:{0}".CheckedFormat(VolatileSequenceNumber));

            if (IsUpdateNeeded)
                sb.Append(" UpdateNeeded");

            if (HasBeenSet)
                sb.CheckedAppendFormat(" obj:{0}", Object);
            else
                sb.Append(" HasNotBeenSet");

            return sb.ToString();
        }
    }

	#endregion

    //-------------------------------------------------
    #region DataContract serialization related interfaces

    /// <summary>
    /// This interface encapsulates a small set of standard use patterns for transcribing between DataContract objects 
    /// and the corresponding serialized string and file formats.  Objects that implement this interface may be used to
    /// serialize to and from the TObjectType
    /// </summary>
    /// <typeparam name="TObjectType">
    /// The templatized object type on which this adapter is defined.  
    /// Must be a DataContract or compatible type in order to be usable with adapters of this type.
    /// </typeparam>
    public interface IDataContractAdapter<TObjectType>
    {
        /// <summary>
        /// Attempts to use an underlying DataContractSerializer to read the deserialize the corresponding object from the given stream using its ReadObject method.  
        /// Returns the object if the read was successful.
        /// </summary>
        /// <exception cref="System.Runtime.Serialization.SerializationException">May be thrown by the underlying Data contract serialization object's ReadObject method that is used here.</exception>
        TObjectType ReadObject(System.IO.Stream readStream);

        /// <summary>
        /// Attempts to use the contained DataContractSerializer to read the deserialize the corresponding object from the given TextReader.
        /// </summary>
        /// <param name="tr">Acts as the source of the text from which the DataContractSerializer is to deserialize the object.</param>
        /// <exception cref="System.Runtime.Serialization.SerializationException">May be thrown by the underlying Data contract serialization object's ReadObject method that is used here.</exception>
        TObjectType ReadObject(System.IO.TextReader tr);

        /// <summary>
        /// Attempts to use an underlying DataContractSerializer to read the deserialize the corresponding object from the given string.
        /// </summary>
        /// <param name="s">Contains the text from which the DataContractSerializer is to deserialize the object.</param>
        /// <exception cref="System.Runtime.Serialization.SerializationException">May be thrown by the underlying Data contract serialization object's ReadObject method that is used here.</exception>
        TObjectType ReadObject(String s);

        /// <summary>
        /// Attempts to open the given path as an serialized text file and read/deserialize the given object from it using an underlying DataContractSerializer
        /// If the underlying File.Open or ReadObject method call fails, this method will either return the given defaultValue (of rethrow is false) or will rethrow the original exception.
        /// </summary>
        /// <exception cref="System.Runtime.Serialization.SerializationException">May be thrown by the underlying Data contract serialization object's ReadObject method that is used here.</exception>
        TObjectType ReadFromFile(String path, TObjectType defaultValue, bool rethrow);

        /// <summary>
        /// Serializes the given object by calling WriteObject on the contained DataContractSerializer and passing it the given writeStream.
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <param name="writeStream">Gives the stream on which the serialized data is to be written.</param>
        /// <exception cref="System.Runtime.Serialization.InvalidDataContractException">The type being serialized does not conform to data contract rules. For example, the System.Runtime.Serialization.DataContractAttribute attribute has not been applied to the type.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">There is a problem with the instance being written</exception>
        /// <exception cref="System.ServiceModel.QuotaExceededException">The maximum number of objects to serialize has been exceeded. Check the System.Runtime.Serialization.DataContractSerializer.MaxItemsInObjectGraph property.</exception>
        void WriteObject(TObjectType obj, System.IO.Stream writeStream);

        /// <summary>
        /// Attempts to open the given path as a text file and write/serialize the given object into the file using an underlying DataContractSerializer.
        /// If the underlying File.Open or DataContractSerializer.WiteObject call fails, this method will either return (of rethrow is false) or will rethrow the original exception.
        /// </summary>
        /// <exception cref="System.Runtime.Serialization.InvalidDataContractException">The type being serialized does not conform to data contract rules. For example, the System.Runtime.Serialization.DataContractAttribute attribute has not been applied to the type.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">There is a problem with the instance being written</exception>
        /// <exception cref="System.ServiceModel.QuotaExceededException">The maximum number of objects to serialize has been exceeded. Check the System.Runtime.Serialization.DataContractSerializer.MaxItemsInObjectGraph property.</exception>
        void WriteToFile(TObjectType obj, String path, bool rethrow);

        /// <summary>
        /// Serializes the given object by calling WriteObject on the underlying DataContractSerializer to serialize and write the object into a String
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <returns>A string containing the serialized representation of the given object as serialized by the contained DataContractSerializer</returns>
        /// <exception cref="System.Runtime.Serialization.InvalidDataContractException">The type being serialized does not conform to data contract rules. For example, the System.Runtime.Serialization.DataContractAttribute attribute has not been applied to the type.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">There is a problem with the instance being written</exception>
        /// <exception cref="System.ServiceModel.QuotaExceededException">The maximum number of objects to serialize has been exceeded. Check the System.Runtime.Serialization.DataContractSerializer.MaxItemsInObjectGraph property.</exception>
        string ConvertObjectToString(TObjectType obj);
    }

    /// <summary>
    /// Base class typically used when implementing the IDataContractAdapter interface.  
    /// Provides implementations for helper methods that implemented purely based on use of other interface methods.
    /// </summary>
    /// <typeparam name="TObjectType">
    /// The templatized object type on which this adapter is defined.  
    /// Must be a DataContract or compatible object type in order to be usable with adapters of this type.
    /// </typeparam>
    public abstract class DataContractAdapterBase<TObjectType> 
        : DisposableBase
        , IDataContractAdapter<TObjectType>
    {
        /// <summary>
        /// Gets or sets the contained System.Text.Encoding property to determine what character encoding will be used during appropriate serialization and deserialization operations.
        /// </summary>
        /// <returns>The text encoding to use. The default is Encoding.ASCII</returns>
        public virtual System.Text.Encoding Encoding { get { return _encoding; } set { _encoding = value; } }
        private System.Text.Encoding _encoding = System.Text.Encoding.ASCII;

        /// <summary>
        /// Attempts to use an underlying DataContractSerializer to read the deserialize the corresponding object from the given stream using its ReadObject method.  
        /// Returns the object if the read was successful.
        /// </summary>
        public abstract TObjectType ReadObject(Stream readStream);

        /// <summary>
        /// When overriden in a derived class, this method attempts to use the contained DataContractSerializer to read the deserialize the corresponding object from the given TextReader.
        /// <para/>The default implementation provided here will throw a System.NotImplementedException on any attempted use.
        /// </summary>
        /// <param name="tr">Acts as the source of the text from which the DataContractSerializer is to deserialize the object.</param>
        /// <exception cref="System.NotImplementedException">The default implementation provided here will throw a System.NotImplementedException on any attempted use.</exception>
        public virtual TObjectType ReadObject(System.IO.TextReader tr)
        {
            throw new System.NotImplementedException("ReadObject(TextReader) is not supported for the {0} class".CheckedFormat(Fcns.CurrentClassName));
        }

        /// <summary>
        /// Attempts to use an underlying DataContractSerializer to read the deserialize the corresponding object from the given string.
        /// </summary>
        /// <param name="s">Contains the text from which the DataContractSerializer is to deserialize the object.</param>
        public virtual TObjectType ReadObject(string s)
        {
            using (System.IO.StringReader sr = new StringReader(s))
                return ReadObject(sr);
        }

        /// <summary>
        /// Attempts to open the given path as an serialized text file and read/deserialize the given object from it using an underlying DataContractSerializer
        /// If the underlying File.Open or ReadObject method call fails, this method will either return the given defaultValue (of rethrow is false) or will rethrow the original exception.
        /// </summary>
        public TObjectType ReadFromFile(String path, TObjectType defaultValue, bool rethrow)
        {
            try
            {
                using (System.IO.FileStream fileStream = System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    return ReadObject(fileStream);
                }
            }
            catch
            {
                if (rethrow)
                    throw;

                return defaultValue;
            }
        }

        /// <summary>
        /// Serializes the given object by calling WriteObject on the contained DataContractSerializer and passing it the given writeStream.
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <param name="writeStream">Gives the stream on which the serialized data is to be written.</param>
        /// <exception cref="System.Runtime.Serialization.InvalidDataContractException">The type being serialized does not conform to data contract rules. For example, the System.Runtime.Serialization.DataContractAttribute attribute has not been applied to the type.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">There is a problem with the instance being written</exception>
        /// <exception cref="System.ServiceModel.QuotaExceededException">The maximum number of objects to serialize has been exceeded. Check the System.Runtime.Serialization.DataContractSerializer.MaxItemsInObjectGraph property.</exception>
        public abstract void WriteObject(TObjectType obj, Stream writeStream);

        /// <summary>
        /// Attempts to open the given path as a text file and write/serialize the given object into the file using an underlying DataContractSerializer.
        /// If the underlying File.Open or DataContractSerializer.WiteObject call fails, this method will either return (of rethrow is false) or will rethrow the original exception.
        /// </summary>
        public void WriteToFile(TObjectType obj, String path, bool rethrow)
        {
            try
            {
                using (System.IO.FileStream fileStream = System.IO.File.Open(path, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write, System.IO.FileShare.None))
                {
                    WriteObject(obj, fileStream);
                }
            }
            catch
            {
                if (rethrow)
                    throw;
            }
        }

        /// <summary>
        /// Serializes the given object by calling WriteObject on the underlying DataContractSerializer to serialize and write the object into a String
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <returns>A string containing the serialized representation of the given object as serialized by the contained DataContractSerializer</returns>
        public abstract string ConvertObjectToString(TObjectType obj);
    }

    #endregion

    #region DataContractObject to/from xml string or byte stream helper

    /// <summary>
    /// This adapter class encapsulates a small set of standard use patterns for transcribing between DataContract objects 
    /// and the corresponding ASCII Xml strings and files.  This object contains and makes use of a DataContractSerializer to
    /// implement that actual Serialization and Deserialization behavior.
    /// </summary>
    /// <typeparam name="TObjectType">
    /// The templatized object type on which this adapter is defined.  
    /// Must be a DataContract or compatible class in order to be usable by this adapter.
    /// </typeparam>
    public class DataContractAsciiXmlAdapter<TObjectType>
        : DataContractXmlAdapter<TObjectType>
        where TObjectType : class
    {
        /// <summary>
        /// Default constructor.
        /// Sets writer ConformanceLevel to Document, OmitXmlDeclaration to false, Encoding to ASCII, Indent to true.
        /// Sets reader ConformanceLevel to Auto, and CloseInput to false, VerifyObjectName = true
        /// </summary>
        public DataContractAsciiXmlAdapter() : base(System.Text.Encoding.ASCII) {}
    }

    /// <summary>
    /// This adapter class encapsulates a small set of standard use patterns for transcribing between DataContract objects 
    /// and the corresponding Xml strings and files.  This object contains and makes use of a DataContractSerializer to
    /// implement that actual Serialization and Deserialization behavior.
    /// </summary>
    /// <typeparam name="TObjectType">
    /// The templatized object type on which this adapter is defined.  
    /// Must be a DataContract or compatible object type in order to be usable by this adapter.
    /// </typeparam>
    public class DataContractXmlAdapter<TObjectType>
        : DataContractAdapterBase<TObjectType>
    {
        /// <summary>
        /// Base constructor.  
        /// Sets writer ConformanceLevel to Document, OmitXmlDeclaration to false, Indent to true.
        /// Sets reader ConformanceLevel to Auto, and CloseInput to false, VerifyObjectName = true
        /// </summary>
        /// <param name="encoding">Defines the XmlWritterSettings.Encoding that will be used.</param>
        public DataContractXmlAdapter(System.Text.Encoding encoding)
        {
            base.Encoding = encoding;

            xws = new System.Xml.XmlWriterSettings()
            {
                ConformanceLevel = System.Xml.ConformanceLevel.Document,
                OmitXmlDeclaration = false,
                Encoding = encoding,
                Indent = true,
                CloseOutput = false,    // this class will explicitly close the underlying stream
            };

            xrs = new System.Xml.XmlReaderSettings()
            {
                ConformanceLevel = System.Xml.ConformanceLevel.Auto,
                CloseInput = false,     // this class will explicitly close any underlying stream
            };

            VerifyObjectName = true;
        }

        #region XmlWritterSettings setup accessor properties

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.CheckCharacters propertyL: a value indicating whether to do character checking.
        /// </summary>
        /// <returns>true to do character checking; otherwise false. The default is true.</returns>
        public bool CheckCharacters { get { return xws.CheckCharacters; } set { xws.CheckCharacters = value; } }

        /// <summary>
        /// Set to true to configure the XmlWritterSettings to omit the xml declaration and to use the Fragment ConformanceLevel.  
        /// Set to false to use normal Document format and to include the XmlDeclaration.
        /// </summary>
        /// <returns>true if XmlWritterSettings is set to generate xml fragments or false if the XmlWritterSettings are set to generate documents.  default is false.</returns>
        public bool GenerateFragment 
        {
            get { return (xws.ConformanceLevel == System.Xml.ConformanceLevel.Fragment); } 
            set 
            { 
                xws.ConformanceLevel = (value ? System.Xml.ConformanceLevel.Fragment : System.Xml.ConformanceLevel.Document);
                xws.OmitXmlDeclaration = value;
            }
        }

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.ConformanceLevel property: the level of conformance which the System.Xml.XmlWriter complies with.
        /// </summary>
        /// <returns>One of the System.Xml.ConformanceLevel values. The default is ConformanceLevel.Document.</returns>
        public System.Xml.ConformanceLevel ConformanceLevel { get { return xws.ConformanceLevel; } set { xws.ConformanceLevel = value; } }

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.Encoding property to determine what character encoding will be used.
        /// </summary>
        /// <returns>The text encoding to use. The default is Encoding.ASCII</returns>
        public override System.Text.Encoding Encoding { get { return xws.Encoding; } set { xws.Encoding = base.Encoding = value; } }

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.Indent property to determine if generated Xml text will be wrapped and indented.
        /// </summary>
        /// <returns>true to write individual elements on new lines and indent; otherwise false.  The default is true.</returns>
        public bool Indent { get { return xws.Indent; } set { xws.Indent = value; } }

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.IndentChars property: 
        /// the character string to use when indenting. This setting is
        ///     used when the System.Xml.XmlWriterSettings.Indent property is set to true.
        /// </summary>
        /// <returns>
        /// The character string to use when indenting. This can be set to any string
        ///     value. However, to ensure valid XML, you should specify only valid white
        ///     space characters, such as space characters, tabs, carriage returns, or line
        ///     feeds. The default is two spaces.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The value assigned to the System.Xml.XmlWriterSettings.IndentChars is null.</exception>
        public string IndentChars { get { return xws.IndentChars; } set { xws.IndentChars = value; } }

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.NewLineChars property: the character string to use for line breaks.
        /// </summary>
        /// <returns>
        /// The character string to use for line breaks. This can be set to any string
        /// value. However, to ensure valid XML, you should specify only valid white
        /// space characters, such as space characters, tabs, carriage returns, or line
        /// feeds. The default is \r\n (carriage return, new line).
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The value assigned to the System.Xml.XmlWriterSettings.NewLineChars is null.</exception>
        public string NewLineChars { get { return xws.NewLineChars; } set { xws.NewLineChars = value; } }

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.NewLineHandling property: 
        /// a value indicating whether to normalize line breaks in the output.
        /// </summary>
        /// <returns>
        /// One of the System.Xml.NewLineHandling values. The default is System.Xml.NewLineHandling.Replace.
        /// </returns>
        public System.Xml.NewLineHandling NewLineHandling { get { return xws.NewLineHandling; } set { xws.NewLineHandling = value; } }

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.NewLineOnAttributes property: 
        /// a value indicating whether to write attributes on a new line.
        /// </summary>
        /// <returns>
        /// true to write attributes on individual lines; otherwise false. The default
        /// is false.  Note: This setting has no effect when the System.Xml.XmlWriterSettings.Indent
        /// property value is false.  When System.Xml.XmlWriterSettings.NewLineOnAttributes
        /// is set to true, each attribute is pre-pended with a new line and one extra
        /// level of indentation.
        /// </returns>
        public bool NewLineOnAttributes { get { return xws.NewLineOnAttributes; } set { xws.NewLineOnAttributes = value; } }

        /// <summary>
        /// Gets or sets the contained XmlWritterSettings.OmitXmlDeclaration property: 
        /// a value indicating whether to write an XML declaration.
        /// </summary>
        /// <returns>
        /// true to omit the XML declaration; otherwise false. The default is false.
        /// </returns>
        public bool OmitXmlDeclaration { get { return xws.OmitXmlDeclaration; } set { xws.OmitXmlDeclaration = value; } }

        #endregion

        /// <summary>Gives the value of the VerifyObjectName parameter that will be passed to the DataContractSerializer's verifyObjectName parameter for ReadObject calls.</summary>
        public bool VerifyObjectName { get; set; }

        /// <summary>Settings used when serializing an object to Xml</summary>
        protected System.Xml.XmlWriterSettings xws;

        /// <summary>Settings used when deserializing an object from Xml</summary>
        protected System.Xml.XmlReaderSettings xrs;

        /// <summary>The DataContractSerializer instance that is used by this adapter.</summary>
        DataContractSerializer dcs = new DataContractSerializer(typeof(TObjectType));

        /// <summary>
        /// Attempts to use the contained DataContractSerializer to read the deserialize the corresponding object from the given stream using its ReadObject method.  
        /// Returns the object if the read was successful.
        /// </summary>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The VerifyObjectName Property is set to true, and the element name and namespace do not correspond to the values set in the constructor.</exception>
        public override TObjectType ReadObject(System.IO.Stream readStream)
        {
            using (System.IO.TextReader tr = new System.IO.StreamReader(readStream, Encoding))
                return ReadObject(tr);
        }

        /// <summary>
        /// Attempts to use the contained DataContractSerializer to read the deserialize the corresponding object from the given TextReader.
        /// </summary>
        /// <param name="tr">Acts as the source for the text from which the DataContractSerializer is to deserialize the object.</param>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The VerifyObjectName Property is set to true, and the element name and namespace do not correspond to the values set in the constructor.</exception>
        public override TObjectType ReadObject(System.IO.TextReader tr)
        {
            using (System.Xml.XmlReader xr = System.Xml.XmlReader.Create(tr, xrs))
            {
                return (TObjectType)dcs.ReadObject(xr, VerifyObjectName);
            }
        }

        /// <summary>
        /// Serializes the given object by calling WriteObject on the contained DataContractSerializer and passing it the given writeStream and the contains of the XmlWriterSettings.
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <param name="writeStream">Gives the stream on which the serialized data is to be written.</param>
        /// <exception cref="System.Runtime.Serialization.InvalidDataContractException">The type being serialized does not conform to data contract rules. For example, the System.Runtime.Serialization.DataContractAttribute attribute has not been applied to the type.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">There is a problem with the instance being written</exception>
        /// <exception cref="System.ServiceModel.QuotaExceededException">The maximum number of objects to serialize has been exceeded. Check the System.Runtime.Serialization.DataContractSerializer.MaxItemsInObjectGraph property.</exception>
        public override void WriteObject(TObjectType obj, System.IO.Stream writeStream)
        {
            using (System.Xml.XmlWriter xw = System.Xml.XmlWriter.Create(writeStream, xws))
            {
                dcs.WriteObject(xw, obj);
                xw.Flush();
            }
        }

        /// <summary>A string builder object that is constructed and (re)used by ConvertObjectToString.</summary>
        System.Text.StringBuilder sb = null;

        /// <summary>
        /// Serializes the given object by calling WriteObject on the contained DataContractSerializer to serialize and write the object into a String
        /// using a reusable StringBuilder and a temporary XmlWriter.
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <returns>A string containing the ASCII Xml representation of the given object as serialized by the contained DataContractSerializer</returns>
        public override string ConvertObjectToString(TObjectType obj)
        {
            if (sb == null)
                sb = new System.Text.StringBuilder();
            else
                sb.Clear();

            using (System.Xml.XmlWriter xw = System.Xml.XmlWriter.Create(sb, xws))
            {
                dcs.WriteObject(xw, obj);
                xw.Flush();
            }

            return sb.ToString();
        }
    }

    #endregion

    #region DataContract Json Adapter

    /// <summary>
    /// This adapter class encapsulates a small set of standard use patterns for transcribing between DataContract objects 
    /// and the corresponding JSON format strings and files.  This object contains and makes use of a DataContractJsonSerializer to
    /// implement that actual Serialization and Deserialization behavior.
    /// </summary>
    /// <typeparam name="TObjectType">
    /// The templatized object type on which this adapter is defined.  
    /// Must be a DataContract or compatible object type in order to be usable by this adapter.
    /// </typeparam>
    public class DataContractJsonAdapter<TObjectType>
        : DataContractAdapterBase<TObjectType>
    {
        /// <summary>The DataContractJsonSerializer instance that is used by this adapter.</summary>
        DataContractJsonSerializer dcjs = new DataContractJsonSerializer(typeof(TObjectType));

        /// <summary>
        /// Attempts to use the contained DataContractJsonSerializer to read the deserialize the corresponding object from the given stream using its ReadObject method.  
        /// Returns the object if the read was successful.
        /// </summary>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The VerifyObjectName Property is set to true, and the element name and namespace do not correspond to the values set in the constructor.</exception>
        public override TObjectType ReadObject(System.IO.Stream readStream)
        {
            return (TObjectType) dcjs.ReadObject(readStream);
        }

        /// <summary>
        /// Attempts to use the contained DataContractJsonSerializer to read the deserialize the corresponding object from the given string.
        /// </summary>
        /// <param name="s">Contains the text from which the DataContractJsonSerializer is to deserialize the object.</param>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The VerifyObjectName Property is set to true, and the element name and namespace do not correspond to the values set in the constructor.</exception>
        public override TObjectType ReadObject(String s)
        {
            byte [] byteArray = System.Text.Encoding.ASCII.GetBytes(s);
            using (MemoryStream byteArrayStreamReader = new MemoryStream(byteArray, false))
            {
                return (TObjectType) dcjs.ReadObject(byteArrayStreamReader);
            }
        }

        /// <summary>
        /// Serializes the given object by calling WriteObject on the contained DataContractJsonSerializer and passing it the given writeStream.
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <param name="writeStream">Gives the stream on which the serialized data is to be written.</param>
        /// <exception cref="System.Runtime.Serialization.InvalidDataContractException">The type being serialized does not conform to data contract rules. For example, the System.Runtime.Serialization.DataContractAttribute attribute has not been applied to the type.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">There is a problem with the instance being written</exception>
        /// <exception cref="System.ServiceModel.QuotaExceededException">The maximum number of objects to serialize has been exceeded. Check the System.Runtime.Serialization.DataContractSerializer.MaxItemsInObjectGraph property.</exception>
        public override void WriteObject(TObjectType obj, System.IO.Stream writeStream)
        {
            dcjs.WriteObject(writeStream, obj);
        }

        /// <summary>
        /// Serializes the given object by calling WriteObject on the contained DataContractJsonSerializer to serialize and write the object into a String
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <returns>A string containing the serialized representation of the given object as serialized by the contained DataContractJsonSerializer</returns>
        public override string ConvertObjectToString(TObjectType obj)
        {
            if (writeMemoryStream == null)
            {
                writeMemoryStream = new MemoryStream();
                AddExplicitDisposeAction(() => Fcns.DisposeOfObject(ref writeMemoryStream));
            }

            try 
            {
                dcjs.WriteObject(writeMemoryStream, obj);
                writeMemoryStream.Flush();

                byte[] buffer = writeMemoryStream.GetBuffer();
                string result = System.Text.Encoding.ASCII.GetString(buffer, 0, unchecked((int) writeMemoryStream.Length));

                writeMemoryStream.Position = 0;
                writeMemoryStream.SetLength(0);

                return result;
            }
            catch
            {
                Fcns.DisposeOfObject(ref writeMemoryStream);
                throw;
            }
        }

        private MemoryStream writeMemoryStream = null;
    }

    #endregion

    //-------------------------------------------------

    #region ScopedLock

    /// <summary>
    /// This class is intened to allow more fine grain control of the use of a mutex object than the native lock keyword and/or basic Monitor methods directly support.
    /// This object is generally expected to be used in the context of a using statement (along with its implicit finally calling this object's Dispose method).
    /// This object additional supports the concept of Locking (and Releasing) the null objects which converts this object's behavior into a thread synchroniziation no-op.
    /// As such this object can be used to implement a stanardized Lock/Release pattern even in cases where the underlying object may, or may not, actually be using a mutex object.
    /// </summary>
    public class ScopedLock : IDisposable
    {
        /// <summary>
        /// Default constructor: Supports optional locking.  
        /// If the given <paramref name="mutexObject"/> is non-null and if <paramref name="acquireLock"/> is true then this constructor will Lock the given <paramref name="mutexObject"/>.
        /// Dispose method will release the held mutexObject if one is held at that time.
        /// </summary>
        public ScopedLock(object mutexObject = null, bool acquireLock = true)
        {
            if (acquireLock && mutexObject != null)
                Lock(mutexObject);
        }

        /// <summary>Calls Release in order to unlock any currently held lock.</summary>
        public void Dispose()
        {
            Release();
        }

        /// <summary>field records the object (if any) that has been Locked (Entered) so that it may be Released(Exited) later.</summary>
        private object lockedMutexObject = null;

        /// <summary>Returns true if this object is currently holding a locked mutex object (and thus can be Released)</summary>
        public bool HasLock { get { return (lockedMutexObject != null); } }

        /// <summary>
        /// This method is used to, optionally (if the given mutexObject is non-null), lock the given mutexObject by calling Monitor.Enter on it and then saving it to be the internally held locked mutexObject.
        /// <para/>This method always calls Release inorder to Release any previoulsy held locked mutexObject before attempting to lock the given one.
        /// </summary>
        public void Lock(object mutexObject)
        {
            Release();

            if (mutexObject != null)
            {
                System.Threading.Monitor.Enter(mutexObject);
                lockedMutexObject = mutexObject;
            }
        }

        /// <summary>If the object currently HasLock on a previously locked mutexObject then this method will Exit the monitor on it and clear the HasLock indication.</summary>
        public void Release()
        {
            if (HasLock)
            {
                System.Threading.Monitor.Exit(lockedMutexObject);
                lockedMutexObject = null;
            }
        }
    }

    #endregion
}

//-------------------------------------------------
