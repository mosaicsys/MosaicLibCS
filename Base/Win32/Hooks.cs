//-------------------------------------------------------------------
/*! @file Hooks.cs
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
using System.Security.AccessControl;
using System.Runtime.InteropServices;

using MosaicLib;
using MosaicLib.Utils;

namespace MosaicLib.Win32.Hooks
{
    using Microsoft.Win32;      // this using is located here to address namespace and symbol definition overlap issues in this source.

    #region Fcns static class

    /// <summary>
    /// This static partial class is effectively a namespace for the Win32 related classes, definitions, and static methods.
    /// </summary>
    public static partial class Fcns
    {
        #region extern kernal functions and related definitions

        [DllImport("kernel32")]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerDelegate handlerDelegate, bool add);

        public delegate bool ConsoleCtrlHandlerDelegate(CtrlType ctrlType);

        #endregion
    }

    public enum CtrlType
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6
    }

    #endregion

    #region Hook helper classes

    public class ConsoleCtrlHandlerHook : Utils.DisposableBase
    {
        /// <summary>This enumeration defines what a client provided delegate can trigger the ConsoleCtrlHandlerHook to do after the client delegate returns</summary>
        public enum ClientProvidedDelegateResult
        {
            /// <summary>The client provided delegate did not handle the control request.  hook returns Cancel=false.  The entitiy that called the hook dispatches to the next available ConsoleCtrlHandler.</summary>
            NotHandled,

            /// <summary>The client provided delegate would like the hook to attempt to ignore the control request.  hook returns Cancel=true</summary>
            Ignore,

            /// <summary>The client provided delegate would like the hook to call Environment.Exit.  The hook will obtain the exit code from its ExitCode property, which the client provided delegate should update with the desired value before returning.</summary>
            Exit,
        }

        public delegate ClientProvidedDelegateResult ClientProvidedDelegate(ConsoleCtrlHandlerHook sender, CtrlType ctrlType);

        public ConsoleCtrlHandlerHook(string name, ClientProvidedDelegate clientProvidedDelegate = null, int defaultExitCode = 0)
        {
            Logger = new Logging.Logger(name);

            this.clientProvidedDelegate = clientProvidedDelegate;
            ExitCode = defaultExitCode;

            consoleCtrlHandlerDelegate = ConsoleCtrlHandler;

            Hooks.Fcns.SetConsoleCtrlHandler(consoleCtrlHandlerDelegate, true);

            AddExplicitDisposeAction(() => Release());
        }

        public Logging.Logger Logger { get; private set; }

        private readonly ClientProvidedDelegate clientProvidedDelegate;

        public int ExitCode { get { return _exitCode; } set { _exitCode = value; } }
        private volatile int _exitCode = 0;

        private Fcns.ConsoleCtrlHandlerDelegate consoleCtrlHandlerDelegate;

        private bool ConsoleCtrlHandler(CtrlType ctrlType)
        {            
            string methodName = "{0}({1})".CheckedFormat(Utils.Fcns.CurrentMethodName, ctrlType);

            Logger.Debug.Emit("{0} called", methodName);

            bool returnValue = false;
            string reason = null;

            if (clientProvidedDelegate != null)
            {
                ClientProvidedDelegateResult cpdResult = ClientProvidedDelegateResult.NotHandled;

                try
                {
                    cpdResult = clientProvidedDelegate(this, ctrlType);
                    reason = "client delegate gave result: {0}".CheckedFormat(cpdResult);
                }
                catch (System.Exception ex)
                {
                    reason = "client provided delegate generated unexpected exception:".CheckedFormat(ex.ToString(ExceptionFormat.Full));
                    cpdResult = ClientProvidedDelegateResult.Ignore;
                }

                switch (cpdResult)
                {
                    default:
                    case ClientProvidedDelegateResult.NotHandled: returnValue = false; break;
                    case ClientProvidedDelegateResult.Ignore: returnValue = true; break;
                    case ClientProvidedDelegateResult.Exit:
                        {
                            int exitCode = ExitCode;

                            Logger.Emitter((exitCode == 0) ? Logging.MesgType.Signif : Logging.MesgType.Fatal).Emit("{0}: triggering System.Environment.Exit({1}) by client request", methodName, exitCode);

                            Logger.WaitForDistributionComplete((0.5).FromSeconds());

                            Logging.ShutdownLogging();

                            Environment.Exit(exitCode);
                        }
                        break;
                }
            }

            Logger.Debug.Emit("{0} returning handled:{1} reason: '{2}'", methodName, returnValue, reason);

            Logger.WaitForDistributionComplete((0.5).FromSeconds());

            return returnValue;
        }

        public void Release()
        {
            Hooks.Fcns.SetConsoleCtrlHandler(consoleCtrlHandlerDelegate, false);

            consoleCtrlHandlerDelegate = null;
        }
    }

    #endregion
}
