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
    public class NullObj 
    {
        /// <summary>Default (only) constructor</summary>
        public NullObj() { } 
    }

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
        /// <summary>Default constructor</summary>
        public NamedValueList() { }

        /// <summary>
        /// Copy constructor from IList{NamedValues}.  
        /// Creates a new NamedValueList from the given list of NamedValues by cloning each of them.
        /// </summary>
        public NamedValueList(IList<NamedValue> rhs)
        {
            Capacity = rhs.Count;
            foreach (NamedValue rhsItem in rhs)
                Add(new NamedValue(rhsItem));
        }

        /// <summary>
        /// get/set index operator by name.  Gets the NamedValue for the requested name.  
        /// getter returns the node from the contained list or a new NamedValue for the given name if the requested one was not found.
        /// setter replaces the selected NamedValue item in the list or adds a new NamedValue object to the list.
        /// </summary>
        /// <param name="name">Gives the name for the desired NamedValue to get/replace</param>
        /// <returns>The selected NamedValue as described in the summary</returns>
        /// <exception cref="System.InvalidOperationException">thrown by setter if the given name is not equal to the Name property from the assigned NamedValue object</exception>
        public NamedValue this[string name]
        {
            get
            {
                BuildDictIfNeeded();
                int idx = -1;
                if (nameToIndexDict.TryGetValue(name, out idx))
                    return this[idx];
                else
                    return new NamedValue(name);
            }

            set
            {
                if (name != value.Name)
                    throw new System.InvalidOperationException("Index string must equal Name in given NamedValue object");

                BuildDictIfNeeded();
                int idx = -1;
                if (nameToIndexDict.TryGetValue(name, out idx))
                    this[idx] = value;
                else
                {
                    idx = Count;
                    Add(value);
                    nameToIndexDict[name] = idx;
                }
            }
        }

        /// <summary>Contains the dictionary used to convert from names to list indexes.  Only built if [name] index operator is used.</summary>
        protected Dictionary<string, int> nameToIndexDict = null;

        /// <summary>Method used by client to reset the nameToIndex Dictionary if contents of NamedValues list is changed outside of use of [name] index operator.</summary>
        public void InvalidateNameToIndexDictionary() 
        { 
            nameToIndexDict = null; 
        }

        /// <summary>Internal method used to build the nameToIndexDictionary when needed.</summary>
        protected void BuildDictIfNeeded()
        {
            if (nameToIndexDict == null)
            {
                nameToIndexDict = new Dictionary<string, int>();
                for (int idx = 0; idx < Count; idx++)
                {
                    NamedValue nvItem = this[idx];
                    nameToIndexDict[nvItem.Name] = idx;
                }
            }
        }
    }

    /// <summary>
    /// This object defines a single named value.  A named value is a pairing of a name and a string and/or a binary value.
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public class NamedValue
    {
        /// <summary>Constructor - builds NamedValue containing null string and binary data</summary>
        public NamedValue(string name) : this(name, null, null) { }
        /// <summary>Constructor - builds NamedValue containing given string and null binary data</summary>
        public NamedValue(string name, string str) : this(name, str, null) { }
        /// <summary>Constructor - builds NamedValue containing null string and given binary data.  Clones the binary data.</summary>
        public NamedValue(string name, byte[] data) : this(name, null, data) { }
        /// <summary>Constructor - builds NamedValue containing given string and given binary data.  Clones the binary data if non-null</summary>
        public NamedValue(string name, string str, byte[] data) { Name = name; Str = str; Data = (data != null ? (byte[])data.Clone() : null); }
        /// <summary>Copy constructor.  builds clone of the given rhs containing copy of Name and Str properties and clone of binary data if it is non-null.</summary>
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

        /// <summary>Returns true if the object has a non-null Str property value</summary>
        public bool HasStrValue { get { return (Str != null); } }
        /// <summary>Returns true if the object has a non-null Data property value</summary>
        public bool HasDataValue { get { return (Data != null); } }
        /// <summary>Returns true if the object has a non-null Str property or a non-null Data property value</summary>
        public bool HasValue { get { return (HasStrValue || HasDataValue); } }
    }

    #endregion}
}
