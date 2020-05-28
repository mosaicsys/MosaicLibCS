//-------------------------------------------------------------------
/*! @file Registry.cs
 *  @brief This file contains Registry related tools.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2010 Mosaic Systems Inc.
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
using System.Security.AccessControl;
using System.Runtime.InteropServices;

namespace MosaicLib.Win32.Registry
{
    using Microsoft.Win32;      // this using is located here to address namespace and symbol definition overlap issues in this source.
    using MosaicLib.Utils;

    #region Fcns static class

    /// <summary>
    /// This static partial class is effectively a namespace for the Win32 Registry related classes, definitions, and static methods.
    /// </summary>
    public static partial class Fcns
    {
        #region extern kernal functions

        /// <summary>
        /// Provide access to the Win32 RegFlushKey API call.  
        /// This method is used to force a OS level registry commit for key changes that need to be persisted to disk.
        /// In normal use this is performed at specific points in an application's installation/removal pattern.
        /// </summary>
        /// <param name="hKey">Gives the key handle value that identifies the key to be flushed.  Appears to be ignored in many Win32 implementations.</param>
        /// <returns>a Win32 error code or 0 to indicate success.</returns>
        /// <remarks>
        /// From MSDN remarks on this API method:
        /// Calling RegFlushKey is an expensive operation that significantly affects system-wide performance as it consumes disk bandwidth and blocks 
        /// modifications to all keys by all processes in the registry hive that is being flushed until the flush operation completes. 
        /// RegFlushKey should only be called explicitly when an application must guarantee that registry changes are persisted to disk immediately 
        /// after modification. All modifications made to keys are visible to other processes without the need to flush them to disk.
        /// Alternatively, the registry has a 'lazy flush' mechanism that flushes registry modifications to disk at regular intervals of time. 
        /// In addition to this regular flush operation, registry changes are also flushed to disk at system shutdown. 
        /// Allowing the 'lazy flush' to flush registry changes is the most efficient way to manage registry writes to the registry store on disk.
        /// The RegFlushKey function returns only when all the data for the hive that contains the specified key has been written to the registry store on disk.
        /// The RegFlushKey function writes out the data for other keys in the hive that have been modified since the last lazy flush or system start.
        /// </remarks>
        [DllImport("advapi32")]
        public static extern int RegFlushKey(IntPtr hKey);

        #endregion

        #region Split Registry Key Path methods

        /// <summary>
        /// Defines the default delimeter character that is used here to demarcate boundaries in a registry key path.
        /// <para/>Is the \ character (backslash)
        /// </summary>
        public const char DefaultRegPathDelimiter = '\\';

        /// <summary>
        /// Defines the set of delimeter characters that are used here to demarcate boundaries in a registry key path.
        /// <para/>consists of the DefaultRegPathDelimeter (\) and the forward slash / characters.
        /// </summary>
        public static readonly char[] DefaultRegPathDelimiters = new char[] { DefaultRegPathDelimiter, '/' };

        /// <summary>
        /// Splits the given regKeyPath string into a sequence of key names contained in the returned array.  
        /// </summary>
        /// <param name="regKeyPath">A delimited registry key path (similar to a directory path) to split</param>
        /// <returns>an Array of strings that contain the individual key strings as split from the given path string.</returns>
        public static string[] SplitRegistryKeyPath(string regKeyPath)
        {
            return SplitRegistryKeyPath(regKeyPath, DefaultRegPathDelimiters);
        }

        /// <summary>
        /// Splits the given regKeyPath string into a sequence of key names contained in the returned array.  
        /// </summary>
        /// <param name="regKeyPath">A delimited registry key path (similar to a directory path) to split</param>
        /// <param name="delimiters">Gives the array of char values that are used as delimiters</param>
        /// <returns>an Array of strings that contain the individual key strings as split from the given path string.</returns>
        public static string[] SplitRegistryKeyPath(string regKeyPath, char[] delimiters)
        {
            return regKeyPath.Split(delimiters);
        }

        #endregion

        #region Open Registry Key Path methods

        /// <summary>
        /// Opens and returns the RegistryKey found at the given delimited full registry path.  Requests ReadWriteSubTree permissions.
        /// </summary>
        /// <param name="regKeyPath">Gives the delimited full registry key path to traverse.</param>
        /// <returns>The RegistryKey instance for the requested path.</returns>
        /// <exception cref="System.ArgumentException">If the requested regKeyPath is not valid or cannot be opened</exception>
        public static RegistryKey OpenRegistryKeyPath(string regKeyPath)
        {
            return OpenRegistryKeyPath(null, SplitRegistryKeyPath(regKeyPath, DefaultRegPathDelimiters), RegistryKeyPermissionCheck.ReadWriteSubTree);
        }

        /// <summary>
        /// Opens and returns the RegistryKey found at the given delimited full registry path.  Requests the given permissions.
        /// </summary>
        /// <param name="regKeyPath">Gives the delimited full registry key path to traverse.</param>
        /// <param name="permissions">Gives the RegistryKeyPermissionCheck value for the requested permissions.</param>
        /// <returns>The RegistryKey instance for the requested path.</returns>
        /// <exception cref="System.ArgumentException">If the requested regKeyPath is not valid or cannot be opened</exception>
        public static RegistryKey OpenRegistryKeyPath(string regKeyPath, RegistryKeyPermissionCheck permissions) 
        {
            return OpenRegistryKeyPath(null, SplitRegistryKeyPath(regKeyPath, DefaultRegPathDelimiters), permissions);
        }

        /// <summary>
        /// Opens and returns the RegistryKey found at the given delimited partial registry path, starting at the given startAtKey.  Requests the given permissions.
        /// </summary>
        /// <param name="startAtKey">Gives the RegistryKey instance to start the relative traversal at, or null if the traversal should start at the root.</param>
        /// <param name="relativeRegKeyPath">Gives the delimited partial registry key path to traverse starting at the given key.</param>
        /// <param name="permissions">Gives the RegistryKeyPermissionCheck value for the requested permissions.</param>
        /// <returns>The RegistryKey instance for the requested path.</returns>
        /// <exception cref="System.ArgumentException">If the requested regKeyPath is not valid or cannot be opened</exception>
        public static RegistryKey OpenRegistryKeyPath(this RegistryKey startAtKey, string relativeRegKeyPath, RegistryKeyPermissionCheck permissions)
        {
            return OpenRegistryKeyPath(startAtKey, SplitRegistryKeyPath(relativeRegKeyPath, DefaultRegPathDelimiters), permissions);
        }

        /// <summary>
        /// Opens and returns the RegistryKey found at the given partial registry path, starting at the given startAtKey.  Requests the given permissions.
        /// </summary>
        /// <param name="startAtKey">Gives the RegistryKey instance to start the relative traversal at, or null if the traversal should start at the root.</param>
        /// <param name="keyPathArray">Gives the partial registry key path to traverse starting at the given key, in array form.</param>
        /// <param name="permissions">Gives the RegistryKeyPermissionCheck value for the requested permissions.</param>
        /// <returns>The RegistryKey instance for the requested path.</returns>
        /// <exception cref="System.ArgumentException">If the requested regKeyPath is not valid or cannot be opened</exception>
        public static RegistryKey OpenRegistryKeyPath(this RegistryKey startAtKey, string[] keyPathArray, RegistryKeyPermissionCheck permissions)
        {
            RegistryKey currentKey = startAtKey;
            bool preventDisposeCurrentKey = true;       // it came from the one we were given on call

            try
            {
                RegistryKey nextKey = null;

                if (keyPathArray == null)
                    new System.ArgumentNullException("keyPathArray").Throw();
                else if (keyPathArray.Length == 0)
                    new System.ArgumentException("must have at least one element", "keyPathArray").Throw();

                int keyIdx = 0;

                if (currentKey == null)
                {
                    currentKey = GetRegistryHiveKey(keyPathArray[keyIdx++]);
                    preventDisposeCurrentKey = true;        // it came from the GetRegistryHiveKey method (static key)
                }

                for (; keyIdx < keyPathArray.Length; keyIdx++)
                {
                    string keyName = keyPathArray[keyIdx];

                    if (permissions == RegistryKeyPermissionCheck.ReadWriteSubTree)
                    {
                        nextKey = currentKey.OpenSubKey(keyName, permissions);
                        if (nextKey == null)
                            nextKey = currentKey.CreateSubKey(keyName, permissions);
                        if (nextKey == null)
                            new System.ArgumentException(Utils.Fcns.CheckedFormat("Unable to create key:{0} under path:{1}, {2}", keyName, currentKey.ToString(), permissions.ToString()), Utils.Fcns.CheckedFormat("keyPathArray[{0}]", keyIdx)).Throw();
                    }
                    else
                    {
                        nextKey = currentKey.OpenSubKey(keyName, permissions);
                        if (nextKey == null)
                            new System.ArgumentException(Utils.Fcns.CheckedFormat("Unable to open key:{0} under path:{1}, {2}", keyName, currentKey.ToString(), permissions.ToString()), Utils.Fcns.CheckedFormat("keyPathArray[{0}]", keyIdx)).Throw();
                    }

                    if (!preventDisposeCurrentKey)
                        Utils.Fcns.DisposeOfObject(ref currentKey);

                    currentKey = nextKey;
                    preventDisposeCurrentKey = false;       // it comes from one that was opened above so we can dispose it if it is not the one we will actually return.
                }

                return currentKey;
            }
            catch (System.Exception ex)
            {
                // will re-throw the original exception after disposing of any intermediate key
                if (!preventDisposeCurrentKey && currentKey != null)
                    Utils.Fcns.DisposeOfObject(ref currentKey);

                ex.Throw();
                return null;
            }
        }

        #endregion

        #region Registry Hive Helper methods

        /// <summary>
        /// Accepts a given Registry Hive name and returns the RegistryHive instance that matches.
        /// Supports HKEY_CLASSES_ROOT, HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, HKEY_USERS, HKEY_CURRENT_CONFIG, and HKEY_PERMORMANCE_DATA.
        /// Also supports corresponding shorthand versions HKCR, HKCU, HKLM, HKU, HKCC, and HKPD.
        /// </summary>
        static public RegistryHive GetRegistryHiveCode(string hiveName)
        {
            switch (hiveName)
            {
                case "HKEY_CLASSES_ROOT": case "HKCR": case "HDCR": return RegistryHive.ClassesRoot;        // HDCR appears to have been a typo - preserving for backward compatibility.
                case "HKEY_CURRENT_USER": case "HKCU": return RegistryHive.CurrentUser;
                case "HKEY_LOCAL_MACHINE": case "HKLM": return RegistryHive.LocalMachine;
                case "HKEY_USERS": case "HKU": return RegistryHive.Users;
                case "HKEY_CURRENT_CONFIG": case "HKCC": return RegistryHive.CurrentConfig;
                case "HKEY_PERFORMANCE_DATA": case "HKPD": return RegistryHive.PerformanceData;
                default:
                    new System.ArgumentException(Utils.Fcns.CheckedFormat("HiveName:{0} not found", hiveName), "hiveName").Throw();
                    return default(RegistryHive);
            }
        }

        /// <summary>
        /// Returns a pre-existing RegistryKey that corresponds to the given hiveName as converted to a RegistryHive type using the <see cref="GetRegistryHiveCode"/> method.
        /// </summary>
        static public RegistryKey GetRegistryHiveKey(this string hiveName)
        {
            RegistryHive hiveCode = GetRegistryHiveCode(hiveName);
            switch (hiveCode)
            {
                case RegistryHive.ClassesRoot: return Registry.ClassesRoot;
                case RegistryHive.CurrentUser: return Registry.CurrentUser;
                case RegistryHive.LocalMachine: return Registry.LocalMachine;
                case RegistryHive.Users: return Registry.Users;
                case RegistryHive.CurrentConfig: return Registry.CurrentConfig;
                case RegistryHive.PerformanceData: return Registry.PerformanceData;
                default:
                    new System.ArgumentException(Utils.Fcns.CheckedFormat("HiveCode:{0} is not valid for HiveName:{1}", hiveCode, hiveName), "hiveName").Throw();
                    return null;
            }
        }

        #endregion
    }

    #endregion

    #region Registry Key Creation classes

    /// <summary>
    /// This struct is used to contain a registry key value pair.  
    /// </summary>
    public struct RegValueSpec
    {
        /// <summary>
        /// Constructs a key value pair with the given valueName and valueObject.  
        /// Sets valueKind to RegistryValueKind.Unknown which will cause any call to RegistryKey.SetValue to attempt to dynamically derive the type from the actual type of the ValueObject.
        /// </summary>
        public RegValueSpec(string valueName, object valueObject) : this(valueName, valueObject, RegistryValueKind.Unknown) { }
        /// <summary>
        /// Constructs a key value pair with the given valueName, valueObject, and valueKind.  
        /// Sets valueKind to RegistryValueKind.Unknown which will cause any call to RegistryKey.SetValue to attempt to dynamically derive the type from the actual type of the ValueObject.
        /// </summary>
        public RegValueSpec(string valueName, object valueObject, RegistryValueKind valueKind) 
            : this() 
        {
            ValueName = valueName;
            ValueObject = valueObject;
            ValueKind = valueKind;
        }

        string ValueName { get; set; }
        object ValueObject { get; set; }
        RegistryValueKind ValueKind { get; set; }

        /// <summary>
        /// Local method Sets the value ValueName to contain the ValueObject, using the contained ValueKind, under the given underRegKey RegistryKey.
        /// Uses RegistryKey.SetValue.
        /// </summary>
        public void SetValue(RegistryKey underRegKey)
        {
            underRegKey.SetValue(ValueName, ValueObject, ValueKind);
        }
    }

    #endregion
}
