//-------------------------------------------------------------------
/*! @file Interconnect/Remoting/Buffers.cs
 *  @brief Common Buffer related definitions for Modular.Interconnect.Remoting.Buffers
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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
using System.Reflection;
using System.Runtime.InteropServices;

using MosaicLib.Modular.Common;
using MosaicLib.Semi.E005.Data;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

// Please note: see comments in for MosaicLib.Modular.Interconnect.Remoting in Remoting.cs

namespace MosaicLib.Modular.Interconnect.Remoting.Buffers
{
    #region BufferHeaderV1, BufferPurpose, BufferHeaderFlags

    /// <summary>Version 1 buffer transport header.  [Minimum Size=26]</summary>
    [StructLayout(LayoutKind.Sequential, Size = 26)]
    public struct BufferHeaderV1 : IEquatable<BufferHeaderV1>
    {
        /// <summary>Returns the serialized size of this header using GetMarshaledByteArraySize [26]</summary>
        public static readonly int MinimumSize = typeof(BufferHeaderV1).GetMarshaledByteArraySize(rethrow: false);

        /// <summary>First 4 bytes of any given buffer header.  Used both to carry the purpose of the buffer and as a basic heuristic to confirm protocol version and delivery alignment.</summary>
        public PurposeCode PurposeCode;

        /// <summary>Returns true if the current header's Magic matches the expected value.</summary>
        public bool IsPurposeCodeValid { get { return PurposeCode.IsValid(includeAck: AckSeqNum != 0); } }

        /// <summary>Information about the purpose of the delivery of this buffer</summary>
        public BufferHeaderFlags Flags;

        /// <summary>Gives the header length</summary>
        public int Length { get { return Math.Max(_length, MinimumSize); } set { _length = unchecked((ushort) value); } }
        private ushort _length;

        /// <summary>When non-zero this gives the BufferSeqNum of the contents of this buffer.  Will be zero for None and Session related buffers.</summary>
        public ulong SeqNum;

        /// <summary>Gives the BufferSeqNum of the last buffer that has been sequentially accepted by the remote end's session message reconstruction logic.  This indicates that the buffer has been accepted and that the sender no longer needs to retain it for possible future retransmission.</summary>
        public ulong AckSeqNum;

        /// <summary>Gives the Stream for which this buffer is to be accumulated.  This field may ignored (but would be zero) for Management buffers.</summary>
        public ushort MessageStream;

        /// <summary>IEquatable{BufferHeaderV1} implementation method.  Returns true if both headers have the same contents.</summary>
        public bool Equals(BufferHeaderV1 other)
        {
            return (PurposeCode == other.PurposeCode
                    && Flags == other.Flags
                    && Length == other.Length
                    && SeqNum == other.SeqNum
                    && AckSeqNum == other.AckSeqNum
                    && MessageStream == other.MessageStream
                    );
        }

        /// <summary>Debugging and logging helper method</summary>
        public override string ToString()
        {
            string flagsStr = (Flags == BufferHeaderFlags.None) ? string.Empty : " flags:{0}".CheckedFormat(Flags);
            string seqStr = (SeqNum == 0) ? string.Empty : " seq:{0}".CheckedFormat(SeqNum);
            string ackStr = (AckSeqNum == 0) ? string.Empty : " ack:{0}".CheckedFormat(AckSeqNum);
            string mesgStrmStr = (MessageStream == 0 && PurposeCode == Buffers.PurposeCode.Management) ? string.Empty : " mStrm:{0}".CheckedFormat(MessageStream);

            return "{0}{1} hLen:{2}{3}{4}{5}".CheckedFormat(PurposeCode, flagsStr, Length, seqStr, ackStr, mesgStrmStr);
        }

        /// <summary>Returns true if this header is equal to a (default) one.</summary>
        public bool IsEmpty { get { return this.Equals(default(BufferHeaderV1)); } }

        /// <summary>Returns an empty (default) BufferHeaderV1</summary>
        public static BufferHeaderV1 Empty { get { return default(BufferHeaderV1); } }
    }

    /// <summary>
    /// This enumeration is used to define the purpose for the delivery of each buffer header.
    /// <para/>None (0x0000), IsBeingResent (0x0001), BufferContainsE005NVS (0x0002), 
    /// <para/>MessageContainsStreamSetup (0x0100), MessageContainsJsonNVS (0x0200), MessageContainsJsonString(0x0400)
    /// </summary>
    [Flags]
    public enum BufferHeaderFlags : ushort
    {
        /// <summary>Placeholder (default) value [0x0000]</summary>
        None = 0x0000,

        /// <summary>Used to indicate that this buffer is being resent (it has been sent before). [0x0001]</summary>
        BufferIsBeingResent = 0x0001,

        /// <summary>Used to indicate that this buffer contains an NVS serialized using E005. [0x0002]</summary>
        BufferContainsE005NVS = 0x0002,

        /// <summary>This flag is used on the first buffer in a message to indicate that the message is used to (re)initialize a stream. [0x0100]</summary>
        MessageContainsStreamSetup = 0x0100,

        /// <summary>This flag is used on the first buffer in a message to indicate that the message data contains an JSON serialized NVS [0x0200]</summary>
        MessageContainsJsonNVS = 0x0200,

        /// <summary>This flag is used on the first buffer in a message to indicate that the message data contains an JSON serialized string [0x0400]</summary>
        MessageContainsJsonString = 0x0400,
    }

    /// <summary>
    /// This enumeration serves two purposes (joke not intended) 
    /// - it defines the valid set of purposes that a buffer can be used for
    /// - and it is composed of values choosen to support use as magic numbers.
    /// <para/>None (0x00000000), Management (0xde47ea12), MessageStart (0xde47ea13), MessageMiddle (0xde47ea14), MessageEnd (0xde47ea15), Message (0xde47ea16)
    /// </summary>
    public enum PurposeCode : uint
    {
        /// <summary>Placeholder default value.  This value is used for Ack only buffers [0x00000000]</summary>
        None = 0x00000000,

        /// <summary>This buffer is being used to forward a management request (E005 NVS data). [0xde47ea12]</summary>
        Management = 0xde47ea12,

        /// <summary>This buffer is the first buffer in a message. [0xde47ea13]</summary>
        MessageStart = 0xde47ea13,

        /// <summary>This buffer is in the middle of a message. [0xde47ea14]</summary>
        MessageMiddle = 0xde47ea14,

        /// <summary>This buffer is the end of a message. [0xde47ea15]</summary>
        MessageEnd = 0xde47ea15,

        /// <summary>This buffer is the only buffer in a message (is is both the Start and the End). [0xde47ea16]</summary>
        Message = 0xde47ea16,

        /// <summary>This buffer is only used to carry an Ack code. [0xde47ea17]</summary>
        Ack = 0xde47ea17,
    }

    /// <summary>
    /// When a buffer carries a Management payload, it is expected to have NVS contents and in those contents it is expected to have a Type keyword with one of the following values to indicate the type of Management that is being performed.
    /// <para/>None (0), RequestOpenSession, RequestResumeSession, SessionRequestAcceptedResponse, NoteSessionTerminated, Status
    /// </summary>
    public enum ManagementType : uint
    {
        /// <summary>Placeholder default value (0)</summary>
        None = 0,

        /// <summary>Indicates sender's desire to start a new session using the current transport connection.  Required keys: Type, Name, SessionUUID</summary>
        RequestOpenSession,

        /// <summary>Indicates sender's desire to resume a previously open session using the current transport connection.  Required keys: Type, Name, SessionUUID, BufferSize</summary>
        RequestResumeSession,

        /// <summary>Indicates sender's acceptance of a prior open or resume ression request.  Required keys: Type, Name, SessionUUID, BufferSize</summary>
        SessionRequestAcceptedResponse,

        /// <summary>Indicates sender's (typically only clients send this) desire to close the session now.  Once received this is perminent.  Receiver sends back a NoteSessionTerminated.</summary>
        RequestCloseSession,

        /// <summary>Indicates that the session has been terminated.  Required keys: Type, Reason, optional keys: Name, SessionUUID (generally included)</summary>
        NoteSessionTerminated,

        /// <summary>Status update focused management payload.  Required keys: Type, supported keys: HeldBufferSeqNums</summary>
        Status,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given <paramref name="purposeCode"/> value is known</summary>
        public static bool IsValid(this PurposeCode purposeCode, bool includeManagement = true, bool includeMessages = true, bool includeAck = true)
        {
            switch (purposeCode)
            {
                case PurposeCode.Management:
                    return includeManagement;
                case PurposeCode.MessageStart:
                case PurposeCode.MessageMiddle:
                case PurposeCode.MessageEnd:
                case PurposeCode.Message:
                    return includeMessages;
                case PurposeCode.Ack:
                    return includeAck;
                default:
                    return false;
            }
        }
    }

    #endregion

    #region BufferState and Buffer

    /// <summary>
    /// Records the "state" of the buffer - generally indicates what the buffer last had done to it (or is about to have done to it).
    /// <para/>None (0), Created, Acquired, ReceivePosted, Received, Data, ReadyToSend, ReadyToResend, SendPosted, Sent, Delivered, Released
    /// </summary>
    public enum BufferState : int
    {
        None = 0,
        Created,
        Acquired,
        Clear,
        ReceivePosted,
        Received,
        Data,
        ReadyToSend,
        ReadyToResend,
        SendPosted,
        Sent,
        Delivered,
        Released,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true the given buffer <paramref name="state"/> is in one of the states that is valid for being sent as part of a message.
        /// <para/>Acquired, Created, or Data.
        /// </summary>
        public static bool IsReadyToPost(this BufferState state)
        {
            switch (state)
            {
                case BufferState.Acquired:
                case BufferState.Created:
                case BufferState.Data:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// This is the object that is used to manage and track buffers.
    /// <para/>Please note that the header is both present directly in this object and space is reserved at the beginning of the byte array for it to be copied into/out of immedaitely arround transport transfers of the buffer contents.
    /// </summary>
    public class Buffer : IEquatable<Buffer>
    {
        /// <summary>
        /// Constructs a buffer of the requested <paramref name="bufferSize"/>.
        /// </summary>
        public Buffer(int bufferSize, BufferPool bufferPool = null, Logging.IMesgEmitter stateEmitter = null)
            : this(stateEmitter)
        {
            this.bufferPool = bufferPool;

            byteArray = new byte [bufferSize];
            byteArraySize = bufferSize;

            byteCount = header.Length;
        }

        /// <summary>
        /// internal common constructor
        /// </summary>
        protected Buffer(Logging.IMesgEmitter stateEmitter)
        {
            StateEmitter = stateEmitter ?? Logging.NullMesgEmitter.Instance;
            BufferName = "Buffer_{0:x4}".CheckedFormat(nameInstanceNum & 0xffff);
        }

        public Buffers.BufferPool bufferPool;
        public Logging.IMesgEmitter StateEmitter { get; private set; }

        public static AtomicUInt64 nameInstanceNumGen = new AtomicUInt64();
        public readonly ulong nameInstanceNum = nameInstanceNumGen.Increment();
        public string BufferName { get; private set; }

        public BufferState State { get; private set; }
        public QpcTimeStamp TimeStamp { get; private set; }

        public ulong SeqNum 
        { 
            get { return header.SeqNum; } 
            set 
            { 
                header.SeqNum = value;

                // if this is an end of message buffer (Message or MessageEnd) and we have an associated Message then also set the Message's MesgLastBufferSeqNum to this value.
                if (((header.PurposeCode == Buffers.PurposeCode.Message) || (header.PurposeCode == Buffers.PurposeCode.MessageEnd)) && Message != null)
                    Message.LastBufferSeqNum = value;
            } 
        }
        public PurposeCode PurposeCode { get { return header.PurposeCode; } set { header.PurposeCode = value; } }
        public BufferHeaderFlags Flags { get { return header.Flags; } set { header.Flags = value; } }

        public Messages.Message Message { get; set; }
        public INotifyable NotifyOnSetState { get; set; }

        public BufferHeaderV1 header;
        public int byteArraySize;

        public int byteCount;
        public byte[] byteArray;

        private static readonly byte[] emptyByteArray = EmptyArrayFactory<byte>.Instance;

        public Buffer SetState(QpcTimeStamp qpcTimeStamp, BufferState state, string reason)
        {
            BufferState entryState = State;
            State = state;
            TimeStamp = qpcTimeStamp;

            switch (state)
            {
                case BufferState.SendPosted: CopyHeaderToByteArray(); break;
                case BufferState.Received: UpdateHeaderFromByteArray(); break;
                default: break;
            }

            if (StateEmitter.IsEnabled)
                StateEmitter.Emit("{0} State changed to {1} [from: {2}, reason: {3}]", BufferName, state, entryState, reason ?? "NoReasonGiven");

            INotifyable notifyOnStateChange = NotifyOnSetState;
            if (notifyOnStateChange != null)
                notifyOnStateChange.Notify();

            return this;
        }

        public Buffer Update(PurposeCode? purposeCode = null, BufferHeaderFlags? orInFlags = null, ulong? seqNum = null, int? byteCount = null, ulong? ackSeqNum = null, INamedValueSet buildPayloadDataFromE005NVS = null, bool throwOnException = false, byte[] copyPayloadDataFromByteArray = null, INotifyable notifyOnSetState = null)
        {
            if (purposeCode != null)
                header.PurposeCode = purposeCode ?? PurposeCode.None;

            if (orInFlags != null)
                header.Flags |= orInFlags ?? BufferHeaderFlags.None;

            if (seqNum != null)
                SeqNum = seqNum ?? 0;

            if (byteCount != null)
                this.byteCount = byteCount ?? 0;

            if (ackSeqNum != null)
                header.AckSeqNum = ackSeqNum ?? 0;

            if (buildPayloadDataFromE005NVS != null)
            {
                header.Flags |= BufferHeaderFlags.BufferContainsE005NVS;
                copyPayloadDataFromByteArray = buildPayloadDataFromE005NVS.ConvertToE005Data(throwOnException: throwOnException);
            }

            if (copyPayloadDataFromByteArray != null)
            {
                byteArray.SafeCopyFrom(header.Length, copyPayloadDataFromByteArray);
                this.byteCount = Math.Min(header.Length + copyPayloadDataFromByteArray.Length, byteArraySize);
            }

            if (notifyOnSetState != null)
                NotifyOnSetState = notifyOnSetState;

            return this;
        }

        public INamedValueSet GetPayloadAsE005NVS(INamedValueSet fallbackValue = null)
        {
            if ((Flags & BufferHeaderFlags.BufferContainsE005NVS) != 0)
            {
                int startAtIndex = header.Length;
                string ec = null;
                INamedValueSet nvs = NamedValueSet.Empty.ConvertFromE005Data(byteArray, ref startAtIndex, ref ec);

                if (ec.IsNullOrEmpty() && startAtIndex == byteCount)
                    return nvs;
            }

            return fallbackValue;
        }

        /// <summary>
        /// Asks the buffer to return itself to the pool from which it was allocated.
        /// </summary>
        public void ReturnToPool(QpcTimeStamp qpcTimeStamp, string reason = null)
        {
            if (bufferPool != null)
                bufferPool.Release(qpcTimeStamp, this, reason);
            else
                Release(qpcTimeStamp, reason);
        }

        /// <summary>
        /// Sets the byteCount to the headerSize and sets the header's BufferSeqNum to zero.
        /// </summary>
        public void Clear(QpcTimeStamp qpcTimeStamp, bool clearByteArrayContents = false, string reason = null)
        {
            header = BufferHeaderV1.Empty;
            byteCount = header.Length;
            Message = null;
            NotifyOnSetState = null;

            if (clearByteArrayContents)
                System.Array.Clear(byteArray, 0, byteArraySize);

            SetState(qpcTimeStamp, Buffers.BufferState.Clear, reason);
        }

        /// <summary>
        /// Used to indicate that this buffer is being abandoned so that its storage may be relcaimed during a future GC cycle.
        /// Sets the byteArray to null and the byteArraySize to zero.
        /// </summary>
        public void Release(QpcTimeStamp qpcTimeStamp, string reason = null)
        {
            header = BufferHeaderV1.Empty;

            bufferPool = null;
            byteArray = null;
            byteArraySize = 0;

            SetState(qpcTimeStamp, Buffers.BufferState.Released, reason);
        }

        /// <summary>
        /// Returns byteArraySize - byteCount (clipped to be no less than zero)
        /// </summary>
        public int AvailableSpace { get { return Math.Max(0, byteArraySize - byteCount); } }

        /// <summary>
        /// Copies the contents of the header into the buffer's byteArray.
        /// </summary>
        public void CopyHeaderToByteArray()
        {
            header.MarshalStructIntoToByteArray(byteArray, rethrow: false);
        }

        /// <summary>
        /// Copies the initial contents of the byteArray into the header
        /// </summary>
        public void UpdateHeaderFromByteArray()
        {
            header = byteArray.MarshalStructFromByteArray<BufferHeaderV1>(rethrow: false);
        }

        /// <summary>
        /// IEquatable{Buffer} implementation method.  Returns true if both buffers have the same contents.  Ignores header.Length bytes at the front of both byteArrays (as one may have been updated when the other has not).
        /// </summary>
        public bool Equals(Buffer other)
        {
            return Equals(other, compareTimeStamps: false);
        }

        /// <summary>
        /// IEquatable{Buffer} implementation method.  Returns true if both buffers have the same contents.  Ignores header.Length bytes at the front of both byteArrays (as one may have been updated when the other has not).
        /// </summary>
        public bool Equals(Buffer other, bool compareTimeStamps)
        {
            int headerSize = header.Length;

            bool isEqual = (other != null
                            && header.Equals(other.header)
                            && byteCount == other.byteCount
                            && ((byteCount <= headerSize) || byteArray.Skip(headerSize).Take(byteCount - headerSize).SequenceEqual(other.byteArray.Skip(headerSize).Take(other.byteCount-headerSize)))
                            && State == other.State
                            && (TimeStamp == other.TimeStamp || !compareTimeStamps)
                            );

            return isEqual;
        }

        public override string ToString()
        {
            if (Flags.IsSet(BufferHeaderFlags.BufferContainsE005NVS))
                return "{0} State:{1} Header:{2} [E005NVS {3}:{4}]".CheckedFormat(BufferName, State, header, byteCount, GetPayloadAsE005NVS().SafeToStringSML());

            if (PurposeCode == Buffers.PurposeCode.Message && Flags.IsAnySet(BufferHeaderFlags.MessageContainsJsonNVS | BufferHeaderFlags.MessageContainsJsonString))
            {
                string str = ByteArrayTranscoders.ByteStringTranscoder.Encode(byteArray, header.Length, byteCount - header.Length).GenerateSquareBracketEscapedVersion();

                return "{0} State:{1} Header:{2} [Str {3}:{4}]".CheckedFormat(BufferName, State, header, byteCount, str);
            }

            return "{0} State:{1} Header:{2} [Data {3}:{4}]".CheckedFormat(BufferName, State, header, byteCount, ByteArrayTranscoders.HexStringTranscoder.Encode(byteArray, header.Length, Math.Min(40, byteCount - header.Length)));
        }
    }

    #endregion

    #region BufferPool

    /// <summary>
    /// This is the object type that is used to manage reclamation of Buffers after their use in a Message has been completed so that they can be reused later.  
    /// This is also used as a factory objects for Buffer objects and will produce such objects using a configured buffer size.
    /// NOTE: this object is no thread safe - it may only be safely used by a single thread (at a time).
    /// </summary>
    public class BufferPool
    {
        /// <summary>Default buffer size 1024.  Nominal default usable size is 1024 - 26 == 998)</summary>
        const int defaultBufferSize = 1024;

        /// <summary>
        /// Constructor.
        /// <para/>defaultBufferSize == 1024.  Usable space is 1024-26 == 998
        /// </summary>
        public BufferPool(int maxTotalSpaceInBytes = 1024000, int bufferSize = defaultBufferSize, bool clearBufferContentsOnRelease = false, Logging.IMesgEmitter bufferStateEmitter = null, INamedValueSet configNVS = null, string configNVSKeyPrefix = "BufferPool.")
        {
            if (configNVS != null)
            {
                maxTotalSpaceInBytes = configNVS["{0}MaxTotalSpaceInBytes".CheckedFormat(configNVSKeyPrefix)].VC.GetValue(defaultValue: maxTotalSpaceInBytes, rethrow: false);
                bufferSize = configNVS["{0}BufferSize".CheckedFormat(configNVSKeyPrefix)].VC.GetValue(defaultValue: bufferSize, rethrow: false);
            }

            MaxTotalSpaceInBytes = maxTotalSpaceInBytes;
            ChangeBufferSize(QpcTimeStamp.Now, bufferSize);
            _bufferSize = bufferSize;
            ClearBufferContentsOnRelease = clearBufferContentsOnRelease;
            BufferStateEmitter = bufferStateEmitter;

            bufferCount = 0;
        }

        /// <summary>
        /// Use this method to "drain the pool".  This removes all of the buffers from the pool and Releases each of them.
        /// </summary>
        public void Drain(QpcTimeStamp qpcTimeStamp, string reason = null)
        {
            Buffer[] capturedBufferArray = capturedBufferArray = bufferArray.SafeToArray();

            bufferArray.SetAll(null);
            bufferCount = 0;

            capturedBufferArray.Where(buffer => buffer != null).DoForEach(buffer => buffer.Release(qpcTimeStamp));
        }

        public int MaxTotalSpaceInBytes { get; private set; }
        public int BufferSize { get { return _bufferSize; } set { if (_bufferSize != value) ChangeBufferSize(QpcTimeStamp.Now, value); } }
        [Obsolete("This property is no longer used (2018-08-16)")]
        public int AutomaticallyAddHeaderSizeAtOrBelow { get; private set; }
        public bool ClearBufferContentsOnRelease { get; private set; }
        public Logging.IMesgEmitter BufferStateEmitter { get; set; }

        public int MaxBuffersToRetain { get; private set; }

        private int _bufferSize;

        private void ChangeBufferSize(QpcTimeStamp qpcTimeStamp, int value)
        {
            Drain(qpcTimeStamp);

            _bufferSize = value.Clip(128, 16384);

            MaxBuffersToRetain = Math.Max(5, MaxTotalSpaceInBytes / _bufferSize);

            bufferArray = new Buffer[MaxBuffersToRetain];
        }

        private Buffer[] bufferArray;
        private volatile int bufferCount;

        /// <summary>
        /// This obtains a buffer instance from the pool, or creates a new one if the pool is empty.
        /// </summary>
        public Buffer Acquire(QpcTimeStamp qpcTimeStamp, string reason = null)
        {
            if (bufferCount > 0)
            {
                int bufferIndex = --bufferCount;

                Buffer buffer = bufferArray[bufferIndex];
                bufferArray[bufferIndex] = null;

                return buffer.SetState(qpcTimeStamp, BufferState.Acquired, reason ?? "BufferPool.{0}.1".CheckedFormat(Fcns.CurrentMethodName));
            }
            else
            {
                Buffer buffer = new Buffer(bufferSize: BufferSize, bufferPool: this, stateEmitter: BufferStateEmitter)
                                .SetState(qpcTimeStamp, BufferState.Created, reason ?? "BufferPool.{0}.2".CheckedFormat(Fcns.CurrentMethodName));

                return buffer;
            }
        }

        /// <summary>
        /// This returns the given <paramref name="buffer"/> to the pool provided that it was originally obtained from this pool and provided that the pool is not already full.
        /// </summary>
        public void Release(QpcTimeStamp qpcTimeStamp, Buffer buffer, string reason)
        {
            if (buffer == null)
            { }
            else if (buffer.bufferPool == this && buffer.byteArraySize >= BufferSize && bufferCount < MaxBuffersToRetain)
            {
                buffer.Clear(qpcTimeStamp, clearByteArrayContents: ClearBufferContentsOnRelease, reason: reason ?? "BufferPool.{0}.1".CheckedFormat(Fcns.CurrentMethodName));

                int bufferIndex = bufferCount++;

                bufferArray[bufferIndex] = buffer;
            }
            else
            {
                buffer.Release(qpcTimeStamp, reason ?? "BufferPool.{0}.1".CheckedFormat(Fcns.CurrentMethodName));
            }
        }
    }

    #endregion
}

//-------------------------------------------------------------------
