//-------------------------------------------------------------------
/*! @file JsonDotNetPersist.cs
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

using MosaicLib.Modular.Persist;

using Json = Newtonsoft.Json;

namespace Mosaic.ToolsLib.JsonDotNet
{
    /// <summary>
    /// This class is the most commonly used type that implements the IPersistentStorage interface.
    /// This class uses a JsonDotNet to attempt to load objects
    /// from the configured ring of files and to write objects to the next file in the ring when requested.
    /// This class is based on the PersistentObjectFileRingStorageAdapterBase class that implements most of the
    /// ring specific logic.
    /// </summary>
    /// <typeparam name="ObjType">
    /// Defines the ObjType on which the IPersistentStorage operates.  Must be a class with default constructor that implements the IPersistSequenceable interface.
    /// </typeparam>
    public class DataContractPersistentJsonDotNetTextFileRingStorageAdapter<ObjType>
        : PersistentObjectFileRingStorageAdapterBase<ObjType>
        , IPersistentStorage<ObjType>
        where ObjType : class, IPersistSequenceable, new()
    {
        /// <summary>
        /// Required constructor.  Takes given <paramref name="name"/> and <paramref name="ringConfig"/>.
        /// </summary>
        public DataContractPersistentJsonDotNetTextFileRingStorageAdapter(string name, PersistentObjectFileRingConfig ringConfig)
            : base(name, ringConfig)
        {            
            Json.JsonSerializerSettings jss = new Json.JsonSerializerSettings()
            {
                TypeNameHandling = Json.TypeNameHandling.Auto,
                TypeNameAssemblyFormatHandling = Json.TypeNameAssemblyFormatHandling.Simple,
                ObjectCreationHandling = Json.ObjectCreationHandling.Replace,       // Otherwise ValueContainerEnvelope properties do not get re-assigned correctly during deserialization.
            };

            js = Json.JsonSerializer.CreateDefault(jss);
        }

        private Json.JsonSerializer js;

        /// <summary>
        /// Allows client to obtain and/or update the <see cref="JsonDotNet.JsonFormattingSpec"/> that is being used to control how this instance serializes of objects to JSON.
        /// </summary>
        public JsonFormattingSpec JsonFormattingSpec { get => _JsonFormattingSpec.MakeCopyOfThis(); set { _JsonFormattingSpec = value?.MakeCopyOfThis() ?? new JsonFormattingSpec(); } }
        private JsonFormattingSpec _JsonFormattingSpec = DefaultJsonFormattingSpec;

        private static readonly JsonFormattingSpec DefaultJsonFormattingSpec = new JsonFormattingSpec() { JsonFormatting = Json.Formatting.Indented };

        /// <summary>
        /// Used to reset the last loaded object its default contents.
        /// <para/>Sets Object = new ObjType();
        /// </summary>
        protected override void InnerClearObject()
        {
            Object = new ObjType();
        }

        /// <summary>
        /// Implementation for required abstract method.  Asks the contained DataContractSerializer instance to read an object from the given readStream using the ReadObject method.
        /// </summary>
        protected override object InnerReadObject(System.IO.Stream readStream)
        {
            using (StreamReader sr = new StreamReader(readStream))
            {
                return js.Deserialize(sr, typeof(ObjType));
            }
        }

        /// <summary>
        /// Implementation for required abstract method.  Asks the contained DataContractSerializer instance to write the given object to the given writeStream using the WriteObject method.
        /// </summary>
        protected override void InnerWriteObject(ObjType obj, System.IO.Stream writeStream)
        {
            js.Formatting = _JsonFormattingSpec.JsonFormatting;

            using (TextWriter tw = new StreamWriter(writeStream))
            using (Json.JsonWriter jw = new Json.JsonTextWriter(tw) { CloseOutput = false, IndentChar = _JsonFormattingSpec.IndentChar, Indentation = _JsonFormattingSpec.IndentCharPerLevelCount, QuoteChar = _JsonFormattingSpec.QuoteChar, QuoteName = _JsonFormattingSpec.QuoteName })
            {
                js.Serialize(jw, obj, TObjectTypeType);
                tw.Flush();
            }
        }

        private Type TObjectTypeType = typeof(ObjType);
    }

}
