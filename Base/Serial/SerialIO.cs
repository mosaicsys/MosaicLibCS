//-------------------------------------------------------------------
/*! @file SerialIO.cs
 *  @brief This file defines the public interface that is provided by the classes which, when combined, make up the SerialIO portions of this library.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2006 Mosaic Systems Inc.  (C++ library version: SerialPort.h, SerialPort.cpp)
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

using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.SerialIO
{
	//-----------------------------------------------------------------
	#region PortConfig

	//-----------------------------------------------------------------
	/// <summary>
    /// Define the patterns of line termination that the port will expect to receive and/or write.
    /// <para/>
    /// None = 0, Auto, CR, CRLF, Custom
    /// </summary>
	public enum LineTerm
	{
        /// <summary>Used for ports that carry binary data.</summary>
		None = 0,
        /// <summary>Tx uses CRLF, Rx accepts CR, LF, CRLF or LFCR as a single EndOfLine condition.</summary>
        Auto,
        /// <summary>Tx and Rx EOL is CR only</summary>
        CR,
        /// <summary>Tx and Rx EOL is CRLF</summary>
        CRLF,
        /// <summary>Tx EOL is manually specified, Rx EOL detection is implemented using the <see cref="PacketEndScannerDelegate"/> delegate.</summary>
        Custom,
	};

	//-----------------------------------------------------------------

	/// <summary>
    /// This struct contains all of the information that is used to define the type and behavior of a SerialIO.Port.  
	/// Valid SpecStr values:
    ///	<para/>	&lt;ComPort port='com1' uartConfig='9600,n,8,1'/&gt;
    ///	<para/>	&lt;ComPort port='com200'&gt;&lt;UartConfig baud='9600' DataBits='8' Mode='rs232-3wire' Parity='none' StopBits='1'/&gt;&lt;/ComPort&gt;
    ///	<para/>	&lt;TcpClient addr='127.0.0.1' port='5002'/&gt;
    ///	<para/>	&lt;TcpServer addr='127.0.0.1' port='8001'/&gt;		addr is optional - will use any if no address is provided
    ///	<para/>	&lt;UdpClient addr='127.0.0.1' port='5005'/&gt;
    ///	<para/>	&lt;UdpServer addr='127.0.0.1' port='5006'/&gt;		addr is optional - will use any if no address is provided
    ///	<para/> &lt;NullPort/&gt;
    /// </summary>
    public struct PortConfig
    {
        #region Constructors

        /// <summary>Constructor for normal non-packetized send/recive.</summary>
        /// <param name="name">This gives the name that will be used for the port created from this config contents</param>
        /// <param name="specStr">This gives the port target type configuration string that is used to define the type of port that will be created and where it will be connected to.</param>
        /// <param name="lineTerm">Sets both the RxLineTerm and TxLineTerm properties from this value.</param>
        public PortConfig(string name, string specStr, LineTerm lineTerm) 
            : this(name, specStr, lineTerm, lineTerm) 
        { }

        /// <summary>Constructor for packetized communication using one or more packet end strings to deliniate packets</summary>
        /// <param name="name">This gives the name that will be used for the port created from this config contents</param>
        /// <param name="specStr">This gives the port target type configuration string that is used to define the type of port that will be created and where it will be connected to.</param>
        /// <param name="rxPacketEndStrArray">
        /// Explicitly defines the set of packet end strings.  Internally sets RxLineTerm to LineTerm.Custom.  
        /// Selects packetized reception if this value is a non-empty array
        /// </param>
        /// <param name="txLineTerm">This determines the contents of the TxPacketEndStr to match the line termination characters selected here.</param>
        public PortConfig(string name, string specStr, string[] rxPacketEndStrArray, LineTerm txLineTerm) 
            : this(name, specStr, LineTerm.Custom, txLineTerm) 
        { 
            RxPacketEndStrArray = rxPacketEndStrArray; 
        }

        /// <summary>Constructor for packetized communication using client provided custom PacketEndScannerDelegate</summary>
        /// <param name="name">This gives the name that will be used for the port created from this config contents</param>
        /// <param name="specStr">This gives the port target type configuration string that is used to define the type of port that will be created and where it will be connected to.</param>
        /// <param name="rxPacketEndScannerDelegate">Selects that the client will use packetized reception using this custom delegate to to detect packet boundaries.</param>
        /// <param name="txLineTerm">This determines the contents of the TxPacketEndStr to match the line termination characters selected here.</param>
        public PortConfig(string name, string specStr, PacketEndScannerDelegate rxPacketEndScannerDelegate, LineTerm txLineTerm)
            : this(name, specStr, LineTerm.Custom, txLineTerm)
        {
            RxPacketEndScannerDelegate = rxPacketEndScannerDelegate;
        }

        /// <summary>Common Constructor for ports that use line termination.</summary>
        /// <param name="name">This gives the name that will be used for the port created from this config contents</param>
        /// <param name="specStr">This gives the port target type configuration string that is used to define the type of port that will be created and where it will be connected to.</param>
        /// <param name="rxLineTerm">
        /// Sets the RxLineTerm property from this value.  
        /// Also sets TrimWhitespaceOnRx (and thus requires use of packetized reception) if LineTerm is neither None nor Custom.
        /// </param>
        /// <param name="txLineTerm">Sets the TxLineTerm property from this value.</param>
        public PortConfig(string name, string specStr, LineTerm rxLineTerm, LineTerm txLineTerm)
			: this(name, specStr)
		{ 
            TrimWhitespaceOnRx = (rxLineTerm != LineTerm.None && rxLineTerm != LineTerm.Custom);
            DetectWhitespace |= TrimWhitespaceOnRx;

            RxLineTerm = rxLineTerm;
            TxLineTerm = txLineTerm;
        }

        /// <summary>
        /// Standard basic constructor - requires name and specStr.  Sets many properties to their default values.
        /// </summary>
        /// <param name="name">This gives the name that will be used for the port created from this config contents</param>
        /// <param name="specStr">This gives the port target type configuration string that is used to define the type of port that will be created and where it will be connected to.</param>
        public PortConfig(string name, string specStr)
            : this()
        {
            Name = name;
            SpecStr = specStr;

            PartBaseIVI = Values.Instance;
            IConfig = Config.Instance;

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
            TraceActionDoneMesgType = Logging.MesgType.Trace;
            TraceActionErrorMesgType = Logging.MesgType.Debug;
            TraceActionStateMesgType = Logging.MesgType.None;
            TraceActionUpdateMesgType = Logging.MesgType.None;
            LoggerGroupID = String.Empty;       // use default
            TraceDataLoggerGroupID = "LDG.SerialIO.TraceData";
            TraceDataLoggerInitialLogGate = Logging.LogGate.All;

            RxBufferSize = 4096;
            TxBufferSize = 4096;
        }

        /// <summary>Returns a new PortConfig instance derived from the given cloneFrom copy but with a new name and specStr value.</summary>
        /// <param name="name">This gives the name that will be used for the port created from this config contents</param>
        /// <param name="specStr">This gives the port target type configuration string that is used to define the type of port that will be created and where it will be connected to.</param>
        /// <param name="cloneFrom">Gives the instance from which to copy all fields/Properties except the Name and SpecStr from.</param>
        public PortConfig(string name, string specStr, PortConfig cloneFrom)
            : this()
        {
            Name = name;
            SpecStr = specStr;

            PartBaseIVI = cloneFrom.PartBaseIVI;
            IConfig = cloneFrom.IConfig;

            rxPacketEndStrArray = cloneFrom.rxPacketEndStrArray;
            rxPacketEndScannerDelegate = cloneFrom.rxPacketEndScannerDelegate;
            txPacketEndStr = cloneFrom.txPacketEndStr;
            txPacketEndStrByteArray = cloneFrom.txPacketEndStrByteArray;

            loggerGroupID = cloneFrom.loggerGroupID;
            traceDataLoggerGroupID = cloneFrom.traceDataLoggerGroupID;
            TraceDataLoggerInitialLogGate = cloneFrom.TraceDataLoggerInitialLogGate;

            TrimWhitespaceOnRx = cloneFrom.TrimWhitespaceOnRx;
            DiscardWhitespacePacketsOnRx = cloneFrom.DiscardWhitespacePacketsOnRx;
            DetectWhitespace = cloneFrom.DetectWhitespace;
            EnableAutoReconnect = cloneFrom.EnableAutoReconnect;
            ReconnectHoldoff = cloneFrom.ReconnectHoldoff;
            ConnectTimeout = cloneFrom.ConnectTimeout;
            ReadTimeout = cloneFrom.ReadTimeout;
            WriteTimeout = cloneFrom.WriteTimeout;
            IdleTime = cloneFrom.IdleTime;
            SpinWaitTimeLimit = cloneFrom.SpinWaitTimeLimit;
            ErrorMesgType = cloneFrom.ErrorMesgType;
            InfoMesgType = cloneFrom.InfoMesgType;
            DebugMesgType = cloneFrom.DebugMesgType;
            TraceMesgType = cloneFrom.TraceMesgType;
            TraceDataMesgType = cloneFrom.TraceDataMesgType;
            RxBufferSize = cloneFrom.RxBufferSize;
            TxBufferSize = cloneFrom.TxBufferSize;
            TraceDataFormat = cloneFrom.TraceDataFormat;
            TraceDataEventMask = cloneFrom.TraceDataEventMask;
            TraceDataAsciiEscapeChar = cloneFrom.TraceDataAsciiEscapeChar;
        }

        #endregion

        #region Public Get-only properties for construction time set values

        /// <summary>Get only property.  Gives the PortConfig's Name value which will be used as the name of Serial Ports that are created from this PortConfig</summary>
        public string Name { get; private set; }

        /// <summary>
        /// Get only property.  Gives the PortConfig's target Specificiation String which will be used to choose the type of port and to specify how it is connected.
        /// Valid SpecStr values:
        ///	<para/>	&lt;ComPort port="com1" uartConfig="9600,n,8,1"/&gt;
        ///	<para/>	&lt;ComPort port="\\.\com200"&gt;&lt;UartConfig baud="9600" DataBits="8" Mode="rs232-3wire" Parity="none" StopBits="1"/&gt;&lt;/ComPort&gt;
        ///	<para/>	&lt;TcpClient addr="127.0.0.1" port="5002"/&gt;
        ///	<para/>	&lt;TcpServer addr="127.0.0.1" port="8001"/&gt;		addr is optional - will use any if no address is provided
        ///	<para/>	&lt;UdpClient addr="127.0.0.1" port="5005"/&gt;
        ///	<para/>	&lt;UdpServer addr="127.0.0.1" port="5006"/&gt;		addr is optional - will use any if no address is provided
        ///	<para/> &lt;NullPort/&gt;
        /// </summary>
        public string SpecStr { get; private set; }

        #endregion

        #region Public Set-only properties

        /// <summary>
        /// Set only property.  Verifies that no RxPacketendScannerDelegate has been selected (throws ArgumentException if not).  
        /// Sets the RxPacketEndStrArray based from a given LineTerm value.
        /// <para/>LineTerm.None, and LineTerm.Custom set the RxPacketEndStrArray to null.  
        /// <para/>LineTerm.Auto sets the RxPacketEndStrArray to contain "\r" and "\n" and turns on TrimWhitespaceOnRx (if not already on)
        /// <para/>LineTerm.CR sets the RxPacketEndStrArray to "\r" and 
        /// <para/>LineTerm.CRLF sets the RxPacketEndStrArray to "\r\n".
        /// </summary>
        public LineTerm RxLineTerm
        {
            set
            {
                if (RxPacketEndScannerDelegate != null)
                    new System.ArgumentException("RxLineTerm (RxPacketEndStrArray) and RxPacketEndScanerDelegate cannot both be used as the same time").Throw();

                switch (value)
                {
                    case LineTerm.None: RxPacketEndStrArray = emptyStrArray; break;
                    case LineTerm.Auto: RxPacketEndStrArray = new string[] { "\r", "\n" }; TrimWhitespaceOnRx = true; break;
                    case LineTerm.CR: RxPacketEndStrArray = new string[] { "\r" }; break;
                    case LineTerm.CRLF: RxPacketEndStrArray = new string[] { "\r\n" }; break;
                    case LineTerm.Custom: RxPacketEndStrArray = null; break;
                    default: RxPacketEndStrArray = null; break;
                }
            }
        }

        /// <summary>
        /// Set only property.  Sets the TxPacketEndStr based on the given LineTerm value.
        /// LineTerm.None, and LineTerm.Custom set TxPacketEndStr to the empty string.
        /// LineTerm.CR set the TxPacketEndStr to "\r".  LineTerm.CRLF and LineTerm.Auto set the TxPacketEndStr to "\r\n"
        /// </summary>
        public LineTerm TxLineTerm
        {
            set
            {
                switch (value)
                {
                    case LineTerm.None: TxPacketEndStr = string.Empty; break;
                    case LineTerm.Auto: TxPacketEndStr = "\r\n"; break;
                    case LineTerm.CR: TxPacketEndStr = "\r"; break;
                    case LineTerm.CRLF: TxPacketEndStr = "\r\n"; break;
                    case LineTerm.Custom: TxPacketEndStr = string.Empty; break;
                    default: TxPacketEndStr = string.Empty; break;
                }
            }
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Get/Set property.  May be set to define the IValuesInterconnection that the serial part will use for publishing its base state and its PortSpecStr (et. al.).
        /// Constructor default is set from Values.Instance singleton.  If this value is explicitly assigned to null then the part will not create IVA's and/or publish related information.
        /// </summary>
        public IValuesInterconnection PartBaseIVI { get; set; }

        /// <summary>
        /// Get/Set property.  May be set to define the IConfig instance that the serial part will use for specific configurable values.
        /// </summary>
        public IConfig IConfig { get; set; }

        /// <summary>
        /// Get/Set property.  
        /// Getter returns the contained array (if non-null) or the an empty string[] array.
        /// Setter verifies that no RxPacketendScannerDelegate has been selected (throws ArgumentException if not) and then sets the contained value from the given one.
        /// <para/>Note that the use of the RxLineTerm property setter directly assigns this value.
        /// </summary>
        public string[] RxPacketEndStrArray
        {
            get { return rxPacketEndStrArray ?? emptyStrArray; }
            set 
            {
                if (rxPacketEndScannerDelegate != null)
                    new System.ArgumentException("RxPacketEndStrArray and RxPacketEndScanerDelegate cannot both be used as the same time").Throw();

                rxPacketEndStrArray = value;
            }
        }

        /// <summary>
        /// Get/Set property.
        /// Getter returns the client provided PacketEndScannerDelegate, or null if the client has not assigned one.
        /// Setter verifies that the RxPacketEndStrArray does not specify any end patterns (throws ArgumentException if not) and then sets the contained value from the given one.
        /// </summary>
        public PacketEndScannerDelegate RxPacketEndScannerDelegate
        {
            get { return rxPacketEndScannerDelegate; }
            set 
            {
                if (!RxPacketEndStrArray.IsNullOrEmpty())
                    new System.ArgumentException("RxPacketEndScanerDelegate and RxPacketEndStrArray cannot both be used as the same time").Throw();

                rxPacketEndScannerDelegate = value; 
            }
        }

        /// <summary>
        /// Get/Set property.  Gives the string who's ascii byte equivilant will be appended to every Write operation.  
        /// Nothing will be appended if this string is assigned as null or the empty string.
        /// Returns empty string even if assigned to null.
        /// </summary>
        public string TxPacketEndStr
        {
            get { return txPacketEndStr ?? String.Empty; }
            set 
            {
                txPacketEndStr = value ?? String.Empty;
                txPacketEndStrByteArray = ByteArrayTranscoders.ByteStringTranscoder.Decode(txPacketEndStr);
            }
        }

        /// <summary>Get only property.  Returns the byte array equivilant of the TxPacketEndStr.</summary>
        public byte[] TxLineTermBytes
        {
            get { return (txPacketEndStrByteArray ?? emptyByteArray); }
        }

        /// <summary>Only valid in packet mode.  Selects that leading and trailing whitespace shall be removed from the data contained in each Packet produced by the port.</summary>
        [Obsolete("This property been replaced with the more clearly named TrimWhitespaceOnRx property.  Please replace use of StripWhitepaceOnRx accordingly.  [2014-10-24]")]
        public bool StripWhitespaceOnRx { get { return TrimWhitespaceOnRx; } set { TrimWhitespaceOnRx = value; } }

        /// <summary>Only valid in packet mode.  Selects that leading and trailing whitespace shall be removed from the data contained in each Packet produced by the port.</summary>
        public bool TrimWhitespaceOnRx { get; set; }

        /// <summary>Only valid in packet mode.  Selects that whitespace packets shall be removed/discarded from the Packet sequence produced by the port.</summary>
        public bool DiscardWhitespacePacketsOnRx { get; set; }

        /// <summary>This property is used in conjunction with TrimWhitespaceOnRx and DiscardWhitespacePacketsOnRx to control use of whitespace detection for ports that make use of a sliding buffer.</summary>
        public bool DetectWhitespace { get; set; }

        /// <summary>Set to true so that the port will automatically attempt to reconnect any time the current connection is lost.  When false the client is responsible for performing such actions explicitly when needed.</summary>
        public bool EnableAutoReconnect { get; set; }

        /// <summary>Defines the period of time after failing to connect before the next connection attempt can be made.  Only used when EnableAutoReconnect is true.</summary>
        public TimeSpan ReconnectHoldoff { get; set; }

        /// <summary>Defines the maximum period of time between attempting to open a connection and completing the connection before the connection attempt is viewed as having failed.  Not supported for all connection types.</summary>
        public TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// Defines the maximum period of time that a Read Action may be outstanding and be partially completed before it will be viewed has having failed due to the time limit being reached. 
        /// Use is context and port type dependant.
        /// </summary>
        public TimeSpan ReadTimeout { get; set; }

        /// <summary>
        /// Defines the maximum period of time between starting a write operation and completing the operation before it will be viewed has having failed due to the time limit being reached.
        /// Use is context and port type depedant.
        /// </summary>
        public TimeSpan WriteTimeout { get; set; }

        /// <summary>Defines the minimum time between successive Read Actions for the port to transition from receiving to idle.  Used as the packet timeout for packetized ports.  Used as the flush idle period for all ports.</summary>
        public TimeSpan IdleTime { get; set; }

        /// <summary>Defines the port's thread wait time limit for spinning when otherwise idle.  This is rarely overridden from its default value.</summary>
        public TimeSpan SpinWaitTimeLimit { get; set; }

        /// <summary>Defines the Logging.MesgType that is produced by the port for error messages that it produces</summary>
        public Logging.MesgType ErrorMesgType { get; set; }
        
        /// <summary>Defines the Logging.MesgType that is produced by the port for information messages that it produces</summary>
        public Logging.MesgType InfoMesgType { get; set; }
        
        /// <summary>Defines the Logging.MesgType that is produced by the port for debug messages that it produces</summary>
        public Logging.MesgType DebugMesgType { get; set; }
        
        /// <summary>Defines the Logging.MesgType that is produced by the port for trace messages that it produces</summary>
        public Logging.MesgType TraceMesgType { get; set; }

        /// <summary>Defines the Logging.MesgType that is produced by the port for data trace messages that it produces</summary>
        public Logging.MesgType TraceDataMesgType { get; set; }

        /// <summary>Defines the Logging.MesgType that is used for Trace level port Action Done events.  Defaults to Logger.MesgType.Debug</summary>
        public Logging.MesgType TraceActionDoneMesgType { get; set; }

        /// <summary>Defines the Logging.MesgType that is used for Trace level port Action Error events.  Defaults to Logger.MesgType.Info</summary>
        public Logging.MesgType TraceActionErrorMesgType { get; set; }

        /// <summary>Defines the Logging.MesgType that is used for Trace level port Action State events.  Defaults to Logger.MesgType.None</summary>
        public Logging.MesgType TraceActionStateMesgType { get; set; }

        /// <summary>Defines the Logging.MesgType that is used for Trace level port Action Update events.  Defaults to Logger.MesgType.None</summary>
        public Logging.MesgType TraceActionUpdateMesgType { get; set; }

        /// <summary>Defines the Logger GroupID that the port will use for its message logger.  When empty or null the port will use the default Logger GroupID.</summary>
        public string LoggerGroupID
        {
            get { return (loggerGroupID ?? String.Empty); }
            set { loggerGroupID = value; }
        }

        /// <summary>Defines the Logger GroupID that the port will use for its trace logger.  When empty or null the port will use the default Logger GroupID.</summary>
        public string TraceDataLoggerGroupID 
		{ 
			get { return (traceDataLoggerGroupID ?? String.Empty); } 
			set { traceDataLoggerGroupID = value; } 
		}

        /// <summary>Defines the initial LogGate that shall be used with the trace data logger.  Defaults to All.  Typically set to Debug to restrict trace data messages unless the source is explicitly elevated in the config.</summary>
        public Logging.LogGate TraceDataLoggerInitialLogGate { get; set; }

        /// <summary>Defines the receiver buffer size that the port will use.  Purpose and meaning is port type specific.</summary>
        public uint RxBufferSize { get; set; }

        /// <summary>Defines the transmit buffer size that the port will use.  Purpose and meaning is port type specific.</summary>
        public uint TxBufferSize { get; set; }

        /// <summary>Returns true if the IdleTime is non-zero</summary>
        public bool IdleTimerEnabled { get { return (IdleTime != TimeSpan.Zero); } }

        /// <summary>Gives the client specified TraceDataFormat that is to be used for a port created and configured from this spec struct.  When null the port will use default values derived from the connection type and/or from modular config.</summary>
        public TraceDataFormat ? TraceDataFormat { get; set; }

        /// <summary>Gives the client specified TraceDataEventMask that is to be used for a port created and configured from this spec struct.  When null the port will use default values derived from the connection type and/or from modular config.</summary>
        public TraceDataEvent? TraceDataEventMask { get; set; }

        /// <summary>Gives the client specified escape char that will be used when escaping the ascii text.  When null the port will use a default value of '&amp;' or from modular config</summary>
        public char? TraceDataAsciiEscapeChar { get; set; }

        #endregion

        #region Private fields

        private string[] rxPacketEndStrArray;
        private PacketEndScannerDelegate rxPacketEndScannerDelegate;
        private string txPacketEndStr;
        private byte[] txPacketEndStrByteArray;
        private string loggerGroupID;				        // for normal logger
        private string traceDataLoggerGroupID;				// for data trace logger

        private static string[] emptyStrArray = EmptyArrayFactory<string>.Instance;
        private static byte[] emptyByteArray = EmptyArrayFactory<byte>.Instance;

        #endregion
    }

    /// <summary>
    /// This flag enum is used to select various details about the type and inclusion of SerialIO Trace Data.
    /// <para/>None (0x00), OldXmlishStyle (0x01), UseMessageDataField (0x02), IncludeEscapedAscii (0x04), IncludeDottedAscii (0x08), IncludeHex (0x10), DefaultBinaryV2 (0x02), DefaultAsciiV2 (0x04)
    /// </summary>
    [Flags]
    public enum TraceDataFormat
    {
        None = 0x00,
        OldXmlishStyle = 0x01,
        UseMessageDataField = 0x02,
        IncludeEscapedAscii = 0x04,
        IncludeDottedAscii = 0x08,
        IncludeHex = 0x10,

        DefaultBinaryV2 = (UseMessageDataField),
        DefaultAsciiV2 = (IncludeEscapedAscii),
    }

    /// <summary>
    /// This flag enum is used to select which sources of trace data a given SerialIO port should include.
    /// <para/>None (0x00), Flush (0x01), Write (0x02), Read (0x04), Packet (0x08), DefaultBinaryV2 (0x07), DefaultPacketV2 (0x0b), All (0x0f)
    /// </summary>
    [Flags]
    public enum TraceDataEvent
    {
        None = 0x00,
        Flush = 0x01,
        Write = 0x02,
        Read = 0x04,
        Packet = 0x08,

        DefaultBinaryV2 = (Flush | Read | Write),
        DefaultPacketV2 = (Flush | Write | Packet),
        All = (Flush | Write | Read | Packet),
    }

	#endregion

	//-----------------------------------------------------------------
	#region IPort and Actions

	//-----------------------------------------------------------------

	/// <summary>
    /// This enum defines a set of values that are used to catagorize the results of a given Read or Write action on a SerialIO.Port.
    /// <para/>Values: None(0, default), ReadDone, ReadTimeout, ReadFailed, ReadCanceled, ReadRemoteEndHasBeenClosed, WriteDone, WriteFailed, WriteCanceled
    /// </summary>
	public enum ActionResultEnum
	{
		/// <summary>enum value when no result state is known (action has been reset or is in progress)</summary>
		None = 0,

		/// <summary>one or more bytes were successfully returned from the port</summary>
		ReadDone,
		/// <summary>no bytes have been or were received from the port during the stated time period</summary>
		ReadTimeout,
		/// <summary>some error was reported by the port during the read.  read will terminate with all bytes that had been previously received.</summary>
		ReadFailed,
		/// <summary>user requested cancel operation on this action</summary>
		ReadCanceled,
        /// <summary>special failure case where the other end of the connection has closed the connection and there are no more bytes to read from it.</summary>
        ReadRemoteEndHasBeenClosed,

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
	/// Each time the client starts the action, the io device will attempt to process it and place all resulting information into this 
	/// instance and then mark the action as complete.  The ActionResultEnum property contains a summary code for the completion type of the action.  
	/// If this summary code indicates that the action failed then the ResultCode property will be a non-empty string.  
	/// If the summary code indicates that the operation was completed normally then the ResultCode property will be the empty string.
	/// 
	/// Please note that the ownership of this object is shared as long as there is an action that is queued or in progress which references this object.  
	/// The action initiator must not modify the contents of this parameter object until such an action has completed.
	/// </remarks>
	public class ReadActionParam
	{
		// items that are based to the request
		private Byte [] buffer = null;
		private int bytesToRead = 0;

		// results from completion of the read
		private volatile int bytesRead = 0;
		private volatile ActionResultEnum actionResultEnum = ActionResultEnum.None;
		private volatile string resultCode = null;

        /// <summary>Constructs an empty object with no allocated buffer</summary>
		public ReadActionParam() { }
        /// <summary>Constructs the object and allocates buffer of indicated size.</summary>
        public ReadActionParam(int bufferSize) : this(new byte[bufferSize]) { }
        /// <summary>Constructs the object to use given buffer.</summary>
        public ReadActionParam(byte[] buffer) : this() { Buffer = buffer; }
        /// <summary>Constructs the object to use given buffer and to specify the number of bytes to read.</summary>
        public ReadActionParam(byte[] buffer, int bytesToRead) : this() { Buffer = buffer; BytesToRead = bytesToRead; }

        /// <summary>Clears the StartTime, BytesRead, ActionResultEnum and ResultCode</summary>
        /// <remarks>This method is normally only used by a Port object itself</remarks>
        public void Reset() { StartTime = QpcTimeStamp.Zero; bytesRead = 0; actionResultEnum = ActionResultEnum.None; resultCode = null; }
        /// <summary>Resets and object and then sets the StartTime to Now.</summary>
        /// <remarks>This method is normally only used by a Port object itself</remarks>
        public void Start() { Reset(); StartTime = QpcTimeStamp.Now; }

        /// <summary>Provides Get/Set access to the buffer that is referenced by this object.  Setter also sets the BytesToRead to the length of the given buffer.</summary>
        public Byte[] Buffer { get { return buffer; } set { buffer = value; bytesToRead = (buffer ?? emptyByteArray).Length; } }
        /// <summary>Provides Get/Set access to the Number of Bytes To Read.  Setter constrains the assigned value so that it cannot exceed the buffer length.</summary>
        public int BytesToRead { get { return bytesToRead; } set { bytesToRead = Math.Min(value, (buffer ?? emptyByteArray).Length); } }
        /// <summary>Set to true so that Read operatin will only complete after reading BytesToRead bytes.  Set to false so that the Read Action will complete as soon as it has read at least one byte.</summary>
        public bool WaitForAllBytes { get; set; }

        /// <summary>Gives the time at which the Read Action started, or zero if it has not been started.</summary>
        public QpcTimeStamp StartTime { get; private set; }
        /// <summary>Returns true if the StartTime is non-zero</summary>
        public bool HasBeenStarted { get { return !StartTime.IsZero; } }
        /// <summary>Returns the number of bytes read.  This may be used to setup a Read action to append to a set of bytes that are already in the corresponding buffer if this value is non-zero when the Read action is Started.</summary>
        public int BytesRead { get { return bytesRead; } set { bytesRead = value; } }
        /// <summary>Returns the ActionResultEnum value after the read action has completed.</summary>
        public ActionResultEnum ActionResultEnum { get { return actionResultEnum; } set { actionResultEnum = value; } }
        /// <summary>Returns the string ResultCode after the Read Action has completed.  This is generally the same value as the Read Action's ResultCode value.</summary>
        public string ResultCode { get { return resultCode; } set { resultCode = value; } }

        private static byte[] emptyByteArray = EmptyArrayFactory<byte>.Instance;
    }

	/// <summary>This class is used with the SerialIO.Port Write Action.  It contains the set of parameters to be passed to the action and the place where the action records the results once it has been performed</summary>
	/// <remarks>
	/// The client creates an instance of this class and fills in the buffer and then creates a Write Action using the Param instance.  
	/// Each time the client starts the action, the io device will attempt to process it and place all resulting information into this 
	/// instance and then mark the action as complete.  The ActionResultEnum property contains a summary code for the completion type of the action.  
	/// If this summary code indicates that the action failed then the ResultCode property will be a non-empty string.  
	/// If the summary code indicates that the operation was completed normally then the ResultCode property will be the empty string.
	/// 
	/// Please note that the ownership of this object is shared as long as there is an action that is queued or in progress which references this object.  
	/// The action initiator must not modify the contents of this parameter object until such an action has completed.
	/// </remarks>
	public class WriteActionParam
	{
		// items that are based to the request
		private Byte [] buffer = null;
		private int bytesToWrite = 0;
		private bool isNonBlocking = false;

		// results from completion of the read
		private volatile int bytesWritten = 0;
		private volatile ActionResultEnum actionResultEnum = ActionResultEnum.None;
		private volatile string resultCode = null;

        /// <summary>Constructs an empty object with no allocated buffer</summary>
        public WriteActionParam() { }
        /// <summary>Constructs the object to write the contents of the given buffer.  BytesToWrite is automatically set to the buffer length.</summary>
        public WriteActionParam(byte[] buffer) : this() { Buffer = buffer; }
        /// <summary>Constructs the object to write the contents of the given string value (transcoded using ByteArrayTranscoders.ByteStringTranscoder).  BytesToWrite is automatically set to the string's length.</summary>
        public WriteActionParam(string value) : this() { BufferAsStr = value; }
        /// <summary>Constructs the object to write from the given buffer and to specify the number of bytes to write.</summary>
        public WriteActionParam(byte[] buffer, int bytesToWrite) : this() { Buffer = buffer; BytesToWrite = bytesToWrite; }

        /// <summary>Clears the StartTime, BytesToWritten, ActionResultEnum and ResultCode</summary>
        /// <remarks>This method is normally only used by a Port object itself</remarks>
        public void Reset() { StartTime = QpcTimeStamp.Zero; bytesWritten = 0; actionResultEnum = ActionResultEnum.None; resultCode = null; }

        /// <summary>Resets the action params and assigns the Buffer to the given buffer, automatically setting the BufferLength to the Length of the given buffer (or 0 if buffer is null).</summary>
        public void SetupToWrite(byte[] buffer) { Reset(); Buffer = buffer; }
        /// <summary>Resets the action params and assigns the Buffer to the given string value transcoded using the ByteArrayTranscoder.ByteStringTranscoder, automatically setting the BufferLength to the Length of the given string (or 0 if the string is null or empty).</summary>
        public void SetupToWrite(string value) { Reset(); BufferAsStr = value; }
        /// <summary>Resets the action params and assigns the Buffer and BufferLength to the given values.</summary>
        public void SetupToWrite(byte[] buffer, int bytesToWrite) { Reset(); Buffer = buffer; BytesToWrite = bytesToWrite; }

        /// <summary>Resets and object and then sets the StartTime to Now.</summary>
        /// <remarks>This method is normally only used by a Port object itself</remarks>
        public void Start() { Reset(); StartTime = QpcTimeStamp.Now; }


        /// <summary>Provides Get/Set access to the buffer that is referenced by this object.  Setter also sets the BytesToWrite to the length of the given buffer.</summary>
        public Byte[] Buffer { get { return buffer; } set { buffer = value; bytesToWrite = (buffer ?? emptyByteArray).Length; } }
        /// <summary>Provides Get/Set access to the Number of Bytes To Write.  Setter constrains the assigned value so that it cannot exceed the buffer length.</summary>
        public int BytesToWrite { get { return bytesToWrite; } set { bytesToWrite = Math.Min(value, (buffer ?? emptyByteArray).Length); } }

        /// <summary>
        /// Provides Get/Set access to the byte array buffer that is referenced by this object, but converted to/from a System.String using the ByteArrayTranscoders.ByteStringTranscoder.  
        /// Setter also sets the BytesToWrite to the length of the given String.
        /// Setting this to the null string is identical to setting it to the empty string.
        /// </summary>
        public string BufferAsStr { get { return ByteArrayTranscoders.ByteStringTranscoder.Encode(Buffer ?? emptyByteArray); } set { Buffer = ByteArrayTranscoders.ByteStringTranscoder.Decode(value ?? string.Empty); } }

        /// <summary>
        /// Determines how the Write Action will be serviced.  When false the Write Action will wait in the port until the transmit buffer has available space.
        /// When true the write operation will fail immediately if the transmit buffer does not have any available space when the Write Action is started.
        /// If set to true then the write operation will only be accepted if the transmit buffer has some space available 
        /// </summary>
		public bool IsNonBlocking { get { return isNonBlocking; } set { isNonBlocking = value; } }

        /// <summary>Gives the time at which the Write Action started, or zero if it has not been started.</summary>
        public QpcTimeStamp StartTime { get; private set; }
        /// <summary>Returns true if the StartTime is non-zero</summary>
        public bool HasBeenStarted { get { return !StartTime.IsZero; } }
        /// <summary>Returns the number of bytes written.</summary>
        public int BytesWritten { get { return bytesWritten; } set { bytesWritten = value; } }
        /// <summary>Returns the ActionResultEnum value after the read action has completed.</summary>
        public ActionResultEnum ActionResultEnum { get { return actionResultEnum; } set { actionResultEnum = value; } }
        /// <summary>Returns the string ResultCode after the Write Action has completed.  This is generally the same value as the Write Action's ResultCode value.</summary>
        public string ResultCode { get { return resultCode; } set { resultCode = value; } }

        private static byte[] emptyByteArray = EmptyArrayFactory<byte>.Instance;
    }

	//-----------------------------------------------------------------

	/// <summary>Interface to client facet of a Port Read Action.  Paramter type is ReadActionParam which contains the details of how the action is to be performed</summary>
	public interface IReadAction : IClientFacetWithParam<ReadActionParam> { }

	/// <summary>Interface to client facet of a Port Write Action.  Paramter type is WriteActionParam which contains the details of how the action is to be performed</summary>
	public interface IWriteAction : IClientFacetWithParam<WriteActionParam> { }

    /// <summary>Interface to client facet of a Port GetNextPacket Action.  Result is the next packet extracted from the internal sliding buffer.</summary>
    public interface IGetNextPacketAction : IClientFacetWithResult<Packet> { }

    /// <summary>Interface to client facet of a Port Flush Action.  Parameter type is TimeSpan which contains the desired Flush Period to use.</summary>
    public interface IFlushAction : IClientFacetWithParam<TimeSpan> { }

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

        /// <summary>Gives information about the port's underlying behavior</summary>
        IPortBehavior PortBehavior { get; }

        /// <summary>Gives the current PortConfig</summary>
        PortConfig PortConfig { get; }

        /// <summary>Returns an IReadAction which refers to the given ReadActionParam instance and which may be used to execute a read using the ReadActionParam defined behavior</summary>
        /// <remarks>This may not be used with Port's that have been configured to use an internal sliding buffer.  Use CreateGetnextPacketAction instead in these cases.</remarks>
		IReadAction CreateReadAction(ReadActionParam param = null);

        /// <summary>Returns an IWriteAction which refers to the given WriteActionParam instance and which may be used to execute a write using the WriteActionParams defined behavior</summary>
        IWriteAction CreateWriteAction(WriteActionParam param = null);

        /// <summary>
        /// Returns an IBasicAction.  Underlying action TimeSpan parameter has been initilized to given value of flushWaitTime.  
        /// This action may be used to flush the port of characters for the given wait time period.
        /// </summary>
        IFlushAction CreateFlushAction(TimeSpan flushWaitLimit = default(TimeSpan));

        // the following methods are only useful with Port's that have an associated SlidingPacketBuffer

        /// <summary>
        /// Asynchronous property that returns true for Ports that have been configured to use a SlidingBuffer and which have at least one packet decoded from the sliding buffer.  
        /// Transitions from false to true will be associated with the part's BaseStateNotifier being notified.
        /// </summary>
        bool HasPacket { get; }

        /// <summary>
        /// Asynchronous property that returns the number of packets that are currently available to be dequeued from the Port.  
        /// This property will only be non-zero on Ports that have been configured to use a SlidingBuffer.
        /// Increases in the value of this value will be associated with the part's BaseStateNotifier being notified.
        /// </summary>
        int NumPacketsReady { get; }

        /// <summary>
        /// Returns an IGetNextPacketAction which may be executed to attempt to dequeue the next available Packet received by the Port.
        /// GetNextPacketActions do not wait.  If there is no packet available then the action completes with the result (packet) set to null.
        /// </summary>
        IGetNextPacketAction CreateGetNextPacketAction();
	}

    /// <summary>
    /// This interface defines as set of properies that may be used by clients to determine information about the behavior of the port that might effect
    /// how the client would normally use it.
    /// </summary>
    public interface IPortBehavior
    {
        /// <summary>Gives the DataDeliveryBehavior enum value that this port believes it implements</summary>
        DataDeliveryBehavior DataDeliveryBehavior { get; }

        /// <summary>Returns true if this port's underlying engine is network based (EtherNet or WIFI, ...)</summary>
        /// <remarks>false for COM ports, true for TCP and UDP ports.</remarks>
        bool IsNetworkPort { get; }

        /// <summary>Returns true if this port's DataDeliveryBehavior is ByteStream</summary>
        /// <remarks>true for COM ports and TCP ports.</remarks>
        bool IsByteStreamPort { get; }

        /// <summary>Returns true if this port's DataDeliveryBehavior is Datagram</summary>
        /// <remarks>true for UDP ports</remarks>
        bool IsDatagramPort { get; }

        /// <summary>Returns true if this port generally initiates the connection to the transport medium</summary>
        /// <remarks>true for COM ports and TCP and UDP Client ports</remarks>
        bool IsClientPort { get; }

        /// <summary>Returns true if this port generally accepts connections from the transport medium</summary>
        /// <remarks>true for TCP and UDP server ports</remarks>
        bool IsServerPort { get; }
    }

    /// <summary>
    /// This enum defines a set of values that help characterize the data delivery behavior for this port.  
    /// Currently the only normal values are ByteStream and Datagram.
    /// <para/>Undefined = 0, ByteStream, Datagram, None
    /// </summary>
    public enum DataDeliveryBehavior
    {
        /// <summary>Indicates that this port does not know what Data Delivery Behavior it implements.</summary>
        Undefined = 0,

        /// <summary>
        /// Indicates that bytes are generally delivered in order but that they deliniation between groups of bytes at the recieving end may be 
        /// arbitrarily different from the delinations at the sending end provided that the byte order is not modified.
        /// </summary>
        ByteStream,

        /// <summary>Indicates that bytes are generally delivered in the same units (and agregation) as they are written in</summary>
        Datagram,

        /// <summary>Indicates that this port does not deliver any data...</summary>
        None,
    }

	#endregion

	//-----------------------------------------------------------------
	#region Port Factory

    /// <summary>This Exception type is thrown for an invalid or unrecognized PortConfig SpecStr</summary>
    public class InvalidPortConfigSpecStrException : System.Exception
    {
        /// <summary>Exception constructor</summary>
        public InvalidPortConfigSpecStrException(string mesg) : base(mesg) { }
    }

	/// <summary>This static class provides static methods that may be used to create various types of SerialIO.IPort objects.</summary>
	public static partial class Factory
	{
        /// <summary>Create an IPort implementation object based on the first element in the SpecStr in the given portConfig struct.</summary>
		/// <param name="portConfig">Provides all configuration details for the port to be created.</param>
        /// <param name="allowThrow">Set to true to allow this method to throw an exception if the given portConfig.SpecStr cannot be understood.  Set to false to force the method to construct and return a NullPort instead.</param>
		/// <returns>The created SerialIO.Port object as an IPort.  Throws an InvalidPortConfigSpecStr exception if the required concrete type cannot be determined from the portConfig.SpecStr.</returns>
        /// <exception cref="InvalidPortConfigSpecStrException">thrown if the required concrete type cannot be determined from the portConfig.SpecStr and the allowThrow property is true</exception>
        public static IPort CreatePort(this PortConfig portConfig, bool allowThrow = true)
		{
			bool success = true;
			Utils.StringScanner specScan = new StringScanner(portConfig.SpecStr);
			System.Exception ex = null;

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
                success &= specScan.ExtractToken(out elementName, TokenType.AlphaNumeric, false, false, false); // do not require token end
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
					ex = new InvalidPortConfigSpecStrException(Utils.Fcns.CheckedFormat("MosaicLib.SerialIO.Factory.CreatePort call failed: error:'{0}' for port:'{1}'", comPortConfig.ErrorCode, portConfig.Name));
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
                ex = new InvalidPortConfigSpecStrException(Utils.Fcns.CheckedFormat("MosaicLib.SerialIO.Factory.CreatePort call failed: SpecStr parse error:'{0}'", epConfig.ErrorCode));
			}

			if (ex == null)
                ex = new InvalidPortConfigSpecStrException(Utils.Fcns.CheckedFormat("MosaicLib.SerialIO.Factory.CreatePort call failed: SpecStr:'{0}' not recognized for port:'{1}'", portConfig.SpecStr, portConfig.Name));

            if (allowThrow)
                throw ex;

            return new NullPort(portConfig);
		}
	}

    #endregion

	//-----------------------------------------------------------------
}
