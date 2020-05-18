//-------------------------------------------------------------------
/*! @file E005Mesg.cs
 *  @brief This file defines common types, constants, and methods that are used with semi E005 Messages.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
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
using System.Linq;

using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Semi.E005.Data;

namespace MosaicLib.Semi.E005
{
    #region IMessage, MessageException, Message, ExtensionMethods

    /// <summary>Interface defines the basic accessor properties for all types of E005 Messages and provides the primary methods that can be used to manipulate its contents.</summary>
	public interface IMessage
	{
        /// <summary>Defines the Stream/Function for this message.  Primary messages may expect a reply.</summary>
        StreamFunction SF { get; }

        /// <summary>When this property is true it indicates that high rate logging should be used for actions that relate to this message</summary>
        bool IsHighRate { get; set; }

        /// <summary>Defines the sequence number to be used with this message.  This is generally used as the SystemBytes in the ten byte header.  A reply message's SeqNum comes from the primary message it is in reply to.</summary>
        UInt32 SeqNum { get; set; }

        /// <summary>TenByteHeader may be provided/assigned in some cases</summary>
        ITenByteHeader TenByteHeader { get; }

        /// <summary>This method can be used to set/update a message's TenByteHeader when needed.  When <paramref name="keepMessageSF"/> and/or <paramref name="keepMessageSeqNum"/> are set then the corresponding properties in the incomming <paramref name="tbh"/> will be udpated/replaced from the message's current values, otherwise the message's SF and/or SeqNum will be updated from the incomming <paramref name="tbh"/>'s corresponding contents.</summary>
        IMessage SetTenByteHeader(TenByteHeaderBase tbh, bool keepMessageSF = true, bool keepMessageSeqNum = true);

        /// <summary>Gives user access to the port to which this message will be sent or from which it was received</summary>
        Port.IPort Port { get; set; }

        /// <summary>Returns the contents of the message as a byte array.</summary>
        byte [] ContentBytes { get; }

        /// <summary>Sets the messsage content.  Throws if the message already has been given non-null conents.  Supports call chaining</summary>
        IMessage SetContentBytes(byte[] contentBytes, bool makeCopy = true);

        /// <summary>Returns the reply message that is associated with this message, or null if there is currently no such message.</summary>
        IMessage Reply { get; }

        /// <summary>When called on a request message that expects a reply, this method generates a reply message for it.  Throws when called on an occurrance or reply message.</summary>
        IMessage CreateReply();

        /// <summary>Sets the reply message that is to be associated with this message.  Throws if this message does not expect a reply or if one has already been given.  Supports call chaining</summary>
        IMessage SetReply(IMessage reply, bool replaceReply = false, bool isHighRateReply = false);

        /// <summary>Action factory method.  Calls the associated port's (or the default Manager's DefaultPort if null)'s SendMessage action factory method and returns the resulting action.  When this action is run it will ask the port to send this message.</summary>
        IClientFacet Send();
    }

    /// <summary>
    /// Exception type thrown for invalid attempts to use IMessage methods and properties.
    /// </summary>
    public class MessageException : System.Exception
    {
        /// <summary>MessageException constructor</summary>
        public MessageException(string mesg, IMessage e005Message = null, System.Exception innerException = null) 
            : base(mesg, innerException) 
        {
            E005Message = E005Message;
        }

        public IMessage E005Message { get; private set; }
    }

	/// <summary>This is the implemenation object for the IMessageBase, IMessage and IMessagePortFacet interfaces.</summary>
	public class Message : IMessage
	{
		//-----------------------------
		#region Construction

        /// <summary>
        /// Constructs a message for this given <paramref name="sf"/> which will be sent through the given <paramref name="port"/>.
        /// If the given <paramref name="port"/> is null, the message will be sent to the default Manager's DefaultPort.
        /// </summary>
        public Message(StreamFunction sf, Port.IPort port = null)
        {
            SF = sf;
            Port = port;
        }

        /// <summary>
        /// Internal constructor used to generate message bodies on reception.
        /// </summary>
        internal Message(TenByteHeaderBase tbh, Port.IPort port, Manager.IManagerPortFacet managerPortFacet)
        {
            Port = port;
            SetTenByteHeader(tbh, keepMessageSF: false, keepMessageSeqNum: false);
            IsHighRate = managerPortFacet.IsHighRateSF(SF);
        }

        #endregion

		//-----------------------------
		#region IMessage interface

        /// <summary>Defines the Stream/Function for this message.  Primary messages may expect a reply.</summary>
        public StreamFunction SF { get; private set; }

        /// <summary>When this property is true it indicates that high rate logging should be used for actions that relate to this message</summary>
        public bool IsHighRate { get; set; }

        /// <summary>Defines the sequence number to be used with this message.  This is generally used as the SystemBytes in the ten byte header.  A reply message's SeqNum comes from the primary message it is in reply to.</summary>
        public UInt32 SeqNum 
        { 
            get { return _seqNum; } 
            set
            {
                if (_seqNum != 0)
                    throw new MessageException("The SeqNum property cannot be be changed after a non-zero value has been assigned [{0}]".CheckedFormat(this), this);

                _seqNum = value;
            } 
        }
        private UInt32 _seqNum;

        /// <summary>TenByteHeader may be provided/assigned in some cases</summary>
        public ITenByteHeader TenByteHeader { get; private set; }

        /// <summary>This method can be used to set/update a message's TenByteHeader when needed.  When <paramref name="keepMessageSF"/> and/or <paramref name="keepMessageSeqNum"/> are set then the corresponding properties in the incomming <paramref name="tbh"/> will be udpated/replaced from the message's current values, otherwise the message's SF and/or SeqNum will be updated from the incomming <paramref name="tbh"/>'s corresponding contents.</summary>
        public IMessage SetTenByteHeader(TenByteHeaderBase tbh, bool keepMessageSF = true, bool keepMessageSeqNum = true)
        {
            if (keepMessageSF)
                tbh.SF = SF;
            else
                SF = tbh.SF;

            if (keepMessageSeqNum)
                tbh.SystemBytes = _seqNum;
            else
                _seqNum = tbh.SystemBytes;

            TenByteHeader = tbh;

            return this;
        }

        /// <summary>Gives user access to the port to which this message will be sent or from which it was received</summary>
        public Port.IPort Port 
        {
            get { return _port; }
            set
            {
                if (_port != null)
                    throw new MessageException("This message is already associated with a port", this);

                _port = value;
            }
        }
        private Port.IPort _port = null;

        public byte [] ContentBytes 
        {
            get { return _contentBytes.MapNullToEmpty(); }
        }
        private byte [] _contentBytes = null;

        public IMessage Reply { get; private set; }

        /// <summary>Sets the messsage content.  Throws if the message already has been given non-null conents.  Supports call chaining</summary>
        public IMessage SetContentBytes(byte[] contentByteArray, bool makeCopy = true)
        {
            if (_contentBytes != null)
                throw new MessageException("{0} cannot be used when message already has non-null contents [{1}]".CheckedFormat(Fcns.CurrentMethodName, this), this);

            _contentBytes = (makeCopy) ? contentByteArray.SafeToArray() : contentByteArray;

            return this;
        }

        IMessage IMessage.CreateReply()
        {
            if (!SF.ReplyExpected)
                throw new MessageException("{0} is not valid for this message [{1}]".CheckedFormat(Fcns.CurrentMethodName, this), this);

            if (Reply != null)
                throw new MessageException("{0} cannot be used when message already has an associated Reply [{1}]".CheckedFormat(Fcns.CurrentMethodName, this), this);

            var replySF = SF.ReplySF;

            var reply = new Message(replySF, Port) 
                    { 
                        SF = replySF, 
                        SeqNum = SeqNum,
                        IsHighRate = IsHighRate,
                    }
                    .SetTenByteHeader(TenByteHeader.MakeCopyOfThis(), keepMessageSF: true, keepMessageSeqNum: true);

            return reply;
        }

        IMessage IMessage.SetReply(IMessage reply, bool replaceReply, bool isHighRateReply)
        {
            if (Reply != null && !replaceReply)
                throw new MessageException("{0} cannot be used when message already has an associated Reply [{1}]".CheckedFormat(Fcns.CurrentMethodName, this), this);

            Reply = reply;

            if (isHighRateReply && !Reply.IsHighRate)
                Reply.IsHighRate = true;

            return this;
        }

        IClientFacet IMessage.Send() 
        { 
            return (Port ?? Manager.Manager.Instance.DefaultPort).SendMessage(this); 
        }

		#endregion

        /// <summary>Debugging and logging helper method </summary>
        public override string ToString()
        {
            var headerStr = (TenByteHeader != null) ? TenByteHeader.ToString() : SF.ToString();

            if (_contentBytes.IsNullOrEmpty())
            {
                return "{0} [header only]".CheckedFormat(headerStr);
            }
            else if (_contentBytes.Length <= 1000)
            {
                try
                {
                    if (!SF.PayloadIsABS)
                    {
                        ValueContainer vc = ValueContainer.Empty.ConvertFromE005Data(_contentBytes.SafeToArray(), throwOnException: true);
                        return "{0} len:{1} SML:{2}".CheckedFormat(headerStr, _contentBytes.SafeLength(), vc.ToStringSML());
                    }
                }
                catch { }

                return "{0} len:{1} hex:[{2}]".CheckedFormat(headerStr, _contentBytes.SafeLength(), ByteArrayTranscoders.HexStringTranscoder.Encode(_contentBytes));
            }
            else
            {
                return "{0} len:{1} truncatedHex:[{2}]".CheckedFormat(headerStr, _contentBytes.SafeLength(), ByteArrayTranscoders.HexStringTranscoder.Encode(_contentBytes.SafeSubArray(0, 128)));
            }
        }
	}

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Sets the message content from the given <paramref name="vc"/> by converting its contents to E005 data and setting the message contents from that value.
        /// </summary>
        public static IMessage SetContentBytes(this IMessage mesg, ValueContainer vc, bool throwOnException = true)
        {
            return mesg.SetContentBytes(vc.ConvertToE005Data(throwOnException: throwOnException));
        }

        /// <summary>
        /// Sets the message content from the given <paramref name="vcb"/> by building its contents, converting them to E005 data, and setting the message contents from that value.
        /// </summary>
        public static IMessage SetContentBytes(this IMessage mesg, IValueContainerBuilder vcb, bool throwOnException = true)
        {
            return mesg.SetContentBytes(vcb.BuildContents(), throwOnException: throwOnException);
        }

        /// <summary>
        /// Sets the message's IsHighRate property to the given <paramref name="isHighRate"/> value (defaults to true).
        /// </summary>
        public static IMessage SetIsHighRate(this IMessage mesg, bool isHighRate = true)
        {
            mesg.IsHighRate = isHighRate;
            return mesg;
        }

        /// <summary>
        /// Gets the given <paramref name="mesg"/>'s content bytes converted from E005 data to a ValueContainer.
        /// </summary>
        public static ValueContainer GetDecodedContents(this IMessage mesg, bool throwOnException = true)
        {
            if (mesg != null)
                return ValueContainer.Empty.ConvertFromE005Data(mesg.ContentBytes, throwOnException: throwOnException);
            else if (throwOnException)
                throw new System.NullReferenceException("mesg was null");
            else
                return ValueContainer.Empty;
        }
    }

    #endregion

    #region StreamFunction, ExtensionMethods

    /// <summary>Helper struct used to pack, unpack and manipulate Stream/Function bytes for a message object.</summary>
    /// <remarks>also used as Bytes2/Bytes3 in HSMS headers</remarks>
	public struct StreamFunction
	{
		Byte byte2;
        Byte byte3;

        /// <summary>Gets/Sets the stored value of Byte2 (StreamByte and WBit)</summary>
        public Byte Byte2 { get { return byte2; } set { byte2 = value; } }
        /// <summary>Gets/Sets the stored value of Byte3 (FunctionByte)</summary>
        public Byte Byte3 { get { return byte3; } set { byte3 = value; } }
        /// <summary>Gets/Sets Byte2 and Byte3 (Stream, Function and WBit) from a single packed word (in network order)</summary>
        public UInt16 B2B3 { get { return Utils.Data.Pack(byte2, byte3); } set { Utils.Data.Unpack(value, out byte2, out byte3); } }

        /// <summary>Returns the Stream and Function packed into a single word (in network order).</summary>
        public UInt16 PackedSF { get { return Utils.Data.Pack(StreamByte, FunctionByte); } }

        /// <summary>Gets/Sets the bottom 7 bits of Byte2 (Stream)</summary>
        public Byte StreamByte { get { return (byte)(byte2 & 0x7f); } set { byte2 = (byte)((byte2 & 0x80) | (value & 0x7f)); } }
        /// <summary>Gets/Sets all of Byte3 to the given value (Function).</summary>
        public Byte FunctionByte { get { return byte3; } set { byte3 = value; } }
        /// <summary>Gets/Sets the WBit which is the top bit of Byte2 (Stream).</summary>
        public bool WBit { get { return ((byte2 & 0x80) != 0); } set { byte2 = (byte)((byte2 & 0x7f) | (value ? 0x80 : 0x00)); } }

        /// <summary>Returns true if the WBit is true to indicate that a reply is expected to this message.  Normally this is only true for SF's that are Primary.</summary>
        public bool ReplyExpected { get { return WBit; } set { WBit = value; } }

        /// <summary>Returns true if this StreamFunction is Primary (ie if Function is odd)</summary>
        public bool IsPrimary { get { return ((FunctionByte & 0x01) != 0); } }
        /// <summary>Returns true if this StreamFunction is not Primary (ie if Function is not odd)</summary>
        public bool IsSecondary { get { return !IsPrimary; } }

        /// <summary>Returns true if this instance's contents are all zero.</summary>
        public bool IsEmpty { get { return (byte2 == 0 && byte3 == 0); } }

        /// <summary>Sets Byte2 and Byte3 from the given stream, function and isReplyExpected values.</summary>
        public void SetSF(Byte stream, Byte function, bool isReplyExpected) { StreamByte = stream; FunctionByte = function; ReplyExpected = isReplyExpected; }
        /// <summary>Sets Byte2 and Byte3 from the given stream and function values.  ReplyExpected (WBit) is assigned to true iff function is odd.</summary>
        public void SetSF(Byte stream, Byte function) { SetSF(stream, function, (function & 1) != 0); }
        /// <summary>Sets Byte2 and Byte3 explicitly from the given values.</summary>
        public void SetBytes(Byte byte2, Byte byte3) { Byte2 = byte2; Byte3 = byte3; }

        /// <summary>Constructs this object based on the given desired stream, function and isReplyExpected values.</summary>
        public StreamFunction(Byte stream, Byte function, bool isReplyExpected) : this() { SetSF(stream, function, isReplyExpected); }
        /// <summary>Constructs this object based on the given byte2 and byte3 values.</summary>
        public StreamFunction(Byte byte2, Byte byte3) : this() { SetBytes(byte2, byte3); }
        /// <summary>Constructs this object based on a single word that contains byte2 and byte3</summary>
        public StreamFunction(UInt16 b2b3) : this() { B2B3 = b2b3; }

        /// <summary>Returns a new StreamFunction that is set to the Stream/Function for a reply to this StreamFunction.  If this SF IsPrimary and expects a reply then the return value has the same stream with the function incremented by 1 and the W bit clear.  Otherwise the returned object is the default object (S0/F0)</summary>
        public StreamFunction ReplySF { get { return ((IsPrimary && ReplyExpected) ? new StreamFunction(StreamByte, (Byte)(FunctionByte + 1), false) : new StreamFunction()); } }

        /// <summary>Returns a new StreamFunction that is set to the same Stream as the source StreamFunction but with FunctionByte set to zero and ReplyExpected set to false.</summary>
        public StreamFunction TransactionAbortReplySF { get { return ((IsPrimary && ReplyExpected) ? new StreamFunction(StreamByte, 0, false) : new StreamFunction()); } }

        /// <summary>Returns text version of the decoded contents of byte2/byte3</summary>
        public override string ToString()
		{
			return "S{0}/F{1}{2}".CheckedFormat(StreamByte, FunctionByte, WBit ? "[W]" : "");
		}

        /// <summary>
        /// Returns true for S/F combinations that use a pure binary payload.
        /// <para/>Surrently S2F25, and S2F26
        /// </summary>
        public bool PayloadIsABS
        {
            get
            {
                return (StreamByte == 2 && (FunctionByte == 25 || FunctionByte == 26));
            }
        }
	}

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Attempts to parse the given string <paramref name="s"/> as a StreamFunction.
        /// <para/>Valid values are of the form "S1F16", "S3F11W", etc.
        /// </summary>
        public static StreamFunction ParseAsStreamFunction(this string s, bool throwOnIssue = true)
        {
            StringScanner scanner = new StringScanner(s);

            int stream = 0;
            if (!(scanner.MatchToken("S", skipLeadingWhiteSpace: false, requireTokenEnd: false) && scanner.ParseValue(out stream, 0, tokenType: TokenType.NumericDigits) && stream.IsInRange(1, 255)))
            {
                if (throwOnIssue)
                    throw new MessageException("'{0}' {1} failed: could not parse stream value Snnn".CheckedFormat(s, Fcns.CurrentMethodName));
            }

            scanner.MatchToken(@"/", skipLeadingWhiteSpace: false, skipTrailingWhiteSpace: false, requireTokenEnd: false);

            int function = 0;
            if (!(scanner.MatchToken("F", skipLeadingWhiteSpace: false, requireTokenEnd: false) && scanner.ParseValue(out function, 0, tokenType: TokenType.NumericDigits) && stream.IsInRange(1, 255)))
            {
                if (throwOnIssue)
                    throw new MessageException("'{0}' {1} failed: could not parse function value Fnnn".CheckedFormat(s, Fcns.CurrentMethodName));
            }

            bool expectReply = scanner.MatchToken("W") || scanner.MatchToken("[W]");

            if (!scanner.IsAtEnd && throwOnIssue)
            {
                throw new MessageException("'{0}' {1} failed: unrecognized content at '{2}'".CheckedFormat(s, Fcns.CurrentMethodName, scanner.Rest));
            }

            return new StreamFunction((byte)stream, (byte)function, expectReply);
        }
    }


	#endregion

	#region ITenByteHeader and TenByteHeaderBase

	/// <summary>
    /// Interface provided by all types of TenByteHeader implementation objects.
    /// TenByteHeader objects are used with E004 and E037 ports as part of the SEMI standard family of serial connection protocols.
    /// These headers contains routing and sequencing information as well as the Stream/Function of the message for SECS-II Message payloads.
    /// </summary>
	/// <remarks>
	/// This interface allows the generic E005.Message objects and the E005.Manager objects to get access to critical header contents
	/// without needing to know the details of which specific header type the information came from.  This is important because E037 HSMS
	/// and E005 SECS-I Headers use some of their byte contents quite differently and as such are implemented here using different storage and
	/// access classes.  See TenByteHeaderBase for more details.
	/// </remarks>

	public interface ITenByteHeader : ICopyable<TenByteHeaderBase>
	{
        /// <summary>Gets/Sets StreamFunction contained in this TBH (bytes 2..3)</summary>
        StreamFunction SF { get; set; }

        /// <summary>Gets/Sets SystemBytes from TBH (bytes 6..9)</summary>
        UInt32 SystemBytes { get; set; }

        /// <summary>Gives get/set access to the UInt16 that represents Byte 0 and Byte 1 of the Ten Byte Header - usually the SessionID</summary>
        UInt16 B0B1 { get; set; }

        /// <summary>Gives get/set access to the UInt16 that represents Byte 4 and Byte 5 of the Ten Byte Header - used as the PType and SType for E037</summary>
        UInt16 B4B5 { get; set; }

        /// <summary>Encodes contents of this object into the given bytes.  Returns true if target array and given offset provide access to 10 usable bytes or false otherwise.</summary>
        bool Encode(byte [] toTenByteHeaderDataArray, int atOffsetIdx);

        /// <summary>Updates contents of this object from the selected bytes in the given source byte list.  Returns true if source array and given offset provide access to 10 usable bytes or false otherwise.</summary>
        bool Decode(byte [] fromTenByteHeaderDataArray, int atOffsetIdx);

        /// <summary>Returns a new byte array of 10 bytes containing the encoded contents of this object.</summary>
        byte[] ByteArray { get; }
	}

	//-------------------------------------------------------------------

	/// <summary>
    /// Base class which is intended for use by TenByteHeader implementation classes.  
    /// This utility class provides common implementations for the common parts of the E005 and E037 TenByteHeader implementation classes.
    /// </summary>
	public class TenByteHeaderBase : ITenByteHeader
	{
        /// <summary>Default constructor.  Gives all bytes as zero (S0/F0).</summary>
		public TenByteHeaderBase() { }
        /// <summary>Construct a simple TBH for a given StreamFunction value.</summary>
        public TenByteHeaderBase(StreamFunction sf) : this() { SF = sf; }
        /// <summary>Copy constructor.  Clones/Replicates contents of given itbh value.</summary>
        public TenByteHeaderBase(ITenByteHeader itbh) : this() { B0B1 = itbh.B0B1; SF = itbh.SF; B4B5 = itbh.B4B5; SystemBytes = itbh.SystemBytes; }

        /// <summary>Returns a MemberwiseClone of this object</summary>
        public TenByteHeaderBase MakeCopyOfThis(bool deepCopy = true)
        {
            return (TenByteHeaderBase)MemberwiseClone();
        }

		private UInt16 b0b1 = 0;
		private Byte b2 = 0, b3 = 0;        // must remain synchronized with SF contents.
		private UInt16 b4b5 = 0;
		private UInt32 systemBytes = 0;		// normally used to carry the packet or transaction sequence number

		private bool sfSet = false;
		private StreamFunction sf;

        /// <summary>Gets/Sets StreamFunction contained in this TBH</summary>
        public StreamFunction SF { get { if (!sfSet) { sf.SetBytes(b2, b3); sfSet = true; } return sf; } set { sf = value; sfSet = true; b2 = sf.Byte2; b3 = sf.Byte3; } }
        /// <summary>Gets/Sets SystemBytes from TBH (bytes 6..9)</summary>
        public UInt32 SystemBytes { get { return systemBytes; } set { systemBytes = value; } }

        /// <summary>Gives get/set access to the UInt16 that represents Byte 0 and Byte 1 of the Ten Byte Header - usually the SessionID</summary>
        public UInt16 B0B1 { get { return b0b1; } set { b0b1 = value; } }
        /// <summary>Gets/Sets Byte2 from TBH.  Set triggers SF rebuild.</summary>
        protected Byte B2 { get { return b2; } set { sfSet = false; b2 = value; } }
        /// <summary>Gets/Sets Byte3 from TBH.  Set triggers SF rebuild.</summary>
        protected Byte B3 { get { return b3; } set { sfSet = false; b3 = value; } }
        /// <summary>Gives get/set access to the UInt16 that represents Byte 4 and Byte 5 of the Ten Byte Header - used as the PType and SType for E037</summary>
        public UInt16 B4B5 { get { return b4b5; } set { b4b5 = value; } }

        /// <summary>Encodes contents of this object into the given byte array.  Returns true if target array and given offset provide access to 10 usable bytes or false otherwise.</summary>
        public bool Encode(byte [] toTenByteHeaderDataArray, int atOffsetIdx)
		{
            if (!toTenByteHeaderDataArray.IsSafeIndex(atOffsetIdx, 10))
                return false;

			Utils.Data.Unpack(b0b1, toTenByteHeaderDataArray, atOffsetIdx + 0);
			toTenByteHeaderDataArray[atOffsetIdx + 2] = b2;
			toTenByteHeaderDataArray[atOffsetIdx + 3] = b3;
			Utils.Data.Unpack(b4b5, toTenByteHeaderDataArray, atOffsetIdx + 4);
			Utils.Data.Unpack(systemBytes, toTenByteHeaderDataArray, atOffsetIdx + 6);

			return true;
		}

        /// <summary>Updates contents of this object from the selected bytes in the given source byte array.  Returns true if source array and given offset provide access to 10 usable bytes or false otherwise.</summary>
        public bool Decode(byte [] fromTenByteHeaderDataArray, int atOffsetIdx)
		{
			b0b1 = 0;
			b2 = 0;
			b3 = 0;
			sfSet = false;
			b4b5 = 0;
			systemBytes = 0;

            if (fromTenByteHeaderDataArray.SafeCount() < atOffsetIdx + 10)
				return false;

			Utils.Data.Pack(fromTenByteHeaderDataArray, atOffsetIdx + 0, out b0b1);
			b2 = fromTenByteHeaderDataArray [atOffsetIdx + 2];
			b3 = fromTenByteHeaderDataArray [atOffsetIdx + 3];
			Utils.Data.Pack(fromTenByteHeaderDataArray, atOffsetIdx + 4, out b4b5);
			Utils.Data.Pack(fromTenByteHeaderDataArray, atOffsetIdx + 6, out systemBytes);

			return true;
		}

        /// <summary>Returns a new byte array of 10 bytes containing the encoded contents of this object.</summary>
        public byte[] ByteArray { get { byte[] bytes = new byte[10]; Encode(bytes, 0); return bytes; } }

        /// <summary>Returns a string containing the formatted body of the 10 bytes followed by the current SF contents formatted as a string.</summary>
        public override string ToString()
		{
            return "{0} TBH[${1:x4},${2:x4},${3:x8}]".CheckedFormat(SF, b0b1, /*b2, b3,*/ b4b5, systemBytes);
		}
	}

	#endregion

    #region IHTool

    /// <summary>This struct is used to refer to and process E005 Item Header (IH) bytes.</summary>
    /// <remarks>
    /// This struct provides means to convert between the byte array image of a Item Header and a programatically usefull version.
    /// </remarks>
    public struct IHTool
    {
        /// <summary>Constructor: Uses Setup(data, idx)</summary>
        public IHTool(IList<byte> data, int idx = 0) : this() 
        { 
            Setup(data, idx); 
        }

        /// <summary>Constructor: Uses Setup(ifc, numItems)</summary>
        public IHTool(ItemFormatCode ifc, int numItems) : this() 
        { 
            Setup(ifc, numItems); 
        }

        /// <summary>Constructor: Uses Setup(ifc, numItems, minIHLengthBytes)</summary>
        public IHTool(ItemFormatCode ifc, int numItems, int minIHLengthBytes) : this() 
        { 
            Setup(ifc, numItems, minIHLengthBytes); 
        }

        /// <summary>Setup the IHTool contents from the given data bytes starting at the given offset.</summary>
        /// <returns>true on success, false otherwise</returns>
        public bool Setup(IList<byte> data, int idx = 0)
        {
            Clear();

            if (data.IsNullOrEmpty() && idx == 0)
            {
                Setup(ItemFormatCode.None, 0, 0);
                return true;
            }

            int dataLength = data.SafeCount();
            int remainingBytes = dataLength - idx;

            // check for and extract byte zero from ih
            if (remainingBytes < 1)
            {
                FaultCode = "E005.IH Setup Failed: attempt to index IH.b0 past end of given data array";
                return false;
            }

            b0 = data[idx++];
            remainingBytes--;

            // extract ifc and convert it to defined enum value (as approprate)
            Int32 ifcValue = (Int32)(b0 >> 2);

            if (System.Enum.IsDefined(typeof(ItemFormatCode), ifcValue))
            {
                typeInfo.IFC = (ItemFormatCode)ifcValue;
                isDefined = true;
            }
            else
            {
                FaultCode = "E005.IH Setup Failed: format code value:{0} is not a legal item format code value".CheckedFormat(ifcValue);
                return false;
            }

            // extract ihNumLengthBytes and test it
            ihNumLengthBytes = (byte)(b0 & 0x03);
            if (ihNumLengthBytes < 1 || ihNumLengthBytes > 3)
            {
                FaultCode = "E005.IH Setup Failed: length bytes:{0} is not valid".CheckedFormat(ihNumLengthBytes);
                return false;
            }

            if (remainingBytes < ihNumLengthBytes)
            {
                FaultCode = "E005.IH Setup Failed: attempt to index IH past end of given data array";
                return false;
            }

            if (ihNumLengthBytes >= 1)
                b1 = data[idx + 0];
            if (ihNumLengthBytes >= 2)
                b2 = data[idx + 1];
            if (ihNumLengthBytes >= 3)
                b3 = data[idx + 2];

            idx += ihNumLengthBytes;
            remainingBytes -= ihNumLengthBytes;

            // convert b1 through b3 to ihLength

            switch (ihNumLengthBytes)
            {
                case 1:
                    unchecked { ihLengthValue = (int)Utils.Data.Pack(0, 0, 0, b1); }
                    break;
                case 2:
                    unchecked { ihLengthValue = (int)Utils.Data.Pack(0, 0, b1, b2); }
                    break;
                case 3:
                    unchecked { ihLengthValue = (int)Utils.Data.Pack(0, b1, b2, b3); }
                    break;
                default:
                    ihLengthValue = 0;
                    break;
            }

            if (typeInfo.IFC.IsList())		// this is the only supported format code with a zero item size
            {
                itemCount = ihLengthValue;
                if (remainingBytes < itemCount)
                {
                    FaultCode = "E005.IH Setup Failed: attempt to index IH list body past end of given data array";
                    return false;
                }
                return true;
            }

            if (typeInfo.IFC == ItemFormatCode.W && ihLengthValue < ExtraSize)
            {
                FaultCode = "E005.IH Setup Failed: WSTR item length field below minimum supported size";
                return false;
            }

            if (ItemElementSize > 0)
            {
                if (remainingBytes < ihLengthValue)
                {
                    FaultCode = "E005.IH Setup Failed: attempt to index IH body past end of given data array";
                    return false;
                }

                itemCount = (ihLengthValue - ExtraSize) / ItemElementSize;
            }
            else
            {
                FaultCode = "E005.IH Setup Failed: Unexpected format code or ItemElementSize is not consistant with item format code";
                itemCount = 0;
                return false;
            }

            return true;
        }

        /// <summary>Setup IHTool contents for the given IFC, and numItems using the minimum necesary number of length bytes.</summary>
        /// <returns>true on success, false otherwise</returns>
        public bool Setup(ItemFormatCode ifc, int numItems) 
        { 
            return Setup(ifc, numItems, 0); 
        }

        /// <summary>Setup IHTool contents for the given IFC, numItems and no less than minIHLengthBytes for the number of length bytes in the resulting header.</summary>
        /// <returns>true on success, false otherwise</returns>
        public bool Setup(ItemFormatCode ifc, int numItems, int minIHLengthBytes)
        {
            Clear();

            typeInfo.IFC = ifc;
            isDefined = !typeInfo.IFC.IsInvalid();
            var isNone = typeInfo.IFC.IsNone();

            itemCount = numItems;

            if (isNone)
                ihLengthValue = 0;
            else if (typeInfo.IFC.IsList())
                ihLengthValue = itemCount;
            else if (ItemElementSize > 0)
                ihLengthValue = itemCount * ItemElementSize + ExtraSize;
            else
            {
                // item size is zero
                if (!typeInfo.IFC.IsUsableWithE005())
                    FaultCode = "E005.IH Setup Failed: IFC {0} is not usable as E005 data".CheckedFormat(TypeInfo);
                else
                    FaultCode = "E005.IH Setup internal failure: zero item size is not legal for IFC {0} (only for list items)".CheckedFormat(TypeInfo);

                return false;
            }

            ihNumLengthBytes = 0;

            if (isNone)
                ihNumLengthBytes = 0;
            else if (ihLengthValue <= 0x000000ff)
                ihNumLengthBytes = Math.Max(minIHLengthBytes, 1);
            else if (ihLengthValue <= 0x0000ffff)
                ihNumLengthBytes = Math.Max(minIHLengthBytes, 2);
            else if (ihLengthValue <= 0x00ffffff)
                ihNumLengthBytes = Math.Max(minIHLengthBytes, 3);
            else
                ihNumLengthBytes = Math.Max(minIHLengthBytes, 4);

            unchecked { b0 = (byte)(((int)(ifc) << 2) | (ihNumLengthBytes & 0x03)); }

            switch (ihNumLengthBytes)
            {
                case 0:
                    break;
                case 1:
                    unchecked { b1 = (Byte)ihLengthValue; }
                    break;
                case 2:
                    unchecked { Utils.Data.Unpack((UInt16)ihLengthValue, out b1, out b2); }
                    break;
                case 3:
                    unchecked { Byte umsb = 0; Utils.Data.Unpack((UInt32)ihLengthValue, out umsb, out b1, out b2, out b3); }
                    break;
                default:
                    FaultCode = "E005.IH Setup Failed: attempt to setup IH with invalid number of length bytes:{0}".CheckedFormat(ihNumLengthBytes);
                    return false;
            }

            if (ihLengthValue < 0 || ihLengthValue > 0x00ffffff)
            {
                FaultCode = "E005.IH Setup Failed: attempt to setup oversized IH body with length:{0}".CheckedFormat(ihLengthValue);
                return false;
            }

            return true;
        }

        /// <summary>Returns true if the contents of the tool are no longer in their default constructor/cleared state.</summary>
        public bool IsDefined { get { return isDefined; } }
        /// <summary>Returns true if the FaultCode is a non-empty string</summary>
        public bool IsFaulted { get { return !string.IsNullOrEmpty(faultCode); } }
        /// <summary>Returns string.Empty if tool IsValid, string.Empty if !IsDefined and non-empty string in all other cases.</summary>
        public string FaultCode
        {
            get { return (IsValid ? string.Empty : faultCode); }
            internal set
            {
                if (!string.IsNullOrEmpty(value) && faultCode != value)
                    Utils.Asserts.LogFaultOccurance(value);

                faultCode = value;
            }
        }
        /// <summary>True if the IHTool's contents have been defined and there is no recorded fault code.</summary>
        public bool IsValid { get { return IsDefined && !IsFaulted; } }
        /// <summary>This gives a copy of the IFC TypeInfo decoder struct that this IHTool is using</summary>
        public TypeInfo TypeInfo { get { return typeInfo; } }

        /// <summary>This gives the number of bytes of length that follows the type code byte in the header (between 1 and 3)</summary>
        public int NumLengthBytes { get { return ihNumLengthBytes; } }
        /// <summary>This gives the number of bytes per item for the stored data (0 for lists)</summary>
        public int ItemElementSize { get { return typeInfo.ItemElementSize; } }
        /// <summary>This gives the number of type specific extra bytes that follows the header</summary>
        public int ExtraSize { get { return typeInfo.ExtraSize; } }
        /// <summary>This gives the number of Items that are in the body that follows the header</summary>
        public int ItemCount { get { return (IsValid ? itemCount : 0); } }

        /// <summary>Gives the size in bytes of the entire header</summary>
        public int HeaderSize { get { return (!typeInfo.IFC.IsNone() ? (1 + ihNumLengthBytes) : 0); } }

        /// <summary>
        /// Gives the length value from the header.  
        /// For lists this is the number of items in the list that follow the header.  
        /// For ASCII strings this gives the total number of bytes of data that follows the header
        /// For WSTR strings this is added to the extraBytes (2) to give the total number of bytes that follows the header
        /// For other types this gives the total number of bytes of data that follow the header
        /// </summary>
        public int HeaderLengthValue { get { return ihLengthValue; } }

        /// <summary>
        /// Gives Header length value less ExtraSize
        /// </summary>
        public int DataLengthValue { get { return HeaderLengthValue - ExtraSize; } }

        /// <summary>
        /// Gives the total size of the data that is needed to store the header, extra bytes and the item body as
        /// HeaderSize + ExtraSize + HeaderLengthValue.  
        /// NOTE: for lists the HeaderLengthValue is not included since it is not actually a byte count: it is a count of the number
        /// of list items that follow the header without knowledge of the actual length of any one of them.
        /// </summary>
        public int TotalSize { get { return HeaderSize + ExtraSize + (typeInfo.IFC.IsList() ? 0 : HeaderLengthValue); } }

        /// <summary>Generate header bytes from tool contents and save into given buffer at indicated index</summary>
        /// <param name="data">buffer to place header bytes into.</param>
        /// <param name="idx">index of position in buffer to place header bytes into</param>
        /// <returns>number of bytes that were put into buffer or 0 if header could not be saved to buffer.</returns>
        public int GenerateHeaderBytes(byte[] data, int idx)
        {
            if (typeInfo.IFC.IsNone())
            {
                return 0;
            }

            if (ihNumLengthBytes <= 0 || ihNumLengthBytes > 3)
            {
                if (string.IsNullOrEmpty(FaultCode))
                    FaultCode = "E005.IH {0} GenerateHeaderBytes failed: internal: NumLengthByte:{1} is not valid".CheckedFormat(TypeInfo, ihNumLengthBytes);
                return 0;
            }

            if (data == null || (HeaderSize + idx) > data.Length)
            {
                if (string.IsNullOrEmpty(FaultCode))
                    FaultCode = "E005.IH {0} GenerateHeaderBytes failed: target buffer is not valid and large enough to store header".CheckedFormat(TypeInfo);
                return 0;
            }

            data[idx++] = b0;
            if (ihNumLengthBytes > 0)
                data[idx++] = b1;
            if (ihNumLengthBytes > 1)
                data[idx++] = b2;
            if (ihNumLengthBytes > 2)
                data[idx++] = b3;

            return ihNumLengthBytes + 1;
        }

        /// <summary>Reset the IHTool to the default constructed state with no faultcode.</summary>
        public void Clear()
        {
            isDefined = false;
            faultCode = null;
            typeInfo.IFC = ItemFormatCode.None;
            ihNumLengthBytes = 0;
            ihLengthValue = 0;
            itemCount = 0;

            b0 = b1 = b2 = b3 = 0;
        }

        /// <summary>true if the IHTool has been setup or initialized from a data body</summary>
        private bool isDefined;
        /// <summary>non-empty string indicates source of last detected fault</summary>
        private string faultCode;

        /// <summary>the typeinfo for the IFC code from the first byte in the header</summary>
        private TypeInfo typeInfo;
        /// <summary>the number of bytes in the length field in the header</summary>
        private int ihNumLengthBytes;
        /// <summary>the value of the length field in the header</summary>
        private int ihLengthValue;
        /// <summary>stated number of items in the body</summary>
        private int itemCount;

        /// <summary>bytes used to pack to/from the header in a byte buffer.</summary>
        private byte b0, b1, b2, b3;
    }

    #endregion

    #region TypeInfo

    /// <summary>
    /// This struct defines various properties of a ItemFormatCode value.  
    /// It also provides a set of static helper methods that are used in handling IFC values and conversions between native types and IFC types
    /// </summary>
    public struct TypeInfo
    {
        private struct SizeInfo
        {
            public readonly int itemElementSize;
            public readonly int extraSize;

            public SizeInfo(int itemElementSize, int extraSize)
            {
                this.itemElementSize = itemElementSize;
                this.extraSize = extraSize;
            }
        }

        /// <summary>Explicit constructor from a given ItemFormatCode</summary>
        public TypeInfo(ItemFormatCode ifc) : this() { IFC = ifc; }

        private ItemFormatCode ifc;
        private SizeInfo sizeInfo;
        private bool isDefined;

        public static TypeInfo None { get { return new TypeInfo(ItemFormatCode.None); } }
        public static TypeInfo Invalid { get { return new TypeInfo(ItemFormatCode.Invalid); } }

        /// <summary>Gets stored ItemFormatCode value, sets stored value and updates corresponding itemSize</summary>
        public ItemFormatCode IFC
        {
            get { return (isDefined ? ifc : ItemFormatCode.None); }
            set
            {
                ifc = value;
                isDefined = GetSizeInfoForIFC(ifc, out sizeInfo);
            }
        }

        /// <summary>Gets per item size for stored IFC value</summary>
        public int ItemElementSize { get { return sizeInfo.itemElementSize; } }
        /// <summary>Gets extra overhead size for stored IFC value (non-ascii, non-jis strings...)</summary>
        public int ExtraSize { get { return sizeInfo.extraSize; } }

        /// <summary>Convert given ItemFormatCode into an itemSize and extraSize, returns true if ItemFormatCode is Valid or is None</summary>
        public static bool GetIFCSizeInfo(ItemFormatCode ifc, out int itemElementSize, out int extraSize)
        {
            SizeInfo sizeInfo;
            bool success = GetSizeInfoForIFC(ifc, out sizeInfo);
            itemElementSize = sizeInfo.itemElementSize;
            extraSize = sizeInfo.extraSize;
            return success;
        }

        private static Dictionary<ItemFormatCode, SizeInfo> ifcToSizeInfoMap
            = new Dictionary<ItemFormatCode, SizeInfo>
			{
				{ ItemFormatCode.L, new SizeInfo(0,0) }, 
				{ ItemFormatCode.Bi, new SizeInfo(1,0) }, { ItemFormatCode.Bo, new SizeInfo(1,0) }, 
				{ ItemFormatCode.A, new SizeInfo(1,0) }, { ItemFormatCode.J, new SizeInfo(1,0) }, { ItemFormatCode.W, new SizeInfo(2,2) }, 
				{ ItemFormatCode.I1, new SizeInfo(1,0) }, { ItemFormatCode.I2, new SizeInfo(1,0) }, { ItemFormatCode.I4, new SizeInfo(4,0) }, { ItemFormatCode.I8, new SizeInfo(8,0) }, 
				{ ItemFormatCode.U1, new SizeInfo(1,0) }, { ItemFormatCode.U2, new SizeInfo(2,0) }, { ItemFormatCode.U4, new SizeInfo(4,0) }, { ItemFormatCode.U8, new SizeInfo(8,0) }, 
				{ ItemFormatCode.F4, new SizeInfo(4,0) }, { ItemFormatCode.F8, new SizeInfo(8,0) }, 
				{ ItemFormatCode.None, new SizeInfo(0,0) }, 
			};

        private static bool GetSizeInfoForIFC(ItemFormatCode ifc, out SizeInfo sizeInfo)
        {
            if (ifcToSizeInfoMap.TryGetValue(ifc, out sizeInfo))
                return true;

            sizeInfo = new SizeInfo();
            return false;
        }

        /// <summary>Dictionary used to convert between an IFC and the xml Element name to use for that IFC value.</summary>
        private static Dictionary<ItemFormatCode, string> ifcToElementNameMap
            = new Dictionary<ItemFormatCode, string>
			{
				{ ItemFormatCode.L, "L" }, 
				{ ItemFormatCode.Bi, "Bi" }, { ItemFormatCode.Bo, "Bo" }, 
				{ ItemFormatCode.A, "A" }, { ItemFormatCode.J, "J" }, { ItemFormatCode.W, "W" }, 
				{ ItemFormatCode.I1, "I1" }, { ItemFormatCode.I2, "I2" }, { ItemFormatCode.I4, "I4" }, { ItemFormatCode.I8, "I8" }, 
				{ ItemFormatCode.U1, "U1" }, { ItemFormatCode.U2, "U2" }, { ItemFormatCode.U4, "U4" }, { ItemFormatCode.U8, "U8" }, 
				{ ItemFormatCode.F4, "F4" }, { ItemFormatCode.F8, "F8" }, 
				{ ItemFormatCode.Null, "Null" }, { ItemFormatCode.None, "None" }, { ItemFormatCode.Invalid, "Invalid" }
			};

        /// <summary>static method used to convert an IFC to the corresponding XML element name.</summary>
        public static string GetIFCXmlElementName(ItemFormatCode ifc)
        {
            string elementName = null;
            if (ifcToElementNameMap.TryGetValue(ifc, out elementName))
                return elementName;
            else
                return "InvalidIFC_{0}".CheckedFormat(ifc);
        }

        /// <summary>gets the XML element name for the stored IFC value.</summary>
        public string IFCXmlElementName { get { return GetIFCXmlElementName(IFC); } }

        /// <summary>Same as IFCXmlElementName</summary>
        public override string ToString() { return IFCXmlElementName; }
    }

    #endregion
}