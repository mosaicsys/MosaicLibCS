//-------------------------------------------------------------------
/*! @file ModbusClient.cs
 *  @brief This file defines Modbus helper definitiions and classes that are specific to Modbus Clients
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2011 Mosaic Systems Inc.
 * Copyright (c) 2010 Mosaic Systems Inc.  (prior C++ library version)
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
using System.Collections.Generic;

using System.Net;
using System.Net.Sockets;

using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Part;

namespace MosaicLib.SerialIO.Modbus.Client
{
	//--------------------------------------------------------------------------

    ///<summary>Function Storage and Execution object for FC02_ReadDiscretes function code.  <see cref="DiscreteFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class ReadDiscretes : DiscreteFunctionBase<ReadDiscretes>
    {
        /// <summary>Constructor</summary>
        public ReadDiscretes() { FC = FunctionCode.FC02_ReadDiscretes; }
    };

    ///<summary>Function Storage and Execution object for FC01_ReadCoils function code.  <see cref="DiscreteFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class ReadCoils : DiscreteFunctionBase<ReadCoils>
    {
        /// <summary>Constructor</summary>
        public ReadCoils() { FC = FunctionCode.FC01_ReadCoils; }

        /// <summary>Defines the zero based coil address for the first coil that is read by this command.  This is a proxy for the FirstDiscreteAddr property in the DiscreteFunctionBase base class.</summary>
        public UInt16 FirstCoilAddr { get { return FirstDiscreteAddr; } set { FirstDiscreteAddr = value; } }
        /// <summary>Defines the number of coils that will be read by this command.  This is a proxy for the NumDiscretes property in the DiscreteFunctionBase base class</summary>
        public UInt16 NumCoils { get { return NumDiscretes; } set { NumDiscretes = value; } }
    };

    ///<summary>Function Storage and Execution object for FC05_WriteSingleCoil function code.  <see cref="DiscreteFunctionBase{DerivedType}"/> and <see cref="ClientFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class WriteCoil : ClientFunctionBase<WriteCoil>
    {
        /// <summary>Constructor</summary>
        public WriteCoil() { FC = FunctionCode.FC05_WriteSingleCoil; }

        /// <summary>Defines the zero based coil address for the coil that is written by this command.  This is a proxy for the FirstDiscreteAddr property in the DiscreteFunctionBase base class.</summary>
        public UInt16 CoilAddr { get { return HeaderWord1; } set { HeaderWord1 = value; } }

        /// <summary>
        /// Gets or sets the boolean value that will be written to the coil.  
        /// This is a proxy for the HeaderWord2 in the DiscreteFunctionBase base class which contains 0xff00 when the coil is to be turned on and 0x0000 when the coil is to be turned off.
        /// </summary>
        public bool Value
        {
            get { return (HeaderWord2 != 0); }
            set { HeaderWord2 = unchecked((UInt16)(value ? 0xff00 : 0x0000)); }
        }
    };

    ///<summary>Function Storage and Execution object for FC0f_WriteMultipleCoils function code.  <see cref="DiscreteFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class WriteCoils : DiscreteFunctionBase<WriteCoils>
    {
        /// <summary>Constructor</summary>
        public WriteCoils() { FC = FunctionCode.FC0f_WriteMultipleCoils; }

        /// <summary>Defines the zero based coil address for the first coil that is written by this command.  This is a proxy for the FirstDiscreteAddr property in the DiscreteFunctionBase base class.</summary>
        public UInt16 FirstCoilAddr { get { return FirstDiscreteAddr; } set { FirstDiscreteAddr = value; } }
        /// <summary>Defines the number of coils that will be written by this command.  This is a proxy for the NumDiscretes property in the DiscreteFunctionBase base class</summary>
        public UInt16 NumCoils { get { return NumDiscretes; } set { NumDiscretes = value; } }
    };

    ///<summary>Function Storage and Execution object for FC04_ReadInputRegisters function code.  <see cref="RegistersFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class ReadInputRegisters : RegistersFunctionBase<ReadInputRegisters>
    {
        /// <summary>Constructor</summary>
        public ReadInputRegisters() { FC = FunctionCode.FC04_ReadInputRegisters; }

        /// <summary>Defines the zero based input register address for the first input register that is read by this command.  This is a proxy for the FirstRegAddr1 property in the RegistersFunctionBase base class.</summary>
        public UInt16 FirstInputRegAddr { get { return FirstRegAddr1; } set { FirstRegAddr1 = value; } }
        /// <summary>Defines the number of input registers that will be read by this command.  This is a proxy for the NumRegs1 property in the RegistersFunctionBase base class</summary>
        public UInt16 NumInputRegs { get { return NumRegs1; } set { NumRegs1 = value; } }
    };

    ///<summary>Function Storage and Execution object for FC03_ReadHoldingRegisters function code.  <see cref="RegistersFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class ReadHoldingRegisters : RegistersFunctionBase<ReadHoldingRegisters>
    {
        /// <summary>Constructor</summary>
        public ReadHoldingRegisters() { FC = FunctionCode.FC03_ReadHoldingRegisters; }

        /// <summary>Defines the zero based holding register address for the first holding register that is read by this command.  This is a proxy for the FirstRegAddr1 property in the RegistersFunctionBase base class.</summary>
        public UInt16 FirstHoldingRegAddr { get { return FirstRegAddr1; } set { FirstRegAddr1 = value; } }
        /// <summary>Defines the number of holding registers that will be read by this command.  This is a proxy for the NumRegs1 property in the RegistersFunctionBase base class</summary>
        public UInt16 NumHoldingRegs { get { return NumRegs1; } set { NumRegs1 = value; } }
    };

    ///<summary>Function Storage and Execution object for FC06_WriteSingleHoldingRegister function code.  <see cref="RegistersFunctionBase{DerivedType}"/> and <see cref="ClientFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class WriteHoldingRegister : ClientFunctionBase<WriteHoldingRegister>
    {
        /// <summary>Constructor</summary>
        public WriteHoldingRegister() { FC = FunctionCode.FC06_WriteSingleHoldingRegister; }

        /// <summary>Defines the zero based holding register address that is written by this command.  This is a proxy for the HeaderWord1 property in the ClientFunctionBase base class.</summary>
        public UInt16 HoldingRegAddr { get { return HeaderWord1; } set { HeaderWord1 = value; } }

        /// <summary>Defines the holding register value that is written by this command.  This is a proxy for the HeaderWord2 property in the ClientFunctionBase base class.</summary>
        public Int16 Value { get { return unchecked((Int16) HeaderWord2); } set { HeaderWord2 = unchecked((UInt16) value); } }
    };

    ///<summary>Function Storage and Execution object for FC10_WriteMutlipleHoldingRegisters function code.  <see cref="RegistersFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class WriteHoldingRegisters : RegistersFunctionBase<WriteHoldingRegisters>
    {
        /// <summary>Constructor</summary>
        public WriteHoldingRegisters() { FC = FunctionCode.FC10_WriteMutlipleHoldingRegisters; }

        /// <summary>Defines the zero based holding register address for the first holding register that will be written by this command.  This is a proxy for the FirstRegAddr1 property in the RegistersFunctionBase base class.</summary>
        public UInt16 FirstHoldingRegAddr { get { return FirstRegAddr1; } set { FirstRegAddr1 = value; } }
        /// <summary>Defines the number of holding registers that will be written by this command.  This is a proxy for the NumRegs1 property in the RegistersFunctionBase base class</summary>
        public UInt16 NumHoldingRegs { get { return NumRegs1; } set { NumRegs1 = value; } }
    };

    ///<summary>Function Storage and Execution object for FC17_ReadWriteMultipleRegisters function code.  <see cref="RegistersFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class ReadWriteMultipleRegisters : RegistersFunctionBase<ReadWriteMultipleRegisters>
    {
        /// <summary>Constructor</summary>
        public ReadWriteMultipleRegisters() { FC = FunctionCode.FC17_ReadWriteMultipleRegisters; }

        /// <summary>Defines the zero based input register address for the first input register that is read by this command.  This is a proxy for the FirstRegAddr1 property in the DiscreteFunctionBase base class.</summary>
        public UInt16 FirstInputRegAddr { get { return FirstRegAddr1; } set { FirstRegAddr1 = value; } }
        /// <summary>Defines the number of input registers that will be read by this command.  This is a proxy for the NumRegs1 property in the DiscreteFunctionBase base class</summary>
        public UInt16 NumInputRegs { get { return NumRegs1; } set { NumRegs1 = value; } }

        /// <summary>Defines the zero based holding register address for the first holding register that will be written by this command.  This is a proxy for the FirstRegAddr2 property in the RegistersFunctionBase base class.</summary>
        public UInt16 FirstHoldingRegAddr { get { return FirstRegAddr2; } set { FirstRegAddr2 = value; } }
        /// <summary>Defines the number of holding registers that will be written by this command.  This is a proxy for the NumRegs2 property in the RegistersFunctionBase base class</summary>
        public UInt16 NumHoldingRegs { get { return NumRegs2; } set { NumRegs2 = value; } }
    };

    ///<summary>Function Storage and Execution object for FC16_MaskWriteRegister function code.  <see cref="RegistersFunctionBase{DerivedType}"/> and <see cref="ClientFunctionBase{DerivedType}"/> for more details on use.</summary>
    public class MaskWriteHoldingRegister : ClientFunctionBase<MaskWriteHoldingRegister>
    {
        /// <summary>Constructor</summary>
        public MaskWriteHoldingRegister() { FC = FunctionCode.FC16_MaskWriteRegister; }

        /// <summary>Defines the zero based holding register address that is modified by this command.  This is a proxy for the HeaderWord1 property in the ClientFunctionBase base class.</summary>
        public UInt16 HoldingRegAddr { get { return HeaderWord1; } set { HeaderWord1 = value; } }

        /// <summary>Defines the value that will be And'ed into the holding register value by this command.  This is a proxy for the HeaderWord2 property in the ClientFunctionBase base class.</summary>
        public Int16 AndMask
        {
            get { return unchecked((Int16) HeaderWord2); }
            set { HeaderWord2 = unchecked((UInt16) value); }
        }

        /// <summary>Defines the value that will be Or'ed into the holding register value by this command.  This is a proxy for the HeaderWord3 property in the ClientFunctionBase base class.</summary>
        public Int16 OrMask
        {
            get { return unchecked((Int16) HeaderWord3); }
            set { HeaderWord3 = unchecked((UInt16) value); }
        }
    }

    //--------------------------------------------------------------------------

    #region Client function base classes: DiscreteFunctionBase, RegistersFunctionBase, ClientFunctionBase, ClientFunctionState

    ///<summary>Base class for Client side of Discrete related function codes.  Derived from ClientFunctionBase</summary>
    public class DiscreteFunctionBase<DerivedType> : ClientFunctionBase<DerivedType> where DerivedType : class
    {
        /// <summary>Defines the zero based discrete/coil address for the first discrete/coil that is handled by this command</summary>
        public UInt16 FirstDiscreteAddr { get { return HeaderWord1; } set { HeaderWord1 = value; } }
        /// <summary>Defines the number of dicrete/coil values that will be transfered using this command</summary>
        public UInt16 NumDiscretes { get { return HeaderWord2; } set { HeaderWord2 = value; } }
    };

    ///<summary>Base class for Client side of Register related function codes.  Derived from ClientFunctionBase</summary>
    public class RegistersFunctionBase<DerivedType> : ClientFunctionBase<DerivedType> where DerivedType : class
    {
        /// <summary>Defines the zero based register address for the first register in the first block of regsiters that will be handled by this command</summary>
        public UInt16 FirstRegAddr1 { get { return HeaderWord1; } set { HeaderWord1 = value; } }
        /// <summary>Defines the number of registers in the first block of registers that will be transfered using this command</summary>
        public UInt16 NumRegs1 { get { return HeaderWord2; } set { HeaderWord2 = value; } }

        /// <summary>Defines the zero based register address for the first register in the second block of regsiters that will be handled by this command</summary>
        public UInt16 FirstRegAddr2 { get { return HeaderWord3; } set { HeaderWord3 = value; } }
        /// <summary>Defines the number of registers in the second block of registers that will be transfered using this command</summary>
        public UInt16 NumRegs2 { get { return HeaderWord4; } set { HeaderWord4 = value; } }
    };

	///<summary>Enum defines the progress state for a function storage and execution object (derived from FunctionBase)</summary>
	public enum ClientFunctionState
	{
		///<summary>Function is ready to be run</summary>
		Ready = 0,
		///<summary>Function is in Progress</summary>
		InProgress,
		///<summary>Function completed successfully</summary>
		Succeeded,
		///<summary>Function completed unsuccessfully</summary>
		Failed,
	}

	///<summary>
    /// This class is the templatized version of the ClientFunctionBase.  It allows the base class to implement a passthrough version of the Setup method.
    /// This class is the base class for all Modbus Client Function Exection wrapper objects.
    /// This class defines the storage and common execution pattern for all Modbus supported client operation/command objects that are generally used by a client
    /// to run one or more modbus commands.  Generally individual function execution objects will be derived from this object to implement each of the support
    /// Modbus fucntion codes where the derived object will provide properties and methods that are specific to that function code while the logic in the base class
    /// here will be provide the majority of the implementation of common execution flow patterns such as encoding command packets for transmission and checking and
    /// decoding responses.
    ///</summary>
    ///<remarks>
    /// Basic information in this object includes:
    /// <list type="bullet">
    /// <item>ADUType: RTU (RS232/485) or MBAP (TCP/UDP)</item>
    /// <item>unit addr/RTU device addr</item>
    /// <item>function code</item>
    /// <item>default timeout</item>
    /// <item>other characteristics about the specific command</item>
    /// </list>
    /// This object is expected to be a base class for a set of derived classes that define the construction and use for a specific modbus function codes, or sets thereof.
    /// Common code supports construction of the header, definition and placement of the data, generation of the crc prior to sending the request paacket as well as
    /// each of the reverse steps required after receiving the response packet (if any).
    /// 
    /// In addition, allowing the client to retain the prior storage for the data, setup and headers minimizes the repeated work that is required
    /// when running the same modbus function repeatedly.
    ///</remarks>

    public class ClientFunctionBase<DerivedType> : ClientFunctionBase where DerivedType : class
    {
        /// <summary>Constructor</summary>
        public ClientFunctionBase() : base() { }

        /// <summary>
        /// This method is used to setup the Function after defining the setup property values and before attempting to Get or Set the data content of the fucntion
        /// and before sending the function and/or calling PrepareToSend.  This method Initializes the request ADU in which all of the header contents are stored and in
        /// which the pre-response register/coil data can be accessed.
        /// </summary>
        /// <returns>This object as the DerivedType to support pass through chainging</returns>
        public new DerivedType Setup()
        {
            base.Setup();

            return (this as DerivedType);
        }
    }
	
	///<summary>
    /// This class is the base class for all Modbus Client Function Exection wrapper objects.
    /// This class defines the storage and common execution pattern for all Modbus supported client operation/command objects that are generally used by a client
    /// to run one or more modbus commands.  Generally individual function execution objects will be derived from this object to implement each of the support
    /// Modbus fucntion codes where the derived object will provide properties and methods that are specific to that function code while the logic in the base class
    /// here will be provide the majority of the implementation of common execution flow patterns such as encoding command packets for transmission and checking and
    /// decoding responses.
    ///</summary>
    ///<remarks>
    /// Basic information in this object includes:
    /// <list type="bullet">
    /// <item>ADUType: RTU (RS232/485) or MBAP (TCP/UDP)</item>
    /// <item>unit addr/RTU device addr</item>
    /// <item>function code</item>
    /// <item>default timeout</item>
    /// <item>other characteristics about the specific command</item>
    /// </list>
    /// This object is expected to be a base class for a set of derived classes that define the construction and use for a specific modbus function codes, or sets thereof.
    /// Common code supports construction of the header, definition and placement of the data, generation of the crc prior to sending the request paacket as well as
    /// each of the reverse steps required after receiving the response packet (if any).
    /// 
    /// In addition, allowing the client to retain the prior storage for the data, setup and headers minimizes the repeated work that is required
    /// when running the same modbus function repeatedly.
    ///</remarks>

    public class ClientFunctionBase : FunctionBase
    {
        #region construction

        /// <summary>Default constructor.  Sets TimeLimit to default of 0.5 seconds and sets ClientFunctionState to Ready</summary>
        public ClientFunctionBase() 
        {
            TimeLimit = TimeSpan.FromSeconds(0.5);  // set the default time limit

            ClientFunctionState = ClientFunctionState.Ready;
            ClientFunctionStateTime = QpcTimeStamp.Now;

            LastSuccessTime = QpcTimeStamp.Zero;
        }

        #endregion

        #region Object usage methods

        /// <summary>
        /// This method is used to setup the Function after defining the setup property values and before attempting to Get or Set the data content of the fucntion
        /// and before sending the function and/or calling PrepareToSend.  This method Initializes the request ADU in which all of the header contents are stored and in
        /// which the pre-response register/coil data can be accessed.
        /// </summary>
        public void Setup()
        {
            requestAdu.InitializeRequestPDU(true);
        }

        /// <summary>
        /// Updates requestADU immediately prior to sending the bytes from its Packet.  Marks ClientFunctionState as InProgress unless a failure was detected.
        /// This method signature variant sets defaultMaximiumNumberOfTries to 1.
        /// </summary>
        /// <returns>true on success, false if the command prepare to send failed and marked the command as Failed</returns>
        public bool PrepareToSendRequest()
        {
            return PrepareToSendRequest(1);
        }

        /// <summary>
        /// Updates requestADU immediately prior to sending the bytes from its Packet.  Marks ClientFunctionState as InProgress unless a failure was detected.
        /// returns true on success, false if the command prepare to send failed and marked the command as Failed.
        /// </summary>
        /// <param name="defaultMaximumNumberOfTries">
        /// Provides the default maximum number of tries that the client is configured for.  
        /// This will be used to update the MaximumNumberOfTries property if it is less than 1.
        /// </param>
		public bool PrepareToSendRequest(int defaultMaximumNumberOfTries)
		{
            if (MaximumNumberOfTries < 1)
                MaximumNumberOfTries = Math.Max(defaultMaximumNumberOfTries, 1);
            
            ClientFunctionState = ClientFunctionState.InProgress;
            ClientFunctionStateTime = QpcTimeStamp.Now;

			ExceptionCode = ExceptionCode.None;
            ErrorCodeStr = String.Empty;
            ExceptionCodeIsFromResponse = false;

            string s = requestAdu.PrepareToSendRequest();
            if (!String.IsNullOrEmpty(s))
			{
				NoteFailed("Function Request ADU is not valid: " + s);
				return false;
			}

            responseAdu.PrepareToReceiveResponse(requestAdu);
			responseAdu.PktBuf.numBytes = 0;

			return true;
        }

        ///<summary>
        /// Attempts to Decode the response packet in the responseADU.  If the command has completed then the command state updated accordingly.
        /// returns true if a complete response was found or if a fatal error was found.
        ///</summary>
        public bool AttemptToDecodeResponsePkt()
		{
			string s;
			bool complete = responseAdu.AttemptToDecodeResponsePkt(requestAdu, out s);

            if (!String.IsNullOrEmpty(s))
                NoteFailed(ExceptionCode.PacketDecodeFailed, false, s);
            else if (complete && responseAdu.IsExceptionResponse)
            {
    			ExceptionCode rxEC = responseAdu.ReceivedExceptionCode;

                if (rxEC != ExceptionCode.None)
                    NoteFailed(rxEC, true, "");
                else
                    NoteFailed(ExceptionCode.Undefined, true, "ADU contains explicit Exception Response with ExceptionCode.None");
            }
            else if (complete)
				NoteSucceeded();

            return complete;
		}

        #endregion

        #region ClientFunctionState and related properties

        /// <summary>Returns true if the ClientFunctionState is Succeeded</summary>
        public bool Succeeded { get { return (ClientFunctionState == ClientFunctionState.Succeeded); } }
        /// <summary>Returns true if the ClientFunctionState is Failed</summary>
        public bool Failed { get { return (ClientFunctionState == ClientFunctionState.Failed); } }
        /// <summary>Returns true if the function Succeeded or Failed</summary>
        public bool IsComplete { get { return (Succeeded || Failed); } }

        /// <summary>Gives he current State of this Client Function instance.</summary>
        public ClientFunctionState ClientFunctionState { get; set; }
        /// <summary>Gives the QpcTimeStamp of the last time the ClientFunctionState was internally assigned.</summary>
        public QpcTimeStamp ClientFunctionStateTime { get; set; }

        /// <summary>Gives an ExceptionCode that was returned in the command response or ExcpetionCode.Custom if the failure came from some other source.</summary>
        public ExceptionCode ExceptionCode { get; set; }
        /// <summary>Set to true if this exception code was obtained from the resonseADU body.</summary>
        public bool ExceptionCodeIsFromResponse { get; set; }
        /// <summary>Gives a string description of the last detected failure/command fault reason or the reported ExceptionCode</summary>
        public string ErrorCodeStr { get; set; }
        /// <summary>Gives the QpcTimeStamp at the last time this command succeeded</summary>
        public QpcTimeStamp LastSuccessTime { get; set; }

        /// <summary>
        /// Sets the ClientFunctionState to Failed.  
        /// Updates the contained ExceptionCode to be the given value and sets the ErrorCodeStr based on the function code, the ExceptionCode, and the given ecStr
        /// </summary>
        public string NoteFailed(ExceptionCode ec, bool ecIsFromResponseADU, string comment)
		{
			ExceptionCode = ec;
            ExceptionCodeIsFromResponse = ecIsFromResponseADU;

            if (String.IsNullOrEmpty(comment))
				ErrorCodeStr = Fcns.CheckedFormat("{0} Response was Ex:${1:x2},{2}", requestAdu.FCInfo.FC.ToString(), unchecked((int) ExceptionCode), ExceptionCode.ToString());
			else
				ErrorCodeStr = Fcns.CheckedFormat("{0} Response was Ex:${1:x2},{2} comment:'{3}'", requestAdu.FCInfo.FC.ToString(), unchecked((int) ExceptionCode), ExceptionCode.ToString(), comment);

            ClientFunctionState = ClientFunctionState.Failed;
			ClientFunctionStateTime = QpcTimeStamp.Now;

            return ErrorCodeStr;
		}

        /// <summary>
        /// Sets the ClientFunctionState to Failed.  
        /// Updates the contained ExceptionCode to be ExceptionCode.Custom and sets the ErrorCodeStr based on the function code and the given ecStr
        /// </summary>
        public string NoteFailed(string ecStr)
		{
            ExceptionCode = ExceptionCode.Custom;
            ExceptionCodeIsFromResponse = false;
			ErrorCodeStr = Fcns.CheckedFormat("{0} failed: {1}", requestAdu.FCInfo.FC.ToString(), ecStr);

            ClientFunctionState = ClientFunctionState.Failed;
			ClientFunctionStateTime = QpcTimeStamp.Now;

            return ErrorCodeStr;
		}

        /// <summary>Sets the ClientFunctionState to Succeeded.</summary>
        public void NoteSucceeded()
		{
			ClientFunctionState = ClientFunctionState.Succeeded;
			ClientFunctionStateTime = LastSuccessTime = QpcTimeStamp.Now;
		}

        #endregion

        #region Additional local state information

        /// <summary>
        /// This property defines the maximum number of total attempts that the client will make to perform this request.  
        /// If this number is less than or equal to zero then the client will replace this property value with the current default maximum number of trys
        /// which will be no less than 1.
        /// </summary>
        public int MaximumNumberOfTries { get; set; }

        /// <summary>
        /// This property is updated by the client during execution of the command to indicate the current try number (1..n).  This property will also
        /// reflect the number of tries that were required after the command has been successfully completed.
        /// </summary>
        public int CurrentTryNumber { get; set; }

        #endregion

        #region Common methods used by clients to Get and Set coils, discreets, holding registers and input registers

        /// <summary>
        /// This method is used to read discrete data from the correct ADU and saves this data into the given array's elements.
        /// The ADU is choosen as the response ADU for FuntionCodes that do reading or the request ADU for all other FunctionCodes.
        /// Transfer always starts with the first discrete value contained in the selected ADU.  The given array must be non-null and
        /// its length cannot exceed the number of discrete values contained in the selected ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool GetDiscretes(bool[] discreteValueArray)
        {
            bool readFromResponse = requestAdu.FCInfo.DoesRead;

            return GetDiscretes(readFromResponse, discreteValueArray, 0, ((discreteValueArray != null) ? discreteValueArray.Length : 0));
        }

        /// <summary>
        /// This method is used to read discrete data from the correct ADU and saves this data into the given array's elements.
        /// The ADU is choosen as the response ADU for FuntionCodes that do reading or the request ADU for all other FunctionCodes.
        /// result always starts with the first discrete value contained in the selected ADU.
        /// </summary>
        /// <returns>boolean array containing data from appropriate ADU</returns>
        public bool[] GetDiscretes()
        {
            bool readFromResponse = requestAdu.FCInfo.DoesRead;
            bool[] discretesArray = new bool[readFromResponse ? responseAdu.NumItemsInResponse : requestAdu.NumWriteItemsInRequest];
            GetDiscretes(readFromResponse, discretesArray, 0, discretesArray.Length);
            return discretesArray;
        }

        /// <summary>
        /// This method is used to transfer coil data into the request ADU from the given array's elements.
        /// Transfer always starts with the first discrete value contained in the request ADU.  The given array must be non-null and
        /// its length cannot exceed the number of discrete values contained in the request ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool SetDiscretes(bool[] discreteValueArray)
        {
            return SetDiscretes(discreteValueArray, 0, ((discreteValueArray != null) ? discreteValueArray.Length : 0));
        }

        /// <summary>
        /// This method is used to read register data from the correct ADU and saves this data into the given array's elements.
        /// The ADU is choosen as the response ADU for FuntionCodes that do reading or the request ADU for all other FunctionCodes.
        /// Transfer always starts with the first register value contained in the selected ADU.  The given array must be non-null and
        /// its length cannot exceed the number of register values contained in the selected ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool GetRegisters(short[] regValueArray)
        {
            bool readFromResponse = requestAdu.FCInfo.DoesRead;

            return GetRegisters(readFromResponse, regValueArray, 0, ((regValueArray != null) ? regValueArray.Length : 0));
        }

        /// <summary>
        /// This method is used to read register data from the correct ADU and saves this data into the given array's elements.
        /// The ADU is choosen as the response ADU for FuntionCodes that do reading or the request ADU for all other FunctionCodes.
        /// result always starts with the first discrete value contained in the selected ADU.
        /// </summary>
        /// <returns>register array containing data from appropriate ADU</returns>
        public short[] GetRegisters()
        {
            bool readFromResponse = requestAdu.FCInfo.DoesRead;
            short[] registerArray = new short[readFromResponse ? responseAdu.NumItemsInResponse : requestAdu.NumWriteItemsInRequest];
            GetRegisters(readFromResponse, registerArray, 0, registerArray.Length);
            return registerArray;
        }

        /// <summary>
        /// This method is used to transfer holding register data into the request ADU from the given array's elements.
        /// Transfer always starts with the first holding register value contained in the request ADU.  The given array must be non-null and
        /// its length cannot exceed the number of holding register values contained in the request ADU.
        /// </summary>
        /// <returns>True on success, false in all other cases.</returns>
        public bool SetRegisters(short[] regValueArray)
        {
            return SetRegisters(regValueArray, 0, ((regValueArray != null) ? regValueArray.Length : 0));
        }

        #endregion
    };

    #endregion

    //--------------------------------------------------------------------------

    #region Client port adapter(s)

    /// <summary>
    /// Each Modbus Client Function Adapter type of object supports a set of Run methods that may be used to run one or more Modbus Client Function
    /// using some underlying communication medium.  The <seealso cref="ModbusClientFunctionPortAdapter"/> class is an example of this which supports
    /// running Modbus Client Functions using any type of SerialIO.IPort objet.
    /// </summary>
    public interface IModbusClientFunctionAdapter : IPartBase
    {
        /// <summary>
        /// Attempts to run the given function.  Returns true on success on false otherwise.  
        /// function's final state reflects the details of the success and failure (including the failure reason, as appropriate)
        /// </summary>
        bool Run(ClientFunctionBase function);

        /// <summary>
        /// Attempts to run a sequence of functions from the passed functionArray.  Returns true on success on false otherwise.  
        /// function's final state reflects the details of the success and failure (including the failure reason, as appropriate)
        /// </summary>
        /// <param name="functionArray">Gives a list/array of ClientFunctionBase instances that are to be Run.</param>
        /// <param name="stopOnFirstError">Set this to true to block executing functions after any first error is encountered.  Set this to false to attempt to run each function regardless of the success of any prior function in the array.</param>
        /// <returns>Returns true on success on false otherwise.  </returns>
        bool Run(IEnumerable<ClientFunctionBase> functionArray, bool stopOnFirstError);
    }

    /// <summary>
    /// Thie class defines a Simple IPart that may be used to create and control an IPort instance (from a given <see cref="MosaicLib.SerialIO.PortConfig"/> object)
    /// and to use that port to perform the underlying data transfers required to run Modbus Client Functions, assuming that the port can be used to connect
    /// to an appropriately configured Modbus Server.
    /// </summary>
    public class ModbusClientFunctionPortAdapter : Modular.Part.SimplePartBase, IModbusClientFunctionAdapter
    {
        #region static values

        static int DefaultMaximumNumberOfTriesForStreamPorts = 1;
        static int DefaultMaximumNumberOfTriesForDatagramPorts = 3;

        #endregion

        #region Construction and Destruction

        /// <summary>Contructor - requires <paramref name="partID"/> and <paramref name="portConfig"/></summary>
        public ModbusClientFunctionPortAdapter(string partID, SerialIO.PortConfig portConfig)
            : base(partID)
        {
            Timeout = portConfig.ReadTimeout;
            portConfig.ReadTimeout = TimeSpan.FromSeconds(Math.Min(0.1, Timeout.TotalSeconds));

            port = SerialIO.Factory.CreatePort(portConfig);
            portBaseStateObserver = new SequencedRefObjectSourceObserver<IBaseState, Int32>(port.BaseStateNotifier);

            IPortBehavior portBehavior = port.PortBehavior;

            DefaultMaximumNumberOfTries = (portBehavior.IsDatagramPort ? DefaultMaximumNumberOfTriesForDatagramPorts : DefaultMaximumNumberOfTriesForStreamPorts);

            FlushPeriod = (portBehavior.IsNetworkPort ? TimeSpan.FromSeconds(0.1) : TimeSpan.FromSeconds(0.3));            // use 0.1 as default for network connections, 0.2 for other types.
            NominalSpinWaitPeriod = TimeSpan.FromSeconds(0.2);

            portReadAction = port.CreateReadAction(portReadActionParam = new ReadActionParam() { WaitForAllBytes = false });
            portWriteAction = port.CreateWriteAction(portWriteActionParam = new WriteActionParam());
            portFlushAction = port.CreateFlushAction(FlushPeriod);
            portReinitializeAction = port.CreateGoOnlineAction(true);

            portReadAction.NotifyOnComplete.AddItem(actionWaitEvent);
            portWriteAction.NotifyOnComplete.AddItem(actionWaitEvent);
            portFlushAction.NotifyOnComplete.AddItem(actionWaitEvent);
            portReinitializeAction.NotifyOnComplete.AddItem(actionWaitEvent);
        }

        /// <summary>
        /// Requird implementation method which is used to handle explicit dispose operations from Part Base.
        /// Signature is derived from abstract method in DisposeableBase base class.
        /// </summary>
        protected override void Dispose(DisposableBase.DisposeType disposeType)
        {
            if (disposeType == DisposeType.CalledExplicitly)
            {
                if (port != null)
                    port.StopPart();
                Fcns.DisposeOfObject(ref port);
                Fcns.DisposeOfObject(ref actionWaitEvent);
            }
        }

        #endregion

        #region internals

        IPort port = null;
        ISequencedObjectSourceObserver<IBaseState> portBaseStateObserver = null;

        IReadAction portReadAction = null;
        ReadActionParam portReadActionParam = null;
        IWriteAction portWriteAction = null;
        WriteActionParam portWriteActionParam = null;
        IFlushAction portFlushAction = null;
        IBasicAction portReinitializeAction = null;
        WaitEventNotifier actionWaitEvent = new WaitEventNotifier(WaitEventNotifier.Behavior.WakeOne);

        #endregion

        #region Public properties and methods

        /// <summary>Defines the transaction time limit value that is used by this client when running modbus transactions.</summary>
        public TimeSpan Timeout { get; set; }
        /// <summary>
        /// Defines the default maximum number of attempts that the client will use when attempting to perform each transaction.
        /// Defaults to value that depends on the serial port byte delivery behavior (1 try for stream ports, more for datagram ports).
        /// Client will always make at least one attempt to perform each transaction even if this number is less than 1.
        /// </summary>
        public int DefaultMaximumNumberOfTries { get; set; }
        /// <summary>Defines the period of time that is used for post-transaction failure flush operations.</summary>
        public TimeSpan FlushPeriod { get; set; }
        /// <summary>Defines the nominal spin speed for this client.</summary>
        public TimeSpan NominalSpinWaitPeriod { get; set; }

        /// <summary>Pass through method allows the caller to have the port create a GoOnline Action which is generally used to start the port.</summary>
        public IBasicAction CreateGoOnlineActionOnPort(bool andInitialize)
        {
            return port.CreateGoOnlineAction(andInitialize);
        }

        /// <summary>Pass through method allows the caller to have the port create a GoOffline Action which is generally used to close the port.</summary>
        public IBasicAction CreateGoOfflineActionOnPort()
        {
            return port.CreateGoOfflineAction();
        }

        /// <summary>Pass through method allows the caller to Stop the Port part that is managed by this client.</summary>
        public void StopPort()
        {
            port.StopPart();
        }

        /// <summary>Service method used to propagate published port state information into and through this client object.</summary>
        public void Service()
        {
            InnerServicePortStateRelay();
        }

        private void InnerServicePortStateRelay()
        {
            if (portBaseStateObserver.IsUpdateNeeded)
            {
                portBaseStateObserver.Update();
                SetBaseState(portBaseStateObserver.Object, "Republishing from port", true);
            }
        }

        /// <summary>
        /// Attempts to run the given function.  Returns true on success on false otherwise.  
        /// function's final state reflects the details of the success and failure (including the failure reason, as appropriate)
        /// </summary>
        public bool Run(ClientFunctionBase function)
        {
            return InnerRun(function);
        }

        /// <summary>
        /// Attempts to run a sequence of functions from the passed functionArray.  Returns true on success on false otherwise.  
        /// function's final state reflects the details of the success and failure (including the failure reason, as appropriate)
        /// </summary>
        /// <param name="functionArray">Gives a list/array of ClientFunctionBase instances that are to be Run.</param>
        /// <param name="stopOnFirstError">Set this to true to block executing functions after any first error is encountered.  Set this to false to attempt to run each function regardless of the success of any prior function in the array.</param>
        /// <returns>Returns true on success on false otherwise.  </returns>
        public bool Run(IEnumerable<ClientFunctionBase> functionArray, bool stopOnFirstError)
        {
            bool success = true;

            foreach (ClientFunctionBase function in functionArray)
            {
                if (Run(function))
                    continue;

                success = false;

                if (stopOnFirstError)
                    break;
            }

            return success;
        }

        private bool nextCommandInitialFlushIsNeeded = false;

        /// <summary>
        /// Performs the steps required to attempt to run the given function using the port for communications.
        /// </summary>
        /// <param name="function">Gives the function that the client will attempt to run.</param>
        /// <returns>True if the function was completed successfully and false otherwise.</returns>
        protected bool InnerRun(ClientFunctionBase function)
        {
            InnerServicePortStateRelay();

            bool performInitialFlush = nextCommandInitialFlushIsNeeded;

            nextCommandInitialFlushIsNeeded = false;

            // assign a new transaction number and build the transmit buffer contents
            if (!function.PrepareToSendRequest(DefaultMaximumNumberOfTries))
                return false;

            function.CurrentTryNumber = 1;

            if (!portBaseStateObserver.Object.IsConnected)
            {
                function.NoteFailed("Port is not connected: " + portBaseStateObserver.Object.ToString());
                nextCommandInitialFlushIsNeeded = true;
                return false;
            }

            // Flush the port if the last command did not succeed.
            if (performInitialFlush && FlushPeriod != TimeSpan.Zero)
            {

                portFlushAction.ParamValue = FlushPeriod;
                portFlushAction.Run();      // failures will not directly effect the success/failure of the function execution
            }

            for (;;)
            {
                InnerServicePortStateRelay();

                // logic to perform on later tries
                if (function.CurrentTryNumber != 1)
                {
                    Log.Debug.Emit("Function FC:{0} Starting Try {1} of {2}", function.requestAdu.FCInfo.FC, function.CurrentTryNumber, function.MaximumNumberOfTries);

                    if (FlushPeriod != TimeSpan.Zero)
                    {
                        portFlushAction.ParamValue = FlushPeriod;
                        portFlushAction.Run();      // failures will not directly effect the success/failure of the function execution

                        InnerServicePortStateRelay();
                    }
                }

                // Setup the write and read actions

                portWriteActionParam.Reset();
                portWriteActionParam.Buffer = function.requestAdu.PktBuf.bytes;
                portWriteActionParam.BytesToWrite = function.requestAdu.PktBuf.numBytes;

                portReadActionParam.Reset();
                portReadActionParam.Buffer = function.responseAdu.PktBuf.bytes;
                portReadActionParam.BytesToRead = function.responseAdu.PktBuf.bytes.Length;
                portReadActionParam.BytesRead = 0;      // start reading into the beginning of the buffer.

                // reset the function's ClientFunctionState to InProgress if it was not already there.
                if (function.ClientFunctionState != ClientFunctionState.InProgress)
                {
                    ClientFunctionState priorState = function.ClientFunctionState;
                    function.ClientFunctionState = ClientFunctionState.InProgress;
                    Log.Debug.Emit("Function FC:{0} resetting state to {1} from {2} at try:{3}", function.requestAdu.FCInfo.FC, function.ClientFunctionState, priorState, function.CurrentTryNumber);
                }

                // Write the request to the target

                portWriteAction.Run();

                string resultCode = (function.IsComplete ? function.ErrorCodeStr : null);
                ExceptionCode exceptionCode = ExceptionCode.Undefined;

                if (resultCode == null && !portWriteAction.ActionState.Succeeded)
                {
                    resultCode = Fcns.CheckedFormat("Port write failed: {0}", portWriteAction.ActionState);
                    exceptionCode = ExceptionCode.CommunicationError;
                }

                // wait to receive a usable response
                QpcTimer timeLimitTimer = new QpcTimer() { TriggerInterval = Timeout, Started = true };
                bool allowRetry = false;

                if (resultCode == null)
                    resultCode = Fcns.MapNullOrEmptyTo(portReadAction.Start(), null);

                while (resultCode == null && !function.IsComplete)
                {
                    actionWaitEvent.Wait(NominalSpinWaitPeriod);

                    InnerServicePortStateRelay();

                    if (portReadAction.ActionState.IsComplete)
                    {
                        function.responseAdu.PktBuf.numBytes = portReadActionParam.BytesRead;

                        if (portReadAction.ActionState.Failed && portReadActionParam.ActionResultEnum != ActionResultEnum.ReadTimeout)
                        {
                            resultCode = "PortReadAction failed: " + portReadAction.ActionState.ResultCode;
                            exceptionCode = ExceptionCode.CommunicationError;
                            allowRetry = true;
                            break;
                        }

                        if (function.responseAdu.PktBuf.numBytes > 0)
                        {
                            if (function.AttemptToDecodeResponsePkt())
                            {
                                resultCode = function.ErrorCodeStr;
                                exceptionCode = function.ExceptionCode;
                                break;
                            }

                            if (portReadAction.ActionState.Succeeded && port.PortBehavior.IsDatagramPort)
                            {
                                resultCode = "Incomplete response received on Datagram Port";
                                exceptionCode = ExceptionCode.PacketDecodeFailed;
                                allowRetry = true;
                                break;
                            }
                        }
                        
                        if (timeLimitTimer.IsTriggered)
                        {
                            resultCode = Fcns.CheckedFormat("Time limit reached after {0:f3} seconds, {1} bytes received", timeLimitTimer.ElapsedTimeInSeconds, portReadActionParam.BytesRead);
                            exceptionCode = ((portReadActionParam.BytesRead == 0) ? ExceptionCode.CommunciationTimeoutWithNoResponse : ExceptionCode.CommunicationTimeoutWithPartialResponse);
                            allowRetry = true;
                            break;
                        }
                        else
                        {
                            // setup to append bytes in the next read operation to the current buffer
                            portReadActionParam.BytesToRead = function.responseAdu.PktBuf.bytes.Length - portReadActionParam.BytesRead;
                            resultCode = Fcns.MapNullOrEmptyTo(portReadAction.Start(), null);
                        }
                    }
                }

                bool attemptRetry = (allowRetry && !function.Succeeded && !function.ExceptionCodeIsFromResponse && (function.CurrentTryNumber < function.MaximumNumberOfTries));

                if (!attemptRetry)
                {
                    if (!function.IsComplete)
                    {
                        if (resultCode != null)
                            function.NoteFailed(exceptionCode, false, resultCode);
                        else
                            function.NoteFailed(ExceptionCode.Undefined, false, "Internal: Run failed with no reported cause");
                    }

                    if (function.Failed && !function.ExceptionCodeIsFromResponse)
                        nextCommandInitialFlushIsNeeded = true;

                    if (function.CurrentTryNumber != 1)
                    {
                        if (function.Succeeded)
                            Log.Debug.Emit("Function FC:{0} succeeded at try:{1} of {2}", function.requestAdu.FCInfo.FC, function.CurrentTryNumber, function.MaximumNumberOfTries);
                        else
                            Log.Debug.Emit("Function FC:{0} failed at try:{1} of {2} [{3} {4}]", function.requestAdu.FCInfo.FC, function.CurrentTryNumber, function.MaximumNumberOfTries, function.ExceptionCode, function.ErrorCodeStr);
                    }

                    return function.Succeeded;
                }

                Log.Debug.Emit("Function FC:{0} attempting (re)try:{1} of {2} [prior ec:{3}]", function.requestAdu.FCInfo.FC, function.CurrentTryNumber, function.MaximumNumberOfTries, Fcns.MapNullToEmpty(resultCode));

                function.CurrentTryNumber++;

                if (function.ClientFunctionState != ClientFunctionState.InProgress)
                {
                    function.ClientFunctionState = ClientFunctionState.InProgress;
                    function.ClientFunctionStateTime = QpcTimeStamp.Now;
                }
            }
        }

        #endregion
    }

    #endregion

    //--------------------------------------------------------------------------
}
