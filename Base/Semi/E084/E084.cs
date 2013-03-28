//-------------------------------------------------------------------
/*! @file E084.cs
 *  @brief This file provides common definitions that relate to the use of the Semi standard E084 interface.
 *
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2010 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

namespace MosaicLib.Semi.E084       //! namespace within which to define information that is based on E084.  Current rev 0704
{
    using System;
    using System.Runtime.Serialization;

    //-------------------------------------------------------------------
    public enum PIOSelect : byte
    {
        OHT = 0,
        AGV = 1,
    }

	//-------------------------------------------------------------------
	#region Pin Bitmask constant/enum definitions

	// define packed word values to use for each of the Active to Passive E084 pins and for each of the Passive to Active E084 pins.
	// NOTE: the lower 8 bits are defined to match the E84 standard pins while the upper 8 bits are used to support additional inputs to the E084 state machine that are not carried within the standard 25 pin connector.
	
    [System.Flags]
	public enum ActiveToPassivePinBits : uint     // cannot use System.UInt32 here?
	{
		VALID_pin14		= 0x0001,
		CS_0_pin15		= 0x0002,
		CS_1_pin16		= 0x0004,
		AM_AVBL_pin17	= 0x0008,
		TR_REQ_pin18	= 0x0010,
		BUSY_pin19		= 0x0020,
		COMPT_pin20		= 0x0040,
		CONT_pin21		= 0x0080,
		XferILock_sig	= 0x0800,		// Transfer (Light Curtain) Interlock - not part of E084 standard pins
        PinsBitMask     = 0x00ff,       // defines bits that are actual E084 pins - that are checked for zero to determin when PinBits are in an valid Idle state, and those that are passed to/from hardware.
	}

    [System.Flags]
	public enum PassiveToActivePinBits : uint     // cannot use System.UInt32 here?
	{
		L_REQ_pin1		= 0x0001,
		U_REQ_pin2		= 0x0002,
		VA_pin3			= 0x0004,
		READY_pin4		= 0x0008,
		VS_0_pin5		= 0x0010,
		VS_1_pin6		= 0x0020,
		HO_AVBL_pin7	= 0x0040,
		ES_pin8			= 0x0080,
        PinsBitMask     = 0x00ff,       // defines bits that are actual E084 pins - those that are passed to/from hardware.
    }

	#endregion

	//-------------------------------------------------------------------
	#region state accessor interfaces for ActiveToPassive and PassiveToActive Pin state objects
	
	///<summary>Interface used to define and access ActiveToPassivePins state.</summary>
	public interface IActiveToPassivePinsState
	{
		// ActiveToPassive pins use 0 Vdc (pin 24) and 24 Vdc (pin 23) [references provided by Passive side]

        /// <summary>Provides the low level interface/port name</summary>
        string IFaceName { get; set; }

		///<summary>Provides the represented state as a bitwise packed word.</summary>
		ActiveToPassivePinBits PackedWord { get; }
	
		///<summary>True if ActiveToPassive pins are idle.</summary>
		bool IsIdle { get; }

		///<summary>True if ActiveToPassive pins have made a valid port selection.</summary>
		bool IsSelectActive { get; }

		///<summary>VALID - used as qualifier for CS_0 and CS_1, requests passive to acknowledge availability of automated transfer</summary>
		bool VALID { get; }

		///<summary>CS_0 - select line for first port</summary>
		bool CS_0 { get; }

		///<summary>CS_1 - select line for second port (opt)</summary>
		bool CS_1 { get; }

		///<summary>AM_AVBL - only used for interbay passive OHS vehicles</summary>
		bool AM_AVBL { get; }

		///<summary>TR_REQ - request passive to engage transfer and signal when automatic transfer is ready</summary>
		bool TR_REQ { get; }

		///<summary>BUSY - inform passive that physical transfer is in process</summary>
		bool BUSY { get; }

		///<summary>COMPT - inform passive that requested transfer is complete (after BUSY cleared), hold until READY cleared</summary>
		bool COMPT { get; }

		///<summary>CONT - request passive to retain use of port for followon transfer.  Set and cleared at each BUSY transition to on</summary>
		bool CONT { get; }

		///<summary>LC_ILOCK - external input (not a normal e84 pin) indicating if the Light Curtain Interlock is in a non-tripped state (beam not broken).  State machine faults if this signal goes false while VALID is true.</summary>
		///<remarks>This signal is NOT part of the E084 standard.</remarks>
		bool XferILock { get; }
	}

    public interface IPassiveToActivePinsState
	{
		// PassiveToActivePins use SIGNAL_COM (pin25) and SIGNAL_24V (pin 22) [references provided by Active side]

        /// <summary>Provides the low level interface/port name</summary>
        string IFaceName { get; set; }

        ///<summary>Provides the represented state as a bitwise packed word.</summary>
		PassiveToActivePinBits PackedWord { get; }

        /// <summary>Returns true if PassiveToActive pins are in an idle state (ES or ES+HO)</summary>
        bool IsIdle { get; }

        /// <summary>Returns true if PassiveToActive pins are in a selectable state (ES+HO)</summary>
        bool IsSelectable { get; }

		///<summary>L_REQ - to active: set signal to indicate that port is ready to accept an automatic load transfer request, cleared to signal that physical delivery has completed.</summary>
		bool L_REQ { get; }

		///<summary>U_REQ - to active: set signal to indicate that port is ready to accept an automatic unload transfer request, cleared to signal that physical removal has completed.</summary>
		bool U_REQ { get; }

		///<summary>VA - only used for interbay passive OHS vehicles.  Valid signal for use wiht VS_0 and VS_1 select signals.</summary>
		bool VA { get; }

		///<summary>READY - to active: set signal to indicate that port is allocated for transfer and that port is ready for physical transfer to begin.  signal cleared when COMPT has been observed.</summary>
		bool READY { get; }

		///<summary>VS_0 - only used for interbay passive OHS vehicles</summary>
		bool VS_0 { get; }

		///<summary>VS_1 - only used for interbay passive OHS vehicles</summary>
		bool VS_1 { get; }

		///<summary>HO_AVBL - inform AMHS that handoff is available.  Used both within transfer session to inform active of transfer failure(s), Used outside of transfer session to block active from requesting one.</summary>
		bool HO_AVBL { get; }

		///<summary>ES (emergency stop) - Please see E084 standard for details on specific meaning of this signal.  External actors are required to halt motion immediately when this signal is not active.</summary>
		///<remarks>NOTE: This signal is active (current flowing) whne motion is permitted.</remarks>
		bool ES { get; }
	}
	
	#endregion

	//-------------------------------------------------------------------
	#region Corresponding storage and utility classes
	
	///<summary>Utility class used to implement packed and boolean property formats for Active to Passive pins.  Also supports conversion between formats.</summary>
    ///<remarks>Object is choosen to be a struct to simplify use patterns related to references and publication.</remarks>
    
    [DataContract(Namespace = Constants.E084NameSpace)]
    public struct ActiveToPassivePinsState : IActiveToPassivePinsState
	{
        public ActiveToPassivePinsState(string ifaceName, ActiveToPassivePinBits value) : this() { IFaceName = ifaceName; PackedWord = value; }
        public ActiveToPassivePinsState(IActiveToPassivePinsState rhs) : this() { IFaceName = rhs.IFaceName; PackedWord = rhs.PackedWord; }

		public override bool Equals(object rhsAsObject)
		{
			IActiveToPassivePinsState rhs = rhsAsObject as IActiveToPassivePinsState;
            return ((rhs != null) ? (rhs.PackedWord == PackedWord) : false);
		}

        public override int GetHashCode() { return base.GetHashCode(); }

        [DataMember]
        public string IFaceName { get; set; }

        [DataMember]
		public ActiveToPassivePinBits PackedWord
		{
			get 
			{
				ActiveToPassivePinBits packed 
                    = ( (VALID		? ActiveToPassivePinBits.VALID_pin14	: 0)
					    | (CS_0		? ActiveToPassivePinBits.CS_0_pin15		: 0)
					    | (CS_1		? ActiveToPassivePinBits.CS_1_pin16		: 0)
					    | (AM_AVBL	? ActiveToPassivePinBits.AM_AVBL_pin17	: 0)
					    | (TR_REQ	? ActiveToPassivePinBits.TR_REQ_pin18	: 0)
					    | (BUSY		? ActiveToPassivePinBits.BUSY_pin19		: 0)
					    | (COMPT	? ActiveToPassivePinBits.COMPT_pin20	: 0)
					    | (CONT		? ActiveToPassivePinBits.CONT_pin21		: 0)
                        | (XferILock ? ActiveToPassivePinBits.XferILock_sig  : 0)
					    );
				return packed;
			}
			
			set
			{
				VALID		= ((value & ActiveToPassivePinBits.VALID_pin14)		!= 0);
				CS_0		= ((value & ActiveToPassivePinBits.CS_0_pin15)		!= 0);
				CS_1		= ((value & ActiveToPassivePinBits.CS_1_pin16)		!= 0);
				AM_AVBL	    = ((value & ActiveToPassivePinBits.AM_AVBL_pin17)	!= 0);
				TR_REQ	    = ((value & ActiveToPassivePinBits.TR_REQ_pin18)	!= 0);
				BUSY		= ((value & ActiveToPassivePinBits.BUSY_pin19)		!= 0);
				COMPT		= ((value & ActiveToPassivePinBits.COMPT_pin20)		!= 0);
				CONT		= ((value & ActiveToPassivePinBits.CONT_pin21)		!= 0);
                XferILock    = ((value & ActiveToPassivePinBits.XferILock_sig)   != 0);
			}
		}

		public override string ToString()
		{
            return Utils.Fcns.CheckedFormat("{10}:${0:x}{1}{2}{3}{4}{5}{6}{7}{8}{9}"
                                            , (UInt32) PackedWord
                                            , (VALID ? ",VALID" : "")
                                            , (CS_0 ? ",CS_0" : "")
                                            , (CS_1 ? ",CS_1" : "")
                                            , (AM_AVBL ? ",AM_AVBL" : "")
                                            , (TR_REQ ? ",TR_REQ" : "")
                                            , (BUSY ? ",BUSY" : "")
                                            , (COMPT ? ",COMPT" : "")
                                            , (CONT ? ",CONT" : "")
                                            , (!XferILock ? ",XferILockFault" : "")
                                            , (IFaceName ?? "[null]")
                                            );
		}
	
		///<summary>True if ActiveToPassive pins are idle.</summary>
        public bool IsIdle { get { return (0 == (PackedWord & ActiveToPassivePinBits.PinsBitMask)); } }

		///<summary>True if ActiveToPassive pins have made a valid port selection.</summary>
        public bool IsSelectActive 
        { 
            get 
            {
                return (((PackedWord & ActiveToPassivePinBits.VALID_pin14) != 0)
                        && (PackedWord & (ActiveToPassivePinBits.CS_0_pin15 | ActiveToPassivePinBits.CS_1_pin16)) != 0);
            } 
        }

		///<summary>VALID - used as qualifier for CS_0 and CS_1, requests passive to acknowledge availability of automated transfer</summary>
		public bool VALID { get; set; }

		///<summary>CS_0 - select line for first port</summary>
		public bool CS_0 { get; set; }

		///<summary>CS_1 - select line for second port (opt)</summary>
		public bool CS_1 { get; set; }

		///<summary>AM_AVBL - only used for interbay passive OHS vehicles</summary>
		public bool AM_AVBL { get; set; }

		///<summary>TR_REQ - request passive to engage transfer and signal when automatic transfer is ready</summary>
		public bool TR_REQ { get; set; }

		///<summary>BUSY - inform passive that physical transfer is in process</summary>
		public bool BUSY { get; set; }

		///<summary>COMPT - inform passive that requested transfer is complete (after BUSY cleared), hold until READY cleared</summary>
		public bool COMPT { get; set; }

		///<summary>CONT - request passive to retain use of port for followon transfer.  Set and cleared at each BUSY transition to on</summary>
		public bool CONT { get; set; }

        ///<summary>XferILock - external input (not an e84 pin) indicating if the Transfer (Light Curtain) Interlock is in a non-tripped state.  State machine faults if this signal goes false while VALID is true.</summary>
		///<remarks>This signal is NOT part of the E084 standard and is not physically present in any standard 25 pin E084 electical interface.</remarks>
		public bool XferILock { get; set; }
    }

	///<summary>Utility class used to implement packed and boolean property formats for Passive to Active pins.  Also supports conversion between formats.</summary>
    [DataContract(Namespace = Constants.E084NameSpace)]
    public struct PassiveToActivePinsState : IPassiveToActivePinsState
	{
        public PassiveToActivePinsState(string ifaceName, PassiveToActivePinBits value) : this() { IFaceName = IFaceName; PackedWord = value; }
        public PassiveToActivePinsState(IPassiveToActivePinsState rhs) : this() { IFaceName = rhs.IFaceName; PackedWord = rhs.PackedWord; }

		public override bool Equals(object rhsAsObject)
		{
			IPassiveToActivePinsState rhs = rhsAsObject as IPassiveToActivePinsState;
            return ((rhs != null) ? (rhs.PackedWord == PackedWord) : false);
		}

        public override int GetHashCode() { return base.GetHashCode(); }

        [DataMember]
        public string IFaceName { get; set; }

        [DataMember]
		public PassiveToActivePinBits PackedWord
		{
			get 
			{
				PassiveToActivePinBits packed 
                    = ( (L_REQ		? PassiveToActivePinBits.L_REQ_pin1		: 0)
					    | (U_REQ	? PassiveToActivePinBits.U_REQ_pin2		: 0)
					    | (VA		? PassiveToActivePinBits.VA_pin3		: 0)
					    | (READY	? PassiveToActivePinBits.READY_pin4	    : 0)
					    | (VS_0	    ? PassiveToActivePinBits.VS_0_pin5		: 0)
					    | (VS_1	    ? PassiveToActivePinBits.VS_1_pin6		: 0)
					    | (HO_AVBL	? PassiveToActivePinBits.HO_AVBL_pin7	: 0)
					    | (ES		? PassiveToActivePinBits.ES_pin8		: 0)
					    );
				return packed;
			}
			
			set
			{
				L_REQ		= ((value & PassiveToActivePinBits.L_REQ_pin1)	!= 0);
				U_REQ		= ((value & PassiveToActivePinBits.U_REQ_pin2)	!= 0);
				VA		    = ((value & PassiveToActivePinBits.VA_pin3)		!= 0);
				READY	    = ((value & PassiveToActivePinBits.READY_pin4)	!= 0);
				VS_0	    = ((value & PassiveToActivePinBits.VS_0_pin5)	!= 0);
				VS_1		= ((value & PassiveToActivePinBits.VS_1_pin6)	!= 0);
				HO_AVBL	    = ((value & PassiveToActivePinBits.HO_AVBL_pin7) != 0);
				ES		    = ((value & PassiveToActivePinBits.ES_pin8)		!= 0);
			}
		}

		public override string ToString()
		{
            return Utils.Fcns.CheckedFormat("{9}:${0:x}{1}{2}{3}{4}{5}{6}{7}{8}"
                                            , (UInt32) PackedWord
                                            , (L_REQ ? ",L_REQ" : "")
                                            , (U_REQ ? ",U_REQ" : "")
                                            , (VA ? ",VA" : "")
                                            , (READY ? ",READY" : "")
                                            , (VS_0 ? ",VS_0" : "")
                                            , (VS_1 ? ",VS_1" : "")
                                            , (HO_AVBL ? ",HO_AVBL" : "")
                                            , (ES ? ",ES" : "")
                                            , (IFaceName ?? "[null]")
                                            );
		}
	
        /// <summary>Returns true if PassiveToActive pins are in an idle state (ES or ES+HO)</summary>
        public bool IsIdle 
        { 
            get 
            { 
                PassiveToActivePinBits packedWord = (PackedWord & PassiveToActivePinBits.PinsBitMask); 
                if (packedWord == (PassiveToActivePinBits.ES_pin8))
                    return true;
                if (packedWord == (PassiveToActivePinBits.ES_pin8 | PassiveToActivePinBits.HO_AVBL_pin7))
                    return true;
                return false;
            }
        }

        /// <summary>Returns true if PassiveToActive pins are in a selectable state (ES+HO)</summary>
        public bool IsSelectable
        {
            get
            {
                PassiveToActivePinBits packedWord = (PackedWord & PassiveToActivePinBits.PinsBitMask);
                return (packedWord == (PassiveToActivePinBits.ES_pin8 | PassiveToActivePinBits.HO_AVBL_pin7));
            }
        }

		///<summary>L_REQ - to active: set signal to indicate that port is ready to accept an automatic load transfer request, cleared to signal that physical delivery has completed.</summary>
		public bool L_REQ { get; set; }

		///<summary>U_REQ - to active: set signal to indicate that port is ready to accept an automatic unload transfer request, cleared to signal that physical removal has completed.</summary>
        public bool U_REQ { get; set; }

		///<summary>VA - only used for interbay passive OHS vehicles.  Valid signal for use wiht VS_0 and VS_1 select signals.</summary>
        public bool VA { get; set; }

		///<summary>READY - to active: set signal to indicate that port is allocated for transfer and that port is ready for physical transfer to begin.  signal cleared when COMPT has been observed.</summary>
        public bool READY { get; set; }

		///<summary>VS_0 - only used for interbay passive OHS vehicles</summary>
        public bool VS_0 { get; set; }

		///<summary>VS_1 - only used for interbay passive OHS vehicles</summary>
        public bool VS_1 { get; set; }

		///<summary>HO_AVBL - inform AMHS that handoff is available.  Used both within transfer session to inform active of transfer failure(s), Used outside of transfer session to block active from requesting one.</summary>
        public bool HO_AVBL { get; set; }

		///<summary>ES (emergency stop) - Please see E084 standard for details on specific meaning of this signal.  External actors are required to halt motion immediately when this signal is not active.</summary>
		///<remarks>NOTE: This signal is active (current flowing) whne motion is permitted.</remarks>
        public bool ES { get; set; }
	};

	#endregion

	//-------------------------------------------------------------------
    #region E084 Timer related utility classes

    /// <summary>This class is a pseudo base class that simply serves to define a set of constant default values for used in derived classes.</summary>
    class TimersCommon
    {
        ///<summary>Minimum expected time limit value for most timer values</summary>
	    public const double MinimumStandardTimerValue = 1.0;
        ///<summary>Default time limit value for handshake type timers</summary>
	    public const double DefaultStandardTimerValue = 2.0;
        ///<summary>Default time limit value used for TP3 and TP4</summary>
	    public const double DefaultMotionTimerValue = 60.0;
        ///<summary>Maximum expected time limit value for most timer values</summary>
	    public const double MaximumStandardTimerValue = 999.0;
    }

    /// <summary>Defines the storage and use of the TA1, TA2, and TA3 timer values</summary>
    [DataContract(Namespace = Constants.E084NameSpace)]
    class ActiveTimers : TimersCommon
	{
        ///<summary>VALID on to [L_REQ or U_REQ] on</summary>
        [DataMember]
		double TA1 { get; set; }

        ///<summary>T_REQ on to READY on</summary>
        [DataMember]
        double TA2 { get; set; }

        ///<summary>COMPT on to READY off</summary>
        [DataMember]
        double TA3 { get; set; }

		public ActiveTimers()
        {
			TA1 = DefaultStandardTimerValue;
			TA2 = DefaultStandardTimerValue;
			TA3 = DefaultStandardTimerValue; 
        }
        public ActiveTimers(ActiveTimers rhs) { TA1 = rhs.TA1; TA2 = rhs.TA2; TA3 = rhs.TA3; }

		public override bool Equals(object rhsAsObject)
		{
			ActiveTimers rhs = rhsAsObject as ActiveTimers;
            return (rhs != null && rhs.TA1 == TA1 && rhs.TA2 == TA2 && TA3 == rhs.TA3);
		}

        public override int GetHashCode() { return base.GetHashCode(); }
    };

    /// <summary>Defines the storage and use of the TP1, TP2, TP3, TP4, TP5, and TP6 timer values</summary>
    [DataContract(Namespace = Constants.E084NameSpace)]
    class PassiveTimers : TimersCommon
	{
        ///<summary>L_REQ or U_REQ on to TR_REQ on</summary>
        [DataMember]
        double TP1 { get; set; }

        ///<summary>READY on to BUSY on</summary>
        [DataMember]
        double TP2 { get; set; }

        ///<summary>BUSY on to carrier delivered or carrier removed</summary>
        [DataMember]
        double TP3 { get; set; }

        ///<summary>L_REQ or U_REQ off to BUSY off</summary>
        [DataMember]
        double TP4 { get; set; }

        ///<summary>READY off to VALID off</summary>
        [DataMember]
        double TP5 { get; set; }

        ///<summary>VALID off to VALID on [CONT handoff]</summary>
        [DataMember]
        double TP6 { get; set; }

		public PassiveTimers()
        {
			TP1 = DefaultStandardTimerValue;
			TP2 = DefaultStandardTimerValue;
			TP3 = DefaultMotionTimerValue;
			TP4 = DefaultMotionTimerValue;
			TP5 = DefaultStandardTimerValue;
			TP6 = DefaultStandardTimerValue;
        }
        public PassiveTimers(PassiveTimers rhs) { TP1 = rhs.TP1; TP2 = rhs.TP2; TP3 = rhs.TP3; TP4 = rhs.TP4; TP5 = rhs.TP5; TP6 = rhs.TP6; }

		public override bool Equals(object rhsAsObject)
		{
			PassiveTimers rhs = rhsAsObject as PassiveTimers;
            return (rhs != null && rhs.TP1 == TP1 && rhs.TP2 == TP2 && rhs.TP3 == TP3 && rhs.TP4 == TP4 && rhs.TP5 == TP5 && rhs.TP6 == TP6);
		}

        public override int GetHashCode() { return base.GetHashCode(); }
	}

    /// <summary>This class defines specific internal timer values that may be used within a relevant state machine engine.</summary>
    [DataContract(Namespace = Constants.E084NameSpace)]
	class DelayTimers : TimersCommon
	{
	    const double MinimumTD0Value = 0.1;
	    const double DefaultTD0Value = 0.1;
	    const double MaximumTD0Value = 0.2;
	    const double DefaultTD1Value = 1.0;	// otherwise uses the MinimumStandardTimerValue and MaximumStandardTimerValue

        ///<summary>Active side minumum nominal delay between CS_x on and VALID on</summary>
        [DataMember]
        public double TD0 { get; set; }

        ///<summary>Active side minimum delay between VALID off and VALID on (such as for CONT handoff)</summary>
        [DataMember]
        public double TD1 { get; set; }

		public DelayTimers() { TD0 = DefaultTD0Value; TD1 = DefaultTD1Value; }
        public DelayTimers(DelayTimers rhs) { TD0 = rhs.TD0; TD1 = rhs.TD1; }

		public override bool Equals(object rhsAsObject)
		{
			DelayTimers rhs = rhsAsObject as DelayTimers;
            return (rhs != null && rhs.TD0 == TD0 && rhs.TD1 == TD1);
		}

        public override int GetHashCode() { return base.GetHashCode(); }
    }

    #endregion

    //-------------------------------------------------------------------
}
