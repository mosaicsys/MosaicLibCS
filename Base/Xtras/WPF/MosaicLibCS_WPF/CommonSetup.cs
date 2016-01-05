//-------------------------------------------------------------------
/*! @file CommonSetup.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved.
 * Copyright (c) 2015 Mosaic Systems Inc.  All rights reserved.
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

namespace MosaicLib.WPF.Common
{
    using MosaicLib;        // apparently this makes MosaicLib get searched before MosaicLib.WPF.Logging for resolving symbols here.

    public static partial class AppSetup
    {
        public static void HandleOnStartup(StartupEventArgs e, ref Logging.Logger appLogger)
        {
            string logBaseName = System.Reflection.Assembly.GetCallingAssembly().FullName.Split(',').SafeAccess(0);        // split off the "name" of the assembly from the other parts that make up its "full" name.

            HandleOnStartup(e, ref appLogger, logBaseName);
        }

        public static void HandleOnStartup(StartupEventArgs e, ref Logging.Logger appLogger, string logBaseName)
        {
            MosaicLib.Modular.Config.Config.AddStandardProviders(e.Args);

            int ringQueueSize = 500;
            int traceQueueSize = 1000;
            Logging.ListMesgEmitter issueListEmitter = new Logging.ListMesgEmitter() { MesgType = Logging.MesgType.Error };
            Logging.ListMesgEmitter valuesListEmitter = new Logging.ListMesgEmitter() { MesgType = Logging.MesgType.Debug };

            Logging.FileRotationLoggingConfig ringConfig = new Logging.FileRotationLoggingConfig((logBaseName ?? String.Empty) + "LogFile")
            {
                mesgQueueSize = ringQueueSize,
                nameUsesDateAndTime = true,
            }.UpdateFromModularConfig("Config.Logging.FileRing.", issueListEmitter, valuesListEmitter);

            Logging.FileRotationLoggingConfig traceRingConfig = new Logging.FileRotationLoggingConfig((logBaseName ?? String.Empty) + "TraceRing")
            {
                mesgQueueSize = traceQueueSize,
                nameUsesDateAndTime = false,     // will use 4 digit file names.  Limit of 100 files total
                includeThreadInfo = true,
            }.UpdateFromModularConfig("Config.Logging.TraceRing.", issueListEmitter, valuesListEmitter);

            Logging.ILogMessageHandler dirRingLMH = Logging.CreateQueuedTextFileRotationDirectoryLogMessageHandler(ringConfig);
            Logging.ILogMessageHandler lmh = null; //  Logging.CreateDiagnosticTraceLogMessageHandler(Logging.LogGate.Debug);
            Logging.ILogMessageHandler wpfLMH = MosaicLib.WPF.Logging.WpfLogMessageHandlerToolBase.Instance;

            if (dirRingLMH != null)
                Logging.AddLogMessageHandlerToDefaultDistributionGroup(dirRingLMH);
            if (lmh != null)
                Logging.AddLogMessageHandlerToDefaultDistributionGroup(lmh);

            // how to change wpfLMH logging level after construction
            if (wpfLMH != null)
                Logging.AddLogMessageHandlerToDefaultDistributionGroup(wpfLMH);

            appLogger = new Logging.Logger("AppLogger");
            Logging.LogMessage lm = appLogger.GetLogMessage(Logging.MesgType.Signif, "App Starting", appLogger.GetStackFrame(0));
            lm.NamedValueSet = new NamedValueSet() { { "AppEvent", "OnStartup" } };
            appLogger.EmitLogMessage(ref lm);

            // emit the config messages obtained above.
            Logging.Logger appLoggerCopy = appLogger;
            issueListEmitter.EmittedItemList.ForEach((item) => appLoggerCopy.Error.Emit(item.MesgStr));
            valuesListEmitter.EmittedItemList.ForEach((item) => appLoggerCopy.Debug.Emit(item.MesgStr));

            // setup the loadPortTraceGroup - to recieve trace data from 
            string traceLoggingGroupName = "LGID.Trace";

            Logging.ILogMessageHandler traceRingLMH = Logging.CreateQueuedTextFileRotationDirectoryLogMessageHandler(traceRingConfig);

            Logging.AddLogMessageHandlerToDistributionGroup(traceLoggingGroupName, traceRingLMH);
            Logging.SetDistributionGroupGate(traceLoggingGroupName, Logging.LogGate.All);

            Logging.LinkDistributionToGroup(Logging.DefaultDistributionGroupName, traceLoggingGroupName);
            Logging.MapLoggersToDistributionGroup(Logging.LoggerNameMatchType.Regex, @"(\.Data|\.Trace)", traceLoggingGroupName);
        }

        public static void HandleOnDeactivated(Logging.ILogger appLogger)
        {
            Logging.LogMessage lm = appLogger.GetLogMessage(Logging.MesgType.Signif, "App Deactiviated", appLogger.GetStackFrame(0));
            lm.NamedValueSet = new NamedValueSet() { { "AppEvent", "OnDeactivated" } };
            appLogger.EmitLogMessage(ref lm);
        }

        public static void HandleOnExit(Logging.ILogger appLogger)
        {
            Logging.LogMessage lm = appLogger.GetLogMessage(Logging.MesgType.Signif, "App Stopping", appLogger.GetStackFrame(0));
            lm.NamedValueSet = new NamedValueSet() { { "AppEvent", "OnExit" } };
            appLogger.EmitLogMessage(ref lm);

            Logging.ShutdownLogging();
        }

    }
}
