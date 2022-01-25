//-------------------------------------------------------------------
/*! @file E094.cs
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

namespace MosaicLib.Semi.E094
{
    //-------------------------------------------------------------------
    // E094-0306

    /// <summary>
    /// Enumeration to go with CJ "State" property.  Used in U1 format
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
    /// Specifies the known values for Process order management (used during CJ creation).  Used in U1 format.
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
