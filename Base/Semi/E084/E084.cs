//-------------------------------------------------------------------
/*! @file E084.cs
 *  @brief This file provides common definitions that relate to the use of the Semi standard E084 interface.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2010 Mosaic Systems Inc.
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

using System;
using System.Runtime.Serialization;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.Utils;

namespace MosaicLib.Semi.E084       //! namespace within which to define information that is based on E084.  Current rev 0704
{
    //-------------------------------------------------------------------
    /// <summary>
    /// Enum is used to identify one of the two possible PIO interfaces
    /// </summary>
    [Flags]
    public enum PIOSelect : byte
    {
        /// <summary>Indicates that no PIO interface is being identified.  0</summary>
        None = 0,
        /// <summary>Indicates that the OHT PIO interface is being identified.  1</summary>
        OHT = 1,
        /// <summary>Indicates that the AGV PIO interface is being identified.  2</summary>
        AGV = 2,
        /// <summary>Indicates that both the OHT and AGV PIO interfaces are being identified.  3</summary>
        All = 3,
    }

	//-------------------------------------------------------------------
	#region Pin Bitmask constant/enum definitions

    /// <summary>
    /// Enum defines a set of flag values that may be combined to represent the state of the Active To Passive Pin Bits on an E084 interface.
    /// </summary>
    /// <remarks>
    /// NOTE: the lower 8 bits are defined to match the E84 standard pins and pin ordering while the upper 8 bits are used to support additional inputs to the 
    /// E084 state machine that are not carried within the standard 25 pin connector.
    /// </remarks>
    [System.Flags]
	public enum ActiveToPassivePinBits : uint
	{
        /// <summary>Defines the bit value when no pins are asserted</summary>
        None = 0x0000,
        /// <summary>Defines the bit value when no pins are active</summary>
        NoActivePins = 0x0000,
        /// <summary>Defines the bit for the VALID signal.<para/>pin 14, 0x0001</summary>
		VALID_pin14	= 0x0001,
        /// <summary>Defines the bit for the CS_0 signal.<para/>pin 15, 0x0002</summary>
        CS_0_pin15 = 0x0002,
        /// <summary>Defines the bit for the CS_1 signal.<para/>pin 16, 0x0004</summary>
        CS_1_pin16 = 0x0004,
        /// <summary>Defines the bit for the AM_AVBL signal.<para/>pin 17, 0x0008</summary>
        AM_AVBL_pin17 = 0x0008,
        /// <summary>Defines the bit for the TR_REQ signal.<para/>pin 18, 0x0010</summary>
        TR_REQ_pin18 = 0x0010,
        /// <summary>Defines the bit for the BUSY signal.<para/>pin 19, 0x0020</summary>
        BUSY_pin19 = 0x0020,
        /// <summary>Defines the bit for the COMPT signal.<para/>pin 20, 0x0040</summary>
        COMPT_pin20 = 0x0040,
        /// <summary>Defines the bit for the CONT signal.<para/>pin 21, 0x0080</summary>
        CONT_pin21 = 0x0080,
        /// <summary>
        /// Defines the bit used to conveigh the state of the Transfer Interlock Signal (Light Curtain) where asserted means that the transfer is permitted.  
        /// This is not a defined pin in the E84 interface standard.  It is often used externally to force the A->P ES signal to the fault state (off) when the interlock is tripped (de-asserted).
        /// <para/>pin None, 0x0800
        /// </summary>
        XferILock_sig = 0x0800,
        /// <summary>
        /// Defines the bit positions that are actual E84 pins.  This mask does not include any bits in the packed word that are not actual E84 A to P DIO pins.
        /// These are are checked for zero to determine when PinBits are in an valid Idle state, and are passed to/from underlying E84 hardware interface, often to an external optical interface.
        /// <para/>pin N/A, 0x00ff
        /// </summary>
        PinsBitMask = 0x00ff,
	}

    /// <summary>
    /// Enum defines a set of flag values that may be combined to represent the state of the Passive To Active Pin Bits on an E084 interface.
    /// </summary>
    /// <remarks>
    /// NOTE: the lower 8 bits are defined to match the E84 standard pins and pin ordering while the upper 8 bits are used to support additional inputs to the 
    /// E084 state machine that are not carried within the standard 25 pin connector.
    /// </remarks>
    [System.Flags]
	public enum PassiveToActivePinBits : uint
	{
        /// <summary>Defines the bit value when no pins are asserted</summary>
        None = 0x0000,
        /// <summary>Defines the bit value when no pins are active</summary>
        NoActivePins = 0x0000,
        /// <summary>Defines the bit for the L_REQ signal.<para/>pin 1, 0x0001</summary>
        L_REQ_pin1 = 0x0001,
        /// <summary>Defines the bit for the U_REQ signal.<para/>pin 2, 0x0002</summary>
        U_REQ_pin2 = 0x0002,
        /// <summary>Defines the bit for the VA signal.<para/>pin 3, 0x0004</summary>
        VA_pin3 = 0x0004,
        /// <summary>Defines the bit for the READY signal.<para/>pin 4, 0x0008</summary>
        READY_pin4 = 0x0008,
        /// <summary>Defines the bit for the VS_0 signal.<para/>pin 5, 0x0010</summary>
        VS_0_pin5 = 0x0010,
        /// <summary>Defines the bit for the VS_1 signal.<para/>pin 6, 0x0020</summary>
        VS_1_pin6 = 0x0020,
        /// <summary>Defines the bit for the HO_AVBL signal.<para/>pin 7, 0x0040</summary>
        HO_AVBL_pin7 = 0x0040,
        /// <summary>Defines the bit for the ES signal.<para/>pin 8, 0x0080</summary>
        ES_pin8 = 0x0080,
        /// <summary>defines bits that are actual E084 pins, and are passed to/from underlying E84 hardware interface, often to an external optical interface.<para/>pin N/A, 0x00ff</summary>
        PinsBitMask = 0x00ff,
    }

	#endregion

	//-------------------------------------------------------------------
	#region state accessor interfaces for ActiveToPassive and PassiveToActive Pin state objects
	
	///<summary>Interface used to define and access ActiveToPassivePins state.</summary>
    ///<remarks>ActiveToPassive pins use 0 Vdc (pin 24) and 24 Vdc (pin 23) [references provided by Passive side]</remarks>
	public interface IActiveToPassivePinsState : IEquatable<IActiveToPassivePinsState>
	{
        /// <summary>Provides get/set access to the low level interface/port name that is relevant for this state object.  May be set to String.Empty or to null</summary>
        string IFaceName { get; set; }

        ///<summary>Provides get/set access to the contained state as a bitwise packed word.</summary>
        ActiveToPassivePinBits PackedWord { get; }
	
		///<summary>True if ActiveToPassive pins are idle.</summary>
		bool IsIdle { get; }

		///<summary>True if ActiveToPassive pins have made a valid port selection.</summary>
		bool IsSelectActive { get; }

		///<summary>VALID - used as qualifier for CS_0 and CS_1, requests passive to acknowledge availability of automated transfer.<para/>pin 14, 0x0001</summary>
		bool VALID { get; }

        ///<summary>CS_0 - select line for first port.<para/>pin 15, 0x0002</summary>
		bool CS_0 { get; }

        ///<summary>CS_1 - select line for second port (opt).<para/>pin 16, 0x0004</summary>
		bool CS_1 { get; }

        ///<summary>AM_AVBL - only used for interbay passive OHS vehicles.<para/>pin 17, 0x0008</summary>
		bool AM_AVBL { get; }

        ///<summary>TR_REQ - request passive to engage transfer and signal when automatic transfer is ready.<para/>pin 18, 0x0010</summary>
		bool TR_REQ { get; }

        ///<summary>BUSY - inform passive that physical transfer is in process.<para/>pin 19, 0x0020</summary>
		bool BUSY { get; }

        ///<summary>COMPT - inform passive that requested transfer is complete (after BUSY cleared), hold until READY cleared.<para/>pin 20, 0x0040</summary>
		bool COMPT { get; }

        ///<summary>CONT - request passive to retain use of port for followon transfer.  Set and cleared at each BUSY transition to on.<para/>pin 21, 0x0080</summary>
		bool CONT { get; }

		///<summary>
        ///(aka LC_ILOCK) - external input (not a normal e84 pin) indicating if the Light Curtain Interlock is in a non-tripped state (beam not broken).  
        ///State machine faults if this signal goes false while VALID is true.
        ///<para/>pin none, 0x0800
        ///</summary>
		///<remarks>This signal is NOT part of the E084 standard and is not included in the 25 pin connector.</remarks>
		bool XferILock { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IActiveToPassivePinsState rhs);
    }

    ///<summary>Interface used to define and access PassiveToActivePins state.</summary>
    ///<remarks>PassiveToActivePins use SIGNAL_COM (pin25) and SIGNAL_24V (pin 22) [references provided by Active side]</remarks>
    public interface IPassiveToActivePinsState : IEquatable<IPassiveToActivePinsState>
	{
        /// <summary>Provides get/set access to the low level interface/port name that is relevant for this state object.  May be set to String.Empty or to null</summary>
        string IFaceName { get; set; }

        ///<summary>Provides get/set access to the contained state as a bitwise packed word.</summary>
		PassiveToActivePinBits PackedWord { get; }

        /// <summary>Returns true if PassiveToActive pins are in an idle state (ES or ES+HO)</summary>
        bool IsIdle { get; }

        /// <summary>Returns true if PassiveToActive pins are in a selectable state (ES+HO)</summary>
        bool IsSelectable { get; }

        ///<summary>L_REQ - to active: set signal to indicate that port is ready to accept an automatic load transfer request, cleared to signal that physical delivery has completed.<para/>pin 1, 0x0001</summary>
		bool L_REQ { get; }

        ///<summary>U_REQ - to active: set signal to indicate that port is ready to accept an automatic unload transfer request, cleared to signal that physical removal has completed.<para/>pin 2, 0x0002</summary>
		bool U_REQ { get; }

        ///<summary>VA - only used for interbay passive OHS vehicles.  Valid signal for use with VS_0 and VS_1 select signals.<para/>pin 3, 0x0004</summary>
		bool VA { get; }

        ///<summary>READY - to active: set signal to indicate that port is allocated for transfer and that port is ready for physical transfer to begin.  signal cleared when COMPT has been observed.<para/>pin 4, 0x0008</summary>
		bool READY { get; }

        ///<summary>VS_0 - only used for interbay passive OHS vehicles.<para/>pin 5, 0x0010</summary>
		bool VS_0 { get; }

        ///<summary>VS_1 - only used for interbay passive OHS vehicles.<para/>pin 6, 0x0020</summary>
		bool VS_1 { get; }

        ///<summary>HO_AVBL - inform AMHS that handoff is available.  Used both within transfer session to inform active of transfer failure(s), Used outside of transfer session to block active from requesting one.<para/>pin 7, 0x0040</summary>
		bool HO_AVBL { get; }

        ///<summary>ES (emergency stop) - Please see E084 standard for details on specific meaning of this signal.  External actors are required to halt motion immediately when this signal is not active.<para/>pin 8, 0x0080</summary>
		///<remarks>NOTE: This signal is active (current flowing) when motion is permitted.</remarks>
		bool ES { get; }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IPassiveToActivePinsState rhs);
    }
	
	#endregion

	//-------------------------------------------------------------------
	#region Corresponding storage and utility classes

    ///<summary>Utility class used to implement packed and boolean property formats for IActiveToPassivePinsState.  Also supports conversion between formats.</summary>
    ///<remarks>Object is choosen to be a struct to simplify use patterns related to references and publication.</remarks>
    [DataContract(Namespace = Constants.E084NameSpace)]
    public struct ActiveToPassivePinsState : IActiveToPassivePinsState
	{
        /// <summary>Object based polymorphic copy constructor</summary>
        public ActiveToPassivePinsState(object o)
            : this(new ValueContainer(o))
        { }

        /// <summary>Object based polymorphic copy constructor</summary>
        public ActiveToPassivePinsState(ValueContainer vc) 
            : this()
        {
            if (vc.IsObject && vc.ValueAsObject is IActiveToPassivePinsState)
            {
                SetFrom(vc.ValueAsObject as IActiveToPassivePinsState);
            }
            else
            {
                IFaceName = "From:{0}".CheckedFormat(vc);
                PackedWord = vc.GetValue<ActiveToPassivePinBits>(false);
            }
        }

        /// <summary>Copy constructor</summary>
        public ActiveToPassivePinsState(IActiveToPassivePinsState rhs) 
            : this() 
        {
            SetFrom(rhs);
        }

        /// <summary>Helper method for use in copy constructors</summary>
        private void SetFrom(IActiveToPassivePinsState rhs)
        {
            if (rhs != null)
            {
                IFaceName = rhs.IFaceName;
                PackedWord = rhs.PackedWord;
            }
            else
            {
                IFaceName = "SetFromNull";
                PackedWord = ActiveToPassivePinBits.NoActivePins;
            }
        }

        /// <summary>
        /// Returns true if this object has the same contents as the given other
        /// </summary>
        public bool Equals(IActiveToPassivePinsState other)
        {
            return (other != null && IFaceName == other.IFaceName && PackedWord == other.PackedWord);
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IActiveToPassivePinsState rhs)
        {
            return Equals(rhs);
        }

        /// <summary>
        /// Compares this object against the rhs to determine if they are both of the same type and, if so, if they have the same contents.
        /// </summary>
        /// <param name="rhsAsObject">defines the object against which this one will be compared.</param>
        /// <returns>true if both objects contain the same values, false if rhsAsObject is null, is not of this type or has different contents</returns>
        public override bool Equals(object rhsAsObject)
		{
            return Equals(rhsAsObject as IActiveToPassivePinsState);
		}

        /// <summary>
        /// Override for GetHashCode so as to prevent warning due to overriden Equal method.
        /// </summary>
        /// <returns>base.GetHashCode();</returns>
        public override int GetHashCode() { return base.GetHashCode(); }

        /// <summary>Provides get/set access to the low level interface/port name that is relevant for this state object.  May be set to String.Empty or to null</summary>
        [DataMember]
        public string IFaceName { get; set; }

        ///<summary>Provides get/set access to the contained state as a bitwise packed word.</summary>
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

        /// <summary>Returns a print/log suitable string version of the state of this A2P pins state object</summary>
        public override string ToString()
		{
            return Utils.Fcns.CheckedFormat("{10}:${0:x2}{1}{2}{3}{4}{5}{6}{7}{8}{9}"
                                            , (UInt32) PackedWord
                                            , (VALID ? ",VALID" : "")
                                            , (CS_0 ? ",CS_0" : "")
                                            , (CS_1 ? ",CS_1" : "")
                                            , (AM_AVBL ? ",AM_AVBL" : "")
                                            , (TR_REQ ? ",TR_REQ" : "")
                                            , (BUSY ? ",BUSY" : "")
                                            , (COMPT ? ",COMPT" : "")
                                            , (CONT ? ",CONT" : "")
                                            , (!XferILock ? "" : ",XferILockOk")
                                            , (IFaceName ?? "[none]")
                                            );
		}
	
		///<summary>True if ActiveToPassive pins are idle.</summary>
        public bool IsIdle { get { return (0 == (PackedWord & ActiveToPassivePinBits.PinsBitMask)); } }

		///<summary>True if ActiveToPassive pins have made a valid port selection (VALID and CS_0 or CS_1).</summary>
        public bool IsSelectActive 
        { 
            get 
            {
                return (((PackedWord & ActiveToPassivePinBits.VALID_pin14) != 0)
                        && (PackedWord & (ActiveToPassivePinBits.CS_0_pin15 | ActiveToPassivePinBits.CS_1_pin16)) != 0);
            } 
        }

        ///<summary>VALID - used as qualifier for CS_0 and CS_1, requests passive to acknowledge availability of automated transfer.<para/>pin 14, 0x0001</summary>
        public bool VALID { get; set; }

        ///<summary>CS_0 - select line for first port.<para/>pin 15, 0x0002</summary>
        public bool CS_0 { get; set; }

        ///<summary>CS_1 - select line for second port (opt).<para/>pin 16, 0x0004</summary>
        public bool CS_1 { get; set; }

        ///<summary>AM_AVBL - only used for interbay passive OHS vehicles.<para/>pin 17, 0x0008</summary>
        public bool AM_AVBL { get; set; }

        ///<summary>TR_REQ - request passive to engage transfer and signal when automatic transfer is ready.<para/>pin 18, 0x0010</summary>
        public bool TR_REQ { get; set; }

        ///<summary>BUSY - inform passive that physical transfer is in process.<para/>pin 19, 0x0020</summary>
        public bool BUSY { get; set; }

        ///<summary>COMPT - inform passive that requested transfer is complete (after BUSY cleared), hold until READY cleared.<para/>pin 20, 0x0040</summary>
        public bool COMPT { get; set; }

        ///<summary>CONT - request passive to retain use of port for followon transfer.  Set and cleared at each BUSY transition to on.<para/>pin 21, 0x0080</summary>
        public bool CONT { get; set; }

        ///<summary>
        ///(aka LC_ILOCK) - external input (not a normal e84 pin) indicating if the Light Curtain Interlock is in a non-tripped state (beam not broken).  
        ///State machine faults if this signal goes false while VALID is true.
        ///<para/>pin none, 0x0800
        ///</summary>
        ///<remarks>This signal is NOT part of the E084 standard and is not included in the 25 pin connector.</remarks>
        public bool XferILock { get; set; }
    }

    ///<summary>Utility class used to implement packed and boolean property formats for IPassiveToActivePinsState.  Also supports conversion between formats.</summary>
    ///<remarks>Object is choosen to be a struct to simplify use patterns related to references and publication.</remarks>
    [DataContract(Namespace = Constants.E084NameSpace)]
    public struct PassiveToActivePinsState : IPassiveToActivePinsState
	{
        /// <summary>Object based polymorphic copy constructor</summary>
        public PassiveToActivePinsState(object o)
            : this(new ValueContainer(o))
        { }
        /// <summary>Object based polymorphic copy constructor</summary>
        public PassiveToActivePinsState(ValueContainer vc) 
            : this()
        {
            if (vc.IsObject && vc.ValueAsObject is IPassiveToActivePinsState)
            {
                SetFrom(vc.ValueAsObject as IPassiveToActivePinsState);
            }
            else
            {
                IFaceName = "From:{0}".CheckedFormat(vc);
                PackedWord = vc.GetValue<PassiveToActivePinBits>(false);
            }
        }

        /// <summary>Copy constructor</summary>
        public PassiveToActivePinsState(IPassiveToActivePinsState rhs) 
            : this() 
        {
            SetFrom(rhs);
        }

        /// <summary>Helper method for use in copy constructors</summary>
        private void SetFrom(IPassiveToActivePinsState rhs)
        {
            if (rhs != null)
            {
                IFaceName = rhs.IFaceName;
                PackedWord = rhs.PackedWord;
            }
            else
            {
                IFaceName = "SetFromNull";
                PackedWord = PassiveToActivePinBits.NoActivePins;
            }
        }

        /// <summary>
        /// Returns true if this object has the same contents as the given other
        /// </summary>
        public bool Equals(IPassiveToActivePinsState other)
        {
            return (other != null && IFaceName == other.IFaceName && PackedWord == other.PackedWord);
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IPassiveToActivePinsState rhs)
        {
            return Equals(rhs);
        }

        /// <summary>
        /// Compares this object against the rhs to determine if they are both of the same type and, if so, if they have the same contents.
        /// </summary>
        /// <param name="rhsAsObject">defines the object against which this one will be compared.</param>
        /// <returns>true if both objects contain the same values, false if rhsAsObject is null, is not of this type or has different contents</returns>
        public override bool Equals(object rhsAsObject)
		{
            return Equals(rhsAsObject as IPassiveToActivePinsState);
		}

        /// <summary>
        /// Override for GetHashCode so as to prevent warning due to overriden Equal method.
        /// </summary>
        /// <returns>base.GetHashCode();</returns>
        public override int GetHashCode() { return base.GetHashCode(); }

        /// <summary>Provides get/set access to the low level interface/port name that is relevant for this state object.  May be set to String.Empty or to null</summary>
        [DataMember]
        public string IFaceName { get; set; }

        ///<summary>Provides get/set access to the contained state as a bitwise packed word.</summary>
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

        /// <summary>Returns a print/log suitable string version of the state of this P2A pins state object</summary>
		public override string ToString()
		{
            return Utils.Fcns.CheckedFormat("{9}:${0:x2}{1}{2}{3}{4}{5}{6}{7}{8}"
                                            , (UInt32) PackedWord
                                            , (L_REQ ? ",L_REQ" : "")
                                            , (U_REQ ? ",U_REQ" : "")
                                            , (VA ? ",VA" : "")
                                            , (READY ? ",READY" : "")
                                            , (VS_0 ? ",VS_0" : "")
                                            , (VS_1 ? ",VS_1" : "")
                                            , (HO_AVBL ? ",HO_AVBL" : "")
                                            , (ES ? ",ES" : "")
                                            , (IFaceName ?? "[none]")
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

        ///<summary>L_REQ - to active: set signal to indicate that port is ready to accept an automatic load transfer request, cleared to signal that physical delivery has completed.<para/>pin 1, 0x0001</summary>
        public bool L_REQ { get; set; }

        ///<summary>U_REQ - to active: set signal to indicate that port is ready to accept an automatic unload transfer request, cleared to signal that physical removal has completed.<para/>pin 2, 0x0002</summary>
        public bool U_REQ { get; set; }

        ///<summary>VA - only used for interbay passive OHS vehicles.  Valid signal for use with VS_0 and VS_1 select signals.<para/>pin 3, 0x0004</summary>
        public bool VA { get; set; }

        ///<summary>READY - to active: set signal to indicate that port is allocated for transfer and that port is ready for physical transfer to begin.  signal cleared when COMPT has been observed.<para/>pin 4, 0x0008</summary>
        public bool READY { get; set; }

        ///<summary>VS_0 - only used for interbay passive OHS vehicles.<para/>pin 5, 0x0010</summary>
        public bool VS_0 { get; set; }

        ///<summary>VS_1 - only used for interbay passive OHS vehicles.<para/>pin 6, 0x0020</summary>
        public bool VS_1 { get; set; }

        ///<summary>HO_AVBL - inform AMHS that handoff is available.  Used both within transfer session to inform active of transfer failure(s), Used outside of transfer session to block active from requesting one.<para/>pin 7, 0x0040</summary>
        public bool HO_AVBL { get; set; }

        ///<summary>ES (emergency stop) - Please see E084 standard for details on specific meaning of this signal.  External actors are required to halt motion immediately when this signal is not active.<para/>pin 8, 0x0080</summary>
        ///<remarks>NOTE: This signal is active (current flowing) when motion is permitted.</remarks>
        public bool ES { get; set; }
	};

	#endregion

	//-------------------------------------------------------------------
    #region E084 Timer related utility classes

    /// <summary>Defines the storage and use of the TA1, TA2, and TA3 timer values</summary>
    [DataContract(Namespace = Constants.E084NameSpace)]
    public class ActiveTimers
	{
        ///<summary>Default time limit value for handshake type timers TA1, TA2 and TA3 [2.0]</summary>
        public const double DefaultHandshakeTimerValue = 2.0;

        ///<summary>VALID on to [L_REQ or U_REQ] on</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TA1 { get; set; }

        ///<summary>T_REQ on to READY on</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TA2 { get; set; }

        ///<summary>COMPT on to READY off</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TA3 { get; set; }

        /// <summary>Debugging and Logging helper method</summary>
        public override string ToString()
        {
            return "TA1:{0:f1} TA2:{1:f1} TA3:{2:f1}".CheckedFormat(TA1, TA2, TA3);
        }

        /// <summary>
        /// Default Constructor:
        /// TA1 = 2.0, TA2 = 2.0, TA3 = 2.0
        /// </summary>
		public ActiveTimers()
        {
            SetFrom(null);
        }

        /// <summary>Copy constructor</summary>
        public ActiveTimers(ActiveTimers rhs) 
        {
            SetFrom(rhs);
        }

        /// <summary>Constructor and Copy constructor helper function</summary>
        public ActiveTimers SetFrom(ActiveTimers rhs)
        {
            if (rhs != null)
            {
                TA1 = rhs.TA1;
                TA2 = rhs.TA2;
                TA3 = rhs.TA3;
            }
            else
            {
                TA1 = DefaultHandshakeTimerValue;
                TA2 = DefaultHandshakeTimerValue;
                TA3 = DefaultHandshakeTimerValue;
            }

            return this;
        }

        /// <summary>
        /// Update this object's ConfigItem marked public properties from corresponingly named config keys (using the namePrefix)
        /// </summary>
        public ActiveTimers Setup(string namePrefix, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            return Setup(config: null, namePrefix: namePrefix, issueEmitter: issueEmitter, valueEmitter: valueEmitter);
        }

        /// <summary>
        /// Update this object's ConfigItem marked public properties from corresponingly named config keys (using the namePrefix)
        /// </summary>
        public ActiveTimers Setup(IConfig config, string namePrefix, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<ActiveTimers> adapter = new ConfigValueSetAdapter<ActiveTimers>(config) { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(namePrefix);

            return this;
        }

        /// <summary>
        /// Compares this object against the rhs to determine if they are both of the same type and, if so, if they have the same contents.
        /// </summary>
        /// <param name="rhsAsObject">defines the object against which this one will be compared.</param>
        /// <returns>true if both objects contain the same values, false if rhsAsObject is null, is not of this type or has different contents</returns>
        public override bool Equals(object rhsAsObject)
		{
			ActiveTimers rhs = rhsAsObject as ActiveTimers;
            return (rhs != null && rhs.TA1 == TA1 && rhs.TA2 == TA2 && TA3 == rhs.TA3);
		}

        /// <summary>
        /// Override for GetHashCode so as to prevent warning due to overriden Equal method.
        /// </summary>
        /// <returns>base.GetHashCode();</returns>
        public override int GetHashCode() { return base.GetHashCode(); }
    };

    /// <summary>Defines the storage and use of the TP1, TP2, TP3, TP4, TP5, and TP6 timer values</summary>
    [DataContract(Namespace = Constants.E084NameSpace)]
    public class PassiveTimers
    {       
        ///<summary>Minimum expected time limit value for most timer values [1.0]</summary>
        public const double MinimumHandshakeTimerValue = 1.0;
        ///<summary>Default time limit value for handshake type timers [2.0]</summary>
        public const double DefaultHandshakeTimerValue = 2.0;
        ///<summary>Default time limit value used for TP3 and TP4 [60.0]</summary>
        public const double DefaultMotionTimerValue = 60.0;
        ///<summary>Maximum expected time limit value for most timer values [999.0]</summary>
        public const double MaximumTimerValue = 999.0;

        ///<summary>L_REQ or U_REQ on to TR_REQ on</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TP1 { get; set; }

        ///<summary>READY on to BUSY on</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TP2 { get; set; }

        ///<summary>BUSY on to carrier delivered or carrier removed</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TP3 { get; set; }

        ///<summary>L_REQ or U_REQ off to BUSY off</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TP4 { get; set; }

        ///<summary>READY off to VALID off</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TP5 { get; set; }

        ///<summary>VALID off to VALID on [CONT handoff]</summary>
        ///<remarks>This timer is not normally used since continuous handoff is not supported on most OHT I300I tools</remarks>
        [DataMember]
        [ConfigItem(IsOptional=true)]
        [ValueSetItem(SilenceIssues=true)]
        public double TP6 { get; set; }

        /// <summary>Debugging and Logging helper method</summary>
        public override string ToString()
        {
            if (TP6 == 0.0)
                return "TP1:{0:f1} TP2:{1:f1} TP3:{2:f1} TP4:{3:f1} TP5:{4:f1}".CheckedFormat(TP1, TP2, TP3, TP4, TP5);
            else
                return "TP1:{0:f1} TP2:{1:f1} TP3:{2:f1} TP4:{3:f1} TP5:{4:f1} TP6:{5:f1}".CheckedFormat(TP1, TP2, TP3, TP4, TP5, TP6);
        }

        /// <summary>
        /// Default constructor:
        /// TP1 = 2.0, TP2 = 2.0, TP3 = 60.0, TP4 = 60.0, TP5 = 2.0, TP6 = 0.0
        /// </summary>
        public PassiveTimers()
        {
            SetFrom(null);
        }

        /// <summary>Copy constructor</summary>
        public PassiveTimers(PassiveTimers rhs)
        {
            SetFrom(rhs);
        }

        /// <summary>Constructor and Copy constructor helper function</summary>
        public PassiveTimers SetFrom(PassiveTimers rhs)
        {
            if (rhs != null)
            {
                TP1 = rhs.TP1;
                TP2 = rhs.TP2;
                TP3 = rhs.TP3;
                TP4 = rhs.TP4;
                TP5 = rhs.TP5;
                TP6 = rhs.TP6;
            }
            else
            {
                TP1 = DefaultHandshakeTimerValue;
                TP2 = DefaultHandshakeTimerValue;
                TP3 = DefaultMotionTimerValue;
                TP4 = DefaultMotionTimerValue;
                TP5 = DefaultHandshakeTimerValue;
                TP6 = 0.0;
            }

            return this;
        }

        /// <summary>
        /// Update this object's ConfigItem marked public properties from corresponingly named config keys (using the namePrefix)
        /// </summary>
        public PassiveTimers Setup(string namePrefix, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<PassiveTimers> adapter = new ConfigValueSetAdapter<PassiveTimers>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(namePrefix);

            return this;
        }

        /// <summary>
        /// Compares this object against the rhs to determine if they are both of the same type and, if so, if they have the same contents.
        /// </summary>
        /// <param name="rhsAsObject">defines the object against which this one will be compared.</param>
        /// <returns>true if both objects contain the same values, false if rhsAsObject is null, is not of this type or has different contents</returns>
        public override bool Equals(object rhsAsObject)
		{
			PassiveTimers rhs = rhsAsObject as PassiveTimers;
            return (rhs != null && rhs.TP1 == TP1 && rhs.TP2 == TP2 && rhs.TP3 == TP3 && rhs.TP4 == TP4 && rhs.TP5 == TP5 && rhs.TP6 == TP6);
		}

        /// <summary>
        /// Override for GetHashCode so as to prevent warning due to overriden Equal method.
        /// </summary>
        /// <returns>base.GetHashCode();</returns>
        public override int GetHashCode() { return base.GetHashCode(); }
	}

    /// <summary>This class defines specific internal timer values that may be used within a relevant state machine engine.</summary>
    [DataContract(Namespace = Constants.E084NameSpace)]
	public class DelayTimers
	{
        /// <summary>Defines the minimum value that TD0 may normally be set to [0.1]</summary>
	    public const double MinimumTD0Value = 0.1;
        /// <summary>Defines the default value used for TD0 [0.1]</summary>
        public const double DefaultTD0Value = 0.1;
        /// <summary>Defines the maximum value that TD0 may normally be set to [0.2]</summary>
        public const double MaximumTD0Value = 0.2;
        /// <summary>Defines the default value used for TD1 [1.0]</summary>
        public const double DefaultTD1Value = 1.0;	// otherwise uses the MinimumStandardTimerValue and MaximumStandardTimerValue

        ///<summary>Active side minumum nominal delay between CS_x on and VALID on</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TD0 { get; set; }

        ///<summary>Active side minimum delay between VALID off and VALID on (such as for CONT handoff)</summary>
        [DataMember]
        [ConfigItem]
        [ValueSetItem]
        public double TD1 { get; set; }

        /// <summary>Debugging and Logging helper method</summary>
        public override string ToString()
        {
            return "TD0:{0:f1} TD1:{1:f1}".CheckedFormat(TD0, TD1);
        }

        /// <summary>
        /// Default constructor:
        /// TD0 = 0.1, TD1 = 1.0
        /// </summary>
		public DelayTimers() 
        {
            SetFrom(null);
        }

        /// <summary>Copy constructor</summary>
        public DelayTimers(DelayTimers rhs) 
        {
            SetFrom(rhs);
        }

        /// <summary>Constructor and Copy constructor helper function</summary>
        public DelayTimers SetFrom(DelayTimers rhs)
        {
            if (rhs != null)
            {
                TD0 = rhs.TD0;
                TD1 = rhs.TD1;
            }
            else
            {
                TD0 = DefaultTD0Value;
                TD1 = DefaultTD1Value;
            }

            return this;
        }

        /// <summary>
        /// Update this object's ConfigItem marked public properties from corresponingly named config keys (using the namePrefix)
        /// </summary>
        public DelayTimers Setup(string namePrefix, Logging.IMesgEmitter issueEmitter, Logging.IMesgEmitter valueEmitter)
        {
            ConfigValueSetAdapter<DelayTimers> adapter = new ConfigValueSetAdapter<DelayTimers>() { ValueSet = this, SetupIssueEmitter = issueEmitter, UpdateIssueEmitter = issueEmitter, ValueNoteEmitter = valueEmitter }.Setup(namePrefix);

            return this;
        }

        /// <summary>
        /// Compares this object against the rhs to determine if they are both of the same type and, if so, if they have the same contents.
        /// </summary>
        /// <param name="rhsAsObject">defines the object against which this one will be compared.</param>
        /// <returns>true if both objects contain the same values, false if rhsAsObject is null, is not of this type or has different contents</returns>
		public override bool Equals(object rhsAsObject)
		{
			DelayTimers rhs = rhsAsObject as DelayTimers;
            return (rhs != null && rhs.TD0 == TD0 && rhs.TD1 == TD1);
		}

        /// <summary>
        /// Override for GetHashCode so as to prevent warning due to overriden Equal method.
        /// </summary>
        /// <returns>base.GetHashCode();</returns>
        public override int GetHashCode() { return base.GetHashCode(); }
    }

    #endregion

    //-------------------------------------------------------------------
}
