//-------------------------------------------------------------------
/*! @file E005Data.cs
    @brief This files defines small set of classes and interfaces that are used with Semi standard data objects under the E005 standard (SECS-II)
   
    Copyright (c) Mosaic Systems Inc.,  All rights reserved
    Copyright (c) 2008 Mosaic Systems Inc.,  All rights reserved

	Licensed under the Apache License, Version 2.0 (the "License");
	you may not use this file except in compliance with the License.
	You may obtain a copy of the License at

	     http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software
	distributed under the License is distributed on an "AS IS" BASIS,
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	See the License for the specific language governing permissions and
	limitations under the License.
 */
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Modular.Common;

namespace MosaicLib.Semi.E005.Data
{
    ///-------------------------------------------------------------------
    ///<remarks>
    /// This section describes the basic patterns that are used with E005 data.  
    ///
    /// E005 data is generally interpreted in two ways: either as a raw byte sequence or as a typed value object tree.  
    /// In printable form, the value object tree representation is often refered to as SML representation.
    /// 
    /// The code provided here supports byte sequence representation using simple byte arrays and supports object tree represenation using
    /// Modular.Common.ValueContainer objects and tree's thereof.  This code will also provide means to convert between E005 IFC (ItemFormatCode) values
    /// and the corresponding ValueContainer ContainerStorageType values.
    ///
    /// At present all of the logic that is to be used to build and interpret E005 data objects has been refocused on the ValueContainer representation thereof
    /// as this representation is more generally useful (and more generally used) throughout this library.
    /// 
    /// Also please note that the native byte array represenation of an E005 data object is not immutable.  
    /// Client code must remember that when such byte arrays are shared between class instances and/or between threads, the client code is explicitly responsible for all
    /// ownership and write permission rules.  As such the code here is generally intended for use in the last layer between business logic based on other more suitable
    /// constructs (including the selected use of ValueContainer and INamedValueSet objects) and serialized transmission and/or storage logic where the actual
    /// binary byte array representation is required.
    ///</remarks>
    ///-------------------------------------------------------------------

    #region ItemFormatCode and related extension methods

    /// <summary>
    /// Define the set of E005 known format codes per E005 Table 1.  
    /// Additional codes are added as internal placeholders with special (non-data) meaning 
    /// <para/>L (000), Bi (010), Bo (011), A (020), J (021), W (022), I8 (030), I1 (031), I2 (032), I4 (034), F8 (040), F4 (044), U8 (050), U1 (051), U2 (052), U4 (054),
    /// None (-1 - empty byte array)
    /// </summary>
    public enum ItemFormatCode
    {
        // List, Binary, Boolean, ASCII, JIS8, WSTR, 
        /// <summary>List = octal 00</summary>
        L = 0x00,
        /// <summary>Binary = octal 10</summary>
        Bi = 0x08,
        /// <summary>Boolean = octal 11</summary>
        Bo = 0x09,
        /// <summary>Ascii = octal 20</summary>
        A = 0x10,
        /// <summary>JIS8 = octal 21 - partial support.  supported as if it was ascii for byte to ValueContainer conversion</summary>
        J = 0x11,
        /// <summary>W = octal 22 - partial support.</summary>
        W = 0x12,
        /// <summary>Int64 = octal 30</summary>
        I8 = 0x18,
        /// <summary>Int8 = octal 31</summary>
        I1 = 0x19,
        /// <summary>Int16 = octal 32</summary>
        I2 = 0x1a,
        /// <summary>Int32 = octal 34</summary>
        I4 = 0x1c,
        /// <summary>Double = octal 40</summary>
        F8 = 0x20,
        /// <summary>Single = octal 44</summary>
        F4 = 0x24,
        /// <summary>UInt64 = octal 50</summary>
        U8 = 0x28,
        /// <summary>UInt8 = octal 51</summary>
        U1 = 0x29,
        /// <summary>UInt16 = octal 52</summary>
        U2 = 0x2a,
        /// <summary>UInt32 = octal 54</summary>
        U4 = 0x2c,

        /// <summary>None = -1.  not part of E005.  serialized as empty byte array (zero length array)</summary>
        None = -1,
        /// <summary>Invalid = -2.  not part of E005.  not serializable</summary>
        Invalid = -2,
        /// <summary>Null = -3.  not part of E005.  not serializable</summary>
        Null = -3,
    }

    public static partial class ExtensionMethods
    {
        #region predicate helpers

        /// <summary>
        /// Returns true if the given ifc is equal to ItemFormatCode.L
        /// </summary>
        public static bool IsList(this ItemFormatCode ifc)
        {
            return (ifc == ItemFormatCode.L);
        }

        #endregion

        #region Type conversion methods

        /// <summary>
        /// Returns the ContainerStorageType that corresponds to the given ifc ItemFormatCode value, or ContainerStorageType.None if the value of ifc is not recognized
        /// Returns true if the given ifc is recognized and supported or false otherwise.
        /// </summary>
        public static ContainerStorageType ConvertToContainerStorageType(this ItemFormatCode ifc)
        {
            ContainerStorageType cst;
            ifc.ConvertToContainerStorageType(out cst);
            return cst;
        }

        /// <summary>
        /// Produces the ContainerStorageType that corresponds to the given ifc ItemFormatCode value.
        /// Returns true if the given ifc is recognized and supported or false otherwise.
        /// </summary>
        public static bool ConvertToContainerStorageType(this ItemFormatCode ifc, out ContainerStorageType cst)
        {
            switch (ifc)
            {
                case ItemFormatCode.A: cst = ContainerStorageType.String; return true;
                case ItemFormatCode.W: cst = ContainerStorageType.String; return true;
                case ItemFormatCode.Bo: cst = ContainerStorageType.Boolean; return true;
                case ItemFormatCode.Bi: cst = ContainerStorageType.Binary; return true;
                case ItemFormatCode.L: cst = ContainerStorageType.Object; return true;
                case ItemFormatCode.I1: cst = ContainerStorageType.SByte; return true;
                case ItemFormatCode.I2: cst = ContainerStorageType.Int16; return true;
                case ItemFormatCode.I4: cst = ContainerStorageType.Int32; return true;
                case ItemFormatCode.I8: cst = ContainerStorageType.Int64; return true;
                case ItemFormatCode.U1: cst = ContainerStorageType.Byte; return true;
                case ItemFormatCode.U2: cst = ContainerStorageType.UInt16; return true;
                case ItemFormatCode.U4: cst = ContainerStorageType.UInt32; return true;
                case ItemFormatCode.U8: cst = ContainerStorageType.UInt64; return true;
                case ItemFormatCode.F4: cst = ContainerStorageType.Single; return true;
                case ItemFormatCode.F8: cst = ContainerStorageType.Double; return true;
                case ItemFormatCode.None: cst = ContainerStorageType.None; return true;
                default: cst = ContainerStorageType.None; return false;
            }
        }

        /// <summary>
        /// Produces the ItemFormatCode that corresponds to the given cst ContainerStorateType value, or ItemFormatCode.None if the value of cst is not recognized
        /// Returns true if the given ifc is recognized and supported or false otherwise.
        /// </summary>
        public static ItemFormatCode ConvertToItemFormatCode(this ContainerStorageType cst)
        {
            ItemFormatCode ifc;
            cst.ConvertToItemFormatCode(out ifc);
            return ifc;
        }

        /// <summary>
        /// Produces the ItemFormatCode that corresponds to the given cst ContainerStorateType value.
        /// Returns true if the given cst is recognized and supported or false otherwise.
        /// </summary>
        public static bool ConvertToItemFormatCode(this ContainerStorageType cst, out ItemFormatCode ifc)
        {
            switch (cst)
            {
                case ContainerStorageType.String: ifc = ItemFormatCode.A; return true;      // this can actually be either A or W
                case ContainerStorageType.Boolean: ifc = ItemFormatCode.Bo; return true;
                case ContainerStorageType.Binary: ifc = ItemFormatCode.Bi; return true;
                case ContainerStorageType.Object: ifc = ItemFormatCode.L; return true;
                case ContainerStorageType.SByte: ifc = ItemFormatCode.I1; return true;
                case ContainerStorageType.Int16: ifc = ItemFormatCode.I2; return true;
                case ContainerStorageType.Int32: ifc = ItemFormatCode.I4; return true;
                case ContainerStorageType.Int64: ifc = ItemFormatCode.I8; return true;
                case ContainerStorageType.Byte: ifc = ItemFormatCode.U1; return true;
                case ContainerStorageType.UInt16: ifc = ItemFormatCode.U2; return true;
                case ContainerStorageType.UInt32: ifc = ItemFormatCode.U4; return true;
                case ContainerStorageType.UInt64: ifc = ItemFormatCode.U8; return true;
                case ContainerStorageType.Single: ifc = ItemFormatCode.F4; return true;
                case ContainerStorageType.Double: ifc = ItemFormatCode.F8; return true;
                case ContainerStorageType.None: ifc = ItemFormatCode.None; return true;
                default: ifc = ItemFormatCode.None; return false;
            }
        }

        #endregion
    }

    #endregion

    #region E005 Data conversion to/from and related extension methods.

    /// <summary>
    /// Extension methods that are used to help in interactions between E005 Data objects, ValueContainers, NamedValueSets and NamedValues.
    /// </summary>
    public static partial class ExtensionMethods
    {
        #region ValueContainer ConvertFrom/To methods

        public static byte[] ConvertToE005Data(this ValueContainer vc, bool throwOnException)
        {
            try
            {
                List<byte> byteArrayBuilder = new List<byte>();

                byteArrayBuilder.AppendWithIH(vc);

                return (byteArrayBuilder.ToArray());
            }
            catch (System.Exception ex)
            {
                if (throwOnException)
                {
                    string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
                    throw new ConvertValueException("{0} failed on '{1}'".CheckedFormat(methodName, vc), ex);
                }

                return new byte[0];
            }
        }

        public static ValueContainer ConvertFromE005Data(this ValueContainer vc, byte[] byteArray, bool throwOnException)
        {
            try
            {
                vc = DecodeE005Data(byteArray);

                return vc;
            }
            catch (System.Exception ex)
            {
                if (throwOnException)
                {
                    string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
                    throw new ConvertValueException("{0} failed on '{1}'".CheckedFormat(methodName, vc), ex);
                }

                return ValueContainer.Empty;
            }
        }

        #endregion

        #region NamedValueSet ConvertFrom/To methods

        public static byte[] ConvertToE005Data(this INamedValueSet nvs, bool throwOnException)
        {
            try
            {
                List<byte> byteArrayBuilder = new List<byte>();

                byteArrayBuilder.AppendWithIH(nvs);

                return (byteArrayBuilder.ToArray());
            }
            catch (System.Exception ex)
            {
                if (throwOnException)
                {
                    string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
                    throw new ConvertValueException("{0} failed on '{1}'".CheckedFormat(methodName, nvs), ex);
                }

                return emptyByteArray;
            }
        }

        public static readonly byte[] emptyByteArray = new byte[0];

        public static NamedValueSet ConvertFromE005Data(this NamedValueSet nvs, byte[] byteArray, bool throwOnException)
        {
            System.Exception exToThrow = null;

            try
            {
                int startIndex = 0;
                string ec = string.Empty;

                nvs =  nvs.ConvertFromE005Data(byteArray, ref startIndex, ref ec);

                if (!ec.IsNullOrEmpty())
                {
                    string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
                    string byteArrayInHex = MosaicLib.Utils.ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray);
                    exToThrow = new ConvertValueException("{0} failed on '{1}': {2}".CheckedFormat(methodName, byteArrayInHex, ec), null);
                }
            }
            catch (System.Exception ex)
            {
                if (throwOnException)
                {
                    string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
                    string byteArrayInHex = MosaicLib.Utils.ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray);
                    exToThrow = new ConvertValueException("{0} failed on '{1}'".CheckedFormat(methodName, byteArrayInHex), ex);
                }
            }

            if (exToThrow != null && throwOnException)
                throw exToThrow;

            return nvs;
        }

        public static NamedValueSet ConvertFromE005Data(this NamedValueSet nvs, byte[] byteArray, ref int startIndex, ref string ec)
        {
            nvs = (nvs ?? new NamedValueSet()).ConvertToWriteable();

            {
                int nvsListNumElements;

                ItemFormatCode nvsIFC = DecodeE005ItemHeader(byteArray, ref startIndex, ref ec, out nvsListNumElements);

                if (!nvsIFC.IsList() && ec.IsNullOrEmpty())
                    ec = "nvs root IFC.{0} is not a list".CheckedFormat(nvsIFC);

                for (int listIndex = 0; listIndex < nvsListNumElements && ec.IsNullOrEmpty(); listIndex++)
                {
                    int nvListNumElements = 0;
                    ItemFormatCode nvIFC = DecodeE005ItemHeader(byteArray, ref startIndex, ref ec, out nvListNumElements);

                    if (!nvIFC.IsList() || nvListNumElements != 2)
                        ec = "sub-list {0} [IFC.{1}/{2}] must be a 2 element list".CheckedFormat(listIndex, nvIFC, nvListNumElements);

                    ValueContainer vcName = ValueContainer.Empty, vcValue = ValueContainer.Empty;
                    string name = string.Empty;

                    if (ec.IsNullOrEmpty())
                    {
                        vcName = DecodeE005Data(byteArray, ref startIndex, ref ec);

                        name = vcName.GetValue<string>(false);
                    }

                    if (ec.IsNullOrEmpty() && name.IsNullOrEmpty())
                        ec = "in sub-list {0} could not obtain non-empty value name from {1}".CheckedFormat(listIndex, vcName);

                    if (ec.IsNullOrEmpty())
                        vcValue = DecodeE005Data(byteArray, ref startIndex, ref ec);

                    if (ec.IsNullOrEmpty())
                        nvs.SetValue(name, vcValue);
                }
            }

            return nvs;
        }

        #endregion

        #region Append support methods (AppendWithIH variants, AppendIH, AppendContentBytes, AppendRaw variants)

        public static void AppendWithIH(this List<byte> byteArrayBuilder, ValueContainer vc)
        {
            // first take care of known value types and object types
            switch (vc.cvt)
            {
                case ContainerStorageType.Boolean: byteArrayBuilder.AppendIH(ItemFormatCode.Bo, 1); byteArrayBuilder.AppendRaw(vc.u.b); return;
                case ContainerStorageType.Binary: byteArrayBuilder.AppendIH(ItemFormatCode.Bi, 1); byteArrayBuilder.AppendRaw(vc.u.bi); return;
                case ContainerStorageType.SByte: byteArrayBuilder.AppendIH(ItemFormatCode.I1, 1); byteArrayBuilder.AppendRaw(vc.u.i8); return;
                case ContainerStorageType.Int16: byteArrayBuilder.AppendIH(ItemFormatCode.I2, 2); byteArrayBuilder.AppendRaw(vc.u.i16); return;
                case ContainerStorageType.Int32: byteArrayBuilder.AppendIH(ItemFormatCode.I4, 4); byteArrayBuilder.AppendRaw(vc.u.i32); return;
                case ContainerStorageType.Int64: byteArrayBuilder.AppendIH(ItemFormatCode.I8, 8); byteArrayBuilder.AppendRaw(vc.u.i64); return;
                case ContainerStorageType.Byte: byteArrayBuilder.AppendIH(ItemFormatCode.U1, 1); byteArrayBuilder.AppendRaw(vc.u.u8); return;
                case ContainerStorageType.UInt16: byteArrayBuilder.AppendIH(ItemFormatCode.U2, 2); byteArrayBuilder.AppendRaw(vc.u.u16); return;
                case ContainerStorageType.UInt32: byteArrayBuilder.AppendIH(ItemFormatCode.U4, 4); byteArrayBuilder.AppendRaw(vc.u.u32); return;
                case ContainerStorageType.UInt64: byteArrayBuilder.AppendIH(ItemFormatCode.U8, 8); byteArrayBuilder.AppendRaw(vc.u.u64); return;
                case ContainerStorageType.Single: byteArrayBuilder.AppendIH(ItemFormatCode.F4, 4); byteArrayBuilder.AppendRaw(vc.u.f32); return;
                case ContainerStorageType.Double: byteArrayBuilder.AppendIH(ItemFormatCode.F8, 8); byteArrayBuilder.AppendRaw(vc.u.f64); return;
                case ContainerStorageType.String: byteArrayBuilder.AppendWithIH(vc.o as string); return;
                case ContainerStorageType.IListOfString: byteArrayBuilder.AppendWithIH(vc.GetValue<string[]>(true)); return;
                case ContainerStorageType.IListOfVC: byteArrayBuilder.AppendWithIH(vc.GetValue<IList<ValueContainer>>(true)); return;
                case ContainerStorageType.Object:
                    if (vc.o != null)
                    {
                        if (vc.o is INamedValue)
                        {
                            byteArrayBuilder.AppendWithIH((INamedValue)vc.o);
                            return;
                        }
                        else if (vc.o is INamedValueSet)
                        {
                            byteArrayBuilder.AppendWithIH((INamedValueSet)vc.o);
                            return;
                        }
                        else if (vc.o is ValueContainer[])
                        {
                            byteArrayBuilder.AppendWithIH(new List<ValueContainer>((ValueContainer[])vc.o));
                            return;
                        }
                    }

                    break;
                default:
                    break;
            }

            byteArrayBuilder.AppendWithIH(vc.ToStringSML());     // fallback is always to handle it like a string
        }

        public static void AppendWithIH(this List<byte> byteArrayBuilder, INamedValueSet nvs)
        {
            byteArrayBuilder.AppendIH(ItemFormatCode.L, nvs.Count);

            foreach (INamedValue nv in nvs)
            {
                byteArrayBuilder.AppendWithIH(nv);
            }
        }

        public static void AppendWithIH(this List<byte> byteArrayBuilder, INamedValue nv)
        {
            byteArrayBuilder.AppendIH(ItemFormatCode.L, 2);

            byteArrayBuilder.AppendWithIH(nv.Name);
            byteArrayBuilder.AppendWithIH(nv.VC);
        }

        public static void AppendWithIH(this List<byte> byteArrayBuilder, String s)
        {
            int sLen = s.MapNullToEmpty().Length;

            if (s.IsByteSerializable())
            {
                byteArrayBuilder.AppendIH(ItemFormatCode.A, sLen);
                byteArrayBuilder.AddRange(ByteArrayTranscoders.ByteStringTranscoder.Decode(s));
            }
            else
            {
                byteArrayBuilder.AppendIH(ItemFormatCode.W, 2 + sLen * 2);
                byteArrayBuilder.AddRange(new byte [] { 0x00, 0x01 });      // ISO 10646 UCS-2 (Unicode 2 - appears to be the same as native dotNet strings)
                foreach (char c in s)
                {
                    byteArrayBuilder.Add(unchecked((byte) (c >> 8)));
                    byteArrayBuilder.Add(unchecked((byte) (c >> 0)));
                }
            }
        }

        public static void AppendWithIH(this List<byte> byteArrayBuilder, string [] stringArray)
        {
            stringArray = stringArray ?? emptyStringArray;

            byteArrayBuilder.AppendIH(ItemFormatCode.L, stringArray.Length);

            foreach (string s in stringArray)
                byteArrayBuilder.AppendWithIH(s);
        }

        public static readonly string [] emptyStringArray = new string[0];

        public static void AppendWithIH(this List<byte> byteArrayBuilder, IList<ValueContainer> vcList)
        {
            vcList = vcList ?? emptyVCList;

            byteArrayBuilder.AppendIH(ItemFormatCode.L, vcList.Count);

            int count = vcList.Count;
            for (int idx = 0; idx < count; idx++)
                byteArrayBuilder.AppendWithIH(vcList[idx]);
        }

        public static readonly IList<ValueContainer> emptyVCList = new List<ValueContainer>().AsReadOnly();

        internal static void AppendWithIH<TItemType>(this List<byte> byteArrayBuilder, ItemFormatCode ifc, int itemSizeInBytes, TItemType[] itemArray) where TItemType : struct
        {
            int itemArrayLength = ((itemArray != null) ? itemArray.Length : 0);

            byteArrayBuilder.AppendIH(ifc, itemArrayLength * itemSizeInBytes);

            for (int idx = 0; idx < itemArrayLength; idx++)
            {
                ValueContainer vc = new ValueContainer(itemArray[idx]);
                byteArrayBuilder.AppendContentBytes(vc);
            }
        }

        internal static void AppendIH(this List<byte> byteArrayBuilder, ItemFormatCode ifc, int numItems)
        {
            byte umsb, ulsb, lmsb, llsb;
            Utils.Data.Unpack(unchecked((UInt32) Math.Max(0, numItems)), out umsb, out ulsb, out lmsb, out llsb);

            byte headerByte = unchecked((byte)(((int) ifc) << 2));

            // headers do not support 4 byte count values - truncate the count to 3 bytes...

            if (ulsb != 0)
            {
                headerByte |= 0x03;
                byteArrayBuilder.AddRange(new byte [] { headerByte, ulsb, lmsb, llsb });
            }
            else if (lmsb != 0)
            {
                headerByte |= 0x02;
                byteArrayBuilder.AddRange(new byte [] { headerByte, lmsb, llsb });
            }
            else if (llsb != 0 || ifc != ItemFormatCode.Invalid)
            {
                headerByte |= 0x01;
                byteArrayBuilder.AddRange(new byte [] { headerByte, llsb });
            }
            else
            {
                // this is defined in E005 as an invalid pattern - the bottom 2 bits of the header byte should not be zero.
                byteArrayBuilder.AddRange(new byte [] { headerByte });
            }
        }

        public static void AppendContentBytes(this List<byte> byteArrayBuilder, ValueContainer vc)
        {
            switch (vc.cvt)
            {
                case ContainerStorageType.Boolean: byteArrayBuilder.AppendRaw(vc.u.b); return;
                case ContainerStorageType.Binary: byteArrayBuilder.AppendRaw(vc.u.bi); return;
                case ContainerStorageType.SByte: byteArrayBuilder.AppendRaw(vc.u.i8); return;
                case ContainerStorageType.Int16: byteArrayBuilder.AppendRaw(vc.u.i16); return;
                case ContainerStorageType.Int32: byteArrayBuilder.AppendRaw(vc.u.i32); return;
                case ContainerStorageType.Int64: byteArrayBuilder.AppendRaw(vc.u.i64); return;
                case ContainerStorageType.Byte: byteArrayBuilder.AppendRaw(vc.u.u8); return;
                case ContainerStorageType.UInt16: byteArrayBuilder.AppendRaw(vc.u.u16); return;
                case ContainerStorageType.UInt32: byteArrayBuilder.AppendRaw(vc.u.u32); return;
                case ContainerStorageType.UInt64: byteArrayBuilder.AppendRaw(vc.u.u64); return;
                case ContainerStorageType.Single: byteArrayBuilder.AppendRaw(vc.u.f32); return;
                case ContainerStorageType.Double: byteArrayBuilder.AppendRaw(vc.u.f64); return;
                default:
                    {
                        string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
                        throw new ConvertValueException("{0} cannot be used directly with {1}".CheckedFormat(methodName, vc), null);
                    }
            }
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, bool bo)
        {
            byteArrayBuilder.Add(unchecked((byte)(bo ? 1 : 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, sbyte i8)
        {
            byteArrayBuilder.Add(unchecked((byte)(i8 >> 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, short i16)
        {
            byteArrayBuilder.Add(unchecked((byte)(i16 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(i16 >> 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, int i32)
        {
            byteArrayBuilder.Add(unchecked((byte)(i32 >> 24)));
            byteArrayBuilder.Add(unchecked((byte)(i32 >> 16)));
            byteArrayBuilder.Add(unchecked((byte)(i32 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(i32 >> 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, long i64)
        {
            byteArrayBuilder.Add(unchecked((byte)(i64 >> 56)));
            byteArrayBuilder.Add(unchecked((byte)(i64 >> 48)));
            byteArrayBuilder.Add(unchecked((byte)(i64 >> 40)));
            byteArrayBuilder.Add(unchecked((byte)(i64 >> 32)));
            byteArrayBuilder.Add(unchecked((byte)(i64 >> 24)));
            byteArrayBuilder.Add(unchecked((byte)(i64 >> 16)));
            byteArrayBuilder.Add(unchecked((byte)(i64 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(i64 >> 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, byte u8)
        {
            byteArrayBuilder.Add(unchecked((byte)(u8 >> 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, ushort u16)
        {
            byteArrayBuilder.Add(unchecked((byte)(u16 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(u16 >> 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, uint u32)
        {
            byteArrayBuilder.Add(unchecked((byte)(u32 >> 24)));
            byteArrayBuilder.Add(unchecked((byte)(u32 >> 16)));
            byteArrayBuilder.Add(unchecked((byte)(u32 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(u32 >> 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, ulong u64)
        {
            byteArrayBuilder.Add(unchecked((byte)(u64 >> 56)));
            byteArrayBuilder.Add(unchecked((byte)(u64 >> 48)));
            byteArrayBuilder.Add(unchecked((byte)(u64 >> 40)));
            byteArrayBuilder.Add(unchecked((byte)(u64 >> 32)));
            byteArrayBuilder.Add(unchecked((byte)(u64 >> 24)));
            byteArrayBuilder.Add(unchecked((byte)(u64 >> 16)));
            byteArrayBuilder.Add(unchecked((byte)(u64 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(u64 >> 0)));
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, float f32)
        {
            ValueContainer.Union u = new ValueContainer.Union() { f32 = f32 };
            byteArrayBuilder.AppendRaw(u.u32);
        }

        public static void AppendRaw(this List<byte> byteArrayBuilder, double f64)
        {
            ValueContainer.Union u = new ValueContainer.Union() { f64 = f64 };
            byteArrayBuilder.AppendRaw(u.u64);
        }

        #endregion

        #region Normalize

        /// <summary>
        /// This method accepts a given ValueContainer (lhsVC) and if it is an IListOfVC that only contains String VCs 
        /// then it convers the given IListOfVC vc to an IListOfString vc containing the strings from the IListOfVC items and returns it.
        /// If convertEmptyList is passed as true and the contained IListOfVC is null or empty then it will be converted to an emtpy IListOfString VC.
        /// If deep is passed as true then the method will be applied recursively on any ValueContainer object tree.
        /// </summary>
        public static ValueContainer Normalize(this ValueContainer lhsVC, bool convertEmptyList, bool deep)
        {
            if (lhsVC.cvt == ContainerStorageType.IListOfVC)
            {
                IList<ValueContainer> vcList = lhsVC.o as IList<ValueContainer> ?? emptyIListOfVC;

                if ((vcList.Count != 0 || convertEmptyList) && vcList.All(vc => vc.cvt == ContainerStorageType.String))
                    return new ValueContainer(new List<string>(vcList.Select(vc => (vc.o as String))).AsReadOnly() as IList<string>);
                else if (deep)
                    return new ValueContainer(new List<ValueContainer>(vcList.Select(vc => vc.Normalize(convertEmptyList, true))).AsReadOnly() as IList<ValueContainer>);
            }

            return lhsVC;
        }
        
        #endregion

        #region DecodeE005Data (byte array) related methods [inclues Normalize]

        public static ValueContainer DecodeE005Data(this byte[] byteArray)
        {
            int startIndex = 0;
            string ec = null;

            ValueContainer vc = DecodeE005Data(byteArray, ref startIndex, ref ec);

            if (ec == null && startIndex != byteArray.Length)
                ec = "not all bytes were consumed ({0} != {1})".CheckedFormat(startIndex, byteArray.Length);

            if (!ec.IsNullOrEmpty())
            {
                string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
                string byteArrayInHex = MosaicLib.Utils.ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray);

                throw new ConvertValueException("{0} failed on '{1}': {2}".CheckedFormat(methodName, byteArrayInHex, ec), null);
            }

            return vc;
        }

        public static ItemFormatCode DecodeE005ItemHeader(this byte[] byteArray, ref int startIndex, ref string ec, out int ibNumElements)
        {
            int ibNumBytes, ibIndexOffsetShift;
            return DecodeE005ItemHeader(byteArray, ref startIndex, ref ec, out ibNumBytes, out ibNumElements, out ibIndexOffsetShift);
        }

        public static ItemFormatCode DecodeE005ItemHeader(this byte[] byteArray, ref int startIndex, ref string ec, out int ibNumBytes, out int ibNumElements, out int ibIndexOffsetShift)
        {
            int byteArrayLength = byteArray.Length;

            ibNumBytes = 0;
            ibNumElements = 0;
            ibIndexOffsetShift = 0;

            if (startIndex >= byteArrayLength)
                ec = ec.MapNullOrEmptyTo("IHHeader decode reached end of array");

            byte ihByte = byteArray.SafeAccess(startIndex++);
            ItemFormatCode ifc = unchecked((ItemFormatCode) (ihByte >> 2));
            int numLenBytes = (ihByte & 0x03);
            
            if (startIndex + numLenBytes > byteArrayLength)
                ec = ec.MapNullOrEmptyTo("IHHeader len decode reached end of array");

            switch (numLenBytes)
            {
                default:
                case 0: ec = ec.MapNullOrEmptyTo("IHHeader contained invalid zero for num len bytes"); break;
                case 1: ibNumBytes = unchecked((int)byteArray.SafeAccess(startIndex)); startIndex += 1; break;
                case 2: ibNumBytes = unchecked((int)Utils.Data.Pack2(byteArray, startIndex)); startIndex += 2; break;
                case 3: ibNumBytes = unchecked((int)Utils.Data.Pack3(byteArray, startIndex)); startIndex += 3; break;
            }
            ibNumElements = ibNumBytes;

            if (!ec.IsNullOrEmpty())
                return ItemFormatCode.Invalid;

            // validate and calculate ibNumElements from ibNumBytes - no additional validation required for byte sized objects (Bo, Bi, I1, U1).
            switch (ifc)
            {
                case ItemFormatCode.W:
                    if (ibNumBytes >= 2 && ibNumBytes.IsEven())
                    {
                        // skip over the string type bytes
                        startIndex += 2;

                        ibNumElements = (ibNumBytes - 2) >> 1;
                    }
                    else
                    {
                        ec = ec.MapNullOrEmptyTo("Invalid IFC.{0} body length of {1}.  Must be >= 2 and must be even".CheckedFormat(ifc, ibNumBytes));
                    }
                    break;
                case ItemFormatCode.I2:
                case ItemFormatCode.U2:
                    ibIndexOffsetShift = 1;
                    break;
                case ItemFormatCode.I4:
                case ItemFormatCode.U4:
                case ItemFormatCode.F4:
                    ibIndexOffsetShift = 2;
                    break;
                case ItemFormatCode.I8:
                case ItemFormatCode.U8:
                case ItemFormatCode.F8:
                    ibIndexOffsetShift = 3;
                    break;
            }

            if (ibIndexOffsetShift != 0)
            {
                ibNumElements = (ibNumBytes >> ibIndexOffsetShift);
                if (ibNumBytes != (ibNumElements << ibIndexOffsetShift))
                {
                    ec = ec.MapNullOrEmptyTo("Invalid IFC.{0} body length of {1}.  Must be a multiple of {2}".CheckedFormat(ifc, ibNumBytes, (1 << ibIndexOffsetShift)));
                }
            }

            if (!ec.IsNullOrEmpty())
                return ItemFormatCode.Invalid;

            return ifc;
        }

        private static readonly IList<string> emptyIListOfString = new List<string>().AsReadOnly();
        private static readonly IList<ValueContainer> emptyIListOfVC = new List<ValueContainer>().AsReadOnly();

        public static ValueContainer DecodeE005Data(this byte[] byteArray, ref int startIndex, ref string ec)
        {
            int ibNumBytes, ibNumElements, ibIndexOffsetShift;
            ItemFormatCode ifc = DecodeE005ItemHeader(byteArray, ref startIndex, ref ec, out ibNumBytes, out ibNumElements, out ibIndexOffsetShift);

            int byteArrayLength = byteArray.Length;
            int remainingBytes = (byteArrayLength - startIndex);

            if (ifc == ItemFormatCode.None && ec.IsNullOrEmpty())
                return ValueContainer.Empty;

            bool isList = (ifc == ItemFormatCode.L);

            if (!isList && startIndex + ibNumBytes > byteArrayLength)
                ec = ec.MapNullOrEmptyTo("IHHeader payload length larger then array remaining space");

            if (!ec.IsNullOrEmpty())
                return ValueContainer.Empty;

            ValueContainer vc = ValueContainer.Empty;
            int localStartIndex = startIndex;     // so we can use it in lamba expressions

            switch (ifc)
            {
                case ItemFormatCode.L:
                    {
                        List<ValueContainer> vcList = new List<ValueContainer>();

                        for (int listItemIndex = 0; listItemIndex < ibNumElements && ec.IsNullOrEmpty(); listItemIndex++)
                        {
                            vcList.Add(DecodeE005Data(byteArray, ref startIndex, ref ec));
                        }

                        /// Todo: reconfirm that we should not recognize and automatically convert IListOfVC into IListOfString.

                        vc.SetFromObject(vcList);
                    }

                    ibNumBytes = 0; // block later startIndex += ibNumBytes from changing startIndex for this IFC case (lists use the byte count as the sub-element count)
                    break;

                case ItemFormatCode.A:
                case ItemFormatCode.J:
                    vc.SetValue<string>(ByteArrayTranscoders.ByteStringTranscoder.Encode(byteArray, localStartIndex, ibNumElements));
                    break;

                case ItemFormatCode.W:
                    {
                        StringBuilder sb = new StringBuilder(ibNumElements);

                        // DecodeE005ItemHeader has already skipped over the 2 byte string format type.  we always treat it as UTC2

                        for (int i = 0; i < ibNumElements; i++)
                        {
                            sb.Append(unchecked((char)Utils.Data.Pack2(byteArray, localStartIndex)));
                            localStartIndex += 2;                                
                        }

                        vc.SetFromObject(sb.ToString());
                    }
                    break;

                case ItemFormatCode.Bo:
                    if (ibNumElements == 1)
                        vc.SetValue(byteArray[localStartIndex] != 0);
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyBoArray);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idx => (byteArray[localStartIndex + idx] != 0)).ToArray());
                    break;

                case ItemFormatCode.Bi: 
                    // NOTE: only the numElements == 1 case is unique relative to U1 format.  both array representations would be represented in the vc as Object storage types and thus cannot be destinguished
                    if (ibNumElements == 1)
                        vc.SetValue(byteArray[localStartIndex], ContainerStorageType.Binary, false);
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyU1Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => byteArray[localStartIndex + idxOffset]).ToArray());
                    break;

                case ItemFormatCode.I1:
                    if (ibNumElements == 1)
                        vc.SetValue(unchecked((sbyte)byteArray[localStartIndex]));
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyI1Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => unchecked((sbyte)byteArray[localStartIndex + idxOffset])).ToArray());
                    break;

                case ItemFormatCode.I2:
                    if (ibNumElements == 1)
                        vc.SetValue(unchecked((short)Utils.Data.Pack2(byteArray, localStartIndex)));
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyI2Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => unchecked((short)Utils.Data.Pack2(byteArray, localStartIndex + (idxOffset << ibIndexOffsetShift)))).ToArray());
                    break;

                case ItemFormatCode.I4:
                    if (ibNumElements == 1)
                        vc.SetValue(unchecked((int)Utils.Data.Pack4(byteArray, localStartIndex)));
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyI4Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => unchecked((int)Utils.Data.Pack4(byteArray, localStartIndex + (idxOffset << ibIndexOffsetShift)))).ToArray());
                    break;

                case ItemFormatCode.I8:
                    if (ibNumElements == 1)
                        vc.SetValue(unchecked((long)Utils.Data.Pack8(byteArray, localStartIndex)));
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyI8Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => unchecked((long)Utils.Data.Pack8(byteArray, localStartIndex + (idxOffset << ibIndexOffsetShift)))).ToArray());
                    break;

                case ItemFormatCode.U1:
                    // NOTE: only the numElements == 1 case is unique relative to Bi format.  both array representations would be represented in the vc as Object storage types and thus cannot be destinguished
                    if (ibNumElements == 1)
                        vc.SetValue(byteArray[localStartIndex], ContainerStorageType.Byte, false);
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyU1Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => byteArray[localStartIndex + idxOffset]).ToArray());
                    break;

                case ItemFormatCode.U2:
                    if (ibNumElements == 1)
                        vc.SetValue(Utils.Data.Pack2(byteArray, localStartIndex));
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyU2Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => Utils.Data.Pack2(byteArray, localStartIndex + (idxOffset << ibIndexOffsetShift))).ToArray());
                    break;

                case ItemFormatCode.U4:
                    if (ibNumElements == 1)
                        vc.SetValue(Utils.Data.Pack4(byteArray, localStartIndex));
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyU4Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => Utils.Data.Pack4(byteArray, localStartIndex + (idxOffset << ibIndexOffsetShift))).ToArray());
                    break;

                case ItemFormatCode.U8:
                    if (ibNumElements == 1)
                        vc.SetValue(Utils.Data.Pack8(byteArray, localStartIndex));
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyU8Array);
                    else
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => Utils.Data.Pack8(byteArray, localStartIndex + (idxOffset << ibIndexOffsetShift))).ToArray());
                    break;

                case ItemFormatCode.F4:
                    if (ibNumElements == 1)
                    {
                        vc.cvt = ContainerStorageType.Single;
                        vc.u.u32 = Utils.Data.Pack4(byteArray, localStartIndex);
                    }
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyF4Array);
                    else
                    {
                        ValueContainer vc2 = new ValueContainer(0.0f);
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => { vc2.u.u32 = Utils.Data.Pack4(byteArray, localStartIndex + (idxOffset << ibIndexOffsetShift)); return vc2.u.f32; }).ToArray());
                    }
                    break;

                case ItemFormatCode.F8:
                    if (ibNumElements == 1)
                    {
                        vc.cvt = ContainerStorageType.Double;
                        vc.u.u64 = Utils.Data.Pack8(byteArray, localStartIndex);
                    }
                    else if (ibNumElements == 0)
                        vc.SetFromObject(emptyF8Array);
                    else
                    {
                        ValueContainer vc2 = new ValueContainer(0.0);
                        vc.SetFromObject(Enumerable.Range(0, ibNumElements).Select(idxOffset => { vc2.u.u64 = Utils.Data.Pack8(byteArray, localStartIndex + (idxOffset << ibIndexOffsetShift)); return vc2.u.f64; }).ToArray());
                    }
                    break;
            }

            startIndex += ibNumBytes;

            return vc;
        }

        private static readonly bool[] emptyBoArray = new bool[0];
        private static readonly byte[] emptyBiArray = new byte[0];
        private static readonly sbyte[] emptyI1Array = new sbyte[0];
        private static readonly short[] emptyI2Array = new short[0];
        private static readonly int[] emptyI4Array = new int[0];
        private static readonly long[] emptyI8Array = new long[0];
        private static readonly byte[] emptyU1Array = new byte[0];
        private static readonly ushort[] emptyU2Array = new ushort[0];
        private static readonly uint[] emptyU4Array = new uint[0];
        private static readonly ulong[] emptyU8Array = new ulong[0];
        private static readonly float[] emptyF4Array = new float[0];
        private static readonly double[] emptyF8Array = new double[0];

        #endregion
    }

    #endregion

    #region IValueContainerBuilder interface and related classes and methods

    /// <summary>
    /// This interface defines the internally usable method that any IValueContainerBuilder object provides to allow the given ValueContainer object tree to be incrementally
    /// constructed to contain the desired content.
    /// </summary>
    public interface IValueContainerBuilder
    {
        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        ValueContainer BuildContents();
    }

    /// <summary>
    /// This struct contains a set of properties that are used to determine how SECS-II data type conversions are to be performed for cases where
    /// a single managed type may be represented by more than on SECS-II data type.  
    /// </summary>
    public struct TypeConversionSettings
    {
        public bool ByteIsBinary { get; set; }
        public bool StringsUseW { get; set; }
    }

    /// <summary>
    /// This class is derived from System.Collections.Generic.List{IValueContainerBuilder} and implements the IValueContainerBuilder interface.
    /// This class overrides the Add and AddRange methods to return this object so as to support call chaining.
    /// Whenever the BuildContents method is invoked this object will generate and return a ValueContainer containing an array of ValueContainers generated by the subordinate IValueContainerBuilders BuildContents methods.
    /// </summary>
    public class ListValueBuilder : List<IValueContainerBuilder>, IValueContainerBuilder
    {
        /// <summary>
        /// Default constructor.  Produces an empty list.
        /// </summary>
        public ListValueBuilder() : base() { }

        /// <summary>
        /// Enumarable based constructor.  Adds each of the ICxValueBuilder objects from the given enumerable collection.
        /// </summary>
        public ListValueBuilder(IEnumerable<IValueContainerBuilder> collection) : base(collection) { }

        /// <summary>
        /// Adds the given object to the end of this list.  Return value supports call chaining.
        /// </summary>
        public new ListValueBuilder Add(IValueContainerBuilder item)
        {
            base.Add(item);
            return this;
        }

        /// <summary>
        /// Adds the given range of objects to the end of this list.  Return value supports call chaining.
        /// </summary>
        public new ListValueBuilder AddRange(IEnumerable<IValueContainerBuilder> collection)
        {
            base.AddRange(collection);
            return this;
        }

        #region IValueContainerBuilder Members

        ValueContainer[] vcArray = null;
        int vcArrayLength = 0;

        ValueContainer IValueContainerBuilder.BuildContents()
        {
            if (vcArray == null || vcArray.Length != this.Count)
            {
                vcArray = new ValueContainer[this.Count];
                vcArrayLength = vcArray.Length;
            }

            for (int listIdx = 0; listIdx < vcArrayLength; listIdx++)
            {
                IValueContainerBuilder vcb = this[listIdx];
                vcArray[listIdx] = ((vcb != null) ? vcb.BuildContents() : ValueContainer.Empty);
            }

            return new ValueContainer(vcArray);
        }

        #endregion
    }

    /// <summary>
    /// Helper class for building list with a pair of BasicValueBuilder built values in it.
    /// </summary>
    public class TwoValueListBuilder<ValueType1, ValueType2> : IValueContainerBuilder
    {
        public TwoValueListBuilder()
        {
            listBuilder.Add(valueBuilder1);
            listBuilder.Add(valueBuilder2);
        }

        private ListValueBuilder listBuilder = new ListValueBuilder();
        private BasicValueBuilder<ValueType1> valueBuilder1 = new BasicValueBuilder<ValueType1>();
        private BasicValueBuilder<ValueType2> valueBuilder2 = new BasicValueBuilder<ValueType2>();

        public ValueType1 Value1 { get { return valueBuilder1.Value; } set { valueBuilder1.Value = value; } }
        public ValueType2 Value2 { get { return valueBuilder2.Value; } set { valueBuilder2.Value = value; } }

        ValueContainer IValueContainerBuilder.BuildContents()
        {
            return ((IValueContainerBuilder)listBuilder).BuildContents();
        }
    }

    /// <summary>
    /// Helper class for generating a list representation of a NamedValueSet.
    /// </summary>
    public class NamedValueSetBuilder : IValueContainerBuilder
    {
        public INamedValueSet NamedValueSet { get; set; }

        ValueContainer IValueContainerBuilder.BuildContents()
        {
            return new ValueContainer(NamedValueSet);
        }
    }

    /// <summary>
    /// Helper class for generating a list representation of a single NamedValue object
    /// </summary>
    public class NamedValueBuilder : IValueContainerBuilder
    {
        public INamedValue NamedValue { get; set; }

        ValueContainer IValueContainerBuilder.BuildContents()
        {
            return new ValueContainer(NamedValue);
        }
    }

    /// <summary>
    /// Helper class for building Attribute name/value pair lists where the value element has an arbitrary type.
    /// </summary>
    public class AttributeListValueBuilder : ListValueBuilder, IValueContainerBuilder
    {
        /// <summary>
        /// Default constructor.  Produces a list of two elements: An attribute name and a ICxValueBuilder for the value.
        /// </summary>
        public AttributeListValueBuilder()
        {
            Add(attributeNameBuilder);
            Add(null);      // placeholder for the value  ValueBuilder property is used to define the actual builder object that is used here.
        }

        /// <summary>
        /// Get/Set property give access to the AttributeName that will be placed in the attribute/value two element list that is generated by this object.
        /// </summary>
        public String AttributeName { get { return attributeNameBuilder.Value; } set { attributeNameBuilder.Value = value ?? String.Empty; } }
        private BasicValueBuilder<string> attributeNameBuilder = new BasicValueBuilder<string>() { Value = String.Empty };

        /// <summary>
        /// Get/Set property gives access to the ICxValueBuilder that will be used to generte the value element in the attribute/value two element list that is generated by this object.
        /// </summary>
        public IValueContainerBuilder ValueBuilder { get { return this[1]; } set { this[1] = value; } }
    }

    /// <summary>
    /// Helper class for building Attribute name/value pair lists where the value element has a compile time known type.
    /// </summary>
    public class AttributeValueTupleBuilder<ValueType> : AttributeListValueBuilder, IValueContainerBuilder
    {
        public AttributeValueTupleBuilder()
        {
            base.ValueBuilder = valueBuilder;
        }

        private BasicValueBuilder<ValueType> valueBuilder = new BasicValueBuilder<ValueType>();

        public new IValueContainerBuilder ValueBuilder { get { return base.ValueBuilder; } private set { throw new System.FieldAccessException("ValueBuilder property is read only for this class"); } }

        public ValueType Value { get { return valueBuilder.Value; } set { valueBuilder.Value = value; } }
    }

    /// <summary>
    /// This generic class implements the IValueContainerBuilder interface and contains an Array of the indicated ElementType.  
    /// It may be passed a TypeConversionSettings value at construction time to customize the ByteType used when ElementType is System.Byte.  
    /// This class may only be used with native types that can be converted to SECS-II data types which support the array notation, notably Bi, Bo, I1, I2, I4, I8, U1, U2, U4, U8, F4 and F8.  
    /// Use of any other native type will cause the class constructor to throw a SetValueException.
    /// </summary>
    /// <typeparam name="ElementType">Defines the type of element for the Array object.  Will be converted to the corresponding SECS-II data type</typeparam>
    public class ArrayValueBuilder<ElementType> : IValueContainerBuilder where ElementType : struct
    {
        /// <summary>
        /// Get/Set property gives the caller access to the array that will be generated by the BuildContents method.  Defaults to null.  null will be treated as an empty list
        /// </summary>
        public ElementType[] Array { get; set; }

        private static readonly ElementType[] emptyArray = new ElementType[0];

        /// <summary>
        /// Gives the caller access to the TypeConversionSettings that is used by this object, either the default value or the caller provided value.
        /// </summary>
        public TypeConversionSettings TypeConversionSettings { get; private set; }

        /// <summary>
        /// Default constructor.  TypeConversionSettings is set to default value.
        /// </summary>
        /// <exception cref="SetValueException">thrown if ElementType is not a supported type</exception>
        public ArrayValueBuilder()
            : this(new TypeConversionSettings())
        {
        }

        /// <summary>
        /// Constructor.  TypeConversionSettings set to given value.
        /// </summary>
        /// <exception cref="SetValueException">thrown if ElementType is not a supported type</exception>
        public ArrayValueBuilder(TypeConversionSettings typeConversionSettings)
        {
            TypeConversionSettings = typeConversionSettings;
        }

        ValueContainer IValueContainerBuilder.BuildContents()
        {
            ElementType[] array = Array ?? emptyArray;

            if (array.Length == 1)
            {
                if (typeof(ElementType) != typeof(byte))
                    return new ValueContainer(array[0]);
                else if (TypeConversionSettings.ByteIsBinary)
                    return ValueContainer.Empty.SetValue(array[0], ContainerStorageType.Binary, false);
                else
                    return ValueContainer.Empty.SetValue(array[0], ContainerStorageType.Byte, false);
            }
            else
            {
                return new ValueContainer(array);
            }
        }
    }

    /// <summary>
    /// This class is a very simple placeholder class that supports injecting the contents of the contained ValueContainer object into an object tree.
    /// The BuildContents methods simply returns the current value of the ValueContainer property.
    /// </summary>
    public class ValueContainerBuilder : IValueContainerBuilder
    {
        public ValueContainer ValueContainer { get; set; }

        ValueContainer IValueContainerBuilder.BuildContents()
        {
            return ValueContainer;
        }
    }

    /// <summary>
    /// This generic class implements the IValueContainerBuilder interface and contains a single Value of the indicated ElementType.  
    /// It may be passed a TypeConversionSettings value at construction time to customize the ByteType or StringType used when ElementType is System.Byte or System.String.
    /// This class may only be used with native types that can be converted to SECS-II data types other than lists, notably A, W, Bi, Bo, I1, I2, I4, I8, U1, U2, U4, U8, F4, and F8.
    /// Use of any other native type will cause the class constructor to throw a SetValueException.
    /// </summary>
    /// <typeparam name="ElementType">Defines the type of element for the Array object.  Will be converted to the corresponding SECS-II data type</typeparam>
    public class BasicValueBuilder<ElementType> : IValueContainerBuilder
    {
        /// <summary>
        /// Get/Set property gives the caller access to the Value that will be appended when the AppendToCxValue method is used.
        /// </summary>
        public ElementType Value { get; set; }

        /// <summary>
        /// Gives the caller access to the TypeConversionSettings that is used by this object, either the default value or the caller provided value.
        /// </summary>
        public TypeConversionSettings TypeConversionSettings { get; private set; }

        /// <summary>
        /// Default Constructor.  TypeConversionSettings is set to default.
        /// </summary>
        /// <exception cref="SetValueException">thrown if ElementType is not a supported type</exception>
        public BasicValueBuilder()
            : this(new TypeConversionSettings())
        {
        }

        /// <summary>
        /// Constructor.  TypeConversionSettings is set to given value.
        /// </summary>
        /// <exception cref="SetValueException">thrown if ElementType is not a supported type</exception>
        public BasicValueBuilder(TypeConversionSettings typeConversionSettings)
        {
            TypeConversionSettings = typeConversionSettings;
        }

        ValueContainer IValueContainerBuilder.BuildContents()
        {
            if (typeof(ElementType) != typeof(byte))
                return new ValueContainer(Value);
            else if (TypeConversionSettings.ByteIsBinary)
                return ValueContainer.Empty.SetValue(Value, ContainerStorageType.Binary, false);
            else
                return ValueContainer.Empty.SetValue(Value, ContainerStorageType.Byte, false);
        }
    }

    #endregion

    #region Exception types: ConvertValueException, GetValueException, SetValueException

    /// <summary>
    /// Exception type used when any of the Convert[To/From]Str extension methods below above catch an unexpected exception and are told to (re)throw it
    /// </summary>
    public class ConvertValueException : System.Exception
    {
        public ConvertValueException(string mesg, System.Exception innerException) : base(mesg, innerException) { }
    }

    /// <summary>
    /// Exception type used when any of the GetYYY extension methods below fail to get a value of the desired data type.
    /// </summary>
    public class GetValueException : System.Exception
    {
        public GetValueException(string mesg, System.Exception innerException) : base(mesg, innerException) { }
    }

    /// <summary>
    /// Exception type used when any of the SetYYY extension methods below fail to set a value of the desired data type because they are not compatible with it.
    /// </summary>
    public class SetValueException : System.Exception
    {
        public SetValueException(string mesg, System.Exception innerException) : base(mesg, innerException) { }
    }

    #endregion
}
