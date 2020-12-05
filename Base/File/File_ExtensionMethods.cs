//-------------------------------------------------------------------
/*! @file File_ExtensionMethods.cs
 *  @brief This file contains a set of File and Path related helper extension methods.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2020 Mosaic Systems Inc.
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
using System.Linq;

using MosaicLib.Utils;

namespace MosaicLib.File
{
    /// <summary>
    /// File Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        private static readonly char[] invalidPathCharArray = System.IO.Path.GetInvalidPathChars();
        private static readonly char[] invalidFileNameCharArray = System.IO.Path.GetInvalidFileNameChars();

        /// <summary>
        /// If needed this method replaces each of the invalid characters in the given <paramref name="fileName"/> with the string %hhhh where hhhh is the hex version of the corresponding character.
        /// <para/>see System.IO.Path.GetInvalidFileNameChars().
        /// </summary>
        public static string SanitizeFileName(this string fileName)
        {
            if (fileName.IsNullOrEmpty() || !fileName.Any(ch => invalidFileNameCharArray.Contains(ch) || ch == '%'))
                return fileName;

            StringBuilder sb = new StringBuilder(fileName.Length);

            foreach (char ch in fileName)
            {
                if (ch == '%')
                    sb.Append("%%");
                else if (!invalidFileNameCharArray.Contains(ch))
                    sb.Append(ch);
                else
                    sb.CheckedAppendFormat("%{0:x4}", unchecked((int) ch));
            }

            return sb.ToString();
        }
    }
}

//-------------------------------------------------------------------
