//-------------------------------------------------------------------
/*! @file Logging.cs
 *  @brief This provides the definitions, including interfaces, classes, types and functions that are essential to the MosaicLib logging system.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2006 Mosaic Systems Inc.  (C++ library version)
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
using System.Runtime.Serialization;
using System.Text;

using MosaicLib.Modular.Common;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib
{
	/// <summary>
	/// This static public class contains the definitions that are used to create the MosaicLib logging system.  
	/// This system is designed to provide efficient, source identified, gate controllable, multi-threaded logging 
	/// of messages and/or data from multiple sources into some number of, possibly source specific, message handlers.  
	/// </summary>
	/// <remarks>
	/// Terms:
    /// <list type="bullet">
    /// <item>
    ///		<see cref="Logging.LogMessage"/> - The basic log message object, has a message placed within it and is then
    ///			emitted into the logging system where it may be recorded in one or more destinations (LogMessageHandlers).
    ///			LogMessages carry a sourceInfo (of the Logger from which they were allocated), a mesg type, a message, 
    ///			an INamedValueSet and a timestamp.
    ///			
    ///         Please NOTE: use of stack frame recording for purposes of file and line number output has been deprecated.
    ///         Optimizations done by the JITer can collapse multiple method layers and thus the skipStackFrames based calculation
    ///         of the frame of the original calling code is no longer accurate and can thus report incorrect and missleading file and line numbers.
    /// </item>
    /// <item>
    ///		LoggerName - each Logger is associated with a string that defines its name.  All Loggers
    ///			that share the same value for this string will also share the same distribution
    ///			rules although each one may provide a separate locally defined LoggingConfig value.
    /// </item>
    /// <item>
    ///		<see cref="Logging.Logger"/> - an object from which LogMessages are obtained and into which LogMessages
    ///			are typically emitted.  Each Logger instance represents a single portal into
    ///			the log distribution system.  Multiple Logger instances may be constructed using 
    ///			the same LoggerName, in which case they share the same common distribution rules.  
    ///			Each logger instances maintains a separate instance LogGate level which may be used 
    ///			in conjunction with the corresponding gate levels of the message handlers that are 
    ///			associated with this LoggerName to efficiently control the allocation and generation 
    ///			of log message contents based on the permissibility of the message type at the time 
    ///			it is to be allocated.  Loggers also control whether the messages that they are responsible 
    ///			for allocating will have a valid StackFrame obtained and assigned (for access to file name and line number).
    ///			ILogger represents the interface that all loggers must support in order to be used with this system.
    /// </item>
    /// <item>
    ///		<see cref="Logging.ILoggerConfig"/> - a combination of values the determine which types of messages a logger will actually
    ///			allocate and/or emit as well as all details about what is included in the messages (such as optional recording
    ///			of stack frames).  The portion of the ILoggerConfig that is used to determine which types of messages to 
    ///			allocate is called the LogGate.
    /// </item>
    /// <item>
    ///		<see cref="Logging.LogGate"/> - provides a simple bitmask style class/mechanism that is used to implement an efficient gate test to determine
    ///			if a given mesg type should be allocated and/or emitted.  LogGate values are typically applied in
    ///			a least restrictive to most restrictive manner (as a LogMessage is routed) so that messages that cannot
    ///			be processed (are gated off in all possible places they might go) will never be generated while other
    ///			messages are forwarded to those handlers that might be interested in them.
    /// </item>
    /// <item>
    ///		<see cref="Logging.IMesgEmitter"/> - a shorthand helper class that is provided by loggers to client code and which can be used to generate
    ///			and emit simple formatted messages of a specific type while providing the client with a variety of useful 
    ///			shorthand variations on the means by which the message itself is generated.  Loggers can generally provide IMesgEmitters
    ///			for each mesg type as needed by their client.
    /// </item>
    /// </list>
    /// 
	/// The MosaicLib Logging system is loosely modeled after Apache's Log4j and Log4Net projects.  It generally preserves
	/// the concept that messages are associated with a source (string identifier) and that the gating and routing of the message
	/// to handlers (appenders in log4j terminology) are determined by the exact source id for the message.  This system has been
	/// modified to emphasize the performance of the distribution system rather than its complete flexibility.  In this system
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

		/// <summary>
        /// This enum defines a set of message types and an implicit ranking of their severities.
        /// <para/>message types: None, Fatal, Error, Warning, Signif, Info, Debug, Trace
        /// <para/>other values: Max, All
        /// </summary>
        [DataContract(Namespace=Constants.LoggingNameSpace)]
		public enum MesgType : int
		{
            /// <summary>used as a gate to suppress passing all mesg types.  [0]</summary>
            [EnumMember]
			None = 0,
            /// <summary>used to record occurrence of setup related (ctor) issues that may prevent use of the affected entity.  [1]</summary>
            [EnumMember]
            Fatal = 1,
            /// <summary>used to record occurrence of unexpected failures that might prevent future actions from operating correctly.  [2]</summary>
            [EnumMember]
            Error = 2,
            /// <summary>used to record occurrence of unexpected failures which are not expected to prevent future actions from operating correctly.  [3]</summary>
            [EnumMember]
            Warning = 3,
            /// <summary>used to record occurrence of significant milestones and/or changes during normal operation of the system.  [4]</summary>
            [EnumMember]
            Signif = 4,
            /// <summary>used to record occurrence of relatively insignificant items.  [5]</summary>
            [EnumMember]
            Info = 5,
            /// <summary>used to record occurrence of information that is intended to provide an even more detailed view.  [6]</summary>
            [EnumMember]
            Debug = 6,
            /// <summary>used to record occurrence of very frequent events such as those used to track data transfer, flow of control, construction and destruction, etc...  [7]</summary>
            [EnumMember]
            Trace = 7,			
            /// <summary>
            /// Defines the last MesgType member that represents an actual message type.  Used to size arrays that may be indexed by a MesgType (cast as an Int32)
            /// <para/>Max = Trace [7]
            /// </summary>
            [EnumMember]
            Max = Trace,

            /// <summary>used as a level to permit passing all mesg types.  [Max + 1 = 8]</summary>
            [EnumMember]
            All,
        }

        private static readonly MesgType[] allMesgTypesArray = new MesgType[] { MesgType.Fatal, MesgType.Error, MesgType.Warning, MesgType.Signif, MesgType.Info, MesgType.Debug, MesgType.Trace };

        /// <summary>Returns an enumerable set of all message types (Fatal, Error, Warning, Signif, Info, Debug, Trace).</summary>
        public static IEnumerable<MesgType> AllMesgTypes { get { return allMesgTypesArray; } }

        /// <summary>Returns mesgType.ToString()</summary>
		public static string ConvertToString(this MesgType mesgType) { return mesgType.ToString(); }

        /// <summary>Returns 3 character version of mesgType.ToString()</summary>
        public static string ConvertToFixedWidthString(this MesgType mesgType)
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

        /// <summary>
        /// Convert extension method that can be used to convert a System.Diagnostics.TraceLevel to a MesgType
        /// </summary>
        public static MesgType ConvertToMesgType(this System.Diagnostics.TraceLevel traceLevel, MesgType verboseLevel = MesgType.Debug, MesgType defaultLevel = MesgType.Error)
        {
            switch (traceLevel)
            {
                case System.Diagnostics.TraceLevel.Off: return MesgType.None;
                case System.Diagnostics.TraceLevel.Error: return MesgType.Error;
                case System.Diagnostics.TraceLevel.Warning: return MesgType.Warning;
                case System.Diagnostics.TraceLevel.Info: return MesgType.Info;
                case System.Diagnostics.TraceLevel.Verbose: return verboseLevel;
                default: return defaultLevel;
            }
        }

		#endregion

		//-------------------------------------------------------------------
		#region MesgTypeMask and LogGate

		/// <summary>
		/// Provides means by which MesgType enum codes are used to implement mask values as part of message type gating code
		/// </summary>
		/// <remarks>
		/// The basic functionality of this value object is to implement a container that is used
		/// to test if given types of messages may be emitted or passed at various points in the
		/// log message distribution system.  This includes source filtering (preventing messages
		/// from actually being constructed or emitted into system) and distribution filtering where messages may
		/// go to distinct sub-sets of the full set of log sinks based on the values of the MesgTypeMask's
		/// that are associated with each one.
		/// 
		/// This class also implements updater methods that allow gates to be constructed and/or
		/// modified based on the contents of other gates.  In particular expressions such as "lhs |= rhs;" 
		/// may be used to allow the lhs gate to be made to be at least as permissive as the rhs gate.
		/// 
		/// Currently this implementation is based on a bit mask approach that also supports
		/// ranking based gating when the mesg type is used as a level mask.
		/// </remarks>

        [DataContract(Namespace=MosaicLib.Constants.LoggingNameSpace)]
		public struct MesgTypeMask : IEquatable<MesgTypeMask>
		{
			/// <summary>Enum defines how a MesgType is used.  Bit is as a single bit, Level is as a mask for all types at or above the given level</summary>
			public enum MaskType
			{
                /// <summary>Mask type to use when treating MesgType as a specific type</summary>
				Bit = 0,
                /// <summary>Mask type to use when treating MesgType as a severity level so that the resulting bit mask includes all levels that are at least as severe as the given one.</summary>
				Level			
			}

            /// <summary>Constructor for mask of given type (Bit or Level) for given MesgType.  <paramref name="maskType"/> defaults to Bit</summary>
			public MesgTypeMask(MesgType mesgType, MaskType maskType = MaskType.Bit) 
                : this(ConvertToBitMask(mesgType, maskType)) 
            { }

            /// <summary>Constructor for mask with <paramref name="maskBits"/> specified explicitly</summary>
            public MesgTypeMask(int maskBits) 
            { 
                this.maskBits = maskBits; 
            }

            /// <summary>Constructor for parsing from a string version</summary>
            public MesgTypeMask(string parseFromStr)
            {
                maskBits = parseFromStr.TryParse(fallbackValue: default(MesgTypeMask)).MaskBits;
            }

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

            /// <summary>IEquatable{LogGate} implementation methods.  Returns true if this gate object has the same MaskBits contents as the other gate object has.</summary>
            public bool Equals(MesgTypeMask other) { return maskBits.Equals(other.maskBits); }


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

            /// <summary>Gives caller get/set access to the underlying bit mask.  Helper property for unit testing.</summary>
            public int MaskBits { get { return maskBits; } set { maskBits = value; } }

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
						return unchecked((1 << (shift + 1)) - 1);	// include all the bits at the current level and all levels above it (ie bits at progressively lower bit positions)
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

                    case (1 << ((int)(MesgType.Fatal) - 1)): detail = "Fatal"; break;
                    case (1 << ((int)(MesgType.Error) - 1)): detail = "Error"; break;
                    case (1 << ((int)(MesgType.Warning) - 1)): detail = "Warning"; break;
                    case (1 << ((int)(MesgType.Signif) - 1)): detail = "Signif"; break;
                    case (1 << ((int)(MesgType.Info) - 1)): detail = "Info"; break;
                    case (1 << ((int)(MesgType.Debug) - 1)): detail = "Debug"; break;
                    case (1 << ((int)(MesgType.Trace) - 1)): detail = "Trace"; break;

                    // case ((1 << ((int)(MesgType.Fatal) - 0)) - 1): detail = "Fatal+"; break;     // there is nothing above Fatal so we cannot have a Fatal+
                    case ((1 << ((int)(MesgType.Error) - 0)) - 1): detail = "Error+"; break;
                    case ((1 << ((int)(MesgType.Warning) - 0)) - 1): detail = "Warning+"; break;
                    case ((1 << ((int)(MesgType.Signif) - 0)) - 1): detail = "Signif+"; break;
                    case ((1 << ((int)(MesgType.Info) - 0)) - 1): detail = "Info+"; break;
                    case ((1 << ((int)(MesgType.Debug) - 0)) - 1): detail = "Debug+"; break;
                    case ((1 << ((int)(MesgType.Trace) - 0)) - 1): detail = "Trace+"; break;
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

                    {"Fatal", (1 << ((int)(MesgType.Fatal) - 1))},
                    {"Error", (1 << ((int)(MesgType.Error) - 1))},
                    {"Warning", (1 << ((int)(MesgType.Warning) - 1))},
                    {"Signif", (1 << ((int)(MesgType.Signif) - 1))},
                    {"Info", (1 << ((int)(MesgType.Info) - 1))},
                    {"Debug", (1 << ((int)(MesgType.Debug) - 1))},
                    {"Trace", (1 << ((int)(MesgType.Trace) - 1))},

                    // {"Fatal+", ((1 << ((int)(MesgType.Fatal) - 0)) - 1)},     // there is nothing above Fatal so we cannot have a Fatal+
                    {"Error+", ((1 << ((int)(MesgType.Error) - 0)) - 1)},
                    {"Warning+", ((1 << ((int)(MesgType.Warning) - 0)) - 1)},
                    {"Signif+", ((1 << ((int)(MesgType.Signif) - 0)) - 1)},
                    {"Info+", ((1 << ((int)(MesgType.Info) - 0)) - 1)},
                    {"Debug+", ((1 << ((int)(MesgType.Debug) - 0)) - 1)},
                    {"Trace+", ((1 << ((int)(MesgType.Trace) - 0)) - 1)},
                };

            /// <summary>Provides a method that will attempt to generate a MesgTypeMask from a given string resulting from calling ToString on another MesgTypeMask.  Returns true if the original could be reconstructed or false if the string was not recognized and was not decidable.</summary>
            public static bool TryParse(string str, out MesgTypeMask mtm)
            {
                return str.TryParse(out mtm, fallbackValue: default(MesgTypeMask));
            }
        }

		/// <summary>
		/// This struct is a container for a MesgTypeMask which is variation on the MesgTypeMask with the simple constructor set to use the 
		/// given MesgType as a gate level rather than a standard type mask.
		/// </summary>

        [DataContract(Namespace = MosaicLib.Constants.LoggingNameSpace)]
        public struct LogGate : IEquatable<LogGate>
		{
            /// <summary>Standard constructor to build a level type mask (as opposed to a bit type mask)</summary>
			public LogGate(MesgType mesgType, MesgTypeMask.MaskType maskType = Logging.MesgTypeMask.MaskType.Level) 
            { 
                mask = new MesgTypeMask(mesgType, maskType); 
            }

            /// <summary>Copy constructor</summary>
            public LogGate(LogGate other) 
                : this(other.mask) 
            { }

            /// <summary>Copy constructor from a MesgTypeMask</summary>
            public LogGate(MesgTypeMask mtm) 
            { 
                mask = mtm; 
            }

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

            /// <summary>IEquatable{LogGate} implementation methods.  Returns true if this gate object has the same MaskBits contents as the other gate object has.</summary>
            public bool Equals(LogGate other) { return mask.Equals(other.mask); }

            /// <summary>Returns true if the mask includes the given mesg type.</summary>
            public bool IsTypeEnabled(MesgType testType) { return mask.IsTypeEnabled(testType, MesgTypeMask.MaskType.Bit); }

            /// <summary>private value object that holds the contained MesgTypeMask.</summary>
			private MesgTypeMask mask;
            /// <summary>get/set property that gives external access to the underlying MesgTypeMask that this object contains and uses.</summary>
            public MesgTypeMask MesgTypeMask { get { return mask; } set { mask = value; } }

            /// <summary>Gives caller get/set access to the underlying MesgTypeMask bit mask.</summary>
            public int MaskBits { get { return mask.MaskBits; } set { mask.MaskBits = value; } }

            /// <summary>Provide added debugging support.</summary>
            public override string ToString()
            {
                return ToString(allowTerseVersion: false);
            }

            /// <summary>Provide added debugging support.  Then allowTerseVersion is true, this method can return strings like "Debug", etc. in place of the longer versions.</summary>
            public string ToString(bool allowTerseVersion)
            {
                if (allowTerseVersion)
                {
                    if (this == LogGate.All)
                        return "All";
                    if (this == LogGate.Error)
                        return "Error";
                    if (this == LogGate.Warning)
                        return "Warning";
                    if (this == LogGate.Signif)
                        return "Signif";
                    if (this == LogGate.Info)
                        return "Info";
                    if (this == LogGate.Debug)
                        return "Debug";
                    if (this == LogGate.Trace)
                        return "Trace";
                    if (this == LogGate.None)
                        return "None";
                }

                return Utils.Fcns.CheckedFormat("LogGate:{0}", mask);
            }

            /// <summary>Dictionary of names to corresponding common LogGate values.</summary>
            public static readonly Dictionary<string, LogGate> LogGateNameMap
                = new Dictionary<string, LogGate>()
                {
                    {"None", LogGate.None},
                    {"All", LogGate.All},
                    {"Error", LogGate.Error},
                    {"Warning", LogGate.Warning},
                    {"Signif", LogGate.Signif},
                    {"Info", LogGate.Info},
                    {"Debug", LogGate.Debug},
                    {"Trace", LogGate.Trace},
                };

            /// <summary>
            /// Provides a method that will attempt to generate a LogGate from a given string resulting from calling ToString on another LogGate.  
            /// Returns true if the original could be reconstructed or false if the string was not recognized or it could not otherwise be decoded.
            /// </summary>
            public bool TryParse(string str)
            {
                LogGate logGate = default(LogGate);
                bool success = str.TryParse(out logGate, fallbackValue: this);

                if (success)
                    mask = logGate.mask;

                return success;
            }

            /// <summary>Static LogGate that has no message types enabled. [0x0000]</summary>
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

            #region cast operators

            /// <summary>
            /// Cast operator for explicit conversion from a ValueContainer to a LogGate.  Supports <paramref name="vc"/> containing an integer or a supported (and parsable) string representation.
            /// </summary>
            /// <exception cref="MosaicLib.Modular.Common.ValueContainerGetValueException">may be thrown if either of the underlying GetValue calls generate an conversion exception.</exception>
            /// <exception cref="System.InvalidCastException">thrown if the given string cannot be parsed as a LogGate using LogGate.TryParse.</exception>
            public static explicit operator LogGate(ValueContainer vc)
            {
                if (vc.cvt.IsInteger(includeSigned: true, includeUnsigned: true))
                    return (LogGate)vc.GetValue<int>(rethrow: true);
                else
                    return (LogGate)vc.GetValue<string>(rethrow: true);
            }

            /// <summary>
            /// Cast operator for explicit conversion to ValueContainer
            /// </summary>
            public static explicit operator ValueContainer(LogGate logGate)
            {
                return new ValueContainer((string) logGate);
            }

            /// <summary>
            /// Cast operator for explicit conversion from a string to a LogGate - uses LogGate.TryParse.
            /// </summary>
            /// <exception cref="System.InvalidCastException">thrown if the given string cannot be parsed as a LogGate using LogGate.TryParse.</exception>
            public static explicit operator LogGate(string str)
            {
                LogGate logGate = default(LogGate);
                if (logGate.TryParse(str))
                    return logGate;

                throw new System.InvalidCastException("No valid cast to LogGate found for value {0}".CheckedFormat(new ValueContainer(str)));
            }

            /// <summary>
            /// Cast operator for explicit conversion to string
            /// </summary>
            public static explicit operator string(LogGate logGate)
            {
                return logGate.ToString(allowTerseVersion: true);
            }

            /// <summary>
            /// Cast operator for explicit conversion from int <paramref name="maskBits"/> to a LogGate
            /// </summary>
            public static explicit operator LogGate(int maskBits)
            {
                return new LogGate() { MaskBits = maskBits };
            }

            /// <summary>
            /// Cast operator for explicit conversion to int
            /// </summary>
            public static explicit operator int(LogGate logGate)
            {
                return logGate.MaskBits;
            }

            #endregion
        }

		#endregion

		//-------------------------------------------------------------------
		#region LoggerID

        /// <summary>Special value used as a LoggerID to indicate that no logger is identified (-1)</summary>
		public const int LoggerID_Invalid = -1;
        /// <summary>Special value used as a LoggerID to cover specific internal handling cases. (-2)</summary>
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

            /// <summary>Gives access to a LogGate object which requests all corresponding loggers to increase their local gate level using this value..</summary>
            LogGate LogGateIncrease { get; }

            /// <summary>Query method to use the LogGate to determine if a given MesgType is currently enabled.</summary>
            bool IsTypeEnabled(MesgType testType);

            /// <summary>Recording of source stack frames is no longer supported.</summary>
            [Obsolete("Support for recording File and Line has been removed.  Use of this property is no longer supported. (2017-07-21)")]
            bool RecordSourceStackFrame { get; }

            /// <summary>Pooling of LogMessages has been removed.  This property is no longer supported.</summary>
            [Obsolete("Pooling of LogMessages has been removed.  This property is no longer supported. (2016-12-22)")]
            bool SupportsReferenceCountedRelease { get; }
		}

        /// <summary>Implement standard storage object for supporting the ILoggerConfig interface.  These objects are used to define and control logging for specific groups of loggers and log message handlers.</summary>
		public struct LoggerConfig : ILoggerConfig, IEquatable<ILoggerConfig>
		{
            // all LoggerConfig objects are constructed using default constructor.  Client may set properties using post CTOR {} notation.

            /// <summary>Copy constructor</summary>
            public LoggerConfig(LoggerConfig rhs) 
                : this()
            {
                GroupName = rhs.GroupName;
                LogGate = rhs.LogGate;
                LogGateIncrease = rhs.LogGateIncrease;
            }

            /// <summary>Name of corresponding Logger Group.  May be empty if no group is associated.  For LogMessageHandlers, this name will be derived from the Handler's name</summary>
            public String GroupName { get; set; }

            /// <summary>Gives access to the LogGate object which defines the set of messages that are permitted to be generated by, or passed through, a given interface.</summary>
            public LogGate LogGate { get; set; }

            /// <summary>Gives access to a LogGate object which requests all corresponding loggers to increase their local gate level using this value..</summary>
            public LogGate LogGateIncrease { get; set; }

            /// <summary>Query method to use the LogGate to determine if a given MesgType is currently enabled.</summary>
            public bool IsTypeEnabled(MesgType testType) { return LogGate.IsTypeEnabled(testType); }

            /// <summary>Recording of source stack frames is no longer supported.</summary>
            [Obsolete("Support for recording File and Line has been removed.  Use of this property is no longer supported. (2017-07-21)")]
            public bool RecordSourceStackFrame { get { return false; } set { } } 

            /// <summary>Pooling of LogMessages has been removed.  This property is no longer supported.</summary>
            [Obsolete("Pooling of LogMessages has been removed.  This property is no longer supported. (2016-12-22)")]
            public bool SupportsReferenceCountedRelease { get { return false; } set { } }

            /// <summary>Returns true if this object has the same contents as the given <paramref name="other"/> object.</summary>
            public bool Equals(ILoggerConfig other)
            {
                return (other != null &&
                        GroupName == other.GroupName &&
                        LogGate == other.LogGate &&
                        LogGateIncrease == other.LogGateIncrease
                        );
            }

            /// <summary>Provide added debugging support using override for LoggerConfig.ToString() method.</summary>
            public override string ToString()
            {
                string logGateIncreaseStr = ((LogGateIncrease == LogGate.None) ? "" : " incr:{0}".CheckedFormat(LogGateIncrease.ToString(true)));
 
                return Utils.Fcns.CheckedFormat("Grp:'{0}' {1}{2}", GroupName, LogGate.ToString(true), logGateIncreaseStr);
            }

            /// <summary>
            /// Or combination operator: returns new LoggerConfig using lhs's name and the OR combination of the remaining properties from both the lhs and rhs
            /// </summary>
			public static LoggerConfig operator |(LoggerConfig lhs, LoggerConfig rhs)
            {
                return new LoggerConfig()
                {
                    GroupName = lhs.GroupName,
                    LogGate = (lhs.LogGate | rhs.LogGate),
                    LogGateIncrease = (lhs.LogGateIncrease | rhs.LogGateIncrease),
                }; 
            }

            /// <summary>
            /// And combination operator: returns new LoggerConfig using lhs's name and the AND combination of the remaining properties from both the lhs and rhs
            /// </summary>
            public static LoggerConfig operator &(LoggerConfig lhs, LoggerConfig rhs)
            {
                return new LoggerConfig()
                {
                    GroupName = lhs.GroupName,
                    LogGate = (lhs.LogGate & rhs.LogGate),
                    LogGateIncrease = lhs.LogGateIncrease,      // just keep the existing increase, do not reduce it using the value in the rhs.
                };
            }

            /// <summary>
            /// And combination operator: returns new LoggerConfig using lhs settings and its LogGate ANDed with the rhs LogGate.
            /// </summary>
            public static LoggerConfig operator &(LoggerConfig lhs, LogGate rhs)
            {
                return new LoggerConfig()
                {
                    GroupName = lhs.GroupName,
                    LogGate = (lhs.LogGate & rhs),
                    LogGateIncrease = lhs.LogGateIncrease,
                };
            }

            /// <summary>Returns LoggerConfig with LogGate = LogGate.None</summary>
			public static LoggerConfig None { get { return none; } }

            /// <summary>Returns LoggerConfig with LogGate = LogGate.All</summary>
            public static LoggerConfig All { get { return all; } }

            /// <summary>Returns LoggerConfig with LogGate = LogGate.All</summary>
            [Obsolete("Support for recording File and Line has been removed.  Please use the All property in its place. (2017-07-21)")]
            public static LoggerConfig AllNoFL { get { return all; } }

            /// <summary>Returns LoggerConfig with LogGate = LogGate.All</summary>
            [Obsolete("Support for recording File and Line has been removed.  Please use the All property in its place. (2017-07-21)")]
            public static LoggerConfig AllWithFL { get { return all; } }

            private static LoggerConfig none = new LoggerConfig() { LogGate = LogGate.None };
            private static LoggerConfig all = new LoggerConfig() { LogGate = LogGate.All };
        }

        /// <summary> class used by distribution system to generate and distribute the sequenced LoggerConfig values that are observed using the SequencedLoggerConfigObserver objects. </summary>
		public class SequencedLoggerConfigSource
		{
            /// <summary>Default constructor</summary>
			public SequencedLoggerConfigSource() 
            { }

            /// <summary>pseudo Copy constructor</summary>
            public SequencedLoggerConfigSource(LoggerConfig initialLoggerConfig) 
            { 
                LoggerConfig = initialLoggerConfig; 
            }

			private LoggerConfig localValueCopy;
			private Utils.GuardedSequencedValueObject<LoggerConfig> guardedValue = new MosaicLib.Utils.GuardedSequencedValueObject<LoggerConfig>();

            /// <summary>
            /// get/set property which implements the essential utility of this class.  
            /// getter returns last, locally assigned copy.  setter determines if the guarded copy needs to be updated and does so when required (typically due to change)</summary>
            public LoggerConfig LoggerConfig
			{
				get 
                { 
                    return localValueCopy; 
                }
				set 
                { 
                    if (!localValueCopy.Equals(value) || !guardedValue.HasBeenSet) 
                    { 
                        guardedValue.Object = localValueCopy = value; 
                    } 
                }
			}

            /// <summary>Gives publicly accessible version of private guardedValue object as an ISequenceObjectSource{LoggerConfig, Int32}</summary>
            public Utils.ISequencedObjectSource<LoggerConfig, Int32> LoggerConfigSource { get { return guardedValue; } }
		}

		/// <summary> class used by Logger implementations to manage their cached copy of the distribution LoggerConfig level for this named source.  This object may be used as an ILoggerConfig.</summary>
		public class SequencedLoggerConfigObserver : Utils.SequencedValueObjectSourceObserver<LoggerConfig, Int32>, ILoggerConfig
		{
            /// <summary>Default constructor, requires an ISequencedObjectSource{LoggerConfig, Int32}, from which to observe new LoggerConfig values being pushed.</summary>
			public SequencedLoggerConfigObserver(Utils.ISequencedObjectSource<LoggerConfig, Int32> lcSource) 
                : base(lcSource) 
            { }

            /// <summary>Copy constructor, copies the required ISequencedObjectSource{LoggerConfig, Int32} from the given rhs.</summary>
            public SequencedLoggerConfigObserver(SequencedLoggerConfigObserver rhs) 
                : base(rhs) 
            { }

            /// <summary>Returns cached copy from LoggerConfig Source</summary>
            public ILoggerConfig LoggerConfig { get { return Object; } }

            /// <summary>Returns GroupName from cached copy from LoggerConfig Source</summary>
			public String GroupName { get { return LoggerConfig.GroupName; } }

            /// <summary>Returns LogGate from cached copy from LoggerConfig Source</summary>
            public LogGate LogGate { get { return LoggerConfig.LogGate; } }

            /// <summary>Gives access to a LogGate object which requests all corresponding loggers to increase their local gate level using this value..</summary>
            public LogGate LogGateIncrease { get { return LoggerConfig.LogGateIncrease; } }

            /// <summary>Calls IsTypeEnabled on cached copy from LoggerConfig Source</summary>
            public bool IsTypeEnabled(MesgType testType) { return LoggerConfig.IsTypeEnabled(testType); }

            /// <summary>Returns RecordSourceStackFrame from cached copy from LoggerConfig Source</summary>
            [Obsolete("Support for recording File and Line has been removed.  Use of this property is no longer supported. (2017-07-21)")]
            public bool RecordSourceStackFrame { get { return false; } }

            /// <summary>Pooling of LogMessages has been removed.  This property is no longer supported.</summary>
            [Obsolete("Pooling of LogMessages has been removed.  This property is no longer supported. (2016-12-22)")]
            public bool SupportsReferenceCountedRelease { get { return false; } }
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
			private static readonly Utils.ISequencedObjectSource<LoggerConfig, Int32> allLoggerConfigSource = new SequencedLoggerConfigSource(LoggerConfig.All).LoggerConfigSource;

            /// <summary>Constructor:  Requires loggerID and loggerName</summary>
			public LoggerSourceInfo(int loggerID, string loggerName) : this(loggerID, loggerName, allLoggerConfigSource) { }

            /// <summary>Constructor:  Accepts loggerID, loggerName, and an ISequencedObjectSource{LoggerConfig, Int32}</summary>
            public LoggerSourceInfo(int loggerID, string loggerName, Utils.ISequencedObjectSource<LoggerConfig, Int32> loggerConfigSource) 
			{
				ID = loggerID;
				Name = loggerName;
				LoggerConfigSource = loggerConfigSource;
			}

            /// <summary>Default Constructor</summary>
			public LoggerSourceInfo()
			{
				ID = LoggerID_Invalid;
				Name = "_EmptyLSI_";
                LoggerConfigSource = allLoggerConfigSource;
			}

            /// <summary>Gives the LoggerID for the Logger with this Name</summary>
            public int ID { get; private set; }

            /// <summary>Gives the Logger Name for this Logging Source.  Usually this is a component name or other object ID and/or path name.</summary>
            public string Name { get; private set; }

            /// <summary>Gives the ISequencedObjectSource{LoggerConfig, Int32} that will be used by Logger instances to track logging configuration changes made in the distribution system.</summary>
            public Utils.ISequencedObjectSource<LoggerConfig, Int32> LoggerConfigSource { get; private set; }

            /// <summary>Returns true if the ID is valid, the Name is neither null nor empty and the LoggerConfigSource is not null.</summary>
            public bool IsValid { get { return IsLoggerIDValid(ID) && !string.IsNullOrEmpty(Name) && LoggerConfigSource != null; } }

            /// <summary>Returns a singleton empty object</summary>
            public static LoggerSourceInfo Empty { get { return empty; } }
            private static readonly LoggerSourceInfo empty = new LoggerSourceInfo();

            /// <summary>debugging helper method</summary>
            public override string ToString()
            {
                if (IsValid)
                    return "LoggerSourceInfo: id:{0} name:'{1}' config:'{2}'".CheckedFormat(ID, Name, LoggerConfigSource.Object);
                else
                    return "LoggerSourceInfo: id:{0} name:'{1}' [NotValid]".CheckedFormat(ID, Name);
            }
        }

		#endregion

		//-------------------------------------------------------------------
		#region LogMessage class and related definitions

        /// <summary>The Message Seqeunce number used when there is no message. (0)</summary>
		public const int NullMessageSeqNum = 0;

        /// <summary>This interface defines the publicly accessible properties and methods that a LogMessage provides for accessing its stored contents.</summary>
        public interface ILogMessage : IEquatable<ILogMessage>
        {
            /// <summary>Returns the name of the sourcing logger object or a fixed string if there is no such object.</summary>
            string LoggerName { get; }

            /// <summary>Returns the MesgType of the contained message</summary>
            MesgType MesgType { get; }

            /// <summary>Returns the string body of the message or the empty string if none was given</summary>
            string Mesg { get; }

            /// <summary>Returns an escaped version the string body of the message or the empty string if none was given</summary>
            string MesgEscaped { get; }

            /// <summary>Returns a read-only instance of the, possibly empty, named value set that was given to this log message</summary>
            INamedValueSet NamedValueSet { get; }

            /// <summary>Returns a, possibly null, byte array of binary data that is associated with this message.</summary>
            byte[] Data { get; }

            /// <summary>True if the message has been given to the distribution system.</summary>
            bool Emitted { get; }

            /// <summary>Returns the QpcTimeStamp at which this message was emitted or zero if it has not been emitted</summary>
            QpcTimeStamp EmittedQpcTime { get; }

            /// <summary>Returns the sequence number that the distribution system assigned to this message when it was emitted, or zero if it has not been emitted</summary>
            int SeqNum { get; }

            /// <summary>Returns the ThreadID of the thread that setup this message</summary>
            int ThreadID { get; }

            /// <summary>Returns the Win32 ThreadID (from kernel32.GetCurrentThreadID) of the thread that setup this message</summary>
            int Win32ThreadID { get; }

            /// <summary>Returns the Name of the Thread that initially setup this message.</summary>
            string ThreadName { get; }

            /// <summary>Returns the DataTime taken at the time the message was emitted or the empty DateTime if it has not bee emitted.</summary>
            DateTime EmittedDateTime { get; }

            /// <summary>Method used to get the EmittedDataTime in one of the standard supported formats.</summary>
            string GetFormattedDateTime(Utils.Dates.DateTimeFormat dtFormat = Utils.Dates.DateTimeFormat.LogDefault);
        }

		/// <summary>This class implements the public sharable container that is used for all information that is to be logged using this logging system.</summary>
		/// <remarks>
		/// Messages are generated from a specific source and also include:
		///		a MesgType, a message string, an optional INamedValueSet, and an optional data byte array
		/// </remarks>
        [DataContract(Namespace=Constants.LoggingNameSpace), Serializable]
        public class LogMessage : ILogMessage
		{
            /// <summary>Default constructor.</summary>
			public LogMessage() 
            {
                ThreadID = -1;
                Win32ThreadID = -1;
            }

            /// <summary>Copy constructor - creates a duplicate of the given <paramref name="other"/></summary>
            public LogMessage(LogMessage other) 
            {
                _loggerSourceInfo = other._loggerSourceInfo;
                _loggerID = other._loggerID;
                _loggerName = other._loggerName;
                MesgType = other.MesgType;
                _mesg = other.Mesg;
                _mesgEscaped = other._mesgEscaped;
                _nvs = other._nvs;
                _data = ((other.Data != null) ? (other.Data.Clone() as byte []) : null);
                Emitted = other.Emitted;
                _emittedQpcTime = other._emittedQpcTime;
                EmittedDateTime = other.EmittedDateTime;
                SeqNum = other.SeqNum;
                ThreadID = other.ThreadID;
                Win32ThreadID = other.Win32ThreadID;
                _threadName = other._threadName;
            }

            /// <summary>Resets contents to default state</summary>
            [Obsolete("This method will be deprecated as messages are no longer reused.  Please remove the use of this method [2017-12-19]")]
            public LogMessage Reset()
            {
                MesgType = MesgType.None; 
                _mesg = string.Empty;
                _mesgEscaped = null;
                _nvs = null;
                ThreadID = -1;
                Win32ThreadID = -1;
                ThreadName = null;
                Emitted = false;
                _emittedQpcTime = QpcTimeStamp.Zero;
                EmittedDateTime = default(DateTime);
                SeqNum = NullMessageSeqNum;

                return this;
            }

            /// <summary>
            /// Asserts that the message is in the not-emitted state.  
            /// Helps enforces that message contents cannot be changed after they have been emitted.
            /// Primarily used for enforcing that pool messages are recycled correctly.
            /// </summary>
            public LogMessage AssertNotEmitted(string caller) 
            { 
                if (Emitted) 
                    Utils.Asserts.TakeBreakpointAfterFault("AssertNotEmitted failed for: {0}".CheckedFormat(caller));

                return this;
            }

            /// <summary>Sets up contents from a given source with an explicitly given Mesg</summary>
            [Obsolete("The use of this method has been replaced by the version that does not provide a sourceStackFrame.  Recording of source stack frames has been deperated (2017-07-21)")]
            public LogMessage Setup(LoggerSourceInfo loggerSourceInfo, MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame)
            {
                return Setup(loggerSourceInfo, mesgType, mesg);
            }

            /// <summary>Sets up contents from a given source with an explicitly given mesgType and an optional mesg, nvs, and data</summary>
            public LogMessage Setup(LoggerSourceInfo loggerSourceInfo = null, MesgType mesgType = Logging.MesgType.None, string mesg = null, INamedValueSet nvs = null, byte [] data = null)
            {
                LoggerSourceInfo = loggerSourceInfo;
                MesgType = mesgType;
                Mesg = mesg;
                _nvs = nvs.ConvertToReadOnly(mapNullToEmpty: false).MapEmptyToNull();
                _data = data;
                SetThreadID();

                return this;
            }

            /// <summary>
            /// Debug helper method (this method is not intended for use in logging)
            /// </summary>
            public override string ToString()
            {
                return "{0} {1} {2}:{3}".CheckedFormat(Emitted ? "Emitted" : "NotEmitted", MesgType, LoggerName, MesgEscaped);
            }

            /// <summary>
            /// Returns the LoggerSourceInfo of the logger that generated this message or null if message is in default state or no such source was given.
            /// Messages all share a reference to the same logger source id and string if they all come from the same source id.
            /// </summary>
            public LoggerSourceInfo LoggerSourceInfo { get { return _loggerSourceInfo; } private set { _loggerSourceInfo = value; if (value != null && value.IsValid) { _loggerName = value.Name; _loggerID = value.ID; } } }
            [NonSerialized]
            private LoggerSourceInfo _loggerSourceInfo;

            /// <summary>Returns true if the current SourceInfo is non null.</summary>
            public bool IsLoggerSourceInfoValid { get { return (LoggerSourceInfo != null && LoggerSourceInfo.IsValid); } }

            /// <summary>Returns the LoggerID in the Log Distribution System for the LoggerSourceInfo or LoggerID_Invalid if there is no valid LoggerSourceInfo</summary>
            public int LoggerID { get { return _loggerID ?? LoggerID_Invalid; } }

            [DataMember(Order = 100, Name = "LoggerID", IsRequired = false, EmitDefaultValue = false)]
            private int? _loggerID = null;

            /// <summary>Returns the LoggerName for the logger reference in the given LoggerSourceInfo or a fixed string ("NULL_LOGGER") if there is none</summary>
            public string LoggerName { get { return (_loggerName ?? GetLoggerName("_NullLoggerName_")); } }

            /// <summary>helper method used to Get LoggerName where caller specifies the string to return when the message has no defined LoggerSourceInfo</summary>
            public string GetLoggerName(string nullLoggerName) { return (IsLoggerSourceInfoValid ? LoggerSourceInfo.Name : nullLoggerName); }

            [DataMember(Order = 200, Name = "LoggerName", IsRequired = false, EmitDefaultValue = false)]
            private string _loggerName = null;

			// information about the actual event

            /// <summary>Returns the MesgType that the message was setup for or MesgType.None if the message has not been setup.</summary>
            [DataMember(Order = 300, Name="MesgType", IsRequired = false, EmitDefaultValue = false)]
            public MesgType MesgType { get; private set; }

            /// <summary>Returns the current Message Body.  Returns empty string if the message has not been given a body.  Setter allows the contained mesg to be updated, provided that the message has not been emitted.</summary>
            public string Mesg 
			{ 
				get { return (_mesg ?? string.Empty); } 
				set { AssertNotEmitted("Mesg property Set"); _mesg = value; }
			}

            [DataMember(Order = 400, Name="Mesg", IsRequired=false, EmitDefaultValue=false)]
            private string _mesg = null;

            /// <summary>Returns an escaped version the string body of the message or the empty string if none was given</summary>
            public string MesgEscaped
            {
                get
                {
                    if (_mesgEscaped == null)
                        _mesgEscaped = _mesg.GenerateLoggingVersion();

                    return _mesgEscaped;
                }
            }
            [NonSerialized]
            private string _mesgEscaped = null;

			// the message data - an optional binary block of bytes

            /// <summary>Returns the current Message Data body as a block of bytes or null if there are none.  Setter allows the contained data body to be updated provided that the message has not been emitted. </summary>
            public byte[] Data
			{
				get { return _data; }
                set 
                { 
                    AssertNotEmitted("Data property Set"); 
                    _data = value; 
                }
			}
            [DataMember(Order = 500, Name = "Data", IsRequired = false, EmitDefaultValue = false)]
            private byte[] _data = null;

            // the message can contain an INamedValueSet

            /// <summary>
            /// Getter returns a read-only instance of the, possibly empty, named value set that was given to this log message.
            /// Setter converts the given value from to be readonly (using the ConvertToReadOnly extension methods).  If the given value is null or is already readonly then it will be used by the LogMessage without change or cloning.
            /// </summary>
            public INamedValueSet NamedValueSet 
            { 
                get { return _nvs ?? Modular.Common.NamedValueSet.Empty; } 
                set 
                { 
                    AssertNotEmitted("NamedValueSet property Set"); 
                    _nvs = value.ConvertToReadOnly(mapNullToEmpty: false).MapEmptyToNull(); 
                } 
            }
            /// <summary>Returns raw _nvs field contents.  Used for logging to clarify cases where no NVS has been provided as seperate from case where an empty one has.</summary>
            internal INamedValueSet Raw_nvs { get { return _nvs; } }

            [DataMember(Order = 600, Name = "NVS", IsRequired = false, EmitDefaultValue = false)]
            private NamedValueSet _nvs = null;

            /// <summary>Returns the System.Diagnostics.StackFrame from which the message was created/emitted.  May be null if SourceStackFrames are not being acquired.</summary>
            [Obsolete("Support for recording of stack frames for logging has been removed.  This property is no longer supported (2017-07-21)")]
            public System.Diagnostics.StackFrame SourceStackFrame { get { return null; } }

			// information about wether the message has been emitted and if so when it was emitted (qpc and local)

            /// <summary>True if the message has been given to the distribution system.</summary>
            public bool Emitted { get { return _emitted; } private set { _emitted = value; _emittedDC = (value) ? (bool?)null : false; } }
            [NonSerialized]
            private bool _emitted;

            [DataMember(Order = 700, Name = "Emitted", IsRequired = false, EmitDefaultValue = false)]
            private bool? _emittedDC = false;

            /// <summary>Returns the QpcTimeStamp at which the message was first emitted. - non transferable between process spaces.</summary>
            public QpcTimeStamp EmittedQpcTime { get { return _emittedQpcTime; } private set { _emittedQpcTime = value; } }
            [NonSerialized]
            private QpcTimeStamp _emittedQpcTime;

            [DataMember(Order = 700, Name = "EmittedAge", IsRequired = false, EmitDefaultValue = false)]
            private double _emittedAge { get { return EmittedQpcTime.IsZero ? 0.0 : EmittedQpcTime.Age.TotalSeconds; } set { EmittedQpcTime = ((value == 0.0) ? QpcTimeStamp.Zero : QpcTimeStamp.Now + value.FromSeconds()); } }

            /// <summary>Returns the DataTime taken at the time the message was emitted or the empty DateTime if it has not bee emitted.</summary>
            public DateTime EmittedDateTime { get; private set; }

            [DataMember(Order = 800, Name = "EmittedDateTime", IsRequired = false, EmitDefaultValue = false)]
            private string _emittedDateTime { get { return EmittedDateTime.ToString("o"); } set { EmittedDateTime = DateTime.ParseExact(value, "o", null); } }

            /// <summary>Resets the message to the non-emitted state. - used during internal Logging infrastructure message recycling.</summary>
            [Obsolete("This method will be deprecated as messages are no longer reused.  Please remove the use of this method [2017-12-19]")]
            LogMessage ResetEmitted()
			{
				Emitted = false;
				EmittedQpcTime = QpcTimeStamp.Zero;
                EmittedDateTime = DateTime.MinValue;

                return this;
			}

            /// <summary>Marks the message as having been emitted.  This essentially makes the message read only.</summary>
            public LogMessage NoteEmitted() 
			{ 
				AssertNotEmitted("NoteEmitted call");

                if (LoggerSourceInfo == null)
                    LoggerSourceInfo = LoggerSourceInfo.Empty;

                Emitted = true;
				EmittedQpcTime = QpcTimeStamp.Now;
				EmittedDateTime = DateTime.Now;

                return this;
			}

            /// <summary>Returns the message Sequence number as assigned when the message was emitted.  May be used to determine when the message has been delivered from the distribution system.</summary>
            [DataMember(Order = 900, Name = "SeqNum", IsRequired = false, EmitDefaultValue = false)]
            public int SeqNum { get; internal set; }

            /// <summary>Returns the ThreadID that initially setup this message.</summary>
            [DataMember(Order = 1000, Name = "ThreadID", IsRequired = false, EmitDefaultValue = false)]
            public int ThreadID { get; private set; }

            /// <summary>Returns the Win32 ThreadID (from kernel32.GetCurrentThreadID) of the thread that setup this message</summary>
            [DataMember(Order = 1100, Name = "Win32ThreadID", IsRequired = false, EmitDefaultValue = false)]
            public int Win32ThreadID { get; private set; }

            /// <summary>Returns the Name of the Thread that initially setup this message.</summary>
            public string ThreadName { get { return _threadName ?? String.Empty; } private set { _threadName = value; } }
            [DataMember(Order = 1200, Name = "ThreadName", IsRequired = false, EmitDefaultValue = false)]
            private string _threadName = null;

            /// <summary>Method used to set the contained ThreadID and ThreadName during setup</summary>
            private LogMessage SetThreadID() 
            {
                System.Threading.Thread currentThread = System.Threading.Thread.CurrentThread;
                ThreadID = currentThread.ManagedThreadId;
                _threadName = currentThread.Name;

                Win32ThreadID = Utils.Win32.GetCurrentThreadId();

                return this;
            }

            /// <summary>Method used to get the EmittedDataTime formatted in one of the standard supported formats.</summary>
            public string GetFormattedDateTime(Utils.Dates.DateTimeFormat dtFormat = Utils.Dates.DateTimeFormat.LogDefault) 
            { 
                return Utils.Dates.CvtToString(EmittedDateTime, dtFormat); 
            }

            [OnDeserialized]
            private void HandleOnDeserialized(StreamingContext context)
            {
                _emitted = _emittedDC ?? true;
                if (_nvs != null && !_nvs.IsReadOnly)
                    _nvs.IsReadOnly = true;

                if (_emitted && EmittedQpcTime.IsZero)
                    EmittedQpcTime = QpcTimeStamp.Now;
            }

            bool IEquatable<ILogMessage>.Equals(ILogMessage other)
            {
                return this.Equals(other, includeEmittedQpcTime: false);
            }

            /// <summary>
            /// IEquatable{ILogMessage}.Equals implementation method - allows caller to indicate if emittedQpcTime should be included in comparison (defaults to false).
            /// </summary>
            public bool Equals(ILogMessage other, bool includeEmittedQpcTime = false)
            {
                bool edtEquals = (EmittedDateTime == other.EmittedDateTime);

                return (other != null
                    && SeqNum == other.SeqNum
                    && LoggerName == other.LoggerName
                    && MesgType == other.MesgType
                    && Mesg == other.Mesg
                    && NamedValueSet.Equals(other.NamedValueSet)
                    && Data.IsEqualTo(other.Data)
                    && Emitted == other.Emitted
                    && (!includeEmittedQpcTime || EmittedQpcTime == other.EmittedQpcTime)
                    && EmittedDateTime == other.EmittedDateTime
                    && ThreadID == other.ThreadID
                    && Win32ThreadID == other.Win32ThreadID
                    && ThreadName == other.ThreadName
                    );
            }
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
		/// The use of this interface (and the associated MesgEmitterImpl class) reduces the number of method signature variations that
		/// are needed to support a full set of client message generation use models by removing the MesgType from the equation.  This allows
		/// us to use optimized versions of the 0, 1, 2 and 3 argument string format patterns and helps minimize the cost of including Emit 
		/// calls for these numbers of arguments where the Emit call is generally disabled in the source.
		/// </remarks>
        public interface IMesgEmitter
		{
            /// <summary>True if the IMesgEmitter is valid and its mesgType is enabled in the parent logger object</summary>
            bool IsEnabled { get; }

            /// <summary>Gives the caller access to the message type that this emitter will generate - mainly used for emitters that generate LogMessage objects.</summary>
            MesgType MesgType { get; }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will emit the given string using behavior defined by the actual implementation class.</summary>
            /// <param name="str">Gives the string message body to emit.</param>
            void Emit(string str);

            /// <summary>EmitWith method.  If the Emitter IsEnabled then it will emit the given string using behavior defined by the actual implementation class.</summary>
            /// <param name="str">Gives the string message body to emit.</param>
            /// <param name="nvs">Gives the INamedValueSet that is to be included in the message (will get merged with any default NVS defined by logger)</param>
            /// <param name="data">Gives the byte array data that is to be included in the message</param>
            void EmitWith(string str, INamedValueSet nvs = null, byte[] data = null);

            /// <summary>Variant of CheckedFormat for the same parameter signature</summary>
            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and arg, using Fcns.CheckedFormat, and emit the resulting string.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            void Emit(string fmt, object arg0);

            /// <summary>Variant of CheckedFormat for the same parameter signature</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            void Emit(string fmt, object arg0, object arg1);

            /// <summary>Variant of CheckedFormat for the same parameter signature</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg2">Gives the third argument object instance that will be formatted into a string in the resulting message</param>
            void Emit(string fmt, object arg0, object arg1, object arg2);

            /// <summary>Variant of CheckedFormat for the same parameter signature</summary>
            void Emit(string fmt, params object[] args);

            /// <summary>Variant of CheckedFormat for the same parameter signature</summary>
            void Emit(IFormatProvider provider, string fmt, params object[] args);

            /// <summary>
            /// Emit method variant.  If the Emitter IsEnabled then it will emit the given string using behavior defined by the actual implementation class.
            /// Parameter signature supports nested use with offset distance to root caller for stack frame recording.
            /// </summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="str">Gives the string message body to emit.</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            void Emit(int skipNStackFrames, string str);

            /// <summary>Variant of CheckedFormat for the same parameter signature for nested use with offset distance to root caller for stack frame recording</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            void Emit(int skipNStackFrames, string fmt, object arg0);

            /// <summary>Variant of CheckedFormat for the same parameter signature for nested use with offset distance to root caller for stack frame recording</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            void Emit(int skipNStackFrames, string fmt, object arg0, object arg1);

            /// <summary>Variant of CheckedFormat for the same parameter signature for nested use with offset distance to root caller for stack frame recording</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg2">Gives the third argument object instance that will be formatted into a string in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            void Emit(int skipNStackFrames, string fmt, object arg0, object arg1, object arg2);

            /// <summary>Variant of CheckedFormat for the same parameter signature for nested use with offset distance to root caller for stack frame recording</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="args">Gives the array of object instances that will be formatted into a strings in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            void Emit(int skipNStackFrames, string fmt, params object[] args);

            /// <summary>Variant of CheckedFormat for the same parameter signature for nested use with offset distance to root caller for stack frame recording</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="provider">Gives the culture specific IFormatProvider that will be used to convert individual Object instances into their string equivalents</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="args">Gives the array of object instances that will be formatted into a strings in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            void Emit(int skipNStackFrames, IFormatProvider provider, string fmt, params object[] args);
        }

        /// <summary>Returns an emitter that may be used to accept emit calls but which will never emit anything.</summary>
        public static IMesgEmitter NullEmitter { get { return NullMesgEmitter.Instance; } }

        /// <summary>
        /// Interface used by BasicLoggerBase to allow it to set the INamedValueSet used with the logger's created message emitters.  Derived classes are expected to support this interface
        /// with their IMesgEmitter implementation class in order to properly support the SetDefaultNamedValueSetForEmitter functionality provided by the BasicLoggerBase.
        /// </summary>
        public interface IDefaultNamedValueSetSetter
        {
            /// <summary>
            /// Setter only property.  Passes the desired NamedValueSet that shall be given to all future log messages emitted by the message emitter implementation class that implements this interface.
            /// </summary>
            INamedValueSet DefaultNamedValueSet { set; }

            /// <summary>
            /// Setter only property.  Passes the desired merge behavior that shall be used when merging the DefaultNamedValueSet into any caller provided one.  Defaults to NamedValueSetMergeBehavior.AddNewItems.
            /// </summary>
            NamedValueMergeBehavior DefaultNamedValueSetMergeBehavior { set; }
        }

        #endregion

        //-------------------------------------------------------------------
        #region IBasicLogger and ILogger

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

            /// <summary>
            /// Returns a message emitter that will emit messages of the given MesgType and from this logger.
            /// <para/>If the given mesgType is less than or equal to MesgType.None or if the given mesgType is > MesgType.Max then the method returns Logging.NullEmitter
            /// </summary>
            IMesgEmitter Emitter(MesgType mesgType);

            /// <summary>
            /// MesgType indexed property.  This is a short hand version of the Emitter method defined here.
            /// <para/>If the given mesgType is less than or equal to MesgType.None or if the given mesgType is > MesgType.Max then the method returns Logging.NullEmitter
            /// </summary>
            IMesgEmitter this[MesgType mesgType] { get; }

            /// <summary>Allows the caller to specify the default NamedValueSet value that is to be attached to all LogMessages generated by the logger's emitter for the given mesgType.  Supports call chaining.</summary>
            IBasicLogger SetDefaultNamedValueSetForEmitter(MesgType mesgType, INamedValueSet nvs, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddNewItems);

            /// <summary>Allows the caller to specify the default NamedValueSet value that is to be attached to all LogMessages generated by the logger's emitter for for all of the message types that match the given log gate.  Supports call chaining.</summary>
            IBasicLogger SetDefaultNamedValueSetForEmitter(LogGate logGate, INamedValueSet nvs, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddNewItems);

            /// <summary>
            /// Helper method: Gets the current System.Diagnostics.StackFrame of the caller (up skipNStackFrames levels from the level above this method)
            /// <para/>Returns created StackFrame if stack frame tagging support is currently enabled or null if it is not
            /// </summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            [Obsolete("Support for recording stack frame during logging has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            System.Diagnostics.StackFrame GetStackFrame(int skipNStackFrames);

            /// <summary>
            /// This property returns true if this logger is currently configured to record stack frames
            /// </summary>
            [Obsolete("Support for recording stack frame during logging has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            bool IsRecordingSourceStackFrame { get; }
        }

		/// <summary>This interface defines the methods that is provided by all entities that can act like log message sources.</summary>
		/// <remarks>
		/// This interface defines how loggers may be used as gateways to the LogMessage allocation and distribution system.  The
		/// underlying ILogMessageDistribution object maintains a pool of LogMessages and a list of LogMessageHandler objects.
		/// Alternately a local ILogger instance may allocated its own copies of LogMessages which will not be released to the
		/// LogMessageDistribution system's pool when it has finished using them.
		/// 
		/// The ILogger is used to create an association of a name, an integer loggerID, a per source instance gate and a cached
		/// copy of the distribution gate for the given name.  It serves the purpose of simplifying access to the allocation and
		/// distribution system.
		/// 
		/// Note: multiple instances of this interface may be used with the same logger name.  Each instance will share the same
		/// id, source and distribution gating.  However, each instance may specify a separate instance gate value if desired.
		/// The default value of the instance gate is to pass all messages so that by default, the distribution gate is the only
		/// gate that may prevent allocation (and thus generation) of certain types of log messages.
		/// 
		/// MT Note:  Each logger instance's IsTypeEnabled and GetLogMessage and EmitLogMessage methods
		/// may be safely used by multiple threads in a reentrant fashion with the following restrictions:
		///		A) the logger instance must not be created, or reconfigured using multiple threads.
		///		B) the IsTypeEnabled method may not produce the correct result if multiple threads attempt
		///			to use it concurrently immediately after there has been a change in the distribution
		///			system gating for this logger.  Attempted uses after the the one that applies the change
		///			will produce consistent and correct results.
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

            /// <summary>returns a new message with type, source, message, file and line filled in (as appropriate and enabled).</summary>
            LogMessage GetLogMessage(MesgType mesgType, string mesg = null, INamedValueSet nvs = null, byte [] data = null);

            /// <summary>returns a new message with type, source, message, file and line filled in (as appropriate and enabled).</summary>
            [Obsolete("The sourceStackFrame parameter is no longer supported.  Please use the variant of this method without this parameter.  (2017-07-21)")]
            LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame, int skipNStackFrames = 0);

            /// <summary>returns a new message with type, source, message, file and line filled in.</summary>
            [Obsolete("The sourceStackFrame and allocateFromDist parameters are no longer supported.  Please use the variant of this method without these parameters.  (2016-12-22, 2017-07-21)")]
            LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame, bool allocatedFromDist);

            /// <summary>Emits the message.  Takes ownership by setting the caller's reference to null.</summary>
			void EmitLogMessage(ref LogMessage mesg);

            /// <summary>Waits for last message emitted by this logger to have been distributed and processed.</summary>
            bool WaitForDistributionComplete(TimeSpan timeLimit);

            /// <summary>shuts down this source and prevents it from allocating or emitting further log messages.</summary>
            void Shutdown();
		};

		#endregion

        #region ILogger Extension methods

        /// <summary>ILogger extension methods: Makes a copy of the given message and Emits it.</summary>
        public static void CopyAndEmitLogMessage(this ILogger logger, LogMessage mesg)
        {
            LogMessage mesgCopy = new LogMessage(mesg);
            logger.EmitLogMessage(ref mesgCopy);
        }

        #endregion

        //-------------------------------------------------------------------
        #region BasicLoggerBase

        /// <summary>
        /// This class is a utility/base class that assists in implementing IBasicLogger objects.
        /// It provides common glue code for supporting the various means of getting IMesgEmitter objects
        /// from the IBasicLogger.  This class is abstract in that it makes no assumptions about the implementation
        /// of any specific IMesgEmitter.  As such it requires a derived class to implement the CreateMesgEmitter 
        /// abstract method which is used as a factory to create actual IMesgEmitter implementation objects for the
        /// client to use to emit messages.
        /// </summary>

        public abstract class BasicLoggerBase : IBasicLogger
        {
            /// <summary>Default constructor</summary>
            public BasicLoggerBase() { }

            /// <summary>Copy constructor.  Makes a new LoggerConfig observer as a copy of the one use by the rsh</summary>
            public BasicLoggerBase(BasicLoggerBase rhs)
            {
                LoggerConfigObserver = new SequencedLoggerConfigObserver(rhs.LoggerConfigObserver);
            }

            /// <summary>
            /// Allows a derived class to provide a LoggerConfigObserver for this object that it can safely use to determine if it
            /// is expected to record stack frames (or not).
            /// </summary>
            protected SequencedLoggerConfigObserver LoggerConfigObserver { get; set; }

            /// <summary>
            /// Derived class must implement this method to create a message emitter of the requested type.
            /// The BasicLoggerBase will retain the created emitter and use it for all later logging requests of that type.
            /// </summary>
            /// <param name="mesgType">Gives the message type that the emitter is to produce.</param>
            /// <returns>An implementation of the IMesgEmitter interface that may be used to emit messages for the requested type.</returns>
            protected abstract IMesgEmitter CreateMesgEmitter(MesgType mesgType);

            #region IBasicLogger interface

            /// <summary>Returns a message emitter for Error messages from this logger</summary>
            public IMesgEmitter Error { get { return (error ?? (error = Emitter(MesgType.Error))); } }
            
            /// <summary>Returns a message emitter for Warning messages from this logger</summary>
            public IMesgEmitter Warning { get { return (warning ?? (warning = Emitter(MesgType.Warning))); } }
            
            /// <summary>Returns a message emitter for Signif messages from this logger</summary>
            public IMesgEmitter Signif { get { return (signif ?? (signif = Emitter(MesgType.Signif))); } }
            
            /// <summary>Returns a message emitter for Info messages from this logger</summary>
            public IMesgEmitter Info { get { return (info ?? (info = Emitter(MesgType.Info))); } }
            
            /// <summary>Returns a message emitter for Debug messages from this logger</summary>
            public IMesgEmitter Debug { get { return (debug ?? (debug = Emitter(MesgType.Debug))); } }

            /// <summary>Returns a message emitter for Trace messages from this logger</summary>
            public IMesgEmitter Trace { get { return (trace ?? (trace = Emitter(MesgType.Trace))); } }

            /// <summary>
            /// Returns a message emitter that will emit messages of the given MesgType and from this logger.
            /// <para/>If the given mesgType is less than or equal to MesgType.None or if the given mesgType is > MesgType.Max then the method returns Logging.NullEmitter
            /// </summary>
            public IMesgEmitter Emitter(MesgType mesgType)
            {
                IMesgEmitter emitter = null;

                int mesgTypeIdx = (int)mesgType;
                if (mesgTypeIdx > (int)MesgType.None && mesgTypeIdx < emitters.Length)
                {
                    emitter = emitters[mesgTypeIdx];
                    if (emitter == null)
                    {
                        emitter = emitters[mesgTypeIdx] = CreateMesgEmitter(mesgType);
                    }
                }

                return ((emitter != null) ? emitter : Logging.NullEmitter);
            }

            /// <summary>
            /// MesgType indexed property.  This is a short hand version of the Emitter method defined here.
            /// <para/>If the given mesgType is less than or equal to MesgType.None or if the given mesgType is > MesgType.Max then the method returns Logging.NullEmitter
            /// </summary>
            public IMesgEmitter this[MesgType mesgType] 
            {
                get { return Emitter(mesgType); } 
            }

            /// <summary>Allows the caller to specify the default NamedValueSet value that is to be attached to all LogMessages generated by the logger's emitter for the given mesgType.  Supports call chaining.</summary>
            public IBasicLogger SetDefaultNamedValueSetForEmitter(MesgType mesgType, INamedValueSet nvs, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddNewItems)
            {
                if (mesgType != MesgType.All)
                {
                    IMesgEmitter emitter = Emitter(mesgType);
                    IDefaultNamedValueSetSetter defaultNvsSetter = emitter as IDefaultNamedValueSetSetter;

                    if (defaultNvsSetter != null)
                    {
                        defaultNvsSetter.DefaultNamedValueSet = nvs;
                        defaultNvsSetter.DefaultNamedValueSetMergeBehavior = mergeBehavior;
                    }
                }
                else
                {
                    foreach (IMesgEmitter emitter in AllMesgTypes.Select(mt => Emitter(mt)))
                    {
                        IDefaultNamedValueSetSetter defaultNvsSetter = emitter as IDefaultNamedValueSetSetter;

                        if (defaultNvsSetter != null)
                        {
                            defaultNvsSetter.DefaultNamedValueSet = nvs;
                            defaultNvsSetter.DefaultNamedValueSetMergeBehavior = mergeBehavior;
                        }
                    }
                }

                return this;
            }

            /// <summary>Allows the caller to specify the default NamedValueSet value that is to be attached to all LogMessages generated by the logger's emitter for for all of the message types that match the given log gate.  Supports call chaining.</summary>
            public IBasicLogger SetDefaultNamedValueSetForEmitter(LogGate logGate, INamedValueSet nvs, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddNewItems)
            {
                foreach (var mesgType in AllMesgTypes)
                {
                    if (logGate.IsTypeEnabled(mesgType))
                        SetDefaultNamedValueSetForEmitter(mesgType, nvs, mergeBehavior: mergeBehavior);
                }

                return this;
            }

            /// <summary>Helper method: Gets the current System.Diagnostics.StackFrame of the caller (up skipNStackFrames levels)</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <returns>Selected StackFrame if stack frame tagging support is currently enabled or null if it is not</returns>
            [Obsolete("Support for recording stack frame during logging has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            public System.Diagnostics.StackFrame GetStackFrame(int skipNStackFrames)
            {
                return null;
            }

            /// <summary>
            /// This property returns true if this logger is currently configured to record stack frames
            /// </summary>
            [Obsolete("Support for recording File and Line has been removed.  Use of this property is no longer supported. (2017-07-21)")]
            public bool IsRecordingSourceStackFrame { get { return false; } }

            #endregion

            /// <summary>
            /// Preallocated array of IMesgEmitters that have been created.  Allows this caller to create only the emitters that are actually used
            /// but also allows it to reuse each emitter type without effort.
            /// </summary>
            private IMesgEmitter[] emitters = new IMesgEmitter[((int)MesgType.Max) + 1];

            /// <summary>
            /// per type IMesgEmitter cached results to further improve the code paths for the high usage direct message type IMesgEmitter properies
            /// </summary>
            private IMesgEmitter error, warning, signif, info, debug, trace;
        }

        #endregion

        //-------------------------------------------------------------------
        #region IMesgEmitter Implementations: LoggerMesgEmitterImpl, LMHMesgEmitter, ListEmitter, NullMesgEmitter, ThrowMesgEmitter, GenericMesgEmitterBase

        #region LoggerMesgEmitterImpl

        /// <summary>
        /// This is the primary IMesgEmitter implementation class that is used with all ILogger objects (but not with all IBasicLogger ones).
        /// This implementation class provides implementations of each of the Emit signatures by using the ILogger object to determine if the
        /// Emitter's message type is enabled, and if so it allocates a log message from the distribution pool, places the message string into
        /// the log message object and then emits the message back through the ILogger into the distribution system.
        /// All Emit method signatures which make use of formating make use of Utils.Fcns.CheckedFormat to eliminate some common causes of logging
        /// induced exceptions.
        /// </summary>
		public class LoggerMesgEmitterImpl : GenericMesgEmitterBase, IDefaultNamedValueSetSetter
		{
            /// <summary>
            /// get/set: gives the ILogger which will be used as the source for the all emitted messages.  
            /// This ILogger is also used to obtain Stack Frames and to allocate LogMessage objects.
            /// </summary>
            public ILogger Logger { get; set; }

            /// <summary>Debugging helper method</summary>
            public override string ToString()
            {
                return Utils.Fcns.CheckedFormat("LoggerEmitter {0} {1}", MesgType, Logger.Name);
            }

			#region IMesgEmitter override Members

            /// <summary>
            /// Returns true if Logger is non-null and if it reports IsTypeEnabled(MesgType).
            /// </summary>
			public override bool IsEnabled { get { return (Logger != null && Logger.IsTypeEnabled(MesgType)); } }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will acquire a LogMessage, assign the given str as its mesg body and emit it using the associated Logger instance.</summary>
            /// <param name="str">Gives the string message body to emit.</param>
            public override void Emit(string str)
            {
                if (IsEnabled)
                {
                    LogMessage lm = Logger.GetLogMessage(MesgType, str)
                        .SetDefaults(DefaultNamedValueSet, DefaultNamedValueSetMergeBehavior);
                    Logger.EmitLogMessage(ref lm);
                }
            }

            /// <summary>EmitWith method.  If the Emitter IsEnabled then it will emit the given string using behavior defined by the actual implementation class.</summary>
            /// <param name="str">Gives the string message body to emit.</param>
            /// <param name="nvs">Gives the INamedValueSet that is to be included in the message (will get merged with any default NVS defined by logger)</param>
            /// <param name="data">Gives the byte array data that is to be included in the message</param>
            public override void EmitWith(string str, INamedValueSet nvs = null, byte [] data = null)
            {
                if (IsEnabled)
                {
                    LogMessage lm = Logger.GetLogMessage(MesgType, str, nvs: nvs, data: data)
                        .SetDefaults(DefaultNamedValueSet, DefaultNamedValueSetMergeBehavior);
                    Logger.EmitLogMessage(ref lm);
                }
            }


            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will acquire a LogMessage, format the given fmt and args to generate the LogMessage Mesg body and emit the LogMessage using the associated Logger instance.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            public override void Emit(string fmt, object arg0)
            {
                if (IsEnabled)
                {
                    LogMessage lm = Logger.GetLogMessage(MesgType, fmt.CheckedFormat(arg0))
                        .SetDefaults(DefaultNamedValueSet, DefaultNamedValueSetMergeBehavior);
                    Logger.EmitLogMessage(ref lm);
                }
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will acquire a LogMessage, format the given fmt and args to generate the LogMessage Mesg body and emit the LogMessage using the associated Logger instance.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            public override void Emit(string fmt, object arg0, object arg1)
            {
                if (IsEnabled)
                {
                    LogMessage lm = Logger.GetLogMessage(MesgType, fmt.CheckedFormat(arg0, arg1))
                        .SetDefaults(DefaultNamedValueSet, DefaultNamedValueSetMergeBehavior);
                    Logger.EmitLogMessage(ref lm);
                }
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will acquire a LogMessage, format the given fmt and args to generate the LogMessage Mesg body and emit the LogMessage using the associated Logger instance.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg2">Gives the third argument object instance that will be formatted into a string in the resulting message</param>
            public override void Emit(string fmt, object arg0, object arg1, object arg2)
            {
                if (IsEnabled)
                {
                    LogMessage lm = Logger.GetLogMessage(MesgType, fmt.CheckedFormat(arg0, arg1, arg2))
                        .SetDefaults(DefaultNamedValueSet, DefaultNamedValueSetMergeBehavior);
                    Logger.EmitLogMessage(ref lm);
                }
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will acquire a LogMessage, format the given fmt and args to generate the LogMessage Mesg body and emit the LogMessage using the associated Logger instance.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="args">Gives the array of object instances that will be formatted into a strings in the resulting message</param>
            public override void Emit(string fmt, params object[] args)
            {
                if (IsEnabled)
                {
                    LogMessage lm = Logger.GetLogMessage(MesgType, fmt.CheckedFormat(args))
                        .SetDefaults(DefaultNamedValueSet, DefaultNamedValueSetMergeBehavior);
                    Logger.EmitLogMessage(ref lm);
                }
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will acquire a LogMessage, format the given fmt and args to generate the LogMessage Mesg body and emit the LogMessage using the associated Logger instance.</summary>
            /// <param name="provider">Gives the culture specific IFormatProvider that will be used to convert individual Object instances into their string equivalents</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="args">Gives the array of object instances that will be formatted into a strings in the resulting message</param>
            public override void Emit(IFormatProvider provider, string fmt, params object[] args)
            {
                if (IsEnabled)
                {
                    LogMessage lm = Logger.GetLogMessage(MesgType, provider.CheckedFormat(fmt, args))
                        .SetDefaults(DefaultNamedValueSet, DefaultNamedValueSetMergeBehavior);
                    Logger.EmitLogMessage(ref lm);
                }
            }

            #endregion

            #region IDefaultNamedValueSetSetter interface

            /// <summary>
            /// Setter only property.  Passes the desired INamedValueSet that shall be given to all future log messages emitted by the message emitter implementation class that implements this interface.
            /// </summary>
            public INamedValueSet DefaultNamedValueSet
            {
                get { return _defaultNamedValueSet; }
                set { _defaultNamedValueSet = value.ConvertToReadOnly(mapNullToEmpty: false); }
            }

            private volatile INamedValueSet _defaultNamedValueSet = null;

            /// <summary>
            /// Setter only property.  Passes the desired merge behavior that shall be used when merging the DefaultNamedValueSet into any caller provided one.  Defaults to NamedValueSetMergeBehavior.AddNewItems.
            /// </summary>
            public NamedValueMergeBehavior DefaultNamedValueSetMergeBehavior 
            {
                get { return _defaultNamedValueSetMergeBehavior; }
                set { _defaultNamedValueSetMergeBehavior = value; }
            }

            private volatile NamedValueMergeBehavior _defaultNamedValueSetMergeBehavior = NamedValueMergeBehavior.AddNewItems;

            #endregion
        }

        #endregion

        #region locally defined extension method

        /// <summary>
        /// Call chaining extension method that is used to set or update the NamedValueSet property on the given lm LogMessage instance.
        /// </summary>
        public static LogMessage SetDefaults(this LogMessage lm, INamedValueSet nvs, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddNewItems)
        {
            if (lm != null && nvs != null)
            {
                if (lm.Raw_nvs == null)
                    lm.NamedValueSet = nvs;
                else if (!nvs.IsNullOrEmpty())
                    lm.NamedValueSet = lm.Raw_nvs.MergeWith(nvs, mergeBehavior: mergeBehavior);
            }

            return lm;
        }

        /// <summary>
        /// Call chaining extension method that is used to set the Mesg property on the given lm LogMessage instance.
        /// </summary>
        public static LogMessage SetMesg(this LogMessage lm, string mesg)
        {
            if (lm != null)
                lm.Mesg = mesg;

            return lm;
        }

        #endregion

        #region LMHMesgEmitter

        /// <summary>
        /// This emitter is used to emit messages directly into a given ILogMessageHandler instance.  This is currently used for unit testing.
        /// </summary>
        public class LMHMesgEmitter : GenericMesgEmitterBase
        {
            /// <summary>Constructor</summary>
            public LMHMesgEmitter(ILogMessageHandler lmh, MesgType mesgType)
            {
                this.lmh = lmh;
                MesgType = mesgType;

                IsEnabled = (lmh != null) && lmh.LoggerConfig.LogGate.IsTypeEnabled(mesgType);
            }

            private ILogMessageHandler lmh;

            /// <summary>
            /// Method implements the required abstract basic Emit method from the base class.
            /// If the emitter is  enabled then this method generates a log message and asks the referenced ILogMessageHandler to handle it.
            /// </summary>
            /// <param name="str">Gives the string message body to emit.</param>
            public override void Emit(string str)
            {
                if (IsEnabled)
                {
                    LogMessage lm = new LogMessage()
                        .Setup(LoggerSourceInfo.Empty, MesgType, str)
                        .NoteEmitted();

                    lmh.HandleLogMessage(lm);
                }
            }

            /// <summary>
            /// Method re-implements the EmitWith method from the base class.
            /// If the emitter is enabled then this method generates a log message and asks the referenced ILogMessageHandler to handle it.
            /// </summary>
            /// <param name="str">Gives the string message body to emit.</param>
            /// <param name="nvs">Gives the INamedValueSet to emit with the message.</param>
            /// <param name="data">Gives the byte array data to emit with the message.</param>
            public override void EmitWith(string str, INamedValueSet nvs = null, byte[] data = null)
            {
                if (IsEnabled)
                {
                    LogMessage lm = new LogMessage()
                        .Setup(LoggerSourceInfo.Empty, MesgType, str, nvs: nvs, data: data)
                        .NoteEmitted();

                    lmh.HandleLogMessage(lm);
                }
            }
        }

        #endregion

        #region ListMesgEmitter

        /// <summary>
        /// This emitter is used to collect emitted messages into a List of Items, each of which contains an emitted MesgStr, and optional NVS and Data (from EmitWith)
        /// </summary>
        public class ListMesgEmitter : GenericMesgEmitterBase
        {
            /// <summary>Default constructor</summary>
            public ListMesgEmitter(bool isEnabled = true)
            {
                IsEnabled = isEnabled;
                EmittedItemList = new List<Item>();
            }

            /// <summary>
            /// get/set property implements required abstract property from base class.  May be set as desired to enable and disable this emitter's Emit methods from emitting anything.
            /// <para/>Defaults to true.
            /// </summary>
            public new bool IsEnabled { get { return base.IsEnabled; } set { base.IsEnabled = value; } }

            /// <summary>
            /// Method implements the required abstract basic Emit method from the base class.
            /// If the emitter is not enabled then this method immediately returns.
            /// Generates a new Item with its Mesg property assigned to the given Str.  
            /// The resulting Item is appended to the EmittedItemList maintained by this instance.
            /// </summary>
            /// <param name="str">Gives the string message body to emit.</param>
            public override void Emit(string str)
            {
                EmitWith(str);
            }

            /// <summary>EmitWith method.  If the Emitter IsEnabled then it will emit the given string using behavior defined by the actual implementation class.</summary>
            /// <param name="str">Gives the string message body to emit.</param>
            /// <param name="nvs">Gives the INamedValueSet that is to be included in the message (will get merged with any default NVS defined by logger)</param>
            /// <param name="data">Gives the byte array data that is to be included in the message</param>
            public override void EmitWith(string str, INamedValueSet nvs = null, byte[] data = null)
            {
                if (!IsEnabled)
                    return;

                Item item = new Item() { MesgStr = str, NVS = nvs, Data = data, SeqNum = staticItemSeqNumGen.Increment() };

                using (new ScopedLock(EmittedItemListMutex))
                {
                    EmittedItemList.Add(item);
                }
            }

            /// <summary>
            /// Gives the caller access to the List of Items that have been generated by this emitter.  Caller may manipulate this list provided that it is done in a means that does not violate
            /// concurrency rules for the underlying List.  <see cref="EmittedItemListMutex"/> property which may be used to synchronize internal and external access to the EmittedItemList if 
            /// this emitter may be used concurrently by more than one thread.
            /// </summary>
            public List<Item> EmittedItemList { get; private set; }

            /// <summary>Clears the contents of the EmittedItemList.  Threadsafe if non-null <see cref="EmittedItemListMutex"/> has been provided.</summary>
            public void Clear() { using (new ScopedLock(EmittedItemListMutex)) { EmittedItemList.Clear(); } }

            /// <summary>Captures and returns an array of the Items that are currently in the.  Threadsafe if non-null <see cref="EmittedItemListMutex"/> has been provided.</summary>
            public Item[] EmitterItemArray { get { using (new ScopedLock(EmittedItemListMutex)) { return EmittedItemList.ToArray(); } } }

            /// <summary>
            /// Returns an IEnumerable that produces the set of MesgStrs from each of the items that are currently in the list
            /// </summary>
            public IEnumerable<string> EmittedMesgStrs { get { return EmitterItemArray.Select(item => item.MesgStr); } }

            /// <summary>Optional: If this object is assigned a non-null value it will be used as a mutex when internally accessing the EmittedItemList to append items to it.</summary>
            public object EmittedItemListMutex { get; set; }

            private static AtomicUInt64 staticItemSeqNumGen = new AtomicUInt64();

            /// <summary>This is the container object used for emitted items.  It consists of a MesgStr and a, possibly null, StackFrame.</summary>
            public class Item
            {
                /// <summary>Returns the emitted Message String for this Item</summary>
                public String MesgStr { get; internal set; }

                /// <summary>Returns the emitted NVS for this Item (may be null)</summary>
                public INamedValueSet NVS { get; internal set; }

                /// <summary>Returns the emitted Data for this Item (may be null)</summary>
                public byte [] Data { get; internal set; }

                /// <summary>Returns the sequence number assigned to this item.</summary>
                public ulong SeqNum { get; internal set; }

                /// <summary>Returns the collected StackFrame for this Item or null if no frame was acquired.</summary>
                [Obsolete("Support for recording stack frame during logging has been removed.  Use of this property is no longer supported. (2017-07-21)")]
                public System.Diagnostics.StackFrame StackFrame { get { return null; } internal set { } }

                /// <summary>Generates a printable version of the contained MesgStr (and StackFrame if included) contents</summary>
                public override string ToString()
                {
                    StringBuilder sb = new StringBuilder();

                    sb.CheckedAppendFormat("MesgStr:[{0}]", MesgStr.GenerateSquareBracketEscapedVersion());

                    if (NVS != null)
                        sb.CheckedAppendFormat(" {0}", NVS.ToStringSML());

                    if (Data != null)
                        sb.CheckedAppendFormat(" Data:[{0}]", ByteArrayTranscoders.HexStringTranscoder.Encode(Data));

                    sb.CheckedAppendFormat(" SeqNum:{0}", SeqNum);

                    return sb.ToString();
                }
            }
        }

        #endregion

        #region NullMesgEmitter

        /// <summary>
        /// This class implements a generic null emitter class.
        /// </summary>
        public class NullMesgEmitter : GenericMesgEmitterBase
        {
            /// <summary>
            /// Simple singleton instance of this NullMesgEmitter class.
            /// </summary>
            public static readonly IMesgEmitter Instance = new NullMesgEmitter();

            /// <summary>Returns false</summary>
            public override bool IsEnabled { get { return false; } }
        }

        #endregion

        #region ThrowMesgEmitter

        /// <summary>
        /// This class implements an IMesgEmitter that will throw an exception containing the emitted string.
        /// This class has two constructors.  The default constructor builds an instance that throws a custom exception type as <see cref="ThrowMesgEmitter.Exception"/>.
        /// The custom constructor requires that the caller provide a delegate that is used to construct the exceptions that are thrown by Emit.
        /// </summary>
        public class ThrowMesgEmitter : GenericMesgEmitterBase
        {
            /// <summary>
            /// Default constructor.  Using this constructor will cause the emitter to throw a <see cref="ThrowMesgEmitter.Exception"/> instance for each call to Emit.
            /// </summary>
            public ThrowMesgEmitter()
                : this(mesg => new ThrowMesgEmitter.Exception(Utils.Fcns.MapNullOrEmptyTo(mesg, "[Emit given empty string]")))
            { }

            /// <summary>
            /// Custom exception factory delegate Constructor.  
            /// The provided delegate instance is used to construct and returns the exception to be thrown on each Emit call.  
            /// If this delegate instance returns null then no exception will be thrown for that Emit call.
            /// </summary>
            /// <param name="exceptionFactoryDelegate">Gives the instance of the exception factory that is to be used.  Must be of type <see cref="ExceptionFactoryDelegate"/></param>
            public ThrowMesgEmitter(ExceptionFactoryDelegate exceptionFactoryDelegate)
            {
                if (exceptionFactoryDelegate == null)
                    throw new System.ArgumentNullException("exceptionFactoryDelegate");

                this.exceptionFactoryDelegate = exceptionFactoryDelegate;
            }

            /// <summary>
            /// The delegate type required by this class.  Accepts a string message and returns an instance that is derived from type <see cref="System.Exception"/> or null.
            /// If this delegate returns null then no exception will be thrown by this emitter.
            /// </summary>
            /// <param name="mesgStr">The message to emit</param>
            /// <returns>An instance of type <see cref="System.Exception"/> or null.</returns>
            public delegate System.Exception ExceptionFactoryDelegate(string mesgStr);

            /// <summary>
            /// This Exception class is the default Exception class used when the ThrowMesgEmitter's default constructor is used.
            /// </summary>
            public class Exception : System.Exception
            {
                /// <summary>Exception Constructor</summary>
                /// <param name="msg">Receives the emitted message and stores it as the Exception's Message</param>
                public Exception(string msg) : base(msg) { }
            }

            /// <summary>Returns true</summary>
            public override bool IsEnabled { get { return true; } }

            /// <summary>
            /// This method passes the given str to the ExceptionFactoryDelegate to generate a new System.Exception or ThrowMesgEmitter.Exception instance which is then thrown.
            /// </summary>
            public override void Emit(string str)
            {
                System.Exception ex = exceptionFactoryDelegate(str);
                if (ex != null)
                    throw ex;
            }

            private ExceptionFactoryDelegate exceptionFactoryDelegate;
        }

        #endregion

        #region GenericMesgEmitterBase

        /// <summary>
        /// This is a Generic IMesgEmitter base class.  
        /// It provides a set of default implementations for most of the IMesgEmitter related classes to simplify creation of additional IMesgEmitter types.
        /// All Emit method signatures which make use of formating make use of Utils.Fcns.CheckedFormat to eliminate some common causes of logging
        /// induced exceptions.
        /// </summary>
        public abstract class GenericMesgEmitterBase : IMesgEmitter
        {
            /// <summary>get/set property: Allows an emitter to be used recursively where it records the caller of its caller (etc.)</summary>
            public int SkipNAdditionalStackFrames { get; set; }

            /// <summary>Debugging helper method</summary>
            public override string ToString()
            {
                return Utils.Fcns.CheckedFormat("{0} Generic Emitter", MesgType);
            }

            #region IMesgEmitter Members

            /// <summary>get/protected set property.  If this is not set to True then the emitter will block emitting all frames.</summary>
            public virtual bool IsEnabled { get; protected set; }

            /// <summary>
            /// get/set property: Defines the MesgType that this emitter is associated with.  
            /// Mainly used for LogMessage type emitters.  May not have any actual use in other derived message emitter types.
            /// </summary>
            public MesgType MesgType { get; set; }

            /// <summary>get/protected set property implements.  Set to enable the collection of StackFrames for the emitted Items generated by this instance.</summary>
            [Obsolete("Support for recording stack frame during logging has been removed.  Use of this property is no longer supported. (2017-07-21)")]
            public virtual bool CollectStackFrames { get { return false; } set { } }

            /// <summary>Helper method: If enabled using the CollectStackFrames property, gets the current System.Diagnostics.StackFrame of the caller (up skipNStackFrames levels from the level above this method)</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <returns>Selected StackFrame if stack frame tagging support is currently enabled using the CollectStackFrames property, or null if it is not</returns>
            [Obsolete("Support for recording stack frame during logging has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            protected virtual System.Diagnostics.StackFrame GetStackFrame(int skipNStackFrames)
            {
                return null;
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will emit the given string using the Emit(1,str) variant.</summary>
            /// <param name="str">Gives the string message body to emit.</param>
            public virtual void Emit(string str) 
            { }

            /// <summary>EmitWith method.  If the Emitter IsEnabled then it will emit the given string using behavior defined by the actual implementation class.</summary>
            /// <param name="str">Gives the string message body to emit.</param>
            /// <param name="nvs">Gives the INamedValueSet that is to be included in the message (will get merged with any default NVS defined by logger)</param>
            /// <param name="data">Gives the byte array data that is to be included in the message</param>
            public virtual void EmitWith(string str, INamedValueSet nvs = null, byte [] data = null)
            {
                if (IsEnabled)
                {
                    StringBuilder sb = new StringBuilder(str);

                    if (nvs != null)
                        sb.CheckedAppendFormat(" nvs:{0}", nvs.ToStringSML());

                    if (data != null)
                        sb.CheckedAppendFormat(" data:[{0}]", ByteArrayTranscoders.HexStringTranscoder.Encode(data));

                    Emit(sb.ToString());
                }
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and arg, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            public virtual void Emit(string fmt, object arg0) 
            {
                if (IsEnabled)
                    Emit(Utils.Fcns.CheckedFormat(fmt, arg0));
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and args, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            public virtual void Emit(string fmt, object arg0, object arg1) 
            {
                if (IsEnabled)
                    Emit(Utils.Fcns.CheckedFormat(fmt, arg0, arg1));
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and args, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg2">Gives the third argument object instance that will be formatted into a string in the resulting message</param>
            public virtual void Emit(string fmt, object arg0, object arg1, object arg2) 
            {
                if (IsEnabled)
                    Emit(Utils.Fcns.CheckedFormat(fmt, arg0, arg1, arg2));
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and args, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="args">Gives the array of object instances that will be formatted into a strings in the resulting message</param>
            public virtual void Emit(string fmt, params object[] args) 
            {
                if (IsEnabled)
                    Emit(Utils.Fcns.CheckedFormat(fmt, args));
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and arg, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="provider">Gives the culture specific IFormatProvider that will be used to convert individual Object instances into their string equivalents</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="args">Gives the array of object instances that will be formatted into a strings in the resulting message</param>
            public virtual void Emit(IFormatProvider provider, string fmt, params object[] args) 
            {
                if (IsEnabled)
                    Emit(Utils.Fcns.CheckedFormat(provider, fmt, args));
            }

            /// <summary>Basic abstract Emit method variant.  Actual behavior is determined by the implementation object.</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="str">Gives the string message body to emit.</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            public virtual void Emit(int skipNStackFrames, string str)
            {
                Emit(str);
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and arg, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            public virtual void Emit(int skipNStackFrames, string fmt, object arg0)
            {
                Emit(fmt, arg0);
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and args, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            public virtual void Emit(int skipNStackFrames, string fmt, object arg0, object arg1)
            {
                Emit(fmt, arg0, arg1);
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and args, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="arg0">Gives the first argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg1">Gives the second argument object instance that will be formatted into a string in the resulting message</param>
            /// <param name="arg2">Gives the third argument object instance that will be formatted into a string in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            public virtual void Emit(int skipNStackFrames, string fmt, object arg0, object arg1, object arg2)
            {
                Emit(fmt, arg0, arg1, arg2);
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and args, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="args">Gives the array of object instances that will be formatted into a strings in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            public virtual void Emit(int skipNStackFrames, string fmt, params object[] args)
            {
                Emit(fmt, args);
            }

            /// <summary>Emit method variant.  If the Emitter IsEnabled then it will format the given fmt and args, using Fcns.CheckedFormat, and emit the resulting string using the Emit(1,str) variant.</summary>
            /// <param name="skipNStackFrames">Gives the number of additional stack frame to skip, from the one above this one, when acquiring a StackFrame to get the source file and line number from.</param>
            /// <param name="provider">Gives the culture specific IFormatProvider that will be used to convert individual Object instances into their string equivalents</param>
            /// <param name="fmt">Gives the format string that will be passed to CheckedFormat</param>
            /// <param name="args">Gives the array of object instances that will be formatted into a strings in the resulting message</param>
            [Obsolete("Support for recording source stack frames (for File and Line recording) has been removed.  Use of this method is no longer supported. (2017-07-21)")]
            public virtual void Emit(int skipNStackFrames, IFormatProvider provider, string fmt, params object[] args)
            {
                Emit(provider, fmt, args);
            }

            #endregion
        }

        #endregion

        #endregion

        //-------------------------------------------------------------------
        #region IMesgEmitter helpers

        /// <summary>
        /// This object is used to simplify configurable IMesgEmitters in client objects that do not have their own loggers 
        /// but which need to support emitting of messages using externally provided emitters.  This is a common case for nameless 
        /// utility classes that are generally embedded in some other object such as an active part and where the messages that they
        /// emit can reasonably be reported to have come from the hosting part.  Automatically handles mapping provided null values
        /// so that the nullEmitter is used by default or whenever the Emitter has been explicitly assigned to null.
        /// </summary>
        /// <remarks>This object is a struct to allow the JIT compiler to fully optimize the logic embedded in the Emitter property</remarks>
        public struct MesgEmitterContainer
        {
            /// <summary>Internal storage for the emitter.</summary>
            private IMesgEmitter mesgEmitter;

            /// <summary>get/set public accessible property.  setter may assigned an IMesgEmitter or null.  getter returns the given emitter or the NullEmitter by default or if the last set value was null.</summary>
            public IMesgEmitter Emitter 
            { 
                get { return mesgEmitter ?? (mesgEmitter = NullEmitter); } 
                set { mesgEmitter = value ?? NullEmitter; } 
            }

            /// <summary>Support implicit casting from GenericMesgEmitterBase(IMesgEmitter proxy) to MesgEmitterContainer</summary>
            public static implicit operator MesgEmitterContainer(GenericMesgEmitterBase emitter)
            {
                return new MesgEmitterContainer() { Emitter = emitter };
            }

            /// <summary>Explicit constructor from a given IMesgEmitter instance</summary>
            public MesgEmitterContainer(IMesgEmitter emitter)
                : this()
            {
                Emitter = emitter;
            }
        }

        /// <summary>
        /// This AnnotatedItemAttribute is used to annotate IMesgEmitter or MesgEmitterContainer properties or fields on a target object's class so that they can be updated using
        /// a dictionary of name/emitter pairs to assign the named properties/fields to use the correspondingly desired emitter instance.  
        /// <seealso cref="SetAnnotatedInstanceEmitters"/> which is used to perform this assignment when desired.
        /// </summary>
        public class MesgEmitterPropertyAttribute : Modular.Reflection.Attributes.AnnotatedItemAttributeBase {}

        /// <summary>
        /// This method accepts a target object and a Dictionary of name/IMesgEmitter pairs.  It harvests the object's type for the set of properties and fields that have been annotated
        /// using the <see cref="MesgEmitterPropertyAttribute"/> attribute and which support being assigned from either an IMesgEmitter or an MesgEmitterContainer.  It iterates through
        /// the dictionary's key/value pairs and assigns each annotated item that has a matching name to use the corresponding emitter.  Key/Value pairs in the dictionary that do not match
        /// any annotated property/field in the target object are ignored.  Annotated property/fields that cannot be assigned from an IMesgEmitter or an MesgEmitterContainer are also ignored.
        /// </summary>
        /// <typeparam name="TargetObjectType">This is the type of the target object form which the list of accessible IMesgEmitter/MesgEmitterContainer properties and fields are harvested.</typeparam>
        /// <param name="targetObject">This is the object which contains the usable properties and fields that may be assigned from the dictionary.</param>
        /// <param name="emitterSet">This is the set of key/value pairs that are used to select and assign the desired properties/fields.  </param>
        /// <remarks>
        /// This method uses reflection rather than dynamic code generation to support finding and assigning properties with the desired values.  
        /// This choice is based on the expectation that this method will only typically be used during startup and thus the added overhead of the use of the PropertyInfo/FieldInfo SetValue method
        /// will be limited to the period during which objects are being constructed.
        /// </remarks>
        public static void SetAnnotatedInstanceEmitters<TargetObjectType>(TargetObjectType targetObject, IDictionary<string, IMesgEmitter> emitterSet)
        {
            Dictionary<string, Modular.Reflection.Attributes.ItemInfo<MesgEmitterPropertyAttribute>> itemDictionary
                = Modular.Reflection.Attributes.AnnotatedClassItemAccessHelper<MesgEmitterPropertyAttribute>.ExtractItemInfoAccessDictionaryFrom(typeof(TargetObjectType), MosaicLib.Modular.Reflection.Attributes.ItemSelection.IncludeExplicitItems);

            foreach (KeyValuePair<string, IMesgEmitter> kvp in emitterSet)
            {
                string targetItemName = kvp.Key;
                IMesgEmitter emitter = kvp.Value;

                Modular.Reflection.Attributes.ItemInfo<MesgEmitterPropertyAttribute> item = null;

                if (itemDictionary.TryGetValue(targetItemName ?? String.Empty, out item))
                {
                    try
                    {
                        if (item.IsProperty)
                        {
                            if (item.CanSetValue && item.PropertyInfo.PropertyType.IsAssignableFrom(typeof(IMesgEmitter)))
                                item.PropertyInfo.SetValue(targetObject, emitter, null);
                            else if (item.CanSetValue && item.PropertyInfo.PropertyType.IsAssignableFrom(typeof(MesgEmitterContainer)))
                                item.PropertyInfo.SetValue(targetObject, new MesgEmitterContainer() { Emitter = emitter }, null);
                        }
                        else if (item.IsField)
                        {
                            if (item.CanSetValue && item.FieldInfo.FieldType.IsAssignableFrom(typeof(IMesgEmitter)))
                                item.FieldInfo.SetValue(targetObject, emitter);
                            else if (item.CanSetValue && item.FieldInfo.FieldType.IsAssignableFrom(typeof(MesgEmitterContainer)))
                                item.FieldInfo.SetValue(targetObject, new MesgEmitterContainer() { Emitter = emitter });
                        }
                    }
                    catch
                    { }
                }
            }
        }

        #endregion

        //-------------------------------------------------------------------
		#region LoggerBase

		/// <summary>
		/// This class is a partial implementation class for most Logger type objects.  
		/// It implements all default functionality but only provides a single constructor with a full set of arguments.
		/// This class is used as the base for the Logger class and the QueuedLogger class.
		/// </summary>
        public abstract class LoggerBase : BasicLoggerBase, ILogger
        {
            #region MyTraceEmitter - used for logging trace messages related to use of the logger itself

            /// <summary>MyTraceEmitter storage field.  Defaults to Logging.NullEmitter</summary>
            private IMesgEmitter myTraceEmitter;

            /// <summary>Property provides IMesgEmitter that is used for logger trace messages (construction, destruction, cloning, ...)</summary>
            protected IMesgEmitter MyTraceEmitter { get { return myTraceEmitter ?? Logging.NullEmitter; } }

            #endregion

            #region Constructors

            /// <summary>
            /// Base class constructor.  
            /// Initializes all internal behavior logic including:
            /// <list type="bullet">
            /// <item>captures handle to distribution engine (caller provided or singleton instance)</item>
            /// <item>captures and configured initial Instance Log Gate</item>
            /// <item>looks up and saves source info for this named logger</item>
            /// <item>Initializes base class LoggerConfigObserver used to handle distributed per logger/group configuration behavior.</item>
            /// <item>Captures and applies custom logger group name assignment.</item>
            /// <item>Sets up optional MyTraceEmitter and emits created and destroyed messages if enabled.</item>
            /// </list>
            /// </summary>
            /// <param name="name">Gives the logger name for this logger instance.  Used to obtain the matching LoggerSourceInfo from LogDistribution.</param>
            /// <param name="groupName">Defines the group name the logger should be included in.  All loggers using the same logger name will be moved to this group ID</param>
            /// <param name="initialInstanceLogGate">Defines the initial value for the logger's InstanceLogGate value.</param>
            /// <param name="traceLoggerCtor">Set to true to cause the logger to define a trace emitter that will be used to emit construction/destruction messages for this logger instance.</param>
            /// <param name="allowUseOfModularConfig">can be given as false to suppress use of modular config key Logging.Loggers.[name].LogGate as source for additional LogGate value.</param>
            /// <param name="callerProvidedLMD">can be used to define the ILogMessageDistributionForLoggers instance that this object will be used with</param>
            public LoggerBase(string name, string groupName, LogGate initialInstanceLogGate, bool traceLoggerCtor = true, bool allowUseOfModularConfig = true, ILogMessageDistributionForLoggers callerProvidedLMD = null)
			{
				dist = callerProvidedLMD ?? LogMessageDistribution.Instance;

                if (dist == null)
				    Utils.Asserts.TakeBreakpointAfterFault("{0}: LogMessageDistribution is null".CheckedFormat(ClassName));

                sourceInfo = dist.GetLoggerSourceInfo(name, allowUseOfModularConfig);

                LoggerConfigObserver = new SequencedLoggerConfigObserver(sourceInfo.LoggerConfigSource);

                if (!groupName.IsNullOrEmpty() && groupName != GroupName)
                    GroupName = groupName;

                if (traceLoggerCtor)
                    myTraceEmitter = new LoggerMesgEmitterImpl() { Logger = this, MesgType = MesgType.Trace, SkipNAdditionalStackFrames = 1 };	// this emitter records the stack frame 1 above its caller, ie our caller

                if (initialInstanceLogGate == LogGate.All)
                {
                    InstanceLogGate = initialInstanceLogGate;

                    MyTraceEmitter.Emit("{0} object has been created", ClassName);
                }
                else
                {
                    MyTraceEmitter.Emit("{0} object has been created with initialLogGate:{1}", ClassName, initialInstanceLogGate);

                    InstanceLogGate = initialInstanceLogGate;
                }
			}

            /// <summary>
            /// Copy constructor for cloning.  Produces a separate instance of a LoggerBase object that is initialized to the same settings as the copy source but which can be used independantly of it.
            /// </summary>
            public LoggerBase(LoggerBase rhs)
                : base(rhs)
			{
				dist = rhs.dist;
				sourceInfo = rhs.sourceInfo;

                explicitlyGivenInstanceLogGate = rhs.explicitlyGivenInstanceLogGate;
                optionallyElevatedLogGate = rhs.optionallyElevatedLogGate;
				myTraceEmitter = rhs.myTraceEmitter;

				MyTraceEmitter.Emit("{0} object has been copied", ClassName);
			}

            #endregion

            #region ILogger implementation

            /// <summary>Returns the Name of this logger object, as given when the logger was constructed.  This will be used as the source string in all log messages that are created by this logger object.</summary>
			public string Name { get { return sourceInfo.Name; } }

            /// <summary>Returns the full LoggerSourceInfo obtained from the log distribution system when the logger was constructed.</summary>
            public LoggerSourceInfo LoggerSourceInfo { get { return sourceInfo; } }

            /// <summary>
            /// Returns the logger's active group name.  
            /// May be set to change the logger's active group name to the name of another distribution group.
            /// Setting this name to null or empty returns the logger to the default distribution group.
            /// Setting this name to a non-configured group in the distribution system will block distribution of the messages it creates
            /// until one or more LMH (LogMessageHandlers) are added to the group or until the group is linked to another group that will handle the messages.
            /// </summary>
            public string GroupName 
			{
				get { return LoggerConfigObserver.GroupName; }
				set
				{
					dist.SetLoggerDistributionGroupName(sourceInfo.ID, value);
					LoggerConfigObserver.Update();
				}
			}

            /// <summary>
            /// Logger instance specific source gate.
            /// May be used to block message types at the source before consulting the corespondingly configured gate for this logger name in the distribution system.
            /// May be elevated using Logging.Logger.[name].LogGate.Increase config key to enable generation of messages above the construction default value for this logger.
            /// </summary>
            public LogGate InstanceLogGate 
            { 
                get 
                {
                    if (LoggerConfigObserver.IsUpdateNeeded)
                    {
                        LoggerConfigObserver.Update();
                        optionallyElevatedLogGate = explicitlyGivenInstanceLogGate | LoggerConfigObserver.LogGateIncrease;
                    }

                    return optionallyElevatedLogGate;
                }
                set 
                { 
                    explicitlyGivenInstanceLogGate = value;

                    LoggerConfigObserver.Update();
                    optionallyElevatedLogGate = explicitlyGivenInstanceLogGate | LoggerConfigObserver.LogGateIncrease;
                } 
            }

            private LogGate explicitlyGivenInstanceLogGate = LogGate.All;
            private LogGate optionallyElevatedLogGate = LogGate.All;

            /// <summary>
            /// Consults the InstanceSourceGate and the LoggerConfig observed from the distribution system to determine if the given message type is currently enabled.
            /// </summary>
            public bool IsTypeEnabled(MesgType mesgType)
			{
                if (!InstanceLogGate.IsTypeEnabled(mesgType) || loggerHasBeenShutdown)
					return false;

				if (!LoggerConfigObserver.IsTypeEnabled(mesgType))
					return false;

				return true;
			}

            /// <summary>returns a new message with type, source, message, file and line filled in (as appropriate and enabled).</summary>
            public virtual LogMessage GetLogMessage(MesgType mesgType, string mesg = null, INamedValueSet nvs = null, byte [] data = null)
            {
                LogMessage lm = null;

                if (!loggerHasBeenShutdown)
                    lm = new LogMessage().Setup(sourceInfo, mesgType, mesg: mesg ?? string.Empty, nvs: nvs, data: data);

                return lm;
            }

            /// <summary>Allocates and returns a non-pooled message of the requested type, and initializes it with the given message string</summary>
            [Obsolete("The sourceStackFrame parameter is no longer supported.  Please use the variant of this method without this parameter.  (2016-12-22, 2017-07-21)")]
            public virtual LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame, int skipNStackFrames = 0) 
            {
                return GetLogMessage(mesgType, mesg);
            }

            /// <summary>
            /// Returns a new message with type, source, message, file and line filled in.  
            /// The obtained message is Setup with the given settings and content and is then returned.
            /// allocateFromDist is now ignored.  All messages are created locally.
            /// <para/>Note: if the loggerHasBeenShutdown then this method will return null.
            /// </summary>
            [Obsolete("The sourceStackFrame and allocateFromDist parameters are no longer supported.  Please use the variant of this method without these parameters.  (2016-12-22, 2017-07-21)")]
            public virtual LogMessage GetLogMessage(MesgType mesgType, string mesg, System.Diagnostics.StackFrame sourceStackFrame, bool allocateFromDist)	
			{
                return GetLogMessage(mesgType, mesg);
			}

            /// <summary>Emits the message.  Takes ownership by setting the caller's reference to null.</summary>
            public virtual void EmitLogMessage(ref LogMessage mesg)
			{
				if (mesg != null && !loggerHasBeenShutdown)
					dist.DistributeMessage(ref mesg);
			}

            /// <summary>Waits for last message emitted by this logger to have been distributed and processed</summary>
            /// <returns>true if distribution of the last message emitted here completed within the given time limit, false otherwise.</returns>
            public virtual bool WaitForDistributionComplete(TimeSpan timeLimit)		
			{
				return dist.WaitForDistributionComplete(sourceInfo.ID, timeLimit);
			}

            /// <summary>shuts down this source and prevents it from allocating or emitting further log messages.</summary>
            public virtual void Shutdown()
			{
				MyTraceEmitter.Emit("{0} object has been shutdown", ClassName);

				InstanceLogGate = LogGate.None;
				loggerHasBeenShutdown = true;
			}

			#endregion

            #region BasicLoggerBase implementation

            /// <summary>Provide implementation for abstract IMesgEmitter creation method as required by BasicLoggerBase</summary>
            protected override IMesgEmitter CreateMesgEmitter(MesgType mesgType)
            {
                return new LoggerMesgEmitterImpl() { Logger = this, MesgType = mesgType, SkipNAdditionalStackFrames = 0 };
            }

            #endregion

            #region protected fields

            /// <summary>Property defined by implementation class to describe the type of class that this logger falls into.  Used in assert and trace messages.</summary>
            protected abstract string ClassName { get; }

            /// <summary>the distribution system to which we belong</summary>
            protected ILogMessageDistributionForLoggers dist = null;

            /// <summary>stores the reference id and name</summary>
            protected LoggerSourceInfo sourceInfo = null;

            /// <summary>Set to true to indicate that the logger has been shutdown</summary>
			protected volatile bool loggerHasBeenShutdown = false;
        
            #endregion

            /// <summary>Debug helper method</summary>
            public override string ToString()
            {
                return Utils.Fcns.CheckedFormat("Logger:{0}", Name);
            }
        }

		#endregion

		//-------------------------------------------------------------------
		#region Logger implementation class

		/// <summary>This class provides the standard basic implementation for use as an ILogger</summary>
		public class Logger : LoggerBase
		{
            /// <summary>Constructor.  Uses given logger name, and group name.  Use LogGate.All and enables instance trace logging</summary>
            /// <param name="name">Provides the LoggerName (source ID) to use for this logger.</param>
            /// <param name="groupName">Provides the GroupName that this logger name will be assigned/moved to</param>
            public Logger(string name, string groupName = "") : base(name, groupName, LogGate.All, traceLoggerCtor: true) { }

            /// <summary>Constructor.  Uses given logger name, and initialInstanceLogGate.  Enables instance trace logging</summary>
            /// <param name="name">Provides the LoggerName (source ID) to use for this logger.</param>
            /// <param name="initialInstanceLogGate">Defines the initial instance group gate that may be more restrictive than the gate assigned to the group or the logger through the distribution system.</param>
            public Logger(string name, LogGate initialInstanceLogGate) : base(name, string.Empty, initialInstanceLogGate, traceLoggerCtor: true) { }

            /// <summary>Constructor.  Uses given logger name, group name, and initialInstanceLogGate.  Use default group name and enables instance trace logging</summary>
            /// <param name="name">Provides the LoggerName (source ID) to use for this logger.</param>
            /// <param name="groupName">Provides the GroupName that this logger name will be assigned/moved to</param>
            /// <param name="initialInstanceLogGate">Defines the initial instance group gate that may be more restrictive than the gate assigned to the group or the logger through the distribution system.</param>
            /// <param name="traceLoggerCtor">Set to true to cause the logger to define a trace emitter that will be used to emit construction/destruction messages for this logger instance.</param>
            /// <param name="callerProvidedLMD">can be used to define the ILogMessageDistributionForLoggers instance that this object will be used with</param>
            /// <param name="allowUseOfModularConfig">can be given as false to suppress use of modular config key Logging.Loggers.[name].LogGate as source for additional LogGate value.</param>
            public Logger(string name, string groupName, LogGate initialInstanceLogGate, bool traceLoggerCtor = true, ILogMessageDistributionForLoggers callerProvidedLMD = null, bool allowUseOfModularConfig = true)
                : base(name, groupName, initialInstanceLogGate, traceLoggerCtor: traceLoggerCtor, allowUseOfModularConfig: allowUseOfModularConfig, callerProvidedLMD: callerProvidedLMD) 
            { }

            /// <summary>Copy constructor.</summary>
            /// <param name="rhs">Gives the Logger instance to make a copy from.</param>
            public Logger(Logger rhs) : base(rhs) { }

            /// <summary>Defines the ClassName value that will be used by the LoggerBase when generating trace messages (if enabled).</summary>
			protected override string ClassName { get { return "Logger"; } }
		}

		#endregion

        #region internal ConfigLogger (Logger for use in Modular.Config that prevents recursive use of config when createing such logger objects

		/// <summary>
        /// This class provides the standard basic implementation for use as an ILogger in Modular.Config portions of the codebase.
        /// This logger type blocks recursive use of Modular.Config when creating a logger for use by Modular.Config.
        /// </summary>
        internal class ConfigLogger : LoggerBase
        {
            /// <summary>Constructor.  Uses given logger name, group name, and initialInstanceLogGate.  Use default group name and enables instance trace logging</summary>
            /// <param name="name">Provides the LoggerName (source ID) to use for this logger.</param>
            /// <param name="groupName">Provides the GroupName that this logger name will be assigned/moved to</param>
            /// <param name="initialInstanceLogGate">Defines the initial instance group gate that may be more restrictive than the gate assigned to the group or the logger through the distribution system.</param>
            public ConfigLogger(string name, string groupName, LogGate initialInstanceLogGate) 
                : base(name, groupName, initialInstanceLogGate, traceLoggerCtor: true, allowUseOfModularConfig: false) 
            { }

            /// <summary>Defines the ClassName value that will be used by the LoggerBase when generating trace messages (if enabled).</summary>
            protected override string ClassName { get { return "ConfigLogger"; } }
        }

        #endregion

        //-------------------------------------------------------------------
		#region Trace helper objects (CtorDisposeTrace, EntryExitTrace, TimerTrace)

        /// <summary>
        /// This class is used to provide a Trace on construction/destruction.  It logs a configurable message on construction and explicit disposal.
        /// Generally this class is used as the base class for the EnterExitTrace and TimerTrace classes.  By default messages use MesgType.Trace.
        /// </summary>
        /// <remarks>
        /// The following Trace object classes were inspired by the use of the TraceLogger class from logger.h in the log4cplus library developed by Tad E. Smith.
        /// In most cases these objects should be used in the context of a using statement such as:
        ///
        ///	using (var ctTrace = new MosaicLib.Logging.CtorDisposeTrace(logger, "TraceID")) { [do stuff] }
        /// </remarks>
        public class CtorDisposeTrace : IDisposable
		{
            /// <summary>
            /// Defines the default message type that will be used for messages emitted by this object.
            /// <para/>currently MesgType.Trace
            /// </summary>
			protected const MesgType defaultMesgType = MesgType.Trace;

            /// <summary>
            /// Defines the default warning message type that will be used for warning messages emitted by this object.
            /// <para/>currently MesgType.Warning
            /// </summary>
			protected const MesgType defaultWarningMesgType = MesgType.Warning;

            /// <summary>Constructor.  Uses default prefix strings.</summary>
            /// <param name="logger">Gives logger instance to use.</param>
            /// <param name="traceID">Gives traceID string to use in resulting emitted messages.  When null this will be replaced with the name of the method that called this one.</param>
            /// <param name="mesgType">Gives the MesgType to use for the emitted messages.</param>
            /// <param name="ctorPrefixStr">Gives the name used for ctor type emitted messages.  Typically used when sub-classing this object for other purposes.</param>
            /// <param name="disposePrefixStr">Gives the name used for dispose type emitted messages.  Typically used when sub-classing this object for other purposes.</param>
            /// <param name="warningMesgType">Gives the Warning MesgType to use for the emitted warning messages (if any).</param>
            /// <param name="setStartTime">When true, requests that the object set its StartTime using SetStartTime.</param>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public CtorDisposeTrace(IBasicLogger logger, string traceID = null, MesgType mesgType = defaultMesgType, string ctorPrefixStr = "Ctor:", string disposePrefixStr = "Dispose:", MesgType warningMesgType = defaultWarningMesgType, bool setStartTime = false)
                : this((logger != null) ? logger.Emitter(mesgType) : null, traceID ?? new System.Diagnostics.StackFrame(1).GetMethod().Name, ctorPrefixStr, disposePrefixStr, (logger != null) ? logger.Emitter(warningMesgType) : null, setStartTime)
            { }

            /// <summary>Full Constructor for use with emitter.</summary>
            /// <param name="emitter">Gives emitter instance to use for normal output.</param>
            /// <param name="traceID">Gives traceID string to use in resulting emitted messages.  When null this will be replaced with the name of the method that called this one.</param>
            /// <param name="ctorPrefixStr">Gives the name used for ctor type emitted messages.  Typically used when sub-classing this object for other purposes.</param>
            /// <param name="disposePrefixStr">Gives the name used for dispose type emitted messages.  Typically used when sub-classing this object for other purposes.</param>
            /// <param name="warningEmitter">Gives emitter instance to use for warning output.</param>
            /// <param name="setStartTime">When true, requests that the object set its StartTime using SetStartTime.</param>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public CtorDisposeTrace(IMesgEmitter emitter, string traceID = null, string ctorPrefixStr = "Ctor:", string disposePrefixStr = "Dispose:", IMesgEmitter warningEmitter = null, bool setStartTime = false)
            {
                traceID = traceID ?? new System.Diagnostics.StackFrame(1).GetMethod().Name;

                mesgEmitter = emitter;
                this.warningEmitter = warningEmitter;

                string ctorStr = String.Concat(ctorPrefixStr.MapNullToEmpty(), traceID);

                if (mesgEmitter != null)
                    mesgEmitter.Emit(ctorStr);

                disposeStr = String.Concat(disposePrefixStr.MapNullToEmpty(), traceID);

                if (setStartTime)
                    SetStartTime();
            }

            /// <summary>Deprecated Constructor</summary>
            [Obsolete("Method signatures which support stack frame traversal counters have been deprecated.  Please switch to use of signatures without stack frame traversal counts. [2017-12-19]")]
            public CtorDisposeTrace(IBasicLogger logger, string traceID, MesgType mesgType, int ctorSkipNStackFrames)
                : this(logger, traceID.MapNullToEmpty(), mesgType) 
            { }

            /// <summary>Deprecated Constructor</summary>
            [Obsolete("Method signatures which support stack frame traversal counters have been deprecated.  Please switch to use of signatures without stack frame traversal counts. [2017-12-19]")]
            public CtorDisposeTrace(IBasicLogger logger, string traceID, MesgType mesgType, string ctorPrefixStr, int ctorSkipNStackFrames, string disposePrefixStr, int disposeSkipNStackFrames)
                : this(logger, traceID.MapNullToEmpty(), mesgType, ctorPrefixStr, disposePrefixStr)
			{ }

            /// <summary>Deprecated Constructor</summary>
            [Obsolete("Method signatures which support stack frame traversal counters have been deprecated.  Please switch to use of signatures without stack frame traversal counts. [2017-12-19]")]
            public CtorDisposeTrace(IMesgEmitter emitter, string traceID, string ctorPrefixStr, int ctorSkipNStackFrames, string disposePrefixStr, int disposeSkipNStackFrames)
                : this(emitter, traceID.MapNullToEmpty(), ctorPrefixStr, disposePrefixStr)
            { }

            /// <summary>
            /// Implementation method for <see cref="IDisposable"/> interface.
            /// </summary>
			public void Dispose()
			{
                try
                {
                    if (disposeStr != null && ((mesgEmitter != null && mesgEmitter.IsEnabled) || (warningEmitter != null && warningEmitter.IsEnabled)))
                    {
                        string timeCountRateStr = String.Empty;
                        double runTime = RunTime;

                        if (!HaveStartTime)
                        {}
                        else if (!HaveCount)
                            timeCountRateStr = Utils.Fcns.CheckedFormat("runTime:{0:f6}", RunTime);
                        else if (runTime == 0.0)
                            timeCountRateStr = Utils.Fcns.CheckedFormat("runTime:{0:f6} count:{1}", RunTime, Count);
                        else
                            timeCountRateStr = Utils.Fcns.CheckedFormat("runTime:{0:f6} count:{1} rate:{2:f3}", RunTime, Count, Count / runTime);

                        bool haveExtraMessage = !String.IsNullOrEmpty(ExtraMessage);
                        string finalDisposeStr;

                        if (!HaveStartTime && !haveExtraMessage)
                            finalDisposeStr = disposeStr;
                        else if (HaveStartTime && !haveExtraMessage)
                            finalDisposeStr = Utils.Fcns.CheckedFormat("{0} [{1}]", disposeStr, timeCountRateStr);
                        else if (!HaveStartTime && haveExtraMessage)
                            finalDisposeStr = Utils.Fcns.CheckedFormat("{0} [{1}]", disposeStr, ExtraMessage);
                        else
                            finalDisposeStr = Utils.Fcns.CheckedFormat("{0} [{1} {2}]", disposeStr, timeCountRateStr, ExtraMessage);

                        if (mesgEmitter != null)
                            mesgEmitter.Emit(finalDisposeStr);
                        else if (warningEmitter != null)
                            warningEmitter.Emit("Unexpected disposal of trace object for mesg:{0}", finalDisposeStr);
                    }
                }
                catch
                { }

                // clear the mesgEmitter and warningEmitter to prevent this method from attempting to log any additional content
				mesgEmitter = warningEmitter = null;
                disposeStr = null;
			}

            /// <summary>Storage for string that will be emitted on dispose</summary>
			protected string disposeStr = null;

            /// <summary>Storage for emitter that was used by ctor and which will be used on dispose.</summary>
            protected IMesgEmitter mesgEmitter;

            /// <summary>Storage for emitter that will be used for unexpected dispose cases.</summary>
            protected IMesgEmitter warningEmitter;

            /// <summary>Property used by derived classes to track start/end timing</summary>
            public double StartTime { get { return startTime; } set { startTime = value; HaveStartTime = true; } }

            /// <summary>Backing field for StartTime property</summary>
            private double startTime = 0.0;

            /// <summary>Method sets the StartTime property to Time.Qpc.TimeNow</summary>
            public void SetStartTime()
            {
                StartTime = Time.Qpc.TimeNow;
            }

            /// <summary>Returns true if the StartTime property has been set</summary>
            public bool HaveStartTime { get; private set; }

            /// <summary>String that may be assigned by the client to include additional information in the [] section of the dispose message that is generated.</summary>
            public string ExtraMessage { get; set; }

            /// <summary>Returns the time in seconds (from QPC) that have elapsed since the SetStartTime method was called.  Returns zero if HaveStartTime is not true (ie the StartTime property has not been set or the SetStartTime method has not been called).</summary>
            public double RunTime
            {
                get
                {
                    return (HaveStartTime ? (Time.Qpc.TimeNow - StartTime) : 0.0);
                }
            }

            /// <summary>
            /// Set by client code, prior to Stop, to define the number of "items" that have been processed between the Start and Stop messages.
            /// Causes Stop message to include the elapsed time, the count, and the rate (if appropriate) in the Stop: messages.
            /// <para/>Count is a double so that the pattern can be used in more rate determination type cases.
            /// </summary>
            public double Count { get { return count; } set { count = value; HaveCount = true; } }

            /// <summary>
            /// Gets set to true if the Count property has been assigned.  False otherwise.
            /// </summary>
            public bool HaveCount { get; private set; }

            /// <summary>Backing store for the Count property</summary>
            private double count = 0.0;
		}

		//-------------------------------------------------------------------

        /// <summary>
        /// This class is generally used as a method entry/exit Trace.  It is based on the CtorDisposeTrace with modified default log message prefixes.
        /// By default messages use CtorDisposeTrace default message type (MesgType.Trace)
        /// <para/>Also supports ExtraMessage property.
        /// </summary>
        /// <remarks>
        ///	using (var eeTrace = new MosaicLib.Logging.EntryExitTrace(logger)) { [do stuff] }
        /// </remarks>
        public class EnterExitTrace : CtorDisposeTrace
		{
            /// <summary>Enter:</summary>
			private const string entryPrefixStr = "Enter:";
            /// <summary>Exit:</summary>
            private const string exitPrefixStr = "Exit:";
            /// <summary>MesgType.Trace</summary>
            private const MesgType defaultEntryExitMesgType = MesgType.Trace;

            /// <summary>constructor.</summary>
            /// <param name="logger">provides logger that will be used to emit Enter(ctor) and Exit(dispose) messages</param>
            /// <param name="traceID">Gives caller provided traceID to generate "Enter:{traceID}" and "Exit:{traceID}" messages for.  When null this will be replaced with the name of the method that called this one.</param>
            /// <param name="mesgType">Gives message type to use for emitted Enter and Leave messages</param>
            /// <param name="warningMesgType">Gives the Warning MesgType to use for the emitted warning messages (if any).</param>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public EnterExitTrace(IBasicLogger logger, string traceID = null, MesgType mesgType = defaultEntryExitMesgType, MesgType warningMesgType = defaultWarningMesgType)
                : base(logger, traceID ?? new System.Diagnostics.StackFrame(1).GetMethod().Name, mesgType, entryPrefixStr, exitPrefixStr, warningMesgType: warningMesgType, setStartTime: true)
            { }

            /// <summary>constructor for use with emitter.</summary>
            /// <param name="emitter">Gives emitter instance to use for normal output.</param>
            /// <param name="traceID">Gives caller provided traceID to generate "Enter:{traceID}" and "Exit:{traceID}" messages for.  When null this will be replaced with the name of the method that called this one.</param>
            /// <param name="warningEmitter">Gives emitter instance to use for warning output.</param>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public EnterExitTrace(IMesgEmitter emitter, string traceID = null, IMesgEmitter warningEmitter = null)
                : base(emitter, traceID ?? new System.Diagnostics.StackFrame(1).GetMethod().Name, entryPrefixStr, exitPrefixStr, warningEmitter: warningEmitter, setStartTime: true)
            { }

            /// <summary>Deprecated Constructor</summary>
            [Obsolete("Method signatures which support stack frame traversal counters have been deprecated.  Please switch to use of signatures without stack frame traversal counts. [2017-12-19]")]
            public EnterExitTrace(IBasicLogger logger, string traceID, MesgType mesgType, int ctorSkipNStackFrames)
                : this(logger, traceID.MapNullToEmpty(), mesgType) 
            { }

            /// <summary>Deprecated Constructor</summary>
            [Obsolete("Method signatures which support stack frame traversal counters have been deprecated.  Please switch to use of signatures without stack frame traversal counts. [2017-12-19]")]
            public EnterExitTrace(IMesgEmitter emitter, string traceID, int ctorSkipNStackFrames)
                : this(emitter, traceID.MapNullToEmpty())
            { }
        }

		//-------------------------------------------------------------------

        /// <summary>
        /// This class is generally used as a section start/stop Trace.  It is based on the CtorDisposeTrace with modified default log message prefixes.
        /// By default start and stop messages use MesgType.Debug.
        /// <para/>Also supports Count and ExtraMessage properties.
        /// </summary>
        /// <remarks>
        ///	using (var tTrace = new MosaicLib.Logging.TimerTrace(logger, "MeasurementName")) { [do stuff] }
        /// </remarks>
        public class TimerTrace : CtorDisposeTrace
		{
            /// <summary>Start:</summary>
            private const string startPrefixStr = "Start:";
            /// <summary>Stop:</summary>
            private const string stopPrefixStr = "Stop:";
            /// <summary>MesgType.Debug</summary>
            private const MesgType defaultStartStopMesgType = MesgType.Debug;

            /// <summary>constructor.</summary>
            /// <param name="logger">provides logger that will be used to emit Start(ctor) and Stop(dispose) messages</param>
            /// <param name="traceID">Gives caller provided traceID to generate "Start:{traceID}" and "Stop:{traceID}" messages for.  When null this will be replaced with the name of the method that called this one.</param>
            /// <param name="mesgType">Gives message type to use for emitted Start and Stop messages</param>
            /// <param name="warningMesgType">Gives the Warning MesgType to use for the emitted warning messages (if any).</param>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public TimerTrace(IBasicLogger logger, string traceID = null, MesgType mesgType = MesgType.Debug, MesgType warningMesgType = defaultWarningMesgType)
                : base(logger, traceID ?? new System.Diagnostics.StackFrame(1).GetMethod().Name, mesgType, startPrefixStr, stopPrefixStr, warningMesgType: warningMesgType, setStartTime: true) 
            { }

            /// <summary>constructor for use with emitter.</summary>
            /// <param name="emitter">Gives emitter instance to use for normal output.</param>
            /// <param name="traceID">Gives caller provided traceID to generate "Start:{traceID}" and "Stop:{traceID}" messages for.</param>
            /// <param name="warningEmitter">Gives emitter instance to use for warning output.</param>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public TimerTrace(IMesgEmitter emitter, string traceID = null, IMesgEmitter warningEmitter = null)
                : base(emitter, traceID ?? new System.Diagnostics.StackFrame(1).GetMethod().Name, startPrefixStr, stopPrefixStr, warningEmitter: warningEmitter, setStartTime: true)
            { }

            /// <summary>Deprecated Constructor</summary>
            [Obsolete("Method signatures which support stack frame traversal counters have been deprecated.  Please switch to use of signatures without stack frame traversal counts. [2017-12-19]")]
            public TimerTrace(IBasicLogger logger, string traceID, MesgType mesgType, int ctorSkipNStackFrames)
				: this(logger, traceID.MapNullToEmpty(), mesgType)
			{ }

            /// <summary>Deprecated Constructor</summary>
            [Obsolete("Method signatures which support stack frame traversal counters have been deprecated.  Please switch to use of signatures without stack frame traversal counts. [2017-12-19]")]
            public TimerTrace(IMesgEmitter emitter, string traceID, int ctorSkipNStackFrames)
                : this(emitter, traceID.MapNullToEmpty())
            { }
		}

		#endregion

		//-------------------------------------------------------------------
	}

    #region Logging related ExtensionMethods

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Emit extension method to be used with IMesgEmitters.  
        /// Allows an IFormattable <paramref name="formattable"/> to be safely emitted through the emitter.  
        /// Internally uses SafeToString and allows the caller to specify the SafeToString optional paramters.
        /// </summary>
        public static void Emit(this Logging.IMesgEmitter emitter, IFormattable formattable, string format = null, IFormatProvider formatProvider = null, string mapNullTo = "", ExceptionFormat caughtExceptionToStringFormat = (ExceptionFormat.TypeAndMessageAndStackTrace))
        {
            if (emitter != null && emitter.IsEnabled)
            {
                emitter.Emit(formattable.SafeToString(format, formatProvider, mapNullTo: mapNullTo, caughtExceptionToStringFormat: caughtExceptionToStringFormat));
            }
        }

        /// <summary>
        /// Provides a method that will attempt to generate a LogGate from a given string resulting from calling ToString on another LogGate.  
        /// Returns true if the original could be reconstructed or false if the string was not recognized or it could not otherwise be decoded.
        /// </summary>
        public static Logging.LogGate TryParse(this string str, Logging.LogGate fallbackValue = default(Logging.LogGate))
        {
            bool ignoreSuccessOut = false;
            return str.TryParse(fallbackValue, out ignoreSuccessOut);
        }

        /// <summary>
        /// Provides a method that will attempt to generate a LogGate from a given string resulting from calling ToString on another LogGate.  
        /// Returns true if the original could be reconstructed or false if the string was not recognized or it could not otherwise be decoded.
        /// </summary>
        public static bool TryParse(this string str, out Logging.LogGate resultOut, Logging.LogGate fallbackValue = default(Logging.LogGate))
        {
            bool success = false;
            resultOut = str.TryParse(fallbackValue, out success);
            return success;
        }

        /// <summary>
        /// Provides a method that will attempt to generate a LogGate from a given string resulting from calling ToString on another LogGate.  
        /// Returns true if the original could be reconstructed or false if the string was not recognized or it could not otherwise be decoded.
        /// </summary>
        public static Logging.LogGate TryParse(this string str, Logging.LogGate fallbackValue, out bool success)
        {
            Logging.LogGate logGate = fallbackValue;

            if (Utils.StringScanner.FindTokenValueByName(str, Logging.LogGate.LogGateNameMap, out logGate))
            {
                success = true;
                return logGate;
            }

            Utils.StringScanner ss = new MosaicLib.Utils.StringScanner(str);
            if (ss.MatchToken("LogGate:", false, false, false, TokenType.ToNextWhiteSpace))
            {
                Logging.MesgTypeMask mtm = logGate.MesgTypeMask;

                if (Logging.MesgTypeMask.TryParse(ss.Rest, out mtm))
                {
                    success = true;
                    return new Logging.LogGate(mtm);
                }
            }

            int mask = 0;
            if (ss.ParseValue(out mask) && ss.IsAtEnd)
            {
                success = true;
                return new Logging.LogGate(new Logging.MesgTypeMask(mask));
            }

            success = false;
            return fallbackValue;
        }

        /// <summary>
        /// Provides a method that will attempt to generate a MesgTypeMask from a given string resulting from calling ToString on another MesgTypeMask.  Returns true if the original could be reconstructed or false if the string was not recognized and was not decidable.
        /// </summary>
        public static Logging.MesgTypeMask TryParse(this string str, Logging.MesgTypeMask fallbackValue = default(Logging.MesgTypeMask))
        {
            bool ignoreSuccessOut = false;
            return str.TryParse(fallbackValue, out ignoreSuccessOut);
        }

        /// <summary>
        /// Provides a method that will attempt to generate a MesgTypeMask from a given string resulting from calling ToString on another MesgTypeMask.  Returns true if the original could be reconstructed or false if the string was not recognized and was not decidable.
        /// </summary>
        public static bool TryParse(this string str, out Logging.MesgTypeMask resultOut, Logging.MesgTypeMask fallbackValue = default(Logging.MesgTypeMask))
        {
            bool success = false;
            resultOut = str.TryParse(fallbackValue, out success);
            return success;
        }

        /// <summary>
        /// Provides a method that will attempt to generate a MesgTypeMask from a given string resulting from calling ToString on another MesgTypeMask.  Returns true if the original could be reconstructed or false if the string was not recognized and was not decidable.
        /// </summary>
        public static Logging.MesgTypeMask TryParse(this string str, Logging.MesgTypeMask fallbackValue, out bool success)
        {
            Utils.StringScanner scan = new MosaicLib.Utils.StringScanner(str);

            string name = scan.ExtractToken(MosaicLib.Utils.TokenType.AlphaNumeric, false);
            if (scan.Char == '+')
            {
                name = name + scan.Char;
                scan.Idx++;
            }

            int maskBits = 0;

            if (Utils.StringScanner.FindTokenValueByName(name, Logging.MesgTypeMask.MesgTypeMaskNameMap, out maskBits) && (scan.IsAtEnd || scan.Char == '['))    // ignore trailing [$hh] if it is present
                success = true;
            else if (name == "Custom" && scan.MatchToken("[$", false, false) && scan.ParseHexValue(out maskBits, 1, 8, false, false, false) && scan.MatchToken("]", false, false) && scan.IsAtEnd)
                success = true;
            else
                success = false;

            return success ? new Logging.MesgTypeMask(maskBits) : fallbackValue;
        }
    }

    #endregion

	//-------------------------------------------------------------------
}

//-------------------------------------------------------------------
