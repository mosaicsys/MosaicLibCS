//-------------------------------------------------------------------
/*! @file ComSerialIO.cs
 * @brief This file contains the definitions of the ComPort SerialIO port implementation class.
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

namespace MosaicLib.SerialIO
{
	//-----------------------------------------------------------------

	using System;
	using System.IO.Ports;
	using MosaicLib.Utils;
	using MosaicLib.Time;
	using MosaicLib.Modular.Action;
	using MosaicLib.Modular.Part;

	//-----------------------------------------------------------------
	#region ComPort Factory method

    public static partial class Factory
	{
        /// <summary>Creates a Com type IPort implementation from the given portConfig and comPortConfig</summary>
        /// <param name="portConfig">Provides all configuration details for the port to be created.</param>
        /// <param name="comPortConfig">Provides the ComPort specific configuration information.  Typically derived by parsing the portConfig.SpecStr.</param>
        /// <returns>the created Com type IPort object.</returns>
		public static IPort CreateComPort(PortConfig portConfig, ComPortConfig comPortConfig)
		{
			return new ComPort(portConfig, comPortConfig);
		}
	}

	#endregion

	//-----------------------------------------------------------------
	#region ComPortConfig struct

	/// <summary>This struct is used to parse the specStr for a PortConfig when external code has decided that the PortConfig is a ComPort config</summary>
	/// <remarks>
	/// we support the following two formats
	///		<ComPort port="\\.\com200"><UartConfig baud="9600" DataBits="8" Mode="rs232-3wire" Parity="none" StopBits="1"/></ComPort>
	///		<ComPort port="com1" uartConfig="9600,n,8,1"/>
	/// </remarks>
	public struct ComPortConfig
	{
        /// <summary>Constructor.  Parses the given specStr</summary>
		public ComPortConfig(string specStr) : this() { SpecStr = specStr; ParseSpec(); }

		string portName;
		ComPortUartConfig uartConfig;
		string faultCode;

        /// <summary>getter access to the SpecStr value from which this object was constructed, or null if the default constructor was used.</summary>
        public string SpecStr { get; private set; }
        /// <summary>getter access to the PortName that was parsed from the SpecStr, or null if no valid PortName has been so obtained.</summary>
        public string PortName { get { return portName; } }
        /// <summary>getter access to the ComPortUartConfig that was parsed from the SpecStr, or null if no valid UartConfig has been so obtained.</summary>
        public ComPortUartConfig UartConfig { get { return uartConfig; } }
        /// <summary>True if a valid SpecStr has been given to this object and false otherwise.</summary>
        public bool IsValid { get { return faultCode == string.Empty; } }
        /// <summary>Returns a non-empty description of the issue if IsValid is false.  returns empty string if IsValid is ture.</summary>
        public string ErrorCode { get { return (IsValid ? string.Empty : (!String.IsNullOrEmpty(SpecStr) ? ("ComPortConfig parse failed: " + faultCode) : "No valid SpecStr has been parsed")); } }

		private void ParseSpec()
		{
            faultCode = String.Empty;
			Utils.StringScanner specScanner = new StringScanner(SpecStr);

            if (IsValid && !specScanner.MatchToken("<ComPort", false, false))
				faultCode = Utils.Fcns.CheckedFormat("invalid element name in '{0}' at idx {1}", specScanner.Str, specScanner.Idx);

			if (IsValid && !specScanner.ParseXmlAttribute("port", out portName))
				faultCode = Utils.Fcns.CheckedFormat("missing port attribute in '{0}' at idx {1}", specScanner.Str, specScanner.Idx);

            if (IsValid && specScanner.MatchToken(">", true, false))
			{
				Utils.StringScanner copy = specScanner;

				if (IsValid && !uartConfig.ParseString(ref specScanner))
					faultCode = Utils.Fcns.CheckedFormat("unable to parse expected UartConfig element in '{0}' at idx {1}", copy.Str, copy.Idx);

                if (IsValid && !specScanner.MatchToken("</ComPort>", true, false))
					faultCode = Utils.Fcns.CheckedFormat("missing element close '</ComPort>' in '{0}' at idx {1}", specScanner.Str, specScanner.Idx);
			}
			else
			{
				string uartConfigStr = string.Empty;

				if (IsValid && !specScanner.ParseXmlAttribute("uartConfig", out uartConfigStr))
					faultCode = Utils.Fcns.CheckedFormat("missing expected config attribute in '{0}' at idx {1}", specScanner.Str, specScanner.Idx);

				if (IsValid && !uartConfig.ParseString(uartConfigStr))
					faultCode = Utils.Fcns.CheckedFormat("uartConfig attribute could not be parsed from '{0}'", uartConfigStr);

                if (IsValid && !specScanner.MatchToken("/>", true, false))
					faultCode = Utils.Fcns.CheckedFormat("missing element close '/>' in '{0}' at idx {1}", specScanner.Str, specScanner.Idx);
			}
		}
	}

	#endregion

	//-----------------------------------------------------------------
	#region ComPort class

    /// <summary>Provides an implementation of the SerialIO PortBase class for use as a traditional Com type serial port (using RS-232, RS-422, or RS-485).</summary>
    internal class ComPort : PortBase
	{
		#region CTor, DTor

		public ComPort(PortConfig portConfig, ComPortConfig comPortConfig)
			: base(portConfig, "ComPort")
		{
			this.comPortConfig = comPortConfig;
            PortBehavior = new PortBehaviorStorage() { DataDeliveryBehavior = DataDeliveryBehavior.ByteStream, IsClientPort = true };

			PrivateBaseState = new BaseState(false, true);
			PublishBaseState("object constructed");

			CreatePort();
		}

		void CreatePort()
		{
			ComPortUartConfig uartConfig = comPortConfig.UartConfig;

			sp = new System.IO.Ports.SerialPort();
			// sp.PortName = comPortConfig.PortName;        // do this when opening the port
			sp.BaudRate = uartConfig.BaudRateInt;
			sp.DataBits = uartConfig.DataBits;
			sp.Handshake = uartConfig.Handshake;
			sp.Parity = uartConfig.Parity;
			sp.StopBits = uartConfig.StopBits;
			sp.RtsEnable = uartConfig.Handshake == System.IO.Ports.Handshake.None;
			sp.DtrEnable = true;

			sp.ReadTimeout = 0;		// read operations are non-blocking at this level
			sp.WriteTimeout = 100;	// write operations are blocking at this level - we assume that there will always be enough buffer space to accept the bytes relatively quickly!

			// sp.Encoding;		// accept default of Ascii
			sp.ReadBufferSize = (int) PortConfig.RxBufferSize;
			sp.WriteBufferSize = (int) PortConfig.TxBufferSize;

			sp.ReceivedBytesThreshold = 1;	// fire event after 1 character

			sp.PinChanged += delegate(object sender, SerialPinChangedEventArgs e) { TraceData.Emit("<PinChangedEvent type='{0}'/>", e.EventType); threadWakeupNotifier.Notify(); };
			sp.ErrorReceived += delegate(object sender, SerialErrorReceivedEventArgs e) { TraceData.Emit("<ErrorReceivedEvent type='{0}'/>", e.EventType); threadWakeupNotifier.Notify(); };
			sp.DataReceived += delegate(object sender, SerialDataReceivedEventArgs e) { threadWakeupNotifier.Notify(); };
		}

		protected override void DisposeCalledPassdown(DisposeType disposeType)		// this is called after StopPart has completed during dispose
		{
			base.DisposeCalledPassdown(disposeType);

			if (disposeType == DisposeType.CalledExplicitly)
				MosaicLib.Utils.Fcns.DisposeOfObject(ref sp);
		}

		#endregion

		#region private and protected fields, properties and methods

		ComPortConfig comPortConfig;
		System.IO.Ports.SerialPort sp = null;

		#endregion

		protected override string InnerPerformGoOnlineAction(string actionName, bool andInitialize)
		{
			string faultCode = null;

			try
			{
				if (sp == null)
					CreatePort();

				if (sp.IsOpen && andInitialize)
				{
					sp.Close();
					SetBaseState(ConnState.Disconnected, actionName + ".Inner.DoInit", true);
				}

				SetBaseState(ConnState.Connecting, actionName + ".Inner.DoOpen", true);

                if (!sp.IsOpen)
                {
                    sp.PortName = comPortConfig.PortName;
                    sp.Open();
                }
			}
			catch (System.Exception ex)
			{
				faultCode = "Exception:" + ex.Message;
			}

			if (string.IsNullOrEmpty(faultCode))
			{
				SetBaseState(ConnState.Connected, actionName + ".Inner.Done", true);
				return string.Empty;
			}
			else
			{
				SetBaseState(ConnState.ConnectFailed, actionName + ".Inner.Failed", true);
				return faultCode;
			}
		}

		protected override string InnerPerformGoOfflineAction(string actionName)
		{
			string faultCode = null;

			try
			{
				if (InnerIsConnected)
					sp.Close();
			}
			catch (System.Exception ex)
			{
				faultCode = "Exception:" + ex.Message;
			}

			if (string.IsNullOrEmpty(faultCode))
			{
				SetBaseState(ConnState.Disconnected, actionName + ".Inner.Done", true);
				return string.Empty;
			}
			else
			{
				SetBaseState(ConnState.ConnectionFailed, actionName + ".Inner.Failed", true);
				return faultCode;
			}
		}

		protected override int InnerReadBytesAvailable
		{
			get
			{
                if (!InnerIsConnected)
                    return 0;

                try
                {
                    return sp.BytesToRead;
                }
                catch
                {
                    return 1;       // cause caller to attempt to read this byte and thus have the read fail.
                }
			}
		}

		protected override int InnerWriteSpaceUsed 
		{
			get
			{
				if (!InnerIsConnected)
					return 0;

				return sp.BytesToWrite;
			}
		}

		protected override int InnerWriteSpaceAvailable
		{
			get
			{
				if (!InnerIsConnected)
					return 0;

				return (sp.WriteBufferSize - sp.BytesToWrite);
			}
		}

        protected override string InnerHandleRead(byte[] buffer, int startIdx, int maxCount, out int didCount, ref ActionResultEnum readResult)
		{
			didCount = 0;

			if (!InnerIsConnected)
				return "InnerHandleRead failed: serial port is null or is not open";

            try
            {
                didCount = sp.Read(buffer, startIdx, maxCount);
                return string.Empty;
            }
            catch (System.TimeoutException e)
            {
                Log.Trace.Emit("TimeoutException:{0}", e);
                return string.Empty;
            }
            catch (System.Exception ex)
            {
                return "Exception:" + ex.Message;
            }
		}

        protected override string InnerHandleWrite(byte[] buffer, int startIdx, int count, out int didCount, ref ActionResultEnum writeResult)
		{
			didCount = 0;

			if (!InnerIsConnected)
				return "InnerHandleWrite failed: serial port is null or is not open";

			try
			{
				int maxCount = Math.Min(count, InnerWriteSpaceAvailable);

				sp.Write(buffer, startIdx, maxCount);

				didCount = maxCount;

				return string.Empty;
			}
			catch (System.Exception ex)
			{
				return "Exception:" + ex.Message;
			}
		}

        /// <summary>
        /// Returns true if a Serial Port is defined and it reports that it is IsOpen
        /// </summary>
		protected override bool InnerIsConnected
		{
			get
			{
				bool isConnected = (sp != null && sp.IsOpen);
				return isConnected;
			}
		}
	}

	#endregion

	//-----------------------------------------------------------------
}
