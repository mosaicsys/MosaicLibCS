//-------------------------------------------------------------------
/*! @file E087.cs
	@brief This file provides common definitions that relate to the use of the E087 interface.

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

using System;
using System.Collections.Generic;
using MosaicLib.Semi.E005;
using MosaicLib.Utils;
using System.Text;

namespace MosaicLib.Semi.E087
{
	//-------------------------------------------------------------------
	// E087-0304

	//-------------------------------------------------------------------
	// Section 9: Load Port

    /// <summary>
    /// Load Port Transfer State (LTS)
    /// </summary>
	public enum LTS : int
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
		Undefined = -1,
        /// <summary>0: The port has been placed out of service and cannot be used for carrier handoffs.</summary>
		OutOfService = 0,
        /// <summary>1: Port cannot be used for carrier arrival or departure as the port, or the carrier that is currently there, is in use for some other purpose.</summary>
		TransferBlocked = 1,
        /// <summary>2: Port is ready to have a carrier placed.</summary>
		ReadyToLoad = 2,
        /// <summary>3: Port is ready to have its carrier removed - it may still be clamped depending on carrier unlcamp handling and material transfer start trigger.</summary>
		ReadyToUnload = 3,
        /// <summary>4: this is a summary state (of 1,2,3): used with E87:ChangeServiceStatus</summary>
		InService = 4,
        /// <summary>5: this is a summary state (of 2 and 3)</summary>
		TransferReady = 5,
	}

    /// <summary>
    /// LTS Transitions that match the enumerated valid LTS state transitions.
    /// </summary>
	public enum LTS_Transition : int
	{
        /// <summary>1: no state -> OutOfService or InService</summary>
		Transition1 = 1,
        /// <summary>2: OutOfService -> InService</summary>
		Transition2 = 2,
        /// <summary>3: InService -> OutOfService</summary>
		Transition3 = 3,
        /// <summary>4: InService -> TransferReady or TransferBlocked (on operator inservice or system reset)</summary>
		Transition4 = 4,
        /// <summary>5: TransferReady -> ReadyToLoad or ReadyToUnload (changed to inservice, system reset or transition 10)</summary>
		Transition5 = 5,
        /// <summary>6: ReadyToLoad -> TransferBlocked (manual load or AMHS load started)</summary>
		Transition6 = 6,
        /// <summary>7: ReadyToUnload -> TransferBlocked (manual load or AMHS unload started)</summary>
		Transition7 = 7,
        /// <summary>8: TransferBlocked -> ReadyToLoad</summary>
		Transition8 = 8,
        /// <summary>9: TransferBlocked -> ReadyToUnload</summary>
		Transition9 = 9,
        /// <summary>10: TransferBlocked -> TransferReady (transfer was unsuccessful and the carrier was not loaded or unloaded)</summary>
		Transition10 = 10,
	}

    /// <summary>ExtensionMethod wrapper class</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given LTS value is TransferBlocked, ReadyToLoad, ReadyToUnload, InService (pseudo state), or TransferReady (pseudo state)
        /// </summary>
        public static bool IsInService(this LTS lts)
        {
			switch (lts)
			{
			case LTS.Undefined:			return false;
			case LTS.OutOfService:		return false;
			case LTS.TransferBlocked:	return true;
			case LTS.ReadyToLoad:		return true;
			case LTS.ReadyToUnload:		return true;
			case LTS.InService:			return true;
			case LTS.TransferReady:		return true;
			default:					return false;
			}
        }

        /// <summary>
        /// Returns true if the given LTS value is a valid target state for a service state change (LTS.InService or LTS.OutOfService)
        /// </summary>
        public static bool IsValidServiceStateChangeTarget(this LTS lts)
        {
            return (lts == LTS.InService || lts == LTS.OutOfService);
        }
    }

	// LTS transition events dvs: PortID, PortTransferState (LTS new), CarrierID

	//-------------------------------------------------------------------
	// Section 10: Carrier Object

	// Carrier Object State Model (COSM):

    /// <summary>
    /// Carrier Object State Model (COSM): Carrier ID Status (CIDS)
    /// </summary>
	public enum CIDS : int
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
		Undefined = -1,
        /// <summary>0: CarrierID has not been read yet.</summary>
		IDNotRead = 0,
        /// <summary>1: CarrierID read has been attempted, but value has not yet been verified by equipment or host.</summary>
		WaitingForHost = 1,
        /// <summary>2: CarrierID has been read and verified.</summary>
		IDVerificationOk = 2,
        /// <summary>3: Carrier was canceled before CIDS reached IDVerificationOk state.</summary>
		IDVerificationFailed = 3,
	}

    /// <summary>
    /// Carrier Object State Model (COSM): Carrier Slot Map Status (CSMS)
    /// </summary>
	public enum CSMS : int 
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
		Undefined = -1,
        /// <summary>0: SlotMap has not been obtained yet.</summary>
		SlotMapNotRead = 0,
        /// <summary>1: SlotMap has been obtained, but value has not yet been verified by equipment or host.</summary>
		WaitingForHost = 1,
        /// <summary>2: SlotMap has been obtained and verified.</summary>
		SlotMapVerificationOk = 2,
        /// <summary>3: Carrier was canceled before CSMS reached SlotMapVerficationOk state.</summary>
		SlotMapVerificationFailed = 3,
	}

    /// <summary>
    /// Carrier Object State Model (COSM): Carrier Accessing Status (CAS)
    /// </summary>
	public enum CAS : int
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
		Undefined = -1,
        /// <summary>0: Carrier has not been accessed.</summary>
		NotAccessed = 0,
        /// <summary>1: Carrier has been accessed and it is not complete/stopped</summary>
		InAccess = 1,
        /// <summary>2: Carrier use has been completed normally</summary>
		CarrierComplete = 2,
        /// <summary>3: Carrier use has been completed abnormally (jobs stopped/aborted, processing failed, ...)</summary>
		CarrierStopped = 3,
	}

    /// <summary>
    /// COSM Transitions that match the enumerated valid combined CIDS, CSMS, and CAS state transitions.
    /// </summary>
	public enum COSM_Transition : int
	{
        /// <summary>1: CIDS, CSMS, CAS: noState -> Carrier (A carrier is instantiated - no event required)</summary>
		Transition1 = 1,
        /// <summary>2: CIDS: noState -> IdNotRead (bind or carrier notification)</summary>
		Transition2 = 2,
        /// <summary>3: CIDS: noState -> WaitingForHost (cid not existing was successfully read, cid read successful but equipment based verification failed)</summary>
		Transition3 = 3,
        /// <summary>4: CIDS: noState -> IdVerifyOk (id read fail or unknown cid followed by PWC)</summary>
		Transition4 = 4,
        /// <summary>5: CIDS: noState -> IdVerifyFail (id read fail or unknown cid followed by cancel carrier)</summary>
		Transition5 = 5,
        /// <summary>6: CIDS: IdNotRead -> IdVerifyOk (id read ok and equip verified ok)</summary>
		Transition6 = 6,
        /// <summary>7: CIDS: IdNotRead -> WaitForHost (id read failed)</summary>
		Transition7 = 7,
        /// <summary>8: CIDS: WaitForHost -> IdVerifyOk (pwc received)</summary>
		Transition8 = 8,
        /// <summary>9: CIDS: WaitForHost -> IdVerifyFailed (cancel carrier received)</summary>
		Transition9 = 9,
        /// <summary>10: CIDS: IdNotRead -> WaitForHost (bypass false, carrier rxed with reader offline or not present)</summary>
		Transition10 = 10,
        /// <summary>11: CIDS: IdNotRead -> IdVerifyOk (bypass true, carrier rxed when reader offline or not present)</summary>
		Transition11 = 11,
        /// <summary>12: CSMS: noState -> SMNotRead (carrier instantiated)</summary>
		Transition12 = 12,
        /// <summary>13: CSMS: SMNotRead -> SMVerifyOk (sm read and equip verified ok)</summary>
		Transition13 = 13,
        /// <summary>14: CSMS: SMNotRead -> WaitForHost (sm read and wait for host to verify, sm read but equip did not verify, sm read failed, sm read with abnormal substrate position)</summary>
		Transition14 = 14,
        /// <summary>15: CSMS: WaitForHost -> SMVerifyOk (PWC received)</summary>
		Transition15 = 15,
        /// <summary>16: CSMS: WaitForHost -> SMVerifyFailed (CancleCarrier received)</summary>
		Transition16 = 16,
        /// <summary>17: CAS: noState -> NotAccessed (carrier instantiated)</summary>
		Transition17 = 17,
        /// <summary>18: CAS: NotAccessed -> InAccess (access started)</summary>
		Transition18 = 18,
        /// <summary>19: CAS: InAccess -> CarrierComplete (access completed normally)</summary>
		Transition19 = 19,
        /// <summary>20: CAS: InAccess -> CarrierStopped (access completed abnormally - jobs failed, or cancel carrier)</summary>
		Transition20 = 20,
        /// <summary>21: CIDS, CSMS, CAS: Carrier -> noState (carrier unloaded from tool, CancelBind or CancelCarrierNotification, equip verify failed and equip initiated cancel bind).</summary>
		Transition21 = 21,
	}

	// COSM transition events dvs: PortID, CarrierID, LocationID, CIDS, CSMS, CAS, SlotMap, Reason

	//-------------------------------------------------------------------
	// Section 11: Access Mode

    /// <summary>
    /// Access Mode State (AMS)
    /// </summary>
	public enum AMS : int
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
		Undefined = -1,
        /// <summary>0: Port is to be used for Manual handoff (by hand or using PGV)</summary>
		Manual = 0,
        /// <summary>1: Port is to be used for Automatic handoff using E084 interactions with OHT or AGV type AMHS hardware</summary>
		Automatic = 1,
	}

    /// <summary>
    /// AMS Transitions that match the enumerated valid AMS state transitions.
    /// </summary>
	public enum AMS_Transition : int
	{
        /// <summary>1: noState -> Manual or Auto (system restart)</summary>
		Transition1 = 1,
        /// <summary>2: Manual -> Auto (change access performed, Can happen anytime except during carrier transfer)</summary>
		Transition2 = 2,
        /// <summary>3: Auto -> Manual (change access performed, Can happen anytime except during carrier transfer)</summary>
		Transition3 = 3,
	}

	// AMS transition events dvs: PortID, AMS

	//-------------------------------------------------------------------
	// Section 12: Reservation State Model

    /// <summary>
    /// Load Port Reservation State Model (LRS)
    /// </summary>
	public enum LRS : int
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
		Undefined = -1,
        /// <summary>0: The Port position has been reserved for later carrier arrival</summary>
		NotReserved = 0,
        /// <summary>1: The Port position has not been reserved for later carrier arrival</summary>
		Reserved = 1,
	}

    /// <summary>
    /// LRS Transitions that match the enumerated valid LRS state transitions.
    /// </summary>
	public enum LRS_Transition : int
	{
        /// <summary>1: noState -> NotReserved (system reset)</summary>
		Transition1 = 1,
        /// <summary>2: NotReserved -> Reserved (reserveAtPort or bind)</summary>
		Transition2 = 2,
        /// <summary>3: Reserved -> NotReserved (cancelReservation, cancel bind)</summary>
		Transition3 = 3,
	}

	// LRS transition events dvs: PortID, LRS, CarrierID

	//-------------------------------------------------------------------
	// Section 13: Load Port/Carrier Association State Model

    /// <summary>
    /// Load Port/Carrier Association State Model (LCAS)
    /// </summary>
	public enum LCAS : int
	{
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
		Undefined = -1,
        /// <summary>0: Port is not associated with a carrier object</summary>
		NotAssociated = 0,
        /// <summary>1: Port is associated with a carrier object</summary>
		Associated = 1,
	}

    /// <summary>
    /// LCAS Transitions that match the enumerated valid LCAS state transitions.
    /// </summary>
	public enum LCAS_Transition : int
	{
        /// <summary>1: noState -> NotAssociated (system reset)</summary>
		Transition1 = 1,
        /// <summary>2: NotAssociated -> Associated (bind when port not occupied, carrierID read for unassocated port, pwc or cancel carrier in response to cid read fail or unknown carrierID)</summary>
		Transition2 = 2,
        /// <summary>3: Associated -> NotAssociated (cancel bind or carrier departed port)</summary>
		Transition3 = 3,
        /// <summary>4: Associated -> Associated (equip based verify failed - reassoc with value read)</summary>
		Transition4 = 4,
	}

	// LCAS transition events dvs: PortID, LCAS, CarrierID

	//-------------------------------------------------------------------
	// Section 15: Carrier Release Control, Carrier Unclamp control

    /// <summary>
    /// Values that are generally used to define Carrier Hold Control behavior.
    /// </summary>
	public enum CarrierHoldControl : byte
	{
        /// <summary>0: Local default value to use when there is no valid value.</summary>
		CarrierHoldControl_Undefined = 0,
        /// <summary>1: E87 Release command must be explicitly between CAS transitions to CarrierComplete or CarrierStopped and LTS transition to ReadyToUnload</summary>
		CarrierHoldControl_HostRelease = 1,
        /// <summary>2: LTS transitions to ReadyToUnload directly after CAS transitions to CarrierComplete or CarrierStopped</summary>
		CarrierHoldControl_EquipmentRelease = 2,
	}

    /// <summary>
    /// Values that are generally used to define Auto Unclamp Control behavior.
    /// </summary>
	public enum AutoUnclampControl : byte
	{
        /// <summary>0: Local default value to use when there is no valid value.</summary>
		AutoUnclampControl_Undefined = 0,
        /// <summary>1: unclamp on transition to CarrierComplete or CarrierStopped (or carrier canceled)</summary>
		AutoUnclampControl_CarrierTriggeredUnclamp = 1,
        /// <summary>2: unclamp during transfer start (from TRREQ to READY)</summary>
		AutoUnclampControl_AMHSTriggeredUnclamp = 2,
	}

	//-------------------------------------------------------------------
	// Section 16: Services

    /// <summary>
    /// A pair of status related values (error code and error text)
    /// </summary>
	public class StatusPair
	{
        /// <summary>
        /// Default constructor.  Produces a successful StatusPair with Code = ERRCODE.NoError and Text = String.Empty.
        /// <para/>This constructor may be used with property initializers to create StatusPair objects with alternate contents.
        /// </summary>
        public StatusPair() 
        {
            ErrCode = ERRCODE.NoError;
            ErrText = String.Empty;
        }

        /// <summary>The ERRCODE code value for this StatusPair</summary>
		public ERRCODE ErrCode { get; set; }
        /// <summary>The String text value for this StatusPair</summary>
		public String ErrText { get; set; }

        /// <summary>Returns true if the Code is ERRCODE.NoError and the Text is empty</summary>
		public bool IsSuccess
        { 
            get 
            { 
                return (ErrCode.IsSuccess() && ErrText == String.Empty); 
            } 
        }

        /// <summary>Returns a convenient string version of the contents of this object for logging and debugging purposes</summary>
        public override string ToString()
        {
            if (IsSuccess)
                return "Success";
            else
                return Fcns.CheckedFormat("ErrCode:{0}[{1}] ErrText:'{2}'", ErrCode, unchecked((int) ErrCode), ErrText);
        }
    }

    /// <summary>
    /// Used to generate and handle response error code sets for Carrier Action related Stream/Functions.
    /// </summary>
	public class CarrierActionResult
	{
        /// <summary>Gives the outer CAACK value.</summary>
		public E005.CAACK CAACK = CAACK.Undefinded;

        /// <summary>Gives a list of StatusPair values.</summary>
		public List<StatusPair> StatusList  
        { 
            get 
            { 
                if (statusList == null) 
                    statusList = new List<StatusPair>(); 
                return statusList; 
            }
            set { statusList = value; }
        }

        /// <summary>Returns true if the StatusList is empty</summary>
        public bool StatusListIsEmpty
        {
            get { return (statusList == null || statusList.Count == 0); }
        }

        /// <summary>Returns true if the StatusList is empty or it contains a single IsSuccess StatusPair</summary>
        public bool StatusListIsSuccess
        {
            get { return (StatusListIsEmpty || (statusList.Count == 1 && statusList[0].IsSuccess)); }
        }

        /// <summary>backing storage for StatusList property</summary>
        private List<StatusPair> statusList = null;

        /// <summary>
        /// Default constructor.  
        /// Sets CAACK = CAACK.Undefined and StatusList to be empty.
        /// <para/>This is generally used with property initializers to complete the content construction.
        /// </summary>
        public CarrierActionResult() 
        {
            CAACK = CAACK.Undefinded;
        }

        /// <summary>
        /// Gives get/set access to the first element of the StatusList.  
        /// Getter returns first StatusPair in StatusList, setting the list to contain a single successfull StatusPair if required.
        /// Setter sets the first element of the StatusList to the given StatusPair value, creating or enlarging the StatusList as needed.
        /// </summary>
        public StatusPair FirstStatusPair
        {
            get 
            {
                if (statusList == null || statusList.Count == 0)
                    statusList = new List<StatusPair>() { new StatusPair() };

                return StatusList[0];
            }
            set 
            {
                if (statusList == null)
                    statusList = new List<StatusPair> { value };
                else if (statusList.Count == 0)
                    statusList.Add(value);
                else
                    statusList[0] = value;
            }
        }

        /// <summary>Returns true if the CAACK value IsSuccess and StatusListIsSuccess (list is empty or contains single IsSuccess StatusPair).</summary>
	    public bool IsSuccess
		{ 
            get
            {
                return (CAACK.IsSuccess() && StatusListIsSuccess);
            }
		}

        /// <summary>If the CAACK is in its constructor default value then this method sets it to the given value.  In either case the method returns the target object to support call chaining.</summary>
		public CarrierActionResult SetFirstCAACK(CAACK caack)
        { 
            if (CAACK == CAACK.Undefinded) 
                CAACK = caack; 

            return this;
        }

        /// <summary>Sets the first CAACK to CAACK.CommandPerformedWithErrors.  Supports call chaining.</summary>
        public CarrierActionResult SetCommandFailed() { SetFirstCAACK(CAACK.CommandPerformedWithErrors); return this; }
        /// <summary>Sets the first CAACK to CAACK.CommandPerformedWithErrors.  Attaches a StatusPair with the given errCode and errText.  Supports call chaining.</summary>
        public CarrierActionResult SetCommandFailed(ERRCODE errCode, String errText) { SetCommandFailed(); AttachStatus(errCode, errText); return this; }
        /// <summary>Sets the first CAACK to CAACK.AcknowledgedCompletionWillBeSignaledByLaterEvent.  Supports call chaining.</summary>
        public CarrierActionResult SetCommandCompletionWillBeSignaledByLaterEvent() { SetFirstCAACK(CAACK.AcknowledgedCompletionWillBeSignaledByLaterEvent); return this; }
        /// <summary>Sets the first CAACK to CAACK.InvalidCommand.  Attaches a StatusPair with the given errCode and errText.  Supports call chaining.</summary>
        public CarrierActionResult SetCommandInvalid(ERRCODE errCode, String errText) { SetFirstCAACK(CAACK.InvalidCommand); AttachStatus(errCode, errText); return this; }
        /// <summary>Sets the first CAACK to CAACK.CannotPerformNow.  Attaches a StatusPair with the given errCode and errText.  Supports call chaining.</summary>
        public CarrierActionResult SetCannotPerformNow(ERRCODE errCode, String errText) { SetFirstCAACK(CAACK.CannotPerformNow); AttachStatus(errCode, errText); return this; }
        /// <summary>Sets the first CAACK to CAACK.InvalidDataOrArgument.  Attaches a StatusPair with the given errCode and errText.  Supports call chaining.</summary>
        public CarrierActionResult SetInvalidDataOrArgument(ERRCODE errCode, String errText) { SetFirstCAACK(CAACK.InvalidDataOrArgument); AttachStatus(errCode, errText); return this; }
        /// <summary>Sets the first CAACK to CAACK.RejectedInvalidState.  Attaches a StatusPair with the given errCode and errText.  Supports call chaining.</summary>
        public CarrierActionResult SetRejectedInvalidState(ERRCODE errCode, String errText) { SetFirstCAACK(CAACK.RejectedInvalidState); AttachStatus(errCode, errText); return this; }

        /// <summary>Constructs and Attaches a new StatusPair from the given errCode and errText.  Supports call chaining.</summary>
        public CarrierActionResult AttachStatus(ERRCODE errCode, String errText) { StatusList.Add(new StatusPair() { ErrCode = errCode, ErrText = errText }); return this; }
        /// <summary>Attaches the given StatusPair.  Supports call chaining.</summary>
        public CarrierActionResult AttachStatus(StatusPair statusPair) { StatusList.Add(statusPair); return this; }

        /// <summary>Special case of SetCannotPerformNow which uses ERRCODE.OperationIsNotImplemented and an ErrText derived from the given function name.  Supports call chaining.</summary>
        public CarrierActionResult FunctionNotImplemented(String functionName) { SetCannotPerformNow(ERRCODE.OperationIsNotImplemented, "Operation:" + functionName + " is not implemented"); return this; }

        /// <summary>If the StatusListIsSuccess then sets the first CAACK to CAACK.AcknowledgedCommandHasBeenPerformed otherwise replaces either IsSuccess CAACK with CAACK.CommandPerformedWithErrors.  Supports call chaining.</summary>
        public CarrierActionResult SetCommandHasBeenPerformed()								
		{
            if (StatusListIsSuccess)
            {
				return SetFirstCAACK(CAACK.AcknowledgedCommandHasBeenPerformed);
            }
			else if (CAACK.IsSuccess() && !StatusListIsSuccess)
            {
				CAACK = CAACK.CommandPerformedWithErrors;
            }

            return this;
		}

        /// <summary>
        /// Generates and returns this object as an Xml'is formated E005 data object composed of L, U1, U4 and A elements.
        /// </summary>
        public string ToXMLString()
        {
            // returns something like <L> <U1>caack<U1> <L> <L> <U4>errcode</U4> <A>errtext</A> </L> ... </L>

            StringBuilder sb = new StringBuilder();

            sb.CheckedAppendFormat(@"<L> <U1>{0}</U1> <L>", unchecked((int) CAACK));

            foreach (StatusPair statusPair in StatusList)
            {
                sb.CheckedAppendFormat(" <L> <U4>{0}</U4> <A>{1}</A> </L>", unchecked((int) statusPair.ErrCode), Fcns.MapNullToEmpty(statusPair.ErrText));
            }

            sb.Append(" </L> </L>");

            return sb.ToString();
        }
	};

	//-------------------------------------------------------------------
	// Section 18: Additional Events

	// BufferCapacityChangedEvent (BufferPartitionInfo)
	// CarrierApproachingCompleteEvent (CarrierID)
	// CarrierClampedEvent (PortID, CarrierID, LocationID)
	// CarrierClosedEvent (PortID, CarrierID, LocationID)
	// CarrierLocationChangeEvent (CarrierID, LocationID:new, CarrierLocationMatrix)
	// CarrierOpenedEvent (CarrierID, LocationID, PortID)
	// CarrierUnclampedEvent (PortID, CarrierID, LocationID)
	// CarrierIDReadFailEvent (PortID, CarrierID, LocationID)
	// IDReaderAvailableEvent (PortID)
	// IDReaderUnavailableEvent (PortID)
	// UnknownCarrierIDEvent (PortID)
	// 

	//-------------------------------------------------------------------
	// Section 19: Variable Data

	//-------------------------------------------------------------------
	// Section 20: Alarms

    /// <summary>
    /// an enum listing each of the alarms that may be produced by this system.
    /// </summary>
	public enum Alarms : int
	{
        /// <summary>A PIO state or usage error was detected.</summary>
		PIOFailure,
        /// <summary>attempt was made to manually deliver a carrier to port in auto mode, or auto delivery was started to a port in manual mode (also sets PIOFailed)</summary>
		AccessModeViolation,

		CarrierVerificationFailure,

        SlotMapReadFailed,

        SlotMapVerificationFailed,

        AttemptToUseOutOfServiceLoadPort,

        CarrierPresenceError,

        CarrierPlacementError,

        /// <summary>Dock or Undock failed</summary>
		CarrierDockFailure,

        /// <summary>Open or Close failed</summary>
		CarrierDoorFailure,

        DuplicateCarrierID,

        CarrierRemovalError,

        /// <summary>Clamp or Unclamp failed</summary>
		CarrierClampFailure,

        /// <summary>manual transfer started but not finished</summary>
		ManualTransferFailed,

        PortOperationFailed,
	}

	//-------------------------------------------------------------------
	// Section 6.1 Table 4

    /// <summary>
    /// Defines the known SlotState values
    /// </summary>
	public enum SlotState : int	// enum for values stored in a SlotMap
	{
        /// <summary>-1 or ?: the substrate slot map state for this slot is not valid</summary>
        Invalid = -1,
        /// <summary>0 or !: the substrate map result is not known for this slot or the map result indicates some unknown error for this slot (can be a consolidation of cross and double slotted for some mapping hardware)</summary>
		Undefined = 0,
        /// <summary>1 or -: the slot is known to be empty</summary>
		Empty = 1,
        /// <summary>2 or *: no mapping hardware available.  Slot is presumed occupied until additional information is available about it.</summary>
		NotEmpty = 2, 
        /// <summary>3 or o: slot is known to contain a substrate that passed the position and size tolerance tests.</summary>
		CorrectlyOccupied = 3,
        /// <summary>4 or D: slot is believed to contain a double slotted (thick) substrate.</summary>
		DoubleSlotted = 4, 
        /// <summary>5 or X: slot is believed to contain a cross sloted substrate.  May also apply to slots that are adjacent to such a wafer.</summary>
		CrossSlotted = 5,
	}

    /// <summary>
    /// Defines the variants for ToString conversions for List{SlotState} and SlotState[] ToString extension methods.
    /// <para/>Graphics (?!-*oDX) and Digits(?012345)
    /// </summary>
    public enum SlotStateStringFormat
    {
        /// <summary>Generate string version of SlotSet set using mapping of ? = Invalid, ! = Undefined, - = Empty, * = NotEmpty, o = CorrectlyOccupied, D = DoubleSlotted, X = CrossSlotted</summary>
        Graphics,
        /// <summary>Generate string version of SlotSet set using mapping of ? = Invalid, 0 = Undefined, 1 = Empty, 2 = NotEmpty, 3 = CorrectlyOccupied, 4 = DoubleSlotted, 5 = CrossSlotted</summary>
        Digits
    }

    /// <summary>ExtensionMethod wrapper class</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Sets the given slotStateList to contain the decoded version of the given fromSlotMapStr using the character parsing from the SlotState enum summary comments
        /// <para/>? = Invalid, ! = Undefined, - = Empty, * = NotEmpty, o = CorrectlyOccupied, D = DoubleSlotted, X = CrossSlotted, digits = corresponding states, all else = Invalid
        /// <para/>Returns the given slotStateList (or a new one if the method was passed null for the list.  If needed, the given list will be cleared before parsing.
        /// </summary>
        public static List<SlotState> Parse(this List<SlotState> slotStateList, string fromSlotMapStr)
        {
            return Parse(slotStateList, fromSlotMapStr, false);
        }

        /// <summary>
        /// Sets, or appends, the given slotStateList to contain the decoded version of the given fromSlotMapStr using the character parsing from the SlotState enum summary comments
        /// <para/>? = Invalid, ! = Undefined, - = Empty, * = NotEmpty, o = CorrectlyOccupied, D = DoubleSlotted, X = CrossSlotted, digits = corresponding states, all else = Invalid
        /// <para/>Returns the given slotStateList (or a new one if the method was passed null for the list.  If append is false and the list is not empty on entry, the given list will be cleared before parsing.
        /// </summary>
        public static List<SlotState> Parse(this List<SlotState> slotStateList, string fromSlotMapStr, bool append)
        {
            if (slotStateList == null)
                slotStateList = new List<SlotState>();

            if (!append && slotStateList.Count != 0)
                slotStateList.Clear();

            foreach (char c in Fcns.MapNullToEmpty(fromSlotMapStr))
            {
                switch (c)
                {
                    case '!':
                    case '0': slotStateList.Add(SlotState.Undefined); break;
                    case '-':
                    case '1': slotStateList.Add(SlotState.Empty); break;
                    case '*':
                    case '2': slotStateList.Add(SlotState.NotEmpty); break;
                    case 'o': 
                    case 'O':
                    case '3': slotStateList.Add(SlotState.CorrectlyOccupied); break;
                    case 'd':
                    case 'D':
                    case '4': slotStateList.Add(SlotState.DoubleSlotted); break;
                    case 'x':
                    case 'X':
                    case '5': slotStateList.Add(SlotState.CrossSlotted); break;
                    case '?':
                    default: slotStateList.Add(SlotState.Invalid); break;
                }
            }

            return slotStateList;
        }

        /// <summary>
        /// Converts the given slotStateList to a string using the requested format of Graphics or Digits, and returns the string.
        /// </summary>
        public static string ToString(this List<SlotState> slotStateList, SlotStateStringFormat format)
        {
            return ToString(slotStateList, (format == SlotStateStringFormat.Graphics ? graphicsCharArray : digitsCharArray));
        }

        /// <summary>Char array used to translate a SlotState value (casted as an integer + 1) into a graphical character</summary>
        public static readonly char[] graphicsCharArray = ("?!-*oDX".ToCharArray());
        /// <summary>Char array used to translate a SlotState value (casted as an integer) into a digit equivilant.  SlotState.Invalid is transformed to '?'</summary>
        public static readonly char[] digitsCharArray = ("?012345".ToCharArray());

        /// <summary>
        /// Converts the given slotStateList to a string using the given char array which is used to obtain the character for each
        /// SlotState value (casted as an integer), shifted right by one.  char array index 0 is for the SlotState.Invalid state,
        /// char array index 1 is for SlotState.Undefined, etc... All states that after incrementing, do not map to valid index will
        /// be represented in the output by a ?.
        /// </summary>
        public static string ToString(this List<SlotState> slotStateList, char[] slotStateToCharMappingArray)
        {
            StringBuilder sb = new StringBuilder();

            if (slotStateList != null)
            {
                foreach (SlotState slotState in slotStateList)
                {
                    sb.Append(slotStateToCharMappingArray.SafeAccess(1 + unchecked((int)slotState), '?'));
                }
            }

            return sb.ToString();
        }
    }

	//-------------------------------------------------------------------

	// End items that are derived directly from E087 and/or other Semi standard 
	//	document(s).
}
