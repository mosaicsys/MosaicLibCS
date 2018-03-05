//-------------------------------------------------------------------
/*! @file ScanEngine.cs
 *  @brief This file defines the basic interface and base Part for the scan engine pattern being created here
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
using System.Linq;
using System.Collections.Generic;

using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.PartsLib.Scan.ScanEngine
{
    #region ScanEnginePartConfig

    /// <summary>
    /// External configuration object used with the <see cref="ScanEnginePart"/> class
    /// </summary>
    public class ScanEnginePartConfig
    {
        /// <summary>
        /// Default constructor.  Caller provides ScanEnginePart name which is also the prefix for any config keys that are used to Setup this value.
        /// <para/>defaults: NominalRateInHz = 30.0
        /// </summary>
        public ScanEnginePartConfig()
        {
            NominalRateInHz = 30.0;
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        public ScanEnginePartConfig(ScanEnginePartConfig other)
        {
            Name = other.Name;
            IConfig = other.IConfig ?? Config.Instance;
            IValuesInterconnection fallbackIVI = other.PartBaseIVI ?? other.PlugInsIVI ?? Values.Instance;
            PartBaseIVI = other.PartBaseIVI ?? fallbackIVI;
            PlugInsIVI = other.PlugInsIVI ?? fallbackIVI;
            Installed = other.Installed;
            NominalRateInHz = other.NominalRateInHz;
            _pluginsToAddList = other._pluginsToAddList;        // no need to clone: it is already readonly.
            _pluginsToAddSet = other._pluginsToAddSet;
        }

        /// <summary>
        /// SimEngine part Name (aka PartID).  Also used as a default prefix for any config keys that are used to Setup this object and related plugin config keys.
        /// </summary>
        public string Name { get; set; }

        public IConfig IConfig { get; set; }
        public IValuesInterconnection PartBaseIVI { get; set; }
        public IValuesInterconnection PlugInsIVI { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true)]
        public bool Installed { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true)]
        public double NominalRateInHz { get; set; }

        public TimeSpan NominalServicePeriod
        {
            get { return TimeSpan.FromSeconds((NominalRateInHz > 0.0) ? 1.0 / NominalRateInHz : 1.0); }
            set { double period = value.TotalSeconds; NominalRateInHz = (period > 0.0 ? 1.0/period : 0.0); }
        }

        public IList<IScanEnginePlugin> PluginsToAddList { get { return _pluginsToAddList; } set { _pluginsToAddList = new ReadOnlyIList<IScanEnginePlugin>(value); } }
        private IList<IScanEnginePlugin> _pluginsToAddList;

        public IEnumerable<IScanEnginePlugin> PluginsToAddSet { get { return _pluginsToAddSet ?? _pluginsToAddList ?? emptyPluginArray; } set { _pluginsToAddSet = value; } }
        private IEnumerable<IScanEnginePlugin> _pluginsToAddSet = null;

        /// <summary>
        /// Uses Modular.Config to Setup portions of this instance.  Requires and issueEmitter and a valueEmitter to use (use null or Logging.NullEmitter to suppress output).
        /// Prefixes this object's Name with period in front of the annotated members that can be filled in from config keys.  All such config keys are ReadOnlyOnce = true and IsOptional = true.
        /// </summary>
        public ScanEnginePartConfig Setup(Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<ScanEnginePartConfig> adapter;

            Name = Name ?? String.Empty;

            // update values from any lpmInstanceName derived keys.
            adapter = new ConfigValueSetAdapter<ScanEnginePartConfig>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(Name + ".");

            return this;
        }

        public static readonly IScanEnginePlugin[] emptyPluginArray = EmptyArrayFactory<IScanEnginePlugin>.Instance;
    }

    #endregion

    #region IScanEnginePlugin

    /// <summary>
    /// This defines the interface that a <see cref="ScanEnginePart"/> uses to interact with each of the plugins that are installed under that engine.
    /// This interface allows the engine to setup each plugin to support Modular.Config based configuration and to establish other interconnections.
    /// It also defines the primary methods that make up the scan pattern as UpdateInputs, Service and UpdateOutputs.  See the ScanEnginePart description for more details
    /// on the scan pattern and see the individual methods below for more details on the intended purpose for each.
    /// </summary>
    public interface IScanEnginePlugin
    {
        /// <summary>
        /// Gives the ScanEnginePart access to the name of the plugin
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The ScanEnginePart calls this method for newly added plugins to let the plugin perform pre-flight configuration and interconnection.  The plugin is passed the name
        /// of the engine so that the plugin may use it as a prefix for any config keys that the plugin supports the use of or requires.  Plugins are responsible for all
        /// naming of config key's that it depends on and for naming of all interconnections that it depends on such as named values.
        /// <para/>Current expectation is that config keys will be found under {engineName}.{pluginName}. while named values will only be prefixed with {pluginName}.
        /// </summary>
        void Setup(string scanEnginePartName, IConfig pluginsIConfig, IValuesInterconnection pluginsIVI);

        /// <summary>
        /// The engine's scan is divided into phases where the first phase is to call all of the plugin's UpdateInputs methods.  This method is expected to simply
        /// update the plugin's copy of all values it obtains from other sources that it is interconnected with.
        /// </summary>
        void UpdateInputs();

        /// <summary>
        /// The engine's scan is divided into phases where the second phase is to call all of the plugin's Service methods.  
        /// Plugin's are expected to implement all of their logic tree, calculation tree, and modeling behavior in this method.
        /// This method is passed a dynamically calculated measuredServiceInterval value that gives the plugin a reasonable approximation of the actual interval that
        /// is elapsing between calls to this Service method.
        /// </summary>
        void Service(TimeSpan measuredServiceInterval, QpcTimeStamp timeStampNow);

        /// <summary>
        /// The engine's scan is divided into phases where the third phase is to call all of the plugin's UpdateOutput methods.  This method is expected to simply
        /// update the external values that the plugin provides that may be interconnected with other plugins and with other parts of the system.
        /// </summary>
        void UpdateOutputs();
    }

    #endregion

    #region ScanEnginePluginBase classes

    /// <summary>
    /// This is the primary IScanEnginePlugin base class.  
    /// It requires three template types that are used with ValueSet type adapters to provide a default implementation for the config, input and output values support
    /// including providing default implmentations for the Setup, UpdateInputs and UpdateOutputs methods.  
    /// Derived classes need only provide the template class types and implement the Service method.
    /// </summary>
    /// <typeparam name="TConfigValueSetType">Configuration ValueSet type.  Must be a public class that supports new().  Must use MosaicLib.Modular.Config.Attributes.ConfigItem to annotate public settable members that will receive config key values.</typeparam>
    /// <typeparam name="TInputValueSetType">Inputs ValueSet type.  Must be a public class that supports new().  Must use MosaicLib.Modular.Interconnect.Values.Attributes.ValueSetItem to annotate public get/set member that will receive input values.</typeparam>
    /// <typeparam name="TOutputValueSetType">Outputs ValueSet type.  Must be a public class that supports new().  Must use MosaicLib.Modular.Interconnect.Values.Attributes.ValueSetItem to annotate public get/set member that will generally be the sources of output values.</typeparam>
    public abstract class ScanEnginePluginBase<TConfigValueSetType, TInputValueSetType, TOutputValueSetType> : ScanEnginePluginBase
        where TConfigValueSetType : class, ICloneable, new()
        where TInputValueSetType : class, new()
        where TOutputValueSetType : class, new()
    {
        public ScanEnginePluginBase(string name) : this(name, default(TConfigValueSetType)) { }

        public ScanEnginePluginBase(string name, TConfigValueSetType initialConfig) 
            : base(name) 
        {
            configAdapter.SetupIssueEmitter = configAdapter.UpdateIssueEmitter = configAdapter.ValueNoteEmitter = Logger.Debug;
            if (initialConfig != null)
                configAdapter.ValueSet = (TConfigValueSetType) (initialConfig.Clone());

            inputAdapter.IssueEmitter = outputAdapter.IssueEmitter = Logger.Debug;
            inputAdapter.ValueNoteEmitter = outputAdapter.ValueNoteEmitter = Logging.NullEmitter;
        }

        protected TConfigValueSetType ConfigValues { get { return configAdapter.ValueSet; } }
        protected TInputValueSetType InputValues { get { return inputAdapter.ValueSet; } }
        protected TOutputValueSetType OutputValues { get { return outputAdapter.ValueSet; } }

        private ConfigValueSetAdapter<TConfigValueSetType> configAdapter = new ConfigValueSetAdapter<TConfigValueSetType>() { ValueSet = new TConfigValueSetType() };
        private ValueSetAdapter<TInputValueSetType> inputAdapter = new ValueSetAdapter<TInputValueSetType>() { ValueSet = new TInputValueSetType() };
        private ValueSetAdapter<TOutputValueSetType> outputAdapter = new ValueSetAdapter<TOutputValueSetType>() { ValueSet = new TOutputValueSetType() };

        public override void Setup(string scanEnginePartName, IConfig pluginsIConfig, IValuesInterconnection pluginsIVI)
        {
            configAdapter.Setup(pluginsIConfig, "{0}.{1}.".CheckedFormat(scanEnginePartName, Name), scanEnginePartName, Name);
            inputAdapter.Setup(pluginsIVI, "{0}.".CheckedFormat(Name), scanEnginePartName, Name).Set();
            outputAdapter.Setup(pluginsIVI, "{0}.".CheckedFormat(Name), scanEnginePartName, Name).Set();
        }

        public override void UpdateInputs()
        {
            if (configAdapter.IsUpdateNeeded)
                configAdapter.Update();

            inputAdapter.Update();
        }

        public override void UpdateOutputs()
        {
            outputAdapter.Set();
        }
    }

    /// <summary>
    /// A really simple partially abstract base class to use for an IScanEnginePlugin
    /// </summary>
    public abstract class ScanEnginePluginBase : IScanEnginePlugin
    {
        public ScanEnginePluginBase(string name) 
        { 
            Name = name;
            Logger = new Logging.Logger(name, Logging.LookupDistributionGroupName, Logging.LogGate.All);
        }

        protected Logging.Logger Logger { get; set; }

        #region IScanEnginePlugin

        public string Name { get; private set; }

        public virtual void Setup(string scanEnginePartName, IConfig pluginsIConfig, IValuesInterconnection pluginsIVI) { }

        public virtual void UpdateInputs() { }

        public abstract void Service(TimeSpan measuredServiceInterval, QpcTimeStamp timeStampNow);

        public virtual void UpdateOutputs() { }

        #endregion

        #region Rng

        protected Random Rng { get { return (_rng ?? (_rng = new Random())); } }
        private Random _rng = null;
        
        #endregion
    }

    #endregion

    #region ScanEnginePart, IScanEnginePart

    /// <summary>
    /// SimpleActivePart used to implement the pluggable Scan Engine.  
    /// This part is generally used for scan interval based logic, calculations, modeling, etc. for PLC sytle logic, and simulation.
    /// This part is given a set of one or more plugin objects that provide its actual externally visible functionality.
    /// Internally this part implements a simple scan loop that consists of the following phases: UpdateInputs, Service (calculate), UpdateOutputs, Wait.
    /// During each active phase, the corresponding plugin interface method is called accross all of the plugins
    /// </summary>
    public class ScanEnginePart : MosaicLib.Modular.Part.SimpleActivePartBase
    {
        #region Construction

        public ScanEnginePart(ScanEnginePartConfig config, IEnumerable<IScanEnginePlugin> pluginSet)
            : this(config, pluginSet.ToArray())
        { }

        public ScanEnginePart(ScanEnginePartConfig config, params IScanEnginePlugin[] pluginParamsArray)
            : base(config.Name, initialSettings: SimpleActivePartBaseSettings.DefaultVersion0.Build(waitTimeLimit: config.NominalServicePeriod, partBaseIVI: config.PartBaseIVI))
        {
            ActionLoggingConfig = Modular.Action.ActionLoggingConfig.Debug_Debug_Trace_Trace;    // redefine the log levels for actions

            Config = new ScanEnginePartConfig(config);

            PrivateBaseState.SetSimulated(false, true);

            scanEngineHelper = new ScanEngineHelper(config, pluginParamsArray);
        }

        protected ScanEnginePartConfig Config { get; private set; }
        private ScanEngineHelper scanEngineHelper;

        #endregion

        #region Add and AddRange methods

        public ScanEnginePart Add(IScanEnginePlugin plugin)
        {
            return Add(new[] { plugin });
        }

        public ScanEnginePart Add(params IScanEnginePlugin[] pluginParamsArray)
        {
            if (HasBeenStarted)
                throw new System.InvalidOperationException("{0} is not valid after part has been Started".CheckedFormat(Fcns.CurrentMethodName));

            scanEngineHelper.AddRange(pluginParamsArray);

            return this;
        }

        public ScanEnginePart AddRange(IEnumerable<IScanEnginePlugin> pluginSet)
        {
            if (HasBeenStarted)
                throw new System.InvalidOperationException("{0} is not valid after part has been Started".CheckedFormat(Fcns.CurrentMethodName));

            return AddRange(pluginSet.ToArray());
        }

        public ScanEnginePart Add<TValueType>(DelegateItemSpec<TValueType> delegateItemSpec)
        {
            if (HasBeenStarted)
                throw new System.InvalidOperationException("{0} is not valid after part has been Started".CheckedFormat(Fcns.CurrentMethodName));

            scanEngineHelper.Add(delegateItemSpec);

            return this;
        }

        #endregion

        #region standard and required SimpleAtivePart methods/overrides

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            SetBaseState(UseState.Online, "By explicit request", true);
            return String.Empty;
        }

        bool hasHelperBeenSetup = false;

        protected override void PerformMainLoopService() 
        {
            if (!hasHelperBeenSetup)
            {
                scanEngineHelper.Setup(PartID);
                hasHelperBeenSetup = true;
            }

            scanEngineHelper.Service();
        }

        #endregion
    }

    #endregion

    #region ScanEngineHelper

    /// <summary>
    /// Helper class used to implement the pluggable Scan Engine.  
    /// This helper is generally used for scan interval based logic, calculations, modeling, etc. for PLC sytle logic, and simulation.
    /// This helper is given a set of one or more plugin objects that provide its actual externally visible functionality.
    /// Internally this part implements a simple Service method that consists of the following phases: UpdateInputs, Service (calculate), UpdateOutputs.
    /// The client is required to implement whatever the desired logic is so that Service method is called on the expected interval.
    /// </summary>
    public class ScanEngineHelper
    {
        public ScanEngineHelper(ScanEnginePartConfig config, params IScanEnginePlugin [] pluginParamsArray)
        {
            Config = new ScanEnginePartConfig(config);
            Logger = new Logging.Logger(Name);

            AddRange(Config.PluginsToAddSet);
            AddRange(pluginParamsArray);
        }

        protected ScanEnginePartConfig Config { get; private set; }
        public Logging.ILogger Logger { get; private set; }

        public string Name { get { return Config.Name; } }

        private DelegateValueSetAdapter delegateValueSetAdapter = new DelegateValueSetAdapter() { OptimizeSets = true };

        public ScanEngineHelper Add(IScanEnginePlugin plugin)
        {
            if (hasBeenSetup)
                throw new System.InvalidOperationException("{0} is not valid after object has been Setup or Serviced".CheckedFormat(Fcns.CurrentMethodName));

            return AddRange(new[] { plugin });
        }

        public ScanEngineHelper Add(params IScanEnginePlugin[] pluginArray)
        {
            if (hasBeenSetup)
                throw new System.InvalidOperationException("{0} is not valid after object has been Setup or Serviced".CheckedFormat(Fcns.CurrentMethodName));

            return AddRange(pluginArray);
        }

        public ScanEngineHelper AddRange(IEnumerable<IScanEnginePlugin> pluginSet)
        {
            if (hasBeenSetup)
                throw new System.InvalidOperationException("{0} is not valid after object has been Setup or Serviced".CheckedFormat(Fcns.CurrentMethodName));

            foreach (IScanEnginePlugin plugin in pluginSet ?? emptyPluginArray)
            {
                Logger.Debug.Emit("Added plugin '{0}'", plugin.Name);

                pluginList.Add(plugin);
            }

            return this;
        }

        public ScanEngineHelper Add<TValueType>(DelegateItemSpec<TValueType> delegateItemSpec)
        {
            if (hasBeenSetup)
                throw new System.InvalidOperationException("{0} is not valid after object has been Setup or Serviced".CheckedFormat(Fcns.CurrentMethodName));

            delegateValueSetAdapter.Add(delegateItemSpec);

            return this;
        }

        bool hasBeenSetup = false;

        public virtual void Setup(params string [] paramsStringArray)
        {
            delegateValueSetAdapter.IssueEmitter = Logger.Debug;
            delegateValueSetAdapter.ValueNoteEmitter = Logger.Trace;
            delegateValueSetAdapter.EmitValueNoteNoChangeMessages = false;

            delegateValueSetAdapter.Setup(Config.PlugInsIVI, new [] { Name }.Concat(paramsStringArray).ToArray()).Set().Update();

            // Setup
            foreach (IScanEnginePlugin plugin in pluginList.ToArray())
            {
                try
                {
                    plugin.Setup(Name, Config.IConfig, Config.PlugInsIVI);
                }
                catch (System.Exception ex)
                {
                    Logger.Error.Emit("Setup of plugin '{0}' failed: {1}", plugin.Name, ex.ToString(ExceptionFormat.TypeAndMessage));
                    pluginList.Remove(plugin);
                }
            }

            pluginArray = pluginList.ToArray();

            hasBeenSetup = true;
        }

        public void Service()
        {
            Service(Config.NominalServicePeriod, QpcTimeStamp.Now);
        }

        public virtual void Service(TimeSpan measuredServiceInterval, QpcTimeStamp timeStampNow)
        {
            if (pluginArray == null)
                Setup(string.Empty);

            IScanEnginePlugin currentPlugin = null;
            try
            {
                if (delegateValueSetAdapter.IsUpdateNeeded)
                    delegateValueSetAdapter.Update();

                // UpdateInputs
                foreach (IScanEnginePlugin plugin in pluginArray)
                {
                    currentPlugin = plugin;
                    plugin.UpdateInputs();
                }

                // Service
                foreach (IScanEnginePlugin plugin in pluginArray)
                {
                    currentPlugin = plugin;
                    plugin.Service(measuredServiceInterval, timeStampNow);
                }

                // UpdateOutputs
                foreach (IScanEnginePlugin plugin in pluginArray)
                {
                    currentPlugin = plugin;
                    plugin.UpdateOutputs();
                }

                delegateValueSetAdapter.Set();
            }
            catch (System.Exception ex)
            {
                if (currentPlugin != null)
                {
                    pluginList.Remove(currentPlugin);
                    pluginArray = null;

                    Logger.Error.Emit("Plugin '{0}' removed after it generated unexpected exception: {1}", currentPlugin.Name, ex.ToString(ExceptionFormat.TypeAndMessage));
                    Logger.Debug.Emit(ex.ToString(ExceptionFormat.Full));
                }
                else
                {
                    Logger.Debug.Emit("Encountered unexpected exception in main loop (no current plugin): {0}", ex.ToString(ExceptionFormat.Full));
                }
            }
        }

        #region pluginList, pluginArray, emptyPluginArray;

        protected List<IScanEnginePlugin> pluginList = new List<IScanEnginePlugin>();
        protected IScanEnginePlugin[] pluginArray;
        public static readonly IScanEnginePlugin[] emptyPluginArray = EmptyArrayFactory<IScanEnginePlugin>.Instance;

        #endregion
    }

    #endregion
}
