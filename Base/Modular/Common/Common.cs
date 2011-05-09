//-------------------------------------------------------------------
/*! @file Common.cs
 *  @brief This file defines common defintions that are used by Modular pattern implementations
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
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

namespace MosaicLib.Modular.Common
{
    using System;
    using System.Collections.Generic;
    using System.ServiceModel;
    using System.Runtime.Serialization;
    using MosaicLib.Utils;

    #region Basic types

    /// <summary>Define a Null type that can be used as a ParamType or a ResultType in ParamType and ResultType types in the IAction related pattern types.</summary>
    public class NullObj { public NullObj() { } }

    #endregion

    #region IDType

    /// <summary>This enum is used with ISessionValueSetServices (and other places) to define what the id should be used as for methods that support looking up something by an ID</summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public enum IDType
    {
        /// <summary>ID contains the name of an object</summary>
        [DataMember]
        Name = 0,

        /// <summary>ID contains the UUID of an object</summary>
        [DataMember]
        UUID = 1,
    }

    #endregion

    #region Named Values

    /// <summary>
    /// This object defines a list of NamedValue.  
    /// This list allows use of a CollectionDataContractAttribute to allow the list element names to be explicitly specified
    /// </summary>
    [CollectionDataContract(ItemName = "nvi", Namespace = Constants.ModularNameSpace)]
    public class NamedValueList : List<NamedValue>
    {
        public NamedValueList() { }
        public NamedValueList(IList<NamedValue> rhs)
        {
            Capacity = rhs.Count;
            foreach (NamedValue rhsItem in rhs)
                Add(new NamedValue(rhsItem));
        }
    }

    /// <summary>
    /// This object defines a single named value.  A named value is a pairing of a name and a string and/or a binary value.
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public class NamedValue
    {
        public NamedValue(string name) : this(name, null, null) { }
        public NamedValue(string name, string str) : this(name, str, null) { }
        public NamedValue(string name, byte[] data) : this(name, null, data) { }
        public NamedValue(string name, string str, byte[] data) { Name = name; Str = str; Data = (data != null ? (byte[])data.Clone() : null); }
        public NamedValue(NamedValue rhs) : this(rhs.Name, rhs.Str, rhs.Data) { }

        /// <summary>This property gives the caller access to the name of the named value</summary>
        [DataMember(Order = 1, IsRequired = true)]
        public string Name { get; set; }

        /// <summary>This property if non-null if the NV has been given a string value</summary>
        [DataMember(Order = 2, IsRequired = false, EmitDefaultValue = false)]
        public string Str { get; set; }

        /// <summary>This property is non-null if the NV has been given a binary value (possibly from a ValueSet or other E005DataSource)</summary>
        [DataMember(Order = 3, IsRequired = false, EmitDefaultValue = false)]
        public byte[] Data { get; set; }
    }

    #endregion}
}
