//-------------------------------------------------------------------
/*! @file E005.cs
	@brief This file provides common definitions that relate to the use of the E005 interface.

	Copyright (c) Mosaic Systems Inc.,  All rights reserved.
	Copyright (c) 2015 Mosaic Systems Inc.,  All rights reserved.
	Copyright (c) 2006 Mosaic Systems Inc.,  All rights reserved.  (C++ library version)

	Licensed under the Apache License, Version 2.0 (the "License");
	you may not use this file except in compliance with the License.
	You may obtain a copy of the License at

	     http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software
	distributed under the License is distributed on an "AS IS" BASIS,
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	See the License for the specific language governing permissions and
	limitations under the License.
 */
//-------------------------------------------------------------------

namespace MosaicLib.Semi.E005
{
	//-------------------------------------------------------------------
	// Section 9.6, Table 3: Data Item Dictionary

    /// <summary>
    /// Carrier Action Acknowledge code (CAACK).
    /// </summary>
	public enum CAACK : byte
	{
        /// <summary>255: Local default value to use when there is no valid value.</summary>
		Undefinded = 255,
        /// <summary>0: Acknowledged: the command/operation has been performed</summary>
		AcknowledgedCommandHasBeenPerformed = 0,
        /// <summary>1: </summary>
		InvalidCommand = 1,
        /// <summary>2: </summary>
        CannotPerformNow = 2,
        /// <summary>3: Invalid data or parameters were provided to this operation</summary>
        InvalidDataOrArgument = 3,
        /// <summary>4: Acknowledged: the command/operation has been accepted.  Its completion will be signaled by a collection event.</summary>
        AcknowledgedCompletionWillBeSignaledByLaterEvent = 4,
        /// <summary>5: Rejected: The target object's state does not permit this operation to be perfomed now.</summary>
        RejectedInvalidState = 5,
        /// <summary>6: </summary>
        CommandPerformedWithErrors = 6,
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
	public enum ERRCODE : int
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        Undefined = -1,
        /// <summary>0: ERRCODE value to use for success</summary>
		NoError = 0,
        /// <summary></summary>
		UnknownObjectInObjectSpecifier = 1,
        /// <summary></summary>
        UnknownTargetObjectType = 2,
        /// <summary></summary>
        UnknownObjectInstance = 3,
        /// <summary></summary>
        UnknownAttributeName = 4,
        /// <summary></summary>
        ReadonlyAttributeAccessDenied = 5,
        /// <summary></summary>
        UnknownObjectType = 6,
        /// <summary></summary>
        InvalidAttributeValue = 7,
        /// <summary></summary>
        SyntaxError = 8,
        /// <summary></summary>
        VerificationError = 9,
        /// <summary></summary>
        ValidationError = 10,
        /// <summary></summary>
        ObjectIdentifierInUse = 11,
        /// <summary></summary>
        ParametersImproperlySpecified = 12,
        /// <summary></summary>
        InsufficientParametersSpecified = 13,
        /// <summary></summary>
        UnsupportedOptionRequested = 14,
        /// <summary></summary>
        Busy = 15,
        /// <summary></summary>
        NotAvailableForProcessing = 16,
        /// <summary></summary>
        /// <summary></summary>
        CommandNotValidForCurrentState = 17,
        /// <summary></summary>
        NoMaterialAltered = 18,
        /// <summary></summary>
        MaterialPartiallyProcessed = 19,
        /// <summary></summary>
        AllMaterialProcessed = 20,
        /// <summary></summary>
        RecipeSpecificationRelatedError = 21,
        /// <summary></summary>
        FailedDuringProcessing = 22,
        /// <summary></summary>
        FailedWhileNotProcessing = 23,
        /// <summary></summary>
        FailedDueToLackOfMaterial = 24,
        /// <summary></summary>
        JobAborted = 25,
        /// <summary></summary>
        JobStopped = 26,
        /// <summary></summary>
        JobCancelled = 27,
        /// <summary></summary>
        CannotChangeSelectedRecipe = 28,
        /// <summary></summary>
        UnknownEvent = 29,
        /// <summary></summary>
        DuplicateReportID = 30,
        /// <summary></summary>
        UnknownDataReport = 31,
        /// <summary></summary>
        DataReportNotLinked = 32,
        /// <summary></summary>
        UnknownTraceReport = 33,
        /// <summary></summary>
        DuplicateTraceID = 34,
        /// <summary></summary>
        TooManyDataReports = 35,
        /// <summary></summary>
        SamplePeriodOutOfRange = 36,
        /// <summary></summary>
        GroupSizeToLarge = 37,
        /// <summary></summary>
        RecoveryActionCurrentlyInvalid = 38,
        /// <summary></summary>
        BusyWithAnotherRecoveryCurrentlyUnableToPerformTheRecovery = 39,
        /// <summary></summary>
        NoActiveRecoveryAction = 40,
        /// <summary></summary>
        ExceptionRecoveryFailed = 41,
        /// <summary></summary>
        ExceptionRecoveryAborted = 42,
        /// <summary></summary>
        InvalidTableElement = 43,
        /// <summary></summary>
        UnknownTableElement = 44,
        /// <summary></summary>
        CannotDeletePredefined = 45,
        /// <summary></summary>
        InvalidToken = 46,
        /// <summary></summary>
        /// <summary></summary>
        InvalidParameter = 47,
        /// <summary></summary>
        LoadPortDoesNotExist = 48,
        /// <summary></summary>
        LoadPortAlreadyInUse = 49,
        /// <summary></summary>
        MissingCarrier = 50,

		// 51..63 - reserved
		// 64..32767 - user defined
		// 32768..65535 - reserved
		// >= 65536 - user defined

        /// <summary>Internal error: out of memory</summary>
        MemoryAllocationFailed = 100001,
        /// <summary>Internal error: The operation has not been implemented</summary>
        OperationIsNotImplemented = 100002,
        /// <summary>Internal error: The operation caused an unexpected exception to be thrown</summary>
        OperationThrewException = 100003,
        /// <summary>Internal error: The operation detected an internal error</summary>
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

}
