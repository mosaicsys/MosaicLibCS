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
using System.Collections.Generic;
using System.Runtime.Serialization;

using MosaicLib.Modular.Persist;

using Newtonsoft.Json;
using MosaicLib.Utils;

namespace Mosaic.JsonDotNet
{
    #region DataContractJsonDotNetAdapter

    /// <summary>
    /// This adapter class encapsulates a small set of standard use patterns for transcribing between DataContract objects 
    /// and the corresponding JSON strings and files using a JsonSerializer from the Newtonsoft.Json assembly.  
    /// </summary>
    /// <typeparam name="TObjectType">
    /// The templatized object type on which this adapter is defined.  
    /// Must be a DataContract or compatible object type in order to be usable by this adapter.
    /// </typeparam>
    public class DataContractJsonDotNetAdapter<TObjectType>
        : DataContractAdapterBase<TObjectType>
    {
        public DataContractJsonDotNetAdapter()
        {
            JsonSerializerSettings jss = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
                ObjectCreationHandling = ObjectCreationHandling.Replace,        // Otherwise ValueContainerEnvelope properties do not get re-assigned correctly during deserialization.
                Formatting = Formatting.Indented,
            };

            js = JsonSerializer.CreateDefault(jss);
        }

        JsonSerializer js;
        Type TObjectTypeType = typeof(TObjectType);

        #region usage control settings

        /// <summary>
        /// Gets or sets the contained JsonSerializer.Formatting property to determine if generated Json text will be wrapped and indented (Formatting.Indented).
        /// </summary>
        /// <returns>true to write individual elements on new lines and indent; otherwise false.  The default is true.</returns>
        public bool Indent { get { return js.Formatting.IsSet(Formatting.Indented); } set { js.Formatting = js.Formatting.Set(Formatting.Indented, value); } }

        #endregion

        /// <summary>
        /// Attempts to use the contained JsonSerializer to read the deserialize the corresponding object from the given stream using its ReadObject method.  
        /// Returns the object if the read was successful.
        /// </summary>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The VerifyObjectName Property is set to true, and the element name and namespace do not correspond to the values set in the constructor.</exception>
        public override TObjectType ReadObject(System.IO.Stream readStream)
        {
            using (StreamReader sr = new StreamReader(readStream, Encoding))
                return ReadObject(sr);
        }

        /// <summary>
        /// Attempts to use the contained JsonSerializer to read the deserialize the corresponding object from the given TextReader.
        /// </summary>
        /// <param name="tr">Acts as the source of the text from which the JsonSerializer is to deserialize the object.</param>
        public override TObjectType ReadObject(System.IO.TextReader tr)
        {
            using (JsonReader jtr = new JsonTextReader(tr))
            {
                return js.Deserialize<TObjectType>(jtr);
            }
        }

        /// <summary>
        /// Serializes the given object by calling WriteObject on the contained JsonSerializer and passing it the given writeStream.
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <param name="writeStream">Gives the stream on which the serialized data is to be written.</param>
        public override void WriteObject(TObjectType obj, System.IO.Stream writeStream)
        {
            // Todo: determine how to both set Encoding and how to avoid closing the writeStream
            using (TextWriter tw = new StreamWriter(writeStream, Encoding, DefaultStreamWriterBufferSize, true))
            using (JsonWriter jw = new JsonTextWriter(tw) { CloseOutput = false })
            {
                js.Serialize(jw, obj, TObjectTypeType);
                jw.Flush();
            }
        }

        private const int DefaultStreamWriterBufferSize = 4096;

        /// <summary>
        /// Serializes the given object by calling WriteObject on the contained JsonSerializer to serialize and write the object into a String
        /// </summary>
        /// <param name="obj">Gives the object that is to be serialized</param>
        /// <returns>A string containing the serialized representation of the given object as serialized by the contained JsonSerializer</returns>
        public override string ConvertObjectToString(TObjectType obj)
        {
            if (writeMemoryStream == null)
            {
                writeMemoryStream = new MemoryStream();
                AddExplicitDisposeAction(() => Fcns.DisposeOfObject(ref writeMemoryStream));
            }

            try
            {
                WriteObject(obj, writeMemoryStream);

                byte[] buffer = writeMemoryStream.GetBuffer();
                string result = System.Text.Encoding.ASCII.GetString(buffer, 0, unchecked((int)writeMemoryStream.Length));

                writeMemoryStream.Position = 0;
                writeMemoryStream.SetLength(0);

                return result;
            }
            catch
            {
                Fcns.DisposeOfObject(ref writeMemoryStream);
                throw;
            }
        }

        private MemoryStream writeMemoryStream = null;
    }

    #endregion

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
        /// Required constructor.  Takes given name and ringConfig.
        /// </summary>
        public DataContractPersistentJsonDotNetTextFileRingStorageAdapter(string name, PersistentObjectFileRingConfig ringConfig)
            : base(name, ringConfig)
        {
            JsonSerializerSettings jss = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
                ObjectCreationHandling = ObjectCreationHandling.Replace,        // Otherwise ValueContainerEnvelope properties do not get re-assigned correctly during deserialization.
                Formatting = Formatting.Indented,
            };

            js = JsonSerializer.CreateDefault(jss);
        }

        JsonSerializer js;

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
            using (StreamWriter sw = new StreamWriter(writeStream))
            {
                js.Serialize(sw, obj);
                sw.Flush();
            }
        }
    }

}
