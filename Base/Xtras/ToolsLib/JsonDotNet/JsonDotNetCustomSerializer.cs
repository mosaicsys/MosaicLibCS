//-------------------------------------------------------------------
/*! @file JsonDotNetCustomSerializer.cs
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

using MosaicLib.Modular.Common.CustomSerialization;
using MosaicLib.Utils;

using Json = Newtonsoft.Json;

namespace Mosaic.ToolsLib.JsonDotNet
{
    public class JsonDotNetCustomSerializerFactory : ITypeSerializerItemFactory
    {
        public JsonDotNetCustomSerializerFactory(string factoryName = null, JsonFormattingSpec formattingSpec = null)
        {
            FactoryName = factoryName ?? Fcns.CurrentClassName;
            _FormattingSpec = formattingSpec?.MakeCopyOfThis() ?? new JsonFormattingSpec();
        }

        public string FactoryName {get; private set;}

        private JsonFormattingSpec _FormattingSpec { get; }

        ITypeSerializerItem ITypeSerializerItemFactory.AttemptToGenerateSerializationSpecItemFor(Type targetType, string targetTypeStr, string assemblyFileName)
        {
            return new JsonDotNetTypeSerializerItem(targetType: targetType, targetTypeStr: targetTypeStr, assemblyFileName: assemblyFileName, factoryName: FactoryName);
        }

        ITypeSerializerItem ITypeSerializerItemFactory.AttemptToGenerateSerializationSpecItemFor(string targetTypeStr, string assemblyFileName)
        {
            return new JsonDotNetTypeSerializerItem(targetTypeStr: targetTypeStr, factoryName: FactoryName);
        }

        private class JsonDotNetTypeSerializerItem : TypeSerializerItemBase
        {
            public JsonDotNetTypeSerializerItem(Type targetType = null, string targetTypeStr = null, string assemblyFileName = null, string factoryName = null, JsonFormattingSpec jsonFormattingSpec = null)
                : base(targetType: targetType, targetTypeStr: targetTypeStr, assemblyFileName: assemblyFileName, factoryName: factoryName)
            {
                JsonFormattingSpec = jsonFormattingSpec ?? new JsonFormattingSpec();
            }

            public JsonFormattingSpec JsonFormattingSpec { get => _JsonFormattingSpec.MakeCopyOfThis(); set => _JsonFormattingSpec = value?.MakeCopyOfThis() ?? DefaultJsonFormattingspec; }
            private JsonFormattingSpec _JsonFormattingSpec = DefaultJsonFormattingspec;

            private static readonly JsonFormattingSpec DefaultJsonFormattingspec = new JsonFormattingSpec();

            public override TypeAndValueCarrier Serialize(object valueObject)
            {
                Json.JsonSerializer serializer = new Json.JsonSerializer()
                {
                    TypeNameHandling = Json.TypeNameHandling.Auto,
                    TypeNameAssemblyFormatHandling = Json.TypeNameAssemblyFormatHandling.Simple,
                    Formatting = _JsonFormattingSpec.JsonFormatting,
                };

                using (StringWriter sw = new StringWriter())
                using (Json.JsonTextWriter jtw = new Json.JsonTextWriter(sw) { IndentChar = _JsonFormattingSpec.IndentChar, Indentation = _JsonFormattingSpec.IndentCharPerLevelCount, QuoteChar = _JsonFormattingSpec.QuoteChar, QuoteName = _JsonFormattingSpec.QuoteName })
                {
                    serializer.Serialize(jtw, valueObject);
                    return new TypeAndValueCarrier(typeStr: TargetTypeStr, assemblyFileName: AssemblyFileName, factoryName: FactoryName, valueStr: sw.ToString());
                }
            }

            public override object Deserialize(TypeAndValueCarrier valueCarrier)
            {
                Json.JsonSerializer serializer = new Json.JsonSerializer()
                {
                    ObjectCreationHandling = Json.ObjectCreationHandling.Replace,        // Otherwise ValueContainerEnvelope properties do not get re-assigned correctly during deserialization.
                };

                using (StringReader sr = new StringReader(valueCarrier.ValueStr))
                using (Json.JsonTextReader jtr = new Json.JsonTextReader(sr))
                {
                    return serializer.Deserialize(jtr, TargetType);
                }
            }
        }
    }
}
