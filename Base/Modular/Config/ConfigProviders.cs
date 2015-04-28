//-------------------------------------------------------------------
/*! @file ConfigProviers.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2015 Mosaic Systems Inc., All rights reserved
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

using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using MosaicLib.Utils;
using System.Text;
using System.Collections;

namespace MosaicLib.Modular.Config
{
    #region ConfigKeyProviderBase

    /// <summary>
    /// Base IConfigKeyProvider implementation class.
    /// </summary>
    public abstract class ConfigKeyProviderBase : IConfigKeyProvider
    {
        /// <summary>
        /// Constructor.  Requires name.  Other properties shall be initialized using property initializer list.
        /// Provider generated messages will use the string "Config.Provider.{name}" where {name} is the name given here.
        /// </summary>
        public ConfigKeyProviderBase(string name)
        {
            Logger = new Logging.Logger("Config.Provider." + name);
            Name = name;
        }

        /// <summary>
        /// This is the Logger instance that the provider will use.
        /// </summary>
        public Logging.Logger Logger { get; protected set; }

        /// <summary>
        /// This property is used by the provider to prefix all of the partial key names that it creates with a common prefix.  When this string is not empty
        /// it will effecitevely shift all of the keys that are served by this provider to live under the partial path specified as the common prefix.
        /// The provider must account for this when searching for keys using its public methods and all key strings returned by this provider must start with this prefix.
        /// </summary>
        public string KeyPrefix { get { return keyPrefix ?? String.Empty; } protected set { keyPrefix = value; } }

        private string keyPrefix;

        /// <summary>
        /// Gives the name of the provider
        /// </summary>
        public string Name { get; private set; }

        /// <summary>Indicates the Basic Flags values that will be common to all keys that are served by this provider.  Currently used to indicate if all keys are Fixed.</summary>
        public ConfigKeyProviderFlags BaseFlags { get; protected set; }

        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given matchType and searchString values.  
        /// If matchType is given as KeyMatchType.MatchAll then the searchString value is ignored.
        /// </summary>
        public abstract string[] SearchForKeys(KeyMatchType matchType, string searchString);

        /// <summary>
        /// Attempts to find information about the specified key and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// returns null if the key was not found or is not supported by this provider.
        /// </summary>
        /// <remarks>
        /// At the IConfigKeyProvider level, this method is typically only called once per fixed or read-only-once key as the config instance may keep prior key access objects and simply return clones of them.
        /// </remarks>
        public abstract IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec);

        /// <summary>
        /// This method allows the caller to update a set of values of the indicated keys to contain the corresponding valueAsString values.
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known
        /// values for this key.  
        /// This method returns the empty string on success or an error code on failure.
        /// </summary>
        /// <remarks>At the IConfigKeyProvider level this method will be invoked for each sub-set of keys that share the same original provider</remarks>
        public virtual string SetValues(KeyValuePair<IConfigKeyAccess, string>[] keyAccessAndValuesPairArray, string commentStr)
        {
            if (BaseFlags.IsFixed)
                return Fcns.CheckedFormat("Invalid: Provider '{0}' does not support Setting key values", Name);
            else
                return Fcns.CheckedFormat("Invalid: Provider '{0}' did not implement the SetValues handler", Name);
        }

        /// <summary>
        /// Override ToString for logging and debugging
        /// </summary>
        public override string ToString()
        {
            return "'{0}' {1}".CheckedFormat(Name, BaseFlags);
        }
    }

    #endregion

    #region DictionaryConfigKeyProvider and derived types: MainArgsConfigKeyProvider, EnvVarsConfigKeyProvider, AppConfigConfigKeyProvider, IncludeFilesConfigKeyProvider

    /// <summary>
    /// This class provides an implementation for ConfigKeyProviders that can be implemented using a dictionary.
    /// </summary>
    public class DictionaryConfigKeyProvider : ConfigKeyProviderBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public DictionaryConfigKeyProvider(string name, bool isFixed)
            : base(name)
        {
            BaseFlags = new ConfigKeyProviderFlags() { IsFixed = isFixed };
        }

        /// <summary>
        /// Dictionary construction helper method.  Populates the dictionary from the given array of name/value pairs, prefixing each given name
        /// with the CommonKeyPrefix.
        /// <para/>Method supports call chaining.
        /// </summary>
        public DictionaryConfigKeyProvider ImportInitialValues(KeyValuePair<string, string> [] nameValuePairArray)
        {
            ConfigKeyAccessFlags flags = new ConfigKeyAccessFlags();

            AddConfigKeyAccessArray(nameValuePairArray.Select((kvp) => new ConfigKeyAccessImpl(KeyPrefix + kvp.Key, flags, null, this) { ValueAsString = kvp.Value }).ToArray());

            return this;
        }

        /// <summary>
        /// implementation method used to add a set of generated ConfigKeyAccessImpl objects to the dictionary.  Used by ImportInitialValues and/or derived types.
        /// </summary>
        protected void AddConfigKeyAccessArray(ConfigKeyAccessImpl[] ckaiArray)
        {
            foreach (var ckai in ckaiArray)
            {
                keyItemDictionary[ckai.Key] = new Item()
                {
                    key = ckai.Key,
                    initialValueAsString = ckai.ValueAsString,
                    ckai = ckai,
                };
            }
        }

        /// <summary>
        /// Gives the internal storage object that is used for the keyItemDictionary.
        /// </summary>
        protected class Item
        {
            /// <summary>The key name</summary>
            public string key;
            /// <summary>The initial value from construction time.</summary>
            public string initialValueAsString;
            /// <summary>The provider reference ConfigKeyAccessImpl object used for this key</summary>
            public ConfigKeyAccessImpl ckai;
            /// <summary>The last comment string given to this key if the dictionary is not fixed.</summary>
            public string comment;
        }

        /// <summary>
        /// The dictionary.  Is actually a key/item dictionary where the Item contains all of the per key information that the class needs to track, and have access to.
        /// </summary>
        protected Dictionary<string, Item> keyItemDictionary = new Dictionary<string, Item>();

        private static readonly string[] emptyStringArray = new string[0];
        private static readonly Item emptyItem = new Item() { key = String.Empty };

        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given matchType and searchString values.  
        /// If matchType is given as KeyMatchType.MatchAll then the searchString value is ignored.
        /// </summary>
        public override string[] SearchForKeys(KeyMatchType matchType, string searchString)
        {
            searchString = searchString ?? String.Empty;

            switch (matchType)
            {
                case KeyMatchType.MatchAll:
                    return keyItemDictionary.Keys.ToArray();
                case KeyMatchType.MatchExact:
                    if (keyItemDictionary.ContainsKey(searchString))
                        return new string [] { searchString };
                    else 
                        return emptyStringArray;
                case KeyMatchType.MatchPrefix:
                    {
                        List<string> matchList = new List<string>();

                        foreach (string key in keyItemDictionary.Keys)
                        {
                            if (key.StartsWith(searchString))
                                matchList.Add(key);
                        }

                        return matchList.ToArray();
                    }

                default:
                    return emptyStringArray;
            }
        }

        /// <summary>
        /// Attempts to find information about the given key and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// returns null if the key was not found or is not supported by this provider.
        /// </summary>
        public override IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec)
        {
            Item item = null;
            string key = ((keyAccessSpec != null) ? keyAccessSpec.Key : null) ?? String.Empty;

            keyItemDictionary.TryGetValue(key, out item);

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
        public override string SetValues(KeyValuePair<IConfigKeyAccess, string>[] keyAccessAndValuesPairArray, string commentStr)
        {
            if (BaseFlags.IsFixed)
                return Fcns.CheckedFormat("Invalid: Provider '{0}' does not support Setting key values", Name);

            int numChangedItems = 0;
            string firstError = null;

            foreach (var kvp in keyAccessAndValuesPairArray)
            {
                Item item = null;
                IConfigKeyAccess kvpIcka = kvp.Key;
                string key = (kvpIcka != null ? kvpIcka.Key : null);
                if (key == null)
                    firstError = firstError ?? "Internal: Encountered null key spec";
                else if (!key.StartsWith(KeyPrefix))
                    firstError = firstError ?? Fcns.CheckedFormat("Internal: key:'{0}' is not valid for use with provider:'{1}', required key prefix:'{2}'", kvpIcka.Key, Name, KeyPrefix);
                else if (!keyItemDictionary.TryGetValue(key ?? String.Empty, out item) || item == null || item.ckai == null)
                {
                    item = new Item() { key = key, ckai = new ConfigKeyAccessImpl(key, kvpIcka.Flags, null, this) { ValueAsString = kvp.Value }, initialValueAsString = kvp.Value, comment = commentStr };
                    keyItemDictionary[key] = item;

                    numChangedItems++;

                    Logger.Debug.Emit("{0}: added new key:'{1}' value:'{2}'", Name, key, kvp.Value);
                }
                else
                {
                    if (item.ckai.ProviderFlags.IsFixed)
                        firstError = firstError ?? Fcns.CheckedFormat("Internal: key:'{0}' is fixed in provider:'{1}'", kvpIcka.Key, Name);
                    else
                    {
                        string entryValueAsString = item.ckai.ValueAsString;

                        item.ckai.ValueAsString = kvp.Value;
                        item.comment = commentStr;

                        numChangedItems++;

                        if (entryValueAsString == item.initialValueAsString)
                            Logger.Debug.Emit("{0}: key:'{1}' value changed to '{2}' [from initial value:'{3}']", Name, key, kvp.Value, item.initialValueAsString);
                        else
                            Logger.Debug.Emit("{0}: key:'{1}' value changed to '{2}' [from:'{3}', initial:'{4}']", Name, key, kvp.Value, entryValueAsString, item.initialValueAsString);
                    }
                }
            }

            if (numChangedItems > 0)
                firstError = firstError ?? HandleSetValuesChangedContents();

            return firstError ?? String.Empty;
        }

        /// <summary>
        /// Method that may be overriden in derived objects to persist changes to the dictionary
        /// </summary>
        protected virtual string HandleSetValuesChangedContents() { return String.Empty; }
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
        public MainArgsConfigKeyProvider(string name, ref string[] mainArgs, string keyPrefix)
            : base(name, true)
        {
            KeyPrefix = keyPrefix;

            if (mainArgs != null)
            {
                List<string> unusedArgsList = new List<string>() { };
                List<KeyValuePair<string, string>> generatedKeyValuePairList = new List<KeyValuePair<string, string>>(); 

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
                        generatedKeyValuePairList.Add(new KeyValuePair<string, string>(key, value));
                    }
                }

                if (unusedArgsList.Count != mainArgs.Length)
                {
                    mainArgs = unusedArgsList.ToArray();
                }

                ImportInitialValues(generatedKeyValuePairList.ToArray());
            }
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
        public EnvVarsConfigKeyProvider(string name, string keyPrefix)
            : base(name, true)
        {
            KeyPrefix = keyPrefix;

            List<KeyValuePair<string, string>> generatedKeyValuePairList = new List<KeyValuePair<string, string>>();

            IDictionary envVarIDictionary = System.Environment.GetEnvironmentVariables();
            foreach (object keyObject in envVarIDictionary.Keys)
            {
                object valueObject = envVarIDictionary[keyObject];
                string keyStr = keyObject as string;
                string valueStr = valueObject as string;

                if (keyStr != null && valueStr != null)
                {
                    generatedKeyValuePairList.Add(new KeyValuePair<string,string>(KeyPrefix + keyStr, valueStr));
                }
            }

            ImportInitialValues(generatedKeyValuePairList.ToArray());
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
        public AppConfigConfigKeyProvider(string name, string keyPrefix)
            : base(name, true)
        {
            KeyPrefix = keyPrefix;

            Dictionary<string, string> generatedKeyValueDictionary = new Dictionary<string, string>();

            appSettings = System.Configuration.ConfigurationManager.AppSettings;

            foreach (string key in appSettings.AllKeys)
            {
                if (!generatedKeyValueDictionary.ContainsKey(key))
                {
                    string[] valuesArray = appSettings.GetValues(key) ?? emptyKeyValuesArray;

                    appSettingsDictionary[key] = valuesArray;
                    generatedKeyValueDictionary[key] = valuesArray.SafeAccess(0, String.Empty);
                }
            }

            ImportInitialValues(generatedKeyValueDictionary.ToArray());
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
        public IncludeFilesConfigKeyProvider(string name, string searchPrefix, IConfig config, string keyPrefix)
            : base(name, true)
        {
            KeyPrefix = keyPrefix;

            string[] includeKeysArray = config.SearchForKeys(KeyMatchType.MatchPrefix, searchPrefix);

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

                        string validationResult = ((keyItem.Validator != null) ? keyItem.Validator.Validate(keyItem.Value) : String.Empty);
                        if (!String.IsNullOrEmpty(validationResult))
                        {
                            Logger.Debug.Emit("Key '{0}' Value:'{1}' failed validation:'{2}'", fullKey, keyItem.Value, validationResult);
                        }
                        if (!includeFilesKeysDictionary.ContainsKey(fullKey))
                        {
                            includeFilesKeysDictionary[fullKey] = new ConfigKeyAccessImpl(fullKey, flags, null, this) { ValueAsString = keyItem.Value };
                        }
                        else
                        {
                            Logger.Warning.Emit("Redundant value found for key '{0}' under path '{1}'", fullKey, includePath);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.Debug.Emit("Include file load from '{0}' w/Prefix:'{1}' failed: {2}", includePath, keyPrefix, ex);
                }
            }

            AddConfigKeyAccessArray(includeFilesKeysDictionary.Values.ToArray());
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
        /// Constructor:  
        /// </summary>
        public IniFileConfigKeyProvider(string name, string filePath, string keyPrefix, bool isReadWrite)
            : base(name, !isReadWrite)
        {
            if (isReadWrite)
                BaseFlags = new ConfigKeyProviderFlags(BaseFlags) { MayBeChanged = true, IsPersisted = true };

            ConfigKeyAccessFlags defaultAccessFlags = new ConfigKeyAccessFlags();

            KeyPrefix = keyPrefix;

            givenFilePath = filePath;
            fullFilePath = System.IO.Path.GetFullPath(filePath);

            try
            {
                rawFileLines = System.IO.File.ReadAllLines(fullFilePath);
            }
            catch (System.Exception ex)
            {
                Logger.Error.Emit("Unable to read INI file contents from File '{0}': {1}", fullFilePath, ex.ToString());
            }

            trimmedFileLines = rawFileLines.Select((line) => line.Trim()).ToArray();

            SectionItem currentSection = null;

            for (int lineIdx = 0; lineIdx < trimmedFileLines.Length; lineIdx++)
            {
                int lineNum = lineIdx + 1;
                string trimmedLine = trimmedFileLines[lineIdx];
                string rawFileLine = rawFileLines[lineIdx];

                if (trimmedLine.StartsWith("["))
                {
                    if (trimmedLine.EndsWith("]"))
                    {
                        currentSection = new SectionItem() { lineIdx = lineIdx, sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2) };
                        sectionItemList.Add(currentSection);
                    }
                    else
                    {
                        Logger.Debug.Emit(@"File '{0}' lineNum:{1} Invalid section header:'{2}'", fullFilePath, lineNum, trimmedLine);
                    }
                }
                else if (trimmedLine.Length == 0)
                {
                    // the line is a blank line
                }
                else if (trimmedLine.StartsWith(@"#") || trimmedLine.StartsWith(@"//") || trimmedLine.StartsWith(@";"))
                {
                    // the line is a comment line
                }
                else
                {
                    string[] linePartsArray = rawFileLine.Split(new char[] { '=' }, 2);

                    string fileKeyName = linePartsArray.SafeAccess(0, String.Empty).Trim();

                    ValueItem valueItem = new ValueItem() { lineIdx = lineIdx, section = currentSection, fileKeyName = fileKeyName, hasEquals = linePartsArray.Length >= 2 };

                    if (currentSection != null)
                        valueItem.ckai = new ConfigKeyAccessImpl("{0}{1}.{2}".CheckedFormat(KeyPrefix, currentSection.sectionName, valueItem.fileKeyName), defaultAccessFlags, null, this);
                    else
                        valueItem.ckai = new ConfigKeyAccessImpl("{0}{1}".CheckedFormat(KeyPrefix, valueItem.fileKeyName), defaultAccessFlags, null, this);

                    if (valueItem.hasEquals)
                        valueItem.ckai.ValueAsString = linePartsArray.SafeAccess(1, String.Empty);
                    else
                        valueItem.ckai.ValueAsString = null;

                    valueItemList.Add(valueItem);
                }
            }

            AddConfigKeyAccessArray(valueItemList.Select((vItem) => vItem.ckai).ToArray());
        }

        string givenFilePath = null;
        string fullFilePath = null;
        string[] rawFileLines = new string[0];
        string[] trimmedFileLines = new string [0];
        string[] savedLines = null;

        List<SectionItem> sectionItemList = new List<SectionItem>();
        List<ValueItem> valueItemList = new List<ValueItem>();

        class SectionItem
        {
            public int lineIdx = -1;
            public string sectionName = String.Empty;
        }

        class ValueItem
        {
            public SectionItem section;

            public int lineIdx = -1;

            public string fileKeyName;
            public bool hasEquals;

            public ConfigKeyAccessImpl ckai;
        }

        /// <summary>
        /// This method will attempt to rewrite the given INI file path
        /// </summary>
        protected override string HandleSetValuesChangedContents()
        {
            string[] linesToSave = rawFileLines;

            foreach (ValueItem valueItem in valueItemList)
            {
                ConfigKeyAccessImpl ckai = valueItem.ckai;
                if (valueItem.hasEquals || ckai.HasValue)
                {
                    if (ckai.HasValue)
                        linesToSave[valueItem.lineIdx] = "{0}={1}".CheckedFormat(valueItem.fileKeyName, ckai.ValueAsString ?? String.Empty);
                    else
                        linesToSave[valueItem.lineIdx] = valueItem.fileKeyName;
                }
            }

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

    #region ConfigKeyFile and ConfigKeyStore

    /// <summary>
    /// Class used to define the file content/format for ConfigKeyFiles.  
    /// This is used for Include files and this is the base type used by the ConfigKeyStore class which is the current default implementation for persisting non-fixed config key values.
    /// </summary>
    [DataContract(Namespace = Constants.ConfigNameSpace)]
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
    [DataContract(Namespace = Constants.ConfigNameSpace)]
    public class ConfigKeyStore : ConfigKeyFile, Persist.IPersistSequenceable
    {
        #region IPersistSequenceable Members

        /// <summary>Property is required by IPersistSequenceable interface.  Used to determine which of a set of loaded file contents is the most recent (highest sequence number value).</summary>
        [DataMember(Order = 10, Name = "SeqNum", IsRequired=false)]
        public ulong PersistedVersionSequenceNumber { get; set; }

        #endregion
    }

    /// <summary>Proxy class, derived from List{KeyItem}, used to allow Xml element naming to be customized.</summary>
    [CollectionDataContract(Namespace = Constants.ConfigNameSpace, Name = "Set", ItemName = "Item")]
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
    [DataContract(Namespace = Constants.ConfigNameSpace)]
    [KnownType(typeof(Int32Validator))]
    [KnownType(typeof(DoubleValidator))]
    [KnownType(typeof(EnumValidator))]
    public class KeyItem
    {
        /// <summary>Gives the full, or partial, key name of the key for this KeyItem.  May be partial name in include files (ConfigKeyFile).  Always full key in ConfigKeyStore instances and files.</summary>
        [DataMember(Order = 10)]
        public string Key { get; set; }

        /// <summary>Value property give the key's value (default, current, or fixed) formatted as a string.</summary>
        [DataMember(Order = 100)]
        public string Value { get; set; }

#if (false) 
        // older Flags are on hold for now
        /// <summary>Optional property allows the KeyItem to define usage specific flags.  When not defined, the value Fixed is used.  Explicitly setting the key Flags to Normal, or Default, indicates that the key is settable.</summary>
        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
#endif

        /// <summary>Optional meta-data property allows an include file to define the string name of the object type that is contained in the Value property.  Not currently used</summary>
        [DataMember(Order = 210, IsRequired = false, EmitDefaultValue = false)]
        public string Type { get; set; }

        /// <summary>Optional IKeyValueValidator instance property.  May currently be defined as an Int32Validatior, DoubleValidator or EnumValidator to support configurable value validation logic.  Not currently used</summary>
        [DataMember(Order = 220, IsRequired = false, EmitDefaultValue = false)]
        public IKeyValueValidator Validator { get; set; }

        /// <summary>Optional meta-data property to give a description of the key for documentation and assistance purposes</summary>
        [DataMember(Order = 1000, IsRequired = false, EmitDefaultValue = false)]
        public string Description { get; set; }

        /// <summary>Optional property that may be provided as a comment.  Generally intended for use when saving a new (overlay) key value into the currently configured Persist storage system.</summary>
        [DataMember(Order = 1010, IsRequired = false, EmitDefaultValue = false)]
        public string Comment { get; set; }

        /// <summary>Debugging assistant method - gives quick view of item contents</summary>
        public override string ToString()
        {
            string TypeStr = (String.IsNullOrEmpty(Type) ? String.Empty : " Type:" + Type);
            string DescStr = (String.IsNullOrEmpty(Description) ? String.Empty : " Desc:" + Description);

            return Fcns.CheckedFormat("Key:'{0}' Value:'{1}'{2}{3}", Key, Value, TypeStr, DescStr);
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
