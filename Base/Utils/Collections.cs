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

using MosaicLib;
using MosaicLib.Utils;

namespace MosaicLib.Utils
{
    namespace Collections
    {
        #region LockedObjectListWithCachedArray

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
            /// <summary>Default contstructor</summary>
            public LockedObjectListWithCachedArray() { }

            /// <summary>Collection based constructor.  Sets up the list to contain the given collection of objects.</summary>
            public LockedObjectListWithCachedArray(IEnumerable<ObjectType> collection)
            {
                AddRange(collection);
            }

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

        #endregion

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
            public ItemType [] DequeueAll()
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

        #region ReadOnlyIList

        /// <summary>
        /// This class is a local replacement for the System.Collections.ObjectModel.ReadOnlyCollection as the native one simply provides a read-only facade on the underlying mutable IList from which it is constructed.
        /// <para/>Note: This object is intended as a utility storage class.  All interfaces are implemented explicitly so the caller can only make use of this object's contents by casting it to one of the supported interfaces.
        /// </summary>
        [Serializable]
        public class ReadOnlyIList<TItemType> : IList<TItemType>, ICollection<TItemType>, IEnumerable<TItemType>, IList, ICollection, IEnumerable
        {
            /// <summary>
            /// Constructs the contents of this item from the set of explicitly defined items (<paramref name="firstItem"/> followed by 0 or <paramref name="moreItemsArray"/> items).
            /// </summary>
            public ReadOnlyIList(TItemType firstItem, params TItemType[] moreItemsArray)
            {
                itemsArray = firstItem.Concat(moreItemsArray).ToArray();
            }

            /// <summary>
            /// Constructs the contents of this item based on the contents of the given <paramref name="sourceItemList"/>.  
            /// If the given <paramref name="sourceItemList"/> is null then this method will be constructed as an empty list.
            /// </summary>
            public ReadOnlyIList(IList<TItemType> sourceItemList)
            {
                ReadOnlyIList<TItemType> sourceAaROIL = sourceItemList as ReadOnlyIList<TItemType>;

                itemsArray = (sourceAaROIL != null) ? sourceAaROIL.itemsArray : sourceItemList.SafeToArray(fallbackArray: emptyArray);
            }

            /// <summary>
            /// Constructs the contents of this item based on the contents of the given <paramref name="sourceItemCollection"/>.  
            /// If the given <paramref name="sourceItemCollection"/> is null then this method will be constructed as an empty list.
            /// </summary>
            public ReadOnlyIList(ICollection<TItemType> sourceItemCollection = null)
            {
                ReadOnlyIList<TItemType> sourceAaROIL = sourceItemCollection as ReadOnlyIList<TItemType>;

                itemsArray = (sourceAaROIL != null) ? sourceAaROIL.itemsArray : sourceItemCollection.SafeToArray(fallbackArray: emptyArray);
            }

            /// <summary>
            /// Constructs the contents of this item based on the contents of the given <paramref name="sourceItemSet"/>.  
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

            private ReadOnlyCollection<TItemType> ROCOfItems { get { return _rocOfItems = (_rocOfItems ?? new ReadOnlyCollection<TItemType>(itemsArray)); } }

            #region IList, ICollection, IEnumerable implementations

            int IList<TItemType>.IndexOf(TItemType item)
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

            TItemType IList<TItemType>.this[int index]
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

            bool ICollection<TItemType>.Contains(TItemType item)
            {
                return ROCOfItems.Contains(item);
            }

            void ICollection<TItemType>.CopyTo(TItemType[] array, int arrayIndex)
            {
                ROCOfItems.CopyTo(array, arrayIndex);
            }

            int ICollection<TItemType>.Count
            {
                get { return itemsArray.Length; }
            }

            bool ICollection<TItemType>.IsReadOnly
            {
                get { return true; }
            }

            bool ICollection<TItemType>.Remove(TItemType item)
            {
                throw new System.NotSupportedException("{0}.{1} cannot be used.  collection is read-only".CheckedFormat(Fcns.CurrentClassLeafName, Fcns.CurrentMethodName));
            }

            IEnumerator<TItemType> IEnumerable<TItemType>.GetEnumerator()
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

            int ICollection.Count
            {
                get { return itemsArray.Length; }
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
        }

        /// <summary>
        /// IEnumerator{TItemType} struct that supports enumerating through an array.
        /// </summary>
        public struct ArrayEnumerator<TItemType> : IEnumerator<TItemType>, IDisposable, IEnumerator
        {
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
                        throw new System.InvalidOperationException("The enumerator is positioned before the first element of the array or after the last element");
                    }

                    return this.Current;
                }
            }

            /// <summary>Releases all resources used by this enumerator (currently this is a no-op).</summary>
            public void Dispose()
            {
            }

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
    }

    namespace Collections.Trees
    {
        public class Tree<TItemType, TKeyPathItemType>
            where TKeyPathItemType: IEquatable<TKeyPathItemType>
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
                        throw new DuplicatePathException("Key path [{0}] already found while building tree".CheckedFormat(keyPathArray.ToString(separator: " ")));

                    if (currentNode == null)
                    {
                        currentNode = new TreeNode<TItemType,TKeyPathItemType> { Item = item, KeyPathArray = keyPathArray, IsRootNode = keyPathArray.IsNullOrEmpty() };
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
            public static TreeNode<TItemType,TKeyPathItemType> TryToFillInAndReturnParentNode<TItemType, TKeyPathItemType>(this TreeNode<TItemType, TKeyPathItemType> treeNode, Dictionary<TKeyPathItemType[], TreeNode<TItemType, TKeyPathItemType>> treeNodeDictionary)
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
