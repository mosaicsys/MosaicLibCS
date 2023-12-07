//-------------------------------------------------------------------
/*! @file JsonDotNetDataContractAdapter.cs
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

using MosaicLib.Utils;

using Json = Newtonsoft.Json;

namespace Mosaic.ToolsLib.JsonDotNet
{
    /// <summary>
    /// Consolidated information that is used when specifying Json formatting.  Generally used with <see cref="JsonDotNet"/> related classes here.
    /// </summary>
    public class JsonFormattingSpec : ICopyable<JsonFormattingSpec>
    {
        /// <summary>Gets or sets the internal <see cref="Json.JsonSerializer"/>'s Formatting property.</summary>
        public Json.Formatting JsonFormatting { get; set; } = Json.Formatting.None;

        /// <summary>This is the character that will be used to set each <see cref="Json.JsonTextWriter.IndentChar"/> property.  Defaults to ' '</summary>
        public char IndentChar { get; set; } = ' ';

        /// <summary>This is the indentation character count that will be used, per indent level.  Used to set the <see cref="Json.JsonTextWriter.Indentation"/> property.  Defaults to 2</summary>
        public int IndentCharPerLevelCount { get; set; } = 2;

        /// <summary>This is the string quote character that will be used to set the <see cref="Json.JsonTextWriter.QuoteChar"/> property.  Can only be set to ' or ".  Defaults to ".</summary>
        public char QuoteChar { get; set; } = '"';

        /// <summary>This determines if object names will be quoted.  Used to set the <see cref="Json.JsonTextWriter.QuoteName"/> property.  Defaults to true.</summary>
        public bool QuoteName { get; set; } = true;

        public JsonFormattingSpec MakeCopyOfThis(bool deepCopy = true)
        {
            return (JsonFormattingSpec)MemberwiseClone();
        }
    }

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
            Json.JsonSerializerSettings jss = new Json.JsonSerializerSettings()
            {
                TypeNameHandling = Json.TypeNameHandling.Auto,
                TypeNameAssemblyFormatHandling = Json.TypeNameAssemblyFormatHandling.Simple,
                ObjectCreationHandling = Json.ObjectCreationHandling.Replace,        // Otherwise ValueContainerEnvelope properties do not get re-assigned correctly during deserialization.
            };

            js = Json.JsonSerializer.CreateDefault(jss);
        }

        Json.JsonSerializer js;
        Type TObjectTypeType = typeof(TObjectType);

        #region usage control settings

        /// <summary>
        /// Gets or sets the contained JsonSerializer.Formatting property to determine if generated Json text will be wrapped and indented (Formatting.Indented).
        /// </summary>
        /// <returns>true to write individual elements on new lines and indent; otherwise false.  The default is true.</returns>
        public bool Indent { get { return _JsonFormattingSpec.JsonFormatting == Json.Formatting.Indented; } set { _JsonFormattingSpec.JsonFormatting = value ? Json.Formatting.Indented : Json.Formatting.None; } }

        /// <summary>
        /// Allows client to obtain and/or update the <see cref="JsonDotNet.JsonFormattingSpec"/> that is being used to control how this instance serializes of objects to JSON.
        /// </summary>
        public JsonFormattingSpec JsonFormattingSpec { get => _JsonFormattingSpec.MakeCopyOfThis(); set { _JsonFormattingSpec = value?.MakeCopyOfThis() ?? new JsonFormattingSpec(); } }
        private JsonFormattingSpec _JsonFormattingSpec = DefaultJsonFormattingSpec.MakeCopyOfThis();

        private static readonly JsonFormattingSpec DefaultJsonFormattingSpec = new JsonFormattingSpec() { JsonFormatting = Json.Formatting.Indented };

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
            using (Json.JsonReader jtr = new Json.JsonTextReader(tr))
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
            js.Formatting = _JsonFormattingSpec.JsonFormatting;

            using (TextWriter tw = new StreamWriter(writeStream, Encoding, DefaultStreamWriterBufferSize, true))
            using (Json.JsonWriter jw = new Json.JsonTextWriter(tw) { CloseOutput = false, IndentChar = _JsonFormattingSpec.IndentChar, Indentation = _JsonFormattingSpec.IndentCharPerLevelCount, QuoteChar = _JsonFormattingSpec.QuoteChar, QuoteName = _JsonFormattingSpec.QuoteName })
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
}
