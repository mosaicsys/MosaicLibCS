//-------------------------------------------------------------------
/*! @file Config.cs
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
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Collections;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using MosaicLib.Utils.StringMatching;

namespace MosaicLib.Modular.Config
{
    #region ConfigKeyAccessFlags, ConfigKeyProviderFlags

    /// <summary>
    /// This struct contains values that define specific details about how a client wants to access a specific config key.
    /// <para/>Supported concepts include ReadOnlyOnce (default), MayBeChanged, Optional, Required (default), Silent, EnsureExists, DefaultProvider
    /// </summary>
    public struct ConfigKeyAccessFlags
    {
        /// <summary>Copy constructor.  Often used with property initializers.</summary>
        public ConfigKeyAccessFlags(ConfigKeyAccessFlags other) 
            : this()
        {
            MayBeChanged = other.MayBeChanged;
            IsOptional = other.IsOptional;
            silenceIssues = other.silenceIssues;
            SilenceLogging = other.SilenceLogging;
            EnsureExists = other.EnsureExists;
            DefaultProviderName = other.DefaultProviderName;
        }

        /// <summary>Flag value indicates that the config key is Required and that it will only be read once, typically early in the application launch cycle.  If it its value is changed later, the application must be restarted to begin using the new (latest) value.</summary>
        public bool ReadOnlyOnce { get { return !MayBeChanged; } set { MayBeChanged = !value; } }

        /// <summary>Value that indicates that the config key may accept changes during Update calls.</summary>
        public bool MayBeChanged { get; set; }

        /// <summary>Flag value indicates that the config key is optional.  No error or siginficant issue should be reported on an attempt to ready this key when it is not present.</summary>
        public bool IsOptional { get; set; }

        /// <summary>Flag value indicates that the config key is Required.  An Issue log message will be generated if no provider supports this key.</summary>
        public bool IsRequired { get { return !IsOptional; } set { IsOptional = !value; } }

        /// <summary>Set this flag to true to block emitting issue messages for this config key</summary>
        public bool SilenceIssues { get { return (silenceIssues || SilenceLogging); } set { silenceIssues = value; } }
        private bool silenceIssues;

        /// <summary>Set this flag to prevent any logging related to this config key access</summary>
        public bool SilenceLogging { get; set; }

        /// <summary>When true, this property indicates that the client would like the config instance or provider instance to attempt to create the key if it does not already exist.</summary>
        public bool? EnsureExists { get; set; }

        /// <summary>When non-empty and when EnsureExists is selected, this property allows the client to indicate the name of the default provider which the client would like to use to create the key if it does not already exist.</summary>
        public string DefaultProviderName { get; set; }

        [Obsolete("Use of this property is no longer supported.  Please use the ConfigKeyAccessSpec.MetaData property instead (2017-11-11)")]
        public INamedValueSet NVS { get { return NamedValueSet.Empty; } set { } }

        /// <summary>ToString override for debugging and logging.  Gives a string that summarizes the flag values indicated by this </summary>
        public override string ToString()
        {
            return Fcns.CheckedFormat("{0}{1}{2}{3}{4}", 
                                        (ReadOnlyOnce ? "RdOnce" : "CanChange"), 
                                        (IsOptional ? "+Opt" : "+Req"), 
                                        (SilenceLogging ? "-Logging" : (SilenceIssues ? "-Issues" : String.Empty)),
                                        ((EnsureExists ?? false) ? "+EnsureExists" : ""), 
                                        (DefaultProviderName.IsNullOrEmpty() ? "" : " DefaultProvider:{0}".CheckedFormat(DefaultProviderName)));
        }
    }

    /// <summary>
    /// This struct contains values that define specific details about how a provider supports keys (either a specific one or all of them in general)
    /// <para/>Supported concepts include MayBeChanged, Fixed, KeyNotFound
    /// </summary>
    /// <remarks>
    /// This object is used both at the provider level to define characteristics of this provider in general.  
    /// It is also used to annotate each key served by the provider to indicate key specific details/status as given by the provider (or the IConfig on which the key is being accessed)
    /// </remarks>
    public struct ConfigKeyProviderFlags
    {
        /// <summary>Copy constructor.  Often used with property initializers</summary>
        public ConfigKeyProviderFlags(ConfigKeyProviderFlags rhs)
            : this()
        {
            IsFixed = rhs.IsFixed;
            IsPersisted = rhs.IsPersisted;
            keysMayBeAddedUsingEnsureExistsOption = rhs.keysMayBeAddedUsingEnsureExistsOption;
            KeyWasNotFound = rhs.KeyWasNotFound;
        }

        /// <summary>Flag is set by a provider to indicate that the value will not change from its initial value.</summary>
        public bool IsFixed { get; set; }

        /// <summary>Flag is set by a provider to indicate that the value may be change using the SetValue(s) method(s).</summary>
        public bool MayBeChanged { get { return !IsFixed; } set { IsFixed = !value; } }

        /// <summary>
        /// Provider level flag indicates if the provider allows keys to be added by using the GetConfigKeyAccess's ensureExists option.  This flag is only supported by providers that are not fixed.
        /// </summary>
        public bool KeysMayBeAddedUsingEnsureExistsOption { get { return (keysMayBeAddedUsingEnsureExistsOption && !IsFixed); } set { keysMayBeAddedUsingEnsureExistsOption = value; } }
        private bool keysMayBeAddedUsingEnsureExistsOption;

        /// <summary>The provider sets this to true if changes to a key will be peristed.</summary>
        public bool IsPersisted { get; set; }

        /// <summary>Flags is set by the provider in individual IConfigKeyAccess items to indicate that the requested key was not found</summary>
        public bool KeyWasNotFound { get; set; }

        /// <summary>ToString override for debugging and logging</summary>
        public override string ToString()
        {
            return Fcns.CheckedFormat("{0}{1}{2}", (IsFixed ? "Fixed" : "MayBeChanged"), (KeyWasNotFound ? "+NotFound" : String.Empty), (KeysMayBeAddedUsingEnsureExistsOption ? "+CanUseEE" : String.Empty));
        }

        /// <summary>
        /// Returns a ConfigKeyProviderFlags object that is the logical or of the contents of this one and the contents of the given rhs.
        /// <para/>MayBeChanged is the logical and as it is simply the inverse of IsFixed which is logically ored when merging these two struct contents.
        /// </summary>
        public ConfigKeyProviderFlags MergeWith(ConfigKeyProviderFlags rhs)
        {
            IsFixed |= rhs.IsFixed;
            IsPersisted |= rhs.IsPersisted;
            KeyWasNotFound |= rhs.KeyWasNotFound;
            return this;
        }
    }

    #endregion

    #region ExtensionMethods - helpers for use with IConfig, IConfigKeyAccess, and IConfigKeyProvider objects.

    /// <summary>Extension methods to be used with ConfigKeyAccessFlags and IConfigKeyAccess objects</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Attempts to find information about the given key and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// If the key does not exist then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and null for the ValueAsString and this object is returned.
        /// <para/>Uses ConfigKeyAccessFlags.Optional for the flags.
        /// </summary>
        public static IConfigKeyAccess GetConfigKeyAccess(this string key)
        {
            return Config.Instance.GetConfigKeyAccess(key);
        }

        /// <summary>
        /// Attempts to find information about the given key and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// If the key does not exist then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and null for the ValueAsString and this object is returned.
        /// </summary>
        public static IConfigKeyAccess GetConfigKeyAccess(this IConfig config, string key, bool isOptional = true, bool mayBeChanged = true, bool? ensureExists = null, string defaultProviderName = null, INamedValueSet keyMetaData = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate, ValueContainer? defaultValue = null)
        {
            return config.GetConfigKeyAccess(new ConfigKeyAccessSpec(key, new ConfigKeyAccessFlags() { IsOptional = isOptional, MayBeChanged = mayBeChanged, EnsureExists = ensureExists, DefaultProviderName = defaultProviderName }, keyMetaData: keyMetaData, mergeBehavior: mergeBehavior), defaultValue: defaultValue);
        }

        /// <summary>
        /// Attempts to find information from the given provider about the given key and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// If the provider does not support this key then the provider will return null.
        /// <para/>Uses ConfigKeyAccessFlags.Optional for the flags.
        /// </summary>
        public static IConfigKeyAccess GetConfigKeyAccess(this IConfigKeyProvider provider, string key, bool isOptional = true, bool mayBeChanged = true, bool? ensureExists = null, string defaultProviderName = null, INamedValueSet keyMetaData = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate, ValueContainer defaultValue = default(ValueContainer))
        {
            return provider.GetConfigKeyAccess(new ConfigKeyAccessSpec(key, new ConfigKeyAccessFlags() { IsOptional = isOptional, MayBeChanged = mayBeChanged, EnsureExists = ensureExists, DefaultProviderName = defaultProviderName }, keyMetaData: keyMetaData, mergeBehavior: mergeBehavior), initialValue: defaultValue);
        }

        /// <summary>
        /// Attempts to find information about the given key and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// If the key does not exist then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and null for the ValueAsString and this object is returned.
        /// <para/>Uses ConfigKeyAccessFlags.ReadOnlyOnce | ConfigKeyAccessFlags.Optional for the flags.
        /// </summary>
        public static IConfigKeyAccess GetConfigKeyAccessOnce(this IConfig config, string key, bool isOptional = true, bool? ensureExists = null, string defaultProviderName = null, INamedValueSet keyMetaData = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate, ValueContainer? defaultValue = null)
        {
            return config.GetConfigKeyAccess(key: key, isOptional: isOptional, mayBeChanged: false, ensureExists: ensureExists, defaultProviderName: defaultProviderName, keyMetaData: keyMetaData, mergeBehavior: mergeBehavior, defaultValue: defaultValue);
        }

        /// <summary>
        /// Extension method to get a typed value from a IConfigKeyAccess object.  
        /// Returns the key's value parsed as the given type if the key exists and could be parsed successfully.
        /// Returns the given defaultValue in all other cases.
        /// Updates the keyAccess's ResultCode field to the empty string on success or to a description of the failure reason on failure (if possible)
        /// Throws ValueContainerGetValueException if the key's contained value cannot be converted to the desired type and the user has passed rethrow as true.
        /// </summary>
        /// <exception cref="MosaicLib.Modular.Common.ValueContainerGetValueException">May be thrown if the contained value cannot be converted to the desired type and the caller has passed rethrow = true</exception>
        public static ValueT GetValue<ValueT>(this IConfigKeyAccess keyAccess, ValueT defaultValue = default(ValueT), bool rethrow = false)
        {
            string key = ((keyAccess != null) ? keyAccess.Key : String.Empty);
            string methodName = Fcns.CheckedFormat("{0}<{1}>(key:'{2}', default:'{3}')", Fcns.CurrentMethodName, typeof(ValueT), key, defaultValue);

            ValueT value;

            if (keyAccess != null && Config.TryGetValue(methodName, keyAccess, out value, defaultValue, rethrow))
                return value;
            else
                return defaultValue;
        }

        /// <summary>
        /// Extension method to attempt get a typed value from a IConfigKeyAccess object.  
        /// Returns assignes the key's parsed value to the value output parmeter and returns true if the key exists and could be parsed successfully.
        /// Returns assigns the given defaultValue to the value output parameter and returns false in all other cases.
        /// Updates the keyAccess's ResultCode field to the empty string on success or to a description of the failure reason on failure (if possible)
        /// </summary>
        public static bool TryGetValue<ValueT>(this IConfigKeyAccess keyAccess, out ValueT value, ValueT defaultValue = default(ValueT))
        {
            string key = ((keyAccess != null) ? keyAccess.Key : String.Empty);
            string methodName = Fcns.CheckedFormat("{0}<{1}>(key:'{2}', default:'{3}')", Fcns.CurrentMethodName, typeof(ValueT), key, defaultValue);

            if (keyAccess != null)
            {
                return Config.TryGetValue(methodName, keyAccess, out value, defaultValue, false);
            }
            else
            {
                value = defaultValue;
                return false;
            }
        }

        /// <summary>
        /// Convienience extension method for use in setting the keyAccess's keys value and then updatinging the keyAccess to reflect the new value.
        /// Attempts to assign the given key's value from the valueContainer.  Returns empty string on success or a description of the failure reason on failure.
        /// </summary>
        public static string SetValue(this IConfigKeyAccess keyAccess, ValueContainer valueContainer, string commentStr = "", bool autoUpdate = true)
        {
            ConfigKeyAccessImpl ckai = keyAccess as ConfigKeyAccessImpl;
            IConfig configInstance = ((ckai != null) ? (ckai.ConfigInternal as IConfig) : null) ?? Config.Instance;

            if (keyAccess != null)
            {
                string ec = configInstance.SetValue(keyAccess, valueContainer, commentStr);
                if (autoUpdate)
                    keyAccess.UpdateValue();
                return ec;
            }
            else
                return "keyAccess parameter was given as null";
        }

        /// <summary>
        /// Convienience extension method for use in setting the keyAccess's keys value and then updatinging the keyAccess to reflect the new value.
        /// Attempts to assign the given key's value from the valueContainer.  Returns empty string on success or a description of the failure reason on failure.
        /// </summary>
        public static string SetValue(this IConfigKeyAccess keyAccess, object value, string commentStr = "", bool autoUpdate = true)
        {
            ConfigKeyAccessImpl ckai = keyAccess as ConfigKeyAccessImpl;
            IConfig configInstance = ((ckai != null) ? (ckai.ConfigInternal as IConfig) : null) ?? Config.Instance;

            if (keyAccess != null)
            {
                string ec = configInstance.SetValue(keyAccess, new ValueContainer(value), commentStr);
                if (autoUpdate)
                    keyAccess.UpdateValue();
                return ec;
            }
            else
                return "keyAccess parameter was given as null";
        }
    }

    #endregion

    #region IConfig, IConfigSubscrition, IConfigKeyGetSet, KeyMatchType

    /// <summary>
    /// Top level interface specification for Modular config key specific methods.
    /// <para/>Supports interfaces: IConfigSubscription, and IConfigKeyGetSet
    /// <para/>Supports properties/methods: Supports AddProvider, IssueEmitter, and TraceEmitter
    /// </summary>
    public interface IConfig : IConfigSubscription, IConfigKeyGetSet
    {
        #region Provider related methods

        /// <summary>
        /// This method attempts to add this given provider to the set that are used by this instance.
        /// <para/>Recommended practice is that each provider use a unique Name and that they provide non-overlapping sets of keys.  
        /// However no logic enforces, nor strictly requires, that either condition be true.
        /// </summary>
        void AddProvider(IConfigKeyProvider provider);

        /// <summary>
        /// This property generates and returns an array of information about the currently registered providers.
        /// </summary>
        IConfigKeyProviderInfo[] Providers { get; }

        /// <summary>
        /// When this value is non-empty, ConfigKeyGetSet interface's SetValues method will attempt to find and use the indicated provider when setting a value for a key that was not previously found.
        /// </summary>
        string DefaultProviderName { get; set; }

        #endregion

        #region IMesgEmitters for logging (Issues and Trace)

        /// <summary>Gives client (and extension methods) access to the currently selected emitter that is to be used to emit messages about config instance specific issues (errors)</summary>
        Logging.IMesgEmitter IssueEmitter { get; }

        /// <summary>Gives client (and extension methods) access to the currently selected trace emitter that is used to emit trace messages fro the config instance.</summary>
        Logging.IMesgEmitter TraceEmitter { get; }

        #endregion
    }

    /// <summary>
    /// Defines the interface that is used by clients to track when one or more config key values may have been updated.
    /// </summary>
    public interface IConfigSubscription
    {
        #region Subscription related elements of interface

        /// <summary>This is the list of notifiyable objects that will be informed whenever any seqNum in the ConfigSubscriptionSeqNums may have changed.  Client is required to consult the contents of that object to determine what might have changed.</summary>
        IBasicNotificationList ChangeNotificationList { get; }

        /// <summary>Returns the ConfigSubscriptionSeqNums containing the most recently produced sequence number values from each source.</summary>
        ConfigSubscriptionSeqNums SeqNums { get; }

        /// <summary>Returns the ChangeSeqNum property value from SeqNums</summary>
        int ChangeSeqNum { get; }

        /// <summary>This property is true when any ReadOnce config key's value is not Fixed and it has been changed and no longer matches the original value that was read for it.</summary>
        bool ReadOnceConfigKeyHasBeenChangedSinceItWasRead { get; }

        #endregion
    }

    /// <summary>
    /// Container structure for various IConfigSubscription related sequence number values.
    /// The use a struct support simple default and copy constructors.  This type implements IEquatable{ConfigSubscriptionSeqNums} to test for change.
    /// <para/>ChangeSeqNum, EnsureExistsSeqNum, KeyMetaDataChangeSeqNum
    /// </summary>
    public struct ConfigSubscriptionSeqNums : IEquatable<ConfigSubscriptionSeqNums>
    {
        /// <summary>This sequence number counts the number of times that the one or more config key values have changed</summary>
        public Int32 ChangeSeqNum { get; set; }

        /// <summary>This sequence number counts the number of times that a key has been added to a provider due to the use of the EnsureExists attribute.</summary>
        public Int32 EnsureExistsSeqNum { get; set; }

        /// <summary>This sequence number counts the number of times that a key has been found or created.</summary>
        public Int32 KeyAddedSeqNum { get; set; }

        /// <summary>This sequence number counts the number of times that an existing config key's meta-data values have been changed by merging (adding new keys) with previously defined values.</summary>
        public Int32 KeyMetaDataChangeSeqNum { get; set; }

        /// <summary>Implements IEquatable interface.  Indicates whether the current object is equal to another object of the same type.</summary>
        public bool Equals(ConfigSubscriptionSeqNums other)
        {
            return (ChangeSeqNum == other.ChangeSeqNum && EnsureExistsSeqNum == other.EnsureExistsSeqNum && KeyAddedSeqNum == other.KeyAddedSeqNum && KeyMetaDataChangeSeqNum == other.KeyMetaDataChangeSeqNum);
        }

        /// <summary>
        /// Debugging and logging helper method
        /// </summary>
        public override string ToString()
        {
            return "Chg:{0} EE:{1} KeyAdd:{2} KeyMDChg:{3}".CheckedFormat(ChangeSeqNum, EnsureExistsSeqNum, KeyAddedSeqNum, KeyMetaDataChangeSeqNum);
        }
    }

    /// <summary>
    /// This interface defines the details about any ConfigBase type of instance that may be used by IConfigKeyAccess methods to implement internal behavior
    /// </summary>
    internal interface IConfigInternal
    {
        /// <summary>
        /// This method will attempt to get, and update, the current value for the given configKeyAccess from the KeySource it was previously found in.  
        /// Only keys that have been successfully found from a source can be updated.
        /// Returns true if any aspect of the given key access was changed, otherwise returns false.
        /// </summary>
        /// <remarks>
        /// This method is intended for internal use only.  
        /// It assumes that the icka given is non-null, not ValueIsFixed.  As such it is assumed to have been previously found.
        /// </remarks>
        bool TryUpdateConfigKeyAccess(string methodName, IConfigKeyAccess icka, bool suppressLogging = false);

        /// <summary>Returns the ConfigSubscriptionSeqNums containing the most recently produced sequence number values from each source.</summary>
        ConfigSubscriptionSeqNums SeqNums { get; }
    }

    /// <summary>
    /// Defines the interface used by clients to obtain values from config keys accessed by a key string.
    /// </summary>
    public interface IConfigKeyGetSet
    {
        /// <summary>
        /// Gets/Sets the entire set of name mappings as an enumeration.  
        /// This set of mappings is used by GetConfigKeyAccess to support mapping from the given key to an alternate key on a case by case basis.  
        /// This mapping table may be used to allow two (or more) entities to end up using the same key accesd even if they do not know about each other in advance.
        /// </summary>
        IEnumerable<Modular.Common.IMapNameFromTo> MapNameFromToSet { get; set; }

        /// <summary>
        /// Adds the given set of IMapNameFromTo items to the current mapping MapNameFromToSet.
        /// </summary>
        IConfigKeyGetSet AddRange(IEnumerable<Modular.Common.IMapNameFromTo> addMapNameFromToSet);

        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given MatchRuleSet and searchPredicate values.
        /// MatchRuleSet is used to filter the keys based on their names while searchPredicate may be used to filter the returned keys based on their MetaData contents
        /// null matchRulesSet and/or searchPredicate disables filtering based on that parameter.  If both are null then all known keys will be returned.
        /// The array returned is contains the union of the corresponding sorted arrays returned by each provider.
        /// </summary>
        string[] SearchForKeys(MatchRuleSet matchRuleSet = null, ConfigKeyFilterPredicate searchPredicate = null);

        /// <summary>
        /// Attempts to find information about the given spec and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// If the key does not exist but the client has requested EnsureExists, and an appropriate provider can be found under which to create it then it will be created using the given <paramref name="defaultValue"/> as its initial value.
        /// If the key does not exist and the client has not requested EnsureExists then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and using the given <paramref name="defaultValue"/>.
        /// </summary>
        IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec, ValueContainer ? defaultValue = null);

        /// <summary>
        /// This method allows the caller to update the persisted value for a specific key (if this is supported for the given key).
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known
        /// values for this key.  
        /// This method returns the empty string on success or an error code on failure.
        /// <para/>This method does not update the given keyAccess's value.  keyAccess is only used to define the key to use along with the pertinant value of its flags (primarily for SilenceIssues)
        /// </summary>
        string SetValue(IConfigKeyAccess keyAccess, ValueContainer valueContainer, string commentStr);

        /// <summary>
        /// This method allows the caller to attempt to update a set of values of the indicated keys to contain the corresponding ValueContainer values.
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known
        /// values for this key.  
        /// This method returns the empty string on success or an error code on failure.  
        /// On failure, all keys that could be updated will have been and the returned error code will be from the first key that could not be updated.
        /// <para/>This method does not update any given keyAccess's value.  keyAccess is only used to define the key to use along with the pertinant value of its flags (primarily for SilenceIssues)
        /// </summary>
        string SetValues(KeyValuePair<IConfigKeyAccess, ValueContainer>[] keyAccessAndValuesPairArray, string commentStr);
    }

    /// <summary>
    /// Predicate Delegate that may be used to determine if the contents of a given key and INamedValueSet meet, or adhear to, a caller's specific constraints.
    /// This delegate is generally used with the SearchForKeys method.
    /// </summary>
    public delegate bool ConfigKeyFilterPredicate(string key, INamedValueSet metaDat, ValueContainer vc);

    #endregion

    #region IConfigKeyAccessSpec, IConfigKeyAccessProviderInfo, IConfigKeyAccess, ConfigKeyAccessSpec, ToStringDetailLevel

    /// <summary>
    /// This interface defines a config key name and the access specific flags that the client wishes to use when accessing this key
    /// <para/>The <seealso cref="ConfigKeyAccessSpec"/> class is available as an implementation class that may be used by clients when desired
    /// </summary>
    public interface IConfigKeyAccessSpec
    {
        /// <summary>Gives the full key name for this item</summary>
        string Key { get; }

        /// <summary>Gives the client access to the set of Flags that are relevant for access to this config key.</summary>
        ConfigKeyAccessFlags Flags { get; }

        /// <summary>
        /// Returns a readonly copy of the client provided key meta data INamedValueSet for this key spec.  
        /// When getting a config key accessor, if this property is non-empty then it will be merged into the root CKA's key meta data using the given MergeBehavior
        /// </summary>
        INamedValueSet MetaData { get; }

        /// <summary>This property is used when combining client provided key MetaData (above) into previously specified key meta data from other sources in the root CKA instance.  Defaults to AddAndUpdate</summary>
        NamedValueMergeBehavior MergeBehavior { get; }
    }

    /// <summary>
    /// This interface defines information from the provider that is serving this key.
    /// </summary>
    public interface IConfigKeyAccessProviderInfo
    {
        /// <summary>
        /// Gives information (Name, BaseFlags) about the provider that is serving this key.  
        /// Also used internally to obtain the provider instance that is used to serve this key.
        /// </summary>
        IConfigKeyProviderInfo ProviderInfo { get; }

        /// <summary>Gives the client access to the set of Flags from the key's provider.  Used to indicate Fixed keys and when the KeyWasNotFound</summary>
        ConfigKeyProviderFlags ProviderFlags { get; }

        /// <summary>Returns a readonly copy of the key metadata INamedValueSet for this key as defined by the provider.</summary>
        INamedValueSet ProviderMetaData { get; }

        /// <summary>Optional.  Gives a user oriented description of the key's purpose, use, and valid values.</summary>
        string Description { get; }
    }

    /// <summary>
    /// This interface extends the <see cref="IConfigKeyAccessSpec"/> and <see cref="IConfigKeyAccessProviderInfo"/> interfaces 
    /// by giving the client with the means to access to a choosen config key's value and related information.
    /// </summary>
    public interface IConfigKeyAccess : IConfigKeyAccessProviderInfo
    {
        /// <summary>Gives the full key name for this item</summary>
        string Key { get; }

        /// <summary>Gives the client access to the set of Flags that are relevant for access to this config key.</summary>
        ConfigKeyAccessFlags Flags { get; }

        /// <summary>
        /// Empty when access is valid, last Update call succeeded, and client has not assigned any other error.  
        /// Not empty when an issue has been recorded in one of these steps.
        /// Setter clears the error from the last Update call but not for errors recorded when the access object was constructed.
        /// </summary>
        string ResultCode { get; set; }

        /// <summary>Returns true if the Flags indicate that the key is ReadOnlyOnce or the ProviderFlags indicate that it IsFixed or if the KeyWasNotFound.</summary>
        bool ValueIsFixed { get; }

        /// <summary>Returns the current value of the key as a string.  May be null, such as when the key was not found.</summary>
        string ValueAsString { get; }

        /// <summary>Returns the current value of the key in a ValueContainer as the provider last read (or saved) it.  May contain null, such as when the key was not found.</summary>
        ValueContainer VC { get; }

        /// <summary>Alternate (old) name for newly named VC property</summary>
        ValueContainer ValueContainer { get; }

        /// <summary>Returns a combined version of the key's meta data and the provider's meta data.  Filtering is generally performed against the contents of this property.</summary>
        INamedValueSet MetaData { get; }

        /// <summary>Returns the most recently updated copy of the key's meta</summary>
        INamedValueSet KeyMetaData { get; }

        /// <summary>True if this KeyAccess object is usable (ResultCode is empty)</summary>
        bool IsUsable { get; }

        /// <summary>True if this KeyAccess object's ValueContainer contents are not null or None.  Generally this is false when the given key was not found.</summary>
        bool HasValue { get; }

        /// <summary>This property returns true if ValueIsFixed is false and ValueSeqNum is not the same as the CurrentSeqNum.</summary>
        bool IsUpdateNeeded { get; }

        /// <summary>
        /// This method will refresh the ValueContainer and ValueAsString properties to represent the most recently accepted value for the corresponding key and provider.
        /// This method will update the KeyMetaData if the reference copy has been modified since this accessor object was created.
        /// This method will have no effect if the flag indicates that the key is a ReadOnlyOnce key or if the provider indicates that the key is Fixed.
        /// Returns true if the ValueAsString value changed or false otherwise.
        /// </summary>
        bool UpdateValue(bool forceUpdate = false);

        /// <summary>
        /// This method will refresh the ValueContainer and ValueAsString properties to represent the most recently accepted value for the corresponding key and provider.
        /// This method will update the NVS if the reference copy has been modified since this accessor object was created.
        /// This method will have no effect if the flag indicates that the key is a ReadOnlyOnce key or if the provider indicates that the key is Fixed.
        /// If the andMetaData parameter is set to true then this access object's NVS will be updated to the most recent one for this key, if needed.
        /// <para/>supports call chaining
        /// </summary>
        IConfigKeyAccess UpdateValueInline(bool forceUpdate = false);

        /// <summary>
        /// returns a string represenation of this key access object using the requested level of detail
        /// </summary>
        string ToString(ToStringDetailLevel detailLevel);

        /// <summary>
        /// Gives the seqeunce number of the value as it was last updated.
        /// </summary>
        int ValueSeqNum { get; }

        /// <summary>
        /// Gives the sequence number of the root ConfigKey value for this key (as created and maintained by the provider)
        /// </summary>
        int CurrentSeqNum { get; }

        /// <summary>
        /// Gives the seqeunce number of the accessor's key MetaData taken at the time it was last updated.
        /// </summary>
        int MetaDataSeqNum { get; }

        /// <summary>
        /// Gives the sequence number of the root ConfigKey MetaData for this key (as created and maintained by the provider)
        /// </summary>
        int CurrentMetaDataSeqNum { get; }
    }

    /// <summary>
    /// This is a basic implementation class for the IConfigKeyAccessSpec
    /// </summary>
    public class ConfigKeyAccessSpec : IConfigKeyAccessSpec
    {
        /// <summary>Normal constructor.  May be used with inline values or with property initializers</summary>
        public ConfigKeyAccessSpec(string key = null, ConfigKeyAccessFlags flags = default(ConfigKeyAccessFlags), INamedValueSet keyMetaData = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate) 
        {
            Key = key;
            Flags = flags;
            MetaData = keyMetaData;
            MergeBehavior = mergeBehavior;
        }

        /// <summary>Copy Constructor</summary>
        public ConfigKeyAccessSpec(IConfigKeyAccessSpec other)
        {
            Key = other.Key;
            Flags = other.Flags;
            MetaData = other.MetaData;
            MergeBehavior = other.MergeBehavior;
        }

        /// <summary>Copy Constructor</summary>
        public ConfigKeyAccessSpec(IConfigKeyAccess other)
        {
            Key = other.Key;
            Flags = other.Flags;
            MetaData = other.KeyMetaData;
        }

        /// <summary>Gives the full key name for this item</summary>
        public string Key { get; set; }

        /// <summary>Gives the client access to the set of Flags that are relevant to access to this config key.</summary>
        public ConfigKeyAccessFlags Flags { get; set; }

        /// <summary>
        /// Returns a readonly copy of the client provided key meta data INamedValueSet for this key spec.  
        /// When getting a config key accessor, if this property is non-empty then it will be merged into the root CKA's key meta data using the given MergeBehavior
        /// </summary>
        public INamedValueSet MetaData { get { return metaData ?? NamedValueSet.Empty; } set { metaData = value.ConvertToReadOnly(mapNullToEmpty: false); } }
        protected INamedValueSet metaData;

        /// <summary>This property is used when combining client provided key MetaData (above) into previously specified key meta data from other sources in the root CKA instance.  Defaults to AddAndUpdate</summary>
        public NamedValueMergeBehavior MergeBehavior { get; set; }

        /// <summary>ToString override for logging and debugging</summary>
        public override string ToString()
        {
            return "key:'{0}' flags:{1} metaData:{2} {3}".CheckedFormat(Key, Flags, MetaData.ToStringSML(traversalType: TraversalType.Flatten), MergeBehavior);
        }
    }

    /// <summary>
    /// Defines a set of levels of detail that may be used with various Config related ToString methods which support it.
    /// <para/>Values: Nominal (default), ReferenceInfoOnly, Full
    /// </summary>
    public enum ToStringDetailLevel
    {
        /// <summary>Nominal level of detail.  Produces same results as native ToString method.  This is the default value (0).</summary>
        Nominal = 0,

        /// <summary>Only include information about the object that defines what it refers to, but not about its actual content or state.</summary>
        ReferenceInfoOnly,

        /// <summary>Return full inforamation.  This is expected to include a proper superset of the information from each of the other levels of detail.</summary>
        Full,
    }

    #endregion

    #region IConfigKeyProviderInfo, IConfigKeyProvider

    /// <summary>
    /// IConfigKeyProviders are the means used to modularize the Config infrastructure and to allow third party key sources to be integrated into this system.
    /// This interface defines the set of getter properties that give basic readonly information about the provider and its configuration.
    /// </summary>
    public interface IConfigKeyProviderInfo
    {
        /// <summary>
        /// Gives the name of the provider
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Indicates the Basic Flags values that will be common to all keys that are served by this provider.  
        /// </summary>
        ConfigKeyProviderFlags BaseFlags { get; }

        /// <summary>
        /// Defines the string (or the empty string) that all keys that are handled by this provider will start with.  
        /// This is used if this provider is intended to place all of its keys in a, possibly unique, sub-tree.
        /// </summary>
        string KeyPrefix { get; }

        /// <summary>Gives the default INamedValueSet MetaData for this provider.  Also used as the base for the meta data nvs that is put into each config key provided by this provider.</summary>
        INamedValueSet ProviderMetaData { get; }
    }

    /// <summary>
    /// IConfigKeyProviders are the means used to modularize the Config infrastructure and to allow third party key sources to be integrated into this system.
    /// This interface defines the set of methods that must be implemented by a config key provider in order for the Config infrastructure to make use of it.
    /// Key providers that generate only fixed keys must implement the set key interface methods but they will never be called and can simply return fixed error messages
    /// indicateing "Not Implemented".
    /// </summary>
    public interface IConfigKeyProvider : IConfigKeyProviderInfo
    {
        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given MatchRuleSet and searchPredicate values.
        /// MatchRuleSet is used to filter the keys based on their names while searchPredicate may be used to filter the returned keys based on their MetaData contents
        /// null matchRulesSet and/or searchPredicate disables filtering based on that parameter.  If both are null then all known keys will be returned.
        /// The array returned is contains the union of the corresponding sorted arrays returned by each provider.
        /// </summary>
        string[] SearchForKeys(MatchRuleSet matchRuleSet = null, ConfigKeyFilterPredicate searchPredicate = null);

        /// <summary>
        /// Attempts to find information about the given keyAccessSpec and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// returns null if the key was not found or is not supported by this provider.
        /// if ensureExists is true and this provider supports ensureExists then it will create, and return, a new key using the given <paramref name="initialValue"/> as its initial value.
        /// </summary>
        /// <remarks>
        /// At the IConfigKeyProvider level, this method is typically only called once per fixed or read-only-once key as the config instance may keep prior key access objects and simply return clones of them.
        /// </remarks>
        IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec, bool ensureExists = false, ValueContainer initialValue = default(ValueContainer));

        /// <summary>
        /// This method allows the caller to update a set of values of the indicated keys to contain the corresponding ValueContainer values.
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known values for this key.  
        /// This method returns the empty string on success or an error code on failure.
        /// </summary>
        /// <remarks>At the IConfigKeyProvider level this method will be invoked for each sub-set of keys that share the same original provider</remarks>
        string SetValues(KeyValuePair<IConfigKeyAccess, ValueContainer>[] keyAccessAndValuesPairArray, string commentStr);
    }

    #endregion

    #region Config static class (effectively a namespace and an Extension Method source)

    /// <summary>
    /// This static class defines the means to get access to the IConfig singleton Instance property 
    /// and to provide extension method that help setup configuration in commonly used manners and to Try to parse strings as a given ValueT
    /// <para/>By default the IConfig singleton Instance will be constructed using the ConfigBase class.
    /// </summary>
    public static class Config
    {
        #region Singleton instance

        /// <summary>Gives the caller get and set access to the singleton Configuration instance that is used to access application wide configuration information.</summary>
        public static IConfig Instance 
        { 
            get 
            { 
                return singletonInstanceHelper.Instance; 
            } 
            set 
            { 
                singletonInstanceHelper.Instance = value; 
            }
        }

        private static SingletonHelperBase<IConfig> singletonInstanceHelper = new SingletonHelperBase<IConfig>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new ConfigBase("Config"));

        #endregion

        #region Instance setup methods

        /// <summary>
        /// This method creates and adds the standard set of IConfigKeyProviders to the current Config.Instance.
        /// </summary>
        public static void AddStandardProviders(StandardProviderSelect providerSelect = (StandardProviderSelect.EnvVars | StandardProviderSelect.AppConfig | StandardProviderSelect.Include))
        {
            string[] mainArgs = null;

            AddStandardProviders(Instance, ref mainArgs, providerSelect: providerSelect);
        }

        /// <summary>
        /// This method creates and adds the standard set of IConfigKeyProviders to the current Config.Instance.
        /// </summary>
        public static void AddStandardProviders(string[] mainArgs, StandardProviderSelect providerSelect = StandardProviderSelect.All)
        {
            AddStandardProviders(Instance, ref mainArgs, providerSelect: providerSelect);
        }

        /// <summary>
        /// This method creates and adds the standard set of IConfigKeyProviders to the current Config.Instance.
        /// </summary>
        public static void AddStandardProviders(ref string[] mainArgs, StandardProviderSelect providerSelect = StandardProviderSelect.All)
        {
            AddStandardProviders(Instance, ref mainArgs, providerSelect: providerSelect);
        }

        /// <summary>
        /// This method creates and adds the standard set of IConfigKeyProviders to the given IConfig instance
        /// </summary>
        public static IConfig AddStandardProviders(this IConfig config, ref string[] mainArgs, StandardProviderSelect providerSelect = StandardProviderSelect.All)
        {
            if (mainArgs != null && providerSelect.IsSet(StandardProviderSelect.MainArgs))
                config.AddProvider(new MainArgsConfigKeyProvider("MainArgs", ref mainArgs, String.Empty));

            if (providerSelect.IsSet(StandardProviderSelect.EnvVars))
                config.AddProvider(new EnvVarsConfigKeyProvider("EnvVars", String.Empty));

            if (providerSelect.IsSet(StandardProviderSelect.AppConfig))
                config.AddProvider(new AppConfigConfigKeyProvider("AppConfig", String.Empty));

            if (providerSelect.IsSet(StandardProviderSelect.Include))
                config.AddProvider(new IncludeFilesConfigKeyProvider("Include", "Include.File", config, String.Empty));

            return config;
        }

        #endregion

        #region additional helper methods: TryGetValue

        /// <summary>
        /// common helper method for some extension methods.  Attempt get a typed value from a IConfigKeyAccess object.  
        /// Returns assignes the key's parsed value to the value output parmeter and returns true if the key exists and could be parsed successfully.
        /// Returns assigns the given defaultValue to the value output parameter and returns false in all other cases.
        /// Updates the keyAccess's ResultCode field to the empty string on success or to a description of the failure reason on failure (if possible)
        /// </summary>
        internal static bool TryGetValue<ValueT>(string methodName, IConfigKeyAccess icka, out ValueT value, ValueT defaultValue, bool rethrow)
        {
            bool getSuccess = false;

            if (icka != null && icka.HasValue)
            {
                ValueContainer valueContainer = icka.VC;
                string resultCode = icka.ResultCode;

                try
                {
                    value = valueContainer.GetValue<ValueT>(rethrow: true);
                    icka.ResultCode = null;
                    getSuccess = true;
                }
                catch (System.Exception ex)
                {
                    value = defaultValue;

                    string ec = Fcns.MapNullOrEmptyTo(resultCode, ex.ToString());

                    if (!icka.Flags.SilenceIssues)
                        Config.Instance.IssueEmitter.Emit("{0} failed: {1}", methodName, ec);

                    if (!valueContainer.IsNullOrNone || !icka.Flags.IsOptional)
                        icka.ResultCode = ec;

                    if (rethrow)
                        throw ex;
                }
            }
            else
            {
                value = defaultValue;
            }

            return getSuccess;
        }

        #endregion
    }

    /// <summary>
    /// This enumeration can be used by clients to customize the set of standard providers that are added when using any of the AddStandardProviders helper methods.
    /// </summary>
    [Flags]
    public enum StandardProviderSelect
    {
        None = 0x00,
        MainArgs = 0x01,
        EnvVars = 0x02,
        AppConfig = 0x04,
        Include  = 0x08,

        All = (MainArgs | EnvVars | AppConfig | Include),
    }

    #endregion

    #region ConfigBase

    /// <summary>
    /// Partially abstract base class which may be used as the base for IConfig implementation classes.  
    /// Provides common Logger and Trace logger.  
    /// Provides implementation methods/properties for most of the IConfigSubscription interface and for most of the IConfigKeyGetSet interface.
	/// </summary>
	public class ConfigBase : IConfig, IConfigInternal, IEnumerable
    {
        #region Construction and loggers

        /// <summary>
        /// Named constructor.  Creates Logger and Trace from given name.
        /// </summary>
        public ConfigBase(string name)
        {
            Name = name;
            Logger = new Logging.ConfigLogger(name, Logging.LookupDistributionGroupName, Logging.LogGate.All);
            Trace = new Logging.ConfigLogger(name + ".Trace", Logging.LookupDistributionGroupName, Logging.LogGate.All);
        }

        /// <summary>
        /// Name value is readonly.  Assigned by constructor.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Base Logger to be used by this and implementation classes.
        /// </summary>
        protected Logging.ILogger Logger { get; private set; }

        /// <summary>
        /// Base Trace logger to be used by this and implementation classes.  Generally used to report non-setup related activity for non-Silent keys (value updates, repeated value gets).
        /// </summary>
        protected Logging.ILogger Trace { get; private set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (mutex)
            {
                return allKnownKeysDictionary.Values.ToArray().GetEnumerator();
            }
        }

        #endregion

        #region IConfig (Providers, Logging)

        /// <summary>
        /// This method attempts to add this given provider to the set that are used by this instance.
        /// <para/>Recommended practice is that each provider use a unique Name and that they provide non-overlapping sets of keys.  
        /// However no logic enforces, nor strictly requires, that either condition be true.
        /// </summary>
        public void AddProvider(IConfigKeyProvider provider)
        {
            // please note that the use of the mutex is not strictly required to protect the lockedProviderList but its use here is intended to indicate
            // that the list of providers shall only be changed when no other thread is using or changing the list.
            lock (mutex)
            {
                lockedProviderList.Add(provider);

                Logger.Debug.Emit("Added provider:'{0}' base flags:{1}", provider.Name, provider.BaseFlags);
            }
        }

        /// <summary>
        /// This property generates and returns an array of information about the currently registered providers.
        /// </summary>
        /// <remarks>
        /// An explicit clone is included here as the Array value is used both internally so that cached array instance cannot be given directly to callers
        /// as they are able to change its contents (but not length).
        /// </remarks>
        public IConfigKeyProviderInfo[] Providers
        {
            get
            {
                return lockedProviderList.Array.Clone() as IConfigKeyProviderInfo [];
            }
        }

        /// <summary>
        /// When this value is non-empty, ConfigKeyGetSet interface's SetValues method will attempt to find and use the indicated provider when setting a value for a key that was not previously found.
        /// </summary>
        public string DefaultProviderName { get; set; }

        /// <summary>
        /// Gives default IMesgEmitter to use for reporting issues.  Defaults to Logger.Warning.
        /// </summary>
        public Logging.IMesgEmitter IssueEmitter { get { return Logger.Warning; } }

        /// <summary>
        /// Gives default IMesgEmitter to use for reporting trace activities.  Defaults to Trace.Trace.
        /// </summary>
        public Logging.IMesgEmitter TraceEmitter { get { return Trace.Trace; } }

        #endregion

        #region IConfigSubscription implementation and related protected methods

        /// <summary>This is the list of notifiyable objects that will be informed whenever any seqNum in the ConfigSubscriptionSeqNums may have changed.  Client is required to consult the contents of that object to determine what might have changed.</summary>
        public IBasicNotificationList ChangeNotificationList { get { return changeNotificationList; } }

        /// <summary>Returns the ConfigSubscriptionSeqNums containing the most recently produced sequence number values from each source.</summary>
        public ConfigSubscriptionSeqNums SeqNums 
        { 
            get 
            { 
                return new ConfigSubscriptionSeqNums() 
                { 
                    ChangeSeqNum = changeSeqNum.VolatileValue, 
                    EnsureExistsSeqNum = ensureExistsSeqNum.VolatileValue, 
                    KeyAddedSeqNum = keyAddedSeqNum.VolatileValue,
                    KeyMetaDataChangeSeqNum = keyMetaDataChangeSeqNum.VolatileValue 
                }; 
            } 
        }


        /// <summary>Returns the ChangeSeqNum property value from SeqNums</summary>
        public int ChangeSeqNum { get { return changeSeqNum.VolatileValue; } }

        /// <summary>This property is true when any ReadOnce config key's value is not Fixed and it has been changed and no longer matches the original value that was read for it.</summary>
        public bool ReadOnceConfigKeyHasBeenChangedSinceItWasRead { get; private set; }

        private BasicNotificationList changeNotificationList = new BasicNotificationList();
        private AtomicInt32 changeSeqNum = new AtomicInt32();
        private AtomicInt32 ensureExistsSeqNum = new AtomicInt32();
        private AtomicInt32 keyAddedSeqNum = new AtomicInt32();
        private AtomicInt32 keyMetaDataChangeSeqNum = new AtomicInt32();

        /// <summary>
        /// Method used to bump the relevant change sequence number value(s) and Notify the items on the ChangeNotificationList.
        /// <para/>This method now also updates the ReadOnceConfigKeyHasBeenChangedSinceItWasRead flag
        /// </summary>
        protected void NotifyClientsOfChange(bool change = false, bool ensureExists = false, bool keyAdded = false, bool keyMetaDataChange = false)
        {
            int readOnceKeysThatHaveChangedCount = 0;

            if (change || ensureExists)
            {
                foreach (IConfigKeyAccess changedKey in changedKeyDicationary.Values)
                {
                    ConfigKeyAccessImpl readOnceCKAI = null;
                    if (readOnlyOnceKeyDictionary.TryGetValue(changedKey.Key ?? String.Empty, out readOnceCKAI) && readOnceCKAI != null && changedKey.ValueAsString != readOnceCKAI.ValueAsString)
                        readOnceKeysThatHaveChangedCount++;
                }

                bool newReadOnceConfigKeyHasBeenChangedSinceItWasReadValue = (readOnceKeysThatHaveChangedCount != 0);
                if (ReadOnceConfigKeyHasBeenChangedSinceItWasRead != newReadOnceConfigKeyHasBeenChangedSinceItWasReadValue)
                {
                    ReadOnceConfigKeyHasBeenChangedSinceItWasRead = newReadOnceConfigKeyHasBeenChangedSinceItWasReadValue;
                    Logger.Debug.Emit("ReadOnceConfigKeyHasBeenChangedSinceItWasRead has been set to {0}", ReadOnceConfigKeyHasBeenChangedSinceItWasRead);
                }
            }

            if (change)
                changeSeqNum.IncrementSkipZero();

            if (ensureExists)
                ensureExistsSeqNum.IncrementSkipZero();

            if (keyAdded)
                keyAddedSeqNum.IncrementSkipZero();

            if (keyMetaDataChange)
                keyMetaDataChangeSeqNum.IncrementSkipZero();

            changeNotificationList.Notify();
        }

        #endregion

        #region IConfigKeyGetSet implementation

        /// <summary>
        /// Gets/Sets the entire set of name mappings as an enumeration.  
        /// This set of mappings is used by GetConfigKeyAccess to support mapping from the given key to an alternate key on a case by case basis.  
        /// This mapping table may be used to allow two (or more) entities to end up using the same key accesd even if they do not know about each other in advance.
        /// </summary>
        public IEnumerable<Modular.Common.IMapNameFromTo> MapNameFromToSet 
        {
            get { lock (mutex) { return nameMappingSet.ToArray(); } }
            set { lock (mutex) { InnerSetMappingArray(value); } }
        }

        /// <summary>
        /// Adds the given set of IMapNameFromTo items to the current mapping MapNameFromToSet.
        /// </summary>
        public IConfigKeyGetSet AddRange(IEnumerable<Modular.Common.IMapNameFromTo> addMapNameFromToSet)
        {
            lock (mutex)
            {
                InnerAddRange(addMapNameFromToSet);
            }

            return this;
        }

        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given MatchRuleSet and searchPredicate values.
        /// MatchRuleSet is used to filter the keys based on their names while searchPredicate may be used to filter the returned keys based on their MetaData contents
        /// null matchRulesSet and/or searchPredicate disables filtering based on that parameter.  If both are null then all known keys will be returned.
        /// The array returned is contains the union of the corresponding sorted arrays returned by each provider.
        /// </summary>
        public string[] SearchForKeys(MatchRuleSet matchRuleSet = null, ConfigKeyFilterPredicate searchPredicate = null)
        {
            SortedList<string, object> foundKeysList = new SortedList<string, object>();

            matchRuleSet = matchRuleSet ?? MatchRuleSet.Any;

            lock (mutex)
            {
                foreach (IConfigKeyProvider provider in lockedProviderList.Array)
                {
                    string[] providerKeysFound = provider.SearchForKeys(matchRuleSet, searchPredicate);

                    foreach (string key in providerKeysFound)
                    {
                        if (!foundKeysList.ContainsKey(key))
                            foundKeysList.Add(key, null);
                    }
                }
            }

            return foundKeysList.Keys.ToArray();
        }

        /// <summary>
        /// Attempts to find information about the given keyAccessSpec and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// If the key does not exist but the client has requested EnsureExists, and an appropriate provider can be found under which to create it then it will be created using the given <paramref name="defaultValue"/> as its initial value.
        /// If the key does not exist and the client has not requested EnsureExists then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and using the given <paramref name="defaultValue"/>.
        /// </summary>
        public IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec, ValueContainer? defaultValue = null)
        {
            string methodName = Fcns.CheckedFormat("{0}({1})", Fcns.CurrentMethodName, keyAccessSpec);

            keyAccessSpec = keyAccessSpec ?? emptyKeyAccessSpec;

            IConfigKeyAccess icka = TryGetConfigKeyAccess(methodName, keyAccessSpec, defaultValue);

            if (icka.Flags.SilenceLogging)
            { }
            else if (icka.IsUsable)
                TraceEmitter.Emit("{0} succeeded: InitialValue:'{1}'", methodName, icka.ValueAsString);
            else if (keyAccessSpec.Flags.IsOptional && icka.ProviderFlags.KeyWasNotFound && !keyAccessSpec.Flags.SilenceIssues)
                TraceEmitter.Emit("{0} failed: error:'{1}', InitialValue:'{2}'", methodName, icka.ResultCode, icka.ValueAsString);
            else if (!keyAccessSpec.Flags.SilenceIssues)
                IssueEmitter.Emit("{0} failed: error:'{1}', InitialValue:'{2}'", methodName, icka.ResultCode, icka.ValueAsString);

            return icka;
        }

        private static readonly IConfigKeyAccessSpec emptyKeyAccessSpec = new ConfigKeyAccessSpec() { Key = String.Empty };

        #endregion

        #region Inner methods (name mapping: InnerSetMappingArray, InnerMapSanitizedName, InnerResetNameMapping, InnerAddRange1

        private MapNameFromToList nameMappingSet = new MapNameFromToList();
        private volatile Dictionary<string, string> nameMappingDictionary = new Dictionary<string, string>();

        protected void InnerSetMappingArray(IEnumerable<Modular.Common.IMapNameFromTo> nameMappingSet)
        {
            InnerResetNameMapping();
            InnerAddRange(nameMappingSet);
        }

        protected string InnerMapSanitizedName(string name)
        {
            Dictionary<string, string> nmp = nameMappingDictionary;

            string mappedName = null;

            if (nmp.TryGetValue(name, out mappedName) && mappedName != null)
                return mappedName;

            if (nameMappingSet.Map(name, ref mappedName) && mappedName != null)
                return mappedName;

            return name;
        }

        protected void InnerResetNameMapping()
        {
            nameMappingSet = new Modular.Common.MapNameFromToList();
            nameMappingDictionary = new Dictionary<string, string>();
        }

        private void InnerAddRange(IEnumerable<Modular.Common.IMapNameFromTo> addMapNameFromToSet)
        {
            foreach (Modular.Common.IMapNameFromTo mapItem in addMapNameFromToSet ?? emptyMapFromToArray)
            {
                nameMappingSet.Add(mapItem);
                if (mapItem.IsSimpleMap)
                    nameMappingDictionary[mapItem.From.Sanitize()] = mapItem.To.Sanitize();
                else if (mapItem is Modular.Common.MapNameFromToList)
                    InnerAddRange(mapItem as IEnumerable<Modular.Common.IMapNameFromTo>);
            }
        }

        private Modular.Common.IMapNameFromTo[] emptyMapFromToArray = new Common.IMapNameFromTo[0];

        #endregion

        #region internal methods used to implement Get and Update behavior (TryGetConfigKeyAccess, TryUpdateConfigKeyAccess)

        /// <summary>
        /// This abstract method must be implemented by a derived implementation object in order to actually read config key values.
        /// If the requested key exists then an IConfigKeyAccess for it will be returned.
        /// If the key does not exist but the client has requested EnsureExists, and an appropriate provider can be found under which to create it then it will be created using the given <paramref name="defaultValueIn"/> as its initial value.
        /// If the key does not exist and the client has not requested EnsureExists then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and using the given <paramref name="defaultValueIn"/>.
        /// </summary>
        protected virtual IConfigKeyAccess TryGetConfigKeyAccess(string methodName, IConfigKeyAccessSpec keyAccessSpec, ValueContainer ? defaultValueIn = null)
        {
            methodName = methodName + " [TryGetCKA]";

            keyAccessSpec = keyAccessSpec ?? emptyKeyAccessSpec;
            ConfigKeyAccessFlags flags = keyAccessSpec.Flags;

            string mappedKey = InnerMapSanitizedName(keyAccessSpec.Key);
            IConfigKeyAccessSpec mappedKeyAccessSpec = ((mappedKey == keyAccessSpec.Key) ? keyAccessSpec : new ConfigKeyAccessSpec(keyAccessSpec) { Key = mappedKey });

            bool ensureExists = keyAccessSpec.Flags.EnsureExists ?? false;
            string defaultProviderName = keyAccessSpec.Flags.DefaultProviderName.MapNullToEmpty();

            using (var eeTrace = new Logging.EnterExitTrace(TraceEmitter, methodName))
            {
                ConfigKeyAccessImpl ckaiRoot = null;

                bool tryUpdate = false;

                lock (mutex)
                {
                    // if the client is asking for a ReadOnlyOnce key and we have already seen this key as a read only once key then return a clone of the previously seen version.
                    if (flags.ReadOnlyOnce && readOnlyOnceKeyDictionary.TryGetValue(mappedKey ?? String.Empty, out ckaiRoot) && ckaiRoot != null)
                    {
                        // if the client provided any meta data then merge it into the root ROO key.
                        MergeMetaDataIfNeeded(ckaiRoot, keyAccessSpec.MetaData, keyAccessSpec.MergeBehavior, notifyClients: true);

                        if (!flags.SilenceLogging)
                            Trace.Trace.Emit("{0}: Using clone of prior instance:{1} [ROO]", methodName, ckaiRoot.ToString(ToStringDetailLevel.Full));
                    }

                    // if this key has been seen before then return a clone of the previously seen one.
                    if (ckaiRoot == null && allKnownKeysDictionary.TryGetValue(mappedKey ?? String.Empty, out ckaiRoot) && ckaiRoot != null && !ckaiRoot.ProviderFlags.KeyWasNotFound)
                    {
                        // if the client provided any meta data then merge it into the root normal key
                        MergeMetaDataIfNeeded(ckaiRoot, keyAccessSpec.MetaData, keyAccessSpec.MergeBehavior, notifyClients: true);

                        if (!flags.SilenceLogging)
                            Trace.Trace.Emit("{0}: Starting with clone of prior instance:{1}", methodName, ckaiRoot.ToString(ToStringDetailLevel.Full));

                        tryUpdate = !ckaiRoot.ValueIsFixed;     // if we find an old key that does not have a fixed value then attempt to update the new key in case the persisted value has changed since the key was first seen.
                    }

                    string providerName = "[NoProviderFound]";

                    if (ckaiRoot == null)
                    {
                        // no existing IConfigKeyAccess was found (ckaiRoot).  Search through the providers to see which one (if any) can provide an accessor for this (mapped) key.

                        IConfigKeyProvider defaultProvider = defaultProvider = (!defaultProviderName.IsNullOrEmpty() ? lockedProviderList.Array.FirstOrDefault(p => p.Name == defaultProviderName) : null);
                        IConfigKeyAccess ickaFromProvider = ((defaultProvider != null) ? defaultProvider.GetConfigKeyAccess(mappedKeyAccessSpec) : null);

                        if (ickaFromProvider != null)
                        {
                            providerName = defaultProvider.Name;
                        }
                        else
                        {
                            foreach (IConfigKeyProvider provider in lockedProviderList.Array)
                            {
                                ickaFromProvider = provider.GetConfigKeyAccess(mappedKeyAccessSpec);

                                if (ickaFromProvider != null)
                                    break;
                            }
                        }

                        if (ickaFromProvider != null && !flags.SilenceLogging && ickaFromProvider.ResultCode.IsNullOrEmpty() && ickaFromProvider.ProviderInfo != null)
                        {
                            Trace.Trace.Emit("{0}: Found provider: {1}", methodName, ickaFromProvider.ProviderInfo.Name);
                        }

                        if (ickaFromProvider == null && ensureExists)
                        {
                            IConfigKeyProvider provider = defaultProvider ?? lockedProviderList.Array.FirstOrDefault(p => p.BaseFlags.KeysMayBeAddedUsingEnsureExistsOption);
                            ValueContainer defaultValue = defaultValueIn ?? ValueContainer.Empty;

                            // NOTE: historically (unit test code) you can use EE for items with a Null default value, but not for items with a None default value.
                            if (provider != null && !defaultValue.IsEmpty)
                            {
                                providerName = provider.Name;

                                ickaFromProvider = provider.GetConfigKeyAccess(mappedKeyAccessSpec, ensureExists: true, initialValue: defaultValueIn ?? ValueContainer.Empty);

                                if (ickaFromProvider != null)
                                {
                                    if (!flags.SilenceLogging)
                                        Trace.Trace.Emit("{0}: created key '{1}' using provider '{2}'", methodName, mappedKeyAccessSpec.Key, provider.Name);

                                    NotifyClientsOfChange(ensureExists: true);
                                }
                            }
                        }

                        // if a new icka was successfully found above from a provider then generate a ckaiRoot (by casting or cloning), merge any keyAccessSpec MetaData into it and save it in the main key tables for later fast access.
                        if (ickaFromProvider != null && ickaFromProvider.ResultCode.IsNullOrEmpty() && !ickaFromProvider.ProviderFlags.KeyWasNotFound)
                        {
                            ckaiRoot = ickaFromProvider as ConfigKeyAccessImpl;
                            if (ckaiRoot != null)
                            {
                                ckaiRoot.ConfigInternal = this;

                                // when adding a new ckai to the known set, merge any MetaData from the client provided keyAccessSpec into the root instance.
                                MergeMetaDataIfNeeded(ckaiRoot, keyAccessSpec.MetaData, keyAccessSpec.MergeBehavior, notifyClients: false);

                                allKnownKeysDictionary[mappedKey] = ckaiRoot;
                                if (ickaFromProvider.Flags.ReadOnlyOnce)
                                    readOnlyOnceKeyDictionary[mappedKey] = ckaiRoot;

                                NotifyClientsOfChange(keyAdded: true, keyMetaDataChange: !keyAccessSpec.MetaData.IsNullOrEmpty());
                            }
                            else
                            {
                                if (!keyAccessSpec.Flags.SilenceIssues)
                                    IssueEmitter.Emit("Internal: {0}: key:'{1}' gave icka type '{2}' from provider '{3}' which is not usable here (cannot be casted to ConfigKeyAccessImpl)", methodName, keyAccessSpec.Key, ickaFromProvider.GetType(), providerName);
                            }
                        }
                    }
                }

                IConfigKeyAccess ickaReturn = null;

                if (ckaiRoot != null)
                {
                    ickaReturn = new ConfigKeyAccessImpl(ckaiRoot) { Flags = flags };
                }
                else
                {
                    // we did not find or create an ckaiRoot (from which to clone an accessor to give back to the caller).  As such the key was not found, nor created, by any provider.
                    var ckai = new ConfigKeyAccessImpl(mappedKey, flags, keyAccessSpec.MetaData, null) { ProviderFlags = new ConfigKeyProviderFlags() { KeyWasNotFound = true }, ConfigInternal = this };
                    if (defaultValueIn != null)
                        ckai.VC = defaultValueIn ?? ValueContainer.Empty; 

                    ickaReturn = ckai;

                    if (!flags.SilenceIssues && !flags.IsOptional)
                        Trace.Trace.Emit("{0}: {1}", methodName, ickaReturn.ResultCode);
                }

                if (tryUpdate)
                    TryUpdateConfigKeyAccess(methodName, ickaReturn);

                string ickaAsStr = ickaReturn.ToString();
                eeTrace.ExtraMessage = ickaAsStr;

                if (ickaReturn.IsUsable && !ickaReturn.Flags.SilenceLogging)
                    Trace.Trace.Emit("{0}: gave {1}", methodName, ickaAsStr);

                return ickaReturn;
            }
        }

        /// <summary>
        /// If required, merges the given <paramref name="keyMetaDataToMergeIn"/> into the current KeyMetaData in the given <paramref name="ckaiRoot"/> instance using the given <paramref name="mergeBehavior"/>.
        /// Also increments the CurrentMetaDataSeqNum in the ConfigKeyAccessImpl instance on which the merge is performed.
        /// </summary>
        private bool MergeMetaDataIfNeeded(ConfigKeyAccessImpl ckaiRoot, INamedValueSet keyMetaDataToMergeIn, NamedValueMergeBehavior mergeBehavior, bool notifyClients = true)
        {
            if (!keyMetaDataToMergeIn.IsNullOrEmpty() && ckaiRoot != null)
            {
                INamedValueSet entryKeyMetaData = ckaiRoot.KeyMetaData;

                if (ckaiRoot.KeyMetaData.IsNullOrEmpty() || mergeBehavior == NamedValueMergeBehavior.Replace)
                    ckaiRoot.KeyMetaData = keyMetaDataToMergeIn.ConvertToReadOnly().MapEmptyToNull();
                else
                    ckaiRoot.KeyMetaData = ckaiRoot.KeyMetaData.MergeWith(keyMetaDataToMergeIn, mergeBehavior);

                ckaiRoot.IncrementMetaDataSeqNum();
                ckaiRoot.MetaDataSeqNum = ckaiRoot.CurrentMetaDataSeqNum;       // merging is only done on the ckaiRoot so its MetaDataSeqNum (for cloaning) is always current.

                if (notifyClients)
                    NotifyClientsOfChange(keyMetaDataChange: true);

                if (entryKeyMetaData.IsNullOrEmpty())
                    TraceEmitter.Emit("key '{0}' root instance MetaData is been initialized from '{1}'", ckaiRoot.Key, keyMetaDataToMergeIn.ToStringSML(traversalType: TraversalType.Flatten));
                else
                    TraceEmitter.Emit("key '{0}' root instance MetaData has been merged with '{1}' [{2}]", ckaiRoot.Key, keyMetaDataToMergeIn.ToStringSML(traversalType: TraversalType.Flatten), mergeBehavior);

                return true;
            }

            return false;
        }

        /// <summary>
        /// This method will attempt to get, and update, the current value for the given configKeyAccess from the RootICKA it was previously dervied from.  
        /// Only items that are derived from ConfigKeyAccessImpl and that have a non-null RootICKA can be successfully updated.
        /// Only updates the KeyMetaData and the ResultCode in the current item if its ValueIsFixed property is true.
        /// Returns true if any aspect of the given key access was changed, otherwise returns false.
        /// </summary>
        /// <remarks>
        /// This method is intended for internal use only.  
        /// It assumes that the icka given is non-null, not ValueIsFixed.  As such it is assumed to have been previously found.
        /// </remarks>
        public virtual bool TryUpdateConfigKeyAccess(string methodName, IConfigKeyAccess icka, bool suppressLogging = false)
        {
            ConfigKeyAccessFlags flags = icka.Flags;

            suppressLogging |= icka.Flags.SilenceLogging;

            using (var eeTrace = new Logging.EnterExitTrace(!suppressLogging ? TraceEmitter : null, methodName))
            {
                int entryValueSeqNum = icka.ValueSeqNum;
                int entryMetaDataSeqNum = icka.MetaDataSeqNum;
                ValueContainer entryValue = icka.VC;
                INamedValueSet entryKeyMetaData = icka.KeyMetaData;
                string entryResultCode = icka.ResultCode;

                int updatedValueSeqNum = entryValueSeqNum;
                int updatedMetaDataSeqNum = entryMetaDataSeqNum;
                ValueContainer updatedValue = entryValue;
                INamedValueSet updatedKeyMetaData = entryKeyMetaData;
                string updatedResultCode = entryResultCode;

                ConfigKeyAccessImpl ckai = icka as ConfigKeyAccessImpl;
                IConfigKeyAccess rootICKA = (ckai != null) ? ckai.RootICKA : null;

                if (ckai == null || ckai.Provider == null || rootICKA == null)
                {
                    if (icka.IsUsable)
                        updatedResultCode = "This IConfigKeyAccess object cannot be updated using this IConfig instance";
                }
                else
                {
                    lock (mutex)
                    {
                        updatedValueSeqNum = rootICKA.ValueSeqNum;
                        updatedMetaDataSeqNum = rootICKA.MetaDataSeqNum;
                        updatedValue = rootICKA.VC;
                        updatedKeyMetaData = rootICKA.KeyMetaData;
                        updatedResultCode = rootICKA.ResultCode;
                    }
                }

                bool valueOrSeqNumChanged = ((ckai != null) && !ckai.ValueIsFixed && (!entryValue.IsEqualTo(updatedValue) || entryValueSeqNum != updatedValueSeqNum));
                bool metaDataSeqNumChanged = ((ckai != null) && (entryMetaDataSeqNum != updatedMetaDataSeqNum));
                bool resultCodeChanged = (entryResultCode != updatedResultCode);

                if (valueOrSeqNumChanged)
                {
                    ckai.VC = updatedValue;
                    ckai.ValueSeqNum = updatedValueSeqNum;
                }

                if (metaDataSeqNumChanged)
                {
                    ckai.KeyMetaData = updatedKeyMetaData;
                    ckai.MetaDataSeqNum = updatedMetaDataSeqNum;
                }

                if (resultCodeChanged)
                {
                    icka.ResultCode = updatedResultCode;
                }

                if (valueOrSeqNumChanged || metaDataSeqNumChanged || resultCodeChanged)
                    eeTrace.ExtraMessage = String.Join(",", new string[] { valueOrSeqNumChanged ? "Value" : "", metaDataSeqNumChanged ? "MetaData" : "", resultCodeChanged ? "ResultCode" : "" }.Where(s => !s.IsNullOrEmpty()));
                else
                    eeTrace.ExtraMessage = "no change";

                if (!suppressLogging)
                {
                    if (resultCodeChanged)
                        Logger.Debug.Emit("{0}: key '{1}' result code changed to '{2}' [from:'{3}']", methodName, icka.Key, icka.ResultCode, entryResultCode);

                    if (valueOrSeqNumChanged)
                        Logger.Debug.Emit("{0}: key '{1}' value updated to {2} seq:{3} [from:{4} seq:{5}]", methodName, icka.Key, icka.VC, icka.ValueSeqNum, entryValue, entryValueSeqNum);

                    if (metaDataSeqNumChanged)
                        Logger.Debug.Emit("{0}: key '{1}' keyMetaData updated to {2} seq:{3} [from:{4} seq:{5}]", methodName, icka.Key, icka.VC, icka.KeyMetaData.ToStringSML(traversalType: TraversalType.Flatten), entryValue, entryKeyMetaData.ToStringSML(traversalType: TraversalType.Flatten));
                }

                return (valueOrSeqNumChanged || metaDataSeqNumChanged || resultCodeChanged);
            }
        }

        #endregion

        #region Common stroage and handling for providers, Fixed and ReadOnlyOnce keys

        /// <summary>
        /// Gives the mutex instance that is used to gaurd access to the internals of this IConfig instance.  
        /// This is used to make certain that only one thread may use the API at any given time.
        /// </summary>
        protected readonly object mutex = new object();

        /// <summary>Stores the list of Providers that have been added to this IConfig instance.</summary>
        protected Utils.Collections.LockedObjectListWithCachedArray<IConfigKeyProvider> lockedProviderList = new Utils.Collections.LockedObjectListWithCachedArray<IConfigKeyProvider>();

        /// <summary>
        /// This contains the set of keys and key access objects where the client has indicated that the key will be read only once.  
        /// This allows later clients that ask for the same key with the same flag, to be given the same value, 
        /// even if the underlying key value was changed since the firdst client get read only once access to it.
        /// </summary>
        protected Dictionary<string, ConfigKeyAccessImpl> readOnlyOnceKeyDictionary = new Dictionary<string, ConfigKeyAccessImpl>();

        /// <summary>
        /// Contains the set of all key access objects that know to this IConfig instance so far, excluding those that were requested but which were not found and could not be created.
        /// Once a key has been seen, the IConfig instance will not search the providers list but will simply derive the new key access from the first seen one since it already knows
        /// which provider to use.
        /// </summary>
        protected Dictionary<string, ConfigKeyAccessImpl> allKnownKeysDictionary = new Dictionary<string, ConfigKeyAccessImpl>();

        /// <summary>
        /// This is the set of all key access objects for keys that have been used with SetValue(s).  
        /// This allows the engine to determine if any read once keys have been changed since they were first accessed.
        /// </summary>
        protected Dictionary<string, IConfigKeyAccess> changedKeyDicationary = new Dictionary<string, IConfigKeyAccess>();

        #endregion

        #region SetValue related methods

        /// <summary>
        /// This method allows the caller to update the persisted value for a specific key (if this is supported for the given key).
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known
        /// values for this key.  
        /// This method returns the empty string on success or an error code on failure.
        /// <para/>This method does not update the given keyAccess's value.  keyAccess is only used to define the key to use along with the pertinant value of its flags (primarily for SilenceIssues)
        /// </summary>
        public string SetValue(IConfigKeyAccess keyAccess, ValueContainer valueContainer, string commentStr)
        {
            string methodName = Fcns.CheckedFormat("{0}({1}, value:{2}, comment:{3})", Fcns.CurrentMethodName, keyAccess.ToString(ToStringDetailLevel.ReferenceInfoOnly), valueContainer, commentStr);

            return SetValues(methodName, keyAccess.Flags.SilenceIssues, new[] { new KeyValuePair<IConfigKeyAccess, ValueContainer>(keyAccess, valueContainer) }, commentStr);
        }

        /// <summary>
        /// This method allows the caller to attempt to update a set of values of the indicated keys to contain the corresponding ValueContainer values.
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known
        /// values for this key.  
        /// This method returns the empty string on success or an error code on failure.  
        /// On failure, all keys that could be updated will have been and the returned error code will be from the first key that could not be updated.
        /// <para/>This method does not update any given keyAccess's value.  keyAccess is only used to define the key to use along with the pertinant value of its flags (primarily for SilenceIssues)
        /// </summary>
        public string SetValues(KeyValuePair<IConfigKeyAccess, ValueContainer>[] keyAccessAndValuesPairArray, string commentStr)
        {
            StringBuilder sb = new StringBuilder();
            sb.CheckedAppendFormat("{0}(", Fcns.CurrentMethodName);
            bool silenceIssues = true;

            foreach (var kop in keyAccessAndValuesPairArray)
            {
                if (sb.Length != 0)
                    sb.Append(',');
                sb.CheckedAppendFormat("'{0}'<={1}", kop.Key.ToString(ToStringDetailLevel.ReferenceInfoOnly), kop.Value);

                silenceIssues &= kop.Key.Flags.SilenceIssues;
            }
            sb.CheckedAppendFormat(", comment:{0})", commentStr);

            return SetValues(sb.ToString(), silenceIssues, keyAccessAndValuesPairArray, commentStr);
        }

        /// <summary>
        /// This method allows the caller to update a set of values of the indicated keys to contain the corresponding valueAsString values.
        /// This method will generally record the given comment to help explain why the change was made.  
        /// The method may enforce constraints on the permitted values that may be so saved and may require that the given flags match externally known values for this key.  
        /// This method returns the empty string on success or the first non-empty error code it encountered on failure.
        /// </summary>
        protected virtual string SetValues(string methodName, bool silenceIssues, KeyValuePair<IConfigKeyAccess, ValueContainer>[] keyAccessAndValuesPairArray, string commentStr)
        {
            string firstError = null;

            using (var eeTrace = new Logging.EnterExitTrace(TraceEmitter, methodName))
            {
                // first sort the keys by their provider
                Dictionary<IConfigKeyProviderInfo, List<KeyValuePair<IConfigKeyAccess, ValueContainer>>> localDictionaryOfListsOfItemsByProvider = new Dictionary<IConfigKeyProviderInfo, List<KeyValuePair<IConfigKeyAccess, ValueContainer>>>();

                foreach (var kvp in keyAccessAndValuesPairArray)
                {
                    IConfigKeyAccess icka = kvp.Key;

                    if (icka == null)
                    {
                        firstError = firstError ?? "null IConfigKeyAccess encountered";
                        continue;
                    }

                    IConfigKeyProviderInfo providerInfo = icka.ProviderInfo;

                    if (!icka.IsUsable || providerInfo == null)
                    {
                        firstError = firstError ?? "{0} is not usable or is missing a provider".CheckedFormat(icka);
                        continue;
                    }

                    if (!localDictionaryOfListsOfItemsByProvider.ContainsKey(providerInfo))
                        localDictionaryOfListsOfItemsByProvider[providerInfo] = new List<KeyValuePair<IConfigKeyAccess, ValueContainer>>();

                    localDictionaryOfListsOfItemsByProvider[providerInfo].Add(kvp);
                }

                lock (mutex)
                {
                    foreach (IConfigKeyProviderInfo providerInfo in localDictionaryOfListsOfItemsByProvider.Keys)
                    {
                        IConfigKeyProvider provider = providerInfo as IConfigKeyProvider;

                        if (provider != null)
                        {
                            string ec = provider.SetValues(localDictionaryOfListsOfItemsByProvider[providerInfo].ToArray(), commentStr);

                            if (firstError == null && !string.IsNullOrEmpty(ec))
                                firstError = ec;
                        }
                        else if (firstError != null)
                        {
                            firstError = "Internal: Provider '{0}' is not usable (instance does not support the IConfigKeyProvider interface)".CheckedFormat(providerInfo);
                        }
                    }
                }

                bool success = (firstError == null);
                eeTrace.ExtraMessage = (success ? "Succeeded" : "Failed:" + firstError);

                if (!silenceIssues && !success)
                    Logger.Debug.Emit("{0}: Failed: {1}", methodName, firstError);

                NotifyClientsOfChange(change: true);
            }

            return firstError ?? String.Empty;
        }

        #endregion

        #region EnsureExists related methods

        /// <summary>
        /// This method allows the caller to attempt to verify that the given set of keys either already exist, or attempts to create them in the most suitable provider.
        /// </summary>
        public string EnsureExists(KeyValuePair<IConfigKeyAccessSpec, ValueContainer>[] keyAccessSpecAndValuesPairArray)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }

    #endregion

    #region ConfigKeyAccessImpl

    /// <summary>
    /// implementation class used to store IConfigKeyMetaData information.  The corresponding public interace is immutable.  As long as all
    /// copies of this object type are returned using the public interface then the contents of this object type can also be viewed as immutable.
    /// </summary>
    public class ConfigKeyAccessImpl : IConfigKeyAccess
    {
        /// <summary>Normal constructor - caller is expected to use Property initializers.  If provider is not null then ProviderFlags will be set to provider.BaseFlags</summary>
        public ConfigKeyAccessImpl(string key, ConfigKeyAccessFlags flags, INamedValueSet metaData, IConfigKeyProvider provider)
        {
            Key = key;
            Flags = flags;
            KeyMetaData = metaData;
            Provider = provider;
        }

        /// <summary>Normal constructor - caller is expected to use Property initializers.  If provider is not null then ProviderFlags will be set to provider.BaseFlags</summary>
        public ConfigKeyAccessImpl(IConfigKeyAccessSpec spec, IConfigKeyProvider provider)
        {
            Key = spec.Key;
            Flags = spec.Flags;
            KeyMetaData = spec.MetaData;
            Provider = provider;
        }

        /// <summary>"copyish" constructor for making key access objects derived from other ones - in most cases this is for ReadOnlyOnce keys or Fixed keys</summary>
        public ConfigKeyAccessImpl(IConfigKeyAccess other)
        {
            Key = other.Key;
            Flags = other.Flags;
            KeyMetaData = other.KeyMetaData;

            ConfigKeyAccessImpl ckai = other as ConfigKeyAccessImpl;
            ConfigKeyAccessImpl ckaiRoot = null;
            if (ckai != null)
            {
                ConfigInternal = ckai.ConfigInternal;
                RootICKA = (ckai.RootICKA != null) ? ckai.RootICKA : other;
                ckaiRoot = RootICKA as ConfigKeyAccessImpl;
            }

            ResultCode = other.ResultCode;
            VC = other.VC;
            HasValue = other.HasValue;

            ValueSeqNum = other.ValueSeqNum;
            MetaDataSeqNum = other.MetaDataSeqNum;

            ProviderFlags = other.ProviderFlags;

            if (ckaiRoot != null)
            {
                Provider = ckaiRoot.Provider;
            }
            else
            {
                CurrentSeqNum = other.CurrentSeqNum;
                CurrentMetaDataSeqNum = other.CurrentMetaDataSeqNum;

                Provider = other.ProviderInfo as IConfigKeyProvider;      // this will produce null if the rhs's ProviderInfo object does not also implement IConfigKeyProvider.  This does not attempt to change the rhs.MetaData information.
            }
        }

        /// <summary>Gives the IConfigInternal instance under which this config key access was created</summary>
        internal IConfigInternal ConfigInternal { get; set; }

        /// <summary>Gives the original CKA from which this CKA was last cloned.  Usually the RootICKA is created, and maintained, by the corresponding provider.</summary>
        internal IConfigKeyAccess RootICKA { get; set; }
        
        /// <summary>Gives the provider instance that is serving this key.</summary>
        public IConfigKeyProvider Provider 
        { 
            get { return _provider; } 
            set 
            { 
                _provider = value;
                if (_provider != null)
                {
                    ProviderFlags = _provider.BaseFlags;
                    ProviderMetaData = _provider.ProviderMetaData;
                }
                _metaData = null;
            } 
        }
        private IConfigKeyProvider _provider;

        private string ProviderName { get { return ((Provider != null) ? Provider.Name : String.Empty); } }

        #region IConfigKeyAccess Members

        /// <summary>Gives the full key name for this item</summary>
        public string Key { get; set; }

        /// <summary>Gives the client access to the set of Flags that are relevant to access to this config key.</summary>
        public ConfigKeyAccessFlags Flags { get; set; }

        /// <summary>
        /// Gives information (Name, BaseFlags) about the provider that is serving this key.  
        /// Also used internally to obtain the provider instance that is used to serve this key.
        /// </summary>
        public IConfigKeyProviderInfo ProviderInfo { get { return Provider; } }

        /// <summary>Gives the client access to the set of Flags from the key's provider.  Used to indicate Fixed keys and when the KeyWasNotFound</summary>
        public ConfigKeyProviderFlags ProviderFlags { get; internal set; }

        /// <summary>Returns a readonly copy of the key metadata INamedValueSet for this key as defined by the provider.</summary>
        public INamedValueSet ProviderMetaData { get; internal set; }

        /// <summary>Optional.  Gives a user oriented description of the key's purpose, use, and valid values.</summary>
        public string Description { get { return MetaData["Description"].VC.GetValue<string>(false) ?? String.Empty; } }

        /// <summary>
        /// Empty when access is valid, last Update call succeeded, and client has not assigned any other error.  
        /// Not empty when an issue has been recorded in one of these steps.
        /// Setter clears the error from the last Update call but not for errors recorded when the access object was constructed.
        /// </summary>
        public string ResultCode { get { return (ProviderFlags.KeyWasNotFound ? "Key was not found" : null) ?? resultCode ?? String.Empty; } set { resultCode = value; } }

        private string resultCode;

        /// <summary>Returns true if the Flags indicate that the key is ReadOnlyOnce or the ProviderFlags indicate that it IsFixed or if the KeyWasNotFound.</summary>
        public bool ValueIsFixed { get { return (Flags.ReadOnlyOnce || ProviderFlags.IsFixed || ProviderFlags.KeyWasNotFound); } }

        /// <summary>Returns the current value of the key as a string (i.e. as it is stored and handled by the configuration system).  May be null, such as when the key was not found.</summary>
        public string ValueAsString
        {
            get 
            {
                if (vc.cvt == ContainerStorageType.String)
                    return vc.GetValue<string>(ContainerStorageType.String, false, false);
                else if (vc.cvt.IsReferenceType())
                    return vc.GetValue<string>(ContainerStorageType.String, false, false);
                else if (vc.IsNullOrNone)       // special case so that an empty container gives back ValueAsString as just null rather than "None"
                    return null;
                else
                    return vc.ToString();
            }
        }

        /// <summary>Returns the current value of the key in a ValueContainer as the provider last read (or saved) it.  May contain None, such as when the key was not found.</summary>
        public ValueContainer VC
        {
            get { return vc; }
            set
            {
                vc = value;
                HasValue = !value.IsNullOrEmpty;
            }
        }

        /// <summary>Alternate (old) name for newly named VC property</summary>
        public ValueContainer ValueContainer { get { return VC; } }

        private Common.ValueContainer vc;

        /// <summary>Returns a combined version of the key's meta data and the provider's meta data.  Filtering is generally performed against the contents of this property.</summary>
        public INamedValueSet MetaData 
        {
            get { return _metaData ?? (_metaData = new NamedValueSet(NamedValueSet.Empty, subSets: new [] {KeyMetaData.MapEmptyToNull(), ProviderMetaData}.Where(nvs => nvs != null), asReadOnly: true)); } 
        }
        private INamedValueSet _metaData = null;

        /// <summary>Returns the most recently updated copy of the key's meta</summary>
        public INamedValueSet KeyMetaData { get { return _keyMetaData ?? NamedValueSet.Empty; } set { _keyMetaData = value.ConvertToReadOnly(mapNullToEmpty: false); _metaData = null; } }
        protected INamedValueSet _keyMetaData;

        /// <summary>True if this KeyAccess object is usable (ResultCode is empty)</summary>
        public bool IsUsable { get { return String.IsNullOrEmpty(ResultCode); } }

        /// <summary>True if this KeyAccess object's ValueContainer contents are not null or None.  Generally this is false when the given key was not found.</summary>
        public bool HasValue { get; private set; }

        /// <summary>This property returns true if ValueIsFixed is false and ValueSeqNum is not the same as the CurrentSeqNum or MetaDataSeqNum is not equal to CurrentMetaDataSeqNum.</summary>
        public bool IsUpdateNeeded { get { return (!ValueIsFixed && ((ValueSeqNum != CurrentSeqNum) || (MetaDataSeqNum != CurrentMetaDataSeqNum))); } }

        /// <summary>
        /// This method will refresh the ValueAsString property to represent the most recently accepted value for the corresponding key and provider.
        /// This method will have no effect if the flag indicates that the key is a ReadOnlyOnce key or if the provider indicates that the key is Fixed.
        /// Returns true if the ValueAsString value changed or false otherwise.
        /// </summary>
        public bool UpdateValue(bool forceUpdate = false)
        {
            // UpdateValue does not do anything when the IConfigKeyAccess flags include Fixed or ReadOnlyOnce.
            if (!IsUpdateNeeded && !forceUpdate || ValueIsFixed || (ConfigInternal == null))
                return false;

            string methodName = Fcns.CheckedFormat("IConfigKeyAccess.UpdateValue[key:'{0}']", Key);

            return ConfigInternal.TryUpdateConfigKeyAccess(methodName, this);
        }

        IConfigKeyAccess IConfigKeyAccess.UpdateValueInline(bool forceUpdate)
        {
            UpdateValue(forceUpdate: forceUpdate);

            return this;
        }

        /// <summary>
        /// Gives the seqeunce number of the value as it was last updated.
        /// </summary>
        public int ValueSeqNum { get; set; }

        /// <summary>
        /// Gives the sequence number of the root ConfigKey value for this key (as created and maintained by the provider)
        /// </summary>
        public int CurrentSeqNum { get { return (RootICKA != null ? RootICKA.CurrentSeqNum : _currentSeqNum); } internal set { _currentSeqNum = value; } }
        private int _currentSeqNum = 0;


        /// <summary>
        /// Gives the seqeunce number of the accessor's key MetaData taken at the time it was last updated.
        /// </summary>
        public int MetaDataSeqNum { get; set; }

        /// <summary>
        /// Gives the sequence number of the root ConfigKey MetaData for this key (as created and maintained by the provider)
        /// </summary>
        public int CurrentMetaDataSeqNum { get { return (RootICKA != null ? RootICKA.CurrentMetaDataSeqNum : _currentMetaDataSeqNum); } internal set { _currentMetaDataSeqNum = value; } }
        private int _currentMetaDataSeqNum = 0;

        /// <summary>
        /// Increments the backing value for the CurrentMetaDataSeqNum (skips zero)
        /// </summary>
        internal void IncrementMetaDataSeqNum()
        {
            _currentMetaDataSeqNum = _currentMetaDataSeqNum.IncrementSkipZero();
        }

        /// <summary>
        /// basic logging/debugging ToString implementation.  Returns ToString(ToStringDetailLevel.Nominal)
        /// </summary>
        public override string ToString()
        {
            return ToString(ToStringDetailLevel.Nominal);
        }

        /// <summary>
        /// returns a string represenation of this key access object using the requested level of detail
        /// </summary>
        /// <summary>
        /// returns a string represenation of this key access object using the requested level of detail
        /// </summary>
        public string ToString(ToStringDetailLevel detailLevel)
        {
            string metaDataStr = (MetaData ?? NamedValueSet.Empty).ToStringSML(traversalType: TraversalType.Flatten);

            switch (detailLevel)
            {
                case ToStringDetailLevel.ReferenceInfoOnly:
                    return Fcns.CheckedFormat("key:'{0}' flags:{1} md:{2} {3}", Key, Flags, metaDataStr, ProviderName, ProviderFlags);
                case ToStringDetailLevel.Nominal:
                case ToStringDetailLevel.Full:
                default:
                    if (IsUsable)
                        return Fcns.CheckedFormat("key:'{0}' Value:{1} flags:{2} md:{3} {4}", Key, VC, Flags, metaDataStr, ProviderFlags);
                    else
                        return Fcns.CheckedFormat("key:'{0}' Value:{1} flags:{2} md:{3} {4} ec:'{5}'", Key, VC, Flags, metaDataStr, ProviderFlags, ResultCode);
            }
        }

        #endregion
    }

    #endregion
}
