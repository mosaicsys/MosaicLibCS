//-------------------------------------------------------------------
/*! @file MMTimer.cs
 * @brief This file defines interfaces and classes that help give the client access to and use of the windows multi-media related timer functions.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2002 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

namespace MosaicLib.Time
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Static "namespace" for P-Invoke method names used to interact with winmm.dll
    /// </summary>
    public static class winmm_dll
    {
        /// <summary>
        /// winmm.dll timeBeginPeriod call.  Consult appropriate Win32 documentation for full details on parameters and use.
        /// </summary>
        [DllImport("winmm.dll")]
		internal static extern uint timeBeginPeriod(uint uMilliseconds);

        /// <summary>
        /// winmm.dll timeEndPeriod call.  Consult appropriate Win32 documentation for full details on parameters and use.
        /// </summary>
        [DllImport("winmm.dll")]
		internal static extern uint timeEndPeriod(uint uMilliseconds);

        /// <summary>
        /// winmm.dll timeGetTime call.  Consult appropriate Win32 documentation for full details on parameters and use.
        /// </summary>
        [DllImport("winmm.dll")]
        public static extern uint timeGetTime();
    }

    /// <summary>
    /// This class has been deprecated and replaced by the newer version called winmm_dll
    /// </summary>
    [Obsolete("Use of this class has been deprecated and replaced by the newer version called winmm_dll. (2013-06-16)")]
    public static class MMTimer
    {
        /// <summary>
        /// winmm.dll timeBeginPeriod call.  Consult appropriate Win32 documentation for full details on parameters and use.
        /// </summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint uMilliseconds);

        /// <summary>
        /// winmm.dll timeEndPeriod call.  Consult appropriate Win32 documentation for full details on parameters and use.
        /// </summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint uMilliseconds);

        /// <summary>
        /// winmm.dll timeGetTime call.  Consult appropriate Win32 documentation for full details on parameters and use.
        /// </summary>
        [DllImport("winmm.dll")]
        public static extern uint timeGetTime();
    }

    /// <summary>
    /// This class is used as a lifetime wrapper for the request to the Win32 kernel to increase the system mm timer resolution.
    /// On construction this class uses winmm_dll.timeBeginPeriod and on explicit disposal this class performs the matching call to winmm_dll.timeEndPeriod.
    /// </summary>
	public class MMTimerPeriod : Utils.DisposableBase
	{
        /// <summary>Constrution calls winmm_dll.timeBeginPeriod(1)</summary>
		public MMTimerPeriod() : this(1) {}

		/// <summary>Constrution calls winmm_dll/timeBeginPeriod(uMilliseconds)</summary>
		public MMTimerPeriod(uint uMilliseconds) 
		{
			periodMilliseconds = uMilliseconds;

            if (0 == winmm_dll.timeBeginPeriod(periodMilliseconds))
				periodHasBeenSet = true;
		}

		#region DisposableBase Members

        /// <summary>
        /// Implementation for abstract DisposableBase: regardless of DisposeType, if the timer period has been set then this method will invoke winmm_dll.timeEndPeriod to release the acquired timer resolution.
        /// </summary>
        /// <param name="type"></param>
		protected override void Dispose(DisposeType type)
		{
			if (periodHasBeenSet)
			{
				periodHasBeenSet = false;
                winmm_dll.timeEndPeriod(periodMilliseconds);
			}
		}

		#endregion

		#region Member variables

        /// <summary>Contains the requested timer resolution from the construction call</summary>
		private uint periodMilliseconds = 0;
        /// <summary>Flag indicates if construction call to timeBeginPeriod was successfull, or not.</summary>
        private bool periodHasBeenSet = false;

		#endregion
	}
}

//-------------------------------------------------------------------
