//-------------------------------------------------------------------
/*! @file Modbus.cs
 * @brief This file defines Modbus helper definitiions and classes that are common to all Modbus Clients and Servers
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2010 Mosaic Systems Inc.  All rights reserved (prior C++ library version)
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
//-------------------------------------------------------------------

namespace MosaicLib.SerialIO.Modbus
{
	//-----------------------------------------------------------------

	using System;
	using System.Collections.Generic;

	using System.Net;
	using System.Net.Sockets;

	using MosaicLib.Utils;
	using MosaicLib.Time;
	using MosaicLib.Modular.Action;
	using MosaicLib.Modular.Part;

	//--------------------------------------------------------------------------

    ///<summary>Defines an Application Data Unit framing type for an individual Modbus PDU operation.</summary>
	public enum ADUType
	{
        /// <summary>None - default value</summary>
		None = 0,
        /// <summary>Serial (232/485) binary version</summary>
        RTU,
        /// <summary>ModbusTCP/ModbusUDP version.</summary>
        MBAP,
	}

    //--------------------------------------------------------------------------

    ///<summary>defines Modbus Function Code values that this software is aware of.	</summary>
	///<remarks>
	///	Please note that the literal names do not exactly match the Modbus-IDA.org specified names in all cases
	///	(modified use of plural vs singular for Register(s) and Coil(s) in some cases).
	///	Also added the term "Holding" in some cases.
	///	
	///	For reference:
	///	Coil = Discrete Output (digital) (read/write)
	///	Discrete = Discrete Input (digital) (read only)
	///	HoldingRegister = Analog Output (int16) (read/write)
	///	InputRegister = Analog  Input (int16) (read only)
	///</remarks>
	public enum FunctionCode : byte
	{
        /// <summary>Function code 0: placeholder and default value.</summary>
		FC00_None = 0x00,
        /// <summary>Function code 1: ReadCoils</summary>
        /// <remarks>w1 = 1st coil idx, w2 = nCoils (0..2000), rqNData = 0, rspNData = fc + bc + (nCoils + 7 >> 3)</remarks>
        FC01_ReadCoils = 0x01,
        /// <summary>Function code 2: ReadDiscretes</summary>
        /// <remarks>w1 = 1st discr idx, w2 = nDisc (0..2000), rqNData = 0, rspNData = fc + bc + (nDisc + 7 >> 3)</remarks>
        FC02_ReadDiscretes = 0x02,
        /// <summary>Function code 3: ReadHoldingRegisgters</summary>
        /// <remarks>w1 = 1st holding reg idx, w2 = nRegs (0..125), rqNData = 0, rspNData = fc + bc + (nRegs * 2)</remarks>
        FC03_ReadHoldingRegisters = 0x03,
        /// <summary>Function code 4: ReadInputRegisters</summary>
        /// <remarks>w1 = 1st input reg idx, w2 = nRegs (0..125), rqNData = 0, rspNData = fc + bc + (nRegs * 2)</remarks>
        FC04_ReadInputRegisters = 0x04,
        /// <summary>Function code 5: WriteSingleCoil</summary>
        /// <remarks>w1 = coil idx, w2 = (0x0000 or 0xff00), rqNData = 0, rspNData = fc + 2 + 2</remarks>
        FC05_WriteSingleCoil = 0x05,
        /// <summary>Function code 6: WriteSingleHoldingRegister</summary>
        /// <remarks>w1 = reg idx, w2 = reg value, rqNData = 0, rspNData = fc + 2 + 2</remarks>
        FC06_WriteSingleHoldingRegister = 0x06,
        /// <summary>Function Code 8: Diagnostics: Serial Line Only</summary>
        /// <remarks>w1 = sub-code, rqNData = n*2 (sub-code dependent), rspNData = fc + 2 + m*2</remarks>
        FC08_Diagnostics = 0x08,
        /// <summary>Function code 15: WriteMultipleCoils</summary>
        /// <remarks>w1 = 1st reg idx, w2 = nRegs (0..1968), </remarks>
        FC0f_WriteMultipleCoils = 0x0f,
        /// <summary>Function code 16: WriteMultipleHoldingRegisters</summary>
        /// <remarks>w1 = 1st reg idx, w2 = nRegs (0..123), reqNData = 2 * nRegs, rspNData = fc + 2 + 2</remarks>
        FC10_WriteMutlipleHoldingRegisters = 0x10,
        /// <summary>Function code 22: Mask Write Register</summary>
        /// <remarks>w1 = regIdx, w2 = and mask, reqNData = 2 (or mask), rspNData = fc + 2 + 2 + 2</remarks>
        FC16_MaskWriteRegister = 0x16,
        /// <summary>Function code 23: ReadWriteMultipleRegisters</summary>
        /// <remarks>w1 = 1st read reg idx, w2 = nRdRegs (0..123), w3 = 1st wr reg idx, w4 = nWrRegs, reqNData = 2*nWrRegs, rspNData = fc + 1 + 2*nRdRegs</remarks>
        FC17_ReadWriteMultipleRegisters = 0x17,
        /// <summary>Function code 43: EncapsulatedInteraceTransport</summary>
        /// <remarks>b1 = MEI type, b2..bn are mei specific payload data, rspNData = fc + mei + rspNData</remarks>
        FC2b_EncapsulatedInterfaceTransport = 0x2b,
    }

    /// <summary>
    /// Enum defines supported FC2b MEI (Modbus Encapsulated Interface) codes.  
    /// At present the FC2b_EncapsulatedInterfaceTransport command and this enum are not supported by this code.
    /// </summary>
    public enum MEICodes
    {
        /// <remarks>structure TBD (not implemented at this point)</remarks>
        MEI0e_ReadDeviceIdentification = 0x0e,
    }

    /// <summary>
    /// Enum defines supported FC08 Diagnostic codes.
    /// </summary>
    public enum DiagnosticSubCodes
    {
        /// <summary>ReturnQueryData</summary>
        ReturnQueryData = 0x00,
        /// <summary>RestartCommunicationsOption</summary>
        RestartCommunicationsOption = 0x01,
        /// <summary>ReturnDiagnosticRegister</summary>
        ReturnDiagnosticRegister = 0x02,
        /// <summary>ChangeASCIIInputDelimiter</summary>
        ChangeASCIIInputDelimiter = 0x03,
        /// <summary>ForceListenOnlyMode</summary>
        ForceListenOnlyMode = 0x04,
        // 0x05 .. 0x09 Reserved
        /// <summary>ClearCountersAndDiagnosticRegister</summary>
        ClearCountersAndDiagnosticRegister = 0x0a,
        /// <summary>ReturnBusMessageCount</summary>
        ReturnBusMessageCount = 0x0b,
        /// <summary>ReturnBusCommunicationErrorCount</summary>
        ReturnBusCommunicationErrorCount = 0x0c,
        /// <summary>ReturnBusExceptionErrorCount</summary>
        ReturnBusExceptionErrorCount = 0x0d,
        /// <summary>ReturnServerMessageCount</summary>
        ReturnServerMessageCount = 0x0e,
        /// <summary>ReturnServerNoResponseCount</summary>
        ReturnServerNoResponseCount = 0x0f,
        /// <summary>ReturnServerNAKCount</summary>
        ReturnServerNAKCount = 0x10,
        /// <summary>ReturnServerBusyCount</summary>
        ReturnServerBusyCount = 0x11,
        /// <summary>ReturnBusCharacterOverrunCount</summary>
        ReturnBusCharacterOverrunCount = 0x12,
        // 0x13 Reserved
        /// <summary>ClearOverrunCounterAndFlag</summary>
        ClearOverrunCounterAndFlag = 0x14,
        // 0x15..0xffff Reserved
    }

	///<summary>Defines the well known exception codes that can be produced by Modbus.  Custom value is reserved for locally generated errors.</summary>
	public enum ExceptionCode : int
	{
        /// <summary>No Error: Placeholder and Default Value</summary>
		None = 0x00,
        /// <summary>The given function code or sub-code was not accepted</summary>
        IllegalFunction = 0x01,
        /// <summary>The given data address is not accessable on this device under the corresponding function code</summary>
        IllegalDataAddress = 0x02,
        /// <summary>The given data value is not valid</summary>
        IllegalDataValue = 0x03,
        /// <summary>Slave Device Failure</summary>
        SlaveDeviceFailure = 0x04,
        /// <summary>Unknown Acknowledge error</summary>
        Acknowledge = 0x05,
        /// <summary>Slave Device is already busy</summary>
        SlaveDeviceBusy = 0x06,
        /// <summary>Unknow Device Hardware (Memory Parity) Error</summary>
        MemoryParityError = 0x08,
        /// <summary>Gateway is unable to route the given request</summary>
        GatewayPathUnavailable = 0x0a,
        /// <summary>Gateway did not recieve a response for the given request</summary>
        GatewayTargetDeviceFailedToRespond = 0x0b,
        /// <summary>Base code for local custom error codes - these are each larger than will fit in the byte exception code that Modbus directly supports.</summary>
        Custom = 0x101,
        /// <summary>Undefined custom code</summary>
        Undefined = 0x102,
        /// <summary>Attempt to decode a modbus packet did not succeed</summary>
        PacketDecodeFailed = 0x103,
        /// <summary>Communication failure was detected while sending or receiving modbus packet data.</summary>
        CommunicationError = 0x104,
        /// <summary>No response was received within the stated timeout time limit</summary>
        CommunciationTimeoutWithNoResponse = 0x105,
        /// <summary>An incomplete response was received at the end of the stated timeout time limit</summary>
        CommunicationTimeoutWithPartialResponse = 0x106,
        /// <summary>This exception code is returned by Modbus Server API methods to indicate that they are ignoring the request and that no reply should be sent.</summary>
        IgnoreRequest = 0xff01,
	};

    //--------------------------------------------------------------------------

    #region Common fucntion base class: FunctionBase

    /// <summary>
    /// This is the base class for the ClientFunctionBase.  
    /// This class includes the fields, properties and methods that are useful for implementing both Modbus clients and Modbus servers.
    /// </summary>
    public class FunctionBase
    {
        #region connstruction and related public fields

        /// <summary>Default constructor: creates the request and response ADU</summary>
        public FunctionBase()
        {
            requestAdu = new Details.ADU() { IsRequest = true };
            responseAdu = new Details.ADU() { IsRequest = false };
        }

        /// <summary>The storage and content builder object for the request ADU (and PDU)</summary>
        public readonly Details.ADU requestAdu;
        /// <summary>The storage and content decode object for the response ADU (and PDU)</summary>
        public readonly Details.ADU responseAdu;

        #endregion

        #region Properties used to setup and interpret a function

        /// <summary>Defines the UnitID used for outbound ModbusTCP traffic (as carried in the MBAP header).</summary>
        public byte UnitID { get { return requestAdu.UnitID; } set { requestAdu.UnitID = value; } }

        /// <summary>Defines the Device Address used for outbound Modbus RTU traffic.  This is a synonym for the UnitID property.</summary>
        public byte RTUAddr { get { return UnitID; } set { UnitID = value; } }

        /// <summary>Header Word 1: content, meaning, and validity depends on FCInfo and isRequest.  Must be set prior to calling Setup.</summary>
        public ushort HeaderWord1 { get { return requestAdu.HeaderWord1; } set { requestAdu.HeaderWord1 = value; } }
        /// <summary>Header Word 2: content, meaning, and validity depends on FCInfo and isRequest.  Must be set prior to calling Setup.</summary>
        public ushort HeaderWord2 { get { return requestAdu.HeaderWord2; } set { requestAdu.HeaderWord2 = value; } }
        /// <summary>Header Word 3: content, meaning, and validity depends on FCInfo and isRequest.  Must be set prior to calling Setup.</summary>
        public ushort HeaderWord3 { get { return requestAdu.HeaderWord3; } set { requestAdu.HeaderWord3 = value; } }
        /// <summary>Header Word 4: content, meaning, and validity depends on FCInfo and isRequest.  Must be set prior to calling Setup.</summary>
        public ushort HeaderWord4 { get { return requestAdu.HeaderWord4; } set { requestAdu.HeaderWord4 = value; } }

        /// <summary>Defines the timeout that is used when execting this function</summary>
        public TimeSpan TimeLimit { get; set; }

        /// <summary>
        /// Gets/Sets the ADUType that this function will be framed with.  
        /// <para/>
        /// Must be set prior to first use of the function as the setter clears various parts of the contained request and response, including any payload data.
        /// </summary>
        public ADUType ADUType
        {
            get { return requestAdu.ADUType; }
            set 
            { 
                requestAdu.ADUType = value;
                requestAdu.InitializePDU();
                responseAdu.ADUType = value;
                responseAdu.InitializePDU();
            }
        }

        /// <summary>Gets/Sets the FunctionCode that this function object has been setup to perform.  Must be set prior to first use of the function.</summary>
        public FunctionCode FC
        {
            get { return FCInfo.FC; }
            set { FCInfo = new Details.FCInfo(value); }
        }

        /// <summary>Gets the FCInfo for the FunctionCode that this function has been setup to execute.</summary>
        public Details.FCInfo FCInfo
        {
            get { return requestAdu.FCInfo; }
            protected set { requestAdu.FCInfo = value; }
        }

        #endregion

        #region Common methods used to Get and Set coils and discretes

        /// <summary>
        /// This method is used to read discrete data from the selected ADU and saves this data into a selected region of the given array's elements.
        /// Transfer allways starts with the first discrete value contained in the selected ADU.  
        /// The given array must be non-null, the selected portion of the array to fill must be entirely contained in the array and the
        /// length of the selected portion to full must be no larger than the number of discrete values contained in the selected ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool GetDiscretes(bool readFromResponse, bool[] discreteValueArray, int arrayStartingIdx, int numValuesToRead)
        {
            if (!FCInfo.IsDigital)
                return false;

            int numReadableBits = (readFromResponse ? responseAdu.NumItemsInResponse : requestAdu.NumWriteItemsInRequest);

            // validate that the array can be used as described
            if (discreteValueArray == null || arrayStartingIdx < 0 || numValuesToRead <= 0 || arrayStartingIdx + numValuesToRead > discreteValueArray.Length)
                return false;

            // validate that the command conatins enought bits
            if (numValuesToRead > numReadableBits)
                return false;

            int numBytes = ((numValuesToRead + 7) >> 3);
            bool success = false;

            success = (readFromResponse ? responseAdu : requestAdu).GetDataItems(0, true, 0, numBytes, byteBuf);

            int bitIdx = arrayStartingIdx;
            for (int byteIdx = 0; bitIdx < numValuesToRead && byteIdx < Details.MaximumPDUSize; byteIdx++)
            {
                for (byte bitMask = 0x01; bitMask != 0 && bitIdx < numValuesToRead; bitMask = unchecked((byte)(bitMask << 1)), bitIdx++)
                {
                    discreteValueArray[bitIdx] = ((byteBuf[byteIdx] & bitMask) != 0);
                }
            }

            return success;
        }

        private byte[] byteBuf = new byte[Details.MaximumPDUSize];

        /// <summary>
        /// This method is used to transfer coil data into the request ADU from a selected region the given array's elements.
        /// Transfer allways starts with the first coil value contained in the request ADU.  
        /// The given array must be non-null, the selected portion of the array must be entirely contained in the array and the
        /// length of the selected portion must be no larger than the number of coil values contained in the selected ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool SetDiscretes(bool[] discreteValueArray, int arrayStartingIdx, int numValuesToWrite)
        {
            return SetDiscretes(false, discreteValueArray, arrayStartingIdx, numValuesToWrite);
        }

        /// <summary>
        /// This method is used to transfer coil data into the request ADU from a selected region the given array's elements.
        /// Transfer allways starts with the first coil value contained in the request ADU.  
        /// The given array must be non-null, the selected portion of the array must be entirely contained in the array and the
        /// length of the selected portion must be no larger than the number of coil values contained in the selected ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool SetDiscretes(bool putDataInResponse, bool[] discreteValueArray, int arrayStartingIdx, int numValuesToWrite)
        {
            if (!FCInfo.IsDigital)
                return false;

            int numSettableBits = (putDataInResponse ? responseAdu.NumItemsInResponse : requestAdu.NumWriteItemsInRequest);

            // validate that the array can be used as described
            if (discreteValueArray == null || arrayStartingIdx < 0 || numValuesToWrite <= 0 || arrayStartingIdx + numValuesToWrite > discreteValueArray.Length)
                return false;

            // validate that the command conatins enought bits
            if (numValuesToWrite > numSettableBits)
                return false;

            int numBytes = ((numValuesToWrite + 7) >> 3);

            int bitIdx = 0;
            for (int byteIdx = 0; bitIdx < numValuesToWrite && byteIdx < Details.MaximumPDUSize; byteIdx++)
            {
                byteBuf[byteIdx] = 0;       // clear it so that prior bit values do not get stuck here in sequential set calls.
                for (byte bitMask = 0x01; bitMask != 0 && bitIdx < numValuesToWrite; bitMask = unchecked((byte)(bitMask << 1)), bitIdx++)
                {
                    if (discreteValueArray[bitIdx])
                        byteBuf[byteIdx] |= bitMask;
                }
            }

            return (putDataInResponse ? responseAdu : requestAdu).SetDataItems(0, true, 0, numBytes, byteBuf);
        }

        #endregion

        #region Common methods used to Get and Set input and holding registers

        /// <summary>
        /// This method is used to read register data from the selected ADU and saves this data into a selected region of the given array's elements.
        /// Transfer allways starts with the first register value contained in the selected ADU.  
        /// The given array must be non-null, the selected portion of the array must be entirely contained in the array and the
        /// length of the selected portion to full must be no larger than the number of register values contained in the selected ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool GetRegisters(bool readFromResponse, short[] regValueArray, int arrayStartingIdx, int numValuesToRead)
        {
            if (!FCInfo.IsRegister)
                return false;

            int numReadableRegs = (readFromResponse ? responseAdu.NumItemsInResponse : requestAdu.NumWriteItemsInRequest);

            // validate that the array can be used as described
            if (regValueArray == null || arrayStartingIdx < 0 || numValuesToRead <= 0 || arrayStartingIdx + numValuesToRead > regValueArray.Length)
                return false;

            // validate that the command conatins enought bits
            if (numValuesToRead > numReadableRegs)
                return false;

            return (readFromResponse ? responseAdu : requestAdu).GetDataItems(0, true, arrayStartingIdx, numValuesToRead, regValueArray);
        }

        /// <summary>
        /// This method is used to transfer holding register data into the request ADU from a selected region the given array's elements.
        /// Transfer allways starts with the first holding register value contained in the request ADU.  
        /// The given array must be non-null, the selected portion of the array must be entirely contained in the array and the
        /// length of the selected portion must be no larger than the number of holding register values contained in the selected ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool SetRegisters(short[] regValueArray, int arrayStartingIdx, int numValuesToWrite)
        {
            return SetRegisters(false, regValueArray, arrayStartingIdx, numValuesToWrite);
        }

        /// <summary>
        /// This method is used to transfer holding register data into the request ADU from a selected region the given array's elements.
        /// Transfer allways starts with the first holding register value contained in the request ADU.  
        /// The given array must be non-null, the selected portion of the array must be entirely contained in the array and the
        /// length of the selected portion must be no larger than the number of holding register values contained in the selected ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool SetRegisters(bool putDataInResponse, short[] regValueArray, int arrayStartingIdx, int numValuesToWrite)
        {
            if (!FCInfo.IsRegister)
                return false;

            int numSettableRegs = (putDataInResponse ? responseAdu.NumItemsInResponse : requestAdu.NumWriteItemsInRequest);

            // validate that the array can be used as described
            if (regValueArray == null || arrayStartingIdx < 0 || numValuesToWrite <= 0 || arrayStartingIdx + numValuesToWrite > regValueArray.Length)
                return false;

            // validate that the command conatins enought bits
            if (numValuesToWrite > numSettableRegs)
                return false;

            return (putDataInResponse ? responseAdu : requestAdu).SetDataItems(0, true, arrayStartingIdx, numValuesToWrite, regValueArray);
        }

        #endregion
    }

    #endregion


    //--------------------------------------------------------------------------

    /// <summary>
    /// This class is effectively a namespace for a set of definitions that give the Modbus specific implementation details.
    /// It includes methods used to calculate Modbus LRC and CRC-16 values as used with Ascii and RTU framed Modbus packets.
    /// </summary>
    public static partial class Details
    {
        //--------------------------------------------------------------------------
        #region Constants

        /// <summary>The default TCP (and UDP) port number used for serving ModbusTCP requests</summary>
        public const ushort ModbusTCPPort = 502;

        /// <summary>The MBAP protocol ID used for ModbusTCP/ModbusUDP</summary>
        public const ushort MBAPProtocolID_ModbusTCP = 0;

        /// <summary>Mask to determine if the function code in a response indicates the response is an exception (true means yes, false means normal response)</summary>
        public const byte ExceptionFunctionCodeMask = 0x80;

        #endregion

        //--------------------------------------------------------------------------
        #region MBAP definition

        ///<summary>Defines the fields that are found in an MBAP header (Modbus Application Header)</summary>
        public struct MBAP
        {
            /// <summary>client uses as seq num, server copies into response MBAPs</summary>
            public ushort TransactionID { get; set; }
            /// <summary>0 == Modbus, others reserved, server "copies" into response</summary>
            public ushort ProtocolID { get; set; }
            /// <summary>number of bytes following this field (PDU size + 1 for the UnitID)</summary>
            public ushort Length { get; set; }
            /// <summary>Remote slave address for use with gateways</summary>
            public byte UnitID { get; set; }
        };

        #endregion

        //--------------------------------------------------------------------------
        #region Packet Buffer and related definitions

        /// <summary>The ADU overhead when using RTU packets</summary>
        /// <remarks>1 address byte, 2 crc bytes</remarks>
        public const int ADUOverhead_RTU = 1 + 2;

        /// <summary>The ADU overhead when using MBAP</summary>
        /// <remarks>TransactionID (2), ProtocolID (2), Length (2), UnitID (1)</remarks>
        public const int ADUOverhead_MBAP = 2 + 2 + 2 + 1;

        /// <summary>Defines the Maximum RTU PDU (Protocol Data Unit) size.  This is the total number of bytes.</summary>
        /// <remarks>maximum RTU packet is 256 bytes long total</remarks>
        public const int MaximumPDUSize = 256 - ADUOverhead_RTU;

        /// <summary>Defines the smallest buffer that can accomidate a maximum sized PDU carried by MBAP.</summary>
        /// <remarks>maximum  buffer is maximum PDU size + MBAP Overhead.  Note: MBAP ADU Overhead is larger than RTU ADU Overhead</remarks>
        public const int MaximumBufferSize = MaximumPDUSize + ADUOverhead_MBAP;	// 

        ///<summary>Defines the storage object that is used to store a maximum sized Modbus command or response</summary>
        ///<remarks>This is defined as a class so that default constructor can allocate the array</remarks>
        public class PacketBuffer
        {
            /// <summary>Provides an alternate maximum legal buffer size that is ADUType specific</summary>
            public int maxValidPacketSize = 0;
            /// <summary>Gives the number of valid bytes in the bytes buffer.</summary>
            public int numBytes = 0;
            /// <summary>A fixed size buffer that can hold any valid Modbus Request or Response</summary>
            public readonly byte[] bytes = new byte[MaximumBufferSize];
            /// <summary>True if the numBytes length is valid</summary>
            public bool IsLenValid { get { return (numBytes >= 0 && bytes != null && numBytes <= bytes.Length && (maxValidPacketSize == 0 || numBytes <= maxValidPacketSize)); } }

            /// <summary>Sets the number of contained bytes to zero</summary>
            public void Clear() { numBytes = 0; }

            /// <summary>Marks the contents as invalid by setting the numBytes to be -1</summary>
            public void Invalidate() { numBytes = -1; }

            /// <summary>Used for testing purposes, returns the contained byte array, trimmed as needed, to the numBytes that are defined herein.</summary>
            public byte[] ValidBytes
            {
                get
                {
                    if (!IsLenValid)
                        return null;

                    byte[] result = bytes;
                    System.Array.Resize(ref result, numBytes);

                    return result;
                }

                set
                {
                    byte [] copyFrom = ((value != null) ? value : new byte [0]);
                    if (copyFrom.Length > bytes.Length)
                        System.Array.Resize(ref copyFrom, bytes.Length);

                    copyFrom.CopyTo(bytes, 0);
                    numBytes = copyFrom.Length;
                }
        
            }
        };

        #endregion

        //--------------------------------------------------------------------------
        #region FCInfo - Function Code docoder and helper object

        ///<summary>Gives decoded useage information about a given Modbus function code.</summary>
        public struct FCInfo
        {
            /// <summary>Gives the constructed function code</summary>
            public FunctionCode FC { get; private set; }
            /// <summary>True if the fc is used to write coils</summary>
            public bool WriteDigital { get; private set; }
            /// <summary>True if the fc is used to read coils or discretes</summary>
            public bool ReadDigital { get; private set; }
            /// <summary>True if the fc is used to write holding registers</summary>
            public bool WriteRegister { get; private set; }
            /// <summary>True if the fc is used to read holding or input registers</summary>
            public bool ReadRegister { get; private set; }
            /// <summary>
            /// True if the fc applies to a single item at the selected address.  
            /// False if the fc applies to a set of 1 or more items starting at the selected address
            /// </summary>
            public bool IsSingle { get; private set; }
            /// <summary>true if the fc reads from any source</summary>
            public bool DoesRead { get; private set; }
            /// <summary>true if the fc writes to any target</summary>
            public bool DoesWrite { get; private set; }
            /// <summary>true if the fc reads or writes digital(s) (coil or discretes)</summary>
            public bool IsDigital { get; private set; }
            /// <summary>true if the fc reads or writes register(s) (holding or input)</summary>
            public bool IsRegister { get; private set; }

            /// <summary>Gives the number of header bytes used in the request for this fc</summary>
            public ushort RequestHeaderBytes { get; private set; }
            /// <summary>Gives the number of header bytes used in a successfull response for this fc</summary>
            public ushort ResponseHeaderBytes { get; private set; }

            /// <summary>Gives the offset in the request PDU to the byte that carries the data byte count for the request.  This value will be zero if there is no data byte count field in this type of request</summary>
            public int PDUOffsetToRequestDataByteCount { get; private set; }
            /// <summary>Gives the offset in the response PDU to the byte that carries the data byte count for the response.  This value will be zero if there is no data byte count field in this type of response</summary>
            public int PDUOffsetToResponseDataByteCount { get; private set; }

            /// <summary>Non-Default constructor:  Accepts a given function code and fills in the corresponding property values in this object.</summary>
            public FCInfo(FunctionCode fc)
                : this()
            {
                this.FC = fc;
                switch (fc)
                {
                    case FunctionCode.FC01_ReadCoils: ReadDigital = true; RequestHeaderBytes = (1 + 2 + 2); ResponseHeaderBytes = (1 + 1); break;
                    case FunctionCode.FC02_ReadDiscretes: ReadDigital = true; RequestHeaderBytes = (1 + 2 + 2); ResponseHeaderBytes = (1 + 1); break;
                    case FunctionCode.FC03_ReadHoldingRegisters: ReadRegister = true; RequestHeaderBytes = (1 + 2 + 2); ResponseHeaderBytes = (1 + 1); break;
                    case FunctionCode.FC04_ReadInputRegisters: ReadRegister = true; RequestHeaderBytes = (1 + 2 + 2); ResponseHeaderBytes = (1 + 1); break;
                    case FunctionCode.FC05_WriteSingleCoil: WriteDigital = true; IsSingle = true; RequestHeaderBytes = (1 + 2 + 2); ResponseHeaderBytes = (1 + 2 + 2); break;
                    case FunctionCode.FC06_WriteSingleHoldingRegister: WriteRegister = true; IsSingle = true; RequestHeaderBytes = (1 + 2 + 2); ResponseHeaderBytes = (1 + 2 + 2); break;
                    case FunctionCode.FC0f_WriteMultipleCoils: WriteDigital = true; RequestHeaderBytes = (1 + 2 + 2 + 1); ResponseHeaderBytes = (1 + 2 + 2); break;
                    case FunctionCode.FC10_WriteMutlipleHoldingRegisters: WriteRegister = true; RequestHeaderBytes = (1 + 2 + 2 + 1); ResponseHeaderBytes = (1 + 2 + 2); break;
                    case FunctionCode.FC16_MaskWriteRegister: WriteRegister = true; IsSingle = true; RequestHeaderBytes = (1 + 2 + 2 + 2); ResponseHeaderBytes = (1 + 2 + 2 + 2); break;
                    case FunctionCode.FC17_ReadWriteMultipleRegisters: WriteRegister = true; ReadRegister = true; RequestHeaderBytes = (1 + 2 + 2 + 2 + 2 + 1); ResponseHeaderBytes = (1 + 1); break;
                }

                DoesRead = (ReadDigital || ReadRegister);
                DoesWrite = (WriteDigital || WriteRegister);
                IsDigital = (ReadDigital || WriteDigital);
                IsRegister = (ReadRegister || WriteRegister);

                // DataByteCount field is always the last field in the request or response when it is included.
                PDUOffsetToRequestDataByteCount = ((!IsSingle && DoesWrite) ? (RequestHeaderBytes - 1) : 0);
                PDUOffsetToResponseDataByteCount = ((!IsSingle && DoesRead) ? (ResponseHeaderBytes - 1) : 0);
            }

            /// <summary>
            /// Returns a printed version of the Function Code contained here.
            /// </summary>
            public override string ToString()
            {
                string rw = (DoesRead ? (DoesWrite ? " RW" : " Rd") : (DoesWrite ? " Wr" : ""));
                string xferType = (IsDigital ? " Dig" : (IsRegister ? " Reg" : ""));

                return Fcns.CheckedFormat("FC:{0}{1}", FC, rw, xferType);
            }
        }

        #endregion

        #region FC related constants

        /// <summary>Maximum number of Coils that can be written as a set</summary>
        public const int PDU_MaximumCoilsPerWrite = 1968;       // 0x7b0
        /// <summary>Maximum number of Coils or Discretes that can be read as a set.</summary>
        public const int PDU_MaximumDiscretesPerRead = 2000;    // 0x7d0
        /// <summary>Maximum number of Holding Registers that can be written as a set.</summary>
        public const int PDU_MaximumRegistersPerWrite = 123;    // 0x7b
        /// <summary>Maximum number of Holding Registers or Input Registers that can be read as a set.</summary>
        public const int PDU_MaximumRegistersPerRead = 125;     // 0x7d
        /// <summary>Maximum number of Holding Registers that can be written using the FC17_ReadWriteMultipleRegisters command.</summary>
        public const int PDU_MaximumWriteRegistersPerReadWrite = 121;  // 0x79

        /// <summary>This gives the maximum number of Coils or Discretes that can be transfered in any FC</summary>
        public const int PDU_MaximumDiscretesPerFC = 2000;      // by manual observation from the above values
        /// <summary>This gives the maximum number of Registers that can be transfered in any FC</summary>
        public const int PDU_MaximumRegistersPerFC = 125;       // by manual observation from the above values

        #endregion

        //--------------------------------------------------------------------------
        #region ADU (Application Data Unit)

        ///<summary>
        ///Storage and use object for all types of Modbus ADU (Application Data Unit).
        ///This object extends the PDU class and adds the ADUType specific header/prefix and postfix setup logic that is required in order to be able to 
        ///send or receive and decode a Modbus request or response.
        ///</summary>
        public class ADU : PDU
        {
            /// <summary>Default constructor.  Leaves all values in their constructor default states.</summary>
            public ADU()
            { }

            /// <summary>Local storage for MBAP related fields and work.</summary>
            protected MBAP mbap = new MBAP();

            /// <summary>Defines the UnitID used for outbound ModbusTCP traffic (as carried in the MBAP header).</summary>
            public byte UnitID { get { return mbap.UnitID; } set { mbap.UnitID = value; } }

            /// <summary>Defines the Device Address used for outbound Modbus RTU traffic.  This is a synonym for the UnitID property.</summary>
            public byte RTUAddr { get { return UnitID; } set { UnitID = value; } }

            /// <summary>Gives access to the TransactinID contained in this ADU's MBAP header.</summary>
            public UInt16 TransactionID { get { return mbap.TransactionID; } set { mbap.TransactionID = value; } }

            /// <summary>
            /// must be incremented atomically for safe MT use of this code
            /// </summary>
            /// <remarks>Note that this field is declared as public so that Unit test code can initialize it to specific values when needed.  Normal client code should not need to change this value directly.</remarks>
            public static Utils.AtomicInt32 mbSeqNumCounter = new AtomicInt32(1);

            /// <summary>
            /// Calls PrepareToSend on a newly generated transaction ID and returns the PrepareToSend result.
            /// </summary>
            public string PrepareToSendRequest()
            {
                // generate the next transaction ID - not necessarily consecutive per function or client
                UInt16 transactionID = ((ADUType == ADUType.MBAP) ? unchecked((UInt16)mbSeqNumCounter.IncrementSkipZero()) : (UInt16) 0);
                return PrepareToSend(transactionID);
            }

            /// <summary>
            /// Calls PrepareToSend on the TransactionID from the request and returns its value.
            /// </summary>
            public string PrepareToSendResponse(ADU requestAdu)
            {
                return PrepareToSend(requestAdu.TransactionID);
            }

            /// <summary>
            /// Final step before sending this ADU's PktBuf contents.  
            /// For RTU packets this updates the RTUAddr and calculates and appends the checksum-crc
            /// For MBAP packets this updates the mbap header and copies it into the PktBuf contents.
            /// </summary>
            /// <param name="transactionIDToUse">For MBAP packets this defines the transationID that will be placed in the header</param>
            /// <returns>Error string for unrecognized ADUType, empty string in all other cases.</returns>
            public string PrepareToSend(UInt16 transactionIDToUse)
            {
                switch (ADUType)
                {
                    case ADUType.RTU:
                        {
                            // put the initial zero rtuAddr into the packet
                            PktBuf.bytes[0] = RTUAddr;

                            // generate CRC and put into PktBuf

                            int crcByteCount = PktBuf.numBytes - 2;
                            UInt16 crc = CalcRTUCRC16(PktBuf.bytes, 0, crcByteCount);

                            byte crcMSB, crcLSB;
                            Utils.Data.Unpack(crc, out crcMSB, out crcLSB);

                            // low byte first then high byte per modbus spec - these are the last two bytes in the PktBuf
                            PktBuf.bytes[PktBuf.numBytes - 2] = crcLSB;
                            PktBuf.bytes[PktBuf.numBytes - 1] = crcMSB;
                        }
                        break;
                    case ADUType.MBAP:
                        {
                            // setup mbap length - it does not include the first 3 words of the MBAP.  It does include the UnitID.
                            mbap.Length = unchecked((ushort)(PktBuf.numBytes - 6));

                            mbap.TransactionID = transactionIDToUse;

                            // transfer the initial MBAP into PktBuf.  All of it will be over written later.
                            Utils.Data.Unpack(mbap.TransactionID, PktBuf.bytes, 0);
                            Utils.Data.Unpack(mbap.ProtocolID, PktBuf.bytes, 2);
                            Utils.Data.Unpack(mbap.Length, PktBuf.bytes, 4);
                            PktBuf.bytes[6] = UnitID;
                        }
                        break;
                    default:
                        return "Invalid ADU Type";
                }

                if (PktBuf.numBytes == 0)
                    return "Invalid Fucntion or Setup: transfer buffer is zero length";

                return "";
            }

            /// <summary>
            /// This method attempts to decode and validate the data contained in the PktBuf that is contained under this ADU as a request packet.  
            /// The expected ADUType must have already been set in this object.
            /// In addition the PDU must have already been initialized at least once for the given ADU value.
            /// Retruns true if a complete response was found or if a fatal error was found.
            /// </summary>
            /// <param name="ecStr">empty on successful decode.  Non-empty if an issue is detected that prevents valid decode of the packet data.</param>
            /// <returns>true if a complete response was found or if a fatal error was found.</returns>
            public bool AttemptToDecodeRequestPkt(out string ecStr)
            {
                ecStr = "";

                bool enoughBytesForFunctionCode = (PktBuf.numBytes >= (ADUSize + 1));
                bool enoughBytesForFCHeader = false;
                bool enoughBytesForFullRequest = false;

                if (enoughBytesForFunctionCode)
                {
                    FunctionCode fc = unchecked((FunctionCode)PktBuf.bytes[PDUStartOffset]);
                    if (FCInfo.FC != fc)
                        FCInfo = new FCInfo(fc);
                    enoughBytesForFCHeader = (PktBuf.numBytes >= (ADUSize + FCInfo.RequestHeaderBytes));
                }

                if (enoughBytesForFCHeader)
                {
                    // decode the header bytes in the PDU
                    ecStr = DecodeRequestPDUFromPacketData();
                    if (!String.IsNullOrEmpty(ecStr))
                        return true;

                    int requiredFullRequestSize = (ADUSize + PDUSize);
                    if (PktBuf.numBytes == requiredFullRequestSize)
                    {
                        enoughBytesForFullRequest = true;
                    }
                    else if (PktBuf.numBytes > requiredFullRequestSize)
                    {
                        ecStr = Fcns.CheckedFormat("AttemptToDecodeRequestPkt failed: Received length:{0} is longer than required length:{1} for fc:{2}", PktBuf.numBytes, requiredFullRequestSize, FCInfo.FC);
                        return true;
                    }
                }

                if (!enoughBytesForFullRequest)
                    return false;

                    // we have a non-zero PDUSize - perform ADU Specific content verification steps.
                switch (ADUType)
                {
                    case ADUType.RTU:
                        {
                            // extract the RTUAddr from the request
                            RTUAddr = PktBuf.bytes[0];

                            // check CR and verify response from correct RTUAddr and response carries correct fucntion code
                            int crcDataBlockSize = (ADUSize + PDUSize - 2);
                            UInt16 calcCRC = CalcRTUCRC16(PktBuf.bytes, 0, crcDataBlockSize);
                            byte inboundCrcLSB = PktBuf.bytes[crcDataBlockSize];
                            byte inboundCrcMSB = PktBuf.bytes[crcDataBlockSize + 1];
                            UInt16 inboundCRC = Utils.Data.Pack(inboundCrcMSB, inboundCrcLSB);

                            if (calcCRC != inboundCRC)
                                ecStr = "RTU CRC Error";
                        }
                        break;
                    case ADUType.MBAP:
                        {
                            // extract the MBAP from the response
                            mbap.TransactionID = Utils.Data.Pack2(PktBuf.bytes, 0);
                            mbap.ProtocolID = Utils.Data.Pack2(PktBuf.bytes, 2);
                            mbap.Length = Utils.Data.Pack2(PktBuf.bytes, 4);
                            mbap.UnitID = PktBuf.bytes[6];

                            // first two words of reply MBAP must match transmit MBAP
                            if (mbap.ProtocolID != MBAPProtocolID_ModbusTCP)
                                ecStr = "Unsupported MBAP ProtocolID";
                            else if (mbap.Length != (ADUSize + PDUSize - 6))
                                ecStr = "Unexpected MBAP Length in request";
                        }
                        break;
                }

                return true;    // we have recieved a complete response, even if it is not a valid one.
            }

            /// <summary>
            /// This method sets up a responsePDU in preparation for reception for a given requestAdu.  
            /// This involves updating the expected ADUType, FCInfo, NumItemsInResponse, and the PDUSize (using InitializePDU).
            /// </summary>
            public void PrepareToReceiveResponse(ADU requestAdu)
            {
                ADUType = requestAdu.ADUType;
                FCInfo = requestAdu.FCInfo;
                NumItemsInResponse = requestAdu.NumReadItemsInRequest;

                InitializeResponsePDU();
            }

            /// <summary>
            /// This method attempts to decode and validate the data contained in the PktBuf that is contained under this ADU as a response packet to the given request ADU.
            /// </summary>
            /// <param name="requestAdu">Gives the request ADU that matches this possible response packet.  Used to obatin the FC, ADU type, MBAP/RTU header.</param>
            /// <param name="ecStr">empty on successful decode.  Non-empty if an issue is detected that prevents valid decode of the packet data.</param>
            /// <returns>true if a complete response was found or if a fatal error was found.</returns>
            public bool AttemptToDecodeResponsePkt(ADU requestAdu, out string ecStr)
            {
                ecStr = "";

                if (ADUType != requestAdu.ADUType || FCInfo.FC != requestAdu.FCInfo.FC || NumItemsInResponse != requestAdu.NumReadItemsInRequest)
                {
                    PrepareToReceiveResponse(requestAdu);
                }

                bool enoughBytesForException = (PktBuf.numBytes >= ADUSize + 2);
                bool enoughBytesForResponse = (PktBuf.numBytes >= (ADUSize + PDUSize));

                int usePduSize = 0;

                if (enoughBytesForException && IsExceptionResponse)
                    usePduSize = 2;
                else if (enoughBytesForResponse)
                    usePduSize = PDUSize;
                else
                    return false;       // based on the request, we do not have enough bytes for a full response

                if (PDUSize == 0)
                    ecStr = "AttemptToDecodeResponsePkt: Internal Error: PDUSize zero after verifying sufficient size";

                if (String.IsNullOrEmpty(ecStr))
                {
                    // we have a non-zero PDUSize - perform ADU Specific content verification steps.
                    switch (ADUType)
                    {
                        case ADUType.RTU:
                            {
                                // check CR and verify response from correct RTUAddr and response carries correct fucntion code
                                int crcDataBlockSize = (ADUSize + usePduSize - 2);
                                UInt16 calcCRC = CalcRTUCRC16(PktBuf.bytes, 0, crcDataBlockSize);
                                byte inboundCrcLSB = PktBuf.bytes[crcDataBlockSize];
                                byte inboundCrcMSB = PktBuf.bytes[crcDataBlockSize + 1];
                                UInt16 inboundCRC = Utils.Data.Pack(inboundCrcMSB, inboundCrcLSB);

                                if (calcCRC != inboundCRC)
                                    ecStr = "RTU CRC Error";
                                else if (PktBuf.bytes[0] != requestAdu.PktBuf.bytes[0] && (requestAdu.PktBuf.bytes[0] != 255))      // the broadcast address is actually zero....
                                    ecStr = "Response from incorrect RTUAddr";
                            }
                            break;
                        case ADUType.MBAP:
                            {
                                // extract the MBAP from the response
                                mbap.TransactionID = Utils.Data.Pack2(PktBuf.bytes, 0);
                                mbap.ProtocolID = Utils.Data.Pack2(PktBuf.bytes, 2);
                                mbap.Length = Utils.Data.Pack2(PktBuf.bytes, 4);
                                mbap.UnitID = PktBuf.bytes[6];

                                // first two words of reply MBAP must match transmit MBAP
                                if (requestAdu.mbap.TransactionID != mbap.TransactionID)
                                    ecStr = "MBAP TranscationID Mismatch";
                                else if (requestAdu.mbap.ProtocolID != mbap.ProtocolID)
                                    ecStr = "MBAP ProtocolID Mismatch";
                                else if (requestAdu.mbap.UnitID != mbap.UnitID)
                                    ecStr = "Response from unexpected UnitID";
                                else if (IsResponse && (mbap.Length != (ADUSize + usePduSize - 6)))
                                    ecStr = "Unexpected MBAP Length in response";
                            }
                            break;
                    }
                }

                if (String.IsNullOrEmpty(ecStr))
                {
                    // verify that the fc matches the outgoing one even if response is an excpetion.  
                    // Also verify that we did not receive any extra bytes.
                    byte txFC = requestAdu.PktBuf.bytes[requestAdu.PDUStartOffset];
                    byte rxFC = unchecked((byte)(PktBuf.bytes[PDUStartOffset] & ~ExceptionFunctionCodeMask));

                    if (txFC != rxFC)
                        ecStr = "FunctionCode Mismatch";
                    else if (PktBuf.numBytes > (ADUSize + usePduSize))
                        ecStr = "Received Extra (unexepted) bytes";
                }

                if (String.IsNullOrEmpty(ecStr) && !IsExceptionResponse && requestAdu.FCInfo.PDUOffsetToResponseDataByteCount > 0)
                {
                    byte pduDataByteCount = 0;
                    if (PDUStartOffset + requestAdu.FCInfo.PDUOffsetToResponseDataByteCount < PktBuf.numBytes)
                        pduDataByteCount = PktBuf.bytes[PDUStartOffset + requestAdu.FCInfo.PDUOffsetToResponseDataByteCount];
                    if (requestAdu.FCInfo.ResponseHeaderBytes + pduDataByteCount != PDUSize)
                        ecStr = "Response Byte Count does not match expected size";
                }

                if (String.IsNullOrEmpty(ecStr))
                    ecStr = DecodeRxPkt();

                return true;    // we have recieved a complete response, even if it is not a valid one.
            }

            /// <summary>
            /// Virtual method used to attempt to Decode FC specific details about the received packet and the data it contains.
            /// </summary>
            /// <returns>Empty String on success</returns>
            /// <remarks>
            /// at present we depend on the ADU CRC test or the MBAP length test to confirm that the received
            /// pattern is valid (either as a response of the expected size or as an exception response
            /// </remarks>
            public virtual string DecodeRxPkt()
            {
                return "";
            }

        }

        #endregion

        //--------------------------------------------------------------------------
        #region PDU (Protocol Data Unit)

        ///<summary>
        ///Storage and use object for a Modbus PDU (Protocol Data Unit)
        ///This object generally stores the body of a Modbus request or response packet, including space reserved for the ADU prefix and/or suffix.
        ///This object provides means to setup the common parts of most ADU's and to read and write the Data portion of the underlying command data.
        ///This object also provides means to extract the exception code from response packets that indicate a failure in their function code byte.
        ///</summary>
        public class PDU
        {
            /// <summary>Debug assist method: returns a printable string verison for portions of the contents of this PDU</summary>
            public override string ToString()
            {
                string reqRspType = (IsRequest ? "Req" : (IsResponse ? "Rsp" : "NRR"));
                string rxEC = ((IsResponse && ReceivedExceptionCode != ExceptionCode.None) ? " RxEC:" + ReceivedExceptionCode.ToString() : "");
                string txEC = ((IsResponse && ExceptionCodeToSend != ExceptionCode.None) ? " TxEC:" + ExceptionCodeToSend.ToString() : "");

                return Fcns.CheckedFormat("{0} {1} {2} HW ${3:x4} ${4:x4} ${5:x4} ${6:x4} nBytes:{7}{8}{9}", 
                                            reqRspType, ADUType, FCInfo, HeaderWord1, HeaderWord2, HeaderWord3, HeaderWord4, PktBuf.numBytes,
                                            rxEC, txEC);
            }

            /// <summary>Default constructor.  Leaves all values in their constructor default states.</summary>
            public PDU()
            {
                PktBuf = new PacketBuffer();
            }

            /// <summary>True if this is a request, False if this is a response.  Must be set prior to calling Setup.</summary>
            public bool IsRequest { get; set; }
            /// <summary>Indicates which ADU type is being used here.  Determines the internal offsets into the PacketBuffer to reserve space for the ADU portions.  Must be set prior to calling Setup.</summary>
            public ADUType ADUType { get; set; }
            /// <summary>Gives the FCInfo for this packet.  Must be set prior to calling Setup.</summary>
            public FCInfo FCInfo { get; set; }
            /// <summary>Header Word 1: content, meaning, and validity depends on FCInfo and isRequest.  Must be set prior to calling Setup.</summary>
            public ushort HeaderWord1 { get; set; }
            /// <summary>Header Word 2: content, meaning, and validity depends on FCInfo and isRequest.  Must be set prior to calling Setup.</summary>
            public ushort HeaderWord2 { get; set; }
            /// <summary>Header Word 3: content, meaning, and validity depends on FCInfo and isRequest.  Must be set prior to calling Setup.</summary>
            public ushort HeaderWord3 { get; set; }
            /// <summary>Header Word 4: content, meaning, and validity depends on FCInfo and isRequest.  Must be set prior to calling Setup.</summary>
            public ushort HeaderWord4 { get; set; }

            /// <summary>True if this is a response, False if this is a request.  Is functionally equivilant to !IsRequest.</summary>
            public bool IsResponse { get { return !IsRequest; } set { IsRequest = !value; } }

            /// <summary>For requests this gives the address of the first item (coil or register) that is written by this request.</summary>
            public UInt16 FirstWriteItemAddrInRequest
            {
                get
                {
                    if (IsRequest && FCInfo.DoesWrite)
                        return ((FCInfo.FC == FunctionCode.FC17_ReadWriteMultipleRegisters) ? HeaderWord3 : HeaderWord1);
                    else
                        return 0;
                }
            }

            /// <summary>For requests this gives the number of multiple items (coils or registers) that are written by this request.  Return 0 for responses and single writes.</summary>
            public UInt16 NumWriteItemsInRequest
            {
                get 
                {
                    if (IsRequest && FCInfo.DoesWrite && !FCInfo.IsSingle)
                        return ((FCInfo.FC == FunctionCode.FC17_ReadWriteMultipleRegisters) ? HeaderWord4 : HeaderWord2);
                    else
                        return 0;
                }
            }

            /// <summary>For requests this gives the address of the first item (coil or register) that is read by this request.</summary>
            public UInt16 FirstReadItemAddrInRequest
            {
                get
                {
                    if (IsRequest && FCInfo.DoesRead)
                        return HeaderWord1;
                    else
                        return 0;
                }
            }


            /// <summary>For requests this gives the number of multiple items (discretes, coils, or registers) that are read by this request.  Returns 0 for responses and single reads.</summary>
            public UInt16 NumReadItemsInRequest
            {
                get
                {
                    if (IsRequest && FCInfo.DoesRead && !FCInfo.IsSingle)
                        return HeaderWord2;
                    else
                        return 0;
                }
            }

            /// <summary>For requests, this gives the number of non-header data bytes that will be used to transfer the coil or register data that will be written by the request.  Will only be non-zero for write multiple functions.</summary>
            public byte DataByteCountInRequest
            {
                get
                {
                    int byteCount = 0;
                    if (FCInfo.IsDigital)
                        byteCount = ((NumWriteItemsInRequest + 7) >> 3);      // round up from bits to bytes
                    else if (FCInfo.IsRegister)
                        byteCount = (NumWriteItemsInRequest * 2);

                    return unchecked((byte)Math.Max(Math.Min(byteCount, 255), 0));
                }
            }

            /// <summary>Set explicitly in responses to indicate how many items (discretes, coils, holding or input registers) will be transfered in the reponse.  Not intended for use in Requests.</summary>
            public int NumItemsInResponse { get; set; }

            /// <summary>For responses, this gives the number of non-header data bytes thar will be used to transfer the read discrete or register data.  Will only be non-zero for read multiple functions.</summary>
            public byte DataByteCountInResponse
            {
                get
                {
                    int byteCount = 0;
                    if (FCInfo.IsDigital)
                        byteCount = ((NumItemsInResponse + 7) >> 3);      // round up from bits to bytes
                    else if (FCInfo.IsRegister)
                        byteCount = (NumItemsInResponse * 2);

                    return unchecked((byte)Math.Max(Math.Min(byteCount, 255), 0));
                }
            }

            /// <summary>
            /// This method is used to fill in all of the internally decoded values based on the ADUType, FCInfo and Header Words.
            /// It updates all of the internal offsets that allow the data portion of the PDO to be safely accessed.  This method
            /// must be called before accessing the data array (coils and/or registers) for the stored packet.  This method
            /// may only be called after the FCInfo, ADUType and Header Word values are known and defined.
            /// </summary>
            public void InitializePDU()
            {
                switch (ADUType)
                {
                    case ADUType.RTU: PDUStartOffset = 1; ADUSize = ADUOverhead_RTU; PktBuf.maxValidPacketSize = 256; break;
                    case ADUType.MBAP: PDUStartOffset = ADUOverhead_MBAP; ADUSize = ADUOverhead_MBAP; PktBuf.maxValidPacketSize = 0; break;
                    default: PDUStartOffset = 0; ADUSize = 0; break;
                }

                PDUSize = 0;
            }

            /// <summary>
            /// Attempts to extract PDU specific information from the request Packet Data contained in the PktBuf.byte array contained in this object.
            /// This decoding is specific for Request packets.
            /// </summary>
            /// <returns>Empty string on success.  Non-empty string error message on failure.</returns>
            /// <remarks>
            /// At present there is no corresponding method for Decoding Response PDU's.  This is done at the ADU level as the validation portion requires access to the
            /// corresponding request ADU.
            /// </remarks>
            public string DecodeRequestPDUFromPacketData()
            {
                if (!IsRequest)
                    return "Internal: DecodeRequestPDUFromPacketData is not legal for use with a response PDU";

                PDUSize = FCInfo.RequestHeaderBytes;

                int idx = PDUStartOffset + 1;   // skip the fc byte as it has already been processed.

                if (FCInfo.RequestHeaderBytes >= 3) { HeaderWord1 = Data.Pack2(PktBuf.bytes, idx); idx += 2; }
                if (FCInfo.RequestHeaderBytes >= 5) { HeaderWord2 = Data.Pack2(PktBuf.bytes, idx); idx += 2; }
                if (FCInfo.RequestHeaderBytes >= 7) { HeaderWord3 = Data.Pack2(PktBuf.bytes, idx); idx += 2; }
                if (FCInfo.RequestHeaderBytes >= 9) { HeaderWord4 = Data.Pack2(PktBuf.bytes, idx); idx += 2; }

                byte dataByteCountInRequest = 0;

                if (FCInfo.PDUOffsetToRequestDataByteCount > 0)
                {
                    dataByteCountInRequest = PktBuf.bytes[PDUStartOffset + FCInfo.PDUOffsetToRequestDataByteCount];

                    PDUSize += dataByteCountInRequest;
                }

                PDUSize = FCInfo.RequestHeaderBytes + dataByteCountInRequest;

                if (FCInfo.IsSingle)
                { }
                else if (FCInfo.IsDigital)
                {
                    int requiredBytes = ((NumWriteItemsInRequest + 7) >> 3);
                    if (requiredBytes != dataByteCountInRequest)
                        return Fcns.CheckedFormat("Request PDU length mismatch fc:{0} numItems:{1} lenByte:{2} != {3} required bytes from items", FCInfo.FC, NumWriteItemsInRequest, dataByteCountInRequest, requiredBytes);
                }
                else if (FCInfo.IsRegister)
                {
                    int requiredBytes = (NumWriteItemsInRequest  * 2);
                    if (requiredBytes != dataByteCountInRequest)
                        return Fcns.CheckedFormat("Request PDU length mismatch fc:{0} numItems:{1} lenByte:{2} != {3} required bytes from items", FCInfo.FC, NumWriteItemsInRequest, dataByteCountInRequest, requiredBytes);
                }

                return String.Empty;
            }

            /// <summary>
            /// This method updates the RequestPDU specific fields and FC header contents for RequestPDU bodies.  The ADUType, FCInfo, and header words must be
            /// defined/updated prior to calling this method.  This method must be called before putting any vector data values (coils or registers) in the data
            /// portion of this PDU.
            /// </summary>
            public void InitializeRequestPDU(bool forSend)
            {
                InitializePDU();

                if (IsRequest)
                {
                    int idx = PDUStartOffset;

                    PDUSize = FCInfo.RequestHeaderBytes;

                    if (forSend)
                    {
                        PktBuf.bytes[idx++] = unchecked((byte)(FCInfo.FC));
                        if (FCInfo.RequestHeaderBytes >= 3) { Data.Unpack(HeaderWord1, PktBuf.bytes, idx); idx += 2; }
                        if (FCInfo.RequestHeaderBytes >= 5) { Data.Unpack(HeaderWord2, PktBuf.bytes, idx); idx += 2; }
                        if (FCInfo.RequestHeaderBytes >= 7) { Data.Unpack(HeaderWord3, PktBuf.bytes, idx); idx += 2; }
                        if (FCInfo.RequestHeaderBytes >= 9) { Data.Unpack(HeaderWord4, PktBuf.bytes, idx); idx += 2; }

                        if (FCInfo.PDUOffsetToRequestDataByteCount > 0)
                        {
                            PktBuf.bytes[PDUStartOffset + FCInfo.PDUOffsetToRequestDataByteCount] = DataByteCountInRequest;
                            PDUSize += DataByteCountInRequest;
                        }

                        PktBuf.numBytes = (ADUSize + PDUSize);
                    }
                    else
                    {
                        if (FCInfo.PDUOffsetToRequestDataByteCount > 0)
                            PDUSize += DataByteCountInRequest;
                    }
                }
            }

            /// <summary>
            /// This method Initializes this PDU and sets the PDUSize for the response based on the ExceptionCodeToSend, the FCInfo and the DataByteCountInResponse.
            /// ExceptionCodeToSend is only non-zero in Servers.
            /// </summary>
            public void InitializeResponsePDU()
            {
                InitializePDU();

                if (IsResponse)
                {
                    if (ExceptionCodeToSend != ExceptionCode.None)
                    {
                        PDUSize = 2;
                    }
                    else
                    {
                        PDUSize = FCInfo.ResponseHeaderBytes;

                        if (FCInfo.PDUOffsetToResponseDataByteCount > 0)
                            PDUSize += DataByteCountInResponse;
                    }

                    if (PktBuf.numBytes == 0)
                        PktBuf.numBytes = (ADUSize + PDUSize);
                }
            }

            /// <summary>
            /// This method invokes InitializeResponsePDU and then if this response is not an excpetion, this method also fills in the PDU header.
            /// </summary>
            public void InitializeResponsePDUForSend()
            {
                InitializeResponsePDU();

                if (IsResponse)
                {
                    int idx = PDUStartOffset;

                    if (ExceptionCodeToSend != ExceptionCode.None)
                    {
                        PDUSize = 2;

                        PktBuf.bytes[idx++] = unchecked((byte)((byte)FCInfo.FC | ExceptionFunctionCodeMask));
                        PktBuf.bytes[idx++] = unchecked((byte)ExceptionCodeToSend);

                        PktBuf.numBytes = (ADUSize + PDUSize);
                    }
                    else
                    {
                        PDUSize = FCInfo.ResponseHeaderBytes;

                        PktBuf.bytes[idx++] = unchecked((byte)(FCInfo.FC));
                        if (FCInfo.ResponseHeaderBytes >= 3) { Data.Unpack(HeaderWord1, PktBuf.bytes, idx); idx += 2; }
                        if (FCInfo.ResponseHeaderBytes >= 5) { Data.Unpack(HeaderWord2, PktBuf.bytes, idx); idx += 2; }
                        if (FCInfo.ResponseHeaderBytes >= 7) { Data.Unpack(HeaderWord3, PktBuf.bytes, idx); idx += 2; }
                        if (FCInfo.ResponseHeaderBytes >= 9) { Data.Unpack(HeaderWord4, PktBuf.bytes, idx); idx += 2; }

                        if (FCInfo.PDUOffsetToResponseDataByteCount > 0)
                        {
                            PktBuf.bytes[PDUStartOffset + FCInfo.PDUOffsetToResponseDataByteCount] = DataByteCountInResponse;
                            PDUSize += DataByteCountInResponse;
                        }

                        PktBuf.numBytes = (ADUSize + PDUSize);
                    }
                }
            }

            /// <summary>Calculated offset where the PDU starts (depends on ADUType)</summary>
            public int PDUStartOffset { get; private set; }
            /// <summary>Gives the size in bytes of this packet's ADU</summary>
            public int ADUSize { get; private set; }
            /// <summary>Gives the total calculated size of this PDU in bytes</summary>
            public int PDUSize { get; private set; }

            /// <summary>Gives the PacketBuffer that stores this PDU's contents.</summary>
            public PacketBuffer PktBuf { get; private set; }

            /// <summary>Local storage for an intermediate buffer used for byte order conversion on GetDataItems call.</summary>
            private PacketBuffer convertPktBuf { get; set; }

            /// <summary>
            /// Helper method used to copy data out of the PktBuf
            /// </summary>
            /// <param name="startGetAtByteOffset">Provides the starting byte offset at which the data is to be obtained.  Adds to the offset determined by the corresponding skipHeader flag</param>
            /// <param name="skipHeader">Pass as true if startGetAtByteOffset should have the header size added to it.</param>
            /// <param name="firstItemIdx">Gives the index of the first item in the itemsArray that is to be copied.</param>
            /// <param name="numItems">Gives the number of items from the itemsArray that are to be copied</param>
            /// <param name="itemsArray">Gives the array from which the data is copied</param>
            /// <returns>True on success, False if the data could not be (safely) copied.</returns>
            public bool GetDataItems<ItemT>(int startGetAtByteOffset, bool skipHeader, int firstItemIdx, int numItems, ItemT[] itemsArray) where ItemT : struct
            {
                int endItemIdx = (firstItemIdx + numItems);

                if (itemsArray == null || firstItemIdx < 0 || endItemIdx > itemsArray.Length)
                    return false;

                Type itemType = typeof(ItemT);
                int itemSize = itemType.IsValueType ? System.Runtime.InteropServices.Marshal.SizeOf(itemType) : 0;
                int copyDataLen = numItems * itemSize;

                startGetAtByteOffset += PDUStartOffset;
                if (skipHeader)
                {
                    if (IsRequest)
                        startGetAtByteOffset += FCInfo.RequestHeaderBytes;
                    else
                        startGetAtByteOffset += FCInfo.ResponseHeaderBytes;
                }

                if (!PktBuf.IsLenValid || startGetAtByteOffset < 0 || (startGetAtByteOffset + copyDataLen) > PktBuf.numBytes)
                    return false;

                int startPutAtByteOffset = (firstItemIdx * itemSize);

                if (Data.IsMachineBigEndian || itemSize == 1)
                {
                    // copy the raw bytes - they stay in machine order
                    System.Buffer.BlockCopy(PktBuf.bytes, startGetAtByteOffset, itemsArray, 0, copyDataLen);

                    return true;
                }
                else
                {
                    if (convertPktBuf == null)
                        convertPktBuf = new PacketBuffer();

                    // copy the raw bytes - they stay in machine order
                    System.Buffer.BlockCopy(PktBuf.bytes, startGetAtByteOffset, convertPktBuf.bytes, 0, copyDataLen);

                    // change the byte order to BigEndian
                    bool success = Utils.Data.ChangeByteOrder(convertPktBuf.bytes, 0, numItems, itemSize, Data.ByteOrder.BigEndian, Data.MachineOrder);

                    System.Buffer.BlockCopy(convertPktBuf.bytes, 0, itemsArray, 0, copyDataLen);

                    return success;
                }
            }

            /// <summary>
            /// Helper method used to copy data into the PktBuf
            /// </summary>
            /// <param name="startPutAtByteOffset">Provides the starting byte offset at which the data is to be put.  Adds to the offset determined by the corresponding skipHeader flag</param>
            /// <param name="skipHeader">Pass as true if startPutAtByteOffset should have the header size added to it.</param>
            /// <param name="firstItemIdx">Gives the index of the first item in the itemsArray that is to be copied.</param>
            /// <param name="numItems">Gives the number of items from the itemsArray that are to be copied</param>
            /// <param name="itemsArray">Gives the array from which the data is copied</param>
            /// <returns>True on success, False if the data could not be (safely) copied.</returns>
            public bool SetDataItems<ItemT>(int startPutAtByteOffset, bool skipHeader, int firstItemIdx, int numItems, ItemT[] itemsArray) where ItemT : struct
            {
                int endItemIdx = (firstItemIdx + numItems);

                if (itemsArray == null || firstItemIdx < 0 || endItemIdx > itemsArray.Length)
                    return false;

                Type itemType = typeof(ItemT);
                int itemSize = itemType.IsValueType ? System.Runtime.InteropServices.Marshal.SizeOf(itemType) : 0;
                int copyDataLen = numItems * itemSize;

                startPutAtByteOffset += PDUStartOffset;
                if (skipHeader)
                {
                    if (IsRequest)
                        startPutAtByteOffset += FCInfo.RequestHeaderBytes;
                    else
                        startPutAtByteOffset += FCInfo.ResponseHeaderBytes;
                }

                if (!PktBuf.IsLenValid || startPutAtByteOffset < 0 || (startPutAtByteOffset + copyDataLen) > PktBuf.numBytes)
                    return false;

                int startGetAtByteOffset = (firstItemIdx * itemSize);

                // copy the raw bytes - they stay in machine order
                System.Buffer.BlockCopy(itemsArray, startGetAtByteOffset, PktBuf.bytes, startPutAtByteOffset, copyDataLen);

                // change the byte order to BigEndian
                return Utils.Data.ChangeByteOrder(PktBuf.bytes, startPutAtByteOffset, numItems, itemSize, Data.MachineOrder, Data.ByteOrder.BigEndian);
            }

            /// <summary>
            /// Returns true if this is an Initialized response and the FC byte has its ExceptionFunctionCode bit set.
            /// </summary>
            public bool IsExceptionResponse
            {
                get
                {
                    return (IsResponse 
                            && (ADUSize > 0) && (PDUStartOffset > 0)
                            && (PktBuf.numBytes >= (ADUSize + 2))
                            && ((PktBuf.bytes[PDUStartOffset] & ExceptionFunctionCodeMask) != 0)
                            );
                }
            }

            /// <summary>
            /// This propety is only valid for responses that have been correctly Initialized so that their ADUSize and PDUStartOffset are non-zero.
            /// When a response PDU contains a sufficient number of bytes (ADUSize + 2) and the FC byte has its exception bit set the this property
            /// returns the contents of the first byte after the FC converted to an ExceptionCode value.  
            /// If the use of the property is not valid then this property returns ExceptionCode.Undefined.  In all other cases this property returns
            /// ExceptionCode.None.
            /// </summary>
            public ExceptionCode ReceivedExceptionCode
            {
                get
                {
                    if (IsRequest || ADUSize <= 0 || PDUStartOffset <= 0)
                        return ExceptionCode.Undefined;

                    if (!IsExceptionResponse)
                        return ExceptionCode.None;

                    byte pduExceptionCodeByte = PktBuf.bytes[PDUStartOffset + 1];

                    return unchecked((ExceptionCode)pduExceptionCodeByte);
                }
            }

            /// <summary>In a response PDU, set this to a non-None value in order to have the response send an Exception value rather than a normal response.</summary>
            public ExceptionCode ExceptionCodeToSend { get; set; }
        }

        #endregion

        //--------------------------------------------------------------------------
        #region Data Check Tools (ASCII LRC and RTU CRC-16 claculators)

        /// <summary>
        /// Calculate and return the logitudinal redundancy check - used for ASCII framing
        /// </summary>
        static public byte CalcAsciiLRC(byte[] byteArray, int startIdx, int byteCount)
        {
            byte sum = 0;

            for (int idx = startIdx; byteCount > 0; idx++, byteCount--)
            {
                sum = unchecked((byte)(sum + byteArray[idx]));
            }

            return sum;
        }

        /// <summary>
        /// Calculate and return the ModbusCRC-16 cyclic redundancy check - used for RTU framing
        /// </summary>
        /// <remarks>
        /// This method and the two tables that it depends on are derived from the Modbus RTU sample CRC code as found on the modbus.org site.
        /// </remarks>
        static public UInt16 CalcRTUCRC16(byte[] byteArray, int startIdx, int byteCount)
        {
            byte crcMSB = 0xff;      // initialize the most significant byte of CRC result
            byte crcLSB = 0xff;      // initialize the least significant byte of the CRC result

            for (int idx = startIdx; byteCount > 0; idx++, byteCount--)
            {
                byte nextByte = byteArray[idx];
                byte tableIdx = unchecked((byte)(crcLSB ^ nextByte));  // calculated index into the two precalculated CRC lookup tables.

                crcLSB = unchecked((byte)(crcMSB ^ crcCalcTable1[tableIdx]));
                crcMSB = crcCalcTable2[tableIdx];
            }

            return Utils.Data.Pack(crcMSB, crcLSB);
        }

        static private readonly byte[] crcCalcTable1 = new byte[]
        {
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40
        };

        static private readonly byte[] crcCalcTable2 = new byte[]
        {
            0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7, 0x05, 0xC5, 0xC4, 0x04, 
            0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09, 0x08, 0xC8, 
            0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD, 0x1D, 0x1C, 0xDC, 
            0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3, 0x11, 0xD1, 0xD0, 0x10, 
            0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32, 0x36, 0xF6, 0xF7, 0x37, 0xF5, 0x35, 0x34, 0xF4, 
            0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A, 0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38, 
            0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE, 0x2E, 0x2F, 0xEF, 0x2D, 0xED, 0xEC, 0x2C, 
            0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26, 0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 
            0xA0, 0x60, 0x61, 0xA1, 0x63, 0xA3, 0xA2, 0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4, 
            0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F, 0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68, 
            0x78, 0xB8, 0xB9, 0x79, 0xBB, 0x7B, 0x7A, 0xBA, 0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 
            0xB4, 0x74, 0x75, 0xB5, 0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0, 
            0x50, 0x90, 0x91, 0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 
            0x9C, 0x5C, 0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 
            0x88, 0x48, 0x49, 0x89, 0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
            0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83, 0x41, 0x81, 0x80, 0x40
        };

        #endregion
    }

    //--------------------------------------------------------------------------
}
