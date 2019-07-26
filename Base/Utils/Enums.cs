//-------------------------------------------------------------------
/*! @file Enums.cs
 *  @brief This file contains a number of enum related helper methods
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
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
using System.Text;
using System.Collections.Generic;

namespace MosaicLib.Utils
{
    #region Enum

    /// <summary>
    /// Enum class is essentially a namespace for series of static Enum related helper methods
    /// </summary>
    public static partial class Enum
	{
		#region TryParse variants and Parse

        /// <summary>
        /// Helper function to parse a string s as an enum of type EnumT
        ///     Uses System.Enum.Parse to convert the string representation of the name or numeric value of one or
        ///     more enumerated constants to an equivalent enumerated object. 
        /// </summary>
        public static EnumT TryParse<EnumT>(this string s, EnumT parseFailedResult = default(EnumT), bool ignoreCase = true, bool autoTrim = true)
        {
            EnumT result;

            s.Parse(out result, fallbackValue: parseFailedResult, ignoreCase: ignoreCase, autoTrim: autoTrim, rethrow: false);

            return result;
        }

        /// <summary>
        /// Helper function to parse a string s as an enum of type EnumT
        ///     Uses System.Enum.Parse to convert the string representation of the name or numeric value of one or
        ///     more enumerated constants to an equivalent enumerated object. 
        ///     A parameter specifies whether the operation is case-sensitive.
        /// </summary>
        /// <remarks>
        /// NOTE: !!!!! The following code cannot use System.Enum.TryParse as the addition of the required "where" clause would prevent the use of this method in the StringScanner.ParseValue generic methods.
        /// </remarks>
        public static bool TryParse<EnumT>(this string s, out EnumT result, EnumT parseFailedResult = default(EnumT), bool ignoreCase = true, bool autoTrim = true)
        {
            return s.Parse(out result, fallbackValue: parseFailedResult, ignoreCase: ignoreCase, autoTrim: autoTrim, rethrow: false);
        }

        /// <summary>
        /// Helper function that is used to call System.Enum.Parse with optional catching of any exception that it throws.  
        /// Returns true if the parse succeeded, or false if rethrow is false and the parse did not, or could not, succeed.
        /// </summary>
        public static bool Parse<EnumT>(this string s, out EnumT result, EnumT fallbackValue = default(EnumT), bool ignoreCase = true, bool autoTrim = true, bool rethrow = true)
		{
			Type enumT = typeof(EnumT);

            result = fallbackValue;

            try
            {
                if (s != null && autoTrim)
                    s = s.Trim();

                if (s.IsNullOrEmpty() && !rethrow)
                    return false;

                result = (EnumT)System.Enum.Parse(enumT, s, ignoreCase);
                return true;
            }
            catch (System.Exception ex)
            {
                // In theory the above use of System.Enum.Parse can generate a System.ArgumentExecption, an System.ArgumentException, or a System.OverflowException
                //  Documentation for System.Enum.Parse added possibility of throw for System.OverflowExecption in documentation for .Net 3.5 (was not in 2.0 or 3.0)
                //  Catching all System.Exceptions here is still safe since the risk of additional undocumented exceptions that can be thrown is much larger than the
                //  risk that we will fail to pass on some other unexpected type of exception that is not a direct result of calling System.Enum.Parse.

                if (rethrow && ex != null)
                    throw;

                return false;
            }
		}

        /// <summary>
        /// Trial alternative version of TryParse.  This one is based directly on System.Enum.TryParse and suppports that same set of optional parameters as the current TryParse local variant does.
        /// </summary>
        private static bool TryParse2<EnumT>(this string s, out EnumT result, EnumT parseFailedResult = default(EnumT), bool ignoreCase = true, bool autoTrim = true)
            where EnumT : struct
        {
            Type enumT = typeof(EnumT);

            if (!enumT.IsEnum)
                throw new System.ArgumentException("Type:'{0}' is not usable with Utils.Enum.{1}.  It must be a System.Enum".CheckedFormat(typeof(EnumT), Fcns.CurrentMethodName));

            if (s != null)
                s = s.Trim();

            if (!s.IsNullOrEmpty())
            {
                if (System.Enum.TryParse(s, ignoreCase, out result))
                    return true;
            }

            result = parseFailedResult;
            return false;
        }

		#endregion
	}

	#endregion
}

//-------------------------------------------------------------------
