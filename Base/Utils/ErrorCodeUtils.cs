//-------------------------------------------------------------------
/*! @file ErrorCodeUtils.cs
 *  @brief This file contains defintions of a set of methods that are usefull for converting Win32 error codes into more useful formats.
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

namespace MosaicLib.Utils
{
    /// <summary>
    /// Class acts as a namespace for a small set of static Error code related helper methods.
    /// </summary>
	public static partial class EC
	{
		public static string FmtWin32EC(string objID, int win32EC)
		{
			string errAsStr = CvtWin32ECTostring(win32EC, "Win32NoError", "Win32UnknownError");

			return FmtStdEC(objID, win32EC, errAsStr);
		}

		public static string FmtStdEC(string objID, string errorStr)
		{
			if (string.IsNullOrEmpty(objID))
				return "Err:" + errorStr;
			else
				return objID + ":Err:" + errorStr;
		}

		public static string FmtStdEC(string objID, int errorCode, string errorStr)
		{
			if (string.IsNullOrEmpty(objID))
				return Fcns.CheckedFormat("Err:{0}_{1}", errorCode, errorStr);
			else
				return Fcns.CheckedFormat("{0}:Err:{1}_{2}", objID, errorCode, errorStr);
		}

		public static string CvtWin32ECTostring(int win32EC, string noErrorReturnStr, string unknownErrorReturnStr)
		{
			if (win32EC == 0)
				return noErrorReturnStr;

			System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception(win32EC);
			return e.Message;
		}
	}
}

//-----------------------------------------------------------------
