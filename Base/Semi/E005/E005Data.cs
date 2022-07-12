//-------------------------------------------------------------------
/*! @file E005Data.cs
 *  @brief This files defines small set of classes and interfaces that are used with Semi standard data objects under the E005 standard (SECS-II)
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using System.Collections;

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

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        #region predicate helpers

        /// <summary>Returns true if the given <paramref name="ifc"/> is equal to ItemFormatCode.L</summary>
        public static bool IsList(this ItemFormatCode ifc)
        {
            return (ifc == ItemFormatCode.L);
        }

        /// <summary>Returns true if the given <paramref name="ifc"/> is equal to ItemFormatCode.None</summary>
        public static bool IsNone(this ItemFormatCode ifc)
        {
            return (ifc == ItemFormatCode.None);
        }

        /// <summary>Returns true if the given <paramref name="ifc"/> is equal to ItemFormatCode.Null</summary>
        public static bool IsNull(this ItemFormatCode ifc)
        {
            return (ifc == ItemFormatCode.Null);
        }

        /// <summary>Returns true if the given <paramref name="ifc"/> is equal to ItemFormatCode.Invalid</summary>
        public static bool IsInvalid(this ItemFormatCode ifc)
        {
            return (ifc == ItemFormatCode.Invalid);
        }

        /// <summary>Returns true if the given <paramref name="ifc"/> is neither ItemFormatCode.None nor ItemFormatCode.Invalid</summary>
        public static bool IsValid(this ItemFormatCode ifc)
        {
            return (ifc != ItemFormatCode.None && ifc != ItemFormatCode.Invalid);
        }

        /// <summary>Returns true if the given <paramref name="ifc"/> can be placed in an IH, false if not (ie value outside of range from 0 to 63)</summary>
        public static bool IsUsableWithE005(this ItemFormatCode ifc) 
        { 
            return (((int)ifc >= 0 && (int)ifc <= 63) || ifc.IsNone()); 
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
                case ContainerStorageType.Object: ifc = ItemFormatCode.L; return true;  // this is not a great choice
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
        #region ValueContainer ConvertFrom/To methods (ConvertToE005Data, ConvertFromE005Data)

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
                    new ConvertValueException("{0} failed on '{1}'".CheckedFormat(Fcns.CurrentMethodName, vc), ex).Throw();

                return emptyByteArray;
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
                    new ConvertValueException("{0} failed on '{1}'".CheckedFormat(Fcns.CurrentMethodName, vc), ex).Throw();

                return ValueContainer.Empty;
            }
        }

        #endregion

        #region NamedValueSet ConvertFrom/To methods (ConvertToE005Data, ConvertFromE005Data)

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
                    new ConvertValueException("{0} failed on '{1}'".CheckedFormat(Fcns.CurrentMethodName, nvs), ex).Throw();

                return emptyByteArray;
            }
        }

        public static readonly byte[] emptyByteArray = EmptyArrayFactory<byte>.Instance;

        public static NamedValueSet ConvertFromE005Data(this NamedValueSet nvs, byte[] byteArray, bool throwOnException)
        {
            System.Exception exToThrow = null;

            try
            {
                int startIndex = 0;
                string ec = string.Empty;

                nvs = nvs.ConvertFromE005Data(byteArray, ref startIndex, ref ec);

                if (!ec.IsNullOrEmpty())
                {
                    string byteArrayInHex = MosaicLib.Utils.ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray);
                    exToThrow = new ConvertValueException("{0} failed on '{1}': {2}".CheckedFormat(Fcns.CurrentMethodName, byteArrayInHex, ec), null);
                }
            }
            catch (System.Exception ex)
            {
                if (throwOnException)
                {
                    string byteArrayInHex = MosaicLib.Utils.ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray);
                    exToThrow = new ConvertValueException("{0} failed on '{1}'".CheckedFormat(Fcns.CurrentMethodName, byteArrayInHex), ex);
                }
            }

            if (exToThrow != null && throwOnException)
                exToThrow.Throw();

            return nvs;
        }

        public static NamedValueSet ConvertFromE005Data(this NamedValueSet nvs, byte[] byteArray, ref int startIndex, ref string ec)
        {
            nvs = nvs.ConvertToWritable(mapNullToEmpty: true);

            {
                int nvsListNumElements;

                ItemFormatCode nvsIFC = DecodeE005ItemHeader(byteArray, ref startIndex, ref ec, out nvsListNumElements);

                if (!nvsIFC.IsList() && ec.IsNullOrEmpty())
                    ec = "nvs root IFC.{0} is not a list".CheckedFormat(nvsIFC);

                for (int listIndex = 0; listIndex < nvsListNumElements && ec.IsNullOrEmpty(); listIndex++)
                {
                    int nvListNumElements;
                    ItemFormatCode nvIFC = DecodeE005ItemHeader(byteArray, ref startIndex, ref ec, out nvListNumElements);

                    if (!nvIFC.IsList() || (nvListNumElements != 2 && nvListNumElements != 1))
                        ec = "sub-list {0} [IFC.{1}/{2}] must be a 1 or 2 element list".CheckedFormat(listIndex, nvIFC, nvListNumElements);

                    ValueContainer vcName = ValueContainer.Empty, vcValue = ValueContainer.Empty;
                    string name = string.Empty;

                    if (ec.IsNullOrEmpty())
                    {
                        vcName = DecodeE005Data(byteArray, ref startIndex, ref ec);

                        name = vcName.GetValueA(false);
                    }

                    if (ec.IsNullOrEmpty() && name.IsNullOrEmpty())
                        ec = "in sub-list {0} could not obtain non-empty value name from {1}".CheckedFormat(listIndex, vcName);

                    // leave the vcValue empty for 1 element lists.
                    if (ec.IsNullOrEmpty() && (nvListNumElements == 2))
                        vcValue = DecodeE005Data(byteArray, ref startIndex, ref ec);

                    if (ec.IsNullOrEmpty())
                        nvs.SetValue(name, vcValue);
                }
            }

            return nvs;
        }

        #endregion

        #region NamedValue ConvertFrom/To methods(ConvertToE005Data, ConvertFromE005Data)


        public static byte[] ConvertToE005Data(this INamedValue nv, bool throwOnException)
        {
            try
            {
                List<byte> byteArrayBuilder = new List<byte>();

                byteArrayBuilder.AppendWithIH(nv);

                return (byteArrayBuilder.ToArray());
            }
            catch (System.Exception ex)
            {
                if (throwOnException)
                    new ConvertValueException("{0} failed on '{1}'".CheckedFormat(Fcns.CurrentMethodName, nv), ex).Throw();

                return emptyByteArray;
            }
        }

        public static NamedValue ConvertFromE005Data(this NamedValue nv, byte[] byteArray, bool throwOnException)
        {
            System.Exception exToThrow = null;

            try
            {
                int startIndex = 0;
                string ec = string.Empty;

                nv = nv.ConvertFromE005Data(byteArray, ref startIndex, ref ec);

                if (!ec.IsNullOrEmpty())
                {
                    string byteArrayInHex = MosaicLib.Utils.ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray);
                    exToThrow = new ConvertValueException("{0} failed on '{1}': {2}".CheckedFormat(Fcns.CurrentMethodName, byteArrayInHex, ec), null);
                }
            }
            catch (System.Exception ex)
            {
                if (throwOnException)
                {
                    string byteArrayInHex = MosaicLib.Utils.ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray);
                    exToThrow = new ConvertValueException("{0} failed on '{1}'".CheckedFormat(Fcns.CurrentMethodName, byteArrayInHex), ex);
                }
            }

            if (exToThrow != null && throwOnException)
                exToThrow.Throw();

            return nv;
        }

        public static NamedValue ConvertFromE005Data(this NamedValue nv, byte[] byteArray, ref int startIndex, ref string ec)
        {
            nv = nv.ConvertToWritable(mapNullToEmpty: true);

            {
                int nvListNumElements;

                ItemFormatCode nvIFC = DecodeE005ItemHeader(byteArray, ref startIndex, ref ec, out nvListNumElements);

                if ((!nvIFC.IsList() || (nvListNumElements != 2 && nvListNumElements != 1)) && ec.IsNullOrEmpty())
                    ec = "nv root IFC.{0}/{1} must be a 1 or 2 element list".CheckedFormat(nvIFC, nvListNumElements);

                ValueContainer vcName = ValueContainer.Empty, vcValue = ValueContainer.Empty;

                if (ec.IsNullOrEmpty())
                {
                    vcName = DecodeE005Data(byteArray, ref startIndex, ref ec);

                    nv.Name = vcName.GetValueA(false);
                }

                if (ec.IsNullOrEmpty() && nv.Name.IsNullOrEmpty())
                    ec = "nv could not obtain non-empty name from {1}".CheckedFormat(vcName);

                // leave the vcValue empty for 1 element lists.
                if (ec.IsNullOrEmpty() && (nvListNumElements == 2))
                    vcValue = DecodeE005Data(byteArray, ref startIndex, ref ec);

                if (ec.IsNullOrEmpty())
                    nv.VC = vcValue;
            }

            return nv;
        }
        #endregion

        #region Append support methods (AppendWithIH variants, AppendListHeader, AppendIH, AppendContentBytes, AppendRaw variants)

        /// <summary>
        /// Appends the given object <paramref name="o"/> to the given <paramref name="byteArrayBuilder"/>.
        /// Directly supports INamedValueSet, INamedValue, string [], and IList{string}.  
        /// In all other cases it creates a ValueContainer for the given value and calls AppendWithIH on that.
        /// </summary>
        public static void AppendWithIH(this List<byte> byteArrayBuilder, object o)
        {
            if (o == null)
                byteArrayBuilder.AppendWithIH(ValueContainer.CreateFromObject(o));
            else if (o is INamedValueSet)
                byteArrayBuilder.AppendWithIH(o as INamedValueSet);
            else if (o is INamedValue)
                byteArrayBuilder.AppendWithIH(o as INamedValue);
            else if (o is string[])
                byteArrayBuilder.AppendWithIH(o as string[]);
            else if (o is IList<string>)
                byteArrayBuilder.AppendWithIH(o as IList<string>);
            else
                byteArrayBuilder.AppendWithIH(ValueContainer.CreateFromObject(o));
        }

        /// <summary>
        /// Appends the contents of the given <paramref name="vc"/> to the given <paramref name="byteArrayBuilder"/>.
        /// Supported content (ContainerStorageType) types include: Bo, Bi, I1, I2, I4, I8, U1, U2, U4, U8, F4, F8, A, IListOfString, INamedValueSet, INamedValue, arrays of supported value types, and IListOfVC for supported VC content types.
        /// When given an unsupported type, this method simply appends the ToStringSML contents of the given <paramref name="vc"/> to the <paramref name="byteArrayBuilder"/> as a string.
        /// </summary>
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
                case ContainerStorageType.IListOfString: byteArrayBuilder.AppendWithIH(vc.GetValueLS(true)); return;
                case ContainerStorageType.IListOfVC: byteArrayBuilder.AppendWithIH(vc.GetValueL(true)); return;
                case ContainerStorageType.INamedValueSet: byteArrayBuilder.AppendWithIH(vc.o as INamedValueSet); return;
                case ContainerStorageType.INamedValue: byteArrayBuilder.AppendWithIH(vc.o as INamedValue); return;
                case ContainerStorageType.Object:
                    if (vc.o != null)
                    {
                        Type oType = vc.o.GetType();

                        if (vc.o is INamedValue) { byteArrayBuilder.AppendWithIH(vc.o as INamedValue); return; }
                        else if (vc.o is INamedValueSet) { byteArrayBuilder.AppendWithIH(vc.o as INamedValueSet); return; }
                        else if (vc.o is ValueContainer[]) { byteArrayBuilder.AppendWithIH(new List<ValueContainer>(vc.o as ValueContainer[] ?? emptyVCArray)); return; }
                        else if (vc.o is string[]) { byteArrayBuilder.AppendWithIH(vc.GetValue<string[]>(true)); return; }
                        else if (oType == typeof(bool[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.Bo, vc.o as bool[]); return; }
                        else if (oType == typeof(sbyte[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.I1, vc.o as sbyte[]); return; }
                        else if (oType == typeof(short[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.I2, vc.o as short[]); return; }
                        else if (oType == typeof(int[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.I4, vc.o as int[]); return; }
                        else if (oType == typeof(long[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.I8, vc.o as long[]); return; }
                        else if (oType == typeof(byte[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.U1, vc.o as byte[]); return; }
                        else if (oType == typeof(ushort[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.U2, vc.o as ushort[]); return; }
                        else if (oType == typeof(uint[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.U4, vc.o as uint[]); return; }
                        else if (oType == typeof(ulong[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.U8, vc.o as ulong[]); return; }
                        else if (oType == typeof(float[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.F4, vc.o as float[]); return; }
                        else if (oType == typeof(double[])) { byteArrayBuilder.AppendWithIH(ItemFormatCode.F8, vc.o as double[]); return; }
                        else if (oType == typeof(BiArray)) {  byteArrayBuilder.AppendWithIH(ItemFormatCode.Bi, (vc.o as BiArray).SafeToArray()) ; return; }
                    }

                    break;
                default:
                    break;
            }

            byteArrayBuilder.AppendWithIH(vc.ToStringSML());     // fallback is always to handle it like a string
        }

        /// <summary>
        /// Gives an empty array of ValueContainer elements.
        /// </summary>
        public static readonly ValueContainer[] emptyVCArray = EmptyArrayFactory<ValueContainer>.Instance;

        /// <summary>
        /// Appends the given <paramref name="nvs"/> to the given <paramref name="byteArrayBuilder"/>.
        /// This consists of a list of the individual NamedValues from the given <paramref name="nvs"/> which are appending using the corresonding AppendWithIH variant.
        /// </summary>
        public static void AppendWithIH(this List<byte> byteArrayBuilder, INamedValueSet nvs)
        {
            nvs = nvs ?? NamedValueSet.Empty;

            byteArrayBuilder.AppendListHeader(nvs.Count);

            foreach (INamedValue nv in nvs)
            {
                byteArrayBuilder.AppendWithIH(nv);
            }
        }

        /// <summary>
        /// Appends the given <paramref name="nv"/> to the given <paramref name="byteArrayBuilder"/>.
        /// If the <paramref name="nv"/> is not empty then this will consist of a two element list with the <paramref name="nv"/>.Name followed by the <paramref name="nv"/>.VC.
        /// Otherwise if the <paramref name="nv"/> is empty it will consist of a one element list with just the <paramref name="nv"/>'s Name.
        /// </summary>
        public static void AppendWithIH(this List<byte> byteArrayBuilder, INamedValue nv)
        {
            nv = nv ?? NamedValue.Empty;

            if (!nv.VC.IsEmpty)
            {
                byteArrayBuilder.AppendListHeader(2);

                byteArrayBuilder.AppendWithIH(nv.Name);
                byteArrayBuilder.AppendWithIH(nv.VC);
            }
            else
            {
                byteArrayBuilder.AppendListHeader(1);

                byteArrayBuilder.AppendWithIH(nv.Name);

                // there is no representation in E005 data for an empty value.  As such we just do not include the value at all in this case
            }
        }

        /// <summary>
        /// Appends the given string <paramref name="s"/> to the given <paramref name="byteArrayBuilder"/> using either A or W representation as appropriate, depending on the contents of the given string.
        /// </summary>
        public static void AppendWithIH(this List<byte> byteArrayBuilder, string s)
        {
            s = s ?? string.Empty;

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

        /// <summary>
        /// Appends the given <paramref name="stringList"/> list of strings to the given <paramref name="byteArrayBuilder"/>
        /// </summary>
        public static void AppendWithIH(this List<byte> byteArrayBuilder, IList<string> stringList)
        {
            stringList = stringList ?? ReadOnlyIList<string>.Empty;
            int stringListCount = stringList.Count;

            byteArrayBuilder.AppendListHeader(stringListCount);

            for (int idx = 0; idx < stringListCount; idx++)
                byteArrayBuilder.AppendWithIH(stringList[idx]);
        }

        /// <summary>
        /// Appends the given <paramref name="stringArray"/> set of strings to the given <paramref name="byteArrayBuilder"/>
        /// </summary>
        public static void AppendWithIH(this List<byte> byteArrayBuilder, string[] stringArray)
        {
            stringArray = stringArray ?? emptyStringArray;

            byteArrayBuilder.AppendListHeader(stringArray.Length);

            foreach (string s in stringArray)
                byteArrayBuilder.AppendWithIH(s);
        }

        /// <summary>
        /// Gives an empty array of strings.
        /// </summary>
        public static readonly string [] emptyStringArray = EmptyArrayFactory<string>.Instance;

        /// <summary>
        /// Appends the given <paramref name="vcList"/> list of ValueContainer items to the given <paramref name="byteArrayBuilder"/>
        /// </summary>
        public static void AppendWithIH(this List<byte> byteArrayBuilder, IList<ValueContainer> vcList)
        {
            vcList = vcList ?? emptyVCList;

            byteArrayBuilder.AppendListHeader(vcList.Count);

            int count = vcList.Count;
            for (int idx = 0; idx < count; idx++)
                byteArrayBuilder.AppendWithIH(vcList[idx]);
        }

        /// <summary>
        /// Contains an Empty ReadOnlyIList{ValueContainer}
        /// </summary>
        public static readonly IList<ValueContainer> emptyVCList = ReadOnlyIList<ValueContainer>.Empty;

        /// <summary>
        /// Appends an array of the given <typeparamref name="TItemType"/> to the given <paramref name="byteArrayBuilder"/>.  Caller must provide the matching <paramref name="ifc"/>.
        /// <para/>Note the given itemSizeInBytes is ignored.
        /// </summary>
        public static void AppendWithIH<TItemType>(this List<byte> byteArrayBuilder, ItemFormatCode ifc, int itemSizeInBytes, TItemType[] itemArray) where TItemType : struct
        {
            byteArrayBuilder.AppendWithIH(ifc, itemArray);
        }

        /// <summary>
        /// Appends an array of the given <typeparamref name="TItemType"/> to the given <paramref name="byteArrayBuilder"/>.  Caller must provide the <paramref name="ifc"/> that matches the given <typeparamref name="TItemType"/>.
        /// <para/>Supports ItemFormatCodes: Bo, Bi, U1, U2, U4, U8, I1, I2, I4, I8, F4, F8
        /// </summary>
        /// <exception cref="UnsupportedTypeException">is thrown if the given <paramref name="ifc"/> is not one of the supported content types (listed above)</exception>
        public static void AppendWithIH<TItemType>(this List<byte> byteArrayBuilder, ItemFormatCode ifc, TItemType[] itemArray) where TItemType : struct
        {
            itemArray = itemArray.MapNullToEmpty();
            int itemArrayLength = itemArray.SafeLength();

            switch (ifc)
            {
                case ItemFormatCode.Bo:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 1);
                    foreach (var item in (itemArray as bool[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.Bi:
                case ItemFormatCode.U1:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 1);
                    foreach (var item in (itemArray as byte[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.U2:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 2);
                    foreach (var item in (itemArray as ushort[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.U4:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 4);
                    foreach (var item in (itemArray as uint[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.U8:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 8);
                    foreach (var item in (itemArray as ulong[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.I1:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 1);
                    foreach (var item in (itemArray as sbyte[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.I2:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 2);
                    foreach (var item in (itemArray as short[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.I4:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 4);
                    foreach (var item in (itemArray as int[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.I8:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 8);
                    foreach (var item in (itemArray as long[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.F4:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 4);
                    foreach (var item in (itemArray as float[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                case ItemFormatCode.F8:
                    byteArrayBuilder.AppendIH(ifc, itemArrayLength * 8);
                    foreach (var item in (itemArray as double[])) { byteArrayBuilder.AppendRaw(item); }
                    break;
                default:
                    new UnsupportedTypeException("{0} does not support use with ItemFormatCode.{1}".CheckedFormat(Fcns.CurrentMethodName, ifc)).Throw();
                    break;
            }
        }

        /// <summary>
        /// Appends a list header for a list with the given <paramref name="numItems"/> number of items to the given <paramref name="byteArrayBuilder"/>
        /// <para/>Calls AppendIH(ItemFormatCode.L, <paramref name="numItems"/>)
        /// </summary>
        public static void AppendListHeader(this List<byte> byteArrayBuilder, int numItems)
        {
            byteArrayBuilder.AppendIH(ItemFormatCode.L, numItems);
        }

        /// <summary>
        /// Appends an IH header for the given <paramref name="ifc"/> ItemFormatCode and <paramref name="numItems"/>.
        /// <para/>The E005 IH header consists of a mixed type and <paramref name="numItems"/> count byte followed by between 1 and 3 bytes of the actual content byte count.
        /// </summary>
        public static void AppendIH(this List<byte> byteArrayBuilder, ItemFormatCode ifc, int numItems)
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

        /// <summary>
        /// Appends the raw contents of the given <paramref name="vc"/> to the given <paramref name="byteArrayBuilder"/>.
        /// <para/>Supports ContainerStorageTypes: Bo, Bi, I1, I2, I4, I8, U1, U2, U4, U8, F4, and F8
        /// </summary>
        /// <exception cref="UnsupportedTypeException">is thrown if the given <paramref name="vc"/>'s value is not one of the supported content types (listed above)</exception>
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
                    new UnsupportedTypeException("{0} cannot be used directly with {1}".CheckedFormat(Fcns.CurrentMethodName, vc)).Throw();
                    return;
            }
        }



        /// <summary>Appends the raw contents of the given <paramref name="bo"/> to the given <paramref name="byteArrayBuilder"/></summary>
        public static void AppendRaw(this List<byte> byteArrayBuilder, bool bo)
        {
            byteArrayBuilder.Add(unchecked((byte)(bo ? 1 : 0)));
        }

        /// <summary>Appends the raw contents of the given <paramref name="i8"/> to the given <paramref name="byteArrayBuilder"/></summary>
        public static void AppendRaw(this List<byte> byteArrayBuilder, sbyte i8)
        {
            byteArrayBuilder.Add(unchecked((byte)(i8 >> 0)));
        }

        /// <summary>Appends the raw contents of the given <paramref name="i16"/> to the given <paramref name="byteArrayBuilder"/> in big-endian order</summary>
        public static void AppendRaw(this List<byte> byteArrayBuilder, short i16)
        {
            byteArrayBuilder.Add(unchecked((byte)(i16 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(i16 >> 0)));
        }

        /// <summary>Appends the raw contents of the given <paramref name="i32"/> to the given <paramref name="byteArrayBuilder"/> in big-endian order</summary>
        public static void AppendRaw(this List<byte> byteArrayBuilder, int i32)
        {
            byteArrayBuilder.Add(unchecked((byte)(i32 >> 24)));
            byteArrayBuilder.Add(unchecked((byte)(i32 >> 16)));
            byteArrayBuilder.Add(unchecked((byte)(i32 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(i32 >> 0)));
        }

        /// <summary>Appends the raw contents of the given <paramref name="i64"/> to the given <paramref name="byteArrayBuilder"/> in big-endian order</summary>
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

        /// <summary>Appends the raw contents of the given <paramref name="u8"/> to the given <paramref name="byteArrayBuilder"/></summary>
        public static void AppendRaw(this List<byte> byteArrayBuilder, byte u8)
        {
            byteArrayBuilder.Add(unchecked((byte)(u8 >> 0)));
        }

        /// <summary>Appends the raw contents of the given <paramref name="u16"/> to the given <paramref name="byteArrayBuilder"/> in big-endian order</summary>
        public static void AppendRaw(this List<byte> byteArrayBuilder, ushort u16)
        {
            byteArrayBuilder.Add(unchecked((byte)(u16 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(u16 >> 0)));
        }

        /// <summary>Appends the raw contents of the given <paramref name="u32"/> to the given <paramref name="byteArrayBuilder"/> in big-endian order</summary>
        public static void AppendRaw(this List<byte> byteArrayBuilder, uint u32)
        {
            byteArrayBuilder.Add(unchecked((byte)(u32 >> 24)));
            byteArrayBuilder.Add(unchecked((byte)(u32 >> 16)));
            byteArrayBuilder.Add(unchecked((byte)(u32 >> 8)));
            byteArrayBuilder.Add(unchecked((byte)(u32 >> 0)));
        }

        /// <summary>Appends the raw contents of the given <paramref name="u64"/> to the given <paramref name="byteArrayBuilder"/> in big-endian order</summary>
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

        /// <summary>Appends the raw contents of the given <paramref name="f32"/> to the given <paramref name="byteArrayBuilder"/> in big-endian order</summary>
        public static void AppendRaw(this List<byte> byteArrayBuilder, float f32)
        {
            ValueContainer.Union u = new ValueContainer.Union() { f32 = f32 };
            byteArrayBuilder.AppendRaw(u.u32);
        }

        /// <summary>Appends the raw contents of the given <paramref name="f64"/> to the given <paramref name="byteArrayBuilder"/> in big-endian order</summary>
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
                    return new ValueContainer(new ReadOnlyIList<string>(vcList.Select(vc => (vc.o as String))));
                else if (deep)
                    return new ValueContainer(new ReadOnlyIList<ValueContainer>(vcList.Select(vc => vc.Normalize(convertEmptyList, true))));
            }

            return lhsVC;
        }
        
        #endregion

        #region DecodeE005Data (byte array) related methods [includes Normalize]

        public static ValueContainer DecodeE005Data(this byte[] byteArray)
        {
            int startIndex = 0;
            string ec = null;

            ValueContainer vc = DecodeE005Data(byteArray, ref startIndex, ref ec);

            if (ec == null && startIndex != byteArray.Length)
                ec = "not all bytes were consumed ({0} != {1})".CheckedFormat(startIndex, byteArray.Length);

            if (!ec.IsNullOrEmpty())
            {
                string byteArrayInHex = MosaicLib.Utils.ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray);

                new ConvertValueException("{0} failed on '{1}': {2}".CheckedFormat(Fcns.CurrentMethodName, byteArrayInHex, ec)).Throw();
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

                        ibNumBytes -= 2;
                        ibNumElements = ibNumBytes >> 1;
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

        private static readonly IList<ValueContainer> emptyIListOfVC = ReadOnlyIList<ValueContainer>.Empty;

        public static ValueContainer DecodeE005Data(this byte[] byteArray, ref int startIndex, ref string ec)
        {
            int entryStartIndex = startIndex;

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

                        // NOTE: after the following line, the vc will contain an array of ValueContainers.  Calling vc.GetValue<string []>(true) will convert this to an array of strings automatically.
                        // As such this code does not need to be concerned with detecting and destinguishing between list of strings and lists of values (superset of list of strings)

                        vc.SetFromObject(vcList);
                    }

                    ibNumBytes = 0; // block later startIndex += ibNumBytes from changing startIndex for this IFC case (lists use the byte count as the sub-element count)
                    break;

                case ItemFormatCode.A:
                case ItemFormatCode.J:
                    vc.SetValueA(ByteArrayTranscoders.ByteStringTranscoder.Encode(byteArray, localStartIndex, ibNumElements));
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
                    if (ibNumElements == 1)
                        vc.SetValue(byteArray[localStartIndex], ContainerStorageType.Binary, false);
                    else if (ibNumElements == 0)
                        vc.SetFromObject(BiArray.Empty);
                    else
                        vc.SetFromObject(new BiArray(Enumerable.Range(0, ibNumElements).Select(idxOffset => byteArray[localStartIndex + idxOffset]).ToArray()));
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

        private static readonly bool[] emptyBoArray = EmptyArrayFactory<bool>.Instance;
        private static readonly sbyte[] emptyI1Array = EmptyArrayFactory<sbyte>.Instance;
        private static readonly short[] emptyI2Array = EmptyArrayFactory<short>.Instance;
        private static readonly int[] emptyI4Array = EmptyArrayFactory<int>.Instance;
        private static readonly long[] emptyI8Array = EmptyArrayFactory<long>.Instance;
        private static readonly byte[] emptyU1Array = EmptyArrayFactory<byte>.Instance;
        private static readonly ushort[] emptyU2Array = EmptyArrayFactory<ushort>.Instance;
        private static readonly uint[] emptyU4Array = EmptyArrayFactory<uint>.Instance;
        private static readonly ulong[] emptyU8Array = EmptyArrayFactory<ulong>.Instance;
        private static readonly float[] emptyF4Array = EmptyArrayFactory<float>.Instance;
        private static readonly double[] emptyF8Array = EmptyArrayFactory<double>.Instance;

        #endregion
    }

    #endregion

    #region IValueContainerBuilder interface and related classes and methods

    /// <summary>
    /// This interface defines the internally usable method that any IValueContainerBuilder object provides to allow the given ValueContainer object tree to be incrementally
    /// constructed to contain the desired content.
    /// <para/>This interface now also implements (requires the implementation of) the IE005DataContentBuilder interface which supports construction of the built object in its
    /// E005 data byte sequence representation.
    /// </summary>
    public interface IValueContainerBuilder : IE005DataContentBuilder
    {
        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        ValueContainer BuildContents();
    }

    /// <summary>
    /// This interface defines the internally usable method that any IE005DataContentBuilder object provides to allow the given builder tree to be incrementally appended to a byte array builder list
    /// </summary>
    public interface IE005DataContentBuilder
    {
        /// <summary>
        /// Generates and appends the contents of this builder as E005 data to the given <paramref name="byteArrayBuilder"/> list.
        /// </summary>
        void BuildAndAppendContents(List<byte> byteArrayBuilder);

        /// <summary>
        /// Generates and returns a byte array contains the E005 content version of the given builder tree.
        /// </summary>
        byte[] BuildByteArray();
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
    /// Base class for most IValueContainerBuilder derived classes.
    /// </summary>
    public abstract class ValueContainerBuilderBase : IValueContainerBuilder
    {
        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        public abstract ValueContainer BuildContents();

        /// <summary>
        /// Generates and appends the contents of this builder as E005 data to the given <paramref name="byteArrayBuilder"/> list.
        /// </summary>
        public virtual void BuildAndAppendContents(List<byte> byteArrayBuilder)
        {
            var vc = BuildContents();

            byteArrayBuilder.AppendWithIH(vc);
        }

        /// <summary>
        /// Generates and returns a byte array contains the E005 content version of the given builder tree.
        /// </summary>
        public byte[] BuildByteArray()
        {
            var byteArrayBuilder = new List<byte>();

            BuildAndAppendContents(byteArrayBuilder);

            return byteArrayBuilder.ToArray();
        }

        /// <summary>
        /// Generates and returns the current built contents formatted using the SML representation.
        /// </summary>
        public override string ToString()
        {
            return BuildContents().ToStringSML();
        }
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
        public ListValueBuilder()
            : base()
        { }

        /// <summary>
        /// Enumarable based constructor.  Adds each of the IValueContainerBuilder objects from the given enumerable collection.
        /// </summary>
        public ListValueBuilder(IEnumerable<IValueContainerBuilder> collection) 
            : base(collection)
        { }

        /// <summary>
        /// params based constructor.
        /// </summary>
        public ListValueBuilder(params IValueContainerBuilder[] itemParamsArray)
            : base(itemParamsArray)
        { }

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

        ValueContainer[] vcArray = null;
        int vcArrayLength = 0;

        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        public ValueContainer BuildContents()
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

        /// <summary>
        /// Generates and appends the contents of this builder as E005 data to the given <paramref name="byteArrayBuilder"/> list.
        /// </summary>
        public void BuildAndAppendContents(List<byte> byteArrayBuilder)
        {
            byteArrayBuilder.AppendIH(ItemFormatCode.L, Count);
            foreach (IE005DataContentBuilder dcb in this)
                dcb.BuildAndAppendContents(byteArrayBuilder);
        }

        /// <summary>
        /// Generates and returns a byte array contains the E005 content version of the given builder tree.
        /// </summary>
        public byte[] BuildByteArray()
        {
            var byteArrayBuilder = new List<byte>();

            BuildAndAppendContents(byteArrayBuilder);

            return byteArrayBuilder.ToArray();
        }

        /// <summary>
        /// Generates and returns the current built contents formatted using the SML representation.
        /// </summary>
        public override string ToString()
        {
            return BuildContents().ToStringSML();
        }
    }

    /// <summary>
    /// Helper class for building list with a pair of BasicValueBuilder built values in it.
    /// </summary>
    public class TwoValueListBuilder<ValueType1, ValueType2> : ValueContainerBuilderBase
    {
        public TwoValueListBuilder()
        {
            listBuilder.Add(valueBuilder1);
            listBuilder.Add(valueBuilder2);
        }

        private readonly ListValueBuilder listBuilder = new ListValueBuilder();
        private readonly BasicValueBuilder<ValueType1> valueBuilder1 = new BasicValueBuilder<ValueType1>();
        private readonly BasicValueBuilder<ValueType2> valueBuilder2 = new BasicValueBuilder<ValueType2>();

        public ValueType1 Value1 { get { return valueBuilder1.Value; } set { valueBuilder1.Value = value; } }
        public ValueType2 Value2 { get { return valueBuilder2.Value; } set { valueBuilder2.Value = value; } }

        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        public override ValueContainer BuildContents()
        {
            return ((IValueContainerBuilder)listBuilder).BuildContents();
        }

        /// <summary>
        /// Generates and appends the contents of this builder as E005 data to the given <paramref name="byteArrayBuilder"/> list.
        /// </summary>
        public override void BuildAndAppendContents(List<byte> byteArrayBuilder)
        {
            listBuilder.BuildAndAppendContents(byteArrayBuilder);
        }
    }

    /// <summary>Helper class for generating a list representation of a NamedValueSet.</summary>
    public class NamedValueSetBuilder : ValueContainerBuilderBase, System.Collections.IEnumerable
    {
        /// <summary>
        /// Gives the INamedValueSet instance that the contents will be built from.
        /// </summary>
        public INamedValueSet NamedValueSet { get; set; }

        /// <summary>
        /// Support inline construction
        /// </summary>
        public NamedValueSetBuilder Add(string name, IValueContainerBuilder vcb)
        {
            nvsBuilderDictionary = nvsBuilderDictionary ?? new Dictionary<string, IValueContainerBuilder>();

            nvsBuilderDictionary[name] = vcb;

            return this;
        }

        /// <summary>
        /// Support inline construction
        /// </summary>
        public NamedValueSetBuilder Add(string name, ValueContainer vc)
        {
            var nvs = (NamedValueSet as NamedValueSet);
            if (nvs == null || nvs.IsReadOnly)
            {
                NamedValueSet = (nvs = NamedValueSet.ConvertToWritable());
            }

            nvs.SetValue(name, vc);

            return this;
        }

        /// <summary>
        /// Support inline construction
        /// </summary>
        public NamedValueSetBuilder Add(string name, object o)
        {
            Add(name, ValueContainer.CreateFromObject(o));

            return this;
        }

        /// <summary>
        /// Required for use of Add method(s) in initializers
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            var set = EmptyArrayFactory<object>.Instance
                    .ConditionalConcatItems(!NamedValueSet.IsNullOrEmpty(), NamedValueSet)
                    .ConditionalConcatItems(!nvsBuilderDictionary.IsNullOrEmpty(), nvsBuilderDictionary)
                    .ToArray();

            return ((IEnumerable)set).GetEnumerator();
        }

        private Dictionary<string, IValueContainerBuilder> nvsBuilderDictionary = null;

        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        public override ValueContainer BuildContents()
        {
            if (nvsBuilderDictionary == null)
                return new ValueContainer(NamedValueSet.ConvertToReadOnly());

            var nvs = new NamedValueSet(NamedValueSet.MapNullToEmpty());

            foreach (var kvp in nvsBuilderDictionary)
                nvs.SetValue(kvp.Key, kvp.Value.BuildContents());

            return new ValueContainer(nvs);
        }

        public override void BuildAndAppendContents(List<byte> byteArrayBuilder)
        {
            var nvs = NamedValueSet.MapNullToEmpty();

            if (nvsBuilderDictionary != null && nvsBuilderDictionary.Count > 0)
            {
                var nonOverlappingNVSetArray = nvs.Where(nv => !nvsBuilderDictionary.ContainsKey(nv.Name)).ToArray();
                var combinedItemCount = nvsBuilderDictionary.Count + nonOverlappingNVSetArray.Length;

                byteArrayBuilder.AppendIH(ItemFormatCode.L, combinedItemCount);

                foreach (var nv in nonOverlappingNVSetArray)
                {
                    byteArrayBuilder.AppendWithIH(nv);
                }

                foreach (var vcb in nvsBuilderDictionary)
                {
                    byteArrayBuilder.AppendIH(ItemFormatCode.L, 2);
                    byteArrayBuilder.AppendWithIH(vcb.Key);
                    vcb.Value.BuildAndAppendContents(byteArrayBuilder);
                }
            }
            else
            {
                byteArrayBuilder.AppendIH(ItemFormatCode.L, nvs.Count);

                foreach (var nv in nvs)
                {
                    byteArrayBuilder.AppendWithIH(nv);
                }
            }
        }
    }

    /// <summary>Helper class for generating a list representation of a single NamedValue object</summary>
    public class NamedValueBuilder : ValueContainerBuilderBase
    {
        /// <summary>Default Constructor</summary>
        public NamedValueBuilder() { }

        /// <summary>Content builder variant</summary>
        public NamedValueBuilder(string name, ValueContainer vc) { NamedValue = new NamedValue(name, vc); }

        /// <summary>Content builder variant</summary>
        public NamedValueBuilder(string name, object value) { NamedValue = new NamedValue(name, value); }

        /// <summary>
        /// Gives the INamedValue instance that the contents will be built from.
        /// </summary>
        public INamedValue NamedValue { get; set; }

        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        public override ValueContainer BuildContents()
        {
            return new ValueContainer(NamedValue.MapNullToEmpty());
        }

        /// <summary>
        /// Generates and appends the contents of this builder as E005 data to the given <paramref name="byteArrayBuilder"/> list.
        /// </summary>
        public override void BuildAndAppendContents(List<byte> byteArrayBuilder)
        {
            var nv = NamedValue.MapNullToEmpty();

            byteArrayBuilder.AppendWithIH(nv);
        }
    }

    /// <summary>
    /// Helper class for building Attribute name/value pair lists where the value element has an arbitrary type.
    /// </summary>
    public class AttributeListValueBuilder : ListValueBuilder, IValueContainerBuilder
    {
        /// <summary>
        /// Default constructor.  Produces a list of two elements: An attribute name and a IValueContainerBuilder for the value.
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
        private readonly BasicValueBuilder<string> attributeNameBuilder = new BasicValueBuilder<string>() { Value = String.Empty };

        /// <summary>
        /// Get/Set property gives access to the IValueContainerBuilder that will be used to generte the value element in the attribute/value two element list that is generated by this object.
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

        private readonly BasicValueBuilder<ValueType> valueBuilder = new BasicValueBuilder<ValueType>();

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
    public class ArrayValueBuilder<ElementType> : ValueContainerBuilderBase where ElementType : struct
    {
        /// <summary>
        /// Get/Set property gives the caller access to the array that will be generated by the BuildContents method.  Defaults to null.  null will be treated as an empty list
        /// </summary>
        public ElementType[] Array { get; set; }

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
        { }

        /// <summary>
        /// Constructor.  TypeConversionSettings set to given value.
        /// </summary>
        /// <exception cref="SetValueException">thrown if ElementType is not a supported type</exception>
        public ArrayValueBuilder(TypeConversionSettings typeConversionSettings)
        {
            TypeConversionSettings = typeConversionSettings;
        }

        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        public override ValueContainer BuildContents()
        {
            ElementType[] array = Array ?? EmptyArrayFactory<ElementType>.Instance;

            if (array.Length == 1)
            {
                if (typeof(ElementType) == typeof(byte) && TypeConversionSettings.ByteIsBinary)
                    return ValueContainer.Create(array[0], ContainerStorageType.Binary);
                else
                    return ValueContainer.Create(array[0]);
            }
            else
            {
                if (typeof(ElementType) == typeof(byte) && TypeConversionSettings.ByteIsBinary)
                    return new ValueContainer(new BiArray((byte [])(System.Object) array));
                else
                    return new ValueContainer(array);
            }
        }
    }

    /// <summary>
    /// This class is a very simple placeholder class that supports injecting the contents of the contained ValueContainer object into an object tree.
    /// The BuildContents methods simply returns the current value of the ValueContainer property.
    /// </summary>
    public class ValueContainerBuilder : ValueContainerBuilderBase
    {
        /// <summary>
        /// Gives the ValueContainer instance that the contents will be built from.
        /// </summary>
        public ValueContainer ValueContainer { get { return VC; } set { VC = value; } }

        /// <summary>
        /// Gives the ValueContainer instance that the contents will be built from.
        /// </summary>
        public ValueContainer VC { get; set; }

        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        public override ValueContainer BuildContents()
        {
            return VC;
        }

        /// <summary>
        /// Generates and appends the contents of this builder as E005 data to the given <paramref name="byteArrayBuilder"/> list.
        /// </summary>
        public override void BuildAndAppendContents(List<byte> byteArrayBuilder)
        {
            byteArrayBuilder.AppendWithIH(VC);
        }
    }

    /// <summary>
    /// This generic class implements the IValueContainerBuilder interface and contains a single Value of the indicated ElementType.  
    /// It may be passed a TypeConversionSettings value at construction time to customize the ByteType or StringType used when ElementType is System.Byte or System.String.
    /// This class may only be used with native types that can be converted to SECS-II data types other than lists, notably A, W, Bi, Bo, I1, I2, I4, I8, U1, U2, U4, U8, F4, and F8.
    /// Use of any other native type will cause the class constructor to throw a SetValueException.
    /// </summary>
    /// <typeparam name="ElementType">Defines the type of element for the Array object.  Will be converted to the corresponding SECS-II data type</typeparam>
    public class BasicValueBuilder<ElementType> : ValueContainerBuilderBase
    {
        /// <summary>
        /// Get/Set property gives the caller access to the Value that will be appended when the BuildContents method is used.
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

        /// <summary>
        /// Generates and returns a ValueContainer that contains the built contents of this entity.
        /// </summary>
        public override ValueContainer BuildContents()
        {
            if (typeof(ElementType) != typeof(byte))
                return new ValueContainer(Value);
            else if (TypeConversionSettings.ByteIsBinary)
                return ValueContainer.Create(Value, ContainerStorageType.Binary, false);
            else
                return ValueContainer.Create(Value, ContainerStorageType.Byte, false);
        }
    }

    #endregion

    #region Exception types: ConvertValueException, GetValueException, SetValueException

    /// <summary>
    /// Exception type used when any of the Convert[To/From]Str extension methods below above catch an unexpected exception and are told to (re)throw it
    /// </summary>
    public class ConvertValueException : System.Exception
    {
        public ConvertValueException(string mesg, System.Exception innerException = null) : base(mesg, innerException) { }
    }

    /// <summary>
    /// Exception type used when any of the GetYYY extension methods below fail to get a value of the desired data type.
    /// </summary>
    public class GetValueException : System.Exception
    {
        public GetValueException(string mesg, System.Exception innerException = null) : base(mesg, innerException) { }
    }

    /// <summary>
    /// Exception type used when any of the SetYYY extension methods below fail to set a value of the desired data type because they are not compatible with it.
    /// </summary>
    public class SetValueException : System.Exception
    {
        public SetValueException(string mesg, System.Exception innerException = null) : base(mesg, innerException) { }
    }

    /// <summary>
    /// Exception type used when a given type or ValueContainer content type is not supported by the current method.
    /// </summary>
    public class UnsupportedTypeException : System.Exception
    {
        public UnsupportedTypeException(string mesg, System.Exception innerException = null) : base(mesg, innerException) { }
    }

    #endregion

    #region Shorthand types (L, VCB, A, W, Bi, Bo, I1, I2, I4, I8, U1, U2, U4, U8, F4, F8, ShorthandValueBuilderBase<>, NV, NVS)

    /// <summary>
    /// Shorthand for simplified version of ListValueBuilder
    /// </summary>
    public class L : ListValueBuilder
    {
        /// <summary>
        /// constructs an empty list builder.
        /// <para/>Items may be added explicitly seperately as desired.
        /// </summary>
        public L()
            : base()
        { }

        /// <summary>
        /// nested list constructor.
        /// </summary>
        public L(ListValueBuilder nestedListBuilder)
            : base(nestedListBuilder as IValueContainerBuilder)
        { }

        /// <summary>
        /// params based constructor.
        /// </summary>
        public L(params IValueContainerBuilder[] itemParamsArray) 
            : base(itemParamsArray)
        { }

        /// <summary>
        /// enumerator based constructor.
        /// </summary>
        public L(IEnumerable<IValueContainerBuilder> builderSet) 
            : base(builderSet)
        { }
    }

    /// <summary>
    /// Shorthand for simplified version of ValueContainerBuilder
    /// </summary>
    public class VCB : ValueContainerBuilder
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public VCB() { }

        /// <summary>
        /// Content initialization constructor
        /// </summary>
        public VCB(ValueContainer vc) { VC = vc; }
    }

    /// <summary>
    /// Shorthand for ValueContainerBuilder that builds a string value.
    /// </summary>
    public class A : ValueContainerBuilder
    {
        /// <summary>Base constructor</summary>
        public A(string s = null)
        {
            Value = s;
        }

        /// <summary>This property can be used to get or set the contained value.  Setting the value to null is the same as setting it to the empty string.</summary>
        public string Value
        {
            get { return _value; }
            set { ValueContainer = ValueContainer.Create(_value = value.MapNullToEmpty()); }
        }

        private string _value = null;
    }

    /// <summary>
    /// Shorthand for ValueContainerBuilder that builds a string value.
    /// </summary>
    public class W : A
    {
        /// <summary>Base constructor</summary>
        public W(string s = null) : base(s) {}
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a Bi value</summary>
    public class Bi : ValueContainerBuilder
    {
        /// <summary>Base constructor</summary>
        public Bi(byte value = default(byte)) 
        {
            Value = value; 
        }

        /// <summary>This property can be used to get or set the contained value.</summary>
        public byte Value
        {
            get { return _value; }
            set { ValueContainer = ValueContainer.Create(_value = value, ContainerStorageType.Bi); }
        }

        private byte _value = default(byte);
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a Bo value</summary>
    public class Bo : ShorthandValueBuilderBase<bool>
    {
        /// <summary>Base constructor</summary>
        public Bo(bool value = default(bool)) : base(value) {}
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a I1 value</summary>
    public class I1 : ShorthandValueBuilderBase<sbyte>
    {
        /// <summary>Base constructor</summary>
        public I1(sbyte value = default(sbyte)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a I2 value</summary>
    public class I2 : ShorthandValueBuilderBase<short>
    {
        /// <summary>Base constructor</summary>
        public I2(short value = default(short)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a I4 value</summary>
    public class I4 : ShorthandValueBuilderBase<int>
    {
        /// <summary>Base constructor</summary>
        public I4(int value = default(int)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a I8 value</summary>
    public class I8 : ShorthandValueBuilderBase<long>
    {
        /// <summary>Base constructor</summary>
        public I8(long value = default(long)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a U1 value</summary>
    public class U1 : ShorthandValueBuilderBase<byte>
    {
        /// <summary>Base constructor</summary>
        public U1(byte value = default(byte)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a U2 value</summary>
    public class U2 : ShorthandValueBuilderBase<ushort>
    {
        /// <summary>Base constructor</summary>
        public U2(ushort value = default(ushort)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a U4 value</summary>
    public class U4 : ShorthandValueBuilderBase<uint>
    {
        /// <summary>Base constructor</summary>
        public U4(uint value = default(uint)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a U8 value</summary>
    public class U8 : ShorthandValueBuilderBase<ulong>
    {
        /// <summary>Base constructor</summary>
        public U8(ulong value = default(ulong)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a F4 value</summary>
    public class F4 : ShorthandValueBuilderBase<float>
    {
        /// <summary>Base constructor</summary>
        public F4(float value = default(float)) : base(value) { }
    }

    /// <summary>Shorthand ValueContainerBuilder that builds a F8 value</summary>
    public class F8 : ShorthandValueBuilderBase<double>
    {
        /// <summary>Base constructor</summary>
        public F8(double value = default(double)) : base(value) { }
    }

    /// <summary>Base class for some of the value builder shorthand classes.</summary>
    public class ShorthandValueBuilderBase<TValueType> : ValueContainerBuilder where TValueType : struct
    {
        /// <summary>Protected base constructor</summary>
        protected ShorthandValueBuilderBase(TValueType value)
        {
            Value = value;
        }

        /// <summary>This property can be used to get or set the contained value.</summary>
        public TValueType Value
        {
            get { return _value; }
            set { ValueContainer = ValueContainer.Create(_value = value); }
        }

        private TValueType _value = default(TValueType);
    }

    /// <summary>Shorthand name version of helper class for generating a list representation of a single NamedValue object</summary>
    public class NVB : NamedValueBuilder
    {
        /// <summary>Default Constructor</summary>
        public NVB() : base() { }

        /// <summary>Content builder variant</summary>
        public NVB(string name, ValueContainer vc) : base(name, vc) { }

        /// <summary>Content builder variant</summary>
        public NVB(string name, object value) : base(name, value) { }
    }

    /// <summary>Shorthand name version of helper class for generating a list representation of a NamedValueSet.</summary>
    public class NVSB : NamedValueSetBuilder
    { }

    #endregion

    #region ExtensionMethods (IValueContainerBuilder related)

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Creates, and returns, an IValueContainerBuilder for the given <paramref name="vc"/> value.
        /// </summary>
        public static IValueContainerBuilder MakeVCBuilder(this ValueContainer vc)
        {
            return new ValueContainerBuilder() { VC = vc };
        }

        /// <summary>
        /// Creates, and returns, a ListValueBuilder IValueContainerBuilder for the given <paramref name="vcArray"/> array of ValueContainers. 
        /// </summary>
        public static IValueContainerBuilder MakeListBuilder(this ValueContainer[] vcArray)
        {
            return new L(vcArray.Select(vc => vc.MakeVCBuilder()));
        }

        /// <summary>
        /// Creates, and returns, a ListValueBuilder IValueContainerBuilder for the given <paramref name="vcSet"/> array of ValueContainers. 
        /// </summary>
        public static IValueContainerBuilder MakeListBuilder(this IEnumerable<ValueContainer> vcSet)
        {
            return new L(vcSet.Select(vc => vc.MakeVCBuilder()));
        }

        /// <summary>
        /// Creates, and returns, a NamedValueSetBuilder IValudContainerBuilder for the given <paramref name="nvs"/>.
        /// </summary>
        public static IValueContainerBuilder MakeListBuilder(this INamedValueSet nvs)
        {
            return new NamedValueSetBuilder() { NamedValueSet = nvs.MapNullToEmpty() };
        }

        /// <summary>
        /// Returns a new list type ValueContainerBuilder that, when built, will produce a list of the given set of strings.
        /// </summary>
        public static IValueContainerBuilder MakeListBuilder(this IEnumerable<string> strSet)
        {
            return new VCB(ValueContainer.Create(strSet));
        }

        /// <summary>
        /// Returns a new list type ValueContainerBuilder that, when built, will produce a list of the given set of value items.
        /// </summary>
        public static IValueContainerBuilder MakeListBuilder<TItemType>(this IEnumerable<TItemType> itemSet) where TItemType : struct
        {
            return new VCB(ValueContainer.Create(itemSet.Select(item => ValueContainer.Create(item))));
        }
    }

    #endregion
}
