//-------------------------------------------------------------------
/*! @file Logging.cs
 * @brief This provides the definitions, including interfaces, classes, types and functions that are essential to the MosaicLib logging system.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

namespace MosaicLib
{
	using System;
	using MosaicLib.Time;
    using System.Runtime.Serialization;
    using System.Collections.Generic;

	/// <summary>
	/// This static public class contains the definitions that are used to define the MosaicLib logging system.  
	/// This system is designed to provide efficient, source identified, gate controlable, multi-threaded logging 
	/// of messages and/or data from multiple sources into some number of, possibly source specific, message handlers.  
	/// </summary>
	/// <remarks>
	/// Terms:
    /// <list type="bullet">
    /// <item>
    ///		LogMessage - an object that is allocated from the distribution system, has a message placed within it and is then
    ///			emitted into the logging system where it may be recorded in one or more destinations (LogMessageHandlers)
    ///			LogMessages support use with strings and may be modified by making use of their Text.StringBuffer.  
    ///			LogMessages carry a sourceInfo (of the Logger from which they were allocated), a mesg type, a message, 
    ///			a string containing space delimited keyword(s) and a timestamp.  In addition they are generally 
    ///			annotated with the stack frame of the client code from which they were created so that the logging
    ///			infrastructure has access to the file name and line number of the source code that allocated the message.  
    /// </item>
    /// <item>
    ///		LoggerName - each Logger is associated with a string that defines its name.  All Loggers
    ///			that share the same value for this string will also share the same distribution
    ///			rules although each one may provide a seperate locally defined LoggingConfig value.
    /// </item>
    /// <item>
    ///		Logger - an object from which LogMessage(s) are obtained and into which LogMessage(s)
    ///			are typically emitted.  Each Logger instance represents a single portal into
    ///			the log distribution system.  Multiple Logger instances may be constructed using 
    ///			the same LoggerName, in which case they share the same common distribution rules.  
    ///			Each logger instances maintains a seperate instance LogGate level which may be used 
    ///			in conjuction with the corresponding gate levels of the message handlers that are 
    ///			associated with this LoggerName to efficiently control the allocation and generation 
    ///			of log message contents based on the permissabiliy of the message type at the time 
    ///			it is to be allocated.  Loggers also control whether the messages that they are responsible 
    ///			for allocating will have a valid StackFrame obtained and assigned (for access to file name and line number).
    ///			ILogger represents the interface that all loggers must support in order to be used with this system.
    /// </item>
    /// <item>
    ///		LoggingConfig - a combination of values the determine which types of messages a logger will actually
    ///			allocate and/or emit as well as all details about what is included in the messages (such as optional recording
    ///			of stack frames).  The portion of the LoggerConfig that is used to determine which types of messages to 
    ///			allocate is called the LogGate.
    /// </item>
    /// <item>
    ///		LogGate - provides a simple bitmask style class/mechanism that is used to implment an efficient gate test to determine
    ///			if a given mesg type should be allocated and/or emitted.  LogGate values are typically applied in
    ///			a least restrictive to most restrictive manner (as a LogMessage is routed) so that messages that cannot
    ///			be processed (are gated off in all possible places they might go) will never be generated while other
    ///			messages are forwarded to those consumers that might be interested in them.
    /// </item>
    /// <item>
    ///		IMesgEmitter - a shorthand helper class that is provided by loggers to client code and which can be used to generate
    ///			and emit simple formatted messages of a specific type while providing the client with a variety of useful 
    ///			shorthand variations on the means by which the message itself is generated.  Loggers can generally provide IMesgEmitters
    ///			for each mesg type as needed by their client.
    /// </item>
    /// </list>
    /// 
	/// The MosaicLib Logging system is loosely modeled after Apache's Log4j and Log4Net projects.  It generally preserves
	/// the concept that messages are associated with a source (string identifier) and that the gating and routing of the message
	/// to handlers (appenders in log4j terminology) are deteremined by the exact source id for the message.  This system has been
	/// modified to emphasize the performance of the distribution system rather than its complete flexability.  In this system
	/// each logger name belongs to a single log distribution group (generally the default group) which determines the set of
	/// message handlers to which the message may be given.  In addition this system supports a much more simple configuration
	/// model and formatting model than the log4j system does.
	/// 
	/// Finally the use of a public static partial class allows the term Logging to be used like a normal namespace but also allows
	/// global functions to be provided in that "namespace".  The notable downside to this approach is that client code cannot 
    /// drop the need for the Logging prefix with "using MosaicLib.Logging".
	/// </remarks>

	public static partial class Logging
	{
		//-------------------------------------------------------------------
		#region MesgType and related methods

		/// <summary>this enum defines a set of message types and an implicit ranking of their severities</summary>
		public enum MesgType
		{
            /// <summary>used as a gate to surpress passing all mesg types</summary>
			None = 0,
            /// <summary>used to record occurance of setup related (ctor) issues that may prevent use of the affected entity</summary>
			Fatal,
            /// <summary>used to record occurance of unexpected failures that might prevent future actions from operating correctly</summary>
			Error,
            /// <summary>used to record occurance of unexpected failures which are not expected to prevent future actions from operating correctly.</summary>
			Warning,
            /// <summary>used to record occurance of significant milestones and/or changes during normal operation of the system</summary>
			Signif,
            /// <summary>used to record occurance of relatively insignificant items.</summary>
			Info,
            /// <summary>used to record occurance of information that is intended to provide an even more detailed view</summary>
			Debug,
            /// <summary>used to record occurance of very frequent events such as those used to track data transfer, flow of control, construction and destruction, etc...</summary>
			Trace,
            /// <summary>used as a level to permit passing all mesg types</summary>
			All,			
			
			Max = Trace,
		}

        /// <summary>Returns mesgType.ToString()</summary>
		public static string ConvertToString(MesgType mesgType) { return mesgType.ToString(); }

        /// <summary>Returns 3 character version of mesgType.ToString()</summary>
        public static string ConvertToFixedWidthString(MesgType mesgType)
		{
			switch (mesgType)
			{
				case MesgType.None:
					return "   ";
				case MesgType.Fatal:
					return "Ftl";
				case MesgType.Error:
					return "Err";
				case MesgType.Warning:
					return "Wrn";
				case MesgType.Signif:
					return "Sig";
				case MesgType.Info:
					return "Inf";
				case MesgType.Debug:
					return "Dbg";
				case MesgType.Trace:
					return "Trc";
				case MesgType.All:
					return "All";
				default:
					return "UNKNOWN";
			}
		}

		#endregion

		//-------------------------------------------------------------------
		#region MesgTypeMask and LogGate

		/// <summary>
		/// Provides means by which MesgType enum codes are used to implement mask values as part of message type gating code
		/// </summary>
		/// <remarks>
		/// The basic funtionality of this value object is to implement a container that is used
		/// to test if given types of messages may be emitted or passed at various points in the
		/// log message distribution system.  This includes source filtering (preventing messages
		/// from actually being constructed or emitted into system) and distribution filtering where messages may
		/// go to destinct sub-sets of the full set of log sinks based on the values of the MesgTypeMask's
		/// that are associated with each one.
		/// 
		/// This class also implements updator methods that allow gates to be constructured and/or
		/// modified based on the contents of other gates.  In particular expressions such as "lhs |= rhs;" 
		/// may be used to allow the lhs gate to be made to be at least as permissive as the rhs gate.
		/// 
		/// Currently this implementation is based on a bit mask approach that also supports
		/// ranking based gating when the mesg type is used as a level mask.
		/// </remarks>

        [DataContract(Namespace=MosaicLib.Constants.LoggingNameSpace)]
		public struct MesgTypeMask
		{
			/// <summary>Enum defines how a MesgType is used.  Bit is as a single bit, Level is as a mask for all types at or above the given level</summary>
			public enum MaskType
			{
                /// <summary>Mask type to use when treating MesgType as a specific type</summary>
				Bit = 0,
                /// <summary>Mask type to use when treating MesgType as a severity level so that the resulting bit mask includes all levels that are at least as severe as the given one.</summary>
				Level			
			}

            /// <summary>Standard constructor for a bit type MesgTypeMask</summary>
			public MesgTypeMask(MesgType mesgType) : this(mesgType, MaskType.Bit) { }

            /// <summary>Constructor for mask of given type (Bit or Level) for given MesgType</summary>
			public MesgTypeMask(MesgType mesgType, MaskType maskType) : this(ConvertToBitMask(mesgType, maskType)) { }

            /// <summary>Returns new MesgTypeMask containing the logical or of the masks on the left and right sides</summary>
            public static MesgTypeMask operator |(MesgTypeMask lhs, MesgTypeMask rhs) { return new MesgTypeMask(lhs.maskBits | rhs.maskBits); }
            /// <summary>Returns new MesgTypeMask containing the logical and of the masks on the left and right sides</summary>
            public static MesgTypeMask operator &(MesgTypeMask lhs, MesgTypeMask rhs) { return new MesgTypeMask(lhs.maskBits & rhs.maskBits); }
            /// <summary>Returns new MesgTypeMask containing the logical inverse (compliment) of the given mask (on the right of the operator)</summary>
            public static MesgTypeMask operator ~(MesgTypeMask rhs) { return new MesgTypeMask(~rhs.maskBits); }

            /// <summary>Returns true if the left and right side contain the same mask value</summary>
            public static bool operator ==(MesgTypeMask lhs, MesgTypeMask rhs) { return lhs.maskBits == rhs.maskBits; }
            /// <summary>Returns true if left and and right side contain different mask values</summary>
            public static bool operator !=(MesgTypeMask lhs, MesgTypeMask rhs) { return !(lhs == rhs); }
            /// <summary>Returns true if the given rhs object is a MesgTypeMask and its mask is the same as this object's mask.</summary>
            public override bool Equals(object rhs) { return (rhs != null && rhs is MesgTypeMask && maskBits == ((MesgTypeMask)rhs).maskBits); }
            /// <summary>Provided for consistency</summary>
            public override int GetHashCode() { return maskBits.GetHashCode(); }

            /// <summary>Returns true if the mask includes the given mesg type.</summary>
            public bool IsTypeEnabled(MesgType testType) { return IsTypeEnabled(testType, MaskType.Bit); }
            /// <summary>Generates a bit mask using ConvertToBitMask(testType, maskType) and returns true if all bits in that mask are also set in this object's mask</summary>
            public bool IsTypeEnabled(MesgType testType, MaskType maskType) { return AreAllTestBitsEnabled(ConvertToBitMask(testType, maskType)); }
            /// <summary>Returns true if any bits in the rhs mask are also set in this object's mask</summary>
            public bool AreAnyBitsEnabled(ref MesgTypeMask rhs) { return AreAnyTestBitsEnabled(rhs.maskBits); }
            /// <summary>Returns true if all bits in the rhs mask are also set in this object's mask</summary>
            public bool AreAllBitsEnabled(ref MesgTypeMask rhs) { return AreAllTestBitsEnabled(rhs.maskBits); }

            /// <summary>Returns true if any of the given bits in testBits are also set in this object's mask</summary>
            public bool AreAnyTestBitsEnabled(int testBits) { return (0 != (maskBits & testBits)); }
            /// <summary>Returns true if all of the given bits in testBits are also set in this object's mask</summary>
            public bool AreAllTestBitsEnabled(int testBits) { return (testBits == (maskBits & testBits)); }

            /// <summary>Constructor for mask with initialBits specified explicitly</summary>
            public MesgTypeMask(int initialBits) { maskBits = initialBits; }

            /// <summary>Returns a mask derived from the given mesgType and maskType.</summary>
            /// <param name="mesgType">Defines the mesg type enum value from which the mask is derived.</param>
            /// <param name="maskType">Defines whether the mask is a Bit mask (only one bit set) or a Level mask (all bits at this or any more severe level are set).</param>
            /// <remarks>MesgType.None always produces a zero mask while MesgType.All always returns a mask with all bits set.</remarks>
            public static int ConvertToBitMask(MesgType mesgType, MaskType maskType)
			{
				int shift = (int) mesgType - 1;

				switch (mesgType)
				{
					case MesgType.None:
						return 0;
					case MesgType.All:
						return -1;
					default:
						break;
				}

				switch (maskType)
				{
					case MaskType.Bit:
						return unchecked(1 << shift);
					case MaskType.Level:
						return unchecked((1 << (shift + 1)) - 1);	// include all the bits at the current level and above it
					default:
						return 0;
				}
			}

			#region Private methods and variables

			/// <summary>
			/// maskBits is a bit field integer that is used in one of two ways.  It can represent the bit (or bits) for a given MesgType or
			/// it can represent a set of bits for one or more MesgTypes.  In the later form it is typically used as a form of GateLevel.
			/// This variable is declared volatile in order to simplify the locking requirements for the users of this object.
			/// </summary>
            
            [DataMember]
			private volatile int maskBits;

			#endregion

            /// <summary>
            /// Converts the contained mask bits into a printable version.  
            /// Supports both level and bit type masks as well as non-standard custom bit patterns.
            /// </summary>
            public override string ToString()
            {
                string detail;

                switch (maskBits)
                {
                    case 0: detail = "None"; break;
                    case -1: detail = "All"; break;
                    case (1 << (int)(MesgType.Fatal)): detail = "Fatal"; break;
                    case (1 << (int)(MesgType.Error)): detail = "Error"; break;
                    case (1 << (int)(MesgType.Warning)): detail = "Warning"; break;
                    case (1 << (int)(MesgType.Signif)): detail = "Signif"; break;
                    case (1 << (int)(MesgType.Info)): detail = "Info"; break;
                    case (1 << (int)(MesgType.Debug)): detail = "Debug"; break;
                    case (1 << (int)(MesgType.Trace)): detail = "Trace"; break;
                    case ((1 << ((int)(MesgType.Fatal) + 1)) - 1): detail = "Fatal+"; break;
                    case ((1 << ((int)(MesgType.Error) + 1)) - 1): detail = "Error+"; break;
                    case ((1 << ((int)(MesgType.Warning) + 1)) - 1): detail = "Warning+"; break;
                    case ((1 << ((int)(MesgType.Signif) + 1)) - 1): detail = "Signif+"; break;
                    case ((1 << ((int)(MesgType.Info) + 1)) - 1): detail = "Info+"; break;
                    case ((1 << ((int)(MesgType.Debug) + 1)) - 1): detail = "Debug+"; break;
                    case ((1 << ((int)(MesgType.Trace) + 1)) - 1): detail = "Trace+"; break;
                    default: detail = "Custom"; break;
                }

                return Utils.Fcns.CheckedFormat("{0}[${1:x2}]", detail, maskBits);
            }

            /// <summary>Dictionary of common mask patterns and their corresponding mask values, defined to match the return value from the ToString() method. </summary>
            public static readonly Dictionary<string, int> MesgTypeMaskNameMap
                = new Dictionary<string, int>()
                {
                    {"None", 0},
                    {"All", -1},

                    {"Fatal", (int)(MesgType.Fatal)},
                    {"Error", (int)(MesgType.Error)},
                    {"Warning", (int)(MesgType.Warning)},
                    {"Signif", (int)(MesgType.Signif)},
                    {"Info", (int)(MesgType.Info)},
                    {"Debug", (int)(MesgType.Debug)},
                    {"Trace", (int)(MesgType.Trace)},

                    {"Fatal+", ((1 << ((int)(MesgType.Fatal) + 1)) - 1)},
                    {"Error+", ((1 << ((int)(MesgType.Error) + 1)) - 1)},
                    {"Warning+", ((1 << ((int)(MesgType.Warning) + 1)) - 1)},
                    {"Signif+", ((1 << ((int)(MesgType.Signif) + 1)) - 1)},
                    {"Info+", ((1 << ((int)(MesgType.Info) + 1)) - 1)},
                    {"Debug+", ((1 << ((int)(MesgType.Debug) + 1)) - 1)},
                    {"Trace+", ((1 << ((int)(MesgType.Trace) + 1)) - 1)},
                };

            /// <summary>Provides a method that will attempt to generate a MesgTypeMask from a given string resulting from calling ToString on another MesgTypeMask.  Returns true if the original could be reconstructed or false if the string was not recognized and was not decodable.</summary>
            public static bool TryParse(string s, out MesgTypeMask mtm)
            {
                int maskBits = 0;
                bool success = false;
                Utils.StringScanner scan = new MosaicLib.Utils.StringScanner(s);

                if (scan.ParseTokenAndMapValueByName<int>(MesgTypeMaskNameMap, out maskBits) && (scan.IsAtEnd || scan.Char == '['))     // ignore trailing [$hh] if it is present
                    success = true;
                else if (scan.MatchToken("Custom[$") && scan.ParseValue(out maskBits) && scan.MatchToken("]") && scan.IsAtEnd)
                    success = true;

                mtm = new MesgTypeMask(maskBits);
                return success;
            }
		}

		/// <summary>
		/// This struct is a container for a MesgTypeMask which is variation on the MesgTypeMask with the simple constructor set to use the 
		/// given MesgType as a gate level rather than a standard type mask.
		/// </summary>

        [DataContract(Namespace = MosaicLib.Constants.LoggingNameSpace)]
        public struct LogGate
		{
            /// <summary>Standard constructor to build a level type mask (as opposed to a bit type mask)</summary>
			public LogGate(MesgType mesgType) { mask = new MesgTypeMask(mesgType, MesgTypeMask.MaskType.Level); }
            /// <summary>Copy constructor</summary>
            public LogGate(LogGate rhs) : this(rhs.mask) { }
            /// <summary>Copy constructor from a MesgTypeMask</summary>
            public LogGate(MesgTypeMask rhs) { mask = rhs; }

            /// <summary>Returns new LogGate containing the logical or of the masks on the left and right sides</summary>
			public static LogGate operator |(LogGate lhs, LogGate rhs) { return new LogGate(lhs.mask | rhs.mask); }
            /// <summary>Returns new LogGate containing the logical and of the masks on the left and right sides</summary>
			public static LogGate operator &(LogGate lhs, LogGate rhs) { return new LogGate(lhs.mask & rhs.mask); }
            /// <summary>Returns new LogGate containing the logical inverse (compliment) of the given mask (on the right of the operator)</summary>
			public static LogGate operator ~(LogGate lhs) { return new LogGate(~lhs.mask); }

            /// <summary>Returns true if the left and right side contain the same mask value</summary>
			public static bool operator ==(LogGate lhs, LogGate rhs) { return lhs.mask == rhs.mask; }
            /// <summary>Returns true if left and and right side contain different mask values</summary>
			public static bool operator !=(LogGate lhs, LogGate rhs) { return !(lhs == rhs); }
            /// <summary>Returns true if the given rhs object is a LogGate and its mask is the same as this object's mask.</summary>
			public override bool Equals(object rhs) { return (rhs != null && rhs is LogGate && mask == ((LogGate) rhs).mask); }
            /// <summary>Provided for consistency</summary>
            public override int GetHashCode() { return mask.GetHashCode(); }

            /// <summary>Returns true if the mask includes the given mesg type.</summary>
            public bool IsTypeEnabled(MesgType testType) { return mask.IsTypeEnabled(testType, MesgTypeMask.MaskType.Bit); }

            /// <summary>private value object that holds the contained MesgTypeMask.</summary>
			private MesgTypeMask mask;
            /// <summary>get/set property that gives external access to the underlying MesgTypeMask that this object contains and uses.</summary>
            public MesgTypeMask MesgTypeMask { get { return mask; } set { mask = value; } }

            /// <summary>Static LogGate that has no message types enabled.</summary>
            public static LogGate None { get { return new LogGate(MesgType.None); } }
            /// <summary>Static LogGate that has all Error and higher message types enabled.</summary>
            public static LogGate Error { get { return new LogGate(MesgType.Error); } }
            /// <summary>Static LogGate that has all Warning and higher message types enabled.</summary>
            public static LogGate Warning { get { return new LogGate(MesgType.Warning); } }
            /// <summary>Static LogGate that has all Signif and higher message types enabled.</summary>
            public static LogGate Signif { get { return new LogGate(MesgType.Signif); } }
            /// <summary>Static LogGate that has all Info and higher message types enabled.</summary>
            public static LogGate Info { get { return new LogGate(MesgType.Info); } }
            /// <summary>Static LogGate that has all Debug and higher message types enabled.</summary>
            public static LogGate Debug { get { return new LogGate(MesgType.Debug); } }
            /// <summary>Static LogGate that has all Trace and higher message types enabled.</summary>
            public static LogGate Trace { get { return new LogGate(MesgType.Trace); } }
            /// <summary>Static LogGate that has all message types enabled.</summary>
            public static LogGate All { get { return new LogGate(MesgType.All); } }
		}

		#endregion

		//-------------------------------------------------------------------
		#region LoggerID

        /// <summary>Special value used as a LoggerID to indicate that no logger is identified</summary>
		public const int LoggerID_Invalid = -1;
        /// <summary>Special value used as a LoggerID to cover specific internal handling cases.</summary>
        public const int LoggerID_InternalLogger = -2;

        /// <summary>Returns true if the given lid is >= 0</summary>
		public static bool IsLoggerIDValid(int lid) { return (lid >= 0); }

		#endregion

		//-------------------------------------------------------------------
		#region LoggerConfig, source and observer types

        /// <summary>Define the interface provided by all LoggerConfig objects.  These objects are used to define and control logging for specific groups of loggers and log message handlers.</summary>
		public interface ILoggerConfig
		{
            /// <summary>Name of corresponding Logger Group.  May be empty if no group is associated.  For LogMessageHandlers, this name will be derived from the Handler's name</summary>
			String GroupName { get; }

            /// <summary>Gives access to the LogGate object which defines the set of messages that are permitted to be generated by, or passed through, a given interface.</summary>
            LogGate LogGate { get; }

            /// <summary>Query method to use the LogGate to determine if a given MesgType is currently enabled.</summary>
            bool IsTypeEnabled(MesgType testType);

            /// <summary>Propery that is set to true if this Logger should record stack frames for source file and line number extraction.</summary>
            bool RecordSourceStackFrame { get; }

            /// <summary>Determines allocation and release style for individual messages.</summary>
            /// <remarks>
            /// In LogMessageHandlers, this property indicates if the handler supports use of reference counted release strategy for the messages it handles (or not).  Some handlers may not support this and as such will prevent their loggers from using pooled log messages.
            /// In Loggers, this property determines how the distribution system will allocate each message used by the logger.  If the property is true then pooled messages may be used for internally allocated and emitted used messages, otherwise a GC based message allocation and release approach is used.
            /// </remarks>
            bool SupportsReferenceCountedRelease { get; }
		}

        /// <summary>Implement standard storage object for supporting the ILoggerConfig inteface.  These objects are used to define and control logging for specific groups of loggers and log message handlers.</summary>
		public struct LoggerConfig : ILoggerConfig
		{
            // all LoggerConfig objects are constructed using default constructor.  Client may set properties using post CTOR {} notation.

            /// <summary>Name of corresponding Logger Group.  May be empty if no group is associated.  For LogMessageHandlers, this name will be derived from the Handler's name</summary>
            public String GroupName { get; set; }

            /// <summary>Gives access to the LogGate object which defines the set of messages that are permitted to be generated by, or passed through, a given interface.</summary>
            public LogGate LogGate { get; set; }

            /// <summary>Query method to use the LogGate to determine if a given MesgType is currently enabled.</summary>
            public bool IsTypeEnabled(MesgType testType) { return LogGate.IsTypeEnabled(testType); }

            /// <summary>Propery that is set to true if this Logger should record stack frames for source file and line number extraction.</summary>
            public bool RecordSourceStackFrame { get; set; }    // synonim for recordFileAndLine;            /// <summary></summary>

            /// <summary>Determines allocation and release style for individual messages.</summary>
            /// <remarks>
            /// In LogMessageHandlers, this property indicates if the handler supports use of reference counted release strategy for the messages it handles (or not).  Some handlers may not support this and as such will prevent their loggers from using pooled log messages.
            /// In Loggers, this property determines how the distribution system will allocate each message used by the logger.  If the property is true then pooled messages may be used for internally allocated and emitted used messages, otherwise a GC based message allocation and release approach is used.
            /// </remarks>
            public bool SupportsReferenceCountedRelease { get; set; }

            /// <summary>
            /// Or combination operator: returns new LoggerConfig using lhs's name and the OR combination of the remaining properties from both the lhs and rhs
            /// </summary>
			public static LoggerConfig operator |(LoggerConfig lhs, LoggerConfig rhs)
            { 
                return new LoggerConfig() 
                { 
                    GroupName = lhs.GroupName
                    , LogGate = (lhs.LogGate | rhs.LogGate)
                    , RecordSourceStackFrame = (lhs.RecordSourceStackFrame | rhs.RecordSourceStackFrame) 
                    , SupportsReferenceCountedRelease = (lhs.SupportsReferenceCountedRelease | rhs.SupportsReferenceCountedRelease)
                }; 
            }

            /// <summary>
            /// And combination operator: returns new LoggerConfig using lhs's name and the AND combination of the remaining properties from both the lhs and rhs
            /// </summary>
            public static LoggerConfig operator &(LoggerConfig lhs, LoggerConfig rhs)
            { 
                return new LoggerConfig() 
                { 
                    GroupName = lhs.GroupName
                    , LogGate = (lhs.LogGate & rhs.LogGate)
                    , RecordSourceStackFrame = (lhs.RecordSourceStackFrame & rhs.RecordSourceStackFrame)
                    , SupportsReferenceCountedRelease = (lhs.SupportsReferenceCountedRelease & rhs.SupportsReferenceCountedRelease)
                };
            }

            private static LoggerConfig none = new LoggerConfig() { LogGate = LogGate.None, RecordSourceStackFrame = false, SupportsReferenceCountedRelease = true };
            private static LoggerConfig allWithFL = new LoggerConfig() { LogGate = LogGate.All, RecordSourceStackFrame = true, SupportsReferenceCountedRelease = true };
            private static LoggerConfig allNoFL = new LoggerConfig() { LogGate = LogGate.All, RecordSourceStackFrame = false, SupportsReferenceCountedRelease = true };

            /// <summary>Returns LoggerConfig with LogGate = LogGate.None, RecordSourceStackFrame = false, SupportsReferenceCountedRelease = true</summary>
			public static LoggerConfig None { get { return none; } }
            /// <summary>Returns LoggerConfig with LogGate = LogGate.All, RecordSourceStackFrame = true, SupportsReferenceCountedRelease = true</summary>
            public static LoggerConfig AllNoFL { get { return allNoFL; } }
            /// <summary>Returns LoggerConfig with LogGate = LogGate.All, RecordSourceStackFrame = false, SupportsReferenceCountedRelease = true</summary>
            public static LoggerConfig AllWithFL { get { return allWithFL; } }
		}

        /// <summary> class used by distribution system to generate and distribute the sequenced LoggerConfig values that are observed using the SequencedLoggerConfigObserver objects. </summary>
		public class SequencedLoggerConfigSource
		{
			public SequencedLoggerConfigSource() { }
			public SequencedLoggerConfigSource(LoggerConfig initialLoggerConfig) { LoggerConfig = initialLoggerConfig; }

			private LoggerConfig localValueCopy;
			private Utils.GuardedSequencedValueObject<LoggerConfig> guardedValue = new MosaicLib.Utils.GuardedSequencedValueObject<LoggerConfig>();

			public LoggerConfig LoggerConfig
			{
				get { return localValueCopy; }
				set { if (!localValueCopy.Equals(value) || !guardedValue.HasBeenSet) { guardedValue.Object = localValueCopy = value; } }
			}

			public Utils.ISequencedObjectSource<LoggerConfig, int> LoggerConfigSource { get { return guardedValue; } }
		}

		/// <summary> class used by Logger implementations to manage their cached copy of the distribution LoggerConfig level for this named source.  This object may be used as an ILoggerConfig.</summary>
		public class SequencedLoggerConfigObserver : Utils.SequencedValueObjectSourceObserver<LoggerConfig, int>, ILoggerConfig
		{
			public SequencedLoggerConfigObserver(Utils.ISequencedObjectSource<LoggerConfig, int> lcSource) : base(lcSource) { }

            /// <summary>Returns cached copy from LoggerConfig Source</summary>
            public ILoggerConfig LoggerConfig { get { return Object; } }

            /// <summary>Returns GroupName from cached copy from LoggerConfig Source</summary>
			public String GroupName { get { return LoggerConfig.GroupName; } }
            /// <summary>Returns LogGate from cached copy from LoggerConfig Source</summary>
            public LogGate LogGate { get { return LoggerConfig.LogGate; } }
            /// <summary>Calls IsTypeEnabled on cached copy from LoggerConfig Source</summary>
            public bool IsTypeEnabled(MesgType testType) { return LoggerConfig.IsTypeEnabled(testType); }
            /// <summary>Returns RecordSourceStackFrame from cached copy from LoggerConfig Source</summary>
            public bool RecordSourceStackFrame { get { return LoggerConfig.RecordSourceStackFrame; } }
            /// <summary>Returns SupportsReferenceCountedRelease from cached copy from LoggerConfig Source</summary>
            public bool SupportsReferenceCountedRelease { get { return LoggerConfig.SupportsReferenceCountedRelease; } }
		}

		#endregion

		//-------------------------------------------------------------------
		#region LogSourceInfo

		/// <summary>
		/// The LoggerSourceInfo is a reference object that is created by the Logging distribution service and 
		/// which is used to retain a single shared constant copy of a logger's id and name and to give
		/// logger's access to the SequencedLoggerConfigSource that is maintained for each logger.  
		/// </summary>
		/// <remarks>
		/// Loggers will include their own SequencedLoggerConfigObserver to retain a cached copy of the source config information.
		/// </remarks>

		public class LoggerSourceInfo
		{
			readonly int		loggerID;
			readonly string		loggerName;
			readonly Utils.ISequencedObjectSource<LoggerConfig, int> loggerConfigSource;
			private static readonly Utils.ISequencedObjectSource<LoggerConfig, int> allLoggerConfigSource = new SequencedLoggerConfigSource(LoggerConfig.AllWithFL).LoggerConfigSource;

			public LoggerSourceInfo(int loggerID, string loggerName) : this(loggerID, loggerName, allLoggerConfigSource) { }
			public LoggerSourceInfo(int loggerID, string loggerName, Utils.ISequencedObjectSource<LoggerConfig, int> loggerConfigSource) 
			{
				this.loggerID = loggerID;
				this.loggerName = loggerName;
				this.loggerConfigSource = loggerConfigSource;
			}

			public LoggerSourceInfo()
			{
				loggerID = LoggerID_Invalid;
				loggerName = "[INVALID]";
				loggerConfigSource = allLoggerConfigSource;
			}

			public int ID { get { return loggerID; } }
			public string Name { get { return loggerName; } }
			public Utils.ISequencedObjectSource<LoggerConfig, int> LoggerConfigSource { get { return loggerConfigSource; } }
			public bool IsValid { get { return IsLoggerIDValid(ID) && !string.IsNullOrEmpty(Name) && LoggerConfigSource != null; } }
		}

		#endregion

		//-------------------------------------------------------------------
		#region LogMessage class and related definitions

		public const int NullMessageSeqNum = 0;

        //-------------------------------------------------------------------

        /// <summary>This interface defines the publically accessible properties and methods that a LogMessage provides for accessing its stored contents.</summary>
        public interface ILogMessage
        {
            /// <summary>Returns the name of the sourcing logger object or a fixed string if there is no such object.</summary>
            string LoggerName { get; }
            /// <summary>Returns the MesgType of the contained message</summary>
            MesgType MesgType { get; }
            /// <summary>Returns the string body of the message or the empty string if none was given</summary>
            string Mesg { get; }
            /// <summary>Returns a, possibly null, byte array of binary data that is associated with this message.</summary>
            byte[] Data { get; }
            /// <summary>Returns a, possibly empty, array of keyword strings that are associated with this message.</summary>
            string[] KeywordArray { get; }
            /// <summary>Returns an easily printable concatinated and comma delimited copy of the strings in the KeywordArray</summary>
            string Keywords { get; }
            /// <summary>Returns the QpcTimeStamp at which this message was emitted or zero if it has not been emitted</summary>
            QpcTimeStamp EmittedQpcTime { get; }
            /// <summary>Returns the sequence number that the distribution system assigned to this message when it was emitted, or zero if it has not been emitted</summary>
            int SeqNum { get; }
            /// <summary>Returns the ThreadID of the thread that setup this message</summary>
            int ThreadID { get; }
            /// <summary>Returns the DataTime taken at the time the message was emitted or the empty DateTime if it has not bee emitted.</summary>
            DateTime EmittedDateTime { get; }
            /// <summary>Method used to get the EmittedDataTime formatted as a standard string.</summary>
            string GetFormattedDateTime();
            /// <summary>Method used to get the EmittedDataTime in one of the standard supported formats.</summary>
            string GetFormattedDateTime(Utils.Dates.DateTimeFormat dtFormat);
        }

        //-------------------------------------------------------------------

		/// <summary>This class implements the public sharable container that is used for all information that is to be logged using this logging system.</summary>
		/// <remarks>
		/// Messages are generated from a specific source and also include:
		///		a MesgType, a message string, an optional space delimited string containing relevant keywords, 
		///		an optional reference to the stack frame from which the message was, originally, allocated and from which the
		///		distribution system can get the file name an line number of the allocating code.
		/// </remarks>

        public class LogMessage : Utils.Pooling.RefCountedRefObjectBase<LogMessage>, ILogMessage
		{
            /// <summary>Default constructor.</summary>
			public LogMessage () {}

            /// <summary>Copy constructor</summary>
            public LogMessage(LogMessage rhs) 
            {
                loggerSourceInfo = rhs.LoggerSourceInfo;
                mesgType = rhs.MesgType;
                mesg = rhs.Mesg;
                sourceStackFrame = rhs.SourceStackFrame;
                keywordArray = new List<string>(rhs.KeywordArray).ToArray();
                data = ((rhs.Data != null) ? new List<byte>(rhs.Data).ToArray() : null);
                emitted = rhs.emitted;
                emittedDateTime = rhs.EmittedDateTime;
                emittedQpcTime = rhs.EmittedQpcTime;
                seqNum = rhs.SeqNum;
                threadID = rhs.ThreadID;
            }

            /// <summary>Resets contents to default state</summary>
            public void Reset() { ResetEmitted(); mesgType = MesgType.None; mesg = string.Empty; KeywordArray = null; sourceStackFrame = null; threadID = -1; SeqNum = NullMessageSeqNum; }
            /// <summary>Asserts that the message is in the not-emitted state.  Primarily used for enforcing that pool messages are recycled correctly.</summary>
            public void AssertNotEmitted(string caller) { if (Emitted) Utils.Assert.BreakpointFault("AssertNotEmitted failed for:" + caller); }

            /// <summary>Sets up contents from a given source with no explicitly given Mesg</summary>
            public void Setup(LoggerSourceInfo loggerSourceInfo, MesgType mesgType, System.Diagnostics.StackFrame sourceStackFrame)
            { Mesg = null; this.loggerSourceInfo = loggerSourceInfo; this.mesgType = mesgType; this.sourceStackFrame = sourceStackFrame; KeywordArray = null; SetThreadID(); }
            /// <summary>Sets up contents from a given source with an explicitly given Mesg</summary>
            public void Setup(LoggerSourceInfo loggerSourceInfo, MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame)
            { Mesg = mesg; this.loggerSourceInfo = loggerSourceInfo; this.mesgType = mesgType; this.sourceStackFrame = sourceStackFrame; KeywordArray = null; SetThreadID(); }

			// information about the message source
			private LoggerSourceInfo loggerSourceInfo = null;					// messages all share a pointer to the same logger source id and string
            /// <summary>Returns the LoggerSourceInfo of the logger that generated this message or null if message is in default state or no such source was given</summary>
            public LoggerSourceInfo LoggerSourceInfo { get { return loggerSourceInfo; } }
            /// <summary>Returns true if the current SourceInfo is non null.</summary>
            public bool IsLoggerSourceInfoValid { get { return (loggerSourceInfo != null); } }

            /// <summary>Returns the LoggerID in the Log Distribution System for the LoggerSourceInfo or LoggerID_Invalid if there is no valid LoggerSourceInfo</summary>
            public int LoggerID { get { return (IsLoggerSourceInfoValid ? loggerSourceInfo.ID : LoggerID_Invalid); } }
            /// <summary>Returns the LoggerName for the logger reference in the given LoggerSourceInfo or a fixed string ("NULL_LOGGER") if there is none</summary>
            public string LoggerName { get { return GetLoggerName("NULL_LOGGER"); } }
            /// <summary>helper method used to Get LoggerName where caller specifies the string to return when the message has no defined LoggerSourceInfo</summary>
            public string GetLoggerName(string nullLoggerName) { return (IsLoggerSourceInfoValid ? loggerSourceInfo.Name : nullLoggerName); }

			// information about the actual event

			private MesgType mesgType = MesgType.None;
            /// <summary>Returns the MesgType that the message was setup for or MesgType.None if the message has not been setup.</summary>
            public MesgType MesgType { get { return mesgType; } }

			// the message text.
			private string mesg = null;

            /// <summary>Returns the current Message Body.  Returns empty string if the message has not been given a body.  Setter allows the contained mesg to be updated, provided that the message has not been emitted.</summary>
            public string Mesg 
			{ 
				get { return ((mesg != null) ? mesg : string.Empty); } 
				set { AssertNotEmitted("Mesg property Set"); mesg = value; }
			}

			// the message data - an optional binary block of bytes

			private byte [] data = null;

            /// <summary>Returns the current Message Data body as a block of bytes or null if there are none.  Setter allows the contained data body to be updated provided that the message has not been emitted. </summary>
            public byte[] Data
			{
				get { return data; }
                set { AssertNotEmitted("Data property Set"); data = value; }
			}

			// the message can contain keywords
            private static readonly string[] emptyKeywordArray = new string[0];
            private string[] keywordArray = null;
            /// <summary>Returns the current KeywordArray (which may be empty).  Setter requires that the message has not been emitted. </summary>
            public string[] KeywordArray { get { return (keywordArray != null ? keywordArray : emptyKeywordArray); } set { AssertNotEmitted("KeywordArray property Set"); keywordArray = value; } }
            /// <summary>Returns an easily printable, comma seperated string of the given keywords or the empty string if there are none</summary>
            public string Keywords { get { return (keywordArray != null ? String.Join(",", keywordArray) : String.Empty); } }

			// optonal information about the source file and line where message was generated.
			private System.Diagnostics.StackFrame sourceStackFrame = null;
            /// <summary>Returns the System.Diagnostics.StackFrame from which the message was created/emitted.  May be null if SourceStackFrames are not being acquired.</summary>
            public System.Diagnostics.StackFrame SourceStackFrame { get { return sourceStackFrame; } }

			// information about wether the message has been emitted and if so when
			//	it was emitted (qpc and local)

			private bool emitted = false;
			private QpcTimeStamp emittedQpcTime = QpcTimeStamp.Zero;
			private DateTime emittedDateTime = new DateTime();

            /// <summary>True if the message has been given to the distribution system.  Cleared after distribution is complete or the message has been cloned.</summary>
            public bool Emitted
			{
				get { return emitted; }
			}

            /// <summary>Resets the message to the non-emitted state. - used during internal Logging infrastructure message recycling.</summary>
            void ResetEmitted()
			{
				emitted = false;
				emittedQpcTime.Time = 0.0;
			}

            /// <summary>Marks the message as having been emitted.</summary>
            public void NoteEmitted() 
			{ 
				AssertNotEmitted("NoteEmitted call");
                if (loggerSourceInfo == null)
    				Utils.Assert.BreakpointFault("NoteEmitted failed: SourceID == null");

                emitted = true;
				emittedQpcTime.SetToNow();
				emittedDateTime = DateTime.Now;
			}
            /// <summary>Returns the QpcTimeStamp at which the message was first emitted. - non transferable between process spaces.</summary>
            public QpcTimeStamp EmittedQpcTime { get { return emittedQpcTime; } }

			private int	seqNum = NullMessageSeqNum;		// owned and filled in during distribution, non-zero when valid
            /// <summary>Returns the message Sequence number as assigned when the message was emitted.  May be used to determine when the message has been delivered from the distribution system.</summary>
            public int SeqNum { get { return seqNum; } internal set { seqNum = value; } }

			private int	threadID = -1;
            /// <summary>Returns the ThreadID that initially setup the message.</summary>
            public int ThreadID { get { return threadID; } }
            /// <summary>Method used to set the contained ThreadID during setup</summary>
            private void SetThreadID() { threadID = System.Threading.Thread.CurrentThread.ManagedThreadId; }

            /// <summary>Returns the DataTime taken at the time the message was emitted or the empty DateTime if it has not bee emitted.</summary>
            public DateTime EmittedDateTime { get { return emittedDateTime; } }
            /// <summary>Method used to get the EmittedDataTime formatted as a standard string.</summary>
            public string GetFormattedDateTime() { return GetFormattedDateTime(Utils.Dates.DateTimeFormat.LogDefault); }
            /// <summary>Method used to get the EmittedDataTime formatted in one of the standard supported formats.</summary>
            public string GetFormattedDateTime(Utils.Dates.DateTimeFormat dtFormat) { return Utils.Dates.CvtToString(ref emittedDateTime, dtFormat); }

            /// <summary>Internal method used to cleanup the contents of a message on release to the message pool for internally recycled messages in the distribution system.</summary>
            protected override void PerformPostReleaseCleanup() { Reset(); } // provide non-default post release cleanup behavior
		};

		#endregion

		//-------------------------------------------------------------------
		#region IMesgEmitter

		/// <summary>
		/// This interface provides a set of client callable utility methods that allow the client to easily format and emit
		/// log messages of a given type to the underlying logger object to which this interface is bound.  ILogger objects
		/// provide IMesgEmitters for each of the standard LogMessage levels and most messages that are generated by a client will
		/// be generated and emitted through a IMesgEmitter.
		/// </summary>
		/// <remarks>
		/// The use of this interface (and the associated MesgEmitterImpl class) reduces the number of methed signature variations that
		/// are needed to support a full set of client message generation use models by removing the MesgType from the equation.  This allows
		/// us to use optimized versions of the 0, 1, 2 and 3 argument string format patterns and helps minimize the cost of including Emit 
		/// calls for these numbers of arguments where the Emit call is generally disabled in the source.
		/// </remarks>

        public interface IMesgEmitter
		{
            /// <summary>True if the IMesgEmitter is valid and its mesgType is enabled in the parent logger object</summary>
            bool IsEnabled { get; }

            /// <summary>Gives the caller access to the message type that this emitter will generate</summary>
            MesgType MesgType { get; }

            void Emit(string str);
            void Emit(string fmt, object arg0);
            void Emit(string fmt, object arg0, object arg1);
            void Emit(string fmt, object arg0, object arg1, object arg2);
            void Emit(string fmt, params object[] args);
            void Emit(IFormatProvider provider, string fmt, params object[] args);
            
            void Emit(int skipNStackLevels, string str);
			void Emit(int skipNStackLevels, string fmt, object arg0);
			void Emit(int skipNStackLevels, string fmt, object arg0, object arg1);
			void Emit(int skipNStackLevels, string fmt, object arg0, object arg1, object arg2);
			void Emit(int skipNStackLevels, string fmt, params object [] args);
            void Emit(int skipNStackLevels, IFormatProvider provider, string fmt, params object[] args);
        }

        /// <summary>Returns an emitter that may be used to accept emit calls but which will never emit anything.</summary>
        public static IMesgEmitter NullEmitter { get { return MesgEmitterImpl.Null; } }

        #endregion

		//-------------------------------------------------------------------
		#region ILogger

        /// <summary>
        /// This interface defines the most basic set of properties and methods that are provided by objects that can be used as log message sources.
        /// </summary>
        /// <remarks>
        /// This interface allows the client to obtain IMesgEmitters at any of the standard log levels with which the client can generate and emit 
        /// log messages.  
        /// </remarks>
        public interface IBasicLogger
        {
            /// <summary>Returns a message emitter for Error messages from this logger</summary>
            IMesgEmitter Error { get; }
            /// <summary>Returns a message emitter for Warning messages from this logger</summary>
            IMesgEmitter Warning { get; }
            /// <summary>Returns a message emitter for Signif messages from this logger</summary>
            IMesgEmitter Signif { get; }
            /// <summary>Returns a message emitter for Info messages from this logger</summary>
            IMesgEmitter Info { get; }
            /// <summary>Returns a message emitter for Debug messages from this logger</summary>
            IMesgEmitter Debug { get; }
            /// <summary>Returns a message emitter for Trace messages from this logger</summary>
            IMesgEmitter Trace { get; }

            /// <summary>Returns a message emitter that will emit messages of the given MesgType and from this logger.</summary>
            IMesgEmitter Emitter(MesgType mesgType);

            /// <summary>Helper method: Gets the current System.Diagnostics.StackFrame of the caller (up skipNFrames levels)</summary>
            /// <returns>Selected StackFrame if stack frame tagging support is currently enabled or null if it is not</returns>
            System.Diagnostics.StackFrame GetStackFrame(int skipNFrames);
        }

		/// <summary>This interface defines the methods that is provided by all entities that can act like log message sources.</summary>
		/// <remarks>
		/// This interface defines how loggers may be used as gatways to the LogMessage allocation and distribution system.  The
		/// underlying ILogMessageDistribution object maintains a pool of LogMessages and a list of LogMessageHandler objects.
		/// Alternately a local ILogger instance may allocated its own copies of LogMessages which will not be released to the
		/// LogMessageDistribution system's pool when it has finished using them.
		/// 
		/// The ILogger is used to create an association of a name, an integer loggerID, a per source instance gate and a cached
		/// copy of the distribution gate for the given name.  It serves the purpose of simplifying access to the allocation and
		/// distribution system.
		/// 
		/// Note: multiple instances of this interface may be used with the same logger name.  Each instance will share the same
		/// id, source and distribution gating.  However, each instance may specify a seperate instance gate value if desired.
		/// The default value of the instance gate is to pass all messages so that by default, the distribution gate is the only
		/// gate that may prevent allocation (and thus generation) of certain types of log messages.
		/// 
		/// MT Note:  Each logger instace's IsTypeEnabled and GetLogMessage and EmitLogMessage methods
		/// may be safely used by multiple threads in a renterant fashion with the following restrictions:
		///		A) the logger instance must not be created, or reconfigured using multiple threads.
		///		B) the IsTypeEnabled method may not produce the correct result if multiple threads attempt
		///			to use it concurrently immediately after there has been a change in the distribution
		///			system gating for this logger.  Attempted uses after the the one that applies the change
		///			will produce consistant and correct results.
		/// </remarks>

        public interface ILogger : IBasicLogger
		{
            /// <summary>Returns the name of this logger.</summary>
            string Name { get; }

            /// <summary>Returns the common LoggerSourceInfo for this logger.</summary>
            LoggerSourceInfo LoggerSourceInfo { get; }

            /// <summary>Returns the Distribution Group Name for this logger</summary>
            string GroupName { get; set; }

            /// <summary>returns true if the given message type is currently enabled.</summary>
            bool IsTypeEnabled(MesgType mesgType);

            /// <summary>returns a new message with type, source, file and line filled in.  Message will be empty.</summary>
            LogMessage GetLogMessage(MesgType mesgType, System.Diagnostics.StackFrame sourceStackFrame);

            /// <summary>returns a new message with type, source, message, file and line filled in.</summary>
            LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame);

            /// <summary>retval returns a new message with type, source, message, file and line filled in.</summary>
            LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame, bool allocatedFromDist);

            /// <summary>Emits and consumes the message (mesgP will be set to null)</summary>
			void EmitLogMessage(ref LogMessage mesg);

            /// <summary>Waits for last message emitted by this logger to have been distributed and processed.</summary>
            bool WaitForDistributionComplete(TimeSpan timeLimit);

            /// <summary>shuts down this source and prevents it from allocating or emitting further log messages.</summary>
            void Shutdown();
		};

		#endregion

		//-------------------------------------------------------------------
		#region IMesgEmitter implementation class

		public class MesgEmitterImpl : IMesgEmitter
		{
            public ILogger Logger { get; set; }
			public MesgType MesgType { get; set; }
            public int SkipNAdditionalStackFrames { get; set; }

			public MesgEmitterImpl() {}

			#region IMesgEmitter Members

			public bool IsEnabled { get { return (Logger != null && Logger.IsTypeEnabled(MesgType)); } }

			System.Diagnostics.StackFrame GetStackFrame(int skipNStackFrames)
			{
				if (Logger == null)
					return null;
				return Logger.GetStackFrame(1 + skipNStackFrames + SkipNAdditionalStackFrames);
			}

			public void Emit(string str) { Emit(1, str); }
			public void Emit(string fmt, object arg0) { Emit(1, fmt, arg0); }
			public void Emit(string fmt, object arg0, object arg1) { Emit(1, fmt, arg0, arg1); }
			public void Emit(string fmt, object arg0, object arg1, object arg2) { Emit(1, fmt, arg0, arg1, arg2); }
			public void Emit(string fmt, params object [] args) { Emit(1, fmt, args); }
            public void Emit(IFormatProvider provider, string fmt, params object[] args) { Emit(1, provider, fmt, args); }

			public void Emit(int skipNStackFrames, string str)
			{
				if (IsEnabled)
				{
					LogMessage lm = Logger.GetLogMessage(MesgType, str, GetStackFrame(1 + skipNStackFrames), true);
					Logger.EmitLogMessage(ref lm);
				}
			}

			public void Emit(int skipNStackFrames, string fmt, object arg0)
			{
				if (IsEnabled)
				{
					LogMessage lm = Logger.GetLogMessage(MesgType, string.Empty, GetStackFrame(1 + skipNStackFrames), true);
					if (lm != null)
						lm.Mesg = Utils.Fcns.CheckedFormat(fmt, arg0);
					Logger.EmitLogMessage(ref lm);
				}
			}

			public void Emit(int skipNStackFrames, string fmt, object arg0, object arg1)
			{
				if (IsEnabled)
				{
					LogMessage lm = Logger.GetLogMessage(MesgType, string.Empty, GetStackFrame(1 + skipNStackFrames), true);
					if (lm != null)
						lm.Mesg = Utils.Fcns.CheckedFormat(fmt, arg0, arg1);
					Logger.EmitLogMessage(ref lm);
				}
			}

			public void Emit(int skipNStackFrames, string fmt, object arg0, object arg1, object arg2)
			{
				if (IsEnabled)
				{
					LogMessage lm = Logger.GetLogMessage(MesgType, string.Empty, GetStackFrame(1 + skipNStackFrames), true);
					if (lm != null)
						lm.Mesg = Utils.Fcns.CheckedFormat(fmt, arg0, arg1, arg2);
					Logger.EmitLogMessage(ref lm);
				}
			}

			public void Emit(int skipNStackFrames, string fmt, params object [] args)
			{
				if (IsEnabled)
				{
					LogMessage lm = Logger.GetLogMessage(MesgType, string.Empty, GetStackFrame(1 + skipNStackFrames), true);
					if (lm != null)
						lm.Mesg = Utils.Fcns.CheckedFormat(fmt, args);
					Logger.EmitLogMessage(ref lm);
				}
			}

            public void Emit(int skipNStackFrames, IFormatProvider provider, string fmt, params object[] args)
            {
                if (IsEnabled)
                {
                    LogMessage lm = Logger.GetLogMessage(MesgType, string.Empty, GetStackFrame(1 + skipNStackFrames), true);
                    if (lm != null)
                        lm.Mesg = Utils.Fcns.CheckedFormat(provider, fmt, args);
                    Logger.EmitLogMessage(ref lm);
                }
            }

            #endregion

			#region NullEmitter

            private static readonly IMesgEmitter nullEmitter = new MesgEmitterImpl() { Logger = null, MesgType = MesgType.None, SkipNAdditionalStackFrames = 0 };

			public static IMesgEmitter Null { get { return nullEmitter; } }

			#endregion
		}

		#endregion

		//-------------------------------------------------------------------
		#region LoggerBase

		/// <summary>
		/// This class is a partial implementation class for most Logger type objects.  
		/// It implements all default functionality but only provides a single construtor with a full set of arguments.
		/// This class is used as the base for the Logger class and the QueuedLogger class.
		/// </summary>

		public abstract class LoggerBase : ILogger
		{
			private IMesgEmitter myTraceEmitter = MesgEmitterImpl.Null;
			protected IMesgEmitter MyTraceEmitter { get { return myTraceEmitter; } }

			public LoggerBase(string name, string groupName, LogGate initialInstanceLogGate, bool traceLoggerCtor)
			{
				dist = GetLogMessageDistribution();
                if (dist == null)
				    Utils.Assert.BreakpointFault(ClassName + ": LogMessageDistribution is null");

				sourceInfo = dist.GetLoggerSourceInfo(name);
                instanceLogGate = initialInstanceLogGate;
                distLoggerConfigObserver = new SequencedLoggerConfigObserver(sourceInfo.LoggerConfigSource);

				if (!string.IsNullOrEmpty(groupName) && groupName != GroupName)
					GroupName = groupName;

				if (traceLoggerCtor)
					myTraceEmitter = new MesgEmitterImpl() { Logger = this, MesgType = MesgType.Trace, SkipNAdditionalStackFrames = 1 } ;	// this emitter records the stack frame 1 above its caller

				MyTraceEmitter.Emit("{0} object has been created", ClassName);
			}

			public LoggerBase(LoggerBase rhs)
			{
				dist = rhs.dist;
				sourceInfo = rhs.sourceInfo;
				instanceLogGate = rhs.instanceLogGate;
				distLoggerConfigObserver = rhs.distLoggerConfigObserver;
				myTraceEmitter = rhs.myTraceEmitter;

				MyTraceEmitter.Emit("{0} object has been copied", ClassName);
			}

			#region ILogger implementation

			public string Name { get { return sourceInfo.Name; } }
			public LoggerSourceInfo LoggerSourceInfo { get { return sourceInfo; } }

			public string GroupName 
			{
				get { return distLoggerConfigObserver.GroupName; }
				set
				{
					dist.SetLoggerDistributionGroupName(sourceInfo.ID, value);
					distLoggerConfigObserver.Update();
				}
			}

			public System.Diagnostics.StackFrame GetStackFrame(int skipNFrames) 
			{
				if (distLoggerConfigObserver.RecordSourceStackFrame)
					return new System.Diagnostics.StackFrame(skipNFrames + 1, true);
				else
					return null;
			}

			public LogGate InstanceLogGate { get { return instanceLogGate; } set { instanceLogGate = value; } }
			public bool IsTypeEnabled(MesgType mesgType)
			{
				if (!instanceLogGate.IsTypeEnabled(mesgType) || loggerHasBeenShutdown)
					return false;

				distLoggerConfigObserver.Update();
				if (!distLoggerConfigObserver.IsTypeEnabled(mesgType))
					return false;

				return true;
			}

			public LogMessage GetLogMessage(MesgType mesgType, System.Diagnostics.StackFrame sourceStackFrame) { return GetLogMessage(mesgType, string.Empty, sourceStackFrame, false); }
			public LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame) { return GetLogMessage(mesgType, mesg, sourceStackFrame, false); }
			public virtual LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame, bool allocateFromDist)	//!< @retval returns a new message with type, source, message, file and line filled in.
			{
				LogMessage lm = null;
				if (!loggerHasBeenShutdown)
				{
                    bool allowAllocateFromDist = false;// disable pool based allocation for all messages (for now)

                    // only allocate messages from the pool if the caller permits and if the group (currently) only has handlers that support reference counted release.
                    if (allocateFromDist && LoggerSourceInfo.LoggerConfigSource.Object.SupportsReferenceCountedRelease && allowAllocateFromDist)
						lm = dist.GetLogMessage();
					else
						lm = new LogMessage();
				}
                if (lm != null)
                {
                    if (!lm.IsUnique)
                        Utils.Assert.BreakpointFault("Allocated Message is no Unique");

                    lm.Setup(sourceInfo, mesgType, mesg, GetStackFrame(1));
                }

				return lm;
			}

            /// <summary>Emits and consumes the message (mesgP will be set to null)</summary>
			public virtual void EmitLogMessage(ref LogMessage mesg)
			{
				if (mesg != null && !loggerHasBeenShutdown)
					dist.DistributeMessage(ref mesg);
			}

            public virtual bool WaitForDistributionComplete(TimeSpan timeLimit)		//!< Waits for last message emitted by this logger to have been distributed and processed.
			{
				return dist.WaitForDistributionComplete(sourceInfo.ID, timeLimit);
			}

			public virtual void Shutdown()										//!< shuts down this source and prevents it from allocating or emitting further log messages.
			{
				MyTraceEmitter.Emit("{0} object has been shutdown", ClassName);

				instanceLogGate = LogGate.None;
				loggerHasBeenShutdown = true;
			}

			#endregion

            #region IBaseMessageLogger interface

            public IMesgEmitter Error { get { return Emitter(MesgType.Error); } }
            public IMesgEmitter Warning { get { return Emitter(MesgType.Warning); } }
            public IMesgEmitter Signif { get { return Emitter(MesgType.Signif); } }
            public IMesgEmitter Info { get { return Emitter(MesgType.Info); } }
            public IMesgEmitter Debug { get { return Emitter(MesgType.Debug); } }
            public IMesgEmitter Trace { get { return Emitter(MesgType.Trace); } }
            public IMesgEmitter Emitter(MesgType mesgType)
            {
                IMesgEmitter emitter = null;

                int mesgTypeIdx = (int)mesgType;
                if (mesgTypeIdx > (int)MesgType.None && mesgTypeIdx < emitters.Length)
                {
                    emitter = emitters[mesgTypeIdx];
                    if (emitter == null)
                        emitter = emitters[mesgTypeIdx] = new MesgEmitterImpl() {Logger = this, MesgType = mesgType, SkipNAdditionalStackFrames = 0};
                }

                return ((emitter != null) ? emitter : MesgEmitterImpl.Null);
            }

            #endregion

            protected abstract string ClassName { get; }

			protected ILogMessageDistribution dist = null;				// the distribution system to which we belong
			protected LoggerSourceInfo sourceInfo = null;				// stores the reference id and name
			protected volatile bool loggerHasBeenShutdown = false;

			protected LogGate instanceLogGate = LogGate.All;			// The logger instance specific gate level that we use.
			protected SequencedLoggerConfigObserver	distLoggerConfigObserver = null;	// handle obtained from sourceInfo

			protected IMesgEmitter [] emitters = new IMesgEmitter [((int) MesgType.Max) + 1];
		}

		#endregion

		//-------------------------------------------------------------------
		#region Logger implementation class

		/// <summary>This class provides the standard basic implementation for use as an ILogger</summary>

		public class Logger : LoggerBase
		{
			public Logger(string name) : this(name, string.Empty) { }
			public Logger(string name, string groupName) : this(name, groupName, LogGate.All) { }
			public Logger(string name, LogGate initialInstanceLogGate) : this(name, string.Empty, initialInstanceLogGate) { }
			public Logger(string name, string groupName, LogGate initialInstanceLogGate) : base(name, groupName, initialInstanceLogGate, true) { }
			public Logger(Logger rhs) : base(rhs) { }

			protected override string ClassName { get { return "Logger"; } }
		}

		#endregion

		//-------------------------------------------------------------------
		#region Trace helper objects

        // NOTE:
        // The following Trace object classes were inspired by the use of the TraceLogger class from logger.h in the log4cplus library developed by Tad E. Smith.
        // In most cases these objects should be used in the context of a using statement such as:
        //
        //	using (IDisposable traceObj = MosaicLib.Logging.EntryExitTrace(logger, "TestTrace")) { [do stuff] }

        /// <summary>
        /// This class is used to provide a Trace on construction/destruction.  It logs a configurable message on construction and explicit disposal.
        /// Generally this class is used as the base class for the EnterExitTrace and TimerTrace classes.  By default messages use MesgType.Trace.
        /// </summary>

        public class CtorDisposeTrace : Utils.DisposableBase
		{
			private const string defaultCtorPrefixStr = "Ctor:";
			private const string defaultDisposePrefixStr = "Dispose:";
			protected const MesgType defaultMesgType = MesgType.Trace;

            public CtorDisposeTrace(IBasicLogger logger, string traceID)
				: this(logger, traceID, defaultMesgType, defaultCtorPrefixStr, 1, defaultDisposePrefixStr, 0) {}

            public CtorDisposeTrace(IBasicLogger logger, string traceID, MesgType mesgType)
				: this(logger, traceID, mesgType, defaultCtorPrefixStr, 1, defaultDisposePrefixStr, 0) {}

            public CtorDisposeTrace(IBasicLogger logger, string traceID, MesgType mesgType, int ctorSkipNStackFrames)
				: this(logger, traceID, mesgType, defaultCtorPrefixStr, ctorSkipNStackFrames + 1, defaultDisposePrefixStr, 0) {}

            public CtorDisposeTrace(IBasicLogger logger, string traceID, MesgType mesgType, string ctorPrefixStr, int ctorSkipNStackFrames, string disposePrefixStr, int disposeSkipNStackFrames)
			{
				if (logger != null)
				{
					mesgEmitter = logger.Emitter(mesgType);
					warningEmitter = logger.Warning;
				}

				string ctorStr = ctorPrefixStr + traceID;
				mesgEmitter.Emit(ctorSkipNStackFrames + 1, ctorStr);

				disposeStr = disposePrefixStr + traceID;
				this.disposeSkipNStackFrames = disposeSkipNStackFrames;
			}

			protected override void Dispose(Utils.DisposableBase.DisposeType disposeType)
			{
				if (disposeStr != null)
				{
					if (disposeType == DisposeType.CalledExplicitly && mesgEmitter != null)
						mesgEmitter.Emit(disposeSkipNStackFrames + 1, disposeStr);
					else if (warningEmitter != null)
						warningEmitter.Emit(disposeSkipNStackFrames + 1, "Unexpected '{0}' disposal of trace object for mesg:'{1}'", disposeType.ToString(), disposeStr);
				}

				mesgEmitter = warningEmitter = null;
			}

			protected string disposeStr = null;
			protected IMesgEmitter mesgEmitter = MesgEmitterImpl.Null;
			protected IMesgEmitter warningEmitter = MesgEmitterImpl.Null;
			protected int disposeSkipNStackFrames = 0;
		}

		//-------------------------------------------------------------------

        /// <summary>
        /// This class is generally used as a method entry/exit Trace.  It is based on the CtorDisposeTrace with modified default log message prefixes.
        /// By default messages use CtorDisposeTrace default message type (MesgType.Trace)
        /// </summary>
        /// <remarks>
        ///	using (var eeTrace = MosaicLib.Logging.EntryExitTrace(logger)) { [do stuff] }
        /// </remarks>

        public class EnterExitTrace
			: CtorDisposeTrace
		{
			private const string entryPrefixStr = "Enter:";
			private const string exitPrefixStr = "Exit:";

			public EnterExitTrace(IBasicLogger logger)
				: base(logger, new System.Diagnostics.StackFrame(1).GetMethod().Name, defaultMesgType, entryPrefixStr, 1, exitPrefixStr, 0) {}

			public EnterExitTrace(IBasicLogger logger, string traceID)
				: base(logger, traceID, defaultMesgType, entryPrefixStr, 1, exitPrefixStr, 0) {}

			public EnterExitTrace(IBasicLogger logger, string traceID, MesgType mesgType)
				: base(logger, traceID, mesgType, entryPrefixStr, 1, exitPrefixStr, 0) {}

			public EnterExitTrace(IBasicLogger logger, string traceID, MesgType mesgType, int ctorSkipNStackFrames)
				: base(logger, traceID, mesgType, entryPrefixStr, ctorSkipNStackFrames + 1, exitPrefixStr, 0) {}
		}

		//-------------------------------------------------------------------

        /// <summary>
        /// This class is generally used as a section start/stop Trace.  It is based on the CtorDisposeTrace with modified default log message prefixes.
        /// By default start and stop messages use MesgType.Debug.
        /// </summary>
        /// <remarks>
        ///	using (var tTract = MosaicLib.Logging.TimerTrace(logger, "MeasurementName")) { [do stuff] }
        /// </remarks>
        public class TimerTrace
			: CtorDisposeTrace
		{
			private const string startPrefixStr = "Start:";
			private const string stopPrefixStr = "Stop:";
            private const MesgType defaultStartStopMesgType = MesgType.Debug;

            public TimerTrace(IBasicLogger logger, string traceID)
				: this(logger, traceID, defaultStartStopMesgType, 1) {}

            public TimerTrace(IBasicLogger logger, string traceID, MesgType mesgType)
				: this(logger, traceID, mesgType, 1) {}

            public TimerTrace(IBasicLogger logger, string traceID, MesgType mesgType, int ctorSkipNStackFrames)
				: base(logger, traceID, mesgType, startPrefixStr, ctorSkipNStackFrames + 1, stopPrefixStr, 0)
			{
				startTime = Time.Qpc.TimeNow;
			}

			protected override void Dispose(Utils.DisposableBase.DisposeType disposeType)
			{
				if (disposeType == DisposeType.CalledExplicitly && mesgEmitter != null)
				{
					double runTime = Time.Qpc.TimeNow - startTime;

					mesgEmitter.Emit(1 + disposeSkipNStackFrames, "{0} [runTime:{1:f6}]", disposeStr, runTime);
					disposeStr = null;		// prevent additional dispose message
				}

				base.Dispose(disposeType);
			}

			protected double startTime;
		}

		#endregion

		//-------------------------------------------------------------------
	}

	//-------------------------------------------------------------------
}

//-------------------------------------------------------------------
