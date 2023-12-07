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

using MessagePack;
using Mosaic.ToolsLib.JsonDotNet;
using Mosaic.ToolsLib.MDRF2.Common;
using Mosaic.ToolsLib.MDRF2.Reader;
using Mosaic.ToolsLib.MessagePackUtils;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mosaic.ToolsLib.Dynamic
{
    /// <summary>
    /// This value is used to configure one or more instances of <see cref="DynamicKVC"/>.
    /// This defines all of the settings that a given <see cref="DynamicKVC"/> instance uses to determine its detailed behavior.
    /// </summary>
    public struct DynamicKVCConfig
    {
        /// <summary>When true, special VC suffix handling is enabled for property getters this instance.  Defaults to <see langword="true"/>.</summary>
        public bool EnablePropertyGetterVCSuffixHandling 
        { 
            get { return _EnablePropertyGetterVCSuffixHandling ?? (EnablePropertyGetterVCSuffixHandling = true); } 
            set { _EnablePropertyGetterVCSuffixHandling = value; } 
        }
        private bool? _EnablePropertyGetterVCSuffixHandling;

        /// <summary>
        /// When this key is null and a dynamic property is accessed using a binding with the IgnoreCase propety set to true then the key name will be set to the property name in lower case (using <see cref="String.ToLower"/>).
        /// When this key is false this dynamic object will not ignore the case of given dynamic property names.  
        /// When this key is true this dynamic object will always access keys using the lower case version of any given property name (using <see cref="String.ToLower"/>).
        /// Defaults to <see langword="null"/>.
        /// </summary>
        public bool? OverrideIgnoreCase { get; set; }

        /// <summary>
        /// When set to true, the <see cref="DynamicKVC.TryGetMember(GetMemberBinder, out object)"/> method will always return true
        /// even if the requested key was not found.  
        /// This allows the client to try to use keys that may or may not exist adn then to use standard nullible techniques
        /// to determine which value to use (such as with ??).
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool TryGetMemberAlwaysReturnsTrue 
        { 
            get { return _TryGetMemberAlwaysReturnsTrue ?? (TryGetMemberAlwaysReturnsTrue = false); } 
            set { _TryGetMemberAlwaysReturnsTrue = value; } 
        }
        private bool? _TryGetMemberAlwaysReturnsTrue;
    }

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
    public class DynamicKVC : DynamicObject, System.Collections.IEnumerable, IEquatable<DynamicKVC>, IDictionary<string, ValueContainer>, IMDRF2MessagePackSerializable
    {
        /// <summary>
        /// Gives the caller access to the <see cref="DynamicKVCConfig"/> value that is currently being used by this object.
        /// </summary>
        public DynamicKVCConfig Config { get { return _Config; } set { _Config = value; } }
        private DynamicKVCConfig _Config;

        /// <inheritdoc cref="DynamicKVCConfig.EnablePropertyGetterVCSuffixHandling"/>
        public bool EnablePropertyGetterVCSuffixHandling { get => _Config.EnablePropertyGetterVCSuffixHandling; set => _Config.EnablePropertyGetterVCSuffixHandling = value; }

        /// <inheritdoc cref="DynamicKVCConfig.OverrideIgnoreCase"/>
        public bool? OverrideIgnoreCase { get => _Config.OverrideIgnoreCase; set => _Config.OverrideIgnoreCase = value; }

        /// <inheritdoc cref="DynamicKVCConfig.TryGetMemberAlwaysReturnsTrue"/>
        public bool TryGetMemberAlwaysReturnsTrue { get => _Config.TryGetMemberAlwaysReturnsTrue; set => _Config.TryGetMemberAlwaysReturnsTrue = value; }

        public ICollection<string> Keys => ((IDictionary<string, ValueContainer>)kvcDictionary).Keys;

        public ICollection<ValueContainer> Values => ((IDictionary<string, ValueContainer>)kvcDictionary).Values;

        public int Count => ((ICollection<KeyValuePair<string, ValueContainer>>)kvcDictionary).Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<string, ValueContainer>>)kvcDictionary).IsReadOnly;

        public ValueContainer this[string key] { get => ((IDictionary<string, ValueContainer>)kvcDictionary)[key]; set => ((IDictionary<string, ValueContainer>)kvcDictionary)[key] = value; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DynamicKVC() { }

        /// <summary>
        /// Constructor that accepts a <see cref="DynamicKVCConfig"/> value.
        /// </summary>
        public DynamicKVC(DynamicKVCConfig config)
        {
            _Config = config;        
        }

        /// <summary>
        /// "Copy" Constructor.  
        /// If the given <paramref name="other"/> is a <see cref="DynamicKVC"/> then this constructor first updates its
        /// <see cref="Config"/> from the given <paramref name="other"/>'s <see cref="DynamicKVC.Config"/>.
        /// Then it uses <see cref="UpdateFrom(object)"/> to set the contents from the given <paramref name="other"/>.
        /// </summary>
        public DynamicKVC(object other)
        {
            if (other is DynamicKVC otherDynamicKVC)
                Config = otherDynamicKVC.Config;

            UpdateFrom(other);
        }

        /// <summary>
        /// Updates the <see cref="DynamicKVC.Config"/> from the given <paramref name="config"/> value.
        /// <para/>supports call chaining.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynamicKVC ConfigFrom(DynamicKVCConfig config)
        {
            Config = config;
            return this;
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
            else if (other is JObject jObject)
            {
                foreach (var jProperty in jObject.Properties())
                {
                    if (jProperty.Value is JObject subJObject)
                        kvcDictionary[GetKeyName(jProperty.Name)] = ValueContainer.CreateFromObject(new DynamicKVC(Config).UpdateFrom(subJObject));
                    else
                        kvcDictionary[GetKeyName(jProperty.Name)] = jProperty.Value.ConvertToVC();
                }
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
            else if (!(_Config.OverrideIgnoreCase ?? ignoreCaseIn))
                return propertyName;
            else
                return propertyName.ToLower();
        }

        #region Initializer construction helper methods (Add variants, AddRange variants)

        /// <summary>
        /// This allows the class to be used with a Dictionary style initializer to add keywords to the set.
        /// Adds/updates the indicated <paramref name="keyName"/> to be set to <see cref="ValueContainer.Empty"/>.
        /// </summary>
        public DynamicKVC Add(string keyName)
        {
            kvcDictionary[GetKeyName(keyName)] = ValueContainer.Empty;

            return this;
        }

        /// <summary>
        /// This allows the class to be used with a Dictionary style initializer to add/update ValueContainer values to the set.
        /// Sets the indicated <paramref name="keyName"/> to the given <paramref name="vc"/> value.
        /// Supports call chaining.
        /// </summary>
        public DynamicKVC Add(string keyName, ValueContainer vc)
        {
            kvcDictionary[GetKeyName(keyName)] = vc;

            return this;
        }

        /// <summary>
        /// This allows the class to be used with a Dictionary style initializer to add values to the set.
        /// Sets the indicated <paramref name="keyName"/> to the given <paramref name="value"/> value using the <see cref="ValueContainer.CreateFromObject(object)"/> method.
        /// Supports call chaining.
        /// </summary>
        public DynamicKVC Add(string keyName, object value)
        {
            kvcDictionary[GetKeyName(keyName)] = ValueContainer.CreateFromObject(value);

            return this;
        }

        /// <summary>
        /// Updates this instance's contents by adding/updating its contents from the given <paramref name="kvpRange"/> 
        /// using <see cref="UpdateFrom(object)"/>
        /// Supports call chaining.
        /// </summary>
        public DynamicKVC AddRange(IEnumerable<KeyValuePair<string, object>> kvpRange)
        {
            return UpdateFrom(kvpRange);
        }

        /// <summary>
        /// Updates this instance's contents by adding/updating its contents from the given <paramref name="kvcRange"/> 
        /// using <see cref="UpdateFrom(object)"/>
        /// Supports call chaining.
        /// </summary>
        public DynamicKVC AddRange(IEnumerable<KeyValuePair<string, ValueContainer>> kvcRange)
        {
            return UpdateFrom(kvcRange);
        }


        #endregion

        protected Dictionary<string, ValueContainer> kvcDictionary = new Dictionary<string, ValueContainer>();

        #region DynamicObject overrides (GetDynamicMemberNames, TryGetMember, TrySetMember, TryDeleteMember, ToString)

        /// <inheritdoc/>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return kvcDictionary.Keys;
        }

        /// <inheritdoc/>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var keyName = GetKeyName(binder.Name, binder.IgnoreCase);
            bool isVCKeyName = _Config.EnablePropertyGetterVCSuffixHandling && keyName.EndsWith("VC");
            var keyNameNoSuffix = isVCKeyName ? keyName.Substring(0, keyName.Length - 2) : keyName;

            var success = kvcDictionary.TryGetValue(keyNameNoSuffix, out ValueContainer vc);

            if (!isVCKeyName)
                result = vc.ValueAsObject;
            else
                result = vc;

            return success || _Config.TryGetMemberAlwaysReturnsTrue;
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
            return ConvertToNamedValueSet().ToStringSML();
        }

        /// <summary>
        /// Converts this <see cref="DynamicKVC"/> instance into a <see cref="NamedValueSet"/> and returns it.
        /// If <paramref name="recusrive"/> is true and this instance dictionary contains sub-ordinate <see cref="DynamicKVC"/> instances then this method will convert them to <see cref="NamedValueSet"/> as well (recursively)
        /// </summary>
        public NamedValueSet ConvertToNamedValueSet(bool recusrive = true)
        {
            NamedValueSet nvs = new NamedValueSet(); ;

            foreach (var kvc in kvcDictionary)
            {
                if (kvc.Value.IsObject && kvc.Value.ValueAsObject is DynamicKVC dynamicKVC)
                    nvs.SetValue(kvc.Key, dynamicKVC.ConvertToNamedValueSet(recusrive: recusrive).CreateVC());
                else
                    nvs.SetValue(kvc.Key, kvc.Value);
            }

            return nvs;
        }

        /// <summary>
        /// Recursively converts this <see cref="DynamicKVC"/> instance into a <see cref="Newtonsoft.Json.Linq.JObject"/> and returns it.
        /// </summary>
        public Newtonsoft.Json.Linq.JObject ConvertToJObject()
        {
            var jObject = new Newtonsoft.Json.Linq.JObject();

            foreach (var kvc in kvcDictionary)
            {
                if (kvc.Value.IsObject && kvc.Value.o is DynamicKVC dynamicKVC)
                    jObject[kvc.Key] = dynamicKVC.ConvertToJObject();
                else
                    jObject[kvc.Key] = kvc.Value.ConvertToJToken();
            }

            return jObject;
        }

        #endregion

        #region Explicit Methods required to support property initializer construction.

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return kvcDictionary.GetEnumerator();
        }

        #endregion

        #region IEquatable<DynamicKVC>, IEnumerable<KeyValuePair<string, ValueContainer>>

        /// <inheritdoc/>
        public bool Equals(DynamicKVC other)
        {
            return kvcDictionary.IsEqualTo(other.kvcDictionary);
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, ValueContainer>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, ValueContainer>>)kvcDictionary).GetEnumerator();
        }

        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            return ((IDictionary<string, ValueContainer>)kvcDictionary).ContainsKey(key);
        }

        /// <inheritdoc/>
        void IDictionary<string, ValueContainer>.Add(string key, ValueContainer value)
        {
            ((IDictionary<string, ValueContainer>)kvcDictionary).Add(key, value);
        }

        /// <inheritdoc/>
        public bool Remove(string key)
        {
            return ((IDictionary<string, ValueContainer>)kvcDictionary).Remove(key);
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, out ValueContainer value)
        {
            return ((IDictionary<string, ValueContainer>)kvcDictionary).TryGetValue(key, out value);
        }

        /// <inheritdoc/>
        public void Add(KeyValuePair<string, ValueContainer> item)
        {
            ((ICollection<KeyValuePair<string, ValueContainer>>)kvcDictionary).Add(item);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ((ICollection<KeyValuePair<string, ValueContainer>>)kvcDictionary).Clear();
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, ValueContainer> item)
        {
            return ((ICollection<KeyValuePair<string, ValueContainer>>)kvcDictionary).Contains(item);
        }

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, ValueContainer>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, ValueContainer>>)kvcDictionary).CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, ValueContainer> item)
        {
            return ((ICollection<KeyValuePair<string, ValueContainer>>)kvcDictionary).Remove(item);
        }

        #endregion

        #region IMDRF2MessagePackSerializable implementation

        void IMDRF2MessagePackSerializable.Serialize(ref MessagePackWriter mpWriter, MessagePackSerializerOptions mpOptions)
        {
            KVCSetFormatter.Instance.Serialize(ref mpWriter, kvcDictionary, mpOptions);
        }

        void IMDRF2MessagePackSerializable.Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions)
        {
            var kvcSet = KVCSetFormatter.Instance.Deserialize(ref mpReader, mpOptions);

            kvcDictionary = kvcSet.MapNullToEmpty().ToDictionary(kvc => kvc.Key, elementSelector: kvc => kvc.Value);
        }

        #endregion
    }

    internal class LocalGetMemberBinder : System.Dynamic.GetMemberBinder
    {
        public LocalGetMemberBinder(string name, bool ignoreCase = false) : base(name, ignoreCase) { }

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// This is a factory class for creating consistently configured instances of the <see cref="DynamicKVC"/> class.
    /// </summary>
    public class DynamicKVCFactory
    {
        /// <summary>
        /// This gives a single static <see cref="DynamicKVCFactory"/>.  This is typically used by specific ToDynamicKVC extension methods.
        /// </summary>
        public static DynamicKVCFactory Default { get; } = new DynamicKVCFactory();

        /// <summary>
        /// Gives the the <see cref="DynamicKVCConfig"/> value that is currently being used by this object when constructing new DynamicKVC instances.
        /// </summary>
        public DynamicKVCConfig Config { get; set; }

        /// <summary>
        /// Factory method.  
        /// This method constructs a new <see cref="DynamicKVC"/> instance,
        /// Initializes its <see cref="DynamicKVC.Config"/> from this factory instance's <see cref="Config"/>,
        /// And then calls <see cref="DynamicKVC.UpdateFrom(object)"/> on the given <paramref name="from"/> to optinally initialize the created object's contents.
        /// </summary>
        public DynamicKVC Create(object from = null)
        {
            return new DynamicKVC(Config).UpdateFrom(from);
        }
    }

    /// <summary>
    /// MDRF2 type name handler for use with <see cref="DynamicKVC"/> instances.
    /// <para/>Note: The serializer used by this type name converter supports converting arbitrary object types to DynamicKVC instances in order to serialize them.
    /// Deserialization always generates <see cref="IMDRF2QueryRecord"/>{<see cref="DynamicKVC"/>} records.
    /// <para/>Note: this deserializer can be used with any 
    /// </summary>
    public class DynamicKVCTypeNameConverter : TypeNameHandlers.MDRF2MessagePackSerializableTypeNameHandler<DynamicKVC>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public DynamicKVCTypeNameConverter() 
        {
            FactoryMethod = () => Factory.Create();
        }

        /// <summary>
        /// <inheritdoc/>
        /// <para/>If the given <paramref name="value"/> is not a <see cref="DynamicKVC"/> then this method constructs a new <see cref="DynamicKVC"/> instance from the given <paramref name="value"/> and then serializes that new instance.
        /// </summary>
        public override void Serialize(ref MessagePackWriter mpWriter, object value, MessagePackSerializerOptions mpOptions)
        {
            var d = value as DynamicKVC ?? Factory.Create(value);

            base.Serialize(ref mpWriter, d, mpOptions);
        }

        /// <summary>
        /// Specifies the <see cref="DynamicKVCFactory"/> instance to use.  Defaults to <see cref="DynamicKVCFactory.Default"/>.
        /// </summary>
        public DynamicKVCFactory Factory { get => _Factory; set => _Factory = value ?? DynamicKVCFactory.Default; }
        private DynamicKVCFactory _Factory = DynamicKVCFactory.Default;
    }
}
