//-------------------------------------------------------------------
/*! @file E005.cs
 *  @brief This file provides common definitions that relate to the use of the E005 interface.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2015 Mosaic Systems Inc.
 * Copyright (c) 2006 Mosaic Systems Inc.  (C++ library version)
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

namespace MosaicLib.Semi.E005
{
	//-------------------------------------------------------------------
	// Section 9.6, Table 3: Data Item Dictionary

    /// <summary>
    /// Alarm Category Code Byte (the Alarm severity portion of it)
    /// <para/>PersonalSafety (1), EquipmentSafety (2), ParameterControlWarning (3), ParameterControlError (4), IrrecoverableError (5), EquipmentStatusWarning (6), AttentionFlags (7), DataIntegrity (8), 
    /// E041_Attention (100), E041_Warning (101), E041_Error (102), E041_Alarm (103) 
    /// </summary>
    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum ALCD : byte
    {
        [EnumMember]
        PersonalSafety = 1,

        [EnumMember]
        EquipmentSafety = 2,

        [EnumMember]
        ParameterControlWarning = 3,

        [EnumMember]
        ParameterControlError = 4,

        [EnumMember]
        IrrecoverableError = 5,

        [EnumMember]
        EquipmentStatusWarning = 6,

        [EnumMember]
        AttentionFlags = 7,

        [EnumMember]
        DataIntegrity = 8,

        // values 9-63 are reserved

        // values 64..127 are available for custom meanings

        /// <summary>First value in the custom region = 64</summary>
        [EnumMember]
        BeginCustomRegion = 64,

        /// <summary>
        /// This is not an indication of any form of failure.  
        /// It is simply used to inform the user/decision authority of some occurrence and optionally request/prompt the user/decision authority to provide some input.  
        /// These generally do not have an associated ALID. (100)
        /// </summary>
        [EnumMember]
        E041_Attention = 100,

        /// <summary>Similar to an alarm but not generally passed to the host, may not have a known ALID (101)</summary>
        [EnumMember]
        E041_Warning = 101,

        /// <summary>Error annunciators are similar to Alarm annunciators except that they support (recovery) action invocation by the currently active decision authority. (102)</summary>
        [EnumMember]
        E041_Error = 102,

        /// <summary>Under E041 Alarm annunciators are used simply to report exception conditions but they do not offer or support (recovery) action invocation by the decision authority. (103)</summary>
        [EnumMember]
        E041_Alarm = 103,

        /// <summary>Interpretaion of this annunciator type is state and context dependant, especially in relation to when this annunciator is passed to a host.  May or may not have a known ALID. (104)</summary>
        [EnumMember]
        E041_Dynamic = 104,
    }

    /// <summary>
    /// Carrier Action Acknowledge code (CAACK).
    /// </summary>
    [DataContract(Namespace=Constants.SemiNameSpace)]
	public enum CAACK : byte
	{
        /// <summary>255: Local default value to use when there is no valid value.</summary>
        [EnumMember]
		Undefinded = 255,
        /// <summary>0: Acknowledged: the command/operation has been performed</summary>
        [EnumMember]
        AcknowledgedCommandHasBeenPerformed = 0,
        /// <summary>1: The command was not valid</summary>
        [EnumMember]
        InvalidCommand = 1,
        /// <summary>2: The command can not be performed successfully at this time.</summary>
        [EnumMember]
        CannotPerformNow = 2,
        /// <summary>3: Invalid data or parameters were provided to this operation</summary>
        [EnumMember]
        InvalidDataOrArgument = 3,
        /// <summary>4: Acknowledged: the command/operation has been accepted.  Its completion will be signaled by a collection event.</summary>
        [EnumMember]
        AcknowledgedCompletionWillBeSignaledByLaterEvent = 4,
        /// <summary>5: Rejected: The target object's state does not permit this operation to be perfomed now.</summary>
        [EnumMember]
        RejectedInvalidState = 5,
        /// <summary>6: Command performed but did not succeed.</summary>
        [EnumMember]
        CommandPerformedWithErrors = 6,
	}

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum CPACK : byte
    {
        [EnumMember]
        Accepted = 0,
        [EnumMember]
        CPNAME_ParameterNameDoesNotExist = 1,
        [EnumMember]
        CPVALUE_IllegalValueSpecified = 2,
        [EnumMember]
        CPVALUE_IllegalFormatSpecified = 3,
    }

    /// <summary>ExtensionMethod wrapper class</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given caack value is ether CAACK.AcknowledgedCommandHasBeenPerformed or CAACK.AcknowledgedCompletionWillBeSignaledByLaterEvent.
        /// </summary>
        public static bool IsSuccess(this CAACK caack)
        {
            return (caack == CAACK.AcknowledgedCommandHasBeenPerformed || caack == CAACK.AcknowledgedCompletionWillBeSignaledByLaterEvent);
        }
    }

    /// <summary>
    /// A code that is used to identify specifc error conditions.  Used in a wide variety of stream/functions
    /// </summary>
    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum ERRCODE : int
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        [EnumMember]
        Undefined = -1,

        /// <summary>0: ERRCODE value to use for success</summary>
        [EnumMember]
        NoError = 0,

        [EnumMember]
        UnknownObjectInObjectSpecifier = 1,

        [EnumMember]
        UnknownTargetObjectType = 2,
        
        [EnumMember]
        UnknownObjectInstance = 3,
        
        [EnumMember]
        UnknownAttributeName = 4,
        
        [EnumMember]
        ReadonlyAttributeAccessDenied = 5,
        
        [EnumMember]
        UnknownObjectType = 6,
        
        [EnumMember]
        InvalidAttributeValue = 7,
        
        [EnumMember]
        SyntaxError = 8,
        
        [EnumMember]
        VerificationError = 9,
        
        [EnumMember]
        ValidationError = 10,
        
        [EnumMember]
        ObjectIdentifierInUse = 11,
        
        [EnumMember]
        ParametersImproperlySpecified = 12,
        
        [EnumMember]
        InsufficientParametersSpecified = 13,
        
        [EnumMember]
        UnsupportedOptionRequested = 14,
        
        [EnumMember]
        Busy = 15,
        
        [EnumMember]
        NotAvailableForProcessing = 16,
        
        [EnumMember]
        CommandNotValidForCurrentState = 17,
        
        [EnumMember]
        NoMaterialAltered = 18,
        
        [EnumMember]
        MaterialPartiallyProcessed = 19,
        
        [EnumMember]
        AllMaterialProcessed = 20,
        
        [EnumMember]
        RecipeSpecificationRelatedError = 21,
        
        [EnumMember]
        FailedDuringProcessing = 22,
        
        [EnumMember]
        FailedWhileNotProcessing = 23,
        
        [EnumMember]
        FailedDueToLackOfMaterial = 24,
        
        [EnumMember]
        JobAborted = 25,
        
        [EnumMember]
        JobStopped = 26,
        
        [EnumMember]
        JobCancelled = 27,
        
        [EnumMember]
        CannotChangeSelectedRecipe = 28,
        
        [EnumMember]
        UnknownEvent = 29,
        
        [EnumMember]
        DuplicateReportID = 30,
        
        [EnumMember]
        UnknownDataReport = 31,
        
        [EnumMember]
        DataReportNotLinked = 32,
        
        [EnumMember]
        UnknownTraceReport = 33,
        
        [EnumMember]
        DuplicateTraceID = 34,
        
        [EnumMember]
        TooManyDataReports = 35,
        
        [EnumMember]
        SamplePeriodOutOfRange = 36,
        
        [EnumMember]
        GroupSizeToLarge = 37,
        
        [EnumMember]
        RecoveryActionCurrentlyInvalid = 38,
        
        [EnumMember]
        BusyWithAnotherRecoveryCurrentlyUnableToPerformTheRecovery = 39,
        
        [EnumMember]
        NoActiveRecoveryAction = 40,
        
        [EnumMember]
        ExceptionRecoveryFailed = 41,
        
        [EnumMember]
        ExceptionRecoveryAborted = 42,
        
        [EnumMember]
        InvalidTableElement = 43,
        
        [EnumMember]
        UnknownTableElement = 44,
        
        [EnumMember]
        CannotDeletePredefined = 45,
        
        [EnumMember]
        InvalidToken = 46,
        
        [EnumMember]
        InvalidParameter = 47,
        
        [EnumMember]
        LoadPortDoesNotExist = 48,
        
        [EnumMember]
        LoadPortAlreadyInUse = 49,

        [EnumMember]
        MissingCarrier = 50,

		// 51..63 - reserved
		// 64..32767 - user defined
		// 32768..65535 - reserved
		// >= 65536 - user defined

        /// <summary>Internal error: out of memory</summary>
        [EnumMember]
        MemoryAllocationFailed = 100001,

        /// <summary>Internal error: The operation has not been implemented</summary>
        [EnumMember]
        OperationIsNotImplemented = 100002,

        /// <summary>Internal error: The operation caused an unexpected exception to be thrown</summary>
        [EnumMember]
        OperationThrewException = 100003,

        /// <summary>Internal error: The operation detected an internal error</summary>
        [EnumMember]
        OperationDetectedInternalError = 100004,
	}

    /// <summary>ExtensionMethod wrapper class</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given errcode value is ERRCODE.NoError.
        /// </summary>
        public static bool IsSuccess(this ERRCODE errcode)
        {
            return (errcode == ERRCODE.NoError);
        }
    }

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum ACKA : byte
    {
        [EnumMember]
        False = 0,
        [EnumMember]
        True = 1,
    }

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum ACKC7 : byte
    {
        [EnumMember]
        Accepted = 0,
        [EnumMember]
        PermissionNotGranted = 1,
        [EnumMember]
        LengthError = 2,
        [EnumMember]
        MatrixOverflow = 3,
        [EnumMember]
        PPIDNotFound = 4,
        [EnumMember]
        ModeNotSuppoted = 5,
    }

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum ACKC7A : byte
    {
        [EnumMember]
        Accepted = 0,
        [EnumMember]
        MDLNIsNotConsistent = 1,
        [EnumMember]
        SOFTREVIsNotConsistent = 2,
        [EnumMember]
        InvalidCCODE = 3,
        [EnumMember]
        InvalidPPARMValue = 4,
        /// <summary>See ERRW7</summary>
        [EnumMember]
        OtherError = 5,
    }

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum ACKC10
    {
        [EnumMember]
        AcceptedForDisplay = 0,
        [EnumMember]
        MessageWillNotBeDisplayed = 1,
        [EnumMember]
        TerminalNotAvailable = 2,
    }

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum HCACK : byte
    {
        [EnumMember]
        Acknowledge_CommandHasBeenPerformed = 0,
        [EnumMember]
        CommandDoesNotExist = 1,
        [EnumMember]
        CannotPerformNow = 2,
        [EnumMember]
        AtLeastOneParmeterIsInvalid = 3,
        [EnumMember]
        Acknowledge_CompletionWillBeSignaledLaterByEvent = 4,
        [EnumMember]
        Rejected_AlreadyInDesiredCondition = 5,
        [EnumMember]
        NoSuchObjectExists = 6,
        [EnumMember]
        OperationFailed = 64,
        [EnumMember]
        OtherError = 65,
    }

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum MF : byte
    {
        [EnumMember]
        Wafer = 1,
        [EnumMember]
        Cassette = 2,
        [EnumMember]
        DieOrChip = 3,
        [EnumMember]
        Boat = 4,
        [EnumMember]
        Ingot = 5,
        [EnumMember]
        Leadframe = 6,
        [EnumMember]
        Lot = 7,
        [EnumMember]
        Magazine = 8,
        [EnumMember]
        Package = 9,
        [EnumMember]
        Plate = 10,
        [EnumMember]
        Tube = 11,
        [EnumMember]
        Waferframe = 12,
        [EnumMember]
        Carrier = 13,
        [EnumMember]
        Substrate = 14,
    }

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum PPChangeStatus : byte
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        Created = 1,
        [EnumMember]
        Edited = 2,
        [EnumMember]
        Deleted = 3,
    }

    [DataContract(Namespace = Constants.SemiNameSpace)]
    public enum PPGNT : byte
    {
        [EnumMember]
        Ok = 0,
        [EnumMember]
        AlreadyHave = 1,
        [EnumMember]
        NoSpace = 2,
        [EnumMember]
        InvalidPPID = 3,
        [EnumMember]
        BusyTryLater = 4,
        [EnumMember]
        WillNotAccept = 5,
    }
}
