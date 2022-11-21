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

using System;
using System.Runtime.Serialization;

namespace MosaicLib.Semi.E157
{
    //-------------------------------------------------------------------
    // E157

    /// <summary>
    /// Module Process State
    /// <para/>NoState (0), NotExecuting (1), GeneralExecution (2), StepActive (3), Invalid (255)
    /// </summary>
    [DataContract(Namespace = Constants.E157NameSpace)]
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
    /// <para/>Initial (1), ExecutionStarted (2), ExecutionCompleted (3), ExecutionFailed (4), StepStarted (5), StepCompleted (6), StepFailed (7),
    /// BlockRecordStarted (-10), BlockRecordCompleted (-11), BlockRecordFailed (-12)
    /// </summary>
    [DataContract(Namespace = Constants.E157NameSpace)]
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

        /// <summary>-10: Pseudo Transition - for recording only: Block Record Started (not corresponding state change)</summary>
        [EnumMember]
        BlockRecordStarted = -10,

        /// <summary>-11: Pseudo Transition - for recording only: Block Record Completed (not corresponding state change)</summary>
        [EnumMember]
        BlockRecordCompleted = -11,

        /// <summary>-12: Pseudo Transition - for recording only: Block Record Failed (not corresponding state change)</summary>
        [EnumMember]
        BlockRecordFailed = -12,
    }

    /// <summary>
    /// Module Process State Transitions bit mask enumeration.
    /// <para/>None (0x00), Initial (0x01), ExecutionStarted (0x02), ExecutionCompleted (0x04), ExecutionFailed (0x08), StepStarted (0x20), StepCompleted (0x40), StepFailed (0x80),
    /// BlockRecordStarted (0x200), BlockRecordCompleted (0x400), BlockRecordFailed (0x800)
    /// Standard (0xEF), All (0xEEF)
    /// </summary>
    [Flags, DataContract(Namespace = Constants.E157NameSpace)]
    public enum ModuleProcessStateTransitionBitMask : int
    {
        /// <summary>0x00: placeholder default</summary>
        [EnumMember]
        None = 0x00,

        /// <summary>0x01: Initial (no state -> NotExecuting)</summary>
        [EnumMember]
        Initial = 0x01,

        /// <summary>0x02: Execution Started (NotExecuting -> GeneralExecution)</summary>
        [EnumMember]
        ExecutionStarted = 0x02,

        /// <summary>0x04: Execution Completed (GeneralExecution -> NotExecuting)</summary>
        [EnumMember]
        ExecutionCompleted = 0x04,

        /// <summary>0x08: Execution Failed (GeneralExecution -> NotExecuting)</summary>
        [EnumMember]
        ExecutionFailed = 0x08,

        /// <summary>0x20: Step Started (GeneralExecution -> StepActive)</summary>
        [EnumMember]
        StepStarted = 0x20,

        /// <summary>0x40: Step Completed (StepActive -> GeneralExecution)</summary>
        [EnumMember]
        StepCompleted = 0x40,

        /// <summary>0x80: Step Failed (StepActive -> GeneralExecution)</summary>
        [EnumMember]
        StepFailed = 0x80,

        /// <summary>0x200: Pseudo Transition - for recording only: Block Record Started (not corresponding state change)</summary>
        [EnumMember]
        BlockRecordStarted = 0x200,

        /// <summary>0x400: Pseudo Transition - for recording only: Block Record Completed (not corresponding state change)</summary>
        [EnumMember]
        BlockRecordCompleted = 0x400,

        /// <summary>0x800: Pseudo Transition - for recording only: Block Record Failed (not corresponding state change)</summary>
        [EnumMember]
        BlockRecordFailed = 0x800,

        /// <summary>
        /// 0xEF: the standard set of reportable transitions.  [Initial | ExecutionStarted | ExecutionCompleted | ExecutionFailed | StepStarted | StepCompleted | StepFailed]
        /// </summary>
        [EnumMember]
        Standard = (Initial | ExecutionStarted | ExecutionCompleted | ExecutionFailed | StepStarted | StepCompleted | StepFailed),

        /// <summary>
        /// 0xEEF: the standard set of reportable transitions.  [Initial | ExecutionStarted | ExecutionCompleted | ExecutionFailed | StepStarted | StepCompleted | StepFailed | BlockRecordStarted | BlockRecordCompleted | BlockRecordFailed]
        /// </summary>
        [EnumMember]
        All = (Initial | ExecutionStarted | ExecutionCompleted | ExecutionFailed | StepStarted | StepCompleted | StepFailed | BlockRecordStarted | BlockRecordCompleted | BlockRecordFailed),
    }

    //-------------------------------------------------------------------

    /// <summary>
    /// E157 Extension Methods
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Converts the given <paramref name="transition"/> into its corresponding <see cref="ModuleProcessStateTransitionBitMask"/> value.
        /// </summary>
        public static ModuleProcessStateTransitionBitMask ConvertToBitMask(this ModuleProcessStateTransition transition)
        {
            switch (transition)
            {
                case ModuleProcessStateTransition.Initial: return ModuleProcessStateTransitionBitMask.Initial;
                case ModuleProcessStateTransition.ExecutionStarted: return ModuleProcessStateTransitionBitMask.ExecutionStarted;
                case ModuleProcessStateTransition.ExecutionCompleted: return ModuleProcessStateTransitionBitMask.ExecutionCompleted;
                case ModuleProcessStateTransition.ExecutionFailed: return ModuleProcessStateTransitionBitMask.ExecutionFailed;
                case ModuleProcessStateTransition.StepStarted: return ModuleProcessStateTransitionBitMask.StepStarted;
                case ModuleProcessStateTransition.StepCompleted: return ModuleProcessStateTransitionBitMask.StepCompleted;
                case ModuleProcessStateTransition.StepFailed: return ModuleProcessStateTransitionBitMask.StepFailed;
                case ModuleProcessStateTransition.BlockRecordStarted: return ModuleProcessStateTransitionBitMask.BlockRecordStarted;
                case ModuleProcessStateTransition.BlockRecordCompleted: return ModuleProcessStateTransitionBitMask.BlockRecordCompleted;
                case ModuleProcessStateTransition.BlockRecordFailed: return ModuleProcessStateTransitionBitMask.BlockRecordFailed;
                default: return default(ModuleProcessStateTransitionBitMask);
            }
        }
    }
}
