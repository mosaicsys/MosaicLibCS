//-------------------------------------------------------------------
/*! @file ConfigProviers.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2015 Mosaic Systems Inc.
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
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Collections;
using Microsoft.Win32;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Persist;
using MosaicLib.Utils;
using MosaicLib.Utils.StringMatching;

namespace MosaicLib.Modular.Config
{
    #region ConfigKeyProviderBase

    /// <summary>
    /// Base IConfigKeyProvider implementation class.
    /// </summary>
    public abstract class ConfigKeyProviderBase : DisposableBase, IConfigKeyProvider, IConfigKeyProviderInfo
    {
        /// <summary>
        /// Constructor.  Requires name.  Other properties shall be initialized using property initializer list.
        /// Provider generated messages will use the string "Config.Provider.{name}" where {name} is the name given here.
        /// </summary>
        public ConfigKeyProviderBase(string name) 
            : this(name, null)
        {}
        /// <summary>
        /// Constructor.  Requires name.  Other properties shall be initialized using property initializer list.
        /// Provider generated messages will use the string "Config.Provider.{name}" where {name} is the name given here.
        /// </summary>
        public ConfigKeyProviderBase(string name, INamedValueSet metaData) 
        {
            Logger = new Logging.Logger("Config.Provider." + name);
            Name = name;

            ProviderMetaData = new NamedValueSet() { { "Provider", Name } }.MergeWith(metaData).MakeReadOnly();
        }

        /// <summary>
        /// This is the Logger instance that the provider will use.
        /// </summary>
        public Logging.Logger Logger { get; protected set; }


        #region IConfigKeyProviderInfo

        /// <summary>
        /// Gives the name of the provider
        /// </summary>
        public string Name { get; private set; }

        /// <summary>Indicates the Basic Flags values that will be common to all keys that are served by this provider.  Currently used to indicate if all keys are Fixed.</summary>
        public ConfigKeyProviderFlags BaseFlags { get; protected set; }

        /// <summary>
        /// This property is used by the provider to prefix all of the partial key names that it creates with a common prefix.  When this string is not empty
        /// it will effecitevely shift all of the keys that are served by this provider to live under the partial path specified as the common prefix.
        /// The provider must account for this when searching for keys using its public methods and all key strings returned by this provider must start with this prefix.
        /// </summary>
        public string KeyPrefix { get { return keyPrefix ?? String.Empty; } protected set { keyPrefix = value; } }
        private string keyPrefix;

        /// <summary>Gives the default INamedValueSet that is put into each config key provided by this provider.</summary>
        public INamedValueSet ProviderMetaData { get { return providerMetaData; } private set { providerMetaData = value.ConvertToReadOnly(); } }
        private INamedValueSet providerMetaData;

        #endregion

        #region IConfigKeyProvider (base class methods: abstract and default versions)

        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given MatchRuleSet and searchPredicate values.
        /// MatchRuleSet is used to filter the keys based on their names while searchPredicate may be used to filter the returned keys based on their MetaData contents
        /// null matchRulesSet and/or searchPredicate disables filtering based on that parameter.  If both are null then all known keys will be returned.
        /// The array returned is contains the union of the corresponding sorted arrays returned by each provider.
        /// </summary>
        public abstract string[] SearchForKeys(MatchRuleSet matchRuleSet, ConfigKeyFilterPredicate searchPredicate = null);

        /// <summary>
        /// Attempts to find information about the given keyAccessSpec and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// returns null if the key was not found or is not supported by this provider.
        /// if ensureExists is true and defaultValue is non-empty and non-null and this provider supports 
        /// </summary>
        /// <remarks>
        /// At the IConfigKeyProvider level, this method is typically only called once per fixed or read-only-once key as the config instance may keep prior key access objects and simply return clones of them.
        /// </remarks>
        public abstract IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec, bool ensureExists = false, ValueContainer defaultValue = new ValueContainer());

        /// <summary>
        /// This method allows the caller to update a set of values of the indicated keys to contain the corresponding valueAsString values.
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known
        /// values for this key.  
        /// This method returns the empty string on success or an error code on failure.
        /// </summary>
        /// <remarks>At the IConfigKeyProvider level this method will be invoked for each sub-set of keys that share the same original provider</remarks>
        public virtual string SetValues(KeyValuePair<IConfigKeyAccess, ValueContainer>[] keyAccessAndValuesPairArray, string commentStr)
        {
            if (BaseFlags.IsFixed)
                return Fcns.CheckedFormat("Invalid: Provider '{0}' does not support Setting key values", Name);
            else
                return Fcns.CheckedFormat("Invalid: Provider '{0}' did not implement the SetValues handler", Name);
        }

        #endregion

        /// <summary>
        /// Override ToString for logging and debugging
        /// </summary>
        public override string ToString()
        {
            return "'{0}' {1} {2}".CheckedFormat(Name, ProviderMetaData.ToString(false, true), BaseFlags);
        }
    }

    #endregion

    #region DictionaryConfigKeyProvider and derived types: MainArgsConfigKeyProvider, EnvVarsConfigKeyProvider, AppConfigConfigKeyProvider, IncludeFilesConfigKeyProvider

    /// <summary>
    /// This class provides an implementation for ConfigKeyProviders that can be implemented using a dictionary.
    /// </summary>
    public class DictionaryConfigKeyProvider : ConfigKeyProviderBase, IEnumerable
    {
        /// <summary>Constructor</summary>
        public DictionaryConfigKeyProvider(string name, bool isFixed = true, INamedValueSet metaData = null, bool keysMayBeAddedUsingEnsureExistsOption = false) 
            : base(name, metaData)
        {
            BaseFlags = new ConfigKeyProviderFlags() { IsFixed = isFixed, KeysMayBeAddedUsingEnsureExistsOption = keysMayBeAddedUsingEnsureExistsOption };
        }

        #region AddRange and Add methods for KVP, explicit Name & Value 

        /// <summary>
        /// Dictionary construction helper method.  Adds the given set of name/value pairs to the dictionary, prefixing each given name with the CommonKeyPrefix.
        /// <para/>Method supports call chaining.
        /// </summary>
        public DictionaryConfigKeyProvider AddRange(IEnumerable<KeyValuePair<string, ValueContainer>> nameValuePairSource)
        {
            ConfigKeyAccessFlags flags = new ConfigKeyAccessFlags();

            return AddRange(nameValuePairSource.Select((kvp) => new ConfigKeyAccessImpl(KeyPrefix + kvp.Key, flags, this) { ValueContainer = kvp.Value }));
        }

        /// <summary>
        /// Dictionary construction helper method.  Adds the given name/vc ValueContainer pair to the dictionary, prefixing the name with the CommonKeyPrefix.
        /// <para/>Method supports call chaining.
        /// </summary>
        public DictionaryConfigKeyProvider Add(string name, ValueContainer vc = default(ValueContainer))
        {
            ConfigKeyAccessFlags flags = new ConfigKeyAccessFlags();

            return Add(new ConfigKeyAccessImpl(KeyPrefix + name, flags, this) { ValueContainer = vc });
        }

        /// <summary>
        /// Dictionary construction helper method.  Adds the given name/value pair to the dictionary, prefixing the name with the CommonKeyPrefix.
        /// <para/>Method supports call chaining.
        /// </summary>
        public DictionaryConfigKeyProvider Add(string name, object value)
        {
            ConfigKeyAccessFlags flags = new ConfigKeyAccessFlags();

            return Add(new ConfigKeyAccessImpl(KeyPrefix + name, flags, this) { ValueContainer = new ValueContainer(value) });
        }

        #endregion

        #region protected AddRange and Add methods for ConfigKeyAccessImpl objects.

        /// <summary>
        /// Implementation method used to add a set of generated ConfigKeyAccessImpl objects to the dictionary.  Used by AddRange and/or derived types.
        /// </summary>
        protected DictionaryConfigKeyProvider AddRange(IEnumerable<ConfigKeyAccessImpl> ckaiSource)
        {
            foreach (var ckai in ckaiSource)
            {
                Add(ckai);
            }

            return this;
        }

        /// <summary>
        /// Implementation method used to add a generated ConfigKeyAccessImpl objects to the dictionary.  Used by AddRange and/or derived types.
        /// </summary>
        protected DictionaryConfigKeyProvider Add(ConfigKeyAccessImpl ckai)
        {
            DictionaryKeyItem dItem = new DictionaryKeyItem()
            {
                key = ckai.Key,
                initialContainedValue = ckai.ValueContainer,
                ckai = ckai,
                keyMetaData = ckai.MetaData,
            };

            if (ckai.HasValue)
                ckai.ValueSeqNum = ckai.CurrentSeqNum = dItem.seqNumGen = dItem.seqNumGen.IncrementSkipZero();

            keyItemDictionary[ckai.Key] = dItem;

            return this;
        }

        #endregion

        /// <summary>
        /// Gives the internal storage object that is used for the keyItemDictionary.
        /// </summary>
        protected class DictionaryKeyItem
        {
            /// <summary>The key name</summary>
            public string key;
            /// <summary>The initial value from construction time.</summary>
            public ValueContainer initialContainedValue;
            /// <summary>This value is used to generate sequence numbers for the corresponding config key value.</summary>
            public int seqNumGen;
            /// <summary>The provider reference ConfigKeyAccessImpl object used for this key</summary>
            public ConfigKeyAccessImpl ckai;
            /// <summary>key's MetaData from the ckai</summary>
            public INamedValueSet keyMetaData;
            /// <summary>The last comment string given to this key if the dictionary is not fixed.</summary>
            public string comment;
            /// <summary>Returns the current ConfigKeyAccessImpl's ValueContainer value, or ValueContainer.Empty if the ckai is null</summary>
            public ValueContainer CurrentValue { get { return (ckai != null ? ckai.ValueContainer : ValueContainer.Empty); } }

            public override string ToString()
            {
                return "DCKP.item key:{0} value:{1} [+]".CheckedFormat(key, CurrentValue);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return keyItemDictionary.GetEnumerator();
        }

        /// <summary>
        /// The dictionary.  Is actually a key/item dictionary where the Item contains all of the per key information that the class needs to track, and have access to.
        /// </summary>
        protected Dictionary<string, DictionaryKeyItem> keyItemDictionary = new Dictionary<string, DictionaryKeyItem>();

        private static readonly string[] emptyStringArray = new string[0];
        private static readonly DictionaryKeyItem emptyItem = new DictionaryKeyItem() { key = String.Empty };

        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given MatchRuleSet and searchPredicate values.
        /// MatchRuleSet is used to filter the keys based on their names while searchPredicate may be used to filter the returned keys based on their MetaData contents
        /// null matchRulesSet and/or searchPredicate disables filtering based on that parameter.  If both are null then all known keys will be returned.
        /// The array returned is contains the union of the corresponding sorted arrays returned by each provider.
        /// </summary>
        public override string[] SearchForKeys(MatchRuleSet matchRuleSet = null, ConfigKeyFilterPredicate searchPredicate = null)
        {
            bool searchPredicateIsNull = (searchPredicate == null);
            bool matchRuleSetIsNullOrAny = matchRuleSet.IsNullOrAny();

            if (matchRuleSetIsNullOrAny && searchPredicateIsNull)
                return keyItemDictionary.Keys.ToArray();
            else if (searchPredicateIsNull)
                return keyItemDictionary.Keys.Where((key) => matchRuleSet.MatchesAny(key)).ToArray();
            else if (matchRuleSetIsNullOrAny)
                return keyItemDictionary.Values.Where((item) => searchPredicate(item.key, item.keyMetaData, item.CurrentValue)).Select(item => item.key).ToArray();
            else
                return keyItemDictionary.Values.Where((item) => (matchRuleSet.MatchesAny(item.key, true) && searchPredicate(item.key, item.keyMetaData, item.CurrentValue))).Select(item => item.key).ToArray();
        }

        /// <summary>
        /// Attempts to find information about the given keyAccessSpec and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// returns null if the key was not found or is not supported by this provider.
        /// if ensureExists is true and defaultValue is non-empty and non-null and this provider supports 
        /// </summary>
        public override IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec, bool ensureExists = false, ValueContainer initialValue = new ValueContainer())
        {
            DictionaryKeyItem item = null;
            string key = ((keyAccessSpec != null) ? keyAccessSpec.Key : null) ?? String.Empty;

            if (!keyItemDictionary.TryGetValue(key, out item))
            {
                if (ensureExists && !initialValue.IsEmpty && BaseFlags.KeysMayBeAddedUsingEnsureExistsOption)
                {
                    string ec = EnsureItemExists(keyAccessSpec, initialValue, ref item, "Key created by request using EnsureExists");

                    if (!ec.IsNullOrEmpty())
                        return new ConfigKeyAccessImpl(keyAccessSpec, this) { ResultCode = ec, ProviderFlags = new ConfigKeyProviderFlags(item.ckai.ProviderFlags) { KeyWasNotFound = true } };

                    HandleSetValuesChangedContents();

                    return item.ckai;
                }
            }

            return (item ?? emptyItem).ckai;
        }

        /// <summary>
        /// This method allows the caller to update a set of values of the indicated keys to contain the corresponding valueAsString values.
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known
        /// values for this key.  
        /// This method returns the empty string on success or an error code on failure.
        /// </summary>
        /// <remarks>At the IConfigKeyProvider level this method will be invoked for each sub-set of keys that share the same original provider</remarks>
        public override string SetValues(KeyValuePair<IConfigKeyAccess, ValueContainer>[] keyAccessAndValuesPairArray, string commentStr)
        {
            if (BaseFlags.IsFixed)
                return Fcns.CheckedFormat("Invalid: Provider '{0}' does not support Setting key values", Name);

            int numChangedItems = 0;
            string firstError = null;

            foreach (var kvp in keyAccessAndValuesPairArray)
            {
                DictionaryKeyItem item = null;
                IConfigKeyAccess kvpIcka = kvp.Key;
                string key = (kvpIcka != null ? kvpIcka.Key : null);

                if (key == null)
                    firstError = firstError ?? "Internal: Encountered null key spec";
                else if (!key.StartsWith(KeyPrefix))
                    firstError = firstError ?? Fcns.CheckedFormat("Internal: key:'{0}' is not valid for use with provider:'{1}', required key prefix:'{2}'", kvpIcka.Key, Name, KeyPrefix);
                else if (!keyItemDictionary.TryGetValue(key ?? String.Empty, out item) || item == null || item.ckai == null)
                {
                    if (BaseFlags.KeysMayBeAddedUsingEnsureExistsOption)
                    {
                        string ec = EnsureItemExists(kvpIcka, kvp.Value, ref item, commentStr);

                        if (ec.IsNullOrEmpty())
                        {
                            numChangedItems++;
                        }
                        else
                        {
                            firstError = firstError ?? "cannot add key:'{0}' value:'{1}' to provider '{2}': {3}".CheckedFormat(key, kvp.Value, Name, ec);
                        }
                    }
                    else
                    {
                        string ec = "cannot add key:'{0}' value:'{1}' to provider '{2}': provider flags '{3}' do not support addition of keys when using SetValue".CheckedFormat(key, kvp.Value, Name, BaseFlags);

                        firstError = firstError ?? ec;
                    }
                }
                else
                {
                    if (item.ckai.ProviderFlags.IsFixed)
                        firstError = firstError ?? Fcns.CheckedFormat("Internal: key:'{0}' is fixed in provider:'{1}'", kvpIcka.Key, Name);
                    else
                    {
                        int entryValueSeqNum = item.ckai.ValueSeqNum;
                        ValueContainer entryValue = item.ckai.ValueContainer;

                        item.ckai.ValueContainer = kvp.Value;
                        item.ckai.CurrentSeqNum = item.ckai.ValueSeqNum = item.seqNumGen = item.seqNumGen.IncrementSkipZero();
                        item.comment = commentStr;

                        numChangedItems++;

                        if (entryValueSeqNum == 0)
                            Logger.Debug.Emit("key:'{0}' value changed to {1} seq:{2} [from initial value:{3}]", key, kvp.Value, item.ckai.ValueSeqNum, item.initialContainedValue);
                        else
                            Logger.Debug.Emit("key:'{0}' value changed to {1} seq:{2} [from:{3} seq:{4} initial:{5}]", key, kvp.Value, item.ckai.ValueSeqNum, entryValue, entryValueSeqNum, item.initialContainedValue);
                    }
                }
            }

            if (numChangedItems > 0)
            {
                string changeEC = HandleSetValuesChangedContents();
                firstError = firstError ?? changeEC;
            }

            return firstError ?? String.Empty;
        }

        private string EnsureItemExists(IConfigKeyAccessSpec icks, ValueContainer initialValue, ref DictionaryKeyItem item, string commentStr)
        {
            ConfigKeyAccessImpl ckai = new ConfigKeyAccessImpl(icks.Key, icks.Flags, this) { ValueContainer = initialValue };
            item = new DictionaryKeyItem() 
            { 
                key = icks.Key, 
                ckai = ckai, 
                keyMetaData = ckai.MetaData, 
                initialContainedValue = initialValue, 
                comment = commentStr 
            };

            string ec = VerifyAndNoteItemAdded(item);

            if (ec.IsNullOrEmpty())
                keyItemDictionary[icks.Key] = item;

            // we do not need to log here as the IConfig instance that is calling this method will handle such logging.

            return ec;
        }

        protected virtual string VerifyAndNoteItemAdded(DictionaryKeyItem item)
        {
            return string.Empty;
        }

        /// <summary>
        /// Method that may be overriden in derived objects to persist changes to the dictionary.  
        /// Default implementation returns String.Empty if the BaseFlags.IsFixed is false otherwise it returns an appropriate error message.
        /// </summary>
        protected virtual string HandleSetValuesChangedContents() 
        {
            if (!BaseFlags.IsFixed)
                return String.Empty; 
            else
                return "{0} only supports Fixed value keys".CheckedFormat(Name);
        }
    }

    /// <summary>
    /// Provides a Fixed DictionaryConfigKeyProvider obtained by extracting args from the given array of main args (generally as given to the program)
    /// which are of the form {key}={value}
    /// </summary>
    public class MainArgsConfigKeyProvider : DictionaryConfigKeyProvider
    {
        /// <summary>
        /// Constructor.  Harvests the set of given arguments for ones that match the pattern {key}={value},
        /// removes these from the set (array) and generates a dictionary of IConfigKeyAccess objects to represent
        /// the key/value pairs that were found.  These are added to the dictionary that is supported for this config key provider.
        /// </summary>
        public MainArgsConfigKeyProvider(string name, ref string[] mainArgs, string keyPrefix = "", INamedValueSet metaData = null)
            : base(name, isFixed: true, metaData: metaData)
        {
            KeyPrefix = keyPrefix = keyPrefix.MapNullToEmpty();

            if (mainArgs != null)
            {
                List<string> unusedArgsList = new List<string>() { };
                List<KeyValuePair<string, ValueContainer>> generatedKeyValuePairList = new List<KeyValuePair<string, ValueContainer>>(); 

                for (int idx = 0; idx < mainArgs.Length; idx++)
                {
                    string[] kvTokenArray = (mainArgs[idx] ?? String.Empty).Split(new char[] { '=' }, 2);
                    if (kvTokenArray == null || kvTokenArray.Length != 2)
                    {
                        unusedArgsList.Add(mainArgs[idx]);
                    }
                    else
                    {
                        string key = KeyPrefix + kvTokenArray[0];
                        string value = kvTokenArray[1];
                        generatedKeyValuePairList.Add(new KeyValuePair<string, ValueContainer>(key, new ValueContainer(value)));
                    }
                }

                if (unusedArgsList.Count != mainArgs.Length)
                {
                    mainArgs = unusedArgsList.ToArray();
                }

                AddRange(generatedKeyValuePairList.ToArray());
            }
        }

        /// <summary>
        /// Method always fails because this provider only supports fixed values.
        /// </summary>
        protected override string HandleSetValuesChangedContents()
        {
            return "{0} does not support saving values".CheckedFormat(Name);
        }
    }

    /// <summary>
    /// Provides a Fixed DictionaryConfigKeyProvider obtained by extracting all environment variables from the current process and creating a dictionary based fixed
    /// config key provider for these variables.
    /// </summary>
    public class EnvVarsConfigKeyProvider : DictionaryConfigKeyProvider
    {
        /// <summary>
        /// Constructor.  enumerates all current environment variables and adds them this provider's dictionary of key/value pairs using given keyPrefix value.
        /// </summary>
        public EnvVarsConfigKeyProvider(string name, string keyPrefix = "", INamedValueSet metaData = null)
            : base(name, isFixed: true, metaData: metaData)
        {
            KeyPrefix = keyPrefix = keyPrefix.MapNullToEmpty();


            List<KeyValuePair<string, ValueContainer>> generatedKeyValuePairList = new List<KeyValuePair<string, ValueContainer>>();

            IDictionary envVarIDictionary = System.Environment.GetEnvironmentVariables();
            foreach (object keyObject in envVarIDictionary.Keys)
            {
                object valueObject = envVarIDictionary[keyObject];
                string keyStr = keyObject as string;
                string valueStr = valueObject as string;

                if (keyStr != null && valueStr != null)
                {
                    generatedKeyValuePairList.Add(new KeyValuePair<string, ValueContainer>(KeyPrefix + keyStr, new ValueContainer(valueStr)));
                }
            }

            AddRange(generatedKeyValuePairList.ToArray());
        }
    }

    /// <summary>
    /// Provides a Fixed DictionaryConfigKeyProvider obtained by extracting all app.config values from the current process and creating a dictionary based fixed
    /// config key provider for these values.
    /// </summary>
    public class AppConfigConfigKeyProvider : DictionaryConfigKeyProvider
    {
        /// <summary>
        /// Constructor:  iterates through all of the app config AppSettings and adds the last value given to each app setting key to the dictionary of known config key value pairs.
        /// <para/>the selection to report the last value for given appSettings key/value pair appears to be a "feature" of the GetValues method.
        /// </summary>
        public AppConfigConfigKeyProvider(string name, string keyPrefix = "", INamedValueSet metaData = null)
            : base(name, isFixed: true, metaData: metaData)
        {
            KeyPrefix = keyPrefix = keyPrefix.MapNullToEmpty();

            Dictionary<string, ValueContainer> generatedKeyValueDictionary = new Dictionary<string, ValueContainer>();

            try
            {
                appSettings = System.Configuration.ConfigurationManager.AppSettings;

                foreach (string key in appSettings.AllKeys)
                {
                    if (!generatedKeyValueDictionary.ContainsKey(key))
                    {
                        string[] valuesArray = appSettings.GetValues(key) ?? emptyKeyValuesArray;
                        string firstValue = valuesArray.SafeAccess(0, String.Empty);

                        appSettingsDictionary[key] = valuesArray;

                        if (valuesArray.Length <= 1)
                            generatedKeyValueDictionary[key] = new ValueContainer(firstValue);
                        else
                            generatedKeyValueDictionary[key] = new ValueContainer(valuesArray);     // will produce IListOfString content
                    }
                }

                AddRange(generatedKeyValueDictionary.ToArray());
            }
            catch (System.Exception ex)
            {
                Logger.Error.Emit("Caught unexpected exception while attempting to process AppSettings into keys: {0}", ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

        private readonly string[] emptyKeyValuesArray = new string[0];

        /// <summary>
        /// Debugging helper field.  retains the orginal value returned from System.Configuration.ConfigurationManager.AppSettings.
        /// </summary>
        protected NameValueCollection appSettings;

        /// <summary>
        /// Debugging helper field.  ratains the dictionary of all app setting values indexed by app settings keys.  
        /// AppSettings allows a key to have multiple values which config keys do not support. 
        /// </summary>
        protected readonly Dictionary<string, string[]> appSettingsDictionary = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Provides a type of Fixed DictionaryConfigKeyProvider obtained by extracting all the current keys that are prefixed with
    /// the given searchPrefix key (typically Config.Include) and parsing them as file names or file names and prefix strings.
    /// Then reading each of the indicated include files and adding the contains keys using both the given common keyPrefix and any
    /// include specific prefix from the corresponding original include file key.
    /// </summary>
    public class IncludeFilesConfigKeyProvider : DictionaryConfigKeyProvider
    {
        /// <summary>
        /// Constructor: searches the existing config key space for keys that start with the search prefix, processes these keys as include file specifiers:
        /// either path names or |includeKeyPrefix|includeFilePath| type items.  Then loads each of the resulting files, validates the KeyItem entires contained there and
        /// then adds each of them to the dictionary of key/value pairs provided by this provider using the given keyPrefix combined with the IncludeKeyPrefix (as appropriate).
        /// </summary>
        public IncludeFilesConfigKeyProvider(string name, string searchPrefix, IConfig config, string keyPrefix = "", INamedValueSet metaData = null)
            : base(name, isFixed: true, metaData: metaData)
        {
            KeyPrefix = keyPrefix = keyPrefix.MapNullToEmpty();

            string[] includeKeysArray = config.SearchForKeys(new MatchRuleSet() { { MatchType.Prefix, searchPrefix } });

            List<string> includeKeyValuesList = new List<string>(includeKeysArray.Select((key) => config.GetConfigKeyAccess(key).ValueAsString));

            DataContractAsciiXmlAdapter<ConfigKeyFile> keyStoreLoader = new DataContractAsciiXmlAdapter<ConfigKeyFile>();

            Dictionary<string, ConfigKeyAccessImpl> includeFilesKeysDictionary = new Dictionary<string, ConfigKeyAccessImpl>();

            ConfigKeyAccessFlags flags = new ConfigKeyAccessFlags();

            foreach (string includeKeyValue in includeKeyValuesList)
            {
                string includePath = includeKeyValue;
                string includeKeyPrefix = String.Empty;

                if (includeKeyValue.StartsWith("|"))
                {
                    string[] strArray = includeKeyValue.Substring(1).Split(new char[] { '|' });
                    includeKeyPrefix = strArray.SafeAccess(0, String.Empty);
                    includePath = strArray.SafeAccess(1, String.Empty);
                }

                if (String.IsNullOrEmpty(includeKeyValue))
                    continue;

                try
                {
                    string fullPath = System.IO.Path.GetFullPath(includePath);
                    string fileContents = System.IO.File.ReadAllText(fullPath);
                    ConfigKeyFile ckf = keyStoreLoader.ReadObject(fileContents);

                    foreach (KeyItem keyItem in ckf.KeySet)
                    {
                        string fullKey = KeyPrefix + includeKeyPrefix + keyItem.Key;

                        if (!includeFilesKeysDictionary.ContainsKey(fullKey))
                            includeFilesKeysDictionary[fullKey] = new ConfigKeyAccessImpl(fullKey, flags, this) { ValueContainer = keyItem.VC };
                        else
                            Logger.Warning.Emit("Redundant value found for key '{0}' under path '{1}'", fullKey, includePath);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.Debug.Emit("Include file load from '{0}' w/Prefix:'{1}' failed: {2}", includePath, keyPrefix, ex);
                }
            }

            AddRange(includeFilesKeysDictionary.Values);
        }
    }

    #endregion

    #region  IniFileConfigKeyProvider

    /// <summary>
    /// Provides a type of DicationaryConfigKeyProvier obtained by loading a windows "INI" style text file which consists of sections of lines that read as
    /// <para/>[SectionName]
    /// <para/>key1=value1
    /// <para/>key2=value2
    /// </summary>
    public class IniFileConfigKeyProvider : DictionaryConfigKeyProvider
    {
        /// <summary>
        /// Constructor: Accepts provider name, filePath to ini file to read/write, keyPrefix to prefix on all contained keys, 
        /// and isReadWrite to indicate if the INI file is writable or not (ie if all of the keys should be IsFixed).
        /// </summary>
        public IniFileConfigKeyProvider(string name, string filePath, string keyPrefix = "", bool isReadWrite = false, INamedValueSet metaData = null, IEnumerable<string> ensureSectionsExist = null)
            : base(name, !isReadWrite, metaData, keysMayBeAddedUsingEnsureExistsOption : isReadWrite)
        {
            if (isReadWrite)
                BaseFlags = new ConfigKeyProviderFlags(BaseFlags) { MayBeChanged = true, IsPersisted = true };

            ConfigKeyAccessFlags defaultAccessFlags = new ConfigKeyAccessFlags();

            KeyPrefix = keyPrefix = keyPrefix.MapNullToEmpty();

            givenFilePath = filePath;
            fullFilePath = System.IO.Path.GetFullPath(filePath);

            string[] rawFileLines = new string [0];

            try
            {
                rawFileLines = System.IO.File.ReadAllLines(fullFilePath);
            }
            catch (System.Exception ex)
            {
                Logger.Error.Emit("Unable to read INI file contents from File '{0}': {1}", fullFilePath, ex);
            }

            string [] trimmedFileLines = rawFileLines.Select((line) => line.Trim()).ToArray();

            FileSectionLineItem currentSection = sectionItemDictionary[string.Empty];        // start with the default file Section LineItem (where lines go if they are above the first section
            currentSection.sectionKeyPrefix = KeyPrefix;

            for (int lineIdx = 0; lineIdx < trimmedFileLines.Length; lineIdx++)
            {
                int lineNum = lineIdx + 1;
                string trimmedLine = trimmedFileLines[lineIdx];
                string rawFileLine = rawFileLines[lineIdx];

                if (trimmedLine.StartsWith("["))
                {
                    if (trimmedLine.EndsWith("]"))
                    {
                        string sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);

                        if (sectionItemDictionary.TryGetValue(sectionName, out currentSection))
                        { }
                        else
                        {
                            currentSection = new FileSectionLineItem(lineIdx, rawFileLine, trimmedLine)
                            {
                                sectionName = sectionName,
                                sectionKeyPrefix = "{0}{1}.".CheckedFormat(KeyPrefix, sectionName),
                            };
                            sectionItemDictionary.Add(sectionName, currentSection);
                        }
                    }
                    else
                    {
                        Logger.Debug.Emit(@"File '{0}' lineNum:{1} Invalid section header:'{2}'", fullFilePath, lineNum, trimmedLine);
                    }
                }
                else if (trimmedLine.Length == 0 || trimmedLine.StartsWith(@"#") || trimmedLine.StartsWith(@"//") || trimmedLine.StartsWith(@";"))
                {
                    // the line is a blank line or a comment line
                    currentSection.Add(new LineItem(lineIdx, rawFileLine, commentText:trimmedLine));
                }
                else
                {
                    string[] linePartsArray = rawFileLine.Split(new char[] { '=' }, 2);

                    string fileKeyName = linePartsArray.SafeAccess(0, String.Empty).Trim();

                    ValueLineItem valueItem = new ValueLineItem(lineIdx, rawFileLine, trimmedLine) { fileKeyName = fileKeyName, hasEquals = linePartsArray.Length >= 2 };

                    valueItem.ckai = new ConfigKeyAccessImpl("{0}{1}".CheckedFormat(currentSection.sectionKeyPrefix, valueItem.fileKeyName), defaultAccessFlags, this);

                    if (valueItem.hasEquals)
                        valueItem.ckai.ValueContainer = new ValueContainer(linePartsArray.SafeAccess(1, String.Empty));
                    else
                        valueItem.ckai.ValueContainer = new ValueContainer();

                    currentSection.Add(valueItem);
                }
            }

            if (!ensureSectionsExist.IsNullOrEmpty())
            {
                foreach (string sectionName in ensureSectionsExist)
                {
                    if (!sectionItemDictionary.ContainsKey(sectionName))
                    {
                        string fileLine = "[{0}]".CheckedFormat(sectionName);
                        currentSection = new FileSectionLineItem(rawFileLine: fileLine, trimmedFileLine: fileLine)
                        {
                            sectionName = sectionName,
                            sectionKeyPrefix = "{0}{1}.".CheckedFormat(KeyPrefix, sectionName),
                        };
                        sectionItemDictionary.Add(sectionName, currentSection);
                    }
                }
            }

            sectionItemArray = sectionItemDictionary.Values.ToArray();

            AddRange(sectionItemArray.SelectMany(sectionItem => sectionItem.valueLineItemList.Select((vItem) => vItem.ckai)));
        }

        string givenFilePath = null;
        string fullFilePath = null;

        Dictionary<string, FileSectionLineItem> sectionItemDictionary = new Dictionary<string, FileSectionLineItem>() { { "", new FileSectionLineItem() } };
        FileSectionLineItem[] sectionItemArray;

        string[] savedLines = null;

        class LineItem
        {
            public LineItem(int originalLineIdx = -1, string rawFileLine = null, string trimmedFileLine = "", string commentText = "")
            {
                this.originalLineIdx = originalLineIdx;
                this.rawFileLine = rawFileLine;
                this.trimmedFileLine = trimmedFileLine;
                this.commentText = commentText;

                saveFileLine = rawFileLine;
            }

            public int originalLineIdx = -1;
            public string rawFileLine;
            public string trimmedFileLine;
            public string commentText;
            public string saveFileLine;

            public virtual string UpdateSaveFileLine()
            {
                saveFileLine = rawFileLine;
                return saveFileLine;
            }
        }

        class FileSectionLineItem : LineItem
        {
            public FileSectionLineItem(int originalLineIdx = -1, string rawFileLine = null, string trimmedFileLine = "")
                : base(originalLineIdx, rawFileLine, trimmedFileLine)
            { }

            public string sectionName = null;
            public string sectionKeyPrefix;

            public List<LineItem> lineItemList = new List<LineItem>();
            public List<ValueLineItem> valueLineItemList = new List<ValueLineItem>();

            public FileSectionLineItem Add(LineItem lineItem)
            {
                lineItemList.Add(lineItem);

                ValueLineItem vli = lineItem as ValueLineItem;
                if (vli != null)
                    valueLineItemList.Add(vli);

                return this;
            }
        }

        class ValueLineItem : LineItem
        {
            public ValueLineItem(int originalLineIdx, string rawFileLine, string trimmedFileLine)
                : base(originalLineIdx, rawFileLine, trimmedFileLine)
            { }

            public string fileKeyName;
            public bool hasEquals;

            public ConfigKeyAccessImpl ckai;

            public override string UpdateSaveFileLine()
            {
                if (hasEquals || ckai.HasValue)
                {
                    if (ckai.HasValue)
                        saveFileLine = "{0}={1}".CheckedFormat(fileKeyName, ckai.ValueContainer.ValueAsObject);
                    else
                        saveFileLine = fileKeyName;
                }
                else
                {
                    saveFileLine = rawFileLine;
                }

                return saveFileLine;
            }
        }

        protected override string VerifyAndNoteItemAdded(DictionaryKeyItem item)
        {
            string key = item.key.MapNullToEmpty();

            FileSectionLineItem addToSection = null;

            // find the section with the longest sectionKeyPrefix that is a prefix for this key.  Keys are added to the first section in the list by default.
            foreach (FileSectionLineItem testAddToSection in sectionItemArray)
            {
                if (key.StartsWith(testAddToSection.sectionKeyPrefix) && (addToSection == null || testAddToSection.sectionKeyPrefix.Length > addToSection.sectionKeyPrefix.Length))
                    addToSection = testAddToSection;
            }

            if (addToSection == null)
            {
                return "key does not start with any known prefix in this provider";
            }

            string fileKeyName = key.Substring(addToSection.sectionKeyPrefix.Length);

            ValueLineItem newValueItem = new ValueLineItem(-1, "", "") { fileKeyName = fileKeyName, ckai = item.ckai, hasEquals = item.ckai.HasValue };

            // find the first non-blank line back from the end of the section and then insert the value line item before that item.

            IEnumerable<Tuple<LineItem, int>> sectionLineItemsWithIndecies = addToSection.lineItemList.Select((li, idx) => Tuple.Create(li, idx));

            Tuple<LineItem, int> lastValueLineItemTuple = sectionLineItemsWithIndecies.Where(t => t.Item1 is ValueLineItem).LastOrDefault();
            Tuple<LineItem, int> firstInsertPointLineItemTuple = sectionLineItemsWithIndecies.Where(t => t.Item1.commentText.Contains("SectionNewItemsInsertPoint")).LastOrDefault();
            Tuple<LineItem, int> lastEmptyLineItemTuple = sectionLineItemsWithIndecies.Where(t => t.Item1.commentText.IsEmpty()).LastOrDefault();    // comments have non-empty rawFileLine but have empty trimmedFileLine contents

            Tuple<LineItem, int> insertSearchStartingPoint = new Tuple<LineItem,int>[] { lastValueLineItemTuple, firstInsertPointLineItemTuple, lastEmptyLineItemTuple }.Where(t => t != null).FirstOrDefault();

            int insertBeforeLineItemIdx = ((insertSearchStartingPoint != null) ? insertSearchStartingPoint.Item2 : addToSection.lineItemList.Count);

            // skip over the last ValueLineItem or a single comment line as selected above
            if (insertBeforeLineItemIdx < addToSection.lineItemList.Count)
            {
                LineItem lineItem = addToSection.lineItemList[insertBeforeLineItemIdx];
                if (lineItem is ValueLineItem || !lineItem.commentText.IsNullOrEmpty())
                    insertBeforeLineItemIdx++;
            }

            if (insertBeforeLineItemIdx.IsInRange(0, addToSection.lineItemList.Count))
                addToSection.lineItemList.Insert(insertBeforeLineItemIdx, newValueItem);
            else
                addToSection.lineItemList.Add(newValueItem);

            addToSection.valueLineItemList.Add(newValueItem);

            return string.Empty;
        }

        /// <summary>
        /// This method will attempt to rewrite the given INI file path
        /// </summary>
        protected override string HandleSetValuesChangedContents()
        {
            string[] linesToSave = sectionItemArray.SelectMany(sectionItem => (sectionItem.sectionName != null ? new string[] { sectionItem.rawFileLine } : new string[0]).Concat(sectionItem.lineItemList.Select(lineItem => lineItem.UpdateSaveFileLine()))).ToArray();

            try
            {
                System.IO.File.WriteAllLines(fullFilePath, linesToSave);

                savedLines = linesToSave;

                Logger.Debug.Emit("Updated contents of INI File '{0}'", fullFilePath);

                return String.Empty;
            }
            catch (System.Exception ex)
            {
                string mesg = "File write failed to INI File '{0}': {1}".CheckedFormat(fullFilePath, ex);

                return mesg;
            }
        }
    }

    #endregion

    #region PersistentXmlTextFileRingProvider, PersistentSerializedTextFileRingProviderBase

    /// <summary>
    /// Provides a type of DicationaryConfigKeyProvider obtained by using a DataContractPersistentXmlTextFileRingStorageAdapter based on the ConfigKeyStore file format.
    /// Normally this provider is used for read/write behavior and is most easily used to support EnsureExists usage patterns and/or moderate to high write rate usages
    /// with the same file IO failure handling that is provided through the use of the PeristentObjectFileRing.
    /// </summary>
    public class PersistentXmlTextFileRingProvider : PersistentSerializedTextFileRingProviderBase
    {
        /// <summary>
        /// Constructor: Accepts provider name, filePath to ini file to read/write, keyPrefix to prefix on all contained keys, 
        /// and isReadWrite to indicate if the INI file is writable or not (ie if all of the keys should be IsFixed).
        /// </summary>
        public PersistentXmlTextFileRingProvider(string name, PersistentObjectFileRingConfig ringConfig, string keyPrefix = "", bool isReadWrite = true, INamedValueSet metaData = null, bool sortKeysOnSave = false)
            : base(name, ringConfig, new DataContractPersistentXmlTextFileRingStorageAdapter<ConfigKeyStore>(name, ringConfig) { Object = new ConfigKeyStore() }, keyPrefix: keyPrefix, isReadWrite: isReadWrite, metaData: metaData, keysMayBeAddedUsingEnsureExistsOption: isReadWrite, sortKeysOnSave: sortKeysOnSave)
        { }
    }

    /// <summary>
    /// Provides a type of DicationaryConfigKeyProvier obtained by using a DataContractPersistentXmlTextFileRingStorageAdapter based on the ConfigKeyStore file format.
    /// Normally this provider is used for read/write behavior and is most easily used to support EnsureExists usage patterns and/or moderate to high write rate usages
    /// with the same file IO failure handling that is provided through the use of the PeristentObjectFileRing.
    /// </summary>
    public class PersistentSerializedTextFileRingProviderBase : DictionaryConfigKeyProvider
    {
        /// <summary>
        /// Constructor: Accepts provider name, filePath to ini file to read/write, keyPrefix to prefix on all contained keys, 
        /// and isReadWrite to indicate if the INI file is writable or not (ie if all of the keys should be IsFixed).
        /// </summary>
        public PersistentSerializedTextFileRingProviderBase(string name, PersistentObjectFileRingConfig ringConfig, IPersistentStorage<ConfigKeyStore> ringAdapter, string keyPrefix = "", bool isReadWrite = true, bool keysMayBeAddedUsingEnsureExistsOption = true, INamedValueSet metaData = null, bool sortKeysOnSave = false)
            : base(name, !isReadWrite, metaData, keysMayBeAddedUsingEnsureExistsOption: isReadWrite)
        {
            if (isReadWrite)
                BaseFlags = new ConfigKeyProviderFlags(BaseFlags) { MayBeChanged = true, IsPersisted = true };

            ConfigKeyAccessFlags defaultAccessFlags = new ConfigKeyAccessFlags();

            KeyPrefix = keyPrefix = keyPrefix.MapNullToEmpty();
            SortKeysOnSave = sortKeysOnSave;

            this.ringAdapter = ringAdapter;

            string activity = "Initial";
            try
            {
                if (!ringConfig.AnyRingFilesExist)
                {
                    Logger.Debug.Emit("No persist ring files found.  Assuming that this is the initial use.");
                    activity = "ringAdapter.Save";
                    ringAdapter.Save();
                }
                else
                {
                    activity = "ringAdapter.Load";
                    ringAdapter.Load();
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error.Emit("{0} failed at {1}: {2}", Fcns.CurrentMethodName, activity, ex);
            }

            configKeyStore = ringAdapter.Object;
            if (configKeyStore == null)
            {
                configKeyStore = new ConfigKeyStore();
                ringAdapter.Object = configKeyStore;
            }

            foreach (KeyItem fileKeyItem in configKeyStore.KeySet)
            {
                PersistKeyTracker pkt = new PersistKeyTracker()
                {
                    fileKeyItem = fileKeyItem,
                    ckai = new ConfigKeyAccessImpl("{0}{1}".CheckedFormat(KeyPrefix, fileKeyItem.Key), defaultAccessFlags, this) { ValueContainer = fileKeyItem.VC },
                };

                persistKeyTrackerList.Add(pkt);
            }

            AddRange(persistKeyTrackerList.Select(pkt => pkt.ckai));

            foreach (var pkt in persistKeyTrackerList)
            {
                if (pkt.dictionaryKeyItem == null)
                {
                    DictionaryKeyItem dki = null;

                    if (keyItemDictionary.TryGetValue(pkt.ckai.Key, out dki))
                        pkt.dictionaryKeyItem = dki;
                }
            }
        }

        IPersistentStorage<ConfigKeyStore> ringAdapter;
        ConfigKeyStore configKeyStore;
        List<PersistKeyTracker> persistKeyTrackerList = new List<PersistKeyTracker>();
        bool regenerateConfigKeyStoreKeySetOnNextSave = false;
        public bool SortKeysOnSave { get; private set; }

        private void RegenerateConfigKeyStoreKeySetIfNeeded()
        {
            if (!regenerateConfigKeyStoreKeySetOnNextSave)
                return;

            if (SortKeysOnSave)
                configKeyStore.KeySet = new KeySet(persistKeyTrackerList.Select(tracker => tracker.fileKeyItem).OrderBy(keyItem => keyItem.Key));

            regenerateConfigKeyStoreKeySetOnNextSave = false;
        }

        protected class PersistKeyTracker
        {
            public KeyItem fileKeyItem;
            public DictionaryKeyItem dictionaryKeyItem;
            public ConfigKeyAccessImpl ckai;

            public override string ToString()
            {
                return dictionaryKeyItem.ToString();
            }
        }

        protected override string VerifyAndNoteItemAdded(DictionaryKeyItem item)
        {
            if (!item.key.StartsWith(KeyPrefix))
                return "key does not start with required prefix";

            string persistKey = item.key.Substring(KeyPrefix.Length);

            PersistKeyTracker pkt = new PersistKeyTracker()
            {
                dictionaryKeyItem = item,
                ckai = item.ckai,
                fileKeyItem = new KeyItem() { Key = persistKey, VC = item.ckai.ValueContainer, Comment = item.comment },
            };

            persistKeyTrackerList.Add(pkt);
            configKeyStore.Dictionary[pkt.fileKeyItem.Key] = pkt.fileKeyItem;

            regenerateConfigKeyStoreKeySetOnNextSave = true;

            return string.Empty;
        }

        /// <summary>
        /// This method will attempt to update the configKeyStore contents and save it to the next file in the ring.
        /// </summary>
        protected override string HandleSetValuesChangedContents()
        {
            // update the configKeyStore KeySet item(s) contents
            foreach (var pkt in persistKeyTrackerList)
            {
                if (!pkt.fileKeyItem.VC.Equals(pkt.ckai.ValueContainer))
                    pkt.fileKeyItem.VC = pkt.ckai.ValueContainer;

                if (pkt.fileKeyItem.Comment != pkt.dictionaryKeyItem.comment)
                    pkt.fileKeyItem.Comment = pkt.dictionaryKeyItem.comment;
            }

            try
            {
                // Save it to the next item in the ring
                RegenerateConfigKeyStoreKeySetIfNeeded();

                ringAdapter.Save();

                Logger.Debug.Emit("Updated contents of File '{0}'", ringAdapter.LastObjectFilePath);

                return String.Empty;
            }
            catch (System.Exception ex)
            {
                string mesg = "ringAdapter.Save failed: {0}".CheckedFormat(ex);

                return mesg;
            }
        }
    }

    #endregion

    #region RegistryKeyTreeProvider

    /// <summary>
    /// Provides a type of DicationaryConfigKeyProvier obtained by loading a windows registry key tree
    /// <para/>[SectionName]
    /// <para/>key1=value1
    /// <para/>key2=value2
    /// </summary>
    public class RegistryKeyTreeProvider : DictionaryConfigKeyProvider
    {
        /// <summary>
        /// Constructor: Accepts provider name, rootKeyPath to enumerate through, keyPrefix to prefix on all contained keys, 
        /// and isReadWrite to indicate if the INI file is writable or not (ie if all of the keys should be IsFixed).
        /// </summary>
        public RegistryKeyTreeProvider(string name, string registryRootPath, string keyPrefix, bool isReadWrite, INamedValueSet metaData = null)
            : base(name, !isReadWrite, metaData, keysMayBeAddedUsingEnsureExistsOption : false)
        {
            if (isReadWrite)
            {
                BaseFlags = new ConfigKeyProviderFlags(BaseFlags) { MayBeChanged = true, IsPersisted = true };
                defaultAccessFlags.MayBeChanged = true;
            }

            KeyPrefix = keyPrefix = keyPrefix.MapNullToEmpty();

            RegistryRootPath = registryRootPath;

            AddExplicitDisposeAction(
                () => 
                {
                    openRegKeyList.ForEach((regKey) => Fcns.DisposeOfGivenObject(regKey));
                    openRegKeyList.Clear(); 
                });

            try
            {
                RegistryKeyPermissionCheck keyTreePermissions = isReadWrite ? Microsoft.Win32.RegistryKeyPermissionCheck.ReadWriteSubTree : Microsoft.Win32.RegistryKeyPermissionCheck.ReadSubTree;

                List<string> regKeyPathList = new List<string>(Win32.Registry.Fcns.SplitRegistryKeyPath(RegistryRootPath));
                string lastKey = null;
                if (regKeyPathList.Count > 0)
                {
                    lastKey = regKeyPathList[regKeyPathList.Count-1];
                    regKeyPathList.RemoveAt(regKeyPathList.Count-1);
                }
                string [] parentPathArray = regKeyPathList.ToArray();

                using (RegistryKey parentReadOnlyKey = Win32.Registry.Fcns.OpenRegistryKeyPath(null, parentPathArray, RegistryKeyPermissionCheck.ReadSubTree))
                {
                    RegistryKey rootRegKey = Win32.Registry.Fcns.OpenRegistryKeyPath(parentReadOnlyKey, lastKey, keyTreePermissions);
                    openRegKeyList.Add(rootRegKey);

                    Enumerate(keyPrefix, rootRegKey, keyTreePermissions);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error.Emit("Unable to successfully enumerate Registry keys and values under '{0}': {1}", RegistryRootPath, ex);
            }

            AddRange(valueItemList.Select((vItem) => vItem.ckai));
        }

        ConfigKeyAccessFlags defaultAccessFlags = new ConfigKeyAccessFlags();

        private void Enumerate(string keyPrefix, RegistryKey regKey, RegistryKeyPermissionCheck keyTreePermissions)
        {
            if (regKey == null)
                return;

            string currentKeyName = regKey.Name;

            string[] valueNames = regKey.GetValueNames();

            foreach (string valueName in valueNames)
            {
                try
                {
                    RegistryValueKind valueKind = regKey.GetValueKind(valueName);
                    object valueAsObject = regKey.GetValue(valueName, null);
                    ValueContainer valueContainer = new ValueContainer(valueAsObject);
                    bool isFixed = false;

                    ValueItem item = null;
                    string keyName = "{0}{1}".CheckedFormat(keyPrefix, valueName);

                    item = new ValueItem() { parentRegKey = regKey, valueName = valueName, initialValueKind = valueKind, lastContainedValue = valueContainer, isFixed = isFixed };

                    item.ckai = new ConfigKeyAccessImpl(keyName, defaultAccessFlags, this) { ValueContainer = item.lastContainedValue };
                    if (item.isFixed)
                        item.ckai.ProviderFlags = item.ckai.ProviderFlags.MergeWith(new ConfigKeyProviderFlags() { IsFixed = true });

                    valueItemList.Add(item);
                }
                catch (System.Exception ex)
                {
                    Logger.Debug.Emit("Could not extract '{0}/{1}'s type or value: {2}", currentKeyName, valueName, ex);
                }
            }

            string[] subKeyNames = regKey.GetSubKeyNames();

            foreach (string subKeyName in subKeyNames)
            {
                try
                {
                    RegistryKey subKey = Win32.Registry.Fcns.OpenRegistryKeyPath(regKey, subKeyName, keyTreePermissions);
                    openRegKeyList.Add(subKey);

                    string subKeyPrefix = "{0}{1}.".CheckedFormat(keyPrefix, subKeyName);

                    Enumerate(subKeyPrefix, subKey, keyTreePermissions);
                }
                catch (System.Exception ex)
                {
                    Logger.Debug.Emit("Could not enumerate into registry sub-key '{0}/{1}': {2}", currentKeyName, subKeyName, ex); 
                }
            }
        }

        /// <summary>
        /// Set this property to True to force each HandleSetValuesChangedContents call to flush the registry to disk before returning (or not). 
        /// </summary>
        public bool ForceFullFlushOnUpdate { get; set; }

        string RegistryRootPath {get; set;}

        List<RegistryKey> openRegKeyList = new List<RegistryKey>();
        List<ValueItem> valueItemList = new List<ValueItem>();

        class ValueItem
        {
            public RegistryKey parentRegKey;

            public string valueName;
            public RegistryValueKind initialValueKind;
            public bool isFixed;
            public ConfigKeyAccessImpl ckai;
            public ValueContainer lastContainedValue;
        }

        /// <summary>
        /// This method will attempt to update any registry keys for values that have been changed since they were last updated.
        /// </summary>
        protected override string HandleSetValuesChangedContents()
        {
            string mesg = null;

            int updateCount = 0;

            try
            {
                foreach (ValueItem item in valueItemList)
                {
                    if (!item.ckai.ProviderFlags.IsFixed && !item.lastContainedValue.IsEqualTo(item.ckai.ValueContainer))
                    {
                        try
                        {
                            bool doSet = !item.isFixed;

                            if (doSet)
                            {
                                object valueAsObject = item.ckai.ValueContainer.ValueAsObject;

                                // convert IList<String> values into string array values.
                                IList<String> vaoailos = valueAsObject as IList<String>;
                                if (vaoailos != null)
                                    valueAsObject = vaoailos.ToArray();

                                item.parentRegKey.SetValue(item.valueName, valueAsObject);
                                item.lastContainedValue = item.ckai.ValueContainer;
                                updateCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            mesg = mesg ?? "Could not write key to registry failed at '{0}': {1}".CheckedFormat(item.ckai.Key, ex);
                        }
                    }
                }

                if (ForceFullFlushOnUpdate && updateCount != 0)
                {
                    MosaicLib.Win32.Registry.Fcns.RegFlushKey(new IntPtr());
                }
            }
            catch (System.Exception ex)
            {
                mesg = mesg ?? "Registry key write failed under '{0}': {1}".CheckedFormat(RegistryRootPath, ex);
            }

            return mesg ?? String.Empty;
        }
    }

    #endregion

    #region ConfigKeyFile and ConfigKeyStore (used by IncludeFilesConfigKeyProvider)

    /// <summary>
    /// Class used to define the file content/format for ConfigKeyFiles.  
    /// This is used for Include files and this is the base type used by the ConfigKeyStore class which is the current default implementation for persisting non-fixed config key values.
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public class ConfigKeyFile
    {
        /// <summary>Property defines the load/save element that KeyItem's appear in.  Property implementation wrappers the Dictionary to provide more useful local storage of the KeyItem collection.</summary>
        [DataMember(Order = 20, Name = "Set")]
        public KeySet KeySet
        {
            get
            {
                return new KeySet(Dictionary.Values);
            }

            set
            {
                Dictionary.Clear();
                if (value != null)
                {
                    foreach (KeyItem item in value)
                        Dictionary[item.Key] = item;
                }
            }
        }

        /// <summary>backing container property for KeySet property.  Used to avoid making programmatic clients resort to simple list search to find keys.  Assigning this to null causes new empty dictionary to be constructed and assign/used instead.</summary>
        public Dictionary<string, KeyItem> Dictionary
        {
            get
            {
                if (dictionary == null)
                    dictionary = new Dictionary<string, KeyItem>();
                return dictionary;
            }
            set { dictionary = value; }
        }

        private Dictionary<string, KeyItem> dictionary;
    }

    /// <summary>
    /// Class defines the file storage format used by the default implementation for persisting non-fixed config key values.
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public class ConfigKeyStore : ConfigKeyFile, Persist.IPersistSequenceable
    {
        #region IPersistSequenceable Members

        /// <summary>Property is required by IPersistSequenceable interface.  Used to determine which of a set of loaded file contents is the most recent (highest sequence number value).</summary>
        [DataMember(Order = 10, Name = "SeqNum", IsRequired=false)]
        public ulong PersistedVersionSequenceNumber { get; set; }

        #endregion
    }

    /// <summary>Proxy class, derived from List{KeyItem}, used to allow Xml element naming to be customized.</summary>
    [CollectionDataContract(Namespace = Constants.ModularNameSpace, Name = "Set", ItemName = "Item")]
    public class KeySet : List<KeyItem>
    {
        /// <summary>Default constructor.  Creates empty list</summary>
        public KeySet() { }
        /// <summary>Copy constructor.  Creates list which references each of the KeyItem's produced by the given itemIter iterator/enumerable set.</summary>
        public KeySet(IEnumerable<KeyItem> itemIter) : base(itemIter) { }
    }

    /// <summary>
    /// DataContract Configuration Key information serialization/deserialization item class.  
    /// Used with <see cref="KeySet"/> and <seealso cref="ConfigKeyFile"/> and <seealso cref="ConfigKeyStore"/> classes.
    /// Used to associate the Key name, the string Value and other related properties both for load only cases (Include key source) and load/save cases (Persist)
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    [KnownType(typeof(Int32Validator))]
    [KnownType(typeof(DoubleValidator))]
    [KnownType(typeof(EnumValidator))]
    public class KeyItem
    {
        /// <summary>Gives the full, or partial, key name of the key for this KeyItem.  May be partial name in include files (ConfigKeyFile).  Always full key in ConfigKeyStore instances and files.</summary>
        [DataMember(Order = 10)]
        public string Key { get; set; }

        /// <summary>Value property give the key's value (default, current, or fixed).</summary>
        public ValueContainer VC { get { return VCE.VC; } set { VCE.VC = value; } }

        /// <summary>private implementation field used as a serialization envelope for the ValueContainer property.</summary>
        [DataMember(Name = "VC", Order = 90, IsRequired = false, EmitDefaultValue = false)]
        private ValueContainerEnvelope VCE 
        {
            get { return ((internalVCE != null) ? internalVCE : (internalVCE = new ValueContainerEnvelope())); } 
            set { internalVCE = ((value != null) ? value : new ValueContainerEnvelope()); } 
        }
        private ValueContainerEnvelope internalVCE = null;  // construction must match default value produced when using DataContract based deserialization.

        /// <summary>Preserved for backward compatibility with existing xml include files.  Assigns the VC to be the given string value.   Getter always returns null to prevent serialization.</summary>
        [DataMember(Order = 100, IsRequired=false, EmitDefaultValue=false)]
        private string Value 
        { 
            get { return null; } 
            set { VC = new ValueContainer(value); } 
        }

        /// <summary>Optional meta-data property to give a description of the key for documentation and assistance purposes</summary>
        [DataMember(Order = 1000, IsRequired = false, EmitDefaultValue = false)]
        public string Description { get; set; }

        /// <summary>Optional property that may be provided as a comment.  Generally intended for use when saving a new (overlay) key value into the currently configured Persist storage system.</summary>
        [DataMember(Order = 1010, IsRequired = false, EmitDefaultValue = false)]
        public string Comment { get; set; }

        /// <summary>Debugging assistant method - gives quick view of item contents</summary>
        public override string ToString()
        {
            string DescStr = (Description.IsNullOrEmpty() ? String.Empty : " Desc:'{0}'".CheckedFormat(Description));
            string CommentStr = (Comment.IsNullOrEmpty() ? String.Empty : " Comment:'{0}'".CheckedFormat(Comment));

            return Fcns.CheckedFormat("Key:'{0}' VC:{1}{2}{3}", Key, VC, DescStr, CommentStr);
        }
    }

    #region Value Validation (work in progress - not currently in use

    /// <summary>
    /// This interface defines the API of supported value Validator classes
    /// </summary>
    public interface IKeyValueValidator
    {
        /// <summary>
        /// This method is passed the value, formatted as a string, to be validated.
        /// It returns the empty string if the entire string contents are considered valid for this key or, if the value is not valid, 
        /// it returns a non-empty string error message describing why the given value is not valid.
        /// <para/>For validation errors, the return value should be a simple description of the validation failure.  
        /// It should not start with any common prefix as these are added by the caller when useful.
        /// </summary>
        string Validate(string valueStr);
    }

    /// <summary>Double specific <see cref="IKeyValueValidator"/> implementation class to be used as the Validator element in a <seealso cref="KeyItem"/> instance</summary>
    [DataContract(Namespace = Constants.ConfigNameSpace)]
    public class DoubleValidator : IKeyValueValidator
    {
        /// <summary>Optional value that defines the minimum permitted value of the range accepted by this validator</summary>
        [DataMember(Order = 10, IsRequired = false, EmitDefaultValue = false)]
        public double? Min { get; set; }

        /// <summary>Optional value that defines the maximum permitted value of the range accepted by this validator</summary>
        [DataMember(Order = 20, IsRequired = false, EmitDefaultValue = false)]
        public double? Max { get; set; }

        /// <summary>Optional boolean value.  When explicitly set to true, NaN is a permitted value for this validator</summary>
        [DataMember(Order = 30, IsRequired = false, EmitDefaultValue = false)]
        public bool? AllowNaN { get; set; }

        /// <summary>
        /// Returns empty string when given value can be parsed as a Double and it is no less than the Min (when non-null) and no greater than the Max (when non-null) and is not a NaN when the AllowNaN property is null or false.
        /// Returns a string description when at least one of these conditions has not been met.
        /// </summary>
        public string Validate(string valueStr)
        {
            double value;
            if (!StringScanner.ParseValue(valueStr, out value))
                return Fcns.CheckedFormat("'{0}' could not parsed as a valid double value", valueStr);

            if (Min.HasValue && Max.HasValue)
            {
                bool inRange = (value >= Min.GetValueOrDefault() && value <= Max.GetValueOrDefault());
                if (!inRange)
                    return Fcns.CheckedFormat("'{0}' is not in range from Min:{1} to Max:{2}", valueStr, Min, Max);
            }
            else if (Min.HasValue)
            {
                bool inRange = (value >= Min.GetValueOrDefault());
                if (!inRange)
                    return Fcns.CheckedFormat("'{0}' is not greater than or equal to Min:{1}", valueStr, Min);
            }
            else if (Max.HasValue)
            {
                bool inRange = (value <= Max.GetValueOrDefault());
                if (!inRange)
                    return Fcns.CheckedFormat("'{0}' is not less than or equal to Max:{1}", valueStr, Max);
            }

            if (!AllowNaN.GetValueOrDefault() && Double.IsNaN(value))
            {
                return Fcns.CheckedFormat("'{0}' is not a number (NaN)", valueStr);
            }

            return String.Empty;
        }
    }

    /// <summary>Int32 specific <see cref="IKeyValueValidator"/> implementation class to be used as the Validator element in a <seealso cref="KeyItem"/> instance</summary>
    [DataContract(Namespace = Constants.ConfigNameSpace)]
    public class Int32Validator : IKeyValueValidator
    {
        /// <summary>Optional value that defines the minimum permitted value of the range accepted by this validator</summary>
        [DataMember(Order = 10, IsRequired = false, EmitDefaultValue = false)]
        public int? Min { get; set; }

        /// <summary>Optional value that defines the maximum permitted value of the range accepted by this validator</summary>
        [DataMember(Order = 20, IsRequired = false, EmitDefaultValue = false)]
        public int? Max { get; set; }

        /// <summary>
        /// Returns empty string when given value can be parsed as an Int32 and it is no less than the Min (when non-null) and no greater than the Max (when non-null).
        /// Returns a string description when at least one of these conditions has not been met.
        /// </summary>
        public string Validate(string valueStr)
        {
            int value;
            if (!StringScanner.ParseValue(valueStr, out value))
                return Fcns.CheckedFormat("'{0}' could not parsed as a valid Int32 value", valueStr);

            if (Min.HasValue && Max.HasValue)
            {
                bool inRange = (value >= Min.GetValueOrDefault() && value <= Max.GetValueOrDefault());
                if (!inRange)
                    return Fcns.CheckedFormat("'{0}' is not in range from Min:{1} to Max:{2}", valueStr, Min, Max);
            }
            else if (Min.HasValue)
            {
                bool inRange = (value >= Min.GetValueOrDefault());
                if (!inRange)
                    return Fcns.CheckedFormat("'{0}' is not greater than or equal to Min:{1}", valueStr, Min);
            }
            else if (Max.HasValue)
            {
                bool inRange = (value <= Max.GetValueOrDefault());
                if (!inRange)
                    return Fcns.CheckedFormat("'{0}' is not less than or equal to Max:{1}", valueStr, Max);
            }

            return String.Empty;
        }
    }

    /// <summary>
    /// Type specific <see cref="IKeyValueValidator"/> implementation class to be used as the Validator element in a <seealso cref="KeyItem"/> instance.
    /// Elements of this type must provide a TypeName value that evaluates to a Type symbol that is suitable for use with with System.Enum.GetNames and System.Enum.Parse
    /// </summary>
    [DataContract(Namespace = Constants.ConfigNameSpace)]
    public class EnumValidator : IKeyValueValidator
    {
        /// <summary>Constructor/Initialized/Set to the full typename of the enumeration against which key values shall be validated.</summary>
        [DataMember(Order = 10, IsRequired = true)]
        public String TypeName { get { return typeName; } set { typeName = value; Type = null; } }
        /// <summary>Backing storage for TypeName property.</summary>
        private String typeName;

        private Type Type { get; set; }
        private String MembersListStr { get; set; }
        private bool IsFlagEnum { get; set; }

        /// <summary>
        /// Returns empty string when given value can be parsed to a value of the Type named in the TypeName property.
        /// Returns non-empty string description of the issue when the given valueStr cannot be successfully parsed in this way.
        /// </summary>
        public string Validate(string valueStr)
        {
            try
            {
                bool isValidEnumType = false;

                if (Type == null)
                {
                    try
                    {
                        Type = System.Type.GetType(TypeName);
                        MembersListStr = String.Join(",", System.Enum.GetNames(Type));
                        IsFlagEnum = Type.GetCustomAttributes(false).Any((a) => (a is FlagsAttribute));

                        isValidEnumType &= Type.IsEnum;
                    }
                    catch
                    {
                        isValidEnumType = false;
                    }
                }

                if (!isValidEnumType)
                    return Fcns.CheckedFormat("Could not validate '{0}': TypeName:'{1}' is not a valid System.Enum Type", valueStr, TypeName);

                try
                {
                    object value = System.Enum.Parse(Type, valueStr);

                    return String.Empty;    // value is a valid instance of this enum
                }
                catch
                {
                    string mustStr = IsFlagEnum ? "Must be composed of" : "Must be one of";
                    return Fcns.CheckedFormat("'{0}' is not a valid member of the '{1}' enumeration.  {2} '{3}'", valueStr, TypeName, mustStr, MembersListStr);
                }
            }
            catch
            {
                return "Unexpected Exception thrown by Validate method.  Verify that TypeName is valid.";
            }
        }
    }

    #endregion

    #endregion
}
