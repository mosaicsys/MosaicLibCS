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
using System.Collections.Generic;
using System.Runtime.Serialization;

using MosaicLib.Utils;
using MosaicLib.Modular;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.CustomSerialization;

using Newtonsoft.Json;

namespace Mosaic.JsonDotNet
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
                    TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
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
}
