//-------------------------------------------------------------------
/*! @file CommonSetup.cs
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using System.Windows.Threading;
using System.Windows;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;

namespace MosaicLib.WPF.Common
{
    using MosaicLib;        // apparently this makes MosaicLib get searched before MosaicLib.WPF.Logging for resolving symbols here.

    /// <summary>
    /// This enumeration is used with some Config.Logging keys to configure how standard logging is setup when using this AppSetup helper class
    /// <para/>Disabled (0x00), IncludeAlways (0x01), IncludeWhenDebuggerAttached (0x02), NonQueued (0x04)
    /// </summary>
    [Flags]
    public enum LogMessageHandlerSettingFlags : int
    {
        Disabled = 0x00,
        IncludeAlways = 0x01,
        IncludeWhenDebuggerAttached = 0x02,
        NonQueued = 0x04,
    }

    /// <summary>
    /// Static helper methods with methods that can be used to handle OnStartup, OnDeactivated and OnExit events (using correspondingly named HandleYYY methods)
    /// </summary>
    public static partial class AppSetup
    {
        /// <summary>
        /// Defines the MesgType that is used for AppEvents that are generated here.  Defaults to MesgType.Signif
        /// </summary>
        public static Logging.MesgType AppEventMesgType { get { return appEventMesgType; } set { appEventMesgType = value; } }
        private static Logging.MesgType appEventMesgType = Logging.MesgType.Signif;

        /// <summary>
        /// This method generates a useful default LogBaseName which is derived from the FullName of the calling assembly by taking the first portion up to the first ','
        /// </summary>
        public static string DefaultLogBaseName { get { return CallerAssembly.GetAssemblyShortName(); } }

        /// <summary>
        /// Gets the <paramref name="assembly"/>'s "short name".
        /// Gets the FullName from the given <paramref name="assembly"/> and returns the parts that comes before the first comma (',')
        /// <para/>for example "TestAssembly, Version=0.1.7.0, Culture=neutral, PublicKeyToken=null" => "TestAssembly"
        /// </summary>
        public static string GetAssemblyShortName(this System.Reflection.Assembly assembly, string fallbackValue = "$$GivenAssemblyIsNull$$")
        {
            if (assembly != null)
                return assembly.FullName.Split(new[] { ',' }, 2).SafeAccess(0); // split off the "name" of the assembly from the other parts that make up its "full" name.
            else
                return fallbackValue;
        }

        /// <summary>
        /// Returns the first assembly that has a different FullName than this methods DeclaringType.Assembly, or if there are no other such assemblies within 10 stack frames up from this method
        /// then this property returns the this method's DeclaringType.Assembly itself.
        /// </summary>
        public static System.Reflection.Assembly CallerAssembly
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get
            {
                try
                {
                    System.Reflection.Assembly thisMethodAssembly = new System.Diagnostics.StackFrame().GetMethod().DeclaringType.Assembly;
                    string thieMethodAssemblyFullName = thisMethodAssembly.FullName;

                    System.Reflection.Assembly callStackLevelAssembly = null;

                    for (int callStackLevel = 1; callStackLevel < 10; callStackLevel++)
                    {
                        try
                        {
                            callStackLevelAssembly = null;
                            callStackLevelAssembly = new System.Diagnostics.StackFrame(callStackLevel).GetMethod().DeclaringType.Assembly;

                            if (callStackLevelAssembly.FullName != thieMethodAssemblyFullName)
                                break;
                        }
                        catch { }
                    }

                    return (callStackLevelAssembly ?? thisMethodAssembly);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// An enumeration of the supported log message handler types that may be used as the file log ring for the default logging group.
        /// </summary>
        public enum FileRingLogMessageHandlerType
        {
            None = 0,
            TextFileRotationDirectoryLogMessageHandler,
            TextFileDateTreeDirectoryLogMessageHandler,
        }

        /// <summary>
        /// Gives get/set access to the AppSetup's default main logger type.  Defaults to FileRingLogMessageHandlerType.TextFileRotationDirectoryLogMessageHandler.
        /// This value will be used during HandleOnStartup to define which type of main logger the setup will use.
        /// </summary>
        public static FileRingLogMessageHandlerType DefaultMainLoggerType { get { return defaultMainLoggerType; } set { defaultMainLoggerType = value; } }
        private static FileRingLogMessageHandlerType defaultMainLoggerType = FileRingLogMessageHandlerType.TextFileRotationDirectoryLogMessageHandler;

        public static UnhandledExceptionEventHandler UnhandledExceptionEventHandler { get { return _unhandledExceptionEventHandler; } set { _unhandledExceptionEventHandler = value;} }
        private static UnhandledExceptionEventHandler _unhandledExceptionEventHandler = DefaultUnhandledExceptionEventHandler;

        public static void DefaultUnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                sb.CheckedAppendFormat("{0} has been triggered", Fcns.CurrentMethodName);
                sb.AppendLine();
                sb.CheckedAppendFormat(" DateTime   : {0:o}", DateTime.Now);
                sb.AppendLine();
                sb.CheckedAppendFormat(" QpcTime    : {0:f6}", Time.QpcTimeStamp.Now.Time);
                sb.AppendLine();
                sb.AppendLine();

                System.Exception ex = e.ExceptionObject as System.Exception;
                if (ex != null)
                {
                    sb.AppendLine("System.Exception:");

                    sb.CheckedAppendFormat(" Type       : {0}", ex.GetType());
                    sb.AppendLine();

                    sb.CheckedAppendFormat(" Message    : {0}", ex.Message);
                    sb.AppendLine();

                    sb.AppendLine(" Stack Trace:");
                    sb.CheckedAppendFormat("{0}", ex.StackTrace);
                    sb.AppendLine();

                    if (ex.Source != null)
                    {
                        sb.CheckedAppendFormat(" Source     : {0}", ex.Source);
                        sb.AppendLine();
                    }

                    if (ex.Data != null)
                    {
                        sb.AppendLine(" Data       :");
                        object[] keysArray = ex.Data.Keys.SafeToArray<object>();
                        object[] valuesArray = ex.Data.Values.SafeToArray<object>();
                        for (int rowIdx = 0; rowIdx < keysArray.Length; rowIdx++)
                        {
                            sb.CheckedAppendFormat("  Row {0:d3}   : {1}={2}", (rowIdx + 1), keysArray[rowIdx], valuesArray[rowIdx]);
                            sb.AppendLine();
                        }
                    }

                    if (ex.TargetSite != null)
                    {
                        sb.CheckedAppendFormat(" TargetSite : {0}", ex.TargetSite);
                        sb.AppendLine();
                    }

                    if (ex.HelpLink != null)
                    {
                        sb.CheckedAppendFormat(" Help Link  : {0}", ex.HelpLink);
                        sb.AppendLine();
                    }

                    if (ex.InnerException != null)
                    {
                        sb.CheckedAppendFormat(" Inner Exception : {0}", ex.InnerException.ToString(ExceptionFormat.TypeAndMessage | ExceptionFormat.IncludeStackTrace));
                        sb.AppendLine();
                    }
                }
                else
                {
                    Type eoType = (e.ExceptionObject != null ? e.ExceptionObject.GetType() : null);

                    sb.AppendLine("Unrecognized Exception Object:");
                    sb.CheckedAppendFormat("   Type:{0}", eoType.MapDefaultTo<object>("[Null]"));
                    sb.AppendLine();
                    sb.CheckedAppendFormat(" Object:{0}", e.ExceptionObject.MapDefaultTo("[Null]"));
                    sb.AppendLine();
                }

                if (sender != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("Sender:");
                    sb.CheckedAppendFormat("   Type:{0}", sender.GetType());
                    sb.AppendLine();
                    sb.CheckedAppendFormat(" Object:{0}", sender);
                    sb.AppendLine();
                }

                if (e.IsTerminating)
                {
                    sb.AppendLine();
                    sb.AppendLine("IsTerminating was set");
                }

                string text = sb.ToString();

                System.IO.File.WriteAllText(uehFileWritePath, text);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("{0}: generated unexpected exception: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessage | ExceptionFormat.IncludeStackTrace));
            }
        }

        private static string uehFileWritePath = "UnhandledExceptionReport.txt";

        /// <summary>
        /// Basic HandleOnStartup method signature.  Caller provides logBaseName.  
        /// Program's command line arguments will be obtained from the given StartupEventArgs value.  These will be used with the (optional) initial setup of Modular.Config.
        /// appLogger will be assigned to a new logger (this is expected to be used by the client in calls to later HandleYYY methods).
        /// <para/>See the description of the full HandleOnStartup method variant for more details.
        /// </summary>
        public static void HandleOnStartup(StartupEventArgs e, ref Logging.Logger appLogger, string logBaseName = null, bool useSetLMH = true, bool enableUEH = true)
        {
            string[] args = (e != null) ? e.Args : null;
            HandleOnStartup(ref args, ref appLogger, logBaseName: logBaseName, addWPFLMH: false, addSetLMH: (e != null) && useSetLMH, enableUEH: enableUEH);
        }

        /// <summary>
        /// Basic HandleOnStartup method signature.  Caller provides logBaseName.  
        /// Program's command line arguments will be obtained from the given StartupEventArgs value.  These will be used with the (optional) initial setup of Modular.Config.
        /// appLogger will be assigned to a new logger (this is expected to be used by the client in calls to later HandleYYY methods).
        /// <para/>See the description and remarks for the full HandleOnStartup method variant for more details.
        /// <para/>The following is a list of well known named values that are supported by this method:
        /// <list type="bullet">
        /// <item>logBaseName: optional name used in the default log file names that are generated.  When null the hosting assembly's name will be used.  Also used to name the main thread if it has not already been named.</item>
        /// <item>addWPFLMH: no longer supported - ignored</item>
        /// <item>addSetLMH: pass as true to enable creating a set based log message handler.</item>
        /// <item>enableUEH: if true, this method will install an unhandled exception handler, if none has already been established.  Set to false to disable this behavior.</item>
        /// <item>providerSelect: set to StandardProviderSelect value to override the default behavior of StandardProviderSelect.All.  This value will be ignored if there are already any providers registered with Config.Instance when this method is called.  If the given argsRef array value is null then MainArgs will be excluded from this value.</item>
        /// <item>uehFileWritePath: may be used to override the config key value (Config.UnhandledExceptionEventHandler.FilePath), and/or its initial value.  Must be non-empty to enable setting up an UEH.</item>
        /// <item>mainLoggerType: may be used to override the config key value (Config.Logging.MainLogger.Type), and/or its initial value.  When neither this parameter, nor the configuration key are found to give a valid FileRingLogMessageHandlerType value, the static DefaultMainLoggerType is used.</item>
        /// <item>diagTraceLMHSettingFlags, setLMHSettingFlags: may be used to override the corresponding config key values (Config.Logging.DiagnosticTrace/Set), and/or their initial values. (LogMessageHandlerSettingFlags)</item>
        /// <item>setName, setCapacity, setLogGate: may be used to override the corresponding config key values (Config.Logging.Set.Name/Capacity/LogGate), and/or their initial values.</item>
        /// <item>appEventMesgType: may be used to define the MesgType to be used for App Event messages.  When present, this key's value is used to set the AppEventMesgType.</item>
        /// <item>traceLoggingGroupName: may be used to override the config key value (Config.Logging.TraceRing.LGID) and/or its initial value.</item>
        /// <item>traceRingLinkFromDefaultGroup, traceRingLinkToDefaultGroup: when present these boolean values control the corresponding function without regard to the contents of the corresponding modular config values.</item>
        /// <item>preloadMainFileRingLoggingConfigFromNVS, preloadMainDateTreeLoggingConfigFromNVS, preloadTraceFileRingLoggingConfigFromNVS: allows the client to specify an NVS that is used to setup the underlying logging config object using a NamedValueSetAdapter before the Modular.Config based values are applied.</item>
        /// </list>
        /// </summary>
        public static void HandleOnStartup(StartupEventArgs e, ref Logging.Logger appLogger, INamedValueSet nvs, System.Reflection.Assembly hostingAssy = null)
        {
            string[] args = (e != null) ? e.Args : null;
            HandleOnStartup(ref args, ref appLogger, nvs: nvs, hostingAssy: hostingAssy);
        }

        /// <summary>
        /// Full HandleOnStartup method signature.
        /// argsRef line arguments string array will be used with the (optional) initial setup of Modular.Config.  This array will be replaced with one that has the consumed parameters removed from it.
        /// appLogger will be assigned to a new logger (this is expected to be used by the client in calls to later HandleYYY methods).
        /// logBaseName is used to define the name of the logger instances and will appear in the resulting output log file file names.
        /// When addWPFLMH is passed as true then this method will also add an instance of the WpfLogMessageHandlerToolBase to the default log distribution group.
        /// </summary>
        public static void HandleOnStartup(ref string[] argsRef, ref Logging.Logger appLogger, string logBaseName = null, bool addWPFLMH = false, bool addSetLMH = false, bool enableUEH = true)
        {
            HandleOnStartup(ref argsRef, ref appLogger, new NamedValueSet()
            {
                { "logBaseName", logBaseName },
                { "addWPFLMH", addWPFLMH },
                { "addSetLMH", addSetLMH },
                { "enableUEH", enableUEH },
            });
        }

        /// <summary>
        /// Full HandleOnStartup method signature.
        /// <paramref name="argsRef"/> line arguments string array will be used with the (optional) initial setup of Modular.Config.  
        /// If this parameter is non-null, it will be processed to extract the key=value items it contains, and will be replaced with a new array that has the consumed key=value items removed from it.
        /// <paramref name="appLogger"/> will be assigned to a new logger (this is expected to be used by the client in calls to later HandleYYY methods).
        /// <paramref name="nvs"/> is used to pass all directly caller configurable values that are supported by this method (see remarks section below)
        /// <paramref name="hostingAssy"/> allows the caller to provide the hosting assembly that will be used.  If null then the CallerAssembly will be used as the hosting assembly.
        /// <list type="bullet">
        /// <item>logBaseName: optional name used in the default log file names that are generated.  When null the hosting assembly's name will be used.  Also used to name the main thread if it has not already been named.</item>
        /// <item>addWPFLMH: no longer supported - ignored</item>
        /// <item>addSetLMH: pass as true to enable creating a set based log message handler.</item>
        /// <item>enableUEH: if true, this method will install an unhandled exception handler, if none has already been established.  Set to false to disable this behavior.</item>
        /// <item>providerSelect: set to StandardProviderSelect value to override the default behavior of StandardProviderSelect.All.  This value will be ignored if there are already any providers registered with Config.Instance when this method is called.  If the given argsRef array value is null then MainArgs will be excluded from this value.</item>
        /// <item>uehFileWritePath: may be used to override the config key value (Config.UnhandledExceptionEventHandler.FilePath), and/or its initial value.  Must be non-empty to enable setting up an UEH.</item>
        /// <item>mainLoggerType: may be used to override the config key value (Config.Logging.MainLogger.Type), and/or its initial value.  When neither this parameter, nor the configuration key are found to give a valid FileRingLogMessageHandlerType value, the static DefaultMainLoggerType is used.</item>
        /// <item>diagTraceLMHSettingFlags, setLMHSettingFlags: may be used to override the corresponding config key values (Config.Logging.DiagnosticTrace/Set), and/or their initial values. (LogMessageHandlerSettingFlags)</item>
        /// <item>setName, setCapacity, setLogGate: may be used to override the corresponding config key values (Config.Logging.Set.Name/Capacity/LogGate), and/or their initial values. [defaults: LogMessageHistory, 1000, Debug]</item>
        /// <item>appEventMesgType: may be used to define the MesgType to be used for App Event messages.  When present, this key's value is used to set the AppEventMesgType. [default: Signif]</item>
        /// <item>traceLoggingGroupName: may be used to override the config key value (Config.Logging.TraceRing.LGID) and/or its initial value.</item>
        /// <item>traceRingLinkFromDefaultGroup, traceRingLinkToDefaultGroup: when present these boolean values control the corresponding function without regard to the contents of the corresponding modular config values.</item>
        /// <item>preloadMainFileRingLoggingConfigFromNVS, preloadMainDateTreeLoggingConfigFromNVS, preloadTraceFileRingLoggingConfigFromNVS: allows the client to specify an NVS that is used to setup the underlying logging config object using a NamedValueSetAdapter before the Modular.Config based values are applied.</item>
        /// </list>
        /// </summary>
        public static void HandleOnStartup(ref string[] argsRef, ref Logging.Logger appLogger, INamedValueSet nvs = null, System.Reflection.Assembly hostingAssy = null)
        {
            hostingAssy = hostingAssy ?? CallerAssembly;

            nvs = nvs.MapNullToEmpty();

            string logBaseName = nvs["logBaseName"].VC.GetValueA(rethrow: false) ?? hostingAssy.GetAssemblyShortName();
            bool addWPFLMH = nvs["addWPFLMH"].VC.GetValue<bool?>(rethrow: false) ?? false;
            bool addSetLMH = nvs["addSetLMH"].VC.GetValue<bool?>(rethrow: false) ?? false;
            bool enableUEH = nvs["enableUEH"].VC.GetValue<bool?>(rethrow: false) ?? true;

            System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            System.Threading.Thread currentThread = System.Threading.Thread.CurrentThread;
            System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();
            System.Reflection.Assembly mainAssy = System.Reflection.Assembly.GetEntryAssembly();

            if (currentThread.Name.IsNullOrEmpty())
                currentThread.Name = "{0}.Main".CheckedFormat(logBaseName);

            IConfig config = Config.Instance;

            StandardProviderSelect providerSelect = nvs["providerSelect"].VC.GetValue<StandardProviderSelect>(rethrow: false, defaultValue: StandardProviderSelect.All);
            if (argsRef == null && providerSelect.IsSet(StandardProviderSelect.MainArgs))
                argsRef = System.Environment.GetCommandLineArgs();

            if (providerSelect != StandardProviderSelect.None && config.Providers.IsNullOrEmpty())
                config.AddStandardProviders(ref argsRef, providerSelect: providerSelect);

            if (enableUEH && UnhandledExceptionEventHandler != null)
            {
                uehFileWritePath = nvs["uehFileWritePath"].VC.GetValueA(rethrow: false)
                                    ?? config.GetConfigKeyAccessOnce("Config.UnhandledExceptionEventHandler.FilePath", silenceLogging: true).GetValue(uehFileWritePath);

                if (!uehFileWritePath.IsNullOrEmpty())
                {
                    AppDomain currentDomain = AppDomain.CurrentDomain;
                    currentDomain.UnhandledException += UnhandledExceptionEventHandler;
                }
            }

            int ringQueueSize = 500;
            int traceQueueSize = 1000;
            Logging.ListMesgEmitter issueListEmitter = new Logging.ListMesgEmitter() { MesgType = Logging.MesgType.Error };
            Logging.ListMesgEmitter valuesListEmitter = new Logging.ListMesgEmitter() { MesgType = Logging.MesgType.Debug };

            FileRingLogMessageHandlerType mainLoggerType = nvs["mainLoggerType"].VC.GetValue<FileRingLogMessageHandlerType?>(rethrow: false)
                ?? config.GetConfigKeyAccessOnce("Config.Logging.MainLogger.Type").GetValue<FileRingLogMessageHandlerType?>()
                ?? DefaultMainLoggerType;

            Logging.FileRotationLoggingConfig fileRotationRingConfig = new Logging.FileRotationLoggingConfig(logBaseName.MapNullToEmpty() + "LogFile")
            {
                mesgQueueSize = ringQueueSize,
                nameUsesDateAndTime = true,
                fileHeaderLines = Logging.GenerateDefaultHeaderLines(logBaseName, includeNullForDynamicLines: true, hostingAssembly: hostingAssy),
                fileHeaderLinesDelegate = Logging.GenerateDynamicHeaderLines,
                logGate = Logging.LogGate.Debug,
            };

            Logging.Handlers.TextFileDateTreeLogMessageHandler.Config dateTreeDirConfig = new Logging.Handlers.TextFileDateTreeLogMessageHandler.Config(logBaseName.MapNullToEmpty() + "Log", @".\Logs")
            {
                FileHeaderLines = Logging.GenerateDefaultHeaderLines(logBaseName, includeNullForDynamicLines: true, hostingAssembly: hostingAssy),
                FileHeaderLinesDelegate = Logging.GenerateDynamicHeaderLines,
                LogGate = Logging.LogGate.Debug,
            };

            switch (mainLoggerType)
            {
                case FileRingLogMessageHandlerType.TextFileRotationDirectoryLogMessageHandler:
                    fileRotationRingConfig.UpdateFromModularConfig("Config.Logging.FileRing.", issueListEmitter, valuesListEmitter, configInstance: config, preloadFromNVS: nvs["preloadMainFileRingLoggingConfigFromNVS"].VC.GetValueNVS(rethrow: false));
                    break;
                case  FileRingLogMessageHandlerType.TextFileDateTreeDirectoryLogMessageHandler:
                    dateTreeDirConfig.UpdateFromModularConfig("Config.Logging.DateTree.", issueListEmitter, valuesListEmitter, configInstance: config, preloadFromNVS: nvs["preloadMainDateTreeLoggingConfigFromNVS"].VC.GetValueNVS(rethrow: false));
                    break;

                default:
                case FileRingLogMessageHandlerType.None:
                    break;
            }

            Logging.ILogMessageHandler mainLMHnoQ = null;

            int maxQueueSize = 1000;

            switch (mainLoggerType)
            {
                case FileRingLogMessageHandlerType.TextFileRotationDirectoryLogMessageHandler:
                    mainLMHnoQ = new Logging.Handlers.TextFileRotationLogMessageHandler(fileRotationRingConfig);
                    maxQueueSize = fileRotationRingConfig.mesgQueueSize;
                    break;
                case FileRingLogMessageHandlerType.TextFileDateTreeDirectoryLogMessageHandler:
                    mainLMHnoQ = new Logging.Handlers.TextFileDateTreeLogMessageHandler(dateTreeDirConfig);
                    maxQueueSize = dateTreeDirConfig.MesgQueueSize;
                    break;
                default:
                case FileRingLogMessageHandlerType.None:
                    break;
            }

            LogMessageHandlerSettingFlags diagTraceLMHSettingFlags = nvs["diagTraceLMHSettingFlags"].VC.GetValue<LogMessageHandlerSettingFlags?>(rethrow: false) 
                                                                    ?? config.GetConfigKeyAccessOnce("Config.Logging.DiagnosticTrace").GetValue(LogMessageHandlerSettingFlags.IncludeWhenDebuggerAttached);
            LogMessageHandlerSettingFlags setLMHSettingFlags = nvs["setLMHSettingFlags"].VC.GetValue<LogMessageHandlerSettingFlags?>(rethrow: false) 
                                                                    ?? config.GetConfigKeyAccessOnce("Config.Logging.Set").GetValue(addSetLMH ? LogMessageHandlerSettingFlags.IncludeAlways : LogMessageHandlerSettingFlags.Disabled);

            bool addDiagTraceLMH = diagTraceLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeAlways) || diagTraceLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeWhenDebuggerAttached) && System.Diagnostics.Debugger.IsAttached;
            addSetLMH = setLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeAlways) || setLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeWhenDebuggerAttached) && System.Diagnostics.Debugger.IsAttached;

            Logging.ILogMessageHandler diagTraceLMH = addDiagTraceLMH ? Logging.CreateDiagnosticTraceLogMessageHandler(logGate: Logging.LogGate.Debug) : null;

            Logging.ILogMessageHandler setLMH = null;
            if (addSetLMH)
            {
                string setName = nvs["setName"].VC.GetValueA(rethrow: false) ?? config.GetConfigKeyAccessOnce("Config.Logging.Set.Name").GetValue("LogMessageHistory");
                int setCapacity = nvs["setCapacity"].VC.GetValue<int?>(rethrow: false) ?? config.GetConfigKeyAccessOnce("Config.Logging.Set.Capacity").GetValue(1000);
                Logging.LogGate setLogGate = nvs["setLogGate"].VC.GetValue<Logging.LogGate?>(rethrow: false) ?? config.GetConfigKeyAccessOnce("Config.Logging.Set.LogGate").GetValue(Logging.LogGate.Debug);

                if (!setName.IsNullOrEmpty())
                    setLMH = new MosaicLib.Logging.Handlers.SetLogMessageHandler(setName, capacity: setCapacity, logGate: setLogGate);
            }

            // Normally all of the standard lmh objects (main, diag, wpf) share the use of a single message queue.
            List<Logging.ILogMessageHandler> mainLMHList = new List<Logging.ILogMessageHandler>();

            if (mainLMHnoQ != null)
                mainLMHList.Add(mainLMHnoQ);

            if (diagTraceLMH != null)
            {
                if (diagTraceLMHSettingFlags.IsClear(LogMessageHandlerSettingFlags.NonQueued))
                    mainLMHList.Add(diagTraceLMH);
                else
                    Logging.AddLogMessageHandlerToDefaultDistributionGroup(diagTraceLMH);
            }

            if (setLMH != null)
            {
                if (setLMHSettingFlags.IsClear(LogMessageHandlerSettingFlags.NonQueued))
                    mainLMHList.Add(setLMH);
                else
                    Logging.AddLogMessageHandlerToDefaultDistributionGroup(setLMH);
            }

            Logging.ILogMessageHandler mainLMHQueueLMH = new Logging.Handlers.QueueLogMessageHandler("lmhMainSet.q", mainLMHList.ToArray(), maxQueueSize: maxQueueSize);
            Logging.AddLogMessageHandlerToDefaultDistributionGroup(mainLMHQueueLMH);

            string traceLoggingGroupName = nvs["traceLoggingGroupName"].VC.GetValueA(rethrow: false) ?? config.GetConfigKeyAccessOnce("Config.Logging.TraceRing.LGID", silenceLogging: true).GetValue("LGID.Trace");
            bool traceRingEnable = config.GetConfigKeyAccessOnce("Config.Logging.TraceRing.Enable").GetValue(!traceLoggingGroupName.IsNullOrEmpty());

            if (traceRingEnable)
            {
                // Setup the trace logger.  This logger uses a seperate message queue.
                Logging.FileRotationLoggingConfig traceRingConfig = new Logging.FileRotationLoggingConfig((logBaseName ?? String.Empty) + "TraceRing")
                {
                    mesgQueueSize = traceQueueSize,
                    nameUsesDateAndTime = false,     // will use 4 digit file names.  Limit of 100 files total
                    includeThreadInfo = true,
                    fileHeaderLines = Logging.GenerateDefaultHeaderLines("{0} Trace Output".CheckedFormat(logBaseName), includeNullForDynamicLines: true, hostingAssembly: hostingAssy),
                    fileHeaderLinesDelegate = Logging.GenerateDynamicHeaderLines,
                    logGate = Logging.LogGate.All,
                }.UpdateFromModularConfig("Config.Logging.TraceRing.", issueListEmitter, valuesListEmitter, configInstance: config, preloadFromNVS: nvs["preloadTraceFileRingLoggingConfigFromNVS"].VC.GetValueNVS(rethrow: false));

                Logging.ILogMessageHandler traceRingLMH = Logging.CreateQueuedTextFileRotationDirectoryLogMessageHandler(traceRingConfig);

                Logging.AddLogMessageHandlerToDistributionGroup(traceLoggingGroupName, traceRingLMH);
                Logging.SetDistributionGroupGate(traceLoggingGroupName, Logging.LogGate.All);

                Logging.MapLoggersToDistributionGroup(Logging.LoggerNameMatchType.Regex, @"(\.Data|\.Trace)", traceLoggingGroupName);

                bool traceRingLinkFromDefaultGroup = nvs["traceRingLinkFromDefaultGroup"].VC.GetValue<bool?>(rethrow: false) ?? config.GetConfigKeyAccessOnce("Config.Logging.TraceRing.LinkFromDefaultGroup").GetValue(true);
                bool traceRingLinkToDefaultGroup = nvs["traceRingLinkToDefaultGroup"].VC.GetValue<bool?>(rethrow: false) ?? config.GetConfigKeyAccessOnce("Config.Logging.TraceRing.LinkToDefaultGroup", silenceLogging: true).GetValue(false);

                if (traceRingLinkFromDefaultGroup)
                {
                    Logging.UpdateGroupSettings(Logging.DefaultDistributionGroupName, new Logging.GroupSettings() { GroupLinkageBehavior = Logging.GroupLinkageBehavior.IncludeLinkedLMHInstancesInGroupGateEvaluation });
                    Logging.LinkDistributionToGroup(Logging.DefaultDistributionGroupName, traceLoggingGroupName);
                }

                if (traceRingLinkToDefaultGroup)
                {
                    Logging.UpdateGroupSettings(traceLoggingGroupName, new Logging.GroupSettings() { GroupLinkageBehavior = Logging.GroupLinkageBehavior.IncludeLinkedLMHInstancesInGroupGateEvaluation });
                    Logging.LinkDistributionToGroup(traceLoggingGroupName, Logging.DefaultDistributionGroupName);
                }
            }

            if (nvs.Contains("appEventMesgType"))
                AppEventMesgType = nvs["appEventMesgType"].VC.GetValue<Logging.MesgType?>(rethrow: false) ?? Logging.MesgType.None;

            appLogger = new Logging.Logger("AppLogger");
            appLogger.Emitter(AppEventMesgType).EmitWith("App Starting", nvs: new NamedValueSet() { { "AppEvent", "OnStartup" } });

            // emit the config messages obtained above.
            Logging.Logger appLoggerCopy = appLogger;
            issueListEmitter.EmittedItemList.ForEach((item) => appLoggerCopy.Error.Emit(item.MesgStr));
            valuesListEmitter.EmittedItemList.ForEach((item) => appLoggerCopy.Debug.Emit(item.MesgStr));

            if (addWPFLMH)
                appLogger.Warning.Emit("Use of MosaicLib.WPF.Logging is no longer supported.  Please enable use of SetLogMessageHandler (addSetLMH/useSetLMH) and convert to use of set based log display controls");
        }

        /// <summary>
        /// Logs a message to indicate that the app was Deactivated using the AppEventMesgType message type (which defaults to MesgType.Signif)
        /// </summary>
        public static void HandleOnDeactivated(Logging.ILogger appLogger)
        {
            appLogger.Emitter(AppEventMesgType).EmitWith("App Deactivated", nvs: new NamedValueSet() { { "AppEvent", "OnDeactivated" } });
        }

        /// <summary>
        /// Logs a message to indicate that the app is Stopping using the AppEventMesgType message type (which defaults to MesgType.Signif)
        /// Then runs Logging.ShutdownLogging().
        /// </summary>
        public static void HandleOnExit(Logging.ILogger appLogger)
        {
            appLogger.Emitter(AppEventMesgType).EmitWith("App Stopping", nvs: new NamedValueSet() { { "AppEvent", "OnExit" } });

            Logging.ShutdownLogging();
        }
    }
}
