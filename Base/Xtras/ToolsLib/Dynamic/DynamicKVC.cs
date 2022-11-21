//-------------------------------------------------------------------
/*! @file DynamicKVC.cs
 *  @brief Defines the DynamicKVC class.
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

using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mosaic.ToolsLib.Dynamic
{
    /// <summary>
    /// This <see cref="DynamicObject"/> variant supports a set of locally specific use cases.  
    /// This variant internally stores a <see cref="Dictionary{String, ValueContainer}"/>
    /// The set of supported cases are best documented 
    /// These use cases include:
    /// <list type="bullet">
    /// <item>Copy Construction, Content replacement using <see cref="DynamicKVC.SetFrom(object)"/>, and Content update using <see cref="DynamicKVC.UpdateFrom(object, bool)"/></item>
    /// <item>Copy Construction/Set/Update from <see cref="INamedValueSet"/>, <see cref="IEnumerable{KeyValuePair{String, ValueContainer}}"/></item>
    /// <item>Copy Construction/Set/Update from <see cref="IEnumerable{KeyValuePair{String, object}}"/> where each object is converted using <see cref="ValueContainer.CreateFromObject(object)"/></item>
    /// <item>Copy Construction/Set/Update from other <see cref="DynamicObject"/> instances</item>
    /// <item><see cref="DynamicKVC.ToString"/> produced using NamedValueSet.ToStringSML()</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Note: when <see cref="DynamicKVC.EnablePropertyGetterVCSuffixHandling"/> is true, getting a dynamic property who's name ends with "VC" causes the dynamic property return the direct <see cref="ValueContainer"/> value for the key with the "VC" removed from the property name.
    /// This configuration property does not have any effect when setting a dynamic properties value as the key's contents are already set using the <see cref="ValueContainer.CreateFromObject(object)"/> method which "unwraps" such values automatically.
    /// </remarks>
    public class DynamicKVC : DynamicObject
    {
        /// <summary>Static value defines the default value that is used for each new instances <see cref="DynamicKVC.EnablePropertyGetterVCSuffixHandling"/> property value.  Default is <see langword="true"/></summary>
        public static bool DefaultEnablePropertyGetterVCSuffixHandling { get; set; } = true;

        /// <summary>When true, special VC suffix handling is enabled for property getters this instance</summary>
        public bool EnablePropertyGetterVCSuffixHandling { get; set; } = DefaultEnablePropertyGetterVCSuffixHandling;

        /// <summary>Static value defines the default value that is used for each new instances <see cref="DynamicKVC.OverrideIgnoreCase"/> property value.</summary>
        public static bool ? DefaultOverrideIgnoreCase { get; set; }

        /// <summary>
        /// When this key is null and a dynamic property is accessed using a binding with the IgnoreCase propety set to true then the key name will be set to the property name in lower case (using <see cref="String.ToLower"/>).
        /// When this key is false this dynamic object will not ignore the case of given dynamic property names.  
        /// When this key is true this dynamic object will always access keys using the lower case version of any given property name (using <see cref="String.ToLower"/>).
        /// </summary>
        public bool ? OverrideIgnoreCase { get; set; } = DefaultOverrideIgnoreCase;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DynamicKVC() { }

        /// <summary>
        /// "Copy" Constructor.  Uses <see cref="UpdateFrom(object)"/> to set the contents from the given <paramref name="other"/>.
        /// </summary>
        public DynamicKVC(object other)
        {
            UpdateFrom(other);       // since it is already clear
        }

        /// <summary>
        /// Clears the current contents and then calls <see cref="UpdateFrom(object)"/> to populate this instance's contents from the given <paramref name="other"/>.
        /// <para/>supports call chaining.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynamicKVC SetFrom(object other)
        {
            if (kvcDictionary.Count > 0)
                kvcDictionary.Clear();

            return UpdateFrom(other);
        }

        /// <summary>
        /// Updates this instance's contents from the given <paramref name="other"/>'s contents.
        /// This method directly populates/replaces the values for each of the key/value pairs that it extracts from the given <paramref name="other"/> object.
        /// <para/>supports call chaining.
        /// </summary>
        /// <remarks>
        /// Supports the following key source mechanisms:
        /// <list type="bullet">
        /// <item>From another <see cref="DynamicKVC"/>, <see cref="INamedValueSet"/>, or a set of <see cref="KeyValuePair{String, ValueContainer}"/> instances without change.</item>
        /// <item>From a set of <see cref="KeyValuePair{String, Object}"/> instances, from a <see cref="DynamicObject"/> instance, or from a <see cref="ExpandoObject"/> instance, using <see cref="ValueContainer.CreateFromObject(object)"/> on each of the given keyed object values.</item>
        /// <item>From any other <see cref="System.Object"/> by extracting the member names and values for all public instance members (properties or fields) from the given object and converting them using <see cref="ValueContainer.CreateFromObject(object)"/>.</item>
        /// </list>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynamicKVC UpdateFrom(object other)
        {
            if (other == null)
            { }
            else if (other is DynamicKVC otherDKVC)
            {
                foreach (var kvc in otherDKVC.kvcDictionary)
                    kvcDictionary[GetKeyName(kvc.Key)] = kvc.Value;
            }
            else if (other is INamedValueSet nvs)
            {
                foreach (var nv in nvs)
                    kvcDictionary[GetKeyName(nv.Name)] = nv.VC;
            }
            else if (other is IEnumerable<KeyValuePair<string, ValueContainer>> kvcSet)
            {
                foreach (var kvc in kvcSet)
                    kvcDictionary[GetKeyName(kvc.Key)] = kvc.Value;
            }
            else if (other is IEnumerable<KeyValuePair<string, object>> kvpSet)     // note: this handles the ExpandoObject case as well as it directly supports this enumerable type.
            {
                foreach (var kvc in kvpSet)
                    kvcDictionary[GetKeyName(kvc.Key)] = ValueContainer.CreateFromObject(kvc.Value);
            }
            else if (other is DynamicObject d)
            {
                var memberNamesSet = d.GetDynamicMemberNames();

                foreach (var memberName in memberNamesSet)
                {
                    var gmb = new LocalGetMemberBinder(memberName);
                    if (d.TryGetMember(gmb, out object value))
                        kvcDictionary[GetKeyName(gmb.Name)] = ValueContainer.CreateFromObject(value);
                }
            }
            else
            {
                var bindingFlags = (System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.GetProperty | System.Reflection.BindingFlags.GetField);
                var memberSet = other.GetType().GetMembers(bindingFlags);

                foreach (var member in memberSet)
                {
                    if (member is System.Reflection.PropertyInfo propertyInfo)
                        kvcDictionary[GetKeyName(member.Name)] = ValueContainer.CreateFromObject(propertyInfo.GetValue(other));
                    else if (member is System.Reflection.FieldInfo fieldInfo)
                        kvcDictionary[GetKeyName(member.Name)] = ValueContainer.CreateFromObject(fieldInfo.GetValue(other));
                }
            }

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected string GetKeyName(string propertyName, bool ignoreCaseIn = false)
        {
            if (propertyName == null)
                return string.Empty;
            else if (!(OverrideIgnoreCase ?? ignoreCaseIn))
                return propertyName;
            else
                return propertyName.ToLower();
        }

        protected Dictionary<string, ValueContainer> kvcDictionary = new Dictionary<string, ValueContainer>();

        /// <inheritdoc/>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return kvcDictionary.Keys;
        }

        /// <inheritdoc/>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var keyName = GetKeyName(binder.Name, binder.IgnoreCase);
            bool isVCKeyName = EnablePropertyGetterVCSuffixHandling && keyName.EndsWith("VC");
            var keyNameNoSuffix = isVCKeyName ? keyName.Substring(0, keyName.Length - 2) : keyName;

            var success = kvcDictionary.TryGetValue(keyNameNoSuffix, out ValueContainer vc);

            if (!isVCKeyName)
                result = vc.ValueAsObject;
            else
                result = vc;

            return success;
        }

        /// <inheritdoc/>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            kvcDictionary[GetKeyName(binder.Name, binder.IgnoreCase)] = ValueContainer.CreateFromObject(value);

            return true;
        }

        /// <inheritdoc/>
        public override bool TryDeleteMember(DeleteMemberBinder binder)
        {
            return kvcDictionary.Remove(GetKeyName(binder.Name, binder.IgnoreCase));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return kvcDictionary.ConvertToNamedValueSet().ToStringSML();
        }
    }

    internal class LocalGetMemberBinder : System.Dynamic.GetMemberBinder
    {
        public LocalGetMemberBinder(string name, bool ignoreCase = false) : base(name, ignoreCase) { }

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            throw new NotImplementedException();
        }
    }
}
