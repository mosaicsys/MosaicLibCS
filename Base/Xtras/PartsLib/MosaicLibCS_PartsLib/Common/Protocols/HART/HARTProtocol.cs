//-------------------------------------------------------------------
/*! @file HARTProtocol.cs
 *  @brief 
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
    /*
     * Please note: the information contained here is derived from a Brooks Instruments Installation and Operation Manual titled 
     * "S-Protocol Commnication Command Description"  for the "Brooks SLA Series Flow Meters and Controllers".  This document is identified
     * as Part Number: 541B037AHG and was obtained directly from the brooksinstrument.com web site as "x-dpt-s-protocol-slaseries-tmf-eng.pdf"
     * found using the following url: "http://brooksinstrument.com/documentation-a-downloads.html?task=search"
     * 
     * This information has not been direved from, or informed by, access to any HART Communications Foundation (HCF) documentation concerning the HART Protocol itself
     * and as such may not perfectly match with information in the related protcol specifications.
     * 
     * This document's use of the term HART is only to match the corresponding wording and documentation in the BrooksInstruments document from which it is derived.
     */

    /* General layout of a HART protocol packet:
     * a) preamble: 2 to 5 bytes of 0xff
     * b) StartChar: byte containing address type (long/short) and direction (M->S/S->M)
     * c) Addr: 1 or 5 bytes as short or long address.  ShortAddr is number from 1 to 15.  Long addr is 24 bit ID, 8 bit type, 5 bit vendor.  both long and short include Primary/Secondary master.  Long includes flag to say slave in burts mode.
     * d) Command: single character
     * e) Payload Byte count: single byte gives count of remaining status (0 or 2) and data bytes in packet.  Only S->M packets conain status.
     * f) Status: 2 bytes (only present for S->M packets)
     * g) Data: 0..24 bytes.
     * h) Checksum: bitwise XOR of all prior bytes from (b) StartChar to last Data byte.  XOR of prior bytes and checksum byte must be zero for valid received packets.
     */

    public static partial class Details
    {
        /// <summary>Preamble character: 0xff</summary>
        public const byte PreambleByte = 0xff;
        /// <summary>Default Minimum number of Preamble characters for master to send when issuing a request</summary>
        public const int DefaultMinPreambleBytesToSend = 5;
        /// <summary>Default Minimum number of Preamble characters to receive for any HART packet (from master or to master) - may not be enforced.</summary>
        public const int DefaultMinPreambleBytesToReceive = 2;

        public const int MaxPreambleBytes = 15;
        public const int MaxTransmitCommandLength = MaxPreambleBytes + 1 + 5 + 1 + 1 + 24 + 1;  // preambles, start, long addr, cmd, len, data, sum
        public const int MaxRecieveReplyLength = MaxPreambleBytes + 1 + 5 + 1 + 1 + 2 + 24 + 1;  // preambles, start, long addr, cmd, len, status, data, sum
    }

    /// <summary>Represents the information that is in an entire packet - includes decodable variants for long and short addressing.</summary>
    public class Packet
    {
        /// <summary>Qpc TimeStamp.  Set to send start time for transmit packets and set to receive complete time on received packets.</summary>
        public Time.QpcTimeStamp TimeStamp { get; set; }
        /// <summary>Address related header information</summary>
        public PacketAddressInfo PacketAddressInfo { get; set; }
        /// <summary>Command</summary>
        public byte CommandCode { get; set; }
        /// <summary>Status bytes (only in SlaveToMaster_ACK packets)</summary>
        public StatusBytes StatusBytes { get; set; }
        /// <summary>Parameter Data for given command.  May be null when empty.</summary>
        public byte[] Data { get; set; }
        /// <summary></summary>
        public byte DataLenByte { get { return ((Data == null) ? (byte) 0 : unchecked((byte)(Data.Length & 0x00ff))); } }

        public Packet() { PacketAddressInfo = new PacketAddressInfo(); StatusBytes = new StatusBytes(); Data = null; }
        public Packet(PacketAddressInfo addrInfo) { PacketAddressInfo = new PacketAddressInfo(addrInfo); StatusBytes = new StatusBytes(); Data = null; }
        public Packet(CommandCode commandCode, PacketAddressInfo addrInfo) : this((byte) commandCode, addrInfo) { }
        public Packet(byte commandCode, PacketAddressInfo addrInfo) { CommandCode = commandCode; PacketAddressInfo = new PacketAddressInfo(addrInfo); StatusBytes = new StatusBytes(); Data = null; }

        /// <summary></summary>
        public void SetTagName(String tagNameStr)
        {
            byte[] tagAsciiBytes = Utils.ByteArrayTranscoders.ByteStringTranscoder.Decode(tagNameStr);

            // force resize the array to 8 bytes in length
            byte[] data = Data;
            System.Array.Resize<byte>(ref data, 6);

            PackFrom4AsciiBytes(tagAsciiBytes, 0, ref data, 0);
            PackFrom4AsciiBytes(tagAsciiBytes, 4, ref data, 3);

            Data = data;
        }

        /// <summary></summary>
        public String DecodePackedASCII(int startIdx, int numPackedBytes)
        {
            int endIdx = startIdx + numPackedBytes;
            int outSize = unchecked(((numPackedBytes + 2) * 4) / 3);
            byte[] unpackedBytes = new byte[outSize];
            int outOffset = 0;

            while (startIdx < endIdx)
            {
                UnpackTo4AsciiBytes(Data, startIdx, ref unpackedBytes, outOffset);
                startIdx += 3;
                outOffset += 4;
            }

            return Utils.ByteArrayTranscoders.ByteStringTranscoder.Encode(unpackedBytes);
        }

        #region ASCII pack/unpack help methods

        /// <summary></summary>
        static void PackFrom4AsciiBytes(byte[] inAsciiArray, int inAsciiOffset, ref byte[] outPackedArray, int outPackedOffset)
        {
            if (inAsciiArray == null || (inAsciiArray.Length < (inAsciiOffset + 4)))
                Array.Resize<byte>(ref inAsciiArray, inAsciiOffset + 4);

            byte in0 = (byte)(inAsciiArray[inAsciiOffset++] & 0x3f);
            byte in1 = (byte)(inAsciiArray[inAsciiOffset++] & 0x3f);
            byte in2 = (byte)(inAsciiArray[inAsciiOffset++] & 0x3f);
            byte in3 = (byte)(inAsciiArray[inAsciiOffset++] & 0x3f);

            if (outPackedArray == null || (outPackedArray.Length < (outPackedOffset + 3)))
                Array.Resize<byte>(ref outPackedArray, outPackedOffset + 3);

            outPackedArray[outPackedOffset++] = (byte)(((in0 << 2) | (in1 >> 4)) & 0xff);       // take 6 from in0 and 2 from in1
            outPackedArray[outPackedOffset++] = (byte)(((in1 << 4) | (in2 >> 2)) & 0xff);       // take 4 from in1 and 4 from in2
            outPackedArray[outPackedOffset++] = (byte)(((in2 << 6) | (in3 >> 0)) & 0xff);       // take 2 from in2 and 6 from in3
        }

        /// <summary></summary>
        static void UnpackTo4AsciiBytes(byte[] inPackedArray, int inPackedOffset, ref byte[] outAsciiArray, int outAsciiOffset)
        {
            if (inPackedArray == null || (inPackedArray.Length < (inPackedOffset + 3)))
                Array.Resize<byte>(ref inPackedArray, inPackedOffset + 3);

            byte in0 = (byte)(inPackedArray[inPackedOffset++] & 0xff);
            byte in1 = (byte)(inPackedArray[inPackedOffset++] & 0xff);
            byte in2 = (byte)(inPackedArray[inPackedOffset++] & 0xff);

            if (outAsciiArray == null || (outAsciiArray.Length < (outAsciiOffset + 4)))
                Array.Resize<byte>(ref outAsciiArray, outAsciiOffset + 4);

            outAsciiArray[outAsciiOffset++] = MapUnpackedByte((in0 >> 2));                          // take top 6 bits from in0
            outAsciiArray[outAsciiOffset++] = MapUnpackedByte(((in0 & 0x03) << 4) | (in1 >> 4));    // take bottom 2 bits from in0 and top 4 bits from in1
            outAsciiArray[outAsciiOffset++] = MapUnpackedByte(((in1 & 0x0f) << 2) | (in2 >> 6));    // take bottom 4 bits of in1 and top 2 bits of in2
            outAsciiArray[outAsciiOffset++] = MapUnpackedByte(in2);                                 // take bottom 6 bits from in2
        }

        /// <summary></summary>
        static byte MapUnpackedByte(int inputByte)
        {
            return (byte)((inputByte & 0x3f) | (((inputByte & 0x20) != 0) ? 0x00 : 0x40));
        }

        #endregion
    }

    /// <summary>Represents the information that is held in the address type and data portions of a HART packet.</summary>
    public class PacketAddressInfo
    {
        /// <summary>MessageType: part of StartByte</summary>
        public MessageType MessageType { get; set; }
        /// <summary>AddressType: part of StartByte</summary>
        public AddressType AddressType { get; set; }
        /// <summary>MasterAddr: Part of first Address bytes</summary>
        public MasterAddr MasterAddr { get; set; }
        /// <summary>ShortSlaveAddr - contents only used if this is a ShortAddr_1Byte type of address</summary>
        public ShortSlaveAddr ShortSlaveAddr { get; set; }
        /// <summary>ManufactureID - part of first address byte in 5 byte Long address</summary>
        public byte ManufactureID { get; set; }        // only uses the bottom 5 bits - the rest are masked off/ignored or may cause a communication error.
        /// <summary>DeviceTypeID - part of second address byte in 5 byte Long address</summary>
        public byte DeviceTypeID { get; set; }
        /// <summary>DeviceID (serial number) - bytes 3..5 in 5 byte Long address.</summary>
        public UInt32 DeviceID { get; set; }           // only uses the bottom 24 bits - the rest are masked off/ignored or may cause a communication error.

        public override string ToString()
        {
            switch (AddressType)
            {
                case AddressType.Short_1Byte:
                    return Utils.Fcns.CheckedFormat("Short1:{0}", (byte) ShortSlaveAddr);
                case AddressType.Long_5Bytes:
                    return Utils.Fcns.CheckedFormat("Long5:Manu{0:x2},DType{1:x2},DevID{2:x6}", ManufactureID, DeviceTypeID, DeviceID); 
                default:
                    return Utils.Fcns.CheckedFormat("Unknown AddressType:{0}", AddressType);
            }
        }

        /// <summary>Returns a new PacketAddressInfo object that is configured to be used to send broadcast packets.</summary>
        public static PacketAddressInfo Broadcast { get { return new PacketAddressInfo() { AddressType = AddressType.Short_1Byte, ShortSlaveAddr = ShortSlaveAddr.Broadcast }; } }

        public PacketAddressInfo(DeviceUniqueIDInfo fromUniqueID) : this(fromUniqueID.ManufacturerIDCode, fromUniqueID.DeviceTypeCode, fromUniqueID.DeviceID) { }

        public PacketAddressInfo(byte manufactureID, byte deviceTypeID) : this(manufactureID, deviceTypeID, 0) { } 

        public PacketAddressInfo(byte manufactureID, byte deviceTypeID, UInt32 deviceID) : this() 
        {
            AddressType = AddressType.Long_5Bytes;
            ManufactureID = manufactureID;
            DeviceTypeID = deviceTypeID;
            DeviceID = deviceID;
        }

        public PacketAddressInfo()
        {
            MessageType = MessageType.MasterToSlave_STX;
            MasterAddr = MasterAddr.Primary;
            ShortSlaveAddr = ShortSlaveAddr.Reserved;
        }

        public PacketAddressInfo(PacketAddressInfo rhs)
        {
            MessageType = rhs.MessageType;
            AddressType = rhs.AddressType;
            MasterAddr = rhs.MasterAddr;
            ShortSlaveAddr = rhs.ShortSlaveAddr;
            ManufactureID = rhs.ManufactureID;
            DeviceTypeID = rhs.DeviceTypeID;
            DeviceID = rhs.DeviceID;
        }

        /// <summary></summary>
        public bool UnpackStartChar(byte startChar)
        {
            bool success = true;

            AddressType = (((startChar & (int)AddressType.Mask) == (int)AddressType.Long_5Bytes) ? AddressType.Long_5Bytes : AddressType.Short_1Byte);

            switch (startChar & (byte)MessageType.Mask)
            {
                case (byte)MessageType.MasterToSlave_STX: MessageType = MessageType.MasterToSlave_STX; break;
                case (byte)MessageType.SlaveToMaster_ACK: MessageType = MessageType.SlaveToMaster_ACK; break;
                default: MessageType = MessageType.Undefined; success = false; break;
            }

            success &= ((startChar & 0x38) == 0);  // verify the rest of the bits are zero

            return success;
        }

        /// <summary></summary>
        public byte StartChar
        {
            get { return unchecked((byte)((byte)AddressType | (byte)MessageType)); }
            set { UnpackStartChar(value); }
        }

        /// <summary></summary>
        public bool UnpackShortAddr(byte shortAddrByte)
        {
            bool success = true;

            MasterAddr = ((shortAddrByte & (int)MasterAddr.Mask) == (int)MasterAddr.Primary) ? MasterAddr.Primary : MasterAddr.Secondary;

            int maskedShortSlaveAddr = (shortAddrByte & (int)ShortSlaveAddr.Mask);
            ShortSlaveAddr = (ShortSlaveAddr)maskedShortSlaveAddr;
            if (ShortSlaveAddr == ShortSlaveAddr.Undefined)
                success = false;

            success &= ((shortAddrByte & 0x30) == 0);  // verify the rest of the bits are zero

            return success;
        }

        /// <summary></summary>
        public byte PackShortAddr()
        {
            return unchecked((byte)((byte)MasterAddr | (byte)ShortSlaveAddr));
        }

        /// <summary></summary>
        public bool UnpackLongAddr(byte[] bytes, int startIdx)
        {
            bool success = true;

            if (bytes == null || startIdx < 0 || (startIdx + 5) > bytes.Length)
            {
                MasterAddr = MasterAddr.Undefined; ManufactureID = 0; DeviceTypeID = 0; DeviceID = 0;
                return false;
            }

            byte b0 = bytes[startIdx++], b1 = bytes[startIdx++], b2 = bytes[startIdx++], b3 = bytes[startIdx++], b4 = bytes[startIdx++];

            MasterAddr = ((b0 & (int)MasterAddr.Mask) == (int)MasterAddr.Primary) ? MasterAddr.Primary : MasterAddr.Secondary;
            ManufactureID = (byte)(b0 & 0x1f);
            success &= ((b0 & 0x60) == 0);      // this code does not support slave burst mode (0x40)

            DeviceTypeID = b1;

            DeviceID = Utils.Data.Pack(0, b2, b3, b4);

            return success;
        }

        /// <summary></summary>
        public bool PackLongAddr(byte[] bytes, int putIdx)
        {
            bool success = true;
            bool slaveInBurstMode = false;

            if (bytes == null || putIdx < 0 || (putIdx + 5) > bytes.Length)
                return false;

            success &= ((ManufactureID & 0x1f) == 0);
            bytes[putIdx++] = (byte)((int)MasterAddr | (slaveInBurstMode ? 0x40 : 0x00) | (ManufactureID & 0x1f));

            success &= ((DeviceID & 0xffffff) == 0);
            success &= Utils.Data.Unpack(DeviceID, bytes, putIdx);

            bytes[putIdx] = DeviceTypeID;

            return success;
        }
    }

    /// <summary>Address Type field enum: Short_1Byte = 0x00, Long_5Bytes = 0x80.</summary>
    public enum AddressType : byte
    {
        Short_1Byte = 0x00,
        Long_5Bytes = 0x80,
        Mask = 0x80,
        Undefined = 0xff,
    }

    /// <summary>Message Type field enum: MasterToSlave_STX = 0x02, SlaveToMaster_ACK = 0x06</summary>
    public enum MessageType : byte
    {
        Undefined = 0,
        MasterToSlave_STX = 0x02,
        SlaveToMaster_ACK = 0x06,
        Mask = 0x07,
    }

    /// <summary>Master Addr field enum: Primary = 0x80, Secondary = 0x00</summary>
    public enum MasterAddr : byte
    {
        Secondary = 0x00,
        Primary = 0x80,
        Mask = 0x80,
        Undefined = 0xff,
    }

    /// <summary>Short Slave Address field enum: Reserved=0x00, ID1 = 0x01, ... ID15 = 0x0f</summary>
    public enum ShortSlaveAddr : byte
    {
        Broadcast = 0x00,
        Reserved = 0x00,
        Undefined = 0x00,
        ID1 = 0x01, ID2, ID3, ID4, ID5, ID6, ID7, ID8, ID9, ID10, ID11, ID12, ID13, ID14, ID15,
        Mask = 0x0f,
    }

    /// <summary>Enumerated values for some Status Error Codes: covers some values that may be passed in first byte of StatusBytes</summary>
    public enum StatusErrorCode : ushort
    {
        NoError = 0,
        CommunicationError = 0x80,
        Undefined = 0x01,
        InvalidSelection = 0x02,
        PassedParameterTooLarge = 0x03,
        PassedParameterTooSmall = 0x04,
        IncorrectByteCount = 0x05,
        TransmitterSpecificCommandError = 0x06,
        InWriteProtectMode = 0x07,
        CommandSpecificError = 0x08,    // through 0x0f
        AccessRestricted = 0x10,
        DeviceIsBusy = 0x20,
        CommandNotImplemented = 0x40,

        DeviceMalfunction = 0xf001,
        OtherError = 0xf002,
    }

    /// <summary>struct used to decode and interpret the two byte status field that is included in all SlaveToMaster_ACK packets.</summary>
    public struct StatusBytes
    {
        public bool ExtractFrom(byte[] bytes, int startIdx)
        {
            B0 = (byte) StatusErrorCode.Undefined; B1 = 0;
            if (bytes == null || (startIdx + 2) > bytes.Length)
                return false;

            B0 = bytes[startIdx++];
            B1 = bytes[startIdx];

            if ((B0 & 0x80) != 0)
                ErrorCode = StatusErrorCode.CommunicationError;
            else if (B0 >= 8 && B0 <= 15)
                ErrorCode = StatusErrorCode.CommandSpecificError;
            else if (B0 == 0)
                ErrorCode = StatusErrorCode.NoError;
            else
            {
                try
                {
                    ErrorCode = (StatusErrorCode)Convert.ChangeType(B0, typeof(StatusErrorCode));
                }
                catch
                {
                    ErrorCode = StatusErrorCode.OtherError;
                }
            }

            if (ErrorCode == StatusErrorCode.NoError && DeviceMalfunction)
                ErrorCode = StatusErrorCode.DeviceMalfunction;

            return true;
        }

        bool HasError { get { return (ErrorCode != StatusErrorCode.NoError); } }
        bool IsCommunicationError { get { return (ErrorCode == StatusErrorCode.CommunicationError); } }
        StatusErrorCode ErrorCode { get; set; }

        bool DeviceMalfunction { get { return (!IsCommunicationError && ((B1 & 0x80) != 0)); } }
        bool ConfigurationChanged { get { return (!IsCommunicationError && ((B1 & 0x40) != 0)); } }
        bool ColdStart { get { return (!IsCommunicationError && ((B1 & 0x20) != 0)); } }
        bool MoreStatusAvailable { get { return (!IsCommunicationError && ((B1 & 0x10) != 0)); } }
        bool PrimaryVariableAnalogOutputFixed { get { return (!IsCommunicationError && ((B1 & 0x08) != 0)); } }
        bool PrimaryVariableAnalogOutputSaturated { get { return (!IsCommunicationError && ((B1 & 0x04) != 0)); } }
        bool NonPrimaryVariableOutOfRange { get { return (!IsCommunicationError && ((B1 & 0x02) != 0)); } }
        bool PrimaryVariableOutOfRange { get { return (!IsCommunicationError && ((B1 & 0x01) != 0)); } }

        public override string ToString()
        {
            return Utils.Fcns.CheckedFormat("Status ${0:x2}{1:x2} {2}", B0, B1, ErrorCode);
        }

        public byte B0 { get; set; }
        public byte B1 { get; set; }
    }

    /// <summary></summary>
    public class DeviceUniqueIDInfo
    {
        public byte Expansion { get; set; }
        public byte ManufacturerIDCode { get; set; }
        public byte DeviceTypeCode { get; set; }
        public byte MinimumRequiredPreambleCharacters { get; set; }
        public byte UniversalCommandRevisionLevel { get; set; }
        public byte TransmiterSpecificCommandRevisionLevel { get; set; }
        public byte SoftwareRevision { get; set; }
        public byte HardwareRevision { get; set; }     // format xxxxx.yyy: x=5bit device hardware revision level, y:physical signaling code (0 == RS485)
        public byte Flags { get; set; }
        public UInt32 DeviceID { get; set; }           // only lower 24 bits are valid.

        public DeviceUniqueIDInfo() { }

        public PacketAddressInfo PacketAddressInfo 
        { 
            get 
            {
                return new PacketAddressInfo(this);
            } 
        }

        public string DecodeFrom(byte[] bytes, int startOffset)
        {
            if (bytes == null || bytes.Length < (startOffset + 12))
                return "Invalid Length: UniqueID requires 12 bytes";

            Expansion = bytes[startOffset++];
            ManufacturerIDCode = bytes[startOffset++];
            DeviceTypeCode = bytes[startOffset++];
            MinimumRequiredPreambleCharacters = bytes[startOffset++];
            UniversalCommandRevisionLevel = bytes[startOffset++];
            TransmiterSpecificCommandRevisionLevel = bytes[startOffset++];
            SoftwareRevision = bytes[startOffset++];
            HardwareRevision = bytes[startOffset++];
            Flags = bytes[startOffset++];
            byte devIDbyte0 = bytes[startOffset++];
            byte devIDbyte1 = bytes[startOffset++];
            byte devIDbyte2 = bytes[startOffset++];
            DeviceID = Utils.Data.Pack(0, devIDbyte0, devIDbyte1, devIDbyte2);

            return String.Empty;
        }
    }

    /// <summary></summary>
    public enum CommandCode : byte
    {
        // Universal Commands
        ReadUniqueIdentifier = 0,
        ReadPrimaryVariable = 1,
        ReadCurrentAndAllDynamicVariables = 3,
        ReadUniqueIdentifierAssociatedWithTag = 11,
        ReadMessage = 12,
        ReadTagDescriptorDate = 13,
        ReadFinalAssemblyNumber = 16,

        // Common Practice Commands
        SetPrimaryVariableLowerRange = 37,          // triggers a sensor zero action.
        ReadAdditionalTransmitterStatus = 48,

        // Terminal Specific Commands - not defined here
    }
}

//-------------------------------------------------------------------
