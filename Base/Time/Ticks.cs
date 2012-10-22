//-------------------------------------------------------------------
/*! @file Ticks.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2012 Mosaic Systems Inc.  All rights reserved
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

    #region GetTickCount

    public static class Ticks
    {
        [DllImport("Kernel32.dll")]
        public static extern UInt32 GetTickCount();
    }

    #endregion
}

//-------------------------------------------------------------------
