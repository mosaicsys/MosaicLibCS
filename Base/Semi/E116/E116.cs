//-------------------------------------------------------------------
/*! @file E116.cs
 *  @brief This file provides common definitions that relate to the use of the E116-0306 interface.
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

namespace MosaicLib.Semi.E116
{
    //-------------------------------------------------------------------
    // Definitions from E116-0306

    /// <summary>
    /// E116 EPT State
    /// <para/>Idle (0), Busy (1), Blocked (2), NoState (3), Invalid (255)
    /// </summary>
    /// <remarks>
    /// Module specific notes:
    /// <para/>Idle: a module may only be idle if there is no material present, and the module is not executing a task, and the module's current condition indicates it is available to prepare for and/or process at least one type of useful task (aka no fault conditions are active that would block ability to perform useful task related work)
    /// <para/>Busy: the module module is currently executing a task and it does not have any fault conditions active that prevent the execution of at least one task.  The module may have material present in this state.
    /// <para/>Blocked: the module is unable to start a task or to continue executing the current task.  This may be due to fault conditions, or while aborting processing by request, or while idle with aborted material present, or while pausing processing by request, or while paused (awaiting resume request).  The module may have material present in this state.
    /// </remarks>
    [DataContract(Namespace = Constants.E116NameSpace)]
    public enum EPTState : byte
    {
        [EnumMember]
        Idle = 0,

        [EnumMember]
        Busy = 1,

        [EnumMember]
        Blocked = 2,

        [EnumMember]
        NoState = 3,

        [EnumMember]
        Invalid = 255,      // locally defined value
    }

    /// <summary>
    /// E116 EPT Element Type
    /// <para/>Equipment (0), ProductionEPTModule (2), EFEMOrLoadPortEPTModule (2), Invalid (255)
    /// </summary>
    [DataContract(Namespace = Constants.E116NameSpace)]
    public enum EPTElementType : byte
    {
        [EnumMember]
        Equipment = 0,

        [EnumMember]
        ProductionEPTModule = 1,

        [EnumMember]
        EFEMOrLoadPortEPTModule = 2,

        [EnumMember]
        Invalid = 255,      // locally defined value
    }

    /// <summary>
    /// E116 Task Type
    /// <para/>NoTask (0), Unspecified (1), Process (2), Support (3), EquipmentMaintenance (4), EquipmentDiagnostics (5), Waiting (6)
    /// </summary>
    [DataContract(Namespace = Constants.E116NameSpace)]
    public enum TaskType : byte
    {
        /// <summary>The value to use when no task is active and no task execution has been interrupted by a trasition to the Blocked state.</summary>
        [EnumMember]
        NoTask = 0,

        /// <summary>Placeholder value to use when no other task type listed here is suitable.</summary>
        [EnumMember]
        Unspecified = 1,
        
        /// <summary>
        /// This task adds value, typically to specific material.  
        /// This may include causing desired physical changes to the material or performing desired analysis of the material as part of quality control.
        /// This type may be used when the task is a purge operation that is required to produce desired process results.  
        /// Generally operations that can be catagorized as Support will not be reported as being a Process Task.
        /// </summary>
        [EnumMember]
        Process = 2,
        
        /// <summary>
        /// This task does not add value either because there is no material or because it is required before another value adding task can begin.  
        /// This includes material handling, and material alignment, as well as other pure material transfer related operations including loadlock pump and vent operations.
        /// </summary>
        [EnumMember]
        Support = 3,
        
        /// <summary>This task is used to maintain the equipment's functionality or performance, such as cleaning cycles</summary>
        [EnumMember]
        EquipmentMaintenance = 4,
        
        /// <summary>This task covers operations used to determine the equipment's health</summary>
        [EnumMember]
        EquipmentDiagnostics = 5,
        
        /// <summary>
        /// This task covers normal conditions where a module is waiting for an external entity to complete a dependent activity. 
        /// Example cases include when a module is waiting for a robot (or pgv) to remove completed material, 
        /// or when a module is waiting for the host, or some other decision authority, to explicitly confirm some reported information (such as LPT waiting for E087 ProceedWithCarrier).
        /// </summary>
        [EnumMember]
        Waiting = 6,
    }

    /// <summary>
    /// E116 Blocked Reason
    /// <para/>NotBlocked (0), Unknown (1), SafetyThreshold (2), ErrorCondition (3), ParametricException (4), Abort (5), Pause (6), Reserved_7 (7), Reserved_8 (8), Reserved_9 (9)
    /// </summary>
    /// <remarks>
    /// BlockedReasonText is expected to read as one of:
    /// <para/>Fault: - 'description of fault condition'
    /// <para/>Abort: - 'description of abort situation'
    /// <para/>Pause: - 'description of pause situation'
    /// </remarks>
    [DataContract(Namespace = Constants.E116NameSpace)]
    public enum BlockedReason : byte
    {
        /// <summary>Value used when the module is not blocked.</summary>
        [EnumMember]
        NotBlocked = 0,
        
        /// <summary>Value used when no other reason code is a better choice.</summary>
        [EnumMember]
        Unknown = 1,
        
        /// <summary>module is blocked because a safety threshold condition is not being met.</summary>
        [EnumMember]
        SafetyThreshold = 2,
        
        /// <summary>module is blocked because of some other Error (or fault) Condition</summary>
        [EnumMember]
        ErrorCondition = 3,
        
        /// <summary>the precise meaning of this reason does not appear to be defind in the standard.</summary>
        [EnumMember]
        ParametricException = 4,
        
        /// <summary>module is blocked because it is aborting processing of a prior task or because it is effectively idle with correspondingly aborted material present.</summary>
        [EnumMember]
        Abort = 5,
        
        /// <summary>module is blocked because it is pausing processing of a prior task or because it is waiting to be resumed.</summary>
        [EnumMember]
        Pause = 6,

        [EnumMember]
        Reserved_7 = 7,

        [EnumMember]
        Reserved_8 = 8,

        [EnumMember]
        Reserved_9 = 9,
    }

    /// <summary>
    /// Locally defined additional set of blocked reasons.
    /// <para/>NotBlocked (0), Unknown (1), SafetyThreshold (2), ErrorCondition (3), ParametricException (4), Abort (5), Pause (6), 
    /// FaultDetected (128), FaultConditionIsActive (129), FacilitiesRelatedFaultConditionIsActive (130),
    /// </summary>
    [DataContract(Namespace = Constants.E116NameSpace)]
    public enum BlockedReasonEx : byte
    {
        /// <summary>Value used when the module is not blocked.</summary>
        [EnumMember]
        NotBlocked = 0,
        /// <summary>Value used when no other reason code is a better choice.</summary>
        [EnumMember]
        Unknown = 1,
        /// <summary>module is blocked because a safety threshold condition is not being met.</summary>
        [EnumMember]
        SafetyThreshold = 2,
        /// <summary>module is blocked because of some other Error (or fault) Condition</summary>
        [EnumMember]
        ErrorCondition = 3,
        /// <summary>the precise meaning of this reason does not appear to be defind in the standard.</summary>
        [EnumMember]
        ParametricException = 4,
        /// <summary>module is blocked because it is aborting processing of a prior task or because it is effectively idle with correspondingly aborted material present.</summary>
        [EnumMember]
        Abort = 5,
        /// <summary>module is blocked because it is pausing processing of a prior task or because it is waiting to be resumed.</summary>
        [EnumMember]
        Pause = 6,

        /// <summary>module is blocked because a fault condition occurance terminated normal task processing or normal residence in the idle state.  The module will generally require being successfully initialized before it can return to idle.</summary>
        [EnumMember]
        FaultDetected = 128,

        /// <summary>module is blocked because a fault condition is active.  The module may return to Idle after the condition has been resolved and any corresponding Annuciator has been acknoledged or the module has successfully been initialized.</summary>
        [EnumMember]
        FaultConditionIsActive = 129,

        /// <summary>module is blocked because a facilities related fault condition is active.  The module may return to Idle after the condition has been resolved and any corresponding Annuciator has been acknoledged or the module has successfully been initialized.</summary>
        [EnumMember]
        FacilitiesRelatedFaultConditionIsActive = 130,
    }

    /// <summary>
    /// EPT Transitions that match the enumerated valid EPT state transitions.
    /// </summary>
    [DataContract(Namespace = Constants.E116NameSpace)]
    public enum EPTTransition : int
    {
        /// <summary>1: no state -> Any (initialization complete)</summary>
        [EnumMember]
        Transition1 = 1,
        /// <summary>2: Idle -> Busy</summary>
        [EnumMember]
        Transition2 = 2,
        /// <summary>3: Busy -> Idle</summary>
        [EnumMember]
        Transition3 = 3,
        /// <summary>4: Busy -> Busy</summary>
        [EnumMember]
        Transition4 = 4,
        /// <summary>5: Busy -> Blocked</summary>
        [EnumMember]
        Transition5 = 5,
        /// <summary>6: Blocked -> Busy</summary>
        [EnumMember]
        Transition6 = 6,
        /// <summary>7: Blocked -> Idle</summary>
        [EnumMember]
        Transition7 = 7,
        /// <summary>8: Idle -> Blocked</summary>
        [EnumMember]
        Transition8 = 8,
        /// <summary>9: Blocked -> Blocked</summary>
        [EnumMember]
        Transition9 = 9,
    }

    //-------------------------------------------------------------------
}
