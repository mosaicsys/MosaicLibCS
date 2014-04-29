//-------------------------------------------------------------------
/*! @file ModbusServer.cs
 * @brief This file defines Modbus helper definitiions and classes that are specific to Modbus Servers
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2013 Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2010 Mosaic Systems Inc.  All rights reserved (portions of prior C++ library version)
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

namespace MosaicLib.SerialIO.Modbus.Server
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

    /// <summary>
    /// This interface defines the set of FunctionCode related methods that a Modbus Server must implement in order 
    /// to support basic modbus functions related to the reading and writing of coils, discretes, input registers and holding registers.
    /// </summary>
	public interface IBasicReadWriteFCs
	{
		///<summary>Function Storage and Execution object for FC02_ReadDiscretes function code.</summary>
		ExceptionCode FC02_ReadDiscretes(UInt16 firstDiscreteAddr, UInt16 numDiscretes, bool [] discreteValues);
		
		///<summary>Function Storage and Execution object for FC01_ReadCoils function code.</summary>
        ExceptionCode FC01_ReadCoils(UInt16 firstCoilAddr, UInt16 numCoils, bool[] coilValues);

		///<summary>Function Storage and Execution object for FC05_WriteSingleCoil function code.</summary>
		ExceptionCode FC05_WriteSingleCoil(UInt16 coilAddr, bool coilValue);

		///<summary>Function Storage and Execution object for FC0f_WriteMultipleCoils function code.</summary>
		ExceptionCode FC0f_WriteCoils(UInt16 firstCoilAddr, UInt16 numCoils, bool [] coilValues);
		
		///<summary>Function Storage and Execution object for FC04_ReadInputRegisters function code.</summary>
		ExceptionCode FC04_ReadInputRegisters(UInt16 firstInputRegisterAddr, UInt16 numInputRegisters, Int16 [] inputRegisterValues);

		///<summary>Function Storage and Execution object for FC03_ReadHoldingRegisters function code.</summary>
        ExceptionCode FC03_ReadHoldingRegisters(UInt16 firstHoldingRegisterAddr, UInt16 numHoldingRegisters, Int16[] holdingRegisterValues);

		///<summary>Function Storage and Execution object for FC06_WriteSingleHoldingRegister function code.</summary>
		ExceptionCode FC06_WriteSingleHoldingRegister(UInt16 holdingRegisterAddr, Int16 holdingRegisterValue);	
		
		///<summary>Function Storage and Execution object for FC10_WriteMutlipleHoldingRegisters function code.</summary>
        ExceptionCode FC10_WriteMutlipleHoldingRegisters(UInt16 firstHoldingRegisterAddr, UInt16 numHoldingRegisters, Int16[] holdingRegisterValues);

		///<summary>Function Storage and Execution object for FC17_ReadWriteMultipleRegisters function code.</summary>
		ExceptionCode FC17_ReadWriteMultipleRegisters(UInt16 firstInputRegisterAddr, UInt16 numInputRegisters, Int16 [] inputRegisterValues, UInt16 firstHoldingRegisterAddr, UInt16 numHoldingRegisters, Int16 [] holdingRegisterValues);
		
		///<summary>Function Storage and Execution object for FC16_MaskWriteRegister function code.</summary>
		ExceptionCode FC16_MaskWriteRegister(UInt16 holdingRegisterAddr, Int16 holdingRegisterAndMaskValue, Int16 holdingRegisterOrMaskValue);
	}

    /// <summary>
    /// This interface is the primary interface that is to be implemented by Modbus execution engines (FCServers).
    /// it extends the <see cref="IBasicReadWriteFCs"/> by adding a Name property and a Service method.  
    /// This interface is used by the ServerFunctionContainer to process each decoded function code request.
    /// </summary>
    public interface IModbusFCServer : IBasicReadWriteFCs
    {
        /// <summary>Gives the name of the FC Service instance</summary>
        string Name { get; }

        /// <summary>Allows the FC Server instance to support a repeated service method so that it can perform background activities without its own thread.</summary>
        void Service();
    }

    //--------------------------------------------------------------------------

    /// <summary>
    /// This class encapsulates nearly all of the logic that is required to accept, decode, process, and respond to 
    /// modbus requests at the binary wire level.  Functionally this class is similar to the client's <see cref="MosaicLib.SerialIO.Modbus.Client.ClientFunctionBase"/> class
    /// in that it encapsulates both the Modbus function level aspects and the binary transcoding and validation aspects for a Modbus server.
    /// This class is generally used in conjunction with a server communication object that reads into this object's requestAdu buffer and writes the
    /// response, if any, from the corresponding responseAdu buffer.
    /// </summary>
    public class ServerFunctionContainer : FunctionBase
    {
        #region IMesgEmitter and setup properties

        /// <summary>MesgEmitter container field used to emit Issue messages from this object.  Use with <see cref="Emitters"/> property to set from a dictionary of name -> emitter instance mappings.</summary>
        [Logging.MesgEmitterProperty]
        public Logging.MesgEmitterContainer Issue = new Logging.MesgEmitterContainer();

        /// <summary>MesgEmitter container field used to emit Debug messages from this object.  Use with <see cref="Emitters"/> property to set from a dictionary of name -> emitter instance mappings.</summary>
        [Logging.MesgEmitterProperty]
        public Logging.MesgEmitterContainer Debug = new Logging.MesgEmitterContainer();

        /// <summary>MesgEmitter container field used to emit Trace messages from this object.  Use with <see cref="Emitters"/> property to set from a dictionary of name -> emitter instance mappings.</summary>
        [Logging.MesgEmitterProperty]
        public Logging.MesgEmitterContainer Trace = new Logging.MesgEmitterContainer();

        /// <summary>Set only property used to set one or more of the <see cref="Logging.MesgEmitterContainer"/> from a caller provided dictionary of name -> emitter instance mappings.</summary>
        public IDictionary<string, Logging.IMesgEmitter> Emitters { set { Logging.SetAnnotatedInstanceEmitters(this, value); } }

        #endregion

        #region Private fields

        private bool[] coilArray = new bool[Details.PDU_MaximumDiscretesPerFC];
        private bool[] discreteArray = new bool[Details.PDU_MaximumDiscretesPerFC];
        private Int16[] holdingRegisterArray = new Int16[Details.PDU_MaximumRegistersPerFC];
        private Int16[] inputRegisterArray = new Int16[Details.PDU_MaximumRegistersPerFC];

        #endregion

        /// <summary>
        /// Attempts to decode the bytes in the requestAdu.  Returns true if a complete response was found or if a fatal error was found.
        /// </summary>
        public bool AttemptToDecodeRequestPkt(out string ec)
        {
            return requestAdu.AttemptToDecodeRequestPkt(out ec);
        }

        /// <summary>Get/Set Server configuration property used to set the server to respond to all addresses/UnitIDs or only to the server's configured address</summary>
        public bool RespondToAllTargets { get; set; }
        /// <summary>Get/Set Server configuration property gives the MBAP UnitID that this server is generally expecting to be addressed as.  Ignored when not using MBAP or when <see cref="RespondToAllTargets"/> is set to true</summary>
        public byte MBAPUnitID { get; set; }

        /// <summary>
        /// Services the previously decoded request ADU packet by invoking the correspondingly selected method in the given IModbusFCServer instance and using
        /// the response and data that it provides to generate the response packet (if any) that can be sent back to the client.
        /// Returns true if a response packet is available to send or false if this request produced no response packet.
        /// </summary>
        /// <returns>true if a response packet is available to send or false if this request produced no response packet.</returns>
        public bool ServiceDecodedRequest(IModbusFCServer fcServer)
        {
            fcServer.Service();

            // update the responseAdu as an appropriate response to the requestAdu
            responseAdu.PktBuf.Clear();

            responseAdu.FCInfo = requestAdu.FCInfo;
            responseAdu.ADUType = requestAdu.ADUType;
            responseAdu.NumItemsInResponse = requestAdu.NumReadItemsInRequest;
            responseAdu.ExceptionCodeToSend = ExceptionCode.None;

            if (RespondToAllTargets)
            {
                // respond as whatever target the request was aimed at
                if (ADUType == ADUType.RTU)
                    responseAdu.RTUAddr = requestAdu.RTUAddr;
                else
                    responseAdu.UnitID = requestAdu.UnitID;
            }
            else
            {
                if (ADUType == ADUType.RTU)
                {
                    if (requestAdu.RTUAddr != RTUAddr)
                    {
                        Debug.Emitter.Emit("Ignoring miss-addressed request {0} {1} RTUAddr:{2}", requestAdu.ADUType, requestAdu.FCInfo.FC, requestAdu.RTUAddr);
                        return false;   // the request is not for us
                    }
                    responseAdu.RTUAddr = RTUAddr;
                }
                else
                {
                    if (requestAdu.UnitID != MBAPUnitID)
                    {
                        Debug.Emitter.Emit("Ignoring miss-addressed request {0} {1} UnitID:{2}", requestAdu.ADUType, requestAdu.FCInfo.FC, requestAdu.UnitID);
                        return false;   // the request is not for us
                    }
                    responseAdu.UnitID = MBAPUnitID;
                }
            }

            responseAdu.InitializeResponsePDU();       // sets up internal pointer to count/length values for later use.

            ExceptionCode exceptionCode = ExceptionCode.None;

            try
            {
                // extract the relevant data from the requestADU
                if (exceptionCode == ExceptionCode.None)
                {
                    // get the correct data from the request
                    bool getDataSuccess = true;

                    switch (requestAdu.FCInfo.FC)
                    {
                        case FunctionCode.FC0f_WriteMultipleCoils: getDataSuccess = GetDiscretes(false, coilArray, 0, requestAdu.NumWriteItemsInRequest); break;
                        case FunctionCode.FC10_WriteMutlipleHoldingRegisters: getDataSuccess = GetRegisters(false, holdingRegisterArray, 0, requestAdu.NumWriteItemsInRequest); break;
                        case FunctionCode.FC17_ReadWriteMultipleRegisters: getDataSuccess = GetRegisters(false, holdingRegisterArray, 0, requestAdu.NumWriteItemsInRequest); break;
                        default: break;
                    }

                    if (!getDataSuccess)
                        exceptionCode = ExceptionCode.IllegalDataValue;
                }

                // ask the fcServer to perform the requested action
                if (exceptionCode == ExceptionCode.None)
                {
                    switch (requestAdu.FCInfo.FC)
                    {
                        case FunctionCode.FC00_None: exceptionCode = ExceptionCode.IllegalFunction; break;
                        case FunctionCode.FC01_ReadCoils: exceptionCode = fcServer.FC01_ReadCoils(requestAdu.FirstReadItemAddrInRequest, requestAdu.NumReadItemsInRequest, coilArray); break;
                        case FunctionCode.FC02_ReadDiscretes: exceptionCode = fcServer.FC02_ReadDiscretes(requestAdu.FirstReadItemAddrInRequest, requestAdu.NumReadItemsInRequest, discreteArray); break;
                        case FunctionCode.FC03_ReadHoldingRegisters: exceptionCode = fcServer.FC03_ReadHoldingRegisters(requestAdu.FirstReadItemAddrInRequest, requestAdu.NumReadItemsInRequest, holdingRegisterArray); break;
                        case FunctionCode.FC04_ReadInputRegisters: exceptionCode = fcServer.FC04_ReadInputRegisters(requestAdu.FirstReadItemAddrInRequest, requestAdu.NumReadItemsInRequest, inputRegisterArray); break;
                        case FunctionCode.FC05_WriteSingleCoil: exceptionCode = fcServer.FC05_WriteSingleCoil(requestAdu.FirstWriteItemAddrInRequest, (HeaderWord2 != 0)); break;
                        case FunctionCode.FC06_WriteSingleHoldingRegister: exceptionCode = fcServer.FC06_WriteSingleHoldingRegister(requestAdu.FirstWriteItemAddrInRequest, unchecked((Int16) HeaderWord2)); break;
                        case FunctionCode.FC08_Diagnostics: exceptionCode = ExceptionCode.IllegalFunction; break;
                        case FunctionCode.FC0f_WriteMultipleCoils: exceptionCode = fcServer.FC0f_WriteCoils(requestAdu.FirstWriteItemAddrInRequest, requestAdu.NumWriteItemsInRequest, coilArray); break;
                        case FunctionCode.FC10_WriteMutlipleHoldingRegisters: exceptionCode = fcServer.FC10_WriteMutlipleHoldingRegisters(requestAdu.FirstWriteItemAddrInRequest, requestAdu.NumWriteItemsInRequest, holdingRegisterArray); break;
                        case FunctionCode.FC16_MaskWriteRegister: exceptionCode = fcServer.FC16_MaskWriteRegister(requestAdu.FirstWriteItemAddrInRequest, unchecked((Int16) HeaderWord2), unchecked((Int16) HeaderWord3)); break;
                        case FunctionCode.FC17_ReadWriteMultipleRegisters: exceptionCode = fcServer.FC17_ReadWriteMultipleRegisters(requestAdu.FirstReadItemAddrInRequest, requestAdu.NumReadItemsInRequest, inputRegisterArray, requestAdu.FirstWriteItemAddrInRequest, requestAdu.NumWriteItemsInRequest, holdingRegisterArray); break;
                        case FunctionCode.FC2b_EncapsulatedInterfaceTransport: exceptionCode = ExceptionCode.IllegalFunction; break;
                        default: exceptionCode = ExceptionCode.IllegalFunction; break;
                    }
                }

                // if the fcServer operation succeeded then build a normal response packet
                if (exceptionCode == ExceptionCode.None)
                {
                    // put the correct data into the response
                    bool putDataSuccess = true;

                    switch (requestAdu.FCInfo.FC)
                    {
                        case FunctionCode.FC01_ReadCoils: putDataSuccess = SetDiscretes(true, coilArray, 0, responseAdu.NumItemsInResponse); break;
                        case FunctionCode.FC02_ReadDiscretes: putDataSuccess = SetDiscretes(true, discreteArray, 0, responseAdu.NumItemsInResponse); break;
                        case FunctionCode.FC03_ReadHoldingRegisters: putDataSuccess = SetRegisters(true, holdingRegisterArray, 0, responseAdu.NumItemsInResponse); break;
                        case FunctionCode.FC04_ReadInputRegisters: putDataSuccess = SetRegisters(true, inputRegisterArray, 0, responseAdu.NumItemsInResponse); break;
                        case FunctionCode.FC17_ReadWriteMultipleRegisters: putDataSuccess = SetRegisters(true, inputRegisterArray, 0, responseAdu.NumItemsInResponse); break;
                        default: break;
                    }

                    if (!putDataSuccess)
                        exceptionCode = ExceptionCode.IllegalDataValue;
                }

                if (exceptionCode == ExceptionCode.None)
                {
                    responseAdu.InitializeResponsePDUForSend();
                    responseAdu.PrepareToSendResponse(requestAdu);

                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Issue.Emitter.Emit("Modbus Servier '{0}' threw unexpected exception: fc:{1} ex:{2}", fcServer.Name, requestAdu.FCInfo.FC, ex);
                exceptionCode = ExceptionCode.SlaveDeviceFailure;
            }

            if (exceptionCode != ExceptionCode.IgnoreRequest)
            {
                // build an excpetion response packet
                responseAdu.ExceptionCodeToSend = exceptionCode;

                responseAdu.InitializeResponsePDUForSend();
                responseAdu.PrepareToSendResponse(requestAdu);

                return true;
            }
            else
            {
                // make this packet invalid so that it cannot be sent
                responseAdu.PktBuf.Invalidate();
                return false;
            }
        }
    }

    //--------------------------------------------------------------------------

    #region Server port adapter(s)

    /// <summary>
    /// Thie class defines a Simple IPart that may be used to create and control an IPort instance (from a given <see cref="MosaicLib.SerialIO.PortConfig"/> object)
    /// and to use that port to perform the underlying data transfers required to run Modbus Client Functions, assuming that the port can be used to connect
    /// to an appropriately configured Modbus Server.
    /// </summary>
    public class ModbusServerFunctionPortAdapter : Modular.Part.SimpleActivePartBase
    {
        #region Construction and Destruction

        /// <summary>Contructor</summary>
        public ModbusServerFunctionPortAdapter(string partID, SerialIO.PortConfig portConfig, IModbusFCServer fcServer, ADUType aduType, byte unitID, bool responseToAllUnits)
            : base(partID, TimeSpan.FromSeconds(0.2))
        {
            this.fcServer = fcServer;

            Timeout = portConfig.ReadTimeout;
            portConfig.ReadTimeout = TimeSpan.FromSeconds(Math.Min(0.1, Timeout.TotalSeconds));

            port = SerialIO.Factory.CreatePort(portConfig);
            portBaseStateObserver = new SequencedRefObjectSourceObserver<IBaseState, Int32>(port.BaseStateNotifier);

            IPortBehavior portBehavior = port.PortBehavior;

            IDictionary<string, Logging.IMesgEmitter> emitters = new Dictionary<string,Logging.IMesgEmitter>() { { "Issue", Log.Error }, {"Debug", Log.Debug}, {"Trace", Log.Trace} }; 

            serverFunctionContainer = new ServerFunctionContainer() { ADUType = aduType, Emitters = emitters, UnitID = unitID, RTUAddr = unitID, MBAPUnitID = unitID, RespondToAllTargets = responseToAllUnits };

            FlushPeriod = (portBehavior.IsDatagramPort ? TimeSpan.FromSeconds(0.0) : TimeSpan.FromSeconds(0.1));

            portReadAction = port.CreateReadAction(portReadActionParam = new ReadActionParam() { WaitForAllBytes = false });
            portWriteAction = port.CreateWriteAction(portWriteActionParam = new WriteActionParam());
            portFlushAction = port.CreateFlushAction(FlushPeriod);

            portReadAction.NotifyOnComplete.AddItem(threadWakeupNotifier);
            portWriteAction.NotifyOnComplete.AddItem(threadWakeupNotifier);
            portFlushAction.NotifyOnComplete.AddItem(threadWakeupNotifier);

            port.BaseStateNotifier.NotificationList.AddItem(threadWakeupNotifier);

            AddExplicitDisposeAction(() => Fcns.DisposeOfObject(ref port));
        }

        /// <summary>
        /// Catch StopPart at this level and use it to also stop the port.
        /// </summary>
        protected override void PreStopPart()
        {
            port.StopPart();
            base.PreStopPart();
        }

        #endregion

        #region internals

        IModbusFCServer fcServer = null;
        IPort port = null;
        ISequencedObjectSourceObserver<IBaseState> portBaseStateObserver = null;

        ServerFunctionContainer serverFunctionContainer = null;

        IReadAction portReadAction = null;
        ReadActionParam portReadActionParam = null;
        IWriteAction portWriteAction = null;
        WriteActionParam portWriteActionParam = null;
        IFlushAction portFlushAction = null;

        #endregion

        #region

        /// <summary>Get/Set property defines the maximum time between the arrival of the first byte of a Modbus request and the completion of the reception of the entire request.</summary>
        public TimeSpan Timeout { get; set; }
        /// <summary>Get/Set property defines the nominal flush period that is used after any failed transation</summary>
        public TimeSpan FlushPeriod { get; set; }

        /// <summary>Provides the implementation of the Standard GoOnline Action.  Runs a pass through GoOnline action on the contained Port object.</summary>
        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            return InnerRunRelayAction(port.CreateGoOnlineAction(andInitialize), "GoOnline" + (andInitialize ? "+Init" : ""));
        }

        /// <summary>Provides the implementation of the Standard GoOffline Action.  Runs a pass through GoOffline action on the contained Port object.</summary>
        protected override string PerformGoOfflineAction()
        {
            return InnerRunRelayAction(port.CreateGoOfflineAction(), "GoOffline"); ;
        }

        /// <summary>Provides the implementation of the Standard Service Action.  Runs a pass through Service action on the contained Port object.</summary>
        protected override string PerformServiceAction(string serviceName)
        {
            return InnerRunRelayAction(port.CreateServiceAction(serviceName), Fcns.CheckedFormat("ServiceAction({0})", serviceName));
        }

        private string InnerRunRelayAction(IBasicAction portAction, string description)
        {
            portAction.NotifyOnComplete.AddItem(threadWakeupNotifier);

            string resultCode = Fcns.MapNullOrEmptyTo(portAction.Start(), null);

            while (resultCode == null)
            {
                WaitForSomethingToDo();

                InnerServiceFCServerAndStateRelay();

                if (portAction.ActionState.IsComplete)
                    break;
            }

            portAction.NotifyOnComplete.RemoveItem(threadWakeupNotifier);

            resultCode = (portAction.ActionState.ResultCode ?? Fcns.CheckedFormat("Internal: port.{0} complete with null ResultCode", description));

            return resultCode;
        }

        QpcTimeStamp bufferFillStartTime = QpcTimeStamp.Zero;

        /// <summary>
        /// Provides the server specific version of the Part's Main Loop Service method.
        /// </summary>
        protected override void PerformMainLoopService()
        {
            InnerServiceFCServerAndStateRelay();

            bool portIsConnected = portBaseStateObserver.Object.IsConnected;

            if (portWriteAction.ActionState.IsPendingCompletion || portFlushAction.ActionState.IsPendingCompletion)
            {
                // we cannot service a new request until the write and/or flush from a prior service loop have completed.
                return;
            }

            bool startFlush = false;
            bool startWrite = false;

            if (portIsConnected && portReadAction.ActionState.CanStart)
            {
                if (portReadAction.ActionState.IsComplete)
                {
                    serverFunctionContainer.requestAdu.PktBuf.numBytes = portReadActionParam.BytesRead;

                    string ec = null;
                    TimeSpan bufferAge = bufferFillStartTime.Age;
                    if (serverFunctionContainer.AttemptToDecodeRequestPkt(out ec))
                    {
                        if (String.IsNullOrEmpty(ec))
                        {
                            Log.Trace.Emit("Attempting to perform request ADU:{0}", serverFunctionContainer.requestAdu);

                            if (serverFunctionContainer.ServiceDecodedRequest(fcServer))
                                startWrite = true;
                            else
                                Log.Trace.Emit("Decoded request produced no response [ADU:{0}]", serverFunctionContainer.requestAdu);
                        }
                        else
                        {
                            Log.Error.Emit("Invalid request received: {0} [numBytes:{1}]", ec, portReadActionParam.BytesRead);
                        }

                        portReadActionParam.BytesRead = 0;
                    }
                    else if (portReadActionParam.BytesRead > 0)
                    {
                        if (port.PortBehavior.IsDatagramPort)
                        {
                            Log.Warning.Emit("Invalid partial request received from datagram port.  Discarding {0} bytes", portReadActionParam.BytesRead);

                            portReadActionParam.BytesRead = 0;
                        }
                        else if (bufferAge > Timeout)
                        {
                            Log.Warning.Emit("Incomplete Partial Request timeout after {0:f3} seconds.  Discarding {0} bytes", bufferAge.TotalSeconds, portReadActionParam.BytesRead);

                            portReadActionParam.BytesRead = 0;
                        }
                    }
                    else
                    {
                        Log.Trace.Emit("Empty read completed");
                    }
                }

                if (!startFlush)
                {
                    // start the read immediately even if we are also starting a write (keep the interface primed)
                    if (portReadActionParam.BytesRead == 0)
                        bufferFillStartTime = QpcTimeStamp.Now;

                    if (portReadActionParam.Buffer == null)
                    {
                        portReadActionParam.Buffer = serverFunctionContainer.requestAdu.PktBuf.bytes;
                        portReadActionParam.BytesToRead = serverFunctionContainer.requestAdu.PktBuf.bytes.Length;
                    }

                    portReadAction.Start();
                }
            }
            else if (portReadAction.ActionState.IsComplete && portReadActionParam.BytesRead > 0)
            {
                Log.Debug.Emit("Discarding {0} bytes of read data: port is no longer connected");
                portReadActionParam.BytesRead = 0;
            }

            if (!portIsConnected)
            { }
            else if (startWrite)
            {
                Log.Trace.Emit("Writing response ADU:{0}", serverFunctionContainer.responseAdu);

                portWriteActionParam.Buffer = serverFunctionContainer.responseAdu.PktBuf.bytes;
                portWriteActionParam.BytesToWrite = serverFunctionContainer.responseAdu.PktBuf.numBytes;
                portWriteActionParam.BytesWritten = 0;

                portWriteAction.Start();
            }
            else if (startFlush)
            {
                portFlushAction.Start();
            }
        }

        private void InnerServiceFCServerAndStateRelay()
        {
            if (portBaseStateObserver.IsUpdateNeeded)
            {
                portBaseStateObserver.Update();
                SetBaseState(portBaseStateObserver.Object, "Republishing from port", true);
            }

            fcServer.Service();

        }

        #endregion
    }

    #endregion

    //--------------------------------------------------------------------------
}
