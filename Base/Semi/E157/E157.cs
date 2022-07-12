//-------------------------------------------------------------------
/*! @file E157.cs
 *  @brief This file provides common definitions that relate to the use of the E157 interface.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2022 Mosaic Systems Inc.
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

namespace MosaicLib.Semi.E157
{
    //-------------------------------------------------------------------
    // E157

    /// <summary>
    /// Module Process State
    /// <para/>NoState (0), NotExecuting (1), GeneralExecution (2), StepActive (3), Invalid (255)
    /// </summary>
    [DataContract(Namespace = Constants.E094NameSpace)]
    public enum ModuleProcessState : byte
    {
        [EnumMember]
        NoState = 0,

        [EnumMember]
        NotExecuting = 1,

        [EnumMember]
        GeneralExecution = 2,

        [EnumMember]
        StepActive = 3,

        [EnumMember]
        Invalid = 255,      // locally defined value
    }

    /// <summary>
    /// Named Module Process State Transitions.
    /// <para/>Initial (1), ExecutionStarted (2), ExecutionCompleted (3), ExecutionFailed (4), StepStarted (5), StepCompleted (6), StepFailed (7)
    /// </summary>
    [DataContract(Namespace = Constants.E116NameSpace)]
    public enum ModuleProcessStateTransition : int
    {
        /// <summary>1: Initial (no state -> NotExecuting)</summary>
        [EnumMember]
        Initial = 1,

        /// <summary>2: Execution Started (NotExecuting -> GeneralExecution)</summary>
        [EnumMember]
        ExecutionStarted = 2,

        /// <summary>3: Execution Completed (GeneralExecution -> NotExecuting)</summary>
        [EnumMember]
        ExecutionCompleted = 3,

        /// <summary>4: Execution Failed (GeneralExecution -> NotExecuting)</summary>
        [EnumMember]
        ExecutionFailed = 4,

        /// <summary>5: Step Started (GeneralExecution -> StepActive)</summary>
        [EnumMember]
        StepStarted = 5,

        /// <summary>6: Step Completed (StepActive -> GeneralExecution)</summary>
        [EnumMember]
        StepCompleted = 6,
        
        /// <summary>7: Step Failed (StepActive -> GeneralExecution)</summary>
        [EnumMember]
        StepFailed = 7,
    }

    //-------------------------------------------------------------------
}
