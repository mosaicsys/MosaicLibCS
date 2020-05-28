//-------------------------------------------------------------------
/*! @file Collections.cs
 *  @brief This file contains small set of use specific collection classes for use in multi-threaded applications.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2014 Mosaic Systems Inc.
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Utils
{
    namespace Collections
    {
        #region SimpleLockedQueue

        /// <summary>
        /// Provides a simple, thread safe, queue for first-in first-out storage of items.  
        /// This object is based on the System.Collections.Generic.Queue object with a simplified API.  
        /// It wraps the various accessor methods with use of a local mutex to enforce thead safety, and implements a cached copy of the queue count with
        /// a VolatileCount property that allows the user to avoid needing locked access to the queue to check the size of the queue at the last time that 
        /// its length was changed.
        /// </summary>
        /// <typeparam name="ItemType">Defines the type of item that the client will store in this queue.  May be a reference or a value type.</typeparam>
        public class SimpleLockedQueue<ItemType>
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public SimpleLockedQueue() { }

            /// <summary>
            /// Enqueues the given <paramref name="item"/>.
            /// </summary>
            public SimpleLockedQueue<ItemType> Enqueue(ItemType item)
            {
                lock (mutex)
                {
                    backingQueue.Enqueue(item);
                    volatileCount = backingQueue.Count;
                }

                return this;
            }

            /// <summary>
            /// Enqueues the given items.
            /// </summary>
            public SimpleLockedQueue<ItemType> EnqueueItems(params ItemType[] itemParamsArray)
            {
                lock (mutex)
                {
                    try
                    {
                        if (itemParamsArray != null)
                            itemParamsArray.DoForEach(item => backingQueue.Enqueue(item));
                    }
                    finally
                    {
                        volatileCount = backingQueue.Count;
                    }
                }

                return this;
            }

            /// <summary>
            /// Enqueues the given <paramref name="set"/> of items.
            /// </summary>
            public SimpleLockedQueue<ItemType> EnqueueSet(ICollection<ItemType> set)
            {
                lock (mutex)
                {
                    try
                    {
                        if (set != null)
                            set.DoForEach(item => backingQueue.Enqueue(item));
                    }
                    finally
                    {
                        volatileCount = backingQueue.Count;
                    }
                }

                return this;
            }


            /// <summary>
            /// Attempts to return the next object in the queue (without removing it).  throws if the queue is currently empty.
            /// </summary>
            /// <exception cref="System.InvalidOperationException">This exception is thrown if the queue is empty when attempting to peek at its first element.</exception>:
            public ItemType Peek()
            {
                lock (mutex)
                {
                    return backingQueue.Peek();
                }
            }

            /// <summary>
            /// Attempts to return the next object in the queue (without removing it) if the queue is not currently empty, or returns the given emptyValue value if the queue is already empty.
            /// </summary>
            public ItemType Peek(ItemType emptyValue)
            {
                lock (mutex)
                {
                    if (!IsEmpty)
                        return backingQueue.Peek();
                }

                return emptyValue;
            }

            /// <summary>
            /// Attempts to dequeue and return the next object in the queue.  throws if the queue is currently empty.
            /// </summary>
            /// <exception cref="System.InvalidOperationException">This exception is thrown if the queue is empty when attempting to dequeue its first element.</exception>:
            public ItemType Dequeue()
            {
                lock (mutex)
                {
                    try
                    {
                        ItemType item = backingQueue.Dequeue();
                        return item;
                    }
                    finally
                    {
                        volatileCount = backingQueue.Count;
                    }
                }
            }

            /// <summary>
            /// Attempts to dequeue and return the next object in the queue if the queue is not currently empty, or returns the given emptyValue value if the queue is already empty.
            /// </summary>
            public ItemType Dequeue(ItemType emptyValue)
            {
                lock (mutex)
                {
                    if (!IsEmpty)
                    {
                        try
                        {
                            ItemType item = backingQueue.Dequeue();
                            return item;
                        }
                        finally
                        {
                            volatileCount = backingQueue.Count;
                        }
                    }
                }

                return emptyValue;
            }


            /// <summary>
            /// Dequeues and removes all of the items in the queue and returns them in an array.
            /// </summary>
            public ItemType[] DequeueAll()
            {
                ItemType[] array;

                lock (mutex)
                {
                    try
                    {
                        if (IsEmpty)
                        {
                            array = Collections.EmptyArrayFactory<ItemType>.Instance;
                        }
                        else
                        {
                            array = backingQueue.ToArray();
                            backingQueue.Clear();
                        }
                    }
                    finally
                    {
                        volatileCount = backingQueue.Count;
                    }
                }

                return array;
            }

            /// <summary>
            /// Returns the synchronized count from the backing queue object.  Locks the corresponding mutex as the backing queue object's Count property is non-reenterant.
            /// </summary>
            public int Count { get { lock (mutex) { return backingQueue.Count; } } }

            /// <summary>
            /// Returns the current contents of the queue converted to an array
            /// </summary>
            public ItemType[] ToArray()
            {
                lock (mutex)
                {
                    return backingQueue.ToArray();
                }
            }

            /// <summary>
            /// Returns the last queue count observed after Enqueueing or Dequeing the last item.
            /// </summary>
            public int VolatileCount { get { return volatileCount; } }

            /// <summary>
            /// Returns true if the VolatileCount is zero.
            /// </summary>
            public bool IsEmpty
            {
                get
                {
                    return (VolatileCount == 0);
                }
            }

            /// <summary>This is the mutex object that will be locked when accessing the backing queue.</summary>
            private readonly object mutex = new object();

            /// <summary>This is the backing queue object on which this object is based.</summary>
            private Queue<ItemType> backingQueue = new Queue<ItemType>();

            /// <summary>storage for the VolatileCount property.  Used as a local cached copy of the queue.Count value.</summary>
            private volatile int volatileCount = 0;
        }

        #endregion

        #region EmptyArrayFactory

        /// <summary>
        /// This is a static factory class used to produce instances of an empty array of a given type.  
        /// The use of generics here and the immutability of empty arrays allows multiple code points to use the same empty array instance (one per requested <typeparamref name="TItemType"/>)
        /// </summary>
        public static class EmptyArrayFactory<TItemType>
        {
            /// <summary>
            /// Returns an instance of an empty array of the selected <typeparamref name="TItemType"/> type.  This is a singleton, immutable, instance.
            /// </summary>
            public static TItemType[] Instance { get { return instance; } }

            private static readonly TItemType[] instance = new TItemType[0];
        }

        #endregion

        #region ReadOnlyIList, ArrayEnumerator

        /// <summary>
        /// This class is a local replacement for the System.Collections.ObjectModel.ReadOnlyCollection as the native one simply provides a read-only facade on the underlying mutable IList from which it is constructed.
        /// <para/>Note: This object is intended as a utility storage class.  All interfaces are implemented explicitly so the caller can only make use of this object's contents by casting it to one of the supported interfaces.
        /// </summary>
        [Serializable]
        public class ReadOnlyIList<TItemType> : IList<TItemType>, ICollection<TItemType>, IEnumerable<TItemType>, IList, ICollection, IEnumerable, IEquatable<IList<TItemType>>
        {
            /// <summary>
            /// Constructs the contents of this instance from the set of explicitly defined items (<paramref name="firstItem"/> followed by 0 or <paramref name="moreItemsArray"/> items).
            /// </summary>
            public ReadOnlyIList(TItemType firstItem, params TItemType[] moreItemsArray)
            {
                itemsArray = firstItem.Concat(moreItemsArray).ToArray();
            }

            /// <summary>
            /// Constructs the contents of this instance based on the contents of the given <paramref name="sourceItemList"/>.  
            /// If the given <paramref name="sourceItemList"/> is null then this method will be constructed as an empty list.
            /// </summary>
            public ReadOnlyIList(IList<TItemType> sourceItemList)
            {
                ReadOnlyIList<TItemType> sourceAaROIL = sourceItemList as ReadOnlyIList<TItemType>;

                itemsArray = (sourceAaROIL != null) ? sourceAaROIL.itemsArray : sourceItemList.SafeToArray(fallbackArray: emptyArray);
            }

            /// <summary>
            /// Constructs the contents of this instance based on the contents of the given <paramref name="sourceItemCollection"/>.  
            /// If the given <paramref name="sourceItemCollection"/> is null then this method will be constructed as an empty list.
            /// </summary>
            public ReadOnlyIList(ICollection<TItemType> sourceItemCollection = null)
            {
                ReadOnlyIList<TItemType> sourceAaROIL = sourceItemCollection as ReadOnlyIList<TItemType>;

                itemsArray = (sourceAaROIL != null) ? sourceAaROIL.itemsArray : sourceItemCollection.SafeToArray(fallbackArray: emptyArray);
            }

            /// <summary>
            /// Constructs the contents of this instance based on the contents of the given <paramref name="sourceItemSet"/>.  
            /// If the given <paramref name="sourceItemSet"/> is null then this method will be constructed as an empty list.
            /// </summary>
            public ReadOnlyIList(IEnumerable<TItemType> sourceItemSet)
            {
                ReadOnlyIList<TItemType> sourceAaROIL = sourceItemSet as ReadOnlyIList<TItemType>;

                itemsArray = (sourceAaROIL != null) ? sourceAaROIL.itemsArray : sourceItemSet.SafeToArray(fallbackArray: emptyArray);
            }

            /// <summary>
            /// Helper static getter - returns a singleton (static) empty ReadOnlyIList{TItemType}.
            /// </summary>
            public static ReadOnlyIList<TItemType> Empty { get { return _empty; } }
            private static readonly ReadOnlyIList<TItemType> _empty = new ReadOnlyIList<TItemType>();

            private TItemType[] itemsArray;
            private static readonly TItemType[] emptyArray = EmptyArrayFactory<TItemType>.Instance;

            [NonSerialized]
            private ReadOnlyCollection<TItemType> _rocOfItems = null;

            private ReadOnlyCollection<TItemType> ROCOfItems { get { return _rocOfItems ?? (_rocOfItems = new ReadOnlyCollection<TItemType>(itemsArray)); } }

            /// <summary>Returns the count of the number of items that are in this set</summary>
            public int Count
            {
                get { return itemsArray.Length; }
            }

            #region IList, ICollection, IEnumerable implementations

            /// <summary>
            /// returns the (zero based) index of the first element in this collection that is EqualityComparer{TItemType}.Default.Equals to the given <paramref name="item"/> value.  Returns -1 if no such collection element is found.
            /// </summary>
            public int IndexOf(TItemType item)
            {
                return ROCOfItems.IndexOf(item);
            }

            void IList<TItemType>.Insert(int index, TItemType item)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void IList<TItemType>.RemoveAt(int index)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            /// <summary>
            /// Getter returns the <paramref name="index"/> selected item from the collection.  Setter is not supported for this read-only collection object.
            /// </summary>
            /// <exception cref="System.ArgumentOutOfRangeException">if the given value of <paramref name="index"/> is less than zero or is greater or equal to the collection Count</exception>
            /// <exception cref="System.NotSupportedException">if the client attempts to use the indexed setter.</exception>
            public TItemType this[int index]
            {
                get
                {
                    return itemsArray[index];
                }
                set
                {
                    throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
                }
            }

            void ICollection<TItemType>.Add(TItemType item)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void ICollection<TItemType>.Clear()
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            /// <summary>
            /// Returns true if the collection contains an element that is EqualityComparer{TItemType}.Default.Equals to the given <paramref name="item"/> value, or false otherwise.
            /// </summary>
            public bool Contains(TItemType item)
            {
                return ROCOfItems.Contains(item);
            }

            /// <summary>
            /// Attempts to copy the set contents to the given <paramref name="array"/> starting at the given <paramref name="arrayIndex"/> starting offset.
            /// </summary>
            /// <exception cref="System.ArgumentNullException">is thrown if the given <paramref name="array"/> is null.</exception>
            /// <exception cref="System.ArgumentOutOfRangeException">is thrown if the given <paramref name="arrayIndex"/> is negative.</exception>
            /// <exception cref="System.ArgumentException">is thrown if the collection's contents to not fit in the given <paramref name="array"/> starting at the given <paramref name="arrayIndex"/> position.</exception>
            public void CopyTo(TItemType[] array, int arrayIndex)
            {
                ROCOfItems.CopyTo(array, arrayIndex);
            }

            /// <summary>
            /// Returns true.
            /// </summary>
            public bool IsReadOnly
            {
                get { return true; }
            }

            bool ICollection<TItemType>.Remove(TItemType item)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            /// <summary>
            /// Returns an enumerator that can be used to enumerate through the elements of this collection.
            /// </summary>
            public IEnumerator<TItemType> GetEnumerator()
            {
                return new ArrayEnumerator<TItemType>(itemsArray);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ArrayEnumerator<TItemType>(itemsArray);
            }

            int IList.Add(object value)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void IList.Clear()
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            bool IList.Contains(object value)
            {
                return (ROCOfItems as IList).Contains(value);
            }

            int IList.IndexOf(object value)
            {
                return (ROCOfItems as IList).IndexOf(value);
            }

            void IList.Insert(int index, object value)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            bool IList.IsFixedSize
            {
                get { return true; }
            }

            bool IList.IsReadOnly
            {
                get { return true; }
            }

            void IList.Remove(object value)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void IList.RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            object IList.this[int index]
            {
                get
                {
                    return itemsArray[index];
                }
                set
                {
                    throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
                }
            }

            void ICollection.CopyTo(Array array, int index)
            {
                (ROCOfItems as IList).CopyTo(array, index);
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            object ICollection.SyncRoot
            {
                get { return null; }
            }

            #endregion

            #region IEquatable implementation

            /// <summary>
            /// IEquatable implementation method.  Returns true if the contents of this object are equal (Object.Equals) with the contents of the given other list.
            /// </summary>
            public bool Equals(IList<TItemType> other)
            {
                return (this.IsEqualTo(other));
            }

            #endregion
        }

        #endregion

        #region ReadOnlyIDictionary

        public class ReadOnlyIDictionary<TKey, TValue> : IDictionary<TKey, TValue>
        {
            /// <summary>
            /// Constructs the contents of this item from the set of explicitly defined items (<paramref name="firstItem"/> followed by 0 or <paramref name="moreItemsArray"/> items).
            /// </summary>
            public ReadOnlyIDictionary(KeyValuePair<TKey, TValue> firstItem, params KeyValuePair<TKey, TValue>[] moreItemsArray)
            {
                itemsArray = firstItem.Concat(moreItemsArray.MapNullToEmpty()).ToArray();
            }

            /// <summary>
            /// Constructs the contents of this item based on the contents of the given <paramref name="sourceDictionary"/>.  
            /// If the given <paramref name="sourceDictionary"/> is null then this method will be constructed as an empty list.
            /// </summary>
            public ReadOnlyIDictionary(IDictionary<TKey, TValue> sourceDictionary)
            {
                ReadOnlyIDictionary<TKey, TValue> sourceAaROID = sourceDictionary as ReadOnlyIDictionary<TKey, TValue>;

                itemsArray = (sourceAaROID != null) ? sourceAaROID.itemsArray : sourceDictionary.SafeToArray(fallbackArray: emptyArray);
            }

            /// <summary>
            /// Constructs the contents of this item based on the contents of the given <paramref name="sourceItemCollection"/>.  
            /// If the given <paramref name="sourceItemCollection"/> is null then this method will be constructed as an empty list.
            /// </summary>
            public ReadOnlyIDictionary(ICollection<KeyValuePair<TKey, TValue>> sourceItemCollection = null)
            {
                ReadOnlyIDictionary<TKey, TValue> sourceAaROID = sourceItemCollection as ReadOnlyIDictionary<TKey, TValue>;

                itemsArray = (sourceAaROID != null) ? sourceAaROID.itemsArray : sourceItemCollection.SafeToArray(fallbackArray: emptyArray);
            }

            /// <summary>
            /// Constructs the contents of this item based on the contents of the given <paramref name="sourceItemSet"/>.  
            /// If the given <paramref name="sourceItemSet"/> is null then this method will be constructed as an empty list.
            /// </summary>
            public ReadOnlyIDictionary(IEnumerable<KeyValuePair<TKey, TValue>> sourceItemSet)
            {
                ReadOnlyIDictionary<TKey, TValue> sourceAaROID = sourceItemSet as ReadOnlyIDictionary<TKey, TValue>;

                itemsArray = (sourceAaROID != null) ? sourceAaROID.itemsArray : sourceItemSet.SafeToArray(fallbackArray: emptyArray);
            }

            /// <summary>
            /// Helper static getter - returns a singleton (static) empty ReadOnlyIList{TItemType}.
            /// </summary>
            public static ReadOnlyIDictionary<TKey, TValue> Empty { get { return _empty; } }
            private static readonly ReadOnlyIDictionary<TKey, TValue> _empty = new ReadOnlyIDictionary<TKey, TValue>();

            private KeyValuePair<TKey, TValue>[] itemsArray;
            private static readonly KeyValuePair<TKey, TValue>[] emptyArray = EmptyArrayFactory<KeyValuePair<TKey, TValue>>.Instance;

            [NonSerialized]
            private IDictionary<TKey, TValue> _dOfItems = null;

            [NonSerialized]
            private ReadOnlyIList<TKey> _rolOfKeys = null;

            [NonSerialized]
            private ReadOnlyIList<TValue> _rolOfValues = null;

            private IDictionary<TKey, TValue> DOfItems { get { return _dOfItems ?? (_dOfItems = new Dictionary<TKey, TValue>(itemsArray.SafeLength()).SafeAddRange(itemsArray)); } }
            private ReadOnlyIList<TKey> ROLOfKeys { get { return _rolOfKeys ?? (_rolOfKeys = new ReadOnlyIList<TKey>(itemsArray.Select(item => item.Key))); } }
            private ReadOnlyIList<TValue> ROLOfValues { get { return _rolOfValues ?? (_rolOfValues = new ReadOnlyIList<TValue>(itemsArray.Select(item => item.Value))); } }

            /// <summary>Returns the count of the number of items that are in this set</summary>
            public int Count
            {
                get { return itemsArray.Length; }
            }

            #region IDictionary, ICollection, IEnumerable implementations

            void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            bool IDictionary<TKey, TValue>.ContainsKey(TKey key)
            {
                return DOfItems.ContainsKey(key);
            }

            ICollection<TKey> IDictionary<TKey, TValue>.Keys
            {
                get { return ROLOfKeys; }
            }

            bool IDictionary<TKey, TValue>.Remove(TKey key)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
            {
                return DOfItems.TryGetValue(key, out value);
            }

            ICollection<TValue> IDictionary<TKey, TValue>.Values
            {
                get { return ROLOfValues; }
            }

            TValue IDictionary<TKey, TValue>.this[TKey key]
            {
                get
                {
                    return DOfItems[key];
                }
                set
                {
                    throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
                }
            }

            void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void ICollection<KeyValuePair<TKey, TValue>>.Clear()
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
            {
                return DOfItems.Contains(item);
            }

            void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            {
                itemsArray.CopyTo(array, arrayIndex);
            }

            bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
            {
                get { return true; }
            }

            bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return new ArrayEnumerator<KeyValuePair<TKey, TValue>>(itemsArray);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ArrayEnumerator<KeyValuePair<TKey, TValue>>(itemsArray);
            }

            #endregion
        }

        #endregion

        #region ReadOnlyHashSet

        [Serializable]
        public class ReadOnlyHashSet<TItemType> : ISet<TItemType>, ICollection<TItemType>, IEnumerable<TItemType>, IEnumerable
        {
            public ReadOnlyHashSet(IEnumerable<TItemType> set = null)
            {
                itemsArray = set.SafeToArray();
            }

            public ReadOnlyHashSet(TItemType item1, params TItemType [] itemParamsArray)
                : this(item1.Concat(itemParamsArray))
            { }

            public static ReadOnlyHashSet<TItemType> Empty { get { return _Empty; } }
            private static readonly ReadOnlyHashSet<TItemType> _Empty = new ReadOnlyHashSet<TItemType>();

            private TItemType[] itemsArray;
            private static readonly TItemType[] emptyArray = EmptyArrayFactory<TItemType>.Instance;

            [NonSerialized]
            private ISet<TItemType> _setOfItems = null;

            private ISet<TItemType> SetOfItems { get { return _setOfItems ?? (_setOfItems = new HashSet<TItemType>().SafeAddRange(itemsArray)); } }

            /// <summary>Returns the count of the number of items that are in this set</summary>
            public int Count
            {
                get { return itemsArray.Length; }
            }

            #region ISet, ICollection, IEnumerable implementations

            bool ISet<TItemType>.Add(TItemType value)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void ISet<TItemType>.ExceptWith(IEnumerable<TItemType> other)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void ISet<TItemType>.IntersectWith(IEnumerable<TItemType> other)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            /// <summary>Returns true if this set is a proper (strict) subset of the <paramref name="other"/> given collection.</summary>
            /// <exception cref="System.ArgumentNullException">is thrown if <paramref name="other"/> is null</exception>
            public bool IsProperSubsetOf(IEnumerable<TItemType> other)
            {
                return SetOfItems.IsProperSubsetOf(other);
            }

            /// <summary>Returns true if this set is a proper (strict) superset of the <paramref name="other"/> given collection.</summary>
            /// <exception cref="System.ArgumentNullException">is thrown if <paramref name="other"/> is null</exception>
            public bool IsProperSupersetOf(IEnumerable<TItemType> other)
            {
                return SetOfItems.IsProperSupersetOf(other);
            }

            /// <summary>Returns true if this set is a subset of the <paramref name="other"/> given collection.</summary>
            /// <exception cref="System.ArgumentNullException">is thrown if <paramref name="other"/> is null</exception>
            public bool IsSubsetOf(IEnumerable<TItemType> other)
            {
                return SetOfItems.IsSubsetOf(other);
            }

            /// <summary>Returns true if this set is a superset of the <paramref name="other"/> given collection.</summary>
            /// <exception cref="System.ArgumentNullException">is thrown if <paramref name="other"/> is null</exception>
            public bool IsSupersetOf(IEnumerable<TItemType> other)
            {
                return SetOfItems.IsSupersetOf(other);
            }

            /// <summary>Returns true if this set overlaps the <paramref name="other"/> given collection.</summary>
            /// <exception cref="System.ArgumentNullException">is thrown if <paramref name="other"/> is null</exception>
            public bool Overlaps(IEnumerable<TItemType> other)
            {
                return SetOfItems.Overlaps(other);
            }

            /// <summary>Returns true if this set contains the same set of elements as the <paramref name="other"/> given collection.</summary>
            /// <exception cref="System.ArgumentNullException">is thrown if <paramref name="other"/> is null</exception>
            public bool SetEquals(IEnumerable<TItemType> other)
            {
                return SetOfItems.SetEquals(other);
            }

            void ISet<TItemType>.SymmetricExceptWith(IEnumerable<TItemType> other)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void ISet<TItemType>.UnionWith(IEnumerable<TItemType> other)

            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void ICollection<TItemType>.Add(TItemType item)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            void ICollection<TItemType>.Clear()
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            bool ICollection<TItemType>.Contains(TItemType item)
            {
                return SetOfItems.Contains(item);
            }

            void ICollection<TItemType>.CopyTo(TItemType[] array, int arrayIndex)
            {
                itemsArray.CopyTo(array, arrayIndex);
            }

            bool ICollection<TItemType>.IsReadOnly
            {
                get { return true; }
            }

            bool ICollection<TItemType>.Remove(TItemType item)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            public IEnumerator<TItemType> GetEnumerator()
            {
                return new ArrayEnumerator<TItemType>(itemsArray);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ArrayEnumerator<TItemType>(itemsArray);
            }

            #endregion
        }

        #endregion

        #region ArrayEnumerator

        /// <summary>
        /// IEnumerator{TItemType} struct that supports enumerating through an array.
        /// </summary>
        public struct ArrayEnumerator<TItemType> : IEnumerator<TItemType>, IDisposable, IEnumerator
        {
            /// <summary>Constructor.  Sets the array on which this enumerator will enumerate.  Accepts a null array as the same as an empty array.</summary>
            public ArrayEnumerator(TItemType[] array)
            {
                this.array = array;
                this.arrayLength = array.SafeLength();
                this.index = 0;
                this.current = default(TItemType);
            }

            private TItemType[] array;
            private int arrayLength;

            /// <summary>
            /// Gives the index of the "next" element.  
            /// This value is zero (no current element yet) until the first MoveNext call is made at which point it is 1 and indicates that the current element came from array[0] in the array.
            /// Then it advances until index >= arrayLength + 1 which indicates that the current element would be found after the end of the array (since array[arrayLength] is not valid index). 
            /// </summary>
            private int index;

            private TItemType current;

            /// <summary>Gets the element at the enumerator's current "position".  Will return default({TItemType}) if there is no current element.</summary>
            public TItemType Current
            {
                get
                {
                    return this.current;
                }
            }

            /// <summary>Gets the element at the enumerator's current "position".  Throws System.InvalidOperationException if the enumerator is positioned before the first element of the array or if it is positioned after the last element of the array.</summary>
            /// <exception cref="System.InvalidOperationException">The enumerator is positioned before the first element of the array or if it is positioned after the last element of the array.</exception>
            object IEnumerator.Current
            {
                get
                {
                    if (index <= 0 || index >= arrayLength + 1)
                    {
                        new System.InvalidOperationException("The enumerator is positioned before the first element of the array or after the last element").Throw();
                    }

                    return this.Current;
                }
            }

            /// <summary>Releases all resources used by this enumerator (currently this is a no-op).</summary>
            public void Dispose()
            { }

            /// <summary>Advances the enumerator's position.  On the first call to this method the position is advanced to the first element.  After the enumerable contents have been used up the position is advanced to one after the last element.  Returns true if the current position is valid (aka the enumerator has not run off the end of the array)</summary>
            public bool MoveNext()
            {
                if (index < arrayLength)
                {
                    current = array[index];
                    index++;

                    return true;
                }

                return MoveNextRare();
            }

            /// <summary>Inline code optimization that is used to handle the case where the normal MoveNext falls off the end of the array.</summary>
            private bool MoveNextRare()
            {
                index = arrayLength + 1;
                current = default(TItemType);

                return false;
            }

            /// <summary>Sets the enumerator to its initial position (one before the first element of the collection).</summary>
            void IEnumerator.Reset()
            {
                index = 0;
                current = default(TItemType);
            }
        }

        #endregion

        #region collection classes with cached arrays (LockedObjectListWithCachedArray, IListWithCachedArray, IDictionaryWithCachedArrays)

        /// <summary>
        /// Provides a thread safe container for storing a set of objects with a backing cached array for thread safe efficient atomic snapshot of the set contents.
        /// This object is intended to be used in the following cases:
        /// <list type="number">
        /// <item>Where logic frequently iterates through the items in a list but where the contents are rarely changed.  Use of conversion of list contents to a cached Array decreases access/iteration cost and minimizes garbage generation.</item>
        /// <item>Where list content changes may be made on multiple threads with list iteration performed on a single thread.  Use of conversion of list contents to an array allows iterating thread to take a snapshot of the list contents before each iteration method and then iterate without needing to lock or otherwise concern itself with changeable list contents during a single iteration phase.</item>
        /// </list>
        /// Examples of these cases include delegate and event lists as well as any generic list of items that are iterated through much more frequently than the set of such items changes.
        /// </summary>  
        /// <typeparam name="ObjectType">ObjectType may be any reference or value object.  Use is expected to be based on reference object types but does not require it.</typeparam>
        /// <remarks>
        /// Based on the use of a locked list of the objects and a volatile handle to an array of objects that is (re)obtained from the list when needed
        /// </remarks>
        public class LockedObjectListWithCachedArray<ObjectType> : IList<ObjectType>, ICollection<ObjectType>, IEnumerable<ObjectType>, IList, ICollection, IEnumerable
        {
            #region Constructors

            /// <summary>Default contstructor</summary>
            public LockedObjectListWithCachedArray() { }

            /// <summary>Collection based constructor.  Sets up the list to contain the given <paramref name="collection"/> of objects.</summary>
            public LockedObjectListWithCachedArray(IEnumerable<ObjectType> collection)
            {
                AddRange(collection);
            }

            #endregion

            #region Public methods and properties (IndexOf, Insert, RemoveAt, Add, Remove, Clear, AddRange, Contains, this[int index], Count, IsEmpty, Array)

            /// <summary>
            /// Searches for the specified <paramref name="item"/> in the list and returns the zero-based index of the first occurrence, or -1 if the <paramref name="item"/> was not found.
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// <para/>Supports call chaining
            /// </summary>
            public int IndexOf(ObjectType item)
            {
                lock (listMutex)
                {
                    return objectList.IndexOf(item);
                }
            }

            /// <summary>
            /// Adds the given object instance to the list.  
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// Triggers array rebuild on next use.
            /// <para/>Supports call chaining
            /// </summary>
            public LockedObjectListWithCachedArray<ObjectType> Add(ObjectType d)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    objectList.Add(d);
                }

                return this;
            }

            /// <summary>
            /// Inserts the given <paramref name="item"/> into this list at the given <paramref name="index"/>.  The inserted item will be placed before any prior item at the specified location.
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// Triggers array rebuild on next use.
            /// <para/>Supports call chaining
            /// </summary>
            /// <exception cref="System.ArgumentOutOfRangeException">index is less than 0, or it is greater or equal to the Count.</exception>
            public LockedObjectListWithCachedArray<ObjectType> Insert(int index, ObjectType item)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    objectList.Insert(index, item);
                }

                return this;
            }

            /// <summary>
            /// Removes the the item that is at the given <paramref name="index"/> position from the list's current contents.
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// Triggers array rebuild on next use.
            /// <para/>Supports call chaining
            /// </summary>
            /// <exception cref="System.ArgumentOutOfRangeException">index is less than 0, or it is greater or equal to the Count.</exception>
            public LockedObjectListWithCachedArray<ObjectType> RemoveAt(int index)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    objectList.RemoveAt(index);
                }

                return this;
            }

            /// <summary>
            /// Removes the first instance of given object <paramref name="item"/> from the list.  
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// Triggers array rebuild on next use.
            /// <para/>Supports call chaining
            /// </summary>
            public LockedObjectListWithCachedArray<ObjectType> Remove(ObjectType item)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    objectList.Remove(item);
                }

                return this;
            }

            /// <summary>
            /// Removes all objects from the list.  
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// Triggers array rebuild on next use.
            /// <para/>Supports call chaining
            /// </summary>
            public LockedObjectListWithCachedArray<ObjectType> Clear()
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    objectList.Clear();
                }

                return this;
            }

            /// <summary>
            /// Adds the given <paramref name="collection"/> of objects to the end of the list.
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// Triggers array rebuild on next use.
            /// <para/>Supports call chaining
            /// </summary>
            public LockedObjectListWithCachedArray<ObjectType> AddRange(IEnumerable<ObjectType> collection)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    objectList.AddRange(collection);
                }

                return this;
            }

            /// <summary>
            /// returns true if the given <paramref name="item"/> is found in the list.
            /// </summary>
            public bool Contains(ObjectType item)
            {
                lock (listMutex)
                {
                    return objectList.Contains(item);
                }
            }

            /// <summary>
            /// Gets or sets the element at the specified index. 
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// Use of setter triggers array rebuild on next use.
            /// <para/>Supports call chaining
            /// </summary>
            /// <param name="index">The zero-based index of the element to get or set.</param>
            /// <exception cref="System.ArgumentOutOfRangeException">index is less than 0, or it is greater or equal to the Count.</exception>
            public ObjectType this[int index]
            {
                get
                {
                    // the following logic is designed to decrease the risk that the Array will be regenerated many times if a client is getting and setting array elements frequently.
                    if (!rebuildVolatileObjectArray)
                        return Array[index];

                    lock (listMutex)
                    {
                        return objectList[index];
                    }
                }
                set
                {
                    lock (listMutex)
                    {
                        NoteMainListHasBeenChanged();
                        objectList[index] = value;
                    }
                }
            }

            /// <summary>
            ///  Gets the number of elements actually contained in this list using the Length of the Array property
            /// </summary>
            public int Count { get { return Array.Length; } }

            #endregion

            #region IsEmpty, Array, ReadOnlyIList

            /// <summary>Returns true if the Array property is currently empty (returns a zero length array).</summary>
            public bool IsEmpty { get { return (Count == 0); } }

            /// <summary>
            /// Returns the most recently generated copy of the Array version of the underlying list of objects.  Will return a fixed empty array when the list is empty.
            /// Implementation guarantees that returned value will include effects of any change made to the list by the thread that is requesting this array.
            /// Changes made by other threads produce race conditions where the side effects of the change on another thread may, or may not, be visible in the array contents
            /// until the thread reading this property invokes it entirely after another thread in question's Add or Remove method has returned from that method invocation.
            /// This method does not attempt to lock or update the underlying Array value unless it knows that at least one change has been completed to the list contents.
            /// <para/>Also note that the caller must clone the returned array if the caller intends to change its contents as the returned array may be shared by multiple callers.
            /// </summary>
            /// <remarks>
            /// If any change to the list has been recorded via the rebuild flag then this property will lock access to the list, 
            /// generate the array version of it and then retain the Array version for later requests until the list contents have been changed again.
            /// Use of locked access to list during rebuild prevents the risk that the list may change contents while the rebuild is taking place.
            /// </remarks>
            public ObjectType[] Array
            {
                get
                {
                    ObjectType[] array = volatileObjectArray;

                    if (rebuildVolatileObjectArray)
                    {
                        lock (listMutex)
                        {
                            rebuildVolatileObjectArray = false;

                            array = objectList.ToArray();

                            volatileObjectArray = array;
                        }
                    }

                    return array;
                }
            }

            /// <summary>
            /// Returns the most recently generated copy of the ReadOnlyIList version of the underlying list of objects.  Will return a fixed empty ReadOnlyIList when the list is empty.
            /// Implementation guarantees that returned value will include effects of any change made to the list by the thread that is requesting this read only list.
            /// Changes made by other threads produce race conditions where the side effects of the change on another thread may, or may not, be visible in the read only list contents
            /// until the thread reading this property invokes it entirely after another thread in question's Add or Remove method has returned from that method invocation.
            /// This method does not attempt to lock or update the underlying read only list value unless it knows that at least one change has been completed to the list contents.
            /// </summary>
            /// <remarks>
            /// If any change to the list has been recorded via the rebuild flag then this property will lock access to the list, 
            /// generate the new read only list version of it and then retain the generated read only list version for later requests until the main list contents have been changed again.
            /// Use of locked access to list during rebuild prevents the risk that the list may change contents while the rebuild is taking place.
            /// </remarks>
            public ReadOnlyIList<ObjectType> ReadOnlyIList
            {
                get
                {
                    ReadOnlyIList<ObjectType> readOnlyIList = volatileReadOnlyIList;

                    if (rebuildVolatileReadOnlyIList)
                    {
                        lock (listMutex)
                        {
                            rebuildVolatileReadOnlyIList = false;

                            readOnlyIList = new ReadOnlyIList<ObjectType>(objectList);

                            volatileReadOnlyIList = readOnlyIList;
                        }
                    }

                    return readOnlyIList;
                }
            }

            #endregion

            #region Private fields

            /// <summary>mutex used to guard/sequence access to the underlying list so that both changes and access to the list are performed atomically.</summary>
            private readonly object listMutex = new object();

            /// <summary>underlying reference list of delegates, access to this list must only be made while owning the corresponding mutex.</summary>
            private List<ObjectType> objectList = new List<ObjectType>();

            /// <summary>volatile handle to the array of delegates produced during the last rebuild operation.</summary>
            private volatile ObjectType[] volatileObjectArray = EmptyArrayFactory<ObjectType>.Instance;

            private volatile ReadOnlyIList<ObjectType> volatileReadOnlyIList = ReadOnlyIList<ObjectType>.Empty;

            /// <summary>
            /// This method is used to record that a change has been made to the list that shall trigger rebuilding of any/all of the dependent volatile cached versions that are derived from the underlying list.
            /// </summary>
            private void NoteMainListHasBeenChanged()
            {
                rebuildVolatileObjectArray = true;
                rebuildVolatileReadOnlyIList = true;
            }

            /// <summary>volatile boolean used to flag that a rebuild is required during the next access to the Array property.</summary>
            private volatile bool rebuildVolatileObjectArray = true;

            /// <summary>volatile boolean used to flag that a rebuild is required during the next access to the ReadOnlyILIst property.</summary>
            private volatile bool rebuildVolatileReadOnlyIList = true;

            #endregion

            #region IList, ICollection, IEnumerable implementations

            void IList<ObjectType>.Insert(int index, ObjectType item)
            {
                Insert(index, item);
            }

            void IList<ObjectType>.RemoveAt(int index)
            {
                RemoveAt(index);
            }

            bool ICollection<ObjectType>.Remove(ObjectType item)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    return objectList.Remove(item);
                }
            }

            void ICollection<ObjectType>.Add(ObjectType item)
            {
                Add(item);
            }

            void ICollection<ObjectType>.Clear()
            {
                Clear();
            }

            void ICollection<ObjectType>.CopyTo(ObjectType[] array, int arrayIndex)
            {
                lock (listMutex)
                {
                    objectList.CopyTo(array, arrayIndex);
                }
            }

            bool ICollection<ObjectType>.IsReadOnly
            {
                get { return false; }
            }

            IEnumerator<ObjectType> IEnumerable<ObjectType>.GetEnumerator()
            {
                return new ArrayEnumerator<ObjectType>(Array);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ArrayEnumerator<ObjectType>(Array);
            }

            int IList.Add(object value)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    return (objectList as IList).Add(value);
                }
            }

            void IList.Clear()
            {
                Clear();
            }

            bool IList.Contains(object value)
            {
                lock (listMutex)
                {
                    return (objectList as IList).Contains(value);
                }
            }

            int IList.IndexOf(object value)
            {
                lock (listMutex)
                {
                    return (objectList as IList).IndexOf(value);
                }
            }

            void IList.Insert(int index, object value)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    (objectList as IList).Insert(index, value);
                }
            }

            bool IList.IsFixedSize
            {
                get { lock (listMutex) { return (objectList as IList).IsFixedSize; } }
            }

            bool IList.IsReadOnly
            {
                get { return false; }
            }

            void IList.Remove(object value)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    (objectList as IList).Remove(value);
                }
            }

            void IList.RemoveAt(int index)
            {
                lock (listMutex)
                {
                    NoteMainListHasBeenChanged();
                    (objectList as IList).RemoveAt(index);
                }
            }

            object IList.this[int index]
            {
                get
                {
                    return this[index];
                }
                set
                {
                    lock (listMutex)
                    {
                        NoteMainListHasBeenChanged();
                        (objectList as IList)[index] = value;
                    }
                }
            }

            void ICollection.CopyTo(Array array, int index)
            {
                lock (listMutex)
                {
                    (objectList as IList).CopyTo(array, index);
                }
            }

            bool ICollection.IsSynchronized
            {
                get { return true; }
            }

            object ICollection.SyncRoot
            {
                get { return listMutex; }
            }

            #endregion
        }

        /// <summary>
        /// This is a helper generic list type collection that supports automatic generation and reuse of an array.  
        /// This cached array is generally used for efficient iterations on the collection's contents when the frequency of the use of such iteration is much greater than the frequency of collection content changes.
        /// Any time the list contents are changed the cached array will be regenerated on next use.  
        /// Thereafter the array will be retained and reused.
        /// <para/>Please NOTE: this list collection object is not thread safe.  
        /// For cases where the collection is accessed from multiple threads please use the LockedObjectListWithCachedArray collection instead.
        /// </summary>
        public class IListWithCachedArray<TItemType> : IList<TItemType>, ICollection<TItemType>, IEnumerable<TItemType>, IEnumerable
        {
            #region Private fields and related private methods

            private List<TItemType> list;
            private TItemType[] _array = null;
            private ReadOnlyIList<TItemType> _readOnlyIList = null;

            /// <summary>Clears all cached copies of this list's contents.  (sets _array and _readOnlyIList to null)</summary>
            private void NoteContentsChanged()
            {
                _array = null;
                _readOnlyIList = null;
            }

            #endregion

            #region Constructor

            /// <summary>Default Constructor.</summary>
            public IListWithCachedArray()
            {
                list = new List<TItemType>();
            }

            /// <summary>Content Constructor.  Caller provides an non-null <paramref name="collection"/> which is used to initialize the contents of this object.</summary>
            public IListWithCachedArray(IEnumerable<TItemType> collection)
            {
                list = new List<TItemType>(collection);
            }

            #endregion

            #region Custom public properties (Array, ReadOnlyIList)

            /// <summary>
            /// If necessary generates an array from the current list contents and returns it, retaining the resulting array instance until the
            /// list contents are next changed.
            /// <para/>Please note: The use pattern supported here generally expects that the client code which obtains this array will NOT
            /// change its contents.  Any failure to follow this expected pattern may cause unexpected behavior in the client code and in this class
            /// (as it uses the resulting array in some cases).
            /// </summary>
            public TItemType[] Array
            {
                get
                {
                    return _array ?? (_array = list.ToArray());
                }
            }

            /// <summary>
            /// If necessary generates a ReadOnlyIList instance fromthe current list contents and returns it, retainging the resulting read only list until this
            /// list's contents are next changed.
            /// </summary>
            public ReadOnlyIList<TItemType> ReadOnlyIList
            {
                get
                {
                    return _readOnlyIList ?? (_readOnlyIList = new ReadOnlyIList<TItemType>(list));
                }
            }

            #endregion

            #region List like methods (AddRange)

            /// <summary>Adds the given <paramref name="collection"/> of objects to this list.</summary>
            public void AddRange(IEnumerable<TItemType> collection)
            {
                list.AddRange(collection);
                NoteContentsChanged();
            }

            /// <summary>
            /// sweeps through the contents evaluating the given <paramref name="match"/> predicate.  Removes all items from the list for which match returns true.
            /// </summary>
            /// <exception cref="System.ArgumentNullException">thrown if the given <paramref name="match"/> is null.</exception>
            public int RemoveAll(Predicate<TItemType> match)
            {
                if (match == null)
                    new System.ArgumentNullException("match").Throw();

                int remoteCount = 0;
                for (int idx = 0; idx < list.Count;)
                {
                    TItemType item = list[idx];
                    if (match(item))
                    {
                        list.RemoveAt(idx);
                        remoteCount++;
                    }
                    else
                    {
                        idx++;
                    }
                }

                if (remoteCount > 0)
                    NoteContentsChanged();

                return remoteCount;
            }

            #endregion

            #region IList<TItemType>

            /// <summary>Attempt to find the given item in this collection and returns its index, or -1 if it was not found.</summary>
            public int IndexOf(TItemType item)
            {
                return list.IndexOf(item);
            }

            /// <summary>Attempts to insert the given <paramref name="item"/> at the given <paramref name="index"/></summary>
            /// <exception cref="System.ArgumentOutOfRangeException">thrown if the given <paramref name="index"/> is not valid for the current collection contents.</exception>
            public void Insert(int index, TItemType item)
            {
                list.Insert(index, item);
                NoteContentsChanged();
            }

            /// <summary>Attempts to remove the item at the given <paramref name="index"/></summary>
            /// <exception cref="System.ArgumentOutOfRangeException">thrown if the given <paramref name="index"/> is not valid for the current collection contents.</exception>
            public void RemoveAt(int index)
            {
                list.RemoveAt(index);
                NoteContentsChanged();
            }

            /// <summary>Attempts to get (or set) the collection item at the given <paramref name="index"/></summary>
            /// <exception cref="System.ArgumentOutOfRangeException">thrown if the given <paramref name="index"/> is not valid for the current collection contents.</exception>
            public TItemType this[int index]
            {
                get
                {
                    return list[index];
                }
                set
                {
                    list[index] = value;
                    NoteContentsChanged();
                }
            }

            /// <summary>Appends the given <paramref name="item"/> to the current collection</summary>
            public void Add(TItemType item)
            {
                list.Add(item);
                NoteContentsChanged();
            }

            /// <summary>Removes all of the items from this collection.</summary>
            public void Clear()
            {
                list.Clear();
                NoteContentsChanged();
            }

            /// <summary>Returns true if this collection currently contains the given <paramref name="item"/>.  Exact behavior is implemented using the underlying List{TItemType}'s Contains method.</summary>
            public bool Contains(TItemType item)
            {
                return list.Contains(item);
            }

            /// <summary>Copies this collection's contents to the given <paramref name="array"/> starting at the given <paramref name="arrayIndex"/>.</summary>
            /// <exception cref="System.ArgumentNullException">thrown if the given <paramref name="array"/> is null.</exception>
            /// <exception cref="System.ArgumentOutOfRangeException">thrown if the given <paramref name="arrayIndex"/> is not valid for the given <paramref name="array"/>.</exception>
            /// <exception cref="System.ArgumentException">thrown if the contents of this collection cannot be copied to the given <paramref name="array"/> at the given <paramref name="arrayIndex"/> offset because the target array length is not large enough.</exception>
            public void CopyTo(TItemType[] array, int arrayIndex)
            {
                list.CopyTo(array, arrayIndex);
            }

            /// <summary>Returns the current collection's item count.</summary>
            public int Count
            {
                get { return list.Count; }
            }

            /// <summary>Returns false</summary>
            public bool IsReadOnly
            {
                get { return false; }
            }

            /// <summary>Attempts to remove the first occurrence of the given <paramref name="item"/> from this collection.  Returns true if an occurrence of the given <paramref name="item"/> was found and removed, or false otherwise.</summary>
            public bool Remove(TItemType item)
            {
                bool wasRemoved = list.Remove(item);

                NoteContentsChanged();

                return wasRemoved;
            }

            #endregion

            #region IEnumerable<TItemType>

            /// <summary>Returns an enumerator for the Array (Uses ArrayEnumerator{TItemType})</summary>
            public IEnumerator<TItemType> GetEnumerator()
            {
                return new ArrayEnumerator<TItemType>(Array);
            }

            #endregion

            #region IEnumerable

            IEnumerator IEnumerable.GetEnumerator()
            {
                return Array.GetEnumerator();
            }

            #endregion
        }

        /// <summary>
        /// This is a helper generic dictionary type collection that supports automatic generation and reuse of an array of the collection's keys and/or of its values.
        /// These cached arrays are generally used for efficient iterations on the collection's contents when the frequency of the use of such iteration is much greater than the frequency of collection content changes.
        /// <para/>Please NOTE: this list collection object is not thread safe.  
        /// </summary>
        public class IDictionaryWithCachedArrays<TKeyType, TValueType> : IDictionary<TKeyType, TValueType>, IEnumerable<KeyValuePair<TKeyType, TValueType>>, IEnumerable
        {
            #region Private fields and related private methods

            private Dictionary<TKeyType, TValueType> dictionary;
            private TKeyType[] _keyArray = null;
            private TValueType[] _valueArray = null;
            private KeyValuePair<TKeyType, TValueType>[] _keyValuePairArray = null;

            /// <summary>Clears all cached copies of this list's contents.  (sets _keyArray, _valueArray, and _keyValuePairArray to null)</summary>
            private void NoteContentsChanged()
            {
                _keyArray = null;
                _valueArray = null;
                _keyValuePairArray = null;
            }

            #endregion

            #region Constructor

            /// <summary>Default constructor.  Caller can provide an optional <paramref name="copyContentsFrom"/> dictionary to be copied and an optional <paramref name="comparer"/> instance to use.</summary>
            public IDictionaryWithCachedArrays(IDictionary<TKeyType, TValueType> copyContentsFrom = null, IEqualityComparer<TKeyType> comparer = null)
            {
                if (copyContentsFrom != null && comparer != null)
                    dictionary = new Dictionary<TKeyType, TValueType>(copyContentsFrom, comparer);
                else if (copyContentsFrom != null)
                    dictionary = new Dictionary<TKeyType, TValueType>(copyContentsFrom);
                else if (comparer != null)
                    dictionary = new Dictionary<TKeyType, TValueType>(comparer);
                else
                    dictionary = new Dictionary<TKeyType, TValueType>();
            }

            #endregion

            #region Custom public properties (KeyArray, ValueArray, KeyValuePairArray)

            /// <summary>
            /// If necessary generates an array from the current collection of key contents and returns it, retaining the resulting array instance until the
            /// collection contents are next changed.
            /// <para/>Please note: The use pattern supported here generally expects that the client code which obtains this array will NOT
            /// change its contents.  Any failure to follow this expected pattern may cause unexpected behavior in the client code and in this class
            /// (as it uses the resulting array in some cases).
            /// </summary>
            public TKeyType[] KeyArray
            {
                get
                {
                    return _keyArray ?? (_keyArray = dictionary.Keys.ToArray());
                }
            }

            /// <summary>
            /// If necessary generates an array from the current collection of value contents and returns it, retaining the resulting array instance until the
            /// collection contents are next changed.
            /// <para/>Please note: The use pattern supported here generally expects that the client code which obtains this array will NOT
            /// change its contents.  Any failure to follow this expected pattern may cause unexpected behavior in the client code and in this class
            /// (as it uses the resulting array in some cases).
            /// </summary>
            public TValueType[] ValueArray
            {
                get
                {
                    return _valueArray ?? (_valueArray = dictionary.Values.ToArray());
                }
            }

            /// <summary>
            /// If necessary generates an array from the current collection of key value pairs and returns it, retaining the resulting array instance until the
            /// collection contents are next changed.
            /// <para/>Please note: The use pattern supported here generally expects that the client code which obtains this array will NOT
            /// change its contents.  Any failure to follow this expected pattern may cause unexpected behavior in the client code and in this class
            /// (as it uses the resulting array in some cases).
            /// </summary>
            public KeyValuePair<TKeyType, TValueType>[] KeyValuePairArray
            {
                get
                {
                    return _keyValuePairArray ?? (_keyValuePairArray = dictionary.ToArray());
                }
            }

            #endregion

            #region IDictionary<TKeyType, TValueType>

            /// <summary>Attempts to add the given <paramref name="value"/> indexed in the collection under the given <paramref name="key"/></summary>
            /// <exception cref="System.ArgumentNullException">thrown if the given <paramref name="key"/> is null.</exception>
            /// <exception cref="System.ArgumentException">thrown if the colletion already contains a value for the given <paramref name="key"/>.</exception>
            public void Add(TKeyType key, TValueType value)
            {
                dictionary.Add(key, value);
                NoteContentsChanged();
            }

            /// <summary>Returns true if the colletion currently contains a value for the given <paramref name="key"/></summary>
            /// <exception cref="System.ArgumentNullException">thrown if the given <paramref name="key"/> is null.</exception>
            public bool ContainsKey(TKeyType key)
            {
                return dictionary.ContainsKey(key);
            }

            /// <summary>Returns a ICollection{TKeyType} for the keys that are currently in this collection</summary>
            public ICollection<TKeyType> Keys
            {
                get { return dictionary.Keys; }
            }

            /// <summary>Attempts to remove the given <paramref name="key"/> from this collection.  Returns true if the <paramref name="key"/> was found was removed, or false otherwise</summary>
            /// <exception cref="System.ArgumentNullException">thrown if the given <paramref name="key"/> is null.</exception>
            public bool Remove(TKeyType key)
            {
                bool wasRemoved = dictionary.Remove(key);

                if (wasRemoved)
                    NoteContentsChanged();

                return wasRemoved;
            }

            /// <summary>Attempts to find and produce (out) the <paramref name="value"/> for the given <paramref name="key"/>.  Returns true if the <paramref name="key"/> was found.  Returns false if the <paramref name="key"/> was not found, in which case the <paramref name="value"/> will have been set to the default value for <typeparamref name="TValueType"/></summary>
            /// <exception cref="System.ArgumentNullException">thrown if the given <paramref name="key"/> is null.</exception>
            public bool TryGetValue(TKeyType key, out TValueType value)
            {
                return dictionary.TryGetValue(key, out value);
            }

            /// <summary>Returns a ICollection{TValueType} for the values that are currently in this collection</summary>
            public ICollection<TValueType> Values
            {
                get { return dictionary.Values; }
            }

            /// <summary>Attempts to get (or set) the collection's value for the given <paramref name="key"/></summary>
            /// <exception cref="System.ArgumentNullException">thrown if the given <paramref name="key"/> is null.</exception>
            /// <exception cref="System.Collections.Generic.KeyNotFoundException">thrown by the getter if the collection does not currently contains a value for the given <paramref name="key"/>.</exception>
            public TValueType this[TKeyType key]
            {
                get
                {
                    return dictionary[key];
                }
                set
                {
                    dictionary[key] = value;
                    NoteContentsChanged();
                }
            }

            /// <summary>Removes all of the items from this collection.</summary>
            public void Clear()
            {
                dictionary.Clear();
                NoteContentsChanged();
            }

            /// <summary>Returns the number of key/value pairs currently in this collection</summary>
            public int Count
            {
                get { return dictionary.Count; }
            }

            /// <summary>Returns false</summary>
            public bool IsReadOnly
            {
                get { return false; }
            }

            #endregion

            #region ICollection<KeyValuePair<TKeyType, TValueType>>

            void ICollection<KeyValuePair<TKeyType, TValueType>>.Add(KeyValuePair<TKeyType, TValueType> item)
            {
                ((ICollection<KeyValuePair<TKeyType, TValueType>>)dictionary).Add(item);
                NoteContentsChanged();
            }

            bool ICollection<KeyValuePair<TKeyType, TValueType>>.Contains(KeyValuePair<TKeyType, TValueType> item)
            {
                return ((ICollection<KeyValuePair<TKeyType, TValueType>>)dictionary).Contains(item);
            }

            void ICollection<KeyValuePair<TKeyType, TValueType>>.CopyTo(KeyValuePair<TKeyType, TValueType>[] array, int arrayIndex)
            {
                ((ICollection<KeyValuePair<TKeyType, TValueType>>)dictionary).CopyTo(array, arrayIndex);
            }

            bool ICollection<KeyValuePair<TKeyType, TValueType>>.Remove(KeyValuePair<TKeyType, TValueType> item)
            {
                bool wasRemoved = ((ICollection<KeyValuePair<TKeyType, TValueType>>)dictionary).Remove(item);

                if (wasRemoved)
                    NoteContentsChanged();

                return wasRemoved;
            }

            #endregion

            #region IEnumerable<KeyValuePair<TKeyType, TValueType>>

            /// <summary>Returns an enumerator for the KeyValuePairArray (Uses ArrayEnumerator{KeyValuePair{TKeyType, TValueType}})</summary>
            public IEnumerator<KeyValuePair<TKeyType, TValueType>> GetEnumerator()
            {
                return new ArrayEnumerator<KeyValuePair<TKeyType, TValueType>>(KeyValuePairArray);
            }

            #endregion

            #region IEnumerator

            IEnumerator IEnumerable.GetEnumerator()
            {
                return KeyValuePairArray.GetEnumerator();
            }

            #endregion
        }

        #endregion

        #region TokenSet

        /// <summary>
        /// An ITokenSet{TItemType} is a form of ICollection{TItemType} that supports RW and RO rendering, and supports slightly optimized cloning and Contains methods by directly containing the first few tokens in the object and only
        /// adding an actual list of tokens after the set contains more than the fixed base number.
        /// </summary>
        public interface ITokenSet<TItemType> : ICollection<TItemType>, IEquatable<ITokenSet<TItemType>>
        {
            /// <summary>
            /// Returns true if the set contains no tokens
            /// </summary>
            bool IsEmpty { get; }

            /// <summary>
            /// Returns true if the set contains at least one token
            /// </summary>
            bool IsNotEmpty { get; }
        }

        /// <summary>
        /// A TokenSet{<typeparamref name="TItemType"/>} is a form of ICollection{<typeparamref name="TItemType"/>} that supports RW and RO rendering, along with a slighly optimized behavior for cloning and the Contains method.
        /// It internally makes use of a small number of fixed token locations that overflow into the explicit use of a list which is only allocated when needed.
        /// <para/>Currently this set is optimized for between 0 and 3 tokens.
        /// <para/>This class implicilty uses default(<typeparamref name="TItemType"/>) to indicate when a token position is empty.
        /// As such you cannot Add default values to the set.
        /// </summary>
        [DataContract(Namespace = MosaicLib.Constants.MosaicLibNameSpaceRoot)]
        public class TokenSet<TItemType> : ITokenSet<TItemType>
        {
            private static readonly EqualityComparer<TItemType> defaultEqualityComparer = EqualityComparer<TItemType>.Default;
            private static bool Equals(TItemType a, TItemType b) { return defaultEqualityComparer.Equals(a, b); }
            private static bool IsDefault(TItemType item) { return Equals(item, default(TItemType)); }

            /// <summary>
            /// This property returns the sigleton Empty, readonly TokenSet{<typeparamref name="TItemType"/>}.
            /// </summary>
            public static TokenSet<TItemType> Empty { get { return _empty; } }
            private static TokenSet<TItemType> _empty = new TokenSet<TItemType>(asReadOnly: true);

            /// <summary>Constructs a new TokenSet with the given <paramref name="firstItem"/>, and optional <paramref name="moreItemParamsArray"/> item parameters</summary>
            public TokenSet(TItemType firstItem, params TItemType[] moreItemParamsArray)
            {
                token1 = firstItem;
                token2 = moreItemParamsArray.SafeAccess(0);
                token3 = moreItemParamsArray.SafeAccess(1);

                if (moreItemParamsArray.SafeLength() > 2)
                    moreTokens = new List<TItemType>(moreItemParamsArray.Skip(2).WhereIsNotDefault());
            }

            /// <summary>Constructs a new TokenSet from the given <paramref name="otherSet"/> ICollection{<typeparamref name="TItemType"/>}, or the empty set if <paramref name="otherSet"/> is null</summary>
            public TokenSet(ICollection<TItemType> otherSet, bool asReadOnly = false)
            {
                if (otherSet != null)
                {
                    var otherArray = otherSet.SafeToArray();

                    token1 = otherArray.SafeAccess(0);
                    token2 = otherArray.SafeAccess(1);
                    token3 = otherArray.SafeAccess(2);

                    if (otherArray.SafeLength() > 3)
                        moreTokens = new List<TItemType>(otherArray.Skip(3));
                }

                if (asReadOnly)
                    IsReadOnly = true;
            }

            /// <summary>Constructs a new TokenSet from the given <paramref name="otherSet"/>, or the empty set if <paramref name="otherSet"/> is null</summary>
            public TokenSet(TokenSet<TItemType> otherSet = null, bool asReadOnly = false)
            {
                if (otherSet != null)
                {
                    token1 = otherSet.token1;
                    token2 = otherSet.token2;
                    token3 = otherSet.token3;

                    if (!otherSet.moreTokens.IsNullOrEmpty())
                        moreTokens = new List<TItemType>(otherSet.moreTokens);
                }

                if (asReadOnly)
                    IsReadOnly = true;
            }

            /// <summary>
            /// Returns true if the set contains no tokens
            /// </summary>
            public bool IsEmpty { get { return Count == 0; } }

            /// <summary>
            /// Returns true if the set contains at least one token
            /// </summary>
            public bool IsNotEmpty { get { return Count >= 1; } }

            /// <summary>Returns the count of the number of tokens in the set</summary>
            public int Count
            {
                get
                {
                    return ((!IsDefault(token1)).MapToInt()
                            + (!IsDefault(token2)).MapToInt()
                            + (!IsDefault(token3)).MapToInt()
                            + moreTokens.SafeCount()
                            );
                }
            }

            /// <summary>Getter returns true if the list is read only.  Setter may be used to set the list to be read-only, but may not be used to set a read/only set to be read/write</summary>
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

            /// <summary>Adds the given <paramref name="item"/> to this set.  This value is added into the first available local token location or it is added to the backing list if the local token locations are all non-default.</summary>
            /// <exception cref="System.NotSupportedException">Thrown if this method is called on a IsReadyOnly instance</exception>
            public void Add(TItemType item)
            {
                ThrowIfIsReadOnly("Add");

                if (IsDefault(item))
                    return;

                if (IsDefault(token1))
                    token1 = item;
                else if (IsDefault(token2))
                    token2 = item;
                else if (IsDefault(token3))
                    token3 = item;
                else if (moreTokens == null)
                    moreTokens = new List<TItemType>() { item };
                else
                    moreTokens.Add(item);
            }

            /// <summary>Removes all of the added tokens from the set</summary>
            /// <exception cref="System.NotSupportedException">Thrown if this method is called on a IsReadyOnly instance</exception>
            public void Clear()
            {
                ThrowIfIsReadOnly("Clear");

                token1 = default(TItemType);
                token2 = default(TItemType);
                token3 = default(TItemType);
                moreTokens = null;
            }

            /// <summary>
            /// Returns true if the set currently contains the given item (using object.Equals based equality comparison)
            /// </summary>
            public bool Contains(TItemType item)
            {
                if (IsDefault(item))
                    return false;

                return (Equals(token1, item)
                        || Equals(token2, item)
                        || Equals(token3, item)
                        || moreTokens.SafeContains(item)
                        );
            }

            /// <summary>
            /// Copies the set's contents into the given <paramref name="array"/> starting at the given <paramref name="arrayIndex"/>.
            /// </summary>
            public void CopyTo(TItemType[] array, int arrayIndex)
            {
                if (!IsDefault(token1))
                    array[arrayIndex++] = token1;

                if (!IsDefault(token2))
                    array[arrayIndex++] = token2;

                if (!IsDefault(token3))
                    array[arrayIndex++] = token3;

                if (!moreTokens.IsNullOrEmpty())
                    moreTokens.CopyTo(array, arrayIndex);
            }

            /// <summary>
            /// Attempts to remove the first occurrence of the given <paramref name="item"/> if found in the set.
            /// Returns true if an occurrence was found and removed, or false if no such occurrence was found.
            /// </summary>
            /// <exception cref="System.NotSupportedException">Thrown if this method is called on a IsReadyOnly instance</exception>
            public bool Remove(TItemType item)
            {
                ThrowIfIsReadOnly("Remove");

                if (IsDefault(item))
                    return false;

                if (Equals(token1, item))
                {
                    token1 = default(TItemType);
                    return true;
                }
                else if (Equals(token2, item))
                {
                    token2 = default(TItemType);
                    return true;
                }
                else if (Equals(token3, item))
                {
                    token3 = default(TItemType);
                    return true;
                }
                else if (moreTokens != null)
                {
                    return moreTokens.Remove(item);
                }

                return false;
            }

            public IEnumerable<TItemType> GetEnumerable()
            {
                return (EmptyArrayFactory<TItemType>.Instance
                        .ConditionalConcatItems(!IsDefault(token1), token1)
                        .ConditionalConcatItems(!IsDefault(token2), token2)
                        .ConditionalConcatItems(!IsDefault(token3), token3)
                        .ConditionalConcatItems(!moreTokens.IsNullOrEmpty(), moreTokens)
                        );
            }

            public IEnumerator<TItemType> GetEnumerator()
            {
                return GetEnumerable().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            /// <summary>
            /// Determine if this TokenSet has the same set of tokens (by object.Equals equality) as the given <paramref name="other"/> set contains.  Ignores the two sets IsReadOnly values.
            /// </summary>
            public bool Equals(ITokenSet<TItemType> other)
            {
                return this.Equals(other, compareReadOnly: false);
            }

            /// <summary>
            /// Determine if this TokenSet has the same set of tokens (by object.Equals equality) as the given <paramref name="other"/> set contains.  Optinally compares the two sets IsReadOnly values for equality.
            /// </summary>
            public bool Equals(ITokenSet<TItemType> other, bool compareReadOnly)
            {
                if (other == null)
                    return false;

                if (compareReadOnly && (isReadOnly != other.IsReadOnly))
                    return false;

                TokenSet<TItemType> otherAsTS = other as TokenSet<TItemType>;

                // if this set has matching occupied local token values and both sets do not have moreTokens lists then return true
                if (otherAsTS != null && Equals(token1, otherAsTS.token1) && Equals(token2, otherAsTS.token2) && Equals(token3, otherAsTS.token3) && moreTokens == null && otherAsTS.moreTokens == null)
                    return true;

                return this.SequenceEqual(other);
            }

            private bool isReadOnly;

            [DataMember(Order = 1000, IsRequired = false, EmitDefaultValue = false)]
            private TItemType token1;

            [DataMember(Order = 2000, IsRequired = false, EmitDefaultValue = false)]
            private TItemType token2;

            [DataMember(Order = 3000, IsRequired = false, EmitDefaultValue = false)]
            private TItemType token3;

            private List<TItemType> moreTokens;

            [DataMember(Order = 4000, Name = "moreTokens", IsRequired = false, EmitDefaultValue = false)]
            private TItemType[] MoreTokensArray
            {
                get { return moreTokens.SafeToArray(mapNullToEmpty: false).MapEmptyToNull(); }
                set { moreTokens = (value.IsNullOrEmpty() ? null : new List<TItemType>(value)); }
            }

            internal TItemType Token1 { get { return token1; } }
            internal TItemType Token2 { get { return token2; } }
            internal TItemType Token3 { get { return token3; } }
            internal IEnumerable<TItemType> MoreTokens { get { return moreTokens; } }

            public override string ToString()
            {
                if (Count == 0)
                    return "TokenSet:[Empty]";
                else
                    return "TokenSet:[{0}]".CheckedFormat(string.Join(", ", this.Select(item => item.SafeToString())));
            }

            /// <summary>
            /// All deserialized TokenSets are immedaitely set to be ReadOnly
            /// </summary>
            [OnDeserialized]
            private void OnDeserialized(StreamingContext context)
            {
                isReadOnly = true;
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
        }

        #endregion
    }

    /// <summary>
    /// static class of static methods that are used with KeyValuePair instances.
    /// </summary>
    public static class KVP
    {
        /// <summary>Typed KeyValuePair construction helper method</summary>
        public static KeyValuePair<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value) { return new KeyValuePair<TKey, TValue>(key, value); }
    }

    public static partial class ExtensionMethods
    {
        #region ReadOnlyIList<TItemType>, IList<TItemType>, and IEnumerable<TItemType> methods (ConvertToReadOnly, ConvertToWritable)

        /// <summary>
        /// Extension method either returns the given <paramref name="iSetIn"/> (if it is already a ReadOnlyIList instance) or returns a new ReadOnlyIList{TItemType} of the given <paramref name="iSetIn"/>, or null if the given <paramref name="iSetIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static ReadOnlyIList<TItemType> ConvertToReadOnly<TItemType>(this IEnumerable<TItemType> iSetIn, bool mapNullToEmpty = true)
        {
            ReadOnlyIList<TItemType> roIList = iSetIn as ReadOnlyIList<TItemType>;

            if (roIList != null)
                return roIList;

            if (iSetIn == null)
                return mapNullToEmpty ? ReadOnlyIList<TItemType>.Empty : null;

            return new ReadOnlyIList<TItemType>(iSetIn);
        }

        // NOTE: supporting ConvertToReadOnly for ICollection appears to produce a possible conflict with the corresonding version that can be applied to INamedValueSet instances.

        /// <summary>
        /// Extension method either returns the given <paramref name="iListIn"/> (if it is already a ReadOnlyIList instance) or returns a new ReadOnlyIList{TItemType} of the given <paramref name="iListIn"/>, or null if the given <paramref name="iListIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static ReadOnlyIList<TItemType> ConvertToReadOnly<TItemType>(this IList<TItemType> iListIn, bool mapNullToEmpty = true)
        {
            ReadOnlyIList<TItemType> roIList = iListIn as ReadOnlyIList<TItemType>;

            if (roIList != null)
                return roIList;

            if (iListIn == null && !mapNullToEmpty)
                return null;

            if (iListIn.IsNullOrEmpty())
                return ReadOnlyIList<TItemType>.Empty;

            return new ReadOnlyIList<TItemType>(iListIn);
        }

        /// <summary>
        /// Extension method either returns the given <paramref name="iListIn"/> (if it is not IsReadOnly) or returns a new List{TItemType} of the given <paramref name="iListIn"/> is non-empty, or null if the given <paramref name="iListIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static IList<TItemType> ConvertToWritable<TItemType>(this IList<TItemType> iListIn, bool mapNullToEmpty = true)
        {
            if (iListIn != null)
                return !iListIn.IsReadOnly ? iListIn : new List<TItemType>(iListIn);
            else
                return mapNullToEmpty ? new List<TItemType>() : null;
        }

        #endregion

        #region ReadOnlyIList<TItemType> methods (MapNullToEmpty, MapEmptyToNull)

        /// <summary>
        /// Extension method either returns the given <paramref name="readOnlyIListIn"/> (if it is not null) or returns an empty ReadOnlyIList{TItemType} if the given <paramref name="readOnlyIListIn"/> is null.
        /// </summary>
        public static ReadOnlyIList<TItemType> MapNullToEmpty<TItemType>(this ReadOnlyIList<TItemType> readOnlyIListIn)
        {
            return readOnlyIListIn ?? ReadOnlyIList<TItemType>.Empty;
        }

        /// <summary>
        /// Extension method returns the given <paramref name="readOnlyIListIn"/> is non-empty otherwise this method returns null.
        /// </summary>
        public static ReadOnlyIList<TItemType> MapEmptyToNull<TItemType>(this ReadOnlyIList<TItemType> readOnlyIListIn)
        {
            return (readOnlyIListIn.SafeCount() != 0) ? readOnlyIListIn : null;
        }

        #endregion

        #region IDictionary<TKey, TValue>, ReadOnlyIDictionary<TKey,TValue> (ConvertToReadOnly, ConvertToWritable)

        /// <summary>
        /// Extension method either returns the given <paramref name="iDictionaryIn"/> (if it is already a ReadOnlyIDictionary instance) or returns a new ReadOnlyIDictionary{TKey, TValue} of the given <paramref name="iDictionaryIn"/>, or null if the given <paramref name="iDictionaryIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static ReadOnlyIDictionary<TKey, TValue> ConvertToReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> iDictionaryIn, bool mapNullToEmpty = true)
        {
            ReadOnlyIDictionary<TKey, TValue> roIDictionary = iDictionaryIn as ReadOnlyIDictionary<TKey, TValue>;

            if (roIDictionary != null)
                return roIDictionary;

            if (iDictionaryIn == null && !mapNullToEmpty)
                return null;

            if (iDictionaryIn == null || iDictionaryIn.Count <= 0)
                return ReadOnlyIDictionary<TKey, TValue>.Empty;

            return new ReadOnlyIDictionary<TKey, TValue>(iDictionaryIn);
        }

        /// <summary>
        /// Extension method either returns the given <paramref name="iDictionaryIn"/> (if it is not IsReadOnly) or returns a new Dictionary{TKey, TValue} of the given <paramref name="iDictionaryIn"/> is non-empty, or null if the given <paramref name="iDictionaryIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static IDictionary<TKey, TValue> ConvertToWritable<TKey, TValue>(this IDictionary<TKey, TValue> iDictionaryIn, bool mapNullToEmpty = true)
        {
            if (iDictionaryIn != null)
                return !iDictionaryIn.IsReadOnly ? iDictionaryIn : new Dictionary<TKey, TValue>(iDictionaryIn);
            else
                return mapNullToEmpty ? new Dictionary<TKey, TValue>() : null;
        }

        #endregion

        #region ReadOnlyIDictionary<TKey,TValue> methods (MapNullToEmpty, MapEmptyToNull)

        /// <summary>
        /// Extension method either returns the given <paramref name="readOnlyIDictionaryIn"/> (if it is not null) or returns an empty ReadOnlyIDictionary{TKey, TValue} if the given <paramref name="readOnlyIDictionaryIn"/> is null.
        /// </summary>
        public static ReadOnlyIDictionary<TKey, TValue> MapNullToEmpty<TKey, TValue>(this ReadOnlyIDictionary<TKey, TValue> readOnlyIDictionaryIn)
        {
            return readOnlyIDictionaryIn ?? ReadOnlyIDictionary<TKey, TValue>.Empty;
        }

        /// <summary>
        /// Extension method returns the given <paramref name="readOnlyIDictionaryIn"/> is non-empty otherwise this method returns null.
        /// </summary>
        public static ReadOnlyIDictionary<TKey, TValue> MapEmptyToNull<TKey, TValue>(this ReadOnlyIDictionary<TKey, TValue> readOnlyIDictionaryIn)
        {
            return (readOnlyIDictionaryIn.SafeCount() != 0) ? readOnlyIDictionaryIn : null;
        }

        #endregion

        #region IDictionary<TKey, TValue> (SafeAddItems, SafeAddRange, SafeSetValueForKey)

        /// <summary>Adds (using SafeSetKeyValue) the given <paramref name="firstKVP"/> and any following <paramref name="moreKVPItemsArray"/> to the given <paramref name="dictionary"/> and returns it (to support call chaining)</summary>
        public static TDictionary SafeAddItems<TDictionary, TKey, TValue>(this TDictionary dictionary, KeyValuePair<TKey, TValue> firstKVP, params KeyValuePair<TKey, TValue>[] moreKVPItemsArray)
            where TDictionary : IDictionary<TKey, TValue>
        {
            dictionary.SafeSetKeyValue(firstKVP);

            if (!moreKVPItemsArray.IsNullOrEmpty())
                moreKVPItemsArray.DoForEach(item => dictionary.SafeSetKeyValue(item));

            return dictionary;
        }

        /// <summary>Adds (using SafeSetKeyValue) the given <paramref name="itemSet"/> set of items to the given <paramref name="dictionary"/> and returns it (to support call chaining)</summary>
        public static TDictionary SafeAddRange<TDictionary, TKey, TValue>(this TDictionary dictionary, IEnumerable<KeyValuePair<TKey, TValue>> itemSet)
            where TDictionary : IDictionary<TKey, TValue>
        {
            if (itemSet != null && dictionary != null)
                itemSet.DoForEach(item => dictionary.SafeSetKeyValue(item));

            return dictionary;
        }

        /// <summary>If the given <paramref name="dictionary"/> is non-null and the given <paramref name="kvp"/>'s Key is non-null then uses the TKey indexed setter to set the key's value in the dictionary.  Returns the given <paramref name="dictionary"/> to support call chaining.</summary>
        public static TDictionary SafeSetKeyValue<TDictionary, TKey, TValue>(this TDictionary dictionary, KeyValuePair<TKey, TValue> kvp)
            where TDictionary : IDictionary<TKey, TValue>
        {
            if (dictionary != null && kvp.Key != null)
                dictionary[kvp.Key] = kvp.Value;

            return dictionary;
        }

        #endregion

        #region ITokenSet<TItemType> (MapNullToEmpty, MapEmtpyToNull, ConverToReadOnly, ConvertToWritable)

        /// <summary>
        /// Extension method either returns the given <paramref name="setIn"/> (if it is not null) or returns an empty ITokenSet{TItemType} if the given <paramref name="setIn"/> is null.
        /// </summary>
        public static ITokenSet<TItemType> MapNullToEmpty<TItemType>(this ITokenSet<TItemType> setIn)
        {
            return setIn ?? TokenSet<TItemType>.Empty;
        }

        /// <summary>
        /// Extension method returns the given <paramref name="setIn"/> is non-empty otherwise this method returns null.
        /// </summary>
        public static ITokenSet<TItemType> MapEmptyToNull<TItemType>(this ITokenSet<TItemType> setIn)
        {
            return (setIn.SafeCount() != 0) ? setIn : null;
        }

        /// <summary>
        /// Extension method either returns the given <paramref name="iSetIn"/> (if it is already a TokenSet instance) or returns a new TokenSet{TItemType} of the given <paramref name="iSetIn"/>, or null if the given <paramref name="iSetIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static TokenSet<TItemType> ConvertToReadOnly<TItemType>(this ITokenSet<TItemType> iSetIn, bool mapNullToEmpty = true)
        {
            TokenSet<TItemType> set = iSetIn as TokenSet<TItemType>;

            if (set != null && set.IsReadOnly)
                return set;

            if (iSetIn == null && !mapNullToEmpty)
                return null;

            if (iSetIn.IsNullOrEmpty())
                return TokenSet<TItemType>.Empty;

            return new TokenSet<TItemType>(iSetIn, asReadOnly: true);
        }

        /// <summary>
        /// Extension method either returns the given <paramref name="iSetIn"/> (if it is not IsReadOnly) or returns a new TokenSet{TItemType} of the given <paramref name="iSetIn"/> is non-empty, or null if the given <paramref name="iSetIn"/> is null and <paramref name="mapNullToEmpty"/> is false.
        /// </summary>
        public static TokenSet<TItemType> ConvertToWritable<TItemType>(this ITokenSet<TItemType> iSetIn, bool mapNullToEmpty = true)
        {
            TokenSet<TItemType> set = iSetIn as TokenSet<TItemType>;

            if (set != null && !set.IsReadOnly)
                return set;

            if (iSetIn != null)
                return new TokenSet<TItemType>(iSetIn);
            else
                return mapNullToEmpty ? new TokenSet<TItemType>() : null;
        }

        /// <summary>
        /// Determine the given TokenSet (<paramref name="value"/>) has the same set of tokens (by object.Equals equality) as the given <paramref name="other"/> set contains.  
        /// Optinally compares the two sets IsReadOnly values for equality.
        /// </summary>
        public static bool Equals<TItemType>(this ITokenSet<TItemType> value, ITokenSet<TItemType> other, bool compareReadOnly)
        {
            if (Object.ReferenceEquals(value, other))
                return true;

            if (value == null || other == null)
                return false;

            if (compareReadOnly && (value.IsReadOnly != other.IsReadOnly))
                return false;

            TokenSet<TItemType> valueAsTS = value as TokenSet<TItemType>;
            TokenSet<TItemType> otherAsTS = other as TokenSet<TItemType>;

            // if this set has matching occupied local token values and both sets do not have moreTokens lists then return true
            if (valueAsTS != null && otherAsTS != null && Equals(valueAsTS.Token1, otherAsTS.Token1) && Equals(valueAsTS.Token2, otherAsTS.Token2) && Equals(valueAsTS.Token3, otherAsTS.Token3) && valueAsTS.MoreTokens == null && otherAsTS.MoreTokens == null)
                return true;

            return value.SequenceEqual(other);
        }
        #endregion
    }

    namespace Collections.Trees
    {
        public class Tree<TItemType, TKeyPathItemType>
            where TKeyPathItemType : IEquatable<TKeyPathItemType>
        {
            public TreeNode<TItemType, TKeyPathItemType> RootNode { get; private set; }

            public IDictionary<TKeyPathItemType[], TreeNode<TItemType, TKeyPathItemType>> NodeDictionary { get; private set; }

            public Tree(IEnumerable<TItemType> itemSet, Func<TItemType, TKeyPathItemType[]> keyPathSelector, DuplicatePathBehavior duplicatePathBehavior = DuplicatePathBehavior.ReplaceItem)
            {
                bool throwOnDuplicate = duplicatePathBehavior == DuplicatePathBehavior.ThrowDuplicatePathException;
                bool replaceOnDuplicate = duplicatePathBehavior == DuplicatePathBehavior.ReplaceItem;

                Dictionary<TKeyPathItemType[], TreeNode<TItemType, TKeyPathItemType>> treeNodeDictionary = new Dictionary<TKeyPathItemType[], TreeNode<TItemType, TKeyPathItemType>>(comparer: keyPathEqualityComparer);

                foreach (var item in itemSet)
                {
                    TKeyPathItemType[] keyPathArray = keyPathSelector(item) ?? Utils.Collections.EmptyArrayFactory<TKeyPathItemType>.Instance;

                    TreeNode<TItemType, TKeyPathItemType> currentNode = null;

                    if (treeNodeDictionary.TryGetValue(keyPathArray, out currentNode) && currentNode != null && currentNode.Item != null && throwOnDuplicate)
                        new DuplicatePathException("Key path [{0}] already found while building tree".CheckedFormat(keyPathArray.ToString(separator: " "))).Throw();

                    if (currentNode == null)
                    {
                        currentNode = new TreeNode<TItemType, TKeyPathItemType> { Item = item, KeyPathArray = keyPathArray, IsRootNode = keyPathArray.IsNullOrEmpty() };
                        treeNodeDictionary[keyPathArray] = currentNode;

                        var parentNode = currentNode.TryToFillInAndReturnParentNode(treeNodeDictionary);

                        if (parentNode != null)
                            parentNode.subNodeList.Add(currentNode);
                    }
                    else if (replaceOnDuplicate)
                    {
                        currentNode.Item = item;
                    }
                }

                NodeDictionary = treeNodeDictionary;
                RootNode = treeNodeDictionary.SafeTryGetValue(Utils.Collections.EmptyArrayFactory<TKeyPathItemType>.Instance);

                foreach (var node in treeNodeDictionary.Values)
                {
                    node.SubNodeArray = node.subNodeList.ToArray();
                    node.subNodeList = null;
                }
            }

            public static readonly KeyPathEqualityComparer keyPathEqualityComparer = new KeyPathEqualityComparer();

            public class KeyPathEqualityComparer : IEqualityComparer<TKeyPathItemType[]>
            {
                public bool Equals(TKeyPathItemType[] arrayX, TKeyPathItemType[] arrayY)
                {
                    return arrayX.IsEqualTo(arrayY);
                }

                public int GetHashCode(TKeyPathItemType[] array)
                {
                    int length = array.SafeLength();
                    int hash = (array == null) ? -1 : 1 + length;

                    for (int idx = 0; idx < length; idx++)
                    {
                        var item = array[idx];
                        int itemHash = (item != null) ? item.GetHashCode() : 0x55aa55aa;

                        hash = (hash << 5) ^ hash ^ itemHash;
                    }

                    return hash;
                }
            }

        }

        public enum DuplicatePathBehavior
        {
            ReplaceItem = 0,
            ThrowDuplicatePathException,
            IgnoreItem,
        }

        public class DuplicatePathException : System.Exception
        {
            public DuplicatePathException(string message, System.Exception innerException = null)
                : base(message, innerException)
            { }
        }

        public class TreeNode<TItemType, TKeyPathItemType>
        {
            public bool IsRootNode { get; internal set; }
            public TreeNode<TItemType, TKeyPathItemType> ParentNode { get; internal set; }

            public TItemType Item { get; internal set; }

            public TKeyPathItemType[] KeyPathArray { get; internal set; }

            public TreeNode<TItemType, TKeyPathItemType>[] SubNodeArray { get; internal set; }

            internal List<TreeNode<TItemType, TKeyPathItemType>> subNodeList = new List<TreeNode<TItemType, TKeyPathItemType>>();

            public override string ToString()
            {
                return "TreeNode: keyPath:[{0}] item:[{1}]{2}".CheckedFormat(string.Join(" ", KeyPathArray), Item, IsRootNode ? " Root" : "");
            }
        }

        public static partial class ExtensionMethods
        {
            public static TreeNode<TItemType, TKeyPathItemType> TryToFillInAndReturnParentNode<TItemType, TKeyPathItemType>(this TreeNode<TItemType, TKeyPathItemType> treeNode, Dictionary<TKeyPathItemType[], TreeNode<TItemType, TKeyPathItemType>> treeNodeDictionary)
            {
                var currentNode = treeNode;

                while (currentNode.ParentNode == null && !currentNode.IsRootNode)
                {
                    int currentNodeKeyPathLength = currentNode.KeyPathArray.Length;
                    if (currentNodeKeyPathLength == 0)
                        break;

                    TKeyPathItemType[] parentNodeKeyPath = treeNode.KeyPathArray.SafeSubArray(0, currentNodeKeyPathLength - 1);

                    TreeNode<TItemType, TKeyPathItemType> parentNode = null;

                    if (!treeNodeDictionary.TryGetValue(parentNodeKeyPath, out parentNode) || parentNode == null)
                    {
                        parentNode = new TreeNode<TItemType, TKeyPathItemType>() { KeyPathArray = parentNodeKeyPath, IsRootNode = parentNodeKeyPath.IsNullOrEmpty() };
                        treeNodeDictionary[parentNodeKeyPath] = parentNode;
                    }

                    currentNode.ParentNode = parentNode;

                    currentNode = parentNode;
                }

                return treeNode.ParentNode;
            }

            public static string ToString<TKeyPathItemType>(this TKeyPathItemType[] keyPathArray, string separator = " ")
            {
                return string.Join(separator, keyPathArray.Select(keyPathItem => keyPathItem.SafeToString()));
            }
        }
    }

    //-------------------------------------------------

    #region Obsolete LockedObjectListWithCachedArray and LockedDelegateListBase variants that are still being retained outside of the Collections sub-namespace

    /// <summary>
    /// Please replace current use with the equivalent, relocated, MosaicLib.Utils.Collections.LockedObjectListWithCachedArray class
    /// </summary>
    [Obsolete("Please replace current use with the equivalent, relocated, MosaicLib.Utils.Collections.LockedObjectListWithCachedArray class.  (2013-04-02)")]
    public class LockedObjectListWithCachedArray<ObjectType> : Collections.LockedObjectListWithCachedArray<ObjectType>
    {
        /// <summary>
        /// Default Constructor: Please replace current use with the equivalent, relocated, MosaicLib.Utils.Collections.LockedObjectListWithCachedArray class
        /// </summary>
        public LockedObjectListWithCachedArray() { }
    }

    /// <summary>
    /// Provides a thread safe container for storing a set of delegates that can be invoked without locking.
    /// This class is a synonym for the LockedObjectListWithCachedArray templatized class.
    /// </summary>  
    /// <remarks>
    /// Based on the use of a locked list of the delegates and a volatile handle to an array of delegates that is (re)obtained from the
    /// list when needed
    /// </remarks>
    [Obsolete("Please replace current use with the new MosaicLib.Utils.Collections.LockedObjectListWithCachedArray type.  (2013-04-02)")]
    public class LockedDelegateListBase<DelegateType> : Collections.LockedObjectListWithCachedArray<DelegateType>
    {
        /// <summary>
        /// Adds the given delegate instance to the list and triggers the Array copy to be rebuilt.  
        /// Re-entrant and thread safe using leaf lock on list contents.
        /// </summary>
        protected new void Add(DelegateType d)
        {
            base.Add(d);
        }

        /// <summary>
        /// Removes the given delegate instance from the list and triggers the Array copy to be rebuilt.  
        /// Re-entrant and thread safe using leaf lock on list contents.
        /// </summary>
        protected new void Remove(DelegateType d)
        {
            base.Remove(d);
        }

        /// <summary>
        /// Returns the most recently generated copy of the Array version of the underlying list of delegates.  Will return a fixed empty array when the list is empty.
        /// Implementation guarantees that returned value will include effects of any change made to the list by the thread that is requesting this array.
        /// Changes made by other threads produce a race condition where the side effects of the change on another thread will not be visible in the array contents
        /// until the thread reading this property invokes it entirely after another thread in question's Add or Remove method has returned from that method invocation.
        /// This method does not attempt to lock or update the underlying Array value unless it knows that at least one change has been completed to the list contents.
        /// </summary>
        /// <remarks>
        /// If any change to the list has been recorded via the rebuild flag then this property will lock access to the list, 
        /// generate the array version of it and then retain the Array version for later requests until the list contents have been changed again.
        /// Use of locked access to list during rebuild prevents the risk that the list may change contents while the rebuild is taking place.
        /// </remarks>
        protected new DelegateType[] Array
        {
            get { return base.Array; }
        }
    }

    #endregion

    //-------------------------------------------------

    #region Shared resource use related pseudo collections

    namespace Collections
    {
        /// <summary>
        /// This class is a form of container class that uses client visible Token objects to allow multiple clients to collaborate in setting up use of some common
        /// resource when the first client creates the corresponding token and implements automatic release of the shared resource when the last client disposes
        /// of the last token.
        /// <para/>This is an abstract class that must be subclased by a class that implements the abstract Setup and Release methods.
        /// </summary>
        /// <remarks>
        /// This is expected to be especially useful for situations where multiple independent assemblies may need to support use of shared resources and resource setup like logging.
        /// </remarks>
        public abstract class SharedResourceSetupAndReleaseBase : SharedResourceSetupAndReleaseBase<IDisposable, Details.SharedResourceTokenBase>
        { }

        /// <summary>
        /// This class is a form of container class that uses client visible Token objects to allow multiple clients to collaborate in setting up use of some common
        /// resource when the first client creates the corresponding token and implements automatic release of the shared resource when the last client disposes
        /// of the last token.
        /// <para/>This is an abstract class that must be subclased by a class that implements the abstract Setup and Release methods.
        /// </summary>
        /// <remarks>
        /// This is expected to be especially useful for situations where multiple independent assemblies may need to support use of shared resources and resource setup like logging.
        /// </remarks>
        /// <typeparam name="ITokenTypeT">This gives the externally exposed type used for the internal token implmentation type.  This must implement IDisposaible.</typeparam>
        /// <typeparam name="TokenTypeT">
        /// This gives the class that is being used as the Token type implementation class.  
        /// This must support a default constructor, and must implement both {ITokenTypeT} and Details.ISharedResourceTokenBase. 
        /// It must also be IDisposable
        /// </typeparam>
        public abstract class SharedResourceSetupAndReleaseBase<ITokenTypeT, TokenTypeT> where ITokenTypeT : IDisposable where TokenTypeT : Details.ISharedResourceTokenBase, ITokenTypeT, new()
        {
            /// <summary>
            /// Clients call this method to obtain a token that indicates they are making use of the shared resource.  
            /// Clients are required to dispose of the token they obtain from this method when they are done using the shared resource.
            /// The first client to call this method will cause the resource to be setup using this classes abstract Setup method and the last client to dispose
            /// of their token (obtained from this method) will cause this class to use the abstract Release method to release the resource.
            /// </summary>
            public ITokenTypeT GetSharedResourceReferenceToken(string clientName)
            {
                TokenTypeT token = new TokenTypeT() { ClientName = clientName, ReleaseAction = HandleTokenBeingDisposed };

                lock (tokenListMutex)
                {
                    InnerAddToken(token);
                }

                return token;
            }

            /// <summary>
            /// Public property that allows the caller to obtain an array containing the names of the clients that have tokens that have not been released yet.
            /// </summary>
            /// <remarks>This property is provided to allow test code to safely infer the contents of the internal token list.</remarks>
            public string[] CurrentClientsList
            {
                get
                {
                    lock (tokenListMutex)
                    {
                        return tokenList.Select((t) => t.ClientName).ToArray();
                    }
                }
            }

            /// <summary>
            /// Abstract method.  Invoked by this base class when the first client calls GetSharedResourceReferenceToken, passing in the name given by the first client.
            /// <para/>Please note that the method call occurs while owning the mutex on the list of clients and as such use of subordinate locks may cause a deadlock risk.
            /// </summary>
            protected abstract void Setup(string firstClientName);

            /// <summary>
            /// Abstract method.  Invoked by this base class when the last client disposes of the token that it obtained, passing in the name given by that client when it first created its token (which is saved in the token).
            /// <para/>Please note that the method call occurs while owning the mutex on the list of clients and as such use of subordinate locks may cause a deadlock risk.
            /// </summary>
            protected abstract void Release(string lastClientName);

            /// <summary>
            /// This method is given as a delegate to each newly created token so that the token may call it when the token is disposed.  
            /// This method locks the list, removes the given token from the list and if the list is now empty, the method invokes the Release method, passing in the
            /// client name that was captured and saved in the token when it was created.
            /// <para/>Note: this method must only be called while owning the mutex that enforces thread safe access to the list of tokens.
            /// </summary>
            private void HandleTokenBeingDisposed(Details.ISharedResourceTokenBase token)
            {
                lock (tokenListMutex)
                {
                    InnerRemoveToken(token);
                }
            }

            /// <summary>
            /// This is the internal method used to add the given token to the list of client tokens that have requested use of this shared resource.
            /// If the list was empty before the token was added, then this method calls the Setup method with the name of the client that created the given token.
            /// <para/>Note: this method must only be called while owning the mutex that enforces thread safe access to the list of tokens.
            /// </summary>
            protected virtual void InnerAddToken(Details.ISharedResourceTokenBase token)
            {
                bool listWasEmptyOnEntry = (tokenList.Count == 0);

                tokenList.Add(token);

                if (listWasEmptyOnEntry)
                    Setup(token.ClientName);
            }

            /// <summary>
            /// This is the internal method used to remove the given token to the list of client tokens that have requested use of this shared resource.
            /// If the list is empty after the token has been removed then this method calls the Release method with the name of the client that created the given token.
            /// </summary>
            protected virtual void InnerRemoveToken(Details.ISharedResourceTokenBase token)
            {
                tokenList.Remove(token);

                bool updatedListIsEmpty = (tokenList.Count == 0);

                if (updatedListIsEmpty)
                    Release(token.ClientName);
            }

            /// <summary>Mutex object used to guard access to the tokenList</summary>
            private readonly object tokenListMutex = new object();

            /// <summary>The list of tokens that have been created and which have not been disposed.</summary>
            private List<Details.ISharedResourceTokenBase> tokenList = new List<Details.ISharedResourceTokenBase>();

        }

        namespace Details
        {
            /// <summary>
            /// public interface used by templatized version of SharedResourceSetupAndReleaseBase to define what it needs to be able to do with a token implementation object in order
            /// to do its job correctly.  Offers a ClientName getter and setter and a ReleaseAction setter.
            /// </summary>
            public interface ISharedResourceTokenBase : IDisposable
            {
                /// <summary>
                /// Allows the caller to get or set the ClientName that is informally associated with this token.
                /// </summary>
                string ClientName { get; set; }

                /// <summary>
                /// Allows the caller to add an assigned action to the set of such actions that will be invoked when the object is disposed.
                /// </summary>
                Action<ISharedResourceTokenBase> ReleaseAction { set; }
            }

            /// <summary>
            /// Shared Resource type Token implementation class.  
            /// Derived from Disposable base.  To support use as with templatized SharedResourceSetupAndReleaseBase class, property initializers are used   
            /// Constructor retains a clientName and releaseAction.  
            /// The releaseAction will be invoked when the token is disposed.
            /// </summary>
            public class SharedResourceTokenBase : DisposableBase, ISharedResourceTokenBase
            {
                /// <summary>
                /// Gives the name of the client assigned as a property intitializer when the token was initially constructed
                /// </summary>
                public string ClientName { get; set; }

                /// <summary>
                /// Each assigned ReleaseAction gets added to the explicit dispose action list provided by DisposableBase (wrapped in a binding delegate).  Generally only the SharedResourceSetupAndReleaseBase will make use of this to add its own release action.
                /// </summary>
                public Action<ISharedResourceTokenBase> ReleaseAction
                {
                    set
                    {
                        AddExplicitDisposeAction(() => value(this));
                    }
                }
            }
        }
    }

    #endregion

    //-------------------------------------------------
}

//-------------------------------------------------
