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

        public const FileAttributes FileAttributes_None = default(FileAttributes);
        public const FileAttributes FileAttributes_All = (FileAttributes)(-1);

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
        /// Obtains and returns the FileAttributes value for the given <paramref name="filePath"/>.
        /// <para/>If the System.IO.File.GetAttributes call throws a FileNotFoundException and the given <paramref name="fileOrDirectoryNotFoundValue"/> is non-null then this method returns the given <paramref name="fileOrDirectoryNotFoundValue"/>
        /// <para/>If the System.IO.File.GetAttributes call throws a DirectoryNotFoundException and the given <paramref name="fileOrDirectoryNotFoundValue"/> is non-null then this method returns the given <paramref name="fileOrDirectoryNotFoundValue"/>
        /// <para/>If the System.IO.File.GetAttributes call throws any other exception and the given <paramref name="generalExceptionValue"/> is non-null then this method return the given <paramref name="generalExceptionValue"/>
        /// <para/>Otherwise this method rethrows the thrown exception.
        /// </summary>
        /// <remarks>
        /// handling for FileNotFoundException and DirectoryNotFoundException has been consolidated and simplified due to ambiguity about which is thrown when the path is clearly refering to a directory. 
        /// </remarks>
        public static FileAttributes GetFileAttributesForPath(this string filePath, FileAttributes ? fileOrDirectoryNotFoundValue = DoesNotExistFileAttributesValue, FileAttributes ? generalExceptionValue = null)
        {
            try
            {
                return System.IO.File.GetAttributes(filePath);
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
        /// Attempts to modify the given <paramref name="filePath"/>'s <see cref="FileAttributes"/> by obtaining the current value, changing it by anding in the <paramref name="andMask"/> value and then oring in the <paramref name="orMask"/> value,
        /// and then calling <see cref="System.IO.File.SetAttributes(string, FileAttributes)"/> with the resulting value to change the file's attribute values.
        /// Returns the resulting value or the given <paramref name="fallbackValue"/> if an exception is thrown and the <paramref name="rethrow"/> is passed as false.
        /// </summary>
        public static FileAttributes SetFileAttributes(this string filePath, FileAttributes orMask = FileAttributes_None, FileAttributes andMask = FileAttributes_None, bool rethrow = true, FileAttributes fallbackValue = DoesNotExistFileAttributesValue)
        {
            try
            {
                var currentAttributes = (andMask != FileAttributes_None) ? System.IO.File.GetAttributes(filePath) : FileAttributes_None;
                var newAttributes = (currentAttributes & andMask) | orMask;

                if (newAttributes == currentAttributes && currentAttributes != FileAttributes_None)
                    return currentAttributes;

                System.IO.File.SetAttributes(filePath, newAttributes);
                return System.IO.File.GetAttributes(filePath);
            }
            catch (System.Exception ex)
            {
                var _ex = ex;

                if (rethrow)
                    throw;

                return fallbackValue;
            }
        }

        /// <summary>
        /// Attempts to modify the <see cref="FileAttributes"/> for the file referenced by the given <paramref name="fileInfo"/> by applying the given <paramref name="orMask"/> and <paramref name="andMask"/>.
        /// If the operation throws and exception and the <paramref name="rethrow"/> is true then the exception will be rethrown otherwise it will be ignored.
        /// Returns the given <paramref name="fileInfo"/> to support call chaining.
        /// </summary>
        public static FileInfo SetFileAttributes(this FileInfo fileInfo, FileAttributes orMask = FileAttributes_None, FileAttributes andMask = FileAttributes_None, bool rethrow = true)
        {
            try
            {
                if (fileInfo.Exists)
                {
                    var currentAttributes = fileInfo.Attributes;
                    var newAttributes = (currentAttributes & andMask) | orMask;

                    if (newAttributes != currentAttributes)
                        fileInfo.Attributes = newAttributes;

                    return fileInfo;
                }
            }
            catch (System.Exception ex)
            {
                var _ex = ex;

                if (rethrow)
                    throw;
            }

            return fileInfo;
        }

        /// <summary>
        /// Returns true if the <see cref="FileAttributes"/> for file at the given <paramref name="filePath"/> has the <see cref="FileAttributes.ReadOnly"/> flag set.
        /// </summary>
        public static bool GetFileIsReadOnly(this string filePath, bool rethrow = true)
        {
            FileAttributes ? fallbackValue = (rethrow) ? null : (FileAttributes?) DoesNotExistFileAttributesValue;

            return ((filePath.GetFileAttributesForPath(fileOrDirectoryNotFoundValue: fallbackValue, generalExceptionValue: fallbackValue) & FileAttributes.ReadOnly) != 0);
        }

        /// <summary>
        /// Attempts to change the <see cref="FileAttributes.ReadOnly"/> flag for the file at the given <paramref name="filePath"/> to be set or removed based on the given <paramref name="setToBeReadOnly"/> value.
        /// </summary>
        public static FileAttributes SetFileIsReadOnly(this string filePath, bool setToBeReadOnly, bool rethrow = true, FileAttributes fallbackValue = DoesNotExistFileAttributesValue)
        {
            return filePath.SetFileAttributes(orMask: setToBeReadOnly ? FileAttributes.ReadOnly : FileAttributes_None, andMask: ~FileAttributes.ReadOnly, rethrow: rethrow, fallbackValue: fallbackValue);
        }

        /// <summary>
        /// Attempts to change the <see cref="FileAttributes.ReadOnly"/> flag for the file at the file referenced by the given <paramref name="fileInfo"/> to be set or removed based on the given <paramref name="setToBeReadOnly"/> value.
        /// </summary>
        public static FileInfo SetFileIsReadOnly(this FileInfo fileInfo, bool setToBeReadOnly, bool rethrow = true)
        {
            try
            {
                if (fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly) != setToBeReadOnly)
                {
                    if (setToBeReadOnly)
                        fileInfo.Attributes |= FileAttributes.ReadOnly;
                    else
                        fileInfo.Attributes &= ~FileAttributes.ReadOnly;
                }
            }
            catch (System.Exception ex)
            {
                var _ex = ex;

                if (rethrow) 
                    throw;
            }

            return fileInfo;
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

        /// <summary>
        /// This method checks if the given <paramref name="dirPath"/> is an existing directory and, if not, it attempts to create the directory using <see cref="System.IO.Directory.CreateDirectory(string)"/>.
        /// Returns the given <paramref name="dirPath"/> to support call chaining.
        /// </summary>
        public static string CreateDirectoryIfNeeded(this string dirPath)
        {
            if (!System.IO.Directory.Exists(dirPath))
                System.IO.Directory.CreateDirectory(dirPath);

            return dirPath;
        }

        /// <summary>
        /// This method extracts a directory path from the given <paramref name="filePath"/> using <see cref="System.IO.Path.GetDirectoryName(string)"/>.
        /// and checks if the directory path exists and, if not, it attempts to create it using <see cref="System.IO.Directory.CreateDirectory(string)"/>.
        /// Returns the given <paramref name="filePath"/> to support call chaining.
        /// </summary>
        public static string CreateFileDirectoryIfNeeded(this string filePath)
        {
            var dirPath = System.IO.Path.GetDirectoryName(filePath);

            if (!System.IO.Directory.Exists(dirPath))
                System.IO.Directory.CreateDirectory(dirPath);

            return filePath;
        }

        /// <summary>
        /// Obtains dirInfo = <see cref="DirectoryInfo"/> for the given <paramref name="dirPath"/>.  If the directory does not exist then this method attempt to create it using the <see cref="DirectoryInfo.Create()"/> method.
        /// If the directory does exist then this method calls dirInfo.ClearDirectoryIfNeeded passing it the related parameters (<paramref name="clearReadOnlyIfNeeded"/>, <paramref name="recursive"/>, <paramref name="fileSearchPattern"/>, and <paramref name="directorySearchPattern"/>)
        /// </summary>
        public static string CreateOrClearDirectoryIfNeeded(this string dirPath, bool recursive = true, bool clearReadOnlyIfNeeded = true, string fileSearchPattern = "*", string directorySearchPattern = "*")
        {
            var dirInfo = new System.IO.DirectoryInfo(dirPath);

            if (!dirInfo.Exists)
                dirInfo.Create();
            else
                dirInfo.ClearDirectoryIfNeeded(clearReadOnlyIfNeeded: clearReadOnlyIfNeeded, recursive: recursive, fileSearchPattern: fileSearchPattern, directorySearchPattern: directorySearchPattern);

            return dirPath;
        }

        /// <summary>
        /// Obtains dirInfo = <see cref="DirectoryInfo"/> for the given <paramref name="dirPath"/> and then calls
        /// dirInfo.ClearDirectoryIfNeeded passing it the related parameters (<paramref name="clearReadOnlyIfNeeded"/>, <paramref name="recursive"/>, <paramref name="fileSearchPattern"/>, and <paramref name="directorySearchPattern"/>)
        /// </summary>
        public static string ClearDirectoryIfNeeded(this string dirPath, bool recursive = true, bool clearReadOnlyIfNeeded = true, string fileSearchPattern = "*", string directorySearchPattern = "*")
        {
            var dirInfo = new System.IO.DirectoryInfo(dirPath);
            
            dirInfo.ClearDirectoryIfNeeded(fileSearchPattern: fileSearchPattern, directorySearchPattern: directorySearchPattern, clearReadOnlyIfNeeded: clearReadOnlyIfNeeded, recursive: recursive);

            return dirPath;
        }

        /// <summary>
        /// Checks if the given <paramref name="dirInfo"/> represents an existing directory and if so then it attempts to remove the indicated files and/or sub-directories from it, optionally <paramref name="recursive"/>ly.
        /// The set of files to be removed is specified using the given <paramref name="fileSearchPattern"/> and the set of sub-directories to be removed is specified using the given <paramref name="directorySearchPattern"/>.
        /// If <paramref name="fileSearchPattern"/> is given as <see langword="null"/> then no files will be removed.  If <paramref name="directorySearchPattern"/> is given as <see langword="null"/> then no sub-directories will be removed. 
        /// If an excpetion is thrown and <paramref name="rethrow"/> is true then the exception will be rethrown, otherwise it will be ignored.
        /// </summary>
        public static DirectoryInfo ClearDirectoryIfNeeded(this DirectoryInfo dirInfo, bool recursive = true, bool clearReadOnlyIfNeeded = true, string fileSearchPattern = "*", string directorySearchPattern = "*", bool rethrow = true)
        {
            try
            {
                if (dirInfo.Exists)
                {
                    if (fileSearchPattern != null)
                    {
                        foreach (var fileInfo in dirInfo.GetFiles(fileSearchPattern, SearchOption.TopDirectoryOnly))
                        {
                            fileInfo.RemoveFileIfNeeded(clearReadOnlyIfNeeded: clearReadOnlyIfNeeded, rethrow: rethrow);
                        }
                    }

                    if (directorySearchPattern != null)
                    {
                        foreach (var subDirInfo in dirInfo.GetDirectories(directorySearchPattern, SearchOption.TopDirectoryOnly))
                        {
                            subDirInfo.RemoveDirectoryIfNeeded(clearReadOnlyIfNeeded: clearReadOnlyIfNeeded, recursive: recursive, fileSearchPattern: fileSearchPattern, directorySearchPattern: directorySearchPattern);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                var _ex = ex;

                if (rethrow)
                    throw;
            }

            return dirInfo;
        }

        /// <summary>
        /// If <paramref name="createDirectoryIfNeeded"/> is true then this method calls <see cref="CreateFileDirectoryIfNeeded(string)"/> on that path to make sure that the directory exists (creating if needed).
        /// Then this method checks if the given <paramref name="filePath"/> exists.
        /// If the file exists and if <paramref name="clearReadOnlyIfNeeded"/> is true then it attempts to clear the files read only flag (as needed)
        /// and then attempts to delete the file using <see cref="System.IO.File.Delete(string)"/>.
        /// Returns the given <paramref name="filePath"/> to support call chaining.
        /// </summary>
        public static string RemoveFileIfNeeded(this string filePath, bool createDirectoryIfNeeded = false, bool clearReadOnlyIfNeeded = true)
        {
            if (createDirectoryIfNeeded)
                filePath.CreateFileDirectoryIfNeeded();

            var fileInfo = new FileInfo(filePath);

            fileInfo.RemoveFileIfNeeded(clearReadOnlyIfNeeded: clearReadOnlyIfNeeded);

            return filePath;
        }

        /// <summary>
        /// Then this method checks if the file referenced by the given <paramref name="fileInfo"/> exists.
        /// If the file exists and if <paramref name="clearReadOnlyIfNeeded"/> is true then it attempts to clear the files read only flag (as needed)
        /// then it attempts to delete the file using <see cref="System.IO.FileInfo.Delete()"/> method.
        /// If an excpetion is thrown and <paramref name="rethrow"/> is true then the exception will be rethrown, otherwise it will be ignored.
        /// Returns the given <paramref name="fileInfo"/> to support call chaining.
        /// </summary>
        public static FileInfo RemoveFileIfNeeded(this FileInfo fileInfo, bool clearReadOnlyIfNeeded = true, bool rethrow = true)
        {
            try
            {
                if (fileInfo.Exists)
                {
                    if (clearReadOnlyIfNeeded)
                        fileInfo.SetFileIsReadOnly(false, rethrow: rethrow);

                    fileInfo.Delete();
                }
            }
            catch (System.Exception ex)
            {
                var _ex = ex;

                if (rethrow)
                    throw;
            }

            return fileInfo;
        }


        /// <summary>
        /// If the given <paramref name="dirPath"/> is an existing directory then this method will optionally remove its sub-files and and sub-directories <paramref name="recursive"/>ly,
        /// also optionally clearning file read only flags if requested in the <paramref name="clearReadOnlyIfNeeded"/> parameter and will then attempt to remove the directory itself.
        /// </summary>
        public static string RemoveDirectoryIfNeeded(this string dirPath, bool recursive = true, bool clearReadOnlyIfNeeded = true)
        {
            var dirInfo = new System.IO.DirectoryInfo(dirPath);

            dirInfo.RemoveDirectoryIfNeeded(clearReadOnlyIfNeeded: clearReadOnlyIfNeeded, recursive: recursive);

            return dirPath;
        }

        /// <summary>
        /// Checks if the given <paramref name="dirInfo"/> represents an existing directory then this method will optionally remove its sub-files and and sub-directories <paramref name="recursive"/>ly using the dirInfo.ClearDirectoryIfNeeded method.
        /// also optionally clearning file read only flags if requested in the <paramref name="clearReadOnlyIfNeeded"/> parameter and will then attempt to remove the directory itself.
        /// If an excpetion is thrown and <paramref name="rethrow"/> is true then the exception will be rethrown, otherwise it will be ignored.
        /// </summary>
        public static DirectoryInfo RemoveDirectoryIfNeeded(this DirectoryInfo dirInfo, bool recursive = true, bool clearReadOnlyIfNeeded = true, string fileSearchPattern = "*", string directorySearchPattern = "*", bool rethrow = true)
        {
            if (clearReadOnlyIfNeeded)
                dirInfo.ClearDirectoryIfNeeded(clearReadOnlyIfNeeded: clearReadOnlyIfNeeded, recursive: recursive, fileSearchPattern: fileSearchPattern, directorySearchPattern: directorySearchPattern, rethrow: rethrow);

            try
            {
                if (dirInfo.Exists)
                    dirInfo.Delete(recursive: recursive);
            }
            catch (System.Exception ex)
            {
                var _ex = ex;

                if (rethrow)
                    throw;
            }

            return dirInfo;
        }
    }
}

//-------------------------------------------------------------------
