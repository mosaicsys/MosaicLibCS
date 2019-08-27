//-------------------------------------------------------------------
/*! @file E005Port.cs
 *  @brief This file defines common types, constants, and methods that are used with semi E005 Ports.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
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

using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.Attributes;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Semi.E005.Port
{
	//-------------------------------------------------------------------
	#region IPort, PortType, ISendMessageAction

    public interface IPort : Modular.Part.IActivePartBase
    {
        /// <summary>Gives the port number.  Assigned by the manager to which this port belongs.</summary>
        int PortNum { get; }

        /// <summary>Gives this port's PortType</summary>
        PortType PortType { get; }

        /// <summary>Returns the port's configuration parameters as assigned during port creation</summary>
        INamedValueSet PortConfigNVS { get; }

        /// <summary>Creates a message that will be sent to this port</summary>
        IMessage CreateMessage(StreamFunction sf);

        /// <summary>Action factory method.  When run, the resulting action will send a message using this port.  This action may be reused.  Note the message's Port must match this port or must be null.</summary>
        ISendMessageAction SendMessage(IMessage mesg = null);

        /// <summary>Returns a sequence number to use as DATAID values in client code.  Will be unique accross all of the ports that make use of this port's manager instance.</summary>
        UInt32 GetNextDATAID();
    }

    /// <summary>
    /// This enum defines the known types of E005 Port connections
    /// <para/>None (0), E004_Master, E005_Slave, E037_SS_Active, E037_SS_Passive
    /// </summary>
    public enum PortType
    {
        /// <summary>default value used when there is no connection</summary>
        None = 0,

        /// <summary>E004 SECS-I as Equipment</summary>
        [Obsolete("This PortType has not been fully implemented or tested yet")]
        E004_Master,

        /// <summary>E004 SECS-I as Host</summary>
        [Obsolete("This PortType has not been fully implemented or tested yet")]
        E004_Slave,

        /// <summary>Active E037 HSMS-SS initiator</summary>
        E037_Active_SingleSession,

        /// <summary>Passive E037 HSMS-SS target</summary>
        /// <remarks>NOTE: testing for this port type has not been started yet</remarks>
        E037_Passive_SingleSession,
    }

    /// <summary>
    /// Defines the variant of IClientFacet that is returned by the IPort.SendMessage action factory method
    /// </summary>
    public interface ISendMessageAction : Modular.Action.IClientFacetWithParam<IMessage>
    { }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Variant of IPort.CreateMessage that accepts a string represenation of the desired StreamFunction and parses it to get the desired value which is then passed to the corresponding IPort method.  
        /// </summary>
        public static IMessage CreateMessage(this IPort port, string streamFunctionStr)
        {
            return port.CreateMessage(streamFunctionStr.ParseAsStreamFunction());
        }
    }

	#endregion

    //-------------------------------------------------------------------
    #region PortBaseConfig (et. al.)

    /// <summary>This class contains the fields and properties that are used to configured the operation of an E005 Port at the PortBase level.</summary>
    public class PortBaseConfig
    {
        /// <summary>
        /// Defines unsigned short E004 SECS-I DeviceID (0..32767) of connection for equipment, or of intended equipment for host.
        /// <para/>0..32767 - of conn for equipment, of intended device for host, used as sessionID for HSMS connections.
        /// [Defaults to 0]
        /// </summary>
        [NamedValueSetItem]
        public UInt16 DeviceID = 0;

        /// <summary>Sets the delay before attempting an automatic reconnect.  Set this to null to turn off auto reconnect.  [Defaults to 5.0 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan ? AutoReconnectHoldoff = (5.0).FromSeconds();

        public bool AutoReconnectIsEnabled { get { return AutoReconnectHoldoff != null; } }

        /// <summary>Gives the maximum amount of time that the port is allowed to attempt to gracefully disconnet from the remote end when doing a graceful close (GoOffline)  [Defaults to 5.0 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan DisconnectTimeLimit = (5.0).FromSeconds();

        /// <summary>Defines T3 timer value: Reply Timeout.  Used for E004 and E037 ports.  Defines the maximum period that a requesting entity must wait for a response before identifying the response as missing.  [Defaults to 45.0 seconds]</summary>
        [NamedValueSetItem]
        public TimeSpan T3_ReplyTO = TimeSpan.FromSeconds(45.0);

        ///// <summary>Set this to enable transmit throttling</summary>
        //public bool ThrottleEnable = false;
        ///// <summary>Set this to limit the number of blocks that can be sent per throttle limit period</summary>
        //public UInt32 ThrottleMaxBlocksPerLimitPeriod = 100;
        ///// <summary>Set this to a non-zero period to define the limit period for which the </summary>
        //public TimeSpan ThrottleLimitPeriodDuration = TimeSpan.FromSeconds(1.0);

        ///// <summary>Property is true if Throttling is enabled and is usable</summary>
        //public bool IsThrottlingEnabled { get { return (ThrottleEnable && (ThrottleLimitPeriodDuration != TimeSpan.Zero) && (ThrottleMaxBlocksPerLimitPeriod != 0)); } }

        /// <summary>Defines the maximum E005 SECS-II Message Body size that we can accept.</summary>
        [NamedValueSetItem]
        public UInt32 MaximumMesgBodySize = 1 * 1024 * 1024;

        /// <summary>Limits the maximum number of pending messages that may be posted for transmission on a given E005 Port</summary>
        [NamedValueSetItem]
        public UInt32 MaximumSendQueueSize = 256;

        [NamedValueSetItem]
        public Logging.MesgType HeaderTraceMesgType = Logging.MesgType.Trace;

        [NamedValueSetItem]
        public Logging.MesgType MesgTraceMesgType = Logging.MesgType.Trace;

        public PortBaseConfig UpdateFromNVS(INamedValueSet nvs, string keyPrefix = "", Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter valueNoteEmitter = null)
        {
            NamedValueSetAdapter<PortBaseConfig> adapter = new NamedValueSetAdapter<PortBaseConfig>() { ValueSet = this, IssueEmitter = issueEmitter, ValueNoteEmitter = valueNoteEmitter }.Setup(keyPrefix).Set(nvs, merge: true);

            return this;
        }
    }

	#endregion

	//-------------------------------------------------------------------
	#region PortBase, PortConnectionState

    /// <summary>
    /// Port connection state.
    /// <para/>Initial (0), OutOfService, NotConnected, Connecting, Connected, Selecting, Selected, Deselecting, Failed,
    /// </summary>
    public enum PortConnectionState
    {
        /// <summary>Constructor default state.</summary>
        Initial = 0,

        /// <summary>All: port is not accepting connections and/or is not attempting to connect</summary>
        OutOfService,

        /// <summary>All: Port is accepting but has not been connected to, or is not connected and is waiting to be able to attempt to connect again.</summary>
        NotConnected,

        /// <summary>Passive: Port has a connection but has not been selected</summary>
        NotSelected,

        /// <summary>Active, Master: Port is attempting to connect to the other side</summary>
        Connecting,

        /// <summary>Active, Master: Port is attempting to select/initiate a conversation with the other side (as appropriate)</summary>
        Selecting,

        /// <summary>All: Port is connected and selected.</summary>
        Selected,

        /// <summary>Active, Passive: Port is connected and is attempting to deselect the other side (as appropriate).</summary>
        Deselecting,

        /// <summary>All: Connect/select attempt failed or connection failed while in use</summary>
        Failed,
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given PortConnectionState is Selected</summary>
        public static bool IsSelected(this PortConnectionState state) 
        {
            return (state == PortConnectionState.Selected);
        }

        /// <summary>Returns true if the PortConnectionState is NotSelected, Selecting, Selected or Deselecting.</summary>
        public static bool IsConnected(this PortConnectionState state)
        {
            switch (state)
            {
                case PortConnectionState.NotSelected:
                case PortConnectionState.Selecting:
                case PortConnectionState.Selected:
                case PortConnectionState.Deselecting:
                    return true;
                default:
                    return false;
            }
        }
    }

	/// <summary>This is the base class for implementation of all E005.Port derived classes including those for E004 and E037</summary>
	/// <remarks>
	/// This class provides common implementation for common aspects of all E005.Port type objects.  Derived types must implement the details
	/// of their own communications, connection state handling and mesg encode/send and recieve/decode processing.
	/// </remarks>

	public abstract class PortBase : SimpleActivePartBase, IPort
	{
		#region Constructor and related fields

        public PortBase(string partID, int portNum, PortType portType, INamedValueSet portConfigNVS, Manager.IManagerPortFacet managerPortFacet, bool enableQueue = false)
            : base(partID, portType.ToString(), enableQueue: enableQueue, queueSize: portConfigNVS["PartQueueSize"].VC.GetValue<int?>(rethrow: false) ?? 10, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior: true, disablePartBaseIVIUse: !portConfigNVS.Contains("EnablePartIVIUse"), goOnlineAndOfflineHandling: GoOnlineAndGoOfflineHandling.SupportMappedServiceActions))
		{
			PortNum = portNum;
            PortType = portType;
            PortConfigNVS = PortConfigNVS.ConvertToReadOnly();
			ManagerPortFacet = managerPortFacet;

            PortBaseConfig = new Port.PortBaseConfig().UpdateFromNVS(portConfigNVS, issueEmitter: Log.Debug, valueNoteEmitter: Log.Trace);

            var doneMesgType = portConfigNVS["DoneMesgType"].VC.GetValue<Logging.MesgType?>(rethrow: false) ?? Logging.MesgType.Trace;
            var errorMesgType = portConfigNVS["ErrorMesgType"].VC.GetValue<Logging.MesgType?>(rethrow: false) ?? Logging.MesgType.Debug;
            var stateMesgType = portConfigNVS["StateMesgType"].VC.GetValue<Logging.MesgType?>(rethrow: false) ?? Logging.MesgType.None;
            var updateMesgType = portConfigNVS["UpdateMesgType"].VC.GetValue<Logging.MesgType?>(rethrow: false) ?? Logging.MesgType.Trace;

            ActionLoggingReference.Config = new ActionLoggingConfig(doneMesgType: doneMesgType, errorMesgType: errorMesgType, stateMesgType: stateMesgType, updateMesgType: updateMesgType, actionLoggingStyleSelect: ActionLoggingStyleSelect.IncludeRunTimeOnCompletion);

            TraceLogger = new Logging.Logger(PartID + ".Trace", Logging.LookupDistributionGroupName);

            TraceHeaders = TraceLogger.Emitter(PortBaseConfig.HeaderTraceMesgType);
            TraceMesgs = TraceLogger.Emitter(PortBaseConfig.MesgTraceMesgType);

            SetConnectionState(PortConnectionState.Initial, CurrentMethodName, QpcTimeStamp.Now);
		}

        protected Manager.IManagerPortFacet ManagerPortFacet { get; private set; }

        public int PortNum { get; private set; }
        public PortType PortType { get; private set; }
        public INamedValueSet PortConfigNVS { get; private set; }

        public PortBaseConfig PortBaseConfig { get; private set; }

        protected Logging.ILogger TraceLogger { get; private set; }

        public Logging.IMesgEmitter TraceMesgs = Logging.NullEmitter;

        public Logging.IMesgEmitter TraceHeaders = Logging.NullEmitter;

        #endregion

		#region IPort Members (ones not covered above)

        IMessage IPort.CreateMessage(StreamFunction sf)
        {
            return new Message(sf, this);
        }

        protected class SendMessageActionImpl : ActionImplBase<IMessage, NullObj>, ISendMessageAction
        {
            public SendMessageActionImpl(ActionQueue actionQ, IMessage paramValue, ActionMethodDelegateActionArgStrResult<IMessage, NullObj> method, ActionLogging loggingReference, string mesg)
                : base(actionQ, paramValue, false, method, loggingReference, mesg, mesgDetails: "{0}".CheckedFormat(paramValue)) 
            { }
        }

		ISendMessageAction IPort.SendMessage(IMessage mesg)
		{
			// create the action anyways - it will fail when run
            Modular.Action.ActionMethodDelegateActionArgStrResult<IMessage, NullObj> method = PerformSendMessageAction;
            ISendMessageAction action = new SendMessageActionImpl(actionQ, mesg, method, ActionLoggingReference, "SendMessage");

			return action;
		}

        protected string PerformSendMessageAction(IProviderActionBase<IMessage, NullObj> action)
        {
            return PerformSendMessageAction(action.ParamValue, action);
        }

        protected string PerformSendMessageAction(IMessage mesg, IProviderFacet ipf)
        {
            string ec = null;

            if (mesg == null && ec.IsNullOrEmpty())
                ec = "There is no message to send";

            if (mesg.SeqNum == 0)
                mesg.SeqNum = ManagerPortFacet.GetNextMessageSequenceNum();

            if (mesg != null & mesg.Port == null)
                mesg.Port = this;

            if (ec.IsNullOrEmpty())
            {
                if (mesg.Port != this)
                    ec = "Message given to wrong port";
                else if (mesg.Reply != null)
                    ec = "Message already has an assigned reply";
                else if (mesg.ContentBytes.SafeCount() > PortBaseConfig.MaximumMesgBodySize)
                    ec = "Message is larger than maximum supported size:{0}".CheckedFormat(PortBaseConfig.MaximumMesgBodySize);
                else if (!PortConnectionState.IsConnected())
                    ec = "Port is not connected";
                else if (pendingSendOpsDictionary.Count >= PortBaseConfig.MaximumSendQueueSize)
                    ec = "Port send message queue is full";
            }

            // message is ready to be enqueued - make certain it contains the correct type of ten byte header, and then post it to be sent.
            if (ec.IsNullOrEmpty())
            {
                PrepareTenByteHeader(mesg);

                var sendMessageOp = new SendMessageOp(ipf, mesg);
                pendingSendOpsDictionary[mesg.TenByteHeader.SystemBytes] = sendMessageOp;
                readyToSendOpQueue.Enqueue(sendMessageOp);

                ServiceTransmitter(QpcTimeStamp.Now); // give the implementation object a chance to service the SendOpsQueue right now.

                ec = null;      // completion will be indicated elsewhere.
            }

            return ec;
        }

        UInt32 IPort.GetNextDATAID()
        {
            return ManagerPortFacet.GetNextDATAID();
        }

        #endregion

		#region Common Send related code

        protected IDictionaryWithCachedArrays<UInt32, SendMessageOp> pendingSendOpsDictionary = new IDictionaryWithCachedArrays<UInt32, SendMessageOp>();
        protected Queue<SendMessageOp> readyToSendOpQueue = new Queue<SendMessageOp>();

		protected class SendMessageOp
		{
			public SendMessageOp(IProviderFacet ipf, IMessage mesg) 
            { 
                IPF = ipf; 
                Mesg = mesg; 
            }

            public SendMessageOp UpdateNamedValues(INamedValueSet nvs)
            {
                if (IPF != null)
                    IPF.UpdateNamedValues(nvs);

                return this;
            }

            public SendMessageOp CompleteRequest(string resultCode)
            {
                if (IPF != null)
                    IPF.CompleteRequest(resultCode.MapNullToEmpty());

                return this;
            }

            public IProviderFacet IPF { get; private set; }
            public IMessage Mesg { get; private set; }
            public QpcTimeStamp SendPostedTimeStamp { get; set; }
            public UInt32 SystemBytes { get { return (Mesg != null ? Mesg.TenByteHeader.SystemBytes : 0); } }
            public bool IsReplyExpected { get { return Mesg.SF.ReplyExpected; } }
		}

        protected void NoteMesgSendPosted(SendMessageOp smo, QpcTimeStamp qpcTimeStamp)
        {
            if (smo.IsReplyExpected)
                smo.SendPostedTimeStamp = qpcTimeStamp;
        }

        protected void NoteMesgSent(SendMessageOp smo, QpcTimeStamp qpcTimeStamp)
        {
            if (smo.IsReplyExpected)
                smo.SendPostedTimeStamp = qpcTimeStamp;
            else
                CompleteMessageAndRemoveFromPendingSet(smo, string.Empty);
        }

        protected void NoteReplyReceived(SendMessageOp smo, IMessage replyMesg, string ec = null)
        {
            if (smo.IsReplyExpected)
            {
                smo.Mesg.SetReply(replyMesg);
                CompleteMessageAndRemoveFromPendingSet(smo.UpdateNamedValues(new NamedValueSet() { { "Reply", replyMesg } }), ec.MapNullToEmpty());
            }
            else if (ec.IsNeitherNullNorEmpty())
            {
                CompleteMessageAndRemoveFromPendingSet(smo, "Received fault reply [{0}] for mesg [{1}] ec:{2}".CheckedFormat(replyMesg, smo.Mesg, ec));
            }
            else
            {
                CompleteMessageAndRemoveFromPendingSet(smo, "Received unexpected reply [{0}] for mesg [{1}]".CheckedFormat(replyMesg, smo.Mesg));
            }
        }

        protected void CompleteMessageAndRemoveFromPendingSet(SendMessageOp smo, string resultCode)
        {
            pendingSendOpsDictionary.Remove(smo.SystemBytes);

            smo.CompleteRequest(resultCode);
        }

		void CancelPendingSendMessageOps(string reason)
		{
            PerformMainLoopService();

            if (pendingSendOpsDictionary.Count == 0)
				return;

			string ec = "SendCanceled: " + reason;

            foreach (SendMessageOp smo in pendingSendOpsDictionary.ValueArray)
				CompleteMessageAndRemoveFromPendingSet(smo, ec);
		}

		void ServiceSendMessageOpQueue(QpcTimeStamp qpcTimeStamp, bool verifyPortConnectionStateIsSelected = false)
		{
            foreach (var smo in pendingSendOpsDictionary.ValueArray)
			{
                if (smo.IsReplyExpected)
                {
                    TimeSpan elapsedTimeSincePosted = smo.SendPostedTimeStamp.Age(qpcTimeStamp);
                    if (elapsedTimeSincePosted > PortBaseConfig.T3_ReplyTO)
                    {
                        string ec = "SendFailed: reply not received after {0:f3} sec, [T3_ReplyTimeout:{1:f3} secs]".CheckedFormat(elapsedTimeSincePosted.TotalSeconds, PortBaseConfig.T3_ReplyTO.TotalSeconds);
                        CompleteMessageAndRemoveFromPendingSet(smo, ec);
                    }
                }
			}

            if (verifyPortConnectionStateIsSelected && !PortConnectionState.IsSelected())
                CancelPendingSendMessageOps("PortConnectionState is no long functional [{0}]".CheckedFormat(PortConnectionState));
		}

        protected string HandleReceivedMessage(IMessage mesg)
        {
            TraceMesgs.Emit("Received Mesg {0}", mesg);

            StreamFunction sf = mesg.SF;
            UInt32 systemBytes = mesg.TenByteHeader.SystemBytes;

            if (sf.StreamByte == 9 || sf.FunctionByte == 0)
            {
                // error cases

                string faultDescription = null;
                string mesgBodyStr = mesg.GetDecodedContents(throwOnException: false).ToStringSML();
                UInt32 faultSystemBytes = systemBytes;

                if (sf.StreamByte == 9)
                {
                    var mheadVC = mesg.GetDecodedContents(throwOnException: false);
                    var mheadByteArray = (mheadVC.GetValue<BiArray>(rethrow: false) ?? BiArray.Empty).SafeToArray();

                    var mheadTBH = new E037.E037TenByteHeader();
                    if (mheadTBH.Decode(mheadByteArray, 0))
                    {
                        faultSystemBytes = ((ITenByteHeader)mheadTBH).SystemBytes;
                        mesgBodyStr = "[MHEAD: {0}]".CheckedFormat(mheadTBH);
                    }

                    switch (sf.FunctionByte)
                    {
                        case 0: faultDescription = "Abort Transaction"; break;
                        case 1: faultDescription = "UDN - Unrecognized DeviceID"; break;
                        case 3: faultDescription = "USN - Unrecognized Stream"; break;
                        case 5: faultDescription = "UFN - Unrecognized Function"; break;
                        case 7: faultDescription = "UDN - Unrecognized Data"; break;
                        case 9: faultDescription = "TTN - Transaction Timer Timeout"; break;
                        case 11: faultDescription = "DLN - Data Too Long"; break;
                        case 13: faultDescription = "CTN - Conversation Timeout"; break;
                        default: break;
                    }
                }
                else if (sf.FunctionByte == 0)
                {
                    faultDescription = "Abort Transaction";
                }

                if (faultDescription == null)
                    faultDescription = "Internal: unrecognized fault type";

                string ec = "{0}: {1} {2}".CheckedFormat(sf, faultDescription, mesgBodyStr);

                SendMessageOp smo = pendingSendOpsDictionary.SafeTryGetValue(faultSystemBytes);

                if (smo != null)
                    NoteReplyReceived(smo, mesg, ec);
                else
                    Log.Debug.Emit("Unexpected or late fault reply message received: tbh:{0}, ec:{1}", mesg.TenByteHeader, ec);
            }
            else if (sf.IsPrimary)
            {
                ManagerPortFacet.PrimaryMesgReceivedFromPort(mesg);

                if (mesg.Reply != null)
                {
                    TraceMesgs.Emit("Sending message handler generated reply to Mesg {0}: {1}", mesg.TenByteHeader, mesg.Reply);
                    PerformSendMessageAction(mesg.Reply, null);
                }
                else if (mesg.SF.ReplyExpected)
                {
                    TraceMesgs.Emit("Message handler did not generate inline reply to Mesg {0}", mesg.TenByteHeader);
                }
            }
            else
            {
                SendMessageOp smo = pendingSendOpsDictionary.SafeTryGetValue(systemBytes);

                if (smo != null)
                    NoteReplyReceived(smo, mesg);
                else
                    Log.Debug.Emit("Unexpected or late reply message received: tbh:{0}", mesg.TenByteHeader);
            }

            return String.Empty;
        }

		#endregion

		#region SimpleActivePartBase override methods

        protected QpcTimer connectionServiceTimer = new QpcTimer() { TriggerIntervalInSec = 0.10, AutoReset = true, Started = true };
        protected QpcTimer mesgQueueServiceTimer = new QpcTimer() { TriggerIntervalInSec = 0.20, AutoReset = true, Started = true };

        /// <summary>
        /// E005Port level method.  Services Receiver and Transmitter.  
        /// Occasionally services the connection state and the Send Message Op Queue (using connectionServiceTimer and the mesgQueueServiceTimer respectively).
        /// Calls base class (empty) PeformMainLoopService.
        /// </summary>
        protected override void PerformMainLoopService()
		{
            QpcTimeStamp qpcTimeStamp = QpcTimeStamp.Now;

            ServiceReceiver(qpcTimeStamp);
            ServiceTransmitter(qpcTimeStamp);

            if (connectionServiceTimer.GetIsTriggered(qpcTimeStamp))
                ServiceConnection(qpcTimeStamp);

            if (mesgQueueServiceTimer.GetIsTriggered(qpcTimeStamp))
                ServiceSendMessageOpQueue(qpcTimeStamp, verifyPortConnectionStateIsSelected: true);

            base.PerformMainLoopService();
		}

		#endregion

		#region Connection state and management

        public PortConnectionState PortConnectionState { get; private set; }
        public QpcTimeStamp PortConnectionStateTimeStamp { get; private set; }


		protected virtual void SetConnectionState(PortConnectionState state, string reason, QpcTimeStamp qpcTimeStamp)
		{
			PortConnectionState entryState = PortConnectionState;
            bool entryStateIsConnected = PortConnectionState.IsConnected();

            PortConnectionState = state;
            PortConnectionStateTimeStamp = qpcTimeStamp;

			Log.Info.Emit("Connection state changed to:'{0}' [from:'{1}' reason:'{2}']", state, entryState, reason);

			if (!PortConnectionState.IsSelected())
			{
				string cancelReason = "PortConnectionState set to non-selected state: " + state.ToString();

				CancelPendingSendMessageOps(cancelReason);
			}

			// perform first cut update of BaseState Use and Conn states from our PortConnectionState
			switch (PortConnectionState)
			{
                case PortConnectionState.Initial: SetBaseState(Modular.Part.UseState.Initial, Modular.Part.ConnState.Initial, reason, true); break;
                case PortConnectionState.OutOfService: SetBaseState(Modular.Part.UseState.Offline, Modular.Part.ConnState.Disconnected, reason, true); break;
                case PortConnectionState.NotConnected: SetBaseState(Modular.Part.UseState.Online, Modular.Part.ConnState.WaitingForConnect, reason, true); break;
                case PortConnectionState.NotSelected: SetBaseState(Modular.Part.UseState.Online, Modular.Part.ConnState.Connected, reason, true); break;
                case PortConnectionState.Connecting: SetBaseState(Modular.Part.UseState.Online, Modular.Part.ConnState.Connected, reason, true); break;
                case PortConnectionState.Selecting: SetBaseState(Modular.Part.UseState.Online, Modular.Part.ConnState.Connected, reason, true); break;
                case PortConnectionState.Selected: SetBaseState(Modular.Part.UseState.Online, Modular.Part.ConnState.Connected, reason, true); break;
                case PortConnectionState.Deselecting: SetBaseState(Modular.Part.UseState.Online, Modular.Part.ConnState.Connected, reason, true); break;
                case PortConnectionState.Failed: SetBaseState(Modular.Part.UseState.FailedToOffline, Modular.Part.ConnState.ConnectionFailed, reason, true); break;
                default: SetBaseState(Modular.Part.UseState.Undefined, Modular.Part.ConnState.Undefined, reason, true); break;
			}
		}

		#endregion

        #region abstract methods that derived class must implement

        protected abstract void PrepareTenByteHeader(IMessage mesg);
        protected abstract void ServiceReceiver(QpcTimeStamp qpcTimeStamp);
        protected abstract void ServiceTransmitter(QpcTimeStamp qpcTimeStamp);
        protected abstract void ServiceConnection(QpcTimeStamp qpcTimeStamp);

        #endregion
    }

	#endregion

	//-------------------------------------------------------------------
}