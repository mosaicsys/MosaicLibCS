//-------------------------------------------------------------------
/*! @file SerialPortSpec.cs
 * @brief This file contains a set of definitions and classes that are used to specify the target and serial configuration for various types of serial ports and uses.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2002 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using MosaicLib.Utils;
using MosaicLib.Time;

namespace MosaicLib.SerialIO
{
	//-----------------------------------------------------------------

	public enum PortMode
	{
		// standard asynchronous rs-232 modes
		//	unused wires on ports supporting 4, 6 or 8 are placed
		//	in default/ready state.

		DOSMode_Default = 0,			// use device's default port mode (used for compatibility with DOS modes)
									// typcially a synonym for RS232_4wire.

		RS232_3wire,				// RX, TX
		RS232_4wire,				// RX, TX, RTS (Normally this is the Default mode)
		RS232_5wire,				// RX, TX, RTS, CTS
		RS232_7wire,				// RX, TX, RTS, CTS, DSR, DTR
		RS232_9wire,				// RX, TX, RTS, CTS, DSR, DTR, DCD, RI - not supported

		// standard asynchronous rs-485/422 modes
		//	5 wire is TX+, TX-, RX+, RX-, GND		[tx has optional enable gatting]
		//	3 wire is D+, D-, GND
		RS485_5wire,				// Full duplex - can be used with rs-422 as well
		RS485_3wireMDEcho,			// Multi Drop, Transmitted bytes are also received
		RS485_3wireMDNoEcho,		// Multi Drop, Transmitted bytes are not received

		Fiber_1pair,				// RX, TX: Light on == Space == uart high output on TX line
		Fiber_1pairInv,				// RX, TX: Light on == Mark == uart low output on TX line
	};

	//-----------------------------------------------------------------

	public interface IComPortUartConfig
	{
		double BaudRate { get; }
		int BaudRateInt { get; }
		Parity Parity { get; }
		int DataBits { get; }
		StopBits StopBits { get; }
		PortMode PortMode { get; }
		Handshake Handshake { get; }
	}

	public struct ComPortUartConfig : IComPortUartConfig
	{
		//-------------------------------
		// structure contents

		private bool		fromXml;		// parsed from xml style configuration

		private double		baudRate;		// >= 0.0, nominaly 110.0 .. 115200.0
		private Parity		parity;
		private int			dataBits;
		private StopBits	stopBits;
		private PortMode	portMode;

		//-------------------------------
		// public methods

		public double BaudRate { get { return baudRate; } set { baudRate = value; } }
		public int BaudRateInt { get { return (int) baudRate; } set { baudRate = value; } }
		public Parity Parity { get { return parity; } set { parity = value; } }
		public int DataBits { get { return dataBits;} set { dataBits = value; } }
		public StopBits StopBits { get { return stopBits;} set { stopBits = value; } }
		public PortMode PortMode { get { return portMode;} set { portMode = value; } }
		public Handshake Handshake
		{
			get
			{
				switch (PortMode)
				{
					case PortMode.RS232_4wire:
					case PortMode.RS232_5wire:
					case PortMode.RS232_7wire:
					case PortMode.RS232_9wire:
						return Handshake.RequestToSend;

					case PortMode.DOSMode_Default:
					case PortMode.RS232_3wire:
					case PortMode.RS485_5wire:
					case PortMode.RS485_3wireMDEcho:
					case PortMode.RS485_3wireMDNoEcho:
					case PortMode.Fiber_1pair:
					case PortMode.Fiber_1pairInv:
					default:
						return Handshake.None;
				}
			}
		}

		public ComPortUartConfig(string cfgStr) : this() { ParseString(cfgStr); }

		public bool ParseString(string cfgStr) { StringScanner scanner = new StringScanner(cfgStr); return ParseString(ref scanner); }
		public bool ParseString(ref StringScanner scan)
		{
			bool success = true;

			this = new ComPortUartConfig();	// clear

			scan.SkipOverWhiteSpace();

            if (scan.MatchToken("<UartConfig", false, false))
			{
				fromXml = true;

				bool endFound = false;
				// mode, baud, dataBits, stopBits, parity

				while (!endFound && success && scan.IsIdxValid)
				{
                    if (scan.MatchToken("/>", false, false))
					{
						endFound = true;
						continue;
					}

					if (!(scan.ParseXmlAttribute("Baud", out baudRate) && baudRate >= 0.0)
						&& !scan.ParseXmlAttribute("DataBits", DataBitsCharTokenValueMap, out dataBits)
						&& !scan.ParseXmlAttribute("Mode", PortModeTokenValueMap, out portMode)
						&& !scan.ParseXmlAttribute("Parity", ParityTokenValueMap, out parity)
						&& !scan.ParseXmlAttribute("StopBits", StopBitsCharTokenValueMap, out stopBits)
						)
					{
						success = false;
					}
				}

				if (!endFound)
					success = false;
			}
			else
			{
				// parse as dos mode style
				string token;
				success = scan.ExtractToken(out token) && success;

				string [] tokens = token.Split(DosModeDelimiters);

				int idx = 0;
				string baudStr = (tokens.Length > idx ? tokens[idx++] : string.Empty);
				string parityCharStr = (tokens.Length > idx ? tokens[idx++] : string.Empty);
				string dataBitsCharStr = (tokens.Length > idx ? tokens[idx++] : string.Empty);
				string stopBitsCharStr = (tokens.Length > idx ? tokens[idx++] : string.Empty);
				string modeCharStr = (tokens.Length > idx ? tokens[idx++] : string.Empty);

				success = (tokens.Length >= 4 && tokens.Length <= 5);

				success = Double.TryParse(baudStr, out baudRate) && success;
				success = StringScanner.FindTokenValueByName(parityCharStr, ParityCharTokenValueMap, out parity) && success;
                success = StringScanner.FindTokenValueByName(dataBitsCharStr, DataBitsCharTokenValueMap, out dataBits) && success;
                success = StringScanner.FindTokenValueByName(stopBitsCharStr, StopBitsCharTokenValueMap, out stopBits) && success;
                success = StringScanner.FindTokenValueByName(modeCharStr, PortModeCharTokenValueMap, out portMode) && success;
			}

			scan.SkipOverWhiteSpace();

			return success;
		}

        public string FmtToString() { return FmtToString(fromXml); }
		public string FmtToString(bool xmlStyle)
		{
			if (xmlStyle)
			{
				return Fcns.CheckedFormat("<UartConfig  Baud=\"{0}\" DataBits=\"{1}\" Mode=\"{2}\" Parity=\"{3}\" StopBits=\"{4}\"/>"
										, baudRate
                                        , StringScanner.FindTokenNameByValue(dataBits, DataBitsCharTokenValueMap, "?")
                                        , StringScanner.FindTokenNameByValue(portMode, PortModeCharTokenValueMap, "?")
                                        , StringScanner.FindTokenNameByValue(parity, ParityCharTokenValueMap, "?")
                                        , StringScanner.FindTokenNameByValue(stopBits, StopBitsCharTokenValueMap, "?")
										);
			}
			else if (portMode == PortMode.DOSMode_Default)
			{
				return Fcns.CheckedFormat("{0},{1},{2},{3}"
										, baudRate
                                        , StringScanner.FindTokenNameByValue(parity, ParityTokenValueMap, "?")
                                        , StringScanner.FindTokenNameByValue(dataBits, DataBitsCharTokenValueMap, "?")
                                        , StringScanner.FindTokenNameByValue(stopBits, StopBitsCharTokenValueMap, "?")
										);
			}
			else
			{
				return Fcns.CheckedFormat("{0},{1},{2},{3},{4}"
										, baudRate
                                        , StringScanner.FindTokenNameByValue(parity, ParityTokenValueMap, "?")
                                        , StringScanner.FindTokenNameByValue(dataBits, DataBitsCharTokenValueMap, "?")
                                        , StringScanner.FindTokenNameByValue(stopBits, StopBitsCharTokenValueMap, "?")
                                        , StringScanner.FindTokenNameByValue(portMode, PortModeTokenValueMap, "?")
										);
			}
		}

		public static readonly Dictionary<string, PortMode> PortModeTokenValueMap = new Dictionary<string,PortMode>()
		{
			{"rs232-3wire",			PortMode.RS232_3wire}, 
			{"rs232-4wire",			PortMode.RS232_4wire}, 
			{"rs232-5wire",			PortMode.RS232_5wire},
			{"rs232-7wire",			PortMode.RS232_7wire}, 
			{"rs232-9wire",			PortMode.RS232_9wire},
			{"rs485-5wire",			PortMode.RS485_5wire}, 
			{"rs485-3wireMDEcho",	PortMode.RS485_3wireMDEcho},		// full duplex receiver
			{"rs485-3wireMDNoEcho",	PortMode.RS485_3wireMDNoEcho},		// half duplex receiver
			{"fiber-1pair",			PortMode.Fiber_1pair},
			{"fiber-1pairInv",		PortMode.Fiber_1pairInv},
			{"DOSMode-Default",		PortMode.DOSMode_Default},
		};

		public static readonly Dictionary<string, PortMode> PortModeCharTokenValueMap = new Dictionary<string,PortMode>()
		{
			{"2", PortMode.RS232_3wire}, 
			{"c", PortMode.RS232_5wire},
			{"d", PortMode.RS232_7wire}, 
			{"r", PortMode.RS232_9wire},
			{"4", PortMode.RS485_5wire},
			{"m", PortMode.RS485_3wireMDEcho},		// full duplex rx (multidrop)
			{"h", PortMode.RS485_3wireMDNoEcho},	// half duplex rx
			{"f", PortMode.Fiber_1pair},
			{"g", PortMode.Fiber_1pairInv},
			{"", PortMode.DOSMode_Default},
			{"C", PortMode.RS232_5wire},
			{"D", PortMode.RS232_7wire}, 
			{"R", PortMode.RS232_9wire},
			{"M", PortMode.RS485_3wireMDEcho},
			{"H", PortMode.RS485_3wireMDNoEcho}, 
			{"F", PortMode.Fiber_1pair},
			{"G", PortMode.Fiber_1pairInv},
		};

		public static readonly Dictionary<string, int> DataBitsCharTokenValueMap = new Dictionary<string,int>()
		{
			{"8", 8}, 
			{"7", 7}, 
			{"6", 6}, 
			{"5", 5},
		};

		public static readonly Dictionary<string, StopBits> StopBitsCharTokenValueMap = new Dictionary<string,StopBits>()
		{
			{"0", StopBits.None},
			{"1", StopBits.One}, 
			{"2", StopBits.Two}, 
			{"1.5", StopBits.OnePointFive},			// not really a character!
		};

		public static readonly Dictionary<string, Parity> ParityTokenValueMap = new Dictionary<string,Parity>()
		{
			{"none", Parity.None}, 
			{"odd", Parity.Odd}, 
			{"even", Parity.Even}, 
			{"space", Parity.Space}, 
			{"mark", Parity.Mark}, 
			{"0", Parity.Space},	// UART TX pin level output on space
			{"1", Parity.Mark},		// UART TX pin level output on mark
		};

		public static readonly Dictionary<string, Parity> ParityCharTokenValueMap = new Dictionary<string,Parity>()
		{
			{"n", Parity.None}, 
			{"o", Parity.Odd}, 
			{"e", Parity.Even}, 
			{"s", Parity.Space}, 
			{"m", Parity.Mark}, 
			{"N", Parity.None}, 
			{"O", Parity.Odd}, 
			{"E", Parity.Even}, 
			{"S", Parity.Space},
			{"M", Parity.Mark}, 
			{"0", Parity.Space}, 
			{"1", Parity.Mark}, 
		};

		private static readonly char [] DosModeDelimiters  = new char [] { ',' };
	}
}

//-----------------------------------------------------------------
