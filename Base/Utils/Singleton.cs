//-------------------------------------------------------------------
/*! @file Singleton.cs
 *  @brief This file contains helper classes for creation and use of singletons
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2011 Mosaic Systems Inc.
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

namespace MosaicLib.Utils
{
	//-------------------------------------------------

    /// <summary>
    /// Interface implemented by Singleton helper object(s) provided here.  Gives user access to a common Instance, either constructed explicitly or on first use.
    /// </summary>
    public interface ISingleton<TSingletonInstanceProperty> where TSingletonInstanceProperty : class
    {
        /// <summary>
        /// Gives caller access to the underlying Singleton Instance.  
        /// Behavior of this property is dependent on the Instance Behavior and my include AutoConstruct on first use, ManuallyAssign but Must be NonNull on first 
		/// use (or use throws), or ManuallyAssign and may be null.
        /// </summary>
        /// <exception cref="MosaicLib.Utils.SingletonException">
        /// Thrown on attempt to get value from this property when it is not auto construct, has not been assigned a non-null value and null is not a valid value.
        /// Also Thrown on any attempt by the constructor to invoke, or depend on static construction, that attempts to implicitly, recursively, re-accesses the 
		/// Singleton Instance property on the original thread that is constructing the first singleton instance.
        /// </exception>
        /// <remarks>
		/// Underlying object construction exceptions for AutoConstruct case are not caught and will throw through to the caller's code that accesses the 
		/// Instance if it cannot be successfully constructed on first use.
		/// </remarks>
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
        /// <summary>
        /// Constructor - passes the given message through to the base constructor.
        /// </summary>
        public SingletonException(string message) : base(message) { }
    }

    /// <summary>
    /// Enum defines throw related behavior for the Singleton's Instance method
    /// </summary>
    public enum SingletonInstanceBehavior
    {
        /// <summary>First use of Instance property will automatically construct the instance using default constructor.  Instance cannot be provided externally.</summary>
        AutoConstruct = 0,

        /// <summary>First use of Instance property will automatically construct the instance using default constructor if the Instance has not been assigned by that point.  Instance may be provided externally or it may be automatically constructed.  Instance may be explicitly assigned to null to remove the previously obtained instance and it may be set to be non-null if there is no currently defined instance.  To replace the instance, set it to null and then to the next value or use the getter to create one automatically.</summary>
        AutoConstructIfNeeded,

        /// <summary>Instance property is manually assigned.  It must be assigned non-null value prior to first use of Instance property by Singleton user/client code.</summary>
        ManuallyAssign_MustBeNonNull,

        /// <summary>Instance property is manually assigned.  It may be null or non-null at any point that a Singleton user/client attempts to use it.</summary>
        ManuallyAssign_MayBeNull,
    }

    /// <summary>
    /// Local extentsion methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given singletonInstanceBehavior is one of the values that allows a caller to assign the Instance directly.
        /// <para/>AutoConstructIfNeeded, ManuallyAssign_MayBeNull, ManuallyAssign_MustBeNonNull
        /// </summary>
        public static bool InstanceCanBeManuallyAssignedBehavior(this SingletonInstanceBehavior singletonInstanceBehavior)
        {
            return (singletonInstanceBehavior == SingletonInstanceBehavior.AutoConstructIfNeeded 
                    || singletonInstanceBehavior == SingletonInstanceBehavior.ManuallyAssign_MayBeNull 
                    || singletonInstanceBehavior == SingletonInstanceBehavior.ManuallyAssign_MustBeNonNull
                    );
        }

        /// <summary>
        /// Returns true if the given singletonInstanceBehavior that may attempt to construct the instance when first required.
        /// <para/>AutoConstruct, AutoConstructIfNeeded
        /// </summary>
        public static bool IsAutoConstructBehavior(this SingletonInstanceBehavior singletonInstanceBehavior)
        {
            return (singletonInstanceBehavior == SingletonInstanceBehavior.AutoConstruct 
                    || singletonInstanceBehavior == SingletonInstanceBehavior.AutoConstructIfNeeded
                    );
        }

        /// <summary>
        /// Returns true if the singletonInstanceBehavior allows the Instance getter to return null.
        /// <para/>ManuallyAssign_MayBeNull
        /// </summary>
        public static bool DoesBehaviorPermitInstanceGetterToReturnNull(this SingletonInstanceBehavior singletonInstanceBehavior)
        {
            return (singletonInstanceBehavior == SingletonInstanceBehavior.ManuallyAssign_MayBeNull);
        }

        /// <summary>
        /// Returns true if the singletonInstanceBehavior allows the Instance to be set to null.
        /// <para/>ManuallyAssign_MayBeNull, AutoConstructIfNeeded
        /// </summary>
        public static bool DoesBehaviorPermitInstanceToBeSetToNull(this SingletonInstanceBehavior singletonInstanceBehavior)
        {
            return (singletonInstanceBehavior == SingletonInstanceBehavior.ManuallyAssign_MayBeNull || singletonInstanceBehavior == SingletonInstanceBehavior.AutoConstructIfNeeded);
        }
    }

    /// <summary>
    /// Singleton Helper class.  
    /// This class is reentrant and support MT safe use.  
    /// Two constructors are supported.  Default constructor for this object selects SingletonInstanceBehavior.AutoConstruct.  
    /// Second constructor allows caller to explicitly define the instance behavior.
    /// </summary>
    /// <typeparam name="TSingletonObject">
    /// This type parameter gives both the type of the Instance property and is the type that will be constructed using its 
    /// default constructor for AutoConstruct behavior.  This type must be a class and must support a default constructor.
    /// </typeparam>
    public class SingletonHelper<TSingletonObject> : SingletonHelper<TSingletonObject, TSingletonObject>
        where TSingletonObject : class, new()
    {
        /// <summary>
        /// Default constructor.  Constructs a SingletonHelper with AutoConstruct behavior.
        /// Resulting SingletonHelper may be used as IDisposable and supports use with SingletonObjectTypes that are either IDisposable or not.
        /// </summary>
        public SingletonHelper() 
            : base(SingletonInstanceBehavior.AutoConstruct) 
        { }

        /// <summary>
        /// Base constructor.  Constructs a SingletonHelper with client given behavior.
        /// Resulting SingletonHelper may be used as IDisposable and supports use with SingletonObjectTypes that are either IDisposable or not.
        /// </summary>
        /// <param name="behavior">Defines the Instance property construction and use behavior: AutoConsruct, ManuallyAssign_MustBeNonNull or ManuallyAssign_MayBeNull</param>
        public SingletonHelper(SingletonInstanceBehavior behavior) 
            : base(behavior) 
        { }
    }

    /// <summary>
    /// Singleton Helper class with separate specification of the type that the Instance property returns and of the type that is constructed for AutoConstruct behaviors.
    /// This class is reentrant and support MT safe use.  
    /// Two constructors are supported.  Default constructor for this object selects SingletonInstanceBehavior.AutoConstruct.  
    /// Second constructor allows caller to explicitly define the instance behavior.
    /// </summary>
    /// <typeparam name="TSingletonInstanceProperty">
    /// Defines the type of object that is returned by the SingletonHelper's Instance property.  It must be a class or interface.
    /// </typeparam>
    /// <typeparam name="TSingletonObject">
    /// This type parameter gives the type of object that will be constructed using its default constructor for AutoConstruct behavior.
    /// This type must be a class, it must support a default constructor and it must be castable to the TSingletonInstanceProperty type.
    /// </typeparam>
    public class SingletonHelper<TSingletonInstanceProperty, TSingletonObject>
        : SingletonHelperBase<TSingletonInstanceProperty>
        where TSingletonObject : class, TSingletonInstanceProperty, new()
        where TSingletonInstanceProperty : class
    {
        /// <summary>
        /// Default constructor.  Constructs a SingletonHelper with AutoConstruct behavior.
        /// Resulting SingletonHelper may be used as IDisposable and supports use with SingletonObjectTypes that are either IDisposable or not.
        /// </summary>
        public SingletonHelper() 
            : this(SingletonInstanceBehavior.AutoConstruct) 
        { }

        /// <summary>
        /// Base constructor.  Constructs a SingletonHelper with client given behavior.
        /// Resulting SingletonHelper may be used as IDisposable and supports use with SingletonObjectTypes that are either IDisposable or not.
        /// </summary>
        /// <param name="behavior">Defines the Instance property construction and use behavior: AutoConsruct, ManuallyAssign_MustBeNonNull or ManuallyAssign_MayBeNull</param>
        public SingletonHelper(SingletonInstanceBehavior behavior) 
            : base(behavior, () => new TSingletonObject() as TSingletonInstanceProperty)
        { }
    }

    /// <summary>
    /// Singleton Helper Base class which uses a delegate to construct the instance when needed.
    /// This class is reentrant and support MT safe use.  
    /// </summary>
    /// <typeparam name="TSingletonInstanceProperty">
    /// Defines the type of object that is returned by the SingletonHelper's Instance property.  It must be a class or interface type.
    /// </typeparam>
    public class SingletonHelperBase<TSingletonInstanceProperty>
        : Utils.DisposableBase, System.IDisposable, ISingleton<TSingletonInstanceProperty>
        where TSingletonInstanceProperty : class
    {
        #region Construction and Destruction

        /// <summary>
        /// This delegate define the Func type that will be given to the SingletonHelperBase to allow it to construct an instance when needed.
        /// </summary>
        /// <returns>The constructed singleton instance casted as the given TSingletonInstanceProperty type.</returns>
        public delegate TSingletonInstanceProperty InstanceConstructionDelegate();

        /// <summary>
        /// Default constructor.  Constructs a SingletonHelperBase with AutoConstruct behavior and client given InstanceConstructionDelegate.
        /// Resulting SingletonHelper may be used as IDisposable and supports use with SingletonObjectTypes that are either IDisposable or not.
        /// </summary>
        /// <param name="instanceConstructionDelegate">Gives the delegate that will be invoked to construct the instance object if/when that is required</param>
        public SingletonHelperBase(InstanceConstructionDelegate instanceConstructionDelegate) 
            : this(SingletonInstanceBehavior.AutoConstruct, instanceConstructionDelegate) 
        { }

        /// <summary>
        /// Base constructor.  Constructs a SingletonHelperBase with client given behavior and InstanceConstructionDelegate.
        /// Resulting SingletonHelper may be used as IDisposable and supports use with SingletonObjectTypes that are either IDisposable or not.
        /// </summary>
        /// <param name="behavior">Defines the Instance property construction and use behavior: AutoConsruct, ManuallyAssign_MustBeNonNull or ManuallyAssign_MayBeNull</param>
        /// <param name="instanceConstructionDelegate">Gives the delegate that will be invoked to construct the instance object if/when that is required.  Must not be null if the given behavior may AutoConstruct the instance.</param>
        /// <exception cref="SingletonException">thrown if instanceConstructionDelegate is null and the given behavior may attempt to AutoConstruct the instance.</exception>
        public SingletonHelperBase(SingletonInstanceBehavior behavior, InstanceConstructionDelegate instanceConstructionDelegate) 
        { 
            Behavior = behavior;
            this.instanceConstructionDelegate = instanceConstructionDelegate;

            if (instanceConstructionDelegate == null && behavior.IsAutoConstructBehavior())
                throw new SingletonException("Attempt to use a null instanceConstructionDelegate with {0} behavior".CheckedFormat(behavior));
        }

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

        #region ISingleton<TSingletonInstanceProperty> Members

        /// <summary>
        /// Get property returns the current instance.  
        /// If the stored instance is null and the createInstanceOnFirstUse constructor property was true (default) then the new instance is constructed using the 
		/// default constructor.
        /// Set property assigns a non-null instance to the internal storage.  
        /// </summary>
        /// <exception cref="SingletonException">
        /// Get property throws exception if instance is null and null is not defined as a legal instance value (optional constructor parameter)
        /// Set property throws exception if the given value is null or if the contained instance is non-null at the time of the assignment.  
		/// In some Behavior dependent cases this property may first be assigned to null and then to a second instance to dispose of the current instance (if present) 
		/// and then replace it with another.  Generally this is only safe to do in setup and/or test conditions.
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

                        if (value == null && Behavior.IsAutoConstructBehavior())
                        {
                            if (recursiveConstructionCount.Increment() != 1)
                            {
                                throw new SingletonException("Invalid Recursive using of SingletonHelper.Instance property, likely within the constructor for the instance.");

                                //Warning: the above exception has taken some time to discover the need for.  
                                // This code guards against a singleton object's constructor using other entities that may accidentally
                                // attempt to use the Singleton before its construction is complete.  The mutex used here does not block the same thread calling back into the Instance property
                                // and attempting this second construction and as such this exception is intended to highlight the probability of this situation existing in the SingletonObjectType
                                //  classes code.  Failure to block this case produces unexpected results since at minimum two singleton objects are created, of which only one is retained.
                            }

                            value = instanceConstructionDelegate();

                            if (value == null)
                                throw new SingletonException("instanceConstructionDelegate returned null.");

                            recursiveConstructionCount.Decrement();

                            // only update the volatile copy after the construction of the underlying object is complete.
                            instance = value;
                        }
                    }

                    if (value == null && !Behavior.DoesBehaviorPermitInstanceGetterToReturnNull())
                        throw new SingletonException("Attempt to get Instance from {0} SingletonHelper before Instance was assigned to a non-null value".CheckedFormat(Behavior));
                }

                return value;
            }

            set
            {
                lock (instanceMutex)
                {
                    if (!Behavior.InstanceCanBeManuallyAssignedBehavior())
                        throw new SingletonException("Attempt to assign value to {0} SingletonHelper Instance".CheckedFormat(Behavior));

                    if (IsDisposed)
                        throw new SingletonException("Attempt to set Instance after SingletonHelper has been disposed");

                    if (value == null && !Behavior.DoesBehaviorPermitInstanceToBeSetToNull())
                        throw new SingletonException("Attempt to set Instance to null value with {0} behavior".CheckedFormat(Behavior));

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
        /// This private read only field holds the underlying delegate that is used to construct the instance if/when the SingltonHelper needs to do so.
        /// </summary>
        private readonly InstanceConstructionDelegate instanceConstructionDelegate = null;

        /// <summary>
        /// Returns true if the held instance reference is currently non-null.  This method is non-blocking and will not cause any SingletonObjectType object to be constructed.
        /// </summary>
        public bool InstanceExists { get { return (instance != null); } }

        #endregion

        #region private fields and properties

        /// <summary>mutex object for access to change instance field</summary>
        private readonly object instanceMutex = new object();

        /// <summary>volatile reference to constructed or held singleton object</summary>
        private volatile TSingletonInstanceProperty instance = null;
      
        /// <summary>Instance recursion counter used to detect recursive use of Instance during auto construction of SingletonObjectType object.</summary>
        private AtomicInt32 recursiveConstructionCount = new AtomicInt32(0);

        #endregion
    }

	//-------------------------------------------------
}

//-------------------------------------------------
