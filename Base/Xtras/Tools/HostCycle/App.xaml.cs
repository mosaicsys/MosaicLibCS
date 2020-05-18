//-------------------------------------------------------------------
/*! @file App.xaml.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2019 Mosaic Systems Inc., All rights reserved
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
using System.Windows;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.WPF.Common;

namespace HostCycle
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string[] args = (e != null) ? e.Args : null;
            AppSetup.HandleOnStartup(ref args, ref appLogger, new NamedValueSet()
            {
                { "addSetLMH", true },
                { "enableUEH", true },
                { "traceRingLinkToDefaultGroup", true },
            });
        }

        Logging.Logger appLogger;

        protected override void OnDeactivated(EventArgs e)
        {
            AppSetup.HandleOnDeactivated(appLogger);

            base.OnDeactivated(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppSetup.HandleOnExit(appLogger);

            base.OnExit(e);
        }
    }
}
