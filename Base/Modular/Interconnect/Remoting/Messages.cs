//-------------------------------------------------------------------
/*! @file Interconnect/Remoting/Messages.cs
 *  @brief Common Message related definitions for Modular.Interconnect.Remoting.Messages
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

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Remoting.Buffers;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

// Please note: see comments in for MosaicLib.Modular.Interconnect.Remoting in Remoting.cs

namespace MosaicLib.Modular.Interconnect.Remoting.Messages
{
    /// <summary>
    /// Records the "state" of a message (aka a set of 0 or more buffers)
    /// <para/>Initial (0), Data, SendPosted, Sent, Delivered, Received, Released, Failed
    /// </summary>
    public enum MessageState : int
    {
        Initial = 0,
        Data,
        SendPosted,
        Sent,
        Delivered,
        Received,
        Released,
        Failed,
    }

    /// <summary>
    /// Instances of this type generally consist of a set of 0 or more buffers.  Useful messages need to have at least one buffer as empty messages only have local meaning and cannot be transfered through a remoting session.  
    /// These instances are generally used as serialization targets and they support generation of Stream objects that can be used to read from the message's buffer list or to write to (append to) the messages buffer list.
    /// These stream objects are generally expected to be used with external serialization tools so that serialization can be performed directly into, and deserialization directly from, the chains of buffers that are managed by a message instance.
    /// </summary>
    public class Message
    {
        public Message(Buffers.BufferPool bufferSourcePool, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter stateEmitter = null)
        {
            this.bufferSourcePool = bufferSourcePool;
            StateEmitter = stateEmitter ?? bufferSourcePool.BufferStateEmitter ?? Logging.NullMesgEmitter.Instance;
            IssueEmitter = issueEmitter ?? StateEmitter;
        }

        public Buffers.BufferPool bufferSourcePool;
        public Logging.IMesgEmitter IssueEmitter { get; private set; }
        public Logging.IMesgEmitter StateEmitter { get; private set; }

        public List<Buffers.Buffer> bufferList = new List<Buffers.Buffer>(4);

        public static AtomicUInt64 instanceNumGen = new AtomicUInt64();
        public readonly ulong instanceNum = instanceNumGen.Increment();
        public ulong MesgSeqNum { get { return instanceNum; } }

        public ulong LastBufferSeqNum { get; set; }

        public MessageState State { get; private set; }
        public QpcTimeStamp TimeStamp { get; private set; }
        public QpcTimeStamp SendPostedTimeStamp { get; private set; }
        public string Reason { get; private set; }

        public BufferHeaderFlags FirstBufferFlags 
        { 
            get
            {
                Buffers.Buffer firstBuffer = bufferList.SafeAccess(0);
                return (firstBuffer != null) ? firstBuffer.Flags : BufferHeaderFlags.None;
            } 
        }

        public Message SetState(QpcTimeStamp qpcTimeStamp, MessageState state, string reason, bool autoReleaseByState = false)
        {
            MessageState entryState = State;

            reason = reason ?? "NoReasonGiven";

            State = state;
            TimeStamp = qpcTimeStamp;
            Reason = reason;

            Logging.IMesgEmitter emitterToUse = StateEmitter;

            switch (state)
            {
                case MessageState.Initial:
                    ReturnBuffersToPool(qpcTimeStamp, "Message.State set to Initial");
                    break;
                case MessageState.Released:
                    ReturnBuffersToPool(qpcTimeStamp, "Message.State set to Released");
                    break;
                case MessageState.Delivered:
                    if (autoReleaseByState)
                        ReturnBuffersToPool(qpcTimeStamp, "Message.State set to Deliverd and autoReleaseByState is true");
                    break;
                case MessageState.Failed:
                    emitterToUse = IssueEmitter;
                    break;
                case MessageState.SendPosted:
                    SendPostedTimeStamp = qpcTimeStamp;
                    break;
                default: 
                    break;
            }

            if (emitterToUse.IsEnabled)
                emitterToUse.Emit("Message_{0:x4} State changed to {1} [from: {2}, reason: {3}]", instanceNum & 0x0ffff, state, entryState, reason);

            return this;
        }

        /// <summary>Gives the total payload byte count of all of the buffers in the message, excluding the header size in each such buffer</summary>
        public int ByteCount
        {
            get
            {
                int bufferListCount = bufferList.Count;
                int byteCount = 0;

                for (int bufferListIdx = 0; bufferListIdx < bufferListCount; bufferListIdx++)
                {
                    var buffer = bufferList[bufferListIdx];
                    if (buffer != null)
                        byteCount += (buffer.byteArraySize - buffer.header.Length);
                }

                return byteCount;
            }
        }

        public byte [] ByteArray
        {
            get 
            {
                int byteCount = ByteCount;

                byte[] byteArray = new byte[byteCount];

                using (var mrs = MessageReadingStream)
                    mrs.Read(byteArray, 0, byteCount);

                return byteArray;
            }
            set
            {
                SetContents(QpcTimeStamp.Now, value, 0, value.SafeLength());
            }
        }

        public Message SetContents(QpcTimeStamp qpcTimeStamp, byte[] byteArray, int offset, int count)
        {
            ReturnBuffersToPool(qpcTimeStamp, "Message.SetContents called");

            if (count > 0)
            {
                using (var mbs = MessageBuildingStream)
                    mbs.Write(byteArray, offset, count);
            }

            return this;
        }

        public INamedValueSet NVS
        {
            get 
            {
                if (FirstBufferFlags.IsSet(BufferHeaderFlags.MessageContainsJsonNVS))
                {
                    try
                    {
                        using (var mrs = MessageReadingStream)
                            return new DataContractJsonAdapter<NamedValueSet>().ReadObject(mrs);
                    }
                    catch (System.Exception ex)
                    {
                        IssueEmitter.Emit("Attempt to get NVS from {0} failed: {1}", this, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                    }
                }
                else
                {
                    IssueEmitter.Emit("Attempt to get NVS from {0} failed: message is not flagged as containing an NVS", this);
                }

                return NamedValueSet.Empty;
            }

            set 
            {
                try
                {
                    using (var mbs = MessageBuildingStream)
                        new DataContractJsonAdapter<NamedValueSet>().WriteObject(value.ConvertToReadOnly(mapNullToEmpty: false), mbs);

                    Buffers.Buffer firstBuffer = bufferList.SafeAccess(0);
                    if (firstBuffer != null)
                        firstBuffer.Update(orInFlags: BufferHeaderFlags.MessageContainsJsonNVS);
                }
                catch (System.Exception ex)
                {
                    IssueEmitter.Emit("Attempt to set {0} NVS failed: {1}", this, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                }
            }
        }

        public string String
        {
            get
            {
                if (FirstBufferFlags.IsSet(BufferHeaderFlags.MessageContainsJsonString))
                {
                    try
                    {
                        using (var mrs = MessageReadingStream)
                            return new DataContractJsonAdapter<string>().ReadObject(mrs);
                    }
                    catch (System.Exception ex)
                    {
                        IssueEmitter.Emit("Attempt to get String from {0} failed: {1}", this, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                    }
                }
                else
                {
                    IssueEmitter.Emit("Attempt to get String from {0} failed: message is not flagged as containing an String", this);
                }

                return string.Empty;
            }

            set
            {
                try
                {
                    using (var mbs = MessageBuildingStream)
                        new DataContractJsonAdapter<string>().WriteObject(value, mbs);

                    Buffers.Buffer firstBuffer = bufferList.SafeAccess(0);
                    if (firstBuffer != null)
                        firstBuffer.Update(orInFlags: BufferHeaderFlags.MessageContainsJsonString);
                }
                catch (System.Exception ex)
                {
                    IssueEmitter.Emit("Attempt to set {0} String failed: {1}", this, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                }
            }
        }

        public Message Update(INamedValueSet buildPayloadDataFromNVS = null, string buildPayloadDataFromString = null, BufferHeaderFlags? orInFlags = null)
        {
            if (buildPayloadDataFromNVS != null)
                NVS = buildPayloadDataFromNVS;

            if (buildPayloadDataFromString != null)
                String = buildPayloadDataFromString;

            if (orInFlags != null)
            {
                Buffers.Buffer firstBuffer = bufferList.SafeAccess(0);
                if (firstBuffer != null)
                    firstBuffer.Update(orInFlags: orInFlags);
            }

            return this;
        }

        public override string ToString()
        {
            if (FirstBufferFlags.IsSet(BufferHeaderFlags.MessageContainsJsonNVS))
                return "Message_{0:x4} {1} {2} {3} {4}".CheckedFormat(instanceNum & 0xffff, State, FirstBufferFlags, ByteCount, NVS.SafeToStringSML());
            else if (FirstBufferFlags.IsSet(BufferHeaderFlags.MessageContainsJsonString))
                return "Message_{0:x4} {1} {2} {3} [{4}]".CheckedFormat(instanceNum & 0xffff, State, FirstBufferFlags, ByteCount, String);
            else
                return "Message_{0:x4} {1} {2} {3} [{4}]".CheckedFormat(instanceNum & 0xffff, State, FirstBufferFlags, ByteCount, ByteArrayTranscoders.ByteStringTranscoder.Encode(ByteArray).GenerateSquareBracketEscapedVersion());
        }

        public static readonly Buffers.BufferPool fallbackDefaultBufferPool = new BufferPool("FallbackDefaultBufferPool");

        public Message Clear(QpcTimeStamp qpcTimeStamp)
        {
            SetState(qpcTimeStamp, MessageState.Initial, "Clear");

            return this;
        }

        public void ReturnBuffersToPool(QpcTimeStamp qpcTimeStamp, string reason = null)
        {
            var bufferListCount = bufferList.Count;
            if (bufferListCount > 0)
            {
                for (int index = 0; index < bufferListCount; index++)
                    bufferList[index].ReturnToPool(qpcTimeStamp, reason);

                bufferList.Clear();
            }
        }

        /// <summary>
        /// Returns an System.IO.Stream that can be used to writing payload bytes into this message.  Generally this is used with a serialization helper object.
        /// </summary>
        public System.IO.Stream MessageBuildingStream { get { return new BufferListWriteStream(this); } }

        /// <summary>
        /// Returns a System.IO.Stream that can be used to read the payload bytes that are in this message.  Generally this is used with a serialization helper object.
        /// </summary>
        public System.IO.Stream MessageReadingStream { get { return new BufferListReadStream(this); } }

        private class BufferListReadStream : System.IO.Stream
        {
            public BufferListReadStream(Message message)
            {
                this.message = message;
                this.bufferList = (message.bufferList as IList<Buffers.Buffer>) ?? ReadOnlyIList<Buffers.Buffer>.Empty;
                currentBuffer = null;
            }

            Messages.Message message;
            private IList<Buffers.Buffer> bufferList;
            private Buffers.Buffer currentBuffer = null;
            private int currentBufferListIndex = -1;
            private int currentBufferReadOffset = 0;
            private int currentBufferRemainingCount = 0;

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override void Flush() { throw new System.NotImplementedException(); }
            public override long Length { get { throw new NotImplementedException(); } }
            public override long Position { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (!buffer.IsSafeIndex(offset, length: count))
                    new System.ArgumentException("Invalid offset/count combination [bufferSize:{0} offset:{1} count:{2}]".CheckedFormat(buffer.SafeLength(), offset, count)).Throw();

                int putToOffset = offset;

                int totalCountTransferred = 0;

                for (; totalCountTransferred < count; )
                {
                    if (currentBuffer == null)
                    {
                        currentBufferListIndex++;
                        if (currentBufferListIndex < bufferList.SafeCount())
                        {
                            currentBuffer = bufferList[currentBufferListIndex];
                            currentBufferReadOffset = currentBuffer.header.Length;
                            currentBufferRemainingCount = Math.Max(0, currentBuffer.byteCount - currentBufferReadOffset);
                        }
                    }

                    if (currentBuffer == null)
                        break;

                    int iterCopyCount = Math.Min(count - totalCountTransferred, currentBufferRemainingCount);
                    byte [] copyFromBuffer = currentBuffer.byteArray;

                    if (iterCopyCount <= 8)
                    {
                        for (int idx = 0; idx < iterCopyCount; idx++)
                            buffer[putToOffset++] = copyFromBuffer[currentBufferReadOffset++];
                    }
                    else
                    {
                        System.Buffer.BlockCopy(copyFromBuffer, currentBufferReadOffset, buffer, putToOffset, iterCopyCount);

                        currentBufferReadOffset += iterCopyCount;
                        putToOffset += iterCopyCount;
                    }

                    currentBufferRemainingCount -= iterCopyCount;
                    totalCountTransferred += iterCopyCount;

                    if (currentBufferRemainingCount <= 0)
                        currentBuffer = null;
                }

                return totalCountTransferred;
            }

            public override long Seek(long offset, System.IO.SeekOrigin origin) { throw new NotImplementedException(); }

            public override void SetLength(long value) { throw new NotImplementedException(); }

            public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }
        }

        private class BufferListWriteStream : System.IO.Stream
        {
            public BufferListWriteStream(Messages.Message message)
            {
                this.message = message;
                bufferList = message.bufferList ?? new List<Buffers.Buffer>();

                var bufferListCount = bufferList.Count;

                currentBuffer = (bufferListCount > 0) ? bufferList[bufferListCount - 1] : null;
            }

            Messages.Message message;
            private IList<Buffers.Buffer> bufferList;
            private Buffers.Buffer currentBuffer = null;

            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override void Flush() { }
            public override long Length { get { throw new NotImplementedException(); } }

            public override long Position { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

            public override int Read(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }

            public override long Seek(long offset, System.IO.SeekOrigin origin) { throw new NotImplementedException(); }

            public override void SetLength(long value) { throw new NotImplementedException(); }

            public override void Write(byte[] buffer, int offset, int count) 
            {
                if (!buffer.IsSafeIndex(offset, length: count))
                    new System.ArgumentException("Invalid offset/count combination [bufferSize:{0} offset:{1} count:{2}]".CheckedFormat(buffer.SafeLength(), offset, count)).Throw();

                Buffers.BufferPool useBufferPool = message.bufferSourcePool ?? fallbackDefaultBufferPool;

                int getFromOffset = offset;

                int totalCountTransferred = 0;

                for (; totalCountTransferred < count; )
                {
                    if (currentBuffer == null)
                    {
                        QpcTimeStamp tsNow = QpcTimeStamp.Now;

                        currentBuffer = useBufferPool.Acquire(tsNow);

                        if (currentBuffer == null)
                            new System.OutOfMemoryException("Unable to acquire a buffer from the message bufferPool").Throw();

                        bufferList.Add(currentBuffer);

                        if (message.State == MessageState.Initial)
                            message.SetState(tsNow, MessageState.Data, "BufferListWriteStream.Write");
                    }

                    int iterCopyCount = Math.Min(count - totalCountTransferred, currentBuffer.AvailableSpace);

                    byte[] copyToBuffer = currentBuffer.byteArray;

                    if (iterCopyCount <= 8)
                    {
                        int copyToOffset = currentBuffer.byteCount;

                        for (int idx = 0; idx < iterCopyCount; idx++)
                            copyToBuffer[copyToOffset++] = buffer[getFromOffset++];
                    }
                    else
                    {
                        System.Buffer.BlockCopy(buffer, getFromOffset, copyToBuffer, currentBuffer.byteCount, iterCopyCount);

                        getFromOffset += iterCopyCount;
                    }

                    currentBuffer.byteCount += iterCopyCount;
                    if (currentBuffer.AvailableSpace <= 0)
                        currentBuffer = null;

                    totalCountTransferred += iterCopyCount;
                }
            }
        }
    }
}

//-------------------------------------------------------------------
