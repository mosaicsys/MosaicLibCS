//-------------------------------------------------------------------
/*! @file Registry.cs
 * @brief This file contains Registry related tools.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2010 Mosaic Systems Inc., All rights reserved
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

namespace MosaicLib.Win32.Registry
{
    using System;
    using Microsoft.Win32;
    using System.Security.AccessControl;
    using System.Runtime.InteropServices;

    #region Fcns static class

    public static partial class Fcns
    {
        #region extern kernal functions

        [DllImport("advapi32")]
        public static extern int RegFlushKey(IntPtr hKey);

        #endregion

        #region Split Registry Key Path methods

        public const char DefaultRegPathDelimiter = '\\';
        public static readonly char [] DefaultRegPathDelimiters = new char [] { DefaultRegPathDelimiter, '/' };

        public static string[] SplitRegistryKeyPath(string regKeyPath)
        {
            return SplitRegistryKeyPath(regKeyPath, DefaultRegPathDelimiters);
        }

        public static string[] SplitRegistryKeyPath(string regKeyPath, char[] delimiters)
        {
            return regKeyPath.Split(delimiters);
        }

        #endregion

        #region Open Registry Key Path methods

        public static RegistryKey OpenRegistryKeyPath(string regKeyPath)
        {
            return OpenRegistryKeyPath(null, SplitRegistryKeyPath(regKeyPath, DefaultRegPathDelimiters), RegistryKeyPermissionCheck.ReadWriteSubTree);
        }

        public static RegistryKey OpenRegistryKeyPath(string regKeyPath, RegistryKeyPermissionCheck permissions) 
        {
            return OpenRegistryKeyPath(null, SplitRegistryKeyPath(regKeyPath, DefaultRegPathDelimiters), permissions);
        }

        public static RegistryKey OpenRegistryKeyPath(RegistryKey startAtKey, string relativeRegKeyPath, RegistryKeyPermissionCheck permissions)
        {
            return OpenRegistryKeyPath(startAtKey, SplitRegistryKeyPath(relativeRegKeyPath, DefaultRegPathDelimiters), permissions);
        }

        public static RegistryKey OpenRegistryKeyPath(RegistryKey startAtKey, string[] keyPathArray, RegistryKeyPermissionCheck permissions)
        {
            RegistryKey currentKey = startAtKey;
            bool preventDisposeCurrentKey = true;       // it came from the one we were given on call

            try
            {
                RegistryKey nextKey = null;

                if (keyPathArray == null)
                    throw new System.ArgumentNullException("keyPathArray");
                else if (keyPathArray.Length == 0)
                    throw new System.ArgumentException("must have at least one element", "keyPathArray");

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
                            throw new System.ArgumentException(Utils.Fcns.CheckedFormat("Unable to create key:{0} under path:{1}, {2}", keyName, currentKey.ToString(), permissions.ToString()), Utils.Fcns.CheckedFormat("keyPathArray[{0}]", keyIdx));
                    }
                    else
                    {
                        nextKey = currentKey.OpenSubKey(keyName, permissions);
                        if (nextKey == null)
                            throw new System.ArgumentException(Utils.Fcns.CheckedFormat("Unable to open key:{0} under path:{1}, {2}", keyName, currentKey.ToString(), permissions.ToString()), Utils.Fcns.CheckedFormat("keyPathArray[{0}]", keyIdx));
                    }

                    if (!preventDisposeCurrentKey)
                        Utils.Fcns.DisposeOfObject(ref currentKey);

                    currentKey = nextKey;
                    preventDisposeCurrentKey = false;       // it comes from one that was opened above so we can dispose it if it is not the one we will actually return.
                }

                return currentKey;
            }
            catch
            {
                // will re-throw the original exception after disposing of any intermediate key
                if (!preventDisposeCurrentKey && currentKey != null)
                    Utils.Fcns.DisposeOfObject(ref currentKey);

                throw;
            }
        }

        #endregion

        #region Registry Hive Helper methods

        static public RegistryHive GetRegistryHiveCode(string hiveName)
        {
            switch (hiveName)
            {
                case "HKEY_CLASSES_ROOT": case "HDCR": return RegistryHive.ClassesRoot;
                case "HKEY_CURRENT_USER": case "HKCU": return RegistryHive.CurrentUser;
                case "HKEY_LOCAL_MACHINE": case "HKLM": return RegistryHive.LocalMachine;
                case "HKEY_USERS": case "HKU": return RegistryHive.Users;
                case "HKEY_CURRENT_CONFIG": case "HKCC": return RegistryHive.CurrentConfig;
                case "HKEY_PERFORMANCE_DATA": case "HKPD": return RegistryHive.PerformanceData;
                default:
                    throw new System.ArgumentException(Utils.Fcns.CheckedFormat("HiveName:{0} not found", hiveName), "hiveName");
            }
        }

        static public RegistryKey GetRegistryHiveKey(string hiveName)
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
                    throw new System.ArgumentException(Utils.Fcns.CheckedFormat("HiveCode:{0} is not valid for HiveName:{1}", hiveCode, hiveName), "hiveName");
            }
        }

        #endregion
    }

    #endregion

    #region Registry Key Creation classes

    public struct RegValueSpec
    {
        public RegValueSpec(string valueName, object valueObject) : this(valueName, valueObject, RegistryValueKind.Unknown) { }
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

        public void SetValue(RegistryKey underRegKey)
        {
            underRegKey.SetValue(ValueName, ValueObject, ValueKind);
        }
    }

    #endregion
}
