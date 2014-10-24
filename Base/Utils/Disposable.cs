//-------------------------------------------------------------------
/*! @file Disposable.cs
 *  @brief This file defines the DisposableBase class that may be used to help implement a well structured IDisposable pattern in derived classes.
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

namespace MosaicLib.Utils
{
	using System;
    using System.Collections.Generic;

    //-------------------------------------------------------------------
    #region Helper functions and classes

    /// <summary>
    /// Fcns class is essentially a namespace for series of static helper methods
    /// <para/>inclues: DisposeOf... methods, CheckedFormat and other String related methods, array/list specific Equals methods, ...
    /// </summary>
    public static partial class Fcns
	{
		/// <summary>
        /// Helper function used to dispose of things who's type can be casted to an IDisposable type.  
        /// This method captures the handle in the referenced variable or field casted as an IDisposable object.
        /// Then it sets the referenced variable or field to null and finally if the cast produced a non-null object then the object's Dispose method is invoked.
        /// </summary>
		/// <typeparam name="ObjType">Any type of ref object.  May be a type that is castable to IDisposable.</typeparam>
		/// <param name="oRef">
        /// Gives a reference to the ref object that the caller wants to dispose of.  
        /// The given referenced handle will be set to null just before the object's Dispose method is called
        /// </param>
		public static void DisposeOfObject<ObjType>(ref ObjType oRef) where ObjType : class
		{
			IDisposable d = oRef as IDisposable;

            oRef = null;
            
            if (d != null)
				d.Dispose();
		}

        /// <summary>
        /// Helper function used to dispose of things that can be casted to an IDisposable type.
        /// obj.Dispose() method is only called if ObjType can be casted to an IDisposable object.
        /// This method has no effect if the underlying object cannot be casted to the IDisposable type.
        /// </summary>
		/// <typeparam name="ObjType">Any type of ref object.  May be a type that is castable to IDisposable.</typeparam>
        /// <param name="obj">Gives the object that is to be disposed.</param>
        public static void DisposeOfGivenObject<ObjType>(ObjType obj) where ObjType : class
        {
            IDisposable d = obj as IDisposable;

            if (d != null)
                d.Dispose();
        }
    }

    /// <summary>
    /// Instances of this class may be used to invoke a simple delegate on explicit disposal of each instance.  
    /// It is generally used to trigger the delegate to be invoked when flow of control leaves the execution block for a using instruction.
    /// </summary>
    public class InvokeDelegateOnDispose : DisposableBase
    {
        /// <summary>
        /// Define the Delegate type that will be invoked on explicit Dispose of this object.
        /// </summary>
        public delegate void DisposeDelegate();

        /// <summary>
        /// Constructor:  Caller provides the delegate to be invoked on explicit dispose of this object.
        /// </summary>
        /// <param name="disposeDelegate"></param>
        public InvokeDelegateOnDispose(DisposeDelegate disposeDelegate) { this.disposeDelegate = disposeDelegate; }

        /// <summary>
        /// Internal storage for the delegate to be invoked on explicit dispose
        /// </summary>
        private DisposeDelegate disposeDelegate = null;

        /// <summary>
        /// Implementation method for DisposableBase.  Called by Disposable base either for explicit dispose or as part of the finalizer pattern.
        /// </summary>
        /// <param name="disposeType">Gives the dispose type.  DisposeType.CalledExplicitly will trigger the contained delegate to be invoked.  DisposeType.CalledByFinalizer will not.</param>
        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (disposeType == DisposeType.CalledExplicitly && disposeDelegate != null)
            {
                disposeDelegate();

                disposeDelegate = null;
            }
        }
    }

    #endregion

    //-------------------------------------------------------------------
    #region DisposableBase and DisposableBaseBase

    /// <summary>Defines the base class of the DisposableBase class.</summary>
	/// <remarks>
	/// Provides default virtual implementation of Dispose method.  It is necessary to provide this explicit base class for the 
	/// DisposableBase class so that it can mark its override of this method as sealed.
	/// </remarks>
	public class DisposableBaseBase : System.IDisposable
	{
        /// <summary>
        /// Defines the placeholder public external symbol that is used so that this class can implement IDisposable.  
        /// This pattern allows us to override and seal this method in the DisposableBase derived class so that classes that inherit from it cannot re-implement the public Dispose method.
        /// Implementation of this method in this class is empty.
        /// </summary>
		public virtual void Dispose() { }
	}

	/// <summary>
	/// This class is an abstract base class that defines and mostly implements a version the 
	/// standard CLR IDisposable/Dispose/Finalize pattern. 
    /// This class provides both a virtual method that may be overriden to field Dispose(DisposeType) 
    /// calls as well as a Action delegate list pattern using the AddExplicitDisposeAction method.
	/// </summary>
	/// <remarks>
	/// This version uses a slight variation on the standard user provided protected Dispose 
	/// method where this version provides an enum to indicate which type of call is being performed.  
	/// In addition this base class implements certain guard and assertion constructs to detect and 
	/// record if the the public Dispose() method is used in non-standard ways (reentrantly or 
	/// concurrently on 2 or more threads).
	/// 
	/// For more information on this pattern see the msdn.microsoft.com library under the topic
	/// titled "Implementing Finalize and Dispose to Clean Up Unmanaged Resources".
	/// </remarks>

	public abstract class DisposableBase : DisposableBaseBase
	{
		/// <summary>Defines the variations under which the protected abstract Dispose(type) method may be called: CalledExplicitly, CalledByFinalizer</summary>
		public enum DisposeType
		{
            /// <summary>Dispose called by IDisposable.Dispose method - perform full cleanup</summary>
            CalledExplicitly,
            /// <summary>Dispose called by finalizer method - perform local cleanup only.  Access to subordinate objects with finalizers is not safe as they may, or may not, have been finalized already</summary>
            CalledByFinalizer,
		}

		/// <summary>
		/// This virtual method may be overriden by a derived class and is used to perform resource cleanup either
		/// when a user of this object explicitly invokes its public Dispose() method or when this object's Finalizer is called.
        /// The default implementation does nothing directly, however the internal method that calls this method for the DisposeType.CalledExplicitly case
        /// will also invoke each of the System.Action delegates that have been given to this base class using the AddExplicitDisposeAction method.
		/// </summary>
		/// <param name="disposeType">
		/// Defines the source of the Dispose call.  Used by invoked method to determine if it can safely access subordinate reference objects
		/// </param>
		/// <remarks>
		/// When invoked Explicitly this method should release all held resources and should invoke Dispose on any IDisposable reference objects that it is the
		/// unique owner of.  
		/// When invoked by the Finalizer this method should explicitly release any unmanaged resources that it has obtained but must not invoke any methods on
		/// non-null reference object handles that it continues to have access to.
		/// </remarks>
        protected virtual void Dispose(DisposeType disposeType)
        { 
        }

        /// <summary>
        /// true whenever the object has been Disposed explicitly and the System.GC.SuppressFinalize method has completed.
        /// This IsDisposing and IsDisposed may both be set simultaneously for a brief period.
        /// </summary>
        public virtual bool IsDisposed { get; private set; }
        /// <summary>
        /// true whenever either of the object's Dispose patterns are currently in use.  
        /// This IsDisposing and IsDisposed may both be set simultaneously for a brief period.
        /// </summary>
		public virtual bool IsDisposing { get { return (activeDisposeCounter.VolatileValue != 0); } }

        /// <summary>
        /// Default constructor.
        /// </summary>
		public DisposableBase() { }

        /// <summary>
        /// This method provides the sealed public implementation for the System.IDisposable.Dispose explicit dispose method.  
        /// Internally this method invokes the Dispose(DisposeType.CalledExplicitly) overload to pass the explicit dispose request to the derived class(es).
        /// Then it invokes System.GC.SurpressFinalize(this); and finally it sets the IsDisposed property to true.
        /// </summary>
		public sealed override void Dispose()
		{
			// Prevent recursion and concurrent use:
			// we cannot directly invoke the real dispose method if it has already been invoked by either this or another thread

			if (!EnteringDispose())
			{
				Asserts.TakeBreakpointAfterFault("DisposableBase::Attempt to invoke Dispose() reentrantly or concurrently");
				return;
			}

			try
			{
				if (!IsDisposed)
				{
                    // call the virtual Dispose method first.
					this.Dispose(DisposeType.CalledExplicitly);

                    // when it is done, if any explicit Dispose actions have been added using the AddExplicitDisposeAction method then we capture the array of such
                    //  actions and invoke them in revers order of their addition.
                    if (explicitDisposeActionList != null)
                    {
                        Action[] explicitDisposeActionArray = explicitDisposeActionList.ToArray();
                        explicitDisposeActionList = null;

                        for (int idx = explicitDisposeActionArray.Length - 1; idx >= 0; idx--)
                        {
                            Action explicitDisposeAction = explicitDisposeActionArray[idx];
                            explicitDisposeAction();
                        }
                    }

                    // suppress the GC finalize for this object after the derived class injected dispose behavior has been completed.
					System.GC.SuppressFinalize(this);

                    IsDisposed = true;
				}
			}
			catch (System.Exception ex)
			{
				Asserts.TakeBreakpointAfterFault("DisposableBase::Dispose(CalledExplicitly) triggered exception", ex);
				throw;
			}
			finally
			{
				LeavingDispose();
			}
		}

		/// <summary> This is the Finalizer for this object hierarchy.  It will invoke Dispose(DisposeType.CalledByFinalizer). </summary>
		/// <remarks>Derived classes should not implement their own Finalizer.  Instead they should perform all related 
		/// actions within the context of the Dispose(type) method that this Finalizer will invoke.  Please keep in mind that
		/// the use of the explicit Dispose() method (such as when using "using") will invoke the explicit Dispose 
		/// pattern which also suppresses the use of the Finalizer for this object.  As such the Finalizer will not be 
		/// invoked if the object was explicitly Disposed first.
		/// </remarks>
		~DisposableBase() 
		{
			// This is only entered when the finalizer is invoked and that should only occur in a non-reentrant manner on a single thread.
			//	presumably the finalizer is only invoked once the sandbox has no other references to this object that requires finalization and as such
			//  the finalizer infrastructure will be the only source of any thread that may continue to attempt to access this object.

            if (!EnteringDispose())
    			Asserts.TakeBreakpointAfterFault("DisposableBase: unexpected reentrant or concurrent use of Finalizer detected");

			try
			{
				this.Dispose(DisposeType.CalledByFinalizer);
			}
			catch (System.Exception ex)
			{
				Asserts.TakeBreakpointAfterFault("DisposableBase::Dispose(CalledByFinalizer) triggered exception", ex);
			}

            // we leave the activeDisposeCounter non-zero so that any later call will take a breakpoint
        }

        #region Explict Dispose Action list and related methods

        /// <summary>
        /// Sub-class callable method used to add an explicitDisposeAction to the explicitDisposeActionList.  
        /// All such added actions will be invoked in LIFO order when the part is being explicitly disposed.
        /// For SimpleActiveParts this will occure after the part has been stopped and after the DisposeCalledPassdown(disposeType) has taken place.
        /// </summary>
        protected void AddExplicitDisposeAction(Action explicitDisposeAction)
        {
            if (explicitDisposeActionList == null)
                explicitDisposeActionList = new List<Action>();

            explicitDisposeActionList.Add(explicitDisposeAction);
        }

        /// <summary>
        /// This is a private list of actions that will be performed during an explicit dispose.  
        /// <para/>Items in this list will be invoked by the Dispose() method after the inner Dispose(type) method returns.  Itms will be invoked in reverse of the order these items are added to the list.
        /// </summary>
        private List<Action> explicitDisposeActionList = null;

        #endregion

        #region Private methods and variables

        /// <summary>Called prior to invoking actual Dispose method.  Allows caller to determine if this is the first attempt to enter the Dispose(type) method or if this is a recursive or concurrent invocation.</summary>
		/// <returns>true if this is the only active attempt to enter the Dispose method, false otherwise (due to attempted recursion or concurrent use on more than one thread)</returns>
		private bool EnteringDispose()
		{
			return (activeDisposeCounter.Increment() == 1);
		}

		/// <summary> Called to indicate that Dispose method has been completed. </summary>
		private void LeavingDispose()
		{
			activeDisposeCounter.Decrement();
		}

		private AtomicInt32 activeDisposeCounter = new AtomicInt32(0);

		#endregion
    }

    #endregion

    //-------------------------------------------------------------------
    #region DisposableList

    namespace Collections
    {
        /// <summary>
        /// This object is a variant of a generic List that implements the IDisposable pattern.  
        /// It is intended to help support cases where a group of disposable objects are kept as a set and where the client
        /// would like to be able to dispose of them cleanly and easily by disposing of the list itself.
        /// </summary>
        /// <typeparam name="ItemType">Gives the reference or value type that is to be managed by the List.  This type must implement the IDisposable interface.</typeparam>
        public class DisposableList<ItemType> : List<ItemType>, IDisposable where ItemType : IDisposable
        {
            /// <summary>
            /// Initializes a new instance of the list class that is empty and has the default initial capacity.
            /// </summary>
            public DisposableList() { }

            /// <summary>
            /// Initializes a new instance of the list class that 
            /// contains elements copied from the specified collection and has sufficient
            /// capacity to accommodate the number of elements copied.
            /// </summary>
            /// <param name="collection">The collection whose elements are copied to the new list.</param>
            /// <exception cref="System.ArgumentNullException">collection is null.</exception>
            public DisposableList(IEnumerable<ItemType> collection) : base(collection) { }

            /// <summary>
            /// Initializes a new instance of the list class that is empty and has the specified initial capacity.
            /// </summary>
            /// <param name="capacity">The number of elements that the new list can initially store.</param>
            /// <exception cref="System.ArgumentOutOfRangeException">capacity is less than 0.</exception>
            public DisposableList(int capacity) : base(capacity) { }

            #region Disposable implementation

            /// <summary>
            /// true after this object has been explicitly disposed and the System.GC.SuppressFinalize has succeeded.  false otherwise.
            /// </summary>
            public bool IsDisposed { get; private set; }

            /// <summary>
            /// Internal implementation for handling explicit and finalizer based dispose to generally follow the pattern used for DisposableBase.
            /// When invoked explicitly, this method invokes the System.IDisposable.Dispose methods on each of the non-null items in the list.
            /// </summary>
            /// <param name="disposeType">DisposeType being handled.</param>
            protected void Dispose(DisposableBase.DisposeType disposeType)
            {
                if (disposeType == DisposableBase.DisposeType.CalledExplicitly)
                {
                    foreach (ItemType item in this)
                    {
                        if (item != null)
                            item.Dispose();
                    }
                }
            }

            #endregion

            #region IDisposable Members and related implementation

            /// <summary>
            /// Implementation for System.IDisposable.Dispose method.  
            /// Invokes the Dispose(DisposableBase.DisposeType.CalledExplicitly) method then System.GC.SuppressFinalize(this) and then sets IsDisposed to true.
            /// </summary>
            public void Dispose()
            {
                try
                {
                    if (!IsDisposed)
                    {
                        this.Dispose(DisposableBase.DisposeType.CalledExplicitly);

                        System.GC.SuppressFinalize(this);

                        IsDisposed = true;
                    }
                }
                catch (System.Exception ex)
                {
                    Asserts.TakeBreakpointAfterFault("DisposableList::Dispose(CalledExplicitly) triggered exception", ex);
                    throw;
                }
            }

            /// <summary>
			/// This is the Finalizer for this object hierarchy.  It will invoke Dispose(DisposableBase.DisposeType.CalledByFinalizer). 
			/// </summary>
            ~DisposableList()
            {
                try
                {
                    this.Dispose(DisposableBase.DisposeType.CalledByFinalizer);
                }
                catch (System.Exception ex)
                {
                    Asserts.TakeBreakpointAfterFault("DisposableList::Dispose(CalledByFinalizer) triggered exception", ex);
                }
            }
        }

        #endregion
    }

    #endregion
}

//-------------------------------------------------------------------
