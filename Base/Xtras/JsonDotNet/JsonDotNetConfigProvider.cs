//-------------------------------------------------------------------
/*! @file JsonDotNetConfigProvider.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2017 Mosaic Systems Inc.
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
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Persist;

using Newtonsoft.Json;

namespace Mosaic.JsonDotNet
{
    /// <summary>
    /// Provides a type of DictionaryConfigKeyProvider obtained by using a DataContractPersistentJsonDotNetTextFileRingStorageAdapter based on the ConfigKeyStore file format.
    /// Normally this provider is used for read/write behavior and is most easily used to support EnsureExists usage patterns and/or moderate to high write rate usages
    /// with the same file IO failure handling that is provided through the use of the PeristentObjectFileRing.
    /// </summary>
    public class PersistentJsonDotNetTextFileRingProvider : PersistentSerializedTextFileRingProviderBase
    {
        /// <summary>
        /// Constructor: Accepts provider name, filePath to ini file to read/write, keyPrefix to prefix on all contained keys, 
        /// and isReadWrite to indicate if the INI file is writable or not (ie if all of the keys should be IsFixed).
        /// </summary>
        public PersistentJsonDotNetTextFileRingProvider(string name, PersistentObjectFileRingConfig ringConfig, string keyPrefix = "", bool isReadWrite = true, INamedValueSet providerMetaData = null, bool sortKeysOnSave = false)
            : base(name, ringConfig, new DataContractPersistentJsonDotNetTextFileRingStorageAdapter<ConfigKeyStore>(name, ringConfig) { Object = new ConfigKeyStore() }, keyPrefix: keyPrefix, isReadWrite: isReadWrite, providerMetaData: providerMetaData, keysMayBeAddedUsingEnsureExistsOption: isReadWrite, sortKeysOnSave: sortKeysOnSave)
        { }
    }
}
