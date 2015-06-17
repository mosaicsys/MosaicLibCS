//-------------------------------------------------------------------
/*! @file BCRSim.cs
 *  @brief This file contains common interfaces, class and struct definitions that are used in implementing, using, and displaying BCRSimulator Parts and their state.
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

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Helpers;
using MosaicLib.Semi.E084;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;

namespace MosaicLib.PartsLib.Common.BCR.Sim
{
    #region IBCRSimPart

	/// <summary>Use Service commands "Connect", "Disconnect", "Send:{str}" to perform various actions.</summary>
    public interface IBCRSimPart : IActivePartBase
	{
        /// <summary>Property gives client access to the part's port base state</summary>
        IBaseState PortBaseState { get; }

        /// <summary>Property gives client access to the IBaseState notifier for the part's port</summary>
        INotificationObject<IBaseState> PortBaseStateNotifier { get; }
    }

    #endregion

    #region BCRSimPartConfigBase

    public class BCRSimPartConfigBase
	{
        public BCRSimPartConfigBase()
        { }

        public BCRSimPartConfigBase(BCRSimPartConfigBase rhs)
		{
            BCRName = rhs.BCRName;
            Installed = rhs.Installed;
            PortSpecStr = rhs.PortSpecStr;
		}

        public string BCRName { get; set; }

        [ConfigItem(Name = "Installed", IsOptional = true, ReadOnlyOnce = true)]
        public bool Installed { get; set; }

        [ConfigItem(Name = "PortSpecStr", IsOptional = true, ReadOnlyOnce = true)]
        public String PortSpecStr { get; set; }

        public BCRSimPartConfigBase Setup(Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<BCRSimPartConfigBase> adapter;

            BCRName = BCRName ?? String.Empty;

            // update values from any lpmInstanceName derived keys.
            adapter = new ConfigValueSetAdapter<BCRSimPartConfigBase>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = issueEmitter }.Setup(BCRName + ".");

            return this;
        }
    }

    #endregion
}
