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
        public static string DefaultLogBaseName { get { return CallerAssembly.GetAssemblyNameFromFullName(); } }

        private static string GetAssemblyNameFromFullName(this System.Reflection.Assembly assembly)
        {
            if (assembly != null)
                return assembly.FullName.Split(new[] { ',' }, 2).SafeAccess(0); // split off the "name" of the assembly from the other parts that make up its "full" name.
            else
                return "$$GivenAssemblyIsNull$$";
        }

        /// <summary>
        /// Returns the first assembly that has a different FullName than this methods DeclaringType.Assembly, or if there are no other such assemblies within 10 stack frames up from this method
        /// then this property returns the this method's DeclaringType.Assembly itself.
        /// </summary>
        public static System.Reflection.Assembly CallerAssembly
        {
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
            TextFileRotationDirectoryLogMessageHandler,
            TextFileDateTreeDirectoryLogMessageHandler,
            None,
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
        public static void HandleOnStartup(StartupEventArgs e, ref Logging.Logger appLogger, string logBaseName = null, bool useSetLMH = false, bool enableUEH = true)
        {
            string[] args = (e != null) ? e.Args : null;
            HandleOnStartup(ref args, ref appLogger, logBaseName: logBaseName, addWPFLMH: (e != null) && !useSetLMH, addSetLMH: (e != null) && useSetLMH, enableUEH: enableUEH);
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
            System.Reflection.Assembly callerAssy = CallerAssembly;
            logBaseName = logBaseName ?? callerAssy.GetAssemblyNameFromFullName();

            System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            System.Threading.Thread currentThread = System.Threading.Thread.CurrentThread;
            System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();
            System.Reflection.Assembly mainAssy = System.Reflection.Assembly.GetEntryAssembly();

            if (currentThread.Name.IsNullOrEmpty())
                currentThread.Name = "{0}.Main".CheckedFormat(logBaseName);

            IConfig config = Config.Instance;

            if (argsRef != null && config.Providers.IsNullOrEmpty())
                config.AddStandardProviders(ref argsRef);

            if (enableUEH && UnhandledExceptionEventHandler != null)
            {
                uehFileWritePath = config.GetConfigKeyAccessOnce("Config.UnhandledExceptionEventHandler.FilePath", silenceLogging: true).GetValue(uehFileWritePath);

                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += UnhandledExceptionEventHandler;
            }

            int ringQueueSize = 500;
            int traceQueueSize = 1000;
            Logging.ListMesgEmitter issueListEmitter = new Logging.ListMesgEmitter() { MesgType = Logging.MesgType.Error };
            Logging.ListMesgEmitter valuesListEmitter = new Logging.ListMesgEmitter() { MesgType = Logging.MesgType.Debug };

            FileRingLogMessageHandlerType mainLoggerType = config.GetConfigKeyAccessOnce("Config.Logging.MainLogger.Type").GetValue<FileRingLogMessageHandlerType>(DefaultMainLoggerType);

            Logging.FileRotationLoggingConfig fileRotationRingConfig = new Logging.FileRotationLoggingConfig(logBaseName.MapNullToEmpty() + "LogFile")
            {
                mesgQueueSize = ringQueueSize,
                nameUsesDateAndTime = true,
                fileHeaderLines = Logging.GenerateDefaultHeaderLines(logBaseName, includeNullForDynamicLines: true, hostingAssembly: callerAssy),
                fileHeaderLinesDelegate = Logging.GenerateDynamicHeaderLines,
                logGate = Logging.LogGate.Debug,
            };

            Logging.Handlers.TextFileDateTreeLogMessageHandler.Config dateTreeDirConfig = new Logging.Handlers.TextFileDateTreeLogMessageHandler.Config(logBaseName.MapNullToEmpty() + "Log", @".\Logs")
            {
                FileHeaderLines = Logging.GenerateDefaultHeaderLines(logBaseName, includeNullForDynamicLines: true, hostingAssembly: callerAssy),
                FileHeaderLinesDelegate = Logging.GenerateDynamicHeaderLines,
                LogGate = Logging.LogGate.Debug,
            };

            switch (mainLoggerType)
            {
                case FileRingLogMessageHandlerType.TextFileRotationDirectoryLogMessageHandler:
                    fileRotationRingConfig.UpdateFromModularConfig("Config.Logging.FileRing.", issueListEmitter, valuesListEmitter, configInstance: config);
                    break;
                case  FileRingLogMessageHandlerType.TextFileDateTreeDirectoryLogMessageHandler:
                    dateTreeDirConfig.UpdateFromModularConfig("Config.Logging.DateTree.", issueListEmitter, valuesListEmitter, configInstance: config);
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

            LogMessageHandlerSettingFlags diagTraceLMHSettingFlags = config.GetConfigKeyAccessOnce("Config.Logging.DiagnosticTrace").GetValue(LogMessageHandlerSettingFlags.IncludeWhenDebuggerAttached);
            LogMessageHandlerSettingFlags wpfLMHSettingFlags = config.GetConfigKeyAccessOnce("Config.Logging.WPF").GetValue(addWPFLMH ? LogMessageHandlerSettingFlags.IncludeAlways : LogMessageHandlerSettingFlags.Disabled);
            LogMessageHandlerSettingFlags setLMHSettingFlags = config.GetConfigKeyAccessOnce("Config.Logging.Set").GetValue(addSetLMH ? LogMessageHandlerSettingFlags.IncludeAlways : LogMessageHandlerSettingFlags.Disabled);

            bool addDiagTraceLMH = diagTraceLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeAlways) || diagTraceLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeWhenDebuggerAttached) && System.Diagnostics.Debugger.IsAttached;
            addWPFLMH = wpfLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeAlways) || wpfLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeWhenDebuggerAttached) && System.Diagnostics.Debugger.IsAttached;
            addSetLMH = setLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeAlways) || wpfLMHSettingFlags.IsSet(LogMessageHandlerSettingFlags.IncludeWhenDebuggerAttached) && System.Diagnostics.Debugger.IsAttached;

            Logging.ILogMessageHandler diagTraceLMH = addDiagTraceLMH ? Logging.CreateDiagnosticTraceLogMessageHandler(logGate: Logging.LogGate.Debug) : null;
            Logging.ILogMessageHandler wpfLMH = addWPFLMH ? MosaicLib.WPF.Logging.WpfLogMessageHandlerToolBase.Instance : null;

            Logging.ILogMessageHandler setLMH = null;
            if (addSetLMH)
            {
                string setName = config.GetConfigKeyAccessOnce("Config.Logging.Set.Name").GetValue("LogMessageHistory");
                int setCapacity = config.GetConfigKeyAccessOnce("Config.Logging.Set.Capacity").GetValue(1000);
                Logging.LogGate setLogGate = config.GetConfigKeyAccessOnce("Config.Logging.Set.LogGate").GetValue(Logging.LogGate.Debug);

                if (!setName.IsNullOrEmpty())
                    setLMH = new MosaicLib.Logging.Handlers.SetLogMessageHandler(setName, capacity: setCapacity, logGate: setLogGate);
            }

            // Normally all of the standard lmh objects (main, diag, wpf) share the use of a single message queue.
            List<Logging.ILogMessageHandler> mainLMHList = new List<Logging.ILogMessageHandler>();

            if (mainLMHnoQ != null)
                mainLMHList.Add(mainLMHnoQ);

            if (diagTraceLMH != null && diagTraceLMHSettingFlags.IsClear(LogMessageHandlerSettingFlags.NonQueued))
                mainLMHList.Add(diagTraceLMH);
            else if (diagTraceLMH != null)
                Logging.AddLogMessageHandlerToDefaultDistributionGroup(diagTraceLMH);

            if (wpfLMH != null && wpfLMHSettingFlags.IsClear(LogMessageHandlerSettingFlags.NonQueued))
                mainLMHList.Add(wpfLMH);
            else if (wpfLMH != null)
                Logging.AddLogMessageHandlerToDefaultDistributionGroup(wpfLMH);

            if (setLMH != null && setLMHSettingFlags.IsClear(LogMessageHandlerSettingFlags.NonQueued))
                mainLMHList.Add(setLMH);
            else if (setLMH != null)
                Logging.AddLogMessageHandlerToDefaultDistributionGroup(setLMH);

            Logging.ILogMessageHandler mainLMHQueueLMH = new Logging.Handlers.QueueLogMessageHandler("lmhMainSet.q", mainLMHList.ToArray(), maxQueueSize: maxQueueSize);
            Logging.AddLogMessageHandlerToDefaultDistributionGroup(mainLMHQueueLMH);

            string traceLoggingGroupName = config.GetConfigKeyAccessOnce("Config.Logging.TraceRing.LGID", silenceLogging: true).GetValue("LGID.Trace");
            bool traceRingEnable = config.GetConfigKeyAccessOnce("Config.Logging.TraceRing.Enable").GetValue(!traceLoggingGroupName.IsNullOrEmpty());

            if (traceRingEnable)
            {
                // Setup the trace logger.  This logger uses a seperate message queue.
                Logging.FileRotationLoggingConfig traceRingConfig = new Logging.FileRotationLoggingConfig((logBaseName ?? String.Empty) + "TraceRing")
                {
                    mesgQueueSize = traceQueueSize,
                    nameUsesDateAndTime = false,     // will use 4 digit file names.  Limit of 100 files total
                    includeThreadInfo = true,
                    fileHeaderLines = Logging.GenerateDefaultHeaderLines("{0} Trace Output".CheckedFormat(logBaseName), includeNullForDynamicLines: true, hostingAssembly: callerAssy),
                    fileHeaderLinesDelegate = Logging.GenerateDynamicHeaderLines,
                    logGate = Logging.LogGate.All,
                }.UpdateFromModularConfig("Config.Logging.TraceRing.", issueListEmitter, valuesListEmitter, configInstance: config);

                Logging.ILogMessageHandler traceRingLMH = Logging.CreateQueuedTextFileRotationDirectoryLogMessageHandler(traceRingConfig);

                Logging.AddLogMessageHandlerToDistributionGroup(traceLoggingGroupName, traceRingLMH);
                Logging.SetDistributionGroupGate(traceLoggingGroupName, Logging.LogGate.All);

                Logging.MapLoggersToDistributionGroup(Logging.LoggerNameMatchType.Regex, @"(\.Data|\.Trace)", traceLoggingGroupName);

                bool traceRingLinkFromDefaultGroup = config.GetConfigKeyAccessOnce("Config.Logging.TraceRing.LinkFromDefaultGroup").GetValue(true);
                bool traceRingLinkToDefaultGroup = config.GetConfigKeyAccessOnce("Config.Logging.TraceRing.LinkToDefaultGroup", silenceLogging: true).GetValue(false);

                if (traceRingLinkFromDefaultGroup)
                    Logging.LinkDistributionToGroup(Logging.DefaultDistributionGroupName, traceLoggingGroupName);

                if (traceRingLinkToDefaultGroup)
                    Logging.LinkDistributionToGroup(traceLoggingGroupName, Logging.DefaultDistributionGroupName);
            }

            appLogger = new Logging.Logger("AppLogger");
            Logging.LogMessage lm = appLogger.GetLogMessage(AppEventMesgType, "App Starting");
            lm.NamedValueSet = new NamedValueSet() { { "AppEvent", "OnStartup" } };
            appLogger.EmitLogMessage(ref lm);

            // emit the config messages obtained above.
            Logging.Logger appLoggerCopy = appLogger;
            issueListEmitter.EmittedItemList.ForEach((item) => appLoggerCopy.Error.Emit(item.MesgStr));
            valuesListEmitter.EmittedItemList.ForEach((item) => appLoggerCopy.Debug.Emit(item.MesgStr));
        }

        /// <summary>
        /// Logs a message to indicate that the app was Deactivated using the AppEventMesgType message type (which defaults to MesgType.Signif)
        /// </summary>
        public static void HandleOnDeactivated(Logging.ILogger appLogger)
        {
            Logging.LogMessage lm = appLogger.GetLogMessage(AppEventMesgType, "App Deactivated");
            lm.NamedValueSet = new NamedValueSet() { { "AppEvent", "OnDeactivated" } };

            appLogger.EmitLogMessage(ref lm);
        }

        /// <summary>
        /// Logs a message to indicate that the app is Stopping using the AppEventMesgType message type (which defaults to MesgType.Signif)
        /// Then runs Logging.ShutdownLogging().
        /// </summary>
        public static void HandleOnExit(Logging.ILogger appLogger)
        {
            Logging.LogMessage lm = appLogger.GetLogMessage(AppEventMesgType, "App Stopping");
            lm.NamedValueSet = new NamedValueSet() { { "AppEvent", "OnExit" } };
            appLogger.EmitLogMessage(ref lm);

            Logging.ShutdownLogging();
        }
    }
}
