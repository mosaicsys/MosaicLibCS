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
using System.Linq;
using System.Collections.Generic;

namespace MosaicLib.Utils
{
    //-------------------------------------------------
    #region Notification related collections and Collections namespace

    namespace Collections
    {
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
        public class LockedObjectListWithCachedArray<ObjectType>
        {
            /// <summary>Default contstructor</summary>
            public LockedObjectListWithCachedArray() { }

            /// <summary>Collection based constructor.  Sets up the list to contain the given collection of objects.</summary>
            public LockedObjectListWithCachedArray(IEnumerable<ObjectType> collection)
            {
                AddRange(collection);
            }

            #region Public methods and properties

            /// <summary>
            /// Adds the given object instance to the list and triggers the Array be rebuilt on next use.  
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// </summary>
            /// <returns>this object for call chaining</returns>
            public LockedObjectListWithCachedArray<ObjectType> Add(ObjectType d)
            {
                lock (listMutex)
                {
                    rebuildVolatileObjectArray = true;
                    objectList.Add(d);
                }

                return this;
            }
            /// <summary>
            /// Removes the given object instance from the list and triggers the Array be rebuilt on next use.  
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// </summary>
            /// <returns>this object for call chaining</returns>
            public LockedObjectListWithCachedArray<ObjectType> Remove(ObjectType d)
            {
                lock (listMutex)
                {
                    rebuildVolatileObjectArray = true;
                    objectList.Remove(d);
                }

                return this;
            }

            /// <summary>
            /// Adds the given collection of objects to the end of the list and triggers the Array to be rebuilt on its next use.
            /// Re-entrant and thread safe using leaf lock on list contents.
            /// </summary>
            /// <param name="collection">Gives the IEnumerable collection of items to append to the end of this list.</param>
            /// <returns>this object for call chaining</returns>
            public LockedObjectListWithCachedArray<ObjectType> AddRange(IEnumerable<ObjectType> collection)
            {
                lock (listMutex)
                {
                    rebuildVolatileObjectArray = true;
                    objectList.AddRange(collection);
                }

                return this;
            }

            /// <summary>
            /// Gets or sets the element at the specified index. 
            /// </summary>
            /// <param name="index">The zero-based index of the element to get or set.</param>
            /// <returns>The element at the specified index.</returns>
            /// <exception cref="System.ArgumentOutOfRangeException">index is less than 0.  -or- index is equal to or greater than Count.</exception>
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
                        objectList[index] = value;
                        rebuildVolatileObjectArray = true;
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
                            if (array == null)
                                array = emptyObjectArray;

                            volatileObjectArray = array;
                        }
                    }

                    return array;
                }
            }

            #endregion

            #region Private fields

            /// <summary>mutex used to guard/sequence access to the underlying list so that both changes and access to the list are performed atomically.</summary>
            private object listMutex = new object();
            /// <summary>underlying reference list of delegates, access to this list must only be made while owning the corresponding mutex.</summary>
            private List<ObjectType> objectList = new List<ObjectType>();
            /// <summary>Single common empty array that is used as the array when the list is empty.</summary>
            private static ObjectType[] emptyObjectArray = new ObjectType[0];
            /// <summary>volatile handle to the array of delegates produced during the last rebuild operation.</summary>
            private volatile ObjectType[] volatileObjectArray = emptyObjectArray;
            /// <summary>volatile boolean used to flag that a rebuild is required during the next access to the Array property.</summary>
            private volatile bool rebuildVolatileObjectArray = true;

            #endregion
        }

        /// <summary>
        /// Provides a simple, thread safe, queue for first-in first-out storage of items.  
        /// This object is based on the System.Collections.Generic.Queue object with a simplified API.  
        /// It wraps the various accessor methods with use of a local mutex to enforce thead safety, and implements a cached copy of the queue count with
        /// a VoltileCount property that allows the user to avoid needing locked access to the queue to check the size of the queue at the last time that 
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
            /// Enqueues the given item.
            /// </summary>
            public void Enqueue(ItemType item)
            {
                lock (mutex)
                {
                    backingQueue.Enqueue(item);
                    volatileCount = backingQueue.Count;
                }
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
            private object mutex = new object();

            /// <summary>This is the backing queue object on which this object is based.</summary>
            private Queue<ItemType> backingQueue = new Queue<ItemType>();

            /// <summary>storage for the VolatileCount property.  Used as a local cached copy of the queue.Count value.</summary>
            private volatile int volatileCount = 0;
        }
    }

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
        {
        }

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
            private object tokenListMutex = new object();

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
