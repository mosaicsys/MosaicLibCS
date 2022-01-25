//-------------------------------------------------------------------
/*! @file Common.cs
 *  @brief This file defines common definitions that are used by Modular pattern implementations
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
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
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;

using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Semi.E005.Data;
using System.Runtime.CompilerServices;

namespace MosaicLib.Modular.Common
{
    #region Basic types

    /// <summary>Define a Null type that can be used as a ParamType or a ResultType in ParamType and ResultType types in the IAction related pattern types.</summary>
    public struct NullObj { }

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

    #region ValueContainer - contains all value type specific logic (ContainerStorageType, VC related ExtensionMethods, ValueContainerGetValueException)

    /// <summary>
    /// This is a value type storage object for a set of well known types, especially value types.  
    /// It is used to provide a formal box to be placed around such value types so that they can be passed from place to place in the code using a generic container while using copy symantics rather than the more traditional object box/unbox based reference symantics.
    /// This object supports a full range of basic value object types and it supports use with a limited set of well known reference objects, to which it can safely apply techniques so that the actual stored value can safely be stored and passed using a read-only representation to support full thread safety.
    /// This object can also be used to reference and pass arbitrary object types by casting them to the System.Object type, although this is not the preferred purpose for this object.
    /// This ojects is used by, and supports use with, a large and growing number of the other modular types in this library, especially to support generic forms of interconnection, serialization, and attribute harvested value set adapters.
    /// This type is used in place of the traditional object boxing and unboxing solution to avoid the generation and propagation of small short lifetime memory objects when passing such underlying value types from place to place in a more generic manner.
    /// </summary>
    [Serializable]
    public struct ValueContainer : IEquatable<ValueContainer>
    {
        #region non-default "Constructor"

        /// <summary>
        /// Custom value constructor.  Constructs the object and then assigns its initial value as ValueAsObject = given initialValueAsObject.
        /// <para/>ValueAsObject setter extracts the type information from the given object and then attempts to decode
        /// and use the corresponding type specific value storage type, if it is one of the supported types.  
        /// If the extracted type is not one of the supported container types then the given value is simply stored as the given System.Object.
        /// </summary>
        public ValueContainer(System.Object initialValueAsObject)
            : this()
        {
            SetFromObject(initialValueAsObject);
        }

        #endregion

        #region instance storage fields (cvt, o, u)

        /// <summary>A quick encoding on the field/union field in which the value has been placed.  (Type had been called ContainerValueType)</summary>
        public ContainerStorageType cvt;

        /// <summary>Reference to data types that are not supported by the Union</summary>
        public Object o;

        /// <summary>Union is used for storage of known simple data types.</summary>
        public Union u;

        #endregion

        #region value storage Union type (explicit layout struct with all fields using FieldOffset(0))

        /// <summary>The Union type allows for various types to be supported using fields that only use one 8 byte block for storage.</summary>
        [StructLayout(LayoutKind.Explicit, Pack = 8), Serializable]
        public struct Union : IEquatable<Union>
        {
            /// <summary>Boolean value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.Boolean b;
            /// <summary>"Binary" Byte value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.Byte bi;
            /// <summary>SByte value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.SByte i8;
            /// <summary>Int16 value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.Int16 i16;
            /// <summary>Int32 value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.Int32 i32;
            /// <summary>Int64 value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.Int64 i64;
            /// <summary>Byte value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.Byte u8;
            /// <summary>UInt16 value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.UInt16 u16;
            /// <summary>UInt32 value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.UInt32 u32;
            /// <summary>UInt64 value in union</summary>
            [FieldOffset(0)]
            public System.UInt64 u64;
            /// <summary>Single value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.Single f32;
            /// <summary>Double value in union</summary>
            [FieldOffset(0), NonSerialized]
            public System.Double f64;

            /// <summary>Helper property to read/write a TimeSpan - internally gets/saves the i64 TickCount in the TimeSpan</summary>
            public TimeSpan TimeSpan { get { return TimeSpan.FromTicks(i64); } set { i64 = value.Ticks; } }

            /// <summary>Helper property to read/write a DateTime - internally gets/saves the i64 Binary representation in the DateTime</summary>
            public DateTime DateTime { get { return DateTime.FromBinary(i64); } set { i64 = value.ToBinary(); } }

            /// <summary>IEquatable{Union} implementation method.  Returns true if both unions contain the same binary content value (based on u64 field - large enough and has simple and safe basic equality meaning)</summary>
            public bool Equals(Union other)
            {
                return (u64 == other.u64);
            }

            /// <summary>
            /// Compare the contents of this instance to the rhs by comparing the contents of the largest fields that are in both.  For this purpose we are using u64.
            /// </summary>
            public bool IsEqualTo(Union other)
            {
                return this.Equals(other);
            }
        }

        #endregion

        #region predicate properties

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

        #endregion

        #region static "constant value" properties

        /// <summary>
        /// Returns an ValueContainer with cvt set to ContainerStorageType.Object and o set to Null
        /// </summary>
        public static ValueContainer Null { get { return _null; } }
        private static readonly ValueContainer _null = default(ValueContainer).SetToNullObject();

        /// <summary>
        /// Returns an ValueContainer with cvt set to ContainerStorageType.None
        /// </summary>
        public static ValueContainer Empty { get { return _empty; } }
        private static readonly ValueContainer _empty = default(ValueContainer).SetToEmpty();

        #endregion

        #region Direct content updaters (SetToNullObject, SetToEmpty, CopyFrom, DeepCopyFrom)

        /// <summary>
        /// Sets the container to contain the null object: cvt = ContainerStorageType.Object (0), o = null, u = default(Union)
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
        /// Sets the container to be empty: cvt = ContainerStorageType.None, o = null, u = default(Union)
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

                        IList<string> rhsILS = rhs.GetValueLS(rethrow: false);

                        if (rhsILS == null || rhsILS is ReadOnlyIList<string>)
                        {
                            // if the rhs value is null, or is readonly then just assign it to this object and we are done
                            o = rhsILS;
                        }
                        else
                        {
                            // otherwise create a full readonly copy of the given rhsILS and save that for later use
                            o = new ReadOnlyIList<String>(rhsILS);
                        }

                        return this;
                    }

                case ContainerStorageType.IListOfVC:
                    {
                        cvt = ContainerStorageType.IListOfVC;
                        u = rhs.u;

                        IList<ValueContainer> rhsILVC = rhs.GetValueL(rethrow: false);

                        if (rhsILVC == null || rhsILVC is ReadOnlyIList<ValueContainer>)
                        {
                            // if the rhs value is null, or is readonly then just assign it to this object and we are done
                            o = rhsILVC;
                        }
                        else
                        {
                            // otherwise create a full readonly deep copy of the given rhsILVC and save that for later use
                            o = new ReadOnlyIList<ValueContainer>(rhsILVC.Select(vc => ValueContainer.Empty.DeepCopyFrom(vc)));
                        }

                        return this;
                    }

                case ContainerStorageType.INamedValueSet:
                    {
                        cvt = ContainerStorageType.INamedValueSet;
                        u = rhs.u;

                        o = (rhs.o as INamedValueSet).ConvertToReadOnly();

                        return this;
                    }

                case ContainerStorageType.INamedValue:
                    {
                        cvt = ContainerStorageType.INamedValue;
                        u = rhs.u;

                        o = (rhs.o as INamedValue).ConvertToReadOnly();

                        return this;
                    }

                case ContainerStorageType.Object:
                    {
                        cvt = ContainerStorageType.Object;
                        u = rhs.u;

                        if (rhs.o == null)
                            o = null;
                        else if (rhs.o is ICloneable)
                            o = ((ICloneable)rhs.o).Clone();
                        else
                            o = rhs.o;

                        return this;
                    }

                default:
                    return CopyFrom(rhs);
            }
        }

        #endregion

        #region Type decoding helper static methods (DecodeType variants and related readonly static field "type constants")

        /// <summary>
        /// Accepts a given <typeparamref name="TValueType"/> and attempts to generate an apporpriate ContainerStorageType (and isNullable) value as the best container storage type to use with the given <typeparamref name="TValueType"/>.
        /// Unrecognized type default as ContainerStorageType.Object.
        /// </summary>
        /// <remarks>This method will be deprecated in the future as its use has been replaced by the use of an appropriate variant of the static GetDecodedTypeInfo method</remarks>
        public static void DecodeType<TValueType>(out ContainerStorageType decodedValueType, out bool isNullable)
        {
            DecodedTypeInfo dti = StaticTypeDecoder<TValueType>.dti;
            decodedValueType = dti.cst;
            isNullable = dti.isNullable;
        }

        /// <summary>
        /// Accepts a given <paramref name="type"/> and attempts to generate an apporpriate ContainerStorageType (and isNullable) value as the best container storage type to use with the given Type.
        /// Unrecognized type default as ContainerStorageType.Object.
        /// </summary>
        /// <remarks>This method will be deprecated in the future as its use has been replaced by the use of an appropriate variant of the static GetDecodedTypeInfo method</remarks>
        public static void DecodeType(Type type, out ContainerStorageType decodedValueType, out bool isNullable)
        {
            DecodedTypeInfo dti = GetDecodedTypeInfo(type);
            decodedValueType = dti.cst;
            isNullable = dti.isNullable;
        }

        /// <summary>Private class used to optimize type decoding for templatized types.  Use of this class ensures that the type lookup logic will only be run once per unique type of <typeparamref name="TValueType"/></summary>
        public static class StaticTypeDecoder<TValueType>
        {
            public static DecodedTypeInfo dti = ValueContainer.GetDecodedTypeInfo(typeof(TValueType));
        }

        /// <summary>
        /// Accepts a given <typeparamref name="TValueType"/> and attempts to generate an apporpriate ContainerStorageType (and isNullable) value as the best container storage type to use with the given Type.
        /// Unrecognized type default as ContainerStorageType.Object.
        /// </summary>
        public static DecodedTypeInfo GetDecodedTypeInfo<TValueType>()
        {
            return DecodedTypeInfo.GetDecodedTypeInfo<TValueType>();
        }

        /// <summary>
        /// Accepts a given <paramref name="type"/> and attempts to generate an apporpriate ContainerStorageType (and isNullable) value as the best container storage type to use with the given Type.
        /// Unrecognized type default as ContainerStorageType.Object.
        /// </summary>
        public static DecodedTypeInfo GetDecodedTypeInfo(Type type)
        {
            return DecodedTypeInfo.GetDecodedTypeInfo(type);
        }

        private static readonly Dictionary<Type, DecodedTypeInfo> decodedTypeInfoDictionary = GenerateDecodedTypeDictionary();

        private static Dictionary<Type, DecodedTypeInfo> GenerateDecodedTypeDictionary()
        {
            TypeAndDecodedTypeInfo[] decodeTypeAndInfoArray = new[]
            {
                #region string

                new TypeAndDecodedTypeInfo(type: typeof(string), cst: ContainerStorageType.String),

                #endregion

                #region IListOfString: string, string [], IList<string>, List<string>, ReadOnlyCollection<string>, ReadOnlyIList<string>

                new TypeAndDecodedTypeInfo(type: typeof(string []), cst: ContainerStorageType.IListOfString),
                new TypeAndDecodedTypeInfo(type: typeof(IList<string>), cst: ContainerStorageType.IListOfString),
                new TypeAndDecodedTypeInfo(type: typeof(List<string>), cst: ContainerStorageType.IListOfString),
                new TypeAndDecodedTypeInfo(type: typeof(ReadOnlyCollection<string>), cst: ContainerStorageType.IListOfString),
                new TypeAndDecodedTypeInfo(type: typeof(Utils.Collections.ReadOnlyIList<string>), cst: ContainerStorageType.IListOfString),

                #endregion

                #region IListOfVC: ValueContainer [], IList<ValueContainer>, List<ValueContainer>, ReadOnlyCollection<ValueContainer>, ReadOnlyIList<ValueContainer>

                new TypeAndDecodedTypeInfo(type: typeof(ValueContainer []), cst: ContainerStorageType.IListOfVC),
                new TypeAndDecodedTypeInfo(type: typeof(IList<ValueContainer>), cst: ContainerStorageType.IListOfVC),
                new TypeAndDecodedTypeInfo(type: typeof(List<ValueContainer>), cst: ContainerStorageType.IListOfVC),
                new TypeAndDecodedTypeInfo(type: typeof(ReadOnlyCollection<ValueContainer>), cst: ContainerStorageType.IListOfVC),
                new TypeAndDecodedTypeInfo(type: typeof(Utils.Collections.ReadOnlyIList<ValueContainer>), cst: ContainerStorageType.IListOfVC),

                #endregion

                #region NamedValueSet, INamedValueSet, NamedValue, INamedValue

                new TypeAndDecodedTypeInfo(type: typeof(NamedValueSet), cst: ContainerStorageType.INamedValueSet),
                new TypeAndDecodedTypeInfo(type: typeof(INamedValueSet), cst: ContainerStorageType.INamedValueSet),
                new TypeAndDecodedTypeInfo(type: typeof(NamedValue), cst: ContainerStorageType.INamedValue),
                new TypeAndDecodedTypeInfo(type: typeof(INamedValue), cst: ContainerStorageType.INamedValue),

                #endregion

                #region LogGate (nullable and not)

                new TypeAndDecodedTypeInfo(type: typeof(Logging.LogGate), cst: ContainerStorageType.Custom),
                new TypeAndDecodedTypeInfo(type: typeof(Logging.LogGate ?), cst: ContainerStorageType.Custom, isNullable: false),    // NOTE: this is a special case.   Even though the type is nullable, since we are using the Custom type we do not indicate that the type isNullable

                #endregion

                #region Bo, I1 .. I8, U1 .. U8, F4 .. F8, TimeSpan, DateTime

                new TypeAndDecodedTypeInfo(type: typeof(bool), cst: ContainerStorageType.Bo ),

                new TypeAndDecodedTypeInfo(type: typeof(sbyte), cst: ContainerStorageType.I1 ),
                new TypeAndDecodedTypeInfo(type: typeof(short), cst: ContainerStorageType.I2 ),
                new TypeAndDecodedTypeInfo(type: typeof(int), cst: ContainerStorageType.I4 ),
                new TypeAndDecodedTypeInfo(type: typeof(long), cst: ContainerStorageType.I8 ),

                new TypeAndDecodedTypeInfo(type: typeof(byte), cst: ContainerStorageType.U1 ),
                new TypeAndDecodedTypeInfo(type: typeof(ushort), cst: ContainerStorageType.U2 ),
                new TypeAndDecodedTypeInfo(type: typeof(uint), cst: ContainerStorageType.U4 ),
                new TypeAndDecodedTypeInfo(type: typeof(ulong), cst: ContainerStorageType.U8 ),

                new TypeAndDecodedTypeInfo(type: typeof(float), cst: ContainerStorageType.F4 ),
                new TypeAndDecodedTypeInfo(type: typeof(double), cst: ContainerStorageType.F8 ),

                new TypeAndDecodedTypeInfo(type: typeof(TimeSpan), cst: ContainerStorageType.TimeSpan ),
                new TypeAndDecodedTypeInfo(type: typeof(DateTime), cst: ContainerStorageType.DateTime ),

                #endregion

                #region nullable Bo, I1 .. I8, U1 .. U8, F4 .. F8, TimeSpan, DateTime

                new TypeAndDecodedTypeInfo(type: typeof(bool ?), cst: ContainerStorageType.Bo, isNullable: true ),

                new TypeAndDecodedTypeInfo(type: typeof(sbyte ?), cst: ContainerStorageType.I1, isNullable: true ),
                new TypeAndDecodedTypeInfo(type: typeof(short ?), cst: ContainerStorageType.I2, isNullable: true ),
                new TypeAndDecodedTypeInfo(type: typeof(int ?), cst: ContainerStorageType.I4, isNullable: true ),
                new TypeAndDecodedTypeInfo(type: typeof(long ?), cst: ContainerStorageType.I8, isNullable: true ),

                new TypeAndDecodedTypeInfo(type: typeof(byte ?), cst: ContainerStorageType.U1, isNullable: true ),
                new TypeAndDecodedTypeInfo(type: typeof(ushort ?), cst: ContainerStorageType.U2, isNullable: true ),
                new TypeAndDecodedTypeInfo(type: typeof(uint ?), cst: ContainerStorageType.U4, isNullable: true ),
                new TypeAndDecodedTypeInfo(type: typeof(ulong ?), cst: ContainerStorageType.U8, isNullable: true ),

                new TypeAndDecodedTypeInfo(type: typeof(float ?), cst: ContainerStorageType.F4, isNullable: true ),
                new TypeAndDecodedTypeInfo(type: typeof(double ?), cst: ContainerStorageType.F8, isNullable: true ),

                new TypeAndDecodedTypeInfo(type: typeof(TimeSpan ?), cst: ContainerStorageType.TimeSpan, isNullable: true ),
                new TypeAndDecodedTypeInfo(type: typeof(DateTime ?), cst: ContainerStorageType.DateTime, isNullable: true ),

                #endregion
            };

            Dictionary<Type, DecodedTypeInfo> dictionary = new Dictionary<Type, DecodedTypeInfo>();

            foreach (var dtai in decodeTypeAndInfoArray)
                dictionary[dtai.type] = dtai.dti;

            return dictionary;
        }

        private struct TypeAndDecodedTypeInfo
        {
            public TypeAndDecodedTypeInfo(Type type, ContainerStorageType cst, bool isNullable = false)
                : this()
            {
                this.type = type ?? typeof(object);
                dti.cst = cst;
                dti.isNullable = isNullable;
            }
            public Type type;
            public DecodedTypeInfo dti;

            public override string ToString()
            {
                return "Type:{0} {1}".CheckedFormat(type, dti);
            }
        }

        /// <summary>
        /// Gives the ContainerStorageType (cst) and the isNullable value for the type from which this object was generated.
        /// </summary>
        public struct DecodedTypeInfo
        {
            public Type givenType, valueType;
            public ContainerStorageType cst;
            public bool isNullable;
            public bool isEnum;

            public override string ToString()
            {
                return "{0}{1} cst:{2}".CheckedFormat(isNullable ? "Nullable " : "", valueType, cst);
            }

            /// <summary>
            /// Accepts a given <typeparamref name="TValueType"/> and attempts to generate an apporpriate ContainerStorageType (and isNullable) value as the best container storage type to use with the given Type.
            /// Unrecognized type default as ContainerStorageType.Object.
            /// </summary>
            public static DecodedTypeInfo GetDecodedTypeInfo<TValueType>()
            {
                return GetDecodedTypeInfo(typeof(TValueType));
            }

            /// <summary>
            /// Accepts a given <paramref name="type"/> and attempts to generate an apporpriate ContainerStorageType (and isNullable) value as the best container storage type to use with the given Type.
            /// Unrecognized type default as ContainerStorageType.Object.
            /// </summary>
            public static DecodedTypeInfo GetDecodedTypeInfo(Type type)
            {
                DecodedTypeInfo dti;
                if (decodedTypeInfoDictionary.TryGetValue(type, out dti))
                    return dti;

                dti.givenType = type;
                dti.valueType = type;

                dti.isNullable = (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(System.Nullable<>)));

                // for nullable types extract the underlying type.
                if (dti.isNullable)
                    dti.valueType = Nullable.GetUnderlyingType(type);

                var valueType = dti.valueType;

                // all well known value types types are only reachable using the dictionary used above.  The following list is only used to support types that cannot easily be pre-known (and thus added to the dictionary)
                if (iListOfStringType.IsAssignableFrom(valueType) || stringEnumerableType.IsAssignableFrom(valueType))
                {
                    dti.isNullable = false;
                    dti.cst = ContainerStorageType.IListOfString;
                }
                else if (iListOfVCType.IsAssignableFrom(valueType) || vcEnumerableType.IsAssignableFrom(valueType))
                {
                    dti.isNullable = false;
                    dti.cst = ContainerStorageType.IListOfVC;
                }
                else if (iNamedValueSetType.IsAssignableFrom(valueType))
                {
                    dti.isNullable = false;
                    dti.cst = ContainerStorageType.INamedValueSet;
                }
                else if (iNamedValueType.IsAssignableFrom(valueType))
                {
                    dti.isNullable = false;
                    dti.cst = ContainerStorageType.INamedValue;
                }
                else if (valueType.IsEnum)
                {
                    dti.isEnum = true;
                    // leave isNullable from above
                    dti.cst = ContainerStorageType.String;
                }
                else
                {
                    // enable nullable use with ContainerStorageType.Object to support arbitrary boxed value representation (for boxed structs)
                    dti.cst = ContainerStorageType.Object;
                }

                return dti;
            }
        }

        private static readonly Type stringType = typeof(System.String);
        private static readonly Type stringArrayType = typeof(System.String []);
        private static readonly Type stringEnumerableType = typeof(IEnumerable<string>);
        private static readonly Type vcArrayType = typeof(ValueContainer[]);
        private static readonly Type iListOfStringType = typeof(IList<System.String>);
        private static readonly Type iListOfVCType = typeof(IList<ValueContainer>);
        private static readonly Type listOfStringType = typeof(List<System.String>);
        private static readonly Type listOfVCType = typeof(List<ValueContainer>);
        private static readonly Type readOnlyCollectionOfStringType = typeof(ReadOnlyCollection<string>);
        private static readonly Type readOnlyCollectionOfVCType = typeof(ReadOnlyCollection<ValueContainer>);
        private static readonly Type iNamedValueSetType = typeof(INamedValueSet);
        private static readonly Type iNamedValueType = typeof(INamedValue);
        private static readonly Type vcEnumerableType = typeof(IEnumerable<ValueContainer>);

        #endregion

        #region ValueAsObject get/set property

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
                    case ContainerStorageType.Custom: return null;
                    default: return null;
                    case ContainerStorageType.Object: return o;
                    case ContainerStorageType.String: return o;
                    case ContainerStorageType.IListOfString: return o;
                    case ContainerStorageType.IListOfVC: return o;
                    case ContainerStorageType.INamedValueSet: return o;
                    case ContainerStorageType.INamedValue: return o;
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
                SetFromObject(value);
            }
        }

        #endregion

        #region Create, CreateFromObject static ValueContainer factory methods

        /// <summary>
        /// Static ValueContainer creation (factory) method.  Accepts object of any type and returns a ValueContainer instance which contains the <paramref name="value"/> of the object.  
        /// Internally uses SetFromObject to attempt to extract the type for supported and supported boxed contents and to set the ValueContainers ContainerStorageType cvt accordingly.
        /// <para/>Note use alternate SetValue&lt;System.Object&gt;(value) to force the resulting ValueContainer's cvt to be ContainerStorageType.Object and to contain the unmodified object from <paramref name="value"/>.
        /// </summary>
        public static ValueContainer CreateFromObject(System.Object value)
        {
            return default(ValueContainer).SetFromObject(value);
        }

        /// <summary>
        /// Static ValueContainer creation (factory) method.  Accepts <paramref name="value"/> of given (or implied) <typeparamref name="TValueType"/> and returns a ValueContainer instance with contents derived from both.
        /// Internally uses SetValue&lt;TValueType&gt;(value) which actually decodes the ContainerStorageType from the given TValueType and then sets the corresponding storage field from value.
        /// </summary>
        public static ValueContainer Create<TValueType>(TValueType value)
        {
            DecodedTypeInfo dti = StaticTypeDecoder<TValueType>.dti;
            return default(ValueContainer).SetValue<TValueType>(value, dti.cst, dti.isNullable);
        }

        /// <summary>
        /// Static ValueContainer creation (factory method).  Accepts <paramref name="value"/> of given (or implied) <typeparamref name="TValueType"/> and returns a ValueContainer instance with contents derived from both.
        /// Internally uses SetValue&lt;TValueType&gt;(value, decodedValuetype, ) which actually decodes the ContainerStorageType from the given TValueType and then sets the corresponding storage field from value.
        /// This method stores the given value in the desired container storage field.  If the given value does not fit in the indicated
        /// container then this method will attempt to convert the given value to an object and store it as such.
        /// </summary>
        public static ValueContainer Create<TValueType>(TValueType value, ContainerStorageType decodedValueType, bool isNullable = false)
        {
            return default(ValueContainer).SetValue<TValueType>(value, decodedValueType, isNullable);
        }

        /// <summary>
        /// Static ValueContainer creation (factory) method used to generate a ValueContainer to contain a list of the given items.  
        /// This method will produce an IListOfStrings if ItemType is string and otherwise it will produce an IListOfVC
        /// If the given itemParamArray is null or is empty then this method will return an Empty ValueContainer
        /// If only one value is given in the itemParamArray then the returned ValueContainer will simply be a ValueContainer containing that Item.
        /// </summary>
        public static ValueContainer CreateFromItems<ItemType>(params ItemType[] itemParamArray)
        {
            if (itemParamArray.IsNullOrEmpty())
                return ValueContainer.Empty;
            else if (itemParamArray.Length == 1)
                return ValueContainer.Create<ItemType>(itemParamArray[0]);
            else if (typeof(ItemType) == typeof(string))
                return ValueContainer.Create<IList<string>>(new ReadOnlyIList<string>(itemParamArray.Select(item => item as string)), ContainerStorageType.IListOfString);
            else if (typeof(ItemType) == typeof(ValueContainer))
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemParamArray.Select(item => (ValueContainer)((System.Object) item))), ContainerStorageType.IListOfVC);
            else if (typeof(ItemType) == typeof(System.Object))
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemParamArray.Select(item => ValueContainer.CreateFromObject(item))), ContainerStorageType.IListOfVC);
            else
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemParamArray.Select(item => ValueContainer.Create(item))), ContainerStorageType.IListOfVC);
        }

        /// <summary>
        /// Static ValueContainer creation (factory) method used to generate a ValueContainer to contain a set of the given items.  
        /// This method will produce an IListOfStrings if ItemType is string and otherwise it will produce an IListOfVC
        /// If the given itemSet is null then this method will return an Empty ValueContainer.
        /// </summary>
        public static ValueContainer CreateFromSet<ItemType>(IEnumerable<ItemType> itemSet)
        {
            if (itemSet == null)
                return ValueContainer.Empty;
            else if (typeof(ItemType) == typeof(string))
                return ValueContainer.Create<IList<string>>(new ReadOnlyIList<string>(itemSet.Select(item => item as string)), ContainerStorageType.IListOfString);
            else if (typeof(ItemType) == typeof(ValueContainer))
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemSet.Select(item => (ValueContainer)((System.Object)item))), ContainerStorageType.IListOfVC);
            else if (typeof(ItemType) == typeof(System.Object))
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemSet.Select(item => ValueContainer.CreateFromObject(item))), ContainerStorageType.IListOfVC);
            else
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemSet.Select(item => ValueContainer.Create(item))), ContainerStorageType.IListOfVC);
        }

        /// <summary>
        /// Static ValueContainer creation (factory) method used to generate a ValueContainer to contain a set of the given items.  
        /// This method will produce an IListOfStrings if ItemType is string and otherwise it will produce an IListOfVC
        /// If the given itemSet is null then this method will return an Empty ValueContainer.
        /// </summary>
        public static ValueContainer CreateFromCollection<ItemType>(ICollection<ItemType> itemCollection)
        {
            if (itemCollection == null)
                return ValueContainer.Empty;
            else if (typeof(ItemType) == typeof(string))
                return ValueContainer.Create<IList<string>>(new ReadOnlyIList<string>(itemCollection.Select(item => item as string)), ContainerStorageType.IListOfString);
            else if (typeof(ItemType) == typeof(ValueContainer))
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemCollection.Select(item => (ValueContainer)((System.Object)item))), ContainerStorageType.IListOfVC);
            else if (typeof(ItemType) == typeof(System.Object))
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemCollection.Select(item => ValueContainer.CreateFromObject(item))), ContainerStorageType.IListOfVC);
            else
                return ValueContainer.Create<IList<ValueContainer>>(new ReadOnlyIList<ValueContainer>(itemCollection.Select(item => ValueContainer.Create(item))), ContainerStorageType.IListOfVC);
        }

        #endregion

        #region SetFromObject - underlying System.Object type extracting setter method

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
            if (value != null)
            {
                if (value is ValueContainer)
                {
                    CopyFrom((ValueContainer)value);
                }
                else
                {
                    DecodedTypeInfo dti = GetDecodedTypeInfo(value.GetType());

                    SetValue<System.Object>(value, dti.cst, dti.isNullable);
                }
            }
            else
            {
                SetToNullObject();
            }

            return this;
        }

        #endregion

        #region Type specifc GetValue, SetValue, and Create variants (GetValueYY, SetValueYY, CreateYY for YY in Bo, Bi, I1, I2, I4, I8, U1, U2, U4, U8, F4, F8)

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public bool GetValueBo(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.Bo: return u.b;
                default: return GetValue<bool>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public byte GetValueBi(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.Bo: return (byte)u.b.MapToInt();
                default: return GetValue<byte>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public sbyte GetValueI1(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.I1: return u.i8;
                case ContainerStorageType.Bo: return (sbyte) u.b.MapToInt();
                default: return GetValue<sbyte>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public short GetValueI2(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.I2: return u.i16;
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.I1: return u.i8;
                case ContainerStorageType.Bo: return (short) u.b.MapToInt();
                default: return GetValue<short>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public int GetValueI4(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.I4: return u.i32;
                case ContainerStorageType.U2: return u.u16;
                case ContainerStorageType.I2: return u.i16;
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.I1: return u.i8;
                case ContainerStorageType.Bo: return u.b.MapToInt();
                default: return GetValue<int>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public long GetValueI8(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.I8: return u.i64;
                case ContainerStorageType.U4: return u.u32;
                case ContainerStorageType.I4: return u.i32;
                case ContainerStorageType.U2: return u.u16;
                case ContainerStorageType.I2: return u.i16;
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.I1: return u.i8;
                case ContainerStorageType.Bo: return u.b.MapToInt();
                default: return GetValue<long>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public byte GetValueU1(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.Bo: return (byte)u.b.MapToInt();
                default: return GetValue<byte>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public ushort GetValueU2(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.U2: return u.u16;
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.Bo: return (ushort)u.b.MapToInt();
                default: return GetValue<ushort>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public uint GetValueU4(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.U4: return u.u32;
                case ContainerStorageType.U2: return u.u16;
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.Bo: return (uint)u.b.MapToInt();
                default: return GetValue<uint>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public ulong GetValueU8(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.U8: return u.u64;
                case ContainerStorageType.U4: return u.u32;
                case ContainerStorageType.U2: return u.u16;
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.Bo: return (uint) u.b.MapToInt();
                default: return GetValue<ulong>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public float GetValueF4(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.F4: return u.f32;
                case ContainerStorageType.U2: return u.u16;
                case ContainerStorageType.I2: return u.i16;
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.I1: return u.i8;
                case ContainerStorageType.Bo: return u.b.MapToInt();
                default: return GetValue<float>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public double GetValueF8(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.F8: return u.f64;
                case ContainerStorageType.F4: return u.f32;
                case ContainerStorageType.U4: return u.u32;
                case ContainerStorageType.I4: return u.i32;
                case ContainerStorageType.U2: return u.u16;
                case ContainerStorageType.I2: return u.i16;
                case ContainerStorageType.U1: return u.u8;
                case ContainerStorageType.Bi: return u.bi;
                case ContainerStorageType.I1: return u.i8;
                case ContainerStorageType.Bo: return u.b.MapToInt();
                default: return GetValue<double>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueBo(bool value) { cvt = ContainerStorageType.Bo; o = null; u = new Union() { b = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueBi(byte value) { cvt = ContainerStorageType.Bi; o = null; u = new Union() { bi = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueI1(sbyte value) { cvt = ContainerStorageType.I1; o = null; u = new Union() { i8 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueI2(short value) { cvt = ContainerStorageType.I2; o = null; u = new Union() { i16 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueI4(int value) { cvt = ContainerStorageType.I4; o = null; u = new Union() { i32 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueI8(long value) { cvt = ContainerStorageType.I8; o = null; u = new Union() { i64 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueU1(byte value) { cvt = ContainerStorageType.U1; o = null; u = new Union() { u8 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueU2(ushort value) { cvt = ContainerStorageType.U2; o = null; u = new Union() { u16 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueU4(uint value) { cvt = ContainerStorageType.U4; o = null; u = new Union() { u32 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueU8(ulong value) { cvt = ContainerStorageType.U8; o = null; u = new Union() { u64 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueF4(float value) { cvt = ContainerStorageType.F4; o = null; u = new Union() { f32 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueF8(double value) { cvt = ContainerStorageType.F8; o = null; u = new Union() { f64 = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateBo(bool value) { return new ValueContainer() { cvt = ContainerStorageType.Bo, u = new Union() { b = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateBi(byte value) { return new ValueContainer() { cvt = ContainerStorageType.Bi, u = new Union() { bi = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateI1(sbyte value) { return new ValueContainer() { cvt = ContainerStorageType.I1, u = new Union() { i8 = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateI2(short value) { return new ValueContainer() { cvt = ContainerStorageType.I2, u = new Union() { i16 = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateI4(int value) { return new ValueContainer() { cvt = ContainerStorageType.I4, u = new Union() { i32 = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateI8(long value) { return new ValueContainer() { cvt = ContainerStorageType.I8, u = new Union() { i64 = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateU1(byte value) { return new ValueContainer() { cvt = ContainerStorageType.U1, u = new Union() { u8 = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateU2(ushort value) { return new ValueContainer() { cvt = ContainerStorageType.U2, u = new Union() { u16 = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateU4(uint value) { return new ValueContainer() { cvt = ContainerStorageType.U4, u = new Union() { u32 = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateU8(ulong value) { return new ValueContainer() { cvt = ContainerStorageType.U8, u = new Union() { u64 = value } }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateF4(float value) { return new ValueContainer() { cvt = ContainerStorageType.F4, u = new Union() { f32 = value } }; }

        /// <summary>Creates and returns a ValueContainer that contains the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateF8(double value) { return new ValueContainer() { cvt = ContainerStorageType.F8, u = new Union() { f64 = value } }; }

        #endregion

        #region Additional type specific GetValueYY, SetValueYY, and CreateYY variants (Object, A, TimeSpan, DateTime, LS, L, NVS, NV)

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public object GetValueObject(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.Object: return o;
                default: return GetValue<object>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public string GetValueA(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.A: return (o as string);
                default: return GetValue<string>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public TimeSpan GetValueTS(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.TS: return u.TimeSpan;
                case ContainerStorageType.F8: return u.f64.FromSeconds();
                default: return GetValue<TimeSpan>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public DateTime GetValueDT(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.DT: return u.DateTime;
                default: return GetValue<DateTime>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public ReadOnlyIList<string> GetValueLS(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.LS: return o as ReadOnlyIList<string>;
                default: return GetValue<ReadOnlyIList<string>>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public ReadOnlyIList<ValueContainer> GetValueL(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.L: return o as ReadOnlyIList<ValueContainer>;
                default: return GetValue<ReadOnlyIList<ValueContainer>>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public INamedValueSet GetValueNVS(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.NVS: return o as INamedValueSet;
                default: return GetValue<INamedValueSet>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>
        /// Gets the contents of this ValueContainer as the indicated type.  
        /// If the contents matches the implicit type, or any directly convertable type, then it directly returns the related contents. 
        /// Otherwise it returns the resulting using the generic GetValue{}(rethrow, allowAllTypeChangeAttempts) variant which handles supported type conversions.
        /// </summary>
        public INamedValue GetValueNV(bool rethrow = true, bool allowAllTypeChangeAttempts = true)
        {
            switch (cvt)
            {
                case ContainerStorageType.NV: return o as INamedValue;
                default: return GetValue<INamedValue>(rethrow: rethrow, allowTypeChangeAttempt: allowAllTypeChangeAttempts);
            }
        }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueA(string value) { cvt = ContainerStorageType.A; o = value; u = default(Union); }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueTS(TimeSpan value) { cvt = ContainerStorageType.TS; o = null; u = new Union() { TimeSpan = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueDT(DateTime value) { cvt = ContainerStorageType.DT; o = null; u = new Union() { DateTime = value }; }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueLS(IList<string> value, bool mapNullToEmpty = true) { cvt = ContainerStorageType.LS; o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty); u = default(Union); }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="set"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueLS(IEnumerable<string> set, bool mapNullToEmpty = true) { cvt = ContainerStorageType.LS; o = set.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty); u = default(Union); }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueL(IList<ValueContainer> value, bool mapNullToEmpty = true) { cvt = ContainerStorageType.L; o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty); u = default(Union); }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="set"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueL(IEnumerable<ValueContainer> set, bool mapNullToEmpty = true) { cvt = ContainerStorageType.L; o = set.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty); u = default(Union); }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueNVS(INamedValueSet value, bool mapNullToEmpty = true) { cvt = ContainerStorageType.NVS; o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty); u = default(Union); }

        /// <summary>Sets the contents of this ValueContainer to the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public void SetValueNV(INamedValue value, bool mapNullToEmpty = true) { cvt = ContainerStorageType.NV; o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty); u = default(Union); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateA(string value) { return new ValueContainer() { cvt = ContainerStorageType.A, o = value, u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateLS(IList<string> value, bool mapNullToEmpty = true) { return new ValueContainer() { cvt = ContainerStorageType.LS, o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="set"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateLS(IEnumerable<string> set, bool mapNullToEmpty = true) { return new ValueContainer() { cvt = ContainerStorageType.LS, o = set.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateLS(string [] value, bool mapNullToEmpty = true) { return new ValueContainer() { cvt = ContainerStorageType.LS, o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="firstItem"/> concatinated with the given <paramref name="moreItemsArray"/> set of items using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateLS(string firstItem, params string[] moreItemsArray) { return new ValueContainer() { cvt = ContainerStorageType.LS, o = new ReadOnlyIList<string>(firstItem, moreItemsArray), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateL(IList<ValueContainer> value, bool mapNullToEmpty = true) { return new ValueContainer() { cvt = ContainerStorageType.L, o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateL(ValueContainer [] value, bool mapNullToEmpty = true) { return new ValueContainer() { cvt = ContainerStorageType.L, o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="set"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateL(IEnumerable<ValueContainer> set, bool mapNullToEmpty = true) { return new ValueContainer() { cvt = ContainerStorageType.L, o = set.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="firstItem"/> concatinated with the given <paramref name="moreItemsArray"/> set of items using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateL(ValueContainer firstItem, params ValueContainer[] moreItemsArray) { return new ValueContainer() { cvt = ContainerStorageType.L, o = new ReadOnlyIList<ValueContainer>(firstItem, moreItemsArray), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="firstItem"/> concatinated with the given <paramref name="moreItemsArray"/> set of items using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateL(object firstItem, params object [] moreItemsArray) { return new ValueContainer() { cvt = ContainerStorageType.L, o = firstItem.Concat(moreItemsArray.MapNullToEmpty()).Select(o => ValueContainer.CreateFromObject(o)).ConvertToReadOnly(), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="set"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateL(IEnumerable<object> set, bool mapNullToEmpty = true)
        {
            var vcROSet = (set != null) ? set.Select(o => ValueContainer.CreateFromObject(o)).ConvertToReadOnly() : (mapNullToEmpty ? ReadOnlyIList<ValueContainer>.Empty : null); ;
            return new ValueContainer() { cvt = ContainerStorageType.L, o = vcROSet, u = default(Union) };
        }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="set"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateL(IEnumerable set, bool mapNullToEmpty = true) { return ValueContainer.CreateL(set.SafeToSet(mapNullToEmpty: mapNullToEmpty), mapNullToEmpty: mapNullToEmpty); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateTS(TimeSpan value) { return new ValueContainer() { cvt = ContainerStorageType.TS, u = new Union() { TimeSpan = value } }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateDT(DateTime value) { return new ValueContainer() { cvt = ContainerStorageType.DT, u = new Union() { DateTime = value } }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateNVS(INamedValueSet value, bool mapNullToEmpty = true) { return new ValueContainer() { cvt = ContainerStorageType.NVS, o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty), u = default(Union) }; }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using the corresonding ContainerStorageType.</summary>
        public static ValueContainer CreateNV(INamedValue value, bool mapNullToEmpty = true) { return new ValueContainer() { cvt = ContainerStorageType.NV, o = value.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty), u = default(Union) }; }

        #endregion

        #region SetValue variants (Including type specific versions)

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(bool value) { SetValueBo(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(sbyte value) { SetValueI1(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(short value) { SetValueI2(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(int value) { SetValueI4(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(long value) { SetValueI8(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(byte value) { SetValueU1(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(ushort value) { SetValueU2(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(uint value) { SetValueU4(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(ulong value) { SetValueU8(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(float value) { SetValueF4(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(double value) { SetValueF8(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(string value) { SetValueA(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(TimeSpan value) { SetValueTS(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(DateTime value) { SetValueDT(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(IList<string> value) { SetValueLS(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(string [] value) { SetValueLS(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(IList<ValueContainer> value) { SetValueL(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(ValueContainer [] value) { SetValueL(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(INamedValue value) { SetValueNV(value); return this; }

        /// <summary>Type specific version of SetValue{}</summary>
        public ValueContainer SetValue(INamedValueSet value) { SetValueNVS(value); return this; }

        /// <summary>
        /// Typed value setter method.  
        /// This method decodes the ContainerStorageType from the given TValueType and then sets the corresponding storage field from value.
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer SetValue<TValueType>(TValueType value)
        {
            DecodedTypeInfo dti = StaticTypeDecoder<TValueType>.dti;

            // first try doing the assignment directly for supported types
            try
            {
                // handle all nullable cases where value is given as null here.  They all result in this VC being set to Null
                if (dti.isNullable && value == null)
                {
                    SetToNullObject();
                    return this;
                }

                // for well known ContainerStorageTypes (that do not have type ambiguity) we can directly handle the value here and give the optimizer more help
                switch (cvt = dti.cst)
                {
                    case ContainerStorageType.Bo: o = null; u = new Union() { b = (bool)((object)value) }; return this;
                    case ContainerStorageType.Bi: o = null; u = new Union() { bi = (byte)((object)value) }; return this;
                    case ContainerStorageType.I1: o = null; u = new Union() { i8 = (sbyte)((object)value) }; return this;
                    case ContainerStorageType.I2: o = null; u = new Union() { i16 = (short)((object)value) }; return this;
                    case ContainerStorageType.I4: o = null; u = new Union() { i32 = (int)((object)value) }; return this;
                    case ContainerStorageType.I8: o = null; u.i64 = (long)((object)value); return this;
                    case ContainerStorageType.U1: o = null; u = new Union() { u8 = (byte)((object)value) }; return this;
                    case ContainerStorageType.U2: o = null; u = new Union() { u16 = (ushort)((object)value) }; return this;
                    case ContainerStorageType.U4: o = null; u = new Union() { u32 = (uint)((object)value) }; return this;
                    case ContainerStorageType.U8: o = null; u.u64 = (ulong)((object)value); return this;
                    case ContainerStorageType.F4: o = null; u = new Union() { f32 = (float)((object)value) }; return this;
                    case ContainerStorageType.F8: o = null; u.f64 = (double)((object)value); return this;
                    case ContainerStorageType.TimeSpan: o = null; u.TimeSpan = (TimeSpan)((object)value); return this;
                    case ContainerStorageType.DateTime: o = null; u.DateTime = (DateTime)((object)value); return this;
                    case ContainerStorageType.String: o = ((value != null) ? ((System.Object)value).ToString() : null); u = default(Union); return this;
                    case ContainerStorageType.INamedValueSet: o = (value as INamedValueSet).ConvertToReadOnly(mapNullToEmpty: true); u = default(Union); return this;
                    case ContainerStorageType.INamedValue: o = (value as INamedValue).ConvertToReadOnly(mapNullToEmpty: true); u = default(Union); return this;
                    case ContainerStorageType.Object: o = value; u = default(Union); return this;
                    case ContainerStorageType.IListOfString:
                    case ContainerStorageType.IListOfVC:
                    case ContainerStorageType.Custom:
                    default: cvt = ContainerStorageType.None; break;        // types that are not handled here
                }
            }
            catch (System.Exception ex)
            {
                // we do not expect this catch statement to be triggered as the value type is very well known in this method.
                GlobalLastSetValueFailedException = ex;
                unchecked { GlobalSetValueFailedCount++; }
            }

            // fallback to using the more general SetValue method if the above logic does not recognize the given type
            SetValue<TValueType>(value, dti.cst, dti.isNullable);

            return this;
        }

        /// <summary>
        /// This method stores the given value in the desired container storage field.  If the given value does not fit in the indicated
        /// container then this method will attempt to convert the given value to an object and store it as such.
        /// <para/>Supports call chaining
        /// </summary>
        public ValueContainer SetValue<TValueType>(TValueType value, ContainerStorageType decodedValueType, bool isNullable = false)
        {
            try
            {
                // handle all nullable cases where value is given as null here.  They all result in this VC being set to Null
                if (isNullable && value == null)
                {
                    SetToNullObject();
                    return this;
                }

                // update the cvt to be the given decodedValueType and clear the value contained here
                cvt = decodedValueType;
                o = null;
                u = default(Union);

                // see if we can use the simple assignement version.

                switch (decodedValueType)
                {
                    case ContainerStorageType.None: o = null; cvt = ContainerStorageType.None; return this;
                    case ContainerStorageType.Object: o = (System.Object)value; cvt = ContainerStorageType.Object; return this;
                    case ContainerStorageType.String: o = (value as System.String) ?? ((value != null) ? ((System.Object)value).ToString() : null); return this;
                    case ContainerStorageType.Boolean: if (value is bool) { u.b = (bool)((System.Object)value); return this; } break;
                    case ContainerStorageType.Binary: if (value is byte) { u.bi = (byte)((System.Object)value); return this; } break;
                    case ContainerStorageType.SByte: if (value is sbyte) { u.i8 = (sbyte)((System.Object)value); return this; } break;
                    case ContainerStorageType.Int16: if (value is short) { u.i16 = (short)((System.Object)value); return this; } break;
                    case ContainerStorageType.Int32: if (value is int) { u.i32 = (int)((System.Object)value); return this; } break;
                    case ContainerStorageType.Int64: if (value is long) { u.i64 = (long)((System.Object)value); return this; } break;
                    case ContainerStorageType.Byte: if (value is byte) { u.u8 = (byte)((System.Object)value); return this; } break;
                    case ContainerStorageType.UInt16: if (value is ushort) { u.u16 = (ushort)((System.Object)value); return this; } break;
                    case ContainerStorageType.UInt32: if (value is uint) { u.u32 = (uint)((System.Object)value); return this; } break;
                    case ContainerStorageType.UInt64: if (value is ulong) { u.u64 = (ulong)((System.Object)value); return this; } break;
                    case ContainerStorageType.Single: if (value is float) { u.f32 = (float)((System.Object)value); return this; } break;
                    case ContainerStorageType.Double: if (value is double) { u.f64 = (double)((System.Object)value); return this; } break;
                    case ContainerStorageType.TimeSpan: if (value is TimeSpan) { u.TimeSpan = (TimeSpan)((System.Object)value); return this; } break;
                    case ContainerStorageType.DateTime: if (value is DateTime) { u.DateTime = (DateTime)((System.Object)value); return this; } break;
                    case ContainerStorageType.INamedValueSet: o = (value as INamedValueSet).ConvertToReadOnly(mapNullToEmpty: true); return this;
                    case ContainerStorageType.INamedValue: o = (value as INamedValue).ConvertToReadOnly(mapNullToEmpty: true); return this;
                    case ContainerStorageType.IListOfString:
                    case ContainerStorageType.IListOfVC:
                    case ContainerStorageType.Custom:
                    default: break;     // types that are not handled here
                }

                // all other cases will need to attempt to do some form of conversion.

                if (value is string)
                {
                    StringScanner ss = new StringScanner(value as string);

                    switch (decodedValueType)
                    {
                        case ContainerStorageType.Boolean: if (ss.ParseValue<bool>(out u.b) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.Binary: if (ss.ParseValue<byte>(out u.bi) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.SByte: if (ss.ParseValue<sbyte>(out u.i8) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.Int16: if (ss.ParseValue<short>(out u.i16) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.Int32: if (ss.ParseValue<int>(out u.i32) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.Int64: if (ss.ParseValue<long>(out u.i64) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.Byte: if (ss.ParseValue<byte>(out u.u8) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.UInt16: if (ss.ParseValue<ushort>(out u.u16) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.UInt32: if (ss.ParseValue<uint>(out u.u32) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.UInt64: if (ss.ParseValue<ulong>(out u.u64) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.Single: if (ss.ParseValue<float>(out u.f32) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.Double: if (ss.ParseValue<double>(out u.f64) && ss.IsAtEnd) return this; break;
                        case ContainerStorageType.TimeSpan: { TimeSpan ts; if (ss.ParseValue<TimeSpan>(out ts) && ss.IsAtEnd) { u.TimeSpan = ts; return this; } } break;
                        case ContainerStorageType.DateTime: { DateTime dt; if (ss.ParseValue<DateTime>(out dt) && ss.IsAtEnd) { u.DateTime = dt; return this; } } break;
                        default: break;
                    }
                }

                switch (decodedValueType)
                {
                    default: o = null; cvt = ContainerStorageType.None; break;
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
                    case ContainerStorageType.Double:
                        {
                            if (value is TimeSpan)
                                u.f64 = ((TimeSpan)((System.Object)value)).TotalSeconds;
                            else
                                u.f64 = (System.Double)System.Convert.ChangeType(value, typeof(System.Double));
                        }
                        break;
                    case ContainerStorageType.TimeSpan:
                        {
                            if (value is double)
                                u.TimeSpan = ((double)((System.Object)value)).FromSeconds();
                            else
                                u.TimeSpan = (System.TimeSpan)System.Convert.ChangeType(value, typeof(System.TimeSpan));
                        }
                        break;
                    case ContainerStorageType.DateTime: u.DateTime = (System.DateTime)System.Convert.ChangeType(value, typeof(System.DateTime)); break;
                    case ContainerStorageType.IListOfString: 
                        {
                            Object valueAsObject = (System.Object)value;
                            String [] valueAsArrayOfStrings = (valueAsObject as String []);
                            ReadOnlyIList<string> valueAsROIListOfStrings = (value as ReadOnlyIList<string>);
                            IList<string> oAsROIListOfStrings = (o as ReadOnlyIList<string>);

                            if (valueAsObject == null)
                            {
                                // first case - the given value is null
                                o = emptyIListOfString;
                            }
                            else if (valueAsArrayOfStrings != null)
                            {
                                if (oAsROIListOfStrings == null || !(oAsROIListOfStrings.IsEqualTo(valueAsArrayOfStrings)))
                                {
                                    o = new ReadOnlyIList<String>(valueAsArrayOfStrings);
                                }
                                // else o already contains the same readonly List of string from the array - do not replace it - this optimization step is only done for this CST
                            }
                            else if (valueAsROIListOfStrings != null)
                            {
                                if (oAsROIListOfStrings == null || !(oAsROIListOfStrings.IsEqualTo(valueAsROIListOfStrings)))
                                {
                                    o = valueAsROIListOfStrings;
                                }
                                // else o already contains the same readonly List of string contents as the given value.  no need to copy or change the existing contained value.
                            }
                            else
                            {
                                IList<string> valueAsIListOfStrings = (valueAsObject as IList<string>);
                                IEnumerable<string> valueAsStringEnumerable = (valueAsIListOfStrings == null) ? (valueAsObject as IEnumerable<string>) : null;
                                // cast should not throw unless TValueType is not castable to IList<String>.  Also should not be null since valueAsObject is not null.

                                if (valueAsIListOfStrings != null)
                                {
                                    if (oAsROIListOfStrings == null || !(oAsROIListOfStrings.IsEqualTo(valueAsIListOfStrings)))
                                    {
                                        o = new ReadOnlyIList<string>(valueAsIListOfStrings);
                                    }
                                    // else o already contains the same readonly List of string contents as the given value.  no need to copy or change the existing contained value.
                                }
                                else if (valueAsStringEnumerable != null)
                                {
                                    valueAsROIListOfStrings = new ReadOnlyIList<string>(valueAsStringEnumerable);

                                    if (oAsROIListOfStrings == null || !(oAsROIListOfStrings.IsEqualTo(valueAsROIListOfStrings)))
                                    {
                                        o = valueAsROIListOfStrings;
                                    }
                                    // else o already contains the same readonly List of string contents as the given value.  no need to copy or change the existing contained value.
                                }
                                else
                                {
                                    // else this is an unrecognized case.  This setter does not support the given object type.  Use a fallback
                                    cvt = ContainerStorageType.Object;
                                    o = value;
                                }
                            }
                        }
                        break;

                    case ContainerStorageType.IListOfVC:
                        {
                            Object valueAsObject = (System.Object)value;

                            ValueContainer[] valueAsArrayOfVCs = (valueAsObject as ValueContainer[]);
                            ReadOnlyIList<ValueContainer> valueAsROIListOfVCs = (value as ReadOnlyIList<ValueContainer>);
                            IList<ValueContainer> oAsROIListOfVCs = (o as ReadOnlyIList<ValueContainer>);

                            if (valueAsObject == null)
                            {
                                // first case - the given value is null
                                o = emptyIListOfVC;
                            }
                            else if (valueAsArrayOfVCs != null)
                            {
                                // convert given array into a readonly list of deep copies of each of the given VCs
                                o = new ReadOnlyIList<ValueContainer>(valueAsArrayOfVCs.Select(vc => ValueContainer.Empty.DeepCopyFrom(vc)));
                            }
                            else if (valueAsROIListOfVCs != null)
                            {
                                if (oAsROIListOfVCs == null || !(oAsROIListOfVCs.IsEqualTo(valueAsROIListOfVCs)))
                                {
                                    o = valueAsROIListOfVCs;
                                }
                                // else o already contains the same readonly List of VC contents as the given value.  no need to copy or change the existing contained value.
                            }
                            else
                            {
                                IList<ValueContainer> valueAsILVC = (valueAsObject as IList<ValueContainer>);
                                IEnumerable<ValueContainer> valueAsVCEnumerable = (valueAsILVC == null) ? (valueAsObject as IEnumerable<ValueContainer>) : null;

                                if (valueAsILVC != null)
                                {
                                    o = new ReadOnlyIList<ValueContainer>(valueAsILVC.Select(vc => ValueContainer.Empty.DeepCopyFrom(vc)));  // otherwise we need to generate a deep clone of the given list and save that new list (as readonly).
                                }
                                else if (valueAsVCEnumerable != null)
                                {
                                    // convert given enumerable into a readonly list of deep copies of each of the given VCs
                                    o = new ReadOnlyIList<ValueContainer>(valueAsVCEnumerable.Select(vc => ValueContainer.Empty.DeepCopyFrom(vc)));
                                }
                                else
                                {
                                    // else this is an unrecognized case.  This setter does not support the given object type.  Use a fallback
                                    cvt = ContainerStorageType.Object;
                                    o = value;
                                }
                            }
                        }
                        break;
                    case ContainerStorageType.Custom:
                        {
                            if (value == null)
                            {
                                cvt = ContainerStorageType.String;
                                o = null;
                            }
                            else if (value is Logging.LogGate || value is Logging.LogGate?)
                            {
                                Logging.LogGate logGate = (Logging.LogGate)((System.Object)value);
                                CopyFrom((ValueContainer)logGate);      // use fully type specified explicit cast
                            }
                            else
                            {
                                cvt = ContainerStorageType.String;
                                o = ((System.Object)value).ToString(); // use default ToString method
                            }
                        }
                        break;
                }
            }
            catch (System.Exception ex)
            {
                // if the above logic fails then cast the value as an object and save it as such.
                cvt = ContainerStorageType.Object;
                o = value;

                GlobalLastSetValueFailedException = ex;
                unchecked { GlobalSetValueFailedCount++; }
            }

            return this;
        }

        #endregion

        #region GetValue variants and LastGetValueException property

        /// <summary>
        /// Typed GetValue method.  
        /// This method decodes the TValueType to get the corresponding ContainerStorageType and isNullable.
        /// If the contained value type matches this decoded ContainerStorageType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion is successful then this method returns the transfered/converted value.  
        /// If this transfer/conversion throws an exception and <paramref name="rethrow"/> is true then this method rethrows the original exception, otherwise this method returns the given <paramref name="defaultValue"/>
        /// <para/>Note: because this method must decode the type information on each call, it may be less efficient to use this method than to use the version where the
        /// caller explicitly uses DecodeType method combined with the more complete GetValue method.
        /// </summary>
        public TValueType GetValue<TValueType>(bool rethrow, TValueType defaultValue = default(TValueType), bool allowTypeChangeAttempt = true)
        {
            DecodedTypeInfo dti = StaticTypeDecoder<TValueType>.dti;

            try
            {
                // if the stored type is exactly what the caller is asking for and it is one of the supported types then simply return it.
                if (dti.cst == cvt && !dti.isEnum)
                {
                    switch (dti.cst)
                    {
                        case ContainerStorageType.Bo: return (TValueType)((object)u.b);
                        case ContainerStorageType.Bi: return (TValueType)((object)u.bi);
                        case ContainerStorageType.I1: return (TValueType)((object)u.i8);
                        case ContainerStorageType.I2: return (TValueType)((object)u.i16);
                        case ContainerStorageType.I4: return (TValueType)((object)u.i32);
                        case ContainerStorageType.I8: return (TValueType)((object)u.i64);
                        case ContainerStorageType.U1: return (TValueType)((object)u.u8);
                        case ContainerStorageType.U2: return (TValueType)((object)u.u16);
                        case ContainerStorageType.U4: return (TValueType)((object)u.u32);
                        case ContainerStorageType.U8: return (TValueType)((object)u.u64);
                        case ContainerStorageType.F4: return (TValueType)((object)u.f32);
                        case ContainerStorageType.F8: return (TValueType)((object)u.f64);
                        case ContainerStorageType.TimeSpan: return (TValueType)((object)u.TimeSpan);
                        case ContainerStorageType.DateTime: return (TValueType)((object)u.DateTime);
                        case ContainerStorageType.String: if (typeof(TValueType) == stringType) { return (TValueType)o; } break;
                        default: break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                GlobalLastGetValueFailedException = ex;
                unchecked { GlobalGetValueFailedCount++; }
            }

            // fall through to the prior version of GetValue that handles all of the special cases.
            return GetValue<TValueType>(dti.cst, dti.isNullable, rethrow: rethrow, allowTypeChangeAttempt: allowTypeChangeAttempt, defaultValue: defaultValue, isEnumIn: dti.isEnum);
        }

        /// <summary>
        /// property is updated each time GetValue is called.  null indicates that the transfer and/or conversion was successful while any other value indicates why it was not.
        /// <para/>Note: this property is backed by a ThreadStatic field.  As such each managed thread only retains the last ValueContainer related exception that was generated by that thread.  
        /// Any use of other ValueContainer methods between throw and retreival might cause it to be replaced before being retrieved if any of these follow-on calls also threw a new exception.
        /// </summary>
        public System.Exception LastGetValueException { get { return _LastGetValueException; } private set { _LastGetValueException = value; } }

        [ThreadStatic]
        private static System.Exception _LastGetValueException = null;

        /// <summary>
        /// Typed GetValue method.  
        /// Requires that caller has pre-decoded the TValueType to obtain the expected ContainerStorageType and a flag to indicate if the TValueType is a nullable type.
        /// If the contained value type matches the decodedValueType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise if isNullable is false then the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion used and is successful then this method and returns the transfered/converted value.  
        /// If this transfer/conversion is not used or is not successful then this method returns the given defaultValue (which defaults to default(TValueType)).
        /// If rethrow is true and the method encounters any exceptions then it will rethrow the exception.
        /// </summary>
        public TValueType GetValue<TValueType>(ContainerStorageType decodedValueType, bool isNullable = false, bool rethrow = true, TValueType defaultValue = default(TValueType))
        {
            return GetValue<TValueType>(decodedValueType, isNullable, rethrow, true, defaultValue: defaultValue);
        }

        /// <summary>
        /// Typed GetValue method.  
        /// Requires that caller has pre-decoded the TValueType to obtain the expected ContainerStorageType and a flag to indicate if the TValueType is a nullable type.
        /// If the contained value type matches the decodedValueType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise if allowTypeChangeAttempt is true and valueTypeIsNullable is false then the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion used and is successful then this method and returns the transfered/converted value.  
        /// If this transfer/conversion is not used or is not successful then this method returns the given defaultValue (which defaults to default(TValueType)).  
        /// If rethrow is true and the method encounters any exceptions then it will rethrow the exception.
        /// </summary>
        public TValueType GetValue<TValueType>(ContainerStorageType decodedValueType, bool isNullable, bool rethrow, bool allowTypeChangeAttempt, TValueType defaultValue = default(TValueType), bool? isEnumIn = null)
        {
            Type TValueTypeType = typeof(TValueType);
            TValueType value;

            bool isEnum = isEnumIn ?? TValueTypeType.IsEnum;

            LastGetValueException = null;

            try
            {
                if (isEnum)
                {
                    if ((!rethrow || isNullable) && IsNullOrEmpty)
                        value = defaultValue;
                    else
                    {
                        Type inferredEnumType = (!isNullable ? TValueTypeType : Nullable.GetUnderlyingType(TValueTypeType));

                        // convert the integer and string conversion types seperately
                        switch (cvt)
                        {
                            case ContainerStorageType.I1: value = (TValueType)System.Enum.ToObject(inferredEnumType, u.i8); break;
                            case ContainerStorageType.I2: value = (TValueType)System.Enum.ToObject(inferredEnumType, u.i16); break;
                            case ContainerStorageType.I4: value = (TValueType)System.Enum.ToObject(inferredEnumType, u.i32); break;
                            case ContainerStorageType.I8: value = (TValueType)System.Enum.ToObject(inferredEnumType, u.i64); break;
                            case ContainerStorageType.U1: value = (TValueType)System.Enum.ToObject(inferredEnumType, u.u8); break;
                            case ContainerStorageType.U2: value = (TValueType)System.Enum.ToObject(inferredEnumType, u.u16); break;
                            case ContainerStorageType.U4: value = (TValueType)System.Enum.ToObject(inferredEnumType, u.u32); break;
                            case ContainerStorageType.U8: value = (TValueType)System.Enum.ToObject(inferredEnumType, u.u64); break;
                            default:
                                value = (TValueType)System.Enum.Parse(inferredEnumType, ValueAsObject.SafeToString().MapNullToEmpty(), false);
                                break;
                        }
                    }
                }
                else if (decodedValueType == cvt)
                {
                    // no conversion is required.  The stored type already mataches what the client is asking for.
                    switch (cvt)
                    {
                        case ContainerStorageType.None: value = defaultValue; break;
                        default:
                            new ValueContainerGetValueException("ContainerStorageType.{0} is not a supported".CheckedFormat(cvt)).Throw();
                            value = defaultValue;
                            break;
                        case ContainerStorageType.Object:
                            if ((o == null) && (!rethrow || isNullable))
                                value = defaultValue;
                            else
                                value = (TValueType)o;
                            break;
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
                                // IListOfString can be read as String [], List<string> or as IList<String>
                                var iList = o as IList<String> ?? emptyIListOfString;
                                if (TValueTypeType == stringArrayType)
                                    value = (TValueType)((System.Object)(iList.ToArray()));      // special case for reading from an IListOfString to an String array.
                                else if (TValueTypeType == listOfStringType)
                                    value = (TValueType)((System.Object)(new List<string>(iList)));  // special case for reading from an IListOfString to List<string>
                                else if (TValueTypeType == readOnlyCollectionOfStringType)      // special case for reading from an IListOfString to ReadOnlyCollection<string>
                                    value = (TValueType)((System.Object)(new List<string>(iList).AsReadOnly()));
                                else
                                    value = (TValueType)((System.Object)iList);      // all other cases the TValueType should be castable from an IList<String>
                            }
                            break;
                        case ContainerStorageType.IListOfVC:
                            {
                                // IListOfVC can be read as VC [], List<VC> or as IList<VC>
                                var iList = o as IList<ValueContainer> ?? emptyIListOfVC;
                                if (TValueTypeType == vcArrayType)
                                    value = (TValueType)((System.Object)(iList.ToArray()));      // special case for reading from an IListOfVC to an VC array.
                                else if (TValueTypeType == listOfVCType)
                                    value = (TValueType)((System.Object)(new List<ValueContainer>(iList)));  // special case for reading from an IListOfVC to List<VC>
                                else if (TValueTypeType == readOnlyCollectionOfVCType)      // special case for reading from an IListOfString to ReadOnlyCollection<VC>
                                    value = (TValueType)((System.Object)(new List<ValueContainer>(iList).AsReadOnly()));
                                else
                                    value = (TValueType)((System.Object)iList);      // all other cases the TValueType should be castable from an IList<VC>
                            }
                            break;
                        case ContainerStorageType.INamedValueSet: value = (TValueType)o; break;
                        case ContainerStorageType.INamedValue: value = (TValueType)o; break;
                    }
                }
                else if (decodedValueType == ContainerStorageType.Custom)
                {
                    if (TValueTypeType == typeof(Logging.LogGate))
                    {
                        if (IsNullOrEmpty && rethrow == false)
                        {
                            value = defaultValue;
                        }
                        else
                        {
                            Logging.LogGate logGate = (Logging.LogGate)this;            // use explicit cast to get this.
                            value = (TValueType)((object)logGate);
                        }
                    }
                    else if (TValueTypeType == typeof(Logging.LogGate?))
                    {
                        Logging.LogGate ? logGate = (IsNullOrEmpty ? null : (Logging.LogGate ?)((Logging.LogGate)this));            // use explicit cast to get this.
                        value = (TValueType)((object)logGate);
                    }
                    else
                    {
                        new ValueContainerGetValueException("Unable to get {0} as type '{1}': no recognized custom conversion exists".CheckedFormat(this, typeof(TValueType))).Throw();
                        value = defaultValue;
                    }
                }
                else if (decodedValueType == ContainerStorageType.TimeSpan && cvt.IsFloatingPoint() && allowTypeChangeAttempt)
                {
                    // support a direct conversion attempt from floating point value to TimeSpan by interpresting the floating value as seconds.
                    value = (TValueType)((System.Object) (((cvt == ContainerStorageType.Double) ? u.f64 : u.f32).FromSeconds()));
                }
                else if (decodedValueType == ContainerStorageType.Double && cvt == ContainerStorageType.TimeSpan && allowTypeChangeAttempt)
                {
                    // support direct conversion attempt from TimeSpan to double
                    value = (TValueType)((System.Object)(u.TimeSpan.TotalSeconds));
                }
                else if (decodedValueType == ContainerStorageType.Byte && cvt == ContainerStorageType.Binary && allowTypeChangeAttempt)
                {
                    // support direct conversion from Binary to Byte
                    value = (TValueType)((System.Object)(u.bi));
                }
                else if (decodedValueType == ContainerStorageType.Binary && cvt == ContainerStorageType.Byte && allowTypeChangeAttempt)
                {
                    // support direct conversion from Byte to Binary
                    value = (TValueType)((System.Object)(u.u8));
                }
                else if (decodedValueType.IsValueType() && !isNullable && !rethrow && IsNullOrEmpty)
                {
                    // support direct assignment of an empty or null container to a value type with rethrow = false without any attempt to actually perform the conversion (as it will obviously fail).
                    value = defaultValue;
                }
                else if (isNullable && (IsNullOrEmpty || (allowTypeChangeAttempt && cvt == ContainerStorageType.String && (o as string).IsNullOrEmpty())))
                {
                    // we can always assign a null/empty container to a nullable type by setting the type to its default (aka null).
                    // also allows simple conversion of any null or empty string to a nullable type using the same assignement to default (aka null).
                    value = defaultValue;
                }
                else if (allowTypeChangeAttempt)
                {
                    value = defaultValue;
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
                        string valueAsString = valueAsObject as System.String ?? String.Empty;
                        StringScanner ss = new StringScanner(valueAsString);
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
                                    {
                                        TimeSpan ts;
                                        conversionDone = ss.ParseValue(out ts) && ss.IsAtEnd;
                                        value = (TValueType)((System.Object)ts);
                                        break;
                                    }
                                case ContainerStorageType.DateTime:
                                    {
                                        DateTime dt;
                                        conversionDone = StringScanner.ParseValue(valueAsString, out dt);
                                        value = (TValueType)((System.Object)dt);
                                        break;
                                    }
                                default: break;
                            }
                        }
                    }

                    if (!conversionDone)
                    {
                        switch (decodedValueType)
                        {
                            case (ContainerStorageType.IListOfString):
                                if (cvt == ContainerStorageType.IListOfVC)
                                {
                                    IList<ValueContainer> oAsIsListOfVC = o as IList<ValueContainer> ?? emptyIListOfVC;
                                    if (TValueTypeType == stringArrayType)
                                        value = (TValueType)((System.Object)(oAsIsListOfVC.Select(vcItem => vcItem.ValueAsObject.ToString()).ToArray()));
                                    else
                                        value = (TValueType)((System.Object)new ReadOnlyIList<string>(oAsIsListOfVC.Select(vcItem => vcItem.ValueAsObject.ToString())));

                                    conversionDone = true;
                                }
                                else if (cvt == ContainerStorageType.String)
                                {
                                    string s = o as string;
                                    string[] sa = (s != null) ? new[] { s } : Utils.Collections.EmptyArrayFactory<string>.Instance;

                                    if (TValueTypeType == stringArrayType)
                                        value = (TValueType)((System.Object)(sa));
                                    else
                                        value = (TValueType)((System.Object)new ReadOnlyIList<string>(sa));

                                    conversionDone = true;
                                }
                                break;
                            case ContainerStorageType.IListOfVC:
                                if (cvt == ContainerStorageType.IListOfString)
                                {
                                    IList<string> oAsIsListOfString = o as IList<string> ?? emptyIListOfString;
                                    if (TValueTypeType == vcArrayType)
                                        value = (TValueType)((System.Object)(oAsIsListOfString.Select(str => ValueContainer.Create(str)).ToArray()));
                                    else
                                        value = (TValueType)((System.Object)new ReadOnlyIList<ValueContainer>(oAsIsListOfString.Select(str => ValueContainer.Create(str))));

                                    conversionDone = true;
                                }
                                break;
                            case ContainerStorageType.INamedValueSet:
                                if (cvt == ContainerStorageType.IListOfString || cvt == ContainerStorageType.IListOfVC)
                                {
                                    value = (TValueType)((System.Object)this.ConvertToNamedValueSet(asReadOnly: true));

                                    conversionDone = true;
                                }
                                break;
                            case ContainerStorageType.INamedValue:
                                if (cvt == ContainerStorageType.IListOfString || cvt == ContainerStorageType.IListOfVC || cvt == ContainerStorageType.String)
                                {
                                    value = (TValueType)((System.Object)this.ConvertToNamedValue(asReadOnly: true));

                                    conversionDone = true;
                                }
                                break;
                            default: break;
                        }
                    }

                    if (!conversionDone)
                    {
                        if (IsNullOrEmpty && (!rethrow || isNullable))
                            value = defaultValue;
                        else
                            value = (TValueType)System.Convert.ChangeType(valueAsObject, typeof(TValueType));
                    }
                }
                else
                {
                    value = defaultValue;
                    bool valueIsIntendedToBeNull = (cvt.IsReferenceType() && o == null);
                    if (!valueIsIntendedToBeNull)
                    {
                        if (allowTypeChangeAttempt)
                            LastGetValueException = new ValueContainerGetValueException("Unable to get {0} as type '{1}': No known conversion exists".CheckedFormat(this, typeof(TValueType)), null);
                        else
                            LastGetValueException = new ValueContainerGetValueException("Unable to get {0} as type '{1}': allowTypeChangeAttempt is false".CheckedFormat(this, typeof(TValueType)), null);
                    }
                }
            }
            catch (System.Exception ex)
            {
                value = defaultValue;

                if (ex is ValueContainerGetValueException)
                    LastGetValueException = ex;
                else
                    LastGetValueException = new ValueContainerGetValueException("Unable to get {0} as type '{1}': {2}".CheckedFormat(this, typeof(TValueType), ex.Message), ex);

                GlobalLastGetValueFailedException = LastGetValueException;
                unchecked { GlobalGetValueFailedCount++; }
            }

            if (rethrow && LastGetValueException != null)
                LastGetValueException.Throw();

            return value;
        }

        /// <summary>
        /// Typed TryGetValue method.  
        /// Requires that caller has pre-decoded the TValueType to obtain the expected ContainerStorageType and a flag to indicate if the TValueType is a nullable type.
        /// If the contained value type matches the decodedValueType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion is successful then this method and assigns the transfered/converted value and returns true.  
        /// If this transfer/conversion is not successful then this method assigns the given defaultValue (which defaults to default(TValueType)) and returns false.  
        /// If rethrow is true and the method encounters any exceptions then it will rethrow the exception.
        /// </summary>
        /// <remarks>Currently this signature is believed to be somewhat cumbersome and and the usage of the rethrow flag is at best odd.  This method may be deprecated in the future</remarks>
        public bool TryGetValue<TValueType>(out TValueType result, ContainerStorageType decodedValueType, bool isNullable = false, bool rethrow = true, TValueType defaultValue = default(TValueType))
        {
            try
            {
                result = GetValue<TValueType>(decodedValueType, isNullable, rethrow, defaultValue: defaultValue);

                if (LastGetValueException == null)
                    return true;

                result = defaultValue;

                return false;
            }
            catch (System.Exception ex)
            {
                result = defaultValue;

                if (LastGetValueException == null)
                    LastGetValueException = ex;

                if (rethrow)
                    ex.Throw();

                return false;
            }
        }

        /// <summary>
        /// Typed TryGetValue method.  
        /// Internally this method makes use of the corresponding GetValue{TValueType} method passing it the relevant parameters.
        /// If the contained value type matches this decoded ContainerStorageType then this method simply transfers the value from the corresponding storage field to the <paramref name="result"/> and returns true.
        /// Otherwise the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion is successful then this method assigns the transfered/converted value to the <paramref name="result"/> out property and returns true.  
        /// In all other cases this method assigns the out <paramref name="result"/> from the given <paramref name="defaultValue"/> and returns false.
        /// </summary>
        public bool TryGetValue<TValueType>(out TValueType result, TValueType defaultValue = default(TValueType), bool allowTypeChangeAttempt = true)
        {
            try
            {
                result = GetValue<TValueType>(rethrow: true, defaultValue: defaultValue, allowTypeChangeAttempt: allowTypeChangeAttempt);

                return true;
            }
            catch (System.Exception ex)
            {
                result = defaultValue;

                if (LastGetValueException == null)
                    LastGetValueException = ex;

                return false;
            }
        }

        #endregion

        #region Throw helpers (ThrowValueContainerGetValueException, ThrowGivenException - both are now obsolete)

        [Obsolete("This method has been depricated.  Please switch to using the new MosaicLib.Utils System.Exception.Throw() extension method (2020-05-26)")]
        public static void ThrowValueContainerGetValueException(string message, System.Exception innerException = null)
        {
            throw new ValueContainerGetValueException(message, innerException: innerException);
        }

        [Obsolete("This method has been depricated.  Please switch to using the new MosaicLib.Utils System.Exception.Throw() extension method (2020-05-26)")]
        public static void ThrowGivenException(System.Exception ex)
        {
            throw ex;
        }

        #endregion

        #region static global exception counts and latched exceptions - these methods/fields/properties are primarily used for unit testing.

        /// <summary>
        /// Global static volatile pseudo count of the number of exceptions that have occurred within a ValueContainer.SetValue method call.
        /// <para/>Note: As this variable is not atomically incremented, this count may not exactly represent the total count of such exceptions if two such exceptions are generated at the exact same time on two differnt cpu cores.
        /// </summary>
        public static volatile int GlobalSetValueFailedCount = 0;

        /// <summary>
        /// Global static volatile pseudo count of the number of exceptions that have occurred within a ValueContainer.GetValue method call.
        /// <para/>Note: As this variable is not atomically incremented, this count may not exactly represent the total count of such exceptions if two such exceptions are generated at the exact same time on two differnt cpu cores.
        /// </summary>
        public static volatile int GlobalGetValueFailedCount = 0;

        /// <summary>
        /// Global static volatile field that gives the last exception that was generated and caught during a ValueContainer.SetValue method call.
        /// </summary>
        public static volatile System.Exception GlobalLastSetValueFailedException = null;

        /// <summary>
        /// Global static volatile field that gives the last exception that was generated and caught during a ValueContainer.GetValue method call.
        /// </summary>
        public static volatile System.Exception GlobalLastGetValueFailedException = null;

        /// <summary>
        /// This method clears the standard ValueContainer global exception count and record values including GlobalSetValueFailedCount, GlobalGetValueFailedCount, GlobalLastSetValueFailedException, and GlobalLastGetValueFailedException
        /// </summary>
        public static void ClearGlobalExceptionValues()
        {
            GlobalSetValueFailedCount = 0;
            GlobalGetValueFailedCount = 0;
            GlobalLastSetValueFailedException = null;
            GlobalLastGetValueFailedException = null;
        }

        #endregion

        #region EstimatedContentSizeInBytes (deprecated)

        /// <summary>
        /// Returns the estimated size of the given contents in bytes.  
        /// For non-reference types this returns 16.  
        /// For string types this returns the l6 + the length of the string in bytes
        /// For string array and string list types, this returns 16 + the summed length of each of the strings in bytes + 16 bytes of overhead for each one.
        /// </summary>
        [Obsolete("The use of this property has been deprecated.  (2018-03-07)")]
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
                else if (o is INamedValueSet)
                {
                    return (o as INamedValueSet).EstimatedContentSizeInBytes;
                }
                else if (o is INamedValue)
                {
                    return (o as INamedValue).EstimatedContentSizeInBytes;
                }
                else
                {
                    return 0;
                }
            }
        }

        private const int defaultBasePerItemSizeInBytes = 16;

        #endregion

        #region Equality testing related methods (IsEqualTo, Equals, ...) and related overrides (GetHashCode)

        /// <summary>
        /// Equality testing implementation method.  Uses ValueContainer in signature to remove need for casting (as with Equals).
        /// </summary>
        public bool IsEqualTo(ValueContainer other)
        {
            return Equals(other);
        }

        /// <summary>IEquatable{ValueContainer} Equals implementation.  Returns true if the contents of this ValueContainer are "equal" to the contents of the other ValueContainer.</summary>
        public bool Equals(ValueContainer other)
        {
            if (cvt != other.cvt)
                return false;

            switch (cvt)
            {
                case ContainerStorageType.None: return true;
                case ContainerStorageType.Bo: return (u.b == other.u.b);
                case ContainerStorageType.Bi: return (u.bi == other.u.bi);
                case ContainerStorageType.I1: return (u.i8 == other.u.i8);
                case ContainerStorageType.I2: return (u.i16 == other.u.i16);
                case ContainerStorageType.I4: return (u.i32 == other.u.i32);
                case ContainerStorageType.I8: return (u.i64 == other.u.i64);
                case ContainerStorageType.U1: return (u.u8 == other.u.u8);
                case ContainerStorageType.U2: return (u.u16 == other.u.u16);
                case ContainerStorageType.U4: return (u.u32 == other.u.u32);
                case ContainerStorageType.U8: return (u.u64 == other.u.u64);
                case ContainerStorageType.F4: return (u.f32 == other.u.f32);
                case ContainerStorageType.F8: return (u.f64 == other.u.f64);
                case ContainerStorageType.TimeSpan: return (u.TimeSpan == other.u.TimeSpan);
                case ContainerStorageType.DateTime: return (u.DateTime == other.u.DateTime);
                case ContainerStorageType.String: return System.Object.Equals(o, other.o);
                case ContainerStorageType.IListOfString: return (o as IList<String>).IsEqualTo(other.o as IList<String>);
                case ContainerStorageType.IListOfVC: return (o as IList<ValueContainer>).IsEqualTo(other.o as IList<ValueContainer>);
                case ContainerStorageType.INamedValueSet: return (o as INamedValueSet).MapNullToEmpty().Equals(other.o as INamedValueSet);
                case ContainerStorageType.INamedValue: return (o as INamedValue).MapNullToEmpty().Equals(other.o as INamedValue);
                case ContainerStorageType.Object:
                    {
                        if (o != null)
                        {
                            ValueContainer[] vcArray = o as ValueContainer[];

                            if (vcArray != null)
                                return vcArray.IsEqualTo(other.o as ValueContainer[]);
                        }

                        if (o is System.Array && other.o is System.Array)
                            return Utils.Fcns.Equals(o as System.Array, other.o as System.Array);

                        if (o is BiArray && other.o is BiArray)
                            return ((o as BiArray) ?? BiArray.Empty).Equals(other.o as BiArray ?? BiArray.Empty);

                        return System.Object.Equals(o, other.o);
                    }
                default: return false;
            }
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
            if (o == null)
                return (cvt.GetHashCode() ^ u.u64.GetHashCode());
            else
                return (cvt.GetHashCode() ^ u.u64.GetHashCode() ^ o.GetHashCode());
        }

        #endregion

        #region ToString variants (ToString, ToStringSML)

        /// <summary>
        /// Override ToString for logging and debugging.  (Currently the same as ToStringSML)
        /// </summary>
        public override string ToString()
        {
            return ToStringSML();
        }

        /// <summary>
        /// ToString variant that support SML like output format.
        /// This format is generally similar to SML except that it generally uses square brackets as element delimiters rather than
        /// greater and less than symbols (which are heavily used in XML and must thus be escaped when placing such strings in XML output).
        /// </summary>
        public string ToStringSML()
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
                            return "[LS {0}]".CheckedFormat(String.Join(" ", (strList.Select(s => ValueContainer.Create(s).ToStringSML())).ToArray()));
                        else
                            return "[LS]";
                    }
                case ContainerStorageType.INamedValueSet:
                    return (o as INamedValueSet).MapNullToEmpty().ToStringSML(nvsNodeName: "NVS", nvNodeName: "NV");
                case ContainerStorageType.INamedValue:
                    return (o as INamedValue).MapNullToEmpty().ToStringSML(nvNodeName: "NV");
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

                if (o is INamedValueSet) { return (o as INamedValueSet).ToStringSML(); }
                if (o is INamedValue) { return (o as INamedValue).ToStringSML(); }

                Type oType = o.GetType();
                if (oType == typeof(bool[])) { return "[Bool_Array{0}]".CheckedFormat(String.Concat((o as bool[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(sbyte[])) { return "[I1_Array{0}]".CheckedFormat(String.Concat((o as sbyte[]).Select(v => " {0}".CheckedFormat((int)v)))); }
                if (oType == typeof(short[])) { return "[I2_Array{0}]".CheckedFormat(String.Concat((o as short[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(int[])) { return "[I4_Array{0}]".CheckedFormat(String.Concat((o as int[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(long[])) { return "[I8_Array{0}]".CheckedFormat(String.Concat((o as long[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(byte[])) { return "[U1_Array{0}]".CheckedFormat(String.Concat((o as byte[]).Select(v => " {0}".CheckedFormat((uint)v)))); }
                if (oType == typeof(ushort[])) { return "[U2_Array{0}]".CheckedFormat(String.Concat((o as ushort[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(uint[])) { return "[U4_Array{0}]".CheckedFormat(String.Concat((o as uint[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(ulong[])) { return "[U8_Array{0}]".CheckedFormat(String.Concat((o as ulong[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(float[])) { return "[F4_Array{0}]".CheckedFormat(String.Concat((o as float[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(double[])) { return "[F8_Array{0}]".CheckedFormat(String.Concat((o as double[]).Select(v => " {0}".CheckedFormat(v)))); }
                if (oType == typeof(BiArray)) { return "[Bi_Array{0}]".CheckedFormat(String.Concat((o as BiArray).Select(v => " {0}".CheckedFormat(v)))); }
            }

            if (cvt == ContainerStorageType.Object)
                return "[{0} '{1}']".CheckedFormat(cvt, o);
            else
                return "[{0} '{1}']".CheckedFormat(cvt, ValueAsObject);
        }

        /// <summary>Constists of ' ', '"', '[' and ']'</summary>
        private static readonly List<char> basicUnquotedStringExcludeList = new List<char>() { ' ', '\"', '[', ']' };

        private static readonly IList<String> emptyIListOfString = ReadOnlyIList<String>.Empty;
        private static readonly IList<ValueContainer> emptyIListOfVC = ReadOnlyIList<ValueContainer>.Empty;

        #endregion
    }

    /// <summary>
    /// Enumeration that is used with the ValueContainer struct.
    /// <para/>None (0 : default), Custom, Object, String, IListOfString, IListOfVC, INamedValueSet, INamedValue, Boolean, Binary, SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, UInt64, Single, Double, TimeSpan, DateTime
    /// <para/>Alternates: A, LS, L, Bo, Bi, I1, I2, I4, I8, U1, U2, U4, U8, F4, F8, TS, DT, NVS, NV
    /// </summary>
    public enum ContainerStorageType : byte
    {
        /// <summary>Custom value for cases where the storage type has not been defined (the default value: 0)</summary>
        None = 0,
        /// <summary>Custom value for special GetValue/SetValue cases.  This CST is not intended to be used for actual storage</summary>
        Custom,
        /// <summary>Use Object field</summary>
        Object,
        /// <summary>Use Object field as a String</summary>
        String,
        /// <summary>Use Object field as an IList{String} - normally contains a ReadOnlyIList{String}</summary>
        IListOfString,
        /// <summary>Use Object field as an IList{ValueContainer} - normally contains a ReadOnlyIList{ValueContainer}</summary>
        IListOfVC,
        /// <summary>Use Object field as an INamedValueSet - usually marked as ReadOnly</summary>
        INamedValueSet,
        /// <summary>Use Object field as an INamedValue - usually marked as ReadOnly</summary>
        INamedValue,
        /// <summary>Use Union.b field</summary>
        Boolean,
        /// <summary>Use Union.bi field</summary>
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

        /// <summary>Alternate version of String.  Use Object field</summary>
        A = String,
        /// <summary>Alternate version of IListOfString.  Use Object field</summary>
        LS = IListOfString,
        /// <summary>Alternate version of IListOfVC.  Use Object field</summary>
        L = IListOfVC,
        /// <summary>Alternate version of Boolean.  Use Union.b field</summary>
        Bo = Boolean,
        /// <summary>Alternate version of Binary.  Use Union.bi field</summary>
        Bi = Binary,
        /// <summary>Alternate version of SByte.  Use Union.i8 field</summary>
        I1 = SByte,
        /// <summary>Alternate version of Int16.  Use Union.i16 field</summary>
        I2 = Int16,
        /// <summary>Alternate version of Int32.  Use Union.i32 field</summary>
        I4 = Int32,
        /// <summary>Alternate version of Int64.  Use Union.i64 field</summary>
        I8 = Int64,
        /// <summary>Alternate version of Byte.  Use Union.u8 field</summary>
        U1 = Byte,
        /// <summary>Alternate version of UInt16.  Use Union.u16 field</summary>
        U2 = UInt16,
        /// <summary>Alternate version of UInt32.  Use Union.u32 field</summary>
        U4 = UInt32,
        /// <summary>Alternate version of UInt64.  Use Union.u64 field</summary>
        U8 = UInt64,
        /// <summary>Alternate version of Single.  Use Union.f32 field</summary>
        F4 = Single,
        /// <summary>Alternate version of Double.  Use Union.f64 field</summary>
        F8 = Double,
        /// <summary>Alternate version of TimeSpan.  Use Union.TimeSpan property</summary>
        TS = TimeSpan,
        /// <summary>Alternate version of DateTime.  Use Union.DateTime property</summary>
        DT = DateTime,
        /// <summary>Alternate version of INamedValueSet.  Use Object field</summary>
        NVS = INamedValueSet,
        /// <summary>Alternate version of INamedValue.  Use Object field</summary>
        NV = INamedValue,
    }

    /// <summary>Standard extension methods wrapper class/namespace</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given ContainerStorageType is a reference type.  (Currently Object, String, IListOfString, IListOfVC, INamedValueSet, INamedValue)
        /// </summary>
        public static bool IsReferenceType(this ContainerStorageType cst)
        {
            switch (cst)
            {
                case ContainerStorageType.Object:
                case ContainerStorageType.String:
                case ContainerStorageType.IListOfString:
                case ContainerStorageType.IListOfVC:
                case ContainerStorageType.INamedValueSet:
                case ContainerStorageType.INamedValue:
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
        /// <para/>I1, I2, I4, I8 => <paramref name="includeSigned"/>, Bo, Bi, U1, U2, U4, U8 +> <paramref name="includeUnsigned"/>
        /// </summary>
        public static bool IsInteger(this ContainerStorageType cst, bool includeSigned = true, bool includeUnsigned = true)
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
        /// Returns true if the given ContainerStorageType is L or LS.
        /// </summary>
        public static bool IsList(this ContainerStorageType cst)
        {
            switch (cst)
            {
                case ContainerStorageType.L:
                case ContainerStorageType.LS:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true if the given <paramref name="vc"/> is neither IsNull nor IsEmpty
        /// </summary>
        public static bool IsNeitherNullNorEmpty(this ValueContainer vc)
        {
            return !vc.IsNullOrEmpty;
        }

        /// <summary>
        /// If <paramref name="lhs"/> is type compatible with <paramref name="rhs"/> then this method returns the one that has the largest range, otherwise this method returns the given <paramref name="fallbackValue"/> (defaults to ContainerStorageType.None)
        /// <para/>available conversion rules (can be chained): Bi => U1, Bo => I1 or U1, I1 => I2, I2 => I4 or F4, I4 => I8 or F8, U1 => U2 or I2 or F4, U2 => U4 or I4 or F4, U4 => U8 or I8 or F8, F4 => F8
        /// <para/>None,any or any,None => any
        /// </summary>
        public static ContainerStorageType Upcast(this ContainerStorageType lhs, ContainerStorageType rhs, ContainerStorageType fallbackValue = ContainerStorageType.None, bool allowTypeChangeAttempts = false)
        {
            if (lhs == rhs || rhs == ContainerStorageType.None)
                return lhs;
            else if (lhs == ContainerStorageType.None)
                return rhs;
            else if (lhs < rhs)
                return rhs.Upcast(lhs, fallbackValue: fallbackValue);

            // lhs is known >= rhs so decode in order of increasing values of lhs.
            switch (lhs)
            {
                case ContainerStorageType.Bi: // rhs can be Bo
                    if (rhs == ContainerStorageType.Bo)
                        return lhs;
                    break;

                case ContainerStorageType.I1: // rhs can be Bi, Bo
                    if (rhs == ContainerStorageType.Bo)
                        return lhs;
                    else if (rhs == ContainerStorageType.Bi)
                        return ContainerStorageType.I2;
                    break;

                case ContainerStorageType.I2: // rhs can be Bi, Bo, I1
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi || rhs == ContainerStorageType.I1)
                        return lhs;
                    break;

                case ContainerStorageType.I4: // rhs can be Bi, Bo, I1, I2
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi || rhs == ContainerStorageType.I1 || rhs == ContainerStorageType.I2)
                        return lhs;
                    break;

                case ContainerStorageType.I8: // rhs can be Bi, Bo, I1, I2, I4
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi || rhs == ContainerStorageType.I1 || rhs == ContainerStorageType.I2 || rhs == ContainerStorageType.I4)
                        return lhs;
                    break;

                case ContainerStorageType.U1:  // rhs can be Bi, Bo, I1, I2, I4, I8
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi)
                        return lhs;
                    if (rhs == ContainerStorageType.I2 || rhs == ContainerStorageType.I4 || rhs == ContainerStorageType.I8)
                        return rhs;
                    if (rhs == ContainerStorageType.I1)
                        return ContainerStorageType.I2;
                    break;

                case ContainerStorageType.U2:  // rhs can be Bi, Bo, I1, I2, I4, I8, U1
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi || rhs == ContainerStorageType.U1)
                        return lhs;
                    if (rhs == ContainerStorageType.I4 || rhs == ContainerStorageType.I8)
                        return rhs;
                    if (rhs == ContainerStorageType.I1 || rhs == ContainerStorageType.I2)
                        return ContainerStorageType.I4;
                    break;

                case ContainerStorageType.U4:  // rhs can be Bi, Bo, I1, I2, I4, I8, U1, U2
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi || rhs == ContainerStorageType.U1 || rhs == ContainerStorageType.U2)
                        return lhs;
                    if (rhs == ContainerStorageType.I8)
                        return rhs;
                    if (rhs == ContainerStorageType.I1 || rhs == ContainerStorageType.I2 || rhs == ContainerStorageType.I4)
                        return ContainerStorageType.I8;
                    break;

                case ContainerStorageType.U8:  // rhs can be Bi, Bo, I1, I2, I4, I8, U1, U2, U4
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi || rhs == ContainerStorageType.U1 || rhs == ContainerStorageType.U2 || rhs == ContainerStorageType.U4)
                        return lhs;
                    break;

                case ContainerStorageType.F4:  // rhs can be Bi, Bo, I1, I2, I4, I8, U1, U2, U4, U8
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi || rhs == ContainerStorageType.I1 || rhs == ContainerStorageType.I2 || rhs == ContainerStorageType.U1 || rhs == ContainerStorageType.U2)
                        return lhs;
                    if (rhs == ContainerStorageType.I4 || rhs == ContainerStorageType.U4)
                        return ContainerStorageType.F8;
                    break;

                case ContainerStorageType.F8:  // rhs can be Bi, Bo, I1, I2, I4, I8, U1, U2, U4, U8, F4
                    if (rhs == ContainerStorageType.Bo || rhs == ContainerStorageType.Bi || rhs == ContainerStorageType.I1 || rhs == ContainerStorageType.I2 || rhs == ContainerStorageType.I4
                        || rhs == ContainerStorageType.U1 || rhs == ContainerStorageType.U2 || rhs == ContainerStorageType.U4 || rhs == ContainerStorageType.F4)
                    {
                        return lhs;
                    }
                    break;
            }

            if (allowTypeChangeAttempts && (lhs == ContainerStorageType.A || rhs == ContainerStorageType.A))
                return ContainerStorageType.A;

            return fallbackValue;            
        }

        /// <summary>
        /// For value types, this method attempts to convert the given <paramref name="value"/> by upcasting its contents to the given <paramref name="cst"/>.
        /// If the given <paramref name="cst"/> is the same as the <paramref name="value"/>'s contents then the value is returned.
        /// If the given <paramref name="cst"/> is a reachable upcast fromt the given <paramref name="value"/>'s contents then the upcasted contents are returned.
        /// Otherwise the fallbackValue is returned.
        /// </summary>
        public static ValueContainer Cast(this ValueContainer value, ContainerStorageType cst, ValueContainer fallbackValue = default(ValueContainer), bool allowUpcastAttempt = true, bool allowTypeChangeAttempt = false, bool rethrow = false)
        {
            if (value.cvt == cst)
                return value;

            if (allowUpcastAttempt || allowTypeChangeAttempt)
            {
                switch (cst)
                {
                    case ContainerStorageType.Bi: return ValueContainer.CreateBi(value.GetValueBi(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.Bo: return ValueContainer.CreateBo(value.GetValueBo(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.I1: return ValueContainer.CreateI1(value.GetValueI1(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.I2: return ValueContainer.CreateI2(value.GetValueI2(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.I4: return ValueContainer.CreateI4(value.GetValueI4(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.I8: return ValueContainer.CreateI8(value.GetValueI8(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.U1: return ValueContainer.CreateU1(value.GetValueU1(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.U2: return ValueContainer.CreateU2(value.GetValueU2(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.U4: return ValueContainer.CreateU4(value.GetValueU4(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.U8: return ValueContainer.CreateU8(value.GetValueU8(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.F4: return ValueContainer.CreateF4(value.GetValueF4(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.F8: return ValueContainer.CreateF8(value.GetValueF8(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempt));
                    case ContainerStorageType.A: 
                        if (allowTypeChangeAttempt)
                            return ValueContainer.CreateA(value.GetValueA(rethrow: rethrow, allowAllTypeChangeAttempts: true));
                        break;
                    default:
                        break;
                }
            }

            return fallbackValue;
        }

        /// <summary>
        /// Compares the contents of the given <paramref name="lhs"/> to the contents of the given <paramref name="rhs"/> and returns a signed integer that indicates the relative order of the two values.
        /// Returns a negative integer if <paramref name="lhs"/> is less than <paramref name="rhs"/>, zero if they are equal, or a possitive integer otherwise.
        /// If the two given values do not have the same content type, or they cannot be successfully upcasted or converted to a common superset type then the method returns the given <paramref name="fallbackValue"/> which defaults to -2
        /// </summary>
        public static int CompareTo(this ValueContainer lhs, ValueContainer rhs, bool allowUpcastAttempt = true, bool allowTypeChangeAttempts = false, bool rethrow = false, int fallbackValue = int.MinValue)
        {
            bool comparible = (lhs.cvt == rhs.cvt && !lhs.IsNullOrEmpty);
            if (!comparible && (allowUpcastAttempt || allowTypeChangeAttempts))
            {
                var upcastedCST = lhs.cvt.Upcast(rhs.cvt, allowTypeChangeAttempts: allowTypeChangeAttempts);

                if (lhs.cvt != upcastedCST)
                    lhs = lhs.Cast(upcastedCST, fallbackValue: lhs, allowUpcastAttempt: true, allowTypeChangeAttempt: allowTypeChangeAttempts, rethrow: rethrow);

                if (rhs.cvt != upcastedCST)
                    rhs = rhs.Cast(upcastedCST, fallbackValue: rhs, allowUpcastAttempt: true, allowTypeChangeAttempt: allowTypeChangeAttempts, rethrow: rethrow);

                comparible = (lhs.cvt == rhs.cvt && !lhs.IsNullOrEmpty);
            }

            if (!comparible)
                return fallbackValue;

            switch (lhs.cvt)
            {
                case ContainerStorageType.Bi: return lhs.u.bi.CompareTo(rhs.u.bi);
                case ContainerStorageType.Bo: return lhs.u.b.MapToInt().CompareTo(rhs.u.b.MapToInt());
                case ContainerStorageType.I1: return lhs.u.i8.CompareTo(rhs.u.i8);
                case ContainerStorageType.I2: return lhs.u.i16.CompareTo(rhs.u.i16);
                case ContainerStorageType.I4: return lhs.u.i32.CompareTo(rhs.u.i32);
                case ContainerStorageType.I8: return lhs.u.i64.CompareTo(rhs.u.i64);
                case ContainerStorageType.U1: return lhs.u.u8.CompareTo(rhs.u.u8);
                case ContainerStorageType.U2: return lhs.u.u16.CompareTo(rhs.u.u16);
                case ContainerStorageType.U4: return lhs.u.u32.CompareTo(rhs.u.u32);
                case ContainerStorageType.U8: return lhs.u.u64.CompareTo(rhs.u.u64);
                case ContainerStorageType.F4: return lhs.u.f32.CompareTo(rhs.u.f32);
                case ContainerStorageType.F8: return lhs.u.f64.CompareTo(rhs.u.f64);
                case ContainerStorageType.A: return lhs.GetValueA(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempts).MapNullToEmpty().CompareTo(rhs.GetValueA(rethrow: rethrow, allowAllTypeChangeAttempts: allowTypeChangeAttempts).MapNullToEmpty());
                default: return fallbackValue;
            }
        }

        /// <summary>
        /// Returns true if the given value is IsNull, IsEmpty, or IsZero
        /// </summary>
        public static bool IsNullOrEmptyOrZero(this ValueContainer vc)
        {
            return (vc.IsNullOrEmpty || vc.IsZero());
        }

        /// <summary>
        /// Returns true if the given value contains a value that is zero.  CST must be one of (Bi, Bo, I1..I8, U1..U8, F4 or F8)
        /// </summary>
        public static bool IsZero(this ValueContainer vc)
        {
            switch (vc.cvt)
            {
                case ContainerStorageType.Bi: return vc.u.bi == 0;
                case ContainerStorageType.Bo: return vc.u.b.MapToInt() == 0;
                case ContainerStorageType.I1: return vc.u.i8 == 0;
                case ContainerStorageType.I2: return vc.u.i16 == 0;
                case ContainerStorageType.I4: return vc.u.i32 == 0;
                case ContainerStorageType.I8: return vc.u.i64 == 0;
                case ContainerStorageType.U1: return vc.u.u8 == 0;
                case ContainerStorageType.U2: return vc.u.u16 == 0;
                case ContainerStorageType.U4: return vc.u.u32 == 0;
                case ContainerStorageType.U8: return vc.u.u64 == 0;
                case ContainerStorageType.F4: return vc.u.f32 == 0;
                case ContainerStorageType.F8: return vc.u.f64 == 0;
                default: return false;
            }
        }

        /// <summary>
        /// If the given <paramref name="vc"/> is a number (integer or floating type) then this method sets its value to zero and returns it otherwise this method returns the given <paramref name="fallbackValue"/>.
        /// </summary>
        public static ValueContainer SetZero(this ValueContainer vc, ValueContainer fallbackValue = default(ValueContainer))
        {
            if (vc.cvt.IsInteger() || vc.cvt.IsFloatingPoint())
            {
                vc.u = default(ValueContainer.Union);
                return vc;
            }
            else
            {
                return fallbackValue;
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

        /// <summary>
        /// Extension method used to set the contents of a ValueContainer to contain a list of the given items.  
        /// This method will produce an IListOfStrings if ItemType is string and otherwise it will produce an IListOfVC
        /// If the given itemParamArray is null or is empty then this method will return an Empty ValueContainer
        /// If only one value is given in the itemParamArray then the returned ValueContainer will simply be a ValueContainer containing that Item.
        /// </summary>
        public static ValueContainer SetFromItems<ItemType>(this ValueContainer vc, params ItemType[] itemParamArray)
        {
            return ValueContainer.CreateFromItems<ItemType>(itemParamArray);
        }

        /// <summary>
        /// Extension method used to set the contents of a ValueContainer to contain a set of the given items.  
        /// This method will produce an IListOfStrings if ItemType is string and otherwise it will produce an IListOfVC
        /// If the given itemSet is null then this method will return an Empty ValueContainer.
        /// </summary>
        public static ValueContainer SetFromSet<ItemType>(this ValueContainer vc, IEnumerable<ItemType> itemSet)
        {
            return ValueContainer.CreateFromSet<ItemType>(itemSet);
        }

        /// <summary>
        /// Extension method is used to convert the contents ofa given ValueContainer into its "raw" JSON representation.
        /// In particular this includes conversion of INamedValueSet and INamedValue contents into what is best described as JSON dictionary representation.
        /// [{"key1":value},{"key2":value},...].  This method currently attempts to cover all well known ValueContainer contents as well as
        /// unrecognized object types which are encoded to JSON with the help of a ValueContainerEnvelope and a corresponding DataContractJsonSerializer.
        /// </summary>
        public static string ConvertToRawJSON(this ValueContainer vc)
        {
            try
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    using (var xmlWriter = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, ownsStream: false))
                    {
                        xmlWriter.WriteRawJSONElement("root", vc);

                        xmlWriter.Flush();
                        xmlWriter.Close();
                    }

                    string result = Encoding.UTF8.GetString(ms.ToArray());
                    return result;
                }
            }
            catch (System.Exception ex)
            {
                string exStr = ex.ToString(ExceptionFormat.TypeAndMessage);
                return "{{\"ex\":\"{0}\"}}".CheckedFormat(exStr.GenerateQuotableVersion());
            }
        }

        private static void WriteRawJSONElement(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, ValueContainer vc)
        {
            ContainerStorageType vcCST = vc.cvt;

            switch (vcCST)
            {
                case ContainerStorageType.INamedValueSet: xmlWriter.WriteRawJSONElement(elementName, vc.GetValueNVS(rethrow: true)); return;
                case ContainerStorageType.INamedValue: xmlWriter.WriteRawJSONElement(elementName, vc.GetValueNV(rethrow: true)); return;
                case ContainerStorageType.IListOfString: xmlWriter.WriteRawJSONElement(elementName, vc.GetValueLS(rethrow: true)); return;
                case ContainerStorageType.IListOfVC: xmlWriter.WriteRawJSONElement(elementName, vc.GetValueL(rethrow: true)); return;
                case ContainerStorageType.String: xmlWriter.WriteRawJSONElement(elementName, vc.GetValueA(rethrow: true)); return;
                case ContainerStorageType.Custom: xmlWriter.WriteRawJSONElement(elementName, vc.GetValueA(rethrow: true)); return;
                case ContainerStorageType.DateTime: xmlWriter.WriteRawJSONElement(elementName, vc.GetValueDT(rethrow: true).ToString("o")); return;
                case ContainerStorageType.TimeSpan: xmlWriter.WriteRawJSONElement(elementName, vc.GetValueTS(rethrow: true).ToString()); return;
                case ContainerStorageType.None: xmlWriter.WriteRawJSONElementNull(elementName); return;
                case ContainerStorageType.Object: xmlWriter.WriteRawJSONElementAsDecodedObject(elementName, vc.o); return;
                default: break;
            }

            xmlWriter.WriteStartElement(elementName);

            switch (vc.cvt)
            {
                case ContainerStorageType.Bo: xmlWriter.WriteAttributeString("type", "boolean"); xmlWriter.WriteValue(vc.u.b); break;
                case ContainerStorageType.F4: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.f32); break;
                case ContainerStorageType.F8: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.f64); break;
                case ContainerStorageType.Bi: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.bi); break;
                case ContainerStorageType.I1: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.i8); break;
                case ContainerStorageType.I2: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.i16); break;
                case ContainerStorageType.I4: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.i32); break;
                case ContainerStorageType.I8: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.i64); break;
                case ContainerStorageType.U1: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.u8); break;
                case ContainerStorageType.U2: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.u16); break;
                case ContainerStorageType.U4: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue(vc.u.u32); break;
                case ContainerStorageType.U8: xmlWriter.WriteAttributeString("type", "number"); xmlWriter.WriteValue((long)(vc.u.u64)); break;
                default: break;
            }

            xmlWriter.WriteEndElement();
        }

        private static void WriteRawJSONElementNull(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName)
        {
            xmlWriter.WriteStartElement(elementName);
            xmlWriter.WriteAttributeString("type", "null");
            xmlWriter.WriteEndElement();
        }

        private static void WriteRawJSONElementAsDecodedObject(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, object o)
        {
            if (o == null)
                xmlWriter.WriteRawJSONElementNull(elementName);
            else if (o is IEnumerable)
                xmlWriter.WriteRawJSONElement(elementName, (IEnumerable)(o));
            else
            {
                var oType = o.GetType();

                bool hasDataContractAttribute = (!oType.GetCustomAttributes(typeof(DataContractAttribute), false).IsNullOrEmpty());
                bool hasCollectionDataContractAttribute = (!oType.GetCustomAttributes(typeof(CollectionDataContractAttribute), false).IsNullOrEmpty());
                //bool hasSerializableAttribute = (!oType.GetCustomAttributes(typeof(SerializableAttribute), false).IsNullOrEmpty());

                if (hasDataContractAttribute || hasCollectionDataContractAttribute /*|| hasSerializableAttribute*/)
                {
                    new DataContractJsonSerializer(oType).WriteObject(xmlWriter, o);
                }
                else
                {
                    xmlWriter.WriteRawJSONElementVCE(elementName, ValueContainer.CreateFromObject(o));
                }
            }
        }

        private static void WriteRawJSONElementVCE(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, ValueContainer vc)
        {
            ValueContainerEnvelope vce = new ValueContainerEnvelope() { VC = vc };
            DataContractJsonAdapter<ValueContainerEnvelope> vceDCA = new DataContractJsonAdapter<ValueContainerEnvelope>();

            var vceAsStr = vceDCA.ConvertObjectToString(vce);
            xmlWriter.WriteRawJSONElement(elementName, vceAsStr);
        }

        private static void WriteRawJSONElement(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, string s)
        {
            if (s == null)
            {
                xmlWriter.WriteRawJSONElementNull(elementName);
            }
            else
            {
                xmlWriter.WriteStartElement(elementName);
                xmlWriter.WriteAttributeString("type", "string");
                xmlWriter.WriteValue(s);
                xmlWriter.WriteEndElement();
            }
        }

        private static void WriteRawJSONElement(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, INamedValueSet nvs)
        {
            if (nvs == null)
            {
                xmlWriter.WriteRawJSONElementNull(elementName);
            }
            else
            {
                xmlWriter.WriteStartElement(elementName);
                xmlWriter.WriteAttributeString("type", "object");

                foreach (var nv in nvs)
                {
                    if (nv.Name.IsNeitherNullNorEmpty())
                        xmlWriter.WriteRawJSONElement(nv.Name, nv.VC);
                }

                xmlWriter.WriteEndElement();
            }
        }

        private static void WriteRawJSONElement(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, INamedValue nv)
        {
            if (nv == null)
            {
                xmlWriter.WriteRawJSONElementNull(elementName);
            }
            else
            {
                xmlWriter.WriteStartElement(elementName);
                xmlWriter.WriteAttributeString("type", "object");

                if (nv.Name.IsNeitherNullNorEmpty())
                    xmlWriter.WriteRawJSONElement(nv.Name, nv.VC);

                xmlWriter.WriteEndElement();
            }
        }

        private static void WriteRawJSONElement(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, IEnumerable<string> stringSet)
        {
            if (stringSet == null)
            {
                xmlWriter.WriteRawJSONElementNull(elementName);
            }
            else
            {
                xmlWriter.WriteStartElement(elementName);
                xmlWriter.WriteAttributeString("type", "array");

                foreach (var s in stringSet)
                    xmlWriter.WriteRawJSONElement("item", s);

                xmlWriter.WriteEndElement();
            }
        }

        private static void WriteRawJSONElement(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, IEnumerable<ValueContainer> vcSet)
        {
            if (vcSet == null)
            {
                xmlWriter.WriteRawJSONElementNull(elementName);
            }
            else
            {
                xmlWriter.WriteStartElement(elementName);
                xmlWriter.WriteAttributeString("type", "array");

                foreach (var vc in vcSet)
                    xmlWriter.WriteRawJSONElement("item", vc);

                xmlWriter.WriteEndElement();
            }
        }

        private static void WriteRawJSONElement(this System.Xml.XmlDictionaryWriter xmlWriter, string elementName, IEnumerable objectSet)
        {
            if (objectSet == null)
            {
                xmlWriter.WriteRawJSONElementNull(elementName);
            }
            else
            {

                xmlWriter.WriteStartElement(elementName);
                xmlWriter.WriteAttributeString("type", "array");

                foreach (var o in objectSet)
                    xmlWriter.WriteRawJSONElement("item", ValueContainer.CreateFromObject(o));

                xmlWriter.WriteEndElement();
            }
        }

        /// <summary>
        /// Shortcut method that allows the caller to attempt to access an indexed sub-item in a "List" type ValueContainer's contents (L or LS).
        /// If the given vc does not contain a supported type or the given index is not valid then the method will return the given <paramref name="fallbackValue"/>.
        /// </summary>
        public static ValueContainer SafeAccess(this ValueContainer vc, int index, ValueContainer fallbackValue = default(ValueContainer))
        {
            switch (vc.cvt)
            {
                case ContainerStorageType.L:
                    {
                        ReadOnlyIList<ValueContainer> rol = vc.o as ReadOnlyIList<ValueContainer>;

                        if (rol.IsSafeIndex(index))
                            return rol[index];

                        break;
                    }

                case ContainerStorageType.LS:
                    {
                        ReadOnlyIList<string> rol = vc.o as ReadOnlyIList<string>;

                        if (rol.IsSafeIndex(index))
                            return ValueContainer.Create(rol[index]);

                        break;
                    }
                default:
                    break;
            }

            return fallbackValue;
        }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.Bo</summary>
        public static ValueContainer CreateVC(this bool value) { return ValueContainer.CreateBo(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.I1</summary>
        public static ValueContainer CreateVC(this sbyte value) { return ValueContainer.CreateI1(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.I2</summary>
        public static ValueContainer CreateVC(this short value) { return ValueContainer.CreateI2(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.I4</summary>
        public static ValueContainer CreateVC(this int value) { return ValueContainer.CreateI4(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.I8</summary>
        public static ValueContainer CreateVC(this long value) { return ValueContainer.CreateI8(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.U1</summary>
        public static ValueContainer CreateVC(this byte value) { return ValueContainer.CreateU1(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.U2</summary>
        public static ValueContainer CreateVC(this ushort value) { return ValueContainer.CreateU2(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.U4</summary>
        public static ValueContainer CreateVC(this uint value) { return ValueContainer.CreateU4(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.U8</summary>
        public static ValueContainer CreateVC(this ulong value) { return ValueContainer.CreateU8(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.F4</summary>
        public static ValueContainer CreateVC(this float value) { return ValueContainer.CreateF4(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.F8</summary>
        public static ValueContainer CreateVC(this double value) { return ValueContainer.CreateF8(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.DateTime</summary>
        public static ValueContainer CreateVC(this DateTime value) { return ValueContainer.CreateDT(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.TimeStamp</summary>
        public static ValueContainer CreateVC(this TimeSpan value) { return ValueContainer.CreateTS(value); }

        /// <summary>Creates a ValueContainer to contain the given <paramref name="value"/> using ContainerStorageType.A</summary>
        public static ValueContainer CreateVC(this string value) { return ValueContainer.CreateA(value); }
    }

    /// <summary>
    /// Exception that may be thrown by the ValueContainer's GetValue method.  Message will give more details of the problem.
    /// </summary>
    public class ValueContainerGetValueException : System.Exception
    {
        /// <summary>
        /// Constructor requires message and innerException (which may be null)
        /// </summary>
        public ValueContainerGetValueException(string message, System.Exception innerException = null) 
            : base(message, innerException) 
        { }
    }

    #endregion

    #region VCRange

    /// <summary>
    /// Helper struct used to define a range using an optional Low and optional High limit.
    /// </summary>
    public struct VCRange : IEquatable<VCRange>
    {
        /// <summary>When this property has been given a non-Empty numeric value it defines the Low end of the range</summary>
        public ValueContainer Low { get; set; }

        /// <summary>When this property has been given a non-Empty numeric value it defines the High end of the range</summary>
        public ValueContainer High { get; set; }

        public bool IsNullOrEmpty { get { return Low.IsNullOrEmpty && High.IsNullOrEmpty; } }

        /// <summary>
        /// Returns true if the given <paramref name="vc"/> is in the range defined by [Low .. High] where the Low and High limits are only tested if they are NeitherNullNorEmpty.  
        /// <para/>(<paramref name="vc"/> &gt;= Low || Low.IsNullOrEmpty) &amp;&amp; (<paramref name="vc"/> &lt;= High || High.IsNullOrEmpty)
        /// </summary>
        public bool IsInRange(ValueContainer vc)
        {
            bool inRange = (Low.IsNullOrEmpty || vc.CompareTo(Low, fallbackValue: -1) >= 0)
                        && (High.IsNullOrEmpty || vc.CompareTo(High, fallbackValue: 1) <= 0)
                        ;

            return inRange;
        }

        /// <summary>
        /// Debugging and logging helper method
        /// </summary>
        public override string ToString()
        {
            return IsNullOrEmpty ? "[NoRange]" : "{0}..{1}".CheckedFormat(Low, High);
        }

        /// <summary>
        /// IEquality{VCRange} implementation method.  Returns true if this VCRange has the same contents as the given <paramref name="other"/> VCRange.
        /// </summary>
        public bool Equals(VCRange other)
        {
            return Low.Equals(other.Low) && High.Equals(other.High);
        }
    }

    #endregion

    #region BiArray: Byte array variant for use with Bi type.

    /// <summary>
    /// This class is a variant on ReadOnlyIList{byte} that is used when deserializing from Bi arrays and which may be used by a client to force serialization of a byte array type using the Bi type.
    /// </summary>
    public class BiArray : ReadOnlyIList<byte>
    {
        /// <summary>
        /// Singleton Empty BiArray instance.
        /// </summary>
        public static new BiArray Empty { get { return _empty; } }
        private static readonly BiArray _empty = new BiArray();

        /// <summary>
        /// Constructs this instance as a read only set of bytes copied from the given <paramref name="copyFromArray"/>.  Will result in an empty array if given null.
        /// </summary>
        public BiArray(byte[] copyFromArray = null)
            : base(copyFromArray)
        { }

        /// <summary>
        /// Constructs the contents of this instance from the set of explicitly defined items (<paramref name="firstItem"/> followed by 0 or <paramref name="moreItemsArray"/> items).
        /// </summary>
        public BiArray(byte firstItem, params byte[] moreItemsArray)
            : base(firstItem, moreItemsArray)
        { }

        /// <summary>
        /// Constructs the contents of this instance based on the contents of the given <paramref name="sourceItemSet"/>.  
        /// If the given <paramref name="sourceItemSet"/> is null then this method will be constructed as an empty list.
        /// </summary>
        public BiArray(IEnumerable<byte> sourceItemSet)
            : base(sourceItemSet)
        { }

        /// <summary>
        /// Returns a byte array generated from the contents of the given <paramref name="biArray"/> instance, or the empty array if the given <paramref name="biArray"/> was null.
        /// </summary>
        public static implicit operator byte[] (BiArray biArray)
        {
            return biArray.SafeToArray();
        }

        /// <summary>
        /// Debugging and logging assistant.
        /// </summary>
        public override string ToString()
        {
            return "[BiArray{0}]".CheckedFormat(String.Concat(this.Select(b => " {0}".CheckedFormat(b))));
        }
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
    [DataContract(Name = "VC", Namespace = Constants.ModularNameSpace), Serializable]
    public class ValueContainerEnvelope : IEquatable<ValueContainerEnvelope>
    {
        /// <summary>Default constructor.  By default the contained value will read as Empty until it has been explicitly set to some other value.</summary>
        public ValueContainerEnvelope() { }

        #region ValueContainer storage and construction default state handling that is compatible with reality of DataContract deserialization

        /// <summary>
        /// Gives the ValueContainer that this evenelope is being used to serialize from and/or has been deserialized to.
        /// </summary>
        public ValueContainer VC 
        { 
            get { return vc; }
            set 
            { 
                vc = value;
                onSerializingCanBeSkipped = false; 
            } 
        }

        /// <summary>
        /// Backing storage for the VC property.
        /// </summary>
        private ValueContainer vc;

        /// <summary>
        /// Boolean to indicate if the backing store as been set through the VC property since the last time the contents were serialized.
        /// Each time the VC property is directly assigned, internally during deserialization or externally by a client, this value is set to false.
        /// Each time this object is being serialized via the OnSerializing method, this property will be set to true and any immediately next OnSerializing call will skip the related internal work.
        /// </summary>
        private bool onSerializingCanBeSkipped = false;

        #endregion

        #region Equality testing

        public bool Equals(ValueContainerEnvelope other)
        {
            return (other != null && VC.Equals(other.VC));
        }

        public override bool Equals(object other)
        {
            if (other is ValueContainerEnvelope)
                return Equals(other as ValueContainerEnvelope);
            else
                return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion

        #region serialization helper properties each as optional DataMembers

        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            if (!onSerializingCanBeSkipped)
            {
                ClearDecodedProperties();

                ContainerStorageType useCST = vc.cvt;
                if (useCST == ContainerStorageType.Object && vc.o != null)
                {
                    if ((vc.o is string[]) || (vc.o is IList<string>))
                        useCST = ContainerStorageType.IListOfString;
                    else if ((vc.o is ValueContainer[]) || (vc.o is IList<ValueContainer>))
                        useCST = ContainerStorageType.IListOfVC;
                    else if (vc.o is INamedValueSet)
                        useCST = ContainerStorageType.INamedValueSet;
                    else if (vc.o is INamedValue)
                        useCST = ContainerStorageType.INamedValue;
                }
                else if (useCST == ContainerStorageType.Custom)
                {
                    useCST = ContainerStorageType.None;
                }

                switch (useCST)
                {
                    case ContainerStorageType.Object:
                        if (vc.o != null)
                        {
                            if (vc.o is INamedValue)
                                nviGetValue = (vc.o as INamedValue).ConvertToReadOnly() ?? NamedValue.Empty;
                            else if (vc.o is INamedValueSet)
                                nvsGetValue = (vc.o as INamedValueSet).ConvertToReadOnly() ?? NamedValueSet.Empty;
                            else if (vc.o is System.Array)
                                vceArrayHelper = new VCEArrayHelper(vc.o as System.Array);
                            else if (vc.o is System.Array || vc.o is BiArray)
                                vceArrayHelper = new VCEArrayHelper(vc.o as BiArray);
                            else
                            {
                                var oType = vc.o.GetType();

                                try
                                {
                                    CustomSerialization.ITypeSerializerItem tsi = CustomSerialization.CustomSerialization.Instance.GetCustomTypeSerializerItemFor(oType);
                                    if (tsi != null)
                                        tavcGetValue = tsi.Serialize(vc.o);
                                    else
                                        sGetValue = "Failed to obtain a valid Custom TypeSerializerItem for type '{0}' [{1}]".CheckedFormat(oType.GetTypeDigestName(), vc.o);
                                }
                                catch (System.Exception ex)
                                {
                                    sGetValue = "Serialization failed for type '{0}' value '{1}': {2}".CheckedFormat(oType.GetTypeDigestName(), vc.o, ex.ToString(ExceptionFormat.TypeAndMessage));
                                }
                            }
                        }
                        else
                        {
                            nullGetValue = true;
                        }

                        break;
                    case ContainerStorageType.String: 
                        sGetValue = vc.o as string; 
                        break;
                    case ContainerStorageType.IListOfString:
                        slGetValue = new Details.SL(vc.GetValueLS(rethrow: false)); 
                        break;
                    case ContainerStorageType.IListOfVC: 
                        vcaGetValue = vc.GetValueL(rethrow: false).Select(vcItem => new ValueContainerEnvelope() { VC = vcItem }).ToArray(); 
                        break;
                    case ContainerStorageType.INamedValueSet:
                        nvsGetValue = vc.GetValueNVS(rethrow: false).ConvertToReadOnly();
                        break;
                    case ContainerStorageType.INamedValue:
                        nviGetValue = vc.GetValueNV(rethrow: false).ConvertToReadOnly();
                        break;
                }

                onSerializingCanBeSkipped = true;
            }
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            if (vc.cvt != ContainerStorageType.None || onSerializingCanBeSkipped)
                VC = ValueContainer.Empty;
        }

        void ClearDecodedProperties()
        {
            nullGetValue = false;
            oGetValue = null;
            nviGetValue = null;
            nvsGetValue = null;
            vceArrayHelper = null;
            sGetValue = null;
            slGetValue = null;
            tavcGetValue = null;
            vcaGetValue = null;
        }

        [NonSerialized]
        private bool nullGetValue;

        [NonSerialized]
        private object oGetValue;

        [NonSerialized]
        private NamedValue nviGetValue;

        [NonSerialized]
        private NamedValueSet nvsGetValue;

        [NonSerialized]
        private VCEArrayHelper vceArrayHelper;

        [NonSerialized]
        private string sGetValue;

        [NonSerialized]
        private Details.SL slGetValue;

        [NonSerialized]
        private CustomSerialization.TypeAndValueCarrier tavcGetValue;

        [NonSerialized]
        private ValueContainerEnvelope[] vcaGetValue;

        // expectation is that exactly one property/element will produce a non-default value and this will define both the ContainerStorageType and the value for the deserializer to produce.
        // if no listed property produces a non-default value then the envelope will be empty and the deserializer will produce the default (empty) contained value.

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private bool Null { get { return nullGetValue; } set { if (value) VC = new ValueContainer() { cvt = ContainerStorageType.Object, o = null }; } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "o")]
        private object O { get { return oGetValue; } set { VC = new ValueContainer() { cvt = ContainerStorageType.Object, o = value }; } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "nvi")]
        private NamedValue NVI { get { return nviGetValue; } set { VC = ValueContainer.Create(value.MakeReadOnly()); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "nvs")]
        private NamedValueSet NVS { get { return nvsGetValue; } set { VC = ValueContainer.Create(value.MakeReadOnly()); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "array")]
        private VCEArrayHelper Array { get { return vceArrayHelper; } set { VC = value.VC; } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "s")]
        private string S { get { return sGetValue; } set { VC = new ValueContainer() { cvt = ContainerStorageType.String, o = value }; } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "sl")]
        private Details.SL SL { get { return ((vc.cvt == ContainerStorageType.IListOfString) ? new Details.SL(vc.o as IList<String>) : null); } set { VC = new ValueContainer() { cvt = ContainerStorageType.IListOfString, o = new ReadOnlyIList<string>(value) }; } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "tavc")]
        private CustomSerialization.TypeAndValueCarrier TAVC 
        { 
            get { return tavcGetValue; } 
            set 
            {
                try
                {
                    CustomSerialization.ITypeSerializerItem tsi = CustomSerialization.CustomSerialization.Instance.GetCustomTypeSerializerItemFor(value);
                    VC = ValueContainer.CreateFromObject(tsi.Deserialize(value));
                }
                catch (System.Exception ex)
                {
                    VC = ValueContainer.Create("{0} assign from tavc '{1}' failed: {2}".CheckedFormat(Fcns.CurrentMethodName, value, ex.ToString(ExceptionFormat.Full)));
                }
            } 
        }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "vca")]
        private ValueContainerEnvelope[] VCA { get { return vcaGetValue; } set { VC = ValueContainer.Create(new ReadOnlyIList<ValueContainer>(value.Select((ve) => ve.VC))); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "b")]
        private bool? B { get { return ((vc.cvt == ContainerStorageType.Boolean) ? (bool?)vc.u.b : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Boolean, u = new ValueContainer.Union() { b = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "bi")]
        private Byte ? Bi { get { return ((vc.cvt == ContainerStorageType.Binary) ? (Byte?)vc.u.bi : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Binary, u = new ValueContainer.Union() { bi = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "f32")]
        private Single? F32 { get { return ((vc.cvt == ContainerStorageType.Single) ? (Single?)vc.u.f32 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Single, u = new ValueContainer.Union() { f32 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "f64")]
        private Double? F64 { get { return ((vc.cvt == ContainerStorageType.Double) ? (Double?)vc.u.f64 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Double, u = new ValueContainer.Union() { f64 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "i8")]
        private SByte? I8 { get { return ((vc.cvt == ContainerStorageType.SByte) ? (SByte?)vc.u.i8 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.SByte, u = new ValueContainer.Union() { i8 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "i16")]
        private Int16? I16 { get { return ((vc.cvt == ContainerStorageType.Int16) ? (Int16?)vc.u.i16 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Int16, u = new ValueContainer.Union() { i16 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "i32")]
        private Int32? I32 { get { return ((vc.cvt == ContainerStorageType.Int32) ? (Int32?)vc.u.i32 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Int32, u = new ValueContainer.Union() { i32 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "i64")]
        private Int64? I64 { get { return ((vc.cvt == ContainerStorageType.Int64) ? (Int64?)vc.u.i64 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Int64, u = new ValueContainer.Union() { i64 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "u8")]
        private Byte? U8 { get { return ((vc.cvt == ContainerStorageType.Byte) ? (Byte?)vc.u.u8 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.Byte, u = new ValueContainer.Union() { u8 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "u16")]
        private UInt16? U16 { get { return ((vc.cvt == ContainerStorageType.UInt16) ? (UInt16?)vc.u.u16 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.UInt16, u = new ValueContainer.Union() { u16 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "u32")]
        private UInt32? U32 { get { return ((vc.cvt == ContainerStorageType.UInt32) ? (UInt32?)vc.u.u32 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.UInt32, u = new ValueContainer.Union() { u32 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "u64")]
        private UInt64? U64 { get { return ((vc.cvt == ContainerStorageType.UInt64) ? (UInt64?)vc.u.u64 : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.UInt64, u = new ValueContainer.Union() { u64 = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "ts")]
        private TimeSpan? TS { get { return ((vc.cvt == ContainerStorageType.TimeSpan) ? (TimeSpan?)vc.u.TimeSpan : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.TimeSpan, u = new ValueContainer.Union() { TimeSpan = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "dt")]
        private DateTime? DT { get { return ((vc.cvt == ContainerStorageType.DateTime) ? (DateTime?)vc.u.DateTime : null); } set { VC = (value.HasValue ? new ValueContainer() { cvt = ContainerStorageType.DateTime, u = new ValueContainer.Union() { DateTime = value.GetValueOrDefault() } } : ValueContainer.Null); } }

        #endregion

        /// <summary>Override ToString for logging and debugging.</summary>
        public override string ToString()
        {
            return VC.ToStringSML();
        }
    }

    /// <summary>
    /// This class is used to support serializing arrays in value container envelopes.  
    /// It prevents the VCE from having a lot more nullable properties that need to be traversed when serializing VCs where array serialization is relatively rare.
    /// </summary>
    [DataContract(Name = "VCEAH", Namespace = Constants.ModularNameSpace), Serializable]
    public class VCEArrayHelper
    {
        public VCEArrayHelper(System.Array array) 
        {
            if (array != null)
            {
                Type arrayType = array.GetType();
                if (arrayType == typeof(bool[]))
                    bo = array as bool[];
                else if (arrayType == typeof(sbyte[]))
                    i1 = array as sbyte[];
                else if (arrayType == typeof(short[]))
                    i2 = array as short[];
                else if (arrayType == typeof(int[]))
                    i4 = array as int[];
                else if (arrayType == typeof(long[]))
                    i8 = array as long[];
                else if (arrayType == typeof(byte[]))
                    u1 = array as byte[];
                else if (arrayType == typeof(ushort[]))
                    u2 = array as ushort[];
                else if (arrayType == typeof(uint[]))
                    u4 = array as uint[];
                else if (arrayType == typeof(ulong[]))
                    u8 = array as ulong[];
                else if (arrayType == typeof(float[]))
                    f4 = array as float[];
                else if (arrayType == typeof(double[]))
                    f8 = array as double[];
            }
        }

        public VCEArrayHelper(BiArray biArray)
        {
            if (biArray != null)
                bi = biArray;
        }

        public ValueContainer VC { get; private set; }

        [NonSerialized]
        private readonly bool[] bo;

        [NonSerialized]
        private readonly sbyte[] i1;

        [NonSerialized]
        private readonly short[] i2;

        [NonSerialized]
        private readonly int[] i4;

        [NonSerialized]
        private readonly long[] i8;

        [NonSerialized]
        private readonly byte[] u1;

        [NonSerialized]
        private readonly ushort[] u2;

        [NonSerialized]
        private readonly uint[] u4;

        [NonSerialized]
        private readonly ulong[] u8;

        [NonSerialized]
        private readonly float[] f4;

        [NonSerialized]
        private readonly double[] f8;

        [NonSerialized]
        private readonly BiArray bi;

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private bool[] Bo { get { return bo; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private sbyte[] I1 { get { return i1; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private short[] I2 { get { return i2; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private int[] I4 { get { return i4; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private long[] I8 { get { return i8; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private byte[] U1 { get { return u1; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private ushort[] U2 { get { return u2; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private uint[] U4 { get { return u4; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private ulong[] U8 { get { return u8; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private float[] F4 { get { return f4; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private double[] F8 { get { return f8; } set { VC = ValueContainer.Create(value); } }

        [DataMember(EmitDefaultValue = false, IsRequired = false)]
        private byte [] Bi { get { return ((bi != null) ? bi.ToArray() : null); } set { VC = ValueContainer.Create(new BiArray(value)); } }
    }

    namespace Details
    {
        /// <summary>Internal - no documentation provided</summary>
        [CollectionDataContract(ItemName = "s", Namespace = Constants.ModularNameSpace)]
        internal class SL : List<String>
        {
            /// <summary>Internal - no documentation provided</summary>
            public SL() 
            { }

            /// <summary>Internal - no documentation provided</summary>
            public SL(IEnumerable<string> strings) 
                : base(strings ?? emptyIListOfString) 
            { }

            private static readonly IList<string> emptyIListOfString = ReadOnlyIList<string>.Empty;
        }
    }

    #endregion

    #region CustomSerialization namespace and related types

    namespace CustomSerialization
    {
        #region TypeAndValueCarrier

        /// <summary>
        /// This is the class used to carry a serialized type and value when using CustomSerialization.
        /// Deserialization requires that there is a suitable (matching) generic or type specific deserializer that has (already) been registered with the
        /// appropriate CustomSerialization instance (usually the singleton).
        /// <para/>This class implements immutable behavior.
        /// </summary>
        [DataContract(Namespace = Constants.ModularNameSpace)]
        public class TypeAndValueCarrier
        {
            /// <summary>
            /// Required constructor - allows the caller to construct an immutable instance with any/all properties initialized to any correspondingly given parameter values.
            /// </summary>
            public TypeAndValueCarrier(string typeStr = null, string assemblyFileName = null, string factoryName = null, string valueStr = null, byte [] valueByteArray = null, string errorCode = null) 
            {
                TypeStr = typeStr;
                AssemblyFileName = assemblyFileName;
                FactoryName = factoryName;
                ValueStr = valueStr;
                ValueByteArray = valueByteArray;
                _ErrorCode = errorCode.MapEmptyToNull();
            }

            /// <summary>
            /// Carries a text description of the deserialized type
            /// </summary>
            [DataMember(Order = 10, Name = "tyStr", EmitDefaultValue = false, IsRequired = false)]
            public string TypeStr { get; private set; }

            /// <summary>
            /// Carries a text description of the deserialized type
            /// </summary>
            [DataMember(Order = 11, Name = "afn", EmitDefaultValue = false, IsRequired = false)]
            public string AssemblyFileName { get; private set; }

            /// <summary>
            /// Carries a name of the factory object that was used to generate this serialization
            /// </summary>
            [DataMember(Order = 20, Name = "fName", EmitDefaultValue = false, IsRequired = false)]
            public string FactoryName { get { return factoryName.MapNullOrEmptyTo(null); } private set { factoryName = value; } }
            private string factoryName;

            /// <summary>
            /// Carries the value serialized as a string.
            /// </summary>
            [DataMember(Order = 100, Name = "vStr", EmitDefaultValue = false, IsRequired = false)]
            public string ValueStr { get; private set; }

            /// <summary>
            /// Carries the value serialized as a byte array (usually from the BinaryFormatter).
            /// </summary>
            [DataMember(Order = 200, Name = "vBa", EmitDefaultValue = false, IsRequired = false)]
            public byte[] ValueByteArray { get; private set; }

            /// <summary>
            /// Gives any error code assocaited with the generation of conveayance of this object
            /// </summary>
            public string ErrorCode { get { return _ErrorCode.MapNullToEmpty(); } }

            [DataMember(Order = 300, Name = "ec", EmitDefaultValue = false, IsRequired = false)]
            private string _ErrorCode = null;

            /// <summary>
            /// Debugging and logging helper method
            /// </summary>
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                sb.CheckedAppendFormat("tyStr:{0} afn:{1} fName:{2}", TypeStr, AssemblyFileName, FactoryName);

                if (ValueStr != null)
                    sb.CheckedAppendFormat(" vStr:[{0}]", ValueStr.GenerateSquareBracketEscapedVersion());

                if (ValueByteArray != null)
                    sb.CheckedAppendFormat(" vBa:[{0}]", ByteArrayTranscoders.HexStringTranscoder.Encode(ValueByteArray));

                if (ValueStr == null && ValueByteArray == null)
                    sb.CheckedAppendFormat(" noValue");

                if (_ErrorCode.IsNeitherNullNorEmpty())
                    sb.CheckedAppendFormat(" ec:'{0}'", _ErrorCode);

                return sb.ToString();
            }
        }

        #endregion

        /// <summary>
        /// This interface defines the publically usable information for a custom type serializer.
        /// Under CustomSerialization objects which implement this interface are created by one of the registered or fallback factories
        /// which can be used to Serialize and Deserialize the indicated type using Serialize and Deserialize methods defined here.
        /// <para/>NOTE: instances of this type must support re-enterant use of the Serialize and Deserialize methods as this object may be shared and used by many clients concurrently.
        /// </summary>
        public interface ITypeSerializerItem
        {
            /// <summary>Gives the Type instance of the type that this serializer is intended to be used with.  May be null.</summary>
            Type TargetType { get; }

            /// <summary>Gives the "name" of a Type that this serializer is intended to be used with.  May be null, empty or may be an arbitrary string that is only intended for use with a specific factory instance.</summary>
            string TargetTypeStr { get; }

            /// <summary>Gives the file name of the Assemly that this type came from.  May be null.</summary>
            string AssemblyFileName { get; }

            /// <summary>Gives the name of the factory instance that produced this serializer item.  May be null, or empty.</summary>
            string FactoryName { get; }

            /// <summary>
            /// This method is used by clients to generate a TypeAndValueCarrier for the given object.  
            /// If the given object is not supported, or cannot otherwise be serialized using this item then the method must throw a CustomSerializerException that describes the issue encountered.
            /// <para/>NOTE: this method must support re-enterant use as this object may be shared and used by many clients concurrently.
            /// </summary>
            TypeAndValueCarrier Serialize(object valueObject);

            /// <summary>
            /// This method is used by clients to generate an object from the carried ValueStr or ValueByteArray contents from the given TypeAndValueCarrier.  
            /// This method may also use the included TypeStr information in this process if needed.  
            /// If no valid deserialized object can be created from the carried ValueStr and/or ValueByteArray then the method must throw a CustomSerializerException that describes the issue encountered.
            /// <para/>NOTE: this method must support re-enterant use as this object may be shared and used by many clients concurrently.
            /// </summary>
            object Deserialize(TypeAndValueCarrier valueCarrier);
        }

        /// <summary>
        /// Exception that is generally used with issues encountered during CustomSerialization use.
        /// </summary>
        public class CustomSerializerException : System.Exception
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            public CustomSerializerException(string message = null, System.Exception innerException = null)
                : base(message, innerException)
            { }
        }

        /// <summary>
        /// This interface defines the public interface that must be supported by any ITypeSerializerItem factory so that it can be used with a CustomSerializer instance.
        /// </summary>
        public interface ITypeSerializerItemFactory
        {
            /// <summary>Gives the "name" of this factory instance.  May be null, or empty.  If non-empty this will used during deserialization to re-order deserializer searching if a matching factory name is found.</summary>
            string FactoryName { get; }

            /// <summary>
            /// This method attempts to generate and return an ITypeSerializerItem that can be used to serialize the given type.  If this factory does not support serializing the given type it must return null.
            /// </summary>
            ITypeSerializerItem AttemptToGenerateSerializationSpecItemFor(Type targetType, string targetTypeStr, string assemblyFileName);

            /// <summary>
            /// This method attempts to generate and return an ITypeSerializerItem that can be used to deserialize TypeAndValueCarrier objects that contain the given targetTypeStr value.
            /// </summary>
            ITypeSerializerItem AttemptToGenerateSerializationSpecItemFor(string targetTypeStr, string assemblyFileName);
        }

        /// <summary>
        /// This inteface defines the public API that must be implemented by object that can be used for CustomSerialization.  
        /// Normally the client simply makes use of the default CustomSerialization.Instance instance which automatically constructs a CustomSerialization instance for this purpose.
        /// If the application would like to replace this instance with one of another type then it may provide an instance of another class that implements this interface for use in place of a default instance.
        /// </summary>
        public interface ICustomSerialization
        {
            /// <summary>Defines the ITypeSerializerItemFactory instance that should always be used last to attempt to generate ITypeSerializerItems for types that no other factory accepts.</summary>
            ITypeSerializerItemFactory FallbackFactory { get; set; }

            /// <summary>Adds the given ITypeSerializerItemFactory instance onto the end of the set of available ITypeSerializerItemFactory objects</summary>
            CustomSerialization Add(ITypeSerializerItemFactory typeSerializerItemFactory);

            /// <summary>Adds the given set of ITypeSerializerItemFactory instances onto the end of the set of available ITypeSerializerItemFactory objects</summary>
            CustomSerialization AddRange(IEnumerable<ITypeSerializerItemFactory> typeSerializerItemFactorySet);

            /// <summary>Used by clients to obtain an ITypeSerializerItem for the given targetType.  Optional factoryName may be used to attempt to obtain a type serializer from that factory first, provided that no other type serializer has already been generated for this type.</summary>
            ITypeSerializerItem GetCustomTypeSerializerItemFor(Type targetType, string factoryName = null);

            /// <summary>Used by clients to obtain an ITypeSerializerItem for the given targetTypeStr.  Optional factoryName may be used to attempt to obtain a type serializer from that factory first, provided that no other type serializer has already been generated for this typeStr</summary>
            ITypeSerializerItem GetCustomTypeSerializerItemFor(string targetTypeStr, string assemblyFileName = null, string factoryName = null);

            /// <summary>Used by clients to obtain an ITypeSerializerItem for the type (and factoryName) contained in the TypeAndValueCarrier instance.</summary>
            ITypeSerializerItem GetCustomTypeSerializerItemFor(TypeAndValueCarrier typeAndValueCarrier);
        }

        /// <summary>
        /// This is a hybrid class that is used to compartmentailize the CustomSerialization logic.
        /// It contains a settable (AutoConstructIfNeeded) singleton Instance property.
        /// It defines instance methods to register ISpecItemFactory objects using Add and AddRange methods and using the FallbackFactory setter
        /// it provides instance methods that attempt to use the various application provided ITypeSerializerItemFactory, combined with its internal BinaryFormatter version to 
        /// generate ITypeSerializerItem objects that are used to actually perform serialization and deserialization in client code.
        /// </summary>
        public class CustomSerialization : ICustomSerialization
        {
            #region Singleton Instance (et. al.)

            public static ICustomSerialization Instance { get { return singletonInstanceHelper.Instance; } set { singletonInstanceHelper.Instance = value; } }
            private static SingletonHelperBase<ICustomSerialization> singletonInstanceHelper = new SingletonHelperBase<ICustomSerialization>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new CustomSerialization());

            #endregion

            #region ICustomSerialization

            ITypeSerializerItemFactory ICustomSerialization.FallbackFactory 
            { 
                get { lock (mutex) { return fallbackFactory; } } 
                set { lock (mutex) { fallbackFactory = value; useFactoryItemArray = null; } } 
            }
            ITypeSerializerItemFactory fallbackFactory = null;

            CustomSerialization ICustomSerialization.Add(ITypeSerializerItemFactory typeSerializerItemFactory)
            {
                lock (mutex)
                {
                    factoryItemList.Add(typeSerializerItemFactory);
                    useFactoryItemArray = null;
                }

                return this;
            }

            CustomSerialization ICustomSerialization.AddRange(IEnumerable<ITypeSerializerItemFactory> typeSerializerItemFactorySet)
            {
                lock (mutex)
                {
                    factoryItemList.AddRange(typeSerializerItemFactorySet);
                    useFactoryItemArray = null;
                }

                return this;
            }

            ITypeSerializerItem ICustomSerialization.GetCustomTypeSerializerItemFor(Type targetType, string factoryName) 
            {
                ITypeSerializerItem tsi = null;
                lock (mutex)
                {
                    if (typeToSpecItemDictionary.TryGetValue(targetType, out tsi) && tsi != null)
                        return tsi;

                    string targetTypeStr = targetType.ToString();
                    string assemblyFileName = targetType.Assembly.GetName().Name;

                    if (tsi == null)
                    {
                        UpdateFactoryItemArrayIfNeeded();

                        ITypeSerializerItemFactory namedFactory = null;
                        if (!factoryName.IsNullOrEmpty() && factoryNameToFactoryDictionary.TryGetValue(factoryName, out namedFactory) && namedFactory != null)
                            tsi = namedFactory.AttemptToGenerateSerializationSpecItemFor(targetType, targetTypeStr, assemblyFileName);
                    }

                    if (tsi == null)
                        tsi = useFactoryItemArray.Select(factory => factory.AttemptToGenerateSerializationSpecItemFor(targetType, targetTypeStr, assemblyFileName)).FirstOrDefault(factory => factory != null);

                    if (tsi != null)
                        typeToSpecItemDictionary[targetType] = tsi;
                    else
                        new CustomSerializerException("could not create TypeSerializerItem for type:'{0}' factory:'{1}'".CheckedFormat(targetType, factoryName)).Throw();

                    return tsi;
                }
            }

            Dictionary<string, Type> typeNameToTypeDictionary = new Dictionary<string, Type>();

            ITypeSerializerItem ICustomSerialization.GetCustomTypeSerializerItemFor(string targetTypeStr, string assemblyFileName, string factoryName) 
            {
                ITypeSerializerItem tsi = null;

                lock (mutex)
                {
                    if (typeStrToSpecItemDictionary.TryGetValue(targetTypeStr, out tsi) && tsi != null)
                        return tsi;

                    if (tsi == null)
                    {
                        try
                        {
                            Type foundType = null;
                            if (!typeNameToTypeDictionary.TryGetValue(targetTypeStr, out foundType))
                            {
                                Assembly [] currentAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                                // if we can find an assembly with the same name as the original serializing type came from then try to find the type in that assembly first.
                                Assembly tryFirstAssembly = currentAssemblies.FirstOrDefault(assembly => assembly.GetName().Name == assemblyFileName);

                                if (tryFirstAssembly != null)
                                    foundType = tryFirstAssembly.GetType(targetTypeStr);

                                // if the type could not be found there (the indicated assembly is not currently loaded) then ask every assembly in the current app domain to parse the targetTypeStr.
                                // Take the first one that produces a type or null
                                if (foundType == null)
                                    foundType = currentAssemblies.Where(assembly => assembly != tryFirstAssembly).Select(assembly => assembly.GetType(targetTypeStr)).FirstOrDefault(t => t != null);

                                // save the foundType, even if null, in the dictionary so that we will not need to repeatedly look for the type the hard way.
                                typeNameToTypeDictionary[targetTypeStr] = foundType;
                            }

                            if (foundType != null)
                            {
                                ICustomSerialization me = this;
                                tsi = me.GetCustomTypeSerializerItemFor(foundType, factoryName);
                            }
                        }
                        catch
                        { }
                    }

                    if (tsi == null)
                    {
                        UpdateFactoryItemArrayIfNeeded();

                        ITypeSerializerItemFactory namedFactory = null;
                        if (!factoryName.IsNullOrEmpty() && factoryNameToFactoryDictionary.TryGetValue(factoryName, out namedFactory) && namedFactory != null)
                            tsi = namedFactory.AttemptToGenerateSerializationSpecItemFor(targetTypeStr, assemblyFileName);
                    }

                    if (tsi == null)
                        tsi = useFactoryItemArray.Select(factory => factory.AttemptToGenerateSerializationSpecItemFor(targetTypeStr, assemblyFileName)).FirstOrDefault(factory => factory != null);

                    if (tsi != null)
                        typeStrToSpecItemDictionary[targetTypeStr] = tsi;
                    else
                        new CustomSerializerException("could not create TypeSerializerItem for typeStr:'{0}' factory:'{1}'".CheckedFormat(targetTypeStr, factoryName)).Throw();

                    return tsi;
                }
            }

            ITypeSerializerItem ICustomSerialization.GetCustomTypeSerializerItemFor(TypeAndValueCarrier typeAndValueCarrier) 
            {
                ICustomSerialization me = this;

                return me.GetCustomTypeSerializerItemFor(typeAndValueCarrier.TypeStr, typeAndValueCarrier.AssemblyFileName, typeAndValueCarrier.FactoryName);
            }

            #endregion

            #region private fields and other internals

            private readonly object mutex = new object();

            private List<ITypeSerializerItemFactory> factoryItemList = new List<ITypeSerializerItemFactory>();

            private ITypeSerializerItemFactory[] useFactoryItemArray = null;
            private static readonly ITypeSerializerItemFactory jsonDCSerializerFactory = new JsonDataContractFallbackItemFactory() { FactoryName = "FallbackJsonDC" };
            private static readonly ITypeSerializerItemFactory binarySerializerFactory = new BinaryFormatterFallbackItemFactory() { FactoryName = "FallbackBinaryFormatter" };

            private Dictionary<string, ITypeSerializerItemFactory> factoryNameToFactoryDictionary = new Dictionary<string, ITypeSerializerItemFactory>();
            private Dictionary<string, ITypeSerializerItem> typeStrToSpecItemDictionary = new Dictionary<string, ITypeSerializerItem>();
            private Dictionary<Type, ITypeSerializerItem> typeToSpecItemDictionary = new Dictionary<Type, ITypeSerializerItem>();

            private void UpdateFactoryItemArrayIfNeeded()
            {
                if (useFactoryItemArray == null)
                {
                    useFactoryItemArray = factoryItemList.Concat(new[] { fallbackFactory }).Concat(new[] { jsonDCSerializerFactory, binarySerializerFactory }).Where(factory => factory != null).ToArray();

                    factoryNameToFactoryDictionary.Clear();

                    foreach (var factory in useFactoryItemArray.Where(factory => !factory.FactoryName.IsNullOrEmpty()))
                    {
                        if (!factoryNameToFactoryDictionary.ContainsKey(factory.FactoryName))
                            factoryNameToFactoryDictionary.Add(factory.FactoryName, factory);
                    }
                }
            }

            #endregion

            #region fallback Json Data Contract serializer implementation classes for use here

            private class JsonDataContractFallbackItemFactory : ITypeSerializerItemFactory
            {
                public string FactoryName { get; set; }

                public ITypeSerializerItem AttemptToGenerateSerializationSpecItemFor(Type targetType, string targetTypeStr, string assemblyFileName)
                {
                    bool hasDataContractAttribute = (!targetType.GetCustomAttributes(typeof(DataContractAttribute), false).IsNullOrEmpty());
                    bool hasCollectionDataContractAttribute = (!targetType.GetCustomAttributes(typeof(CollectionDataContractAttribute), false).IsNullOrEmpty());
                    bool hasSerializableAttribute = (!targetType.GetCustomAttributes(typeof(SerializableAttribute), false).IsNullOrEmpty());

                    if (hasDataContractAttribute || hasCollectionDataContractAttribute || hasSerializableAttribute)
                        return new JsonDataContractTypeSerializerItem(targetType: targetType, targetTypeStr: targetTypeStr, assemblyFileName: assemblyFileName, factoryName: FactoryName);
                    else
                        return null;
                }

                public ITypeSerializerItem AttemptToGenerateSerializationSpecItemFor(string targetTypeStr, string assemblyFileName)
                {
                    // note: this serializer does not support this factory method.  In all cases the TypeAndValueContainer should be convertable to a targetType or this serializer cannot be used.
                    return null;
                }
            }

            private class JsonDataContractTypeSerializerItem : TypeSerializerItemBase
            {
                public JsonDataContractTypeSerializerItem(Type targetType = null, string targetTypeStr = null, string assemblyFileName = null, string factoryName = null)
                    : base(targetType: targetType, targetTypeStr: targetTypeStr, assemblyFileName: assemblyFileName, factoryName: factoryName)
                { }

                public override TypeAndValueCarrier Serialize(object valueObject)
                {
                    DataContractJsonSerializer dcjs = new DataContractJsonSerializer(TargetType);

                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        dcjs.WriteObject(ms, valueObject);

                        return new TypeAndValueCarrier(typeStr: TargetTypeStr, assemblyFileName: AssemblyFileName, factoryName: FactoryName, valueStr: ByteArrayTranscoders.ByteStringTranscoder.Encode(ms.ToArray()));
                    }
                }

                public override object Deserialize(TypeAndValueCarrier valueCarrier)
                {
                    DataContractJsonSerializer dcjs = new DataContractJsonSerializer(TargetType);

                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream(ByteArrayTranscoders.ByteStringTranscoder.Decode(valueCarrier.ValueStr)))
                    {
                        return dcjs.ReadObject(ms);
                    }
                }
            }
            #endregion

            #region fallback BinaryFormatter implementation classes for use here

            private class BinaryFormatterFallbackItemFactory : ITypeSerializerItemFactory
            {
                public string FactoryName { get; set; }

                public ITypeSerializerItem AttemptToGenerateSerializationSpecItemFor(Type targetType, string targetTypeStr, string assemblyFileName)
                {
                    return new BinaryFormatterTypeSerializerItem(targetType: targetType, targetTypeStr: targetTypeStr, assemblyFileName: assemblyFileName, factoryName: FactoryName);
                }

                public ITypeSerializerItem AttemptToGenerateSerializationSpecItemFor(string targetTypeStr, string assemblyFileName)
                {
                    return new BinaryFormatterTypeSerializerItem(targetTypeStr: targetTypeStr, assemblyFileName: assemblyFileName, factoryName: FactoryName);
                }
            }

            private class BinaryFormatterTypeSerializerItem : TypeSerializerItemBase
            {
                public BinaryFormatterTypeSerializerItem(Type targetType = null, string targetTypeStr = null, string assemblyFileName = null, string factoryName = null)
                    : base(targetType: targetType, targetTypeStr: targetTypeStr, assemblyFileName: assemblyFileName, factoryName: factoryName)
                { }

                public override TypeAndValueCarrier Serialize(object valueObject)
                {
                    IFormatter formatter = new BinaryFormatter();
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        formatter.Serialize(ms, valueObject);

                        return new TypeAndValueCarrier(typeStr: TargetTypeStr, assemblyFileName: AssemblyFileName, factoryName: FactoryName, valueByteArray: ms.GetBuffer());
                    }
                }

                public override object Deserialize(TypeAndValueCarrier valueCarrier)
                {
                    IFormatter formatter = new BinaryFormatter();
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream(valueCarrier.ValueByteArray))
                    {
                        return formatter.Deserialize(ms);
                    }
                }
            }

            #endregion
        }

        /// <summary>
        /// This class provides an abstract base class implementation of the ITypeSerializerItem interface.
        /// Under CustomSerialization objects which implement this interface are created by one of the registered or fallback factories
        /// which can be used to Serialize and Deserialize the indicated type using Serialize and Deserialize methods defined here.
        /// <para/>NOTE: instances of this type must support re-enterant use of the Serialize and Deserialize methods as this object may be shared and
        /// used by many clients concurrently.
        /// </summary>
        public abstract class TypeSerializerItemBase : ITypeSerializerItem
        {
            public TypeSerializerItemBase(Type targetType = null, string targetTypeStr = null, string assemblyFileName = null, string factoryName = null)
            {
                TargetType = targetType ?? typeof(System.Object);
                TargetTypeStr = targetTypeStr;
                AssemblyFileName = assemblyFileName;
                FactoryName = factoryName;
            }

            public Type TargetType { get; private set; }
            public string TargetTypeStr { get; private set; }
            public string AssemblyFileName { get; private set; }
            public string FactoryName { get; private set; }

            public abstract TypeAndValueCarrier Serialize(object valueObject);
            public abstract object Deserialize(TypeAndValueCarrier valueCarrier);
        }
    }

    #endregion

    #region ReadOnly Named Values interfaces (INamedValueSet, INamedValue)

    /// <summary>
    /// This is the read-only interface to a NamedValueSet
    /// <para/>Warning: in order to preserve backward compatibility with the prior INamedValueSet behavior, and due to the limitations in the implementation of the new sub-set related behavior, 
    /// the IEnumerable interface has not generally been extended to support implicit access to the sub-sets.  Specific methods defined below (string indexed getter for example) have been
    /// extended to make implict use of TraversalType.EntireTree.  The caller should assume that TraversalType.TopLevelOnly will be used unless the method explicitly supports specification of
    /// the TraversalType or the method comments indicate otherwise.
    /// </summary>
    public interface INamedValueSet : IEnumerable<INamedValue>, IEquatable<INamedValueSet>
    {
        /// <summary>Returns true if this set, or any of its sub-sets when requested, contains a NamedValue for the given name (after sanitization).  This method is often use to test for the presence of keyword NV items.</summary>
        bool Contains(string name, TraversalType searchTraversalType = TraversalType.EntireTree);

        /// <summary>
        /// If a INamedValue exists for the given name (after sanitization), in this set, or its sub-sets when requested, then this method returns it.
        /// Otherwise this method returns NamedValue.Empty
        /// </summary>
        INamedValue GetNamedValue(string name, TraversalType searchTraversalType = TraversalType.EntireTree);

        /// <summary>
        /// If a INamedValue exists for the given name (after sanitization), in this set, or its sub-sets when requested, then this method returns the ValueContainer (VC) from that INamedValue.
        /// Otherwise this method returns ValueContainer.Empty.
        /// </summary>
        ValueContainer GetValue(string name, TraversalType searchTraversalType = TraversalType.EntireTree);

        /// <summary>
        /// Gets the number of NamedValues that are currently in this set.
        /// <para/>implicitly uses TraverseType.TopLevelOnly
        /// </summary>
        int Count { get; }

        /// <summary>
        /// String indexed property.  Returns the INamedValue from the set if it is found from the given name (after sanitization).
        /// Otherwise returns a new empty INamedValue using the given name (set contents are not changed by this).
        /// <para/>implicitly uses TraverseType.EntireTree
        /// </summary>
        INamedValue this[string name] { get; }

        /// <summary>
        /// String indexed property.  Returns the INamedValue from the set if it is found from the given name (after sanitization).  
        /// Supports searchTraversalType configuration.
        /// Otherwise returns a new empty INamedValue using the given name (set contents are not changed by this).
        /// </summary>
        INamedValue this[string name, TraversalType searchTraversalType] { get; }

        /// <summary>
        /// If a NamedValue exists in the set for the given name (after sanitization), then this method returns the index into the set (in enumerable order) for the corresponding NamedValue.
        /// Otherwise this method returns -1.
        /// <para/>implicitly uses TraverseType.TopLevelOnly
        /// </summary>
        int IndexOf(string name);

        /// <summary>
        /// indexed property.  Returns the indexed INamedValue from the set (if the given index is valid).
        /// <para/>implicitly uses TraverseType.TopLevelOnly
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">thrown if given index is less than 0 or it is greater than or equal to the set's Count</exception>
        INamedValue this[int index] { get; }

        /// <summary>
        /// This property returns true if the collection has been set to be read only.
        /// <para/>implicitly uses TraverseType.TopLevelOnly (SubSets are always retained and used as read-only sets)
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>Returns true if the this INamedValueSet has the same contents, in the same order, as the given <paramref name="other"/>.</summary>
        bool IsEqualTo(INamedValueSet other, TraversalType searchTraversalType = TraversalType.EntireTree, bool compareReadOnly = true);

        /// <summary>Custom ToString variant that allows the caller to determine if the ro/rw postfix should be included on this string result, and to determine if NamedValues with empty VCs should be treated as a keyword.</summary>
        string ToString(bool includeROorRW = true, bool treatNameWithEmptyVCAsKeyword = true, TraversalType traversalType = TraversalType.EntireTree);

        /// <summary>ToString variant that support SML like output format.</summary>
        string ToStringSML(TraversalType traversalType = TraversalType.EntireTree, string nvsNodeName = "NVS", string nvNodeName = "NV");

        /// <summary>
        /// Returns the approximate size of the contents in bytes. 
        /// <para/>implicitly uses TraverseType.EntireTree
        /// </summary>
        int EstimatedContentSizeInBytes { get; }

        /// <summary>
        /// Returns the approximate size of the contents in bytes. 
        /// </summary>
        int GetEstimatedContentSizeInBytes(TraversalType traversalType = TraversalType.EntireTree);

        /// <summary>
        /// Returns an IEnumerable{INamedValueSet} that can be used to iterate through the set of read-only NV sub-sets that are referenced by this set.
        /// </summary>
        IEnumerable<INamedValueSet> SubSets { get; }

        /// <summary>
        /// Returns an IEnumerable{INamedValue} that can be used to iterate through the collection, possibly including the INamedValues in the sub-sets.
        /// </summary>
        IEnumerable<INamedValue> GetEnumerable(TraversalType traversalType = TraversalType.Flatten);

        /// <summary>
        /// Explicitly request that this NVS build and retain the Dictionary it uses to find NamedValue items given a name.
        /// This method may be safely used even with IsReadOnly sets.
        /// <para/>Support call chaining
        /// </summary>
        INamedValueSet BuildDictionary();

        /// <summary>
        /// Returns an enumerable set of the Names (Keys) in this NVS instance.  
        /// </summary>
        IEnumerable<string> Names { get; }
    }

    /// <summary>
    /// Defines the search type traversal that may be used with specific INamedValueSet methods/properties to control how to search/traverse when the set has a non-empty tree of sub-sets.
    /// <para/>EntireTree, TopLevelOnly, Flatten
    /// </summary>
    public enum TraversalType : int
    {
        /// <summary>Specifies that the entire tree shall be traversed</summary>
        EntireTree = 0,

        /// <summary>Specifies that only the NamedValues in the top level set shall be traversed</summary>
        TopLevelOnly,

        /// <summary>
        /// Specifies that the entire tree shall be traversed.
        /// When used with copy construction and ToString this choice will cause the contents of the sub-trees to be merged into the top level and to filter out repeat names by accepting only the first one found in each such case.
        /// </summary>
        Flatten,
    }

    /// <summary>TraversalType related extension methods</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given traversalType indicates that the entire tree should be traversed.  
        /// <para/>True for TraversalType.EntireTree and TraversalType.Flatten
        /// </summary>
        public static bool CoverEntireTree(this TraversalType traversalType) { return (traversalType == TraversalType.EntireTree || traversalType == TraversalType.Flatten); }

        /// <summary>
        /// Returns true if the given traversalType indicates that only the top level of the tree should be traversed
        /// </summary>
        public static bool CoverTopLevelOnly(this TraversalType traversalType) { return (traversalType == TraversalType.TopLevelOnly); }

        /// <summary>
        /// Null tolerante version of INamedValueSet.ToStringSML method.  Returns <paramref name="fallbackValue"/> in place of null.
        /// </summary>
        public static string SafeToStringSML(this INamedValueSet invs, TraversalType traversalType = TraversalType.EntireTree, string nvsNodeName = "NVS", string nvNodeName = "NV", string fallbackValue = "")
        {
            if (invs != null)
                return invs.ToStringSML(traversalType: traversalType, nvsNodeName: nvsNodeName, nvNodeName: nvNodeName);
            else
                return fallbackValue;
        }

        /// <summary>
        /// Null tolerante version of INamedValue.ToStringSML method.  Returns <paramref name="fallbackValue"/> in place of null.
        /// </summary>
        public static string SafeToStringSML(this INamedValue inv, string nvNodeName = "NV", string fallbackValue = "")
        {
            if (inv != null)
                return inv.ToStringSML(nvNodeName: nvNodeName);
            else
                return fallbackValue;
        }
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

        /// <summary>Returns true if the this INamedValue is equal to the given <paramref name="other"/>.</summary>
        bool IsEqualTo(INamedValue other, bool compareReadOnly = true);

        /// <summary>
        /// Returns the approximate size of the contents in bytes. 
        /// </summary>
        int EstimatedContentSizeInBytes { get; }

        /// <summary>Custom ToString variant: when true useDoubleEqualsForRW causes the result to be of the form name==value if the given INamedValue is not Read Only, when true treatNameWithEmptyVAAsKeyword omits the =value if the INamedValue.VC.IsEmpty.</summary>
        string ToString(bool useDoubleEqualsForRW, bool treatNameWithEmptyVCAsKeyword);

        /// <summary>ToString variant that support SML like output format.</summary>
        string ToStringSML(string nvNodeName = "NV");
    }

    #endregion

    #region Named Values (NamedValueSet, NamedValue)

    /// <summary>
    /// This class defines objects each of which act as a Set of NamedValues.  
    /// This Set is internally represented by a List and may also be indexed using a Dictionary 
    /// This object includes a CollectionDataContractAttribute so that it may be serialized as part of other DataContract objects.  Supporting this attribute
    /// is based on the standard Add method signature and the support of the ICollection{NamedValue} interface.
    /// <para/>Warning: in order to preserve backward compatibility with the prior INamedValueSet behavior, and due to the limitations in the implementation of the new sub-set related behavior, 
    /// the IEnumerable interface has not generally been extended to support implicit access to the sub-sets.  Specific methods defined below (string indexed getter for example) have been
    /// extended to make implict use of TraversalType.EntireTree.  The caller should assume that TraversalType.TopLevelOnly will be used unless the method explicitly supports specification of
    /// the TraversalType or the method comments indicate otherwise.
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
    [CollectionDataContract(ItemName = "nvi", Namespace = Constants.ModularNameSpace), Serializable]
    public class NamedValueSet : ICollection<NamedValue>, INamedValueSet
    {
        #region Empty constant

        /// <summary>Returns a readonly empty NamedValueSet</summary>
        public static NamedValueSet Empty { get { return empty; } }
        private static readonly NamedValueSet empty = new NamedValueSet(null, asReadOnly: true);

        #endregion

        #region Contructors

        /// <summary>
        /// Explicit default constructor.  Required by certain usage patterns (data contract collection serialization for example)
        /// </summary>
        public NamedValueSet()
        { }

        /// <summary>
        /// Copy constructor from IEnumerable{INamedValues}.  
        /// Creates a new NamedValueList from the given list of INamedValues.  Treats the null <paramref name="rhsSet"/> as an empty set.
        /// if asReadOnly is false then this produces a fully read/write set even if copying from a fully readonly rhs or copying from an rhs that includes readonly NamedValue items.
        /// if asReadonly is true then this produces a fully IsReadOnly set which can reference any already readonly items from the given rhs set.
        /// <para/>By default (when using all default constructors) this method creates an empty, writable set with no sub-sets.
        /// </summary>
        public NamedValueSet(IEnumerable<INamedValue> rhsSet, bool asReadOnly = false, IEnumerable<INamedValueSet> subSets = null)
        {
            SetFrom(rhsSet, subSets, asReadOnly);
        }

        ///<summary>
        /// Copy constructor from INamedValueSet with explicit TraversalType specification which can be used to perform top level only copy, entire tree copy, or a flatten copy
        ///</summary>
        public NamedValueSet(INamedValueSet other, TraversalType copyTraversalType)
        {
            SetFrom(other, copyTraversalType);
        }

        #endregion

        #region Constructor helper methods (SetFrom variants)

        public NamedValueSet SetFrom(INamedValueSet other, TraversalType copyTraversalType)
        {
            if (other == null)
                return this;

            switch (copyTraversalType)
            {
                default:
                case TraversalType.EntireTree:
                    return SetFrom(other.GetEnumerable(TraversalType.TopLevelOnly), other.SubSets, other.IsReadOnly);

                case TraversalType.Flatten:
                case TraversalType.TopLevelOnly:
                    return SetFrom(other.GetEnumerable(copyTraversalType), null, other.IsReadOnly);
            }
        }

        public NamedValueSet SetFrom(IEnumerable<INamedValue> set, IEnumerable<INamedValueSet> subSets = null, bool asReadOnly = false)
        {
            ThrowIfIsReadOnly("The SetFrom method");

            InnerAddRange(set, asReadOnly: asReadOnly);

            SubSets = subSets;

            if (asReadOnly)
                isReadOnly = true;      // we do not need to iterate through the contents again as we just filled it with readonly items.

            return this;
        }

        #endregion

        #region Locally defined helper utility methods and properties (Add, AddRange variants, GetNamedValue, GetValue, SetKeyword, SetValue variants, Remove, RemoveAt, RemoveInline, string indexer, int indexer)

        /// <summary>
        /// This allows the class to be used with a Dictionary style initializer to add keywords to the set.
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        /// </summary>
        public NamedValueSet Add(string keyword)
        {
            ThrowIfIsReadOnly("The Add(keyword) method");

            return SetValue(new NamedValue(keyword));       // note: SetValue is used here so that if the client attempts to add the same keyword two or more times it will not fail
        }

        /// <summary>
        /// Provide an alternate name for the SetValue method.  This allows the class to be used with a Dictionary style initializer to add ValueContainer values to the set.
        /// When the ValueContainer vc is given as empty (the default) then the method effectively adds a keyword to this set.
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        /// </summary>
        public NamedValueSet Add(string name, ValueContainer vc)
        {
            ThrowIfIsReadOnly("The Add(name, vc) method");

            return SetValue(name, vc);// note: SetValue is used here so that if the client attempts to add the same name two or more times the value will be set to the last provided value
        }

        /// <summary>
        /// Provide an alternate name for the SetValue method.  This allows the class to be used with a Dictionary style initializer to add values to the set.
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        /// </summary>
        public NamedValueSet Add(string name, object value)
        {
            ThrowIfIsReadOnly("The Add(name, value) method");

            return SetValue(name, value);// note: SetValue is used here so that if the client attempts to add the same name two or more times the value will be set to the last provided value
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

            InnerAddRange(range, false);

            return this;
        }

        private void InnerAddRange(IEnumerable<INamedValue> range, bool asReadOnly)
        {
            if (range != null)
            {
                Tuple<NamedValue, int>[] tArray = range.Where(inv => (inv != null)).Select(inv => Tuple.Create(asReadOnly ? inv.ConvertToReadOnly() : inv.ConvertToNamedValue(), AttemptToFindItemIndexInList(inv.Name.Sanitize(), true))).ToArray();

                foreach (var t in tArray.Where(item => item.Item2 >= 0))
                {
                    int index = t.Item2;
                    NamedValue nv = t.Item1;

                    // we found a matching NamedValue in the list

                    NamedValue listNV = list[index];
                    if (!listNV.IsReadOnly && !asReadOnly)
                    {
                        // update the value for the NamedValue item that is already in the set
                        listNV.VC = nv.VC;
                    }
                    else
                    {
                        // replace the NamedValue with a new one with the same name and the new value (dictionary does not need to be reset)
                        list[index] = asReadOnly ? nv : new NamedValue(nv);
                    }
                }

                int addCount = 0;
                foreach (var t in tArray.Where(item => item.Item2 < 0))
                {
                    NamedValue nv = t.Item1;
                    list.Add(asReadOnly ? nv : new NamedValue(nv));
                    addCount++;
                }

                if (addCount > 0)
                    nameToIndexDictionary = null;
            }
        }

        /// <summary>
        /// If a INamedValue exists for the given name (after sanitization), in this set, or its sub-sets when requested, then this method returns it.
        /// Otherwise this method returns NamedValue.Empty
        /// </summary>
        public INamedValue GetNamedValue(string name, TraversalType searchTraversalType = TraversalType.EntireTree)
        {
            name = name.Sanitize();

            INamedValue nv = AttemptToFindItemInList(name, true);

            if (nv == null && searchTraversalType.CoverEntireTree() && !subSetsArray.IsNullOrEmpty())
                nv = subSetsArray.Select(nvs => nvs.GetNamedValue(name, searchTraversalType)).FirstOrDefault(item => !item.IsNullOrEmpty());

            return nv ?? NamedValue.Empty;
        }

        /// <summary>
        /// If a INamedValue exists for the given name (after sanitization), in this set, or its sub-sets when requested, then this method returns the ValueContainer (VC) from that INamedValue.
        /// Otherwise this method returns ValueContainer.Empty.
        /// </summary>
        public ValueContainer GetValue(string name, TraversalType searchTraversalType = TraversalType.EntireTree)
        {
            return GetNamedValue(name, searchTraversalType).VC;
        }

        /// <summary>
        /// Updates the desired NamedValue to be a keyword (has empty value container value), or adds a new keyword NamedValue, from the given name, to the list if it was not already present.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet SetKeyword(string name, bool asReadOnly = false)
        {
            return SetValue(new NamedValue(name) { IsReadOnly = asReadOnly });
        }

        /// <summary>
        /// Updates the desired NamedValue to contain the given vc value, or adds a new NamedValue, initialized from the given name and vc value, to the list if it was not already present.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet SetValue(string name, ValueContainer vc, bool asReadOnly = false)
        {
            return SetValue(new NamedValue(name, vc, asReadOnly: asReadOnly));
        }

        /// <summary>
        /// Updates the desired NamedValue to contain the given object value or adds a new NamedValue, initialized from the given name and object value, to the list if it was not already present.
        /// In either case the given object value will be assigned into a ValueContainer automatically using this signature.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet SetValue(string name, object value, bool asReadOnly = false)
        {
            return SetValue(new NamedValue(name, value, asReadOnly: asReadOnly));
        }

        /// <summary>
        /// Explicit ValueContainer setter.  Updates the desired NamedValue to contain the given vc value (if it was not read only), replaces it with the given nv it is present as is readonly, or adds the given NamedValue to the list if it was not already present.
        /// <para/>NOTE: in the case where this method adds the given nv to the set or replaces the NamedValue (because it is read only) the set will directly reference the given nv object)
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet SetValue(NamedValue nv)
        {
            ThrowIfIsReadOnly("The SetValue method");

            int index = AttemptToFindItemIndexInList(nv.Name, true);

            if (index >= 0)
            {
                // we found a matching NamedValue in the list

                NamedValue listNV = list[index];
                if (!listNV.IsReadOnly && !nv.IsReadOnly)
                {
                    // update the value for the NamedValue item that is already in the set
                    listNV.VC = nv.VC;
                }
                else
                {
                    // replace the NamedValue with a new one with the same name and the new value (dictionary does not need to be reset)
                    list[index] = nv;
                }
            }
            else
            {
                // name is not already in the set - make a new NamedValue with the given santized name and vc value and add it to the list.
                nameToIndexDictionary = null;
                list.Add(nv);
            }

            return this;
        }

        /// <summary>
        /// Asks the underlying List to Remove the given <paramref name="name"/>ed valuess (after sanitization) from the set.
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
                new System.ArgumentOutOfRangeException("index", "the given index is less than 0 or it is greater than or equal to the set's Count").Throw();

            nameToIndexDictionary = null;
            list.RemoveAt(index);
        }

        /// <summary>
        /// Removes each of the named values in the given <paramref name="nameParamsArray"/> from the set, if any.
        /// <para/>Supports call chaining
        /// </summary>
        public NamedValueSet RemoveInline(params string[] nameParamsArray)
        {
            ThrowIfIsReadOnly("The RemoveInline method");

            foreach (var name in nameParamsArray.MapNullToEmpty())
            {
                string sanitizedName = name.Sanitize();

                int index = AttemptToFindItemIndexInList(sanitizedName, false);     // do not create a dictionary for this effort since we are just going to delete it anyway

                if (index >= 0)
                {
                    nameToIndexDictionary = null;
                    list.RemoveAt(index);
                }
            }

            return this;
        }

        /// <summary>
        /// get/set string indexed operator give caller access to a NamedValue for the given indexed name.
        /// getter returns the indicated NamedValue, if found in this list, or returns an empty readonly NamedValue if the given name was not found in the list (returned value is not added to the list in this case)
        /// setter requires that the given value is non-null and that its Name is equal to the string index given to the setter (see exceptions below).  Once accepted, this setter sets the indicated NamedValue to contain the ValueContainer from the given value NamedValue, or the setter adds the given NamedValue to the list if it was not already present in the list.
        /// <para/>getter implicitly uses TraverseType.EntireTree, setter implicitly uses TraverseType.TopLevelOnly
        /// </summary>
        /// <param name="name">Gives the name index to get or set a NamedValue from/to</param>
        /// <returns>The indicated NamedValue or an empty NamedValue with the given name if the name was not found in this list</returns>
        /// <exception cref="System.ArgumentNullException">This exception is thrown by the setter if the given value is null.</exception>
        /// <exception cref="System.ArgumentException">This exception is thrown by the setter if the given value.Name propery is not equal to the string index.</exception>
        public NamedValue this[string name]
        {
            get
            {
                INamedValue iNv = GetNamedValue(name, TraversalType.EntireTree);
                NamedValue nv = iNv as NamedValue;

                if (nv != null)
                    return nv;

                if (iNv != null)
                    return new NamedValue(iNv, asReadOnly: true);       // if this object is changed it will not actually modify the contents of the set.

                return NamedValue.Empty;
            }
            set
            {
                ThrowIfIsReadOnly("The String indexed property setter");

                if (value == null)
                    new System.ArgumentNullException("value").Throw();

                string sanitizedName = name.Sanitize();

                if (sanitizedName != value.Name.Sanitize())
                    new System.ArgumentException("value.Name must match index string for string Indexed Setter.").Throw();

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
                    new System.ArgumentOutOfRangeException("index", "the given index is less than 0 or it is greater than or equal to the set's Count").Throw();

                return list[index];
            }
        }

        #endregion

        #region INamedValueSet (leftovers - most of the other members and properties are found in other regions of this class) - Includes IEquatable<INamedValueSet>

        /// <summary>Returns true if this set, or any of its sub-sets, contains a NamedValue for the given name (after sanitization).</summary>
        public bool Contains(string name, TraversalType searchTraversalType = TraversalType.EntireTree)
        {
            string sanitizedName = name.Sanitize();

            return ((AttemptToFindItemIndexInList(sanitizedName, true) >= 0)
                    || (searchTraversalType.CoverEntireTree() && (subSetsArray ?? emptySubSetsArray).Any(subSet => subSet.Contains(name, searchTraversalType)))
                    );
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

        /// <summary>
        /// String indexed property.  Returns the INamedValue from the set if it is found from the given name (after sanitization).
        /// Otherwise returns a new empty INamedValue using the given name (set contents are not changed by this).
        /// <para/>implicitly uses TraverseType.EntireTree
        /// </summary>
        INamedValue INamedValueSet.this[string name]
        {
            get
            {
                return GetNamedValue(name, TraversalType.EntireTree);
            }
        }

        /// <summary>
        /// String indexed property.  Returns the INamedValue from the set if it is found from the given name (after sanitization).  Supports searchType configuration
        /// Otherwise returns a new empty INamedValue using the given name (set contents are not changed by this).
        /// </summary>
        INamedValue INamedValueSet.this[string name, TraversalType searchTraversalType] 
        {
            get
            {
                return GetNamedValue(name, searchTraversalType);
            }
        }

        /// <summary>Returns true if the this INamedValueSet has the same contents, in the same order, as the given rhs.</summary>
        public bool IsEqualTo(INamedValueSet rhs, TraversalType searchTraversalType = TraversalType.EntireTree, bool compareReadOnly = true)
        {
            if (System.Object.ReferenceEquals(this, rhs))
                return true;

            if (rhs == null || Count != rhs.Count || (IsReadOnly != rhs.IsReadOnly && compareReadOnly))
                return false;

            int setCount = Count;
            for (int index = 0; index < setCount; index++)
            {
                if (!list[index].IsEqualTo(rhs[index], compareReadOnly: compareReadOnly))
                    return false;
            }

            if (searchTraversalType.CoverEntireTree())
            {
                INamedValueSet[] thisSubSetsArray = (subSetsArray ?? emptySubSetsArray);
                INamedValueSet[] rhsSubSetsArray = (rhs.SubSets.ToArray() ?? emptySubSetsArray);

                int thisSubSetsArrayLength = thisSubSetsArray.Length; 

                if (thisSubSetsArrayLength != rhsSubSetsArray.Length)
                    return false;

                for (int idx = 0; idx < thisSubSetsArrayLength; idx++)
                {
                    if (!thisSubSetsArray[idx].IsEqualTo(rhsSubSetsArray[idx], searchTraversalType: searchTraversalType, compareReadOnly: compareReadOnly))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true if the this INamedValueSet has the same contents, in the same order, as the given other NVS.
        /// <para/>implicitly uses TraverseType.EntireTree
        /// </summary>
        public bool Equals(INamedValueSet other)
        {
            return IsEqualTo(other, TraversalType.EntireTree);
        }

        /// <summary>
        /// This method is required to support the fact that INamedValueSet implements IEnumerable{INamedValue} and the standard enumerators already implemented by the underling SortedList
        /// cannot be directly casted to this interface.
        /// <para/>TraverseType.EntireTree is implicit
        /// </summary>
        IEnumerator<INamedValue> IEnumerable<INamedValue>.GetEnumerator()
        {
            return GetEnumerable(TraversalType.EntireTree).GetEnumerator();
        }

        /// <summary>
        /// Explicitly request that this NVS build and retain the Dictionary it uses to find NamedValue items given a name.
        /// This method may be safely used even with IsReadOnly sets.
        /// <para/>Support call chaining
        /// </summary>
        INamedValueSet INamedValueSet.BuildDictionary()
        {
            return this.BuildDictionary();
        }


        /// <summary>
        /// Returns an enumerable set of the Names (Keys) in this NVS instance.  
        /// </summary>
        public IEnumerable<string> Names 
        {
            get
            {
                return GetEnumerable().Select(nv => nv.Name);
            }
        }

        #endregion

        #region CollectionDataContract support and related ICollection<NamedValue>, IEnumerable<INamedValue>

        /// <summary>
        /// Gets the number of NamedValues that are currently in this set.
        /// <para/>implicitly uses TraverseType.TopLevelOnly
        /// </summary>
        public int Count
        {
            get { return list.Count; }
        }

        /// <summary>
        /// Returns true if this set contains the given NamedValue instance.
        /// </summary>
        /// <remarks>This is essentially a useless method.  It is provided to that the class can implement ICollection{INamedValue}</remarks>
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
            ThrowIfIsReadOnly("The Add(item) method");

            if (item == null)
                new System.ArgumentNullException("item").Throw();

            string sanitizedName = item.Name.Sanitize();

            if (AttemptToFindItemIndexInList(sanitizedName, false) >= 0)
                new System.ArgumentException("An element with the same sanitized item.Name already exists in this set.").Throw();

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
        /// Clears the underlying collection and removes all sub-sets.  Supports call chaining.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public NamedValueSet Clear()
        {
            ThrowIfIsReadOnly("The Clear method");

            nameToIndexDictionary = null;
            list.Clear();
            subSetsArray = null;

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
        /// If the target NamedValueSet is already IsReadOnly then this method has no effect.
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
        /// <para/>implicitly uses TraverseType.TopLevelOnly (SubSets are always retained and used as read-only sets)
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
                new System.NotSupportedException(reasonPrefix + " is not supported when this object's IsReadOnly property has been set to true").Throw();
        }

        private bool isReadOnly;

        #endregion

        #region Equality support (IEquatable<INamedValueSet> is supported under INamedValueSet)

        /// <summary>Support object level Equality testing versions.</summary>
        public override bool Equals(object rhsAsObject)
        {
            return IsEqualTo(rhsAsObject as INamedValueSet);
        }

        /// <summary>Override GetHashCode because Equals has been.</summary>
        public override int GetHashCode()
        {
            var hashCode = isReadOnly.GetHashCode();

            var invs = (INamedValueSet) this;

            hashCode = invs.Select((nv, index) => nv.GetHashCode() ^ index.GetHashCode()).Aggregate(hashCode, (a, b) => a ^ b);

            return hashCode;
        }

        #endregion

        #region ToString (variants), ToStringSML, and EstimatedContentSizeInBytes

        /// <summary>
        /// Provide local debug/logging assistance version of this method
        /// </summary>
        public override string ToString()
        {
            return ToString(true, false, TraversalType.Flatten);
        }

        /// <summary>Custom ToString variant that allows the caller to determine if the ro/rw postfix should be included on this string result and if each NV with an empty VC should be treated like a keyword.</summary>
        public string ToString(bool includeROorRW, bool treatNameWithEmptyVCAsKeyword, TraversalType traversalType = TraversalType.EntireTree)
        {
            StringBuilder sb = new StringBuilder("[");

            switch (traversalType)
            {
                case TraversalType.EntireTree:
                    if (!subSetsArray.IsNullOrEmpty())
                    {
                        var e1 = GetEnumerable(TraversalType.TopLevelOnly).Select(iNv => iNv.ToString(includeROorRW, treatNameWithEmptyVCAsKeyword));
                        var e2 = new string[] { "SubSets {0}".CheckedFormat(String.Join(",", subSetsArray.Select(invs => invs.ToString(false, treatNameWithEmptyVCAsKeyword, traversalType)).ToArray())) };
                        sb.Append(String.Join(",", e1.Concat(e2).ToArray()));
                    }
                    else
                    {
                        sb.Append(String.Join(",", GetEnumerable(TraversalType.TopLevelOnly).Select(iNv => iNv.ToString(includeROorRW, treatNameWithEmptyVCAsKeyword)).ToArray()));
                    }
                    break;

                default:
                case TraversalType.Flatten:
                case TraversalType.TopLevelOnly:
                    sb.Append(String.Join(",", GetEnumerable(traversalType).Select(iNv => iNv.ToString(includeROorRW, treatNameWithEmptyVCAsKeyword)).ToArray()));
                    break;
            }

            sb.Append("]");

            if (includeROorRW)
                sb.Append(IsReadOnly ? "ro" : "rw");

            return sb.ToString();
        }

        /// <summary>ToString variant that support SML like output format.</summary>
        public string ToStringSML(TraversalType traversalType = TraversalType.EntireTree, string nvsNodeName = "NVS", string nvNodeName = "NV")
        {
            if (list.IsNullOrEmpty() && subSetsArray.IsNullOrEmpty())
                return "[{0}]".CheckedFormat(nvsNodeName);

            StringBuilder sb = new StringBuilder("[{0} ".CheckedFormat(nvsNodeName));

            switch (traversalType)
            {
                case TraversalType.EntireTree:
                    sb.Append(String.Join(" ", GetEnumerable(traversalType).Select((nv) => nv.ToStringSML(nvNodeName: nvNodeName)).ToArray()));
                    if (!subSetsArray.IsNullOrEmpty())
                        sb.CheckedAppendFormat(" [{0}-SubSets {1}]", nvsNodeName, String.Join(" ", subSetsArray.Select(invs => invs.ToStringSML(traversalType, nvsNodeName: nvsNodeName, nvNodeName: nvNodeName))));
                    break;

                default:
                case TraversalType.Flatten:
                case TraversalType.TopLevelOnly:
                    sb.Append(String.Join(" ", GetEnumerable(traversalType).Select((nv) => nv.ToStringSML(nvNodeName: nvNodeName)).ToArray()));
                    break;
            }

            sb.Append("]");

            return sb.ToString();
        }

        /// <summary>
        /// Returns the approximate size of the contents in bytes.
        /// </summary>
        public int EstimatedContentSizeInBytes
        {
            get 
            {
                return GetEstimatedContentSizeInBytes(TraversalType.EntireTree);
            }
        }

        /// <summary>
        /// Returns the approximate size of the contents in bytes. 
        /// </summary>
        public int GetEstimatedContentSizeInBytes(TraversalType traversalType = TraversalType.EntireTree)
        {
            int totalApproximateSize = GetEnumerable(traversalType).Sum(inv => inv.EstimatedContentSizeInBytes);
            return totalApproximateSize + 10;
        }


        #endregion

        #region Tree Structure support

        /// <summary>
        /// Returns an IEnumerable{INamedValue} that can be used to iterate through the collection, possibly including the INamedValues in the sub-sets.
        /// </summary>
        public IEnumerable<INamedValue> GetEnumerable(TraversalType traversalType = TraversalType.Flatten)
        {
            switch (traversalType)
            {
                default:
                case TraversalType.EntireTree:
                    return list.Concat((subSetsArray ?? emptySubSetsArray).SelectMany(subSet => subSet.GetEnumerable(traversalType)));
                case TraversalType.Flatten:
                    return list.Concat((subSetsArray ?? emptySubSetsArray).SelectMany(subSet => subSet.GetEnumerable(traversalType))).Distinct(compareINVNamesEqualityComparer);
                case TraversalType.TopLevelOnly:
                    return list;
            }
        }

        /// <summary>
        /// private class used to support use of Linq.Distinct to filter out redundant sub-set nv items when using TraverseType.Flatten.
        /// </summary>
        private class CompareINVNamesEqualityComparer : IEqualityComparer<INamedValue>
        {
            public bool Equals(INamedValue x, INamedValue y)
            {
                if (x == y)
                    return true;
                if (x == null || y == null)
                    return false;
                return (x.Name == y.Name);
            }

            public int GetHashCode(INamedValue obj)
            {
                return ((obj != null) ? obj.Name.GetHashCode() : 0);
            }
        }
        private static readonly IEqualityComparer<INamedValue> compareINVNamesEqualityComparer = new CompareINVNamesEqualityComparer();

        /// <summary>
        /// Getter returns an enumerator that can be used to obtain the next level of NV sub-sets that are referenced by this set.
        /// <para/>Setter may be used to define the set of sub-set INamedValueSet objects that will be used under this nvs.
        /// </summary>
        public IEnumerable<INamedValueSet> SubSets 
        { 
            get { return subSetsArray ?? emptySubSetsArray; } 
            set 
            {
                ThrowIfIsReadOnly("The SubSets property setter");

                subSetsArray = ((value == null) ? null : value.Where(invs => !invs.IsNullOrEmpty()).Select(invs => invs.ConvertToReadOnly()).ToArray());
            } 
        }

        private INamedValueSet [] subSetsArray = null;
        private static readonly INamedValueSet[] emptySubSetsArray = EmptyArrayFactory<INamedValueSet>.Instance;

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
            if (nameToIndexDictionary == null && Count >= minElementsToUseDictionary && createDictionaryIfNeeded)
                BuildDictionary();

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
        [NonSerialized]
        volatile Dictionary<string, int> nameToIndexDictionary = null;

        /// <summary>Defines the minimum Set size required to decide to create the nameToIndexDictionary</summary>
        const int minElementsToUseDictionary = 10;

        /// <summary>
        /// Explicitly request that this NVS build and retain the Dictionary it uses to find NamedValue items given a name.
        /// This method may be safely used even with IsReadOnly sets.
        /// <para/>Support call chaining
        /// </summary>
        public NamedValueSet BuildDictionary()
        {
            Dictionary<string, int> d = new Dictionary<string, int>();

            int setCount = Count;
            for (int idx = 0; idx < setCount; idx++)
                d[list[idx].Name] = idx;

            nameToIndexDictionary = d;

            return this;
        }

        #endregion

        #region OnDeserialed support

        /// <summary>
        /// Note: The following method is retained for documentation purposes but the actual deserialziation appears to make a new copy of this Set on completion of the deserialization process
        /// and the new copy is not marked readonly by default.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            MakeReadOnly();
        }

        #endregion
    }

    /// <summary>
    /// This object defines a single named value.  A named value is a pairing of a name and a ValueContainer.
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace), Serializable]
    public class NamedValue : INamedValue
    {
        #region Empty constant

        /// <summary>Returns a readonly empty NamedValueSet</summary>
        public static NamedValue Empty { get { return empty; } }
        private static readonly NamedValue empty = new NamedValue(string.Empty) { IsReadOnly = true };

        #endregion

        #region Constructors

        /// <summary>Default Constructor, for use with deserialization</summary>
        public NamedValue()
        {
            Name = string.Empty;
        }

        /// <summary>Constructor - builds NamedValue with the given <paramref name="keyword"/> value.</summary>
        /// <remarks>Note: This construction signature cannot also be given the optional asReadOnly flag as is done with other construtors or it will take precidence over the object value version below when used to construct a boolean NamedValue (causes test regression).</remarks>
        public NamedValue(string keyword)
        {
            Name = keyword ?? string.Empty;
        }

        /// <summary>Constructor - builds NamedValue with the given <paramref name="name"/> and ValueContainer <paramref name="vc"/> value.</summary>
        public NamedValue(string name, ValueContainer vc, bool asReadOnly = false) 
        {
            Name = name ?? string.Empty;
            VC = vc;

            isReadOnly = asReadOnly;
        }

        /// <summary>Helper Constructor - builds NamedValue with the given <paramref name="name"/> and object <paramref name="value"/>.  VC is constructed to contain the given object <paramref name="value"/></summary>
        public NamedValue(string name, object value, bool asReadOnly = false) 
            : this(name: name, vc: new ValueContainer(value), asReadOnly: asReadOnly) 
        {}

        /// <summary>
        /// Copy constructor.  builds a copy of the given rhs containing copy of Name and VC properties.
        /// The IsReadOnly on resulting copy is set from asReadOnly.  
        /// In addition if asReadonly is true and the given rhs is not readonly and its contained value is an IListOfString and the copy's contained value is set to a expliclty
        /// copy of the given rhs.  This stop cannot be peformed for general contained values that have opaque Object values in them and it not required for String contents (already immutable)
        /// and for all other value content types which do not naturaly share references.
        /// </summary>
        public NamedValue(INamedValue rhs, bool asReadOnly = false)
        {
            bool rhsIsNotNull = (rhs != null);

            Name = (rhsIsNotNull && rhs.Name != null) ? rhs.Name : string.Empty;

            if (!rhsIsNotNull)
            {}
            if (asReadOnly && !rhs.IsReadOnly)
                vc.DeepCopyFrom(rhs.VC);
            else
                vc = rhs.VC;

            VCHasBeenSet = (rhsIsNotNull && rhs.VCHasBeenSet);
            isReadOnly = asReadOnly;
        }

        #endregion

        #region SetValue methods

        /// <summary>
        /// Updates this NamedValue's contained value (VC property) from the given object value.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the item has been set to IsReadOnly</exception>
        public NamedValue SetValue(object valueAsObject)
        {
            ThrowIfIsReadOnly(Fcns.CurrentMethodName);

            VC = ValueContainer.CreateFromObject(valueAsObject);

            return this;
        }

        /// <summary>
        /// Updates this NamedValue's contained value (VC property) from the given <paramref name="vc"/>.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the item has been set to IsReadOnly</exception>
        public NamedValue SetValue(ValueContainer vc)
        {
            ThrowIfIsReadOnly(Fcns.CurrentMethodName);

            VC = vc;

            return this;
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
        public bool IsEqualTo(INamedValue rhs, bool compareReadOnly = true)
        {
            return (rhs != null 
                    && Name == rhs.Name 
                    && VC.IsEqualTo(rhs.VC) 
                    && (IsReadOnly == rhs.IsReadOnly || !compareReadOnly)
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
            var hashCode = vc.GetHashCode() ^ isReadOnly.GetHashCode();
            if (Name != null)
                hashCode ^= Name.GetHashCode();

            return hashCode;
        }

        #endregion

        #region IsReadOnly support

        /// <summary>
        /// This method can be used on a newly created NamedValue to set its IsReadOnly property to true while also supporting call chaining.
        /// If this NamedValue is already IsReadOnly then this method has no effect.
        /// </summary>
        /// <remarks>Use the ConvertToReadOnly extension method to convert INamedValue objects to be ReadOnly.</remarks>
        public NamedValue MakeReadOnly()
        {
            if (!IsReadOnly)
                IsReadOnly = true;

            return this;
        }

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
                new System.NotSupportedException(reasonPrefix + " is not supported when this object's IsReadOnly property has been set").Throw();
        }

        private bool isReadOnly;

        #endregion

        #region ToString (variants), ToStringSML, and EstimatedContentSizeInBytes

        /// <summary>
        /// Custom ToString variant that allows the caller to determine if the ro/rw postfix should be included on this string result and if NV's with an empty VC should be treated like a keyword (by only returning the name)
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
        public string ToStringSML(string nvNodeName = "NV")
        {
            if (!VC.IsEmpty)
                return "[{0} {1} {2}]".CheckedFormat(nvNodeName, ValueContainer.Create(Name).ToStringSML(), VC.ToStringSML());
            else if (!Name.IsNullOrEmpty())
                return "[{0} {1}]".CheckedFormat(nvNodeName, ValueContainer.Create(Name).ToStringSML());
            else
                return "[{0}]".CheckedFormat(nvNodeName);
        }

        /// <summary>
        /// Returns the approximate size of the contents in bytes. 
        /// </summary>
        [Obsolete("The use of this property has been deprecated.  (2018-03-07)")]
        public int EstimatedContentSizeInBytes 
        {
            get { return (Name.Length * sizeof(char)) + VC.EstimatedContentSizeInBytes + 10; }
        }

        #endregion

        #region OnDeserialed support

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            MakeReadOnly();
    }

    #endregion
    }

    #endregion

    #region NamedValue and NamedValueSet related extension methods

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
        /// Conditional <paramref name="nvs"/>.SetKeyword variant as extension method. 
        /// If the given <paramref name="condition"/> is true and the given <paramref name="name"/> is non-mepty then this method Sets the given <paramref name="name"/> as a keyword (vc is empty), 
        /// otherwise the method does not modify the given <paramref name="nvs"/>.
        /// </summary>
        public static NamedValueSet ConditionalSetKeyword(this NamedValueSet nvs, string name, bool condition)
        {
            if (condition && nvs != null && name.IsNeitherNullNorEmpty())
                nvs.SetKeyword(name);

            return nvs;
        }

        /// <summary>
        /// Conditional <paramref name="nvs"/>.SetValue variant as extension method. 
        /// If the given <paramref name="condition"/> is true and the given <paramref name="name"/> is non-empty then this method Sets the given <paramref name="name"/> to the given <paramref name="value"/>, 
        /// otherwise the method does not modify the given <paramref name="nvs"/>.
        /// </summary>
        public static NamedValueSet ConditionalSetValue<TValueType>(this NamedValueSet nvs, string name, bool condition, TValueType value)
        {
            if (condition && nvs != null && name.IsNeitherNullNorEmpty())
                nvs.SetValue(name, value);

            return nvs;
        }

        /// <summary>
        /// <paramref name="nvs"/>.SetValue variant as extension method.  
        /// If the given nullable <paramref name="value"/> is not null and the given <paramref name="name"/> is non-empty then this method Sets the given <paramref name="name"/> to the given <paramref name="value"/>
        /// otherwise the method does not modify the given nvs.
        /// </summary>
        public static NamedValueSet SetValueIfNotNull<TValueType>(this NamedValueSet nvs, string name, TValueType? value)
            where TValueType : struct
        {
            if (value != null && nvs != null && name.IsNeitherNullNorEmpty())
                nvs.SetValue(name, value.GetValueOrDefault());

            return nvs;
        }

        /// <summary>
        /// <paramref name="nvs"/>.SetValue variant as extension method.  
        /// If the given <paramref name="value"/> is not null and the given <paramref name="name"/> is non-empty then this method Sets the given <paramref name="name"/> to the given <paramref name="value"/>
        /// otherwise the method does not modify the given nvs.
        /// </summary>
        public static NamedValueSet SetValueIfNotNull<TValueType>(this NamedValueSet nvs, string name, TValueType value)
            where TValueType : class
        {
            if (value != null && nvs != null && name.IsNeitherNullNorEmpty())
                nvs.SetValue(name, value);

            return nvs;
        }

        /// <summary>
        /// Converts the given INamedValueSet <paramref name="iNvSet"/> to a readonly NamedValueSet, either by casting or by copying.
        /// If the given <paramref name="iNvSet"/> value is null then this method return NamedValueSet.Empty or null, based on the value of the given <paramref name="mapNullToEmpty"/> parameter.
        /// If the given <paramref name="iNvSet"/> value IsReadOnly and its type is actually a NamedValueSet then this method returns the given iNvSet down casted as a NamedValueSet (path used for serialziation)
        /// Otherwise this method returns a new readonly NamedValueSet created as a sufficiently deep clone of the given iNvSet.
        /// </summary>
        public static NamedValueSet ConvertToReadOnly(this INamedValueSet iNvSet, bool mapNullToEmpty = true)
        {
            if (iNvSet == null)
                return (mapNullToEmpty ? NamedValueSet.Empty : null);

            // special case where we downcast readonly NamedValueSets (passed as INamedValueSets) so that we do not need to copy them after they are already readonly.
            if (iNvSet.IsReadOnly && (iNvSet is NamedValueSet))
                return (iNvSet as NamedValueSet);

            return new NamedValueSet(iNvSet.GetEnumerable(TraversalType.TopLevelOnly), asReadOnly: true,subSets: iNvSet.SubSets);
        }

        /// <summary>
        /// Converts the given INamedValue <paramref name="iNv"/> to a readonly NamedValue, either by casting or by copying.
        /// If the given <paramref name="iNv"/> value is null then this method returns NamedValue.Empty or null, based on the value of the given <paramref name="mapNullToEmpty"/> parameter.
        /// If the given <paramref name="iNv"/> value IsReadOnly and its type is actually a NamedValue then this method returns the given <paramref name="iNv"/> down casted as a NamedValue (path used for serialziation)
        /// Otherwise this method returns a new readonly NamedValue as a copy of the given <paramref name="iNv"/>.
        /// </summary>
        public static NamedValue ConvertToReadOnly(this INamedValue iNv, bool mapNullToEmpty = false)
        {
            if (iNv == null)
                return (mapNullToEmpty ? NamedValue.Empty : null);

            if (iNv.IsReadOnly && (iNv is NamedValue))
                return (iNv as NamedValue);

            return new NamedValue(iNv, asReadOnly: true);
        }

        [Obsolete("Please replace use of this method with its new, now correctly spelled, variant (2018-07-04)")]
        public static NamedValueSet ConvertToWriteable(this INamedValueSet iNvSet, bool mapNullToEmpty = true)
        {
            return iNvSet.ConvertToWritable(mapNullToEmpty: mapNullToEmpty);
        }

        [Obsolete("Please replace use of this method with its new, now correctly spelled, variant (2018-07-04)")]
        public static NamedValue ConvertToWriteable(this INamedValue iNv, bool mapNullToEmpty = true)
        {
            return iNv.ConvertToWritable(mapNullToEmpty: mapNullToEmpty);
        }

        /// <summary>
        /// Converts the given INamedValueSet iNvSet to a read/write NamedValueSet by cloning if needed.
        /// If the given <paramref name="iNvSet"/> is null then this method returns a new writable NamedValueSet or null, based on the value of the given <paramref name="mapNullToEmpty"/> parameter.
        /// If the given <paramref name="iNvSet"/> value is not null and it is !IsReadonly then return the given value.
        /// Otherwise this method constructs and returns a new readwrite NamedValueSet copy the given nvSet.
        /// </summary>
        public static NamedValueSet ConvertToWritable(this INamedValueSet iNvSet, bool mapNullToEmpty = true)
        {
            if (iNvSet == null)
                return mapNullToEmpty ? new NamedValueSet() : null;

            if (!iNvSet.IsReadOnly && (iNvSet is NamedValueSet))
                return (iNvSet as NamedValueSet);

            return new NamedValueSet(iNvSet.GetEnumerable(TraversalType.TopLevelOnly), asReadOnly: false, subSets: iNvSet.SubSets);
        }

        /// <summary>
        /// Converts the given INamedValue <paramref name="iNv"/> to a read/write NamedValue by cloning if needed.
        /// If the given <paramref name="iNv"/> is null this method returns a new NamedValue or null, based on the value of the given <paramref name="mapNullToEmpty"/> parameter.
        /// If the given <paramref name="iNv"/> !IsReadonly and is already a NamedValue then this method returns it casted but otherwise unchanged.
        /// Otherwise this method constructs and returns a new readonly NamedValue copy of the given <paramref name="iNv"/>.
        /// </summary>
        public static NamedValue ConvertToWritable(this INamedValue iNv, bool mapNullToEmpty = true)
        {
            if (iNv == null)
                return mapNullToEmpty ? new NamedValue() : null;

            if (!iNv.IsReadOnly && (iNv is NamedValue))
                return (iNv as NamedValue);

            return new NamedValue(iNv, asReadOnly: false);
        }

        /// <summary>
        /// Converts the given <paramref name="iNv"/> to a NamedValue instance by cloning if needed.
        /// If the given <paramref name="iNv"/> is null this method returns a new NamedValue or null, based on the value of the given <paramref name="mapNullToEmpty"/> parameter.
        /// If the given <paramref name="iNv"/> is already a NamedValue then this method returns it casted but otherwise unchanged.
        /// Otherwise this method constructs and returns a new readonly NamedValue copy of the given <paramref name="iNv"/>.
        /// </summary>
        public static NamedValue ConvertToNamedValue(this INamedValue iNv, bool mapNullToEmpty = true)
        {
            if (iNv == null)
                return mapNullToEmpty ? new NamedValue() : null;

            if (iNv is NamedValue)
                return (iNv as NamedValue);

            return new NamedValue(iNv, asReadOnly: iNv.IsReadOnly);
        }

        /// <summary>
        /// Attempts to convert (or create) a NamedValueSet from the contents of the given <paramref name="vc"/>.
        /// If <paramref name="vc"/> already contains an INamedValueSet then its contents are converted and returned.
        /// If <paramref name="vc"/> contains a list of strings then this method generates and returns a new keyword NVS.
        /// If <paramref name="vc"/> contains a list of VCs then this method converts each to a NV and then generates and returns an NVS.
        /// Otherwise this method generats an NVS containing a single NV created from the given <paramref name="vc"/> and returns it.
        /// <para/>Note: For each of the above cases the resulting INamedValueSet or NamedValueSet is converted to be writable or readonly and null/not based on the <paramref name="asReadOnly"/> and <paramref name="mapNullToEmpty"/> parameter values.
        /// </summary>
        public static NamedValueSet ConvertToNamedValueSet(this ValueContainer vc, bool asReadOnly = false, bool rethrow = false, bool mapNullToEmpty = true)
        {
            INamedValueSet invs = null;

            switch (vc.cvt)
            {
                case ContainerStorageType.INamedValueSet:
                    invs = (vc.o as INamedValueSet);
                    break;
                case ContainerStorageType.IListOfString:
                    {
                        IList<string> sList = vc.GetValueLS(rethrow: rethrow) ?? emptyIListOfString;

                        invs = new NamedValueSet(sList.Select(s => new NamedValue(s) { IsReadOnly = asReadOnly })) { IsReadOnly = asReadOnly };
                    }
                    break;
                case ContainerStorageType.IListOfVC:
                    {
                        IList<ValueContainer> vcList = vc.GetValueL(rethrow: rethrow) ?? emptyIListOfVC;

                        invs = new NamedValueSet(vcList.Select(vcItem => vcItem.ConvertToNamedValue(asReadOnly: asReadOnly, rethrow: rethrow, mapNullToEmpty: mapNullToEmpty))) { IsReadOnly = asReadOnly };
                    }
                    break;
                case ContainerStorageType.None:
                    invs = NamedValueSet.Empty;
                    break;
                default:
                    invs = new NamedValueSet(new [] { vc.ConvertToNamedValue(asReadOnly: asReadOnly, rethrow: rethrow, mapNullToEmpty: mapNullToEmpty) }, asReadOnly: asReadOnly);
                    break;
            }

            if (asReadOnly)
                return invs.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty);
            else
                return invs.ConvertToWritable(mapNullToEmpty: mapNullToEmpty);
        }

        /// <summary>
        /// Converts the given <paramref name="dictionary"/>'s contents to a NamedValueSet and returns it.
        /// </summary>
        public static NamedValueSet ConvertToNamedValueSet<TValueType>(this IDictionary<string, TValueType> dictionary)
        {
            var nvs = new NamedValueSet();

            dictionary.DoForEach(kvp => nvs.SetValue(kvp.Key, ValueContainer.Create(kvp.Value)));

            return nvs;
        }

        /// <summary>
        /// Attempts to convert (or create) a NamedValue from the contents of the given <paramref name="vc"/>.
        /// If the given <paramref name="vc"/> contains a INamedValue then this value is converted and returned.
        /// If the given <paramref name="vc"/> contains a list of strings then this method generates and returns a NamedValue from it (with Name from first element and values from rest)
        /// If the given <paramref name="vc"/> contains a list of VCs then this method generates and returns a NamedValue from it (with Name from first element as a string and values from the rest)
        /// If the given <paramref name="vc"/> is Empty then this method returns an empty NamedValue.
        /// Otherwise this method creates a keyword NamedValue from the given <paramref name="vc"/>'s contents interpreted as a string.
        /// <para/>Note: For each of the above cases the resulting INamedValueSet or NamedValueSet is converted to be writable or readonly and null/not based on the <paramref name="asReadOnly"/> and <paramref name="mapNullToEmpty"/> parameter values.
        /// </summary>
        public static NamedValue ConvertToNamedValue(this ValueContainer vc, bool asReadOnly = false, bool rethrow = false, bool mapNullToEmpty = true)
        {
            INamedValue inv;
            switch (vc.cvt)
            {
                case ContainerStorageType.INamedValue: 
                    inv = (vc.o as INamedValue); 
                    break;
                case ContainerStorageType.IListOfString:
                    {
                        IList<string> sList = vc.GetValueLS(rethrow: rethrow) ?? emptyIListOfString;

                        string nodeName = sList.SafeAccess(0);

                        switch (sList.Count)
                        {
                            case 0: inv = NamedValue.Empty; break;
                            case 1: inv = new NamedValue(nodeName) { IsReadOnly = asReadOnly }; break;
                            case 2: inv = new NamedValue(nodeName, sList[1], asReadOnly: asReadOnly); break;
                            default: inv = new NamedValue(nodeName, sList.SafeSubArray(1), asReadOnly: asReadOnly); break;
                        }
                    }
                    break;
                case ContainerStorageType.IListOfVC:
                    {
                        IList<ValueContainer> vcList = vc.GetValueL(rethrow: rethrow) ?? emptyIListOfVC;

                        string nodeName = vcList.SafeAccess(0).GetValueA(rethrow: rethrow);

                        switch (vcList.Count)
                        {
                            case 0: inv = NamedValue.Empty; break;
                            case 1: inv = new NamedValue(nodeName) { IsReadOnly = asReadOnly }; break;
                            case 2: inv = new NamedValue(nodeName, vcList[1], asReadOnly: asReadOnly); break;
                            default: inv = new NamedValue(nodeName, vcList.SafeSubArray(1), asReadOnly: asReadOnly); break;
                        }
                    }
                    break;
                case ContainerStorageType.None:
                    inv = NamedValue.Empty;
                    break;
                default: 
                    inv = new NamedValue(vc.GetValueA(rethrow: rethrow)); 
                    break;
            }

            if (asReadOnly)
                return inv.ConvertToReadOnly(mapNullToEmpty: mapNullToEmpty);
            else
                return inv.ConvertToWritable(mapNullToEmpty: mapNullToEmpty);
        }

        /// <summary>
        /// Returns true if the given iNvSet is either null or it refers to a NamedValueSet that is currently empty.
        /// </summary>
        public static bool IsNullOrEmpty(this INamedValueSet iNvSet)
        {
            return (iNvSet == null || (iNvSet.Count == 0 && iNvSet.SubSets.IsNullOrEmpty()));
        }

        /// <summary>
        /// Returns true if the given iNv is either null or it refers to a NamedValue that is currently empty (Name IsNullOrEmpty and VC IsEmpty)
        /// </summary>
        public static bool IsNullOrEmpty(this INamedValue iNv)
        {
            return (iNv == null || (iNv.Name.IsNullOrEmpty() && iNv.VC.IsEmpty));
        }

        /// <summary>
        /// Returns true if the given <paramref name="iNvSet"/> is neither Null nor Empty
        /// </summary>
        public static bool IsNeitherNullNorEmpty(this INamedValueSet iNvSet)
        {
            return !iNvSet.IsNullOrEmpty();
        }

        /// <summary>
        /// Returns true if the given <paramref name="iNv"/> is neither Null nor Empty
        /// </summary>
        public static bool IsNeitherNullNorEmpty(this INamedValue iNv)
        {
            return !iNv.IsNullOrEmpty();
        }

        /// <summary>
        /// passes the given <paramref name="iNvSet"/> through as the return value unless it is a non-null, empty set in which case this method returns null.
        /// </summary>
        public static INamedValueSet MapEmptyToNull(this INamedValueSet iNvSet)
        {
            return (iNvSet.IsNullOrEmpty() ? null : iNvSet);
        }

        /// <summary>
        /// passes the given <paramref name="nvSet"/> through as the return value unless it is a non-null, empty set in which case this method returns null.
        /// </summary>
        public static NamedValueSet MapEmptyToNull(this NamedValueSet nvSet)
        {
            return (nvSet.IsNullOrEmpty() ? null : nvSet);
        }

        /// <summary>
        /// passes the given <paramref name="iNvSet"/> through as the return value unless it is a null, in which case this method returns NamedValueSet.Empty.
        /// </summary>
        public static INamedValueSet MapNullToEmpty(this INamedValueSet iNvSet)
        {
            return (iNvSet ?? NamedValueSet.Empty);
        }

        /// <summary>
        /// passes the given <paramref name="nvSet"/> through as the return value unless it is a null, in which case this method returns NamedValueSet.Empty.
        /// </summary>
        public static NamedValueSet MapNullToEmpty(this NamedValueSet nvSet)
        {
            return (nvSet ?? NamedValueSet.Empty);
        }

        /// <summary>
        /// passes the given iNv through as the return value unless it is a non-null, empty set in which case this method returns null.
        /// </summary>
        public static INamedValue MapEmptyToNull(this INamedValue iNv)
        {
            return (iNv.IsNullOrEmpty() ? null : iNv);
        }

        /// <summary>
        /// passes the given iNv through as the return value unless it is a null, in which case this method returns NamedValue.Empty.
        /// </summary>
        public static INamedValue MapNullToEmpty(this INamedValue iNv)
        {
            return (iNv ?? NamedValue.Empty);
        }

        /// <summary>
        /// This method operates on the given <paramref name="lhs"/> NamedValueSet and uses the given <paramref name="mergeBehavior"/> merge behavior to merge the contents of the <paramref name="rhs"/> into the <paramref name="lhs"/>
        /// <para/>If the given <paramref name="lhs"/> IsReadonly then a new NamedValueSet created as a copy of the contents of the given lhs and this new one is used in its place and is returned.
        /// <para/>This method supports call chaining by returning the <paramref name="lhs"/> after any modification have been made.
        /// </summary>
        /// <param name="lhs">Gives the object that the rhs NV items will be merged into</param>
        /// <param name="rhs">Gives the object that contains the NV items that will be merged into the lhs and which may be used to update corresonding items in the lhs</param>
        /// <param name="mergeBehavior">Defines the merge behavior that will be used for this merge when the rhs and lhs contain NV items with the same name but different values.  Defaults to NamedValueMergeBehavior.AddAndUpdate</param>
        public static NamedValueSet MergeWith(this INamedValueSet lhs, INamedValueSet rhs, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
        {
            NamedValueSet lhsRW = lhs.ConvertToWritable(mapNullToEmpty: false);

            if (((mergeBehavior & NamedValueMergeBehavior.Replace) != NamedValueMergeBehavior.None))
                return rhs.ConvertToWritable(mapNullToEmpty: true);

            return lhsRW.MergeWith(rhs as IEnumerable<INamedValue>, mergeBehavior: mergeBehavior);
        }

        /// <summary>
        /// This method operates on the given lhs NamedValueSet and uses the given mergeBehavior to merge the contents of the given rhs into the lhs.
        /// <para/>If the given lhs IsReadonly then a new NamedValueSet created as a copy of the contents of the given lhs and this new one is used in its place and is returned.
        /// <para/>This method supports call chaining by returning the lhs after any modification have been made.
        /// </summary>
        /// <param name="lhs">Gives the object that the rhs NV items will be merged into</param>
        /// <param name="rhsSet">Gives the enumerable object that defines the NV items that will be merged into the lhs and which may be used to update corresonding items in the lhs</param>
        /// <param name="mergeBehavior">Defines the merge behavior that will be used for this merge when the rhs and lhs contain NV items with the same name but different values.</param>
        public static NamedValueSet MergeWith(this INamedValueSet lhs, IEnumerable<INamedValue> rhsSet, NamedValueMergeBehavior mergeBehavior)
        {
            NamedValueSet lhsRW = lhs.ConvertToWritable(mapNullToEmpty: false);

            bool add = mergeBehavior.IsAddSelected();
            bool update = mergeBehavior.IsUpdateSelected();

            bool removeEmpty = ((mergeBehavior & NamedValueMergeBehavior.RemoveEmpty) != 0);
            bool removeNull = ((mergeBehavior & NamedValueMergeBehavior.RemoveNull) != 0);
            bool appendLists = ((mergeBehavior & NamedValueMergeBehavior.AppendLists) != 0);
            bool isSum = ((mergeBehavior & NamedValueMergeBehavior.Sum) != 0);

            if (lhsRW == null || lhsRW.IsReadOnly)
            {
                lhsRW = lhsRW ?? NamedValueSet.Empty;
                lhsRW = new NamedValueSet(lhsRW.GetEnumerable(TraversalType.TopLevelOnly), asReadOnly: false, subSets: lhsRW.SubSets);
            }

            if (((mergeBehavior & NamedValueMergeBehavior.Replace) != NamedValueMergeBehavior.None))
            {
                if (lhsRW.Count > 0)
                    lhsRW.Clear();

                lhsRW.AddRange(rhsSet);

                return lhsRW;
            }

            if (rhsSet != null)
            {
                foreach (INamedValue rhsItem in rhsSet)
                {
                    bool lhsContainsRhsName = lhsRW.Contains(rhsItem.Name);
                    bool rhsIsIListOfString = rhsItem.VC.cvt == ContainerStorageType.IListOfString;
                    bool rhsIsIListOfVC = rhsItem.VC.cvt == ContainerStorageType.IListOfVC;

                    if (lhsContainsRhsName)
                    {
                        bool mayBeAppend = appendLists && (rhsIsIListOfString || rhsIsIListOfVC);
                        INamedValue lhsItem = ((isSum || mayBeAppend) ? lhsRW[rhsItem.Name] : null);
                        bool isAppend = (mayBeAppend && lhsItem != null && lhsItem.VC.cvt == rhsItem.VC.cvt);

                        if ((removeEmpty && rhsItem.VC.IsEmpty) || (removeNull && rhsItem.VC.IsNull))
                        {
                            lhsRW.Remove(rhsItem.Name);
                        }
                        else if (update)
                        {
                            if (isSum)
                                lhsRW.SetValue(rhsItem.Name, lhsItem.VC.Sum(rhsItem.VC, allowUpcastAttempts: ((mergeBehavior & NamedValueMergeBehavior.EnableUpcast) != 0)));
                            else if (!isAppend)
                                lhsRW.SetValue(rhsItem.Name, rhsItem.VC);
                            else if (rhsIsIListOfString)
                                lhsRW.SetValue(rhsItem.Name, new ReadOnlyIList<string>((lhsItem.VC.GetValueLS(false) ?? emptyIListOfString).Concat(rhsItem.VC.GetValueLS(false) ?? emptyIListOfString)));
                            else
                                lhsRW.SetValue(rhsItem.Name, new ReadOnlyIList<ValueContainer>((lhsItem.VC.GetValueL(false) ?? emptyIListOfVC).Concat(rhsItem.VC.GetValueL(false) ?? emptyIListOfVC)));
                        }
                        // else leave the existing lhs item alone.
                    }
                    else if (add)
                    {
                        lhsRW.SetValue(rhsItem.Name, rhsItem.VC);
                    }
                    // else do not add the non-matching item to the lhs.
                }
            }

            return lhsRW;
        }

        private static readonly IList<string> emptyIListOfString = ReadOnlyIList<string>.Empty;
        private static readonly IList<ValueContainer> emptyIListOfVC = ReadOnlyIList<ValueContainer>.Empty;

        /// <summary>
        /// Returns a ValueContainer containing the sum of the given <paramref name="lhs"/> and <paramref name="rhs"/> values, 
        /// if the two values have the same contained type and that type is "sumable", 
        /// or returns the given <paramref name="lhs"/> value if they are not.
        /// if the <paramref name="lhs"/> value is Empty then this method returns the <paramref name="rhs"/> value.
        /// </summary>
        public static ValueContainer Sum(this ValueContainer lhs, ValueContainer rhs, bool allowUpcastAttempts = false, bool rethrow = false)
        {
            bool isCompatible = (lhs.cvt == rhs.cvt);

            if (!isCompatible && allowUpcastAttempts)
            {
                var upcastedCST = lhs.cvt.Upcast(rhs.cvt);

                if (lhs.cvt != upcastedCST)
                    lhs = lhs.Cast(upcastedCST, fallbackValue: lhs, allowUpcastAttempt: true, rethrow: rethrow);

                if (rhs.cvt != upcastedCST)
                    rhs = rhs.Cast(upcastedCST, fallbackValue: rhs, allowUpcastAttempt: true, rethrow: rethrow);

                isCompatible = (lhs.cvt == rhs.cvt);
            }

            ValueContainer result = lhs;

            if (isCompatible)
            {
                switch (lhs.cvt)
                {
                    case ContainerStorageType.String: result.o = string.Concat((lhs.o as string), (rhs.o as string)); break;      // string.Concat does MapNullToEmpty on its own
                    case ContainerStorageType.IListOfString: result.SetValueLS(new ReadOnlyIList<string>(lhs.GetValue(false, emptyIListOfString).Concat(rhs.GetValue(false, emptyIListOfString)))); break;
                    case ContainerStorageType.IListOfVC: result.SetValueL(new ReadOnlyIList<ValueContainer>(lhs.GetValue(false, emptyIListOfVC).Concat(rhs.GetValue(false, emptyIListOfVC)))); break;
                    case ContainerStorageType.Boolean: result.u.b = lhs.u.b | rhs.u.b; break;
                    case ContainerStorageType.Binary: result.u.bi = unchecked((Byte)(lhs.u.bi + rhs.u.bi)); break;
                    case ContainerStorageType.SByte: result.u.i8 = unchecked((SByte)(lhs.u.i8 + rhs.u.i8)); break;
                    case ContainerStorageType.Int16: result.u.i16 = unchecked((Int16)(lhs.u.i16 + rhs.u.i16)); break;
                    case ContainerStorageType.Int32: result.u.i32 = lhs.u.i32 + rhs.u.i32; break;
                    case ContainerStorageType.Int64: result.u.i64 = lhs.u.i64 + rhs.u.i64; break;
                    case ContainerStorageType.Byte: result.u.u8 = unchecked((Byte)(lhs.u.u8 + rhs.u.u8)); break;
                    case ContainerStorageType.UInt16: result.u.u16 = unchecked((UInt16)(lhs.u.u16 + rhs.u.u16)); break;
                    case ContainerStorageType.UInt32: result.u.u32 = lhs.u.u32 + rhs.u.u32; break;
                    case ContainerStorageType.UInt64: result.u.u64 = lhs.u.u64 + rhs.u.u64; break;
                    case ContainerStorageType.Single: result.u.f32 = lhs.u.f32 + rhs.u.f32; break;
                    case ContainerStorageType.Double: result.u.f64 = lhs.u.f64 + rhs.u.f64; break;
                    case ContainerStorageType.TimeSpan: result.u.TimeSpan = lhs.u.TimeSpan + rhs.u.TimeSpan; break;
                    case ContainerStorageType.DateTime:
                    default:
                        break;
                }
            }
            else if (lhs.IsEmpty && (allowUpcastAttempts || rhs.cvt == ContainerStorageType.Boolean))
            {
                result = rhs;
            }

            return result;
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

        /// <summary>
        /// Attemps to "Add" the contents of each of the given KeyValuePair items to this set.
        /// Returns this object to support call chaining.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public static NamedValueSet AddRange<TElementType>(this NamedValueSet nvs, IEnumerable<KeyValuePair<string, TElementType>> kvpSet)
        {
            nvs.AddRange(kvpSet.Select(kvp => new NamedValue(kvp.Key, kvp.Value)));

            return nvs;
        }

        /// <summary>
        /// Attemps to "Add" the contents of each of the given dictionary's DictionaryItem items to this set.
        /// Returns this object to support call chaining.
        /// </summary>
        /// <exception cref="System.NotSupportedException">thrown if the collection has been set to IsReadOnly</exception>
        public static NamedValueSet AddRange(this NamedValueSet nvs, IDictionary dictionary)
        {
            if (nvs != null && dictionary != null)
                nvs.AddRange(from DictionaryEntry entry in dictionary select new NamedValue(entry.Key as string, entry.Value));

            return nvs;
        }

        /// <summary>
        /// Obtains the <paramref name="name"/>ed NamedValue from the given <paramref name="nvs"/> NamedValueSet and then removes it from the <paramref name="nvs"/> and returns it.
        /// </summary>
        public static NamedValue RemoveAndReturnNamedValue(this NamedValueSet nvs, string name)
        {
            var nv = nvs[name];
            nvs.Remove(name);
            return nv;
        }
    }

    /// <summary>
    /// This Flag enumeration is used to help specify the caller's specific desired behavior when "merging" two or more INamedValueSet objects (a from NVS and an into NVS).
    /// <para/>None (0x00), AddNewItems (0x01), UpdateExistingItems(0x02), RemoveEmpty(0x04), RemoveNull(0x08), AppendLists (0x10), Sum (0x20), Replace(0x40) and useful combinations of these.
    /// </summary>
    [Flags]
    public enum NamedValueMergeBehavior
    {
        /// <summary>Placeholder default value [0x00]</summary>
        None = 0x00,

        /// <summary>Merge by adding new items from the rhs into the lhs only if the lhs does not already contains an element with the same name. [0x01]</summary>
        AddNewItems = 0x01,

        /// <summary>Merge by replacing the values for selected items in the lhs with the values of corresondingly named items in the rhs. [0x02]</summary>
        UpdateExistingItems = 0x02,

        /// <summary>
        /// Select to remove items from the lhs when a corresondingly named item is found in the rhs that has the empty value (also called a keyword item) [0x04]
        /// <para/>The two variants of Remove are provided to support two different means of selecting which lhs items are to be removed so that the caller has control of any potential ambiguity in the use of keyword and/or null update/add items in the rhs
        /// </summary>
        RemoveEmpty = 0x04,

        /// <summary>
        /// Select to remove items from the lhs when a corresondingly named item if found in the rhs that has been given the value of null [0x08]
        /// <para/>The two variants of Remove are provided to support two different means of selecting which lhs items are to be removed so that the caller has control of any potential ambiguity in the use of keyword and/or null update/add items in the rhs
        /// </summary>
        RemoveNull = 0x08,

        /// <summary>Select to request that the merge operation will concatinate the contents of corresonding list objects.  This behavior is only useful when combined with Update [0x10]</summary>
        AppendLists = 0x10,

        /// <summary>Select to request that the merge operation will sum corresponding contents of corresonding objects.  This behavior is only useful when combined with Update [0x10]</summary>
        Sum = 0x20,

        /// <summary>Select to request that the merge operation produce the given merge from NVS without change.</summary>
        Replace = 0x40,

        /// <summary>Combine with Sum to allow the sum to cast the Upcast the values if needed and supported</summary>
        EnableUpcast = 0x80,

        /// <summary>Shorthand for AddNewItems [0x01]</summary>
        AddOnly = AddNewItems,

        /// <summary>Shorthand for UpdateExistingItems [0x02]</summary>
        UpdateOnly = UpdateExistingItems,

        /// <summary>Shorthand for AddNewItems | UpdateExistingItems [0x03]</summary>
        AddAndUpdate = AddNewItems | UpdateExistingItems,
    }

    #endregion

    #region name mapping: IMapNameFromTo, MapNameFromTo, RegexMapNameFromTo and MapNameFromToList

    /// <summary>
    /// Interface for objects/classes that are used to support name mapping.
    /// </summary>
    public interface IMapNameFromTo
    {
        /// <summary>Gives the From string (name, prefix, regex) for the mapping</summary>
        string From { get; }

        /// <summary>Gives the To string (name, prefix, regex) for the mapping</summary>
        string To { get; }

        /// <summary>
        /// Returns true if this is a simple map, or false if it is not (such as for a Regular Expression)
        /// </summary>
        bool IsSimpleMap { get; }

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string.
        /// </summary>
        bool CanMap(string from);

        /// <summary>
        /// If this map can convert the given from string then it assigns the converted value to the given output to parameter and returns true.
        /// If this map does not match the given from string or if the conversion fails then this method does not modify the to paramter and instead returns false.
        /// </summary>
        bool Map(string from, ref string to);

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string in the direction that inverts the normal map direction.
        /// </summary>
        bool CanInverseMap(string from);

        /// <summary>
        /// If this map can convert the given from string in the inverse/reverse direction then it assigns the converted value to the given output to parameter and returns true.
        /// If this map does not match the given from string or if the inverse conversion fails then this method does not modify the to paramter and instead returns false.
        /// </summary>
        bool MapInverse(string from, ref string to);
    }

    /// <summary>
    /// Immutable item instance class for mapping names from one value to another.  
    /// Generally used with MapNamesFromToList when interacting with values interconnect's MapNameFromToSet and related AddRange method
    /// <para/>This class supports use with DataContract serialization and deserialization.
    /// </summary>
    [DataContract(Namespace = Constants.ModularCommonNameSpace)]
    public class MapNameFromTo : IMapNameFromTo
    {
        /// <summary>
        /// Basic constructor used to set the instance's From and To property values to the given ones.
        /// </summary>
        public MapNameFromTo(string from, string to)
        {
            From = from;
            To = to;
        }

        /// <summary>Gives the from name for the mapping (the name that will be replaced with the To name).</summary>
        [DataMember]
        public string From { get; private set; }

        /// <summary>Gives the to name for the mapping (the name that will actually be used to access the table).</summary>
        [DataMember]
        public string To { get; private set; }

        /// <summary>Debugging and logging assistance method</summary>
        public override string ToString()
        {
            return "'{0}'=>'{1}'".CheckedFormat(From, To);
        }

        /// <summary>
        /// Returns true if this is a simple map, or false if it is not (such as for a Regular Expression)
        /// </summary>
        public virtual bool IsSimpleMap { get { return true; } }

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string.
        /// </summary>
        public virtual bool CanMap(string from)
        {
            return (from == From);
        }

        /// <summary>
        /// If this map can convert the given from string then it assigns the converted value to the given output to parameter and returns true.
        /// If this map does not match the given from string or if the conversion fails then this method does not modify the to paramter and instead returns false.
        /// </summary>
        public virtual bool Map(string from, ref string to)
        {
            if (!CanMap(from))
                return false;

            to = To;
            return true;
        }

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string in the direction that inverts the normal map direction.
        /// </summary>
        public virtual bool CanInverseMap(string from)
        {
            return (from == To);
        }

        /// <summary>
        /// If this map can convert the given from string in the inverse/reverse direction then it assigns the converted value to the given output to parameter and returns true.
        /// If this map does not match the given from string or if the inverse conversion fails then this method does not modify the to paramter and instead returns false.
        /// </summary>
        public virtual bool MapInverse(string from, ref string to)
        {
            if (!CanInverseMap(from))
                return false;

            to = From;
            return true;
        }
    }

    /// <summary>
    /// Immutable item instance class which can be used to accept (match) any Map but which implements a noop transform function (from gets to)
    /// </summary>
    public class MapNameAcceptAll : MapNameFromTo
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public MapNameAcceptAll()
            : base(string.Empty, string.Empty)
        { }

        public override string ToString()
        {
            return "AcceptAll";
        }

        /// <summary>
        /// This is not a simple map as it can map anything to itself
        /// </summary>
        public override bool IsSimpleMap { get { return false; } }

        /// <summary>
        /// This can map any string
        /// </summary>
        public override bool CanMap(string from)
        {
            return true;
        }

        /// <summary>
        /// This maps any string to itself
        /// </summary>
        public override bool Map(string from, ref string to)
        {
            to = from;
            return true;
        }

        /// <summary>
        /// This can inverse map any string
        /// </summary>
        public override bool CanInverseMap(string from)
        {
            return true;
        }

        /// <summary>
        /// This inverse maps any string to itself
        /// </summary>
        public override bool MapInverse(string from, ref string to)
        {
            to = from;
            return true;
        }
    }

    /// <summary>
    /// Immutable item instance class for use of common prefix replacement based mapping of names from one value to another.  
    /// Generally used with MapNamesFromToList when interacting with values interconnect's MapNameFromToSet and related AddRange method
    /// <para/>This class supports use with DataContract serialization and deserialization.
    /// </summary>
    [DataContract(Namespace = Constants.ModularCommonNameSpace)]
    public class MapNamePrefixFromTo : MapNameFromTo
    {
        /// <summary>
        /// Basic constructor used to set the instance's From (prefix) and To (prefix) property values to the given ones.
        /// </summary>
        public MapNamePrefixFromTo(string fromPrefix, string toPrefix)
            : base(fromPrefix, toPrefix)
        { }

        /// <summary>
        /// Returns true if this is a simple map, or false if it is not.  (not in this case)
        /// </summary>
        public override bool IsSimpleMap { get { return false; } }

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string.
        /// </summary>
        public override bool CanMap(string from)
        {
            return from.MapNullToEmpty().StartsWith(From);
        }

        /// <summary>
        /// If this map can convert the given from string then it assigns the converted value to the given output to parameter and returns true.
        /// If this map does not match the given from string or if the conversion fails then this method does not modify the to paramter and instead returns false.
        /// </summary>
        public override bool Map(string from, ref string to)
        {
            if (!from.MapNullToEmpty().StartsWith(From))
                return false;

            to = "{0}{1}".CheckedFormat(To, from.Substring(From.Length));
            return true;
        }

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string in the direction that inverts the normal map direction.
        /// </summary>
        public override bool CanInverseMap(string from)
        {
            return from.MapNullToEmpty().StartsWith(To);
        }

        /// <summary>
        /// If this map can convert the given from string in the inverse/reverse direction then it assigns the converted value to the given output to parameter and returns true.
        /// If this map does not match the given from string or if the inverse conversion fails then this method does not modify the to paramter and instead returns false.
        /// </summary>
        public override bool MapInverse(string from, ref string to)
        {
            if (!from.MapNullToEmpty().StartsWith(To))
                return false;

            to = "{0}{1}".CheckedFormat(From, from.Substring(To.Length));
            return true;
        }

    }

    /// <summary>
    /// Immutable item instance class for use of Regular expressions <seealso cref="System.Text.RegularExpressions.Regex"/> for mapping names from one value to another.  
    /// Generally used with MapNamesFromToList when interacting with values interconnect's MapNameFromToSet and related AddRange method
    /// <para/>This class supports use with DataContract serialization and deserialization.
    /// </summary>
    [DataContract(Namespace = Constants.ModularCommonNameSpace)]
    public class RegexMapNameFromTo : MapNameFromTo
    {
        /// <summary>
        /// Basic constructor used to set the instance's From (regex expression) and To (regex expression) property values to the given ones.
        /// </summary>
        public RegexMapNameFromTo(string from, string to)
            : base(from, to)
        {
            UpdateRegex();
        }

        System.Text.RegularExpressions.Regex regexFrom = null;

        void UpdateRegex()
        {
            if (regexFrom == null)
                regexFrom = new System.Text.RegularExpressions.Regex(From);
        }

        /// <summary>
        /// Returns true if this is a simple map, or false if it is not (such as for a Regular Expression)
        /// </summary>
        public override bool IsSimpleMap { get { return false; } }

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string.
        /// </summary>
        public override bool CanMap(string from)
        {
            try
            {
                UpdateRegex();

                System.Text.RegularExpressions.Match match = regexFrom.Match(from);
                return match.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// If this map can convert the given from string then it assigns the converted value to the given output to parameter and returns true.
        /// If this map does not match the given from string or if the conversion fails then this method does not modify the to paramter and instead returns false.
        /// </summary>
        public override bool Map(string from, ref string to)
        {
            try
            {
                UpdateRegex();

                System.Text.RegularExpressions.Match match = regexFrom.Match(from);
                if (match != null && match.Success)
                {
                    to = match.Result(To);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string in the direction that inverts the normal map direction.
        /// <para/>Regex maps do not currently support inverse mapping.
        /// </summary>
        public override bool CanInverseMap(string from) 
        { 
            return false; 
        }
    }

    /// <summary>
    /// MapNameFromTo basic Collection class for mapping sets of names from one value to another.  
    /// Generally used when interacting with values interconnect's MapNameFromToSet and related AddRange method.
    /// <para/>This class supports use with DataContract serialization and deserialization.
    /// </summary>
    [CollectionDataContract(ItemName = "Map", Namespace = Constants.ModularCommonNameSpace)]
    [KnownType(typeof(MapNameFromTo))]
    [KnownType(typeof(RegexMapNameFromTo))]
    public class MapNameFromToList : List<IMapNameFromTo>, IMapNameFromTo
    {
        /// <summary>Constructs an empty list.</summary>
        public MapNameFromToList() { }

        /// <summary>Constructs a list containing the elements from the given rhs enumerable source.</summary>
        public MapNameFromToList(IEnumerable<IMapNameFromTo> rhs) : base(rhs) { }

        /// <summary>Helper method allows use with nested braces initialization (as with Dictionary)</summary>
        public MapNameFromToList Add(string from, string to)
        {
            Add(new MapNameFromTo(from, to));
            return this;
        }

        /// <summary>Debugging and logging assistance method</summary>
        public override string ToString()
        {
            return "MapNameFromToList({0})".CheckedFormat(string.Join(",", this.Select((item) => item.ToString()).ToArray()));
        }

        /// <summary>Returns the empty string (From is not supported by this type)</summary>
        public string From { get { return string.Empty; } }

        /// <summary>Returns the empty string (To is not supported by this type)</summary>
        public string To { get { return string.Empty; } }

        /// <summary>
        /// Returns true if this is a simple map, or false if it is not (such as for a Regular Expression)
        /// </summary>
        public bool IsSimpleMap { get { return this.All(map => map.IsSimpleMap); } }

        /// <summary>
        /// Returns true if at least one of the maps in this list can be used to match and map the given from string.
        /// </summary>
        public bool CanMap(string from)
        {
            return this.Any(mapFromTo => mapFromTo.CanMap(from));
        }

        /// <summary>
        /// If this list of maps can convert the given from string then the first successful match is used to assign the converted value to the given output to parameter and this method returns true.
        /// If this list maps does not find a match for the given from string then this method does not modify the to paramter and instead returns false.
        /// </summary>
        public bool Map(string from, ref string to)
        {
            foreach (IMapNameFromTo map in this)
            {
                if (map.Map(from, ref to))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if this map can be used to match and map the given from string in the direction that inverts the normal map direction.
        /// </summary>
        public bool CanInverseMap(string from)
        {
            return this.Any(mapFromTo => mapFromTo.CanInverseMap(from));
        }

        /// <summary>
        /// If this map can convert the given from string in the inverse/reverse direction then it assigns the converted value to the given output to parameter and returns true.
        /// If this map does not match the given from string or if the inverse conversion fails then this method does not modify the to paramter and instead returns false.
        /// </summary>
        public bool MapInverse(string from, ref string to)
        {
            foreach (IMapNameFromTo map in this)
            {
                if (map.MapInverse(from, ref to))
                    return true;
            }

            return false;
        }
    }

    #endregion

    #region NamedValueSetItemAttribute, INamedValueSetAdapter, NamedValueSetAdapter

    namespace Attributes
    {
        /// <summary>
        /// This attribute is used to annotate public get/set properties and fields in a class in order that the class can be used as the ValueSet for
        /// a NamedValueSetAdapter adapter.  Each such property or field in the ValueSet class specifies a specific property and value source that 
        /// will receive the values from Update calls and which is used as the value source for Set calls on the Adapter.
        /// <para/>Name = null, NameAdjust = NameAdjust.Prefix0, SilenceIssues = false
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
        public class NamedValueSetItemAttribute : AnnotatedItemAttributeBase
        {
            /// <summary>
            /// Default constructor.
            /// <para/>Name = null, NameAdjust = NameAdjust.Prefix0, SilenceIssues = false, StorageType = ContainerStorageType.None
            /// </summary>
            public NamedValueSetItemAttribute()
            { }
        }
    }

    /// <summary>
    /// ValueSet type agnostic interface for public methods in actual NamedValueSetAdapter implementation class
    /// </summary>
    public interface INamedValueSetAdapter
    {
        /// <summary>Defines the emitter used to emit Setup, Set, and Update related errors.  Defaults to the null emitter.</summary>
        Logging.IMesgEmitter IssueEmitter { get; set; }

        /// <summary>Defines the emitter used to emit Update related changes in config point values.  Defaults to the null emitter.</summary>
        Logging.IMesgEmitter ValueNoteEmitter { get; set; }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        INamedValueSetAdapter Setup(params string[] baseNames);

        /// <summary>
        /// Processes the given nvs and extracts and assigns values to each of the adapters annotated ValueSet members from the correspondingly named values in the given nvs.
        /// <para/>The merge parameter determines if (false) all annotated items are set (even if they are not included in the given nvs), or if (true) only the items that are explicitly included in the given nvs are set.
        /// <para/>Supports call chaining.
        /// </summary>
        INamedValueSetAdapter Set(INamedValueSet nvs, bool merge = false);

        /// <summary>
        /// Generates and returns an INamedValueSet with named values for each of the identified items in the adapter's ValueSet.
        /// <para/>If requested using the optional asReadOnly parameter, the returned nvs will be set to be read-only.
        /// </summary>
        INamedValueSet Get(bool asReadOnly = false);
    }

    /// <summary>
    /// This adapter class provides a client with a ValueSet style tool that supports getting and setting sets of values to/from INamedValueSet instances.
    /// </summary>
    /// <typeparam name="TValueSet">
    /// Specifies the class type on which this adapter will operate.  
    /// Adapter harvests the list of <see cref="MosaicLib.Modular.Common.Attributes.NamedValueSetItemAttribute"/> annotated public member items from this type.
    /// </typeparam>
    /// <remarks>
    /// The primary methods/properties used on this adapter are: Construction, ValueSet, Setup, Set, Get
    /// </remarks>
    public class NamedValueSetAdapter<TValueSet>
        : NamedValueSetAdapter<TValueSet, Attributes.NamedValueSetItemAttribute>
        where TValueSet : class
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NamedValueSetAdapter(ItemSelection itemSelection = (ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeInheritedItems)) 
            : base(itemSelection: itemSelection) 
        { }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public new NamedValueSetAdapter<TValueSet> Setup(params string[] baseNames)
        {
            base.Setup(baseNames: baseNames);

            return this;
        }

        /// <summary>
        /// Processes the given nvs and extracts and assigns values to each of the adapters annotated ValueSet members from the correspondingly named values in the given nvs.
        /// <para/>The merge parameter determines if (false) all annotated items are set (even if they are not included in the given nvs), or if (true) only the items that are explicitly included in the given nvs are set.
        /// <para/>Supports call chaining.
        /// </summary>
        public new NamedValueSetAdapter<TValueSet> Set(INamedValueSet nvs, bool merge = false)
        {
            base.Set(nvs: nvs, merge: merge);

            return this;
        }
    }

    /// <summary>
    /// This adapter class provides a client with a ValueSet style tool that supports getting and setting sets of values to/from INamedValueSet instances.
    /// </summary>
    /// <typeparam name="TValueSet">
    /// Specifies the class type on which this adapter will operate.  
    /// Adapter harvests the list of <see cref="MosaicLib.Modular.Common.Attributes.NamedValueSetItemAttribute"/> annotated public member items from this type.
    /// </typeparam>
    /// <typeparam name="TAttribute">
    /// Allows the client to customize this adapter to make use of any <seealso cref="Attributes.NamedValueSetItemAttribute"/> derived attribute type.
    /// This is intended to allow the client to make use of multiple custom attribute types in order to customize which adapter any given annotated item in a value set class the item is intended to be used with.
    /// </typeparam>
    /// <remarks>
    /// The primary methods/properties used on this adapter are: Construction, ValueSet, Setup, Set, Get
    /// </remarks>
    public class NamedValueSetAdapter<TValueSet, TAttribute> : INamedValueSetAdapter
        where TValueSet : class
        where TAttribute : Attributes.NamedValueSetItemAttribute, new()
    {
        #region Ctor

        /// <summary>
        /// Config instance constructor.  Assigns adapter to use given configInstance IConfig service instance.  This may be overridden during the Setup call.
        /// For use with Property Initializers and the Setup method to define and setup the adapter instance for use.
        /// <para/>Please Note: the Setup method must be called before the adapter can be used.  
        /// </summary>
        public NamedValueSetAdapter(ItemSelection itemSelection = (ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeInheritedItems | ItemSelection.UseStrictAttributeTypeChecking))
        {
            valueSetItemInfoList = AnnotatedClassItemAccessHelper<TAttribute>.ExtractItemInfoAccessListFrom(typeof(TValueSet), itemSelection);
            NumItems = valueSetItemInfoList.Count;

            MustSupportGet = MustSupportSet = true;

            itemAccessSetupInfoArray = new ItemAccessSetupInfo<TAttribute>[NumItems];
        }

        #endregion

        #region public methods and properies

        /// <summary>
        /// Contains the ValueSet object that is used as the value source for Set calls and receives updated values during Update.
        /// </summary>
        public TValueSet ValueSet { get; set; }

        /// <summary>Defines the emitter used to emit Setup, Set, and Update related errors.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter IssueEmitter { get { return FixupEmitterRef(ref issueEmitter); } set { issueEmitter = value; } }

        /// <summary>Defines the emitter used to emit Update related changes in config point values.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter ValueNoteEmitter { get { return FixupEmitterRef(ref valueNoteEmitter); } set { valueNoteEmitter = value; } }

        /// <summary>When true (the default), TValueSet class is expected to provide public setters for all annotated properties</summary>
        public bool MustSupportSet { get; set; }

        /// <summary>When true (the default), TValueSet class is expected to provide public getters for all annotated properties</summary>
        public bool MustSupportGet { get; set; }

        /// <summary>This property helps define the set of behaviors that this adapter shall perform.  It defaults to ItemAccess.Normal (Get and Set).  Setting it to any other value will also clear the corresponding MustSupport flag(s)</summary>
        public ItemAccess ItemAccess
        {
            get { return _itemAccess; }
            set
            {
                _itemAccess = value;
                if (!_itemAccess.IsSet(ItemAccess.UseGetterIfPresent))
                    MustSupportGet = false;
                if (!_itemAccess.IsSet(ItemAccess.UseSetterIfPresent))
                    MustSupportSet = false;
            }
        }
        private ItemAccess _itemAccess = ItemAccess.Normal;

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public NamedValueSetAdapter<TValueSet, TAttribute> Setup(params string[] baseNames)
        {
            // setup all of the static information

            for (int idx = 0; idx < NumItems; idx++)
            {
                ItemInfo<TAttribute> itemInfo = valueSetItemInfoList[idx];
                Attributes.NamedValueSetItemAttribute itemAttribute = itemInfo.ItemAttribute;

                string memberName = itemInfo.MemberInfo.Name;
                string itemName = (!string.IsNullOrEmpty(itemAttribute.Name) ? itemAttribute.Name : itemInfo.MemberInfo.Name);
                string nvsItemName = itemInfo.GenerateFullName(baseNames);

                if (MustSupportGet && !itemInfo.CanGetValue)
                {
                    if (!itemAttribute.SilenceIssues)
                        IssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: Member must provide public getter, in ValueSet type '{2}'", memberName, nvsItemName, TValueSetTypeStr);

                    continue;
                }

                if (MustSupportSet && !itemInfo.CanSetValue)
                {
                    if (!itemAttribute.SilenceIssues) 
                        IssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: Member must provide public setter, in ValueSet type '{2}'", memberName, nvsItemName, TValueSetTypeStr);

                    continue;
                }

                var itemAccessSetupInfo = new ItemAccessSetupInfo<TAttribute>()
                {
                    NVSItemName = nvsItemName,
                    ItemInfo = itemInfo,
                    MemberToValueFunc = ItemAccess.UseGetter() ? itemInfo.GenerateGetMemberToVCFunc<TValueSet>() : null,
                    MemberFromValueAction = ItemAccess.UseSetter() ? itemInfo.GenerateSetMemberFromVCAction<TValueSet>(forceRethrowFlag: false) : null,
                };

                Logging.IMesgEmitter selectedIssueEmitter = IssueEmitter;

                if (itemAccessSetupInfo.MemberToValueFunc == null)
                {
                    if (MustSupportGet && ItemAccess.UseGetter() && itemInfo.CanGetValue && !itemAttribute.SilenceIssues) 
                        selectedIssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: no valid accessor delegate could be generated for its ValueSet type:'{3}'", memberName, nvsItemName, itemInfo.ItemType, TValueSetTypeStr);
                }

                if (itemAccessSetupInfo.MemberFromValueAction == null)
                {
                    if (MustSupportSet && ItemAccess.UseSetter() && itemInfo.CanSetValue && !itemAttribute.SilenceIssues)
                        selectedIssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: no valid accessor delegate could be generated for its ValueSet type:'{3}'", memberName, nvsItemName, itemInfo.ItemType, TValueSetTypeStr);
                }

                itemAccessSetupInfoArray[idx] = itemAccessSetupInfo;
            }

            return this;
        }

        /// <summary>
        /// Processes the given nvs and extracts and assigns values to each of the adapters annotated ValueSet members from the correspondingly named values in the given nvs.
        /// <para/>The merge parameter determines if (false) all annotated items are set (even if they are not included in the given nvs), or if (true) only the items that are explicitly included in the given nvs are set.
        /// <para/>Supports call chaining.
        /// </summary>
        public NamedValueSetAdapter<TValueSet, TAttribute> Set(INamedValueSet nvs, bool merge = false)
        {
            if (ValueSet == null)
            {
                IssueEmitter.Emit("ValueSet property must be non-null before Set can be called");
                return this;
            }

            return Set(nvs, ValueSet, merge: merge);
        }

        /// <summary>
        /// Processes the given nvs and extracts and assigns values to each of the adapters annotated <paramref name="valueSet"/> members from the correspondingly named values in the given nvs.
        /// <para/>The merge parameter determines if (false) all annotated items are set (even if they are not included in the given nvs), or if (true) only the items that are explicitly included in the given nvs are set.
        /// <para/>Supports call chaining.
        /// </summary>
        public NamedValueSetAdapter<TValueSet, TAttribute> Set(INamedValueSet nvs, TValueSet valueSet, bool merge = false)
        {
            if (valueSet == null)
                new System.ArgumentNullException("valueSet").Throw();

            foreach (var iasi in itemAccessSetupInfoArray)
            {
                INamedValue inv = nvs[iasi.NVSItemName];

                if (iasi != null && iasi.MemberFromValueAction != null && (!merge || !inv.IsNullOrEmpty()))
                    iasi.MemberFromValueAction(valueSet, inv.VC, IssueEmitter, ValueNoteEmitter, rethrow: false);
            }

            return this;
        }

        /// <summary>
        /// Generates and returns an INamedValueSet with named values for each of the identified items in the adapter's ValueSet.
        /// <para/>If requested using the optional asReadOnly parameter, the returned nvs will be set to be read-only.
        /// </summary>
        public INamedValueSet Get(bool asReadOnly = false)
        {
            if (ValueSet == null)
                new System.NullReferenceException("ValueSet property must be non-null before Get can be called").Throw();

            return Get(ValueSet, asReadOnly: asReadOnly);
        }

        /// <summary>
        /// Generates and returns an INamedValueSet with named values for each of the identified items in the given <paramref name="valueSet"/>.
        /// <para/>If requested using the optional <paramref name="asReadOnly"/> parameter, the returned nvs will be set to be read-only.
        /// </summary>
        public INamedValueSet Get(TValueSet valueSet, bool asReadOnly = false)
        {
            if (valueSet == null)
                new System.ArgumentNullException("valueSet").Throw();

            NamedValueSet nvs = new NamedValueSet();

            foreach (var iasi in itemAccessSetupInfoArray)
            {
                if (iasi != null && iasi.MemberToValueFunc != null)
                    nvs.SetValue(iasi.NVSItemName, iasi.MemberToValueFunc(valueSet, IssueEmitter, ValueNoteEmitter, rethrow: false));
            }

            if (asReadOnly)
                nvs.IsReadOnly = true;

            return nvs;
        }

        #endregion

        #region private fields, properties

        readonly Type TValueSetType = typeof(TValueSet);
        readonly string TValueSetTypeStr = typeof(TValueSet).Name;

        List<ItemInfo<TAttribute>> valueSetItemInfoList = null;       // gets built by the AnnotatedClassItemAccessHelper.
        int NumItems { get; set; }

        /// <summary>
        /// Internal class used to capture the key specific setup information for a given annotated property in the ValueSet.
        /// </summary>
        private class ItemAccessSetupInfo<TItemAttribute>
            where TItemAttribute : Attributes.NamedValueSetItemAttribute, new()
        {
            /// <summary>
            /// Retains access to the ItemInfo for the corresponding item in the value set
            /// </summary>
            public ItemInfo<TItemAttribute> ItemInfo { get; set; }

            /// <summary>
            /// Returns the ItemAttribute from the contained ItemInfo
            /// </summary>
            public Attributes.NamedValueSetItemAttribute ItemAttribute { get { return ItemInfo.ItemAttribute; } }

            /// <summary>
            /// Returns the symbol name of the Property or Field to which this item is attached.
            /// </summary>
            public string MemberName { get { return ItemInfo.MemberInfo.Name; } }

            /// <summary>
            /// Gives the item name to use for this item in corresponding NamedValueSet instances.
            /// </summary>
            public string NVSItemName { get; set; }

            /// <summary>delegate that is used to set a specific member's value from a given config key's value object's stored value.</summary>
            /// <remarks>this item will be null for static items and for items that failed to be setup correctly.</remarks>
            public AnnotatedClassItemAccessHelper.GetMemberAsVCFunctionDelegate<TValueSet> MemberToValueFunc { get; set; }

            /// <summary>delegate that is used to set a specific member's value from a given config key's value object's stored value.</summary>
            /// <remarks>this item will be null for static items and for items that failed to be setup correctly.</remarks>
            public AnnotatedClassItemAccessHelper.SetMemberFromVCActionDelegate<TValueSet> MemberFromValueAction { get; set; }
        }

        /// <remarks>Non-null elements in this array correspond to fully vetted gettable and settable ValueSet items.</remarks>
        readonly ItemAccessSetupInfo<TAttribute>[] itemAccessSetupInfoArray = null;

        #endregion

        #region message emitter glue

        private Logging.IMesgEmitter issueEmitter = null, valueNoteEmitter = null;

        /// <summary>If the given <paramref name="emitterRef"/>is null then this method sets it to the Logging.NullEmitter.  Returns the original, or updated, <paramref name="emitterRef"/></summary>
        private Logging.IMesgEmitter FixupEmitterRef(ref Logging.IMesgEmitter emitterRef)
        {
            if (emitterRef == null)
                emitterRef = Logging.NullEmitter;
            return emitterRef;
        }

        #endregion

        #region INamedValueSetAdapter explicit implementation methods

        INamedValueSetAdapter INamedValueSetAdapter.Setup(params string[] baseNames)
        {
            return Setup(baseNames);
        }

        INamedValueSetAdapter INamedValueSetAdapter.Set(INamedValueSet nvs, bool merge)
        {
            return Set(nvs, merge: merge);
        }

        #endregion
    }

    #endregion

    #region DelegateItemSpec (variants)

    /// <summary>
    /// This class is used by client code to define the relationship between a named item and optional getter and/or setter delegates.
    /// <para/>This version is used with getter and/or setter delegates that are parameterized to produce/consume ValueContainer values.
    /// </summary>
    public class DelegateItemSpec : DelegateItemSpec<ValueContainer>
    {
        /// <summary>
        /// Constructor.  Requires name.  Accepts nameAdjust, getterDelegate, and setterDelegate.
        /// </summary>
        public DelegateItemSpec(string name, NameAdjust nameAdjust = NameAdjust.Prefix0, Func<ValueContainer> getterDelegate = null, Action<ValueContainer> setterDelegate = null, INamedValueSet metaData = null)
            : base(name, nameAdjust: nameAdjust, getterDelegate: getterDelegate, setterDelegate: setterDelegate, metaData: metaData)
        { }
    }

    /// <summary>
    /// This class is used by client code to define the relationship between a named item and optional getter and/or setter delegates.
    /// <para/>Supports all TValueTypes that are supported by ValueContainer
    /// </summary>
    public class DelegateItemSpec<TValueType>
    {
        /// <summary>
        /// Constructor.  Requires name.  Accepts nameAdjust, getterDelegate, and setterDelegate.
        /// </summary>
        public DelegateItemSpec(string name, NameAdjust nameAdjust = NameAdjust.Prefix0, Func<TValueType> getterDelegate = null, Action<TValueType> setterDelegate = null, INamedValueSet metaData = null)
        {
            Name = name;
            NameAdjust = nameAdjust;
            GetterDelegate = getterDelegate;
            SetterDelegate = setterDelegate;
            MetaData = metaData;
        }

        /// <summary>
        /// Internal copy constructor - client code is not expected to make copies of instances this object type.
        /// </summary>
        internal DelegateItemSpec(DelegateItemSpec<TValueType> other)
        {
            Name = other.Name;
            NameAdjust = other.NameAdjust;
            GetterDelegate = other.GetterDelegate;
            SetterDelegate = other.SetterDelegate;
            _metaData = other._metaData;
        }

        /// <summary>Gives the item's "Name" this is generally used during a Setup method and is combined with the associated NameAdjust, and any strings that are given to Setup to produce an full name used with Modular.Interconnect.Values and/or Modular.Config</summary>
        public string Name { get; private set; }

        /// <summary>Gives the NameAdjust to be used with this item.  Defaults to Prefix0 when not expicitly set in the constructor.  May be set directly or using a property initializer.</summary>
        public NameAdjust NameAdjust { get; set; }

        /// <summary>Optional delegate used to produce each new TValueType value for transfer.  Defaults to null when not explicitly set in the constructor.  May be set directly or using a property initializer.</summary>
        public Func<TValueType> GetterDelegate { get; set; }

        /// <summary>Optional delegate used to consume each newly transferred TValueType value.  Defaults to null when not explicitly set in the constructor.  May be set directly or using a property initializer.</summary>
        public Action<TValueType> SetterDelegate { get; set; }

        /// <summary>Returns true if the GetterDelegate is non-null</summary>
        public bool HasGetterDelegate { get { return (GetterDelegate != null); } }

        /// <summary>Returns true if the SetterDelegate is non-null</summary>
        public bool HasSetterDelegate { get { return (SetterDelegate != null); } }

        /// <summary>Allows the item to specify the MetaData to be used (where supported)</summary>
        public INamedValueSet MetaData { get { return _metaData.MapNullToEmpty(); } set { _metaData = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }
        private INamedValueSet _metaData = null;

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            string gsStr;
            if (HasGetterDelegate && HasSetterDelegate)
                gsStr = "Getter+Setter";
            else if (HasGetterDelegate && !HasSetterDelegate)
                gsStr = "Getter";
            else if (!HasGetterDelegate && HasSetterDelegate)
                gsStr = "Setter";
            else
                gsStr = "NeitherGetterNorSetter-Invalid";

            string mdStr = (MetaData.IsNullOrEmpty() ? "" : " md:{0}".CheckedFormat(MetaData.ToStringSML()));

            return "DIS Type:{0} Name:'{1}' NameAdj:{2} {3}{4}".CheckedFormat(typeof(TValueType), Name, NameAdjust, gsStr, mdStr);
        }
    }

    #endregion
}
