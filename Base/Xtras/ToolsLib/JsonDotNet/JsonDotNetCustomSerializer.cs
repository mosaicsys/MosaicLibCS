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
using System.Linq;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.CustomSerialization;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosaic.ToolsLib.JsonDotNet
{
    public class JsonDotNetCustomSerializerFactory : ITypeSerializerItemFactory
    {
        public JsonDotNetCustomSerializerFactory(string factoryName = null)
        {
            FactoryName = factoryName ?? Fcns.CurrentClassName;
        }

        public string FactoryName {get; private set;}

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
            public JsonDotNetTypeSerializerItem(Type targetType = null, string targetTypeStr = null, string assemblyFileName = null, string factoryName = null)
                : base(targetType: targetType, targetTypeStr: targetTypeStr, assemblyFileName: assemblyFileName, factoryName: factoryName)
            { }

            public override TypeAndValueCarrier Serialize(object valueObject)
            {
                JsonSerializer serializer = new JsonSerializer()
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                };

                using (StringWriter sw = new StringWriter())
                using (JsonTextWriter jtw = new JsonTextWriter(sw))
                {
                    serializer.Serialize(jtw, valueObject);
                    return new TypeAndValueCarrier(typeStr: TargetTypeStr, assemblyFileName: AssemblyFileName, factoryName: FactoryName, valueStr: sw.ToString());
                }
            }

            public override object Deserialize(TypeAndValueCarrier valueCarrier)
            {
                JsonSerializer serializer = new JsonSerializer()
                {
                    ObjectCreationHandling = ObjectCreationHandling.Replace,        // Otherwise ValueContainerEnvelope properties do not get re-assigned correctly during deserialization.
                };

                using (StringReader sr = new StringReader(valueCarrier.ValueStr))
                using (JsonTextReader jtr = new JsonTextReader(sr))
                {
                    return serializer.Deserialize(jtr, TargetType);
                }
            }
        }
    }

    /// <summary>
    /// ExtensionMethods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// This extension method is used to convert the given <paramref name="jToken"/> to its ValueContainer equivalant
        /// </summary>
        public static ValueContainer ConvertToVC(this JToken jToken, bool rethrow = false)
        {
            switch (jToken.Type)
            {
                case JTokenType.Object: return ValueContainer.CreateNVS(((JObject)jToken).ConvertToNVS(rethrow: rethrow));
                case JTokenType.Property: return ValueContainer.CreateNV(((JProperty)jToken).ConvertToNV(rethrow: rethrow));
                case JTokenType.Array: return ValueContainer.CreateL(((JArray)jToken).ConvertToVCSet(rethrow: rethrow));

                case JTokenType.None: return ValueContainer.Empty;
                case JTokenType.Null: return ValueContainer.Null;

                case JTokenType.Boolean: return ValueContainer.CreateBo(jToken.ToObject<bool>());
                case JTokenType.Integer: return ValueContainer.CreateI8(jToken.ToObject<long>());
                case JTokenType.Float: return ValueContainer.CreateF8(jToken.ToObject<double>());
                case JTokenType.String: return ValueContainer.CreateA(jToken.ToObject<string>());
                case JTokenType.Date: return ValueContainer.CreateDT(jToken.ToObject<DateTime>());
                case JTokenType.TimeSpan: return ValueContainer.CreateTS(jToken.ToObject<TimeSpan>());

                default:
                    if (rethrow)
                        throw new System.InvalidCastException($"JToken type {jToken.Type} is not supported here [{jToken}]");
                    else
                        return ValueContainer.CreateA($"Usupported JToken: {jToken}");
            }
        }

        /// <summary>
        /// This extension method is used to convert the given <paramref name="jObject"/> to its INamedValueSet equivalant.
        /// </summary>
        public static INamedValueSet ConvertToNVS(this JObject jObject, bool rethrow = false)
        {
            return new NamedValueSet(jObject.Properties().Select(JProperty => JProperty.ConvertToNV(rethrow: rethrow))).MakeReadOnly();
        }

        /// <summary>
        /// This extension method is used to convert the given <paramref name="jProperty"/> to its INamedValue equivalant.
        /// </summary>
        public static INamedValue ConvertToNV(this JProperty jProperty, bool rethrow = false)
        {
            return new NamedValue(jProperty.Name, jProperty.Value.ConvertToVC(rethrow: rethrow)).MakeReadOnly();
        }

        /// <summary>
        /// This extension method is used to convert the given <paramref name="jArray"/> to its ReadOnlyIList{ValueContainer} equivalant.
        /// </summary>
        public static ReadOnlyIList<ValueContainer> ConvertToVCSet(this JArray jArray, bool rethrow = false)
        {
            return new ReadOnlyIList<ValueContainer>(jArray.AsJEnumerable().Select(jToken => jToken.ConvertToVC(rethrow: rethrow)));
        }
    }
}
