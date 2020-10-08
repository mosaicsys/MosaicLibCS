//-------------------------------------------------------------------
/*! @file Dates.cs
 *  @brief This file contains a small set of DateTime related helper methods
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
	#region Dates

    /// <summary>
    /// Dates class is essentially a namespace for series of static Date related helper methods
    /// </summary>
    public static class Dates
	{
        /// <summary>
        /// This method converts the given <paramref name="dt"/> DateTime into a double in units of seconds (UTC) since 00:00:00.000 Jan 1, 1601 (aka the FTime base offset).
        /// </summary>
        public static double GetUTCTimeSince1601(this DateTime dt, bool mapZero = true)
        {
            if (dt.IsZero())
                return 0.0;
            else
                return dt.ToFileTimeUtc() * 0.0000001;
        }

        /// <summary>
        /// This method converts the given <paramref name="utcTimeSince1601"/> into a UTC DateTime value.  
        /// <paramref name="utcTimeSince1601"/> is a double in units of seconds (UTC) since  00:00:00.000 Jan 1, 1601 (aka the FTime base offset).
        /// <para/>If the given <paramref name="utcTimeSince1601"/> is either infinity and <paramref name="mapInfinities"/> is given as true (the default)
        /// Then this method returns DateTime.MaxValue for PositiveInfinity and DateTime.MinValue for NegativeInfinity.
        /// </summary>
        public static DateTime GetDateTimeFromUTCTimeSince1601(this double utcTimeSince1601, bool mapInfinities = true, bool mapZero = true)
        {
            if (utcTimeSince1601 == 0.0 && mapZero)
            {
                return default(DateTime);
            }
            else if (utcTimeSince1601.IsInfinity() && mapInfinities)
            {
                return (utcTimeSince1601 > 0) ? DateTime.MaxValue : DateTime.MinValue;
            }

            long utcFTime = unchecked((long)(utcTimeSince1601 * 10000000.0));

            return DateTime.FromFileTimeUtc(utcFTime);
        }

		// methods used to provide timestamps for LogMessages

		/// <summary>Enum to define the supported formats for converting DataTime values to a string.</summary>
		public enum DateTimeFormat
		{
			/// <summary>Enum value when format should look like 1970-01-01 00:00:00.000</summary>
			LogDefault = 0,

            /// <summary>Enum value when format should look like 19700101_000000</summary>
            Short,

			/// <summary>Enum value when format should look like 19700101_000000.000</summary>
			ShortWithMSec,

            /// <summary>Enum value when format should look like </summary>
            RoundTrip,
		}

		/// <summary>Converts the given DateTime value to a string using the given summary desired format</summary>
		/// <param name="dt">Specifies the DateTime value to convert</param>
		/// <param name="dtFormat">Specifies the desired format from the set of supported enum values.</param>
		/// <returns>The DateTime converted to a string based on the desired format.</returns>
		public static string CvtToString(ref DateTime dt, DateTimeFormat dtFormat)
		{
			string result = string.Empty;

			switch (dtFormat)
			{
				default:
				case DateTimeFormat.LogDefault:
                    result = dt.ToString("yyyy-MM-dd HH:mm:ss.fff");
					break;
                case DateTimeFormat.Short:
                    result = dt.ToString("yyyyMMdd_HHmmss");
                    break;
				case DateTimeFormat.ShortWithMSec:
                    result = dt.ToString("yyyyMMdd_HHmmss.fff");
					break;
                case DateTimeFormat.RoundTrip:
                    result = dt.ToString("o");
                    break;
			}

			return result;
		}

        /// <summary>Converts the given DateTime value to a string using the given summary desired format</summary>
        /// <param name="dt">Specifies the DateTime value to convert</param>
        /// <param name="dtFormat">Specifies the desired format from the set of supported enum values.</param>
        /// <returns>The DateTime converted to a string based on the desired format.</returns>
        public static string CvtToString(this DateTime dt, DateTimeFormat dtFormat)
        {
            return CvtToString(ref dt, dtFormat);
        }
	}

	#endregion
}

//-------------------------------------------------------------------
