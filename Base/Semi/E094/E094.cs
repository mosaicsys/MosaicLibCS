//-------------------------------------------------------------------
/*! @file E094.cs
 *  @brief This file provides common definitions that relate to the use of the E094 interface.
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

namespace MosaicLib.Semi.E094
{
    //-------------------------------------------------------------------
    // E094-0306

    /// <summary>
    /// Enumeration to go with CJ "State" property.  Used in U1 format
    /// <para>
    /// <see cref="Queued"/> (0), <see cref="Selected"/> (1), <see cref="WaitingForStart"/> (2), <see cref="Executing"/> (3), <see cref="Paused"/> (4), <see cref="Completed"/> (5), 
    /// <see cref="Removed"/> (253), <see cref="Created"/> (254), <see cref="Invalid"/> (255)
    /// </para>
    /// </summary>
    [DataContract(Namespace = Constants.E094NameSpace)]
    public enum CJState : byte
    {
        [EnumMember]
        Queued = 0,
        [EnumMember]
        Selected = 1,
        [EnumMember]
        WaitingForStart = 2,
        [EnumMember]
        Executing = 3,
        [EnumMember]
        Paused = 4,
        [EnumMember]
        Completed = 5,
        [EnumMember]
        Removed = 253,    // locally defined value
        [EnumMember]
        Created = 254,      // locally defined value
        [EnumMember]
        Invalid = 255,      // locally defined value
    }

    /// <summary>
    /// <see cref="CJState"/> Transitions.
    /// </summary>
    [DataContract(Namespace = Constants.E040NameSpace)]
    public enum CJState_Transition : int
    {
        /// <summary>noState -> <see cref="CJState.Queued"/>: Job created.</summary>
        [EnumMember]
        Transition1 = 1,

        /// <summary><see cref="CJState.Queued"/> -> <see cref="CJState.Removed"/>: Job Stop, Abort, or Cancel was requested.</summary>
        [EnumMember]
        Transition2 = 2,

        /// <summary><see cref="CJState.Queued"/> -> <see cref="CJState.Selected"/>: Job is ready to be activated.  Material may have arrived, or is the next expected to arrive.</summary>
        [EnumMember]
        Transition3 = 3,

        /// <summary><see cref="CJState.Selected"/> -> <see cref="CJState.Queued"/>: Job was deselected by request.  (usually in order to be able to peform a CJHOQ for a hot lot).</summary>
        [EnumMember]
        Transition4 = 4,

        /// <summary><see cref="CJState.Selected"/> -> <see cref="CJState.Executing"/>: Job activated and was started (Automatic Start).  (some) Material is available.</summary>
        [EnumMember]
        Transition5 = 5,

        /// <summary><see cref="CJState.Selected"/> -> <see cref="CJState.WaitingForStart"/>: Job activated and is ready to start (Manual Start).  (some) Material is available.</summary>
        [EnumMember]
        Transition6 = 6,

        /// <summary><see cref="CJState.WaitingForStart"/> -> <see cref="CJState.Executing"/>: Job Start request received.</summary>
        [EnumMember]
        Transition7 = 7,

        /// <summary><see cref="CJState.Executing"/> -> <see cref="CJState.Paused"/>: Job Pause requested.  Remaining Queued/Pooled PJs will be held and will not be moved to SettingUp state.  Other PJs will not be affected.</summary>
        [EnumMember]
        Transition8 = 8,

        /// <summary><see cref="CJState.Paused"/> -> <see cref="CJState.Executing"/>: Job Resume requested.</summary>
        [EnumMember]
        Transition9 = 9,

        /// <summary><see cref="CJState.Executing"/> -> <see cref="CJState.Completed"/>: Job completed.  All attached PJs have been completed.</summary>
        [EnumMember]
        Transition10 = 10,

        /// <summary>Active (<see cref="CJState.Selected"/>, <see cref="CJState.WaitingForStart"/>, <see cref="CJState.Executing"/>, or <see cref="CJState.Paused"/>) -> <see cref="CJState.Completed"/>: Job CJStop requested.</summary>
        [EnumMember]
        Transition11 = 11,

        /// <summary>Active (<see cref="CJState.Selected"/>, <see cref="CJState.WaitingForStart"/>, <see cref="CJState.Executing"/>, or <see cref="CJState.Paused"/>) -> <see cref="CJState.Completed"/>: Job CJAbort requested.</summary>
        [EnumMember]
        Transition12 = 12,

        /// <summary><see cref="CJState.Completed"/> -> <see cref="CJState.Removed"/>: Job removed.  Normally this is triggered by removal of the material/carriers that are associated with this job, or its subordinate jobs.</summary>
        [EnumMember]
        Transition13 = 13,
    }

    /// <summary>
    /// Specifies the known values for Process order management (used during CJ creation).  Used in U1 format.
    /// <para>
    /// <see cref="None"/> (0), <see cref="Arrival"/> (1), <see cref="Optimize"/> (2), <see cref="List"/> (3) 
    /// </para>
    /// </summary>
    [DataContract(Namespace = Constants.E094NameSpace)]
    public enum ProcessOrderMgmt : byte
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        Arrival = 1,
        [EnumMember]
        Optimize = 2,
        [EnumMember]
        List = 3,
    }

    /// <summary>
    /// Supported Control Job commands as used with S16/F27[W].  Used in U1 format.
    /// <para>
    /// <see cref="None"/> (0), <see cref="CJStart"/> (1), <see cref="CJPause"/> (2), <see cref="CJResume"/> (3), 
    /// <see cref="CJCancel"/> (4), <see cref="CJDeselect"/> (5), <see cref="CJStop"/> (6), <see cref="CJAbort"/> (7), 
    /// <see cref="CJHOQ"/> (8)
    /// </para>
    /// </summary>
    [DataContract(Namespace = Constants.E094NameSpace)]
    public enum CTLJOBCMD : byte
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        CJStart = 1,
        [EnumMember]
        CJPause = 2,
        [EnumMember]
        CJResume = 3,
        [EnumMember]
        CJCancel = 4,
        [EnumMember]
        CJDeselect = 5,
        [EnumMember]
        CJStop = 6,
        [EnumMember]
        CJAbort = 7,
        [EnumMember]
        CJHOQ = 8,
    }

    /// <summary>
    /// Common success result code for many Stream 14 functions.  (used for ControlJobs)
    /// <para>
    /// <see cref="Success"/> (0), <see cref="Error"/> (1), <see cref="Denied_Internal"/> (255) 
    /// </para>
    /// </summary>
    [DataContract(Namespace = Constants.E094NameSpace)]
    public enum OBJACK : byte
    {
        [EnumMember]
        Success = 0,
        [EnumMember]
        Error = 1,
        [EnumMember]
        Denied_Internal = 255,
    }

    /// <summary>
    /// Action value for use with S16/F27
    /// <para>
    /// <see cref="SaveJobs"/> (0), <see cref="RemoveJobs"/> (1) 
    /// </para>
    /// </summary>
    [DataContract(Namespace = Constants.E094NameSpace)]
    public enum S16F27_Action : byte
    {
        [EnumMember]
        SaveJobs = 0,
        [EnumMember]
        RemoveJobs = 1,
    }

    //-------------------------------------------------------------------
}
