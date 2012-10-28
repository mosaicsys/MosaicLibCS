//-------------------------------------------------------------------
/*! @file Pooling.cs
 *  @brief This file defines the MosaicLib.Utils.Pooling namespace which provides a set of utility definitions and classes that are useful for implementing reference counted and pooled objects.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version embodied in PoolIface.h and PoolImpl.h)
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

namespace MosaicLib.Utils.Pooling
{
	using System;
	using System.Collections.Generic;

	#region Pooling Intefaces

    /// <summary>
    /// Defines a delegate that is used by a pooled object to allow it to release itself to the pool from which the object was created.  
    /// Use of a delegate allows the pool to combine its own reference and the private method it uses to return objects to the pool without requireing that the pooled object knows anything about the pool to which it belongs.
    /// </summary>
	public delegate void ReleaseObjectToPoolDelegate<ObjType>(ref ObjType oRef);

    /// <summary>This interface defines the behavior that clients of reference counted objects use to add and remove references to the object and to query its current reference count state.</summary>
    /// <remarks>
    /// Please note that IRefCountedObject's generally support both pooled and non-pooled use.  
    /// Objects create within a pool may be handled and returned to the pool as long as all relevant external code honors the reference count sementics.  
    /// Objects that are manually created outside of a pool do not belong to any pool and will not be returned to any based on use of RemoveReference.
    /// Internal assertions about correct use of AddReference and RemoveReference are only enforced for objects that were created by, and belong to, a specific pool.
    /// </remarks>
    public interface IRefCountedObject<ObjectType> where ObjectType : class
	{
        /// <summary>Allows the caller to create a new reference to the object.  Returns the object on which the operation was performed.</summary>
        ObjectType AddReference();
        /// <summary>Allows the caller to release a reference to an object and thus decrement the contained reference count.  If object belongs to a pool then it will be released to that pool once the reference count returns to zero.  Caller must pass the referring field or variable by reference as the method nulls the contents of the referenced handle.</summary>
        void RemoveReference(ref ObjectType refHandle);
        /// <summary>Returns the current refernece count</summary>
        int RefCount { get; }
        /// <summary>Returns true if the current RefCount is exactly 1</summary>
        bool IsUnique { get; }
        /// <summary>Returns true if the object was created by a pool and can be returned to it.</summary>
        bool BelongsToPool { get; }
	}

    /// <summary>This interface is used by the Pool itself to assign the ReleaseObjectToPoolDelegate after a new object has been added to the pool.</summary>
    public interface IPoolableRefCountedObject<ObjectType> : IRefCountedObject<ObjectType> where ObjectType : class, new()
	{
        /// <summary>Pool is allowed to get or set the ReleaseObjectToPoolDelegate.  Property may only be set when object IsUnique</summary>
        ReleaseObjectToPoolDelegate<ObjectType> ReleaseObjectToPoolDelegate { get; set; }

        /// <summary>Used during initial building of pool objects</summary>
        void DecrementRefCount();

        /// <summary>
        /// Method that is used by pool or by object implementation to remove object references that are not returned to a pool 
        /// (ether because the object does not belong to one or because the pool is no longer enabled or has already reached its capacity limit)
        /// If ObjectType implements IDisposable, then the refHandle's Dispose method will be invoked by this methed.
        /// </summary>
        void DisposeOfSelf(ref ObjectType selfObjRef);
	}

    /// <summary>This interface defines the public interface for Object Pools based on object types that support the IPoolableRefCountedObject interface.</summary>
    public interface IObjectPool<ObjectType>
		where ObjectType : class, IPoolableRefCountedObject<ObjectType>, new()
	{
        /// <summary>Method allows caller to obtain a free object from the pool.  Returned object will either be newly created for will have been returned to the pool when its reference count was decremented to zero.</summary>
        ObjectType GetFreeObjectFromPool();
        /// <summary>Returns the maximum number of pool objects that may be retained in the pool.  Additional objects will be dropped (and will be collected by the GC).</summary>
        int Capacity { get; }
        /// <summary>Returns the current number of object in the pool.</summary>
        int Count { get; }
        /// <summary>Releases all objects from the pool and disables the return of objects to the pool.  Subsiquent calls to GetFreeObjectFromPool will explicitly create objects and will not associate them with the pool.</summary>
        void Shutdown();
	}

	#endregion

	#region Pooled object base classe

    /// <summary>This is a common base class that may be used as the base for objects that need to implement the IRefCountedObject and IPoolableRefCountedObject interfaces.</summary>
    /// <remarks>
    /// This class is defined as abstract to indicate that it cannot be directly constructed.
    /// 
    /// If ObjectType is IDisposable, calls to RemoveReference will directly Dispose of the object when the final reference has been removed and the object does not belon to a pool.
    /// As such use of this base is not recommended for Objects which are also IDisposable unless the user is fully aware of this side effect of the final call to RemoveReferences.
    /// </remarks>
    public abstract class RefCountedRefObjectBase<ObjectType> : IPoolableRefCountedObject<ObjectType> where ObjectType : class, new()
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
                    DisposeOfSelf(ref objRef);     // directly call our own DiposeOfObject method to clear the given handle and, optionally, dispose of th eobject.
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
                // NOTE: the following tests are performed explicitly to prevent calling into Utils.Assert static class (and thus attemting to construct a new Logger instance before the distribution signleton has been constructed.
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
            if (typeof(ObjectType) is IDisposable)
                Utils.Fcns.DisposeOfObject(ref selfObjRef);
            else
                selfObjRef = null;
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

	#region ObjectPool class

    /// <summary>This generic class provides the primary implementation for the IObjectPool generic interface.</summary>
    /// <typeparam name="ObjectType">Non-abstract object type that supports the IPoolableRefCountedObject interface.  Supports ObjectTypes that are IDisposable.</typeparam>
    /// <remarks>
    /// Addition of support for IDisposable ObjectType's adds some complexity and release cost to this class.  
    /// Given the rate at which ObjectPool's are generally created and disposed and given the added value of supporting disposable objects in the pool, the added overhead
    /// is viewed as acceptable.
    /// </remarks>

    public class ObjectPool<ObjectType> 
        : Utils.DisposableBase
        , IObjectPool<ObjectType>
		where ObjectType : class, IPoolableRefCountedObject<ObjectType>, new()
	{
		#region Constructor and Destructor

		const int DefaultPoolSize = 1000;
		const int DefaultCapacity = 10000;
		const int MinimumCapacity = 10;

        /// <summary>Default constructor - uses default initial pool size and capacity of 1000, and 10000 respectively.</summary>
		public ObjectPool() : this(DefaultPoolSize, DefaultCapacity) {}

        /// <summary>Constructor - caller defines the initial pool size and capacity.  Method enfores minimum capaticy of initialPoolSize and 10, whichever is greater.</summary>
        public ObjectPool(int initialPoolSize, int initialCapacity) 
		{
			if (initialCapacity < initialPoolSize)
				initialCapacity = initialPoolSize;
			if (initialCapacity < MinimumCapacity)
				initialCapacity = MinimumCapacity;

			freeObjectStack = new Stack<IPoolableRefCountedObject<ObjectType>>(initialCapacity);
			freeObjectStackCapacity = initialCapacity;

            roReleaseObjectToPoolDelegate = ImplementReleaseObjectToPoolDelegate;

            poolIsEnabled = true;

            // explicitly allocated initialPoolSize elements and tell each one that we have removed the initial reference.  
            // as such Initial objects enter the pool the same way that normal objects to.

            for (int count = 0; count < initialPoolSize; count++)
			{
				ObjectType obj = ConstructNewObject();
                obj.DecrementRefCount();            // skip the full release handler
                freeObjectStack.Push(obj);
			}
		}

        /// <summary>implementation method for DisposableBase.  On explicit dispose, this method will perform a Shutdown if needed so as to dispose of all objects in the stack.</summary>
        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (poolIsEnabled && disposeType == DisposeType.CalledExplicitly)
                Shutdown();
        }

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
        public int Capacity { get { lock (freeObjectStackMutex) { return freeObjectStackCapacity; } } }

        /// <summary>Returns the number of free objects in the pool.</summary>
        public int Count { get { lock (freeObjectStackMutex) { return freeObjectStack.Count; } } }

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

        #endregion

		#region Private instance methods

        /// <summary>readonly instance to the ReleaseObjectToPoolDelegate for this pool.</summary>
        private readonly ReleaseObjectToPoolDelegate<ObjectType> roReleaseObjectToPoolDelegate;

        /// <summary>This method invokes default new() constructor on ObjectType and then assigns the resulting object's ReleaseObjectToPoolDelegate if this pool is currently enabled.</summary>
        private ObjectType ConstructNewObject()
        {
            ObjectType obj = new ObjectType();

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

        /// <summary>volatile bool used to determine if the pool is enabled.  Pool constrution enables the pool.  Explicit call to Shutdown or explicit disposal of this pool object. </summary>
        private volatile bool poolIsEnabled = false;

        /// <summary>object used as mutex for access to freeObjectStack and freeObjectStackCapacity.</summary>
        object freeObjectStackMutex = new object();

        /// <summary>Stack of free objects that are currently in the pool.  Use of Fifo semantics is choosen to generally improve cache and virtual memory efficiency.</summary>
        Stack<IPoolableRefCountedObject<ObjectType>> freeObjectStack = null;

        /// <summary>field defines the maximum number of objects that the freeObjectStack can hold.</summary>
        int freeObjectStackCapacity = 0;

        #endregion
    }

	#endregion
}

//-------------------------------------------------------------------
