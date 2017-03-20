//-------------------------------------------------------------------
/*! @file ConfigValueSetAdapter.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2014 Mosaic Systems Inc.  
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
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.Utils;

namespace MosaicLib.Modular.Config
{
    #region Custom Attributes and related defintions to be used with the ConfigValueSetAdapter

    namespace Attributes
    {
        //-------------------------------------------------------------------

        /// <summary>
        /// This attribute is used to annotate public settable properties and fields in a class in order that the class can be used as the ConfigValueSet for
        /// a ConfigValueSetAdapter adapter.  Each settable property or field in the ConfigValueSet class specifies a specific property and value source that 
		/// will receive the initial value and possibly later updates for config key's value that use normal update behavior.
        /// <para/>Name = null, NameAdjust = NameAdjust.Prefix0, UpdateNormally = false (ReadOnlyOnce = true), IsOptional = false, SilenceIssues = false
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
        public class ConfigItemAttribute : AnnotatedItemAttributeBase
        {
            /// <summary>
            /// Default constructor.
            /// <para/>Name = null, NameAdjust = NameAdjust.Prefix0, UpdateNormally = false (ReadOnlyOnce = true), IsOptional = false, SilenceIssues = false
            /// </summary>
            public ConfigItemAttribute() 
                : base()
            { }

            /// <summary>
            /// This property defines the config key's value update behavior that is used for the identified config key's value as Normal or ReadOnlyOnce (default)
            /// </summary>
            public ConfigKeyAccessFlags AccessFlags { get { return accessFlags; } set { accessFlags = value; } }
            private ConfigKeyAccessFlags accessFlags = new ConfigKeyAccessFlags();

            /// <summary>
            /// gives C# client access to the NamedValueSet that contains some of the values that are setable here.
            /// </summary>
            public INamedValueSet MetaData
            {
                get
                {
                    if (metaData == null)
                    {
                        NamedValueSet nvs = new NamedValueSet() { { "AccessFlags", AccessFlags } };

                        if (ensureExistsHasBeenSet)
                            nvs.Add("EnsureExists", EnsureExists);

                        if (defaultProviderNameHasBeenSet)
                            nvs.Add("DefaultProviderName", DefaultProviderName);

                        if (additionalKeywordsHasBeenSet)
                            nvs.AddRange(AdditionalKeywords.Select(keyword => new NamedValue(keyword)));

                        INamedValueSet mergeWithNVS = GetDerivedTypeMetaDataToMerge();
                        if (!mergeWithNVS.IsNullOrEmpty())
                            nvs.SubSets = new INamedValueSet[] { mergeWithNVS };

                        metaData = nvs.ConvertToReadOnly();
                    }

                    return metaData;
                }
            }
            private INamedValueSet metaData = null;

            /// <summary>
            /// Derived types may override this method implementation to allow them merge the returned meta-data into the per key meta-data that will be used with the key access objects generated here from this attribute.
            /// <para/>Please note that this method should always return an INVS instance that has already been converted to be ReadOnly so that later layers where this is also done will not generate an excessive number of clones of the original one returned here.
            /// </summary>
            protected virtual INamedValueSet GetDerivedTypeMetaDataToMerge() { return null; }

            /// <summary>
            /// This property is true when the config key's value will follow the MayBeChanged update behavior.  (defaults to false)
            /// </summary>
            public bool UpdateNormally { get { return accessFlags.MayBeChanged; } set { accessFlags.MayBeChanged = value; } }

            /// <summary>
            /// This property is true when the config key's value will follow the ReadOnlyOnce update behavior.  This may be used as a shorthand for the ValueUpdateBehavior property.  (defaults to true)
            /// </summary>
            public bool ReadOnlyOnce { get { return accessFlags.ReadOnlyOnce; } set { accessFlags.ReadOnlyOnce = value; } }

            /// <summary>
            /// When an item is marked as optional, no error messages will be generated if the config key is not found during setup.  (defaults to false)
            /// </summary>
            public bool IsOptional { get { return accessFlags.IsOptional; } set { accessFlags.IsOptional = value; } }

            /// <summary>
            /// When an item is marked to SilenceLogging, neither Issue nor Value messages will be emitted in relation to attempts to access this key.  (defaults to false)
            /// </summary>
            public bool SilenceLogging { get { return accessFlags.SilenceLogging; } set { accessFlags.SilenceLogging = value; } }

            /// <summary>
            /// Set this property to true to attempt to create a config key in the default provider, or in any provider that supports this feature, if the key was not already found.
            /// </summary>
            public bool EnsureExists { get { return ensureExists; } set { ensureExists = value; ensureExistsHasBeenSet = true; metaData = null; } }
            private bool ensureExists = false;
            private bool ensureExistsHasBeenSet = false;

            /// <summary>
            /// This property may be used to request that the key be obtained from the named provider before searching other providers for the same name.  
            /// In addition when using the EnsureExists feature, the provider named here will be asked to create the requested config key before consulting other providers.
            /// </summary>
            public string DefaultProviderName { get { return defaultProviderName.MapNullToEmpty(); } set { defaultProviderName = value; defaultProviderNameHasBeenSet = true; metaData = null; } }
            private string defaultProviderName = null;
            private bool defaultProviderNameHasBeenSet = false;

            /// <summary>
            /// Allows the caller to specify a set of additional keywords to be included in the NamedValueSet that will be produced when using the MetaData property.
            /// </summary>
            public string[] AdditionalKeywords { get { return (additionalKeywords ?? emptyStringArray); } set { additionalKeywords = value; additionalKeywordsHasBeenSet = true; metaData = null; } }
            private string [] additionalKeywords = null;
            private bool additionalKeywordsHasBeenSet = false;

            private static readonly string[] emptyStringArray = new string[0];

        }
    }

    #endregion

    //-------------------------------------------------------------------
    #region ConfigValueSetAdapter

    /// <summary>
    /// This adapter class provides a client with a tool that can be constructed and setup to monitor (as appropriate) a set of config keys
    /// and to place their resulting values into the corresponding set of Attributed identified member items in a given instance of the corresponding
    /// TConfigValueSet class.  This adapter class supports both Normal and ReadOnlyOnce config key's values.
    /// </summary>
    /// <typeparam name="TConfigValueSet">
    /// Specifies the class type on which this adapter will operate.  
    /// Adapter harvests the list of <see cref="MosaicLib.Modular.Config.Attributes.ConfigItemAttribute"/> annotated public member items from this type.
    /// </typeparam>
    /// <remarks>
    /// The primary methods/properties used on this adapter are: Construction, ValueSet, Setup, Update, IsUpdateNeeded
    /// </remarks>

    public class ConfigValueSetAdapter<TConfigValueSet> : DisposableBase where TConfigValueSet : class
    {
        #region Ctor

        /// <summary>
        /// Default constructor.  Assigns adapter to use default Config.Instance IConfig service instance.  
        /// For use with Property Initializers and the Setup method to define and setup the adapter instance for use.
        /// Setup method is used to generate final derived item names and to bind and make the initial update to the ValueSet contents.
        /// Setup method may also specify/override the config instance that is to be used.
        /// <para/>Please Note: the Setup method must be called before the adapter can be used.  
        /// </summary>
        public ConfigValueSetAdapter()
            : this(null)
        { }

        /// <summary>
        /// Config instance constructor.  Assigns adapter to use given configInstance IConfig service instance.  This may be overriden during the Setup call.
        /// For use with Property Initializers and the Setup method to define and setup the adapter instance for use.
        /// <para/>Please Note: the Setup method must be called before the adapter can be used.  
        /// </summary>
        public ConfigValueSetAdapter(IConfig configInstance)
        {
            ConfigInstance = configInstance;

            configItemInfoList = AnnotatedClassItemAccessHelper<Attributes.ConfigItemAttribute>.ExtractItemInfoAccessListFrom(typeof(TConfigValueSet), ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeInheritedItems);
            NumItems = configItemInfoList.Count;

            keySetupInfoArray = new KeySetupInfo[NumItems];
        }

        #endregion

        #region public methods and properies

        /// <summary>
        /// Contains the ValueSet object that is to receive the selected config key values during initial Setup and then during later Update calls.
        /// </summary>
        public TConfigValueSet ValueSet { get; set; }

        /// <summary>Defines the emitter used to emit Setup related errors.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter SetupIssueEmitter { get { return FixupEmitterRef(ref setupIssueEmitter); } set { setupIssueEmitter = value; } }
        /// <summary>Defines the emitter used to emit Update related errors.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter UpdateIssueEmitter { get { return FixupEmitterRef(ref updateIssueEmitter); } set { updateIssueEmitter = value; } }
        /// <summary>Defines the emitter used to emit Update related changes in config point values.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter ValueNoteEmitter { get { return FixupEmitterRef(ref valueNoteEmitter); } set { valueNoteEmitter = value; } }

        /// <summary>
        /// When this property has non-empty contents, the MetaData for each IConfigKeyAccessSpec generated by this adapter will have the contents of this NVS merged
        /// into whatever MetaData the key was already given.  Merging uses NamedValueMergeBehavior.AddNewItems behavior.
        /// </summary>
        public INamedValueSet DefaultKeyMetaData { get; set; }

        ///<summary>
        ///After Setup has been performed, this property will contain the IConfig instance with which this adapter interacts.
        ///This property will remain in its default state (null) until Setup has been performed.
        ///</summary>
        private IConfig ConfigInstance { get; set; }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, 
        /// registers these config key's values with the Configuration Service and updates the ValueSet's items to contains the corresponding values from the 
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NamePrefixGroup attribute property value.
        /// </param>
        public ConfigValueSetAdapter<TConfigValueSet> Setup(params string[] baseNames)
        {
            return Setup(null, baseNames);
        }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, 
        /// registers these config key's values with the Configuration Service and updates the ValueSet's items to contains the corresponding values from the 
        /// </summary>
        /// <param name="configInstance">Allows the caller to (re)specifiy the IConfig instance that is to be used henceforth by this adapter</param>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public ConfigValueSetAdapter<TConfigValueSet> Setup(IConfig configInstance, params string[] baseNames)
        {
            if (configInstance != null || ConfigInstance == null)
                ConfigInstance = configInstance ?? Config.Instance;

            if (ValueSet == null)
                throw new System.NullReferenceException("ValueSet property must be non-null before Setup can be called");

            // setup all of the static information
            bool anySetupIssues = false;
            int idx;

            for (idx = 0; idx < NumItems; idx++)
            {
                ItemInfo<Attributes.ConfigItemAttribute> itemInfo = configItemInfoList[idx];
                Attributes.ConfigItemAttribute itemAttribute = itemInfo.ItemAttribute;

                string memberName = itemInfo.MemberInfo.Name;
                string itemName = (!string.IsNullOrEmpty(itemAttribute.Name) ? itemAttribute.Name : itemInfo.MemberInfo.Name);
                string fullKeyName = GenerateFullKeyName(itemInfo, baseNames);

                if (!itemInfo.CanSetValue)
                {
                    if (!itemAttribute.SilenceIssues)
                        SetupIssueEmitter.Emit("Member/key '{0}'/'{1}' is not usable:  There is no valid public member Setter, in ValueSet type '{2}'", memberName, fullKeyName, tConfigValueSetTypeStr);
                    continue;
                }

                ConfigKeyAccessFlags customAccessFlags = new ConfigKeyAccessFlags(itemAttribute.AccessFlags) { SilenceIssues = true };
                INamedValueSet keyMetaData = itemAttribute.MetaData.MapEmptyToNull();

                if (keyMetaData.IsNullOrEmpty())
                    keyMetaData = DefaultKeyMetaData;
                else if (!DefaultKeyMetaData.IsNullOrEmpty())
                    keyMetaData = keyMetaData.ConvertToWriteable().MergeWith(DefaultKeyMetaData, NamedValueMergeBehavior.AddNewItems);

                bool ensureExists = (keyMetaData ?? NamedValueSet.Empty)["EnsureExists"].VC.GetValue<bool>(false);
                ValueContainer defaultValue = (ensureExists ? GetDefaultValue(itemInfo) : ValueContainer.Empty);

                IConfigKeyAccess keyAccess = ConfigInstance.GetConfigKeyAccess(new ConfigKeyAccessSpec(fullKeyName, customAccessFlags) { MetaData = keyMetaData }, defaultValue);

                KeySetupInfo keySetupInfo = new KeySetupInfo()
                {
                    KeyAccess = keyAccess,
                    ItemInfo = itemInfo,
                    FullItemName = fullKeyName,
                };

                keySetupInfo.UpdateMemberFromKeyAccessAction = GenerateUpdateMemberFromKeyAccessAction(keySetupInfo);

                if (!keyAccess.IsUsable)
                {
                    if (!itemAttribute.SilenceIssues)
                    {
                        if (keyAccess.Flags.IsOptional)
                        {
                            ValueNoteEmitter.Emit("Optional Member/Key '{0}'/'{1}' is not usable: {2}", memberName, keyAccess.Key, keyAccess.ResultCode);
                        }
                        else
                        {
                            SetupIssueEmitter.Emit("Member/Key '{0}'/'{1}' is not usable: {2}", memberName, keyAccess.Key, keyAccess.ResultCode);
                            anySetupIssues = true;
                        }
                    }
                    continue;
                }
                else if (keySetupInfo.UpdateMemberFromKeyAccessAction == null)
                {
                    if (!itemAttribute.SilenceIssues)
                    {
                        SetupIssueEmitter.Emit("Member/Key '{0}'/'{1}' is not usable: no valid accessor delegate could be generated for its ValueSet type:'{3}'", memberName, fullKeyName, itemInfo.ItemType, tConfigValueSetTypeStr);
                        anySetupIssues = true;
                    }
                    continue;
                }

                keySetupInfoArray[idx] = keySetupInfo;
            }

            // next link events - this initializes the Configuration Service Changed event callback handler and connects it to the Configuration Service.
            IBasicNotificationList notificationList = ConfigInstance.ChangeNotificationList;
            notificationList.AddItem(updateNotificationList);
            AddExplicitDisposeAction(() => notificationList.RemoveItem(updateNotificationList));

            if (!anySetupIssues)
                Update(true, SetupIssueEmitter, ValueNoteEmitter);
            else
                Update(true, ValueNoteEmitter, ValueNoteEmitter);

            return this;
        }

        /// <summary>
        /// This property will be true after the user commits an update to one or more dynamic configuration config key's values that this adapter is tracking.
        /// </summary>
        public bool IsUpdateNeeded
        {
            get 
            {
                return (isUpdateNeeded || (ConfigInstance != null && !lastUpdateConfigChangeSeqNums.Equals(ConfigInstance.SeqNums))); 
            }
            internal set 
            { 
                isUpdateNeeded = value; 
            }
        }

        private ConfigSubscriptionSeqNums lastUpdateConfigChangeSeqNums;
        private volatile bool isUpdateNeeded = false;

        /// <summary>
        /// This property allows the caller to connect one or more <see cref="MosaicLib.Utils.INotifyable"/> objects to receive Notify calls whenever
        /// one or more dynamic configuration config key's values have been distributed that this adapter is tracking.
        /// </summary>
        public IBasicNotificationList UpdateNotificationList { get { return updateNotificationList; } }

        private BasicNotificationList updateNotificationList = new BasicNotificationList();

        /// <summary>
        /// Must be called by the client to apply any pending updates to dynamic config key's values that this adapter is tracking.  
        /// The relevant ValueSet item values will be updated by this method based on the dynamic config key's value updates that have been received since 
        /// the last call to Setup or Update.
        /// </summary>
        public ConfigValueSetAdapter<TConfigValueSet> Update()
        {
            return Update(false, UpdateIssueEmitter, ValueNoteEmitter);
        }

        private ConfigValueSetAdapter<TConfigValueSet> Update(bool isFirstUpdate, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueNoteEmitter)
        {
            if (ValueSet == null)
                throw new System.NullReferenceException("ValueSet property must be non-null before Update can be called");

            if (!IsUpdateNeeded && !isFirstUpdate)
                return this;

            IsUpdateNeeded = false;
            lastUpdateConfigChangeSeqNums = ConfigInstance.SeqNums;

            foreach (var keySetupInfo in keySetupInfoArray)
            {
                if (keySetupInfo == null)
                    continue;

                IConfigKeyAccess keyAccess = keySetupInfo.KeyAccess;

                bool isReadOnlyOnceKey = (keySetupInfo.ItemAttribute.ReadOnlyOnce || keySetupInfo.KeyAccess.ValueIsFixed);
                bool doRead = (!isReadOnlyOnceKey || isFirstUpdate);        // we need to read even ReadOnlyOnce keys at least once!

                if (doRead)
                {
                    keyAccess.UpdateValue();

                    if (keySetupInfo.UpdateMemberFromKeyAccessAction != null)
                        keySetupInfo.UpdateMemberFromKeyAccessAction(ValueSet, (!keySetupInfo.ItemAttribute.SilenceIssues ? updateIssueEmitter : Logging.NullEmitter), valueNoteEmitter);
                    else if (!keyAccess.Flags.SilenceLogging)
                        ValueNoteEmitter.Emit("Member/Key '{0}'/'{1}' in type '{2}' was not changed: there is no member update delegate", keySetupInfo.FullItemName, keyAccess.Key, tConfigValueSetTypeStr);
                }
            }

            return this;
        }

        /// <summary>
        /// Gives the caller access to the set of IConfigKeyAccess items that this adapter has generated and uses to support its operation
        /// </summary>
        public IConfigKeyAccess[] ICKAArray
        {
            get 
            { 
                return keySetupInfoArray.Where(item => item != null).Select(item => item.KeyAccess).ToArray(); 
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Simple method that uses basic reflection to obtain the desird Property/Field contents enclosed in a ValueContainer.
        /// </summary>
        ValueContainer GetDefaultValue(ItemInfo<ConfigItemAttribute> itemInfo)
        {
            if (ValueSet != null)
            {
                if (itemInfo.IsProperty)
                    return new ValueContainer(itemInfo.PropertyInfo.GetValue(ValueSet, emptyObjectArray));
                else if (itemInfo.IsField)
                    return new ValueContainer(itemInfo.FieldInfo.GetValue(ValueSet));
            }

            return ValueContainer.Empty;
        }

        private static readonly object[] emptyObjectArray = new object[0];

        protected string GenerateFullKeyName(ItemInfo<Attributes.ConfigItemAttribute> itemInfo, string[] baseNames)
        {
            return itemInfo.GenerateFullName(baseNames);
        }

        Action<TConfigValueSet, Logging.IMesgEmitter, Logging.IMesgEmitter> GenerateUpdateMemberFromKeyAccessAction(KeySetupInfo keySetupInfo)
        {
            ItemInfo<Attributes.ConfigItemAttribute> itemInfo = keySetupInfo.ItemInfo;

            Action<TConfigValueSet, Logging.IMesgEmitter, Logging.IMesgEmitter> method = null;
            Action<TConfigValueSet, IConfigKeyAccess> innerBoundSetter = null;

            // we only support the legal data types for config key's values here
            if (itemInfo.ItemType == typeof(bool))
            {
                Func<IConfigKeyAccess, bool, bool> ikaGetter = (ika, defaultValue) => ika.GetValue<bool>(defaultValue);
                Action<TConfigValueSet, bool> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, bool>(itemInfo);
                Func<TConfigValueSet, bool> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, bool>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(sbyte))
            {
                Func<IConfigKeyAccess, sbyte, sbyte> ikaGetter = (ika, defaultValue) => ika.GetValue<sbyte>(defaultValue);
                Action<TConfigValueSet, sbyte> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, sbyte>(itemInfo);
                Func<TConfigValueSet, sbyte> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, sbyte>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(short))
            {
                Func<IConfigKeyAccess, short, short> ikaGetter = (ika, defaultValue) => ika.GetValue<short>(defaultValue);
                Action<TConfigValueSet, short> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, short>(itemInfo);
                Func<TConfigValueSet, short> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, short>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(int))
            {
                Func<IConfigKeyAccess, int, int> ikaGetter = (ika, defaultValue) => ika.GetValue<int>(defaultValue);
                Action<TConfigValueSet, int> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, int>(itemInfo);
                Func<TConfigValueSet, int> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, int>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(long))
            {
                Func<IConfigKeyAccess, long, long> ikaGetter = (ika, defaultValue) => ika.GetValue<long>(defaultValue);
                Action<TConfigValueSet, long> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, long>(itemInfo);
                Func<TConfigValueSet, long> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, long>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(byte))
            {
                Func<IConfigKeyAccess, byte, byte> ikaGetter = (ika, defaultValue) => ika.GetValue<byte>(defaultValue);
                Action<TConfigValueSet, byte> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, byte>(itemInfo);
                Func<TConfigValueSet, byte> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, byte>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(ushort))
            {
                Func<IConfigKeyAccess, ushort, ushort> ikaGetter = (ika, defaultValue) => ika.GetValue<ushort>(defaultValue);
                Action<TConfigValueSet, ushort> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, ushort>(itemInfo);
                Func<TConfigValueSet, ushort> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, ushort>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(uint))
            {
                Func<IConfigKeyAccess, uint, uint> ikaGetter = (ika, defaultValue) => ika.GetValue<uint>(defaultValue);
                Action<TConfigValueSet, uint> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, uint>(itemInfo);
                Func<TConfigValueSet, uint> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, uint>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(ulong))
            {
                Func<IConfigKeyAccess, ulong, ulong> ikaGetter = (ika, defaultValue) => ika.GetValue<ulong>(defaultValue);
                Action<TConfigValueSet, ulong> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, ulong>(itemInfo);
                Func<TConfigValueSet, ulong> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, ulong>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(float))
            {
                Func<IConfigKeyAccess, float, float> ikaGetter = (ika, defaultValue) => ika.GetValue<float>(defaultValue);
                Action<TConfigValueSet, float> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, float>(itemInfo);
                Func<TConfigValueSet, float> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, float>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(double))
            {
                Func<IConfigKeyAccess, double, double> ikaGetter = (ika, defaultValue) => ika.GetValue<double>(defaultValue);
                Action<TConfigValueSet, double> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, double>(itemInfo);
                Func<TConfigValueSet, double> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, double>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(string))
            {
                Func<IConfigKeyAccess, string, string> ikaGetter = (ika, defaultValue) => (ika.HasValue ? ika.ValueAsString : defaultValue);
                Action<TConfigValueSet, string> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, string>(itemInfo);
                Func<TConfigValueSet, string> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, string>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(bool ?))
            {
                Func<IConfigKeyAccess, bool ?, bool ?> ikaGetter = (ika, defaultValue) => ika.GetValue<bool ?>(defaultValue);
                Action<TConfigValueSet, bool ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, bool ?>(itemInfo);
                Func<TConfigValueSet, bool ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, bool ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(sbyte ?))
            {
                Func<IConfigKeyAccess, sbyte ?, sbyte ?> ikaGetter = (ika, defaultValue) => ika.GetValue<sbyte ?>(defaultValue);
                Action<TConfigValueSet, sbyte ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, sbyte ?>(itemInfo);
                Func<TConfigValueSet, sbyte ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, sbyte ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(short ?))
            {
                Func<IConfigKeyAccess, short ?, short ?> ikaGetter = (ika, defaultValue) => ika.GetValue<short ?>(defaultValue);
                Action<TConfigValueSet, short ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, short ?>(itemInfo);
                Func<TConfigValueSet, short ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, short ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(int ?))
            {
                Func<IConfigKeyAccess, int ?, int ?> ikaGetter = (ika, defaultValue) => ika.GetValue<int ?>(defaultValue);
                Action<TConfigValueSet, int ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, int ?>(itemInfo);
                Func<TConfigValueSet, int ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, int ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(long ?))
            {
                Func<IConfigKeyAccess, long ?, long ?> ikaGetter = (ika, defaultValue) => ika.GetValue<long ?>(defaultValue);
                Action<TConfigValueSet, long ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, long ?>(itemInfo);
                Func<TConfigValueSet, long ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, long ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(byte ?))
            {
                Func<IConfigKeyAccess, byte ?, byte ?> ikaGetter = (ika, defaultValue) => ika.GetValue<byte ?>(defaultValue);
                Action<TConfigValueSet, byte ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, byte ?>(itemInfo);
                Func<TConfigValueSet, byte ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, byte ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(ushort ?))
            {
                Func<IConfigKeyAccess, ushort ?, ushort ?> ikaGetter = (ika, defaultValue) => ika.GetValue<ushort ?>(defaultValue);
                Action<TConfigValueSet, ushort ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, ushort ?>(itemInfo);
                Func<TConfigValueSet, ushort ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, ushort ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(uint ?))
            {
                Func<IConfigKeyAccess, uint ?, uint ?> ikaGetter = (ika, defaultValue) => ika.GetValue<uint ?>(defaultValue);
                Action<TConfigValueSet, uint ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, uint ?>(itemInfo);
                Func<TConfigValueSet, uint ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, uint ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(ulong ?))
            {
                Func<IConfigKeyAccess, ulong ?, ulong ?> ikaGetter = (ika, defaultValue) => ika.GetValue<ulong ?>(defaultValue);
                Action<TConfigValueSet, ulong ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, ulong ?>(itemInfo);
                Func<TConfigValueSet, ulong ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, ulong ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(float ?))
            {
                Func<IConfigKeyAccess, float ?, float ?> ikaGetter = (ika, defaultValue) => ika.GetValue<float ?>(defaultValue);
                Action<TConfigValueSet, float ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, float ?>(itemInfo);
                Func<TConfigValueSet, float ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, float ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(double ?))
            {
                Func<IConfigKeyAccess, double ?, double ?> ikaGetter = (ika, defaultValue) => ika.GetValue<double ?>(defaultValue);
                Action<TConfigValueSet, double ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, double ?>(itemInfo);
                Func<TConfigValueSet, double ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, double ?>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType == typeof(ValueContainer))
            {
                Func<IConfigKeyAccess, ValueContainer, ValueContainer> ikaGetter = (ika, defaultValue) => ika.ValueContainer;
                Action<TConfigValueSet, ValueContainer> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, ValueContainer>(itemInfo);
                Func<TConfigValueSet, ValueContainer> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, ValueContainer>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else if (itemInfo.ItemType.IsEnum)
            {
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika)
                {
                    object defaultValue = null;
                    if (itemInfo.IsProperty)
                        defaultValue = itemInfo.PropertyInfo.GetValue(valueSetObj, null);
                    else
                        defaultValue = itemInfo.FieldInfo.GetValue(valueSetObj);

                    string valueAsStr = (ika.HasValue ? ika.ValueAsString : (defaultValue ?? String.Empty).ToString()) ?? String.Empty;

                    // this is less efficient but will work
                    object castedValue = null;
                    try
                    {
                        castedValue = System.Enum.Parse(itemInfo.ItemType, valueAsStr);
                    }
                    catch (System.Exception ex1)
                    {
                        try
                        {
                            castedValue = System.Enum.ToObject(itemInfo.ItemType, 0);

                            ika.ResultCode = Fcns.CheckedFormat("Attempt to cast value '{0}' to type '{1}' failed: unable to construct default value for type [{2}]", valueAsStr, itemInfo.ItemType, ex1);
                        }
                        catch (System.Exception ex2)
                        {
                            ika.ResultCode = Fcns.CheckedFormat("Attempt to cast value '{0}' to type '{1}' failed: {2}", valueAsStr, itemInfo.ItemType, ex2);
                            castedValue = null;
                        }
                    }

                    if (itemInfo.IsProperty)
                        itemInfo.PropertyInfo.SetValue(valueSetObj, castedValue, null);
                    else
                        itemInfo.FieldInfo.SetValue(valueSetObj, castedValue);
                };
            }
            else if (itemInfo.ItemType == typeof(Logging.LogGate))
            {
                Func<IConfigKeyAccess, Logging.LogGate, Logging.LogGate> ikaGetter
                    = (ika, defaultValue)
                        =>
                        {
                            Logging.LogGate gate = defaultValue;
                            gate.TryParse(ika.GetValue<string>(defaultValue.ToString()));
                            return gate;
                        };
                Action<TConfigValueSet, Logging.LogGate> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TConfigValueSet, Logging.LogGate>(itemInfo);
                Func<TConfigValueSet, Logging.LogGate> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TConfigValueSet, Logging.LogGate>(itemInfo);
                innerBoundSetter = delegate(TConfigValueSet valueSetObj, IConfigKeyAccess ika) { pfSetter(valueSetObj, ikaGetter(ika, pfGetter(valueSetObj))); };
            }
            else
            {
                // the item type is not a supported data type.
                return null;
            }

            if (innerBoundSetter != null)
            {
                method = delegate(TConfigValueSet valueSetObj, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter)
                {
                    IConfigKeyAccess keyAccess = keySetupInfo.KeyAccess;

                    try
                    {
                        keyAccess.ResultCode = String.Empty;
                        keyAccess.UpdateValue();

                        string valueAsString = keyAccess.ValueAsString;
                        innerBoundSetter(valueSetObj, keyAccess);

                        if (keyAccess.Flags.SilenceLogging)
                        { }
                        else if (keyAccess.ResultCode.IsNullOrEmpty())
                        {
                            valueUpdateEmitter.Emit("Updated Member/Key '{0}'/'{1}' with new value '{2}' [type:'{3}']", keySetupInfo.MemberName, keyAccess.Key, valueAsString, tConfigValueSetTypeStr);
                        }
                        else if (!keyAccess.Flags.SilenceIssues)
                        {
                            updateIssueEmitter.Emit("Updated failed on Member/Key '{0}'/'{1}', value '{2}', type '{3}', error:'{4}'", keySetupInfo.MemberName, keyAccess.Key, valueAsString, tConfigValueSetTypeStr, keyAccess.ResultCode);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        if (!keyAccess.Flags.SilenceIssues)
                            updateIssueEmitter.Emit("Member/Key '{0}'/'{1}' in type '{2}' could not be set: {3}", keySetupInfo.MemberName, keyAccess.Key, tConfigValueSetTypeStr, ex);
                    }
                };
            }

            return method;
        }

        #endregion

        #region private fields, properties

        Type tConfigValueSetType = typeof(TConfigValueSet);
        string tConfigValueSetTypeStr = typeof(TConfigValueSet).Name;

        List<ItemInfo<Attributes.ConfigItemAttribute>> configItemInfoList = null;       // gets built by the AnnotatedClassItemAccessHelper.
        int NumItems { get; set; }

        /// <summary>
        /// Internal class used to capture the key specific setup information for a given annotated property in the ValueSet.
        /// </summary>
        private class KeySetupInfo
        {
            /// <summary>
            /// The key's corresponding config key access object.
            /// </summary>
            public IConfigKeyAccess KeyAccess { get; set; }

            /// <summary>
            /// Retains access to the ItemInfo for the corresponding item in the value set
            /// </summary>
            public ItemInfo<Attributes.ConfigItemAttribute> ItemInfo { get; set; }

            /// <summary>
            /// Returns the ItemAttribute from the contained ItemInfo
            /// </summary>
            public Attributes.ConfigItemAttribute ItemAttribute { get { return ItemInfo.ItemAttribute; } }

            /// <summary>
            /// Returns the item full name derived from the ItemAttribute and ItemInfo.
            /// </summary>
            public string FullItemName { get; set; }

            /// <summary>
            /// Returns the item member name.
            /// </summary>
            public string MemberName { get { return (ItemInfo.MemberInfo.Name); } }

            /// <summary>delegate that is used to set a specific member's value from a given config key's value object's stored value.</summary>
            /// <remarks>this item will be null for static items and for items that failed to be setup correctly.</remarks>
            public Action<TConfigValueSet, Logging.IMesgEmitter, Logging.IMesgEmitter> UpdateMemberFromKeyAccessAction { get; set; }
        }

        /// <remarks>Non-null elements in this array correspond to fully vetted writeable value set items.</remarks>
        KeySetupInfo[] keySetupInfoArray = null;

        #endregion

        #region message emitter glue

        private Logging.IMesgEmitter setupIssueEmitter = null, updateIssueEmitter = null, valueNoteEmitter = null;
        private Logging.IMesgEmitter FixupEmitterRef(ref Logging.IMesgEmitter emitterRef)
        {
            if (emitterRef == null)
                emitterRef = Logging.NullEmitter;
            return emitterRef;
        }

        #endregion
    }

    #endregion

    //-------------------------------------------------------------------
}