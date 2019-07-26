//-------------------------------------------------------------------
/*! @file Pooling.cs
 *  @brief This file defines the MosaicLib.Utils.Pooling namespace which provides a set of utility definitions and classes that are useful for implementing reference counted and pooled objects.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2006 Mosaic Systems Inc.  (C++ library version embodied in PoolIface.h and PoolImpl.h)
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

namespace MosaicLib.Utils.Pooling
{
    #region Very basic FreeList object for single threaded use

    /// <summary>
    /// This class gives a very basic free list implementation where items can be added to the list (up to some limit)
    /// and later be obtained from the list.  
    /// Internally the implementation uses a LIFO queue to minimimize processor cache coherancy issues.
    /// <para/>This implemenation requires the client to define a non-null FactoryDelegate that is used to construct new objects when needed.
    /// <para/>The client may also define a ClearDelegate that, if non-null, will be used to clear each item that is returned to the free list.
    /// <para/>NOTE: this type is NOT thread safe.  The client must make certain that only one thread attempts to make use of any given instance of this class at a time.
    /// </summary>
    /// <typeparam name="TItemType">Gives the type of object maintained by the list.  Must be a reference type (class)</typeparam>
    public class BasicFreeList<TItemType> where TItemType : class
    {
        /// <summary>
        /// Constructor.  Sets MaxItemsToKeep to default to 10.  Sets WillDispose to true if TItemType is IDisposable
        /// </summary>
        public BasicFreeList()
        {
            MaxItemsToKeep = 10;
            WillDispose = typeof(IDisposable).IsAssignableFrom(typeof(TItemType));
        }

        /// <summary>
        /// Delegate used to construct new TItemType objects when needed.  If this property is null then the Get method will return null if the list is empty.
        /// </summary>
        public Func<TItemType> FactoryDelegate { get; set; }

        /// <summary>
        /// Optional delegate.  
        /// When non-null this delegate will be used to "Clear" items that are being returned to the list.  
        /// Items that are discarded when the list is full will not be "Clear"ed.
        /// </summary>
        public Action<TItemType> ClearDelegate { get; set; }

        /// <summary>
        /// Defines the length of the free list, above which Released items will be discarded, rather than being appended to the free list.
        /// </summary>
        public int MaxItemsToKeep { get; set; }

        /// <summary>Property determines if the Release function will call Dispose on objects that are discarded because the free list is full.</summary>
        private bool WillDispose { get; set; }

        /// <summary>
        /// Attempts to obtain the last item in the free list (if it is not empty) and return it.
        /// If the free list is empty and the FactoryDelegate is non-null then this method will invoke the FactoryDelegate to construct a new object and will return it.
        /// If the list is empty and the FactoryDelegate is null then this method returns null.
        /// </summary>
        public TItemType Get()
        {
            int getIndex = freeList.Count - 1;
            if (getIndex >= 0)
            {
                TItemType item = freeList[getIndex];
                freeList.RemoveAt(getIndex);
                return item;
            }
            
            Func<TItemType> factoryDelegate = FactoryDelegate;

            if (factoryDelegate != null)
                return factoryDelegate();

            return null;
        }

        /// <summary>
        /// This method attempt to return the given <paramref name="item"/> to the free list. 
        /// If the given <paramref name="item"/> is null then the method has no effect.
        /// If the free list is no full then the <paramref name="item"/> will be added to the back of the free list, optionally being cleared using the ClearDelegate first.
        /// If the free list is no full then the <paramref name="item"/> will be discarded.  
        /// Before discarding, If TItemType is IDisposable then the <paramref name="item"/> will be disposed using the Fcns.DisposeOfGivenObject first.
        /// <para/>This method requires reference access to the given <paramref name="item"/> and the reference will be assigned to null before the method returns.
        /// </summary>
        public void Release(ref TItemType item)
        {
            if (item == null)
                return;

            if (freeList.Count < MaxItemsToKeep)
            {
                Action<TItemType> clearDelegate = ClearDelegate;
                if (clearDelegate != null)
                    clearDelegate(item);

                freeList.Add(item);
            }
            else if (WillDispose)
            {
                Fcns.DisposeOfGivenObject(item);
            }

            item = null;
        }

        /// <summary>
        /// This method attempt to return the given <paramref name="item"/> to the free list. 
        /// If the given <paramref name="item"/> is null then the method has no effect.
        /// If the free list is no full then the <paramref name="item"/> will be added to the back of the free list, optionally being cleared using the ClearDelegate first.
        /// If the free list is no full then the <paramref name="item"/> will be discarded.  
        /// Before discarding, If TItemType is IDisposable then the <paramref name="item"/> will be disposed using the Fcns.DisposeOfGivenObject first.
        /// <para/>This method does not require reference access to the given <paramref name="item"/> and the caller given value will not be assigned to null.
        /// </summary>
        public void ReleaseGivenItem(TItemType item)
        {
            Release(ref item);
        }

        /// <summary>
        /// This is the actual free list that is used to hold onto the items that have been Released and that are available to be given out by Get calls.
        /// </summary>
        private List<TItemType> freeList = new List<TItemType>();
    }

    #endregion

    #region Basic Pool Interface and implementation

    /// <summary>This interface defines the public interface for Basic Object Pools.</summary>
    public interface IBasicObjectPool<ObjectType>
        where ObjectType : class
    {
        /// <summary>Method allows caller to obtain a free object from the pool.  Returned object will either be newly created for will have been returned to the pool when its reference count was decremented to zero.</summary>
        ObjectType GetFreeObjectFromPool();

        /// <summary>
        /// Returns the given object to the pool.  Has no effect if given the null value.  Generally the object should have been acquired from the pool.  
        /// <para/>The caller is required to pass the last variable/handle to theObject to this method by reference and this method will set the referenced variable/handle to null to complete the return of the object.
        /// If the pool is already full then the object will be dropped or disposed (as appropriate) using Fcns.DisposeOfObject
        /// </summary>
        void ReturnObjectToPool(ref ObjectType objRef);

        /// <summary>Returns the maximum number of pool objects that may be retained in the pool.  Additional objects will be dropped (and will be collected by the GC).</summary>
        int Capacity { get; }

        /// <summary>Returns the current number of object in the pool.</summary>
        int Count { get; }

        /// <summary>Releases all objects from the pool and disables the return of objects to the pool.  Subsequent calls to GetFreeObjectFromPool will explicitly create objects and will not associate them with the pool.</summary>
        void Shutdown();

        /// <summary>Restarts the pool if it has been Shutdown since it was constructed or since the last time it was Started.</summary>
        IBasicObjectPool<ObjectType> StartIfNeeded();

        /// <summary>Returns true if the pool is enabled</summary>
        bool IsPoolEnabled { get; }
    }

    /// <summary>
    /// This class implements a basic Object Pool where objects of a given type may be acquired from the pool (which creates them as needed) and may 
    /// be returned to the pool (which may dispose them if the pool is already full).  The client is responsible for all object lifetime managment
    /// </summary>
    public class BasicObjectPool<ObjectType>
        : Utils.DisposableBase
        , IBasicObjectPool<ObjectType>
        where ObjectType : class
    {
        #region Construction, Setup, Disposal

        /// <summary>
        /// Default constructor.  Enables the pool and sets the Capacity to the default of 1.  
        /// Caller must set the ObjectFactoryDelegate property to a valid factory delegate before attempting to Get an object from the pool
        /// </summary>
        public BasicObjectPool()
        {
            Capacity = 1;
            AddExplicitDisposeAction(() => Shutdown());
            StartIfNeeded();
        }

        /// <summary>
        /// This propery defines the delegate that is used to construct new objects in the pool.  This property is generally initialized during object construction.
        /// <para/>This propertie's setter is not threadsafe and must not be changed after the first call to GetFreeObjectFromPool has been made if the GetFreeObjectFromPool method
        /// is being used by more than one thread at a time.
        /// </summary>
        public System.Func<ObjectType> ObjectFactoryDelegate { get; set; }

        /// <summary>
        /// Optional delegate.  
        /// When non-null this delegate will be used to "Clear" items that are being returned to the list.  
        /// Items that are discarded when the list is full may not be "Clear"ed.
        /// </summary>
        public Action<ObjectType> ObjectClearDelegate { get; set; }

        #endregion

        #region IObjectPool<ObjectType> Members

        /// <summary>
        /// Attempts to obtain a free object from the pool.  If there are none or if the pool is not enabled, then explicitly Constructs a new object and returns it.
        /// </summary>
        public virtual ObjectType GetFreeObjectFromPool()
        {
            ObjectType obj = null;

            lock (freeObjectStackMutex)
            {
                if (poolIsEnabled && freeObjectStack.Count > 0)
                    obj = freeObjectStack.Pop();
            }

            if (obj == null && ObjectFactoryDelegate != null)
                obj = ObjectFactoryDelegate();

            return obj;
        }

        /// <summary>
        /// Returns the given object to the pool.  Has no effect if given the null value.  Generally the object should have been acquired from the pool.  
        /// <para/>The caller is required to pass the last variable/handle to theObject to this method by reference and this method will set the referenced variable/handle to null to complete the return of the object.
        /// If the pool is already full then the object will be dropped or disposed (as appropriate) using Fcns.DisposeOfObject
        /// </summary>
        public virtual void ReturnObjectToPool(ref ObjectType objRef)
        {
            ObjectType objRefCopy = objRef;

            objRef = null;

            if (objRefCopy != null)
            {
                if (ObjectClearDelegate != null)
                    ObjectClearDelegate(objRefCopy);

                lock (freeObjectStackMutex)
                {
                    if (poolIsEnabled && freeObjectStack.Count < freeObjectStackCapacity)
                    {
                        freeObjectStack.Push(objRefCopy);
                        objRefCopy = null;
                    }
                }
            }

            Fcns.DisposeOfObject(ref objRefCopy);
        }

        /// <summary>Returns the maximum number of free objects that can be contained in the pool.</summary>
        public int Capacity { get { return (IsPoolEnabled ? freeObjectStackCapacity : 0); } set { freeObjectStackCapacity = value; } }

        /// <summary>Returns the number of free objects in the pool.</summary>
        public int Count { get { lock (freeObjectStackMutex) { return ((freeObjectStack != null) ? freeObjectStack.Count : 0); } } }

        /// <summary>Disables the pool and disposes of all objects that remain in the freeObjctStack.</summary>
        public void Shutdown()
        {
            lock (freeObjectStackMutex)
            {
                // disable the pool
                poolIsEnabled = false;

                if (freeObjectStack != null)
                {
                    // remove and dispose of all objects in the freeObjectStack
                    while (freeObjectStack.Count > 0)
                    {
                        ObjectType obj = freeObjectStack.Pop();
                        Fcns.DisposeOfObject(ref obj);
                    }

                    freeObjectStack = null;
                }
            }
        }

        /// <summary>Restarts the pool if it has been Shutdown since it was constructed or since the last time it was Started.</summary>
        public IBasicObjectPool<ObjectType> StartIfNeeded()
        {
            lock (freeObjectStackMutex)
            {
                if (!poolIsEnabled)
                {
                    if (freeObjectStack == null)
                        freeObjectStack = new Stack<ObjectType>();

                    poolIsEnabled = true;
                }
            }

            return this;
        }

        /// <summary>Returns true if the pool is enabled</summary>
        public bool IsPoolEnabled { get { return poolIsEnabled;  } }

        #endregion

        #region Private instance variables

        /// <summary>volatile bool used to determine if the pool is enabled.  Pool construction enables the pool.  Explicit call to Shutdown or explicit disposal of this pool object. </summary>
        private volatile bool poolIsEnabled = false;

        /// <summary>object used as mutex for access to freeObjectStack and freeObjectStackCapacity.</summary>
        private readonly object freeObjectStackMutex = new object();

        /// <summary>Stack of free objects that are currently in the pool.  Use of LIFO semantics is chosen to generally improve cache and virtual memory efficiency.</summary>
        private Stack<ObjectType> freeObjectStack = null;

        /// <summary>field defines the maximum number of objects that the freeObjectStack can hold.</summary>
        private volatile int freeObjectStackCapacity = 0;

        #endregion
    }

    #endregion

    #region Reference counted object Pool related Intefaces

    /// <summary>
    /// Defines a delegate that is used by a pooled object to allow it to release itself to the pool from which the object was created.  
    /// Use of a delegate allows the pool to combine its own reference and the private method it uses to return objects to the pool without requiring that the pooled object knows anything about the pool to which it belongs.
    /// </summary>
	public delegate void ReleaseObjectToPoolDelegate<ObjType>(ref ObjType oRef);

    /// <summary>This interface defines the behavior that clients of reference counted objects use to add and remove references to the object and to query its current reference count state.</summary>
    /// <remarks>
    /// Please note that IRefCountedObject's generally support both pooled and non-pooled use.  
    /// Objects create within a pool may be handled and returned to the pool as long as all relevant external code honors the reference count semantics.  
    /// Objects that are manually created outside of a pool do not belong to any pool and will not be returned to any based on use of RemoveReference.
    /// Internal assertions about correct use of AddReference and RemoveReference are only enforced for objects that were created by, and belong to, a specific pool.
    /// </remarks>
    public interface IRefCountedObject<ObjectType> where ObjectType : class
	{
        /// <summary>Allows the caller to create a new reference to the object.  Returns the object on which the operation was performed.</summary>
        ObjectType AddReference();
        /// <summary>Allows the caller to release a reference to an object and thus decrement the contained reference count.  If object belongs to a pool then it will be released to that pool once the reference count returns to zero.  Caller must pass the referring field or variable by reference as the method nulls the contents of the referenced handle.</summary>
        void RemoveReference(ref ObjectType refHandle);
        /// <summary>Returns the current reference count</summary>
        int RefCount { get; }
        /// <summary>Returns true if the current RefCount is exactly 1</summary>
        bool IsUnique { get; }
        /// <summary>Returns true if the object was created by a pool and can be returned to it.</summary>
        bool BelongsToPool { get; }
	}

    /// <summary>This interface is used by the Pool itself to assign the ReleaseObjectToPoolDelegate after a new object has been added to the pool.</summary>
    public interface IPoolableRefCountedObject<ObjectType> : IRefCountedObject<ObjectType> where ObjectType : class
	{
        /// <summary>Pool is allowed to get or set the ReleaseObjectToPoolDelegate.  Property may only be set when object IsUnique</summary>
        ReleaseObjectToPoolDelegate<ObjectType> ReleaseObjectToPoolDelegate { get; set; }

        /// <summary>Used during initial building of pool objects</summary>
        void DecrementRefCount();

        /// <summary>
        /// Method that is used by pool or by object implementation to remove object references that are not returned to a pool 
        /// (ether because the object does not belong to one or because the pool is no longer enabled or has already reached its capacity limit)
        /// If ObjectType implements IDisposable, then the refHandle's Dispose method will be invoked by this method.
        /// </summary>
        void DisposeOfSelf(ref ObjectType selfObjRef);
	}

    /// <summary>This interface defines the public interface for Object Pools based on object types that support the IPoolableRefCountedObject interface.</summary>
    public interface IObjectPool<ObjectType>
		where ObjectType : class, IPoolableRefCountedObject<ObjectType>
	{
        /// <summary>Method allows caller to obtain a free object from the pool.  Returned object will either be newly created for will have been returned to the pool when its reference count was decremented to zero.</summary>
        ObjectType GetFreeObjectFromPool();
        /// <summary>Returns the maximum number of pool objects that may be retained in the pool.  Additional objects will be dropped (and will be collected by the GC).</summary>
        int Capacity { get; }
        /// <summary>Returns the current number of object in the pool.</summary>
        int Count { get; }
        /// <summary>Releases all objects from the pool and disables the return of objects to the pool.  Subsequent calls to GetFreeObjectFromPool will explicitly create objects and will not associate them with the pool.</summary>
        void Shutdown();
        /// <summary>(re)Enables the use of the pool if it has been Shutdown.  Has no effect if it has just been constructed or if it has already been started.</summary>
        void StartIfNeeded();
	}

    #endregion

    #region ObjectPool classes

    /// <summary>This generic class provides the primary implementation for the IObjectPool generic interface.</summary>
    /// <typeparam name="ObjectType">Non-abstract object type that supports the IPoolableRefCountedObject interface.  Supports ObjectTypes that are IDisposable.</typeparam>
    /// <remarks>
    /// Addition of support for IDisposable ObjectType's adds some complexity and release cost to this class.  
    /// Given the rate at which ObjectPool's are generally created and disposed and given the added value of supporting disposable objects in the pool, the added overhead
    /// is viewed as acceptable.
    /// </remarks>

    public class ObjectPool<ObjectType>
        : ObjectPoolBase<ObjectType>
        where ObjectType : class, IPoolableRefCountedObject<ObjectType>, new()
    {
        /// <summary>Default constructor - uses default initial pool size and capacity of 1000, and 10000 respectively.</summary>
        public ObjectPool() 
            : this(DefaultPoolSize, DefaultCapacity) 
        { 
        }

        /// <summary>Constructor - caller defines the initial pool size and capacity.  Method enforces minimum capacity of initialPoolSize and 10, whichever is greater.</summary>
        public ObjectPool(int initialPoolSize, int initialCapacity)
            : base(initialPoolSize, initialCapacity, () => new ObjectType())
        {}

    }

    /// <summary>This generic class provides the primary implementation for the IObjectPool generic interface.</summary>
    /// <typeparam name="ObjectType">Non-abstract object type that supports the IPoolableRefCountedObject interface.  Supports ObjectTypes that are IDisposable.</typeparam>
    /// <remarks>
    /// Addition of support for IDisposable ObjectType's adds some complexity and release cost to this class.  
    /// Given the rate at which ObjectPool's are generally created and disposed and given the added value of supporting disposable objects in the pool, the added overhead
    /// is viewed as acceptable.
    /// </remarks>

    public class ObjectPoolBase<ObjectType>
        : Utils.DisposableBase
        , IObjectPool<ObjectType>
        where ObjectType : class, IPoolableRefCountedObject<ObjectType>
    {
        #region Constructor and Destructor

        /// <summary>This value gives the default pool size (number of pre-allocated objects) that will be used when the user uses the default constructor.</summary>
        protected const int DefaultPoolSize = 1000;
        /// <summary>This value gives the default capacity that will be used when the user uses the default constructor.</summary>
        protected const int DefaultCapacity = 10000;
        /// <summary>Gives the Minimum Capacity for any ObjectPool</summary>
        protected const int MinimumCapacity = 10;

        /// <summary>Default constructor - uses default initial pool size and capacity of 1000, and 10000 respectively.</summary>
        public ObjectPoolBase(System.Func<ObjectType> objectFactoryDelegate) 
            : this(DefaultPoolSize, DefaultCapacity, objectFactoryDelegate) 
        { 
        }

        /// <summary>Constructor - caller defines the initial pool size and capacity.  Method enforces minimum capacity of initialPoolSize and 10, whichever is greater.</summary>
        public ObjectPoolBase(int initialPoolSize, int initialCapacity, System.Func<ObjectType> objectFactoryDelegate)
        {
            InitialCapacity = initialCapacity;
            InitialPoolSize = initialPoolSize;
            if (InitialCapacity < InitialPoolSize)
                InitialCapacity = InitialPoolSize;
            if (InitialCapacity < MinimumCapacity)
                InitialCapacity = MinimumCapacity;

            ObjectFactoryDelegate = objectFactoryDelegate;

            roReleaseObjectToPoolDelegate = ImplementReleaseObjectToPoolDelegate;

            StartIfNeeded();
        }

        /// <summary>implementation method for DisposableBase.  On explicit dispose, this method will perform a Shutdown if needed so as to dispose of all objects in the stack.</summary>
        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (poolIsEnabled && disposeType == DisposeType.CalledExplicitly)
                Shutdown();
        }

        private System.Func<ObjectType> ObjectFactoryDelegate { get; set; }

        #endregion

        #region IObjectPool<ObjectType> Members

        /// <summary>Attempts to obtain a free object from the pool.  If there are none or if the pool is not enabled, then explicitly Constructs a new object and returns it.</summary>
        public virtual ObjectType GetFreeObjectFromPool()
        {
            ObjectType obj = null;

            lock (freeObjectStackMutex)
            {
                if (poolIsEnabled && freeObjectStack.Count > 0)
                    obj = freeObjectStack.Pop().AddReference();
            }

            if (obj == null)
                obj = ConstructNewObject();

            if (!obj.IsUnique)
                Asserts.TakeBreakpointAfterFault("Pool.GetFreeObjectFromPool gave non-Unique object");

            return obj;
        }

        /// <summary>Returns the maximum number of free objects that can be contained in the pool.</summary>
        public int Capacity { get { lock (freeObjectStackMutex) { return (IsPoolEnabled ? freeObjectStackCapacity : 0); } } }

        /// <summary>Returns the number of free objects in the pool.</summary>
        public int Count { get { lock (freeObjectStackMutex) { return ((freeObjectStack != null) ? freeObjectStack.Count : 0); } } }

        /// <summary>Disables the pool and disposes of all objects that remain in the freeObjctStack.</summary>
        public void Shutdown()
        {
            lock (freeObjectStackMutex)
            {
                // disable the pool
                poolIsEnabled = false;

                if (freeObjectStack != null)
                {
                    // remove and dispose of all objects in the freeObjectStack
                    while (freeObjectStack.Count > 0)
                    {
                        IPoolableRefCountedObject<ObjectType> obj = freeObjectStack.Pop();
                        ObjectType objAsDerivedType = obj as ObjectType;
                        if (obj != null && objAsDerivedType != null)
                            obj.DisposeOfSelf(ref objAsDerivedType);
                    }

                    freeObjectStack = null;
                }

                freeObjectStackCapacity = 0;
            }
        }

        /// <summary>(re)Enables the use of the pool if it has been Shutdown.  Has no effect if it has just been constructed or if it has already been started.</summary>
        public void StartIfNeeded()
        {
            lock (freeObjectStackMutex)
            {
                if (!poolIsEnabled)
                {
                    if (freeObjectStack == null)
                    {
                        freeObjectStack = new Stack<IPoolableRefCountedObject<ObjectType>>(InitialCapacity);
                        freeObjectStackCapacity = InitialCapacity;
                    }

                    poolIsEnabled = true;

                    // explicitly allocate InitialPoolSize elements and tell each one that we have removed the initial reference.  
                    // as such Initial objects enter the pool the same way that normal objects to.

                    for (int count = freeObjectStack.Count; count < InitialPoolSize; count++)
                    {
                        ObjectType obj = ConstructNewObject();
                        obj.DecrementRefCount();            // skip the full release handler
                        freeObjectStack.Push(obj);
                    }
                }
            }
        }

        /// <summary>Returns true if the pool is enabled</summary>
        public bool IsPoolEnabled { get { return poolIsEnabled; } }

        #endregion

        #region Private and Protected instance methods

        /// <summary>readonly instance to the ReleaseObjectToPoolDelegate for this pool.</summary>
        private readonly ReleaseObjectToPoolDelegate<ObjectType> roReleaseObjectToPoolDelegate;

        /// <summary>This method invokes default new() constructor on ObjectType and then assigns the resulting object's ReleaseObjectToPoolDelegate if this pool is currently enabled.</summary>
        private ObjectType ConstructNewObject()
        {
            ObjectType obj = ObjectFactoryDelegate();

            // mark newly created objects as belonging to this pool only if the pool is enabled
            if (poolIsEnabled && obj != null)
                obj.ReleaseObjectToPoolDelegate = roReleaseObjectToPoolDelegate;

            return obj;
        }

        /// <summary>Implementation method for ReleaseToPoolDelegate.  Confirms that object implements IPoolableRefCountedObject and that it belongs to this pool.</summary>
        /// <remarks>This delegate method is only called immediately after the object has decremented the reference count to zero.  As such the test for RefCount == 0 is not repeated here.</remarks>
        private void ImplementReleaseObjectToPoolDelegate(ref ObjectType objRef)
        {
            IPoolableRefCountedObject<ObjectType> item = objRef as IPoolableRefCountedObject<ObjectType>;

            if (item == null || item.ReleaseObjectToPoolDelegate != roReleaseObjectToPoolDelegate)
            {
                Asserts.TakeBreakpointAfterConditionCheckFailed("Pool.ReleaseObjectToPool failed: returned object is not of correct type or does not belong to this pool.");
                return; // caller will dispose of objRef as appropriate
            }

            lock (freeObjectStackMutex)
            {
                if (poolIsEnabled && freeObjectStack.Count < freeObjectStackCapacity)
                {
                    objRef = null;
                    freeObjectStack.Push(item);
                    item = null;
                }
                // else - caller will dispose of objRef as appropriate
            }
        }

        #endregion

        #region Private instance variables

        /// <summary>volatile bool used to determine if the pool is enabled.  Pool construction enables the pool.  Explicit call to Shutdown or explicit disposal of this pool object. </summary>
        private volatile bool poolIsEnabled = false;

        /// <summary>object used as mutex for access to freeObjectStack and freeObjectStackCapacity.</summary>
        private readonly object freeObjectStackMutex = new object();

        private int InitialCapacity { get; set; }
        private int InitialPoolSize { get; set; }

        /// <summary>Stack of free objects that are currently in the pool.  Use of LIFO semantics is chosen to generally improve cache and virtual memory efficiency.</summary>
        private Stack<IPoolableRefCountedObject<ObjectType>> freeObjectStack = null;

        /// <summary>field defines the maximum number of objects that the freeObjectStack can hold.</summary>
        private int freeObjectStackCapacity = 0;

        #endregion
    }

    #endregion

    #region Pooled object base class (RefCountedRefObjectBase)

    /// <summary>This is a common base class that may be used as the base for objects that need to implement the IRefCountedObject and IPoolableRefCountedObject interfaces.</summary>
    /// <remarks>
    /// This class is defined as abstract to indicate that it cannot be directly constructed.
    /// 
    /// If ObjectType is IDisposable, calls to RemoveReference will directly Dispose of the object when the final reference has been removed and the object does not belong to a pool.
    /// As such use of this base is not recommended for Objects which are also IDisposable unless the user is fully aware of this side effect of the final call to RemoveReferences.
    /// </remarks>
    public abstract class RefCountedRefObjectBase<ObjectType> : IPoolableRefCountedObject<ObjectType> where ObjectType : class
	{
		#region IPoolableRefCountedObject<ObjectType> implementation
        
        /// <summary>Increments contained reference count and returns (new) handle to object.</summary>
		public virtual ObjectType AddReference()
		{
            refCount.Increment();
			return (this as ObjectType);
		}

        /// <summary>Decrements contined reference count and consumes (nulls) given reference to the derived ObjectType variable that is being Removed.  If ObjectType implements IDisposable and object is not returned to a pool, object will be Disposed instead.</summary>
        public virtual void RemoveReference(ref ObjectType objRef)
		{
            if (refCount.VolatileValue <= 0)
            {
                Asserts.TakeBreakpointAfterFault("RemoveReference called on released object");
                objRef = null;
                return;
            }

			int decrementedRefCount = refCount.Decrement();
            if (decrementedRefCount == 0)
            {
                if (BelongsToPool)
                    ReleaseFinalObjectReferenceAndReturnToPool(ref objRef);

                if (objRef != null)
                    DisposeOfSelf(ref objRef);     // directly call our own DiposeOfObject method to clear the given handle and, optionally, dispose of the object.
            }
            else
            {
                objRef = null;
            }
		}

        /// <summary>Returns the current reference count for this object</summary>
        public int RefCount { get { return refCount.VolatileValue; } }

        /// <summary>Returns true if the current RefCount is exactly 1.</summary>
        public bool IsUnique { get { return RefCount == 1; } }

        /// <summary>Returns true if the object was created by a pool and can be returned to it.</summary>
        /// <summary>Method returns true if this object belongs to a pool.  (ie it's ReleaseObjectToPoolDelegate field is non-null.</summary>
        public bool BelongsToPool { get { return (releaseObjectToPoolDelegate != null); } }

        /// <summary>Delegate used to return an object to it's owning pool.  If this is null then the object does not belong to a pool.</summary>
        public ReleaseObjectToPoolDelegate<ObjectType> ReleaseObjectToPoolDelegate
        {
            get { return releaseObjectToPoolDelegate; }
            set
            {
                // NOTE: the following tests are performed explicitly to prevent calling into Utils.Assert static class (and thus attempting to construct a new Logger instance before the distribution singleton has been constructed.
                if (!IsUnique || BelongsToPool)
                    Asserts.TakeBreakpointAfterFault("ReleaseObjectToPoolDelegate can only be set when object instance IsUnique and when it does not already belong to a pool");

                releaseObjectToPoolDelegate = value;
            }
        }

        /// <summary>Used during initial building of pool objects</summary>
        public void DecrementRefCount()
        {
            refCount.Decrement();
        }

        /// <summary>Final low level method invoked on an selfObjRef when the object could not be returned to a pool.  Nulls the given selfObjRef and invokes the object's Dispose method if ObjectType implements IDisposable.</summary>
        public void DisposeOfSelf(ref ObjectType selfObjRef)
        {
            Utils.Fcns.DisposeOfObject(ref selfObjRef);
        }

        #endregion

        /// <summary>
        /// Internal method which implements the case where RemoveReference decrements the RefCount to 0 and the object belongs to a pool.
        /// Confirms that the given objRef refers to this object (breakpoint if not) then invokes the PerformPostReleaseCleanup method and invokes the
        /// releaseObjectToPoolDelegate delegate to return the object to the pool (or release it).
        /// </summary>
        private void ReleaseFinalObjectReferenceAndReturnToPool(ref ObjectType objRef)
		{
            RefCountedRefObjectBase<ObjectType> objRefAsMe = objRef as RefCountedRefObjectBase<ObjectType>;
            bool objectsAreTheSame = (this == objRefAsMe);

            if (!objectsAreTheSame)
                Asserts.TakeBreakpointAfterConditionCheckFailed("reference given to RemoveReference must refer to the the invoked object");
            if (refCount.VolatileValue != 0)
                Asserts.TakeBreakpointAfterConditionCheckFailed(Utils.Fcns.CheckedFormat("ReleaseFinalObjectReferenceAndReturnToPool called with non-zero refCount:{0}", refCount));

            // perform object specific cleanup
            PerformPostReleaseCleanup();

            // call Pool's delegate to return this object to the pool
            // delegate is known to be non-null due to BelongsToPool conditional check in calling RemoveReference method
            releaseObjectToPoolDelegate(ref objRef);

            // caller will dispose of objRef if above code did not.
        }

        /// <summary>
        /// Virtual method which may be implemented by derived class to reset the contents of the object immediately prior to its release back to the source Pool.  This method will not be called by the RemoveReference if the object does not belong to a Pool at the time that the "final" reference is released.
        /// </summary>
		protected virtual void PerformPostReleaseCleanup() { }

        private ReleaseObjectToPoolDelegate<ObjectType> releaseObjectToPoolDelegate = null;
		private AtomicInt32 refCount = new AtomicInt32(1);
	}

	#endregion
}

//-------------------------------------------------------------------
