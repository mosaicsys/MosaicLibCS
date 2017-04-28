//-------------------------------------------------------------------
/*! @file PerformanceMonitorHost.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2017 Mosaic Systems Inc.
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
using System.IO;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Tools;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.PartsLib.Tools.MDRF.Writer;
using MosaicLib.PartsLib.Tools.Performance;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.Tools.PerformanceMonitorHost
{
    public static class PerformanceMonitorHost
    {
        private static string appName = "AppNameNotFound";

        private static MMTimerPeriod mmTimerPeriod;

        [Flags]
        private enum LocalUserFileIndexRowFlagBits : ulong
        {
            /// <summary>0x0100</summary>
            NormalLMHMesgOccurrence = 0x0100,
            /// <summary>0x0200</summary>
            IssueLMHMesgOccurrence = 0x0200,
            /// <summary>0x1000</summary>
            CpuSampleGroups = 0x1000,
            /// <summary>0x2000</summary>
            CpuAggregateGroups = 0x2000,
            /// <summary>0x4000</summary>
            FileRWSampleGroups = 0x4000,
            /// <summary>0x8000</summary>
            FileRWAggregateGroups = 0x8000,
            /// <summary>0x10000</summary>
            ProcessDeltaOccurrence = 0x10000,
            /// <summary>0x20000</summary>
            PingAggregateGroups = 0x20000,
        }

        static void Main(string[] args)
        {
            try
            {
                mmTimerPeriod = new MMTimerPeriod();

                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                appName = System.IO.Path.GetFileNameWithoutExtension(currentProcess.MainModule.ModuleName);

                Logging.AddLogMessageHandlerToDefaultDistributionGroup(new Logging.Handlers.QueueLogMessageHandler(Logging.CreateConsoleLogMessageHandler(logGate: Logging.LogGate.Info, data: false, nvs: false)));

                IConfig config = Config.Instance;

                config.AddStandardProviders(ref args);

                List<Logging.ILogMessageHandler> lmhList = new List<Logging.ILogMessageHandler>();

                bool useOutputDebugStringLMH = config.GetConfigKeyAccessOnce("Logging.LMH.UseOutputDebugString").GetValue<bool>(false);
                bool useDiagnosticTraceLMH = config.GetConfigKeyAccessOnce("Logging.LMH.UseDiagnosticTrace").GetValue<bool>(false);

                if (useOutputDebugStringLMH)
                    lmhList.Add(Logging.CreateWin32DebugLogMessageHandler(appName, logGate: Logging.LogGate.Info));

                if (useDiagnosticTraceLMH)
                    lmhList.Add(Logging.CreateDiagnosticTraceLogMessageHandler(logGate: Logging.LogGate.Info));

                if (!lmhList.IsEmpty())
                    Logging.AddLogMessageHandlerToDefaultDistributionGroup(new Logging.Handlers.QueueLogMessageHandler("LMH.Queue", lmhList.ToArray()));

                Logging.StartLoggingIfNeeded();

                Logging.Logger appLogger = new Logging.Logger("AppMain");

                DictionaryConfigKeyProvider localProvider = new DictionaryConfigKeyProvider("localCKP")
                {
                    { "PerfSuite.MDRFFiles.DirPath", @".\Data" },
                };

                if (config.SearchForKeys(new Utils.StringMatching.MatchRuleSet(Utils.StringMatching.MatchType.Exact, "PerfSuite.Ping.PingTargetArray")).IsNullOrEmpty())
                    localProvider.Add("PerfSuite.Ping.PingTargetArray", "localhost"); // "localhost,8.8.8.8,8.8.4.4"

                config.AddProvider(localProvider);

                OutputDebugStringCapturePart odsCapturePart;
                PerformanceSuitePart perfSuite;

                List<IActivePartBase> partsList = new List<IActivePartBase>();

                using (Win32.Hooks.ConsoleCtrlHandlerHook consoleCtrlHandlerHook = new Win32.Hooks.ConsoleCtrlHandlerHook("cch", (sender, ctrlType) => CtrlHandlerDelegate(sender, ctrlType, partsList)))
                {
                    bool isDebuggerAttached = System.Diagnostics.Debugger.IsAttached;
                    bool enableOutputDebugStringCapturePart = config.GetConfigKeyAccessOnce("Logging.EnableOutputDebugStringCapturePart").GetValue<bool>(!isDebuggerAttached);
                    if (enableOutputDebugStringCapturePart && !useOutputDebugStringLMH && !useDiagnosticTraceLMH)
                        partsList.Add(odsCapturePart = new OutputDebugStringCapturePart("ODS", generateMesgType: Logging.MesgType.Info));

                    var perfSuiteConfig = new PerformanceSuitePartConfig().Setup();
                    partsList.Add(perfSuite = new PerformanceSuitePart(perfSuiteConfig));

                    MDRFLogMessageHandlerAdapterConfig mdrfLMHConfig = new MDRFLogMessageHandlerAdapterConfig() { OnlyRecordMessagesIfFileIsAlreadyActive = true }.Setup();
                    MDRFLogMessageHandlerAdapter mdrfLMH = new MDRFLogMessageHandlerAdapter("LMH.MDRF", Logging.LogGate.All, perfSuite.MDRFWriter, mdrfLMHConfig);
                    Logging.AddLogMessageHandlerToDefaultDistributionGroup(new Logging.Handlers.QueueLogMessageHandler(mdrfLMH));

                    IClientFacet[] goOnlineActionArray = partsList.Select(part => part.CreateGoOnlineAction(true).StartInline()).ToArray();
                    goOnlineActionArray.DoForEach((a) => a.WaitUntilComplete());

                    for (; ; )
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logging.ShutdownLogging();

                Console.WriteLine("{0}: failed with exception: {1}".CheckedFormat(appName, ex.ToString(ExceptionFormat.Full)));

                Environment.Exit(4);
            }
        }

        private static Win32.Hooks.ConsoleCtrlHandlerHook.ClientProvidedDelegateResult CtrlHandlerDelegate(Win32.Hooks.ConsoleCtrlHandlerHook sender, Win32.Hooks.CtrlType ctrlType, List<IActivePartBase> partsList)
        {
            IClientFacet [] goOfflineActionArray = partsList.Select(part => part.CreateGoOfflineAction().StartInline()).ToArray();

            QpcTimer waitTimeLimitTimer = new QpcTimer() { TriggerIntervalInSec = 0.5 }.Start();

            goOfflineActionArray.WaitUntilSetComplete(() => waitTimeLimitTimer.IsTriggered);

            sender.ExitCode = (goOfflineActionArray.Any(a => a.ActionState.Failed) ? 1 : 0);

            partsList.TakeAndDisposeOfGivenObjects();

            return Win32.Hooks.ConsoleCtrlHandlerHook.ClientProvidedDelegateResult.Exit;
        }
    }
}
