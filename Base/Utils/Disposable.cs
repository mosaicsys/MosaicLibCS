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

    //-------------------------------------------------------------------
    #region Helper functions and classes

    /// <summary>Provide placeholder to put DisposeOfObject method</summary>
	public static partial class Fcns
	{
		/// <summary>Helper function used to dispose of things that can be casted to an IDisposable type.  oRef.Dispose() method is only called if ObjType can be casted to an IDisposable object.</summary>
		/// <typeparam name="ObjType">Any type of ref object.  May be a type that is castable to IDisposable.</typeparam>
		/// <param name="oRef">Gives a reference to the ref object that the caller wants to dispose of.  Referenced handle will be set to null before any underlying Dispose is performed.</param>
		public static void DisposeOfObject<ObjType>(ref ObjType oRef) where ObjType : class
		{
			IDisposable d = oRef as IDisposable;

            oRef = null;
            
            if (d != null)
				d.Dispose();
		}
    }

    /// <summary>
    /// This class is used to invoke a simple delegate on explicit disposal of the object.  It is generally used to trigger the delegate to be invoked when flow of control leaves the execution block for a using instruction.
    /// </summary>
    public class InvokeDelegateOnDispose : DisposableBase
    {
        public delegate void DisposeDelegate();
        public InvokeDelegateOnDispose(DisposeDelegate disposeDelegate) { this.disposeDelegate = disposeDelegate; }
        private DisposeDelegate disposeDelegate = null;

        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (disposeType == DisposeType.CalledExplicitly && disposeDelegate != null)
            {
                try
                {
                    disposeDelegate();
                }
                catch { }

                disposeDelegate = null;
            }
        }
    }

    #endregion

    //-------------------------------------------------------------------
    #region DisposableBase and DisposableBaseBase

    /// <summary>Defines the base class of the DisposableBase class.</summary>
	/// <remarks>
	/// Provides default virual implementation of Dispose method.  It is necessary to provide this explicit base class for the 
	/// DisposableBase class so that it can mark its override of this method as sealed.
	/// </remarks>
	public class DisposableBaseBase : System.IDisposable
	{
		public virtual void Dispose() { }
	}

	/// <summary>
	/// This class is an abstract base class that defines and mostly implements a version the 
	/// standard CLR IDisposable/Dispose/Finalize pattern.  
	/// </summary>
	/// <remarks>
	/// This version uses a slight variation on the standard user provided protected Dispose 
	/// method where this version provides an enum to indicate which type of call is being performed.  
	/// In addition this base class implements certain gaurd and assertion constructs to detect and 
	/// record if the the public Dispose() method is used in non-standard ways (reenterantly or 
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
			CalledExplicitly,		/// <summary>Dispose called by IDisposable.Dispose method - perform full cleanup</summary>
			CalledByFinalizer,		/// <summary>Dispose called by finalizer method - perform local cleanup only.  Access to subordinate objects with finalizers is not safe as they may, or may not, have been finalized already</summary>
		}

		/// <summary>
		/// This abstract method is implemented by a derived class and is used to perform resource cleanup either
		/// when a user of this object explicitly invokes its public Dispose() method or when this object's Finalizer is called.
		/// </summary>
		/// <param name="disposeType">
		/// Defines the source of the Dispose call.  Used by invoked method to detemine if it can safely access subordinate reference objects
		/// </param>
		/// <remarks>
		/// When invoked Explicitly this method should release all held resources and should invoke Dispose on any IDisposable reference objects that it is the
		/// unique owner of.  
		/// When invoked by the Finalizer this method should explicitly release any unmanaged resources that it has obtained but must not invoke any methods on
		/// non-null reference object handles that it continues to have access to.
		/// </remarks>

		protected abstract void Dispose(DisposeType disposeType);

		public virtual bool IsDisposed { get { return isDisposed; } }
		public virtual bool IsDisposing { get { return (activeDisposeCounter.VolatileValue != 0); } }

		public DisposableBase() { }

		public sealed override void Dispose()
		{
			// Prevent recursion and concurrent use:
			// we cannot directly invoke the real dispose method if it has already been invoked by either this or another thread

			if (!EnteringDispose())
			{
				Assert.BreakpointFault("DisposableBase::Attempt to invoke Dispose() renterantly or concurrently");
				return;
			}

			try
			{
				if (!IsDisposed)
				{
					this.Dispose(DisposeType.CalledExplicitly);

					System.GC.SuppressFinalize(this);

					isDisposed = true;
				}
			}
			catch (System.Exception ex)
			{
				Assert.BreakpointFault("DisposableBase::Dispose(CalledExplicitly) triggered exception", ex);
				throw;
			}
			finally
			{
				LeavingDispose();
			}
		}

		/// <summary> This is the Finalizer for this object hierarchy.  It will invoke Dispose(CalledByFinalizer). </summary>
		/// <remarks>Derived classes should not implement their own Finalizer.  Instead they should perform all related 
		/// actions within the context of the Dispose(type) method that this Finalizer will invoke.  Please keep in mind that
		/// the use of the explicit Dispose() method (such as when using "using") will invoke the abstract Dispose(type) 
		/// method and then will surpress the use of the Finalizer for this object.  As such the Finalizer will not be 
		/// invoked if the object was successfully Disposed first.
		/// </remarks>

		~DisposableBase() 
		{
			// This is only entered when the finalizer is invoked and that should only occur in a non-renternat manner on a single thread.
			//	presumably the finalizer is only invoked once the sandbox has no references to an object that requires finalization and as such
			//  the finalizer infrastructure will be the only source of any thread that may continue to attempt to access this object.

            if (!EnteringDispose())
    			Assert.BreakpointFault("DisposableBase: unexpected renterant or concurrent use of Finalizer detected");

			try
			{
				this.Dispose(DisposeType.CalledByFinalizer);
			}
			catch (System.Exception ex)
			{
				Assert.BreakpointFault("DisposableBase::Dispose(CalledByFinalizer) triggered exception", ex);
			}

            // we leave the activeDisposeCounter non-zero so that any later call will take a breakpoint
		}

		#region Private methods and variables

		/// <summary>Called prior to invoking actual Dispose method.  Allows caller to determine if this is the first attempt to enter the Dispose(type) method or if this is a recursive or concurrent invokation.</summary>
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

		private bool isDisposed = false;
		private AtomicInt32 activeDisposeCounter = new AtomicInt32(0);

		#endregion
    }

    #endregion
}

//-------------------------------------------------------------------
