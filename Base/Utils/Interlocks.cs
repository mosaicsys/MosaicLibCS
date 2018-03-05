//-------------------------------------------------------------------
/*! @file Interlocks.cs
 *  @brief This file contains a set of extension methods and definitions that are useful for implementing OnRequest and Continuous iterlocks.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.Utils;

namespace MosaicLib.Utils.Interlocks
{
    public static partial class ExtensionMethods
    {
        #region AddDenyReason variants

        /// <summary>
        /// This extension method accepts an, optionally null, <paramref name="denyReasonList"/>, maps it to be non-null (using MapNullToEmpty) and appends the given <paramref name="denyReason"/> string to it.
        /// If the given <paramref name="denyReason"/> is null or emtpy or the given <paramref name="triggerCondition"/> is false then this method will have no effect (it will simply return the given <paramref name="denyReasonList"/> value).
        /// If <paramref name="appendDetails"/> is true and <paramref name="formattableDetails"/> is non-null then this method will add the concatination of the given <paramref name="denyReason"/>, the given <paramref name="seperator"/> and the results generated from the IFormattable object's ToString method.
        /// If used, the <paramref name="formattableDetails"/> is evaulated using the SafeToString method which is passed the given <paramref name="caughtExceptionToStringFormat"/> value which is responsible for handling cases where the ToString method throws an exception.
        /// </summary>
        public static IList<string> AddDenyReason(this IList<string> denyReasonList, string denyReason, IFormattable formattableDetails = null, bool appendDetails = true, string seperator = " ", ExceptionFormat caughtExceptionToStringFormat = (ExceptionFormat.TypeAndMessageAndStackTrace), bool triggerCondition = true)
        {
            if (!triggerCondition || denyReason.IsNullOrEmpty())
                return denyReasonList;

            denyReasonList = denyReasonList.ConvertToWritable();

            if (appendDetails && formattableDetails != null)
                denyReasonList.Add("{0}{1}{2}".CheckedFormat(denyReason, seperator, formattableDetails.SafeToString(caughtExceptionToStringFormat: caughtExceptionToStringFormat)));
            else
                denyReasonList.Add(denyReason);

            return denyReasonList;
        }

        /// <summary>
        /// This extension method accepts an, optionally null, <paramref name="denyReasonList"/>, maps it to be non-null (using MapNullToEmpty) and appends the given <paramref name="denyReason"/> string to it.
        /// If the given <paramref name="denyReason"/> is null or emtpy or the given <paramref name="triggerCondition"/> is false then this method will have no effect (it will simply return the given <paramref name="denyReasonList"/> value).
        /// If <paramref name="appendDetails"/> is true and <paramref name="detailsFactoryDelegate"/> is non-null then this method will add the concatination of the given <paramref name="denyReason"/>, the given <paramref name="seperator"/> and the results returned by invoking the delegate.
        /// If used, the <paramref name="detailsFactoryDelegate"/> is evaulated using the SafeToString method which is passed the given <paramref name="caughtExceptionToStringFormat"/> value which is responsible for handling cases where the delegate throws an exception.
        /// </summary>
        public static IList<string> AddDenyReason(this IList<string> denyReasonList, string denyReason, Func<string> detailsFactoryDelegate, bool appendDetails = true, string seperator = " ", ExceptionFormat caughtExceptionToStringFormat = (ExceptionFormat.TypeAndMessageAndStackTrace), bool triggerCondition = true)
        {
            if (!triggerCondition || denyReason.IsNullOrEmpty())
                return denyReasonList;

            denyReasonList = denyReasonList.ConvertToWritable();

            if (appendDetails && detailsFactoryDelegate != null)
                denyReasonList.Add("{0}{1}{2}".CheckedFormat(denyReason, seperator, detailsFactoryDelegate.SafeToString(caughtExceptionToStringFormat: caughtExceptionToStringFormat)));
            else
                denyReasonList.Add(denyReason);

            return denyReasonList;
        }

        #endregion

    }
}

//-------------------------------------------------------------------
