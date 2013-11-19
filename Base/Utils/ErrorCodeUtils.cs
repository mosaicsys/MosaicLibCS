//-------------------------------------------------------------------
/*! @file ErrorCodeUtils.cs
 *  @brief This file contains definitions of a set of methods that are useful for converting Win32 error codes into more useful formats.
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
        /// <summary>
        /// Combines the use of CvtWin32ECToString and FmtStdEC to generate an error string from an optional objID and a Win32 Error Code.
        /// </summary>
		public static string FmtWin32EC(string objID, int win32EC)
		{
			string errAsStr = CvtWin32ECToString(win32EC, "Win32NoError", "Win32UnknownError");

			return FmtStdEC(objID, win32EC, errAsStr);
		}

        /// <summary>
        /// Utility method for combining an optional objID and a given errorStr as "Err:" + errorStr, or objID + ":Err:" + errorStr.
        /// </summary>
		public static string FmtStdEC(string objID, string errorStr)
		{
			if (string.IsNullOrEmpty(objID))
				return "Err:" + errorStr;
			else
				return objID + ":Err:" + errorStr;
		}

        /// <summary>
        /// Generates a formatted string version of the given errorCode and errorStr.  Includes the given objID if it is neither null nor empty.
        /// </summary>
		public static string FmtStdEC(string objID, int errorCode, string errorStr)
		{
			if (string.IsNullOrEmpty(objID))
				return Fcns.CheckedFormat("Err:{0}_{1}", errorCode, errorStr);
			else
				return Fcns.CheckedFormat("{0}:Err:{1}_{2}", objID, errorCode, errorStr);
		}

        /// <summary>
        /// static helper method that is used to convert Win32 DWORD error codes into strings.  
        /// Does this by creating a Win32Exception from the given win32EC error code value and then returns the Message that the exception created.
        /// </summary>
        /// <param name="win32EC">Gives the value of the Win32 Error Code that is to be converted to a string.</param>
        /// <param name="noErrorReturnStr">Gives the value to return when win32EC is zero</param>
        /// <param name="unknownErrorReturnStr">Unused - ignored.</param>
        /// <returns>noErrorReturnStr if win32EC is zero or Message from constructed Win32Exception if win32EC is not zero.</returns>
		public static string CvtWin32ECToString(int win32EC, string noErrorReturnStr, string unknownErrorReturnStr)
		{
			if (win32EC == 0)
				return noErrorReturnStr;

			System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception(win32EC);
			return e.Message;
		}

        /// <summary>
        /// static helper method that is used to convert Win32 DWORD error codes into strings.  
        /// Does this by creating a Win32Exception from the given win32EC error code value and then returns the Message that the exception created.
        /// </summary>
        /// <param name="win32EC">Gives the value of the Win32 Error Code that is to be converted to a string.</param>
        /// <param name="noErrorReturnStr">Gives the value to return when win32EC is zero</param>
        /// <param name="unknownErrorReturnStr">Unused - ignored.</param>
        /// <returns>noErrorReturnStr if win32EC is zero or Message from constructed Win32Exception if win32EC is not zero.</returns>
        [System.Obsolete("This method name is not spelled correctly.  Please replace its use with the correctly named CvtWin32ECToString. (2013-06-16)")]
        public static string CvtWin32ECTostring(int win32EC, string noErrorReturnStr, string unknownErrorReturnStr)
        {
            return CvtWin32ECToString(win32EC, noErrorReturnStr, unknownErrorReturnStr);
        }
    }
}

//-----------------------------------------------------------------
