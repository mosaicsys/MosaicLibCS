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

namespace MosaicLib.Modular.Common
{
    using System;
    using System.Collections.Generic;
    using System.ServiceModel;
    using System.Runtime.Serialization;
    using MosaicLib.Utils;
    using System.Runtime.InteropServices;

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
    public struct ValueContainer
    {
        /// <summary>A quick encoding on the field/union field in which the value has been placed.</summary>
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
            /// <summary>SByte value in union</summary>
            [FieldOffset(0)]
            public System.SByte i8;
            /// <summary>SByte value in union</summary>
            [FieldOffset(0)]
            public System.Int16 i16;
            /// <summary>Int16 value in union</summary>
            [FieldOffset(0)]
            public System.Int32 i32;
            /// <summary>Int32 value in union</summary>
            [FieldOffset(0)]
            public System.Int64 i64;
            /// <summary>Int64 value in union</summary>
            [FieldOffset(0)]
            public System.Byte u8;
            /// <summary>Byte value in union</summary>
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

            /// <summary>
            /// Compare the contents of this instance to the rhs by comparing the contents of the largest fields that are in both.  For this purpose we are using u64.
            /// </summary>
            public bool IsEqualTo(Union rhs)
            {
                return (u64 == rhs.u64);
            }
        }

        /// <summary>Clears the container.  Identical to setting it to its own default.</summary>
        public void Clear()
        {
            cvt = default(ContainerStorageType);
            o = default(System.Object);
            u = default(Union);
        }

        /// <summary></summary>
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
            else
            {
                isNullable = false;     // this flag is not useful when used with object types
                decodedValueType = ContainerStorageType.Object;
            }
        }

        /// <summary>
        /// get/set property.
        /// getter returns the currently contained value casted as an System.Object
        /// setter extracts the type information from the given value and then attempts to decode
        /// and use the type specific value storage type, if it is one of the supported types.  
        /// If the extracted type is not one of the supported container types then the given value is simply stored as an System.Object.
        /// <para/>Use SetValue{System.Object}(value) if you want to force the container to store value as an object.
        /// <para/>Supports call chaining
        /// </summary>
        public System.Object ValueAsObject
        {
            get
            {
                switch (cvt)
                {
                    default:
                    case ContainerStorageType.Object: return o;
                    case ContainerStorageType.Boolean: return u.b;
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
                }
            }
            set
            {
                if (value != null)
                {
                    Type t = value.GetType();
                    ContainerStorageType valueCVT;
                    bool valueTypeIsNullable;

                    DecodeType(t, out valueCVT, out valueTypeIsNullable);

                    SetValue<System.Object>(value, valueCVT, valueTypeIsNullable);
                }
                else
                {
                    Clear();
                }
            }
        }

        /// <summary>
        /// Type extracting setter.  Extracts the type information from the given value and then attempts to decode
        /// and use the type specific value storage type, if it is one of the supported types.  If the extracted type
        /// is not one of the supported container types then the given value is simply stored as an System.Object
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
            Clear();

            try
            {
                bool forceToNull = (isNullable && value == null);

                if (!forceToNull)
                {
                    cvt = decodedValueType;
                    switch (decodedValueType)
                    {
                        default:
                        case ContainerStorageType.Object: o = value; cvt = ContainerStorageType.Object; break;
                        case ContainerStorageType.Boolean: u.b = (System.Boolean)System.Convert.ChangeType(value, typeof(System.Boolean)); break;
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
        /// Typed GetValue method.  
        /// Requires that caller has pre-decoded the TValueType to obtain the expected ContainerStorageType and a flag to indicate if the TValueType is a nullable type.
        /// If the contained value type matches the decodedValueType then this method simply transfers the value from the corresponding storage field to the value.
        /// Otherwise the method uses the System.Convert.ChangeType method to attempt to convert the contained value into the desired TValueType type.
        /// If this transfer or conversion is successful then this method and returns the transfered/converted value.  If this transfer/conversion is not
        /// successful then this method returns default(TValueType).  
        /// If rethrow is true and the method counters any excpetions then it will rethrow the exception.
        /// </summary>
        public TValueType GetValue<TValueType>(ContainerStorageType decodedValueType, bool isNullable, bool rethrow)
        {
            TValueType value;

            try
            {
                if (decodedValueType == cvt)
                {
                    switch (cvt)
                    {
                        default:
                        case ContainerStorageType.Object: value = (TValueType)o; break;
                        case ContainerStorageType.Boolean: value = (TValueType)((System.Object)u.b); break;
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
                    }
                }
                else if (!isNullable)
                {
                    value = (TValueType)System.Convert.ChangeType(ValueAsObject, typeof(TValueType));
                }
                else
                {
                    value = default(TValueType);
                    bool valueIsIntendedToBeNull = (cvt == ContainerStorageType.Object && o == null);
                    if (!valueIsIntendedToBeNull && rethrow)
                        throw new ValueContainerGetValueException("Unable to get {0} as type '{1}': No known conversion exists".CheckedFormat(this, typeof(TValueType)), null);
                }
            }
            catch (System.Exception ex)
            {
                value = default(TValueType);
                if (rethrow)
                    throw new ValueContainerGetValueException("Unable to get {0} as type '{1}': {2}".CheckedFormat(this, typeof(TValueType), ex.Message), null);
            }

            return value;
        }

        /// <summary>
        /// Property to attempt to interpret the contained value as a Nullable{double}.  This is especially useful for binding with WPF
        /// </summary>
        public double ? ValueAsDouble
        {
            get { return GetValue<double ?>(ContainerStorageType.Double, true, false); }
            set { SetValue<double ?>(value); }
        }

        /// <summary>
        /// Equality testing implementation method.  Uses ValueContainer in signature to remove need for casting (as with Equals).
        /// </summary>
        public bool IsEqualTo(ValueContainer rhs)
        {
            if (cvt != rhs.cvt)
                return false;

            if (cvt == ContainerStorageType.Object)
                return System.Object.Equals(o, rhs.o);
            else
                return u.IsEqualTo(rhs.u);
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

        /// <summary>Override ToString for logging and debugging.</summary>
        public override string ToString()
        {
            if (cvt == ContainerStorageType.Object)
                return Fcns.CheckedFormat("{0}:'{1}'", cvt, ValueAsObject);
            else
                return Fcns.CheckedFormat("{0}:{1}", cvt, ValueAsObject);
        }
    }

    /// <summary>Enumeration that is used with the ValueContainer struct.</summary>
    public enum ContainerStorageType : int
    {
        /// <summary>Use Object field -(the default value: 0)</summary>
        Object = 0,
        /// <summary>Use Union.Boolean field</summary>
        Boolean,
        /// <summary>Use Union.SByte field</summary>
        SByte,
        /// <summary>Use Union.Int16 field</summary>
        Int16,
        /// <summary>Use Union.Int32 field</summary>
        Int32,
        /// <summary>Use Union.Int64 field</summary>
        Int64,
        /// <summary>Use Union.Byte field</summary>
        Byte,
        /// <summary>Use Union.UInt16 field</summary>
        UInt16,
        /// <summary>Use Union.UInt32 field</summary>
        UInt32,
        /// <summary>Use Union.UInt64 field</summary>
        UInt64,
        /// <summary>Use Union.Single field</summary>
        Single,
        /// <summary>Use Union.Double field</summary>
        Double,
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

    #region Named Values

    /// <summary>
    /// This object defines a list of NamedValue.  
    /// This list allows use of a CollectionDataContractAttribute to allow the list element names to be explicitly specified
    /// </summary>
    [CollectionDataContract(ItemName = "nvi", Namespace = Constants.ModularNameSpace)]
    public class NamedValueList : List<NamedValue>
    {
        /// <summary>Default constructor</summary>
        public NamedValueList() { }

        /// <summary>
        /// Copy constructor from IList{NamedValues}.  
        /// Creates a new NamedValueList from the given list of NamedValues by cloning each of them.
        /// </summary>
        public NamedValueList(IList<NamedValue> rhs)
        {
            Capacity = rhs.Count;
            foreach (NamedValue rhsItem in rhs)
                Add(new NamedValue(rhsItem));
        }

        /// <summary>
        /// get/set index operator by name.  Gets the NamedValue for the requested name.  
        /// getter returns the node from the contained list or a new NamedValue for the given name if the requested one was not found.
        /// setter replaces the selected NamedValue item in the list or adds a new NamedValue object to the list.
        /// </summary>
        /// <param name="name">Gives the name for the desired NamedValue to get/replace</param>
        /// <returns>The selected NamedValue as described in the summary</returns>
        /// <exception cref="System.InvalidOperationException">thrown by setter if the given name is not equal to the Name property from the assigned NamedValue object</exception>
        public NamedValue this[string name]
        {
            get
            {
                BuildDictIfNeeded();
                int idx = -1;
                if (nameToIndexDict.TryGetValue(name ?? String.Empty, out idx))
                    return this[idx];
                else
                    return new NamedValue(name);
            }

            set
            {
                if (name != value.Name)
                    throw new System.InvalidOperationException("Index string must equal Name in given NamedValue object");

                BuildDictIfNeeded();
                int idx = -1;
                if (nameToIndexDict.TryGetValue(name ?? String.Empty, out idx))
                    this[idx] = value;
                else
                {
                    idx = Count;
                    Add(value);
                    nameToIndexDict[name] = idx;
                }
            }
        }

        /// <summary>Contains the dictionary used to convert from names to list indexes.  Only built if [name] index operator is used.</summary>
        protected Dictionary<string, int> nameToIndexDict = null;

        /// <summary>Method used by client to reset the nameToIndex Dictionary if contents of NamedValues list is changed outside of use of [name] index operator.</summary>
        public void InvalidateNameToIndexDictionary() 
        { 
            nameToIndexDict = null; 
        }

        /// <summary>Internal method used to build the nameToIndexDictionary when needed.</summary>
        protected void BuildDictIfNeeded()
        {
            if (nameToIndexDict == null)
            {
                nameToIndexDict = new Dictionary<string, int>();
                for (int idx = 0; idx < Count; idx++)
                {
                    NamedValue nvItem = this[idx];
                    nameToIndexDict[nvItem.Name] = idx;
                }
            }
        }
    }

    /// <summary>
    /// This object defines a single named value.  A named value is a pairing of a name and a string and/or a binary value.
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public class NamedValue
    {
        /// <summary>Constructor - builds NamedValue containing null string and binary data</summary>
        public NamedValue(string name) : this(name, null, null) { }
        /// <summary>Constructor - builds NamedValue containing given string and null binary data</summary>
        public NamedValue(string name, string str) : this(name, str, null) { }
        /// <summary>Constructor - builds NamedValue containing null string and given binary data.  Clones the binary data.</summary>
        public NamedValue(string name, byte[] data) : this(name, null, data) { }
        /// <summary>Constructor - builds NamedValue containing given string and given binary data.  Clones the binary data if non-null</summary>
        public NamedValue(string name, string str, byte[] data) { Name = name; Str = str; Data = (data != null ? (byte[])data.Clone() : null); }
        /// <summary>Copy constructor.  builds clone of the given rhs containing copy of Name and Str properties and clone of binary data if it is non-null.</summary>
        public NamedValue(NamedValue rhs) : this(rhs.Name, rhs.Str, rhs.Data) { }

        /// <summary>This property gives the caller access to the name of the named value</summary>
        [DataMember(Order = 1, IsRequired = true)]
        public string Name { get; set; }

        /// <summary>This property if non-null if the NV has been given a string value</summary>
        [DataMember(Order = 2, IsRequired = false, EmitDefaultValue = false)]
        public string Str { get; set; }

        /// <summary>This property is non-null if the NV has been given a binary value (possibly from a ValueSet or other E005DataSource)</summary>
        [DataMember(Order = 3, IsRequired = false, EmitDefaultValue = false)]
        public byte[] Data { get; set; }

        /// <summary>Returns true if the object has a non-null Str property value</summary>
        public bool HasStrValue { get { return (Str != null); } }
        /// <summary>Returns true if the object has a non-null Data property value</summary>
        public bool HasDataValue { get { return (Data != null); } }
        /// <summary>Returns true if the object has a non-null Str property or a non-null Data property value</summary>
        public bool HasValue { get { return (HasStrValue || HasDataValue); } }
    }

    #endregion
}
