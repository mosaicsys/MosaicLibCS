//-------------------------------------------------------------------
/*! @file SerialIO.cs
 * @brief This file defines the public interface that is provided by the classes which, when combined, make up the SerialIO portions of this library.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version: SerialPort.h, SerialPort.cpp)
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
	//-----------------------------------------------------------------

	using System;

	using MosaicLib.Utils;
	using MosaicLib.Time;
	using MosaicLib.Modular.Action;
	using MosaicLib.Modular.Part;

	//-----------------------------------------------------------------
	#region PortConfig

	//-----------------------------------------------------------------
	/// <summary>Define the patterns of line termination that the port will expect to receive and/or write.</summary>
	public enum LineTerm
	{
		None = 0,		// binary stream
		Auto,			// sends CRLF accepts any 
        CR,
        CRLF,
        Custom,
	};

	//-----------------------------------------------------------------

	/// <summary>This struct contains all of the information that is used to define the type and behavior of a SerialIO.Port</summary>
	/// <remarks>
	/// Valid SpecStr values:
	///		<ComPort port="com1" uartConfig="9600,n,8,1"/>
	///		<ComPort port="\\.\com200"><UartConfig baud="9600" DataBits="8" Mode="rs232-3wire" Parity="none" StopBits="1"/></ComPort>
	///		<TcpClient addr="127.0.0.1" port="5002"/>
	///		<TcpServer addr="127.0.0.1" port="8001"/>		addr is optional - will use any if no address is provided
	///		<UdpClient addr="127.0.0.1" port="5005"/>
	///		<UdpServer addr="127.0.0.1" port="5006"/>		addr is optional - will use any if no address is provided
	/// </remarks>

	public struct PortConfig
	{
		private string name;
		private string specStr;
        private string[] rxPacketEndStrArray;
        private string txPacketEndStr;
        private byte[] txPacketEndStrByteArray;
		private string traceDataLoggerGroupID;				// for data trace logger

		public PortConfig(string name, string specStr, LineTerm lineTerm) : this(name, specStr, lineTerm, (lineTerm == LineTerm.Auto ? LineTerm.CRLF : lineTerm)) { }
        public PortConfig(string name, string specStr, string[] rxPacketEndStrArray, LineTerm txLineTerm) : this(name, specStr, LineTerm.Custom, txLineTerm) { RxPacketEndStrArray = rxPacketEndStrArray; }
        public PortConfig(string name, string specStr, LineTerm rxLineTerm, LineTerm txLineTerm)
			: this()
		{ 
			this.name = name; 
			this.specStr = specStr;

            StripWhitespaceOnRx = true;
            switch (rxLineTerm)
            {
                case LineTerm.None: rxPacketEndStrArray = new string[0]; break;
                case LineTerm.Auto: rxPacketEndStrArray = new string[] { "\r", "\n" }; StripWhitespaceOnRx = true; break;
                case LineTerm.CR: rxPacketEndStrArray = new string[] { "\r" }; break;
                case LineTerm.CRLF: rxPacketEndStrArray = new string[] { "\r\n" }; break;
                case LineTerm.Custom: rxPacketEndStrArray = new string[0]; break;
                default: rxPacketEndStrArray = null; break;
            }
            switch (txLineTerm)
            {
                case LineTerm.None: txPacketEndStr = String.Empty; break;
                case LineTerm.Auto: txPacketEndStr = "\r\n"; break;
                case LineTerm.CR: txPacketEndStr = "\r"; break;
                case LineTerm.CRLF: txPacketEndStr = "\r\n"; break;
                case LineTerm.Custom: txPacketEndStr = string.Empty; break;
                default: txPacketEndStr = string.Empty; break;
            }
            txPacketEndStrByteArray = ByteArrayTranscoders.ByteStringTranscoder.Decode(txPacketEndStr);

            EnableAutoReconnect = false;
			ReconnectHoldoff = TimeSpan.FromSeconds(5.0);
			ConnectTimeout = TimeSpan.FromSeconds(5.0);
			ReadTimeout = TimeSpan.FromSeconds(1.0);
			WriteTimeout = TimeSpan.FromSeconds(1.0);
			IdleTime = TimeSpan.FromSeconds(10.0);
			SpinWaitTimeLimit = TimeSpan.FromSeconds(0.10);

			ErrorMesgType = Logging.MesgType.Error;
			InfoMesgType = Logging.MesgType.Info;
			DebugMesgType = Logging.MesgType.Debug;
            TraceMesgType = Logging.MesgType.Trace;
            TraceDataMesgType = Logging.MesgType.Trace;
			TraceDataLoggerGroupID = "LDG.SerialIO.TraceData";

			RxBufferSize = 4096;
			TxBufferSize = 4096;
        }

		public string Name { get { return name; } }
		public string SpecStr { get { return specStr; } }
        public string[] RxPacketEndStrArray
        {
            get { return ((rxPacketEndStrArray != null) ? rxPacketEndStrArray : emptyStrArray); }
            set { rxPacketEndStrArray = (value != null ? value : emptyStrArray); }
        }

        public string TxPacketEndStr
        {
            get { return ((txPacketEndStr != null) ? txPacketEndStr : String.Empty); }
            set 
            { 
                txPacketEndStr = ((value != null) ? value : String.Empty);
                txPacketEndStrByteArray = ByteArrayTranscoders.ByteStringTranscoder.Decode(txPacketEndStr);
            }
        }
        public byte[] TxLineTermBytes
        {
            get { return txPacketEndStrByteArray; }
        }

        public bool StripWhitespaceOnRx { get; set; }
        public bool EnableAutoReconnect { get; set; }
        public TimeSpan ReconnectHoldoff { get; set; }
        public TimeSpan ConnectTimeout { get; set; }
        public TimeSpan ReadTimeout { get; set; }
        public TimeSpan WriteTimeout { get; set; }
        public TimeSpan IdleTime { get; set; }      // time between successive reads for port to transition from receiving to idle - mainly used as a timeout for packet mode.
        public TimeSpan SpinWaitTimeLimit { get; set; }

        public Logging.MesgType ErrorMesgType { get; set; }
        public Logging.MesgType InfoMesgType { get; set; }
        public Logging.MesgType DebugMesgType { get; set; }
        public Logging.MesgType TraceMesgType { get; set; }
        public Logging.MesgType TraceDataMesgType { get; set; }
		public string TraceDataLoggerGroupID 
		{ 
			get { return (traceDataLoggerGroupID != null ? traceDataLoggerGroupID : Logging.DefaultDistributionGroupName); } 
			set { traceDataLoggerGroupID = value; } 
		}

        public uint RxBufferSize { get; set; }
        public uint TxBufferSize { get; set; }

		public bool IdleTimerEnabled { get { return (IdleTime != TimeSpan.Zero); } }

        static string[] emptyStrArray = new string[0];
	}

	#endregion

	//-----------------------------------------------------------------
	#region IPort and Actions

	//-----------------------------------------------------------------

	/// <summary>This enum defines a set of values that are used to catagorize the results of a given Read or Write action on a SerialIO.Port.</summary>
	public enum ActionResultEnum
	{
		/// <summary>enum value when no result state is known (action has been reset or is in progress)</summary>
		None = 0,

		/// <summary>one or more bytes were successfully returned from the port</summary>
		ReadDone,
		/// <summary>no bytes have been or were recieved from the port during the stated time period</summary>
		ReadTimeout,
		/// <summary>some error was reported by the port during the read.  read will terminate with all bytes that had been previously received.</summary>
		ReadFailed,
		/// <summary>user requested cancel operation on this action</summary>
		ReadCanceled,

		/// <summary>given number of bytes were successfully posted to the port</summary>
		WriteDone,
		/// <summary>given number of bytes could not be successfully posted to the port in he given time period (either due to error or due to timeout)</summary>
		WriteFailed,
		/// <summary>user requested cancel operation on this action</summary>
		WriteCanceled,
	}

	/// <summary>This class is used with the SerialIO.Port Read Action.  It contains the set of parameters to be passed to the action and the place where the action records the results once it has been performed</summary>
	/// <remarks>
	/// The client creates an instance of this class and fills in the buffer and then creates a Read Action using the Param instance.  
	/// Each time the client starets the action, the io device will attempt to process it and place all resulting information into this 
	/// instance and then mark the action as complete.  The ActionResultEnum property contains a summary code for the completion type of the action.  
	/// If this summary code indicates that the action failed then the ResultCode property will be a non-empty string.  
	/// If the summary code indicates that the operation was completed normally then the ResultCode property will be the empty string.
	/// 
	/// Please note that the ownership of this object is shared as long as their is an action that is queued or in progress which references this object.  
	/// The action initiator must not modify the contents of this parameter object until such an action has completed.
	/// </remarks>
	public class ReadActionParam
	{
		// items that are based to the request
		private Byte [] buffer = null;
		private int bytesToRead = 0;
		private bool waitForAllBytes = false;

		// results from completion of the read
		private QpcTimeStamp startTime = QpcTimeStamp.Zero;
		private volatile int bytesRead = 0;
		private volatile ActionResultEnum actionResultEnum = ActionResultEnum.None;
		private volatile string resultCode = null;

		public ReadActionParam() { }
		public ReadActionParam(int bufferSize) : this(new byte[bufferSize]) { }
		public ReadActionParam(byte [] buffer) : this(buffer, (buffer != null ? buffer.Length : 0)) { }
		public ReadActionParam(byte [] buffer, int bytesToRead) : this() { Buffer = buffer; BytesToRead = bytesToRead;  }

		public void Reset() { startTime = QpcTimeStamp.Zero; bytesRead = 0; actionResultEnum = ActionResultEnum.None; resultCode = null; }
		public void Start() { Reset(); startTime = QpcTimeStamp.Now; }

		public Byte [] Buffer { get { return buffer; } set { buffer = value; bytesToRead = (buffer != null ? buffer.Length : 0); } }
		public int BytesToRead { get { return bytesToRead; } set { if (buffer != null) bytesToRead = Math.Min(value, buffer.Length); else bytesToRead = 0; } }
		public bool WaitForAllBytes { get { return waitForAllBytes; } set { waitForAllBytes = value; } }

		public QpcTimeStamp StartTime { get { return startTime; } }
		public bool HasBeenStarted { get { return startTime != QpcTimeStamp.Zero; } }
		public int BytesRead { get { return bytesRead; } set { bytesRead = value; } }
		public ActionResultEnum ActionResultEnum { get { return actionResultEnum; } set { actionResultEnum = value; } }
		public string ResultCode { get { return resultCode; } set { resultCode = value; } }
	}

	/// <summary>This class is used with the SerialIO.Port Write Action.  It contains the set of parameters to be passed to the action and the place where the action records the results once it has been performed</summary>
	/// <remarks>
	/// The client creates an instance of this class and fills in the buffer and then creates a Write Action using the Param instance.  
	/// Each time the client starets the action, the io device will attempt to process it and place all resulting information into this 
	/// instance and then mark the action as complete.  The ActionResultEnum property contains a summary code for the completion type of the action.  
	/// If this summary code indicates that the action failed then the ResultCode property will be a non-empty string.  
	/// If the summary code indicates that the operation was completed normally then the ResultCode property will be the empty string.
	/// 
	/// Please note that the ownership of this object is shared as long as their is an action that is queued or in progress which references this object.  
	/// The action initiator must not modify the contents of this parameter object until such an action has completed.
	/// </remarks>
	public class WriteActionParam
	{
		// items that are based to the request
		private Byte [] buffer = null;
		private int bytesToWrite = 0;
		private bool isNonBlocking = false;

		// results from completion of the read
		private QpcTimeStamp startTime = QpcTimeStamp.Zero;
		private volatile int bytesWritten = 0;
		private volatile ActionResultEnum actionResultEnum = ActionResultEnum.None;
		private volatile string resultCode = null;

		public WriteActionParam() { }
		public WriteActionParam(byte [] buffer) : this(buffer, (buffer != null ? buffer.Length : 0)) { }
		public WriteActionParam(byte [] buffer, int bytesToWrite) : this() { Buffer = buffer; BytesToWrite = bytesToWrite; }

		public void Reset() { startTime = QpcTimeStamp.Zero; bytesWritten = 0; actionResultEnum = ActionResultEnum.None; resultCode = null; }
		public void Start() { Reset(); startTime = QpcTimeStamp.Now; }

		public Byte [] Buffer { get { return buffer; } set { buffer = value; bytesToWrite = (buffer != null ? buffer.Length : 0); } }
		public int BytesToWrite { get { return bytesToWrite; } set { if (buffer != null) bytesToWrite = Math.Min(value, buffer.Length); else bytesToWrite = 0; } }
		public bool IsNonBlocking { get { return isNonBlocking; } set { isNonBlocking = value; } }

		public QpcTimeStamp StartTime { get { return startTime; } }
		public bool HasBeenStarted { get { return startTime != QpcTimeStamp.Zero; } }
		public int BytesWritten { get { return bytesWritten; } set { bytesWritten = value; } }
		public ActionResultEnum ActionResultEnum { get { return actionResultEnum; } set { actionResultEnum = value; } }
		public string ResultCode { get { return resultCode; } set { resultCode = value; } }
	}

	//-----------------------------------------------------------------

	/// <summary>Interface to client facet of a Port Read Action.  Paramter type is ReadActionParam which contains the details of how the action is to be performed</summary>
	public interface IReadAction : IClientFacetWithParam<ReadActionParam> { }

	/// <summary>Interface to client facet of a Port Write Action.  Paramter type is WriteActionParam which contains the details of how the action is to be performed</summary>
	public interface IWriteAction : IClientFacetWithParam<WriteActionParam> { }

    /// <summary>Interface to client facet of a Port GetNextPacket Action.  Result is the next packet extracted from the internal sliding buffer.</summary>
    public interface IGetNextPacketAction : IClientFacetWithResult<Packet> { }

	/// <summary>Interface that defines the functionality provided by a MosaicLib.SerialIO.IPort including the ability to create read, write and flush actions.</summary>
	/// <remarks>
	/// This interface defines the public methods that are provided by all types of SerialIO.Port objects.  These include the ability to create
	/// Read, Write, and Flush Actions.  Read and Write actions are special in that they make use of a reference to a shared Parameter struct that contains 
	/// information used to define the details of the read or write operation as well as providing a place for the Port to place additional details on how and why 
	/// it completed the action.  Read Actions make use of the ReadActionParam class and Write Actions make use of the WriteActionParam classes for these purposes.
	/// </remarks>

	public interface IPort : Modular.Part.IActivePartBase
	{
        /// <summary>Gives the Port Name</summary>
		string Name { get; }

        /// <summary>Gives the current PortConfig</summary>
        PortConfig PortConfig { get; }

        /// <summary>Returns an IReadAction which refers to the given ReadActionParam instance and which may be used to execute a read using the ReadActionParam defined behavior</summary>
        /// <remarks>This may not be used with Port's that have been configured to use an internal sliding buffer.  Use CreateGetnextPacketAction instead in these cases.</remarks>
		IReadAction CreateReadAction(ReadActionParam param);

        /// <summary>Returns an IWriteAction which refers to the given WriteActionParam instance and which may be used to execute a write using the WriteActionParams defined behavior</summary>
        IWriteAction CreateWriteAction(WriteActionParam param);

        /// <summary>Returns an IBasicAction.  Underlying action TimeSpan parameter has been initilized to given value of flushWaitTime.  This action may be used to flush the port of characters for the given wait time period.</summary>
        IBasicAction CreateFlushAction(TimeSpan flushWaitLimit);

        // the following methods are only useful with Port's that have an associated SlidingPacketBuffer

        /// <summary>Asynchronous property that returns true for Ports that have been configured to use a SlidingBuffer and which have at least one packet decoded from the sliding buffer</summary>
        bool HasPacket { get; }

        /// <summary>Asynchronous property that returns the number of packets that are currently available to be dequeued from the Port.  This property will only be non-zero on Ports that have been configured to use a SlidingBuffer.</summary>
        int NumPacketsReady { get; }

        /// <summary>Returns an IGetNextPacketAction which may be executed to attempt to dequeue the next available Packet recieved by the Port.</summary>
        IGetNextPacketAction CreateGetNextPacketAction();
	}

	#endregion

	//-----------------------------------------------------------------
	#region Port Factory

	public class InvalidPortConfigSpecStr : SystemException
	{
		public InvalidPortConfigSpecStr(string mesg) : base(mesg) { }
	}

	/// <summary>This static class provides static methods that may be used to create various types of SerialIO.IPort objects.</summary>
	public static partial class Factory
	{
		/// <summary>Create an IPort implementation object based on the first element in the SpecStr in the given portConfig struct.</summary>
		/// <param name="portConfig">Provides all configuration details for the port to be created.</param>
		/// <returns>The created SerialIO.Port object as an IPort.  Throws an InvalidPortConfigSpecStr exception if the required concrete type cannot be determined from the portConfig.SpecStr.</returns>

		public static IPort CreatePort(PortConfig portConfig)
		{
			bool success = true;
			Utils.StringScanner specScan = new StringScanner(portConfig.SpecStr);
			System.Exception e = null;

			string elementName = null;
			IPPortEndpointConfig epConfig = null;
			if (IPPortEndpointConfig.TryParse(ref specScan, out epConfig))
			{
				elementName = epConfig.ElementName;
				success = epConfig.IsValid;
			}
			else
			{
                success &= specScan.MatchToken("<", false, false);
                success &= specScan.ExtractToken(out elementName);
			}

			if (elementName == "NullPort")
			{
				return new NullPort(portConfig);
			}
			else if (elementName == "ComPort")
			{
				ComPortConfig comPortConfig = new ComPortConfig(portConfig.SpecStr);
				if (comPortConfig.IsValid)
					return CreateComPort(portConfig, comPortConfig);
				else
					e = new InvalidPortConfigSpecStr(Utils.Fcns.CheckedFormat("MosaicLib.SerialIO.Factory.CreatePort call failed: error:'{0}' for port:'{1}'", comPortConfig.ErrorCode, portConfig.Name));
			}
			else if (epConfig.IsValid)
			{
				if (elementName == "TcpClient")
				{
					return CreateTcpClientPort(portConfig, epConfig);
				}
				else if (elementName == "TcpServer")
				{
					return CreateTcpServerPort(portConfig, epConfig);
				}
				else if (elementName == "UdpClient")
				{
					return CreateUdpClientPort(portConfig, epConfig);
				}
				else if (elementName == "UdpServer")
				{
					return CreateUdpServerPort(portConfig, epConfig);
				}
			}
			else
			{
				e = new InvalidPortConfigSpecStr(Utils.Fcns.CheckedFormat("MosaicLib.SerialIO.Factory.CreatePort call failed: SpecStr parse error:'{0}'", epConfig.ErrorCode));
			}

			if (e == null)
				e = new InvalidPortConfigSpecStr(Utils.Fcns.CheckedFormat("MosaicLib.SerialIO.Factory.CreatePort call failed: SpecStr:'{0}' not recognized for port:'{1}'", portConfig.SpecStr, portConfig.Name));

			throw e;		// there is no return value since we threw instead.
		}
	}

	#endregion

	//-----------------------------------------------------------------
}
