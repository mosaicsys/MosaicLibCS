//-------------------------------------------------------------------
/*! @file E040.cs
 *  @brief This file provides common definitions that relate to the use of the E087 interface.
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
        Created = 254,      // locally defined value
        [EnumMember]
        Invalid = 255,      // locally defined value
    }

    /// <summary>
    /// PRJob recipe method specification enumeration used with various forms of PRJob creation (S16F11, et. al.)
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
        PRJobWaitingForStart = 4,
    }

    /// <summary>
    /// PRJob event IDs for use with S16/F9
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
