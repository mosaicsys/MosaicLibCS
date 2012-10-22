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
	using System.Runtime.InteropServices;

    public static class MMTimer
    {
        [DllImport("winmm.dll")]
		internal static extern uint timeBeginPeriod(uint uMilliseconds);

		[DllImport("winmm.dll")]
		internal static extern uint timeEndPeriod(uint uMilliseconds);

        [DllImport("winmm.dll")]
        public static extern uint timeGetTime();
    }

	public class MMTimerPeriod : Utils.DisposableBase
	{
        /// <summary>Constrution requests winmm.dll/timeBeginPeriod(0)</summary>
		public MMTimerPeriod() : this(0) {}

		/// <summary>Constrution requests winmm.dll/timeBeginPeriod(uMilliseconds)</summary>
		public MMTimerPeriod(uint uMilliseconds) 
		{
			periodMilliseconds = uMilliseconds;

			if (0 == MMTimer.timeBeginPeriod(periodMilliseconds))
				periodHasBeenSet = true;
		}

		#region DisposableBase Members

		protected override void Dispose(DisposeType type)
		{
			if (periodHasBeenSet)
			{
				periodHasBeenSet = false;
				MMTimer.timeEndPeriod(periodMilliseconds);
			}
		}

		#endregion

		#region Member variables

		uint periodMilliseconds = 0;
		bool periodHasBeenSet = false;

		#endregion
	}
}

//-------------------------------------------------------------------
