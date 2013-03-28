//-------------------------------------------------------------------
/*! @file HARTBusMaster.cs
 *  @brief Defines a primary class and supporting interfaces and classes to provide a SerialIO Bus Master Driver class for use in communicating with HART devices
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
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

namespace MosaicLib.PartsLib.Protocols.HART
{
    using System;
    using System.Collections.Generic;

    #region Individual Commands

    namespace Commands
    {
        public class ReadUniqueIdentifierCommand : BusMasterCommandBase
        {
            public DeviceUniqueIDInfo UniqueID { get; private set; }

            public ReadUniqueIdentifierCommand() : this(new PacketAddressInfo()) { }
            public ReadUniqueIdentifierCommand(PacketAddressInfo addrInfo) : this(CommandCode.ReadUniqueIdentifier, addrInfo) {}
            protected ReadUniqueIdentifierCommand(CommandCode commandCode, PacketAddressInfo addrInfo)
                : base(commandCode, addrInfo)
            {
                UniqueID = new DeviceUniqueIDInfo();
            }

            protected internal override string DecodeResponse()
            {
                string decodeResult = UniqueID.DecodeFrom(SlaveToMasterPacketInfo.Data, 0);

                if (!string.IsNullOrEmpty(decodeResult))
                    decodeResult = Utils.Fcns.CheckedFormat("Cmd{0}: DecodeResponse failed: {1}", (byte) MasterToSlavePacketInfo.CommandCode, decodeResult);

                return decodeResult;
            }
        }

        public struct Variable
        {
            public byte UnitsSelect { get; set; }
            public float Value { get; set; }
        }

        public class ReadPrimaryVariableCommand : BusMasterCommandBase
        {
            public Variable Variable { get; private set; }

            public ReadPrimaryVariableCommand() : this(new PacketAddressInfo()) { }
            public ReadPrimaryVariableCommand(PacketAddressInfo addrInfo)
                : base(CommandCode.ReadPrimaryVariable, addrInfo)
            {
            }

            protected internal override string DecodeResponse()
            {
                byte [] responseData = SlaveToMasterPacketInfo.Data;
                if (responseData == null || responseData.Length < 5)
                    return Utils.Fcns.CheckedFormat("Cmd{0}: DecodeResponse failed: response data missing or insufficient size", (byte)MasterToSlavePacketInfo.CommandCode);

                Variable v = new Variable();

                v.UnitsSelect = responseData[0];

                Utils.Data.ChangeByteOrder(responseData, 1, 4, MosaicLib.Utils.Data.ByteOrder.BigEndian);
                v.Value = BitConverter.ToSingle(responseData, 1);

                Variable = v;

                return String.Empty;
            }
        }

        public class ReadCurrentAndAllDynamicVariables : BusMasterCommandBase
        {
            public Variable CurrentVariable { get; private set; }
            public Variable[] DynamicVariables { get { return dynamicVariables; } }

            private Variable[] dynamicVariables = new Variable [0];

            public ReadCurrentAndAllDynamicVariables() : this(new PacketAddressInfo()) { }
            public ReadCurrentAndAllDynamicVariables(PacketAddressInfo addrInfo)
                : base(CommandCode.ReadCurrentAndAllDynamicVariables, addrInfo)
            {
            }

            protected internal override string DecodeResponse()
            {
                byte[] responseData = SlaveToMasterPacketInfo.Data;
                if (responseData == null || responseData.Length < 4)
                    return Utils.Fcns.CheckedFormat("Cmd{0}: DecodeResponse failed: response data missing or insufficient size", (byte)MasterToSlavePacketInfo.CommandCode);

                Variable v = new Variable();
                v.UnitsSelect = 0;
                Utils.Data.ChangeByteOrder(responseData, 0, 4, MosaicLib.Utils.Data.ByteOrder.BigEndian);
                v.Value = BitConverter.ToSingle(responseData, 0);

                CurrentVariable = v;

                int scanIdx = 4;
                int remainingLength = responseData.Length - scanIdx;
                int variableCount = (remainingLength / 5);
                if (remainingLength != (variableCount * 5))
                    return Utils.Fcns.CheckedFormat("Cmd{0}: DecodeResponse failed: response data size is invalid: found partial variable", (byte)MasterToSlavePacketInfo.CommandCode);

                Array.Resize<Variable>(ref dynamicVariables, variableCount);

                for (int varIdx = 0; varIdx < variableCount; varIdx++, scanIdx += 5)
                {
                    v.UnitsSelect = responseData[scanIdx];
                    Utils.Data.ChangeByteOrder(responseData, scanIdx + 1, 4, MosaicLib.Utils.Data.ByteOrder.BigEndian);
                    v.Value = BitConverter.ToSingle(responseData, scanIdx + 1);

                    dynamicVariables[varIdx] = v;
                }

                return String.Empty;
            }
        }

        public class ReadUniqueIdentifierByTagCommand : ReadUniqueIdentifierCommand
        {
            public string Tag { get { return tag; } set { tag = value ?? string.Empty; MasterToSlavePacketInfo.SetTagName(tag); } }
            private string tag = string.Empty;

            public ReadUniqueIdentifierByTagCommand() : this(string.Empty) { }
            public ReadUniqueIdentifierByTagCommand(string findTagName)
                : base(CommandCode.ReadUniqueIdentifierAssociatedWithTag, PacketAddressInfo.Broadcast)
            {
                Tag = findTagName;
            }
        }

        public class ReadMessageCommand : BusMasterCommandBase
        {
            public string Message { get; private set; }

            public ReadMessageCommand() : this(new PacketAddressInfo()) { }
            public ReadMessageCommand(PacketAddressInfo addrInfo)
                : base(CommandCode.ReadMessage, addrInfo)
            {
                Message = String.Empty;
            }

            protected internal override string DecodeResponse()
            {
                byte [] responseData = SlaveToMasterPacketInfo.Data;
                if (responseData == null)
                    return Utils.Fcns.CheckedFormat("Cmd{0}: DecodeResponse failed: response data missing or insufficient size", (byte)MasterToSlavePacketInfo.CommandCode);

                Message = SlaveToMasterPacketInfo.DecodePackedASCII(0, responseData.Length) ?? string.Empty;

                return String.Empty;
            }
        }
        public class ReadTagDescriptorDateCommand : BusMasterCommandBase
        {
            public string Tag { get; private set; }
            public string Descriptor { get; private set; }
            public string Date { get; private set; }

            public ReadTagDescriptorDateCommand() : this(new PacketAddressInfo()) { }
            public ReadTagDescriptorDateCommand(PacketAddressInfo addrInfo)
                : base(CommandCode.ReadTagDescriptorDate, addrInfo)
            {
                Tag = Descriptor = Date = String.Empty;
            }

            protected internal override string DecodeResponse()
            {
                byte[] responseData = SlaveToMasterPacketInfo.Data;
                if (responseData == null || responseData.Length != 21)
                    return Utils.Fcns.CheckedFormat("Cmd{0}: DecodeResponse failed: response data missing or insufficient size", (byte)MasterToSlavePacketInfo.CommandCode);

                Tag = SlaveToMasterPacketInfo.DecodePackedASCII(0, 6) ?? string.Empty;
                Descriptor = SlaveToMasterPacketInfo.DecodePackedASCII(6, 12) ?? string.Empty;
                Date = SlaveToMasterPacketInfo.DecodePackedASCII(18, 3) ?? string.Empty;

                return String.Empty;
            }
        }

        public class ReadFinalAssemblyNumberCommand : BusMasterCommandBase
        {
            public UInt32 FinalAssemblyNumber { get; private set; }

            public ReadFinalAssemblyNumberCommand() : this(new PacketAddressInfo()) { }
            public ReadFinalAssemblyNumberCommand(PacketAddressInfo addrInfo)
                : base(CommandCode.ReadFinalAssemblyNumber, addrInfo)
            {
            }

            protected internal override string DecodeResponse()
            {
                byte[] responseData = SlaveToMasterPacketInfo.Data;
                UInt32 v = 0;

                if (responseData == null || responseData.Length < 3 || !Utils.Data.Pack(responseData, 0, 3, out v))
                    return Utils.Fcns.CheckedFormat("Cmd{0}: DecodeResponse failed: response data missing or insufficient size", (byte)MasterToSlavePacketInfo.CommandCode);

                FinalAssemblyNumber = v;

                return String.Empty;
            }
        }

        public class SetPrimaryVariableLowerRangeCommand : BusMasterCommandBase
        {
            public SetPrimaryVariableLowerRangeCommand(PacketAddressInfo addrInfo)
                : base(CommandCode.SetPrimaryVariableLowerRange, addrInfo)
            {
            }
        }

        public class ReadAdditionalTransmitterStatusCommand : BusMasterCommandBase
        {
            public byte[] Data { get { return SlaveToMasterPacketInfo.Data; } }

            public ReadAdditionalTransmitterStatusCommand() : this(new PacketAddressInfo()) { }
            public ReadAdditionalTransmitterStatusCommand(PacketAddressInfo addrInfo)
                : base(CommandCode.ReadAdditionalTransmitterStatus, addrInfo)
            {
            }

            protected internal override string DecodeResponse()
            {
                return base.DecodeResponse();
            }
        }
    }

    #endregion

    #region BusMasterCommandBase

    /// <summary>
    /// This class is the base class for all HART Bus Master Command objects such as the ReadUniqueIdentifierCommand.  
    /// This base class includes the actual code that is used to format and write commands as well as to incrementally receive and decode responses.
    /// This class works in conjunction with the BusMaster object that instantates a IPort and which provides the client usable RunCommand method that is used
    /// to actuate objects of this type.
    /// </summary>

    public class BusMasterCommandBase
    {
        public Packet MasterToSlavePacketInfo { get; private set; }
        public PacketAddressInfo MasterToSlaveAddrInfo { get { return MasterToSlavePacketInfo.PacketAddressInfo; } set { MasterToSlavePacketInfo.PacketAddressInfo = value; } }
        public Packet SlaveToMasterPacketInfo { get; private set; }
        public string ResultCode { get; protected internal set; }
        public TimeSpan ResponseTimeLimit { get; set; }
        public int MaximumRetries { get; set; }

        protected BusMasterCommandBase(CommandCode commandCode, PacketAddressInfo addrInfo) : this(new Packet((byte)commandCode, addrInfo)) { }
        protected BusMasterCommandBase(byte commandCode, PacketAddressInfo addrInfo) : this(new Packet(commandCode, addrInfo)) {}
        protected BusMasterCommandBase(Packet masterToSlavePacketInfo) 
        { 
            MasterToSlavePacketInfo = masterToSlavePacketInfo; 
            SlaveToMasterPacketInfo = new Packet(); 
            ResultCode = string.Empty; 
            ResponseTimeLimit = TimeSpan.Zero;      // use default value
            MaximumRetries = 0;
        }

        public TimeSpan RunTime
        {
            get
            {
                TimeSpan runTime = SlaveToMasterPacketInfo.TimeStamp - MasterToSlavePacketInfo.TimeStamp;
                return ((runTime > TimeSpan.Zero) ? runTime : TimeSpan.Zero);
            }
        }

        protected internal virtual string DecodeResponse()
        {
            return string.Empty;
        }

        protected internal byte[] masterToSlaveBuffer = new byte[Details.MaxTransmitCommandLength];
        protected internal int masterToSlaveBufferLen = 0;
        protected internal byte[] slaveToMasterBuffer = new byte[Details.MaxRecieveReplyLength + 1];    // always allow space for an extra char so that we can detect unexpectedly long packets even for full length packets.
        protected internal int slaveToMasterBufferLen = 0;

        private SerialIO.IPort sp = null;
        protected internal SerialIO.IPort SP 
        { 
            get { return sp; } 
            set 
            {
                if (!Object.ReferenceEquals(sp, value))
                {
                    sp = value;
                    spWriteAction = ((sp != null) ? sp.CreateWriteAction(spWriteActionParam) : null);
                    spReadAction = ((sp != null) ? sp.CreateReadAction(spReadActionParam) : null);
                }
            } 
        }
        protected internal SerialIO.WriteActionParam spWriteActionParam = new MosaicLib.SerialIO.WriteActionParam();
        protected internal SerialIO.IWriteAction spWriteAction = null;
        protected internal Time.QpcTimeStamp spWriteCompleteTime = Time.QpcTimeStamp.Zero;
        protected internal SerialIO.ReadActionParam spReadActionParam = new MosaicLib.SerialIO.ReadActionParam();
        protected internal SerialIO.IReadAction spReadAction = null;

        protected internal void SetSucceeded()
        {
            ResultCode = String.Empty;
        }

        protected internal void SetFailed(string reason)
        {
            if (String.IsNullOrEmpty(ResultCode))
                ResultCode = reason;
        }

        protected internal string RunCmdWrite()
        {
            masterToSlaveBufferLen = 0;
            slaveToMasterBufferLen = 0;

            for (int preambles = 0; preambles < Details.DefaultMinPreambleBytesToSend; preambles++)
                masterToSlaveBuffer[masterToSlaveBufferLen++] = Details.PreambleByte;

            int startCharIdx = masterToSlaveBufferLen;
            masterToSlaveBuffer[masterToSlaveBufferLen++] = MasterToSlavePacketInfo.PacketAddressInfo.StartChar;

            if (MasterToSlavePacketInfo.PacketAddressInfo.AddressType == AddressType.Short_1Byte)
                masterToSlaveBuffer[masterToSlaveBufferLen++] = MasterToSlavePacketInfo.PacketAddressInfo.PackShortAddr();
            else
            {
                MasterToSlavePacketInfo.PacketAddressInfo.PackLongAddr(masterToSlaveBuffer, masterToSlaveBufferLen);
                masterToSlaveBufferLen += 5;
            }

            masterToSlaveBuffer[masterToSlaveBufferLen++] = MasterToSlavePacketInfo.CommandCode;
            byte dataCount = MasterToSlavePacketInfo.DataLenByte;
            byte [] data = MasterToSlavePacketInfo.Data;
            masterToSlaveBuffer[masterToSlaveBufferLen++] = dataCount;

            for (byte cmdDataIdx = 0; cmdDataIdx < dataCount; cmdDataIdx++)
                masterToSlaveBuffer[masterToSlaveBufferLen++] = data[cmdDataIdx];

            // build checksum
            byte checkSumByte = 0x00;
            for (int checkSumIdx = startCharIdx; checkSumIdx < masterToSlaveBufferLen; checkSumIdx++)
            {
                checkSumByte = (byte) (checkSumByte ^ masterToSlaveBuffer[checkSumIdx]);
            }

            masterToSlaveBuffer[masterToSlaveBufferLen++] = checkSumByte;

            // capture timestamp
            MasterToSlavePacketInfo.TimeStamp = Time.QpcTimeStamp.Now;

            // update WriteActionParms
            spWriteActionParam.Reset();
            spWriteActionParam.Buffer = masterToSlaveBuffer;
            spWriteActionParam.BytesToWrite = masterToSlaveBufferLen;

            // run it.
            string ec = ((spWriteAction != null) ? spWriteAction.Run() : "WriteFailed: No SerialPort Defined");

            spWriteCompleteTime = Time.QpcTimeStamp.Now;

            if (!String.IsNullOrEmpty(ec))
                SetFailed(ec);

            return ec;
        }

        protected internal bool RunIncrementalCmdResponseRead(ref string ecOut)
        {
            Time.QpcTimeStamp timeoutAfter = spWriteCompleteTime + ResponseTimeLimit;
            TimeSpan remainingTime = timeoutAfter - Time.QpcTimeStamp.Now;

            spReadActionParam.Reset();
            spReadActionParam.Buffer = slaveToMasterBuffer;
            spReadActionParam.BytesRead = slaveToMasterBufferLen;
            spReadActionParam.BytesToRead = slaveToMasterBuffer.Length;     // we will be happy to stop before this as well
            spReadActionParam.WaitForAllBytes = false;

            string ec = ((spReadAction != null) ? spReadAction.Run() : "ReadFailed: No SerialPort Defined");

            TimeSpan runTime = spReadActionParam.StartTime.Age;

            if (spReadActionParam.BytesRead != slaveToMasterBufferLen)
            {
                slaveToMasterBufferLen = spReadActionParam.BytesRead;

                string decodeEC = null;
                if (AttemptToDecodeAndVerifyCmdResponse(ref decodeEC))
                {
                    ec = decodeEC ?? ec;

                    SlaveToMasterPacketInfo.TimeStamp = Time.QpcTimeStamp.Now;

                    TimeSpan spReadActionRunTime = SlaveToMasterPacketInfo.TimeStamp - spReadActionParam.StartTime;

                    ecOut = ec;

                    return true;
                }
            }
            else
            {
                switch (spReadActionParam.ActionResultEnum)
                {
                    case MosaicLib.SerialIO.ActionResultEnum.ReadDone:
                    case MosaicLib.SerialIO.ActionResultEnum.ReadTimeout:
                        ec = String.Empty;
                        // neither of these cases are errors at this level
                        break;
                    default:
                        // all other cases are errors
                        if (String.IsNullOrEmpty(ec))
                            ec = "??" + spReadActionParam.ResultCode;
                        break;
                }
            }

            if (String.IsNullOrEmpty(ec) && remainingTime < TimeSpan.Zero)
            {
                if (slaveToMasterBufferLen == 0)
                    ec = "Timeout waiting for any response from target device(s)";
                else
                    ec = "Timeout waiting for complete response from target device(s)";

                SlaveToMasterPacketInfo.TimeStamp = Time.QpcTimeStamp.Now;
            }

            bool cmdFailed = !String.IsNullOrEmpty(ecOut = ec);
            return cmdFailed;
        }

        protected bool AttemptToDecodeAndVerifyCmdResponse(ref string ec)
        {
            if (slaveToMasterBufferLen == 0)
                return false;       // keep waiting for (more) data

            int scanIdx = 0;

            // verify that we have at least one preamble byte
            if (slaveToMasterBuffer[0] != Details.PreambleByte)
            {
                ec = "DecodeResponse failed: no preamble byte(s) found";
                return true;
            }

            // skip preambles...
            for (;scanIdx < slaveToMasterBufferLen && scanIdx < Details.MaxPreambleBytes; scanIdx++)
            {
                if (slaveToMasterBuffer[scanIdx] != Details.PreambleByte)
                    break;
            }

            if (scanIdx >= slaveToMasterBufferLen)
                return false;

            if (slaveToMasterBuffer[scanIdx] == Details.PreambleByte)
            {
                ec = "DecodeResponse failed: too many preamble bytes found";
                return true;
            }

            int startCharIdx = scanIdx;

            byte startByte = SlaveToMasterPacketInfo.PacketAddressInfo.StartChar = slaveToMasterBuffer[scanIdx++];

            if (SlaveToMasterPacketInfo.PacketAddressInfo.MasterAddr != MasterToSlavePacketInfo.PacketAddressInfo.MasterAddr)
            {
                ec = Utils.Fcns.CheckedFormat("DecodeResponse failed: Unexpected MasterAddr in start char:{0:x2} ", startByte);
                return true;
            }

            int rxAddrLen = ((SlaveToMasterPacketInfo.PacketAddressInfo.AddressType == AddressType.Short_1Byte) ? 1 : 5);

            // first minimum length test: cannot even look until we have enough data for a valid packet that has no data

            int minNoDataReplyBytes = 1 + rxAddrLen + 1 + 1 + 0 + 1;

            if (slaveToMasterBufferLen < startCharIdx + minNoDataReplyBytes)
                return false;       // keep waiting for more data

            switch (SlaveToMasterPacketInfo.PacketAddressInfo.AddressType)
            {
                case AddressType.Short_1Byte: SlaveToMasterPacketInfo.PacketAddressInfo.UnpackShortAddr(slaveToMasterBuffer[scanIdx++]); break;
                case AddressType.Long_5Bytes: SlaveToMasterPacketInfo.PacketAddressInfo.UnpackLongAddr(slaveToMasterBuffer, scanIdx); scanIdx += 5; break;
                default:
                    ec = Utils.Fcns.CheckedFormat("DecodeResponse failed: invalid start char:${0:x2}", startByte);
                    return true;
            }

            SlaveToMasterPacketInfo.CommandCode = slaveToMasterBuffer[scanIdx++];

            byte dataLenWithStatus = slaveToMasterBuffer[scanIdx++];
            int dataLen = dataLenWithStatus - 2;
            if (dataLen < 0)
            {
                ec = Utils.Fcns.CheckedFormat("DecodeResponse failed: invalid byte count:{0}, response packets must contain status", dataLenWithStatus);
                return true;
            }
            else if (dataLen > 24)
            {
                ec = Utils.Fcns.CheckedFormat("DecodeResponse failed: byte count:{0} exceeds upper limit of 24 + status", dataLenWithStatus);
                return true;
            }

            int minReplyBytesWithData = minNoDataReplyBytes + dataLenWithStatus;      // which includes the 2 bytes of status

            if (slaveToMasterBufferLen < startCharIdx + minReplyBytesWithData)
                return false;       // keep waiting

            if (scanIdx >= slaveToMasterBufferLen)
                return false;

            SlaveToMasterPacketInfo.StatusBytes.ExtractFrom(slaveToMasterBuffer, scanIdx);
            scanIdx += 2;

            byte [] data = SlaveToMasterPacketInfo.Data;
            System.Array.Resize<byte>(ref data, dataLen);
            SlaveToMasterPacketInfo.Data = data;

            if (dataLen > 0)
            {
                System.Buffer.BlockCopy(slaveToMasterBuffer, scanIdx, data, 0, dataLen);
                scanIdx += dataLen;
            }

            // verify that this is the end of the packet
            byte receivedChecksum = slaveToMasterBuffer[scanIdx++];

            if (slaveToMasterBufferLen > scanIdx)
            {
                ec = Utils.Fcns.CheckedFormat("DecodeResponse failed: response is longer that expected: {0} > {1}", slaveToMasterBufferLen, minReplyBytesWithData);
                return true;
            }

            // now check the checksum byte
            byte calculatedChecksum = 0x00;

            for (int checksumIdx = startCharIdx; checksumIdx < scanIdx; checksumIdx++)
                calculatedChecksum = (byte)(calculatedChecksum ^ slaveToMasterBuffer[checksumIdx]);

            if (calculatedChecksum != 0)
            {
                ec = Utils.Fcns.CheckedFormat("DecodeResponse failed: checksum error: packet longitudinal sum:${0:x2} != $00", calculatedChecksum);
                return true;
            }

            ec = DecodeResponse();

            return true;
        }
    }

    #endregion

    #region BusMaster

    /// <summary>
    /// 
    /// </summary>

    public class BusMaster : Modular.Part.SimplePartBase
    {
        public BusMaster(string name, string targetSpecStr, bool autoConnect)
            : base(name, "HART.BusMaster")
        {
            portConfig = new SerialIO.PortConfig(name + ".sp", targetSpecStr, MosaicLib.SerialIO.LineTerm.None);
            portConfig.EnableAutoReconnect = autoConnect;
            portConfig.SpinWaitTimeLimit = TimeSpan.FromSeconds(0.001);
            portConfig.TraceDataLoggerGroupID = "LGID.Trace";
            portConfig.TraceDataMesgType = Logging.MesgType.Trace;
            portConfig.TraceMesgType = Logging.MesgType.Trace;
            portConfig.DebugMesgType = Logging.MesgType.Trace;

            try
            {
                sp = SerialIO.Factory.CreatePort(portConfig);
            }
            catch (System.Exception ex)
            {
                Log.Error.Emit("CreatePort failed: {0}", ex.Message);
                sp = SerialIO.Factory.CreatePort(new MosaicLib.SerialIO.PortConfig(name + ".sp", "<NullPort/>", MosaicLib.SerialIO.LineTerm.None));
            }

            spFlushAction = sp.CreateFlushAction(TimeSpan.FromSeconds(0.100));
        }

        protected override void Dispose(MosaicLib.Utils.DisposableBase.DisposeType disposeType)
        {
            if (disposeType == DisposeType.CalledExplicitly)
                Utils.Fcns.DisposeOfObject(ref sp);

            spFlushAction = null;
        }

        public string Connect()
        {
            if (!BaseState.IsOnline)
                SetBaseState(Modular.Part.UseState.AttemptOnline, "Attempting Connect while Offline", true);

            Modular.Part.IBaseState spBaseState = sp.BaseState;
            if (!spBaseState.IsConnected)
                SetBaseState(Modular.Part.ConnState.Connecting, "Attempting to Connect", true);

            Modular.Part.IBasicAction spGoOnlineAction = sp.CreateGoOnlineAction();
            string ec = spGoOnlineAction.Run();

            if (String.IsNullOrEmpty(ec))
                SetBaseState(Modular.Part.UseState.Online, Modular.Part.ConnState.Connected, "Connect complete", true);
            else if (BaseState.UseState == MosaicLib.Modular.Part.UseState.AttemptOnline)
                SetBaseState(Modular.Part.UseState.AttemptOnlineFailed, Modular.Part.ConnState.ConnectFailed, "Connect failed:" + ec, true);
            else
                SetBaseState(Modular.Part.ConnState.ConnectFailed, "Connect failed:" + ec, true);

            return ec;
        }

        public string Disconnect()
        {
            Modular.Part.IBaseState spBaseState = sp.BaseState;

            Modular.Part.IBasicAction spGoOfflineAction = sp.CreateGoOfflineAction();

            // warning: what to do if the sp has not been started at this point...
            string ec = null;

            if (sp.IsRunning)
                ec = spGoOfflineAction.Run();
            else
                Log.Debug.Emit("Serial Port does not appear to have been started: skipping GoOffline action [{0}]", sp.BaseState);

            if (String.IsNullOrEmpty(ec))
                SetBaseState(Modular.Part.ConnState.Disconnected, "Disconnect completed", true);
            else
                SetBaseState(Modular.Part.ConnState.Disconnected, "Disconnect failed:" + ec, true);

            return ec;
        }

        public string Reset()
        {
            string ec = Disconnect();
            if (String.IsNullOrEmpty(ec))
                ec = Connect();

            return ec;
        }

        public bool IsConnected { get { return sp.BaseState.IsConnected; } }

        public bool ServiceConnectionState() { return ServiceConnectionState(false); }
        public bool ServiceConnectionState(bool autoConnect)
        {
            Modular.Part.IBaseState spBaseState = sp.BaseState;

            if (BaseState.ConnState != spBaseState.ConnState)
                SetBaseState(spBaseState.ConnState, "SP ConnState changed", true);

            if (autoConnect && portConfig.EnableAutoReconnect && !spBaseState.IsConnected)
                Connect();

            return BaseState.IsOnline && sp.BaseState.IsConnected;
        }

        public TimeSpan DefaultResponseTimeLimit = TimeSpan.FromSeconds(0.200);

        public bool RunCommand(BusMasterCommandBase cmd)
        {
            if (cmd == null)
                return (lastCmdSuccess = false);

            string ec = null;

            if (!ServiceConnectionState(true))
                ec = "SerialPort is not connected or Part is not Online";

            if (string.IsNullOrEmpty(ec))
            {
                if (!lastCmdSuccess)
                    ec = spFlushAction.Run();

                if (cmd.SP != sp)
                {
                    cmd.SP = sp;        // update commands serial port if needed.
                    if (cmd.ResponseTimeLimit == TimeSpan.Zero)
                        cmd.ResponseTimeLimit = DefaultResponseTimeLimit;
                }

                if (String.IsNullOrEmpty(ec))
                    ec = cmd.RunCmdWrite();

                while (String.IsNullOrEmpty(ec))
                {
                    if (cmd.RunIncrementalCmdResponseRead(ref ec))
                        break;
                }
            }

            if (String.IsNullOrEmpty(ec))
            {
                cmd.SetSucceeded();
                return lastCmdSuccess = true;
            }
            else
            {
                cmd.SetFailed(ec);
                return lastCmdSuccess = false;
            }
        }

        public bool RunCommands(List<BusMasterCommandBase> cmdList, bool stopOnFirstError, ref string firstEC)
        {
            bool success = true;

            foreach (BusMasterCommandBase cmd in cmdList)
            {
                success &= RunCommand(cmd);

                if (!string.IsNullOrEmpty(cmd.ResultCode))
                {
                    firstEC = String.IsNullOrEmpty(firstEC) ? cmd.ResultCode : firstEC;
                    if (stopOnFirstError)
                        break;
                }
            }

            return success;
        }

        private SerialIO.PortConfig portConfig;
        private SerialIO.IPort sp;
        private Modular.Part.IBasicAction spFlushAction = null;
        private bool lastCmdSuccess = false;
    }

    #endregion
}

//-------------------------------------------------------------------
