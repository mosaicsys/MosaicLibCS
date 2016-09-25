//-------------------------------------------------------------------
/*! @file Common.cs
 *  @brief This file defines common definitions that are used by Modular pattern implementations
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
using System.Collections.Generic;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections;
using System.Text;

using MosaicLib.Utils;

namespace MosaicLib.Modular.Common
{
    #region Basic types

    /// <summary>Define a Null type that can be used as a ParamType or a ResultType in ParamType and ResultType types in the IAction related pattern types.</summary>
    public class NullObj 
    {
        /// <summary>Default (only) constructor</summary>
        public NullObj() { } 
    }

    #endregion

    #region IDType

    /// <summary>This enum is used with ISessionValueSetServices (and other places) to define what the id should be used as for methods that support looking up something by an ID</summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public enum IDType
    {
        /// <summary>ID contains the name of an object</summary>
        [DataMember]
        Name = 0,

        /// <summary>ID contains the UUID of an object</summary>
        [DataMember]
        UUID = 1,
    }

    #endregion

    #region ValueContainer - contains all value type specific logic

    /// <summary>
    /// This is a value type storage for a set of well known types that can be used to help marshal many types of values from place to place using copying of an explicit box
    /// so-as to avoid the need to box and unbox the values so they can be passed as objects which would be expected to increase the GC load significantly and posses the added
    /// risk of forcing lots of high generation GC activity since these values are intended to be shared between threads.
    /// </summary>
    public struct ValueContainer : IEquatable<ValueContainer>
    {
        /// <summary>
        /// Custom value constructor.  Constructs the object and then assigns its initial value as ValueAsObject = given initialValueAsObject.
        /// <para/>ValueAsObject setter extracts the type information from the given object and then attempts to decode
        /// and use the corresponding type specific value storage type, if it is one of the supported types.  
        /// If the extracted type is not one of the supported container types then the given value is simply stored as the given System.Object.
        /// </summary>
        public ValueContainer(System.Object initialValueAsObject)
            : this()
        {
            ValueAsObject = initialValueAsObject;
        }

        /// <summary>A quick encoding on the field/union field in which the value has been placed.  (Type had been called ContainerValueType)</summary>
        public ContainerStorageType cvt;

        /// <summary>Reference to data types that are not supported by the Union</summary>
        public Object o;

        /// <summary>Union is used for storage of known simple data types.</summary>
        public Union u;

        /// <summary>The Union type allows for various types to be supported using fields that only use one 8 byte block for storage.</summary>
        [StructLayout(LayoutKind.Explicit, Pack = 8)]
        public struct Union
        {
            /// <summary>Boolean value in union</summary>
            [FieldOffset(0)]
            public System.Boolean b;
            /// <summary>"Binary" Byte value in union</summary>
            [FieldOffset(0)]
            public System.Byte bi;
            /// <summary>SByte value in union</summary>
            [FieldOffset(0)]
            public System.SByte i8;
            /// <summary>Int16 value in union</summary>
            [FieldOffset(0)]
            public System.Int16 i16;
            /// <summary>Int32 value in union</summary>
            [FieldOffset(0)]
            public System.Int32 i32;
            /// <summary>Int64 value in union</summary>
            [FieldOffset(0)]
            public System.Int64 i64;
            /// <summary>Byte value in union</summary>
            [FieldOffset(0)]
            public System.Byte u8;
            /// <summary>UInt16 value in union</summary>
            [FieldOffset(0)]
            public System.UInt16 u16;
            /// <summary>UInt32 value in union</summary>
            [FieldOffset(0)]
            public System.UInt32 u32;
            /// <summary>UInt64 value in union</summary>
            [FieldOffset(0)]
            public System.UInt64 u64;
            /// <summary>Single value in union</summary>
            [FieldOffset(0)]
            public System.Single f32;
            /// <summary>Double value in union</summary>
            [FieldOffset(0)]
            public System.Double f64;

            /// <summary>Helper property to read/write a TimeSpan - internally gets/saves the i64 TickCount in the TimeSpan</summary>
            public TimeSpan TimeSpan { get { return TimeSpan.FromTicks(i64); } set { i64 = value.Ticks; } }

            /// <summary>Helper property to read/write a DateTime - internally gets/saves the i64 Binary representation in the DateTime</summary>
            public DateTime DateTime { get { return DateTime.FromBinary(i64); } set { i64 = value.ToBinary(); } }

            /// <summary>
            /// Compare the contents of this instance to the rhs by comparing the contents of the largest fields that are in both.  For this purpose we are using u64.
            /// </summary>
            public bool IsEqualTo(Union rhs)
            {
                return (u64 == rhs.u64);
            }
        }

        /// <summary>
        /// Returns true if the contained type is ContainerStorageType.Object.
        /// </summary>
        public bool IsObject
        {
            get
            {
                return (cvt == ContainerStorageType.Object);
            }
        }

        /// <summary>
        /// Returns true if the contained type is ContainerStorageType.Object and contained value is null.
        /// </summary>
        public bool IsNullObject
        {
            get
            {
                return (cvt == ContainerStorageType.Object && o == null);
            }
        }

        /// <summary>
        /// Returns true if the contained type is ContainerStorageType.Object and contained value is not null.
        /// </summary>
        public bool IsNonNullObject
        {
            get
            {
                return (cvt == ContainerStorageType.Object && o != null);
            }
        }

        /// <summary>
        /// Returns true if the contained type is a reference type and contained value is null.
        /// </summary>
        public bool IsNull
        {
            get
            {
                return (cvt.IsReferenceType() && o == null);
            }
        }

        /// <summary>
        /// Returns true if the container is empty, aka if the cvt is ContainerStorageType.None.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return (cvt.IsNone());
            }
        }

        /// <summary>
        /// Returns true if the contained type is a reference type and contained value is null or the contained type is None
        /// </summary>
        public bool IsNullOrNone
        {
            get
            {
                return (cvt.IsNone() || IsNull);
            }
        }

        /// <summary>
        /// Returns true if the contained value IsNull or IsEmpty (this is functionally identical to the IsNullOrNone property
        /// </summary>
        public bool IsNullOrEmpty
        {
            get
            {
                return (IsEmpty || IsNull);
            }
        }

        /// <summary>
        /// Returns an ValueContainer with cvt set to ContainerStorageType.Object and o set to Null
        /// </summary>
        public static ValueContainer Null 
        { 
            get 
            { 
                return new ValueContainer().SetToNullObject(); 
            } 
        }

        /// <summary>
        /// Returns an ValueContainer with cvt set to ContainerStorageType.None
        /// </summary>
        public static ValueContainer Empty 
        { 
            get 
            { 
                return new ValueContainer().SetToEmpty(); 
            } 
        }

        /// <summary>
        /// Sets the container to contain the null object: cvt = ContainerStorageType.Object (0), o = null, u = default(Union) = default(Union)
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer SetToNullObject()
        {
            cvt = ContainerStorageType.Object;
            o = null;
            u = default(Union);

            return this;
        }

        /// <summary>
        /// Sets the container to be empty: cvt = ContainerStorageType.None, o = null, u = default(Union) = default(Union)
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer SetToEmpty()
        {
            cvt = ContainerStorageType.None;
            o = null;
            u = default(Union);

            return this;
        }

        /// <summary>
        /// Sets the container to have the same contents as the given rhs.
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer CopyFrom(ValueContainer rhs)
        {
            cvt = rhs.cvt;
            o = rhs.o;
            u = rhs.u;

            return this;
        }

        /// <summary>
        /// Sets the container to have the same contents as the given rhs, performing deep copy 
        /// if the given rhs is an IListOfString and the current contents are not equivilant to it.
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer DeepCopyFrom(ValueContainer rhs)
        {
            switch (rhs.cvt)
            {
                case ContainerStorageType.IListOfString:
                    {
                        cvt = ContainerStorageType.IListOfString;
                        u = rhs.u;

                        IList<string> rhsILS = rhs.GetValue<IList<String>>(ContainerStorageType.IListOfString, false, false);

                        if (rhsILS == null || rhsILS.IsReadOnly)
                        {
                            // if the rhs value is null, or is readonly then just assign it to this object and we are done
                            o = rhsILS;
                        }
                        else
                        {
                            // otherwise create a full readonly copy of the given rhsILS and save that for later use
                            o = new List<String>(rhsILS).AsReadOnly();
                        }

                        return this;
                    }

                case ContainerStorageType.IListOfVC:
                    {
                        cvt = ContainerStorageType.IListOfVC;
                        u = rhs.u;

                        IList<ValueContainer> rhsILVC = rhs.GetValue<IList<ValueContainer>>(ContainerStorageType.IListOfVC, false, false);

                        if (rhsILVC == null || rhsILVC.IsReadOnly)
                        {
                            // if the rhs value is null, or is readonly then just assign it to this object and we are done
                            o = rhsILVC;
                        }
                        else
                        {
                            // otherwise create a full readonly deep copy of the given rhsILVC and save that for later use
                            o = new List<ValueContainer>(rhsILVC.Select(vc => ValueContainer.Empty.DeepCopyFrom(vc))).AsReadOnly();
                        }

                        return this;
                    }

                default:
                    return CopyFrom(rhs);
            }
        }

        /// <summary>
        /// Accepts a given type and attempts to generate an apporpriate ContainerStorageType (and isNullable) value as the best container storage type to use with the given Type.
        /// Unrecognized type default as ContainerStorageType.Object.
        /// </summary>
        public static void DecodeType(Type type, out ContainerStorageType decodedValueType, out bool isNullable)
        {
            isNullable = (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(System.Nullable<>)));

            Type valueType = type;

            // for nullable types extract the underlying type.
            if (isNullable)
                valueType = Nullable.GetUnderlyingType(type);

            if (valueType == typeof(System.Boolean)) decodedValueType = ContainerStorageType.Boolean;
            else if (valueType == typeof(System.SByte)) decodedValueType = ContainerStorageType.SByte;
            else if (valueType == typeof(System.Int16)) decodedValueType = ContainerStorageType.Int16;
            else if (valueType == typeof(System.Int32)) decodedValueType = ContainerStorageType.Int32;
            else if (valueType == typeof(System.Int64)) decodedValueType = ContainerStorageType.Int64;
            else if (valueType == typeof(System.Byte)) decodedValueType = ContainerStorageType.Byte;
            else if (valueType == typeof(System.UInt16)) decodedValueType = ContainerStorageType.UInt16;
            else if (valueType == typeof(System.UInt32)) decodedValueType = ContainerStorageType.UInt32;
            else if (valueType == typeof(System.UInt64)) decodedValueType = ContainerStorageType.UInt64;
            else if (valueType == typeof(System.Single)) decodedValueType = ContainerStorageType.Single;
            else if (valueType == typeof(System.Double)) decodedValueType = ContainerStorageType.Double;
            else if (valueType == typeof(System.TimeSpan)) decodedValueType = ContainerStorageType.TimeSpan;
            else if (valueType == typeof(System.DateTime)) decodedValueType = ContainerStorageType.DateTime;
            else if (valueType == stringType) decodedValueType = ContainerStorageType.String;
            else if (iListOfStringType.IsAssignableFrom(valueType) || valueType == stringArrayType)
            {
                isNullable = false;
                decodedValueType = ContainerStorageType.IListOfString;
            }
            else if (iListOfVCType.IsAssignableFrom(valueType) || valueType == vcArrayType)
            {
                isNullable = false;
                decodedValueType = ContainerStorageType.IListOfVC;
            }
            else if (valueType.IsEnum)
            {
                isNullable = false;
                decodedValueType = ContainerStorageType.String;
            }
            else
            {
                isNullable = false;     // this flag is not useful when used with reference types
                decodedValueType = ContainerStorageType.Object;
            }
        }

        private static readonly Type stringType = typeof(System.String);
        private static readonly Type stringArrayType = typeof(System.String []);
        private static readonly Type vcArrayType = typeof(ValueContainer[]);
        private static readonly Type iListOfStringType = typeof(IList<System.String>);
        private static readonly Type iListOfVCType = typeof(IList<ValueContainer>);

        /// <summary>
        /// get/set property.
        /// getter returns the currently contained value casted as an System.Object
        /// setter extracts the type information from the given value and then attempts to decode
        /// and use the corresponding type specific value storage type, if it is one of the supported types.  
        /// If the extracted type is not one of the supported container types then the given value is simply stored as an System.Object.
        /// If the given object is a ValueContainer then this object's contents are set to be a copy of the given containers contents.
        /// <para/>Use SetValue{System.Object}(value) if you want to force the container to store value as an object.
        /// </summary>
        public System.Object ValueAsObject
        {
            get
            {
                switch (cvt)
                {
                    case ContainerStorageType.None: return null;
                    default:
                    case ContainerStorageType.Object: return o;
                    case ContainerStorageType.String: return o;
                    case ContainerStorageType.IListOfString: return o;
                    case ContainerStorageType.IListOfVC: return o;
                    case ContainerStorageType.Boolean: return u.b;
                    case ContainerStorageType.Binary: return u.bi;
                    case ContainerStorageType.SByte: return u.i8;
                    case ContainerStorageType.Int16: return u.i16;
                    case ContainerStorageType.Int32: return u.i32;
                    case ContainerStorageType.Int64: return u.i64;
                    case ContainerStorageType.Byte: return u.u8;
                    case ContainerStorageType.UInt16: return u.u16;
                    case ContainerStorageType.UInt32: return u.u32;
                    case ContainerStorageType.UInt64: return u.u64;
                    case ContainerStorageType.Single: return u.f32;
                    case ContainerStorageType.Double: return u.f64;
                    case ContainerStorageType.TimeSpan: return u.TimeSpan;
                    case ContainerStorageType.DateTime: return u.DateTime;
                }
            }
            set
            {
                if (value != null)
                {
                    Type t = value.GetType();

                    if (t == typeof(ValueContainer))
                    {
                        CopyFrom((ValueContainer)value);
                    }
                    else
                    {
                        ContainerStorageType valueCVT;
                        bool valueTypeIsNullable;

                        DecodeType(t, out valueCVT, out valueTypeIsNullable);

                        SetValue<System.Object>(value, valueCVT, valueTypeIsNullable);
                    }
                }
                else
                {
                    SetToNullObject();
                }
            }
        }

        /// <summary>
        /// Type extracting setter.  Extracts the type information from the given value and then attempts to decode
        /// and use the corresponding type specific value storage type, if it is one of the supported types.  If the extracted type
        /// is not one of the supported container types then the given value is simply stored as an System.Object
        /// If the given object is a ValueContainer then this object's contents are set to be a copy of the given containers contents.
        /// <para/>Use SetValue{System.Object}(value) if you want to force the container to store value as an object.
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer SetFromObject(System.Object value)
        {
            ValueAsObject = value;

            return this;
        }

        /// <summary>
        /// Typeed value setter method.  
        /// This method decodes the ContainerStorageType from the given TValueType and then sets the corresponding storage field from value.
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer SetValue<TValueType>(TValueType value)
        {
            Type t = typeof(TValueType);
            ContainerStorageType valueCVT;
            bool valueTypeIsNullable;
            DecodeType(t, out valueCVT, out valueTypeIsNullable);

            SetValue<TValueType>(value, valueCVT, valueTypeIsNullable);

            return this;
        }

        /// <summary>
        /// This method stores the given value in the desired container storage field.  If the given value does not fit in the indicated
        /// container then this method will attempt to convert the given value to an object and store it as such.
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer SetValue<TValueType>(TValueType value, ContainerStorageType decodedValueType, bool isNullable)
        {
            SetToNullObject();

            try
            {
                bool forceToNull = (isNullable && (value == null));

                if (!forceToNull)
                {
                    cvt = decodedValueType;
                    switch (decodedValueType)
                    {
                        case ContainerStorageType.None: o = null; cvt = ContainerStorageType.None; break;
                        default:
                        case ContainerStorageType.Object: o = (System.Object)value; cvt = ContainerStorageType.Object; break;
                        case ContainerStorageType.String: o = ((value != null) ? ((System.Object) value).ToString() : null); break;
                        case ContainerStorageType.Boolean: u.b = (System.Boolean)System.Convert.ChangeType(value, typeof(System.Boolean)); break;
                        case ContainerStorageType.Binary: u.bi = (System.Byte)System.Convert.ChangeType(value, typeof(System.Byte)); break;
                        case ContainerStorageType.SByte: u.i8 = (System.SByte)System.Convert.ChangeType(value, typeof(System.SByte)); break;
                        case ContainerStorageType.Int16: u.i16 = (System.Int16)System.Convert.ChangeType(value, typeof(System.Int16)); break;
                        case ContainerStorageType.Int32: u.i32 = (System.Int32)System.Convert.ChangeType(value, typeof(System.Int32)); break;
                        case ContainerStorageType.Int64: u.i64 = (System.Int64)System.Convert.ChangeType(value, typeof(System.Int64)); break;
                        case ContainerStorageType.Byte: u.u8 = (System.Byte)System.Convert.ChangeType(value, typeof(System.Byte)); break;
                        case ContainerStorageType.UInt16: u.u16 = (System.UInt16)System.Convert.ChangeType(value, typeof(System.UInt16)); break;
                        case ContainerStorageType.UInt32: u.u32 = (System.UInt32)System.Convert.ChangeType(value, typeof(System.UInt32)); break;
                        case ContainerStorageType.UInt64: u.u64 = (System.UInt64)System.Convert.ChangeType(value, typeof(System.UInt64)); break;
                        case ContainerStorageType.Single: u.f32 = (System.Single)System.Convert.ChangeType(value, typeof(System.Single)); break;
                        case ContainerStorageType.Double: u.f64 = (System.Double)System.Convert.ChangeType(value, typeof(System.Double)); break;
                        case ContainerStorageType.TimeSpan: u.TimeSpan = (System.TimeSpan)System.Convert.ChangeType(value, typeof(System.TimeSpan)); break;
                        case ContainerStorageType.DateTime: u.DateTime = (System.DateTime)System.Convert.ChangeType(value, typeof(System.DateTime)); break;
                        case ContainerStorageType.IListOfString: 
                            {
                                Object valueAsObject = (System.Object)value;
                                String [] valueAsArrayOfStrings = (valueAsObject as String []);
                                IList<String> oAsIListOfStrings = (o as IList<String>);

                                if (valueAsObject == null)
                                {
                                    o = emptyIListOfString;
                                }
                                else if (valueAsArrayOfStrings != null)
                                {
                                    if (oAsIListOfStrings == null || !oAsIListOfStrings.IsReadOnly || !(oAsIListOfStrings.IsEqualTo(valueAsArrayOfStrings)))
                                        o = new List<String>(valueAsArrayOfStrings).AsReadOnly();
                                    // else o already contains the same readonly List of string from the array - do not replace it - this optimization step is only done for this CST
                                }
                                else
                                {
                                    IList<String> valueAsIListOfStrings = ((IList<String>) valueAsObject);     // cast should not throw unless TValueType is not castable to IList<String>.  Also should not be null since valueAsObject is not null.

                                    if (oAsIListOfStrings == null || !oAsIListOfStrings.IsReadOnly || !(oAsIListOfStrings.IsEqualTo(valueAsIListOfStrings)))
                                    {
                                        if (valueAsIListOfStrings == null || valueAsIListOfStrings.IsReadOnly)
                                            o = valueAsIListOfStrings;
                                        else
                                            o = new List<String>(valueAsIListOfStrings).AsReadOnly();
                                    }
                                    // else o already contains the same readonly List of string contents as the given value.  no need to copy or change the existing contained value.
                                }
                                break;
                            }
                        case ContainerStorageType.IListOfVC:
                            {
                                Object valueAsObject = (System.Object)value;

                                // first case - the given value is null

                                ValueContainer[] valueAsArrayOfVCs = (valueAsObject as ValueContainer[]);

                                if (valueAsObject == null)
                                {
                                    o = emptyIListOfVC;
                                }
                                else if (valueAsArrayOfVCs != null)
                                {
                                    // convert given array into a readonly list of deep copies of each of the given VCs
                                    o = new List<ValueContainer>(valueAsArrayOfVCs.Select(vc => ValueContainer.Empty.DeepCopyFrom(vc))).AsReadOnly();
                                }
                                else
                                {
                                    IList<ValueContainer> valueAsILVC = ((IList<ValueContainer>)valueAsObject);     // cast should not throw unless TValueType is not castable to IList<VC>.  Also should not be null since valueAsObject is not null.

                                    if (valueAsILVC.IsReadOnly)
                                        o = valueAsILVC;        // if valueAsILVC is already readonly then we just keep that value as it has already been deep cloned
                                    else
                                        o = new List<ValueContainer>(valueAsILVC.Select(vc => ValueContainer.Empty.DeepCopyFrom(vc))).AsReadOnly();  // otherwise we need to generate a deep clone of the given list and save that new list (as readonly).
                                }
                                break;
                            }
                    }
                }
                else
                {
                    cvt = ContainerStorageType.Object;
                    // o has already been cleared above.
                }
            }
            catch
            {
                // if the above logic fails then cast the value as an object and save it as such.
                cvt = ContainerStorageType.Object;
                o = value;
            }

            return this;
        }

        /// <summary>
        /// Typed GetValue method.  
        /// This method decodes the TValueType to get the corresponding ContainerStorageType.
        /// If the contained value type matches this decoded ContainerStorageType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion is successful then this method returns the transfered/converted value.  If this transfer/conversion is not
        /// successful then this method returns default(TValueType).
        /// <para/>Note: because this method must decode the type information on each call, it may be less efficient to use this method than to use the version where the
        /// caller explicitly uses DecodeType method combined with the more complete GetValue method.
        /// </summary>
        public TValueType GetValue<TValueType>(bool rethrow)
        {
            Type t = typeof(TValueType);
            ContainerStorageType valueCVT;
            bool valueTypeIsNullable;

            DecodeType(t, out valueCVT, out valueTypeIsNullable);

            return GetValue<TValueType>(valueCVT, valueTypeIsNullable, rethrow);
        }

        /// <summary>
        /// property is updated each time GetValue is called.  null indicates that the transfer and/or conversion was successfull while any other value indicates why it was not.
        /// </summary>
        public System.Exception LastGetValueException { get; private set; }

        /// <summary>
        /// Typed GetValue method.  
        /// Requires that caller has pre-decoded the TValueType to obtain the expected ContainerStorageType and a flag to indicate if the TValueType is a nullable type.
        /// If the contained value type matches the decodedValueType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise if isNullable is false then the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion used and is successful then this method and returns the transfered/converted value.  
        /// If this transfer/conversion is not used or is not successful then this method returns default(TValueType).  
        /// If rethrow is true and the method encounters any excpetions then it will rethrow the exception.
        /// </summary>
        public TValueType GetValue<TValueType>(ContainerStorageType decodedValueType, bool isNullable, bool rethrow)
        {
            return GetValue<TValueType>(decodedValueType, isNullable, rethrow, true);
        }

        /// <summary>
        /// Typed GetValue method.  
        /// Requires that caller has pre-decoded the TValueType to obtain the expected ContainerStorageType and a flag to indicate if the TValueType is a nullable type.
        /// If the contained value type matches the decodedValueType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise if allowTypeChangeAttempt is true and isNullable is false then the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion used and is successful then this method and returns the transfered/converted value.  
        /// If this transfer/conversion is not used or is not successful then this method returns default(TValueType).  
        /// If rethrow is true and the method encounters any excpetions then it will rethrow the exception.
        /// </summary>
        public TValueType GetValue<TValueType>(ContainerStorageType decodedValueType, bool isNullable, bool rethrow, bool allowTypeChangeAttempt)
        {
            Type TValueTypeType = typeof(TValueType);
            TValueType value;

            LastGetValueException = null;

            try
            {
                if (TValueTypeType.IsEnum)
                {
                    if (!rethrow && IsNullOrEmpty)
                        value = default(TValueType);
                    else
                    {
                        // This covers both string and numeric representations of the enumeration as the string representation of a number can also be parsed as the enum.
                        value = (TValueType)System.Enum.Parse(TValueTypeType, ValueAsObject.ToString().MapNullToEmpty(), false);
                    }
                }
                else if (decodedValueType == cvt)
                {
                    // no conversion is required.  The stored type already mataches what the client is asking for.
                    switch (cvt)
                    {
                        case ContainerStorageType.None: value = default(TValueType); break;
                        default:
                        case ContainerStorageType.Object: value = (TValueType)o; break;
                        case ContainerStorageType.String: value = (TValueType)o; break;
                        case ContainerStorageType.Boolean: value = (TValueType)((System.Object)u.b); break;
                        case ContainerStorageType.Binary: value = (TValueType)((System.Object)u.bi); break;
                        case ContainerStorageType.SByte: value = (TValueType)((System.Object)u.i8); break;
                        case ContainerStorageType.Int16: value = (TValueType)((System.Object)u.i16); break;
                        case ContainerStorageType.Int32: value = (TValueType)((System.Object)u.i32); break;
                        case ContainerStorageType.Int64: value = (TValueType)((System.Object)u.i64); break;
                        case ContainerStorageType.Byte: value = (TValueType)((System.Object)u.u8); break;
                        case ContainerStorageType.UInt16: value = (TValueType)((System.Object)u.u16); break;
                        case ContainerStorageType.UInt32: value = (TValueType)((System.Object)u.u32); break;
                        case ContainerStorageType.UInt64: value = (TValueType)((System.Object)u.u64); break;
                        case ContainerStorageType.Single: value = (TValueType)((System.Object)u.f32); break;
                        case ContainerStorageType.Double: value = (TValueType)((System.Object)u.f64); break;
                        case ContainerStorageType.TimeSpan: value = (TValueType)((System.Object)u.TimeSpan); break;
                        case ContainerStorageType.DateTime: value = (TValueType)((System.Object)u.DateTime); break;
                        case ContainerStorageType.IListOfString:
                            {
                                // IListOfString can be read as String [] or as IList<String>
                                if ((TValueTypeType == stringArrayType))
                                    value = (TValueType)((System.Object)((o as IList<String> ?? emptyIListOfString).ToArray()));      // special case for reading from an IListOfString to an String array.
                                else
                                    value = (TValueType)((System.Object)(o as IList<String> ?? emptyIListOfString));      // all other cases the TValueType should be castable from an IList<String>

                                break; 
                            }
                        case ContainerStorageType.IListOfVC:
                            {
                                // IListOfVC can be read as VC [] or as IList<VC>
                                if ((TValueTypeType == vcArrayType))
                                    value = (TValueType)((System.Object)((o as IList<ValueContainer> ?? emptyIListOfVC).ToArray()));      // special case for reading from an IListOfVC to an VC array.
                                else
                                    value = (TValueType)((System.Object)(o as IList<ValueContainer> ?? emptyIListOfVC));      // all other cases the TValueType should be castable from an IList<VC>

                                break;
                            }
                    }
                }
                else if (decodedValueType == ContainerStorageType.TimeSpan && cvt.IsFloatingPoint() && allowTypeChangeAttempt)
                {
                    // support a direct conversion attempt from floating point value to TimeSpan by interpresting the floating value as seconds.
                    value = (TValueType)((System.Object) TimeSpan.FromSeconds((cvt == ContainerStorageType.Double) ? u.f64 : u.f32));
                }
                else if (decodedValueType.IsValueType() && !isNullable && !rethrow && IsNullOrEmpty)
                {
                    // support direct assignment of an empty or null container to a value type with rethrow = false by without any attempt to actually perform the conversion (it will fail).
                    value = default(TValueType);
                }
                else if (isNullable && (IsNullOrEmpty || (allowTypeChangeAttempt && cvt == ContainerStorageType.String && (o as string).IsNullOrEmpty())))
                {
                    // we can always assign a null/empty container to a nullable type by setting the type to its default (aka null).
                    // also allows simple conversion of any null or empty string to a nullable type using the same assignement to default (aka null).
                    value = default(TValueType);
                }
                else if (allowTypeChangeAttempt)
                {
                    value = default(TValueType);
                    System.Object valueAsObject = ValueAsObject;

                    bool conversionDone = false;
                    if (!conversionDone && decodedValueType == ContainerStorageType.String)
                    {
                        // convert contained type to a String using object.ToString()
                        value = (TValueType)System.Convert.ChangeType(((valueAsObject != null) ? valueAsObject.ToString() : null), typeof(TValueType));
                        conversionDone = true;
                    }

                    if (!conversionDone && decodedValueType.IsValueType() && valueAsObject is System.String)
                    {
                        // if the string is not empty then attempt to parse the string to the desired type.  This only succeeds if the entire string is parsed as the desired type.
                        StringScanner ss = new StringScanner(valueAsObject as System.String ?? String.Empty);
                        if (!ss.IsAtEnd)
                        {
                            switch (decodedValueType)
                            {
                                case ContainerStorageType.Boolean: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.Boolean>(false), typeof(System.Boolean)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.Binary: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.Byte>(0), typeof(System.Byte)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.SByte: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.SByte>(0), typeof(System.SByte)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.Int16: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.Int16>(0), typeof(System.Int16)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.Int32: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.Int32>(0), typeof(System.Int32)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.Int64: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.Int64>(0), typeof(System.Int64)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.Byte: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.Byte>(0), typeof(System.Byte)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.UInt16: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.UInt16>(0), typeof(System.UInt16)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.UInt32: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.UInt32>(0), typeof(System.UInt32)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.UInt64: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.UInt64>(0), typeof(System.UInt64)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.Single: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.Single>(0.0f), typeof(System.Single)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.Double: value = (TValueType)System.Convert.ChangeType(ss.ParseValue<System.Double>(0.0f), typeof(System.Double)); conversionDone = ss.IsAtEnd; break;
                                case ContainerStorageType.TimeSpan:
                                    // first attempt to parse the string as a double.  If that is completely successful then save it as a TimeSpan using FromSeconds.
                                    {
                                        StringScanner ssd = new StringScanner(ss);
                                        double d = 0.0;
                                        if (ssd.ParseValue(out d) && ssd.IsAtEnd)
                                        {
                                            value = (TValueType)((System.Object)TimeSpan.FromSeconds(d));
                                            conversionDone = true;
                                            break;
                                        }
                                    }
                                    // next attempt to extract a single token (to next whitespace) and parse that as a TimeSpan using its TryParse method.  
                                    // If that token was the entire input and it could be successfully parsed to a TimeSpan then the conversion is done.
                                    {
                                        string token = ss.ExtractToken();
                                        TimeSpan ts;
                                        conversionDone = TimeSpan.TryParse(token, out ts) && ss.IsAtEnd;
                                        value = (TValueType)((System.Object)ts);
                                        break;
                                    }
                                case ContainerStorageType.DateTime:
                                    {
                                        string token = ss.ExtractToken();
                                        DateTime dt;
                                        conversionDone = DateTime.TryParse(token, out dt) && ss.IsAtEnd;
                                        value = (TValueType)((System.Object)dt);
                                        break;
                                    }
                                default: break;
                            }
                        }
                    }

                    if (!conversionDone)
                    {
                        value = (TValueType)System.Convert.ChangeType(valueAsObject, typeof(TValueType));
                    }
                }
                else
                {
                    value = default(TValueType);
                    bool valueIsIntendedToBeNull = (cvt.IsReferenceType() && o == null);
                    if (!valueIsIntendedToBeNull)
                        LastGetValueException = new ValueContainerGetValueException("Unable to get {0} as type '{1}': No known conversion exists".CheckedFormat(this, typeof(TValueType)), null);
                }
            }
            catch (System.Exception ex)
            {
                value = default(TValueType);
                LastGetValueException = new ValueContainerGetValueException("Unable to get {0} as type '{1}': {2}".CheckedFormat(this, typeof(TValueType), ex.Message), ex);
            }

            if (rethrow && LastGetValueException != null)
                throw LastGetValueException;

            return value;
        }

        /// <summary>
        /// Typed TryGetValue method.  
        /// Requires that caller has pre-decoded the TValueType to obtain the expected ContainerStorageType and a flag to indicate if the TValueType is a nullable type.
        /// If the contained value type matches the decodedValueType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion is successful then this method and assigns the transfered/converted value and returns true.  
        /// If this transfer/conversion is not successful then this method assigns default(TValueType) and returns false.  
        /// If rethrow is true and the method encounters any excpetions then it will rethrow the exception.
        /// </summary>
        public bool TryGetValue<TValueType>(out TValueType result, ContainerStorageType decodedValueType, bool isNullable, bool rethrow)
        {
            result = default(TValueType);
            try
            {
                result = GetValue<TValueType>(decodedValueType, isNullable, rethrow);
                return (LastGetValueException == null);
            }
            catch (System.Exception ex)
            {
                if (LastGetValueException == null)
                    LastGetValueException = ex;
                if (rethrow)
                    throw;
                return false;
            }
        }

        /// <summary>
        /// Returns the estimated size of the given contents in bytes.  
        /// For non-reference types this returns 16.  
        /// For string types this returns the l6 + the length of the string in bytes
        /// For string array and string list types, this returns 16 + the summed length of each of the strings in bytes + 16 bytes of overhead for each one.
        /// </summary>
        public int EstimatedContentSizeInBytes
        {
            get
            {
                if (!cvt.IsReferenceType())
                {
                    return defaultBasePerItemSizeInBytes;      // we use a fixed estimate of 16 bytes for all non-object contents (including TimeStamp and DateTime)
                }
                else if (o is String)
                {
                    return defaultBasePerItemSizeInBytes + (o as String).EstimatedContentSizeInBytes();
                }
                else if (o is String[])
                {
                    return defaultBasePerItemSizeInBytes + (o as String[]).EstimatedContentSizeInBytes();
                }
                else if (o is IList<String>)
                {
                    return defaultBasePerItemSizeInBytes + (o as IList<String>).EstimatedContentSizeInBytes();
                }
                else if (o is IList<ValueContainer>)
                {
                    return defaultBasePerItemSizeInBytes + (o as IList<ValueContainer>).EstimatedContentSizeInBytes();
                }
                else if (o is ValueContainer[])
                {
                    return defaultBasePerItemSizeInBytes + (o as ValueContainer []).EstimatedContentSizeInBytes();
                }
                else
                {
                    return 0;
                }
            }
        }

        private const int defaultBasePerItemSizeInBytes = 16;

        /// <summary>
        /// Equality testing implementation method.  Uses ValueContainer in signature to remove need for casting (as with Equals).
        /// </summary>
        public bool IsEqualTo(ValueContainer rhs)
        {
            if (cvt != rhs.cvt)
                return false;

            if (IsNonNullObject)
            {
                ValueContainer[] vcArray = o as ValueContainer[];
                ValueContainer[] rhsVCArray = rhs.o as ValueContainer[];

                if (vcArray != null)
                {
                    return vcArray.IsEqualTo(rhsVCArray);
                }
            }

            if (cvt == ContainerStorageType.IListOfString)
                return (o as IList<String>).IsEqualTo(rhs.o as IList<String>);
            else if (cvt == ContainerStorageType.IListOfVC)
                return (o as IList<ValueContainer>).IsEqualTo(rhs.o as IList<ValueContainer>);
            else if (cvt.IsReferenceType())
                return System.Object.Equals(o, rhs.o);
            else if (cvt.IsNone())
                return true;
            else
                return u.IsEqualTo(rhs.u);
        }

        /// <summary>IEquatable{ValueContainer} Equals implementation.  Returns true if the contents of this ValueContainer are "equal" to the contents of the other ValueContainer.</summary>
        public bool Equals(ValueContainer other)
        {
            return IsEqualTo(other);
        }

        /// <summary>Support Equality testing for boxed versions.</summary>
        public override bool Equals(object rhsAsObject)
        {
            if ((rhsAsObject == null) || !(rhsAsObject is ValueContainer))
                return false;

            return IsEqualTo((ValueContainer)rhsAsObject);
        }

        /// <summary>Override GetHashCode because Equals has been.</summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Override ToString for logging and debugging.  
        /// This format is generally similar to SML except that it generally uses square brackets as element delimiters rather than
        /// greater and less than symbols (which are heavily used in XML and must thus be escaped when placing such strings in XML output).
        /// </summary>
        public override string ToString()
        {
            if (cvt.IsNone())
                return "[None]";

            if (IsNull)
            {
                if (cvt == ContainerStorageType.Object)
                    return "[Null]";
                else
                    return "[Null:{0}]".CheckedFormat(cvt);
            }

            switch (cvt)
            {
                case ContainerStorageType.Boolean:
                    return "[Bool {0}]".CheckedFormat(u.b);
                case ContainerStorageType.Binary:
                    return "[Bi {0}]".CheckedFormat(u.bi);
                case ContainerStorageType.SByte:
                    return "[I1 {0}]".CheckedFormat(u.i8);
                case ContainerStorageType.Int16:
                    return "[I2 {0}]".CheckedFormat(u.i16);
                case ContainerStorageType.Int32:
                    return "[I4 {0}]".CheckedFormat(u.i32);
                case ContainerStorageType.Int64:
                    return "[I8 {0}]".CheckedFormat(u.i64);
                case ContainerStorageType.Byte:
                    return "[U1 {0}]".CheckedFormat(u.u8);
                case ContainerStorageType.UInt16:
                    return "[U2 {0}]".CheckedFormat(u.u16);
                case ContainerStorageType.UInt32:
                    return "[U4 {0}]".CheckedFormat(u.u32);
                case ContainerStorageType.UInt64:
                    return "[U8 {0}]".CheckedFormat(u.u64);
                case ContainerStorageType.Single:
                    return "[F4 {0}]".CheckedFormat(u.f32);
                case ContainerStorageType.Double:
                    return "[F8 {0}]".CheckedFormat(u.f64);
                case ContainerStorageType.String:
                    {
                        string s = o as string ?? string.Empty;

                        if (s.IsNullOrEmpty())
                            return "[A]";
                        else if (s.IsBasicAscii(basicUnquotedStringExcludeList, false))
                            return "[A {0}]".CheckedFormat(s);
                        else
                            return "[A \"{0}\"]".CheckedFormat(s.GenerateJSONVersion());
                    }
                case ContainerStorageType.DateTime:
                    return "[DateTime {0}]".CheckedFormat(u.DateTime.ToString("o"));
                case ContainerStorageType.TimeSpan:
                    return "[TimeSpan {0}]".CheckedFormat(u.TimeSpan.TotalSeconds);
                case ContainerStorageType.IListOfString:
                    {
                        IList<string> strList = (o as IList<string>) ?? emptyIListOfString;
                        if (strList.Count > 0)
                            return "[LS {0}]".CheckedFormat(String.Join(" ", (strList.Select(s => new ValueContainer(s).ToStringSML())).ToArray()));
                        else
                            return "[LS]";
                    }
            }

            if (o != null)
            {
                ValueContainer[] vcArray = (o as ValueContainer[]);
                if (cvt == ContainerStorageType.IListOfVC || vcArray != null)
                {
                    vcArray = vcArray ?? (GetValue<IList<ValueContainer>>(false) ?? emptyIListOfVC).ToArray();

                    if (vcArray.Length > 0)
                        return "[L {0}]".CheckedFormat(String.Join(" ", vcArray.Select((vc) => vc.ToStringSML()).ToArray()));
                    else
                        return "[L]";
                }

                if (o is INamedValueSet)
                {
                    return (o as INamedValueSet).ToStringSML();
                }

                if (o is INamedValue)
                {
                    return (o as INamedValue).ToStringSML();
                }
            }

            if (cvt == ContainerStorageType.Object)
                return "[{0} '{1}']".CheckedFormat(cvt, o);
            else
                return "[{0} '{1}']".CheckedFormat(cvt, ValueAsObject);
        }

        /// <summary>ToString variant that support SML like output format. (Currently the same as ToString)</summary>
        public string ToStringSML()
        {
            return ToString();
        }

        /// <summary>Constists of ' ', '"', '[' and ']'</summary>
        private static readonly List<char> basicUnquotedStringExcludeList = new List<char>() { ' ', '\"', '[', ']' };

        private static readonly IList<String> emptyIListOfString = new List<String>().AsReadOnly();
        private static readonly IList<ValueContainer> emptyIListOfVC = new List<ValueContainer>().AsReadOnly();
    }

    /// <summary>
    /// Enumeration that is used with the ValueContainer struct.
    /// <para/>None (0 : default), Object, String, IListOfString, Boolean, SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, UInt64, Single, Double, TimeSpan, DateTime
    /// </summary>
    public enum ContainerStorageType : int
    {
        /// <summary>Custom value for cases where the storage type has not been defined -(the default value: 0)</summary>
        None = 0,
        /// <summary>Use Object field</summary>
        Object,
        /// <summary>Use Object field as a String</summary>
        String,
        /// <summary>Use Object field as an IList{String} - usually contains a ReadOnlyCollection{String}</summary>
        IListOfString,
        /// <summary>Use Object field as an IList{ValueContainer} - usually contains a ReadOnlyCollection{ValueContainer}</summary>
        IListOfVC,
        /// <summary>Use Union.b field</summary>
        Boolean,
        /// <summary>Use Union.vi field</summary>
        Binary,
        /// <summary>Use Union.i8 field (signed)</summary>
        SByte,
        /// <summary>Use Union.i16 field</summary>
        Int16,
        /// <summary>Use Union.i32 field</summary>
        Int32,
        /// <summary>Use Union.i64 field</summary>
        Int64,
        /// <summary>Use Union.u8 field (unsigned)</summary>
        Byte,
        /// <summary>Use Union.u16 field</summary>
        UInt16,
        /// <summary>Use Union.u32 field</summary>
        UInt32,
        /// <summary>Use Union.u64 field</summary>
        UInt64,
        /// <summary>Use Union.f32 field</summary>
        Single,
        /// <summary>Use Union.f64 field</summary>
        Double,
        /// <summary>Uses Union.i64 field to contain the TimeSpan's tick value</summary>
        TimeSpan,
        /// <summary>Use Union.i64 field to contain the DateTime's serialized Binary value.</summary>
        DateTime,
    }

    /// <summary>Standard extension methods wrapper class/namespace</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given ContainerStorageType is a reference type.  (Currently Object, String, IListOfString)
        /// </summary>
        public static bool IsReferenceType(this ContainerStorageType cst)
        {
            switch (cst)
            {
                case ContainerStorageType.Object:
                case ContainerStorageType.String:
                case ContainerStorageType.IListOfString:
                case ContainerStorageType.IListOfVC:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true if the given ContainerStorageType is a value type (aka it is not a reference type and it is not None).
        /// </summary>
        public static bool IsValueType(this ContainerStorageType cst)
        {
            return !cst.IsReferenceType() && !cst.IsNone();
        }

        /// <summary>
        /// Returns true if the given ContainerStorageType is None.
        /// </summary>
        public static bool IsNone(this ContainerStorageType cst)
        {
            return (cst == ContainerStorageType.None);
        }

        /// <summary>
        /// Returns true if the given ContainerStorageType is String.
        /// </summary>
        public static bool IsString(this ContainerStorageType cst)
        {
            return (cst == ContainerStorageType.String);
        }

        /// <summary>
        /// Returns true if the given ContainerStorageType is a signed or unsigned integer.  Boolean is treated as an unsigned type.
        /// </summary>
        public static bool IsInteger(this ContainerStorageType cst, bool includeSigned, bool includeUnsigned)
        {
            switch (cst)
            {
                case ContainerStorageType.SByte:
                case ContainerStorageType.Int16:
                case ContainerStorageType.Int32:
                case ContainerStorageType.Int64: 
                    return includeSigned;
                case ContainerStorageType.Boolean:
                case ContainerStorageType.Binary:
                case ContainerStorageType.Byte:
                case ContainerStorageType.UInt16:
                case ContainerStorageType.UInt32:
                case ContainerStorageType.UInt64: 
                    return includeUnsigned;
                default: 
                    return false;
            }
        }

        /// <summary>
        /// Returns true if the given ContainerStorageType is Boolean.
        /// </summary>
        public static bool IsBoolean(this ContainerStorageType cst)
        {
            return (cst == ContainerStorageType.Boolean);
        }

        /// <summary>
        /// Returns true if the given ContainerStorageType is Double or Single.
        /// </summary>
        public static bool IsFloatingPoint(this ContainerStorageType cst)
        {
            switch (cst)
            {
                case ContainerStorageType.Double:
                case ContainerStorageType.Single:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Extension method version of static Utils.Fcns.Equals method for two lists of ValueContainers.
        /// Returns true if both lists have the same length and each of lhs' ValueContainer contents are IsEqualTo the corresponding rhs'
        /// </summary>
        public static bool IsEqualTo(this IList<ValueContainer> lhs, IList<ValueContainer> rhs)
        {
            if (Object.ReferenceEquals(lhs, rhs))
                return true;

            if (lhs == null || rhs == null || lhs.Count != rhs.Count)
                return false;

            int count = lhs.Count;
            for (int idx = 0; idx < count; idx++)
            {
                if (!lhs[idx].IsEqualTo(rhs[idx]))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Excpetion that may be thrown by the ValueContainer's GetValue method.  Message will give more details of the problem.
    /// </summary>
    public class ValueContainerGetValueException : System.Exception
    {
        /// <summary>
        /// Constructor requires message and innerException (which may be null)
        /// </summary>
        public ValueContainerGetValueException(string message, System.Exception innerException) 
            : base(message, innerException) 
        { }
    }

    #endregion

    #region ValueContainerEnvelope - DataContract envelope for ValueContainer

    /// <summary>
    /// DataContract compatible Envelope for ValueContainer objects.  
    /// Serializer and Deserializer attempt to efficiently transfer both the ContainerStorageType and the correspoinding value of the contained ValueContainer.
    /// </summary>
    /// <remarks>
    /// Please note the custom handling of the VC property allows this class to be used as the base class for DataContract objects where no class constructor is used when
    /// creating the deserialized version of an object and thus the fields are all set to the default(type) values without regard to any coded constructor behavior.
    /// </remarks>
    [DataContract(Name = "VC", Namespace = Constants.ModularNameSpace)]
    public class ValueContainerEnvelope
    {
        /// <summary>Default constructor.  By default the contained value will read as Empty until it has been explicitly set to some other value.</summary>
        public ValueContainerEnvelope() { }

        #region ValueContainer storage and construction default state handling that is compatible with reality of DataContract deserialization

        /// <summary>
        /// For serialized objects this property is initialized to the vc value given to the CreateEnvelopeFor method.
        /// For deserialized objects this property is intialized by the type specific DataMember in the envelope serializtion class
        /// The getter returns ValueContainer.None
        /// </summary>
        public ValueContainer VC 
        { 
            get 
            {
                if (vcPropertySetterHasBeenUsed)
                    return vc;
                else
                    return ValueContainer.Empty;
            }
            set 
            { 
                vc = value; 
                vcPropertySetterHasBeenUsed = true; 
            } 
        }

        /// <summary>
        /// Backing storage for the VC property.
        /// </summary>
        private ValueContainer vc;

        /// <summary>
        /// Boolean to indicate if the backing store as been set through the VC property.  
        /// This property allows the base class to determine if the VC should read as Empty (since it has not been set).
        /// </summary>
        private bool vcPropertySetterHasBeenUsed;

        #endregion

        #region serialization helper properties each as optional DataMembers

        // expectation is that exactly one property/element will produce a non-default value and this will define both the ContainerStorageType and the value for the deserializer to produce.
        // if no listed property produces a non-default value then the envelope will be empty and the deserializer will produce the default (empty) contained value.

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private bool Null { get { return vc.IsNullObject; } set { if (value) VC = ValueContainer.Null; }}

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private object o 
        { 
            get 
            {
                if (vc.cvt != ContainerStorageType.Object || vc.IsNull)
                    return null;

                ValueContainer[] vta = vc.o as ValueContainer[];

                if (vta != null)
                    return null;

                return vc.o;
            } 
            set 
            { 
                VC = new ValueContainer() { cvt = ContainerStorageType.Object, o = value }; 
            } 
        }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private String s { get { return (vc.cvt == ContainerStorageType.String ? (vc.o as String) : null); } set { VC = new ValueContainer() { cvt = ContainerStorageType.String, o = value }; } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private Details.sl sl { get { return ((vc.cvt == ContainerStorageType.IListOfString) ? new Details.sl(vc.o as IList<String>) : null); } set { VC = new ValueContainer() { cvt = ContainerStorageType.IListOfString, o = value.AsReadOnly() }; } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private bool? b { get { return ((vc.cvt == ContainerStorageType.Boolean) ? (bool?)vc.u.b : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Boolean, u = new ValueContainer.Union() { b = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private Byte ? bi { get { return ((vc.cvt == ContainerStorageType.Binary) ? (Byte?)vc.u.bi : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Binary, u = new ValueContainer.Union() { bi = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private Single? f32 { get { return ((vc.cvt == ContainerStorageType.Single) ? (Single?)vc.u.f32 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Single, u = new ValueContainer.Union() { f32 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private Double? f64 { get { return ((vc.cvt == ContainerStorageType.Double) ? (Double?)vc.u.f64 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Double, u = new ValueContainer.Union() { f64 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private SByte? i8 { get { return ((vc.cvt == ContainerStorageType.SByte) ? (SByte?)vc.u.i8 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.SByte, u = new ValueContainer.Union() { i8 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private Int16? i16 { get { return ((vc.cvt == ContainerStorageType.Int16) ? (Int16?)vc.u.i16 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Int16, u = new ValueContainer.Union() { i16 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private Int32? i32 { get { return ((vc.cvt == ContainerStorageType.Int32) ? (Int16?)vc.u.i32 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Int32, u = new ValueContainer.Union() { i32 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private Int64? i64 { get { return ((vc.cvt == ContainerStorageType.Int64) ? (Int16?)vc.u.i64 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Int64, u = new ValueContainer.Union() { i64 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private Byte? u8 { get { return ((vc.cvt == ContainerStorageType.Byte) ? (Byte?)vc.u.u8 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Byte, u = new ValueContainer.Union() { u8 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private UInt16? u16 { get { return ((vc.cvt == ContainerStorageType.UInt16) ? (UInt16?)vc.u.u16 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.UInt16, u = new ValueContainer.Union() { u16 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private UInt32? u32 { get { return ((vc.cvt == ContainerStorageType.UInt32) ? (UInt32?)vc.u.u32 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.UInt32, u = new ValueContainer.Union() { u32 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private UInt64? u64 { get { return ((vc.cvt == ContainerStorageType.UInt64) ? (UInt64?)vc.u.u64 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.UInt64, u = new ValueContainer.Union() { u64 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private TimeSpan? ts { get { return ((vc.cvt == ContainerStorageType.TimeSpan) ? (TimeSpan?)vc.u.TimeSpan : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.TimeSpan, u = new ValueContainer.Union() { TimeSpan = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private DateTime? dt { get { return ((vc.cvt == ContainerStorageType.DateTime) ? (DateTime?)vc.u.DateTime : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.DateTime, u = new ValueContainer.Union() { DateTime = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private ValueContainerEnvelope [] vca 
        { 
            get 
            {
                if (vc.cvt == ContainerStorageType.IListOfVC)
                {
                    IList<ValueContainer> vcList = vc.o as IList<ValueContainer>;

                    return (vcList != null) ? vcList.Select((vcItem) => new ValueContainerEnvelope() { VC = vcItem }).ToArray() : null;
                }
                else if (vc.cvt == ContainerStorageType.Object && vc.o != null)
                {
                    ValueContainer[] vcArray = vc.o as ValueContainer[];

                    return (vcArray != null) ? vcArray.Select((vcItem) => new ValueContainerEnvelope() { VC = vcItem }).ToArray() : null;
                }

                return null;
            } 
            set 
            {
                VC = new ValueContainer(new List<ValueContainer>(value.Select((ve) => ve.VC)).AsReadOnly() as IList<ValueContainer>);
            } 
        }

        #endregion

        /// <summary>Override ToString for logging and debugging.</summary>
        public override string ToString()
        {
            return VC.ToString();
        }
    }

    namespace Details
    {
        /// <summary>Internal - no documentation provided</summary>
        [CollectionDataContract(ItemName = "s", Namespace = Constants.ModularNameSpace)]
        internal class sl : List<String>
        {
            /// <summary>Internal - no documentation provided</summary>
            public sl() { }
            /// <summary>Internal - no documentation provided</summary>
            public sl(IList<String> es) : base(es) { }
        }
    }

    #endregion

    #region ReadOnly Named Values interfaces (INamedValueSet, INamedValue)

    /// <summary>
    /// This is the read-only interface to a NamedValueSet
    /// </summary>
    public interface INamedValueSet : IEnumerable<INamedValue>, IEquatable<INamedValueSet>
    {
        /// <summary>Returns true if the set contains a NamedValue for the given name (after sanitization)</summary>
        bool Contains(string name);

        /// <summary>
        /// If a NamedValue exists in the set for the given name (after sanitization), then this method returns the ValueContainer (VC) from that NamedValue.
        /// Otherwise this method returns ValueContainer.Empty.
        /// </summary>
        ValueContainer GetValue(string name);

        /// <summary>
        /// Gets the number of NamedValues that are currently in the set.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// String indexed property.  Returns the INamedValue from the set if it is found from the given name (after sanitization).  
        /// Otherwise returns a new empty INamedValue using the given name (set contents are not changed by this).
        /// </summary>
        INamedValue this[string name] { get; }

        /// <summary>
        /// If a NamedValue exists in the set for the given name (after sanitization), then this method returns the index into the set (in enumerable order) for the corresponding NamedValue.
        /// Otherwise this method returns -1.
        /// </summary>
        int IndexOf(string name);

        /// <summary>
        /// indexed property.  Returns the indexed INamedValue from the set (if the given index is valid).  
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">thrown if given index is less than 0 or it is greater than or equal to the set's Count</exception>
        INamedValue this[int index] { get; }

        /// <summary>This property returns true if the collection has been set to be read only.</summary>
        bool IsReadOnly { get; }

        /// <summary>Returns true if the this INamedValueSet has the same contents, in the same order, as the given rhs.</summary>
        bool IsEqualTo(INamedValueSet rhs);

        /// <summary>Custom ToString variant that allows the caller to determine if the ro/rw postfix should be included on thie string result, and to determine if NamedValues with empty VCs should be treated as a keyword.</summary>
        string ToString(bool includeROorRW, bool treatNameWithEmptyVCAsKeyword);

        /// <summary>ToString variant that support SML like output format.</summary>
        string ToStringSML();

        /// <summary>
        /// Returns the approximate size of the contents in bytes. 
        /// </summary>
        int EstimatedContentSizeInBytes { get; }
    }

    /// <summary>
    /// This is the read-only interface to a NamedValue
    /// </summary>
    public interface INamedValue : IEquatable<INamedValue>
    {
        /// <summary>This property gives the caller access to the name of the named value</summary>
        string Name { get; }

        /// <summary>This property gives access to the ValueContainer contained of this named value</summary>
        ValueContainer VC { get; }

        /// <summary>This property is used internally to support VC returning ValueContainer.Empty when the VC property has never been explicitly set.</summary>
        bool VCHasBeenSet { get; }

        /// <summary>This property returns true if the item has been set to be read only.</summary>
        bool IsReadOnly { get; }

        /// <summary>Returns true if the this INamedValue is equal to the given rhs.</summary>
        bool IsEqualTo(INamedValue rhs);

        /// <summary>
        /// Returns the approximate size of the contents in bytes. 
        /// </summary>
        int EstimatedContentSizeInBytes { get; }

        /// <summary>ToString variant that support SML like output format.</summary>
        string ToStringSML();
    }

    #endregion

    #region Named Values (NamedValueSet, NamedValue)

    /// <summary>
    /// This class defines objects each of which act as a Set of NamedValues.  
    /// This Set is internally represented by a List and may also be indexed using a Dictionary 
    /// This object includes a CollectionDataContractAttribute so that it may be serialized as part of other DataContract objects.  Supporting this attribute
    /// is based on the standard Add method signature and the support of the ICollection{NamedValue} interface.
    /// </summary>
    /// <remarks>
    /// Note: in order to support use of Dictionary type indexing on the name, all Names are santized by replacing null with String.Empty
    /// before being used as a kay.  As such the set can only contain one NamedValue with its Name set to null or Empty.
    /// </remarks>
    /// <remarks>
    /// Note: the mechanism used to access and construct the underlying List is designed to support seamless operation with DataContract deserialization where
    /// Initial constructed object contents do not make use of any default constructor and instead essentally zero-fill the object as if it were a struct rather than
    /// being a class.
    /// </remarks>
    [CollectionDataContract(ItemName = "nvi", Namespace = Constants.ModularNameSpace)]
    public class NamedValueSet : ICollection<NamedValue>, INamedValueSet
    {
        #region Empty constant

        /// <summary>Returns a readonly empty NamedValueSet</summary>
        public static NamedValueSet Empty { get { return empty; } }
        private static readonly NamedValueSet empty = new NamedValueSet() { IsReadOnly = true };

        #endregion

        #region Contructors

        /// <summary>Default constructor.  Creates an empty writable NamedValueSet</summary>
        public NamedValueSet() 
        { }

        /// <summary>
        /// Copy constructor from IEnumerable{NamedValues}.  
        /// Creates a new NamedValueList as a set of copies of the given list of NamedValues.  Treats the null rhs as an empty set.
        /// Produces a fully read/write set even if copying from a fully readonly rhs or copying from an rhs that includes readonly NamedValue items.
        /// </summary>
        public NamedValueSet(IEnumerable<INamedValue> rhsSet) 
            : this(rhsSet, false) 
        { }

        /// <summary>
        /// Copy constructor from IEnumerable{NamedValues}.  
        /// Creates a new NamedValueList from the given list of NamedValues by cloning each of them.  Treats the null rhs as an empty set.
        /// if asReadOnly is false then this produces a fully read/write set even if copying from a fully readonly rhs or copying from an rhs that includes readonly NamedValue items.
        /// if asReadonly is true then this produces a fully IsReadOnly set which can reference any already readonly items from the given rhs set.
        /// </summary>
        public NamedValueSet(IEnumerable<INamedValue> rhsSet, bool asReadOnly)
        {
            if (rhsSet != null)
            {
                if (!asReadOnly)
                {
                    foreach (NamedValue rhsItem in rhsSet)
                        list.Add(new NamedValue(rhsItem));
                }
                else
                {
                    foreach (NamedValue rhsItem in rhsSet)
                        list.Add(rhsItem.ConvertToReadOnly());

                    isReadOnly = true;      // we do not need to iterate through the contents again as we just filled it with readonly items.
                }
            }
        }

        #endregion

        #region Locally defined helper utility methods and properties

        /// <summary>
        /// Provide an alternate name for the SetValue method.  This allows the class to be used with a Dictionary style initializer to add values to the set.
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        /// </summary>
        public NamedValueSet Add(string name, object value)
        {
            ThrowIfIsReadOnly("The Add method");

            return SetValue(name, value);
        }

        /// <summary>
        /// Attemps to "Add" each of the non-null INamedValue items in the given range to this set.
        /// Internally uses SetValue which replaces identically named values with the last given value.
        /// Returns this object to support call chaining.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet AddRange(IEnumerable<INamedValue> range)
        {
            ThrowIfIsReadOnly("The AddRange method");

            if (range != null)
            {
                foreach (INamedValue nvItem in range)
                {
                    if (nvItem != null)
                        SetValue(nvItem.Name, nvItem.VC);
                }
            }

            return this;
        }

        /// <summary>
        /// Explicit ValueContainer getter.  Returns the ValueContainer contained in the named NamedValue.  Returns ValueContainer.Empty if the given name is not found in this list.
        /// </summary>
        public ValueContainer GetValue(string name)
        {
            NamedValue nv = AttemptToFindItemInList(name.Sanitize(), true);

            return ((nv != null) ? nv.VC : ValueContainer.Empty);
        }

        /// <summary>
        /// Explicit ValueContainer setter.  Updates the desired NamedValue to contain the given vc value, or adds a new NamedValue, initialized from the given name and vc value, to the list if it was not already present.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet SetValue(string name, ValueContainer vc)
        {
            ThrowIfIsReadOnly("The SetValue method");

            string sanitizedName = name.Sanitize();
            int index = AttemptToFindItemIndexInList(sanitizedName, true);

            if (index >= 0)
            {
                // we found a matching NamedValue in the list

                NamedValue nv = list[index];
                if (!nv.IsReadOnly)
                {
                    // update the value for the NamedValue item that is already in the set
                    nv.VC = vc;
                }
                else
                {
                    // replace the NamedValue with a new one with the same name and the new value (dictionary does not need to be reset)
                    list[index] = new NamedValue(sanitizedName) { VC = vc };
                }
            }
            else
            {
                // name is not already in the set - make a new NamedValue with the given santized name and vc value and add it to the list.
                nameToIndexDictionary = null;
                list.Add(new NamedValue(sanitizedName) { VC = vc });
            }
            
            return this;
        }

        /// <summary>
        /// Explicit ValueContainer setter.  Updates the desired NamedValue to contain the given object value or adds a new NamedValue, initialized from the given name and object value, to the list if it was not already present.
        /// In either case the given object value will be assigned into a ValueContainer automatically using this signature.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet SetValue(string name, object value)
        {
            return SetValue(name, new ValueContainer(value));
        }

        /// <summary>
        /// Asks the underlying SortedList to Remove the given name (after sanitization) from the set.
        /// Returns true if the given sanitized name was found and removed from the set.  Returns false otherwise.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public bool Remove(string name)
        {
            ThrowIfIsReadOnly("The Remove name method");

            string sanitizedName = name.Sanitize();

            int index = AttemptToFindItemIndexInList(sanitizedName, false);     // do not create a dictionary for this effort since we are just going to delete it anyway

            if (index >= 0)
            {
                nameToIndexDictionary = null;
                list.RemoveAt(index);

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Removes the element at the given indexed element.  
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">thrown if given index is less than 0 or it is greater than or equal to the set's Count</exception>
        public void RemoveAt(int index)
        {
            ThrowIfIsReadOnly("The RemoveAt method");

            if (index < 0 || index >= Count)
                throw new System.ArgumentOutOfRangeException("index", "the given index is less than 0 or it is greater than or equal to the set's Count");

            nameToIndexDictionary = null;
            list.RemoveAt(index);
        }

        /// <summary>
        /// get/set string indexed operator give caller access to a NamedValue for the given indexed name.
        /// getter returns the indicated NamedValue, if found in this list, or returns an empty readonly NamedValue if the given name was not found in the list (returned value is not added to the list in this case)
        /// setter requires that the given value is non-null and that its Name is equal to the string index given to the setter (see exceptions below).  Once accepted, this setter sets the indicated NamedValue to contain the ValueContainer from the given value NamedValue, or the setter adds the given NamedValue to the list if it was not already present in the list.
        /// </summary>
        /// <param name="name">Gives the name index to get or set a NamedValue from/to</param>
        /// <returns>The indicated NamedValue or an empty NamedValue with the given name if the name was not found in this list</returns>
        /// <exception cref="System.ArgumentNullException">This exception is thrown by the setter if the given value is null.</exception>
        /// <exception cref="System.ArgumentException">This exception is thrown by the setter if the given value.Name propery is not equal to the string index.</exception>
        public NamedValue this[string name]
        {
            get
            {
                string sanitizedName = name.Sanitize();

                NamedValue nv = AttemptToFindItemInList(sanitizedName, true);

                return nv ?? NamedValue.Empty;
            }
            set
            {
                ThrowIfIsReadOnly("The String indexed setter property");

                if (value == null)
                    throw new System.ArgumentNullException("value");

                string sanitizedName = name.Sanitize();

                if (sanitizedName != value.Name.Sanitize())
                    throw new System.ArgumentException("value.Name must match index string for string Indexed Setter.");

                SetValue(sanitizedName, value.VC);
            }
        }

        /// <summary>
        /// indexed property.  Returns the indexed NamedValue from the set (if the given index is valid).  
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">thrown if given index is less than 0 or it is greater than or equal to the set's Count</exception>
        public NamedValue this[int index]
        {
            get 
            {
                if (index < 0 || index >= Count)
                    throw new System.ArgumentOutOfRangeException("index", "the given index is less than 0 or it is greater than or equal to the set's Count");

                return list[index];
            }
        }

        #endregion

        #region INamedValueSet (leftovers - most of the other members and properties are found in other regions of this class)

        /// <summary>Returns true if the set contains a NamedValue for the given name (after sanitization)</summary>
        public bool Contains(string name)
        {
            string sanitizedName = name.Sanitize();

            return (AttemptToFindItemIndexInList(sanitizedName, true) >= 0);
        }

        INamedValue INamedValueSet.this[string name]
        {
            get { return this[name]; }
        }

        /// <summary>
        /// If a NamedValue exists in the set for the given name (after sanitization), then this method returns the index into the set (in enumerable order) for the corresponding NamedValue.
        /// Otherwise this method returns -1.
        /// </summary>
        public int IndexOf(string name)
        {
            string sanitizedName = name.Sanitize();

            return AttemptToFindItemIndexInList(sanitizedName, true);
        }

        /// <summary>
        /// indexed property.  Returns the indexed INamedValue from the set (if the given index is valid).  
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">thrown if given index is less than 0 or it is greater than or equal to the set's Count</exception>
        INamedValue INamedValueSet.this[int index] 
        { 
            get { return this[index]; } 
        }

        /// <summary>Returns true if the this INamedValueSet has the same contents, in the same order, as the given rhs.</summary>
        public bool IsEqualTo(INamedValueSet rhs)
        {
            if (rhs == null || Count != rhs.Count || IsReadOnly != rhs.IsReadOnly)
                return false;

            if (System.Object.ReferenceEquals(this, rhs))
                return true;

            int setCount = Count;
            for (int index = 0; index < setCount; index++)
            {
                if (!list[index].IsEqualTo(rhs[index]))
                    return false;
            }

            return true;
        }

        /// <summary>Returns true if the this INamedValueSet has the same contents, in the same order, as the given other NVS.</summary>
        public bool Equals(INamedValueSet other)
        {
            return IsEqualTo(other);
        }

        /// <summary>
        /// This method is required to support the fact that INamedValueSet implements IEnumerable{INamedValue} and the standard enumerators already implemented by the underling SortedList
        /// cannot be directly casted to this interface.
        /// </summary>
        IEnumerator<INamedValue> IEnumerable<INamedValue>.GetEnumerator()
        {
            return list.Select((nv) => nv as INamedValue).GetEnumerator();
        }

        #endregion

        #region CollectionDataContract support and related ICollection<NamedValue>, IEnumerable<INamedValue>

        /// <summary>
        /// Gets the number of NamedValues that are currently in the set.
        /// </summary>
        public int Count
        {
            get { return list.Count; }
        }

        /// <summary>
        /// Returns true if the set contains the given NamedValue instance.
        /// </summary>
        /// <remarks>This is essentially a useless method.  It is provied to that the class can implement ICollection{INamedValue}</remarks>
        public bool Contains(NamedValue nv)
        {
            return list.Contains(nv);
        }

        /// <summary>
        /// Copies the elements of the System.Collections.Generic.ICollection{T} to an System.Array, starting at a particular System.Array index.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional System.Array that is the destination of the elements copied from System.Collections.Generic.ICollection{T}. 
        /// The System.Array must have zero-based indexing.
        /// </param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        /// <exception cref="System.ArgumentNullException">thrown if the given array is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">thrown if the given arrayIndex is less than zero.</exception>
        /// <exception cref="System.ArgumentException">
        /// thrown if the given array is not one-dimensional, or arrayIndex is greater than the length of the array, 
        /// or the collection does not fit into the array at and after the given arrayIndex.
        /// </exception>
        public void CopyTo(NamedValue[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Asks the underlying SortedList to Add the given item using its sanitized Name as the key.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">thrown if the given item is null.</exception>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        /// <exception cref="System.ArgumentException">thrown if an element with the same sanitized item.Name already exists in this set.</exception>
        public void Add(NamedValue item)
        {
            ThrowIfIsReadOnly("The Add method");

            if (item == null)
                throw new System.ArgumentNullException("item");

            string sanitizedName = item.Name.Sanitize();

            if (AttemptToFindItemIndexInList(sanitizedName, false) >= 0)
                throw new System.ArgumentException("An element with the same sanitized item.Name already exists in this set.");

            nameToIndexDictionary = null;
            list.Add(item);
        }

        /// <summary>
        /// Asks the underlying SortedList to Remove the given name (after sanitization) from the set if both the name and value match the corresponding named item in the set.
        /// Returns true if the given sanitized name was found and removed from the set.  Returns false otherwise.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        /// <remarks>This is essentially a useless method.  It is provied to that the class can implement ICollection{INamedValue}</remarks>
        public bool Remove(NamedValue nv)
        {
            ThrowIfIsReadOnly("The Remove NamedValue method");

            nameToIndexDictionary = null;
            return list.Remove(nv);
        }

        /// <summary>
        /// Clears the underlying collection.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        void ICollection<NamedValue>.Clear()
        {
            Clear();
        }

        /// <summary>
        /// Clears the underlying collection.  Supports call chaining
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet Clear()
        {
            ThrowIfIsReadOnly("The Clear method");

            nameToIndexDictionary = null;
            list.Clear();

            return this;
        }

        /// <summary>
        /// Returns the corresponding Dict.Values's enumerator.
        /// Required to support IEnumerable{NamedValue} interface.
        /// </summary>
        public IEnumerator<NamedValue> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        /// <summary>
        /// Returns the corresponding Dict.Values's enumerator.
        /// Required to support IEnumerable{NamedValue} interface.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        #endregion 

        #region IsReadOnly support

        /// <summary>
        /// This method can be used on a newly created NamedValueSet to set its IsReadOnly property to true while also supporting call chaining.
        /// </summary>
        /// <remarks>Use the ConvertToReadOnly extension method to convert INamedValueSet objects to be ReadOnly.</remarks>
        public NamedValueSet MakeReadOnly()
        {
            if (!IsReadOnly)
                IsReadOnly = true;

            return this;
        }

        /// <summary>
        /// getter returns true if the collection has been set to be read only.
        /// if setter is given true then it sets the collection to be read only and sets all of the contained NamedValues to read only.
        /// setter only accepts being given false if the collection has not already been set to read only.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the setter is given false and collection has already been set to IsReadOnly</exception>
        public bool IsReadOnly
        {
            get { return isReadOnly; }
            set
            {
                if (value && !isReadOnly)
                {
                    isReadOnly = true;

                    int setCount = Count;
                    for (int idx = 0; idx < setCount; idx++)
                    {
                        list[idx].IsReadOnly = true;
                    }
                }
                else if (!value && isReadOnly)
                {
                    ThrowIfIsReadOnly("Setting the IsReadOnly property to false");
                }
            }
        }

        /// <summary>
        /// This method checks if the collection IsReadOnly and then throws a NotSupportedException if it is.
        /// The exception message is the given reasonPrefix + " is not supported when this object IsReadOnly property has been set to true"
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        private void ThrowIfIsReadOnly(string reasonPrefix)
        {
            if (IsReadOnly)
                throw new System.NotSupportedException(reasonPrefix + " is not supported when this object IsReadOnly property has been set to true");
        }

        private bool isReadOnly;

        #endregion

        #region Equality support

        /// <summary>Support object level Equality testing versions.</summary>
        public override bool Equals(object rhsAsObject)
        {
            return IsEqualTo(rhsAsObject as INamedValueSet);
        }

        /// <summary>Override GetHashCode because Equals has been.</summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion

        #region ToString (variants), ToStringSML, and EstimatedContentSizeInBytes

        /// <summary>
        /// Provide local debug/logging assistance version of this method
        /// </summary>
        public override string ToString()
        {
            return ToString(true, false);
        }

        /// <summary>Custom ToString variant that allows the caller to determine if the ro/rw postfix should be included on thie string result and if each NV with an empty VC should be treated like a keyword.</summary>
        public string ToString(bool includeROorRW, bool treatNameWithEmptyVCAsKeyword)
        {
            return "[{0}]{1}".CheckedFormat(String.Join(",", list.Select((nv) => nv.ToString(includeROorRW, treatNameWithEmptyVCAsKeyword)).ToArray()), (includeROorRW ? (IsReadOnly ? "ro" : "rw") : String.Empty));
        }

        /// <summary>ToString variant that support SML like output format.</summary>
        public string ToStringSML()
        {
            return "[L {0}]".CheckedFormat(String.Join(" ", list.Select((nv) => nv.ToStringSML()).ToArray()));
        }

        /// <summary>Returns the approximate size of the contents in bytes.</summary>
        public int EstimatedContentSizeInBytes
        {
            get 
            {
                int numItems = Count;
                int totalApproximateSize = 0;

                for (int idx = 0; idx < numItems; idx++)
                {
                    totalApproximateSize += list[idx].EstimatedContentSizeInBytes;
                }

                return totalApproximateSize;
            }
        }

        #endregion

        #region Underlying storage and indexing fields

        /// <summary>This is the actual list of NamedValue objects that are contained in this set.</summary>
        List<NamedValue> list = new List<NamedValue>();

        /// <summary>
        /// Return the NamedValue instance in the list who's Name matches the given sanitizedName, or null if there is none.
        /// </summary>
        private NamedValue AttemptToFindItemInList(string sanitizedName, bool createDictionaryIfNeeded)
        {
            int index = AttemptToFindItemIndexInList(sanitizedName, createDictionaryIfNeeded);

            return ((index >= 0) ? list[index] : null);
        }

        /// <summary>
        /// Return the index of the NamedValue in the list who's Name matches the given sanitizedName, or -1 if there is none.  
        /// </summary>
        private int AttemptToFindItemIndexInList(string sanitizedName, bool createDictionaryIfNeeded)
        {
            if (nameToIndexDictionary == null && Count >= minElementsToUseDicationary && createDictionaryIfNeeded)
                GenerateDicationary();

            if (nameToIndexDictionary != null)
            {
                int itemIndex;

                if (nameToIndexDictionary.TryGetValue(sanitizedName, out itemIndex))
                    return itemIndex;
                else
                    return -1;
            }
            else
            {
                int setCount = Count;
                for (int idx = 0; idx < setCount; idx++)
                {
                    if (list[idx].Name == sanitizedName)
                        return idx;
                }

                return -1;
            }
        }

        /// <summary>This dictionary is only used to optimize access to sets that have at least 10 elements</summary>
        volatile Dictionary<string, int> nameToIndexDictionary = null;

        /// <summary>Defines the minimum Set size required to decide to create the nameToIndexDictionary</summary>
        const int minElementsToUseDicationary = 10;

        /// <summary>
        /// Generates a Dictionary that can be used to index from a given sanitized name to the index of the corresponding NamedValue in the list.
        /// </summary>
        private void GenerateDicationary()
        {
            Dictionary<string, int> d = new Dictionary<string, int>();

            int setCount = Count;
            for (int idx = 0; idx < setCount; idx++)
                d[list[idx].Name] = idx;

            nameToIndexDictionary = d;
        }

        #endregion
    }

    /// <summary>
    /// This object defines a single named value.  A named value is a pairing of a name and a ValueContainer.
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public class NamedValue : INamedValue
    {
        #region Empty constant

        /// <summary>Returns a readonly empty NamedValueSet</summary>
        public static NamedValue Empty { get { return empty; } }
        private static readonly NamedValue empty = new NamedValue(string.Empty) { IsReadOnly = true };

        #endregion

        #region Constructors

        /// <summary>Constructor - builds NamedValue with the given name.  VC defaults to ValueContainer.Empty</summary>
        public NamedValue(string name) 
        {
            Name = name;
        }

        /// <summary>Helper Constructor - builds NamedValue with the given name and object value.  VC is constructed to contain the given object value</summary>
        public NamedValue(string name, object value)
        {
            Name = name;
            VC = new ValueContainer(value);
        }

        /// <summary>
        /// Copy constructor.  builds a copy of the given rhs using its Name and a copy of its VC.
        /// The results of this copy operation are read/write even if the given rhs is readonly.
        /// </summary>
        public NamedValue(INamedValue rhs)
            : this(rhs.Name)
        {
            if (rhs.IsReadOnly)
                vc = rhs.VC;
            else
                vc.DeepCopyFrom(rhs.VC);

            VCHasBeenSet = rhs.VCHasBeenSet;
        }

        /// <summary>
        /// Copy constructor.  builds a copy of the given rhs containing copy of Name and VC properties.
        /// The IsReadOnly on resulting copy is set from asReadOnly.  
        /// In addition if asReadonly is true and the given rhs is not readonly and its contained value is an IListOfString and the copy's contained value is set to a expliclty
        /// copy of the given rhs.  This stop cannot be peformed for general contained values that have opaque Object values in them and it not required for String contents (already immutable)
        /// and for all other value content types which do not naturaly share references.
        /// </summary>
        public NamedValue(INamedValue rhs, bool asReadOnly)
            :this(rhs.Name)
        {
            if (asReadOnly && !rhs.IsReadOnly)
                vc.DeepCopyFrom(rhs.VC);
            else
                vc = rhs.VC;

            VCHasBeenSet = rhs.VCHasBeenSet;
            isReadOnly = asReadOnly;
        }

        #endregion

        #region payload properties, serialzation and corresponiding proxy properties for managing serialization

        /// <summary>This property gives the caller read-only access to the name of the named value</summary>
        [DataMember(Order = 10)]
        public string Name { get; internal set; }

        /// <summary>This get/set property gives access to the ValueContainer contained of this named value</summary>
        public ValueContainer VC 
        {
            get 
            { 
                return (VCHasBeenSet ? vc : ValueContainer.Empty); 
            }
            set 
            {
                ThrowIfIsReadOnly("VC property setter");
                vc = value;
                VCHasBeenSet = true; 
            }
        }
        private ValueContainer vc;

        /// <summary>
        /// Returns true if the VC property setter has been explicitly called.  Returns false otherwise.  
        /// Allows VC property getter to return ValueContainer.Empty (non-default value) until the VC property has been explicitly set.
        /// </summary>
        public bool VCHasBeenSet { get; private set; }

        /// <summary>This private property is the version that is used to serialize/deserialize the contained ValueContainer</summary>
        [DataMember(Order = 20)]
        private ValueContainerEnvelope Env 
        { 
            get 
            { 
                return new ValueContainerEnvelope() { VC = VC }; 
            } 
            set 
            {
                ThrowIfIsReadOnly("Env property setter");
                VC = value.VC; 
            }
        }

        #endregion

        #region Equality support

        /// <summary>
        /// Returns true if the given rhs has the same Name, VC contents and IsReadOnly value as this object.
        /// </summary>
        public bool IsEqualTo(INamedValue rhs)
        {
            return (rhs != null 
                    && Name == rhs.Name 
                    && VC.IsEqualTo(rhs.VC) 
                    && IsReadOnly == rhs.IsReadOnly
                    );
        }

        /// <summary>
        /// Returns true if the given other NV has the same Name, VC contents and IsReadOnly value as this object.
        /// </summary>
        public bool Equals(INamedValue other)
        {
            return IsEqualTo(other);
        }

        /// <summary>Support object level Equality testing versions.</summary>
        public override bool Equals(object rhsAsObject)
        {
            return IsEqualTo(rhsAsObject as INamedValue);
        }

        /// <summary>Override GetHashCode because Equals has been.</summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion

        #region IsReadOnly support

        /// <summary>
        /// getter returns true if the item has been set to be read only.
        /// if setter is given true then it sets the item to be read only.
        /// setter only accepts being given false if the item has not already been set to read only.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the setter is given false and item has already been set to IsReadOnly</exception>
        public bool IsReadOnly
        {
            get { return isReadOnly; }
            set
            {
                if (value && !isReadOnly)
                    isReadOnly = true;
                else if (!value && isReadOnly)
                    ThrowIfIsReadOnly("Setting the IsReadOnly property to false");
            }
        }

        /// <summary>
        /// This method checks if the item IsReadOnly and then throws a NotSupportedException if it is.
        /// The exception message is the given reasonPrefix + " is not supported when this object IsReadOnly property has been set"
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the item has been set to IsReadOnly</exception>
        private void ThrowIfIsReadOnly(string reasonPrefix)
        {
            if (IsReadOnly)
                throw new System.NotSupportedException(reasonPrefix + " is not supported when this object IsReadOnly property has been set");
        }

        private bool isReadOnly;

        #endregion

        #region ToString (variants), ToStringSML, and EstimatedContentSizeInBytes

        /// <summary>
        /// Custom ToString variant that allows the caller to determine if the ro/rw postfix should be included on thie string result and if NV's with an empty VC should be treated like a keyword (by only returning the name)
        /// </summary>
        public string ToString(bool useDoubleEqualsForRW, bool treatNameWithEmptyVCAsKeyword)
        {
            if (VC.IsEmpty && treatNameWithEmptyVCAsKeyword)
                return "{0}".CheckedFormat(Name);
            else if (!useDoubleEqualsForRW || IsReadOnly)
                return "{0}={1}".CheckedFormat(Name, VC);
            else
                return "{0}=={1}".CheckedFormat(Name, VC);
        }

        /// <summary>
        /// Provide local debug/logging assistance version of this method.
        /// </summary>
        public override string ToString()
        {
            return ToString(true, false);
        }

        /// <summary>ToString variant that support SML like output format.</summary>
        public string ToStringSML()
        {
            return "[L {0} {1}]".CheckedFormat((new ValueContainer(Name)).ToStringSML(), VC.ToStringSML());
        }

        /// <summary>
        /// Returns the approximate size of the contents in bytes. 
        /// </summary>
        public int EstimatedContentSizeInBytes 
        {
            get { return (Name.Length * sizeof(char)) + VC.EstimatedContentSizeInBytes; }
        }

        #endregion
    }

    #endregion

    #region NamedValue related extension methods

    /// <summary>Standard extension methods wrapper class/namespace</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Sanitizes the given name for use as a Dictionary key by replacing any given null value with string.Empty.
        /// </summary>
        public static string Sanitize(this string name)
        {
            return name ?? string.Empty;
        }

        /// <summary>
        /// Converts the given INamedValueSet iNvSet to a readonly NamedValueSet, either by casting or by copying.
        /// If the given iNvSet value is null then this method return null.
        /// If the given iNvSet value IsReadOnly and its type is actually a NamedValueSet then this method returns the given iNvSet down casted as a NamedValueSet (path used for serialziation)
        /// Otherwise this method returns a new readonly NamedValueSet created as a sufficiently deep clone of the given iNvSet.
        /// </summary>
        public static NamedValueSet ConvertToReadOnly(this INamedValueSet iNvSet)
        {
            if (iNvSet == null)
                return null;

            // special case where we downcast readonly NamedValueSets (passed as INamedValueSets) so that we do not need to copy them after they are already readonly.
            if (iNvSet.IsReadOnly && (iNvSet is NamedValueSet))
                return iNvSet as NamedValueSet;

            return new NamedValueSet(iNvSet, true);
        }

        /// <summary>
        /// Converts the given INamedValue iNv to a readonly NamedValue, either by casting or by copying.
        /// If the given iNv value IsReadOnly and its type is actually a NamedValue then this method returns the given iNv down casted as a NamedValue (path used for serialziation)
        /// Otherwise this method returns a new readonly NamedValue this is a copy of the given inv.
        /// </summary>
        public static NamedValue ConvertToReadOnly(this INamedValue iNv)
        {
            if (iNv.IsReadOnly && (iNv is NamedValue))
                return iNv as NamedValue;

            return new NamedValue(iNv, true);
        }

        /// <summary>
        /// Converts the given NamedValueSet nvSet to a read/write NamedValueSet by cloning if needed.
        /// If the given nvSet value is not null and it is !IsReadonly then return the given value.
        /// Otherwise this method constructs and returns a new readwrite NamedValueSet copy the given nvSet.
        /// </summary>
        public static NamedValueSet ConvertToWriteable(this NamedValueSet nvSet)
        {
            if (nvSet != null && !nvSet.IsReadOnly)
                return nvSet;

            return new NamedValueSet(nvSet, false);
        }

        /// <summary>
        /// Converts the given NamedValue nv to a read/write NamedValue by cloning if needed.
        /// If the given nv !IsReadonly then this method returns it unchanged.
        /// Otherwise this method constructs and returns a new readonly NamedValue copy of the given nv.
        /// </summary>
        public static NamedValue ConvertToWriteable(this NamedValue nv)
        {
            if (!nv.IsReadOnly)
                return nv;

            return new NamedValue(nv, false);
        }

        /// <summary>
        /// Returns true if the given iNvSet is either null or it refers to a NamedValueSet that is currently empty.
        /// </summary>
        public static bool IsNullOrEmpty(this INamedValueSet iNvSet)
        {
            return (iNvSet == null || iNvSet.Count == 0);
        }

        /// <summary>
        /// Returns true if the given iNv is either null or it refers to a NamedValue that is currently empty (Name IsNullOrEmpty and VC IsEmpty)
        /// </summary>
        public static bool IsNullOrEmpty(this INamedValue iNv)
        {
            return (iNv == null || (iNv.Name.IsNullOrEmpty() && iNv.VC.IsEmpty));
        }

        /// <summary>
        /// passes the given iNvSet through as the return value unless it is a non-null, empty set in which case this method returns null.
        /// </summary>
        public static INamedValueSet MapEmptyToNull(this INamedValueSet iNvSet)
        {
            bool isEmptySet = (iNvSet != null && iNvSet.Count == 0);
            return (isEmptySet ? null : iNvSet);
        }

        /// <summary>
        /// passes the given iNvSet through as the return value unless it is a null, in which case this method returns NamedValueSet.Empty.
        /// </summary>
        public static INamedValueSet MapNullToEmpty(this INamedValueSet iNvSet)
        {
            return (iNvSet ?? NamedValueSet.Empty);
        }

        /// <summary>
        /// This method operates on the given lhs NamedValueSet and uses AddAndUpdate merge behavior to merge the contents of the rhs into the lhs
        /// <para/>If the given lhs IsReadonly then a new NamedValueSet created as a copy of the contents of the given lhs and this new one is used in its place and is returned.
        /// <para/>This method supports call chaining by returning the lhs after any modification have been made.
        /// </summary>
        /// <param name="lhs">Gives the object that the rhs NV items will be merged into</param>
        /// <param name="rhs">Gives the object that contains the NV items that will be merged into the lhs and which may be used to update corresonding items in the lhs</param>
        public static NamedValueSet MergeWith(this NamedValueSet lhs, INamedValueSet rhs)
        {
            return lhs.MergeWith(rhs, NamedValueMergeBehavior.AddAndUpdate);
        }

        /// <summary>
        /// This method operates on the given lhs NamedValueSet and uses the given mergeBehavior to merge the contents of the given rhs into the lhs.
        /// <para/>If the given lhs IsReadonly then a new NamedValueSet created as a copy of the contents of the given lhs and this new one is used in its place and is returned.
        /// <para/>This method supports call chaining by returning the lhs after any modification have been made.
        /// </summary>
        /// <param name="lhs">Gives the object that the rhs NV items will be merged into</param>
        /// <param name="rhs">Gives the enumerable object that defines the NV items that will be merged into the lhs and which may be used to update corresonding items in the lhs</param>
        /// <param name="mergeBehavior">Defines the merge behavior that will be used for this merge when the rhs and lhs contain NV items with the same name but different values.  Defaults to NamedValueMergeBehavior.AddAndUpdate</param>
        public static NamedValueSet MergeWith(this NamedValueSet lhs, IEnumerable<INamedValue> rhs, NamedValueMergeBehavior mergeBehavior)
        {
            bool add = mergeBehavior.IsAddSelected();
            bool update = mergeBehavior.IsUpdateSelected();

            if (lhs == null || lhs.IsReadOnly)
                lhs = lhs.ConvertToWriteable();

            if (rhs != null)
            {
                foreach (INamedValue rhsItem in rhs)
                {
                    bool lhsContainsRhsName = !lhs.Contains(rhsItem.Name);
                    if (lhsContainsRhsName ? add : update)
                        lhs.SetValue(rhsItem.Name, rhsItem.VC);
                }
            }

            return lhs;
        }

        /// <summary>Returns true if the given mergeBehavior value has the AddNewItems flag set.</summary>
        public static bool IsAddSelected(this NamedValueMergeBehavior mergeBehavior)
        {
            return ((mergeBehavior & NamedValueMergeBehavior.AddNewItems) != NamedValueMergeBehavior.None);
        }

        /// <summary>Returns true if the given mergeBehavior value has the UpdateExsitingItems flag set.</summary>
        public static bool IsUpdateSelected(this NamedValueMergeBehavior mergeBehavior)
        {
            return ((mergeBehavior & NamedValueMergeBehavior.UpdateExistingItems) != NamedValueMergeBehavior.None);
        }
    }

    [Flags]
    public enum NamedValueMergeBehavior
    {
        None = 0,
        /// <summary>Merge by adding new items from the rhs into the lhs only if the lhs does not already contains an element with the same name.</summary>
        AddNewItems = 1,
        /// <summary>Merge by updating only each item in the lhs that is also in the rhs by replacing the lhs item's value the corresponding rhs item's value.</summary>
        UpdateExistingItems = 2,

        /// <summary>Shorthand for AddNewItems</summary>
        AddOnly = AddNewItems,
        /// <summary>Shorthand for UpdateExistingItems</summary>
        UpdateOnly = UpdateExistingItems,
        /// <summary>Shorthand for AddNewItems | UpdateExistingItems</summary>
        AddAndUpdate = AddNewItems | UpdateExistingItems,
    }

    #endregion
}
