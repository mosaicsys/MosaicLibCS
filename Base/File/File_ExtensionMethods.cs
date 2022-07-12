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

using System.Text;
using System.IO;
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

        /// <summary>
        /// An entirely invalid combination of FileAttributes values used when indicating that a give path does not exist.  This choice is based on code review of dotnet source code.
        /// </summary>
        /// <remarks>
        /// From review of dotnet source: this value is used internally by FileSystemInfo and GetFileAttributes for (all?) Win32 errors.
        /// Also note that there appears to be special handling for Errors 2 (ERROR_FILE_NOT_FOUND), 3 (ERROR_PATH_NOT_FOUND) and 21 (ERROR_NOT_READY)
        /// where any other error produced by an attempt to get file attributes for a given path will try again using a slightly different code path.
        /// </remarks>
        public const FileAttributes DoesNotExistFileAttributesValue = (FileAttributes)(-1);

        /// <summary>
        /// Returns true if the given <paramref name="fileAttributes"/> value has the FileAttributes.Directory flag bit set and is not equal to the DoesNotExistFileAttributesValue (aka -1)
        /// </summary>
        public static bool IsExistingDirectory(this FileAttributes fileAttributes)
        {
            if (fileAttributes == DoesNotExistFileAttributesValue)
                return false;
            else if ((fileAttributes & FileAttributes.Directory) != 0)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Returns true if:
        /// <para/>the given <paramref name="fileAttributes"/> value is not equal to DoesNotExistFileAttributesValue (aka -1), 
        /// <para/>and it does not have the FileAttributes.Directory flag bit set, 
        /// <para/> and (it is not zero or the <paramref name="zeroIndicatesExsitingFile"/> parameter is true).
        /// </summary>
        public static bool IsExistingFile(this FileAttributes fileAttributes, bool zeroIndicatesExsitingFile = true)
        {
            if (fileAttributes == DoesNotExistFileAttributesValue)
                return false;
            else if ((fileAttributes & FileAttributes.Directory) != 0)
                return false;
            else if (fileAttributes != default(FileAttributes))
                return true;
            else
                return zeroIndicatesExsitingFile;
        }

        /// <summary>
        /// Obtains and returns the FileAttributes value for the given <paramref name="path"/>.
        /// <para/>If the System.IO.File.GetAttributes call throws a FileNotFoundException and the given <paramref name="fileOrDirectoryNotFoundValue"/> is non-null then this method returns the given <paramref name="fileOrDirectoryNotFoundValue"/>
        /// <para/>If the System.IO.File.GetAttributes call throws a DirectoryNotFoundException and the given <paramref name="fileOrDirectoryNotFoundValue"/> is non-null then this method returns the given <paramref name="fileOrDirectoryNotFoundValue"/>
        /// <para/>If the System.IO.File.GetAttributes call throws any other exception and the given <paramref name="generalExceptionValue"/> is non-null then this method return the given <paramref name="generalExceptionValue"/>
        /// <para/>Otherwise this method rethrows the thrown exception.
        /// </summary>
        /// <remarks>
        /// handling for FileNotFoundException and DirectoryNotFoundException has been consolidated and simplified due to ambiguity about which is thrown when the path is clearly refering to a directory. 
        /// </remarks>
        public static FileAttributes GetFileAttributesForPath(this string path, FileAttributes ? fileOrDirectoryNotFoundValue = DoesNotExistFileAttributesValue, FileAttributes ? generalExceptionValue = null)
        {
            try
            {
                return System.IO.File.GetAttributes(path);
            }
            catch (System.IO.FileNotFoundException)
            {
                if (fileOrDirectoryNotFoundValue != null)
                    return fileOrDirectoryNotFoundValue ?? DoesNotExistFileAttributesValue;
                else
                    throw;
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                if (fileOrDirectoryNotFoundValue != null)
                    return fileOrDirectoryNotFoundValue ?? DoesNotExistFileAttributesValue;
                else
                    throw;
            }
            catch 
            {
                if (generalExceptionValue != null)
                    return generalExceptionValue ?? DoesNotExistFileAttributesValue;
                else
                    throw;
            }
        }

        /// <summary>
        /// If the given <paramref name="path"/> refers to an existing directory (via GetFileAttributesForPath().IsExistingDirectory() 
        /// then this method returns a DirectoryInfo for the given <paramref name="path"/> otherwise this method
        /// returns a FileInfo for the given <paramref name="path"/>.
        /// </summary>
        public static FileSystemInfo GetFileSystemInfoForPath(this string path)
        {
            if (path.GetFileAttributesForPath().IsExistingDirectory())
                return new DirectoryInfo(path);
            else 
                return new FileInfo(path);
        }
    }
}

//-------------------------------------------------------------------
