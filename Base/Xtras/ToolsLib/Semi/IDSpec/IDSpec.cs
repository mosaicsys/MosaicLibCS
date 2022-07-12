//-------------------------------------------------------------------
/*! @file IDSpec.cs
 *  @brief This file provides common definitions that relate to the use of object that support IDSpec lookup.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2022 Mosaic Systems Inc.
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
using System.Runtime.Serialization;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;

namespace Mosaic.ToolsLib.Semi.IDSpec
{
    /// <summary>
    /// This interface supports obtaining specification information about one or more IDs from an ID assignment/management system.
    /// </summary>
    public interface IIDSpecLookupHelper
    {
        /// <summary>
        /// Returns the <see cref="IIDSpec"/> for the given <paramref name="name"/>, <paramref name="idTypeMask"/>, and <paramref name="nvs"/>, if found, or returns null if no corresponding <see cref="IIDSpec"/> was found.
        /// </summary>
        IIDSpec GetIDSpec(string name, IDType idTypeMask = IDType.Any, INamedValueSet nvs = null);

        /// <summary>
        /// Returns the <see cref="IIDSpec"/> for the given <paramref name="id"/>, <paramref name="idTypeMask"/>, and <paramref name="nvs"/>, if found, or returns null if no corresponding <see cref="IIDSpec"/> was found.
        /// </summary>
        IIDSpec GetIDSpec(int id, IDType idTypeMask = IDType.Any, INamedValueSet nvs = null);

        /// <summary>
        /// Returns the set of <see cref="IIDSpec"/> that match the given <paramref name="idTypeMask"/>, and <paramref name="nvs"/>.
        /// If a non-empty <paramref name="nameArray"/> is provided then the resulting <see cref="IIDSpec"/> array will be for the corresponding set of names.
        /// </summary>
        IIDSpec[] GetIDSpecs(string[] nameArray = null, IDType idTypeMask = IDType.Any, INamedValueSet nvs = null);

        /// <summary>
        /// This method is used to obtain a subtree lookup helper instance that performs lookup operations on a sub-tree of the id name space.
        /// </summary>
        IIDSpecLookupHelper GetSubtreeLookupHelper(string treeName, INamedValueSet nvs = null, string infixDelimiter = ".");
    }

    /// <summary>
    /// This interface defines the set of basic, readonly, information that is known (has been specified) for a given ID.
    /// <para/>Note that although the ID is generally expected to be unique accross all IDTypes, actual combinations of ID, IDType and ConnectionID are only required to be unique when all three are used together.
    /// </summary>
    public interface IIDSpec
    {
        /// <summary>Gives the Name that is associated with this ID</summary>
        string Name { get; }

        /// <summary>Gives the <see cref="IDType"/> that is associated with this ID</summary>
        IDType IDType { get; }

        /// <summary>Gives the numeric identifier.  Normally only strictly possitive, non-zero values, are used here.</summary>
        int ID { get; }

        /// <summary>Gives the NamedValueSet Meta Data that is known about the identifier.  The specific expected contents are not defined here.</summary>
        INamedValueSet NVS { get; }
    }

    /// <summary>
    /// This is the primary implementation type for the <see cref="IIDSpec"/> interface.
    /// </summary>
    [DataContract(Namespace = Constants.SemiNameSpace)]
    public struct IDSpec : IIDSpec
    {
        /// <ineritdoc/>
        public string Name { get; set; }

        /// <ineritdoc/>
        public IDType IDType { get; set; }

        /// <ineritdoc/>
        public int ID { get; set; }

        /// <ineritdoc/>
        public INamedValueSet NVS { get { return nvs.MapNullToEmpty(); } set { nvs = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }
        private INamedValueSet nvs;
    }

    /// <summary>
    /// IDType.  This type is used both to indicate the specific type of some <see cref="IIDSpec"/> item and as a mask of types to match
    /// when searching for an ID by name.
    /// <para/>Any (0xffffffff), None (0x00), ALID (0x01), CEID (0x02), ECID (0x04), DVID (0x08), SVID (0x10), ModuleID (0x20)
    /// </summary>
    [DataContract(Namespace = Constants.SemiNameSpace)]
    [Flags]
    public enum IDType : int
    {
        /// <summary>Any - placeholder value indicates that any type may be used.</summary>
        [EnumMember]
        Any = unchecked((int) 0xffffffff),

        /// <summary>Placeholder default value for use when type is not known.</summary>
        [EnumMember]
        None = 0x00,

        /// <summary>Alarm ID</summary>
        [EnumMember]
        ALID = 0x01,

        /// <summary>Collection Event ID</summary>
        [EnumMember]
        CEID = 0x02,

        /// <summary>Equipment Constant ID</summary>
        [EnumMember]
        ECID = 0x04,

        /// <summary>Data Variable ID (aka DVNAME in E005)</summary>
        [EnumMember]
        DVID = 0x08,

        /// <summary>Status Variable ID</summary>
        [EnumMember]
        SVID = 0x10,

        /// <summary>Module ID</summary>
        [EnumMember]
        ModuleID = 0x20,
    }

    /// <summary>
    /// This struct is used with the corresponding <see cref="ExtensionMethods.GetNameIIDSpecPair(IIDSpecLookupHelper, string, IDType, INamedValueSet)"/>
    /// method to simplify cases where the caller needs to retain the original name that it was asking for in addition to the results of the lookup.
    /// </summary>
    public struct NameIIDSpecPair
    {
        /// <summary>
        /// Gives the Name that was passed to the corresponding GetIDSpec method.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gives the <see cref="IIDSpec"/> that was returned from the corresponding GetIDSpec method.
        /// </summary>
        public IIDSpec IDSpec { get; set; }
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns the <see cref="IIDSpec"/> for the given <paramref name="name"/>, <paramref name="idTypeMask"/>, and <paramref name="nvs"/>, if found, or returns null if no corresponding <see cref="IIDSpec"/> was found.
        /// </summary>
        public static NameIIDSpecPair GetNameIIDSpecPair(this IIDSpecLookupHelper specLookupHelper, string name, IDType idTypeMask = IDType.Any, INamedValueSet nvs = null)
        {
            return new NameIIDSpecPair()
            {
                Name = name,
                IDSpec = specLookupHelper?.GetIDSpec(name, idTypeMask, nvs),
            };
        }

    }

    //-------------------------------------------------------------------
}
