//-------------------------------------------------------------------
/*! @file E040.cs
 *  @brief This file provides common definitions that relate to the use of the E040 interface.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2019 Mosaic Systems Inc.
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

using System.Runtime.Serialization;

namespace MosaicLib.Semi.E040
{
    //-------------------------------------------------------------------
    // E040-0705

    /// <summary>
    /// Enumeration of the PRJob state values.  Also called PRSTATE
    /// <para>
    /// <see cref="QueuedOrPooled"/> (0), <see cref="SettingUp"/> (1), <see cref="WaitingForStart"/> (2), 
    /// <see cref="Processing"/> (3), <see cref="ProcessComplete"/> (4), <see cref="Reserved_5"/> (5), 
    /// <see cref="Pausing"/> (6), <see cref="Paused"/> (7), 
    /// <see cref="Stopping"/> (8), <see cref="Aborting"/> (9), <see cref="Stopped"/> (10), <see cref="Aborted"/> (11),
    /// <see cref="Removed"/> (253), <see cref="Created"/> (254), <see cref="Invalid"/> (255)
    /// </para>
    /// </summary>
    [DataContract(Namespace = Constants.E040NameSpace)]
    public enum PRJobState : byte
    {
        [EnumMember]
        QueuedOrPooled = 0,
        [EnumMember]
        SettingUp = 1,
        [EnumMember]
        WaitingForStart = 2,
        [EnumMember]
        Processing = 3,
        [EnumMember]
        ProcessComplete = 4,
        [EnumMember]
        Reserved_5 = 5,
        [EnumMember]
        Pausing = 6,
        [EnumMember]
        Paused = 7,
        [EnumMember]
        Stopping = 8,
        [EnumMember]
        Aborting = 9,
        [EnumMember]
        Stopped = 10,
        [EnumMember]
        Aborted = 11,
        [EnumMember]
        Removed = 253,    // locally defined value
        [EnumMember]
        Created = 254,      // locally defined value
        [EnumMember]
        Invalid = 255,      // locally defined value
    }

    /// <summary>
    /// <see cref="PRJobState"/> Transitions.
    /// </summary>
    [DataContract(Namespace = Constants.E040NameSpace)]
    public enum PRJobState_Transition : int
    {
        /// <summary>noState -> <see cref="PRJobState.QueuedOrPooled"/>: Job created.</summary>
        [EnumMember]
        Transition1 = 1,

        /// <summary><see cref="PRJobState.QueuedOrPooled"/> -> <see cref="PRJobState.SettingUp"/>: Job ready for setup.</summary>
        [EnumMember]
        Transition2 = 2,

        /// <summary><see cref="PRJobState.SettingUp"/> -> <see cref="PRJobState.WaitingForStart"/>: Setup complete (Manual Start).</summary>
        [EnumMember]
        Transition3 = 3,

        /// <summary><see cref="PRJobState.SettingUp"/> -> <see cref="PRJobState.Processing"/>: Setup complete (Automatic Start).</summary>
        [EnumMember]
        Transition4 = 4,

        /// <summary><see cref="PRJobState.WaitingForStart"/> -> <see cref="PRJobState.Processing"/>: Job Start requested (Manual Start).</summary>
        [EnumMember]
        Transition5 = 5,

        /// <summary><see cref="PRJobState.Processing"/> -> <see cref="PRJobState.ProcessComplete"/>: Material processing, and post processing complete.  Job awaiting material departure.</summary>
        [EnumMember]
        Transition6 = 6,

        /// <summary>Post Active (<see cref="PRJobState.ProcessComplete"/>, <see cref="PRJobState.Stopped"/>, or <see cref="PRJobState.Aborted"/>) -> <see cref="PRJobState.Removed"/>: Job removed.  Material departed.</summary>
        [EnumMember]
        Transition7 = 7,

        /// <summary>Executing (<see cref="PRJobState.SettingUp"/>, <see cref="PRJobState.WaitingForStart"/>, or <see cref="PRJobState.Processing"/>) -> <see cref="PRJobState.Pausing"/>: Job Pause requested.</summary>
        [EnumMember]
        Transition8 = 8,

        /// <summary><see cref="PRJobState.Pausing"/> -> <see cref="PRJobState.Paused"/>: Job paused.  Material as been moved ot a safe location and condition to wait for Resume, Stop from.</summary>
        [EnumMember]
        Transition9 = 9,

        /// <summary>Pause (<see cref="PRJobState.Pausing"/> or <see cref="PRJobState.Paused"/>) -> selected Executing sub-state (<see cref="PRJobState.SettingUp"/>, <see cref="PRJobState.WaitingForStart"/>, or <see cref="PRJobState.Processing"/>): Job Resume requested.</summary>
        [EnumMember]
        Transition10 = 10,

        /// <summary>Executing (<see cref="PRJobState.SettingUp"/>, <see cref="PRJobState.WaitingForStart"/>, or <see cref="PRJobState.Processing"/>) -> <see cref="PRJobState.Stopping"/>: Job Stop requested.</summary>
        [EnumMember]
        Transition11 = 11,

        /// <summary>Pause (<see cref="PRJobState.Pausing"/> or <see cref="PRJobState.Paused"/>) -> <see cref="PRJobState.Stopping"/>: Job Stop requested.</summary>
        [EnumMember]
        Transition12 = 12,

        /// <summary>Executing (<see cref="PRJobState.SettingUp"/>, <see cref="PRJobState.WaitingForStart"/>, or <see cref="PRJobState.Processing"/>) -> <see cref="PRJobState.Aborting"/>: Job Abort requested.</summary>
        [EnumMember]
        Transition13 = 13,

        /// <summary><see cref="PRJobState.Stopping"/> -> <see cref="PRJobState.Aborting"/>: Job Abort requested.</summary>
        [EnumMember]
        Transition14 = 14,

        /// <summary>Pause (<see cref="PRJobState.Pausing"/> or <see cref="PRJobState.Paused"/>) -> <see cref="PRJobState.Aborting"/>: Job Abort requested.</summary>
        [EnumMember]
        Transition15 = 15,

        /// <summary><see cref="PRJobState.Aborting"/> -> <see cref="PRJobState.Aborted"/>: Job Abort completed.</summary>
        [EnumMember]
        Transition16 = 16,

        /// <summary><see cref="PRJobState.Stopping"/> -> <see cref="PRJobState.Stopped"/>: Job Stop completed.  Material was either processed or skipped.</summary>
        [EnumMember]
        Transition17 = 17,

        /// <summary><see cref="PRJobState.QueuedOrPooled"/> -> <see cref="PRJobState.Removed"/>: Job was Cancelled.</summary>
        [EnumMember]
        Transition18 = 18,
    }

    /// <summary>
    /// PRJob recipe method specification enumeration used with various forms of PRJob creation (S16F11, et. al.)
    /// <para>
    /// <see cref="None"/> (0), <see cref="RecipeOnly"/> (1), <see cref="RecipeWithVariableTuning"/> (2) 
    /// </para>
    /// </summary>
    [DataContract(Namespace = Constants.E040NameSpace)]
    public enum PRRECIPEMETHOD : byte
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        RecipeOnly = 1,
        [EnumMember]
        RecipeWithVariableTuning = 2,
    }

    /// <summary>
    /// The set of PRJob commands that are supported using S16/F5[W].  These are used in string (20) representation.
    /// <para>
    /// <see cref="None"/> (0), <see cref="START"/>, <see cref="STOP"/>, <see cref="PAUSE"/>, <see cref="RESUME"/>, <see cref="ABORT"/>, <see cref="CANCEL"/> 
    /// </para>
    /// </summary>
    [DataContract(Namespace = Constants.E040NameSpace)]
    public enum PRCMDNAME : int
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        START,
        [EnumMember]
        STOP,
        [EnumMember]
        PAUSE,
        [EnumMember]
        RESUME,
        [EnumMember]
        ABORT,
        [EnumMember]
        CANCEL,
    }

    /// <summary>
    /// The set of PRJob milestone values as used with S16/F7
    /// <para>
    /// <see cref="None"/> (0), <see cref="PRJobSetup"/> (1), <see cref="PRJobProcessing"/> (2), <see cref="PRJobProcessingComplete"/> (3), <see cref="PRJobComplete"/> (4), <see cref="PRJobWaitingForStart"/> (5), 
    /// </para>
    /// </summary>
    public enum PRJOBMILESTONE : byte
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        PRJobSetup = 1,
        [EnumMember]
        PRJobProcessing = 2,
        [EnumMember]
        PRJobProcessingComplete = 3,
        [EnumMember]
        PRJobComplete = 4,
        [EnumMember]
        PRJobWaitingForStart = 5,
    }

    /// <summary>
    /// PRJob event IDs for use with S16/F9
    /// <para>
    /// <see cref="None"/> (0), <see cref="WaitingForMaterial"/> (1), <see cref="JobStateChange"/> (2), 
    /// </para>
    /// </summary>
    public enum PREVENTID : byte
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        WaitingForMaterial = 1,
        [EnumMember]
        JobStateChange = 2,
    }

    //-------------------------------------------------------------------
}
