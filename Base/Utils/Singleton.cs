//-------------------------------------------------------------------
/*! @file Singleton.cs
 * @brief This file contains helper classes for creation and use of singletons
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
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

    /// <summary>
    /// Interface implemented by Singleton helper object(s) provided here.  Gives user access to a common Instance, either constructed explicitly or on first use.
    /// </summary>
    public interface ISingleton<TSingletonInstanceProperty> where TSingletonInstanceProperty : class
    {
        /// <summary>
        /// Gives caller access to the unerlying Singleton Instance.  
        /// Behavior of this property is dependent on the Instance Behavior and my include AutoConstruct on first use, ManuallyAssign but Must be NonNull on first use (or use throws), or ManuallyAssign and may be null.
        /// </summary>
        /// <exception cref="MosaicLib.Utils.SingletonException">
        /// Thrown on attempt to get value from this property when it is not auto construct, has not been assigned a non-null value and null is not a valid value.
        /// Also Thrown on any attempt by the constructor to invoke, or depend on static construction, that attempts to implicitly, recursively, re-accesses the Singleton Instance perperty on the original thread that is constructing the first singleton instance.
        /// </exception>
        /// <remarks>Underlying object construction exceptions for AutoConstruct case are not caught and will throw through to the caller's code that accesses the Instance if it cannot be successfully constructed on first use.</remarks>
        TSingletonInstanceProperty Instance { get; }

        /// <summary>
        /// Gives the caller access to see what behavior the singleton has
        /// </summary>
        SingletonInstanceBehavior Behavior { get; }

        /// <summary>
        /// Returns true if the Instance has already been constructed and assigned so that the property will return a non-null value.
        /// </summary>
        bool InstanceExists { get; }
    }

    /// <summary>
    /// Singleton specific Exception class.
    /// </summary>
    public class SingletonException : System.Exception
    {
        public SingletonException(string message) : base(message) { }
    }

    /// <summary>
    /// Enum defines throw related behavior for the Singleton's Instance method
    /// </summary>
    public enum SingletonInstanceBehavior
    {
        /// <summary>First use of Instance property will automatically construct the instance using default constructor.  Instance cannot be provided externally.</summary>
        AutoConstruct = 0,
        /// <summary>First use of Instance property will automatically construct the instance using default constructor if the Instance has not been assigned by that point.  Instance may be provided externally or it may be automatically constructed.</summary>
        AutoConstructIfNeeded,
        /// <summary>Instance property is manually assigned.  It must be assigned non-null value prior to first use of Instance property by Singleton user/client code.</summary>
        ManuallyAssign_MustBeNonNull,
        /// <summary>Instance property is manually assigned.  It may be null or non-null at any point that a Singleton user/client attempts to use it.</summary>
        ManuallyAssign_MayBeNull,
    }

    /// <summary>
    /// Singleton Helper class.  
    /// This class is renterant and support MT safe use.  
    /// Two constructors are supported.  Default constructor for this object selects SingletonInstanceBehavior.AutoConstruct.  
    /// Second constructor allows caller to explicitly define the instance behavior.
    /// </summary>
    /// <typeparam name="TSingletonObject">
    /// This type parameter gives both the type of the Instance property and is the type that will be constructed using its 
    /// defafult constructor for AutoConstruct behaviour.  This type must be a class and must support a default constructor.
    /// </typeparam>
    public class SingletonHelper<TSingletonObject> : SingletonHelper<TSingletonObject, TSingletonObject>
        where TSingletonObject : class, new()
    {
        public SingletonHelper() : base() { }
        public SingletonHelper(SingletonInstanceBehavior behavior) : base(behavior) { }
    }

    /// <summary>
    /// Singleton Helper class with seperate specifictation of the type that the Instance property returns and of the type that is constructed for AutoConstruct behaviors.
    /// This class is renterant and support MT safe use.  
    /// Two constructors are supported.  Default constructor for this object selects SingletonInstanceBehavior.AutoConstruct.  
    /// Second constructor allows caller to explicitly define the instance behavior.
    /// </summary>
    /// <typeparam name="TSingletonInstanceProperty">
    /// Defines the type of object that is returned by the SingletonHelper's Instance property.  It must be a class or interface.
    /// </typeparam>
    /// <typeparam name="TSingletonObject">
    /// This type parameter gives the type of object that will be constructed using its defafult constructor for AutoConstruct behaviour.
    /// This type must be a class, it must support a default constructor and it must be castable to the TSingletonInstanceProperty type.
    /// </typeparam>
    public class SingletonHelper<TSingletonInstanceProperty, TSingletonObject>
        : Utils.DisposableBase, System.IDisposable, ISingleton<TSingletonInstanceProperty>
        where TSingletonObject : class, TSingletonInstanceProperty, new()
        where TSingletonInstanceProperty : class
    {
        #region Construction and Destruction

        /// <summary>
        /// Default constructor.  Constructs a SingletonHelper with AutoConstruct behavior.
        /// Resulting SingletonHelper may be used as IDisposable and supports use with SingletonObjectTypes that are either IDisposable or not.
        /// </summary>
        public SingletonHelper() : this(SingletonInstanceBehavior.AutoConstruct) { }

        /// <summary>
        /// Base constructor.  Constructs a SingletonHelper with client given behavior.
        /// Resulting SingletonHelper may be used as IDisposable and supports use with SingletonObjectTypes that are either IDisposable or not.
        /// </summary>
        /// <param name="behavior">Defines the Instance property construction and use behavior: AutoConsruct, ManuallyAssign_MustBeNonNull or ManuallyAssign_MayBeNull</param>
        public SingletonHelper(SingletonInstanceBehavior behavior) { Behavior = behavior; }

        /// <summary>
        /// Callback method from DisposableBase on explicit destruction via client explicitly calling IDisposable.Dispose use or when called by finalizer.
        /// Method nulls held reference to instance and disposes of held instance (if it is/was IDisposable).
        /// </summary>
        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (disposeType == DisposeType.CalledExplicitly)
            {
                lock (instanceMutex)
                {
                    InnerDispose();
                }
            }
        }

        #endregion

        #region InnerDispose

        private void InnerDispose()
        {
            if (instance != null)
            {
                // we do not use Utils.Fcns.DisposeOfObject because instance is declared as volatile.
                System.IDisposable id = instance as System.IDisposable;

                // prevent Instance property from seeing object
                instance = null;

                // dispose of it (or not as appropriate)
                if (id != null)
                    id.Dispose();
            }
        }

        #endregion

        #region ISingleton<SingletonObjectType> Members

        /// <summary>
        /// Get property returns the current instance.  
        /// If the stored instance is null and the createInstanceOnFirstUse ctor property was true (default) then the new instance is constructed using the default constructor.
        /// Set property assigns a non-null instance to the internal storage.  
        /// </summary>
        /// <exception cref="SingletonException">
        /// Get property throws exception if instance is null and null is not defined as a legal instance value (optional ctor parameter)
        /// Set property throws exception if the given value is null or if the contained instance is non-null at the time of the assignment.  In some Behavior dependent cases this property may first be assigned to null and then to a second instance to dispose of the current instance (if present) and thne replace it with another.  Generally this is only safe to do in setup and/or test conditions.
        /// </exception>

        public TSingletonInstanceProperty Instance
        {
            get 
            {
                TSingletonInstanceProperty value = instance;

                if (value == null)
                {
                    if (IsDisposed)
                        throw new SingletonException("Attempt to get Instance after SingletonHelper has been disposed");

                    lock (instanceMutex)
                    {
                        value = instance;

                        if (value == null && CreateInstanceOnFirstUse)
                        {
                            if (recursiveConstructionCount.Increment() != 1)
                            {
                                throw new SingletonException("Invalid Recursive using of SingletonHelper.Instance property, likely within the constructor for the instance.");

                                //Warning: the above exception has taken some time to discover the need for.  
                                // This code guards against a singleton object's constructor using other entities that may accidentally
                                // attempt to use the Singleton before its construction is complete.  The mutex used here does not block the same thread calling back into the Instance property
                                // and attempting this second construction and as such this excpetion is intended to highlight the probability of this situation exising in the SingletonObjectType
                                //  classes code.  Failure to block this case produces unexpected results since at minimum two singleton objects are created, of which only one is retained.
                            }

                            value = new TSingletonObject();

                            recursiveConstructionCount.Decrement();
                        }

                        instance = value;   // do not update the volatile until the construction of the underlying object is complete.
                    }
                }

                if (value == null && !NullIsLegalInstanceValue)
                    throw new SingletonException("Attempt to retrieve Instance from SingletonHelper before any singleton object was assigned");

                return value;
            }

            set
            {
                lock (instanceMutex)
                {
                    if (!InstanceMayBeAssigned)
                        throw new SingletonException("Attempt to assign value to SingletonHelper whose Behavior does not permit this");

                    if (IsDisposed)
                        throw new SingletonException("Attempt to set Instance after SingletonHelper has been disposed");

                    if (value == null && !NullIsLegalInstanceValue)
                        throw new SingletonException("Attempt to set Instance to illegal null value");

                    if (instance != null && value != null)
                        throw new SingletonException("Attempt to replace existing non-null Instance");

                    if (instance != null && value == null)
                        InnerDispose();

                    instance = value;
                }
            }
        }

        /// <summary>
        /// Returns the constructed Instance behavior for this SingletonHelper object
        /// </summary>
        public SingletonInstanceBehavior Behavior { get; private set; }

        /// <summary>
        /// Returns true if the held instance reference is currently non-null.  This method is non-blocking and will not cause any SingletonObjectType object to be constructed.
        /// </summary>
        public bool InstanceExists { get { return (instance != null); } }

        #endregion

        #region private fields and properties

        /// <summary>True if behavior is AutoConstruct</summary>
        private bool CreateInstanceOnFirstUse { get { return (Behavior == SingletonInstanceBehavior.AutoConstruct || Behavior == SingletonInstanceBehavior.AutoConstructIfNeeded); } }

        /// <summary>True if behavior is AutoConstruct</summary>
        private bool InstanceMayBeAssigned 
        { 
            get 
            {
                switch (Behavior)
                {
                    case SingletonInstanceBehavior.AutoConstruct: return false;
                    case SingletonInstanceBehavior.AutoConstructIfNeeded: return true;
                    case SingletonInstanceBehavior.ManuallyAssign_MayBeNull: return true;
                    case SingletonInstanceBehavior.ManuallyAssign_MustBeNonNull: return true;
                    default: return false;
                }
            } 
        }

        /// <summary>True if behavior is ManuallyAssign_MayBeNull</summary>
        private bool NullIsLegalInstanceValue { get { return (Behavior == SingletonInstanceBehavior.ManuallyAssign_MayBeNull || Behavior == SingletonInstanceBehavior.AutoConstructIfNeeded); } }

        /// <summary>mutex object for access to change instance field</summary>
        private readonly object instanceMutex = new object();
        /// <summary>volatile refernece to constructed or held singleton object</summary>
        private volatile TSingletonInstanceProperty instance = null;
        /// <summary>Instance recursion counter used to detect recursive use of Instance during auto construction of SingletonObjectType object.</summary>
        private AtomicInt32 recursiveConstructionCount = new AtomicInt32(0);

        #endregion
    }

	//-------------------------------------------------
}

//-------------------------------------------------
