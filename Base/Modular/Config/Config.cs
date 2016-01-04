//-------------------------------------------------------------------
/*! @file Config.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2014 Mosaic Systems Inc., All rights reserved
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
using MosaicLib.Modular.Common;
using MosaicLib.Utils.StringMatching;

namespace MosaicLib.Modular.Config
{
    #region ConfigKeyAccessFlags, ConfigKeyProviderFlags

    /// <summary>
    /// This struct contains values that define specific details about how a client wants to access a specific config key.
    /// <para/>Supported concepts include ReadOnlyOnce (default), MayBeChanged, Optional, Required (default), Silent 
    /// </summary>
    public struct ConfigKeyAccessFlags
    {
        /// <summary>Copy constructor.  Often used with property initializers</summary>
        public ConfigKeyAccessFlags(ConfigKeyAccessFlags rhs) 
            : this()
        {
            MayBeChanged = rhs.MayBeChanged;
            IsOptional = rhs.IsOptional;
            SilenceIssues = rhs.silenceIssues;
            SilenceLogging = rhs.SilenceLogging;
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

        /// <summary>ToString override for debugging and logging</summary>
        public override string ToString()
        {
            return Fcns.CheckedFormat("{0}{1}{2}", (ReadOnlyOnce ? "RdOnce" : "CanChange"), (IsOptional ? "+Opt" : "+Req"), (SilenceLogging ? "-Logging" : (SilenceIssues ? "-Issues" : String.Empty)));
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
            keysMayBeAdded = rhs.keysMayBeAdded;
            KeyWasNotFound = rhs.KeyWasNotFound;
        }

        /// <summary>Flag is set by a provider to indicate that the value will not change from its initial value.</summary>
        public bool IsFixed { get; set; }

        /// <summary>Flag is set by a provider to indicate that the value may be change using the SetValue(s) method(s).</summary>
        public bool MayBeChanged { get { return !IsFixed; } set { IsFixed = !value; } }

        /// <summary>Provider level flag indicates if the provider allows keys to be added.  This is only possible for providers that are not fixed.</summary>
        public bool KeysMayBeAdded { get { return (keysMayBeAdded && !IsFixed); } set { keysMayBeAdded = value; } }
        private bool keysMayBeAdded;

        /// <summary>The provider sets this to true if changes to a key will be peristed.</summary>
        public bool IsPersisted { get; set; }

        /// <summary>Flags is set by the provider in individual IConfigKeyAccess items to indicate that the requested key was not found</summary>
        public bool KeyWasNotFound { get; set; }

        /// <summary>ToString override for debugging and logging</summary>
        public override string ToString()
        {
            return Fcns.CheckedFormat("{0}{1}{2}", (IsFixed ? "Fixed" : "MayBeChanged"), (KeyWasNotFound ? "+NotFound" : String.Empty), (KeysMayBeAdded ? "+CanAdd" : String.Empty));
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
        /// Attempts to find information about the given key and returns an object that implements the IConfigKeyAccess interface
        /// for this key.  If the key does not exist then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and null for the ValueAsString
        /// and this object is returned.
        /// <para/>Uses ConfigKeyAccessFlags.Optional for the flags.
        /// </summary>
        public static IConfigKeyAccess GetConfigKeyAccess(this string key)
        {
            return Config.Instance.GetConfigKeyAccess(key);
        }

        /// <summary>
        /// Attempts to find information about the given key and returns an object that implements the IConfigKeyAccess interface
        /// for this key.  If the key does not exist then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and null for the ValueAsString
        /// and this object is returned.
        /// <para/>Uses ConfigKeyAccessFlags.Optional for the flags.
        /// </summary>
        public static IConfigKeyAccess GetConfigKeyAccess(this IConfig config, string key)
        {
            return config.GetConfigKeyAccess(new ConfigKeyAccessSpec(key, new ConfigKeyAccessFlags() { MayBeChanged = true, IsOptional = true }));
        }

        /// <summary>
        /// Attempts to find information from the given provider about the given key and returns an object that implements the IConfigKeyAccess interface
        /// for this key.  If the provider does not support this key then the provider will return null.
        /// <para/>Uses ConfigKeyAccessFlags.Optional for the flags.
        /// </summary>
        public static IConfigKeyAccess GetConfigKeyAccess(this IConfigKeyProvider provider, string key)
        {
            return provider.GetConfigKeyAccess(new ConfigKeyAccessSpec(key, new ConfigKeyAccessFlags() { MayBeChanged = true, IsOptional = true }));
        }

        /// <summary>
        /// Attempts to find information about the given key and returns an object that implements the IConfigKeyAccess interface
        /// for this key.  If the key does not exist then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and null for the ValueAsString
        /// and this object is returned.
        /// <para/>Uses ConfigKeyAccessFlags.ReadOnlyOnce | ConfigKeyAccessFlags.Optional for the flags.
        /// </summary>
        public static IConfigKeyAccess GetConfigKeyAccessOnce(this IConfig config, string key)
        {
            return config.GetConfigKeyAccess(new ConfigKeyAccessSpec(key, new ConfigKeyAccessFlags() { ReadOnlyOnce = true, IsOptional = true }));
        }

        /// <summary>
        /// Extension method to get a typed value from a IConfigKeyAccess object.  
        /// Returns the key's value parsed as the given type if the key exists and could be parsed successfully.
        /// Returns the given defaultValue in all other cases.
        /// Updates the keyAccess's ResultCode field to the empty string on success or to a description of the failure reason on failure (if possible)
        /// </summary>
        public static ValueT GetValue<ValueT>(this IConfigKeyAccess keyAccess, ValueT defaultValue)
        {
            string key = ((keyAccess != null) ? keyAccess.Key : String.Empty);
            string methodName = Fcns.CheckedFormat("{0}<{1}>(key:'{2}', default:'{3}')", new System.Diagnostics.StackFrame().GetMethod().Name, typeof(ValueT), key, defaultValue);

            ValueT value;

            if (keyAccess != null && Config.TryGetValue(methodName, keyAccess, out value, defaultValue, false))
                return value;
            else
                return defaultValue;
        }

        /// <summary>
        /// Extension method to get a typed value from a IConfigKeyAccess object.  
        /// Returns the key's value parsed as the given type if the key exists and could be parsed successfully.
        /// Returns the given defaultValue in all other cases.
        /// Updates the keyAccess's ResultCode field to the empty string on success or to a description of the failure reason on failure (if possible)
        /// Throws ValueContainerGetValueException if the key's contained value cannot be converted to the desired type and the user has passed rethrow as true.
        /// </summary>
        /// <exception cref="MosaicLib.Modular.Common.ValueContainerGetValueException">May be thrown if the contained value cannot be converted to the desired type and the caller has passed rethrow = true</exception>
        public static ValueT GetValue<ValueT>(this IConfigKeyAccess keyAccess, ValueT defaultValue, bool rethrow)
        {
            string key = ((keyAccess != null) ? keyAccess.Key : String.Empty);
            string methodName = Fcns.CheckedFormat("{0}<{1}>(key:'{2}', default:'{3}')", new System.Diagnostics.StackFrame().GetMethod().Name, typeof(ValueT), key, defaultValue);

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
        public static bool TryGetValue<ValueT>(this IConfigKeyAccess keyAccess, out ValueT value, ValueT defaultValue)
        {
            string key = ((keyAccess != null) ? keyAccess.Key : String.Empty);
            string methodName = Fcns.CheckedFormat("{0}<{1}>(key:'{2}', default:'{3}')", new System.Diagnostics.StackFrame().GetMethod().Name, typeof(ValueT), key, defaultValue);

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
        public static string SetValue(this IConfigKeyAccess keyAccess, ValueContainer valueContainer, string commentStr)
        {
            if (keyAccess != null)
            {
                string ec = Config.Instance.SetValue(keyAccess, valueContainer, commentStr);
                keyAccess.UpdateValue();
                return ec;
            }
            else
                return "keyAccess parameter was given as null";
        }
    }

    #endregion

    #region Key Value processing helpers

    /// <summary>
    /// This delegate represents a method that can be used to attempt to parse a given string.  
    /// If the parse is successfull then the delegate retains the parsed value as a side effect of the invoke and returns the empty string.
    /// If the parse is not successfull then the delegate returns a non-empty string description of why the parse failed.
    /// </summary>
    delegate string TryParseDelegate(string valueStr);

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

        /// <summary>This is the list of notifiyable objects that will be informed whenever a new set of config key values become available</summary>
        IBasicNotificationList ChangeNotificationList { get; }

        /// <summary>This sequence number counts the number of times that the one or more config key values have changed</summary>
        Int32 ChangeSeqNum { get; }

        /// <summary>This property is true when any ReadOnce config key's value is not Fixed and it has been changed and no longer matches the original value that was read for it.</summary>
        bool ReadOnceConfigKeyHasBeenChangedSinceItWasRead { get; }

        #endregion
    }

    /// <summary>
    /// Defines the interface used by clients to obtain values from config keys accessed by a key string.
    /// </summary>
    public interface IConfigKeyGetSet
    {
        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given MatchRuleSet contents.  
        /// If matchRuleSet is passed as MatchRuleSet.All then all keys will be returned.
        /// The array returned is contains the union of the corresponding sorted arrays returned by each provider.
        /// </summary>
        string[] SearchForKeys(MatchRuleSet matchRuleSet);

        /// <summary>
        /// Attempts to find information about the given spec and returns an object that implements the IConfigKeyAccess interface
        /// for this key.  If the key does not exist then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and null for the ValueAsString
        /// and this object is returned.
        /// </summary>
        IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec);

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

        /// <summary>Gives the client access to the set of Flags that are relevant to access to this config key.</summary>
        ConfigKeyAccessFlags Flags { get; }
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
        INamedValueSet KeyMetaData { get; }

        /// <summary>Optional.  Gives a user oriented description of the key's purpose, use, and valid values.</summary>
        string Description { get; }
    }

    /// <summary>
    /// This interface extends the <see cref="IConfigKeyAccessSpec"/> and <see cref="IConfigKeyAccessProviderInfo"/> interfaces 
    /// by giving the client with the means to access to a choosen config key's value and related information.
    /// </summary>
    public interface IConfigKeyAccess : IConfigKeyAccessSpec, IConfigKeyAccessProviderInfo
    {
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
        Common.ValueContainer ValueContainer { get; }

        /// <summary>True if this KeyAccess object is usable (ResultCode is empty)</summary>
        bool IsUsable { get; }

        /// <summary>True if this KeyAccess object's ValueContainer contents are not null or None.  Generally this is false when the given key was not found.</summary>
        bool HasValue { get; }

        /// <summary>
        /// This method will refresh the ValueAsString property to represent the most recently accepted value for the corresponding key and provider.
        /// This method will have no effect if the flag indicates that the key is a ReadOnlyOnce key or if the provider indicates that the key is Fixed.
        /// Returns true if the ValueAsString value changed or false otherwise.
        /// </summary>
        bool UpdateValue();

        /// <summary>
        /// returns a string represenation of this key access object using the requested level of detail
        /// </summary>
        string ToString(ToStringDetailLevel detailLevel);
    }

    /// <summary>
    /// This is a basic implementation class for the IConfigKeyAccessSpec
    /// </summary>
    public class ConfigKeyAccessSpec : IConfigKeyAccessSpec
    {
        /// <summary>Default constructor for use with property initializers</summary>
        public ConfigKeyAccessSpec() { }

        /// <summary>Full constructor</summary>
        public ConfigKeyAccessSpec(string key, ConfigKeyAccessFlags flags) 
        {
            Key = key;
            Flags = flags;
        }

        /// <summary>Copy Constructor</summary>
        public ConfigKeyAccessSpec(IConfigKeyAccessSpec rhs)
        {
            Key = rhs.Key;
            Flags = rhs.Flags;
        }

        /// <summary>Gives the full key name for this item</summary>
        public string Key { get; set; }

        /// <summary>Gives the client access to the set of Flags that are relevant to access to this config key.</summary>
        public ConfigKeyAccessFlags Flags { get; set; }

        /// <summary>ToString override for logging and debugging</summary>
        public override string ToString()
        {
            return "key:'{0}' flags:{1}".CheckedFormat(Key, Flags);
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
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given MatchRuleSet contents.  
        /// If matchRuleSet is passed as MatchRuleSet.All then all keys will be returned.
        /// </summary>
        string[] SearchForKeys(MatchRuleSet matchRuleSet);

        /// <summary>
        /// Attempts to find information about the given keyAccessSpec and returns an object that implements the IConfigKeyAccess interface for this key.  
        /// returns null if the key was not found or is not supported by this provider.
        /// </summary>
        /// <remarks>
        /// At the IConfigKeyProvider level, this method is typically only called once per fixed or read-only-once key as the config instance may keep prior key access objects and simply return clones of them.
        /// </remarks>
        IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec);

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

    #region Config static class (effectively a namepsace and an Extension Method source)

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
        /// <para/>Standard set is: EnvVars, AppConfig, Include
        /// </summary>
        public static void AddStandardProviders()
        {
            string[] mainArgs = null;

            AddStandardProviders(Instance, ref mainArgs);
        }

        /// <summary>
        /// This method creates and adds the standard set of IConfigKeyProviders to the current Config.Instance.
        /// <para/>Standard set is: MainArgs, EnvVars, AppConfig, Include
        /// </summary>
        public static void AddStandardProviders(string[] mainArgs)
        {
            AddStandardProviders(Instance, ref mainArgs);
        }

        /// <summary>
        /// This method creates and adds the standard set of IConfigKeyProviders to the current Config.Instance.
        /// <para/>Standard set is: MainArgs, EnvVars, AppConfig, Include
        /// </summary>
        public static void AddStandardProviders(ref string[] mainArgs)
        {
            AddStandardProviders(Instance, ref mainArgs);
        }

        /// <summary>
        /// This method creates and adds the standard set of IConfigKeyProviders to the given IConfig instance
        /// <para/>Standard set is: MainArgs, EnvVars, AppConfig, Include
        /// </summary>
        public static IConfig AddStandardProviders(this IConfig config, ref string[] mainArgs)
        {
            if (mainArgs != null)
            {
                config.AddProvider(new MainArgsConfigKeyProvider("MainArgs", ref mainArgs, String.Empty));
            }

            config.AddProvider(new EnvVarsConfigKeyProvider("EnvVars", String.Empty));
            config.AddProvider(new AppConfigConfigKeyProvider("AppConfig", String.Empty));
            config.AddProvider(new IncludeFilesConfigKeyProvider("Include", "Include.File", config, String.Empty));

            return config;
        }

        #endregion

        #region additional helper methods

        /// <summary>
        /// common helper method for some extension methods.  Attempt get a typed value from a IConfigKeyAccess object.  
        /// Returns assignes the key's parsed value to the value output parmeter and returns true if the key exists and could be parsed successfully.
        /// Returns assigns the given defaultValue to the value output parameter and returns false in all other cases.
        /// Updates the keyAccess's ResultCode field to the empty string on success or to a description of the failure reason on failure (if possible)
        /// </summary>
        internal static bool TryGetValue<ValueT>(string methodName, IConfigKeyAccess keyAccess, out ValueT value, ValueT defaultValue, bool rethrow)
        {
            bool getSuccess = false;

            if (keyAccess != null)
            {
                ValueContainer valueContainer = keyAccess.ValueContainer;
                string resultCode = keyAccess.ResultCode;

                try
                {
                    value = valueContainer.GetValue<ValueT>(true);
                    keyAccess.ResultCode = null;
                    getSuccess = true;
                }
                catch (System.Exception ex)
                {
                    value = defaultValue;

                    string ec = Fcns.MapNullOrEmptyTo(resultCode, ex.ToString());

                    if (!keyAccess.Flags.SilenceIssues)
                        Config.Instance.IssueEmitter.Emit("{0} failed: {1}", methodName, ec);

                    if (!valueContainer.IsNullOrNone || !keyAccess.Flags.IsOptional)
                        keyAccess.ResultCode = ec;

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

    #endregion

    #region ConfigBase

    /// <summary>
    /// Partially abstract base class which may be used as the base for IConfig implementation classes.  
    /// Provides common Logger and Trace logger.  
    /// Provides implementation methods/properties for most of the IConfigSubscription interface and for most of the IConfigKeyGetSet interface.
	/// </summary>
	public class ConfigBase : IConfig
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

                Trace.Info.Emit("Added provider:'{0}' base flags:{1}", provider.Name, provider.BaseFlags);
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
        /// Gives default IMesgEmitter to use for reporting issues.  Defaults to Logger.Warning.
        /// </summary>
        public Logging.IMesgEmitter IssueEmitter { get { return Logger.Warning; } }

        /// <summary>
        /// Gives default IMesgEmitter to use for reporting trace activities.  Defaults to Trace.Trace.
        /// </summary>
        public Logging.IMesgEmitter TraceEmitter { get { return Trace.Trace; } }

        #endregion

        #region IConfigSubscription implementation and related protected methods

        /// <summary>This is the list of notifiyable objects that will be informed whenever a new set of config key values become available</summary>
        public IBasicNotificationList ChangeNotificationList { get { return changeNotificationList; } }

        /// <summary>This sequence number counts the number of times that the one or more config key values have changed</summary>
        public Int32 ChangeSeqNum { get { return changeSeqNum.VolatileValue; } }

        /// <summary>This property is true when any ReadOnce config key's value is not Fixed and it has been changed and no longer matches the original value that was read for it.</summary>
        public bool ReadOnceConfigKeyHasBeenChangedSinceItWasRead { get; private set; }

        private BasicNotificationList changeNotificationList = new BasicNotificationList();
        private AtomicInt32 changeSeqNum = new AtomicInt32();

        /// <summary>
        /// Method used to bump the changeSeqNum and Notify the items on the ChangeNotificationList.
        /// <para/>This method now alos updates the ReadOnceConfigKeyHasBeenChangedSinceItWasRead flag
        /// </summary>
        protected void NotifyClientsOfChange()
        {
            int readOnceKeysThatHaveChangedCount = 0;

            foreach (IConfigKeyAccess changedKey in changedKeyDicationary.Values)
            {
                IConfigKeyAccess readOnceVersion = null;
                if (readOnlyOnceKeyDictionary.TryGetValue(changedKey.Key ?? String.Empty, out readOnceVersion) && readOnceVersion != null && changedKey.ValueAsString != readOnceVersion.ValueAsString)
                    readOnceKeysThatHaveChangedCount++;
            }

            bool newReadOnceConfigKeyHasBeenChangedSinceItWasReadValue = (readOnceKeysThatHaveChangedCount != 0);
            if (ReadOnceConfigKeyHasBeenChangedSinceItWasRead != newReadOnceConfigKeyHasBeenChangedSinceItWasReadValue)
            {
                ReadOnceConfigKeyHasBeenChangedSinceItWasRead = newReadOnceConfigKeyHasBeenChangedSinceItWasReadValue;
                Trace.Info.Emit("ReadOnceConfigKeyHasBeenChangedSinceItWasRead has been set to {0}", ReadOnceConfigKeyHasBeenChangedSinceItWasRead);
            }

            changeSeqNum.IncrementSkipZero();
            changeNotificationList.Notify();
        }

        #endregion

        #region IConfigKeyGetSet implementation

        /// <summary>
        /// SearchForKeys allows the caller to "explore" the key space and find sets of known keys based on the given MatchRuleSet contents.  
        /// If matchRuleSet is passed as MatchRuleSet.All then all keys will be returned.
        /// The array returned is contains the union of the corresponding sorted arrays returned by each provider.
        /// </summary>
        public string[] SearchForKeys(MatchRuleSet matchRuleSet)
        {
            SortedList<string, object> foundKeysList = new SortedList<string, object>();

            lock (mutex)
            {
                foreach (IConfigKeyProvider provider in lockedProviderList.Array)
                {
                    string[] providerKeysFound = provider.SearchForKeys(matchRuleSet);

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
        /// Attempts to find information about the given keyAccessSpec and returns an object that implements the IConfigKeyAccess interface
        /// for this key.  If the key does not exist then a stub ConfigKeyAccess object is generated containing a non-empty ResultCode and null for the ValueAsString
        /// and this object is returned.
        /// </summary>
        public IConfigKeyAccess GetConfigKeyAccess(IConfigKeyAccessSpec keyAccessSpec)
        {
            string methodName = Fcns.CheckedFormat("{0}({1})", new System.Diagnostics.StackFrame().GetMethod().Name, keyAccessSpec);

            keyAccessSpec = keyAccessSpec ?? emptyKeyAccessSpec;

            IConfigKeyAccess icka = TryGetConfigKeyAccess(methodName, keyAccessSpec);

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

        #region internal methods used to implement Get and Update behavior

        /// <summary>
        /// This abstract method must be implemented by a derived implementation object in order to actually read config key values.
        /// If the requested key exists then an IConfigKeyAccess for it will be returned.
        /// If the requested key does not exist or is no valid then this method returns a IConfigKeyAccess instance with a non-empty ResultCode to indicate the source of the failure.
        /// </summary>
        protected virtual IConfigKeyAccess TryGetConfigKeyAccess(string methodName, IConfigKeyAccessSpec keyAccessSpec)
        {
            methodName = methodName + " [TryGetCKA]";

            keyAccessSpec = keyAccessSpec ?? emptyKeyAccessSpec;
            ConfigKeyAccessFlags flags = keyAccessSpec.Flags;
            string key = keyAccessSpec.Key;

            using (var eeTrace = new Logging.EnterExitTrace(TraceEmitter, methodName))
            {
                IConfigKeyAccess icka = null;
                bool tryUpdate = false;

                lock (mutex)
                {
                    // if the client is asking for a ReadOnlyOnce key and we have already seen this key as a read only once key then return a clone of the previously seen version.
                    if (flags.ReadOnlyOnce && readOnlyOnceKeyDictionary.TryGetValue(key ?? String.Empty, out icka) && icka != null)
                    {
                        if (!flags.SilenceLogging)
                            Trace.Trace.Emit("{0}: Using clone of prior instance:{1} [ROO]", methodName, icka.ToString(ToStringDetailLevel.Full));

                        icka = new ConfigKeyAccessImpl(icka, this) { Flags = flags };
                    }

                    // if this key has been seen before then return a clone of the previously seen one.
                    if (icka == null && allSeenKeysDictionary.TryGetValue(key ?? String.Empty, out icka) && icka != null)
                    {
                        if (!flags.SilenceLogging)
                            Trace.Trace.Emit("{0}: Starting with clone of prior instance:{1}", methodName, icka.ToString(ToStringDetailLevel.Full));

                        icka = new ConfigKeyAccessImpl(icka, this) { Flags = flags };

                        tryUpdate = !icka.ValueIsFixed;     // if we find an old key that does not have a fixed value then attempt to update the new key in case the persisted value has changed since the key was first seen.
                    }

                    if (icka == null)
                    {
                        foreach (IConfigKeyProvider provider in lockedProviderList.Array)
                        {
                            icka = provider.GetConfigKeyAccess(keyAccessSpec);

                            if (icka != null)
                            {
                                if (!flags.SilenceLogging)
                                    Trace.Trace.Emit("{0}: Found provider:{1}", methodName, provider.Name);

                                break;
                            }
                        }

                        if (icka != null)
                        {
                            allSeenKeysDictionary[key] = icka;
                            if (icka.Flags.ReadOnlyOnce)
                                readOnlyOnceKeyDictionary[key] = icka;

                            icka = new ConfigKeyAccessImpl(icka, this) { Flags = flags };
                        }
                        else
                        {
                            icka = new ConfigKeyAccessImpl(key, flags, this, null) { ProviderFlags = new ConfigKeyProviderFlags() { KeyWasNotFound = true } };

                            if (!flags.SilenceIssues && !flags.IsOptional)
                                Trace.Trace.Emit("{0}: {1}", methodName, icka.ResultCode);
                        }
                    }
                }

                if (tryUpdate)
                    TryUpdateConfigKeyAccess(methodName, icka);

                string ickaAsStr = icka.ToString();
                eeTrace.ExtraMessage = ickaAsStr;

                if (icka.IsUsable && !icka.Flags.SilenceLogging)
                    Trace.Trace.Emit("{0}: gave {1}", methodName, ickaAsStr);

                return icka;
            }
        }

        /// <summary>
        /// This method will attempt to get, and update, the current value for the given configKeyAccess from the KeySource it was previously found in.  
        /// Only keys that have been successfully found from a source can be updated.
        /// Returnst true if any aspect of the given key access was changed, otherwise returns false.
        /// </summary>
        /// <remarks>
        /// This method is intended for internal use only.  
        /// It assumes that the icka given is non-null, not ValueIsFixed.  As such it is assumed to have been previously found.
        /// </remarks>
        internal virtual bool TryUpdateConfigKeyAccess(string methodName, IConfigKeyAccess icka)
        {
            ConfigKeyAccessFlags flags = icka.Flags;

            using (var eeTrace = (!icka.Flags.SilenceLogging ? new Logging.EnterExitTrace(TraceEmitter, methodName) : null))
            {
                ValueContainer entryValue = icka.ValueContainer;
                string entryResultCode = icka.ResultCode;

                ValueContainer updatedValue = entryValue;
                string updatedResultCode = entryResultCode;

                ConfigKeyAccessImpl ckai = icka as ConfigKeyAccessImpl;

                if (ckai == null || ckai.Provider == null)
                {
                    if (icka.IsUsable)
                        updatedResultCode = "This IConfigKeyAccess object cannot be updated by this IConfig instance";
                }
                else
                {
                    IConfigKeyAccess updatedICKA = null;
                    lock (mutex)
                    {
                        updatedICKA = ckai.Provider.GetConfigKeyAccess(icka);
                    }

                    if (updatedICKA != null)
                    {
                        updatedValue = updatedICKA.ValueContainer;
                        updatedResultCode = updatedICKA.ResultCode;
                    }
                    else if (icka.IsUsable)
                    {
                        updatedResultCode = "Internal: Provider returned null IConfigKeyAccess during update attempt.";
                    }
                }

                bool valueChanged = false, resultCodeChanged = false;

                if (ckai != null && !entryValue.IsEqualTo(updatedValue))
                    ckai.ValueContainer = updatedValue;

                if (entryResultCode != updatedResultCode)
                    icka.ResultCode = updatedResultCode;

                if (valueChanged && !resultCodeChanged)
                {
                    if (!icka.Flags.SilenceLogging)
                        Logger.Debug.Emit("{0}: key '{1}' value updated to {2} [from:{3}]", methodName, icka.Key, icka.ValueContainer, entryValue);
                    eeTrace.ExtraMessage = "Value udpated";
                }
                else if (resultCodeChanged && !valueChanged)
                {
                    if (!icka.Flags.SilenceLogging)
                        Logger.Debug.Emit("{0}: key '{1}' result code changed to '{2}' [from:'{3}']", methodName, icka.Key, icka.ResultCode, entryResultCode);
                    eeTrace.ExtraMessage = "ResultCode udpated";
                }
                else if (valueChanged && resultCodeChanged)
                {
                    if (!icka.Flags.SilenceLogging)
                        Logger.Debug.Emit("{0}: key '{1}' value&rc updated to {2}/'{3}' [from:{4}/'{5}']", methodName, icka.Key, icka.ValueContainer, icka.ResultCode, entryValue, entryResultCode);
                    eeTrace.ExtraMessage = "Value and ResultCode udpated";
                }
                else
                {
                    eeTrace.ExtraMessage = "no change";
                }

                return (valueChanged || resultCodeChanged);
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
        protected Dictionary<string, IConfigKeyAccess> readOnlyOnceKeyDictionary = new Dictionary<string, IConfigKeyAccess>();

        /// <summary>
        /// Contains the set of all key access objects that have been seen by the IConfig instance so far.  
        /// Once a key has been seen, the IConfig instance will not search the providers list but will simply derive the new key access from the first seen one since it already knows
        /// which provider to use.
        /// </summary>
        protected Dictionary<string, IConfigKeyAccess> allSeenKeysDictionary = new Dictionary<string, IConfigKeyAccess>();

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
            string methodName = Fcns.CheckedFormat("{0}({1}, value:{2}, comment:{3})", new System.Diagnostics.StackFrame().GetMethod().Name, keyAccess.ToString(ToStringDetailLevel.ReferenceInfoOnly), valueContainer, commentStr);

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
            sb.CheckedAppendFormat("{0}(", new System.Diagnostics.StackFrame().GetMethod().Name);
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
                Dictionary<IConfigKeyProviderInfo, List<KeyValuePair<IConfigKeyAccess, ValueContainer>>> dictionaryOfListsOfItemsByProvider = new Dictionary<IConfigKeyProviderInfo, List<KeyValuePair<IConfigKeyAccess, ValueContainer>>>();

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

                    if (!dictionaryOfListsOfItemsByProvider.ContainsKey(providerInfo))
                        dictionaryOfListsOfItemsByProvider[providerInfo] = new List<KeyValuePair<IConfigKeyAccess, ValueContainer>>();

                    dictionaryOfListsOfItemsByProvider[providerInfo].Add(kvp);
                }

                lock (mutex)
                {
                    foreach (IConfigKeyProviderInfo providerInfo in dictionaryOfListsOfItemsByProvider.Keys)
                    {
                        IConfigKeyProvider provider = providerInfo as IConfigKeyProvider;

                        if (provider != null)
                        {
                            string ec = provider.SetValues(dictionaryOfListsOfItemsByProvider[providerInfo].ToArray(), commentStr);

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

                NotifyClientsOfChange();
            }

            return firstError ?? String.Empty;
        }

        #endregion
    }

    #endregion

    #region ConfigKeyAccessImpl

    /// <summary>
    /// implementation class used to store IConfigKeyMetaData information.  The corresponding public interace is immutable.  As long as all
    /// copies of this object type are returned using the public interface then the contents of this object type can also be viewed as immutable.
    /// </summary>
    public class ConfigKeyAccessImpl : ConfigKeyAccessSpec, IConfigKeyAccess
    {
        /// <summary>Normal constructor - caller is expected to use Property initializers.  If provider is not null then ProviderFlags will be set to provider.BaseFlags</summary>
        public ConfigKeyAccessImpl(string key, ConfigKeyAccessFlags flags, ConfigBase configBaseInstance, IConfigKeyProvider provider)
            : base(key, flags)
        {
            ConfigBaseInstance = configBaseInstance;

            Provider = provider;
            if (provider != null)
                ProviderFlags = provider.BaseFlags;
        }

        /// <summary>"copyish" constructor for making key access objects derived from other ones - in most cases this is for ReadOnlyOnce keys or Fixed keys</summary>
        public ConfigKeyAccessImpl(IConfigKeyAccess rhs, ConfigBase configBaseInstance)
            : base(rhs)
        {
            DerivedFromKey = rhs;

            ResultCode = rhs.ResultCode;
            ValueContainer = rhs.ValueContainer;
            HasValue = rhs.HasValue;

            ConfigBaseInstance = configBaseInstance;

            ProviderFlags = rhs.ProviderFlags;
            Provider = rhs.ProviderInfo as IConfigKeyProvider;      // this will produce null if the rhs's ProviderInfo object does not also implement IConfigKeyProvider
        }

        private ConfigBase ConfigBaseInstance { get; set; }
        private IConfigKeyAccess DerivedFromKey { get; set; }

        /// <summary>Gives the provider instance that is serving this key.</summary>
        public IConfigKeyProvider Provider { get; set; }

        private string ProviderName { get { return ((Provider != null) ? Provider.Name : String.Empty); } }

        #region IConfigKeyAccess Members

        /// <summary>
        /// Gives information (Name, BaseFlags) about the provider that is serving this key.  
        /// Also used internally to obtain the provider instance that is used to serve this key.
        /// </summary>
        public IConfigKeyProviderInfo ProviderInfo { get { return Provider; } }

        /// <summary>Gives the client access to the set of Flags from the key's provider.  Used to indicate Fixed keys and when the KeyWasNotFound</summary>
        public ConfigKeyProviderFlags ProviderFlags { get; internal set; }

        /// <summary>Returns a readonly copy of the key metadata INamedValueSet for this key as defined by the provider.</summary>
        public INamedValueSet KeyMetaData { get { return (keyMetaData ?? NamedValueSet.Empty); } set { keyMetaData = value.ConvertToReadOnly(); } }
        private INamedValueSet keyMetaData;

        /// <summary>Optional.  Gives a user oriented description of the key's purpose, use, and valid values.</summary>
        public string Description { get { return KeyMetaData["Description"].VC.GetValue<string>(false) ?? String.Empty; } }

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
                if (valueContainer.cvt == ContainerStorageType.String)
                    return valueContainer.GetValue<string>(ContainerStorageType.String, false, false);
                else if (valueContainer.cvt.IsReferenceType())
                    return valueContainer.GetValue<string>(ContainerStorageType.String, false, false);
                else if (valueContainer.IsNullOrNone)       // special case so that an empty container gives back ValueAsString as just null rather than "None"
                    return null;
                else
                    return valueContainer.ToString();
            }
        }

        public ValueContainer ValueContainer
        {
            get { return valueContainer; }
            set 
            { 
                valueContainer = value;
                HasValue = !value.IsNullOrNone;
            }
        }

        private Common.ValueContainer valueContainer;

        /// <summary>True if this KeyAccess object is usable (ResultCode is empty)</summary>
        public bool IsUsable { get { return String.IsNullOrEmpty(ResultCode); } }

        /// <summary>True if this KeyAccess object's ValueContainer contents are not null or None.  Generally this is false when the given key was not found.</summary>
        public bool HasValue { get; private set; }

        /// <summary>
        /// This method will refresh the ValueAsString property to represent the most recently accepted value for the corresponding key and provider.
        /// This method will have no effect if the flag indicates that the key is a ReadOnlyOnce key or if the provider indicates that the key is Fixed.
        /// Returns true if the ValueAsString value changed or false otherwise.
        /// </summary>
        public bool UpdateValue()
        {
            // UpdateValue does not do anything when the IConfigKeyAccess flags include Fixed or ReadOnlyOnce.
            if (ValueIsFixed || ConfigBaseInstance == null)
                return false;

            string methodName = Fcns.CheckedFormat("IConfigKeyAccess.UpdateValue[key:'{0}']", Key);

            return ConfigBaseInstance.TryUpdateConfigKeyAccess(methodName, this);
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
            string metaDataStr = KeyMetaData.ToString(false, true);

            switch (detailLevel)
            {
                case ToStringDetailLevel.ReferenceInfoOnly:
                    return Fcns.CheckedFormat("key:'{0}' flags:{1} md:{2} {3}", Key, Flags, metaDataStr, ProviderName, ProviderFlags);
                case ToStringDetailLevel.Nominal:
                case ToStringDetailLevel.Full:
                default:
                    if (IsUsable)
                        return Fcns.CheckedFormat("key:'{0}' Value:{1} flags:{2} md:{3} {4}", Key, ValueContainer, Flags, metaDataStr, ProviderFlags);
                    else
                        return Fcns.CheckedFormat("key:'{0}' Value:{1} flags:{2} md:{3} {4} ec:'{5}'", Key, ValueContainer, Flags, metaDataStr, ProviderFlags, ResultCode);
            }
        }

        #endregion
    }

    #endregion
}
