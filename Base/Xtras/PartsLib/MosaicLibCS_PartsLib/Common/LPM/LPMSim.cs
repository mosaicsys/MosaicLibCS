//-------------------------------------------------------------------
/*! @file LPMSim.cs
 *  @brief This file contains common interfaces, class and struct definitions that are used in implementing, using, and displaying LPMSimulator Parts and their state.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
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
using System.Linq;
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Helpers;
using MosaicLib.Semi.E084;
using MosaicLib.Semi.E087;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;

namespace MosaicLib.PartsLib.Common.LPM.Sim
{
    #region ILPMSimPart

    public interface ILPMSimPart : IActivePartBase
	{
        /// <summary>
        /// Creates a GUI Action that is used to handle screen button presses (et. al.) that are directly related to the LPM.
        /// The list of commands includes:
        /// <para/>
        /// "Reset_Port.Clicked"
        /// </summary>
        IStringParamAction CreateGUIAction(string guiRequest);

        /// <summary>
        /// Allows an E099TagRWSimEngine instance to inform this LPM that it can be used to simulate tag read/write operations for TagRW instance 1
        /// </summary>
        E099.Sim.ITagRWSimEngine TagRWSimEngine { get; set; }
    }

    #endregion

    #region LPMSimPartConfigBase

    public enum LPType : int
    {
        None = 0,
        Fixload6,
        VisionLPM,
    }

    public enum LPTagRWType : int
    {
        None = 0,
        Hermos,
        BrooksRFID,
        OmronV640,
    }


    public class LPMSimPartConfigBase
	{
        public LPMSimPartConfigBase(LPMSimPartConfigBase rhs)
		{
            LPMName = rhs.LPMName;
            Type = rhs.Type;
            PortSpecStr = rhs.PortSpecStr;
            PersistDirPathStr = rhs.PersistDirPathStr;

            Mapper_Installed = rhs.Mapper_Installed;
            E84_1_Installed = rhs.E84_1_Installed;
            E84_2_Installed = rhs.E84_2_Installed;
            IntegratedRFID_Type = rhs.IntegratedRFID_Type;
            SeperateRFID_Type = rhs.SeperateRFID_Type;

			ResetInputsOnStart = rhs.ResetInputsOnStart;
			ResetPositionOnStart = rhs.ResetPositionOnStart;
			ResetE84OnStart = rhs.ResetE84OnStart;

            useFastMotion = rhs.useFastMotion;
            VacuumOffMotionTime = rhs.VacuumOffMotionTime;
            VacuumOnMotionTime = rhs.VacuumOnMotionTime;
            ClampMotionTime = rhs.ClampMotionTime;
            TBarMotionTime = rhs.TBarMotionTime;
			MidMotionTime = rhs.MidMotionTime;
			LongMotionTime = rhs.LongMotionTime;

            MapResultPattern = new LPMSimConfigSetAndArrayItems(rhs.MapResultPattern);
            CarrierTypeSpec = new LPMSimConfigSetAndArrayItems(rhs.CarrierTypeSpec);
		}

        public LPMSimPartConfigBase(string lpmName, string persistDirBasePathStr) 
            : this ()
        {
            LPMName = lpmName;
            PersistDirPathStr = Path.Combine(persistDirBasePathStr, lpmName);
        }

        public LPMSimPartConfigBase()
        {
            ResetE84OnStart = true;

            SetMotionTimes(false);
        }

        public string LPMName { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true)]        // IsOptional so that search path with LPAll does not log errors when it is not found.
        public LPType Type { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true)]        // IsOptional so that search path with LPAll does not log errors when it is not found.
        public string PortSpecStr { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true, SilenceIssues=true)]        // this point is almost never used
        public string PersistDirPathStr { get; set; }

        [ConfigItem(Name = "Mapper.Installed", IsOptional = true, ReadOnlyOnce = true)]
        public bool Mapper_Installed { get; set; }

        [ConfigItem(Name = "E84.1.Installed", IsOptional = true, ReadOnlyOnce = true)]
        public bool E84_1_Installed { get; set; }

        [ConfigItem(Name = "E84.2.Installed", IsOptional = true, ReadOnlyOnce = true)]
        public bool E84_2_Installed { get; set; }

        [ConfigItem(Name = "IntegratedRFID.Type", IsOptional = true, ReadOnlyOnce = true)]
        public LPTagRWType IntegratedRFID_Type { get; set; }

        [ConfigItem(Name = "SeperateRFID.Type", IsOptional = true, ReadOnlyOnce = true)]
        public LPTagRWType SeperateRFID_Type { get; set; }

        public bool ResetInputsOnStart { get; set; }
        public bool ResetPositionOnStart { get; set; }
        public bool ResetE84OnStart { get; set; }

        [ConfigItem(ReadOnlyOnce = true, IsOptional = true, SilenceIssues=true)]        // this can be found in either LPAll or LP<n> sections - silence for simplicity.
        public bool UseFastMotion { get { return useFastMotion; } set { SetMotionTimes(value); } }

        private bool useFastMotion = false;

        public TimeSpan VacuumOnMotionTime { get; set; }
        public TimeSpan VacuumOffMotionTime { get; set; }
        public TimeSpan ClampMotionTime { get; set; }
        public TimeSpan TBarMotionTime { get; set; }
        public TimeSpan MidMotionTime { get; set; }         // pivot, shuttle
        public TimeSpan LongMotionTime { get; set; }        // door

        public LPMSimPartConfigBase SetMotionTimes(bool fastMotion)
        {
            this.useFastMotion = fastMotion;

            VacuumOffMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.333 : 0.75);
            VacuumOnMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.25 : 0.5);
            ClampMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.333 : 0.5);
            TBarMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 0.5 : 1.0);
            MidMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 1.0 : 2.0);      // pivot, shuttle
            LongMotionTime = TimeSpan.FromSeconds(UseFastMotion ? 1.0 : 3.0);      // door

            return this;
        }

        public LPMSimConfigSetAndArrayItems MapResultPattern = new LPMSimConfigSetAndArrayItems() { Item1 = "ooooooooooooooooooooooooo" };    // 25 correctly occupied slots
        public LPMSimConfigSetAndArrayItems CarrierTypeSpec = new LPMSimConfigSetAndArrayItems() { Item1 = "FOUP InfoPads='None'" };

        public LPMSimPartConfigBase Setup(Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<LPMSimPartConfigBase> adapter;

            LPMName = LPMName ?? String.Empty;

            // update values from any LPAll derived kesy.  We never log issues when trying to read from LPAll prefixed keys.
            adapter = new ConfigValueSetAdapter<LPMSimPartConfigBase>() { ValueSet = this, SetupIssueEmitter = Logging.NullEmitter, UpdateIssueEmitter = Logging.NullEmitter, ValueNoteEmitter = valueEmitter }.Setup("LPAll.");
            MapResultPattern.UpdateFromModularConfig("LPAll.MapResultPattern.", Logging.NullEmitter, valueEmitter);
            CarrierTypeSpec.UpdateFromModularConfig("LPAll.CarrierTypeSpec.", Logging.NullEmitter, valueEmitter);

            // update values from any lpmInstanceName derived keys.
            adapter = new ConfigValueSetAdapter<LPMSimPartConfigBase>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(LPMName + ".");
            MapResultPattern.UpdateFromModularConfig(LPMName + ".MapResultPattern.", issueEmitter, valueEmitter);
            CarrierTypeSpec.UpdateFromModularConfig(LPMName + ".CarrierTypeSpec.", issueEmitter, valueEmitter);

            return this;
        }

        public bool IsEqualTo(LPMSimPartConfigBase rhs)
        {
            return (LPMName == rhs.LPMName
                    && Type == rhs.Type
                    && PortSpecStr == rhs.PortSpecStr
                    && PersistDirPathStr == rhs.PersistDirPathStr
                    && Mapper_Installed == rhs.Mapper_Installed
                    && E84_1_Installed == rhs.E84_1_Installed
                    && E84_2_Installed == rhs.E84_2_Installed
                    && IntegratedRFID_Type == rhs.IntegratedRFID_Type
                    && SeperateRFID_Type == rhs.SeperateRFID_Type
                    && ResetInputsOnStart == rhs.ResetInputsOnStart
                    && ResetPositionOnStart == rhs.ResetPositionOnStart
                    && ResetE84OnStart == rhs.ResetE84OnStart
                    && UseFastMotion == rhs.UseFastMotion
                    && VacuumOnMotionTime == rhs.VacuumOnMotionTime
                    && VacuumOffMotionTime == rhs.VacuumOffMotionTime
                    && ClampMotionTime == rhs.ClampMotionTime
                    && TBarMotionTime == rhs.TBarMotionTime
                    && MidMotionTime == rhs.MidMotionTime
                    && LongMotionTime == rhs.LongMotionTime
                    && MapResultPattern.IsEqualTo(rhs.MapResultPattern)
                    && CarrierTypeSpec.IsEqualTo(rhs.CarrierTypeSpec)
                    );
        }
    }

    public class LPMSimConfigSetAndArrayItems
    {
        [ConfigItem(Name = "1", IsOptional=true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item1 { get; set; }
        [ConfigItem(Name = "2", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item2 { get; set; }
        [ConfigItem(Name = "3", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item3 { get; set; }
        [ConfigItem(Name = "4", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item4 { get; set; }
        [ConfigItem(Name = "5", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item5 { get; set; }
        [ConfigItem(Name = "6", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item6 { get; set; }
        [ConfigItem(Name = "7", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item7 { get; set; }
        [ConfigItem(Name = "8", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item8 { get; set; }
        [ConfigItem(Name = "9", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item9 { get; set; }
        [ConfigItem(Name = "10", IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
        public string Item10 { get; set; }

        public LPMSimConfigSetAndArrayItems()
        { }

        public LPMSimConfigSetAndArrayItems(LPMSimConfigSetAndArrayItems rhs)
        {
            Item1 = rhs.Item1;
            Item2 = rhs.Item2;
            Item3 = rhs.Item3;
            Item4 = rhs.Item4;
            Item5 = rhs.Item5;
            Item6 = rhs.Item6;
            Item7 = rhs.Item7;
            Item8 = rhs.Item8;
            Item9 = rhs.Item9;
            Item10 = rhs.Item10;
        }

        public string[] ItemsAsArray
        {
            get
            {
                String[] filteredArray = new[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9, Item10 }.TakeWhile((s) => !String.IsNullOrEmpty(s)).ToArray();
                return filteredArray;
            }
        }

        public LPMSimConfigSetAndArrayItems UpdateFromModularConfig(string prefixStr, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<LPMSimConfigSetAndArrayItems> adapter = new ConfigValueSetAdapter<LPMSimConfigSetAndArrayItems>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(prefixStr);

            return this;
        }

        internal bool IsEqualTo(LPMSimConfigSetAndArrayItems rhs)
        {
            return ItemsAsArray.IsEqualTo(rhs.ItemsAsArray);
        }
    }

    #endregion
}
