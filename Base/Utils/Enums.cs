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
		#region TryParse

        /// <summary>
        /// Helper function to parse a string s as an enum of type EnumT
        ///     Uses System.Enum.Parse to convert the string representation of the name or numeric value of one or
        ///     more enumerated constants to an equivalent enumerated object. 
        /// </summary>
        /// <typeparam name="EnumT">The specific Enum type to parse.  This method will throw if this is not derived from System.Enum</typeparam>
		/// <param name="s">The string to parse</param>
		/// <returns>parsed EnumT value on success, or default(EnumT) on failure</returns>
		/// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
		public static EnumT TryParse<EnumT>(string s)
		{
			return TryParse<EnumT>(s, default(EnumT), true);
		}

        /// <summary>
        /// Helper function to parse a string s as an enum of type EnumT
        ///     Uses System.Enum.Parse to convert the string representation of the name or numeric value of one or
        ///     more enumerated constants to an equivalent enumerated object. 
        /// </summary>
        /// <typeparam name="EnumT">The specific Enum type to parse.  This method will throw if this is not derived from System.Enum</typeparam>
		/// <param name="s">The string to parse</param>
		/// <param name="parseFailedResult">Defines the EnumT value that will be returned if the parse fails</param>
		/// <returns>parsed EnumT value on success, or parseFailedResult on failure</returns>
		/// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
		public static EnumT TryParse<EnumT>(string s, EnumT parseFailedResult)
		{
			return TryParse<EnumT>(s, parseFailedResult, true);
		}

        /// <summary>
        /// Helper function to parse a string s as an enum of type EnumT
        ///     Uses System.Enum.Parse to convert the string representation of the name or numeric value of one or
        ///     more enumerated constants to an equivalent enumerated object. 
        /// </summary>
        /// <typeparam name="EnumT">The specific Enum type to parse.  This method will throw if this is not derived from System.Enum</typeparam>
        /// <param name="s">The string to parse</param>
        /// <param name="parseFailedResult">Defines the EnumT value that will be returned if the parse fails</param>
        /// <param name="ignoreCase">If true, ignore case; otherwise, regard case.</param>
        /// <returns>parsed EnumT value on success, or parseFailedResult on failure</returns>
        /// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
        public static EnumT TryParse<EnumT>(string s, EnumT parseFailedResult, bool ignoreCase)
        {
            EnumT result;

            TryParse<EnumT>(s, out result, parseFailedResult, ignoreCase);

            return result;
        }

        /// <summary>
        /// Helper function to parse a string s as an enum of type EnumT
        ///     Uses System.Enum.Parse to convert the string representation of the name or numeric value of one or
        ///     more enumerated constants to an equivalent enumerated object. 
        /// </summary>
        /// <typeparam name="EnumT">The specific Enum type to parse.  This method will throw if this is not derived from System.Enum</typeparam>
        /// <param name="s">The string containing the value to parse into the specified EnumT type.</param>
        /// <param name="result">Assigned to the Parsed value on success or the default(EnumT) on failure</param>
        /// <returns>True if the Parse was successful, false otherwise</returns>
        /// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
        public static bool TryParse<EnumT>(string s, out EnumT result)
		{
			return TryParse<EnumT>(s, out result, default(EnumT), true);
		}

        /// <summary>
        /// Helper function to parse a string s as an enum of type EnumT
        ///     Uses System.Enum.Parse to convert the string representation of the name or numeric value of one or
        ///     more enumerated constants to an equivalent enumerated object. 
        /// </summary>
        /// <typeparam name="EnumT">The specific Enum type to parse.  This method will throw if this is not derived from System.Enum</typeparam>
        /// <param name="s">The string containing the value to parse into the specified EnumT type.</param>
        /// <param name="result">Assigned to the Parsed value on success or to parseFailedResult on failure</param>
        /// <param name="parseFailedResult">Defines the EnumT value that will be assigned to the result if the parse itself fails.</param>
        /// <returns>True if the Parse was successful, false otherwise</returns>
        /// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum.</exception>
        public static bool TryParse<EnumT>(string s, out EnumT result, EnumT parseFailedResult)
        {
            return TryParse<EnumT>(s, out result, parseFailedResult, true);
        }

        /// <summary>
        /// Helper function to parse a string s as an enum of type EnumT
        ///     Uses System.Enum.Parse to convert the string representation of the name or numeric value of one or
        ///     more enumerated constants to an equivalent enumerated object. 
        ///     A parameter specifies whether the operation is case-sensitive.
        /// </summary>
        /// <typeparam name="EnumT">The specific Enum type to parse.  This method will throw if this is not derived from System.Enum</typeparam>
        /// <param name="s">The string containing the value to parse into the specified EnumT type.</param>
        /// <param name="result">Assigned to the Parsed value on success or to parseFailedResult on failure</param>
        /// <param name="parseFailedResult">Defines the EnumT value that will be assigned to the result if the parse itself fails.</param>
        /// <param name="ignoreCase">If true, ignore case; otherwise, regard case.</param>
        /// <returns>True if the Parse was successful, false otherwise</returns>
        /// <exception cref="System.InvalidOperationException">Thrown if EnumT is not a type of enum. (where clause only requires it is a type of struct)</exception>
        public static bool TryParse<EnumT>(string s, out EnumT result, EnumT parseFailedResult, bool ignoreCase)
		{
			Type enumT = typeof(EnumT);

			if (!enumT.IsEnum)
				throw new System.InvalidOperationException(Fcns.CheckedFormat("Type:'{0}' is not usable with Utils.Enum.TryParse.  It must be a System.Enum", typeof(EnumT).ToString()));

            try
            {
                if (s == null)
                {
                    result = parseFailedResult;
                    return false;
                }

                result = (EnumT)System.Enum.Parse(typeof(EnumT), s, ignoreCase);
                return true;
            }
            catch (System.Exception)
            {
                // we expect System.ArgumentExecption or System.InvalidCastException but have observed System.OverflowException as well
                //  Documentation for System.Enum.Parse added possibility of throw for System.OverflowExecption in documentation for .Net 3.5 (was not in 2.0 or 3.0)
                //  Catching all System.Exceptions here is still safe since the risk of additional undocumented exceptions that can be thrown is much larger than the
                //  risk that we will fail to pass on some other unexpected type of exception that is not a direct result of calling System.Enum.Parse.
                // Prior code had only caught System.ArgumentException and System.InvalidCastException

                result = parseFailedResult;
                return false;
            }
		}

		#endregion
	}

	#endregion
}

//-------------------------------------------------------------------
