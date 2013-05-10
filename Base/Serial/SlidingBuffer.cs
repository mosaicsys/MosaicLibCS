//-------------------------------------------------------------------
/*! @file SlidingBuffer.cs
 *  @brief This file defines the SlidingBuffer and SlidingLineBuffer classes that are used for packet/line reassembly on serial device drivers.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2002 Mosaic Systems Inc., All rights reserved. (C++ library version, found under Utils)
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

namespace MosaicLib.SerialIO
{
	//-------------------------------------------------------------------

    using System;
    using System.Collections;
    using System.Collections.Generic;
	using MosaicLib.Time;

    //-------------------------------------------------------------------

    #region basic SlidingBuffer

    /// <summary>Utility class provided to simplify buffer management related to chunking a stream of incoming characters into groups or frames.</summary>
	/// <remarks>
	/// This class implements a single fixed buffer, into which, characters are placed in an asynchronous and intermittant bassis (as they are received) and from 
	/// which characters are consumed in deliniated groups such as packets.  The sliding buffer uses a single linear contiguous buffer for ease in access and
	/// performs occasional down copy when there is not enough space in the buffer to place newly incoming charactes.  This linear buffer is self resetting so that
	/// consumption of the last character in the buffer (at any location in it) automatically resets the buffer pointers so that the next character placed in the
	/// buffer is placed at its first location.  This optimization help prevent the need for down copying for most casses on packetized communication systems since
	/// there is frequently reception gaps between packets.
	/// </remarks>

	public class SlidingBuffer
	{
        /// <summary>Constructor</summary>
        /// <param name="size">defines the size of the buffer and thus the maximum number of characters that may be stored at one time.</param>
		public SlidingBuffer(uint size)
		{
			buffer = new byte [size];
		}

        /// <summary>Gives the caller access to the internal buffer for purposes of extracting data from it.</summary>
        /// <param name="buffer">Passes out a reference to the underlying byte array buffer</param>
        /// <param name="nextGetIdx">Passes out the index that marks the offset into the buffer at which the next unused byte is found</param>
        /// <param name="availableByteCount">Passes out the number of bytes that are available at the nextGetIdx offset.</param>
        public virtual void GetBufferGetAccessInfo(out byte[] buffer, out int nextGetIdx, out int availableByteCount)
		{
			buffer = this.buffer;
			nextGetIdx = getIdx;
			availableByteCount = BufferDataCount;
		}

        /// <summary>Gives the caller access to the internal buffer for purposes of adding data to it.</summary>
        /// <param name="desiredSpace">Tells the SlidingBuffer how much space the caller would like to have to append new bytes into the buffer.  Buffer will align itself if needed to attempt to provide the requested space.</param>
        /// <param name="buffer">Passes out a reference to the underlying byte array buffer</param>
        /// <param name="nextPutIdx">Passes out the index that new bytes should be copied to in order to append them to the buffer</param>
        /// <param name="spaceRemaining">Passes out the amount of space that is remaining in the buffer at and after the nextPutIdx offset</param>
        /// <remarks>
        /// All buffer access by the caller must be complete before any thread invokes any other method on this buffer.  
        /// Caller is expected to calls AddedNChars after appending bytes to the buffer.
        /// </remarks>
        public virtual void GetBufferPutAccessInfo(int desiredSpace, out byte[] buffer, out int nextPutIdx, out int spaceRemaining)
		{
			buffer = this.buffer;
			spaceRemaining = BufferDataSpaceRemaining;

			if (putIdx != 0 && desiredSpace > spaceRemaining)
			{
				AlignBuffer();
				spaceRemaining = BufferDataSpaceRemaining;
			}

			nextPutIdx = putIdx;
		}

        /// <summary></summary>
        public virtual void UsedNChars(int n)
		{
			getIdx += n;
			getTimeStamp = QpcTimeStamp.Now;

			if (BufferEmpty)
				ResetBuffer();
			else if (BufferDataSpaceRemaining <= 0)
				AlignBuffer();
		}

        /// <summary>Empties the buffer by discarding any data that is currently in it.</summary>
        public void FlushBuffer() { ResetBuffer(); }

        /// <summary>Shifts any existing buffered data down so that the first byte in the buffer is obtained from index zero.  This makes the space available be as large as possible.</summary>
        public virtual void AlignBuffer()
		{
            if (!BufferEmpty)
            {
                if (getIdx != 0)
                {
                    int shiftByN = getIdx;
                    System.Buffer.BlockCopy(buffer, shiftByN, buffer, 0, BufferDataCount);
                    putIdx -= shiftByN;
                    getIdx = 0;

                    BufferHasBeenShifted(shiftByN);
                }

                getTimeStamp = putTimeStamp;	// over estimate the get timestamp on a shift (act as if some bytes were obtained during the shift).
            }
            else
            {
                // buffer is empty - just reset it.
                ResetBuffer();
            }
		}

        /// <summary>Caller is indicating that characters have been added to the buffer.</summary>
        /// <param name="n">Gives the number of bytes that have been appended/copied into the buffer by the caller.</param>
        public virtual void AddedNChars(int n)
        {
            putIdx += n;
            putTimeStamp = QpcTimeStamp.Now;

            BufferHasBeenIncreased(n);
        }

        /// <summary>Clears the buffer contents, aligns the empty buffer and updates the get and put timestamps.</summary>
        protected virtual void ResetBuffer()
        {
            QpcTimeStamp now = QpcTimeStamp.Now;

            getIdx = 0;
            getTimeStamp = now;
            putIdx = 0;
            putTimeStamp = now;

            BufferHasBeenReset();
        }

        /// <summary>Virtual method use to tell derived classes when buffer has had n characters added to it.</summary>
        protected virtual void BufferHasBeenIncreased(int nCharsAdded)
        {
        }

        /// <summary>Virtual method use to tell derived classes when buffer has been shifted by n characters.</summary>
        protected virtual void BufferHasBeenShifted(int byN)
        {
        }

        /// <summary>Virtual method use to tell derived classes when buffer has been reset (is now empty and get and put idx are now zero.</summary>
        protected virtual void BufferHasBeenReset()
        {
        }

        /// <summary>Returns the number of bytes that are currently held in the buffer.  (putIdx - getIdx)</summary>
        public int BufferDataCount { get { return (putIdx - getIdx); } }
        /// <summary>Returns true if BufferDataCount is zero</summary>
        public bool BufferEmpty { get { return (putIdx <= getIdx); } }
        /// <summary>Returns the QpcTimeStamp from the last time that bytes were pulled from the buffer, or put to the buffer after it has been aligned, or the buffer was reset.</summary>
        public QpcTimeStamp ContentGetTime { get { return getTimeStamp; } }	// >= timestamp of head char
        /// <summary>Returns the QpcTimeStamp from the last time that bytes were appended into the buffer or the buffer was reset.</summary>
        public QpcTimeStamp ContentPutTime { get { return putTimeStamp; } }	// = timestamp of tail char

        /// <summary>Returns the number of bytes that can be added to the buffer before it would become full.</summary>
        public int BufferDataSpaceRemaining { get { return (buffer.Length - putIdx); } }

		protected byte []		buffer;

		protected int			getIdx = 0;
		protected QpcTimeStamp	getTimeStamp = QpcTimeStamp.Zero;

		protected int			putIdx = 0;
		protected QpcTimeStamp	putTimeStamp = QpcTimeStamp.Zero;
    }

    #endregion

    //-------------------------------------------------------------------

    #region SlidingPacketBuffer

    #region SlidingPacketBuffer support types

    /// <summary>Enum used to define various types of contents (or meanings) that a SlidingPacketBuffer Packet can contain/conveigh</summary>
    public enum PacketType
    {
        /// <summary>Default constructor value</summary>
	    Null = 0,
        /// <summary>no data was read or was available</summary>
	    None,
        /// <summary>data was obtained and is included</summary>
	    Data,
        /// <summary>data was obtained: all of it was whitespace and was dicarded</summary>
	    WhiteSpace,
        /// <summary>packet represents data that has been/is being flushed from the port</summary>
	    Flushed,
        /// <summary>packet contains partial data but no packet end pattern was found before packet accumulation time limit was reached.</summary>
	    Timeout,
        /// <summary>packet contains an error code</summary>
	    Error,			//!< packet data contains an error code.
    };

    /// <summary>
    /// General class used to store a packet as extracted from a SlidingPacketBuffer.  
    /// </summary>

    public class Packet
    {
        public Packet() : this(PacketType.None, null, String.Empty) { }
        public Packet(PacketType type) : this(type, null, String.Empty) { }
        public Packet(PacketType type, byte[] data) : this(type, data, String.Empty) { }
        public Packet(PacketType type, byte[] data, String errorCode) 
        {
            this.type = type;
            this.data = data;
            this.dataStr = null;
            this.errorCode = errorCode;
        }

        private PacketType type;
        private byte [] data;
        private string dataStr;
        private string errorCode;

        public PacketType Type { get { return type; } set { type = value; } }
        public byte[] Data { get { return data; } set { data = value; dataStr = null; } }
        public string DataStr { get { return ((dataStr != null) ? dataStr : (dataStr = Utils.ByteArrayTranscoders.ByteStringTranscoder.Encode(data))); } }
        public string ErrorCode { get { return (errorCode != null ? errorCode : String.Empty); } set { errorCode = value; } }

        public bool IsNullOrNone { get { return (type == PacketType.Null || type == PacketType.None); } }
        public bool IsWhiteSpace { get { return (type == PacketType.WhiteSpace); } }
        public bool IsData { get { return (type == PacketType.Data); } }
        public bool IsNonEmptyData { get { return (IsData && data != null && data.Length > 0); } }
        public bool IsError { get { return (type == PacketType.Error || type == PacketType.Timeout); } }

        public override bool Equals(object rhsAsObject)
        {
            Packet rhs = rhsAsObject as Packet;
            if (rhs == null)
                return false;

            return (type == rhs.type && data.Equals(rhs.data) && errorCode == rhs.errorCode);
        }

        public override int GetHashCode()
        {
 	         return base.GetHashCode();
        }

        private static byte[] emptyData = new byte[0];

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool allwaysIncludeData)
        {
            string typeAsStr = Type.ToString();
            byte[] dataTemp = (Data != null ? Data : emptyData);

            if (!allwaysIncludeData || IsError)
            {
                switch (type)
                {
                    case PacketType.Data:
                    case PacketType.Flushed:
                        return Utils.Fcns.CheckedFormat("{0} [{1} bytes]", typeAsStr, dataTemp.Length);
                    case PacketType.Error:
                    case PacketType.Timeout:
                        return Utils.Fcns.CheckedFormat("{0} ec:'{1}'", typeAsStr, ErrorCode);
                    default:
                        return typeAsStr;
                }
            }

            string dataAsStr = Utils.ByteArrayTranscoders.ByteStringTranscoder.Encode(dataTemp);

            if (ErrorCode == string.Empty)
                return Utils.Fcns.CheckedFormat("{0}: '{1}'", typeAsStr, dataAsStr);
            else
                return Utils.Fcns.CheckedFormat("{0}: '{1}' ec:'{2}'", typeAsStr, dataAsStr, ErrorCode);
        }
    }

    #endregion

    public class SlidingPacketBuffer : SlidingBuffer
    {
        /// <summary>Constructor</summary>
        /// <param name="size">gives the desired size of the buffer.  Defines the maximum number of bytes that can be in one packet.</param>
        /// <param name="packetEndStrArray">gives a list of one or more strings which define a packet end delimiter pattern.</param>
        /// <param name="packetTimeout">gives the maximum amount of time that may elpased from the last byte added before a valid packet end is found</param>
        public SlidingPacketBuffer(uint size, string[] packetEndStrArray, TimeSpan packetTimeout) 
            : this(size, packetEndStrArray, packetTimeout, true) { }

        public SlidingPacketBuffer(uint size, string[] packetEndStrArray, TimeSpan packetTimeout, bool stripWhitespace)
            : base(size)
        { 
            StripWhitespace = stripWhitespace; 
            PacketTimeout = packetTimeout; 
            PacketEndStrArray = packetEndStrArray; 
        }

        /// <summary>Returns true if NumPacketsReaday is not zero</summary>
        public bool HasPacket { get { return (NumPacketsReady != 0); } }

        /// <summary>Returns the number of packets that are currently in the queue of extracted packets</summary>
        public int NumPacketsReady { get { return extractedPacketQueue.Count; } }

        /// <summary>Attempts to extract the next packet from the queue and return it.  Returns null if there are currently no packets in the queue.</summary>
        /// <returns>next Packet from queue of extracted packets or null if there are none.</returns>
        public Packet GetNextPacket() { return GetNextPacket(false); }

        /// <summary>Attempts to extract the next packet from the queue and return it.  Returns null if there are currently no packets in the queue.</summary>
        /// <param name="flushBuffer">When true, if the buffer is not empty, the outstanding bytes will be placed into a flush packet which will be enqueued before attempting to extract and return the next packet from the queue.</param>
        /// <returns>next Packet from the queue of extracted packets or null if there are none.</returns>
        public Packet GetNextPacket(bool flushBuffer)
        {
            if (flushBuffer)
            {
                Service();

                int flushCount = BufferDataCount;

                if (flushCount > 0)
                {
                    // extract bytes from buffer and append into new packet
                    Packet p = new Packet(PacketType.Flushed, new byte[flushCount]);

                    System.Buffer.BlockCopy(buffer, getIdx, p.Data, 0, flushCount);

                    extractedPacketQueue.Enqueue(p);

                    UsedNChars(flushCount);
                }
            }

            if (extractedPacketQueue.Count != 0)
                return extractedPacketQueue.Dequeue();

            Service();

            if (extractedPacketQueue.Count != 0)
                return extractedPacketQueue.Dequeue();

            return null;
        }

        /// <summary>Allows the sliding buffer to implement timeout detection on any partially received packet or other unrecognized data.</summary>
        public void Service() { Service(false); }

        /// <summary>Implements scan for new packets (invoked internally after reception of new data) and implements stale rx data detection and handling when no new data has been received.</summary>
        public void Service(bool performFullRescan)
        {
            // check for unscanned data
            if (performFullRescan)
                scanAfterCount = 0;

            bool foundPacket = false;
            while (scanAfterCount < BufferDataCount)
            {
                // find shortest packet with end characters
                int shortestPacketLen = 0;
                int shortestScanLen = BufferDataCount;

                for (int packetEndIdx = 0; packetEndIdx < numPacketEndStrs; packetEndIdx++)
                {
                    int packetLen = 0;

                    byte [] patternBytes = packetEndByteArrayList[packetEndIdx];
                    int patternLen = patternBytes.Length;
                    int scanEndCount = (BufferDataCount - patternLen) + 1;
                    int scanEndIdx = getIdx + scanEndCount;

                    for (int patternScanIdx = (scanAfterCount + getIdx); patternScanIdx < scanEndIdx; patternScanIdx++)
                    {
                        int patternOffset = 0;
                        for (; patternOffset < patternLen; patternOffset++)
                        {
                            if (buffer[patternScanIdx + patternOffset] != patternBytes[patternOffset])
                                break;
                        }

                        if (patternOffset == patternLen)
                        {
                            packetLen = (patternScanIdx + patternOffset - getIdx);
                            break;
                        }
                    }

                    // take the shortest scan pattern count as the shortest scan length we have failed to find a match for
                    //  this then will allow us to start the next scan at just before this point in the buffer so that we do not rescan characters that are entirely known to not match any of the end chars.
                    if (shortestScanLen > scanEndCount)
                        shortestScanLen = scanEndCount;

                    if (packetLen > 0 && (shortestPacketLen == 0 || packetLen < shortestPacketLen))
                        shortestPacketLen = packetLen;
                }

                if (shortestPacketLen > 0)
                {
                    foundPacket = true;
                    scanAfterCount = 0;

                    int dataCopyStartIdx = getIdx;
                    int dataCopyLen = shortestPacketLen;

                    if (StripWhitespace)
                    {
                        while (dataCopyLen > 0)
                        {
                            if (!IsWhiteSpace(buffer[dataCopyStartIdx]))
                                break;

                            dataCopyStartIdx++;
                            dataCopyLen--;
                        }

                        while (dataCopyLen > 0)
                        {
                            if (!IsWhiteSpace(buffer[dataCopyStartIdx + dataCopyLen - 1]))
                                break;

                            dataCopyLen--;
                        }
                    }

                    // extract bytes from buffer and append into new packet
                    Packet p = new Packet(((dataCopyLen > 0) ? PacketType.Data : PacketType.WhiteSpace), new byte[dataCopyLen]);

                    if (dataCopyLen > 0)
                        System.Buffer.BlockCopy(buffer, dataCopyStartIdx, p.Data, 0, dataCopyLen);

                    extractedPacketQueue.Enqueue(p);

                    UsedNChars(shortestPacketLen);
                }
                else
                {
                    // Set the next scan to skip over most of the characters that have already been scanned.
                    // The use of -1 forces the pattern to keep rescanning for all of the patterns in case the longest end pattern has not been completely received at this point.
                    scanAfterCount = shortestScanLen - 1;
                    break;
                }
            }

            // drop leading whitespace from buffer immediately (this will prevent them from 
            if (StripWhitespace && BufferDataCount > 0)
            {
                if (IsWhiteSpace(buffer[getIdx]))
                    UsedNChars(1);
            }

            // check for timeout: when both the contentPutAge and the contentGetAge are larger than the PacketTimeout and it is not zero.
            if (!foundPacket && BufferDataCount > 0 && PacketTimeout != TimeSpan.Zero)
            {
                QpcTimeStamp now = QpcTimeStamp.Now;
                TimeSpan contentPutAge = now - ContentPutTime;
                TimeSpan contentGetAge = now - ContentGetTime;

                if (contentPutAge > PacketTimeout && contentGetAge > PacketTimeout)
                {
                    double contentAgeInSec = System.Math.Min(contentPutAge.TotalSeconds, contentGetAge.TotalSeconds);
                    string opStr = ((contentPutAge < contentGetAge) ? "Put" : "Get");

                    // transfer the current bytes in the sliding buffer into a new packet and reset the sliding buffer
                    Packet p = new Packet(PacketType.Timeout, new byte[BufferDataCount], Utils.Fcns.CheckedFormat("Timeout: {0} stale chars found in buffer {1:f3} seconds after most recent {2}", BufferDataCount, contentAgeInSec, opStr));
    				System.Buffer.BlockCopy(buffer, getIdx, p.Data, 0, BufferDataCount);

                    extractedPacketQueue.Enqueue(p);

                    UsedNChars(BufferDataCount);
                }
            }
        }

        /// <summary>Gets or set the current StripWhitespace flag.  When set to true, all leading and trailing white space is removed from packet data for Data packets.  Packet type will be changed to Whitespace if the resulting Data packet is empty.</summary>
        public bool StripWhitespace 
        { 
            get { return stripWhitespace; }
            set { stripWhitespace = value; Service(true); } 
        }

        /// <summary>Gets or sets the current PacketTimeout value.  This value is used during Service calls to determine if remaining bytes in the buffer have been idle for to long in which case a timeout packet is generated and enqueued to discard them.  When this value is set to zero, no local timeout handling is performed within the sliding buffer.</summary>
        public TimeSpan PacketTimeout
        {
            get { return packetTimeout; }
            set { packetTimeout = value; Service(true); }
        }

        /// <summary>Gets or sets the current set/array of non-null and non-empty packet end delimiter strings.</summary>
        public string[] PacketEndStrArray 
        {
            get { return packetEndStrList.ToArray(); }
            set
            {
                // clone given array
                packetEndStrList.Clear();
                if (value != null)
                {
                    foreach (string endStr in value)
                    {
                        if (!string.IsNullOrEmpty(endStr))
                            packetEndStrList.Add(endStr);
                    }
                }

                numPacketEndStrs = packetEndStrList.Count;

                packetEndByteArrayList.Clear();

                foreach (string s in packetEndStrList)
                    packetEndByteArrayList.Add(Utils.ByteArrayTranscoders.ByteStringTranscoder.Decode(s));

                Service(true);
            }
        }

        protected override void BufferHasBeenIncreased(int nCharsAdded)
        {
            // possibly search through the added bytes looking for packet end marks
            Service();  // this method may remove characters from the buffer, may cause it to be Reset or to be shifted.
        }

        protected override void BufferHasBeenShifted(int byN)
        {
            // nothing to do. - scanAfterCount is an offset from the getIdx so when the buffer is shifted, there is no change in the scanAfterCount value.
        }

        protected override void BufferHasBeenReset()
        {
            scanAfterCount = 0;     // reset the scaner to zero so that we start scanning from the start of the buffer
        }


        /// <summary>Allows the caller to Reset both the buffer and the queue of extracted packets.</summary>
        /// <param name="clearQueue">set to true if this method should clear the queue in addition to emptying the buffer.</param>
        protected void ResetBuffer(bool clearQueue)
        {
            ResetBuffer();

            if (clearQueue)
                extractedPacketQueue.Clear();
        }

        private bool stripWhitespace = false;
        private TimeSpan packetTimeout = TimeSpan.Zero;
        private List<string> packetEndStrList = new List<string>();
        private List<byte[]> packetEndByteArrayList = new List<byte[]>();
        private int numPacketEndStrs = 0;
        private int scanAfterCount = 0;

        private Queue<Packet> extractedPacketQueue = new Queue<Packet>();

        /// <summary>Returns true if the given byte is considered to be any ASCII whitespace character (calls Char.IsWhiteSpace).</summary>
        protected static bool IsWhiteSpace(byte b)
        {
            return (Char.IsWhiteSpace((char) b));
        }
    }

    #endregion
}

//-----------------------------------------------------------------
