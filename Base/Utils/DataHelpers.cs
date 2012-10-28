//-------------------------------------------------------------------
/*! @file DataHelpers.cs
 * This file contains a series of utility classes that are used to encapuslate
 * track, record and/or manage data objects.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

namespace MosaicLib.Utils
{
	//-------------------------------------------------
	using System;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;

	//-------------------------------------------------
	#region Data Packing, Unpacking, Byte Order manipulations

	/// <summary>
	/// This static partial class provides static helper methods that can be used to pack and unpack values.
	/// Byte array versions always use network order (msb first).
	/// </summary>
	public static partial class Data
    {
        #region Packing

        public static UInt16 Pack(Byte msb, Byte lsb) { unchecked { return (UInt16) ((((UInt32) msb) << 8) | ((UInt32) lsb)); } }
        public static UInt32 Pack(Byte umsb, Byte ulsb, Byte lmsb, Byte llsb) { unchecked { return ((((UInt32)umsb) << 24) | (((UInt32)ulsb) << 16) | (((UInt32)lmsb) << 8) | ((UInt32)llsb)); } }
        public static UInt32 Pack(Byte ulsb, Byte lmsb, Byte llsb) { unchecked { return ((((UInt32)ulsb) << 16) | (((UInt32)lmsb) << 8) | ((UInt32)llsb)); } }
        public static UInt32 Pack(UInt16 msw, UInt16 lsw) { unchecked { return ((((UInt32)msw) << 16) | ((UInt32)lsw)); } }

		public static bool Pack(Byte [] byteArray, int baseIdx, out UInt16 value) 
		{ 
			value = 0;

            if (byteArray == null || (baseIdx < 0) || ((baseIdx + 1) >= byteArray.Length))
				return false;

            value = Pack(byteArray [baseIdx], byteArray [baseIdx + 1]);
			return true;
		}

		public static bool Pack(Byte [] byteArray, int baseIdx, out UInt32 value) 
		{ 
			value = 0;

            if (byteArray == null || (baseIdx < 0) || ((baseIdx + 3) >= byteArray.Length))
				return false;

            value = Pack(byteArray [baseIdx], byteArray [baseIdx + 1], byteArray [baseIdx + 2], byteArray [baseIdx + 3]);
			return true;
        }

        public static bool Pack(Byte[] byteArray, int baseIdx, int numBytes, out UInt32 value)
        {
            value = 0;

            if (byteArray == null || (baseIdx < 0) || ((baseIdx + numBytes) > byteArray.Length))
                return false;

            switch (numBytes)
            {
                case 2: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1]); return true;
                case 3: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2]); return true;
                case 4: value = Pack(byteArray[baseIdx], byteArray[baseIdx + 1], byteArray[baseIdx + 2], byteArray[baseIdx + 3]); return true;
                default: return false;
            }
        }

        #endregion

        #region Unpacking

        public static void Unpack(UInt16 w, out Byte msb, out Byte lsb)
		{
			unchecked
			{
				msb = (Byte) (w >> 8);
				lsb = (Byte) (w >> 0);
			}
		}

		public static void Unpack(UInt32 l, out UInt16 msw, out UInt16 lsw)
		{
			unchecked
			{
				msw = (UInt16) (l >> 16);
				lsw = (UInt16) (l >> 0);
			}
		}

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

		public static bool Unpack(UInt16 w, byte [] byteArray, int baseIdx)
		{
			if (byteArray == null || baseIdx < 0 || ((baseIdx + 2) > byteArray.Length))
				return false;
			Unpack(w, out byteArray [baseIdx], out byteArray [baseIdx + 1]);
			return true;
		}

		public static bool Unpack(UInt32 l, byte [] byteArray, int baseIdx)
		{
			if (byteArray == null || baseIdx < 0 || ((baseIdx + 4) > byteArray.Length))
				return false;
			Unpack(l, out byteArray [baseIdx], out byteArray [baseIdx + 1], out byteArray [baseIdx + 2], out byteArray [baseIdx + 3]);
			return true;
        }

        public static bool Unpack(UInt32 l, byte[] byteArray, int baseIdx, int numBytes)
        {
            if (byteArray == null || baseIdx < 0 || ((baseIdx + numBytes) > byteArray.Length))
                return false;

            switch (numBytes)
            {
                case 2: Unpack((UInt16) (l & 0xffff), out byteArray[baseIdx], out byteArray[baseIdx + 1]); return true;
                case 3: Unpack(l, out byteArray[baseIdx], out byteArray[baseIdx + 1], out byteArray[baseIdx + 2]); return true;
                case 4: Unpack(l, out byteArray[baseIdx], out byteArray[baseIdx + 1], out byteArray[baseIdx + 2], out byteArray[baseIdx + 3]); return true;
                default: return false;
            }
        }

        #endregion

        #region Byte order manipulation

        public enum ByteOrder
        {
            LittleEndian, BigEndian
        }

        public static ByteOrder MachineOrder { get { return ((BitConverter.IsLittleEndian) ? ByteOrder.LittleEndian : ByteOrder.BigEndian); } }
        public static bool IsMachineLittleEndian { get { return (MachineOrder == ByteOrder.LittleEndian); } }
        public static bool IsMachineBigEndian { get { return (MachineOrder == ByteOrder.BigEndian); } }

        public static bool ChangeByteOrder(byte[] byteArray, int baseIdx, int itemSize, ByteOrder fromByteOrder) 
        { 
            return ChangeByteOrder(byteArray, baseIdx, itemSize, fromByteOrder, MachineOrder); 
        }

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

        public static bool ChangeByteOrder(byte[] byteArray, int baseIdx, int numItems, int itemSize, ByteOrder fromByteOrder) 
        { 
            return ChangeByteOrder(byteArray, baseIdx, itemSize, numItems, fromByteOrder, MachineOrder); 
        }

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

        public static void Swap<TypeT>(ref TypeT left, ref TypeT right)
        {
            TypeT temp = left;
            left = right;
            right = temp;
        }

        #endregion
    }

	#endregion

	//-------------------------------------------------
	#region Atomic Value types

    public interface IAtomicValue<ValueType> where ValueType : struct
    {
		/// <summary>Provide accessor to underlying value as a volatile value (without locking)</summary>
        ValueType VolatileValue { get; set; }

        /// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
        ValueType Value { get; set; }

        /// <summary>Performs Iterlocked.Increment</summary>
        ValueType Increment();
        /// <summary>Performs Iterlocked.Increment once or twice to produce a non-zero value</summary>
        ValueType IncrementSkipZero();
        /// <summary>Performs Iterlocked.Decrement</summary>
        ValueType Decrement();
        /// <summary>Performs Iterlocked.Add</summary>
        ValueType Add(ValueType value);
        /// <summary>Performs Iterlocked.Exchange</summary>
        ValueType Exchange(ValueType value);
        /// <summary>Performs Iterlocked.CompareExchange</summary>
        ValueType CompareExchange(ValueType value, ValueType comparand);
    }

    // surpress "warning CS0420: 'xxxx': a reference to a volatile field will not be treated as volatile"
    //	The following structs are designed to support use of atomic, interlocked operations on volatile values
    #pragma warning disable 0420

	/// <summary>
	/// This struct provides the standard System.Threading.Interlocked operations wrapped around a volatile System.Int32 value.  This is done to allow us to surpress the warnings that are generated when passing a volatile by reference
	/// </summary>
    public struct AtomicInt32 : IAtomicValue<System.Int32>
	{
		public AtomicInt32(System.Int32 value) { this.value = value; }

		private volatile System.Int32 value;

		/// <summary>Provide accessor to underlying value as a volatile value</summary>
		public System.Int32 VolatileValue { get { return this.value; } set { this.value = value; }}

		/// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
		public System.Int32 Value
		{
			get { return CompareExchange(0, 0); }
			set { Exchange(value); }
		}

		public System.Int32 Increment() { return System.Threading.Interlocked.Increment(ref this.value); }
		public System.Int32 IncrementSkipZero() { Int32 value = Increment(); while (value == 0) value = Increment(); return value; }
		public System.Int32 Decrement() { return System.Threading.Interlocked.Decrement(ref this.value); }
		public System.Int32 Add(System.Int32 value) { return System.Threading.Interlocked.Add(ref this.value, value); }
		public System.Int32 Exchange(System.Int32 value) { return System.Threading.Interlocked.Exchange(ref this.value, value); }
		public System.Int32 CompareExchange(System.Int32 value, System.Int32 comparand) { return System.Threading.Interlocked.CompareExchange(ref this.value, value, comparand); }
	}

	/// <summary>
	/// This struct provides the same functionality as AtomicInt32 but casted to act as an UInt32
	/// </summary>
    public struct AtomicUInt32 : IAtomicValue<System.UInt32>
	{
		private AtomicInt32 ai32;

		public AtomicUInt32(System.UInt32 value) { ai32 = new AtomicInt32(unchecked((Int32) value)); }

		/// <summary>Provide accessor to underlying value as a volatile value</summary>
		public System.UInt32 VolatileValue { get { return unchecked((UInt32) ai32.VolatileValue); } set { ai32.VolatileValue = unchecked((Int32) value); } }

		/// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
		public System.UInt32 Value { get { return unchecked((UInt32) ai32.Value); } set { ai32.Value = unchecked((Int32) value); } }

		public System.UInt32 Increment() { return unchecked((UInt32) ai32.Increment()); }
		public System.UInt32 IncrementSkipZero() { return unchecked((UInt32) ai32.IncrementSkipZero()); }
		public System.UInt32 Decrement() { return unchecked((UInt32) ai32.Decrement()); }
		public System.UInt32 Add(System.UInt32 value) { return unchecked((UInt32) ai32.Add(unchecked((Int32) value))); }
		public System.UInt32 Exchange(System.UInt32 value) { return unchecked((UInt32) ai32.Exchange(unchecked((Int32) value))); }
		public System.UInt32 CompareExchange(System.UInt32 value, System.UInt32 comparand) { return unchecked((UInt32) ai32.CompareExchange(unchecked((Int32) value), unchecked((Int32) comparand))); }
	}

    /// <summary>
    /// This struct provides the standard System.Threading.Interlocked operations wrapped around a volatile System.Int64 value.  This is done to allow us to surpress the warnings that are generated when passing a volatile by reference
    /// </summary>
    public struct AtomicInt64 : IAtomicValue<System.Int64>
    {
        public AtomicInt64(System.Int64 value) { this.value = value; }

        private System.Int64 value;     // cannot be volatile since bus access to this object is not allways atomic

        /// <summary>Provide accessor to underlying value as a volatile value</summary>
        public System.Int64 VolatileValue { get { return this.value; } set { this.value = value; } }

        /// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
        public System.Int64 Value
        {
            get { return CompareExchange(0, 0); }
            set { Exchange(value); }
        }

        public System.Int64 Increment() { return System.Threading.Interlocked.Increment(ref this.value); }
        public System.Int64 IncrementSkipZero() { Int64 value = Increment(); while (value == 0) value = Increment(); return value; }
        public System.Int64 Decrement() { return System.Threading.Interlocked.Decrement(ref this.value); }
        public System.Int64 Add(System.Int64 value) { return System.Threading.Interlocked.Add(ref this.value, value); }
        public System.Int64 Exchange(System.Int64 value) { return System.Threading.Interlocked.Exchange(ref this.value, value); }
        public System.Int64 CompareExchange(System.Int64 value, System.Int64 comparand) { return System.Threading.Interlocked.CompareExchange(ref this.value, value, comparand); }
    }

    /// <summary>
    /// This struct provides the same functionality as AtomicInt64 but casted to act as an UInt64
    /// </summary>
    public struct AtomicUInt64 : IAtomicValue<System.UInt64>
    {
        private AtomicInt64 ai64;

        public AtomicUInt64(System.UInt64 value) { ai64 = new AtomicInt64(unchecked((Int64)value)); }

        /// <summary>Provide accessor to underlying value as a volatile value</summary>
        public System.UInt64 VolatileValue { get { return unchecked((UInt64)ai64.VolatileValue); } set { ai64.VolatileValue = unchecked((Int64)value); } }

        /// <summary>Provide R/W accessor property for the underlying value using atomic access/swap operations</summary>
        public System.UInt64 Value { get { return unchecked((UInt64)ai64.Value); } set { ai64.Value = unchecked((Int64)value); } }

        public System.UInt64 Increment() { return unchecked((UInt64)ai64.Increment()); }
        public System.UInt64 IncrementSkipZero() { return unchecked((UInt64)ai64.IncrementSkipZero()); }
        public System.UInt64 Decrement() { return unchecked((UInt64)ai64.Decrement()); }
        public System.UInt64 Add(System.UInt64 value) { return unchecked((UInt64)ai64.Add(unchecked((Int64)value))); }
        public System.UInt64 Exchange(System.UInt64 value) { return unchecked((UInt64)ai64.Exchange(unchecked((Int64)value))); }
        public System.UInt64 CompareExchange(System.UInt64 value, System.UInt64 comparand) { return unchecked((UInt64)ai64.CompareExchange(unchecked((Int64)value), unchecked((Int64)comparand))); }
    }

#pragma warning restore 0420

	#endregion

    //-------------------------------------------------
	/// <remarks>
	/// The following GuardedObject and SequenceNumber related defintions form an essential component that is required to support the efficient 
	/// poll version of event reaction and handling.  
	///
	/// Sequence numbers by themselves are provided in simple form (with and without time stamps on last increment) and in an interlocked form.
	/// The simple form is not thread renterant and any user of the simple form must implement appropriate thread locking to prevent concurrent use
	/// of the interface methods on two or more threads.  The interlocked form supports non-blocking and non-locking use in such multithread
	/// environments.
	/// 
	/// A Sequence number observer pattern is provided that is constructed from a ISequenceNumberValue provider and allows the caller to quickly
	/// test if the provider has been Incremented recently and to Update the local copy when desired so as to reset such a change indicator.
	/// 
	/// The basic Sequence number classes are also agregated into Sequenced Objects.  There are a number of these implementation classes that 
	/// all focus on implementing on of the ISequencedRefObject or ISequencedValueObject interfaces.  These implementations allow the owner to
	/// provide a generic interface from which clients can determine if a published value has been recently written and to obtain such a value 
	/// in a thread safe manner.  The pattern of implementation objects include a version that uses Interlocked operations to update the value and
	/// to increment the sequence number along with a larger set of implementations that are lock based.  
	/// </remarks>

	//-------------------------------------------------
	#region IObjectSource, IVolatileObjectSource interfaces

	#region Object source interface

	/// <summary>
	/// Inteface that provides a templatized Object property for obtaining an Object (or copy thereof) from some object container or source.  
	/// This interface is most frequently provided by an implementation object that provides thread safe access control (guarded), sequencing 
	/// and/or automatic notification.
	/// </summary>
	/// <typeparam name="ObjectType">The type of the Object property.</typeparam>
	public interface IObjectSource<ObjectType>
	{
        /// <summary>Property caller access to contained object.  May invoke locking so as to keep sequence number synchronized with each object.</summary>
		ObjectType Object { get; }
	}

	/// <summary>
	/// Defines the provided by objects that give read access to some internal volatile ref object of the specified type.
	/// </summary>
	/// <typeparam name="ObjectType">The type of the VolatileObject property.</typeparam>
	public interface IVolatileObjectSource<ObjectType> where ObjectType : class
	{
        /// <summary>Provides caller with volatile access to the contained object.  Will allow caller to snapshot a specific object with minimal locking required to produce a valid object.</summary>
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

	/// <summary> This interface defines the methods that are available for all sequence number objects and is used to generate the next value in the seaquence </summary>
	/// <typeparam name="SeqNumberType">Defines the numeric type used by this sequence number's counter.</typeparam>
	public interface ISequenceNumberGenerator<SeqNumberType>
	{
        /// <summary>Advances the sequence number to the next value and returns it</summary>
        /// <remarks>SkipZero type SequenceNumberGenerators implement this by performing the increment and then doing it again if the result of the first was zero.</remarks>
		SeqNumberType Increment();
	}

	#endregion

	#region Sequenced object interfaces

	/// <summary>Combines the IObjectSource and ISequenceNumberValue interfaces to represent a source of sequenced objects.</summary>
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
	}

	/// <summary>This interface gives the client access to the Object from the source that was obtained during the last Update call.</summary>
	public interface ISequencedObjectSourceObserver<ObjectType> : IObjectSource<ObjectType>, ISequencedSourceObserver { }

	/// <summary>This inteface combines the funtionality of a ISequencedSourceObserver and an ISequenceNumberValue</summary>
	/// <typeparam name="SeqNumberType">Defines the type of the sequence number</typeparam>
	public interface ISequenceNumberObserver<SeqNumberType> : ISequenceNumberValue<SeqNumberType>, ISequencedSourceObserver { }

	/// <summary>This interface combines an ISequencedObjectSourceObserver and a ISequenceNumberValue</summary>
	/// <typeparam name="ObjectType">Defines the type of the observed object</typeparam>
	/// <typeparam name="SeqNumberType">Defines the type of the sequence number</typeparam>
	public interface ISequencedObjectSourceObserver<ObjectType, SeqNumberType> : ISequencedObjectSourceObserver<ObjectType>, ISequenceNumberValue<SeqNumberType> { }

	#endregion

	#region Sequenced Ref and Value object observer interfaces

	/// <summary>Inteface defines a ISequencedObjectSourceObserver for use with ref type objects</summary>
	/// <typeparam name="ObjectType">Defines the type of the observed object.  Must be a ref type.</typeparam>
	/// <typeparam name="SeqNumberType">Defines the type of the sequence number.</typeparam>
	public interface ISequencedRefObjectSourceObserver<ObjectType, SeqNumberType> : ISequencedObjectSourceObserver<ObjectType, SeqNumberType>
		where ObjectType : class
	{ }

	/// <summary>Inteface defines a ISequencedObjectSourceObserver for use with value type objects</summary>
	/// <typeparam name="ObjectType">Defines the type of the observed object.  Must be a value type.</typeparam>
	/// <typeparam name="SeqNumberType">Defines the type of the sequence number.</typeparam>
	public interface ISequencedValueObjectSourceObserver<ObjectType, SeqNumberType> : ISequencedObjectSourceObserver<ObjectType, SeqNumberType>
		where ObjectType : struct
	{ }

	#endregion

	#endregion

	//-------------------------------------------------
	#region Guarded object implementation

	/// <summary>Implements the IGuardedObjectSource.  Uses a volatile handle to implement object access safety/synchronization.</summary>
	/// <typeparam name="ObjectType">The type of the guarded object.  Must be a ref type.</typeparam>
	public class VolatileRefObject<ObjectType> : IObjectSource<ObjectType>, IVolatileObjectSource<ObjectType> where ObjectType : class
	{
		public VolatileRefObject() { }
		public VolatileRefObject(ObjectType initialValue) { Object = initialValue; }

        /// <summary>Property caller access to contained object.  synonym for VolatileObject.</summary>
        public virtual ObjectType Object { get { return volatileObjHandle; } set { volatileObjHandle = value; } }
        /// <summary>Provides caller with volatile access to the contained object.  Will allow caller to snapshot a specific object with minimal locking required to produce a valid object.</summary>
        public virtual ObjectType VolatileObject { get { return volatileObjHandle; } }

		protected volatile ObjectType volatileObjHandle = null;		// reference objects can be atomically updated
	}

	/// <summary>Implements the IGuardedObjectSource.  Uses mutex to implement object access synchronization.</summary>
	/// <typeparam name="ObjectType">The type of the guarded object.  Must be a ref type.</typeparam>
	public class GuardedRefObject<ObjectType> : IObjectSource<ObjectType>, IVolatileObjectSource<ObjectType> where ObjectType : class
	{
		public GuardedRefObject() { }
		public GuardedRefObject(ObjectType initialValue) { Object = initialValue; }

        /// <summary>Property caller access to contained object.  Uses locking to control access to internal object.</summary>
        public virtual ObjectType Object { get { lock (mutex) { return volatileObjHandle; } } set { lock (mutex) { volatileObjHandle = value; } } }
        /// <summary>Provides caller with volatile access to the contained object.  Will allow caller to snapshot a specific object with minimal locking required to produce a valid object.</summary>
        public virtual ObjectType VolatileObject { get { return volatileObjHandle; } }

		protected object mutex = new object();
		protected volatile ObjectType volatileObjHandle = null;		// reference objects can be atomically updated
	}

	/// <summary>Implements the IGuardedObjectSource.  Uses mutex to implement object access synchronization.</summary>
	/// <typeparam name="ObjectType">The type of the guarded object.  Must be a value type.</typeparam>
	public class GuardedValueObject<ObjectType> : IObjectSource<ObjectType> where ObjectType : struct
	{
		public GuardedValueObject() { }
		public GuardedValueObject(ObjectType initialValue) { Object = initialValue; }

        /// <summary>Property caller access to contained object.  Uses locking to control access to internal (value) object.</summary>
        public virtual ObjectType Object { get { lock (mutex) { return valueObjStorage; } } set { lock (mutex) { valueObjStorage = value; } } }

		protected object mutex = new object();
		protected ObjectType valueObjStorage;		// value objects are stored internally.  Accept default value
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

		public virtual bool SkipZero { get { return skipZero; } set { skipZero = value; } }

		public virtual bool HasBeenSet { get { return hasSequenceNumberBeenSet; } }
        public virtual ValueType SequenceNumber { get { return sequenceNumberGen.Value; } set { sequenceNumberGen.Value = value; InnerSequenceNumberHasBeenSet(); } }
        public virtual ValueType VolatileSequenceNumber { get { return sequenceNumberGen.VolatileValue; } }
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

		protected virtual void InnerSequenceNumberHasBeenSet() { hasSequenceNumberBeenSet = true;  }

		protected IAtomicValue<ValueType> sequenceNumberGen = null;
		private volatile bool hasSequenceNumberBeenSet = false;
		private bool skipZero = false;
    }

    #region Int32 SequenceNumber implementations

    /// <summary> 
	/// This class provides an implementation of the ISequenceNumber for the System.Int32 data type.  
	/// By default this verison skips the zero value.
    /// This version only support reenterant Increment if SkipZero is explicitly constructed or set to be false.
	/// </summary>
    public class SequenceNumberInt : SequenceNumberBase<System.Int32>
	{
		/// <summary>Constructor: initial value is zero, has not been set, skips zero on increment</summary>
		public SequenceNumberInt() : base(0, true, false) {}
		/// <summary>Constructor: initialValue as given, has been set if initial value is non-zero, skips zero on increment</summary>
        public SequenceNumberInt(System.Int32 initialValue) : base(initialValue, true, true) { }
		/// <summary>Constructor: initialValue as given, has been set if initial value is non-zero, skips zero on increment only if skipZero is true </summary>
        public SequenceNumberInt(System.Int32 initialValue, bool skipZero) : base(initialValue, skipZero, true) { }
	}

	/// <summary> 
	/// This class provides an implementation of the ISequenceNumber for the System.int data type and uses Interlocked.Increment.  
	/// This version supports renterant use of Increment.  It does not support use of SkipZero.  
	/// This class can also be used as a INotifyable target.
	/// </summary>
    public class InterlockedSequenceNumberInt : SequenceNumberBase<System.Int32>, INotifyable
	{
		public InterlockedSequenceNumberInt() : base(0, false, false) {}
		public InterlockedSequenceNumberInt(int initialValue) : base(initialValue, false, true) { }

		private new bool SkipZero { get { return base.SkipZero; } set { Asserts.ThrowIfConditionIsNotTrue(value == false, "InterlockedSequenceNumberInt.SkipZero must be false"); base.SkipZero = false; } }

		public virtual void Notify() { InnerIncrementNumber(); }
    }

    #endregion

    #region Uint64 SequenceNumber implementation

    /// <summary> 
    /// This class provides an implementation of the ISequenceNumber for the System.UInt64 data type.  
    /// By default skipZero is false.
    /// This version only support reenterant Increment if SkipZero is explicitly constructed or set to be false.
    /// </summary>
    public class SequenceNumberUInt64 : SequenceNumberBase<System.UInt64>
    {
        /// <summary>Constructor: initial value is zero, has not been set, skips zero on increment</summary>
        public SequenceNumberUInt64() : base(0, false, false) { }
        /// <summary>Constructor: initialValue as given, has been set if initial value is non-zero, skips zero on increment</summary>
        public SequenceNumberUInt64(System.UInt64 initialValue) : base(initialValue, false, true) { }
        /// <summary>Constructor: initialValue as given, has been set if initial value is non-zero, skips zero on increment only if skipZero is true </summary>
        public SequenceNumberUInt64(System.UInt64 initialValue, bool skipZero) : base(initialValue, skipZero, true) { }
    }

    public class InterlockedSequenceNumberUInt64 : SequenceNumberBase<System.UInt64>, INotifyable
    {
        public InterlockedSequenceNumberUInt64() : base(0, false, false) { }
        public InterlockedSequenceNumberUInt64(System.UInt64 initialValue) : base(initialValue, false, true) { }

        private new bool SkipZero { get { return base.SkipZero; } set { Asserts.ThrowIfConditionIsNotTrue(value == false, "InterlockedSequenceNumberUInt64.SkipZero must be false"); base.SkipZero = false; } }

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
		public SequenceNumberObserver(ISequenceNumberValue<SeqNumberType> sequenceNumberSource) 
		{ 
			this.sequenceNumberSource = sequenceNumberSource;
			copyOfLastValue = new SeqNumberType();
			hasBeenUpdated = false;
			Update(); 
		}

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

		#endregion

		#region ISequenceNumberValue<SeqNumberType> Members

		public bool HasBeenSet { get { return hasBeenUpdated; } }
		public SeqNumberType SequenceNumber { get { return copyOfLastValue; } }
		public SeqNumberType VolatileSequenceNumber { get { return copyOfLastValue; } }

		#endregion
	}

	#endregion

	//-------------------------------------------------
	#region Sequenced Ref and Value object implementations

	/// <summary>A variation of a GuardedRefObject that can be used as an ISequencedRefObjectSource</summary>
	/// <typeparam name="ObjectType">Gives the type of the guarded object.  Must be a ref type.</typeparam>
	public class GuardedSequencedRefObject<ObjectType> : GuardedRefObject<ObjectType>, ISequencedObjectSource<ObjectType, int> where ObjectType : class
	{
		public GuardedSequencedRefObject() { }
		public GuardedSequencedRefObject(ObjectType initialValue) { Object = initialValue; }

		public override ObjectType Object { set { lock (mutex) { volatileObjHandle = value; seqNum.Increment(); } } }

		public virtual bool HasBeenSet	{ get { return seqNum.HasBeenSet; } }		// lock not needed for access to set once volatile boolean
		public virtual int SequenceNumber { get { lock (mutex) { return seqNum.VolatileSequenceNumber; } } }
		public virtual int VolatileSequenceNumber { get { return seqNum.VolatileSequenceNumber; } }
		public virtual int Increment() { lock (mutex) { return seqNum.Increment(); } }

		protected SequenceNumberInt seqNum = new SequenceNumberInt();
	}

	/// <summary>A variation of a VolatileRefObject that can be used as an ISequencedRefObjectSource</summary>
	/// <typeparam name="ObjectType">Gives the type of the access controlled object.  Must be a ref type.</typeparam>
	public class InterlockedSequencedRefObject<ObjectType> : VolatileRefObject<ObjectType>, ISequencedObjectSource<ObjectType, int> where ObjectType : class
	{
		public InterlockedSequencedRefObject() {}
		public InterlockedSequencedRefObject(ObjectType initialValue) { Object = initialValue; }

		public override ObjectType Object { set { volatileObjHandle = value; seqNum.Increment(); } }
		public virtual bool HasBeenSet { get { return seqNum.HasBeenSet; } }		// lock not needed for access to set once volatile boolean
		public virtual int SequenceNumber { get { return seqNum.SequenceNumber; } }
		public virtual int VolatileSequenceNumber { get { return seqNum.VolatileSequenceNumber; } }
		public virtual int Increment() { return seqNum.Increment(); }

		private InterlockedSequenceNumberInt seqNum  = new InterlockedSequenceNumberInt();
	}

	/// <summary>A variation of a GuardedValueObject that can be used as an ISequencedValueObjectSource</summary>
	/// <typeparam name="ObjectType">Gives the type of the guarded object.  Must be a value type.</typeparam>
	public class GuardedSequencedValueObject<ObjectType> : GuardedValueObject<ObjectType>, ISequencedObjectSource<ObjectType, int> where ObjectType : struct
	{
		public GuardedSequencedValueObject() { }
		public GuardedSequencedValueObject(ObjectType initialValue) { Object = initialValue; }

		public override ObjectType Object { set { lock (mutex) { valueObjStorage = value; seqNum.Increment(); } } }

		public virtual bool HasBeenSet { get { return seqNum.HasBeenSet; } }		// lock not needed for access to set once volatile boolean
		public virtual int SequenceNumber { get { lock (mutex) { return seqNum.VolatileSequenceNumber; } } }
		public virtual int VolatileSequenceNumber { get { return seqNum.VolatileSequenceNumber; } }
		public virtual int Increment() { lock (mutex) { return seqNum.Increment(); } }

		protected SequenceNumberInt seqNum = new SequenceNumberInt();
	}

	#endregion

	//-------------------------------------------------
	#region Sequenced Ref and Value object Observer implementation classes (so that they can be used as a base class)

	/// <summary>Provides an implementation of the ISequencedRefObjectSourceObserver</summary>
	/// <typeparam name="ObjectType">Gives the type of the observed object.  Must be a ref type.</typeparam>
	/// <typeparam name="SeqNumberType">Gives the type of the sequence number.</typeparam>
	public class SequencedRefObjectSourceObserver<ObjectType, SeqNumberType> : ISequencedRefObjectSourceObserver<ObjectType, SeqNumberType>
		where ObjectType : class
		where SeqNumberType : new()
	{
		private ISequencedObjectSource<ObjectType, SeqNumberType> objSource;
		private SequenceNumberObserver<SeqNumberType> seqNumObserver;
		private ObjectType localObjCopy = null;

		public SequencedRefObjectSourceObserver(ISequencedObjectSource<ObjectType, SeqNumberType> objSource)
		{
			this.objSource = objSource;
			seqNumObserver = new SequenceNumberObserver<SeqNumberType>(objSource);
			IsUpdateNeeded = true;
			Update();
		}

        public SequencedRefObjectSourceObserver(SequencedRefObjectSourceObserver<ObjectType, SeqNumberType> rhs)
        {
            objSource = rhs.objSource;
            seqNumObserver = new SequenceNumberObserver<SeqNumberType>(rhs.seqNumObserver);
            localObjCopy = rhs.localObjCopy;
        }

		#region ISequencedRefObjectSourceObserver<ObjectType, SeqNumberType> Members

		public ObjectType Object { get { return localObjCopy; } }

		public bool IsUpdateNeeded { get { return seqNumObserver.IsUpdateNeeded; } set { seqNumObserver.IsUpdateNeeded = value; } }
		public bool Update() { if (seqNumObserver.Update()) { localObjCopy = objSource.Object; return true; } else return false; }
		public bool HasBeenSet { get { return seqNumObserver.HasBeenSet; } }
		public SeqNumberType SequenceNumber { get { return seqNumObserver.SequenceNumber; } }
		public SeqNumberType VolatileSequenceNumber { get { return seqNumObserver.VolatileSequenceNumber; } }

		#endregion
	}

	/// <summary>Provides an implementation of the ISequencedValueObjectSourceObserver</summary>
	/// <typeparam name="ObjectType">Gives the type of the observed object.  Must be a value type.</typeparam>
	/// <typeparam name="SeqNumberType">Gives the type of the sequence number.</typeparam>
	public class SequencedValueObjectSourceObserver<ObjectType, SeqNumberType> : ISequencedValueObjectSourceObserver<ObjectType, SeqNumberType>
		where ObjectType : struct
		where SeqNumberType : new()
	{
		private ISequencedObjectSource<ObjectType, SeqNumberType> objSource;
		private SequenceNumberObserver<SeqNumberType> seqNumObserver;
		private ObjectType localObjCopy;

		public SequencedValueObjectSourceObserver(ISequencedObjectSource<ObjectType, SeqNumberType> objSource)
		{
			this.objSource = objSource;
			seqNumObserver = new SequenceNumberObserver<SeqNumberType>(objSource);
			IsUpdateNeeded = true;
			Update();
		}

        public SequencedValueObjectSourceObserver(SequencedValueObjectSourceObserver<ObjectType, SeqNumberType> rhs)
        {
            objSource = rhs.objSource;
            seqNumObserver = new SequenceNumberObserver<SeqNumberType>(rhs.seqNumObserver);
            localObjCopy = rhs.localObjCopy;
        }

		#region ISequencedValueObjectSourceObserver<ObjectType, SeqNumberType> Members

		public ObjectType Object { get { return localObjCopy; } }

		public bool IsUpdateNeeded { get { return seqNumObserver.IsUpdateNeeded; } set { seqNumObserver.IsUpdateNeeded = value; } }
		public bool Update() { if (seqNumObserver.Update()) { localObjCopy = objSource.Object; return true; } else return false; }
		public bool HasBeenSet { get { return seqNumObserver.HasBeenSet; } }
		public SeqNumberType SequenceNumber { get { return seqNumObserver.SequenceNumber; } }
		public SeqNumberType VolatileSequenceNumber { get { return seqNumObserver.VolatileSequenceNumber; } }

		#endregion
	}

	#endregion

    //-------------------------------------------------
    #region DataContractObject to/from xml string or byte stream helper

    public class DataContractAsciiXmlAdapter<ObjType>
        where ObjType : class
    {
        public DataContractAsciiXmlAdapter()
        {
            xws.ConformanceLevel = System.Xml.ConformanceLevel.Document;
            xws.OmitXmlDeclaration = false;
            xws.Encoding = System.Text.Encoding.ASCII;
            xws.Indent = true;
            xws.CloseOutput = false;     // we will explicitly close the underlying stream

            xrs.ConformanceLevel = System.Xml.ConformanceLevel.Auto;
            xrs.CloseInput = false;
        }

        public bool Indent { get { return xws.Indent; } set { xws.Indent = value; } }
        public bool GenerateFragment 
        {
            get { return (xws.ConformanceLevel == System.Xml.ConformanceLevel.Fragment); } 
            set 
            { 
                xws.ConformanceLevel = (value ? System.Xml.ConformanceLevel.Fragment : System.Xml.ConformanceLevel.Document);
                xws.OmitXmlDeclaration = value;
            }
        }

        System.Xml.XmlWriterSettings xws = new System.Xml.XmlWriterSettings();
        System.Xml.XmlReaderSettings xrs = new System.Xml.XmlReaderSettings();
        DataContractSerializer dcs = new DataContractSerializer(typeof(ObjType));

        public ObjType ReadObject(System.IO.Stream readStream)
        {
            return dcs.ReadObject(readStream) as ObjType;
        }

        public ObjType ReadObject(String s)
        {
            using (System.IO.StringReader sr = new System.IO.StringReader(s))
            using (System.Xml.XmlReader xr = System.Xml.XmlReader.Create(sr, xrs))
            {
                return dcs.ReadObject(xr) as ObjType;
            }
        }

        public void WriteObject(ObjType obj, System.IO.Stream writeStream)
        {
            using (System.Xml.XmlWriter xw = System.Xml.XmlWriter.Create(writeStream, xws))
            {
                dcs.WriteObject(xw, obj);
                xw.Flush();
            }
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        public string ConvertObjectToString(ObjType obj)
        {
            if (sb == null)
                sb = new System.Text.StringBuilder();
            else
                sb.Length = 0;

            using (System.Xml.XmlWriter xw = System.Xml.XmlWriter.Create(sb, xws))
            {
                dcs.WriteObject(xw, obj);
                xw.Flush();
            }

            return sb.ToString();
        }
    }

    #endregion

    //-------------------------------------------------

}

//-------------------------------------------------
