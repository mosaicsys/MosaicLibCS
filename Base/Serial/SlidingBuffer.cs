//-------------------------------------------------------------------
/*! @file SlidingBuffer.cs
 *  @brief This file defines the SlidingBuffer and SlidingLineBuffer classes that are used for packet/line reassembly on serial device drivers.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2002 Mosaic Systems Inc.  (C++ library version, found under Utils)
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
using System.Collections;
using System.Collections.Generic;

using MosaicLib.Time;
using MosaicLib.Utils.Collections;

namespace MosaicLib.SerialIO
{
    //-------------------------------------------------------------------

    #region SlidingBuffer (base for SlidingPacketBuffer)

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

        /// <summary>
        /// This method is called to indicate that the given number of bytes have been used from the buffer.  This causes the
        /// SlidingBuffer to advance the getIdx by the given number of bytes.  It may cause the buffer to be Reset or Aligned
        /// depending on the final state after the getIdx has been advanced.
        /// </summary>
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
        public void FlushBuffer() { ResetBuffer(true); }

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
                ResetBuffer(false);
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

        /// <summary>Clears the buffer contents, aligns the empty buffer and updates the get and put timestamps.  Optionally resets the get and put timers</summary>
        public virtual void ResetBuffer(bool resetTimers = true)
        {
            getIdx = 0;
            putIdx = 0;

            if (resetTimers)
                getTimeStamp = putTimeStamp = QpcTimeStamp.Now;

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

        /// <summary>This is the field that references the underlying byte buffer that is used by this SlidingBuffer instance.</summary>
		protected byte [] buffer;

        /// <summary>This gives ths index into the buffer at which the first content byte is found (if any)</summary>
		protected int getIdx = 0;
        
        /// <summary>This gives the QpcTimeStamp at the point when the getIdx was last changed.</summary>
        protected QpcTimeStamp getTimeStamp = QpcTimeStamp.Zero;

        /// <summary>This gives the index into the buffer at which the next byte can be put into the buffer, or the buffer length if the buffer is full.</summary>
        protected int putIdx = 0;

        /// <summary>This gives the QpcTimeStamp at the point when the putIdx was last changed.</summary>
        protected QpcTimeStamp putTimeStamp = QpcTimeStamp.Zero;
    }

    #endregion

    //-------------------------------------------------------------------

    #region SlidingPacketBuffer

    #region SlidingPacketBuffer support types

    /// <summary>
    /// Enum used to define various types of contents (or meanings) that a SlidingPacketBuffer Packet can contain/conveigh.
    /// <para/> Null = 0, None, Data, Whitespace, Flushed, Timeout, Error
    /// </summary>
    public enum PacketType
    {
        /// <summary>Default constructor value</summary>
	    Null = 0,
        /// <summary>no data was read or was available</summary>
	    None,
        /// <summary>data was obtained and is included</summary>
	    Data,
        /// <summary>data was obtained: all of it was whitespace.  It may have been trimmed</summary>
	    WhiteSpace,
        /// <summary>packet represents data that has been/is being flushed from the port</summary>
	    Flushed,
        /// <summary>packet contains partial data but no packet end pattern was found before packet accumulation time limit was reached.</summary>
	    Timeout,
        /// <summary>packet contains an error code</summary>
	    Error,
    };

    /// <summary>
    /// General class used to store a packet as extracted from a SlidingPacketBuffer.  
    /// </summary>
    public class Packet
    {
        /// <summary>Default constructor.  Creates null packet of type None</summary>
        public Packet() : this(PacketType.None, null, null) { }
        /// <summary>Constructs an empty packet of the given type</summary>
        public Packet(PacketType type) : this(type, null, null) { }
        /// <summary>Constructs a packet of the given type with the given data content and no error</summary>
        public Packet(PacketType type, byte[] data) : this(type, data, null) { }
        /// <summary>Full constructor.</summary>
        public Packet(PacketType type, byte[] data, String errorCode) 
        {
            Type = type;
            this.data = data;
            this.dataStr = null;
            this.errorCode = errorCode;
        }

        /// <summary>Private storage for data content</summary>
        private byte [] data;
        /// <summary>Private storage for errorCode (if any) - Property handles mapping of null to empty</summary>
        private string errorCode;
        /// <summary>Private storage for cached string version of data content</summary>
        private string dataStr;

        /// <summary>Returns the PacketType for this Packet</summary>
        public PacketType Type { get; set; }
        /// <summary>Returns the Data content array for this Packet.  May be null if no data is assocaited with this Packet</summary>
        public byte[] Data { get { return data; } set { data = value; dataStr = null; } }

        /// <summary>Contains the ErrorCode for property for any Packet.  Will return String.Empty whenever the internally stored copy of this property is, or has been set to, null.  Setter will set Type to Error if it is Null</summary>
        public string ErrorCode 
        { 
            get { return (errorCode ?? String.Empty); } 
            set 
            { 
                errorCode = value;
                if (Type == default(PacketType))
                    Type = PacketType.Error;
            }
        }

        /// <summary>
        /// By default this get property generates, saves and, returns a Transcoded string version of the Data property.  
        /// This get property value is cached and will latch the first transcoded version until the Data property or the DataStr property is set to Null.
        /// The setter sets the internal property storage field to null, without regard to the value assigned to the setter and thus resets the cached copy 
        /// so that it will be re-generated during the next call to the getter.
        /// </summary>
        public string DataStr 
        { 
            get { return ((dataStr != null) ? dataStr : (dataStr = Utils.ByteArrayTranscoders.ByteStringTranscoder.Encode(data))); } 
            set { dataStr = null; } 
        }

        /// <summary>Returns true if the Type is PacketType.Null or PacketType.None</summary>
        public bool IsNullOrNone { get { return (Type == PacketType.Null || Type == PacketType.None); } }
        /// <summary>Returns true if the Type is PacketType.WhiteSpace</summary>
        public bool IsWhiteSpace { get { return (Type == PacketType.WhiteSpace); } }
        /// <summary>Returns true if the type is PacketType.Data</summary>
        public bool IsData { get { return (Type == PacketType.Data); } }
        /// <summary>Returns true if the type is PacketType.Data and the data array is non-null and non-empty</summary>
        public bool IsNonEmptyData { get { return (IsData && Data != null && Data.Length > 0); } }
        /// <summary>Returns true if the type is PacketType.Data and the data array is null or empty</summary>
        public bool IsEmptyData { get { return (IsData && (Data == null || Data.Length == 0)); } }
        /// <summary>Returns true if the type is PacketType.Null or PacketType.None or the type is PacketType.Data and the data array is null or empty</summary>
        public bool IsNullOrNoneOrEmptyData { get { return (IsNullOrNone || IsEmptyData); } }
        /// <summary>Returns true if the type is PacketType.Error or PacketType.Timeout</summary>
        public bool IsError { get { return (Type == PacketType.Error || Type == PacketType.Timeout); } }

        /// <summary>Returns true if the rhsAsObject is a non-null Packet instance and if its Type, Data and ErrorCode properties are the same</summary>
        public override bool Equals(object rhsAsObject)
        {
            Packet rhs = rhsAsObject as Packet;
            if (rhs == null)
                return false;

            return (Type == rhs.Type && data.Equals(rhs.data) && errorCode == rhs.errorCode);
        }

        /// <summary>Passthrough to base.GetHashCode</summary>
        public override int GetHashCode()
        {
 	        return base.GetHashCode();
        }

        /// <summary>Fixed emptyData field used in place of null Data property during ToString operations.</summary>
        private static byte[] emptyData = EmptyArrayFactory<byte>.Instance;

        /// <summary>
        /// Returns a string version of the Packet that indicates the type and length of the packet but does not include its data content
        /// </summary>
        public override string ToString()
        {
            return ToString(false);
        }

        /// <summary>
        /// Returns a string version of the Packet that indicates its type and length or type and content based on the allwaysIncludeData parameter value.
        /// </summary>
        public string ToString(bool allwaysIncludeData)
        {
            string typeAsStr = Type.ToString();
            byte[] dataTemp = (Data ?? emptyData);

            if (!allwaysIncludeData || IsError)
            {
                switch (Type)
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

    /// <summary>
    /// This delegate is used as an alternate Packet End detection tool.  
    /// A client may provide an instance of this delegate type the SlidingPacketBuffer in which case the SlidingPacketBuffer will use the
    /// delegate in place of its internal scanner to detect packet end conditions.  This allows the client to have full flexability in
    /// determining when a full packet has arrived and in determining the rules for how to handle conditions where more than one
    /// packet end exists in the buffer at the same time.
    /// </summary>
    /// <param name="buffer">Gives the delegate access to the internal buffer being used by the SlidingPacketBuffer.  The delegate shoult not modify the contents of this array.</param>
    /// <param name="startIdx">Gives the delegate the index of the first content byte in the buffer.</param>
    /// <param name="currentContentCount">Gives the delegate the current byte count in the buffer</param>
    /// <param name="lastScannedContentCount">
    /// Gives the delegate the content count during the last call to the delegate.  
    /// This may be used to optimize out rescanning of early parts of the buffer.
    /// </param>
    /// <returns>
    /// The length of the next packet to generate from the buffer or 0 to indicate that no packet is ready to be removed from the buffer yet.
    /// A negative number indicates that the Math.Abs(given number) of characters should be removed from the buffer and handled as flushed bytes.
    /// </returns>
    public delegate int PacketEndScannerDelegate(byte [] buffer, int startIdx, int currentContentCount, int lastScannedContentCount);

    #endregion

    /// <summary>
    /// This class implements a packetizing version of the SlidingBuffer.  
    /// This implementation is given a set of criteria for determining the end of a given packet and uses the given criteria to scan the contents of the sliding buffer
    /// looking for packet boundaries.  When such boundaries are found, this class extracts the head contents of the buffer up to the first found boundary and encapuslates
    /// that byte block as a received packet which is placed in the packet queue.  
    /// This class also supports triming whitespace from the data that is encapsulated in a packet, detecting timeout conditions where the buffer is not empty and no new data has arrived within
    /// the required period so that incomplete packets may be configured to get flushed as timeout packets if the client would like.
    /// </summary>
    public class SlidingPacketBuffer : SlidingBuffer
    {
        /// <summary>Minimal constructor.  Requires that buffer size be specified directly.</summary>
        /// <param name="size">gives the desired size of the buffer.  Defines the maximum number of bytes that can be in one packet.</param>
        public SlidingPacketBuffer(uint size)
            : base(size)
        { }

        /// <summary>Constructor</summary>
        /// <param name="size">gives the desired size of the buffer.  Defines the maximum number of bytes that can be in one packet.</param>
        /// <param name="packetEndStrArray">gives a list of one or more strings which define a packet end delimiter pattern.</param>
        /// <param name="packetTimeout">gives the maximum amount of time that may elpased from the last byte added before a valid packet end is found</param>
        /// <param name="stripWhitespace">Sets the StripWhitespace property to the value given in this argument</param>
        public SlidingPacketBuffer(uint size, string[] packetEndStrArray, TimeSpan packetTimeout, bool stripWhitespace = true)
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
        public void Service() 
        { 
            Service(false); 
        }

        /// <summary>Implements scan for new packets (invoked internally after reception of new data) and implements stale rx data detection and handling when no new data has been received.</summary>
        public void Service(bool performFullRescan)
        {
            // check for unscanned data
            if (performFullRescan)
                lastScannedContentCount = 0;

            if (BufferDataCount == 0 || BufferDataCount == lastScannedContentCount)
                return;

            PacketEndScannerDelegate scannerDelegate = packetEndScannerDelegate ?? defaultPacketEndScannerDelegate ?? (defaultPacketEndScannerDelegate = DefaultPacketEndScannerDelegate);

            bool foundPacket = false;

            for (;;)
            {
                int bufferDataCount = BufferDataCount;
                int nextPacketLen = scannerDelegate(buffer, getIdx, bufferDataCount, lastScannedContentCount);

                if (nextPacketLen == 0)
                {
                    lastScannedContentCount = bufferDataCount;
                    break;
                }
                else
                {
                    lastScannedContentCount = 0;
                    foundPacket = true;

                    int dataCopyStartIdx = getIdx;
                    bool isFlushedData = (nextPacketLen < 0);

                    nextPacketLen = Math.Abs(nextPacketLen);
                    int dataCopyLen = Math.Min(bufferDataCount, nextPacketLen);

                    bool isWhitespace = false;
                    if (isFlushedData)
                    {
                        // we do not strip whitespace from flushed data.
                    }
                    else if (stripWhitespace)
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

                        isWhitespace = (dataCopyLen == 0);
                    }
                    else if (detectWhitespace)
                    {
                        isWhitespace = true;    // assume that it is all whitespace
                        for (int scanOffset = 0; scanOffset < dataCopyLen; scanOffset++)
                        {
                            if (!IsWhiteSpace(buffer[getIdx + scanOffset]))
                            {
                                isWhitespace = false;
                                break;
                            }
                        }
                    }

                    // extract bytes from buffer and append into new packet
                    if (!isWhitespace || !DiscardWhitespacePackets)
                    {
                        Packet p;
                        if (!isFlushedData)
                            p = new Packet((!isWhitespace ? PacketType.Data : PacketType.WhiteSpace), new byte[dataCopyLen]);
                        else
                            p = new Packet(PacketType.Flushed, new byte[dataCopyLen]);

                        if (dataCopyLen > 0)
                            System.Buffer.BlockCopy(buffer, dataCopyStartIdx, p.Data, 0, dataCopyLen);

                        extractedPacketQueue.Enqueue(p);
                    }
                    // else this run is all whitespace and we have been configured to DiscardWhitespacePackets - so just drop these bytes.

                    UsedNChars(nextPacketLen);
                }
            }

            // drop leading whitespace from buffer immediately (this will prevent them from causing generation of unexpected timeout packets for trailing whitespace that is ignored)
            if (stripWhitespace)
            {
                int whiteSpaceRunLength = 0;

                while (whiteSpaceRunLength < BufferDataCount && IsWhiteSpace(buffer[getIdx + whiteSpaceRunLength]))
                    whiteSpaceRunLength++;

                if (whiteSpaceRunLength > 0)
                    UsedNChars(whiteSpaceRunLength);
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

        private PacketEndScannerDelegate defaultPacketEndScannerDelegate = null;

        private int DefaultPacketEndScannerDelegate(byte[] buffer, int startIdx, int currentContentCount, int lastScannedContentCount)
        {
            // find shortest packet with end characters
            int shortestPacketLen = 0;
            int numPacketEndStrs = packetEndByteArrayList.Count;

            for (int packetEndIdx = 0; packetEndIdx < numPacketEndStrs; packetEndIdx++)
            {
                int possiblePacketLen = 0;

                byte [] patternBytes = packetEndByteArrayList[packetEndIdx];
                int patternLen = patternBytes.Length;

                // For each pattern we will the new bytes and some (or all) of the old bytes to see if we can find this end pattern in the buffer.
                // To minimize rescan, each pattern's scan starts at the startIdx + the lastScannedContentCount, rewound at most patternLen - 1 bytes.  The rewind will not start earlier than
                // startIdx for the scan
                int patternScanStartIdx = Math.Max(startIdx, (startIdx + lastScannedContentCount - (patternLen - 1)));

                // the scan ends at the startIdx + currentContentCount less patternLen - 1 bytes.  If patternLen is longer than currentContentCount then no scanning will take place on this pass.
                // This scan end position makes certain that we do not attempt to check for the pattern past the end of the input buffer.
                int scanLimitCount = (currentContentCount - (patternLen - 1));
                if (shortestPacketLen > 0 && scanLimitCount > shortestPacketLen)
                    scanLimitCount = shortestPacketLen;

                int scanEndIdx = startIdx + scanLimitCount;

                for (int patternScanIdx = patternScanStartIdx; patternScanIdx < scanEndIdx; patternScanIdx++)
                {
                    int patternOffset = 0;
                    for (; patternOffset < patternLen; patternOffset++)
                    {
                        if (buffer[patternScanIdx + patternOffset] != patternBytes[patternOffset])
                            break;
                    }

                    if (patternOffset == patternLen)
                    {
                        possiblePacketLen = (patternScanIdx + patternOffset - startIdx);
                        break;
                    }
                }

                if (possiblePacketLen > 0 && (shortestPacketLen == 0 || possiblePacketLen < shortestPacketLen))
                    shortestPacketLen = possiblePacketLen;
            }

            return shortestPacketLen;
        }

        /// <summary>
        /// Gets or set the current StripWhitespace flag.  When set to true, all leading and trailing white space is removed from packet data for Data packets.  Packet type will be changed to Whitespace if the resulting Data packet is empty.
        /// When set true this property will also set the DetectWhitespace property.
        /// </summary>
        public bool StripWhitespace 
        { 
            get { return stripWhitespace; }
            set 
            { 
                stripWhitespace = value;
                if (value)
                    detectWhitespace = true;
                Service(true); 
            } 
        }

        /// <summary>Get/Set property.  Determines if this SPB is allowed to look for whitespace.  When set to false it also disables StipWhitespace.</summary>
        public bool DetectWhitespace 
        { 
            get { return detectWhitespace; } 
            set 
            {
                detectWhitespace = value;
                if (!value)
                    stripWhitespace = false;
            } 
        }

        /// <summary>Get/Set property.  When set to true, Whitespace packets will be discarded at the point that they are created (they will not be enqueued).  If this property is changed while the buffer is in use, the results may not be well defined until the buffer is next fully empty.</summary>
        public bool DiscardWhitespacePackets { get; set; }

        /// <summary>Gets or sets the current PacketTimeout value.  This value is used during Service calls to determine if remaining bytes in the buffer have been idle for to long in which case a timeout packet is generated and enqueued to discard them.  When this value is set to zero, no local timeout handling is performed within the sliding buffer.</summary>
        public TimeSpan PacketTimeout
        {
            get { return packetTimeout; }
            set 
            { 
                packetTimeout = value; 
                Service(true); 
            }
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

                packetEndByteArrayList.Clear();

                foreach (string s in packetEndStrList)
                    packetEndByteArrayList.Add(Utils.ByteArrayTranscoders.ByteStringTranscoder.Decode(s));

                Service(true);
            }
        }

        /// <summary>
        /// When non-null this delegate is used in place of the PacketEndStrArray contents to scan for packet boundaries and use them to determin
        /// when and how the inserted buffer data is divided into packets.  When null, the SlidingPacketBuffer uses its internal mechanism to detect these
        /// boundaries based on the contents of the PacketEndStrArray property contents.  
        /// Setting this property to a non-null value will clear the PacketEndStrArray property and will Service the buffer
        /// </summary>
        public PacketEndScannerDelegate PacketEndScannerDelegate 
        {
            get { return packetEndScannerDelegate; }
            set
            {
                packetEndScannerDelegate = value;
                if (value != null)
                    PacketEndStrArray = EmptyArrayFactory<string>.Instance;
            }
        }

        /// <summary>
        /// (Possibly) search through the added bytes looking for packet end marks.
        /// This method may remove characters from the buffer, and may cause it to be Reset and/or to be shifted.
        /// </summary>
        /// <param name="nCharsAdded">Gives the number of characters that have been added to the buffer.</param>
        protected override void BufferHasBeenIncreased(int nCharsAdded)
        {
            // possibly search through the added bytes looking for packet end marks
            Service();  // this method may remove characters from the buffer, may cause it to be Reset or to be shifted.
        }

        /// <summary>
        /// Records the side effects caused by Resetting the buffer.
        /// Reset the scanner to force the next Service to do a full rescan.
        /// </summary>
        protected override void BufferHasBeenReset()
        {
            lastScannedContentCount = 0;
        }

        /// <summary>
        /// This method is called to indicate that the given number of bytes have been used from the buffer.  This causes the
        /// SlidingBuffer to advance the getIdx by the given number of bytes.  It may cause the buffer to be Reset or Aligned
        /// depending on the final state after the getIdx has been advanced.
        /// </summary>
        public override void UsedNChars(int n)
        {
            lastScannedContentCount = Math.Max(lastScannedContentCount - n, 0);

            base.UsedNChars(n);
        }

        /// <summary>Call this method to Reset the sliding buffer and to clear the queue of packets that have already been extracted from it.</summary>
        protected void ResetBufferAndClearQueue()
        {
            base.ResetBuffer();

            extractedPacketQueue.Clear();
        }

        private bool stripWhitespace = false;
        private bool detectWhitespace = false;
        private TimeSpan packetTimeout = TimeSpan.Zero;
        private List<string> packetEndStrList = new List<string>();
        private List<byte[]> packetEndByteArrayList = new List<byte[]>();
        private PacketEndScannerDelegate packetEndScannerDelegate = null;
        private int lastScannedContentCount = 0;

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
